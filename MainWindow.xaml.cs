using Newtonsoft.Json.Linq;
using RCLayoutPreview.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private string currentJsonPath;
        private JObject jsonData;
        private EditorWindow editorWindow;
        private bool placeholderRemoved = false;
        private HashSet<string> usedElementNames = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            LoadStubData();

            // Create and show editor window
            editorWindow = new EditorWindow(this);
            editorWindow.XamlContentChanged += EditorWindow_XamlContentChanged;
            editorWindow.JsonDataChanged += EditorWindow_JsonDataChanged;
            editorWindow.Show();

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogStatus("Layout initialized.");
        }

        private void EditorWindow_XamlContentChanged(object sender, string xamlContent)
        {
            TryPreviewXaml(xamlContent);
        }

        private void EditorWindow_JsonDataChanged(object sender, JObject newJsonData)
        {
            jsonData = newJsonData;
            // If we have current XAML content, refresh the preview with new data
            if (PreviewHost?.Content is FrameworkElement frameworkElement)
            {
                frameworkElement.DataContext = jsonData;
                XamlFixer.ProcessNamedFields(frameworkElement, jsonData, DebugModeToggle.IsChecked == true);
            }
        }

        private void TryPreviewXaml(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
            {
                LogStatus("XAML content is empty or null.");
                return;
            }

            // --- Begin: Duplicate field name detection ---
            var nameRegex = new Regex("Name=\"([^\"]+)\"");
            var nameMatches = nameRegex.Matches(xamlContent);
            var nameSet = new HashSet<string>();
            var duplicateNames = new List<string>();
            foreach (Match match in nameMatches)
            {
                string name = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // Allow duplicate placeholders (e.g., Placeholder1, Placeholder2, Placeholder3)
                    if (name.StartsWith("Placeholder"))
                        continue;
                    // Only error if the exact same field name (including suffix) is duplicated
                    if (nameSet.Contains(name))
                    {
                        duplicateNames.Add(name);
                    }
                    else
                    {
                        nameSet.Add(name);
                    }
                }
            }
            if (duplicateNames.Count > 0)
            {
                ShowErrorPopup($"Error: Duplicate field names detected in XAML: {string.Join(", ", duplicateNames)}. Please ensure all element names are unique.");
                LogStatus($"Duplicate field names found: {string.Join(", ", duplicateNames)}");
                return;
            }
            // --- End: Duplicate field name detection ---

            try
            {
                // Clear any previous content
                PreviewHost.Content = null;
                usedElementNames.Clear();

                // Process the XAML through any utility methods
                string processedXaml = XamlFixer.Preprocess(xamlContent);
                LogStatus("XAML processed for preview");

                // Fix common XAML issues
                if (processedXaml.Contains("FontSize=\"\""))
                {
                    LogStatus("Invalid FontSize detected in XAML. Replacing with default value.");
                    processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
                }

                // Replace any remaining template placeholders
                processedXaml = processedXaml.Replace("{styles}", "");
                processedXaml = processedXaml.Replace("{content}", "");

                // Fix duplicate element names
                processedXaml = EnsureUniqueElementNames(processedXaml);

                // Check if the XAML contains any valid field names and handle the placeholder
                if (PlaceholderSwapManager.ContainsValidField(processedXaml))
                {
                    if (PlaceholderSwapManager.ContainsPlaceholder(processedXaml))
                    {
                        // Generate a message based on the field detected
                        string fieldMessage = PlaceholderSwapManager.GenerateFieldDetectedMessage(processedXaml);
                        
                        if (!string.IsNullOrEmpty(fieldMessage))
                        {
                            // Option 1: Replace placeholder with a message about the detected field
                            processedXaml = PlaceholderSwapManager.ReplacePlaceholderWithMessage(processedXaml, fieldMessage);
                            
                            // Option 2: Remove the placeholder entirely (choose this or the above)
                            // processedXaml = PlaceholderSwapManager.RemovePlaceholder(processedXaml);
                            
                            if (!placeholderRemoved)
                            {
                                LogStatus($"Field detected: {fieldMessage}");
                                placeholderRemoved = true;
                            }
                        }
                    }
                }
                else
                {
                    // Reset the placeholderRemoved flag if no valid fields are present
                    placeholderRemoved = false;
                }

                // Make sure the XAML has a root element that is a FrameworkElement
                if (!IsValidRootElement(processedXaml))
                {
                    processedXaml = $"<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{processedXaml}</Grid>";
                    LogStatus("Added Grid container to wrap content");
                }

                // Make sure namespaces are present
                if (!processedXaml.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""))
                {
                    string rootTag = Regex.Match(processedXaml, @"<(\w+)").Groups[1].Value;
                    string xmlnsDeclaration = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                    processedXaml = Regex.Replace(processedXaml,
                        $"<{rootTag}",
                        $"<{rootTag} {xmlnsDeclaration}");
                    LogStatus("Added XAML namespaces to content");
                }

                // Fix any Binding expressions that might be causing issues
                processedXaml = FixBindingExpressions(processedXaml);

                // Detect and handle empty Name attributes before parsing
                var emptyNameMatches = Regex.Matches(processedXaml, "Name\\s*=\\s*\"\\s*\"");
                if (emptyNameMatches.Count > 0)
                {
                    ShowErrorPopup($"Warning: {emptyNameMatches.Count} empty Name attribute(s) were found and removed from the XAML. Please check your layout for missing names.");
                    processedXaml = Regex.Replace(processedXaml, "Name\\s*=\\s*\"\\s*\"", "");
                }

                object element = null;

                try
                {
                    // Try direct parsing first
                    element = XamlReader.Parse(processedXaml);
                    LogStatus("XAML parsed successfully with XamlReader.Parse");
                }
                catch (Exception parseEx)
                {
                    LogStatus($"Direct parsing failed: {parseEx.Message}. Trying alternate method...");

                    try
                    {
                        // Create a proper ParserContext for better control
                        var context = new ParserContext
                        {
                            BaseUri = new Uri("pack://application:,,,/")
                        };
                        context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                        context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

                        // Use memory stream approach
                        byte[] bytes = Encoding.UTF8.GetBytes(processedXaml);
                        using (var stream = new MemoryStream(bytes))
                        {
                            element = XamlReader.Load(stream, context);
                            LogStatus("XAML parsed successfully with XamlReader.Load");
                        }
                    }
                    catch (Exception loadEx)
                    {
                        LogStatus($"All parsing attempts failed: {loadEx.Message}");
                        throw new XamlParseException($"Failed to parse XAML: {parseEx.Message}", parseEx);
                    }
                }

                // Handle Window elements
                if (element is Window window)
                {
                    LogStatus("XAML contains a Window element. Extracting content.");
                    element = window.Content;
                }

                // Set content if it's a FrameworkElement
                if (element is FrameworkElement frameworkElement)
                {
                    if (jsonData == null)
                    {
                        LogStatus("JSON data is null. Cannot bind values.");
                        PreviewHost.Content = frameworkElement;
                        return;
                    }

                    frameworkElement.DataContext = jsonData;
                    LogStatus("Processing named fields...");
                    XamlFixer.ProcessNamedFields(frameworkElement, jsonData, DebugModeToggle.IsChecked == true);

                    PreviewHost.Content = frameworkElement;
                    LogStatus("Preview updated successfully.");
                }
                else if (element != null)
                {
                    LogStatus($"Parsed XAML is not a FrameworkElement. Found: {element.GetType().Name}");
                }
                else
                {
                    LogStatus("Failed to parse XAML into an element.");
                }
            }
            catch (XamlParseException ex)
            {
                ShowErrorPopup($"XAML parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorPopup($"Preview error: {ex.Message}");
            }
        }

        // New method to ensure unique element names
        private string EnsureUniqueElementNames(string xaml)
        {
            // Regular expression to find Name attributes
            var nameRegex = new Regex(@"Name=""([^""]+)""");
            
            // Use a match evaluator to replace names that are duplicates
            return nameRegex.Replace(xaml, match => {
                string originalName = match.Groups[1].Value;
                string uniqueName = originalName;
                int counter = 1;
                
                // If this name is already used, generate a unique one by adding a suffix
                while (usedElementNames.Contains(uniqueName))
                {
                    uniqueName = $"{originalName}_{counter++}";
                }
                
                // Add the unique name to our used names collection
                usedElementNames.Add(uniqueName);
                
                // Return the attribute with the potentially modified name
                return $"Name=\"{uniqueName}\"";
            });
        }

        // Helper method to fix binding expressions that might be causing issues
        private string FixBindingExpressions(string xaml)
        {
            // Fix any unescaped binding expressions
            xaml = Regex.Replace(xaml, "{Binding([^}]*)}", m =>
            {
                // Check if this is already properly escaped
                if (xaml.IndexOf(m.Value) > 0 && xaml[xaml.IndexOf(m.Value) - 1] == '{')
                    return m.Value; // Already escaped

                return "{Binding" + m.Groups[1].Value + "}";
            });

            return xaml;
        }

        private bool IsValidRootElement(string xaml)
        {
            // Check if the XAML has a valid root element
            string pattern = @"^\s*<\s*([a-zA-Z0-9_]+)";
            Match match = Regex.Match(xaml, pattern);
            if (match.Success)
            {
                string rootElement = match.Groups[1].Value;
                switch (rootElement.ToLower())
                {
                    case "grid":
                    case "stackpanel":
                    case "border":
                    case "dockpanel":
                    case "canvas":
                    case "wrappanel":
                    case "viewbox":
                    case "window":
                    case "page":
                    case "usercontrol":
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }

        public static T FindElementByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name)
                    return fe;

                var result = FindElementByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void ShowErrorPopup(string errorMessage)
        {
            PopupMessage.Text = errorMessage;
            PopupOverlay.Visibility = Visibility.Visible;
            LogStatus("Popup overlay displayed with message: " + errorMessage);
        }

        private void PopupOkButton_Click(object sender, RoutedEventArgs e)
        {
            PopupOverlay.Visibility = Visibility.Collapsed;
        }

        private void LoadStubData()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LogStatus($"Base directory: {baseDirectory}");

            string jsonPath = Path.Combine(baseDirectory, "stubdata5.json");
            LogStatus($"Checking path: {jsonPath}");

            if (File.Exists(jsonPath))
            {
                try
                {
                    currentJsonPath = jsonPath;
                    string jsonContent = File.ReadAllText(jsonPath);
                    jsonData = JObject.Parse(jsonContent);
                    LogStatus($"Loaded JSON: {Path.GetFileName(jsonPath)}");
                }
                catch (Exception ex)
                {
                    LogStatus($"Error parsing JSON file: {ex.Message}");
                }
            }
            else
            {
                LogStatus($"File does not exist: {jsonPath}");
            }
        }

        private void LogStatus(string message)
        {
            if (StatusLabel != null)
            {
                StatusLabel.Text = message;
            }
            Console.WriteLine($"Status: {message}");
        }

        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            var debugMode = (sender as CheckBox)?.IsChecked == true;
            LogStatus(debugMode ? "Debug mode enabled" : "Debug mode disabled");

            if (PreviewHost?.Content is FrameworkElement frameworkElement && jsonData != null)
            {
                XamlFixer.ProcessNamedFields(frameworkElement, jsonData, debugMode);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Close the editor window when the preview window is closed
            editorWindow.Close();
            base.OnClosing(e);
        }
    }
}
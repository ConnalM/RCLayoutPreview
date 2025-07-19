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
using System.Linq;
using System.Diagnostics;

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private string currentJsonPath;
        private JObject jsonData;
        private EditorWindow editorWindow;
        private bool placeholderRemoved = false;
        private HashSet<string> usedElementNames = new HashSet<string>();
        private FrameworkElement currentHighlightedElement;
        private System.Windows.Controls.ToolTip currentToolTip;
        private static int _operationCounter = 0;

        public MainWindow()
        {
            LogPerformance("MainWindow constructor start");
            InitializeComponent();
            LoadStubData();

            // Create and show editor window
            editorWindow = new EditorWindow(this);
            editorWindow.XamlContentChanged += EditorWindow_XamlContentChanged;
            editorWindow.JsonDataChanged += EditorWindow_JsonDataChanged;
            editorWindow.Show();

            this.Loaded += MainWindow_Loaded;
            LogPerformance("MainWindow constructor end");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogStatus("Layout initialized.");
        }

        private void EditorWindow_XamlContentChanged(object sender, string xamlContent)
        {
            var opId = ++_operationCounter;
            LogPerformance($"[{opId}] XAML content changed - start processing");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                LogStatus($"[{opId}] XAML content changed, updating preview...");
                TryPreviewXaml(xamlContent, opId);
                stopwatch.Stop();
                LogPerformance($"[{opId}] XAML content changed - completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"[{opId}] Error handling XAML change after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}\nStack: {ex.StackTrace}";
                Debug.WriteLine(error);
                LogStatus($"Error updating XAML: {ex.Message}");
                ShowErrorPopup($"Error updating XAML: {ex.Message}");
            }
            LogPerformance($"[{opId}] EditorWindow_XamlContentChanged - FINAL EXIT");
        }

        private void EditorWindow_JsonDataChanged(object sender, JObject newJsonData)
        {
            var opId = ++_operationCounter;
            LogPerformance($"[{opId}] JSON data changed - start processing");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                LogStatus($"[{opId}] JSON data changed, updating preview...");
                jsonData = newJsonData;
                
                if (PreviewHost?.Content is FrameworkElement frameworkElement)
                {
                    frameworkElement.DataContext = jsonData;
                    UpdatePreviewFields(frameworkElement, opId);
                }
                else
                {
                    LogStatus("Warning: No preview content to update");
                }
                
                stopwatch.Stop();
                LogPerformance($"[{opId}] JSON data changed - completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"[{opId}] Error handling JSON change after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}\nStack: {ex.StackTrace}";
                Debug.WriteLine(error);
                LogStatus($"Error updating with new JSON: {ex.Message}");
                ShowErrorPopup($"Error updating with new JSON: {ex.Message}");
            }
        }

        private void UpdatePreviewFields(FrameworkElement frameworkElement, int opId = 0)
        {
            if (frameworkElement == null)
            {
                LogPerformance($"[{opId}] UpdatePreviewFields - frameworkElement is null");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            LogPerformance($"[{opId}] UpdatePreviewFields start - element type: {frameworkElement.GetType().Name}");
            
            try
            {
                if (jsonData == null)
                {
                    LogStatus("Warning: JSON data is null");
                    return;
                }

                var debugMode = DebugModeToggle?.IsChecked ?? false;
                LogPerformance($"[{opId}] About to call ProcessNamedFields - debug mode: {debugMode}");
                
                XamlFixer.ProcessNamedFields(frameworkElement, jsonData, debugMode);
                
                stopwatch.Stop();
                LogPerformance($"[{opId}] UpdatePreviewFields completed in {stopwatch.ElapsedMilliseconds}ms");
                LogStatus($"[{opId}] Updated preview fields on {frameworkElement.GetType().Name}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"[{opId}] Exception in UpdatePreviewFields after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}\nStack: {ex.StackTrace}";
                Debug.WriteLine(error);
                LogStatus($"Error updating preview: {ex.Message}");
                ShowErrorPopup($"Error updating preview: {ex.Message}");
            }
        }

        private void TryPreviewXaml(string xamlContent, int opId = 0)
        {
            var stopwatch = Stopwatch.StartNew();
            LogPerformance($"[{opId}] TryPreviewXaml start - content length: {xamlContent?.Length ?? 0}");
            
            try
            {
                if (string.IsNullOrWhiteSpace(xamlContent))
                {
                    LogStatus("XAML content is empty or null.");
                    return;
                }

                // Step 1: Clean placeholders
                LogPerformance($"[{opId}] Step 1: Cleaning placeholders");
                xamlContent = Regex.Replace(xamlContent, @"<([a-zA-Z0-9_]+)\s*([^>]*)?\{[a-zA-Z0-9_]+\}([^>]*)?>", 
                    m => $"<!-- Invalid tag removed: {m.Value} -->");
                xamlContent = Regex.Replace(xamlContent, @"\{[a-zA-Z0-9_]+\}", "");

                // Step 2: Duplicate field name detection
                LogPerformance($"[{opId}] Step 2: Duplicate name detection");
                var nameRegex = new Regex(@"Name=""([^""]+)""");
                var nameMatches = nameRegex.Matches(xamlContent);
                var nameSet = new HashSet<string>();
                var duplicateNames = new List<string>();
                
                foreach (Match match in nameMatches)
                {
                    string name = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("Placeholder"))
                    {
                        if (nameSet.Contains(name))
                            duplicateNames.Add(name);
                        else
                            nameSet.Add(name);
                    }
                }
                
                if (duplicateNames.Count > 0)
                {
                    ShowErrorPopup($"Error: Duplicate field names detected in XAML: {string.Join(", ", duplicateNames)}");
                    LogStatus($"Duplicate field names found: {string.Join(", ", duplicateNames)}");
                    return;
                }

                // Step 3: Clear previous content
                LogPerformance($"[{opId}] Step 3: Clearing previous content");
                PreviewHost.Content = null;
                usedElementNames.Clear();

                // Step 4: Process XAML
                LogPerformance($"[{opId}] Step 4: Processing XAML");
                string processedXaml = XamlFixer.Preprocess(xamlContent);
                LogStatus("XAML processed for preview");

                // Step 5: Fix common issues
                LogPerformance($"[{opId}] Step 5: Fixing common issues");
                if (processedXaml.Contains("FontSize=\"\""))
                {
                    processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
                }

                processedXaml = processedXaml.Replace("{styles}", "");
                processedXaml = processedXaml.Replace("{content}", "");
                processedXaml = EnsureUniqueElementNames(processedXaml);

                // Step 6: Handle placeholders
                LogPerformance($"[{opId}] Step 6: Handling placeholders");
                if (PlaceholderSwapManager.ContainsValidField(processedXaml))
                {
                    try
                    {
                        string fieldMessage = PlaceholderSwapManager.GenerateFieldDetectedMessage(processedXaml);
                        if (!string.IsNullOrEmpty(fieldMessage) && !placeholderRemoved)
                        {
                            LogStatus($"Field detected: {fieldMessage}");
                            placeholderRemoved = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error generating field message: {ex.Message}");
                    }
                }
                else
                {
                    placeholderRemoved = false;
                }

                // Step 7: Ensure valid structure
                LogPerformance($"[{opId}] Step 7: Ensuring valid structure");
                if (!IsValidRootElement(processedXaml))
                {
                    processedXaml = $"<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{processedXaml}</Grid>";
                }

                if (!processedXaml.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""))
                {
                    string rootTag = Regex.Match(processedXaml, @"<(\w+)").Groups[1].Value;
                    string xmlnsDeclaration = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                    processedXaml = Regex.Replace(processedXaml, $"<{rootTag}", $"<{rootTag} {xmlnsDeclaration}");
                }

                processedXaml = FixBindingExpressions(processedXaml);
                
                var emptyNameMatches = Regex.Matches(processedXaml, @"Name\s*=\s*""\s*""");
                if (emptyNameMatches.Count > 0)
                {
                    processedXaml = Regex.Replace(processedXaml, @"Name\s*=\s*""\s*""", "");
                }

                // Step 8: Parse XAML
                LogPerformance($"[{opId}] Step 8: Parsing XAML");
                object element = null;
                try
                {
                    element = XamlReader.Parse(processedXaml);
                    LogStatus("XAML parsed successfully");
                }
                catch (Exception parseEx)
                {
                    LogStatus($"Direct parsing failed: {parseEx.Message}. Trying alternate method...");
                    var context = new ParserContext
                    {
                        BaseUri = new Uri("pack://application:,,,/")
                    };
                    context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                    context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(processedXaml)))
                    {
                        element = XamlReader.Load(stream, context);
                        LogStatus("XAML parsed successfully with alternate method");
                    }
                }

                // Step 9: Handle parsed element
                LogPerformance($"[{opId}] Step 9: Handling parsed element");
                if (element is Window window)
                {
                    LogStatus("XAML contains a Window element");
                    var content = window.Content as FrameworkElement;
                    if (content != null)
                    {
                        Debug.WriteLine($"[TryPreviewXaml] Window content extracted: {content.GetType().Name}");
                        Debug.WriteLine($"[TryPreviewXaml] Window content is: {content}");
                        
                        // Check if the content has children BEFORE we extract it
                        if (content is Panel panel)
                        {
                            Debug.WriteLine($"[TryPreviewXaml] Content Panel has {panel.Children.Count} children");
                            for (int i = 0; i < panel.Children.Count; i++)
                            {
                                var child = panel.Children[i];
                                Debug.WriteLine($"[TryPreviewXaml] Child {i}: {child.GetType().Name} - Name: '{(child as FrameworkElement)?.Name ?? "(no name)"}'");
                            }
                        }
                        else if (content is ContentControl cc && cc.Content != null)
                        {
                            Debug.WriteLine($"[TryPreviewXaml] ContentControl contains: {cc.Content.GetType().Name}");
                        }
                        else
                        {
                            Debug.WriteLine($"[TryPreviewXaml] Content has no obvious children to traverse");
                        }
                        
                        AddHoverBehavior(content);
                        PreviewHost.Content = content;
                        content.DataContext = jsonData;
                        
                        LogPerformance($"[{opId}] About to update fields for Window content");
                        UpdatePreviewFields(content, opId);
                    }
                    else
                    {
                        Debug.WriteLine($"[TryPreviewXaml] Window content is null or not a FrameworkElement");
                    }
                    ApplyWindowProperties(window);
                }
                else if (element is FrameworkElement fe)
                {
                    AddHoverBehavior(fe);
                    PreviewHost.Content = fe;
                    LogStatus("Preview updated with parsed element");
                    fe.DataContext = jsonData;
                    
                    LogPerformance($"[{opId}] About to update fields for FrameworkElement");
                    UpdatePreviewFields(fe, opId);
                }
                
                stopwatch.Stop();
                LogPerformance($"[{opId}] TryPreviewXaml completed in {stopwatch.ElapsedMilliseconds}ms");
                LogPerformance($"[{opId}] METHOD COMPLETED - RETURNING TO CALLER");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"[{opId}] Error previewing XAML after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}\nStack: {ex.StackTrace}";
                Debug.WriteLine(error);
                LogStatus($"Preview error: {ex.Message}");
                ShowErrorPopup($"Preview error: {ex.Message}");
            }
        }

        private void ApplyWindowProperties(Window window)
        {
            if (window.Title != null)
            {
                Title = window.Title;
                LogStatus($"Window Title applied: {window.Title}");
            }

            if (ApplyWindowSizeToggle?.IsChecked == true)
            {
                if (window.Width > 0) Width = window.Width;
                if (window.Height > 0) Height = window.Height;
            }

            if (window.Background != null)
            {
                Background = window.Background;
            }
        }

        private void LoadStubData()
        {
            LogPerformance("LoadStubData start");
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string jsonPath = Path.Combine(baseDirectory, "stubdata5.json");

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
            LogPerformance("LoadStubData end");
        }

        private void LogStatus(string message)
        {
            try
            {
                if (StatusLabel != null)
                    StatusLabel.Text = message;
                Debug.WriteLine($"Status: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error logging status: {ex.Message}");
            }
        }

        private void LogPerformance(string message)
        {
            Debug.WriteLine($"PERF: {DateTime.Now:HH:mm:ss.fff} - {message}");
        }

        private void ShowErrorPopup(string errorMessage)
        {
            try
            {
                if (PopupMessage != null && PopupOverlay != null)
                {
                    PopupMessage.Text = errorMessage;
                    PopupOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    Debug.WriteLine($"Cannot show error popup: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing popup: {ex.Message}");
            }
        }

        private void PopupOkButton_Click(object sender, RoutedEventArgs e)
        {
            PopupOverlay.Visibility = Visibility.Collapsed;
        }

        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            var opId = ++_operationCounter;
            LogPerformance($"[{opId}] Debug mode toggle start");
            
            try
            {
                var debugMode = (sender as CheckBox)?.IsChecked ?? false;
                LogStatus(debugMode ? "Debug mode enabled" : "Debug mode disabled");
                
                // Add detailed logging about debug mode state
                Debug.WriteLine($"[DebugModeToggle] Debug mode is now: {debugMode}");
                Debug.WriteLine($"[DebugModeToggle] This will show {(debugMode ? "field names" : "actual values")} in preview");

                if (PreviewHost?.Content is FrameworkElement frameworkElement && jsonData != null)
                {
                    Debug.WriteLine($"[DebugModeToggle] Re-processing {frameworkElement.GetType().Name} with debug mode: {debugMode}");
                    UpdatePreviewFields(frameworkElement, opId);
                }
                else
                {
                    Debug.WriteLine($"[DebugModeToggle] No content to update - PreviewHost.Content: {PreviewHost?.Content?.GetType().Name ?? "null"}");
                }
                
                LogPerformance($"[{opId}] Debug mode toggle end");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{opId}] Error in debug mode toggle: {ex.Message}");
                ShowErrorPopup($"Error updating debug mode: {ex.Message}");
            }
        }

        private string EnsureUniqueElementNames(string xaml)
        {
            var nameRegex = new Regex(@"Name=""([^""]+)""");
            return nameRegex.Replace(xaml, match => {
                string originalName = match.Groups[1].Value;
                string uniqueName = originalName;
                int counter = 1;
                
                while (usedElementNames.Contains(uniqueName))
                    uniqueName = $"{originalName}_{counter++}";
                
                usedElementNames.Add(uniqueName);
                return $"Name=\"{uniqueName}\"";
            });
        }

        private bool IsValidRootElement(string xaml)
        {
            string pattern = @"^\s*<\s*([a-zA-Z0-9_]+)";
            Match match = Regex.Match(xaml, pattern);
            if (match.Success)
            {
                string rootElement = match.Groups[1].Value.ToLower();
                return rootElement switch
                {
                    "grid" or "stackpanel" or "border" or "dockpanel" or
                    "canvas" or "wrappanel" or "viewbox" or "window" or
                    "page" or "usercontrol" => true,
                    _ => false
                };
            }
            return false;
        }

        private string FixBindingExpressions(string xaml)
        {
            return Regex.Replace(xaml, "{Binding([^}]*)}", m =>
            {
                if (xaml.IndexOf(m.Value) > 0 && xaml[xaml.IndexOf(m.Value) - 1] == '{')
                    return m.Value;
                return "{Binding" + m.Groups[1].Value + "}";
            });
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

        private void AddHoverBehavior(FrameworkElement element)
        {
            if (element == null) return;
            
            Debug.WriteLine($"AddHoverBehavior: Adding safe hover behavior to {element.GetType().Name}");
            
            // Use a simple, safe approach - only add hover to the PreviewHost itself
            // This avoids the complex recursive traversal that was causing infinite loops
            try
            {
                if (PreviewHost != null)
                {
                    PreviewHost.MouseMove -= PreviewHost_SafeMouseMove;
                    PreviewHost.MouseMove += PreviewHost_SafeMouseMove;
                    PreviewHost.MouseLeave -= PreviewHost_MouseLeave;
                    PreviewHost.MouseLeave += PreviewHost_MouseLeave;
                    Debug.WriteLine("Safe mouse handlers added to PreviewHost");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding safe hover behavior: {ex.Message}");
            }
        }

        private void PreviewHost_SafeMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                var hitTestResult = VisualTreeHelper.HitTest(PreviewHost, e.GetPosition(PreviewHost));
                
                if (hitTestResult?.VisualHit is FrameworkElement element)
                {
                    var namedElement = FindNamedElementSafe(element);
                    
                    if (namedElement != null && namedElement != currentHighlightedElement)
                    {
                        Debug.WriteLine($"Found named element: {namedElement.Name} ({namedElement.GetType().Name})");
                        
                        // Clear previous highlight
                        if (currentHighlightedElement != null)
                            currentHighlightedElement.Effect = null;

                        // Apply new highlight
                        currentHighlightedElement = namedElement;
                        currentHighlightedElement.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Yellow,
                            ShadowDepth = 0,
                            BlurRadius = 15,
                            Opacity = 0.5
                        };

                        // Show tooltip with element info
                        ShowElementTooltip(namedElement);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in safe mouse move: {ex.Message}");
            }
        }

        private void PreviewHost_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                // Clear highlight when mouse leaves PreviewHost
                if (currentHighlightedElement != null)
                {
                    currentHighlightedElement.Effect = null;
                    currentHighlightedElement = null;
                }
                HideElementTooltip();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in mouse leave: {ex.Message}");
            }
        }

        private FrameworkElement FindNamedElementSafe(FrameworkElement element)
        {
            // Strategy: 
            // 1. First try to find a more specific child element under the mouse
            // 2. If no child found, then look up the tree for a container
            
            try
            {
                // Step 1: Look DOWN the tree for the most specific named element
                var childElement = FindNamedChildElement(element, 5); // Limit depth to 5 levels
                if (childElement != null)
                {
                    Debug.WriteLine($"Found child element: {childElement.Name} ({childElement.GetType().Name})");
                    return childElement;
                }

                // Step 2: Look UP the tree for a container (original behavior)
                int maxLevels = 10;
                var current = element;
                
                for (int level = 0; level < maxLevels && current != null; level++)
                {
                    if (!string.IsNullOrEmpty(current.Name) && !(current is Viewbox))
                    {
                        Debug.WriteLine($"Found parent element: {current.Name} ({current.GetType().Name})");
                        return current;
                    }
                    
                    current = VisualTreeHelper.GetParent(current) as FrameworkElement;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindNamedElementSafe: {ex.Message}");
                return null;
            }
        }

        private FrameworkElement FindNamedChildElement(DependencyObject parent, int maxDepth)
        {
            if (parent == null || maxDepth <= 0) return null;

            try
            {
                // First check if the current element is a named FrameworkElement
                if (parent is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name) && !(fe is Viewbox))
                    return fe;

                // Then search children
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    var result = FindNamedChildElement(child, maxDepth - 1);
                    if (result != null)
                        return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindNamedChildElement: {ex.Message}");
                return null;
            }
        }

        private void ShowElementTooltip(FrameworkElement element)
        {
            try
            {
                var info = BuildElementInfoSafe(element);
                Debug.WriteLine($"Showing tooltip for {element.Name}: {info}");
                
                if (currentToolTip == null)
                {
                    currentToolTip = new System.Windows.Controls.ToolTip
                    {
                        Background = Brushes.LightYellow,
                        BorderBrush = Brushes.DarkGray,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 12,
                        HasDropShadow = true,
                        StaysOpen = false,
                        PlacementTarget = PreviewHost,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                    };
                }

                currentToolTip.Content = info;
                currentToolTip.IsOpen = true;
                Debug.WriteLine("Tooltip opened successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing tooltip: {ex.Message}");
            }
        }

        private void HideElementTooltip()
        {
            try
            {
                if (currentToolTip != null)
                {
                    currentToolTip.IsOpen = false;
                    Debug.WriteLine("Tooltip hidden");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding tooltip: {ex.Message}");
            }
        }

        private string BuildElementInfoSafe(FrameworkElement element)
        {
            try
            {
                var info = new StringBuilder();
                info.AppendLine($"Type: {element.GetType().Name}");
                
                if (!string.IsNullOrEmpty(element.Name))
                    info.AppendLine($"Name: {element.Name}");
                
                info.AppendLine($"Size: {element.ActualWidth:F0} x {element.ActualHeight:F0}");
                
                // Enhanced content extraction
                switch (element)
                {
                    case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text):
                        string preview = tb.Text.Length > 50 ? tb.Text.Substring(0, 47) + "..." : tb.Text;
                        info.AppendLine($"Text: \"{preview}\"");
                        if (tb.FontSize > 0) info.AppendLine($"FontSize: {tb.FontSize}");
                        break;
                        
                    case Label label when label.Content != null:
                        string content = label.Content.ToString();
                        string labelPreview = content.Length > 50 ? content.Substring(0, 47) + "..." : content;
                        info.AppendLine($"Content: \"{labelPreview}\"");
                        if (label.FontSize > 0) info.AppendLine($"FontSize: {label.FontSize}");
                        break;
                        
                    case Image image when image.Source != null:
                        info.AppendLine($"Source: {System.IO.Path.GetFileName(image.Source.ToString())}");
                        break;
                        
                    case Panel panel:
                        int childCount = panel.Children.Count;
                        info.AppendLine($"Children: {childCount}");
                        
                        // Show first few named children
                        var namedChildren = panel.Children.OfType<FrameworkElement>()
                            .Where(child => !string.IsNullOrEmpty(child.Name))
                            .Take(3)
                            .ToList();
                            
                        if (namedChildren.Any())
                        {
                            info.AppendLine("Named Children:");
                            foreach (var child in namedChildren)
                            {
                                info.AppendLine($"  • {child.Name} ({child.GetType().Name})");
                            }
                            
                            if (panel.Children.OfType<FrameworkElement>().Count(c => !string.IsNullOrEmpty(c.Name)) > 3)
                            {
                                info.AppendLine("  • ... (more)");
                            }
                        }
                        break;
                        
                    case ContentControl cc when cc.Content is FrameworkElement contentElement:
                        info.AppendLine($"Content: {contentElement.GetType().Name}");
                        if (!string.IsNullOrEmpty(contentElement.Name))
                            info.AppendLine($"Content Name: {contentElement.Name}");
                        break;
                }
                
                // Add layout information
                if (element.Parent is Panel parentPanel)
                {
                    int index = parentPanel.Children.IndexOf(element);
                    if (index >= 0)
                        info.AppendLine($"Position in Parent: {index + 1} of {parentPanel.Children.Count}");
                }

                // Add Grid position if applicable
                if (Grid.GetRow(element) >= 0 || Grid.GetColumn(element) >= 0)
                {
                    info.AppendLine($"Grid Position: Row {Grid.GetRow(element)}, Column {Grid.GetColumn(element)}");
                }
                
                return info.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error building element info: {ex.Message}");
                return $"Type: {element.GetType().Name}\nError: Could not retrieve details";
            }
        }
    }
}
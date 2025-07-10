using Newtonsoft.Json.Linq;
using RCLayoutPreview.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private string currentJsonPath;
        private JObject jsonData;
        private DispatcherTimer previewTimer;
        private string lastEditorContent = string.Empty;
        private TextBlock statusLabel;
        private bool autoUpdateEnabled = true; // Flag to control auto-update
        private int previewDelayMilliseconds = 3000; // Configurable delay in milliseconds
        private DateTime lastEditTime;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize UI elements
            statusLabel = FindName("StatusLabel") as TextBlock;
            HideButton("Load JSON");
            HideButton("Preview");

            // Load JSON data automatically
            LoadStubData();

            // Clear the preview area initially
            var previewHost = FindName("PreviewHost") as ContentControl;
            if (previewHost != null)
            {
                previewHost.Content = null;
            }

            // Set up timer for automatic preview
            previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            previewTimer.Tick += AutoPreviewTick;
            previewTimer.Start();
        }

        private void HideButton(string buttonContent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is DockPanel dockPanel)
                {
                    foreach (var dockChild in LogicalTreeHelper.GetChildren(dockPanel))
                    {
                        if (dockChild is StackPanel stackPanel)
                        {
                            foreach (var stackChild in LogicalTreeHelper.GetChildren(stackPanel))
                            {
                                if (stackChild is Button button && button.Content.ToString() == buttonContent)
                                {
                                    // Remove hiding logic for Preview button
                                    if (buttonContent == "Preview")
                                    {
                                        button.Visibility = Visibility.Visible;
                                        return;
                                    }

                                    button.Visibility = Visibility.Collapsed;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AutoPreviewTick(object sender, EventArgs e)
        {
            if (!autoUpdateEnabled) return; // Skip auto-update if disabled

            var editor = FindName("Editor") as TextBox;
            if (editor != null)
            {
                string currentContent = editor.Text;
                if (currentContent != lastEditorContent && !string.IsNullOrWhiteSpace(currentContent))
                {
                    lastEditTime = DateTime.Now;
                    lastEditorContent = currentContent;
                }

                // Check if the delay has elapsed since the last edit
                if ((DateTime.Now - lastEditTime).TotalMilliseconds >= previewDelayMilliseconds)
                {
                    TryPreviewXaml(currentContent);
                }
            }
        }

        private void TryPreviewXaml(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
            {
                LogStatus("XAML content is empty or null.");
                return;
            }

            try
            {
                var previewHost = FindName("PreviewHost") as ContentControl;
                if (previewHost == null)
                {
                    LogStatus("Preview host is not found.");
                    return;
                }

                // Clear the preview host before updating
                previewHost.Content = null;

                string processedXaml = XamlFixer.Preprocess(xamlContent);
                LogStatus("XAML processed for preview");

                // Validate XAML for common issues
                if (processedXaml.Contains("FontSize=\"\""))
                {
                    LogStatus("Invalid FontSize detected in XAML. Replacing with default value.");
                    processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
                }

                object element = System.Windows.Markup.XamlReader.Parse(processedXaml);
                if (element is Window window)
                {
                    LogStatus("XAML contains a Window element. Extracting content.");
                    element = window.Content;
                }

                if (element is FrameworkElement frameworkElement)
                {
                    if (jsonData == null)
                    {
                        LogStatus("JSON data is null. Cannot bind values.");
                        return;
                    }

                    // Apply JSON data to the DataContext
                    frameworkElement.DataContext = jsonData;

                    // Use XamlFixer to process named fields
                    LogStatus("Processing named fields...");
                    XamlFixer.ProcessNamedFields(frameworkElement, jsonData, debugMode: true);

                    previewHost.Content = frameworkElement;
                    LogStatus("Preview updated successfully.");
                }
                else
                {
                    LogStatus("Parsed XAML does not contain a valid FrameworkElement.");
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
            var layoutRoot = PreviewHost.Content as FrameworkElement;
            var popupOverlay = FindElementByName<Grid>(layoutRoot, "PopupOverlay");
            var popupMessage = FindElementByName<TextBlock>(layoutRoot, "PopupMessage");

            if (popupOverlay == null)
            {
                LogStatus("PopupOverlay not found in the visual tree.");
                return;
            }

            if (popupMessage == null)
            {
                LogStatus("PopupMessage not found in the visual tree.");
                return;
            }

            popupMessage.Text = errorMessage;
            Dispatcher.BeginInvoke(() =>
            {
                popupOverlay.Visibility = Visibility.Visible;
                popupOverlay.UpdateLayout(); // Force layout update
                LogStatus("PopupOverlay visibility set to Visible.");
            });
            LogStatus("Popup overlay displayed with message: " + errorMessage);
        }

        private void PopupOkButton_Click(object sender, RoutedEventArgs e)
        {
            var popupOverlay = FindName("PopupOverlay") as Grid;
            if (popupOverlay != null)
            {
                popupOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadStubData()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LogStatus($"Base directory: {baseDirectory}");

            string jsonPath = Path.Combine(baseDirectory, "stubdata4.json");
            LogStatus($"Checking path: {jsonPath}");

            if (File.Exists(jsonPath))
            {
                try
                {
                    currentJsonPath = jsonPath;
                    string jsonContent = File.ReadAllText(jsonPath);

                    // Clear and reload JSON data
                    jsonData = JObject.Parse(jsonContent);
                    LogStatus($"Loaded JSON: {Path.GetFileName(jsonPath)}");

                    return;
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

            LogStatus("No JSON data files found.");
        }

        private void LogStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = message;
            }
            Console.WriteLine($"Status: {message}");
        }

        private void LoadLayout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XAML Layout (*.xaml)|*.xaml",
                Title = "Select Layout XAML"
            };

            if (dlg.ShowDialog() == true)
            {
                var editor = FindName("Editor") as TextBox;
                if (editor != null)
                {
                    try
                    {
                        string xamlContent = File.ReadAllText(dlg.FileName);
                        editor.Text = xamlContent;
                        LogStatus($"Loaded layout: {Path.GetFileName(dlg.FileName)}");

                        // Force an immediate preview
                        TryPreviewXaml(xamlContent);
                    }
                    catch (Exception ex)
                    {
                        LogStatus($"Error loading layout: {ex.Message}");
                    }
                }
            }
        }

        private void LoadJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Data (*.json)|*.json",
                Title = "Select JSON Data",
                FileName = "stubdata4.json" // Default to stubdata4.json
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    currentJsonPath = dlg.FileName;
                    string json = File.ReadAllText(dlg.FileName);
                    jsonData = JObject.Parse(json);

                    LogStatus($"Loaded: {Path.GetFileName(dlg.FileName)}");

                    // Update preview with new data
                    var editor = FindName("Editor") as TextBox;
                    if (editor != null && !string.IsNullOrWhiteSpace(editor.Text))
                    {
                        TryPreviewXaml(editor.Text);
                    }
                }
                catch (Exception ex)
                {
                    LogStatus($"Error loading JSON: {ex.Message}");
                }
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var editor = FindName("Editor") as TextBox;
            if (editor != null && !string.IsNullOrWhiteSpace(editor.Text))
            {
                var layoutRoot = PreviewHost.Content as FrameworkElement;
                var popupOverlay = FindElementByName<Grid>(layoutRoot, "PopupOverlay");
                Console.WriteLine("Found popupOverlay: " + (popupOverlay != null));

                TryPreviewXaml(editor.Text);
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            var editor = FindName("Editor") as TextBox;
            if (editor != null && !string.IsNullOrWhiteSpace(editor.Text))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XAML Layout (*.xaml)|*.xaml",
                    Title = "Save Layout XAML"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, editor.Text);
                    LogStatus($"Saved layout to: {Path.GetFileName(dlg.FileName)}");
                }
            }
            else
            {
                LogStatus("Nothing to save. Editor is empty.");
            }
        }

        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            var debugMode = (sender as CheckBox)?.IsChecked == true;
            LogStatus(debugMode ? "Debug mode enabled" : "Debug mode disabled");

            // Update the preview if we have content
            var editor = FindName("Editor") as TextBox;
            if (editor != null && !string.IsNullOrWhiteSpace(editor.Text))
            {
                TryPreviewXaml(editor.Text);
            }
        }

        private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
        {
            var autoUpdate = (sender as CheckBox)?.IsChecked == true;
            ToggleAutoUpdate(autoUpdate);
        }

        private void ToggleAutoUpdate(bool enable)
        {
            autoUpdateEnabled = enable;
            LogStatus(enable ? "Auto-update enabled" : "Auto-update disabled");
        }

        private void DelayInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var delayInput = (sender as TextBox)?.Text;
            if (int.TryParse(delayInput, out var delay))
            {
                previewDelayMilliseconds = delay;
                LogStatus($"Preview delay updated to {previewDelayMilliseconds} ms.");
            }
            else
            {
                LogStatus("Invalid delay input. Please enter a valid number.");
            }
        }
    }
}
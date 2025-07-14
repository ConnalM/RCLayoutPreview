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
        private EditorWindow editorWindow;

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

            try
            {
                PreviewHost.Content = null;

                string processedXaml = XamlFixer.Preprocess(xamlContent);
                LogStatus("XAML processed for preview");

                if (processedXaml.Contains("FontSize=\"\""))
                {
                    LogStatus("Invalid FontSize detected in XAML. Replacing with default value.");
                    processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
                }

                object element = XamlReader.Parse(processedXaml);
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

                    frameworkElement.DataContext = jsonData;
                    LogStatus("Processing named fields...");
                    XamlFixer.ProcessNamedFields(frameworkElement, jsonData, DebugModeToggle.IsChecked == true);

                    PreviewHost.Content = frameworkElement;
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
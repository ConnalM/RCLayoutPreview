using RCLayoutPreview.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Xml;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        // Path to the current stubdata JSON file
        private string currentJsonPath;
        // Parsed stubdata as JObject
        private JObject jsonData;
        // Reference to the editor window
        private EditorWindow editorWindow;
        // Tracks if placeholder has been removed for current preview
        private bool placeholderRemoved = false;
        // Tracks used element names to ensure uniqueness
        private HashSet<string> usedElementNames = new HashSet<string>();
        // For UI hover highlighting
        private FrameworkElement currentHighlightedElement;
        private ToolTip currentToolTip;

        /// <summary>
        /// Main window constructor. Initializes UI, loads stubdata, sets up editor window and event handlers.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            LoadStubData();

            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Use helper for window placement
            bool usedSaved = WindowPlacementHelper.RestoreWindowPlacement(this, "PreviewWindow");
            if (!usedSaved)
            {
                this.Left = screenWidth * 0.66;
                this.Top = 0;
                this.Width = screenWidth * 0.33;
                this.Height = screenHeight;
            }

            editorWindow = new EditorWindow(this);
            if (!WindowPlacementHelper.RestoreWindowPlacement(editorWindow, "EditorWindow"))
            {
                editorWindow.Left = 0;
                editorWindow.Top = 0;
                editorWindow.Width = screenWidth * 0.66;
                editorWindow.Height = screenHeight;
            }
            editorWindow.XamlContentChanged += EditorWindow_XamlContentChanged;
            editorWindow.JsonDataChanged += EditorWindow_JsonDataChanged;
            editorWindow.Show();

            // Set up UI logging for stubdata field handler
            StubDataFieldHandler.UILogStatus = LogStatus;

            this.Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Called when the main window is loaded. Used for initial status logging.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogStatus("Layout initialized.");
        }

        /// <summary>
        /// Event handler for XAML content changes in the editor window. Triggers preview update.
        /// </summary>
        private void EditorWindow_XamlContentChanged(object sender, string xamlContent)
        {
            TryPreviewXaml(xamlContent);
        }

        /// <summary>
        /// Event handler for stubdata JSON changes in the editor window. Updates data context and refreshes preview.
        /// </summary>
        private void EditorWindow_JsonDataChanged(object sender, JObject newJsonData)
        {
            jsonData = newJsonData;
            // If we have current XAML content, refresh the preview with new data
            if (PreviewHost?.Content is FrameworkElement frameworkElement)
            {
                frameworkElement.DataContext = jsonData;
                // Modular field/placeholder processing
                ProcessFieldsAndPlaceholders(frameworkElement, jsonData, DebugModeToggle.IsChecked == true);
            }
        }

        /// <summary>
        /// Processes all fields and placeholders in the root element, recursively.
        /// </summary>
        /// <param name="rootElement">Root UI element to process</param>
        /// <param name="jsonData">Stubdata JSON</param>
        /// <param name="debugMode">If true, show debug info in UI</param>
        private void ProcessFieldsAndPlaceholders(FrameworkElement rootElement, JObject jsonData, bool debugMode)
        {
            if (rootElement == null) return;
            // Recursively process all children
            ProcessElementRecursively(rootElement, jsonData, debugMode);
            // Synchronize widths after layout is updated
            rootElement.LayoutUpdated += (s, e) =>
            {
                SynchronizeEchoFieldWidths(rootElement);
            };
        }

        /// <summary>
        /// Recursively processes each element for placeholder and stubdata field logic.
        /// </summary>
        /// <param name="element">Current UI element</param>
        /// <param name="jsonData">Stubdata JSON</param>
        /// <param name="debugMode">If true, show debug info in UI</param>
        private void ProcessElementRecursively(FrameworkElement element, JObject jsonData, bool debugMode)
        {
            if (element == null) return;
            // Ensure hit-testable for tooltips and highlight
            if (!string.IsNullOrEmpty(element.Name))
            {
                string normalizedFieldName = RemoveFieldSuffix(element.Name);
                if (element is Label lbl)
                {
                    lbl.IsHitTestVisible = true;
                    if (lbl.Background == null || (lbl.Background is SolidColorBrush b && b.Color.A == 0))
                        lbl.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                }
                else if (element is TextBlock tb)
                {
                    tb.IsHitTestVisible = true;
                    if (tb.Background == null || (tb.Background is SolidColorBrush b && b.Color.A == 0))
                        tb.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                }
                else if (element is Button btn && btn.Opacity == 0)
                {
                    btn.IsHitTestVisible = false;
                }
                // Get position from parent tag if available (for placeholder formatting)
                int position = 1;
                var parentTag = (element.Parent as FrameworkElement)?.Tag?.ToString() ?? "";
                if (int.TryParse(Regex.Match(parentTag, @"\((\d+)\)").Groups[1].Value, out int pos))
                {
                    position = pos;
                }
                // Handle placeholders
                if (PlaceholderHandler.IsPlaceholderElement(element))
                {
                    PlaceholderHandler.DisplayPlaceholder(element, position);
                }
                // Handle stubdata fields
                else if (StubDataFieldHandler.IsStubDataField(element))
                {
                    StubDataFieldHandler.DisplayStubDataField(element, jsonData, debugMode, normalizedFieldName);
                }
            }
            // Recursively process children
            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is FrameworkElement childElement)
                {
                    ProcessElementRecursively(childElement, jsonData, debugMode);
                }
            }
        }

        /// <summary>
        /// Main method for previewing XAML. Cleans, validates, parses, and displays XAML, then binds stubdata and processes fields.
        /// </summary>
        /// <param name="xamlContent">Full XAML document as string</param>
        private void TryPreviewXaml(string xamlContent)
        {
            if (!ValidateXamlContent(xamlContent)) return;

            xamlContent = CleanXamlPlaceholders(xamlContent);
            if (CheckForDuplicateNames(xamlContent)) return;

            try
            {
                string processedXaml = PreprocessXaml(xamlContent);
                processedXaml = ProcessPlaceholders(processedXaml);
                processedXaml = EnsureRootElementAndNamespaces(processedXaml);
                processedXaml = FixBindingExpressions(processedXaml);
                processedXaml = RemoveEmptyNameAttributes(processedXaml);

                object element = ParseXamlContent(processedXaml);
                ApplyParsedElement(element);
                PostProcessPreview();
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

        private bool ValidateXamlContent(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
            {
                LogStatus("XAML content is empty or null.");
                return false;
            }
            return true;
        }

        private bool CheckForDuplicateNames(string xamlContent)
        {
            var duplicateNames = DetectDuplicateNames(xamlContent);
            if (duplicateNames.Count > 0)
            {
                HandleDuplicateNames(duplicateNames);
                return true;
            }
            return false;
        }

        private string PreprocessXaml(string xamlContent)
        {
            PreviewHost.Content = null;
            usedElementNames.Clear();
            string processedXaml = XamlPreprocessor.Preprocess(xamlContent);
            if (processedXaml.Contains("FontSize=\"\""))
            {
                LogStatus("Invalid FontSize detected in XAML. Replacing with default value.");
                processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
            }
            processedXaml = processedXaml.Replace("{styles}", "");
            processedXaml = processedXaml.Replace("{content}", "");
            processedXaml = EnsureUniqueElementNames(processedXaml);
            return processedXaml;
        }

        private string ProcessPlaceholders(string processedXaml)
        {
            if (PlaceholderSwapManager.ContainsValidField(processedXaml))
            {
                if (PlaceholderSwapManager.ContainsPlaceholder(processedXaml))
                {
                    string fieldMessage = PlaceholderSwapManager.GenerateFieldDetectedMessage(processedXaml);
                    if (!string.IsNullOrEmpty(fieldMessage))
                    {
                        processedXaml = PlaceholderSwapManager.ReplacePlaceholderWithMessage(processedXaml, fieldMessage);
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
                placeholderRemoved = false;
            }
            return processedXaml;
        }

        private string EnsureRootElementAndNamespaces(string processedXaml)
        {
            if (!IsValidRootElement(processedXaml))
            {
                processedXaml = $"<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{processedXaml}</Grid>";
                LogStatus("Added Grid container to wrap content");
            }
            if (!processedXaml.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""))
            {
                string rootTag = Regex.Match(processedXaml, @"<(\w+)").Groups[1].Value;
                string xmlnsDeclaration = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                processedXaml = Regex.Replace(processedXaml,
                    $"<{rootTag}",
                    $"<{rootTag} {xmlnsDeclaration}");
                LogStatus("Added XAML namespaces to content");
            }
            return processedXaml;
        }

        private object ParseXamlContent(string processedXaml)
        {
            var element = XamlValidationHelper.ParseXaml(processedXaml, out string error);
            if (element != null)
            {
                LogStatus("XAML parsed successfully");
                return element;
            }
            else
            {
                LogStatus($"XAML parsing failed: {error}");
                throw new XamlParseException($"Failed to parse XAML: {error}");
            }
        }

        private void ApplyParsedElement(object element)
        {
            if (element is Window window)
            {
                ApplyWindowProperties(window);
            }
            else
            {
                SetupPreviewHost(element);
            }
        }

        private void HandleDuplicateNames(List<string> duplicateNames)
        {
            ShowErrorPopup($"Error: Duplicate field names detected in XAML: {string.Join(", ", duplicateNames)}. Please ensure all element names are unique.");
            LogStatus($"Duplicate field names found: {string.Join(", ", duplicateNames)}");
        }

        private void PostProcessPreview()
        {
            if (PreviewHost.Content is FrameworkElement frameworkElement && jsonData != null)
            {
                frameworkElement.DataContext = jsonData;
                ProcessFieldsAndPlaceholders(frameworkElement, jsonData, DebugModeToggle.IsChecked == true);
            }
            AddHoverBehavior();
        }

        // --- Helper methods for TryPreviewXaml refactor ---

        private string CleanXamlPlaceholders(string xamlContent)
        {
            xamlContent = Regex.Replace(xamlContent, @"<([a-zA-Z0-9_]+)\s*([^>]*)?\{[a-zA-Z0-9_]+\}([^>]*)?>", m =>
            {
                return $"<!-- Invalid tag removed: {m.Value} -->";
            });
            xamlContent = Regex.Replace(xamlContent, @"\{[a-zA-Z0-9_]+\}", "");
            return xamlContent;
        }

        private List<string> DetectDuplicateNames(string xamlContent)
        {
            var nameRegex = new Regex("Name=\"([^\"]+)\"");
            var nameMatches = nameRegex.Matches(xamlContent);
            var nameSet = new HashSet<string>();
            var duplicateNames = new List<string>();
            foreach (Match match in nameMatches)
            {
                string name = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (name.StartsWith("Placeholder"))
                        continue;
                    if (nameSet.Contains(name))
                        duplicateNames.Add(name);
                    else
                        nameSet.Add(name);
                }
            }
            return duplicateNames;
        }

        private string PreprocessAndFixXaml(string xamlContent)
        {
            PreviewHost.Content = null;
            usedElementNames.Clear();
            string processedXaml = XamlPreprocessor.Preprocess(xamlContent);
            if (processedXaml.Contains("FontSize=\"\""))
            {
                LogStatus("Invalid FontSize detected in XAML. Replacing with default value.");
                processedXaml = processedXaml.Replace("FontSize=\"\"", "FontSize=\"14\"");
            }
            processedXaml = processedXaml.Replace("{styles}", "");
            processedXaml = processedXaml.Replace("{content}", "");
            processedXaml = EnsureUniqueElementNames(processedXaml);
            return processedXaml;
        }

        private string HandlePlaceholders(string processedXaml)
        {
            if (PlaceholderSwapManager.ContainsValidField(processedXaml))
            {
                if (PlaceholderSwapManager.ContainsPlaceholder(processedXaml))
                {
                    string fieldMessage = PlaceholderSwapManager.GenerateFieldDetectedMessage(processedXaml);
                    if (!string.IsNullOrEmpty(fieldMessage))
                    {
                        processedXaml = PlaceholderSwapManager.ReplacePlaceholderWithMessage(processedXaml, fieldMessage);
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
                placeholderRemoved = false;
            }
            return processedXaml;
        }

        private string EnsureRootAndNamespaces(string processedXaml)
        {
            if (!IsValidRootElement(processedXaml))
            {
                processedXaml = $"<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{processedXaml}</Grid>";
                LogStatus("Added Grid container to wrap content");
            }
            if (!processedXaml.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""))
            {
                string rootTag = Regex.Match(processedXaml, @"<(\w+)").Groups[1].Value;
                string xmlnsDeclaration = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                processedXaml = Regex.Replace(processedXaml,
                    $"<{rootTag}",
                    $"<{rootTag} {xmlnsDeclaration}");
                LogStatus("Added XAML namespaces to content");
            }
            return processedXaml;
        }

        private string RemoveEmptyNameAttributes(string processedXaml)
        {
            var emptyNameMatches = Regex.Matches(processedXaml, "Name\\s*=\\s*\"\\s*\"");
            if (emptyNameMatches.Count > 0)
            {
                ShowErrorPopup($"Warning: {emptyNameMatches.Count} empty Name attribute(s) were found and removed from the XAML. Please check your layout for missing names.");
                processedXaml = Regex.Replace(processedXaml, "Name\\s*=\\s*\"\\s*\"", "");
            }
            return processedXaml;
        }

        private object ParseXaml(string processedXaml)
        {
            var element = XamlValidationHelper.ParseXaml(processedXaml, out string error);
            if (element != null)
            {
                LogStatus("XAML parsed successfully");
                return element;
            }
            else
            {
                LogStatus($"XAML parsing failed: {error}");
                throw new XamlParseException($"Failed to parse XAML: {error}");
            }
        }

        private void ApplyWindowProperties(Window window)
        {
            LogStatus("XAML contains a Window element. Extracting content.");
            PreviewHost.Content = null;
            PreviewHost.Content = window.Content;
            PreviewHost.IsHitTestVisible = true;
            PreviewHost.IsEnabled = true;
            if (window.Content is FrameworkElement fe)
            {
                fe.IsHitTestVisible = true;
                fe.IsEnabled = true;
            }
            if (window.Title != null)
            {
                this.Title = window.Title;
                LogStatus($"Window Title applied: {window.Title}");
            }
            if (ApplyWindowSizeToggle != null && ApplyWindowSizeToggle.IsChecked == true)
            {
                if (window.Width > 0)
                {
                    this.Width = window.Width;
                    LogStatus($"Window Width applied: {window.Width}");
                }
                if (window.Height > 0)
                {
                    this.Height = window.Height;
                    LogStatus($"Window Height applied: {window.Height}");
                }
            }
            if (window.Background != null)
            {
                this.Background = window.Background;
                LogStatus("Window Background applied.");
            }
        }

        private void SetupPreviewHost(object element)
        {
            PreviewHost.Content = element;
            LogStatus("Preview updated with parsed element.");
            PreviewHost.IsHitTestVisible = true;
            PreviewHost.IsEnabled = true;
            if (element is FrameworkElement fe)
            {
                fe.IsHitTestVisible = true;
                fe.IsEnabled = true;
            }
        }

        /// <summary>
        /// Loads stubdata JSON from disk and parses it into jsonData.
        /// </summary>
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

        /// <summary>
        /// Logs a status message to the UI and console.
        /// </summary>
        /// <param name="message">Message to log</param>
        private void LogStatus(string message)
        {
            if (StatusLabel != null)
            {
                StatusLabel.Text = message;
            }
            Console.WriteLine($"Status: {message}");
        }

        /// <summary>
        /// Event handler for debug mode toggle. Refreshes preview with debug info if enabled.
        /// </summary>
        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            var debugMode = (sender as CheckBox)?.IsChecked == true;
            LogStatus(debugMode ? "Debug mode enabled" : "Debug mode disabled");
            if (PreviewHost?.Content is FrameworkElement frameworkElement && jsonData != null)
            {
                // Refresh the preview content with the updated diagnostics mode
                // Modular field/placeholder processing
                ProcessFieldsAndPlaceholders(frameworkElement, jsonData, debugMode);
                PreviewHost.Content = null; // Clear the content
                PreviewHost.Content = frameworkElement; // Reapply the content
                LogStatus("Preview refreshed with updated diagnostics mode.");
            }
            else
            {
                LogStatus("Preview content is not available to refresh.");
            }
        }

        /// <summary>
        /// Called when the main window is closing. Closes the editor window.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            WindowPlacementHelper.SaveWindowPlacement(this, "PreviewWindow");
            editorWindow.Close();
            base.OnClosing(e);
            // Ensure the application fully shuts down when the main window closes
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Attaches hover behavior for tooltips to the preview host.
        /// </summary>
        private void AddHoverBehavior()
        {
            if (PreviewHost != null)
            {
                PreviewHost.MouseMove -= PreviewHost_SafeMouseMove;
                PreviewHost.MouseMove += PreviewHost_SafeMouseMove;
                PreviewHost.MouseLeave -= PreviewHost_MouseLeave;
                PreviewHost.MouseLeave += PreviewHost_MouseLeave;
            }
        }

        /// <summary>
        /// Performs a deep hit test to find the deepest named element at a given point in the preview.
        /// </summary>
        /// <param name="pt">Point to hit test</param>
        /// <returns>Deepest named FrameworkElement at the point, or null</returns>
        private FrameworkElement DeepHitTestForNamedElement(Point pt)
        {
            var results = new List<FrameworkElement>();
            VisualTreeHelper.HitTest(PreviewHost, null,
                new HitTestResultCallback(hit =>
                {
                    if (hit.VisualHit is FrameworkElement fe)
                    {
                        // Only consider visible, hit-testable, non-zero opacity elements
                        if (!string.IsNullOrEmpty(fe.Name) && fe.IsHitTestVisible && fe.Opacity > 0)
                            results.Add(fe);
                    }
                    return HitTestResultBehavior.Continue;
                }),
                new PointHitTestParameters(pt));
            // Return the deepest (last) named element
            return results.Count > 0 ? results[results.Count - 1] : null;
        }

        /// <summary>
        /// Mouse move handler for preview host. Highlights and shows tooltip for named elements under cursor.
        /// </summary>
        private void PreviewHost_SafeMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pt = e.GetPosition(PreviewHost);
            FrameworkElement namedElement = DeepHitTestForNamedElement(pt);
            if (namedElement != null)
            {
                if (namedElement != currentHighlightedElement)
                {
                    // Remove highlight from previous
                    if (currentHighlightedElement != null)
                        currentHighlightedElement.Effect = null;
                    // Highlight
                    currentHighlightedElement = namedElement;
                    currentHighlightedElement.Effect = new DropShadowEffect
                    {
                        Color = Colors.Yellow,
                        ShadowDepth = 0,
                        BlurRadius = 12,
                        Opacity = 0.7
                    };
                }
                // Show tooltip and always update its position
                ShowElementTooltip(namedElement, pt);
                e.Handled = true;
            }
            else
            {
                // No named element found, hide tooltip and remove highlight
                if (currentHighlightedElement != null)
                    currentHighlightedElement.Effect = null;
                currentHighlightedElement = null;
                if (currentToolTip != null)
                    currentToolTip.IsOpen = false;
            }
        }

        /// <summary>
        /// Mouse leave handler for preview host. Removes highlight and hides tooltip.
        /// </summary>
        private void PreviewHost_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentHighlightedElement != null)
                currentHighlightedElement.Effect = null;
            currentHighlightedElement = null;
            if (currentToolTip != null)
                currentToolTip.IsOpen = false;
        }

        /// <summary>
        /// Finds the nearest named child element in the visual tree, searching up and down.
        /// </summary>
        /// <param name="element">Element to start search from</param>
        /// <returns>Nearest named FrameworkElement, or null</returns>
        private FrameworkElement FindNamedChildElement(FrameworkElement element)
        {
            // If hit test lands on a Viewbox, search its child
            if (element is Viewbox viewbox && VisualTreeHelper.GetChildrenCount(viewbox) > 0)
            {
                var child = VisualTreeHelper.GetChild(viewbox, 0) as FrameworkElement;
                if (child != null)
                {
                    var named = FindNamedChildElement(child);
                    if (named != null)
                        return named;
                }
            }
            // Traverse up the tree to find the nearest named element
            var up = element;
            while (up != null)
            {
                if (!string.IsNullOrEmpty(up.Name))
                    return up;
                if (up.Parent is FrameworkElement parent)
                    up = parent;
                else
                    break;
            }
            // Traverse down the visual tree from the hit element
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(element);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                    return fe;
                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(current, i));
            }
            return null;
        }

        /// <summary>
        /// Shows a tooltip for the given element at the specified mouse position.
        /// </summary>
        /// <param name="element">Element to show tooltip for</param>
        /// <param name="mousePos">Mouse position relative to preview host</param>
        private void ShowElementTooltip(FrameworkElement element, Point mousePos)
        {
            var info = BuildElementInfoSafe(element);
            if (currentToolTip == null)
            {
                currentToolTip = new ToolTip
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    PlacementTarget = PreviewHost,
                    StaysOpen = true,
                    Background = new SolidColorBrush(Color.FromRgb(255,255,220)),
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8)
                };
            }
            currentToolTip.Content = info;
            currentToolTip.HorizontalOffset = mousePos.X + 12;
            currentToolTip.VerticalOffset = mousePos.Y + 12;
            currentToolTip.IsOpen = true;
        }

        /// <summary>
        /// Builds a string with diagnostic info about the given element (type, name, size, position).
        /// </summary>
        /// <param name="element">Element to describe</param>
        /// <returns>Diagnostic info string</returns>
        private string BuildElementInfoSafe(FrameworkElement element)
        {
            var info = new StringBuilder();
            info.AppendLine($"Type: {element.GetType().Name}");
            if (!string.IsNullOrEmpty(element.Name))
                info.AppendLine($"Name: {element.Name}");
            info.AppendLine($"Size: {element.ActualWidth:F0} x {element.ActualHeight:F0}");
            if (element.Parent is Panel parentPanel)
            {
                int idx = parentPanel.Children.IndexOf(element);
                info.AppendLine($"Position in Parent: {idx + 1} of {parentPanel.Children.Count}");
            }
            if (Grid.GetRow(element) >= 0 || Grid.GetColumn(element) >= 0)
                info.AppendLine($"Grid Position: Row {Grid.GetRow(element)}, Column {Grid.GetColumn(element)}");
            return info.ToString().TrimEnd();
        }

        /// <summary>
        /// Shows an error popup overlay with the given message and logs the status.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        private void ShowErrorPopup(string errorMessage)
        {
            // Try to set the popup message and show overlay if available
            if (PopupMessage != null && PopupOverlay != null)
            {
                PopupMessage.Text = errorMessage;
                PopupOverlay.Visibility = Visibility.Visible;
            }
            LogStatus("Popup overlay displayed with message: " + errorMessage);
        }

        /// <summary>
        /// Ensures all element names in the XAML are unique by appending a suffix to duplicates.
        /// </summary>
        /// <param name="xaml">XAML string to process</param>
        /// <returns>XAML string with unique element names</returns>
        private string EnsureUniqueElementNames(string xaml)
        {
            var nameRegex = new Regex("Name=\"([^\"]+)\"");
            return nameRegex.Replace(xaml, match => {
                string originalName = match.Groups[1].Value;
                string uniqueName = originalName;
                int counter = 1;
                while (usedElementNames.Contains(uniqueName))
                {
                    uniqueName = $"{originalName}_{counter++}";
                }
                usedElementNames.Add(uniqueName);
                return $"Name=\"{uniqueName}\"";
            });
        }

        /// <summary>
        /// Fixes binding expressions in the XAML that might be malformed or unescaped.
        /// </summary>
        /// <param name="xaml">XAML string to process</param>
        /// <returns>XAML string with fixed binding expressions</returns>
        private string FixBindingExpressions(string xaml)
        {
            xaml = Regex.Replace(xaml, "{Binding([^}]*)}", m =>
            {
                if (xaml.IndexOf(m.Value) > 0 && xaml[xaml.IndexOf(m.Value) - 1] == '{')
                    return m.Value;
                return "{Binding" + m.Groups[1].Value + "}";
            });
            return xaml;
        }

        /// <summary>
        /// Checks if the XAML string has a valid root element (e.g., Grid, StackPanel, Window).
        /// </summary>
        /// <param name="xaml">XAML string to check</param>
        /// <returns>True if root element is valid, false otherwise</returns>
        private bool IsValidRootElement(string xaml)
        {
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

        /// <summary>
        /// Event handler for OK button on error popup. Hides the popup.
        /// </summary>
        private void PopupOkButton_Click(object sender, RoutedEventArgs e)
        {
            if (PopupOverlay != null)
                PopupOverlay.Visibility = Visibility.Collapsed;
        }

        private string RemoveFieldSuffix(string fieldName)
        {
            // Removes trailing _N (where N is an integer) from a field name
            return Regex.Replace(fieldName, "_\\d+$", "");
        }

        /// <summary>
        /// Synchronizes the widths of all TextBlocks in the same container that match the echo/real field pattern.
        /// </summary>
        private void SynchronizeEchoFieldWidths(FrameworkElement rootElement)
        {
            if (rootElement == null) return;
            // Only process Panels (Grid, StackPanel, DockPanel, etc.)
            if (rootElement is Panel panel)
            {
                // Find all descendant TextBlocks with Name matching pattern <base>_<num>_<suffix>
                var allTextBlocks = new List<TextBlock>();
                var queue = new Queue<DependencyObject>();
                foreach (var child in panel.Children)
                    queue.Enqueue(child as DependencyObject);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current is TextBlock tb && !string.IsNullOrEmpty(tb.Name))
                        allTextBlocks.Add(tb);
                    int count = VisualTreeHelper.GetChildrenCount(current);
                    for (int i = 0; i < count; i++)
                        queue.Enqueue(VisualTreeHelper.GetChild(current, i));
                }
                // Group by base name and number (e.g., OnDeckNickname6)
                var groups = new Dictionary<string, List<TextBlock>>();
                var regex = new System.Text.RegularExpressions.Regex(@"^(.*?)(\d+)_([12])$");
                foreach (var tb in allTextBlocks)
                {
                    var match = regex.Match(tb.Name);
                    if (match.Success)
                    {
                        string baseName = match.Groups[1].Value + match.Groups[2].Value; // e.g., OnDeckNickname6
                        if (!groups.ContainsKey(baseName))
                            groups[baseName] = new List<TextBlock>();
                        groups[baseName].Add(tb);
                    }
                }
                // For each group, set all widths to the max ActualWidth
                foreach (var group in groups.Values)
                {
                    if (group.Count >= 2)
                    {
                        double maxWidth = group.Max(tb => tb.ActualWidth);
                        foreach (var tb in group)
                        {
                            tb.Width = maxWidth;
                        }
                    }
                }
            }
            // Recursively process children
            foreach (var child in LogicalTreeHelper.GetChildren(rootElement))
            {
                if (child is FrameworkElement childElement)
                {
                    SynchronizeEchoFieldWidths(childElement);
                }
            }
        }

        public void SaveWindowPlacementFromEditor()
        {
            WindowPlacementHelper.SaveWindowPlacement(this, "PreviewWindow");
        }
    }
}
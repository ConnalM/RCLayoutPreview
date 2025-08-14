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
            StubDataFieldHandler.UILogStatus = UpdateStatus;

            // Add F8 keyboard shortcut for error navigation from preview window
            this.KeyDown += MainWindow_KeyDown;
            
            this.Loaded += MainWindow_Loaded;
        }
        
        /// <summary>
        /// Called when the main window is loaded. Used for initial status logging.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Layout initialized.");
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
                if (jsonData != null && jsonData["RacerData"] != null)
                {
                    frameworkElement.DataContext = jsonData["RacerData"];
                }
                else
                {
                    frameworkElement.DataContext = jsonData;
                }
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
            
            // Clear any existing popups before processing - if there are no new issues, the popup should disappear
            ClearAnyExistingPopups();
            
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
                
                // If we reach here without issues being detected, ensure popups are hidden
                // This handles the case where issues were fixed but detection didn't run
                EnsurePopupsHiddenOnSuccess();
            }
            catch (XamlParseException ex)
            {
                ShowErrorPopup($"XAML parsing error: {ex.Message}");
                UpdateStatus($"XAML parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorPopup($"Preview error: {ex.Message}");
                UpdateStatus($"Preview error: {ex.Message}");
            }
        }

        private bool ValidateXamlContent(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
            {
                UpdateStatus("XAML content is empty or null.");
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
                UpdateStatus("Invalid FontSize detected in XAML. Replacing with default value.");
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
                            UpdateStatus($"Field detected: {fieldMessage}");
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
                UpdateStatus("Added Grid container to wrap content");
            }
            if (!processedXaml.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""))
            {
                string rootTag = Regex.Match(processedXaml, @"<(\w+)").Groups[1].Value;
                string xmlnsDeclaration = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                processedXaml = Regex.Replace(processedXaml,
                    $"<{rootTag}",
                    $"<{rootTag} {xmlnsDeclaration}");
                UpdateStatus("Added XAML namespaces to content");
            }
            return processedXaml;
        }

        private object ParseXamlContent(string processedXaml)
        {
            var element = XamlValidationHelper.ParseXamlWithPosition(processedXaml, out string error, out int errorPosition);
            if (element != null)
            {
                UpdateStatus("XAML parsed successfully");
                return element;
            }
            else
            {
                // Create enhanced error message with correct position
                string enhancedError = XamlValidationHelper.CreateEnhancedErrorMessage(error, processedXaml, errorPosition);
                
                // Get context around the error
                string context = XamlValidationHelper.GetErrorContext(processedXaml, errorPosition);
                
                UpdateStatus($"XAML parsing failed: {enhancedError}");
                
                // Navigate to error in editor if possible
                if (editorWindow != null && errorPosition >= 0)
                {
                    // Map the error position back to the original editor content
                    NavigateToErrorInEditor(editorWindow.Editor.Text, errorPosition, enhancedError, context);
                }
                
                throw new XamlParseException($"Failed to parse XAML: {enhancedError}");
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
            UpdateStatus($"Duplicate field names found: {string.Join(", ", duplicateNames)}");
        }

        private void PostProcessPreview()
        {
            if (PreviewHost.Content is FrameworkElement frameworkElement && jsonData != null)
            {
                frameworkElement.DataContext = jsonData;
                // Always use the current value of DebugModeToggle.IsChecked
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
            
            // First pass: collect all names and detect exact duplicates
            var allNames = new List<string>();
            foreach (Match match in nameMatches)
            {
                string name = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (name.StartsWith("Placeholder"))
                        continue;
                        
                    allNames.Add(name);
                    
                    if (nameSet.Contains(name))
                        duplicateNames.Add(name);
                    else
                        nameSet.Add(name);
                }
            }
            
            // Second pass: detect naming pattern inconsistencies
            var namingIssues = DetectNamingPatternIssues(allNames);
            if (namingIssues.Count > 0)
            {
                // Remove duplicates from the issues list
                var uniqueIssues = namingIssues.Distinct().ToList();
                string issues = string.Join("; ", uniqueIssues);
                
                // Show both status message and prominent popup warning
                UpdateStatus($"WARNING - Naming pattern issues detected: {issues}");
                ShowNamingPatternWarning($"⚠️ NAMING PATTERN ISSUES DETECTED\n\n{string.Join("\n\n", uniqueIssues)}\n\nThese issues may cause confusion. Consider using consistent naming patterns like 'dockPanel1', 'dockPanel2', etc.");
            }
            
            return duplicateNames;
        }

        /// <summary>
        /// Detects naming pattern inconsistencies like missing numeric suffixes
        /// </summary>
        /// <param name="allNames">List of all element names found in XAML</param>
        /// <returns>List of naming issue descriptions</returns>
        private List<string> DetectNamingPatternIssues(List<string> allNames)
        {
            var issues = new List<string>();
            
            // Known patterns from snippet templates - if we see these base names without numbers, suggest the expected pattern
            var expectedNumberedPatterns = new Dictionary<string, string[]>
            {
                { "dockPanel", new[] { "dockPanel1", "dockPanel2" } },  // From scaffolding and lower dockpanel
                { "menu", new[] { "menu1" } },                         // From menu snippets
                { "Window", new[] { "Window_1", "Window_2" } }         // From window menu items
            };
            
            // Get the current XAML content for line/column calculation
            string xamlContent = editorWindow?.Editor?.Text ?? "";
            
            // Check for context-aware naming patterns
            foreach (string name in allNames)
            {
                // Check if this is a base name that should typically have a number
                if (expectedNumberedPatterns.ContainsKey(name))
                {
                    string[] expectedNames = expectedNumberedPatterns[name];
                    
                    // Check if this is likely the first or second in sequence based on other clues
                    bool hasFirstPattern = allNames.Exists(n => n == expectedNames[0]);
                    bool hasSecondPattern = expectedNames.Length > 1 && allNames.Exists(n => n == expectedNames[1]);
                    
                    // Find the position of this problematic name in the XAML
                    int namePosition = FindNamePositionInXaml(xamlContent, name);
                    string locationInfo = "";
                    
                    if (namePosition >= 0)
                    {
                        XamlValidationHelper.GetLineAndColumnFromPosition(xamlContent, namePosition, out int line, out int column);
                        locationInfo = $" (Line {line}, Column {column})";
                        
                        // Store warning info for potential F8 navigation, but don't auto-navigate
                        // Only show the warning in status bar and popup - don't interrupt user's workflow
                        if (editorWindow != null)
                        {
                            string editorMessage = "";
                            if (!hasFirstPattern && !hasSecondPattern)
                            {
                                editorMessage = $"Naming suggestion: '{name}' could be '{expectedNames[0]}' for consistency";
                            }
                            else if (hasFirstPattern && !hasSecondPattern && expectedNames.Length > 1)
                            {
                                editorMessage = $"Naming suggestion: '{name}' should probably be '{expectedNames[1]}' (found '{expectedNames[0]}')";
                            }
                            else if (!hasFirstPattern && hasSecondPattern)
                            {
                                editorMessage = $"Naming suggestion: '{name}' should probably be '{expectedNames[0]}' (found '{expectedNames[1]}')";
                            }
                            
                            // Store the warning for potential F8 navigation, but don't auto-navigate to avoid workflow disruption
                            editorWindow.StoreNamingWarning(namePosition, editorMessage);
                        }
                    }
                    
                    if (!hasFirstPattern && !hasSecondPattern)
                    {
                        // Neither exists, suggest the first one
                        issues.Add($"'{name}' should probably be '{expectedNames[0]}' (based on typical layout patterns){locationInfo}");
                    }
                    else if (hasFirstPattern && !hasSecondPattern && expectedNames.Length > 1)
                    {
                        // First exists, this should be second
                        issues.Add($"'{name}' should probably be '{expectedNames[1]}' (found '{expectedNames[0]}'){locationInfo}");
                    }
                    else if (!hasFirstPattern && hasSecondPattern)
                    {
                        // Second exists, this should be first
                        issues.Add($"'{name}' should probably be '{expectedNames[0]}' (found '{expectedNames[1]}'){locationInfo}");
                    }
                }
            }
            
            // Group names by their base (e.g., "dockPanel" from "dockPanel1", "dockPanel2")  
            var nameGroups = new Dictionary<string, List<string>>();
            
            foreach (string name in allNames)
            {
                // Extract base name by removing trailing digits
                string baseName = Regex.Replace(name, @"\d+$", "");
                
                if (baseName != name) // Only if it had digits
                {
                    if (!nameGroups.ContainsKey(baseName))
                        nameGroups[baseName] = new List<string>();
                    nameGroups[baseName].Add(name);
                }
            }
            
            // Check for gaps in numbered sequences
            foreach (var group in nameGroups)
            {
                if (group.Value.Count > 1)
                {
                    var numbers = new List<int>();
                    foreach (string name in group.Value)
                    {
                        var numberMatch = Regex.Match(name, @"(\d+)$");
                        if (numberMatch.Success)
                        {
                            numbers.Add(int.Parse(numberMatch.Groups[1].Value));
                        }
                    }
                    
                    numbers.Sort();
                    
                    // Check for gaps (e.g., dockPanel1, dockPanel3 - missing dockPanel2)
                    for (int i = 1; i < numbers.Count; i++)
                    {
                        if (numbers[i] - numbers[i-1] > 1)
                        {
                            int missing = numbers[i-1] + 1;
                            issues.Add($"Missing '{group.Key}{missing}' in sequence");
                        }
                    }
                    
                    // Check if sequence should start from 1 instead of 0
                    if (numbers.Count > 0 && numbers[0] == 0)
                    {
                        issues.Add($"'{group.Key}' sequence starts from 0, consider starting from 1");
                    }
                }
            }
            
            return issues;
        }

        /// <summary>
        /// Finds the position of a specific Name attribute value in the XAML content
        /// </summary>
        /// <param name="xaml">XAML content to search</param>
        /// <param name="nameValue">The name value to find (e.g., "dockPanel")</param>
        /// <returns>Character position of the Name attribute, or -1 if not found</returns>
        private int FindNamePositionInXaml(string xaml, string nameValue)
        {
            if (string.IsNullOrEmpty(xaml) || string.IsNullOrEmpty(nameValue))
                return -1;

            // Look for Name="nameValue" pattern
            string pattern = $@"Name\s*=\s*""{Regex.Escape(nameValue)}""";
            var match = Regex.Match(xaml, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                // Return the position of the Name attribute start
                return match.Index;
            }

            return -1;
        }

        /// <summary>
        /// Shows a naming pattern warning popup with yellow/orange styling to distinguish from errors
        /// </summary>
        /// <param name="warningMessage">Warning message to display</param>
        public void ShowNamingPatternWarning(string warningMessage)
        {
            // Try to set the popup message and show overlay if available
            try
            {
                var popupMessage = FindName("PopupMessage") as TextBlock;
                var popupOverlay = FindName("PopupOverlay") as FrameworkElement;
                
                if (popupMessage != null && popupOverlay != null)
                {
                    popupMessage.Text = warningMessage;
                    popupOverlay.Visibility = Visibility.Visible;
                    
                    // Change styling to indicate this is a warning (yellow/orange) rather than error (red)
                    if (popupMessage.Parent is Border border)
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(255, 248, 220)); // Light yellow background
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Orange border
                    }
                    popupMessage.Foreground = new SolidColorBrush(Color.FromRgb(133, 100, 4)); // Dark yellow text
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if UI elements not found
                Console.WriteLine($"Naming pattern warning: {warningMessage}");
                Console.WriteLine($"Warning display error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears any existing error/warning popups at the start of preview refresh
        /// This ensures outdated popups are removed when the preview is updated
        /// </summary>
        private void ClearAnyExistingPopups()
        {
            try
            {
                var popupOverlay = FindName("PopupOverlay") as FrameworkElement;
                if (popupOverlay != null && popupOverlay.Visibility == Visibility.Visible)
                {
                    popupOverlay.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Popup clearing is optional, don't crash if it fails
            }
        }

        /// <summary>
        /// Ensures popups are hidden when preview refresh completes successfully without new issues
        /// This is a safety net in case the issue detection doesn't run but problems were fixed
        /// </summary>
        private void EnsurePopupsHiddenOnSuccess()
        {
            try
            {
                var popupOverlay = FindName("PopupOverlay") as FrameworkElement;
                if (popupOverlay != null && popupOverlay.Visibility == Visibility.Visible)
                {
                    // Check if the popup is showing a warning (as opposed to a critical error)
                    var popupMessage = FindName("PopupMessage") as TextBlock;
                    if (popupMessage != null)
                    {
                        string popupText = popupMessage.Text ?? "";
                        
                        // Only auto-hide naming pattern warnings, not critical errors
                        if (popupText.Contains("NAMING PATTERN ISSUES") || popupText.Contains("should probably be"))
                        {
                            popupOverlay.Visibility = Visibility.Collapsed;
                            UpdateStatus("Outdated naming pattern warning cleared (issues may have been resolved)");
                        }
                    }
                }
            }
            catch
            {
                // Popup management is optional, don't crash if it fails
            }
        }

        // Add essential missing methods to restore functionality
        private void LoadStubData()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            jsonData = StubDataService.LoadStubData(baseDirectory, message => Console.WriteLine($"Status: {message}"));
            if (jsonData != null)
            {
                currentJsonPath = Path.Combine(baseDirectory, AppConstants.StubDataFileName);
            }
        }

        private void UpdateStatus(string message)
        {
            try
            {
                var statusLabel = FindName("StatusLabel") as TextBlock;
                if (statusLabel != null)
                {
                    statusLabel.Text = message;
                    // Ensure status text is visible with high contrast colors
                    statusLabel.Foreground = new SolidColorBrush(Colors.DarkBlue);
                    statusLabel.FontWeight = FontWeights.Normal;
                }
            }
            catch { }
            Console.WriteLine($"Status: {message}");
        }

        // Add simple ShowErrorPopup method
        public void ShowErrorPopup(string errorMessage)
        {
            try
            {
                var popupMessage = FindName("PopupMessage") as TextBlock;
                var popupOverlay = FindName("PopupOverlay") as FrameworkElement;
                
                if (popupMessage != null && popupOverlay != null)
                {
                    popupMessage.Text = errorMessage;
                    popupOverlay.Visibility = Visibility.Visible;
                    
                    // Reset styling to error colors (red) in case it was previously a warning
                    if (popupMessage.Parent is Border border)
                    {
                        border.Background = new SolidColorBrush(Colors.White); // White background for errors
                        border.BorderBrush = new SolidColorBrush(Colors.Red); // Red border for errors
                    }
                    popupMessage.Foreground = new SolidColorBrush(Colors.Black); // Black text for errors
                }
                else
                {
                    // Fallback to message box
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error display error: {ex.Message}");
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add essential helper methods
        private string RemoveFieldSuffix(string fieldName)
        {
            return Regex.Replace(fieldName, "_\\d+$", "");
        }

        private void AddHoverBehavior() { /* Implementation stub */ }
        private void SynchronizeEchoFieldWidths(FrameworkElement rootElement) { /* Implementation stub */ }
        private string EnsureUniqueElementNames(string xaml) { return xaml; }
        private string FixBindingExpressions(string xaml) { return xaml; }
        private string RemoveEmptyNameAttributes(string xaml) { return xaml; }
        private bool IsValidRootElement(string xaml) { return true; }
        private void NavigateToErrorInEditor(string originalXaml, int errorPosition, string enhancedError, string context) { }
        private void ApplyWindowProperties(Window window) { PreviewHost.Content = window.Content; }
        private void SetupPreviewHost(object element) { PreviewHost.Content = element; }
        public void SaveWindowPlacementFromEditor() { }
        
        /// <summary>
        /// Event handler for debug mode toggle. Refreshes preview with debug info if enabled.
        /// </summary>
        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            var debugMode = (sender as CheckBox)?.IsChecked == true;
            UpdateStatus(debugMode ? "Field Names view enabled" : "Field Names view disabled");
            
            if (editorWindow != null)
            {
                editorWindow.SetAutoUpdateEnabled(!debugMode);
            }
            
            // CRITICAL FIX: Force preview refresh when Field Names toggle changes
            // This ensures the current preview immediately shows field names or data based on the toggle
            if (PreviewHost?.Content is FrameworkElement frameworkElement && jsonData != null)
            {
                // Reprocess the current preview with the new debug mode setting
                ProcessFieldsAndPlaceholders(frameworkElement, jsonData, debugMode);
                UpdateStatus(debugMode ? "Field Names view enabled - preview refreshed" : "Field Names view disabled - preview refreshed");
            }
        }

        /// <summary>
        /// Called when the main window is closing. Closes the editor window.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            WindowPlacementHelper.SaveWindowPlacement(this, "PreviewWindow");
            editorWindow?.Close();
            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Event handler for OK button on error popup. Hides the popup.
        /// </summary>
        private void PopupOkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var popupOverlay = FindName("PopupOverlay") as FrameworkElement;
                if (popupOverlay != null)
                    popupOverlay.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        /// <summary>
        /// Handles keyboard shortcuts for the preview window, including F8 for error navigation
        /// </summary>
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // F8: Navigate to last error in editor
            if (e.Key == System.Windows.Input.Key.F8)
            {
                GoToLastErrorInEditor();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Navigates to the last error in the editor window from the preview window
        /// </summary>
        private void GoToLastErrorInEditor()
        {
            if (editorWindow != null)
            {
                // Focus the editor window first
                editorWindow.Activate();
                editorWindow.Focus();
                
                // Check if the editor has an error to navigate to
                if (editorWindow.HasErrorToNavigate())
                {
                    // Trigger the editor's go to error functionality
                    editorWindow.GoToLastError();
                    UpdateStatus("Navigated to last error in editor (F8 from preview)");
                }
                else
                {
                    // No error available, but still focus the editor
                    UpdateStatus("No recent errors to navigate to (F8 from preview)");
                    
                    // Show a brief message in the preview window
                    ShowErrorPopup("No recent XAML parsing errors found.\n\nPress F8 in the editor window to access more error navigation options.");
                }
            }
            else
            {
                UpdateStatus("Editor window not available for error navigation");
            }
        }
    }
}
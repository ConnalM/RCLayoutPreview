using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Data; // <-- Add this for DependencyPropertyHelper

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Handles detection, lookup, and display of stubdata fields in XAML.
    /// </summary>
    public static class StubDataFieldHandler
    {
        // Delegate for logging to UI
        public static Action<string> UILogStatus = null;

        // Global flag to indicate if ThemeDictionary is present in the loaded XAML
        public static bool ThemeDictionaryActive = false;

        // Holds the current XAML string for debug extraction
        public static string CurrentXamlContent { get; set; }

        public static bool IsStubDataField(System.Windows.FrameworkElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Name) && !element.Name.StartsWith("Placeholder");
        }
        public static void DisplayStubDataField(System.Windows.FrameworkElement element, JObject jsonData, bool debugMode, string normalizedFieldName)
        {
            if (element == null || jsonData == null) return;
            JToken value = null;
            string foundGroup = null;
            bool found = false;
            Debug.WriteLine($"[StubDataFieldHandler] Looking up field: {normalizedFieldName}");
            UILogStatus?.Invoke($"Looking up field: {normalizedFieldName}");

            // Search all top-level groups for the field
            foreach (var prop in jsonData.Properties())
            {
                if (prop.Value is JObject group)
                {
                    if (group.TryGetValue(normalizedFieldName, out value))
                    {
                        foundGroup = prop.Name;
                        found = true;
                        Debug.WriteLine($"[StubDataFieldHandler] Found field '{normalizedFieldName}' in group '{foundGroup}'.");
                        UILogStatus?.Invoke($"Found field '{normalizedFieldName}' in group '{foundGroup}'.");
                        break;
                    }
                }
            }

            if (!found)
            {
                Debug.WriteLine($"[StubDataFieldHandler] Field '{normalizedFieldName}' not found in any stubdata group.");
                UILogStatus?.Invoke($"Field '{normalizedFieldName}' not found in any stubdata group.");
            }

            if (debugMode)
            {
                string displayText = normalizedFieldName;
                
                // Show field names without yellow background - use original colors
                if (element is TextBlock tb2)
                {
                    tb2.Text = displayText;
                    // Don't change background or foreground - keep original styling
                    tb2.FontWeight = FontWeights.Bold;
                }
                else if (element is Label lbl)
                {
                    lbl.Content = displayText;
                    // Don't change background or foreground - keep original styling  
                    lbl.FontWeight = FontWeights.Bold;
                }
                else if (element is ContentControl contentControl)
                {
                    contentControl.Content = displayText;
                    // Don't change background or foreground - keep original styling
                }
                
                Debug.WriteLine($"[StubDataFieldHandler] DEBUG MODE: Set '{normalizedFieldName}' with original styling");
                UILogStatus?.Invoke($"DEBUG MODE: Displaying field name '{normalizedFieldName}' with original styling");
                
                return; // Prevent further processing in diagnostics mode
            }
            else if (value != null && element is Image imageElement)
            {
                string imagePath = value.ToString();
                Debug.WriteLine($"[StubDataFieldHandler] Setting image source for '{normalizedFieldName}' to '{imagePath}'");
                UILogStatus?.Invoke($"Image path for '{normalizedFieldName}': {imagePath}");

                try
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string fullPath = Path.Combine(baseDirectory, imagePath);
                    Debug.WriteLine($"[StubDataFieldHandler] Resolved full path for '{normalizedFieldName}': {fullPath}");
                    UILogStatus?.Invoke($"Resolved full path for '{normalizedFieldName}': {fullPath}");

                    if (File.Exists(fullPath))
                    {
                        Debug.WriteLine($"[StubDataFieldHandler] File exists at path: {fullPath}");
                        UILogStatus?.Invoke($"File exists at path: {fullPath}");

                        imageElement.Source = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                        Debug.WriteLine($"[StubDataFieldHandler] Image control source set to: {imageElement.Source}");
                        UILogStatus?.Invoke($"Image source set for '{normalizedFieldName}': {imageElement.Source}");
                    }
                    else
                    {
                        Debug.WriteLine($"[StubDataFieldHandler] Image file not found at path: {fullPath}");
                        UILogStatus?.Invoke($"Image file not found for '{normalizedFieldName}' at path: {fullPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StubDataFieldHandler] Error setting image source: {ex.Message}");
                    UILogStatus?.Invoke($"Error setting image source for '{normalizedFieldName}': {ex.Message}");
                }
            }
            else if (value != null)
            {
                Debug.WriteLine($"[StubDataFieldHandler] Retrieved value for '{normalizedFieldName}': {value}");
                UILogStatus?.Invoke($"Value for '{normalizedFieldName}': {value}");

                string displayText = value.ToString();
                SolidColorBrush colorBrush = null;

                // Apply player-specific background color if field is in RacerData group and ends with a number NOT preceded by underscore
                // This works independently of ThemeDictionary (which handles foreground colors and global themes)
                if (foundGroup == "RacerData" && Regex.IsMatch(normalizedFieldName, @"(?<!_)\d+$"))
                {
                    int playerIndex = RCLayoutPreview.Helpers.XamlFixer.GetPlayerIndex(normalizedFieldName);
                    Debug.WriteLine($"[StubDataFieldHandler] XamlFixer.GetPlayerIndex('{normalizedFieldName}') returned {playerIndex}");
                    UILogStatus?.Invoke($"Player index for '{normalizedFieldName}': {playerIndex}");

                    if (playerIndex > 0)
                    {
                        colorBrush = RCLayoutPreview.Helpers.XamlFixer.GetColor(playerIndex);
                        Debug.WriteLine($"[StubDataFieldHandler] Applying SolidColorBrush: {colorBrush?.Color}");
                        UILogStatus?.Invoke($"Applying SolidColorBrush: {colorBrush?.Color}");
                    }
                }

                // Set display text and always apply background color if available (independent of ThemeDictionary)
                if (element is TextBlock tb3)
                {
                    tb3.Text = displayText;
                    if (colorBrush != null) tb3.Background = colorBrush;
                    LogTextBlockResourceDebug(tb3, "TextBlock");
                    LogStaticResourceDebug(tb3, "TextBlock");
                    LogXamlStaticResourceDebug(tb3.Name, "TextBlock");
                }
                else if (element is Label lbl)
                {
                    lbl.Content = displayText;
                    if (colorBrush != null) lbl.Background = colorBrush;
                    LogResourceDebug(lbl, "Label");
                    LogStaticResourceDebug(lbl, "Label");
                    LogXamlStaticResourceDebug(lbl.Name, "Label");
                }
                else if (element is ContentControl contentControl)
                {
                    contentControl.Content = displayText;
                }
            }
        }

        public static void SetThemeDictionaryActive(bool active)
        {
            ThemeDictionaryActive = active;
        }

        // Call this method when loading/parsing XAML to detect ThemeDictionary.xaml
        public static void DetectThemeDictionary(string xamlContent)
        {
            if (!string.IsNullOrEmpty(xamlContent) && xamlContent.Contains("ThemeDictionary.xaml"))
            {
                SetThemeDictionaryActive(true);
            }
            else
            {
                SetThemeDictionaryActive(false);
            }
        }

        // Helper to log resource lookup and value details for Background/Foreground for Control
        private static void LogResourceDebug(Control ctrl, string type)
        {
            var bgSource = System.Windows.DependencyPropertyHelper.GetValueSource(ctrl, Control.BackgroundProperty);
            var fgSource = System.Windows.DependencyPropertyHelper.GetValueSource(ctrl, Control.ForegroundProperty);
            object bgValue = ctrl.Background, fgValue = ctrl.Foreground;

            Debug.WriteLine($"[{type}] '{ctrl.Name}' Background value source: {bgSource.BaseValueSource}, value: {bgValue}");
            UILogStatus?.Invoke($"{type} '{ctrl.Name}' Background value source: {bgSource.BaseValueSource}, value: {bgValue}");
            Debug.WriteLine($"[{type}] '{ctrl.Name}' Foreground value source: {fgSource.BaseValueSource}, value: {fgValue}");
            UILogStatus?.Invoke($"{type} '{ctrl.Name}' Foreground value source: {fgSource.BaseValueSource}, value: {fgValue}");
        }

        // Helper to log resource lookup and value details for Background/Foreground for TextBlock
        private static void LogTextBlockResourceDebug(TextBlock tb, string type)
        {
            var bgSource = System.Windows.DependencyPropertyHelper.GetValueSource(tb, TextBlock.BackgroundProperty);
            var fgSource = System.Windows.DependencyPropertyHelper.GetValueSource(tb, TextBlock.ForegroundProperty);
            object bgValue = tb.Background, fgValue = tb.Foreground;

            Debug.WriteLine($"[{type}] '{tb.Name}' Background value source: {bgSource.BaseValueSource}, value: {bgValue}");
            UILogStatus?.Invoke($"{type} '{tb.Name}' Background value source: {bgSource.BaseValueSource}, value: {bgValue}");
            Debug.WriteLine($"[{type}] '{tb.Name}' Foreground value source: {fgSource.BaseValueSource}, value: {fgValue}");
            UILogStatus?.Invoke($"{type} '{tb.Name}' Foreground value source: {fgSource.BaseValueSource}, value: {fgValue}");
        }

        // Enhanced debug: Try to match the brush to a StaticResource in the merged dictionaries
        private static void LogStaticResourceDebug(FrameworkElement element, string type)
        {
            // Only run if ThemeDictionaryActive
            if (!ThemeDictionaryActive) return;
            var window = Window.GetWindow(element);
            if (window == null) return;
            var resources = window.Resources.MergedDictionaries;
            foreach (var dict in resources)
            {
                foreach (var key in dict.Keys)
                {
                    var resource = dict[key];
                    if (resource is Brush brush)
                    {
                        if ((element is Control ctrl && ctrl.Background == brush) ||
                            (element is TextBlock tb && tb.Background == brush))
                        {
                            Debug.WriteLine($"[{type}] '{element.Name}' Background uses StaticResource key: {key}");
                            UILogStatus?.Invoke($"{type} '{element.Name}' Background uses StaticResource key: {key}");
                            Debug.WriteLine($"[{type}] Looking up resource '{key}' in ThemeDictionary: {brush}");
                            UILogStatus?.Invoke($"{type} Looking up resource '{key}' in ThemeDictionary: {brush}");
                        }
                        if ((element is Control ctrl2 && ctrl2.Foreground == brush) ||
                            (element is TextBlock tb2 && tb2.Foreground == brush))
                        {
                            Debug.WriteLine($"[{type}] '{element.Name}' Foreground uses StaticResource key: {key}");
                            UILogStatus?.Invoke($"{type} '{element.Name}' Foreground uses StaticResource key: {key}");
                            Debug.WriteLine($"[{type}] Looking up resource '{key}' in ThemeDictionary: {brush}");
                            UILogStatus?.Invoke($"{type} Looking up resource '{key}' in ThemeDictionary: {brush}");
                        }
                    }
                }
            }
        }

        // Log the original XAML StaticResource attribute for the element
        private static void LogXamlStaticResourceDebug(string elementName, string type)
        {
            if (string.IsNullOrEmpty(CurrentXamlContent) || string.IsNullOrEmpty(elementName)) return;
            // Find the element by Name in the XAML
            var regex = new System.Text.RegularExpressions.Regex($"<[^>]*Name=\"{elementName}\"[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var match = regex.Match(CurrentXamlContent);
            if (match.Success)
            {
                string tag = match.Value;
                // Look for StaticResource attributes
                var attrRegex = new System.Text.RegularExpressions.Regex("(Background|Foreground|BorderBrush)=\\\"\\{StaticResource ([^}]+)\\}\\\"");
                var attrMatches = attrRegex.Matches(tag);
                foreach (System.Text.RegularExpressions.Match attrMatch in attrMatches)
                {
                    string prop = attrMatch.Groups[1].Value;
                    string key = attrMatch.Groups[2].Value;
                    Debug.WriteLine($"[{type}] '{elementName}' XAML: {prop}={{StaticResource {key}}}");
                    UILogStatus?.Invoke($"{type} '{elementName}' XAML: {prop}={{StaticResource {key}}}");

                    // Debug: List all available resources
                    var window = Application.Current?.MainWindow;
                    if (window != null)
                    {
                        // Log all available resources in MainWindow
                        Debug.WriteLine($"[{type}] MainWindow has {window.Resources.Count} direct resources, {window.Resources.MergedDictionaries.Count} merged dictionaries");
                        UILogStatus?.Invoke($"{type} MainWindow has {window.Resources.Count} direct resources, {window.Resources.MergedDictionaries.Count} merged dictionaries");

                        foreach (var dict in window.Resources.MergedDictionaries)
                        {
                            Debug.WriteLine($"[{type}] Merged dictionary has {dict.Count} resources");
                            UILogStatus?.Invoke($"{type} Merged dictionary has {dict.Count} resources");
                            foreach (var resKey in dict.Keys)
                            {
                                Debug.WriteLine($"[{type}] Available resource key: {resKey}");
                                UILogStatus?.Invoke($"{type} Available resource key: {resKey}");
                            }
                        }

                        // Try lookup
                        var value = window.TryFindResource(key);
                        Debug.WriteLine($"[{type}] Looking up resource key '{key}' in ThemeDictionary: {value}");
                        UILogStatus?.Invoke($"{type} Looking up resource key '{key}' in ThemeDictionary: {value}");

                        // Also try to find in the element's visual tree if MainWindow lookup fails
                        if (value == null)
                        {
                            // Try looking in the preview host's resources
                            var previewHost = window.FindName("PreviewHost") as ContentControl;
                            if (previewHost?.Content is FrameworkElement content)
                            {
                                Debug.WriteLine($"[{type}] PreviewHost content has {content.Resources.Count} direct resources, {content.Resources.MergedDictionaries.Count} merged dictionaries");
                                UILogStatus?.Invoke($"{type} PreviewHost content has {content.Resources.Count} direct resources, {content.Resources.MergedDictionaries.Count} merged dictionaries");

                                value = content.TryFindResource(key);
                                Debug.WriteLine($"[{type}] Fallback lookup in preview content for '{key}': {value}");
                                UILogStatus?.Invoke($"{type} Fallback lookup in preview content for '{key}': {value}");
                            }
                        }
                    }
                }
            }
        }
    }
}
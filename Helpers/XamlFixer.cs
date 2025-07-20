using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Threading;
using System.Collections.Concurrent;

namespace RCLayoutPreview.Helpers
{
    public static class XamlFixer
    {
        private static Dictionary<string, LayoutSnippet> snippetCache;
        private static CancellationTokenSource _currentCts;
        private static readonly object _syncLock = new object();
        private static readonly ConcurrentDictionary<string, SolidColorBrush> _brushCache = new ConcurrentDictionary<string, SolidColorBrush>();

        public static void ProcessNamedFields(FrameworkElement rootElement, JObject jsonData, bool debugMode)
        {
            if (rootElement == null || jsonData == null)
                throw new ArgumentNullException("Root element or JSON data cannot be null.");

            // Ensure we're on the UI thread
            if (!rootElement.Dispatcher.CheckAccess())
            {
                rootElement.Dispatcher.Invoke(() => ProcessNamedFields(rootElement, jsonData, debugMode));
                return;
            }

            // Cancel any previous processing
            lock (_syncLock)
            {
                if (_currentCts != null)
                {
                    _currentCts.Cancel();
                    _currentCts.Dispose();
                }
                _currentCts = new CancellationTokenSource();
                
                // Add a timeout to prevent infinite processing
                _currentCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout
            }

            var cancellationToken = _currentCts.Token;

            try
            {
                Debug.WriteLine("[ProcessNamedFields] Starting field processing...");
                
                // Pre-fetch all relevant data from JSON to avoid repeated lookups
                var dataCache = PreProcessJsonData(jsonData);
                
                // Process all elements in a single batch
                var elements = new List<FrameworkElement>();
                CollectElementsSafe(rootElement, elements, cancellationToken);
                
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("[ProcessNamedFields] Processing cancelled during collection");
                    return;
                }
                
                int totalElements = elements.Count;
                int processedCount = 0;
                
                foreach (var element in elements)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.WriteLine("[ProcessNamedFields] Processing cancelled");
                        return;
                    }

                    try
                    {
                        ProcessElementSingle(element, dataCache, debugMode);
                        processedCount++;

                        // Update progress every 50 elements and check for timeout
                        if (processedCount % 50 == 0)
                        {
                            Debug.WriteLine($"[ProcessNamedFields] Processed {processedCount}/{totalElements} elements");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ProcessElementSingle] Error processing element {element?.Name}: {ex.Message}");
                    }
                }

                Debug.WriteLine("[ProcessNamedFields] Field processing completed successfully");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ProcessNamedFields] Processing timed out or was cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessNamedFields] Fatal error: {ex.Message}");
            }
        }

        private static Dictionary<string, JToken> PreProcessJsonData(JObject jsonData)
        {
            var cache = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var dataGroup in new[] { "RacerData", "GenericData", "Actions" })
            {
                if (jsonData[dataGroup] is JObject groupData)
                {
                    foreach (var prop in groupData.Properties())
                    {
                        cache[prop.Name] = prop.Value;
                    }
                }
            }
            
            return cache;
        }

        private static void CollectElements(FrameworkElement element, List<FrameworkElement> elements)
        {
            // This method has been replaced by CollectElementsSafe to prevent infinite loops
            // Redirecting to the safe version
            CollectElementsSafe(element, elements, CancellationToken.None);
        }

        private static void CollectElementsSafe(FrameworkElement element, List<FrameworkElement> elements, CancellationToken cancellationToken)
        {
            if (element == null || cancellationToken.IsCancellationRequested) return;

            Debug.WriteLine($"[CollectElementsSafe] Visiting element: {element.GetType().Name} - Name: '{element.Name ?? "(no name)"}'");

            // Prevent runaway collection
            if (elements.Count > 5000)
            {
                Debug.WriteLine("[CollectElementsSafe] Maximum element count reached, stopping collection");
                return;
            }

            if (!string.IsNullOrEmpty(element.Name))
            {
                Debug.WriteLine($"[CollectElementsSafe] >>> FOUND NAMED ELEMENT: '{element.Name}' ({element.GetType().Name})");
                elements.Add(element);
            }

            // Use pattern matching and switch for more efficient type checking
            switch (element)
            {
                case Panel panel:
                    Debug.WriteLine($"[CollectElementsSafe] Panel '{panel.GetType().Name}' has {panel.Children.Count} children");
                    foreach (var child in panel.Children.OfType<FrameworkElement>())
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        CollectElementsSafe(child, elements, cancellationToken);
                    }
                    break;

                case ItemsControl itemsControl:
                    // Limit ItemsControl traversal to prevent runaway
                    var itemCount = Math.Min(itemsControl.Items.Count, 500);
                    Debug.WriteLine($"[CollectElementsSafe] ItemsControl has {itemCount} items");
                    for (int i = 0; i < itemCount; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (itemsControl.Items[i] is FrameworkElement item)
                            CollectElementsSafe(item, elements, cancellationToken);
                    }
                    break;

                    if (foundGroup == "RacerData")
                    {
                        int playerIndex = GetPlayerIndex(parsedField.FieldType);
                        var colorBrush = GetColor(playerIndex);
                        if (element is TextBlock textBlock)
                        {
                            textBlock.Background = colorBrush;
                            textBlock.Foreground = new SolidColorBrush(Colors.White);
                        }
                        else if (element is Label label)
                        {
                            label.Background = colorBrush;
                            label.Foreground = new SolidColorBrush(Colors.White);
                        }
                    }

                    if (debugMode)
                    {
                        // Show the field name only
                        if (element is TextBlock textBlock)
                        {
                            textBlock.Text = element.Name;
                        }
                        else if (element is Label label)
                        {
                            label.Content = element.Name;
                        }
                        else if (element is ContentControl contentControl)
                        {
                            contentControl.Content = element.Name;
                        }
                    }
                    else if (value != null)
                    {
                        // Show the value only
                        string displayText = value.ToString();
                        if (element is TextBlock textBlock)
                        {
                            textBlock.Text = displayText;
                        }
                        else if (element is Label label)
                        {
                            label.Content = displayText;
                        }
                        else if (element is ContentControl contentControl)
                        {
                            contentControl.Content = displayText;
                        }
                    }
                }
            }

            switch (element)
            {
                case TextBlock tb:
                    tb.Text = text;
                    tb.Foreground = textBrush;
                    if (background != null) 
                    {
                        tb.Background = background;
                    }
                    Debug.WriteLine($"[ApplyElementStyle] TextBlock '{tb.Name}' - Text: '{text}', Foreground: {textBrush.Color}, Background: {background?.Color.ToString() ?? "None"}");
                    break;
                    
                case Label lbl:
                    lbl.Content = text;
                    lbl.Foreground = textBrush;
                    if (background != null) 
                    {
                        lbl.Background = background;
                    }
                    Debug.WriteLine($"[ApplyElementStyle] Label '{lbl.Name}' - Content: '{text}', Foreground: {textBrush.Color}, Background: {background?.Color.ToString() ?? "None"}");
                    break;
                    
                case ContentControl cc:
                    cc.Content = text;
                    Debug.WriteLine($"[ApplyElementStyle] ContentControl '{cc.Name}' - Content: '{text}'");
                    break;
            }
        }

        private static int GetPlayerIndex(string fieldType)
        {
            // Check for Lane/Position patterns first
            var laneMatch = Regex.Match(fieldType, @"(?:Lane|Position|RaceLeader|SeasonLeader|SeasonRaceLeader)(\d+)");
            if (laneMatch.Success && int.TryParse(laneMatch.Groups[1].Value, out int laneNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Lane/Position pattern, index: {laneNum}");
                return laneNum;
            }

            // Check for Nickname patterns
            var nameMatch = Regex.Match(fieldType, @"(?:NextHeatNickname|OnDeckNickname|Pos)(\d+)");
            if (nameMatch.Success && int.TryParse(nameMatch.Groups[1].Value, out int nameNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Nickname pattern, index: {nameNum}");
                return nameNum;
            }

            // Check for simple numbered field patterns like "Name1", "LapTime1", etc.
            var simpleNumberMatch = Regex.Match(fieldType, @"^[A-Za-z]+(\d+)$");
            if (simpleNumberMatch.Success && int.TryParse(simpleNumberMatch.Groups[1].Value, out int simpleNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Simple numbered pattern, index: {simpleNum}");
                return simpleNum;
            }

            // Check for field names ending with numbers after underscores (e.g., "SomeField_1")
            var underscoreNumberMatch = Regex.Match(fieldType, @"_(\d+)$");
            if (underscoreNumberMatch.Success && int.TryParse(underscoreNumberMatch.Groups[1].Value, out int underscoreNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Underscore number pattern, index: {underscoreNum}");
                return underscoreNum;
            }

            // Use a hash of the field type for consistent color assignment as fallback
            var hash = Math.Abs(fieldType.GetHashCode());
            int fallbackIndex = (hash % 20) + 1;
            Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Using hash fallback, index: {fallbackIndex}");
            return fallbackIndex;
        }

        private static Color GetPlayerColor(int playerIndex)
        {
            // Map player index to a color (0-7)
            int colorIndex = (playerIndex - 1) % 8;

            return colorIndex switch
            {
                0 => Color.FromRgb(192, 0, 0),      // Red
                1 => Color.FromRgb(0, 112, 192),    // Blue
                2 => Color.FromRgb(0, 176, 80),     // Green
                3 => Color.FromRgb(112, 48, 160),   // Purple
                4 => Color.FromRgb(255, 192, 0),    // Gold
                5 => Color.FromRgb(0, 176, 240),    // Light Blue
                6 => Color.FromRgb(146, 208, 80),   // Light Green
                _ => Color.FromRgb(255, 102, 0)     // Orange (for index 7 or fallback)
            };
        }

        public static string Preprocess(string rawXaml)
        {
            if (string.IsNullOrWhiteSpace(rawXaml))
                throw new ArgumentException("No XAML provided.");

            string xaml = rawXaml.Trim();

            // Add root if missing
            if (!xaml.StartsWith("<"))
                xaml = $"<Grid>{xaml}</Grid>";

            // Add default WPF namespaces if missing
            if (!xaml.Contains("xmlns="))
            {
                var match = Regex.Match(xaml, @"^<(\w+)");
                if (match.Success)
                {
                    string root = match.Groups[1].Value;
                    string ns = $" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
                              + $" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
                    xaml = Regex.Replace(xaml, $"^<{root}", $"<{root}{ns}");
                }
            }

            // Try to parse and clean up attributes
            try
            {
                var xml = XElement.Parse(xaml);

                // Remove invalid Padding from Grid
                foreach (var grid in xml.Descendants().Where(e => e.Name.LocalName == "Grid"))
                    grid.Attributes("Padding").Remove();

                // Remove unknown VerticalAlignment on StackPanels
                foreach (var panel in xml.Descendants().Where(e => e.Name.LocalName == "StackPanel"))
                    panel.Attributes("VerticalAlignment").Where(a => !IsAllowedAlignment(a.Value)).Remove();

                return xml.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                // If preprocessing fails, fall back to the string-based fixes
                return xaml;
            }
        }

        private static bool IsAllowedAlignment(string value)
        {
            return value switch
            {
                "Top" or "Center" or "Bottom" or "Stretch" => true,
                _ => false
            };
        }

        private static LayoutSnippet GetSnippetByName(string name)
        {
            if (snippetCache == null)
            {
                // Cache all snippets by name for faster lookup
                snippetCache = LayoutSnippet.GetDefaultSnippets()
                    .ToDictionary(s => s.Name, s => s);
            }

            return snippetCache.TryGetValue(name, out var snippet) ? snippet : null;
        }
    }
}
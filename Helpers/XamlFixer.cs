using Newtonsoft.Json.Linq;
using RCLayoutPreview.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

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
                        ProcessElementSingle(element, dataCache, debugMode, jsonData);
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

            Debug.WriteLine("[PreProcessJsonData] Processing JSON data for field lookup cache");

            foreach (var dataGroup in new[] { "RacerData", "GenericData", "Actions" })
            {
                if (jsonData[dataGroup] is JObject groupData)
                {
                    Debug.WriteLine($"[PreProcessJsonData] Processing data group: {dataGroup} with {groupData.Count} properties");
                    foreach (var prop in groupData.Properties())
                    {
                        cache[prop.Name] = prop.Value;
                        //Debug.WriteLine($"[PreProcessJsonData] Cached field: '{prop.Name}' = '{prop.Value}'");
                    }
                }
            }

            Debug.WriteLine($"[PreProcessJsonData] Finished building cache with {cache.Count} entries");
            return cache;
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
            }
        }

        private static void ProcessElementSingle(FrameworkElement element, Dictionary<string, JToken> dataCache, bool debugMode, JObject jsonData)
        {
            if (element == null || string.IsNullOrEmpty(element.Name))
                return;

            Debug.WriteLine($"[ProcessElementSingle] ========== Processing element: {element.Name} ({element.GetType().Name}) ==========");

            // Try to parse the field name
            if (!FieldNameParser.TryParse(element.Name, out var parsedField))
            {
                Debug.WriteLine($"[ProcessElementSingle] Could not parse field name: {element.Name}");
                return;
            }

            // Get the lookup field name from parsed field type
            string originalFieldName = element.Name;
            string lookupFieldName = parsedField.FieldType;
            
            Debug.WriteLine($"[ProcessElementSingle] Original field name: {originalFieldName}");
            Debug.WriteLine($"[ProcessElementSingle] Parsed field type for lookup: {lookupFieldName}");

            // Track value resolution
            string foundGroup = null;
            JToken value = null;
            bool valueFound = false;
            string resolvedFieldName = lookupFieldName;
            
            // Try direct lookup with the field type
            if (dataCache.TryGetValue(lookupFieldName, out value))
            {
                foundGroup = DetermineDataGroup(lookupFieldName, jsonData);
                valueFound = true;
                Debug.WriteLine($"[ProcessElementSingle] DIRECT LOOKUP: Found value for {lookupFieldName} in group {foundGroup}: '{value}'");
            }
            else
            {
                // If not found, check if it's a RacerData field by pattern
                if (IsRacerDataField(lookupFieldName))
                {
                    foundGroup = "RacerData";
                    Debug.WriteLine($"[ProcessElementSingle] No value found, but field '{lookupFieldName}' matched RacerData pattern");
                }
                else
                {
                    foundGroup = "GenericData"; // Default
                    Debug.WriteLine($"[ProcessElementSingle] No value found for '{lookupFieldName}', using default GenericData group");
                }
            }

            // Apply styling based on the element type and data
            ApplyElementStyle(element, parsedField, foundGroup, value, valueFound, resolvedFieldName, debugMode);
        }

        private static string DetermineDataGroup(string fieldType, JObject jsonData)
        {
            // First check directly in the JSON data by field type
            if (jsonData["RacerData"] is JObject racerData && racerData[fieldType] != null)
            {
                Debug.WriteLine($"[DetermineDataGroup] Field '{fieldType}' found in RacerData group");
                return "RacerData";
            }
            
            if (jsonData["GenericData"] is JObject genericData && genericData[fieldType] != null)
            {
                Debug.WriteLine($"[DetermineDataGroup] Field '{fieldType}' found in GenericData group");
                return "GenericData";
            }
                
            if (jsonData["Actions"] is JObject actions && actions[fieldType] != null)
            {
                Debug.WriteLine($"[DetermineDataGroup] Field '{fieldType}' found in Actions group");
                return "Actions";
            }
            
            // If not found, check if it matches any RacerData patterns
            if (IsRacerDataField(fieldType))
            {
                Debug.WriteLine($"[DetermineDataGroup] Field '{fieldType}' matched RacerData pattern");
                return "RacerData";
            }
            
            // Default
            Debug.WriteLine($"[DetermineDataGroup] Field '{fieldType}' using default GenericData group");
            return "GenericData";
        }

        private static bool IsRacerDataField(string fieldType)
        {
            // Field patterns that indicate racer-specific data
            string[] racerPatterns = {
                @"^Lane\d+",
                @"^Position\d+",
                @"^RaceLeader\d+",
                @"^SeasonLeader\d+",
                @"^SeasonRaceLeader\d+",
                @"^NextHeat",
                @"^OnDeck",
                @"^Pos\d+",
                @"^Avatar_Lane\d+",
                @"^LapTime_\d+",
                @"^Name\d+",
                @"^Nickname\d+",
                @"^Nickname_Lane\d+",
                @"^LapTime_Lane\d+"
            };

            foreach (var pattern in racerPatterns)
            {
                if (Regex.IsMatch(fieldType, pattern))
                {
                    Debug.WriteLine($"[IsRacerDataField] Field '{fieldType}' matched pattern '{pattern}'");
                    return true;
                }
            }
            
            Debug.WriteLine($"[IsRacerDataField] Field '{fieldType}' did not match any RacerData patterns");
            return false;
        }

        private static void ApplyElementStyle(FrameworkElement element, FieldNameParser parsedField, string foundGroup, JToken value, bool valueFound, string resolvedFieldName, bool debugMode)
        {
            string text;
            
            // Debug field information
            Debug.WriteLine($"[ApplyElementStyle] Styling element '{element.Name}' with resolvedField '{resolvedFieldName}'");
            Debug.WriteLine($"[ApplyElementStyle] Value found: {valueFound}, Value: '{value}', Group: {foundGroup}");
            
            // Determine the text to display
            if (debugMode)
            {
                // In debug mode, show the element name itself
                text = element.Name;
                Debug.WriteLine($"[ApplyElementStyle] Debug mode enabled, using element name as text: '{text}'");
            }
            else if (valueFound && value != null)
            {
                // Use the value from JSON if found
                text = value.ToString();
                Debug.WriteLine($"[ApplyElementStyle] Using JSON value for {resolvedFieldName}: '{text}'");
            }
            else
            {
                // For fields with no value, use empty string
                text = string.Empty;
                Debug.WriteLine($"[ApplyElementStyle] Using default text: '{text}'");
            }

            // Apply color styling based on the field group and type
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush background = null;

            if (foundGroup == "RacerData")
            {
                int playerIndex = GetPlayerIndex(resolvedFieldName);
                background = GetColor(playerIndex);
                textBrush = new SolidColorBrush(Colors.White);
                Debug.WriteLine($"[ApplyElementStyle] Applied RacerData styling for '{resolvedFieldName}' with playerIndex {playerIndex}, background color: {background.Color}");
            }
            else
            {
                Debug.WriteLine($"[ApplyElementStyle] Using default styling for non-RacerData field '{resolvedFieldName}'");
            }

            // Apply styling based on element type
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
            Debug.WriteLine($"[GetPlayerIndex] Determining player index for field: '{fieldType}'");
            
            // Check for Lane/Position patterns first
            var laneMatch = Regex.Match(fieldType, @"(?:Lane|Position|RaceLeader|SeasonLeader|SeasonRaceLeader)(\d+)");
            if (laneMatch.Success && int.TryParse(laneMatch.Groups[1].Value, out int laneNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Lane/Position pattern matched, index: {laneNum}");
                return laneNum;
            }

            // Check for Nickname patterns with NextHeat, OnDeck, etc.
            var nameMatch = Regex.Match(fieldType, @"(?:NextHeatNickname|OnDeckNickname|Pos)(\d+)");
            if (nameMatch.Success && int.TryParse(nameMatch.Groups[1].Value, out int nameNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Nickname pattern matched, index: {nameNum}");
                return nameNum;
            }

            // Check for simple numbered field patterns like "Name1", "LapTime1", etc.
            var simpleNumberMatch = Regex.Match(fieldType, @"^[A-Za-z]+(\d+)$");
            if (simpleNumberMatch.Success && int.TryParse(simpleNumberMatch.Groups[1].Value, out int simpleNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Simple numbered pattern matched, index: {simpleNum}");
                return simpleNum;
            }

            // Check for field names ending with numbers after underscores (e.g., "SomeField_1")
            var underscoreNumberMatch = Regex.Match(fieldType, @"_(\d+)$");
            if (underscoreNumberMatch.Success && int.TryParse(underscoreNumberMatch.Groups[1].Value, out int underscoreNum))
            {
                Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - Underscore number pattern matched, index: {underscoreNum}");
                return underscoreNum;
            }

            // Use a hash of the field type for consistent color assignment as fallback
            var hash = Math.Abs(fieldType.GetHashCode());
            int fallbackIndex = (hash % 20) + 1;
            Debug.WriteLine($"[GetPlayerIndex] '{fieldType}' - No pattern matched, using hash fallback, index: {fallbackIndex}");
            return fallbackIndex;
        }

        private static SolidColorBrush GetColor(int playerIndex)
        {
            // Use cached brush if available to improve performance
            string key = $"Player{playerIndex}";
            var brush = _brushCache.GetOrAdd(key, _ => new SolidColorBrush(GetPlayerColor(playerIndex)));
            Debug.WriteLine($"[GetColor] Player index {playerIndex} maps to color: {brush.Color}");
            return brush;
        }

        private static Color GetPlayerColor(int playerIndex)
        {
            // Map player index to a color (0-7)
            int colorIndex = (playerIndex - 1) % 8;
            
            Color color = colorIndex switch
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
            
            Debug.WriteLine($"[GetPlayerColor] Color index {colorIndex} maps to RGB color: ({color.R},{color.G},{color.B})");
            return color;
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
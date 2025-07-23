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

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Handles generic XAML preprocessing and attribute cleanup.
    /// This class should only contain logic that is not specific to placeholders or stubdata fields.
    /// </summary>
    public static class XamlFixer
    {
        private static Dictionary<string, LayoutSnippet> snippetCache;

        /// <summary>
        /// Preprocesses raw XAML string to ensure it is valid and well-formed for WPF parsing.
        /// Adds missing root and namespaces, and cleans up invalid attributes.
        /// </summary>
        /// <param name="rawXaml">Raw XAML string</param>
        /// <returns>Processed XAML string</returns>
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

        /// <summary>
        /// Checks if a VerticalAlignment value is valid for StackPanel.
        /// </summary>
        private static bool IsAllowedAlignment(string value)
        {
            return value switch
            {
                "Top" or "Center" or "Bottom" or "Stretch" => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// Handles detection, lookup, and display of placeholder elements in XAML.
    /// This class is completely independent of stubdata field logic.
    /// </summary>
    public static class PlaceholderHandler
    {
        /// <summary>
        /// Determines if the given FrameworkElement is a placeholder (by name convention).
        /// </summary>
        public static bool IsPlaceholderElement(FrameworkElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Name) && element.Name.StartsWith("Placeholder");
        }

        /// <summary>
        /// Displays the placeholder value in the element, using position if needed.
        /// </summary>
        public static void DisplayPlaceholder(FrameworkElement element, int position = 1)
        {
            if (element == null) return;
            string initialValue = "";
            // Get the initial value from the element
            if (element is Label lbl)
                initialValue = lbl.Content?.ToString() ?? "";
            else if (element is TextBlock tb)
                initialValue = tb.Text ?? "";
            // Replace {0} with position if present
            if (initialValue.Contains("{0}"))
                initialValue = string.Format(initialValue, position);
            // Set the value and style for preview
            if (element is TextBlock textBlock)
            {
                textBlock.Text = initialValue;
                textBlock.Background = new SolidColorBrush(Colors.Black);
                textBlock.Foreground = new SolidColorBrush(Colors.White);
            }
            else if (element is Label label)
            {
                label.Content = initialValue;
                label.Background = new SolidColorBrush(Colors.Black);
                label.Foreground = new SolidColorBrush(Colors.White);
            }
        }
    }

    /// <summary>
    /// Handles detection, lookup, and display of stubdata5 (JSON) fields in XAML.
    /// This class is completely independent of placeholder logic.
    /// </summary>
    public static class StubDataFieldHandler
    {
        /// <summary>
        /// Determines if the given FrameworkElement is a stubdata field (by parsing its name).
        /// </summary>
        public static bool IsStubDataField(FrameworkElement element)
        {
            return element != null && FieldNameParser.TryParse(element.Name, out _);
        }

        /// <summary>
        /// Looks up the field value in the provided JSON and displays it in the element.
        /// </summary>
        public static void DisplayStubDataField(FrameworkElement element, JObject jsonData, bool debugMode)
        {
            if (element == null || jsonData == null) return;
            if (!FieldNameParser.TryParse(element.Name, out var parsedField)) return;
            string normalizedFieldType = Regex.Replace(parsedField.FieldType, @"(_\d+)$", "");
            JToken value = null;
            string foundGroup = null;
            // Try to find the value in RacerData, GenericData, or Actions
            if (jsonData["RacerData"] is JObject racerData)
            {
                if (racerData.TryGetValue(parsedField.FieldType, out value) ||
                    racerData.TryGetValue(normalizedFieldType, out value))
                {
                    foundGroup = "RacerData";
                }
            }
            if (value == null && jsonData["GenericData"] is JObject genericData)
            {
                if (genericData.TryGetValue(parsedField.FieldType, out value) ||
                    genericData.TryGetValue(normalizedFieldType, out value))
                {
                    foundGroup = "GenericData";
                }
            }
            if (value == null && jsonData["Actions"] is JObject actionsData)
            {
                if (actionsData.TryGetValue(parsedField.FieldType, out value) ||
                    actionsData.TryGetValue(normalizedFieldType, out value))
                {
                    foundGroup = "Actions";
                }
            }
            // Always set default foreground to white for preview
            if (element is TextBlock textBlock)
                textBlock.Foreground = new SolidColorBrush(Colors.White);
            else if (element is Label label)
                label.Foreground = new SolidColorBrush(Colors.White);
            // Display diagnostics mode (field name only)
            if (debugMode)
            {
                string displayText = parsedField.FieldType;
                if (element is TextBlock tb2)
                    tb2.Text = displayText;
                else if (element is Label lbl)
                    lbl.Content = displayText;
                else if (element is ContentControl contentControl)
                    contentControl.Content = displayText;
            }
            // Display actual value if found
            else if (value != null)
            {
                string displayText = value.ToString();
                if (foundGroup == "RacerData")
                {
                    int playerIndex = GetPlayerIndex(parsedField.FieldType);
                    var colorBrush = GetColor(playerIndex);
                    if (element is TextBlock tb3)
                    {
                        tb3.Text = displayText;
                        tb3.Background = colorBrush;
                    }
                    else if (element is Label lbl)
                    {
                        lbl.Content = displayText;
                        lbl.Background = colorBrush;
                    }
                    else if (element is ContentControl contentControl)
                    {
                        contentControl.Content = displayText;
                    }
                }
                else
                {
                    if (element is TextBlock tb4)
                        tb4.Text = displayText;
                    else if (element is Label lbl)
                        lbl.Content = displayText;
                    else if (element is ContentControl contentControl)
                        contentControl.Content = displayText;
                }
            }
        }

        /// <summary>
        /// Gets the player index from the field type for color assignment.
        /// </summary>
        private static int GetPlayerIndex(string fieldType)
        {
            var laneMatch = Regex.Match(fieldType, @"(?:Lane|Position|RaceLeader|SeasonLeader|SeasonRaceLeader)(\d+)");
            if (laneMatch.Success && int.TryParse(laneMatch.Groups[1].Value, out int laneNum))
                return laneNum;
            var nameMatch = Regex.Match(fieldType, @"(?:NextHeatNickname|OnDeckNickname|Pos)(\d+)");
            if (nameMatch.Success && int.TryParse(nameMatch.Groups[1].Value, out int nameNum))
                return nameNum;
            return Math.Abs(fieldType.GetHashCode() % 20) + 1;
        }

        /// <summary>
        /// Gets a color brush for the player index.
        /// </summary>
        private static SolidColorBrush GetColor(int playerIndex)
        {
            switch ((playerIndex - 1) % 8)
            {
                case 0: return new SolidColorBrush(Color.FromRgb(192, 0, 0));      // Red
                case 1: return new SolidColorBrush(Color.FromRgb(0, 112, 192));    // Blue
                case 2: return new SolidColorBrush(Color.FromRgb(0, 176, 80));     // Green
                case 3: return new SolidColorBrush(Color.FromRgb(112, 48, 160));   // Purple
                case 4: return new SolidColorBrush(Color.FromRgb(255, 192, 0));    // Gold
                case 5: return new SolidColorBrush(Color.FromRgb(0, 176, 240));    // Light Blue
                case 6: return new SolidColorBrush(Color.FromRgb(146, 208, 80));   // Light Green
                case 7: return new SolidColorBrush(Color.FromRgb(255, 102, 0));    // Orange
                default: return new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray (fallback)
            }
        }
    }
}
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    public static class XamlFixer
    {
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

        public static void TestXamlFixer()
        {
            string[] testInputs = new[]
            {
                "<TextBlock Text=\"Hello\" />",
                "<Grid><TextBlock Text=\"Hi\" /></Grid>",
                "<Grid Padding=\"10\"><TextBlock Text=\"Test\" /></Grid>",
                "<StackPanel VerticalAlignment=\"Middle\"><TextBlock Text=\"Bad Align\" /></StackPanel>",
                "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><TextBlock Text=\"OK\" /></Grid>"
            };

            foreach (var input in testInputs)
            {
                string output = Preprocess(input);
                Console.WriteLine("Input:\n" + input);
                Console.WriteLine("Output:\n" + output);
                Console.WriteLine("-----");
            }
        }

        public static void ProcessNamedFields(FrameworkElement rootElement, JObject jsonData, bool debugMode)
        {
            if (rootElement == null || jsonData == null)
                throw new ArgumentNullException("Root element or JSON data cannot be null.");

            Debug.WriteLine("[ProcessNamedFields] Starting field processing...");
            ProcessElementRecursively(rootElement, jsonData, debugMode);
            Debug.WriteLine("[ProcessNamedFields] Field processing completed.");
        }

        private static void ProcessElementRecursively(FrameworkElement element, JObject jsonData, bool debugMode)
        {
            if (!string.IsNullOrEmpty(element.Name))
            {
                if (FieldNameParser.TryParse(element.Name, out var parsedField))
                {
                    // Look up in each group: RacerData, GenericData, Actions
                    JToken value = null;
                    string foundGroup = null;

                    if (jsonData["RacerData"] is JObject racerData && racerData.TryGetValue(parsedField.FieldType, out value))
                    {
                        foundGroup = "RacerData";
                    }
                    else if (jsonData["GenericData"] is JObject genericData && genericData.TryGetValue(parsedField.FieldType, out value))
                    {
                        foundGroup = "GenericData";
                    }
                    else if (jsonData["Actions"] is JObject actionsData && actionsData.TryGetValue(parsedField.FieldType, out value))
                    {
                        foundGroup = "Actions";
                    }

                    if (value != null)
                    {
                        // Apply color to both TextBlock and Label elements from RacerData
                        if (foundGroup == "RacerData")
                        {
                            // Extract player/lane identifier for consistent color per player
                            int playerIndex = GetPlayerIndexFromField(parsedField.FieldType);
                            var colorBrush = GetPlayerColorBrush(playerIndex);

                            if (element is TextBlock textBlock)
                            {
                                textBlock.Text = value.ToString();
                                textBlock.Background = colorBrush;
                                textBlock.Foreground = new SolidColorBrush(Colors.White);
                            }
                            else if (element is Label label)
                            {
                                label.Content = value.ToString();
                                label.Background = colorBrush;
                                label.Foreground = new SolidColorBrush(Colors.White);
                            }
                            else if (element is ContentControl contentControl)
                            {
                                contentControl.Content = value.ToString();
                            }
                        }
                        else
                        {
                            // For non-RacerData fields, just set the content without background
                            if (element is TextBlock textBlock)
                            {
                                textBlock.Text = value.ToString();
                            }
                            else if (element is Label label)
                            {
                                label.Content = value.ToString();
                            }
                            else if (element is ContentControl contentControl)
                            {
                                contentControl.Content = value.ToString();
                            }
                        }
                    }
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is FrameworkElement childElement)
                {
                    ProcessElementRecursively(childElement, jsonData, debugMode);
                }
            }
        }

        private static int GetPlayerIndexFromField(string fieldType)
        {
            // Match patterns like Lane1, Position2, RaceLeader3, etc.
            var laneMatch = Regex.Match(fieldType, @"(?:Lane|Position|RaceLeader|SeasonLeader|SeasonRaceLeader)(\d+)");
            if (laneMatch.Success && int.TryParse(laneMatch.Groups[1].Value, out int laneNum))
            {
                return laneNum;
            }

            // Match patterns like NextHeatNickname1, OnDeckNickname2, etc.
            var nameMatch = Regex.Match(fieldType, @"(?:NextHeatNickname|OnDeckNickname|Pos)(\d+)");
            if (nameMatch.Success && int.TryParse(nameMatch.Groups[1].Value, out int nameNum))
            {
                return nameNum;
            }

            // If no specific pattern matches, use a hash of the field type for a consistent color
            return Math.Abs(fieldType.GetHashCode() % 20) + 1;
        }

        private static SolidColorBrush GetPlayerColorBrush(int playerIndex)
        {
            // Use a fixed set of distinct colors for players
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
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

        public static void ProcessNamedFields(FrameworkElement rootElement, JObject jsonData, bool debugMode)
        {
            if (rootElement == null || jsonData == null)
                throw new ArgumentNullException("Root element or JSON data cannot be null.");

            Debug.WriteLine("[ProcessNamedFields] Starting field processing...");
            try
            {
                ProcessElementRecursively(rootElement, jsonData, debugMode);
                Debug.WriteLine("[ProcessNamedFields] Field processing completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessNamedFields] Error during field processing: {ex.Message}");
                throw;
            }
        }

        private static void ProcessElementRecursively(FrameworkElement element, JObject jsonData, bool debugMode)
        {
            try
            {
                if (!string.IsNullOrEmpty(element.Name))
                {
                    Debug.WriteLine($"[ProcessElementRecursively] Processing element: {element.Name}");

                    if (debugMode)
                    {
                        // Display the field name instead of the value
                        Debug.WriteLine($"[Diagnostics Mode] Element Name: {element.Name}");
                        if (element is TextBlock textBlock)
                        {
                            textBlock.Text = element.Name;
                            Debug.WriteLine($"[Diagnostics Mode] TextBlock updated with Name: {element.Name}");
                        }
                        else if (element is Label label)
                        {
                            label.Content = element.Name;
                            Debug.WriteLine($"[Diagnostics Mode] Label updated with Name: {element.Name}");
                        }
                        else if (element is ContentControl contentControl)
                        {
                            contentControl.Content = element.Name;
                            Debug.WriteLine($"[Diagnostics Mode] ContentControl updated with Name: {element.Name}");
                        }
                    }
                    else
                    {
                        // Display the value normally
                        JToken value = jsonData.SelectToken(element.Name);
                        if (value != null)
                        {
                            string displayText = value.ToString();
                            Debug.WriteLine($"[Normal Mode] Element Name: {element.Name}, Value: {displayText}");
                            if (element is TextBlock textBlock)
                            {
                                textBlock.Text = displayText;
                                Debug.WriteLine($"[Normal Mode] TextBlock updated with Value: {displayText}");
                            }
                            else if (element is Label label)
                            {
                                label.Content = displayText;
                                Debug.WriteLine($"[Normal Mode] Label updated with Value: {displayText}");
                            }
                            else if (element is ContentControl contentControl)
                            {
                                contentControl.Content = displayText;
                                Debug.WriteLine($"[Normal Mode] ContentControl updated with Value: {displayText}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessElementRecursively] Error processing element: {ex.Message}");
                throw;
            }
        }

        private static int GetPlayerIndex(string fieldType)
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
    }
}
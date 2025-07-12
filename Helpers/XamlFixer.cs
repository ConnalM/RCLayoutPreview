using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls; // Added for TextBlock
using System.Windows.Media; // Added for SolidColorBrush and Colors
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

                // Add more fixer rules here...

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
                // No root, no namespace
                "<TextBlock Text=\"Hello\" />",

                // No namespace
                "<Grid><TextBlock Text=\"Hi\" /></Grid>",

                // Invalid attribute
                "<Grid Padding=\"10\"><TextBlock Text=\"Test\" /></Grid>",

                // StackPanel with bad alignment
                "<StackPanel VerticalAlignment=\"Middle\"><TextBlock Text=\"Bad Align\" /></StackPanel>",

                // Well-formed XAML
                "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><TextBlock Text=\"OK\" /></Grid>"
            };

            foreach (var input in testInputs)
            {
                string output = RCLayoutPreview.Helpers.XamlFixer.Preprocess(input);
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
            Debug.WriteLine($"[ProcessNamedFields] Processing element of type: {element.GetType().Name}, Name: {element.Name ?? "(Unnamed)"}");

            if (!string.IsNullOrEmpty(element.Name))
            {
                // Parse the field name using FieldNameParser
                if (FieldNameParser.TryParse(element.Name, out var parsedField))
                {
                    Debug.WriteLine($"[ProcessNamedFields] Parsed FieldType: {parsedField.FieldType}, InstanceIndex: {parsedField.InstanceIndex}");

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
                        Debug.WriteLine($"[ProcessNamedFields] JSON value found for {parsedField.FieldType} in {foundGroup}: {value}");

                        if (element is TextBlock textBlock)
                        {
                            textBlock.Text = value.ToString();
                            if (foundGroup == "RacerData")
                            {
                                // Assign background color dynamically based on field name hash
                                int hash = parsedField.FieldType.GetHashCode();
                                byte r = (byte)((hash >> 16) & 0xFF);
                                byte g = (byte)((hash >> 8) & 0xFF);
                                byte b = (byte)(hash & 0xFF);
                                textBlock.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
                                Debug.WriteLine($"[ProcessNamedFields] Background color set for {parsedField.FieldType}: RGB({r}, {g}, {b})");
                            }
                        }
                        else if (element is ContentControl contentControl)
                        {
                            contentControl.Content = value.ToString();
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ProcessNamedFields] No JSON value found for {parsedField.FieldType}");
                    }

                    if (debugMode)
                    {
                        // Highlight the element in debug mode
                        element.ToolTip = $"Bound to: {parsedField.FieldType}";
                    }
                }
                else
                {
                    Debug.WriteLine($"[ProcessNamedFields] Failed to parse field name: {element.Name}");
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is FrameworkElement childElement)
                {
                    ProcessElementRecursively(childElement, jsonData, debugMode);
                }
                else
                {
                    Debug.WriteLine($"[ProcessNamedFields] Skipping non-FrameworkElement child of type: {child.GetType().Name}");
                }
            }
        }
    }
}

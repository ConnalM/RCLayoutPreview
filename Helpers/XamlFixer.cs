using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls; // Added for TextBlock
using Newtonsoft.Json.Linq;

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

            foreach (var child in LogicalTreeHelper.GetChildren(rootElement))
            {
                if (child is FrameworkElement element && !string.IsNullOrEmpty(element.Name))
                {
                    if (jsonData.TryGetValue(element.Name, out var value))
                    {
                        if (element is TextBlock textBlock)
                        {
                            textBlock.Text = value.ToString();
                        }
                        else if (element is ContentControl contentControl)
                        {
                            contentControl.Content = value.ToString();
                        }
                        // Add more element type handling as needed
                    }

                    if (debugMode)
                    {
                        // Highlight the element in debug mode
                        element.ToolTip = $"Bound to: {element.Name}";
                    }
                }
            }
        }
    }
}

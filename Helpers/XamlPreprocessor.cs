using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Handles generic XAML preprocessing and attribute cleanup.
    /// </summary>
    public static class XamlPreprocessor
    {
        /// <summary>
        /// Cleans and preprocesses raw XAML string for safe parsing and preview.
        /// Adds missing root, namespaces, and removes unwanted attributes.
        /// </summary>
        /// <param name="rawXaml">Raw XAML string</param>
        /// <returns>Preprocessed XAML string</returns>
        public static string Preprocess(string rawXaml)
        {
            if (string.IsNullOrWhiteSpace(rawXaml))
                throw new ArgumentException("No XAML provided.");

            string xaml = rawXaml.Trim();
            // Ensure XAML starts with a root element
            if (!xaml.StartsWith("<"))
                xaml = $"<Grid>{xaml}</Grid>";
            // Add default namespaces if missing
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
            try
            {
                // Parse as XML and clean up attributes
                var xml = XElement.Parse(xaml);
                // Remove Padding from all Grids
                foreach (var grid in xml.Descendants().Where(e => e.Name.LocalName == "Grid"))
                    grid.Attributes("Padding").Remove();
                // Remove invalid VerticalAlignment from StackPanels
                foreach (var panel in xml.Descendants().Where(e => e.Name.LocalName == "StackPanel"))
                    panel.Attributes("VerticalAlignment").Where(a => !IsAllowedAlignment(a.Value)).Remove();
                return xml.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                // If XML parsing fails, return original string
                return xaml;
            }
        }

        /// <summary>
        /// Checks if a VerticalAlignment value is allowed for StackPanel.
        /// </summary>
        /// <param name="value">Alignment value</param>
        /// <returns>True if allowed, false otherwise</returns>
        private static bool IsAllowedAlignment(string value)
        {
            return value switch
            {
                "Top" or "Center" or "Bottom" or "Stretch" => true,
                _ => false
            };
        }
    }
}
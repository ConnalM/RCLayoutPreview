using System;
using System.Text.RegularExpressions;
using System.Diagnostics;
using RCLayoutPreview.Helpers;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Manages the automatic detection and removal of placeholder elements from layouts
    /// when valid Race Coordinator field names are detected.
    /// </summary>
    public static class PlaceholderSwapManager
    {
        // Regex pattern for the default placeholder element
        private static readonly string DefaultPlaceholderPattern =
            @"<TextBlock\s+Text=""Race Layout Preview Loaded""[^>]*(?:/>|>[^<]*</TextBlock>)";

        /// <summary>
        /// Checks if the XAML contains any non-placeholder field name (generic detection).
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>True if a valid field is found, false otherwise</returns>
        public static bool ContainsValidField(string xamlContent)
        {
            // Inline field name detection logic since FieldNameHelper and PlaceholderHelper do not provide it
            if (string.IsNullOrWhiteSpace(xamlContent)) return false;
            var fieldNameRegex = new System.Text.RegularExpressions.Regex("Name=\"([^\"]+)\"");
            var matches = fieldNameRegex.Matches(xamlContent);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var name = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("Placeholder"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the first non-placeholder field name found in the XAML content.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>The detected field name, or null if none is found</returns>
        public static string GetFirstFieldName(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent)) return null;
            var fieldNameRegex = new System.Text.RegularExpressions.Regex("Name=\"([^\"]+)\"");
            var matches = fieldNameRegex.Matches(xamlContent);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var name = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("Placeholder"))
                    return name;
            }
            return null;
        }

        /// <summary>
        /// Determines if the XAML contains the default placeholder element.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>True if the placeholder is found, false otherwise</returns>
        public static bool ContainsPlaceholder(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return false;
            return Regex.IsMatch(xamlContent, DefaultPlaceholderPattern);
        }

        /// <summary>
        /// Generates a message based on the field type detected in the XAML content.
        /// </summary>
        /// <param name="xamlContent">The XAML content to analyze</param>
        /// <returns>A descriptive message about the detected field</returns>
        public static string GenerateFieldDetectedMessage(string xamlContent)
        {
            var fieldName = GetFirstFieldName(xamlContent);
            if (string.IsNullOrEmpty(fieldName))
                return null;
            // Message is generic, just shows the field name
            return $"Now showing: {fieldName} data";
        }

        /// <summary>
        /// Replaces the placeholder element with a message about the detected field.
        /// </summary>
        /// <param name="xamlContent">The XAML content to modify</param>
        /// <param name="message">The message to display instead of the placeholder</param>
        /// <returns>The modified XAML content</returns>
        public static string ReplacePlaceholderWithMessage(string xamlContent, string message)
        {
            // Replace the placeholder with a TextBlock showing the message
            return Regex.Replace(xamlContent, DefaultPlaceholderPattern,
                $"<TextBlock Text=\"{message}\" FontSize=\"20\" Foreground=\"Gray\" HorizontalAlignment=\"Center\" />");
        }

        /// <summary>
        /// Removes the placeholder element entirely from the XAML content.
        /// </summary>
        /// <param name="xamlContent">The XAML content to modify</param>
        /// <returns>The modified XAML content</returns>
        public static string RemovePlaceholder(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return xamlContent;
            return Regex.Replace(xamlContent, DefaultPlaceholderPattern, "");
        }
    }
}
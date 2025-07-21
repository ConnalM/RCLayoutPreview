using System;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Manages the automatic detection and removal of placeholder elements from layouts
    /// when valid Race Coordinator field names are detected.
    /// </summary>
    public static class PlaceholderSwapManager
    {
        // The placeholder element pattern to search for and replace
        private static readonly string DefaultPlaceholderPattern =
            @"Name=""Placeholder\d+""";

        /// <summary>
        /// Checks if the XAML contains a valid field name based on predefined patterns.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>True if a valid field is found, false otherwise</returns>
        public static bool ContainsValidField(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return false;

            return Regex.IsMatch(xamlContent, DefaultPlaceholderPattern);
        }

        /// <summary>
        /// Gets the first field name found in the XAML content.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>The detected field name, or null if none is found</returns>
        public static string GetFirstFieldName(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return null;

            var match = Regex.Match(xamlContent, DefaultPlaceholderPattern);
            if (match.Success)
            {
                var nameMatch = Regex.Match(match.Value, @"Name=""([^""]*)""");
                if (nameMatch.Success && nameMatch.Groups.Count > 1)
                {
                    return nameMatch.Groups[1].Value;
                }
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
        /// Replaces the placeholder with a temporary field name
        /// </summary>
        public static string RemovePlaceholder(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return xamlContent;

            var match = Regex.Match(xamlContent, DefaultPlaceholderPattern);
            if (match.Success)
            {
                var placeholder = match.Value;
                var fieldName = "Temp_Field_1";
                return xamlContent.Replace(placeholder, $"Name=\"{fieldName}\"");
            }

            return xamlContent;
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

            return $"Placeholder replaced with temporary field";
        }
    }
}
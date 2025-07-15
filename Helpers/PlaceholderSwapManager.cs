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
            @"<TextBlock\s+Text=""Race Layout Preview Loaded""[^>]*(?:/>|>[^<]*</TextBlock>)";

        // Patterns for valid field names in Race Coordinator
        private static readonly string[] ValidFieldPatterns = new[]
        {
            @"Name=""(?:LapTime|BestLap|AvgLap|LastLap|Position|Nickname)_Position\d+(?:_\d+)?""",
            @"Name=""(?:NextHeatName|NextHeatNickname\d+|OnDeckName|OnDeckNickname\d+)(?:_\d+)?""",
            @"Name=""(?:RaceTimer|LapRecord|LapRecordHolder|CurrentEventName|CurrentTrackName)(?:_\d+)?""",
            @"Name=""(?:Avatar_Position\d+)(?:_\d+)?""",
            @"Name=""(?:SeasonLeader\d+|RaceLeader\d+|SeasonRaceLeader\d+)(?:_\d+)?"""
        };

        /// <summary>
        /// Checks if the XAML contains a valid field name based on predefined patterns.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>True if a valid field is found, false otherwise</returns>
        public static bool ContainsValidField(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return false;

            foreach (var pattern in ValidFieldPatterns)
            {
                if (Regex.IsMatch(xamlContent, pattern, RegexOptions.IgnoreCase))
                {
                    Debug.WriteLine($"[PlaceholderSwapManager] Valid field detected with pattern: {pattern}");
                    return true;
                }
            }

            return false;
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

            foreach (var pattern in ValidFieldPatterns)
            {
                var match = Regex.Match(xamlContent, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var nameMatch = Regex.Match(match.Value, @"Name=""([^""]*)""");
                    if (nameMatch.Success && nameMatch.Groups.Count > 1)
                    {
                        return nameMatch.Groups[1].Value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes the default placeholder element from the XAML.
        /// </summary>
        /// <param name="xamlContent">The XAML content to process</param>
        /// <returns>The XAML content with placeholder removed if appropriate</returns>
        public static string RemovePlaceholder(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return xamlContent;

            if (ContainsPlaceholder(xamlContent) && ContainsValidField(xamlContent))
            {
                // Replace the placeholder with an empty string
                var result = Regex.Replace(xamlContent, DefaultPlaceholderPattern, "", RegexOptions.IgnoreCase);
                Debug.WriteLine("[PlaceholderSwapManager] Placeholder removed from XAML");
                return result;
            }

            return xamlContent;
        }

        /// <summary>
        /// Replaces the default placeholder with a customized message.
        /// </summary>
        /// <param name="xamlContent">The XAML content to process</param>
        /// <param name="message">The new message to display</param>
        /// <returns>The XAML content with placeholder replaced</returns>
        public static string ReplacePlaceholderWithMessage(string xamlContent, string message)
        {
            if (string.IsNullOrWhiteSpace(xamlContent) || string.IsNullOrWhiteSpace(message))
                return xamlContent;

            if (ContainsPlaceholder(xamlContent))
            {
                var newTextBlock = $"<TextBlock Text=\"{message}\" FontSize=\"22\" FontWeight=\"Bold\" Foreground=\"DarkGray\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\" />";
                
                var result = Regex.Replace(xamlContent, DefaultPlaceholderPattern, newTextBlock, RegexOptions.IgnoreCase);
                Debug.WriteLine($"[PlaceholderSwapManager] Placeholder replaced with message: {message}");
                return result;
            }

            return xamlContent;
        }

        /// <summary>
        /// Determines if the XAML contains the default placeholder.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>True if the placeholder is present, false otherwise</returns>
        public static bool ContainsPlaceholder(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return false;

            return Regex.IsMatch(xamlContent, DefaultPlaceholderPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Generates a dynamic message based on the detected field.
        /// </summary>
        /// <param name="xamlContent">The XAML content to check</param>
        /// <returns>A customized message or null if no field was detected</returns>
        public static string GenerateFieldDetectedMessage(string xamlContent)
        {
            var fieldName = GetFirstFieldName(xamlContent);
            if (string.IsNullOrWhiteSpace(fieldName))
                return null;

            // Parse the field name to generate a friendly message
            if (FieldNameParser.TryParse(fieldName, out var parsed))
            {
                string fieldType = parsed.FieldType;

                if (fieldType.Contains("LapTime") || fieldType.Contains("BestLap"))
                    return $"Now showing: Lap Time data";
                
                if (fieldType.Contains("Position"))
                    return $"Now showing: Position data";
                
                if (fieldType.Contains("Nickname"))
                    return $"Now showing: Racer information";
                
                if (fieldType.Contains("RaceTimer"))
                    return $"Now showing: Race Timer";
                
                if (fieldType.Contains("NextHeat") || fieldType.Contains("OnDeck"))
                    return $"Now showing: Upcoming heat information";
                
                return $"Now showing: {fieldType} data";
            }

            return "Layout preview active";
        }
    }
}
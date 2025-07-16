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

        // Patterns for valid field names in Race Coordinator - updated for correct format
        private static readonly string[] ValidFieldPatterns = new[]
        {
            @"Name=""(?:LapTime|BestLap|AvgLap|LastLap|Position|Nickname)_\d+(?:_\d+)?""",
            @"Name=""(?:NextHeatName|NextHeatNickname\d+|OnDeckName|OnDeckNickname\d+)(?:_\d+)?""",
            @"Name=""(?:RaceTimer|LapRecord|LapRecordHolder|CurrentEventName|CurrentTrackName)(?:_\d+)?""",
            @"Name=""(?:Avatar)_\d+(?:_\d+)?""",
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

            // Parse the field name to get its components
            var fieldParts = fieldName.Split('_');
            if (fieldParts.Length == 0)
                return null;

            string fieldType = fieldParts[0];
            string dataType = "";

            // Determine the data type based on the field name
            if (fieldType.StartsWith("LapTime"))
                dataType = "Lap Time";
            else if (fieldType.StartsWith("BestLap"))
                dataType = "Best Lap";
            else if (fieldType.StartsWith("AvgLap"))
                dataType = "Average Lap";
            else if (fieldType.StartsWith("LastLap"))
                dataType = "Last Lap";
            else if (fieldType.StartsWith("Nickname"))
                dataType = "Racer Name";
            else if (fieldType.StartsWith("Position"))
                dataType = "Position";
            else if (fieldType.StartsWith("NextHeat"))
                dataType = "Next Heat";
            else if (fieldType.StartsWith("RaceTimer"))
                dataType = "Race Timer";
            else
                dataType = fieldType;

            return $"Now showing: {dataType} data";
        }

        /// <summary>
        /// Replaces the placeholder element with a message about the detected field.
        /// </summary>
        /// <param name="xamlContent">The XAML content to modify</param>
        /// <param name="message">The message to display instead of the placeholder</param>
        /// <returns>The modified XAML content</returns>
        public static string ReplacePlaceholderWithMessage(string xamlContent, string message)
        {
            // Do not replace the placeholder; keep the original text intact
            return xamlContent;
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
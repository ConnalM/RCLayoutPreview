using System.Text.RegularExpressions;
using RCLayoutPreview.Helpers;

namespace RCLayoutPreview.Helpers
{
    public static class PlaceholderHelper
    {
        // Use constants for magic strings
        private static readonly Regex PlaceholderRegex = new Regex($"Name=\"{AppConstants.PlaceholderPrefix}\\d+\"", RegexOptions.Compiled);
        private static readonly Regex PlaceholderNameOnlyRegex = new Regex($"{AppConstants.PlaceholderPrefix}\\d+", RegexOptions.Compiled);

        public static bool ContainsPlaceholder(string xaml)
        {
            return PlaceholderRegex.IsMatch(xaml);
        }

        public static string FindNearestPlaceholder(string text, int caretOffset)
        {
            int startPos = System.Math.Max(0, caretOffset - 100);
            int endPos = System.Math.Min(text.Length, caretOffset + 100);
            string searchText = text.Substring(startPos, endPos - startPos);
            var matches = PlaceholderNameOnlyRegex.Matches(searchText);
            if (matches.Count == 0) return null;
            int cursorRelativePos = caretOffset - startPos;
            int closestDistance = int.MaxValue;
            string closestPlaceholder = null;
            foreach (Match match in matches)
            {
                int distance = System.Math.Abs(match.Index - cursorRelativePos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlaceholder = match.Value;
                }
            }
            return closestPlaceholder;
        }

        public static string ReplacePlaceholderWithFieldName(string text, string placeholder, string fieldName)
        {
            string pattern = $"Name=\"{placeholder}\"";
            string replacement = $"Name=\"{fieldName}\"";
            int placeholderIndex = text.IndexOf(pattern);
            if (placeholderIndex >= 0)
            {
                return text.Remove(placeholderIndex, pattern.Length).Insert(placeholderIndex, replacement);
            }
            return text;
        }
    }
}

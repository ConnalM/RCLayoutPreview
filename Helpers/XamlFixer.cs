using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace RCLayoutPreview.Helpers
{
    public static class XamlFixer
    {
        public static int GetPlayerIndex(string fieldType)
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

        public static SolidColorBrush GetColor(int playerIndex)
        {
            // Use RC's standard 4-lane color scheme: Red, White, Blue, Yellow
            switch ((playerIndex - 1) % 4)
            {
                case 0: return new SolidColorBrush(Color.FromRgb(255, 0, 0));      // Red
                case 1: return new SolidColorBrush(Color.FromRgb(255, 255, 255));  // White  
                case 2: return new SolidColorBrush(Color.FromRgb(0, 0, 255));      // Blue
                case 3: return new SolidColorBrush(Color.FromRgb(255, 255, 0));    // Yellow
                default: return new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray (fallback)
            }
        }
    }
}
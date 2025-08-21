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
            // Use all 8 available colors in rotation
            switch ((playerIndex - 1) % 8)
            {
                case 0: return new SolidColorBrush(Color.FromRgb(255, 0, 0));       // Bright Red
                case 1: return new SolidColorBrush(Color.FromRgb(0, 120, 255));     // Bright Blue
                case 2: return new SolidColorBrush(Color.FromRgb(0, 255, 0));       // Bright Green
                case 3: return new SolidColorBrush(Color.FromRgb(153, 50, 204));    // Bright Purple (Orchid)
                case 4: return new SolidColorBrush(Color.FromRgb(255, 215, 0));     // Bright Gold (Goldenrod)
                case 5: return new SolidColorBrush(Color.FromRgb(0, 191, 255));     // Bright Light Blue (Deep Sky Blue)
                case 6: return new SolidColorBrush(Color.FromRgb(124, 252, 0));     // Bright Light Green (Lawn Green)
                case 7: return new SolidColorBrush(Color.FromRgb(255, 140, 0));     // Bright Orange (Dark Orange)
                default: return new SolidColorBrush(Color.FromRgb(128, 128, 128));  // Gray (fallback)
            }
        }
    }
}
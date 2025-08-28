using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using RCLayoutPreview.Helpers;

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
            int colorIdx = (playerIndex - 1) % PlayerColors.Colors.Length;
            if (colorIdx >= 0 && colorIdx < PlayerColors.Colors.Length)
                return new SolidColorBrush(PlayerColors.Colors[colorIdx]);
            else
                return new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray (fallback)
        }
    }
}
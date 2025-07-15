using System.Collections.Generic;

namespace RCLayoutPreview.Helpers
{
    public class LayoutSnippet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string XamlTemplate { get; set; }
        public List<string> RequiredFields { get; set; } = new List<string>();
        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, string>();
        public string DefaultStyles { get; set; }

        // Constants for XAML templates
        private static readonly string RacerRowTemplate =
            "<StackPanel Orientation=\"Horizontal\" Margin=\"5\">" +
            "<Label Name=\"Position_Position{0}\" Width=\"30\" Content=\"{0}\" {styles}/>" +
            "<Label Name=\"Nickname_Position{0}\" Width=\"150\" {styles}/>" +
            "<Label Name=\"Lap_Position{0}\" Width=\"50\" HorizontalContentAlignment=\"Right\" {styles}/>" +
            "</StackPanel>";

        private static readonly string TimerTemplate =
            "<StackPanel>" +
            "<TextBlock Text=\"Race Time\" FontSize=\"16\" HorizontalAlignment=\"Center\"/>" +
            "<Label Name=\"RaceTimer\" FontSize=\"40\" HorizontalAlignment=\"Center\" {styles}/>" +
            "</StackPanel>";

        private static readonly string AvatarTemplate =
            "<Border BorderBrush=\"White\" BorderThickness=\"1\" Width=\"100\" Height=\"100\">" +
            "<Image Name=\"Avatar_Position{0}\" Stretch=\"UniformToFill\"/>" +
            "</Border>";

        private static readonly string NextRaceTemplate =
            "<StackPanel Margin=\"10\">" +
            "<TextBlock Text=\"Next Race\" FontSize=\"20\" HorizontalAlignment=\"Center\" {styles}/>" +
            "<Label Name=\"NextHeatName\" FontSize=\"24\" HorizontalAlignment=\"Center\" {styles}/>" +
            "<ItemsControl Name=\"NextHeatRacers\">" +
            "<ItemsControl.ItemTemplate>" +
            "<DataTemplate>" +
            "<TextBlock Text=\"{Binding}\" FontSize=\"16\" {styles}/>" +
            "</DataTemplate>" +
            "</ItemsControl.ItemTemplate>" +
            "</ItemsControl>" +
            "</StackPanel>";

        private static readonly string LapRecordTemplate =
            "<StackPanel>" +
            "<TextBlock Text=\"Lap Record\" FontSize=\"16\" HorizontalAlignment=\"Center\"/>" +
            "<Label Name=\"LapRecord\" FontSize=\"24\" HorizontalAlignment=\"Center\" {styles}/>" +
            "<Label Name=\"LapRecordHolder\" FontSize=\"16\" HorizontalAlignment=\"Center\" {styles}/>" +
            "</StackPanel>";

        private static readonly string RacerStatsTemplate =
            "<StackPanel Margin=\"5\">" +
            "<Label Name=\"BestLap_Position{0}\" Content=\"Best: \" {styles}/>" +
            "<Label Name=\"AvgLap_Position{0}\" Content=\"Avg: \" {styles}/>" +
            "<Label Name=\"LastLap_Position{0}\" Content=\"Last: \" {styles}/>" +
            "</StackPanel>";

        // List of available snippets
        public static List<LayoutSnippet> GetDefaultSnippets()
        {
            return new List<LayoutSnippet>
            {
                new LayoutSnippet
                {
                    Name = "Racer Row",
                    Description = "A row showing racer name, position, and lap count",
                    Category = "Racers",
                    XamlTemplate = RacerRowTemplate,
                    RequiredFields = new List<string> { "Position_Position", "Nickname_Position", "Lap_Position" },
                    DefaultStyles = "FontSize=\"20\" Background=\"Black\" Foreground=\"White\""
                },

                new LayoutSnippet
                {
                    Name = "Timer Display",
                    Description = "Large timer display with labels",
                    Category = "Timing",
                    XamlTemplate = TimerTemplate,
                    RequiredFields = new List<string> { "RaceTimer" },
                    DefaultStyles = "Background=\"Black\" Foreground=\"White\""
                },

                new LayoutSnippet
                {
                    Name = "Racer Avatar",
                    Description = "Display racer's avatar image",
                    Category = "Racers",
                    XamlTemplate = AvatarTemplate,
                    RequiredFields = new List<string> { "Avatar_Position" },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" }
                    }
                },

                new LayoutSnippet
                {
                    Name = "Next Race Info",
                    Description = "Display information about the next race",
                    Category = "Race Info",
                    XamlTemplate = NextRaceTemplate,
                    RequiredFields = new List<string> { "NextHeatName", "NextHeatRacers" },
                    DefaultStyles = "Foreground=\"White\""
                },

                new LayoutSnippet
                {
                    Name = "Lap Record",
                    Description = "Display current lap record and holder",
                    Category = "Timing",
                    XamlTemplate = LapRecordTemplate,
                    RequiredFields = new List<string> { "LapRecord", "LapRecordHolder" },
                    DefaultStyles = "Background=\"Black\" Foreground=\"White\""
                },

                new LayoutSnippet
                {
                    Name = "Racer Stats",
                    Description = "Display lap time statistics for a racer",
                    Category = "Racers",
                    XamlTemplate = RacerStatsTemplate,
                    RequiredFields = new List<string> { "BestLap_Position", "AvgLap_Position", "LastLap_Position" },
                    DefaultStyles = "FontSize=\"16\" Foreground=\"White\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" }
                    }
                }
            };
        }
    }
}
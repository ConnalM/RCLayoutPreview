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
        private static readonly string BasicDocumentTemplate =
            "<Window\r\n" +
            "    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\r\n" +
            "    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\r\n" +
            "    xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"\r\n" +
            "    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"\r\n" +
            "    xmlns:local=\"clr-namespace:RaceCoordinator\"\r\n" +
            "    mc:Ignorable=\"d\"\r\n" +
            "    Title=\"Race Layout\"\r\n" +
            "    Height=\"720\" Width=\"1280\"\r\n" +
            "    Background=\"Black\">\r\n" +
                    "\r\n" +
            "    <!-- Optional styling resources -->\r\n" +
            "    <!-- <Window.Resources>\r\n" +
            "         <ResourceDictionary Source=\"ThemeDictionary.xaml\"/>\r\n" +
            "     </Window.Resources> -->\r\n" +
                    "\r\n" +
            "    <Viewbox Stretch=\"Uniform\">\r\n" +
            "        <Grid>\r\n" +
            "            <!-- Insert layout elements below -->\r\n" +
            "            <!-- Example: <TextBlock Name=\"LapTime_Position1_1\" FontSize=\"20\" Background=\"Black\" Foreground=\"White\" /> -->\r\n" +
            "            {content}\r\n" +
            "            <TextBlock Text=\"Race Layout Preview Loaded\"\r\n" +
            "                       FontSize=\"28\"\r\n" +
            "                       FontWeight=\"Bold\"\r\n" +
            "                       Foreground=\"Black\"\r\n" +
            "                       HorizontalAlignment=\"Center\"\r\n" +
            "                       VerticalAlignment=\"Center\" />\r\n" +
            "        </Grid>\r\n" +
            "    </Viewbox>\r\n" +
            "</Window>";

        // Fixed template to avoid duplicate names
        private static readonly string RacerRowTemplate =
            "<StackPanel Orientation=\"Horizontal\" Margin=\"5\">" +
            "<Label Name=\"PositionLabel_Position{0}\" Width=\"30\" Content=\"{0}\" {styles}/>" +
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

        private static readonly string ViewboxTemplate =
            "<Viewbox Stretch=\"Uniform\" Width=\"400\" Height=\"300\">\r\n" +
            "    {content}\r\n" +
            "</Viewbox>";

        private static readonly string LapTimeTemplate =
            "<TextBlock Name=\"LapTime_Position{0}_1\" " +
            "FontSize=\"20\" " +
            "Background=\"Black\" " +
            "Foreground=\"White\" " +
            "HorizontalAlignment=\"Center\" " +
            "Margin=\"5\" {styles}/>";

        // List of available snippets
        public static List<LayoutSnippet> GetDefaultSnippets()
        {
            return new List<LayoutSnippet>
            {
                new LayoutSnippet
                {
                    Name = "Basic RC Layout",
                    Description = "Standard Race Coordinator layout file structure",
                    Category = "Documents",
                    XamlTemplate = BasicDocumentTemplate,
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{content}", "Your layout content here" }
                    }
                },

                new LayoutSnippet
                {
                    Name = "Racer Row",
                    Description = "A row showing racer name, position, and lap count",
                    Category = "Racers",
                    XamlTemplate = RacerRowTemplate,
                    RequiredFields = new List<string> { "PositionLabel_Position", "Nickname_Position", "Lap_Position" },
                    DefaultStyles = "FontSize=\"20\" Background=\"Black\" Foreground=\"White\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" }
                    }
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
                },

                new LayoutSnippet
                {
                    Name = "Viewbox Container",
                    Description = "A scaling container that maintains content aspect ratio",
                    Category = "Containers",
                    XamlTemplate = ViewboxTemplate,
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{content}", "Place content here" }
                    }
                },

                new LayoutSnippet
                {
                    Name = "Lap Time Display",
                    Description = "Display a racer's lap time with Race Coordinator naming format",
                    Category = "Timing",
                    XamlTemplate = LapTimeTemplate,
                    RequiredFields = new List<string> { "LapTime_Position" },
                    DefaultStyles = "FontWeight=\"Bold\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" }
                    }
                }
            };
        }

        // Process a snippet, handling any placeholders
        public static string ProcessSnippet(LayoutSnippet snippet, int position = 1, string content = "")
        {
            string xaml = snippet.XamlTemplate;

            // Replace position placeholder
            xaml = xaml.Replace("{0}", position.ToString());

            // Replace content placeholder if present
            if (xaml.Contains("{content}"))
            {
                xaml = xaml.Replace("{content}", content ?? "");
            }

            // Apply default styles if specified
            if (!string.IsNullOrEmpty(snippet.DefaultStyles))
            {
                xaml = xaml.Replace("{styles}", snippet.DefaultStyles);
            }
            else
            {
                xaml = xaml.Replace("{styles}", "");
            }

            return xaml;
        }
    }
}
using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    public static class LayoutSnippetUtility
    {
        /// <summary>
        /// Save the layout snippet class content to a file, avoiding the file truncation issue
        /// </summary>
        /// <param name="filePath">The path to save the file</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool SaveLayoutSnippetClass()
        {
            try
            {
                // Create a complete string with the entire class content
                string completeContent = GetCompleteLayoutSnippetContent();
                
                // Save to the actual file location
                string filePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "Helpers", 
                    "LayoutSnippet.cs");
                
                // Make sure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                // Save with UTF-8 encoding without BOM
                File.WriteAllText(filePath, completeContent, new UTF8Encoding(false));
                
                Debug.WriteLine($"Successfully saved complete LayoutSnippet.cs file to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving LayoutSnippet.cs: {ex.Message}");
                return false;
            }
        }
        
        // This method contains the entire class as a string to avoid truncation issues
        private static string GetCompleteLayoutSnippetContent()
        {
            return @"using System.Collections.Generic;

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
            ""<Window\r\n"" +
            ""    xmlns=\""http://schemas.microsoft.com/winfx/2006/xaml/presentation\""\r\n"" +
            ""    xmlns:x=\""http://schemas.microsoft.com/winfx/2006/xaml\""\r\n"" +
            ""    xmlns:d=\""http://schemas.microsoft.com/expression/blend/2008\""\r\n"" +
            ""    xmlns:mc=\""http://schemas.openxmlformats.org/markup-compatibility/2006\""\r\n"" +
            ""    xmlns:local=\""clr-namespace:RaceCoordinator\""\r\n"" +
            ""    mc:Ignorable=\""d\""\r\n"" +
            ""    Title=\""Race Layout\""\r\n"" +
            ""    Height=\""720\"" Width=\""1280\""\r\n"" +
            ""    Background=\""Black\"">\r\n"" +
            ""\r\n"" +
            ""    <!-- Optional styling resources -->\r\n"" +
            ""    <!-- <Window.Resources>\r\n"" +
            ""         <ResourceDictionary Source=\""ThemeDictionary.xaml\""\/>\r\n"" +
            ""     <\/Window.Resources> -->\r\n"" +
            ""\r\n"" +
            ""    <Viewbox Stretch=\""Uniform\"">\r\n"" +
            ""        <Grid>\r\n"" +
            ""            <!-- Insert layout elements below -->\r\n"" +
            ""            <!-- Example: <TextBlock Name=\""LapTime_1_1\"" FontSize=\""20\"" Background=\""Black\"" Foreground=\""White\"" \/> -->\r\n"" +
            ""            <TextBlock Text=\""Race Layout Preview Loaded\""\r\n"" +
            ""                       FontSize=\""28\""\r\n"" +
            ""                       FontWeight=\""Bold\""\r\n"" +
            ""                       Foreground=\""Black\""\r\n"" +
            ""                       HorizontalAlignment=\""Center\""\r\n"" +
            ""                       VerticalAlignment=\""Center\"" \/>\r\n"" +
            ""        <\/Grid>\r\n"" +
            ""    <\/Viewbox>\r\n"" +
            ""<\/Window>"";

        // Using clear placeholder names for easy identification and replacement
        private static readonly string RacerRowTemplate =
            ""<StackPanel Orientation=\""Horizontal\"" Margin=\""5\"">\r\n"" +
            ""    <Label Name=\""Placeholder1\"" Width=\""30\"" Content=\""{0}\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder2\"" Width=\""150\"" Content=\""Racer {0}\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder3\"" Width=\""50\"" HorizontalContentAlignment=\""Right\"" Content=\""0\"" {styles}\/>\r\n"" +
            ""<\/StackPanel>"";

        private static readonly string TimerTemplate =
            ""<StackPanel>\r\n"" +
            ""    <TextBlock Text=\""Race Time\"" FontSize=\""16\"" HorizontalAlignment=\""Center\""\/>\r\n"" +
            ""    <Label Name=\""Placeholder1\"" Content=\""00:00.000\"" FontSize=\""40\"" HorizontalAlignment=\""Center\"" {styles}\/>\r\n"" +
            ""<\/StackPanel>"";

        private static readonly string AvatarTemplate =
            ""<Border BorderBrush=\""White\"" BorderThickness=\""1\"" Width=\""100\"" Height=\""100\"">\r\n"" +
            ""    <Image Name=\""Placeholder1\"" Stretch=\""UniformToFill\""\/>\r\n"" +
            ""<\/Border>"";

        private static readonly string NextRaceTemplate =
            ""<StackPanel Margin=\""10\"">\r\n"" +
            ""    <TextBlock Text=\""Next Race\"" FontSize=\""20\"" HorizontalAlignment=\""Center\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder1\"" Content=\""Placeholder Heat\"" FontSize=\""24\"" HorizontalAlignment=\""Center\"" {styles}\/>\r\n"" +
            ""    <ItemsControl Name=\""Placeholder2\"">\r\n"" +
            ""        <ItemsControl.ItemTemplate>\r\n"" +
            ""            <DataTemplate>\r\n"" +
            ""                <TextBlock Text=\""{Binding}\"" FontSize=\""16\"" {styles}\/>\r\n"" +
            ""            <\/DataTemplate>\r\n"" +
            ""        <\/ItemsControl.ItemTemplate>\r\n"" +
            ""    <\/ItemsControl>\r\n"" +
            ""<\/StackPanel>"";

        private static readonly string LapRecordTemplate =
            ""<StackPanel>\r\n"" +
            ""    <TextBlock Text=\""Lap Record\"" FontSize=\""16\"" HorizontalAlignment=\""Center\""\/>\r\n"" +
            ""    <Label Name=\""Placeholder1\"" Content=\""00:00.000\"" FontSize=\""24\"" HorizontalAlignment=\""Center\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder2\"" Content=\""Placeholder Name\"" FontSize=\""16\"" HorizontalAlignment=\""Center\"" {styles}\/>\r\n"" +
            ""<\/StackPanel>"";

        private static readonly string RacerStatsTemplate =
            ""<StackPanel Margin=\""5\"">\r\n"" +
            ""    <Label Name=\""Placeholder1\"" Content=\""Best: 00:00.000\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder2\"" Content=\""Avg: 00:00.000\"" {styles}\/>\r\n"" +
            ""    <Label Name=\""Placeholder3\"" Content=\""Last: 00:00.000\"" {styles}\/>\r\n"" +
            ""<\/StackPanel>"";

        private static readonly string ViewboxTemplate =
            ""<Viewbox Stretch=\""Uniform\"" Width=\""400\"" Height=\""300\"">\r\n"" +
            ""    {content}\r\n"" +
            ""<\/Viewbox>"";

        // Using clear placeholder instead of actual field names
        private static readonly string LapTimeTemplate =
            ""<TextBlock Name=\""Placeholder1\"" \r\n"" +
            ""           Text=\""00:00.000\"" \r\n"" +
            ""           FontSize=\""20\"" \r\n"" +
            ""           Background=\""Black\"" \r\n"" +
            ""           Foreground=\""White\"" \r\n"" +
            ""           HorizontalAlignment=\""Center\"" \r\n"" +
            ""           Margin=\""5\"" {styles}\/>"";

        // List of available snippets
        public static List<LayoutSnippet> GetDefaultSnippets()
        {
            return new List<LayoutSnippet>
            {
                new LayoutSnippet
                {
                    Name = ""Basic RC Layout"",
                    Description = ""Standard Race Coordinator layout file structure"",
                    Category = ""Documents"",
                    XamlTemplate = BasicDocumentTemplate,
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{content}"", ""Your layout content here"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Racer Row"",
                    Description = ""A row showing racer name, position, and lap count"",
                    Category = ""Racers"",
                    XamlTemplate = RacerRowTemplate,
                    RequiredFields = new List<string> { ""Position_{0}_1"", ""Nickname_{0}_1"", ""Lap_{0}_1"" },
                    DefaultStyles = ""FontSize=\""20\"" Background=\""Black\"" Foreground=\""White\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{0}"", ""Racer Position (1-8)"" },
                        { ""Placeholder1"", ""Position_{0}_1 (position)"" },
                        { ""Placeholder2"", ""Nickname_{0}_1 (name)"" },
                        { ""Placeholder3"", ""Lap_{0}_1 (lap count)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Timer Display"",
                    Description = ""Large timer display with labels"",
                    Category = ""Timing"",
                    XamlTemplate = TimerTemplate,
                    RequiredFields = new List<string> { ""RaceTimer_1"" },
                    DefaultStyles = ""Background=\""Black\"" Foreground=\""White\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""Placeholder1"", ""RaceTimer_1 (race time)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Racer Avatar"",
                    Description = ""Display racer's avatar image"",
                    Category = ""Racers"",
                    XamlTemplate = AvatarTemplate,
                    RequiredFields = new List<string> { ""Avatar_{0}_1"" },
                    DefaultStyles = """",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{0}"", ""Racer Position (1-8)"" },
                        { ""Placeholder1"", ""Avatar_{0}_1 (racer avatar)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Next Race Info"",
                    Description = ""Display information about the next race"",
                    Category = ""Race Info"",
                    XamlTemplate = NextRaceTemplate,
                    RequiredFields = new List<string> { ""NextHeatName_1"", ""NextHeatRacers"" },
                    DefaultStyles = ""Foreground=\""White\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""Placeholder1"", ""NextHeatName_1 (next heat name)"" },
                        { ""Placeholder2"", ""NextHeatRacers (list of racers)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Lap Record"",
                    Description = ""Display current lap record and holder"",
                    Category = ""Timing"",
                    XamlTemplate = LapRecordTemplate,
                    RequiredFields = new List<string> { ""LapRecord_1"", ""LapRecordHolder_1"" },
                    DefaultStyles = ""Background=\""Black\"" Foreground=\""White\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""Placeholder1"", ""LapRecord_1 (record time)"" },
                        { ""Placeholder2"", ""LapRecordHolder_1 (record holder)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Racer Stats"",
                    Description = ""Display lap time statistics for a racer"",
                    Category = ""Racers"",
                    XamlTemplate = RacerStatsTemplate,
                    RequiredFields = new List<string> { ""BestLap_{0}_1"", ""AvgLap_{0}_1"", ""LastLap_{0}_1"" },
                    DefaultStyles = ""FontSize=\""16\"" Foreground=\""White\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{0}"", ""Racer Position (1-8)"" },
                        { ""Placeholder1"", ""BestLap_{0}_1 (best lap time)"" },
                        { ""Placeholder2"", ""AvgLap_{0}_1 (average lap time)"" },
                        { ""Placeholder3"", ""LastLap_{0}_1 (last lap time)"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Viewbox Container"",
                    Description = ""A scaling container that maintains content aspect ratio"",
                    Category = ""Containers"",
                    XamlTemplate = ViewboxTemplate,
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{content}"", ""Place content here"" }
                    }
                },

                new LayoutSnippet
                {
                    Name = ""Lap Time Display"",
                    Description = ""Display a racer's lap time with Race Coordinator naming format"",
                    Category = ""Timing"",
                    XamlTemplate = LapTimeTemplate,
                    RequiredFields = new List<string> { ""LapTime_{0}_1"" },
                    DefaultStyles = ""FontWeight=\""Bold\"""",
                    Placeholders = new Dictionary<string, string>
                    {
                        { ""{0}"", ""Racer Position (1-8)"" },
                        { ""Placeholder1"", ""LapTime_{0}_1 (lap time)"" }
                    }
                }
            };
        }

        // Process a snippet, handling any placeholders
        public static string ProcessSnippet(LayoutSnippet snippet, int position = 1, string content = """")
        {
            string xaml = snippet.XamlTemplate;

            // Replace position placeholder
            xaml = xaml.Replace(""{0}"", position.ToString());

            // Replace content placeholder if present
            if (xaml.Contains(""{content}""))
            {
                xaml = xaml.Replace(""{content}"", content ?? """");
            }

            // Apply default styles if specified
            if (!string.IsNullOrEmpty(snippet.DefaultStyles))
            {
                xaml = xaml.Replace(""{styles}"", snippet.DefaultStyles);
            }
            else
            {
                xaml = xaml.Replace(""{styles}"", """");
            }

            return xaml;
        }
    }
}";
        }
    }
}
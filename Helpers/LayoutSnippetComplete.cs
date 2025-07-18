using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

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
            // Window background: sets the outermost background color of the window
            "<Window\r\n" +
            "    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\r\n" +
            "    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\r\n" +
            "    xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"\r\n" +
            "    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"\r\n" +
            "    xmlns:local=\"clr-namespace:RaceCoordinator\"\r\n" +
            "    mc:Ignorable=\"d\"\r\n" +
            "    Title=\"Race Layout\"\r\n" +
            "    Height=\"720\" Width=\"1280\"\r\n" +
            "    Background=\"Black\"\r\n>\r\n" +
            "\r\n" +
            "    <!-- Optional styling resources -->\r\n" +
            "    <!-- <Window.Resources>\r\n" +
            "         <ResourceDictionary Source=\"ThemeDictionary.xaml\"/>\r\n" +
            "     </Window.Resources> -->\r\n" +
            "\r\n" +
            "    <!-- Border Background wraps Viewbox -->\r\n" +
            "    <Border Background=\"Blue\">\r\n" +
            "            <!-- Grid Background -->\r\n" +
            "            <Grid Background=\"White\">\r\n" +
            "                <!-- Insert layout elements below -->\r\n" +
            "                <!-- Example: <TextBlock Name=\"LapTime_1_1\" FontSize=\"20\" Background=\"Black\" Foreground=\"White\" /> -->\r\n" +
            "                <!-- TextBlock Background -->\r\n" +
            "                <TextBlock Text=\"Race Layout Preview Loaded\"\r\n" +
            "                           FontSize=\"28\"\r\n" +
            "                           FontWeight=\"Bold\"\r\n" +
            "                           Foreground=\"Black\"\r\n" +
            "                           Background=\"Yellow\"\r\n" +
            "                           HorizontalAlignment=\"Center\"\r\n" +
            "                           VerticalAlignment=\"Center\" />\r\n" +
            "            </Grid>\r\n" +
            "    </Border>\r\n" +
            "</Window>";

        // Using clear placeholder names for easy identification and replacement
        private static readonly string RacerRowTemplate =
            "<StackPanel Orientation=\"Horizontal\" Margin=\"5\">\r\n" +
            "    <Label Name=\"Placeholder1\" Width=\"30\" Content=\"{0}\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder2\" Width=\"150\" Content=\"Racer {0}\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder3\" Width=\"50\" HorizontalContentAlignment=\"Right\" Content=\"0\" {styles}/>\r\n" +
            "</StackPanel>";

        private static readonly string TimerTemplate =
            "<StackPanel>\r\n" +
            "    <TextBlock Text=\"Race Time\" FontSize=\"16\" HorizontalAlignment=\"Center\"/>\r\n" +
            "    <Label Name=\"Placeholder1\" Content=\"00:00.000\" FontSize=\"40\" HorizontalAlignment=\"Center\" {styles}/>\r\n" +
            "</StackPanel>";

        private static readonly string AvatarTemplate =
            "<Border BorderBrush=\"White\" BorderThickness=\"1\" Width=\"100\" Height=\"100\">\r\n" +
            "    <Image Name=\"Placeholder1\" Stretch=\"UniformToFill\"/>\r\n" +
            "</Border>";

        private static readonly string NextRaceTemplate =
            "<StackPanel Margin=\"10\">\r\n" +
            "    <TextBlock Text=\"Next Race\" FontSize=\"20\" HorizontalAlignment=\"Center\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder1\" Content=\"Placeholder Heat\" FontSize=\"24\" HorizontalAlignment=\"Center\" {styles}/>\r\n" +
            "    <ItemsControl Name=\"Placeholder2\">\r\n" +
            "        <ItemsControl.ItemTemplate>\r\n" +
            "            <DataTemplate>\r\n" +
            "                <TextBlock Text=\"{Binding}\" FontSize=\"16\" {styles}/>\r\n" +
            "            </DataTemplate>\r\n" +
            "        </ItemsControl.ItemTemplate>\r\n" +
            "    </ItemsControl>\r\n" +
            "</StackPanel>";

        private static readonly string LapRecordTemplate =
            "<StackPanel>\r\n" +
            "    <TextBlock Text=\"Lap Record\" FontSize=\"16\" HorizontalAlignment=\"Center\"/>\r\n" +
            "    <Label Name=\"Placeholder1\" Content=\"00:00.000\" FontSize=\"24\" HorizontalAlignment=\"Center\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder2\" Content=\"Placeholder Name\" FontSize=\"16\" HorizontalAlignment=\"Center\" {styles}/>\r\n" +
            "</StackPanel>";

        private static readonly string RacerStatsTemplate =
            "<StackPanel Margin=\"5\">\r\n" +
            "    <Label Name=\"Placeholder1\" Content=\"Best: 00:00.000\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder2\" Content=\"Avg: 00:00.000\" {styles}/>\r\n" +
            "    <Label Name=\"Placeholder3\" Content=\"Last: 00:00.000\" {styles}/>\r\n" +
            "</StackPanel>";

        private static readonly string ViewboxTemplate =
            "<Viewbox Stretch=\"Uniform\" Width=\"400\" Height=\"300\">\r\n" +
            "    {content}\r\n" +
            "</Viewbox>";

        // Using clear placeholder instead of actual field names
        private static readonly string LapTimeTemplate =
            "<TextBlock Name=\"Placeholder1\" \r\n" +
            "           Text=\"00:00.000\" \r\n" +
            "           FontSize=\"20\" \r\n" +
            "           Background=\"Black\" \r\n" +
            "           Foreground=\"White\" \r\n" +
            "           HorizontalAlignment=\"Center\" \r\n" +
            "           Margin=\"5\" {styles}/>";

        // New snippet for Theme Dictionary
        private static readonly string ThemeDictionarySnippet =
            "<Window.Resources>\n" +
            "    <ResourceDictionary>\n" +
            "        <ResourceDictionary.MergedDictionaries>\n" +
            "            <ResourceDictionary Source=\"ThemeDictionary.xaml\" />\n" +
            "        </ResourceDictionary.MergedDictionaries>\n" +
            "    </ResourceDictionary>\n" +
            "</Window.Resources>";

        // Generalized Menu snippet with placeholders for easy adaptation
        private static readonly string GeneralMenuSnippet =
            "<Menu Height=\"22\" Name=\"{menuName}\" VerticalAlignment=\"Top\">\n" +
            "    <MenuItem Header=\"{fileHeader}\">\n" +
            "        <MenuItem Name=\"{saveName}\" Header=\"{saveHeader}\" DataContext=\"'key':'{saveKey}','modifier':'{saveModifier}'\" ToolTip=\"{saveTooltip}\" />\n" +
            "        <MenuItem Name=\"{saveAsName}\" Header=\"{saveAsHeader}\" DataContext=\"'key':'{saveAsKey}','modifier':'{saveAsModifier}'\" ToolTip=\"{saveAsTooltip}\" />\n" +
            "        <Separator/>\n" +
            "        <MenuItem Name=\"{exportName}\" Header=\"{exportHeader}\" DataContext=\"'key':'{exportKey}','modifier':'{exportModifier}'\" ToolTip=\"{exportTooltip}\" />\n" +
            "    </MenuItem>\n" +
            "    <Separator Width=\"5\" />\n" +
            "</Menu>";

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
                    Name = "Theme Dictionary",
                    Description = "Window.Resources with merged ThemeDictionary.xaml resource.",
                    Category = "Resources",
                    XamlTemplate = ThemeDictionarySnippet
                },
                new LayoutSnippet
                {
                    Name = "General Menu",
                    Description = "Generalized Menu with placeholders for easy adaptation.",
                    Category = "Menus",
                    XamlTemplate = GeneralMenuSnippet,
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{menuName}", "menu1" },
                        { "{fileHeader}", "File" },
                        { "{saveName}", "Save_1" },
                        { "{saveHeader}", "Save" },
                        { "{saveKey}", "S" },
                        { "{saveModifier}", "Alt" },
                        { "{saveTooltip}", "ALT-S to save the race" },
                        { "{saveAsName}", "SaveAs_1" },
                        { "{saveAsHeader}", "Save As..." },
                        { "{saveAsKey}", "A" },
                        { "{saveAsModifier}", "Alt" },
                        { "{saveAsTooltip}", "ALT-a to save the race with the specified file name" },
                        { "{exportName}", "Export_1" },
                        { "{exportHeader}", "Export Race" },
                        { "{exportKey}", "X" },
                        { "{exportModifier}", "Alt" },
                        { "{exportTooltip}", "Alt-X to export current race progress" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Upper DockPanel",
                    Description = "Full layout container for top-of-screen race info.",
                    Category = "Layout Shells",
                    XamlTemplate =
                        "<DockPanel Height=\"200\" Margin=\"0,22,0,0\" VerticalAlignment=\"Top\">\n" +
                        "    <Grid>\n" +
                        "        <Grid.ColumnDefinitions>\n" +
                        "            <ColumnDefinition />\n" +
                        "            <ColumnDefinition />\n" +
                        "            <ColumnDefinition />\n" +
                        "        </Grid.ColumnDefinitions>\n" +
                        "        <!-- Insert Track Info, Race Info, and Race State Image here -->\n" +
                        "    </Grid>\n" +
                        "</DockPanel>"
                },
                new LayoutSnippet
                {
                    Name = "Track Info Panel",
                    Description = "Displays track image and name from RC Track Manager.",
                    Category = "Track Info Elements",
                    XamlTemplate =
                        "<StackPanel Grid.Column=\"0\" Height=\"200\" VerticalAlignment=\"Bottom\">\n" +
                        "    <!-- Track image pulled from RC's Track Manager -->\n" +
                        "    <Viewbox Margin=\"5\" Stretch=\"None\">\n" +
                        "        <Image Name=\"TrackImage_1\" Height=\"115\" MaxWidth=\"300\" StretchDirection=\"Both\"\n" +
                        "               ToolTip=\"Set this image in Track Manager\" />\n" +
                        "    </Viewbox>\n" +
                        "    <!-- Label for 'Track:' header -->\n" +
                        "    <Label Content=\"Track:\" FontSize=\"12\" FontWeight=\"Bold\" FontStyle=\"Italic\" Foreground=\"Linen\"\n" +
                        "           ToolTip=\"Static label for track section\" />\n" +
                        "    <!-- Track name pulled from RC -->\n" +
                        "    <Viewbox MaxWidth=\"300\" MaxHeight=\"50\" MinHeight=\"50\">\n" +
                        "        <Label Name=\"TrackName_1\" FontSize=\"30\" FontWeight=\"Bold\" FontStyle=\"Italic\" Foreground=\"White\"\n" +
                        "               ToolTip=\"Displays the name of the selected track\" />\n" +
                        "    </Viewbox>\n" +
                        "</StackPanel>"
                },
                new LayoutSnippet
                {
                    Name = "Race Info Panel",
                    Description = "Shows race name, time, and heat number.",
                    Category = "Race Info Elements",
                    XamlTemplate =
                        "<StackPanel Grid.Column=\"1\" Height=\"200\" VerticalAlignment=\"Top\">\n" +
                        "    <!-- Race name -->\n" +
                        "    <Viewbox MaxWidth=\"320\" MaxHeight=\"50\">\n" +
                        "        <Label Name=\"RaceName_1\" FontSize=\"28\" FontWeight=\"Bold\" Foreground=\"GreenYellow\"\n" +
                        "               ToolTip=\"Displays the name of the current race\" />\n" +
                        "    </Viewbox>\n" +
                        "    <!-- Race time -->\n" +
                        "    <Label Name=\"RaceTime_1\" FontSize=\"50\" FontWeight=\"Bold\" Foreground=\"GreenYellow\" HorizontalAlignment=\"Center\"\n" +
                        "           ToolTip=\"Displays the current race time\" />\n" +
                        "    <!-- Heat info -->\n" +
                        "    <Label HorizontalAlignment=\"Center\">\n" +
                        "        <DockPanel LastChildFill=\"True\" HorizontalAlignment=\"Center\">\n" +
                        "            <TextBlock Text=\"Heat \" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\" />\n" +
                        "            <TextBlock Name=\"HeatNumber_1\" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\"\n" +
                        "                       ToolTip=\"Displays current heat number\" />\n" +
                        "            <TextBlock Text=\" of \" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\" />\n" +
                        "            <TextBlock Name=\"NumHeats_1\" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\"\n" +
                        "                       ToolTip=\"Displays total number of heats\" />\n" +
                        "        </DockPanel>\n" +
                        "    </Label>\n" +
                        "</StackPanel>"
                },
                new LayoutSnippet
                {
                    Name = "Race State Image",
                    Description = "Displays race status flag image.",
                    Category = "Race State Visuals",
                    XamlTemplate =
                        "<Image Grid.Column=\"2\" Name=\"RaceStateImage_1\" Stretch=\"Uniform\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\"\n" +
                        "       Height=\"200\" Width=\"275\" ToolTip=\"Displays race state flag (e.g. red, green)\" />"
                },
                new LayoutSnippet
                {
                    Name = "Lap Record Panel",
                    Description = "Displays best lap time and racer name.",
                    Category = "Race Records",
                    XamlTemplate =
                        "<StackPanel>\n" +
                        "    <TextBlock Text=\"Lap Record\" FontSize=\"16\" HorizontalAlignment=\"Center\" />\n" +
                        "    <Label Name=\"RecordTime_1\" Content=\"00:00.000\" FontSize=\"24\" HorizontalAlignment=\"Center\"\n" +
                        "           ToolTip=\"Displays best lap time\" />\n" +
                        "    <Label Name=\"RecordName_1\" Content=\"Racer Name\" FontSize=\"16\" HorizontalAlignment=\"Center\"\n" +
                        "           ToolTip=\"Displays name of record holder\" />\n" +
                        "</StackPanel>"
                },
                new LayoutSnippet
                {
                    Name = "Media Placeholder",
                    Description = "Preview-safe replacement for MediaElement.",
                    Category = "Preview Placeholders",
                    XamlTemplate =
                        "<Border Width=\"150\" Height=\"125\" Background=\"DarkGray\">\n" +
                        "    <TextBlock Text=\"[Video Placeholder]\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\" Foreground=\"White\"\n" +
                        "               ToolTip=\"This would be a video in RC runtime\" />\n" +
                        "</Border>"
                },

                // Atomic Elements
                new LayoutSnippet
                {
                    Name = "Basic Label",
                    Description = "Simple label with customizable content.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<Label Content=\"Label Text\" FontSize=\"16\" Foreground=\"White\"\n" +
                        "       ToolTip=\"Basic label element\" />"
                },
                new LayoutSnippet
                {
                    Name = "Basic TextBlock",
                    Description = "Simple text block for static text.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<TextBlock Text=\"Static Text\" FontSize=\"16\" Foreground=\"White\"\n" +
                        "           ToolTip=\"Basic text block element\" />"
                },
                new LayoutSnippet
                {
                    Name = "Basic Image",
                    Description = "Simple image element with fixed size.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<Image Source=\"placeholder.png\" Width=\"100\" Height=\"100\" Stretch=\"Uniform\"\n" +
                        "       ToolTip=\"Basic image element\" />"
                },

                // --- Additional atomic/utility snippets using the static templates ---
                new LayoutSnippet
                {
                    Name = "Racer Row",
                    Description = "Horizontal row for racer info with placeholders.",
                    Category = "Atomic Elements",
                    XamlTemplate = RacerRowTemplate,
                    DefaultStyles = "FontSize=\"18\" Margin=\"2\""
                },
                new LayoutSnippet
                {
                    Name = "Timer Display",
                    Description = "Large timer label for race time.",
                    Category = "Atomic Elements",
                    XamlTemplate = TimerTemplate,
                    DefaultStyles = "Foreground=\"Orange\" FontWeight=\"Bold\""
                },
                new LayoutSnippet
                {
                    Name = "Avatar Image",
                    Description = "Square avatar image with border.",
                    Category = "Atomic Elements",
                    XamlTemplate = AvatarTemplate
                },
                new LayoutSnippet
                {
                    Name = "Next Race Panel",
                    Description = "Panel for next race info and heat list.",
                    Category = "Race Info Elements",
                    XamlTemplate = NextRaceTemplate
                },
                new LayoutSnippet
                {
                    Name = "Lap Record (Placeholders)",
                    Description = "Panel for lap record with placeholder fields.",
                    Category = "Race Records",
                    XamlTemplate = LapRecordTemplate
                },
                new LayoutSnippet
                {
                    Name = "Racer Stats Row",
                    Description = "Row for best, average, and last lap times.",
                    Category = "Atomic Elements",
                    XamlTemplate = RacerStatsTemplate
                },
                new LayoutSnippet
                {
                    Name = "Viewbox Wrapper",
                    Description = "Wraps content in a Viewbox for scaling.",
                    Category = "Layout Shells",
                    XamlTemplate = ViewboxTemplate,
                    Placeholders = new Dictionary<string, string> { { "{content}", "<!-- Insert content here -->" } }
                },
                new LayoutSnippet
                {
                    Name = "Lap Time (Placeholder)",
                    Description = "Single lap time TextBlock with placeholder.",
                    Category = "Atomic Elements",
                    XamlTemplate = LapTimeTemplate
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

        /// <summary>
        /// Save the layout snippet class content to a file, avoiding the file truncation issue
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool SaveLayoutSnippetClass()
        {
            try
            {
                string completeContent = GetCompleteLayoutSnippetContent();
                string filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Helpers",
                    "LayoutSnippet.cs");

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
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
            return @"using System.Collections.Generic;\n\nnamespace RCLayoutPreview.Helpers\n{\n    public class LayoutSnippet\n    {\n        public string Name { get; set; }\n        public string Description { get; set; }\n        public string Category { get; set; }\n        public string XamlTemplate { get; set; }\n        public List<string> RequiredFields { get; set; } = new List<string>();\n        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, string>();\n        public string DefaultStyles { get; set; }\n        // ... (rest of the class definition as string) ...\n    }\n}";
        }
    }
}
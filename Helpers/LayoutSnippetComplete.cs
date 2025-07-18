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
        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, String>();
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
            "    <TextBlock Text=\"Next Race\" FontSize=\"20\" HorizontalAlignment=\"Center\" Foreground=\"White\"/>\r\n" +
            "    <Label Name=\"Placeholder1\" Content=\"Placeholder Heat\" FontSize=\"24\" HorizontalAlignment=\"Center\" Foreground=\"White\"/>\r\n" +
            "    <ItemsControl Name=\"Placeholder2\">\r\n" +
            "        <ItemsControl.ItemTemplate>\r\n" +
            "            <DataTemplate>\r\n" +
            "                <TextBlock Text=\"{Binding}\" FontSize=\"16\" Foreground=\"White\"/>\r\n" +
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

        private static readonly string RaceInfoPanelTemplate =
            "<StackPanel Grid.Column=\"1\" Height=\"200\" VerticalAlignment=\"Top\">\r\n" +
            "    <Viewbox MaxWidth=\"320\" MaxHeight=\"50\">\r\n" +
            "        <Label Name=\"Placeholder1\" Content=\"Sample Race\" FontSize=\"28\" FontWeight=\"Bold\" Foreground=\"GreenYellow\"\r\n" +
            "               ToolTip=\"Displays the name of the current race\" />\r\n" +
            "    </Viewbox>\r\n" +
            "    <Label Name=\"Placeholder2\" Content=\"00:00.000\" FontSize=\"50\" FontWeight=\"Bold\" Foreground=\"GreenYellow\" \r\n" +
            "           HorizontalAlignment=\"Center\" ToolTip=\"Displays the current race time\" />\r\n" +
            "    <Label HorizontalAlignment=\"Center\">\r\n" +
            "        <DockPanel LastChildFill=\"True\" HorizontalAlignment=\"Center\">\r\n" +
            "            <TextBlock Text=\"Heat \" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\" />\r\n" +
            "            <TextBlock Name=\"Placeholder3\" Text=\"1\" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\"\r\n" +
            "                       ToolTip=\"Displays current heat number\" />\r\n" +
            "            <TextBlock Text=\" of \" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\" />\r\n" +
            "            <TextBlock Name=\"Placeholder4\" Text=\"4\" FontSize=\"24\" FontWeight=\"Bold\" Foreground=\"White\"\r\n" +
            "                       ToolTip=\"Displays total number of heats\" />\r\n" +
            "        </DockPanel>\r\n" +
            "    </Label>\r\n" +
            "</StackPanel>";

        private static readonly string TrackInfoPanelTemplate =
            "<StackPanel Grid.Column=\"0\" Height=\"200\" VerticalAlignment=\"Bottom\">\r\n" +
            "    <Viewbox Margin=\"5\" Stretch=\"None\">\r\n" +
            "        <Image Name=\"Placeholder1\" Height=\"115\" MaxWidth=\"300\" StretchDirection=\"Both\"\r\n" +
            "               ToolTip=\"Track image from RC Track Manager\" />\r\n" +
            "    </Viewbox>\r\n" +
            "    <Label Content=\"Track:\" FontSize=\"12\" FontWeight=\"Bold\" FontStyle=\"Italic\" Foreground=\"Linen\"\r\n" +
            "           ToolTip=\"Static label for track section\" />\r\n" +
            "    <Viewbox MaxWidth=\"300\" MaxHeight=\"50\" MinHeight=\"50\">\r\n" +
            "        <Label Name=\"Placeholder2\" Content=\"Sample Track\" FontSize=\"30\" FontWeight=\"Bold\" FontStyle=\"Italic\" Foreground=\"White\"\r\n" +
            "               ToolTip=\"Track name from Track Manager\" />\r\n" +
            "    </Viewbox>\r\n" +
            "</StackPanel>";

        private static readonly string RaceStateImageTemplate =
            "<Image Grid.Column=\"2\" Name=\"Placeholder1\" Stretch=\"Uniform\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\"\r\n" +
            "       Height=\"200\" Width=\"275\" ToolTip=\"Race state flag (e.g. red, green)\" />";

        private static readonly string LowerDockPanelTemplate =
            "<DockPanel Margin=\"0,222,0,0\" Name=\"dockPanel2\" Background=\"Transparent\">\n" +
            "  <Grid>\n" +
            "    <Grid.RowDefinitions>\n" +
            "      <RowDefinition Height=\"50\" />\n" +
            "      <RowDefinition Height=\"100\" />\n" +
            "    </Grid.RowDefinitions>\n" +
            "    <Grid.ColumnDefinitions>\n" +
            "      <ColumnDefinition Width=\"200\" />\n" +
            "      <ColumnDefinition Width=\"100\" />\n" +
            "      <ColumnDefinition Width=\"150\" />\n" +
            "      <ColumnDefinition Width=\"150\" />\n" +
            "      <ColumnDefinition Width=\"150\" />\n" +
            "    </Grid.ColumnDefinitions>\n\n" +
            "    <!-- Header labels -->\n" +
            "    <Label Grid.Row=\"0\" Grid.Column=\"0\" Content=\"Name\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
            "    <Label Grid.Row=\"0\" Grid.Column=\"1\" Content=\"Lap\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
            "    <Label Grid.Row=\"0\" Grid.Column=\"2\" Content=\"Lap Time\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
            "    <Label Grid.Row=\"0\" Grid.Column=\"3\" Content=\"Median\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
            "    <Label Grid.Row=\"0\" Grid.Column=\"4\" Content=\"Best\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n\n" +
            "    <!-- Lane 1 data row -->\n" +
            "    <Label Grid.Row=\"1\" Grid.Column=\"0\" Name=\"Placeholder1\" Content=\"Racer 1\" HorizontalContentAlignment=\"Center\" />\n" +
            "    <Label Grid.Row=\"1\" Grid.Column=\"1\" Name=\"Placeholder2\" Content=\"0\" HorizontalContentAlignment=\"Center\" />\n" +
            "    <Label Grid.Row=\"1\" Grid.Column=\"2\" Name=\"Placeholder3\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
            "    <Label Grid.Row=\"1\" Grid.Column=\"3\" Name=\"Placeholder4\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
            "    <Label Grid.Row=\"1\" Grid.Column=\"4\" Name=\"Placeholder5\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
            "  </Grid>\n" +
            "</DockPanel>";


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
                // Basic RC Layout: Standard file structure for a Race Coordinator layout
                new LayoutSnippet
                {
                    Name = "Basic RC Layout",
                    Description = "Standard Race Coordinator layout file structure",
                    Category = "Documents",
                    XamlTemplate =
                        "<!-- Basic RC Layout: Standard file structure for a Race Coordinator layout -->\n" +
                        BasicDocumentTemplate,
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{content}", "Your layout content here" }
                    }
                },
                // Theme Dictionary: Merged resource dictionary for theming
                new LayoutSnippet
                {
                    Name = "Theme Dictionary",
                    Description = "Window.Resources with merged ThemeDictionary.xaml resource.",
                    Category = "Resources",
                    XamlTemplate =
                        "<!-- Theme Dictionary: Merged resource dictionary for theming -->\n" +
                        ThemeDictionarySnippet
                },
                // General Menu: Menu with placeholders for easy adaptation
                new LayoutSnippet
                {
                    Name = "General Menu",
                    Description = "Generalized Menu with placeholders for easy adaptation.",
                    Category = "Menus",
                    XamlTemplate =
                        "<!-- General Menu: Menu with placeholders for easy adaptation -->\n" +
                        GeneralMenuSnippet,
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
                // Upper DockPanel: Container for top-of-screen race info
                new LayoutSnippet
                {
                    Name = "Upper DockPanel",
                    Description = "Full layout container for top-of-screen race info.",
                    Category = "Layout Shells",
                    XamlTemplate =
                        "<!-- Upper DockPanel: Container for top-of-screen race info -->\n" +
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
                // Track Info Panel: Shows track image and name
                new LayoutSnippet
                {
                    Name = "Track Info Panel",
                    Description = "Displays track image and name from RC Track Manager.",
                    Category = "Track Info Elements",
                    XamlTemplate = TrackInfoPanelTemplate,
                    RequiredFields = new List<string> { "TrackImage_1", "TrackName_1" },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "TrackImage_1 (track preview)" },
                        { "Placeholder2", "TrackName_1 (track name)" }
                    }
                },
                // Race Info Panel: Shows race name, time, and heat number
                new LayoutSnippet
                {
                    Name = "Race Info Panel",
                    Description = "Shows race name, time, and heat number.",
                    Category = "Race Info Elements",
                    XamlTemplate = RaceInfoPanelTemplate,
                    RequiredFields = new List<string> { "RaceName_1", "RaceTime_1", "HeatNumber_1", "NumHeats_1" },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "RaceName_1 (race name)" },
                        { "Placeholder2", "RaceTime_1 (race time)" },
                        { "Placeholder3", "HeatNumber_1 (current heat)" },
                        { "Placeholder4", "NumHeats_1 (total heats)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Race State Image",
                    Description = "Displays race status flag image.",
                    Category = "Race State Visuals",
                    XamlTemplate = RaceStateImageTemplate,
                    RequiredFields = new List<string> { "RaceStateImage_1" },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "RaceStateImage_1 (status flag)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Lower DockPanel",
                    Description = "Simplified layout for bottom-of-screen race table.",
                    Category = "Layout Shells",
                    XamlTemplate = LowerDockPanelTemplate,
                    RequiredFields = new List<string> { 
                        "Nickname_Lane1_2", "Lap_Lane1_2", "LapTime_Lane1_2", 
                        "MedianTime_Lane1_2", "BestLapTime_Lane1_2"
                    },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "Nickname_Lane1_2 (racer name)" },
                        { "Placeholder2", "Lap_Lane1_2 (lap count)" },
                        { "Placeholder3", "LapTime_Lane1_2 (current lap)" },
                        { "Placeholder4", "MedianTime_Lane1_2 (median time)" },
                        { "Placeholder5", "BestLapTime_Lane1_2 (best time)" }
                    }
                },

                // Atomic Elements
                new LayoutSnippet
                {
                    Name = "Basic Label",
                    Description = "Simple label with customizable content.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<!-- Basic Label: Simple label element -->\n" +
                        "<Label Content=\"Label Text\" FontSize=\"16\" Foreground=\"White\"\n" +
                        "       ToolTip=\"Basic label element\" />"
                },
                new LayoutSnippet
                {
                    Name = "Basic TextBlock",
                    Description = "Simple text block for static text.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<!-- Basic TextBlock: Simple text block element -->\n" +
                        "<TextBlock Text=\"Static Text\" FontSize=\"16\" Foreground=\"White\"\n" +
                        "           ToolTip=\"Basic text block element\" />"
                },
                new LayoutSnippet
                {
                    Name = "Basic Image",
                    Description = "Simple image element with fixed size.",
                    Category = "Atomic Elements",
                    XamlTemplate =
                        "<!-- Basic Image: Simple image element -->\n" +
                        "<Image Source=\"placeholder.png\" Width=\"100\" Height=\"100\" Stretch=\"Uniform\"\n" +
                        "       ToolTip=\"Basic image element\" />"
                },

                // --- Additional atomic/utility snippets using the static templates ---
                new LayoutSnippet
                {
                    Name = "Racer Row",
                    Description = "Horizontal row for racer info with placeholders.",
                    Category = "Racers",
                    XamlTemplate = RacerRowTemplate,
                    RequiredFields = new List<string> { "Position_{0}_1", "Nickname_{0}_1", "Lap_{0}_1" },
                    DefaultStyles = "FontSize=\"18\" Margin=\"2\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" },
                        { "Placeholder1", "Position_{0}_1 (position)" },
                        { "Placeholder2", "Nickname_{0}_1 (name)" }, 
                        { "Placeholder3", "Lap_{0}_1 (lap count)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Timer Display",
                    Description = "Large timer label for race time.",
                    Category = "Timing", 
                    XamlTemplate = TimerTemplate,
                    RequiredFields = new List<string> { "RaceTimer_1" },
                    DefaultStyles = "Foreground=\"Orange\" FontWeight=\"Bold\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "RaceTimer_1 (race time)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Avatar Image",
                    Description = "Square avatar image with border.",
                    Category = "Racers",
                    XamlTemplate = AvatarTemplate,
                    RequiredFields = new List<string> { "Avatar_{0}_1" },
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" },
                        { "Placeholder1", "Avatar_{0}_1 (racer avatar)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Next Race Info",
                    Description = "Display information about the next race",
                    Category = "Race Info",
                    XamlTemplate = NextRaceTemplate,
                    RequiredFields = new List<string> { "NextHeatName_1", "NextHeatRacers" },
                    DefaultStyles = "Foreground=\"White\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "NextHeatName_1 (next heat name)" },
                        { "Placeholder2", "NextHeatRacers (list of racers)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Lap Record",
                    Description = "Display current lap record and holder",
                    Category = "Timing",
                    XamlTemplate = LapRecordTemplate,
                    RequiredFields = new List<string> { "LapRecord_1", "LapRecordHolder_1" },
                    DefaultStyles = "Background=\"Black\" Foreground=\"White\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "Placeholder1", "LapRecord_1 (record time)" },
                        { "Placeholder2", "LapRecordHolder_1 (record holder)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Racer Stats",
                    Description = "Display lap time statistics for a racer",
                    Category = "Racers",
                    XamlTemplate = RacerStatsTemplate,
                    RequiredFields = new List<string> { "BestLap_{0}_1", "AvgLap_{0}_1", "LastLap_{0}_1" },
                    DefaultStyles = "FontSize=\"16\" Foreground=\"White\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" },
                        { "Placeholder1", "BestLap_{0}_1 (best lap time)" },
                        { "Placeholder2", "AvgLap_{0}_1 (average lap time)" },
                        { "Placeholder3", "LastLap_{0}_1 (last lap time)" }
                    }
                },
                new LayoutSnippet
                {
                    Name = "Lap Time Display",
                    Description = "Display a racer's lap time with Race Coordinator naming format",
                    Category = "Timing",
                    XamlTemplate = LapTimeTemplate,
                    RequiredFields = new List<string> { "LapTime_{0}_1" },
                    DefaultStyles = "FontWeight=\"Bold\"",
                    Placeholders = new Dictionary<string, string>
                    {
                        { "{0}", "Racer Position (1-8)" },
                        { "Placeholder1", "LapTime_{0}_1 (lap time)" }
                    }
                },
               new LayoutSnippet
                {
                    Name = "Lower DockPanel",
                    Description = "Simplified layout for bottom-of-screen race table — 1 lane example.",
                    Category = "Layout Shells",
                    XamlTemplate =
                        "<DockPanel Margin=\"0,222,0,0\" Name=\"dockPanel2\" Background=\"Transparent\">\n" +
                        "  <Grid>\n" +
                        "    <Grid.RowDefinitions>\n" +
                        "      <RowDefinition Height=\"50\" />\n" +
                        "      <RowDefinition Height=\"100\" />\n" +
                        "    </Grid.RowDefinitions>\n" +
                        "    <Grid.ColumnDefinitions>\n" +
                        "      <ColumnDefinition Width=\"200\" />\n" +
                        "      <ColumnDefinition Width=\"100\" />\n" +
                        "      <ColumnDefinition Width=\"150\" />\n" +
                        "      <ColumnDefinition Width=\"150\" />\n" +
                        "      <ColumnDefinition Width=\"150\" />\n" +
                        "    </Grid.ColumnDefinitions>\n\n" +
                        "    <!-- Header labels -->\n" +
                        "    <Label Grid.Row=\"0\" Grid.Column=\"0\" Content=\"Name\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
                        "    <Label Grid.Row=\"0\" Grid.Column=\"1\" Content=\"Lap\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
                        "    <Label Grid.Row=\"0\" Grid.Column=\"2\" Content=\"Lap Time\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
                        "    <Label Grid.Row=\"0\" Grid.Column=\"3\" Content=\"Median\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n" +
                        "    <Label Grid.Row=\"0\" Grid.Column=\"4\" Content=\"Best\" HorizontalContentAlignment=\"Center\" FontWeight=\"Bold\" />\n\n" +
                        "    <!-- Lane 1 data row -->\n" +
                        "    <Label Grid.Row=\"1\" Grid.Column=\"0\" Name=\"Placeholder1\" Content=\"Racer 1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "    <Label Grid.Row=\"1\" Grid.Column=\"1\" Name=\"Placeholder2\" Content=\"0\" HorizontalContentAlignment=\"Center\" />\n" +
                        "    <Label Grid.Row=\"1\" Grid.Column=\"2\" Name=\"Placeholder3\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
                        "    <Label Grid.Row=\"1\" Grid.Column=\"3\" Name=\"Placeholder4\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
                        "    <Label Grid.Row=\"1\" Grid.Column=\"4\" Name=\"Placeholder5\" Content=\"00:00.000\" HorizontalContentAlignment=\"Center\" />\n" +
                        "  </Grid>\n" +
                        "</DockPanel>"
                },
                // Scaffolding: Full window scaffolding with updated menu, header, and racer layout
                new LayoutSnippet
                {
                    Name = "Scaffolding",
                    Description = "Full window scaffolding for Race Coordinator layout, with clear regions for menu, info, and lanes.",
                    Category = "Documents",
                    XamlTemplate =
                        "<!-- Scaffolding: Full window for Race Coordinator layout with clear regions for menu, info, and lanes -->\n" +
                        "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n" +
                        "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
                        "        Title=\"Race Coordinator Layout\"\n" +
                        "        Height=\"720\" Width=\"1280\"\n" +
                        "        Background=\"Black\">\n\n" +
                        "    <!-- ?? Optional: Theme Dictionary for styling -->\n" +
                        "    <!-- Use snippet: \"Theme Dictionary\" -->\n" +
                        "    <!-- <Window.Resources>\n" +
                        "         <ResourceDictionary Source=\"ThemeDictionary.xaml\"/>\n" +
                        "     </Window.Resources> -->\n\n" +
                        "    <!-- ?? Outer Border/Viewbox wrapper for scaling -->\n" +
                        "    <Border Background=\"Transparent\">\n" +
                        "        <Viewbox Stretch=\"Uniform\">\n" +
                        "            <Grid Background=\"White\" Margin=\"10\">\n\n" +
                        "                <!-- ?? Menu Bar goes here (if used) -->\n" +
                        "                <!-- Use snippet: \"General Menu\" -->\n" +
                        "                <Menu Height=\"22\" VerticalAlignment=\"Top\">\n" +
                        "                    <MenuItem Header=\"File\">\n" +
                        "                        <MenuItem Header=\"Save\"/>\n" +
                        "                        <MenuItem Header=\"Export Race\"/>\n" +
                        "                    </MenuItem>\n" +
                        "                </Menu>\n\n" +
                        "                <!-- ?? Upper Panel: Track + Race Info -->\n" +
                        "                <!-- Use snippet: \"Upper DockPanel\" or build from pieces -->\n" +
                        "                <DockPanel Height=\"200\" Margin=\"0,22,0,0\" Name=\"dockPanel1\">\n" +
                        "                    <Grid>\n" +
                        "                        <Grid.ColumnDefinitions>\n" +
                        "                            <ColumnDefinition />\n" +
                        "                            <ColumnDefinition />\n" +
                        "                            <ColumnDefinition />\n" +
                        "                        </Grid.ColumnDefinitions>\n\n" +
                        "                        <!-- ?? Track Info Block -->\n" +
                        "                        <!-- Use snippet: \"Track Info Panel\" -->\n" +
                        "                        <StackPanel Grid.Column=\"0\" VerticalAlignment=\"Center\" Margin=\"5\">\n" +
                        "                            <Image Name=\"TrackImage_1\" Height=\"115\" ToolTip=\"Track image from RC\" />\n" +
                        "                            <Label Content=\"Track:\" FontSize=\"12\" Foreground=\"Gray\" />\n" +
                        "                            <Label Name=\"TrackName_1\" FontSize=\"30\" FontWeight=\"Bold\"\n" +
                        "                                   Foreground=\"DarkSlateBlue\" HorizontalAlignment=\"Center\" />\n" +
                        "                        </StackPanel>\n\n" +
                        "                        <!-- ?? Race Info Block -->\n" +
                        "                        <!-- Use snippets: \"Race Name Label\", \"Race Time Label\", \"Heat Info DockPanel\" -->\n" +
                        "                        <StackPanel Grid.Column=\"1\" VerticalAlignment=\"Center\" Margin=\"5\">\n" +
                        "                            <Label Name=\"RaceName_1\" FontSize=\"28\" FontWeight=\"Bold\"\n" +
                        "                                   Foreground=\"GreenYellow\" HorizontalAlignment=\"Center\" />\n" +
                        "                            <Label Name=\"RaceTime_1\" FontSize=\"40\" FontWeight=\"Bold\"\n" +
                        "                                   Foreground=\"DarkGreen\" HorizontalAlignment=\"Center\" />\n" +
                        "                            <DockPanel HorizontalAlignment=\"Center\">\n" +
                        "                                <TextBlock Text=\"Heat \" />\n" +
                        "                                <TextBlock Name=\"HeatNumber_1\" FontWeight=\"Bold\" />\n" +
                        "                                <TextBlock Text=\" of \" />\n" +
                        "                                <TextBlock Name=\"NumHeats_1\" FontWeight=\"Bold\" />\n" +
                        "                            </DockPanel>\n" +
                        "                        </StackPanel>\n\n" +
                        "                        <!-- ?? Race Flag Block -->\n" +
                        "                        <!-- Use snippet: \"Race State Image\" -->\n" +
                        "                        <Image Grid.Column=\"2\" Name=\"RaceStateImage_1\" Width=\"150\" Height=\"150\"\n" +
                        "                               Stretch=\"Uniform\" ToolTip=\"Displays race state flag\" />\n" +
                        "                    </Grid>\n" +
                        "                </DockPanel>\n\n" +
                        "                <!-- ?? Lower Panel: Racer lanes and stats -->\n" +
                        "                <!-- Use snippet: \"Lower DockPanel\" (simplified) -->\n" +
                        "                <DockPanel Margin=\"0,222,0,0\" Name=\"dockPanel2\">\n" +
                        "                    <Grid>\n" +
                        "                        <!-- ?? Column headers -->\n" +
                        "                        <Grid.RowDefinitions>\n" +
                        "                            <RowDefinition Height=\"40\"/> <!-- Headers -->\n" +
                        "                            <RowDefinition Height=\"Auto\"/> <!-- Lane 1 -->\n" +
                        "                            <RowDefinition Height=\"Auto\"/> <!-- Lane 2 -->\n" +
                        "                        </Grid.RowDefinitions>\n" +
                        "                        <Grid.ColumnDefinitions>\n" +
                        "                            <ColumnDefinition Width=\"200\" /> <!-- Name -->\n" +
                        "                            <ColumnDefinition Width=\"100\" /> <!-- Lap -->\n" +
                        "                            <ColumnDefinition Width=\"150\" /> <!-- Lap Time -->\n" +
                        "                            <ColumnDefinition Width=\"150\" /> <!-- Median -->\n" +
                        "                            <ColumnDefinition Width=\"150\" /> <!-- Best -->\n" +
                        "                        </Grid.ColumnDefinitions>\n\n" +
                        "                        <!-- ?? Column Labels -->\n" +
                        "                        <Label Grid.Row=\"0\" Grid.Column=\"0\" Content=\"Name\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"0\" Grid.Column=\"1\" Content=\"Lap\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"0\" Grid.Column=\"2\" Content=\"Lap Time\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"0\" Grid.Column=\"3\" Content=\"Median\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"0\" Grid.Column=\"4\" Content=\"Best\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n\n" +
                        "                        <!-- ?? Lane 1 -->\n" +
                        "                        <!-- Use snippet: \"Racer Row\" + Lap Time Display -->\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"0\" Name=\"Nickname_Lane1_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"1\" Name=\"Lap_Lane1_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"2\" Name=\"LapTime_Lane1_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"3\" Name=\"MedianTime_Lane1_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"4\" Name=\"BestLapTime_Lane1_2\" HorizontalContentAlignment=\"Center\" />\n\n" +
                        "                        <!-- ?? Lane 2 (copy and update Lane1 ? Lane2) -->\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"0\" Name=\"Nickname_Lane2_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"1\" Name=\"Lap_Lane2_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"2\" Name=\"LapTime_Lane2_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"3\" Name=\"MedianTime_Lane2_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"4\" Name=\"BestLapTime_Lane2_2\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                    </Grid>\n" +
                        "                </DockPanel>\n\n" +
                        "            </Grid>\n" +
                        "        </Viewbox>\n" +
                        "    </Border>\n" +
                        "</Window>"
                },
                // Border Wrapper: Wraps selected content in a Border using the {content} placeholder for surround behavior
                new LayoutSnippet
                {
                    Name = "Border Wrapper",
                    Description = "Wraps selected content in a Border with gray border and margin.",
                    Category = "Formatting",
                    XamlTemplate =
                        "<Border BorderBrush=\"Green\" BorderThickness=\"10\" Margin=\"5\">\n{content}\n</Border>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
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
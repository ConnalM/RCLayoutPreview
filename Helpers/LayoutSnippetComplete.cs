using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Represents a reusable XAML layout snippet, with placeholders and required fields.
    /// </summary>
    public class LayoutSnippet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string XamlTemplate { get; set; }
        public List<string> RequiredFields { get; set; } = new List<string>();
        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, String>();
        public string DefaultStyles { get; set; }

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
        

        // List of available snippets
        public static List<LayoutSnippet> GetDefaultSnippets()
        {
            return new List<LayoutSnippet>
            {
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
                        "                                   Foreground=\"DarkSlateBlue\" HorizontalAlignment=\"Center\" />\r\n" +
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
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"0\" Name=\"Nickname_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"1\" Name=\"Lap_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"2\" Name=\"LapTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"3\" Name=\"MedianTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"1\" Grid.Column=\"4\" Name=\"BestLapTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n\n" +
                        "                        <!-- ?? Lane 2 (copy and update Lane1 ? Lane2) -->\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"0\" Name=\"Nickname_Lane2_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"1\" Name=\"Lap_Lane2_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"2\" Name=\"LapTime_Lane2_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"3\" Name=\"MedianTime_Lane2_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                        <Label Grid.Row=\"2\" Grid.Column=\"4\" Name=\"BestLapTime_Lane2_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "                    </Grid>\n" +
                        "                </DockPanel>\n\n" +
                        "            </Grid>\n" +
                        "        </Viewbox>\n" +
                        "    </Border>\n" +
                        "</Window>"
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
                
                // Menu Container: Wraps content in a Menu layout
                new LayoutSnippet
                {
                    Name = "Menu",
                    Description = "Wraps content in a Menu container.",
                    Category = "Layout Containers", 
                    XamlTemplate = "<Menu Height=\"22\" Name=\"menu1\" VerticalAlignment=\"Top\">\n    {content}\n</Menu>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // Menu Container (also in Menus): Wraps content in a Menu layout
                new LayoutSnippet
                {
                    Name = "Menu Container",
                    Description = "Wraps content in a Menu container for organizing menu items.",
                    Category = "Menus", 
                    XamlTemplate = "<Menu Height=\"22\" Name=\"menu1\" VerticalAlignment=\"Top\">\n    {content}\n</Menu>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // File Menu: Save, Save As, Export
                new LayoutSnippet
                {
                    Name = "File Menu",
                    Description = "File operations menu with Save, Save As, and Export options.",
                    Category = "Menus",
                    XamlTemplate =
                        "<!-- File Menu: Standard file operations -->\n" +
                        "<MenuItem Header=\"File\">\n" +
                        "    <MenuItem Name=\"Save_1\" Header=\"Save\" DataContext=\"'key':'S','modifier':'Alt'\" ToolTip=\"ALT-S to save the race\" />\n" +
                        "    <MenuItem Name=\"SaveAs_1\" Header=\"Save As...\" DataContext=\"'key':'A','modifier':'Alt'\" ToolTip=\"ALT-A to save the race with the specified file name\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"Export_1\" Header=\"Export Race\" DataContext=\"'key':'X','modifier':'Alt'\" ToolTip=\"ALT-X to export current race progress\" />\n" +
                        "</MenuItem>"
                },

                // Race Director Menu: Race control operations
                new LayoutSnippet
                {
                    Name = "Race Director Menu",
                    Description = "Race control menu with start, pause, heat management options.",
                    Category = "Menus",
                    XamlTemplate =
                        "<!-- Race Director Menu: Race control operations -->\n" +
                        "<MenuItem Header=\"Race Director\">\n" +
                        "    <MenuItem Name=\"Start_1\" Header=\"Start/Resume Heat\" DataContext=\"'key':'S','modifier':'Control'\" ToolTip=\"CTRL-S to start or restart the heat\" />\n" +
                        "    <MenuItem Name=\"Pause_1\" Header=\"Pause Heat\" DataContext=\"'key':'P','modifier':'Control'\" ToolTip=\"CTRL-P to pause the heat\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"AddLaps_1\" Header=\"Add Laps/Sections\" DataContext=\"'key':'A','modifier':'Control'\" ToolTip=\"CTRL-A to add laps/sections\" />\n" +
                        "    <MenuItem Name=\"NextHeat_1\" Header=\"Next Heat\" DataContext=\"'key':'N','modifier':'Control'\" ToolTip=\"CTRL-N to advance to the next heat\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"RestartHeat_1\" Header=\"Restart Heat\" DataContext=\"'key':'R','modifier':'Control'\" ToolTip=\"CTRL-R to restart the current heat\" />\n" +
                        "    <MenuItem Name=\"DeferHeat_1\" Header=\"Defer Heat\" DataContext=\"'key':'D','modifier':'Control'\" ToolTip=\"CTRL-D to defer this heat until the end of the race\" />\n" +
                        "    <MenuItem Name=\"SkipHeat_1\" Header=\"Skip Heat\" DataContext=\"'key':'F5','modifier':'Alt'\" ToolTip=\"ALT-F5 to skip the remainder of this heat\" />\n" +
                        "    <MenuItem Name=\"SkipRace_1\" Header=\"Skip Race\" DataContext=\"'key':'F4','modifier':'Alt'\" ToolTip=\"ALT-F4 to end this race\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"ModifyHeats_1\" Header=\"Modify Heats\" DataContext=\"'key':'H','modifier':'Control'\" ToolTip=\"CTRL-H to modify heat rotations\" />\n" +
                        "    <MenuItem Name=\"LapEditor_1\" Header=\"Edit Laps\" DataContext=\"'key':'E','modifier':'Control'\" ToolTip=\"CTRL-E to modify lap records\" />\n" +
                        "</MenuItem>"
                },

                // Track Power Menu: Power control operations
                new LayoutSnippet
                {
                    Name = "Track Power Menu",
                    Description = "Track power control menu for master and lane power management.",
                    Category = "Menus", 
                    XamlTemplate =
                        "<!-- Track Power Menu: Power control operations -->\n" +
                        "<MenuItem Header=\"Track Power\">\n" +
                        "    <MenuItem CommandParameter=\"0\" Name=\"PowerOn_1\" Header=\"Master Power On\" DataContext=\"'key':'0','modifier':'Control'\" ToolTip=\"CTRL-0 to force master power on\" />\n" +
                        "    <MenuItem CommandParameter=\"0\" Name=\"PowerOff_1\" Header=\"Master Power Off\" DataContext=\"'key':'0','modifier':'Alt'\" ToolTip=\"ALT-0 to force master power off\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem CommandParameter=\"1\" Name=\"PowerOn_2\" Header=\"Lane 1 Power On\" DataContext=\"'key':'1','modifier':'Control'\" ToolTip=\"CTRL-1 to force lane 1 power on\" />\n" +
                        "    <MenuItem CommandParameter=\"1\" Name=\"PowerOff_2\" Header=\"Lane 1 Power Off\" DataContext=\"'key':'1','modifier':'Alt'\" ToolTip=\"ALT-1 to force lane 1 power off\" />\n" +
                        "</MenuItem>"
                },

                // Windows Menu: Window display operations
                new LayoutSnippet
                {
                    Name = "Windows Menu",
                    Description = "Windows menu for displaying various race information windows.",
                    Category = "Menus",
                    XamlTemplate =
                        "<!-- Windows Menu: Window display operations -->\n" +
                        "<MenuItem Header=\"Windows\">\n" +
                        "    <MenuItem Name=\"Window_1\" Header=\"Leader Board\" CommandParameter=\"data/xaml/LeaderBoard.xaml\" DataContext=\"'key':'F1'\" ToolTip=\"F1 to display the Leader Board\" />\n" +
                        "    <MenuItem Name=\"Window_2\" Header=\"Top 5\" CommandParameter=\"data/xaml/Top5.xaml\" DataContext=\"'key':'F1','modifier':'Control'\" ToolTip=\"CTRL-F1 to display the Top 5 Leader Board\" />\n" +
                        "    <MenuItem Name=\"Window_3\" Header=\"Group Leader Board\" CommandParameter=\"data/xaml/GroupLeaderBoard.xaml\" DataContext=\"'key':'F3'\" ToolTip=\"F3 to display the Group Leader Board\" />\n" +
                        "    <MenuItem Name=\"Window_4\" Header=\"Heat Results\" CommandParameter=\"data/xaml/HeatResults.xaml\" DataContext=\"'key':'F4'\" ToolTip=\"F4 to display the Heat Results window\" />\n" +
                        "    <MenuItem Name=\"Window_5\" Header=\"Race Results\" CommandParameter=\"data/xaml/RaceResults.xaml\" DataContext=\"'key':'F5'\" ToolTip=\"F5 to display the Race Results window\" />\n" +
                        "    <MenuItem Name=\"Window_6\" Header=\"Segment Times\" CommandParameter=\"data/xaml/SegmentTimeOnlyView_1L.xaml\" DataContext=\"'key':'F6','modifier':'Control'\" ToolTip=\"CTRL-F6 to display the Segment Timing window\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"Window_7\" Header=\"Season Race Leader Board\" CommandParameter=\"data/xaml/SeasonRaceLeaderBoard.xaml\" DataContext=\"'key':'F1','modifier':'Alt'\" ToolTip=\"ALT-F1 to display the Season Race Leader Board\" />\n" +
                        "    <MenuItem Name=\"Window_8\" Header=\"Season Leader Board\" CommandParameter=\"data/xaml/SeasonLeaderBoard.xaml\" ToolTip=\"Display the Season Leader Board\" />\n" +
                        "    <Separator/>\n" +
                        "    <MenuItem Name=\"Window_9\" Header=\"On Deck\" CommandParameter=\"data/xaml/OnDeck_1L.xaml\" DataContext=\"'key':'F6'\" ToolTip=\"F6 to display the On Deck window\" />\n" +
                        "    <MenuItem Name=\"ViewHeats_1\" Header=\"Heat List\" DataContext=\"'key':'F7'\" ToolTip=\"F7 to display heat rotations\" />\n" +
                        "    <MenuItem Name=\"Window_10\" Header=\"Next Heat\" CommandParameter=\"data/xaml/NextHeat_1L.xaml\" DataContext=\"'key':'F8'\" ToolTip=\"F8 to display all drivers in the next heat\" />\n" +
                        "</MenuItem>"
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
                    Description = "Single lane layout for bottom-of-screen race table.",
                    Category = "Layout Shells",
                    XamlTemplate =
                        "<!-- Single Lane Lower DockPanel: Clean layout for bottom-of-screen race table -->\n" +
                        "<DockPanel Margin=\"0,222,0,0\" Name=\"dockPanel2\">\n" +
                        "    <Grid>\n" +
                        "        <!-- Column headers -->\n" +
                        "        <Grid.RowDefinitions>\n" +
                        "            <RowDefinition Height=\"40\"/> <!-- Headers -->\n" +
                        "            <RowDefinition Height=\"Auto\"/> <!-- Lane 1 -->\n" +
                        "        </Grid.RowDefinitions>\n" +
                        "        <Grid.ColumnDefinitions>\n" +
                        "            <ColumnDefinition Width=\"200\" /> <!-- Name -->\n" +
                        "            <ColumnDefinition Width=\"100\" /> <!-- Lap -->\n" +
                        "            <ColumnDefinition Width=\"150\" /> <!-- Lap Time -->\n" +
                        "            <ColumnDefinition Width=\"150\" /> <!-- Median -->\n" +
                        "            <ColumnDefinition Width=\"150\" /> <!-- Best -->\n" +
                        "        </Grid.ColumnDefinitions>\n\n" +
                        "        <!-- Column Labels -->\n" +
                        "        <Label Grid.Row=\"0\" Grid.Column=\"0\" Content=\"Name\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"0\" Grid.Column=\"1\" Content=\"Lap\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"0\" Grid.Column=\"2\" Content=\"Lap Time\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"0\" Grid.Column=\"3\" Content=\"Median\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"0\" Grid.Column=\"4\" Content=\"Best\" FontWeight=\"Bold\" HorizontalContentAlignment=\"Center\" />\n\n" +
                        "        <!-- Lane 1 -->\n" +
                        "        <Label Grid.Row=\"1\" Grid.Column=\"0\" Name=\"Nickname_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"1\" Grid.Column=\"1\" Name=\"Lap_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"1\" Grid.Column=\"2\" Name=\"LapTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"1\" Grid.Column=\"3\" Name=\"MedianTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "        <Label Grid.Row=\"1\" Grid.Column=\"4\" Name=\"BestLapTime_Lane1_1\" HorizontalContentAlignment=\"Center\" />\n" +
                        "    </Grid>\n" +
                        "</DockPanel>",
                    RequiredFields = new List<string> { 
                        "Nickname_Lane1_1", "Lap_Lane1_1", "LapTime_Lane1_1", 
                        "MedianTime_Lane1_1", "BestLapTime_Lane1_1"
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
          
                
                // Border Wrapper: Wraps selected content in a Border using the {content} placeholder for surround behavior
                new LayoutSnippet
                {
                    Name = "Border Wrapper",
                    Description = "Wraps selected content in a Border with gray border and margin.",
                    Category = "Formatting",
                    XamlTemplate =
                        "<Border BorderBrush=\"Green\" BorderThickness=\"10\" Margin=\"5\">\n    {content}\n</Border>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },
                // New snippet for Grid
                new LayoutSnippet
                {
                    Name = "Grid",
                    Description = "Wraps content in a Grid layout.",
                    Category = "Layout Containers",
                    XamlTemplate = "<Grid>\n    {content}\n</Grid>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // New snippet for DockPanel
                new LayoutSnippet
                {
                    Name = "DockPanel",
                    Description = "Wraps content in a DockPanel layout.",
                    Category = "Layout Containers",
                    XamlTemplate = "<DockPanel LastChildFill=\"True\">\n    {content}\n</DockPanel>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // New snippet for Viewbox
                new LayoutSnippet
                {
                    Name = "Viewbox",
                    Description = "Wraps content in a Viewbox for scaling.",
                    Category = "Layout Containers",
                    XamlTemplate = "<Viewbox Stretch=\"Uniform\">\n    {content}\n</Viewbox>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // New snippet for StackPanel
                new LayoutSnippet
                {
                    Name = "StackPanel",
                    Description = "Wraps content in a StackPanel layout.",
                    Category = "Layout Containers",
                    XamlTemplate = "<StackPanel Orientation=\"Vertical\">\n    {content}\n</StackPanel>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // New snippet for Canvas
                new LayoutSnippet
                {
                    Name = "Canvas",
                    Description = "Wraps content in a Canvas layout for absolute positioning.",
                    Category = "Layout Containers",
                    XamlTemplate = "<Canvas>\n    {content}\n</Canvas>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },

                // New snippet for UniformGrid
                new LayoutSnippet
                {
                    Name = "UniformGrid",
                    Description = "Wraps content in a UniformGrid with evenly sized cells.",
                    Category = "Layout Containers",
                    XamlTemplate = "<UniformGrid Rows=\"2\" Columns=\"2\">\n    {content}\n</UniformGrid>",
                    Placeholders = new Dictionary<string, string> { { "{content}", "" } }
                },
            };
        }

        /// <summary>
        /// Processes a snippet, replacing placeholders and applying styles/content.
        /// </summary>
        /// <param name="snippet">LayoutSnippet to process</param>
        /// <param name="position">Position value for formatting</param>
        /// <param name="content">Content to insert for {content} placeholder</param>
        /// <returns>Processed XAML string</returns>
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

            // CRITICAL FIX: Process all placeholders from the Placeholders dictionary
            if (snippet.Placeholders != null && snippet.Placeholders.Count > 0)
            {
                foreach (var placeholder in snippet.Placeholders)
                {
                        xaml = xaml.Replace(placeholder.Key, placeholder.Value);
                }
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

        /// <summary>
        /// Returns the entire class definition as a string to avoid truncation issues.
        /// </summary>
        private static string GetCompleteLayoutSnippetContent()
        {
            return @"using System.Collections.Generic;\n\nnamespace RCLayoutPreview.Helpers\n{\n    public class LayoutSnippet\n    {\n        public string Name { get; set; }\n        public string Description { get; set; }\n        public string Category { get; set; }\n        public string XamlTemplate { get; set; }\n        public List<string> RequiredFields { get; set; } = new List<string>();\n        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, string>();\n        public string DefaultStyles { get; set; }\n        // ... (rest of the class definition as string) ...\n    }\n}";
        }
    }
}
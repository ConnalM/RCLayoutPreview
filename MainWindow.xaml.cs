using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RCLayoutPreview.Data;
using RCLayoutPreview.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using static RCLayoutPreview.Data.StubDataBuilder;
using static RCLayoutPreview.Helpers.RacerHelpers;

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private bool isPreviewing = false;
        private string currentLayoutPath = null;
        private string currentJsonPath = null;
        private RaceData currentData = null;
        private bool IsDiagnosticsMode => DebugModeToggle?.IsChecked == true;

        public MainWindow()
        {
            InitializeComponent();
            // Auto-load stubdata.json if it exists in the app directory
            var stubPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stubdata.json");
            if (File.Exists(stubPath))
            {
                try
                {
                    currentJsonPath = stubPath;
                    var json = File.ReadAllText(stubPath);
                    currentData = JsonConvert.DeserializeObject<RaceData>(json);
                    SetStatus($"Auto-loaded: {Path.GetFileName(stubPath)}");
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to auto-load stubdata.json: {ex.Message}");
                }
            }
            Preview_Click(null, null);
            RCLayoutPreview.Helpers.XamlFixer.TestXamlFixer();
        }

        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || isPreviewing)
                return;
            Preview_Click(null, null); // Re-render with new debug state
        }

        private void LoadLayout_Click(object sender, RoutedEventArgs e)
        {
            SetStatus(IsDiagnosticsMode ? "Diagnostics: ON" : "Diagnostics: OFF");
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XAML Layout (*.xaml)|*.xaml",
                Title = "Select Layout XAML"
            };
            SetStatus($"Layout loaded: {currentLayoutPath}");
            if (dlg.ShowDialog() == true)
            {
                currentLayoutPath = dlg.FileName;
                Editor.Text = File.ReadAllText(currentLayoutPath);
                SetStatus($"Loaded: {System.IO.Path.GetFileName(currentLayoutPath)}");
            }
        }

        private void LoadJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Data (*.json)|*.json",
                Title = "Select Stub Data"
            };

            if (dlg.ShowDialog() == true)
            {
                currentJsonPath = dlg.FileName;
                currentData = JsonConvert.DeserializeObject<RaceData>(File.ReadAllText(currentJsonPath));
                SetStatus($"Loaded: {System.IO.Path.GetFileName(currentJsonPath)}");
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || Editor == null || isPreviewing)
                return;

            try
            {
                isPreviewing = true;

                FrameworkElement layout = LoadLayoutFromText(Editor.Text);
                SetStatus("Previewing layout");

                if (layout is Panel panel)
                    panel.Background = Brushes.Black;
                else if (layout is Border border)
                    border.Background = Brushes.Black;
                else if (layout is Control control)
                    control.Background = Brushes.Black;

                foreach (var tb in GetAllNamedTextBlocks(layout))
                {
                    if (tb.Foreground == null || tb.Foreground == Brushes.Black)
                        tb.Foreground = Brushes.White;
                }

                if (!string.IsNullOrWhiteSpace(currentJsonPath) && File.Exists(currentJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(currentJsonPath);
                        currentData = JsonConvert.DeserializeObject<RaceData>(json);
                        var patch = StubDataBuilder.GenerateFromLayout(layout);
                        StubDataBuilder.PatchMissingFields(currentData, patch);

                        // Top-up missing fields using layout-driven patch
                        foreach (var patchRacer in patch.Racers)
                        {
                            var target = GetRacerByQualifier("Lane", patchRacer.Lane, currentData.Racers);
                            if (target == null) continue;

                            foreach (var kvp in patchRacer.Extras)
                            {
                                target.Extras ??= new Dictionary<string, JToken>();
                                if (!target.Extras.ContainsKey(kvp.Key))
                                    target.Extras[kvp.Key] = kvp.Value;
                            }
                        }

                        foreach (var kvp in patch.GenericData?.GetType().GetProperties())
                        {
                            var value = kvp.GetValue(patch.GenericData);
                            var prop = currentData.GenericData?.GetType().GetProperty(kvp.Name);
                            if (prop?.GetValue(currentData.GenericData) == null)
                                prop?.SetValue(currentData.GenericData, value);
                        }

                        PrintFieldOriginSummary(currentData, patch);

                        SetStatus("Stub JSON loaded and patched with layout fields.");
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Failed to load JSON: {ex.Message}");
                        currentData = StubDataBuilder.GenerateFromLayout(layout); // fallback
                    }
                }
                else
                {
                    currentData = StubDataBuilder.GenerateFromLayout(layout);
                    SetStatus("No stub JSON found. Created stub data from layout.");
                }

                // Ensure NextHeatNickname1 is present in Extras for every racer
                foreach (var racer in currentData.Racers)
                {
                    racer.Extras ??= new Dictionary<string, JToken>();
                    if (!racer.Extras.ContainsKey("NextHeatNickname1"))
                    {
                        racer.Extras["NextHeatNickname1"] = "DefaultNickname"; // Set your desired default value here
                    }
                }

                // After generating or updating currentData, call DumpAllRacerExtras.
                if (currentData == null)
                    currentData = GenerateFakeData();

                // Add this line to verify injected fields:
                RCLayoutPreview.Data.StubDataBuilder.DumpAllRacerExtras(currentData);

                // After calling DumpAllRacerExtras, add this check to log missing expected fields:
                var expectedFields = new[] { "NextHeatNickname1", /* add other expected fields here */ };
                foreach (var racer in currentData.Racers)
                {
                    foreach (var field in expectedFields)
                    {
                        if (racer.Extras == null || !racer.Extras.ContainsKey(field))
                        {
                            Console.WriteLine($"❌ Missing field '{field}' in Racer.Lane={racer.Lane}");
                        }
                    }
                }

                foreach (var racer in currentData.Racers)
                {
                    if (racer.Extras != null && racer.Extras.ContainsKey("NextHeatNickname1"))
                    {
                        Console.WriteLine($"Lane {racer.Lane}: NextHeatNickname1 = {racer.Extras["NextHeatNickname1"]}");
                    }
                    else
                    {
                        Console.WriteLine($"Lane {racer.Lane}: NextHeatNickname1 not found in Extras.");
                    }
                }

                layout.DataContext = currentData;

                // Wrap the layout in a border for consistent padding/background
                var wrapper = new Border
                {
                    Background = Brushes.Black,
                    Padding = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = layout
                };

                // Always inject stub data so TextBlocks are updated
                InjectStubData(layout, currentData);

                var overlayContainer = new Grid();
                overlayContainer.Children.Add(wrapper);

                var overlay = BuildDiagnosticsOverlay(GetAllNamedTextBlocks(layout), currentData);
                overlayContainer.Children.Add(overlay);

                var diagnosticsToggle = new CheckBox
                {
                    Content = "Diagnostics Mode",
                    IsChecked = IsDiagnosticsMode,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10),
                    Foreground = Brushes.White,
                    Background = Brushes.Black,
                    Opacity = 0.8,
                    Width = 150,
                    Height = 30,
                    BorderBrush = Brushes.Red,
                    BorderThickness = new Thickness(2)
                };
                Grid.SetZIndex(diagnosticsToggle, 1000);
                diagnosticsToggle.Checked += DebugModeToggle_Changed;
                diagnosticsToggle.Unchecked += DebugModeToggle_Changed;
                overlayContainer.Children.Add(diagnosticsToggle);

                Console.WriteLine("Diagnostics checkbox added to overlayContainer.");

                PreviewHost.Content = overlayContainer;

                HookBlockClickHandlers(layout);
                SetStatus("Preview updated");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview error:\n" + ex.Message);
                SetStatus("Failed to preview layout");
            }
            finally
            {
                isPreviewing = false;
            }
        }

        private void SetStatus(string message)
        {
            StatusLabel.Text = message; // Update the UI element or log the message.
        }

        private void HookBlockClickHandlers(FrameworkElement layout)
        {
            foreach (var block in GetAllNamedTextBlocks(layout))
            {
                block.MouseLeftButtonDown -= OnDebugBlockClicked; // prevent stacking events
                block.MouseLeftButtonDown += OnDebugBlockClicked;
            }
        }

        private void OnDebugBlockClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock block)
            {
                // Add visual adorner outline
                var adornerLayer = AdornerLayer.GetAdornerLayer(block);
                if (adornerLayer != null)
                {
                    adornerLayer.Add(new DebugFieldSelector(block));
                }

                // Optional: Immediate visual feedback
                block.Background = Brushes.DeepSkyBlue;
                block.Foreground = Brushes.White;
                block.FontWeight = FontWeights.Bold;

                // Optional: show field name in tooltip
                block.ToolTip = block.Name;
            }
        }

        private FrameworkElement CloneVisual(FrameworkElement original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            string xaml = XamlWriter.Save(original);
            using (var reader = new StringReader(xaml))
            using (var xml = XmlReader.Create(reader))
                return (FrameworkElement)XamlReader.Load(xml);
        }

        private FrameworkElement LoadLayoutFromText(string rawXaml)
        {
            string fixedXaml = XamlFixer.Preprocess(rawXaml);

            using (var reader = new StringReader(fixedXaml))
            using (var xml = XmlReader.Create(reader))
            {
                object root = XamlReader.Load(xml);

                switch (root)
                {
                    case ResourceDictionary rd when rd.Contains("LayoutRoot"):
                        return CloneVisual(rd["LayoutRoot"] as FrameworkElement);

                    case FrameworkElement fe when fe.GetType().Name != "Window":
                        return CloneVisual(fe);

                    case Window win when win.Content is FrameworkElement inner:
                        return CloneVisual(inner);

                    default:
                        throw new InvalidOperationException("Unsupported XAML structure.");
                }
            }
        }

        private RaceData GenerateFakeData()
        {
            var list = new List<Racer>();
            for (int i = 1; i <= 6; i++)
            {
                list.Add(new Racer
                {
                    Name = $"Racer {i}",
                    Lap = 12 + i,
                    BestLapTime = $"{20.5 + i:0.000}",
                    GapLeader = i == 1 ? "0.000" : $"+{0.4 * i:0.000}",
                    CarModel = $"Car {i}",
                    Avatar = "🤖",
                    LaneColor = "#FF00FF",
                    FuelPercent = 95 - i * 6,
                    ReactionTime = $"{0.6 - i * 0.03:0.000}s",
                    IsLeader = i == 1,
                    Lane = i,
                    TireChoice = (i % 2 == 0) ? "Soft" : "Hard",
                    PitStops = i / 2,
                });
            }

            return new RaceData
            {
                GenericData = new GenericData
                {
                    RaceName = "Spring Showdown",
                    TrackName = "Kirkwood Circuit",
                    NextHeatNumber = "7"
                },
                Racers = list
            };
        }

        private Racer GetRacerByQualifier(string qualifier, int index, List<Racer> racers)
        {
            if (racers == null || index < 1) return null;

            return qualifier switch
            {
                "Lane" => racers.FirstOrDefault(r => r.Lane == index),
                "Position" => racers.OrderBy(r => r.Position).Skip(index - 1).FirstOrDefault(),
                "RaceLeader" => racers.OrderBy(r => r.IsLeader ? 0 : 1).Skip(index - 1).FirstOrDefault(),
                "GroupLeader" => racers.Skip(index - 1).FirstOrDefault(), // Stub: no group logic yet
                "TeamLeader" => racers.Skip(index - 1).FirstOrDefault(), // Stub: no team logic yet
                _ => null
            };
        }

        private void InjectStubData(FrameworkElement layout, RaceData data)
        {
            if (layout == null || data == null)
                return;

            foreach (var block in GetAllNamedTextBlocks(layout))
            {
                string fieldName = block.Tag as string ?? block.Name;
                string value = null;

                // Try to parse the field name using RC conventions
                ParsedField parsed;
                if (FieldNameParser.TryParse(fieldName, out parsed))
                {
                    if (parsed.IsGeneric)
                    {
                        // Generic data (e.g., NextHeatNumber_1)
                        var prop = data.GenericData?.GetType().GetProperty(parsed.BaseName);
                        value = prop?.GetValue(data.GenericData)?.ToString();
                    }
                    else
                    {
                        // Racer data (e.g., Name_Lane1, Lap_Position2, etc.)
                        var racer = data.Racers?.ElementAtOrDefault(parsed.InstanceIndex - 1);
                        if (racer != null)
                        {
                            if (racer.Extras != null && racer.Extras.TryGetValue(parsed.BaseName, out var extraVal))
                                value = extraVal?.ToString();
                        }
                    }
                }
                else
                {
                    // Fallback: try to find the field in any racer's Extras
                    foreach (var racer in data.Racers)
                    {
                        if (racer.Extras != null && racer.Extras.TryGetValue(fieldName, out var extraVal))
                        {
                            value = extraVal?.ToString();
                            break;
                        }
                    }
                }

                // Set the Text property for the block
                block.Text = value ?? string.Empty;
            }
        }

        private IEnumerable<TextBlock> GetAllNamedTextBlocks(DependencyObject parent)
        {
            if (parent == null) yield break;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Name))
                    yield return tb;

                foreach (var descendant in GetAllNamedTextBlocks(child))
                    yield return descendant;
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentLayoutPath))
            {
                MessageBox.Show("No layout file loaded.");
                return;
            }

            try
            {
                File.WriteAllText(currentLayoutPath, Editor.Text);
                SetStatus($"Saved: {System.IO.Path.GetFileName(currentLayoutPath)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save error:\n" + ex.Message);
            }
        }

        private StackPanel BuildDiagnosticsOverlay(IEnumerable<TextBlock> blocks, RaceData data)
        {
            var panel = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10)
            };

            foreach (var block in blocks)
            {
                var tag = block.Tag as string ?? block.Name;
                string value = null;

                if (FieldNameParser.TryParse(tag, out var parsed))
                {
                    if (parsed.IsGeneric)
                    {
                        var prop = data.GenericData?.GetType().GetProperty(parsed.BaseName);
                        value = prop?.GetValue(data.GenericData)?.ToString();

                        // If not found in GenericData, check all Racers' Extras
                        if (string.IsNullOrWhiteSpace(value) && data.Racers != null)
                        {
                            foreach (var racer in data.Racers)
                            {
                                if (racer.Extras != null && racer.Extras.TryGetValue(parsed.BaseName, out var extraVal))
                                {
                                    value = extraVal?.ToString();
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        var racer = data.Racers?.ElementAtOrDefault(parsed.InstanceIndex - 1);
                        if (racer != null)
                        {
                            if (racer.Extras != null && racer.Extras.TryGetValue(parsed.BaseName, out var extraVal))
                                value = extraVal?.ToString();
                            else
                            {
                                var prop = racer.GetType().GetProperty(parsed.BaseName);
                                value = prop?.GetValue(racer)?.ToString();
                            }
                        }
                    }
                }
                else
                {
                    // If parsing fails, look for the full tag in Extras
                    if (data.Racers != null)
                    {
                        foreach (var racer in data.Racers)
                        {
                            if (racer.Extras != null && racer.Extras.TryGetValue(tag, out var extraVal))
                            {
                                value = extraVal?.ToString();
                                break;
                            }
                        }
                    }
                }

                panel.Children.Add(new TextBlock
                {
                    Text = $"{tag}: {(string.IsNullOrWhiteSpace(value) ? "[missing]" : value)}",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    Margin = new Thickness(4, 2, 4, 2)
                });
            }

            return panel;
        }
    }

    public static class FieldNameParser
    {
        public static bool TryParse(string rawName, out ParsedField parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(rawName))
                return false;

            // Regex: FieldName(_QualifierTypeQualifierIndex)?(_InstanceIndex)?
            // e.g. NextHeatNickname1_1, Name_Lane2, Lap_Position1_2
            string pattern = @"^(?<field>[A-Za-z0-9]+?)(?:_(?<qualifier>[A-Za-z]+)?(?<qualifierIndex>\d+))?(?:_(?<instanceIndex>\d+))?$";
            var match = Regex.Match(rawName, pattern);

            if (match.Success)
            {
                string field = match.Groups["field"].Value;
                string qualifier = match.Groups["qualifier"].Success ? match.Groups["qualifier"].Value : null;
                string qualifierIndex = match.Groups["qualifierIndex"].Success ? match.Groups["qualifierIndex"].Value : null;
                string instanceIndex = match.Groups["instanceIndex"].Success ? match.Groups["instanceIndex"].Value : null;

                // Default values
                string qualifierType = qualifier;
                int? qIndex = string.IsNullOrEmpty(qualifierIndex) ? (int?)null : int.Parse(qualifierIndex);
                int iIndex = string.IsNullOrEmpty(instanceIndex) ? 1 : int.Parse(instanceIndex);
                bool isGeneric = string.IsNullOrEmpty(qualifierType) && !qIndex.HasValue;

                // Fallback: infer Lane if pattern is like SomeField1_1 (no qualifier, but numeric suffix)
                if (string.IsNullOrEmpty(qualifierType) && !qIndex.HasValue && rawName.Contains("_"))
                {
                    var parts = rawName.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int laneIdx))
                    {
                        qualifierType = "Lane";
                        qIndex = laneIdx;
                        isGeneric = false;
                    }
                }

                parsed = new ParsedField
                {
                    BaseName = field,
                    QualifierType = qualifierType,
                    QualifierIndex = qIndex,
                    InstanceIndex = iIndex,
                    IsGeneric = isGeneric
                };
                return true;
            }

            return false;
        }
    }

    public class ParsedField
    {
        public string BaseName { get; set; }
        public string QualifierType { get; set; }
        public int? QualifierIndex { get; set; }
        public int InstanceIndex { get; set; }
        public bool IsGeneric { get; set; }
    }
}
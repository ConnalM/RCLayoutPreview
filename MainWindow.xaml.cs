using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using RCLayoutPreview.Helpers;


namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private string currentLayoutPath = null;
        private string currentJsonPath = null;
        private RaceData currentData = null;
        private bool IsDiagnosticsMode => DebugToggle.IsChecked == true;


        public MainWindow()
        {
            
            var themeDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ThemeDictionary.xaml")
            };
            Application.Current.Resources.MergedDictionaries.Add(themeDict);

            InitializeComponent();
        }

        

        private void DebugModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            Preview_Click(null, null); // Re-render with new debug state
        }

        private void LoadLayout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XAML Layout (*.xaml)|*.xaml",
                Title = "Select Layout XAML"
            };

            if (dlg.ShowDialog() == true)
            {
                currentLayoutPath = dlg.FileName;
                Editor.Text = File.ReadAllText(currentLayoutPath);
                StatusLabel.Text = $"Loaded: {System.IO.Path.GetFileName(currentLayoutPath)}";
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
                StatusLabel.Text = $"Loaded: {System.IO.Path.GetFileName(currentJsonPath)}";
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || Editor == null)
                return;
            try
            {
                FrameworkElement layout = LoadLayoutFromText(Editor.Text);

                // Set background fallback
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

                if (currentData == null)
                    currentData = GenerateFakeData();

                layout.DataContext = currentData;

                HookBlockClickHandlers(layout); // 🎯 Adds click handlers to blocks

                InjectStubData(layout, currentData);
                PreviewHost.Content = layout;
                StatusLabel.Text = "Preview updated";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview error:\n" + ex.Message);
                StatusLabel.Text = "Failed to preview layout";
            }
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



        private FrameworkElement LoadLayoutFromText(string rawXaml)
        {
            string fixedXaml = XamlFixer.Preprocess(rawXaml);

            using (var reader = new StringReader(fixedXaml))
            using (var xml = XmlReader.Create(reader))
            {
                object root = XamlReader.Load(xml);

                // Handle ResourceDictionary with named root
                if (root is ResourceDictionary rd && rd.Contains("LayoutRoot"))
                    return (FrameworkElement)rd["LayoutRoot"];

                // Direct visual element (Grid, StackPanel, etc.)
                if (root is FrameworkElement fe && fe.GetType().Name != "Window")
                    return fe;

                // Handle Window: unwrap its content if possible
                if (root is Window win)
                {
                    if (win.Content is FrameworkElement inner)
                        return inner;

                    // Rare: content isn't a FrameworkElement (e.g. raw string or image)
                    throw new InvalidOperationException("Window contains unsupported content type.");
                }

                throw new InvalidOperationException("Unsupported XAML structure.");
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
                    PitStops = i / 2
                });
            }

            return new RaceData
            {
                GenericData = new GenericData
                {
                    RaceName = "Spring Showdown",
                    TrackName = "Kirkwood Circuit"
                },
                Racers = list
            };
        }

        private void InjectStubData(FrameworkElement layout, RaceData data)
        {
            if (layout == null || data == null)
                return;

            foreach (var block in GetAllNamedTextBlocks(layout))
            {
                string fieldName = block.Tag as string ?? block.Name;

                // Use your parser that outputs a FieldNameParser instance
                FieldNameParser parsed;
                if (!FieldNameParser.TryParse(fieldName, out parsed))
                {
                    if (IsDiagnosticsMode)
                    {
                        block.Text = $"⚠️ Unknown field: {fieldName}";
                        block.Background = Brushes.DarkRed;
                        block.Foreground = Brushes.White;
                    }
                    else
                    {
                        block.Text = string.Empty;
                        block.Background = null;
                        block.Foreground = null;
                    }
                    continue;
                }

                string field = parsed.FieldType;
                int index = parsed.InstanceIndex;
                string value = null;

                if (parsed.IsGeneric)
                {
                    var prop = data.GenericData?.GetType().GetProperty(field);
                    value = prop?.GetValue(data.GenericData)?.ToString();
                }
                else
                {
                    int racerIndex = index - 1;
                    if (racerIndex >= 0 && racerIndex < data.Racers.Count)
                    {
                        var racer = data.Racers[racerIndex];
                        var prop = racer.GetType().GetProperty(field);
                        value = prop?.GetValue(racer)?.ToString();
                    }
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    block.Text = IsDiagnosticsMode ? $"{field} (no value)" : string.Empty;
                    block.Background = IsDiagnosticsMode ? Brushes.DarkOrange : null;
                    block.Foreground = IsDiagnosticsMode ? Brushes.White : null;
                }
                else
                {
                    block.Text = $"{field} ({value})";
                    block.Background = null;
                    block.Foreground = null;
                }
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
                StatusLabel.Text = $"Saved: {System.IO.Path.GetFileName(currentLayoutPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save error:\n" + ex.Message);
            }
        }
        }

    public class RaceData
    {
        public GenericData GenericData { get; set; }
        public List<Racer> Racers { get; set; }
    }

    public class GenericData
    {
        public string RaceName { get; set; }
        public string TrackName { get; set; }
        public string NextHeatNumber { get; set; }

        // ⬇️ Add these two lines
        public string EventName { get; set; }
        public string Weather { get; set; }
    }

    public class Racer
    {
        public string Name { get; set; }
        public int Lap { get; set; }
        public string BestLapTime { get; set; }
        public string GapLeader { get; set; }
        public string CarModel { get; set; }
        public string Avatar { get; set; }
        public string LaneColor { get; set; }
        public int FuelPercent { get; set; }
        public string ReactionTime { get; set; }
        public bool IsLeader { get; set; }
        public int Lane { get; set; }
        public string TireChoice { get; set; }
        public int PitStops { get; set; }
    }
}
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RCLayoutPreview.Data;
using RCLayoutPreview.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace RCLayoutPreview
{
    public partial class MainWindow : Window
    {
        private bool isPreviewing = false;
        private string currentLayoutPath = null;
        private string currentJsonPath = null;
        private RaceData currentData = null;
        private bool IsDiagnosticsMode => DebugModeToggle?.IsChecked == true;
        private TextBlock Editor; // Reference to the Editor TextBlock
        private ContentControl PreviewHost; // Reference to the Preview host
        private StackPanel PreviewPanel; // Reference to the preview panel
        private TextBlock StatusBlock; // Reference to the status block
        private CheckBox DebugModeToggle; // Reference to debug mode toggle

        public MainWindow()
        {
            InitializeComponent();
            
            // Find necessary UI elements
            Editor = FindName("Editor") as TextBlock;
            PreviewHost = FindName("PreviewHost") as ContentControl;
            PreviewPanel = FindName("PreviewPanel") as StackPanel;
            StatusBlock = FindName("StatusLabel") as TextBlock;
            DebugModeToggle = FindName("DebugModeToggle") as CheckBox;
            
            // Auto-load stubdata4.json if it exists in the app directory, fallback to stubdata.json
            var stub4Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stubdata4.json");
            var stubPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stubdata.json");
            
            if (File.Exists(stub4Path))
            {
                try
                {
                    currentJsonPath = stub4Path;
                    var json = File.ReadAllText(stub4Path);
                    currentData = JsonConvert.DeserializeObject<RaceData>(json);
                    SetStatus($"Auto-loaded: {Path.GetFileName(stub4Path)}");
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to auto-load stubdata4.json: {ex.Message}");
                    LoadFallbackStubData(stubPath);
                }
            }
            else if (File.Exists(stubPath))
            {
                LoadFallbackStubData(stubPath);
            }
            
            Preview_Click(null, null);
            Helpers.XamlFixer.TestXamlFixer();
            
            // Test the field parser
            FieldNameParser.TestFieldNameParser();
        }

        private void LoadFallbackStubData(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    currentJsonPath = path;
                    var json = File.ReadAllText(path);
                    currentData = JsonConvert.DeserializeObject<RaceData>(json);
                    SetStatus($"Auto-loaded fallback: {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to auto-load fallback stub data: {ex.Message}");
                }
            }
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
                if (Editor != null)
                    Editor.Text = File.ReadAllText(currentLayoutPath);
                SetStatus($"Loaded: {System.IO.Path.GetFileName(currentLayoutPath)}");
            }
        }

        private void LoadJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Data (*.json)|*.json",
                Title = "Select Stub Data",
                FileName = "stubdata4.json" // Default to stubdata4.json
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    currentJsonPath = dlg.FileName;
                    var json = File.ReadAllText(currentJsonPath);
                    currentData = JsonConvert.DeserializeObject<RaceData>(json);
                    SetStatus($"Loaded: {System.IO.Path.GetFileName(currentJsonPath)}");
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to load JSON: {ex.Message}");
                }
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
                        SetStatus($"Stub JSON loaded: {Path.GetFileName(currentJsonPath)}");
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Failed to load JSON: {ex.Message}");
                        currentData = GenerateFakeData(); // fallback
                    }
                }
                else
                {
                    // Try to find stubdata4.json in the app directory
                    var stub4Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stubdata4.json");
                    if (File.Exists(stub4Path))
                    {
                        try
                        {
                            currentJsonPath = stub4Path;
                            var json = File.ReadAllText(stub4Path);
                            currentData = JsonConvert.DeserializeObject<RaceData>(json);
                            SetStatus($"Auto-loaded: {Path.GetFileName(stub4Path)}");
                        }
                        catch
                        {
                            currentData = GenerateFakeData(); // fallback on error
                            SetStatus("Using generated stub data");
                        }
                    }
                    else
                    {
                        currentData = GenerateFakeData();
                        SetStatus("No stub JSON found. Created default data.");
                    }
                }

                // We set DataContext so that existing XAML bindings continue to work (if any)
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

                // Debug: print all fields in JSON
                currentData.DumpAllFields();

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
                
                if (PreviewHost != null)
                    PreviewHost.Content = overlayContainer;

                HookBlockClickHandlers(layout);
                SetStatus("Preview updated");
                
                // Also update the sample blocks in the preview panel if they exist
                UpdateDefaultPreview();
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
        
        private void UpdateDefaultPreview()
        {
            // Update default preview blocks with direct values
            try
            {
                if (currentData == null)
                    return;
                
                // Look for preview panels we can populate
                if (PreviewPanel != null && PreviewPanel.Children.Count > 0)
                {
                    foreach (var child in PreviewPanel.Children)
                    {
                        if (child is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Name))
                        {
                            UpdateTextBlock(tb, currentData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating preview panel: {ex.Message}");
            }
        }

        private FrameworkElement LoadLayoutFromText(string xamlText)
        {
            if (string.IsNullOrWhiteSpace(xamlText))
                throw new ArgumentException("No XAML provided");

            try
            {
                string fixedXaml = Helpers.XamlFixer.Preprocess(xamlText);
                using (var reader = new StringReader(fixedXaml))
                {
                    using (var xmlReader = XmlReader.Create(reader))
                    {
                        return (FrameworkElement)XamlReader.Load(xmlReader);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load XAML: {ex.Message}", ex);
            }
        }

        private void SetStatus(string message)
        {
            if (StatusBlock != null)
                StatusBlock.Text = message;
            Console.WriteLine($"Status: {message}");
        }

        // New method that maps TextBlock names to JSON fields by stripping trailing _N
        private void InjectStubData(FrameworkElement layout, RaceData data)
        {
            if (layout == null || data == null)
                return;
            
            Debug.WriteLine("Injecting stub data into layout");
            
            // Process all TextBlocks with names
            foreach (var textBlock in GetAllNamedTextBlocks(layout))
            {
                UpdateTextBlock(textBlock, data);
            }
            
            Debug.WriteLine("Finished injecting stub data");
        }
        
        private void UpdateTextBlock(Text
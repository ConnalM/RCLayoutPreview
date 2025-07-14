using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Document;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;

namespace RCLayoutPreview
{
    public partial class EditorWindow : Window
    {
        private string currentJsonPath;
        private JObject jsonData;
        private TextBlock statusLabel;
        private bool autoUpdateEnabled = true;
        private int previewDelayMilliseconds = 3000;
        private DateTime lastEditTime;
        private string lastEditorContent = string.Empty;
        private DispatcherTimer previewTimer;
        private MainWindow previewWindow;
        private SearchPanel searchPanel;

        public event EventHandler<string> XamlContentChanged;
        public event EventHandler<JObject> JsonDataChanged;

        public EditorWindow(MainWindow previewWindow)
        {
            InitializeComponent();
            this.previewWindow = previewWindow;
            statusLabel = FindName("StatusLabel") as TextBlock;

            // Set up editor
            Editor.ShowLineNumbers = true;
            Editor.TextChanged += Editor_TextChanged;
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");

            // Add search panel
            searchPanel = SearchPanel.Install(Editor);
            
            // Add keyboard shortcuts
            Editor.InputBindings.Add(new KeyBinding(
                ApplicationCommands.Find,
                new KeyGesture(Key.F, ModifierKeys.Control)));
            
            // Set up timer for automatic preview
            previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            previewTimer.Tick += AutoPreviewTick;
            previewTimer.Start();

            LoadStubData();

            // Set up drag and drop
            Editor.AllowDrop = true;
            Editor.Drop += Editor_Drop;
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (!autoUpdateEnabled) return;

            string currentContent = Editor.Text;
            if (currentContent != lastEditorContent && !string.IsNullOrWhiteSpace(currentContent))
            {
                lastEditTime = DateTime.Now;
                lastEditorContent = currentContent;
            }
        }

        private void AutoPreviewTick(object sender, EventArgs e)
        {
            if (!autoUpdateEnabled) return;

            if ((DateTime.Now - lastEditTime).TotalMilliseconds >= previewDelayMilliseconds)
            {
                string currentContent = Editor.Text;
                if (!string.IsNullOrWhiteSpace(currentContent))
                {
                    try
                    {
                        // Validate XML before sending
                        var doc = new XmlDocument();
                        doc.LoadXml(currentContent);
                        
                        XamlContentChanged?.Invoke(this, currentContent);
                        LogStatus("Preview updated");
                    }
                    catch (XmlException ex)
                    {
                        LogStatus($"Invalid XAML: {ex.Message}");
                    }
                }
            }
        }

        private void LoadStubData()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LogStatus($"Base directory: {baseDirectory}");

            string jsonPath = Path.Combine(baseDirectory, "stubdata5.json");
            LogStatus($"Checking path: {jsonPath}");

            if (File.Exists(jsonPath))
            {
                try
                {
                    currentJsonPath = jsonPath;
                    string jsonContent = File.ReadAllText(jsonPath);
                    jsonData = JObject.Parse(jsonContent);
                    LogStatus($"Loaded JSON: {Path.GetFileName(jsonPath)}");
                    PopulateJsonFieldsTree();
                    JsonDataChanged?.Invoke(this, jsonData);
                    return;
                }
                catch (Exception ex)
                {
                    LogStatus($"Error parsing JSON file: {ex.Message}");
                }
            }

            LogStatus("No JSON data files found.");
        }

        private void LogStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = message;
            }
            Console.WriteLine($"Status: {message}");
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
                try
                {
                    string xamlContent = File.ReadAllText(dlg.FileName);
                    Editor.Text = xamlContent;
                    LogStatus($"Loaded layout: {Path.GetFileName(dlg.FileName)}");
                    XamlContentChanged?.Invoke(this, xamlContent);
                }
                catch (Exception ex)
                {
                    LogStatus($"Error loading layout: {ex.Message}");
                }
            }
        }

        private void LoadJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Data (*.json)|*.json",
                Title = "Select JSON Data",
                FileName = "stubdata5.json"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    currentJsonPath = dlg.FileName;
                    string json = File.ReadAllText(dlg.FileName);
                    jsonData = JObject.Parse(json);
                    LogStatus($"Loaded: {Path.GetFileName(dlg.FileName)}");
                    PopulateJsonFieldsTree();
                    JsonDataChanged?.Invoke(this, jsonData);
                }
                catch (Exception ex)
                {
                    LogStatus($"Error loading JSON: {ex.Message}");
                }
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Editor.Text))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XAML Layout (*.xaml)|*.xaml",
                    Title = "Save Layout XAML"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, Editor.Text);
                    LogStatus($"Saved layout to: {Path.GetFileName(dlg.FileName)}");
                }
            }
            else
            {
                LogStatus("Nothing to save. Editor is empty.");
            }
        }

        private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
        {
            autoUpdateEnabled = (sender as CheckBox)?.IsChecked == true;
            if (autoUpdateEnabled)
            {
                lastEditTime = DateTime.Now;
                XamlContentChanged?.Invoke(this, Editor.Text);
            }
            LogStatus(autoUpdateEnabled ? "Auto-update enabled" : "Auto-update disabled");
        }

        private void DelayInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(DelayInput.Text, out var delay))
            {
                previewDelayMilliseconds = delay;
                LogStatus($"Preview delay updated to {previewDelayMilliseconds} ms.");
            }
            else
            {
                LogStatus("Invalid delay input. Please enter a valid number.");
            }
        }

        private void JsonFieldsTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            var selectedItem = treeView?.SelectedItem as TreeViewItem;
            if (selectedItem != null)
            {
                DragDrop.DoDragDrop(treeView, selectedItem.Header.ToString(), DragDropEffects.Copy);
            }
        }

        private void JsonFieldsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            var selectedItem = treeView?.SelectedItem as TreeViewItem;
            if (selectedItem != null)
            {
                var caretOffset = Editor.CaretOffset;
                Editor.Document.Insert(caretOffset, selectedItem.Header.ToString());
            }
        }

        private void Editor_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                var droppedText = e.Data.GetData(DataFormats.StringFormat) as string;
                var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor));
                if (pos.HasValue)
                {
                    var offset = Editor.Document.GetOffset(pos.Value.Line, pos.Value.Column);
                    Editor.Document.Insert(offset, droppedText);
                }
                else
                {
                    Editor.Document.Insert(Editor.CaretOffset, droppedText);
                }
            }
        }

        private void PopulateJsonFieldsTree()
        {
            if (jsonData == null) return;

            JsonFieldsTree.Items.Clear();

            foreach (var property in jsonData.Properties())
            {
                var groupItem = new TreeViewItem { Header = property.Name };
                if (property.Value is JObject groupObj)
                {
                    foreach (var field in groupObj.Properties())
                    {
                        var fieldItem = new TreeViewItem { Header = field.Name };
                        groupItem.Items.Add(fieldItem);
                    }
                }
                JsonFieldsTree.Items.Add(groupItem);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (searchPanel != null)
            {
                searchPanel.Uninstall();
                searchPanel = null;
            }
            base.OnClosed(e);
        }
    }
}
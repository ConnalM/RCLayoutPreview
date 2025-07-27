using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using Newtonsoft.Json.Linq;
using RCLayoutPreview.Helpers;
using RCLayoutPreview.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Windows.Controls.Primitives;

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
        private bool fieldDetected = false;
        private string currentXamlPath = null;
        private Popup completionPopup;
        private ListBox completionListBox;
        private List<string> xamlKeywords;
        private List<string> fieldNames;
        private List<string> allCompletions;
        private FoldingManager foldingManager;
        private XmlFoldingStrategy foldingStrategy;

        public event EventHandler<string> XamlContentChanged;
        public event EventHandler<JObject> JsonDataChanged;

        public EditorWindow(MainWindow previewWindow)
        {
            InitializeComponent();
            this.previewWindow = previewWindow;

            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Use helper for window placement
            if (!WindowPlacementHelper.RestoreWindowPlacement(this, "EditorWindow"))
            {
                this.Left = previewWindow.Left + previewWindow.Width + 20;
                this.Top = previewWindow.Top;
                this.Width = screenWidth * 0.34 - 20;
                this.Height = previewWindow.Height;
            }

            statusLabel = FindName("StatusLabel") as TextBlock;

            // Set up editor
            Editor.ShowLineNumbers = true;
            Editor.TextChanged += Editor_TextChanged;
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");

            // Add folding support
            foldingManager = FoldingManager.Install(Editor.TextArea);
            foldingStrategy = new XmlFoldingStrategy();
            UpdateFoldings();

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

            // Set up drag and drop for snippets
            Editor.AllowDrop = true;
            Editor.Drop += Editor_Drop;

            // Predictive text setup
            SetupPredictiveText();

            LoadStubData();
        }

        private void SetupPredictiveText()
        {
            // XAML keywords/tags
            xamlKeywords = new List<string> {
                "Window", "Grid", "StackPanel", "DockPanel", "Border", "TextBlock", "Label", "Button", "Image", "ItemsControl", "Viewbox", "Canvas", "UserControl", "Page", "ContentControl", "RowDefinition", "ColumnDefinition", "Background", "Foreground", "FontSize", "FontWeight", "HorizontalAlignment", "VerticalAlignment", "Margin", "Padding", "Width", "Height", "Name", "Content", "Text", "Source", "DataContext", "Binding"
            };

            // Load stubdata5.json field names
            fieldNames = new List<string>();
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stubdata5.json");
                if (File.Exists(jsonPath))
                {
                    var json = JObject.Parse(File.ReadAllText(jsonPath));
                    foreach (var group in json.Properties())
                    {
                        if (group.Value is JObject obj)
                        {
                            foreach (var prop in obj.Properties())
                                fieldNames.Add(prop.Name);
                        }
                    }
                }
            }
            catch { }

            allCompletions = new List<string>();
            allCompletions.AddRange(xamlKeywords);
            allCompletions.AddRange(fieldNames);
            allCompletions = allCompletions.Distinct().OrderBy(s => s).ToList();

            // Setup popup
            completionListBox = new ListBox
            {
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                MinWidth = 180,
                MaxHeight = 200
            };
            completionListBox.PreviewMouseLeftButtonUp += CompletionListBox_PreviewMouseLeftButtonUp;
            completionListBox.PreviewKeyDown += CompletionListBox_PreviewKeyDown;

            completionPopup = new Popup
            {
                PlacementTarget = Editor,
                Placement = PlacementMode.RelativePoint,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = completionListBox
            };

            Editor.TextArea.TextEntering += Editor_TextArea_TextEntering;
            Editor.TextArea.TextEntered += Editor_TextArea_TextEntered;
            Editor.TextArea.PreviewKeyDown += Editor_PreviewKeyDown;
        }

        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (completionPopup.IsOpen && (e.Key == Key.Down || e.Key == Key.Up))
            {
                completionListBox.Focus();
                if (completionListBox.Items.Count > 0)
                {
                    if (completionListBox.SelectedIndex < 0)
                        completionListBox.SelectedIndex = 0;
                }
                e.Handled = true;
            }
        }

        private void Editor_TextArea_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string word = GetCurrentWord();
            if (string.IsNullOrEmpty(word) || word.Length < 2)
            {
                completionPopup.IsOpen = false;
                return;
            }
            // Show suggestions that contain the word anywhere (not just at the start)
            var matches = allCompletions
                .Where(s => s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (matches.Count > 0)
            {
                completionListBox.ItemsSource = matches;
                completionListBox.SelectedIndex = 0;
                var caret = Editor.TextArea.Caret;
                var loc = Editor.TextArea.TextView.GetVisualPosition(caret.Position, ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom) + new System.Windows.Vector(0, 5);
                completionPopup.HorizontalOffset = loc.X;
                completionPopup.VerticalOffset = loc.Y;
                completionPopup.IsOpen = true;
                // Do not steal focus from the editor
            }
            else
            {
                completionPopup.IsOpen = false;
            }
        }

        private void Editor_TextArea_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (completionPopup.IsOpen && (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0])))
            {
                // Accept completion on non-word character
                InsertSelectedCompletion();
                completionPopup.IsOpen = false;
            }
        }

        private void CompletionListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            InsertSelectedCompletion();
            completionPopup.IsOpen = false;
            Editor.TextArea.Focus();
        }

        private void CompletionListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                InsertSelectedCompletion();
                completionPopup.IsOpen = false;
                Editor.TextArea.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                completionPopup.IsOpen = false;
                Editor.TextArea.Focus();
                e.Handled = true;
            }
        }

        private void InsertSelectedCompletion()
        {
            if (completionListBox.SelectedItem is string selected)
            {
                var word = GetCurrentWord();
                if (!string.IsNullOrEmpty(word))
                {
                    int offset = Editor.CaretOffset;
                    int start = offset - word.Length;
                    Editor.Document.Replace(start, word.Length, selected);
                    Editor.CaretOffset = start + selected.Length;
                }
            }
        }

        private string GetCurrentWord()
        {
            int offset = Editor.CaretOffset;
            if (offset == 0) return string.Empty;
            var text = Editor.Text.Substring(0, offset);
            var match = Regex.Match(text, "([A-Za-z0-9_]+)$");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (!autoUpdateEnabled) return;

            string currentContent = Editor.Text;
            if (currentContent != lastEditorContent && !string.IsNullOrWhiteSpace(currentContent))
            {
                lastEditTime = DateTime.Now;
                lastEditorContent = currentContent;

                // Update foldings
                UpdateFoldings();

                // Check for valid fields and notify if found for the first time
                CheckForValidFields(currentContent);
            }
        }

        private void UpdateFoldings()
        {
            if (foldingManager != null && foldingStrategy != null)
            {
                foldingStrategy.UpdateFoldings(foldingManager, Editor.Document);
            }
        }

        private void CheckForValidFields(string content)
        {
            // Check if the content contains valid fields
            if (PlaceholderSwapManager.ContainsValidField(content))
            {
                if (!fieldDetected)
                {
                    fieldDetected = true;
                    string fieldName = PlaceholderSwapManager.GetFirstFieldName(content);
                    string baseFieldName = RemoveFieldSuffix(fieldName); // <-- Use truncated field name
                    string message = PlaceholderSwapManager.GenerateFieldDetectedMessage(content);

                    // Update the status with a notification
                    LogStatus($"Field detected: {(string.IsNullOrEmpty(message) ? baseFieldName : message)}");

                    // If we're editing, update immediately rather than waiting for the timer
                    if (autoUpdateEnabled)
                    {
                        if (XamlValidationHelper.IsValidXml(content, out _))
                        {
                            XamlContentChanged?.Invoke(this, content);
                        }
                    }
                }
            }
            else
            {
                // Reset detection flag if no fields are found anymore
                fieldDetected = false;
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
                    if (XamlValidationHelper.IsValidXml(currentContent, out string error))
                    {
                        XamlContentChanged?.Invoke(this, currentContent);
                        LogStatus("Preview updated");
                    }
                    else
                    {
                        LogStatus($"Invalid XAML: {error}");
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
                    currentXamlPath = dlg.FileName;
                    UpdateWindowTitleWithFileName();
                    LogStatus($"Loaded layout: {Path.GetFileName(dlg.FileName)}");
                    XamlContentChanged?.Invoke(this, xamlContent);

                    // Check for valid fields in the loaded file
                    CheckForValidFields(xamlContent);
                }
                catch (Exception ex)
                {
                    LogStatus($"Error loading layout: {ex.Message}");
                }
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Editor.Text))
            {
                if (!string.IsNullOrEmpty(currentXamlPath))
                {
                    File.WriteAllText(currentXamlPath, Editor.Text);
                    LogStatus($"Saved layout to: {Path.GetFileName(currentXamlPath)}");
                    UpdateWindowTitleWithFileName();
                }
                else
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "XAML Layout (*.xaml)|*.xaml",
                        Title = "Save Layout XAML"
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        File.WriteAllText(dlg.FileName, Editor.Text);
                        currentXamlPath = dlg.FileName;
                        LogStatus($"Saved layout to: {Path.GetFileName(dlg.FileName)}");
                        UpdateWindowTitleWithFileName();
                    }
                }
            }
            else
            {
                LogStatus("Nothing to save. Editor is empty.");
            }
        }

        private void SaveAsLayout_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Editor.Text))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "XAML Layout (*.xaml)|*.xaml",
                    Title = "Save Layout As"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, Editor.Text);
                    currentXamlPath = dlg.FileName;
                    LogStatus($"Saved layout as: {Path.GetFileName(dlg.FileName)}");
                    UpdateWindowTitleWithFileName();
                }
            }
            else
            {
                LogStatus("Nothing to save. Editor is empty.");
            }
        }

        private void UpdateWindowTitleWithFileName()
        {
            if (!string.IsNullOrEmpty(currentXamlPath))
            {
                this.Title = $"RC Layout Editor - {Path.GetFileName(currentXamlPath)}";
            }
            else
            {
                this.Title = "RC Layout Editor";
            }
        }

        private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
        {
            autoUpdateEnabled = (sender as CheckBox)?.IsChecked == true;
            if (previewTimer != null)
            {
                if (autoUpdateEnabled)
                {
                    previewTimer.Start();
                    lastEditTime = DateTime.Now;
                    XamlContentChanged?.Invoke(this, Editor.Text);
                }
                else
                {
                    previewTimer.Stop();
                }
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
            var clickedItem = e.OriginalSource as DependencyObject;
            while (clickedItem != null && !(clickedItem is TreeViewItem))
                clickedItem = VisualTreeHelper.GetParent(clickedItem);

            if (clickedItem is TreeViewItem item)
            {
                item.IsSelected = true;
                DragDrop.DoDragDrop(item, item.Header.ToString(), DragDropEffects.Copy);
            }
        }

        private void JsonFieldsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            var selectedItem = treeView?.SelectedItem as TreeViewItem;
            if (selectedItem != null)
            {
                // Get the field name that was clicked
                string fieldName = selectedItem.Header.ToString();

                // Get the current position in the editor
                int caretOffset = Editor.CaretOffset;
                string actualFieldName = FormatFieldNameWithSuffix(fieldName);

                // If there is a selection, check if it matches a field name pattern
                if (Editor.SelectionLength > 0)
                {
                    string selectedText = Editor.SelectedText;
                    // Regex for RC field name pattern
                    var fieldNameRegex = new Regex(@"[A-Za-z0-9_]+_\d+");
                    if (fieldNameRegex.IsMatch(selectedText))
                    {
                        // Replace the selected field name with the new field name (with suffix)
                        Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, actualFieldName);
                        Editor.CaretOffset = Editor.SelectionStart + actualFieldName.Length;
                        LogStatus($"Replaced field name '{selectedText}' with '{actualFieldName}'");
                    }
                    else
                    {
                        // If not a field name, just insert at selection
                        Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, actualFieldName);
                        Editor.CaretOffset = Editor.SelectionStart + actualFieldName.Length;
                        LogStatus($"Inserted field name '{actualFieldName}' at selection");
                    }
                }
                else
                {
                    // Look for a placeholder nearby
                    string placeholder = FindNearestPlaceholder(Editor.Text, caretOffset);
                    if (!string.IsNullOrEmpty(placeholder))
                    {
                        // Replace the placeholder with the field name
                        ReplacePlaceholderWithFieldName(placeholder, actualFieldName);
                        LogStatus($"Replaced {placeholder} with {actualFieldName}");
                    }
                    else
                    {
                        // No selection and no placeholder, insert at caret position
                        Editor.Document.Insert(caretOffset, actualFieldName);
                        Editor.CaretOffset = caretOffset + actualFieldName.Length;
                        LogStatus($"Inserted field name '{actualFieldName}' at cursor");
                    }
                }

                // Check if this field triggers the placeholder removal
                CheckForValidFields(Editor.Text);
            }
        }

        private string FindNearestPlaceholder(string text, int caretOffset)
        {
            return PlaceholderHelper.FindNearestPlaceholder(text, caretOffset);
        }

        private void ReplacePlaceholderWithFieldName(string placeholder, string fieldName)
        {
            string newText = PlaceholderHelper.ReplacePlaceholderWithFieldName(Editor.Text, placeholder, fieldName);
            if (newText != Editor.Text)
            {
                int idx = newText.IndexOf($"Name=\"{fieldName}\"");
                Editor.Text = newText;
                if (idx >= 0)
                    Editor.CaretOffset = idx + ($"Name=\"{fieldName}\"").Length;
            }
        }

        private string FormatFieldNameWithSuffix(string fieldName)
        {
            // Scan the entire editor text for existing field names with suffixes
            string text = Editor.Text;
            // Regex to match fieldNam1e_1, fieldName1_2, etc.
            var suffixRegex = new Regex($@"{Regex.Escape(fieldName)}_(\d+)");
            var matches = suffixRegex.Matches(text);
            int maxSuffix = 0;
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int suffix))
                {
                    if (suffix > maxSuffix)
                        maxSuffix = suffix;
                }
            }
            // Next available suffix
            int nextSuffix = maxSuffix + 1;
            return $"{fieldName}_{nextSuffix}";
        }

        private void Editor_Drop(object sender, DragEventArgs e)
        {
            // Handle dropping layout snippets
            if (e.Data.GetDataPresent("LayoutSnippet"))
            {
                var snippet = e.Data.GetData("LayoutSnippet") as LayoutSnippet;
                if (snippet != null && SnippetGallery != null)
                {
                    // Process the snippet
                    string processedSnippet = SnippetGallery.ProcessSnippet(snippet);

                    // Get drop position
                    var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor));
                    int offset = pos.HasValue
                        ? Editor.Document.GetOffset(pos.Value.Line, pos.Value.Column)
                        : Editor.CaretOffset;

                    // Get the current indentation
                    string indentation = GetCurrentIndentation();

                    // Apply indentation to the snippet
                    if (!string.IsNullOrEmpty(indentation))
                    {
                        processedSnippet = ApplyIndentation(processedSnippet, indentation);
                    }

                    // Check if there's selected text to replace
                    if (Editor.SelectionLength > 0 && processedSnippet.Contains("{content}"))
                    {
                        string selectedText = Editor.SelectedText;
                        processedSnippet = processedSnippet.Replace("{content}", selectedText);
                        Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, processedSnippet);
                    }
                    else
                    {
                        Editor.Document.Insert(offset, processedSnippet);
                    }

                    LogStatus($"Inserted {snippet.Name} snippet");

                    // Check if this snippet triggers the placeholder removal
                    CheckForValidFields(Editor.Text);
                }
                return;
            }

            // Handle dropping regular strings
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

                // Check if this dropped text triggers the placeholder removal
                CheckForValidFields(Editor.Text);
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

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (!autoUpdateEnabled)
            {
                string currentContent = Editor.Text;
                if (!string.IsNullOrWhiteSpace(currentContent))
                {
                    if (XamlValidationHelper.IsValidXml(currentContent, out string error))
                    {
                        XamlContentChanged?.Invoke(this, currentContent);
                        LogStatus("Preview refreshed manually");
                    }
                    else
                    {
                        LogStatus($"Invalid XAML: {error}");
                    }
                }
            }
        }

        private void SaveWindowPlacement()
        {
            WindowPlacementHelper.SaveWindowPlacement(this, "EditorWindow");
        }

        private void CloseEditor_Click(object sender, RoutedEventArgs e)
        {
            SaveWindowPlacement();
            if (previewWindow != null)
            {
                // Save preview window placement before closing
                previewWindow.SaveWindowPlacementFromEditor();
                previewWindow.Close();
            }
            this.Close();
        }

        // Update OnClosed to only call SaveWindowPlacement
        protected override void OnClosed(EventArgs e)
        {
            SaveWindowPlacement();
            if (searchPanel != null)
            {
                searchPanel.Uninstall();
                searchPanel = null;
            }
            base.OnClosed(e);
        }

        /// <summary>
        /// Removes trailing _N (where N is an integer) from a field name, e.g. Name1_1 -> Name1.
        /// Used to get the base field name for stubdata lookup.
        /// </summary>
        /// <param name="fieldName">Field name with possible numeric suffix</param>
        /// <returns>Field name without trailing _N</returns>
        private string RemoveFieldSuffix(string fieldName)
        {
            return Regex.Replace(fieldName, @"_\d+$", "");
        }

        private void ClearEditor_Click(object sender, RoutedEventArgs e)
        {
            Editor.Clear();
            LogStatus("Editor cleared.");
        }

        private string GetCurrentIndentation()
        {
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            if (line == null) return string.Empty;
            string lineText = Editor.Document.GetText(line.Offset, Math.Min(line.Length, Editor.CaretOffset - line.Offset));
            return new string(lineText.TakeWhile(c => c == ' ' || c == '\t').ToArray());
        }

        private string ApplyIndentation(string text, string indentation)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 1; i < lines.Length; i++)
            {
                lines[i] = indentation + lines[i];
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
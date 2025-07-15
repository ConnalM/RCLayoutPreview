using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Document;
using Newtonsoft.Json.Linq;
using RCLayoutPreview.Helpers;
using RCLayoutPreview.Controls;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;

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

        public event EventHandler<string> XamlContentChanged;
        public event EventHandler<JObject> JsonDataChanged;
        public event EventHandler<string> ValidFieldDetected;

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

            // Set up drag and drop for snippets
            Editor.AllowDrop = true;
            Editor.Drop += Editor_Drop;

            LoadStubData();
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (!autoUpdateEnabled) return;

            string currentContent = Editor.Text;
            if (currentContent != lastEditorContent && !string.IsNullOrWhiteSpace(currentContent))
            {
                lastEditTime = DateTime.Now;
                lastEditorContent = currentContent;

                // Check for valid fields and notify if found for the first time
                CheckForValidFields(currentContent);
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
                    string message = PlaceholderSwapManager.GenerateFieldDetectedMessage(content);

                    // Update the status with a notification
                    LogStatus($"Field detected: {(string.IsNullOrEmpty(message) ? fieldName : message)}");

                    // If we're editing, update immediately rather than waiting for the timer
                    if (autoUpdateEnabled)
                    {
                        try
                        {
                            // Try to validate XML before sending an update
                            var doc = new XmlDocument();
                            doc.LoadXml(content);
                            XamlContentChanged?.Invoke(this, content);
                        }
                        catch (XmlException)
                        {
                            // Ignore XML errors during typing - the timer will handle them
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
                // Get the field name that was clicked
                string fieldName = selectedItem.Header.ToString();

                // Get the current position in the editor
                int caretOffset = Editor.CaretOffset;

                // Look for a placeholder nearby
                string placeholder = FindNearestPlaceholder(Editor.Text, caretOffset);
                if (!string.IsNullOrEmpty(placeholder))
                {
                    // Replace the placeholder with the field name
                    // Determine if we need to add position and instance suffix
                    string actualFieldName = FormatFieldNameWithSuffix(fieldName);

                    // Replace the placeholder with the field name
                    ReplacePlaceholderWithFieldName(placeholder, actualFieldName);
                    LogStatus($"Replaced {placeholder} with {actualFieldName}");
                }
                else
                {
                    // No placeholder found, just insert at caret position
                    Editor.Document.Insert(caretOffset, fieldName);
                    LogStatus($"Inserted field: {fieldName}");
                }

                // Check if this field triggers the placeholder removal
                CheckForValidFields(Editor.Text);
            }
        }

        private string FindNearestPlaceholder(string text, int caretOffset)
        {
            // This regex finds placeholders in the format Name="Placeholder1", Name="Placeholder2", etc.
            var placeholderRegex = new Regex(@"Name=""(Placeholder\d+)""");

            // Look for placeholders around the cursor position
            // First look in a reasonable range around the cursor (100 characters)
            int startPos = Math.Max(0, caretOffset - 100);
            int endPos = Math.Min(text.Length, caretOffset + 100);
            string searchText = text.Substring(startPos, endPos - startPos);

            // Find all placeholders in this range
            var matches = placeholderRegex.Matches(searchText);
            if (matches.Count == 0)
            {
                // No placeholders found in the vicinity
                return null;
            }

            // Find the closest placeholder to the cursor
            int cursorRelativePos = caretOffset - startPos;
            int closestDistance = int.MaxValue;
            string closestPlaceholder = null;

            foreach (Match match in matches)
            {
                int distance = Math.Abs(match.Index - cursorRelativePos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlaceholder = match.Groups[1].Value;
                }
            }

            return closestPlaceholder;
        }

        private string FormatFieldNameWithSuffix(string fieldName)
        {
            // Scan the entire editor text for existing field names with suffixes
            string text = Editor.Text;
            // Regex to match fieldName_1, fieldName_2, etc.
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

        private void ReplacePlaceholderWithFieldName(string placeholder, string fieldName)
        {
            // Find the placeholder in the text
            string pattern = $"Name=\"{placeholder}\"";
            string replacement = $"Name=\"{fieldName}\"";

            // Get the document text
            string text = Editor.Text;

            // Replace the first occurrence of the placeholder
            int placeholderIndex = text.IndexOf(pattern);
            if (placeholderIndex >= 0)
            {
                // Replace the placeholder
                Editor.Document.Replace(placeholderIndex, pattern.Length, replacement);

                // Move cursor to just after the replaced text
                Editor.CaretOffset = placeholderIndex + replacement.Length;
            }
        }

        private string GetCurrentIndentation()
        {
            // Get the current line
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            if (line == null) return string.Empty;

            // Extract text from the start of the line to the caret position
            string lineText = Editor.Document.GetText(line.Offset, Math.Min(line.Length, Editor.CaretOffset - line.Offset));

            // Extract only the whitespace at the beginning
            return new string(lineText.TakeWhile(c => c == ' ' || c == '\t').ToArray());
        }

        private string ApplyIndentation(string text, string indentation)
        {
            // Split the text by newlines
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Apply indentation to each line except the first one (which will inherit the caret's indentation)
            for (int i = 1; i < lines.Length; i++)
            {
                lines[i] = indentation + lines[i];
            }

            // Join the lines back together
            return string.Join(Environment.NewLine, lines);
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
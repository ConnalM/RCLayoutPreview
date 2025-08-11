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
        private int themeCheckCounter = 0; // Debug counter

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
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.StubDataFileName);
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
                    UpdateStatus($"Field detected: {(string.IsNullOrEmpty(message) ? baseFieldName : message)}");

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

            // Check for XAML preview updates
            if ((DateTime.Now - lastEditTime).TotalMilliseconds >= previewDelayMilliseconds)
            {
                string currentContent = Editor.Text;
                if (!string.IsNullOrWhiteSpace(currentContent))
                {
                    if (XamlValidationHelper.IsValidXml(currentContent, out string error))
                    {
                        XamlContentChanged?.Invoke(this, currentContent);
                        UpdateStatus("Preview updated");
                    }
                    else
                    {
                        UpdateStatus($"Invalid XAML: {error}");
                    }
                }
            }
            
            // Simple ThemeDictionary refresh check
            if (previewWindow != null)
            {
                CheckForThemeDictionaryChanges();
            }
        }
        
        private void CheckForThemeDictionaryChanges()
        {
            themeCheckCounter++;
            try
            {
                string themeFile = FindThemeDictionaryPath();
                UpdateStatus($"[DEBUG #{themeCheckCounter}] Checking ThemeDictionary: {themeFile}");
                
                if (!string.IsNullOrEmpty(themeFile) && File.Exists(themeFile))
                {
                    var fileInfo = new FileInfo(themeFile);
                    UpdateStatus($"[DEBUG #{themeCheckCounter}] File exists. Current: {fileInfo.LastWriteTime}, Last: {previewWindow.lastThemeDictionaryWriteTime}");
                    UpdateStatus($"[DEBUG #{themeCheckCounter}] Ticks comparison: Current: {fileInfo.LastWriteTime.Ticks}, Last: {previewWindow.lastThemeDictionaryWriteTime.Ticks}");
                    
                    // Simple check - if file is newer than last check, reload
                    if (fileInfo.LastWriteTime.Ticks != previewWindow.lastThemeDictionaryWriteTime.Ticks)
                    {
                        UpdateStatus($"[DEBUG #{themeCheckCounter}] CHANGE DETECTED! Triggering proper ThemeDictionary refresh...");
                        
                        // CRITICAL FIX: Call the proper ReloadThemeDictionary method instead of re-parsing XAML
                        previewWindow.ReloadThemeDictionary();
                        UpdateStatus("ThemeDictionary refreshed with FileStream method");
                    }
                    // Only show "unchanged" message every 10th check to avoid spam
                    else if (themeCheckCounter % 10 == 0)
                    {
                        UpdateStatus($"[DEBUG #{themeCheckCounter}] ThemeDictionary unchanged");
                    }
                }
                else
                {
                    UpdateStatus($"[DEBUG #{themeCheckCounter}] ThemeDictionary.xaml not found");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"[DEBUG #{themeCheckCounter}] Error: {ex.Message}");
            }
        }
        
        private string FindThemeDictionaryPath()
        {
            // Try multiple possible locations for ThemeDictionary.xaml
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: BaseDirectory = {baseDirectory}");
            
            string[] possiblePaths = {
                // 1. Same directory as executable (bin\Debug)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThemeDictionary.xaml"),
                // 2. Project root (go up from bin\Debug to project root)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "ThemeDictionary.xaml"),
                // 3. Direct project root path
                Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName, "ThemeDictionary.xaml"),
                // 4. Current working directory
                Path.Combine(Directory.GetCurrentDirectory(), "ThemeDictionary.xaml"),
                // 5. EXPLICIT PATH: The path from your workspace
                @"C:\Program Files (x86)\Race Coordinator\data\xaml\VS\RCLayoutPreview\ThemeDictionary.xaml"
            };
            
            UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: Checking {possiblePaths.Length} possible paths:");
            for (int i = 0; i < possiblePaths.Length; i++)
            {
                try
                {
                    string fullPath = Path.GetFullPath(possiblePaths[i]); // Resolve .. references
                    UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: Path {i + 1}: {fullPath}");
                    if (File.Exists(fullPath))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: FOUND at {fullPath}");
                        UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: File size: {fileInfo.Length} bytes, LastWriteTime: {fileInfo.LastWriteTime}");
                        
                        // Read first few lines to verify content
                        try
                        {
                            string[] lines = File.ReadAllLines(fullPath);
                            if (lines.Length > 0)
                            {
                                UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: First line: {lines[0]}");
                                
                                // Look for RSValueColor line to verify content
                                var rsValueLine = lines.FirstOrDefault(line => line.Contains("RSValueColor"));
                                if (!string.IsNullOrEmpty(rsValueLine))
                                {
                                    UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: RSValueColor line: {rsValueLine.Trim()}");
                                }
                            }
                        }
                        catch (Exception readEx)
                        {
                            UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: Error reading file: {readEx.Message}");
                        }
                        
                        return fullPath;
                    }
                    else
                    {
                        UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: NOT FOUND at {fullPath}");
                    }
                }
                catch (Exception ex) 
                {
                    UpdateStatus($"[DEBUG] EditorWindow FindThemeDictionary: Error checking path {i + 1}: {ex.Message}");
                }
            }
            
            UpdateStatus("[DEBUG] EditorWindow FindThemeDictionary: No ThemeDictionary.xaml found in any location");
            return null;
        }

        private void LoadStubData()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            UpdateStatus($"Base directory: {baseDirectory}");
            jsonData = StubDataService.LoadStubData(baseDirectory, message => UpdateStatus(message));
            if (jsonData != null)
            {
                currentJsonPath = Path.Combine(baseDirectory, AppConstants.StubDataFileName);
                PopulateJsonFieldsTree();
                JsonDataChanged?.Invoke(this, jsonData);
                return;
            }
            UpdateStatus("No JSON data files found.");
        }

        /// <summary>
        /// Updates the status message in the UI and logs to the console.
        /// /// <param name="message">Message to display</param>
        private void UpdateStatus(string message, bool isError = false)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = isError ? string.Empty : message;
            }

            var errorMessageControl = FindName("ErrorMessage") as TextBlock;
            if (errorMessageControl != null)
            {
                if (isError)
                {
                    errorMessageControl.Text = message;
                    errorMessageControl.Visibility = Visibility.Visible;
                }
                else
                {
                    errorMessageControl.Visibility = Visibility.Collapsed;
                }
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
                LoadLayoutFile(dlg.FileName);
            }
        }

        /// <summary>
        /// Loads a XAML layout file and adds it to recent files
        /// /// </summary>
        /// <param name="filePath">Path to the XAML file</param>
        private void LoadLayoutFile(string filePath)
        {
            try
            {
                string xamlContent = File.ReadAllText(filePath);
                Editor.Text = xamlContent;
                currentXamlPath = filePath;
                UpdateWindowTitleWithFileName();
                UpdateStatus($"Loaded layout: {Path.GetFileName(filePath)}");
                XamlContentChanged?.Invoke(this, xamlContent);

                // Add to recent files
                RecentFilesHelper.AddRecentFile(filePath);

                // Check for valid fields in the loaded file
                CheckForValidFields(xamlContent);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading layout: {ex.Message}");
                // Remove from recent files if it failed to load
                RecentFilesHelper.RemoveRecentFile(filePath);
            }
        }

        /// <summary>
        /// Handles Recent Files button click - shows the context menu
        /// /// </summary>
        private void RecentFiles_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                PopulateRecentFilesMenu();
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Populates the recent files context menu with current recent files
        /// /// </summary>
        private void PopulateRecentFilesMenu()
        {
            var recentFilesButton = FindName("RecentFilesButton") as Button;
            var contextMenu = recentFilesButton?.ContextMenu;
            
            if (contextMenu == null)
                return;
                
            contextMenu.Items.Clear();

            var recentFiles = RecentFilesHelper.GetRecentFilesInfo().ToList();

            if (recentFiles.Count == 0)
            {
                var noFilesItem = new MenuItem
                {
                    Header = "(No recent files)",
                    IsEnabled = false
                };
                contextMenu.Items.Add(noFilesItem);
                return;
            }

            // Add recent files
            for (int i = 0; i < recentFiles.Count; i++)
            {
                var fileInfo = recentFiles[i];
                var menuItem = new MenuItem
                {
                    Header = $"{i + 1}. {fileInfo.DisplayName}",
                    ToolTip = fileInfo.ToolTip,
                    Tag = fileInfo.FullPath
                };

                menuItem.Click += RecentFileMenuItem_Click;
                contextMenu.Items.Add(menuItem);
            }

            // Add separator and clear option
            if (recentFiles.Count > 0)
            {
                contextMenu.Items.Add(new Separator());
                
                var clearItem = new MenuItem
                {
                    Header = "Clear Recent Files",
                    FontStyle = FontStyles.Italic
                };
                clearItem.Click += ClearRecentFiles_Click;
                contextMenu.Items.Add(clearItem);
            }
        }

        /// <summary>
        /// Handles clicking on a recent file menu item
        /// /// </summary>
        private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
            {
                if (File.Exists(filePath))
                {
                    LoadLayoutFile(filePath);
                }
                else
                {
                    UpdateStatus($"File not found: {Path.GetFileName(filePath)}");
                    // Remove the missing file from recent files
                    RecentFilesHelper.RemoveRecentFile(filePath);
                    MessageBox.Show($"The file '{Path.GetFileName(filePath)}' could not be found and has been removed from the recent files list.",
                                  "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Handles clearing all recent files
        /// /// </summary>
        private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all recent files?", 
                                       "Clear Recent Files", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                RecentFilesHelper.ClearRecentFiles();
                UpdateStatus("Recent files cleared");
            }
        }

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Editor.Text))
            {
                if (!string.IsNullOrEmpty(currentXamlPath))
                {
                    File.WriteAllText(currentXamlPath, Editor.Text);
                    UpdateStatus($"Saved layout to: {Path.GetFileName(currentXamlPath)}");
                    UpdateWindowTitleWithFileName();
                    
                    // Add to recent files when saved
                    RecentFilesHelper.AddRecentFile(currentXamlPath);
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
                        UpdateStatus($"Saved layout to: {Path.GetFileName(dlg.FileName)}");
                        UpdateWindowTitleWithFileName();
                        
                        // Add to recent files when saved
                        RecentFilesHelper.AddRecentFile(currentXamlPath);
                    }
                }
            }
            else
            {
                UpdateStatus("Nothing to save. Editor is empty.");
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
                    UpdateStatus($"Saved layout as: {Path.GetFileName(dlg.FileName)}");
                    UpdateWindowTitleWithFileName();
                    
                    // Add to recent files when saved
                    RecentFilesHelper.AddRecentFile(currentXamlPath);
                }
            }
            else
            {
                UpdateStatus("Nothing to save. Editor is empty.");
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
            
            UpdateStatus(autoUpdateEnabled ? 
                "Auto-update enabled" : 
                "Auto-update disabled");
        }

        private void DelayInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(DelayInput.Text, out var delay))
            {
                previewDelayMilliseconds = delay;
                UpdateStatus($"Preview delay updated to {previewDelayMilliseconds} ms.");
            }
            else
            {
                UpdateStatus("Invalid delay input. Please enter a valid number.");
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

        private void JsonSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchBox = sender as TextBox;
            if (searchBox == null || jsonData == null) return;

            string searchText = searchBox.Text.ToLower();

            // Preserve expanded state of categories and their children
            var expandedState = new Dictionary<string, HashSet<string>>();
            foreach (TreeViewItem item in JsonFieldsTree.Items)
            {
                if (item.IsExpanded)
                {
                    var expandedChildren = new HashSet<string>();
                    foreach (TreeViewItem child in item.Items)
                    {
                        if (child.IsExpanded)
                        {
                            expandedChildren.Add(child.Header.ToString());
                        }
                    }
                    expandedState[item.Header.ToString()] = expandedChildren;
                }
            }

            JsonFieldsTree.Items.Clear();

            foreach (var property in jsonData.Properties())
            {
                var groupItem = new TreeViewItem { Header = property.Name };
                if (property.Value is JObject groupObj)
                {
                    foreach (var field in groupObj.Properties())
                    {
                        if (string.IsNullOrWhiteSpace(searchText) || field.Name.ToLower().Contains(searchText))
                        {
                            var fieldItem = new TreeViewItem { Header = field.Name };
                            groupItem.Items.Add(fieldItem);
                        }
                    }
                }
                if (groupItem.Items.Count > 0)
                {
                    JsonFieldsTree.Items.Add(groupItem);
                    // Restore expanded state for categories and their children
                    if (expandedState.TryGetValue(groupItem.Header.ToString(), out var expandedChildren))
                    {
                        groupItem.IsExpanded = true;
                        foreach (TreeViewItem child in groupItem.Items)
                        {
                            if (expandedChildren.Contains(child.Header.ToString()))
                            {
                                child.IsExpanded = true;
                            }
                        }
                    }
                }
            }
        }

        private void JsonFieldsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeView = sender as TreeView;
            var clickedItem = e.OriginalSource as DependencyObject;
            while (clickedItem != null && !(clickedItem is TreeViewItem))
                clickedItem = VisualTreeHelper.GetParent(clickedItem);

            if (clickedItem is TreeViewItem item)
            {
                // Toggle expansion for categories
                if (item.Items.Count > 0)
                {
                    item.IsExpanded = !item.IsExpanded;
                    e.Handled = true;
                    return;
                }

                // Handle field name insertion for leaf nodes
                string fieldName = item.Header.ToString();
                int caretOffset = Editor.CaretOffset;
                Editor.Document.Insert(caretOffset, fieldName);
                Editor.CaretOffset = caretOffset + fieldName.Length;
            }
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

                    UpdateStatus($"Inserted {snippet.Name} snippet");

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
            // Manual refresh of preview
            string currentContent = Editor.Text;
            if (!string.IsNullOrWhiteSpace(currentContent))
            {
                if (XamlValidationHelper.IsValidXml(currentContent, out string error))
                {
                    XamlContentChanged?.Invoke(this, currentContent);
                    UpdateStatus("Preview refreshed manually");
                }
                else
                {
                    UpdateStatus($"Invalid XAML: {error}");
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

        // Update OnClosed to ensure closing this window also closes the main window and the application
        protected override void OnClosed(EventArgs e)
        {
            SaveWindowPlacement();
            // Do NOT call previewWindow.Close() here to avoid InvalidOperationException
            if (searchPanel != null)
            {
                searchPanel.Uninstall();
                searchPanel = null;
            }
            base.OnClosed(e);
            // Ensure the application fully shuts down if this window is closed
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Removes trailing _N (where N is an integer) from a field name, e.g. Name1_1 -> Name1.
        /// Used to get the base field name for stubdata lookup.
        /// /// </summary>
        /// <param name="fieldName">Field name with possible numeric suffix</param>
        /// <returns>Field name without trailing _N</returns>
        private string RemoveFieldSuffix(string fieldName)
        {
            return Regex.Replace(fieldName, @"_\d+$", "");
        }

        private void ClearEditor_Click(object sender, RoutedEventArgs e)
        {
            Editor.Clear();
            UpdateStatus("Editor cleared.");
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

        private void JsonSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = sender as TextBox;
            if (searchBox != null && searchBox.Text == "Search JSON fields...")
            {
                searchBox.TextChanged -= JsonSearchBox_TextChanged; // Temporarily detach event
                searchBox.Text = string.Empty;
                searchBox.Foreground = Brushes.Black;
                searchBox.TextChanged += JsonSearchBox_TextChanged; // Reattach event
            }
        }

        private void JsonSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = sender as TextBox;
            if (searchBox != null && string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.TextChanged -= JsonSearchBox_TextChanged; // Temporarily detach event
                searchBox.Text = "Search JSON fields...";
                searchBox.Foreground = Brushes.Gray;
                searchBox.TextChanged += JsonSearchBox_TextChanged; // Reattach event
            }
        }

        // Allows MainWindow to control auto-update from diagnostics toggle
        public void SetAutoUpdateEnabled(bool enabled)
        {
            autoUpdateEnabled = enabled;
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
            
            UpdateStatus(enabled ? 
                "Auto-update enabled" : 
                "Auto-update disabled");
        }
    }
}
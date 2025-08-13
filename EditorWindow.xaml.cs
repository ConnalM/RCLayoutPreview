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
    /// <summary>
    /// Simple RelayCommand implementation for keyboard shortcuts
    /// /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

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

        // Error navigation tracking
        private int lastErrorPosition = -1;
        private string lastErrorMessage = "";
        private string lastErrorContext = "";

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

            // Add keyboard shortcuts for menu items
            AddKeyboardShortcuts();

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
            
            // Initialize Recent Files menu
            PopulateRecentFilesMenu();
        }

        /// <summary>
        /// Adds keyboard shortcuts for menu items
        /// /// </summary>
        private void AddKeyboardShortcuts()
        {
            // Existing shortcuts
            Editor.InputBindings.Add(new KeyBinding(
                ApplicationCommands.Find,
                new KeyGesture(Key.F, ModifierKeys.Control)));
                
            Editor.InputBindings.Add(new KeyBinding(
                ApplicationCommands.Replace,
                new KeyGesture(Key.H, ModifierKeys.Control)));
            
            // New shortcuts for File menu
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => LoadLayout_Click(null, null)),
                new KeyGesture(Key.O, ModifierKeys.Control)));
            
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => SaveLayout_Click(null, null)),
                new KeyGesture(Key.S, ModifierKeys.Control)));
                
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => SaveAsLayout_Click(null, null)),
                new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));

            // New shortcuts for Edit menu
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ClearEditor_Click(null, null)),
                new KeyGesture(Key.Delete, ModifierKeys.Control | ModifierKeys.Shift)));

            // New shortcuts for View menu
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => PreviewButton_Click(null, null)),
                new KeyGesture(Key.F5)));
                
            // New shortcuts for error navigation
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => GoToLastError_Click(null, null)),
                new KeyGesture(Key.F8)));

            // Handle Replace command
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Replace, Replace_Executed));
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
                        // Use enhanced validation for better error reporting
                        var element = XamlValidationHelper.ParseXamlWithPosition(content, out string error, out int errorPosition);
                        if (element != null)
                        {
                            XamlContentChanged?.Invoke(this, content);
                        }
                        // Don't show errors here as this is called frequently during typing
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
                    // Use enhanced validation with position information
                    var element = XamlValidationHelper.ParseXamlWithPosition(currentContent, out string error, out int errorPosition);
                    if (element != null)
                    {
                        XamlContentChanged?.Invoke(this, currentContent);
                        UpdateStatus("Preview updated");
                    }
                    else
                    {
                        // Show enhanced error with position
                        string enhancedError = XamlValidationHelper.CreateEnhancedErrorMessage(error, currentContent, errorPosition);
                        UpdateStatus($"Invalid XAML: {enhancedError}");
                        
                        // Optionally navigate to error position during auto-preview (less intrusive)
                        if (errorPosition >= 0)
                        {
                            try
                            {
                                var location = Editor.Document.GetLocation(errorPosition);
                                // Just show the position in status, don't navigate automatically during auto-preview
                                UpdateStatus($"Invalid XAML at Line {location.Line}, Column {location.Column}: {enhancedError}");
                            }
                            catch
                            {
                                UpdateStatus($"Invalid XAML: {enhancedError}");
                            }
                        }
                    }
                }
            }
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

                // Add to recent files and update menu
                RecentFilesHelper.AddRecentFile(filePath);
                PopulateRecentFilesMenu();

                // Check for valid fields in the loaded file
                CheckForValidFields(xamlContent);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading layout: {ex.Message}");
                // Remove from recent files if it failed to load
                RecentFilesHelper.RemoveRecentFile(filePath);
                PopulateRecentFilesMenu();
            }
        }

        /// <summary>
        /// Populates the recent files menu with current recent files
        /// /// </summary>
        private void PopulateRecentFilesMenu()
        {
            var recentFilesMenuItem = FindName("RecentFiles_1") as MenuItem;
            if (recentFilesMenuItem == null) return;

            recentFilesMenuItem.Items.Clear();

            var recentFiles = RecentFilesHelper.GetRecentFilesInfo().ToList();

            if (recentFiles.Count == 0)
            {
                var noFilesItem = new MenuItem
                {
                    Header = "(No recent files)",
                    IsEnabled = false
                };
                recentFilesMenuItem.Items.Add(noFilesItem);
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
                recentFilesMenuItem.Items.Add(menuItem);
            }

            // Add separator and clear option
            if (recentFiles.Count > 0)
            {
                recentFilesMenuItem.Items.Add(new Separator());

                var clearItem = new MenuItem
                {
                    Header = "Clear Recent Files",
                    FontStyle = FontStyles.Italic
                };
                clearItem.Click += ClearRecentFiles_Click;
                recentFilesMenuItem.Items.Add(clearItem);
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
                    // Remove the missing file from recent files and update menu
                    RecentFilesHelper.RemoveRecentFile(filePath);
                    PopulateRecentFilesMenu();
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
                PopulateRecentFilesMenu();
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

                    // Add to recent files when saved and update menu
                    RecentFilesHelper.AddRecentFile(currentXamlPath);
                    PopulateRecentFilesMenu();
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

                        // Add to recent files when saved and update menu
                        RecentFilesHelper.AddRecentFile(currentXamlPath);
                        PopulateRecentFilesMenu();
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

                    // Add to recent files when saved and update menu
                    RecentFilesHelper.AddRecentFile(currentXamlPath);
                    PopulateRecentFilesMenu();
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
                // Use enhanced validation with position information
                var element = XamlValidationHelper.ParseXamlWithPosition(currentContent, out string error, out int errorPosition);
                if (element != null)
                {
                    XamlContentChanged?.Invoke(this, currentContent);
                    UpdateStatus("Preview refreshed manually");
                }
                else
                {
                    // Show enhanced error and navigate to position for manual refresh
                    string enhancedError = XamlValidationHelper.CreateEnhancedErrorMessage(error, currentContent, errorPosition);
                    string context = XamlValidationHelper.GetErrorContext(currentContent, errorPosition);
                    
                    if (errorPosition >= 0)
                    {
                        NavigateToPosition(errorPosition, enhancedError, context);
                    }
                    else
                    {
                        ShowParsingError(enhancedError, context);
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

        private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowSearchReplaceDialog();
        }

        /// <summary>
        /// Shows the find dialog (called from toolbar button)
        /// /// </summary>
        private void ShowFindDialog(object sender, RoutedEventArgs e)
        {
            ShowSearchReplaceDialog();
        }

        /// <summary>
        /// Handles recent files toolbar button click
        /// /// </summary>
        private void RecentFilesToolbar_Click(object sender, RoutedEventArgs e)
        {
            // Create a context menu for recent files
            var contextMenu = new ContextMenu();
            var recentFiles = RecentFilesHelper.GetRecentFilesInfo().ToList();

            if (recentFiles.Count == 0)
            {
                var noFilesItem = new MenuItem
                {
                    Header = "(No recent files)",
                    IsEnabled = false
                };
                contextMenu.Items.Add(noFilesItem);
            }
            else
            {
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
                contextMenu.Items.Add(new Separator());

                var clearItem = new MenuItem
                {
                    Header = "Clear Recent Files",
                    FontStyle = FontStyles.Italic
                };
                clearItem.Click += ClearRecentFiles_Click;
                contextMenu.Items.Add(clearItem);
            }

            // Show context menu at toolbar button
            var button = sender as Button;
            if (button != null)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Find next occurrence of text in the editor
        /// /// </summary>
        private bool FindNext(string searchText, bool matchCase, bool wholeWord, ref int lastFindIndex)
        {
            if (string.IsNullOrEmpty(searchText)) return false;

            string text = Editor.Text;
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int startIndex = (lastFindIndex >= 0) ? lastFindIndex + 1 : Editor.CaretOffset;
            int foundIndex = -1;

            if (wholeWord)
            {
                foundIndex = FindWholeWord(text, searchText, startIndex, comparison);
                if (foundIndex == -1 && startIndex > 0)
                {
                    foundIndex = FindWholeWord(text, searchText, 0, comparison);
                }
            }
            else
            {
                foundIndex = text.IndexOf(searchText, startIndex, comparison);
                if (foundIndex == -1 && startIndex > 0)
                {
                    foundIndex = text.IndexOf(searchText, 0, comparison);
                }
            }

            if (foundIndex >= 0)
            {
                Editor.Select(foundIndex, searchText.Length);
                Editor.ScrollToLine(Editor.Document.GetLineByOffset(foundIndex).LineNumber);
                lastFindIndex = foundIndex;
                return true;
            }

            lastFindIndex = -1;
            return false;
        }

        /// <summary>
        /// Find whole word occurrences
        /// /// </summary>
        private int FindWholeWord(string text, string searchText, int startIndex, StringComparison comparison)
        {
            int index = startIndex;
            while ((index = text.IndexOf(searchText, index, comparison)) >= 0)
            {
                bool isWholeWord = (index == 0 || !char.IsLetterOrDigit(text[index - 1])) &&
                                   (index + searchText.Length >= text.Length || !char.IsLetterOrDigit(text[index + searchText.Length]));
                
                if (isWholeWord) return index;
                index++;
            }
            return -1;
        }

        /// <summary>
        /// Replace next occurrence
        /// /// </summary>
        private bool ReplaceNext(string searchText, string replaceText, bool matchCase, bool wholeWord, ref int lastFindIndex)
        {
            if (string.IsNullOrEmpty(searchText)) return false;

            // If current selection matches search text, replace it
            if (!string.IsNullOrEmpty(Editor.SelectedText))
            {
                bool matches;
                if (wholeWord)
                {
                    matches = matchCase 
                        ? Editor.SelectedText == searchText 
                        : Editor.SelectedText.Equals(searchText, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    matches = matchCase 
                        ? Editor.SelectedText == searchText 
                        : Editor.SelectedText.Equals(searchText, StringComparison.OrdinalIgnoreCase);
                }

                if (matches)
                {
                    int selectionStart = Editor.SelectionStart;
                    Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, replaceText ?? "");
                    lastFindIndex = selectionStart + (replaceText?.Length ?? 0) - 1;
                    
                    // Find next occurrence
                    FindNext(searchText, matchCase, wholeWord, ref lastFindIndex);
                    return true;
                }
            }

            // Find and select next occurrence
            return FindNext(searchText, matchCase, wholeWord, ref lastFindIndex);
        }

        /// <summary>
        /// Replace all occurrences
        /// /// </summary>
        private int ReplaceAll(string searchText, string replaceText, bool matchCase, bool wholeWord)
        {
            if (string.IsNullOrEmpty(searchText)) return 0;

            string text = Editor.Text;
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            int count = 0;
            int index = 0;
            
            Editor.BeginChange();
            try
            {
                while (index < text.Length)
                {
                    int foundIndex;
                    if (wholeWord)
                    {
                        foundIndex = FindWholeWord(text, searchText, index, comparison);
                    }
                    else
                    {
                        foundIndex = text.IndexOf(searchText, index, comparison);
                    }

                    if (foundIndex == -1) break;

                    Editor.Document.Replace(foundIndex, searchText.Length, replaceText ?? "");
                    text = Editor.Text; // Refresh text after replacement
                    index = foundIndex + (replaceText?.Length ?? 0);
                    count++;
                }
            }
            finally
            {
                Editor.EndChange();
            }

            return count;
        }

        /// <summary>
        /// Shows a comprehensive search and replace dialog for the XAML editor
        /// /// </summary>
        private void ShowSearchReplaceDialog()
        {
            var dialog = new Window
            {
                Title = "Find and Replace",
                Width = 450,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            // Find section
            var findLabel = new Label { Content = "Find:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(findLabel, 0); Grid.SetColumn(findLabel, 0);
            grid.Children.Add(findLabel);

            var findTextBox = new TextBox { Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(findTextBox, 0); Grid.SetColumn(findTextBox, 1);
            grid.Children.Add(findTextBox);

            var findButton = new Button { Content = "Find Next", Margin = new Thickness(5), Height = 25 };
            Grid.SetRow(findButton, 0); Grid.SetColumn(findButton, 2);
            grid.Children.Add(findButton);

            // Replace section
            var replaceLabel = new Label { Content = "Replace:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(replaceLabel, 1); Grid.SetColumn(replaceLabel, 0);
            grid.Children.Add(replaceLabel);

            var replaceTextBox = new TextBox { Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(replaceTextBox, 1); Grid.SetColumn(replaceLabel, 1);
            grid.Children.Add(replaceTextBox);

            var replaceButton = new Button { Content = "Replace", Margin = new Thickness(5), Height = 25 };
            Grid.SetRow(replaceButton, 1); Grid.SetColumn(replaceButton, 2);
            grid.Children.Add(replaceButton);

            // Options section
            var optionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            Grid.SetRow(optionsPanel, 2); Grid.SetColumn(optionsPanel, 1);
            
            var matchCaseCheckBox = new CheckBox { Content = "Match case", Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };
            var wholeWordCheckBox = new CheckBox { Content = "Whole word", VerticalAlignment = VerticalAlignment.Center };
            
            optionsPanel.Children.Add(matchCaseCheckBox);
            optionsPanel.Children.Add(wholeWordCheckBox);
            grid.Children.Add(optionsPanel);

            // Button section
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5, 15, 5, 5) };
            Grid.SetRow(buttonPanel, 3); Grid.SetColumn(buttonPanel, 1); Grid.SetColumnSpan(buttonPanel, 2);

            var replaceAllButton = new Button { Content = "Replace All", Width = 80, Margin = new Thickness(5, 0, 5, 0) };
            var closeButton = new Button { Content = "Close", Width = 80, Margin = new Thickness(5, 0, 0, 0) };

            buttonPanel.Children.Add(replaceAllButton);
            buttonPanel.Children.Add(closeButton);
            grid.Children.Add(buttonPanel);

            // Status section
            var statusLabel = new Label { Content = "Ready", Foreground = Brushes.Gray, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetRow(statusLabel, 4); Grid.SetColumn(statusLabel, 1);
            grid.Children.Add(statusLabel);

            dialog.Content = grid;

            // Pre-fill with selected text if any
            if (!string.IsNullOrEmpty(Editor.SelectedText))
            {
                findTextBox.Text = Editor.SelectedText;
                replaceTextBox.Focus();
            }
            else
            {
                findTextBox.Focus();
            }

            // Search state
            int lastFindIndex = -1;

            // Event handlers
            findButton.Click += (s, e) =>
            {
                if (FindNext(findTextBox.Text, matchCaseCheckBox.IsChecked == true, wholeWordCheckBox.IsChecked == true, ref lastFindIndex))
                {
                    statusLabel.Content = $"Found at position {lastFindIndex}";
                    statusLabel.Foreground = Brushes.Green;
                }
                else
                {
                    statusLabel.Content = $"'{findTextBox.Text}' not found";
                    statusLabel.Foreground = Brushes.Red;
                }
            };

            replaceButton.Click += (s, e) =>
            {
                if (ReplaceNext(findTextBox.Text, replaceTextBox.Text, matchCaseCheckBox.IsChecked == true, wholeWordCheckBox.IsChecked == true, ref lastFindIndex))
                {
                    statusLabel.Content = $"Replaced at position {lastFindIndex}";
                    statusLabel.Foreground = Brushes.Blue;
                }
                else
                {
                    statusLabel.Content = "Nothing to replace";
                    statusLabel.Foreground = Brushes.Orange;
                }
            };

            replaceAllButton.Click += (s, e) =>
            {
                int count = ReplaceAll(findTextBox.Text, replaceTextBox.Text, matchCaseCheckBox.IsChecked == true, wholeWordCheckBox.IsChecked == true);
                statusLabel.Content = $"Replaced {count} occurrence(s)";
                statusLabel.Foreground = count > 0 ? Brushes.Blue : Brushes.Orange;
                UpdateStatus($"Replaced {count} occurrences");
            };

            closeButton.Click += (s, e) => dialog.Close();

            // Keyboard shortcuts
            findTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    findButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    dialog.Close();
                    e.Handled = true;
                }
            };

            replaceTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    replaceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    dialog.Close();
                    e.Handled = true;
                }
            };

            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    dialog.Close();
                    e.Handled = true;
                }
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// Navigates to a specific position in the editor and shows error information
        /// /// </summary>
        /// <param name="position">Character position to navigate to</param>
        /// <param name="errorMessage">Error message to display</param>
        /// <param name="context">Error context to display</param>
        public void NavigateToPosition(int position, string errorMessage, string context)
        {
            try
            {
                // Store error information for "Go to Last Error" functionality
                lastErrorPosition = position;
                lastErrorMessage = errorMessage;
                lastErrorContext = context;

                // Ensure position is within bounds
                position = Math.Max(0, Math.Min(position, Editor.Document.TextLength - 1));

                // Get line and column from position
                var location = Editor.Document.GetLocation(position);
                
                // Navigate to the position
                Editor.CaretOffset = position;
                Editor.ScrollToLine(location.Line);
                
                // Select a small area around the error to highlight it
                int selectionStart = Math.Max(0, position - 10);
                int selectionEnd = Math.Min(Editor.Document.TextLength, position + 10);
                Editor.Select(selectionStart, selectionEnd - selectionStart);

                // Show error information
                string positionInfo = $"Line {location.Line}, Column {location.Column}";
                string fullMessage = $"XAML Parsing Error at {positionInfo}:\n{errorMessage}";
                
                if (!string.IsNullOrEmpty(context))
                {
                    fullMessage += $"\n\nContext: {context}";
                }

                UpdateStatus(fullMessage, true);

                // Also show a message box for immediate attention
                MessageBox.Show(fullMessage, "XAML Parsing Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Focus the editor
                Editor.Focus();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error navigating to position {position}: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a parsing error when position mapping is not available
        /// /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="context">Error context</param>
        public void ShowParsingError(string errorMessage, string context)
        {
            // Store error information even when position is unknown
            lastErrorPosition = -1;
            lastErrorMessage = errorMessage;
            lastErrorContext = context;

            string fullMessage = $"XAML Parsing Error:\n{errorMessage}";
            
            if (!string.IsNullOrEmpty(context))
            {
                fullMessage += $"\n\nContext: {context}";
            }

            UpdateStatus(fullMessage, true);

            // Show message box
            MessageBox.Show(fullMessage, "XAML Parsing Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Navigates to the last known error position
        /// /// </summary>
        private void GoToLastError_Click(object sender, RoutedEventArgs e)
        {
            if (lastErrorPosition >= 0 && lastErrorPosition < Editor.Document.TextLength)
            {
                try
                {
                    var location = Editor.Document.GetLocation(lastErrorPosition);
                    
                    // Navigate to the position
                    Editor.CaretOffset = lastErrorPosition;
                    Editor.ScrollToLine(location.Line);
                    
                    // Select a small area around the error
                    int selectionStart = Math.Max(0, lastErrorPosition - 10);
                    int selectionEnd = Math.Min(Editor.Document.TextLength, lastErrorPosition + 10);
                    Editor.Select(selectionStart, selectionEnd - selectionStart);

                    // Show error information
                    string positionInfo = $"Line {location.Line}, Column {location.Column}";
                    string message = $"Last Error Location: {positionInfo}";
                    if (!string.IsNullOrEmpty(lastErrorMessage))
                    {
                        message += $"\nError: {lastErrorMessage}";
                    }
                    if (!string.IsNullOrEmpty(lastErrorContext))
                    {
                        message += $"\nContext: {lastErrorContext}";
                    }

                    UpdateStatus(message);
                    Editor.Focus();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error navigating to last error: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(lastErrorMessage))
            {
                // Show last error message even if position is not available
                UpdateStatus($"Last Error: {lastErrorMessage}");
                MessageBox.Show($"Last Error:\n{lastErrorMessage}\n\n{lastErrorContext}", 
                    "Last XAML Error", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                UpdateStatus("No recent errors to navigate to.");
                MessageBox.Show("No recent XAML parsing errors found.", 
                    "Go to Last Error", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
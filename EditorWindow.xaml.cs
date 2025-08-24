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
using ICSharpCode.AvalonEdit.Rendering;

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
        
        // Naming warning tracking (stored but not immediately displayed)
        private int lastNamingWarningPosition = -1;
        private string lastNamingWarningMessage = "";

        public event EventHandler<string> XamlContentChanged;
        public event EventHandler<JObject> JsonDataChanged;
        public event EventHandler<string> SelectedElementChanged;

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

            // Initialize WordWrap state to match the checkbox (default is True)
            Editor.WordWrap = true;
            Editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;

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
            Editor.PreviewDrop += Editor_PreviewDrop;
            Editor.DragEnter += (s, e) => LogStatus("Editor_DragEnter triggered");

            // Predictive text setup
            SetupPredictiveText();

            LoadStubData();
            
            // Initialize Recent Files menu
            PopulateRecentFilesMenu();
            
            // Initialize error button state
            UpdateErrorButtonState();

            Editor.TextArea.SelectionChanged += Editor_SelectionChanged;
            Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        }

        private void Caret_PositionChanged(object sender, EventArgs e)
        {
            if (completionPopup != null && completionPopup.IsOpen)
            {
                PositionCompletionPopup();
            }
        }

        /// <summary>
        /// Adds keyboard shortcuts for menu items
        /// /// </summary>
        private void AddKeyboardShortcuts()
        {
            // Remove any default Replace command bindings from Editor
            Editor.InputBindings.Clear();
            Editor.CommandBindings.Clear();

            // Add Find and Replace shortcuts to Editor
            Editor.InputBindings.Add(new KeyBinding(
                ApplicationCommands.Find,
                new KeyGesture(Key.F, ModifierKeys.Control)));
            Editor.InputBindings.Add(new KeyBinding(
                ApplicationCommands.Replace,
                new KeyGesture(Key.H, ModifierKeys.Control)));
            Editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Replace, Replace_Executed));
            Editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (s, e) => ShowSearchReplaceDialog()));

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

        private void ShowCompletionPopupAtCaret()
        {
            var caret = Editor.TextArea.Caret;
            var textView = Editor.TextArea.TextView;
            if (!textView.IsLoaded || !textView.IsArrangeValid)
            {
                textView.VisualLinesChanged += TextView_VisualLinesChanged_ShowCompletion;
                return;
            }
            PositionCompletionPopup();
            if (!completionPopup.IsOpen)
            {
                completionPopup.IsOpen = true;
                textView.VisualLinesChanged += TextView_VisualLinesChanged_RepositionCompletion;
            }
            // Debug logging
            UpdateStatus($"[CompletionPopup] Show at caret: Offset={Editor.CaretOffset} IsOpen={completionPopup.IsOpen}");
            Console.WriteLine($"[CompletionPopup] Show at caret: Offset={Editor.CaretOffset} IsOpen={completionPopup.IsOpen}");
        }

        private void PositionCompletionPopup()
{
    var caret = Editor.TextArea.Caret;
    var textView = Editor.TextArea.TextView;

    var visualLoc = textView.GetVisualPosition(caret.Position, VisualYPosition.LineBottom);
    var screenLoc = textView.PointToScreen(visualLoc);
    var relativeLoc = completionPopup.PlacementTarget.PointFromScreen(screenLoc);

    completionPopup.PlacementTarget = Editor.TextArea; // or Editor
    completionPopup.Placement = PlacementMode.RelativePoint;
    completionPopup.HorizontalOffset = relativeLoc.X;
    completionPopup.VerticalOffset = relativeLoc.Y;

    // Debug logging
    UpdateStatus($"[CompletionPopup] Position: Visual=({visualLoc.X},{visualLoc.Y}) Screen=({screenLoc.X},{screenLoc.Y}) Relative=({relativeLoc.X},{relativeLoc.Y}) IsOpen={completionPopup.IsOpen}");
    Console.WriteLine($"[CompletionPopup] Position: Visual=({visualLoc.X},{visualLoc.Y}) Screen=({screenLoc.X},{screenLoc.Y}) Relative=({relativeLoc.X},{relativeLoc.Y}) IsOpen={completionPopup.IsOpen}");
}

        private void TextView_VisualLinesChanged_ShowCompletion(object sender, EventArgs e)
        {
            var textView = Editor.TextArea.TextView;
            textView.VisualLinesChanged -= TextView_VisualLinesChanged_ShowCompletion;
            ShowCompletionPopupAtCaret();
        }

        private void TextView_VisualLinesChanged_RepositionCompletion(object sender, EventArgs e)
        {
            if (completionPopup.IsOpen)
            {
                PositionCompletionPopup();
            }
            else
            {
                var textView = Editor.TextArea.TextView;
                textView.VisualLinesChanged -= TextView_VisualLinesChanged_RepositionCompletion;
            }
        }

        private void CloseCompletionPopup()
        {
            if (completionPopup.IsOpen)
            {
                completionPopup.IsOpen = false;
                var textView = Editor.TextArea.TextView;
                textView.VisualLinesChanged -= TextView_VisualLinesChanged_RepositionCompletion;
            }
        }

        private void Editor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string word = GetCurrentWord();
                if (string.IsNullOrEmpty(word) || word.Length < 2)
                {
                    CloseCompletionPopup();
                }
                else
                {
                    // Filter completions by current word
                    var filtered = allCompletions.Where(s => s.StartsWith(word, StringComparison.OrdinalIgnoreCase)).ToList();
                    completionListBox.ItemsSource = filtered;
                    if (filtered.Count > 0)
                    {
                        ShowCompletionPopupAtCaret();
                    }
                    else
                    {
                        CloseCompletionPopup();
                    }
                }
            }), DispatcherPriority.Input);
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
                    string formattedFieldName = FormatFieldNameWithSuffix(selected);
                    Editor.Document.Replace(start, word.Length, formattedFieldName);
                    Editor.CaretOffset = start + formattedFieldName.Length;
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
                        
                        // Clear any previous error information on successful parsing
                        ClearErrorState();
                    }
                    else
                    {
                        // Show enhanced error with position
                        string enhancedError = XamlValidationHelper.CreateEnhancedErrorMessage(error, currentContent, errorPosition);
                        string context = XamlValidationHelper.GetErrorContext(currentContent, errorPosition);
                        
                        // Store error information for later navigation
                        StoreErrorInformation(errorPosition, enhancedError, context);
                        
                        if (errorPosition >= 0)
                        {
                            try
                            {
                                var location = Editor.Document.GetLocation(errorPosition);
                                // Show the error in status with line/column information
                                string statusMessage = $"XAML Error at Line {location.Line}, Column {location.Column}: {enhancedError}";
                                UpdateStatus(statusMessage, true);
                                
                                // Also show in preview window's error popup (less intrusive than message box)
                                if (previewWindow != null)
                                {
                                    previewWindow.ShowErrorPopup($"XAML Error at Line {location.Line}, Column {location.Column}:\n{enhancedError}\n\nPress F8 to navigate to error location.");
                                }
                            }
                            catch
                            {
                                UpdateStatus($"Invalid XAML: {enhancedError}", true);
                                
                                // Show in preview window's error popup
                                if (previewWindow != null)
                                {
                                    previewWindow.ShowErrorPopup($"XAML Parsing Error:\n{enhancedError}\n\nPress F8 in editor to see more details.");
                                }
                            }
                        }
                        else
                        {
                            UpdateStatus($"Invalid XAML: {enhancedError}", true);
                            
                            // Show in preview window's error popup
                            if (previewWindow != null)
                            {
                                previewWindow.ShowErrorPopup($"XAML Parsing Error:\n{enhancedError}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stores error information for later navigation without showing intrusive dialogs
        /// /// </summary>
        private void StoreErrorInformation(int errorPosition, string errorMessage, string context)
        {
            lastErrorPosition = errorPosition;
            lastErrorMessage = errorMessage;
            lastErrorContext = context;
            
            // Update the error button state
            UpdateErrorButtonState();
        }

        /// <summary>
        /// Clears error state when XAML parsing succeeds
        /// /// </summary>
        private void ClearErrorState()
        {
            // Don't clear the error information immediately, in case user wants to navigate to last error
            // lastErrorPosition = -1;
            // lastErrorMessage = "";
            // lastErrorContext = "";
            
            // Update the error button state
            UpdateErrorButtonState();
        }

        /// <summary>
        /// Checks if there's a stored error position or naming warning that can be navigated to
        /// /// </summary>
        /// <returns>True if there's an error or warning that can be navigated to</returns>
        public bool HasErrorToNavigate()
        {
            return lastErrorPosition >= 0 || !string.IsNullOrEmpty(lastErrorMessage) || 
                   lastNamingWarningPosition >= 0 || !string.IsNullOrEmpty(lastNamingWarningMessage);
        }

        /// <summary>
        /// Updates the visual state of the error button based on available error information
        /// /// </summary>
        private void UpdateErrorButtonState()
        {
            try
            {
                var errorButton = FindName("GoToErrorButton") as Button;
                if (errorButton != null)
                {
                    bool hasError = HasErrorToNavigate();
                    if (hasError)
                    {
                        errorButton.IsEnabled = true;
                        errorButton.Opacity = 1.0;
                        
                        // Prioritize errors over warnings in the tooltip
                        if (lastErrorPosition >= 0)
                        {
                            errorButton.ToolTip = $"Go to Last Error - Line {Editor.Document.GetLocation(lastErrorPosition).Line} (F8)";
                        }
                        else if (lastNamingWarningPosition >= 0)
                        {
                            errorButton.ToolTip = $"Go to Naming Warning - Line {Editor.Document.GetLocation(lastNamingWarningPosition).Line} (F8)";
                        }
                        else if (!string.IsNullOrEmpty(lastErrorMessage))
                        {
                            errorButton.ToolTip = "Show Last Error (F8)";
                        }
                        else
                        {
                            errorButton.ToolTip = "Show Naming Warning (F8)";
                        }
                    }
                    else
                    {
                        errorButton.IsEnabled = false;
                        errorButton.Opacity = 0.5;
                        errorButton.ToolTip = "No recent errors or warnings (F8)";
                    }
                }
            }
            catch
            {
                // Error button state update is optional, don't crash if it fails
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

            int retries = 3;
            bool success = false;
            string errorMsg = string.Empty;

            while (retries > 0 && !success)
            {
                try
                {
                    if (dlg.ShowDialog() == true)
                    {
                        LoadLayoutFile(dlg.FileName);
                        success = true; // If load is successful, set success to true
                    }
                    else
                    {
                        success = true; // If dialog is canceled, consider it successful (do nothing)
                    }
                }
                catch (IOException ioEx)
                {
                    retries--;
                    errorMsg = $"I/O error while opening file: {ioEx.Message}.\nRetries left: {retries}";
                    UpdateStatus(errorMsg, true);
                    System.Threading.Thread.Sleep(1000); // Wait before retrying
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    retries = 0; // Do not retry on permission errors
                    errorMsg = $"Access denied: {uaEx.Message}";
                    UpdateStatus(errorMsg, true);
                }
                catch (Exception ex)
                {
                    retries = 0;
                    errorMsg = $"Unexpected error: {ex.Message}";
                    UpdateStatus(errorMsg, true);
                }
            }

            // If still unsuccessful after retries, show error message
            if (!success && !string.IsNullOrEmpty(errorMsg))
            {
                MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>
        /// Handles the Word Wrap toggle change to enable/disable horizontal scrolling
        /// </summary>
        private void WordWrapToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (Editor == null) return;

            // Get the WordWrapToggle checkbox from the XAML
            var wordWrapToggle = FindName("WordWrapToggle") as CheckBox;
            bool wordWrapEnabled = wordWrapToggle?.IsChecked == true;
            
            // When word wrap is enabled, hide horizontal scroll bar
            // When word wrap is disabled, show horizontal scroll bar
            Editor.WordWrap = wordWrapEnabled;
            Editor.HorizontalScrollBarVisibility = wordWrapEnabled ? 
                System.Windows.Controls.ScrollBarVisibility.Hidden : 
                System.Windows.Controls.ScrollBarVisibility.Auto;
            
            UpdateStatus(wordWrapEnabled ? 
                "Word wrap enabled - horizontal scroll bar hidden" : 
                "Word wrap disabled - horizontal scroll bar available");
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

        private void LogStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = message;
            }
            Console.WriteLine($"Status: {message}");
        }

        private void JsonFieldsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LogStatus("JsonFieldsTree_MouseDoubleClick triggered");

            var treeView = sender as TreeView;
            if (treeView == null) return;

            // Get the mouse position relative to the TreeView
            Point mousePos = e.GetPosition(treeView);
            // Get the element under the mouse
            var hitTestResult = treeView.InputHitTest(mousePos) as DependencyObject;
            while (hitTestResult != null && !(hitTestResult is TreeViewItem))
                hitTestResult = VisualTreeHelper.GetParent(hitTestResult);

            var treeViewItem = hitTestResult as TreeViewItem;
            if (treeViewItem != null)
            {
                // Only insert if this is a leaf node (no children)
                if (treeViewItem.Items.Count == 0)
                {
                    string fieldName = treeViewItem.Header.ToString();
                    LogStatus($"Double-clicked field name: {fieldName}");

                    int caretOffset = Editor.CaretOffset;
                    LogStatus($"Caret offset: {caretOffset}");

                    string actualFieldName = FormatFieldNameWithSuffix(fieldName);
                    LogStatus($"Formatted field name: {actualFieldName}");

                    if (Editor.SelectionLength > 0)
                    {
                        string selectedText = Editor.SelectedText;
                        LogStatus($"Selected text: {selectedText}");

                        var fieldNameRegex = new Regex(@"[A-Za-z0-9_]+_\d+");
                        if (fieldNameRegex.IsMatch(selectedText))
                        {
                            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, actualFieldName);
                            Editor.CaretOffset = Editor.SelectionStart + actualFieldName.Length;
                            LogStatus($"Replaced field name '{selectedText}' with '{actualFieldName}'");
                        }
                        else
                        {
                            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, actualFieldName);
                            Editor.CaretOffset = Editor.SelectionStart + actualFieldName.Length;
                            LogStatus($"Inserted field name '{actualFieldName}' at selection");
                        }
                    }
                    else
                    {
                        string placeholder = FindNearestPlaceholder(Editor.Text, caretOffset);
                        LogStatus($"Nearest placeholder: {placeholder}");

                        if (!string.IsNullOrEmpty(placeholder))
                        {
                            ReplacePlaceholderWithFieldName(placeholder, actualFieldName);
                            LogStatus($"Replaced {placeholder} with {actualFieldName}");
                        }
                        else
                        {
                            Editor.Document.Insert(caretOffset, actualFieldName);
                            Editor.CaretOffset = caretOffset + actualFieldName.Length;
                            LogStatus($"Inserted field name '{actualFieldName}' at cursor");
                        }
                    }

                    // Check if this field triggers the placeholder removal
                    CheckForValidFields(Editor.Text);
                    LogStatus("Editor content validated for fields");
                }
                else
                {
                    // If it's a group header, just expand/collapse, do not insert anything
                    treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
                    LogStatus("Double-clicked group header, toggled expand/collapse");
                }
            }
            else
            {
                LogStatus("No valid item selected in TreeView");
            }
            e.Handled = true;
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
            LogStatus($"FormatFieldNameWithSuffix triggered for field name: {fieldName}");

            // Scan the entire editor text for existing field names with suffixes
            string text = Editor.Text;
            LogStatus($"Editor text length: {text.Length}");

            // Regex to match fieldName_1, fieldName_2, etc.
            var suffixRegex = new Regex($"{Regex.Escape(fieldName)}_(\\d+)");
            var matches = suffixRegex.Matches(text);
            LogStatus($"Number of matches found: {matches.Count}");

            int maxSuffix = 0;
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int suffix))
                {
                    LogStatus($"Found suffix: {suffix}");
                    if (suffix > maxSuffix)
                        maxSuffix = suffix;
                }
            }

            // Next available suffix
            int nextSuffix = maxSuffix + 1;
            string formattedFieldName = $"{fieldName}_{nextSuffix}";
            LogStatus($"Formatted field name with suffix: {formattedFieldName}");

            return formattedFieldName;
        }

        private void Editor_Drop(object sender, DragEventArgs e)
        {
            LogStatus("Editor_Drop triggered");

            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                var droppedText = e.Data.GetData(DataFormats.StringFormat) as string;
                LogStatus($"Dropped text: {droppedText}");

                var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor));
                int offset = pos.HasValue
                    ? Editor.Document.GetOffset(pos.Value.Line, pos.Value.Column)
                    : Editor.CaretOffset;
                LogStatus($"Drop position offset: {offset}");

                // Format the dropped text with the instance number suffix
                string formattedFieldName = FormatFieldNameWithSuffix(droppedText);
                LogStatus($"Formatted field name: {formattedFieldName}");

                // Look for a placeholder nearby
                string placeholder = FindNearestPlaceholder(Editor.Text, offset);
                LogStatus($"Nearest placeholder: {placeholder}");

                if (!string.IsNullOrEmpty(placeholder))
                {
                    // Replace the placeholder with the field name
                    ReplacePlaceholderWithFieldName(placeholder, formattedFieldName);
                    LogStatus($"Replaced {placeholder} with {formattedFieldName}");
                }
                else
                {
                    // No placeholder, insert at drop position
                    Editor.Document.Insert(offset, formattedFieldName);
                    Editor.CaretOffset = offset + formattedFieldName.Length;
                    LogStatus($"Inserted field name '{formattedFieldName}' at drop position");
                }

                // Validate the editor content for fields
                CheckForValidFields(Editor.Text);
                LogStatus("Editor content validated for fields");

                // Refresh preview
                XamlContentChanged?.Invoke(this, Editor.Text);
                // Queue highlight after UI update
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    previewWindow.HighlightPreviewElement(formattedFieldName);
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else
            {
                LogStatus("No valid data present in drag event");
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
                    
                    // Clear any previous error information on successful parsing
                    ClearErrorState();
                }
                else
                {
                    // Show enhanced error and navigate to position for manual refresh
                    string enhancedError = XamlValidationHelper.CreateEnhancedErrorMessage(error, currentContent, errorPosition);
                    string context = XamlValidationHelper.GetErrorContext(currentContent, errorPosition);
                    
                    // Store error information
                    StoreErrorInformation(errorPosition, enhancedError, context);
                    
                    if (errorPosition >= 0)
                    {
                        // For manual refresh, show detailed error with navigation
                        NavigateToPosition(errorPosition, enhancedError, context);
                    }
                    else
                    {
                        // Show error without navigation
                        ShowParsingError(enhancedError, context);
                    }
                    
                    // Also show in preview window's error popup
                    if (previewWindow != null)
                    {
                        var location = Editor.Document.GetLocation(Math.Max(0, errorPosition));
                        previewWindow.ShowErrorPopup($"XAML Error at Line {location.Line}, Column {location.Column}:\n{enhancedError}");
                    }
                }
            }
            else
            {
                UpdateStatus("Cannot preview: Editor content is empty.");
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
                Height = 210,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(10, 5, 10, 10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Find row
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Replace row
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Options row
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Button row
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status row
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); // Label column
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TextBox column
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Button column

            // Find row
            var findLabel = new Label { Content = "Find:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,0) };
            Grid.SetRow(findLabel, 0); Grid.SetColumn(findLabel, 0);
            grid.Children.Add(findLabel);

            var findTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(findTextBox, 0); Grid.SetColumn(findTextBox, 1);
            grid.Children.Add(findTextBox);

            var findButton = new Button { Content = "Find Next", Margin = new Thickness(5), Height = 25, MinWidth = 90 };
            Grid.SetRow(findButton, 0); Grid.SetColumn(findButton, 2);
            grid.Children.Add(findButton);

            // Replace row
            var replaceLabel = new Label { Content = "Replace:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,0) };
            Grid.SetRow(replaceLabel, 1); Grid.SetColumn(replaceLabel, 0);
            grid.Children.Add(replaceLabel);

            var replaceTextBox = new TextBox { Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(replaceTextBox, 1); Grid.SetColumn(replaceTextBox, 1);
            grid.Children.Add(replaceTextBox);

            var replaceButton = new Button { Content = "Replace", Margin = new Thickness(5), Height = 25, MinWidth = 90 };
            Grid.SetRow(replaceButton, 1); Grid.SetColumn(replaceButton, 2);
            grid.Children.Add(replaceButton);

            // Options section
            var optionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 8, 5, 8) };
            var matchCaseCheckBox = new CheckBox { Content = "Match case", Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };
            var wholeWordCheckBox = new CheckBox { Content = "Whole word", VerticalAlignment = VerticalAlignment.Center };
            optionsPanel.Children.Add(matchCaseCheckBox);
            optionsPanel.Children.Add(wholeWordCheckBox);
            Grid.SetRow(optionsPanel, 2); Grid.SetColumn(optionsPanel, 0); Grid.SetColumnSpan(optionsPanel, 3);
            grid.Children.Add(optionsPanel);

            // Button section
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5, 10, 5, 5) };
            var replaceAllButton = new Button { Content = "Replace All", Width = 80, Margin = new Thickness(5, 0, 5, 0) };
            var closeButton = new Button { Content = "Close", Width = 80, Margin = new Thickness(5, 0, 0, 0) };
            buttonPanel.Children.Add(replaceAllButton);
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 3); Grid.SetColumn(buttonPanel, 0); Grid.SetColumnSpan(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            // Status section
            var statusLabel = new Label { Content = "Ready", Foreground = Brushes.Gray, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetRow(statusLabel, 4); Grid.SetColumn(statusLabel, 0); Grid.SetColumnSpan(statusLabel, 3);
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

                // Focus the editor window first
                this.Activate();
                this.Focus();
                Editor.Focus();

                // Ensure position is within bounds
                position = Math.Max(0, Math.Min(position, Editor.Document.TextLength - 1));

                // Get line and column from position
                var location = Editor.Document.GetLocation(position);
                
                // Navigate to the position
                Editor.CaretOffset = position;
                Editor.ScrollToLine(location.Line);
                
                // Select a wider area around the error to make it more visible
                int selectionStart = Math.Max(0, position - 15);
                int selectionEnd = Math.Min(Editor.Document.TextLength, position + 30);
                Editor.Select(selectionStart, selectionEnd - selectionStart);

                // Show error information in status
                string positionInfo = $"Line {location.Line}, Column {location.Column}";
                string statusMessage = $"Error at {positionInfo}: {errorMessage}";
                UpdateStatus(statusMessage, true);

                // Update error button state
                UpdateErrorButtonState();
                
                // Flash the error area to draw attention
                FlashErrorSelection();

                // Show a less intrusive message (no popup during auto-navigation)
                
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

            // Update error button state
            UpdateErrorButtonState();
        }

        /// <summary>
        /// Navigates to the last known error position (public method for external calls)
        /// </summary>
        public void GoToLastError()
        {
            GoToLastError_Click(null, null);
        }

        /// <summary>
        /// Navigates to the last known error position
        /// </summary>
        private void GoToLastError_Click(object sender, RoutedEventArgs e)
        {
            // Prioritize errors over warnings - errors are more critical
            if (lastErrorPosition >= 0 && lastErrorPosition < Editor.Document.TextLength)
            {
                try
                {
                    // Focus the editor window first
                    this.Activate();
                    this.Focus();
                    Editor.Focus();

                    var location = Editor.Document.GetLocation(lastErrorPosition);
                    
                    // Navigate to the position
                    Editor.CaretOffset = lastErrorPosition;
                    Editor.ScrollToLine(location.Line);
                    
                    // Select a wider area around the error to make it more visible
                    int selectionStart = Math.Max(0, lastErrorPosition - 10);
                    int selectionEnd = Math.Min(Editor.Document.TextLength, lastErrorPosition + 25);
                    Editor.Select(selectionStart, selectionEnd - selectionStart);

                    // Show error information in status
                    string positionInfo = $"Line {location.Line}, Column {location.Column}";
                    string message = $"Navigated to Error at {positionInfo}";
                    if (!string.IsNullOrEmpty(lastErrorMessage))
                    {
                        message += $": {lastErrorMessage}";
                    }

                    UpdateStatus(message);
                    
                    // Flash the error area to draw attention
                    FlashErrorSelection();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error navigating to last error: {ex.Message}");
                }
            }
            else if (lastNamingWarningPosition >= 0 && lastNamingWarningPosition < Editor.Document.TextLength)
            {
                // Navigate to naming warning if no error is available
                ShowNamingWarning(lastNamingWarningPosition, lastNamingWarningMessage);
            }
            else if (!string.IsNullOrEmpty(lastErrorMessage))
            {
                // Focus the window for dialog display
                this.Activate();
                
                // Show last error message even if position is not available
                UpdateStatus($"Last Error: {lastErrorMessage}");
                MessageBox.Show($"Last Error:\n{lastErrorMessage}\n\n{lastErrorContext}", 
                    "Last XAML Error", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!string.IsNullOrEmpty(lastNamingWarningMessage))
            {
                // Focus the window for dialog display
                this.Activate();
                
                // Show last naming warning
                UpdateStatus($"Last Naming Warning: {lastNamingWarningMessage}");
                MessageBox.Show($"Naming Pattern Suggestion:\n{lastNamingWarningMessage}\n\nThis is a suggestion to improve code consistency.", 
                    "Naming Pattern Warning", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Focus the window for dialog display
                this.Activate();
                
                UpdateStatus("No recent errors or warnings to navigate to.");
                MessageBox.Show("No recent XAML parsing errors or naming warnings found.", 
                    "Go to Last Error", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Shows a naming pattern warning in the editor at a specific position
        /// /// </summary>
        /// <param name="position">Position of the naming issue</param>
        /// <param name="warningMessage">Warning message to display</param>
        public void ShowNamingWarning(int position, string warningMessage)
        {
            try
            {
                // Focus the editor window first
                this.Activate();
                this.Focus();
                Editor.Focus();

                // Ensure position is within bounds
                position = Math.Max(0, Math.Min(position, Editor.Document.TextLength - 1));

                // Get line and column from position
                var location = Editor.Document.GetLocation(position);
                
                // Navigate to the position
                Editor.CaretOffset = position;
                Editor.ScrollToLine(location.Line);
                
                // Select the Name attribute to highlight the issue
                int selectionStart = position;
                int selectionEnd = Math.Min(Editor.Document.TextLength, position + 20); // Select Name="..." part
                
                // Try to find the end of the Name attribute
                string text = Editor.Document.GetText(position, Math.Min(50, Editor.Document.TextLength - position));
                var nameMatch = Regex.Match(text, @"Name\s*=\s*""[^""]*""");
                if (nameMatch.Success)
                {
                    selectionEnd = position + nameMatch.Length;
                }
                
                Editor.Select(selectionStart, selectionEnd - selectionStart);

                // Show warning information in status with orange coloring
                string positionInfo = $"Line {location.Line}, Column {location.Column}";
                string statusMessage = $"Naming Warning at {positionInfo}: {warningMessage}";
                UpdateStatus(statusMessage);
                
                // Create a subtle flash effect for warnings (different from error flash)
                FlashWarningSelection();

            }
            catch (Exception ex)
            {
                UpdateStatus($"Error showing naming warning at position {position}: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores a naming pattern warning for potential F8 navigation without immediately displaying it
        /// This prevents workflow disruption while still allowing on-demand navigation
        /// /// </summary>
        /// <param name="position">Position of the naming issue</param>
        /// <param name="warningMessage">Warning message</param>
        public void StoreNamingWarning(int position, string warningMessage)
        {
            lastNamingWarningPosition = position;
            lastNamingWarningMessage = warningMessage;
            
            // Update error button to show that there's something to navigate to
            UpdateErrorButtonState();
        }

        /// <summary>
        /// Creates a subtle visual flash effect for warning highlights (orange)
        /// </summary>
        private void FlashWarningSelection()
        {
            try
            {
                var originalBackground = Editor.TextArea.Background;
                var flashBrush = new SolidColorBrush(Colors.Orange) { Opacity = 0.15 }; // Orange and more subtle
                
                // Flash effect - use orange to indicate warning (not error)
                Editor.TextArea.Background = flashBrush;
                
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)  // Shorter flash for warnings
                };
                timer.Tick += (s, e) =>
                {
                    Editor.TextArea.Background = originalBackground;
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // Flash effect is optional, don't crash if it fails
            }
        }

        /// <summary>
        /// Creates a visual flash effect on the selected error area
        /// </summary>
        private void FlashErrorSelection()
        {
            try
            {
                var originalBackground = Editor.TextArea.Background;
                var flashBrush = new SolidColorBrush(Colors.Red) { Opacity = 0.2 };
                
                // Flash effect - use red to make it more noticeable
                Editor.TextArea.Background = flashBrush;
                
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(800)  // Longer flash duration
                };
                timer.Tick += (s, e) =>
                {
                    Editor.TextArea.Background = originalBackground;
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // Flash effect is optional, don't crash if it fails
            }
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            string elementName = null;
            int caret = Editor.CaretOffset;
            string text = Editor.Text;

            // Ensure caret is within bounds
            if (caret < 0 || caret > text.Length)
            {
                UpdateStatus("Caret position is out of bounds.");
                return;
            }

            // Find the start of the tag containing the caret
            int tagStart = caret > 0 ? text.LastIndexOf('<', caret - 1) : -1;
            int tagEnd = caret < text.Length ? text.IndexOf('>', caret) : -1;

            if (tagStart >= 0 && tagEnd > tagStart)
            {
                string tagText = text.Substring(tagStart, tagEnd - tagStart + 1);
                var nameMatch = Regex.Match(tagText, "Name=\"([^\"]+)\"");
                if (nameMatch.Success)
                {
                    elementName = nameMatch.Groups[1].Value;
                }
            }

            // Fallback: use previous logic if not found
            if (elementName == null)
            {
                string selectedText = Editor.SelectedText;
                var nameMatch = Regex.Match(selectedText, "Name=\"([^\"]+)\"");
                if (nameMatch.Success)
                {
                    elementName = nameMatch.Groups[1].Value;
                }
                else
                {
                    int searchStart = Math.Max(0, caret - 200);
                    int searchEnd = Math.Min(text.Length, caret + 200);
                    string context = text.Substring(searchStart, searchEnd - searchStart);
                    var contextMatch = Regex.Match(context, "Name=\"([^\"]+)\"");
                    if (contextMatch.Success)
                    {
                        elementName = contextMatch.Groups[1].Value;
                    }
                    else
                    {
                        var tagMatch = Regex.Match(selectedText, "<([a-zA-Z0-9_]+)");
                        if (tagMatch.Success)
                        {
                            elementName = tagMatch.Groups[1].Value;
                        }
                    }
                }
            }
            UpdateStatus($"Selection changed, extracted element name: {elementName}");
            SelectedElementChanged?.Invoke(this, elementName);
        }

        private void ShowSearchReplaceDialog(object sender, RoutedEventArgs e)
        {
            ShowSearchReplaceDialog();
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

        private void Editor_TextArea_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (completionPopup.IsOpen && (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0])))
            {
                InsertSelectedCompletion();
                completionPopup.IsOpen = false;
            }
        }

        private void Editor_PreviewDrop(object sender, DragEventArgs e)
        {
            LogStatus("Editor_PreviewDrop logic executing");
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                var droppedText = e.Data.GetData(DataFormats.StringFormat) as string;
                LogStatus($"PreviewDrop: Dropped text: {droppedText}");

                var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor));
                int offset = pos.HasValue
                    ? Editor.Document.GetOffset(pos.Value.Line, pos.Value.Column)
                    : Editor.CaretOffset;
                LogStatus($"PreviewDrop: Drop position offset: {offset}");

                string formattedFieldName = FormatFieldNameWithSuffix(droppedText);
                LogStatus($"PreviewDrop: Formatted field name: {formattedFieldName}");

                string placeholder = FindNearestPlaceholder(Editor.Text, offset);
                LogStatus($"PreviewDrop: Nearest placeholder: {placeholder}");

                if (!string.IsNullOrEmpty(placeholder))
                {
                    // Replace the placeholder with the field name
                    ReplacePlaceholderWithFieldName(placeholder, formattedFieldName);
                    LogStatus($"PreviewDrop: Replaced {placeholder} with {formattedFieldName}");
                }
                else
                {
                    Editor.Document.Insert(offset, formattedFieldName);
                    Editor.CaretOffset = offset + formattedFieldName.Length;
                    Dispatcher.BeginInvoke(new Action(() => {
                        Editor.Select(Editor.CaretOffset, 0); // Clear selection after drop
                    }), DispatcherPriority.ApplicationIdle);
                    LogStatus($"PreviewDrop: Inserted field name '{formattedFieldName}' at drop position");
                }

                // Validate the editor content for fields
                CheckForValidFields(Editor.Text);
                LogStatus("PreviewDrop: Editor content validated for fields");

                XamlContentChanged?.Invoke(this, Editor.Text);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    previewWindow.HighlightPreviewElement(formattedFieldName);
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                e.Handled = true;
            }
            else
            {
                LogStatus("PreviewDrop: No valid data present in drag event");
            }
        }

        private void VersionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show($"Application Version: {version}", "Version", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
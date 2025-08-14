using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using RCLayoutPreview.Helpers;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using ICSharpCode.AvalonEdit;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace RCLayoutPreview.Controls
{
    /// <summary>
    /// Interaction logic for SnippetGallery.xaml
    /// </summary>
    public partial class SnippetGallery : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<LayoutSnippet> snippets;
        private ICollectionView snippetsView;
        private string searchText;

        private TextBox searchBox;
        private ItemsControl snippetsList;
        private TextEditor editor;
        
        // Interactive tooltip popup system
        private Popup detailPopup;
        private DispatcherTimer showTimer;
        private DispatcherTimer hideTimer;
        private FrameworkElement currentHoverElement;

        public event PropertyChangedEventHandler PropertyChanged;

        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText != value)
                {
                    searchText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
                    UpdateFilter();
                }
            }
        }

        public SnippetGallery()
        {
            InitializeComponent();
            
            // Initialize interactive popup system
            InitializeInteractiveTooltips();
            
            // Initialize controls after XAML loading
            Loaded += (s, e) => {
                searchBox = FindName("SearchBox") as TextBox;
                snippetsList = FindName("SnippetsList") as ItemsControl;
                
                if (searchBox != null)
                    searchBox.TextChanged += SearchBox_TextChanged;
                
                // Try to find the editor in our parent window
                if (Window.GetWindow(this) is EditorWindow editorWindow)
                {
                    editor = editorWindow.FindName("Editor") as TextEditor;
                }
                
                LoadSnippets();
                DataContext = this;
            };
            
            // Cleanup when unloading
            Unloaded += (s, e) => {
                showTimer?.Stop();
                hideTimer?.Stop();
                if (detailPopup != null)
                {
                    detailPopup.IsOpen = false;
                }
            };
        }

        /// <summary>
        /// Initializes the interactive tooltip system using Popup instead of ToolTip
        /// </summary>
        private void InitializeInteractiveTooltips()
        {
            // Create popup for interactive tooltips
            detailPopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Placement = PlacementMode.Right,
                StaysOpen = true, // This is key - allows interaction
                HorizontalOffset = 10,
                VerticalOffset = -50
            };
            
            // Create timers for showing/hiding
            showTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800) // Show after 800ms hover
            };
            showTimer.Tick += ShowTimer_Tick;
            
            hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // Hide after 500ms outside
            };
            hideTimer.Tick += HideTimer_Tick;
        }

        private void LoadSnippets()
        {
            if (snippetsList == null) return;
            
            snippets = new ObservableCollection<LayoutSnippet>(LayoutSnippet.GetDefaultSnippets());
            
            snippetsView = CollectionViewSource.GetDefaultView(snippets);
            snippetsView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            
            snippetsList.ItemsSource = snippetsView;
        }

        private void UpdateFilter()
        {
            if (snippetsView == null) return;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                snippetsView.Filter = null;
                return;
            }

            string searchLower = searchText.ToLower();
            snippetsView.Filter = item =>
            {
                if (item is LayoutSnippet snippet)
                {
                    return snippet.Name.ToLower().Contains(searchLower) ||
                           snippet.Description.ToLower().Contains(searchLower) ||
                           snippet.Category.ToLower().Contains(searchLower);
                }
                return false;
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                SearchText = textBox.Text;
        }

        private void SnippetItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Mouse.OverrideCursor = Cursors.Hand;
                
                // Start interactive tooltip system
                currentHoverElement = element;
                hideTimer.Stop();
                showTimer.Start();
            }
        }

        private void SnippetItem_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
            
            // Handle tooltip hiding with delay
            showTimer.Stop();
            if (detailPopup.IsOpen)
            {
                hideTimer.Start();
            }
        }

        /// <summary>
        /// Shows the interactive tooltip popup after hover delay
        /// </summary>
        private void ShowTimer_Tick(object sender, EventArgs e)
        {
            showTimer.Stop();
            
            if (currentHoverElement?.DataContext is LayoutSnippet snippet)
            {
                ShowInteractiveTooltip(snippet, currentHoverElement);
            }
        }

        /// <summary>
        /// Hides the interactive tooltip popup after leave delay
        /// </summary>
        private void HideTimer_Tick(object sender, EventArgs e)
        {
            hideTimer.Stop();
            
            // Check if mouse is over the popup itself
            if (detailPopup.Child is FrameworkElement popupContent)
            {
                var mousePosition = Mouse.GetPosition(popupContent);
                var bounds = new Rect(0, 0, popupContent.ActualWidth, popupContent.ActualHeight);
                
                if (!bounds.Contains(mousePosition))
                {
                    detailPopup.IsOpen = false;
                }
                else
                {
                    // Mouse is over popup, check again later
                    hideTimer.Start();
                }
            }
            else
            {
                detailPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// Creates and shows an interactive tooltip popup with scrollable XAML content
        /// </summary>
        /// <param name="snippet">Layout snippet to show details for</param>
        /// <param name="targetElement">Element to position popup relative to</param>
        private void ShowInteractiveTooltip(LayoutSnippet snippet, FrameworkElement targetElement)
        {
            // Create the popup content
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.3
                }
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            // Header with snippet name
            var nameBlock = new TextBlock
            {
                Text = snippet.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(nameBlock, 0);
            
            // Description
            var descBlock = new TextBlock
            {
                Text = snippet.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(descBlock, 1);
            
            // Scrollable XAML content - THIS IS THE KEY IMPROVEMENT
            var scrollViewer = new ScrollViewer
            {
                MinWidth = 400,
                MinHeight = 200,
                MaxHeight = 400,
                MaxWidth = 800,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };
            
            var xamlText = new TextBlock
            {
                Text = snippet.XamlTemplate,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                Padding = new Thickness(8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 250))
            };
            
            scrollViewer.Content = xamlText;
            Grid.SetRow(scrollViewer, 2);
            
            grid.Children.Add(nameBlock);
            grid.Children.Add(descBlock);
            grid.Children.Add(scrollViewer);
            border.Child = grid;
            
            // Set up popup properties
            detailPopup.PlacementTarget = targetElement;
            detailPopup.Child = border;
            
            // Handle mouse events on the popup to keep it open
            border.MouseEnter += (s, e) => hideTimer.Stop();
            border.MouseLeave += (s, e) => hideTimer.Start();
            
            detailPopup.IsOpen = true;
        }

        private void SnippetItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check for double-click
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                if (sender is FrameworkElement element && element.DataContext is LayoutSnippet snippet && editor != null)
                {
                    InsertSnippet(snippet);
                    e.Handled = true;
                }
            }
            // For single click, start drag operation
            else if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Left)
            {
                if (sender is FrameworkElement element && element.DataContext is LayoutSnippet snippet)
                {
                    // Prepare the drag data
                    DataObject dragData = new DataObject();
                    dragData.SetData("LayoutSnippet", snippet);
                    dragData.SetData(DataFormats.StringFormat, ProcessSnippetForDrag(snippet));
                    
                    // Start the drag operation
                    DragDrop.DoDragDrop(element, dragData, DragDropEffects.Copy);
                    e.Handled = true;
                }
            }
        }

        public void InsertSnippet(LayoutSnippet snippet)
        {
            if (editor == null) return;

            string selectedText = editor.SelectionLength > 0 ? editor.SelectedText : "";
            string processedSnippet = ProcessSnippet(snippet, 1, selectedText);

            // Get the current indentation
            string indentation = GetIndentation();
            if (!string.IsNullOrEmpty(indentation))
            {
                processedSnippet = ApplyIndentation(processedSnippet, indentation);
            }

            if (editor.SelectionLength > 0)
            {
                editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, processedSnippet);
            }
            else
            {
                editor.Document.Insert(editor.CaretOffset, processedSnippet);
            }
        }

        private string GetIndentation()
        {
            if (editor == null) return string.Empty;
            
            // Get the current line
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            if (line == null) return string.Empty;
            
            // Extract text from the start of the line to the caret position
            string lineText = editor.Document.GetText(line.Offset, Math.Min(line.Length, editor.CaretOffset - line.Offset));
            
            // Extract only the whitespace at the beginning
            return new string(lineText.TakeWhile(c => c == ' ' || c == '\t').ToArray());
        }

        private string ApplyIndentation(string text, string indentation)
        {
            // Split the text by newlines
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Apply indentation logic:
            // - First line: Insert at cursor position (no additional indentation)
            // - Subsequent lines: Apply the base indentation from the cursor position
            for (int i = 1; i < lines.Length; i++)
            {
                // Skip empty lines or lines that are just whitespace
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                    
                // For subsequent lines (i > 0), apply the base indentation from cursor position
                lines[i] = indentation + lines[i];
            }
            
            // Join the lines back together
            return string.Join(Environment.NewLine, lines);
        }

        public string ProcessSnippet(LayoutSnippet snippet, int position = 1)
        {
            string xaml = snippet.XamlTemplate;
            // Replace position placeholder
            xaml = xaml.Replace("{0}", position.ToString());

            // Replace all other placeholders with their default values if present
            if (snippet.Placeholders != null)
            {
                foreach (var kvp in snippet.Placeholders)
                {
                    xaml = xaml.Replace(kvp.Key, kvp.Value);
                }
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

            return xaml;
        }

        public string ProcessSnippet(LayoutSnippet snippet, int position, string content)
        {
            return LayoutSnippet.ProcessSnippet(snippet, position, content);
        }

        // Explicitly call the correct overload in drag and drop
        private string ProcessSnippetForDrag(LayoutSnippet snippet)
        {
            return LayoutSnippet.ProcessSnippet(snippet, 1, "");
        }
    }
}
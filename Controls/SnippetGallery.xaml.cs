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

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<LayoutSnippet> SnippetSelected;

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
                Mouse.OverrideCursor = Cursors.Hand;
        }

        private void SnippetItem_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
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
                    dragData.SetData(DataFormats.StringFormat, ProcessSnippet(snippet));
                    
                    // Start the drag operation
                    DragDrop.DoDragDrop(element, dragData, DragDropEffects.Copy);
                    e.Handled = true;
                }
            }
        }

        public void InsertSnippet(LayoutSnippet snippet)
        {
            if (editor == null) return;

            string processedSnippet = ProcessSnippet(snippet);
            
            // Get the current indentation
            string indentation = GetIndentation();
            
            // Apply indentation to each line of the snippet
            if (!string.IsNullOrEmpty(indentation))
            {
                processedSnippet = ApplyIndentation(processedSnippet, indentation);
            }
            
            // Check if there's selected text to replace
            if (editor.SelectionLength > 0)
            {
                // If there's a {content} placeholder, replace it with the selected text
                if (processedSnippet.Contains("{content}"))
                {
                    string selectedText = editor.SelectedText;
                    processedSnippet = processedSnippet.Replace("{content}", selectedText);
                }
                
                // Replace the selected text with the snippet
                editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, processedSnippet);
            }
            else
            {
                // If no selection, just insert at caret position
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
            
            // Apply indentation to each line except the first one (which will inherit the caret's indentation)
            for (int i = 1; i < lines.Length; i++)
            {
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
    }
}
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

namespace RCLayoutPreview.Controls
{
    public partial class SnippetGallery : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<LayoutSnippet> snippets;
        private ICollectionView snippetsView;
        private string searchText;

        private TextBox searchBox;
        private ItemsControl snippetsList;

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
                searchBox = this.FindName("SearchBox") as TextBox;
                snippetsList = this.FindName("SnippetsList") as ItemsControl;
                
                if (searchBox != null)
                    searchBox.TextChanged += SearchBox_TextChanged;
                
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
            if (sender is FrameworkElement element && element.DataContext is LayoutSnippet snippet)
            {
                Mouse.OverrideCursor = Cursors.Hand;

                DataObject dragData = new DataObject();
                dragData.SetData("LayoutSnippet", snippet);
                dragData.SetData(DataFormats.StringFormat, ProcessSnippet(snippet));

                DragDrop.DoDragDrop(element, dragData, DragDropEffects.Copy);
                Mouse.OverrideCursor = null;
            }
        }

        private void SnippetItem_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
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
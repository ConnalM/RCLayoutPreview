using RCLayoutPreview.Helpers;
using System;
using System.IO;
using System.Windows;

namespace RCLayoutPreview
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try 
            {
                // Ensure our LayoutSnippet.cs file is complete and not truncated
                LayoutSnippetUtility.SaveLayoutSnippetClass();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

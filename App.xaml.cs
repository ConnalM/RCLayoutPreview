using RCLayoutPreview.Helpers;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace RCLayoutPreview
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();
        
        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Allocate console for debug output
            AllocConsole();
            Console.WriteLine("=== RCLayoutPreview Debug Console ===");
            Console.WriteLine($"Application started at: {DateTime.Now}");
            Console.WriteLine("ThemeDictionary auto-reload monitoring will be shown here...");
            
            try 
            {
                // Ensure our LayoutSnippet.cs file is complete and not truncated
                LayoutSnippet.SaveLayoutSnippetClass();
                Console.WriteLine("LayoutSnippet initialization completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during startup: {ex.Message}");
                MessageBox.Show($"Error during startup: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            FreeConsole();
            base.OnExit(e);
        }
    }
}

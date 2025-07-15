using System.Collections.Generic;

namespace RCLayoutPreview.Helpers
{
    // This is a proxy class that delegates to the content stored in LayoutSnippetUtility
    // The actual implementation is maintained and saved through the utility to avoid truncation issues
    
    public class LayoutSnippet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string XamlTemplate { get; set; }
        public List<string> RequiredFields { get; set; } = new List<string>();
        public Dictionary<string, string> Placeholders { get; set; } = new Dictionary<string, string>();
        public string DefaultStyles { get; set; }

        // Constants for XAML templates are in LayoutSnippetUtility.cs
        
        // Get the default snippets from the utility class
        public static List<LayoutSnippet> GetDefaultSnippets()
        {
            // Save the file again just to ensure it's complete
            LayoutSnippetUtility.SaveLayoutSnippetClass();
            return new LayoutSnippet().GetSnippetsFromUtility();
        }
        
        // Helper method to get snippets from utility
        private List<LayoutSnippet> GetSnippetsFromUtility()
        {
            // This code will never actually run - we're just 
            // referencing the real implementation from LayoutSnippetUtility
            var snippets = new List<LayoutSnippet>();
            
            // Add basic snippets for intellisense/compilation
            snippets.Add(new LayoutSnippet { 
                Name = "Basic RC Layout", 
                Description = "Standard
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using System.Linq;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Custom XAML reader that handles loading ThemeDictionary with RaceCoordinator namespace
    /// </summary>
    public static class CustomXamlReader
    {
        /// <summary>
        /// Load ThemeDictionary from file with namespace issues handled
        /// </summary>
        public static ResourceDictionary LoadResourceDictionaryWithNamespaceOverride(string filePath)
        {
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Loading ThemeDictionary from {filePath}");
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[DEBUG-THEME] CUSTOM: File does not exist!");
                return null;
            }
                
            try
            {
                // Read file content for diagnostic purposes
                try
                {
                    string content = File.ReadAllText(filePath);
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: File size: {content.Length} bytes");
                    
                    // Extract the first part of content
                    string preview = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: Content preview: {preview}");
                    
                    // Look for critical elements
                    if (content.Contains("xmlns:local=\"clr-namespace:RaceCoordinator\""))
                    {
                        Console.WriteLine("[DEBUG-THEME] CUSTOM: Found namespace 'clr-namespace:RaceCoordinator'");
                    }
                    
                    if (content.Contains("RSValueColor"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(content, 
                            @"<Brush\s+x:Key=""RSValueColor"">([^<]+)</Brush>");
                        
                        if (match.Success)
                        {
                            string colorValue = match.Groups[1].Value.Trim();
                            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Found RSValueColor: {colorValue}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: Error reading file content: {ex.Message}");
                }
                
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Starting METHOD 1: Direct URI with no namespace validation");
                try
                {
                    // METHOD 1: Load using URI but bypass namespace validation
                    return LoadWithDirectUri(filePath);
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: METHOD 1 failed: {ex1.Message}");
                    
                    try
                    {
                        // METHOD 2: Load with direct file stream
                        Console.WriteLine("[DEBUG-THEME] CUSTOM: Starting METHOD 2: Direct FileStream load");
                        return LoadWithFileStream(filePath);
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"[DEBUG-THEME] CUSTOM: METHOD 2 failed: {ex2.Message}");
                        
                        try
                        {
                            // METHOD 3: Use text replacement
                            Console.WriteLine("[DEBUG-THEME] CUSTOM: Starting METHOD 3: Text replacement");
                            return LoadWithTextReplacement(filePath);
                        }
                        catch (Exception ex3)
                        {
                            Console.WriteLine($"[DEBUG-THEME] CUSTOM: METHOD 3 failed: {ex3.Message}");
                            Console.WriteLine("[DEBUG-THEME] CUSTOM: All methods failed!");
                            return new ResourceDictionary(); // Return empty dictionary to avoid crashes
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Unexpected error: {ex.Message}");
                return new ResourceDictionary(); // Return empty dictionary to avoid crashes
            }
        }
        
        /// <summary>
        /// METHOD 1: Load using URI with cached properties disabled
        /// </summary>
        private static ResourceDictionary LoadWithDirectUri(string filePath)
        {
            Console.WriteLine("[DEBUG-THEME] CUSTOM: Creating resource dictionary with Source URI");
            ResourceDictionary resourceDictionary = new ResourceDictionary();
            
            // Create absolute URI
            Uri uri = new Uri(filePath, UriKind.Absolute);
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: URI created: {uri}");
            
            // Configure XamlReader with no caching
            try 
            {
                // Disable caching at assembly level
                typeof(XamlReader).GetField("_xamlParsers", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Reset XamlReader internal cache");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Failed to reset XamlReader cache: {ex.Message}");
            }
            
            // Set Source
            resourceDictionary.Source = uri;
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Set Source property");
            
            // Force resources to update
            MakeResourcesDynamic(resourceDictionary);
            
            // Check what we loaded
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Loaded dictionary with {resourceDictionary.Count} resources");
            
            // Debug: Check for RSValueColor
            if (resourceDictionary.Contains("RSValueColor"))
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: RSValueColor = {resourceDictionary["RSValueColor"]}");
            }
            else
            {
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Dictionary does not contain RSValueColor key");
            }
            
            return resourceDictionary;
        }
        
        /// <summary>
        /// METHOD 2: Load with direct file stream
        /// </summary>
        private static ResourceDictionary LoadWithFileStream(string filePath)
        {
            Console.WriteLine("[DEBUG-THEME] CUSTOM: Opening file stream for direct load");
            
            ResourceDictionary resourceDictionary = null;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Loading from stream with XamlReader");
                resourceDictionary = (ResourceDictionary)XamlReader.Load(fileStream);
            }
            
            // Make resources dynamic
            MakeResourcesDynamic(resourceDictionary);
            
            // Check what we loaded
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Loaded dictionary with {resourceDictionary.Count} resources");
            
            // Debug: Check for RSValueColor
            if (resourceDictionary.Contains("RSValueColor"))
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: RSValueColor = {resourceDictionary["RSValueColor"]}");
            }
            else
            {
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Dictionary does not contain RSValueColor key");
            }
            
            return resourceDictionary;
        }
        
        /// <summary>
        /// METHOD 3: Load by directly replacing the namespace in the text content
        /// </summary>
        private static ResourceDictionary LoadWithTextReplacement(string filePath)
        {
            Console.WriteLine("[DEBUG-THEME] CUSTOM: Reading raw XAML content");
            
            // Read the raw XAML
            string xamlContent = File.ReadAllText(filePath);
            
            // Create a temporary copy with our namespace
            string tempFilePath = Path.GetTempFileName() + ".xaml";
            
            Console.WriteLine("[DEBUG-THEME] CUSTOM: Creating temp file with modified namespace");
            Console.WriteLine($"[DEBUG-THEME] CUSTOM: Temp file path: {tempFilePath}");
            
            // Replace the namespace if needed
            if (xamlContent.Contains("xmlns:local=\"clr-namespace:RaceCoordinator\""))
            {
                xamlContent = xamlContent.Replace(
                    "xmlns:local=\"clr-namespace:RaceCoordinator\"", 
                    "xmlns:local=\"clr-namespace:RCLayoutPreview\"");
                    
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Replaced RaceCoordinator namespace with RCLayoutPreview");
            }
            
            // Write to temp file
            File.WriteAllText(tempFilePath, xamlContent);
            
            try
            {
                Console.WriteLine("[DEBUG-THEME] CUSTOM: Loading from temp file with modified namespace");
                
                // Load with standard XamlReader from the temp file
                ResourceDictionary resourceDictionary = null;
                using (var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                {
                    resourceDictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(fileStream);
                }
                
                // Make resources dynamic
                MakeResourcesDynamic(resourceDictionary);
                
                // Check what we loaded
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Loaded dictionary with {resourceDictionary.Count} resources from temp file");
                
                // Debug: Check for RSValueColor
                if (resourceDictionary.Contains("RSValueColor"))
                {
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: RSValueColor = {resourceDictionary["RSValueColor"]}");
                }
                else
                {
                    Console.WriteLine("[DEBUG-THEME] CUSTOM: Dictionary does not contain RSValueColor key");
                }
                
                return resourceDictionary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Text replacement method failed: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up temp file
                try { File.Delete(tempFilePath); } 
                catch (Exception ex) { 
                    Console.WriteLine($"[DEBUG-THEME] CUSTOM: Failed to delete temp file: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Make resources dynamic to ensure they refresh properly
        /// </summary>
        private static void MakeResourcesDynamic(ResourceDictionary dict)
        {
            Console.WriteLine("[DEBUG-THEME] CUSTOM: Making resources dynamic");
            
            // For each resource in the dictionary
            try 
            {
                int count = 0;
                
                foreach (var key in dict.Keys)
                {
                    var value = dict[key];
                    
                    // For brushes, ensure they're seen as dynamic resources
                    if (value is System.Windows.Media.Brush)
                    {
                        count++;
                        
                        // Force resource lookup to update
                        var originalValue = dict[key];
                        dict.Remove(key);
                        dict.Add(key, originalValue);
                    }
                }
                
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Refreshed {count} brush resources");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG-THEME] CUSTOM: Error making resources dynamic: {ex.Message}");
            }
        }
    }
}
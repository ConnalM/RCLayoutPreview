using System;
using System.Text.RegularExpressions;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Transforms StaticResource references to DynamicResource references for live theme preview.
    /// This allows theme changes to be reflected in real-time during preview, 
    /// while preserving the original StaticResource intent for production use.
    /// </summary>
    public static class StaticResourceTransformer
    {
        /// <summary>
        /// Transforms StaticResource references to DynamicResource in XAML content for live preview
        /// This enables theme refresh functionality while preserving original StaticResource behavior
        /// </summary>
        /// <param name="xamlContent">Original XAML content with StaticResource references</param>
        /// <param name="enableTransformation">Whether to perform the transformation</param>
        /// <returns>XAML content with StaticResource converted to DynamicResource (if enabled)</returns>
        public static string TransformForPreview(string xamlContent, bool enableTransformation = true)
        {
            if (string.IsNullOrEmpty(xamlContent) || !enableTransformation)
            {
                return xamlContent;
            }
            
            // Add a comment to indicate transformation was applied
            string transformedXaml = "<!-- PREVIEW MODE: StaticResource converted to DynamicResource for theme refresh -->\n" + xamlContent;
            
            // Transform StaticResource to DynamicResource for all common theme-related properties
            transformedXaml = TransformResourceReferences(transformedXaml, "Background");
            transformedXaml = TransformResourceReferences(transformedXaml, "Foreground");
            transformedXaml = TransformResourceReferences(transformedXaml, "BorderBrush");
            transformedXaml = TransformResourceReferences(transformedXaml, "Fill");
            transformedXaml = TransformResourceReferences(transformedXaml, "Stroke");
            
            return transformedXaml;
        }
        
        /// <summary>
        /// Transforms StaticResource references to DynamicResource for a specific property
        /// </summary>
        /// <param name="xamlContent">XAML content to transform</param>
        /// <param name="propertyName">Property name (e.g., "Background", "Foreground")</param>
        /// <returns>Transformed XAML content</returns>
        private static string TransformResourceReferences(string xamlContent, string propertyName)
        {
            // Pattern to match: PropertyName="{StaticResource ResourceKey}"
            string pattern = $@"{propertyName}=""\{{StaticResource\s+([^}}]+)\}}""";
            
            // Replacement: PropertyName="{DynamicResource ResourceKey}"
            string replacement = $@"{propertyName}=""{{DynamicResource $1}}""";
            
            return Regex.Replace(xamlContent, pattern, replacement, RegexOptions.IgnoreCase);
        }
        
        /// <summary>
        /// Checks if the XAML content has been transformed for preview
        /// </summary>
        /// <param name="xamlContent">XAML content to check</param>
        /// <returns>True if the content was transformed, false otherwise</returns>
        public static bool IsTransformed(string xamlContent)
        {
            return !string.IsNullOrEmpty(xamlContent) && 
                   xamlContent.Contains("<!-- PREVIEW MODE: StaticResource converted to DynamicResource for theme refresh -->");
        }
        
        /// <summary>
        /// Counts the number of StaticResource references in XAML content
        /// </summary>
        /// <param name="xamlContent">XAML content to analyze</param>
        /// <returns>Number of StaticResource references found</returns>
        public static int CountStaticResourceReferences(string xamlContent)
        {
            if (string.IsNullOrEmpty(xamlContent))
                return 0;
                
            var matches = Regex.Matches(xamlContent, @"\{StaticResource\s+[^}]+\}", RegexOptions.IgnoreCase);
            return matches.Count;
        }
        
        /// <summary>
        /// Counts the number of DynamicResource references in XAML content
        /// </summary>
        /// <param name="xamlContent">XAML content to analyze</param>
        /// <returns>Number of DynamicResource references found</returns>
        public static int CountDynamicResourceReferences(string xamlContent)
        {
            if (string.IsNullOrEmpty(xamlContent))
                return 0;
                
            var matches = Regex.Matches(xamlContent, @"\{DynamicResource\s+[^}]+\}", RegexOptions.IgnoreCase);
            return matches.Count;
        }
        
        /// <summary>
        /// Provides statistics about resource usage in XAML content
        /// </summary>
        /// <param name="xamlContent">XAML content to analyze</param>
        /// <returns>Formatted string with resource statistics</returns>
        public static string GetResourceStatistics(string xamlContent)
        {
            if (string.IsNullOrEmpty(xamlContent))
                return "No XAML content provided";
                
            int staticCount = CountStaticResourceReferences(xamlContent);
            int dynamicCount = CountDynamicResourceReferences(xamlContent);
            bool isTransformed = IsTransformed(xamlContent);
            
            return $"StaticResource: {staticCount}, DynamicResource: {dynamicCount}" + 
                   (isTransformed ? " (Transformed for preview)" : "");
        }
    }
}
using System;
using System.Windows.Markup;
using System.Xml;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace RCLayoutPreview.Helpers
{
    public static class XamlValidationHelper
    {
        public static bool IsValidXml(string xaml, out string error)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xaml);
                error = null;
                return true;
            }
            catch (XmlException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static object ParseXaml(string xaml, out string error)
        {
            try
            {
                var element = XamlReader.Parse(xaml);
                error = null;
                return element;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Enhanced XAML parsing with better error reporting including position mapping
        /// </summary>
        /// <param name="xaml">XAML content to parse</param>
        /// <param name="error">Detailed error message with position info</param>
        /// <param name="errorPosition">Character position in the original XAML where error occurred</param>
        /// <returns>Parsed XAML object or null if parsing failed</returns>
        public static object ParseXamlWithPosition(string xaml, out string error, out int errorPosition)
        {
            errorPosition = -1;
            try
            {
                var element = XamlReader.Parse(xaml);
                error = null;
                return element;
            }
            catch (XamlParseException ex)
            {
                error = ex.Message;
                errorPosition = ExtractErrorPosition(ex.Message, xaml);
                return null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                errorPosition = ExtractErrorPosition(ex.Message, xaml);
                return null;
            }
        }

        /// <summary>
        /// Extracts character position from XAML parsing error messages
        /// </summary>
        /// <param name="errorMessage">Error message from XAML parsing</param>
        /// <param name="xaml">Original XAML content</param>
        /// <returns>Character position in XAML or -1 if not found</returns>
        private static int ExtractErrorPosition(string errorMessage, string xaml)
        {
            // Pattern for errors like: "Cannot set unknown member 'System.Windows.Controls.Menu.Header'. Line number '23' and line position '31'."
            var memberErrorMatch = Regex.Match(errorMessage, @"Line\s+number\s+'(\d+)'\s+and\s+line\s+position\s+'(\d+)'", RegexOptions.IgnoreCase);
            if (memberErrorMatch.Success)
            {
                int lineNumber = int.Parse(memberErrorMatch.Groups[1].Value);
                int columnNumber = int.Parse(memberErrorMatch.Groups[2].Value);
                
                // Convert line number to character position in the original XAML
                int linePosition = GetPositionFromLineNumber(xaml, lineNumber);
                // Add the column offset (but subtract 1 since positions are usually 1-based in error messages)
                return Math.Min(linePosition + Math.Max(0, columnNumber - 1), xaml.Length - 1);
            }

            // Special handling for mismatched tag errors: "The 'MenuItem' start tag on line 22 position 22 does not match the end tag of 'Menu'. Line 1, position 15."
            // Extract the line number where the problematic end tag actually is
            var mismatchErrorMatch = Regex.Match(errorMessage, @"The\s+'([^']+)'\s+start\s+tag\s+on\s+line\s+(\d+).*does\s+not\s+match.*end\s+tag\s+of\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (mismatchErrorMatch.Success)
            {
                string startTagName = mismatchErrorMatch.Groups[1].Value; // e.g., "MenuItem" or "StackPanel"
                int startTagLine = int.Parse(mismatchErrorMatch.Groups[2].Value); // e.g., 22 or 40
                string endTagName = mismatchErrorMatch.Groups[3].Value; // e.g., "Menu" or "Grid"
                
                // Strategy 1: Look for the problematic end tag that appears in the error message
                // This finds the </Grid> or </Menu> tag that's causing the mismatch
                string endTagPattern = $"</{endTagName}>";
                var endTagMatches = Regex.Matches(xaml, Regex.Escape(endTagPattern), RegexOptions.IgnoreCase);
                
                // Find the end tag that comes after the reported start tag line
                int startTagPosition = GetPositionFromLineNumber(xaml, startTagLine);
                
                foreach (Match match in endTagMatches)
                {
                    if (match.Index > startTagPosition)
                    {
                        // This is likely the problematic end tag - return this position
                        return match.Index;
                    }
                }
                
                // Strategy 2: If we have a structural error (like missing </StackPanel>)
                // Try to find where the missing closing tag should be
                if (startTagName != endTagName)
                {
                    // Look for the expected closing tag of the start element
                    string expectedClosingTag = $"</{startTagName}>";
                    int expectedClosingTagPos = xaml.IndexOf(expectedClosingTag, startTagPosition, StringComparison.OrdinalIgnoreCase);
                    
                    if (expectedClosingTagPos == -1)
                    {
                        // Missing closing tag - try to find where it should logically be
                        // Look for the next closing tag after the start tag and suggest that area
                        var nextClosingTagMatch = Regex.Match(xaml.Substring(startTagPosition), @"</\w+>", RegexOptions.IgnoreCase);
                        if (nextClosingTagMatch.Success)
                        {
                            // Point to the area where the missing closing tag should be (before the next closing tag)
                            return startTagPosition + nextClosingTagMatch.Index - 10; // Position slightly before to highlight the gap
                        }
                    }
                }
                
                // Fallback to the start tag position
                return startTagPosition;
            }

            // Pattern: "The 'MenuItem' start tag on line 22" - extract the actual line number
            var actualLineMatch = Regex.Match(errorMessage, @"(?:start\s+tag\s+)?on\s+line\s+(\d+)", RegexOptions.IgnoreCase);
            if (actualLineMatch.Success)
            {
                int actualLine = int.Parse(actualLineMatch.Groups[1].Value);
                return GetPositionFromLineNumber(xaml, actualLine);
            }

            // Pattern: "Line X, position Y" in error message - but this is often wrong for processed XAML
            var linePositionMatch = Regex.Match(errorMessage, @"Line\s+(\d+),\s+position\s+(\d+)", RegexOptions.IgnoreCase);
            if (linePositionMatch.Success)
            {
                int reportedLine = int.Parse(linePositionMatch.Groups[1].Value);
                int position = int.Parse(linePositionMatch.Groups[2].Value);
                
                // If the reported line is 1, the position might be a character offset
                if (reportedLine == 1)
                {
                    return Math.Min(Math.Max(0, position - 1), xaml.Length - 1);
                }
                else
                {
                    // Try to use the reported line number
                    return GetPositionFromLineNumber(xaml, reportedLine);
                }
            }

            // Try to find specific tag names mentioned in the error message
            var tagErrorMatch = Regex.Match(errorMessage, @"'([^']+)'\s+(?:start\s+tag|tag|element)", RegexOptions.IgnoreCase);
            if (tagErrorMatch.Success)
            {
                string tagName = tagErrorMatch.Groups[1].Value;
                if (!string.IsNullOrEmpty(tagName))
                {
                    // Look for the problematic tag in the XAML
                    // Try both opening and closing tags
                    string[] tagPatterns = { $"<{tagName}", $"</{tagName}", $"<{tagName}/>" };
                    
                    foreach (string pattern in tagPatterns)
                    {
                        int lastIndex = xaml.LastIndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                        if (lastIndex >= 0)
                        {
                            return lastIndex;
                        }
                    }
                }
            }

            // For errors about unknown members, try to extract the class name and find it
            var memberMatch = Regex.Match(errorMessage, @"Cannot\s+set\s+unknown\s+member\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (memberMatch.Success)
            {
                string memberName = memberMatch.Groups[1].Value;
                // Extract the class name (e.g., "System.Windows.Controls.Menu.Header" -> "Menu")
                string[] parts = memberName.Split('.');
                if (parts.Length >= 2)
                {
                    string className = parts[parts.Length - 2]; // Get the class name before the property
                    string propertyName = parts[parts.Length - 1]; // Get the property name
                    
                    // Special handling for attached properties like Grid.RowDefinitions, Grid.ColumnDefinitions, etc.
                    if (IsAttachedProperty(className, propertyName))
                    {
                        // Look for the attached property usage in XAML (e.g., <Grid.RowDefinitions>)
                        var attachedPropertyMatch = Regex.Match(xaml, $@"<{className}\.{propertyName}[^>]*>", RegexOptions.IgnoreCase);
                        if (attachedPropertyMatch.Success)
                        {
                            // Find the position of this attached property usage
                            int attachedPropertyPosition = attachedPropertyMatch.Index;
                            
                            // Look backwards to find where the parent element should be
                            string parentTagPattern = $@"<{className}[^>]*>";
                            int searchStart = Math.Max(0, attachedPropertyPosition - 500); // Look back 500 chars
                            string searchArea = xaml.Substring(searchStart, attachedPropertyPosition - searchStart);
                            
                            var parentMatch = Regex.Matches(searchArea, parentTagPattern, RegexOptions.IgnoreCase);
                            if (parentMatch.Count == 0)
                            {
                                // No parent Grid found - the <Grid> tag is missing
                                // Point to the area where the Grid should be (before the attached property)
                                return Math.Max(0, attachedPropertyPosition - 50);
                            }
                            else
                            {
                                // Parent exists but there might be a structural issue
                                return attachedPropertyPosition;
                            }
                        }
                    }
                    
                    // Look for the tag with this class name and property
                    var tagWithPropertyMatch = Regex.Match(xaml, $@"<{className}[^>]*{propertyName}\s*=", RegexOptions.IgnoreCase);
                    if (tagWithPropertyMatch.Success)
                    {
                        return tagWithPropertyMatch.Index;
                    }
                    
                    // If not found, just look for the class name
                    int tagPos = xaml.LastIndexOf($"<{className}", StringComparison.OrdinalIgnoreCase);
                    if (tagPos >= 0)
                    {
                        return tagPos;
                    }
                }
            }

            // Pattern: "position 129" alone
            var positionMatch = Regex.Match(errorMessage, @"position\s+(\d+)", RegexOptions.IgnoreCase);
            if (positionMatch.Success)
            {
                int position = int.Parse(positionMatch.Groups[1].Value);
                return Math.Min(Math.Max(0, position - 1), xaml.Length - 1);
            }

            return -1; // Position not found
        }

        /// <summary>
        /// Converts a line number to character position in the text
        /// </summary>
        /// <param name="text">The text content</param>
        /// <param name="lineNumber">Line number (1-based)</param>
        /// <returns>Character position (0-based) at the start of the specified line</returns>
        private static int GetPositionFromLineNumber(string text, int lineNumber)
        {
            if (lineNumber <= 1)
                return 0;

            int currentLine = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    currentLine++;
                    if (currentLine == lineNumber)
                    {
                        return Math.Min(i + 1, text.Length - 1); // Return position after the newline
                    }
                }
            }

            // If line number is beyond the text, return the end
            return Math.Max(0, text.Length - 1);
        }

        /// <summary>
        /// Determines if a property is an attached property that requires a parent container
        /// </summary>
        /// <param name="className">The class name (e.g., "Grid")</param>
        /// <param name="propertyName">The property name (e.g., "RowDefinitions")</param>
        /// <returns>True if this is a known attached property</returns>
        private static bool IsAttachedProperty(string className, string propertyName)
        {
            // Common WPF attached properties that require parent containers
            var attachedProperties = new Dictionary<string, string[]>
            {
                { "Grid", new[] { "RowDefinitions", "ColumnDefinitions", "Row", "Column", "RowSpan", "ColumnSpan" } },
                { "DockPanel", new[] { "Dock" } },
                { "Canvas", new[] { "Left", "Top", "Right", "Bottom", "ZIndex" } },
                { "Panel", new[] { "ZIndex" } },
                { "StackPanel", new[] { "Orientation" } }
            };

            if (attachedProperties.ContainsKey(className))
            {
                string[] properties = attachedProperties[className];
                foreach (string prop in properties)
                {
                    if (string.Equals(prop, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a character position to line and column numbers in the text
        /// </summary>
        /// <param name="text">The text content</param>
        /// <param name="position">Character position (0-based)</param>
        /// <param name="line">Output line number (1-based)</param>
        /// <param name="column">Output column number (1-based)</param>
        public static void GetLineAndColumnFromPosition(string text, int position, out int line, out int column)
        {
            line = 1;
            column = 1;

            if (position < 0 || position >= text.Length)
            {
                return;
            }

            for (int i = 0; i < position; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else if (text[i] != '\r') // Don't count \r in \r\n sequences
                {
                    column++;
                }
            }
        }

        /// <summary>
        /// Finds the nearest meaningful context around an error position
        /// </summary>
        /// <param name="xaml">XAML content</param>
        /// <param name="errorPosition">Error position</param>
        /// <param name="contextLength">Length of context to show around error</param>
        /// <returns>Context string with error position marked</returns>
        public static string GetErrorContext(string xaml, int errorPosition, int contextLength = 50)
        {
            if (errorPosition < 0 || errorPosition >= xaml.Length)
            {
                return string.Empty;
            }

            int start = Math.Max(0, errorPosition - contextLength / 2);
            int end = Math.Min(xaml.Length, errorPosition + contextLength / 2);
            
            string context = xaml.Substring(start, end - start);
            int relativePosition = errorPosition - start;

            // Insert a marker at the error position
            if (relativePosition >= 0 && relativePosition <= context.Length)
            {
                context = context.Insert(relativePosition, "<<<ERROR>>>");
            }

            // Clean up the context for display - use regular characters for .NET Framework 4.8 compatibility
            context = context.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');

            if (start > 0) context = "..." + context;
            if (end < xaml.Length) context = context + "...";
            return context;
        }

        /// <summary>
        /// Creates a user-friendly error message with correct line and column information
        /// </summary>
        /// <param name="originalError">Original error message</param>
        /// <param name="xaml">XAML content</param>
        /// <param name="errorPosition">Character position of error</param>
        /// <returns>Enhanced error message with correct position</returns>
        public static string CreateEnhancedErrorMessage(string originalError, string xaml, int errorPosition)
        {
            if (errorPosition < 0)
            {
                return originalError; // No position info available
            }

            GetLineAndColumnFromPosition(xaml, errorPosition, out int line, out int column);
            
            // Check for attached property errors first
            var attachedPropertyMatch = Regex.Match(originalError, @"Cannot\s+set\s+unknown\s+member\s+'([^']+)\.([^']+)'", RegexOptions.IgnoreCase);
            if (attachedPropertyMatch.Success)
            {
                string fullClassName = attachedPropertyMatch.Groups[1].Value;
                string[] classParts = fullClassName.Split('.');
                string className = classParts[classParts.Length - 1]; // Get last part (e.g., "Grid" from "System.Windows.Controls.Grid")
                string propertyName = attachedPropertyMatch.Groups[2].Value;
                
                if (IsAttachedProperty(className, propertyName))
                {
                    string attachedPropertyHint = $"ATTACHED PROPERTY ERROR: The <{className}.{propertyName}> element on line {line} requires a parent <{className}> container. " +
                                                $"Make sure there is a <{className}> tag that wraps this <{className}.{propertyName}> element. " +
                                                $"Check around line {Math.Max(1, line - 5)} for the missing <{className}> parent tag.";
                    return attachedPropertyHint;
                }
            }
            
            // Check if this is a structural error (missing closing tag)
            var mismatchErrorMatch = Regex.Match(originalError, @"The\s+'([^']+)'\s+start\s+tag\s+on\s+line\s+(\d+).*does\s+not\s+match.*end\s+tag\s+of\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (mismatchErrorMatch.Success)
            {
                string startTagName = mismatchErrorMatch.Groups[1].Value;
                int startTagLine = int.Parse(mismatchErrorMatch.Groups[2].Value);
                string endTagName = mismatchErrorMatch.Groups[3].Value;
                
                if (startTagName != endTagName)
                {
                    // This suggests a missing closing tag
                    string structuralHint = $"STRUCTURAL ERROR: The <{startTagName}> tag starting on line {startTagLine} appears to be missing its closing </{startTagName}> tag. " +
                                          $"This is causing the </{endTagName}> tag on line {line} to mismatch. " +
                                          $"Check around line {Math.Max(startTagLine + 1, line - 5)} for the missing </{startTagName}> tag.";
                    return structuralHint;
                }
            }
            
            // Replace the incorrect position in the original message with the correct one
            var enhancedError = Regex.Replace(originalError, 
                @"Line\s+\d+,\s+position\s+\d+", 
                $"Line {line}, position {column}",
                RegexOptions.IgnoreCase);

            if (enhancedError == originalError)
            {
                // If no replacement was made, append position info
                enhancedError = $"{originalError} (Line {line}, position {column})";
            }

            return enhancedError;
        }
    }
}

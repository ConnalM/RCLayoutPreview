using System;
using System.Windows.Markup;
using System.Xml;
using System.Text.RegularExpressions;

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
            // Try to extract position from various error message formats
            
            // Pattern: "Line 1, position 129."
            var linePositionMatch = Regex.Match(errorMessage, @"Line\s+(\d+),\s+position\s+(\d+)", RegexOptions.IgnoreCase);
            if (linePositionMatch.Success)
            {
                int line = int.Parse(linePositionMatch.Groups[1].Value);
                int position = int.Parse(linePositionMatch.Groups[2].Value);
                
                // Convert line/position to character offset
                // Note: XAML parsing treats the entire content as one line, so we use the position directly
                return Math.Min(position - 1, xaml.Length - 1); // Convert to 0-based and ensure within bounds
            }

            // Pattern: "position 129"
            var positionMatch = Regex.Match(errorMessage, @"position\s+(\d+)", RegexOptions.IgnoreCase);
            if (positionMatch.Success)
            {
                int position = int.Parse(positionMatch.Groups[1].Value);
                return Math.Min(position - 1, xaml.Length - 1); // Convert to 0-based and ensure within bounds
            }

            // Pattern: "character 129" 
            var characterMatch = Regex.Match(errorMessage, @"character\s+(\d+)", RegexOptions.IgnoreCase);
            if (characterMatch.Success)
            {
                int position = int.Parse(characterMatch.Groups[1].Value);
                return Math.Min(position - 1, xaml.Length - 1); // Convert to 0-based and ensure within bounds
            }

            return -1; // Position not found
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

            // Clean up the context for display
            context = context.Replace('\n', '?').Replace('\r', ' ').Replace('\t', '?');

            if (start > 0) context = "..." + context;
            if (end < xaml.Length) context = context + "...";

            return context;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Handles detection, lookup, and display of placeholder elements in XAML.
    /// </summary>
    public static class PlaceholderHandler
    {
        /// <summary>
        /// Determines if the given element is a placeholder (by name convention).
        /// </summary>
        /// <param name="element">FrameworkElement to check</param>
        /// <returns>True if element is a placeholder, false otherwise</returns>
        public static bool IsPlaceholderElement(System.Windows.FrameworkElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Name) && element.Name.StartsWith("Placeholder");
        }

        /// <summary>
        /// Displays a placeholder element, optionally formatting its content by position.
        /// </summary>
        /// <param name="element">Placeholder element to display</param>
        /// <param name="position">Position value for formatting (default 1)</param>
        public static void DisplayPlaceholder(System.Windows.FrameworkElement element, int position = 1)
        {
            if (element == null) return;
            string initialValue = "";
            // Get initial value from Label or TextBlock
            if (element is Label lbl)
                initialValue = lbl.Content?.ToString() ?? "";
            else if (element is TextBlock tb)
                initialValue = tb.Text ?? "";
            // Format with position if needed
            if (initialValue.Contains("{0}"))
                initialValue = string.Format(initialValue, position);
            // Hide any placeholder with Transparent foreground or if its text is empty
            if (element is TextBlock textBlock)
            {
                if (textBlock.Foreground == Brushes.Transparent || string.IsNullOrWhiteSpace(initialValue))
                {
                    textBlock.Text = "";
                    textBlock.Background = Brushes.Transparent;
                    textBlock.Visibility = Visibility.Hidden;
                    return;
                }
                textBlock.Text = initialValue;
                textBlock.Background = new SolidColorBrush(Colors.Black);
                textBlock.Foreground = new SolidColorBrush(Colors.White);
            }
            else if (element is Label label)
            {
                if (label.Foreground == Brushes.Transparent || string.IsNullOrWhiteSpace(initialValue))
                {
                    label.Content = "";
                    label.Background = Brushes.Transparent;
                    label.Visibility = Visibility.Hidden;
                    return;
                }
                label.Content = initialValue;
                label.Background = new SolidColorBrush(Colors.Black);
                label.Foreground = new SolidColorBrush(Colors.White);
            }
        }
    }
}

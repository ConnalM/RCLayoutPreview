using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Handles detection, lookup, and display of stubdata fields in XAML.
    /// </summary>
    public static class StubDataFieldHandler
    {
        // Delegate for logging to UI
        public static Action<string> UILogStatus = null;

        public static bool IsStubDataField(System.Windows.FrameworkElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Name) && !element.Name.StartsWith("Placeholder");
        }
        public static void DisplayStubDataField(System.Windows.FrameworkElement element, JObject jsonData, bool debugMode, string normalizedFieldName)
        {
            if (element == null || jsonData == null) return;
            JToken value = null;
            string foundGroup = null;
            bool found = false;
            Debug.WriteLine($"[StubDataFieldHandler] Looking up field: {normalizedFieldName}");
            UILogStatus?.Invoke($"Looking up field: {normalizedFieldName}");
            // Search all top-level groups for the field
            foreach (var prop in jsonData.Properties())
            {
                if (prop.Value is JObject group)
                {
                    if (group.TryGetValue(normalizedFieldName, out value))
                    {
                        foundGroup = prop.Name;
                        found = true;
                        Debug.WriteLine($"[StubDataFieldHandler] Found field '{normalizedFieldName}' in group '{foundGroup}'.");
                        UILogStatus?.Invoke($"Found field '{normalizedFieldName}' in group '{foundGroup}'.");
                        break;
                    }
                }
            }
            if (!found)
            {
                Debug.WriteLine($"[StubDataFieldHandler] Field '{normalizedFieldName}' not found in any stubdata group.");
                UILogStatus?.Invoke($"Field '{normalizedFieldName}' not found in any stubdata group.");
            }
            if (element is TextBlock textBlock)
                textBlock.Foreground = new SolidColorBrush(Colors.White);
            else if (element is Label label)
                label.Foreground = new SolidColorBrush(Colors.White);
            if (debugMode)
            {
                string displayText = normalizedFieldName;
                if (element is TextBlock tb2)
                    tb2.Text = displayText;
                else if (element is Label lbl)
                    lbl.Content = displayText;
                else if (element is ContentControl contentControl)
                    contentControl.Content = displayText;
            }
            else if (value != null)
            {
                string displayText = value.ToString();
                // Always try to get player index using XamlFixer.GetPlayerIndex
                int playerIndex = RCLayoutPreview.Helpers.XamlFixer.GetPlayerIndex(normalizedFieldName);
                Debug.WriteLine($"[StubDataFieldHandler] XamlFixer.GetPlayerIndex('{normalizedFieldName}') returned {playerIndex}");
                SolidColorBrush colorBrush = null;
                if (playerIndex > 0)
                {
                    colorBrush = RCLayoutPreview.Helpers.XamlFixer.GetColor(playerIndex);
                    Debug.WriteLine($"[StubDataFieldHandler] Calling XamlFixer.GetColor({playerIndex})");
                }
                if (element is TextBlock tb3)
                {
                    tb3.Text = displayText;
                    tb3.Background = colorBrush;
                }
                else if (element is Label lbl)
                {
                    lbl.Content = displayText;
                    lbl.Background = colorBrush;
                }
                else if (element is ContentControl contentControl)
                {
                    contentControl.Content = displayText;
                }
            }
        }
    }
}

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.IO;

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

            if (debugMode)
            {
                string displayText = normalizedFieldName;
                if (element is TextBlock tb2)
                    tb2.Text = displayText;
                else if (element is Label lbl)
                    lbl.Content = displayText;
                else if (element is ContentControl contentControl)
                    contentControl.Content = displayText;
                return; // Prevent further processing in diagnostics mode
            }
            else if (value != null && element is Image imageElement)
            {
                string imagePath = value.ToString();
                Debug.WriteLine($"[StubDataFieldHandler] Setting image source for '{normalizedFieldName}' to '{imagePath}'");
                UILogStatus?.Invoke($"Image path for '{normalizedFieldName}': {imagePath}");

                try
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string fullPath = Path.Combine(baseDirectory, imagePath);
                    Debug.WriteLine($"[StubDataFieldHandler] Resolved full path for '{normalizedFieldName}': {fullPath}");
                    UILogStatus?.Invoke($"Resolved full path for '{normalizedFieldName}': {fullPath}");

                    if (File.Exists(fullPath))
                    {
                        Debug.WriteLine($"[StubDataFieldHandler] File exists at path: {fullPath}");
                        UILogStatus?.Invoke($"File exists at path: {fullPath}");

                        imageElement.Source = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                        Debug.WriteLine($"[StubDataFieldHandler] Image control source set to: {imageElement.Source}");
                        UILogStatus?.Invoke($"Image source set for '{normalizedFieldName}': {imageElement.Source}");
                    }
                    else
                    {
                        Debug.WriteLine($"[StubDataFieldHandler] Image file not found at path: {fullPath}");
                        UILogStatus?.Invoke($"Image file not found for '{normalizedFieldName}' at path: {fullPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StubDataFieldHandler] Error setting image source: {ex.Message}");
                    UILogStatus?.Invoke($"Error setting image source for '{normalizedFieldName}': {ex.Message}");
                }
            }
            else if (value != null)
            {
                Debug.WriteLine($"[StubDataFieldHandler] Retrieved value for '{normalizedFieldName}': {value}");
                UILogStatus?.Invoke($"Value for '{normalizedFieldName}': {value}");

                string displayText = value.ToString();
                SolidColorBrush colorBrush = null;

                // Only apply color if field is in RacerData group and ends with a number NOT preceded by underscore
                if (foundGroup == "RacerData" && Regex.IsMatch(normalizedFieldName, @"(?<!_)\d+$"))
                {
                    int playerIndex = RCLayoutPreview.Helpers.XamlFixer.GetPlayerIndex(normalizedFieldName);
                    Debug.WriteLine($"[StubDataFieldHandler] XamlFixer.GetPlayerIndex('{normalizedFieldName}') returned {playerIndex}");
                    UILogStatus?.Invoke($"Player index for '{normalizedFieldName}': {playerIndex}");

                    if (playerIndex > 0)
                    {
                        colorBrush = RCLayoutPreview.Helpers.XamlFixer.GetColor(playerIndex);
                        Debug.WriteLine($"[StubDataFieldHandler] Calling XamlFixer.GetColor({playerIndex})");
                        UILogStatus?.Invoke($"Color for player index {playerIndex}: {colorBrush?.Color}");
                    }
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

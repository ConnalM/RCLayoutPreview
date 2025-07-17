using System.Linq;
using System.Xml.Linq;

namespace RCLayoutPreview.Helpers
{
    public static class XamlDesignSafePreParser
    {
        private static readonly string[] UnsafeElementNames = { "Storyboard", "MediaElement", "Script" };

        /// <summary>
        /// Strips unsafe elements from XAML for design-time safety.
        /// </summary>
        /// <param name="rawXaml">The original XAML string.</param>
        /// <returns>A string with unsafe elements removed.</returns>
        public static string StripUnsafeElementsForDesign(string rawXaml)
        {
            if (string.IsNullOrWhiteSpace(rawXaml))
                return rawXaml;

            try
            {
                var xml = XElement.Parse(rawXaml);

                // Find and remove any unsafe elements, ignoring namespaces for simplicity
                xml.Descendants()
                   .Where(e => UnsafeElementNames.Contains(e.Name.LocalName))
                   .Remove();

                return xml.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                // If parsing fails, return the original XAML to avoid breaking the preview
                return rawXaml;
            }
        }
    }
}

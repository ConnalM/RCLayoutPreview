using System.Linq;
using System.Xml.Linq;

namespace RCLayoutPreview.Helpers
{
    public static class XamlFixer
    {
        public static string Preprocess(string rawXaml)
        {
            try
            {
                var xml = XElement.Parse(rawXaml);

                // 🔧 Rule: Strip invalid Padding from Grid
                foreach (var grid in xml.Descendants().Where(e => e.Name.LocalName == "Grid"))
                    grid.Attributes("Padding").Remove();

                // 🔧 Rule: Remove any unknown 'VerticalAlignment' on StackPanels (as an example)
                foreach (var panel in xml.Descendants().Where(e => e.Name.LocalName == "StackPanel"))
                    panel.Attributes("VerticalAlignment").Where(a => !IsAllowedAlignment(a.Value)).Remove();

                // Add more fixer rules here...

                return xml.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                // If preprocessing fails, fall back to the raw string
                return rawXaml;
            }
        }

        private static bool IsAllowedAlignment(string value)
        {
            return value switch
            {
                "Top" or "Center" or "Bottom" or "Stretch" => true,
                _ => false
            };
        }
    }
}
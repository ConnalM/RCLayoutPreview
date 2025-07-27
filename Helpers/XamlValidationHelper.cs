using System;
using System.Windows.Markup;
using System.Xml;

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
    }
}

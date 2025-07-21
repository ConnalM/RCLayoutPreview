using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        
        public static bool TryParse(string rawName, out FieldNameParser parsed)
        {
            parsed = null;
            
            if (string.IsNullOrWhiteSpace(rawName))
                return false;

            // Simply remove last two characters (_1 or _2)
            string fieldType = rawName.Substring(0, rawName.Length - 2);
            
            parsed = new FieldNameParser 
            { 
                FieldType = fieldType
            };
            
            Debug.WriteLine($"[FieldNameParser] Parsed '{rawName}' to '{fieldType}'");
            return true;
        }
    }
}
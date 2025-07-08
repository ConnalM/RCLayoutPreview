using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers // Updated namespace to avoid conflict
{
    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        public int InstanceIndex { get; private set; } = 1;

        public static bool TryParse(string rawName, out FieldNameParser parsed)
        {
            parsed = null;

            // Log the rawName for debugging purposes
            Debug.WriteLine($"[FieldNameParser] Raw field name: {rawName}");

            if (string.IsNullOrWhiteSpace(rawName) || rawName.Length <= 2)
            {
                Debug.WriteLine($"[FieldNameParser] Invalid field name: {rawName}");
                return false;
            }

            // Remove the last two characters to get the FieldType
            string fieldType = rawName.Substring(0, rawName.Length - 2);

            parsed = new FieldNameParser
            {
                FieldType = fieldType,
                InstanceIndex = 1 // Default instance index
            };

            // Log the parsed FieldType for debugging purposes
            Debug.WriteLine($"[FieldNameParser] Parsed field type: {parsed.FieldType}");

            return true;
        }
    }
}
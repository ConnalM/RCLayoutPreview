using System.Text.RegularExpressions;
using System.Diagnostics; // Ensure this is added

namespace RCLayoutPreview.Helpers // Updated namespace to avoid conflict
{
    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        public string Context { get; private set; }
        public int InstanceIndex { get; private set; }
        public bool IsGeneric { get; private set; }
        public string BaseName => FieldType; // Add this for compatibility

        public static bool TryParse(string rawName, out FieldNameParser parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(rawName))
                return false;

            // Enhanced regex: handles FieldName, FieldName1, FieldName_Context, FieldName1_Context, FieldName_Context_1, etc.
            var match = Regex.Match(rawName, @"^(?<field>[A-Za-z_]+?)(?<num>\d+)?(_(?<context>[A-Za-z]+))?(_(?<index>\d+))?$");
            if (match.Success)
            {
                // Compose the base field name (e.g., NextHeatNickname1)
                string field = match.Groups["field"].Value;
                string num = match.Groups["num"].Success ? match.Groups["num"].Value : "";
                string baseName = field + num;

                parsed = new FieldNameParser
                {
                    FieldType = baseName,
                    Context = match.Groups["context"].Success ? match.Groups["context"].Value : null,
                    InstanceIndex = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : 1,
                    IsGeneric = string.IsNullOrEmpty(match.Groups["context"].Value)
                };
                return true;
            }

            // Fallback: treat entire string as a generic field
            parsed = new FieldNameParser
            {
                FieldType = rawName,
                Context = null,
                InstanceIndex = 1,
                IsGeneric = true
            };

            return true;
        }

        // Add a simple test method to verify parsing logic
        public static void TestFieldNameParser()
        {
            string test = "NextHeatNickname1_1";
            if (TryParse(test, out var parsed))
            {
                Debug.WriteLine($"Raw: {test}");
                Debug.WriteLine($"FieldType: {parsed.FieldType}");
                Debug.WriteLine($"Context: {parsed.Context}");
                Debug.WriteLine($"InstanceIndex: {parsed.InstanceIndex}");
                Debug.WriteLine($"IsGeneric: {parsed.IsGeneric}");
            }
            else
            {
                Debug.WriteLine("Parsing failed.");
            }
        }
    }
}
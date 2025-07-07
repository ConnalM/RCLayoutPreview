using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers
{
    public class ParsedField
    {
        public string BaseName { get; set; }
        public int InstanceIndex { get; set; }
        
        // Returns the name as it would appear in the JSON file
        // The new format simply removes _number suffix
        public string JsonName => BaseName;
    }

    public class FieldNameParser
    {
        /// <summary>
        /// Parse a field name by stripping the trailing _N suffix if present
        /// This is the only transformation needed for the new format
        /// </summary>
        public static bool TryParse(string rawName, out ParsedField parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(rawName))
                return false;
                
            // Match standard pattern fieldName_N where N is a number
            var match = Regex.Match(rawName, @"^(.+)_(\d+)$");
            
            if (match.Success)
            {
                // Extract the base name without the _N suffix
                string baseName = match.Groups[1].Value;
                int instanceIndex = int.Parse(match.Groups[2].Value);
                
                parsed = new ParsedField
                {
                    BaseName = baseName,
                    InstanceIndex = instanceIndex
                };
                
                Debug.WriteLine($"Parsed field: {rawName} -> BaseName={parsed.BaseName}, JsonName={parsed.JsonName}");
                return true;
            }
            
            // If no suffix, use the field name as is
            parsed = new ParsedField
            {
                BaseName = rawName,
                InstanceIndex = 1 // Default instance
            };
            
            Debug.WriteLine($"Direct field: {rawName}, JsonName={parsed.JsonName}");
            return true;
        }

        // Add a test method to verify parsing logic
        public static void TestFieldNameParser()
        {
            string[] tests = {
                "NextHeatNumber_1", 
                "NextHeatNickname1_1",
                "NextHeatNickname2_1",
                "NextHeatNickname_1",
                "Nickname_Lane1_1",
                "RaceName_1",
                "PlainField"
            };
            
            Debug.WriteLine("====== FIELD PARSER TEST =====");
            foreach (var test in tests)
            {
                if (TryParse(test, out var parsed))
                {
                    Debug.WriteLine($"Raw: {test}");
                    Debug.WriteLine($"BaseName: {parsed.BaseName}");
                    Debug.WriteLine($"JsonName: {parsed.JsonName}");
                    Debug.WriteLine($"InstanceIndex: {parsed.InstanceIndex}");
                    Debug.WriteLine("-------------------");
                }
                else
                {
                    Debug.WriteLine($"Parsing failed for: {test}");
                }
            }
            Debug.WriteLine("====== END FIELD PARSER TEST =====");
        }
    }
}
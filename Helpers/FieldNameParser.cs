using System.Text.RegularExpressions;
using System.Diagnostics; // Ensure this is added

namespace RCLayoutPreview.Helpers // Updated namespace to avoid conflict
{
    public class ParsedField
    {
        public string BaseName { get; set; }
        public string QualifierType { get; set; }
        public int? QualifierIndex { get; set; }
        public int InstanceIndex { get; set; }
        public bool IsGeneric { get; set; }
    }

    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        public string Context { get; private set; }
        public int InstanceIndex { get; private set; }
        public bool IsGeneric { get; private set; }
        public string BaseName => FieldType; // Add this for compatibility

        public static bool TryParse(string rawName, out ParsedField parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(rawName))
                return false;

            // Updated regex to correctly parse names like "NextHeatNickname1_1"
            string pattern = @"^(?<field>[A-Za-z]+?(?:\d+)?)(?:_(?<qualifier>[A-Za-z]+)(?<qualifierIndex>\d*))?(?:_(?<instanceIndex>\d+))?$";
            var match = Regex.Match(rawName, pattern);

            if (match.Success)
            {
                string field = match.Groups["field"].Value;
                string qualifier = match.Groups["qualifier"].Value;
                string qualifierIndex = match.Groups["qualifierIndex"].Value;
                string instanceIndex = match.Groups["instanceIndex"].Value;

                parsed = new ParsedField
                {
                    BaseName = field,
                    QualifierType = qualifier,
                    QualifierIndex = string.IsNullOrEmpty(qualifierIndex) ? null : int.Parse(qualifierIndex),
                    InstanceIndex = string.IsNullOrEmpty(instanceIndex) ? 1 : int.Parse(instanceIndex),
                    IsGeneric = string.IsNullOrEmpty(qualifier) && string.IsNullOrEmpty(qualifierIndex)
                };
                return true;
            }

            return false;
        }

        // Add a simple test method to verify parsing logic
        public static void TestFieldNameParser()
        {
            string test = "NextHeatNickname1_1";
            if (TryParse(test, out var parsed))
            {
                Debug.WriteLine($"Raw: {test}");
                Debug.WriteLine($"BaseName: {parsed.BaseName}");
                Debug.WriteLine($"QualifierType: {parsed.QualifierType}");
                Debug.WriteLine($"QualifierIndex: {parsed.QualifierIndex}");
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
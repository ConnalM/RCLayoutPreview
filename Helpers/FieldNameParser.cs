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

            Debug.WriteLine($"[FieldNameParser] Raw field name: {rawName}");

            if (string.IsNullOrWhiteSpace(rawName))
            {
                Debug.WriteLine($"[FieldNameParser] Invalid field name: {rawName}");
                return false;
            }

            // First try to match format with suffix index: e.g. NextHeatNickname1_1
            var suffixMatch = Regex.Match(rawName, @"^(.+\d+)_(\d+)$");
            if (suffixMatch.Success)
            {
                string fieldType = suffixMatch.Groups[1].Value;
                int instanceIndex = 1;
                int.TryParse(suffixMatch.Groups[2].Value, out instanceIndex);
                parsed = new FieldNameParser
                {
                    FieldType = fieldType,
                    InstanceIndex = instanceIndex
                };
                Debug.WriteLine($"[FieldNameParser] Parsed field type: {parsed.FieldType}, InstanceIndex: {parsed.InstanceIndex}");
                return true;
            }

            // Second try to match format without suffix index: e.g. Nickname_Position1
            var simpleMatch = Regex.Match(rawName, @"^(.*?)(?:_(\d+))?$");
            if (simpleMatch.Success)
            {
                string fieldType = simpleMatch.Groups[1].Value;
                int instanceIndex = 1;
                if (simpleMatch.Groups[2].Success)
                {
                    int.TryParse(simpleMatch.Groups[2].Value, out instanceIndex);
                }
                parsed = new FieldNameParser
                {
                    FieldType = fieldType,
                    InstanceIndex = instanceIndex
                };
                Debug.WriteLine($"[FieldNameParser] Parsed field type: {parsed.FieldType}, InstanceIndex: {parsed.InstanceIndex}");
                return true;
            }

            Debug.WriteLine($"[FieldNameParser] Could not parse field name: {rawName}");
            return false;
        }
    }
}
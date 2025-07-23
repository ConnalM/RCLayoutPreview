using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RCLayoutPreview.Helpers // Updated namespace to avoid conflict
{
    /// <summary>
    /// Parses Race Coordinator field names into type and index for stubdata lookup.
    /// </summary>
    public class FieldNameParser
    {
        /// <summary>
        /// The parsed field type (e.g. LapTime_1)
        /// </summary>
        public string FieldType { get; private set; }
        /// <summary>
        /// The instance index (default 1)
        /// </summary>
        public int InstanceIndex { get; private set; } = 1;

        /// <summary>
        /// Attempts to parse a raw field name into type and index.
        /// </summary>
        /// <param name="rawName">Raw field name string</param>
        /// <param name="parsed">Parsed FieldNameParser object</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
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
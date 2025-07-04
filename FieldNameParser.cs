using System.Text.RegularExpressions;

namespace RCLayoutPreview.Helpers
{
    public class ParsedField
    {
        public string BaseName { get; set; }             // e.g. LapTime
        public string QualifierType { get; set; }        // e.g. Lane, Position, RaceLeader
        public int? QualifierIndex { get; set; }         // e.g. 2
        public int InstanceIndex { get; set; } = 1;      // Default to 1
        public bool IsGeneric => QualifierType == null;
    }

    public static class FieldNameParserUtility
    {
        public static bool TryParse(string tag, out ParsedField result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            var parts = tag.Split('_');
            if (parts.Length < 1 || parts.Length > 3) return false;

            var baseName = parts[0];
            string qualifierType = null;
            int? qualifierIndex = null;
            int instanceIndex = 1;

            // Handle qualifier (e.g. Lane2)
            if (parts.Length >= 2 && TrySplitQualifier(parts[1], out var qType, out var qIndex))
            {
                qualifierType = qType;
                qualifierIndex = qIndex;
            }
            else if (parts.Length == 2 && int.TryParse(parts[1], out var indexOnly))
            {
                instanceIndex = indexOnly;
            }

            // Handle instance index
            if (parts.Length == 3 && int.TryParse(parts[2], out var idx))
            {
                instanceIndex = idx;
            }

            result = new ParsedField
            {
                BaseName = baseName,
                QualifierType = qualifierType,
                QualifierIndex = qualifierIndex,
                InstanceIndex = instanceIndex
            };

            return true;
        }

        private static bool TrySplitQualifier(string input, out string type, out int index)
        {
            type = null;
            index = 0;

            var match = Regex.Match(input, @"^([A-Za-z]+)(\d+)$");
            if (match.Success)
            {
                type = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
                return true;
            }

            return false;
        }
    }
}
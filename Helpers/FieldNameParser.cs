using System.Text.RegularExpressions;

namespace RCLayoutPreview.Helpers // Updated namespace to avoid conflict
{
    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        public string Context { get; private set; }
        public int InstanceIndex { get; private set; }
        public bool IsGeneric { get; private set; }

        public static bool TryParse(string rawName, out FieldNameParser parsed)
        {
            parsed = null;

            var match = Regex.Match(rawName, @"^(?<field>\w+?)(_(?<context>\w+?))?(_(?<index>\d+))?$");
            if (!match.Success) return false;

            parsed = new FieldNameParser
            {
                FieldType = match.Groups["field"].Value,
                Context = match.Groups["context"].Success ? match.Groups["context"].Value : null,
                InstanceIndex = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : 1,
                IsGeneric = string.IsNullOrEmpty(match.Groups["context"].Value)
            };

            return true;
        }
    }
}
using System.Text.RegularExpressions;

namespace YourNamespaceHere // 🔁 Replace with your actual namespace
{
    public class FieldNameParser
    {
        public string FieldType { get; private set; }
        public string Context { get; private set; } // e.g. Lane2, Position1, etc.
        public int InstanceIndex { get; private set; } = 1;

        public bool IsGeneric => string.IsNullOrEmpty(Context);

        public static bool TryParse(string rawName, out FieldNameParser parsed)
        {
            parsed = null;

            var match = Regex.Match(rawName, @"^(?<field>\w+?)(_(?<context>\w+?))?(_(?<index>\d+))?$");
            if (!match.Success) return false;

            parsed = new FieldNameParser
            {
                FieldType = match.Groups["field"].Value,
                Context = match.Groups["context"].Success ? match.Groups["context"].Value : null,
                InstanceIndex = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : 1
            };

            return true;
        }
    }
}
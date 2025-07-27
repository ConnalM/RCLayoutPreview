using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace RCLayoutPreview.Helpers
{
    public static class StubDataService
    {
        public static JObject LoadStubData(string baseDirectory, Action<string> updateStatus)
        {
            string jsonPath = Path.Combine(baseDirectory, AppConstants.StubDataFileName);
            updateStatus?.Invoke($"Checking path: {jsonPath}");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    updateStatus?.Invoke($"Loaded JSON: {Path.GetFileName(jsonPath)}");
                    return JObject.Parse(jsonContent);
                }
                catch (Exception ex)
                {
                    updateStatus?.Invoke($"Error parsing JSON file: {ex.Message}");
                }
            }
            else
            {
                updateStatus?.Invoke($"File does not exist: {jsonPath}");
            }
            return null;
        }
    }
}

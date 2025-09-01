using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SK_Share_Settings
{
    [Serializable]
    public class AllModsSettingsSchema
    {
        public string settingsFileVersion;
        public string rimworldVersion;
        public string exportTimestamp;
        public SettingsSchema[] modSettings;

        public string SettingsFileVersion { get => settingsFileVersion; }
        public string RimworldVersion { get => rimworldVersion; }
        public string ExportTimestamp { get => exportTimestamp; }
        public SettingsSchema[] ModSettings { get => modSettings; }

        public AllModsSettingsSchema()
        {
        }

        public AllModsSettingsSchema(string settingsFileVersion, string rimworldVersion, string exportTimestamp, SettingsSchema[] modSettings)
        {
            this.settingsFileVersion = settingsFileVersion;
            this.rimworldVersion = rimworldVersion;
            this.exportTimestamp = exportTimestamp;
            this.modSettings = modSettings;
        }

        public string ToJson()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{");

                sb.AppendFormat("\"settingsFileVersion\":\"{0}\",", EscapeJsonString(settingsFileVersion ?? ""));
                sb.AppendFormat("\"rimworldVersion\":\"{0}\",", EscapeJsonString(rimworldVersion ?? ""));
                sb.AppendFormat("\"exportTimestamp\":\"{0}\",", EscapeJsonString(exportTimestamp ?? ""));

                sb.Append("\"modSettings\":[");

                if (modSettings != null && modSettings.Length > 0)
                {
                    for (int i = 0; i < modSettings.Length; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(modSettings[i].ToJson());
                    }
                }

                sb.Append("]}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }

        public static AllModsSettingsSchema FromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var result = new AllModsSettingsSchema();

                result.settingsFileVersion = ExtractJsonStringValue(json, "settingsFileVersion");
                result.rimworldVersion = ExtractJsonStringValue(json, "rimworldVersion");
                result.exportTimestamp = ExtractJsonStringValue(json, "exportTimestamp");

                result.modSettings = ExtractModSettingsArray(json);

                return result.IsValid() ? result : null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static string ExtractJsonStringValue(string json, string key)
        {
            try
            {
                string searchPattern = $"\"{key}\":\"";
                int startIndex = json.IndexOf(searchPattern);
                if (startIndex == -1) return null;

                startIndex += searchPattern.Length;
                int endIndex = startIndex;

                // Find the end of the string value, handling escaped quotes
                while (endIndex < json.Length)
                {
                    if (json[endIndex] == '"' && (endIndex == 0 || json[endIndex - 1] != '\\'))
                    {
                        break;
                    }
                    endIndex++;
                }

                if (endIndex >= json.Length) return null;

                string value = json.Substring(startIndex, endIndex - startIndex);
                return UnescapeJsonString(value);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SettingsSchema[] ExtractModSettingsArray(string json)
        {
            try
            {
                string arrayStart = "\"modSettings\":[";
                int startIndex = json.IndexOf(arrayStart);
                if (startIndex == -1) return null;

                startIndex += arrayStart.Length;

                // Find the matching closing bracket for the array
                int bracketCount = 1;
                int endIndex = startIndex;

                while (endIndex < json.Length && bracketCount > 0)
                {
                    char c = json[endIndex];
                    if (c == '[') bracketCount++;
                    else if (c == ']') bracketCount--;
                    endIndex++;
                }

                if (bracketCount != 0) return null; // Malformed JSON

                string arrayContent = json.Substring(startIndex, endIndex - startIndex - 1);

                if (string.IsNullOrWhiteSpace(arrayContent) || arrayContent.Trim() == "")
                    return new SettingsSchema[0];

                // Split the array content into individual JSON objects
                var jsonObjects = SplitJsonArray(arrayContent);
                var settingsArray = new SettingsSchema[jsonObjects.Count];

                for (int i = 0; i < jsonObjects.Count; i++)
                {
                    settingsArray[i] = SettingsSchema.FromJson(jsonObjects[i]);
                }

                return settingsArray;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static List<string> SplitJsonArray(string arrayContent)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(arrayContent))
                return result;

            int depth = 0;
            int start = 0;
            bool inString = false;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                char c = arrayContent[i];

                // Handle string boundaries
                if (c == '"' && (i == 0 || arrayContent[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;

                        // If we're back to depth 0, we've found a complete object
                        if (depth == 0)
                        {
                            string jsonObject = arrayContent.Substring(start, i - start + 1).Trim();
                            if (!string.IsNullOrEmpty(jsonObject))
                            {
                                result.Add(jsonObject);
                            }

                            // Skip any commas and whitespace to find the start of the next object
                            i++;
                            while (i < arrayContent.Length && (arrayContent[i] == ',' || char.IsWhiteSpace(arrayContent[i])))
                            {
                                i++;
                            }
                            start = i;
                            i--; // Adjust for the loop increment
                        }
                    }
                }
            }

            return result;
        }

        private static string UnescapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str.Replace("\\\\", "\\")
                     .Replace("\\\"", "\"")
                     .Replace("\\n", "\n")
                     .Replace("\\r", "\r")
                     .Replace("\\t", "\t");
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(settingsFileVersion) &&
                   !string.IsNullOrEmpty(rimworldVersion) &&
                   !string.IsNullOrEmpty(exportTimestamp) &&
                   modSettings != null &&
                   modSettings.Length > 0 &&
                   modSettings.All(schema => schema != null && schema.IsValid());
        }

        public int GetModCount()
        {
            return modSettings?.Length ?? 0;
        }

        public SettingsSchema GetSettingsForMod(string packageId)
        {
            if (modSettings == null || string.IsNullOrEmpty(packageId))
                return null;

            return modSettings.FirstOrDefault(schema =>
                string.Equals(schema.ModPackageId, packageId, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsModSettings(string packageId)
        {
            return GetSettingsForMod(packageId) != null;
        }
    }
}
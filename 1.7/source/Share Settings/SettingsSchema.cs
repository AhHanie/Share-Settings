using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace SK_Share_Settings
{
    [Serializable]
    public class SettingsSchema
    {
        // UnityStubs' JsonUtility serializes get-only properties and escapes '<' and non-ASCII as Unicode escapes; this keeps the
        // 1.6 wire format (fields only, minimal escaping) so clipboards stay readable in both versions.
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string settingsFileVersion;
        public string modVersion;
        public string modPackageId;
        public string settings;

        [JsonIgnore]
        public string ModVersion { get => modVersion; }
        [JsonIgnore]
        public string ModPackageId { get => modPackageId; }
        [JsonIgnore]
        public string Settings { get => settings; }
        [JsonIgnore]
        public string SettingsFileVersion { get => settingsFileVersion; }

        public SettingsSchema()
        {
        }

        public SettingsSchema(string settingsFileVersion, string modVersion, string modPackageId, string settings)
        {
            this.settingsFileVersion = settingsFileVersion;
            this.modVersion = modVersion;
            this.modPackageId = modPackageId;
            this.settings = settings;
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public static SettingsSchema FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<SettingsSchema>(json, JsonOptions);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(settingsFileVersion) &&
                   !string.IsNullOrEmpty(modVersion) &&
                   !string.IsNullOrEmpty(modPackageId) &&
                   !string.IsNullOrEmpty(settings) &&
                   IsValidXml(settings);
        }

        private bool IsValidXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            try
            {
                XDocument.Parse(xml);
                return xml.Contains("<SettingsBlock>") && xml.Contains("<ModSettings");
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
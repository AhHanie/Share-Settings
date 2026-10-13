using System;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace SK_Share_Settings
{
    [Serializable]
    public class SettingsSchema
    {
        public string settingsFileVersion;
        public string modVersion;
        public string modPackageId;
        public string settings;

        public string ModVersion { get => modVersion; }
        public string ModPackageId { get => modPackageId; }
        public string Settings { get => settings; }
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
            return JsonUtility.ToJson(this);
        }

        public static SettingsSchema FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<SettingsSchema>(json);
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
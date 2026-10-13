using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Verse;

namespace SK_Share_Settings
{
    public static class SettingsSerializer
    {
        public static string SerializeSettingsToXml(ModSettings settings)
        {
            if (settings == null)
            {
                Log.Warning("[SK Share Settings] Cannot serialize null settings");
                return null;
            }

            try
            {
                // Create a temporary file to write settings to, then read back as string
                string tempFile = Path.GetTempFileName();

                try
                {
                    // Use Scribe to save settings to temp file
                    Scribe.saver.InitSaving(tempFile, "SettingsBlock");
                    try
                    {
                        ModSettings settingsToSave = settings;
                        Scribe_Deep.Look(ref settingsToSave, "ModSettings");
                    }
                    finally
                    {
                        Scribe.saver.FinalizeSaving();
                    }

                    // Read the XML content back as string
                    string xmlContent = File.ReadAllText(tempFile);

                    // Compress the XML to make it more compact for copying
                    string compactXml = CompressXml(xmlContent);

                    return compactXml;
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch (Exception deleteEx)
                        {
                            Log.Warning($"[SK Share Settings] Failed to delete temp file {tempFile}: {deleteEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to serialize settings for {settings.GetType().Name}: {ex.Message}");
                Log.Error($"[SK Share Settings] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private static string CompressXml(string xml)
        {
            try
            {
                // Parse and reformat the XML to remove unnecessary whitespace
                var doc = XDocument.Parse(xml);
                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch (Exception ex)
            {
                Log.Warning($"[SK Share Settings] Failed to compress XML, using original: {ex.Message}");
                return xml; // Return original if compression fails
            }
        }

        public static bool DeserializeSettingsFromXml(string xmlContent, ModSettings targetSettings)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                Log.Warning("[SK Share Settings] Cannot deserialize empty or null XML content");
                return false;
            }

            if (targetSettings == null)
            {
                Log.Warning("[SK Share Settings] Cannot deserialize into null target settings");
                return false;
            }

            try
            {
                // Create a temporary file with the XML content
                string tempFile = Path.GetTempFileName();

                try
                {
                    // Write XML to temp file
                    File.WriteAllText(tempFile, xmlContent);

                    // Validate that we can read the file back
                    if (!File.Exists(tempFile))
                    {
                        Log.Error("[SK Share Settings] Temp file was not created successfully");
                        return false;
                    }

                    // Use Scribe to load settings from temp file
                    Scribe.loader.InitLoading(tempFile);
                    try
                    {
                        // Load the settings into the target instance
                        ModSettings loadedSettings = targetSettings;
                        Scribe_Deep.Look(ref loadedSettings, "ModSettings");
                        return true;
                    }
                    finally
                    {
                        Scribe.loader.FinalizeLoading();
                    }
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch (Exception deleteEx)
                        {
                            Log.Warning($"[SK Share Settings] Failed to delete temp file {tempFile}: {deleteEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to deserialize settings into {targetSettings.GetType().Name}: {ex.Message}");
                Log.Error($"[SK Share Settings] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
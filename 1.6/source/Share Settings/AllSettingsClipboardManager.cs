using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SK_Share_Settings
{
    public static class AllSettingsClipboardManager
    {
        private static bool cachedAllClipboardValid = false;
        private static float lastAllClipboardCheckTime = 0f;
        private const float CLIPBOARD_CHECK_INTERVAL = 1.0f; // Check every 1 second
        private static IEnumerable<Verse.Mod> cachedModsWithSettings;

        public static void CopyAllSettings()
        {
            try
            {
                var modsWithSettings = GetModsWithSettings();
                if (modsWithSettings == null || !modsWithSettings.Any())
                {
                    Messages.Message("SKShareSettings.NoModsWithSettings".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                List<SettingsSchema> allModSettings = new List<SettingsSchema>();

                int successCount = 0;
                int totalCount = 0;

                foreach (var mod in modsWithSettings)
                {
                    totalCount++;
                    ModSettings settings = GetModSettings(mod);
                    if (settings == null)
                    {
                        continue;
                    }

                    var schema = new SettingsSchema(
                        settingsFileVersion: "1.0",
                        modVersion: mod.Content.ModMetaData.ModVersion.Length > 0 ? mod.Content.ModMetaData.ModVersion : "noversion",
                        modPackageId: mod.Content.PackageId,
                        settings: SettingsSerializer.SerializeSettingsToXml(settings)
                    );

                    if (schema.IsValid())
                    {
                        allModSettings.Add(schema);
                        successCount++;
                    }
                }

                if (allModSettings.Count == 0)
                {
                    Messages.Message("SKShareSettings.NoValidSettingsToCopy".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                var allModsSchema = new AllModsSettingsSchema(
                    settingsFileVersion: "1.0",
                    rimworldVersion: VersionControl.CurrentVersionStringWithRev,
                    exportTimestamp: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    modSettings: allModSettings.ToArray()
                );

                if (!allModsSchema.IsValid())
                {
                    Messages.Message("SKShareSettings.CopyAllFailed".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }
                GUIUtility.systemCopyBuffer = allModsSchema.ToJson();

                Messages.Message("SKShareSettings.CopyAllSuccess".Translate(successCount, totalCount), MessageTypeDefOf.SilentInput);
                InvalidateAllClipboardCache();
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to copy all settings: {ex.Message}");
                Messages.Message("SKShareSettings.CopyAllException".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        public static void PasteAllSettings()
        {
            try
            {
                var clipboardSettings = GetAllSettingsFromClipboard();
                if (clipboardSettings == null)
                {
                    Messages.Message("SKShareSettings.NoValidAllClipboard".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                var modsWithSettings = GetModsWithSettings();
                if (modsWithSettings == null || !modsWithSettings.Any())
                {
                    Messages.Message("SKShareSettings.NoModsWithSettings".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                var currentModsByPackageId = modsWithSettings.ToDictionary(mod => mod.Content.PackageId, mod => mod);
                int matchingMods = 0;
                int totalClipboardMods = clipboardSettings.GetModCount();

                foreach (var clipboardModSchema in clipboardSettings.ModSettings)
                {
                    if (currentModsByPackageId.ContainsKey(clipboardModSchema.ModPackageId))
                    {
                        matchingMods++;
                    }
                }

                if (matchingMods == 0)
                {
                    Messages.Message("SKShareSettings.NoMatchingMods".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                string confirmationMessage = "SKShareSettings.ConfirmPasteAll".Translate(
                    matchingMods,
                    totalClipboardMods,
                    clipboardSettings.RimworldVersion,
                    clipboardSettings.ExportTimestamp
                );

                if (matchingMods < totalClipboardMods)
                {
                    int missingMods = totalClipboardMods - matchingMods;
                    confirmationMessage += "SKShareSettings.MissingModsWarning".Translate(missingMods);
                }

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    confirmationMessage,
                    delegate
                    {
                        ApplyAllPastedSettings(clipboardSettings, currentModsByPackageId);
                    }
                ));
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to paste all settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteAllException".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        public static bool HasValidAllSettingsInClipboardCached()
        {
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastAllClipboardCheckTime >= CLIPBOARD_CHECK_INTERVAL)
            {
                cachedAllClipboardValid = HasValidAllSettingsInClipboard();
                lastAllClipboardCheckTime = currentTime;
            }

            return cachedAllClipboardValid;
        }

        public static void InvalidateAllClipboardCache()
        {
            lastAllClipboardCheckTime = 0f;
            cachedAllClipboardValid = false;
        }

        private static void ApplyAllPastedSettings(AllModsSettingsSchema clipboardSettings, Dictionary<string, Verse.Mod> currentModsByPackageId)
        {
            try
            {
                int successCount = 0;
                int attemptCount = 0;

                foreach (var clipboardModSchema in clipboardSettings.ModSettings)
                {
                    if (!currentModsByPackageId.TryGetValue(clipboardModSchema.ModPackageId, out Verse.Mod currentMod))
                    {
                        continue;
                    }

                    attemptCount++;

                    try
                    {
                        ModSettings currentSettings = GetModSettings(currentMod);
                        if (currentSettings == null)
                        {
                            continue;
                        }

                        HarmonyPatcher.deserializingNow = true;
                        HarmonyPatcher.cachedSettings = currentSettings;
                        bool success = SettingsSerializer.DeserializeSettingsFromXml(clipboardModSchema.Settings, currentSettings);
                        HarmonyPatcher.deserializingNow = false;

                        if (success)
                        {
                            currentMod.WriteSettings();
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SK Share Settings] Exception applying settings for mod {currentMod.Content.Name}: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    Messages.Message("SKShareSettings.PasteAllSuccess".Translate(successCount, attemptCount), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("SKShareSettings.PasteAllFailed".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to apply all pasted settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteAllApplyFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private static bool HasValidAllSettingsInClipboard()
        {
            var clipboardSettings = GetAllSettingsFromClipboard();
            return clipboardSettings != null && clipboardSettings.IsValid();
        }

        private static AllModsSettingsSchema GetAllSettingsFromClipboard()
        {
            try
            {
                string clipboardText = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrWhiteSpace(clipboardText))
                    return null;

                var schema = AllModsSettingsSchema.FromJson(clipboardText);
                return schema?.IsValid() == true ? schema : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IEnumerable<Verse.Mod> GetModsWithSettings()
        {
            try
            {
                if (cachedModsWithSettings == null)
                {
                    cachedModsWithSettings = LoadedModManager.ModHandles
                        .Where(mod => !mod.SettingsCategory().NullOrEmpty())
                        .GroupBy(mod => mod.Content.PackageId)
                        .Select(group => group.First()) // Why are there duplicate mods ... ??
                        .OrderBy(mod => mod.SettingsCategory())
                        .ToList();
                }
                return cachedModsWithSettings;
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to get mods with settings: {ex.Message}");
                return Enumerable.Empty<Verse.Mod>();
            }
        }

        private static ModSettings GetModSettings(Verse.Mod mod)
        {
            if (mod == null || HarmonyPatcher.modSettingsField == null)
                return null;

            return (ModSettings)HarmonyPatcher.modSettingsField.GetValue(mod);
        }
    }
}
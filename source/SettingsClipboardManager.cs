using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace SK_Share_Settings
{
    public static class SettingsClipboardManager
    {
        private static bool cachedClipboardValid = false;
        private static float lastClipboardCheckTime = 0f;
        private const float CLIPBOARD_CHECK_INTERVAL = 1.0f; // Check every 1 second

        public static void CopySettings(Mod mod, Dialog_ModSettings dialog)
        {
            try
            {
                CloseCurrentDialog(dialog);

                ModSettings settings = GetModSettings(mod);
                if (settings == null)
                {
                    Messages.Message("SKShareSettings.NoSettingsToCopy".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog(mod);
                    return;
                }

                var schema = new SettingsSchema(
                    settingsFileVersion: "1.0",
                    modVersion: mod.Content.ModMetaData.ModVersion.Length > 0 ? mod.Content.ModMetaData.ModVersion : "noversion",
                    modPackageId: mod.Content.PackageId,
                    settings: SettingsSerializer.SerializeSettingsToXml(settings)
                );

                if (!schema.IsValid())
                {
                    Messages.Message("SKShareSettings.CopyFailed".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog(mod);
                    return;
                }

                // Copy JSON to clipboard
                GUIUtility.systemCopyBuffer = schema.ToJson();

                Messages.Message("SKShareSettings.CopySuccess".Translate(mod.Content.Name, schema.ModVersion), MessageTypeDefOf.SilentInput);

                InvalidateClipboardCache();
                ReopenDialog(mod);
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to copy settings: {ex.Message}");
                Messages.Message("SKShareSettings.CopyException".Translate(), MessageTypeDefOf.RejectInput);
                ReopenDialog(mod);
            }
        }

        public static void PasteSettings(Mod mod, Dialog_ModSettings dialog)
        {
            try
            {
                var clipboardSettings = GetSettingsFromClipboard();
                if (clipboardSettings == null)
                {
                    Messages.Message("SKShareSettings.NoValidClipboard".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                string currentPackageId = mod.Content.PackageId;
                string clipboardPackageId = clipboardSettings.ModPackageId;

                if (!string.Equals(currentPackageId, clipboardPackageId, StringComparison.OrdinalIgnoreCase))
                {
                    Messages.Message("SKShareSettings.PackageIdMismatch".Translate(currentPackageId, clipboardPackageId), MessageTypeDefOf.RejectInput);
                    return;
                }

                string currentModVersion = mod.Content.ModMetaData.ModVersion.Length > 0 ? mod.Content.ModMetaData.ModVersion : "noversion";
                string sourceModVersion = clipboardSettings.ModVersion;

                string confirmationMessage = "SKShareSettings.ConfirmPaste".Translate(mod.Content.Name, clipboardSettings.ModVersion);

                bool versionMismatch = !string.Equals(currentModVersion, sourceModVersion, StringComparison.OrdinalIgnoreCase);
                if (versionMismatch)
                {
                    confirmationMessage += "SKShareSettings.VersionMismatchWarning".Translate(currentModVersion, sourceModVersion);
                }

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    confirmationMessage,
                    delegate
                    {
                        ApplyPastedSettings(mod, clipboardSettings, dialog);
                    }
                ));
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to paste settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteException".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        public static bool HasValidSettingsInClipboardCached(Mod mod)
        {
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastClipboardCheckTime >= CLIPBOARD_CHECK_INTERVAL)
            {
                cachedClipboardValid = HasValidSettingsInClipboard(mod);
                lastClipboardCheckTime = currentTime;
            }

            return cachedClipboardValid;
        }

        public static void InvalidateClipboardCache()
        {
            lastClipboardCheckTime = 0f;
            cachedClipboardValid = false;
        }

        private static void ApplyPastedSettings(Mod mod, SettingsSchema clipboardSettings, Dialog_ModSettings dialog)
        {
            try
            {
                CloseCurrentDialog(dialog);

                ModSettings currentSettings = GetModSettings(mod);
                if (currentSettings == null)
                {
                    Messages.Message("SKShareSettings.NoCurrentSettings".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog(mod);
                    return;
                }

                HarmonyPatcher.deserializingNow = true;
                HarmonyPatcher.cachedSettings = currentSettings;
                bool success = SettingsSerializer.DeserializeSettingsFromXml(clipboardSettings.Settings, currentSettings);
                HarmonyPatcher.deserializingNow = false;

                if (success)
                {
                    mod.WriteSettings();

                    string currentModName = mod.Content.ModMetaData.Name;
                    string sourceModVersion = clipboardSettings.ModVersion;

                    Messages.Message("SKShareSettings.PasteSuccess".Translate(sourceModVersion, currentModName), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("SKShareSettings.PasteApplyFailed".Translate(), MessageTypeDefOf.RejectInput);
                }

                ReopenDialog(mod);
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to apply pasted settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteApplyFailed".Translate(), MessageTypeDefOf.RejectInput);
                ReopenDialog(mod);
            }
        }

        private static bool HasValidSettingsInClipboard(Mod mod)
        {
            var clipboardSettings = GetSettingsFromClipboard();
            if (clipboardSettings == null || !clipboardSettings.IsValid())
                return false;

            if (mod?.Content?.PackageId != null)
            {
                return string.Equals(mod.Content.PackageId, clipboardSettings.ModPackageId, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static SettingsSchema GetSettingsFromClipboard()
        {
            try
            {
                string clipboardText = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrWhiteSpace(clipboardText))
                    return null;

                var schema = SettingsSchema.FromJson(clipboardText);
                return schema?.IsValid() == true ? schema : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ModSettings GetModSettings(Mod mod)
        {
            if (mod == null || HarmonyPatcher.modSettingsField == null)
                return null;

            return (ModSettings)HarmonyPatcher.modSettingsField.GetValue(mod);
        }

        private static void CloseCurrentDialog(Dialog_ModSettings dialog)
        {
            if (dialog != null)
            {
                HarmonyPatcher.lockCurrentMod = true;
                dialog.Close(doCloseSound: false);
                HarmonyPatcher.lockCurrentMod = false;
            }
        }

        private static void ReopenDialog(Mod mod)
        {
            if (mod != null)
            {
                // Small delay to ensure the dialog is fully closed, then reopen
                LongEventHandler.QueueLongEvent(delegate
                {
                    Find.WindowStack.Add(new Dialog_ModSettings(mod));
                }, "SKShareSettings.RefreshingSettings".Translate(), false, null);
            }
        }
    }
}
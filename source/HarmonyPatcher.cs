using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace SK_Share_Settings
{
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(Dialog_ModSettings))]
    public class HarmonyPatcher
    {
        public static Mod currentMod;
        public static Dialog_ModSettings currentDialog;
        public const float BUTTON_AREA_HEIGHT = 50f;
        public static bool lockCurrentMod = false;
        public static bool deserializingNow = false;
        public static ModSettings cachedSettings;

        private static Harmony harmonyInstance;
        private static FieldInfo modSettingsField;

        private static bool cachedClipboardValid = false;
        private static float lastClipboardCheckTime = 0f;
        private const float CLIPBOARD_CHECK_INTERVAL = 1.0f; // Check every 1 second

        static HarmonyPatcher()
        {
            harmonyInstance = new Harmony("rimworld.sk.sharesettings");
            harmonyInstance.PatchAll();

            modSettingsField = typeof(Verse.Mod).GetField("modSettings", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, typeof(Mod))]
        public static void Constructor_Postfix(Dialog_ModSettings __instance, Mod mod)
        {
            currentMod = mod;
            currentDialog = __instance;
            InvalidateClipboardCache();
        }

        [HarmonyPostfix]
        [HarmonyPatch("DoWindowContents")]
        public static void DoWindowContents_Postfix(Dialog_ModSettings __instance, Rect inRect)
        {
            if (currentMod == null) return;

            const float buttonWidth = 120f;
            const float buttonHeight = 30f;

            Rect pasteButtonRect = new Rect(
                inRect.width - buttonWidth,
                inRect.height - buttonHeight,
                buttonWidth,
                buttonHeight
            );

            Rect copyButtonRect = new Rect(
                inRect.width - buttonWidth - buttonWidth,
                inRect.height - buttonHeight,
                buttonWidth,
                buttonHeight
            );

            if (Widgets.ButtonText(copyButtonRect, "SKShareSettings.CopySettings".Translate()))
            {
                CopySettings();
            }

            // Paste Settings Button - only enabled if clipboard has valid settings
            // Use cached validation result to avoid expensive checks every frame
            GUI.enabled = HasValidSettingsInClipboardCached();
            if (Widgets.ButtonText(pasteButtonRect, "SKShareSettings.PasteSettings".Translate()))
            {
                PasteSettings();
            }
            GUI.enabled = true;

            if (Mouse.IsOver(copyButtonRect))
            {
                TooltipHandler.TipRegion(copyButtonRect, "SKShareSettings.CopyTooltip".Translate());
            }

            if (Mouse.IsOver(pasteButtonRect))
            {
                string tooltip = HasValidSettingsInClipboardCached() ?
                    "SKShareSettings.PasteTooltip".Translate() :
                    "SKShareSettings.PasteTooltipNoSettings".Translate();
                TooltipHandler.TipRegion(pasteButtonRect, tooltip);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("PreClose")]
        public static void PreClose_Postfix(Dialog_ModSettings __instance)
        {
            if (!lockCurrentMod)
            {
                currentMod = null;
            }
            currentDialog = null;
        }

        private static void CopySettings()
        {
            try
            {
                CloseCurrentDialog();

                ModSettings settings = GetCurrentModSettings();
                if (settings == null)
                {
                    Messages.Message("SKShareSettings.NoSettingsToCopy".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog();
                    return;
                }

                var schema = new SettingsSchema(
                    settingsFileVersion: "1.0",
                    modVersion: currentMod.Content.ModMetaData.ModVersion.Length > 0 ? currentMod.Content.ModMetaData.ModVersion : "noversion",
                    modPackageId: currentMod.Content.PackageId,
                    settings: SettingsSerializer.SerializeSettingsToXml(settings)
                );

                if (!schema.IsValid())
                {
                    Messages.Message("SKShareSettings.CopyFailed".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog();
                    return;
                }

                // Copy JSON to clipboard
                GUIUtility.systemCopyBuffer = schema.ToJson();

                Messages.Message("SKShareSettings.CopySuccess".Translate(currentMod.Content.Name, schema.ModVersion), MessageTypeDefOf.SilentInput);

                InvalidateClipboardCache();
                ReopenDialog();
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to copy settings: {ex.Message}");
                Messages.Message("SKShareSettings.CopyException".Translate(), MessageTypeDefOf.RejectInput);
                ReopenDialog();
            }
        }

        private static void PasteSettings()
        {
            try
            {
                var clipboardSettings = GetSettingsFromClipboard();
                if (clipboardSettings == null)
                {
                    Messages.Message("SKShareSettings.NoValidClipboard".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                string currentPackageId = currentMod.Content.PackageId;
                string clipboardPackageId = clipboardSettings.ModPackageId;

                if (!string.Equals(currentPackageId, clipboardPackageId, StringComparison.OrdinalIgnoreCase))
                {
                    Messages.Message("SKShareSettings.PackageIdMismatch".Translate(currentPackageId, clipboardPackageId), MessageTypeDefOf.RejectInput);
                    return;
                }

                string currentModVersion = currentMod.Content.ModMetaData.ModVersion.Length > 0 ? currentMod.Content.ModMetaData.ModVersion : "noversion";
                string sourceModVersion = clipboardSettings.ModVersion;

                string confirmationMessage = "SKShareSettings.ConfirmPaste".Translate(currentMod.Content.Name, clipboardSettings.ModVersion);

                bool versionMismatch = !string.Equals(currentModVersion, sourceModVersion, StringComparison.OrdinalIgnoreCase);
                if (versionMismatch)
                {
                    confirmationMessage += "SKShareSettings.VersionMismatchWarning".Translate(currentModVersion, sourceModVersion);
                }

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    confirmationMessage,
                    delegate
                    {
                        ApplyPastedSettings(clipboardSettings);
                    }
                ));
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to paste settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteException".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private static void ApplyPastedSettings(SettingsSchema clipboardSettings)
        {
            try
            {
                CloseCurrentDialog();

                ModSettings currentSettings = GetCurrentModSettings();
                if (currentSettings == null)
                {
                    Messages.Message("SKShareSettings.NoCurrentSettings".Translate(), MessageTypeDefOf.RejectInput);
                    ReopenDialog();
                    return;
                }

                deserializingNow = true;
                cachedSettings = currentSettings;
                bool success = SettingsSerializer.DeserializeSettingsFromXml(clipboardSettings.Settings, currentSettings);
                deserializingNow = false;

                if (success)
                {
                    currentMod.WriteSettings();

                    string currentModName = currentMod.Content.ModMetaData.Name;
                    string sourceModVersion = clipboardSettings.ModVersion;

                    Messages.Message("SKShareSettings.PasteSuccess".Translate(sourceModVersion, currentModName), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("SKShareSettings.PasteApplyFailed".Translate(), MessageTypeDefOf.RejectInput);
                }

                ReopenDialog();
            }
            catch (Exception ex)
            {
                Log.Error($"[SK Share Settings] Failed to apply pasted settings: {ex.Message}");
                Messages.Message("SKShareSettings.PasteApplyFailed".Translate(), MessageTypeDefOf.RejectInput);
                ReopenDialog();
            }
        }

        private static ModSettings GetCurrentModSettings()
        {
            if (currentMod == null || modSettingsField == null)
                return null;

            return (ModSettings)modSettingsField.GetValue(currentMod);
        }

        private static void CloseCurrentDialog()
        {
            lockCurrentMod = true;
            currentDialog.Close(doCloseSound: false);
            lockCurrentMod = false;
        }

        private static void ReopenDialog()
        {
            if (currentMod != null)
            {
                // Small delay to ensure the dialog is fully closed, then reopen
                LongEventHandler.QueueLongEvent(delegate
                {
                    Find.WindowStack.Add(new Dialog_ModSettings(currentMod));
                }, "SKShareSettings.RefreshingSettings".Translate(), false, null);
            }
        }

        private static bool HasValidSettingsInClipboardCached()
        {
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastClipboardCheckTime >= CLIPBOARD_CHECK_INTERVAL)
            {
                cachedClipboardValid = HasValidSettingsInClipboard();
                lastClipboardCheckTime = currentTime;
            }

            return cachedClipboardValid;
        }

        private static void InvalidateClipboardCache()
        {
            lastClipboardCheckTime = 0f;
            cachedClipboardValid = false;
        }

        private static bool HasValidSettingsInClipboard()
        {
            var clipboardSettings = GetSettingsFromClipboard();
            if (clipboardSettings == null || !clipboardSettings.IsValid())
                return false;

            if (currentMod?.Content?.PackageId != null)
            {
                return string.Equals(currentMod.Content.PackageId, clipboardSettings.ModPackageId, StringComparison.OrdinalIgnoreCase);
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
    }

    // Patch Window.PreOpen to increase height for Dialog_ModSettings instances
    [HarmonyPatch(typeof(Window))]
    public class WindowPreOpenPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch("PreOpen")]
        public static void PreOpen_Postfix(Window __instance)
        {
            if (__instance is Dialog_ModSettings && HarmonyPatcher.currentMod != null)
            {
                // Increase the window height to accommodate our buttons
                __instance.windowRect.height += HarmonyPatcher.BUTTON_AREA_HEIGHT;
            }
        }
    }

    // When writing to mod settings, scribe creates a new instance. We want it modify the existing one.
    [HarmonyPatch(typeof(ScribeExtractor))]
    public class ScribeExtractorCreateInstancePatcher
    {
        [HarmonyPrefix]
        [HarmonyPatch("CreateInstance", MethodType.Normal)]
        [HarmonyPatch(new Type[] { typeof(Type), typeof(object[]) })]
        public static bool CreateInstance_Prefix(Type type, object[] ctorArgs, ref IExposable __result)
        {
            if (!HarmonyPatcher.deserializingNow)
                return true;

            if (HarmonyPatcher.cachedSettings != null)
            {
                __result = HarmonyPatcher.cachedSettings;
                return false;
            }

            return true;
        }
    }
}
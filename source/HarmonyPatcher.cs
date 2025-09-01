using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace SK_Share_Settings
{
    [HarmonyPatch(typeof(Dialog_ModSettings))]
    public class HarmonyPatcher
    {
        public static Mod currentMod;
        public static Dialog_ModSettings currentDialog;
        public const float BUTTON_AREA_HEIGHT = 50f;
        public static bool lockCurrentMod = false;
        public static bool deserializingNow = false;
        public static ModSettings cachedSettings;

        public static FieldInfo modSettingsField;

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, typeof(Mod))]
        public static void Constructor_Postfix(Dialog_ModSettings __instance, Mod mod)
        {
            currentMod = mod;
            currentDialog = __instance;
            SettingsClipboardManager.InvalidateClipboardCache();
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
                SettingsClipboardManager.CopySettings(currentMod, currentDialog);
            }

            // Paste Settings Button - only enabled if clipboard has valid settings
            // Use cached validation result to avoid expensive checks every frame
            GUI.enabled = SettingsClipboardManager.HasValidSettingsInClipboardCached(currentMod);
            if (Widgets.ButtonText(pasteButtonRect, "SKShareSettings.PasteSettings".Translate()))
            {
                SettingsClipboardManager.PasteSettings(currentMod, currentDialog);
            }
            GUI.enabled = true;

            if (Mouse.IsOver(copyButtonRect))
            {
                TooltipHandler.TipRegion(copyButtonRect, "SKShareSettings.CopyTooltip".Translate());
            }

            if (Mouse.IsOver(pasteButtonRect))
            {
                string tooltip = SettingsClipboardManager.HasValidSettingsInClipboardCached(currentMod) ?
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

    [HarmonyPatch(typeof(Dialog_Options))]
    public class ModOptionsHarmonyPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("DoWindowContents")]
        public static void DoWindowContents_Postfix(Dialog_Options __instance, Rect inRect)
        {
            if (__instance.selectedCategory != OptionCategoryDefOf.Mods || __instance.selectedMod != null)
                return;

            const float buttonWidth = 120f;
            const float buttonHeight = 30f;

            float ourButtonY = inRect.yMax - buttonHeight;

            float rightEdge = inRect.width - 10f;

            Rect pasteAllButtonRect = new Rect(
                0,
                ourButtonY,
                buttonWidth,
                buttonHeight
            );

            Rect copyAllButtonRect = new Rect(
                0,
                ourButtonY - buttonHeight,
                buttonWidth,
                buttonHeight
            );

            if (Widgets.ButtonText(copyAllButtonRect, "SKShareSettings.CopyAllSettings".Translate()))
            {
                AllSettingsClipboardManager.CopyAllSettings();
            }

            GUI.enabled = AllSettingsClipboardManager.HasValidAllSettingsInClipboardCached();
            if (Widgets.ButtonText(pasteAllButtonRect, "SKShareSettings.PasteAllSettings".Translate()))
            {
                AllSettingsClipboardManager.PasteAllSettings();
            }
            GUI.enabled = true;

            if (Mouse.IsOver(copyAllButtonRect))
            {
                TooltipHandler.TipRegion(copyAllButtonRect, "SKShareSettings.CopyAllTooltip".Translate());
            }

            if (Mouse.IsOver(pasteAllButtonRect))
            {
                string tooltip = AllSettingsClipboardManager.HasValidAllSettingsInClipboardCached() ?
                    "SKShareSettings.PasteAllTooltip".Translate() :
                    "SKShareSettings.PasteAllTooltipNoSettings".Translate();
                TooltipHandler.TipRegion(pasteAllButtonRect, tooltip);
            }
        }
    }
}
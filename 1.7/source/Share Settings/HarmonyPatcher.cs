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
        public const float BUTTON_AREA_HEIGHT = 50f;
        public static bool deserializingNow = false;
        public static ModSettings cachedSettings;

        public static readonly FieldInfo modSettingsField = AccessTools.Field(typeof(Verse.Mod), "modSettings");

        public static void BeginInPlaceLoad(ModSettings target)
        {
            cachedSettings = target;
            deserializingNow = true;
        }

        public static void EndInPlaceLoad()
        {
            deserializingNow = false;
            cachedSettings = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch("DoWindowContents")]
        public static void DoWindowContents_Postfix(Dialog_ModSettings __instance, Rect inRect, Verse.Mod ___mod)
        {
            if (___mod == null) return;

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
                SettingsClipboardManager.CopySettings(___mod, __instance);
            }

            bool hasValidSettings = SettingsClipboardManager.HasValidSettingsInClipboardCached(___mod);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && hasValidSettings;
            if (Widgets.ButtonText(pasteButtonRect, "SKShareSettings.PasteSettings".Translate()))
            {
                SettingsClipboardManager.PasteSettings(___mod, __instance);
            }
            GUI.enabled = previousEnabled;

            if (Mouse.IsOver(copyButtonRect))
            {
                TooltipHandler.TipRegion(copyButtonRect, "SKShareSettings.CopyTooltip".Translate());
            }

            if (Mouse.IsOver(pasteButtonRect))
            {
                string tooltip = hasValidSettings ?
                    "SKShareSettings.PasteTooltip".Translate() :
                    "SKShareSettings.PasteTooltipNoSettings".Translate();
                TooltipHandler.TipRegion(pasteButtonRect, tooltip);
            }
        }
    }

    // Vanilla InitialSize is `new Vector2(900f, 700f)`; grow the 700f to make room for our buttons.
    [HarmonyPatch(typeof(Dialog_ModSettings), nameof(Dialog_ModSettings.InitialSize), MethodType.Getter)]
    public static class ModSettingsDialogHeightPatcher
    {
        [HarmonyInfix(700f)]
        public static float Postfix(float __result)
        {
            return __result + HarmonyPatcher.BUTTON_AREA_HEIGHT;
        }
    }

    // Vanilla Dialog_ModSettings.PreClose() calls mod.WriteSettings() with no error handling, and
    // WindowStack.TryRemove only removes the window from its internal list after PreClose() returns.
    // If the settings write throws (e.g. a transient IOException from an antivirus/indexer/cloud-sync
    // file lock) the dialog is stranded on the window stack and every later close attempt, including
    // our own close-then-reopen flow, fails the same way. Wrapping just that one call lets the
    // window finish closing.
    [HarmonyPatch(typeof(Dialog_ModSettings), nameof(Dialog_ModSettings.PreClose))]
    public static class ModSettingsDialogCloseWritePatcher
    {
        [HarmonyInfix(typeof(Verse.Mod), nameof(Verse.Mod.WriteSettings))]
        public static Exception Finalizer(Exception __exception, Verse.Mod __instance)
        {
            if (__exception != null)
            {
                Log.Error($"[SK Share Settings] Suppressed exception while writing settings of {__instance?.Content?.Name} on dialog close (would otherwise permanently strand the settings window): {__exception}");
            }

            return null;
        }
    }

    // When writing to mod settings, scribe creates a new instance. We want it modify the existing one.
    [HarmonyPatch(typeof(ScribeExtractor))]
    public class ScribeExtractorCreateInstancePatcher
    {
        [HarmonyPrefix]
        [HarmonyPatch("CreateInstance", MethodType.Normal)]
        [HarmonyPatch(new Type[] { typeof(Type), typeof(object[]) })]
        public static bool CreateInstance_Prefix(Type type, ref IExposable __result)
        {
            if (!HarmonyPatcher.deserializingNow)
                return true;

            ModSettings target = HarmonyPatcher.cachedSettings;
            if (target == null || type != target.GetType())
                return true;

            // The root settings node is instantiated before any nested IExposable, so hand out the
            // existing instance once and let everything nested be created normally.
            HarmonyPatcher.deserializingNow = false;
            __result = target;
            return false;
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

            bool hasValidSettings = AllSettingsClipboardManager.HasValidAllSettingsInClipboardCached();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && hasValidSettings;
            if (Widgets.ButtonText(pasteAllButtonRect, "SKShareSettings.PasteAllSettings".Translate()))
            {
                AllSettingsClipboardManager.PasteAllSettings();
            }
            GUI.enabled = previousEnabled;

            if (Mouse.IsOver(copyAllButtonRect))
            {
                TooltipHandler.TipRegion(copyAllButtonRect, "SKShareSettings.CopyAllTooltip".Translate());
            }

            if (Mouse.IsOver(pasteAllButtonRect))
            {
                string tooltip = hasValidSettings ?
                    "SKShareSettings.PasteAllTooltip".Translate() :
                    "SKShareSettings.PasteAllTooltipNoSettings".Translate();
                TooltipHandler.TipRegion(pasteAllButtonRect, tooltip);
            }
        }
    }
}

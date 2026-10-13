using HarmonyLib;
using System.Reflection;
using Verse;

namespace SK_Share_Settings
{
    public class Mod : Verse.Mod
    {
        private static Harmony harmonyInstance;
        public Mod(ModContentPack content)
            : base(content)
        {
            harmonyInstance = new Harmony("rimworld.sk.sharesettings");
            LongEventHandler.QueueLongEvent(Init, "Sk.Share_Settings.Init", doAsynchronously: true, null);
        }

        private static void Init()
        {
            harmonyInstance.PatchAll();
            HarmonyPatcher.modSettingsField = typeof(Verse.Mod).GetField("modSettings", BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
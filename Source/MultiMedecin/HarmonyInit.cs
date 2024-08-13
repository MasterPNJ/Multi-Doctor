using HarmonyLib;
using Verse;

namespace MultiMedecin
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.MasterPNJ.multimedecin");
            harmony.PatchAll();
        }
    }
}
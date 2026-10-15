using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(SurgeryOutcomeComp_SurgeonSuccessChance), nameof(SurgeryOutcomeComp_SurgeonSuccessChance.AffectQuality))]
    public static class Patch_SurgerySuccessPreview
    {
        public static bool Prefix(Pawn surgeon, Bill bill, ref float quality)
        {
            if (!SurgerySuccessPreview.TryGetSurgeonStat(bill, surgeon, out var stat)) return true;
            // GetQuality normally reads a cached stat. Only the UI preview uses the fresh team stat.
            quality *= stat;
            return false;
        }
    }
}

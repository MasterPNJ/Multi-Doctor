using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    // When a pawn dies, remove any surgery bills that explicitly require them as surgeon.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_ClearBills
    {
        public static void Prefix(Pawn __instance)
        {
            if (__instance.Map == null) return;
            foreach (var pawn in __instance.Map.mapPawns.AllPawnsSpawned)
            {
                var billStack = pawn.BillStack;
                if (billStack == null) continue;
                for (int i = billStack.Count - 1; i >= 0; i--)
                {
                    if (billStack[i] is BillMedicalEx medicalBill && medicalBill.surgeon == __instance)
                    {
                        billStack.Delete(billStack[i]);
                    }
                }
            }
        }
    }
}
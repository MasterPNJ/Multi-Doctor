using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_ClearBills
    {
        public static void Postfix(Pawn __instance)
        {
            // Preserve the operation and let another doctor take over after the surgeon dies.
            foreach (var map in Find.Maps)
                foreach (var patient in map.mapPawns.AllPawnsSpawned.ToList())
                    foreach (var bill in patient.BillStack?.Bills.OfType<BillMedicalEx>().ToList() ??
                        new System.Collections.Generic.List<BillMedicalEx>())
                    {
                        if (bill.surgeon == __instance)
                        {
                            SurgeryTeamUtility.StopSession(bill);
                            bill.surgeon = null;
                            Compat.SetPawnRestrictionSafe(bill, null);
                        }
                        bill.assignedDoctors?.Remove(__instance);
                    }

            var team = Find.World?.GetComponent<DefaultSurgeryTeamComponent>();
            if (team == null) return;
            team.defaultAssistants?.Remove(__instance);
            if (team.defaultLeadSurgeon != __instance) return;
            team.fastOperationEnabled = false;
            team.defaultLeadSurgeon = null;
        }
    }
}

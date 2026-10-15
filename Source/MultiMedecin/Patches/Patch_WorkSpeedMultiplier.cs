using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(ref float __result, StatRequest req, StatDef ___stat)
        {
            if (___stat?.defName != "MedicalOperationSpeed" ||
                !(req.Thing is Pawn pawn) || pawn.CurJob?.def != JobDefOf.DoBill ||
                !(pawn.CurJob.bill is BillMedicalEx bill) || bill.surgeon != pawn) return;
            __result *= SurgeryTeamUtility.SpeedMultiplier(bill);
        }
    }
}

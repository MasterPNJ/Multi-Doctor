using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using MultiDoctorSurgery.UI;
using UnityEngine;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(StatWorker __instance, ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is BillMedicalEx medicalBill)
            {
                // Limit the application to the main surgery job only, not support tasks
                if (pawn.CurJob.def != JobDefOf.DoBill)
                {
                    return;
                }

                // Get the 'stat' field from the __instance using AccessTools
                StatDef statDef = (StatDef)AccessTools.Field(typeof(StatWorker), "stat").GetValue(__instance);

                // Check if the modified stat is the surgery speed
                StatDef medicalOperationSpeed = DefDatabase<StatDef>.GetNamed("MedicalOperationSpeed", errorOnFail: false);
                if (medicalOperationSpeed != null && statDef == medicalOperationSpeed)
                {
                    // Calculate the speed multiplier
                    float speedMultiplier = Dialog_AssignDoctors.GetCurrentSpeedBonus(medicalBill);

                    // Log the stat value before applying the multiplier for verification
                    Log.Message($"[Debug] Before applying multiplier - Pawn: {pawn.Name.ToStringShort}, Stat: {statDef.defName}, Value: {__result}");

                    // Apply the speed multiplier
                    __result *= speedMultiplier;

                    // Log to verify that only the surgery speed stat is modified
                    Log.Message($"[Debug] After applying surgery speed multiplier {speedMultiplier} for pawn {pawn.Name.ToStringShort} on job {pawn.CurJob.def.defName}, Final Value: {__result}");
                }
            }
        }
    }
}

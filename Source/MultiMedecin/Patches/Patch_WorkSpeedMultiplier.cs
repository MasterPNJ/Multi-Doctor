using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using MultiDoctorSurgery.UI;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is BillMedicalEx medicalBill)
            {
                //Limit the application to the main surgery job only, not to support tasks
                if (pawn.CurJob.def != JobDefOf.DoBill)
                {
                    return;
                }

                // Calculate the speed multiplier based on the number of assistants
                float speedMultiplier = Dialog_AssignDoctors.GetCurrentSpeedBonus(medicalBill);

                //Log.Message($"[Debug] Applying speed multiplier {speedMultiplier} for pawn {pawn.Name.ToStringShort} on job {pawn.CurJob.def.defName}");

                // Apply the speed multiplier to the base speed result
                __result *= speedMultiplier;
            }
        }
    }
}

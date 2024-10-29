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
                // Calculate speed multiplier based on the current number of assistants
                float speedMultiplier = Dialog_AssignDoctors.GetCurrentSpeedBonus(medicalBill);

                // Apply the speed multiplier to the base speed result
                __result *= speedMultiplier;
            }
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is BillMedicalEx medicalBill)
            {
                var assignedDoctors = medicalBill.assignedDoctors;

                if (assignedDoctors != null && assignedDoctors.Count > 1)
                {
                    float speedMultiplier = 1f + ((assignedDoctors.Count - 1) * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor);
                    __result *= speedMultiplier;
                }
            }
        }
    }
}

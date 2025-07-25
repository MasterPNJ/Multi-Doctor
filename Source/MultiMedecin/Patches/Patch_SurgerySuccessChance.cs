using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using MultiDoctorSurgery.UI;
using UnityEngine;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_SurgerySuccessChance
    {
        public static void Postfix(StatWorker __instance, ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is BillMedicalEx medicalBill)
            {
                if (pawn.CurJob.def != JobDefOf.DoBill)
                {
                    return;
                }

                StatDef statDef = (StatDef)AccessTools.Field(typeof(StatWorker), "stat").GetValue(__instance);
                if (statDef == StatDefOf.MedicalSurgerySuccessChance)
                {
                    float bonus = Dialog_AssignDoctors.GetCurrentSuccessBonus(medicalBill);
                    //float before = __result; // store the original value for logging

                    __result = Mathf.Min(__result + bonus, MultiDoctorSurgeryMod.settings.maxSuccessBonus);

                    //Log.Message($"{pawn.NameShortColored} : base {before:P2} + bonus {bonus:P2} = final {__result:P2}");
                }
            }
        }
    }
}
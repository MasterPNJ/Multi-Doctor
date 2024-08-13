using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace MultiMedecin
{
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryStartNewDoBillJob")]
    public static class Patch_TryStartNewDoBillJob
    {
        static bool Prefix(Pawn pawn, Bill bill, IBillGiver giver, List<ThingCount> chosenIngThings, out Job haulOffJob, bool dontCreateJobIfHaulOffRequired)
        {
            Log.Message("Patch_TryStartNewDoBillJob: Prefix called");
            haulOffJob = null;

            if (bill.recipe.Worker is Recipe_Surgery && giver is Pawn patientPawn)
            {
                Log.Message("Patch_TryStartNewDoBillJob: Surgery bill detected");

                if (!Find.WindowStack.Windows.OfType<Dialog_ConfirmSurgery>().Any(w => w.bill == bill))
                {
                    Find.WindowStack.Add(new Dialog_ConfirmSurgery(pawn, patientPawn, bill, giver));
                }

                if (pawn.jobs.curJob?.def == JobDefOf.DoBill && pawn.jobs.curJob.targetA.Thing == giver)
                {
                    Log.Message("Patch_TryStartNewDoBillJob: Job DoBill already in progress for this giver");
                    return false;
                }

                return false;
            }

            return true;
        }
    }
}
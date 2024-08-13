using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace MultiMedecin
{
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    public static class Patch_JobDriver_DoBill
    {
        static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_DoBill __instance)
        {
            Log.Message("Patch_JobDriver_DoBill: Postfix called");
            Job job = __instance.job;
            Pawn pawn = __instance.pawn;
            IBillGiver giver = job.GetTarget(TargetIndex.A).Thing as IBillGiver;
            Bill_Medical bill = job.bill as Bill_Medical;

            if (bill != null && bill.recipe.Worker is Recipe_Surgery)
            {
                Log.Message("Patch_JobDriver_DoBill: Surgery bill detected");
                try
                {
                    Pawn giverPawn = bill.GiverPawn;
                    if (giverPawn == null)
                    {
                        Log.Error("Medical Bill does not have a GiverPawn. Bill details: " + bill.ToString());
                        yield break;
                    }
                }
                catch (NullReferenceException e)
                {
                    Log.Error("Error accessing GiverPawn in JobDriver_DoBill: " + e.Message);
                    yield break;
                }

                if (!Find.WindowStack.Windows.OfType<Dialog_ConfirmSurgery>().Any(w => w.bill == bill))
                {
                    Find.WindowStack.Add(new Dialog_ConfirmSurgery(pawn, (Pawn)giver, bill, giver));
                    yield break;
                }
            }

            foreach (var toil in toils)
            {
                yield return toil;
            }
        }
    }
}
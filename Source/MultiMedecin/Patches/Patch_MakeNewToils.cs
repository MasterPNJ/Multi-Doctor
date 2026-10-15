using HarmonyLib;
using Verse.AI;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    public static class Patch_MakeNewToils
    {
        public static void Postfix(JobDriver_DoBill __instance)
        {
            if (!(__instance.job?.bill is BillMedicalEx bill)) return;
            var job = __instance.job;
            // Driver cleanup covers every interruption, including travel and ingredient collection.
            __instance.AddFinishAction(condition => SurgeryTeamUtility.StopSession(bill, job));
        }
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))]
    public static class Patch_DoRecipeWork
    {
        public static void Postfix(Toil __result)
        {
            var toil = __result;
            var originalInit = toil.initAction;
            toil.initAction = () =>
            {
                originalInit?.Invoke();
                BeginWork(toil);
            };
            var originalTick = toil.tickAction;
            toil.AddFinishAction(() =>
            {
                if (toil.actor?.CurJob?.bill is BillMedicalEx bill && bill.activeWorkToil == toil)
                    bill.workInProgress = false;
            });
            toil.tickAction = () =>
            {
                // Reconnect a session restored in the middle of the work toil.
                BeginWork(toil);
                if (toil.actor?.CurJob?.bill is BillMedicalEx bill && !SurgeryTeamUtility.IsOperating(bill)) return;
                originalTick?.Invoke();
            };
#if !RIMWORLD_1_5
            var originalInterval = toil.tickIntervalAction;
            toil.tickIntervalAction = interval =>
            {
                BeginWork(toil);
                if (toil.actor?.CurJob?.bill is BillMedicalEx bill && !SurgeryTeamUtility.IsOperating(bill)) return;
                originalInterval?.Invoke(interval);
            };
#endif
        }

        private static void BeginWork(Toil toil)
        {
            if (toil.actor?.CurJob?.bill is BillMedicalEx bill)
                SurgeryTeamUtility.BeginWork(bill, toil.actor, toil);
        }
    }
}

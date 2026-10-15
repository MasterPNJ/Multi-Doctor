using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery
{
    public static class SurgerySuccessPreview
    {
        private sealed class Context
        {
            public BillMedicalEx Bill;
            public Pawn Surgeon;
            public float Bonus;
            public float SurgeonStat;
        }

        [ThreadStatic] private static Context current;

        internal static bool TryGetBonus(Pawn surgeon, out float bonus)
        {
            bonus = current?.Bonus ?? 0f;
            return current != null && current.Surgeon == surgeon;
        }

        internal static bool TryGetSurgeonStat(Bill bill, Pawn surgeon, out float value)
        {
            value = current?.SurgeonStat ?? 0f;
            return current != null && current.Bill == bill && current.Surgeon == surgeon;
        }

        public static bool TryEstimate(BillMedicalEx bill, Pawn surgeon, IEnumerable<Pawn> assistants, out float chance)
        {
            chance = 0f;
            if (bill?.recipe == null || bill.GiverPawn == null || surgeon == null) return false;
            var previous = current;
            current = new Context
            {
                Bill = bill,
                Surgeon = surgeon,
                Bonus = SurgeryTeamUtility.EstimateSuccess(assistants.Where(p => p != surgeon), bill.recipe)
            };
            try
            {
                // Read a fresh stat, bypassing cached values from the surgeon's current job.
                // The same stat postfix applies the selected team's bonus during this scope.
                current.SurgeonStat = StatDefOf.MedicalSurgerySuccessChance.Worker.GetValue(StatRequest.For(surgeon), true);
                var effect = bill.recipe.surgeryOutcomeEffect;
                if (effect == null)
                {
                    chance = 1f; // Recipe_Surgery.CheckSurgeryFail also bypasses failure in this case.
                    return true;
                }
                // Custom outcome selection is not necessarily a probability equal to quality.
                if (effect.outcomes == null || effect.outcomes.Count == 0 ||
                    effect.outcomes[0].GetType() != typeof(SurgeryOutcomeSuccess)) return false;
                chance = Mathf.Clamp01(effect.GetQuality(bill.recipe, surgeon, bill.GiverPawn,
                    new List<Thing>(), bill.Part, bill));
                return !float.IsNaN(chance);
            }
            catch (Exception exception)
            {
                Log.WarningOnce("[MultiDoctorSurgery] Could not estimate surgery success: " + exception, 1824911);
                return false;
            }
            finally
            {
                // Never change the bill, pawn job, XP, or the real outcome while drawing the UI.
                current = previous;
            }
        }
    }
}

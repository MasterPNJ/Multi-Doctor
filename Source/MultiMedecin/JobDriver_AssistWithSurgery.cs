using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MultiMedecin
{
    public class JobDriver_AssistWithSurgery : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message($"[MultiMedecin] {pawn.LabelShort} is trying to assist with surgery.");
            return this.pawn.Reserve(this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOnDespawnedOrNull(TargetIndex.C);

            // Go to the bed where the surgery is happening
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Wait with the surgery in progress
            Toil wait = Toils_General.WaitWith(TargetIndex.A, 5000, true, true); // 5000 ticks = 5 in-game seconds
            wait.tickAction = () =>
            {
                Pawn patient = (Pawn)this.job.GetTarget(TargetIndex.B).Thing;
                Pawn surgeon = (Pawn)this.job.GetTarget(TargetIndex.C).Thing;
                if (this.pawn.IsHashIntervalTick(1000)) // every in-game second
                {
                    // Assist by improving surgery outcome, learning, etc.
                    this.pawn.skills.Learn(SkillDefOf.Medicine, 0.1f);
                    Log.Message($"[MultiMedecin] {this.pawn.LabelShort} is assisting {surgeon.LabelShort} with the surgery on {patient.LabelShort}.");
                }
            };
            yield return wait;
        }
    }
}
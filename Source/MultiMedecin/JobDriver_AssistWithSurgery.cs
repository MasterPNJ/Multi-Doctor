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
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            Toil goToBed = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            yield return goToBed;

            Toil assist = new Toil();
            assist.initAction = () =>
            {
                Pawn patient = (Pawn)this.job.GetTarget(TargetIndex.B).Thing;
                Pawn surgeon = (Pawn)this.job.GetTarget(TargetIndex.C).Thing;
                Messages.Message($"{this.pawn.LabelShort} is assisting {surgeon.LabelShort} with the surgery on {patient.LabelShort}.", MessageTypeDefOf.PositiveEvent);
            };
            assist.defaultCompleteMode = ToilCompleteMode.Never;
            assist.WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return assist;

            yield break;
        }
    }
}
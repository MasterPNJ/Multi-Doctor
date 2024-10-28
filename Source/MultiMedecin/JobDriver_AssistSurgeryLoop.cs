using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // JobDriver personnalisé pour les assistants
    public class JobDriver_AssistSurgeryLoop : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil assistToil = new Toil();
            assistToil.initAction = () =>
            {
                Log.Message($"{pawn.Name} a commencé le Job AssistSurgeryLoop pour {job.targetA.Thing.Label}");
            };
            assistToil.tickAction = () =>
            {
                if (!IsSurgeryOngoing() || !IsStillAssignedToAssist())
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            assistToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return assistToil;
        }

        private bool IsSurgeryOngoing()
        {
            var medicalBill = job.bill as BillMedicalEx;
            return medicalBill != null && medicalBill.SurgeryStarted;
        }

        private bool IsStillAssignedToAssist()
        {
            var medicalBill = job.bill as BillMedicalEx;
            return medicalBill != null && medicalBill.assignedDoctors.Contains(pawn);
        }
    }
}

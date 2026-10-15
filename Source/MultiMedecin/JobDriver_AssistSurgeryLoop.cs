using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MultiDoctorSurgery
{
    public class JobDriver_AssistSurgeryLoop : JobDriver
    {
        private const float XpPerTick = 0.05f;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

#if RIMWORLD_1_5
        protected override IEnumerable<Toil> MakeNewToils()
#else
        public override IEnumerable<Toil> MakeNewToils()
#endif
        {
            this.FailOn(() => !CanContinue());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var assist = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            assist.tickAction = () =>
            {
                var bill = job.bill as BillMedicalEx;
                if (!SurgeryTeamUtility.IsActiveAssistant(bill, pawn))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // No completion reward: interrupted work only earns its actual working time.
                if (SurgeryTeamUtility.IsWorking(bill))
                    pawn.skills?.GetSkill(bill.recipe.workSkill ?? SkillDefOf.Medicine)?.Learn(XpPerTick);
            };
            yield return assist;
        }

        private bool CanContinue()
        {
            var bill = job.bill as BillMedicalEx;
            return SurgeryTeamUtility.IsOperating(bill) &&
                SurgeryTeamUtility.CanAssist(pawn, bill.GiverPawn, false) &&
                bill.assignedDoctors?.Contains(pawn) == true;
        }
    }
}

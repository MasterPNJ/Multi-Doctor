using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // Customized JobDriver for assistants
    public class JobDriver_AssistSurgeryLoop : JobDriver
    {
        private const float xpPerTick = 0.05f; // XP per tick for assistants, adjust this value as needed

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
                // Initialization action
            };
            assistToil.tickAction = () =>
            {
                var patient = job.targetA.Thing as Pawn;
                var medicalBill = job.bill as BillMedicalEx;

                // Vérification de l'état du patient
                /* // DEBUG LOG // 
                if (patient == null)
                {
                    Log.Message("[AssistSurgeryLoop] Patient is null.");
                }
                else
                {
                    if (patient.Dead)
                    {
                        Log.Message("[AssistSurgeryLoop] Patient is dead.");
                    }
                    else if (patient.Downed && !IsPatientUnderAnesthesia(patient))
                    {
                        Log.Message("[AssistSurgeryLoop] Patient is downed but not anesthetized.");
                    }
                }

                if (medicalBill == null)
                {
                    Log.Message("[AssistSurgeryLoop] Medical bill is null.");
                }
                else
                {
                    if (!medicalBill.SurgeryStarted)
                    {
                        Log.Message("[AssistSurgeryLoop] Surgery has not started.");
                    }
                    if (!medicalBill.assignedDoctors.Contains(pawn))
                    {
                        Log.Message("[AssistSurgeryLoop] Assistant is no longer assigned to assist.");
                    }
                }
                */

                // Condition combinée
                if (patient == null || patient.Dead || (patient.Downed && !IsPatientUnderAnesthesia(patient)) ||
                    medicalBill == null || !medicalBill.SurgeryStarted ||
                    !medicalBill.assignedDoctors.Contains(pawn) ||
                    (patient != null && medicalBill != null && !patient.BillStack.Bills.Contains(medicalBill)))
                {
                    if (medicalBill != null)
                    {
                        medicalBill.SurgeryStarted = false;
                    }
                    AwardSurgeryExperience();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Accorder de l'expérience chaque tick pendant que la chirurgie est en cours
                AwardTickExperience();
            };
            assistToil.defaultCompleteMode = ToilCompleteMode.Never;

            // Ensure AwardSurgeryExperience is called even if the job is interrupted
            assistToil.AddFinishAction(() =>
            {
                var medicalBill = job.bill as BillMedicalEx;
                if (medicalBill != null)
                {
                    medicalBill.SurgeryStarted = false;
                }
                if (IsSurgeryOngoing())
                {
                    AwardSurgeryExperience();
                }
            });

            yield return assistToil;
        }

        private bool IsPatientUnderAnesthesia(Pawn patient)
        {
            return patient.health.hediffSet.HasHediff(HediffDefOf.Anesthetic);
        }

        private bool IsSurgeryOngoing()
        {
            var medicalBill = job.bill as BillMedicalEx;
            return medicalBill != null && medicalBill.SurgeryStarted;
        }

        private bool IsStillAssignedToAssist()
        {
            var medicalBill = job.bill as BillMedicalEx;
            var patient = job.targetA.Thing as Pawn;

            return medicalBill != null &&
                   patient != null &&
                   !patient.Dead &&
                   !patient.Downed &&
                   medicalBill.assignedDoctors.Contains(pawn);
        }

        private void AwardTickExperience()
        {
            // Grant a small amount of XP each tick to reflect ongoing assistance
            SkillDef skill = SkillDefOf.Medicine;
            pawn.skills?.GetSkill(skill).Learn(xpPerTick);
        }

        private void AwardSurgeryExperience()
        {
            // Retrieve the main surgeon and the list of assistants
            var medicalBill = job.bill as BillMedicalEx;
            if (medicalBill == null) return;

            Pawn surgeon = medicalBill.surgeon;
            List<Pawn> assistants = medicalBill.assignedDoctors;

            // Calculate total XP
            float totalXp = 1000f; // Base total XP value, adjust if necessary
            SkillDef skill = SkillDefOf.Medicine;

            if (assistants == null || assistants.Count == 0)
            {
                // If the surgeon is alone, they receive 100% of the XP
                surgeon.skills?.GetSkill(skill).Learn(totalXp);
            }
            else
            {
                // The surgeon receives 50% of the XP
                float surgeonXp = 0.5f * totalXp;
                surgeon.skills?.GetSkill(skill).Learn(surgeonXp);

                // Share the remaining XP among the assistants
                float assistantXp = (0.5f * totalXp) / assistants.Count;
                foreach (var assistant in assistants)
                {
                    if (assistant != surgeon) // Ensure the assistant is not the surgeon
                    {
                        assistant.skills?.GetSkill(skill).Learn(assistantXp);
                    }
                }
            }
        }
    }
}

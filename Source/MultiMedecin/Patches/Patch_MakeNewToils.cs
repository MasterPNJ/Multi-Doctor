using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    public static class Patch_MakeNewToils
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_DoBill __instance)
        {
            if (__instance.job != null && __instance.job.bill is BillMedicalEx medicalBill)
            {
                var patient = medicalBill.GiverPawn;
                var assignedDoctors = medicalBill.assignedDoctors;
                var surgeon = medicalBill.surgeon;

                if (assignedDoctors != null && assignedDoctors.Count > 0)
                {
                    // V�rifier que le chirurgien principal est bien celui qui ex�cute le travail
                    if (surgeon != null && surgeon != __instance.pawn)
                    {
                        // Annuler le travail si ce n'est pas le chirurgien principal
                        __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        yield break;
                    }

                    // Convertir les toils en liste pour manipulation
                    List<Toil> toilsList = toils.ToList();

                    // Cr�er un nouveau Toil pour d�marrer les assistants apr�s que le job principal est actif
                    Toil startAssistantsToil = new Toil();
                    startAssistantsToil.initAction = () =>
                    {
                        foreach (var doctor in assignedDoctors)
                        {
                            if (doctor != __instance.pawn)
                            {
                                StartAssistantJobLoop(doctor, patient, medicalBill); // Passer le medicalBill
                            }
                        }
                    };
                    startAssistantsToil.defaultCompleteMode = ToilCompleteMode.Instant;

                    // Ins�rer le nouveau Toil apr�s le premier Toil principal
                    if (toilsList.Count > 1)
                    {
                        toilsList.Insert(1, startAssistantsToil);
                    }
                    else
                    {
                        toilsList.Add(startAssistantsToil);
                    }

                    // Cr�er un nouveau Toil pour d�finir le flag SurgeryStarted
                    Toil setSurgeryStartedToil = new Toil();
                    setSurgeryStartedToil.initAction = () =>
                    {
                        medicalBill.SurgeryStarted = true;
                        Log.Message("[Main Surgeon] Chirurgie commenc�e.");
                    };
                    setSurgeryStartedToil.defaultCompleteMode = ToilCompleteMode.Instant;
                    toilsList.Insert(2, setSurgeryStartedToil); // Ins�rer apr�s les assistants

                    // Ajouter un FinishAction pour lib�rer les m�decins apr�s l'op�ration
                    Toil lastToil = toilsList.Last();
                    lastToil.AddFinishAction(() =>
                    {
                        foreach (var doctor in assignedDoctors)
                        {
                            if (doctor != __instance.pawn)
                            {
                                // Lib�rer le m�decin de son travail actuel et effacer sa file de travaux
                                if (doctor.jobs != null)
                                {
                                    doctor.jobs.ClearQueuedJobs();
                                    doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
                                }
                            }
                        }

                        // D�finir le flag SurgeryStarted � false apr�s la fin de la chirurgie
                        medicalBill.SurgeryStarted = false;
                        Log.Message("[Main Surgeon] Chirurgie termin�e.");
                    });

                    // Renvoyer la nouvelle liste de Toils
                    foreach (var toil in toilsList)
                    {
                        yield return toil;
                    }
                }
                else
                {
                    // Si pas de m�decins assign�s, renvoyer les Toils originaux
                    foreach (var toil in toils)
                    {
                        yield return toil;
                    }
                }
            }
            else
            {
                // Si le bill n'est pas un BillMedicalEx, renvoyer les Toils originaux
                foreach (var toil in toils)
                {
                    yield return toil;
                }
            }
        }

        private static void StartAssistantJobLoop(Pawn doctor, Pawn patient, BillMedicalEx medicalBill)
        {
            // Cr�er un travail personnalis� qui fait que l'assistant reste pr�s du patient
            Job job = JobMaker.MakeJob(MyCustomJobDefs.AssistSurgeryLoop, patient);
            job.bill = medicalBill; // Associer le bill de chirurgie au job
            doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
        }
    }
}

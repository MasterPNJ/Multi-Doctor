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
                    // Vérifier que le chirurgien principal est bien celui qui exécute le travail
                    if (surgeon != null && surgeon != __instance.pawn)
                    {
                        // Annuler le travail si ce n'est pas le chirurgien principal
                        __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        yield break;
                    }

                    // Convertir les toils en liste pour manipulation
                    List<Toil> toilsList = toils.ToList();

                    // Créer un nouveau Toil pour démarrer les assistants après que le job principal est actif
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

                    // Insérer le nouveau Toil après le premier Toil principal
                    if (toilsList.Count > 1)
                    {
                        toilsList.Insert(1, startAssistantsToil);
                    }
                    else
                    {
                        toilsList.Add(startAssistantsToil);
                    }

                    // Créer un nouveau Toil pour définir le flag SurgeryStarted
                    Toil setSurgeryStartedToil = new Toil();
                    setSurgeryStartedToil.initAction = () =>
                    {
                        medicalBill.SurgeryStarted = true;
                        Log.Message("[Main Surgeon] Chirurgie commencée.");
                    };
                    setSurgeryStartedToil.defaultCompleteMode = ToilCompleteMode.Instant;
                    toilsList.Insert(2, setSurgeryStartedToil); // Insérer après les assistants

                    // Ajouter un FinishAction pour libérer les médecins après l'opération
                    Toil lastToil = toilsList.Last();
                    lastToil.AddFinishAction(() =>
                    {
                        foreach (var doctor in assignedDoctors)
                        {
                            if (doctor != __instance.pawn)
                            {
                                // Libérer le médecin de son travail actuel et effacer sa file de travaux
                                if (doctor.jobs != null)
                                {
                                    doctor.jobs.ClearQueuedJobs();
                                    doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
                                }
                            }
                        }

                        // Définir le flag SurgeryStarted à false après la fin de la chirurgie
                        medicalBill.SurgeryStarted = false;
                        Log.Message("[Main Surgeon] Chirurgie terminée.");
                    });

                    // Renvoyer la nouvelle liste de Toils
                    foreach (var toil in toilsList)
                    {
                        yield return toil;
                    }
                }
                else
                {
                    // Si pas de médecins assignés, renvoyer les Toils originaux
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
            // Créer un travail personnalisé qui fait que l'assistant reste près du patient
            Job job = JobMaker.MakeJob(MyCustomJobDefs.AssistSurgeryLoop, patient);
            job.bill = medicalBill; // Associer le bill de chirurgie au job
            doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
        }
    }
}

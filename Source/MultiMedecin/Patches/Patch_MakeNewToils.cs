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
                    // Check that the lead surgeon is the one carrying out the work
                    if (surgeon != null && surgeon != __instance.pawn)
                    {
                        // Cancel work if not the main surgeon
                        __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        yield break;
                    }

                    // Converting toils into lists for manipulation
                    List<Toil> toilsList = toils.ToList();

                    // Create a new Toil to start assistants after the main job is active
                    Toil startAssistantsToil = new Toil();
                    startAssistantsToil.initAction = () =>
                    {
                        foreach (var doctor in assignedDoctors)
                        {
                            if (doctor != __instance.pawn)
                            {
                                StartAssistantJobLoop(doctor, patient, medicalBill); // Pass the medicalBill
                            }
                        }
                    };
                    startAssistantsToil.defaultCompleteMode = ToilCompleteMode.Instant;

                    // Insert the new Toil after the first main Toil
                    if (toilsList.Count > 1)
                    {
                        toilsList.Insert(1, startAssistantsToil);
                    }
                    else
                    {
                        toilsList.Add(startAssistantsToil);
                    }

                    // Create a new Toil to set the SurgeryStarted flag
                    Toil setSurgeryStartedToil = new Toil();
                    setSurgeryStartedToil.initAction = () =>
                    {
                        medicalBill.SurgeryStarted = true;
                        //Log.Message("[Main Surgeon] Chirurgie commencée.");
                    };
                    setSurgeryStartedToil.defaultCompleteMode = ToilCompleteMode.Instant;
                    toilsList.Insert(2, setSurgeryStartedToil); // Insert after assistants

                    // Add a FinishAction to release the doctors after the operation
                    Toil lastToil = toilsList.Last();
                    lastToil.AddFinishAction(() =>
                    {
                        foreach (var doctor in assignedDoctors)
                        {
                            if (doctor != __instance.pawn)
                            {
                                // Release the doctor from his current job and clear his work queue
                                if (doctor.jobs != null)
                                {
                                    doctor.jobs.ClearQueuedJobs();
                                    doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
                                }
                            }
                        }

                        // Set the SurgeryStarted flag to false after the end of surgery
                        medicalBill.SurgeryStarted = false;
                        //Log.Message("[Main Surgeon] Chirurgie terminée.");
                    });

                    // Resend the new Toils list
                    foreach (var toil in toilsList)
                    {
                        yield return toil;
                    }
                }
                else
                {
                    // If no doctors assigned, return the original Toils
                    foreach (var toil in toils)
                    {
                        yield return toil;
                    }
                }
            }
            else
            {
                // If the bill is not a BillMedicalEx, return the original Toils
                foreach (var toil in toils)
                {
                    yield return toil;
                }
            }
        }

        private static void StartAssistantJobLoop(Pawn doctor, Pawn patient, BillMedicalEx medicalBill)
        {
            if (doctor.workSettings != null && !doctor.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && !doctor.def.race.IsMechanoid)
            {
                return; // Skip if the pawn cannot perform medical work
            }
            // Create a personalised job that keeps the assistant close to the patient
            Job job = JobMaker.MakeJob(MyCustomJobDefs.AssistSurgeryLoop, patient);
            job.bill = medicalBill; // Linking the surgery bill to the job
            doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
        }
    }
}

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

                // Check that only the assigned surgeon is allowed to start the operation
                if (surgeon != null && surgeon != __instance.pawn)
                {
                    // If the pawn trying to start the job is not the main surgeon, cancel the job
                    __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    yield break; // Exit immediately if it's not the main surgeon
                }

                if (assignedDoctors != null && assignedDoctors.Count > 0)
                {
                    // Convert toils to a list to allow manipulation
                    List<Toil> toilsList = toils.ToList();

                    // Create a new Toil to start assistant jobs after the main job is active
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
                        //Log.Message("[Main Surgeon] Surgery started.");
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
                        //Log.Message("[Main Surgeon] Surgery completed.");
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
            // Create a personalized job that keeps the assistant close to the patient
            Job job = JobMaker.MakeJob(MyCustomJobDefs.AssistSurgeryLoop, patient);
            job.bill = medicalBill; // Link the surgery bill to the job
            doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
        }
    }
}

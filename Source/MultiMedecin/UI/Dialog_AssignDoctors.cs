using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_AssignDoctors : Window
    {
        private Pawn patient;
        private RecipeDef recipe;
        private BillMedicalEx bill;
        private List<Pawn> availableDoctors;
        private Vector2 surgeonScrollPosition;
        private Vector2 assistantScrollPosition;
        private Pawn selectedSurgeon; // Principal surgeon selected

        // Variables for storing previous assignments
        private Pawn previousSurgeon;
        private List<Pawn> previousAssignedDoctors;

        private float currentSpeedBonus;
        private float currentSuccessRate;
        private float currentTotalSuccessRate; // New variable to store total success rate

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;

            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            // Store previous assignments
            this.previousSurgeon = bill.surgeon;
            this.previousAssignedDoctors = new List<Pawn>(bill.assignedDoctors);

            // Check if there's already a selected surgeon, otherwise pick the best doctor as default
            selectedSurgeon = bill.assignedDoctors.FirstOrDefault() ?? availableDoctors.OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level).FirstOrDefault();
            if (selectedSurgeon != null && !bill.assignedDoctors.Contains(selectedSurgeon))
            {
                bill.assignedDoctors.Insert(0, selectedSurgeon); // Ensure the lead surgeon is at the start
            }

            // Calculate multipliers with existing assigned doctors
            CalculateMultipliers();

            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            this.forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "AssignDoctors_Title".Translate(patient.Name.ToStringShort));
            Text.Font = GameFont.Small;

            float curY = 40f;

            // Display speed and success rate multipliers
            Widgets.Label(new Rect(0, curY, inRect.width, 30f), $"Speed Bonus: {currentSpeedBonus:F2}");
            curY += 30f;
            Widgets.Label(new Rect(0, curY, inRect.width, 30f), $"Success Rate Bonus: {currentSuccessRate:P}");
            curY += 30f;

            // Display the total surgery success rate with the adjustable maximum limit
            float maxTotalSuccessRate = MultiDoctorSurgeryMod.settings.maxSuccessBonus; // Use the adjustable max limit from settings
            Widgets.Label(new Rect(0, curY, inRect.width, 30f), $"Total Success Rate (Max {maxTotalSuccessRate:P}): {currentTotalSuccessRate:P}");
            curY += 40f;

            // Surgeon selection
            Text.Font = GameFont.Medium;
            string surgeonLabel = "AssignDoctors_SelectSurgeon".Translate();
            Rect surgeonLabelRect = new Rect(0, curY, inRect.width, 25f);
            Widgets.Label(surgeonLabelRect, surgeonLabel);
            // Draw the line under the text
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(surgeonLabelRect.x, surgeonLabelRect.yMax - 2f, Text.CalcSize(surgeonLabel).x);
            Text.Font = GameFont.Small;
            curY += 30f;

            Rect surgeonOutRect = new Rect(0f, curY, inRect.width, 100f);
            Rect surgeonViewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);

            Widgets.BeginScrollView(surgeonOutRect, ref surgeonScrollPosition, surgeonViewRect);

            float surgeonY = 0f;
            foreach (var doctor in availableDoctors)
            {
                Rect rowRect = new Rect(0, surgeonY, surgeonViewRect.width, 30f);
                bool isSelected = doctor == selectedSurgeon;
                string label = "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level);
                if (Widgets.RadioButtonLabeled(rowRect, label, isSelected))
                {
                    selectedSurgeon = doctor;
                    bill.assignedDoctors.Remove(doctor);
                    bill.assignedDoctors.Insert(0, doctor); // Ensure selected surgeon is the first in the list
                    CalculateMultipliers();
                }
                surgeonY += 35f;
            }
            Widgets.EndScrollView();
            curY += 110f;

            // Selecting medical assistants
            Text.Font = GameFont.Medium;
            string assistantsLabel = "AssignDoctors_SelectAssistants".Translate();
            Rect assistantsLabelRect = new Rect(0, curY, inRect.width, 25f);
            Widgets.Label(assistantsLabelRect, assistantsLabel);
            // Draw the line under the text
            Widgets.DrawLineHorizontal(assistantsLabelRect.x, assistantsLabelRect.yMax - 2f, Text.CalcSize(assistantsLabel).x);
            Text.Font = GameFont.Small;
            curY += 30f;

            Rect assistantOutRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 70f);
            Rect assistantViewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f); // -1 pour ne pas compter le chirurgien principal

            Widgets.BeginScrollView(assistantOutRect, ref assistantScrollPosition, assistantViewRect);

            float assistantY = 0f;
            foreach (var doctor in availableDoctors)
            {
                if (doctor == selectedSurgeon) continue;
                bool isAssigned = bill.assignedDoctors.Contains(doctor);
                Rect rowRect = new Rect(0, assistantY, assistantViewRect.width, 30f);
                bool newIsAssigned = isAssigned;
                string label = "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level);
                Widgets.CheckboxLabeled(rowRect, label, ref newIsAssigned);

                if (newIsAssigned != isAssigned)
                {
                    if (newIsAssigned && bill.assignedDoctors.Count < MultiDoctorSurgeryMod.settings.maxDoctors)
                        bill.assignedDoctors.Add(doctor);
                    else if (!newIsAssigned)
                        bill.assignedDoctors.Remove(doctor);

                    CalculateMultipliers();
                }
                assistantY += 35f;
            }
            Widgets.EndScrollView();
            curY += 70f;

            // Buttons at the bottom
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Confirm".Translate()))
            {
                // Cancel work in progress by previous surgeons and doctors
                CancelOngoingJobs();

                // Store the main surgeon in the bill
                bill.surgeon = selectedSurgeon;

                // Ensure that the lead surgeon is on the list of assigned doctors
                if (!bill.assignedDoctors.Contains(selectedSurgeon))
                {
                    bill.assignedDoctors.Add(selectedSurgeon);
                }

                // Assigning the bill to the lead surgeon
                bill.SetPawnRestriction(selectedSurgeon);
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Cancel".Translate()))
            {
                // Cancel the operation by deleting the bill
                patient.BillStack.Bills.Remove(bill);
                Close();
            }
        }

        private void CalculateMultipliers()
        {
            // Reset multipliers to base values
            currentSpeedBonus = 1f;
            currentSuccessRate = 0f;

            int assistantsCount = bill.assignedDoctors.Count - 1; // Exclude lead surgeon

            // Calculate bonus based on the number of assistants
            currentSpeedBonus += assistantsCount * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor;
            currentSuccessRate += assistantsCount * MultiDoctorSurgeryMod.settings.successRateMultiplier;

            // Apply limits for speed and success bonuses using settings
            currentSpeedBonus = Mathf.Min(currentSpeedBonus, MultiDoctorSurgeryMod.settings.maxSpeedBonus);
            currentSuccessRate = Mathf.Min(currentSuccessRate, MultiDoctorSurgeryMod.settings.maxSuccessBonus);

            // Get the base surgery success rate of the lead surgeon
            float baseSuccessRate = selectedSurgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance);

            // Calculate total success rate by adding base rate and bonus
            float totalSuccessRate = baseSuccessRate + currentSuccessRate;
            totalSuccessRate = Mathf.Min(totalSuccessRate, MultiDoctorSurgeryMod.settings.maxSuccessBonus); // Apply the adjustable max limit from settings

            // Store the total success rate for display
            currentTotalSuccessRate = totalSuccessRate;
        }

        public static float GetCurrentSpeedBonus(BillMedicalEx bill)
        {
            int assistantsCount = bill.assignedDoctors.Count - 1;
            float speedBonus = 1f + assistantsCount * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor;
            return Mathf.Min(speedBonus, MultiDoctorSurgeryMod.settings.maxSpeedBonus);
        }

        private void CancelOngoingJobs()
        {
            // Cancel the work of the previous surgeon if he is operating
            if (previousSurgeon != null && previousSurgeon.CurJob != null && previousSurgeon.CurJob.bill == bill)
            {
                previousSurgeon.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Cancel the work of previously assigned doctors
            if (previousAssignedDoctors != null)
            {
                foreach (var doctor in previousAssignedDoctors)
                {
                    if (doctor != previousSurgeon && doctor.CurJob != null && doctor.CurJob.def == MyCustomJobDefs.AssistSurgeryLoop)
                    {
                        // Finish the job if it's the assistant
                        doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }
        }
    }
}

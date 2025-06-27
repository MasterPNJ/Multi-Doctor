using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MultiDoctorSurgery;

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

        // The required skill for this recipe
        private SkillDef requiredSkill;

        // Variables for storing previous assignments
        private Pawn previousSurgeon;
        private List<Pawn> previousAssignedDoctors;

        private float currentSpeedBonus;
        private float currentSuccessRate;
        private float currentTotalSuccessRate; // New variable to store total success rate

        // Sorting mode and order for the doctor list
        private enum SortingMode { ByName, BySkill }
        private SortingMode sortingMode = SortingMode.BySkill;
        private bool isAscending = true;

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;

            // Determine the required skill for the recipe
            this.requiredSkill = recipe.workSkill ?? SkillDefOf.Medicine; // Fallback to Medicine if workSkill is null

            this.availableDoctors = patient.Map.mapPawns.AllPawns
            .Where(p => p != null // Ensure the pawn is not null
                        && p != patient // Exclude the patient
                        && !p.Dead // Exclude dead pawns
                        && !p.Downed // Exclude downed pawns
                        && (p.IsColonist || (p.Faction != null && p.Faction == Faction.OfPlayer)) // Include paramedics or any pawn under player's control
                        && p.health != null && p.health.capacities != null // Ensure health and capacities exist
                        && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) // Ensure the pawn can manipulate
                        && (p.def.race.IsMechanoid || (p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))) // Must be able to doctor
                        && (p.skills != null || p.def.race.IsMechanoid)) // Include if the pawn has skills or is a mechanoid
            .ToList();

            // Log the available doctors for debugging
            //Log.Message($"[MultiDoctorSurgery] Available doctors for surgery: {string.Join(", ", availableDoctors.Select(d => d.Name.ToStringShort))}");

            // Store previous assignments
            this.previousSurgeon = bill.surgeon;
            this.previousAssignedDoctors = new List<Pawn>(bill.assignedDoctors);

            // Check if there's already a selected surgeon, otherwise pick the best doctor as default
            selectedSurgeon = bill.assignedDoctors.FirstOrDefault()
            ?? availableDoctors
                .Where(d => d.skills != null && d.skills.GetSkill(requiredSkill) != null) // Ensure skills and the required skill exist
                .OrderByDescending(d => d.skills.GetSkill(requiredSkill).Level)
                .FirstOrDefault();
            if (selectedSurgeon != null && !bill.assignedDoctors.Contains(selectedSurgeon))
            {
                // Clear assistants and make sure only the selected surgeon is in the list
                bill.assignedDoctors.Clear();
                bill.assignedDoctors.Insert(0, selectedSurgeon); // Insert the surgeon as the first item
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

            // Sorting buttons with toggleable order
            if (Widgets.ButtonText(new Rect(0, curY, inRect.width / 2f, 25f), "AssignDoctors_SortByName".Translate()))
            {
                if (sortingMode == SortingMode.ByName)
                {
                    isAscending = !isAscending; // Toggle order if sorting mode is already by name
                }
                else
                {
                    sortingMode = SortingMode.ByName;
                    isAscending = true; // Reset to ascending when switching sorting mode
                }
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, curY, inRect.width / 2f, 25f), "AssignDoctors_SortBySkill".Translate()))
            {
                if (sortingMode == SortingMode.BySkill)
                {
                    isAscending = !isAscending; // Toggle order if sorting mode is already by skill
                }
                else
                {
                    sortingMode = SortingMode.BySkill;
                    isAscending = true; // Reset to ascending when switching sorting mode
                }
            }
            curY += 35f;

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

            // Sort doctors based on the selected sorting mode and order
            var sortedDoctors = sortingMode == SortingMode.ByName
            ? (isAscending ? availableDoctors.OrderBy(d => d.Name.ToStringShort) : availableDoctors.OrderByDescending(d => d.Name.ToStringShort))
            : (isAscending
                ? availableDoctors.OrderBy(d => d.def.race.IsMechanoid ? -1 : d.skills?.GetSkill(requiredSkill)?.Level ?? 0)
                : availableDoctors.OrderByDescending(d => d.def.race.IsMechanoid ? -1 : d.skills?.GetSkill(requiredSkill)?.Level ?? 0));

            // Log the sorted doctors for debugging
            //Log.Message($"[MultiDoctorSurgery] Sorted doctors: {string.Join(", ", sortedDoctors.Select(d => d.Name.ToStringShort))}");

            Widgets.BeginScrollView(surgeonOutRect, ref surgeonScrollPosition, surgeonViewRect);

            try
            {
                float surgeonY = 0f;
                foreach (var doctor in sortedDoctors)
                {
                    if (doctor == null)
                    {
                        Log.Warning("[MultiDoctorSurgery] Skipped null doctor.");
                        continue;
                    }

                    int requiredSkillLevel = recipe.skillRequirements?.FirstOrDefault(req => req.skill == requiredSkill)?.minLevel ?? 0;
                    int doctorSkillLevel = doctor.skills?.GetSkill(requiredSkill)?.Level ?? 0;

                    if (!doctor.def.race.IsMechanoid && doctorSkillLevel < requiredSkillLevel)
                    {
                       // Log.Message($"[MultiDoctorSurgery] Doctor {doctor.Name.ToStringShort} skipped (Skill level {doctorSkillLevel} < Required {requiredSkillLevel}).");
                        continue;
                    }

                    Rect rowRect = new Rect(0, surgeonY, surgeonViewRect.width, 30f);
                    bool isSelected = doctor == selectedSurgeon;
                    string label = doctor.def.race.IsMechanoid
                        ? $"{doctor.Name.ToStringShort} (Mechanoid)"
                        : $"{doctor.Name.ToStringShort} ({requiredSkill.label}: {doctorSkillLevel})";

                    if (Widgets.RadioButtonLabeled(rowRect, label, isSelected))
                    {
                        selectedSurgeon = doctor;

                        // Clear assistants and make sure only the selected surgeon is in the list
                        bill.assignedDoctors.Clear();
                        bill.assignedDoctors.Insert(0, selectedSurgeon); // Insert the surgeon as the first item
                        CalculateMultipliers();
                    }
                    surgeonY += 35f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
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
            Rect assistantViewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f); // -1 to exclude the lead surgeon

            Widgets.BeginScrollView(assistantOutRect, ref assistantScrollPosition, assistantViewRect);

            try
            {
                float assistantY = 0f;
                foreach (var doctor in sortedDoctors)
                {
                    if (doctor == selectedSurgeon) continue;

                    // V�rifie si le m�decin ou ses propri�t�s sont nulles
                    if (doctor == null || doctor.health == null || (doctor.skills == null && !doctor.def.race.IsMechanoid))
                    {
                        Log.Warning($"[MultiDoctorSurgery] Skipping null or invalid doctor: {doctor?.Name?.ToStringShort ?? "null"}");
                        continue;
                    }

                    // R�cup�re le niveau de comp�tence ou 0 pour les m�cano�des
                    int doctorSkillLevel = doctor.def.race.IsMechanoid ? 0 : doctor.skills.GetSkill(requiredSkill)?.Level ?? 0;

                    // Cr�e l'�tiquette (label) pour affichage
                    string label = doctor.def.race.IsMechanoid
                        ? $"{doctor.Name.ToStringShort} (Mechanoid)"
                        : $"{doctor.Name.ToStringShort} ({requiredSkill.label}: {doctorSkillLevel})";

                    // Affiche la checkbox pour assigner ou retirer un assistant
                    Rect rowRect = new Rect(0, assistantY, assistantViewRect.width, 30f);
                    bool isAssigned = bill.assignedDoctors.Contains(doctor);
                    bool newIsAssigned = isAssigned;

                    Widgets.CheckboxLabeled(rowRect, label, ref newIsAssigned);

                    // Met � jour la liste des assistants si l'�tat change
                    if (newIsAssigned != isAssigned)
                    {
                        if (newIsAssigned && (bill.assignedDoctors.Count - 1) < (MultiDoctorSurgeryMod.settings.maxDoctors - 1))
                            bill.assignedDoctors.Add(doctor);
                        else if (!newIsAssigned)
                            bill.assignedDoctors.Remove(doctor);

                        CalculateMultipliers();
                    }

                    assistantY += 35f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
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
                Compat.SetPawnRestrictionSafe(bill, selectedSurgeon);
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
            currentSpeedBonus = 1f; // Base speed multiplier
            currentSuccessRate = 0f; // Base success rate bonus

            // Get the base surgery success rate of the lead surgeon
            float baseSuccessRate = selectedSurgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance);

            // Calculate bonuses based on each assistant's medical skill level (excluding lead surgeon)
            for (int i = 1; i < bill.assignedDoctors.Count; i++) // Start from index 1 to skip the lead surgeon
            {
                var assistant = bill.assignedDoctors[i];

                // V�rifie si c'est un m�cano�de
                if (assistant.def.race.IsMechanoid)
                {
                    // Applique un bonus fixe ou sp�cifique aux m�cano�des
                    float mechSpeedBonus = MultiDoctorSurgeryMod.settings.mechSpeedBonus; // Exemple: bonus fixe
                    float mechSuccessBonus = MultiDoctorSurgeryMod.settings.mechSuccessBonus; // Exemple: bonus fixe

                    currentSpeedBonus += mechSpeedBonus;
                    currentSuccessRate += mechSuccessBonus;

                    //Log.Message($"[MultiDoctorSurgery] Mechanoid {assistant.Name.ToStringShort} added speed bonus: {mechSpeedBonus}, success bonus: {mechSuccessBonus}");
                }
                else
                {
                    // Utilise le niveau de comp�tence pour les humains
                    float skillLevel = assistant.skills.GetSkill(requiredSkill)?.Level ?? 0;

                    // Calcule les bonus pour les humains
                    currentSpeedBonus += skillLevel * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor / 20f; // Divided by 20 to normalize
                    currentSuccessRate += skillLevel * MultiDoctorSurgeryMod.settings.successRateMultiplier / 20f; // Divided by 20 to normalize
                }
            }

            // Apply limits for speed and success bonuses based on settings
            currentSpeedBonus = Mathf.Min(currentSpeedBonus, MultiDoctorSurgeryMod.settings.maxSpeedBonus);
            currentSuccessRate = Mathf.Min(currentSuccessRate, MultiDoctorSurgeryMod.settings.maxSuccessBonus);

            // Calculate total success rate by adding base rate and the calculated success rate bonus
            float totalSuccessRate = baseSuccessRate + currentSuccessRate;

            // Apply the adjustable maximum limit for total success rate
            currentTotalSuccessRate = Mathf.Min(totalSuccessRate, MultiDoctorSurgeryMod.settings.maxSuccessBonus);
        }

        private void LogSurgeonStats(Pawn surgeon, string context)
        {
            StatDef medicalOperationSpeed = DefDatabase<StatDef>.GetNamed("MedicalOperationSpeed", errorOnFail: false);
            if (medicalOperationSpeed != null)
            {
                Log.Message($"[Surgeon Stats {context}] {surgeon.Name.ToStringShort} - MedicalOperationSpeed: {surgeon.GetStatValue(medicalOperationSpeed)}, " +
                            $"MedicalSurgerySuccessChance: {surgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance)}, " +
                            $"GeneralLaborSpeed: {surgeon.GetStatValue(StatDefOf.GeneralLaborSpeed)}, " +
                            $"Manipulation: {surgeon.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation)}, " +
                            $"Sight: {surgeon.health.capacities.GetLevel(PawnCapacityDefOf.Sight)}");
            }
            else
            {
                Log.Message($"[Surgeon Stats {context}] {surgeon.Name.ToStringShort} - Could not find MedicalOperationSpeed stat.");
            }
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
                    if (doctor == null) continue; // Skip null doctors
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

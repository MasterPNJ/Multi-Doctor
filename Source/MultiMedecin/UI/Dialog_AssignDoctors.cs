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
        private Pawn selectedSurgeon; // Chirurgien principal sélectionné

        // Variables pour stocker les assignations précédentes
        private Pawn previousSurgeon;
        private List<Pawn> previousAssignedDoctors;

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;

            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            // Stocker les assignations précédentes
            this.previousSurgeon = bill.surgeon;
            this.previousAssignedDoctors = new List<Pawn>(bill.assignedDoctors);

            // Si un chirurgien est déjà assigné, le sélectionner, sinon choisir le meilleur
            if (bill.surgeon != null && availableDoctors.Contains(bill.surgeon))
            {
                selectedSurgeon = bill.surgeon;
            }
            else
            {
                // Par défaut, sélectionner le médecin avec la meilleure compétence
                selectedSurgeon = availableDoctors.OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level).FirstOrDefault();
            }

            // Ajouter le chirurgien principal à la liste des médecins assignés s'il n'y est pas déjà
            if (selectedSurgeon != null && !bill.assignedDoctors.Contains(selectedSurgeon))
            {
                bill.assignedDoctors.Add(selectedSurgeon);
            }

            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            this.forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            // Titre
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "AssignDoctors_Title".Translate(patient.Name.ToStringShort));
            Text.Font = GameFont.Small;

            float curY = 40f;

            // Sélection du chirurgien principal
            Text.Font = GameFont.Medium;
            string surgeonLabel = "AssignDoctors_SelectSurgeon".Translate();
            Rect surgeonLabelRect = new Rect(0, curY, inRect.width, 25f);
            Widgets.Label(surgeonLabelRect, surgeonLabel);
            // Dessiner la ligne sous le texte
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

                    // Ajouter le chirurgien principal aux médecins assignés s'il n'y est pas
                    if (!bill.assignedDoctors.Contains(selectedSurgeon))
                    {
                        bill.assignedDoctors.Add(selectedSurgeon);
                    }
                }

                surgeonY += 35f;
            }

            Widgets.EndScrollView();

            curY += 110f;

            // Sélection des médecins assistants
            Text.Font = GameFont.Medium;
            string assistantsLabel = "AssignDoctors_SelectAssistants".Translate();
            Rect assistantsLabelRect = new Rect(0, curY, inRect.width, 25f);
            Widgets.Label(assistantsLabelRect, assistantsLabel);
            // Dessiner la ligne sous le texte
            Widgets.DrawLineHorizontal(assistantsLabelRect.x, assistantsLabelRect.yMax - 2f, Text.CalcSize(assistantsLabel).x);
            Text.Font = GameFont.Small;
            curY += 30f;

            Rect assistantOutRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 70f);
            Rect assistantViewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f); // -1 pour ne pas compter le chirurgien principal

            Widgets.BeginScrollView(assistantOutRect, ref assistantScrollPosition, assistantViewRect);

            float assistantY = 0f;
            foreach (var doctor in availableDoctors)
            {
                if (doctor == selectedSurgeon)
                {
                    // Ne pas afficher le chirurgien principal dans la liste des assistants
                    continue;
                }

                bool isAssigned = bill.assignedDoctors.Contains(doctor);
                Rect rowRect = new Rect(0, assistantY, assistantViewRect.width, 30f);

                bool newIsAssigned = isAssigned;
                string label = "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level);
                Widgets.CheckboxLabeled(rowRect, label, ref newIsAssigned);

                if (newIsAssigned != isAssigned)
                {
                    if (newIsAssigned)
                    {
                        if (bill.assignedDoctors.Count < MultiDoctorSurgeryMod.settings.maxDoctors)
                            bill.assignedDoctors.Add(doctor);
                    }
                    else
                    {
                        bill.assignedDoctors.Remove(doctor);
                    }
                }

                assistantY += 35f;
            }

            Widgets.EndScrollView();

            // Boutons en bas
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Confirm".Translate()))
            {
                // Annuler les travaux en cours des chirurgiens et médecins précédents
                CancelOngoingJobs();

                // Stocker le chirurgien principal dans le bill
                bill.surgeon = selectedSurgeon;

                // S'assurer que le chirurgien principal est dans la liste des médecins assignés
                if (!bill.assignedDoctors.Contains(selectedSurgeon))
                {
                    bill.assignedDoctors.Add(selectedSurgeon);
                }

                // Assigner le bill au chirurgien principal
                bill.SetPawnRestriction(selectedSurgeon);

                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Cancel".Translate()))
            {
                // Annuler l'opération en supprimant le bill
                patient.BillStack.Bills.Remove(bill);
                Close();
            }
        }

        private void CancelOngoingJobs()
        {
            // Annuler le travail du chirurgien précédent s'il est en train d'opérer
            if (previousSurgeon != null && previousSurgeon.CurJob != null && previousSurgeon.CurJob.bill == bill)
            {
                previousSurgeon.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Annuler les travaux des médecins précédemment assignés
            if (previousAssignedDoctors != null)
            {
                foreach (var doctor in previousAssignedDoctors)
                {
                    if (doctor != previousSurgeon && doctor.CurJob != null && doctor.CurJob.targetA != null && doctor.CurJob.targetA.Thing == patient)
                    {
                        doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }
        }
    }
}

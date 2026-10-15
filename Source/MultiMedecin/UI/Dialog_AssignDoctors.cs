using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_AssignDoctors : Window
    {
        private readonly Pawn patient;
        private readonly RecipeDef recipe;
        private readonly BillMedicalEx bill;
        private readonly bool creating;
        private readonly SkillDef requiredSkill;
        private readonly List<Pawn> selectedAssistants;
        private List<Pawn> availableDoctors;
        private Pawn selectedSurgeon;
        private Vector2 surgeonScrollPosition;
        private Vector2 assistantScrollPosition;
        private bool sortBySkill = MultiDoctorSurgeryMod.settings.sortBySkillDefault;
        private bool isAscending = MultiDoctorSurgeryMod.settings.sortAscendingDefault;
        private bool committed;

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill, bool creating = false)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;
            this.creating = creating;
            requiredSkill = recipe.workSkill ?? SkillDefOf.Medicine;
            availableDoctors = SurgeryTeamUtility.AvailableDoctors(patient);
            selectedSurgeon = SurgeryTeamUtility.CanOperate(bill.surgeon, patient, recipe) ? bill.surgeon :
                availableDoctors.Where(p => SurgeryTeamUtility.CanOperate(p, patient, recipe))
                    .OrderByDescending(p => p.skills?.GetSkill(requiredSkill)?.Level ?? 0).FirstOrDefault();
            selectedAssistants = (bill.assignedDoctors ?? new List<Pawn>())
                .Where(p => p != selectedSurgeon && p != bill.surgeon && availableDoctors.Contains(p))
                .Distinct().Take(MultiDoctorSurgeryMod.settings.maxDoctors - 1).ToList();
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(460f, 720f);

        public override void PostClose()
        {
            base.PostClose();
            // Dismissing a new operation cancels it. Editing and cancelling preserves the old team.
            if (creating && !committed && patient.BillStack?.Bills.Contains(bill) == true)
            {
                SurgeryTeamUtility.StopSession(bill);
                patient.BillStack.Delete(bill);
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width, 35), "AssignDoctors_Title".Translate(patient.LabelShortCap));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(0, 40, rect.width / 2, 28), "AssignDoctors_SortByName".Translate()))
                ChangeSort(false);
            if (Widgets.ButtonText(new Rect(rect.width / 2, 40, rect.width / 2, 28), "AssignDoctors_SortBySkill".Translate()))
                ChangeSort(true);
            bool showMechs = MultiDoctorSurgeryMod.settings.showMechanoidDoctors;
            Widgets.CheckboxLabeled(new Rect(0, 78, rect.width, 28), "MultiDoctorSurgery_ShowMechanoidDoctors".Translate(), ref showMechs);
            if (showMechs != MultiDoctorSurgeryMod.settings.showMechanoidDoctors)
            {
                MultiDoctorSurgeryMod.settings.showMechanoidDoctors = showMechs;
                availableDoctors = SurgeryTeamUtility.AvailableDoctors(patient);
                selectedAssistants.RemoveAll(p => !availableDoctors.Contains(p));
                if (!availableDoctors.Contains(selectedSurgeon)) selectedSurgeon = null;
            }

            float speed = SurgeryTeamUtility.EstimateSpeed(selectedAssistants, recipe);
            float bonus = SurgeryTeamUtility.EstimateSuccess(selectedAssistants, recipe);
            Widgets.Label(new Rect(0, 115, rect.width, 28), "AssignDoctors_EstimatedSpeed".Translate(speed.ToString("F2")));
            Widgets.Label(new Rect(0, 145, rect.width, 28), "AssignDoctors_EstimatedSuccessBonus".Translate(bonus.ToString("P0")));
            string chance = SurgerySuccessPreview.TryEstimate(bill, selectedSurgeon, selectedAssistants, out var estimate)
                ? estimate.ToString("P0") : "—";
            Widgets.Label(new Rect(0, 175, rect.width, 28), "AssignDoctors_EstimatedSuccessChance".Translate(chance));
            string explanation = "AssignDoctors_EstimateExplanation".Translate();
            float explanationHeight = Text.CalcHeight(explanation, rect.width);
            Widgets.Label(new Rect(0, 207, rect.width, explanationHeight), explanation);

            float surgeonY = 215 + explanationHeight;
            Widgets.Label(new Rect(0, surgeonY, rect.width, 28), "AssignDoctors_SelectSurgeon".Translate());
            var sorted = SortedDoctors().ToList();
            var surgeons = sorted.Where(p => SurgeryTeamUtility.CanOperate(p, patient, recipe)).ToList();
            DrawDoctors(new Rect(0, surgeonY + 32, rect.width, 110), ref surgeonScrollPosition, surgeons, true);
            float assistantsY = surgeonY + 155;
            Widgets.Label(new Rect(0, assistantsY, rect.width, 28), "AssignDoctors_SelectAssistants".Translate());
            DrawDoctors(new Rect(0, assistantsY + 32, rect.width, Mathf.Max(20f, rect.height - assistantsY - 87)), ref assistantScrollPosition,
                sorted.Where(p => p != selectedSurgeon).ToList(), false);

            if (Widgets.ButtonText(new Rect(0, rect.height - 35, rect.width / 2, 35), "AssignDoctors_Confirm".Translate()))
            {
                if (patient.BillStack?.Bills.Contains(bill) != true)
                {
                    Close();
                    return;
                }
                if (!SurgeryTeamUtility.AssignTeam(bill, selectedSurgeon, selectedAssistants))
                {
                    Messages.Message("AssignDoctors_InvalidTeam".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                committed = true;
                Close();
            }
            if (Widgets.ButtonText(new Rect(rect.width / 2, rect.height - 35, rect.width / 2, 35), "AssignDoctors_Cancel".Translate()))
                Close();
        }

        private void DrawDoctors(Rect rect, ref Vector2 scroll, List<Pawn> doctors, bool surgeonList)
        {
            Widgets.BeginScrollView(rect, ref scroll, new Rect(0, 0, rect.width - 16, Mathf.Max(1, doctors.Count) * 32));
            try
            {
                if (doctors.Count == 0)
                    Widgets.Label(new Rect(0, 0, rect.width - 16, 30), "AssignDoctors_NoDoctors".Translate());
                for (int i = 0; i < doctors.Count; i++)
                {
                    var doctor = doctors[i];
                    var row = new Rect(0, i * 32, rect.width - 16, 30);
                    string label = DoctorLabel(doctor, requiredSkill);
                    if (surgeonList)
                    {
                        if (Widgets.RadioButtonLabeled(row, label, doctor == selectedSurgeon))
                        {
                            selectedSurgeon = doctor;
                            selectedAssistants.Remove(doctor);
                        }
                    }
                    else
                    {
                        bool assigned = selectedAssistants.Contains(doctor);
                        bool selected = assigned;
                        Widgets.CheckboxLabeled(row, label, ref selected);
                        if (selected && !assigned && selectedAssistants.Count < MultiDoctorSurgeryMod.settings.maxDoctors - 1)
                            selectedAssistants.Add(doctor);
                        else if (!selected) selectedAssistants.Remove(doctor);
                    }
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        internal static string DoctorLabel(Pawn doctor, SkillDef skill)
        {
            return doctor.RaceProps.IsMechanoid
                ? "AssignDoctors_MechEntry".Translate(doctor.LabelShortCap).ToString()
                : $"{doctor.LabelShortCap} ({skill.label}: {doctor.skills?.GetSkill(skill)?.Level ?? 0})";
        }

        private IEnumerable<Pawn> SortedDoctors()
        {
            if (sortBySkill)
                return isAscending ? availableDoctors.OrderBy(p => p.skills?.GetSkill(requiredSkill)?.Level ?? 0) :
                    availableDoctors.OrderByDescending(p => p.skills?.GetSkill(requiredSkill)?.Level ?? 0);
            return isAscending ? availableDoctors.OrderBy(p => p.LabelShort) : availableDoctors.OrderByDescending(p => p.LabelShort);
        }

        private void ChangeSort(bool bySkill)
        {
            isAscending = sortBySkill == bySkill ? !isAscending : !bySkill;
            sortBySkill = bySkill;
            MultiDoctorSurgeryMod.settings.sortBySkillDefault = sortBySkill;
            MultiDoctorSurgeryMod.settings.sortAscendingDefault = isAscending;
        }
    }
}

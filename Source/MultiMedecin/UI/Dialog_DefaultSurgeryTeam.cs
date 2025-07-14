using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_DefaultSurgeryTeam : Window
    {
        private List<Pawn> availableDoctors;
        private Vector2 surgeonScrollPosition;
        private Vector2 assistantScrollPosition;
        private Pawn selectedSurgeon;
        private List<Pawn> selectedAssistants = new List<Pawn>();

        // Sorting system for doctors
        private enum SortingMode { ByName, BySkill }
        private SortingMode sortingMode = SortingMode.BySkill;
        private bool isAscending = true;

        private List<Pawn> GetSortedDoctors()
        {
            IEnumerable<Pawn> doctors = sortingMode == SortingMode.ByName
                ? (isAscending
                    ? availableDoctors.OrderBy(d => d.Name.ToStringShort)
                    : availableDoctors.OrderByDescending(d => d.Name.ToStringShort))
                : (isAscending
                    ? availableDoctors.OrderBy(d => d.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                    : availableDoctors.OrderByDescending(d => d.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0));
            return doctors.ToList();
        }

        public Dialog_DefaultSurgeryTeam()
        {
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;

            var map = Find.CurrentMap;
            if (map != null)
            {
                availableDoctors = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    .ToList();
            }
            else
            {
                availableDoctors = new List<Pawn>();
            }

            var team = Find.World.GetComponent<DefaultSurgeryTeamComponent>();
            selectedSurgeon = team.defaultLeadSurgeon;
            if (team.defaultAssistants != null)
            {
                selectedAssistants = new List<Pawn>(team.defaultAssistants);
            }
        }

        public override Vector2 InitialSize => new Vector2(400f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, curY, inRect.width, 30f), "DefaultTeam_Title".Translate());
            Text.Font = GameFont.Small;
            curY += 40f;

            // Ensure assistant list doesn't contain invalid pawns or the lead surgeon
            selectedAssistants.RemoveAll(p => p == null || p == selectedSurgeon || !availableDoctors.Contains(p));

            // Sorting buttons similar to the assign doctor menu
            if (Widgets.ButtonText(new Rect(0, curY, inRect.width / 2f, 25f), "AssignDoctors_SortByName".Translate()))
            {
                if (sortingMode == SortingMode.ByName)
                {
                    isAscending = !isAscending;
                }
                else
                {
                    sortingMode = SortingMode.ByName;
                    isAscending = true;
                }
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, curY, inRect.width / 2f, 25f), "AssignDoctors_SortBySkill".Translate()))
            {
                if (sortingMode == SortingMode.BySkill)
                {
                    isAscending = !isAscending;
                }
                else
                {
                    sortingMode = SortingMode.BySkill;
                    isAscending = true;
                }
            }
            curY += 35f;

            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectSurgeon".Translate());
            curY += 30f;
            Rect outRect = new Rect(0f, curY, inRect.width, 100f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);
            Widgets.BeginScrollView(outRect, ref surgeonScrollPosition, viewRect);
            try
            {
                float y = 0f;
                var sortedDoctors = GetSortedDoctors();

                foreach (var doctor in sortedDoctors)
                {
                    Rect row = new Rect(0, y, viewRect.width, 30f);
                    bool isSel = doctor == selectedSurgeon;
                    string label = $"{doctor.Name.ToStringShort} ({SkillDefOf.Medicine.label}: {doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0})";
                    if (Widgets.RadioButtonLabeled(row, label, isSel))
                    {
                        selectedSurgeon = doctor;
                        selectedAssistants.Remove(doctor); // Avoid duplicates
                    }
                    y += 35f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            curY += 110f;

            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectAssistants".Translate());
            curY += 30f;
            outRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 70f);
            viewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f);
            Widgets.BeginScrollView(outRect, ref assistantScrollPosition, viewRect);
            try
            {
                float y = 0f;
                var sortedDoctors = GetSortedDoctors();

                foreach (var doctor in sortedDoctors)
                {
                    if (doctor == selectedSurgeon) continue;
                    Rect row = new Rect(0, y, viewRect.width, 30f);
                    bool isAssigned = selectedAssistants.Contains(doctor);
                    bool newState = isAssigned;
                    string label = $"{doctor.Name.ToStringShort} ({SkillDefOf.Medicine.label}: {doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0})";
                    Widgets.CheckboxLabeled(row, label, ref newState);
                    if (newState != isAssigned)
                    {
                        if (newState && selectedAssistants.Count < MultiDoctorSurgeryMod.settings.maxDoctors - 1)
                        {
                            if (!selectedAssistants.Contains(doctor))
                                selectedAssistants.Add(doctor);
                        }
                        else if (!newState)
                        {
                            selectedAssistants.Remove(doctor);
                        }
                    }
                    y += 35f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            curY = inRect.height - 35f;
            if (Widgets.ButtonText(new Rect(0, curY, inRect.width / 2f, 35f), "AssignDoctors_Confirm".Translate()))
            {
                var team = Find.World.GetComponent<DefaultSurgeryTeamComponent>();

                team.defaultLeadSurgeon = selectedSurgeon;
                team.defaultAssistants = selectedAssistants
                    .Where(p => p != null)
                    .ToList();

                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, curY, inRect.width / 2f, 35f), "AssignDoctors_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
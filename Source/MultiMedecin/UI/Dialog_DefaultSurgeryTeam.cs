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

            selectedSurgeon = MultiDoctorSurgeryMod.settings.defaultLeadSurgeon;
            if (MultiDoctorSurgeryMod.settings.defaultAssistants != null)
            {
                selectedAssistants = new List<Pawn>(MultiDoctorSurgeryMod.settings.defaultAssistants);
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

            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectSurgeon".Translate());
            curY += 30f;
            Rect outRect = new Rect(0f, curY, inRect.width, 100f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);
            Widgets.BeginScrollView(outRect, ref surgeonScrollPosition, viewRect);
            float y = 0f;
            foreach (var doctor in availableDoctors)
            {
                Rect row = new Rect(0, y, viewRect.width, 30f);
                bool isSel = doctor == selectedSurgeon;
                if (Widgets.RadioButtonLabeled(row, doctor.Name.ToStringShort, isSel))
                {
                    selectedSurgeon = doctor;
                }
                y += 35f;
            }
            Widgets.EndScrollView();
            curY += 110f;

            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectAssistants".Translate());
            curY += 30f;
            outRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 70f);
            viewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f);
            Widgets.BeginScrollView(outRect, ref assistantScrollPosition, viewRect);
            y = 0f;
            foreach (var doctor in availableDoctors)
            {
                if (doctor == selectedSurgeon) continue;
                Rect row = new Rect(0, y, viewRect.width, 30f);
                bool isAssigned = selectedAssistants.Contains(doctor);
                bool newState = isAssigned;
                Widgets.CheckboxLabeled(row, doctor.Name.ToStringShort, ref newState);
                if (newState != isAssigned)
                {
                    if (newState && selectedAssistants.Count < MultiDoctorSurgeryMod.settings.maxDoctors - 1)
                        selectedAssistants.Add(doctor);
                    else if (!newState)
                        selectedAssistants.Remove(doctor);
                }
                y += 35f;
            }
            Widgets.EndScrollView();

            curY = inRect.height - 35f;
            if (Widgets.ButtonText(new Rect(0, curY, inRect.width / 2f, 35f), "AssignDoctors_Confirm".Translate()))
            {
                MultiDoctorSurgeryMod.settings.defaultLeadSurgeon = selectedSurgeon;
                MultiDoctorSurgeryMod.settings.defaultAssistants = selectedAssistants.Where(p => p != null).ToList();
                MultiDoctorSurgeryMod.settings.Write();
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, curY, inRect.width / 2f, 35f), "AssignDoctors_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
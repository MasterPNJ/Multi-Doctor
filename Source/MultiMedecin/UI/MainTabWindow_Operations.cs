using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MultiDoctorSurgery.UI
{
    public class MainTabWindow_Operations : MainTabWindow
    {
        private Vector2 scrollPosition;

        public override void PreOpen()
        {
            base.PreOpen();
            this.scrollPosition = Vector2.zero;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Ne pas appeler base.DoWindowContents(inRect);

            List<BillMedicalEx> scheduledOperations = Find.Maps.SelectMany(map => map.mapPawns.FreeColonistsSpawned)
                .SelectMany(pawn => pawn.BillStack.Bills)
                .OfType<BillMedicalEx>()
                .ToList();

            float rowHeight = 30f;
            float contentHeight = scheduledOperations.Count * rowHeight;

            Rect scrollRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, scrollRect);

            float curY = 0f;

            // En-t�te des colonnes
            Rect headerRect = new Rect(0f, curY, scrollRect.width, rowHeight);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Patient".Translate());
            Widgets.Label(new Rect(headerRect.x + headerRect.width / 4f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Operation".Translate());
            Widgets.Label(new Rect(headerRect.x + headerRect.width / 2f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Surgeon".Translate());
            Widgets.Label(new Rect(headerRect.x + 3 * headerRect.width / 4f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Doctors".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            curY += rowHeight;

            foreach (var bill in scheduledOperations)
            {
                Rect rowRect = new Rect(0f, curY, scrollRect.width, rowHeight);

                DrawOperationRow(rowRect, bill);

                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawOperationRow(Rect rect, BillMedicalEx bill)
        {
            Widgets.DrawHighlightIfMouseover(rect);

            Text.Anchor = TextAnchor.MiddleLeft;

            float x = rect.x;
            float width = rect.width / 4f;

            // Patient
            Rect patientRect = new Rect(x, rect.y, width, rect.height);
            Widgets.Label(patientRect, bill.GiverPawn.Name.ToStringShort);
            x += width;

            // Op�ration
            Rect operationRect = new Rect(x, rect.y, width, rect.height);
            Widgets.Label(operationRect, bill.recipe.LabelCap);
            x += width;

            // Chirurgien
            Rect surgeonRect = new Rect(x, rect.y, width, rect.height);
            string surgeonName = bill.surgeon != null ? bill.surgeon.Name.ToStringShort : "OperationsTab_None".Translate().ToString();
            if (Widgets.ButtonText(surgeonRect, surgeonName))
            {
                List<FloatMenuOption> options = GetSurgeonOptions(bill);
                Find.WindowStack.Add(new FloatMenu(options));
            }
            x += width;

            // M�decins assign�s
            Rect doctorsRect = new Rect(x, rect.y, width, rect.height);
            if (Widgets.ButtonText(doctorsRect, "OperationsTab_ViewEdit".Translate()))
            {
                Find.WindowStack.Add(new UI.Dialog_AssignDoctors(bill.GiverPawn, bill.recipe, bill));
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private List<FloatMenuOption> GetSurgeonOptions(BillMedicalEx bill)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            var availableDoctors = bill.GiverPawn.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != bill.GiverPawn && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level);

            foreach (var doctor in availableDoctors)
            {
                options.Add(new FloatMenuOption(
                    "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level),
                    () =>
                    {
                        // Stocker le chirurgien pr�c�dent
                        var previousSurgeon = bill.surgeon;

                        // Mettre � jour le chirurgien
                        bill.surgeon = doctor;
                        bill.SetPawnRestriction(doctor);

                        // S'assurer que le nouveau chirurgien est dans les m�decins assign�s
                        if (!bill.assignedDoctors.Contains(doctor))
                        {
                            bill.assignedDoctors.Add(doctor);
                        }

                        // Annuler le travail du chirurgien pr�c�dent
                        if (previousSurgeon != null && previousSurgeon != doctor && previousSurgeon.CurJob != null && previousSurgeon.CurJob.bill == bill)
                        {
                            previousSurgeon.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }

                        // Annuler le travail du nouveau chirurgien au cas o� il serait d�j� engag� dans un autre travail
                        if (doctor.CurJob != null && doctor.CurJob.bill == bill)
                        {
                            doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }
                    }));
            }

            return options;
        }
    }
}
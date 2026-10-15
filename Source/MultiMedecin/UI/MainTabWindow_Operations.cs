using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class MainTabWindow_Operations : MainTabWindow
    {
        private Vector2 scrollPosition;

        public override void PreOpen()
        {
            base.PreOpen();
            scrollPosition = Vector2.zero;
        }

        public override void DoWindowContents(Rect rect)
        {
            var operations = Find.Maps.SelectMany(map => map.mapPawns.AllPawnsSpawned)
                .Where(p => p.BillStack != null).SelectMany(p => p.BillStack.Bills)
                .OfType<BillMedicalEx>().ToList();
            const float rowHeight = 32f;
            var view = new Rect(0, 0, rect.width - 16, (operations.Count + 1) * rowHeight);
            Widgets.BeginScrollView(new Rect(0, 0, rect.width, rect.height - 50), ref scrollPosition, view);
            try
            {
                string[] headers = { "OperationsTab_Patient", "OperationsTab_Operation", "OperationsTab_Surgeon", "OperationsTab_Doctors" };
                float width = view.width / 4;
                for (int i = 0; i < headers.Length; i++)
                    Widgets.Label(new Rect(i * width, 0, width, rowHeight), headers[i].Translate());
                for (int i = 0; i < operations.Count; i++)
                {
                    var bill = operations[i];
                    float y = (i + 1) * rowHeight;
                    Widgets.Label(new Rect(0, y, width, rowHeight), bill.GiverPawn.LabelShortCap);
                    Widgets.Label(new Rect(width, y, width, rowHeight), bill.recipe.LabelCap);
                    if (Widgets.ButtonText(new Rect(width * 2, y, width, rowHeight),
                        bill.surgeon?.LabelShortCap ?? "OperationsTab_None".Translate().ToString()))
                        Find.WindowStack.Add(new FloatMenu(GetSurgeonOptions(bill)));
                    if (Widgets.ButtonText(new Rect(width * 3, y, width, rowHeight), "OperationsTab_ViewEdit".Translate()))
                        Find.WindowStack.Add(new Dialog_AssignDoctors(bill.GiverPawn, bill.recipe, bill));
                }
            }
            finally { Widgets.EndScrollView(); }

            float buttonWidth = (rect.width - 20) / 3;
            if (Widgets.ButtonText(new Rect(0, rect.height - 40, buttonWidth, 35), "DefaultTeam_Button".Translate()))
                Find.WindowStack.Add(new Dialog_DefaultSurgeryTeam());
            var team = Find.World.GetComponent<DefaultSurgeryTeamComponent>();
            string state = (team.fastOperationEnabled ? "MultiDoctorSurgery_On" : "MultiDoctorSurgery_Off").Translate();
            if (Widgets.ButtonText(new Rect(buttonWidth + 10, rect.height - 40, buttonWidth, 35), "FastOperation_Label".Translate(state)))
            {
                if (!team.fastOperationEnabled && !SurgeryTeamUtility.IsDoctor(team.defaultLeadSurgeon, Find.CurrentMap))
                    Messages.Message("AssignDoctors_InvalidTeam".Translate(), MessageTypeDefOf.RejectInput, false);
                else team.fastOperationEnabled = !team.fastOperationEnabled;
            }
            if (Widgets.ButtonText(new Rect((buttonWidth + 10) * 2, rect.height - 40, buttonWidth, 35), "ManageExcludedOperations".Translate()))
                Find.WindowStack.Add(new Dialog_ManageExcludedOperations());
        }

        private List<FloatMenuOption> GetSurgeonOptions(BillMedicalEx bill)
        {
            var skill = bill.recipe.workSkill ?? SkillDefOf.Medicine;
            var options = new List<FloatMenuOption>();
            foreach (var doctor in SurgeryTeamUtility.AvailableDoctors(bill.GiverPawn)
                .Where(p => SurgeryTeamUtility.CanOperate(p, bill.GiverPawn, bill.recipe))
                .OrderByDescending(p => p.skills?.GetSkill(skill)?.Level ?? 0))
            {
                var chosen = doctor;
                options.Add(new FloatMenuOption(Dialog_AssignDoctors.DoctorLabel(chosen, skill), () =>
                {
                    if (!SurgeryTeamUtility.AssignTeam(bill, chosen, bill.assignedDoctors))
                        Messages.Message("AssignDoctors_InvalidTeam".Translate(), MessageTypeDefOf.RejectInput, false);
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("AssignDoctors_NoDoctors".Translate(), null));
            return options;
        }
    }
}

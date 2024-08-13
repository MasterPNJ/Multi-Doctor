using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MultiMedecin
{
    public class Dialog_ConfirmSurgery : Window
    {
        private Pawn surgeon;
        private Pawn patient;
        public Bill bill;
        private IBillGiver giver;
        private bool confirmed = false;

        public override Vector2 InitialSize => new Vector2(300f, 150f);

        public Dialog_ConfirmSurgery(Pawn surgeon, Pawn patient, Bill bill, IBillGiver giver)
        {
            this.surgeon = surgeon;
            this.patient = patient;
            this.bill = bill;
            this.giver = giver;
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            string text = $"Are you sure you want {this.surgeon.Name.ToStringShort} to operate on {this.patient.Name.ToStringShort}?";
            Widgets.Label(new Rect(0f, 0f, inRect.width, inRect.height - 35f), text);

            float buttonWidth = (inRect.width / 2f) - 10f;
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 35f, buttonWidth, 35f), "Confirm"))
            {
                confirmed = true;
                StartSurgery();
                this.Close();
            }
            if (Widgets.ButtonText(new Rect(buttonWidth + 10f, inRect.height - 35f, buttonWidth, 35f), "Cancel"))
            {
                this.Close();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!confirmed)
            {
                surgeon.jobs.EndCurrentJob(JobCondition.Incompletable);
            }
        }

        private void StartSurgery()
        {
            if (surgeon.jobs.curJob?.def == JobDefOf.DoBill && surgeon.jobs.curJob.targetA.Thing == giver)
            {
                Log.Warning($"{surgeon.Name.ToStringShort} already has a DoBill job for {patient.Name.ToStringShort}. Skipping job creation.");
                return;
            }

            Job job = new Job(JobDefOf.DoBill, patient, giver as Thing);
            job.bill = bill;
            surgeon.jobs.StartJob(job, JobCondition.InterruptForced);
            Messages.Message($"{surgeon.Name.ToStringShort} is starting the operation on {patient.Name.ToStringShort}.", MessageTypeDefOf.PositiveEvent);
        }
    }
}
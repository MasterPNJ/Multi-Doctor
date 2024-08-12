using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MultiMedecin
{
	public class JobDriver_DoBill : JobDriver
	{
		private const TargetIndex BillGiverInd = TargetIndex.A;
		private const TargetIndex PatientInd = TargetIndex.B;

		private IBillGiver BillGiver => (IBillGiver)job.GetTarget(BillGiverInd).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			Log.Message($"[MultiMedecin] {pawn.LabelShort} is trying to reserve {job.GetTarget(TargetIndex.A).Thing.LabelShort} for surgery.");
			return this.pawn.Reserve(this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(BillGiverInd);
			this.FailOnBurningImmobile(BillGiverInd);
			this.FailOn(() => !BillGiver.CurrentlyUsableForBills());
			AddEndCondition(() => BillGiver.CurrentlyUsableForBills() ? JobCondition.Ongoing : JobCondition.Incompletable);

			yield return Toils_Goto.GotoThing(BillGiverInd, PathEndMode.Touch);

			yield return Toils_Reserve.Reserve(PatientInd);

			Toil doBill = new Toil();
			doBill.initAction = () =>
			{
				Pawn patient = (Pawn)this.job.GetTarget(PatientInd).Thing;
				Pawn surgeon = this.pawn;
				Log.Message($"[MultiMedecin] {surgeon.LabelShort} is starting surgery on {patient.LabelShort}.");
				Messages.Message($"{surgeon.LabelShort} is performing surgery on {patient.LabelShort}.", MessageTypeDefOf.PositiveEvent);
			};
			doBill.tickAction = () =>
			{
				// Simulate surgery and check for success/failure
				// Ensure the patient stays in bed during surgery
				Pawn patient = (Pawn)this.job.GetTarget(PatientInd).Thing;
				if (patient.CurrentBed() == null)
				{
					Log.Error($"[MultiMedecin] {patient.LabelShort} is not in bed during surgery! Aborting.");
					EndJobWith(JobCondition.Incompletable);
				}
			};
			doBill.defaultCompleteMode = ToilCompleteMode.Delay;
			doBill.defaultDuration = 5000;
			yield return doBill;

			yield return Toils_Reserve.Release(PatientInd);
		}
	}
}
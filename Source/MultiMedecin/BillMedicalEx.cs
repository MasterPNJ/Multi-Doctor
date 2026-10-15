using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace MultiDoctorSurgery
{
    public class BillMedicalEx : Bill_Medical
    {
        public Pawn surgeon;
        // Existing save format: lead surgeon first, followed by assistants.
        public List<Pawn> assignedDoctors = new List<Pawn>();
        public bool SurgeryStarted;
        internal Job activeSurgeryJob;
        internal Toil activeWorkToil;
        internal bool workInProgress;

        public BillMedicalEx() { }
        public BillMedicalEx(RecipeDef recipe, List<Thing> uniqueIngredients) : base(recipe, uniqueIngredients) { }

        public void NormalizeTeam()
        {
            assignedDoctors = (assignedDoctors ?? new List<Pawn>())
                .Where(p => p != null && !p.Dead && !p.Destroyed && p != surgeon).Distinct()
                .Take(System.Math.Max(0, (MultiDoctorSurgeryMod.settings?.maxDoctors ?? 3) - 1)).ToList();
            if (surgeon != null && !surgeon.Dead && !surgeon.Destroyed)
                assignedDoctors.Insert(0, surgeon);
            else
                surgeon = null;
        }

        public override bool PawnAllowedToStartAnew(Pawn pawn)
        {
            // An unavailable default surgeon must not permanently block another doctor.
            if (surgeon != null && !SurgeryTeamUtility.CanOperate(surgeon, GiverPawn, recipe))
            {
                SurgeryTeamUtility.StopSession(this);
                assignedDoctors?.Remove(surgeon);
                surgeon = null;
                Compat.SetPawnRestrictionSafe(this, null);
            }
            return (surgeon == null || surgeon == pawn) && base.PawnAllowedToStartAnew(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref surgeon, "surgeon");
            Scribe_Collections.Look(ref assignedDoctors, "assignedDoctors", LookMode.Reference);
            Scribe_Values.Look(ref SurgeryStarted, "SurgeryStarted", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeTeam();
                // Restore progress through the vanilla job, then reconnect on its next work tick.
                SurgeryStarted = false;
                activeSurgeryJob = null;
                activeWorkToil = null;
                workInProgress = false;
                Compat.SetPawnRestrictionSafe(this, surgeon);
            }
        }
    }
}

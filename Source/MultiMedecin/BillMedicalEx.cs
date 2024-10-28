using RimWorld;
using Verse;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // New Bill_Medical subclass
    public class BillMedicalEx : Bill_Medical
    {
        public Pawn surgeon;
        public List<Pawn> assignedDoctors = new List<Pawn>();
        public bool SurgeryStarted = false; // New flag

        public BillMedicalEx(RecipeDef recipe, List<Thing> uniqueIngredients) : base(recipe, uniqueIngredients)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref surgeon, "surgeon");
            Scribe_Collections.Look(ref assignedDoctors, "assignedDoctors", LookMode.Reference);
            Scribe_Values.Look(ref SurgeryStarted, "SurgeryStarted", false); // Save the flag
        }
    }
}

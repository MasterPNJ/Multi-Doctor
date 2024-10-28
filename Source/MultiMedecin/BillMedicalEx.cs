using RimWorld;
using Verse;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // Nouvelle sous-classe de Bill_Medical
    public class BillMedicalEx : Bill_Medical
    {
        public Pawn surgeon;
        public List<Pawn> assignedDoctors = new List<Pawn>();
        public bool SurgeryStarted = false; // Nouveau flag

        public BillMedicalEx(RecipeDef recipe, List<Thing> uniqueIngredients) : base(recipe, uniqueIngredients)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref surgeon, "surgeon");
            Scribe_Collections.Look(ref assignedDoctors, "assignedDoctors", LookMode.Reference);
            Scribe_Values.Look(ref SurgeryStarted, "SurgeryStarted", false); // Sauvegarder le flag
        }
    }
}

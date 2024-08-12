using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MultiMedecin
{
    public class MultiSurgeonBill : Bill_Medical
    {
        public List<Pawn> additionalSurgeons;

        public MultiSurgeonBill() : base()
        {
            additionalSurgeons = new List<Pawn>();
        }

        public MultiSurgeonBill(RecipeDef recipe, Pawn pawn) : base(recipe, null)
        {
            additionalSurgeons = new List<Pawn>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref additionalSurgeons, "additionalSurgeons", LookMode.Reference);
        }
    }
}
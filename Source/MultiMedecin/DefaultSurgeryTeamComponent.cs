using System.Collections.Generic;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace MultiDoctorSurgery
{
    public class DefaultSurgeryTeamComponent : WorldComponent
    {
        public Pawn defaultLeadSurgeon;
        public List<Pawn> defaultAssistants = new List<Pawn>();
        public bool fastOperationEnabled = false;

        public DefaultSurgeryTeamComponent(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref defaultLeadSurgeon, "defaultLeadSurgeon");
            Scribe_Collections.Look(ref defaultAssistants, "defaultAssistants", LookMode.Reference);
            Scribe_Values.Look(ref fastOperationEnabled, "fastOperationEnabled", false);
        }
    }
}

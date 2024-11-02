using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace MultiDoctorSurgery
{
    public class Settings : ModSettings
    {
        public float speedMultiplierPerDoctor = 0.5f;
        public float successRateMultiplier = 0.25f;
        public int maxDoctors = 3;

        // New fields for speed and success limits
        public float maxSpeedBonus = 1.95f; // Default to 95%
        public float maxSuccessBonus = 0.98f; // Default to 98%

        public List<string> excludedOperations = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref speedMultiplierPerDoctor, "speedMultiplierPerDoctor", 0.5f);
            Scribe_Values.Look(ref successRateMultiplier, "successRateMultiplier", 0.25f);
            Scribe_Values.Look(ref maxDoctors, "maxDoctors", 3);

            // Save/load new fields
            Scribe_Values.Look(ref maxSpeedBonus, "maxSpeedBonus", 1.95f);
            Scribe_Values.Look(ref maxSuccessBonus, "maxSuccessBonus", 0.98f);

            Scribe_Collections.Look(ref excludedOperations, "excludedOperations", LookMode.Value);

            base.ExposeData();
        }
    }
}

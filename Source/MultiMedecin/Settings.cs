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

        public float mechSpeedBonus = 0.5f; // Bonus fixe de vitesse pour les mécanoïdes assistants
        public float mechSuccessBonus = 0.30f; // Bonus fixe de réussite pour les mécanoïdes assistants

        public List<string> excludedOperations = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref speedMultiplierPerDoctor, "speedMultiplierPerDoctor", 0.5f);
            Scribe_Values.Look(ref successRateMultiplier, "successRateMultiplier", 0.25f);
            Scribe_Values.Look(ref maxDoctors, "maxDoctors", 3);

            // Save/load new fields
            Scribe_Values.Look(ref maxSpeedBonus, "maxSpeedBonus", 1.95f);
            Scribe_Values.Look(ref maxSuccessBonus, "maxSuccessBonus", 0.98f);

            Scribe_Values.Look(ref mechSpeedBonus, "mechSpeedBonus", 0.5f);
            Scribe_Values.Look(ref mechSuccessBonus, "mechSuccessBonus", 0.30f);

            Scribe_Collections.Look(ref excludedOperations, "excludedOperations", LookMode.Value);

            base.ExposeData();
        }
    }
}

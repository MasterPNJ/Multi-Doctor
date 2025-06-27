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

        public string currentPreset = "Default"; // Default preset

        // Default team configuration
        public Pawn defaultLeadSurgeon;
        public List<Pawn> defaultAssistants = new List<Pawn>();
        public bool fastOperationEnabled = false;

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

            if (excludedOperations == null)
            {
                excludedOperations = new List<string>();
            }

            if (!excludedOperations.Contains("HarvestHemogenPack"))
            {
                excludedOperations.Add("HarvestHemogenPack");
            }

            base.ExposeData();
            Scribe_Values.Look(ref currentPreset, "currentPreset", "Default"); // Sauvegarde du preset actif
            Scribe_Collections.Look(ref excludedOperations, "excludedOperations", LookMode.Value);

            // Default team
            Scribe_References.Look(ref defaultLeadSurgeon, "defaultLeadSurgeon");
            Scribe_Collections.Look(ref defaultAssistants, "defaultAssistants", LookMode.Reference);
            Scribe_Values.Look(ref fastOperationEnabled, "fastOperationEnabled", false);
        }
    }
}

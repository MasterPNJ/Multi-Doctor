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
        public float maxSuccessBonus = 2f;

        public float mechSpeedBonus = 0.5f; // Bonus fixe de vitesse pour les mécanoïdes assistants
        public float mechSuccessBonus = 0.30f; // Bonus fixe de réussite pour les mécanoïdes assistants

        public List<string> excludedOperations = new List<string>();

        public string currentPreset = "Default"; // Default preset

        public bool sortBySkillDefault = true;   // true = compétence, false = nom
        public bool sortAscendingDefault = false;  // true = ascendant, false = descendant

        // Default team configuration
        /*
        public Pawn defaultLeadSurgeon;
        public List<Pawn> defaultAssistants = new List<Pawn>();
        public bool fastOperationEnabled = false;
        */

        public bool showMechanoidDoctors = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref speedMultiplierPerDoctor, "speedMultiplierPerDoctor", 0.5f);
            Scribe_Values.Look(ref successRateMultiplier, "successRateMultiplier", 0.25f);
            Scribe_Values.Look(ref maxDoctors, "maxDoctors", 3);

            Scribe_Values.Look(ref maxSpeedBonus, "maxSpeedBonus", 1.95f);
            Scribe_Values.Look(ref maxSuccessBonus, "maxSuccessBonus", 2f);

            Scribe_Values.Look(ref mechSpeedBonus, "mechSpeedBonus", 0.5f);
            Scribe_Values.Look(ref mechSuccessBonus, "mechSuccessBonus", 0.30f);

            Scribe_Collections.Look(ref excludedOperations, "excludedOperations", LookMode.Value);

            Scribe_Values.Look(ref currentPreset, "currentPreset", "Default");
            Scribe_Values.Look(ref sortBySkillDefault, "sortBySkillDefault", true);
            Scribe_Values.Look(ref sortAscendingDefault, "sortAscendingDefault", false);

            Scribe_Values.Look(ref showMechanoidDoctors, "showMechanoidDoctors", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (excludedOperations == null) excludedOperations = new List<string>();
                EnsureDefaultExclusions();
            }
        }

        private void EnsureDefaultExclusions()
        {
            void Add(string defName)
            {
                if (!string.IsNullOrEmpty(defName) && !excludedOperations.Contains(defName))
                    excludedOperations.Add(defName);
            }

            Add("HarvestHemogenPack");
            Add("ExtractHemogenPack");

            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                var workerName = r.workerClass?.Name ?? string.Empty;
                if (r.defName.StartsWith("Administer") || workerName == "Recipe_AdministerIngestible")
                    Add(r.defName);
            }
        }
    }
}

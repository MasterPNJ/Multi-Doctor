using System.Collections.Generic;
using System;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery
{
    public class Settings : ModSettings
    {
        public float speedMultiplierPerDoctor = 0.5f;
        public float successRateMultiplier = 0.25f;
        public int maxDoctors = 3;
        public float maxSpeedBonus = 1.95f;
        public float maxSuccessBonus = 2f;
        public float mechSpeedBonus = 0.5f;
        public float mechSuccessBonus = 0.30f;
        public List<string> excludedOperations = new List<string> { "ExtractHemogenPack", "HarvestHemogenPack" };
        public string currentPreset = "Default";
        public bool sortBySkillDefault = true;
        public bool sortAscendingDefault;
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
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            maxDoctors = Mathf.Clamp(maxDoctors, 1, 5);
            speedMultiplierPerDoctor = Clamp(speedMultiplierPerDoctor, 0, 1, 0.5f);
            successRateMultiplier = Clamp(successRateMultiplier, 0, 1, 0.25f);
            maxSpeedBonus = Clamp(maxSpeedBonus, 1, 3, 1.95f);
            maxSuccessBonus = Clamp(maxSuccessBonus, 0, 3, 2);
            mechSpeedBonus = Clamp(mechSpeedBonus, 0, 1, 0.5f);
            mechSuccessBonus = Clamp(mechSuccessBonus, 0, 1, 0.3f);
            EnsureDefaultExclusions();
            if (string.IsNullOrWhiteSpace(currentPreset)) currentPreset = "Default";
        }

        public void EnsureDefaultExclusions()
        {
            if (excludedOperations == null) excludedOperations = new List<string>();
            foreach (var name in new[] { "ExtractHemogenPack", "HarvestHemogenPack" })
                if (!excludedOperations.Contains(name)) excludedOperations.Add(name);
        }

        public static bool IsHemogenOperation(string name) =>
            name?.IndexOf("Hemogen", StringComparison.OrdinalIgnoreCase) >= 0;

        private static float Clamp(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
    }
}

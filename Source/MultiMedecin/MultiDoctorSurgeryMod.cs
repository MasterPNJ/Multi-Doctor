using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using MultiDoctorSurgery;

namespace MultiDoctorSurgery
{
    [StaticConstructorOnStartup]
    public class MultiDoctorSurgeryMod : Mod
    {
        public static Settings settings;

        public MultiDoctorSurgeryMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<Settings>();
            var harmony = new Harmony("com.multidoctor.surgery");
            harmony.PatchAll();
        }

        public override string SettingsCategory() => "MultiDoctorSurgery_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("MultiDoctorSurgery_SpeedMultiplier".Translate(settings.speedMultiplierPerDoctor.ToString("F2")));
            settings.speedMultiplierPerDoctor = listingStandard.Slider(settings.speedMultiplierPerDoctor, 0f, 1f);

            listingStandard.Label("MultiDoctorSurgery_SuccessMultiplier".Translate(settings.successRateMultiplier.ToString("F2")));
            settings.successRateMultiplier = listingStandard.Slider(settings.successRateMultiplier, 0f, 1f);

            listingStandard.Label("MultiDoctorSurgery_MaxDoctors".Translate(settings.maxDoctors));
            settings.maxDoctors = Mathf.RoundToInt(listingStandard.Slider(settings.maxDoctors, 1, 5));

            listingStandard.Label("MultiDoctorSurgery_MaxSpeedBonus".Translate(settings.maxSpeedBonus.ToString("P")));
            settings.maxSpeedBonus = listingStandard.Slider(settings.maxSpeedBonus, 1f, 3f);

            listingStandard.Label("MultiDoctorSurgery_MaxSuccessBonus".Translate(settings.maxSuccessBonus.ToString("P")));
            settings.maxSuccessBonus = listingStandard.Slider(settings.maxSuccessBonus, 0f, 1f);

            listingStandard.End();
            settings.Write();
        }
    }
}

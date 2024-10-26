using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

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

        public override string SettingsCategory() => "Multi-Doctor Surgery";
    }

    public class Settings : ModSettings
    {
        public float speedMultiplierPerDoctor = 0.5f;
        public float successRateMultiplier = 0.25f;
        public int maxDoctors = 3;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref speedMultiplierPerDoctor, "speedMultiplierPerDoctor", 0.5f);
            Scribe_Values.Look(ref successRateMultiplier, "successRateMultiplier", 0.25f);
            Scribe_Values.Look(ref maxDoctors, "maxDoctors", 3);
            base.ExposeData();
        }
    }

    public class CompMultiDoctor : ThingComp
    {
        public List<Pawn> assignedDoctors = new List<Pawn>();

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref assignedDoctors, "assignedDoctors", LookMode.Reference);
            base.PostExposeData();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.parent is Pawn patient && patient.IsColonistPlayerControlled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Assigner des médecins",
                    defaultDesc = "Sélectionnez plusieurs médecins pour l'opération de ce patient.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignDoctors", true),
                    action = () => Find.WindowStack.Add(new Dialog_AssignDoctors(patient))
                };
            }
        }
    }

    // Patch pour ajuster la chance de réussite de la chirurgie
    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class Patch_CheckSurgeryFail
    {
        public static void Prefix(Pawn surgeon, Pawn patient, List<Thing> ingredients, Bill bill)
        {
            var comp = patient.GetComp<CompMultiDoctor>();
            if (comp != null && comp.assignedDoctors.Count > 1)
            {
                // Calculer la chance de réussite moyenne des médecins assignés
                float totalSuccessChance = comp.assignedDoctors.Sum(d => d.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true));
                float averageSuccessChance = totalSuccessChance / comp.assignedDoctors.Count;

                // Ajuster la chance de réussite de la chirurgie
                float bonus = averageSuccessChance * MultiDoctorSurgeryMod.settings.successRateMultiplier;
                float originalChance = surgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true);
                surgeon.skills.GetSkill(SkillDefOf.Medicine).Level = Mathf.RoundToInt(originalChance + bonus);
            }
        }
    }

    // Patch pour ajuster la durée de la chirurgie
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    public static class Patch_MakeNewToils
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_DoBill __instance)
        {
            foreach (var toil in toils)
            {
                if (__instance.job != null && __instance.job.bill is Bill_Medical medicalBill)
                {
                    var patient = medicalBill.GiverPawn;
                    var comp = patient.GetComp<CompMultiDoctor>();

                    if (comp != null && comp.assignedDoctors.Count > 1)
                    {
                        float multiplier = 1f + ((comp.assignedDoctors.Count - 1) * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor);
                        toil.defaultDuration = Mathf.RoundToInt(toil.defaultDuration / multiplier);
                    }
                }
                yield return toil;
            }
        }
    }

    public class Dialog_AssignDoctors : Window
    {
        private Pawn patient;
        private List<Pawn> availableDoctors;
        private Vector2 scrollPosition;

        public Dialog_AssignDoctors(Pawn patient)
        {
            this.patient = patient;
            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            var comp = patient.GetComp<CompMultiDoctor>();

            Widgets.Label(new Rect(0, 0, inRect.width, 30f),
                "Sélectionner les médecins pour " + patient.Name.ToStringShort);

            Rect outRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float curY = 0;
            foreach (var doctor in availableDoctors)
            {
                bool isAssigned = comp.assignedDoctors.Contains(doctor);
                Rect rowRect = new Rect(0, curY, viewRect.width, 30f);

                bool newIsAssigned = isAssigned;
                Widgets.CheckboxLabeled(rowRect,
                    doctor.Name.ToStringShort + " (Médecine: " +
                    doctor.skills.GetSkill(SkillDefOf.Medicine).Level + ")",
                    ref newIsAssigned);

                if (newIsAssigned != isAssigned)
                {
                    if (newIsAssigned)
                    {
                        if (comp.assignedDoctors.Count < MultiDoctorSurgeryMod.settings.maxDoctors)
                            comp.assignedDoctors.Add(doctor);
                    }
                    else
                    {
                        comp.assignedDoctors.Remove(doctor);
                    }
                }

                curY += 35f;
            }

            Widgets.EndScrollView();
        }
    }
}

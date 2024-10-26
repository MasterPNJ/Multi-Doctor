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

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // Curseur pour le multiplicateur de vitesse
            listingStandard.Label($"Multiplicateur de vitesse par médecin assigné: {settings.speedMultiplierPerDoctor:F2}");
            settings.speedMultiplierPerDoctor = listingStandard.Slider(settings.speedMultiplierPerDoctor, 0f, 2f);

            // Curseur pour le multiplicateur de taux de réussite
            listingStandard.Label($"Multiplicateur de taux de réussite par médecin assigné: {settings.successRateMultiplier:F2}");
            settings.successRateMultiplier = listingStandard.Slider(settings.successRateMultiplier, 0f, 1f);

            // Curseur pour le nombre maximal de médecins assignables
            listingStandard.Label($"Nombre maximal de médecins assignables: {settings.maxDoctors}");
            settings.maxDoctors = Mathf.RoundToInt(listingStandard.Slider(settings.maxDoctors, 1, 5));

            listingStandard.End();
            settings.Write();
        }
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
        public static void Prefix(Pawn surgeon, Pawn patient, List<Thing> ingredients, Bill bill, ref float __state)
        {
            var comp = patient.GetComp<CompMultiDoctor>();
            if (comp != null && comp.assignedDoctors.Count > 1)
            {
                // Sauvegarder la valeur de réussite actuelle
                __state = surgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance);

                // Calculer la chance de réussite moyenne des médecins assignés
                float totalSuccessChance = comp.assignedDoctors.Sum(d => d.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true));
                float averageSuccessChance = totalSuccessChance / comp.assignedDoctors.Count;

                // Calculer le bonus à appliquer
                float bonus = averageSuccessChance * MultiDoctorSurgeryMod.settings.successRateMultiplier;

                // Appliquer le bonus temporaire
                float newSuccessChance = Mathf.Clamp(__state + bonus, 0f, 1f);
                surgeon.skills.GetSkill(SkillDefOf.Medicine).Level = Mathf.RoundToInt(newSuccessChance * 20);

                // Log des informations
                Log.Message($"[Multi-Doctor Surgery] Chirurgie sur {patient.Name.ToStringShort} par {surgeon.Name.ToStringShort}. Médecins assignés: {comp.assignedDoctors.Count}. Compétence originelle: {__state:P2}. Compétence après ajustement: {newSuccessChance:P2}. Chance de réussite moyenne des médecins: {averageSuccessChance:P2}. Bonus appliqué: {bonus:P2}");
            }
        }

        public static void Postfix(Pawn surgeon, Pawn patient, List<Thing> ingredients, Bill bill, float __state)
        {
            var comp = patient.GetComp<CompMultiDoctor>();
            if (comp != null && comp.assignedDoctors.Count > 1)
            {
                // Restaurer la valeur de réussite d'origine
                surgeon.skills.GetSkill(SkillDefOf.Medicine).Level = Mathf.RoundToInt(__state * 20);

                // Log des informations
                Log.Message($"[Multi-Doctor Surgery] Fin de l'opération sur {patient.Name.ToStringShort} par {surgeon.Name.ToStringShort}. Compétence restaurée à : {__state:P2}.");
            }
        }
    }

    // Patch pour ajuster la durée de la chirurgie et faire participer les autres médecins
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

                        // Ajouter une action pour faire participer les autres médecins
                        toil.AddPreTickAction(() =>
                        {
                            foreach (var doctor in comp.assignedDoctors)
                            {
                                if (doctor != __instance.pawn)
                                {
                                    // S'assurer que chaque médecin assigné se déplace près du lit du patient
                                    if (!doctor.Position.InHorDistOf(patient.Position, 2f))
                                    {
                                        Job job = new Job(JobDefOf.Goto, patient.Position);
                                        doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
                                    }
                                }
                            }
                        });

                        // Log des informations
                        Log.Message($"[Multi-Doctor Surgery] Médecins assignés pour l'opération sur {patient.Name.ToStringShort}: {string.Join(", ", comp.assignedDoctors.Select(d => d.Name.ToStringShort))}");
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

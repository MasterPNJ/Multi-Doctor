using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;
using System;
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
            Harmony.DEBUG = true;
        }

        public override string SettingsCategory() => "Multi-Doctor Surgery";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label($"Multiplicateur de vitesse par médecin assigné: {settings.speedMultiplierPerDoctor:F2}");
            settings.speedMultiplierPerDoctor = listingStandard.Slider(settings.speedMultiplierPerDoctor, 0f, 5f);

            listingStandard.Label($"Multiplicateur de taux de réussite par médecin assigné: {settings.successRateMultiplier:F2}");
            settings.successRateMultiplier = listingStandard.Slider(settings.successRateMultiplier, 0f, 1f);

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

    public class CompProperties_MultiDoctor : CompProperties
    {
        public CompProperties_MultiDoctor()
        {
            this.compClass = typeof(CompMultiDoctor);
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
    }

    [HarmonyPatch(typeof(HealthCardUtility), "CreateSurgeryBill")]
    public static class Patch_HealthCardUtility_CreateSurgeryBill
    {
        public static void Postfix(Pawn medPawn, RecipeDef recipe)
        {
            if (recipe.Worker is Recipe_Surgery)
            {
                Find.WindowStack.Add(new Dialog_AssignDoctors(medPawn, recipe));
            }
        }
    }


    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is Bill_Medical medicalBill)
            {
                var patient = medicalBill.GiverPawn;
                var comp = patient.GetComp<CompMultiDoctor>();

                if (comp != null && comp.assignedDoctors.Count > 1)
                {
                    float speedMultiplier = 1f + ((comp.assignedDoctors.Count - 1) * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor);
                    __result *= speedMultiplier;

                    // Log pour vérifier l'application du multiplicateur de vitesse
                    //Log.Message($"[Multi-Doctor Surgery] Multiplicateur de vitesse appliqué au médecin {pawn.Name.ToStringShort} lors de l'opération sur {patient.Name.ToStringShort}. Multiplicateur: {speedMultiplier:F2}");
                }
            }
        }
    }

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
                        // Ajouter une action pour faire participer les autres médecins
                        toil.AddPreInitAction(() =>
                        {
                            foreach (var doctor in comp.assignedDoctors)
                            {
                                if (doctor != __instance.pawn)
                                {
                                    // Assigner un travail de déplacement vers le patient
                                    Job gotoJob = new Job(JobDefOf.Goto, patient.Position);
                                    doctor.jobs.StartJob(gotoJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
                                    // Log.Message($"[Multi-Doctor Surgery] Médecin {doctor.Name.ToStringShort} se déplace vers {patient.Name.ToStringShort} pour assister l'opération.");
                                }
                            }
                        });

                        // Ajouter une action pour faire rester les médecins assignés autour du lit pendant l'opération
                        toil.AddPreTickAction(() =>
                        {
                            foreach (var doctor in comp.assignedDoctors)
                            {
                                if (doctor != __instance.pawn && !doctor.Position.InHorDistOf(patient.Position, 2f))
                                {
                                    // Vérifier que le médecin reste autour du lit
                                    Job gotoJob = new Job(JobDefOf.Goto, patient.Position);
                                    doctor.jobs.StartJob(gotoJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
                                    //Log.Message($"[Multi-Doctor Surgery] Médecin {doctor.Name.ToStringShort} ajuste sa position autour de {patient.Name.ToStringShort} pendant l'opération.");
                                }
                            }
                        });

                        // Log des informations sur la participation des médecins
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
        private RecipeDef recipe;
        private Bill_Medical bill;
        private List<Pawn> availableDoctors;
        private Vector2 scrollPosition;

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = patient.BillStack.Bills.Last() as Bill_Medical;

            var comp = patient.GetComp<CompMultiDoctor>();
            if (comp == null)
            {
                comp = new CompMultiDoctor();
                patient.AllComps.Add(comp);
            }

            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            this.forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            var comp = patient.GetComp<CompMultiDoctor>();

            Widgets.Label(new Rect(0, 0, inRect.width, 30f),
                "Sélectionner les médecins pour " + patient.Name.ToStringShort);

            Rect outRect = new Rect(0f, 40f, inRect.width, inRect.height - 80f);
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

                    // Log pour indiquer l'ajout ou la suppression d'un médecin
                    Log.Message($"[Multi-Doctor Surgery] Médecin {(newIsAssigned ? "ajouté" : "retiré")} pour l'opération sur {patient.Name.ToStringShort}: {doctor.Name.ToStringShort}");
                }

                curY += 35f;
            }

            Widgets.EndScrollView();

            // Bouton "Confirmer"
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f, 35f), "Confirmer"))
            {
                Close();
            }

            // Bouton "Annuler"
            if (Widgets.ButtonText(new Rect(inRect.width / 2f, inRect.height - 35f, inRect.width / 2f, 35f), "Annuler"))
            {
                // Annuler l'opération en supprimant le bill
                patient.BillStack.Bills.Remove(bill);
                Close();
            }
        }
    }
}

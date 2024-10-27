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

    // Nouvelle sous-classe de Bill_Medical
    public class BillMedicalEx : Bill_Medical
    {
        public Pawn surgeon;
        public List<Pawn> assignedDoctors = new List<Pawn>();

        public BillMedicalEx(RecipeDef recipe, List<Thing> uniqueIngredients) : base(recipe, uniqueIngredients)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref surgeon, "surgeon");
            Scribe_Collections.Look(ref assignedDoctors, "assignedDoctors", LookMode.Reference);
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "CreateSurgeryBill")]
    public static class Patch_HealthCardUtility_CreateSurgeryBill
    {
        public static bool Prefix(Pawn medPawn, RecipeDef recipe, BodyPartRecord part, List<Thing> uniqueIngredients, bool sendMessages)
        {
            if (recipe.Worker is Recipe_Surgery)
            {
                // Créer notre bill personnalisé
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                medPawn.BillStack.AddBill(bill);

                // Afficher l'interface pour assigner les médecins
                Find.WindowStack.Add(new Dialog_AssignDoctors(medPawn, recipe, bill));

                // Empêcher l'exécution de la méthode originale
                return false;
            }

            // Continuer l'exécution normale pour les autres recettes
            return true;
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class Patch_WorkSpeedMultiplier
    {
        public static void Postfix(ref float __result, StatRequest req, bool applyPostProcess)
        {
            if (req.Thing is Pawn pawn && pawn.CurJob != null && pawn.CurJob.bill is BillMedicalEx medicalBill)
            {
                var assignedDoctors = medicalBill.assignedDoctors;

                if (assignedDoctors != null && assignedDoctors.Count > 1)
                {
                    float speedMultiplier = 1f + ((assignedDoctors.Count - 1) * MultiDoctorSurgeryMod.settings.speedMultiplierPerDoctor);
                    __result *= speedMultiplier;
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
                if (__instance.job != null && __instance.job.bill is BillMedicalEx medicalBill)
                {
                    var patient = medicalBill.GiverPawn;
                    var assignedDoctors = medicalBill.assignedDoctors;
                    var surgeon = medicalBill.surgeon;

                    if (assignedDoctors != null && assignedDoctors.Count > 0)
                    {
                        // Vérifier que le chirurgien principal est bien celui qui exécute le travail
                        if (surgeon != null && surgeon != __instance.pawn)
                        {
                            // Annuler le travail si ce n'est pas le chirurgien principal
                            __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                            yield break;
                        }

                        // Ajouter une action pour faire participer les autres médecins
                        toil.AddPreInitAction(() =>
                        {
                            foreach (var doctor in assignedDoctors)
                            {
                                if (doctor != __instance.pawn)
                                {
                                    // Assigner un travail de déplacement vers le patient
                                    Job gotoJob = new Job(JobDefOf.Goto, patient.Position);
                                    doctor.jobs.StartJob(gotoJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
                                }
                            }
                        });

                        // Ajouter une action pour faire rester les médecins assignés autour du lit pendant l'opération
                        toil.AddPreTickAction(() =>
                        {
                            foreach (var doctor in assignedDoctors)
                            {
                                if (doctor != __instance.pawn && !doctor.Position.InHorDistOf(patient.Position, 2f))
                                {
                                    // Vérifier que le médecin reste autour du lit
                                    Job gotoJob = new Job(JobDefOf.Goto, patient.Position);
                                    doctor.jobs.StartJob(gotoJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
                                }
                            }
                        });

                        // Log des informations sur la participation des médecins
                        Log.Message($"[Multi-Doctor Surgery] Chirurgien principal: {__instance.pawn.Name.ToStringShort}. Médecins assistants pour l'opération sur {patient.Name.ToStringShort}: {string.Join(", ", assignedDoctors.Where(d => d != __instance.pawn).Select(d => d.Name.ToStringShort))}");
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
        private BillMedicalEx bill;
        private List<Pawn> availableDoctors;
        private Vector2 surgeonScrollPosition;
        private Vector2 assistantScrollPosition;
        private Pawn selectedSurgeon; // Chirurgien principal sélectionné

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;

            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            // Par défaut, sélectionner le médecin avec la meilleure compétence
            this.selectedSurgeon = availableDoctors.OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level).FirstOrDefault();

            // Ajouter le chirurgien principal à la liste des médecins assignés s'il n'y est pas déjà
            if (!bill.assignedDoctors.Contains(selectedSurgeon))
            {
                bill.assignedDoctors.Add(selectedSurgeon);
            }

            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            this.forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            // Titre
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Assigner les médecins pour " + patient.Name.ToStringShort);
            Text.Font = GameFont.Small;

            float curY = 40f;

            // Sélection du chirurgien principal
            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "Sélectionner le chirurgien principal :");
            curY += 30f;

            Rect surgeonOutRect = new Rect(0f, curY, inRect.width, 100f);
            Rect surgeonViewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);

            Widgets.BeginScrollView(surgeonOutRect, ref surgeonScrollPosition, surgeonViewRect);

            float surgeonY = 0f;
            foreach (var doctor in availableDoctors)
            {
                Rect rowRect = new Rect(0, surgeonY, surgeonViewRect.width, 30f);

                bool isSelected = doctor == selectedSurgeon;
                if (Widgets.RadioButtonLabeled(rowRect, $"{doctor.Name.ToStringShort} (Médecine: {doctor.skills.GetSkill(SkillDefOf.Medicine).Level})", isSelected))
                {
                    selectedSurgeon = doctor;

                    // Ajouter le chirurgien principal aux médecins assignés s'il n'y est pas
                    if (!bill.assignedDoctors.Contains(selectedSurgeon))
                    {
                        bill.assignedDoctors.Add(selectedSurgeon);
                    }
                }

                surgeonY += 35f;
            }

            Widgets.EndScrollView();

            curY += 110f;

            // Sélection des médecins assistants
            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "Sélectionner les médecins assistants :");
            curY += 30f;

            Rect assistantOutRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 70f);
            Rect assistantViewRect = new Rect(0f, 0f, inRect.width - 16f, (availableDoctors.Count - 1) * 35f); // -1 pour ne pas compter le chirurgien principal

            Widgets.BeginScrollView(assistantOutRect, ref assistantScrollPosition, assistantViewRect);

            float assistantY = 0f;
            foreach (var doctor in availableDoctors)
            {
                if (doctor == selectedSurgeon)
                {
                    // Ne pas afficher le chirurgien principal dans la liste des assistants
                    continue;
                }

                bool isAssigned = bill.assignedDoctors.Contains(doctor);
                Rect rowRect = new Rect(0, assistantY, assistantViewRect.width, 30f);

                bool newIsAssigned = isAssigned;
                Widgets.CheckboxLabeled(rowRect,
                    $"{doctor.Name.ToStringShort} (Médecine: {doctor.skills.GetSkill(SkillDefOf.Medicine).Level})",
                    ref newIsAssigned);

                if (newIsAssigned != isAssigned)
                {
                    if (newIsAssigned)
                    {
                        if (bill.assignedDoctors.Count < MultiDoctorSurgeryMod.settings.maxDoctors)
                            bill.assignedDoctors.Add(doctor);
                    }
                    else
                    {
                        bill.assignedDoctors.Remove(doctor);
                    }
                }

                assistantY += 35f;
            }

            Widgets.EndScrollView();

            // Boutons en bas
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f, 35f), "Confirmer"))
            {
                // Stocker le chirurgien principal dans le bill
                bill.surgeon = selectedSurgeon;

                // S'assurer que le chirurgien principal est dans la liste des médecins assignés
                if (!bill.assignedDoctors.Contains(selectedSurgeon))
                {
                    bill.assignedDoctors.Add(selectedSurgeon);
                }

                // Assigner le bill au chirurgien principal
                bill.SetPawnRestriction(selectedSurgeon);

                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f, inRect.height - 35f, inRect.width / 2f, 35f), "Annuler"))
            {
                // Annuler l'opération en supprimant le bill
                patient.BillStack.Bills.Remove(bill);
                Close();
            }
        }
    }
}

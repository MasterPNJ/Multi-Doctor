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

        public override string SettingsCategory() => "MultiDoctorSurgery_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("MultiDoctorSurgery_SpeedMultiplier".Translate(settings.speedMultiplierPerDoctor));
            settings.speedMultiplierPerDoctor = listingStandard.Slider(settings.speedMultiplierPerDoctor, 0f, 5f);

            listingStandard.Label("MultiDoctorSurgery_SuccessMultiplier".Translate(settings.successRateMultiplier));
            settings.successRateMultiplier = listingStandard.Slider(settings.successRateMultiplier, 0f, 1f);

            listingStandard.Label("MultiDoctorSurgery_MaxDoctors".Translate(settings.maxDoctors));
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
                        // Log.Message($"[Multi-Doctor Surgery] Chirurgien principal: {__instance.pawn.Name.ToStringShort}. Médecins assistants pour l'opération sur {patient.Name.ToStringShort}: {string.Join(", ", assignedDoctors.Where(d => d != __instance.pawn).Select(d => d.Name.ToStringShort))}");
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

        // Variables pour stocker les assignations précédentes
        private Pawn previousSurgeon;
        private List<Pawn> previousAssignedDoctors;

        public Dialog_AssignDoctors(Pawn patient, RecipeDef recipe, BillMedicalEx bill)
        {
            this.patient = patient;
            this.recipe = recipe;
            this.bill = bill;

            this.availableDoctors = patient.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != patient && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .ToList();

            // Stocker les assignations précédentes
            this.previousSurgeon = bill.surgeon;
            this.previousAssignedDoctors = new List<Pawn>(bill.assignedDoctors);

            // Si un chirurgien est déjà assigné, le sélectionner, sinon choisir le meilleur
            if (bill.surgeon != null && availableDoctors.Contains(bill.surgeon))
            {
                selectedSurgeon = bill.surgeon;
            }
            else
            {
                // Par défaut, sélectionner le médecin avec la meilleure compétence
                selectedSurgeon = availableDoctors.OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level).FirstOrDefault();
            }

            // Ajouter le chirurgien principal à la liste des médecins assignés s'il n'y est pas déjà
            if (selectedSurgeon != null && !bill.assignedDoctors.Contains(selectedSurgeon))
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
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "AssignDoctors_Title".Translate(patient.Name.ToStringShort));
            Text.Font = GameFont.Small;

            float curY = 40f;

            // Sélection du chirurgien principal
            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectSurgeon".Translate());
            curY += 30f;

            Rect surgeonOutRect = new Rect(0f, curY, inRect.width, 100f);
            Rect surgeonViewRect = new Rect(0f, 0f, inRect.width - 16f, availableDoctors.Count * 35f);

            Widgets.BeginScrollView(surgeonOutRect, ref surgeonScrollPosition, surgeonViewRect);

            float surgeonY = 0f;
            foreach (var doctor in availableDoctors)
            {
                Rect rowRect = new Rect(0, surgeonY, surgeonViewRect.width, 30f);

                bool isSelected = doctor == selectedSurgeon;
                string label = "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level);
                if (Widgets.RadioButtonLabeled(rowRect, label, isSelected))
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
            Widgets.Label(new Rect(0, curY, inRect.width, 25f), "AssignDoctors_SelectAssistants".Translate());
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
                string label = "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level);
                Widgets.CheckboxLabeled(rowRect, label, ref newIsAssigned);

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
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Confirm".Translate()))
            {
                // Annuler les travaux en cours des chirurgiens et médecins précédents
                CancelOngoingJobs();

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

            if (Widgets.ButtonText(new Rect(inRect.width / 2f, inRect.height - 35f, inRect.width / 2f, 35f), "AssignDoctors_Cancel".Translate()))
            {
                // Annuler l'opération en supprimant le bill
                patient.BillStack.Bills.Remove(bill);
                Close();
            }
        }

        private void CancelOngoingJobs()
        {
            // Annuler le travail du chirurgien précédent s'il est en train d'opérer
            if (previousSurgeon != null && previousSurgeon.CurJob != null && previousSurgeon.CurJob.bill == bill)
            {
                previousSurgeon.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Annuler les travaux des médecins précédemment assignés
            if (previousAssignedDoctors != null)
            {
                foreach (var doctor in previousAssignedDoctors)
                {
                    if (doctor != previousSurgeon && doctor.CurJob != null && doctor.CurJob.targetA != null && doctor.CurJob.targetA.Thing == patient)
                    {
                        doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }
        }
    }

    public class MainTabWindow_Operations : MainTabWindow
    {
        private Vector2 scrollPosition;

        public override void PreOpen()
        {
            base.PreOpen();
            this.scrollPosition = Vector2.zero;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Ne pas appeler base.DoWindowContents(inRect);

            List<BillMedicalEx> scheduledOperations = Find.Maps.SelectMany(map => map.mapPawns.FreeColonistsSpawned)
                .SelectMany(pawn => pawn.BillStack.Bills)
                .OfType<BillMedicalEx>()
                .ToList();

            float rowHeight = 30f;
            float contentHeight = scheduledOperations.Count * rowHeight;

            Rect scrollRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, scrollRect);

            float curY = 0f;

            // En-tête des colonnes
            Rect headerRect = new Rect(0f, curY, scrollRect.width, rowHeight);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Patient".Translate());
            Widgets.Label(new Rect(headerRect.x + headerRect.width / 4f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Operation".Translate());
            Widgets.Label(new Rect(headerRect.x + headerRect.width / 2f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Surgeon".Translate());
            Widgets.Label(new Rect(headerRect.x + 3 * headerRect.width / 4f, headerRect.y, headerRect.width / 4f, headerRect.height), "OperationsTab_Doctors".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            curY += rowHeight;

            foreach (var bill in scheduledOperations)
            {
                Rect rowRect = new Rect(0f, curY, scrollRect.width, rowHeight);

                DrawOperationRow(rowRect, bill);

                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawOperationRow(Rect rect, BillMedicalEx bill)
        {
            Widgets.DrawHighlightIfMouseover(rect);

            Text.Anchor = TextAnchor.MiddleLeft;

            float x = rect.x;
            float width = rect.width / 4f;

            // Patient
            Rect patientRect = new Rect(x, rect.y, width, rect.height);
            Widgets.Label(patientRect, bill.GiverPawn.Name.ToStringShort);

            x += width;

            // Opération
            Rect operationRect = new Rect(x, rect.y, width, rect.height);
            Widgets.Label(operationRect, bill.recipe.LabelCap);

            x += width;

            // Chirurgien
            Rect surgeonRect = new Rect(x, rect.y, width, rect.height);
            string surgeonName = bill.surgeon != null ? bill.surgeon.Name.ToStringShort : "OperationsTab_None".Translate().ToString();
            if (Widgets.ButtonText(surgeonRect, surgeonName))
            {
                List<FloatMenuOption> options = GetSurgeonOptions(bill);
                Find.WindowStack.Add(new FloatMenu(options));
            }

            x += width;

            // Médecins assignés
            Rect doctorsRect = new Rect(x, rect.y, width, rect.height);
            if (Widgets.ButtonText(doctorsRect, "OperationsTab_ViewEdit".Translate()))
            {
                Find.WindowStack.Add(new Dialog_AssignDoctors(bill.GiverPawn, bill.recipe, bill));
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private List<FloatMenuOption> GetSurgeonOptions(BillMedicalEx bill)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            var availableDoctors = bill.GiverPawn.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != bill.GiverPawn && !p.Dead && !p.Downed && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .OrderByDescending(d => d.skills.GetSkill(SkillDefOf.Medicine).Level);

            foreach (var doctor in availableDoctors)
            {
                options.Add(new FloatMenuOption(
                    "AssignDoctors_DoctorEntry".Translate(doctor.Name.ToStringShort, doctor.skills.GetSkill(SkillDefOf.Medicine).Level),
                    () =>
                    {
                        // Stocker le chirurgien précédent
                        var previousSurgeon = bill.surgeon;

                        // Mettre à jour le chirurgien
                        bill.surgeon = doctor;
                        bill.SetPawnRestriction(doctor);

                        // S'assurer que le nouveau chirurgien est dans les médecins assignés
                        if (!bill.assignedDoctors.Contains(doctor))
                        {
                            bill.assignedDoctors.Add(doctor);
                        }

                        // Annuler le travail du chirurgien précédent
                        if (previousSurgeon != null && previousSurgeon != doctor && previousSurgeon.CurJob != null && previousSurgeon.CurJob.bill == bill)
                        {
                            previousSurgeon.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }

                        // Annuler le travail du nouveau chirurgien au cas où il serait déjà engagé dans un autre travail
                        if (doctor.CurJob != null && doctor.CurJob.bill == bill)
                        {
                            doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }
                    }));
            }

            return options;
        }
    }
}

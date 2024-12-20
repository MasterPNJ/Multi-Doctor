using RimWorld;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_ManageExcludedOperations : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private List<RecipeDef> allRecipes;
        private string searchText = "";
        private string currentPreset = "Default";

        public override Vector2 InitialSize => new Vector2(500f, 700f);

        public Dialog_ManageExcludedOperations()
        {
            this.allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;

            // Charger le preset actif depuis les paramètres
            if (MultiDoctorSurgeryMod.settings != null)
            {
                currentPreset = MultiDoctorSurgeryMod.settings.currentPreset ?? "Default";
            }
            else
            {
                Log.Error("[MultiDoctorSurgery] Settings not initialized correctly.");
            }

            if (MultiDoctorSurgeryMod.settings.excludedOperations == null)
            {
                MultiDoctorSurgeryMod.settings.excludedOperations = new List<string>();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (MultiDoctorSurgeryMod.settings == null)
            {
                Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Error: Settings not initialized.");
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "ExcludedOperations".Translate());

            // preset actif
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inRect.width - 200f, 0, 200f, 30f), $"Preset: {currentPreset}");
            Text.Anchor = TextAnchor.UpperLeft;

            // Search bar
            Rect searchRect = new Rect(0, 40f, inRect.width - 16f, 30f);
            searchText = Widgets.TextField(searchRect, searchText);

            Text.Font = GameFont.Small;
            float curY = 80f; // Start below the search bar to avoid overlap

            // Recipe filtering based on search text
            List<RecipeDef> filteredRecipes = allRecipes.FindAll(recipe =>
                recipe.Worker is Recipe_Surgery && (string.IsNullOrEmpty(searchText) || recipe.label.ToLower().Contains(searchText.ToLower()))
            );

            // Calculate content height to ensure the last item is fully displayed
            float contentHeight = filteredRecipes.Count * 30f + 10f;
            Rect scrollOutRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 50f);
            Rect scrollViewRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect);

            try
            {
                // Display each recipe as a checkbox entry
                float rowY = 0f; // Reset Y position inside scroll view
                foreach (var recipe in filteredRecipes)
                {
                    bool isExcluded = MultiDoctorSurgeryMod.settings.excludedOperations.Contains(recipe.defName);
                    Rect rowRect = new Rect(0, rowY, scrollViewRect.width, 30f);

                    bool originalState = isExcluded;
                    Widgets.CheckboxLabeled(rowRect, recipe.label, ref isExcluded);

                    // Handle changes to the exclusion state
                    if (isExcluded != originalState)
                    {
                        if (isExcluded)
                        {
                            MultiDoctorSurgeryMod.settings.excludedOperations.Add(recipe.defName);
                        }
                        else
                        {
                            MultiDoctorSurgeryMod.settings.excludedOperations.Remove(recipe.defName);
                        }

                        // Save changes to settings preset
                        if (!string.IsNullOrEmpty(currentPreset))
                        {
                            SaveExcludedOperations(currentPreset);
                        }
                    }

                    rowY += 30f;
                }
            }
            catch (System.NullReferenceException ex)
            {
                Log.Error("[MultiDoctorSurgery] NullReferenceException in DoWindowContents: " + ex.Message);
            }

            Widgets.EndScrollView();

            float buttonWidth = 200f;
            float buttonHeight = 35f;
            float buttonSpacing = 10f;

            Rect saveButtonRect = new Rect(inRect.width / 2f - buttonWidth - buttonSpacing / 2f, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);
            Rect loadButtonRect = new Rect(inRect.width / 2f + buttonSpacing / 2f, inRect.height - buttonHeight - 10f, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(saveButtonRect, "Create Preset", true, false, true))
            {
                Find.WindowStack.Add(new Dialog_NamePreset((presetName) =>
                {
                    // Crée un nouveau preset et bascule dessus
                    SaveExcludedOperations(presetName);
                    currentPreset = presetName;
                    Messages.Message($"Preset {presetName} created and selected.", MessageTypeDefOf.PositiveEvent, false);
                }));
            }

            if (Widgets.ButtonText(loadButtonRect, "Load Preset", true, false, true))
            {
                Find.WindowStack.Add(new Dialog_SelectPreset((presetName) => LoadExcludedOperations(presetName)));
            }

        }

        private void SaveExcludedOperations(string presetName)
        {
            string filePath = Path.Combine(GenFilePaths.ConfigFolderPath, $"ExcludedOperations_{presetName}.xml");
            Scribe.saver.InitSaving(filePath, "ExcludedOperations");
            Scribe_Collections.Look(ref MultiDoctorSurgeryMod.settings.excludedOperations, "excludedOperations", LookMode.Value);
            Scribe.saver.FinalizeSaving();

            // Update
            currentPreset = presetName;
            Messages.Message($"Preset {presetName} saved.", MessageTypeDefOf.PositiveEvent, false);

            MultiDoctorSurgeryMod.settings.currentPreset = presetName;
            MultiDoctorSurgeryMod.settings.Write(); // save preset actif
        }

        private void LoadExcludedOperations(string presetName)
        {
            string filePath = Path.Combine(GenFilePaths.ConfigFolderPath, $"ExcludedOperations_{presetName}.xml");
            if (File.Exists(filePath))
            {
                Scribe.loader.InitLoading(filePath);
                Scribe_Collections.Look(ref MultiDoctorSurgeryMod.settings.excludedOperations, "excludedOperations", LookMode.Value);
                Scribe.loader.FinalizeLoading();

                // Update
                currentPreset = presetName;
                MultiDoctorSurgeryMod.settings.Write();

                Messages.Message($"Preset {presetName} loaded.", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Log.Error($"Preset {presetName} not found.");
            }

            MultiDoctorSurgeryMod.settings.currentPreset = presetName;
            MultiDoctorSurgeryMod.settings.Write(); // save preset actif
        }

    }
}

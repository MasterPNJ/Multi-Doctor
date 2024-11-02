using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_ManageExcludedOperations : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private List<RecipeDef> allRecipes;

        public override Vector2 InitialSize => new Vector2(500f, 700f);

        public Dialog_ManageExcludedOperations()
        {
            this.allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;

            // Ensure settings and excludedOperations are initialized
            if (MultiDoctorSurgeryMod.settings == null)
            {
                Log.Error("MultiDoctorSurgeryMod.settings is null. Ensure it is initialized correctly.");
                return;
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

            Text.Font = GameFont.Small;
            float curY = 40f;

            Rect scrollOutRect = new Rect(0f, curY, inRect.width, inRect.height - 50f);
            Rect scrollViewRect = new Rect(0f, 0f, inRect.width - 16f, allRecipes.Count * 30f);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect);

            try
            {
                foreach (var recipe in allRecipes)
                {
                    if (recipe.Worker is Recipe_Surgery)
                    {
                        bool isExcluded = MultiDoctorSurgeryMod.settings.excludedOperations.Contains(recipe.defName);
                        Rect rowRect = new Rect(0, curY, scrollViewRect.width, 30f);

                        Widgets.CheckboxLabeled(rowRect, recipe.label, ref isExcluded);
                        if (isExcluded)
                        {
                            if (!MultiDoctorSurgeryMod.settings.excludedOperations.Contains(recipe.defName))
                            {
                                MultiDoctorSurgeryMod.settings.excludedOperations.Add(recipe.defName);
                            }
                        }
                        else
                        {
                            MultiDoctorSurgeryMod.settings.excludedOperations.Remove(recipe.defName);
                        }

                        curY += 30f;
                    }
                }
            }
            catch (System.NullReferenceException ex)
            {
                Log.Error("NullReferenceException in DoWindowContents: " + ex.Message);
            }

            Widgets.EndScrollView();
        }
    }
}

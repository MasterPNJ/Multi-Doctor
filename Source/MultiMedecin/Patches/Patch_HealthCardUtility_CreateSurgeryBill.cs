using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(HealthCardUtility), "CreateSurgeryBill")]
    public static class Patch_HealthCardUtility_CreateSurgeryBill
    {
        public static bool Prefix(Pawn medPawn, RecipeDef recipe, BodyPartRecord part, ref List<Thing> uniqueIngredients, bool sendMessages, ref Bill_Medical __result)
        {
            // Validate inputs
            if (medPawn == null || recipe == null)
            {
                Log.Error("[MultiDoctorSurgery] Null reference in CreateSurgeryBill. medPawn or recipe is null.");
                return true; // Let the base method handle the case
            }

            // Ensure settings are initialised
            if (MultiDoctorSurgeryMod.settings == null)
            {
                Log.Error("[MultiDoctorSurgery] Settings not initialized. Falling back to vanilla bill creation.");
                return true;
            }

            // Some mods create surgery bills before the pawn is spawned
            if (medPawn.Map == null)
            {
                Log.Warning("[MultiDoctorSurgery] Pawn map is null, skipping custom interface.");
                return true;
            }

            // Check if the recipe is for xenogerm implantation
            if (recipe.defName == "ImplantXenogerm")
            {
                // Allow the game to handle xenogerm implantation normally without our custom interface
                return true;
            }

            // Check if the operation is in the exclusion list
            if (MultiDoctorSurgeryMod.settings.excludedOperations != null &&
                MultiDoctorSurgeryMod.settings.excludedOperations.Contains(recipe.defName))
            {
                // If the operation is excluded, allow the base game method to execute
                return true;
            }

            // Hard-guard for hemogen & "Administer X" so we never open our dialog for them
            if (recipe.defName.IndexOf("Hemogen", StringComparison.OrdinalIgnoreCase) >= 0
                || recipe.defName.StartsWith("Administer")
                || recipe.Worker?.GetType().Name == "Recipe_AdministerIngestible")
            {
                return true;
            }

            // Safety: ensure the ingredients list is not null
            if (uniqueIngredients == null)
            {
                uniqueIngredients = new List<Thing>();
            }

            if (recipe.Worker is Recipe_Surgery)
            {
                // Create our personalized bill
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                // Ensure the medPawn has a BillStack
                if (medPawn.BillStack == null)
                {
                    Log.Error($"[MultiDoctorSurgery] medPawn {medPawn.Name.ToStringShort} does not have a BillStack. Skipping.");
                    return true; // Fallback to base methodmedPawn.BillStack.AddBill(bill);
                }

                medPawn.BillStack.AddBill(bill);
                __result = bill;

                var team = Find.World.GetComponent<DefaultSurgeryTeamComponent>();
                // If fast operation is enabled and a default team exists, assign it automatically
                if (team.fastOperationEnabled && team.defaultLeadSurgeon != null)
                {
                    bill.surgeon = team.defaultLeadSurgeon;
                    bill.assignedDoctors.Clear();
                    bill.assignedDoctors.Add(team.defaultLeadSurgeon);
                    foreach (var p in team.defaultAssistants)
                    {
                        if (p == null) continue;
                        if (bill.assignedDoctors.Count >= MultiDoctorSurgeryMod.settings.maxDoctors) break;
                        bill.assignedDoctors.Add(p);
                    }
                    Compat.SetPawnRestrictionSafe(bill, bill.surgeon);
                }
                else
                {
                    // Display the interface for assigning doctors
                    Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));
                }

                // Prevent execution of the original method
                return false;
            }

            // Continue normal execution for other recipes
            return true;
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(HealthCardUtility), "CreateSurgeryBill")]
    public static class Patch_HealthCardUtility_CreateSurgeryBill
    {
        public static bool Prefix(Pawn medPawn, RecipeDef recipe, BodyPartRecord part, List<Thing> uniqueIngredients, bool sendMessages)
        {
            // Validate inputs
            if (medPawn == null || recipe == null)
            {
                Log.Error("[MultiDoctorSurgery] Null reference in CreateSurgeryBill. medPawn or recipe is null.");
                return true; // Let the base method handle the case
            }

            if (part == null && recipe.appliedOnFixedBodyParts != null && recipe.appliedOnFixedBodyParts.Count > 0)
            {
                Log.Warning("[MultiDoctorSurgery] Warning: part is null, but the recipe requires a body part. Skipping custom behavior.");
                return true; // Laisser le jeu continuer avec la méthode originale
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

            try
            {
                if (recipe.Worker is Recipe_Surgery)
                {
                    // Create our personalized bill
                    var bill = new BillMedicalEx(recipe, uniqueIngredients);
                    bill.Part = part;

                    // Ensure the medPawn has a BillStack
                    if (medPawn.BillStack == null)
                    {
                        Log.Error($"[MultiDoctorSurgery] medPawn {medPawn.Name.ToStringShort} does not have a BillStack. Skipping.");
                        return true; // Fallback to base method
                    }

                    medPawn.BillStack.AddBill(bill);

                    // Display the interface for assigning doctors
                    Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));

                    // Prevent execution of the original method
                    return false;
                }
            }
            catch (System.NullReferenceException ex)
            {
                Log.Error($"[MultiDoctorSurgery] NullReferenceException in CreateSurgeryBill: {ex.Message}\n{ex.StackTrace}");
                return true; // Retourner au comportement de base pour éviter un crash
            }
            catch (System.Exception ex)
            {
                Log.Error($"[MultiDoctorSurgery] Unexpected exception in CreateSurgeryBill: {ex.Message}\n{ex.StackTrace}");
                return true; // Retourner au comportement de base pour éviter un crash
            }
            // Continue normal execution for other recipes
            return true;
        }
    }
}

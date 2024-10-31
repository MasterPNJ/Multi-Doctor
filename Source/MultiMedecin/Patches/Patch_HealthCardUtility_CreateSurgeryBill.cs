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
            // Check if the recipe is for xenogerm implantation
            if (recipe.defName == "ImplantXenogerm")
            {
                // Allow the game to handle xenogerm implantation normally without our custom interface
                return true;
            }

            if (recipe.Worker is Recipe_Surgery)
            {
                // Create our personalized bill
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                medPawn.BillStack.AddBill(bill);

                // Display the interface for assigning doctors
                Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));

                // Prevent execution of the original method
                return false;
            }

            // Continue normal execution for other recipes
            return true;
        }
    }
}

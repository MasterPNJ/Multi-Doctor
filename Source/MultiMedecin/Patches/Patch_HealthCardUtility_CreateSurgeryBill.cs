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
            if (recipe.Worker is Recipe_Surgery)
            {
                // Create our personalised bill
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                medPawn.BillStack.AddBill(bill);

                // Display the interface for assigning doctors
                Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));

                // Prevent execution of the original method
                return false;
            }

            // Continue normal execution for other revenues
            return true;
        }
    }
}

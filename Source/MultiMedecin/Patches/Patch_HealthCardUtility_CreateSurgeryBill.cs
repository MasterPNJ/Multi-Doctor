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
                // Cr�er notre bill personnalis�
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                medPawn.BillStack.AddBill(bill);

                // Afficher l'interface pour assigner les m�decins
                Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));

                // Emp�cher l'ex�cution de la m�thode originale
                return false;
            }

            // Continuer l'ex�cution normale pour les autres recettes
            return true;
        }
    }
}

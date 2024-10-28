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
                // Créer notre bill personnalisé
                var bill = new BillMedicalEx(recipe, uniqueIngredients);
                bill.Part = part;

                medPawn.BillStack.AddBill(bill);

                // Afficher l'interface pour assigner les médecins
                Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, recipe, bill));

                // Empêcher l'exécution de la méthode originale
                return false;
            }

            // Continuer l'exécution normale pour les autres recettes
            return true;
        }
    }
}

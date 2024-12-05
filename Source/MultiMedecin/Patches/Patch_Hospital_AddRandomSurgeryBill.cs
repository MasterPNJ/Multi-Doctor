using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch]
    public static class Patch_Hospital_AddRandomSurgeryBill
    {
        public static bool Prepare()
        {
            bool isHospitalModActive = ModLister.GetActiveModWithIdentifier("adamas.hospital") != null;
            Log.Message($"[MultiDoctorSurgery] Hospital mod detected: {isHospitalModActive}");
            return isHospitalModActive;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            // Récupère la méthode cible de manière dynamique
            var type = AccessTools.TypeByName("Hospital.Utilities.SurgeryUtility");
            return AccessTools.Method(type, "AddRandomSurgeryBill");
        }

        public static bool Prefix(object pawn, object patientData, object hospital)
        {
            // Vérification de base
            if (pawn == null || !(pawn is Pawn guestPawn))
            {
                Log.Error("[MultiDoctorSurgery] Invalid pawn passed to Prefix. Skipping surgery bill creation.");
                return false;
            }

            Log.Message($"[MultiDoctorSurgery] Intercepted surgery bill creation for guest: {guestPawn.Name.ToStringShort}.");

            // Vérifie si le pawn a une BillStack
            if (guestPawn.BillStack == null)
            {
                Log.Warning($"[MultiDoctorSurgery] Guest {guestPawn.Name.ToStringShort} does not have a BillStack. Skipping surgery bill creation.");
                return false;
            }

            // Récupère la recette associée (via le champ CureRecipe)
            var cureRecipeField = AccessTools.Field(patientData.GetType(), "CureRecipe");
            if (cureRecipeField == null)
            {
                Log.Error("[MultiDoctorSurgery] Failed to find 'CureRecipe' field in patientData. Skipping surgery.");
                return false;
            }

            var recipeDef = cureRecipeField.GetValue(patientData) as RecipeDef;
            if (recipeDef == null)
            {
                Log.Warning($"[MultiDoctorSurgery] No valid CureRecipe found for guest {guestPawn.Name.ToStringShort}. Skipping surgery.");
                return false;
            }

            Log.Message($"[MultiDoctorSurgery] Found surgery recipe: {recipeDef.defName}.");

            // Ajout d'un nouveau BillMedicalEx
            try
            {
                var bill = new BillMedicalEx(recipeDef, new List<Thing>());
                guestPawn.BillStack.AddBill(bill);

                Log.Message($"[MultiDoctorSurgery] Successfully added BillMedicalEx for guest: {guestPawn.Name.ToStringShort}.");
                return false; // Empêche la méthode originale de continuer
            }
            catch (System.Exception ex)
            {
                Log.Error($"[MultiDoctorSurgery] Exception while adding BillMedicalEx: {ex.Message}\n{ex.StackTrace}");
                return true; // Laisser le mod original gérer l'erreur
            }
        }
    }
}

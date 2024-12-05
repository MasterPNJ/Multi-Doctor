using HarmonyLib;
using Verse;
using RimWorld;
using System;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(Bill), nameof(Bill.SetPawnRestriction))]
    public static class Patch_Bill_SetPawnRestriction
    {
        public static bool Prefix(Bill __instance, Pawn pawn)
        {
            try
            {
                if (pawn == null)
                {
                    Log.Warning($"[MultiDoctorSurgery] Attempted to restrict a null pawn to a bill. Ignoring.");
                    return false; // Ignore l'appel original
                }

                if (__instance == null)
                {
                    Log.Warning($"[MultiDoctorSurgery] Bill is null while trying to restrict {pawn?.Name?.ToStringShort ?? "unknown"}.");
                    return false; // Ignore l'appel original
                }

                if (ModLister.GetActiveModWithIdentifier("tactical.groups") != null)
                {
                    // Continue normalement si tout est valide
                    return true;
                }
            }
            catch (ArgumentNullException ex)
            {
                Log.Warning($"[MultiDoctorSurgery] Null argument in Tactical Groups mod when restricting pawn {pawn?.Name?.ToStringShort ?? "unknown"}: {ex.Message}");
                return false; // Ignore l'appel original
            }
            catch (Exception ex)
            {
                Log.Error($"[MultiDoctorSurgery] Unexpected error while restricting pawn {pawn?.Name?.ToStringShort ?? "unknown"}: {ex.Message}");
                return false; // Ignore l'appel original
            }

            return true; // Continue si aucun problème détecté
        }
    }
}

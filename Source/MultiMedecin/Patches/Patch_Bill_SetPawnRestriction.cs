using HarmonyLib;
using RimWorld;
using Verse;
using System;
using MultiDoctorSurgery;

[HarmonyPatch(typeof(Bill), "SetPawnRestriction")]
[HarmonyPriority(Priority.First)]
public static class Patch_Bill_SetPawnRestriction
{
    public static bool Prefix(Bill __instance, Pawn pawn)
    {
        try
        {
            if (__instance == null || pawn == null)
            {
                Log.Warning("Patch_Bill_SetPawnRestriction: Bill or Pawn is null. Skipping restriction.");
                return true;
            }

            if (__instance is BillMedicalEx medicalBill)
            {
                if (medicalBill.surgeon == null || medicalBill.surgeon != pawn)
                {
                    Log.Warning("Patch_Bill_SetPawnRestriction: Surgeon is either null or not matching the assigned pawn. Skipping restriction.");
                    return false;
                }
            }

            // Verify if the dictionary is valid before checking keys
            if (__instance.Map != null && __instance.Map.mapPawns != null)
            {
                // Example of accessing safely with ContainsKey
                if (!__instance.Map.mapPawns.FreeColonistsSpawned.Contains(pawn))
                {
                    Log.Warning("Patch_Bill_SetPawnRestriction: Pawn is not in FreeColonistsSpawned. Skipping restriction.");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Exception in Patch_Bill_SetPawnRestriction: {ex}");
        }

        return true;
    }
}

using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiMedecin
{
    [HarmonyPatch(typeof(BillStack))]
    [HarmonyPatch("AddBill")]
    public static class Patch_BillStack_AddBill
    {
        public static void Prefix(Bill bill)
        {
            Log.Message("Patch_BillStack_AddBill: Prefix called");
            if (bill is Bill_Medical medicalBill)
            {
                Log.Message("Patch_BillStack_AddBill: Medical bill detected");
            }
        }
    }
}
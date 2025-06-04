using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery
{
    public static class Compat
    {
        private const string ColonyGroupsPackageId = "derekbickley.ltocolonygroupsfinal";

        public static bool IsColonyGroupsActive => ModLister.GetActiveModWithIdentifier(ColonyGroupsPackageId, false) != null;

        public static void SetPawnRestrictionSafe(Bill bill, Pawn pawn)
        {
            if (IsColonyGroupsActive)
            {
                // Avoid calling SetPawnRestriction to prevent Tactical Groups patch errors
                var field = AccessTools.Field(typeof(Bill), "pawnRestriction");
                if (field != null)
                {
                    field.SetValue(bill, pawn);
                }
                else
                {
                    bill.SetPawnRestriction(pawn);
                }
            }
            else
            {
                bill.SetPawnRestriction(pawn);
            }
        }
    }
}
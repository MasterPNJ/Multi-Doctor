using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery.Patches
{
    [HarmonyPatch(typeof(HealthCardUtility), "CreateSurgeryBill")]
    public static class Patch_HealthCardUtility_CreateSurgeryBill
    {
        private static bool ShouldCustomize(Pawn patient, RecipeDef recipe)
        {
            return patient?.Map != null && MultiDoctorSurgeryMod.settings != null &&
                recipe?.Worker is Recipe_Surgery && recipe.defName != "ImplantXenogerm" &&
                !Settings.IsHemogenOperation(recipe.defName) &&
                !recipe.defName.StartsWith("Administer", StringComparison.OrdinalIgnoreCase) &&
                MultiDoctorSurgeryMod.settings.excludedOperations?.Contains(recipe.defName) != true;
        }

        // Replace only construction. Keep all vanilla warnings, messages and other side effects.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var constructor = AccessTools.Constructor(typeof(Bill_Medical),
                new[] { typeof(RecipeDef), typeof(List<Thing>) });
            var factory = AccessTools.Method(typeof(Patch_HealthCardUtility_CreateSurgeryBill), nameof(CreateBill));
            bool replaced = false;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor))
                {
                    var patient = new CodeInstruction(OpCodes.Ldarg_0);
                    patient.labels.AddRange(instruction.labels);
                    patient.blocks.AddRange(instruction.blocks);
                    yield return patient;
                    yield return new CodeInstruction(OpCodes.Call, factory);
                    replaced = true;
                }
                else yield return instruction;
            }
            if (!replaced)
                Log.Error("[MultiDoctorSurgery] Surgery bill constructor not found. Custom surgery teams are unavailable; vanilla surgery remains active.");
        }

        public static Bill_Medical CreateBill(RecipeDef recipe, List<Thing> ingredients, Pawn patient)
        {
            return ShouldCustomize(patient, recipe)
                ? new BillMedicalEx(recipe, ingredients)
                : new Bill_Medical(recipe, ingredients);
        }

        public static void Postfix(Pawn medPawn, ref Bill_Medical __result)
        {
            if (!(__result is BillMedicalEx bill)) return;
            var team = Find.World?.GetComponent<DefaultSurgeryTeamComponent>();
            if (team?.fastOperationEnabled == true &&
                SurgeryTeamUtility.AssignTeam(bill, team.defaultLeadSurgeon, team.defaultAssistants)) return;
            Find.WindowStack.Add(new UI.Dialog_AssignDoctors(medPawn, bill.recipe, bill, creating: true));
        }
    }
}

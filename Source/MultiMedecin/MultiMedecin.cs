using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using Verse.AI;

namespace MultiMedecin
{
    public class MultiMedecinMod : Mod
    {
        public MultiMedecinMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.example.multimedecin");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class FloatMenuMakerMap_AddHumanlikeOrders_Patch
    {
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (pawn.skills.GetSkill(SkillDefOf.Medicine).Level >= 5)
            {
                opts.Add(new FloatMenuOption("Assist with Surgery", () =>
                {
                    // Logic to add pawn to surgery assistance
                    LocalTargetInfo target = new LocalTargetInfo(IntVec3.FromVector3(clickPos));
                    Job job = new Job(JobDefOf.DoBill, target)
                    {
                        count = 1
                    };
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryStartNewDoBillJob")]
    public static class WorkGiver_DoBill_TryStartNewDoBillJob_Patch
    {
        public static void Postfix(ref Job __result, Pawn pawn, IBillGiver giver)
        {
            if (__result?.def == JobDefOf.DoBill && giver is Building_Bed)
            {
                // Modify job to include multiple doctors
                List<Pawn> additionalDoctors = GetAdditionalDoctors(pawn, (Building_Bed)giver);
                foreach (var doctor in additionalDoctors)
                {
                    Job additionalJob = new Job(JobDefOf.DoBill, (Building_Bed)giver);
                    doctor.jobs.TryTakeOrderedJob(additionalJob, JobTag.Misc);
                }
            }
        }

        private static List<Pawn> GetAdditionalDoctors(Pawn mainDoctor, Building_Bed bed)
        {
            // Logic to find additional doctors
            List<Pawn> additionalDoctors = new List<Pawn>();
            foreach (Pawn pawn in bed.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn != mainDoctor && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                {
                    additionalDoctors.Add(pawn);
                }
            }
            return additionalDoctors;
        }
    }

    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class Recipe_Surgery_CheckSurgeryFail_Patch
    {
        public static void Postfix(bool __result, Pawn surgeon, Pawn patient, List<Thing> ingredients, Bill bill)
        {
            if (__result) // If surgery failed
            {
                // Combine skills of multiple doctors
                var additionalDoctors = GetAdditionalDoctorsAssisting(patient);
                float combinedSkill = surgeon.skills.GetSkill(SkillDefOf.Medicine).Level;
                foreach (var doctor in additionalDoctors)
                {
                    combinedSkill += doctor.skills.GetSkill(SkillDefOf.Medicine).Level * 0.5f; // Adjust contribution weight as needed
                }

                float successChance = combinedSkill / (surgeon.skills.GetSkill(SkillDefOf.Medicine).Level + additionalDoctors.Count * 0.5f);
                if (!Rand.Chance(successChance))
                {
                    TendUtility.DoTend(patient, surgeon, null);
                }
            }
        }

        private static List<Pawn> GetAdditionalDoctorsAssisting(Pawn patient)
        {
            // Logic to find additional doctors assisting in the surgery
            List<Pawn> additionalDoctors = new List<Pawn>();
            foreach (Pawn pawn in patient.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.DoBill && pawn.CurJob.targetA.Thing == patient)
                {
                    additionalDoctors.Add(pawn);
                }
            }
            return additionalDoctors;
        }
    }
}
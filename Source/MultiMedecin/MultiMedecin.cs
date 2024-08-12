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
            IntVec3 intVec = IntVec3.FromVector3(clickPos);
            Building_Bed bed = intVec.GetEdifice(pawn.Map) as Building_Bed;
            if (bed != null && bed.Medical && pawn.skills.GetSkill(SkillDefOf.Medicine).Level >= 5)
            {
                Pawn patient = bed.GetCurOccupant(0); // Get the patient on the bed
                if (patient != null && patient.CurrentBed() == bed)
                {
                    opts.Add(new FloatMenuOption("Assist with Surgery", () =>
                    {
                        List<FloatMenuOption> assistOptions = new List<FloatMenuOption>();
                        foreach (Pawn colonist in bed.Map.mapPawns.FreeColonistsSpawned)
                        {
                            if (colonist.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) && colonist != pawn)
                            {
                                assistOptions.Add(new FloatMenuOption(colonist.LabelShort, delegate
                                {
                                    Job assistJob = new Job(DefDatabase<JobDef>.GetNamed("AssistWithSurgery"), bed)
                                    {
                                        count = 1,
                                        targetB = patient,
                                        targetC = pawn
                                    };
                                    colonist.jobs.TryTakeOrderedJob(assistJob, JobTag.Misc);
                                    Log.Message($"[MultiMedecin] {colonist.LabelShort} will assist with the surgery.");
                                    Messages.Message(colonist.LabelShort + " will assist with the surgery.", MessageTypeDefOf.PositiveEvent, false);
                                }));
                            }
                        }
                        if (assistOptions.Count > 0)
                        {
                            Find.WindowStack.Add(new FloatMenu(assistOptions));
                        }
                        else
                        {
                            Messages.Message("No qualified colonists available to assist with the surgery.", MessageTypeDefOf.RejectInput, false);
                        }
                    }));
                }
            }
        }
    }
}
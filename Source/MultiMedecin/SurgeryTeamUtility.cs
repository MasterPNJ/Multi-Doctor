using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MultiDoctorSurgery
{
    public static class SurgeryTeamUtility
    {
        public static bool IsDoctor(Pawn doctor, Map map)
        {
            if (doctor == null || map == null || !doctor.Spawned || doctor.Map != map || doctor.Dead ||
                doctor.Destroyed || doctor.Downed || doctor.Drafted || doctor.InMentalState ||
                doctor.Faction != Faction.OfPlayer || doctor.jobs == null ||
                doctor.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) != true)
                return false;
            if (doctor.RaceProps.IsMechanoid)
                return doctor.RaceProps.mechEnabledWorkTypes?.Contains(WorkTypeDefOf.Doctor) == true;
            return !doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) &&
                doctor.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true;
        }

        public static bool CanAssist(Pawn doctor, Pawn patient, bool checkReachability = true)
        {
            return patient != null && !patient.Dead && doctor != patient && IsDoctor(doctor, patient.Map) &&
                (!checkReachability || doctor.CanReach(patient, PathEndMode.Touch, Danger.Deadly));
        }

        public static bool CanOperate(Pawn doctor, Pawn patient, RecipeDef recipe)
        {
            return recipe != null && CanAssist(doctor, patient) && recipe.PawnSatisfiesSkillRequirements(doctor);
        }

        public static List<Pawn> AvailableDoctors(Pawn patient)
        {
            return patient?.Map?.mapPawns.AllPawnsSpawned
                .Where(p => CanAssist(p, patient) &&
                    (MultiDoctorSurgeryMod.settings.showMechanoidDoctors || !p.RaceProps.IsMechanoid))
                .ToList() ?? new List<Pawn>();
        }

        public static bool AssignTeam(BillMedicalEx bill, Pawn lead, IEnumerable<Pawn> assistants)
        {
            if (bill == null || !CanOperate(lead, bill.GiverPawn, bill.recipe)) return false;
            var selected = (assistants ?? Enumerable.Empty<Pawn>())
                .Where(p => p != lead && CanAssist(p, bill.GiverPawn)).Distinct()
                .Take(Mathf.Max(0, MultiDoctorSurgeryMod.settings.maxDoctors - 1)).ToList();
            var previousLead = bill.surgeon;
            StopSession(bill);
            if (previousLead?.CurJob?.bill == bill)
                previousLead.jobs.EndCurrentJob(JobCondition.InterruptForced);
            bill.surgeon = lead;
            bill.assignedDoctors = new List<Pawn> { lead };
            bill.assignedDoctors.AddRange(selected);
            Compat.SetPawnRestrictionSafe(bill, lead);
            return true;
        }

        public static bool IsOperating(BillMedicalEx bill)
        {
            return bill != null && bill.SurgeryStarted && bill.activeSurgeryJob != null &&
                bill.surgeon?.CurJob == bill.activeSurgeryJob &&
                bill.surgeon.CurJob.bill == bill &&
                bill.activeWorkToil != null && bill.GiverPawn != null && !bill.GiverPawn.Dead &&
                bill.GiverPawn.BillStack?.Bills.Contains(bill) == true;
        }

        public static bool IsWorking(BillMedicalEx bill)
        {
            return IsOperating(bill) && bill.workInProgress;
        }

        public static bool IsActiveAssistant(BillMedicalEx bill, Pawn doctor)
        {
            return IsOperating(bill) && doctor != bill.surgeon && CanAssist(doctor, bill.GiverPawn, false) &&
                bill.assignedDoctors?.Contains(doctor) == true &&
                doctor.CurJob?.def == MyCustomJobDefs.AssistSurgeryLoop && doctor.CurJob.bill == bill &&
                doctor.Position.AdjacentTo8WayOrInside(bill.GiverPawn.Position);
        }

        public static void BeginWork(BillMedicalEx bill, Pawn lead, Toil toil)
        {
            if (bill == null || lead?.CurJob?.bill != bill) return;
            bill.workInProgress = true;
            if (bill.activeSurgeryJob == lead.CurJob && bill.activeWorkToil == toil) return;
            if (!CanOperate(lead, bill.GiverPawn, bill.recipe) || (bill.surgeon != null && bill.surgeon != lead))
            {
                lead.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }
            bill.surgeon = lead;
            bill.NormalizeTeam();
            bill.activeSurgeryJob = lead.CurJob;
            bill.activeWorkToil = toil;
            bill.SurgeryStarted = true;
            foreach (var doctor in bill.assignedDoctors.ToList())
            {
                if (doctor == lead || !CanAssist(doctor, bill.GiverPawn)) continue;
                var current = doctor.CurJob;
                if (current?.def == MyCustomJobDefs.AssistSurgeryLoop && current.bill == bill) continue;
                // Never steal another surgery, treatment, or explicit player order.
                if (current != null && (current.bill != null || current.playerForced ||
                    current.def == MyCustomJobDefs.AssistSurgeryLoop || current.def == JobDefOf.TendPatient)) continue;
                var job = JobMaker.MakeJob(MyCustomJobDefs.AssistSurgeryLoop, bill.GiverPawn);
                job.bill = bill;
                doctor.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
            }
        }

        public static void StopSession(BillMedicalEx bill, Job expectedJob = null)
        {
            if (bill == null || (expectedJob != null && bill.activeSurgeryJob != expectedJob)) return;
            bill.SurgeryStarted = false;
            bill.activeSurgeryJob = null;
            bill.activeWorkToil = null;
            bill.workInProgress = false;
            foreach (var doctor in (bill.assignedDoctors ?? new List<Pawn>()).ToList())
                if (doctor?.CurJob?.def == MyCustomJobDefs.AssistSurgeryLoop && doctor.CurJob.bill == bill)
                    doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public static IEnumerable<Pawn> Assistants(BillMedicalEx bill, bool activeOnly)
        {
            return (bill?.assignedDoctors ?? Enumerable.Empty<Pawn>())
                .Where(p => p != null && p != bill.surgeon &&
                    (activeOnly ? IsActiveAssistant(bill, p) : CanAssist(p, bill.GiverPawn))).Distinct();
        }

        public static float SpeedMultiplier(BillMedicalEx bill, bool activeOnly = true)
        {
            return EstimateSpeed(Assistants(bill, activeOnly), bill?.recipe);
        }

        public static float EstimateSpeed(IEnumerable<Pawn> assistants, RecipeDef recipe)
        {
            var settings = MultiDoctorSurgeryMod.settings;
            var skill = recipe?.workSkill ?? SkillDefOf.Medicine;
            float speed = 1f;
            foreach (var assistant in assistants.Where(p => p != null).Distinct())
                speed += assistant.RaceProps.IsMechanoid ? settings.mechSpeedBonus :
                    (assistant.skills?.GetSkill(skill)?.Level ?? 0) * settings.speedMultiplierPerDoctor / 20f;
            return Mathf.Clamp(speed, 1f, Mathf.Max(1f, settings.maxSpeedBonus));
        }

        public static float SuccessBonus(BillMedicalEx bill, bool activeOnly = true)
        {
            return EstimateSuccess(Assistants(bill, activeOnly), bill?.recipe);
        }

        public static float EstimateSuccess(IEnumerable<Pawn> assistants, RecipeDef recipe)
        {
            var settings = MultiDoctorSurgeryMod.settings;
            var skill = recipe?.workSkill ?? SkillDefOf.Medicine;
            float bonus = 0f;
            foreach (var assistant in assistants.Where(p => p != null).Distinct())
                bonus += assistant.RaceProps.IsMechanoid ? settings.mechSuccessBonus :
                    (assistant.skills?.GetSkill(skill)?.Level ?? 0) * settings.successRateMultiplier / 20f;
            return Mathf.Max(0f, bonus);
        }

        public static float ApplySuccessBonus(float baseValue, float bonus)
        {
            // A configured cap can limit the increase, but must never penalize the base stat.
            return Mathf.Max(baseValue, Mathf.Min(baseValue + bonus, MultiDoctorSurgeryMod.settings.maxSuccessBonus));
        }
    }
}

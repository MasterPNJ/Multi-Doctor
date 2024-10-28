using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // JobDriver personnalisé pour les assistants
    public class JobDriver_AssistSurgeryLoop : JobDriver
    {
        // Ne pas réserver le patient pour éviter les conflits
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true; // Indique que les réservations sont réussies sans réellement réserver
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Aller près du patient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Boucle d'assistance
            Toil assistToil = new Toil();
            assistToil.initAction = () =>
            {
                Log.Message($"{pawn.Name} a commencé le Job AssistSurgeryLoop pour {job.targetA.Thing.Label}");
                Log.Message($"Bill associé au job de l'assistant : {job.bill?.recipe?.defName ?? "Aucun"}");
            };
            assistToil.tickAction = () =>
            {
                // Optionnel : Ajouter des actions d'assistance ici
                // Par exemple, jouer une animation, interagir avec le patient, etc.

                // Vérifier si la chirurgie est toujours en cours
                if (IsSurgeryOngoing())
                {
                    Log.Message($"{pawn.Name} confirme que la chirurgie est en cours pour {job.targetA.Thing.Label}");
                }
                else
                {
                    Log.Message($"{pawn.Name} ne détecte pas la chirurgie en cours pour {job.targetA.Thing.Label}. Attente...");
                    // Ne pas terminer le job, simplement attendre que la chirurgie commence
                }
            };
            assistToil.defaultCompleteMode = ToilCompleteMode.Never; // Le job ne se termine jamais automatiquement
            yield return assistToil;
        }

        private bool IsSurgeryOngoing()
        {
            // Vérifier si le patient est toujours en cours d'opération
            Pawn patient = (Pawn)job.targetA.Thing;
            var medicalBill = this.job.bill as BillMedicalEx;
            bool ongoing = medicalBill != null && medicalBill.SurgeryStarted;
            Log.Message($"{pawn.Name} vérifie si la chirurgie est en cours pour {patient.Label} : {ongoing}");
            return ongoing;
        }
    }
}

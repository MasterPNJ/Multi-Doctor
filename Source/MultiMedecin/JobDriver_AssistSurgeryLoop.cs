using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace MultiDoctorSurgery
{
    // JobDriver personnalis� pour les assistants
    public class JobDriver_AssistSurgeryLoop : JobDriver
    {
        // Ne pas r�server le patient pour �viter les conflits
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true; // Indique que les r�servations sont r�ussies sans r�ellement r�server
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Aller pr�s du patient
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Boucle d'assistance
            Toil assistToil = new Toil();
            assistToil.initAction = () =>
            {
                Log.Message($"{pawn.Name} a commenc� le Job AssistSurgeryLoop pour {job.targetA.Thing.Label}");
                Log.Message($"Bill associ� au job de l'assistant : {job.bill?.recipe?.defName ?? "Aucun"}");
            };
            assistToil.tickAction = () =>
            {
                // Optionnel : Ajouter des actions d'assistance ici
                // Par exemple, jouer une animation, interagir avec le patient, etc.

                // V�rifier si la chirurgie est toujours en cours
                if (IsSurgeryOngoing())
                {
                    Log.Message($"{pawn.Name} confirme que la chirurgie est en cours pour {job.targetA.Thing.Label}");
                }
                else
                {
                    Log.Message($"{pawn.Name} ne d�tecte pas la chirurgie en cours pour {job.targetA.Thing.Label}. Attente...");
                    // Ne pas terminer le job, simplement attendre que la chirurgie commence
                }
            };
            assistToil.defaultCompleteMode = ToilCompleteMode.Never; // Le job ne se termine jamais automatiquement
            yield return assistToil;
        }

        private bool IsSurgeryOngoing()
        {
            // V�rifier si le patient est toujours en cours d'op�ration
            Pawn patient = (Pawn)job.targetA.Thing;
            var medicalBill = this.job.bill as BillMedicalEx;
            bool ongoing = medicalBill != null && medicalBill.SurgeryStarted;
            Log.Message($"{pawn.Name} v�rifie si la chirurgie est en cours pour {patient.Label} : {ongoing}");
            return ongoing;
        }
    }
}

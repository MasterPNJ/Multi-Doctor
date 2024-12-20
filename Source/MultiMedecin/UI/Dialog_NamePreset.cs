using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_NamePreset : Window
    {
        private Action<string> onConfirmAction;
        private string presetName = "";

        public override Vector2 InitialSize => new Vector2(300f, 150f);

        public Dialog_NamePreset(Action<string> onConfirmAction)
        {
            this.onConfirmAction = onConfirmAction;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Titre
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Enter Preset Name:");
            Text.Font = GameFont.Small;

            // Champ de texte pour le nom du preset
            presetName = Widgets.TextField(new Rect(0, 40f, inRect.width - 16f, 30f), presetName);

            // Boutons
            if (Widgets.ButtonText(new Rect(0, 90f, inRect.width / 2f - 10f, 35f), "Confirm"))
            {
                ConfirmPreset();
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 10f, 90f, inRect.width / 2f - 10f, 35f), "Cancel"))
            {
                Close();
            }

            // Gérer la touche Entrée pour confirmer
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                ConfirmPreset();
                Event.current.Use(); // Empêcher l'action par défaut de RimWorld
            }
        }

        // Méthode pour confirmer la création du preset
        private void ConfirmPreset()
        {
            if (!string.IsNullOrEmpty(presetName.Trim()))
            {
                onConfirmAction?.Invoke(presetName.Trim());
                Close();
            }
            else
            {
                Messages.Message("Preset name cannot be empty.", MessageTypeDefOf.RejectInput, false);
            }
        }

    }
}

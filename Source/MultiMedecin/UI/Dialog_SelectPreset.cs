using RimWorld;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace MultiDoctorSurgery.UI
{
    public class Dialog_SelectPreset : Window
    {
        private Action<string> onConfirmAction;
        private Vector2 scrollPosition = Vector2.zero;
        private string[] availablePresets;

        public override Vector2 InitialSize => new Vector2(400f, 300f);

        public Dialog_SelectPreset(Action<string> onConfirmAction)
        {
            this.onConfirmAction = onConfirmAction;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;

            // Récupérer les presets disponibles
            string configPath = GenFilePaths.ConfigFolderPath;
            availablePresets = Directory.GetFiles(configPath, "ExcludedOperations_*.xml")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(name => name.Replace("ExcludedOperations_", ""))
                .ToArray();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Titre
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Select Preset");
            Text.Font = GameFont.Small;

            // Liste des presets
            float contentHeight = availablePresets.Length * 30f;
            Rect scrollOutRect = new Rect(0, 40f, inRect.width, inRect.height - 50f);
            Rect scrollViewRect = new Rect(0, 0, inRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect);
            float curY = 0f;

            for (int i = 0; i < availablePresets.Length; i++)
            {
                string preset = availablePresets[i];

                // Zone de texte pour le preset
                Rect rowRect = new Rect(0, curY, scrollViewRect.width - 30f, 30f);
                if (Widgets.ButtonText(rowRect, preset))
                {
                    onConfirmAction?.Invoke(preset);
                    Close();
                }

                // Zone de suppression (croix)
                Rect deleteButtonRect = new Rect(scrollViewRect.width - 25f, curY + 5f, 20f, 20f);
                if (Widgets.ButtonImage(deleteButtonRect, TexButton.CloseXSmall)) // Icône standard de croix de RimWorld
                {
                    // Supprimer le fichier de preset
                    string configPath = Path.Combine(GenFilePaths.ConfigFolderPath, $"ExcludedOperations_{preset}.xml");
                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                        Messages.Message($"Preset {preset} deleted.", MessageTypeDefOf.NegativeEvent, false);
                        // Mettre à jour la liste des presets
                        availablePresets = availablePresets.Where(p => p != preset).ToArray();
                    }
                }

                curY += 30f;
            }

            Widgets.EndScrollView();
        }
    }
}

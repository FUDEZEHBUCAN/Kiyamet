using System.Collections.Generic;
using _Root.Scripts.Finale;
using UnityEngine;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Final sinematiği gibi tam ekran sekanslarda gameplay UI canvas'larını gizler.
    /// </summary>
    public static class GameplayUiVisibility
    {
        private static readonly List<Canvas> SuppressedCanvases = new();

        public static bool IsSuppressedForFinale { get; private set; }

        public static void SuppressForFinaleCinematic()
        {
            if (IsSuppressedForFinale)
                return;

            CloseOpenMenus();

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.enabled || ShouldKeepCanvasVisible(canvas))
                    continue;

                canvas.enabled = false;
                SuppressedCanvases.Add(canvas);
            }

            IsSuppressedForFinale = true;
        }

        private static void CloseOpenMenus()
        {
            if (GameplayPauseMenu.Instance != null)
                GameplayPauseMenu.Instance.ForceClose();

            if (UIElementController.LocalInstance != null)
                UIElementController.LocalInstance.Close();
        }

        private static bool ShouldKeepCanvasVisible(Canvas canvas)
        {
            return canvas.GetComponent<FinaleScreenFadeOverlay>() != null
                || canvas.GetComponentInParent<FinaleScreenFadeOverlay>(true) != null
                || canvas.GetComponent<FinaleCreditsOverlay>() != null
                || canvas.GetComponentInParent<FinaleCreditsOverlay>(true) != null;
        }
    }
}

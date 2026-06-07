using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Tam ekran hafif kırmızı hasar flash'ı. Local oyuncunun HUD vignette Image'inde çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class PlayerDamageFlashOverlay : MonoBehaviour
    {
        [SerializeField] private Image overlayImage;
        [SerializeField] private Color flashColor = new Color(0.82f, 0.04f, 0.03f, 1f);
        [SerializeField] private float peakAlpha = 0.3f;
        [SerializeField] private float fadeInDuration = 0.07f;
        [SerializeField] private float fadeOutDuration = 0.24f;

        private Sequence _flashSequence;

        private void Awake()
        {
            if (overlayImage == null)
                overlayImage = GetComponent<Image>();

            ResetOverlay();
        }

        private void OnDisable()
        {
            _flashSequence?.Kill();
            ResetOverlay();
        }

        public static void PlayForLocalPlayer()
        {
            if (NetworkPlayer.Local == null)
                return;

            var overlay = NetworkPlayer.Local.GetComponentInChildren<PlayerDamageFlashOverlay>(true);
            overlay?.PlayFlash();
        }

        public void PlayFlash()
        {
            if (overlayImage == null)
                return;

            _flashSequence?.Kill();

            Color color = flashColor;
            color.a = 0f;
            overlayImage.color = color;
            overlayImage.enabled = true;
            overlayImage.raycastTarget = false;

            _flashSequence = DOTween.Sequence()
                .Append(DOTween.To(
                    () => overlayImage.color.a,
                    alpha =>
                    {
                        color.a = alpha;
                        overlayImage.color = color;
                    },
                    peakAlpha,
                    fadeInDuration).SetEase(Ease.OutQuad))
                .Append(DOTween.To(
                    () => overlayImage.color.a,
                    alpha =>
                    {
                        color.a = alpha;
                        overlayImage.color = color;
                    },
                    0f,
                    fadeOutDuration).SetEase(Ease.InQuad))
                .OnComplete(ResetOverlay);
        }

        private void ResetOverlay()
        {
            if (overlayImage == null)
                return;

            Color color = flashColor;
            color.a = 0f;
            overlayImage.color = color;
        }
    }
}

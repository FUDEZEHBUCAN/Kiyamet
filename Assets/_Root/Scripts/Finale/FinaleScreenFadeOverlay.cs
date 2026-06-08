using DG.Tweening;
using _Root.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Root.Scripts.Finale
{
    /// <summary>
    /// Final sekansında ekranı karartır, evil laugh sesini çalar ve fade sonrası end card gösterir.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(5000)]
    public class FinaleScreenFadeOverlay : MonoBehaviour
    {
        private const int SortOrder = 40000;

        private static FinaleScreenFadeOverlay _instance;

        private Canvas _canvas;
        private Image _overlay;
        private TextMeshProUGUI _continuedText;
        private AudioSource _audioSource;
        private bool _isFading;
        private bool _fadeComplete;
        private float _fadeStartTime;
        private float _fadeDuration;
        private Sequence _continuedSequence;

        public static void PlayFade(
            AudioClip[] laughClips,
            float duration,
            float volume,
            string continuedText,
            float continuedDelayAfterFade,
            float continuedAnimDuration,
            float continuedFontSize)
        {
            EnsureInstance();
            _instance.BeginFade(
                laughClips,
                duration,
                volume,
                continuedText,
                continuedDelayAfterFade,
                continuedAnimDuration,
                continuedFontSize);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(FinaleScreenFadeOverlay));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FinaleScreenFadeOverlay>();
            _instance.BuildUi();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
        }

        private void OnDestroy()
        {
            _continuedSequence?.Kill();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_isFading || _overlay == null)
                return;

            float t = _fadeDuration > 0.001f
                ? Mathf.Clamp01((Time.unscaledTime - _fadeStartTime) / _fadeDuration)
                : 1f;

            _overlay.color = new Color(0f, 0f, 0f, t);

            if (_fadeComplete || t < 1f)
                return;

            _fadeComplete = true;
            _isFading = false;
        }

        private void BeginFade(
            AudioClip[] laughClips,
            float duration,
            float volume,
            string continuedText,
            float continuedDelayAfterFade,
            float continuedAnimDuration,
            float continuedFontSize)
        {
            _continuedSequence?.Kill();
            ResetContinuedText();

            _fadeDuration = Mathf.Max(0.05f, duration);
            _fadeStartTime = Time.unscaledTime;
            _isFading = true;
            _fadeComplete = false;

            if (_overlay != null)
                _overlay.color = new Color(0f, 0f, 0f, 0f);

            PlayLaughClips(laughClips, volume);
            PlayContinuedTextAnimation(
                continuedText,
                continuedDelayAfterFade,
                continuedAnimDuration,
                continuedFontSize,
                _fadeDuration);
        }

        private void PlayLaughClips(AudioClip[] laughClips, float volume)
        {
            if (_audioSource == null || laughClips == null || laughClips.Length == 0)
                return;

            float clampedVolume = Mathf.Clamp01(volume);
            for (int i = 0; i < laughClips.Length; i++)
            {
                AudioClip clip = laughClips[i];
                if (clip != null)
                    _audioSource.PlayOneShot(clip, clampedVolume);
            }
        }

        private void PlayContinuedTextAnimation(
            string text,
            float delayAfterFade,
            float animDuration,
            float fontSize,
            float fadeDuration)
        {
            EnsureContinuedText(text, fontSize);

            RectTransform rect = _continuedText.rectTransform;
            Color baseColor = _continuedText.color;
            baseColor.a = 0f;
            _continuedText.color = baseColor;
            rect.localScale = Vector3.one * 0.82f;
            _continuedText.gameObject.SetActive(true);

            float delay = Mathf.Max(0f, delayAfterFade) + fadeDuration;
            float duration = Mathf.Max(0.2f, animDuration);

            _continuedSequence = DOTween.Sequence().SetUpdate(true);
            _continuedSequence.AppendInterval(delay);
            _continuedSequence.Append(_continuedText.DOFade(1f, duration * 0.55f).SetEase(Ease.OutCubic));
            _continuedSequence.Join(rect.DOScale(1f, duration).SetEase(Ease.OutBack, 1.12f));
            _continuedSequence.Append(rect.DOScale(1.04f, 0.45f).SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo));
        }

        private void ResetContinuedText()
        {
            if (_continuedText == null)
                return;

            _continuedText.DOKill();
            _continuedText.rectTransform.DOKill();
            _continuedText.gameObject.SetActive(false);
        }

        private void EnsureContinuedText(string text, float fontSize)
        {
            if (_continuedText == null)
                BuildContinuedText();

            _continuedText.text = string.IsNullOrWhiteSpace(text) ? "To Be Continued..." : text;
            _continuedText.fontSize = Mathf.Max(24f, fontSize);
        }

        private void BuildUi()
        {
            if (_canvas != null)
                return;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayGo.transform.SetParent(transform, false);

            var rect = overlayGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _overlay = overlayGo.GetComponent<Image>();
            _overlay.raycastTarget = false;
            _overlay.color = new Color(0f, 0f, 0f, 0f);

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            BuildContinuedText();
        }

        private void BuildContinuedText()
        {
            var textGo = new GameObject("ToBeContinuedText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, 220f);
            rect.anchoredPosition = Vector2.zero;

            _continuedText = textGo.GetComponent<TextMeshProUGUI>();
            _continuedText.alignment = TextAlignmentOptions.Center;
            _continuedText.fontStyle = FontStyles.Bold;
            _continuedText.color = new Color(0.96f, 0.96f, 0.98f, 0f);
            _continuedText.raycastTarget = false;
            _continuedText.enableWordWrapping = false;

            GameplayUiFonts.ApplyTo(_continuedText);

            textGo.SetActive(false);
        }
    }
}

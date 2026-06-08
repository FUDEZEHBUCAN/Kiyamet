using System;
using DG.Tweening;
using _Root.Scripts.Network;
using _Root.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Root.Scripts.Finale
{
    /// <summary>
    /// Final sekansı sonrası basit kayan jenerik metni gösterir. Space / Enter / Escape ile atlanabilir.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(5001)]
    public class FinaleCreditsOverlay : MonoBehaviour
    {
        private const int SortOrder = 40001;
        private const int MainMenuSceneBuildIndex = 0;
        private const float TitleFontSize = 132f;
        private const float SectionFontSize = 72f;
        private const float BodyFontSize = 58f;
        private const float LineSpacing = 28f;
        private const float TextBoxWidth = 1500f;
        private const float TextBoxHeight = 9000f;

        private static FinaleCreditsOverlay _instance;
        private static bool _returnToMainMenuInProgress;

        private Canvas _canvas;
        private RectTransform _viewport;
        private TextMeshProUGUI _creditsText;
        private TextMeshProUGUI _skipHintText;
        private AudioSource _musicSource;
        private Tween _scrollTween;
        private Sequence _introSequence;
        private Tween _musicFadeTween;
        private AudioClip _musicClip;
        private float _musicVolume = 0.45f;
        private float _musicFadeInDuration = 2f;
        private float _musicFadeOutDuration = 1.5f;
        private bool _isPlaying;
        private bool _isExiting;
        private Action _onComplete;

        public static bool IsPlaying => _instance != null && _instance._isPlaying;

        public static void Play(
            string[] lines,
            float scrollSpeedPixelsPerSecond,
            Action onComplete = null,
            AudioClip musicClip = null,
            float musicVolume = 0.45f,
            float musicFadeInDuration = 2f,
            float musicFadeOutDuration = 1.5f)
        {
            EnsureInstance();
            _instance.Begin(
                lines,
                scrollSpeedPixelsPerSecond,
                onComplete,
                musicClip,
                musicVolume,
                musicFadeInDuration,
                musicFadeOutDuration);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(FinaleCreditsOverlay));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FinaleCreditsOverlay>();
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
            KillTweens();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_isPlaying)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)
                || UnityEngine.Input.GetKeyDown(KeyCode.Return)
                || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Finish();
            }
        }

        private void Begin(
            string[] lines,
            float scrollSpeedPixelsPerSecond,
            Action onComplete,
            AudioClip musicClip,
            float musicVolume,
            float musicFadeInDuration,
            float musicFadeOutDuration)
        {
            KillTweens();
            _isExiting = false;
            _onComplete = onComplete;
            _isPlaying = true;
            _musicClip = musicClip;
            _musicVolume = Mathf.Clamp01(musicVolume);
            _musicFadeInDuration = Mathf.Max(0.05f, musicFadeInDuration);
            _musicFadeOutDuration = Mathf.Max(0.05f, musicFadeOutDuration);

            var textRect = _creditsText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 1f);

            string body = BuildCreditsBody(lines);
            _creditsText.gameObject.SetActive(true);
            _creditsText.text = body;
            _creditsText.ForceMeshUpdate();

            textRect.sizeDelta = new Vector2(TextBoxWidth, Mathf.Max(_creditsText.preferredHeight + 80f, 256f));
            _creditsText.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            float viewportHeight = _viewport.rect.height;
            if (viewportHeight <= 1f)
                viewportHeight = Screen.height;

            float padding = viewportHeight * 0.1f;
            float viewportCenterY = viewportHeight * 0.5f;
            float titleCenterLocalY = GetTitleLineCenterLocalY(_creditsText);
            float startY = viewportCenterY - titleCenterLocalY;
            float endY = viewportHeight + padding - _creditsText.textBounds.min.y;
            if (endY <= startY + 1f)
                endY = startY + viewportHeight + contentScrollDistance(_creditsText, viewportHeight);

            float distance = endY - startY;
            float speed = Mathf.Max(18f, scrollSpeedPixelsPerSecond);
            float duration = Mathf.Clamp(distance / speed, 14f, 120f);

            textRect.anchoredPosition = new Vector2(0f, startY);

            Color textColor = _creditsText.color;
            textColor.a = 0f;
            _creditsText.color = textColor;

            Color hintColor = _skipHintText.color;
            hintColor.a = 0f;
            _skipHintText.color = hintColor;

            _canvas.gameObject.SetActive(true);
            _skipHintText.gameObject.SetActive(true);

            StartMusicFadeIn();

            _introSequence = DOTween.Sequence().SetUpdate(true);
            _introSequence.Append(_creditsText.DOFade(1f, 0.65f));
            _introSequence.Join(_skipHintText.DOFade(0.55f, 0.65f));
            _introSequence.OnComplete(() =>
            {
                _scrollTween = textRect
                    .DOAnchorPosY(endY, duration)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .OnComplete(Finish);
            });
        }

        private void Finish()
        {
            if (!_isPlaying || _isExiting)
                return;

            _isExiting = true;
            _isPlaying = false;
            KillScrollTweens();

            if (_creditsText != null)
                _creditsText.gameObject.SetActive(false);
            if (_skipHintText != null)
                _skipHintText.gameObject.SetActive(false);

            _onComplete?.Invoke();
            _onComplete = null;
            FadeOutMusicAndReturnToMainMenu();
        }

        private void StartMusicFadeIn()
        {
            if (_musicClip == null || _musicSource == null)
                return;

            _musicFadeTween?.Kill();
            _musicSource.clip = _musicClip;
            _musicSource.volume = 0f;
            _musicSource.loop = true;
            _musicSource.Play();

            _musicFadeTween = _musicSource
                .DOFade(_musicVolume, _musicFadeInDuration)
                .SetEase(Ease.InSine)
                .SetUpdate(true);
        }

        private void FadeOutMusicAndReturnToMainMenu()
        {
            if (_musicClip == null || _musicSource == null || !_musicSource.isPlaying)
            {
                ReturnToMainMenuAfterCredits();
                return;
            }

            _musicFadeTween?.Kill();
            _musicFadeTween = _musicSource
                .DOFade(0f, _musicFadeOutDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _musicSource.Stop();
                    ReturnToMainMenuAfterCredits();
                });
        }

        private async void ReturnToMainMenuAfterCredits()
        {
            if (_returnToMainMenuInProgress)
                return;

            _returnToMainMenuInProgress = true;

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var handler = UnityEngine.Object.FindObjectOfType<NetworkRunnerHandler>();
            if (handler != null)
            {
                try
                {
                    await handler.ShutdownSessionAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FinaleCreditsOverlay] Network shutdown failed: {ex.Message}");
                }
            }

            FinaleScreenFadeOverlay.DestroyInstance();

            if (_instance != null)
            {
                var creditsGo = _instance.gameObject;
                _instance = null;
                UnityEngine.Object.Destroy(creditsGo);
            }

            SceneManager.LoadScene(MainMenuSceneBuildIndex);
        }

        private void KillTweens()
        {
            KillScrollTweens();
            _musicFadeTween?.Kill();
            _musicFadeTween = null;

            if (_musicSource != null)
                _musicSource.DOKill();
        }

        private void KillScrollTweens()
        {
            _scrollTween?.Kill();
            _scrollTween = null;
            _introSequence?.Kill();
            _introSequence = null;

            if (_creditsText != null)
            {
                _creditsText.DOKill();
                _creditsText.rectTransform.DOKill();
            }

            if (_skipHintText != null)
                _skipHintText.DOKill();
        }

        private static string BuildCreditsBody(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                lines = GetDefaultLines();

            var builder = new System.Text.StringBuilder(lines.Length * 24);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine();
                    continue;
                }

                if (i == 0)
                {
                    builder.AppendLine($"<size={TitleFontSize:0}><b>{line.Trim()}</b></size>");
                    continue;
                }

                if (line.StartsWith("##", StringComparison.Ordinal))
                    builder.AppendLine($"<size={SectionFontSize + 8f:0}><b>{line.Substring(2).Trim()}</b></size>");
                else if (line.StartsWith("#", StringComparison.Ordinal))
                    builder.AppendLine($"<size={SectionFontSize:0}><b>{line.Substring(1).Trim()}</b></size>");
                else
                    builder.AppendLine($"<size={BodyFontSize:0}>{line.Trim()}</size>");
            }

            return builder.ToString().TrimEnd();
        }

        private static float GetTitleLineCenterLocalY(TextMeshProUGUI text)
        {
            if (text == null)
                return 0f;

            var lineInfo = text.textInfo;
            if (lineInfo.lineCount > 0)
            {
                var firstLine = lineInfo.lineInfo[0];
                return (firstLine.ascender + firstLine.descender) * 0.5f;
            }

            return text.textBounds.center.y;
        }

        private static float contentScrollDistance(TextMeshProUGUI text, float viewportHeight)
        {
            if (text == null)
                return viewportHeight;

            return Mathf.Max(text.preferredHeight, text.textBounds.size.y) + viewportHeight * 0.35f;
        }

        private static string[] GetDefaultLines() =>
            new[]
            {
                "KIYAMET",
                "",
                "A School Project",
                "",
                "#Development",
                "Team Member",
                "",
                "#Level / World Design",
                "Team Member",
                "",
                "#Art & Design",
                "Team Member",
                "",
                "#UI Design",
                "Team Member",
                "",
                "#Music & Sound",
                "Team Member",
                "",
                "#Special Thanks",
                "To our instructors and everyone who playtested",
                "",
                "Thank you for playing!"
            };

        private void BuildUi()
        {
            if (_canvas != null)
                return;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            var viewportGo = new GameObject("CreditsViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(transform, false);
            _viewport = viewportGo.GetComponent<RectTransform>();
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = Vector2.zero;
            _viewport.offsetMax = Vector2.zero;

            var textGo = new GameObject("CreditsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(_viewport, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(TextBoxWidth, TextBoxHeight);

            _creditsText = textGo.GetComponent<TextMeshProUGUI>();
            _creditsText.alignment = TextAlignmentOptions.Center;
            _creditsText.fontSize = BodyFontSize;
            _creditsText.lineSpacing = LineSpacing;
            _creditsText.color = new Color(0.94f, 0.94f, 0.96f, 1f);
            _creditsText.raycastTarget = false;
            _creditsText.richText = true;
            _creditsText.enableWordWrapping = true;
            _creditsText.overflowMode = TextOverflowModes.Overflow;
            _creditsText.verticalAlignment = VerticalAlignmentOptions.Top;
            GameplayUiFonts.ApplyTo(_creditsText);

            var hintGo = new GameObject("SkipHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(transform, false);

            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(640f, 48f);
            hintRect.anchoredPosition = new Vector2(0f, 28f);

            _skipHintText = hintGo.GetComponent<TextMeshProUGUI>();
            _skipHintText.text = "Skip  ·  Space / Enter / Esc";
            _skipHintText.alignment = TextAlignmentOptions.Center;
            _skipHintText.fontSize = 22f;
            _skipHintText.color = new Color(0.82f, 0.82f, 0.86f, 0.55f);
            _skipHintText.raycastTarget = false;
            GameplayUiFonts.ApplyTo(_skipHintText);

            hintGo.SetActive(false);
            _canvas.gameObject.SetActive(false);

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
        }
    }
}

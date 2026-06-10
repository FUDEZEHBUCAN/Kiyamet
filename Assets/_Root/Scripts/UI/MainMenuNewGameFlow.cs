using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// New Game: tam ekran intro videosu oynatır, bitince lobby sahnesini yükler.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuNewGameFlow : MonoBehaviour
    {
        private const int DefaultLobbySceneBuildIndex = 1;
        private const float IntroPrepareTimeoutSeconds = 8f;

        [Header("UI")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject[] hideOnIntro;
        [SerializeField] private GameObject backgroundLoopRoot;

        [Header("Background Loop")]
        [SerializeField] private VideoPlayer backgroundLoopPlayer;

        [Header("Background Audio")]
        [SerializeField] private AudioSource[] backgroundAudioSources;

        [Header("Intro")]
        [SerializeField] private VideoClip introVideoClip;
        [SerializeField] private int lobbySceneBuildIndex = DefaultLobbySceneBuildIndex;
        [SerializeField] private Vector2Int introRenderSize = new Vector2Int(1920, 1080);

        private RawImage _introRawImage;
        private VideoPlayer _introVideoPlayer;
        private RenderTexture _introRenderTexture;
        private Coroutine _introRoutine;
        private bool _introPlaying;

        private void Awake()
        {
            ResolveReferencesIfMissing();
            BuildIntroOverlay();
            SetIntroOverlayVisible(false);
        }

        private void OnDestroy()
        {
            if (_introVideoPlayer != null)
            {
                _introVideoPlayer.loopPointReached -= OnIntroVideoFinished;
                _introVideoPlayer.errorReceived -= OnIntroVideoError;
            }

            ReleaseIntroRenderTexture();
        }

        private void Update()
        {
            // Adding "UnityEngine." tells the game explicitly to use Unity's input, 
            // bypassing your project's custom Input namespace.
            if (_introPlaying && (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Space)))
            {
                LoadLobbyScene();
            }
        }

        public void BeginNewGame()
        {
            if (_introPlaying)
                return;

            ResolveReferencesIfMissing();

            if (introVideoClip == null)
            {
                Debug.LogWarning("[MainMenuNewGameFlow] Intro video clip atanmamış, lobby yükleniyor.");
                LoadLobbyScene();
                return;
            }

            _introPlaying = true;
            SetMenuVisible(false);
            StopBackgroundAudio();
            SetBackgroundLoopVisible(false);
            SetIntroOverlayVisible(true);

            if (_introRoutine != null)
                StopCoroutine(_introRoutine);

            _introRoutine = StartCoroutine(PlayIntroRoutine());
        }

        private IEnumerator PlayIntroRoutine()
        {
            if (_introVideoPlayer == null || _introRenderTexture == null)
            {
                LoadLobbyScene();
                yield break;
            }

            _introVideoPlayer.Stop();
            _introVideoPlayer.clip = introVideoClip;
            _introVideoPlayer.Prepare();

            float elapsed = 0f;
            while (!_introVideoPlayer.isPrepared && elapsed < IntroPrepareTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_introVideoPlayer.isPrepared)
            {
                Debug.LogError("[MainMenuNewGameFlow] Intro video hazırlanamadı, lobby yükleniyor.");
                LoadLobbyScene();
                yield break;
            }

            _introVideoPlayer.Play();
        }

        private void ResolveReferencesIfMissing()
        {
            if (menuRoot == null)
            {
                Transform menu = transform.Find("Mein Menu");
                if (menu != null)
                    menuRoot = menu.gameObject;
            }

            if (backgroundLoopRoot == null)
            {
                Transform loop = transform.Find("Loop");
                if (loop != null)
                    backgroundLoopRoot = loop.gameObject;
            }

            if (backgroundLoopPlayer == null)
            {
                Transform loopPlayer = transform.Find("Loop/VideoPlayer");
                if (loopPlayer != null)
                    backgroundLoopPlayer = loopPlayer.GetComponent<VideoPlayer>();
            }

            ResolveBackgroundAudioIfMissing();
        }

        private void ResolveBackgroundAudioIfMissing()
        {
            if (backgroundAudioSources != null && backgroundAudioSources.Length > 0)
                return;

            var bgAudioRoot = GameObject.Find("BG Audio");
            if (bgAudioRoot == null)
                return;

            backgroundAudioSources = bgAudioRoot.GetComponents<AudioSource>();
        }

        private void StopBackgroundAudio()
        {
            ResolveBackgroundAudioIfMissing();

            if (backgroundAudioSources == null)
                return;

            for (int i = 0; i < backgroundAudioSources.Length; i++)
            {
                if (backgroundAudioSources[i] == null)
                    continue;

                backgroundAudioSources[i].Stop();
            }
        }

        private void BuildIntroOverlay()
        {
            if (_introRawImage != null)
                return;

            var overlayGo = new GameObject("IntroVideoOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlayGo.transform.SetParent(transform, false);
            overlayGo.transform.SetAsLastSibling();

            var overlayRect = overlayGo.GetComponent<RectTransform>();
            StretchFullScreen(overlayRect);

            _introRawImage = overlayGo.GetComponent<RawImage>();
            _introRawImage.color = Color.white;
            _introRawImage.raycastTarget = false;

            _introRenderTexture = new RenderTexture(introRenderSize.x, introRenderSize.y, 0, RenderTextureFormat.ARGB32);
            _introRenderTexture.Create();
            _introRawImage.texture = _introRenderTexture;

            var playerGo = new GameObject("IntroVideoPlayer");
            playerGo.transform.SetParent(overlayGo.transform, false);

            _introVideoPlayer = playerGo.AddComponent<VideoPlayer>();
            _introVideoPlayer.playOnAwake = false;
            _introVideoPlayer.isLooping = false;
            _introVideoPlayer.waitForFirstFrame = true;
            _introVideoPlayer.skipOnDrop = true;
            _introVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _introVideoPlayer.targetTexture = _introRenderTexture;
            _introVideoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            _introVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _introVideoPlayer.loopPointReached += OnIntroVideoFinished;
            _introVideoPlayer.errorReceived += OnIntroVideoError;
        }

        private void OnIntroVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[MainMenuNewGameFlow] Intro video hatası: {message}");
            if (_introPlaying)
                LoadLobbyScene();
        }

        private void OnIntroVideoFinished(VideoPlayer source)
        {
            if (!_introPlaying)
                return;

            LoadLobbyScene();
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuRoot != null)
                menuRoot.SetActive(visible);

            if (hideOnIntro == null)
                return;

            for (int i = 0; i < hideOnIntro.Length; i++)
            {
                if (hideOnIntro[i] != null)
                    hideOnIntro[i].SetActive(visible);
            }
        }

        private void SetBackgroundLoopVisible(bool visible)
        {
            if (backgroundLoopRoot != null)
            {
                backgroundLoopRoot.SetActive(visible);
                return;
            }

            if (backgroundLoopPlayer == null)
                return;

            if (visible)
                backgroundLoopPlayer.Play();
            else
                backgroundLoopPlayer.Stop();
        }

        private void SetIntroOverlayVisible(bool visible)
        {
            if (_introRawImage != null)
                _introRawImage.gameObject.SetActive(visible);
        }

        private void LoadLobbyScene()
        {
            _introPlaying = false;

            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }

            ReleaseIntroRenderTexture();
            SceneManager.LoadScene(lobbySceneBuildIndex);
        }

        private void ReleaseIntroRenderTexture()
        {
            if (_introRenderTexture == null)
                return;

            if (_introVideoPlayer != null)
                _introVideoPlayer.targetTexture = null;

            if (_introRawImage != null)
                _introRawImage.texture = null;

            _introRenderTexture.Release();
            Destroy(_introRenderTexture);
            _introRenderTexture = null;
        }

        private static void StretchFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}

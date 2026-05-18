using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Linq;
using _Root.Scripts.Network.Lobby;
using _Root.Scripts.UI;

namespace _Root.Scripts.Network
{
    public class NetworkRunnerHandler : MonoBehaviour
    {
        public NetworkRunner networkRunnerPrefab;

        [Tooltip("Kapalıysa PlaytestLobbyController bağlanana kadar bekler.")]
        [SerializeField] private bool autoConnectOnStart = true;

        [SerializeField] private string autoConnectSessionName = "TestSession";

        private NetworkRunner _networkRunner;

        public NetworkRunner Runner => _networkRunner;

        private void Start()
        {
            if (GetComponent<PlaytestLobbyController>() != null)
                return;

            if (!autoConnectOnStart)
                return;

            _ = StartLegacySessionAsync(autoConnectSessionName, connectionToken: null, extraCallbacks: null);
        }

        /// <summary>Playtest lobisi: rol token'ı ile AutoHostOrClient bağlantısı.</summary>
        public async Task<NetworkRunner> StartPlaytestSessionAsync(
            string sessionName,
            byte[] connectionToken,
            INetworkRunnerCallbacks extraCallbacks)
        {
            return await StartLegacySessionAsync(sessionName, connectionToken, extraCallbacks);
        }

        private async Task<NetworkRunner> StartLegacySessionAsync(
            string sessionName,
            byte[] connectionToken,
            INetworkRunnerCallbacks extraCallbacks)
        {
            if (_networkRunner != null)
                return _networkRunner;

            _networkRunner = Instantiate(networkRunnerPrefab);
            _networkRunner.name = "Network Runner";

            if (gameObject.GetComponent<NetworkDebugUI>() == null)
                gameObject.AddComponent<NetworkDebugUI>();

            if (gameObject.GetComponent<GameplayPingDisplay>() == null)
                gameObject.AddComponent<GameplayPingDisplay>();

            if (gameObject.GetComponent<GameplayPauseMenu>() == null)
                gameObject.AddComponent<GameplayPauseMenu>();

            var spawner = GetComponent<Spawner>() ?? FindObjectOfType<Spawner>();
            if (spawner != null)
                _networkRunner.AddCallbacks(spawner);
            else
                Debug.LogError("Spawner bulunamadı! OnInput callback'leri çalışmayacak!");

            if (extraCallbacks != null)
                _networkRunner.AddCallbacks(extraCallbacks);

            var ok = await InitializeNetworkRunnerAsync(
                _networkRunner,
                GameMode.AutoHostOrClient,
                NetAddress.Any(),
                SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                sessionName,
                connectionToken);

            return ok ? _networkRunner : null;
        }
        
        protected virtual async Task<bool> InitializeNetworkRunnerAsync(
            NetworkRunner runner,
            GameMode gameMode,
            NetAddress address,
            SceneRef scene,
            string sessionName,
            byte[] connectionToken)
        {
            try
            {
                var sceneManager = runner.GetComponents(typeof(MonoBehaviour))
                    .OfType<INetworkSceneManager>()
                    .FirstOrDefault();

                if (sceneManager == null)
                    sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

                runner.ProvideInput = true;

                var startGameArgs = new StartGameArgs
                {
                    GameMode = gameMode,
                    Address = address,
                    Scene = scene,
                    SceneManager = sceneManager
                };

                if (!string.IsNullOrEmpty(sessionName))
                    startGameArgs.SessionName = sessionName;

                if (connectionToken != null && connectionToken.Length > 0)
                    startGameArgs.ConnectionToken = connectionToken;

                Debug.Log($"[NetworkRunnerHandler] Starting network runner with GameMode: {gameMode}, SessionName: {sessionName}");
                var result = await runner.StartGame(startGameArgs);

                if (!result.Ok)
                {
                    Debug.LogError($"[NetworkRunnerHandler] StartGame failed: {result.ShutdownReason}");
                    if (runner != null)
                    {
                        await runner.Shutdown();
                        Destroy(runner.gameObject);
                    }

                    _networkRunner = null;
                    return false;
                }

                Debug.Log($"[NetworkRunnerHandler] Network runner started successfully. IsServer: {runner.IsServer}, IsClient: {runner.IsClient}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkRunnerHandler] Exception during network initialization: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public async Task ShutdownSessionAsync()
        {
            if (_networkRunner == null)
                return;

            var runner = _networkRunner;
            _networkRunner = null;

            try
            {
                await runner.Shutdown();
            }
            finally
            {
                if (runner != null)
                    Destroy(runner.gameObject);
            }
        }
    }
}

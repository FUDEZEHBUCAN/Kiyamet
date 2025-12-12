using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace _Root.Scripts.Network
{
    public class NetworkRunnerHandler : MonoBehaviour
    {
        public NetworkRunner networkRunnerPrefab; 
        
        private NetworkRunner _networkRunner;
        
        // Start is called before the first frame update
        void Start()
        {
            _networkRunner = Instantiate(networkRunnerPrefab);
            _networkRunner.name = "Network Runner";
            
            // Debug UI ekle (runtime'da network durumunu gösterir)
            if (gameObject.GetComponent<NetworkDebugUI>() == null)
            {
                gameObject.AddComponent<NetworkDebugUI>();
            }
            
            // Spawner'ı bul ve callback olarak ekle
            var spawner = FindObjectOfType<Spawner>();
            if (spawner != null)
            {
                _networkRunner.AddCallbacks(spawner);
            }
            else
            {
                Debug.LogError("Spawner bulunamadı! OnInput callback'leri çalışmayacak!");
            }
            
            // Aynı session'ı paylaşmak için session name belirle
            var sessionName = "TestSession";
            var clientTask = InitializeNetworkRunner(_networkRunner, GameMode.AutoHostOrClient, NetAddress.Any(),
                SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), sessionName);
        }
        
        protected virtual async Task InitializeNetworkRunner(NetworkRunner runner, GameMode gameMode,
            NetAddress address, SceneRef scene, string sessionName)
        {
            var sceneManager = runner.GetComponents(typeof(MonoBehaviour))
                .OfType<INetworkSceneManager>()
                .FirstOrDefault();

            if (sceneManager == null) 
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

            runner.ProvideInput = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = gameMode,
                Address = address, 
                Scene = scene, 
                SceneManager = sceneManager
            };

            // Session name belirle (tüm client'lar aynı session'a bağlanmalı)
            if (!string.IsNullOrEmpty(sessionName))
            {
                startGameArgs.SessionName = sessionName;
            }

            var result = await runner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                Debug.LogError($"StartGame failed: {result.ShutdownReason}");
            }
        }
    }
}

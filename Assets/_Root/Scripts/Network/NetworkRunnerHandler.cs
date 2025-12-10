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
            
            // Spawner'ı bul ve callback olarak ekle
            var spawner = FindObjectOfType<Spawner>();
            if (spawner != null)
            {
                _networkRunner.AddCallbacks(spawner);
                Debug.Log("Spawner bulundu ve NetworkRunner'a callback olarak eklendi.");
            }
            else
            {
                Debug.LogError("Spawner bulunamadı! OnInput callback'leri çalışmayacak!");
            }
            
            var clientTask = InitializeNetworkRunner(_networkRunner, GameMode.AutoHostOrClient, NetAddress.Any(),
                SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), null);
            Debug.Log("Network Runner başlatılıyor...");
        }
        
        protected virtual async Task InitializeNetworkRunner(NetworkRunner runner, GameMode gameMode,
            NetAddress address, SceneRef scene, Action<NetworkRunner> initialized)
        {
            var sceneManager = runner.GetComponents(typeof(MonoBehaviour))
                .OfType<INetworkSceneManager>()
                .FirstOrDefault();

            if (sceneManager == null) 
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

            runner.ProvideInput = true;

            var result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = gameMode,
                Address = address, 
                Scene = scene, 
                SceneManager = sceneManager
            });

            if (result.Ok)
                // ✅ CALLBACK BURADA MANUEL TETİKLENİR
                initialized?.Invoke(runner);
            else
                Debug.LogError($"StartGame failed: {result.ShutdownReason}");
        }
    }
}

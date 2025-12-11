using System;
using System.Collections.Generic;
using _Root.Scripts.Input;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Diagnostics;

namespace _Root.Scripts.Network
{
    public class Spawner : MonoBehaviour,INetworkRunnerCallbacks
    {
        public NetworkPlayer playerPrefab;

        private CharacterInputController _characterInputController;
        private void OnConnectedToServer()
        {
            
        }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Spawner] ====== OnPlayerJoined ======");
            Debug.Log($"[Spawner] Player: {player}");
            Debug.Log($"[Spawner] IsServer: {runner.IsServer}");
            Debug.Log($"[Spawner] IsClient: {runner.IsClient}");
            Debug.Log($"[Spawner] LocalPlayer: {runner.LocalPlayer}");
            Debug.Log($"[Spawner] SessionName: {runner.SessionInfo.Name}");
            
            // KRİTİK: Sadece server (host) spawn etmeli
            // Client'lar spawn etmemeli - server'dan gelen spawn'ı beklemeliler
            if (runner.IsServer)
            {
                Debug.Log($"[Spawner] ✓ SERVER detected - Spawning player {player}...");

                NetworkPlayer spawnedPlayer = runner.Spawn(
                    playerPrefab,
                    Utils.Utils.GetRandomSpawnPoint(),
                    Quaternion.identity,
                    player  // InputAuthority bu player'a verilecek
                );

                Debug.Log($"[Spawner] ✓ Player {player} spawned successfully!");
                Debug.Log($"[Spawner]   InputAuthority: {spawnedPlayer.Object.InputAuthority}");
                Debug.Log($"[Spawner]   HasInputAuthority: {spawnedPlayer.Object.HasInputAuthority}");
                Debug.Log($"[Spawner]   HasStateAuthority: {spawnedPlayer.Object.HasStateAuthority} ⚠️ (Should be TRUE for server spawn)");
                Debug.Log($"[Spawner]   NetworkObject ID: {spawnedPlayer.Object.Id}");
                Debug.Log($"[Spawner]   Position: {spawnedPlayer.transform.position}");
            }
            else 
            {
                Debug.Log($"[Spawner] ✗ CLIENT detected - NOT spawning");
                Debug.Log($"[Spawner]   Waiting for server to spawn player {player}...");
            }
        }
        
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_characterInputController == null && NetworkPlayer.Local != null)
            {
                _characterInputController = NetworkPlayer.Local.GetComponent<CharacterInputController>();
                
                if (_characterInputController == null)
                {
                    Debug.LogWarning("CharacterInputController bulunamadı!");
                }
                else
                {
                    Debug.Log("CharacterInputController bulundu ve kaydedildi.");
                }
            }

            if (_characterInputController != null)
            {
                var networkInputData = _characterInputController.GetNetworkInput();
                input.Set(networkInputData);
                
                // Debug: Input'un gelip gelmediğini kontrol et (sadece ilk birkaç kez)
                // if (runner.Tick % 60 == 0) // Her 60 tick'te bir log
                // {
                //     Debug.Log($"Input gönderildi - Movement: {networkInputData.MovementInput}");
                // }
            }
            else
            {
                // Input controller yoksa boş input gönder
                input.Set(new NetworkInputData());
            }
        }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            
        }
        

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
            
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            
        }
        

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
            
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            
        }
    }
}
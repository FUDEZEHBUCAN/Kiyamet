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
            // Sadece server (host) spawn etmeli
            if (runner.IsServer)
            {
                runner.Spawn(
                    playerPrefab,
                    Utils.Utils.GetRandomSpawnPoint(),
                    Quaternion.identity,
                    player
                );
            }
        }
        
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_characterInputController == null && NetworkPlayer.Local != null)
            {
                _characterInputController = NetworkPlayer.Local.GetComponent<CharacterInputController>();
            }

            if (_characterInputController != null)
            {
                var networkInputData = _characterInputController.GetNetworkInput();
                input.Set(networkInputData);
            }
            else
            {
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
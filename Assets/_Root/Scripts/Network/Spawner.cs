using System;
using System.Collections.Generic;
using System.Linq;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using _Root.Scripts.Input;
using _Root.Scripts.Network.Lobby;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace _Root.Scripts.Network
{
    public class Spawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static Spawner Instance { get; private set; }

        private const float SharedSpawnSeparationRadius = 2.75f;

        [Header("Player Spawning")]
        public NetworkPlayer playerPrefab;
        [Tooltip("Playtest lobisi — boşsa playerPrefab kullanılır.")]
        [SerializeField] private NetworkPlayer tankPlayerPrefab;
        [SerializeField] private NetworkPlayer supportPlayerPrefab;
        [Tooltip("Player spawn point'leri - boş bırakılırsa sahnede 'Player Spawn' / 'SpawnPoint' aranır.")]
        public Transform[] playerSpawnPoints;

        [Header("Spawn discovery")]
        [SerializeField] private bool autoCollectSpawnPointsInScene = true;

        [Header("Developer Debug")]
        [Tooltip("Açıkken tüm oyuncular playerSpawnPoints listesindeki ilk Transform'ta doğar; dairesel offset ve sahne taraması yapılmaz.")]
        [SerializeField] private bool useSingleSpawnPointDebugMode;

        public bool UseSingleSpawnPointDebugMode => useSingleSpawnPointDebugMode;

        private CharacterInputController _characterInputController;
        private NetworkRunner _runner;
        private NetworkPlayer _configuredTankPrefab;
        private NetworkPlayer _configuredSupportPrefab;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public NetworkPlayer DefaultTankPrefab => tankPlayerPrefab != null ? tankPlayerPrefab : playerPrefab;
        public NetworkPlayer DefaultSupportPrefab => supportPlayerPrefab;

        public void ConfigurePlaytestPrefabs(NetworkPlayer tank, NetworkPlayer support)
        {
            if (tank != null)
                _configuredTankPrefab = tank;
            if (support != null)
                _configuredSupportPrefab = support;
        }

        public void ResetSpawnAssignments()
        {
            EnsurePlayerSpawnPoints();
        }

        public void SpawnPlayerFor(NetworkRunner runner, PlayerRef player, PlayerRoleType role)
        {
            if (!runner.IsServer)
                return;

            _runner = runner;
            EnsurePlayerSpawnPoints();

            if (PlaytestLobbyController.Instance != null
                && PlaytestLobbyController.Instance.TryGetSpawnPrefab(player, out var lockedPrefab)
                && lockedPrefab != null)
            {
                SpawnPlayerWithPrefab(runner, player, lockedPrefab);
                return;
            }

            if (PlaytestLobbyController.Instance != null
                && PlaytestLobbyController.Instance.TryGetLockedRole(player, out var lockedRole))
            {
                role = lockedRole;
            }

            SpawnPlayerWithPrefab(runner, player, ResolvePrefabForRole(role));
        }

        public void SpawnPlayerWithPrefab(NetworkRunner runner, PlayerRef player, NetworkPlayer prefab)
        {
            if (!runner.IsServer || prefab == null)
                return;

            _runner = runner;
            EnsurePlayerSpawnPoints();
            GetSpawnTransform(runner, player, out var spawnPosition, out var spawnRotation);

            Debug.Log($"[Spawner] Spawn player={player.PlayerId} slot={GetSpawnSlotIndex(runner, player)} pos={spawnPosition} prefab={prefab.name}");
            var spawned = runner.Spawn(prefab, spawnPosition, spawnRotation, player);

            if (spawned != null
                && spawned.TryGetComponent<NetworkCharacterControllerCustom>(out var characterController))
            {
                characterController.Teleport(spawnPosition, spawnRotation);
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (PlaytestLobbyController.Instance != null)
                return;

            SpawnPlayerFor(runner, player, PlayerRoleType.Tank);
        }

        private NetworkPlayer ResolvePrefabForRole(PlayerRoleType role)
        {
            switch (role)
            {
                case PlayerRoleType.Support:
                    return _configuredSupportPrefab != null ? _configuredSupportPrefab
                        : supportPlayerPrefab != null ? supportPlayerPrefab
                        : playerPrefab;
                case PlayerRoleType.Tank:
                default:
                    return _configuredTankPrefab != null ? _configuredTankPrefab
                        : tankPlayerPrefab != null ? tankPlayerPrefab
                        : playerPrefab;
            }
        }

        private void EnsurePlayerSpawnPoints()
        {
            if (useSingleSpawnPointDebugMode)
                return;

            if (!autoCollectSpawnPointsInScene)
                return;

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 1)
                return;

            var collected = new List<Transform>();
            foreach (var transform in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform == null)
                    continue;

                var name = transform.name;
                if (name == "Player Spawn"
                    || name.StartsWith("Player Spawn (", StringComparison.Ordinal)
                    || name.StartsWith("SpawnPoint", StringComparison.Ordinal))
                {
                    collected.Add(transform);
                }
            }

            if (collected.Count == 0)
                return;

            collected = collected
                .OrderBy(t => t.name, StringComparer.Ordinal)
                .ToList();

            playerSpawnPoints = collected.ToArray();
        }

        private void GetSpawnTransform(NetworkRunner runner, PlayerRef player, out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            if (TryGetSingleDebugSpawnTransform(out spawnPosition, out spawnRotation))
                return;

            var slot = GetSpawnSlotIndex(runner, player);
            spawnRotation = Utils.Utils.GetSpawnRotationForSlot(slot);
            spawnPosition = Utils.Utils.GetSpawnPositionForSlot(slot);

            var pointCount = playerSpawnPoints != null ? playerSpawnPoints.Length : 0;
            if (pointCount <= 1 || slot >= pointCount)
                ApplySharedSpawnOffset(ref spawnPosition, spawnRotation, slot);
        }

        public bool TryGetSingleDebugSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            spawnPosition = default;
            spawnRotation = Quaternion.identity;

            if (!useSingleSpawnPointDebugMode)
                return false;

            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0 || playerSpawnPoints[0] == null)
            {
                Debug.LogWarning("[Spawner] Single spawn debug mode is on but playerSpawnPoints[0] is not assigned.");
                return false;
            }

            spawnPosition = playerSpawnPoints[0].position;
            spawnRotation = playerSpawnPoints[0].rotation;
            return true;
        }

        private static int GetSpawnSlotIndex(NetworkRunner runner, PlayerRef player)
        {
            var slot = 0;
            foreach (var activePlayer in runner.ActivePlayers.OrderBy(p => p.PlayerId))
            {
                if (activePlayer == player)
                    return slot;
                slot++;
            }

            return player.PlayerId;
        }

        private static void ApplySharedSpawnOffset(ref Vector3 position, Quaternion rotation, int slotIndex)
        {
            if (slotIndex <= 0)
                return;

            var angleRad = slotIndex * (Mathf.PI * 2f / 8f);
            var localOffset = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * SharedSpawnSeparationRadius;
            var yawRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            position += yawRotation * localOffset;
        }

        public void ClearCachedInputController()
        {
            _characterInputController = null;
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_characterInputController == null && NetworkPlayer.Local != null)
                _characterInputController = NetworkPlayer.Local.GetComponent<CharacterInputController>();

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

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnConnectedToServer(NetworkRunner runner) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}

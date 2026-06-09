using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _Root.Scripts.Enums;
using _Root.Scripts.UI;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace _Root.Scripts.Network.Lobby
{
    [DisallowMultipleComponent]
    public class PlaytestLobbyController : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static PlaytestLobbyController Instance { get; private set; }

        private const int MaxLobbyPlayers = 3;

        [Header("References")]
        [SerializeField] private NetworkRunnerHandler networkRunnerHandler;
        [SerializeField] private Spawner spawner;

        [Header("UI")]
        [SerializeField] private PlaytestLobbyView lobbyView;
        [SerializeField] private GameObject lobbyUiPrefab;

        [Header("Player prefabs")]
        [SerializeField] private NetworkPlayer tankPlayerPrefab;
        [SerializeField] private NetworkPlayer supportPlayerPrefab;
        [SerializeField] private NetworkPlayer duelistPlayerPrefab;

        [Header("Session")]
        [SerializeField] private string defaultSessionName = "";

        private NetworkRunner _runner;
        private PlayerRoleType _localPendingRole = PlayerRoleType.Tank;
        private bool _hasPickedRole;
        private bool _localRoleLocked;
        private string _sessionName;
        private string _statusMessage = "Enter a session name and join the lobby.";
        private bool _isConnecting;
        private bool _isConnected;
        private bool _gameStarted;
        private bool _isLeavingSession;

        private readonly Dictionary<PlayerRef, PlayerRoleType> _lockedRoles = new();
        private readonly Dictionary<PlayerRef, NetworkPlayer> _lockedPrefabs = new();
        private PlaytestLobbyView _view;

        public bool IsGameStarted => _gameStarted;
        public bool IsLobbyActive => IsLobbyUiVisible();

        public bool TryGetLockedRole(PlayerRef player, out PlayerRoleType role) =>
            _lockedRoles.TryGetValue(player, out role);

        public bool TryGetSpawnPrefab(PlayerRef player, out NetworkPlayer prefab) =>
            _lockedPrefabs.TryGetValue(player, out prefab);

        private PlaytestLobbyView ResolveLobbyView()
        {
            if (lobbyView != null)
                return lobbyView;

            var childView = GetComponentInChildren<PlaytestLobbyView>(true);
            if (childView != null)
                return childView;

            var prefab = lobbyUiPrefab != null
                ? lobbyUiPrefab
                : Resources.Load<GameObject>("PlaytestLobbyUI");

            if (prefab == null)
                return null;

            var uiInstance = Instantiate(prefab);
            uiInstance.name = "PlaytestLobbyUI";
            return uiInstance.GetComponent<PlaytestLobbyView>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _sessionName = defaultSessionName;

            if (networkRunnerHandler == null)
                networkRunnerHandler = GetComponent<NetworkRunnerHandler>();
            if (spawner == null)
                spawner = GetComponent<Spawner>();

            EnsurePlayerPrefabReferences();

            _view = ResolveLobbyView();
            if (_view == null)
            {
                Debug.LogError("[PlaytestLobby] PlaytestLobbyView not found. Assign lobbyView or lobbyUiPrefab on PlaytestLobbyController, or use PlaytestLobbySystem prefab.");
                return;
            }

            EnsureLobbyEventSystem();
            UnlockLobbyCursor();

            _view.Initialize();
            _view.SetSessionName(_sessionName);
            _view.ConnectClicked += OnViewConnectClicked;
            _view.QuitGameClicked += OnViewQuitGameClicked;
            _view.StartGameClicked += StartMatch;
            _view.LockRoleClicked += OnViewLockRoleClicked;
            _view.LeaveLobbyClicked += OnViewLeaveLobbyClicked;
            _view.RoleSelected += OnViewRoleSelected;
            _view.BindUiEvents();
            RefreshLobbyUi();
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.ConnectClicked -= OnViewConnectClicked;
                _view.QuitGameClicked -= OnViewQuitGameClicked;
                _view.StartGameClicked -= StartMatch;
                _view.LockRoleClicked -= OnViewLockRoleClicked;
                _view.LeaveLobbyClicked -= OnViewLeaveLobbyClicked;
                _view.RoleSelected -= OnViewRoleSelected;
            }

            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (!IsLobbyUiVisible())
                return;

            EnsureLobbyEventSystem();
            UnlockLobbyCursor();
        }

        private bool IsLobbyUiVisible() => !_gameStarted;

        private static void EnsureLobbyEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemGo);
            eventSystemGo.AddComponent<EventSystem>();

            var inputModule = eventSystemGo.AddComponent<StandaloneInputModule>();
            inputModule.forceModuleActive = true;
        }

        private static void UnlockLobbyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnViewConnectClicked() => _ = ConnectAsync();

        private void OnViewQuitGameClicked() => QuitApplication();

        private void OnViewLeaveLobbyClicked() => _ = LeaveSessionAsync();

        public static void QuitApplication()
        {
            SceneManager.LoadScene(0);
        }

        /// <summary>Disconnect and return to the pre-join lobby screen.</summary>
        public async Task LeaveSessionAsync()
        {
            if (_isLeavingSession)
                return;

            _isLeavingSession = true;
            GameplayPauseMenu.Instance?.ForceClose();

            _statusMessage = "Leaving...";
            RefreshLobbyUi();
            _view?.SetLeaveLobbyVisible(true, false);

            try
            {
                if (networkRunnerHandler != null)
                    await networkRunnerHandler.ShutdownSessionAsync();
            }
            finally
            {
                ResetSessionState();
                _isLeavingSession = false;
                _statusMessage = "Left the lobby. Enter a session name to join again.";
                RefreshLobbyUi();
            }
        }

        private void ResetSessionState()
        {
            _runner = null;
            _isConnected = false;
            _isConnecting = false;
            _gameStarted = false;
            _localRoleLocked = false;
            _hasPickedRole = false;
            _localPendingRole = PlayerRoleType.Tank;
            _lockedRoles.Clear();
            _lockedPrefabs.Clear();
        }

        private void OnViewRoleSelected(PlayerRoleType role)
        {
            if (!_isConnected || _isConnecting || _gameStarted || _localRoleLocked)
                return;

            _localPendingRole = role;
            _hasPickedRole = true;
            RefreshLobbyUi();
        }

        private void OnViewLockRoleClicked()
        {
            if (!_isConnected || _localRoleLocked || _runner == null || !_hasPickedRole)
                return;

            if (!PlaytestLobbyPrefabUtility.ValidatePlaytestPrefabs(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab, out var prefabError))
            {
                _statusMessage = prefabError;
                RefreshLobbyUi();
                return;
            }

            if (_runner.IsServer)
            {
                if (TryLockRole(_runner.LocalPlayer, _localPendingRole, out var error))
                {
                    _localRoleLocked = true;
                    _statusMessage = $"Role locked: {RoleDisplayName(_localPendingRole)}.";
                    BroadcastLobbyState();
                }
                else
                {
                    _statusMessage = error;
                }

                RefreshLobbyUi();
                return;
            }

            var payload = PlaytestLobbyNetworkMessages.CreateLockRoleRequest(_localPendingRole);
            _runner.SendReliableDataToServer(PlaytestLobbyNetworkMessages.ReliableKey, payload);
            _statusMessage = "Locking role...";
            RefreshLobbyUi();
        }

        private void RefreshLobbyUi()
        {
            if (_view == null)
                return;

            var lobbyVisible = IsLobbyUiVisible();
            _view.SetVisible(lobbyVisible);
            if (!lobbyVisible)
                return;

            _view.SetRoster(BuildLobbyRosterText());

            if (!_isConnected)
            {
                _view.SetPreConnectPhase(_isConnecting);
                _view.SetStatus(_statusMessage);
                return;
            }

            var isHost = _runner != null && _runner.IsServer;
            _view.SetInLobbyPhase(isHost, _sessionName);
            _view.SetLobbyStatus(_statusMessage);

            if (_hasPickedRole || _localRoleLocked)
                _view.SetSelectedRole(_localPendingRole);
            else
                _view.ClearRoleSelection();

            var tankTaken = IsRoleLockedByOther(PlayerRoleType.Tank);
            var supportTaken = IsRoleLockedByOther(PlayerRoleType.Support);
            var duelistTaken = IsRoleLockedByOther(PlayerRoleType.Duelist);
            var canPick = !_localRoleLocked;
            _view.SetRolePickable(
                PlayerRoleType.Tank,
                canPick && !tankTaken,
                _localRoleLocked && _localPendingRole == PlayerRoleType.Tank);
            _view.SetRolePickable(
                PlayerRoleType.Support,
                canPick && !supportTaken,
                _localRoleLocked && _localPendingRole == PlayerRoleType.Support);
            _view.SetRolePickable(
                PlayerRoleType.Duelist,
                canPick && !duelistTaken,
                _localRoleLocked && _localPendingRole == PlayerRoleType.Duelist);

            if (_localRoleLocked)
            {
                _view.SetLockRoleButton(true, false, $"Locked: {RoleDisplayName(_localPendingRole)}");
            }
            else
            {
                var roleAvailable = (_localPendingRole == PlayerRoleType.Tank && !tankTaken)
                                    || (_localPendingRole == PlayerRoleType.Support && !supportTaken)
                                    || (_localPendingRole == PlayerRoleType.Duelist && !duelistTaken);
                var canLock = _hasPickedRole && roleAvailable;
                _view.SetLockRoleButton(true, canLock, canLock ? "Lock Role" : "Pick an available role");
            }
        }

        private bool IsRoleLockedByOther(PlayerRoleType role)
        {
            if (_runner == null)
                return false;

            foreach (var pair in _lockedRoles)
            {
                if (pair.Value == role && pair.Key != _runner.LocalPlayer)
                    return true;
            }

            return false;
        }

        private string BuildLobbyRosterText()
        {
            if (_runner == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Players ({_runner.ActivePlayers.Count()}):");

            foreach (var player in _runner.ActivePlayers)
            {
                var you = player == _runner.LocalPlayer ? " (you)" : string.Empty;
                if (_lockedRoles.TryGetValue(player, out var role))
                    sb.AppendLine($"  • Player {player.PlayerId}: {RoleDisplayName(role)} — locked{you}");
                else
                    sb.AppendLine($"  • Player {player.PlayerId}: choosing role...{you}");
            }

            if (_runner.IsServer)
                sb.AppendLine("\nStart when everyone has locked a role (solo is fine).");

            return sb.ToString();
        }

        private static string RoleDisplayName(PlayerRoleType role) =>
            PlaytestLobbyRoles.GetDisplayName(role);

        private async Task ConnectAsync()
        {
            if (networkRunnerHandler == null)
            {
                _statusMessage = "NetworkRunnerHandler not found.";
                RefreshLobbyUi();
                return;
            }

            _sessionName = _view != null ? _view.SessionName : _sessionName;
            if (string.IsNullOrWhiteSpace(_sessionName))
            {
                _statusMessage = "Enter a session name before joining.";
                RefreshLobbyUi();
                return;
            }

            _sessionName = _sessionName.Trim();
            _isConnecting = true;
            _statusMessage = "Connecting...";
            _localRoleLocked = false;
            _hasPickedRole = false;
            _localPendingRole = PlayerRoleType.Tank;
            _lockedRoles.Clear();
            _lockedPrefabs.Clear();
            RefreshLobbyUi();

            var runner = await networkRunnerHandler.StartPlaytestSessionAsync(
                _sessionName,
                PlaytestLobbyRoleToken.EncodeJoin(),
                this);

            _isConnecting = false;

            if (runner == null)
            {
                _statusMessage = "Connection failed. Check the console.";
                RefreshLobbyUi();
                return;
            }

            _runner = runner;
            _isConnected = true;
            _statusMessage = runner.IsServer
                ? "You are the host. Choose and lock your role."
                : "Joined the lobby. Choose and lock your role.";
            RefreshLobbyUi();

            if (runner.IsServer)
                BroadcastLobbyState();
        }

        private void StartMatch()
        {
            if (_runner == null || !_runner.IsServer || _gameStarted || spawner == null)
                return;

            if (!PlaytestLobbyPrefabUtility.ValidatePlaytestPrefabs(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab, out var prefabError))
            {
                _statusMessage = prefabError;
                RefreshLobbyUi();
                return;
            }

            foreach (var player in _runner.ActivePlayers)
            {
                if (!_lockedRoles.ContainsKey(player) || !_lockedPrefabs.TryGetValue(player, out var prefab) || prefab == null)
                {
                    _statusMessage = "Every player must lock a role before starting.";
                    RefreshLobbyUi();
                    return;
                }

                var role = _lockedRoles[player];
                if (prefab.RoleType != role)
                {
                    _statusMessage =
                        $"Prefab mismatch for {role}: assigned '{prefab.name}' ({prefab.RoleType}). Check PlaytestLobbyController prefabs.";
                    RefreshLobbyUi();
                    return;
                }
            }

            BeginMatchOnHost();
        }

        private void BeginMatchOnHost()
        {
            _gameStarted = true;
            spawner.ConfigurePlaytestPrefabs(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab);
            spawner.ResetSpawnAssignments();
            spawner.ClearCachedInputController();
            BroadcastGameStarted();

            foreach (var player in _runner.ActivePlayers)
            {
                var prefab = _lockedPrefabs[player];
                Debug.Log($"[PlaytestLobby] Spawning player {player.PlayerId} as {_lockedRoles[player]} using {prefab.name}");
                spawner.SpawnPlayerWithPrefab(_runner, player, prefab);
            }

            _statusMessage = "Starting game...";
            RefreshLobbyUi();
        }

        private void ApplyGameStartedFromNetwork()
        {
            if (_gameStarted)
                return;

            _gameStarted = true;
            if (spawner != null)
                spawner.ClearCachedInputController();

            _statusMessage = "Game started!";
            RefreshLobbyUi();
        }

        private void BroadcastGameStarted()
        {
            if (_runner == null || !_runner.IsServer)
                return;

            var payload = PlaytestLobbyNetworkMessages.CreateGameStarted();
            foreach (var player in _runner.ActivePlayers)
                _runner.SendReliableDataToPlayer(player, PlaytestLobbyNetworkMessages.ReliableKey, payload);
        }

        private bool TryLockRole(PlayerRef player, PlayerRoleType role, out string error)
        {
            error = string.Empty;

            if (!PlaytestLobbyRoles.IsLobbySelectable(role))
            {
                error = "Invalid role.";
                return false;
            }

            if (_lockedRoles.TryGetValue(player, out _))
            {
                error = "You already locked a role.";
                return false;
            }

            foreach (var pair in _lockedRoles)
            {
                if (pair.Value == role && pair.Key != player)
                {
                    error = $"{RoleDisplayName(role)} is already locked by another player.";
                    return false;
                }
            }

            _lockedRoles[player] = role;
            _lockedPrefabs[player] = PlaytestLobbyPrefabUtility.Resolve(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab, role);
            return true;
        }

        private void EnsurePlayerPrefabReferences()
        {
            if (spawner == null)
                return;

            if (tankPlayerPrefab == null)
                tankPlayerPrefab = spawner.DefaultTankPrefab;

            if (supportPlayerPrefab == null)
                supportPlayerPrefab = spawner.DefaultSupportPrefab;

            if (duelistPlayerPrefab == null)
                duelistPlayerPrefab = spawner.DefaultDuelistPrefab;

            if (!PlaytestLobbyPrefabUtility.ValidatePlaytestPrefabs(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab, out var error))
                Debug.LogWarning($"[PlaytestLobby] {error}");
        }

        private void BroadcastLobbyState()
        {
            if (_runner == null || !_runner.IsServer)
                return;

            var payload = PlaytestLobbyNetworkMessages.CreateSyncState(_lockedRoles, _runner.ActivePlayers);
            foreach (var player in _runner.ActivePlayers)
                _runner.SendReliableDataToPlayer(player, PlaytestLobbyNetworkMessages.ReliableKey, payload);
        }

        private void ApplyLobbyState(Dictionary<PlayerRef, PlayerRoleType> synced)
        {
            _lockedRoles.Clear();
            _lockedPrefabs.Clear();
            foreach (var pair in synced)
            {
                _lockedRoles[pair.Key] = pair.Value;
                _lockedPrefabs[pair.Key] = PlaytestLobbyPrefabUtility.Resolve(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab, pair.Value);
            }

            if (_runner != null && _lockedRoles.TryGetValue(_runner.LocalPlayer, out var localRole))
            {
                _localPendingRole = localRole;
                _localRoleLocked = true;
            }

            RefreshLobbyUi();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            BroadcastLobbyState();

            if (_gameStarted)
            {
                runner.SendReliableDataToPlayer(
                    player,
                    PlaytestLobbyNetworkMessages.ReliableKey,
                    PlaytestLobbyNetworkMessages.CreateGameStarted());

                if (spawner != null && _lockedPrefabs.TryGetValue(player, out var prefab) && prefab != null)
                {
                    spawner.ConfigurePlaytestPrefabs(tankPlayerPrefab, supportPlayerPrefab, duelistPlayerPrefab);
                    spawner.SpawnPlayerWithPrefab(runner, player, prefab);
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            _lockedRoles.Remove(player);
            _lockedPrefabs.Remove(player);
            if (runner.IsServer)
                BroadcastLobbyState();
            else
                RefreshLobbyUi();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!runner.IsServer)
                return;

            if (!PlaytestLobbyRoleToken.IsValidJoinToken(token))
            {
                request.Refuse();
                return;
            }

            if (CountActivePlayers(runner) >= MaxLobbyPlayers)
            {
                Debug.Log("[PlaytestLobby] Lobby full — connection refused.");
                request.Refuse();
                return;
            }

            request.Accept();
        }

        private static int CountActivePlayers(NetworkRunner runner)
        {
            int count = 0;
            foreach (var _ in runner.ActivePlayers)
                count++;
            return count;
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data)
        {
            var bytes = data.Array;
            if (bytes == null || data.Count <= 0)
                return;

            var slice = new byte[data.Count];
            System.Array.Copy(bytes, data.Offset, slice, 0, data.Count);

            if (runner.IsServer && PlaytestLobbyNetworkMessages.TryParseLockRoleRequest(slice, out var requestedRole))
            {
                if (TryLockRole(player, requestedRole, out var error))
                {
                    if (player == runner.LocalPlayer)
                    {
                        _localPendingRole = requestedRole;
                        _localRoleLocked = true;
                        _statusMessage = $"Role locked: {RoleDisplayName(requestedRole)}.";
                    }

                    BroadcastLobbyState();
                }
                else
                {
                    runner.SendReliableDataToPlayer(player, PlaytestLobbyNetworkMessages.ReliableKey,
                        PlaytestLobbyNetworkMessages.CreateLockDenied(error));
                }

                return;
            }

            if (PlaytestLobbyNetworkMessages.IsLockDenied(slice)
                && player == runner.LocalPlayer
                && PlaytestLobbyNetworkMessages.TryParseLockDenied(slice, out var reason))
            {
                _statusMessage = string.IsNullOrEmpty(reason) ? "Could not lock role." : reason;
                RefreshLobbyUi();
                return;
            }

            if (PlaytestLobbyNetworkMessages.IsGameStarted(slice))
            {
                ApplyGameStartedFromNetwork();
                return;
            }

            if (PlaytestLobbyNetworkMessages.TryParseSyncState(slice, out var synced))
            {
                if (runner.IsServer)
                    RefreshLobbyUi();
                else
                    ApplyLobbyState(synced);
            }
        }

        #region INetworkRunnerCallbacks

        public void OnConnectedToServer(NetworkRunner runner) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            _statusMessage = $"Connection error: {reason}";
            _isConnected = false;
            _isConnecting = false;
            RefreshLobbyUi();
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            ResetSessionState();
            _statusMessage = _isLeavingSession ? "Left the lobby." : $"Disconnected: {reason}";
            RefreshLobbyUi();
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            ResetSessionState();
            if (!_isLeavingSession)
                _statusMessage = "Session ended.";
            RefreshLobbyUi();
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        #endregion
    }

    /// <summary>Connection token — join only; role is locked after connecting via lobby UI.</summary>
    public static class PlaytestLobbyRoleToken
    {
        private const byte Version = 2;
        private const byte JoinWithoutRole = 0;

        public static byte[] EncodeJoin() => new[] { Version, JoinWithoutRole };

        public static bool IsValidJoinToken(byte[] token) =>
            token != null && token.Length >= 2 && token[0] == Version;
    }

    internal static class PlaytestLobbyPrefabUtility
    {
        public static NetworkPlayer Resolve(NetworkPlayer tank, NetworkPlayer support, NetworkPlayer duelist, PlayerRoleType role)
        {
            switch (role)
            {
                case PlayerRoleType.Support:
                    return support != null ? support : tank;
                case PlayerRoleType.Duelist:
                    return duelist != null ? duelist : tank;
                default:
                    return tank;
            }
        }

        public static bool ValidatePlaytestPrefabs(NetworkPlayer tank, NetworkPlayer support, NetworkPlayer duelist, out string error)
        {
            error = string.Empty;

            if (tank == null)
            {
                error = "Tank player prefab is not assigned.";
                return false;
            }

            if (support == null)
            {
                error = "Support player prefab is not assigned.";
                return false;
            }

            if (duelist == null)
            {
                error = "Duelist player prefab is not assigned.";
                return false;
            }

            if (ReferenceEquals(tank, support) || ReferenceEquals(tank, duelist) || ReferenceEquals(support, duelist))
            {
                error = "Tank, Support and Duelist prefabs must be different assets.";
                return false;
            }

            if (tank.RoleType != PlayerRoleType.Tank)
            {
                error = $"Tank prefab '{tank.name}' has role {tank.RoleType}, expected Tank.";
                return false;
            }

            if (support.RoleType != PlayerRoleType.Support)
            {
                error = $"Support prefab '{support.name}' has role {support.RoleType}, expected Support.";
                return false;
            }

            if (duelist.RoleType != PlayerRoleType.Duelist)
            {
                error = $"Duelist prefab '{duelist.name}' has role {duelist.RoleType}, expected Duelist.";
                return false;
            }

            return true;
        }
    }
}

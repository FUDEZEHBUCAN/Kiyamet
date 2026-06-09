using _Root.Scripts.Boss;
using Fusion;
using UnityEngine;

namespace _Root.Scripts.DevTools
{
    /// <summary>
    /// Demo günü: oyuncular pes ettiğinde host boss'u manuel olarak öldürür (F9 veya pause menüsü).
    /// </summary>
    [DisallowMultipleComponent]
    public class DemoBossKillCheat : MonoBehaviour
    {
        [Header("Demo Day")]
        [SerializeField] private bool demoBossKillEnabled;
        [SerializeField] private KeyCode manualKillKey = KeyCode.F9;
        [SerializeField] private bool showHostHint = true;

        private NetworkRunner _runner;
        private NetworkBoss _boss;
        private bool _killApplied;

        public bool IsEnabled => demoBossKillEnabled;
        public bool HasAppliedKill => _killApplied;

        public void ConfigureForDemoSession(bool enabled)
        {
            demoBossKillEnabled = enabled;
            _killApplied = false;
        }

        public bool CanRequestKill()
        {
            if (!demoBossKillEnabled || _killApplied)
                return false;

            if (!TryResolveRunner(out NetworkRunner runner) || !runner.IsRunning || !runner.IsServer)
                return false;

            if (!TryResolveBoss(out NetworkBoss boss))
                return false;

            return boss.Object.HasStateAuthority && boss.IsAlive;
        }

        public bool TryRequestBossKill()
        {
            if (!CanRequestKill())
                return false;

            TryKillBoss();
            return _killApplied;
        }

        private void Update()
        {
            if (!demoBossKillEnabled || _killApplied)
                return;

            if (!TryResolveRunner(out NetworkRunner runner) || !runner.IsRunning || !runner.IsServer)
                return;

            if (UnityEngine.Input.GetKeyDown(manualKillKey))
                TryRequestBossKill();
        }

        private void OnGUI()
        {
            if (!demoBossKillEnabled || !showHostHint || _killApplied)
                return;

            if (_runner == null || !_runner.IsRunning || !_runner.IsServer)
                return;

            const float width = 460f;
            const float height = 22f;
            var rect = new Rect(12f, Screen.height - height - 12f, width, height);
            GUI.Label(
                rect,
                $"Demo cheat — pes edildiğinde {manualKillKey} veya ESC menüsünden boss'u bitir (host)");
        }

        private void TryKillBoss()
        {
            if (_killApplied || !CanRequestKill())
                return;

            if (!TryResolveBoss(out NetworkBoss boss))
                return;

            boss.ForceDemoKill();
            _killApplied = true;
            Debug.Log("[DemoBossKillCheat] Boss demo skip ile öldürüldü (manuel).");
        }

        private bool TryResolveRunner(out NetworkRunner runner)
        {
            if (_runner != null && _runner.IsRunning)
            {
                runner = _runner;
                return true;
            }

            _runner = FindFirstObjectByType<NetworkRunner>();
            runner = _runner;
            return runner != null && runner.IsRunning;
        }

        private bool TryResolveBoss(out NetworkBoss boss)
        {
            if (_boss != null && _boss.Object != null && _boss.Object.IsValid)
            {
                boss = _boss;
                return true;
            }

            _boss = FindFirstObjectByType<NetworkBoss>();
            boss = _boss;
            return boss != null && boss.Object != null && boss.Object.IsValid;
        }
    }
}

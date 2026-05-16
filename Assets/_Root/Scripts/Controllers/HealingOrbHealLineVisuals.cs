using System.Collections.Generic;
using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Healing orb'dan yarıçap içindeki tüm oyunculara elektrik benzeri LineRenderer bağlantıları.
    /// Her istemcide aynı bağlantılar görünür (yalnızca local oyuncuya özel filtre yok).
    /// </summary>
    [DisallowMultipleComponent]
    public class HealingOrbHealLineVisuals : MonoBehaviour
    {
        [SerializeField] private HealingOrbProjectile orb;
        [SerializeField] private LineRenderer lineTemplate;
        [SerializeField] private Transform linesParent;

        [Header("Elektrik arkı")]
        [SerializeField] private ElectricArcLineUtility.Settings arcSettings = ElectricArcLineUtility.Settings.Default;

        private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
        private readonly Dictionary<int, LineRenderer> _activeLinesByPlayerId = new Dictionary<int, LineRenderer>();
        private readonly Vector3[] _segmentBuffer = new Vector3[64];
        private readonly HashSet<int> _usedPlayerIds = new HashSet<int>();

        private void Awake()
        {
            if (orb == null)
                orb = GetComponent<HealingOrbProjectile>();

            if (lineTemplate == null)
                lineTemplate = GetComponent<LineRenderer>();

            if (linesParent == null)
            {
                var parentGo = new GameObject("HealLines");
                linesParent = parentGo.transform;
                linesParent.SetParent(transform, false);
            }

            if (lineTemplate != null)
                lineTemplate.enabled = false;
        }

        public void UpdateLines(Vector3 orbCenter, IReadOnlyList<NetworkPlayer> targets)
        {
            if (lineTemplate == null || orb == null)
                return;

            int segmentCount = Mathf.Clamp(arcSettings.SegmentsPerLine, 4, _segmentBuffer.Length);
            float time = Time.time;
            _usedPlayerIds.Clear();

            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var player = targets[i];
                    if (player == null || player.Object == null || !player.Object.IsValid || !player.IsAlive)
                        continue;

                    int playerId = player.Object.Id.GetHashCode();
                    _usedPlayerIds.Add(playerId);

                    LineRenderer line = GetOrCreateLine(playerId);
                    if (line == null)
                        continue;

                    Vector3 targetPos = HealingOrbProjectile.GetPlayerHealSamplePosition(player);
                    ElectricArcLineUtility.PopulateElectricArc(
                        line, lineTemplate, orbCenter, targetPos,
                        segmentCount, time, playerId, arcSettings, _segmentBuffer);
                    line.enabled = true;
                }
            }

            ReleaseUnusedLines();
        }

        private LineRenderer GetOrCreateLine(int playerId)
        {
            if (_activeLinesByPlayerId.TryGetValue(playerId, out LineRenderer existing))
                return existing;

            LineRenderer line = AcquireLineFromPool();
            if (line == null)
                return null;

            _activeLinesByPlayerId[playerId] = line;
            return line;
        }

        private LineRenderer AcquireLineFromPool()
        {
            for (int i = 0; i < _linePool.Count; i++)
            {
                LineRenderer pooled = _linePool[i];
                if (pooled == null)
                    continue;

                bool inUse = false;
                foreach (var entry in _activeLinesByPlayerId)
                {
                    if (entry.Value == pooled)
                    {
                        inUse = true;
                        break;
                    }
                }

                if (!inUse)
                    return pooled;
            }

            var lineGo = new GameObject("HealLine");
            lineGo.transform.SetParent(linesParent, false);
            var line = lineGo.AddComponent<LineRenderer>();
            ElectricArcLineUtility.CopyLineRendererSettings(lineTemplate, line);
            _linePool.Add(line);
            return line;
        }

        private void ReleaseUnusedLines()
        {
            var toRemove = new List<int>();

            foreach (var entry in _activeLinesByPlayerId)
            {
                if (_usedPlayerIds.Contains(entry.Key))
                    continue;

                if (entry.Value != null)
                    entry.Value.enabled = false;
                toRemove.Add(entry.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _activeLinesByPlayerId.Remove(toRemove[i]);
        }

        private void OnDisable()
        {
            foreach (var entry in _activeLinesByPlayerId)
            {
                if (entry.Value != null)
                    entry.Value.enabled = false;
            }

            _activeLinesByPlayerId.Clear();
        }
    }
}

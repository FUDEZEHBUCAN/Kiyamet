using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Enemy;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Kubbe tepesinden müttefiklere (yeşil) ve düşmanlara (kırmızı) elektrik arkı çizer.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeDistortionDomeConnectionLines : MonoBehaviour
    {
        [SerializeField] private TimeDistortionDomeZone zone;
        [SerializeField] private TimeDistortionDomeVisuals domeVisuals;
        [SerializeField] private LineRenderer allyLineTemplate;
        [SerializeField] private LineRenderer enemyLineTemplate;
        [SerializeField] private Transform linesParent;

        [Header("Elektrik arkı")]
        [SerializeField] private ElectricArcLineUtility.Settings arcSettings = ElectricArcLineUtility.Settings.Default;
        [SerializeField] private float allyLineWidthScale = 2.2f;
        [SerializeField] private float enemyLineWidthScale = 2.4f;

        [Header("Düşman çizgi hedefi")]
        [Tooltip("Collider merkezinden sonra dünya Y ekseninde eklenir (yukarı: pozitif).")]
        [SerializeField] private float enemyLineHeightOffset = 1.5f;
        [Tooltip("Collider yoksa transform.position + bu yükseklik kullanılır.")]
        [SerializeField] private float enemyFallbackHeight = 1.4f;

        private readonly List<LineRenderer> _allyLinePool = new List<LineRenderer>();
        private readonly List<LineRenderer> _enemyLinePool = new List<LineRenderer>();
        private readonly Dictionary<int, LineRenderer> _activeAllyLines = new Dictionary<int, LineRenderer>();
        private readonly Dictionary<int, LineRenderer> _activeEnemyLines = new Dictionary<int, LineRenderer>();
        private readonly Vector3[] _segmentBuffer = new Vector3[64];
        private readonly HashSet<int> _usedAllyIds = new HashSet<int>();
        private readonly HashSet<int> _usedEnemyIds = new HashSet<int>();

        private void Awake()
        {
            if (zone == null)
                zone = GetComponent<TimeDistortionDomeZone>();

            if (domeVisuals == null)
                domeVisuals = GetComponent<TimeDistortionDomeVisuals>();

            EnsureLinesParent();

            if (allyLineTemplate != null)
                allyLineTemplate.enabled = false;

            if (enemyLineTemplate != null)
                enemyLineTemplate.enabled = false;
        }

        /// <summary>Yarıçap / oyun mantığı için taban nokta (offset yok).</summary>
        public static Vector3 GetEnemyConnectionBasePosition(NetworkEnemy enemy)
        {
            if (enemy == null)
                return Vector3.zero;

            if (TryGetEnemyColliderCenter(enemy, out Vector3 center))
                return center;

            return enemy.transform.position;
        }

        private Vector3 GetEnemyLineTargetPosition(NetworkEnemy enemy)
        {
            if (enemy == null)
                return Vector3.zero;

            if (TryGetEnemyColliderCenter(enemy, out Vector3 center))
                return center + Vector3.up * enemyLineHeightOffset;

            return enemy.transform.position + Vector3.up * (enemyFallbackHeight + enemyLineHeightOffset);
        }

        private static bool TryGetEnemyColliderCenter(NetworkEnemy enemy, out Vector3 center)
        {
            center = Vector3.zero;
            if (enemy == null)
                return false;

            var colliders = enemy.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                center = col.bounds.center;
                return true;
            }

            return false;
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
                ReleaseAllLines();
        }

        public void UpdateConnections(
            IReadOnlyList<NetworkPlayer> allies,
            IReadOnlyList<NetworkEnemy> enemies)
        {
            if (allyLineTemplate == null && enemyLineTemplate == null)
                return;

            if (zone == null || !zone.IsDomeActive)
            {
                ReleaseAllLines();
                return;
            }

            Vector3 apex = domeVisuals != null
                ? domeVisuals.GetDomeApexWorldPosition()
                : zone.transform.position + Vector3.up * zone.Radius;

            int segmentCount = Mathf.Clamp(arcSettings.SegmentsPerLine, 4, _segmentBuffer.Length);
            float time = Time.time;
            _usedAllyIds.Clear();
            _usedEnemyIds.Clear();

            if (allies != null && allyLineTemplate != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    var ally = allies[i];
                    if (!IsPlayerValidForLine(ally))
                        continue;

                    int allyId = GetVisualTargetId(ally);
                    _usedAllyIds.Add(allyId);

                    var line = GetOrCreateLine(allyId, isEnemy: false);
                    if (line == null)
                        continue;

                    Vector3 targetPos = HealingOrbProjectile.GetPlayerHealSamplePosition(ally);
                    PopulateLine(line, allyLineTemplate, apex, targetPos, segmentCount, time, allyId, allyLineWidthScale);
                    line.enabled = true;
                }
            }

            if (enemies != null && enemyLineTemplate != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (!IsEnemyValidForLine(enemy))
                        continue;

                    int enemyId = GetVisualTargetId(enemy);
                    _usedEnemyIds.Add(enemyId);

                    var line = GetOrCreateLine(enemyId, isEnemy: true);
                    if (line == null)
                        continue;

                    Vector3 targetPos = GetEnemyLineTargetPosition(enemy);
                    PopulateLine(line, enemyLineTemplate, apex, targetPos, segmentCount, time, enemyId, enemyLineWidthScale);
                    line.enabled = true;
                }
            }

            ReleaseUnusedLines(_activeAllyLines, _usedAllyIds);
            ReleaseUnusedLines(_activeEnemyLines, _usedEnemyIds);
        }

        private void PopulateLine(
            LineRenderer line,
            LineRenderer template,
            Vector3 apex,
            Vector3 targetPos,
            int segmentCount,
            float time,
            int phaseSeed,
            float widthScale)
        {
            ElectricArcLineUtility.PopulateElectricArc(
                line, template, apex, targetPos,
                segmentCount, time, phaseSeed, arcSettings, _segmentBuffer);

            if (widthScale > 0.001f)
                line.widthMultiplier *= widthScale;
        }

        private static bool IsPlayerValidForLine(NetworkPlayer player)
        {
            if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy)
                return false;

            if (!player.IsAlive)
                return false;

            if (player.Object != null && player.Object.IsValid)
                return true;

            return player.Object == null;
        }

        private static bool IsEnemyValidForLine(NetworkEnemy enemy)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.gameObject.activeInHierarchy)
                return false;

            return enemy.IsAlive;
        }

        private static int GetVisualTargetId(NetworkBehaviour behaviour)
        {
            if (behaviour != null && behaviour.Object != null && behaviour.Object.IsValid)
                return behaviour.Object.Id.GetHashCode();

            return behaviour != null ? behaviour.GetInstanceID() : 0;
        }

        private void EnsureLinesParent()
        {
            var domeMesh = domeVisuals != null ? domeVisuals.transform.Find("DomeVisual") : null;
            if (linesParent != null && linesParent != domeMesh && linesParent != transform)
                return;

            if (linesParent == domeMesh || linesParent == transform.Find("DomeVisual"))
                linesParent = null;

            if (linesParent == null)
            {
                var existing = transform.Find("DomeConnectionLines");
                if (existing != null)
                    linesParent = existing;
                else
                {
                    var parentGo = new GameObject("DomeConnectionLines");
                    linesParent = parentGo.transform;
                    linesParent.SetParent(transform, false);
                }
            }
        }

        private LineRenderer GetOrCreateLine(int targetId, bool isEnemy)
        {
            var active = isEnemy ? _activeEnemyLines : _activeAllyLines;
            if (active.TryGetValue(targetId, out LineRenderer existing))
                return existing;

            var template = isEnemy ? enemyLineTemplate : allyLineTemplate;
            if (template == null)
                return null;

            var line = AcquireLineFromPool(isEnemy);
            if (line == null)
                return null;

            active[targetId] = line;
            return line;
        }

        private LineRenderer AcquireLineFromPool(bool isEnemy)
        {
            var pool = isEnemy ? _enemyLinePool : _allyLinePool;
            var active = isEnemy ? _activeEnemyLines : _activeAllyLines;
            var template = isEnemy ? enemyLineTemplate : allyLineTemplate;

            for (int i = 0; i < pool.Count; i++)
            {
                LineRenderer pooled = pool[i];
                if (pooled == null)
                    continue;

                bool inUse = false;
                foreach (var entry in active)
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

            EnsureLinesParent();

            var lineGo = new GameObject(isEnemy ? "DomeEnemyLine" : "DomeAllyLine");
            lineGo.transform.SetParent(linesParent, false);
            var line = lineGo.AddComponent<LineRenderer>();
            ElectricArcLineUtility.CopyLineRendererSettings(template, line);
            line.useWorldSpace = true;
            pool.Add(line);
            return line;
        }

        private static void ReleaseUnusedLines(Dictionary<int, LineRenderer> activeLines, HashSet<int> usedIds)
        {
            var toRemove = new List<int>();

            foreach (var entry in activeLines)
            {
                if (usedIds.Contains(entry.Key))
                    continue;

                if (entry.Value != null)
                    entry.Value.enabled = false;
                toRemove.Add(entry.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                activeLines.Remove(toRemove[i]);
        }

        private void ReleaseAllLines()
        {
            _usedAllyIds.Clear();
            _usedEnemyIds.Clear();

            foreach (var entry in _activeAllyLines)
            {
                if (entry.Value != null)
                    entry.Value.enabled = false;
            }

            foreach (var entry in _activeEnemyLines)
            {
                if (entry.Value != null)
                    entry.Value.enabled = false;
            }

            _activeAllyLines.Clear();
            _activeEnemyLines.Clear();
        }

        private void OnDisable()
        {
            ReleaseAllLines();
        }
    }
}

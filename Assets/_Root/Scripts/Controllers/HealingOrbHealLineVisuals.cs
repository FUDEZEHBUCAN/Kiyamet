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
        [SerializeField] private int segmentsPerLine = 18;
        [SerializeField] private float waveAmplitude = 0.28f;
        [SerializeField] private float waveFrequency = 14f;
        [SerializeField] private float waveScrollSpeed = 22f;
        [SerializeField] private float secondaryWaveAmplitude = 0.12f;
        [SerializeField] private float secondaryWaveFrequency = 31f;
        [SerializeField] private float widthPulseAmount = 0.08f;
        [SerializeField] private float widthPulseSpeed = 18f;

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

            int segmentCount = Mathf.Clamp(segmentsPerLine, 4, _segmentBuffer.Length);
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
                    PopulateElectricArc(line, orbCenter, targetPos, segmentCount, time, playerId);
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
            CopyLineRendererSettings(lineTemplate, line);
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

        private void PopulateElectricArc(
            LineRenderer line,
            Vector3 start,
            Vector3 end,
            int segmentCount,
            float time,
            int playerId)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
                return;
            }

            Vector3 direction = delta / length;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.0001f)
                perpendicular = Vector3.Cross(direction, Vector3.right);
            perpendicular.Normalize();

            Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).normalized;
            float phaseOffset = playerId * 0.173f;

            line.positionCount = segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / (float)(segmentCount - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float primary = Mathf.Sin(t * waveFrequency + time * waveScrollSpeed + phaseOffset) * waveAmplitude;
                float secondary = Mathf.Sin(t * secondaryWaveFrequency - time * (waveScrollSpeed * 0.85f) + phaseOffset * 2f)
                    * secondaryWaveAmplitude;
                float offset = (primary + secondary) * envelope;

                Vector3 swirl = perpendicular * Mathf.Sin(time * 7f + t * 9f + phaseOffset)
                    + perpendicular2 * Mathf.Cos(time * 6f + t * 8f + phaseOffset);
                _segmentBuffer[i] = basePoint + perpendicular * offset + swirl * (envelope * 0.15f);
            }

            line.SetPositions(_segmentBuffer);

            float widthPulse = 1f + Mathf.Sin(time * widthPulseSpeed + phaseOffset) * widthPulseAmount;
            line.widthMultiplier = lineTemplate.widthMultiplier * widthPulse;
        }

        private static void CopyLineRendererSettings(LineRenderer source, LineRenderer target)
        {
            target.useWorldSpace = true;
            target.loop = false;
            target.alignment = source.alignment;
            target.textureMode = source.textureMode;
            target.textureScale = source.textureScale;
            target.numCornerVertices = source.numCornerVertices;
            target.numCapVertices = source.numCapVertices;
            target.shadowBias = source.shadowBias;
            target.generateLightingData = source.generateLightingData;
            target.widthMultiplier = source.widthMultiplier;
            target.widthCurve = source.widthCurve;
            target.colorGradient = source.colorGradient;
            target.materials = source.materials;
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

using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Enemy;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Shaman ultisi: zaman distorsiyon kubbesi. Müttefiklere hasar azaltma, heal ve cooldown hızlandırma;
    /// düşmanları yavaşlatır.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeDistortionDomeZone : NetworkBehaviour
    {
        private static readonly List<TimeDistortionDomeZone> ActiveDomes = new List<TimeDistortionDomeZone>(4);

        [Header("Kubbe")]
        [SerializeField] private float radius = 8f;
        [SerializeField] private float durationSeconds = 10f;
        [SerializeField] private bool useHorizontalDistanceCheck = true;
        [SerializeField] private Transform domeVisual;
        [SerializeField] private float visualScaleMultiplier = 2f;

        [Header("Müttefik etkileri")]
        [SerializeField] [Range(0f, 1f)] private float allyDamageTakenMultiplier = 0.6f;
        [SerializeField] [Range(0f, 2f)] private float allyHealPerSecondFraction = 0.05f;
        [SerializeField] private float allyCooldownHasteMultiplier = 1.75f;

        [Header("Düşman etkileri")]
        [SerializeField] [Range(0f, 1f)] private float enemySpeedMultiplier = 0.45f;

        [Networked] private NetworkBool IsActive { get; set; }
        [Networked] private Vector3 NetCenter { get; set; }
        [Networked] private float EndSimulationTime { get; set; }
        [Networked] private float SpawnSimulationTime { get; set; }

        public float DomeSpawnSimulationTime => SpawnSimulationTime;

        private NetworkPlayer _owner;
        private TimeDistortionDomeVisuals _visuals;
        private TimeDistortionDomeConnectionLines _connectionLines;
        private readonly List<NetworkPlayer> _allyBuffer = new List<NetworkPlayer>(8);
        private readonly List<NetworkEnemy> _enemyBuffer = new List<NetworkEnemy>(32);
        private readonly HashSet<NetworkEnemy> _enemiesSlowed = new HashSet<NetworkEnemy>();
        private readonly List<NetworkEnemy> _enemiesToUnslow = new List<NetworkEnemy>(32);

        public float Radius => radius;
        public bool IsDomeActive => IsActive;

        public static float GetAllyDamageTakenMultiplier(NetworkPlayer player)
        {
            if (player == null || !player.IsAlive)
                return 1f;

            float best = 1f;
            Vector3 sample = HealingOrbProjectile.GetPlayerHealSamplePosition(player);

            for (int i = 0; i < ActiveDomes.Count; i++)
            {
                var dome = ActiveDomes[i];
                if (dome == null || !dome.IsDomeActive)
                    continue;

                if (!dome.IsWithinRadius(dome.NetCenter, sample))
                    continue;

                best = Mathf.Min(best, dome.allyDamageTakenMultiplier);
            }

            return best;
        }

        private void Awake()
        {
            _visuals = GetComponent<TimeDistortionDomeVisuals>();
            _connectionLines = GetComponent<TimeDistortionDomeConnectionLines>();
        }

        public void CollectTargetsForVisuals(List<NetworkPlayer> allies, List<NetworkEnemy> enemies)
        {
            CollectAllies(NetCenter, allies);
            CollectEnemies(NetCenter, enemies);
        }

        public void ServerInitialize(NetworkPlayer owner, Vector3 center)
        {
            if (!Object.HasStateAuthority || owner == null)
                return;

            _owner = owner;
            NetCenter = center;
            SpawnSimulationTime = Runner.SimulationTime;
            EndSimulationTime = Runner.SimulationTime + durationSeconds;
            IsActive = true;
            transform.position = center;
            RefreshVisuals(true, true);
        }

        public override void Spawned()
        {
            if (!ActiveDomes.Contains(this))
                ActiveDomes.Add(this);

            RefreshVisuals(IsActive, false);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ClearEnemySlowEffects();
            ActiveDomes.Remove(this);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !IsActive)
                return;

            if (_owner == null || !_owner.IsAlive)
            {
                DespawnDome();
                return;
            }

            if (Runner.SimulationTime >= EndSimulationTime)
            {
                DespawnDome();
                return;
            }

            float dt = Runner.DeltaTime;
            ApplyAllyEffects(NetCenter, dt);
            ApplyEnemySlow(NetCenter);
        }

        public override void Render()
        {
            if (!IsActive)
            {
                RefreshVisuals(false, false);
                return;
            }

            transform.position = NetCenter;

            if (_visuals == null)
                _visuals = GetComponent<TimeDistortionDomeVisuals>();

            if (_visuals != null)
            {
                _visuals.SetVisible(true);
                float simTime = Runner != null ? Runner.SimulationTime : Time.time;
                _visuals.TickVisuals(radius, simTime, SpawnSimulationTime);
            }
            else
            {
                UpdateVisualScale();
            }

            if (_connectionLines == null)
                _connectionLines = GetComponent<TimeDistortionDomeConnectionLines>();

            if (_connectionLines != null)
            {
                CollectTargetsForVisuals(_allyBuffer, _enemyBuffer);
                _connectionLines.UpdateConnections(_allyBuffer, _enemyBuffer);
            }
        }

        private bool IsWithinRadius(Vector3 center, Vector3 samplePosition)
        {
            if (useHorizontalDistanceCheck)
            {
                var delta = samplePosition - center;
                delta.y = 0f;
                return delta.sqrMagnitude <= radius * radius;
            }

            return (samplePosition - center).sqrMagnitude <= radius * radius;
        }

        private void ApplyAllyEffects(Vector3 center, float deltaTime)
        {
            CollectAllies(center, _allyBuffer);

            for (int i = 0; i < _allyBuffer.Count; i++)
            {
                var ally = _allyBuffer[i];
                if (ally == null || !ally.IsAlive)
                    continue;

                if (allyHealPerSecondFraction > 0f && deltaTime > 0f)
                {
                    float healAmount = ally.MaxHealth * allyHealPerSecondFraction * deltaTime;
                    ally.RequestHeal(healAmount);
                }

                if (allyCooldownHasteMultiplier > 1.001f && deltaTime > 0f)
                {
                    var melee = ally.GetComponent<MeleeController>();
                    if (melee != null)
                        melee.ApplyCooldownHaste(allyCooldownHasteMultiplier, deltaTime);

                    var signature = ally.GetComponent<SupportSignatureSkillController>();
                    if (signature != null)
                        signature.ApplyCooldownHaste(allyCooldownHasteMultiplier, deltaTime);
                }
            }
        }

        private void CollectAllies(Vector3 center, List<NetworkPlayer> results)
        {
            results.Clear();

            var candidates = new HashSet<NetworkPlayer>();

            if (Runner != null)
            {
                foreach (var playerRef in Runner.ActivePlayers)
                {
                    var playerObject = Runner.GetPlayerObject(playerRef);
                    if (playerObject == null)
                        continue;

                    var player = playerObject.GetComponent<NetworkPlayer>();
                    if (player != null)
                        candidates.Add(player);
                }
            }

            var scenePlayers = FindObjectsOfType<NetworkPlayer>();
            for (int i = 0; i < scenePlayers.Length; i++)
            {
                if (scenePlayers[i] != null)
                    candidates.Add(scenePlayers[i]);
            }

            foreach (var player in candidates)
            {
                if (player == null || !player.IsAlive)
                    continue;

                if (!IsWithinRadius(center, HealingOrbProjectile.GetPlayerHealSamplePosition(player)))
                    continue;

                results.Add(player);
            }
        }

        private void ApplyEnemySlow(Vector3 center)
        {
            CollectEnemies(center, _enemyBuffer);

            _enemiesToUnslow.Clear();
            foreach (var enemy in _enemiesSlowed)
            {
                if (enemy == null || !enemy.IsAlive || !_enemyBuffer.Contains(enemy))
                    _enemiesToUnslow.Add(enemy);
            }

            for (int i = 0; i < _enemiesToUnslow.Count; i++)
            {
                _enemiesToUnslow[i].ClearTimeDistortionSlow();
                _enemiesSlowed.Remove(_enemiesToUnslow[i]);
            }

            for (int i = 0; i < _enemyBuffer.Count; i++)
            {
                var enemy = _enemyBuffer[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.SetTimeDistortionSlow(enemySpeedMultiplier);
                _enemiesSlowed.Add(enemy);
            }
        }

        private void CollectEnemies(Vector3 center, List<NetworkEnemy> results)
        {
            results.Clear();
            var enemies = FindObjectsOfType<NetworkEnemy>();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (!IsWithinRadius(center, TimeDistortionDomeConnectionLines.GetEnemyConnectionBasePosition(enemy)))
                    continue;

                results.Add(enemy);
            }
        }

        private void ClearEnemySlowEffects()
        {
            foreach (var enemy in _enemiesSlowed)
            {
                if (enemy != null)
                    enemy.ClearTimeDistortionSlow();
            }

            _enemiesSlowed.Clear();
        }

        private void RefreshVisuals(bool visible, bool playSpawnAnimation)
        {
            if (_visuals == null)
                _visuals = GetComponent<TimeDistortionDomeVisuals>();

            if (_connectionLines == null)
                _connectionLines = GetComponent<TimeDistortionDomeConnectionLines>();

            if (_connectionLines != null)
                _connectionLines.SetVisible(visible);

            if (_visuals != null)
            {
                _visuals.SetVisible(visible);
                if (visible && playSpawnAnimation)
                    _visuals.PlaySpawnAnimation(radius, SpawnSimulationTime);
                return;
            }

            UpdateVisualScale();
            if (domeVisual != null)
            {
                domeVisual.gameObject.SetActive(visible);
                domeVisual.localPosition = Vector3.zero;
            }
        }

        private void DespawnDome()
        {
            if (!Object.HasStateAuthority)
                return;

            IsActive = false;
            RefreshVisuals(false, false);
            ClearEnemySlowEffects();

            if (_owner != null && _owner.IsUltimateActive)
                _owner.NotifySupportUltimateEnded();

            if (Runner != null && Object != null && Object.IsValid)
                Runner.Despawn(Object);
        }

        private void UpdateVisualScale()
        {
            if (domeVisual == null)
                return;

            float diameter = radius * visualScaleMultiplier;
            domeVisual.localScale = Vector3.one * diameter;
        }

        private void OnDisable()
        {
            ActiveDomes.Remove(this);
            ClearEnemySlowEffects();
        }
    }
}

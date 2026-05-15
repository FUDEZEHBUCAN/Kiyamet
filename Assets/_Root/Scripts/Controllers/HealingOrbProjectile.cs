using System.Collections.Generic;
using Fusion;
using UnityEngine;
using _Root.Scripts.Network;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Shaman imza yeteneği sihir topu: uçuş sonunda yarıçap içindeki tüm oyunculara
    /// <see cref="healFractionOfMaxHealth"/> oranında can verir (sunucu otoritesi).
    /// </summary>
    [DisallowMultipleComponent]
    public class HealingOrbProjectile : NetworkBehaviour
    {
        [Header("Hareket")]
        [SerializeField] private float travelSpeed = 14f;
        [SerializeField] private float maxTravelDistance = 22f;

        [Header("Patlama / iyileştirme")]
        [SerializeField] private float healRadius = 5f;
        [SerializeField] [Range(0f, 1f)] private float healFractionOfMaxHealth = 0.3f;
        [SerializeField] private LayerMask playerLayers = ~0;

        [Networked] private Vector3 NetPosition { get; set; }
        [Networked] private Vector3 MoveDirection { get; set; }
        [Networked] private Vector3 SpawnOrigin { get; set; }
        [Networked] private NetworkBool HasExploded { get; set; }

        private readonly HashSet<NetworkPlayer> _healedPlayers = new HashSet<NetworkPlayer>();

        public void ServerConfigure(Vector3 startPosition, Vector3 direction)
        {
            if (!Object.HasStateAuthority)
                return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            SpawnOrigin = startPosition;
            NetPosition = startPosition;
            MoveDirection = dir;
            transform.position = startPosition;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || HasExploded)
                return;

            float dt = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
            NetPosition += MoveDirection * (travelSpeed * dt);

            if (Vector3.Distance(NetPosition, SpawnOrigin) >= maxTravelDistance)
            {
                ExplodeAt(NetPosition);
            }
        }

        public override void Render()
        {
            transform.position = NetPosition;
        }

        private void ExplodeAt(Vector3 center)
        {
            if (HasExploded)
                return;
            HasExploded = true;

            _healedPlayers.Clear();
            Collider[] hits = Physics.OverlapSphere(center, healRadius, playerLayers, QueryTriggerInteraction.Collide);
            foreach (var col in hits)
            {
                var player = col.GetComponentInParent<NetworkPlayer>();
                if (player == null || !player.IsAlive || _healedPlayers.Contains(player))
                    continue;

                _healedPlayers.Add(player);
                float healAmount = player.MaxHealth * healFractionOfMaxHealth;
                player.Heal(healAmount);
            }

            if (Runner != null && Object != null && Object.IsValid)
                Runner.Despawn(Object);
        }
    }
}

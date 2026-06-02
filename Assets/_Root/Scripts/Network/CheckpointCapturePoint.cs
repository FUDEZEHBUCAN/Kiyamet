using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Network
{
    /// <summary>
    /// Sahneye yerleştirilen checkpoint hacmi. Herhangi bir canlı oyuncu girince aşama kaydedilir (sunucu).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CheckpointCapturePoint : MonoBehaviour
    {
        [SerializeField] private int stage = 1;
        [Tooltip("Boşsa bu objenin transform'u kullanılır.")]
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private bool ignoreDeadPlayers = true;

        public int Stage => stage;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnEnable()
        {
            var manager = NetworkCheckpointManager.FindActiveInstance();
            manager?.RegisterCapturePoint(this);
        }

        private void OnDisable()
        {
            var manager = NetworkCheckpointManager.FindActiveInstance();
            manager?.UnregisterCapturePoint(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetValidPlayer(other, out _))
                return;

            NetworkCheckpointManager.TryCaptureStageFromWorld(stage);
        }

        public void GetRespawnPose(out Vector3 position, out Quaternion rotation)
        {
            var t = respawnPoint != null ? respawnPoint : transform;
            position = t.position;
            rotation = t.rotation;
        }

        private bool TryGetValidPlayer(Collider other, out NetworkPlayer player)
        {
            player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid)
                return false;

            if (ignoreDeadPlayers && !player.IsAlive)
                return false;

            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stage = Mathf.Max(1, stage);
        }

        private void OnDrawGizmosSelected()
        {
            GetRespawnPose(out var pos, out var rot);
            Gizmos.color = new Color(0.2f, 0.95f, 0.45f, 0.85f);
            Gizmos.DrawWireSphere(pos, 0.65f);
            Gizmos.DrawLine(pos, pos + rot * Vector3.forward * 1.2f);
        }
#endif
    }
}

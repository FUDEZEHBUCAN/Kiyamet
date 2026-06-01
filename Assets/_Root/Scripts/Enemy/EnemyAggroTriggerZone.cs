using Fusion;
using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Enemy
{
    /// <summary>
    /// İlk kez bir oyuncu trigger'a girdiğinde seçili düşmanların oyuncu taraması kalıcı olarak açılır.
    /// Oyuncular alandan çıksa bile tarama kapanmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EnemyAggroTriggerZone : MonoBehaviour
    {
        [Header("Controlled enemies")]
        [Tooltip("Bu alana bağlı düşmanlar. Inspector'dan sahne/prefab referanslarını sürükleyin.")]
        [SerializeField] private NetworkEnemy[] controlledEnemies;

        [Header("Options")]
        [SerializeField] private bool disableDetectionOnStart = true;
        [Tooltip("Ölü oyuncular trigger sayılmaz.")]
        [SerializeField] private bool ignoreDeadPlayers = true;

        private bool _aggroActivated;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[EnemyAggroTriggerZone] '{name}' collider should be Is Trigger.", this);
        }

        private void Start()
        {
            if (!TryResolveServerRunner(out _))
                return;

            if (disableDetectionOnStart)
                ApplyDetectionToEnemies(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_aggroActivated)
                return;

            if (!TryResolveServerRunner(out _))
                return;

            if (!TryGetPlayerObject(other, out _))
                return;

            _aggroActivated = true;
            ApplyDetectionToEnemies(true);
        }

        private bool TryGetPlayerObject(Collider other, out NetworkObject playerObject)
        {
            playerObject = null;
            var player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || player.Object == null || !player.Object.IsValid)
                return false;

            if (ignoreDeadPlayers && !player.IsAlive)
                return false;

            playerObject = player.Object;
            return true;
        }

        private void ApplyDetectionToEnemies(bool enabled)
        {
            if (controlledEnemies == null)
                return;

            for (int i = 0; i < controlledEnemies.Length; i++)
            {
                var enemy = controlledEnemies[i];
                if (enemy == null || enemy.Object == null || !enemy.Object.IsValid)
                    continue;

                if (!enemy.Object.HasStateAuthority)
                    continue;

                enemy.SetPlayerDetectionEnabled(enabled);
            }
        }

        private bool TryResolveServerRunner(out NetworkRunner runner)
        {
            runner = null;

            if (controlledEnemies != null)
            {
                for (int i = 0; i < controlledEnemies.Length; i++)
                {
                    var enemy = controlledEnemies[i];
                    if (enemy == null || enemy.Object == null || !enemy.Object.IsValid)
                        continue;

                    runner = enemy.Runner;
                    if (runner != null && runner.IsServer)
                        return true;
                }
            }

            foreach (var activeRunner in NetworkRunner.Instances)
            {
                if (activeRunner == null || !activeRunner.IsRunning || !activeRunner.IsServer)
                    continue;

                runner = activeRunner;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider>();
            if (col == null)
                return;

            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.25f);
            var matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
            {
                Gizmos.matrix = matrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = matrix;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
#endif
    }
}

using UnityEngine;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Boss uyanınca arena çıkış duvarlarını tüm clientlarda aktif eder.
    /// Duvar objelerini Inspector'dan sürükleyin; başlangıçta kapalı tutun.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossArenaBarrierController : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] private NetworkBoss boss;

        [Header("Barriers")]
        [Tooltip("Boss uyandığında aktif edilecek duvar/kapı objeleri.")]
        [SerializeField] private GameObject[] barrierObjects;

        [Header("Options")]
        [SerializeField] private bool deactivateOnStart = true;

        private bool _lastAppliedState;

        private void Awake()
        {
            if (boss == null)
                boss = FindFirstObjectByType<NetworkBoss>();
        }

        private void Start()
        {
            if (deactivateOnStart)
                ApplyBarriers(false);
        }

        private void Update()
        {
            if (boss == null || boss.Object == null || !boss.Object.IsValid)
                return;

            bool shouldBeActive = boss.ArenaBarriersActive;
            if (shouldBeActive == _lastAppliedState)
                return;

            ApplyBarriers(shouldBeActive);
            _lastAppliedState = shouldBeActive;
        }

        private void ApplyBarriers(bool active)
        {
            if (barrierObjects == null)
                return;

            for (int i = 0; i < barrierObjects.Length; i++)
            {
                if (barrierObjects[i] != null)
                    barrierObjects[i].SetActive(active);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (boss == null)
                boss = FindFirstObjectByType<NetworkBoss>();
        }
#endif
    }
}

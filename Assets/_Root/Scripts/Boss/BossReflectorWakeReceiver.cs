using UnityEngine;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Reflector ışını boss gözüne (laserPoint) değdiğinde uyku uyanışı veya taşlaşma geri dönüşüne katkı sağlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossReflectorWakeReceiver : MonoBehaviour
    {
        [SerializeField] private NetworkBoss boss;
        [SerializeField] private Collider eyeCollider;
        [SerializeField, Range(1f, 89f)] private float hitAcceptanceAngle = 35f;

        private void Awake()
        {
            if (boss == null)
                boss = GetComponentInParent<NetworkBoss>();

            if (eyeCollider == null)
                eyeCollider = GetComponent<Collider>();
        }

        public bool CanReceiveLightHit(RaycastHit hit, Vector3 incomingDirection)
        {
            if (hit.collider == null || eyeCollider == null)
                return false;

            if (hit.collider != eyeCollider)
                return false;

            incomingDirection.Normalize();
            float minFacing = Mathf.Cos(hitAcceptanceAngle * Mathf.Deg2Rad);
            return Vector3.Dot(hit.normal, -incomingDirection) >= minFacing;
        }

        public void NotifyLightExposure(float deltaTime)
        {
            if (boss == null)
                boss = GetComponentInParent<NetworkBoss>();

            if (boss == null || deltaTime <= 0f)
                return;

            boss.NotifyReflectorLightExposure(deltaTime);
        }
    }
}

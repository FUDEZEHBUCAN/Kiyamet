using UnityEngine;
using _Root.Scripts.Boss;
using _Root.Scripts.Enemy;

namespace _Root.Scripts.Combat
{
    /// <summary>
    /// Oyuncu saldırıları NetworkEnemy ve NetworkBoss için ortak çözümleme.
    /// </summary>
    public readonly struct CombatDamageTarget
    {
        public NetworkEnemy Enemy { get; }
        public NetworkBoss Boss { get; }
        public bool IsValid => Enemy != null || Boss != null;

        public bool IsAlive
        {
            get
            {
                if (Boss != null)
                    return Boss.IsAlive;
                if (Enemy != null)
                    return Enemy.IsAlive;
                return false;
            }
        }

        public bool IsEliteEnemy => Enemy != null && Enemy.IsEliteEnemy;
        public bool HasActiveKnockback => Enemy != null && Enemy.HasActiveKnockback;

        public CombatDamageTarget(NetworkEnemy enemy, NetworkBoss boss)
        {
            Enemy = enemy;
            Boss = boss;
        }

        public static bool TryFromCollider(Collider col, out CombatDamageTarget target)
        {
            target = default;
            if (col == null)
                return false;

            var boss = col.GetComponentInParent<NetworkBoss>();
            if (boss != null)
            {
                target = new CombatDamageTarget(null, boss);
                return true;
            }

            var enemy = col.GetComponentInParent<NetworkEnemy>();
            if (enemy != null)
            {
                target = new CombatDamageTarget(enemy, null);
                return true;
            }

            return false;
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (Boss != null)
                Boss.TakeDamage(damage, hitPoint, hitNormal);
            else if (Enemy != null)
                Enemy.TakeDamage(damage, hitPoint, hitNormal);
        }
    }
}

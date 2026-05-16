namespace _Root.Scripts.Enums
{
    public enum CameraShakeType
    {
        MeleeAttackSwing,
        MeleeAttackHit,
        DamageTaken,
        DamageBlocked,
        HeavyAttackTaken,
        DoorBreak,
        HealingOrbSpawn,
        /// <summary>Support ulti invuln — sürekli hafif süzülme (StartSupportUltimateFloatShake kullan).</summary>
        SupportUltimateFloat
    }
}
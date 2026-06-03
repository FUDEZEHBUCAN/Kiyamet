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
        /// <summary>Tank ulti açılış darbesi (güç patlaması hissi).</summary>
        TankUltimateActivate,
        /// <summary>Support ulti invuln — sürekli hafif süzülme (StartSupportUltimateFloatShake kullan).</summary>
        SupportUltimateFloat,
        /// <summary>Duelist Mirage Step başlangıç darbesi.</summary>
        DuelistMirageStepStart,
        /// <summary>Duelist Mirage Step spin finale.</summary>
        DuelistMirageStepFinale
    }
}
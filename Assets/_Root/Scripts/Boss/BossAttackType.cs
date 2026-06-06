namespace _Root.Scripts.Boss
{
    public enum BossAttackType : byte
    {
        None = 0,
        Normal = 1,
        Heavy = 2,
        /// <summary>Göz lazeri — Angry anim + (ileride) hasar.</summary>
        EyeLaser = 3,
        JumpAttack = 4,
        /// <summary>Rush — hedefe koşup yumruk (Mutant Right Punch).</summary>
        RushAttack = 5
    }
}

namespace _Root.Scripts.Boss
{
    /// <summary>Boss savaşı kamera sarsıntı presetleri — devasa tehdit hissi.</summary>
    public enum BossCameraShakeType : byte
    {
        None = 0,
        /// <summary>İlk hedef kilidi — uzaktan bile hissedilen kükreme.</summary>
        Aggro,
        /// <summary>Normal melee windup.</summary>
        MeleeWindup,
        /// <summary>Heavy melee windup.</summary>
        HeavyMeleeWindup,
        /// <summary>Boss vuruşu oyuncuya isabet (local).</summary>
        HitPlayer,
        /// <summary>Jump inişi — yer sarsıntısı.</summary>
        JumpLanding,
        /// <summary>Lazer şarj uyarısı.</summary>
        LaserCharge,
        /// <summary>Lazer ateşi.</summary>
        LaserBeam,
        /// <summary>Rush koşusu (hafif).</summary>
        RushRun,
        /// <summary>Rush çarpma.</summary>
        RushImpact,
        /// <summary>Taşlaşma / korku fazı.</summary>
        Petrify,
        /// <summary>Boss ölümü.</summary>
        Death,
        /// <summary>Locomotion adımı — yakındaki oyuncular.</summary>
        Footstep,
        /// <summary>Uyku uyanışı — göz ışığı tutulurken sürekli sarsıntı.</summary>
        WakeLight,
    }
}

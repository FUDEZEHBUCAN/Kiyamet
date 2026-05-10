using _Root.Scripts.Enums;
using _Root.Scripts.Network;

namespace _Root.Scripts.Roles
{
    /// <summary>
    /// Ortak varsayılanlar — şu anki oyunda tank dahil tüm roller aynı yetenek setini kullanır.
    /// </summary>
    public abstract class CharacterRoleRulesBase : ICharacterRoleRules
    {
        public abstract PlayerRoleType RoleType { get; }

        public virtual bool CanDash(NetworkPlayer player) =>
            player != null && player.IsAlive;

        public virtual bool CanBlock(NetworkPlayer player) =>
            player != null && player.IsAlive;

        public virtual bool CanMelee(NetworkPlayer player) =>
            player != null && player.IsAlive;

        public virtual bool CanUseRangedWeapon(NetworkPlayer player) =>
            player != null && player.IsAlive;

        public virtual bool CanJump(NetworkPlayer player) =>
            player != null && player.IsAlive;

        public virtual bool UsesKeyboardCharacterRotation => false;
    }
}

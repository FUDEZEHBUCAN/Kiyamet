using _Root.Scripts.Enums;

namespace _Root.Scripts.Roles
{
    public sealed class DuelistCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly DuelistCharacterRoleRules Instance = new DuelistCharacterRoleRules();

        private DuelistCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Duelist;

        /// <summary>Tank / Support ile aynı: sabitken kamera free look, hareket kamera yönüne göre.</summary>
        public override bool UsesKeyboardCharacterRotation => true;
    }
}

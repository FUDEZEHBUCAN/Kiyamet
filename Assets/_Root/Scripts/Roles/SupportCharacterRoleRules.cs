using _Root.Scripts.Enums;
using _Root.Scripts.Network;

namespace _Root.Scripts.Roles
{
    public sealed class SupportCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly SupportCharacterRoleRules Instance = new SupportCharacterRoleRules();

        private SupportCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Support;

        /// <summary>Tank ile aynı: sabitken kamera free look, hareket kamera yönüne göre.</summary>
        public override bool UsesKeyboardCharacterRotation => true;

        public override bool CanDash(NetworkPlayer player) => false;

        public override bool UsesDashAsSignature => false;
    }
}

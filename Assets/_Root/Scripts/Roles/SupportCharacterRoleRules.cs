using _Root.Scripts.Enums;

namespace _Root.Scripts.Roles
{
    public sealed class SupportCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly SupportCharacterRoleRules Instance = new SupportCharacterRoleRules();

        private SupportCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Support;
    }
}

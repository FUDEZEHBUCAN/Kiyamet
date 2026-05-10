using _Root.Scripts.Enums;

namespace _Root.Scripts.Roles
{
    public sealed class RangedCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly RangedCharacterRoleRules Instance = new RangedCharacterRoleRules();

        private RangedCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Ranged;
    }
}

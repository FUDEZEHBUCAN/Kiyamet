using _Root.Scripts.Enums;

namespace _Root.Scripts.Roles
{
    public sealed class MagicianCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly MagicianCharacterRoleRules Instance = new MagicianCharacterRoleRules();

        private MagicianCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Magician;
    }
}

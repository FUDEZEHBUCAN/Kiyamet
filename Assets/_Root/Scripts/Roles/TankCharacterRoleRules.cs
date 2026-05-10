using _Root.Scripts.Enums;

namespace _Root.Scripts.Roles
{
    public sealed class TankCharacterRoleRules : CharacterRoleRulesBase
    {
        public static readonly TankCharacterRoleRules Instance = new TankCharacterRoleRules();

        private TankCharacterRoleRules() { }

        public override PlayerRoleType RoleType => PlayerRoleType.Tank;

        public override bool UsesKeyboardCharacterRotation => true;
    }
}

using _Root.Scripts.Enums;
using UnityEngine;

namespace _Root.Scripts.Roles
{
    /// <summary>
    /// Rol tipine göre kural kümesi döndürür (allocation yok).
    /// </summary>
    public static class CharacterRoleRulesProvider
    {
        private static readonly ICharacterRoleRules[] Rules =
        {
            TankCharacterRoleRules.Instance,
            RangedCharacterRoleRules.Instance,
            SupportCharacterRoleRules.Instance,
            MagicianCharacterRoleRules.Instance,
            DuelistCharacterRoleRules.Instance
        };

        public static ICharacterRoleRules Get(PlayerRoleType roleType)
        {
            int index = (int)roleType;
            if (index >= 0 && index < Rules.Length)
                return Rules[index];

            Debug.LogWarning($"[CharacterRoleRulesProvider] Bilinmeyen rol ({roleType}), Tank kuralları kullanılıyor.");
            return TankCharacterRoleRules.Instance;
        }
    }
}

using System.Collections.Generic;
using _Root.Scripts.Enums;

namespace _Root.Scripts.UI
{
    public readonly struct RoleSkillCheatsheetEntry
    {
        public readonly string Name;
        public readonly string Binding;
        public readonly string Description;

        public RoleSkillCheatsheetEntry(string name, string binding, string description)
        {
            Name = name;
            Binding = binding;
            Description = description;
        }
    }

    public static class RoleSkillCheatsheetData
    {
        public static string GetRoleTitle(PlayerRoleType role) =>
            role switch
            {
                PlayerRoleType.Support => "Support",
                PlayerRoleType.Duelist => "Duelist",
                _ => "Tank"
            };

        public static IReadOnlyList<RoleSkillCheatsheetEntry> Get(PlayerRoleType role)
        {
            var list = new List<RoleSkillCheatsheetEntry>(7)
            {
                new("Melee", "LMB", "Close-range swing; damages enemies in front of you."),
                new("Dodge", "Alt",
                    "Quick roll in your movement direction (or backward if standing still) to evade attacks."),
            };

            if (role == PlayerRoleType.Support)
            {
                list.Add(new("Healing orb", "E",
                    "Launch a magic orb; allies in its radius are healed continuously until it fades."));
                list.Add(new("Ultimate", "X",
                    "Summon a time dome: allies take less damage, heal over time, and cooldowns recharge faster; enemies inside are slowed. Brief invulnerability while casting."));
                list.Add(new("Interact", "F",
                    "Interact with or pick up nearby objects (press F again to release)."));
            }
            else if (role == PlayerRoleType.Duelist)
            {
                list.Add(new("Shadow Dash", "E",
                    "Quick dash that slices enemies in your path for medium damage; 25% critical hit chance."));
                list.Add(new("Ultimate — Mirage Step", "X",
                    "Blink between enemies within 6 m, striking up to 6 targets, then spin-hit all nearby foes."));
                list.Add(new("Interact", "F",
                    "Interact with or pick up nearby objects (press F again to release)."));
            }
            else
            {
                list.Add(new("Block", "RMB",
                    "Hold to block and absorb the incoming damage."));
                list.Add(new("Dash", "E",
                    "Quick forward dash and apply knock back to enemies to reposition or escape."));
                list.Add(new("Ultimate", "X",
                    "BERSERK; boosts your damage and reduce taken damages for a short time."));
                list.Add(new("Interact", "F",
                    "Interact with or pick up nearby objects (press F again to release)."));
            }

            return list;
        }
    }
}

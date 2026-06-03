using _Root.Scripts.Enums;

namespace _Root.Scripts.Network.Lobby
{
    /// <summary>Playtest lobisinde seçilebilir roller.</summary>
    public static class PlaytestLobbyRoles
    {
        public static bool IsLobbySelectable(PlayerRoleType role) =>
            role == PlayerRoleType.Tank
            || role == PlayerRoleType.Support
            || role == PlayerRoleType.Duelist;

        public static string GetDisplayName(PlayerRoleType role) =>
            role switch
            {
                PlayerRoleType.Support => "Support",
                PlayerRoleType.Duelist => "Duelist",
                _ => "Tank"
            };
    }
}

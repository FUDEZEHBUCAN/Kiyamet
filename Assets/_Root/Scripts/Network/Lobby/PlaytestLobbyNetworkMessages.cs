using System.Collections.Generic;
using _Root.Scripts.Enums;
using Fusion;
using Fusion.Sockets;

namespace _Root.Scripts.Network.Lobby
{
    /// <summary>Playtest lobby reliable data messages (host-authoritative role locks).</summary>
    public static class PlaytestLobbyNetworkMessages
    {
        public static readonly ReliableKey ReliableKey = ReliableKey.FromInts(0, 0, 0, 20260516);

        private const byte OpLockRole = 1;
        private const byte OpSyncState = 2;
        private const byte OpLockDenied = 3;
        private const byte OpGameStarted = 4;
        private const byte RoleNone = 255;

        public static byte[] CreateLockRoleRequest(PlayerRoleType role) =>
            new[] { OpLockRole, (byte)role };

        public static bool TryParseLockRoleRequest(byte[] data, out PlayerRoleType role)
        {
            role = PlayerRoleType.Tank;
            if (data == null || data.Length < 2 || data[0] != OpLockRole)
                return false;

            role = (PlayerRoleType)data[1];
            return role == PlayerRoleType.Tank || role == PlayerRoleType.Support;
        }

        public static byte[] CreateSyncState(IReadOnlyDictionary<PlayerRef, PlayerRoleType> lockedRoles,
            IEnumerable<PlayerRef> activePlayers)
        {
            var list = new List<PlayerRef>();
            foreach (var p in activePlayers)
                list.Add(p);

            var buffer = new byte[2 + list.Count * 5];
            buffer[0] = OpSyncState;
            buffer[1] = (byte)list.Count;
            var offset = 2;
            foreach (var player in list)
            {
                WriteInt32(buffer, offset, player.RawEncoded);
                offset += 4;
                buffer[offset++] = lockedRoles.TryGetValue(player, out var role)
                    ? (byte)role
                    : RoleNone;
            }

            return buffer;
        }

        public static bool TryParseSyncState(byte[] data, out Dictionary<PlayerRef, PlayerRoleType> lockedRoles)
        {
            lockedRoles = new Dictionary<PlayerRef, PlayerRoleType>();
            if (data == null || data.Length < 2 || data[0] != OpSyncState)
                return false;

            int count = data[1];
            int offset = 2;
            for (int i = 0; i < count; i++)
            {
                if (offset + 5 > data.Length)
                    return false;

                int encoded = ReadInt32(data, offset);
                offset += 4;
                byte roleByte = data[offset++];

                if (roleByte == RoleNone)
                    continue;

                var role = (PlayerRoleType)roleByte;
                if (role != PlayerRoleType.Tank && role != PlayerRoleType.Support)
                    continue;

                lockedRoles[PlayerRef.FromEncoded(encoded)] = role;
            }

            return true;
        }

        public static byte[] CreateLockDenied(string reason)
        {
            var reasonBytes = System.Text.Encoding.UTF8.GetBytes(reason ?? string.Empty);
            var buffer = new byte[2 + reasonBytes.Length];
            buffer[0] = OpLockDenied;
            buffer[1] = (byte)reasonBytes.Length;
            if (reasonBytes.Length > 0)
                System.Array.Copy(reasonBytes, 0, buffer, 2, reasonBytes.Length);
            return buffer;
        }

        public static bool TryParseLockDenied(byte[] data, out string reason)
        {
            reason = string.Empty;
            if (data == null || data.Length < 2 || data[0] != OpLockDenied)
                return false;

            int len = data[1];
            if (len <= 0 || data.Length < 2 + len)
                return true;

            reason = System.Text.Encoding.UTF8.GetString(data, 2, len);
            return true;
        }

        public static byte[] CreateGameStarted() => new[] { OpGameStarted };

        public static bool IsGameStarted(byte[] data) =>
            data != null && data.Length >= 1 && data[0] == OpGameStarted;

        public static bool IsSyncState(byte[] data) => data != null && data.Length > 0 && data[0] == OpSyncState;
        public static bool IsLockDenied(byte[] data) => data != null && data.Length > 0 && data[0] == OpLockDenied;

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static int ReadInt32(byte[] buffer, int offset) =>
            buffer[offset]
            | (buffer[offset + 1] << 8)
            | (buffer[offset + 2] << 16)
            | (buffer[offset + 3] << 24);
    }
}

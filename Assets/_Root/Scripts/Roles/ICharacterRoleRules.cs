using _Root.Scripts.Enums;
using _Root.Scripts.Network;

namespace _Root.Scripts.Roles
{
    /// <summary>
    /// Rol bazlı izinler ve ileride rol özel mantığı (hareket/saldırı/skill ayrımı) için sözleşme.
    /// Varsayılan uygulamalar mevcut tank benzeri davranışı korur; alt rollerde override edilir.
    /// </summary>
    public interface ICharacterRoleRules
    {
        PlayerRoleType RoleType { get; }

        bool CanDash(NetworkPlayer player);
        bool CanDodge(NetworkPlayer player);
        bool CanBlock(NetworkPlayer player);
        bool CanMelee(NetworkPlayer player);
        bool CanUseRangedWeapon(NetworkPlayer player);
        bool CanJump(NetworkPlayer player);

        /// <summary>
        /// True ise gövde mouse ile dönmez; yatay bakış kamerada kalır, W/S ileri-geri, A/D yaw döndürür.
        /// </summary>
        bool UsesKeyboardCharacterRotation { get; }

        /// <summary>
        /// False ise dash girişi tank dash'ı yerine rolün imza yeteneğini çalıştırır.
        /// </summary>
        bool UsesDashAsSignature { get; }
    }
}

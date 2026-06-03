namespace _Root.Scripts.Enums
{
    /// <summary>
    /// Oyuncu karakter sınıfları. Her prefab/CharacterData tek bir rol ile eşlenir;
    /// movement, attack ve skill farkları <see cref="Roles.ICharacterRoleRules"/> üzerinden genişletilir.
    /// </summary>
    public enum PlayerRoleType
    {
        Tank = 0,
        Ranged = 1,
        Support = 2,
        Magician = 3,
        Duelist = 4
    }
}

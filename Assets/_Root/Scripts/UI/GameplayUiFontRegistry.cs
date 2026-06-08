using TMPro;
using UnityEngine;

namespace _Root.Scripts.UI
{
    [CreateAssetMenu(fileName = "GameplayUiFontRegistry", menuName = "Game/UI Font Registry")]
    public class GameplayUiFontRegistry : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset tmpFont;
        [SerializeField] private Font legacyFont;

        public TMP_FontAsset TmpFont => tmpFont;
        public Font LegacyFont => legacyFont;
    }
}

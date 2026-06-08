using TMPro;
using UnityEngine;

namespace _Root.Scripts.UI
{
    /// <summary>
    /// Runtime oluşturulan IMGUI ve TMP metinleri için paylaşılan Norse font erişimi.
    /// </summary>
    public static class GameplayUiFonts
    {
        private const string RegistryResourcePath = "GameplayUiFontRegistry";

        private static GameplayUiFontRegistry _registry;
        private static bool _registryLookupDone;

        public static TMP_FontAsset Tmp
        {
            get
            {
                var registry = LoadRegistry();
                if (registry != null && registry.TmpFont != null)
                    return registry.TmpFont;

                return TMP_Settings.defaultFontAsset;
            }
        }

        public static Font LegacyGui
        {
            get
            {
                var registry = LoadRegistry();
                if (registry != null && registry.LegacyFont != null)
                    return registry.LegacyFont;

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                    return font;

                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
        }

        public static void ApplyTo(TextMeshProUGUI text)
        {
            if (text == null)
                return;

            var font = Tmp;
            if (font != null)
                text.font = font;
        }

        private static GameplayUiFontRegistry LoadRegistry()
        {
            if (_registryLookupDone)
                return _registry;

            _registryLookupDone = true;
            _registry = Resources.Load<GameplayUiFontRegistry>(RegistryResourcePath);

            if (_registry == null)
            {
                Debug.LogWarning(
                    $"[GameplayUiFonts] '{RegistryResourcePath}' Resources içinde bulunamadı. " +
                    "Assets/_Root/Resources/GameplayUiFontRegistry.asset oluşturulmalı.");
            }

            return _registry;
        }
    }
}

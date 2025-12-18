using Fusion;
using UnityEngine;

namespace _Root.Scripts.Network
{
    /// <summary>
    /// Runtime'da network durumunu ekranda gösterir (debug amaçlı)
    /// </summary>
    public class NetworkDebugUI : MonoBehaviour
    {
        private NetworkRunner _runner;
        private GUIStyle _style;

        void OnGUI()
        {
            if (_runner == null)
            {
                _runner = FindObjectOfType<NetworkRunner>();
                if (_runner == null) return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                _style.normal.textColor = Color.white;
            }

            float y = 10;
            float lineHeight = 25;

            // Runner info
            GUI.Label(new Rect(10, y, 600, lineHeight), $"=== Network Debug ===", _style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 600, lineHeight), $"IsServer: {_runner.IsServer}", _style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 600, lineHeight), $"IsClient: {_runner.IsClient}", _style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 600, lineHeight), $"LocalPlayer: {_runner.LocalPlayer}", _style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 600, lineHeight), $"SessionName: {_runner.SessionInfo?.Name ?? "N/A"}", _style);
            y += lineHeight;

            y += lineHeight; // Boşluk

            // Tüm NetworkPlayer'ları listele
            GUI.Label(new Rect(10, y, 600, lineHeight), $"=== Players ===", _style);
            y += lineHeight;

            var players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.Object == null) continue;

                // Health bilgisi
                float health = player.CurrentHealth;
                float maxHealth = player.MaxHealth;
                float healthPercent = maxHealth > 0 ? (health / maxHealth) * 100f : 0f;
                
                string playerInfo = $"Player {player.Object.InputAuthority}: " +
                                   $"InputAuth={player.Object.HasInputAuthority}, " +
                                   $"StateAuth={player.Object.HasStateAuthority}, " +
                                   $"Health={health:F1}/{maxHealth:F1} ({healthPercent:F0}%), " +
                                   $"Pos={player.transform.position:F1}";
                
                // Local player yeşil, remote player sarı
                // Health'e göre renk değiştir (düşük health = kırmızı)
                Color textColor = player.Object.HasInputAuthority ? Color.green : Color.yellow;
                if (healthPercent < 30f)
                {
                    textColor = Color.red; // Düşük health = kırmızı
                }
                else if (healthPercent < 50f)
                {
                    textColor = Color.Lerp(Color.red, textColor, healthPercent / 50f); // Kırmızıdan sarıya geçiş
                }
                
                _style.normal.textColor = textColor;
                GUI.Label(new Rect(10, y, 900, lineHeight), playerInfo, _style);
                y += lineHeight;
            }

            _style.normal.textColor = Color.white;
        }
    }
}


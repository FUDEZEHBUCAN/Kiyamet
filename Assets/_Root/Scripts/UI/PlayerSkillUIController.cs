using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using _Root.Scripts.Input;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.UI
{
    public class PlayerSkillUIController : MonoBehaviour
    {
        [System.Serializable]
        private struct SkillSlotUI
        {
            [Tooltip("Skill ikonu (arka plan)")]
            public Image iconImage;

            [Tooltip("Cooldown/charge mask (fillAmount ile güncellenir)")]
            public Image cooldownFillImage;

            [Tooltip("Fill değerini tersle (1-x)")]
            public bool invertFill;
        }

        [Header("Slots (Icon + Fill)")]
        [SerializeField] private SkillSlotUI ultimate;
        [SerializeField] private SkillSlotUI signature;
        [SerializeField] private SkillSlotUI basic;

        [Header("Health & Mana")]
        [Tooltip("Oyuncunun health bar Image komponenti (fillAmount = current/max)")]
        [SerializeField] private Image healthBarImage;
        [Tooltip("Oyuncunun mana bar Image komponenti (fillAmount = current/max)")]
        [SerializeField] private Image manaBarImage;

        [Header("Behavior")]
        [Tooltip("Sadece local player için UI güncelle")]
        [SerializeField] private bool onlyForLocalPlayer = true;
        [SerializeField] private float barFillTweenDuration = 0.3f;

        [Header("Role icons — Tank")]
        [SerializeField] private Sprite tankUltimateIcon;
        [SerializeField] private Sprite tankSignatureIcon;
        [SerializeField] private Sprite tankBasicIcon;

        [Header("Role icons — Support (Shaman)")]
        [SerializeField] private Sprite supportUltimateIcon;
        [SerializeField] private Sprite supportSignatureIcon;
        [SerializeField] private Sprite supportBasicIcon;

        public Image BasicSkillIcon => basic.iconImage;

        private NetworkPlayer _player;
        private PlayerRoleType? _appliedIconRole;
        private NetworkPlayer _cachedPlayerForControllers;
        private NetworkCharacterControllerCustom _characterController;
        private MeleeController _meleeController;
        private CharacterInputController _inputController;

        private Tween _healthBarTween;
        private Tween _manaBarTween;
        private Tween _ultimatePulseTween;
        private float _lastSyncedHealth = float.NaN;
        private float _lastSyncedMana = float.NaN;

        private void Awake()
        {
            ApplySafeDefaults(ultimate);
            ApplySafeDefaults(signature);
            ApplySafeDefaults(basic);

            ApplyBarDefaults(healthBarImage);
            ApplyBarDefaults(manaBarImage);

            TryAutoBindHealthManaBars();
        }

        private void Update()
        {
            EnsurePlayer();

            if (_player == null)
            {
                _lastSyncedHealth = float.NaN;
                _lastSyncedMana = float.NaN;
                _cachedPlayerForControllers = null;
                _characterController = null;
                _meleeController = null;
                _inputController = null;
                StopUltimatePulse();
                SetFill(ultimate, 1f);
                SetFill(signature, 1f);
                SetFill(basic, 1f);
                return;
            }

            if (onlyForLocalPlayer && !(_player.Object != null && _player.Object.HasInputAuthority))
            {
                return;
            }

            TryApplyRoleIcons();
            UpdateUltimateUI(_player);
            RefreshDashAndMeleeCooldownFills();

            UpdateHealthManaBars(_player);
        }

        private void OnDestroy()
        {
            if (_healthBarTween != null && _healthBarTween.IsActive())
                _healthBarTween.Kill();
            if (_manaBarTween != null && _manaBarTween.IsActive())
                _manaBarTween.Kill();
            StopUltimatePulse();
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                CachePlayerControllersIfNeeded();
                return;
            }

            if (NetworkPlayer.Local != null)
            {
                _player = NetworkPlayer.Local;
                CachePlayerControllersIfNeeded();
                return;
            }

            // Fallback: sahnede input authority olan oyuncuyu bul.
            foreach (var p in FindObjectsOfType<NetworkPlayer>())
            {
                if (p != null && p.Object != null && p.Object.HasInputAuthority)
                {
                    _player = p;
                    CachePlayerControllersIfNeeded();
                    return;
                }
            }
        }

        private void CachePlayerControllersIfNeeded()
        {
            if (_player == _cachedPlayerForControllers)
                return;

            _cachedPlayerForControllers = _player;
            _appliedIconRole = null;
            if (_player == null)
            {
                _characterController = null;
                _meleeController = null;
                _inputController = null;
                return;
            }

            _characterController = _player.GetComponent<NetworkCharacterControllerCustom>();
            _meleeController = _player.GetComponent<MeleeController>();
            _inputController = _player.GetComponent<CharacterInputController>();
        }

        private void TryApplyRoleIcons()
        {
            if (_player == null)
                return;

            if (onlyForLocalPlayer && (_player.Object == null || !_player.Object.HasInputAuthority))
                return;

            var role = _player.RoleType;
            if (_appliedIconRole.HasValue && _appliedIconRole.Value == role)
                return;

            _appliedIconRole = role;
            var isSupport = role == PlayerRoleType.Support;
            ApplySlotIcon(ultimate, isSupport ? supportUltimateIcon : tankUltimateIcon);
            ApplySlotIcon(signature, isSupport ? supportSignatureIcon : tankSignatureIcon);
            ApplySlotIcon(basic, isSupport ? supportBasicIcon : tankBasicIcon);
        }

        private static void ApplySlotIcon(SkillSlotUI slot, Sprite sprite)
        {
            if (sprite == null || slot.iconImage == null)
                return;

            slot.iconImage.sprite = sprite;
            slot.iconImage.preserveAspect = true;
        }

        /// <summary>Signature = Dash, Basic = Melee; Fusion TickTimer kalan süresinden normalize.</summary>
        private void RefreshDashAndMeleeCooldownFills()
        {
            if (_player == null || _player.Object == null || !_player.Object.IsValid || _player.Object.Runner == null)
            {
                SetFill(signature, 0f);
                SetFill(basic, 0f);
                return;
            }

            float dashCd = _characterController != null ? _characterController.GetDashCooldownNormalized() : 0f;
            float meleeCd = _meleeController != null ? _meleeController.GetMeleeCooldownNormalized() : 0f;

            SetFill(signature, Mathf.Clamp01(dashCd));
            
            bool isBlockingNow = (_player != null && _player.IsBlocking)
                || (_player != null && _player.Object != null && _player.Object.HasInputAuthority && _inputController != null && _inputController.IsBlockHeld);
            
            if (isBlockingNow)
            {
                SetFillRaw(basic, 0f);
            }
            else
            {
                SetFill(basic, Mathf.Clamp01(meleeCd));
            }
        }

        private void UpdateUltimateUI(NetworkPlayer player)
        {
            // Ultimate UI kuralı:
            // - Aktifken: remainingNormalized (1 -> 0) göster (cooldown gibi akar)
            // - Hazır değilken: chargeNormalized (0 -> 1) birikimini cooldown mask'i olarak göster (mask terslenebilir)
            // - Hazırken: 0 (mask kapalı)
            float fill;

            if (player.IsUltimateActive)
            {
                fill = player.GetUltimateActiveRemainingNormalized();
            }
            else if (!player.IsUltimateReady)
            {
                // Charge ilerledikçe mask azalsın istiyorsak invertFill'i açın.
                fill = 1f - player.GetUltimateChargeNormalized();
            }
            else
            {
                fill = 0f;
            }

            SetFill(ultimate, fill);
            UpdateUltimatePulse(player);
        }

        private static void ApplySafeDefaults(SkillSlotUI slot)
        {
            if (slot.cooldownFillImage != null)
            {
                slot.cooldownFillImage.type = Image.Type.Filled;
            }
        }

        private static void ApplyBarDefaults(Image barImage)
        {
            if (barImage == null)
                return;

            if (barImage.type != Image.Type.Filled)
            {
                barImage.type = Image.Type.Filled;
            }
        }

        private void TryAutoBindHealthManaBars()
        {
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogWarning("[PlayerSkillUIController] Canvas bulunamadı; health/mana bar otomatik atanamadı.");
                return;
            }

            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            if (healthBarImage == null)
            {
                foreach (var img in images)
                {
                    if (img.name.Contains("Health Bar") || img.name.Contains("HealthBar"))
                    {
                        healthBarImage = img;
                        ApplyBarDefaults(healthBarImage);
                        break;
                    }
                }
            }

            if (manaBarImage == null)
            {
                foreach (var img in images)
                {
                    if (img.name.Contains("Mana Bar") || img.name.Contains("ManaBar"))
                    {
                        manaBarImage = img;
                        ApplyBarDefaults(manaBarImage);
                        break;
                    }
                }
            }
        }

        private void UpdateHealthManaBars(NetworkPlayer player)
        {
            if (player == null)
                return;

            if (healthBarImage != null && player.MaxHealth > 0.0001f)
            {
                if (float.IsNaN(_lastSyncedHealth) || Mathf.Abs(player.CurrentHealth - _lastSyncedHealth) > 0.0001f)
                {
                    _lastSyncedHealth = player.CurrentHealth;
                    float healthPercent = Mathf.Clamp01(player.CurrentHealth / player.MaxHealth);
                    TweenBarFill(healthBarImage, ref _healthBarTween, healthPercent);
                }
            }

            if (manaBarImage != null && player.MaxMana > 0.0001f)
            {
                if (float.IsNaN(_lastSyncedMana) || Mathf.Abs(player.CurrentMana - _lastSyncedMana) > 0.0001f)
                {
                    _lastSyncedMana = player.CurrentMana;
                    float manaPercent = Mathf.Clamp01(player.CurrentMana / player.MaxMana);
                    TweenBarFill(manaBarImage, ref _manaBarTween, manaPercent);
                }
            }
        }

        private void TweenBarFill(Image image, ref Tween tweenSlot, float targetFillAmount)
        {
            if (image == null)
                return;

            if (tweenSlot != null && tweenSlot.IsActive())
                tweenSlot.Kill();

            tweenSlot = image.DOFillAmount(Mathf.Clamp01(targetFillAmount), barFillTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        private static void SetFill(SkillSlotUI slot, float value01)
        {
            if (slot.cooldownFillImage == null)
                return;

            float v = Mathf.Clamp01(value01);
            if (slot.invertFill)
            {
                v = 1f - v;
            }

            slot.cooldownFillImage.fillAmount = v;
        }
        
        private static void SetFillRaw(SkillSlotUI slot, float value01)
        {
            if (slot.cooldownFillImage == null)
                return;
            
            slot.cooldownFillImage.fillAmount = Mathf.Clamp01(value01);
        }
        
        private void UpdateUltimatePulse(NetworkPlayer player)
        {
            bool shouldPulse = player != null && player.IsUltimateReady && !player.IsUltimateActive;
            if (shouldPulse)
            {
                StartUltimatePulse();
            }
            else
            {
                StopUltimatePulse();
            }
        }
        
        private void StartUltimatePulse()
        {
            if (ultimate.iconImage == null)
                return;
            
            if (_ultimatePulseTween != null && _ultimatePulseTween.IsActive())
                return;
            
            ultimate.iconImage.transform.localScale = Vector3.one;
            _ultimatePulseTween = ultimate.iconImage.transform
                .DOScale(1.06f, 0.55f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        
        private void StopUltimatePulse()
        {
            if (_ultimatePulseTween != null && _ultimatePulseTween.IsActive())
                _ultimatePulseTween.Kill();
            
            _ultimatePulseTween = null;
            
            if (ultimate.iconImage != null)
                ultimate.iconImage.transform.localScale = Vector3.one;
        }
    }
}

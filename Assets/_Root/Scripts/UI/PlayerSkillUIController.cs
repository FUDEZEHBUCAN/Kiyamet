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

        [Header("Role icons — Duelist")]
        [SerializeField] private Sprite duelistUltimateIcon;
        [SerializeField] private Sprite duelistSignatureIcon;
        [SerializeField] private Sprite duelistBasicIcon;

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

            DisableLegacyCooldownListeners();

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
                ForceUltimateOverlayEmpty();
                ApplyCooldownOverlayFill(signature, 0f);
                ApplyCooldownOverlayFill(basic, 0f);
                return;
            }

            if (onlyForLocalPlayer && !(_player.Object != null && _player.Object.HasInputAuthority))
            {
                return;
            }

            TryApplyRoleIcons();
            UpdateHealthManaBars(_player);
        }

        private void LateUpdate()
        {
            if (_player == null)
                return;

            if (onlyForLocalPlayer && !(_player.Object != null && _player.Object.HasInputAuthority))
                return;

            RefreshUltimateOverlay(_player);
            RefreshDashAndMeleeCooldownFills();
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

            switch (role)
            {
                case PlayerRoleType.Support:
                    ApplySlotIcon(ultimate, supportUltimateIcon);
                    ApplySlotIcon(signature, supportSignatureIcon);
                    ApplySlotIcon(basic, supportBasicIcon);
                    break;
                case PlayerRoleType.Duelist:
                    ApplySlotIcon(ultimate, duelistUltimateIcon);
                    ApplySlotIcon(signature, duelistSignatureIcon);
                    ApplySlotIcon(basic, duelistBasicIcon);
                    break;
                default:
                    ApplySlotIcon(ultimate, tankUltimateIcon);
                    ApplySlotIcon(signature, tankSignatureIcon);
                    ApplySlotIcon(basic, tankBasicIcon);
                    break;
            }
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
                ApplyCooldownOverlayFill(signature, 0f);
                ApplyCooldownOverlayFill(basic, 0f);
                return;
            }

            float dashCd = _characterController != null ? _characterController.GetDashCooldownNormalized() : 0f;
            float meleeCd = _meleeController != null ? _meleeController.GetMeleeCooldownNormalized() : 0f;

            ApplyCooldownOverlayFill(signature, Mathf.Clamp01(dashCd));

            bool isBlockingNow = (_player != null && _player.IsBlocking)
                || (_player != null && _player.Object != null && _player.Object.HasInputAuthority && _inputController != null && _inputController.IsBlockHeld);

            if (isBlockingNow)
                ApplyCooldownOverlayFill(basic, 0f, respectInvertFill: false);
            else
                ApplyCooldownOverlayFill(basic, Mathf.Clamp01(meleeCd));
        }

        public void RefreshUltimateOverlay(NetworkPlayer player)
        {
            if (player == null)
            {
                ForceUltimateOverlayEmpty();
                StopUltimatePulse();
                return;
            }

            _player = player;
            CachePlayerControllersIfNeeded();

            UpdateUltimatePulse(player);
            ApplyUltimateChargeOverlay(player);
        }

        /// <summary>
        /// Kill charge: overlay clockwise kapalı; fill 1→0 (kill arttıkça boşalır, cooldown ile aynı mantık).
        /// Hazır veya aktifken overlay kapalı kalır.
        /// </summary>
        private void ApplyUltimateChargeOverlay(NetworkPlayer player)
        {
            if (player == null || player.IsUltimateActive || player.IsUltimateReady)
            {
                ForceUltimateOverlayEmpty();
                return;
            }

            if (ultimate.cooldownFillImage != null)
                ultimate.cooldownFillImage.fillClockwise = false;

            ApplyCooldownOverlayFill(ultimate, 1f - player.GetUltimateChargeNormalized());
        }

        private void ForceUltimateOverlayEmpty()
        {
            if (ultimate.cooldownFillImage == null)
                return;

            ultimate.cooldownFillImage.fillAmount = 0f;
            ultimate.cooldownFillImage.enabled = false;
        }

        private static void ApplyCooldownOverlayFill(SkillSlotUI slot, float value01, bool respectInvertFill = true)
        {
            if (slot.cooldownFillImage == null)
                return;

            float v = Mathf.Clamp01(value01);
            if (respectInvertFill && slot.invertFill)
                v = 1f - v;

            slot.cooldownFillImage.fillAmount = v;
            slot.cooldownFillImage.enabled = v > 0.001f;
        }

        private void DisableLegacyCooldownListeners()
        {
            DisableLegacyCooldownListener(ultimate.cooldownFillImage);
            DisableLegacyCooldownListener(signature.cooldownFillImage);
            DisableLegacyCooldownListener(basic.cooldownFillImage);
        }

        private static void DisableLegacyCooldownListener(Image overlayImage)
        {
            if (overlayImage == null)
                return;

            var listener = overlayImage.GetComponent<cd_status_listener>();
            if (listener != null)
                listener.enabled = false;
        }

        private static void ApplySafeDefaults(SkillSlotUI slot)
        {
            if (slot.iconImage != null)
            {
                slot.iconImage.color = Color.white;
            }

            if (slot.cooldownFillImage != null)
            {
                slot.cooldownFillImage.type = Image.Type.Filled;
                slot.cooldownFillImage.fillMethod = Image.FillMethod.Radial360;
                slot.cooldownFillImage.fillOrigin = (int)Image.Origin360.Top;
                slot.cooldownFillImage.fillClockwise = false;
                slot.cooldownFillImage.fillAmount = 0f;
                slot.cooldownFillImage.enabled = false;
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

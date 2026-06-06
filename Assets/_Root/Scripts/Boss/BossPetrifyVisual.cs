using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Taşlaşma: skin donma (desatürasyon + kontrast + rim), ortada materyal swap, rock HSV/rim ile belirme.
    /// </summary>
    public class BossPetrifyVisual : MonoBehaviour
    {
        [SerializeField] private Material petrifiedMaterial;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.55f;
        [SerializeField, Range(0f, 1f)] private float freezeDesaturate = 0.82f;
        [SerializeField] private float rimPulseSpeed = 14f;
        [SerializeField] private float rimPulseStrength = 0.28f;

        [Header("Emission")]
        [SerializeField] private bool driveEmissionKeyword = true;
        [ColorUsage(true, true)]
        [SerializeField] private Color freezeEmissionColor = new Color(0.55f, 0.72f, 0.95f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color swapBurstEmissionColor = new Color(1.05f, 1.1f, 1.25f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color stoneRevealEmissionColor = new Color(0.42f, 0.5f, 0.62f, 1f);
        [SerializeField] private float freezeEmissionIntensity = 2.4f;
        [SerializeField] private float swapBurstEmissionIntensity = 5.5f;
        [SerializeField] private float stoneRevealEmissionIntensity = 1.6f;
        [SerializeField] private float swapBurstDuration = 0.3f;
        [SerializeField] private float emissionCrackleSpeed = 24f;
        [SerializeField, Range(0f, 1f)] private float preSwapEmissionRampStart = 0.62f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int HColorId = Shader.PropertyToID("_HColor");
        private static readonly int SColorId = Shader.PropertyToID("_SColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int UseEmissionId = Shader.PropertyToID("_UseEmission");
        private static readonly int RampThresholdId = Shader.PropertyToID("_RampThreshold");
        private static readonly int RampSmoothId = Shader.PropertyToID("_RampSmoothing");
        private static readonly int HsvHId = Shader.PropertyToID("_HSV_H");
        private static readonly int HsvSId = Shader.PropertyToID("_HSV_S");
        private static readonly int HsvVId = Shader.PropertyToID("_HSV_V");
        private static readonly int UseRimId = Shader.PropertyToID("_UseRim");
        private static readonly int RimMinId = Shader.PropertyToID("_RimMin");
        private static readonly int RimMaxId = Shader.PropertyToID("_RimMax");

        private readonly List<Material[]> _defaultSharedMaterials = new List<Material[]>();
        private readonly List<RendererFadeSlot> _slots = new List<RendererFadeSlot>();
        private readonly List<Material> _createdInstances = new List<Material>();

        private Color _stoneHighlight = new Color(0.85f, 0.84f, 0.82f, 1f);
        private Color _stoneShadow = new Color(0.2f, 0.22f, 0.26f, 1f);
        private Color _stoneRim = new Color(0.75f, 0.82f, 0.9f, 0.55f);
        private float _stoneHsvS;
        private float _swapBurstBoost;
        private float _stoneHsvV;
        private float _stoneHsvH;
        private float _stoneRampThreshold = 0.2f;
        private float _stoneUseRim;

        private Coroutine _transitionRoutine;
        private bool _isPetrifiedVisual;

        public bool IsPetrifiedVisual => _isPetrifiedVisual;

        public bool IsShowingRockMaterial()
        {
            if (petrifiedMaterial == null)
                return false;

            EnsureRendererCache();
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] == petrifiedMaterial)
                        return true;
                }
            }

            return false;
        }

        private enum TransitionSegment
        {
            SkinFreeze,
            StoneReveal
        }

        private sealed class RendererFadeSlot
        {
            public Renderer Renderer;
            public Material[] ActiveMaterials;
            public MaterialAnimSnapshot[] Snapshots;
            public int SlotIndex;
        }

        private struct MaterialAnimSnapshot
        {
            public Color Color;
            public Color HColor;
            public Color SColor;
            public Color RimColor;
            public Color EmissionColor;
            public float RampThreshold;
            public float RampSmooth;
            public float HsvH;
            public float HsvS;
            public float HsvV;
            public float UseRim;
            public float RimMin;
            public float RimMax;
            public bool HasH;
            public bool HasS;
            public bool HasRim;
            public bool HasEmission;
            public bool HadEmissionKeyword;
            public bool HasRamp;
            public bool HasHsv;
        }

        public void Configure(Material rockMaterial, Material defaultMaterial = null, Renderer[] renderersOverride = null)
        {
            if (rockMaterial != null)
            {
                petrifiedMaterial = rockMaterial;
                CacheStonePalette(rockMaterial);
            }

            if (defaultMaterial != null)
                normalMaterial = defaultMaterial;

            if (renderersOverride != null && renderersOverride.Length > 0)
                targetRenderers = renderersOverride;
        }

        public void EnsureDefaultMaterialsCached(bool forceRefresh = false)
        {
            EnsureRendererCache();

            if (normalMaterial == null)
            {
                Debug.LogWarning($"[BossPetrifyVisual] normalMaterial atanmamış: {name}");
                return;
            }

            if (!forceRefresh && HasValidDefaultCache())
                return;

            CacheDefaultMaterials();
        }

        private void Awake()
        {
            if (petrifiedMaterial != null)
                CacheStonePalette(petrifiedMaterial);

            CacheDefaultMaterials();
        }

        private void OnDestroy()
        {
            StopTransitionRoutine();
            DestroyCreatedInstances();
        }

        public void ApplyPetrified(bool instant = false)
        {
            if (petrifiedMaterial == null)
            {
                Debug.LogWarning($"[BossPetrifyVisual] petrifiedMaterial atanmamış: {name}");
                return;
            }

            EnsureRendererCache();
            if (_isPetrifiedVisual && _transitionRoutine == null)
                return;

            StopTransitionRoutine();
            if (instant)
            {
                ApplyPetrifiedInstant();
                return;
            }

            _transitionRoutine = StartCoroutine(PetrifyTransitionRoutine());
        }

        public void RestoreFromSleepStone(bool animated = true)
        {
            if (petrifiedMaterial == null || normalMaterial == null)
            {
                Debug.LogWarning($"[BossPetrifyVisual] RestoreFromSleepStone materyal eksik: {name}");
                return;
            }

            if (!IsShowingRockMaterial() && !_isPetrifiedVisual)
                return;

            EnsureDefaultMaterialsCached(forceRefresh: true);
            StopTransitionRoutine();

            if (!animated)
            {
                ApplyNormalSharedMaterials();
                return;
            }

            _transitionRoutine = StartCoroutine(RestoreTransitionRoutine());
        }

        public void RestoreDefault(bool instant = false)
        {
            if (!_isPetrifiedVisual && _transitionRoutine == null && !IsShowingRockMaterial())
                return;

            StopTransitionRoutine();

            if (instant)
            {
                ApplyNormalSharedMaterials();
                return;
            }

            EnsureDefaultMaterialsCached(forceRefresh: true);
            _transitionRoutine = StartCoroutine(RestoreTransitionRoutine());
        }

        private IEnumerator PetrifyTransitionRoutine()
        {
            BuildFadeSlots(useRockMaterials: false);
            ApplySegmentVisual(0f, TransitionSegment.SkinFreeze);

            yield return AnimateSegment(TransitionSegment.SkinFreeze, fadeInDuration);

            SwapToRockMaterials();
            yield return PlaySwapEmissionBurst();

            ApplySegmentVisual(0f, TransitionSegment.StoneReveal);
            yield return AnimateSegment(TransitionSegment.StoneReveal, fadeOutDuration);

            FinalizeRockMaterials();
            _isPetrifiedVisual = true;
            _transitionRoutine = null;
        }

        private IEnumerator RestoreTransitionRoutine()
        {
            BuildFadeSlots(useRockMaterials: true);
            ApplySegmentVisual(1f, TransitionSegment.StoneReveal);

            yield return AnimateSegmentReverse(TransitionSegment.StoneReveal, fadeOutDuration);

            SwapToDefaultMaterials();
            ApplySegmentVisual(1f, TransitionSegment.SkinFreeze);

            yield return AnimateSegmentReverse(TransitionSegment.SkinFreeze, fadeInDuration);

            ApplyNormalSharedMaterials();
        }

        private IEnumerator AnimateSegment(TransitionSegment segment, float duration)
        {
            if (duration <= 0.001f)
            {
                ApplySegmentVisual(1f, segment);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplySegmentVisual(Mathf.SmoothStep(0f, 1f, t), segment);
                yield return null;
            }

            ApplySegmentVisual(1f, segment);
        }

        private IEnumerator AnimateSegmentReverse(TransitionSegment segment, float duration)
        {
            if (duration <= 0.001f)
            {
                ApplySegmentVisual(0f, segment);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplySegmentVisual(1f - Mathf.SmoothStep(0f, 1f, t), segment);
                yield return null;
            }

            ApplySegmentVisual(0f, segment);
        }

        private void ApplyPetrifiedInstant()
        {
            EnsureDefaultMaterialsCached();

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = CreateRockMaterialArray(count);
            }

            ClearRuntimeState();
            _isPetrifiedVisual = true;
        }

        private void ApplySegmentVisual(float t, TransitionSegment segment)
        {
            for (int s = 0; s < _slots.Count; s++)
                ApplySegmentToSlot(_slots[s], t, segment);
        }

        private void ApplySegmentToSlot(RendererFadeSlot slot, float t, TransitionSegment segment)
        {
            t = Mathf.Clamp01(t);

            for (int i = 0; i < slot.ActiveMaterials.Length; i++)
            {
                var mat = slot.ActiveMaterials[i];
                if (mat == null || slot.Snapshots == null || i >= slot.Snapshots.Length)
                    continue;

                ref var snap = ref slot.Snapshots[i];
                float pulsePhase = Time.time * rimPulseSpeed + slot.SlotIndex * 1.37f + i * 0.61f;

                if (segment == TransitionSegment.SkinFreeze)
                    ApplySkinFreeze(mat, snap, t, pulsePhase);
                else
                    ApplyStoneReveal(mat, snap, t, pulsePhase);
            }
        }

        private void ApplySkinFreeze(Material mat, in MaterialAnimSnapshot snap, float t, float pulsePhase)
        {
            var desaturated = Desaturate(snap.Color);
            var albedo = Color.Lerp(snap.Color, desaturated, t * freezeDesaturate);

            SetColorIfExists(mat, ColorId, albedo);
            SetColorIfExists(mat, BaseColorId, albedo);

            if (snap.HasRim)
            {
                float pulse = 1f + rimPulseStrength * Mathf.Sin(pulsePhase) * t * (1f - t * 0.35f);
                var rim = Color.Lerp(snap.RimColor, _stoneRim, t) * pulse;
                mat.SetColor(RimColorId, rim);
            }

            float preSwapRamp = Mathf.InverseLerp(preSwapEmissionRampStart, 1f, t);
            float freezeWeight = t * (0.55f + 0.45f * preSwapRamp);
            ApplyTransitionEmission(
                mat,
                snap,
                freezeEmissionColor,
                freezeEmissionIntensity,
                freezeWeight,
                pulsePhase,
                emissionCrackleSpeed);

            if (snap.HasRim && t > preSwapEmissionRampStart)
            {
                float rimGlow = Mathf.InverseLerp(preSwapEmissionRampStart, 1f, t);
                var rim = mat.GetColor(RimColorId);
                mat.SetColor(RimColorId, rim + freezeEmissionColor * (rimGlow * 0.35f));
            }
        }

        private void ApplyStoneReveal(Material mat, in MaterialAnimSnapshot snap, float t, float pulsePhase)
        {
            if (snap.HasHsv)
            {
                mat.SetFloat(HsvHId, snap.HsvH);
                mat.SetFloat(HsvSId, Mathf.Lerp(snap.HsvS - 0.15f, snap.HsvS, t));
                mat.SetFloat(HsvVId, Mathf.Lerp(snap.HsvV - 0.1f, snap.HsvV, t));
            }
            else
            {
                var desaturated = Desaturate(snap.Color);
                var albedo = Color.Lerp(desaturated, snap.Color, t);
                SetColorIfExists(mat, ColorId, albedo);
                SetColorIfExists(mat, BaseColorId, albedo);
            }

            if (snap.HasRim)
            {
                if (mat.HasProperty(UseRimId))
                    mat.SetFloat(UseRimId, Mathf.Lerp(1f, snap.UseRim, t));

                float pulse = 1f + rimPulseStrength * 0.65f * Mathf.Sin(pulsePhase) * (1f - t);
                mat.SetColor(RimColorId, Color.Lerp(_stoneRim * 1.15f, snap.RimColor, t) * pulse);

                if (mat.HasProperty(RimMinId))
                    mat.SetFloat(RimMinId, Mathf.Lerp(snap.RimMin * 0.6f, snap.RimMin, t));
                if (mat.HasProperty(RimMaxId))
                    mat.SetFloat(RimMaxId, Mathf.Lerp(snap.RimMax * 1.2f, snap.RimMax, t));
            }

            float revealWeight = Mathf.SmoothStep(0f, 1f, t);
            float burstTail = _swapBurstBoost * Mathf.Exp(-t * 5.5f);
            ApplyTransitionEmission(
                mat,
                snap,
                stoneRevealEmissionColor,
                stoneRevealEmissionIntensity + burstTail,
                revealWeight,
                pulsePhase,
                emissionCrackleSpeed * 0.85f,
                burstTail);

            if (snap.HasRim)
            {
                float rimGlow = (1f - t) * 0.25f + burstTail * 0.12f;
                var rim = mat.GetColor(RimColorId);
                mat.SetColor(RimColorId, rim + swapBurstEmissionColor * rimGlow);
            }
        }

        private IEnumerator PlaySwapEmissionBurst()
        {
            if (swapBurstDuration <= 0.001f || swapBurstEmissionIntensity <= 0.001f)
                yield break;

            float elapsed = 0f;
            while (elapsed < swapBurstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swapBurstDuration);
                _swapBurstBoost = Mathf.Sin(t * Mathf.PI) * swapBurstEmissionIntensity;
                ApplySwapBurstEmission();
                yield return null;
            }

            _swapBurstBoost = 0f;
        }

        private void ApplySwapBurstEmission()
        {
            float pulsePhase = Time.time * emissionCrackleSpeed;
            for (int s = 0; s < _slots.Count; s++)
            {
                var slot = _slots[s];
                for (int i = 0; i < slot.ActiveMaterials.Length; i++)
                {
                    var mat = slot.ActiveMaterials[i];
                    if (mat == null || slot.Snapshots == null || i >= slot.Snapshots.Length)
                        continue;

                    float flicker = ComputeEmissionFlicker(pulsePhase + slot.SlotIndex + i * 0.73f);
                    ApplyTransitionEmission(
                        mat,
                        slot.Snapshots[i],
                        swapBurstEmissionColor,
                        _swapBurstBoost,
                        1f,
                        pulsePhase,
                        emissionCrackleSpeed,
                        _swapBurstBoost,
                        forceKeywordOn: true);
                }
            }
        }

        private void ApplyTransitionEmission(
            Material mat,
            in MaterialAnimSnapshot snap,
            Color glowColor,
            float intensity,
            float weight,
            float pulsePhase,
            float crackleSpeed,
            float extraIntensity = 0f,
            bool forceKeywordOn = false)
        {
            if (!mat.HasProperty(EmissionColorId) || weight <= 0.001f && extraIntensity <= 0.001f)
                return;

            if (driveEmissionKeyword && (forceKeywordOn || weight > 0.01f))
                EnableEmissionKeyword(mat);

            float flicker = ComputeEmissionFlicker(pulsePhase * crackleSpeed * 0.08f);
            float totalIntensity = Mathf.Max(0f, intensity + extraIntensity) * weight * flicker;
            var hdr = glowColor * totalIntensity;
            var target = Color.Lerp(snap.EmissionColor, hdr, Mathf.Clamp01(weight + extraIntensity * 0.15f));
            mat.SetColor(EmissionColorId, target);

            if (mat.HasProperty(UseEmissionId) && (forceKeywordOn || weight > 0.05f))
                mat.SetFloat(UseEmissionId, 1f);
        }

        private static float ComputeEmissionFlicker(float phase)
        {
            float a = Mathf.Sin(phase) * 0.5f + 0.5f;
            float b = Mathf.Sin(phase * 2.37f + 1.2f) * 0.5f + 0.5f;
            return Mathf.Lerp(0.72f, 1.28f, a * 0.6f + b * 0.4f);
        }

        private static void EnableEmissionKeyword(Material mat)
        {
            if (mat == null)
                return;

            mat.EnableKeyword("_EMISSION");
            if (mat.globalIlluminationFlags == MaterialGlobalIlluminationFlags.EmissiveIsBlack)
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static void RestoreEmissionKeyword(Material mat, in MaterialAnimSnapshot snap)
        {
            if (mat == null)
                return;

            if (snap.HadEmissionKeyword)
                mat.EnableKeyword("_EMISSION");
            else
                mat.DisableKeyword("_EMISSION");

            if (mat.HasProperty(UseEmissionId))
                mat.SetFloat(UseEmissionId, snap.HadEmissionKeyword ? 1f : 0f);
        }

        private void FinalizeRockMaterials()
        {
            for (int s = 0; s < _slots.Count; s++)
            {
                var slot = _slots[s];
                for (int i = 0; i < slot.ActiveMaterials.Length; i++)
                {
                    var mat = slot.ActiveMaterials[i];
                    if (mat == null || slot.Snapshots == null || i >= slot.Snapshots.Length)
                        continue;

                    ref var snap = ref slot.Snapshots[i];
                    ApplyStoneReveal(mat, snap, 1f, 0f);
                    RestoreEmissionKeyword(mat, snap);
                    if (mat.HasProperty(EmissionColorId))
                        mat.SetColor(EmissionColorId, snap.EmissionColor);
                }
            }
        }

        private static void SetColorIfExists(Material mat, int propertyId, Color color)
        {
            if (mat.HasProperty(propertyId))
                mat.SetColor(propertyId, color);
        }

        private static Color Desaturate(Color color)
        {
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(luminance, luminance, luminance, color.a);
        }

        private void CacheStonePalette(Material rock)
        {
            if (rock.HasProperty(HColorId))
                _stoneHighlight = rock.GetColor(HColorId);
            if (rock.HasProperty(SColorId))
                _stoneShadow = rock.GetColor(SColorId);
            if (rock.HasProperty(RimColorId))
                _stoneRim = rock.GetColor(RimColorId);

            if (rock.HasProperty(HsvSId))
                _stoneHsvS = rock.GetFloat(HsvSId);
            if (rock.HasProperty(HsvVId))
                _stoneHsvV = rock.GetFloat(HsvVId);
            if (rock.HasProperty(HsvHId))
                _stoneHsvH = rock.GetFloat(HsvHId);
            if (rock.HasProperty(RampThresholdId))
                _stoneRampThreshold = rock.GetFloat(RampThresholdId);
            if (rock.HasProperty(UseRimId))
                _stoneUseRim = rock.GetFloat(UseRimId);
        }

        private void BuildFadeSlots(bool useRockMaterials)
        {
            ClearRuntimeState();
            EnsureRendererCache();

            for (int r = 0; r < targetRenderers.Length; r++)
            {
                var renderer = targetRenderers[r];
                if (renderer == null)
                    continue;

                var shared = useRockMaterials
                    ? CreateRockMaterialArray(renderer.sharedMaterials.Length)
                    : GetDefaultMaterialsForRenderer(r);

                var instances = CreateMaterialInstances(shared);
                renderer.materials = instances;

                _slots.Add(new RendererFadeSlot
                {
                    Renderer = renderer,
                    ActiveMaterials = instances,
                    Snapshots = CaptureSnapshots(instances),
                    SlotIndex = r
                });
            }
        }

        private MaterialAnimSnapshot[] CaptureSnapshots(Material[] materials)
        {
            var snapshots = new MaterialAnimSnapshot[materials.Length];

            for (int i = 0; i < materials.Length; i++)
                snapshots[i] = CaptureSnapshot(materials[i]);

            return snapshots;
        }

        private static MaterialAnimSnapshot CaptureSnapshot(Material mat)
        {
            var snap = new MaterialAnimSnapshot();

            if (mat == null)
                return snap;

            if (mat.HasProperty(ColorId))
                snap.Color = mat.GetColor(ColorId);
            else if (mat.HasProperty(BaseColorId))
                snap.Color = mat.GetColor(BaseColorId);
            else
                snap.Color = Color.white;

            snap.HasH = mat.HasProperty(HColorId);
            if (snap.HasH)
                snap.HColor = mat.GetColor(HColorId);

            snap.HasS = mat.HasProperty(SColorId);
            if (snap.HasS)
                snap.SColor = mat.GetColor(SColorId);

            snap.HasRim = mat.HasProperty(RimColorId);
            if (snap.HasRim)
            {
                snap.RimColor = mat.GetColor(RimColorId);
                if (mat.HasProperty(UseRimId))
                    snap.UseRim = mat.GetFloat(UseRimId);
                if (mat.HasProperty(RimMinId))
                    snap.RimMin = mat.GetFloat(RimMinId);
                if (mat.HasProperty(RimMaxId))
                    snap.RimMax = mat.GetFloat(RimMaxId);
            }

            snap.HasEmission = mat.HasProperty(EmissionColorId);
            if (snap.HasEmission)
                snap.EmissionColor = mat.GetColor(EmissionColorId);

            snap.HadEmissionKeyword = mat.IsKeywordEnabled("_EMISSION");

            snap.HasRamp = mat.HasProperty(RampThresholdId);
            if (snap.HasRamp)
            {
                snap.RampThreshold = mat.GetFloat(RampThresholdId);
                if (mat.HasProperty(RampSmoothId))
                    snap.RampSmooth = mat.GetFloat(RampSmoothId);
            }

            snap.HasHsv = mat.HasProperty(HsvSId) && mat.HasProperty(HsvVId);
            if (snap.HasHsv)
            {
                snap.HsvH = mat.HasProperty(HsvHId) ? mat.GetFloat(HsvHId) : 0f;
                snap.HsvS = mat.GetFloat(HsvSId);
                snap.HsvV = mat.GetFloat(HsvVId);
            }

            return snap;
        }

        private Material[] GetDefaultMaterialsForRenderer(int rendererIndex)
        {
            if (rendererIndex >= 0 && rendererIndex < _defaultSharedMaterials.Count
                && _defaultSharedMaterials[rendererIndex].Length > 0)
                return _defaultSharedMaterials[rendererIndex];

            if (normalMaterial != null)
            {
                int count = Mathf.Max(1, targetRenderers[rendererIndex].sharedMaterials.Length);
                return CreateUniformMaterialArray(normalMaterial, count);
            }

            return targetRenderers[rendererIndex].sharedMaterials;
        }

        private Material[] CreateRockMaterialArray(int count)
        {
            return CreateUniformMaterialArray(petrifiedMaterial, count);
        }

        private static Material[] CreateUniformMaterialArray(Material source, int count)
        {
            var materials = new Material[count];
            for (int i = 0; i < count; i++)
                materials[i] = source;

            return materials;
        }

        private Material[] CreateMaterialInstances(Material[] sources)
        {
            var instances = new Material[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null)
                    continue;

                var instance = new Material(sources[i]);
                _createdInstances.Add(instance);
                instances[i] = instance;
            }

            return instances;
        }

        private void SwapToRockMaterials()
        {
            for (int s = 0; s < _slots.Count; s++)
            {
                var slot = _slots[s];
                if (slot.Renderer == null)
                    continue;

                int count = Mathf.Max(1, slot.ActiveMaterials.Length);
                var rockInstances = CreateMaterialInstances(CreateRockMaterialArray(count));
                slot.Renderer.materials = rockInstances;
                slot.ActiveMaterials = rockInstances;
                slot.Snapshots = CaptureSnapshots(rockInstances);
            }
        }

        private void SwapToDefaultMaterials()
        {
            for (int s = 0; s < _slots.Count; s++)
            {
                var slot = _slots[s];
                if (slot.Renderer == null)
                    continue;

                int rendererIndex = FindRendererIndex(slot.Renderer);
                var shared = GetDefaultMaterialsForRenderer(rendererIndex);
                var instances = CreateMaterialInstances(shared);
                slot.Renderer.materials = instances;
                slot.ActiveMaterials = instances;
                slot.Snapshots = CaptureSnapshots(instances);
            }
        }

        private int FindRendererIndex(Renderer renderer)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == renderer)
                    return i;
            }

            return 0;
        }

        private void ApplyNormalSharedMaterials()
        {
            EnsureDefaultMaterialsCached(forceRefresh: true);
            ClearRuntimeState();

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                renderer.sharedMaterials = GetDefaultMaterialsForRenderer(i);
            }

            _isPetrifiedVisual = false;
            _transitionRoutine = null;
        }

        private void StopTransitionRoutine()
        {
            if (_transitionRoutine == null)
                return;

            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
            _swapBurstBoost = 0f;
        }

        private void ClearRuntimeState()
        {
            _slots.Clear();
            DestroyCreatedInstances();
        }

        private void DestroyCreatedInstances()
        {
            for (int i = 0; i < _createdInstances.Count; i++)
            {
                if (_createdInstances[i] != null)
                    Destroy(_createdInstances[i]);
            }

            _createdInstances.Clear();
        }

        private void EnsureRendererCache()
        {
            if (targetRenderers != null && targetRenderers.Length > 0)
                return;

            targetRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            CacheDefaultMaterials();
        }

        private void CacheDefaultMaterials()
        {
            _defaultSharedMaterials.Clear();
            if (targetRenderers == null)
                return;

            foreach (var renderer in targetRenderers)
            {
                if (renderer == null)
                {
                    _defaultSharedMaterials.Add(System.Array.Empty<Material>());
                    continue;
                }

                if (normalMaterial != null)
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    _defaultSharedMaterials.Add(CreateUniformMaterialArray(normalMaterial, count));
                    continue;
                }

                var mats = renderer.sharedMaterials;
                var copy = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                    copy[i] = mats[i];

                _defaultSharedMaterials.Add(copy);
            }
        }

        private bool HasValidDefaultCache()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                return false;

            if (_defaultSharedMaterials.Count != targetRenderers.Length)
                return false;

            for (int i = 0; i < _defaultSharedMaterials.Count; i++)
            {
                var mats = _defaultSharedMaterials[i];
                if (mats == null || mats.Length == 0)
                    return false;

                for (int j = 0; j < mats.Length; j++)
                {
                    if (mats[j] == null || mats[j] == petrifiedMaterial)
                        return false;
                }
            }

            return true;
        }
    }
}

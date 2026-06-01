using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Melee queue penceresi ve dolu queue için silah ucunda glow geri bildirimi (renderer + particle).
    /// </summary>
    public class MeleeWeaponTipGlow : MonoBehaviour
    {
        public enum GlowState
        {
            Off = 0,
            QueueWindow = 1,
            Queued = 2
        }

        [Header("Anchor")]
        [SerializeField] private Transform tipAnchor;

        [Header("Glow visuals")]
        [SerializeField] private Renderer glowRenderer;
        [SerializeField] private ParticleSystem glowParticles;
        [SerializeField] private float glowSphereScale = 0.065f;
        [SerializeField] private float particleSize = 0.035f;
        [SerializeField] private float particleShapeRadius = 0.015f;

        [Header("Colors")]
        [SerializeField] private Color queueWindowColor = new Color(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color queuedColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private float queueWindowIntensity = 1.6f;
        [SerializeField] private float queuedIntensity = 2.8f;
        [SerializeField] private float pulseSpeed = 10f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _propertyBlock;
        private GlowState _state = GlowState.Off;
        private float _pulsePhase;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            EnsureRuntimeGlowIfMissing();
            ApplyGlowScale();
            SetState(GlowState.Off);
        }

        private void OnValidate()
        {
            ApplyGlowScale();
        }

        private void ApplyGlowScale()
        {
            if (glowRenderer == null)
                return;

            glowRenderer.transform.localScale = Vector3.one * glowSphereScale;
        }

        private void Update()
        {
            if (_state == GlowState.Off)
                return;

            _pulsePhase += Time.deltaTime * pulseSpeed;
            float pulse = 0.65f + 0.35f * Mathf.Sin(_pulsePhase);

            if (glowRenderer != null && glowRenderer.enabled)
            {
                Color baseColor = _state == GlowState.Queued ? queuedColor : queueWindowColor;
                float intensity = (_state == GlowState.Queued ? queuedIntensity : queueWindowIntensity) * pulse;
                glowRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId, baseColor * intensity);
                glowRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (glowParticles != null && glowParticles.isPlaying)
            {
                var emission = glowParticles.emission;
                float rate = (_state == GlowState.Queued ? 16f : 8f) * pulse;
                emission.rateOverTime = rate;
            }
        }

        public void SetState(GlowState state)
        {
            _state = state;
            _pulsePhase = 0f;

            bool active = state != GlowState.Off;
            if (glowRenderer != null)
                glowRenderer.enabled = active;

            if (glowParticles != null)
            {
                if (active)
                {
                    if (!glowParticles.isPlaying)
                        glowParticles.Play();
                }
                else if (glowParticles.isPlaying)
                {
                    glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (!active || glowRenderer == null)
                return;

            Color baseColor = state == GlowState.Queued ? queuedColor : queueWindowColor;
            float intensity = state == GlowState.Queued ? queuedIntensity : queueWindowIntensity;
            glowRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, baseColor * intensity);
            glowRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void EnsureRuntimeGlowIfMissing()
        {
            if (tipAnchor == null)
                tipAnchor = transform;

            if (glowRenderer != null)
                return;

            var glowObj = new GameObject("MeleeTipGlow");
            glowObj.transform.SetParent(tipAnchor, false);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = Vector3.one * glowSphereScale;

            var meshFilter = glowObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateSphereMesh();

            glowRenderer = glowObj.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_BaseColor", queueWindowColor);
            mat.SetColor("_Color", queueWindowColor);
            mat.SetColor(EmissionColorId, queueWindowColor * queueWindowIntensity);
            glowRenderer.sharedMaterial = mat;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            glowParticles = CreateDefaultParticles(glowObj.transform);
        }

        private ParticleSystem CreateDefaultParticles(Transform parent)
        {
            var psObj = new GameObject("MeleeTipGlowParticles");
            psObj.transform.SetParent(parent, false);
            var ps = psObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 0.08f;
            main.startSize = particleSize;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = particleShapeRadius;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static Mesh CreateSphereMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }
    }
}

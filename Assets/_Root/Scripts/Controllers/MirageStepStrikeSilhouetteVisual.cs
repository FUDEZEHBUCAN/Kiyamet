using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Mirage Step vuruş anlarında duelist pozunu çizgisel (içi boş) silüet olarak bırakır ve fade out eder.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirageStepStrikeSilhouetteVisual : MonoBehaviour
    {
        private const string OutlineShaderName = "Kiyamet/MirageSilhouetteOutline";

        [SerializeField] private DuelistUltimateController controller;
        [SerializeField] private SkinnedMeshRenderer[] skinnedRenderers;
        [SerializeField] private Color[] silhouetteColors =
        {
            new Color(0.35f, 0.92f, 1f, 1f),
            new Color(0.55f, 0.48f, 1f, 1f),
            new Color(1f, 0.42f, 0.82f, 1f),
            new Color(1f, 0.72f, 0.28f, 1f),
            new Color(0.42f, 1f, 0.72f, 1f),
            new Color(0.95f, 0.38f, 1f, 1f)
        };
        [SerializeField] [Range(0f, 1f)] private float silhouetteAlpha = 0.55f;
        [SerializeField] private float emissionStrength = 1.2f;
        [SerializeField] private float outlineWidth = 0.016f;
        [SerializeField] private float lifetime = 0.85f;
        [SerializeField] private float fadeInDuration = 0.12f;
        [SerializeField] private float fadeDuration = 0.65f;
        [SerializeField] private float poseCaptureDelay = 0.07f;
        [SerializeField] private bool spawnOnSpinFinale = true;

        private int _lastStrikeSequence;
        private Shader _outlineShader;
        private Material _outlineMaterialTemplate;
        private readonly List<ActiveSilhouette> _activeSilhouettes = new();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private sealed class ActiveSilhouette
        {
            public GameObject Root;
            public Material Material;
            public Color BaseColor;
            public float SpawnTime;
        }

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<DuelistUltimateController>();

            if (skinnedRenderers == null || skinnedRenderers.Length == 0)
                skinnedRenderers = CollectBodyRenderers();

            _outlineShader = Shader.Find(OutlineShaderName);
            if (_outlineShader == null)
            {
                Debug.LogWarning($"[MirageStepStrikeSilhouetteVisual] Shader bulunamadı: {OutlineShaderName}");
                return;
            }

            _outlineMaterialTemplate = new Material(_outlineShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _outlineMaterialTemplate.SetFloat(OutlineWidthId, outlineWidth);
        }

        private void Update()
        {
            UpdateSilhouetteFade();
        }

        private void LateUpdate()
        {
            if (controller == null)
                return;

            if (controller.StrikeVisualSequence > _lastStrikeSequence)
            {
                int strikeSequence = controller.StrikeVisualSequence;
                _lastStrikeSequence = strikeSequence;
                if (spawnOnSpinFinale || controller.Phase != DuelistUltimateController.MirageStepPhase.Spin)
                    StartCoroutine(SpawnSilhouetteAfterPoseApplied(strikeSequence));
            }
        }

        private SkinnedMeshRenderer[] CollectBodyRenderers()
        {
            var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var filtered = new List<SkinnedMeshRenderer>();

            foreach (var renderer in all)
            {
                if (renderer == null)
                    continue;

                string name = renderer.name;
                if (name.Contains("Weapon") || name.Contains("FX") || name.Contains("VFX"))
                    continue;

                filtered.Add(renderer);
            }

            return filtered.Count > 0 ? filtered.ToArray() : all;
        }

        private IEnumerator SpawnSilhouetteAfterPoseApplied(int strikeSequence)
        {
            yield return new WaitForEndOfFrame();

            if (poseCaptureDelay > 0f)
                yield return new WaitForSeconds(poseCaptureDelay);

            if (controller == null)
                yield break;

            SpawnSilhouetteAtCurrentPose(strikeSequence);
        }

        private Color ResolveSilhouetteColor(int strikeSequence)
        {
            if (silhouetteColors == null || silhouetteColors.Length == 0)
                return new Color(0.35f, 0.92f, 1f, 1f);

            int index = Mathf.Max(0, strikeSequence - 1) % silhouetteColors.Length;
            Color color = silhouetteColors[index];
            color.a *= silhouetteAlpha;
            return color;
        }

        private void SpawnSilhouetteAtCurrentPose(int strikeSequence)
        {
            if (skinnedRenderers == null || skinnedRenderers.Length == 0 || _outlineShader == null)
                return;

            Color silhouetteColor = ResolveSilhouetteColor(strikeSequence);
            if (silhouetteColor.a <= 0.001f)
                return;

            var root = new GameObject("MirageStrikeSilhouette");
            root.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (!TryBuildCombinedOutlineMesh(root.transform, out Mesh combinedMesh))
            {
                Destroy(root);
                return;
            }

            var meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedMesh;

            var meshRenderer = root.AddComponent<MeshRenderer>();
            Material mat = CreateOutlineMaterialInstance(silhouetteColor);
            meshRenderer.sharedMaterial = mat;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            _activeSilhouettes.Add(new ActiveSilhouette
            {
                Root = root,
                Material = mat,
                BaseColor = silhouetteColor,
                SpawnTime = Time.time
            });
        }

        private bool TryBuildCombinedOutlineMesh(Transform root, out Mesh combinedMesh)
        {
            combinedMesh = null;
            var combines = new List<CombineInstance>();
            var tempMeshes = new List<Mesh>();
            var bakedMesh = new Mesh();
            Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

            foreach (var sourceRenderer in skinnedRenderers)
            {
                if (sourceRenderer == null || !sourceRenderer.gameObject.activeInHierarchy)
                    continue;

                bakedMesh.Clear();
                sourceRenderer.BakeMesh(bakedMesh);
                if (bakedMesh.vertexCount == 0)
                    continue;

                var meshCopy = Instantiate(bakedMesh);
                tempMeshes.Add(meshCopy);
                RecalculateNormalsForOutline(meshCopy);

                combines.Add(new CombineInstance
                {
                    mesh = meshCopy,
                    transform = rootWorldToLocal * sourceRenderer.transform.localToWorldMatrix
                });
            }

            if (combines.Count == 0)
            {
                foreach (var mesh in tempMeshes)
                    Destroy(mesh);
                return false;
            }

            combinedMesh = new Mesh { name = "MirageStrikeSilhouetteMesh" };
            combinedMesh.CombineMeshes(combines.ToArray(), true, true);
            RecalculateNormalsForOutline(combinedMesh);

            foreach (var mesh in tempMeshes)
                Destroy(mesh);

            return combinedMesh.vertexCount > 0;
        }

        private static void RecalculateNormalsForOutline(Mesh mesh)
        {
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void UpdateSilhouetteFade()
        {
            if (_activeSilhouettes.Count == 0)
                return;

            float now = Time.time;

            for (int i = _activeSilhouettes.Count - 1; i >= 0; i--)
            {
                var silhouette = _activeSilhouettes[i];
                if (silhouette.Root == null)
                {
                    _activeSilhouettes.RemoveAt(i);
                    continue;
                }

                float age = now - silhouette.SpawnTime;
                float intensity = ComputeIntensityMultiplier(age);
                float alpha = silhouette.BaseColor.a * intensity;
                ApplyColor(silhouette.Material, silhouette.BaseColor, alpha);

                if (age >= lifetime)
                    DestroySilhouette(silhouette, i);
            }
        }

        private float ComputeIntensityMultiplier(float age)
        {
            float fadeIn = ComputeFadeInMultiplier(age);
            float fadeOut = ComputeFadeOutMultiplier(age);
            return fadeIn * fadeOut;
        }

        private float ComputeFadeInMultiplier(float age)
        {
            if (age <= 0f || fadeInDuration <= 0.001f)
                return fadeInDuration <= 0.001f ? 1f : 0f;

            float t = Mathf.Clamp01(age / fadeInDuration);
            return EaseOutCubic(t);
        }

        private float ComputeFadeOutMultiplier(float age)
        {
            if (age <= 0f)
                return 1f;

            float fadeStart = Mathf.Max(0f, lifetime - fadeDuration);
            if (age <= fadeStart)
                return 1f;

            float fadeT = fadeDuration > 0.001f
                ? Mathf.Clamp01((age - fadeStart) / fadeDuration)
                : 1f;
            return 1f - fadeT;
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private void ApplyColor(Material material, Color baseColor, float alpha)
        {
            if (material == null)
                return;

            alpha = Mathf.Clamp01(alpha);
            var color = new Color(
                baseColor.r * emissionStrength,
                baseColor.g * emissionStrength,
                baseColor.b * emissionStrength,
                alpha);
            material.SetColor(ColorId, color);
        }

        private void DestroySilhouette(ActiveSilhouette silhouette, int index)
        {
            if (silhouette.Root != null)
            {
                var meshFilter = silhouette.Root.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    Destroy(meshFilter.sharedMesh);

                Destroy(silhouette.Root);
            }

            if (silhouette.Material != null)
                Destroy(silhouette.Material);

            _activeSilhouettes.RemoveAt(index);
        }

        private Material CreateOutlineMaterialInstance(Color baseColor)
        {
            Material mat = _outlineMaterialTemplate != null
                ? new Material(_outlineMaterialTemplate)
                : new Material(_outlineShader);
            mat.SetFloat(OutlineWidthId, outlineWidth);
            ApplyColor(mat, baseColor, 0f);
            return mat;
        }

        private void OnDestroy()
        {
            if (_outlineMaterialTemplate != null)
            {
                Destroy(_outlineMaterialTemplate);
                _outlineMaterialTemplate = null;
            }

            for (int i = _activeSilhouettes.Count - 1; i >= 0; i--)
                DestroySilhouette(_activeSilhouettes[i], i);
        }
    }
}

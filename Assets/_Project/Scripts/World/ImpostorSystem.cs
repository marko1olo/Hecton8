// ============================================================================
// HECTON-8 — ImpostorSystem.cs
// Manages impostor billboard generation and rendering for very distant objects.
//
// RESPONSIBILITIES:
//   • Register/unregister impostor candidates
//   • Build runtime billboard materials from scene-owned source renderers
//   • Spawn/despawn billboard instances via ObjectPoolManager
//   • Activate impostors at distance threshold (150m default)
//   • Stabilize impostor transitions with hysteresis and adaptive threshold scaling
//
// ARCHITECTURE:
//   • Singleton via ImpostorSystem.Instance
//   • ITickable — registers with GameTickManager
//   • Zero-GC — pre-allocated collections, struct-based data
//   • Scene-owned fallback material derivation (no runtime Addressables dependency)
//   • ObjectPoolManager for billboard pooling
//
// PERFORMANCE:
//   • Target: < 0.5ms per frame
//   • Zero GC allocations
//   • Cold-path material derivation only
//
// INTEGRATION:
//   • GameTickManager — ITickable registration
//   • Amplify Impostors — offline texture baking (Editor-only)
//   • DynamicResolutionScaler — adaptive weak-device impostor response
//   • ObjectPoolManager — billboard spawning
//
// AMPLIFY IMPOSTORS WORKFLOW:
//   1. Editor: Bake impostor textures via AmplifyImpostor component
//   2. Runtime: Derive billboard material from the candidate's primary shared material
//   3. Runtime: Spawn billboard via ObjectPoolManager
//   4. Runtime: Rotate/scale billboard from cached source bounds
//   5. Runtime: Shift thresholds from quality preset + dynamic resolution pressure
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Manages impostor billboard rendering for very distant objects.
    /// Uses scene-owned source renderers to build billboard fallback materials at registration time.
    /// </summary>
    /// <remarks>
    /// ZERO-GC ARCHITECTURE:
    ///   • Pre-allocated collections with capacity
    ///   • Struct-based ImpostorInstance data
    ///   • No LINQ, no string operations in hot paths
    ///   • No runtime Addressables dependency
    ///   • ITickable for distance-based activation
    /// 
    /// PERFORMANCE TARGET:
    ///   • Impostor processing: < 0.5ms per frame
    ///   • Supports 100+ active impostors
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class ImpostorSystem : MonoBehaviour, ITickable
    {
        private const float MinimumBillboardWidth = 0.25f;
        private const float MinimumBillboardHeight = 0.25f;
        private const float CameraResolveRetryInterval = 1f;
        private static readonly int _baseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _mainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _bumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int _normalMapId = Shader.PropertyToID("_NormalMap");

        private static ImpostorSystem _instance;

        [Header("Impostor Configuration")]
        [SerializeField, Tooltip("Distance threshold for impostor activation.")]
        private float _impostorDistanceThreshold = 150f;

        [SerializeField, Tooltip("Billboard prefab for impostor rendering.")]
        private GameObject _billboardPrefab;

        [SerializeField, Tooltip("Keeps impostors active slightly longer than the entry threshold so they do not thrash at the distance boundary.")]
        private float _impostorExitDistancePaddingPercent = 10f;

        [SerializeField, Tooltip("Scales impostor entry distance down when dynamic resolution already collapsed, so weak hardware moves into cheaper billboards earlier.")]
        private bool _enableAdaptiveThresholdScaling = true;

        [SerializeField, Tooltip("Minimum threshold multiplier applied when DynamicResolutionScaler render scale is at its weakest.")]
        private float _minAdaptiveThresholdMultiplier = 0.72f;

        [SerializeField, Tooltip("Low preset threshold multiplier. Smaller means impostors engage earlier for performance.")]
        private float _lowQualityThresholdMultiplier = 0.85f;

        [SerializeField, Tooltip("High preset threshold multiplier. Larger means impostors engage later for stronger visuals.")]
        private float _highQualityThresholdMultiplier = 1.15f;

        [SerializeField, Tooltip("Optional explicit main camera reference. Falls back to cold-path camera resolve.")]
        private Camera _cameraReference;

        [SerializeField, Tooltip("Fallback billboard shader assigned at authoring time. Used when source renderers do not expose a usable shared material.")]
        private Shader _fallbackBillboardShader;

        private struct ImpostorInstance
        {
            public GameObject OriginalObject;
            public Transform OriginalTransform;
            public GameObject BillboardObject;
            public Renderer BillboardRenderer;
            public EntityId ImpostorID;
            public float ActivationDistanceSqr;
            public float DeactivationDistanceSqr;
            public Vector3 BillboardCenterOffset;
            public Vector3 BillboardScale;
            public Renderer[] ManagedRenderers;
            public bool[] OriginalForceRenderingOffStates;
            public bool IsActive;
        }

        private struct ImpostorTextureData
        {
            public Texture2D AlbedoTexture;
            public Texture2D NormalTexture;
            public Material ImpostorMaterial;
            public bool UsesFallbackMaterial;
            public bool IsLoaded;
        }

        // COLD ALLOC: Dictionary<EntityId, GameObject>[100] — impostor billboard lookup — owner: ImpostorSystem
        private readonly Dictionary<EntityId, GameObject> _impostorBillboards = new Dictionary<EntityId, GameObject>(100);
        // COLD ALLOC: List<ImpostorInstance>[100] — active impostor instances — owner: ImpostorSystem
        private readonly List<ImpostorInstance> _activeImpostors = new List<ImpostorInstance>(100);
        // COLD ALLOC: HashSet<GameObject>[100] — registered candidate lookup — owner: ImpostorSystem
        private readonly HashSet<GameObject> _registeredCandidates = new HashSet<GameObject>();
        // COLD ALLOC: Dictionary<EntityId, ImpostorTextureData>[100] — impostor texture cache — owner: ImpostorSystem
        private readonly Dictionary<EntityId, ImpostorTextureData> _textureCache = new Dictionary<EntityId, ImpostorTextureData>(100);
        // COLD ALLOC: List<Renderer>[32] — renderer registration scratch buffer — owner: ImpostorSystem
        private readonly List<Renderer> _rendererScratch = new List<Renderer>(32);

        private Camera _mainCamera;
        private Transform _cameraTransform;
        private float _cameraResolveRetryTimer;
        private bool _registered;

        /// <summary>
        /// Singleton instance. Null when the system is absent.
        /// </summary>
        public static ImpostorSystem Instance => _instance;

        /// <summary>
        /// Count of currently active impostor instances.
        /// </summary>
        public int ActiveImpostorCount => _activeImpostors.Count;

        /// <summary>
        /// Distance threshold for impostor activation.
        /// </summary>
        public float ImpostorDistanceThreshold => _impostorDistanceThreshold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ImpostorSystem] Duplicate instance detected. Destroying duplicate.");
#endif
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            RestoreAllOriginalVisibility();
            TryUnregister();
        }

        private void OnDestroy()
        {
            RestoreAllOriginalVisibility();
            TryUnregister();

            ReleaseCachedMaterials();
            _impostorBillboards.Clear();
            _activeImpostors.Clear();
            _registeredCandidates.Clear();
            _textureCache.Clear();

            if (_instance == this)
                _instance = null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registered = false;
        }

        /// <summary>
        /// Updates impostor activation against the cached camera position.
        /// </summary>
        public void Tick(float dt)
        {
            if (!TryResolveCamera(dt))
                return;

            float thresholdScale = ResolveThresholdScale();
            float thresholdScaleSqr = thresholdScale * thresholdScale;
            Vector3 cameraPosition = _cameraTransform.position;
            for (int i = _activeImpostors.Count - 1; i >= 0; i--)
            {
                ImpostorInstance instance = _activeImpostors[i];
                if (instance.OriginalObject == null)
                {
                    DespawnBillboard(ref instance);

                    _activeImpostors.RemoveAt(i);
                    continue;
                }

                Transform originalTransform = instance.OriginalTransform != null
                    ? instance.OriginalTransform
                    : instance.OriginalObject.transform;
                Vector3 originalPosition = originalTransform.position;
                float sqrDistance = (originalPosition - cameraPosition).sqrMagnitude;
                float activationDistanceSqr = instance.ActivationDistanceSqr * thresholdScaleSqr;
                float deactivationDistanceSqr = instance.DeactivationDistanceSqr * thresholdScaleSqr;

                if (instance.IsActive && instance.BillboardObject == null)
                {
                    RestoreOriginalVisibility(ref instance);
                    instance.IsActive = false;
                }

                if (sqrDistance > activationDistanceSqr)
                {
                    if (!instance.IsActive)
                    {
                        ActivateImpostor(ref instance);
                        _activeImpostors[i] = instance;
                    }
                    else if (instance.BillboardObject != null)
                    {
                        UpdateBillboardTransform(ref instance, originalPosition);
                    }
                }
                else if (instance.IsActive && sqrDistance < deactivationDistanceSqr)
                {
                    DeactivateImpostor(ref instance);
                    _activeImpostors[i] = instance;
                }
            }
        }

        /// <summary>
        /// Registers a GameObject as an impostor candidate.
        /// </summary>
        /// <param name="obj">Candidate owner.</param>
        /// <param name="lodGroup">Unused placeholder for future LOD integration.</param>
        public void RegisterImpostorCandidate(GameObject obj, LODGroup lodGroup = null)
        {
            if (obj == null)
                return;

            if (!_registeredCandidates.Add(obj))
                return;

            EntityId impostorID = obj.GetEntityId();
            if (_textureCache.ContainsKey(impostorID))
            {
                if (!TryAddImpostorInstance(obj, impostorID, lodGroup))
                    _registeredCandidates.Remove(obj);
                return;
            }

            if (!TryBuildImpostorData(obj, impostorID))
            {
                _registeredCandidates.Remove(obj);
                return;
            }

            if (!TryAddImpostorInstance(obj, impostorID, lodGroup))
            {
                _registeredCandidates.Remove(obj);
                if (_textureCache.TryGetValue(impostorID, out ImpostorTextureData failedData))
                {
                    if (failedData.ImpostorMaterial != null)
                        Destroy(failedData.ImpostorMaterial);

                    _textureCache.Remove(impostorID);
                }
            }
        }

        /// <summary>
        /// Unregisters a GameObject from impostor ownership.
        /// </summary>
        /// <param name="obj">Candidate owner.</param>
        public void UnregisterImpostorCandidate(GameObject obj)
        {
            if (obj == null)
                return;

            if (!_registeredCandidates.Remove(obj))
                return;

            EntityId impostorID = obj.GetEntityId();
            RemoveImpostorInstance(impostorID);

            if (_textureCache.TryGetValue(impostorID, out ImpostorTextureData data))
            {
                if (data.ImpostorMaterial != null)
                    Destroy(data.ImpostorMaterial);

                _textureCache.Remove(impostorID);
            }
        }

        private bool TryResolveCamera(float dt)
        {
            if (_cameraTransform != null)
                return true;

            if (_cameraResolveRetryTimer > 0f)
            {
                _cameraResolveRetryTimer -= Mathf.Max(0f, dt);
                return false;
            }

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            _mainCamera = _cameraReference;
            if (_mainCamera == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (!playerTransform.TryGetComponent(out _mainCamera))
                    _mainCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
            }

            if (_mainCamera == null)
                return false;

            _cameraTransform = _mainCamera.transform;
            return true;
        }

        private void ActivateImpostor(ref ImpostorInstance instance)
        {
            if (!_textureCache.TryGetValue(instance.ImpostorID, out ImpostorTextureData data) || !data.IsLoaded)
                return;

            if (_billboardPrefab == null || ObjectPoolManager.Instance == null)
                return;

            GameObject originalObject = instance.OriginalObject;
            if (originalObject == null)
                return;

            GameObject billboard = ObjectPoolManager.Instance.Spawn(
                _billboardPrefab,
                instance.OriginalTransform.position,
                Quaternion.identity);
            if (billboard == null)
                return;

            Renderer renderer = instance.BillboardRenderer;
            if (renderer == null && !billboard.TryGetComponent(out renderer))
                renderer = billboard.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = data.ImpostorMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.forceRenderingOff = false;
            }

            SuppressOriginalVisibility(ref instance);
            instance.BillboardObject = billboard;
            instance.BillboardRenderer = renderer;
            instance.IsActive = true;
            _impostorBillboards[instance.ImpostorID] = billboard;
            UpdateBillboardTransform(ref instance, instance.OriginalTransform.position);
        }

        private void DeactivateImpostor(ref ImpostorInstance instance)
        {
            DespawnBillboard(ref instance);
            RestoreOriginalVisibility(ref instance);
            instance.IsActive = false;
        }

        private bool TryBuildImpostorData(GameObject obj, EntityId impostorID)
        {
            if (obj == null)
                return false;

            Material sourceMaterial = null;
            Texture2D albedoTexture = null;
            Texture2D normalTexture = null;
            bool usesFallbackMaterial = false;

            if (!TryResolvePrimaryMaterial(obj, out sourceMaterial, out albedoTexture, out normalTexture))
            {
                sourceMaterial = BuildFallbackBillboardMaterial();
                usesFallbackMaterial = sourceMaterial != null;
            }

            if (sourceMaterial == null)
                return false;

            Material impostorMaterial = usesFallbackMaterial
                ? sourceMaterial
                : new Material(sourceMaterial);
            impostorMaterial.enableInstancing = true;

            _textureCache[impostorID] = new ImpostorTextureData
            {
                AlbedoTexture = albedoTexture,
                NormalTexture = normalTexture,
                ImpostorMaterial = impostorMaterial,
                UsesFallbackMaterial = usesFallbackMaterial,
                IsLoaded = true
            };

            return true;
        }

        private bool TryAddImpostorInstance(GameObject obj, EntityId impostorID, LODGroup lodGroup)
        {
            for (int i = 0; i < _activeImpostors.Count; i++)
            {
                if (_activeImpostors[i].ImpostorID == impostorID)
                    return true;
            }

            ImpostorTextureData data = _textureCache[impostorID];
            if (!data.IsLoaded)
                return false;

            if (!TryCacheManagedRenderers(obj, out Renderer[] managedRenderers, out bool[] originalForceRenderingOffStates))
                return false;

            if (!TryCalculateBillboardPresentation(managedRenderers, obj.transform.position, out Vector3 billboardCenterOffset, out Vector3 billboardScale))
                return false;

            float activationDistanceSqr = _impostorDistanceThreshold * _impostorDistanceThreshold;
            float exitPaddingScale = Mathf.Max(1f, 1f + (_impostorExitDistancePaddingPercent * 0.01f));

            ImpostorInstance instance = new ImpostorInstance
            {
                OriginalObject = obj,
                OriginalTransform = obj.transform,
                BillboardObject = null,
                BillboardRenderer = null,
                ImpostorID = impostorID,
                ActivationDistanceSqr = activationDistanceSqr,
                DeactivationDistanceSqr = activationDistanceSqr / (exitPaddingScale * exitPaddingScale),
                BillboardCenterOffset = billboardCenterOffset,
                BillboardScale = billboardScale,
                ManagedRenderers = managedRenderers,
                OriginalForceRenderingOffStates = originalForceRenderingOffStates,
                IsActive = false
            };
            _activeImpostors.Add(instance);
            return true;
        }

        private void RemoveImpostorInstance(EntityId impostorID)
        {
            for (int i = _activeImpostors.Count - 1; i >= 0; i--)
            {
                if (_activeImpostors[i].ImpostorID != impostorID)
                    continue;

                ImpostorInstance instance = _activeImpostors[i];
                if (instance.IsActive)
                {
                    DeactivateImpostor(ref instance);
                }
                else
                {
                    RestoreOriginalVisibility(ref instance);
                }

                _activeImpostors.RemoveAt(i);
                return;
            }
        }

        private bool TryCacheManagedRenderers(GameObject obj, out Renderer[] managedRenderers, out bool[] originalForceRenderingOffStates)
        {
            managedRenderers = null;
            originalForceRenderingOffStates = null;

            if (obj == null)
                return false;

            _rendererScratch.Clear();
            obj.transform.GetComponentsInChildren(true, _rendererScratch);

            int validRendererCount = 0;
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                if (_rendererScratch[i] != null)
                    validRendererCount++;
            }

            if (validRendererCount == 0)
                return false;

            managedRenderers = new Renderer[validRendererCount];
            originalForceRenderingOffStates = new bool[validRendererCount];

            int writeIndex = 0;
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                Renderer renderer = _rendererScratch[i];
                if (renderer == null)
                    continue;

                managedRenderers[writeIndex] = renderer;
                originalForceRenderingOffStates[writeIndex] = renderer.forceRenderingOff;
                writeIndex++;
            }

            return true;
        }

        private void SuppressOriginalVisibility(ref ImpostorInstance instance)
        {
            if (instance.ManagedRenderers == null)
                return;

            for (int i = 0; i < instance.ManagedRenderers.Length; i++)
            {
                Renderer renderer = instance.ManagedRenderers[i];
                if (renderer == null)
                    continue;

                renderer.forceRenderingOff = true;
            }
        }

        private void RestoreOriginalVisibility(ref ImpostorInstance instance)
        {
            if (instance.ManagedRenderers == null || instance.OriginalForceRenderingOffStates == null)
                return;

            int restoreCount = Mathf.Min(instance.ManagedRenderers.Length, instance.OriginalForceRenderingOffStates.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                Renderer renderer = instance.ManagedRenderers[i];
                if (renderer == null)
                    continue;

                renderer.forceRenderingOff = instance.OriginalForceRenderingOffStates[i];
            }
        }

        private void DespawnBillboard(ref ImpostorInstance instance)
        {
            if (instance.BillboardObject != null)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Despawn(instance.BillboardObject);
                else
                    instance.BillboardObject.SetActive(false);
            }

            _impostorBillboards.Remove(instance.ImpostorID);
            instance.BillboardObject = null;
            instance.BillboardRenderer = null;
        }

        private void RestoreAllOriginalVisibility()
        {
            for (int i = _activeImpostors.Count - 1; i >= 0; i--)
            {
                ImpostorInstance instance = _activeImpostors[i];
                if (instance.IsActive)
                    DespawnBillboard(ref instance);

                RestoreOriginalVisibility(ref instance);
                _activeImpostors[i] = instance;
            }
        }

        private void ReleaseCachedMaterials()
        {
            Dictionary<EntityId, ImpostorTextureData>.Enumerator enumerator = _textureCache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Material material = enumerator.Current.Value.ImpostorMaterial;
                if (material != null)
                    Destroy(material);
            }
        }

        private void UpdateBillboardTransform(ref ImpostorInstance instance, Vector3 originalPosition)
        {
            GameObject billboardObject = instance.BillboardObject;
            if (billboardObject == null || _cameraTransform == null)
                return;

            Transform billboardTransform = billboardObject.transform;
            Vector3 billboardPosition = originalPosition + instance.BillboardCenterOffset;
            Quaternion billboardRotation = Quaternion.LookRotation(-_cameraTransform.forward, _cameraTransform.up);
            billboardTransform.SetPositionAndRotation(billboardPosition, billboardRotation);
            billboardTransform.localScale = instance.BillboardScale;
        }

        private float ResolveThresholdScale()
        {
            float qualityScale = 1f;
            LODSystemManager lodSystemManager = LODSystemManager.Instance;
            if (lodSystemManager != null)
            {
                switch (lodSystemManager.QualityPreset)
                {
                    case LODQualityPreset.Low:
                        qualityScale = Mathf.Max(0.1f, _lowQualityThresholdMultiplier);
                        break;

                    case LODQualityPreset.High:
                        qualityScale = Mathf.Max(0.1f, _highQualityThresholdMultiplier);
                        break;
                }
            }

            if (!_enableAdaptiveThresholdScaling)
                return qualityScale;

            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            if (scaler == null)
                return qualityScale;

            float renderScale = Mathf.Clamp01(scaler.CurrentRenderScale);
            float adaptiveScale = Mathf.Lerp(
                Mathf.Clamp(_minAdaptiveThresholdMultiplier, 0.1f, 1f),
                1f,
                renderScale);

            return qualityScale * adaptiveScale;
        }

        private bool TryResolvePrimaryMaterial(GameObject obj, out Material sourceMaterial, out Texture2D albedoTexture, out Texture2D normalTexture)
        {
            sourceMaterial = null;
            albedoTexture = null;
            normalTexture = null;

            if (obj == null)
                return false;

            _rendererScratch.Clear();
            obj.transform.GetComponentsInChildren(true, _rendererScratch);
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                Renderer renderer = _rendererScratch[i];
                if (renderer == null)
                    continue;

                Material sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null)
                    continue;

                sourceMaterial = sharedMaterial;
                albedoTexture = TryResolveTexture(sharedMaterial, _baseMapId, _mainTexId);
                normalTexture = TryResolveTexture(sharedMaterial, _bumpMapId, _normalMapId);
                return true;
            }

            return false;
        }

        private static Texture2D TryResolveTexture(Material material, int primaryPropertyId, int secondaryPropertyId)
        {
            if (material == null)
                return null;

            if (material.HasProperty(primaryPropertyId))
                return material.GetTexture(primaryPropertyId) as Texture2D;

            if (material.HasProperty(secondaryPropertyId))
                return material.GetTexture(secondaryPropertyId) as Texture2D;

            return null;
        }

        private Material BuildFallbackBillboardMaterial()
        {
            Shader shader = ResolveFallbackBillboardShader();
            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.enableInstancing = true;

            if (material.HasProperty(_baseColorId))
                material.SetColor(_baseColorId, Color.white);
            else if (material.HasProperty(_colorId))
                material.SetColor(_colorId, Color.white);

            return material;
        }

        private Shader ResolveFallbackBillboardShader()
        {
            if (_fallbackBillboardShader != null)
                return _fallbackBillboardShader;

            RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Material defaultMaterial = renderPipeline != null ? renderPipeline.defaultMaterial : null;
            return defaultMaterial != null ? defaultMaterial.shader : null;
        }

        private static bool TryCalculateBillboardPresentation(
            Renderer[] managedRenderers,
            Vector3 originPosition,
            out Vector3 billboardCenterOffset,
            out Vector3 billboardScale)
        {
            billboardCenterOffset = Vector3.zero;
            billboardScale = Vector3.one;

            if (managedRenderers == null || managedRenderers.Length == 0)
                return false;

            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < managedRenderers.Length; i++)
            {
                Renderer renderer = managedRenderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                combinedBounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
                return false;

            Vector3 size = combinedBounds.size;
            float width = Mathf.Max(MinimumBillboardWidth, Mathf.Max(size.x, size.z));
            float height = Mathf.Max(MinimumBillboardHeight, size.y);
            billboardCenterOffset = combinedBounds.center - originPosition;
            billboardScale = new Vector3(width, height, 1f);
            return true;
        }

#if UNITY_EDITOR && HECTON8_AMPLIFY_IMPOSTORS
        [MenuItem("Hecton8/LOD System/Verify Amplify Impostors")]
        private static void VerifyAmplifyImpostors()
        {
            Shader impostorShader = FindAmplifyImpostorShaderAsset();
            if (impostorShader != null)
                Debug.Log($"[ImpostorSystem] Impostor shader found: {impostorShader.name}");
            else
                Debug.LogWarning("[ImpostorSystem] Amplify impostor package or shader not found.");
        }

        [MenuItem("Hecton8/LOD System/Create Impostor Baking Preset")]
        private static void CreateImpostorBakingPreset()
        {
            Debug.LogWarning("[ImpostorSystem] Amplify impostor bake preset creation is unavailable because the Amplify package is not installed.");
        }

        [MenuItem("Hecton8/LOD System/Batch Bake Impostors")]
        private static void BatchBakeImpostors()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int bakedCount = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<LODGroup>() == null)
                    continue;

                if (prefab.GetComponent("AmplifyImpostor") == null)
                    continue;

                bakedCount++;
            }

            Debug.Log($"[ImpostorSystem] Batch bake scan complete. Candidates={bakedCount}");
        }
        private static Shader FindAmplifyImpostorShaderAsset()
        {
            string[] shaderGuids = AssetDatabase.FindAssets("Impostor Standard t:Shader");
            for (int i = 0; i < shaderGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(shaderGuids[i]);
                if (string.IsNullOrEmpty(assetPath) || assetPath.IndexOf("Amplify", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                if (shader != null)
                    return shader;
            }

            return null;
        }
#endif
    }
}

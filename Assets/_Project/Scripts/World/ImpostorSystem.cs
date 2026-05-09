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
//   • GlobalRegistry.Impostors is the authoritative runtime lookup.
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
using Hecton8.Gameplay;
using Unity.Mathematics;
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
        private const float DistantGeologyImpostorDistanceMeters = 5000f;
        private const int MaxHotPathImpostorsPerTick = 64;
        private const float AupDistanceThresholdMeters = 50f;
        private const float AupDistanceThresholdSqr = AupDistanceThresholdMeters * AupDistanceThresholdMeters;
        private static readonly int _baseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _legacyBaseMapId = Shader.PropertyToID("_Base_Map");
        private static readonly int _impostorAlbedoAtlasId = Shader.PropertyToID("_ImpostorAlbedoAtlas");
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _mainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _bumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int _normalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int _legacyNormalMapId = Shader.PropertyToID("_Normal_Map");
        private static readonly int _impostorNormalAtlasId = Shader.PropertyToID("_ImpostorNormalAtlas");

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

        [SerializeField, Tooltip("Silhouette-only atlas shader for geology HLOD cards beyond 5km. Assign this to prevent Shader.Find stripping in player builds.")]
        private Shader _distantGeologyBillboardShader;

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
            public bool OriginalActiveSelf;
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
        private readonly HashSet<GameObject> _registeredCandidates = new HashSet<GameObject>(100);
        // COLD ALLOC: Dictionary<EntityId, ImpostorTextureData>[100] — impostor texture cache — owner: ImpostorSystem
        private readonly Dictionary<EntityId, ImpostorTextureData> _textureCache = new Dictionary<EntityId, ImpostorTextureData>(100);
        // COLD ALLOC: Dictionary<EntityId, Renderer>[100] — pooled billboard renderer cache — owner: ImpostorSystem
        private readonly Dictionary<EntityId, Renderer> _billboardRendererCache = new Dictionary<EntityId, Renderer>(100);
        // COLD ALLOC: List<Renderer>[32] — renderer registration scratch buffer — owner: ImpostorSystem
        private readonly List<Renderer> _rendererScratch = new List<Renderer>(32);

        private Camera _mainCamera;
        private Transform _cameraTransform;
        private int _playerRuntimeContextCacheFrame = -1;
        private bool _playerRuntimeContextCacheValid;
        private PlayerRuntimeContext _playerRuntimeContextCache;
        private int _viewerAupCacheFrame = -1;
        private AbsoluteUniversePosition _viewerAupCache;
        private float _cameraResolveRetryTimer;
        private int _impostorTickCursor;
        private bool _registered;
        private bool _serviceRegistered;

        /// <summary>
        /// Registry-backed runtime instance. Null when the system is absent.
        /// </summary>
        public static ImpostorSystem Instance => GlobalRegistry.Impostors;

        /// <summary>
        /// Count of currently active impostor instances.
        /// </summary>
        public int ActiveImpostorCount => _activeImpostors.Count;

        /// <summary>
        /// Distance threshold for impostor activation.
        /// </summary>
        public float ImpostorDistanceThreshold => _impostorDistanceThreshold;

        /// <summary>
        /// Distance threshold for distant geology HLOD billboard activation.
        /// </summary>
        public float DistantGeologyImpostorDistanceThreshold => DistantGeologyImpostorDistanceMeters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        private void Awake()
        {
            ImpostorSystem registered = GlobalRegistry.Impostors;
            if (registered != null && registered != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ImpostorSystem] Duplicate registry owner detected. Destroying duplicate.");
#endif
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            InvalidatePlayerRuntimeCache();
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            RestoreAllOriginalVisibility();
            InvalidatePlayerRuntimeCache();
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            RestoreAllOriginalVisibility();
            InvalidatePlayerRuntimeCache();
            TryUnregister();
            TryUnregisterService();

            ReleaseCachedMaterials();
            _impostorBillboards.Clear();
            _activeImpostors.Clear();
            _registeredCandidates.Clear();
            _textureCache.Clear();
            _billboardRendererCache.Clear();

        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterImpostorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Impostors, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.Impostors, this))
                GlobalRegistry.UnregisterImpostorRuntime(this);

            _serviceRegistered = false;
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
            AbsoluteUniversePosition cameraAup = ResolveViewerAup();
            int batchCount = Mathf.Min(_activeImpostors.Count, MaxHotPathImpostorsPerTick);
            for (int processed = 0; processed < batchCount && _activeImpostors.Count > 0; processed++)
            {
                if (_impostorTickCursor >= _activeImpostors.Count)
                    _impostorTickCursor = 0;

                int i = _impostorTickCursor;
                ImpostorInstance instance = _activeImpostors[i];
                if (instance.OriginalObject == null)
                {
                    DespawnBillboard(ref instance);

                    _activeImpostors.RemoveAt(i);
                    if (_impostorTickCursor >= _activeImpostors.Count)
                        _impostorTickCursor = 0;

                    continue;
                }

                Transform originalTransform = instance.OriginalTransform != null
                    ? instance.OriginalTransform
                    : instance.OriginalObject.transform;
                Vector3 originalPosition = originalTransform.position;
                float sqrDistance = ResolveCameraDistanceSqr(in cameraAup, originalPosition);
                float activationDistanceSqr = instance.ActivationDistanceSqr * thresholdScaleSqr;
                float deactivationDistanceSqr = instance.DeactivationDistanceSqr * thresholdScaleSqr;

                if (instance.IsActive && instance.BillboardObject == null)
                {
                    ApplyOriginalObjectVisibility(ref instance, true);
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
                        UpdateBillboardTransform(ref instance, originalPosition, in cameraAup);
                    }
                }
                else if (instance.IsActive && sqrDistance < deactivationDistanceSqr)
                {
                    DeactivateImpostor(ref instance);
                    _activeImpostors[i] = instance;
                }

                _impostorTickCursor++;
            }
        }

        private static float ResolveCameraDistanceSqr(
            in AbsoluteUniversePosition cameraAup,
            Vector3 objectPosition)
        {
            AbsoluteUniversePosition objectAup = AbsoluteUniversePosition.FromRuntimePosition(objectPosition);
            double distanceSqr = AbsoluteUniversePosition.DistanceSq(in cameraAup, in objectAup);
            return distanceSqr >= float.MaxValue ? float.MaxValue : (float)distanceSqr;
        }

        /// <summary>
        /// Registers a GameObject as an impostor candidate.
        /// </summary>
        /// <param name="obj">Candidate owner.</param>
        /// <param name="lodGroup">Optional source LODGroup for cached presentation bounds.</param>
        public void RegisterImpostorCandidate(GameObject obj, LODGroup lodGroup = null)
        {
            RegisterImpostorCandidate(obj, lodGroup, _impostorDistanceThreshold, false);
        }

        /// <summary>
        /// Registers a geology HLOD candidate. Geometry switches to a pooled camera-facing billboard past 5km.
        /// Baked normal/albedo billboard materials are preferred; source-material fallback is cold-path only.
        /// </summary>
        /// <param name="obj">Candidate owner.</param>
        /// <param name="lodGroup">Optional source LODGroup for cached presentation bounds.</param>
        public void RegisterDistantGeologyImpostorCandidate(GameObject obj, LODGroup lodGroup = null)
        {
            RegisterImpostorCandidate(obj, lodGroup, DistantGeologyImpostorDistanceMeters, true);
        }

        private void RegisterImpostorCandidate(GameObject obj, LODGroup lodGroup, float activationDistanceMeters, bool useDistantGeologyMaterial)
        {
            if (obj == null)
                return;

            if (!_registeredCandidates.Add(obj))
                return;

            EntityId impostorID = obj.GetEntityId();
            if (_textureCache.ContainsKey(impostorID))
            {
                if (!TryAddImpostorInstance(obj, impostorID, lodGroup, activationDistanceMeters))
                    _registeredCandidates.Remove(obj);
                return;
            }

            if (!TryBuildImpostorData(obj, impostorID, useDistantGeologyMaterial))
            {
                _registeredCandidates.Remove(obj);
                return;
            }

            if (!TryAddImpostorInstance(obj, impostorID, lodGroup, activationDistanceMeters))
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
                _cameraResolveRetryTimer -= math.max(0f, dt);
                return false;
            }

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            _mainCamera = _cameraReference;
            if (_mainCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                _mainCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            if (_mainCamera == null &&
                TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                _mainCamera = runtimeContext.PlayerCamera;
            }

            if (_mainCamera == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerTransform.TryGetComponent(out _mainCamera);
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

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (_billboardPrefab == null || pool == null)
                return;

            GameObject originalObject = instance.OriginalObject;
            if (originalObject == null)
                return;

            GameObject billboard = pool.Spawn(
                _billboardPrefab,
                instance.OriginalTransform.position,
                Quaternion.identity);
            if (billboard == null)
                return;

            TryResolveBillboardRenderer(billboard, out Renderer renderer);
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

            instance.BillboardObject = billboard;
            instance.BillboardRenderer = renderer;
            instance.IsActive = true;
            _impostorBillboards[instance.ImpostorID] = billboard;
            AbsoluteUniversePosition cameraAup = ResolveViewerAup();
            UpdateBillboardTransform(ref instance, instance.OriginalTransform.position, in cameraAup);
            ApplyOriginalObjectVisibility(ref instance, false);
        }

        private void DeactivateImpostor(ref ImpostorInstance instance)
        {
            ApplyOriginalObjectVisibility(ref instance, true);
            DespawnBillboard(ref instance);
            instance.IsActive = false;
        }

        private bool TryResolveBillboardRenderer(GameObject billboard, out Renderer renderer)
        {
            renderer = null;
            if (billboard == null)
                return false;

            EntityId key = billboard.GetEntityId();
            if (_billboardRendererCache.TryGetValue(key, out renderer) && renderer != null)
                return true;

            if (!billboard.TryGetComponent(out renderer))
                renderer = null;

            _billboardRendererCache[key] = renderer;
            return renderer != null;
        }

        private bool TryBuildImpostorData(GameObject obj, EntityId impostorID, bool useDistantGeologyMaterial)
        {
            if (obj == null)
                return false;

            Material sourceMaterial = null;
            Texture2D albedoTexture = null;
            Texture2D normalTexture = null;
            bool usesFallbackMaterial = false;

            bool hasPrimaryMaterial = TryResolvePrimaryMaterial(
                obj,
                out sourceMaterial,
                out albedoTexture,
                out normalTexture,
                resolveNormalTexture: !useDistantGeologyMaterial);
            if (useDistantGeologyMaterial)
            {
                Material geologyMaterial = BuildDistantGeologyBillboardMaterial(sourceMaterial, albedoTexture);
                if (geologyMaterial == null)
                    return false;

                _textureCache[impostorID] = new ImpostorTextureData
                {
                    AlbedoTexture = albedoTexture,
                    NormalTexture = null,
                    ImpostorMaterial = geologyMaterial,
                    UsesFallbackMaterial = false,
                    IsLoaded = true
                };

                return true;
            }

            if (!hasPrimaryMaterial)
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

        private bool TryAddImpostorInstance(GameObject obj, EntityId impostorID, LODGroup lodGroup, float activationDistanceMeters)
        {
            for (int i = 0; i < _activeImpostors.Count; i++)
            {
                if (_activeImpostors[i].ImpostorID == impostorID)
                    return true;
            }

            ImpostorTextureData data = _textureCache[impostorID];
            if (!data.IsLoaded)
                return false;

            if (!TryCalculateBillboardPresentation(lodGroup, obj.transform, out Vector3 billboardCenterOffset, out Vector3 billboardScale))
                return false;

            float safeActivationDistance = Mathf.Max(1f, activationDistanceMeters);
            float activationDistanceSqr = safeActivationDistance * safeActivationDistance;
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
                OriginalActiveSelf = obj.activeSelf,
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
                    ApplyOriginalObjectVisibility(ref instance, true);
                }

                _activeImpostors.RemoveAt(i);
                return;
            }
        }

        private static void ApplyOriginalObjectVisibility(ref ImpostorInstance instance, bool visible)
        {
            GameObject originalObject = instance.OriginalObject;
            if (originalObject == null)
                return;

            bool targetActive = visible && instance.OriginalActiveSelf;
            if (originalObject.activeSelf != targetActive)
                originalObject.SetActive(targetActive);
        }

        private void DespawnBillboard(ref ImpostorInstance instance)
        {
            if (instance.BillboardObject != null)
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Despawn(instance.BillboardObject);
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

                ApplyOriginalObjectVisibility(ref instance, true);
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

        private void InvalidatePlayerRuntimeCache()
        {
            _playerRuntimeContextCacheFrame = -1;
            _playerRuntimeContextCacheValid = false;
            _playerRuntimeContextCache = default;
            _viewerAupCacheFrame = -1;
            _viewerAupCache = default;
        }

        private void UpdateBillboardTransform(
            ref ImpostorInstance instance,
            Vector3 originalPosition,
            in AbsoluteUniversePosition cameraAup)
        {
            GameObject billboardObject = instance.BillboardObject;
            if (billboardObject == null || _cameraTransform == null)
                return;

            Transform billboardTransform = billboardObject.transform;
            Vector3 billboardPosition = originalPosition + instance.BillboardCenterOffset;
            AbsoluteUniversePosition billboardAup = AbsoluteUniversePosition.FromRuntimePosition(billboardPosition);
            float3 cameraDeltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in cameraAup, in billboardAup);
            Vector3 cameraDelta = new Vector3(cameraDeltaAup.x, 0f, cameraDeltaAup.z);
            if (cameraDelta.sqrMagnitude <= 0.0001f)
                cameraDelta = billboardTransform.forward;
            if (cameraDelta.sqrMagnitude <= 0.0001f)
                cameraDelta = Vector3.forward;

            Quaternion billboardRotation = Quaternion.LookRotation(cameraDelta, Vector3.up);
            billboardTransform.SetPositionAndRotation(billboardPosition, billboardRotation);
            billboardTransform.localScale = instance.BillboardScale;
        }

        private AbsoluteUniversePosition ResolveViewerAup()
        {
            int frame = Time.frameCount;
            if (_viewerAupCacheFrame == frame)
                return _viewerAupCache;

            _viewerAupCacheFrame = frame;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                _viewerAupCache = playerMovement.CurrentAup;
                return _viewerAupCache;
            }

            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    _viewerAupCache = movementState.PredictedAup;
                    return _viewerAupCache;
                }
            }

            _viewerAupCache = _cameraTransform != null
                ? AbsoluteUniversePosition.FromRuntimePosition(_cameraTransform.position)
                : default;
            return _viewerAupCache;
        }

        private bool TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            int frame = Time.frameCount;
            if (_playerRuntimeContextCacheFrame != frame)
            {
                _playerRuntimeContextCacheFrame = frame;
                _playerRuntimeContextCacheValid =
                    PlayerRuntimeContextService.TryGetActiveRuntimeContext(out _playerRuntimeContextCache) &&
                    _playerRuntimeContextCache != null;
                if (!_playerRuntimeContextCacheValid)
                    _playerRuntimeContextCache = default;
            }

            runtimeContext = _playerRuntimeContextCache;
            return _playerRuntimeContextCacheValid;
        }

        private float ResolveThresholdScale()
        {
            float qualityScale = 1f;
            LODSystemManager lodSystemManager = GlobalRegistry.LODSystem;
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

            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
            if (scaler == null)
                return qualityScale;

            float renderScale = math.saturate(scaler.CurrentRenderScale);
            float adaptiveScale = math.lerp(
                math.clamp(_minAdaptiveThresholdMultiplier, 0.1f, 1f),
                1f,
                renderScale);

            return qualityScale * adaptiveScale;
        }

        private bool TryResolvePrimaryMaterial(
            GameObject obj,
            out Material sourceMaterial,
            out Texture2D albedoTexture,
            out Texture2D normalTexture,
            bool resolveNormalTexture = true)
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
                albedoTexture = TryResolveTexture(sharedMaterial, _impostorAlbedoAtlasId, _baseMapId, _legacyBaseMapId, _mainTexId);
                normalTexture = resolveNormalTexture
                    ? TryResolveTexture(sharedMaterial, _impostorNormalAtlasId, _bumpMapId, _normalMapId, _legacyNormalMapId)
                    : null;
                return true;
            }

            return false;
        }

        private static Texture2D TryResolveTexture(
            Material material,
            int primaryPropertyId,
            int secondaryPropertyId,
            int tertiaryPropertyId,
            int quaternaryPropertyId)
        {
            if (material == null)
                return null;

            if (material.HasProperty(primaryPropertyId))
                return material.GetTexture(primaryPropertyId) as Texture2D;

            if (material.HasProperty(secondaryPropertyId))
                return material.GetTexture(secondaryPropertyId) as Texture2D;

            if (material.HasProperty(tertiaryPropertyId))
                return material.GetTexture(tertiaryPropertyId) as Texture2D;

            if (material.HasProperty(quaternaryPropertyId))
                return material.GetTexture(quaternaryPropertyId) as Texture2D;

            return null;
        }

        private Material BuildDistantGeologyBillboardMaterial(Material sourceMaterial, Texture2D albedoTexture)
        {
            Shader shader = ResolveDistantGeologyBillboardShader();
            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] - distant geology atlas billboard material - owner: ImpostorSystem
            Material material = new Material(shader);
            material.enableInstancing = true;

            Texture2D resolvedAlbedo = albedoTexture;
            if (sourceMaterial != null)
            {
                if (resolvedAlbedo == null)
                    resolvedAlbedo = TryResolveTexture(sourceMaterial, _impostorAlbedoAtlasId, _baseMapId, _legacyBaseMapId, _mainTexId);
            }

            if (resolvedAlbedo != null && material.HasProperty(_baseMapId))
                material.SetTexture(_baseMapId, resolvedAlbedo);

            Color tint = Color.white;
            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty(_baseColorId))
                    tint = sourceMaterial.GetColor(_baseColorId);
                else if (sourceMaterial.HasProperty(_colorId))
                    tint = sourceMaterial.GetColor(_colorId);
            }

            if (material.HasProperty(_baseColorId))
                material.SetColor(_baseColorId, tint);
            else if (material.HasProperty(_colorId))
                material.SetColor(_colorId, tint);

            return material;
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

        private Shader ResolveDistantGeologyBillboardShader()
        {
            if (_distantGeologyBillboardShader != null)
                return _distantGeologyBillboardShader;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Shader shader = Shader.Find("Hecton8/Environment/Hecton_GeologyImpostorBillboard");
            if (shader != null)
                return shader;
#endif

            return ResolveFallbackBillboardShader();
        }

        private static bool TryCalculateBillboardPresentation(
            LODGroup lodGroup,
            Transform originalTransform,
            out Vector3 billboardCenterOffset,
            out Vector3 billboardScale)
        {
            billboardCenterOffset = Vector3.zero;
            billboardScale = Vector3.one;

            if (originalTransform == null)
                return false;

            float size;
            if (lodGroup != null)
            {
                size = Mathf.Max(MinimumBillboardWidth, lodGroup.size);
                Vector3 referencePosition = originalTransform.TransformPoint(lodGroup.localReferencePoint);
                billboardCenterOffset = referencePosition - originalTransform.position;
            }
            else
            {
                Vector3 scale = originalTransform.lossyScale;
                size = Mathf.Max(MinimumBillboardWidth, Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));
            }

            float width = Mathf.Max(MinimumBillboardWidth, size);
            float height = Mathf.Max(MinimumBillboardHeight, size);
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

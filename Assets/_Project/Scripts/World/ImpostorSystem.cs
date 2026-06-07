// ============================================================================
// HECTON-8 — ImpostorSystem.cs
// Manages impostor billboard generation and rendering for very distant objects.
//
// RESPONSIBILITIES:
//   • Register/unregister impostor candidates
//   • Bind authored shared billboard atlas materials from scene-owned source renderers
//   • Spawn/despawn billboard instances via ObjectPoolManager
//   • Activate impostors at distance threshold (150m default)
//   • Stabilize impostor transitions with hysteresis and adaptive threshold scaling
//
// ARCHITECTURE:
//   • Owner-local Instance is the runtime lookup; GlobalRegistry is cold registration.
//   • ITickable — registers with GameTickManager
//   • Zero-GC — pre-allocated collections, struct-based data
//   • Authored shared material atlas binding (no runtime material cloning)
//   • ObjectPoolManager for billboard pooling
//
// PERFORMANCE:
//   • Target: < 0.5ms per frame
//   • Zero GC allocations
//   • Cold-path atlas metadata lookup only
//
// INTEGRATION:
//   • GameTickManager — ITickable registration
//   • Amplify Impostors — offline texture baking (Editor-only)
//   • DynamicResolutionScaler — adaptive weak-device impostor response
//   • ObjectPoolManager — billboard spawning
//
// AMPLIFY IMPOSTORS WORKFLOW:
//   1. Editor: Bake impostor textures via AmplifyImpostor component
//   2. Runtime: Resolve authored shared atlas material and UV rect metadata
//   3. Runtime: Spawn billboard via ObjectPoolManager
//   4. Runtime: Rotate/scale billboard from cached source bounds
//   5. Runtime: Shift thresholds from quality preset + dynamic resolution pressure
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    /// Uses authored shared atlas material records to render far billboards without material clones.
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
    public sealed class ImpostorSystem : MonoBehaviour, ITickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float MinimumBillboardWidth = 0.25f;
        private const float MinimumBillboardHeight = 0.25f;
        private const float CameraResolveRetryInterval = 1f;
        private const float DistantGeologyImpostorDistanceMeters = 5000f;
        private const int MaxHotPathImpostorsPerTick = 64;
        private const int RendererTraversalCapacity = 256;
        private const int MaxAtlasInstanceUploadCapacity = 2048;
        private const int InitialImpostorCapacity = MaxAtlasInstanceUploadCapacity;
        private const int ImpostorAtlasInstanceDataStrideBytes = 32;
        private const int ImpostorAtlasDrawInstanceDataStrideBytes = 64;
        private const float AupDistanceThresholdMeters = 50f;
        private const float AupDistanceThresholdSqr = AupDistanceThresholdMeters * AupDistanceThresholdMeters;
        private const float MinimumQualityThresholdMultiplier = 0.3f;
        private const float MaximumQualityThresholdMultiplier = 1f;
        private static readonly Vector4 DefaultAtlasRect = new Vector4(0f, 0f, 1f, 1f);
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
        private static readonly int _atlasRectId = Shader.PropertyToID("_HectonImpostorAtlasRect");
        private static readonly int _atlasTintFlagsId = Shader.PropertyToID("_HectonImpostorTintFlags");
        private static readonly int _atlasDrawInstanceBufferId = Shader.PropertyToID("_HectonImpostorDrawInstances");
        private static readonly int _atlasDrawInstanceCountId = Shader.PropertyToID("_HectonImpostorDrawInstanceCount");
        private static readonly int _atlasProceduralDrawEnabledId = Shader.PropertyToID("_HectonImpostorProceduralDrawEnabled");
        private static readonly int _h8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");

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

        [SerializeField, Tooltip("Legacy authoring field retained for serialized scene compatibility. Runtime quality scaling is continuous: 0.0 => 0.3x, 1.0 => 1.0x.")]
        private float _lowQualityThresholdMultiplier = MinimumQualityThresholdMultiplier;

        [SerializeField, Tooltip("Legacy authoring field retained for serialized scene compatibility. Runtime quality scaling is continuous: 0.0 => 0.3x, 1.0 => 1.0x.")]
        private float _highQualityThresholdMultiplier = MaximumQualityThresholdMultiplier;

        [SerializeField, Tooltip("Optional explicit main camera reference. Falls back to cold-path camera resolve.")]
        private Camera _cameraReference;

        [SerializeField, Tooltip("Offline-authored shared material for all flora/geology billboard atlas draws. Must point to MAT_Flora_ImpostorAtlas or equivalent.")]
        private Material _authoredImpostorAtlasMaterial;

        [SerializeField, Tooltip("Quad mesh used for single-draw atlas impostor submission. If unset, resolved cold from the billboard prefab MeshFilter.")]
        private Mesh _indirectBillboardMesh;

        [SerializeField, Tooltip("Use one RenderMeshIndirect draw for shared-atlas impostors instead of per-renderer billboard property state.")]
        private bool _enableIndirectAtlasDraw = true;

        [SerializeField, Tooltip("Offline-authored atlas records. Source material or albedo texture maps a candidate to a UV rect in the shared atlas.")]
        private AuthoredImpostorAtlasEntry[] _authoredImpostorAtlasEntries = Array.Empty<AuthoredImpostorAtlasEntry>();

        [Serializable]
        private struct AuthoredImpostorAtlasEntry
        {
            public Material SourceMaterial;
            public Texture2D AlbedoTexture;
            public Vector4 AtlasRect;
            public Color Tint;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ImpostorAtlasInstanceData
        {
            [FieldOffset(0)]
            public Vector4 AtlasRect;

            [FieldOffset(16)]
            public Vector4 TintFlags;

            public static ImpostorAtlasInstanceData Create(Vector4 atlasRect, Color tint, bool distantGeology)
            {
                Vector4 safeRect = SanitizeAtlasRect(atlasRect);
                Color safeTint = SanitizeColor(tint);
                return new ImpostorAtlasInstanceData
                {
                    AtlasRect = safeRect,
                    TintFlags = new Vector4(safeTint.r, safeTint.g, safeTint.b, distantGeology ? 1f : 0f)
                };
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = ImpostorAtlasDrawInstanceDataStrideBytes)]
        private struct ImpostorAtlasDrawInstanceData
        {
            [FieldOffset(0)]
            public Vector4 CenterWidth;

            [FieldOffset(16)]
            public Vector4 HeightFlags;

            [FieldOffset(32)]
            public Vector4 AtlasRect;

            [FieldOffset(48)]
            public Vector4 TintFlags;

            public static ImpostorAtlasDrawInstanceData Create(
                Vector3 center,
                Vector3 scale,
                in ImpostorAtlasInstanceData atlasData)
            {
                Vector3 safeCenter = SanitizeVector3(center);
                Vector3 safeScale = SanitizeVector3(scale);
                float width = math.max(MinimumBillboardWidth, math.abs(safeScale.x));
                float height = math.max(MinimumBillboardHeight, math.abs(safeScale.y));
                return new ImpostorAtlasDrawInstanceData
                {
                    CenterWidth = new Vector4(safeCenter.x, safeCenter.y, safeCenter.z, width),
                    HeightFlags = new Vector4(height, 0f, 0f, 0f),
                    AtlasRect = atlasData.AtlasRect,
                    TintFlags = atlasData.TintFlags
                };
            }
        }

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
            public ImpostorAtlasInstanceData AtlasInstanceData;
            public int AtlasInstanceIndex;
            public bool OriginalActiveSelf;
            public bool IsActive;
            public bool UsesIndirectAtlasDraw;
        }

        private struct ImpostorTextureData
        {
            public Texture2D AlbedoTexture;
            public Texture2D NormalTexture;
            public Material ImpostorMaterial;
            public ImpostorAtlasInstanceData AtlasInstanceData;
            public bool UsesSharedAtlasMaterial;
            public bool IsLoaded;
        }

        // COLD ALLOC: Dictionary<EntityId, GameObject>[2048] - impostor billboard lookup - owner: ImpostorSystem
        private readonly Dictionary<EntityId, GameObject> _impostorBillboards = new Dictionary<EntityId, GameObject>(InitialImpostorCapacity);
        // COLD ALLOC: List<ImpostorInstance>[2048] - active impostor instances - owner: ImpostorSystem
        private readonly List<ImpostorInstance> _activeImpostors = new List<ImpostorInstance>(InitialImpostorCapacity);
        // COLD ALLOC: HashSet<GameObject>[2048] - registered candidate lookup - owner: ImpostorSystem
        private readonly HashSet<GameObject> _registeredCandidates = new HashSet<GameObject>(InitialImpostorCapacity);
        // COLD ALLOC: Dictionary<EntityId, ImpostorTextureData>[2048] - impostor texture cache - owner: ImpostorSystem
        private readonly Dictionary<EntityId, ImpostorTextureData> _textureCache = new Dictionary<EntityId, ImpostorTextureData>(InitialImpostorCapacity);
        // COLD ALLOC: Dictionary<EntityId, Renderer>[2048] - source renderer cache resolved during candidate registration - owner: ImpostorSystem
        private readonly Dictionary<EntityId, Renderer> _sourceRendererCache = new Dictionary<EntityId, Renderer>(InitialImpostorCapacity);
        // COLD ALLOC: Dictionary<EntityId, Renderer>[2048] - pooled billboard renderer cache - owner: ImpostorSystem
        private readonly Dictionary<EntityId, Renderer> _billboardRendererCache = new Dictionary<EntityId, Renderer>(InitialImpostorCapacity);
        // COLD ALLOC: Transform[256] - bounded renderer registration traversal stack - owner: ImpostorSystem
        private readonly Transform[] _rendererTraversalScratch = new Transform[RendererTraversalCapacity];
        // COLD ALLOC: ImpostorAtlasDrawInstanceData[2048] - prepared atlas draw DTOs copied into mapped GPU buffers - owner: ImpostorSystem
        private readonly ImpostorAtlasDrawInstanceData[] _atlasDrawInstanceScratch = new ImpostorAtlasDrawInstanceData[MaxAtlasInstanceUploadCapacity];
        private Camera _mainCamera;
        private Transform _cameraTransform;
        private int _playerRuntimeContextCacheFrame = -1;
        private bool _playerRuntimeContextCacheValid;
        private PlayerRuntimeContext _playerRuntimeContextCache;
        private int _viewerAupCacheFrame = -1;
        private AbsoluteUniversePosition _viewerAupCache;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ITickDispatcher _dispatcher;
        private IObjectPoolService _objectPool;
        private LODSystemManager _lodSystemManager;
        private DynamicResolutionScaler _dynamicResolutionScaler;
        private float _cameraResolveRetryTimer;
        private int _impostorTickCursor;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private bool _hasPendingImpostorTick;
        private float _pendingImpostorDeltaTime;
        private GraphicsBuffer _atlasDrawInstanceBufferA;
        private GraphicsBuffer _atlasDrawInstanceBufferB;
        private GraphicsBuffer _activeAtlasDrawInstanceBuffer;
        private GraphicsBuffer _atlasDrawArgsBuffer;
        private Mesh _resolvedIndirectBillboardMesh;
        private Mesh _atlasDrawArgsMesh;
        private MaterialPropertyBlock _billboardAtlasProperties;
        private MaterialPropertyBlock _atlasDrawProperties;
        private Bounds _atlasDrawBounds;
        private int _atlasDrawUploadBufferIndex;
        private int _activeAtlasDrawInstanceCount;
        private int _activeIndirectAtlasImpostorCount;
        private int _lastAtlasDrawArgsInstanceCount = -1;
        private bool _supportsIndirectAtlasDrawCold;
        private bool _indirectBillboardMeshResolved;
        private bool _hasAtlasDrawBounds;

        /// <summary>
        /// Registry-backed runtime instance. Null when the system is absent.
        /// </summary>
        private static ImpostorSystem s_activeRuntimeInstance;
        public static ImpostorSystem Instance => s_activeRuntimeInstance;

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
            s_activeRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!ValidateRuntimeDtoLayoutsCold())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ImpostorSystem] DTO layout validation failed. Disabling impostor runtime.");
#endif
                enabled = false;
                return;
            }

            ImpostorSystem registered = GlobalRegistry.Impostors;
            if (registered != null && registered != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Duplicate registry owner detected. Destroying duplicate.");
#endif
                Destroy(gameObject);
                return;
            }

            CacheRegistryServicesCold();
        }

        private static bool ValidateRuntimeDtoLayoutsCold()
        {
            int atlasInstanceStride = UnsafeUtility.SizeOf<ImpostorAtlasInstanceData>();
            int atlasDrawInstanceStride = UnsafeUtility.SizeOf<ImpostorAtlasDrawInstanceData>();
            return atlasInstanceStride == ImpostorAtlasInstanceDataStrideBytes &&
                   (atlasInstanceStride & 7) == 0 &&
                   atlasDrawInstanceStride == ImpostorAtlasDrawInstanceDataStrideBytes &&
                   (atlasDrawInstanceStride & 7) == 0;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            ValidateAuthoredAtlasMaterialCold();
            CacheAtlasDrawSupportCold();
            CacheIndirectBillboardMeshCold();
            PrewarmBillboardAtlasPropertiesCold();
            PrewarmAtlasDrawResourcesCold();
            TryRegisterHotSwapListener();
            InvalidatePlayerRuntimeCache();
            TryRegisterService();
            TryRegister();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            RestoreAllOriginalVisibility();
            ReleaseCachedMaterials();
            InvalidatePlayerRuntimeCache();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            RestoreAllOriginalVisibility();
            InvalidatePlayerRuntimeCache();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearCachedRegistryServices();

            ReleaseCachedMaterials();
            ReleaseAtlasDrawResources();
            _impostorBillboards.Clear();
            _activeImpostors.Clear();
            _registeredCandidates.Clear();
            _textureCache.Clear();
            _sourceRendererCache.Clear();
            _billboardRendererCache.Clear();

        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureAuthoredAtlasMaterialEditorOnly();
            _indirectBillboardMeshResolved = false;
        }
#endif

        private void ValidateAuthoredAtlasMaterialCold()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EnsureAuthoredAtlasMaterialEditorOnly();
#endif
        }

#if UNITY_EDITOR
        private void EnsureAuthoredAtlasMaterialEditorOnly()
        {
            if (_authoredImpostorAtlasMaterial != null &&
                !_authoredImpostorAtlasMaterial.enableInstancing)
            {
                _authoredImpostorAtlasMaterial.enableInstancing = true;
                EditorUtility.SetDirty(_authoredImpostorAtlasMaterial);
            }
        }
#endif

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || _dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || _dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterImpostorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Impostors, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.Impostors, this))
                GlobalRegistry.UnregisterImpostorRuntime(this);

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_objectPool == null)
                _objectPool = GlobalRegistry.ObjectPoolService;

            if (_lodSystemManager == null)
                _lodSystemManager = GlobalRegistry.LODSystem;

            if (_dynamicResolutionScaler == null)
                _dynamicResolutionScaler = GlobalRegistry.DynamicResolution;
        }

        private void ClearCachedRegistryServices()
        {
            _playerRuntimeContext = null;
            _dispatcher = null;
            _objectPool = null;
            _lodSystemManager = null;
            _dynamicResolutionScaler = null;
            _mainCamera = null;
            _cameraTransform = null;
        }

        private void CacheIndirectBillboardMeshCold()
        {
            _resolvedIndirectBillboardMesh = _indirectBillboardMesh;
            if (_resolvedIndirectBillboardMesh == null && _billboardPrefab != null)
            {
                MeshFilter meshFilter = _billboardPrefab.GetComponentInChildren<MeshFilter>(true);
                _resolvedIndirectBillboardMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            }

            _indirectBillboardMeshResolved = true;
        }

        private void CacheAtlasDrawSupportCold()
        {
            _supportsIndirectAtlasDrawCold = SystemInfo.supportsInstancing &&
                                             SystemInfo.supportsComputeShaders &&
                                             SystemInfo.graphicsShaderLevel >= 45;
        }

        private Mesh ResolveIndirectBillboardMeshCold()
        {
            if (!_indirectBillboardMeshResolved)
                CacheIndirectBillboardMeshCold();

            return _resolvedIndirectBillboardMesh;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _mainCamera = _cameraReference;
                    _cameraTransform = _mainCamera != null ? _mainCamera.transform : null;
                    _cameraResolveRetryTimer = 0f;
                    InvalidatePlayerRuntimeCache();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrame();
                    TryUnregister();
                    _dispatcher = currentService as ITickDispatcher;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.LODSystemRuntime:
                    _lodSystemManager = currentService as LODSystemManager;
                    break;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionScaler = currentService as DynamicResolutionScaler;
                    break;
            }
        }

        /// <summary>
        /// Updates impostor activation against the cached camera position.
        /// </summary>
        public void Tick(float dt)
        {
            _pendingImpostorDeltaTime = math.min(0.25f, _pendingImpostorDeltaTime + math.max(0f, dt));
            _hasPendingImpostorTick = true;
        }

        private void ProcessImpostorTick(float dt)
        {
            if (!TryResolveCamera(dt))
                return;

            float thresholdScale = ResolveThresholdScale();
            float thresholdScaleSqr = thresholdScale * thresholdScale;
            AbsoluteUniversePosition cameraAup = ResolveViewerAup();
            int batchCount = math.min(_activeImpostors.Count, MaxHotPathImpostorsPerTick);
            for (int processed = 0; processed < batchCount && _activeImpostors.Count > 0; processed++)
            {
                if (_impostorTickCursor >= _activeImpostors.Count)
                    _impostorTickCursor = 0;

                int i = _impostorTickCursor;
                ImpostorInstance instance = _activeImpostors[i];
                if (instance.OriginalObject == null)
                {
                    if (instance.IsActive && instance.UsesIndirectAtlasDraw)
                        DecrementActiveIndirectAtlasCount();

                    DespawnBillboard(ref instance);
                    if (!ReferenceEquals(instance.OriginalObject, null))
                        _registeredCandidates.Remove(instance.OriginalObject);
                    _textureCache.Remove(instance.ImpostorID);
                    _sourceRendererCache.Remove(instance.ImpostorID);
                    RemoveImpostorAtSwap(i);

                    continue;
                }

                Transform originalTransform = instance.OriginalTransform != null
                    ? instance.OriginalTransform
                    : instance.OriginalObject.transform;
                Vector3 originalPosition = ResolveOriginalRuntimePosition(originalTransform);
                float sqrDistance = ResolveCameraDistanceSqr(in cameraAup, originalPosition);
                float activationDistanceSqr = instance.ActivationDistanceSqr * thresholdScaleSqr;
                float deactivationDistanceSqr = instance.DeactivationDistanceSqr * thresholdScaleSqr;

                if (instance.IsActive &&
                    instance.BillboardObject == null &&
                    !instance.UsesIndirectAtlasDraw)
                {
                    ApplyOriginalObjectVisibility(ref instance, true);
                    instance.IsActive = false;
                    _activeImpostors[i] = instance;
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

        public void LateFrameTick()
        {
            if (_hasPendingImpostorTick)
            {
                float dt = _pendingImpostorDeltaTime;
                _pendingImpostorDeltaTime = 0f;
                _hasPendingImpostorTick = false;
                ProcessImpostorTick(dt);
            }

            UploadAndSubmitActiveAtlasDraws();
        }

        private static Vector3 ResolveOriginalRuntimePosition(Transform originalTransform)
        {
            return originalTransform != null ? originalTransform.position : Vector3.zero;
        }

        private static float ResolveCameraDistanceSqr(
            in AbsoluteUniversePosition cameraAup,
            Vector3 objectPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(objectPosition, out AbsoluteUniversePosition objectAup))
                return float.MaxValue;

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
        /// Registers a geology HLOD candidate. Geometry switches to the shared atlas indirect draw path past 5km.
        /// Missing atlas entries fail closed; runtime material fallback is forbidden.
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

            EntityId impostorID = obj.GetEntityId();
            bool hasTextureCache = _textureCache.ContainsKey(impostorID);
            if (!_registeredCandidates.Contains(obj) &&
                (_registeredCandidates.Count >= InitialImpostorCapacity ||
                 _activeImpostors.Count >= InitialImpostorCapacity ||
                 (!hasTextureCache && _textureCache.Count >= InitialImpostorCapacity)))
            {
                return;
            }

            if (!_registeredCandidates.Add(obj))
                return;

            if (hasTextureCache)
            {
                if (!TryAddImpostorInstance(obj, impostorID, lodGroup, activationDistanceMeters))
                    _registeredCandidates.Remove(obj);
                return;
            }

            if (!TryBuildImpostorData(obj, impostorID, useDistantGeologyMaterial))
            {
                _registeredCandidates.Remove(obj);
                _sourceRendererCache.Remove(impostorID);
                return;
            }

            if (!TryAddImpostorInstance(obj, impostorID, lodGroup, activationDistanceMeters))
            {
                _registeredCandidates.Remove(obj);
                if (_textureCache.ContainsKey(impostorID))
                    _textureCache.Remove(impostorID);
                _sourceRendererCache.Remove(impostorID);
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

            if (_textureCache.TryGetValue(impostorID, out _))
                _textureCache.Remove(impostorID);
            _sourceRendererCache.Remove(impostorID);
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
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                _mainCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            if (_mainCamera == null &&
                TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                _mainCamera = runtimeContext.PlayerCamera;
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

            if (instance.UsesIndirectAtlasDraw && data.UsesSharedAtlasMaterial)
            {
                GameObject original = instance.OriginalObject;
                if (original == null)
                    return;

                instance.BillboardObject = null;
                instance.BillboardRenderer = null;
                instance.IsActive = true;
                IncrementActiveIndirectAtlasCount();
                _impostorBillboards.Remove(instance.ImpostorID);
                ApplyOriginalObjectVisibility(ref instance, false);
                return;
            }

            IObjectPoolService pool = _objectPool;
            if (_billboardPrefab == null || pool == null)
                return;

            GameObject originalObject = instance.OriginalObject;
            if (originalObject == null)
                return;

            Transform originalTransform = instance.OriginalTransform != null
                ? instance.OriginalTransform
                : originalObject.transform;
            Vector3 originalPosition = ResolveOriginalRuntimePosition(originalTransform);
            GameObject billboard = pool.Spawn(
                _billboardPrefab,
                originalPosition,
                Quaternion.identity);
            if (billboard == null)
                return;

            TryResolveBillboardRenderer(billboard, pool, out Renderer renderer);
            if (renderer == null)
            {
                pool.Despawn(billboard);
                return;
            }

            renderer.sharedMaterial = data.ImpostorMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.forceRenderingOff = false;
            if (!ApplyBillboardAtlasProperties(renderer, in data))
            {
                pool.Despawn(billboard);
                return;
            }

            instance.BillboardObject = billboard;
            instance.BillboardRenderer = renderer;
            instance.IsActive = true;
            if (!TryTrackBillboardNoGrowth(instance.ImpostorID, billboard))
            {
                ClearBillboardAtlasProperties(renderer);
                instance.BillboardObject = null;
                instance.BillboardRenderer = null;
                instance.IsActive = false;
                renderer = null;
                pool.Despawn(billboard);
                return;
            }

            AbsoluteUniversePosition cameraAup = ResolveViewerAup();
            UpdateBillboardTransform(ref instance, originalPosition, in cameraAup);
            ApplyOriginalObjectVisibility(ref instance, false);
        }

        private bool TryTrackBillboardNoGrowth(EntityId impostorID, GameObject billboard)
        {
            if (_impostorBillboards.ContainsKey(impostorID) ||
                _impostorBillboards.Count < InitialImpostorCapacity)
            {
                _impostorBillboards[impostorID] = billboard;
                return true;
            }

            return false;
        }

        private void DeactivateImpostor(ref ImpostorInstance instance)
        {
            if (instance.IsActive && instance.UsesIndirectAtlasDraw)
                DecrementActiveIndirectAtlasCount();

            ApplyOriginalObjectVisibility(ref instance, true);
            DespawnBillboard(ref instance);
            instance.IsActive = false;
            instance.AtlasInstanceIndex = -1;
        }

        private bool TryResolveBillboardRenderer(GameObject billboard, IObjectPoolService pool, out Renderer renderer)
        {
            renderer = null;
            if (billboard == null || pool == null)
                return false;

            EntityId key = billboard.GetEntityId();
            if (_billboardRendererCache.TryGetValue(key, out renderer) && renderer != null)
                return true;

            if (!pool.TryGetPooledRootRenderer(billboard, out renderer))
                renderer = null;

            if (renderer != null &&
                (_billboardRendererCache.ContainsKey(key) ||
                 _billboardRendererCache.Count < InitialImpostorCapacity))
            {
                _billboardRendererCache[key] = renderer;
            }

            return renderer != null;
        }

        private void PrewarmBillboardAtlasPropertiesCold()
        {
            if (_authoredImpostorAtlasMaterial != null && _billboardAtlasProperties == null)
                _billboardAtlasProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[pooled billboard atlas] - fallback atlas rect/tint payload.
        }

        private bool ApplyBillboardAtlasProperties(Renderer renderer, in ImpostorTextureData data)
        {
            if (renderer == null || _billboardAtlasProperties == null)
                return false;

            _billboardAtlasProperties.Clear();
            _billboardAtlasProperties.SetVector(_atlasRectId, data.AtlasInstanceData.AtlasRect);
            _billboardAtlasProperties.SetVector(_atlasTintFlagsId, data.AtlasInstanceData.TintFlags);
            _billboardAtlasProperties.SetInt(_atlasDrawInstanceCountId, 0);
            _billboardAtlasProperties.SetInt(_atlasProceduralDrawEnabledId, 0);
            _billboardAtlasProperties.SetFloat(_h8GlobalQualityWeightId, ResolveImpostorQualityWeight01());
            renderer.SetPropertyBlock(_billboardAtlasProperties);
            return true;
        }

        private void ClearBillboardAtlasProperties(Renderer renderer)
        {
            if (renderer == null || _billboardAtlasProperties == null)
                return;

            _billboardAtlasProperties.Clear();
            renderer.SetPropertyBlock(_billboardAtlasProperties);
        }

        private bool TryBuildImpostorData(GameObject obj, EntityId impostorID, bool useDistantGeologyMaterial)
        {
            if (obj == null)
                return false;

            Material sourceMaterial = null;
            Texture2D albedoTexture = null;
            Texture2D normalTexture = null;
            TryResolvePrimaryMaterial(
                obj,
                impostorID,
                out sourceMaterial,
                out albedoTexture,
                out normalTexture,
                resolveNormalTexture: !useDistantGeologyMaterial);

            Material sharedMaterial = ResolveSharedImpostorMaterial();
            if (sharedMaterial == null)
                return false;

            Vector4 atlasRect = DefaultAtlasRect;
            Color tint = ResolveSourceTint(sourceMaterial);
            bool hasAuthoredAtlasEntry = TryResolveAuthoredAtlasEntry(sourceMaterial, albedoTexture, out atlasRect, out tint);
            if (!hasAuthoredAtlasEntry)
                return false;

            _textureCache[impostorID] = new ImpostorTextureData
            {
                AlbedoTexture = albedoTexture,
                NormalTexture = useDistantGeologyMaterial ? null : normalTexture,
                ImpostorMaterial = sharedMaterial,
                AtlasInstanceData = ImpostorAtlasInstanceData.Create(atlasRect, tint, useDistantGeologyMaterial),
                UsesSharedAtlasMaterial = true,
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

            if (_activeImpostors.Count >= InitialImpostorCapacity ||
                !_textureCache.TryGetValue(impostorID, out ImpostorTextureData data))
            {
                return false;
            }

            if (!data.IsLoaded)
                return false;

            if (!TryCalculateBillboardPresentation(lodGroup, obj.transform, out Vector3 billboardCenterOffset, out Vector3 billboardScale))
                return false;

            bool usesIndirectAtlasDraw = CanUseIndirectAtlasDraw(in data);
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
                AtlasInstanceData = data.AtlasInstanceData,
                AtlasInstanceIndex = -1,
                OriginalActiveSelf = obj.activeSelf,
                IsActive = false,
                UsesIndirectAtlasDraw = usesIndirectAtlasDraw
            };
            _activeImpostors.Add(instance);
            return true;
        }

        private bool CanUseIndirectAtlasDraw(in ImpostorTextureData data)
        {
            return _enableIndirectAtlasDraw &&
                   _supportsIndirectAtlasDrawCold &&
                   data.UsesSharedAtlasMaterial &&
                   _authoredImpostorAtlasMaterial != null &&
                   ResolveIndirectBillboardMeshCold() != null;
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

                RemoveImpostorAtSwap(i);
                return;
            }
        }

        private void RemoveImpostorAtSwap(int index)
        {
            int count = _activeImpostors.Count;
            if ((uint)index >= (uint)count)
                return;

            int lastIndex = count - 1;
            if (index != lastIndex)
                _activeImpostors[index] = _activeImpostors[lastIndex];

            _activeImpostors.RemoveAt(lastIndex);
            if (_impostorTickCursor >= _activeImpostors.Count)
                _impostorTickCursor = 0;
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
                ClearBillboardAtlasProperties(instance.BillboardRenderer);
                IObjectPoolService pool = _objectPool;
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
                {
                    if (instance.UsesIndirectAtlasDraw)
                        DecrementActiveIndirectAtlasCount();

                    DespawnBillboard(ref instance);
                    instance.IsActive = false;
                    instance.AtlasInstanceIndex = -1;
                }

                ApplyOriginalObjectVisibility(ref instance, true);
                _activeImpostors[i] = instance;
            }

            _activeIndirectAtlasImpostorCount = 0;
        }

        private void IncrementActiveIndirectAtlasCount()
        {
            if (_activeIndirectAtlasImpostorCount < int.MaxValue)
                _activeIndirectAtlasImpostorCount++;
        }

        private void DecrementActiveIndirectAtlasCount()
        {
            if (_activeIndirectAtlasImpostorCount > 0)
                _activeIndirectAtlasImpostorCount--;
        }

        private void ReleaseCachedMaterials()
        {
            ClearAtlasDrawRenderState();
        }

        private void UploadAndSubmitActiveAtlasDraws()
        {
            Material atlasMaterial = _authoredImpostorAtlasMaterial;
            Mesh drawMesh = _resolvedIndirectBillboardMesh;
            if (atlasMaterial == null ||
                drawMesh == null ||
                !_supportsIndirectAtlasDrawCold ||
                _activeIndirectAtlasImpostorCount <= 0)
            {
                ClearAtlasDrawRenderState();
                return;
            }

            int sourceCount = math.min(_activeIndirectAtlasImpostorCount, MaxAtlasInstanceUploadCapacity);
            if (sourceCount <= 0)
            {
                ClearAtlasDrawRenderState();
                return;
            }

            if (!HasAtlasDrawInstanceBufferCapacity(sourceCount))
            {
                ClearAtlasDrawRenderState();
                return;
            }

            GraphicsBuffer writeBuffer = ResolveAtlasDrawInstanceWriteBuffer();
            if (writeBuffer == null || !writeBuffer.IsValid())
            {
                ClearAtlasDrawRenderState();
                return;
            }

            int lockedCount = math.min(sourceCount, math.min(writeBuffer.count, MaxAtlasInstanceUploadCapacity));
            if (lockedCount <= 0)
            {
                ClearAtlasDrawRenderState();
                return;
            }

            int writeCount = BuildAtlasDrawInstanceScratch(lockedCount, out Bounds combinedBounds, out bool hasBounds);
            if (writeCount <= 0 || !hasBounds)
            {
                _activeAtlasDrawInstanceCount = 0;
                _hasAtlasDrawBounds = false;
                ClearAtlasDrawRenderState();
                return;
            }

            long uploadBytes = GraphicsBufferUploadUtility.EstimateUploadBytes<ImpostorAtlasDrawInstanceData>(writeCount);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
            {
                ClearAtlasDrawRenderState();
                return;
            }

            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<ImpostorAtlasDrawInstanceData> mapped = default;
            try
            {
                mapped = writeBuffer.LockBufferForWrite<ImpostorAtlasDrawInstanceData>(0, writeCount);
                bufferLocked = true;
                for (int i = 0; i < writeCount; i++)
                    mapped[i] = _atlasDrawInstanceScratch[i];
                uploadAccepted = true;
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        writeBuffer.UnlockBufferAfterWrite<ImpostorAtlasDrawInstanceData>(writeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                    else
                        GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
                }
            }

            _activeAtlasDrawInstanceBuffer = writeBuffer;
            _atlasDrawUploadBufferIndex ^= 1;
            _activeAtlasDrawInstanceCount = writeCount;
            _atlasDrawBounds = combinedBounds;
            _hasAtlasDrawBounds = true;
            SubmitAtlasIndirectDraw(drawMesh, atlasMaterial);
        }

        private int BuildAtlasDrawInstanceScratch(int maxCount, out Bounds combinedBounds, out bool hasBounds)
        {
            combinedBounds = default;
            hasBounds = false;
            int mappedCount = 0;
            int activeCount = _activeImpostors.Count;
            for (int i = 0; i < activeCount && mappedCount < maxCount; i++)
            {
                ImpostorInstance instance = _activeImpostors[i];
                if (!instance.IsActive ||
                    !instance.UsesIndirectAtlasDraw)
                {
                    continue;
                }

                Transform originalTransform = instance.OriginalTransform != null
                    ? instance.OriginalTransform
                    : (instance.OriginalObject != null ? instance.OriginalObject.transform : null);
                if (originalTransform == null)
                    continue;

                Vector3 center = ResolveOriginalRuntimePosition(originalTransform) + instance.BillboardCenterOffset;
                if (!IsFiniteVector3(center))
                    continue;

                instance.AtlasInstanceIndex = mappedCount;
                _activeImpostors[i] = instance;
                _atlasDrawInstanceScratch[mappedCount] = ImpostorAtlasDrawInstanceData.Create(
                    center,
                    instance.BillboardScale,
                    in instance.AtlasInstanceData);

                Bounds instanceBounds = new Bounds(
                    center,
                    new Vector3(
                        math.max(MinimumBillboardWidth, math.abs(instance.BillboardScale.x)),
                        math.max(MinimumBillboardHeight, math.abs(instance.BillboardScale.y)),
                        math.max(MinimumBillboardWidth, math.abs(instance.BillboardScale.x))));
                if (hasBounds)
                    combinedBounds.Encapsulate(instanceBounds);
                else
                {
                    combinedBounds = instanceBounds;
                    hasBounds = true;
                }

                mappedCount++;
            }

            return mappedCount;
        }

        private void PrewarmAtlasDrawResourcesCold()
        {
            if (!_supportsIndirectAtlasDrawCold ||
                _authoredImpostorAtlasMaterial == null ||
                _resolvedIndirectBillboardMesh == null)
            {
                return;
            }

            EnsureAtlasDrawInstanceBufferCapacity(MaxAtlasInstanceUploadCapacity);
            EnsureAtlasIndirectArgsBufferAllocatedCold();
            EnsureAtlasDrawPropertyBlockCold();
        }

        private void EnsureAtlasDrawPropertyBlockCold()
        {
            if (_atlasDrawProperties == null)
                _atlasDrawProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[atlas impostor draw] - per-frame render payload without mutating shared material.
        }

        private void SubmitAtlasIndirectDraw(Mesh drawMesh, Material atlasMaterial)
        {
            if (drawMesh == null ||
                atlasMaterial == null ||
                _activeAtlasDrawInstanceBuffer == null ||
                !_activeAtlasDrawInstanceBuffer.IsValid() ||
                _activeAtlasDrawInstanceCount <= 0 ||
                !_hasAtlasDrawBounds)
            {
                ClearAtlasDrawRenderState();
                return;
            }

            if (!EnsureAtlasIndirectArgsBuffer(drawMesh))
            {
                ClearAtlasDrawRenderState();
                return;
            }

            EnsureAtlasDrawPropertyBlockCold();
            if (_atlasDrawProperties == null)
            {
                ClearAtlasDrawRenderState();
                return;
            }

            _atlasDrawProperties.Clear();
            _atlasDrawProperties.SetBuffer(_atlasDrawInstanceBufferId, (GraphicsBuffer)_activeAtlasDrawInstanceBuffer);
            _atlasDrawProperties.SetInt(_atlasDrawInstanceCountId, _activeAtlasDrawInstanceCount);
            _atlasDrawProperties.SetInt(_atlasProceduralDrawEnabledId, 1);
            _atlasDrawProperties.SetFloat(_h8GlobalQualityWeightId, ResolveImpostorQualityWeight01());

            RenderParams renderParams = new RenderParams(atlasMaterial)
            {
                matProps = _atlasDrawProperties,
                worldBounds = _atlasDrawBounds,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                camera = _mainCamera
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, drawMesh, _atlasDrawArgsBuffer, 1, 0);
        }

        private bool EnsureAtlasIndirectArgsBuffer(Mesh drawMesh)
        {
            if (drawMesh == null || !_supportsIndirectAtlasDrawCold || _activeAtlasDrawInstanceCount <= 0)
                return false;

            uint indexCount = drawMesh.GetIndexCount(0);
            if (indexCount == 0u)
                return false;

            if (!HasAtlasIndirectArgsBufferReady())
                return false;

            if (ReferenceEquals(_atlasDrawArgsMesh, drawMesh) &&
                _lastAtlasDrawArgsInstanceCount == _activeAtlasDrawInstanceCount)
            {
                return _atlasDrawArgsBuffer != null && _atlasDrawArgsBuffer.IsValid();
            }

            GraphicsBuffer.IndirectDrawIndexedArgs args = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = indexCount,
                instanceCount = unchecked((uint)math.max(0, _activeAtlasDrawInstanceCount)),
                startIndex = drawMesh.GetIndexStart(0),
                baseVertexIndex = unchecked((uint)Mathf.Max(0, drawMesh.GetBaseVertex(0))),
                startInstance = 0u
            };

            if (!GraphicsBufferUploadUtility.TryUploadSingle(_atlasDrawArgsBuffer, args))
                return false;

            _atlasDrawArgsMesh = drawMesh;
            _lastAtlasDrawArgsInstanceCount = _activeAtlasDrawInstanceCount;
            return true;
        }

        private void EnsureAtlasIndirectArgsBufferAllocatedCold()
        {
            if (_atlasDrawArgsBuffer != null && _atlasDrawArgsBuffer.IsValid())
                return;

            ReleaseGraphicsBuffer(ref _atlasDrawArgsBuffer);
            _atlasDrawArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - geology atlas indirect draw args - owner: ImpostorSystem
            _atlasDrawArgsMesh = null;
            _lastAtlasDrawArgsInstanceCount = -1;
        }

        private bool HasAtlasIndirectArgsBufferReady()
        {
            return _atlasDrawArgsBuffer != null && _atlasDrawArgsBuffer.IsValid();
        }

        private void ClearAtlasDrawRenderState()
        {
            _activeAtlasDrawInstanceCount = 0;
            _hasAtlasDrawBounds = false;
            _lastAtlasDrawArgsInstanceCount = -1;
            if (_atlasDrawProperties != null)
                _atlasDrawProperties.Clear();
        }

        private void EnsureAtlasDrawInstanceBufferCapacity(int requiredCount)
        {
            int safeCount = Mathf.NextPowerOfTwo(Mathf.Clamp(requiredCount, 1, MaxAtlasInstanceUploadCapacity));
            if (_atlasDrawInstanceBufferA != null &&
                _atlasDrawInstanceBufferA.IsValid() &&
                _atlasDrawInstanceBufferA.count >= safeCount &&
                _atlasDrawInstanceBufferB != null &&
                _atlasDrawInstanceBufferB.IsValid() &&
                _atlasDrawInstanceBufferB.count >= safeCount)
            {
                if (_activeAtlasDrawInstanceBuffer == null)
                    _activeAtlasDrawInstanceBuffer = _atlasDrawInstanceBufferA;
                return;
            }

            ReleaseGraphicsBuffer(ref _atlasDrawInstanceBufferA);
            ReleaseGraphicsBuffer(ref _atlasDrawInstanceBufferB);
            _atlasDrawInstanceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ImpostorAtlasDrawInstanceData>(safeCount); // COLD ALLOC: GraphicsBuffer[atlas impostor draw DTO A] - shared atlas indirect draw payload - owner: ImpostorSystem
            _atlasDrawInstanceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ImpostorAtlasDrawInstanceData>(safeCount); // COLD ALLOC: GraphicsBuffer[atlas impostor draw DTO B] - shared atlas indirect draw payload - owner: ImpostorSystem
            _activeAtlasDrawInstanceBuffer = _atlasDrawInstanceBufferA;
            _atlasDrawUploadBufferIndex = 0;
        }

        private bool HasAtlasDrawInstanceBufferCapacity(int requiredCount)
        {
            int safeCount = Mathf.NextPowerOfTwo(Mathf.Clamp(requiredCount, 1, MaxAtlasInstanceUploadCapacity));
            return _atlasDrawInstanceBufferA != null &&
                   _atlasDrawInstanceBufferB != null &&
                   _atlasDrawInstanceBufferA.IsValid() &&
                   _atlasDrawInstanceBufferB.IsValid() &&
                   _atlasDrawInstanceBufferA.count >= safeCount &&
                   _atlasDrawInstanceBufferB.count >= safeCount;
        }

        private GraphicsBuffer ResolveAtlasDrawInstanceWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _atlasDrawUploadBufferIndex == 0 ? _atlasDrawInstanceBufferA : _atlasDrawInstanceBufferB;
            if (writeBuffer != null && writeBuffer.IsValid())
                return writeBuffer;

            GraphicsBuffer fallback = ReferenceEquals(_activeAtlasDrawInstanceBuffer, _atlasDrawInstanceBufferA)
                ? _atlasDrawInstanceBufferB
                : _atlasDrawInstanceBufferA;
            return fallback != null && fallback.IsValid() ? fallback : null;
        }

        private void ReleaseAtlasDrawResources()
        {
            ReleaseGraphicsBuffer(ref _atlasDrawInstanceBufferA);
            ReleaseGraphicsBuffer(ref _atlasDrawInstanceBufferB);
            ReleaseGraphicsBuffer(ref _atlasDrawArgsBuffer);
            _activeAtlasDrawInstanceBuffer = null;
            _atlasDrawArgsMesh = null;
            _billboardAtlasProperties = null;
            _atlasDrawProperties = null;
            _atlasDrawUploadBufferIndex = 0;
            _activeAtlasDrawInstanceCount = 0;
            _activeIndirectAtlasImpostorCount = 0;
            _lastAtlasDrawArgsInstanceCount = -1;
            _hasAtlasDrawBounds = false;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
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
            Vector3 cameraDelta;
            if (TryResolveAupFromRuntimeOrigin(billboardPosition, out AbsoluteUniversePosition billboardAup))
            {
                float3 cameraDeltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in cameraAup, in billboardAup);
                cameraDelta = new Vector3(cameraDeltaAup.x, 0f, cameraDeltaAup.z);
            }
            else
            {
                Vector3 localDelta = billboardPosition - _cameraTransform.position;
                cameraDelta = new Vector3(localDelta.x, 0f, localDelta.z);
            }

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
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_viewerAupCacheFrame == frame)
                return _viewerAupCache;

            _viewerAupCacheFrame = frame;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
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

            _viewerAupCache = default;
            return _viewerAupCache;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private bool TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            float qualityScale = math.lerp(
                MinimumQualityThresholdMultiplier,
                MaximumQualityThresholdMultiplier,
                Smooth01(ResolveImpostorQualityWeight01()));

            if (!_enableAdaptiveThresholdScaling)
                return qualityScale;

            DynamicResolutionScaler scaler = _dynamicResolutionScaler;
            if (scaler == null)
                return qualityScale;

            float renderScale = math.saturate(scaler.CurrentRenderScale);
            float adaptiveScale = math.lerp(
                math.clamp(_minAdaptiveThresholdMultiplier, 0.1f, 1f),
                1f,
                renderScale);

            return qualityScale * adaptiveScale;
        }

        private float ResolveImpostorQualityWeight01()
        {
            LODSystemManager lodSystemManager = _lodSystemManager;
            if (lodSystemManager == null)
                return 1f;

            float qualityWeight = lodSystemManager.QualityWeight01;
            return math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private bool TryResolvePrimaryMaterial(
            GameObject obj,
            EntityId impostorID,
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

            if (!TryResolveCachedSourceRendererCold(obj, impostorID, out Renderer renderer))
                return false;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
                return false;

            sourceMaterial = sharedMaterial;
            albedoTexture = TryResolveTexture(sharedMaterial, _impostorAlbedoAtlasId, _baseMapId, _legacyBaseMapId, _mainTexId);
            normalTexture = resolveNormalTexture
                ? TryResolveTexture(sharedMaterial, _impostorNormalAtlasId, _bumpMapId, _normalMapId, _legacyNormalMapId)
                : null;
            return true;
        }

        private bool TryResolveCachedSourceRendererCold(GameObject obj, EntityId impostorID, out Renderer renderer)
        {
            renderer = null;
            if (obj == null)
                return false;

            if (_sourceRendererCache.TryGetValue(impostorID, out renderer) &&
                renderer != null &&
                renderer.sharedMaterial != null)
            {
                return true;
            }

            if (!TryResolvePrimaryRendererCold(obj.transform, out renderer))
                return false;

            if (_sourceRendererCache.ContainsKey(impostorID) ||
                _sourceRendererCache.Count < InitialImpostorCapacity)
            {
                _sourceRendererCache[impostorID] = renderer;
            }

            return true;
        }

        private bool TryResolvePrimaryRendererCold(Transform root, out Renderer renderer)
        {
            renderer = null;
            if (root == null)
                return false;

            int stackCount = 1;
            _rendererTraversalScratch[0] = root;
            while (stackCount > 0)
            {
                Transform current = _rendererTraversalScratch[--stackCount];
                _rendererTraversalScratch[stackCount] = null;
                if (current == null)
                    continue;

                if (current.TryGetComponent(out Renderer candidate) &&
                    candidate != null &&
                    candidate.sharedMaterial != null)
                {
                    renderer = candidate;
                    ClearRendererTraversalScratch(stackCount);
                    return true;
                }

                int childCount = current.childCount;
                if (childCount > RendererTraversalCapacity - stackCount)
                {
                    ClearRendererTraversalScratch(stackCount);
                    return false;
                }

                for (int childIndex = childCount - 1; childIndex >= 0; childIndex--)
                    _rendererTraversalScratch[stackCount++] = current.GetChild(childIndex);
            }

            return false;
        }

        private void ClearRendererTraversalScratch(int count)
        {
            for (int i = 0; i < count; i++)
                _rendererTraversalScratch[i] = null;
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

        private Material ResolveSharedImpostorMaterial()
        {
            return _authoredImpostorAtlasMaterial;
        }

        private bool TryResolveAuthoredAtlasEntry(
            Material sourceMaterial,
            Texture2D albedoTexture,
            out Vector4 atlasRect,
            out Color tint)
        {
            atlasRect = DefaultAtlasRect;
            tint = ResolveSourceTint(sourceMaterial);
            AuthoredImpostorAtlasEntry[] entries = _authoredImpostorAtlasEntries;
            if (entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                AuthoredImpostorAtlasEntry entry = entries[i];
                if (entry.SourceMaterial != null && entry.SourceMaterial == sourceMaterial)
                {
                    atlasRect = SanitizeAtlasRect(entry.AtlasRect);
                    tint = SanitizeAuthoredAtlasTint(entry.Tint);
                    return true;
                }
            }

            for (int i = 0; i < entries.Length; i++)
            {
                AuthoredImpostorAtlasEntry entry = entries[i];
                if (entry.AlbedoTexture != null && entry.AlbedoTexture == albedoTexture)
                {
                    atlasRect = SanitizeAtlasRect(entry.AtlasRect);
                    tint = SanitizeAuthoredAtlasTint(entry.Tint);
                    return true;
                }
            }

            return false;
        }

        private static Color SanitizeAuthoredAtlasTint(Color tint)
        {
            Color safeTint = SanitizeColor(tint);
            if (safeTint.a <= 0.0001f &&
                safeTint.r <= 0.0001f &&
                safeTint.g <= 0.0001f &&
                safeTint.b <= 0.0001f)
            {
                return Color.white;
            }

            return safeTint;
        }

        private static Color ResolveSourceTint(Material sourceMaterial)
        {
            if (sourceMaterial == null)
                return Color.white;

            if (sourceMaterial.HasProperty(_baseColorId))
                return SanitizeColor(sourceMaterial.GetColor(_baseColorId));

            if (sourceMaterial.HasProperty(_colorId))
                return SanitizeColor(sourceMaterial.GetColor(_colorId));

            return Color.white;
        }

        private static Vector4 SanitizeAtlasRect(Vector4 atlasRect)
        {
            if (!float.IsFinite(atlasRect.x) ||
                !float.IsFinite(atlasRect.y) ||
                !float.IsFinite(atlasRect.z) ||
                !float.IsFinite(atlasRect.w) ||
                atlasRect.z <= 0f ||
                atlasRect.w <= 0f)
            {
                return DefaultAtlasRect;
            }

            float width = math.clamp(atlasRect.z, 0.0001f, 1f);
            float height = math.clamp(atlasRect.w, 0.0001f, 1f);
            float x = math.clamp(atlasRect.x, 0f, 1f - width);
            float y = math.clamp(atlasRect.y, 0f, 1f - height);
            return new Vector4(x, y, width, height);
        }

        private static Color SanitizeColor(Color color)
        {
            if (!float.IsFinite(color.r) ||
                !float.IsFinite(color.g) ||
                !float.IsFinite(color.b) ||
                !float.IsFinite(color.a))
            {
                return Color.white;
            }

            return new Color(
                math.saturate(color.r),
                math.saturate(color.g),
                math.saturate(color.b),
                math.saturate(color.a));
        }

        private static Vector3 SanitizeVector3(Vector3 value)
        {
            if (!IsFiniteVector3(value))
                return Vector3.zero;

            return value;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
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
                float lodSize = float.IsFinite(lodGroup.size) ? Mathf.Abs(lodGroup.size) : MinimumBillboardWidth;
                size = Mathf.Max(MinimumBillboardWidth, lodSize);
                Vector3 referencePosition = originalTransform.TransformPoint(lodGroup.localReferencePoint);
                Vector3 originalPosition = originalTransform.position;
                billboardCenterOffset = IsFiniteVector3(referencePosition) && IsFiniteVector3(originalPosition)
                    ? referencePosition - originalPosition
                    : Vector3.zero;
            }
            else
            {
                Vector3 scale = originalTransform.lossyScale;
                float maxScale = Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                size = float.IsFinite(maxScale) ? Mathf.Max(MinimumBillboardWidth, maxScale) : MinimumBillboardWidth;
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
                Hecton8.Core.H8Debug.Log($"[ImpostorSystem] Impostor shader found: {impostorShader.name}");
            else
                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor package or shader not found.");
        }

        [MenuItem("Hecton8/LOD System/Create Impostor Baking Preset")]
        private static void CreateImpostorBakingPreset()
        {
            Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor bake preset creation is unavailable because the Amplify package is not installed.");
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
                if (prefab == null || !prefab.TryGetComponent<LODGroup>(out _))
                    continue;

                if (prefab.GetComponent("AmplifyImpostor") == null)
                    continue;

                bakedCount++;
            }

            Hecton8.Core.H8Debug.Log($"[ImpostorSystem] Batch bake scan complete. Candidates={bakedCount}");
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

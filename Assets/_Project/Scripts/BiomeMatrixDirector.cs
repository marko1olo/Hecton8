using System;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    public interface IBiomeMatrixEventListener
    {
        void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile);
        void OnDepthTierChanged(int depthTier, float depthMeters);
    }

    public static class BiomeMatrixEvents
    {
        private struct BiomeMatrixEventPayload
        {
            public byte EventType;
            public int ProfileSlot;
            public int DepthTier;
            public float DepthMeters;
        }

        private const byte MatrixBiomeChangedEventType = 1;
        private const byte DepthTierChangedEventType = 2;
        private const int PendingEventCapacity = 32;
        private const int MatrixProfileCacheCapacity = 128;

        private static readonly RegistryBucket<IBiomeMatrixEventListener> _listeners = new RegistryBucket<IBiomeMatrixEventListener>(16);
        private static readonly HectonBiomeMatrixProfile[] _profilesBySlot = new HectonBiomeMatrixProfile[MatrixProfileCacheCapacity]; // COLD ALLOC: HectonBiomeMatrixProfile[128] - stable profile lookup for deferred biome matrix payloads - owner: BiomeMatrixEvents
        private static NativeQueue<BiomeMatrixEventPayload> _pendingEvents;
        private static NativeQueue<BiomeMatrixEventPayload> _nextFrameEvents;
        private static int _profileSlotCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BiomeMatrixEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BiomeMatrixEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_profilesBySlot, 0, _profileSlotCount);
            _profileSlotCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(IBiomeMatrixEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IBiomeMatrixEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void RaiseMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            int profileSlot = ResolveProfileSlot(profile);

            Enqueue(new BiomeMatrixEventPayload
            {
                EventType = MatrixBiomeChangedEventType,
                ProfileSlot = profileSlot
            });
        }

        public static void RaiseDepthTierChanged(int depthTier, float depthMeters)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            Enqueue(new BiomeMatrixEventPayload
            {
                EventType = DepthTierChangedEventType,
                DepthTier = depthTier,
                DepthMeters = depthMeters
            });
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out BiomeMatrixEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void Enqueue(in BiomeMatrixEventPayload payload)
        {
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void Dispatch(in BiomeMatrixEventPayload payload)
        {
            IBiomeMatrixEventListener[] listenerBuffer = _listeners.RawArray;
            int listenerCount = _listeners.Count;
            if (payload.EventType == MatrixBiomeChangedEventType)
            {
                HectonBiomeMatrixProfile profile = null;
                if ((uint)payload.ProfileSlot < (uint)_profileSlotCount)
                    profile = _profilesBySlot[payload.ProfileSlot];

                for (int i = listenerCount - 1; i >= 0; i--)
                {
                    IBiomeMatrixEventListener listener = listenerBuffer[i];
                    if (listener != null)
                        listener.OnMatrixBiomeChanged(profile);
                }

                return;
            }

            if (payload.EventType == DepthTierChangedEventType)
            {
                for (int i = listenerCount - 1; i >= 0; i--)
                {
                    IBiomeMatrixEventListener listener = listenerBuffer[i];
                    if (listener != null)
                        listener.OnDepthTierChanged(payload.DepthTier, payload.DepthMeters);
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BiomeMatrixEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BiomeMatrixEventPayload>[32] - deferred biome matrix event lane flushed by SystemDispatcher - owner: BiomeMatrixEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BiomeMatrixEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BiomeMatrixEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BiomeMatrixEventPayload>[32] - next-frame biome matrix event lane prevents same-frame reentrant dispatch - owner: BiomeMatrixEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BiomeMatrixEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<BiomeMatrixEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static int ResolveProfileSlot(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return -1;

            for (int i = 0; i < _profileSlotCount; i++)
            {
                if (ReferenceEquals(_profilesBySlot[i], profile))
                    return i;
            }

            if (_profileSlotCount >= _profilesBySlot.Length)
                return -1;

            int slot = _profileSlotCount++;
            _profilesBySlot[slot] = profile;
            return slot;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4035)]
    [ExecuteAlways]
    public sealed class BiomeMatrixDirector : MonoBehaviour, ISlowTickable
    {
        private const string MissingProfileLabel = "No biome profile";
        private const string NorthCardinalRegionLabel = "North";
        private const string SouthCardinalRegionLabel = "South";
        private const string EastCardinalRegionLabel = "East";
        private const string WestCardinalRegionLabel = "West";
        private const string NoneClusterFocusLabel = "None";
        private const string FertileGrowthClusterFocusLabel = "FertileGrowth";
        private const string BiologicalNestClusterFocusLabel = "BiologicalNest";
        private const string ResourcePocketClusterFocusLabel = "ResourcePocket";
        private const string ShelterPocketClusterFocusLabel = "ShelterPocket";
        private const string HazardPocketClusterFocusLabel = "HazardPocket";
        private const string DebrisFieldClusterFocusLabel = "DebrisField";
        private const string RockCoverClusterFocusLabel = "RockCover";
        private const string NoneStructureFocusLabel = "None";
        private const string NaturalLandmarkStructureFocusLabel = "NaturalLandmark";
        private const string TechFragmentStructureFocusLabel = "TechFragment";
        private const string CaveReadStructureFocusLabel = "CaveRead";
        private const string BiologicalSilhouetteStructureFocusLabel = "BiologicalSilhouette";
        private const string NoneFaunaMoodLabel = "None";
        private const string CalmFaunaMoodLabel = "Calm";
        private const string LivelyFaunaMoodLabel = "Lively";
        private const string MixedFaunaMoodLabel = "Mixed";
        private const string HostileFaunaMoodLabel = "Hostile";

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonBiomeMatrixCatalog matrixCatalog;

        [Header("World Framing")]
        [SerializeField] private float surfaceOffsetMeters = 0f;
        [SerializeField] private Vector3 worldOrigin = Vector3.zero;
        [SerializeField] private float regionDeadZone = 24f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugTier = 1;
        [SerializeField] private string _debugRegion = "North";
        [SerializeField] private string _debugBiomeName = "None";
        [SerializeField] private int _debugMatrixIndex = -1;
        [SerializeField] private bool _debugPlaceholder;
        [SerializeField] private float _debugSurfaceLevelY;
#pragma warning disable 0414
        [SerializeField] private string _debugDepthSource = "SurfaceOffset";
#pragma warning restore 0414
#pragma warning disable 0414
        [SerializeField] private string _debugEvaluationSource = "Player";
#pragma warning restore 0414
        [SerializeField] private string _debugFamilyId = "None";
        [SerializeField] private string _debugFamilyLabel = "None";
        [SerializeField] private string _debugResolutionMode = "Exact";
        [SerializeField] private string _debugAtmosphereMood = "None";
        [SerializeField] private string _debugPrimaryResourceTheme = "None";
        [SerializeField] private string _debugNavigationStyle = "None";
        [SerializeField] private string _debugAtmosphereProfile = "None";
        [SerializeField] private string _debugFaunaFamily = "None";
        [SerializeField] private string _debugThreatStyle = "None";
        [SerializeField] private string _debugRecommendedLoadout = "None";
        [SerializeField] private string _debugResourcePlan = "None";
        [SerializeField] private string _debugResourceChannels = "None";
        [SerializeField] private string _debugEarlyFarmReason = "None";
        [SerializeField] private string _debugLateReturnReason = "None";
        [SerializeField] private string _debugExtractionStyle = "None";
        [SerializeField] private string _debugPocketResource = "None";
        [SerializeField] private string _debugNodeResource = "None";
        [SerializeField] private string _debugSafePocketResource = "None";
        [SerializeField] private string _debugRareObjectiveResource = "None";
        [SerializeField] private int _debugLoosePickupWeight;
        [SerializeField] private int _debugNodeExtractionWeight;
        [SerializeField] private int _debugSalvageRecoveryWeight;
        [SerializeField] private int _debugCommonResourcePull;
        [SerializeField] private int _debugUncommonResourcePull;
        [SerializeField] private int _debugRareResourcePull;
        [SerializeField] private string _debugLandmarkPlan = "None";
        [SerializeField] private string _debugDominantLandmarkRole = "None";
        [SerializeField] private string _debugRouteUse = "None";
        [SerializeField] private string _debugEmotionalRead = "None";
        [SerializeField] private string _debugSpatialPattern = "None";
        [SerializeField] private string _debugResourcePocketPattern = "None";
        [SerializeField] private string _debugNodeClusterPattern = "None";
        [SerializeField] private string _debugSafePocketPattern = "None";
        [SerializeField] private string _debugRouteAnchorPattern = "None";
        [SerializeField] private string _debugRareObjectivePattern = "None";
        [SerializeField] private string _debugExplorationLoop = "None";
        [SerializeField] private string _debugWhyPlayerComesHere = "None";
        [SerializeField] private int _debugRouteClarity;
        [SerializeField] private int _debugSafePocketFrequency;
        [SerializeField] private int _debugRareRewardPull;
        [SerializeField] private int _debugEncounterPressure;
        [SerializeField] private int _debugHazardPressure;
        [SerializeField] private string _debugVisitPurpose = "None";
        [SerializeField] private string _debugCommonRewardHook = "None";
        [SerializeField] private string _debugRareRewardHook = "None";
        [SerializeField] private string _debugLandmarkIdentity = "None";
        [SerializeField] private string _debugSafePocketIdentity = "None";
        [SerializeField] private string _debugRiskSummary = "None";
        [SerializeField] private string _debugExtractionFocus = "None";
        [SerializeField] private string _debugLandmarkGuidance = "None";
        [SerializeField] private int _debugLoosePickupBias;
        [SerializeField] private int _debugNodeExtractionBias;
        [SerializeField] private int _debugSalvageBias;
        [SerializeField] private int _debugCommonResourceBias;
        [SerializeField] private int _debugUncommonResourceBias;
        [SerializeField] private int _debugRareResourceBias;
        [SerializeField] private int _debugRoutePressure;
        [SerializeField] private int _debugLandmarkStrengthValue;
        [SerializeField] private int _debugRewardPullValue;
        [SerializeField] private int _debugSurvivalPressure;
        [SerializeField] private string _debugPrimaryClusterFocus = "None";
        [SerializeField] private string _debugSecondaryClusterFocus = "None";
        [SerializeField] private string _debugPrimaryStructureFocus = "None";
        [SerializeField] private string _debugSecondaryStructureFocus = "None";
        [SerializeField] private string _debugFaunaMoodValue = "None";

        private bool _registeredToTickManager;
        private HectonBiomeMatrixProfile _currentProfile;
        private int _currentDepthTier = 1;
        private float _currentDepthMeters;
        private HectonPlayerMovement _playerMovement;
        private HectonFluidEngine _resolvedFluidEngine;
        private MapMagicBridge _resolvedMapMagicBridge;
        private HectonAtmosphereManager _resolvedAtmosphereManager;
        private bool _editorPreviewDirty = true;
        private Transform _editorLastEvaluationTransform;
        private Vector3 _editorLastEvaluationPosition;
        private float _editorLastSurfaceLevelY = float.NaN;

        internal static BiomeMatrixDirector ActiveRuntimeInstance { get; private set; }

        public HectonBiomeMatrixProfile CurrentProfile => _currentProfile;
        public HectonBiomeFamilyProfile CurrentFamilyProfile => _currentProfile != null ? _currentProfile.familyProfile : null;
        public HectonBiomeMatrixCatalog MatrixCatalog => matrixCatalog;
        public bool HasCatalog => matrixCatalog != null && matrixCatalog.Count > 0;
        public int CurrentDepthTier => _currentDepthTier;
        public float CurrentDepthMeters => _currentDepthMeters;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;
#endif

            TryRegister();
#if UNITY_EDITOR
            _editorPreviewDirty = true;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void Start()
        {
            TryRegister();

            EvaluateMatrix(forcePublish: true);
        }

        private void OnDisable()
        {
            TryUnregister();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            TryUnregister();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            if (Application.isPlaying)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (!ShouldEvaluateEditorPreview())
                return;

            EvaluateMatrix(forcePublish: false);
            CacheEditorPreviewState();
            _editorPreviewDirty = false;
        }

        private bool ShouldEvaluateEditorPreview()
        {
            if (_editorPreviewDirty)
                return true;

            Transform evaluationTransform = ResolveEvaluationTransform();
            if (!ReferenceEquals(_editorLastEvaluationTransform, evaluationTransform))
                return true;

            if (evaluationTransform == null || !HasCatalog)
                return false;

            Vector3 evaluationPosition = evaluationTransform.position;
            if ((evaluationPosition - _editorLastEvaluationPosition).sqrMagnitude > 0.0001f)
                return true;

            float surfaceLevelY = ResolveSurfaceLevelY();
            return !Mathf.Approximately(surfaceLevelY, _editorLastSurfaceLevelY);
        }

        private void CacheEditorPreviewState()
        {
            Transform evaluationTransform = ResolveEvaluationTransform();
            _editorLastEvaluationTransform = evaluationTransform;
            _editorLastEvaluationPosition = evaluationTransform != null
                ? evaluationTransform.position
                : Vector3.zero;
            _editorLastSurfaceLevelY = evaluationTransform != null && HasCatalog
                ? ResolveSurfaceLevelY()
                : float.NaN;
        }
#endif

        private void TryRegister()
        {
            if (!Application.isPlaying || _registeredToTickManager)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        public void SlowTick()
        {
            EvaluateMatrix(forcePublish: false);
        }

        /// <summary>
        /// Forces immediate biome matrix evaluation for the current player position.
        /// </summary>
        public void ForceRefresh()
        {
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
        }

        public void SetMatrixCatalog(HectonBiomeMatrixCatalog catalog)
        {
            matrixCatalog = catalog;
#if UNITY_EDITOR
            _editorPreviewDirty = true;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            _editorPreviewDirty = true;
        }
#endif

        private void EvaluateMatrix(bool forcePublish)
        {
            ResolveReferences();
            Transform evaluationTransform = ResolveEvaluationTransform();

            if (evaluationTransform == null || !HasCatalog)
            {
                bool hadProfile = _currentProfile != null;
                _currentProfile = null;
                _currentDepthMeters = 0f;
                _currentDepthTier = 1;
                _debugResolutionMode = evaluationTransform == null ? "Missing evaluation transform" : "Missing catalog";
                if (hadProfile && Application.isPlaying)
                    BiomeMatrixEvents.RaiseMatrixBiomeChanged(null);
                UpdateDiagnostics(null, 1, HectonBiomeMatrixProfile.CardinalRegion.North);
                return;
            }

            float surfaceLevelY = ResolveSurfaceLevelY();
            float depth = Mathf.Max(0f, surfaceLevelY - evaluationTransform.position.y);
            int tier = ResolveDepthTier(depth);
            HectonBiomeMatrixProfile.CardinalRegion region = ResolveRegion(evaluationTransform.position);
            bool usedFallback;
            HectonBiomeMatrixProfile next = ResolveMatrixProfile(tier, region, out usedFallback);
            bool depthTierChanged = forcePublish || tier != _currentDepthTier;

            _currentDepthMeters = depth;
            _currentDepthTier = tier;
            _debugSurfaceLevelY = surfaceLevelY;
            _debugResolutionMode = next == null ? MissingProfileLabel : usedFallback ? "Fallback" : "Exact";

            if (depthTierChanged && Application.isPlaying)
                BiomeMatrixEvents.RaiseDepthTierChanged(_currentDepthTier, _currentDepthMeters);

            if (forcePublish || next != _currentProfile)
            {
                _currentProfile = next;
                if (Application.isPlaying)
                {
                    GlobalTelemetryBus.PublishBiomeVisited(next != null ? next.biomeName : string.Empty, tier, depth);
                    BiomeMatrixEvents.RaiseMatrixBiomeChanged(_currentProfile);
                }
            }

            UpdateDiagnostics(_currentProfile, tier, region);
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform != null && _playerMovement == null)
                playerTransform.TryGetComponent(out _playerMovement);
        }

        private Transform ResolveEvaluationTransform()
        {
            if (Application.isPlaying)
            {
                _debugEvaluationSource = "Player";
                return playerTransform;
            }

#if UNITY_EDITOR
            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
            if (sceneViewCamera != null)
            {
                _debugEvaluationSource = "SceneView";
                return sceneViewCamera.transform;
            }
#endif

            _debugEvaluationSource = "Player";
            return playerTransform;
        }

        private HectonBiomeMatrixProfile ResolveMatrixProfile(int tier, HectonBiomeMatrixProfile.CardinalRegion region, out bool usedFallback)
        {
            usedFallback = false;
            if (matrixCatalog == null)
                return null;

            HectonBiomeMatrixProfile exact = matrixCatalog.Resolve(tier, region);
            if (exact != null)
                return exact;

            HectonBiomeMatrixProfile[] profiles = matrixCatalog.Profiles;
            if (profiles == null || profiles.Length == 0)
                return null;

            HectonBiomeMatrixProfile bestProfile = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                int tierDelta = Mathf.Abs(profile.depthTier - tier);
                int score = 0;
                score -= tierDelta * 20;

                if (profile.depthTier == tier)
                    score += 1200;
                else if (tierDelta <= 1)
                    score += 200;

                if (profile.region == region)
                    score += 150;

                if (!profile.isPlaceholder)
                    score += 100;

                if (!string.IsNullOrWhiteSpace(profile.biomeName))
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestProfile = profile;
                }
            }

            usedFallback = bestProfile != null;
            return bestProfile;
        }

        private float ResolveSurfaceLevelY()
        {
            if (_playerMovement != null)
            {
                _debugDepthSource = "PlayerMovement";
                return _playerMovement.CurrentWaterSurfaceY;
            }

            if (_resolvedFluidEngine == null)
            {
                _resolvedFluidEngine = GlobalRegistry.Fluid;
            }

            if (_resolvedFluidEngine != null)
            {
                _debugDepthSource = "FluidEngine";
                return _resolvedFluidEngine.WaterLevel;
            }

            if (_resolvedMapMagicBridge == null)
            {
                _resolvedMapMagicBridge = Application.isPlaying
                    ? MapMagicBridge.Instance
                    : MapMagicBridge.Instance;
            }

            if (_resolvedMapMagicBridge != null)
            {
                _debugDepthSource = "MapMagicBridge";
                return _resolvedMapMagicBridge.WaterSurfaceLevel;
            }

            if (_resolvedAtmosphereManager == null)
            {
                _resolvedAtmosphereManager = Application.isPlaying
                    ? Hecton8.Core.GlobalRegistry.Atmosphere
                    : Hecton8.Core.GlobalRegistry.Atmosphere;
            }

            if (_resolvedAtmosphereManager != null)
            {
                _debugDepthSource = "AtmosphereManager";
                return _resolvedAtmosphereManager.SeaLevelY;
            }

            _debugDepthSource = "SurfaceOffset";
            return surfaceOffsetMeters;
        }

        private int ResolveDepthTier(float depth)
        {
            if (depth <= 0f)
                return 1;
            if (depth <= 300f)
                return 2;
            if (depth <= 600f)
                return 3;
            if (depth <= 1000f)
                return 4;
            if (depth <= 1500f)
                return 5;
            if (depth <= 2000f)
                return 6;
            if (depth <= 2500f)
                return 7;
            if (depth <= 3000f)
                return 8;
            if (depth <= 3500f)
                return 9;

            if (depth >= 14000f)
                return 27;

            float clamped = Mathf.Clamp(depth, 3500f, 14000f);
            float normalized = (clamped - 3500f) / 10500f;
            int tier = 10 + Mathf.FloorToInt(normalized * 17f);
            return Mathf.Clamp(tier, 10, 26);
        }

        private HectonBiomeMatrixProfile.CardinalRegion ResolveRegion(Vector3 position)
        {
            Vector3 delta = position - worldOrigin;
            delta.y = 0f;

            if (Mathf.Abs(delta.x) <= regionDeadZone && Mathf.Abs(delta.z) <= regionDeadZone)
                return HectonBiomeMatrixProfile.CardinalRegion.North;

            if (Mathf.Abs(delta.z) >= Mathf.Abs(delta.x))
                return delta.z >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.North : HectonBiomeMatrixProfile.CardinalRegion.South;

            return delta.x >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.East : HectonBiomeMatrixProfile.CardinalRegion.West;
        }

        private void UpdateDiagnostics(
            HectonBiomeMatrixProfile profile,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            _debugTier = tier;
            _debugRegion = ResolveCardinalRegionLabel(region);
            _debugBiomeName = profile != null ? profile.biomeName : "None";
            _debugMatrixIndex = profile != null ? profile.matrixIndex : -1;
            _debugPlaceholder = profile != null && profile.isPlaceholder;
            _debugFamilyId = profile != null ? profile.familyId : "None";
            _debugFamilyLabel = profile != null && profile.familyProfile != null ? profile.familyProfile.familyLabel : "None";
            _debugAtmosphereMood = profile != null && profile.familyProfile != null ? profile.familyProfile.atmosphereMood : "None";
            _debugPrimaryResourceTheme = profile != null && profile.familyProfile != null ? profile.familyProfile.primaryResourceTheme : "None";
            _debugNavigationStyle = profile != null && profile.familyProfile != null ? profile.familyProfile.navigationStyle : "None";
            _debugAtmosphereProfile = profile != null && profile.familyProfile != null && profile.familyProfile.atmosphereProfile != null ? profile.familyProfile.atmosphereProfile.name : "None";
            _debugFaunaFamily = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.familyLabel : "None";
            _debugThreatStyle = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.threatStyle : "None";
            _debugRecommendedLoadout = profile != null && profile.familyProfile != null && profile.familyProfile.recommendedLoadoutPreset != null ? profile.familyProfile.recommendedLoadoutPreset.presetName : "None";
            _debugResourcePlan = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.profileLabel : "None";
            _debugResourceChannels = profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.profileLabel : "None";
            _debugEarlyFarmReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.earlyReasonToFarm : "None";
            _debugLateReturnReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.lateReasonToReturn : "None";
            _debugExtractionStyle = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.extractionStyle : "None";
            _debugPocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.resourcePocketItem : null);
            _debugNodeResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.nodeClusterItem : null);
            _debugSafePocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.safePocketItem : null);
            _debugRareObjectiveResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.rareObjectiveRewardItem : null);
            _debugLoosePickupWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.loosePickupWeight : 0;
            _debugNodeExtractionWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.nodeExtractionWeight : 0;
            _debugSalvageRecoveryWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.salvageRecoveryWeight : 0;
            _debugCommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.commonResourcePull : 0;
            _debugUncommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.uncommonResourcePull : 0;
            _debugRareResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.rareResourcePull : 0;
            _debugLandmarkPlan = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.profileLabel : "None";
            _debugDominantLandmarkRole = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.dominantLandmarkRole : "None";
            _debugRouteUse = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.routeUse : "None";
            _debugEmotionalRead = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.emotionalRead : "None";
            _debugSpatialPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.profileLabel : "None";
            _debugResourcePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.resourcePocketPattern : "None";
            _debugNodeClusterPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.nodeClusterPattern : "None";
            _debugSafePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.safePocketPattern : "None";
            _debugRouteAnchorPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.routeAnchorPattern : "None";
            _debugRareObjectivePattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.rareObjectivePattern : "None";
            _debugExplorationLoop = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.explorationLoop : "None";
            _debugWhyPlayerComesHere = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.whyPlayerComesHere : "None";
            _debugRouteClarity = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.routeClarity : 0;
            _debugSafePocketFrequency = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.safePocketFrequency : 0;
            _debugRareRewardPull = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.rareRewardPull : 0;
            _debugEncounterPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.encounterPressure : 0;
            _debugHazardPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.hazardPressure : 0;
            _debugVisitPurpose = profile != null ? profile.visitPurpose : "None";
            _debugCommonRewardHook = profile != null ? profile.commonRewardHook : "None";
            _debugRareRewardHook = profile != null ? profile.rareRewardHook : "None";
            _debugLandmarkIdentity = profile != null ? profile.landmarkIdentity : "None";
            _debugSafePocketIdentity = profile != null ? profile.safePocketIdentity : "None";
            _debugRiskSummary = profile != null ? profile.riskSummary : "None";
            _debugExtractionFocus = profile != null ? profile.extractionFocus : "None";
            _debugLandmarkGuidance = profile != null ? profile.landmarkGuidance : "None";
            _debugLoosePickupBias = profile != null ? profile.loosePickupBias : 0;
            _debugNodeExtractionBias = profile != null ? profile.nodeExtractionBias : 0;
            _debugSalvageBias = profile != null ? profile.salvageBias : 0;
            _debugCommonResourceBias = profile != null ? profile.commonResourceBias : 0;
            _debugUncommonResourceBias = profile != null ? profile.uncommonResourceBias : 0;
            _debugRareResourceBias = profile != null ? profile.rareResourceBias : 0;
            _debugRoutePressure = profile != null ? profile.routePressure : 0;
            _debugLandmarkStrengthValue = profile != null ? profile.landmarkStrength : 0;
            _debugRewardPullValue = profile != null ? profile.rewardPull : 0;
            _debugSurvivalPressure = profile != null ? profile.survivalPressure : 0;
            _debugPrimaryClusterFocus = profile != null ? ResolveClusterFocusLabel(profile.primaryClusterFocus) : "None";
            _debugSecondaryClusterFocus = profile != null ? ResolveClusterFocusLabel(profile.secondaryClusterFocus) : "None";
            _debugPrimaryStructureFocus = profile != null ? ResolveStructureFocusLabel(profile.primaryStructureFocus) : "None";
            _debugSecondaryStructureFocus = profile != null ? ResolveStructureFocusLabel(profile.secondaryStructureFocus) : "None";
            _debugFaunaMoodValue = profile != null ? ResolveFaunaMoodLabel(profile.faunaMood) : "None";
        }

        private static string GetItemLabel(Hecton8.Items.ItemData item)
        {
            if (item == null)
                return "None";

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        private static string ResolveCardinalRegionLabel(HectonBiomeMatrixProfile.CardinalRegion region)
        {
            switch (region)
            {
                case HectonBiomeMatrixProfile.CardinalRegion.South:
                    return SouthCardinalRegionLabel;
                case HectonBiomeMatrixProfile.CardinalRegion.East:
                    return EastCardinalRegionLabel;
                case HectonBiomeMatrixProfile.CardinalRegion.West:
                    return WestCardinalRegionLabel;
                default:
                    return NorthCardinalRegionLabel;
            }
        }

        private static string ResolveClusterFocusLabel(WorldProceduralClusterFocus focus)
        {
            switch (focus)
            {
                case WorldProceduralClusterFocus.FertileGrowth:
                    return FertileGrowthClusterFocusLabel;
                case WorldProceduralClusterFocus.BiologicalNest:
                    return BiologicalNestClusterFocusLabel;
                case WorldProceduralClusterFocus.ResourcePocket:
                    return ResourcePocketClusterFocusLabel;
                case WorldProceduralClusterFocus.ShelterPocket:
                    return ShelterPocketClusterFocusLabel;
                case WorldProceduralClusterFocus.HazardPocket:
                    return HazardPocketClusterFocusLabel;
                case WorldProceduralClusterFocus.DebrisField:
                    return DebrisFieldClusterFocusLabel;
                case WorldProceduralClusterFocus.RockCover:
                    return RockCoverClusterFocusLabel;
                default:
                    return NoneClusterFocusLabel;
            }
        }

        private static string ResolveStructureFocusLabel(WorldProceduralStructureFocus focus)
        {
            switch (focus)
            {
                case WorldProceduralStructureFocus.NaturalLandmark:
                    return NaturalLandmarkStructureFocusLabel;
                case WorldProceduralStructureFocus.TechFragment:
                    return TechFragmentStructureFocusLabel;
                case WorldProceduralStructureFocus.CaveRead:
                    return CaveReadStructureFocusLabel;
                case WorldProceduralStructureFocus.BiologicalSilhouette:
                    return BiologicalSilhouetteStructureFocusLabel;
                default:
                    return NoneStructureFocusLabel;
            }
        }

        private static string ResolveFaunaMoodLabel(WorldProceduralFaunaMood mood)
        {
            switch (mood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return CalmFaunaMoodLabel;
                case WorldProceduralFaunaMood.Lively:
                    return LivelyFaunaMoodLabel;
                case WorldProceduralFaunaMood.Mixed:
                    return MixedFaunaMoodLabel;
                case WorldProceduralFaunaMood.Hostile:
                    return HostileFaunaMoodLabel;
                default:
                    return NoneFaunaMoodLabel;
            }
        }
    }
}

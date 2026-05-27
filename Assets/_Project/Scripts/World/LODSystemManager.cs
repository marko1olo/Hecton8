// ============================================================================
// HECTON-8 — LODSystemManager.cs
// Central coordinator for automatic LOD (Level of Detail) management.
//
// RESPONSIBILITIES:
//   • Register/unregister LODGroup components
//   • Resolve a capped camera-relative distance slice
//   • Apply LOD transitions (crossfade/discrete)
//   • Manage quality presets (Low/Medium/High)
//   • Persist LOD settings via SaveManager
//
// ARCHITECTURE:
//   • GlobalRegistry.LODSystem is the authoritative runtime lookup.
//   • ITickable — registers with GameTickManager
//   • ISaveable — persists quality settings
//   • Zero-GC — pre-allocated collections and fixed distance scratch
//   • AUP-backed far-distance precision beyond floating origin
//
// PERFORMANCE:
//   • Target: < 0.2ms per frame for 64 LODGroups
//   • Zero GC allocations in hot paths
//   • Squared distance calculations (no sqrt)
//
// INTEGRATION:
//   • GameTickManager — ITickable registration
//   • SaveManager — ISaveable (LoadPriority=5)
//   • AbsoluteUniversePosition — far-distance precision beyond floating origin
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;

namespace Hecton8.World
{
    /// <summary>
    /// Quality preset for LOD system.
    /// Controls LOD bias multiplier affecting transition distances.
    /// </summary>
    public enum LODQualityPreset
    {
        /// <summary>LOD Bias = 1.5 (aggressive culling, better performance)</summary>
        Low,
        
        /// <summary>LOD Bias = 1.0 (balanced)</summary>
        Medium,
        
        /// <summary>LOD Bias = 0.7 (quality focus, longer LOD residency)</summary>
        High
    }

    /// <summary>
    /// Central coordinator for automatic LOD management.
    /// Maintains 60 FPS @ 1080p through distance-based mesh simplification.
    /// </summary>
    /// <remarks>
    /// ZERO-GC ARCHITECTURE:
    ///   • Pre-allocated collections with capacity
    ///   • Fixed hot-path distance scratch
    ///   • No LINQ, no string operations in hot paths
    ///   • Struct-based data where possible
    /// 
    /// PERFORMANCE TARGET:
    ///   • LOD processing: < 0.2ms per frame
    ///   • Distance solve: 64 groups per frame
    ///   • Total: < 0.2ms per frame
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)] // Run before gameplay systems
    public sealed class LODSystemManager : MonoBehaviour, ITickable, ILateFrameTickable, ISaveable, IGlobalRegistryHotSwapListener
    {
        private const float CameraResolveRetryInterval = 1f;
        private const int MaxHotPathLODGroupsPerFrame = 64;
        private const float AupDistanceThresholdMeters = 50f;
        private const float AupDistanceThresholdSqr = AupDistanceThresholdMeters * AupDistanceThresholdMeters;
        private const float LODSolveBudgetWarningMs = 0.2f;
        private const int LODPerformanceWarningCooldownFrames = 30;
        private const uint LODSolveBudgetWarningHash = 0x4C4F4457u;
        private const uint LODSystemContextHash = 0x4C4F4453u;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Registry-backed runtime instance. Null if not initialized.
        /// </summary>
        private static LODSystemManager s_activeRuntime;

        public static LODSystemManager Instance => s_activeRuntime;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── LOD Configuration ──────────────────")]
        [SerializeField, Tooltip("Quality preset (Low/Medium/High)")]
        private LODQualityPreset _qualityPreset = LODQualityPreset.Medium;

        [SerializeField, Tooltip("Crossfade distance threshold (meters)")]
        private float _crossfadeDistanceThreshold = 50f;

        [Header("── Performance ──────────────────")]
        [SerializeField, Tooltip("Authoring cap for registered LOD groups. Runtime Tick applies a hard 64-group hot-path batch.")]
        private int _maxLODGroupsPerFrame = 500;

        [SerializeField, Tooltip("Enable performance monitoring")]
        private bool _enablePerformanceMonitoring = true;

        [SerializeField, Tooltip("Optional explicit main camera reference. Falls back to cold-path camera resolve.")]
        private Camera _cameraReference;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: List<LODGroup>[500] — registered LOD groups — owner: LODSystemManager
        private readonly List<LODGroup> _registeredLODGroups = new List<LODGroup>(500);

        // COLD ALLOC: List<Transform>[500] — cached transforms — owner: LODSystemManager
        private readonly List<Transform> _lodGroupTransforms = new List<Transform>(500);

        // COLD ALLOC: List<Vector3>[500] — presentation-only LOD anchors — owner: LODSystemManager
        private readonly List<Vector3> _lodGroupPresentationPositions = new List<Vector3>(500);

        // COLD ALLOC: HashSet<LODGroup>[500] — O(1) duplicate check — owner: LODSystemManager
        private readonly HashSet<LODGroup> _registeredLODGroupsSet = new HashSet<LODGroup>(500);

        // COLD ALLOC: float[64] - capped hot-path distance scratch - owner: LODSystemManager
        private float[] _lodGroupSquaredDistances;

        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _saveRegistered;

        private Camera _mainCamera;
        private Transform _cameraTransform;
        private ISaveService _saveService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ITickDispatcher _dispatcher;
        private DynamicResolutionScaler _dynamicResolutionScaler;
        private ImpostorSystem _impostorSystem;
        private int _viewerAupCacheFrame = -1;
        private AbsoluteUniversePosition _viewerAupCache;
        private float _cameraResolveRetryTimer;
        private float _defaultLODBias = 1f;
        private float _lodRuntimeClockSeconds;
        private float _nextNullCleanupTime;
        private int _nullCleanupCursor;
        private int _lodHotPathCursor;
        private int _lodBatchStartIndex;
        private int _scheduledLODGroupBatchCount;
        private int _nextLODPerformanceWarningFrame;
        private int _lastFrameTransitionCount;
        private bool _registeredHotSwapListener;

        private float _lodSystemCPUTime;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Count of registered LOD groups.
        /// </summary>
        public int RegisteredLODGroupCount => _registeredLODGroups.Count;

        /// <summary>
        /// LOD system CPU time in milliseconds (last frame).
        /// </summary>
        public float LODSystemCPUTime => _lodSystemCPUTime;

        /// <summary>
        /// Current quality preset.
        /// </summary>
        public LODQualityPreset QualityPreset => _qualityPreset;

        /// <summary>
        /// Count of LOD mode transitions applied during the last Tick batch.
        /// </summary>
        public int LastFrameTransitionCount => _lastFrameTransitionCount;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
        }

        private void Awake()
        {
            LODSystemManager registered = GlobalRegistry.LODSystem;
            if (registered != null && registered != this)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Duplicate registry owner detected. Destroying duplicate.");
                #endif
                Destroy(gameObject);
                return;
            }

            CacheRegistryServicesCold();
            EnsureDistanceScratchAllocated();
            _defaultLODBias = QualitySettings.lodBias;
            _lodRuntimeClockSeconds = 0f;
            _nextNullCleanupTime = 0f;
            TryResolveMainCamera();
            ApplyQualityPreset(_qualityPreset);

            TryRegisterSaveParticipant();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[LODSystemManager] Initialized. Max LOD groups: " + _maxLODGroupsPerFrame);
            #endif
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            InvalidateViewerAupCache();
            EnsureDistanceScratchAllocated();
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            RestoreDefaultLODBias();
            UnregisterAllImpostorCandidates();
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();

            ReleaseDistanceScratch();
            ClearCachedRegistryServices();
            InvalidateViewerAupCache();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();

            ReleaseDistanceScratch();

            RestoreDefaultLODBias();
            UnregisterAllImpostorCandidates();
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearCachedRegistryServices();
            InvalidateViewerAupCache();
        }

        private void EnsureDistanceScratchAllocated()
        {
            if (_lodGroupSquaredDistances == null || _lodGroupSquaredDistances.Length < MaxHotPathLODGroupsPerFrame)
                _lodGroupSquaredDistances = new float[MaxHotPathLODGroupsPerFrame];
        }

        private void ReleaseDistanceScratch()
        {
            _lodGroupSquaredDistances = null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || _dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterLODSystemRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.LODSystem, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.LODSystem, this))
                GlobalRegistry.UnregisterLODSystemRuntime(this);

            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveService = null;
            _saveRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_dynamicResolutionScaler == null)
                _dynamicResolutionScaler = GlobalRegistry.DynamicResolution;

            if (_impostorSystem == null)
                _impostorSystem = ImpostorSystem.Instance;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;
        }

        private void ClearCachedRegistryServices()
        {
            _playerRuntimeContext = null;
            _dispatcher = null;
            _dynamicResolutionScaler = null;
            _impostorSystem = null;
            _saveService = null;
            _mainCamera = null;
            _cameraTransform = null;
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
                    InvalidateViewerAupCache();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcher = currentService as ITickDispatcher;
                    _registered = false;
                    TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionScaler = currentService as DynamicResolutionScaler;
                    break;
                case GlobalRegistryServiceSlot.ImpostorRuntime:
                    _impostorSystem = currentService as ImpostorSystem;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    if (isActiveAndEnabled)
                        TryRegisterSaveParticipant();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITICKABLE IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Main LOD system update loop.
        /// Schedules distance calculation jobs and applies LOD transitions.
        /// </summary>
        /// <param name="dt">Delta time from GameTickManager</param>
        public void Tick(float dt)
        {
            _lastFrameTransitionCount = 0;
            AdvanceLodRuntimeClock(dt);

            // Cache camera reference
            if (_mainCamera == null && !TryResolveMainCamera(dt))
            {
                return;
            }

            // Early exit if no LOD groups registered
            if (_registeredLODGroups.Count == 0) return;

            float now = ResolveLodRuntimeClockSeconds();
            if (now >= _nextNullCleanupTime)
            {
                _nextNullCleanupTime = now + 1f;
                CleanupNullRegistrations();

                if (_registeredLODGroups.Count == 0) return;
            }

            long startTicks = 0;
            if (_enablePerformanceMonitoring)
                startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            CalculateDistanceSlice();

            if (_enablePerformanceMonitoring)
            {
                long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _lodSystemCPUTime = (endTicks - startTicks) / (float)System.Diagnostics.Stopwatch.Frequency * 1000f;
                PublishLODPerformanceWarningIfNeeded(_lodSystemCPUTime);
            }
        }

        public void LateFrameTick()
        {
            ApplyLODTransitions();
        }

        private void AdvanceLodRuntimeClock(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _lodRuntimeClockSeconds = math.min(_lodRuntimeClockSeconds + safeDeltaTime, 86400f);
        }

        private float ResolveLodRuntimeClockSeconds()
        {
            return math.isfinite(_lodRuntimeClockSeconds) ? _lodRuntimeClockSeconds : 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  ISAVEABLE IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Save priority (Core system).
        /// </summary>
        public int SavePriority => 5;

        /// <summary>
        /// Load priority (Core system).
        /// </summary>
        public int LoadPriority => 5;

        /// <summary>
        /// Save LOD settings to SaveData.
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            // Save quality preset as integer
            data.LODQualityPreset = (int)_qualityPreset;
        }

        /// <summary>
        /// Load LOD settings from SaveData.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            // Validate and restore quality preset
            int presetValue = data.LODQualityPreset;
            if (presetValue >= 0 && presetValue <= 2)
            {
                ApplyQualityPreset((LODQualityPreset)presetValue);
            }
            else
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Invalid quality preset value. Using default (Medium).");
                #endif
                ApplyQualityPreset(LODQualityPreset.Medium);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Register LODGroup for automatic management.
        /// Called by LODGroup components during OnEnable.
        /// </summary>
        /// <param name="lodGroup">LODGroup to register</param>
        public void RegisterLODGroup(LODGroup lodGroup)
        {
            if (lodGroup == null) return;
            
            // O(1) duplicate check via HashSet
            if (_registeredLODGroupsSet.Contains(lodGroup)) return;

            Transform lodTransform = lodGroup.transform;
            _registeredLODGroups.Add(lodGroup);
            _lodGroupTransforms.Add(lodTransform);
            _lodGroupPresentationPositions.Add(lodTransform.position);
            lodTransform.hasChanged = false;
            _registeredLODGroupsSet.Add(lodGroup);
            TryRegisterImpostorCandidate(lodGroup);

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_registeredLODGroups.Count > _maxLODGroupsPerFrame)
            {
                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Registered LOD groups exceeds max capacity. Consider increasing capacity.");
            }
            #endif
        }

        /// <summary>
        /// Unregister LODGroup from management.
        /// Called by LODGroup components during OnDisable.
        /// </summary>
        /// <param name="lodGroup">LODGroup to unregister</param>
        public void UnregisterLODGroup(LODGroup lodGroup)
        {
            if (lodGroup == null) return;

            // O(1) check via HashSet
            if (!_registeredLODGroupsSet.Remove(lodGroup)) return;

            TryUnregisterImpostorCandidate(lodGroup);

            // Find and remove from lists (O(n) but only if HashSet confirmed presence)
            for (int i = _registeredLODGroups.Count - 1; i >= 0; i--)
            {
                if (_registeredLODGroups[i] == lodGroup)
                {
                    // Swap-remove pattern for O(1) removal
                    int lastIndex = _registeredLODGroups.Count - 1;
                    if (i != lastIndex)
                    {
                        _registeredLODGroups[i] = _registeredLODGroups[lastIndex];
                        _lodGroupTransforms[i] = _lodGroupTransforms[lastIndex];
                        _lodGroupPresentationPositions[i] = _lodGroupPresentationPositions[lastIndex];
                    }
                    _registeredLODGroups.RemoveAt(lastIndex);
                    _lodGroupTransforms.RemoveAt(lastIndex);
                    _lodGroupPresentationPositions.RemoveAt(lastIndex);
                    break;
                }
            }
        }

        /// <summary>
        /// Get current LOD bias multiplier based on quality preset.
        /// </summary>
        /// <returns>LOD bias multiplier (1.5/1.0/0.7)</returns>
        public float GetLODBias()
        {
            switch (_qualityPreset)
            {
                case LODQualityPreset.Low:    return 1.5f;
                case LODQualityPreset.Medium: return 1.0f;
                case LODQualityPreset.High:   return 0.7f;
                default:                      return 1.0f;
            }
        }

        public static float ResolvePresetQualityWeight01(LODQualityPreset preset)
        {
            switch (preset)
            {
                case LODQualityPreset.Low:
                    return 0.25f;
                case LODQualityPreset.Medium:
                    return 0.62f;
                case LODQualityPreset.High:
                    return 1f;
                default:
                    return 0.62f;
            }
        }

        public float ApplyEmergencyLODBiasStrike()
        {
            float current = QualitySettings.lodBias;
            float next = math.max(0.35f, current - 0.1f);
            if (next < current)
                QualitySettings.lodBias = next;

            return QualitySettings.lodBias;
        }

        /// <summary>
        /// Set quality preset and apply LOD bias immediately.
        /// </summary>
        /// <param name="preset">Quality preset to apply</param>
        public void SetQualityPreset(LODQualityPreset preset)
        {
            ApplyQualityPreset(preset);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS — DISTANCE CALCULATION
        // ══════════════════════════════════════════════════════════

        private void CalculateDistanceSlice()
        {
            if (_registeredLODGroups.Count == 0) return;

            EnsureDistanceScratchAllocated();
            Vector3 cameraPosition = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;
            int count = ResolveHotPathLODGroupBatchCount();
            if (_lodHotPathCursor >= _registeredLODGroups.Count)
                _lodHotPathCursor = 0;

            _lodBatchStartIndex = _lodHotPathCursor;
            _scheduledLODGroupBatchCount = count;

            for (int i = 0; i < count; i++)
            {
                int lodGroupIndex = ResolveHotPathLODGroupIndex(_lodBatchStartIndex, i);
                Transform lodTransform = _lodGroupTransforms[lodGroupIndex];
                if (lodTransform == null)
                {
                    _lodGroupSquaredDistances[i] = float.MaxValue;
                    continue;
                }

                RefreshLODAnchorIfChanged(lodGroupIndex, lodTransform);
                _lodGroupSquaredDistances[i] = ResolvePresentationDistanceSqr(
                    cameraPosition,
                    _lodGroupPresentationPositions[lodGroupIndex]);
            }
        }

        private void RefreshLODAnchorIfChanged(int lodGroupIndex, Transform lodTransform)
        {
            if (!lodTransform.hasChanged)
                return;

            _lodGroupPresentationPositions[lodGroupIndex] = lodTransform.position;
            lodTransform.hasChanged = false;
        }

        private void ApplyLODTransitions()
        {
            int count = math.min(_scheduledLODGroupBatchCount, _registeredLODGroups.Count);
            float crossfadeThresholdSqr = _crossfadeDistanceThreshold * _crossfadeDistanceThreshold;
            int transitionCount = 0;

            for (int i = 0; i < count; i++)
            {
                int lodGroupIndex = ResolveHotPathLODGroupIndex(_lodBatchStartIndex, i);
                LODGroup lodGroup = _registeredLODGroups[lodGroupIndex];
                if (lodGroup == null) continue;

                float sqrDist = _lodGroupSquaredDistances[i];

                // Apply crossfade mode for near objects
                if (sqrDist < crossfadeThresholdSqr)
                {
                    if (lodGroup.fadeMode != LODFadeMode.CrossFade)
                    {
                        lodGroup.fadeMode = LODFadeMode.CrossFade;
                        lodGroup.animateCrossFading = true;
                        transitionCount++;
                    }
                }
                else
                {
                    // Discrete switching for distant objects
                    if (lodGroup.fadeMode != LODFadeMode.None)
                    {
                        lodGroup.fadeMode = LODFadeMode.None;
                        lodGroup.animateCrossFading = false;
                        transitionCount++;
                    }
                }
            }

            _lastFrameTransitionCount = transitionCount;
            _lodHotPathCursor = count > 0
                ? ResolveHotPathLODGroupIndex(_lodBatchStartIndex, count)
                : 0;
            _scheduledLODGroupBatchCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  HOT-PATH DISTANCE CHEAT
        // ══════════════════════════════════════════════════════════

        private static float ResolveCameraDistanceSqr(
            in AbsoluteUniversePosition cameraAup,
            in AbsoluteUniversePosition objectAup)
        {
            float3 cameraRelative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in objectAup, in cameraAup);
            float runtimeDistanceSqr = math.lengthsq(cameraRelative);
            if (runtimeDistanceSqr <= AupDistanceThresholdSqr)
                return runtimeDistanceSqr;

            double distanceSqr = AbsoluteUniversePosition.DistanceSq(in cameraAup, in objectAup);
            return distanceSqr >= float.MaxValue ? float.MaxValue : (float)distanceSqr;
        }

        private static float ResolvePresentationDistanceSqr(Vector3 cameraPosition, Vector3 objectPosition)
        {
            float3 localDelta = new float3(
                objectPosition.x - cameraPosition.x,
                objectPosition.y - cameraPosition.y,
                objectPosition.z - cameraPosition.z);
            float distanceSq = math.lengthsq(localDelta);
            return math.isfinite(distanceSq) ? distanceSq : float.MaxValue;
        }

        private void PublishLODPerformanceWarningIfNeeded(float elapsedMilliseconds)
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (elapsedMilliseconds <= LODSolveBudgetWarningMs || currentFrame < _nextLODPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                LODSolveBudgetWarningHash,
                LODSystemContextHash,
                elapsedMilliseconds);
            _nextLODPerformanceWarningFrame = currentFrame + LODPerformanceWarningCooldownFrames;
        }

        private int ResolveHotPathLODGroupBatchCount()
        {
            int authoringCap = math.max(1, _maxLODGroupsPerFrame);
            return math.min(_registeredLODGroups.Count, math.min(authoringCap, MaxHotPathLODGroupsPerFrame));
        }

        private int ResolveHotPathLODGroupIndex(int startIndex, int offset)
        {
            int groupCount = _registeredLODGroups.Count;
            if (groupCount <= 0)
                return 0;

            int index = startIndex + offset;
            return index < groupCount ? index : index % groupCount;
        }

        private bool TryResolveMainCamera(float dt = 0f)
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
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerTransform.TryGetComponent(out _mainCamera);
            }

            if (_mainCamera == null)
            {
                return false;
            }

            _cameraTransform = _mainCamera.transform;
            _cameraResolveRetryTimer = 0f;
            return true;
        }

        private AbsoluteUniversePosition ResolveViewerAup()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_viewerAupCacheFrame == frame)
                return _viewerAupCache;

            _viewerAupCacheFrame = frame;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                snapshot.Aup.IsFinite())
            {
                _viewerAupCache = snapshot.Aup;
                return _viewerAupCache;
            }

            var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                _viewerAupCache = playerMovement.CurrentAup;
                return _viewerAupCache;
            }

            _viewerAupCache = default;
            return _viewerAupCache;
        }

        private void InvalidateViewerAupCache()
        {
            _viewerAupCacheFrame = -1;
            _viewerAupCache = default;
        }

        private void ApplyQualityPreset(LODQualityPreset preset)
        {
            _qualityPreset = preset;
            QualitySettings.lodBias = GetLODBias();
            DistanceMath.PushShaderMathLod(ResolvePresetQualityWeight01(preset));

            _dynamicResolutionScaler?.SetQualityPreset(preset);
        }

        private void RestoreDefaultLODBias()
        {
            QualitySettings.lodBias = _defaultLODBias;
        }

        private void TryRegisterImpostorCandidate(LODGroup lodGroup)
        {
            if (!ShouldUseImpostorCandidate(lodGroup))
                return;

            ImpostorSystem impostorSystem = _impostorSystem;
            if (impostorSystem == null)
                return;

            if (ShouldUseDistantGeologyImpostorCandidate(lodGroup))
                impostorSystem.RegisterDistantGeologyImpostorCandidate(lodGroup.gameObject, lodGroup);
            else
                impostorSystem.RegisterImpostorCandidate(lodGroup.gameObject, lodGroup);
        }

        private void TryUnregisterImpostorCandidate(LODGroup lodGroup)
        {
            if (lodGroup == null)
                return;

            ImpostorSystem impostorSystem = _impostorSystem;
            if (impostorSystem == null)
                return;

            impostorSystem.UnregisterImpostorCandidate(lodGroup.gameObject);
        }

        private void UnregisterAllImpostorCandidates()
        {
            ImpostorSystem impostorSystem = _impostorSystem;
            if (impostorSystem == null)
                return;

            for (int i = _registeredLODGroups.Count - 1; i >= 0; i--)
            {
                LODGroup lodGroup = _registeredLODGroups[i];
                if (lodGroup == null)
                    continue;

                impostorSystem.UnregisterImpostorCandidate(lodGroup.gameObject);
            }
        }

        private static bool ShouldUseImpostorCandidate(LODGroup lodGroup)
        {
            if (lodGroup == null || !lodGroup.enabled)
                return false;

            if (lodGroup.size < 1f)
                return false;

            return lodGroup.gameObject.activeInHierarchy;
        }

        private static bool ShouldUseDistantGeologyImpostorCandidate(LODGroup lodGroup)
        {
            if (lodGroup == null || lodGroup.size < 8f)
                return false;

            GameObject owner = lodGroup.gameObject;
            if (ContainsGeologyMarker(owner.name))
                return true;

            LOD[] lods = lodGroup.GetLODs();
            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers;
                if (renderers == null)
                    continue;

                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                        continue;

                    Material material = renderer.sharedMaterial;
                    if (material == null)
                        continue;

                    Shader shader = material.shader;
                    if ((shader != null && ContainsGeologyMarker(shader.name)) ||
                        ContainsGeologyMarker(material.name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsGeologyMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("AbyssalVoxelRock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Geology", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Mountain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Cliff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CleanupNullRegistrations()
        {
            int cleanupCount = math.min(_registeredLODGroups.Count, MaxHotPathLODGroupsPerFrame);

            for (int processed = 0; processed < cleanupCount && _registeredLODGroups.Count > 0; processed++)
            {
                if (_nullCleanupCursor >= _registeredLODGroups.Count)
                    _nullCleanupCursor = 0;

                int i = _nullCleanupCursor;
                if (_registeredLODGroups[i] != null && _lodGroupTransforms[i] != null)
                {
                    _nullCleanupCursor++;
                    continue;
                }

                _registeredLODGroupsSet.Remove(_registeredLODGroups[i]);

                int lastIndex = _registeredLODGroups.Count - 1;
                if (i != lastIndex)
                {
                    _registeredLODGroups[i] = _registeredLODGroups[lastIndex];
                    _lodGroupTransforms[i] = _lodGroupTransforms[lastIndex];
                    _lodGroupPresentationPositions[i] = _lodGroupPresentationPositions[lastIndex];
                }

                _registeredLODGroups.RemoveAt(lastIndex);
                _lodGroupTransforms.RemoveAt(lastIndex);
                _lodGroupPresentationPositions.RemoveAt(lastIndex);
                if (_nullCleanupCursor >= _registeredLODGroups.Count)
                    _nullCleanupCursor = 0;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR GIZMOS
        // ══════════════════════════════════════════════════════════

        #if UNITY_EDITOR

        [Header("── Gizmos ──────────────────")]
        [SerializeField, Tooltip("Enable LOD Gizmos visualization")]
        private bool _enableGizmos = false;

        [SerializeField, Tooltip("Show LOD transition distance spheres")]
        private bool _showTransitionSpheres = true;

        [SerializeField, Tooltip("Show current LOD level labels")]
        private bool _showLODLabels = true;

        [SerializeField, Tooltip("Show cull distance visualization")]
        private bool _showCullDistance = false;

        // Cached colors to avoid allocation
        private static readonly Color _lod0Color = new Color(0f, 1f, 0f, 0.3f);
        private static readonly Color _lod1Color = new Color(1f, 1f, 0f, 0.3f);
        private static readonly Color _lod2Color = new Color(1f, 0.5f, 0f, 0.3f);
        private static readonly Color _cullColor = new Color(1f, 0f, 0f, 0.3f);

        private void OnDrawGizmosSelected()
        {
            if (!_enableGizmos) return;
            if (!Application.isPlaying) return;
            if (_mainCamera == null) return;

            Vector3 camPos = _mainCamera.transform.position;

            // Draw transition distance spheres
            if (_showTransitionSpheres)
            {
                DrawTransitionSpheres(camPos);
            }

            // Draw LOD labels and cull distance
            for (int i = 0; i < _registeredLODGroups.Count; i++)
            {
                LODGroup lodGroup = _registeredLODGroups[i];
                if (lodGroup == null) continue;

                RefreshLODAnchorIfChanged(i, _lodGroupTransforms[i]);
                Vector3 objPos = _lodGroupPresentationPositions[i];
                float sqrDist = ResolvePresentationDistanceSqr(camPos, objPos);

                // Show current LOD level label
                if (_showLODLabels)
                {
                    DrawLODLabel(lodGroup, objPos, sqrDist);
                }

                // Show cull distance
                if (_showCullDistance)
                {
                    DrawCullDistance(lodGroup, objPos);
                }
            }
        }

        private void DrawTransitionSpheres(Vector3 camPos)
        {
            LOD[] lods = _registeredLODGroups.Count > 0 ? _registeredLODGroups[0].GetLODs() : null;
            if (lods == null || lods.Length == 0) return;

            float lodBias = GetLODBias();

            // Draw sphere for each LOD transition
            for (int i = 0; i < lods.Length; i++)
            {
                float screenRelativeHeight = lods[i].screenRelativeTransitionHeight;
                if (screenRelativeHeight <= 0f) continue;

                // Approximate distance from screen height
                float distance = 1f / screenRelativeHeight * lodBias * 10f;

                Color color = i == 0 ? _lod0Color : i == 1 ? _lod1Color : i == 2 ? _lod2Color : _cullColor;
                Gizmos.color = color;
                Gizmos.DrawWireSphere(camPos, distance);

                // Draw label
                UnityEditor.Handles.Label(
                    camPos + Vector3.up * distance,
                    "LOD" + i + " (" + distance.ToString("F1", CultureInfo.InvariantCulture) + "m)",
                    UnityEditor.EditorStyles.whiteBoldLabel
                );
            }
        }

        private static void DrawLODLabel(LODGroup lodGroup, Vector3 objPos, float distSqr)
        {
            // Get current LOD level
            LOD[] lods = lodGroup.GetLODs();
            int currentLOD = -1;

            for (int i = 0; i < lods.Length; i++)
            {
                float screenHeight = lods[i].screenRelativeTransitionHeight;
                if (screenHeight > 0f)
                {
                    currentLOD = i;
                    break;
                }
            }

                string label = currentLOD >= 0
                    ? "LOD" + currentLOD + " (" + distSqr.ToString("F0", CultureInfo.InvariantCulture) + "m2)"
                    : "Culled (" + distSqr.ToString("F0", CultureInfo.InvariantCulture) + "m2)";
            UnityEditor.Handles.Label(objPos + Vector3.up * 2f, label, UnityEditor.EditorStyles.whiteBoldLabel);
        }

        private static void DrawCullDistance(LODGroup lodGroup, Vector3 objPos)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length == 0) return;

            // Last LOD is cull distance
            float cullScreenHeight = lods[lods.Length - 1].screenRelativeTransitionHeight;
            if (cullScreenHeight <= 0f) return;

            float cullDistance = 1f / cullScreenHeight * 10f;

            Gizmos.color = _cullColor;
            Gizmos.DrawWireSphere(objPos, cullDistance);
        }

        #endif
    }
}

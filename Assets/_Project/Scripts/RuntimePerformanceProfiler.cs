// ============================================================================
// HECTON-8 - RuntimePerformanceProfiler.cs
// Runtime profiler bridge for frame-time, GC and memory budgets.
// Uses ProfilerRecorder and GameTickManager instead of native Update.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Hecton.UI.MainMenu;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Optimization;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
#endif

namespace Hecton8.Dev
{
    /// <summary>
    /// Captures runtime performance counters and reports budget violations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Runtime Performance Profiler")]
    public sealed class RuntimePerformanceProfiler : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int RecorderCapacity = 1;
        private const int RendererOwnershipAuditRootCapacity = 512;
        private const int RendererOwnershipAuditTransformCapacity = 16384;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const string AutoBootstrapObjectName = "__DEV_RuntimePerformanceProfiler";
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string DefaultGameplaySceneName = "02_HECTON_WORLD";
        private const string DriverTraceUpdateMessage = "source=Update";
        private const string DriverTraceTickMessage = "source=Tick";
        private const string HeartbeatTraceMessage = "runtime heartbeat";
        private const string RuntimeWindowTraceMessage = "runtime window";
        private const string RuntimeBudgetTraceMessage = "runtime budget exceeded";
        private const string RuntimeFrozenTraceMessage = "reason=fallback-frozen";
        private const string RuntimeFrozenSummaryTraceMessage = "reason=fallback-frozen-summary";
        private const string RuntimeFrozenThresholdTraceMessage = "reason=fallback-frozen-threshold";
        private const string RuntimeFrozenRecoveredTraceMessage = "reason=fallback-recovered";
        private const string RuntimeVramBudgetTraceMessage = "VRAM OVER BUDGET";
        private const string SceneSnapshotTraceMessage = "scene snapshot";
        private const float SceneTransitionBudgetWarningGraceSeconds = 8f;
        private static readonly double StopwatchTicksToSeconds = 1d / System.Diagnostics.Stopwatch.Frequency;
#if UNITY_EDITOR
        private const int MaxDirtyPlayRetryAttempts = 3;
        private const int FrozenFallbackStallWarningWindowThreshold = 5;
        private const string AutoBootstrapSessionKey = "Hecton8.RuntimeProfiler.AutoBootstrapArmed";
        private const string DirtyPlayRetryPendingKey = "Hecton8.RuntimeProfiler.DirtyPlayRetryPending";
        private const string DirtyPlayRetryCountKey = "Hecton8.RuntimeProfiler.DirtyPlayRetryCount";
        private const string DirtyPlayRetryReasonKey = "Hecton8.RuntimeProfiler.DirtyPlayRetryReason";
#endif
        private static readonly string[] _FrameTimeCandidates =
        {
            "CPU Total Frame Time",
            "Frame Time"
        };

        private static readonly string[] _MainThreadCandidates = { "Main Thread" };
        private static readonly string[] _GcAllocCandidates = { "GC Allocated In Frame" };
        private static readonly string[] _SystemMemoryCandidates = { "System Used Memory", "Total Used Memory" };
        private static readonly string[] _SetPassCandidates = { "SetPass Calls Count" };
        private static readonly string[] _BatchesCandidates = { "Batches Count" };
        private static readonly string[] _ScatterDispatchCandidates = { "WorldScatter.Rebuild.Dispatch" };
        private static readonly string[] _ScatterSamplingBeginCandidates = { "WorldScatter.Sampling.Begin" };
        private static readonly string[] _ScatterSamplingBuildInputsCandidates = { "WorldScatter.Sampling.BuildInputs" };
        private static readonly string[] _ScatterSamplingScheduleCandidates = { "WorldScatter.Sampling.Schedule" };
        private static readonly string[] _ScatterProcessingTotalCandidates = { "WorldScatter.Processing.Total" };
        private static readonly string[] _ScatterProcessingCellsCandidates = { "WorldScatter.Processing.Cells" };
        private static readonly string[] _ScatterProcessingRescueCandidates = { "WorldScatter.Processing.Rescue" };
        private static readonly string[] _ScatterProcessingRestoreCandidates = { "WorldScatter.Processing.Restore" };
        private static readonly string[] _ScatterReconcileTotalCandidates = { "WorldScatter.Reconcile.Total" };
        private static readonly string[] _ScatterReconcileCleanupCandidates = { "WorldScatter.Reconcile.Cleanup" };
        private static readonly string[] _ScatterReconcileSpawnCandidates = { "WorldScatter.Reconcile.Spawn" };
        private static readonly string[] _ScatterReconcileFaunaCandidates = { "WorldScatter.Reconcile.Fauna" };
        private static readonly string[] _ScatterBackendShadowScheduleCandidates = { "WorldScatter.Backend.Shadow.Schedule" };
        private static readonly string[] _ScatterBackendShadowPumpCandidates = { "WorldScatter.Backend.Shadow.Pump" };

        [Header("Execution")]
        [SerializeField] private bool startProfilingOnEnable = true;
        [SerializeField] private bool logBudgetViolations = true;
        [SerializeField] private bool logEveryWindow = false;
        [SerializeField] private float sampleWindowSeconds = 5f;
        [SerializeField, Tooltip("Cooldown between repeated budget warnings while the same runtime breach remains active.")]
        private float budgetViolationLogCooldownSeconds = 30f;

        [Header("File Trace")]
        [SerializeField] private bool writeTraceToFile = true;
        [SerializeField] private string traceSessionLabel = "runtime";
        [SerializeField] private bool traceRendererOwnershipOnSpike = true;
        [SerializeField] private bool traceRendererOwnershipOnGcSpike = false;
        [SerializeField] private float rendererOwnershipAuditCooldownSeconds = 20f;
        [SerializeField] private int rendererOwnershipAuditBatchesThreshold = 900;
        [SerializeField] private int rendererOwnershipAuditGcThresholdBytes = 4 * 1024 * 1024;

        [Header("Scene Snapshots")]
        [SerializeField] private bool logSceneSnapshots = true;
        [SerializeField] private float sceneSnapshotDelaySeconds = 1.5f;

        [Header("Menu Route")]
        [SerializeField] private bool autoStartNewGameFromMainMenu = false;
        [SerializeField] private float autoStartNewGameDelaySeconds = 2.5f;
        [SerializeField] private string autoStartMainMenuSceneName = "01_MAIN_MENU";
        [SerializeField] private bool profileGameplaySceneOnly = true;
        [SerializeField] private string gameplaySceneName = DefaultGameplaySceneName;

        [Header("Budgets")]
        [SerializeField] private float frameTimeBudgetMs = 16.67f;
        [SerializeField] private float mainThreadBudgetMs = 12f;
        [SerializeField] private int gcAllocBudgetBytes = 0;
        [SerializeField] private float systemMemoryBudgetMb = 4096f;
        [SerializeField] private int setPassBudget = 600;
        [SerializeField] private int batchesBudget = 1800;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugProfilingActive;
        [SerializeField] private int _debugWindowCount;
        [SerializeField] private bool _debugLastWindowExceededBudget;
        [SerializeField] private int _debugTickCount;
        [SerializeField] private int _debugFallbackUpdateCount;
        [SerializeField] private float _debugLastDeltaTime;
        [SerializeField] private bool _debugUsingFallbackUpdate;
        [SerializeField] private float _debugLastFrameTimeMs;
        [SerializeField] private float _debugLastMainThreadMs;
        [SerializeField] private int _debugLastGcAllocBytes;
        [SerializeField] private float _debugLastSystemMemoryMb;
        [SerializeField] private int _debugLastSetPassCalls;
        [SerializeField] private int _debugLastBatches;
        [SerializeField] private float _debugPeakFrameTimeMs;
        [SerializeField] private float _debugPeakMainThreadMs;
        [SerializeField] private int _debugPeakGcAllocBytes;
        [SerializeField] private float _debugPeakSystemMemoryMb;
        [SerializeField] private int _debugPeakSetPassCalls;
        [SerializeField] private int _debugPeakBatches;
        [SerializeField] private int _debugGeoBindings;
        [SerializeField] private int _debugGeoGeneratedRoots;
        [SerializeField] private int _debugGeoGeneratedRenderers;
        [SerializeField] private int _debugGeoVoxelVolumes;
        [SerializeField] private int _debugGeoVoxelColliders;
        [SerializeField] private string _debugFrameTimeStat = "Unresolved";
        [SerializeField] private string _debugMainThreadStat = "Unresolved";
        [SerializeField] private string _debugGcAllocStat = "Unresolved";
        [SerializeField] private string _debugSystemMemoryStat = "Unresolved";
        [SerializeField] private string _debugSetPassStat = "Unresolved";
        [SerializeField] private string _debugBatchesStat = "Unresolved";
        [SerializeField] private string _debugLastReport = "None";
        [SerializeField] private string _debugLastOwnershipAudit = "None";
        [SerializeField] private string _debugTraceFilePath = "None";
        [SerializeField] private string _debugCurrentScene = "Unknown";
        [SerializeField] private string _debugPendingSceneSnapshot = "None";
        [SerializeField] private string _debugLastSceneSnapshot = "None";
        [SerializeField] private string _debugLastScatterReport = "None";
        [SerializeField] private string _debugLastDriveSource = "None";
        [SerializeField] private string _debugPendingAutoStart = "None";
        [SerializeField] private float _debugLastScatterTotalMs;
        [SerializeField] private float _debugLastScatterSamplingMs;
        [SerializeField] private float _debugLastScatterRescueMs;
        [SerializeField] private float _debugLastScatterRestoreMs;
        [SerializeField] private float _debugLastScatterReconcileMs;
        [SerializeField] private float _debugLastScatterShadowScheduleMs;
        [SerializeField] private float _debugLastScatterShadowPumpMs;
        [SerializeField] private float _debugLastTextureMB;
        [SerializeField] private float _debugLastRenderTextureMB;
        [SerializeField] private float _debugLastTotalVRAMMB;
#pragma warning disable CS0414
        [SerializeField] private string _debugLastVRAMWarning = "None";
#pragma warning restore CS0414
        [SerializeField] private string _debugLastWindowCadence = "Unknown";
        [SerializeField] private int _debugLastWindowFrameDelta;
        [SerializeField] private bool _debugLastWindowUsedTickDrive;
        [SerializeField] private bool _debugLastWindowUsedFallbackDrive;

        private readonly List<ProfilerRecorderHandle> _availableHandles = new List<ProfilerRecorderHandle>(256);
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        private readonly StringBuilder _scatterBuilder = new StringBuilder(768);
        private readonly StringBuilder _auditBuilder = new StringBuilder(1024);
        private readonly Dictionary<string, int> _auditScatterFamilies = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int> _auditGeologyFamilies = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int> _auditVoxelFamilies = new Dictionary<string, int>(16);
        private readonly Dictionary<string, int> _auditSocketKinds = new Dictionary<string, int>(16);
        // COLD ALLOC: List<GameObject>[512] - non-alloc scene root scratch for renderer ownership audit - owner: RuntimePerformanceProfiler
        private readonly List<GameObject> _auditSceneRoots = new List<GameObject>(RendererOwnershipAuditRootCapacity);
        // COLD ALLOC: Transform[16384] - non-alloc transform traversal stack for renderer ownership audit - owner: RuntimePerformanceProfiler
        private readonly Transform[] _rendererAuditStack = new Transform[RendererOwnershipAuditTransformCapacity];

        private ProfilerRecorder _frameTimeRecorder;
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _scatterDispatchRecorder;
        private ProfilerRecorder _scatterSamplingBeginRecorder;
        private ProfilerRecorder _scatterSamplingBuildInputsRecorder;
        private ProfilerRecorder _scatterSamplingScheduleRecorder;
        private ProfilerRecorder _scatterProcessingTotalRecorder;
        private ProfilerRecorder _scatterProcessingCellsRecorder;
        private ProfilerRecorder _scatterProcessingRescueRecorder;
        private ProfilerRecorder _scatterProcessingRestoreRecorder;
        private ProfilerRecorder _scatterReconcileTotalRecorder;
        private ProfilerRecorder _scatterReconcileCleanupRecorder;
        private ProfilerRecorder _scatterReconcileSpawnRecorder;
        private ProfilerRecorder _scatterReconcileFaunaRecorder;
        private ProfilerRecorder _scatterBackendShadowScheduleRecorder;
        private ProfilerRecorder _scatterBackendShadowPumpRecorder;

        private IVramBudgetReadModel _cachedVramMonitor;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _runtimeOwnerAborted;
        private float _sampleElapsed;
        private float _peakFrameTimeMs;
        private float _peakMainThreadMs;
        private int _peakGcAllocBytes;
        private float _peakSystemMemoryMb;
        private int _peakSetPassCalls;
        private int _peakBatches;
        private float _nextOwnershipAuditAllowedTime;
        private float _nextBudgetViolationLogTime;
        private float _nextVramWarningLogTime;
        private float _peakScatterDispatchMs;
        private float _peakScatterSamplingBeginMs;
        private float _peakScatterSamplingBuildInputsMs;
        private float _peakScatterSamplingScheduleMs;
        private float _peakScatterProcessingTotalMs;
        private float _peakScatterProcessingCellsMs;
        private float _peakScatterProcessingRescueMs;
        private float _peakScatterProcessingRestoreMs;
        private float _peakScatterReconcileTotalMs;
        private float _peakScatterReconcileCleanupMs;
        private float _peakScatterReconcileSpawnMs;
        private float _peakScatterReconcileFaunaMs;
        private float _peakScatterBackendShadowScheduleMs;
        private float _peakScatterBackendShadowPumpMs;
        private bool _pendingSceneSnapshot;
        private float _pendingSceneSnapshotDelay;
        private float _pendingSceneSnapshotDueRealtime;
        private string _pendingSceneSnapshotSceneName = string.Empty;
        private string _pendingSceneSnapshotReason = string.Empty;
        private bool _suppressSamplingForCurrentScene;
        private bool _pendingAutoStartNewGame;
        private float _pendingAutoStartDelay;
        private float _pendingAutoStartDueRealtime;
        private bool _autoStartNewGameTriggered;
#pragma warning disable CS0414
        private bool _hasScatterSnapshot;
#pragma warning restore CS0414
        private ScatterRebuildProfileSnapshot _lastScatterSnapshot;
        private int _lastDrivenFrame = -1;
        private float _lastDriveRealtimeSinceStartup;
        private float _nextDriveHeartbeatTime;
        private double _lastEditorUpdateTime;
        private float _editorFallbackPumpThresholdSeconds;
        private float _nextEditorFallbackTraceTime;
        private float _budgetWarningSuppressedUntilRealtime;
        private bool _loggedFirstDrive;
        private int _invalidFrozenWindowCount;
        private int _suppressedFrozenFallbackSkipCount;
        private float _nextFrozenFallbackTraceTime;
        private bool _loggedPausedFrozenFallback;
        private int _sampleWindowStartFrame;
        private bool _sampleWindowUsedTickDrive;
        private bool _sampleWindowUsedFallbackDrive;
        private bool _rendererOwnershipAuditPending;

#if UNITY_EDITOR
        private static bool _editorHooksRegistered;
        private static bool _dirtyPlayRetryPending;
        private static int _dirtyPlayRetryCount;
        private static string _dirtyPlayLastReason = "None";
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool _developmentProfilerEnsureCompleted;
#endif
        private static RuntimePerformanceProfiler s_activeRuntime;

        internal static RuntimePerformanceProfiler ActiveRuntime =>
            IsRuntimePerformanceProfilerRuntimeUsable(s_activeRuntime)
                ? s_activeRuntime
                : ResolveUsableRuntime();

        /// <summary>
        /// Returns whether runtime profiling is currently sampling and recording trace windows.
        /// </summary>
        public bool IsProfilingActive => _debugProfilingActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
            GlobalRegistry.ClearRuntimePerformanceProfilerRuntime(null);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _developmentProfilerEnsureCompleted = false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDevelopmentRuntimeProfiler()
        {
#if UNITY_EDITOR
            if (!TryConsumeAutoBootstrapArm())
            {
                LogAutoBootstrapDecision("skip-unarmed");
                return;
            }
#endif
            EnsureDevelopmentRuntimeProfilerInstance();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Arms the development runtime profiler to auto-bootstrap on the next play mode entry.
        /// </summary>
        public static void ArmAutoBootstrapForNextPlayMode()
        {
            SessionState.SetBool(AutoBootstrapSessionKey, true);
            _developmentProfilerEnsureCompleted = false;
        }

        private static bool TryConsumeAutoBootstrapArm()
        {
            if (!SessionState.GetBool(AutoBootstrapSessionKey, false))
                return false;

            SessionState.EraseBool(AutoBootstrapSessionKey);
            return true;
        }
#endif

        private static void EnsureDevelopmentRuntimeProfilerInstance()
        {
            if (!Application.isPlaying)
                return;

            if (ShouldYieldToBootstrapProfilerOwner())
            {
                LogAutoBootstrapDecision("yield-bootstrap-owner");
                return;
            }

            if (_developmentProfilerEnsureCompleted)
            {
                LogAutoBootstrapDecision("yield-one-shot");
                return;
            }

            _developmentProfilerEnsureCompleted = true;

            if (ActiveRuntime != null)
            {
                LogAutoBootstrapDecision("yield-instance");
                return;
            }

            RuntimePerformanceProfiler existing = ActiveRuntime;
            if (existing != null)
            {
                LogAutoBootstrapDecision("yield-existing");
                return;
            }

            LogAutoBootstrapDecision("create-auto-bootstrap");
            GameObject profilerObject = new GameObject(AutoBootstrapObjectName);
            RuntimePerformanceProfiler profiler = profilerObject.AddComponent<RuntimePerformanceProfiler>();
            GameBootstrapper.PersistRuntimeService(profiler);
            profiler.ApplyAutoBootstrapDefaults();
        }

        private static bool ShouldYieldToBootstrapProfilerOwner()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                string.Equals(activeScene.name, BootstrapSceneName, StringComparison.Ordinal))
            {
                return true;
            }

            return GlobalRegistry.BootstrapperRuntime != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogAutoBootstrapDecision(string action)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.IsValid() ? activeScene.name : "InvalidScene";
            bool hasBootstrapInstance = GlobalRegistry.BootstrapperRuntime != null;
            bool hasProfilerInstance = ActiveRuntime != null;
            bool hasExistingProfiler = ActiveRuntime != null;
            int frame = SystemDispatcher.CurrentFrameIndex;

            string message =
                $"[RuntimeProfilerBootstrap] action={action} scene={sceneName} frame={frame} " +
                $"bootstrapInstance={hasBootstrapInstance} profilerInstance={hasProfilerInstance} " +
                $"existingProfiler={hasExistingProfiler} ensureCompleted={_developmentProfilerEnsureCompleted}";

            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.bootstrap",
                    $"action={action} scene={sceneName} frame={frame} " +
                    $"bootstrapInstance={hasBootstrapInstance} profilerInstance={hasProfilerInstance} " +
                    $"existingProfiler={hasExistingProfiler} ensureCompleted={_developmentProfilerEnsureCompleted}");
            }
        }
#endif

        internal static void RecordScatterRebuildProfile(in ScatterRebuildProfileSnapshot snapshot)
        {
            RuntimePerformanceProfiler instance = ActiveRuntime;
            if (instance == null)
                return;

            instance.ApplyScatterRebuildProfile(in snapshot);
        }

        private void Awake()
        {
            if (!EnsureRuntimeOwnership())
                return;

            ClampSettings();
            _debugCurrentScene = SceneManager.GetActiveScene().name;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"awake id={GetEntityId()} scene={_debugCurrentScene} name={gameObject.name}");
#endif
        }

        private void OnEnable()
        {
            ClampSettings();
            if (!Application.isPlaying)
                return;

            if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RegisterWithTickManager();
#if UNITY_EDITOR
            RegisterEditorDiagnosticsHooks();
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-enable id={GetEntityId()} scene={SceneManager.GetActiveScene().name} tick={_registeredTick} slow={_registeredSlowTick} late={_registeredLateFrame}");
#endif
            if (startProfilingOnEnable)
                StartProfiling();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())
                return;

            if (_registeredTick && _registeredSlowTick && _registeredLateFrame)
                return;

            RegisterWithTickManager();

            if (GlobalRegistry.Dispatcher != null &&
                (!_registeredTick || !_registeredSlowTick || !_registeredLateFrame) &&
                _debugProfilingActive)
            {
                Hecton8.Core.H8Debug.LogError("[RuntimeProfiler] GameTickManager registration failed.", this);
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!Application.isPlaying)
                return;

            if (!IsActiveRuntimeOwner())
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-disable id={GetEntityId()} scene={SceneManager.GetActiveScene().name} active={_debugProfilingActive}");
#endif
            StopProfiling();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            TryUnregisterHotSwapListener();
#if UNITY_EDITOR
            if (!_dirtyPlayRetryPending)
                UnregisterEditorDiagnosticsHooks();
#endif
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-destroy id={GetEntityId()} scene={SceneManager.GetActiveScene().name} active={_debugProfilingActive} instanceMatch={(GlobalRegistry.RuntimePerformanceProfilerRuntime == this)}");
#endif
            if (_runtimeOwnerAborted)
            {
                ClearRuntimeMirrorIfOwnedBy(this);
                return;
            }

            if (!IsActiveRuntimeOwner())
                return;

            StopProfiling();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            TryUnregisterHotSwapListener();
#if UNITY_EDITOR
            UnregisterEditorDiagnosticsHooks();
#endif
            UnregisterFromTickManager();
            GlobalRegistry.ClearRuntimePerformanceProfilerRuntime(this);
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private bool IsActiveRuntimeOwner()
        {
            return !_runtimeOwnerAborted &&
                   (ReferenceEquals(s_activeRuntime, this) ||
                    ReferenceEquals(GlobalRegistry.RuntimePerformanceProfilerRuntime, this));
        }

        private bool EnsureRuntimeOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            // Ask before aborting anyone. RuntimePerformanceProfiler has no GlobalRegistryServiceSlot, so
            // its slot resolves to Unknown, which is never scene-runtime hot-swappable. Once the registry
            // is ready-locked the registration below is guaranteed to throw, and both abort blocks between
            // here and there would already have retired the live profiler - leaving no owner at all.
            //
            // Only when a real takeover is needed: if this instance already owns the registry slot, the
            // registration early-returns on reference equality and never reaches the guard.
            if (!ReferenceEquals(GlobalRegistry.RuntimePerformanceProfilerRuntime, this) &&
                !GlobalRegistry.IsRuntimeServicePublicationOpen<RuntimePerformanceProfiler>())
            {
                _runtimeOwnerAborted = true;
                return false;
            }

            RuntimePerformanceProfiler runtime = s_activeRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterRuntimePerformanceProfilerRuntime(this);
            if (ReferenceEquals(GlobalRegistry.RuntimePerformanceProfilerRuntime, this))
                s_activeRuntime = this;

            bool ownsRuntime =
                ReferenceEquals(s_activeRuntime, this) &&
                ReferenceEquals(GlobalRegistry.RuntimePerformanceProfilerRuntime, this);
            _runtimeOwnerAborted = !ownsRuntime;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return ownsRuntime;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            RuntimePerformanceProfiler runtime = s_activeRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsRuntimePerformanceProfilerRuntimeUsable(runtime))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "runtime.lifecycle",
                        $"duplicate-awake destroy id={GetEntityId()} existing={runtime.GetEntityId()} name={gameObject.name}");
#endif
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsRuntimePerformanceProfilerRuntimeUsable(runtime))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "runtime.lifecycle",
                        $"duplicate-awake destroy id={GetEntityId()} existing={runtime.GetEntityId()} name={gameObject.name}");
#endif
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            return false;
        }

        private static RuntimePerformanceProfiler ResolveUsableRuntime()
        {
            RuntimePerformanceProfiler runtime = s_activeRuntime;
            if (IsRuntimePerformanceProfilerRuntimeUsable(runtime))
                return runtime;

            ClearRuntimeMirrorIfOwnedBy(runtime);

            runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;
            if (IsRuntimePerformanceProfilerRuntimeUsable(runtime))
            {
                s_activeRuntime = runtime;
                return runtime;
            }

            ClearRuntimeMirrorIfOwnedBy(runtime);
            return null;
        }

        private static bool IsRuntimePerformanceProfilerRuntimeUsable(RuntimePerformanceProfiler runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private static void ClearRuntimeMirrorIfOwnedBy(RuntimePerformanceProfiler runtime)
        {
            if (ReferenceEquals(runtime, null))
                return;

            GlobalRegistry.ClearRuntimePerformanceProfilerRuntime(runtime);
            if (ReferenceEquals(s_activeRuntime, runtime))
                s_activeRuntime = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ClampSettings();
        }
#endif

        [ContextMenu("Start Runtime Performance Profiling")]
        public void StartProfiling()
        {
            if (_runtimeOwnerAborted)
                return;

            StopProfiling();

            if (writeTraceToFile)
            {
                RuntimeDiagnosticsTrace.EnsureSession(traceSessionLabel);
                _debugTraceFilePath = RuntimeDiagnosticsTrace.CurrentFilePath;
                RuntimeDiagnosticsTrace.WriteEvent("session", "Runtime performance profiler session started.");
            }
            else
            {
                _debugTraceFilePath = "Disabled";
            }

            ResolveRecorders();
            _debugProfilingActive =
                _frameTimeRecorder.Valid ||
                _mainThreadRecorder.Valid ||
                _gcAllocRecorder.Valid ||
                _systemMemoryRecorder.Valid ||
                _setPassRecorder.Valid ||
                _batchesRecorder.Valid ||
                _scatterDispatchRecorder.Valid ||
                _scatterSamplingBeginRecorder.Valid ||
                _scatterSamplingBuildInputsRecorder.Valid ||
                _scatterSamplingScheduleRecorder.Valid ||
                _scatterProcessingTotalRecorder.Valid ||
                _scatterProcessingCellsRecorder.Valid ||
                _scatterProcessingRescueRecorder.Valid ||
                _scatterProcessingRestoreRecorder.Valid ||
                _scatterReconcileTotalRecorder.Valid ||
                _scatterReconcileCleanupRecorder.Valid ||
                _scatterReconcileSpawnRecorder.Valid ||
                _scatterReconcileFaunaRecorder.Valid ||
                _scatterBackendShadowScheduleRecorder.Valid ||
                _scatterBackendShadowPumpRecorder.Valid;

            ResetSampleWindow();
            _lastDriveRealtimeSinceStartup = ResolveProfilerMonotonicTimeSeconds();
            _lastEditorUpdateTime = 0d;
            _editorFallbackPumpThresholdSeconds = Mathf.Max(0.25f, sceneSnapshotDelaySeconds);
            _nextEditorFallbackTraceTime = 0f;
            _nextFrozenFallbackTraceTime = 0f;
            _loggedFirstDrive = false;
            _invalidFrozenWindowCount = 0;
            _suppressedFrozenFallbackSkipCount = 0;
            _loggedPausedFrozenFallback = false;
            _nextDriveHeartbeatTime = 0f;
            _budgetWarningSuppressedUntilRealtime =
                ResolveProfilerUnscaledTimeSeconds() + SceneTransitionBudgetWarningGraceSeconds;
            _pendingAutoStartNewGame = false;
            _pendingAutoStartDelay = 0f;
            _autoStartNewGameTriggered = false;
            _debugPendingAutoStart = "None";
            _suppressSamplingForCurrentScene = ShouldSuppressSceneSampling(SceneManager.GetActiveScene().name);
            QueueSceneSnapshot(SceneManager.GetActiveScene().name, "profiling-start");

            if (!_debugProfilingActive)
            {
                _debugLastReport = "No profiler stats resolved.";
                Hecton8.Core.H8Debug.LogWarning("[RuntimeProfiler] No profiler stats were resolved. Use context menu to inspect available counters.", this);
                RuntimeDiagnosticsTrace.WriteEvent("runtime", _debugLastReport);
            }
        }

        /// <summary>
        /// Applies development-friendly sampling settings for live profiling sessions.
        /// </summary>
        public void ConfigureForDevRun(
            bool autoStartOnEnable = true,
            bool enableBudgetViolationLogging = true,
            bool enableWindowLogging = true,
            bool autoStartNewGame = false,
            float sampleWindow = 2f)
        {
            startProfilingOnEnable = autoStartOnEnable;
            logBudgetViolations = enableBudgetViolationLogging;
            logEveryWindow = enableWindowLogging;
            autoStartNewGameFromMainMenu = autoStartNewGame;
            if (!autoStartNewGame)
            {
                _pendingAutoStartNewGame = false;
                _pendingAutoStartDueRealtime = 0f;
                _debugPendingAutoStart = "Disabled";
            }

            sampleWindowSeconds = sampleWindow;
            ClampSettings();
        }

        [ContextMenu("Stop Runtime Performance Profiling")]
        public void StopProfiling()
        {
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _mainThreadRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            DisposeRecorder(ref _systemMemoryRecorder);
            DisposeRecorder(ref _setPassRecorder);
            DisposeRecorder(ref _batchesRecorder);
            DisposeRecorder(ref _scatterDispatchRecorder);
            DisposeRecorder(ref _scatterSamplingBeginRecorder);
            DisposeRecorder(ref _scatterSamplingBuildInputsRecorder);
            DisposeRecorder(ref _scatterSamplingScheduleRecorder);
            DisposeRecorder(ref _scatterProcessingTotalRecorder);
            DisposeRecorder(ref _scatterProcessingCellsRecorder);
            DisposeRecorder(ref _scatterProcessingRescueRecorder);
            DisposeRecorder(ref _scatterProcessingRestoreRecorder);
            DisposeRecorder(ref _scatterReconcileTotalRecorder);
            DisposeRecorder(ref _scatterReconcileCleanupRecorder);
            DisposeRecorder(ref _scatterReconcileSpawnRecorder);
            DisposeRecorder(ref _scatterReconcileFaunaRecorder);
            DisposeRecorder(ref _scatterBackendShadowScheduleRecorder);
            DisposeRecorder(ref _scatterBackendShadowPumpRecorder);

            _debugProfilingActive = false;
            _debugLastOwnershipAudit = "None";
            _pendingSceneSnapshot = false;
            _pendingSceneSnapshotDueRealtime = 0f;
            _debugPendingSceneSnapshot = "None";
            _pendingAutoStartNewGame = false;
            _pendingAutoStartDelay = 0f;
            _pendingAutoStartDueRealtime = 0f;
            _debugPendingAutoStart = "None";
            _lastEditorUpdateTime = 0d;
            _editorFallbackPumpThresholdSeconds = 0f;
            _nextEditorFallbackTraceTime = 0f;
            _nextFrozenFallbackTraceTime = 0f;
            _invalidFrozenWindowCount = 0;
            _suppressedFrozenFallbackSkipCount = 0;
            _loggedPausedFrozenFallback = false;

            if (writeTraceToFile && RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent("session", "Runtime performance profiler session stopped.");
                RuntimeDiagnosticsTrace.CloseSession();
            }

            _debugTraceFilePath = RuntimeDiagnosticsTrace.CurrentFilePath;
        }

        [ContextMenu("Log Available Runtime Profiler Counters")]
        public void LogAvailableCounters()
        {
            ProfilerRecorderHandle.GetAvailable(_availableHandles);
            _reportBuilder.Clear();
            _reportBuilder.Append("[RuntimeProfiler] available counters=").Append(_availableHandles.Count);

            int appended = 0;
            for (int i = 0; i < _availableHandles.Count && appended < 32; i++)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(_availableHandles[i]);
                string name = description.Name;
                if (!ContainsAnyKeyword(name, "Frame", "Main", "GC", "Memory", "Batch", "SetPass"))
                    continue;

                _reportBuilder.Append(" | ")
                    .Append(description.Category.Name)
                    .Append(':')
                    .Append(name);
                appended++;
            }

            Hecton8.Core.H8Debug.Log(_reportBuilder.ToString(), this);
        }

        public void Tick(float deltaTime)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_debugProfilingActive)
                return;

            float sampleDeltaTime = deltaTime > 0f ? deltaTime : SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            DriveSampling(sampleDeltaTime, false);
        }

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_debugProfilingActive)
                return;

            UpdateWorldDiagnostics();
            UpdateVRAMDiagnostics();
            _debugPeakFrameTimeMs = _peakFrameTimeMs;
            _debugPeakMainThreadMs = _peakMainThreadMs;
            _debugPeakGcAllocBytes = _peakGcAllocBytes;
            _debugPeakSystemMemoryMb = _peakSystemMemoryMb;
            _debugPeakSetPassCalls = _peakSetPassCalls;
            _debugPeakBatches = _peakBatches;
        }

        public string DescribeStatus()
        {
            return
                $"active={_debugProfilingActive} scene={_debugCurrentScene} windows={_debugWindowCount} " +
                $"frame={_debugLastFrameTimeMs:0.00}ms main={_debugLastMainThreadMs:0.00}ms " +
                $"gc={_debugLastGcAllocBytes}B mem={_debugLastSystemMemoryMb:0.0}MB " +
                $"setPass={_debugLastSetPassCalls} batches={_debugLastBatches} " +
                $"scatter={_debugLastScatterTotalMs:0.00}ms shadowSchedule={_debugLastScatterShadowScheduleMs:0.00}ms shadowPump={_debugLastScatterShadowPumpMs:0.00}ms " +
                $"ticks={_debugTickCount} fallback={_debugFallbackUpdateCount} " +
                $"fallbackActive={_debugUsingFallbackUpdate} dt={_debugLastDeltaTime:0.000} " +
                $"cadence={_debugLastWindowCadence} frameDelta={_debugLastWindowFrameDelta}";
        }

        [ContextMenu("Log Runtime Performance Profiling Status")]
        public void LogStatusToConsole()
        {
            Hecton8.Core.H8Debug.Log("[RuntimeProfiler] " + DescribeStatus(), this);
        }

        private void PumpPendingRuntimeRoutes(float deltaTime)
        {
            UpdatePendingSceneSnapshot(deltaTime);
            UpdatePendingAutoStart(deltaTime);
        }

        private void RegisterWithTickManager()
        {
            if (_runtimeOwnerAborted || !IsActiveRuntimeOwner() || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            }

            if (!_registeredLateFrame)
            {
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            }
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            float sampleDeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            PumpPendingRuntimeRoutes(sampleDeltaTime);
            FlushPendingRendererOwnershipAudit();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted || !IsActiveRuntimeOwner())
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService != null)
                    RegisterWithTickManager();
                else
                    UnregisterFromTickManager();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.VRAMMonitorRuntime)
                return;

            _cachedVramMonitor = currentService as IVramBudgetReadModel;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedVramMonitor = GlobalRegistry.VRAMBudgetReadModel;
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

        private void UnregisterFromTickManager()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = false;
            }
        }

        private void ResolveRecorders()
        {
            _frameTimeRecorder = StartRecorder(_FrameTimeCandidates, ref _debugFrameTimeStat);
            _mainThreadRecorder = StartRecorder(_MainThreadCandidates, ref _debugMainThreadStat);
            _gcAllocRecorder = StartRecorder(_GcAllocCandidates, ref _debugGcAllocStat);
            _systemMemoryRecorder = StartRecorder(_SystemMemoryCandidates, ref _debugSystemMemoryStat);
            _setPassRecorder = StartRecorder(_SetPassCandidates, ref _debugSetPassStat);
            _batchesRecorder = StartRecorder(_BatchesCandidates, ref _debugBatchesStat);
            _scatterDispatchRecorder = StartRecorder(_ScatterDispatchCandidates);
            _scatterSamplingBeginRecorder = StartRecorder(_ScatterSamplingBeginCandidates);
            _scatterSamplingBuildInputsRecorder = StartRecorder(_ScatterSamplingBuildInputsCandidates);
            _scatterSamplingScheduleRecorder = StartRecorder(_ScatterSamplingScheduleCandidates);
            _scatterProcessingTotalRecorder = StartRecorder(_ScatterProcessingTotalCandidates);
            _scatterProcessingCellsRecorder = StartRecorder(_ScatterProcessingCellsCandidates);
            _scatterProcessingRescueRecorder = StartRecorder(_ScatterProcessingRescueCandidates);
            _scatterProcessingRestoreRecorder = StartRecorder(_ScatterProcessingRestoreCandidates);
            _scatterReconcileTotalRecorder = StartRecorder(_ScatterReconcileTotalCandidates);
            _scatterReconcileCleanupRecorder = StartRecorder(_ScatterReconcileCleanupCandidates);
            _scatterReconcileSpawnRecorder = StartRecorder(_ScatterReconcileSpawnCandidates);
            _scatterReconcileFaunaRecorder = StartRecorder(_ScatterReconcileFaunaCandidates);
            _scatterBackendShadowScheduleRecorder = StartRecorder(_ScatterBackendShadowScheduleCandidates);
            _scatterBackendShadowPumpRecorder = StartRecorder(_ScatterBackendShadowPumpCandidates);
        }

        private ProfilerRecorder StartRecorder(string[] candidates, ref string debugStatName)
        {
            debugStatName = "Unresolved";
            ProfilerRecorderHandle.GetAvailable(_availableHandles);

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                for (int handleIndex = 0; handleIndex < _availableHandles.Count; handleIndex++)
                {
                    ProfilerRecorderDescription description =
                        ProfilerRecorderHandle.GetDescription(_availableHandles[handleIndex]);
                    string descriptionName = description.Name;
                    if (!MatchesCandidate(descriptionName, candidate))
                        continue;

                    try
                    {
                        ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                            description.Category,
                            descriptionName,
                            RecorderCapacity,
                            ProfilerRecorderOptions.Default);
                        if (recorder.Valid)
                        {
                            debugStatName = $"{description.Category.Name}:{descriptionName}";
                            return recorder;
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return default;
        }

        private ProfilerRecorder StartRecorder(string[] candidates)
        {
            string ignored = "Unresolved";
            return StartRecorder(candidates, ref ignored);
        }

        private void SampleRecorders()
        {
            _debugLastFrameTimeMs = ReadMilliseconds(_frameTimeRecorder);
            _debugLastMainThreadMs = ReadMilliseconds(_mainThreadRecorder);
            _debugLastGcAllocBytes = ReadIntValue(_gcAllocRecorder);
            _debugLastSystemMemoryMb = ReadMegabytes(_systemMemoryRecorder);
            _debugLastSetPassCalls = ReadIntValue(_setPassRecorder);
            _debugLastBatches = ReadIntValue(_batchesRecorder);
            float scatterDispatchMs = ReadMilliseconds(_scatterDispatchRecorder);
            float scatterSamplingBeginMs = ReadMilliseconds(_scatterSamplingBeginRecorder);
            float scatterSamplingBuildInputsMs = ReadMilliseconds(_scatterSamplingBuildInputsRecorder);
            float scatterSamplingScheduleMs = ReadMilliseconds(_scatterSamplingScheduleRecorder);
            float scatterProcessingTotalMs = ReadMilliseconds(_scatterProcessingTotalRecorder);
            float scatterProcessingCellsMs = ReadMilliseconds(_scatterProcessingCellsRecorder);
            float scatterProcessingRescueMs = ReadMilliseconds(_scatterProcessingRescueRecorder);
            float scatterProcessingRestoreMs = ReadMilliseconds(_scatterProcessingRestoreRecorder);
            float scatterReconcileTotalMs = ReadMilliseconds(_scatterReconcileTotalRecorder);
            float scatterReconcileCleanupMs = ReadMilliseconds(_scatterReconcileCleanupRecorder);
            float scatterReconcileSpawnMs = ReadMilliseconds(_scatterReconcileSpawnRecorder);
            float scatterReconcileFaunaMs = ReadMilliseconds(_scatterReconcileFaunaRecorder);
            float scatterBackendShadowScheduleMs = ReadMilliseconds(_scatterBackendShadowScheduleRecorder);
            float scatterBackendShadowPumpMs = ReadMilliseconds(_scatterBackendShadowPumpRecorder);

            _debugLastScatterShadowScheduleMs = scatterBackendShadowScheduleMs;
            _debugLastScatterShadowPumpMs = scatterBackendShadowPumpMs;

            _peakFrameTimeMs = Mathf.Max(_peakFrameTimeMs, _debugLastFrameTimeMs);
            _peakMainThreadMs = Mathf.Max(_peakMainThreadMs, _debugLastMainThreadMs);
            _peakGcAllocBytes = Mathf.Max(_peakGcAllocBytes, _debugLastGcAllocBytes);
            _peakSystemMemoryMb = Mathf.Max(_peakSystemMemoryMb, _debugLastSystemMemoryMb);
            _peakSetPassCalls = Mathf.Max(_peakSetPassCalls, _debugLastSetPassCalls);
            _peakBatches = Mathf.Max(_peakBatches, _debugLastBatches);
            _peakScatterDispatchMs = Mathf.Max(_peakScatterDispatchMs, scatterDispatchMs);
            _peakScatterSamplingBeginMs = Mathf.Max(_peakScatterSamplingBeginMs, scatterSamplingBeginMs);
            _peakScatterSamplingBuildInputsMs = Mathf.Max(_peakScatterSamplingBuildInputsMs, scatterSamplingBuildInputsMs);
            _peakScatterSamplingScheduleMs = Mathf.Max(_peakScatterSamplingScheduleMs, scatterSamplingScheduleMs);
            _peakScatterProcessingTotalMs = Mathf.Max(_peakScatterProcessingTotalMs, scatterProcessingTotalMs);
            _peakScatterProcessingCellsMs = Mathf.Max(_peakScatterProcessingCellsMs, scatterProcessingCellsMs);
            _peakScatterProcessingRescueMs = Mathf.Max(_peakScatterProcessingRescueMs, scatterProcessingRescueMs);
            _peakScatterProcessingRestoreMs = Mathf.Max(_peakScatterProcessingRestoreMs, scatterProcessingRestoreMs);
            _peakScatterReconcileTotalMs = Mathf.Max(_peakScatterReconcileTotalMs, scatterReconcileTotalMs);
            _peakScatterReconcileCleanupMs = Mathf.Max(_peakScatterReconcileCleanupMs, scatterReconcileCleanupMs);
            _peakScatterReconcileSpawnMs = Mathf.Max(_peakScatterReconcileSpawnMs, scatterReconcileSpawnMs);
            _peakScatterReconcileFaunaMs = Mathf.Max(_peakScatterReconcileFaunaMs, scatterReconcileFaunaMs);
            _peakScatterBackendShadowScheduleMs = Mathf.Max(_peakScatterBackendShadowScheduleMs, scatterBackendShadowScheduleMs);
            _peakScatterBackendShadowPumpMs = Mathf.Max(_peakScatterBackendShadowPumpMs, scatterBackendShadowPumpMs);
        }

        private void DriveSampling(float deltaTime, bool usingFallbackUpdate)
        {
            float realtimeNow = ResolveProfilerMonotonicTimeSeconds();
            float realtimeDeltaTime = 0f;
            if (_lastDriveRealtimeSinceStartup > 0f)
                realtimeDeltaTime = Mathf.Max(0f, realtimeNow - _lastDriveRealtimeSinceStartup);

            float effectiveDeltaTime = Mathf.Max(deltaTime, realtimeDeltaTime);

            _lastDriveRealtimeSinceStartup = realtimeNow;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            _lastDrivenFrame = currentFrame;
            _debugTickCount++;
            _debugLastDeltaTime = effectiveDeltaTime;
            _debugUsingFallbackUpdate = usingFallbackUpdate;
            _debugLastDriveSource = usingFallbackUpdate ? "Update" : "Tick";
            if (usingFallbackUpdate)
            {
                _debugFallbackUpdateCount++;
                _sampleWindowUsedFallbackDrive = true;
            }
            else
            {
                _sampleWindowUsedTickDrive = true;
            }

            if (!_loggedFirstDrive && RuntimeDiagnosticsTrace.IsActive)
            {
                _loggedFirstDrive = true;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.driver",
                    usingFallbackUpdate ? DriverTraceUpdateMessage : DriverTraceTickMessage);
            }

            if (RuntimeDiagnosticsTrace.IsActive && realtimeNow >= _nextDriveHeartbeatTime)
            {
                _nextDriveHeartbeatTime = realtimeNow + 2f;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.heartbeat",
                    HeartbeatTraceMessage);
            }

            if (_suppressSamplingForCurrentScene)
                return;

            _sampleElapsed += effectiveDeltaTime;
            SampleRecorders();

            if (_sampleElapsed >= sampleWindowSeconds)
                FlushSampleWindow();
        }

        private void FlushSampleWindow()
        {
            int recoveredFrozenWindowCount = _invalidFrozenWindowCount;
            int recoveredSuppressedFrozenSkipCount = _suppressedFrozenFallbackSkipCount;

            _debugWindowCount++;
            _debugLastWindowExceededBudget =
                _peakFrameTimeMs > frameTimeBudgetMs ||
                _peakMainThreadMs > mainThreadBudgetMs ||
                _peakGcAllocBytes > gcAllocBudgetBytes ||
                _peakSystemMemoryMb > systemMemoryBudgetMb ||
                _peakSetPassCalls > setPassBudget ||
                _peakBatches > batchesBudget;
            _debugLastWindowFrameDelta = Mathf.Max(0, SystemDispatcher.CurrentFrameIndex - _sampleWindowStartFrame);
            _debugLastWindowUsedTickDrive = _sampleWindowUsedTickDrive;
            _debugLastWindowUsedFallbackDrive = _sampleWindowUsedFallbackDrive;
            _debugLastWindowCadence = DescribeWindowCadence(
                _debugLastWindowFrameDelta,
                _sampleWindowUsedTickDrive,
                _sampleWindowUsedFallbackDrive);

            if (ShouldSuppressFrozenFallbackWindow())
            {
                _invalidFrozenWindowCount++;
                LogFrozenFallbackSkip();

#if UNITY_EDITOR
                if (_invalidFrozenWindowCount == FrozenFallbackStallWarningWindowThreshold && !EditorApplication.isPaused)
                {
                    FlushSuppressedFrozenFallbackSkips();
                    RuntimeDiagnosticsTrace.WriteEvent("runtime.stall", RuntimeFrozenThresholdTraceMessage);
                }
#endif

                ResetSampleWindow();
                return;
            }

            FlushSuppressedFrozenFallbackSkips();
            if (recoveredFrozenWindowCount > 0)
            {
                RuntimeDiagnosticsTrace.WriteEvent("runtime.resume", RuntimeFrozenRecoveredTraceMessage);
            }

            _invalidFrozenWindowCount = 0;
            _loggedPausedFrozenFallback = false;

            _debugLastReport = _debugLastWindowExceededBudget ? RuntimeBudgetTraceMessage : RuntimeWindowTraceMessage;
            RuntimeDiagnosticsTrace.WriteEvent("runtime", _debugLastReport);

            bool shouldLogWindow = logEveryWindow;
            bool suppressBudgetWarningForSceneTransition =
                _debugLastWindowExceededBudget &&
                ResolveProfilerUnscaledTimeSeconds() < _budgetWarningSuppressedUntilRealtime;
#if UNITY_EDITOR
            bool suppressBudgetWarningForEditorFallback =
                _debugLastWindowExceededBudget &&
                _sampleWindowUsedFallbackDrive &&
                !_sampleWindowUsedTickDrive;
#else
            const bool suppressBudgetWarningForEditorFallback = false;
#endif

            if (!shouldLogWindow &&
                _debugLastWindowExceededBudget &&
                logBudgetViolations &&
                !suppressBudgetWarningForSceneTransition &&
                !suppressBudgetWarningForEditorFallback)
            {
                shouldLogWindow = TryConsumeBudgetWarningCooldown(ref _nextBudgetViolationLogTime);
            }

            if (shouldLogWindow)
            {
                if (_debugLastWindowExceededBudget && !suppressBudgetWarningForEditorFallback)
                    Hecton8.Core.H8Debug.LogWarning(_debugLastReport, this);
                else
                    Hecton8.Core.H8Debug.Log(_debugLastReport, this);
            }
            else if (!_debugLastWindowExceededBudget)
            {
                _nextBudgetViolationLogTime = 0f;
            }

            if (traceRendererOwnershipOnSpike && RuntimeDiagnosticsTrace.IsActive && ShouldCaptureRendererOwnershipAudit())
                _rendererOwnershipAuditPending = true;

            ResetSampleWindow();
        }

        private void FlushPendingRendererOwnershipAudit()
        {
            if (!_rendererOwnershipAuditPending)
                return;

            _rendererOwnershipAuditPending = false;
            CaptureRendererOwnershipAudit();
        }

        private void ResetSampleWindow()
        {
            _sampleElapsed = 0f;
            _sampleWindowStartFrame = SystemDispatcher.CurrentFrameIndex;
            _sampleWindowUsedTickDrive = false;
            _sampleWindowUsedFallbackDrive = false;
            _debugTickCount = 0;
            _debugFallbackUpdateCount = 0;
            _debugLastDeltaTime = 0f;
            _debugUsingFallbackUpdate = false;
            _peakFrameTimeMs = 0f;
            _peakMainThreadMs = 0f;
            _peakGcAllocBytes = 0;
            _peakSystemMemoryMb = 0f;
            _peakSetPassCalls = 0;
            _peakBatches = 0;
            _peakScatterDispatchMs = 0f;
            _peakScatterSamplingBeginMs = 0f;
            _peakScatterSamplingBuildInputsMs = 0f;
            _peakScatterSamplingScheduleMs = 0f;
            _peakScatterProcessingTotalMs = 0f;
            _peakScatterProcessingCellsMs = 0f;
            _peakScatterProcessingRescueMs = 0f;
            _peakScatterProcessingRestoreMs = 0f;
            _peakScatterReconcileTotalMs = 0f;
            _peakScatterReconcileCleanupMs = 0f;
            _peakScatterReconcileSpawnMs = 0f;
            _peakScatterReconcileFaunaMs = 0f;
            _peakScatterBackendShadowScheduleMs = 0f;
            _peakScatterBackendShadowPumpMs = 0f;
        }

        private void ResetSceneTransitionDiagnostics()
        {
            _debugLastFrameTimeMs = 0f;
            _debugLastMainThreadMs = 0f;
            _debugLastGcAllocBytes = 0;
            _debugLastSystemMemoryMb = 0f;
            _debugLastSetPassCalls = 0;
            _debugLastBatches = 0;
            _debugLastScatterTotalMs = 0f;
            _debugLastScatterSamplingMs = 0f;
            _debugLastScatterRescueMs = 0f;
            _debugLastScatterRestoreMs = 0f;
            _debugLastScatterReconcileMs = 0f;
            _debugLastScatterShadowScheduleMs = 0f;
            _debugLastScatterShadowPumpMs = 0f;
            _debugLastWindowCadence = "SceneTransition";
            _debugLastWindowFrameDelta = 0;
            _debugLastWindowUsedTickDrive = false;
            _debugLastWindowUsedFallbackDrive = false;
            _debugLastWindowExceededBudget = false;
        }

        private void LogFrozenFallbackSkip()
        {
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            bool isPaused = false;
#if UNITY_EDITOR
            isPaused = EditorApplication.isPaused;
#endif
            if (isPaused)
            {
                if (_loggedPausedFrozenFallback)
                {
                    _suppressedFrozenFallbackSkipCount++;
                    return;
                }

                _loggedPausedFrozenFallback = true;
            }

            float realtimeNow = ResolveProfilerMonotonicTimeSeconds();
            bool shouldEmit =
                _invalidFrozenWindowCount == 1 ||
                isPaused ||
                realtimeNow >= _nextFrozenFallbackTraceTime;

            if (!shouldEmit)
            {
                _suppressedFrozenFallbackSkipCount++;
                return;
            }

            _nextFrozenFallbackTraceTime = realtimeNow + 10f;

            RuntimeDiagnosticsTrace.WriteEvent("runtime.skip", RuntimeFrozenTraceMessage);
            _suppressedFrozenFallbackSkipCount = 0;
        }

        private void FlushSuppressedFrozenFallbackSkips()
        {
            if (_suppressedFrozenFallbackSkipCount <= 0 || !RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent("runtime.skip", RuntimeFrozenSummaryTraceMessage);
            _suppressedFrozenFallbackSkipCount = 0;
        }

        private static string DescribeWindowCadence(int frameDelta, bool usedTickDrive, bool usedFallbackDrive)
        {
            if (frameDelta <= 0)
            {
                if (usedFallbackDrive && usedTickDrive)
                    return "mixed-frozen";

                if (usedFallbackDrive)
                    return "fallback-frozen";

                if (usedTickDrive)
                    return "tick-frozen";

                return "frozen";
            }

            if (usedTickDrive && usedFallbackDrive)
                return "mixed";

            if (usedTickDrive)
                return "tick";

            if (usedFallbackDrive)
                return "fallback";

            return "unknown";
        }

        private bool ShouldSuppressFrozenFallbackWindow()
        {
            return _debugLastWindowFrameDelta == 0 &&
                   _sampleWindowUsedFallbackDrive &&
                   !_sampleWindowUsedTickDrive;
        }

        private void UpdateWorldDiagnostics()
        {
            _debugGeoBindings = WorldGenerativeGeologyBinding.ActiveBindingCount;
            _debugGeoGeneratedRoots = WorldGenerativeGeologyService.ActiveGeneratedRootCount;
            _debugGeoGeneratedRenderers = WorldGenerativeGeologyService.ActiveGeneratedRendererCount;
            _debugGeoVoxelVolumes = WorldGenerativeGeologyVoxelRuntime.ActiveRuntimeCount;
            _debugGeoVoxelColliders = WorldGenerativeGeologyVoxelRuntime.ActiveColliderCount;
        }

        /// <summary>
        /// Updates VRAM diagnostics from VRAMMonitor.
        /// Called from SlowTick() (~0.5s interval).
        /// </summary>
        private void UpdateVRAMDiagnostics()
        {
            IVramBudgetReadModel monitor = _cachedVramMonitor;
            if (monitor == null)
            {
                _debugLastTextureMB = 0f;
                _debugLastRenderTextureMB = 0f;
                _debugLastTotalVRAMMB = 0f;
                _debugLastVRAMWarning = "VRAMMonitor not available";
                return;
            }

            // Query VRAM breakdown (zero-GC)
            long textureBytes = monitor.TextureMemoryBytes;
            long renderTextureBytes = monitor.RenderTextureMemoryBytes;
            long totalBytes = monitor.TotalVRAMBytes;

            _debugLastTextureMB = textureBytes / BytesPerMegabyte;
            _debugLastRenderTextureMB = renderTextureBytes / BytesPerMegabyte;
            _debugLastTotalVRAMMB = totalBytes / BytesPerMegabyte;

            // Check thresholds
            bool textureOverBudget = monitor.IsTextureMemoryOverBudget;
            bool rtOverBudget = monitor.IsRenderTextureMemoryOverBudget;
            bool totalOverBudget = monitor.IsTotalVRAMOverBudget;

            if (textureOverBudget || rtOverBudget || totalOverBudget)
            {
                _debugLastVRAMWarning = RuntimeVramBudgetTraceMessage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logBudgetViolations)
                {
                    if (TryConsumeBudgetWarningCooldown(ref _nextVramWarningLogTime))
                        Hecton8.Core.H8Debug.LogWarning(RuntimeVramBudgetTraceMessage);
                }
#endif
            }
            else
            {
                _debugLastVRAMWarning = "None";
                _nextVramWarningLogTime = 0f;
            }
        }

        private void ClampSettings()
        {
            sampleWindowSeconds = Mathf.Clamp(sampleWindowSeconds, 0.5f, 60f);
            budgetViolationLogCooldownSeconds = Mathf.Clamp(budgetViolationLogCooldownSeconds, 0f, 300f);
            frameTimeBudgetMs = Mathf.Clamp(frameTimeBudgetMs, 1f, 100f);
            mainThreadBudgetMs = Mathf.Clamp(mainThreadBudgetMs, 1f, 100f);
            gcAllocBudgetBytes = Mathf.Clamp(gcAllocBudgetBytes, 0, 4 * 1024 * 1024);
            systemMemoryBudgetMb = Mathf.Clamp(systemMemoryBudgetMb, 128f, 16384f);
            setPassBudget = Mathf.Clamp(setPassBudget, 1, 10000);
            batchesBudget = Mathf.Clamp(batchesBudget, 1, 20000);
            rendererOwnershipAuditCooldownSeconds = Mathf.Clamp(rendererOwnershipAuditCooldownSeconds, 0f, 300f);
            rendererOwnershipAuditBatchesThreshold = Mathf.Clamp(rendererOwnershipAuditBatchesThreshold, 1, 20000);
            rendererOwnershipAuditGcThresholdBytes = Mathf.Clamp(rendererOwnershipAuditGcThresholdBytes, 0, 32 * 1024 * 1024);
            sceneSnapshotDelaySeconds = Mathf.Clamp(sceneSnapshotDelaySeconds, 0f, 30f);
            autoStartNewGameDelaySeconds = Mathf.Clamp(autoStartNewGameDelaySeconds, 0f, 30f);
        }

        private static float ResolveProfilerUnscaledTimeSeconds()
        {
            if (!Application.isPlaying)
                return 0f;

            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now <= 0d)
                return 0f;

            return now >= float.MaxValue ? float.MaxValue : (float)now;
        }

        private static float ResolveProfilerMonotonicTimeSeconds()
        {
            if (!Application.isPlaying)
                return 0f;

            double now = System.Diagnostics.Stopwatch.GetTimestamp() * StopwatchTicksToSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now <= 0d)
                return 0f;

            return now >= float.MaxValue ? float.MaxValue : (float)now;
        }

        private void ApplyAutoBootstrapDefaults()
        {
            startProfilingOnEnable = true;
            writeTraceToFile = true;
            logSceneSnapshots = true;
            logBudgetViolations = true;
            logEveryWindow = false;
            sampleWindowSeconds = 2f;
            budgetViolationLogCooldownSeconds = 30f;
            traceSessionLabel = "scatter_baseline";
            autoStartNewGameFromMainMenu = false;
            ClampSettings();
        }

        private bool TryConsumeBudgetWarningCooldown(ref float nextAllowedTime)
        {
            if (budgetViolationLogCooldownSeconds <= 0f)
                return true;

            float now = ResolveProfilerUnscaledTimeSeconds();
            if (now < nextAllowedTime)
                return false;

            nextAllowedTime = now + budgetViolationLogCooldownSeconds;
            return true;
        }

        private bool ShouldSuppressSceneSampling(string sceneName)
        {
            if (!profileGameplaySceneOnly)
                return false;

            string resolvedGameplaySceneName = string.IsNullOrWhiteSpace(gameplaySceneName)
                ? DefaultGameplaySceneName
                : gameplaySceneName.Trim();
            return !string.Equals(sceneName, resolvedGameplaySceneName, StringComparison.Ordinal);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _debugCurrentScene = scene.name;
            _suppressSamplingForCurrentScene = ShouldSuppressSceneSampling(scene.name);
            FlushSuppressedFrozenFallbackSkips();
            _invalidFrozenWindowCount = 0;
            _loggedPausedFrozenFallback = false;
            _nextFrozenFallbackTraceTime = 0f;
            _budgetWarningSuppressedUntilRealtime =
                ResolveProfilerUnscaledTimeSeconds() + SceneTransitionBudgetWarningGraceSeconds;
            ResetSampleWindow();
            ResetSceneTransitionDiagnostics();
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "scene",
                    $"loaded name={scene.name} mode={(mode == LoadSceneMode.Single ? "Single" : "Additive")}");
            }

            SampleRecorders();
            CaptureSceneSnapshot(scene.name, "scene-loaded-immediate");
            QueueSceneSnapshot(scene.name, "scene-loaded");
            QueueAutoStartNewGame(scene.name);
            TryAutoStartNewGameFromMenu();
        }

        private void QueueSceneSnapshot(string sceneName, string reason)
        {
            if (!logSceneSnapshots)
                return;

            _pendingSceneSnapshot = true;
            _pendingSceneSnapshotDelay = sceneSnapshotDelaySeconds;
            _pendingSceneSnapshotDueRealtime = ResolveProfilerMonotonicTimeSeconds() + _pendingSceneSnapshotDelay;
            _pendingSceneSnapshotSceneName = string.IsNullOrWhiteSpace(sceneName) ? "Unknown" : sceneName;
            _pendingSceneSnapshotReason = string.IsNullOrWhiteSpace(reason) ? "runtime" : reason;
            _debugPendingSceneSnapshot = $"{_pendingSceneSnapshotSceneName}@{_pendingSceneSnapshotDelay:0.0}s";
        }

        private void UpdatePendingSceneSnapshot(float deltaTime)
        {
            if (!_pendingSceneSnapshot)
                return;

            if (TryCapturePendingSceneSnapshotByRealtime(ResolveProfilerMonotonicTimeSeconds()))
                return;

            _pendingSceneSnapshotDelay -= Mathf.Max(0f, deltaTime);
            if (_pendingSceneSnapshotDelay > 0f)
                return;

            _pendingSceneSnapshot = false;
            _pendingSceneSnapshotDueRealtime = 0f;
            CaptureSceneSnapshot(_pendingSceneSnapshotSceneName, _pendingSceneSnapshotReason);
        }

        private void QueueAutoStartNewGame(string sceneName)
        {
            if (!IsMainMenuAutoStartEnabled() || _autoStartNewGameTriggered)
                return;

            if (!string.Equals(sceneName, autoStartMainMenuSceneName, StringComparison.Ordinal))
                return;

            _pendingAutoStartNewGame = true;
            _pendingAutoStartDelay = Mathf.Max(sceneSnapshotDelaySeconds, autoStartNewGameDelaySeconds);
            _pendingAutoStartDueRealtime = ResolveProfilerMonotonicTimeSeconds() + _pendingAutoStartDelay;
            _debugPendingAutoStart = $"{sceneName}@{_pendingAutoStartDelay:0.0}s";
        }

        private void UpdatePendingAutoStart(float deltaTime)
        {
            if (!_pendingAutoStartNewGame || _autoStartNewGameTriggered)
                return;

            if (TryProcessPendingAutoStartByRealtime(ResolveProfilerMonotonicTimeSeconds()))
                return;

            _pendingAutoStartDelay -= Mathf.Max(0f, deltaTime);
            if (_pendingAutoStartDelay > 0f)
                return;

            TryAutoStartNewGameFromMenu();
        }

        private void TryAutoStartNewGameFromMenu()
        {
            if (!IsMainMenuAutoStartEnabled())
            {
                _pendingAutoStartNewGame = false;
                _pendingAutoStartDueRealtime = 0f;
                _debugPendingAutoStart = "Disabled";
                return;
            }

            if (!_pendingAutoStartNewGame && !_autoStartNewGameTriggered)
            {
                if (!string.Equals(SceneManager.GetActiveScene().name, autoStartMainMenuSceneName, StringComparison.Ordinal))
                    return;
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, autoStartMainMenuSceneName, StringComparison.Ordinal))
            {
                _pendingAutoStartNewGame = false;
                _pendingAutoStartDueRealtime = 0f;
                _debugPendingAutoStart = "None";
                return;
            }

            if (ShouldYieldMenuRouteToShellSmoke())
            {
                _pendingAutoStartNewGame = false;
                _pendingAutoStartDueRealtime = 0f;
                _debugPendingAutoStart = "Deferred:ShellSmoke";
                RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "deferred reason=ShellSmokeOwner");
                return;
            }

            if (GameStartContextHolder.Current.IsValid)
            {
                _pendingAutoStartNewGame = false;
                _pendingAutoStartDueRealtime = 0f;
                _autoStartNewGameTriggered = true;
                _debugPendingAutoStart = "Skipped:ContextAlreadyValid";
                return;
            }

            MainMenuController mainMenuController = VerificationRuntimeProbe.ResolveMainMenuController();
            if (mainMenuController == null)
            {
                _pendingAutoStartDelay = 0.5f;
                _pendingAutoStartNewGame = true;
                _pendingAutoStartDueRealtime = ResolveProfilerMonotonicTimeSeconds() + _pendingAutoStartDelay;
                _debugPendingAutoStart = "retry";
                RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "retry reason=MainMenuControllerMissing");
                return;
            }

            _pendingAutoStartNewGame = false;
            _pendingAutoStartDueRealtime = 0f;
            _autoStartNewGameTriggered = true;
            _debugPendingAutoStart = "Triggered";
            RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "StartGame(slot=NewGame)");
            mainMenuController.StartGame(string.Empty);
        }

        private bool IsMainMenuAutoStartEnabled()
        {
#if HECTON_RUNTIME_PROFILER_MENU_AUTOSTART
            return autoStartNewGameFromMainMenu;
#else
            return false;
#endif
        }

        private static bool ShouldYieldMenuRouteToShellSmoke()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShellVerificationRuntimeSmokeTester shellSmoke =
                VerificationRuntimeProbe.FindSceneObjectIncludingInactive<ShellVerificationRuntimeSmokeTester>();
            if (shellSmoke == null)
                return false;

            return shellSmoke.WantsAutoStart() || ShellVerificationRuntimeSmokeTester.HasPersistedResumeState();
#else
            return false;
#endif
        }

        private bool TryCapturePendingSceneSnapshotByRealtime(float realtimeNow)
        {
            if (!_pendingSceneSnapshot)
                return false;

            if (realtimeNow < _pendingSceneSnapshotDueRealtime)
                return false;

            _pendingSceneSnapshot = false;
            _pendingSceneSnapshotDueRealtime = 0f;
            CaptureSceneSnapshot(_pendingSceneSnapshotSceneName, _pendingSceneSnapshotReason);
            return true;
        }

        private bool TryProcessPendingAutoStartByRealtime(float realtimeNow)
        {
            if (!_pendingAutoStartNewGame || _autoStartNewGameTriggered)
                return false;

            if (realtimeNow < _pendingAutoStartDueRealtime)
                return false;

            TryAutoStartNewGameFromMenu();
            return true;
        }

#if UNITY_EDITOR
        private static void RegisterEditorDiagnosticsHooks()
        {
            if (_editorHooksRegistered)
                return;

            CompilationPipeline.compilationStarted -= HandleEditorCompilationStarted;
            CompilationPipeline.compilationStarted += HandleEditorCompilationStarted;
            CompilationPipeline.compilationFinished -= HandleEditorCompilationFinished;
            CompilationPipeline.compilationFinished += HandleEditorCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
            _editorHooksRegistered = true;
        }

        private static void UnregisterEditorDiagnosticsHooks()
        {
            if (!_editorHooksRegistered)
                return;

            CompilationPipeline.compilationStarted -= HandleEditorCompilationStarted;
            CompilationPipeline.compilationFinished -= HandleEditorCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= HandleEditorUpdate;
            _editorHooksRegistered = false;
        }

        private static void HandleEditorCompilationStarted(object context)
        {
            TryAbortDirtyPlayEntry("CompilationStarted");

            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            CaptureEditorEventSnapshot("editor-compilation-started");
            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.compilation",
                $"started isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandleEditorCompilationFinished(object context)
        {
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            CaptureEditorEventSnapshot("editor-compilation-finished");
            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.compilation",
                $"finished isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandleBeforeAssemblyReload()
        {
            RuntimePerformanceProfiler instance = ActiveRuntime;
            if (instance != null)
            {
                instance.StopProfiling();
                instance.UnregisterFromTickManager();
            }

            UnregisterEditorDiagnosticsHooks();

            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            CaptureEditorEventSnapshot("editor-before-assembly-reload");
            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.assembly_reload",
                $"before isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                if (!_dirtyPlayRetryPending)
                    _dirtyPlayRetryCount = 0;

                if (RuntimeDiagnosticsTrace.IsActive)
                {
                    EditorPlayModeDiagnostics.TracePlayModeStateChange(state, nameof(RuntimePerformanceProfiler));
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "editor.playmode",
                        $"state={state} isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
                }

                return;
            }

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                TryAbortDirtyScenePlayEntry();

                if (RuntimeDiagnosticsTrace.IsActive)
                {
                    EditorPlayModeDiagnostics.TracePlayModeStateChange(state, nameof(RuntimePerformanceProfiler));
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "editor.playmode",
                        $"state={state} isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
                }

                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
                TryAbortDirtyPlayEntry("EnteredPlayMode");

            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            EditorPlayModeDiagnostics.TracePlayModeStateChange(state, nameof(RuntimePerformanceProfiler));
            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.playmode",
                $"state={state} isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandleEditorUpdate()
        {
            UpdateDirtyPlayRetry();

            RuntimePerformanceProfiler instance = ActiveRuntime;
            if (instance == null || !Application.isPlaying || !instance._debugProfilingActive)
                return;

            instance.EditorPumpSamplingFallback(EditorApplication.timeSinceStartup);
        }

        private static void CaptureEditorEventSnapshot(string reason)
        {
            RuntimePerformanceProfiler instance = ActiveRuntime;
            if (instance == null || !instance._debugProfilingActive)
                return;

            instance.SampleRecorders();
            instance.CaptureSceneSnapshot(SceneManager.GetActiveScene().name, reason);
        }

        private static void TryAbortDirtyPlayEntry(string reason)
        {
            if (!Application.isPlaying)
                return;

            if (_dirtyPlayRetryPending)
                return;

            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                ClearDirtyPlayRetryState();
                return;
            }

            if (ShouldContinueDirtyPlayMeasurement(reason))
                return;

            if (!ShouldGuardDirtyPlayScene())
                return;

            if (_dirtyPlayRetryCount >= MaxDirtyPlayRetryAttempts)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    $"[RuntimeProfilerDirtyPlayGate] Dirty play entry persisted after {_dirtyPlayRetryCount} retries. " +
                    $"scene={SceneManager.GetActiveScene().name} reason={reason} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
                return;
            }

            _dirtyPlayRetryPending = true;
            _dirtyPlayRetryCount++;
            _dirtyPlayLastReason = reason;
            SessionState.SetBool(DirtyPlayRetryPendingKey, true);
            SessionState.SetInt(DirtyPlayRetryCountKey, _dirtyPlayRetryCount);
            SessionState.SetString(DirtyPlayRetryReasonKey, _dirtyPlayLastReason);

            Hecton8.Core.H8Debug.LogWarning(
                $"[RuntimeProfilerDirtyPlayGate] Aborting dirty play entry. " +
                $"scene={SceneManager.GetActiveScene().name} reason={reason} retry={_dirtyPlayRetryCount} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");

            EditorPlayModeDiagnostics.RequestStopPlayMode(
                nameof(RuntimePerformanceProfiler),
                $"DirtyPlayGate:{reason}");
        }

        private static bool ShouldContinueDirtyPlayMeasurement(string reason)
        {
            RuntimePerformanceProfiler instance = ActiveRuntime;
            if (instance == null || !instance._debugProfilingActive)
                return false;

            RuntimeDiagnosticsTrace.WriteEvent(
                "play.dirty_entry",
                $"owner={nameof(RuntimePerformanceProfiler)} reason={reason} action=continue " +
                $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
            return true;
        }

        private static void UpdateDirtyPlayRetry()
        {
            if (!_dirtyPlayRetryPending)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            Hecton8.Core.H8Debug.LogWarning(
                $"[RuntimeProfilerDirtyPlayGate] Clearing blocked Play Mode retry. Automatic Play Mode restart is disabled. " +
                $"lastReason={_dirtyPlayLastReason} retry={_dirtyPlayRetryCount}");
            ClearDirtyPlayRetryState();
        }

        private static void TryAbortDirtyScenePlayEntry()
        {
            if (!ShouldGuardDirtyPlayScene())
                return;

            if (!TryCollectDirtyLoadedSceneNames(out string dirtySceneNames))
                return;

            Hecton8.Core.H8Debug.LogWarning(
                $"[RuntimeProfilerDirtySceneGate] Aborting play entry because loaded verification scenes are dirty. " +
                $"scenes={dirtySceneNames}");

            ClearDirtyPlayRetryState();
            EditorPlayModeDiagnostics.RequestStopPlayMode(
                nameof(RuntimePerformanceProfiler),
                $"DirtySceneGate:{dirtySceneNames}");
        }

        private static bool ShouldGuardDirtyPlayScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return false;

            string sceneName = activeScene.name;
            return string.Equals(sceneName, BootstrapSceneName, StringComparison.Ordinal) ||
                   string.Equals(sceneName, "01_MAIN_MENU", StringComparison.Ordinal);
        }

        private static bool TryCollectDirtyLoadedSceneNames(out string dirtySceneNames)
        {
            dirtySceneNames = string.Empty;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;

            StringBuilder dirtyScenes = new StringBuilder(64);
            bool hasDirtyScene = false;
            int loadedSceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < loadedSceneCount; sceneIndex++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(sceneIndex);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded || !loadedScene.isDirty)
                    continue;

                if (hasDirtyScene)
                    dirtyScenes.Append(',');

                dirtyScenes.Append(string.IsNullOrWhiteSpace(loadedScene.name) ? "<unnamed>" : loadedScene.name);
                hasDirtyScene = true;
            }

            if (!hasDirtyScene)
                return false;

            dirtySceneNames = dirtyScenes.ToString();
            return true;
        }

        private static void ClearDirtyPlayRetryState()
        {
            _dirtyPlayRetryPending = false;
            _dirtyPlayRetryCount = 0;
            _dirtyPlayLastReason = "None";
            SessionState.EraseBool(DirtyPlayRetryPendingKey);
            SessionState.EraseInt(DirtyPlayRetryCountKey);
            SessionState.EraseString(DirtyPlayRetryReasonKey);
        }
#endif

#if UNITY_EDITOR
        private void EditorPumpSamplingFallback(double editorNow)
        {
            if (!_debugProfilingActive)
                return;

            if (_lastEditorUpdateTime <= 0d)
            {
                _lastEditorUpdateTime = editorNow;
                return;
            }

            float editorDeltaTime = (float)Math.Max(0d, editorNow - _lastEditorUpdateTime);
            _lastEditorUpdateTime = editorNow;
            if (editorDeltaTime <= 0.0001f)
                return;

            float realtimeNow = ResolveProfilerMonotonicTimeSeconds();
            TryCapturePendingSceneSnapshotByRealtime(realtimeNow);
            TryProcessPendingAutoStartByRealtime(realtimeNow);
            float silenceDuration = realtimeNow - _lastDriveRealtimeSinceStartup;
            if (silenceDuration < _editorFallbackPumpThresholdSeconds)
                return;

            if (RuntimeDiagnosticsTrace.IsActive && realtimeNow >= _nextEditorFallbackTraceTime)
            {
                _nextEditorFallbackTraceTime = realtimeNow + 2f;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "editor.fallback",
                    $"scene={SceneManager.GetActiveScene().name} frame={SystemDispatcher.CurrentFrameIndex} silence={silenceDuration:0.000} dt={editorDeltaTime:0.0000} compiling={EditorApplication.isCompiling} paused={EditorApplication.isPaused}");
            }

            DriveSampling(editorDeltaTime, true);
        }
#endif

        private void CaptureSceneSnapshot(string sceneName, string reason)
        {
            UpdateWorldDiagnostics();
            _debugLastSceneSnapshot = SceneSnapshotTraceMessage;
            _debugPendingSceneSnapshot = "None";
            RuntimeDiagnosticsTrace.WriteEvent("scene.snapshot", _debugLastSceneSnapshot);
        }

        private void ApplyScatterRebuildProfile(in ScatterRebuildProfileSnapshot snapshot)
        {
            _hasScatterSnapshot = true;
            _lastScatterSnapshot = snapshot;
            _debugLastScatterTotalMs = snapshot.TotalMs;
            _debugLastScatterSamplingMs = snapshot.SamplingMs;
            _debugLastScatterRescueMs = snapshot.RescueMs;
            _debugLastScatterRestoreMs = snapshot.RestoreMs;
            _debugLastScatterReconcileMs = snapshot.ReconcileMs;

            _scatterBuilder.Clear();
            _scatterBuilder.Append("total=")
                .Append(snapshot.TotalMs.ToString("0.00"))
                .Append("ms sampling=")
                .Append(snapshot.SamplingMs.ToString("0.00"))
                .Append("ms rescue=")
                .Append(snapshot.RescueMs.ToString("0.00"))
                .Append("ms restore=")
                .Append(snapshot.RestoreMs.ToString("0.00"))
                .Append("ms reconcile=")
                .Append(snapshot.ReconcileMs.ToString("0.00"))
                .Append("ms cleanup=")
                .Append(snapshot.ReconcileCleanupMs.ToString("0.00"))
                .Append("ms spawn=")
                .Append(snapshot.ReconcileSpawnMs.ToString("0.00"))
                .Append("ms fauna=")
                .Append(snapshot.ReconcileFaunaMs.ToString("0.00"))
                .Append("ms active=")
                .Append(snapshot.ActiveCount)
                .Append(" desired=")
                .Append(snapshot.DesiredCount)
                .Append(" reason=")
                .Append(string.IsNullOrWhiteSpace(snapshot.Reason) ? "None" : snapshot.Reason)
                .Append(" zone=")
                .Append(string.IsNullOrWhiteSpace(snapshot.Zone) ? "None" : snapshot.Zone)
                .Append(" biome=")
                .Append(string.IsNullOrWhiteSpace(snapshot.Biome) ? "None" : snapshot.Biome)
                .Append(" pattern=")
                .Append(string.IsNullOrWhiteSpace(snapshot.Pattern) ? "None" : snapshot.Pattern);
            _debugLastScatterReport = _scatterBuilder.ToString();
        }

        private bool ShouldCaptureRendererOwnershipAudit()
        {
            if (!RuntimeDiagnosticsTrace.IsActive)
                return false;

            float now = ResolveProfilerUnscaledTimeSeconds();
            if (now < _nextOwnershipAuditAllowedTime)
                return false;

            if (_peakBatches >= rendererOwnershipAuditBatchesThreshold)
                return true;

            return traceRendererOwnershipOnGcSpike &&
                   _peakGcAllocBytes >= rendererOwnershipAuditGcThresholdBytes;
        }

        private void CaptureRendererOwnershipAudit()
        {
            if (Application.isPlaying)
                _nextOwnershipAuditAllowedTime = ResolveProfilerUnscaledTimeSeconds() + rendererOwnershipAuditCooldownSeconds;

            _auditScatterFamilies.Clear();
            _auditGeologyFamilies.Clear();
            _auditVoxelFamilies.Clear();
            _auditSocketKinds.Clear();

            int totalRenderers = 0;
            int voxelRenderers = 0;
            int geologyRenderers = 0;
            int scatterRenderers = 0;
            int socketRenderers = 0;
            int zoneRenderers = 0;
            int otherRenderers = 0;
            int skippedSceneRoots = 0;
            int skippedSubtrees = 0;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                int rootCount = scene.rootCount;
                if (rootCount > _auditSceneRoots.Capacity)
                {
                    skippedSceneRoots += rootCount;
                    continue;
                }

                _auditSceneRoots.Clear();
                scene.GetRootGameObjects(_auditSceneRoots);
                for (int rootIndex = 0; rootIndex < _auditSceneRoots.Count; rootIndex++)
                {
                    GameObject root = _auditSceneRoots[rootIndex];
                    if (root == null || !root.activeInHierarchy)
                        continue;

                    AccumulateRendererOwnershipAudit(
                        root.transform,
                        ref totalRenderers,
                        ref voxelRenderers,
                        ref geologyRenderers,
                        ref scatterRenderers,
                        ref socketRenderers,
                        ref zoneRenderers,
                        ref otherRenderers,
                        ref skippedSubtrees);
                }
            }

            _auditBuilder.Clear();
            _auditBuilder.Append("renderers total=")
                .Append(totalRenderers)
                .Append(" voxel=")
                .Append(voxelRenderers)
                .Append(" geology=")
                .Append(geologyRenderers)
                .Append(" scatter=")
                .Append(scatterRenderers)
                .Append(" sockets=")
                .Append(socketRenderers)
                .Append(" zones=")
                .Append(zoneRenderers)
                .Append(" other=")
                .Append(otherRenderers)
                .Append(" skippedRoots=")
                .Append(skippedSceneRoots)
                .Append(" skippedSubtrees=")
                .Append(skippedSubtrees);
            AppendTopAuditEntries(_auditBuilder, "scatterTop", _auditScatterFamilies);
            AppendTopAuditEntries(_auditBuilder, "geologyTop", _auditGeologyFamilies);
            AppendTopAuditEntries(_auditBuilder, "voxelTop", _auditVoxelFamilies);
            AppendTopAuditEntries(_auditBuilder, "socketTop", _auditSocketKinds);

            _debugLastOwnershipAudit = _auditBuilder.ToString();
            RuntimeDiagnosticsTrace.WriteEvent("render.audit", _debugLastOwnershipAudit);
        }

        private void AccumulateRendererOwnershipAudit(
            Transform root,
            ref int totalRenderers,
            ref int voxelRenderers,
            ref int geologyRenderers,
            ref int scatterRenderers,
            ref int socketRenderers,
            ref int zoneRenderers,
            ref int otherRenderers,
            ref int skippedSubtrees)
        {
            int stackCount = 0;
            if (!TryPushRendererAuditTransform(root, ref stackCount, ref skippedSubtrees))
                return;

            while (stackCount > 0)
            {
                Transform current = _rendererAuditStack[--stackCount];
                _rendererAuditStack[stackCount] = null;
                if (current == null || !current.gameObject.activeInHierarchy)
                    continue;

                if (current.TryGetComponent(out Renderer renderer) && renderer.enabled)
                {
                    totalRenderers++;
                    ClassifyRendererOwnership(
                        current,
                        ref voxelRenderers,
                        ref geologyRenderers,
                        ref scatterRenderers,
                        ref socketRenderers,
                        ref zoneRenderers,
                        ref otherRenderers);
                }

                int childCount = current.childCount;
                for (int childIndex = 0; childIndex < childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (child != null && child.gameObject.activeInHierarchy)
                        TryPushRendererAuditTransform(child, ref stackCount, ref skippedSubtrees);
                }
            }
        }

        private bool TryPushRendererAuditTransform(Transform target, ref int stackCount, ref int skippedSubtrees)
        {
            if (target == null)
                return false;

            if (stackCount >= _rendererAuditStack.Length)
            {
                skippedSubtrees++;
                return false;
            }

            _rendererAuditStack[stackCount++] = target;
            return true;
        }

        private void ClassifyRendererOwnership(
            Transform rendererTransform,
            ref int voxelRenderers,
            ref int geologyRenderers,
            ref int scatterRenderers,
            ref int socketRenderers,
            ref int zoneRenderers,
            ref int otherRenderers)
        {
            Transform cursor = rendererTransform;
            while (cursor != null)
            {
                if (cursor.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime voxelRuntime))
                {
                    voxelRenderers++;
                    IncrementAuditCount(_auditVoxelFamilies, voxelRuntime.FamilyId);
                    return;
                }

                if (cursor.TryGetComponent(out WorldGenerativeGeologyBinding binding))
                {
                    geologyRenderers++;
                    IncrementAuditCount(_auditGeologyFamilies, binding.FamilyId);
                    return;
                }

                if (cursor.TryGetComponent(out WorldProceduralProxyInstance proxy))
                {
                    scatterRenderers++;
                    IncrementAuditCount(_auditScatterFamilies, proxy.FamilyId);
                    return;
                }

                if (cursor.TryGetComponent(out WorldContentSocket socket))
                {
                    socketRenderers++;
                    IncrementAuditCount(_auditSocketKinds, ResolveSocketKindName(socket.Kind));
                    return;
                }

                if (cursor.TryGetComponent(out WorldZoneAnchor _))
                {
                    zoneRenderers++;
                    return;
                }

                cursor = cursor.parent;
            }

            otherRenderers++;
        }

        private static string ResolveSocketKindName(WorldContentSocket.ContentKind kind)
        {
            switch (kind)
            {
                case WorldContentSocket.ContentKind.ResourcePickup:
                    return "ResourcePickup";
                case WorldContentSocket.ContentKind.ResourceNode:
                    return "ResourceNode";
                case WorldContentSocket.ContentKind.FabricationStation:
                    return "FabricationStation";
                case WorldContentSocket.ContentKind.ConstructionPoint:
                    return "ConstructionPoint";
                case WorldContentSocket.ContentKind.PowerPoint:
                    return "PowerPoint";
                case WorldContentSocket.ContentKind.ServiceTarget:
                    return "ServiceTarget";
                case WorldContentSocket.ContentKind.NavigationMarker:
                    return "NavigationMarker";
                case WorldContentSocket.ContentKind.HazardPoint:
                    return "HazardPoint";
                case WorldContentSocket.ContentKind.CombatPoint:
                    return "CombatPoint";
                case WorldContentSocket.ContentKind.Landmark:
                    return "Landmark";
                default:
                    return "Generic";
            }
        }

        private static void IncrementAuditCount(Dictionary<string, int> counts, string key)
        {
            string safeKey = string.IsNullOrWhiteSpace(key) ? "None" : key;
            if (counts.TryGetValue(safeKey, out int count))
                counts[safeKey] = count + 1;
            else
                counts.Add(safeKey, 1);
        }

        private static void AppendTopAuditEntries(StringBuilder builder, string label, Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return;

            string topKey1 = null;
            string topKey2 = null;
            string topKey3 = null;
            int topCount1 = 0;
            int topCount2 = 0;
            int topCount3 = 0;

            Dictionary<string, int>.Enumerator enumerator = counts.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> pair = enumerator.Current;
                if (pair.Value > topCount1)
                {
                    topKey3 = topKey2;
                    topCount3 = topCount2;
                    topKey2 = topKey1;
                    topCount2 = topCount1;
                    topKey1 = pair.Key;
                    topCount1 = pair.Value;
                }
                else if (pair.Value > topCount2)
                {
                    topKey3 = topKey2;
                    topCount3 = topCount2;
                    topKey2 = pair.Key;
                    topCount2 = pair.Value;
                }
                else if (pair.Value > topCount3)
                {
                    topKey3 = pair.Key;
                    topCount3 = pair.Value;
                }
            }

            builder.Append(' ').Append(label).Append('=');
            AppendTopAuditEntry(builder, topKey1, topCount1);
            if (topCount2 > 0)
            {
                builder.Append(',');
                AppendTopAuditEntry(builder, topKey2, topCount2);
            }

            if (topCount3 > 0)
            {
                builder.Append(',');
                AppendTopAuditEntry(builder, topKey3, topCount3);
            }
        }

        private static void AppendTopAuditEntry(StringBuilder builder, string key, int count)
        {
            builder.Append(string.IsNullOrWhiteSpace(key) ? "None" : key)
                .Append(':')
                .Append(count);
        }

        private static float ReadMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds)
                return (float)(recorder.LastValue / 1000000.0d);

            return (float)recorder.LastValue;
        }

        private static float ReadMegabytes(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.Bytes)
                return (float)(recorder.LastValue / BytesPerMegabyte);

            return (float)recorder.LastValue;
        }

        private static int ReadIntValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0;

            long value = recorder.LastValue;
            if (value <= 0L)
                return 0;

            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();

            recorder = default;
        }

        private static bool MatchesCandidate(string value, string candidate)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(candidate))
                return false;

            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase) ||
                   value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAnyKeyword(string value, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }

#if UNITY_EDITOR
    internal static class EditorPlayModeDiagnostics
    {
        private static string _lastStopOwner = "None";
        private static string _lastStopReason = "None";
        private static double _lastStopRequestTime;

        internal static void RequestStopPlayMode(string owner, string reason, UnityEngine.Object context = null)
        {
            string safeOwner = Sanitize(owner, "UnknownOwner");
            string safeReason = Sanitize(reason, "None");
            _lastStopOwner = safeOwner;
            _lastStopReason = safeReason;
            _lastStopRequestTime = EditorApplication.timeSinceStartup;

            WriteTraceEvent(
                "play.exit_request",
                $"owner={safeOwner} reason={safeReason} scene={GetActiveSceneName()} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} paused={EditorApplication.isPaused}");

            if (context != null)
                Hecton8.Core.H8Debug.Log($"[PlayModeExit] owner={safeOwner} reason={safeReason}", context);
            else
                Hecton8.Core.H8Debug.Log($"[PlayModeExit] owner={safeOwner} reason={safeReason}");

            EditorApplication.isPlaying = false;
        }

        internal static void TracePlayModeStateChange(PlayModeStateChange state, string observer)
        {
            string safeObserver = Sanitize(observer, "UnknownObserver");
            WriteTraceEvent(
                "play.state",
                $"observer={safeObserver} state={state} scene={GetActiveSceneName()} " +
                $"lastStopOwner={_lastStopOwner} lastStopReason={_lastStopReason} " +
                $"lastStopAge={FormatAgeSeconds(_lastStopRequestTime)} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} paused={EditorApplication.isPaused}");
        }

        private static void WriteTraceEvent(string channel, string message)
        {
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(channel, message);
        }

        private static string GetActiveSceneName()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.name : "InvalidScene";
        }

        private static string FormatAgeSeconds(double requestTime)
        {
            if (requestTime <= 0d)
                return "n/a";

            double age = EditorApplication.timeSinceStartup - requestTime;
            if (age < 0d)
                age = 0d;

            return age.ToString("0.000");
        }

        private static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        }
    }
#endif
}

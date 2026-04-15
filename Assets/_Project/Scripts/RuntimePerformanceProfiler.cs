// ============================================================================
// HECTON-8 - RuntimePerformanceProfiler.cs
// Runtime profiler bridge for frame-time, GC and memory budgets.
// Uses ProfilerRecorder and GameTickManager instead of native Update.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Hecton.UI.MainMenu;
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
#endif

namespace Hecton8.Dev
{
    /// <summary>
    /// Captures runtime performance counters and reports budget violations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Runtime Performance Profiler")]
    public sealed class RuntimePerformanceProfiler : MonoBehaviour, ITickable, ISlowTickable
    {
        private const int RecorderCapacity = 1;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const string AutoBootstrapObjectName = "__DEV_RuntimePerformanceProfiler";
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

        private static RuntimePerformanceProfiler _instance;

        [Header("Execution")]
        [SerializeField] private bool startProfilingOnEnable = true;
        [SerializeField] private bool logBudgetViolations = true;
        [SerializeField] private bool logEveryWindow = false;
        [SerializeField] private float sampleWindowSeconds = 5f;

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
        [SerializeField] private bool autoStartNewGameFromMainMenu = true;
        [SerializeField] private float autoStartNewGameDelaySeconds = 2.5f;
        [SerializeField] private string autoStartMainMenuSceneName = "01_MAIN_MENU";

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
        [SerializeField] private string _debugLastVRAMWarning = "None";

        private readonly List<ProfilerRecorderHandle> _availableHandles = new List<ProfilerRecorderHandle>(256);
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        private readonly StringBuilder _scatterBuilder = new StringBuilder(768);
        private readonly StringBuilder _auditBuilder = new StringBuilder(1024);
        private readonly Dictionary<string, int> _auditScatterFamilies = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int> _auditGeologyFamilies = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int> _auditVoxelFamilies = new Dictionary<string, int>(16);
        private readonly Dictionary<string, int> _auditSocketKinds = new Dictionary<string, int>(16);

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

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private float _sampleElapsed;
        private float _peakFrameTimeMs;
        private float _peakMainThreadMs;
        private int _peakGcAllocBytes;
        private float _peakSystemMemoryMb;
        private int _peakSetPassCalls;
        private int _peakBatches;
        private float _nextOwnershipAuditAllowedTime;
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
        private string _pendingSceneSnapshotSceneName = string.Empty;
        private string _pendingSceneSnapshotReason = string.Empty;
        private bool _pendingAutoStartNewGame;
        private float _pendingAutoStartDelay;
        private bool _autoStartNewGameTriggered;
        private bool _hasScatterSnapshot;
        private ScatterRebuildProfileSnapshot _lastScatterSnapshot;
        private int _lastDrivenFrame = -1;
        private float _lastDriveRealtimeSinceStartup;
        private float _nextDriveHeartbeatTime;
        private double _lastEditorUpdateTime;
        private float _editorFallbackPumpThresholdSeconds;
        private float _nextEditorFallbackTraceTime;
        private bool _loggedFirstDrive;

#if UNITY_EDITOR
        private static bool _editorHooksRegistered;
#endif

        internal static RuntimePerformanceProfiler Instance => _instance;

        /// <summary>
        /// Returns whether runtime profiling is currently sampling and recording trace windows.
        /// </summary>
        public bool IsProfilingActive => _debugProfilingActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDevelopmentRuntimeProfiler()
        {
            EnsureDevelopmentRuntimeProfilerInstance();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDevelopmentRuntimeProfilerAfterSceneLoad()
        {
            EnsureDevelopmentRuntimeProfilerInstance();
        }

        private static void EnsureDevelopmentRuntimeProfilerInstance()
        {
            if (!Application.isPlaying)
                return;

            if (_instance != null)
                return;

            RuntimePerformanceProfiler existing = UnityEngine.Object.FindAnyObjectByType<RuntimePerformanceProfiler>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            GameObject profilerObject = new GameObject(AutoBootstrapObjectName);
            DontDestroyOnLoad(profilerObject);

            RuntimePerformanceProfiler profiler = profilerObject.AddComponent<RuntimePerformanceProfiler>();
            profiler.ApplyAutoBootstrapDefaults();
        }
#endif

        internal static void RecordScatterRebuildProfile(in ScatterRebuildProfileSnapshot snapshot)
        {
            RuntimePerformanceProfiler instance = _instance;
            if (instance == null)
                return;

            instance.ApplyScatterRebuildProfile(in snapshot);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.lifecycle",
                    $"duplicate-awake destroy id={GetEntityId()} existing={_instance.GetEntityId()} name={gameObject.name}");
#endif
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

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

            if (_instance != this)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            RegisterWithTickManager();
#if UNITY_EDITOR
            RegisterEditorDiagnosticsHooks();
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-enable id={GetEntityId()} scene={SceneManager.GetActiveScene().name} tick={_registeredTick} slow={_registeredSlowTick}");
#endif
            if (startProfilingOnEnable)
                StartProfiling();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (_instance != this)
                return;

            if (_registeredTick && _registeredSlowTick)
                return;

            RegisterWithTickManager();

            if ((!_registeredTick || !_registeredSlowTick) && _debugProfilingActive)
                Debug.LogError("[RuntimeProfiler] GameTickManager registration failed.", this);
        }

        private void Update()
        {
            if (!Application.isPlaying || !_debugProfilingActive)
                return;

            if (_lastDrivenFrame == Time.frameCount)
                return;

            DriveSampling(Time.unscaledDeltaTime, true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_instance != this)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-disable id={GetEntityId()} scene={SceneManager.GetActiveScene().name} active={_debugProfilingActive}");
#endif
            StopProfiling();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
#if UNITY_EDITOR
            UnregisterEditorDiagnosticsHooks();
#endif
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent(
                "runtime.lifecycle",
                $"on-destroy id={GetEntityId()} scene={SceneManager.GetActiveScene().name} active={_debugProfilingActive} instanceMatch={(_instance == this)}");
#endif
            if (_instance == this)
                _instance = null;
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
            StopProfiling();

            if (writeTraceToFile)
            {
                RuntimeDiagnosticsTrace.EnsureSession(traceSessionLabel);
                _debugTraceFilePath = RuntimeDiagnosticsTrace.CurrentFilePath;
                RuntimeDiagnosticsTrace.WriteEvent("session", "Runtime performance profiler session started.");
                Debug.Log($"[RuntimeProfiler] Trace file: {_debugTraceFilePath}", this);
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
            _lastDriveRealtimeSinceStartup = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
            _lastEditorUpdateTime = 0d;
            _editorFallbackPumpThresholdSeconds = Mathf.Max(0.25f, sceneSnapshotDelaySeconds);
            _nextEditorFallbackTraceTime = 0f;
            _loggedFirstDrive = false;
            _nextDriveHeartbeatTime = 0f;
            _pendingAutoStartNewGame = false;
            _pendingAutoStartDelay = 0f;
            _autoStartNewGameTriggered = false;
            _debugPendingAutoStart = "None";
            QueueSceneSnapshot(SceneManager.GetActiveScene().name, "profiling-start");

            if (!_debugProfilingActive)
            {
                _debugLastReport = "No profiler stats resolved.";
                Debug.LogWarning("[RuntimeProfiler] No profiler stats were resolved. Use context menu to inspect available counters.", this);
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
            float sampleWindow = 2f)
        {
            startProfilingOnEnable = autoStartOnEnable;
            logBudgetViolations = enableBudgetViolationLogging;
            logEveryWindow = enableWindowLogging;
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
            _debugPendingSceneSnapshot = "None";
            _pendingAutoStartNewGame = false;
            _pendingAutoStartDelay = 0f;
            _debugPendingAutoStart = "None";
            _lastEditorUpdateTime = 0d;
            _editorFallbackPumpThresholdSeconds = 0f;
            _nextEditorFallbackTraceTime = 0f;

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

            Debug.Log(_reportBuilder.ToString(), this);
        }

        public void Tick(float deltaTime)
        {
            if (!_debugProfilingActive)
                return;

            float sampleDeltaTime = deltaTime > 0f ? deltaTime : Time.unscaledDeltaTime;
            DriveSampling(sampleDeltaTime, false);
        }

        public void SlowTick()
        {
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
                $"fallbackActive={_debugUsingFallbackUpdate} dt={_debugLastDeltaTime:0.000}";
        }

        [ContextMenu("Log Runtime Performance Profiling Status")]
        public void LogStatusToConsole()
        {
            Debug.Log("[RuntimeProfiler] " + DescribeStatus(), this);
        }

        private void RegisterWithTickManager()
        {
            if (GameTickManager.Instance == null)
                return;

            if (!_registeredTick)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void UnregisterFromTickManager()
        {
            if (GameTickManager.Instance == null)
                return;

            if (_registeredTick)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
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
            float realtimeNow = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
            float realtimeDeltaTime = 0f;
            if (_lastDriveRealtimeSinceStartup > 0f)
                realtimeDeltaTime = Mathf.Max(0f, realtimeNow - _lastDriveRealtimeSinceStartup);

            float effectiveDeltaTime = Mathf.Max(deltaTime, realtimeDeltaTime);

            _lastDriveRealtimeSinceStartup = realtimeNow;
            _lastDrivenFrame = Time.frameCount;
            _debugTickCount++;
            _debugLastDeltaTime = effectiveDeltaTime;
            _debugUsingFallbackUpdate = usingFallbackUpdate;
            _debugLastDriveSource = usingFallbackUpdate ? "Update" : "Tick";
            if (usingFallbackUpdate)
                _debugFallbackUpdateCount++;

            if (!_loggedFirstDrive && RuntimeDiagnosticsTrace.IsActive)
            {
                _loggedFirstDrive = true;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.driver",
                    $"source={_debugLastDriveSource} frame={Time.frameCount} dt={effectiveDeltaTime:0.0000} active={_debugProfilingActive}");
            }

            if (RuntimeDiagnosticsTrace.IsActive && realtimeNow >= _nextDriveHeartbeatTime)
            {
                _nextDriveHeartbeatTime = realtimeNow + 2f;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "runtime.heartbeat",
                    $"scene={SceneManager.GetActiveScene().name} frame={Time.frameCount} source={_debugLastDriveSource} dt={effectiveDeltaTime:0.0000} elapsed={_sampleElapsed:0.0000} active={_debugProfilingActive}");
            }

            _sampleElapsed += effectiveDeltaTime;
            SampleRecorders();
            UpdatePendingSceneSnapshot(effectiveDeltaTime);
            UpdatePendingAutoStart(effectiveDeltaTime);

            if (_sampleElapsed >= sampleWindowSeconds)
                FlushSampleWindow();
        }

        private void FlushSampleWindow()
        {
            _debugWindowCount++;
            _debugLastWindowExceededBudget =
                _peakFrameTimeMs > frameTimeBudgetMs ||
                _peakMainThreadMs > mainThreadBudgetMs ||
                _peakGcAllocBytes > gcAllocBudgetBytes ||
                _peakSystemMemoryMb > systemMemoryBudgetMb ||
                _peakSetPassCalls > setPassBudget ||
                _peakBatches > batchesBudget;

            _reportBuilder.Clear();
            _reportBuilder.Append("[RuntimeProfiler] window=")
                .Append(_debugWindowCount)
                .Append(" frame=")
                .Append(_peakFrameTimeMs.ToString("0.00"))
                .Append("ms main=")
                .Append(_peakMainThreadMs.ToString("0.00"))
                .Append("ms gc=")
                .Append(_peakGcAllocBytes)
                .Append("B mem=")
                .Append(_peakSystemMemoryMb.ToString("0.0"))
                .Append("MB setPass=")
                .Append(_peakSetPassCalls)
                .Append(" batches=")
                .Append(_peakBatches)
                .Append(" geoBindings=")
                .Append(_debugGeoBindings)
                .Append(" geoRoots=")
                .Append(_debugGeoGeneratedRoots)
                .Append(" geoRenderers=")
                .Append(_debugGeoGeneratedRenderers)
                .Append(" geoVoxels=")
                .Append(_debugGeoVoxelVolumes)
                .Append(" geoVoxelColliders=")
                .Append(_debugGeoVoxelColliders)
                .Append(" scatterDispatch=")
                .Append(_peakScatterDispatchMs.ToString("0.00"))
                .Append("ms scatterBegin=")
                .Append(_peakScatterSamplingBeginMs.ToString("0.00"))
                .Append("ms scatterInputs=")
                .Append(_peakScatterSamplingBuildInputsMs.ToString("0.00"))
                .Append("ms scatterSchedule=")
                .Append(_peakScatterSamplingScheduleMs.ToString("0.00"))
                .Append("ms scatterProcess=")
                .Append(_peakScatterProcessingTotalMs.ToString("0.00"))
                .Append("ms scatterCells=")
                .Append(_peakScatterProcessingCellsMs.ToString("0.00"))
                .Append("ms scatterRescue=")
                .Append(_peakScatterProcessingRescueMs.ToString("0.00"))
                .Append("ms scatterRestore=")
                .Append(_peakScatterProcessingRestoreMs.ToString("0.00"))
                .Append("ms scatterReconcile=")
                .Append(_peakScatterReconcileTotalMs.ToString("0.00"))
                .Append("ms scatterCleanup=")
                .Append(_peakScatterReconcileCleanupMs.ToString("0.00"))
                .Append("ms scatterSpawn=")
                .Append(_peakScatterReconcileSpawnMs.ToString("0.00"))
                .Append("ms scatterFauna=")
                .Append(_peakScatterReconcileFaunaMs.ToString("0.00"))
                .Append("ms shadowSchedule=")
                .Append(_peakScatterBackendShadowScheduleMs.ToString("0.00"))
                .Append("ms shadowPump=")
                .Append(_peakScatterBackendShadowPumpMs.ToString("0.00"))
                .Append("ms");

            if (_hasScatterSnapshot)
            {
                _reportBuilder.Append(" scatterTotal=")
                    .Append(_debugLastScatterTotalMs.ToString("0.00"))
                    .Append("ms scatterSample=")
                    .Append(_debugLastScatterSamplingMs.ToString("0.00"))
                    .Append("ms scatterRebuildReason=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Reason) ? "None" : _lastScatterSnapshot.Reason)
                    .Append(" scatterZone=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Zone) ? "None" : _lastScatterSnapshot.Zone)
                    .Append(" scatterPattern=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Pattern) ? "None" : _lastScatterSnapshot.Pattern);
            }

            _debugLastReport = _reportBuilder.ToString();
            RuntimeDiagnosticsTrace.WriteEvent("runtime", _debugLastReport);

            if (logEveryWindow || (_debugLastWindowExceededBudget && logBudgetViolations))
            {
                if (_debugLastWindowExceededBudget)
                    Debug.LogWarning(_debugLastReport, this);
                else
                    Debug.Log(_debugLastReport, this);
            }

            if (traceRendererOwnershipOnSpike && ShouldCaptureRendererOwnershipAudit())
                CaptureRendererOwnershipAudit();

            ResetSampleWindow();
        }

        private void ResetSampleWindow()
        {
            _sampleElapsed = 0f;
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
            if (VRAMMonitor.Instance == null)
            {
                _debugLastTextureMB = 0f;
                _debugLastRenderTextureMB = 0f;
                _debugLastTotalVRAMMB = 0f;
                _debugLastVRAMWarning = "VRAMMonitor not available";
                return;
            }

            // Query VRAM breakdown (zero-GC)
            VRAMMonitor monitor = VRAMMonitor.Instance;
            long textureBytes = monitor.TextureMemoryBytes;
            long renderTextureBytes = monitor.RenderTextureMemoryBytes;
            long totalBytes = monitor.TotalVRAMBytes;
            
            _debugLastTextureMB = textureBytes / BytesPerMegabyte;
            _debugLastRenderTextureMB = renderTextureBytes / BytesPerMegabyte;
            _debugLastTotalVRAMMB = totalBytes / BytesPerMegabyte;

            // Check thresholds
            bool textureOverBudget = VRAMMonitor.Instance.IsTextureMemoryOverBudget;
            bool rtOverBudget = VRAMMonitor.Instance.IsRenderTextureMemoryOverBudget;
            bool totalOverBudget = VRAMMonitor.Instance.IsTotalVRAMOverBudget;

            if (textureOverBudget || rtOverBudget || totalOverBudget)
            {
                _debugLastVRAMWarning = $"VRAM OVER BUDGET: Tex={textureOverBudget} RT={rtOverBudget} Total={totalOverBudget}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logBudgetViolations)
                {
                    Debug.LogWarning($"[RuntimeProfiler] {_debugLastVRAMWarning} | Texture: {_debugLastTextureMB:0.0} MB | RT: {_debugLastRenderTextureMB:0.0} MB | Total: {_debugLastTotalVRAMMB:0.0} MB");
                }
#endif
            }
            else
            {
                _debugLastVRAMWarning = "None";
            }
        }

        private void ClampSettings()
        {
            sampleWindowSeconds = Mathf.Clamp(sampleWindowSeconds, 0.5f, 60f);
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

        private void ApplyAutoBootstrapDefaults()
        {
            startProfilingOnEnable = true;
            writeTraceToFile = true;
            logSceneSnapshots = true;
            logBudgetViolations = true;
            logEveryWindow = false;
            sampleWindowSeconds = 2f;
            traceSessionLabel = "scatter_baseline";
            autoStartNewGameFromMainMenu = true;
            ClampSettings();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _debugCurrentScene = scene.name;
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
            _pendingSceneSnapshotSceneName = string.IsNullOrWhiteSpace(sceneName) ? "Unknown" : sceneName;
            _pendingSceneSnapshotReason = string.IsNullOrWhiteSpace(reason) ? "runtime" : reason;
            _debugPendingSceneSnapshot = $"{_pendingSceneSnapshotSceneName}@{_pendingSceneSnapshotDelay:0.0}s";
        }

        private void UpdatePendingSceneSnapshot(float deltaTime)
        {
            if (!_pendingSceneSnapshot)
                return;

            _pendingSceneSnapshotDelay -= Mathf.Max(0f, deltaTime);
            if (_pendingSceneSnapshotDelay > 0f)
                return;

            _pendingSceneSnapshot = false;
            CaptureSceneSnapshot(_pendingSceneSnapshotSceneName, _pendingSceneSnapshotReason);
        }

        private void QueueAutoStartNewGame(string sceneName)
        {
            if (!autoStartNewGameFromMainMenu || _autoStartNewGameTriggered)
                return;

            if (!string.Equals(sceneName, autoStartMainMenuSceneName, StringComparison.Ordinal))
                return;

            _pendingAutoStartNewGame = true;
            _pendingAutoStartDelay = Mathf.Max(sceneSnapshotDelaySeconds, autoStartNewGameDelaySeconds);
            _debugPendingAutoStart = $"{sceneName}@{_pendingAutoStartDelay:0.0}s";
        }

        private void UpdatePendingAutoStart(float deltaTime)
        {
            if (!_pendingAutoStartNewGame || _autoStartNewGameTriggered)
                return;

            _pendingAutoStartDelay -= Mathf.Max(0f, deltaTime);
            if (_pendingAutoStartDelay > 0f)
                return;

            TryAutoStartNewGameFromMenu();
        }

        private void TryAutoStartNewGameFromMenu()
        {
            if (!_pendingAutoStartNewGame && !_autoStartNewGameTriggered)
            {
                if (!string.Equals(SceneManager.GetActiveScene().name, autoStartMainMenuSceneName, StringComparison.Ordinal))
                    return;
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, autoStartMainMenuSceneName, StringComparison.Ordinal))
            {
                _pendingAutoStartNewGame = false;
                _debugPendingAutoStart = "None";
                return;
            }

            if (ShouldYieldMenuRouteToShellSmoke())
            {
                _pendingAutoStartNewGame = false;
                _debugPendingAutoStart = "Deferred:ShellSmoke";
                RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "deferred reason=ShellSmokeOwner");
                return;
            }

            if (GameStartContextHolder.Current.IsValid)
            {
                _pendingAutoStartNewGame = false;
                _autoStartNewGameTriggered = true;
                _debugPendingAutoStart = "Skipped:ContextAlreadyValid";
                return;
            }

            MainMenuController mainMenuController = VerificationRuntimeProbe.ResolveMainMenuController();
            if (mainMenuController == null)
            {
                _pendingAutoStartDelay = 0.5f;
                _pendingAutoStartNewGame = true;
                _debugPendingAutoStart = $"{autoStartMainMenuSceneName}@retry";
                RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "retry reason=MainMenuControllerMissing");
                return;
            }

            _pendingAutoStartNewGame = false;
            _autoStartNewGameTriggered = true;
            _debugPendingAutoStart = "Triggered";
            RuntimeDiagnosticsTrace.WriteEvent("menu.auto_start", "StartGame(slot=NewGame)");
            mainMenuController.StartGame(string.Empty);
        }

        private static bool ShouldYieldMenuRouteToShellSmoke()
        {
            ShellVerificationRuntimeSmokeTester shellSmoke =
                UnityEngine.Object.FindAnyObjectByType<ShellVerificationRuntimeSmokeTester>(FindObjectsInactive.Include);
            if (shellSmoke == null)
                return false;

            return shellSmoke.WantsAutoStart() || ShellVerificationRuntimeSmokeTester.HasPersistedResumeState();
        }

#if UNITY_EDITOR
        private static void RegisterEditorDiagnosticsHooks()
        {
            if (_editorHooksRegistered)
                return;

            CompilationPipeline.compilationStarted += HandleEditorCompilationStarted;
            CompilationPipeline.compilationFinished += HandleEditorCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
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
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            CaptureEditorEventSnapshot("editor-before-assembly-reload");
            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.assembly_reload",
                $"before isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "editor.playmode",
                $"state={state} isPlaying={Application.isPlaying} isPaused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling}");
        }

        private static void HandleEditorUpdate()
        {
            RuntimePerformanceProfiler instance = _instance;
            if (instance == null || !Application.isPlaying || !instance._debugProfilingActive)
                return;

            instance.EditorPumpSamplingFallback(EditorApplication.timeSinceStartup);
        }

        private static void CaptureEditorEventSnapshot(string reason)
        {
            RuntimePerformanceProfiler instance = _instance;
            if (instance == null || !instance._debugProfilingActive)
                return;

            instance.SampleRecorders();
            instance.CaptureSceneSnapshot(SceneManager.GetActiveScene().name, reason);
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

            float realtimeNow = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
            float silenceDuration = realtimeNow - _lastDriveRealtimeSinceStartup;
            if (silenceDuration < _editorFallbackPumpThresholdSeconds)
                return;

            if (RuntimeDiagnosticsTrace.IsActive && realtimeNow >= _nextEditorFallbackTraceTime)
            {
                _nextEditorFallbackTraceTime = realtimeNow + 2f;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "editor.fallback",
                    $"scene={SceneManager.GetActiveScene().name} frame={Time.frameCount} silence={silenceDuration:0.000} dt={editorDeltaTime:0.0000} compiling={EditorApplication.isCompiling} paused={EditorApplication.isPaused}");
            }

            DriveSampling(editorDeltaTime, true);
        }
#endif

        private void CaptureSceneSnapshot(string sceneName, string reason)
        {
            UpdateWorldDiagnostics();
            _reportBuilder.Clear();
            _reportBuilder.Append("scene=")
                .Append(string.IsNullOrWhiteSpace(sceneName) ? "Unknown" : sceneName)
                .Append(" reason=")
                .Append(string.IsNullOrWhiteSpace(reason) ? "runtime" : reason)
                .Append(" frame=")
                .Append(_debugLastFrameTimeMs.ToString("0.00"))
                .Append("ms main=")
                .Append(_debugLastMainThreadMs.ToString("0.00"))
                .Append("ms gc=")
                .Append(_debugLastGcAllocBytes)
                .Append("B mem=")
                .Append(_debugLastSystemMemoryMb.ToString("0.0"))
                .Append("MB setPass=")
                .Append(_debugLastSetPassCalls)
                .Append(" batches=")
                .Append(_debugLastBatches)
                .Append(" scatter=")
                .Append(_debugLastScatterTotalMs.ToString("0.00"))
                .Append("ms scatterSampling=")
                .Append(_debugLastScatterSamplingMs.ToString("0.00"))
                .Append("ms scatterRescue=")
                .Append(_debugLastScatterRescueMs.ToString("0.00"))
                .Append("ms scatterRestore=")
                .Append(_debugLastScatterRestoreMs.ToString("0.00"))
                .Append("ms scatterReconcile=")
                .Append(_debugLastScatterReconcileMs.ToString("0.00"))
                .Append("ms shadowSchedule=")
                .Append(_debugLastScatterShadowScheduleMs.ToString("0.00"))
                .Append("ms shadowPump=")
                .Append(_debugLastScatterShadowPumpMs.ToString("0.00"))
                .Append("ms");

            if (_hasScatterSnapshot)
            {
                _reportBuilder.Append(" reasonLabel=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Reason) ? "None" : _lastScatterSnapshot.Reason)
                    .Append(" zone=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Zone) ? "None" : _lastScatterSnapshot.Zone)
                    .Append(" biome=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Biome) ? "None" : _lastScatterSnapshot.Biome)
                    .Append(" pattern=")
                    .Append(string.IsNullOrWhiteSpace(_lastScatterSnapshot.Pattern) ? "None" : _lastScatterSnapshot.Pattern);
            }

            _debugLastSceneSnapshot = _reportBuilder.ToString();
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
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
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
                _nextOwnershipAuditAllowedTime = Time.unscaledTime + rendererOwnershipAuditCooldownSeconds;

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
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

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                totalRenderers++;

                WorldGenerativeGeologyVoxelRuntime voxelRuntime = renderer.GetComponentInParent<WorldGenerativeGeologyVoxelRuntime>();
                if (voxelRuntime != null)
                {
                    voxelRenderers++;
                    IncrementAuditCount(_auditVoxelFamilies, voxelRuntime.FamilyId);
                    continue;
                }

                WorldGenerativeGeologyBinding binding = renderer.GetComponentInParent<WorldGenerativeGeologyBinding>();
                if (binding != null)
                {
                    geologyRenderers++;
                    IncrementAuditCount(_auditGeologyFamilies, binding.FamilyId);
                    continue;
                }

                WorldProceduralProxyInstance proxy = renderer.GetComponentInParent<WorldProceduralProxyInstance>();
                if (proxy != null)
                {
                    scatterRenderers++;
                    IncrementAuditCount(_auditScatterFamilies, proxy.FamilyId);
                    continue;
                }

                WorldContentSocket socket = renderer.GetComponentInParent<WorldContentSocket>();
                if (socket != null)
                {
                    socketRenderers++;
                    IncrementAuditCount(_auditSocketKinds, socket.Kind.ToString());
                    continue;
                }

                if (renderer.GetComponentInParent<WorldZoneAnchor>() != null)
                {
                    zoneRenderers++;
                    continue;
                }

                otherRenderers++;
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
                .Append(otherRenderers);
            AppendTopAuditEntries(_auditBuilder, "scatterTop", _auditScatterFamilies);
            AppendTopAuditEntries(_auditBuilder, "geologyTop", _auditGeologyFamilies);
            AppendTopAuditEntries(_auditBuilder, "voxelTop", _auditVoxelFamilies);
            AppendTopAuditEntries(_auditBuilder, "socketTop", _auditSocketKinds);

            _debugLastOwnershipAudit = _auditBuilder.ToString();
            RuntimeDiagnosticsTrace.WriteEvent("render.audit", _debugLastOwnershipAudit);
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

            foreach (KeyValuePair<string, int> pair in counts)
            {
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
}

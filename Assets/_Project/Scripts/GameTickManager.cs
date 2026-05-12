// ============================================================================
// HECTON-8 — GameTickManager.cs
// Edinstvennyy MonoBehaviour s Update/FixedUpdate v proekte.
//
// VSE igrovye sistemy registriruyutsya zdes cherez interfeysy ITickable,
// IFixedTickable, ISlowTickable. Menedzher vyzyvaet ih tsentralizovanno.
//
// PREIMUSchESTVA PERED NATIVE Update():
// ┌────────────────────────────┬──────────────────────────────────┐
// │ Unity Native               │ GameTickManager                   │
// ├────────────────────────────┼──────────────────────────────────┤
// │ Reflection-based dispatch  │ Pryamoy vyzov cherez interfeys     │
// │ ~500 ns per call overhead  │ ~5 ns per call                   │
// │ Net kontrolya poryadka       │ Poryadok registratsii              │
// │ Net vozmozhnosti pauzit    │ bool _isPaused — odin flag       │
// │ GC ot SendMessage v Editor │ Zero GC                          │
// └────────────────────────────┴──────────────────────────────────┘
//
// BEZOPASNOST ITERATsII:
//   Register/Unregister vo vremya Tick? → Buffered.
//   Izmeneniya primenyayutsya posle zaversheniya tekuschey iteratsii.
//   Nikakih InvalidOperationException, nikakih propuskov.
//
// HARDENING (Auto-Cleanup):
//   Unichtozhennye MonoBehaviour ("fake null") avtomaticheski udalyayutsya
//   iz spiskov vo vremya iteratsii. Pattern-matching `is UnityEngine.Object`
//   dlya reference type — zero boxing, zero GC.
//
// ZERO GC:
//   • Net foreach (ispolzuetsya for s indeksom).
//   • Net LINQ.
//   • Net allokatsiy v goryachih putyah.
//   • WaitForSeconds dlya SlowTick keshirovan odin raz.
//   • Swap-remove vmesto List.Remove (bez sdviga massiva).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hecton.Localization;
using Hecton8.Dev;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)] // Tikaet RANShE vseh
    public sealed class GameTickManager : MonoBehaviour, IUpdatable, IFixedTickable, IServiceHeartbeat, IServiceShutdown
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static bool _isShuttingDown;
        private static bool _isEditorExitingPlayMode;

        internal static GameTickManager ActiveRuntimeInstance { get; private set; }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _isShuttingDown = false;
            _isEditorExitingPlayMode = false;
            ActiveRuntimeInstance = null;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorPlayModeHooks()
        {
            if (Application.isBatchMode)
                return;

            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (Application.isBatchMode)
                return;

            switch (state)
            {
                case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                    _isShuttingDown = true;
                    _isEditorExitingPlayMode = true;
                    break;
                case UnityEditor.PlayModeStateChange.EnteredEditMode:
                    _isEditorExitingPlayMode = false;
                    _isShuttingDown = false;
                    break;
                case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                    _isEditorExitingPlayMode = false;
                    break;
            }
        }
#endif

        internal float SlowTickIntervalSeconds => slowTickInterval;
        internal string DebugTopSlowTickOwner => _debugTopSlowTickOwner;
        internal string DebugLastSlowTickReport => _debugLastSlowTickReport;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("Slow Tick")]
        [Tooltip("Interval between SlowTick calls in seconds. 0.5 means 2 calls per second.")]
        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("Diagnostics (Read Only)")]
        [SerializeField] private int _debugTickCount;
        [SerializeField] private int _debugFixedCount;
        [SerializeField] private int _debugSlowCount;

        [Header("Slow Tick Profiling")]
        [SerializeField] private bool enableSlowTickProfiling = true;
        [SerializeField] private float slowTickSpikeThresholdMs = 8f;
        [SerializeField] private float slowTickReportCooldownSeconds = 1.5f;
        [SerializeField] private float _debugLastSlowTickDurationMs;
        [SerializeField] private float _debugTopSlowTickDurationMs;
        [SerializeField] private string _debugTopSlowTickOwner = "None";
        [SerializeField] private string _debugLastSlowTickReport = "None";

        // ══════════════════════════════════════════════════════════
        //  TICK LISTS — buferizovannye kollektsii
        // ══════════════════════════════════════════════════════════

        private TickList<ITickable>      _tickables;
        private TickList<IFixedTickable> _fixedTickables;
        private TickList<ISlowTickable>  _slowTickables;

        // ══════════════════════════════════════════════════════════
        //  SLOW TICK STATE
        // ══════════════════════════════════════════════════════════

        private float _slowTickAccumulator;
        private const int SlowTickProfilerCapacity = 8;
        private const double SlowTickPerformanceWarningBudgetMs = 0.2d;
        private static readonly uint _SlowTickBudgetWarningHash = unchecked((uint)LocHash.Compute("GameTickManager.SlowTickBudgetExceeded"));
        private static readonly uint _GameTickManagerContextHash = unchecked((uint)LocHash.Compute(nameof(GameTickManager)));
        private readonly object[] _slowTickTopOwners = new object[SlowTickProfilerCapacity];
        private readonly double[] _slowTickTopDurationsMs = new double[SlowTickProfilerCapacity];
        private float _nextSlowTickTelemetryTime;
        private bool _loggedFirstUpdateExecution;
        private bool _loggedFirstSlowTickExecution;
        private bool _serviceRegistered;
        private bool _registeredToDispatcher;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ResetSlowTickState();

            if (_serviceRegistered &&
                Application.isPlaying &&
                GlobalRegistry.Dispatcher != null &&
                !_registeredToDispatcher)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Core);
                _registeredToDispatcher = GlobalRegistry.Updatables.Contains(this) ||
                                          GlobalRegistry.FixedTickables.Contains(this);
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableSlowTickProfiling && ShouldLogUnexpectedDisable())
            {
                UnityEngine.Debug.Log(
                    $"[GameTickManager] disabled tickables={_tickables?.Count ?? -1} slowTickables={_slowTickables?.Count ?? -1}",
                    this);
            }
#endif
            UnregisterDispatcherLanes();
            ResetSlowTickState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState(clearTickLists: true);
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState(clearTickLists: true);
        }

        private void ShutdownServiceState(bool clearTickLists)
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableSlowTickProfiling && ShouldLogUnexpectedDisable())
            {
                UnityEngine.Debug.Log(
                    $"[GameTickManager] destroyed isInstance={ReferenceEquals(GlobalRegistry.TickManager, this)} tickables={_tickables?.Count ?? -1} slowTickables={_slowTickables?.Count ?? -1}",
                    this);
            }
#endif
            UnregisterDispatcherLanes();

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.TickManager, this))
                GlobalRegistry.UnregisterTickManager(this);

            _serviceRegistered = false;
            ResetSlowTickState();

            if (clearTickLists)
                ClearTickLists();
        }

        private void UnregisterDispatcherLanes()
        {
            if (!_registeredToDispatcher)
                return;

            if (GlobalRegistry.Updatables.Contains(this))
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);

            if (GlobalRegistry.FixedTickables.Contains(this))
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Core);

            _registeredToDispatcher = false;
        }

        public void InitializeService()
        {
            EnsureInitialized();

            if (!_serviceRegistered)
            {
                GlobalRegistry.RegisterTickManager(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.TickManager, this);
            }

            if (isActiveAndEnabled &&
                !_registeredToDispatcher &&
                Application.isPlaying &&
                GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Core);
                _registeredToDispatcher = GlobalRegistry.Updatables.Contains(this) ||
                                          GlobalRegistry.FixedTickables.Contains(this);
            }
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void ClearTickLists()
        {
            _tickables?.Clear();
            _fixedTickables?.Clear();
            _slowTickables?.Clear();
            Array.Clear(_slowTickTopOwners, 0, _slowTickTopOwners.Length);
            Array.Clear(_slowTickTopDurationsMs, 0, _slowTickTopDurationsMs.Length);
            _debugTickCount = 0;
            _debugFixedCount = 0;
            _debugSlowCount = 0;
            _debugLastSlowTickDurationMs = 0f;
            _debugTopSlowTickDurationMs = 0f;
            _debugTopSlowTickOwner = "None";
            _debugLastSlowTickReport = "None";
            _loggedFirstUpdateExecution = false;
            _loggedFirstSlowTickExecution = false;
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE — edinstvennyy v proekte
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            EnsureInitialized();

            float dt = deltaTime;
            float slowTickDt = dt;
            if (Application.isPlaying &&
                BootstrapState.HasActiveInstance &&
                !BootstrapState.IsGameReady &&
                slowTickDt <= 0f)
            {
                slowTickDt = Time.unscaledDeltaTime;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedFirstUpdateExecution && enableSlowTickProfiling)
                _loggedFirstUpdateExecution = true;
#endif

            _tickables.BeginIteration();

            List<ITickable> items = _tickables.Items;
            int count = items.Count;

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    _tickables.Remove(item);
                    continue;
                }

                // ── Auto-Cleanup: "fake null" (unichtozhennyy MonoBehaviour) ──
                if (item is UnityEngine.Object obj && obj == null)
                {
                    _tickables.Remove(item);
                    continue;
                }

                item.Tick(dt);
            }

            _tickables.EndIteration();
            ProcessSlowTickIfNeeded(slowTickDt);

#if UNITY_EDITOR
            _debugTickCount = _tickables.Count;
#endif
        }

        private static bool ShouldLogUnexpectedDisable()
        {
            if (_isShuttingDown || _isEditorExitingPlayMode)
                return false;

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
#endif

            return Application.isPlaying;
        }

        // ══════════════════════════════════════════════════════════
        //  FIXED UPDATE — edinstvennyy v proekte
        // ══════════════════════════════════════════════════════════

        public void FixedTick(float fixedDeltaTime)
        {
            EnsureInitialized();
#if UNITY_EDITOR
            FixedUpdateHeapLockGuard.Begin();
            try
            {
#endif

            float fdt = fixedDeltaTime;

            _fixedTickables.BeginIteration();

            List<IFixedTickable> items = _fixedTickables.Items;
            int count = items.Count;

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    _fixedTickables.Remove(item);
                    continue;
                }

                // ── Auto-Cleanup: "fake null" (unichtozhennyy MonoBehaviour) ──
                if (item is UnityEngine.Object obj && obj == null)
                {
                    _fixedTickables.Remove(item);
                    continue;
                }

                item.FixedTick(fdt);
            }

            _fixedTickables.EndIteration();

#if UNITY_EDITOR
            _debugFixedCount = _fixedTickables.Count;
#endif
#if UNITY_EDITOR
            }
            finally
            {
                FixedUpdateHeapLockGuard.End();
            }
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SLOW TICK — accumulator-driven loop (2 raza v sekundu po defoltu)
        // ══════════════════════════════════════════════════════════

        private void ResetSlowTickState()
        {
            _slowTickAccumulator = 0f;
        }

        private void ProcessSlowTickIfNeeded(float deltaTime)
        {
            float interval = slowTickInterval;
            if (interval <= 0f)
                interval = 0.5f;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < interval)
                return;

            _slowTickAccumulator = 0f;
            ExecuteSlowTick();
        }

        private void ExecuteSlowTick()
        {
            _slowTickables.BeginIteration();

            List<ISlowTickable> items = _slowTickables.Items;
            int count = items.Count;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedFirstSlowTickExecution && enableSlowTickProfiling)
                _loggedFirstSlowTickExecution = true;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool profileSlowTick = enableSlowTickProfiling;
#else
            const bool profileSlowTick = false;
#endif
            long loopStartTimestamp = profileSlowTick ? Stopwatch.GetTimestamp() : 0L;
            if (profileSlowTick)
                ResetSlowTickProfilerFrame();

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    _slowTickables.Remove(item);
                    continue;
                }

                if (item is UnityEngine.Object obj && obj == null)
                {
                    _slowTickables.Remove(item);
                    continue;
                }

                long itemStartTimestamp = profileSlowTick ? Stopwatch.GetTimestamp() : 0L;
                item.SlowTick();
                if (profileSlowTick)
                {
                    long itemEndTimestamp = Stopwatch.GetTimestamp();
                    RecordSlowTickSample(item, itemEndTimestamp - itemStartTimestamp);
                }
            }

            _slowTickables.EndIteration();

            if (profileSlowTick)
            {
                long loopEndTimestamp = Stopwatch.GetTimestamp();
                CommitSlowTickProfilerFrame(loopEndTimestamp - loopStartTimestamp, count);
            }

#if UNITY_EDITOR
            _debugSlowCount = _slowTickables.Count;
#endif
        }

        /// <summary>
        /// Vechnaya korutina. WaitForSeconds keshirovan — zero GC per yield.
        /// </summary>
        #if false
        private object SlowTickRoutineDisabled()
        {
            // Pervyy yield — chtoby vse sistemy uspeli zaregistrirovatsya
            return null;

            while (true)
            {
                yield break;

                _slowTickables.BeginIteration();

                List<ISlowTickable> items = _slowTickables.Items;
                int count = items.Count;
                long loopStartTimestamp = enableSlowTickProfiling ? Stopwatch.GetTimestamp() : 0L;
                if (enableSlowTickProfiling)
                    ResetSlowTickProfilerFrame();

                for (int i = 0; i < count; i++)
                {
                    var item = items[i];

                    if (item == null)
                    {
                        _slowTickables.Remove(item);
                        continue;
                    }

                    // ── Auto-Cleanup: "fake null" (unichtozhennyy MonoBehaviour) ──
                    if (item is UnityEngine.Object obj && obj == null)
                    {
                        _slowTickables.Remove(item);
                        continue;
                    }

                    long itemStartTimestamp = enableSlowTickProfiling ? Stopwatch.GetTimestamp() : 0L;
                    item.SlowTick();
                    if (enableSlowTickProfiling)
                    {
                        long itemEndTimestamp = Stopwatch.GetTimestamp();
                        RecordSlowTickSample(item, itemEndTimestamp - itemStartTimestamp);
                    }
                }

                _slowTickables.EndIteration();

                if (enableSlowTickProfiling)
                {
                    long loopEndTimestamp = Stopwatch.GetTimestamp();
                    CommitSlowTickProfilerFrame(loopEndTimestamp - loopStartTimestamp, count);
                }

#if UNITY_EDITOR
                _debugSlowCount = _slowTickables.Count;
#endif
            }
        }

        #endif
        private void ResetSlowTickProfilerFrame()
        {
            for (int i = 0; i < SlowTickProfilerCapacity; i++)
            {
                _slowTickTopOwners[i] = null;
                _slowTickTopDurationsMs[i] = 0d;
            }
        }

        private void RecordSlowTickSample(object owner, long elapsedTicks)
        {
            double elapsedMs = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            for (int i = 0; i < SlowTickProfilerCapacity; i++)
            {
                if (elapsedMs <= _slowTickTopDurationsMs[i])
                    continue;

                for (int shift = SlowTickProfilerCapacity - 1; shift > i; shift--)
                {
                    _slowTickTopOwners[shift] = _slowTickTopOwners[shift - 1];
                    _slowTickTopDurationsMs[shift] = _slowTickTopDurationsMs[shift - 1];
                }

                _slowTickTopOwners[i] = owner;
                _slowTickTopDurationsMs[i] = elapsedMs;
                return;
            }
        }

        private void CommitSlowTickProfilerFrame(long elapsedTicks, int registeredCount)
        {
            double totalMs = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            _debugLastSlowTickDurationMs = (float)totalMs;

            if (_slowTickTopOwners[0] != null)
            {
                _debugTopSlowTickDurationMs = (float)_slowTickTopDurationsMs[0];
                _debugTopSlowTickOwner = "Recorded";
            }
            else
            {
                _debugTopSlowTickDurationMs = 0f;
                _debugTopSlowTickOwner = "None";
            }

            float configuredThresholdMs = Mathf.Max(0.01f, slowTickSpikeThresholdMs);
            double warningThresholdMs = configuredThresholdMs < SlowTickPerformanceWarningBudgetMs
                ? configuredThresholdMs
                : SlowTickPerformanceWarningBudgetMs;
            if (totalMs <= warningThresholdMs)
                return;

            if (Time.unscaledTime < _nextSlowTickTelemetryTime)
                return;

            _debugLastSlowTickReport = "Telemetry";
            _nextSlowTickTelemetryTime = Time.unscaledTime + Mathf.Max(0.1f, slowTickReportCooldownSeconds);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _SlowTickBudgetWarningHash,
                _GameTickManagerContextHash,
                (float)totalMs);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — TYPED REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>Registriruet ITickable (kazhdyy kadr).</summary>
        public void Register(ITickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _tickables.Add(tickable);
        }

        /// <summary>Snimaet ITickable s obnovleniya.</summary>
        public void Unregister(ITickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _tickables.Remove(tickable);
        }

        /// <summary>Registriruet IFixedTickable (fizicheskiy shag).</summary>
        public void Register(IFixedTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _fixedTickables.Add(tickable);
        }

        /// <summary>Snimaet IFixedTickable s obnovleniya.</summary>
        public void Unregister(IFixedTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _fixedTickables.Remove(tickable);
        }

        /// <summary>Registriruet ISlowTickable (medlennyy tik).</summary>
        public void Register(ISlowTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _slowTickables.Add(tickable);
        }

        /// <summary>Snimaet ISlowTickable s obnovleniya.</summary>
        public void Unregister(ISlowTickable tickable)
        {
            EnsureInitialized();
            if (tickable != null) _slowTickables.Remove(tickable);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CONVENIENCE AUTO-DETECT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Avto-registratsiya: obekt proveryaetsya na vse interfeysy.
        /// Udobno dlya klassov, realizuyuschih neskolko interfeysov:
        ///   Registry-backed systems use explicit GlobalRegistry lane registration.
        ///
        /// Pattern-matching `is` dlya reference types — zero GC.
        /// </summary>
        public void RegisterAll(object target)
        {
            EnsureInitialized();
            if (target == null) return;

            if (target is ITickable t)
                _tickables.Add(t);

            if (target is IFixedTickable ft)
                _fixedTickables.Add(ft);

            if (target is ISlowTickable st)
                _slowTickables.Add(st);
        }

        /// <summary>
        /// Avto-otpiska: snimaet obekt so vseh spiskov.
        /// </summary>
        public void UnregisterAll(object target)
        {
            EnsureInitialized();
            if (target == null) return;

            if (target is ITickable t)
                _tickables.Remove(t);

            if (target is IFixedTickable ft)
                _fixedTickables.Remove(ft);

            if (target is ISlowTickable st)
                _slowTickables.Remove(st);
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        /// <summary>Kol-vo zaregistrirovannyh ITickable.</summary>
        public int TickableCount      => _tickables?.Count ?? 0;

        /// <summary>Kol-vo zaregistrirovannyh IFixedTickable.</summary>
        public int FixedTickableCount => _fixedTickables?.Count ?? 0;

        /// <summary>Kol-vo zaregistrirovannyh ISlowTickable.</summary>
        public int SlowTickableCount  => _slowTickables?.Count ?? 0;

        // ══════════════════════════════════════════════════════════
        //  TickList<T> — BUFERIZOVANNAYa KOLLEKTsIYa (nested class)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obertka nad List&lt;T&gt; s buferizatsiey Add/Remove.
        ///
        /// PROBLEMA: esli vyzvat List.Add ili List.Remove vo vremya
        /// iteratsii po tomu zhe spisku — InvalidOperationException
        /// ili propusk/dvoynaya obrabotka elementov.
        ///
        /// REShENIE: vo vremya iteratsii vse izmeneniya skladyvayutsya
        /// v bufery _toAdd / _toRemove. Posle zaversheniya iteratsii
        /// (EndIteration) bufery primenyayutsya k osnovnomu spisku.
        ///
        /// ZERO GC:
        ///   • Nikakih foreach — tolko for s indeksom.
        ///   • ReferenceEquals vmesto EqualityComparer (bez boxing).
        ///   • Swap-remove: O(1) udalenie bez sdviga massiva.
        ///   • Spiski pereispolzuyutsya — Clear() ne allotsiruet.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_tickables == null)
                _tickables = new TickList<ITickable>(128);

            if (_fixedTickables == null)
                _fixedTickables = new TickList<IFixedTickable>(64);

            if (_slowTickables == null)
                _slowTickables = new TickList<ISlowTickable>(32);

            if (slowTickInterval <= 0f)
                slowTickInterval = 0.5f;
        }

        private sealed class TickList<T> where T : class
        {
            // ── Osnovnoy spisok ──
            private readonly List<T> _items;

            // ── Bufery otlozhennyh operatsiy ──
            private readonly List<T> _toAdd;
            private readonly List<T> _toRemove;

            // ── Flag: seychas idet iteratsiya ──
            private bool _isIterating;

            // ─────────────────────────────────────────────────────
            //  CONSTRUCTOR
            // ─────────────────────────────────────────────────────

            public TickList(int initialCapacity)
            {
                _items    = new List<T>(initialCapacity);
                _toAdd    = new List<T>(16);
                _toRemove = new List<T>(16);
            }

            // ─────────────────────────────────────────────────────
            //  PUBLIC ACCESSORS
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Pryamoy dostup k vnutrennemu spisku dlya for-tsikla.
            /// TOLKO DLYa ChTENIYa vo vremya iteratsii!
            /// </summary>
            public List<T> Items => _items;

            /// <summary>Tekuschee kol-vo elementov.</summary>
            public int Count => _items.Count;

            public void Clear()
            {
                _isIterating = false;
                _items.Clear();
                _toAdd.Clear();
                _toRemove.Clear();
            }

            // ─────────────────────────────────────────────────────
            //  ADD / REMOVE — s buferizatsiey
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Dobavlyaet element. Esli iteratsiya — v bufer.
            /// Dublikaty ignoriruyutsya (proverka ReferenceEquals).
            /// </summary>
            public void Add(T item)
            {
                if (_isIterating)
                {
                    // Proverka: mozhet, on uzhe v bufere udaleniya?
                    // Esli da — otmenyaem udalenie vmesto dvoynogo dobavleniya.
                    if (ContainsRef(_toRemove, item))
                    {
                        RemoveRef(_toRemove, item);
                        return;
                    }

                    // Ne dobavlyaem dvazhdy
                    if (!ContainsRef(_items, item) && !ContainsRef(_toAdd, item))
                        _toAdd.Add(item);
                }
                else
                {
                    if (!ContainsRef(_items, item))
                        _items.Add(item);
                }
            }

            /// <summary>
            /// Udalyaet element. Esli iteratsiya — v bufer.
            /// Bezopasno pri otsutstvii elementa (no-op).
            /// </summary>
            public void Remove(T item)
            {
                if (_isIterating)
                {
                    // Mozhet, on esche ne dobavlen (v bufere dobavleniya)?
                    if (ContainsRef(_toAdd, item))
                    {
                        RemoveRef(_toAdd, item);
                        return;
                    }

                    if (ContainsRef(_items, item) && !ContainsRef(_toRemove, item))
                        _toRemove.Add(item);
                }
                else
                {
                    SwapRemove(_items, item);
                }
            }

            // ─────────────────────────────────────────────────────
            //  ITERATION GUARDS
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Vyzovi PERED for-tsiklom. Aktiviruet buferizatsiyu.
            /// </summary>
            public void BeginIteration()
            {
                _isIterating = true;
            }

            /// <summary>
            /// Vyzovi POSLE for-tsikla. Primenyaet bufery.
            /// </summary>
            public void EndIteration()
            {
                _isIterating = false;
                FlushPending();
            }

            // ─────────────────────────────────────────────────────
            //  PRIVATE — Flush Buffers
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Primenyaet vse otlozhennye dobavleniya i udaleniya.
            /// Poryadok: snachala udaleniya, potom dobavleniya.
            /// Clear() na List ne allotsiruet — obnulyaet Count, bufer ostaetsya.
            /// </summary>
            private void FlushPending()
            {
                // ── Udaleniya ──
                int removeCount = _toRemove.Count;
                if (removeCount > 0)
                {
                    for (int i = 0; i < removeCount; i++)
                        SwapRemove(_items, _toRemove[i]);

                    _toRemove.Clear();
                }

                // ── Dobavleniya ──
                int addCount = _toAdd.Count;
                if (addCount > 0)
                {
                    for (int i = 0; i < addCount; i++)
                    {
                        T item = _toAdd[i];
                        if (!ContainsRef(_items, item))
                            _items.Add(item);
                    }

                    _toAdd.Clear();
                }
            }

            // ─────────────────────────────────────────────────────
            //  PRIVATE — Zero-GC Collection Helpers
            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Proverka nalichiya po ssylke. Bez EqualityComparer,
            /// bez boxing, bez allokatsiy. O(n) — no spiski malenkie,
            /// i vyzyvaetsya tolko pri Register/Unregister (redko).
            /// </summary>
            private static bool ContainsRef(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Udalenie po ssylke iz nebuferizovannogo spiska.
            /// Ispolzuetsya dlya chistki buferov _toAdd / _toRemove.
            /// Obychnyy Remove (ne swap) — sohranyaet poryadok bufera.
            /// </summary>
            private static void RemoveRef(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                    {
                        list.RemoveAt(i);
                        return;
                    }
                }
            }

            /// <summary>
            /// Swap-Remove: menyaet element s poslednim, udalyaet posledniy.
            /// O(1) udalenie vmesto O(n) sdviga.
            ///
            /// ⚠ NE sohranyaet poryadok elementov.
            ///   Dlya sistem tikov poryadok obychno ne kritichen.
            ///   Esli poryadok vazhen — zamenite na list.RemoveAt(i).
            /// </summary>
            private static void SwapRemove(List<T> list, T item)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], item))
                    {
                        int last = count - 1;
                        list[i] = list[last];  // Swap (no-op esli i == last)
                        list.RemoveAt(last);   // O(1) — posledniy element
                        return;
                    }
                }
            }
        }

#if UNITY_EDITOR
        private static class FixedUpdateHeapLockGuard
        {
            private const string EditorPrefsKey = "Hecton8.FixedUpdateHeapLock.Enabled";
            private const string EnvironmentKey = "HECTON_HEAP_LOCK_GUARD";

            private static bool _stateResolved;
            private static bool _enabled;
            private static long _allocatedBytesAtBegin;
            private static int _depth;

            private static bool IsEnabled
            {
                get
                {
                    if (_stateResolved)
                        return _enabled;

                    _enabled =
                        UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false) ||
                        string.Equals(
                            System.Environment.GetEnvironmentVariable(EnvironmentKey),
                            "1",
                            StringComparison.Ordinal);
                    _stateResolved = true;
                    return _enabled;
                }
            }

            [UnityEditor.MenuItem("Tools/Hecton8/Compliance/Enable FixedUpdate Heap Lock")]
            private static void Enable()
            {
                UnityEditor.EditorPrefs.SetBool(EditorPrefsKey, true);
                _enabled = true;
                _stateResolved = true;
            }

            [UnityEditor.MenuItem("Tools/Hecton8/Compliance/Disable FixedUpdate Heap Lock")]
            private static void Disable()
            {
                UnityEditor.EditorPrefs.SetBool(EditorPrefsKey, false);
                _enabled = false;
                _stateResolved = true;
                _depth = 0;
            }

            internal static void Begin()
            {
                if (!IsEnabled)
                    return;

                if (_depth++ == 0)
                    _allocatedBytesAtBegin = GC.GetAllocatedBytesForCurrentThread();
            }

            internal static void End()
            {
                if (!IsEnabled || _depth <= 0)
                    return;

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _allocatedBytesAtBegin;
                _depth--;
                if (_depth == 0 && allocatedBytes > 0L)
                    throw new InvalidOperationException("[FixedUpdateHeapLockGuard] Fatal Error: managed GC allocation detected during FixedUpdate. bytes=" + allocatedBytes);
            }
        }
#endif
    }
}

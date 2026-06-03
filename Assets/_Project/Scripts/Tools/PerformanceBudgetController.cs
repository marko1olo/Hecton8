using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Tools
{
    /// <summary>
    /// Maintains separate performance budgets for different visual systems.
    /// Provides guardrail enforcement to prevent any single system from consuming too much CPU/GPU.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)] // After most systems but before rendering
    public sealed class PerformanceBudgetController : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        [Header("Budget Settings (Device-Class Envelope)")]
        [SerializeField, Tooltip("Target frame time budget in milliseconds (16.67ms = 60fps)")]
        private float _targetFrameTimeMs = 16.67f;

        [SerializeField, Tooltip("Maximum allowed frame time before throttling (25ms = 40fps)")]
        private float _maxFrameTimeMs = 25f;

        [Header("System Budgets (Percentage of total frame time)")]
        [Range(0f, 0.5f), SerializeField, Tooltip("Microfauna budget (0-50% of frame time)")]
        private float _microfaunaBudget = 0.15f; // 15%

        [Range(0f, 0.5f), SerializeField, Tooltip("Biolum budget (0-50% of frame time)")]
        private float _biolumBudget = 0.20f; // 20%

        [Range(0f, 0.5f), SerializeField, Tooltip("Terrain residency budget (0-50% of frame time)")]
        private float _terrainBudget = 0.25f; // 25%

        [Header("Throttle Settings")]
        [SerializeField, Tooltip("Enable automatic throttling when budget exceeded")]
        private bool _enableThrottling = true;

        [Range(0f, 1f), SerializeField, Tooltip("Minimum continuous performance scalar under full pressure")]
        private float _throttleMultiplier = 0.5f;

        [Range(0f, 1f), SerializeField, Tooltip("How strongly GlobalQualityWeight constrains managed systems")]
        private float _globalQualityInfluence = 1f;

        [SerializeField, Tooltip("Maximum performance scalar loss per second under pressure")]
        private float _performanceDropRate = 3f;

        [SerializeField, Tooltip("Maximum performance scalar recovery per second")]
        private float _performanceRecoverRate = 0.75f;

        [SerializeField, Tooltip("Frame-time deadband around the target before pressure changes")]
        private float _frameTimeHysteresisMs = 1.25f;

        [SerializeField, Tooltip("Frames to average for budget calculation")]
        private int _budgetAverageFrames = 10;

        private const int MaxTrackedBudgetSystems = 32;

        // COLD ALLOC: Dictionary<string,int>[32] - system-name to dense budget index map - owner: PerformanceBudgetController
        private readonly Dictionary<string, int> _systemBudgetIndices = new Dictionary<string, int>(MaxTrackedBudgetSystems);
        // COLD ALLOC: Dictionary<string,SystemBudgetInfo>[32] - legacy status snapshot cache for allocation-free GetBudgetStatus compatibility - owner: PerformanceBudgetController
        private readonly Dictionary<string, SystemBudgetInfo> _budgetStatusSnapshot = new Dictionary<string, SystemBudgetInfo>(MaxTrackedBudgetSystems);
        // COLD ALLOC: SystemBudget[32] - dense budget rows for cache-friendly Tick traversal - owner: PerformanceBudgetController
        private readonly SystemBudget[] _systemBudgets = new SystemBudget[MaxTrackedBudgetSystems];
        private int _systemBudgetCount;

        // Frame time tracking
        private float[] _recentFrameTimes;
        private int _recentFrameCursor;
        private int _recentFrameCount;
        private float _recentFrameSum;
        private float _currentFrameTimeAverage;
        private float _currentPerformanceLevel = 1f;
        private float _budgetPressure01;
        private bool _registeredToTickManager;
        private bool _hotSwapRegistered;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly StringBuilder _statusLogBuilder = new StringBuilder(256); // COLD ALLOC: reused development-only status builder
#endif

        private const float PerformanceApplyEpsilon = 0.0025f;
        private const float RestoredPerformanceThreshold = 0.995f;
        private const float MaxSmoothingDeltaSeconds = 0.1f;

        private void Awake()
        {
            EnsureFrameHistoryCapacity(true);
            InitializeBudgets();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
        }

        private void OnDestroy()
        {
            OnDisable();
            _systemBudgetIndices.Clear();
            _budgetStatusSnapshot.Clear();
            _systemBudgetCount = 0;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            _registeredToTickManager = false;
            if (currentService != null)
                TryRegisterUpdatable();
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredToTickManager = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void Tick(float dt)
        {
            float safeDeltaTime = SanitizeDeltaSeconds(dt);
            UpdateFrameTimeAverage(safeDeltaTime * 1000f);

            if (_enableThrottling)
            {
                UpdateContinuousPerformanceLevel(safeDeltaTime);
            }
            else
            {
                ApplyPerformanceLevel(1f);
            }
        }

        /// <summary>
        /// Registers a system for budget tracking.
        /// </summary>
        public void RegisterSystem(string systemName, IBudgetManagedSystem system)
        {
            if (string.IsNullOrWhiteSpace(systemName) || system == null)
            {
                LogInvalidRegistration(systemName);
                return;
            }

            if (_systemBudgetIndices.ContainsKey(systemName))
            {
                LogDuplicateRegistration(systemName);
                return;
            }

            if (_systemBudgetCount >= MaxTrackedBudgetSystems)
            {
                LogRegistrationCapacityExceeded(systemName);
                return;
            }

            float budgetMs = GetBudgetForSystem(systemName);
            SystemBudget budget = new SystemBudget
            {
                SystemName = systemName,
                System = system,
                BudgetMs = budgetMs,
                PerformanceLevel = _currentPerformanceLevel,
                IsThrottled = _currentPerformanceLevel < RestoredPerformanceThreshold ? (byte)1 : (byte)0
            };
            if (_currentPerformanceLevel < RestoredPerformanceThreshold)
                system.SetPerformanceLevel(_currentPerformanceLevel);

            int index = _systemBudgetCount;
            _systemBudgets[index] = budget;
            _systemBudgetIndices[systemName] = index;
            _systemBudgetCount = index + 1;
            UpdateBudgetStatusSnapshot(in budget);

            LogSystemRegistered(systemName, budgetMs);
        }

        /// <summary>
        /// Unregisters a system from budget tracking.
        /// </summary>
        public void UnregisterSystem(string systemName)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return;

            if (!_systemBudgetIndices.TryGetValue(systemName, out int index))
                return;

            RemoveBudgetAtIndex(index);
            LogSystemUnregistered(systemName);
        }

        /// <summary>
        /// Reports system performance for budget calculation.
        /// </summary>
        public void ReportSystemPerformance(string systemName, float timeUsedMs)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return;

            if (!_systemBudgetIndices.TryGetValue(systemName, out int index))
                return;

            SystemBudget budget = _systemBudgets[index];
            timeUsedMs = math.max(0f, timeUsedMs);
            budget.LastFrameTimeMs = timeUsedMs;
            budget.TotalTimeMs += timeUsedMs;
            budget.FrameCount++;

            // Check if system is over budget
            if (timeUsedMs > budget.BudgetMs)
                budget.OverBudgetCount++;

            _systemBudgets[index] = budget;
            UpdateBudgetStatusSnapshot(in budget);
        }

        /// <summary>
        /// Gets the current budget status for all systems.
        /// Returns an owner-reused snapshot; prefer CopyBudgetStatusNonAlloc for hot or retained reads.
        /// </summary>
        public IReadOnlyDictionary<string, SystemBudgetInfo> GetBudgetStatus()
        {
            return _budgetStatusSnapshot;
        }

        /// <summary>
        /// Copies budget status into caller-owned buffers. Returns copied row count.
        /// </summary>
        public int CopyBudgetStatusNonAlloc(SystemBudgetInfo[] statusDestination, string[] nameDestination = null)
        {
            if (statusDestination == null || statusDestination.Length == 0)
                return 0;

            int copyCount = math.min(_systemBudgetCount, statusDestination.Length);
            for (int i = 0; i < copyCount; i++)
            {
                SystemBudget budget = _systemBudgets[i];
                statusDestination[i] = CreateBudgetInfo(in budget);
                if (nameDestination != null && i < nameDestination.Length)
                    nameDestination[i] = budget.SystemName;
            }

            return copyCount;
        }

        /// <summary>
        /// Returns a compact human-readable status line.
        /// </summary>
        public string DescribeStatus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int throttledCount = CountThrottledSystems();
            int overBudgetCount = CountOverBudgetSystems();

            StringBuilder statusBuilder = _statusLogBuilder;
            statusBuilder.Clear();
            statusBuilder.Append("[PerformanceBudgetController] avgFrame=");
            AppendFixed2(statusBuilder, _currentFrameTimeAverage);
            statusBuilder.Append("ms target=");
            AppendFixed2(statusBuilder, _targetFrameTimeMs);
            statusBuilder.Append("ms max=");
            AppendFixed2(statusBuilder, _maxFrameTimeMs);
            statusBuilder.Append("ms systems=");
            statusBuilder.Append(_systemBudgetCount);
            statusBuilder.Append(" throttled=");
            statusBuilder.Append(throttledCount);
            statusBuilder.Append(" overBudget=");
            statusBuilder.Append(overBudgetCount);
            statusBuilder.Append(" level=");
            AppendPercent0(statusBuilder, _currentPerformanceLevel);
            statusBuilder.Append(" pressure=");
            AppendPercent0(statusBuilder, _budgetPressure01);

            if (_systemBudgetCount == 0)
            {
                statusBuilder.Append(" budgets=none");
                return statusBuilder.ToString();
            }

            statusBuilder.Append(" budgets:");
            int budgetCount = _systemBudgetCount;
            for (int i = 0; i < budgetCount; i++)
            {
                SystemBudget budget = _systemBudgets[i];
                float avgTime = budget.FrameCount > 0 ? budget.TotalTimeMs / budget.FrameCount : 0f;
                float budgetUsage = budget.BudgetMs > 0f ? avgTime / budget.BudgetMs : 0f;
                statusBuilder.Append(' ');
                statusBuilder.Append(budget.SystemName);
                statusBuilder.Append('=');
                AppendFixed2(statusBuilder, avgTime);
                statusBuilder.Append("ms/");
                AppendFixed2(statusBuilder, budget.BudgetMs);
                statusBuilder.Append("ms(");
                AppendPercent0(statusBuilder, budgetUsage);
                statusBuilder.Append(budget.IsThrottled != 0 ? ",reduced@" : ",ok@");
                AppendPercent0(statusBuilder, budget.PerformanceLevel);
                statusBuilder.Append(')');
            }

            return statusBuilder.ToString();
#else
            return "[PerformanceBudgetController] status disabled in release";
#endif
        }

        private void InitializeBudgets()
        {
            // Budgets are configured via inspector, but we could load from config here
        }

        private float GetBudgetForSystem(string systemName)
        {
            float totalBudget = _targetFrameTimeMs;

            if (string.Equals(systemName, "microfauna", StringComparison.OrdinalIgnoreCase))
                return totalBudget * _microfaunaBudget;

            if (string.Equals(systemName, "biolum", StringComparison.OrdinalIgnoreCase))
                return totalBudget * _biolumBudget;

            if (string.Equals(systemName, "terrain", StringComparison.OrdinalIgnoreCase))
                return totalBudget * _terrainBudget;

            float allocated = totalBudget * (_microfaunaBudget + _biolumBudget + _terrainBudget);
            return math.max(0f, totalBudget - allocated) * 0.5f;
        }

        private void UpdateContinuousPerformanceLevel(float deltaTime)
        {
            float targetLevel = ResolveTargetPerformanceLevel();
            float rate = targetLevel < _currentPerformanceLevel ? _performanceDropRate : _performanceRecoverRate;
            float maxDelta = math.max(0f, rate) * deltaTime;
            ApplyPerformanceLevel(MoveTowards(_currentPerformanceLevel, targetLevel, maxDelta));
        }

        private float ResolveTargetPerformanceLevel()
        {
            float floor = math.saturate(_throttleMultiplier);
            float pressureLevel = math.lerp(1f, floor, ResolveFramePressure01());
            float quality = ResolveGlobalQualityWeight01();
            float qualityLevel = math.lerp(floor, 1f, Smooth01(quality));
            float globalQualityLevel = math.lerp(1f, qualityLevel, math.saturate(_globalQualityInfluence));
            return math.clamp(math.min(pressureLevel, globalQualityLevel), floor, 1f);
        }

        private float ResolveFramePressure01()
        {
            float safeAverage = math.isfinite(_currentFrameTimeAverage) ? math.max(0f, _currentFrameTimeAverage) : _maxFrameTimeMs;
            float upperBand = _targetFrameTimeMs + _frameTimeHysteresisMs;
            float lowerBand = math.max(0f, _targetFrameTimeMs - _frameTimeHysteresisMs);
            float pressureRange = math.max(0.001f, _maxFrameTimeMs - upperBand);

            if (safeAverage > upperBand)
            {
                _budgetPressure01 = Smooth01(math.saturate((safeAverage - upperBand) / pressureRange));
            }
            else if (safeAverage < lowerBand)
            {
                _budgetPressure01 = 0f;
            }

            return _budgetPressure01;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = SignalBusRegistry.GlobalQualityWeight01;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private void ApplyPerformanceLevel(float performanceLevel)
        {
            float safeLevel = math.saturate(math.isfinite(performanceLevel) ? performanceLevel : 1f);
            if (math.abs(safeLevel - _currentPerformanceLevel) <= PerformanceApplyEpsilon)
                return;

            _currentPerformanceLevel = safeLevel;
            byte reduced = safeLevel < RestoredPerformanceThreshold ? (byte)1 : (byte)0;

            int budgetCount = _systemBudgetCount;
            for (int i = 0; i < budgetCount; i++)
            {
                SystemBudget budget = _systemBudgets[i];
                if (math.abs(budget.PerformanceLevel - safeLevel) > PerformanceApplyEpsilon)
                {
                    budget.System?.SetPerformanceLevel(safeLevel);
                    budget.PerformanceLevel = safeLevel;
                }

                budget.IsThrottled = reduced;
                _systemBudgets[i] = budget;
                UpdateBudgetStatusSnapshot(in budget);
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            current = math.saturate(math.isfinite(current) ? current : 1f);
            target = math.saturate(math.isfinite(target) ? target : 1f);
            maxDelta = math.max(0f, math.isfinite(maxDelta) ? maxDelta : 0f);
            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;

            return delta > 0f ? current + maxDelta : current - maxDelta;
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 0f);
            return t * t * (3f - 2f * t);
        }

        private static float SanitizeDeltaSeconds(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
                return 0f;

            return math.min(deltaTime, MaxSmoothingDeltaSeconds);
        }

        private void RemoveBudgetAtIndex(int index)
        {
            if ((uint)index >= (uint)_systemBudgetCount)
                return;

            string removedName = _systemBudgets[index].SystemName;
            int lastIndex = _systemBudgetCount - 1;
            if (index != lastIndex)
            {
                SystemBudget moved = _systemBudgets[lastIndex];
                _systemBudgets[index] = moved;
                if (!string.IsNullOrEmpty(moved.SystemName))
                    _systemBudgetIndices[moved.SystemName] = index;
            }

            _systemBudgets[lastIndex] = default;
            _systemBudgetCount = lastIndex;
            if (!string.IsNullOrEmpty(removedName))
            {
                _systemBudgetIndices.Remove(removedName);
                _budgetStatusSnapshot.Remove(removedName);
            }
        }

        private void UpdateBudgetStatusSnapshot(in SystemBudget budget)
        {
            if (string.IsNullOrEmpty(budget.SystemName))
                return;

            _budgetStatusSnapshot[budget.SystemName] = CreateBudgetInfo(in budget);
        }

        private int CountThrottledSystems()
        {
            int count = 0;
            int budgetCount = _systemBudgetCount;
            for (int i = 0; i < budgetCount; i++)
            {
                if (_systemBudgets[i].IsThrottled != 0)
                    count++;
            }

            return count;
        }

        private static void AppendFixed2(StringBuilder builder, float value)
        {
            if (!math.isfinite(value))
            {
                builder.Append("0.00");
                return;
            }

            if (value < 0f)
            {
                builder.Append('-');
                value = -value;
            }

            int scaled = (int)math.round(value * 100f);
            int whole = scaled / 100;
            int fraction = scaled - whole * 100;
            builder.Append(whole);
            builder.Append('.');
            if (fraction < 10)
                builder.Append('0');
            builder.Append(fraction);
        }

        private static void AppendPercent0(StringBuilder builder, float normalizedValue)
        {
            float clamped = math.isfinite(normalizedValue) ? math.max(0f, normalizedValue) : 0f;
            int percent = (int)math.round(clamped * 100f);
            builder.Append(percent);
            builder.Append('%');
        }

        private int CountOverBudgetSystems()
        {
            int count = 0;
            int budgetCount = _systemBudgetCount;
            for (int i = 0; i < budgetCount; i++)
            {
                if (_systemBudgets[i].OverBudgetCount > 0)
                    count++;
            }

            return count;
        }

        private static SystemBudgetInfo CreateBudgetInfo(in SystemBudget budget)
        {
            float avgTime = budget.FrameCount > 0 ? budget.TotalTimeMs / budget.FrameCount : 0f;
            float budgetUsage = budget.BudgetMs > 0f ? avgTime / budget.BudgetMs : 0f;
            return new SystemBudgetInfo
            {
                BudgetMs = budget.BudgetMs,
                AverageTimeMs = avgTime,
                BudgetUsage = budgetUsage,
                PerformanceLevel = budget.PerformanceLevel,
                IsThrottled = budget.IsThrottled,
                OverBudgetCount = budget.OverBudgetCount
            };
        }

        private void UpdateFrameTimeAverage(float frameTimeMs)
        {
            EnsureFrameHistoryCapacity(false);
            if (_recentFrameTimes == null || _recentFrameTimes.Length == 0)
            {
                _currentFrameTimeAverage = frameTimeMs;
                return;
            }

            if (_recentFrameCount < _recentFrameTimes.Length)
            {
                _recentFrameTimes[_recentFrameCount] = frameTimeMs;
                _recentFrameSum += frameTimeMs;
                _recentFrameCount++;
            }
            else
            {
                _recentFrameSum -= _recentFrameTimes[_recentFrameCursor];
                _recentFrameTimes[_recentFrameCursor] = frameTimeMs;
                _recentFrameSum += frameTimeMs;
                _recentFrameCursor++;
                if (_recentFrameCursor >= _recentFrameTimes.Length)
                    _recentFrameCursor = 0;
            }

            _currentFrameTimeAverage = _recentFrameCount > 0
                ? _recentFrameSum / _recentFrameCount
                : 0f;
        }

        private void EnsureFrameHistoryCapacity(bool allowAllocation)
        {
            int requiredCapacity = math.max(1, _budgetAverageFrames);
            if (_recentFrameTimes != null && _recentFrameTimes.Length == requiredCapacity)
                return;

            if (!allowAllocation)
                return;

            _recentFrameTimes = new float[requiredCapacity]; // COLD ALLOC: bounded ring buffer for frame-time averaging
            _recentFrameCursor = 0;
            _recentFrameCount = 0;
            _recentFrameSum = 0f;
            _currentFrameTimeAverage = 0f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogDuplicateRegistration(string systemName)
        {
            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' already registered");
        }

        private void LogInvalidRegistration(string systemName)
        {
            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] Ignoring invalid registration '{systemName}'");
        }

        private void LogRegistrationCapacityExceeded(string systemName)
        {
            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] Ignoring registration '{systemName}' because budget capacity {MaxTrackedBudgetSystems} is full");
        }

        private void LogSystemRegistered(string systemName, float budgetMs)
        {
            Hecton8.Core.H8Debug.Log("[PerformanceBudgetController] Registered system '" + systemName + "' with " + budgetMs.ToString("F2", CultureInfo.InvariantCulture) + "ms budget");
        }

        private void LogSystemUnregistered(string systemName)
        {
            Hecton8.Core.H8Debug.Log($"[PerformanceBudgetController] Unregistered system '{systemName}'");
        }

#else
        private void LogDuplicateRegistration(string systemName) { }
        private void LogInvalidRegistration(string systemName) { }
        private void LogRegistrationCapacityExceeded(string systemName) { }
        private void LogSystemRegistered(string systemName, float budgetMs) { }
        private void LogSystemUnregistered(string systemName) { }
#endif

        private void OnValidate()
        {
            if (_budgetAverageFrames < 1)
                _budgetAverageFrames = 1;

            if (_maxFrameTimeMs < _targetFrameTimeMs + 0.1f)
                _maxFrameTimeMs = _targetFrameTimeMs + 0.1f;

            _throttleMultiplier = math.saturate(_throttleMultiplier);
            _globalQualityInfluence = math.saturate(_globalQualityInfluence);
            _performanceDropRate = math.max(0f, _performanceDropRate);
            _performanceRecoverRate = math.max(0f, _performanceRecoverRate);
            _frameTimeHysteresisMs = math.max(0f, _frameTimeHysteresisMs);
        }
    }

    /// <summary>
    /// Interface for systems that can be managed by the performance budget controller.
    /// </summary>
    public interface IBudgetManagedSystem
    {
        /// <summary>
        /// Sets the performance level (0-1, where 1 = full performance).
        /// </summary>
        void SetPerformanceLevel(float level);
    }

    /// <summary>
    /// Internal budget tracking for a system.
    /// </summary>
    public struct SystemBudget
    {
        public string SystemName;
        public IBudgetManagedSystem System;
        public float BudgetMs;
        public float LastFrameTimeMs;
        public float TotalTimeMs;
        public float PerformanceLevel;
        public int FrameCount;
        public int OverBudgetCount;
        public byte IsThrottled;
    }

    /// <summary>
    /// Public budget status information.
    /// </summary>
    [Serializable]
    public struct SystemBudgetInfo
    {
        public float BudgetMs;
        public float AverageTimeMs;
        public float BudgetUsage; // 0-1 (1 = at budget limit)
        public float PerformanceLevel;
        public byte IsThrottled;
        public int OverBudgetCount;
    }
}

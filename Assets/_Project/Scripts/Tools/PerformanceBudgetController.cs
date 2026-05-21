using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hecton8.Core;
using Unity.Mathematics;

namespace Hecton8.Tools
{
    /// <summary>
    /// Maintains separate performance budgets for different visual systems.
    /// Provides guardrail enforcement to prevent any single system from consuming too much CPU/GPU.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)] // After most systems but before rendering
    public sealed class PerformanceBudgetController : MonoBehaviour, ITickable, IUpdatable
    {
        [Header("Budget Settings (Target MX350)")]
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

        [SerializeField, Tooltip("Throttle multiplier when over budget (0.5 = half performance)")]
        private float _throttleMultiplier = 0.5f;

        [SerializeField, Tooltip("Frames to average for budget calculation")]
        private int _budgetAverageFrames = 10;

        private const int MaxTrackedBudgetSystems = 32;

        // COLD ALLOC: Dictionary<string,int>[32] - system-name to dense budget index map - owner: PerformanceBudgetController
        private readonly Dictionary<string, int> _systemBudgetIndices = new Dictionary<string, int>(MaxTrackedBudgetSystems);
        // COLD ALLOC: SystemBudget[32] - dense budget rows for cache-friendly Tick traversal - owner: PerformanceBudgetController
        private readonly SystemBudget[] _systemBudgets = new SystemBudget[MaxTrackedBudgetSystems];
        private int _systemBudgetCount;

        // Frame time tracking
        private float[] _recentFrameTimes;
        private int _recentFrameCursor;
        private int _recentFrameCount;
        private float _recentFrameSum;
        private float _currentFrameTimeAverage;
        private float _nextBudgetStatusLogTime;
        private bool _registeredToTickManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly StringBuilder _statusLogBuilder = new StringBuilder(256); // COLD ALLOC: reused development-only status builder
#endif

        private const float BudgetStatusLogIntervalSeconds = 5f;
        private const float OverBudgetLogIntervalSeconds = 5f;

        private void Awake()
        {
            EnsureFrameHistoryCapacity(true);
            InitializeBudgets();
        }

        private void OnEnable()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void OnDisable()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredToTickManager = false;
        }

        private void OnDestroy()
        {
            OnDisable();
            _systemBudgetIndices.Clear();
            _systemBudgetCount = 0;
        }

        public void Tick(float dt)
        {
            UpdateFrameTimeAverage(math.max(0f, dt) * 1000f);

            // Check budgets and apply throttling if needed
            if (_enableThrottling)
            {
                CheckAndApplyThrottling();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log budget status periodically
            if (Time.unscaledTime >= _nextBudgetStatusLogTime)
            {
                LogBudgetStatus();
                _nextBudgetStatusLogTime = Time.unscaledTime + BudgetStatusLogIntervalSeconds;
            }
#endif
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
                IsThrottled = 0
            };
            int index = _systemBudgetCount;
            _systemBudgets[index] = budget;
            _systemBudgetIndices[systemName] = index;
            _systemBudgetCount = index + 1;

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
            {
                budget.OverBudgetCount++;
                if (Time.unscaledTime >= budget.NextOverBudgetLogTime)
                {
                    budget.NextOverBudgetLogTime = Time.unscaledTime + OverBudgetLogIntervalSeconds;
                    LogSystemOverBudget(systemName, timeUsedMs, budget.BudgetMs);
                }
            }

            _systemBudgets[index] = budget;
        }

        /// <summary>
        /// Gets the current budget status for all systems.
        /// </summary>
        public Dictionary<string, SystemBudgetInfo> GetBudgetStatus()
        {
            var status = new Dictionary<string, SystemBudgetInfo>(_systemBudgetCount);
            int budgetCount = _systemBudgetCount;
            for (int i = 0; i < budgetCount; i++)
            {
                SystemBudget budget = _systemBudgets[i];

                status[budget.SystemName] = CreateBudgetInfo(in budget);
            }

            return status;
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
                statusBuilder.Append(budget.IsThrottled != 0 ? ",throttled" : ",ok");
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

        private void CheckAndApplyThrottling()
        {
            if (_currentFrameTimeAverage > _maxFrameTimeMs)
            {
                // Frame time is too high, throttle systems
                int budgetCount = _systemBudgetCount;
                for (int i = 0; i < budgetCount; i++)
                {
                    SystemBudget budget = _systemBudgets[i];
                    if (budget.IsThrottled == 0)
                    {
                        budget.System?.SetPerformanceLevel(_throttleMultiplier);
                        budget.IsThrottled = 1;
                        LogSystemThrottled(budget.SystemName, _currentFrameTimeAverage, _maxFrameTimeMs);
                        _systemBudgets[i] = budget;
                    }
                }
            }
            else if (_currentFrameTimeAverage < _targetFrameTimeMs)
            {
                // Frame time is good, restore full performance
                int budgetCount = _systemBudgetCount;
                for (int i = 0; i < budgetCount; i++)
                {
                    SystemBudget budget = _systemBudgets[i];
                    if (budget.IsThrottled != 0)
                    {
                        budget.System?.SetPerformanceLevel(1f);
                        budget.IsThrottled = 0;
                        LogSystemRestored(budget.SystemName);
                        _systemBudgets[i] = budget;
                    }
                }
            }
        }

        private void LogBudgetStatus()
        {
            LogBudgetStatusInternal();
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
                _systemBudgetIndices.Remove(removedName);
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
            Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' already registered");
        }

        private void LogInvalidRegistration(string systemName)
        {
            Debug.LogWarning($"[PerformanceBudgetController] Ignoring invalid registration '{systemName}'");
        }

        private void LogRegistrationCapacityExceeded(string systemName)
        {
            Debug.LogWarning($"[PerformanceBudgetController] Ignoring registration '{systemName}' because budget capacity {MaxTrackedBudgetSystems} is full");
        }

        private void LogSystemRegistered(string systemName, float budgetMs)
        {
            Debug.Log($"[PerformanceBudgetController] Registered system '{systemName}' with {budgetMs:F2}ms budget");
        }

        private void LogSystemUnregistered(string systemName)
        {
            Debug.Log($"[PerformanceBudgetController] Unregistered system '{systemName}'");
        }

        private void LogSystemOverBudget(string systemName, float timeUsedMs, float budgetMs)
        {
            Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' over budget: {timeUsedMs:F2}ms > {budgetMs:F2}ms");
        }

        private void LogSystemThrottled(string systemName, float frameTimeMs, float maxFrameTimeMs)
        {
            Debug.Log($"[PerformanceBudgetController] Throttling system '{systemName}' due to high frame time ({frameTimeMs:F2}ms > {maxFrameTimeMs:F2}ms)");
        }

        private void LogSystemRestored(string systemName)
        {
            Debug.Log($"[PerformanceBudgetController] Restoring system '{systemName}' performance");
        }

        private void LogBudgetStatusInternal()
        {
            Debug.Log(DescribeStatus());
        }
#else
        private void LogDuplicateRegistration(string systemName) { }
        private void LogInvalidRegistration(string systemName) { }
        private void LogRegistrationCapacityExceeded(string systemName) { }
        private void LogSystemRegistered(string systemName, float budgetMs) { }
        private void LogSystemUnregistered(string systemName) { }
        private void LogSystemOverBudget(string systemName, float timeUsedMs, float budgetMs) { }
        private void LogSystemThrottled(string systemName, float frameTimeMs, float maxFrameTimeMs) { }
        private void LogSystemRestored(string systemName) { }
        private void LogBudgetStatusInternal() { }
#endif

        private void OnValidate()
        {
            if (_budgetAverageFrames < 1)
                _budgetAverageFrames = 1;
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
        public int FrameCount;
        public int OverBudgetCount;
        public byte IsThrottled;
        public float NextOverBudgetLogTime;
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
        public byte IsThrottled;
        public int OverBudgetCount;
    }
}

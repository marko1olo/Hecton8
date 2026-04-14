using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tools
{
    /// <summary>
    /// Maintains separate performance budgets for different visual systems.
    /// Provides guardrail enforcement to prevent any single system from consuming too much CPU/GPU.
    /// </summary>
    [DefaultExecutionOrder(500)] // After most systems but before rendering
    public sealed class PerformanceBudgetController : MonoBehaviour, ITickable
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

        // System budgets
        private readonly Dictionary<string, SystemBudget> _systemBudgets = new Dictionary<string, SystemBudget>();

        // Frame time tracking
        private float[] _recentFrameTimes;
        private int _recentFrameCursor;
        private int _recentFrameCount;
        private float _recentFrameSum;
        private float _currentFrameTimeAverage;
        private float _nextBudgetStatusLogTime;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly StringBuilder _statusLogBuilder = new StringBuilder(256); // COLD ALLOC: reused development-only status builder
#endif

        private const float BudgetStatusLogIntervalSeconds = 5f;
        private const float OverBudgetLogIntervalSeconds = 5f;

        // Singleton
        public static PerformanceBudgetController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureFrameHistoryCapacity();
            InitializeBudgets();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null)
                GameTickManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null)
                GameTickManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Tick(float dt)
        {
            UpdateFrameTimeAverage(Mathf.Max(0f, dt) * 1000f);

            // Check budgets and apply throttling if needed
            if (_enableThrottling)
            {
                CheckAndApplyThrottling();
            }

            // Log budget status periodically
            if (Time.unscaledTime >= _nextBudgetStatusLogTime)
            {
                LogBudgetStatus();
                _nextBudgetStatusLogTime = Time.unscaledTime + BudgetStatusLogIntervalSeconds;
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

            if (_systemBudgets.ContainsKey(systemName))
            {
                LogDuplicateRegistration(systemName);
                return;
            }

            float budgetMs = GetBudgetForSystem(systemName);
            _systemBudgets[systemName] = new SystemBudget
            {
                System = system,
                BudgetMs = budgetMs,
                IsThrottled = false
            };

            LogSystemRegistered(systemName, budgetMs);
        }

        /// <summary>
        /// Unregisters a system from budget tracking.
        /// </summary>
        public void UnregisterSystem(string systemName)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return;

            if (_systemBudgets.Remove(systemName))
            {
                LogSystemUnregistered(systemName);
            }
        }

        /// <summary>
        /// Reports system performance for budget calculation.
        /// </summary>
        public void ReportSystemPerformance(string systemName, float timeUsedMs)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return;

            if (!_systemBudgets.TryGetValue(systemName, out var budget))
                return;

            timeUsedMs = Mathf.Max(0f, timeUsedMs);
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
        }

        /// <summary>
        /// Gets the current budget status for all systems.
        /// </summary>
        public Dictionary<string, SystemBudgetInfo> GetBudgetStatus()
        {
            var status = new Dictionary<string, SystemBudgetInfo>(_systemBudgets.Count);
            Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, SystemBudget> kvp = enumerator.Current;
                SystemBudget budget = kvp.Value;
                float avgTime = budget.FrameCount > 0 ? budget.TotalTimeMs / budget.FrameCount : 0f;
                float budgetUsage = budget.BudgetMs > 0 ? (avgTime / budget.BudgetMs) : 0f;

                status[kvp.Key] = new SystemBudgetInfo
                {
                    BudgetMs = budget.BudgetMs,
                    AverageTimeMs = avgTime,
                    BudgetUsage = budgetUsage,
                    IsThrottled = budget.IsThrottled,
                    OverBudgetCount = budget.OverBudgetCount
                };
            }

            return status;
        }

        /// <summary>
        /// Returns a compact human-readable status line.
        /// </summary>
        public string DescribeStatus()
        {
            int throttledCount = CountThrottledSystems();
            int overBudgetCount = CountOverBudgetSystems();

            StringBuilder statusBuilder = new StringBuilder(256);
            statusBuilder.Append("[PerformanceBudgetController] avgFrame=");
            statusBuilder.Append(_currentFrameTimeAverage.ToString("F2"));
            statusBuilder.Append("ms target=");
            statusBuilder.Append(_targetFrameTimeMs.ToString("F2"));
            statusBuilder.Append("ms max=");
            statusBuilder.Append(_maxFrameTimeMs.ToString("F2"));
            statusBuilder.Append("ms systems=");
            statusBuilder.Append(_systemBudgets.Count);
            statusBuilder.Append(" throttled=");
            statusBuilder.Append(throttledCount);
            statusBuilder.Append(" overBudget=");
            statusBuilder.Append(overBudgetCount);

            if (_systemBudgets.Count == 0)
            {
                statusBuilder.Append(" budgets=none");
                return statusBuilder.ToString();
            }

            statusBuilder.Append(" budgets:");
            Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, SystemBudget> kvp = enumerator.Current;
                SystemBudget budget = kvp.Value;
                float avgTime = budget.FrameCount > 0 ? budget.TotalTimeMs / budget.FrameCount : 0f;
                float budgetUsage = budget.BudgetMs > 0f ? avgTime / budget.BudgetMs : 0f;
                statusBuilder.Append(' ');
                statusBuilder.Append(kvp.Key);
                statusBuilder.Append('=');
                statusBuilder.Append(avgTime.ToString("F2"));
                statusBuilder.Append("ms/");
                statusBuilder.Append(budget.BudgetMs.ToString("F2"));
                statusBuilder.Append("ms(");
                statusBuilder.AppendFormat("{0:P0}", budgetUsage);
                statusBuilder.Append(budget.IsThrottled ? ",throttled" : ",ok");
                statusBuilder.Append(')');
            }

            return statusBuilder.ToString();
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
            return Mathf.Max(0, totalBudget - allocated) * 0.5f;
        }

        private void CheckAndApplyThrottling()
        {
            if (_currentFrameTimeAverage > _maxFrameTimeMs)
            {
                // Frame time is too high, throttle systems
                Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<string, SystemBudget> kvp = enumerator.Current;
                    SystemBudget budget = kvp.Value;
                    if (!budget.IsThrottled)
                    {
                        budget.System?.SetPerformanceLevel(_throttleMultiplier);
                        budget.IsThrottled = true;
                        LogSystemThrottled(kvp.Key, _currentFrameTimeAverage, _maxFrameTimeMs);
                    }
                }
            }
            else if (_currentFrameTimeAverage < _targetFrameTimeMs)
            {
                // Frame time is good, restore full performance
                Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<string, SystemBudget> kvp = enumerator.Current;
                    SystemBudget budget = kvp.Value;
                    if (budget.IsThrottled)
                    {
                        budget.System?.SetPerformanceLevel(1f);
                        budget.IsThrottled = false;
                        LogSystemRestored(kvp.Key);
                    }
                }
            }
        }

        private void LogBudgetStatus()
        {
            LogBudgetStatusInternal();
        }

        private int CountThrottledSystems()
        {
            int count = 0;
            Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.IsThrottled)
                    count++;
            }

            return count;
        }

        private int CountOverBudgetSystems()
        {
            int count = 0;
            Dictionary<string, SystemBudget>.Enumerator enumerator = _systemBudgets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.OverBudgetCount > 0)
                    count++;
            }

            return count;
        }

        private void UpdateFrameTimeAverage(float frameTimeMs)
        {
            EnsureFrameHistoryCapacity();

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

        private void EnsureFrameHistoryCapacity()
        {
            int requiredCapacity = Mathf.Max(1, _budgetAverageFrames);
            if (_recentFrameTimes != null && _recentFrameTimes.Length == requiredCapacity)
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
    public class SystemBudget
    {
        public IBudgetManagedSystem System;
        public float BudgetMs;
        public float LastFrameTimeMs;
        public float TotalTimeMs;
        public int FrameCount;
        public int OverBudgetCount;
        public bool IsThrottled;
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
        public bool IsThrottled;
        public int OverBudgetCount;
    }
}

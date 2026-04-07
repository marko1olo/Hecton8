using System;
using System.Collections.Generic;
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
        private readonly Queue<float> _recentFrameTimes = new Queue<float>();
        private float _currentFrameTimeAverage;

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

        public void Tick(float dt)
        {
            // Update frame time average
            _recentFrameTimes.Enqueue(dt * 1000f); // Convert to milliseconds
            if (_recentFrameTimes.Count > _budgetAverageFrames)
                _recentFrameTimes.Dequeue();

            _currentFrameTimeAverage = CalculateAverage(_recentFrameTimes);

            // Check budgets and apply throttling if needed
            if (_enableThrottling)
            {
                CheckAndApplyThrottling();
            }

            // Log budget status periodically
            if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
            {
                LogBudgetStatus();
            }
        }

        /// <summary>
        /// Registers a system for budget tracking.
        /// </summary>
        public void RegisterSystem(string systemName, IBudgetManagedSystem system)
        {
            if (_systemBudgets.ContainsKey(systemName))
            {
                Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' already registered");
                return;
            }

            float budgetMs = GetBudgetForSystem(systemName);
            _systemBudgets[systemName] = new SystemBudget
            {
                System = system,
                BudgetMs = budgetMs,
                IsThrottled = false
            };

            Debug.Log($"[PerformanceBudgetController] Registered system '{systemName}' with {budgetMs:F2}ms budget");
        }

        /// <summary>
        /// Unregisters a system from budget tracking.
        /// </summary>
        public void UnregisterSystem(string systemName)
        {
            if (_systemBudgets.Remove(systemName))
            {
                Debug.Log($"[PerformanceBudgetController] Unregistered system '{systemName}'");
            }
        }

        /// <summary>
        /// Reports system performance for budget calculation.
        /// </summary>
        public void ReportSystemPerformance(string systemName, float timeUsedMs)
        {
            if (!_systemBudgets.TryGetValue(systemName, out var budget))
                return;

            budget.LastFrameTimeMs = timeUsedMs;
            budget.TotalTimeMs += timeUsedMs;
            budget.FrameCount++;

            // Check if system is over budget
            if (timeUsedMs > budget.BudgetMs)
            {
                budget.OverBudgetCount++;
                Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' over budget: {timeUsedMs:F2}ms > {budget.BudgetMs:F2}ms");
            }
        }

        /// <summary>
        /// Gets the current budget status for all systems.
        /// </summary>
        public Dictionary<string, SystemBudgetInfo> GetBudgetStatus()
        {
            var status = new Dictionary<string, SystemBudgetInfo>();

            foreach (var kvp in _systemBudgets)
            {
                var budget = kvp.Value;
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

        private void InitializeBudgets()
        {
            // Budgets are configured via inspector, but we could load from config here
        }

        private float GetBudgetForSystem(string systemName)
        {
            float totalBudget = _targetFrameTimeMs;

            switch (systemName.ToLower())
            {
                case "microfauna":
                    return totalBudget * _microfaunaBudget;
                case "biolum":
                    return totalBudget * _biolumBudget;
                case "terrain":
                    return totalBudget * _terrainBudget;
                default:
                    // Default budget for unknown systems (remaining budget)
                    float allocated = totalBudget * (_microfaunaBudget + _biolumBudget + _terrainBudget);
                    return Mathf.Max(0, totalBudget - allocated) * 0.5f; // Half of remaining
            }
        }

        private void CheckAndApplyThrottling()
        {
            if (_currentFrameTimeAverage > _maxFrameTimeMs)
            {
                // Frame time is too high, throttle systems
                foreach (var kvp in _systemBudgets)
                {
                    var budget = kvp.Value;
                    if (!budget.IsThrottled)
                    {
                        budget.System?.SetPerformanceLevel(_throttleMultiplier);
                        budget.IsThrottled = true;
                        Debug.Log($"[PerformanceBudgetController] Throttling system '{kvp.Key}' due to high frame time ({_currentFrameTimeAverage:F2}ms > {_maxFrameTimeMs:F2}ms)");
                    }
                }
            }
            else if (_currentFrameTimeAverage < _targetFrameTimeMs)
            {
                // Frame time is good, restore full performance
                foreach (var kvp in _systemBudgets)
                {
                    var budget = kvp.Value;
                    if (budget.IsThrottled)
                    {
                        budget.System?.SetPerformanceLevel(1f);
                        budget.IsThrottled = false;
                        Debug.Log($"[PerformanceBudgetController] Restoring system '{kvp.Key}' performance");
                    }
                }
            }
        }

        private void LogBudgetStatus()
        {
            var status = GetBudgetStatus();
            string log = $"[PerformanceBudgetController] Frame Time: {_currentFrameTimeAverage:F2}ms | Budgets:";

            foreach (var kvp in status)
            {
                log += $" {kvp.Key}:{kvp.Value.BudgetUsage:P0}";
            }

            Debug.Log(log);
        }

        private static float CalculateAverage(Queue<float> values)
        {
            if (values.Count == 0) return 0f;

            float sum = 0f;
            foreach (float value in values)
                sum += value;

            return sum / values.Count;
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
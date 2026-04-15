// ============================================================================
// HECTON-8 — LODSystemManager.cs
// Central coordinator for automatic LOD (Level of Detail) management.
//
// RESPONSIBILITIES:
//   • Register/unregister LODGroup components
//   • Schedule Burst-compiled distance calculation jobs
//   • Apply LOD transitions (crossfade/discrete)
//   • Manage quality presets (Low/Medium/High)
//   • Persist LOD settings via SaveManager
//
// ARCHITECTURE:
//   • Singleton via LODSystemManager.Instance
//   • ITickable — registers with GameTickManager
//   • ISaveable — persists quality settings
//   • Zero-GC — pre-allocated collections, NativeArrays
//   • Burst-compiled jobs for distance calculations
//
// PERFORMANCE:
//   • Target: < 1ms per frame for 500 LODGroups
//   • Zero GC allocations in hot paths
//   • Squared distance calculations (no sqrt)
//
// INTEGRATION:
//   • GameTickManager — ITickable registration
//   • SaveManager — ISaveable (LoadPriority=5)
//   • Unity Jobs System — Burst-compiled distance jobs
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
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
    ///   • NativeArray for job data (Allocator.Persistent)
    ///   • No LINQ, no string operations in hot paths
    ///   • Struct-based data where possible
    /// 
    /// PERFORMANCE TARGET:
    ///   • LOD processing: < 1ms per frame
    ///   • Distance job: < 1ms per frame
    ///   • Total: < 2ms per frame
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)] // Run before gameplay systems
    public sealed class LODSystemManager : MonoBehaviour, ITickable, ISaveable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static LODSystemManager _instance;

        /// <summary>
        /// Singleton instance. Null if not initialized.
        /// </summary>
        public static LODSystemManager Instance => _instance;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── LOD Configuration ──────────────────")]
        [SerializeField, Tooltip("Quality preset (Low/Medium/High)")]
        private LODQualityPreset _qualityPreset = LODQualityPreset.Medium;

        [SerializeField, Tooltip("Crossfade distance threshold (meters)")]
        private float _crossfadeDistanceThreshold = 50f;

        [SerializeField, Tooltip("Crossfade duration (seconds)")]
        private float _crossfadeDuration = 0.75f;

        [Header("── Performance ──────────────────")]
        [SerializeField, Tooltip("Max LOD groups to process per frame")]
        private int _maxLODGroupsPerFrame = 500;

        [SerializeField, Tooltip("Enable performance monitoring")]
        private bool _enablePerformanceMonitoring = true;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: List<LODGroup>[500] — registered LOD groups — owner: LODSystemManager
        private readonly List<LODGroup> _registeredLODGroups = new List<LODGroup>(500);

        // COLD ALLOC: List<Transform>[500] — cached transforms — owner: LODSystemManager
        private readonly List<Transform> _lodGroupTransforms = new List<Transform>(500);

        // COLD ALLOC: HashSet<LODGroup>[500] — O(1) duplicate check — owner: LODSystemManager
        private readonly HashSet<LODGroup> _registeredLODGroupsSet = new HashSet<LODGroup>();

        // COLD ALLOC: NativeArray<float3>[500] — job input positions — owner: LODSystemManager
        private NativeArray<float3> _lodGroupPositions;

        // COLD ALLOC: NativeArray<float>[500] — job output squared distances — owner: LODSystemManager
        private NativeArray<float> _lodGroupSquaredDistances;

        private JobHandle _distanceJobHandle;
        private bool _jobScheduled;
        private bool _registered;

        private Camera _mainCamera;
        private Transform _cameraTransform;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[LODSystemManager] Duplicate instance detected. Destroying " + gameObject.name);
                #endif
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Pre-allocate NativeArrays
            _lodGroupPositions = new NativeArray<float3>(_maxLODGroupsPerFrame, Allocator.Persistent);
            _lodGroupSquaredDistances = new NativeArray<float>(_maxLODGroupsPerFrame, Allocator.Persistent);

            // Register with SaveManager
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Register(this);
            }

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[LODSystemManager] Initialized. Max LOD groups: " + _maxLODGroupsPerFrame);
            #endif
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            // Complete any pending jobs
            if (_jobScheduled)
            {
                _distanceJobHandle.Complete();
                _jobScheduled = false;
            }
        }

        private void OnDestroy()
        {
            // Unregister from SaveManager
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Unregister(this);
            }

            // Complete any pending jobs BEFORE disposing NativeArrays
            if (_jobScheduled)
            {
                _distanceJobHandle.Complete();
                _jobScheduled = false;
            }

            // Dispose NativeArrays
            if (_lodGroupPositions.IsCreated)
                _lodGroupPositions.Dispose();

            if (_lodGroupSquaredDistances.IsCreated)
                _lodGroupSquaredDistances.Dispose();

            // Clear singleton
            if (_instance == this)
                _instance = null;
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
            // Cache camera reference
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
                _cameraTransform = _mainCamera.transform;
            }

            // Early exit if no LOD groups registered
            if (_registeredLODGroups.Count == 0) return;

            long startTicks = 0;
            if (_enablePerformanceMonitoring)
                startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            // Complete previous frame's job if still running
            if (_jobScheduled)
            {
                _distanceJobHandle.Complete();
                ApplyLODTransitions();
                _jobScheduled = false;
            }

            // Schedule new distance calculation job
            ScheduleDistanceCalculationJob();

            if (_enablePerformanceMonitoring)
            {
                long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _lodSystemCPUTime = (endTicks - startTicks) / (float)System.Diagnostics.Stopwatch.Frequency * 1000f;
            }
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
                _qualityPreset = (LODQualityPreset)presetValue;
                
                // Apply LOD bias immediately
                QualitySettings.lodBias = GetLODBias();
            }
            else
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[LODSystemManager] Invalid quality preset value. Using default (Medium).");
                #endif
                _qualityPreset = LODQualityPreset.Medium;
                QualitySettings.lodBias = 1.0f;
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

            _registeredLODGroups.Add(lodGroup);
            _lodGroupTransforms.Add(lodGroup.transform);
            _registeredLODGroupsSet.Add(lodGroup);

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_registeredLODGroups.Count > _maxLODGroupsPerFrame)
            {
                Debug.LogWarning("[LODSystemManager] Registered LOD groups exceeds max capacity. Consider increasing capacity.");
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
                    }
                    _registeredLODGroups.RemoveAt(lastIndex);
                    _lodGroupTransforms.RemoveAt(lastIndex);
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

        /// <summary>
        /// Set quality preset and apply LOD bias immediately.
        /// </summary>
        /// <param name="preset">Quality preset to apply</param>
        public void SetQualityPreset(LODQualityPreset preset)
        {
            _qualityPreset = preset;

            // Apply LOD bias to all registered LOD groups
            float lodBias = GetLODBias();
            QualitySettings.lodBias = lodBias;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Use cached strings to avoid Enum.ToString() allocation
            string presetName = preset == LODQualityPreset.Low ? "Low" : 
                               preset == LODQualityPreset.High ? "High" : "Medium";
            Debug.Log("[LODSystemManager] Quality preset set to " + presetName + ". LOD bias: " + lodBias);
            #endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS — DISTANCE CALCULATION
        // ══════════════════════════════════════════════════════════

        private void ScheduleDistanceCalculationJob()
        {
            if (_registeredLODGroups.Count == 0) return;

            // Copy LOD group positions to NativeArray
            float3 camPos = _cameraTransform.position;
            int count = Mathf.Min(_registeredLODGroups.Count, _maxLODGroupsPerFrame);

            for (int i = 0; i < count; i++)
            {
                _lodGroupPositions[i] = _lodGroupTransforms[i].position;
            }

            // Schedule Burst-compiled job
            var job = new DistanceCalculationJob
            {
                CameraPosition = camPos,
                LODGroupPositions = _lodGroupPositions,
                SquaredDistances = _lodGroupSquaredDistances
            };

            _distanceJobHandle = job.Schedule(count, 64);
            _jobScheduled = true;
        }

        private void ApplyLODTransitions()
        {
            int count = Mathf.Min(_registeredLODGroups.Count, _maxLODGroupsPerFrame);
            float crossfadeThresholdSqr = _crossfadeDistanceThreshold * _crossfadeDistanceThreshold;

            for (int i = 0; i < count; i++)
            {
                LODGroup lodGroup = _registeredLODGroups[i];
                if (lodGroup == null) continue;

                float sqrDist = _lodGroupSquaredDistances[i];

                // Apply crossfade mode for near objects
                if (sqrDist < crossfadeThresholdSqr)
                {
                    if (lodGroup.fadeMode != LODFadeMode.CrossFade)
                    {
                        lodGroup.fadeMode = LODFadeMode.CrossFade;
                        lodGroup.animateCrossFading = true;
                    }
                }
                else
                {
                    // Discrete switching for distant objects
                    if (lodGroup.fadeMode != LODFadeMode.None)
                    {
                        lodGroup.fadeMode = LODFadeMode.None;
                        lodGroup.animateCrossFading = false;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  BURST-COMPILED JOB
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Burst-compiled job for calculating squared distances from camera to LOD groups.
        /// Uses squared distance to avoid expensive sqrt operations.
        /// </summary>
        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct DistanceCalculationJob : IJobParallelFor
        {
            [ReadOnly] public float3 CameraPosition;
            [ReadOnly] public NativeArray<float3> LODGroupPositions;
            [WriteOnly] public NativeArray<float> SquaredDistances;

            public void Execute(int index)
            {
                float3 delta = LODGroupPositions[index] - CameraPosition;
                SquaredDistances[index] = math.lengthsq(delta);
            }
        }
    }
}

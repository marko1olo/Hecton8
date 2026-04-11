// ============================================================================
// HECTON-8 — ScatterReconciler.cs
// Main-thread reconciler for scatter candidate placement.
//
// ARCHITECTURE:
//   Extracted from WorldProceduralScatterDirector (11,845-line monolith).
//   Consumes blittable CandidateData from ScatterEvaluator (Job output),
//   resolves managed references (prefabs, families), and drives
//   ObjectPoolManager.Spawn/Despawn for placement lifecycle.
//
// OWNERSHIP: WorldProceduralScatterDirector orchestrates this reconciler.
// LIFETIME:  Created in Awake, disposed in OnDisable/OnDestroy.
//
// ZERO-GC CONTRACT:
//   - No new allocs in Reconcile() hot path.
//   - Pre-allocated managed arrays for residency tracking.
//   - No LINQ, no foreach on Dictionary, no string ops.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Reconciles scatter evaluation results with the live scene.
    /// Spawns new placements, despawns stale ones, and tracks residency
    /// via a key-indexed managed array. All operations on main thread only.
    /// </summary>
    public sealed class ScatterReconciler : IDisposable
    {
        // ══════════════════════════════════════════════════════════
        //  DATA STRUCTURES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tracks a live scatter placement in the scene.
        /// Managed struct — holds GameObject reference for pooling.
        /// </summary>
        public struct LivePlacement
        {
            /// <summary>Unique cell key matching CandidateData.CellKey.</summary>
            public long CellKey;

            /// <summary>Spawned GameObject instance (from ObjectPoolManager).</summary>
            public GameObject Instance;

            /// <summary>World position at spawn time.</summary>
            public float3 Position;

            /// <summary>Family index for variant tracking.</summary>
            public int FamilyIndex;

            /// <summary>Scatter layer index.</summary>
            public int LayerIndex;

            /// <summary>Frame number when this placement was created.</summary>
            public int SpawnFrame;

            /// <summary>Has this placement been claimed in the current reconcile pass?</summary>
            public bool Claimed;
        }

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int MaxLivePlacements = 4096; // COLD ALLOC budget — matches evaluator.
        private const int DespawnBatchSize = 16; // Max despawns per reconcile call.

        // ══════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: MaxLivePlacements entries for residency tracking.
        private readonly LivePlacement[] _placements;
        private readonly long[] _placementKeys;
        private int _placementCount;
        private bool _disposed;
        private bool _initialized;

        // Stats — zero-alloc diagnostics.
        private int _lastSpawnCount;
        private int _lastDespawnCount;
        private int _lastRetainedCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Number of live placements in the scene.</summary>
        public int ActivePlacementCount => _placementCount;

        /// <summary>Spawns performed in the last reconcile pass.</summary>
        public int LastSpawnCount => _lastSpawnCount;

        /// <summary>Despawns performed in the last reconcile pass.</summary>
        public int LastDespawnCount => _lastDespawnCount;

        /// <summary>Retained (unchanged) placements from the last pass.</summary>
        public int LastRetainedCount => _lastRetainedCount;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the reconciler with pre-allocated residency arrays.
        /// </summary>
        /// <remarks>
        /// COLD ALLOC: 4096 × LivePlacement (~160 KB) + 4096 × long (~32 KB).
        /// Persistent for scene lifetime.
        /// </remarks>
        public ScatterReconciler()
        {
            // COLD ALLOC: MaxLivePlacements for residency tracking (persistent).
            _placements = new LivePlacement[MaxLivePlacements];
            _placementKeys = new long[MaxLivePlacements];
            _placementCount = 0;
            _initialized = true;
        }

        /// <summary>
        /// Reconciles scatter evaluation output against live scene state.
        /// Spawns new candidates, retains existing matches, despawns stale entries.
        /// </summary>
        /// <param name="candidates">Candidate array from ScatterEvaluator.</param>
        /// <param name="candidateCount">Number of valid candidates in the array.</param>
        /// <param name="resolvePrefab">
        /// Callback to resolve a candidate's FamilyIndex + LayerIndex to a prefab.
        /// Must return null if no prefab is available (skip placement).
        /// </param>
        /// <remarks>
        /// MAIN THREAD ONLY. Zero GC in hot path.
        /// [FORBID] Calling from Job/Task/Thread.
        /// </remarks>
        public void Reconcile(
            NativeArray<ScatterEvaluator.CandidateData> candidates,
            int candidateCount,
            Func<int, int, GameObject> resolvePrefab)
        {
            if (!_initialized || _disposed) return;

            _lastSpawnCount = 0;
            _lastDespawnCount = 0;
            _lastRetainedCount = 0;

            // Phase 1: Mark all existing placements as unclaimed.
            for (int i = 0; i < _placementCount; i++)
            {
                LivePlacement p = _placements[i];
                p.Claimed = false;
                _placements[i] = p;
            }

            // Phase 2: Match candidates against existing placements.
            int validCount = math.min(candidateCount, candidates.Length);
            for (int c = 0; c < validCount; c++)
            {
                ScatterEvaluator.CandidateData candidate = candidates[c];
                if (!candidate.IsValid) continue;

                int existingIndex = FindPlacementIndex(candidate.CellKey);
                if (existingIndex >= 0)
                {
                    // Existing placement matches — retain it.
                    LivePlacement p = _placements[existingIndex];
                    p.Claimed = true;
                    _placements[existingIndex] = p;
                    _lastRetainedCount++;
                }
                else
                {
                    // New candidate — attempt spawn.
                    if (resolvePrefab == null) continue;

                    GameObject prefab = resolvePrefab(candidate.FamilyIndex, candidate.LayerIndex);
                    if (prefab == null) continue;

                    ObjectPoolManager poolManager = ObjectPoolManager.Instance;
                    if (poolManager == null) continue;

                    GameObject instance = poolManager.Spawn(
                        prefab,
                        new Vector3(candidate.Position.x, candidate.Position.y, candidate.Position.z),
                        Quaternion.Euler(0f, candidate.Rotation, 0f));

                    if (instance == null) continue;

                    instance.transform.localScale = Vector3.one * candidate.Scale;

                    if (!TryAddPlacement(new LivePlacement
                    {
                        CellKey = candidate.CellKey,
                        Instance = instance,
                        Position = candidate.Position,
                        FamilyIndex = candidate.FamilyIndex,
                        LayerIndex = candidate.LayerIndex,
                        SpawnFrame = Time.frameCount,
                        Claimed = true
                    }))
                    {
                        // Residency array full — despawn overflow immediately.
                        poolManager.Despawn(instance);
                    }
                    else
                    {
                        _lastSpawnCount++;
                    }
                }
            }

            // Phase 3: Despawn unclaimed (stale) placements.
            int despawnBudget = DespawnBatchSize;
            for (int i = _placementCount - 1; i >= 0 && despawnBudget > 0; i--)
            {
                if (_placements[i].Claimed) continue;

                GameObject instance = _placements[i].Instance;
                if (instance != null)
                {
                    ObjectPoolManager poolManager = ObjectPoolManager.Instance;
                    if (poolManager != null)
                        poolManager.Despawn(instance);
                }

                // Compact: move last entry to this slot.
                RemovePlacementAt(i);
                _lastDespawnCount++;
                despawnBudget--;
            }
        }

        /// <summary>
        /// Despawns all active placements. Call during scene teardown or reset.
        /// </summary>
        public void DespawnAll()
        {
            ObjectPoolManager poolManager = ObjectPoolManager.Instance;

            for (int i = 0; i < _placementCount; i++)
            {
                GameObject instance = _placements[i].Instance;
                if (instance != null && poolManager != null)
                    poolManager.Despawn(instance);
            }

            _placementCount = 0;
        }

        /// <summary>
        /// Releases all managed state. Call in OnDisable/OnDestroy.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            DespawnAll();
            _disposed = true;
            _initialized = false;
        }

        // ══════════════════════════════════════════════════════════
        //  RESIDENCY TRACKING (Zero-GC linear search)
        // ══════════════════════════════════════════════════════════

        private int FindPlacementIndex(long cellKey)
        {
            for (int i = 0; i < _placementCount; i++)
            {
                if (_placementKeys[i] == cellKey) return i;
            }
            return -1;
        }

        private bool TryAddPlacement(LivePlacement placement)
        {
            if (_placementCount >= MaxLivePlacements)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_placementCount == MaxLivePlacements)
                    Debug.LogWarning("[ScatterReconciler] Max live placements reached. Overflow dropped.");
#endif
                return false;
            }

            _placementKeys[_placementCount] = placement.CellKey;
            _placements[_placementCount] = placement;
            _placementCount++;
            return true;
        }

        private void RemovePlacementAt(int index)
        {
            int lastIndex = _placementCount - 1;
            if (index < lastIndex)
            {
                _placements[index] = _placements[lastIndex];
                _placementKeys[index] = _placementKeys[lastIndex];
            }
            _placementCount--;
        }
    }
}

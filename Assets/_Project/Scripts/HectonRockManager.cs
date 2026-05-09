// ============================================================================
// HECTON-8 — HectonRockManager.cs  v1.1
// Fixed: GPUInstancerPrefab → GPUInstancerPrefabPrototype for GPUI API calls.
//
// GPU Instancer type hierarchy:
//   GPUInstancerPrefab (MonoBehaviour on prefab GO)
//     └─ .prefabPrototype → GPUInstancerPrefabPrototype (ScriptableObject)
//   GPUInstancerAPI methods require GPUInstancerPrefabPrototype, NOT GPUInstancerPrefab.
// ============================================================================

using System;
using System.Collections.Generic;
using GPUInstancer;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class HectonRockManager : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static HectonRockManager Instance => GlobalRegistry.RockManager;

        internal GPUInstancerPrefabManager GpuInstancerManager => gpuiManager;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — ROCK LAYERS
        // ══════════════════════════════════════════════════════════

        [Header("── Rock Layers ───────────────────────────────")]
        [Tooltip("Each layer maps a LayerID to a prefab that has GPUInstancerPrefab component. " +
                 "The prototype is extracted automatically at runtime.")]
        [SerializeField] private RockLayerConfig[] rockLayers;

        [Header("── References ────────────────────────────────")]
        [Tooltip("GPU Instancer Prefab Manager in scene.")]
        [SerializeField] private GPUInstancerPrefabManager gpuiManager;

        [Tooltip("Proximity Collider System for physics generation.")]
        [SerializeField] private ProximityColliderSystem proximityColliderSystem;

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Maximum total instances across all layers. Pre-allocates buffers.")]
        [SerializeField] private int maxExpectedInstances = 120000;

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private int _debugTotalChunks;
        [SerializeField] private int _debugTotalInstances;
        [SerializeField] private int _debugDirtyRebuilds;
        [SerializeField] private bool _debugGPUIReady;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  SERIALIZABLE CONFIG
        // ══════════════════════════════════════════════════════════

        [Serializable]
        public struct RockLayerConfig
        {
            [Tooltip("Unique ID matching HectonRockOutput.layerID.")]
            public int layerId;

            [Tooltip("The prefab with GPUInstancerPrefab component. " +
                     "Must be registered in GPU Instancer Prefab Manager.")]
            public GPUInstancerPrefab prefabReference;
        }

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Dictionary<int, Dictionary<Vector2Int, Matrix4x4[]>> _chunkData;
        private Dictionary<int, Matrix4x4[]> _aggregatedMatrices;
        private Dictionary<int, int> _aggregatedCounts;
        private Dictionary<int, int> _gpuiBufferCapacities;
        private Vector3[] _aggregatedPositions;
        private int _aggregatedPositionCount;
        private int _instanceCapacity;
        private bool _isDirty;
        private bool _layerCapacityOverflowLogged;
        private bool _proximityCapacityOverflowLogged;
        private bool _missingLayerBufferLogged;

        // LayerID → GPUInstancerPrefabPrototype (the actual type GPUI API wants)
        private Dictionary<int, GPUInstancerPrefabPrototype> _prototypeLookup;

        private HashSet<int> _gpuiInitializedLayers;
        private bool _registeredToTickManager;
        private bool _serviceRegistered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            HectonRockManager registered = GlobalRegistry.RockManager;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            // COLD ALLOC: Dictionary<int,Dictionary<Vector2Int,Matrix4x4[]>>[8] - rock chunk maps by layer - owner: HectonRockManager
            _chunkData = new Dictionary<int, Dictionary<Vector2Int, Matrix4x4[]>>(8);
            // COLD ALLOC: Dictionary<int,Matrix4x4[]>[8] - GPUI aggregation buffers by layer - owner: HectonRockManager
            _aggregatedMatrices = new Dictionary<int, Matrix4x4[]>(8);
            // COLD ALLOC: Dictionary<int,int>[8] - active GPUI counts by layer - owner: HectonRockManager
            _aggregatedCounts = new Dictionary<int, int>(8);
            // COLD ALLOC: Dictionary<int,int>[8] - GPUI buffer capacities by layer - owner: HectonRockManager
            _gpuiBufferCapacities = new Dictionary<int, int>(8);
            _instanceCapacity = Mathf.Max(1, maxExpectedInstances);
            // COLD ALLOC: Vector3[_instanceCapacity] - rock proximity aggregation cap - owner: HectonRockManager
            _aggregatedPositions = new Vector3[_instanceCapacity];
            _aggregatedPositionCount = 0;
            _isDirty = false;
            _registeredToTickManager = false;
            _serviceRegistered = false;

            // Build prototype lookup: extract GPUInstancerPrefabPrototype from GPUInstancerPrefab component
            // COLD ALLOC: Dictionary<int,GPUInstancerPrefabPrototype>[8] - prefab prototype lookup by layer - owner: HectonRockManager
            _prototypeLookup = new Dictionary<int, GPUInstancerPrefabPrototype>(8);
            // COLD ALLOC: HashSet<int>[8] - GPUI initialized layer set - owner: HectonRockManager
            _gpuiInitializedLayers = new HashSet<int>(8);

            if (rockLayers != null)
            {
                for (int i = 0; i < rockLayers.Length; i++)
                {
                    RockLayerConfig cfg = rockLayers[i];

                    if (cfg.prefabReference == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError($"[HectonRockManager] Rock layer {i} (layerId={cfg.layerId}) " +
                                       "has null prefabReference!", this);
#endif
                        continue;
                    }

                    GPUInstancerPrefabPrototype proto = cfg.prefabReference.prefabPrototype;
                    if (proto == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError($"[HectonRockManager] Rock layer {i} (layerId={cfg.layerId}) " +
                                       "prefab has no prefabPrototype! Is it registered in GPUI Manager?", this);
#endif
                        continue;
                    }

                    if (!_prototypeLookup.ContainsKey(cfg.layerId))
                    {
                        _prototypeLookup[cfg.layerId] = proto;
                        // COLD ALLOC: Dictionary<Vector2Int,Matrix4x4[]>[64] - authored rock chunk map - owner: HectonRockManager
                        _chunkData[cfg.layerId] = new Dictionary<Vector2Int, Matrix4x4[]>(64);
                        // COLD ALLOC: Matrix4x4[_instanceCapacity] - per-layer GPUI aggregation cap - owner: HectonRockManager
                        _aggregatedMatrices[cfg.layerId] = new Matrix4x4[_instanceCapacity];
                        _aggregatedCounts[cfg.layerId] = 0;
                        _gpuiBufferCapacities[cfg.layerId] = 0;
                    }
                    else
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[HectonRockManager] Duplicate layerId={cfg.layerId} " +
                                          "in rock layers config. Skipping.", this);
#endif
                    }
                }
            }

            if (gpuiManager == null)
            {
                TryGetComponent(out gpuiManager);
                if (gpuiManager == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[HectonRockManager] GPUInstancerPrefabManager not found!", this);
#endif
                }
            }

            if (proximityColliderSystem == null)
            {
                proximityColliderSystem = ProximityColliderSystem.ActiveRuntimeInstance;
                if (proximityColliderSystem == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[HectonRockManager] ProximityColliderSystem not found. " +
                                     "Rocks will render but have no physics.", this);
#endif
                }
            }

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();
            TryRegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterToGlobalRegistry();
            TryRegisterToTickManager();

#if UNITY_EDITOR
            _debugGPUIReady = gpuiManager != null;
#endif
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            TryUnregisterFromGlobalRegistry();
        }

        private void OnDestroy()
        {
            TryUnregisterFromGlobalRegistry();

        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            HectonRockManager registered = GlobalRegistry.RockManager;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterRockManagerRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.RockManager, this);
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterRockManagerRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CHUNK REGISTRATION
        // ══════════════════════════════════════════════════════════

        public void RegisterChunk(int layerId, Vector2Int chunkCoord, Matrix4x4[] matrices)
        {
            if (matrices == null || matrices.Length == 0) return;

            if (!_chunkData.TryGetValue(layerId, out var chunkDict))
            {
                // COLD ALLOC: Dictionary<Vector2Int,Matrix4x4[]>[64] - late-registered rock chunk map - owner: HectonRockManager
                chunkDict = new Dictionary<Vector2Int, Matrix4x4[]>(64);
                _chunkData[layerId] = chunkDict;
            }

            if (!_aggregatedMatrices.ContainsKey(layerId))
            {
                // COLD ALLOC: Matrix4x4[_instanceCapacity] - late-registered rock layer aggregation cap - owner: HectonRockManager
                _aggregatedMatrices[layerId] = new Matrix4x4[_instanceCapacity];
                _aggregatedCounts[layerId] = 0;
                _gpuiBufferCapacities[layerId] = 0;
            }

            chunkDict[chunkCoord] = matrices;
            _isDirty = true;

#if UNITY_EDITOR
            Debug.Log($"[HectonRockManager] RegisterChunk: layer={layerId}, " +
                      $"chunk={chunkCoord}, count={matrices.Length}");
#endif
        }

        public void UnregisterChunk(Vector2Int chunkCoord)
        {
            foreach (var kvp in _chunkData)
            {
                if (kvp.Value.Remove(chunkCoord))
                    _isDirty = true;
            }

#if UNITY_EDITOR
            Debug.Log($"[HectonRockManager] UnregisterChunk: {chunkCoord}");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — REBUILD + PUSH
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (!_isDirty) return;
            _isDirty = false;

            if (gpuiManager == null) return;

            int totalPositionCount = 0;

            // ── Pass 1: Aggregate per-layer and push to GPUI ──
            Dictionary<int, Dictionary<Vector2Int, Matrix4x4[]>>.Enumerator layerEnumerator = _chunkData.GetEnumerator();
            while (layerEnumerator.MoveNext())
            {
                KeyValuePair<int, Dictionary<Vector2Int, Matrix4x4[]>> layerKvp = layerEnumerator.Current;
                int layerId = layerKvp.Key;
                Dictionary<Vector2Int, Matrix4x4[]> chunkDict = layerKvp.Value;

                int layerTotal = 0;
                Dictionary<Vector2Int, Matrix4x4[]>.Enumerator chunkCountEnumerator = chunkDict.GetEnumerator();
                while (chunkCountEnumerator.MoveNext())
                {
                    layerTotal += chunkCountEnumerator.Current.Value.Length;
                }

                if (layerTotal == 0)
                {
                    if (_prototypeLookup.TryGetValue(layerId, out GPUInstancerPrefabPrototype proto))
                    {
                        if (_gpuiInitializedLayers.Contains(layerId))
                        {
                            GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                                gpuiManager, proto, Array.Empty<Matrix4x4>());
                        }
                    }
                    _aggregatedCounts[layerId] = 0;
                    continue;
                }

                if (!_aggregatedMatrices.TryGetValue(layerId, out Matrix4x4[] layerBuffer) || layerBuffer == null)
                {
                    LogMissingLayerBuffer(layerId);
                    _aggregatedCounts[layerId] = 0;
                    continue;
                }

                int writeLimit = Math.Min(layerTotal, layerBuffer.Length);
                if (writeLimit < layerTotal)
                    LogLayerCapacityOverflow(layerId, layerTotal, layerBuffer.Length);

                // Copy all chunks into flat array
                int writeIndex = 0;
                Dictionary<Vector2Int, Matrix4x4[]>.Enumerator chunkCopyEnumerator = chunkDict.GetEnumerator();
                while (chunkCopyEnumerator.MoveNext())
                {
                    if (writeIndex >= writeLimit)
                        break;

                    KeyValuePair<Vector2Int, Matrix4x4[]> chunkKvp = chunkCopyEnumerator.Current;
                    Matrix4x4[] chunkMatrices = chunkKvp.Value;
                    int chunkLen = chunkMatrices.Length;
                    int copyCount = Math.Min(chunkLen, writeLimit - writeIndex);
                    Array.Copy(chunkMatrices, 0, layerBuffer, writeIndex, copyCount);
                    writeIndex += copyCount;
                }

                _aggregatedCounts[layerId] = writeIndex;

                // Push to GPU Instancer
                if (_prototypeLookup.TryGetValue(layerId, out GPUInstancerPrefabPrototype prototype))
                {
                    bool needsInitialize = !_gpuiInitializedLayers.Contains(layerId);
                    int requiredCapacity = layerBuffer.Length;
                    if (!needsInitialize &&
                        _gpuiBufferCapacities.TryGetValue(layerId, out int currentCapacity) &&
                        currentCapacity < requiredCapacity)
                    {
                        needsInitialize = true;
                    }

                    if (needsInitialize)
                    {
                        GPUInstancerAPI.InitializePrototype(
                            gpuiManager,
                            prototype,
                            requiredCapacity,
                            layerTotal);
                        _gpuiInitializedLayers.Add(layerId);
                        _gpuiBufferCapacities[layerId] = requiredCapacity;
                    }

                    GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                        gpuiManager,
                        prototype,
                        layerBuffer,
                        0,
                        0,
                        writeIndex);
                }

                totalPositionCount += writeIndex;
            }

            // ── Pass 2: Aggregate positions for ProximityColliderSystem ──
            if (proximityColliderSystem != null && totalPositionCount > 0)
            {
                int proximityWriteLimit = Math.Min(totalPositionCount, _aggregatedPositions.Length);
                if (proximityWriteLimit < totalPositionCount)
                    LogProximityCapacityOverflow(totalPositionCount, _aggregatedPositions.Length);

                int posWriteIndex = 0;
                layerEnumerator = _chunkData.GetEnumerator();
                while (layerEnumerator.MoveNext())
                {
                    if (posWriteIndex >= proximityWriteLimit)
                        break;

                    Dictionary<Vector2Int, Matrix4x4[]>.Enumerator chunkPositionEnumerator = layerEnumerator.Current.Value.GetEnumerator();
                    while (chunkPositionEnumerator.MoveNext())
                    {
                        if (posWriteIndex >= proximityWriteLimit)
                            break;

                        KeyValuePair<Vector2Int, Matrix4x4[]> chunkKvp = chunkPositionEnumerator.Current;
                        Matrix4x4[] matrices = chunkKvp.Value;
                        int len = matrices.Length;
                        for (int i = 0; i < len && posWriteIndex < proximityWriteLimit; i++)
                        {
                            Matrix4x4 m = matrices[i];
                            _aggregatedPositions[posWriteIndex].x = m.m03;
                            _aggregatedPositions[posWriteIndex].y = m.m13;
                            _aggregatedPositions[posWriteIndex].z = m.m23;
                            posWriteIndex++;
                        }
                    }
                }

                _aggregatedPositionCount = posWriteIndex;

                proximityColliderSystem.Initialize(_aggregatedPositions, _aggregatedPositionCount);
            }
            else if (proximityColliderSystem != null && totalPositionCount == 0)
            {
                _aggregatedPositionCount = 0;
                proximityColliderSystem.ClearRuntimeData();
            }

            UpdateDiagnostics();

#if UNITY_EDITOR
            _debugDirtyRebuilds++;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogLayerCapacityOverflow(int layerId, int requestedCount, int capacity)
        {
            if (_layerCapacityOverflowLogged)
                return;

            _layerCapacityOverflowLogged = true;
            Debug.LogWarning(
                $"[HectonRockManager] Rock layer aggregation exceeded capacity. layerId={layerId} requested={requestedCount} capacity={capacity}. Excess instances were dropped for this rebuild.",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogProximityCapacityOverflow(int requestedCount, int capacity)
        {
            if (_proximityCapacityOverflowLogged)
                return;

            _proximityCapacityOverflowLogged = true;
            Debug.LogWarning(
                $"[HectonRockManager] Proximity aggregation exceeded capacity. requested={requestedCount} capacity={capacity}. Excess collider points were dropped for this rebuild.",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMissingLayerBuffer(int layerId)
        {
            if (_missingLayerBufferLogged)
                return;

            _missingLayerBufferLogged = true;
            Debug.LogWarning(
                $"[HectonRockManager] Missing aggregation buffer for layerId={layerId}. Layer rebuild skipped.",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            int totalChunks = 0;
            int totalInstances = 0;

            foreach (var layerKvp in _chunkData)
            {
                totalChunks += layerKvp.Value.Count;
                foreach (var chunkKvp in layerKvp.Value)
                {
                    totalInstances += chunkKvp.Value.Length;
                }
            }

            _debugTotalChunks = totalChunks;
            _debugTotalInstances = totalInstances;
            _debugGPUIReady = gpuiManager != null;
        }
    }
}

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

        private static HectonRockManager _instance;

        public static HectonRockManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

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
        private Vector3[] _aggregatedPositions;
        private int _aggregatedPositionCount;
        private bool _isDirty;

        // LayerID → GPUInstancerPrefabPrototype (the actual type GPUI API wants)
        private Dictionary<int, GPUInstancerPrefabPrototype> _prototypeLookup;

        private HashSet<int> _gpuiInitializedLayers;
        private bool _registeredToTickManager;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _chunkData = new Dictionary<int, Dictionary<Vector2Int, Matrix4x4[]>>(8);
            _aggregatedMatrices = new Dictionary<int, Matrix4x4[]>(8);
            _aggregatedCounts = new Dictionary<int, int>(8);
            _aggregatedPositions = new Vector3[maxExpectedInstances];
            _aggregatedPositionCount = 0;
            _isDirty = false;
            _registeredToTickManager = false;

            // Build prototype lookup: extract GPUInstancerPrefabPrototype from GPUInstancerPrefab component
            _prototypeLookup = new Dictionary<int, GPUInstancerPrefabPrototype>(8);
            _gpuiInitializedLayers = new HashSet<int>();

            if (rockLayers != null)
            {
                for (int i = 0; i < rockLayers.Length; i++)
                {
                    RockLayerConfig cfg = rockLayers[i];

                    if (cfg.prefabReference == null)
                    {
                        Debug.LogError($"[HectonRockManager] Rock layer {i} (layerId={cfg.layerId}) " +
                                       "has null prefabReference!", this);
                        continue;
                    }

                    GPUInstancerPrefabPrototype proto = cfg.prefabReference.prefabPrototype;
                    if (proto == null)
                    {
                        Debug.LogError($"[HectonRockManager] Rock layer {i} (layerId={cfg.layerId}) " +
                                       "prefab has no prefabPrototype! Is it registered in GPUI Manager?", this);
                        continue;
                    }

                    if (!_prototypeLookup.ContainsKey(cfg.layerId))
                    {
                        _prototypeLookup[cfg.layerId] = proto;
                        _chunkData[cfg.layerId] = new Dictionary<Vector2Int, Matrix4x4[]>(64);
                        _aggregatedMatrices[cfg.layerId] = new Matrix4x4[maxExpectedInstances];
                        _aggregatedCounts[cfg.layerId] = 0;
                    }
                    else
                    {
                        Debug.LogWarning($"[HectonRockManager] Duplicate layerId={cfg.layerId} " +
                                          "in rock layers config. Skipping.", this);
                    }
                }
            }

            if (gpuiManager == null)
            {
                gpuiManager = FindAnyObjectByType<GPUInstancerPrefabManager>();
                if (gpuiManager == null)
                    Debug.LogError("[HectonRockManager] GPUInstancerPrefabManager not found!", this);
            }

            if (proximityColliderSystem == null)
            {
                proximityColliderSystem = FindAnyObjectByType<ProximityColliderSystem>();
                if (proximityColliderSystem == null)
                    Debug.LogWarning("[HectonRockManager] ProximityColliderSystem not found. " +
                                     "Rocks will render but have no physics.", this);
            }

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager)
            {
                if (GameTickManager.Instance != null)
                {
                    GameTickManager.Instance.Register((ISlowTickable)this);
                    _registeredToTickManager = true;
                }
                else
                {
                    Debug.LogError("[HectonRockManager] GameTickManager.Instance is null at Start().", this);
                }
            }

#if UNITY_EDITOR
            _debugGPUIReady = gpuiManager != null;
#endif
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CHUNK REGISTRATION
        // ══════════════════════════════════════════════════════════

        public void RegisterChunk(int layerId, Vector2Int chunkCoord, Matrix4x4[] matrices)
        {
            if (matrices == null || matrices.Length == 0) return;

            if (!_chunkData.TryGetValue(layerId, out var chunkDict))
            {
                chunkDict = new Dictionary<Vector2Int, Matrix4x4[]>(64);
                _chunkData[layerId] = chunkDict;
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
            foreach (var layerKvp in _chunkData)
            {
                int layerId = layerKvp.Key;
                Dictionary<Vector2Int, Matrix4x4[]> chunkDict = layerKvp.Value;

                int layerTotal = 0;
                foreach (var chunkKvp in chunkDict)
                {
                    layerTotal += chunkKvp.Value.Length;
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

                // Ensure buffer is large enough
                if (!_aggregatedMatrices.TryGetValue(layerId, out Matrix4x4[] layerBuffer) ||
                    layerBuffer.Length < layerTotal)
                {
                    int newSize = layerTotal + layerTotal / 5;
                    _aggregatedMatrices[layerId] = new Matrix4x4[newSize];
                    layerBuffer = _aggregatedMatrices[layerId];
                }

                // Copy all chunks into flat array
                int writeIndex = 0;
                foreach (var chunkKvp in chunkDict)
                {
                    Matrix4x4[] chunkMatrices = chunkKvp.Value;
                    int chunkLen = chunkMatrices.Length;
                    Array.Copy(chunkMatrices, 0, layerBuffer, writeIndex, chunkLen);
                    writeIndex += chunkLen;
                }

                _aggregatedCounts[layerId] = layerTotal;

                // Push to GPU Instancer
                if (_prototypeLookup.TryGetValue(layerId, out GPUInstancerPrefabPrototype prototype))
                {
                    // GPUI reads .Length for instance count — need exact-length array
                    Matrix4x4[] gpuiArray;
                    if (layerBuffer.Length == layerTotal)
                    {
                        gpuiArray = layerBuffer;
                    }
                    else
                    {
                        gpuiArray = new Matrix4x4[layerTotal];
                        Array.Copy(layerBuffer, 0, gpuiArray, 0, layerTotal);
                    }

                    if (!_gpuiInitializedLayers.Contains(layerId))
                    {
                        GPUInstancerAPI.InitializeWithMatrix4x4Array(
                            gpuiManager, prototype, gpuiArray);
                        _gpuiInitializedLayers.Add(layerId);
                    }
                    else
                    {
                        GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                            gpuiManager, prototype, gpuiArray);
                    }
                }

                totalPositionCount += layerTotal;
            }

            // ── Pass 2: Aggregate positions for ProximityColliderSystem ──
            if (proximityColliderSystem != null && totalPositionCount > 0)
            {
                if (_aggregatedPositions.Length < totalPositionCount)
                {
                    int newSize = totalPositionCount + totalPositionCount / 5;
                    _aggregatedPositions = new Vector3[newSize];
                }

                int posWriteIndex = 0;
                foreach (var layerKvp in _chunkData)
                {
                    foreach (var chunkKvp in layerKvp.Value)
                    {
                        Matrix4x4[] matrices = chunkKvp.Value;
                        int len = matrices.Length;
                        for (int i = 0; i < len; i++)
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

                if (_aggregatedPositions.Length == _aggregatedPositionCount)
                {
                    proximityColliderSystem.Initialize(_aggregatedPositions);
                }
                else
                {
                    Vector3[] trimmed = new Vector3[_aggregatedPositionCount];
                    Array.Copy(_aggregatedPositions, 0, trimmed, 0, _aggregatedPositionCount);
                    proximityColliderSystem.Initialize(trimmed);
                }
            }
            else if (proximityColliderSystem != null && totalPositionCount == 0)
            {
                proximityColliderSystem.Initialize(Array.Empty<Vector3>());
            }

            UpdateDiagnostics();

#if UNITY_EDITOR
            _debugDirtyRebuilds++;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

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
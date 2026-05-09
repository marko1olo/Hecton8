using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private const float LargeFloraColliderMinimumScale = 0.05f;
        private const uint LargeFloraColliderLookupHash = 2654435761u;

        [Header("Large Flora Collision Proxies")]
        [SerializeField]
        [Tooltip("Pooled prefab with a BoxCollider on the root. One runtime proxy is scaled to the active coral bounds.")]
        private GameObject largeFloraColliderProxyPrefab;

        [SerializeField, Range(1, 64)]
        [Tooltip("Maximum concurrently active large-coral collision proxy count.")]
        private int largeFloraColliderProxyCapacity = 24;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Runtime-local distance in meters at which a large coral BoxCollider proxy is enabled.")]
        private float largeFloraColliderActivateRadius = 10f;

        [SerializeField, Range(1f, 24f)]
        [Tooltip("Runtime-local hysteresis distance in meters at which a large coral BoxCollider proxy is returned to the pool.")]
        private float largeFloraColliderDeactivateRadius = 12f;

        [SerializeField, Range(16, 512)]
        [Tooltip("Maximum active flora records scanned per Tick when looking for nearby large corals.")]
        private int largeFloraColliderScanBudgetPerTick = 160;

        private GameObject[] _largeFloraColliderProxies;
        private BoxCollider[] _largeFloraColliderBoxes;
        private int[] _largeFloraColliderSourceIndices;
        private Vector3[] _largeFloraColliderUniverseCenters;
        private int[] _largeFloraColliderLookupSourceIndices;
        private int[] _largeFloraColliderLookupSlots;
        private GameObject _largeFloraColliderRuntimeProxyPrefab;
        private int _largeFloraColliderScanCursor;
        private int _largeFloraColliderObservedRevision = -1;
        private bool _largeFloraColliderPoolWarmed;

        private void InitializeLargeFloraCollisionProxyState()
        {
            int capacity = ResolveLargeFloraColliderProxyCapacity();
            int lookupCapacity = ResolveLargeFloraColliderLookupCapacity(capacity);
            if (_largeFloraColliderProxies != null &&
                _largeFloraColliderProxies.Length == capacity &&
                _largeFloraColliderBoxes != null &&
                _largeFloraColliderBoxes.Length == capacity &&
                _largeFloraColliderSourceIndices != null &&
                _largeFloraColliderSourceIndices.Length == capacity &&
                _largeFloraColliderUniverseCenters != null &&
                _largeFloraColliderUniverseCenters.Length == capacity &&
                _largeFloraColliderLookupSourceIndices != null &&
                _largeFloraColliderLookupSourceIndices.Length == lookupCapacity &&
                _largeFloraColliderLookupSlots != null &&
                _largeFloraColliderLookupSlots.Length == lookupCapacity)
            {
                EnsureLargeFloraColliderRuntimeProxyPrefab();
                return;
            }

            ClearLargeFloraCollisionProxies();
            // COLD ALLOC: GameObject[largeFloraColliderProxyCapacity] — active large-flora collider proxy cache — owner: HectonMapMagicVegetationBridge
            _largeFloraColliderProxies = new GameObject[capacity];
            // COLD ALLOC: BoxCollider[largeFloraColliderProxyCapacity] — active large-flora collider component cache — owner: HectonMapMagicVegetationBridge
            _largeFloraColliderBoxes = new BoxCollider[capacity];
            // COLD ALLOC: int[largeFloraColliderProxyCapacity] — source-index lookup for large-flora collider proxies — owner: HectonMapMagicVegetationBridge
            _largeFloraColliderSourceIndices = new int[capacity];
            // COLD ALLOC: Vector3[largeFloraColliderProxyCapacity] — cached universe-space proxy centers for squared-distance deactivation — owner: HectonMapMagicVegetationBridge
            _largeFloraColliderUniverseCenters = new Vector3[capacity];
            // COLD ALLOC: int[lookupCapacity] - open-address source index lookup, no Dictionary in Tick - owner: HectonMapMagicVegetationBridge
            _largeFloraColliderLookupSourceIndices = new int[lookupCapacity];
            // COLD ALLOC: int[lookupCapacity] - source-to-slot lookup payload, no Dictionary in Tick - owner: HectonMapMagicVegetationBridge
            _largeFloraColliderLookupSlots = new int[lookupCapacity];
            for (int i = 0; i < capacity; i++)
                _largeFloraColliderSourceIndices[i] = -1;
            ResetLargeFloraColliderSlotLookup();

            _largeFloraColliderScanCursor = 0;
            _largeFloraColliderObservedRevision = -1;
            _largeFloraColliderPoolWarmed = false;
            EnsureLargeFloraColliderRuntimeProxyPrefab();
        }

        private void DisposeLargeFloraCollisionProxyState()
        {
            ClearLargeFloraCollisionProxies();
            _largeFloraColliderProxies = null;
            _largeFloraColliderBoxes = null;
            _largeFloraColliderSourceIndices = null;
            _largeFloraColliderUniverseCenters = null;
            _largeFloraColliderLookupSourceIndices = null;
            _largeFloraColliderLookupSlots = null;
            _largeFloraColliderScanCursor = 0;
            _largeFloraColliderObservedRevision = -1;
            _largeFloraColliderPoolWarmed = false;
            DestroyLargeFloraRuntimeProxyPrefab();
        }

        private void TryWarmupLargeFloraCollisionProxyPool()
        {
            if (_largeFloraColliderPoolWarmed)
                return;

            EnsureLargeFloraColliderRuntimeProxyPrefab();
            GameObject proxyPrefab = GetLargeFloraColliderProxyPrefabNoAlloc();
            if (proxyPrefab == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return;

            pool.Warmup(proxyPrefab, ResolveLargeFloraColliderProxyCapacity());
            _largeFloraColliderPoolWarmed = true;
        }

        private void UpdateLargeFloraCollisionProxies(float dt)
        {
            if (!math.isfinite(dt) || dt < 0f)
                return;

            InitializeLargeFloraCollisionProxyState();
            if (GetLargeFloraColliderProxyPrefabNoAlloc() == null)
                return;

            if (!TryResolveLargeFloraColliderPlayerUniverse(out Vector3 playerUniverse))
                return;

            int activeRevision = ActiveUnderwaterAggregateRevision;
            if (_largeFloraColliderObservedRevision != activeRevision)
            {
                ClearLargeFloraCollisionProxies();
                _largeFloraColliderObservedRevision = activeRevision;
                _largeFloraColliderScanCursor = 0;
            }

            DeactivateDistantLargeFloraCollisionProxies(playerUniverse);
            ScanLargeFloraCollisionProxyCandidates(playerUniverse);
        }

        private bool TryResolveLargeFloraColliderPlayerUniverse(out Vector3 playerUniverse)
        {
            if (TryResolvePlayerRuntimePositionFromAup(out Vector3 runtimePosition))
            {
                playerUniverse = ToUniverseSpace(runtimePosition);
                return true;
            }

            playerUniverse = default;
            return false;
        }

        private void ScanLargeFloraCollisionProxyCandidates(Vector3 playerUniverse)
        {
            NativeArray<Matrix4x4> matrices = ActiveUnderwaterMatricesNative;
            NativeArray<HectonVegetationInstanceData> metadata = ActiveUnderwaterMetadataNative;
            NativeArray<int> types = ActiveUnderwaterTypesNative;
            NativeArray<int> semanticTypes = ActiveUnderwaterSemanticTypesNative;
            int count = ActiveUnderwaterInstanceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                count <= 0)
            {
                return;
            }

            int safeCount = math.min(
                count,
                math.min(
                    matrices.Length,
                    math.min(metadata.Length, math.min(types.Length, semanticTypes.Length))));
            if (safeCount <= 0)
                return;

            float activateRadius = ResolveLargeFloraColliderActivateRadius();
            float activateRadiusSq = activateRadius * activateRadius;
            int scanBudget = math.min(ResolveLargeFloraColliderScanBudget(), safeCount);
            for (int step = 0; step < scanBudget; step++)
            {
                int sourceIndex = _largeFloraColliderScanCursor;
                _largeFloraColliderScanCursor++;
                if (_largeFloraColliderScanCursor >= safeCount)
                    _largeFloraColliderScanCursor = 0;

                if (!IsLargeCoralCollisionProxyCandidate(semanticTypes[sourceIndex]))
                    continue;

                if (!VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds(
                    matrices[sourceIndex],
                    metadata[sourceIndex],
                    types[sourceIndex],
                    semanticTypes[sourceIndex],
                    out float3 center,
                    out float3 extents))
                {
                    continue;
                }

                Vector3 centerRuntime = new Vector3(center.x, center.y, center.z);
                Vector3 centerUniverse = ToUniverseSpace(centerRuntime);
                Vector3 delta = centerUniverse - playerUniverse;
                if (delta.sqrMagnitude > activateRadiusSq)
                    continue;

                ActivateOrUpdateLargeFloraCollisionProxy(sourceIndex, centerRuntime, centerUniverse, extents);
            }
        }

        private static bool IsLargeCoralCollisionProxyCandidate(int semanticType)
        {
            return HectonMapMagicVegetationBridge.IsColonyCoralSemanticType(
                (VegetationSemanticType)semanticType);
        }

        private void DeactivateDistantLargeFloraCollisionProxies(Vector3 playerUniverse)
        {
            if (_largeFloraColliderProxies == null ||
                _largeFloraColliderUniverseCenters == null)
            {
                return;
            }

            float activateRadius = ResolveLargeFloraColliderActivateRadius();
            float deactivateRadius = math.max(ResolveLargeFloraColliderDeactivateRadius(), activateRadius + 0.5f);
            float deactivateRadiusSq = deactivateRadius * deactivateRadius;
            bool lookupDirty = false;
            for (int i = 0; i < _largeFloraColliderProxies.Length; i++)
            {
                GameObject proxy = _largeFloraColliderProxies[i];
                if (proxy == null)
                    continue;

                Vector3 proxyUniverse = _largeFloraColliderUniverseCenters[i];
                Vector3 delta = proxyUniverse - playerUniverse;
                if (delta.sqrMagnitude > deactivateRadiusSq)
                {
                    DeactivateLargeFloraCollisionProxySlot(i, false);
                    lookupDirty = true;
                }
            }

            if (lookupDirty)
                RebuildLargeFloraColliderSlotLookup();
        }

        private void RebaseLargeFloraCollisionProxyRuntimePositions()
        {
            if (_largeFloraColliderProxies == null || _largeFloraColliderUniverseCenters == null)
                return;

            for (int i = 0; i < _largeFloraColliderProxies.Length; i++)
            {
                GameObject proxy = _largeFloraColliderProxies[i];
                if (proxy == null)
                    continue;

                Vector3 runtimeCenter = ToRuntimeSpace(_largeFloraColliderUniverseCenters[i]);
                Transform proxyTransform = proxy.transform;
                proxyTransform.SetPositionAndRotation(runtimeCenter, Quaternion.identity);
            }
        }

        private void ActivateOrUpdateLargeFloraCollisionProxy(int sourceIndex, Vector3 centerRuntime, Vector3 centerUniverse, float3 extents)
        {
            if (!TryFindLargeFloraCollisionProxySlot(sourceIndex, out int slot))
                slot = FindFreeLargeFloraCollisionProxySlot();

            if (slot < 0)
                return;

            GameObject proxy = _largeFloraColliderProxies[slot];
            if (proxy == null)
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool == null)
                    return;

                GameObject proxyPrefab = GetLargeFloraColliderProxyPrefabNoAlloc();
                if (proxyPrefab == null)
                    return;

                proxy = pool.Spawn(proxyPrefab, centerRuntime, Quaternion.identity);
                if (proxy == null)
                    return;

                if (!proxy.TryGetComponent(out BoxCollider box))
                {
                    pool.Despawn(proxy);
                    return;
                }

                _largeFloraColliderProxies[slot] = proxy;
                _largeFloraColliderBoxes[slot] = box;
                _largeFloraColliderSourceIndices[slot] = sourceIndex;
                RegisterLargeFloraColliderSlot(sourceIndex, slot);
            }
            else if (_largeFloraColliderSourceIndices != null && slot < _largeFloraColliderSourceIndices.Length)
            {
                _largeFloraColliderSourceIndices[slot] = sourceIndex;
                RegisterLargeFloraColliderSlot(sourceIndex, slot);
            }

            if (_largeFloraColliderUniverseCenters != null && slot < _largeFloraColliderUniverseCenters.Length)
                _largeFloraColliderUniverseCenters[slot] = centerUniverse;

            float3 size = math.max(extents * 2f, new float3(LargeFloraColliderMinimumScale));
            Transform proxyTransform = proxy.transform;
            proxyTransform.SetPositionAndRotation(centerRuntime, Quaternion.identity);
            proxyTransform.localScale = new Vector3(size.x, size.y, size.z);

            BoxCollider collider = _largeFloraColliderBoxes != null ? _largeFloraColliderBoxes[slot] : null;
            if (collider != null)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
                collider.enabled = true;
            }
        }

        private bool TryFindLargeFloraCollisionProxySlot(int sourceIndex, out int slot)
        {
            slot = -1;
            if (sourceIndex < 0 ||
                _largeFloraColliderLookupSourceIndices == null ||
                _largeFloraColliderLookupSlots == null)
            {
                return false;
            }

            int length = math.min(_largeFloraColliderLookupSourceIndices.Length, _largeFloraColliderLookupSlots.Length);
            if (length <= 0)
                return false;

            int lookupIndex = HashLargeFloraColliderSourceIndex(sourceIndex, length);
            for (int probe = 0; probe < length; probe++)
            {
                int key = _largeFloraColliderLookupSourceIndices[lookupIndex];
                if (key == sourceIndex)
                {
                    int resolvedSlot = _largeFloraColliderLookupSlots[lookupIndex];
                    if (IsLargeFloraColliderSlotLiveForSource(resolvedSlot, sourceIndex))
                    {
                        slot = resolvedSlot;
                        return true;
                    }

                    RebuildLargeFloraColliderSlotLookup();
                    return false;
                }

                if (key < 0)
                    return false;

                lookupIndex = (lookupIndex + 1) & (length - 1);
            }

            return false;
        }

        private int FindFreeLargeFloraCollisionProxySlot()
        {
            if (_largeFloraColliderProxies == null)
                return -1;

            for (int i = 0; i < _largeFloraColliderProxies.Length; i++)
            {
                if (_largeFloraColliderProxies[i] == null)
                    return i;
            }

            return -1;
        }

        private void ClearLargeFloraCollisionProxies()
        {
            if (_largeFloraColliderProxies == null)
            {
                ResetLargeFloraColliderSlotLookup();
                return;
            }

            for (int i = 0; i < _largeFloraColliderProxies.Length; i++)
                DeactivateLargeFloraCollisionProxySlot(i, false);

            ResetLargeFloraColliderSlotLookup();
        }

        private void DeactivateLargeFloraCollisionProxySlot(int slot, bool rebuildLookup = true)
        {
            if (_largeFloraColliderProxies == null || slot < 0 || slot >= _largeFloraColliderProxies.Length)
                return;

            GameObject proxy = _largeFloraColliderProxies[slot];
            if (proxy != null)
            {
                BoxCollider collider = _largeFloraColliderBoxes != null ? _largeFloraColliderBoxes[slot] : null;
                if (collider != null)
                    collider.enabled = false;

                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Despawn(proxy);
                else
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(proxy);
                    else
#endif
                        Destroy(proxy);
                }
            }

            _largeFloraColliderProxies[slot] = null;
            if (_largeFloraColliderBoxes != null && slot < _largeFloraColliderBoxes.Length)
                _largeFloraColliderBoxes[slot] = null;
            if (_largeFloraColliderSourceIndices != null && slot < _largeFloraColliderSourceIndices.Length)
                _largeFloraColliderSourceIndices[slot] = -1;
            if (_largeFloraColliderUniverseCenters != null && slot < _largeFloraColliderUniverseCenters.Length)
                _largeFloraColliderUniverseCenters[slot] = default;
            if (rebuildLookup)
                RebuildLargeFloraColliderSlotLookup();
        }

        private void RegisterLargeFloraColliderSlot(int sourceIndex, int slot)
        {
            if (sourceIndex < 0 ||
                slot < 0 ||
                _largeFloraColliderLookupSourceIndices == null ||
                _largeFloraColliderLookupSlots == null)
            {
                return;
            }

            int length = math.min(_largeFloraColliderLookupSourceIndices.Length, _largeFloraColliderLookupSlots.Length);
            if (length <= 0)
                return;

            int lookupIndex = HashLargeFloraColliderSourceIndex(sourceIndex, length);
            for (int probe = 0; probe < length; probe++)
            {
                int key = _largeFloraColliderLookupSourceIndices[lookupIndex];
                if (key < 0 || key == sourceIndex)
                {
                    _largeFloraColliderLookupSourceIndices[lookupIndex] = sourceIndex;
                    _largeFloraColliderLookupSlots[lookupIndex] = slot;
                    return;
                }

                lookupIndex = (lookupIndex + 1) & (length - 1);
            }
        }

        private void RebuildLargeFloraColliderSlotLookup()
        {
            ResetLargeFloraColliderSlotLookup();
            if (_largeFloraColliderProxies == null || _largeFloraColliderSourceIndices == null)
                return;

            int length = math.min(_largeFloraColliderProxies.Length, _largeFloraColliderSourceIndices.Length);
            for (int slot = 0; slot < length; slot++)
            {
                if (_largeFloraColliderProxies[slot] == null)
                    continue;

                int sourceIndex = _largeFloraColliderSourceIndices[slot];
                if (sourceIndex >= 0)
                    RegisterLargeFloraColliderSlot(sourceIndex, slot);
            }
        }

        private void ResetLargeFloraColliderSlotLookup()
        {
            if (_largeFloraColliderLookupSourceIndices != null)
            {
                for (int i = 0; i < _largeFloraColliderLookupSourceIndices.Length; i++)
                    _largeFloraColliderLookupSourceIndices[i] = -1;
            }

            if (_largeFloraColliderLookupSlots != null)
            {
                for (int i = 0; i < _largeFloraColliderLookupSlots.Length; i++)
                    _largeFloraColliderLookupSlots[i] = -1;
            }
        }

        private bool IsLargeFloraColliderSlotLiveForSource(int slot, int sourceIndex)
        {
            return _largeFloraColliderProxies != null &&
                   _largeFloraColliderSourceIndices != null &&
                   slot >= 0 &&
                   slot < _largeFloraColliderProxies.Length &&
                   slot < _largeFloraColliderSourceIndices.Length &&
                   _largeFloraColliderProxies[slot] != null &&
                   _largeFloraColliderSourceIndices[slot] == sourceIndex;
        }

        private static int HashLargeFloraColliderSourceIndex(int sourceIndex, int lookupCapacity)
        {
            return (int)(((uint)sourceIndex * LargeFloraColliderLookupHash) & (uint)(lookupCapacity - 1));
        }

        private GameObject GetLargeFloraColliderProxyPrefabNoAlloc()
        {
            if (largeFloraColliderProxyPrefab != null)
                return largeFloraColliderProxyPrefab;

            return _largeFloraColliderRuntimeProxyPrefab;
        }

        private void EnsureLargeFloraColliderRuntimeProxyPrefab()
        {
            if (largeFloraColliderProxyPrefab != null || _largeFloraColliderRuntimeProxyPrefab != null)
                return;

            // COLD ALLOC: GameObject[1] + BoxCollider[1] — runtime fallback proxy prefab for large flora collision pool — owner: HectonMapMagicVegetationBridge
            _largeFloraColliderRuntimeProxyPrefab = new GameObject("PFB_Runtime_LargeFloraColliderProxy")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            BoxCollider collider = _largeFloraColliderRuntimeProxyPrefab.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            collider.enabled = false;
            _largeFloraColliderRuntimeProxyPrefab.SetActive(false);
        }

        private void DestroyLargeFloraRuntimeProxyPrefab()
        {
            if (_largeFloraColliderRuntimeProxyPrefab == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null)
                pool.ClearPool(_largeFloraColliderRuntimeProxyPrefab);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_largeFloraColliderRuntimeProxyPrefab);
            else
#endif
                Destroy(_largeFloraColliderRuntimeProxyPrefab);

            _largeFloraColliderRuntimeProxyPrefab = null;
        }

        private int ResolveLargeFloraColliderProxyCapacity()
        {
            return math.clamp(largeFloraColliderProxyCapacity, 1, 64);
        }

        private static int ResolveLargeFloraColliderLookupCapacity(int proxyCapacity)
        {
            int target = math.max(8, proxyCapacity * 2);
            int capacity = 8;
            while (capacity < target && capacity < 256)
                capacity <<= 1;

            return capacity;
        }

        private int ResolveLargeFloraColliderScanBudget()
        {
            return math.clamp(largeFloraColliderScanBudgetPerTick, 1, 512);
        }

        private float ResolveLargeFloraColliderActivateRadius()
        {
            return math.isfinite(largeFloraColliderActivateRadius)
                ? math.max(0.5f, largeFloraColliderActivateRadius)
                : 10f;
        }

        private float ResolveLargeFloraColliderDeactivateRadius()
        {
            return math.isfinite(largeFloraColliderDeactivateRadius)
                ? math.max(1f, largeFloraColliderDeactivateRadius)
                : 12f;
        }
    }
}

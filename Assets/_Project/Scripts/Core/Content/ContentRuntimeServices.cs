using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Optimization;
using Unity.Collections;
using UnityEngine;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.Core.Content
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
    public struct ContentBundleRefState
    {
        public uint Hash;
        public int RefCount;
        public long Bytes;
        public int LastAccessFrame;
        public byte BiomeId;
        public ContentTier Tier;
        public byte IsBiomeCache;
        public byte Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ContentAuthorityTelemetryEntry
    {
        public uint Frame;
        public uint Flags;
        public uint FocusHash;
        public int PendingLoads;
        public int HologramsActive;
        public int BundleRefCount;
        public long EstimatedVramBytes;
        public float VramPressure01;
        public float RamPressure01;
        public uint StateHash;
        public uint Reserved0;
    }

    /// <summary>
    /// Fixed-capacity bundle reference counter. Duplicate loads resolve to ref increments, not second handles.
    /// </summary>
    public sealed class ContentBundleReferenceCounter
    {
        private readonly ContentBundleRefState[] _states;
        private int _count;

        public ContentBundleReferenceCounter(int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            // COLD ALLOC: ContentBundleRefState[safeCapacity] - strict bundle ref table - owner: ContentBundleReferenceCounter
            _states = new ContentBundleRefState[safeCapacity];
        }

        public int Count => _count;

        public bool Acquire(uint hash, long bytes, byte biomeId, ContentTier tier, bool isBiomeCache, int frame)
        {
            if (hash == 0u)
                return false;

            for (int i = 0; i < _count; i++)
            {
                if (_states[i].Hash != hash)
                    continue;

                ContentBundleRefState state = _states[i];
                state.RefCount++;
                state.LastAccessFrame = frame;
                if (bytes > state.Bytes)
                    state.Bytes = bytes;
                if (isBiomeCache)
                    state.IsBiomeCache = 1;
                _states[i] = state;
                return true;
            }

            if (_count >= _states.Length)
                return false;

            _states[_count] = new ContentBundleRefState
            {
                Hash = hash,
                RefCount = 1,
                Bytes = bytes > 0L ? bytes : 0L,
                LastAccessFrame = frame,
                BiomeId = biomeId,
                Tier = tier,
                IsBiomeCache = isBiomeCache ? (byte)1 : (byte)0
            };
            _count++;
            return true;
        }

        public bool Release(uint hash, int frame, out bool becameUnused)
        {
            becameUnused = false;
            for (int i = 0; i < _count; i++)
            {
                if (_states[i].Hash != hash)
                    continue;

                ContentBundleRefState state = _states[i];
                state.RefCount--;
                if (state.RefCount < 0)
                    state.RefCount = 0;

                state.LastAccessFrame = frame;
                becameUnused = state.RefCount == 0;
                _states[i] = state;
                return true;
            }

            return false;
        }

        public bool TrySelectOldestUnusedBiomeCache(out uint hash)
        {
            hash = 0u;
            int bestIndex = -1;
            int bestFrame = int.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                ContentBundleRefState state = _states[i];
                if (state.IsBiomeCache == 0 || state.RefCount != 0)
                    continue;

                if (state.LastAccessFrame >= bestFrame)
                    continue;

                bestFrame = state.LastAccessFrame;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return false;

            hash = _states[bestIndex].Hash;
            return true;
        }

        public bool Remove(uint hash)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_states[i].Hash != hash)
                    continue;

                int last = _count - 1;
                _states[i] = _states[last];
                _states[last] = default;
                _count--;
                return true;
            }

            return false;
        }

        public long EstimateResidentBytes()
        {
            long total = 0L;
            for (int i = 0; i < _count; i++)
            {
                long bytes = _states[i].Bytes;
                if (bytes <= 0L)
                    continue;

                if (total > long.MaxValue - bytes)
                    return long.MaxValue;

                total += bytes;
            }
            return total;
        }
    }

    [CreateAssetMenu(menuName = "HECTON-8/Content/VFX Prewarm Manifest", fileName = "ContentVfxPrewarmManifest")]
    public sealed class ContentVfxPrewarmManifest : ScriptableObject
    {
#if UNITY_ADDRESSABLES_EXIST
        [SerializeField] private AssetReference[] particleSystems = Array.Empty<AssetReference>();
        [SerializeField] private AssetReference[] computeShaders = Array.Empty<AssetReference>();

        public int ParticleSystemCount => particleSystems != null ? particleSystems.Length : 0;
        public int ComputeShaderCount => computeShaders != null ? computeShaders.Length : 0;
        public AssetReference GetParticleSystem(int index) => particleSystems[index];
        public AssetReference GetComputeShader(int index) => computeShaders[index];
#else
        public int ParticleSystemCount => 0;
        public int ComputeShaderCount => 0;
#endif
    }

    /// <summary>
    /// Runtime content authority: async proxy rendering, bundle ref counting, VFX prewarm, VRAM intercept, and AUP-shift cleanup.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8009)]
    public sealed class ContentAuthorityRuntime : MonoBehaviour, IUpdatable
    {
        private const float GhostProxyDelaySeconds = 0.1f;
        private const long HardVramCeilingBytes = 1800L * 1024L * 1024L;
        private const uint VramInterceptFlag = 1u << 0;
        private const uint AupCleanupFlag = 1u << 1;
        private const uint HologramFlag = 1u << 2;
        private const uint NonFiniteFlag = 1u << 3;
        private const uint VramLedgerOwnerHash = 0xC0A77A57u;
        private const int TelemetryCapacity = 300;

        [SerializeField] private ContentAssetHashMap assetHashMap;
        [SerializeField] private Mesh hologramProxyMesh;
        [SerializeField] private Material hologramMaterial;
        [SerializeField] private int hologramPoolCapacity = 16;
        [SerializeField] private ContentVfxPrewarmManifest vfxPrewarmManifest;
        [SerializeField] private bool startVfxPrewarmOnEnable = true;

        // COLD ALLOC: List<PendingLoad>[64] - async load timeout ledger - owner: ContentAuthorityRuntime
        private readonly List<PendingLoad> _pendingLoads = new List<PendingLoad>(64);
        // COLD ALLOC: ContentBundleReferenceCounter[256] - duplicate bundle load guard - owner: ContentAuthorityRuntime
        private readonly ContentBundleReferenceCounter _bundleRefs = new ContentBundleReferenceCounter(256);
#if UNITY_ADDRESSABLES_EXIST
        // COLD ALLOC: List<AsyncOperationHandle>[64] - VFX prewarm handle ledger - owner: ContentAuthorityRuntime
        private readonly List<AsyncOperationHandle> _vfxPrewarmHandles = new List<AsyncOperationHandle>(64);
#endif
        private IDataVault _dataVault;
        private VaultBufferHandle<ContentAuthorityTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private GameObject[] _hologramPool;
        private Renderer[] _hologramRenderers;
        private bool _registeredTick;
        private bool _vfxPrewarmStarted;
        private int _nextHologramIndex;
        private int _hologramsActive;

        public ContentAssetHashMap AssetHashMap => assetHashMap;
        public ContentBundleReferenceCounter BundleReferenceCounter => _bundleRefs;

        private void Awake()
        {
            int capacity = Mathf.Max(1, hologramPoolCapacity);
            // COLD ALLOC: GameObject[capacity] - hidden hologram proxy pool - owner: ContentAuthorityRuntime
            _hologramPool = new GameObject[capacity];
            // COLD ALLOC: Renderer[capacity] - hidden hologram proxy renderers - owner: ContentAuthorityRuntime
            _hologramRenderers = new Renderer[capacity];
            BuildHologramPool(capacity);
        }

        private void OnEnable()
        {
            TryRegister();
            if (startVfxPrewarmOnEnable)
                StartVfxPrewarm();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

#if UNITY_ADDRESSABLES_EXIST
            for (int i = 0; i < _vfxPrewarmHandles.Count; i++)
            {
                if (_vfxPrewarmHandles[i].IsValid())
                    Addressables.Release(_vfxPrewarmHandles[i]);
            }
            _vfxPrewarmHandles.Clear();
#endif
            for (int i = 0; i < _hologramPool.Length; i++)
            {
                if (_hologramPool[i] != null)
                    Destroy(_hologramPool[i]);
            }

            ClearVaultHandles();
        }

        public void Tick(float deltaTime)
        {
            uint flags = 0u;
            TickPendingLoads(ref flags);
            TickAupShiftCleanup(ref flags);
            TickVramIntercept(ref flags);
            TickVfxPrewarm();
            WriteTelemetry(flags);
        }

        public bool RegisterBundleAcquire(uint hash)
        {
            if (assetHashMap == null || !assetHashMap.TryGetEntry(hash, out ContentAssetEntry entry))
                return false;

            bool accepted = _bundleRefs.Acquire(
                hash,
                entry.EstimatedVramBytes,
                entry.BiomeId,
                entry.Tier,
                entry.IsBiomeCache,
                Time.frameCount);

            if (accepted)
                VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());

            return accepted;
        }

        public bool RegisterBundleRelease(uint hash)
        {
            bool released = _bundleRefs.Release(hash, Time.frameCount, out bool becameUnused);
            if (released && becameUnused)
                VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());

            return released;
        }

        public bool TrackAsyncLoad(uint hash, Renderer targetRenderer)
        {
            if (hash == 0u || targetRenderer == null)
                return false;

            for (int i = 0; i < _pendingLoads.Count; i++)
            {
                PendingLoad pending = _pendingLoads[i];
                if (pending.Hash != hash || pending.Target != targetRenderer)
                    continue;

                return true;
            }

            _pendingLoads.Add(new PendingLoad
            {
                Hash = hash,
                StartTime = Time.unscaledTime,
                Target = targetRenderer,
                HologramIndex = -1
            });
            return true;
        }

        public bool CompleteAsyncLoad(uint hash, Renderer targetRenderer)
        {
            for (int i = _pendingLoads.Count - 1; i >= 0; i--)
            {
                PendingLoad pending = _pendingLoads[i];
                if (pending.Hash != hash || pending.Target != targetRenderer)
                    continue;

                HideHologram(pending.HologramIndex);
                RemovePendingLoadAt(i);
                return true;
            }

            return false;
        }

        public bool TryResolveContentEntry(uint hash, out ContentAssetEntry entry)
        {
            if (assetHashMap == null)
            {
                entry = default;
                return false;
            }

            return assetHashMap.TryGetEntry(hash, out entry);
        }

        public void StartVfxPrewarm()
        {
            if (_vfxPrewarmStarted || vfxPrewarmManifest == null)
                return;

            _vfxPrewarmStarted = true;
#if UNITY_ADDRESSABLES_EXIST
            for (int i = 0; i < vfxPrewarmManifest.ParticleSystemCount; i++)
            {
                AssetReference reference = vfxPrewarmManifest.GetParticleSystem(i);
                if (reference == null || !reference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle handle = reference.LoadAssetAsync<ParticleSystem>();
                _vfxPrewarmHandles.Add(handle);
            }

            for (int i = 0; i < vfxPrewarmManifest.ComputeShaderCount; i++)
            {
                AssetReference reference = vfxPrewarmManifest.GetComputeShader(i);
                if (reference == null || !reference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle handle = reference.LoadAssetAsync<ComputeShader>();
                _vfxPrewarmHandles.Add(handle);
            }
#endif
        }

        private void BuildHologramPool(int capacity)
        {
            if (hologramProxyMesh == null || hologramMaterial == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[ContentAuthorityRuntime] Hologram proxy mesh/material missing.", this);
#endif
                return;
            }

            for (int i = 0; i < capacity; i++)
            {
                GameObject proxy = new GameObject("GEN_ContentHologramProxy");
                proxy.hideFlags = HideFlags.HideAndDontSave;
                proxy.transform.SetParent(transform, false);
                MeshFilter filter = proxy.AddComponent<MeshFilter>();
                filter.sharedMesh = hologramProxyMesh;
                MeshRenderer renderer = proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = hologramMaterial;
                proxy.SetActive(false);
                _hologramPool[i] = proxy;
                _hologramRenderers[i] = renderer;
            }
        }

        private void TickPendingLoads(ref uint flags)
        {
            if (_pendingLoads.Count == 0)
                return;

            float now = Time.unscaledTime;
            for (int i = 0; i < _pendingLoads.Count; i++)
            {
                PendingLoad pending = _pendingLoads[i];
                if (pending.HologramIndex >= 0 || now - pending.StartTime < GhostProxyDelaySeconds)
                    continue;

                pending.HologramIndex = ShowHologram(pending.Target);
                _pendingLoads[i] = pending;
                flags |= HologramFlag;
            }
        }

        private int ShowHologram(Renderer target)
        {
            if (target == null || _hologramPool == null || _hologramPool.Length == 0)
                return -1;

            int index = _nextHologramIndex;
            _nextHologramIndex = (_nextHologramIndex + 1) % _hologramPool.Length;
            GameObject proxy = _hologramPool[index];
            if (proxy == null)
                return -1;

            Transform targetTransform = target.transform;
            Transform proxyTransform = proxy.transform;
            proxyTransform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
            proxyTransform.localScale = targetTransform.lossyScale;
            if (!proxy.activeSelf)
            {
                proxy.SetActive(true);
                _hologramsActive++;
            }

            return index;
        }

        private void HideHologram(int index)
        {
            if (index < 0 || _hologramPool == null || index >= _hologramPool.Length)
                return;

            GameObject proxy = _hologramPool[index];
            if (proxy == null || !proxy.activeSelf)
                return;

            proxy.SetActive(false);
            if (_hologramsActive > 0)
                _hologramsActive--;
        }

        private void RemovePendingLoadAt(int index)
        {
            int last = _pendingLoads.Count - 1;
            _pendingLoads[index] = _pendingLoads[last];
            _pendingLoads.RemoveAt(last);
        }

        private void TickAupShiftCleanup(ref uint flags)
        {
            if (SignalBusRegistry.SystemStress01 <= 0.8f)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shifts.Length == 0)
                return;

            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
            {
                governor.ForceDrainPendingReleaseQueue();
                governor.EvictLowestPriorityUnusedAssets(2, AssetPriorityTier.Tier5DistantHlod);
            }

            flags |= AupCleanupFlag;
        }

        private void TickVramIntercept(ref uint flags)
        {
            VRAMMonitor monitor = GlobalRegistry.VRAMMonitor;
            if (monitor == null)
                return;

            long projectedBytes = monitor.TotalVRAMBytes + _bundleRefs.EstimateResidentBytes();
            if (projectedBytes <= HardVramCeilingBytes)
                return;

            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (_bundleRefs.TrySelectOldestUnusedBiomeCache(out uint hash))
            {
                _bundleRefs.Remove(hash);
                VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
            }

            if (governor != null)
            {
                governor.ForceDrainPendingReleaseQueue();
                governor.EvictLowestPriorityUnusedAssets(1, AssetPriorityTier.Tier5DistantHlod);
            }

            flags |= VramInterceptFlag;
        }

        private void TickVfxPrewarm()
        {
#if UNITY_ADDRESSABLES_EXIST
            for (int i = _vfxPrewarmHandles.Count - 1; i >= 0; i--)
            {
                AsyncOperationHandle handle = _vfxPrewarmHandles[i];
                if (!handle.IsDone)
                    continue;

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result is ParticleSystem particleSystem)
                    particleSystem.Simulate(0f, true, true, true);
            }
#endif
        }

        private unsafe void WriteTelemetry(uint flags)
        {
            VRAMPressureMonitor pressure = GlobalRegistry.VRAMPressure;
            long estimate = _bundleRefs.EstimateResidentBytes();
            float rawVramPressure = pressure != null ? pressure.VramPressureFactor : 0f;
            float rawRamPressure = pressure != null ? pressure.RamPressureFactor : 0f;
            bool nonFinite = !IsFinite(rawVramPressure) || !IsFinite(rawRamPressure);
            float vramPressure = Sanitize01(rawVramPressure);
            float ramPressure = Sanitize01(rawRamPressure);
            if (nonFinite)
                flags |= NonFiniteFlag;

            uint stateHash = unchecked((uint)_pendingLoads.Count * 73856093u) ^
                             unchecked((uint)_bundleRefs.Count * 19349663u) ^
                             unchecked((uint)_hologramsActive * 83492791u);

            if (!TryResolveTelemetryPointer(out ContentAuthorityTelemetryEntry* telemetry, out int* cursorPtr))
                return;

            int cursor = *cursorPtr;
            if ((uint)cursor >= TelemetryCapacity)
                cursor = 0;

            telemetry[cursor] = new ContentAuthorityTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                PendingLoads = _pendingLoads.Count,
                HologramsActive = _hologramsActive,
                BundleRefCount = _bundleRefs.Count,
                EstimatedVramBytes = estimate,
                VramPressure01 = vramPressure,
                RamPressure01 = ramPressure,
                StateHash = stateHash
            };
            cursor++;
            if (cursor >= TelemetryCapacity)
                cursor = 0;
            *cursorPtr = cursor;

            if (nonFinite)
                DumpBlackBox();
        }

        private unsafe bool TryResolveTelemetryPointer(
            out ContentAuthorityTelemetryEntry* telemetry,
            out int* cursor)
        {
            telemetry = null;
            cursor = null;

            if (!EnsureTelemetry())
                return false;

            telemetry = (ContentAuthorityTelemetryEntry*)_telemetryHandle.ResolvePointer(_dataVault);
            cursor = (int*)_telemetryCursorHandle.ResolvePointer(_dataVault);
            return telemetry != null && cursor != null;
        }

        private bool EnsureTelemetry()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _dataVault = vault;
            if (!_telemetryHandle.IsCreated || !vault.ResolveBuffer(ref _telemetryHandle))
            {
                _telemetryHandle = vault.GetBufferHandle<ContentAuthorityTelemetryEntry>(
                    BufferID.ContentAuthorityBlackBox,
                    TelemetryCapacity,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_telemetryCursorHandle.IsCreated || !vault.ResolveBuffer(ref _telemetryCursorHandle))
            {
                _telemetryCursorHandle = vault.GetBufferHandle<int>(
                    BufferID.ContentAuthorityTelemetryCursor,
                    1,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            return _telemetryHandle.IsCreated &&
                   _telemetryHandle.Length >= TelemetryCapacity &&
                   _telemetryCursorHandle.IsCreated &&
                   _telemetryCursorHandle.Length >= 1;
        }

        private void ClearVaultHandles()
        {
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _dataVault = null;
        }

        private unsafe void DumpBlackBox()
        {
            if (!TryResolveTelemetryPointer(out ContentAuthorityTelemetryEntry* telemetry, out int* cursorPtr))
                return;

            int cursor = *cursorPtr;
            if ((uint)cursor >= TelemetryCapacity)
                cursor = 0;

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs/AgentLogs/Dump_CONTENT_AUTHORITY_DICTATOR.bin");
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    int index = cursor + i;
                    if (index >= TelemetryCapacity)
                        index -= TelemetryCapacity;

                    ContentAuthorityTelemetryEntry entry = telemetry[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.FocusHash);
                    writer.Write(entry.PendingLoads);
                    writer.Write(entry.HologramsActive);
                    writer.Write(entry.BundleRefCount);
                    writer.Write(entry.EstimatedVramBytes);
                    writer.Write(entry.VramPressure01);
                    writer.Write(entry.RamPressure01);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Reserved0);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Sanitize01(float value)
        {
            if (!IsFinite(value))
                return 0f;
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private void TryRegister()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private struct PendingLoad
        {
            public uint Hash;
            public float StartTime;
            public Renderer Target;
            public int HologramIndex;
        }
    }

    public static class ContentTieredGroupPolicy
    {
        public const uint VisualFeatureSaltCrystals = 1u << 0;
        public const uint VisualFeatureVolumetricSiltWake = 1u << 1;
        public const uint VisualFeatureProceduralHullDents = 1u << 2;
        public const uint VisualFeatureRaymarchDetail = 1u << 3;
        public const uint VisualFeatureParallaxOcclusion16Tap = 1u << 4;
        public const uint DearLieOneDimensionalLut = 1u << 16;
        public const uint DearLieTriangleNoise = 1u << 17;
        public const uint DearLieDotProductVision = 1u << 18;

        public static bool CanDownload(ContentTier tier)
        {
            if (tier != ContentTier.Overkill)
                return true;

            if (HectonXRRuntimeState.IsXRActive)
                return false;

            int graphicsMemory = SystemInfo.graphicsMemorySize;
            return graphicsMemory > 4096;
        }

        public static ContentTier ResolveMaximumRuntimeTier()
        {
            if (HectonXRRuntimeState.IsXRActive || SystemInfo.graphicsMemorySize <= 2048)
                return ContentTier.HighRes;

            return SystemInfo.graphicsMemorySize > 4096 ? ContentTier.Overkill : ContentTier.HighRes;
        }

        public static uint ResolveVisualFeatureMask(ContentTier tier)
        {
            if (HectonXRRuntimeState.IsXRActive || SystemInfo.graphicsMemorySize <= 2048)
            {
                return DearLieOneDimensionalLut |
                       DearLieTriangleNoise |
                       DearLieDotProductVision;
            }

            if (tier == ContentTier.Overkill && SystemInfo.graphicsMemorySize > 4096)
            {
                return VisualFeatureSaltCrystals |
                       VisualFeatureVolumetricSiltWake |
                       VisualFeatureProceduralHullDents |
                       VisualFeatureRaymarchDetail |
                       VisualFeatureParallaxOcclusion16Tap;
            }

            return VisualFeatureSaltCrystals |
                   VisualFeatureVolumetricSiltWake |
                   DearLieTriangleNoise;
        }
    }
}

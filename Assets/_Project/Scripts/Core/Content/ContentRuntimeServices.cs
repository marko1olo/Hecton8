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
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct ContentPendingLoadState
    {
        public uint Hash;
        public float StartTime;
        public int HologramIndex;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct ContentVisualFeatureBudget
    {
        public uint FeatureMask;
        public ushort MaxParticles;
        public byte RaymarchSteps;
        public byte PomTaps;
        public byte SiltWakeLayers;
        public byte SaltCrystalLayers;
        public byte HullDentOctaves;
        public byte Reserved0;
        public int Reserved1;
    }

    /// <summary>
    /// Fixed-capacity bundle reference counter. Duplicate loads resolve to ref increments, not second handles.
    /// </summary>
    public sealed class ContentBundleReferenceCounter
    {
        private readonly int _capacity;
        private IDataVault _vault;
        private VaultBufferHandle<ContentBundleRefState> _statesHandle;
        private VaultBufferHandle<int> _countHandle;

        public ContentBundleReferenceCounter(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
        }

        public unsafe int Count
        {
            get
            {
                return TryResolveNormalized(
                    out ContentBundleRefState* _,
                    out int* _,
                    out int count)
                    ? count
                    : 0;
            }
        }

        public void BindVault(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            _statesHandle = default;
            _countHandle = default;
        }

        public unsafe bool Acquire(uint hash, long bytes, byte biomeId, ContentTier tier, bool isBiomeCache, int frame)
        {
            if (hash == 0u)
                return false;

            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* countPtr, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (states[i].Hash != hash)
                    continue;

                ContentBundleRefState state = states[i];
                if (state.RefCount < 0 || state.RefCount == int.MaxValue)
                {
                    LogRefCountViolation(hash);
                    return false;
                }

                state.RefCount++;
                state.LastAccessFrame = frame;
                if (bytes > state.Bytes)
                    state.Bytes = bytes;
                if (isBiomeCache)
                    state.IsBiomeCache = 1;
                states[i] = state;
                return true;
            }

            if (count >= _capacity)
                return false;

            states[count] = new ContentBundleRefState
            {
                Hash = hash,
                RefCount = 1,
                Bytes = bytes > 0L ? bytes : 0L,
                LastAccessFrame = frame,
                BiomeId = biomeId,
                Tier = tier,
                IsBiomeCache = isBiomeCache ? (byte)1 : (byte)0
            };
            *countPtr = count + 1;
            return true;
        }

        public unsafe bool Release(uint hash, int frame, out bool becameUnused)
        {
            becameUnused = false;
            if (hash == 0u)
            {
                LogRefCountViolation(hash);
                return false;
            }

            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* _, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (states[i].Hash != hash)
                    continue;

                ContentBundleRefState state = states[i];
                if (state.RefCount <= 0)
                {
                    LogRefCountViolation(hash);
                    return false;
                }

                state.RefCount--;
                state.LastAccessFrame = frame;
                becameUnused = state.RefCount == 0;
                states[i] = state;
                return true;
            }

            LogRefCountViolation(hash);
            return false;
        }

        public unsafe bool TryGetState(uint hash, out ContentBundleRefState state)
        {
            state = default;
            if (hash == 0u)
                return false;

            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* _, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (states[i].Hash != hash)
                    continue;

                state = states[i];
                return true;
            }

            return false;
        }

        public unsafe bool TrySelectOldestUnusedBiomeCache(out uint hash)
        {
            hash = 0u;
            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* _, out int count))
                return false;

            int bestIndex = -1;
            int bestFrame = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                ContentBundleRefState state = states[i];
                if (state.IsBiomeCache == 0 || state.RefCount != 0)
                    continue;

                if (state.LastAccessFrame >= bestFrame)
                    continue;

                bestFrame = state.LastAccessFrame;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return false;

            hash = states[bestIndex].Hash;
            return true;
        }

        public unsafe bool Remove(uint hash)
        {
            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* countPtr, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (states[i].Hash != hash)
                    continue;

                int last = count - 1;
                states[i] = states[last];
                states[last] = default;
                *countPtr = last;
                return true;
            }

            return false;
        }

        public unsafe long EstimateResidentBytes()
        {
            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* _, out int count))
                return 0L;

            long total = 0L;
            for (int i = 0; i < count; i++)
            {
                long bytes = states[i].Bytes;
                if (bytes <= 0L)
                    continue;

                if (total > long.MaxValue - bytes)
                    return long.MaxValue;

                total += bytes;
            }
            return total;
        }

        public unsafe void Clear()
        {
            if (!TryResolve(out ContentBundleRefState* states, out int* countPtr))
                return;

            int count = *countPtr;
            if ((uint)count > (uint)_capacity)
                count = _capacity;

            for (int i = 0; i < count; i++)
                states[i] = default;

            *countPtr = 0;
        }

        private unsafe bool TryResolve(out ContentBundleRefState* states, out int* count)
        {
            states = null;
            count = null;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!_statesHandle.IsCreated || !vault.ResolveBuffer(ref _statesHandle))
            {
                _statesHandle = vault.GetBufferHandle<ContentBundleRefState>(
                    BufferID.ContentAuthorityBundleRefs,
                    _capacity,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_countHandle.IsCreated || !vault.ResolveBuffer(ref _countHandle))
            {
                _countHandle = vault.GetBufferHandle<int>(
                    BufferID.ContentAuthorityBundleRefCount,
                    1,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            states = (ContentBundleRefState*)_statesHandle.ResolvePointer(vault);
            count = (int*)_countHandle.ResolvePointer(vault);
            return states != null && count != null && _statesHandle.Length >= _capacity && _countHandle.Length >= 1;
        }

        private unsafe bool TryResolveNormalized(
            out ContentBundleRefState* states,
            out int* countPtr,
            out int count)
        {
            count = 0;
            if (!TryResolve(out states, out countPtr))
                return false;

            count = *countPtr;
            if ((uint)count <= (uint)_capacity)
                return true;

            LogLedgerCountCorruption();
            ClearResolved(states, countPtr, _capacity);
            count = 0;
            return true;
        }

        private static unsafe void ClearResolved(ContentBundleRefState* states, int* countPtr, int capacity)
        {
            for (int i = 0; i < capacity; i++)
                states[i] = default;

            *countPtr = 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogRefCountViolation(uint hash)
        {
            Debug.LogError("[ContentBundleReferenceCounter] Invalid ref-count transition for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLedgerCountCorruption()
        {
            Debug.LogError("[ContentBundleReferenceCounter] Vault ledger count exceeded fixed capacity; cleared residency ledger.");
        }
    }

    [CreateAssetMenu(menuName = "HECTON-8/Content/VFX Prewarm Manifest", fileName = "ContentVfxPrewarmManifest")]
    public sealed class ContentVfxPrewarmManifest : ScriptableObject
    {
        public const int MaxEntries = 64;

#if UNITY_ADDRESSABLES_EXIST
        [Header("Addressable VFX")]
        [Tooltip("Particle systems warmed during loading. Build validation caps total VFX handles at 64.")]
        [SerializeField] private AssetReference[] particleSystems = Array.Empty<AssetReference>();

        [Tooltip("Compute shaders loaded during loading. Build validation caps total VFX handles at 64.")]
        [SerializeField] private AssetReference[] computeShaders = Array.Empty<AssetReference>();

        public int ParticleSystemCount => particleSystems != null ? particleSystems.Length : 0;
        public int ComputeShaderCount => computeShaders != null ? computeShaders.Length : 0;
        public int TotalCount => ParticleSystemCount + ComputeShaderCount;
        public AssetReference GetParticleSystem(int index) => particleSystems[index];
        public AssetReference GetComputeShader(int index) => computeShaders[index];
#else
        public int ParticleSystemCount => 0;
        public int ComputeShaderCount => 0;
        public int TotalCount => 0;
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
        private const ulong BlackBoxMagic = 0x484543544F4E3800UL;
        private const uint BlackBoxEntrySizeBytes = 64u;
        private const int TelemetryCapacity = 300;
        public const int MaxPendingLoadCount = 64;
        private const int PendingLoadCapacity = MaxPendingLoadCount;
#if UNITY_ADDRESSABLES_EXIST
        private const int BundleHandleCapacity = 256;
#endif
        private const string BlackBoxRelativePath = "Docs/AgentLogs/Dump_CONTENT_AUTHORITY_DICTATOR.bin";
        private const string BlackBoxFallbackFileName = "Dump_CONTENT_AUTHORITY_DICTATOR.bin";

        [SerializeField] private ContentAssetHashMap assetHashMap;
        [SerializeField] private Mesh hologramProxyMesh;
        [SerializeField] private Material hologramMaterial;
        [SerializeField] private int hologramPoolCapacity = 16;
        [SerializeField] private ContentVfxPrewarmManifest vfxPrewarmManifest;
        [SerializeField] private bool startVfxPrewarmOnEnable = true;

        // COLD ALLOC: ContentBundleReferenceCounter[256] - duplicate bundle load guard - owner: ContentAuthorityRuntime
        private readonly ContentBundleReferenceCounter _bundleRefs = new ContentBundleReferenceCounter(256);
#if UNITY_ADDRESSABLES_EXIST
        // COLD ALLOC: uint[256] - content bundle handle hashes - owner: ContentAuthorityRuntime
        private readonly uint[] _bundleHandleHashes = new uint[BundleHandleCapacity];
        // COLD ALLOC: AsyncOperationHandle[256] - Addressables handles released by content authority - owner: ContentAuthorityRuntime
        private readonly AsyncOperationHandle[] _bundleHandles = new AsyncOperationHandle[BundleHandleCapacity];
        // COLD ALLOC: List<AsyncOperationHandle>[64] - VFX prewarm handle ledger - owner: ContentAuthorityRuntime
        private readonly List<AsyncOperationHandle> _vfxPrewarmHandles = new List<AsyncOperationHandle>(64);
        // COLD ALLOC: List<AsyncOperationHandle>[64] - resident prewarmed VFX handles for release - owner: ContentAuthorityRuntime
        private readonly List<AsyncOperationHandle> _vfxResidentHandles = new List<AsyncOperationHandle>(64);
#endif
        private IDataVault _dataVault;
        private VRAMMonitor _vramMonitor;
        private VRAMPressureMonitor _vramPressure;
        private AssetLifecycleGovernor _assetLifecycle;
        private VaultBufferHandle<ContentAuthorityTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<ContentPendingLoadState> _pendingLoadsHandle;
        private VaultBufferHandle<int> _pendingLoadCountHandle;
        private Renderer[] _pendingLoadTargets;
        private GameObject[] _hologramPool;
        private Renderer[] _hologramRenderers;
        private bool _registeredTick;
        private bool _vfxPrewarmStarted;
        private bool _blackBoxDumpedThisSession;
        private string _blackBoxDumpPath;
        private int _nextHologramIndex;
        private int _hologramsActive;

        public ContentAssetHashMap AssetHashMap => assetHashMap;
        public ContentBundleReferenceCounter BundleReferenceCounter => _bundleRefs;
        public int HologramPoolCapacity => hologramPoolCapacity;
        public bool HasHologramProxyBinding => hologramProxyMesh != null && hologramMaterial != null;

        private void Awake()
        {
            int capacity = Mathf.Max(1, hologramPoolCapacity);
            // COLD ALLOC: Renderer[64] - Unity object bridge for vault pending-load records - owner: ContentAuthorityRuntime
            _pendingLoadTargets = new Renderer[PendingLoadCapacity];
            // COLD ALLOC: GameObject[capacity] - hidden hologram proxy pool - owner: ContentAuthorityRuntime
            _hologramPool = new GameObject[capacity];
            // COLD ALLOC: Renderer[capacity] - hidden hologram proxy renderers - owner: ContentAuthorityRuntime
            _hologramRenderers = new Renderer[capacity];
            _blackBoxDumpPath = ResolveBlackBoxDumpPath();
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
            ClearPendingLoads();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ClearBundleResidencyState();

#if UNITY_ADDRESSABLES_EXIST
            for (int i = 0; i < _vfxPrewarmHandles.Count; i++)
            {
                if (_vfxPrewarmHandles[i].IsValid())
                    Addressables.Release(_vfxPrewarmHandles[i]);
            }
            _vfxPrewarmHandles.Clear();
            for (int i = 0; i < _vfxResidentHandles.Count; i++)
            {
                if (_vfxResidentHandles[i].IsValid())
                    Addressables.Release(_vfxResidentHandles[i]);
            }
            _vfxResidentHandles.Clear();
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
            if (_dataVault == null)
                CacheDependencies();

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

#if UNITY_ADDRESSABLES_EXIST
        /// <summary>
        /// Registers a content-owned Addressables handle. Do not pass handles already owned by AssetLifecycleGovernor.
        /// </summary>
        public bool RegisterBundleAcquire(uint hash, AsyncOperationHandle handle)
        {
            bool accepted = RegisterBundleAcquire(hash);
            if (!accepted)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                return false;
            }

            if (!handle.IsValid())
                return true;

            if (TryTrackBundleHandle(hash, handle))
                return true;

            _bundleRefs.Release(hash, Time.frameCount, out bool becameUnused);
            if (becameUnused)
                _bundleRefs.Remove(hash);
            VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
            return false;
        }
#endif

        public bool RegisterBundleRelease(uint hash)
        {
            if (_dataVault == null)
                CacheDependencies();

            bool released = _bundleRefs.Release(hash, Time.frameCount, out bool becameUnused);
            if (released && becameUnused)
            {
                bool retainAsBiomeCache = _bundleRefs.TryGetState(hash, out ContentBundleRefState state) &&
                                          state.IsBiomeCache != 0;
                if (!retainAsBiomeCache)
                {
#if UNITY_ADDRESSABLES_EXIST
                    TryReleaseTrackedBundleHandle(hash);
#endif
                    _bundleRefs.Remove(hash);
                }

                VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
            }

            return released;
        }

        public unsafe bool TrackAsyncLoad(uint hash, Renderer targetRenderer)
        {
            if (hash == 0u || targetRenderer == null)
                return false;

            if (_dataVault == null)
                CacheDependencies();

            if (!TryResolvePendingLoadsNormalized(out ContentPendingLoadState* pendingLoads, out int* countPtr, out int count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (pendingLoads[i].Hash != hash || _pendingLoadTargets[i] != targetRenderer)
                    continue;

                return true;
            }

            if (count >= PendingLoadCapacity)
                return false;

            pendingLoads[count] = new ContentPendingLoadState
            {
                Hash = hash,
                StartTime = Time.unscaledTime,
                HologramIndex = -1
            };
            _pendingLoadTargets[count] = targetRenderer;
            *countPtr = count + 1;
            return true;
        }

        public unsafe bool CompleteAsyncLoad(uint hash, Renderer targetRenderer)
        {
            if (!TryResolvePendingLoadsNormalized(out ContentPendingLoadState* pendingLoads, out int* countPtr, out int count))
                return false;

            for (int i = count - 1; i >= 0; i--)
            {
                ContentPendingLoadState pending = pendingLoads[i];
                if (pending.Hash != hash || _pendingLoadTargets[i] != targetRenderer)
                    continue;

                HideHologram(pending.HologramIndex);
                RemovePendingLoadAt(i, pendingLoads, countPtr);
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
            int dispatched = 0;
            for (int i = 0; i < vfxPrewarmManifest.ParticleSystemCount && dispatched < ContentVfxPrewarmManifest.MaxEntries; i++)
            {
                AssetReference reference = vfxPrewarmManifest.GetParticleSystem(i);
                if (reference == null || !reference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle handle = reference.LoadAssetAsync<ParticleSystem>();
                _vfxPrewarmHandles.Add(handle);
                dispatched++;
            }

            for (int i = 0; i < vfxPrewarmManifest.ComputeShaderCount && dispatched < ContentVfxPrewarmManifest.MaxEntries; i++)
            {
                AssetReference reference = vfxPrewarmManifest.GetComputeShader(i);
                if (reference == null || !reference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle handle = reference.LoadAssetAsync<ComputeShader>();
                _vfxPrewarmHandles.Add(handle);
                dispatched++;
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

        private unsafe void TickPendingLoads(ref uint flags)
        {
            if (!TryResolvePendingLoadsNormalized(out ContentPendingLoadState* pendingLoads, out int* _, out int count))
                return;

            if (count == 0)
                return;

            float now = Time.unscaledTime;
            for (int i = 0; i < count; i++)
            {
                ContentPendingLoadState pending = pendingLoads[i];
                if (pending.HologramIndex >= 0 || now - pending.StartTime < GhostProxyDelaySeconds)
                    continue;

                pending.HologramIndex = ShowHologram(_pendingLoadTargets[i]);
                pendingLoads[i] = pending;
                flags |= HologramFlag;
            }
        }

        private int ShowHologram(Renderer target)
        {
            if (target == null || _hologramPool == null || _hologramPool.Length == 0)
                return -1;

            int index = -1;
            GameObject proxy = null;
            int poolLength = _hologramPool.Length;
            for (int i = 0; i < poolLength; i++)
            {
                int candidateIndex = _nextHologramIndex + i;
                if (candidateIndex >= poolLength)
                    candidateIndex -= poolLength;

                GameObject candidate = _hologramPool[candidateIndex];
                if (candidate == null || candidate.activeSelf)
                    continue;

                index = candidateIndex;
                proxy = candidate;
                break;
            }

            if (proxy == null)
                return -1;

            _nextHologramIndex = index + 1;
            if (_nextHologramIndex >= poolLength)
                _nextHologramIndex = 0;

            Transform targetTransform = target.transform;
            Transform proxyTransform = proxy.transform;
            proxyTransform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
            proxyTransform.localScale = targetTransform.lossyScale;
            proxy.SetActive(true);
            _hologramsActive++;

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

        private unsafe void RemovePendingLoadAt(
            int index,
            ContentPendingLoadState* pendingLoads,
            int* countPtr)
        {
            int count = *countPtr;
            if ((uint)index >= (uint)count || count <= 0)
                return;

            int last = count - 1;
            pendingLoads[index] = pendingLoads[last];
            pendingLoads[last] = default;
            _pendingLoadTargets[index] = _pendingLoadTargets[last];
            _pendingLoadTargets[last] = null;
            *countPtr = last;
        }

        private unsafe void ClearPendingLoads()
        {
            if (TryResolvePendingLoadsNormalized(out ContentPendingLoadState* pendingLoads, out int* countPtr, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    HideHologram(pendingLoads[i].HologramIndex);
                    pendingLoads[i] = default;
                    _pendingLoadTargets[i] = null;
                }

                *countPtr = 0;
            }

            if (_pendingLoadTargets == null)
                return;

            for (int i = 0; i < _pendingLoadTargets.Length; i++)
                _pendingLoadTargets[i] = null;
        }

        private void TickAupShiftCleanup(ref uint flags)
        {
            if (SignalBusRegistry.SystemStress01 <= 0.8f)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shifts.Length == 0)
                return;

            AssetLifecycleGovernor governor = _assetLifecycle;
            if (governor != null)
            {
                governor.ForceDrainPendingReleaseQueue();
                governor.EvictLowestPriorityUnusedAssets(2, AssetPriorityTier.Tier5DistantHlod);
            }

            flags |= AupCleanupFlag;
        }

        private void TickVramIntercept(ref uint flags)
        {
            VRAMMonitor monitor = _vramMonitor;
            if (monitor == null)
                return;

            long projectedBytes = monitor.TotalVRAMBytes + _bundleRefs.EstimateResidentBytes();
            if (projectedBytes <= HardVramCeilingBytes)
                return;

            AssetLifecycleGovernor governor = _assetLifecycle;
            if (_bundleRefs.TrySelectOldestUnusedBiomeCache(out uint hash))
            {
#if UNITY_ADDRESSABLES_EXIST
                TryReleaseTrackedBundleHandle(hash);
#endif
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

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (handle.Result is ParticleSystem particleSystem)
                        particleSystem.Simulate(0f, true, true, true);

                    _vfxResidentHandles.Add(handle);
                }
                else if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                RemoveVfxPrewarmHandleAt(i);
            }
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool TryTrackBundleHandle(uint hash, AsyncOperationHandle handle)
        {
            if (hash == 0u || !handle.IsValid())
                return false;

            int emptyIndex = -1;
            for (int i = 0; i < _bundleHandleHashes.Length; i++)
            {
                uint slotHash = _bundleHandleHashes[i];
                if (slotHash == hash)
                {
                    AsyncOperationHandle current = _bundleHandles[i];
                    if (!current.IsValid())
                    {
                        _bundleHandles[i] = handle;
                        return true;
                    }

                    if (!current.Equals(handle))
                        Addressables.Release(handle);

                    return true;
                }

                if (emptyIndex < 0 && slotHash == 0u)
                    emptyIndex = i;
            }

            if (emptyIndex >= 0)
            {
                _bundleHandleHashes[emptyIndex] = hash;
                _bundleHandles[emptyIndex] = handle;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ContentAuthorityRuntime] Bundle handle table exhausted.", this);
#endif
            Addressables.Release(handle);
            return false;
        }

        private bool TryReleaseTrackedBundleHandle(uint hash)
        {
            if (hash == 0u)
                return false;

            for (int i = 0; i < _bundleHandleHashes.Length; i++)
            {
                if (_bundleHandleHashes[i] != hash)
                    continue;

                AsyncOperationHandle handle = _bundleHandles[i];
                if (handle.IsValid())
                    Addressables.Release(handle);

                _bundleHandles[i] = default;
                _bundleHandleHashes[i] = 0u;
                return true;
            }

            return false;
        }

        private void RemoveVfxPrewarmHandleAt(int index)
        {
            int last = _vfxPrewarmHandles.Count - 1;
            _vfxPrewarmHandles[index] = _vfxPrewarmHandles[last];
            _vfxPrewarmHandles.RemoveAt(last);
        }
#endif

        private unsafe void WriteTelemetry(uint flags)
        {
            VRAMPressureMonitor pressure = _vramPressure;
            long estimate = _bundleRefs.EstimateResidentBytes();
            float rawVramPressure = pressure != null ? pressure.VramPressureFactor : 0f;
            float rawRamPressure = pressure != null ? pressure.RamPressureFactor : 0f;
            bool nonFinite = !IsFinite(rawVramPressure) || !IsFinite(rawRamPressure);
            float vramPressure = Sanitize01(rawVramPressure);
            float ramPressure = Sanitize01(rawRamPressure);
            if (nonFinite)
                flags |= NonFiniteFlag;

            int pendingLoadCount = GetPendingLoadCount();
            uint stateHash = unchecked((uint)pendingLoadCount * 73856093u) ^
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
                PendingLoads = pendingLoadCount,
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
            IDataVault vault = _dataVault;
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

        private unsafe int GetPendingLoadCount()
        {
            return TryResolvePendingLoadsNormalized(
                out ContentPendingLoadState* _,
                out int* _,
                out int count)
                ? count
                : 0;
        }

        private unsafe bool TryResolvePendingLoads(
            out ContentPendingLoadState* pendingLoads,
            out int* count)
        {
            pendingLoads = null;
            count = null;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!_pendingLoadsHandle.IsCreated || !vault.ResolveBuffer(ref _pendingLoadsHandle))
            {
                _pendingLoadsHandle = vault.GetBufferHandle<ContentPendingLoadState>(
                    BufferID.ContentAuthorityPendingLoads,
                    PendingLoadCapacity,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_pendingLoadCountHandle.IsCreated || !vault.ResolveBuffer(ref _pendingLoadCountHandle))
            {
                _pendingLoadCountHandle = vault.GetBufferHandle<int>(
                    BufferID.ContentAuthorityPendingLoadCount,
                    1,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            pendingLoads = (ContentPendingLoadState*)_pendingLoadsHandle.ResolvePointer(vault);
            count = (int*)_pendingLoadCountHandle.ResolvePointer(vault);
            return pendingLoads != null &&
                   count != null &&
                   _pendingLoadsHandle.Length >= PendingLoadCapacity &&
                   _pendingLoadCountHandle.Length >= 1;
        }

        private unsafe bool TryResolvePendingLoadsNormalized(
            out ContentPendingLoadState* pendingLoads,
            out int* countPtr,
            out int count)
        {
            count = 0;
            if (!TryResolvePendingLoads(out pendingLoads, out countPtr))
                return false;

            count = *countPtr;
            if ((uint)count <= PendingLoadCapacity)
                return true;

            LogPendingLoadCountCorruption();
            ClearResolvedPendingLoads(pendingLoads, countPtr);
            ClearPendingLoadTargets();
            HideAllHolograms();
            count = 0;
            return true;
        }

        private static unsafe void ClearResolvedPendingLoads(ContentPendingLoadState* pendingLoads, int* countPtr)
        {
            for (int i = 0; i < PendingLoadCapacity; i++)
                pendingLoads[i] = default;

            *countPtr = 0;
        }

        private void ClearPendingLoadTargets()
        {
            if (_pendingLoadTargets == null)
                return;

            for (int i = 0; i < _pendingLoadTargets.Length; i++)
                _pendingLoadTargets[i] = null;
        }

        private void HideAllHolograms()
        {
            if (_hologramPool != null)
            {
                for (int i = 0; i < _hologramPool.Length; i++)
                {
                    GameObject proxy = _hologramPool[i];
                    if (proxy != null && proxy.activeSelf)
                        proxy.SetActive(false);
                }
            }

            _hologramsActive = 0;
        }

        private void ClearVaultHandles()
        {
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _pendingLoadsHandle = default;
            _pendingLoadCountHandle = default;
            _dataVault = null;
            _vramMonitor = null;
            _vramPressure = null;
            _assetLifecycle = null;
        }

        private void ClearBundleResidencyState()
        {
#if UNITY_ADDRESSABLES_EXIST
            for (int i = 0; i < _bundleHandles.Length; i++)
            {
                if (_bundleHandles[i].IsValid())
                    Addressables.Release(_bundleHandles[i]);

                _bundleHandles[i] = default;
                _bundleHandleHashes[i] = 0u;
            }
#endif
            _bundleRefs.Clear();
            VRAMBudgetTracker.Unregister(VramLedgerOwnerHash);
        }

        private unsafe void DumpBlackBox()
        {
            if (_blackBoxDumpedThisSession)
                return;

            if (!TryResolveTelemetryPointer(out ContentAuthorityTelemetryEntry* telemetry, out int* cursorPtr))
                return;

            string path = _blackBoxDumpPath;
            if (string.IsNullOrEmpty(path))
                return;

            int cursor = *cursorPtr;
            if ((uint)cursor >= TelemetryCapacity)
                cursor = 0;

            if (TryWriteBlackBox(path, telemetry, cursor))
            {
                _blackBoxDumpedThisSession = true;
                return;
            }

            string fallbackPath = ResolvePersistentBlackBoxDumpPath();
            if (string.Equals(path, fallbackPath, StringComparison.Ordinal))
                return;

            if (TryWriteBlackBox(fallbackPath, telemetry, cursor))
                _blackBoxDumpedThisSession = true;
        }

        private static unsafe bool TryWriteBlackBox(
            string path,
            ContentAuthorityTelemetryEntry* telemetry,
            int cursor)
        {
            if (string.IsNullOrEmpty(path) || telemetry == null)
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(BlackBoxMagic);
                    writer.Write((uint)TelemetryCapacity);
                    writer.Write(BlackBoxEntrySizeBytes);
                    writer.Write((uint)0u);

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
                        writer.Write(entry.Reserved1);
                        writer.Write(entry.Reserved2);
                        writer.Write(entry.Reserved3);
                        writer.Write(entry.Reserved4);
                    }
                }

                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                LogBlackBoxDumpFailure(path, exception);
                return false;
            }
        }

        private static string ResolveBlackBoxDumpPath()
        {
            try
            {
                string projectPath = Path.Combine(Directory.GetCurrentDirectory(), BlackBoxRelativePath);
                string projectDirectory = Path.GetDirectoryName(projectPath);
                if (!string.IsNullOrEmpty(projectDirectory) && Directory.Exists(projectDirectory))
                    return projectPath;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                LogBlackBoxPathFailure(exception);
            }

            return ResolvePersistentBlackBoxDumpPath();
        }

        private static string ResolvePersistentBlackBoxDumpPath()
        {
            try
            {
                string fallbackDirectory = Application.persistentDataPath;
                return string.IsNullOrEmpty(fallbackDirectory)
                    ? BlackBoxFallbackFileName
                    : Path.Combine(fallbackDirectory, BlackBoxFallbackFileName);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                LogBlackBoxPathFailure(exception);
                return BlackBoxFallbackFileName;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadCountCorruption()
        {
            Debug.LogError("[ContentAuthorityRuntime] Pending load vault count exceeded fixed capacity; cleared pending-load ledger.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBlackBoxDumpFailure(string path, Exception exception)
        {
            Debug.LogError("[ContentAuthorityRuntime] Failed to write content blackbox dump: " +
                           path + " error=" + exception.Message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBlackBoxPathFailure(Exception exception)
        {
            Debug.LogError("[ContentAuthorityRuntime] Failed to resolve content blackbox dump path: " +
                           exception.Message);
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

            CacheDependencies();
            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void CacheDependencies()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            _bundleRefs.BindVault(_dataVault);
            if (_vramMonitor == null)
                _vramMonitor = GlobalRegistry.VRAMMonitor;
            if (_vramPressure == null)
                _vramPressure = GlobalRegistry.VRAMPressure;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecycle;
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
            return ResolveVisualBudget(tier).FeatureMask;
        }

        public static ContentVisualFeatureBudget ResolveVisualBudget(ContentTier tier)
        {
            if (HectonXRRuntimeState.IsXRActive || SystemInfo.graphicsMemorySize <= 2048)
            {
                return new ContentVisualFeatureBudget
                {
                    FeatureMask = DearLieOneDimensionalLut |
                                  DearLieTriangleNoise |
                                  DearLieDotProductVision,
                    MaxParticles = 512,
                    RaymarchSteps = 8,
                    PomTaps = 0,
                    SiltWakeLayers = 1,
                    SaltCrystalLayers = 1,
                    HullDentOctaves = 1
                };
            }

            if (tier == ContentTier.Overkill && SystemInfo.graphicsMemorySize > 4096)
            {
                return new ContentVisualFeatureBudget
                {
                    FeatureMask = VisualFeatureSaltCrystals |
                                  VisualFeatureVolumetricSiltWake |
                                  VisualFeatureProceduralHullDents |
                                  VisualFeatureRaymarchDetail |
                                  VisualFeatureParallaxOcclusion16Tap,
                    MaxParticles = 16384,
                    RaymarchSteps = 64,
                    PomTaps = 16,
                    SiltWakeLayers = 4,
                    SaltCrystalLayers = 3,
                    HullDentOctaves = 4
                };
            }

            return new ContentVisualFeatureBudget
            {
                FeatureMask = VisualFeatureSaltCrystals |
                              VisualFeatureVolumetricSiltWake |
                              DearLieTriangleNoise,
                MaxParticles = 2048,
                RaymarchSteps = 24,
                PomTaps = 4,
                SiltWakeLayers = 2,
                SaltCrystalLayers = 2,
                HullDentOctaves = 2
            };
        }
    }
}

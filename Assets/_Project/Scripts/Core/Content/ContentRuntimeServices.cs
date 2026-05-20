using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.Core.Content
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ContentBundleRefState
    {
        [FieldOffset(0)] public long Bytes;
        [FieldOffset(8)] public uint Hash;
        [FieldOffset(12)] public int RefCount;
        [FieldOffset(16)] public int LastAccessFrame;
        [FieldOffset(20)] public byte BiomeId;
        [FieldOffset(21)] public ContentTier Tier;
        [FieldOffset(22)] public byte IsBiomeCache;
        [FieldOffset(23)] public byte Reserved0;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ContentAuthorityTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint FocusHash;
        [FieldOffset(12)] public int PendingLoads;
        [FieldOffset(16)] public int HologramsActive;
        [FieldOffset(20)] public int BundleRefCount;
        [FieldOffset(24)] public long EstimatedVramBytes;
        [FieldOffset(32)] public float VramPressure01;
        [FieldOffset(36)] public float RamPressure01;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public uint Reserved1;
        [FieldOffset(52)] public uint Reserved2;
        [FieldOffset(56)] public uint Reserved3;
        [FieldOffset(60)] public uint Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ContentPendingLoadState
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public float StartTime;
        [FieldOffset(8)] public int HologramIndex;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ContentVisualFeatureBudget
    {
        [FieldOffset(0)] public uint FeatureMask;
        [FieldOffset(4)] public ushort MaxParticles;
        [FieldOffset(6)] public byte RaymarchSteps;
        [FieldOffset(7)] public byte PomTaps;
        [FieldOffset(8)] public byte SiltWakeLayers;
        [FieldOffset(9)] public byte SaltCrystalLayers;
        [FieldOffset(10)] public byte HullDentOctaves;
        [FieldOffset(11)] public byte Reserved0;
        [FieldOffset(12)] public int Reserved1;
    }

    /// <summary>
    /// Fixed-capacity bundle reference counter. Duplicate loads resolve to ref increments, not second handles.
    /// </summary>
    public sealed class ContentBundleReferenceCounter
    {
        private readonly int _capacity;
        private IDataVault _vault;
        private VaultGenerationHandle<ContentBundleRefState> _statesHandle;
        private VaultGenerationHandle<int> _countHandle;

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

            ReleaseVaultHandles();
            _vault = vault;
            _statesHandle = default;
            _countHandle = default;
        }

        public unsafe bool Acquire(uint hash, long bytes, byte biomeId, ContentTier tier, bool isBiomeCache, int frame)
        {
            if (hash == 0u)
            {
                LogRefCountViolation(hash);
                return false;
            }
            if (bytes < 0L)
            {
                LogInvalidAcquireMetadata(hash, bytes, tier);
                return false;
            }
            if (tier > ContentTier.Overkill)
            {
                LogInvalidAcquireMetadata(hash, bytes, tier);
                return false;
            }

            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* countPtr, out int count))
            {
                LogVaultUnavailable("acquire", hash);
                return false;
            }

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
            {
                LogBundleRefCapacityExceeded(hash, _capacity);
                return false;
            }

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
            {
                LogVaultUnavailable("release", hash);
                return false;
            }

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
            if (hash == 0u)
            {
                LogRefCountViolation(hash);
                return false;
            }

            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* countPtr, out int count))
            {
                LogVaultUnavailable("remove", hash);
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (states[i].Hash != hash)
                    continue;

                if (states[i].RefCount > 0)
                {
                    LogActiveRemoveRejected(hash, states[i].RefCount);
                    return false;
                }

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
            return EstimateResidentBytes(out int _);
        }

        public unsafe long EstimateResidentBytes(out int residentCount)
        {
            if (!TryResolveNormalized(out ContentBundleRefState* states, out int* _, out int count))
            {
                residentCount = 0;
                return 0L;
            }

            residentCount = count;
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

            if (!TryResolveOrAcquire(
                    vault,
                    ref _statesHandle,
                    BufferID.ContentAuthorityBundleRefs,
                    _capacity,
                    out NativeArray<ContentBundleRefState> statesBuffer) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _countHandle,
                    BufferID.ContentAuthorityBundleRefCount,
                    1,
                    out NativeArray<int> countBuffer))
            {
                return false;
            }

            states = (ContentBundleRefState*)statesBuffer.GetUnsafePtr();
            count = (int*)countBuffer.GetUnsafePtr();
            return states != null && count != null && statesBuffer.Length >= _capacity && countBuffer.Length >= 1;
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (_statesHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statesHandle);
            if (_countHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _countHandle);
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
        private static void LogInvalidAcquireMetadata(uint hash, long bytes, ContentTier tier)
        {
            Debug.LogError("[ContentBundleReferenceCounter] Invalid acquire metadata hash=0x" +
                           hash.ToString("X8") + " bytes=" + bytes + " tier=" + tier + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogActiveRemoveRejected(uint hash, int refCount)
        {
            Debug.LogError("[ContentBundleReferenceCounter] Refused to remove active bundle hash=0x" +
                           hash.ToString("X8") + " refCount=" + refCount + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVaultUnavailable(string operation, uint hash)
        {
            Debug.LogError("[ContentBundleReferenceCounter] Vault unavailable during " +
                           operation + " hash=0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleRefCapacityExceeded(uint hash, int capacity)
        {
            Debug.LogError("[ContentBundleReferenceCounter] Bundle ref ledger full capacity=" +
                           capacity + " rejectedHash=0x" + hash.ToString("X8") + ".");
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
        public const int MaxParticlePrefabDepth = 32;
        public const int MaxParticlePrefabNodes = 256;

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
    public sealed class ContentAuthorityRuntime : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
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
        // COLD ALLOC: AsyncOperationHandle[64] - fixed VFX prewarm handle ledger - owner: ContentAuthorityRuntime
        private readonly AsyncOperationHandle[] _vfxPrewarmHandles = new AsyncOperationHandle[ContentVfxPrewarmManifest.MaxEntries];
        // COLD ALLOC: AsyncOperationHandle[64] - fixed resident prewarmed VFX release ledger - owner: ContentAuthorityRuntime
        private readonly AsyncOperationHandle[] _vfxResidentHandles = new AsyncOperationHandle[ContentVfxPrewarmManifest.MaxEntries];
#endif
        private IDataVault _dataVault;
        private VRAMMonitor _vramMonitor;
        private VRAMPressureMonitor _vramPressure;
        private AssetLifecycleGovernor _assetLifecycle;
        private VaultGenerationHandle<ContentAuthorityTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ContentPendingLoadState> _pendingLoadsHandle;
        private VaultGenerationHandle<int> _pendingLoadCountHandle;
        private Renderer[] _pendingLoadTargets;
        private GameObject[] _hologramPool;
        private Renderer[] _hologramRenderers;
        private bool _registeredTick;
        private bool _registeredHotSwap;
        private bool _vfxPrewarmStarted;
        private bool _blackBoxDumpedThisSession;
        private string _blackBoxDumpPath;
        private int _nextHologramIndex;
        private int _hologramsActive;
        private bool _hologramPoolExhaustedLogged;
#if UNITY_ADDRESSABLES_EXIST
        private int _vfxPrewarmHandleCount;
        private int _vfxResidentHandleCount;
#endif

        public ContentAssetHashMap AssetHashMap => assetHashMap;
        public ContentBundleReferenceCounter BundleReferenceCounter => _bundleRefs;
        public int HologramPoolCapacity => hologramPoolCapacity;
        public bool HasHologramProxyBinding => hologramProxyMesh != null && hologramMaterial != null;

        private void Awake()
        {
            int capacity = Mathf.Clamp(hologramPoolCapacity, 1, MaxPendingLoadCount);
            hologramPoolCapacity = capacity;
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
            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
            if (startVfxPrewarmOnEnable)
                StartVfxPrewarm();
        }

        private void Start()
        {
            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            ClearPendingLoads();
            TryUnregister();
            TryUnregisterHotSwap();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            ClearBundleResidencyState();

#if UNITY_ADDRESSABLES_EXIST
            _vfxPrewarmHandleCount = ReleaseVfxHandleRange(_vfxPrewarmHandles, _vfxPrewarmHandleCount);
            _vfxResidentHandleCount = ReleaseVfxHandleRange(_vfxResidentHandles, _vfxResidentHandleCount);
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

            if (assetHashMap == null)
            {
                LogMissingAssetHashMap(hash);
                return false;
            }

            if (!assetHashMap.TryGetEntry(hash, out ContentAssetEntry entry))
            {
                LogMissingAssetHash(hash);
                return false;
            }

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
                    TryReleaseExternalAddressableFault(handle);

                return false;
            }

            if (!handle.IsValid())
            {
                RollbackBundleAcquire(hash);
                LogInvalidBundleHandle(hash);
                return false;
            }

            if (TryTrackBundleHandle(hash, handle))
                return true;

            RollbackBundleAcquire(hash);
            LogBundleHandleTrackFailed(hash);
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
                    bool releaseAccepted = true;
#if UNITY_ADDRESSABLES_EXIST
                    if (!TryReleaseTrackedBundleHandle(hash, out bool handleFound))
                    {
                        LogBundleHandleReleaseMiss(hash);
                        releaseAccepted = !handleFound;
                    }
#endif
                    if (releaseAccepted)
                    {
                        _bundleRefs.Remove(hash);
                    }
                    else
                    {
                        released = false;
                    }
                }

                VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
            }

            return released;
        }

        public unsafe bool TrackAsyncLoad(uint hash, Renderer targetRenderer)
        {
            if (hash == 0u || targetRenderer == null)
            {
                LogInvalidAsyncLoadTrack(hash, targetRenderer == null);
                return false;
            }

            if (_dataVault == null)
                CacheDependencies();

            if (assetHashMap == null)
            {
                LogMissingAssetHashMap(hash);
                return false;
            }

            if (!assetHashMap.TryGetEntry(hash, out ContentAssetEntry _))
            {
                LogMissingAssetHash(hash);
                return false;
            }

            if (!TryResolvePendingLoadsNormalized(out ContentPendingLoadState* pendingLoads, out int* countPtr, out int count))
            {
                LogPendingLoadVaultUnavailable(hash);
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (pendingLoads[i].Hash != hash || _pendingLoadTargets[i] != targetRenderer)
                    continue;

                return true;
            }

            if (count >= PendingLoadCapacity)
            {
                LogPendingLoadCapacityExceeded(hash);
                return false;
            }

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
            {
                LogPendingLoadVaultUnavailable(hash);
                return false;
            }

            for (int i = count - 1; i >= 0; i--)
            {
                ContentPendingLoadState pending = pendingLoads[i];
                if (pending.Hash != hash || _pendingLoadTargets[i] != targetRenderer)
                    continue;

                HideHologram(pending.HologramIndex);
                RemovePendingLoadAt(i, pendingLoads, countPtr);
                return true;
            }

            LogAsyncLoadCompletionMiss(hash, targetRenderer == null);
            return false;
        }

        public bool TryResolveContentEntry(uint hash, out ContentAssetEntry entry)
        {
            if (assetHashMap == null)
            {
                entry = default;
                LogMissingAssetHashMap(hash);
                return false;
            }

            bool resolved = assetHashMap.TryGetEntry(hash, out entry);
            if (!resolved)
                LogMissingAssetHash(hash);
            return resolved;
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
                {
                    LogInvalidVfxPrewarmReference(i, true);
                    continue;
                }

                AsyncOperationHandle handle = reference.LoadAssetAsync<UnityEngine.Object>();
                if (TryQueueVfxPrewarmHandle(handle))
                    dispatched++;
                else if (handle.IsValid())
                {
                    LogVfxPrewarmLedgerFull(true);
                    TryReleaseExternalAddressableFault(handle);
                }
                else
                {
                    LogInvalidVfxPrewarmHandle(i, true);
                }
            }

            for (int i = 0; i < vfxPrewarmManifest.ComputeShaderCount && dispatched < ContentVfxPrewarmManifest.MaxEntries; i++)
            {
                AssetReference reference = vfxPrewarmManifest.GetComputeShader(i);
                if (reference == null || !reference.RuntimeKeyIsValid())
                {
                    LogInvalidVfxPrewarmReference(i, false);
                    continue;
                }

                AsyncOperationHandle handle = reference.LoadAssetAsync<ComputeShader>();
                if (TryQueueVfxPrewarmHandle(handle))
                    dispatched++;
                else if (handle.IsValid())
                {
                    LogVfxPrewarmLedgerFull(false);
                    TryReleaseExternalAddressableFault(handle);
                }
                else
                {
                    LogInvalidVfxPrewarmHandle(i, false);
                }
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
            {
                LogHologramProxyUnavailable(target == null);
                return -1;
            }

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
            {
                if (!_hologramPoolExhaustedLogged)
                {
                    _hologramPoolExhaustedLogged = true;
                    LogHologramPoolExhausted();
                }

                return -1;
            }

            _nextHologramIndex = index + 1;
            if (_nextHologramIndex >= poolLength)
                _nextHologramIndex = 0;

            Transform targetTransform = target.transform;
            Transform proxyTransform = proxy.transform;
            proxyTransform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
            proxyTransform.localScale = targetTransform.lossyScale;
            proxy.SetActive(true);
            _hologramsActive++;
            _hologramPoolExhaustedLogged = false;

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
                governor.SetHeapSanitizerBlindFrameWindow(true, 0f);
                try
                {
                    governor.ForceDrainPendingReleaseQueue();
                    governor.EvictLowestPriorityUnusedAssets(2, AssetPriorityTier.Tier5DistantHlod);
                }
                finally
                {
                    governor.SetHeapSanitizerBlindFrameWindow(false, 0f);
                }
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
                bool releaseAccepted = true;
#if UNITY_ADDRESSABLES_EXIST
                if (!TryReleaseTrackedBundleHandle(hash, out bool handleFound))
                {
                    LogBundleHandleReleaseMiss(hash);
                    releaseAccepted = !handleFound;
                }
#endif
                if (releaseAccepted)
                {
                    _bundleRefs.Remove(hash);
                    VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
                }
            }

            if (governor != null)
            {
                governor.SetHeapSanitizerVramPanicWindow(true, 0f);
                try
                {
                    governor.ForceDrainPendingReleaseQueue();
                    governor.EvictLowestPriorityUnusedAssets(1, AssetPriorityTier.Tier5DistantHlod);
                }
                finally
                {
                    governor.SetHeapSanitizerVramPanicWindow(false, 0f);
                }
            }

            flags |= VramInterceptFlag;
        }

        private void TickVfxPrewarm()
        {
#if UNITY_ADDRESSABLES_EXIST
            for (int i = _vfxPrewarmHandleCount - 1; i >= 0; i--)
            {
                AsyncOperationHandle handle = _vfxPrewarmHandles[i];
                if (!handle.IsDone)
                    continue;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    TryPrewarmParticleHandleResult(handle);

                    if (!TryQueueResidentVfxHandle(handle) && handle.IsValid())
                    {
                        LogVfxResidentLedgerFull();
                        if (!TryStageExternalAddressableRelease(handle))
                            continue;
                    }
                }
                else if (handle.IsValid())
                {
                    LogVfxPrewarmFailed();
                    if (!TryStageExternalAddressableRelease(handle))
                        continue;
                }

                RemoveVfxPrewarmHandleAt(i);
            }
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool TryStageExternalAddressableRelease(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            AssetLifecycleGovernor governor = _assetLifecycle;
            return governor != null && governor.TryStageExternalAddressableRelease(handle);
        }

        private bool TryReleaseExternalAddressableFault(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            AssetLifecycleGovernor governor = _assetLifecycle;
            return governor != null && governor.TryReleaseExternalAddressableFault(handle);
        }

        private bool TryQueueVfxPrewarmHandle(AsyncOperationHandle handle)
        {
            if (!handle.IsValid() || _vfxPrewarmHandleCount >= _vfxPrewarmHandles.Length)
                return false;

            _vfxPrewarmHandles[_vfxPrewarmHandleCount] = handle;
            _vfxPrewarmHandleCount++;
            return true;
        }

        private bool TryQueueResidentVfxHandle(AsyncOperationHandle handle)
        {
            if (!handle.IsValid() || _vfxResidentHandleCount >= _vfxResidentHandles.Length)
                return false;

            _vfxResidentHandles[_vfxResidentHandleCount] = handle;
            _vfxResidentHandleCount++;
            return true;
        }

        private static void TryPrewarmParticleHandleResult(AsyncOperationHandle handle)
        {
            object result = handle.Result;
            if (result is ParticleSystem particleSystem)
            {
                particleSystem.Simulate(0f, true, true, true);
                return;
            }

            if (result is GameObject gameObject)
            {
                int visitedNodes = 0;
                PrewarmParticleHierarchy(gameObject.transform, 0, ref visitedNodes);
            }
        }

        private static void PrewarmParticleHierarchy(Transform root, int depth, ref int visitedNodes)
        {
            if (root == null)
                return;
            if (depth > ContentVfxPrewarmManifest.MaxParticlePrefabDepth)
                return;
            if (visitedNodes >= ContentVfxPrewarmManifest.MaxParticlePrefabNodes)
                return;

            visitedNodes++;
            if (root.TryGetComponent(out ParticleSystem particleSystem))
                particleSystem.Simulate(0f, true, true, true);

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
                PrewarmParticleHierarchy(root.GetChild(i), depth + 1, ref visitedNodes);
        }

        private int ReleaseVfxHandleRange(AsyncOperationHandle[] handles, int count)
        {
            if (handles == null)
                return 0;

            if ((uint)count > (uint)handles.Length)
                count = handles.Length;

            int retainedCount = 0;
            for (int i = 0; i < count; i++)
            {
                AsyncOperationHandle handle = handles[i];
                if (handle.IsValid() && !TryReleaseExternalAddressableFault(handle))
                {
                    handles[retainedCount] = handle;
                    retainedCount++;
                    continue;
                }

                handles[i] = default;
            }

            for (int i = retainedCount; i < count; i++)
                handles[i] = default;

            return retainedCount;
        }

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
                        return TryReleaseExternalAddressableFault(handle);

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
            TryReleaseExternalAddressableFault(handle);
            return false;
        }

        private bool TryReleaseTrackedBundleHandle(uint hash, out bool found)
        {
            found = false;
            if (hash == 0u)
                return false;

            for (int i = 0; i < _bundleHandleHashes.Length; i++)
            {
                if (_bundleHandleHashes[i] != hash)
                    continue;

                found = true;
                AsyncOperationHandle handle = _bundleHandles[i];
                if (handle.IsValid() && !TryStageExternalAddressableRelease(handle))
                    return false;

                _bundleHandles[i] = default;
                _bundleHandleHashes[i] = 0u;
                return true;
            }

            return false;
        }

        private void RemoveVfxPrewarmHandleAt(int index)
        {
            if ((uint)index >= (uint)_vfxPrewarmHandleCount)
                return;

            int last = _vfxPrewarmHandleCount - 1;
            _vfxPrewarmHandles[index] = _vfxPrewarmHandles[last];
            _vfxPrewarmHandles[last] = default;
            _vfxPrewarmHandleCount = last;
        }
#endif

        private unsafe void WriteTelemetry(uint flags)
        {
            VRAMPressureMonitor pressure = _vramPressure;
            long estimate = _bundleRefs.EstimateResidentBytes(out int bundleRefCount);
            float rawVramPressure = pressure != null ? pressure.VramPressureFactor : 0f;
            float rawRamPressure = pressure != null ? pressure.RamPressureFactor : 0f;
            bool nonFinite = !IsFinite(rawVramPressure) || !IsFinite(rawRamPressure);
            float vramPressure = Sanitize01(rawVramPressure);
            float ramPressure = Sanitize01(rawRamPressure);
            if (nonFinite)
                flags |= NonFiniteFlag;

            int pendingLoadCount = GetPendingLoadCount();
            uint stateHash = unchecked((uint)pendingLoadCount * 73856093u) ^
                             unchecked((uint)bundleRefCount * 19349663u) ^
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
                BundleRefCount = bundleRefCount,
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

            if (!TryResolveTelemetryBuffers(
                    out NativeArray<ContentAuthorityTelemetryEntry> telemetryBuffer,
                    out NativeArray<int> cursorBuffer))
                return false;

            telemetry = (ContentAuthorityTelemetryEntry*)telemetryBuffer.GetUnsafePtr();
            cursor = (int*)cursorBuffer.GetUnsafePtr();
            return telemetry != null && cursor != null;
        }

        private bool TryResolveTelemetryBuffers(
            out NativeArray<ContentAuthorityTelemetryEntry> telemetry,
            out NativeArray<int> cursor)
        {
            telemetry = default;
            cursor = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return TryResolveOrAcquire(
                       vault,
                       ref _telemetryHandle,
                       BufferID.ContentAuthorityBlackBox,
                       TelemetryCapacity,
                       out telemetry) &&
                   TryResolveOrAcquire(
                       vault,
                       ref _telemetryCursorHandle,
                       BufferID.ContentAuthorityTelemetryCursor,
                       1,
                       out cursor);
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

            if (!TryResolveOrAcquire(
                    vault,
                    ref _pendingLoadsHandle,
                    BufferID.ContentAuthorityPendingLoads,
                    PendingLoadCapacity,
                    out NativeArray<ContentPendingLoadState> pendingLoadsBuffer) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _pendingLoadCountHandle,
                    BufferID.ContentAuthorityPendingLoadCount,
                    1,
                    out NativeArray<int> countBuffer))
            {
                return false;
            }

            pendingLoads = (ContentPendingLoadState*)pendingLoadsBuffer.GetUnsafePtr();
            count = (int*)countBuffer.GetUnsafePtr();
            return pendingLoads != null &&
                   count != null &&
                   pendingLoadsBuffer.Length >= PendingLoadCapacity &&
                   countBuffer.Length >= 1;
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

        private void RollbackBundleAcquire(uint hash)
        {
            if (_bundleRefs.Release(hash, Time.frameCount, out bool becameUnused) && becameUnused)
                _bundleRefs.Remove(hash);

            VRAMBudgetTracker.RegisterOrUpdate(VramLedgerOwnerHash, _bundleRefs.EstimateResidentBytes());
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
            ReleaseAuthorityVaultHandles(_dataVault);
            _bundleRefs.BindVault(null);
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _pendingLoadsHandle = default;
            _pendingLoadCountHandle = default;
            _dataVault = null;
            _vramMonitor = null;
            _vramPressure = null;
            _assetLifecycle = null;
        }

        private void ReleaseAuthorityVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_telemetryHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _telemetryHandle);
            if (_telemetryCursorHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _telemetryCursorHandle);
            if (_pendingLoadsHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _pendingLoadsHandle);
            if (_pendingLoadCountHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _pendingLoadCountHandle);
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.ContentAuthority,
                    NativeArrayOptions.ClearMemory);
            }

            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ClearBundleResidencyState()
        {
            bool allReleased = true;
#if UNITY_ADDRESSABLES_EXIST
            for (int i = 0; i < _bundleHandles.Length; i++)
            {
                if (_bundleHandles[i].IsValid() && !TryReleaseExternalAddressableFault(_bundleHandles[i]))
                {
                    allReleased = false;
                    continue;
                }

                _bundleHandles[i] = default;
                _bundleHandleHashes[i] = 0u;
            }
#endif
            if (allReleased)
            {
                _bundleRefs.Clear();
                VRAMBudgetTracker.Unregister(VramLedgerOwnerHash);
            }
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
        private static void LogMissingAssetHashMap(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] Asset hash map missing for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingAssetHash(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] No content registry entry for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleHandleTrackFailed(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] Failed to track Addressables bundle handle for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleHandleReleaseMiss(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] No tracked Addressables bundle handle during release for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidBundleHandle(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] Invalid Addressables bundle handle for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidAsyncLoadTrack(uint hash, bool nullTarget)
        {
            Debug.LogError("[ContentAuthorityRuntime] Rejected async load tracking hash=0x" +
                           hash.ToString("X8") + " nullTarget=" + nullTarget + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadVaultUnavailable(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] Pending-load vault unavailable for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadCapacityExceeded(uint hash)
        {
            Debug.LogError("[ContentAuthorityRuntime] Pending-load ledger full for hash 0x" + hash.ToString("X8") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogAsyncLoadCompletionMiss(uint hash, bool nullTarget)
        {
            Debug.LogError("[ContentAuthorityRuntime] Async load completion had no pending entry hash=0x" +
                           hash.ToString("X8") + " nullTarget=" + nullTarget + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidVfxPrewarmReference(int index, bool particle)
        {
            Debug.LogError("[ContentAuthorityRuntime] Invalid VFX prewarm Addressables reference kind=" +
                           (particle ? "particle" : "compute") + " index=" + index + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxPrewarmLedgerFull(bool particle)
        {
            Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle ledger full kind=" +
                           (particle ? "particle" : "compute") + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidVfxPrewarmHandle(int index, bool particle)
        {
            Debug.LogError("[ContentAuthorityRuntime] VFX prewarm returned invalid Addressables handle kind=" +
                           (particle ? "particle" : "compute") + " index=" + index + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxResidentLedgerFull()
        {
            Debug.LogError("[ContentAuthorityRuntime] Resident VFX handle ledger full; releasing completed prewarm handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxPrewarmFailed()
        {
            Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle failed; releasing Addressables handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHologramProxyUnavailable(bool nullTarget)
        {
            Debug.LogError("[ContentAuthorityRuntime] Hologram proxy unavailable nullTarget=" + nullTarget + ".");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHologramPoolExhausted()
        {
            Debug.LogError("[ContentAuthorityRuntime] Hologram proxy pool exhausted; pending asset will remain invisible until a proxy frees.");
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

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseAuthorityVaultHandles(previousService as IDataVault ?? _dataVault);
                    _dataVault = currentService as IDataVault;
                    _bundleRefs.BindVault(_dataVault);
                    _telemetryHandle = default;
                    _telemetryCursorHandle = default;
                    _pendingLoadsHandle = default;
                    _pendingLoadCountHandle = default;
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as VRAMMonitor;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as VRAMPressureMonitor;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as AssetLifecycleGovernor;
                    break;
            }
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
            if (!IsValidTier(tier))
            {
                LogInvalidContentTier(tier);
                return false;
            }

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
            if (!IsValidTier(tier))
            {
                LogInvalidContentTier(tier);
                return ResolveLowVisualBudget();
            }

            if (HectonXRRuntimeState.IsXRActive || SystemInfo.graphicsMemorySize <= 2048)
                return ResolveLowVisualBudget();

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

        private static bool IsValidTier(ContentTier tier)
        {
            return tier <= ContentTier.Overkill;
        }

        private static ContentVisualFeatureBudget ResolveLowVisualBudget()
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidContentTier(ContentTier tier)
        {
            Debug.LogError("[ContentTieredGroupPolicy] Invalid content tier value=" + (byte)tier + ".");
        }
    }
}

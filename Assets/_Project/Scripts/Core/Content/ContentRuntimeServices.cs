using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Optimization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
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
        [FieldOffset(11)] public byte VisualFeatureWeightQ8;
        [FieldOffset(12)] public byte PomWeightQ8;
        [FieldOffset(13)] public byte SiltWakeWeightQ8;
        [FieldOffset(14)] public byte HullDentWeightQ8;
        [FieldOffset(15)] public byte SaltCrystalWeightQ8;
    }

    /// <summary>
    /// Fixed-capacity bundle reference counter. Duplicate loads resolve to ref increments, not second handles.
    /// </summary>
    public sealed class ContentBundleReferenceCounter
    {
        private const ulong BundleRefMutationGuardMask = 1UL << 55;

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
                return TryReadNormalized(
                    out NativeArray<ContentBundleRefState>.ReadOnly _,
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

            if (!OpenOrAcquireNormalizedWriteViews(
                    out ContentBundleRefState* states,
                    out int* countPtr,
                    out int count,
                    out IDataVault writeVault))
            {
                LogVaultUnavailable("acquire", hash);
                return false;
            }

            try
            {
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
            finally
            {
                ReleaseBundleRefMutationGuard(writeVault);
            }
        }

        public unsafe bool Release(uint hash, int frame, out bool becameUnused)
        {
            becameUnused = false;
            if (hash == 0u)
            {
                LogRefCountViolation(hash);
                return false;
            }

            if (!OpenOrAcquireNormalizedWriteViews(
                    out ContentBundleRefState* states,
                    out int* _,
                    out int count,
                    out IDataVault writeVault))
            {
                LogVaultUnavailable("release", hash);
                return false;
            }

            try
            {
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
            finally
            {
                ReleaseBundleRefMutationGuard(writeVault);
            }
        }

        public unsafe bool TryGetState(uint hash, out ContentBundleRefState state)
        {
            state = default;
            if (hash == 0u)
                return false;

            if (!TryReadNormalized(out NativeArray<ContentBundleRefState>.ReadOnly states, out int count))
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
            if (!TryReadNormalized(out NativeArray<ContentBundleRefState>.ReadOnly states, out int count))
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

            if (!OpenOrAcquireNormalizedWriteViews(
                    out ContentBundleRefState* states,
                    out int* countPtr,
                    out int count,
                    out IDataVault writeVault))
            {
                LogVaultUnavailable("remove", hash);
                return false;
            }

            try
            {
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
            finally
            {
                ReleaseBundleRefMutationGuard(writeVault);
            }
        }

        public unsafe long EstimateResidentBytes()
        {
            return EstimateResidentBytes(out int _);
        }

        public unsafe long EstimateResidentBytes(out int residentCount)
        {
            if (!TryReadNormalized(out NativeArray<ContentBundleRefState>.ReadOnly states, out int count))
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
            if (!OpenOrAcquireWriteViews(out ContentBundleRefState* states, out int* countPtr, out IDataVault writeVault))
                return;

            try
            {
                int count = *countPtr;
                if ((uint)count > (uint)_capacity)
                    count = _capacity;

                for (int i = 0; i < count; i++)
                    states[i] = default;

                *countPtr = 0;
            }
            finally
            {
                ReleaseBundleRefMutationGuard(writeVault);
            }
        }

        private unsafe bool OpenOrAcquireWriteViews(
            out ContentBundleRefState* states,
            out int* count,
            out IDataVault writeVault)
        {
            states = null;
            count = null;
            writeVault = null;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!OpenOrAcquireBuffer(
                    vault,
                    ref _statesHandle,
                    BufferID.ContentAuthorityBundleRefs,
                    _capacity,
                    out _) ||
                !OpenOrAcquireBuffer(
                    vault,
                    ref _countHandle,
                    BufferID.ContentAuthorityBundleRefCount,
                    1,
                    out _) ||
                !vault.TryAcquireMutationGuard(BundleRefMutationGuardMask))
            {
                return false;
            }

            bool keepGuard = false;
            try
            {
                writeVault = vault;
                if (!vault.TryResolveHandle(in _statesHandle, out NativeArray<ContentBundleRefState> statesBuffer) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> countBuffer) ||
                    !statesBuffer.IsCreated ||
                    !countBuffer.IsCreated ||
                    statesBuffer.Length < _capacity ||
                    countBuffer.Length < 1)
                {
                    writeVault = null;
                    return false;
                }

                states = (ContentBundleRefState*)statesBuffer.GetUnsafePtr();
                count = (int*)countBuffer.GetUnsafePtr();
                if (states != null && count != null && statesBuffer.Length >= _capacity && countBuffer.Length >= 1)
                {
                    writeVault = vault;
                    keepGuard = true;
                    return true;
                }

                writeVault = null;
                return false;
            }
            finally
            {
                if (!keepGuard)
                    ReleaseBundleRefMutationGuard(vault);
            }
        }

        private static bool OpenOrAcquireBuffer<T>(
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
                if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    return false;

                handle = vault.EnsureGenerationHandle<T>(
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

        private unsafe bool OpenOrAcquireNormalizedWriteViews(
            out ContentBundleRefState* states,
            out int* countPtr,
            out int count,
            out IDataVault writeVault)
        {
            count = 0;
            if (!OpenOrAcquireWriteViews(out states, out countPtr, out writeVault))
                return false;

            count = *countPtr;
            if ((uint)count <= (uint)_capacity)
                return true;

            LogLedgerCountCorruption();
            ClearResolved(states, countPtr, _capacity);
            count = 0;
            return true;
        }

        private static void ReleaseBundleRefMutationGuard(IDataVault vault)
        {
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(BundleRefMutationGuardMask);
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _vault;
            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _countHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.ContentAuthority)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private bool TryReadNormalized(
            out NativeArray<ContentBundleRefState>.ReadOnly states,
            out int count)
        {
            states = default;
            count = 0;

            IDataVault vault = _vault;
            if (vault == null ||
                _statesHandle.BufferID == 0u ||
                _countHandle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in _statesHandle, out states) ||
                !states.IsCreated ||
                states.Length < _capacity ||
                !vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly countBuffer) ||
                !countBuffer.IsCreated ||
                countBuffer.Length < 1)
            {
                states = default;
                return false;
            }

            int resolvedCount = countBuffer[0];
            if ((uint)resolvedCount <= (uint)_capacity)
            {
                count = resolvedCount;
                return true;
            }

            LogLedgerCountCorruption();
            states = default;
            return false;
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
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid ref-count transition.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidAcquireMetadata(uint hash, long bytes, ContentTier tier)
        {
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid acquire metadata.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogActiveRemoveRejected(uint hash, int refCount)
        {
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Refused to remove active bundle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVaultUnavailable(string operation, uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault unavailable.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleRefCapacityExceeded(uint hash, int capacity)
        {
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Bundle ref ledger full.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLedgerCountCorruption()
        {
            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault ledger count exceeded fixed capacity; cleared residency ledger.");
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
    public sealed class ContentAuthorityRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, ISlowTickable, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private const float GhostProxyDelaySeconds = 0.1f;
        private const long CompactHardVramCeilingBytes = 1800L * 1024L * 1024L;
        private const uint VramInterceptFlag = 1u << 0;
        private const uint AupCleanupFlag = 1u << 1;
        private const uint HologramFlag = 1u << 2;
        private const uint NonFiniteFlag = 1u << 3;
        private const int AupCleanupPendingReleaseBudget = 2;
        private const int VramInterceptPendingReleaseBudget = 2;
        private const uint VramLedgerOwnerHash = 0xC0A77A57u;
        private const ulong BlackBoxMagic = 0x484543544F4E3800UL;
        private const ulong ContentPendingLoadMutationGuardMask = 1UL << 53;
        private const ulong ContentTelemetryMutationGuardMask = 1UL << 54;
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
        private IVramBudgetReadModel _vramMonitor;
        private IVramPressureReadModel _vramPressure;
        private IAssetLifecyclePressureSink _assetLifecycle;
        private VaultGenerationHandle<ContentAuthorityTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ContentPendingLoadState> _pendingLoadsHandle;
        private VaultGenerationHandle<int> _pendingLoadCountHandle;
        private Renderer[] _pendingLoadTargets;
        private GameObject[] _hologramPool;
        private Renderer[] _hologramRenderers;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredColdTick;
        private bool _pendingContentVisualSyncTick;
        private bool _blackBoxDumpRequested;
        private bool _pendingAupCleanup;
        private bool _pendingVramIntercept;
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
            _pendingContentVisualSyncTick = true;
        }

        public void LateFrameTick()
        {
            if (!_pendingContentVisualSyncTick)
                return;

            _pendingContentVisualSyncTick = false;
            uint flags = 0u;
            TickPendingLoads(ref flags);
            QueueAupShiftCleanup(ref flags);
            QueueVramIntercept(ref flags);
            WriteTelemetry(flags);
        }

        public void SlowTick()
        {
            FlushAupShiftCleanup();
            FlushVramIntercept();
        }

        public void ColdTick()
        {
            TickVfxPrewarm();
            FlushPendingBlackBoxDump();
        }

        public bool RegisterBundleAcquire(uint hash)
        {
            if (_dataVault == null)
            {
                LogMissingRuntimeDataVault(hash);
                return false;
            }

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
                SystemDispatcher.CurrentFrameIndex);

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
            {
                LogMissingRuntimeDataVault(hash);
                return false;
            }

            bool released = _bundleRefs.Release(hash, SystemDispatcher.CurrentFrameIndex, out bool becameUnused);
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
            {
                LogMissingRuntimeDataVault(hash);
                return false;
            }

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

            if (!OpenOrAcquirePendingLoadNormalizedWritePointers(
                    out ContentPendingLoadState* pendingLoads,
                    out int* countPtr,
                    out int count,
                    out IDataVault writeVault))
            {
                LogPendingLoadVaultUnavailable(hash);
                return false;
            }

            try
            {
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
                    StartTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                    HologramIndex = -1
                };
                _pendingLoadTargets[count] = targetRenderer;
                *countPtr = count + 1;
                return true;
            }
            finally
            {
                ReleasePendingLoadMutationGuard(writeVault);
            }
        }

        public unsafe bool CompleteAsyncLoad(uint hash, Renderer targetRenderer)
        {
            if (!OpenOrAcquirePendingLoadNormalizedWritePointers(
                    out ContentPendingLoadState* pendingLoads,
                    out int* countPtr,
                    out int count,
                    out IDataVault writeVault))
            {
                LogPendingLoadVaultUnavailable(hash);
                return false;
            }

            try
            {
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
            finally
            {
                ReleasePendingLoadMutationGuard(writeVault);
            }
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
                Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy mesh/material missing.", this);
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
            if (!OpenOrAcquirePendingLoadNormalizedWritePointers(
                    out ContentPendingLoadState* pendingLoads,
                    out int* _,
                    out int count,
                    out IDataVault writeVault,
                    allowColdInitialization: false))
                return;

            try
            {
                if (count == 0)
                    return;

                float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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
            finally
            {
                ReleasePendingLoadMutationGuard(writeVault);
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
            if (OpenOrAcquirePendingLoadNormalizedWritePointers(
                    out ContentPendingLoadState* pendingLoads,
                    out int* countPtr,
                    out int count,
                    out IDataVault writeVault))
            {
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        HideHologram(pendingLoads[i].HologramIndex);
                        pendingLoads[i] = default;
                        _pendingLoadTargets[i] = null;
                    }

                    *countPtr = 0;
                }
                finally
                {
                    ReleasePendingLoadMutationGuard(writeVault);
                }
            }

            if (_pendingLoadTargets == null)
                return;

            for (int i = 0; i < _pendingLoadTargets.Length; i++)
                _pendingLoadTargets[i] = null;
        }

        private void QueueAupShiftCleanup(ref uint flags)
        {
            if (SignalBusRegistry.SystemStress01 <= 0.8f)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shifts.Length == 0)
                return;

            _pendingAupCleanup = true;
            flags |= AupCleanupFlag;
        }

        private void FlushAupShiftCleanup()
        {
            if (!_pendingAupCleanup)
                return;

            _pendingAupCleanup = false;
            IAssetLifecyclePressureSink governor = _assetLifecycle;
            if (governor != null)
            {
                governor.SetHeapSanitizerBlindFrameWindow(true, 0f);
                try
                {
                    governor.DrainPendingReleaseQueueBudgeted(AupCleanupPendingReleaseBudget);
                    governor.EvictLowestPriorityUnusedAssets(2, AssetPriorityTierCodes.Tier5DistantHlod);
                }
                finally
                {
                    governor.SetHeapSanitizerBlindFrameWindow(false, 0f);
                }
            }
        }

        private void QueueVramIntercept(ref uint flags)
        {
            IVramBudgetReadModel monitor = _vramMonitor;
            if (monitor == null)
                return;

            long projectedBytes = monitor.TotalVRAMBytes + _bundleRefs.EstimateResidentBytes();
            long hardVramCeilingBytes = ResolveHardVramCeilingBytes();
            if (projectedBytes <= hardVramCeilingBytes)
                return;

            _pendingVramIntercept = true;
            flags |= VramInterceptFlag;
        }

        private void FlushVramIntercept()
        {
            if (!_pendingVramIntercept)
                return;

            _pendingVramIntercept = false;
            IVramBudgetReadModel monitor = _vramMonitor;
            if (monitor == null)
                return;

            long projectedBytes = monitor.TotalVRAMBytes + _bundleRefs.EstimateResidentBytes();
            long hardVramCeilingBytes = ResolveHardVramCeilingBytes();
            if (projectedBytes <= hardVramCeilingBytes)
                return;

            IAssetLifecyclePressureSink governor = _assetLifecycle;
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
                    governor.DrainPendingReleaseQueueBudgeted(VramInterceptPendingReleaseBudget);
                    governor.EvictLowestPriorityUnusedAssets(1, AssetPriorityTierCodes.Tier5DistantHlod);
                }
                finally
                {
                    governor.SetHeapSanitizerVramPanicWindow(false, 0f);
                }
            }
        }

        private static long ResolveHardVramCeilingBytes()
        {
            HardwareTierDetector.EnsureInitialized();
            long budgetBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
            return budgetBytes > 0L ? budgetBytes : CompactHardVramCeilingBytes;
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

            IAssetLifecyclePressureSink governor = _assetLifecycle;
            return governor != null && governor.TryStageExternalAddressableRelease(handle);
        }

        private bool TryReleaseExternalAddressableFault(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            IAssetLifecyclePressureSink governor = _assetLifecycle;
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
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Bundle handle table exhausted.", this);
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
            IVramPressureReadModel pressure = _vramPressure;
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

            if (!OpenOrAcquireTelemetryWritePointer(
                    out ContentAuthorityTelemetryEntry* telemetry,
                    out int* cursorPtr,
                    out IDataVault writeVault,
                    allowColdInitialization: false))
                return;

            try
            {
                int cursor = *cursorPtr;
                if ((uint)cursor >= TelemetryCapacity)
                    cursor = 0;

                telemetry[cursor] = new ContentAuthorityTelemetryEntry
                {
                    Frame = SystemDispatcher.CurrentFrameId,
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
            }
            finally
            {
                ReleaseTelemetryMutationGuard(writeVault);
            }

            if (nonFinite)
                _blackBoxDumpRequested = true;
        }

        private unsafe bool OpenOrAcquireTelemetryWritePointer(
            out ContentAuthorityTelemetryEntry* telemetry,
            out int* cursor,
            out IDataVault writeVault,
            bool allowColdInitialization = true)
        {
            telemetry = null;
            cursor = null;
            writeVault = null;

            if (!OpenOrAcquireTelemetryWriteBuffers(
                    out NativeArray<ContentAuthorityTelemetryEntry> telemetryBuffer,
                    out NativeArray<int> cursorBuffer,
                    out writeVault,
                    allowColdInitialization))
                return false;

            IDataVault guardVault = writeVault;
            bool keepGuard = false;
            try
            {
                telemetry = (ContentAuthorityTelemetryEntry*)telemetryBuffer.GetUnsafePtr();
                cursor = (int*)cursorBuffer.GetUnsafePtr();
                if (telemetry != null && cursor != null)
                {
                    keepGuard = true;
                    return true;
                }

                writeVault = null;
                return false;
            }
            finally
            {
                if (!keepGuard)
                    ReleaseTelemetryMutationGuard(guardVault);
            }
        }

        private bool OpenOrAcquireTelemetryWriteBuffers(
            out NativeArray<ContentAuthorityTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out IDataVault writeVault,
            bool allowColdInitialization = true)
        {
            telemetry = default;
            cursor = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!OpenOrAcquireBuffer(
                    vault,
                    ref _telemetryHandle,
                    BufferID.ContentAuthorityBlackBox,
                    TelemetryCapacity,
                    out _,
                    allowColdInitialization) ||
                !OpenOrAcquireBuffer(
                    vault,
                    ref _telemetryCursorHandle,
                    BufferID.ContentAuthorityTelemetryCursor,
                    1,
                    out _,
                    allowColdInitialization) ||
                !vault.TryAcquireMutationGuard(ContentTelemetryMutationGuardMask))
            {
                return false;
            }

            bool keepGuard = false;
            try
            {
                writeVault = vault;
                if (!vault.TryResolveHandle(in _telemetryHandle, out telemetry) ||
                    !vault.TryResolveHandle(in _telemetryCursorHandle, out cursor) ||
                    !telemetry.IsCreated ||
                    !cursor.IsCreated ||
                    telemetry.Length < TelemetryCapacity ||
                    cursor.Length < 1)
                {
                    telemetry = default;
                    cursor = default;
                    writeVault = null;
                    return false;
                }

                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    ReleaseTelemetryMutationGuard(vault);
            }
        }

        private unsafe int GetPendingLoadCount()
        {
            return TryReadPendingLoadCount(out int count)
                ? count
                : 0;
        }

        private bool TryReadPendingLoadCount(out int count)
        {
            count = 0;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _pendingLoadCountHandle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in _pendingLoadCountHandle, out NativeArray<int>.ReadOnly countBuffer) ||
                !countBuffer.IsCreated ||
                countBuffer.Length < 1)
            {
                return false;
            }

            int resolvedCount = countBuffer[0];
            if ((uint)resolvedCount <= PendingLoadCapacity)
            {
                count = resolvedCount;
                return true;
            }

            LogPendingLoadCountCorruption();
            return false;
        }

        private unsafe bool OpenOrAcquirePendingLoadWritePointers(
            out ContentPendingLoadState* pendingLoads,
            out int* count,
            out IDataVault writeVault,
            bool allowColdInitialization = true)
        {
            pendingLoads = null;
            count = null;
            writeVault = null;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!OpenOrAcquireBuffer(
                    vault,
                    ref _pendingLoadsHandle,
                    BufferID.ContentAuthorityPendingLoads,
                    PendingLoadCapacity,
                    out _,
                    allowColdInitialization) ||
                !OpenOrAcquireBuffer(
                    vault,
                    ref _pendingLoadCountHandle,
                    BufferID.ContentAuthorityPendingLoadCount,
                    1,
                    out _,
                    allowColdInitialization) ||
                !vault.TryAcquireMutationGuard(ContentPendingLoadMutationGuardMask))
            {
                return false;
            }

            bool keepGuard = false;
            try
            {
                writeVault = vault;
                if (!vault.TryResolveHandle(in _pendingLoadsHandle, out NativeArray<ContentPendingLoadState> pendingLoadsBuffer) ||
                    !vault.TryResolveHandle(in _pendingLoadCountHandle, out NativeArray<int> countBuffer) ||
                    !pendingLoadsBuffer.IsCreated ||
                    !countBuffer.IsCreated ||
                    pendingLoadsBuffer.Length < PendingLoadCapacity ||
                    countBuffer.Length < 1)
                {
                    writeVault = null;
                    return false;
                }

                pendingLoads = (ContentPendingLoadState*)pendingLoadsBuffer.GetUnsafePtr();
                count = (int*)countBuffer.GetUnsafePtr();
                if (pendingLoads != null &&
                    count != null &&
                    pendingLoadsBuffer.Length >= PendingLoadCapacity &&
                    countBuffer.Length >= 1)
                {
                    writeVault = vault;
                    keepGuard = true;
                    return true;
                }

                writeVault = null;
                return false;
            }
            finally
            {
                if (!keepGuard)
                    ReleasePendingLoadMutationGuard(vault);
            }
        }

        private unsafe bool OpenOrAcquirePendingLoadNormalizedWritePointers(
            out ContentPendingLoadState* pendingLoads,
            out int* countPtr,
            out int count,
            out IDataVault writeVault,
            bool allowColdInitialization = true)
        {
            count = 0;
            if (!OpenOrAcquirePendingLoadWritePointers(
                    out pendingLoads,
                    out countPtr,
                    out writeVault,
                    allowColdInitialization))
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
            if (_bundleRefs.Release(hash, SystemDispatcher.CurrentFrameIndex, out bool becameUnused) && becameUnused)
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
            ReleaseAuthorityVaultHandle(vault, ref _telemetryHandle);
            ReleaseAuthorityVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseAuthorityVaultHandle(vault, ref _pendingLoadsHandle);
            ReleaseAuthorityVaultHandle(vault, ref _pendingLoadCountHandle);
        }

        private static void ReleaseAuthorityVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.ContentAuthority)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private static bool OpenOrAcquireBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer,
            bool allowColdInitialization = true)
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
                if (!allowColdInitialization || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    return false;

                handle = vault.EnsureGenerationHandle<T>(
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

        private static void ReleaseTelemetryMutationGuard(IDataVault vault)
        {
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(ContentTelemetryMutationGuardMask);
        }

        private static void ReleasePendingLoadMutationGuard(IDataVault vault)
        {
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(ContentPendingLoadMutationGuardMask);
        }

        private bool TryReadExistingTelemetryBuffers(
            out NativeArray<ContentAuthorityTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor)
        {
            telemetry = default;
            cursor = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _telemetryHandle.BufferID != 0u &&
                   _telemetryCursorHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _telemetryHandle, out telemetry) &&
                   telemetry.Length >= TelemetryCapacity &&
                   vault.TryReadOnlyHandle(in _telemetryCursorHandle, out cursor) &&
                   cursor.Length >= 1;
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

        private void DumpBlackBox()
        {
            if (_blackBoxDumpedThisSession)
                return;

            if (!TryReadExistingTelemetryBuffers(
                    out NativeArray<ContentAuthorityTelemetryEntry>.ReadOnly telemetry,
                    out NativeArray<int>.ReadOnly cursorBuffer))
                return;

            string path = _blackBoxDumpPath;
            if (string.IsNullOrEmpty(path))
                return;

            int cursor = cursorBuffer[0];
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

        private void FlushPendingBlackBoxDump()
        {
            if (!_blackBoxDumpRequested)
                return;

            DumpBlackBox();
            if (_blackBoxDumpedThisSession)
                _blackBoxDumpRequested = false;
        }

        private static bool TryWriteBlackBox(
            string path,
            NativeArray<ContentAuthorityTelemetryEntry>.ReadOnly telemetry,
            int cursor)
        {
            if (string.IsNullOrEmpty(path) || telemetry.Length < TelemetryCapacity)
                return false;

            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "contentAuthorityBlackBoxDumpPayload";
            try
            {
                int entrySizeBytes = checked((int)BlackBoxEntrySizeBytes);
                int byteCount = (sizeof(ulong) + (sizeof(uint) * 3)) + (TelemetryCapacity * entrySizeBytes);
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ContentAuthorityRuntime),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                int writeCursor = 0;

                WriteUInt64LittleEndian(payload, ref writeCursor, BlackBoxMagic);
                WriteUInt32LittleEndian(payload, ref writeCursor, (uint)TelemetryCapacity);
                WriteInt32LittleEndian(payload, ref writeCursor, entrySizeBytes);
                WriteUInt32LittleEndian(payload, ref writeCursor, 0u);

                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    int index = cursor + i;
                    if (index >= TelemetryCapacity)
                        index -= TelemetryCapacity;

                    ContentAuthorityTelemetryEntry entry = telemetry[index];
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Frame);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Flags);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.FocusHash);
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.PendingLoads);
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.HologramsActive);
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.BundleRefCount);
                    WriteInt64LittleEndian(payload, ref writeCursor, entry.EstimatedVramBytes);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.VramPressure01);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.RamPressure01);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.StateHash);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Reserved0);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Reserved1);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Reserved2);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Reserved3);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Reserved4);
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, writeCursor);
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
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(ContentAuthorityRuntime), dumpPayloadLabel);
            }
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> target, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(target, ref cursor, math.asuint(value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> target, ref int cursor, long value)
        {
            WriteUInt64LittleEndian(target, ref cursor, unchecked((ulong)value));
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> target, ref int cursor, ulong value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
            target[cursor++] = (byte)(value >> 32);
            target[cursor++] = (byte)(value >> 40);
            target[cursor++] = (byte)(value >> 48);
            target[cursor++] = (byte)(value >> 56);
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> target, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(target, ref cursor, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> target, ref int cursor, uint value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
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
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Asset hash map missing.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingAssetHash(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No content registry entry.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingRuntimeDataVault(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] DataVault dependency unavailable on runtime content route.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleHandleTrackFailed(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to track Addressables bundle handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBundleHandleReleaseMiss(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No tracked Addressables bundle handle during release.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidBundleHandle(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid Addressables bundle handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidAsyncLoadTrack(uint hash, bool nullTarget)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Rejected async load tracking.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadVaultUnavailable(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load vault unavailable.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadCapacityExceeded(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load ledger full.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogAsyncLoadCompletionMiss(uint hash, bool nullTarget)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Async load completion had no pending entry.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidVfxPrewarmReference(int index, bool particle)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid VFX prewarm Addressables reference.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxPrewarmLedgerFull(bool particle)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle ledger full.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidVfxPrewarmHandle(int index, bool particle)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm returned invalid Addressables handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxResidentLedgerFull()
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Resident VFX handle ledger full; releasing completed prewarm handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxPrewarmFailed()
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle failed; releasing Addressables handle.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHologramProxyUnavailable(bool nullTarget)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy unavailable.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHologramPoolExhausted()
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy pool exhausted; pending asset will remain invisible until a proxy frees.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingLoadCountCorruption()
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending load vault count exceeded fixed capacity; cleared pending-load ledger.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBlackBoxDumpFailure(string path, Exception exception)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to write content blackbox dump.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBlackBoxPathFailure(Exception exception)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to resolve content blackbox dump path.");
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
            if ((_registeredTick && _registeredLateFrame && _registeredSlowTick && _registeredColdTick) || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            CacheDependencies();
            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            TryUnregisterDispatcherRoutes(clearPendingState: true);
        }

        private void TryUnregisterDispatcherRoutes(bool clearPendingState)
        {
            if (!_registeredTick && !_registeredLateFrame && !_registeredSlowTick && !_registeredColdTick)
                return;

            if (_registeredTick)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            if (_registeredSlowTick)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            if (_registeredColdTick)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);

            _registeredTick = false;
            _registeredLateFrame = false;
            _registeredSlowTick = false;
            _registeredColdTick = false;
            if (clearPendingState)
            {
                _pendingContentVisualSyncTick = false;
                _pendingAupCleanup = false;
                _pendingVramIntercept = false;
            }
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
            RebindDataVaultCold(GlobalRegistry.DataVault);
            if (_vramMonitor == null)
                _vramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            if (_vramPressure == null)
                _vramPressure = GlobalRegistry.VRAMPressureReadModel;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecyclePressureSink;
        }

        private void RebindDataVaultCold(IDataVault replacementVault, IDataVault releaseVaultFallback = null)
        {
            if (ReferenceEquals(_dataVault, replacementVault))
            {
                _bundleRefs.BindVault(_dataVault);
                EnsureAuthorityVaultBuffersCold();
                return;
            }

            ReleaseAuthorityVaultHandles(_dataVault ?? releaseVaultFallback);
            _bundleRefs.BindVault(replacementVault);
            _dataVault = replacementVault;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _pendingLoadsHandle = default;
            _pendingLoadCountHandle = default;
            EnsureAuthorityVaultBuffersCold();
        }

        private void EnsureAuthorityVaultBuffersCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            OpenOrAcquireBuffer(
                vault,
                ref _telemetryHandle,
                BufferID.ContentAuthorityBlackBox,
                TelemetryCapacity,
                out NativeArray<ContentAuthorityTelemetryEntry> _);
            OpenOrAcquireBuffer(
                vault,
                ref _telemetryCursorHandle,
                BufferID.ContentAuthorityTelemetryCursor,
                1,
                out NativeArray<int> _);
            OpenOrAcquireBuffer(
                vault,
                ref _pendingLoadsHandle,
                BufferID.ContentAuthorityPendingLoads,
                PendingLoadCapacity,
                out NativeArray<ContentPendingLoadState> _);
            OpenOrAcquireBuffer(
                vault,
                ref _pendingLoadCountHandle,
                BufferID.ContentAuthorityPendingLoadCount,
                1,
                out NativeArray<int> _);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultCold(currentService as IDataVault, previousService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as IVramPressureReadModel;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as IAssetLifecyclePressureSink;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherRoutes(clearPendingState: false);
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

    }

    public static class ContentTieredGroupPolicy
    {
        private const int SurvivalGraphicsMemoryMb = 2048;
        private const int OverkillGraphicsMemoryMb = 4096;
        private const float XrVisualBudgetCeiling01 = 0.42f;
        private const float CoreTierVisualCeiling01 = 0.38f;
        private const float HighResTierVisualCeiling01 = 0.68f;
        private const float OverkillDownloadThreshold01 = 0.74f;
        private static readonly int s_coldGraphicsMemoryMb = ResolveColdGraphicsMemoryMb();

        public const uint VisualFeatureSaltCrystals = 1u << 0;
        public const uint VisualFeatureVolumetricSiltWake = 1u << 1;
        public const uint VisualFeatureProceduralHullDents = 1u << 2;
        public const uint VisualFeatureRaymarchDetail = 1u << 3;
        public const uint VisualFeatureParallaxOcclusion16Tap = 1u << 4;
        public const uint DearLieOneDimensionalLut = 1u << 16;
        public const uint DearLieTriangleNoise = 1u << 17;
        public const uint DearLieDotProductVision = 1u << 18;
        private const uint ContinuousVisualFeatureMask =
            VisualFeatureSaltCrystals |
            VisualFeatureVolumetricSiltWake |
            VisualFeatureProceduralHullDents |
            VisualFeatureRaymarchDetail |
            VisualFeatureParallaxOcclusion16Tap |
            DearLieOneDimensionalLut |
            DearLieTriangleNoise |
            DearLieDotProductVision;

        public static bool CanDownload(ContentTier tier)
        {
            if (!IsValidTier(tier))
            {
                LogInvalidContentTier(tier);
                return false;
            }

            if (tier != ContentTier.Overkill)
                return true;

            return ResolveRuntimeVisualBudgetWeight01(ContentTier.Overkill) >= OverkillDownloadThreshold01;
        }

        public static ContentTier ResolveMaximumRuntimeTier()
        {
            return ResolveRuntimeVisualBudgetWeight01(ContentTier.Overkill) >= OverkillDownloadThreshold01
                ? ContentTier.Overkill
                : ContentTier.HighRes;
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
                return ResolveVisualBudgetForWeight(0f);
            }

            return ResolveVisualBudgetForWeight(ResolveRuntimeVisualBudgetWeight01(tier));
        }

        private static bool IsValidTier(ContentTier tier)
        {
            return tier <= ContentTier.Overkill;
        }

        internal static float ResolveRuntimeVisualBudgetWeight01(ContentTier tier)
        {
            return ResolveRuntimeVisualBudgetWeight01(
                tier,
                s_coldGraphicsMemoryMb,
                HectonXRRuntimeState.IsXRActive,
                HomeostasisBrain.GlobalQualityWeight);
        }

        internal static float ResolveRuntimeVisualBudgetWeight01(
            ContentTier tier,
            int graphicsMemoryMb,
            bool xrActive,
            float globalQualityWeight)
        {
            float globalWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float hardwareWeight = ResolveHardwareVisualCapacity01(graphicsMemoryMb);
            float platformCeiling = xrActive ? XrVisualBudgetCeiling01 : 1f;
            float tierCeiling = ResolveTierVisualCeiling01(tier);
            return Smooth01(math.min(math.min(globalWeight, hardwareWeight), math.min(platformCeiling, tierCeiling)));
        }

        internal static float ResolveHardwareVisualCapacity01(int graphicsMemoryMb)
        {
            if (graphicsMemoryMb <= 0)
                return HighResTierVisualCeiling01;

            float range = math.max(1f, OverkillGraphicsMemoryMb - SurvivalGraphicsMemoryMb);
            float t = math.saturate((graphicsMemoryMb - SurvivalGraphicsMemoryMb) / range);
            return Smooth01(t);
        }

        private static float ResolveTierVisualCeiling01(ContentTier tier)
        {
            if (tier == ContentTier.Overkill)
                return 1f;

            if (tier == ContentTier.HighRes)
                return HighResTierVisualCeiling01;

            return CoreTierVisualCeiling01;
        }

        private static int ResolveColdGraphicsMemoryMb()
        {
            return math.max(0, SystemInfo.graphicsMemorySize);
        }

        private static ContentVisualFeatureBudget ResolveVisualBudgetForWeight(float visualWeight01)
        {
            float weight = Smooth01(visualWeight01);
            float pomWeight = SmoothRange01(0.28f, 1f, weight);
            float hullDentWeight = SmoothRange01(0.48f, 1f, weight);
            float siltWakeWeight = SmoothRange01(0.18f, 1f, weight);
            float saltCrystalWeight = SmoothRange01(0.12f, 1f, weight);
            return new ContentVisualFeatureBudget
            {
                FeatureMask = ResolveVisualFeatureMask(),
                MaxParticles = (ushort)RoundLerpInt(512f, 16384f, weight, 512, 16384),
                RaymarchSteps = (byte)RoundLerpInt(8f, 64f, weight, 8, 64),
                PomTaps = (byte)RoundLerpInt(0f, 16f, pomWeight, 0, 16),
                SiltWakeLayers = (byte)RoundLerpInt(1f, 4f, weight, 1, 4),
                SaltCrystalLayers = (byte)RoundLerpInt(1f, 3f, weight, 1, 3),
                HullDentOctaves = (byte)RoundLerpInt(1f, 4f, hullDentWeight, 1, 4),
                VisualFeatureWeightQ8 = EncodeUnitQ8(weight),
                PomWeightQ8 = EncodeUnitQ8(pomWeight),
                SiltWakeWeightQ8 = EncodeUnitQ8(siltWakeWeight),
                HullDentWeightQ8 = EncodeUnitQ8(hullDentWeight),
                SaltCrystalWeightQ8 = EncodeUnitQ8(saltCrystalWeight)
            };
        }

        private static uint ResolveVisualFeatureMask()
        {
            return ContinuousVisualFeatureMask;
        }

        private static byte EncodeUnitQ8(float value)
        {
            float safe = math.saturate(math.isfinite(value) ? value : 0f);
            return (byte)math.round(safe * 255f);
        }

        private static int RoundLerpInt(float min, float max, float weight01, int floor, int ceiling)
        {
            return math.clamp((int)math.round(math.lerp(min, max, math.saturate(weight01))), floor, ceiling);
        }

        private static float SmoothRange01(float start, float end, float value)
        {
            float range = math.max(0.0001f, end - start);
            return Smooth01((value - start) / range);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidContentTier(ContentTier tier)
        {
            Hecton8.Core.H8Debug.LogError("[ContentTieredGroupPolicy] Invalid content tier value.");
        }
    }
}

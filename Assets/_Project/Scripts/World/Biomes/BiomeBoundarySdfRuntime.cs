using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.World;
using Hecton8.World.Biomes.Contracts;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.Biomes
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4310)]
    public sealed class BiomeBoundarySdfRuntime : MonoBehaviour, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001BiomeBoundarySdfRuntimeSignalPushDropCount;
        internal static BiomeBoundarySdfRuntime ActiveRuntimeInstance { get; private set; }

        private const int BiomeHeatmapResolution = 256;
        private const int BiomeHeatmapPixelCount = BiomeHeatmapResolution * BiomeHeatmapResolution;
        private const int TelemetryCapacity = 300;
        private const int BlackBoxDumpEntryBytes = 62;
        private const float DefaultCellSizeMeters = 50f;
        private const float DefaultBlendWidthMeters = 50f;
        private const uint RuntimeContextHash = 0x42424C44u;
        private const uint InvalidResultHash = 0x4242494Eu;
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_BIOME_TRANSITION_BLENDER.bin";
        private const SystemID VaultOwnerSystemId = SystemID.WorldStreaming;
        private const BufferID GlobalBiomeMapBufferId = BufferID.BiomeBoundaryGlobalBiomeMap;
        private const BufferID GlobalBiomeHashMapBufferId = BufferID.BiomeBoundaryGlobalBiomeHashMap;
        private const BufferID TelemetryRingBufferId = BufferID.BiomeBoundaryTelemetryRing;
        private static readonly ulong BiomeMapMutationGuardMask =
            MutationGuardBit(GlobalBiomeMapBufferId) |
            MutationGuardBit(GlobalBiomeHashMapBufferId);

        [Header("Heatmap")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float heatmapOriginAupX;
        [SerializeField] private float heatmapOriginAupZ;
        [SerializeField, Min(0.5f)] private float heatmapCellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Min(0.01f)] private float blendWidthMeters = DefaultBlendWidthMeters;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugMapReady;
        [SerializeField] private byte _debugBiomeA;
        [SerializeField] private byte _debugBiomeB;
        [SerializeField] private float _debugBlend01;
        [SerializeField] private int _debugSampleDiameter;
        [SerializeField] private uint _debugPublishedSequence;

        private VaultGenerationHandle<byte> _globalBiomeMapHandle;
        private VaultGenerationHandle<uint> _globalBiomeHashMapHandle;
        private VaultGenerationHandle<BiomeBoundaryTelemetryEntry> _telemetryRingHandle;
        private IDataVault _dataVault;
        private IDataVault _biomeMapGuardVault;
        private IDataVault _telemetryWriteVault;
        private IPlayerRuntimeContext _playerContext;
        private SampleScratchOwner _sampleScratch;
        private int _telemetryCursor;
        private int _telemetryCount;
        private int _lastBlobBytes;
        private ulong _lastBlobChecksum;
        private bool _nativeStorageReady;
        private bool _registeredSlowTick;
        private bool _originShiftRegistered;
        private bool _registeredHotSwapListener;
        private uint _lastOriginShiftSequence;
        private uint _sequence;
        private readonly BiomeBoundaryTelemetryEntry[] _blackBoxDumpSnapshot = new BiomeBoundaryTelemetryEntry[TelemetryCapacity];
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpInFlight;
        private string _blackBoxDumpRootCold;

        private static readonly WaitCallback s_blackBoxDumpWorker = WriteBlackBoxDumpWorker;

        private struct SampleScratchOwner
        {
            public NativeArray<BiomeBoundarySdfResult> Result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!TryClaimActiveRuntime())
                return;

            RefreshColdRegistryReferences();
            EnsureNativeStorage();
        }

        private void OnEnable()
        {
            if (!TryClaimActiveRuntime())
                return;

            RefreshColdRegistryReferences();
            EnsureNativeStorage();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterOriginShift();
        }

        private void Start()
        {
            RefreshColdRegistryReferences();
            EnsureNativeStorage();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterOriginShift();
            RefreshGlobalBiomeMapIfDirty();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterOriginShift();
            TryUnregisterHotSwapListener();
            _playerContext = null;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterOriginShift();
            TryUnregisterHotSwapListener();
            DisposeNativeStorage();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            if (!_nativeStorageReady)
                return;

            RefreshGlobalBiomeMapIfDirty();
            if (!_debugMapReady || !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            double3 absolute = playerAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absolute)))
            {
                BiomeBoundarySdfResult invalidResult = default;
                RecordTelemetry(in playerAup, in invalidResult, (byte)BiomeBoundarySdfFlags.InvalidInput);
                DumpBlackBox();
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidResultHash, RuntimeContextHash, 1f);
                return;
            }

            BiomeBoundarySdfSettings settings = new BiomeBoundarySdfSettings
            {
                Resolution = new int2(BiomeHeatmapResolution, BiomeHeatmapResolution),
                OriginAupXZ = new double2(heatmapOriginAupX, heatmapOriginAupZ),
                CellSizeMeters = math.max(0.5f, heatmapCellSizeMeters),
                BlendWidthMeters = math.max(0.01f, blendWidthMeters),
                SampleRadiusCells = 2,
                Flags = (byte)BiomeBoundarySdfFlags.None
            };

            if (!TryReadSampleInputs(
                    out NativeArray<byte>.ReadOnly globalBiomeMap,
                    out NativeArray<uint>.ReadOnly globalBiomeHashMap))
            {
                _debugMapReady = false;
                return;
            }

            var job = new BiomeBoundarySdfJobs.BiomeBoundarySdfSampleJob
            {
                GlobalBiomeMap = globalBiomeMap,
                BiomeHashMap = globalBiomeHashMap,
                Result = _sampleScratch.Result,
                Settings = settings,
                SampleAupXZ = new double2(absolute.x, absolute.z)
            };

            job.Execute();
            BiomeBoundarySdfResult result = _sampleScratch.Result[0];

            if (!IsFiniteResult(in result))
            {
                RecordTelemetry(in playerAup, in result, (byte)BiomeBoundarySdfFlags.InvalidInput);
                DumpBlackBox();
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidResultHash, RuntimeContextHash, result.BlendFactor01);
                return;
            }

            PublishGradientSignal(in playerAup, in result, settings.CellSizeMeters);
            RecordTelemetry(in playerAup, in result, result.Flags);
            _debugBiomeA = result.BiomeA;
            _debugBiomeB = result.BiomeB;
            _debugBlend01 = result.BlendFactor01;
            _debugSampleDiameter = result.SampleDiameter;
            _debugPublishedSequence = _sequence;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastOriginShiftSequence = shiftData.Sequence;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                if (_playerContext != null && _playerContext.PlayerTransform != null)
                    playerTransform = _playerContext.PlayerTransform;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredSlowTick = false;
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
        }

        private bool TryClaimActiveRuntime()
        {
            if (!Application.isPlaying)
                return true;

            if (ActiveRuntimeInstance == null)
            {
                ActiveRuntimeInstance = this;
                return true;
            }

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return true;

            enabled = false;
            return false;
        }

        private void TryRegister()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void RefreshColdRegistryReferences()
        {
            CacheDataVaultCold();
            TryEnsureBlackBoxDumpRootCold();

            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_playerContext != null && _playerContext.PlayerTransform != null)
                playerTransform = _playerContext.PlayerTransform;
        }

        private void CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryRegisterOriginShift()
        {
            if (_originShiftRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShift()
        {
            if (!_originShiftRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftRegistered = false;
        }

        private void EnsureNativeStorage()
        {
            if (_nativeStorageReady)
                return;

            CacheDataVaultCold();
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool ready =
                EnsureBiomeVaultBuffer(vault, GlobalBiomeMapBufferId, BiomeHeatmapPixelCount, ref _globalBiomeMapHandle) &&
                EnsureBiomeVaultBuffer(vault, GlobalBiomeHashMapBufferId, BiomeHeatmapPixelCount, ref _globalBiomeHashMapHandle) &&
                EnsureBiomeVaultBuffer(vault, TelemetryRingBufferId, TelemetryCapacity, ref _telemetryRingHandle) &&
                EnsureSampleScratchCold();

            if (!ready)
                return;

            _lastBlobBytes = -1;
            _lastBlobChecksum = 0UL;
            _nativeStorageReady = true;
        }

        private void DisposeNativeStorage()
        {
            ReleaseBiomeVaultBuffers();
            _nativeStorageReady = false;
            _debugMapReady = false;
            _telemetryCursor = 0;
            _telemetryCount = 0;
        }

        private void RefreshGlobalBiomeMapIfDirty()
        {
            if (!_nativeStorageReady)
                return;

            int residentBytes = H8StaticDataArena.IsLoaded ? H8StaticDataArena.ByteLength : 0;
            ulong checksum = H8StaticDataArena.IsLoaded ? H8StaticDataArena.Header.Checksum64 : 0UL;
            if (_lastBlobBytes == residentBytes && _lastBlobChecksum == checksum)
                return;

            if (!H8StaticDataArena.IsLoaded)
            {
                ClearGlobalBiomeMap();
                _debugMapReady = false;
                _lastBlobBytes = residentBytes;
                _lastBlobChecksum = checksum;
                return;
            }

            if (!TryAcquireBiomeMapWriteBuffers(out NativeArray<byte> globalBiomeMap, out NativeArray<uint> globalBiomeHashMap))
            {
                _debugMapReady = false;
                return;
            }

            try
            {
                for (int y = 0; y < BiomeHeatmapResolution; y++)
                {
                    int rowOffset = y * BiomeHeatmapResolution;
                    for (int x = 0; x < BiomeHeatmapResolution; x++)
                    {
                        int index = rowOffset + x;
                        if (H8StaticDataArena.TryGetBiomeHeatmapCell(x, y, out uint biomeHash))
                        {
                            globalBiomeHashMap[index] = biomeHash;
                            globalBiomeMap[index] = ResolveBiomeHeatmapByte(biomeHash);
                        }
                        else
                        {
                            globalBiomeHashMap[index] = 0u;
                            globalBiomeMap[index] = 0;
                        }
                    }
                }
            }
            finally
            {
                ReleaseBiomeMapWriteLocks();
            }

            _debugMapReady = true;
            _lastBlobBytes = residentBytes;
            _lastBlobChecksum = checksum;
        }

        private void ClearGlobalBiomeMap()
        {
            if (!TryAcquireBiomeMapWriteBuffers(out NativeArray<byte> globalBiomeMap, out NativeArray<uint> globalBiomeHashMap))
                return;

            try
            {
                for (int i = 0; i < BiomeHeatmapPixelCount; i++)
                {
                    globalBiomeMap[i] = 0;
                    globalBiomeHashMap[i] = 0u;
                }
            }
            finally
            {
                ReleaseBiomeMapWriteLocks();
            }
        }

        private bool TryAcquireBiomeMapWriteBuffers(
            out NativeArray<byte> globalBiomeMap,
            out NativeArray<uint> globalBiomeHashMap)
        {
            globalBiomeMap = default;
            globalBiomeHashMap = default;
            IDataVault vault = _dataVault;
            if (!_nativeStorageReady || vault == null || _biomeMapGuardVault != null)
                return false;

            bool guardHeld = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(BiomeMapMutationGuardMask))
                    return false;

                guardHeld = true;
                if (_globalBiomeMapHandle.BufferID != (uint)GlobalBiomeMapBufferId ||
                    _globalBiomeHashMapHandle.BufferID != (uint)GlobalBiomeHashMapBufferId ||
                    !vault.TryResolveHandle(in _globalBiomeMapHandle, out globalBiomeMap) ||
                    !vault.TryResolveHandle(in _globalBiomeHashMapHandle, out globalBiomeHashMap) ||
                    globalBiomeMap.Length < BiomeHeatmapPixelCount ||
                    globalBiomeHashMap.Length < BiomeHeatmapPixelCount)
                {
                    return false;
                }

                _biomeMapGuardVault = vault;
                guardHeld = false;
                return true;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(BiomeMapMutationGuardMask);
            }
        }

        private void ReleaseBiomeMapWriteLocks()
        {
            IDataVault vault = _biomeMapGuardVault;
            _biomeMapGuardVault = null;
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(BiomeMapMutationGuardMask);
        }

        private bool TryReadSampleInputs(
            out NativeArray<byte>.ReadOnly globalBiomeMap,
            out NativeArray<uint>.ReadOnly globalBiomeHashMap)
        {
            globalBiomeMap = default;
            globalBiomeHashMap = default;
            IDataVault vault = _dataVault;
            if (!_nativeStorageReady || vault == null || !_sampleScratch.Result.IsCreated || _sampleScratch.Result.Length < 1)
                return false;

            return _globalBiomeMapHandle.BufferID == (uint)GlobalBiomeMapBufferId &&
                   _globalBiomeHashMapHandle.BufferID == (uint)GlobalBiomeHashMapBufferId &&
                   vault.TryReadOnlyHandle(in _globalBiomeMapHandle, out globalBiomeMap) &&
                   vault.TryReadOnlyHandle(in _globalBiomeHashMapHandle, out globalBiomeHashMap) &&
                   globalBiomeMap.Length >= BiomeHeatmapPixelCount &&
                   globalBiomeHashMap.Length >= BiomeHeatmapPixelCount;
        }

        private bool TryAcquireTelemetryWriteBuffer(out NativeArray<BiomeBoundaryTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            if (!_nativeStorageReady ||
                vault == null ||
                _telemetryWriteVault != null ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, VaultOwnerSystemId, out telemetryRing))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (telemetryRing.IsCreated && telemetryRing.Length > 0)
                {
                    _telemetryWriteVault = vault;
                    keepLock = true;
                    return true;
                }

                telemetryRing = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
                    telemetryRing = default;
                }
            }
        }

        private void ReleaseTelemetryWriteLock()
        {
            IDataVault vault = _telemetryWriteVault;
            _telemetryWriteVault = null;
            vault?.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
        }

        private bool TryReadTelemetryRing(out NativeArray<BiomeBoundaryTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            return _nativeStorageReady &&
                   _dataVault != null &&
                   _telemetryRingHandle.BufferID == (uint)TelemetryRingBufferId &&
                   _dataVault.TryReadOnlyHandle(in _telemetryRingHandle, out telemetryRing) &&
                   telemetryRing.Length > 0;
        }

        private bool EnsureBiomeVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsExactVaultHandle(vault, in handle, bufferId, requiredLength))
                return true;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return IsExactVaultHandle(vault, in handle, bufferId, requiredLength);
        }

        private static bool IsExactVaultHandle<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   handle.BufferID == (uint)bufferId &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                   existing.Length >= requiredLength;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            ReleaseBiomeVaultBuffers();
            _dataVault = currentVault;
            _nativeStorageReady = false;
            _debugMapReady = false;
            _lastBlobBytes = -1;
            _lastBlobChecksum = 0UL;
            _telemetryCursor = 0;
            _telemetryCount = 0;
            if (isActiveAndEnabled)
            {
                EnsureNativeStorage();
                RefreshGlobalBiomeMapIfDirty();
            }
        }

        private void ReleaseBiomeVaultBuffers()
        {
            ReleaseBiomeMapWriteLocks();
            ReleaseTelemetryWriteLock();
            ReleaseBiomeVaultHandle(ref _globalBiomeMapHandle);
            ReleaseBiomeVaultHandle(ref _globalBiomeHashMapHandle);
            ReleaseBiomeVaultHandle(ref _telemetryRingHandle);
            DisposeSampleScratchCold();
        }

        private bool EnsureSampleScratchCold()
        {
            if (_sampleScratch.Result.IsCreated && _sampleScratch.Result.Length >= 1)
                return true;

            DisposeSampleScratchCold();
            _sampleScratch.Result = new NativeArray<BiomeBoundarySdfResult>(
                1,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_sampleScratch.Result, nameof(BiomeBoundarySdfRuntime), "_sampleResultScratch", NativeAllocationLifetime.Scene);
            return _sampleScratch.Result.IsCreated && _sampleScratch.Result.Length >= 1;
        }

        private void DisposeSampleScratchCold()
        {
            if (!_sampleScratch.Result.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_sampleScratch.Result);
            _sampleScratch.Result.Dispose();
            _sampleScratch.Result = default;
        }

        private void ReleaseBiomeVaultHandle<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (_dataVault != null && handle.BufferID != 0u)
                _dataVault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            return false;
        }
        private void PublishGradientSignal(in AbsoluteUniversePosition playerAup, in BiomeBoundarySdfResult result, float cellSizeMeters)
        {
            _sequence++;
            var signal = new BiomeGradientSignal
            {
                PositionAup = playerAup,
                BiomeAHash = result.BiomeAHash,
                BiomeBHash = result.BiomeBHash,
                BlendFactor01 = math.saturate(result.BlendFactor01),
                BoundaryDistanceMeters = math.max(0f, result.BoundaryDistanceMeters),
                CellSizeMeters = math.max(0.5f, cellSizeMeters),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BiomeA = result.BiomeA,
                BiomeB = result.BiomeB,
                SampleDiameter = result.SampleDiameter,
                Flags = result.Flags
            };

            SignalBus<BiomeGradientSignal>.TryPushTracked(in signal, ref s_x001BiomeBoundarySdfRuntimeSignalPushDropCount);
        }

        private static bool IsFiniteResult(in BiomeBoundarySdfResult result)
        {
            return math.isfinite(result.BlendFactor01) &&
                   math.isfinite(result.BoundaryDistanceMeters) &&
                   math.isfinite(result.PrimaryWeight) &&
                   math.isfinite(result.SecondaryWeight);
        }

        private void RecordTelemetry(in AbsoluteUniversePosition playerAup, in BiomeBoundarySdfResult result, byte flags)
        {
            if (!TryAcquireTelemetryWriteBuffer(out NativeArray<BiomeBoundaryTelemetryEntry> telemetryRing))
                return;

            try
            {
                int index = _telemetryCursor;
                BiomeBoundaryTelemetryEntry entry = default;
                entry.FrameIndex = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
                entry.Sequence = _sequence;
                entry.OriginShiftSequence = _lastOriginShiftSequence;
                entry.StateHash = HashState(in playerAup, in result, flags);
                entry.GridX = playerAup.GridX;
                entry.GridZ = playerAup.GridZ;
                entry.LocalX = playerAup.LocalX;
                entry.LocalZ = playerAup.LocalZ;
                entry.BiomeAHash = result.BiomeAHash;
                entry.BiomeBHash = result.BiomeBHash;
                entry.BlendFactor01 = result.BlendFactor01;
                entry.BoundaryDistanceMeters = result.BoundaryDistanceMeters;
                entry.MacroCellX = (short)math.clamp(result.MacroCell.x, short.MinValue, short.MaxValue);
                entry.MacroCellY = (short)math.clamp(result.MacroCell.y, short.MinValue, short.MaxValue);
                entry.Flags = flags;
                entry.SampleDiameter = result.SampleDiameter;
                telemetryRing[index] = entry;

                index++;
                _telemetryCursor = index >= telemetryRing.Length ? 0 : index;
                if (_telemetryCount < telemetryRing.Length)
                    _telemetryCount++;
            }
            finally
            {
                ReleaseTelemetryWriteLock();
            }
        }

        private void DumpBlackBox()
        {
            if (!TryReadTelemetryRing(out NativeArray<BiomeBoundaryTelemetryEntry>.ReadOnly telemetryRing))
                return;

            if (Volatile.Read(ref _blackBoxDumpInFlight) != 0)
                return;

            if (_blackBoxDumpRootCold == null || _blackBoxDumpRootCold.Length == 0)
                return;

            Volatile.Write(ref _blackBoxDumpInFlight, 1);
            if (!TryStageBlackBoxDumpSnapshot(telemetryRing))
            {
                Volatile.Write(ref _blackBoxDumpInFlight, 0);
                return;
            }

            try
            {
                if (ThreadPool.QueueUserWorkItem(s_blackBoxDumpWorker, this))
                    return;
            }
            catch (NotSupportedException)
            {
            }

            Volatile.Write(ref _blackBoxDumpInFlight, 0);
        }

        private bool TryStageBlackBoxDumpSnapshot(NativeArray<BiomeBoundaryTelemetryEntry>.ReadOnly telemetryRing)
        {
            if (!telemetryRing.IsCreated)
                return false;

            BiomeBoundaryTelemetryEntry[] snapshot = _blackBoxDumpSnapshot;
            if (snapshot == null || snapshot.Length < TelemetryCapacity)
                return false;

            int capacity = telemetryRing.Length;
            if (capacity <= 0)
                return false;

            int count = math.min(_telemetryCount, capacity);
            if (count <= 0)
                return false;

            int start = count == capacity ? _telemetryCursor : 0;
            for (int i = 0; i < count; i++)
            {
                int entryIndex = start + i;
                if (entryIndex >= capacity)
                    entryIndex -= capacity;

                snapshot[i] = telemetryRing[entryIndex];
            }

            _blackBoxDumpSnapshotCount = count;
            return true;
        }

        private static void WriteBlackBoxDumpWorker(object state)
        {
            BiomeBoundarySdfRuntime runtime = state as BiomeBoundarySdfRuntime;
            if (runtime == null)
                return;

            try
            {
                runtime.TryWriteBlackBoxSnapshotCold();
            }
            finally
            {
                Volatile.Write(ref runtime._blackBoxDumpInFlight, 0);
            }
        }

        private bool TryWriteBlackBoxSnapshotCold()
        {
            int count = _blackBoxDumpSnapshotCount;
            if (count <= 0)
                return false;

            try
            {
                string root = _blackBoxDumpRootCold;
                if (root == null || root.Length == 0)
                    return false;

                string fullPath = Path.Combine(root, BlackBoxDumpPath);
                int byteCount = count * BlackBoxDumpEntryBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    int cursor = 0;
                    for (int i = 0; i < count; i++)
                        WriteTelemetryEntry(payload, ref cursor, _blackBoxDumpSnapshot[i]);

                    return cursor == byteCount && NativeFaultDumpWriter.TryWriteAll(fullPath, payload, byteCount);
                }
                finally
                {
                    if (payload.IsCreated)
                        payload.Dispose();
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static void WriteTelemetryEntry(NativeArray<byte> payload, ref int cursor, BiomeBoundaryTelemetryEntry entry)
        {
            WriteInt32LittleEndian(payload, ref cursor, entry.FrameIndex);
            WriteUInt32LittleEndian(payload, ref cursor, entry.Sequence);
            WriteUInt32LittleEndian(payload, ref cursor, entry.OriginShiftSequence);
            WriteUInt32LittleEndian(payload, ref cursor, entry.StateHash);
            WriteInt64LittleEndian(payload, ref cursor, entry.GridX);
            WriteInt64LittleEndian(payload, ref cursor, entry.GridZ);
            WriteFloatLittleEndian(payload, ref cursor, entry.LocalX);
            WriteFloatLittleEndian(payload, ref cursor, entry.LocalZ);
            WriteUInt32LittleEndian(payload, ref cursor, entry.BiomeAHash);
            WriteUInt32LittleEndian(payload, ref cursor, entry.BiomeBHash);
            WriteFloatLittleEndian(payload, ref cursor, entry.BlendFactor01);
            WriteFloatLittleEndian(payload, ref cursor, entry.BoundaryDistanceMeters);
            WriteInt16LittleEndian(payload, ref cursor, entry.MacroCellX);
            WriteInt16LittleEndian(payload, ref cursor, entry.MacroCellY);
            payload[cursor++] = entry.Flags;
            payload[cursor++] = entry.SampleDiameter;
        }

        private static void WriteInt16LittleEndian(NativeArray<byte> payload, ref int cursor, short value)
        {
            WriteUInt16LittleEndian(payload, ref cursor, (ushort)value);
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> payload, ref int cursor, ushort value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int cursor, uint value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
            payload[cursor++] = (byte)(value >> 16);
            payload[cursor++] = (byte)(value >> 24);
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> payload, ref int cursor, long value)
        {
            WriteUInt64LittleEndian(payload, ref cursor, (ulong)value);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> payload, ref int cursor, ulong value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
            WriteUInt32LittleEndian(payload, ref cursor, (uint)(value >> 32));
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> payload, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, math.asuint(value));
        }

        private bool TryEnsureBlackBoxDumpRootCold()
        {
            try
            {
                string dataPath = Application.dataPath;
                if (string.IsNullOrEmpty(dataPath))
                    return false;

                _blackBoxDumpRootCold = Path.Combine(dataPath, "..");
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static unsafe byte ResolveBiomeHeatmapByte(uint biomeHash)
        {
            if (biomeHash == 0u)
                return 0;

            if (TryResolveBiomeRecord(biomeHash, out H8BiomeRecord record))
                return (byte)math.clamp((int)record.RecordIndex + 1, 1, 255);

            uint folded = biomeHash ^ (biomeHash >> 8) ^ (biomeHash >> 16) ^ (biomeHash >> 24);
            return (byte)(1u + folded % 255u);
        }

        private static unsafe bool TryResolveBiomeRecord(uint biomeHash, out H8BiomeRecord record)
        {
            record = default;
            ReadOnlySpan<H8BiomeRecord> records = H8StaticDataArena.GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes);

            if (records.Length <= 0)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                H8BiomeRecord candidate = records[mid];
                if (candidate.BiomeHash == biomeHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.BiomeHash < biomeHash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return false;
        }

        private static uint HashState(in AbsoluteUniversePosition playerAup, in BiomeBoundarySdfResult result, byte flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashUInt(hash, (uint)playerAup.GridX);
                hash = HashUInt(hash, (uint)(playerAup.GridX >> 32));
                hash = HashUInt(hash, (uint)playerAup.GridZ);
                hash = HashUInt(hash, (uint)(playerAup.GridZ >> 32));
                hash = HashUInt(hash, (uint)math.asint(playerAup.LocalX));
                hash = HashUInt(hash, (uint)math.asint(playerAup.LocalZ));
                hash = HashUInt(hash, result.BiomeAHash);
                hash = HashUInt(hash, result.BiomeBHash);
                hash = HashUInt(hash, (uint)math.asint(result.BlendFactor01));
                hash = HashUInt(hash, flags);
                return hash;
            }
        }

        private static uint HashUInt(uint hash, uint value)
        {
            unchecked
            {
                hash = (hash ^ value) * 16777619u;
                hash = (hash ^ (value >> 16)) * 16777619u;
                return hash;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct BiomeBoundaryTelemetryEntry
        {
            [FieldOffset(0)] public int FrameIndex;
            [FieldOffset(4)] public uint Sequence;
            [FieldOffset(8)] public uint OriginShiftSequence;
            [FieldOffset(12)] public uint StateHash;
            [FieldOffset(16)] public long GridX;
            [FieldOffset(24)] public long GridZ;
            [FieldOffset(32)] public float LocalX;
            [FieldOffset(36)] public float LocalZ;
            [FieldOffset(40)] public uint BiomeAHash;
            [FieldOffset(44)] public uint BiomeBHash;
            [FieldOffset(48)] public float BlendFactor01;
            [FieldOffset(52)] public float BoundaryDistanceMeters;
            [FieldOffset(56)] public short MacroCellX;
            [FieldOffset(58)] public short MacroCellY;
            [FieldOffset(60)] public byte Flags;
            [FieldOffset(61)] public byte SampleDiameter;
            [FieldOffset(62)] public ushort _pad0;
        }
    }
}

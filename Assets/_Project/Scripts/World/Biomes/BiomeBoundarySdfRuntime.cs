using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
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
    public sealed class BiomeBoundarySdfRuntime : MonoBehaviour, ISlowTickable, IOriginShiftListener
    {
        internal static BiomeBoundarySdfRuntime ActiveRuntimeInstance { get; private set; }

        private const int BiomeHeatmapResolution = 256;
        private const int BiomeHeatmapPixelCount = BiomeHeatmapResolution * BiomeHeatmapResolution;
        private const int TelemetryCapacity = 300;
        private const float DefaultCellSizeMeters = 50f;
        private const float DefaultBlendWidthMeters = 50f;
        private const uint RuntimeContextHash = 0x42424C44u;
        private const uint InvalidResultHash = 0x4242494Eu;
        private const string NativeMemoryOwner = nameof(BiomeBoundarySdfRuntime);
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_BIOME_TRANSITION_BLENDER.bin";

        [Header("Heatmap")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float heatmapOriginAupX;
        [SerializeField] private float heatmapOriginAupZ;
        [SerializeField, Min(0.5f)] private float heatmapCellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Min(0.01f)] private float blendWidthMeters = DefaultBlendWidthMeters;
        [SerializeField] private bool forceLowTierKernel;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugMapReady;
        [SerializeField] private byte _debugBiomeA;
        [SerializeField] private byte _debugBiomeB;
        [SerializeField] private float _debugBlend01;
        [SerializeField] private int _debugSampleDiameter;
        [SerializeField] private uint _debugPublishedSequence;

        private NativeArray<byte> _globalBiomeMap;
        private NativeArray<uint> _globalBiomeHashMap;
        private NativeArray<BiomeBoundarySdfResult> _sampleResult;
        private NativeArray<BiomeBoundaryTelemetryEntry> _telemetryRing;
        private int _telemetryCursor;
        private int _telemetryCount;
        private int _lastBlobBytes;
        private ulong _lastBlobChecksum;
        private bool _nativeStorageReady;
        private bool _registeredSlowTick;
        private bool _originShiftRegistered;
        private uint _lastOriginShiftSequence;
        private uint _sequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!TryClaimActiveRuntime())
                return;

            EnsureNativeStorage();
        }

        private void OnEnable()
        {
            if (!TryClaimActiveRuntime())
                return;

            EnsureNativeStorage();
            TryRegister();
            TryRegisterOriginShift();
        }

        private void Start()
        {
            TryRegister();
            TryRegisterOriginShift();
            RefreshGlobalBiomeMapIfDirty();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterOriginShift();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterOriginShift();
            DisposeNativeStorage();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            EnsureNativeStorage();
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

            bool lowTier = ResolveLowTierKernel();
            BiomeBoundarySdfSettings settings = new BiomeBoundarySdfSettings
            {
                Resolution = new int2(BiomeHeatmapResolution, BiomeHeatmapResolution),
                OriginAupXZ = new double2(heatmapOriginAupX, heatmapOriginAupZ),
                CellSizeMeters = math.max(0.5f, heatmapCellSizeMeters),
                BlendWidthMeters = math.max(0.01f, blendWidthMeters),
                SampleRadiusCells = lowTier ? 1 : 2,
                Flags = (byte)(lowTier ? BiomeBoundarySdfFlags.LowTierKernel : BiomeBoundarySdfFlags.None)
            };

            var job = new BiomeBoundarySdfJobs.BiomeBoundarySdfSampleJob
            {
                GlobalBiomeMap = _globalBiomeMap,
                BiomeHashMap = _globalBiomeHashMap,
                Result = _sampleResult,
                Settings = settings,
                SampleAupXZ = new double2(absolute.x, absolute.z)
            };

            job.Run();

            BiomeBoundarySdfResult result = _sampleResult[0];
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

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
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

            _globalBiomeMap = new NativeArray<byte>(BiomeHeatmapPixelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _globalBiomeHashMap = new NativeArray<uint>(BiomeHeatmapPixelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _sampleResult = new NativeArray<BiomeBoundarySdfResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _telemetryRing = new NativeArray<BiomeBoundaryTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            NativeMemorySentinel.RegisterNativeArray(_globalBiomeMap, NativeMemoryOwner, nameof(_globalBiomeMap), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_globalBiomeHashMap, NativeMemoryOwner, nameof(_globalBiomeHashMap), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_sampleResult, NativeMemoryOwner, nameof(_sampleResult), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeAllocationLifetime.Scene);
            _lastBlobBytes = -1;
            _lastBlobChecksum = 0UL;
            _nativeStorageReady = true;
        }

        private void DisposeNativeStorage()
        {
            DisposeNativeArray(ref _globalBiomeMap);
            DisposeNativeArray(ref _globalBiomeHashMap);
            DisposeNativeArray(ref _sampleResult);
            DisposeNativeArray(ref _telemetryRing);
            _nativeStorageReady = false;
            _debugMapReady = false;
            _telemetryCursor = 0;
            _telemetryCount = 0;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private void RefreshGlobalBiomeMapIfDirty()
        {
            EnsureNativeStorage();
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

            for (int y = 0; y < BiomeHeatmapResolution; y++)
            {
                int rowOffset = y * BiomeHeatmapResolution;
                for (int x = 0; x < BiomeHeatmapResolution; x++)
                {
                    int index = rowOffset + x;
                    if (H8StaticDataArena.TryGetBiomeHeatmapCell(x, y, out uint biomeHash))
                    {
                        _globalBiomeHashMap[index] = biomeHash;
                        _globalBiomeMap[index] = ResolveBiomeHeatmapByte(biomeHash);
                    }
                    else
                    {
                        _globalBiomeHashMap[index] = 0u;
                        _globalBiomeMap[index] = 0;
                    }
                }
            }

            _debugMapReady = true;
            _lastBlobBytes = residentBytes;
            _lastBlobChecksum = checksum;
        }

        private void ClearGlobalBiomeMap()
        {
            if (!_globalBiomeMap.IsCreated || !_globalBiomeHashMap.IsCreated)
                return;

            for (int i = 0; i < BiomeHeatmapPixelCount; i++)
            {
                _globalBiomeMap[i] = 0;
                _globalBiomeHashMap[i] = 0u;
            }
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform == null)
                return false;

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            return true;
        }

        private bool ResolveLowTierKernel()
        {
            if (forceLowTierKernel || GlobalRegistry.H8_LOW_MEMORY_PROFILE || GlobalRegistry.ScalabilityTierProfileByte == 0)
                return true;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
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
                Frame = (uint)Time.frameCount,
                BiomeA = result.BiomeA,
                BiomeB = result.BiomeB,
                SampleDiameter = result.SampleDiameter,
                Flags = result.Flags
            };

            SignalBus<BiomeGradientSignal>.Push(in signal);
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
            if (!_telemetryRing.IsCreated || _telemetryRing.Length == 0)
                return;

            int index = _telemetryCursor;
            _telemetryRing[index] = new BiomeBoundaryTelemetryEntry
            {
                FrameIndex = Time.frameCount,
                Sequence = _sequence,
                OriginShiftSequence = _lastOriginShiftSequence,
                StateHash = HashState(in playerAup, in result, flags),
                GridX = playerAup.GridX,
                GridZ = playerAup.GridZ,
                LocalX = playerAup.LocalX,
                LocalZ = playerAup.LocalZ,
                BiomeAHash = result.BiomeAHash,
                BiomeBHash = result.BiomeBHash,
                BlendFactor01 = result.BlendFactor01,
                BoundaryDistanceMeters = result.BoundaryDistanceMeters,
                MacroCellX = result.MacroCell.x,
                MacroCellY = result.MacroCell.y,
                Flags = flags,
                SampleDiameter = result.SampleDiameter
            };

            index++;
            _telemetryCursor = index >= _telemetryRing.Length ? 0 : index;
            if (_telemetryCount < _telemetryRing.Length)
                _telemetryCount++;
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length == 0)
                return;

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", BlackBoxDumpPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int capacity = _telemetryRing.Length;
                int count = math.min(_telemetryCount, capacity);
                int start = count == capacity ? _telemetryCursor : 0;
                for (int i = 0; i < count; i++)
                {
                    int entryIndex = start + i;
                    if (entryIndex >= capacity)
                        entryIndex -= capacity;

                    BiomeBoundaryTelemetryEntry entry = _telemetryRing[entryIndex];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.OriginShiftSequence);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.GridX);
                    writer.Write(entry.GridZ);
                    writer.Write(entry.LocalX);
                    writer.Write(entry.LocalZ);
                    writer.Write(entry.BiomeAHash);
                    writer.Write(entry.BiomeBHash);
                    writer.Write(entry.BlendFactor01);
                    writer.Write(entry.BoundaryDistanceMeters);
                    writer.Write(entry.MacroCellX);
                    writer.Write(entry.MacroCellY);
                    writer.Write(entry.Flags);
                    writer.Write(entry.SampleDiameter);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[BiomeBoundarySdfRuntime] Black-box dump failed: " + exception.Message, this);
#endif
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
            H8BiomeRecord* records = (H8BiomeRecord*)H8StaticDataArena.GetSectionDataPointer(
                H8DataSectionId.Biomes,
                H8DataLayoutConstants.BiomeRecordSize,
                out int count);

            if (records == null || count <= 0)
                return false;

            int low = 0;
            int high = count - 1;
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

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BiomeBoundaryTelemetryEntry
        {
            public int FrameIndex;
            public uint Sequence;
            public uint OriginShiftSequence;
            public uint StateHash;
            public long GridX;
            public long GridZ;
            public float LocalX;
            public float LocalZ;
            public uint BiomeAHash;
            public uint BiomeBHash;
            public float BlendFactor01;
            public float BoundaryDistanceMeters;
            public int MacroCellX;
            public int MacroCellY;
            public byte Flags;
            public byte SampleDiameter;
        }
    }
}

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Scheduling;
using Hecton8.Data;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using ResidencySectorDehydratedSignal = Hecton8.Core.Contracts.Signals.SectorDehydratedSignal;
using ResidencySectorHydratedSignal = Hecton8.Core.Contracts.Signals.SectorResidencyHydratedSignal;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Bitmask state for world chunk residency and streaming transitions.
    /// </summary>
    [Flags]
    public enum ChunkState : byte
    {
        Unloaded = 0,
        Resident = 1 << 0,
        Loading = 1 << 1,
        Evicting = 1 << 2,
        Staged = 1 << 3,
        LOD0 = 1 << 4,
        LOD1 = 1 << 5,
        HighPriority = 1 << 6,
        Pinned = 1 << 7
    }

    /// <summary>
    /// Hardware class used for streaming radius and async GPU upload budgets.
    /// </summary>
    public enum ChunkStreamingScalabilityTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Optional chunk-local readiness contract used to delay scatter until base voxel mesh baking finishes.
    /// </summary>
    public interface IChunkVoxelBakeReadiness
    {
        bool IsBaseVoxelMeshReady(long chunkId);
    }

    /// <summary>
    /// Native load request packet consumed by the main-thread Addressables dispatcher.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ChunkLoadRequest
    {
        [FieldOffset(0)]
        public long ChunkId;
        [FieldOffset(8)]
        public float DistanceSq;
        [FieldOffset(12)]
        public uint Frame;
        [FieldOffset(16)]
        public ushort Padding0;
        [FieldOffset(18)]
        public byte Priority;
        [FieldOffset(19)]
        public byte Flags;
        [FieldOffset(20)]
        private uint _pad1;
        [FieldOffset(24)]
        private uint _pad2;
        [FieldOffset(28)]
        private uint _pad3;
    }

    /// <summary>
    /// Vault-owned chunk state/index slot. The slot index is the streaming-table index; DefinitionIndex points back to authoring arrays.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct ChunkStateSlotDTO
    {
        [FieldOffset(0)]
        public long ChunkId;
        [FieldOffset(8)]
        public int DefinitionIndex;
        [FieldOffset(12)]
        public int StorageIndex;
        [FieldOffset(16)]
        public ushort Padding0;
        [FieldOffset(18)]
        public byte State;
        [FieldOffset(19)]
        public byte Occupied;
        [FieldOffset(20)]
        private uint _pad0;
    }

    /// <summary>
    /// Per-slot residency decision emitted by Burst without a persistent growable output writer.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ResidencyDecisionDTO
    {
        [FieldOffset(0)]
        public long ChunkId;
        [FieldOffset(8)]
        public float DistanceSq;
        [FieldOffset(12)]
        public byte Action;
        [FieldOffset(13)]
        public byte Priority;
        [FieldOffset(14)]
        public byte Flags;
        [FieldOffset(15)]
        private byte _pad0;
    }

    /// <summary>
    /// Fixed black-box telemetry sample for the chunk residency system.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ChunkResidencyTelemetryEntry
    {
        private const uint FlagsMask = 0x0000FFFFu;
        private const int ActiveImpostorCountShift = 16;

        [FieldOffset(0)]
        public long FocusChunkId;
        [FieldOffset(8)]
        public long PlayerGridX;
        [FieldOffset(16)]
        public long PlayerGridY;
        [FieldOffset(24)]
        public long PlayerGridZ;
        [FieldOffset(32)]
        public float3 PlayerLocal;
        [FieldOffset(44)]
        public uint Frame;
        [FieldOffset(48)]
        private uint _packedFlags;
        [FieldOffset(52)]
        public uint StateHash;
        [FieldOffset(56)]
        public ushort PendingLoads;
        [FieldOffset(58)]
        public ushort ResidentCount;
        [FieldOffset(60)]
        public ushort LoadingCount;
        [FieldOffset(62)]
        public ushort EvictingCount;

        public uint Flags
        {
            get => _packedFlags & FlagsMask;
            set => _packedFlags = (_packedFlags & ~FlagsMask) | (value & FlagsMask);
        }

        public ushort ActiveImpostorCount
        {
            get => (ushort)(_packedFlags >> ActiveImpostorCountShift);
            set => _packedFlags = (_packedFlags & FlagsMask) | ((uint)value << ActiveImpostorCountShift);
        }
    }

    /// <summary>
    /// Burst job that evaluates chunk residency by comparing the player AUP against chunk-center AUPs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct RadiusBasedStreamingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<long> ChunkIds;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit> ChunkCenters;
        [ReadOnly, NoAlias] public NativeArray<ChunkStateSlotDTO> ChunkStates;
        [NoAlias] public NativeArray<ChunkResidencyDTO> ResidencyDtos;
        [NoAlias] public NativeArray<ResidencyDecisionDTO> Decisions;
        public double3 PlayerAbsolute;
        public float3 PlayerVelocity;
        public double LoadRadiusSq;
        public double UnloadRadiusSq;
        public double PredictiveDistanceMeters;
        public double TailUnloadRadiusSq;
        public byte PredictiveEnabled;

        /// <inheritdoc />
        public void Execute(int index)
        {
            long chunkId = ChunkIds[index];
            ChunkState state = ReadState(index);

            double3 player = PlayerAbsolute;
            double3 chunk = ToAbsoluteDouble3(ChunkCenters[index]);
            double3 delta = chunk - player;
            double distSq = AupPrecisionMath.DistanceSqSafeDouble(chunk, player);
            if (!math.isfinite(distSq))
                distSq = double.MaxValue;

            float distSqFloat = (float)math.min(distSq, float.MaxValue);
            float speedSq = math.lengthsq(PlayerVelocity);
            bool usePrediction = PredictiveEnabled != 0 && PredictiveDistanceMeters > 0d && speedSq > 0.0001f;
            double3 velocityDirection = default;
            if (usePrediction)
            {
                float invSpeed = math.rsqrt(speedSq);
                velocityDirection = new double3(PlayerVelocity.x * invSpeed, PlayerVelocity.y * invSpeed, PlayerVelocity.z * invSpeed);
            }

            bool resident = HasFlag(state, ChunkState.Resident);
            bool loading = HasFlag(state, ChunkState.Loading);
            bool pinned = HasFlag(state, ChunkState.Pinned);
            bool evicting = HasFlag(state, ChunkState.Evicting);

            bool insideLoadZone = distSq <= LoadRadiusSq;
            if (!insideLoadZone && usePrediction)
            {
                double ahead = math.dot(delta, velocityDirection);
                if (ahead > 0d)
                {
                    double clampedAhead = math.min(ahead, PredictiveDistanceMeters);
                    double3 nearestDelta = delta - (velocityDirection * clampedAhead);
                    insideLoadZone = math.lengthsq(nearestDelta) <= LoadRadiusSq;
                }
            }

            if (!resident && !loading && !evicting && insideLoadZone)
            {
                WriteResidencyDto(index, distSqFloat, state, ChunkResidencyStateFlags.HydrationPending, 3);
                WriteDecision(index, chunkId, distSqFloat, 1, 3);
                return;
            }

            double unloadSq = UnloadRadiusSq;
            if (usePrediction && math.dot(delta, velocityDirection) < 0d)
                unloadSq = TailUnloadRadiusSq;

            if (resident && !pinned && !evicting && distSq >= unloadSq)
            {
                WriteResidencyDto(index, distSqFloat, state, ChunkResidencyStateFlags.DehydrationPending, 1);
                WriteDecision(index, chunkId, distSqFloat, 2, 1);
                return;
            }

            WriteResidencyDto(index, distSqFloat, state, 0, 0);
            WriteDecision(index, chunkId, distSqFloat, 0, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChunkState ReadState(int index)
        {
            if (!ChunkStates.IsCreated || (uint)index >= (uint)ChunkStates.Length)
                return ChunkState.Unloaded;

            ChunkStateSlotDTO slot = ChunkStates[index];
            return slot.Occupied != 0 ? (ChunkState)slot.State : ChunkState.Unloaded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDecision(int index, long chunkId, float distanceSq, byte action, byte priority)
        {
            if (!Decisions.IsCreated || (uint)index >= (uint)Decisions.Length)
                return;

            Decisions[index] = new ResidencyDecisionDTO
            {
                ChunkId = chunkId,
                DistanceSq = distanceSq,
                Action = action,
                Priority = priority,
                Flags = 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteResidencyDto(int index, float distanceSq, ChunkState state, byte pendingFlag, byte priority)
        {
            if (!ResidencyDtos.IsCreated || (uint)index >= (uint)ResidencyDtos.Length)
                return;

            ChunkResidencyDTO dto = ResidencyDtos[index];
            byte preserved = (byte)(dto.StateFlags & ChunkResidencyStateFlags.ThreatOverride);
            byte flags = preserved;
            if (HasFlag(state, ChunkState.Resident))
                flags |= ChunkResidencyStateFlags.Hydrated;
            if (HasFlag(state, ChunkState.Loading))
                flags |= ChunkResidencyStateFlags.Loading;
            if (HasFlag(state, ChunkState.Staged))
                flags |= ChunkResidencyStateFlags.Staged;
            if (HasFlag(state, ChunkState.Pinned))
                flags |= ChunkResidencyStateFlags.Pinned;
            if (HasFlag(state, ChunkState.LOD1) || HasFlag(state, ChunkState.Pinned))
                flags |= ChunkResidencyStateFlags.LOD2Impostor;

            flags = (byte)(flags | pendingFlag);
            dto.DistanceSq = distanceSq;
            dto.StateFlags = flags;
            dto.Priority = priority;
            dto._pad0 = 0;
            dto._pad1 = 0u;
            ResidencyDtos[index] = dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(ChunkState state, ChunkState flag)
        {
            return ((byte)state & (byte)flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.Local.x,
                (position.GridY * CellSize) + position.Local.y,
                (position.GridZ * CellSize) + position.Local.z);
        }
    }

    /// <summary>
    /// Burst-native append/remove operation for chunk impostors. Removal uses swap-back to keep the SOA dense.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HlodImpostorSwapJob : IJob
    {
        [NoAlias] public NativeArray<float4x4> ActiveImpostors;
        [NoAlias] public NativeArray<int> ImpostorTypes;
        [NoAlias] public NativeArray<long> ChunkIds;
        [NoAlias] public NativeArray<float> SpawnTimes;
        [NoAlias] public NativeArray<float3> Centers;
        [NoAlias] public NativeArray<float3> Sizes;
        [NoAlias] public NativeArray<uint> Flags;
        [NoAlias] public NativeArray<StreamingHlodImpostorPoint> CartographyPoints;
        [NoAlias] public NativeArray<int> ActiveCount;
        [NoAlias] public NativeArray<int> FadeOutCount;
        public long ChunkId;
        public float3 Center;
        public float3 Size;
        public float SpawnTimeSeconds;
        public int ImpostorType;
        public uint ImpostorFlags;
        public uint FadeOutFlag;
        public byte Operation;

        public void Execute()
        {
            if (!ActiveCount.IsCreated || ActiveCount.Length <= 0)
                return;

            int count = math.clamp(ActiveCount[0], 0, ActiveImpostors.IsCreated ? ActiveImpostors.Length : 0);
            int foundIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (ChunkIds[i] == ChunkId)
                {
                    foundIndex = i;
                    break;
                }
            }

            if (Operation == 0)
            {
                if (foundIndex < 0 && count >= ActiveImpostors.Length)
                    return;

                int writeIndex = foundIndex >= 0 ? foundIndex : count;
                uint previousFlags = foundIndex >= 0 ? Flags[writeIndex] : 0u;
                if ((previousFlags & FadeOutFlag) != 0u && FadeOutCount.IsCreated && FadeOutCount.Length > 0)
                    FadeOutCount[0] = math.max(0, FadeOutCount[0] - 1);

                float3 safeSize = math.max(Size, new float3(1f, 1f, 1f));
                float radius = math.cmax(safeSize) * 0.5f;
                float4x4 matrix = float4x4.identity;
                matrix.c0.x = safeSize.x;
                matrix.c1.y = safeSize.y;
                matrix.c2.z = safeSize.z;
                matrix.c3.x = Center.x;
                matrix.c3.y = Center.y;
                matrix.c3.z = Center.z;
                matrix.c3.w = SpawnTimeSeconds;
                matrix.c0.w = 1f;
                matrix.c1.w = radius;
                matrix.c2.w = math.asfloat(ImpostorFlags);

                ActiveImpostors[writeIndex] = matrix;
                ImpostorTypes[writeIndex] = ImpostorType;
                ChunkIds[writeIndex] = ChunkId;
                SpawnTimes[writeIndex] = SpawnTimeSeconds;
                Centers[writeIndex] = Center;
                Sizes[writeIndex] = safeSize;
                Flags[writeIndex] = ImpostorFlags;
                CartographyPoints[writeIndex] = new StreamingHlodImpostorPoint
                {
                    Center = Center,
                    Size = safeSize,
                    ChunkId = ChunkId,
                    ImpostorType = ImpostorType,
                    SpawnTimeSeconds = SpawnTimeSeconds,
                    Fade01 = 1f,
                    Flags = ImpostorFlags
                };
                if (foundIndex < 0)
                    ActiveCount[0] = count + 1;
                return;
            }

            if (foundIndex < 0)
                return;

            if (Operation == 2)
            {
                uint previousFlags = Flags[foundIndex];
                uint flags = (previousFlags & ~((uint)(1 << 8))) | ImpostorFlags;
                if ((previousFlags & FadeOutFlag) == 0u &&
                    (flags & FadeOutFlag) != 0u &&
                    FadeOutCount.IsCreated &&
                    FadeOutCount.Length > 0)
                {
                    FadeOutCount[0] = math.max(0, FadeOutCount[0]) + 1;
                }

                float4x4 matrix = ActiveImpostors[foundIndex];
                matrix.c0.w = -1f;
                matrix.c2.w = math.asfloat(flags);
                matrix.c3.w = SpawnTimeSeconds;
                ActiveImpostors[foundIndex] = matrix;
                SpawnTimes[foundIndex] = SpawnTimeSeconds;
                Flags[foundIndex] = flags;

                StreamingHlodImpostorPoint point = CartographyPoints[foundIndex];
                point.SpawnTimeSeconds = SpawnTimeSeconds;
                point.Fade01 = 1f;
                point.Flags = flags;
                CartographyPoints[foundIndex] = point;
                return;
            }

            if ((Flags[foundIndex] & FadeOutFlag) != 0u && FadeOutCount.IsCreated && FadeOutCount.Length > 0)
                FadeOutCount[0] = math.max(0, FadeOutCount[0] - 1);

            int lastIndex = count - 1;
            if (foundIndex != lastIndex)
            {
                ActiveImpostors[foundIndex] = ActiveImpostors[lastIndex];
                ImpostorTypes[foundIndex] = ImpostorTypes[lastIndex];
                ChunkIds[foundIndex] = ChunkIds[lastIndex];
                SpawnTimes[foundIndex] = SpawnTimes[lastIndex];
                Centers[foundIndex] = Centers[lastIndex];
                Sizes[foundIndex] = Sizes[lastIndex];
                Flags[foundIndex] = Flags[lastIndex];
                CartographyPoints[foundIndex] = CartographyPoints[lastIndex];
            }

            ActiveImpostors[lastIndex] = default;
            ImpostorTypes[lastIndex] = 0;
            ChunkIds[lastIndex] = 0L;
            SpawnTimes[lastIndex] = 0f;
            Centers[lastIndex] = default;
            Sizes[lastIndex] = default;
            Flags[lastIndex] = 0u;
            CartographyPoints[lastIndex] = default;
            ActiveCount[0] = lastIndex;
        }
    }

    /// <summary>
    /// Removes hydrated impostors after their shader-visible fade window expires.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HlodImpostorFadeCullJob : IJob
    {
        [NoAlias] public NativeArray<float4x4> ActiveImpostors;
        [NoAlias] public NativeArray<int> ImpostorTypes;
        [NoAlias] public NativeArray<long> ChunkIds;
        [NoAlias] public NativeArray<float> SpawnTimes;
        [NoAlias] public NativeArray<float3> Centers;
        [NoAlias] public NativeArray<float3> Sizes;
        [NoAlias] public NativeArray<uint> Flags;
        [NoAlias] public NativeArray<StreamingHlodImpostorPoint> CartographyPoints;
        [NoAlias] public NativeArray<int> ActiveCount;
        [NoAlias] public NativeArray<int> FadeOutCount;
        public float NowSeconds;
        public float FadeOutSeconds;
        public uint FadeOutFlag;

        public void Execute()
        {
            if (!ActiveCount.IsCreated || ActiveCount.Length <= 0 || !ActiveImpostors.IsCreated)
                return;

            int count = math.clamp(ActiveCount[0], 0, ActiveImpostors.Length);
            int remainingFadeOutCount = 0;
            float invFade = math.rcp(math.max(0.001f, FadeOutSeconds));
            for (int i = count - 1; i >= 0; i--)
            {
                uint flags = Flags[i];
                if ((flags & FadeOutFlag) == 0u)
                    continue;

                float fade01 = math.saturate(1f - ((NowSeconds - SpawnTimes[i]) * invFade));
                if (fade01 <= 0f)
                {
                    int lastIndex = count - 1;
                    if (i != lastIndex)
                    {
                        ActiveImpostors[i] = ActiveImpostors[lastIndex];
                        ImpostorTypes[i] = ImpostorTypes[lastIndex];
                        ChunkIds[i] = ChunkIds[lastIndex];
                        SpawnTimes[i] = SpawnTimes[lastIndex];
                        Centers[i] = Centers[lastIndex];
                        Sizes[i] = Sizes[lastIndex];
                        Flags[i] = Flags[lastIndex];
                        CartographyPoints[i] = CartographyPoints[lastIndex];
                    }

                    ActiveImpostors[lastIndex] = default;
                    ImpostorTypes[lastIndex] = 0;
                    ChunkIds[lastIndex] = 0L;
                    SpawnTimes[lastIndex] = 0f;
                    Centers[lastIndex] = default;
                    Sizes[lastIndex] = default;
                    Flags[lastIndex] = 0u;
                    CartographyPoints[lastIndex] = default;
                    count--;
                    continue;
                }

                remainingFadeOutCount++;
                StreamingHlodImpostorPoint point = CartographyPoints[i];
                point.Fade01 = fade01;
                CartographyPoints[i] = point;
            }

            ActiveCount[0] = count;
            if (FadeOutCount.IsCreated && FadeOutCount.Length > 0)
                FadeOutCount[0] = remainingFadeOutCount;
        }
    }

    /// <summary>
    /// Applies a rare AUP origin shift to active impostor matrices and cartography points.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HlodImpostorAupShiftJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float4x4> ActiveImpostors;
        [NoAlias] public NativeArray<float3> Centers;
        [NoAlias] public NativeArray<StreamingHlodImpostorPoint> CartographyPoints;
        public float3 ShiftMeters;

        public void Execute(int index)
        {
            float4x4 matrix = ActiveImpostors[index];
            matrix.c3.x -= ShiftMeters.x;
            matrix.c3.y -= ShiftMeters.y;
            matrix.c3.z -= ShiftMeters.z;
            ActiveImpostors[index] = matrix;

            float3 center = Centers[index] - ShiftMeters;
            Centers[index] = center;
            StreamingHlodImpostorPoint point = CartographyPoints[index];
            point.Center = center;
            CartographyPoints[index] = point;
        }
    }

    /// <summary>
    /// Data-driven residency manager for Addressables-backed world chunks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4140)] // Streaming must register after dispatcher bootstrap and before world content lanes.
    public sealed class WorldChunkResidencyManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IBaseAirlockEventListener, IStreamingBackpressureService, IGlobalRegistryHotSwapListener, IDisposable
    {
        private int _signalPushDropCount;
        private const int DefaultMaxChunkCount = 512;
        private const int DefaultLoadQueueCapacity = 256;
        private const int TelemetryCapacity = 300;
        private const int ResidencyTelemetryEntrySizeBytes = 64;
        private const int MaxActivationsPerFrame = 5;
        private const int MemoryGuardBytes = 500 * 1024 * 1024;
        private const float DefaultLoadRadiusMeters = 500f;
        private const float DefaultUnloadRadiusMeters = 600f;
        private const double DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private const float ChunkFadeSeconds = 2f;
        private const float ChunkFadeSecondsRcp = 0.5f;
        private const float PredictiveLookaheadSeconds = 5f;
        private const float TeleportDistanceMeters = 160f;
        private const int MaxPredictiveBiomePrefabs = 5;
        private const int HabitatTransitionPauseFrames = 180;
        private const int TeleportImmediateLoadDispatchBudget = 4;
        private const int SurvivalLoadDispatchBudget = 1;
        private const int VisualOverkillLoadDispatchBudget = 4;
        private const int AssetLifecycleFarBehindDrainBudget = 8;
        private const int PagerReadTicketCapacity = 16;
        private const int PagerReadRetireBudgetMinimum = 1;
        private const int PagerReadRetireBudgetVisualOverkill = 4;
        private const int MacroDatabaseEvictionScratchCapacity = 128;
        private const int DehydrationMetadataPayloadBytes = 16;
        private const int DefaultHydrationCopyBudgetBytes = 512 * 1024;
        private const int DefaultMaxConcurrentLoads = 4;
        private const int ActiveImpostorAudioMutedFlag = 1 << 8;
        private const int ActiveImpostorPermanentDestroyFlag = 1 << 9;
        private const int ActiveImpostorFadeOutFlag = 1 << 10;
        private const int ActiveImpostorBaseType = 1;
        private const int ActiveImpostorWreckType = 2;
        private const float SurvivalUnloadRadiusMeters = 400f;
        private const float VisualOverkillUnloadRadiusMeters = 1000f;
        private const float ActiveImpostorFadeOutSeconds = 1.5f;

        private static double RuntimeNowSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            return IsFiniteDouble(now) ? now : 0d;
        }
        private const float MacroDatabaseMiddleQualityThreshold = 0.22f;
        private const float MacroDatabaseHighQualityThreshold = 0.58f;
        private const float MacroDatabaseUltraQualityThreshold = 0.86f;
        private const float ChunkResidencyRuntimeClockMaxSeconds = 16777215f;
        private const long PredictiveVramAbortBytes = 1600L * 1024L * 1024L;
        private const long PredictiveVramResumeBytes = 1400L * 1024L * 1024L;
        private const long PredictiveVramMinimumThresholdBytes = 512L * 1024L * 1024L;
        private const long PredictiveVramReservedHeadroomBytes = 256L * 1024L * 1024L;
        private const long PredictiveVramVisualOverkillCeilingBytes = 4096L * 1024L * 1024L;
        private const float PredictiveVramResumeRatio = 0.875f;
        private const float StreamerStressSpeedSqRcp = 0.00111111112f;
        private const float AdrenalinePurgeSeconds = 3.0f;
        private const byte CriticalMemoryPressureSeverity = 2;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1305_WorldChunkResidency.bin";
        private const string BackpressureDumpRelativePath = "Docs/AgentLogs/Dump_1305_WorldChunkResidency_Backpressure.bin";
        private const string HlodDumpRelativePath = "Docs/AgentLogs/Dump_1305_WorldChunkResidency_HLOD.bin";
        private const ulong HectonDumpMagic = 0x00384E4F54434548UL;
        private const uint WorldChunkResidencyDumpVersion = 1u;
        private const int WorldChunkResidencyDumpHeaderBytes = 32;
        private const uint WorldChunkResidencyDumpLayoutHash = 0x44524357u; // WCRD
        private const double LatencyDebtBaselineMs = 80.0;
        private const double CriticalHoleThresholdMs = 250.0;
        private const float StorageDebtEwmaWeight = 0.08f;
        private const float StorageDebtIdleRecoveryWeight = 0.12f;
        private const float StorageDebtPublishBlend = 0.18f;
        private const float StorageDebtPredictionHalveThreshold = 0.25f;
        private const float StorageDebtPredictionResetThreshold = 0.18f;
        private const float StorageDebtTurbulenceThreshold = 0.5f;
        private const float StorageDebtTurbulenceResetThreshold = 0.4f;
        private const float StorageDebtTurbulenceRangeRcp = 10f;
        private const float StorageDebtDataLinkThreshold = 0.6f;
        private const float StorageDebtDataLinkResetThreshold = 0.45f;
        private const float StorageDebtProxyFallbackThreshold = 0.6f;
        private const float StorageDebtProxyFallbackResetThreshold = 0.45f;
        private const byte LoadRequestFlagPredictive = 1 << 0;
        private const byte LoadRequestFlagTeleport = 1 << 1;
        private const uint TelemetryInvalidAupFlag = 1u << 0;
        private const uint TelemetryShiftFlag = 1u << 1;
        private const uint TelemetryMemoryBreachFlag = 1u << 2;
        private const uint TelemetryTeleportFlag = 1u << 3;
        private const uint TelemetryPredictiveSuspendedFlag = 1u << 4;
        private const uint TelemetryPredictivePrewarmFaultFlag = 1u << 5;
        private const uint TelemetryActivationOverflowFlag = 1u << 6;
        private const uint TelemetryDuplicateChunkIdFlag = 1u << 7;
        private const uint TelemetryAdditiveSceneFaultFlag = 1u << 8;
        private const uint TelemetryReleaseAllResetFlag = 1u << 9;
        private const uint TelemetryAddressablesFaultFlag = 1u << 10;
        private const uint TelemetryActivationFaultFlag = 1u << 11;
        private const uint TelemetryHydrationCopySpikeFlag = 1u << 12;
        private const uint MemoryBreachContextHash = 0x43535452u; // "CSTR"
        private const uint LoadRingOverflowWarningHash = 0x43534F56u; // "CSOV"
        private const uint TeleportContextHash = 0x53545250u; // "STRP"
        private const uint SignalPushDropWarningHash = 0x53534452u; // "SSDR" вЂ” signal-push drops
        private const uint StreamingDirectorSourceHash = 0x53333544u; // "S35D"
        private const BufferID ChunkResidencyVaultBufferId = BufferID.PDAEncyclopediaStreamer_UnlockMaskBufferId;
        private const BufferID AddressablesRequestVaultBufferId = BufferID.PDAEncyclopediaStreamer_RuntimeStateBufferId;
        private const BufferID HlodImpostorVaultBufferId = BufferID.PDAEncyclopediaStreamer_MetadataBufferId;
        private const BufferID StreamingTuningVaultBufferId = BufferID.PDAEncyclopediaStreamer_TelemetryBufferId;
        private const BufferID MockAupShiftVaultBufferId = BufferID.PDAEncyclopediaStreamer_TelemetryCursorBufferId;
        private const SystemID VaultOwnerSystem = SystemID.WorldStreaming;
        private const BufferID HydrationApplyRecordVaultBufferId = BufferID.PDAEncyclopediaStreamer_MockUtf8BufferId;
        private const BufferID ChunkIdsVaultBufferId = BufferID.PDAEncyclopediaStreamer_MockIndexBufferId;
        private const BufferID ChunkCentersVaultBufferId = BufferID.WorldChunkResidencyManager_ChunkCentersVaultBufferId;
        private const BufferID ResidencyTelemetryVaultBufferId = BufferID.WorldChunkResidencyManager_ResidencyTelemetryVaultBufferId;
        private const BufferID DehydrationMetadataVaultBufferId = BufferID.PDAEncyclopediaStreamer_TypewriterStateBufferId;
        private const BufferID LoadStartTimesVaultBufferId = BufferID.PDAEncyclopediaStreamer_H8lrMirrorBufferId;
        private const BufferID LoadImmediateRadiusFlagsVaultBufferId = BufferID.WorldChunkResidencyManager_LoadImmediateRadiusFlagsVaultBufferId;
        private const BufferID ActiveImpostorsVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorsVaultBufferId;
        private const BufferID ImpostorTypesVaultBufferId = BufferID.WorldChunkResidencyManager_ImpostorTypesVaultBufferId;
        private const BufferID ActiveImpostorChunkIdsVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorChunkIdsVaultBufferId;
        private const BufferID ActiveImpostorSpawnTimesVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorSpawnTimesVaultBufferId;
        private const BufferID ActiveImpostorCentersVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorCentersVaultBufferId;
        private const BufferID ActiveImpostorSizesVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorSizesVaultBufferId;
        private const BufferID ActiveImpostorFlagsVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorFlagsVaultBufferId;
        private const BufferID ActiveImpostorCartographyVaultBufferId = BufferID.WorldChunkResidencyManager_ActiveImpostorCartographyVaultBufferId;
        private const BufferID ActiveImpostorCountVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_StateBufferId;
        private const BufferID ActiveImpostorFadeOutCountVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_SnapshotBufferId;
        private const BufferID PagerReadTicketsVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_WaypointsBufferId;
        private const BufferID MacroDatabaseEvictionScratchVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_RebaseSignalsBufferId;
        private const BufferID ChunkStateSlotsVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_TuningBufferId;
        private const BufferID LoadRequestsVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_MockVaultBufferId;
        private const BufferID ResidencyDecisionsVaultBufferId = BufferID.Shinobu38QaWatchdogRuntime_TelemetryRingBufferId;
        private const uint ChunkStateHashSeed = 2166136261u;
        private static readonly int _chunkFadeMaskId = Shader.PropertyToID("_ChunkFadeMask");
        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.World.ChunkResidency.Tick");
        private static readonly ProfilerMarker _loadDispatchMarker = new ProfilerMarker("H8.World.ChunkResidency.LoadDispatch");
        private static readonly ProfilerMarker _releaseMarker = new ProfilerMarker("H8.World.ChunkResidency.Release");
        private static readonly double _StopwatchMillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        private enum AdditiveSceneLoadState : byte
        {
            NotNeeded = 0,
            Pending = 1,
            Failed = 2
        }

        [Serializable]
        public struct ChunkDefinition
        {
            [Tooltip("Optional stable label for editor diagnostics. Not used by hot-path code.")]
            public string label;

            [Tooltip("Optional H8BiomeRecord hash from the Data Monolith. Zero falls back to chunk depth lookup.")]
            public uint biomeHash;

            [Tooltip("Absolute chunk center in meters before runtime floating-origin presentation offsets.")]
            public Vector3 absoluteCenterMeters;

            [Tooltip("Chunk size in meters used for the deterministic 64-bit chunk ID.")]
            [Min(1)] public int chunkSizeMeters;

            [Tooltip("Addressables address for the root chunk prefab or payload asset.")]
            public string addressableAddress;

            [Tooltip("Optional additive scene loaded for massive structural chunks.")]
            public string additiveSceneName;

            [Tooltip("True when this chunk should load through SceneManager.LoadSceneAsync(additive).")]
            public bool useAdditiveScene;

            [Tooltip("Never evict this chunk once it becomes resident.")]
            public bool pinned;

            [Tooltip("Prefab dependencies prewarmed into ObjectPoolManager when the chunk data is resident.")]
            public GameObject[] prefabDependencies;

            [Tooltip("Top prefab frequency list emitted from H8BiomeRecord/Data Monolith authoring. First five entries are predictive-prewarmed.")]
            public GameObject[] predictivePrewarmPrefabs;

            [Tooltip("Activation prefabs spawned from ObjectPoolManager in a five-per-frame Awaitable pass.")]
            public GameObject[] activationPrefabs;

            [Tooltip("Pool count per prefab dependency. Zero uses one warm instance per dependency.")]
            [Min(0)] public int warmupCountPerPrefab;

            [Tooltip("Wait for this optional voxel readiness provider before scatter/flora activation.")]
            public MonoBehaviour voxelBakeReadinessProvider;
        }

        [Header("Residency")]
        [Tooltip("Authoring records for streamable chunks. Runtime state is mirrored into NativeCollections.")]
        [SerializeField] private ChunkDefinition[] chunkDefinitions;

        [Tooltip("Hard cap for native chunk storage. Must be >= authored chunk count.")]
        [SerializeField, Min(1)] private int maxChunkCount = DefaultMaxChunkCount;

        [Tooltip("Native load request queue capacity.")]
        [SerializeField, Min(1)] private int loadQueueCapacity = DefaultLoadQueueCapacity;

        [Tooltip("Distance in meters where unloaded chunks are requested.")]
        [SerializeField, Min(1f)] private float loadRadiusMeters = DefaultLoadRadiusMeters;

        [Tooltip("Distance in meters where resident chunks are evicted. Must stay above load radius.")]
        [SerializeField, Min(1f)] private float unloadRadiusMeters = DefaultUnloadRadiusMeters;

        [Tooltip("Optional authored profile defining physical, visual, and data residency radii.")]
        [SerializeField] private WorldChunkStreamingProfile streamingProfile;

        [Tooltip("Hard cap for simultaneous Addressables/additive scene I/O operations.")]
        [SerializeField, Min(1)] private int maxConcurrentLoads = DefaultMaxConcurrentLoads;

        [Tooltip("Multiplier applied to velocity lookahead in predictive residency jobs.")]
        [SerializeField, Min(0f)] private float predictiveVelocityStretch = 1f;

        [Tooltip("Additional unload distance used to prevent load/unload thrash.")]
        [SerializeField, Min(0f)] private float dehydrationHysteresisMeters = 50f;

        [Tooltip("Maximum estimated hydration payload applied per frame.")]
        [SerializeField, Min(1024)] private int hydrationCopyBudgetBytes = DefaultHydrationCopyBudgetBytes;

        [Tooltip("Automatically schedule a residency evaluation after AUP origin-shift signals.")]
        [SerializeField] private bool reactToAupShiftSignals = true;

        [Tooltip("Apply QualitySettings async upload budgets at runtime from continuous GlobalQualityWeight.")]
        [SerializeField] private bool applyAsyncUploadBudget = true;

        [Tooltip("Suspend predictive expansion while habitat or docking systems mark the player as inside dry space.")]
        [SerializeField] private bool suspendPredictiveStreamingInHabitat = true;

        [Header("Diagnostics")]
        [Tooltip("Current number of resident chunks.")]
        [SerializeField] private int _debugResidentChunks;

        [Tooltip("Current number of loading chunks.")]
        [SerializeField] private int _debugLoadingChunks;

        [Tooltip("Current number of evicting chunks.")]
        [SerializeField] private int _debugEvictingChunks;

        [Tooltip("Current native load request count.")]
        [SerializeField] private int _debugPendingLoadRequests;

        [Tooltip("Last observed AUP shift frame id.")]
        [SerializeField] private uint _debugLastAupShiftFrameId;
        private uint _lastAppliedAupShiftFrameId;

        [Tooltip("0..1 pressure metric for Streamer Stress UI. No string formatting in hot path.")]
        [SerializeField, Range(0f, 1f)] private float _debugStreamerStress01;

        [Tooltip("True when predictive loading is currently suspended by VRAM, habitat, or external systems.")]
        [SerializeField] private bool _debugPredictiveSuspended;

        [Tooltip("True when SystemHealthIndex pressure shrinks streaming radii by 40 percent.")]
        [SerializeField] private bool _debugHealthRadiusSqueezed;

        [Tooltip("Last estimated hydration apply time in milliseconds.")]
        [SerializeField] private float _debugLastHydrationApplyMs;

        [Header("HLOD Impostors")]
        [Tooltip("Optional MonoBehaviour implementing IStreamingHlodMatrixRenderer for chunk LOD2 impostor records.")]
        [SerializeField] private MonoBehaviour lod2ImpostorRenderer;

        [Tooltip("Publish chunk LOD2 impostors through native records instead of spawning proxy GameObjects.")]
        [SerializeField] private bool enableLod2Impostors = true;

        [Tooltip("Distance in meters where chunk impostor records become active. Real geometry remains inside this radius.")]
        [SerializeField, Min(1f)] private float impostorLod2DistanceMeters = HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters;

        [Tooltip("Last published LOD2 impostor count.")]
        [SerializeField] private int _debugActiveImpostorLod2Chunks;

        private VaultGenerationHandle<ChunkStateSlotDTO> _chunkStateSlotsHandle;
        private VaultGenerationHandle<ChunkLoadRequest> _loadRequestsHandle;
        private VaultGenerationHandle<ResidencyDecisionDTO> _residencyDecisionsHandle;
        private JobHandle _residencyJobHandle;
        private long _residencyScheduleTimestamp;
        private bool _residencyJobScheduled;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredAirlockEvents;
        private bool _registeredBackpressureService;
        private bool _registeredHotSwap;

        /// <summary>
        /// Registry-backed runtime instance. Null if not initialized.
        /// </summary>
        private static WorldChunkResidencyManager s_activeRuntime;

        /// <summary>
        /// Active resolve-or-create owner for GlobalRegistry.StreamingBackpressure.
        /// </summary>
        public static WorldChunkResidencyManager Instance => s_activeRuntime;
        private IAsyncPersistenceService _asyncPersistenceService;
        private IDataVault _dataVault;
        private IJobAdmissionService _jobAdmissionService;
        private IMacroDatabaseService _macroDatabaseService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private Hecton8.Optimization.AssetLifecycleGovernor _assetLifecycleGovernor;
        private IVramBudgetReadModel _vramMonitor;
        private IVramPressureReadModel _vramPressure;
        private IObjectPoolService _objectPoolManager;
        private bool _disposed;
        private bool _forceResidencyEvaluation;
        private bool _fadeActive;
        private bool _hasLastPlayerAup;
        private bool _externalPredictiveSuspended;
        private bool _habitatPredictivePauseActive;
        private bool _transportPredictivePauseActive;
        private bool _predictiveVramAborted;
        private bool _streamingVaultBacked;
        private bool _healthRadiusSqueezeActive;
        private bool _teleportResetPending;
        private bool _adrenalinePoolTrimPending;
        private float _adrenalinePurgeUntilTime;
        private float _systemHealthPressure01;
        private float _lastHydrationApplyMs;
        private uint _lastAdrenalineSignalFrame;
        private bool _stateDiagnosticsDirty;
        private float _chunkResidencyRuntimeSeconds;
        private float _fadeTimer;
        private float _pendingChunkFadeMask;
        private bool _chunkFadeMaskDirty;
        private long _predictiveVramCeilingBytes;
        private int _streamingLedgerCapacity;
        private float _lastPredictionDistanceMeters;
        private float _loadQueueCapacityRcp;
        private float _maxChunkCountRcp;
        private float3 _lastPlayerVelocity;
        private AbsoluteUniversePositionBlit _lastPlayerAup;
        private AbsoluteUniversePositionBlit _lastProjectedAup;
        private AbsoluteUniversePositionBlit _pendingTeleportAup;
        private AbsoluteUniversePosition _lastTeleportProbeAup;
        private int _chunkCount;
        private int _pendingLoadRequestCount;
        private int _loadRequestReadIndex;
        private int _loadRequestWriteIndex;
        private int _loadDispatchFrame = -1;
        private float _loadDispatchBudgetTokens;
        private int _pagerReadTicketCount;
        private int _pagerReadRetiredReadyCount;
        private int _pagerReadRetiredFallbackCount;
        private uint _pagerReadRequestSequence;
        private int _pendingAdditiveSceneOperationCount;
        private int _activeImpostorCount;
        private int _activeImpostorFadeOutCount;
        private int _telemetryCursor;
        private int _habitatTransitionPauseFrames;
        private double _latencyEwmaMs;
        private double _oldestPendingMs;
        private double _criticalHoleDebtMs;
        private float _storageDebt01;
        private float _smoothedStorageDebt01;
        private uint _storageDebtSequence;
        private uint _activeImpostorVersion;
        private uint _activeImpostorPointVersion;
        private uint _publishedActiveImpostorVersion;
        private uint _hydrationApplySequence;
        private bool _predictionConstrainedByStorageDebt;
        private bool _turbulenceActiveByStorageDebt;
        private bool _proxyFallbackByStorageDebt;
        private bool _dataLinkDegradedByStorageDebt;
        private bool _dataLinkDegradedNotificationPublished;
        private bool _activeImpostorGpuDirty = true;
        private uint _debugStateHash = ChunkStateHashSeed;
        private int _activeAsyncUploadBudgetHash = int.MinValue;
        private WorldStreamingRuntimeTuning _coldStartTuning;
        private IAmbientBiotaService _ambientBiotaService;
        private IDataVault _streamingLedgerVault;
        private VaultGenerationHandle<ChunkResidencyDTO> _chunkResidencyDtoHandle;
        private VaultGenerationHandle<AddressablesRequestDTO> _addressablesRequestDtoHandle;
        private VaultGenerationHandle<HLOD_ImpostorDTO> _hlodImpostorDtoHandle;
        private VaultGenerationHandle<WorldStreamingRuntimeTuning> _streamingTuningHandle;
        private VaultGenerationHandle<MockAupShiftSignal> _mockAupShiftHandle;
        private VaultGenerationHandle<long> _chunkIdsHandle;
        private VaultGenerationHandle<AbsoluteUniversePositionBlit> _chunkCentersHandle;
        private VaultGenerationHandle<ChunkResidencyTelemetryEntry> _residencyTelemetryHandle;
        private VaultGenerationHandle<double> _loadStartTimesHandle;
        private VaultGenerationHandle<byte> _loadImmediateRadiusFlagsHandle;
        private VaultGenerationHandle<float4x4> _activeImpostorsHandle;
        private VaultGenerationHandle<int> _impostorTypesHandle;
        private VaultGenerationHandle<long> _activeImpostorChunkIdsHandle;
        private VaultGenerationHandle<float> _activeImpostorSpawnTimesHandle;
        private VaultGenerationHandle<float3> _activeImpostorCentersHandle;
        private VaultGenerationHandle<float3> _activeImpostorSizesHandle;
        private VaultGenerationHandle<uint> _activeImpostorFlagsHandle;
        private VaultGenerationHandle<StreamingHlodImpostorPoint> _activeImpostorCartographyPointsHandle;
        private VaultGenerationHandle<int> _activeImpostorCountHandle;
        private VaultGenerationHandle<int> _activeImpostorFadeOutCountHandle;
        private VaultGenerationHandle<H8WorldPageReadTicket> _pagerReadTicketsHandle;
        private VaultGenerationHandle<ulong> _macroDatabaseEvictionScratchHandle;
        private VaultGenerationHandle<ChunkHydrationApplyRecord> _hydrationApplyRecordsHandle;
        private VaultGenerationHandle<byte> _dehydrationMetadataPayloadHandle;
        private int _chunkIdsSentinelId;
        private int _chunkCentersSentinelId;
        private int _chunkStateSlotsSentinelId;
        private int _loadRequestsSentinelId;
        private int _residencyDecisionsSentinelId;
        private int _residencyTelemetrySentinelId;
        private int _loadStartTimesSentinelId;
        private int _loadImmediateRadiusFlagsSentinelId;
        private int _activeImpostorsSentinelId;
        private int _impostorTypesSentinelId;
        private int _activeImpostorChunkIdsSentinelId;
        private int _activeImpostorSpawnTimesSentinelId;
        private int _activeImpostorCentersSentinelId;
        private int _activeImpostorSizesSentinelId;
        private int _activeImpostorFlagsSentinelId;
        private int _activeImpostorCartographyPointsSentinelId;
        private int _activeImpostorCountSentinelId;
        private int _activeImpostorFadeOutCountSentinelId;
        private int _pagerReadTicketsSentinelId;
        private int _macroDatabaseEvictionScratchSentinelId;
        private int _hydrationApplyRecordsSentinelId;
        private int _dehydrationMetadataPayloadSentinelId;
        private long[] _chunkIdsByDefinitionIndex;
        private GameObject[][] _spawnedInstancesByChunk;
        private int[] _spawnedCountsByChunk;
        private bool[] _activationInProgress;
        private int[] _activationVersions;
        private bool[] _predictivePrewarmInProgress;
        private bool[] _predictivePrewarmComplete;
        private int[] _predictivePrewarmVersions;
        private AsyncOperation[] _additiveSceneOperations;
        private bool[] _additiveSceneActivationRequested;
        private bool[] _additiveSceneLoaded;
        private bool[] _additiveSceneUnloadWhenLoaded;
        private bool[] _loadRequestQueuedByChunk;
        private bool[] _evictRequestQueuedByChunk;
        private long[] _deferredEvictChunkIds;
        private int _deferredEvictCount;
#if UNITY_ADDRESSABLES_EXIST
        private int _pendingAddressableLoadCount;
        private int _pendingAddressableCacheClearCount;
        private AsyncOperationHandle<GameObject>[] _addressableHandles;
        private bool[] _hasAddressableHandle;
        private bool[] _addressableLoadPending;
        private AsyncOperationHandle<bool>[] _addressableCacheClearHandles;
        private bool[] _hasAddressableCacheClearHandle;
#endif

        /// <summary>
        /// Number of authored chunks mirrored into native residency state.
        /// </summary>
        public int ChunkCount => _chunkCount;

        /// <summary>
        /// Streamer pressure metric exposed for lightweight UI binding.
        /// </summary>
        public float StreamerStress01 => _debugStreamerStress01;

        /// <summary>
        /// True while speculative prediction is disabled by VRAM, habitat, or external docking code.
        /// </summary>
        public bool IsPredictiveStreamingSuspended => PredictiveStreamingPausedNow;

        public float StorageDebt01 => _storageDebt01;

        public float SmoothedStorageDebt01 => _smoothedStorageDebt01;

        public double LatencyEwmaMs => _latencyEwmaMs;

        public double OldestPendingMs => _oldestPendingMs;

        public double CriticalHoleDebtMs => _criticalHoleDebtMs;

        public uint BackpressureSequence => _storageDebtSequence;

        public bool DataLinkDegraded => _dataLinkDegradedByStorageDebt;

        public int ActiveImpostorCount => _activeImpostorCount;

        public uint ActiveImpostorVersion => _activeImpostorPointVersion;

        public bool TryGetActiveImpostors(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly impostorTypes, out int count)
        {
            count = _activeImpostorCount;
            bool hasMatrices = TryResolveWorldStreamingVaultBuffer(in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<float4x4> activeImpostors);
            bool hasTypes = TryResolveWorldStreamingVaultBuffer(in _impostorTypesHandle, ImpostorTypesVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<int> resolvedImpostorTypes);
            matrices = hasMatrices ? activeImpostors.AsReadOnly() : default;
            impostorTypes = hasTypes ? resolvedImpostorTypes.AsReadOnly() : default;
            return matrices.Length > 0 && impostorTypes.Length > 0 && count > 0;
        }

        public bool TryGetActiveImpostorPoints(out NativeArray<StreamingHlodImpostorPoint>.ReadOnly points, out int count)
        {
            count = _activeImpostorCount;
            points = TryResolveWorldStreamingVaultBuffer(in _activeImpostorCartographyPointsHandle, ActiveImpostorCartographyVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<StreamingHlodImpostorPoint> cartographyPoints)
                ? cartographyPoints.AsReadOnly()
                : default;
            return points.Length > 0 && count > 0;
        }

        public bool TryGetChunkResidencyDtos(out NativeArray<ChunkResidencyDTO>.ReadOnly chunks, out int count)
        {
            NativeArray<ChunkResidencyDTO> resolved = ResolveChunkResidencyDtos();
            chunks = resolved.IsCreated ? resolved.AsReadOnly() : default;
            count = _chunkCount;
            return !_residencyJobScheduled && chunks.IsCreated && count > 0;
        }

        public WorldStreamingRuntimeTuning ReadRuntimeTuning()
        {
            NativeArray<WorldStreamingRuntimeTuning> tuning = ResolveStreamingTuning();
            if (tuning.IsCreated && tuning.Length > 0)
                return tuning[0];

            return _coldStartTuning.PhysicalHydrationRadiusMeters > 0f
                ? _coldStartTuning
                : WorldStreamingRuntimeTuning.CreateDefault();
        }

        public void ApplyRuntimeTuning(in WorldStreamingRuntimeTuning tuning)
        {
            WorldStreamingRuntimeTuning safe = ClampRuntimeTuning(tuning);
            _coldStartTuning = safe;
            NativeArray<WorldStreamingRuntimeTuning> tuningBuffer = ResolveStreamingTuning();
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                tuningBuffer[0] = safe;

            predictiveVelocityStretch = safe.PredictiveVelocityStretch;
            dehydrationHysteresisMeters = safe.DehydrationHysteresisMeters;
            hydrationCopyBudgetBytes = safe.HydrationCopyBudgetBytes;
            maxConcurrentLoads = safe.MaxConcurrentLoads;
            loadRadiusMeters = safe.LoadRadiusMeters;
            unloadRadiusMeters = safe.UnloadRadiusMeters;
            impostorLod2DistanceMeters = safe.VisualResidencyRadiusMeters;
            ClampSettings();
            _forceResidencyEvaluation = true;
        }

#if UNITY_EDITOR
        public bool TryApplyStreamingProfileCsvText(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                return false;

            WorldStreamingRuntimeTuning tuning = ReadRuntimeTuning();
            if (!WorldStreamingProfileCsvParser.TryParse(csv.AsSpan(), ref tuning))
                return false;

            ApplyRuntimeTuning(in tuning);
            return true;
        }
#endif

        private NativeArray<ChunkResidencyDTO> ResolveChunkResidencyDtos()
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _chunkResidencyDtoHandle,
                ChunkResidencyVaultBufferId,
                ResolveStreamingLedgerCapacity(),
                out NativeArray<ChunkResidencyDTO> buffer)
                ? buffer
                : default;
        }

        private NativeArray<AddressablesRequestDTO> ResolveAddressablesRequestDtos()
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _addressablesRequestDtoHandle,
                AddressablesRequestVaultBufferId,
                ResolveStreamingLedgerCapacity(),
                out NativeArray<AddressablesRequestDTO> buffer)
                ? buffer
                : default;
        }

        private NativeArray<HLOD_ImpostorDTO> ResolveHlodImpostorDtos()
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _hlodImpostorDtoHandle,
                HlodImpostorVaultBufferId,
                ResolveStreamingLedgerCapacity(),
                out NativeArray<HLOD_ImpostorDTO> buffer)
                ? buffer
                : default;
        }

        private NativeArray<WorldStreamingRuntimeTuning> ResolveStreamingTuning()
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _streamingTuningHandle,
                StreamingTuningVaultBufferId,
                1,
                out NativeArray<WorldStreamingRuntimeTuning> buffer)
                ? buffer
                : default;
        }

        private NativeArray<ChunkResidencyTelemetryEntry> ResolveResidencyTelemetryRing()
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _residencyTelemetryHandle,
                ResidencyTelemetryVaultBufferId,
                TelemetryCapacity,
                out NativeArray<ChunkResidencyTelemetryEntry> buffer)
                ? buffer
                : default;
        }

        private bool TryResolveLoadTimingBuffers(out NativeArray<double> loadStartTimes, out NativeArray<byte> immediateRadiusFlags)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (!TryResolveWorldStreamingVaultBuffer(
                    in _loadStartTimesHandle,
                    LoadStartTimesVaultBufferId,
                    capacity,
                    out loadStartTimes))
            {
                immediateRadiusFlags = default;
                return false;
            }

            if (TryResolveWorldStreamingVaultBuffer(
                    in _loadImmediateRadiusFlagsHandle,
                    LoadImmediateRadiusFlagsVaultBufferId,
                    capacity,
                    out immediateRadiusFlags))
            {
                return true;
            }

            loadStartTimes = default;
            return false;
        }

        private bool TryResolveActiveImpostorBuffers(
            out NativeArray<float4x4> activeImpostors,
            out NativeArray<int> impostorTypes,
            out NativeArray<long> chunkIds,
            out NativeArray<float> spawnTimes,
            out NativeArray<float3> centers,
            out NativeArray<float3> sizes,
            out NativeArray<uint> flags,
            out NativeArray<StreamingHlodImpostorPoint> cartographyPoints,
            out NativeArray<int> activeCount,
            out NativeArray<int> fadeOutCount)
        {
            return TryResolveActiveImpostorBuffers(
                _dataVault,
                out activeImpostors,
                out impostorTypes,
                out chunkIds,
                out spawnTimes,
                out centers,
                out sizes,
                out flags,
                out cartographyPoints,
                out activeCount,
                out fadeOutCount);
        }

        private bool TryResolveActiveImpostorBuffers(
            IDataVault vault,
            out NativeArray<float4x4> activeImpostors,
            out NativeArray<int> impostorTypes,
            out NativeArray<long> chunkIds,
            out NativeArray<float> spawnTimes,
            out NativeArray<float3> centers,
            out NativeArray<float3> sizes,
            out NativeArray<uint> flags,
            out NativeArray<StreamingHlodImpostorPoint> cartographyPoints,
            out NativeArray<int> activeCount,
            out NativeArray<int> fadeOutCount)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, capacity, out activeImpostors) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _impostorTypesHandle, ImpostorTypesVaultBufferId, capacity, out impostorTypes) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorChunkIdsHandle, ActiveImpostorChunkIdsVaultBufferId, capacity, out chunkIds) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorSpawnTimesHandle, ActiveImpostorSpawnTimesVaultBufferId, capacity, out spawnTimes) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorCentersHandle, ActiveImpostorCentersVaultBufferId, capacity, out centers) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorSizesHandle, ActiveImpostorSizesVaultBufferId, capacity, out sizes) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorFlagsHandle, ActiveImpostorFlagsVaultBufferId, capacity, out flags) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorCartographyPointsHandle, ActiveImpostorCartographyVaultBufferId, capacity, out cartographyPoints) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorCountHandle, ActiveImpostorCountVaultBufferId, 1, out activeCount) &&
                TryResolveWorldStreamingVaultBuffer(vault, in _activeImpostorFadeOutCountHandle, ActiveImpostorFadeOutCountVaultBufferId, 1, out fadeOutCount))
            {
                return true;
            }

            activeImpostors = default;
            impostorTypes = default;
            chunkIds = default;
            spawnTimes = default;
            centers = default;
            sizes = default;
            flags = default;
            cartographyPoints = default;
            activeCount = default;
            fadeOutCount = default;
            return false;
        }

        private bool TryResolveChunkTableBuffers(out NativeArray<long> chunkIds, out NativeArray<AbsoluteUniversePositionBlit> chunkCenters)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (TryResolveWorldStreamingVaultBuffer(in _chunkIdsHandle, ChunkIdsVaultBufferId, capacity, out chunkIds) &&
                TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, capacity, out chunkCenters))
            {
                return true;
            }

            chunkIds = default;
            chunkCenters = default;
            return false;
        }

        private bool TryResolveResidencyStateBuffers(
            out NativeArray<ChunkStateSlotDTO> stateSlots,
            out NativeArray<ResidencyDecisionDTO> decisions)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (TryResolveWorldStreamingVaultBuffer(in _chunkStateSlotsHandle, ChunkStateSlotsVaultBufferId, capacity, out stateSlots) &&
                TryResolveWorldStreamingVaultBuffer(in _residencyDecisionsHandle, ResidencyDecisionsVaultBufferId, capacity, out decisions))
            {
                return true;
            }

            stateSlots = default;
            decisions = default;
            return false;
        }

        private bool TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> stateSlots)
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _chunkStateSlotsHandle,
                ChunkStateSlotsVaultBufferId,
                ResolveStreamingLedgerCapacity(),
                out stateSlots);
        }

        private bool TryResolveLoadRequestQueue(out NativeArray<ChunkLoadRequest> loadRequests)
        {
            return TryResolveWorldStreamingVaultBuffer(
                in _loadRequestsHandle,
                LoadRequestsVaultBufferId,
                ResolveLoadRequestQueueCapacity(),
                out loadRequests);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveLoadRequestQueueCapacity()
        {
            return math.max(1, loadQueueCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveStreamingLedgerCapacity()
        {
            return _streamingLedgerCapacity > 0 ? _streamingLedgerCapacity : math.max(1, maxChunkCount);
        }

        public bool IsChunkImpostorAudioMuted(long chunkId)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (chunkId == 0L ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorChunkIdsHandle, ActiveImpostorChunkIdsVaultBufferId, capacity, out NativeArray<long> activeImpostorChunkIds) ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorFlagsHandle, ActiveImpostorFlagsVaultBufferId, capacity, out NativeArray<uint> activeImpostorFlags) ||
                _activeImpostorCount <= 0)
            {
                return false;
            }

            int count = math.min(_activeImpostorCount, activeImpostorChunkIds.Length);
            for (int i = 0; i < count; i++)
            {
                if (activeImpostorChunkIds[i] == chunkId)
                    return (activeImpostorFlags[i] & ActiveImpostorAudioMutedFlag) != 0u;
            }

            return false;
        }

        /// <summary>
        /// External docking/habitat code can suspend speculative streaming without taking a concrete dependency on this manager.
        /// </summary>
        public void SetPredictiveStreamingSuspended(bool suspended)
        {
            _externalPredictiveSuspended = suspended;
            _forceResidencyEvaluation = true;
        }

        /// <summary>
        /// Returns whether predictive/speculative chunk loading is allowed right now.
        /// </summary>
        public bool ShouldLoadSpeculative()
        {
            return !PredictiveStreamingPausedNow && !_predictiveVramAborted;
        }

        /// <summary>
        /// Computes the deterministic 64-bit chunk ID from an Absolute Universe Position.
        /// </summary>
        /// <param name="position">Chunk center AUP.</param>
        /// <param name="chunkSizeMeters">Chunk size in meters.</param>
        /// <returns>Non-negative 64-bit chunk identifier.</returns>
        public static long BuildChunkId(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            int safeChunkSize = math.max(1, chunkSizeMeters);
            int3 chunk = AbsoluteUniversePosition.ResolveChunkId(in position, safeChunkSize);
            ulong hash = 1469598103934665603UL;
            hash = MixHash(hash, (uint)chunk.x);
            hash = MixHash(hash, (uint)chunk.y);
            hash = MixHash(hash, (uint)chunk.z);
            hash = MixHash(hash, (uint)safeChunkSize);
            return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
        }

        /// <summary>
        /// Returns true when the chunk is currently resident.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        public bool IsChunkResident(long chunkId)
        {
            return IsResident(chunkId);
        }

        /// <summary>
        /// Returns true when the chunk is currently resident.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        public bool IsResident(long chunkId)
        {
            if (_residencyJobScheduled)
                return false;

            return TryGetChunkState(chunkId, out ChunkState state) &&
                   HasFlag(state, ChunkState.Resident);
        }

        private bool TryGetChunkDefinitionIndex(long chunkId, out int definitionIndex)
        {
            definitionIndex = -1;
            if (!TryFindChunkStateSlot(chunkId, out _, out ChunkStateSlotDTO slot))
                return false;

            definitionIndex = slot.DefinitionIndex;
            return definitionIndex >= 0;
        }

        private bool TryGetChunkStorageIndex(long chunkId, out int storageIndex)
        {
            storageIndex = -1;
            if (!TryFindChunkStateSlot(chunkId, out _, out ChunkStateSlotDTO slot))
                return false;

            storageIndex = slot.StorageIndex;
            return storageIndex >= 0;
        }

        private bool TryGetChunkState(long chunkId, out ChunkState state)
        {
            state = ChunkState.Unloaded;
            if (!TryFindChunkStateSlot(chunkId, out _, out ChunkStateSlotDTO slot))
                return false;

            state = (ChunkState)slot.State;
            return true;
        }

        private bool TryGetChunkStateAtStorageIndex(int storageIndex, out ChunkState state)
        {
            state = ChunkState.Unloaded;
            if (!TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> slots) ||
                (uint)storageIndex >= (uint)slots.Length)
            {
                return false;
            }

            ChunkStateSlotDTO slot = slots[storageIndex];
            if (slot.Occupied == 0)
                return false;

            state = (ChunkState)slot.State;
            return true;
        }

        private bool TryFindChunkStateSlot(long chunkId, out int slotIndex, out ChunkStateSlotDTO slot)
        {
            slotIndex = -1;
            slot = default;
            if (chunkId == 0L || !TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> slots))
                return false;

            int count = math.min(_chunkCount, slots.Length);
            for (int i = 0; i < count; i++)
            {
                ChunkStateSlotDTO candidate = slots[i];
                if (candidate.Occupied != 0 && candidate.ChunkId == chunkId)
                {
                    slotIndex = i;
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.StreamingBackpressure owner for player builds.
        /// WorldRuntimeInstaller deliberately skips this owner (OnEnable registration ordering risk).
        /// </summary>
        public static WorldChunkResidencyManager EnsureRuntimeInstance()
        {
            WorldChunkResidencyManager registered = GlobalRegistry.StreamingBackpressure as WorldChunkResidencyManager;
            if (IsWorldChunkResidencyRuntimeUsable(registered))
                return registered;

            WorldChunkResidencyManager active = s_activeRuntime;
            if (IsWorldChunkResidencyRuntimeUsable(active))
                return active;

            // StreamingBackpressureRuntime is NOT a scene hot-swap slot. After LockReady, eviction +
            // reconstruct leaves the slot empty and the replacement's TryRegister throws
            // CriticalBootException (ready-lock). Prefer any still-alive owner over replacement.
            // Mirrors GCMonitor.EnsureRuntimeOwnership: ask IsRuntimeServicePublicationOpen FIRST.
            bool publicationOpen = GlobalRegistry.IsRuntimeServicePublicationOpen<IStreamingBackpressureService>();
            if (!publicationOpen)
            {
                if (!ReferenceEquals(registered, null) && registered != null)
                    return registered;
                if (!ReferenceEquals(active, null) && active != null)
                    return active;
                return null;
            }

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterStreamingBackpressureRuntime(registered);
                if (registered != null)
                    registered._registeredBackpressureService = false;
            }

            if (!ReferenceEquals(active, null) && active != null && !ReferenceEquals(active, registered))
            {
                if (active._registeredBackpressureService)
                {
                    GlobalRegistry.UnregisterStreamingBackpressureRuntime(active);
                    active._registeredBackpressureService = false;
                }
                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;
            }
            else if (!ReferenceEquals(active, null) && active == null)
            {
                s_activeRuntime = null;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Sole StreamingBackpressure owner; WorldRuntimeInstaller deliberately skips construction.
            GameObject runtimeRoot = new GameObject("[WorldChunkResidencyManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<WorldChunkResidencyManager>();
        }

        private static bool IsWorldChunkResidencyRuntimeUsable(WorldChunkResidencyManager manager)
        {
            return manager != null && manager._registeredBackpressureService && manager.isActiveAndEnabled;
        }

        private void Awake()
        {
            _chunkResidencyRuntimeSeconds = 0f;
            RefreshColdServiceCache();
            _coldStartTuning = ResolveInitialStreamingTuning();
            ApplyColdStartTuningToFields(in _coldStartTuning);
            ClampSettings();
            AllocateNativeState();
            AllocateManagedState();
            BuildChunkTables();
            FlushAsyncUploadBudgetPolicySlow();
        }

        private void OnEnable()
        {
            if (!ReferenceEquals(s_activeRuntime, null) && !ReferenceEquals(s_activeRuntime, this) && s_activeRuntime != null)
            {
                enabled = false;
                return;
            }

            s_activeRuntime = this;
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            TryUnregister();
            TryUnregisterHotSwap();
            CompleteResidencyJobForTeardown();
            ReleaseAllChunks();
            ClearColdServiceCache();
        }


        private void OnDestroy()
        {
            DisposeInternal(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeInternal(true);
        }

        private void DisposeInternal(bool releaseChunks)
        {
            if (_disposed)
                return;

            _disposed = true;
            TryUnregister();
            TryUnregisterHotSwap();
            CompleteResidencyJobForTeardown();

            if (releaseChunks)
                ReleaseAllChunks();

            DisposeNativeState();
            ClearColdServiceCache();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                AdvanceChunkResidencyRuntimeClock(deltaTime);
                TickPredictiveSuspension();
                DetectAndHandleTeleport();
                CompleteResidencyJobIfFinished();
                ProcessResidencyResults();
                UpdateChunkFade(deltaTime);
                UpdateStreamerStressMetric();
                WriteTelemetrySample(0L, 0u);
                if (!_residencyJobScheduled)
                    ScheduleForcedResidencyEvaluation();
            }
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            FlushAsyncUploadBudgetPolicySlow();
            ReportSignalPushDrops();

            if (_chunkCount <= 0)
                return;

            if (_residencyJobScheduled)
                return;

            EvaluateAndPublishStorageBackpressure();
            EvictDistantMacroDatabaseBreadcrumbs();
            ScheduleResidencyJob();
        }

        /// <summary>
        /// Drains the signal-push drop counter and reports it, so a dropped signal stops being invisible.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>_signalPushDropCount</c> is passed by ref to seven <c>TryPushTracked</c> calls in this class вЂ”
        /// StorageDebt, StreamingTurbulence, HUDNotification and two lambda-form pushes among them вЂ” and until
        /// this method existed it was ONLY ever incremented. Never read, never reset, never surfaced. A bus
        /// lane that silently drops is precisely the silent-degeneracy shape this project's rules single out:
        /// the system keeps producing plausible output while quietly losing work, and nothing fails.
        /// </para>
        /// <para>
        /// The HUDNotification lane makes it concrete. Its consumer drains at most
        /// <c>MaxHudNotificationSignalsPerLateFrame</c> per frame, so a burst of chunk-residency warnings
        /// overflows by design вЂ” and the overflow was the player simply not being told something the game had
        /// decided to tell them, with no trace anywhere.
        /// </para>
        /// <para>
        /// Idiom copied from <c>RadiationHazardGrid.ConsumeSignalDropFlags</c>, which does the same
        /// <see cref="Interlocked.Exchange"/> drain-and-test and is covered by
        /// <c>RadiationHazardGridSignalDropTelemetryEditTests</c> вЂ” so this is the sanctioned shape in this
        /// codebase rather than an invention. <c>Interlocked</c> rather than a plain read because
        /// <c>TryPushTracked</c> is called from lambda-form pushes that may run off the owner thread, and a
        /// lost increment here would defeat the point of counting at all.
        /// </para>
        /// <para>
        /// Reported through <see cref="GlobalTelemetryBus.PublishPerformanceWarning"/>, the channel this class
        /// already uses for load-ring overflow and teleport pressure, so it lands where an operator is already
        /// looking. Cadence is SlowTick rather than Tick deliberately: a drop count is a rate, not an event,
        /// and sampling it every frame would spam the warning lane it is trying to make legible.
        /// </para>
        /// </remarks>
        private void ReportSignalPushDrops()
        {
            int dropped = Interlocked.Exchange(ref _signalPushDropCount, 0);
            if (dropped <= 0)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                SignalPushDropWarningHash,
                MemoryBreachContextHash,
                dropped);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteResidencyJobIfFinished();
            RetireAsyncPagerReadTickets(ResolvePagerReadRetireBudget());
            DrainAupShiftSignals();
            DrainHlodSwapSignals();
            PollAddressableLoads();
            PollAddressableCacheClears();
            DrainMetabolismSignals();
            TryApplyPendingTeleportReset();
            if (!_residencyJobScheduled)
            {
                ProcessDeferredEvictions();
                ProcessLoadDispatchBudget();
            }
            TryActivateReadySubScenes();
            FlushAdrenalinePoolTrim();
            CullExpiredHlodFadeouts();
            FlushQueuedChunkFadeMask();
            PublishLod2ImpostorResidency();
        }

        private void PublishLod2ImpostorResidency()
        {
            IStreamingHlodMatrixRenderer renderer = ResolveLod2ImpostorRenderer();
            if (!enableLod2Impostors ||
                renderer == null ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<float4x4> activeImpostors) ||
                _activeImpostorCount <= 0)
            {
                if (renderer != null)
                    renderer.ClearBinding();

                _debugActiveImpostorLod2Chunks = 0;
                _publishedActiveImpostorVersion = 0u;
                return;
            }

            bool forceUpload = _activeImpostorGpuDirty ||
                               _publishedActiveImpostorVersion != _activeImpostorVersion ||
                               renderer.BoundInstanceCount != _activeImpostorCount ||
                               (_activeImpostorFadeOutCount > 0 && !renderer.IsUsingVisibleMatrixStream);
            renderer.BindNativeMatrices(activeImpostors, _activeImpostorCount, ResolveActiveImpostorDefaultRadius(), forceUpload);
            _publishedActiveImpostorVersion = _activeImpostorVersion;
            _activeImpostorGpuDirty = false;
            _debugActiveImpostorLod2Chunks = _activeImpostorCount;
        }

        private IStreamingHlodMatrixRenderer ResolveLod2ImpostorRenderer()
        {
            return lod2ImpostorRenderer as IStreamingHlodMatrixRenderer;
        }

        /// <summary>
        /// Queues a load request for a chunk without creating duplicate loading work.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        /// <param name="priority">Priority byte. Higher value means more urgent for this queue.</param>
        public void RequestLoad(long chunkId, byte priority)
        {
            RequestLoad(chunkId, priority, 0, 0f);
        }

        private void RequestLoad(long chunkId, byte priority, byte flags, float distanceSq)
        {
            if (!TryResolveLoadRequestQueue(out NativeArray<ChunkLoadRequest> loadRequests))
                return;

            if (!TryGetChunkDefinitionIndex(chunkId, out int index))
                return;

            if (_loadRequestQueuedByChunk != null && _loadRequestQueuedByChunk[index])
                return;

            int queueCapacity = math.min(ResolveLoadRequestQueueCapacity(), loadRequests.Length);
            if (_pendingLoadRequestCount >= queueCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(LoadRingOverflowWarningHash, MemoryBreachContextHash, _pendingLoadRequestCount);
                return;
            }

            bool markedLoading = false;
            ChunkState requestState = ChunkState.Unloaded;
            if (!_residencyJobScheduled)
            {
                if (!TryGetChunkState(chunkId, out requestState))
                    return;

                if (HasFlag(requestState, ChunkState.Resident) || HasFlag(requestState, ChunkState.Loading) || HasFlag(requestState, ChunkState.Evicting))
                    return;

                requestState |= ChunkState.Loading;
                SetChunkState(chunkId, requestState);
                markedLoading = true;
            }
            else
            {
                _forceResidencyEvaluation = true;
            }

            ChunkLoadRequest request = new ChunkLoadRequest
            {
                ChunkId = chunkId,
                DistanceSq = distanceSq,
                Priority = priority,
                Flags = flags,
                Padding0 = 0,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };
            if (!TryEnqueueLoadRequest(loadRequests, in request))
            {
                if (markedLoading)
                {
                    requestState &= unchecked((ChunkState)~(byte)ChunkState.Loading);
                    SetChunkState(chunkId, requestState);
                }

                return;
            }

            if (_loadRequestQueuedByChunk != null)
                _loadRequestQueuedByChunk[index] = true;

            _pendingLoadRequestCount++;
            _debugPendingLoadRequests = _pendingLoadRequestCount;
        }

        private bool TryEnqueueLoadRequest(NativeArray<ChunkLoadRequest> loadRequests, in ChunkLoadRequest request)
        {
            int capacity = math.min(ResolveLoadRequestQueueCapacity(), loadRequests.Length);
            if (!loadRequests.IsCreated || capacity <= 0 || _pendingLoadRequestCount >= capacity)
                return false;

            if ((uint)_loadRequestWriteIndex >= (uint)capacity)
                _loadRequestWriteIndex = 0;

            loadRequests[_loadRequestWriteIndex] = request;
            _loadRequestWriteIndex++;
            if (_loadRequestWriteIndex >= capacity)
                _loadRequestWriteIndex = 0;
            return true;
        }

        private void RequestAsyncPagerRead(long chunkId)
        {
            IAsyncPersistenceService persistence = _asyncPersistenceService;
            if (!IsAsyncPersistenceUsable(persistence))
                return;

            if (!TryResolveWorldStreamingVaultBuffer(
                    in _pagerReadTicketsHandle,
                    PagerReadTicketsVaultBufferId,
                    PagerReadTicketCapacity,
                    out NativeArray<H8WorldPageReadTicket> pagerReadTickets))
                return;

            if (_pagerReadTicketCount >= PagerReadTicketCapacity)
            {
                RetireAsyncPagerReadTickets(PagerReadTicketCapacity);
                if (_pagerReadTicketCount >= PagerReadTicketCapacity)
                    return;
            }

            uint requestId = AdvancePagerReadRequestId();

            if (persistence.TryRequestChunkPageRead(chunkId, H8WorldPagePayloadTypes.VoxelDeltaRle, requestId, out H8WorldPageReadTicket ticket))
                pagerReadTickets[_pagerReadTicketCount++] = ticket;
        }

        private uint AdvancePagerReadRequestId()
        {
            uint next = unchecked(_pagerReadRequestSequence + 1u);
            if (next == 0u)
                next = 1u;

            _pagerReadRequestSequence = next;
            return next;
        }

        private static int ResolvePagerReadRetireBudget()
        {
            return math.clamp(
                (int)math.ceil(math.lerp(PagerReadRetireBudgetMinimum, PagerReadRetireBudgetVisualOverkill, ResolveSmoothGlobalQualityWeight01())),
                PagerReadRetireBudgetMinimum,
                PagerReadRetireBudgetVisualOverkill);
        }

        private void RetireAsyncPagerReadTickets(int budget)
        {
            if (budget <= 0 ||
                _pagerReadTicketCount <= 0 ||
                !TryResolveWorldStreamingVaultBuffer(
                    in _pagerReadTicketsHandle,
                    PagerReadTicketsVaultBufferId,
                    PagerReadTicketCapacity,
                    out NativeArray<H8WorldPageReadTicket> pagerReadTickets))
                return;

            IAsyncPersistenceService persistence = _asyncPersistenceService;
            if (!IsAsyncPersistenceUsable(persistence))
                return;

            int retired = 0;
            int index = 0;
            while (index < _pagerReadTicketCount && retired < budget)
            {
                H8WorldPageReadTicket ticket = pagerReadTickets[index];
                if (!persistence.TryRetireCompletedChunkPage(in ticket, out H8WorldPageStatus status, out int byteCount))
                {
                    index++;
                    continue;
                }

                if (status == H8WorldPageStatus.Ready && byteCount > 0)
                    _pagerReadRetiredReadyCount++;
                else
                    _pagerReadRetiredFallbackCount++;

                int last = _pagerReadTicketCount - 1;
                pagerReadTickets[index] = pagerReadTickets[last];
                pagerReadTickets[last] = default;
                _pagerReadTicketCount = last;
                retired++;
            }
        }

        /// <summary>
        /// Evicts a resident chunk and releases tracked Addressables handles.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        public void RequestEvict(long chunkId)
        {
            RequestEvict(chunkId, ShouldClearAddressableCacheOnEvict(chunkId));
        }

        private void RequestEvict(long chunkId, bool clearAddressableCache)
        {
            if (!TryGetChunkDefinitionIndex(chunkId, out int index))
                return;

            QueueDeferredEviction(index, chunkId);
            _forceResidencyEvaluation = true;
        }

        private void EvictChunkNow(int index, long chunkId, bool clearAddressableCache)
        {
            if (!TryGetChunkState(chunkId, out ChunkState state))
                return;

            if (HasFlag(state, ChunkState.Pinned))
                return;

            bool threatOverride = ShouldRetainThreatResidency(index);
            if (threatOverride && !TryEnqueueDehydrationMetadata(chunkId, state))
            {
                state |= ChunkState.Pinned | ChunkState.LOD1;
                SetChunkState(chunkId, state);
                return;
            }

            state |= ChunkState.Evicting;
            state &= unchecked((ChunkState)~(byte)ChunkState.Resident);
            SetChunkState(chunkId, state);
            PublishSectorDehydratedSignal(index, chunkId, state);

            DespawnChunkInstances(index);
            ReleaseChunkHandles(index, clearAddressableCache);
            if (threatOverride)
            {
                state = ChunkState.Pinned | ChunkState.LOD1;
                SetChunkState(chunkId, state);
                _forceResidencyEvaluation = true;
                return;
            }

            state = ChunkState.Unloaded;
            SetChunkState(chunkId, state);
            _forceResidencyEvaluation = true;
        }

        private void ClampSettings()
        {
            maxChunkCount = math.max(1, maxChunkCount);
            int authoredCount = chunkDefinitions != null ? chunkDefinitions.Length : 0;
            maxChunkCount = math.max(maxChunkCount, authoredCount);
            loadQueueCapacity = math.max(1, loadQueueCapacity);
            maxConcurrentLoads = math.clamp(maxConcurrentLoads, 1, 16);
            predictiveVelocityStretch = math.max(0f, predictiveVelocityStretch);
            dehydrationHysteresisMeters = math.max(0f, dehydrationHysteresisMeters);
            hydrationCopyBudgetBytes = math.max(1024, hydrationCopyBudgetBytes);
            loadRadiusMeters = math.max(1f, loadRadiusMeters);
            unloadRadiusMeters = math.max(loadRadiusMeters + math.max(1f, dehydrationHysteresisMeters), unloadRadiusMeters);
            impostorLod2DistanceMeters = math.max(1f, impostorLod2DistanceMeters);
            _loadQueueCapacityRcp = math.rcp((float)loadQueueCapacity);
            _maxChunkCountRcp = math.rcp((float)maxChunkCount);
        }

        private WorldStreamingRuntimeTuning ResolveInitialStreamingTuning()
        {
            string projectRoot = ResolveProjectRoot();
            WorldStreamingRuntimeTuning tuning = WorldStreamingLegacyProfileArchaeology.ScanOrEmergency(projectRoot);
            if (streamingProfile != null)
            {
                tuning.PhysicalHydrationRadiusMeters = math.max(1f, streamingProfile.fullSimulationRadius);
                tuning.Lod1RadiusMeters = math.max(tuning.PhysicalHydrationRadiusMeters, streamingProfile.midSimulationRadius);
                tuning.VisualResidencyRadiusMeters = math.max(tuning.Lod1RadiusMeters, streamingProfile.visualResidencyRadius);
                tuning.DataResidencyRadiusMeters = math.max(tuning.VisualResidencyRadiusMeters, streamingProfile.dataResidencyRadius);
                tuning.LoadRadiusMeters = tuning.PhysicalHydrationRadiusMeters;
                tuning.UnloadRadiusMeters = math.max(tuning.LoadRadiusMeters + tuning.DehydrationHysteresisMeters, tuning.LoadRadiusMeters + 1f);
                WorldChunkStreamingProfile.LayerProfile layer = streamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.LargeThreats);
                if (!layer.useChunkResidency)
                    tuning.Flags |= 2;
            }

            return ClampRuntimeTuning(tuning);
        }

        private static string ResolveProjectRoot()
        {
            try
            {
                string dataPath = Application.dataPath;
                if (string.IsNullOrEmpty(dataPath))
                    return string.Empty;

                return Path.GetFullPath(Path.Combine(dataPath, ".."));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void ApplyColdStartTuningToFields(in WorldStreamingRuntimeTuning tuning)
        {
            predictiveVelocityStretch = tuning.PredictiveVelocityStretch;
            dehydrationHysteresisMeters = tuning.DehydrationHysteresisMeters;
            hydrationCopyBudgetBytes = tuning.HydrationCopyBudgetBytes;
            maxConcurrentLoads = tuning.MaxConcurrentLoads;
            loadRadiusMeters = tuning.LoadRadiusMeters;
            unloadRadiusMeters = tuning.UnloadRadiusMeters;
            impostorLod2DistanceMeters = tuning.VisualResidencyRadiusMeters;
        }

        private static WorldStreamingRuntimeTuning ClampRuntimeTuning(in WorldStreamingRuntimeTuning tuning)
        {
            WorldStreamingRuntimeTuning safe = tuning;
            safe.PredictiveVelocityStretch = math.clamp(safe.PredictiveVelocityStretch, 0f, 10f);
            safe.PhysicalHydrationRadiusMeters = math.max(1f, safe.PhysicalHydrationRadiusMeters);
            safe.Lod1RadiusMeters = math.max(safe.PhysicalHydrationRadiusMeters, safe.Lod1RadiusMeters);
            safe.VisualResidencyRadiusMeters = math.max(safe.Lod1RadiusMeters, safe.VisualResidencyRadiusMeters);
            safe.DataResidencyRadiusMeters = math.max(safe.VisualResidencyRadiusMeters, safe.DataResidencyRadiusMeters);
            safe.DehydrationHysteresisMeters = math.max(0f, safe.DehydrationHysteresisMeters);
            safe.MaxConcurrentLoads = math.clamp(safe.MaxConcurrentLoads, 1, 16);
            safe.HydrationCopyBudgetBytes = math.max(1024, safe.HydrationCopyBudgetBytes);
            safe.LoadRadiusMeters = math.max(1f, safe.LoadRadiusMeters);
            safe.UnloadRadiusMeters = math.max(safe.LoadRadiusMeters + math.max(1f, safe.DehydrationHysteresisMeters), safe.UnloadRadiusMeters);
            safe._pad0 = 0;
            safe._pad1 = 0;
            safe._pad2 = 0;
            return safe;
        }

        private void AllocateNativeState()
        {
            int capacity = math.max(1, maxChunkCount);
            _streamingLedgerCapacity = capacity;
            // COLD VAULT: chunk id and AUP center SoA for Burst residency scans.
            // COLD VAULT: ChunkStateSlotDTO[maxChunkCount], ChunkLoadRequest[loadQueueCapacity], ResidencyDecisionDTO[maxChunkCount].
            // COLD VAULT: ChunkResidencyTelemetryEntry[300] - black-box circular telemetry.
            EnsureStreamingLedgerBuffers(capacity);
            // COLD VAULT: double[maxChunkCount] and byte[maxChunkCount] - Addressables IO timing and immediate-radius flags.
            // COLD VAULT: HLOD impostor SoA and counters.
            try
            {
            if (TryResolveChunkTableBuffers(out NativeArray<long> chunkIds, out NativeArray<AbsoluteUniversePositionBlit> chunkCenters))
            {
                RegisterStreamingLedgerArray(chunkIds, nameof(_chunkIdsHandle), out _chunkIdsSentinelId);
                RegisterStreamingLedgerArray(chunkCenters, nameof(_chunkCentersHandle), out _chunkCentersSentinelId);
            }
            if (TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> chunkStateSlots))
                RegisterStreamingLedgerArray(chunkStateSlots, nameof(_chunkStateSlotsHandle), out _chunkStateSlotsSentinelId);
            if (TryResolveLoadRequestQueue(out NativeArray<ChunkLoadRequest> loadRequests))
                RegisterStreamingLedgerArray(loadRequests, nameof(_loadRequestsHandle), out _loadRequestsSentinelId);
            if (TryResolveWorldStreamingVaultBuffer(
                    in _residencyDecisionsHandle,
                    ResidencyDecisionsVaultBufferId,
                    capacity,
                    out NativeArray<ResidencyDecisionDTO> residencyDecisions))
            {
                RegisterStreamingLedgerArray(residencyDecisions, nameof(_residencyDecisionsHandle), out _residencyDecisionsSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _residencyTelemetryHandle,
                    ResidencyTelemetryVaultBufferId,
                    TelemetryCapacity,
                    out NativeArray<ChunkResidencyTelemetryEntry> residencyTelemetry))
            {
                RegisterStreamingLedgerArray(residencyTelemetry, nameof(_residencyTelemetryHandle), out _residencyTelemetrySentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _loadStartTimesHandle,
                    LoadStartTimesVaultBufferId,
                    capacity,
                    out NativeArray<double> loadStartTimes))
            {
                RegisterStreamingLedgerArray(loadStartTimes, nameof(_loadStartTimesHandle), out _loadStartTimesSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _loadImmediateRadiusFlagsHandle,
                    LoadImmediateRadiusFlagsVaultBufferId,
                    capacity,
                    out NativeArray<byte> loadImmediateRadiusFlags))
            {
                RegisterStreamingLedgerArray(loadImmediateRadiusFlags, nameof(_loadImmediateRadiusFlagsHandle), out _loadImmediateRadiusFlagsSentinelId);
            }
            IDataVault vault = _streamingLedgerVault ?? _dataVault;
            if (TryResolveActiveImpostorBuffers(
                    vault,
                    out NativeArray<float4x4> activeImpostors,
                    out NativeArray<int> impostorTypes,
                    out NativeArray<long> activeImpostorChunkIds,
                    out NativeArray<float> activeImpostorSpawnTimes,
                    out NativeArray<float3> activeImpostorCenters,
                    out NativeArray<float3> activeImpostorSizes,
                    out NativeArray<uint> activeImpostorFlags,
                    out NativeArray<StreamingHlodImpostorPoint> activeImpostorCartographyPoints,
                    out NativeArray<int> activeImpostorCount,
                    out NativeArray<int> activeImpostorFadeOutCount))
            {
                RegisterStreamingLedgerArray(activeImpostors, nameof(_activeImpostorsHandle), out _activeImpostorsSentinelId);
                RegisterStreamingLedgerArray(impostorTypes, nameof(_impostorTypesHandle), out _impostorTypesSentinelId);
                RegisterStreamingLedgerArray(activeImpostorChunkIds, nameof(_activeImpostorChunkIdsHandle), out _activeImpostorChunkIdsSentinelId);
                RegisterStreamingLedgerArray(activeImpostorSpawnTimes, nameof(_activeImpostorSpawnTimesHandle), out _activeImpostorSpawnTimesSentinelId);
                RegisterStreamingLedgerArray(activeImpostorCenters, nameof(_activeImpostorCentersHandle), out _activeImpostorCentersSentinelId);
                RegisterStreamingLedgerArray(activeImpostorSizes, nameof(_activeImpostorSizesHandle), out _activeImpostorSizesSentinelId);
                RegisterStreamingLedgerArray(activeImpostorFlags, nameof(_activeImpostorFlagsHandle), out _activeImpostorFlagsSentinelId);
                RegisterStreamingLedgerArray(activeImpostorCartographyPoints, nameof(_activeImpostorCartographyPointsHandle), out _activeImpostorCartographyPointsSentinelId);
                RegisterStreamingLedgerArray(activeImpostorCount, nameof(_activeImpostorCountHandle), out _activeImpostorCountSentinelId);
                RegisterStreamingLedgerArray(activeImpostorFadeOutCount, nameof(_activeImpostorFadeOutCountHandle), out _activeImpostorFadeOutCountSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _pagerReadTicketsHandle,
                    PagerReadTicketsVaultBufferId,
                    PagerReadTicketCapacity,
                    out NativeArray<H8WorldPageReadTicket> pagerReadTickets))
            {
                RegisterStreamingLedgerArray(pagerReadTickets, nameof(_pagerReadTicketsHandle), out _pagerReadTicketsSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _macroDatabaseEvictionScratchHandle,
                    MacroDatabaseEvictionScratchVaultBufferId,
                    MacroDatabaseEvictionScratchCapacity,
                    out NativeArray<ulong> macroDatabaseEvictionScratch))
            {
                RegisterStreamingLedgerArray(macroDatabaseEvictionScratch, nameof(_macroDatabaseEvictionScratchHandle), out _macroDatabaseEvictionScratchSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _hydrationApplyRecordsHandle,
                    HydrationApplyRecordVaultBufferId,
                    capacity,
                    out NativeArray<ChunkHydrationApplyRecord> hydrationApplyRecords))
            {
                RegisterStreamingLedgerArray(hydrationApplyRecords, nameof(_hydrationApplyRecordsHandle), out _hydrationApplyRecordsSentinelId);
            }
            if (TryResolveWorldStreamingVaultBuffer(
                    in _dehydrationMetadataPayloadHandle,
                    DehydrationMetadataVaultBufferId,
                    DehydrationMetadataPayloadBytes,
                    out NativeArray<byte> dehydrationMetadataPayload))
            {
                RegisterStreamingLedgerArray(dehydrationMetadataPayload, nameof(_dehydrationMetadataPayloadHandle), out _dehydrationMetadataPayloadSentinelId);
            }

            NativeArray<ChunkResidencyDTO> residencyDtos = ResolveChunkResidencyDtos();
            if (residencyDtos.IsCreated)
            {
                ChunkResidencyDtoInitJob initJob = new ChunkResidencyDtoInitJob { Chunks = residencyDtos };
                for (int i = 0; i < residencyDtos.Length; i++)
                    initJob.Execute(i);
            }

            NativeArray<WorldStreamingRuntimeTuning> tuning = ResolveStreamingTuning();
            if (tuning.IsCreated && tuning.Length > 0)
                tuning[0] = _coldStartTuning;
            }
            catch
            {
                ReleaseStreamingLedgerBuffers();
                throw;
            }
        }

        private static void RegisterStreamingLedgerArray<T>(
            NativeArray<T> array,
            string label,
            out int sentinelId) where T : struct
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeArray(
                array,
                nameof(WorldChunkResidencyManager),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"NativeMemorySentinel rejected world chunk residency ledger registration for {label}.");
        }

        private void EnsureStreamingLedgerBuffers(int capacity)
        {
            IDataVault vault = _dataVault;
            _streamingVaultBacked = false;
            if (vault == null || capacity <= 0)
            {
                ReleaseStreamingLedgerBuffers();
                return;
            }

            if (_streamingLedgerVault != null && !ReferenceEquals(_streamingLedgerVault, vault))
                ReleaseStreamingLedgerBuffers(_streamingLedgerVault);

            _streamingLedgerCapacity = capacity;
            _streamingLedgerVault = vault;
            if (EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _chunkIdsHandle,
                    ChunkIdsVaultBufferId,
                    capacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<long> chunkIds,
                    ref _chunkIdsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _chunkCentersHandle,
                    ChunkCentersVaultBufferId,
                    capacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<AbsoluteUniversePositionBlit> chunkCenters,
                    ref _chunkCentersSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _chunkStateSlotsHandle,
                    ChunkStateSlotsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ChunkStateSlotDTO> chunkStateSlots,
                    ref _chunkStateSlotsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _loadRequestsHandle,
                    LoadRequestsVaultBufferId,
                    ResolveLoadRequestQueueCapacity(),
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ChunkLoadRequest> loadRequests,
                    ref _loadRequestsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _residencyDecisionsHandle,
                    ResidencyDecisionsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ResidencyDecisionDTO> residencyDecisions,
                    ref _residencyDecisionsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _chunkResidencyDtoHandle,
                    ChunkResidencyVaultBufferId,
                    capacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ChunkResidencyDTO> residencyDtos) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _addressablesRequestDtoHandle,
                    AddressablesRequestVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<AddressablesRequestDTO> addressableRequests) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _hlodImpostorDtoHandle,
                    HlodImpostorVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<HLOD_ImpostorDTO> hlodImpostors) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _streamingTuningHandle,
                    StreamingTuningVaultBufferId,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<WorldStreamingRuntimeTuning> tuning) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _mockAupShiftHandle,
                    MockAupShiftVaultBufferId,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<MockAupShiftSignal> mockAupShift) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _residencyTelemetryHandle,
                    ResidencyTelemetryVaultBufferId,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ChunkResidencyTelemetryEntry> residencyTelemetry,
                    ref _residencyTelemetrySentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _loadStartTimesHandle,
                    LoadStartTimesVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<double> loadStartTimes,
                    ref _loadStartTimesSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _loadImmediateRadiusFlagsHandle,
                    LoadImmediateRadiusFlagsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> loadImmediateRadiusFlags,
                    ref _loadImmediateRadiusFlagsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorsHandle,
                    ActiveImpostorsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float4x4> activeImpostors,
                    ref _activeImpostorsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _impostorTypesHandle,
                    ImpostorTypesVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> impostorTypes,
                    ref _impostorTypesSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorChunkIdsHandle,
                    ActiveImpostorChunkIdsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<long> activeImpostorChunkIds,
                    ref _activeImpostorChunkIdsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorSpawnTimesHandle,
                    ActiveImpostorSpawnTimesVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float> activeImpostorSpawnTimes,
                    ref _activeImpostorSpawnTimesSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorCentersHandle,
                    ActiveImpostorCentersVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float3> activeImpostorCenters,
                    ref _activeImpostorCentersSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorSizesHandle,
                    ActiveImpostorSizesVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float3> activeImpostorSizes,
                    ref _activeImpostorSizesSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorFlagsHandle,
                    ActiveImpostorFlagsVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> activeImpostorFlags,
                    ref _activeImpostorFlagsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorCartographyPointsHandle,
                    ActiveImpostorCartographyVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<StreamingHlodImpostorPoint> activeImpostorCartographyPoints,
                    ref _activeImpostorCartographyPointsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorCountHandle,
                    ActiveImpostorCountVaultBufferId,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> activeImpostorCount,
                    ref _activeImpostorCountSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _activeImpostorFadeOutCountHandle,
                    ActiveImpostorFadeOutCountVaultBufferId,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> activeImpostorFadeOutCount,
                    ref _activeImpostorFadeOutCountSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _pagerReadTicketsHandle,
                    PagerReadTicketsVaultBufferId,
                    PagerReadTicketCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<H8WorldPageReadTicket> pagerReadTickets,
                    ref _pagerReadTicketsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _macroDatabaseEvictionScratchHandle,
                    MacroDatabaseEvictionScratchVaultBufferId,
                    MacroDatabaseEvictionScratchCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ulong> macroDatabaseEvictionScratch,
                    ref _macroDatabaseEvictionScratchSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _hydrationApplyRecordsHandle,
                    HydrationApplyRecordVaultBufferId,
                    capacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ChunkHydrationApplyRecord> hydrationApplyRecords,
                    ref _hydrationApplyRecordsSentinelId) &&
                EnsureWorldStreamingVaultBuffer(
                    vault,
                    ref _dehydrationMetadataPayloadHandle,
                    DehydrationMetadataVaultBufferId,
                    DehydrationMetadataPayloadBytes,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> dehydrationMetadataPayload,
                    ref _dehydrationMetadataPayloadSentinelId))
            {
                _streamingVaultBacked =
                    chunkIds.IsCreated &&
                    chunkCenters.IsCreated &&
                    chunkStateSlots.IsCreated &&
                    loadRequests.IsCreated &&
                    residencyDecisions.IsCreated &&
                    residencyDtos.IsCreated &&
                    addressableRequests.IsCreated &&
                    hlodImpostors.IsCreated &&
                    tuning.IsCreated &&
                    mockAupShift.IsCreated &&
                    residencyTelemetry.IsCreated &&
                    loadStartTimes.IsCreated &&
                    loadImmediateRadiusFlags.IsCreated &&
                    activeImpostors.IsCreated &&
                    impostorTypes.IsCreated &&
                    activeImpostorChunkIds.IsCreated &&
                    activeImpostorSpawnTimes.IsCreated &&
                    activeImpostorCenters.IsCreated &&
                    activeImpostorSizes.IsCreated &&
                    activeImpostorFlags.IsCreated &&
                    activeImpostorCartographyPoints.IsCreated &&
                    activeImpostorCount.IsCreated &&
                    activeImpostorFadeOutCount.IsCreated &&
                    pagerReadTickets.IsCreated &&
                    macroDatabaseEvictionScratch.IsCreated &&
                    hydrationApplyRecords.IsCreated &&
                    dehydrationMetadataPayload.IsCreated;
            }
        }

        private void BuildChunkTables()
        {
            _chunkCount = 0;
            if (chunkDefinitions == null)
                return;

            if (!TryResolveChunkTableBuffers(out NativeArray<long> chunkIds, out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                !TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> stateSlots))
                return;

            NativeArray<ChunkResidencyDTO> residencyDtos = ResolveChunkResidencyDtos();
            NativeArray<HLOD_ImpostorDTO> hlodImpostors = ResolveHlodImpostorDtos();
            int clearCount = math.min(stateSlots.Length, math.min(chunkIds.Length, chunkCenters.Length));
            for (int i = 0; i < clearCount; i++)
            {
                chunkIds[i] = 0L;
                chunkCenters[i] = default;
                stateSlots[i] = default;
            }

            for (int i = 0; i < chunkDefinitions.Length && i < maxChunkCount; i++)
            {
                ChunkDefinition definition = chunkDefinitions[i];
                AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    definition.absoluteCenterMeters.x,
                    definition.absoluteCenterMeters.y,
                    definition.absoluteCenterMeters.z));
                if (!centerAup.IsFinite())
                {
                    DumpTelemetry(TelemetryInvalidAupFlag);
                    continue;
                }

                int chunkSize = definition.chunkSizeMeters > 0 ? definition.chunkSizeMeters : Mathf.RoundToInt(math.max(1f, unloadRadiusMeters));
                long chunkId = BuildChunkId(in centerAup, chunkSize);
                if (TryFindChunkStateSlot(chunkId, out _, out _))
                {
                    WriteTelemetrySample(chunkId, TelemetryDuplicateChunkIdFlag);
                    continue;
                }

                int storageIndex = _chunkCount;
                chunkIds[storageIndex] = chunkId;
                chunkCenters[storageIndex] = AbsoluteUniversePositionBlit.FromAup(in centerAup);
                _chunkIdsByDefinitionIndex[i] = chunkId;
                ChunkState initialState = definition.pinned ? ChunkState.Pinned : ChunkState.Unloaded;
                stateSlots[storageIndex] = new ChunkStateSlotDTO
                {
                    ChunkId = chunkId,
                    DefinitionIndex = i,
                    StorageIndex = storageIndex,
                    Padding0 = 0,
                    State = unchecked((byte)initialState),
                    Occupied = 1
                };
                if (residencyDtos.IsCreated && (uint)storageIndex < (uint)residencyDtos.Length)
                {
                    residencyDtos[storageIndex] = new ChunkResidencyDTO
                    {
                        AUP_Center = ToAbsoluteDouble3(in centerAup),
                        SectorHash = ComputeSectorHash(chunkId),
                        DistanceSq = float.MaxValue,
                        StateFlags = ConvertChunkStateToResidencyFlags(initialState, i),
                        Priority = 0,
                        _pad0 = 0,
                        _pad1 = 0u
                    };
                }

                if (hlodImpostors.IsCreated && (uint)storageIndex < (uint)hlodImpostors.Length)
                {
                    hlodImpostors[storageIndex] = new HLOD_ImpostorDTO
                    {
                        SectorHash = ComputeSectorHash(chunkId),
                        CenterXZ = new float2(centerAup.LocalX, centerAup.LocalZ),
                        RadiusMetersQ = (ushort)math.clamp(chunkSize, 1, ushort.MaxValue),
                        ImpostorType = (byte)(definition.useAdditiveScene ? ActiveImpostorWreckType : ActiveImpostorBaseType),
                        Flags = 0
                    };
                }

                _chunkCount++;
            }

            _stateDiagnosticsDirty = true;
        }

        private void AllocateManagedState()
        {
            int count = math.max(1, chunkDefinitions != null ? chunkDefinitions.Length : 0);
            // COLD ALLOC: long[chunkDefinitions] - definition index to deterministic chunk id map - owner: WorldChunkResidencyManager
            _chunkIdsByDefinitionIndex = new long[count];
            // COLD ALLOC: GameObject[][][chunkDefinitions] - spawned instance tracking for chunk unload - owner: WorldChunkResidencyManager
            _spawnedInstancesByChunk = new GameObject[count][];
            // COLD ALLOC: int[chunkDefinitions] - spawned count tracking for chunk unload - owner: WorldChunkResidencyManager
            _spawnedCountsByChunk = new int[count];
            // COLD ALLOC: bool[chunkDefinitions] - activation Awaitable ownership guard - owner: WorldChunkResidencyManager
            _activationInProgress = new bool[count];
            // COLD ALLOC: int[chunkDefinitions] - activation generation guard for unload/reload races - owner: WorldChunkResidencyManager
            _activationVersions = new int[count];
            // COLD ALLOC: bool[chunkDefinitions] - predictive pool prewarm Awaitable ownership guard - owner: WorldChunkResidencyManager
            _predictivePrewarmInProgress = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - predictive pool prewarm completion guard - owner: WorldChunkResidencyManager
            _predictivePrewarmComplete = new bool[count];
            // COLD ALLOC: int[chunkDefinitions] - predictive prewarm generation guard for unload/reload races - owner: WorldChunkResidencyManager
            _predictivePrewarmVersions = new int[count];
            // COLD ALLOC: AsyncOperation[chunkDefinitions] - additive scene load handles - owner: WorldChunkResidencyManager
            _additiveSceneOperations = new AsyncOperation[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene activation gate state - owner: WorldChunkResidencyManager
            _additiveSceneActivationRequested = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene loaded state - owner: WorldChunkResidencyManager
            _additiveSceneLoaded = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene deferred unload state - owner: WorldChunkResidencyManager
            _additiveSceneUnloadWhenLoaded = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - explicit/deferred load request duplicate guard - owner: WorldChunkResidencyManager
            _loadRequestQueuedByChunk = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - explicit/deferred evict request duplicate guard - owner: WorldChunkResidencyManager
            _evictRequestQueuedByChunk = new bool[count];
            // COLD ALLOC: long[loadQueueCapacity] - deferred evict ids while residency job owns state reads - owner: WorldChunkResidencyManager
            _deferredEvictChunkIds = new long[math.max(1, loadQueueCapacity)];
#if UNITY_ADDRESSABLES_EXIST
            // COLD ALLOC: AsyncOperationHandle<GameObject>[chunkDefinitions] - explicit Addressables release tracking - owner: WorldChunkResidencyManager
            _addressableHandles = new AsyncOperationHandle<GameObject>[count];
            // COLD ALLOC: bool[chunkDefinitions] - valid Addressables handle occupancy map - owner: WorldChunkResidencyManager
            _hasAddressableHandle = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - Addressables completion poll occupancy map - owner: WorldChunkResidencyManager
            _addressableLoadPending = new bool[count];
            // COLD ALLOC: AsyncOperationHandle<bool>[chunkDefinitions] - explicit Addressables cache-clear handles - owner: WorldChunkResidencyManager
            _addressableCacheClearHandles = new AsyncOperationHandle<bool>[count];
            // COLD ALLOC: bool[chunkDefinitions] - Addressables cache-clear occupancy map - owner: WorldChunkResidencyManager
            _hasAddressableCacheClearHandle = new bool[count];
#endif

            if (chunkDefinitions == null)
                return;

            for (int i = 0; i < chunkDefinitions.Length; i++)
            {
                int activationCount = chunkDefinitions[i].activationPrefabs != null
                    ? chunkDefinitions[i].activationPrefabs.Length
                    : 0;
                if (activationCount > 0)
                {
                    // COLD ALLOC: GameObject[activationPrefabs] - chunk-local spawned instance slots - owner: WorldChunkResidencyManager
                    _spawnedInstancesByChunk[i] = new GameObject[activationCount];
                }
            }
        }

        private void TryRegister()
        {
            RefreshColdServiceCache();
            TryRegisterHotSwap();
            TryRegisterDispatcherLanes();

            if (!_registeredBackpressureService)
            {
                // Non-hot-swap slot: post-Ready registration of a different owner throws CriticalBootException.
                // Same-instance re-entry is safe (RegisterServiceAllowSameInstance early-returns). Decline
                // takeover instead of clearing the world streaming owner under ready-lock (GCMonitor pattern).
                IStreamingBackpressureService incumbent = GlobalRegistry.StreamingBackpressure;
                if (!ReferenceEquals(incumbent, this) &&
                    !GlobalRegistry.IsRuntimeServicePublicationOpen<IStreamingBackpressureService>())
                {
                    // Leave _registeredBackpressureService false; OnEnable already demotes duplicates.
                }
                else
                {
                    GlobalRegistry.RegisterStreamingBackpressureRuntime(this);
                    _registeredBackpressureService = ReferenceEquals(GlobalRegistry.StreamingBackpressure, this);
                }
            }

            if (!_registeredAirlockEvents)
            {
                BaseAirlockEvents.Register(this);
                _registeredAirlockEvents = true;
            }
        }

        private void TryRegisterDispatcherLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void RefreshColdServiceCache()
        {
            _dataVault = GlobalRegistry.DataVault;
            _jobAdmissionService = GlobalRegistry.JobAdmission;
            _macroDatabaseService = GlobalRegistry.MacroDatabase;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _assetLifecycleGovernor = GlobalRegistry.AssetLifecycle;
            _vramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            _vramPressure = GlobalRegistry.VRAMPressureReadModel;
            _predictiveVramCeilingBytes = ComputePredictiveVramCeilingBytesCold();

            CacheObjectPoolService(null);

            IAmbientBiotaService ambientBiota = GlobalRegistry.AmbientBiota;
            if (ambientBiota != null)
                _ambientBiotaService = ambientBiota;

            IAsyncPersistenceService persistence = GlobalRegistry.AsyncPersistence;
            if (!IsAsyncPersistenceUsable(persistence))
                persistence = GlobalRegistry.Save as IAsyncPersistenceService;

            if (IsAsyncPersistenceUsable(persistence))
                _asyncPersistenceService = persistence;
            else
                _asyncPersistenceService = null;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPoolManager = pool;
                return;
            }

            _objectPoolManager = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolManager as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolManager = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolManager = null;
            pool = null;
            return false;
        }

        private static bool IsAsyncPersistenceUsable(IAsyncPersistenceService persistence)
        {
            return persistence != null && persistence.IsInitialized;
        }

        private void TryUnregister()
        {
            TryUnregisterDispatcherLanes();

            if (_registeredAirlockEvents)
            {
                BaseAirlockEvents.Unregister(this);
                _registeredAirlockEvents = false;
            }

            if (_registeredBackpressureService)
            {
                GlobalRegistry.UnregisterStreamingBackpressureRuntime(this);
                _registeredBackpressureService = false;
            }
        }

        private void TryUnregisterDispatcherLanes()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
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

        private void ClearColdServiceCache()
        {
            _asyncPersistenceService = null;
            _dataVault = null;
            _jobAdmissionService = null;
            _macroDatabaseService = null;
            _oceanKinematicsService = null;
            _assetLifecycleGovernor = null;
            _vramMonitor = null;
            _vramPressure = null;
            _predictiveVramCeilingBytes = 0L;
            _objectPoolManager = null;
            _ambientBiotaService = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherLanes();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterDispatcherLanes();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    CompleteResidencyJobForTeardown();
                    ReleaseStreamingLedgerBuffers(previousService as IDataVault);
                    _dataVault = currentService as IDataVault;
                    if (_dataVault != null)
                        EnsureStreamingLedgerBuffers(ResolveStreamingLedgerCapacity());
                    break;
                case GlobalRegistryServiceSlot.JobAdmissionRuntime:
                    _jobAdmissionService = currentService as IJobAdmissionService;
                    break;
                case GlobalRegistryServiceSlot.MacroDatabase:
                    _macroDatabaseService = currentService as IMacroDatabaseService;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycleGovernor = currentService as AssetLifecycleGovernor;
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as IVramPressureReadModel;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.AmbientBiotaRuntime:
                    _ambientBiotaService = currentService as IAmbientBiotaService;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    IAsyncPersistenceService persistence = currentService as IAsyncPersistenceService;
                    _asyncPersistenceService = IsAsyncPersistenceUsable(persistence) ? persistence : null;
                    break;
            }
        }

        /// <inheritdoc />
        public void OnBaseAirlockEvent(in BaseAirlockEventPayload payload)
        {
            if (!suspendPredictiveStreamingInHabitat)
                return;

            BaseAirlockEventType eventType = BaseAirlockEventPayload.GetEventType(payload.StatusFlags);
            if (eventType == BaseAirlockEventType.CycleStarted)
            {
                _habitatTransitionPauseFrames = HabitatTransitionPauseFrames;
                _forceResidencyEvaluation = true;
                return;
            }

            if (eventType == BaseAirlockEventType.CycleCompleted || eventType == BaseAirlockEventType.EnvironmentChanged)
            {
                bool isDry = BaseAirlockEventPayload.IsDry(payload.StatusFlags);
                _habitatPredictivePauseActive = isDry;
                _habitatTransitionPauseFrames = isDry ? HabitatTransitionPauseFrames : 0;
                _forceResidencyEvaluation = true;
            }
        }

        private void TickPredictiveSuspension()
        {
            if (_habitatTransitionPauseFrames > 0)
                _habitatTransitionPauseFrames--;

            _transportPredictivePauseActive = false;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (IsPlayerRuntimeContextBound(runtimeContext) &&
                runtimeContext.PlayerTransportCoordinator != null)
            {
                PlayerTransportCoordinator coordinator = runtimeContext.PlayerTransportCoordinator;
                _transportPredictivePauseActive = coordinator.HasActiveTransportSource() &&
                                                  !coordinator.IsTransportActive() &&
                                                  coordinator.BlocksHandheldToolUsage();
            }
        }

        private static bool IsPlayerRuntimeContextBound(IPlayerRuntimeContext runtimeContext)
        {
            return runtimeContext != null &&
                   runtimeContext.IsInitialized &&
                   runtimeContext.PlayerTransform != null;
        }

        private void DetectAndHandleTeleport()
        {
            if (!TryCapturePlayerMotionSnapshot(out AbsoluteUniversePosition playerAup, out _))
                return;

            if (!playerAup.IsFinite())
                return;

            if (!_hasLastPlayerAup)
            {
                _lastTeleportProbeAup = playerAup;
                _hasLastPlayerAup = true;
                return;
            }

            double distSq = DistanceSq(in _lastTeleportProbeAup, in playerAup);
            double thresholdSq = (double)TeleportDistanceMeters * TeleportDistanceMeters;
            _lastTeleportProbeAup = playerAup;

            if (distSq < thresholdSq)
                return;

            HandleTeleport(in playerAup);
        }

        private void HandleTeleport(in AbsoluteUniversePosition playerAup)
        {
            _pendingTeleportAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);
            _teleportResetPending = true;
            _forceResidencyEvaluation = true;
        }

        private void TryApplyPendingTeleportReset()
        {
            if (!_teleportResetPending || _residencyJobScheduled)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromAbsolutePosition(ToAbsoluteDouble3(in _pendingTeleportAup));
            _teleportResetPending = false;
            _pendingTeleportAup = default;
            ApplyTeleportResetNow(in playerAup);
        }

        private void ApplyTeleportResetNow(in AbsoluteUniversePosition playerAup)
        {
            ClearStreamingQueues();
            _lastPlayerAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);
            _lastPlayerVelocity = default;
            _lastPredictionDistanceMeters = 0f;
            ForceImmediateRadiusLoad(in playerAup);
            for (int i = 0; i < TeleportImmediateLoadDispatchBudget && _pendingLoadRequestCount > 0; i++)
                ProcessOneLoadRequest();

            WriteTelemetrySample(0L, TelemetryTeleportFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(TeleportContextHash, MemoryBreachContextHash, _pendingLoadRequestCount);
        }

        private void ClearStreamingQueues()
        {
            ClearLoadRequestQueue();
            ClearResidencyDecisionBuffer();

            _pendingLoadRequestCount = 0;
            _loadRequestReadIndex = 0;
            _loadRequestWriteIndex = 0;
            _loadDispatchFrame = -1;
            _loadDispatchBudgetTokens = 0f;
            _debugPendingLoadRequests = 0;
            _deferredEvictCount = 0;

            if (_loadRequestQueuedByChunk != null)
            {
                for (int i = 0; i < _loadRequestQueuedByChunk.Length; i++)
                {
                    if (_loadRequestQueuedByChunk[i] && !IsChunkLoadInFlight(i))
                    {
                        long queuedChunkId = _chunkIdsByDefinitionIndex != null && (uint)i < (uint)_chunkIdsByDefinitionIndex.Length
                            ? _chunkIdsByDefinitionIndex[i]
                            : 0L;
                        if (queuedChunkId != 0L && TryGetChunkState(queuedChunkId, out ChunkState queuedState))
                        {
                            queuedState &= unchecked((ChunkState)~(byte)ChunkState.Loading);
                            SetChunkState(queuedChunkId, queuedState);
                        }
                    }

                    _loadRequestQueuedByChunk[i] = false;
                }
            }

            if (_evictRequestQueuedByChunk != null)
            {
                for (int i = 0; i < _evictRequestQueuedByChunk.Length; i++)
                    _evictRequestQueuedByChunk[i] = false;
            }
        }

        private void ClearLoadRequestQueue()
        {
            if (!TryResolveLoadRequestQueue(out NativeArray<ChunkLoadRequest> loadRequests))
                return;

            int capacity = math.min(ResolveLoadRequestQueueCapacity(), loadRequests.Length);
            for (int i = 0; i < capacity; i++)
                loadRequests[i] = default;
        }

        private void ClearResidencyDecisionBuffer()
        {
            if (!TryResolveWorldStreamingVaultBuffer(
                    in _residencyDecisionsHandle,
                    ResidencyDecisionsVaultBufferId,
                    ResolveStreamingLedgerCapacity(),
                    out NativeArray<ResidencyDecisionDTO> decisions))
            {
                return;
            }

            int count = math.min(_chunkCount, decisions.Length);
            for (int i = 0; i < count; i++)
                decisions[i] = default;
        }

        private bool IsChunkLoadInFlight(int index)
        {
            if (_additiveSceneOperations != null &&
                (uint)index < (uint)_additiveSceneOperations.Length &&
                _additiveSceneOperations[index] != null)
            {
                return true;
            }

#if UNITY_ADDRESSABLES_EXIST
            return _hasAddressableHandle != null &&
                   (uint)index < (uint)_hasAddressableHandle.Length &&
                   _hasAddressableHandle[index];
#else
            return false;
#endif
        }

        private void ForceImmediateRadiusLoad(in AbsoluteUniversePosition playerAup)
        {
            float effectiveLoadRadiusMeters = ResolveEffectiveLoadRadiusMeters();
            double loadRadiusSq = (double)effectiveLoadRadiusMeters * effectiveLoadRadiusMeters;
            double playerX = ToAbsoluteX(in playerAup);
            double playerY = ToAbsoluteY(in playerAup);
            double playerZ = ToAbsoluteZ(in playerAup);

            if (!TryResolveChunkTableBuffers(out NativeArray<long> chunkIds, out NativeArray<AbsoluteUniversePositionBlit> chunkCenters))
                return;

            for (int i = 0; i < _chunkCount; i++)
            {
                long chunkId = chunkIds[i];
                if (!TryGetChunkStateAtStorageIndex(i, out ChunkState state))
                    continue;

                if (HasFlag(state, ChunkState.Resident) || HasFlag(state, ChunkState.Loading) || HasFlag(state, ChunkState.Evicting))
                    continue;

                AbsoluteUniversePositionBlit center = chunkCenters[i];
                double dx = ToAbsoluteX(in center) - playerX;
                double dy = ToAbsoluteY(in center) - playerY;
                double dz = ToAbsoluteZ(in center) - playerZ;
                double distSq = (dx * dx) + (dy * dy) + (dz * dz);
                if (distSq <= loadRadiusSq)
                    RequestLoad(chunkId, 4, LoadRequestFlagTeleport, (float)math.min(distSq, float.MaxValue));
            }

            _forceResidencyEvaluation = true;
        }

        private void ScheduleResidencyJob()
        {
            if (!TryCapturePlayerMotionSnapshot(out AbsoluteUniversePosition playerAup, out float3 playerVelocity))
                return;

            if (!playerAup.IsFinite() || !IsFinite(playerVelocity))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return;
            }

            _predictiveVramAborted = ResolvePredictiveVramAbortState();
            bool predictiveEnabled = ShouldLoadSpeculative();
            float predictionDistanceMeters = predictiveEnabled ? ResolvePredictionDistanceMeters(playerVelocity) : 0f;
            if (predictionDistanceMeters > 0f && _predictionConstrainedByStorageDebt)
                predictionDistanceMeters *= 0.5f;
            float effectiveLoadRadiusMeters = ResolveEffectiveLoadRadiusMeters();
            float effectiveUnloadRadiusMeters = ResolveEffectiveUnloadRadiusMeters();
            float tailUnloadRadiusMeters = predictiveEnabled
                ? ResolveTailUnloadRadiusMeters(predictionDistanceMeters, effectiveLoadRadiusMeters, effectiveUnloadRadiusMeters)
                : effectiveUnloadRadiusMeters;
            AbsoluteUniversePosition projectedAup = BuildProjectedAup(in playerAup, playerVelocity, predictionDistanceMeters);

            _lastPlayerAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);
            _lastProjectedAup = AbsoluteUniversePositionBlit.FromAup(in projectedAup);
            _lastPlayerVelocity = playerVelocity;
            _lastPredictionDistanceMeters = predictionDistanceMeters;
            _debugPredictiveSuspended = !predictiveEnabled;

            ClearResidencyDecisionBuffer();
            if (!TryResolveChunkTableBuffers(out NativeArray<long> chunkIds, out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                !TryResolveResidencyStateBuffers(
                    out NativeArray<ChunkStateSlotDTO> stateSlots,
                    out NativeArray<ResidencyDecisionDTO> decisions))
            {
                _forceResidencyEvaluation = true;
                return;
            }

            NativeArray<ChunkResidencyDTO> residencyDtos = ResolveChunkResidencyDtos();
            RadiusBasedStreamingJob job = new RadiusBasedStreamingJob
            {
                ChunkIds = chunkIds.GetSubArray(0, _chunkCount),
                ChunkCenters = chunkCenters.GetSubArray(0, _chunkCount),
                ChunkStates = stateSlots.GetSubArray(0, _chunkCount),
                ResidencyDtos = residencyDtos.IsCreated ? residencyDtos.GetSubArray(0, _chunkCount) : default,
                Decisions = decisions.GetSubArray(0, _chunkCount),
                PlayerAbsolute = ToAbsoluteDouble3(in playerAup),
                PlayerVelocity = playerVelocity,
                LoadRadiusSq = (double)effectiveLoadRadiusMeters * effectiveLoadRadiusMeters,
                UnloadRadiusSq = (double)effectiveUnloadRadiusMeters * effectiveUnloadRadiusMeters,
                PredictiveDistanceMeters = predictionDistanceMeters,
                TailUnloadRadiusSq = (double)tailUnloadRadiusMeters * tailUnloadRadiusMeters,
                PredictiveEnabled = predictiveEnabled ? (byte)1 : (byte)0
            };

            if (!TryScheduleParallelAdmitted(
                    job,
                    _chunkCount,
                    32,
                    JobAdmissionLane.Lane1_World,
                    default,
                    out JobHandle scanHandle))
            {
                _forceResidencyEvaluation = true;
                return;
            }

            _residencyJobHandle = scanHandle;
            _residencyJobScheduled = true;
            _residencyScheduleTimestamp = Stopwatch.GetTimestamp();
            _forceResidencyEvaluation = false;
        }

        private void CompleteResidencyJobIfFinished()
        {
            if (!_residencyJobScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _residencyJobHandle))
                return;

            float chainMs = (float)((Stopwatch.GetTimestamp() - _residencyScheduleTimestamp) * _StopwatchMillisecondsPerTick);
            ReportAdmittedJobCompleted<RadiusBasedStreamingJob>(JobAdmissionLane.Lane1_World, chainMs);

            _residencyJobScheduled = false;
        }

        private void CompleteResidencyJobForTeardown()
        {
            if (!_residencyJobScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _residencyJobHandle, forceComplete: true);
            _residencyJobScheduled = false;
        }

        private bool TryScheduleAdmitted<TJob>(
            TJob jobData,
            JobAdmissionLane lane,
            JobHandle dependsOn,
            out JobHandle handle)
            where TJob : struct, IJob
        {
            IJobAdmissionService service = _jobAdmissionService;
            uint jobHash = JobAdmissionHash<TJob>.Value;
            if (service != null && !service.TryAdmitJob(lane, jobHash, out _))
            {
                handle = dependsOn;
                return false;
            }

            handle = jobData.Schedule(dependsOn);
            return true;
        }

        private bool TryScheduleParallelAdmitted<TJob>(
            TJob jobData,
            int arrayLength,
            int innerloopBatchCount,
            JobAdmissionLane lane,
            JobHandle dependsOn,
            out JobHandle handle)
            where TJob : struct, IJobParallelFor
        {
            if (arrayLength <= 0)
            {
                handle = dependsOn;
                return arrayLength == 0;
            }

            IJobAdmissionService service = _jobAdmissionService;
            uint jobHash = JobAdmissionHash<TJob>.Value;
            int safeBatchCount = JobAdmissionScheduleExtensions.ResolveProfiledInnerloopBatchCount(jobHash, arrayLength, innerloopBatchCount);
            if (service != null && !service.TryAdmitJob(lane, jobHash, out _))
            {
                handle = dependsOn;
                return false;
            }

            handle = jobData.Schedule(arrayLength, safeBatchCount, dependsOn);
            return true;
        }

        private void ReportAdmittedJobCompleted<TJob>(JobAdmissionLane lane, float measuredCompleteMs)
            where TJob : struct
        {
            IJobAdmissionService service = _jobAdmissionService;
            if (service == null)
                return;

            service.ReportJobCompleted(lane, JobAdmissionHash<TJob>.Value, measuredCompleteMs);
        }

        private void ProcessResidencyResults()
        {
            if (_residencyJobScheduled)
                return;

            if (!TryResolveWorldStreamingVaultBuffer(
                    in _residencyDecisionsHandle,
                    ResidencyDecisionsVaultBufferId,
                    ResolveStreamingLedgerCapacity(),
                    out NativeArray<ResidencyDecisionDTO> decisions))
            {
                return;
            }

            int count = math.min(_chunkCount, decisions.Length);
            for (int i = 0; i < count; i++)
            {
                ResidencyDecisionDTO decision = decisions[i];
                if (decision.Action != 1)
                    continue;

                byte flags = ResolveLoadFlagsForChunk(decision.ChunkId);
                byte priority = HasFlag(flags, LoadRequestFlagPredictive) ? (byte)2 : (byte)3;
                RequestLoad(decision.ChunkId, priority, flags, ResolveProjectedDistanceSq(decision.ChunkId));
            }

            for (int i = 0; i < count; i++)
            {
                ResidencyDecisionDTO decision = decisions[i];
                if (decision.Action != 2)
                    continue;

                long chunkId = decision.ChunkId;
                bool clearCache = ShouldClearAddressableCacheOnEvict(chunkId);
                RequestEvict(chunkId, clearCache);
                decisions[i] = default;
            }

            for (int i = 0; i < count; i++)
            {
                if (decisions[i].Action == 1)
                    decisions[i] = default;
            }
        }

        private void ScheduleForcedResidencyEvaluation()
        {
            if (!_forceResidencyEvaluation || _residencyJobScheduled || _chunkCount <= 0)
                return;

            ScheduleResidencyJob();
        }

        private void ProcessDeferredEvictions()
        {
            if (_residencyJobScheduled || _deferredEvictCount <= 0 || _deferredEvictChunkIds == null)
                return;

            int count = _deferredEvictCount;
            _deferredEvictCount = 0;
            for (int i = 0; i < count; i++)
            {
                long chunkId = _deferredEvictChunkIds[i];
                _deferredEvictChunkIds[i] = 0L;
                if (!TryGetChunkDefinitionIndex(chunkId, out int index))
                    continue;

                if (_evictRequestQueuedByChunk != null)
                    _evictRequestQueuedByChunk[index] = false;

                EvictChunkNow(index, chunkId, ShouldClearAddressableCacheOnEvict(chunkId));
            }
        }

        private void QueueDeferredEviction(int index, long chunkId)
        {
            if (_deferredEvictChunkIds == null || _evictRequestQueuedByChunk == null || (uint)index >= (uint)_evictRequestQueuedByChunk.Length)
                return;

            if (_evictRequestQueuedByChunk[index])
                return;

            if (_deferredEvictCount >= _deferredEvictChunkIds.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(LoadRingOverflowWarningHash, MemoryBreachContextHash, _deferredEvictCount);
                return;
            }

            _evictRequestQueuedByChunk[index] = true;
            _deferredEvictChunkIds[_deferredEvictCount] = chunkId;
            _deferredEvictCount++;
        }

        private void ProcessLoadDispatchBudget()
        {
            if (_pendingLoadRequestCount <= 0)
                return;

            int inflight = ResolveInFlightLoadCount();
            int maxLoads = ResolveMaxConcurrentLoads();
            if (inflight >= maxLoads)
                return;

            int budget = math.min(ConsumeLoadDispatchBudget(), maxLoads - inflight);
            for (int i = 0; i < budget && _pendingLoadRequestCount > 0; i++)
                ProcessOneLoadRequest();
        }

        private int ResolveInFlightLoadCount()
        {
            int count = math.max(0, _pendingAdditiveSceneOperationCount);
#if UNITY_ADDRESSABLES_EXIST
            count += math.max(0, _pendingAddressableLoadCount);
#endif
            return count;
        }

        private int ResolveMaxConcurrentLoads()
        {
            int cap = math.clamp(maxConcurrentLoads, 1, 16);
            if (_healthRadiusSqueezeActive || _predictiveVramAborted)
                cap = math.min(cap, 2);
            return ResolveQualityScaledConcurrentLoadCap(cap);
        }

        private int ConsumeLoadDispatchBudget()
        {
            if (_predictiveVramAborted)
                return SurvivalLoadDispatchBudget;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_loadDispatchFrame != frame)
            {
                _loadDispatchFrame = frame;
                float perFrame = ResolveLoadDispatchBudgetPerFrame();
                float frameCap = math.ceil(perFrame);
                _loadDispatchBudgetTokens = math.min(frameCap, _loadDispatchBudgetTokens + perFrame);
            }

            int budget = math.clamp((int)math.floor(_loadDispatchBudgetTokens), 0, VisualOverkillLoadDispatchBudget);
            _loadDispatchBudgetTokens = math.max(0f, _loadDispatchBudgetTokens - budget);
            return budget;
        }

        private static float ResolveLoadDispatchBudgetPerFrame()
        {
            return math.lerp(SurvivalLoadDispatchBudget, VisualOverkillLoadDispatchBudget, ResolveSmoothGlobalQualityWeight01());
        }

        private static int ResolveQualityScaledConcurrentLoadCap(int serializedCap)
        {
            int safeCap = math.clamp(serializedCap, 1, 16);
            int lowCap = math.min(safeCap, 2);
            float continuousCap = math.lerp(lowCap, safeCap, ResolveSmoothGlobalQualityWeight01());
            return math.clamp((int)math.ceil(continuousCap), lowCap, safeCap);
        }

        private void ProcessOneLoadRequest()
        {
            if (_pendingLoadRequestCount <= 0 || !TryResolveLoadRequestQueue(out NativeArray<ChunkLoadRequest> loadRequests))
                return;

            using (_loadDispatchMarker.Auto())
            {
                if (!TryDequeueBestLoadRequest(loadRequests, out ChunkLoadRequest request))
                {
                    _pendingLoadRequestCount = 0;
                    _debugPendingLoadRequests = 0;
                    _loadRequestReadIndex = 0;
                    _loadRequestWriteIndex = 0;
                    return;
                }

                _pendingLoadRequestCount = math.max(0, _pendingLoadRequestCount - 1);
                _debugPendingLoadRequests = _pendingLoadRequestCount;
                if (_pendingLoadRequestCount == 0)
                {
                    _loadRequestReadIndex = 0;
                    _loadRequestWriteIndex = 0;
                }

                if (!TryGetChunkDefinitionIndex(request.ChunkId, out int index))
                    return;

                if (_loadRequestQueuedByChunk != null)
                    _loadRequestQueuedByChunk[index] = false;

                if (!TryGetChunkState(request.ChunkId, out ChunkState state))
                    return;

                if (HasFlag(state, ChunkState.Resident) || HasFlag(state, ChunkState.Evicting))
                    return;

                if (!HasFlag(state, ChunkState.Loading))
                {
                    state |= ChunkState.Loading;
                    SetChunkState(request.ChunkId, state);
                }

                if (RuntimeWatchdog.GetAvailableMemory() < MemoryGuardBytes)
                {
                    GlobalTelemetryBus.PublishMemoryBreachEvent(MemoryBreachContextHash, Profiler.GetTotalReservedMemoryLong() * GlobalTelemetryBus.BytesToMegabytes);
                    WriteTelemetrySample(request.ChunkId, TelemetryMemoryBreachFlag);
                    ClearLoadingFlag(request.ChunkId);
                    return;
                }

                bool predictiveAbortNow = ResolvePredictiveVramAbortState();
                _predictiveVramAborted = predictiveAbortNow;
                if (HasFlag(request.Flags, LoadRequestFlagPredictive) && predictiveAbortNow)
                {
                    _debugPredictiveSuspended = true;
                    WriteTelemetrySample(request.ChunkId, TelemetryPredictiveSuspendedFlag);
                    ClearLoadingFlag(request.ChunkId);
                    return;
                }

                DispatchChunkLoad(index, request.ChunkId, HasFlag(request.Flags, LoadRequestFlagPredictive));
            }
        }

        private bool TryDequeueBestLoadRequest(NativeArray<ChunkLoadRequest> loadRequests, out ChunkLoadRequest request)
        {
            request = default;
            int capacity = math.min(loadQueueCapacity, loadRequests.Length);
            int count = math.min(_pendingLoadRequestCount, capacity);
            if (!loadRequests.IsCreated || capacity <= 0 || count <= 0)
                return false;

            if ((uint)_loadRequestReadIndex >= (uint)capacity)
                _loadRequestReadIndex = 0;

            int bestSlot = _loadRequestReadIndex;
            ChunkLoadRequest best = loadRequests[bestSlot];
            for (int i = 1; i < count; i++)
            {
                int slot = _loadRequestReadIndex + i;
                if (slot >= capacity)
                    slot -= capacity;

                ChunkLoadRequest candidate = loadRequests[slot];
                if (candidate.Priority > best.Priority ||
                    (candidate.Priority == best.Priority && candidate.DistanceSq < best.DistanceSq) ||
                    (candidate.Priority == best.Priority && candidate.DistanceSq == best.DistanceSq && candidate.Frame < best.Frame))
                {
                    bestSlot = slot;
                    best = candidate;
                }
            }

            int readSlot = _loadRequestReadIndex;
            request = best;
            if (bestSlot != readSlot)
                loadRequests[bestSlot] = loadRequests[readSlot];
            loadRequests[readSlot] = default;

            _loadRequestReadIndex++;
            if (_loadRequestReadIndex >= capacity)
                _loadRequestReadIndex = 0;

            return true;
        }

        private void DispatchChunkLoad(int index, long chunkId, bool predictive)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return;

            ChunkDefinition definition = chunkDefinitions[index];
            string addressableAddress = ResolveUsableAddressableAddress(in definition);
            RecordAddressablesRequestDto(index, chunkId, addressableAddress);
            AdditiveSceneLoadState additiveSceneState = BeginOrTrackAdditiveSceneLoad(index, chunkId, in definition);
            if (additiveSceneState == AdditiveSceneLoadState.Failed)
            {
                ClearLoadingFlag(chunkId);
                return;
            }

            if (predictive)
                BeginPredictivePrewarm(index);
            else
                WarmChunkPrefabDependencies(index);

#if UNITY_ADDRESSABLES_EXIST
            if (!string.IsNullOrEmpty(addressableAddress))
            {
                if (_addressableHandles == null ||
                    _hasAddressableHandle == null ||
                    _addressableLoadPending == null ||
                    (uint)index >= (uint)_addressableHandles.Length ||
                    (uint)index >= (uint)_hasAddressableHandle.Length ||
                    (uint)index >= (uint)_addressableLoadPending.Length)
                {
                    WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                    ReleaseChunkHandles(index);
                    ClearLoadingFlag(chunkId);
                    return;
                }

                if (!_hasAddressableHandle[index])
                {
                    RecordAddressableLoadStart(index, predictive ? (byte)0 : (byte)1);
                    uint assetHash = StableHash(addressableAddress, chunkId);
                    AssetLifecycleGovernor assetLifecycle = _assetLifecycleGovernor;
                    if (assetLifecycle == null ||
                        !assetLifecycle.TryAcquireAddressableGameObject(
                            assetHash,
                            addressableAddress,
                            this,
                            predictive ? AssetPriorityTier.Tier6Speculative : AssetPriorityTier.Tier2Proximity,
                            AssetResidencyKind.Addressable,
                            0L,
                            true,
                            out AsyncOperationHandle<GameObject> acquiredHandle,
                            out _))
                    {
                        WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                        ClearLoadingFlag(chunkId);
                        return;
                    }

                    if (TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) &&
                        (uint)index < (uint)chunkCenters.Length)
                    {
                        AbsoluteUniversePositionBlit chunkCenter = chunkCenters[index];
                        assetLifecycle.MarkAddressableAssetAup(assetHash, ToAbsoluteDouble3(in chunkCenter));
                    }

                    _addressableHandles[index] = acquiredHandle;
                    _hasAddressableHandle[index] = true;
                    _addressableLoadPending[index] = true;
                    _pendingAddressableLoadCount++;
                    return;
                }

                if (_addressableLoadPending[index])
                    return;

                AsyncOperationHandle<GameObject> handle = _addressableHandles[index];
                if (!handle.IsValid() || !handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                    ReleaseChunkHandles(index);
                    ClearLoadingFlag(chunkId);
                    return;
                }

                if (additiveSceneState == AdditiveSceneLoadState.Pending)
                    return;

                PromoteChunkResident(index, chunkId, handle.Result);
                return;
            }
#else
            if (!string.IsNullOrEmpty(addressableAddress))
            {
                WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                ReleaseChunkHandles(index);
                ClearLoadingFlag(chunkId);
                return;
            }
#endif

            if (additiveSceneState == AdditiveSceneLoadState.Pending)
                return;

            PromoteChunkResident(index, chunkId, null);
        }

        private AdditiveSceneLoadState BeginOrTrackAdditiveSceneLoad(int index, long chunkId, in ChunkDefinition definition)
        {
            string additiveSceneName = ResolveUsableAdditiveSceneName(in definition);
            if (!definition.useAdditiveScene || string.IsNullOrEmpty(additiveSceneName))
                return AdditiveSceneLoadState.NotNeeded;

            if (_additiveSceneLoaded == null ||
                _additiveSceneOperations == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)_additiveSceneOperations.Length ||
                (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length)
            {
                WriteTelemetrySample(chunkId, TelemetryAdditiveSceneFaultFlag);
                return AdditiveSceneLoadState.Failed;
            }

            if (_additiveSceneLoaded[index])
            {
                return AdditiveSceneLoadState.NotNeeded;
            }

            if (_additiveSceneOperations[index] != null)
            {
                _additiveSceneUnloadWhenLoaded[index] = false;
                return AdditiveSceneLoadState.Pending;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(additiveSceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                WriteTelemetrySample(chunkId, TelemetryAdditiveSceneFaultFlag);
                return AdditiveSceneLoadState.Failed;
            }

            operation.allowSceneActivation = false;
            _additiveSceneOperations[index] = operation;
            _additiveSceneActivationRequested[index] = false;
            _pendingAdditiveSceneOperationCount++;
            return AdditiveSceneLoadState.Pending;
        }

        private void PollAddressableLoads()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_addressableLoadPending == null ||
                _addressableHandles == null ||
                _chunkIdsByDefinitionIndex == null ||
                _pendingAddressableLoadCount <= 0)
            {
                return;
            }

            int count = math.min(_addressableLoadPending.Length, math.min(_addressableHandles.Length, _chunkIdsByDefinitionIndex.Length));
            for (int i = 0; i < count; i++)
            {
                if (!_addressableLoadPending[i])
                    continue;

                AsyncOperationHandle<GameObject> handle = _addressableHandles[i];
                long chunkId = _chunkIdsByDefinitionIndex[i];
                if (chunkId == 0L || !TryGetChunkDefinitionIndex(chunkId, out _))
                {
                    ReleaseChunkHandles(i);
                    continue;
                }

                if (!handle.IsValid())
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                    continue;
                }

                if (!handle.IsDone)
                    continue;

                RecordAddressableLoadCompletion(i);

                if (!TryGetChunkState(chunkId, out ChunkState state))
                {
                    ReleaseChunkHandles(i);
                    continue;
                }

                if (HasFlag(state, ChunkState.Resident))
                {
                    ClearAddressableLoadPending(i);
                    continue;
                }

                if (!HasFlag(state, ChunkState.Loading) || HasFlag(state, ChunkState.Evicting))
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                    continue;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (IsAdditiveSceneLoadPending(i))
                        continue;

                    MarkAddressableChunkLoaded(i, chunkId, handle);
                    ClearAddressableLoadPending(i);
                    PromoteChunkResident(i, chunkId, handle.Result);
                }
                else
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                }
            }
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private void ClearAddressableLoadPending(int index)
        {
            if (_addressableLoadPending == null ||
                (uint)index >= (uint)_addressableLoadPending.Length ||
                !_addressableLoadPending[index])
            {
                return;
            }

            _addressableLoadPending[index] = false;
            _pendingAddressableLoadCount = math.max(0, _pendingAddressableLoadCount - 1);
            ClearAddressableLoadTiming(index);
        }

        private void MarkAddressableChunkLoaded(int index, long chunkId, AsyncOperationHandle<GameObject> handle)
        {
            if (chunkDefinitions == null ||
                (uint)index >= (uint)chunkDefinitions.Length ||
                !handle.IsValid() ||
                handle.Status != AsyncOperationStatus.Succeeded)
            {
                return;
            }

            AssetLifecycleGovernor assetLifecycle = _assetLifecycleGovernor;
            if (assetLifecycle == null)
                return;

            ChunkDefinition definition = chunkDefinitions[index];
            if (!HasUsableAddressableAddress(in definition))
                return;

            string addressableAddress = ResolveUsableAddressableAddress(in definition);
            uint assetHash = StableHash(addressableAddress, chunkId);
            assetLifecycle.MarkAddressableLoaded(
                assetHash,
                handle,
                handle.Result,
                EstimateAddressableChunkBytes(index),
                true);

            if (TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) &&
                (uint)index < (uint)chunkCenters.Length)
            {
                AbsoluteUniversePositionBlit chunkCenter = chunkCenters[index];
                assetLifecycle.MarkAddressableAssetAup(assetHash, ToAbsoluteDouble3(in chunkCenter));
            }
        }

#endif

        private void RecordAddressableLoadStart(int index, byte immediateRadius)
        {
            if (!TryResolveLoadTimingBuffers(out NativeArray<double> loadStartTimes, out NativeArray<byte> immediateRadiusFlags) ||
                (uint)index >= (uint)loadStartTimes.Length ||
                (uint)index >= (uint)immediateRadiusFlags.Length)
            {
                return;
            }

            double now = RuntimeNowSeconds();
            loadStartTimes[index] = now;
            immediateRadiusFlags[index] = immediateRadius;
        }

        private void RecordAddressableLoadCompletion(int index)
        {
            if (!TryResolveLoadTimingBuffers(out NativeArray<double> loadStartTimes, out NativeArray<byte> immediateRadiusFlags) ||
                (uint)index >= (uint)loadStartTimes.Length)
                return;
            _ = immediateRadiusFlags;

            double startTime = loadStartTimes[index];
            if (startTime <= 0d || !IsFiniteDouble(startTime))
                return;

            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!IsFiniteDouble(now) || now < startTime)
            {
                DumpBackpressureTelemetry(TelemetryAddressablesFaultFlag);
                ClearAddressableLoadTiming(index);
                return;
            }

            double latencyMs = (now - startTime) * 1000.0;
            if (!IsFiniteDouble(latencyMs))
            {
                DumpBackpressureTelemetry(TelemetryAddressablesFaultFlag);
                ClearAddressableLoadTiming(index);
                return;
            }

            _latencyEwmaMs = _latencyEwmaMs <= 0d
                ? latencyMs
                : math.lerp(_latencyEwmaMs, latencyMs, StorageDebtEwmaWeight);
            ClearAddressableLoadTiming(index);
        }

        private void ClearAddressableLoadTiming(int index)
        {
            if (!TryResolveLoadTimingBuffers(out NativeArray<double> loadStartTimes, out NativeArray<byte> immediateRadiusFlags))
                return;

            if ((uint)index < (uint)loadStartTimes.Length)
                loadStartTimes[index] = 0d;
            if ((uint)index < (uint)immediateRadiusFlags.Length)
                immediateRadiusFlags[index] = 0;
        }

        private void EvaluateAndPublishStorageBackpressure()
        {
            double now = RuntimeNowSeconds();

            double oldestPendingMs = 0d;
            int pendingLoads = 0;
#if UNITY_ADDRESSABLES_EXIST
            pendingLoads = _pendingAddressableLoadCount;
            if (_addressableLoadPending != null &&
                TryResolveLoadTimingBuffers(out NativeArray<double> loadStartTimes, out NativeArray<byte> immediateRadiusFlags))
            {
                int count = math.min(_addressableLoadPending.Length, math.min(loadStartTimes.Length, immediateRadiusFlags.Length));
                for (int i = 0; i < count; i++)
                {
                    if (!_addressableLoadPending[i] || immediateRadiusFlags[i] == 0)
                        continue;

                    double startTime = loadStartTimes[i];
                    if (startTime <= 0d || !IsFiniteDouble(startTime) || now < startTime)
                        continue;

                    double pendingMs = (now - startTime) * 1000.0;
                    if (pendingMs > oldestPendingMs)
                        oldestPendingMs = pendingMs;
                }
            }
#endif

            _oldestPendingMs = oldestPendingMs;
            _criticalHoleDebtMs = math.max(0d, oldestPendingMs - CriticalHoleThresholdMs);
            if (pendingLoads <= 0 && oldestPendingMs <= 0d && _latencyEwmaMs > LatencyDebtBaselineMs)
                _latencyEwmaMs = math.lerp(_latencyEwmaMs, LatencyDebtBaselineMs, StorageDebtIdleRecoveryWeight);

            double rawDebt = ((_latencyEwmaMs - LatencyDebtBaselineMs) * 0.0023) +
                             (oldestPendingMs * 0.001) +
                             (_criticalHoleDebtMs * 0.002);
            if (!IsFiniteDouble(rawDebt))
            {
                DumpBackpressureTelemetry(TelemetryAddressablesFaultFlag);
                rawDebt = 0d;
            }

            _storageDebt01 = math.saturate((float)rawDebt);
            _smoothedStorageDebt01 = math.lerp(_smoothedStorageDebt01, _storageDebt01, StorageDebtPublishBlend);
            _storageDebtSequence++;
            UpdateStorageDebtHysteresisStates();

            byte flags = 0;
            if (_turbulenceActiveByStorageDebt)
                flags |= StorageDebtSignal.HighDebtFlag;
            if (_dataLinkDegradedByStorageDebt)
                flags |= StorageDebtSignal.DataLinkDegradedFlag;
            if (_criticalHoleDebtMs > 0d)
                flags |= StorageDebtSignal.CriticalHoleFlag;
            if (_proxyFallbackByStorageDebt)
                flags |= StorageDebtSignal.ProxyFallbackFlag;

            StorageDebtSignal signal = default;
            signal.Debt01 = _smoothedStorageDebt01;
            signal.LatencyEwmaMs = (float)math.max(0d, _latencyEwmaMs);
            signal.OldestPendingMs = (float)math.max(0d, oldestPendingMs);
            signal.CriticalHoleDebtMs = (float)math.max(0d, _criticalHoleDebtMs);
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Sequence = _storageDebtSequence;
            signal.PendingLoads = (ushort)math.min(ushort.MaxValue, math.max(0, pendingLoads));
            signal.Flags = flags;
            SignalBus<StorageDebtSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            SystemDispatcher.PublishStreamingStorageDebt(_smoothedStorageDebt01);
            CrashTelemetryBuffer.ReportStreamingBackpressureFrame(_smoothedStorageDebt01, _latencyEwmaMs, oldestPendingMs, pendingLoads);

            if (_turbulenceActiveByStorageDebt)
            {
                StreamingTurbulenceSignal turbulence = default;
                turbulence.Intensity01 = math.saturate((_smoothedStorageDebt01 - StorageDebtTurbulenceResetThreshold) * StorageDebtTurbulenceRangeRcp);
                turbulence.Debt01 = _smoothedStorageDebt01;
                turbulence.DurationSeconds = 0.35f;
                turbulence.Frame = signal.Frame;
                turbulence.SourceHash = 0x5354494Fu; // "STIO"
                turbulence.Sequence = _storageDebtSequence;
                SignalBus<StreamingTurbulenceSignal>.TryPushTracked(in turbulence, ref _signalPushDropCount);
            }

            PublishPdaDataLinkState(signal.Frame);
        }

        private void EvictDistantMacroDatabaseBreadcrumbs()
        {
            IMacroDatabaseService macroDatabase = _macroDatabaseService;
            if (macroDatabase == null ||
                !macroDatabase.IsOpen ||
                !TryResolveWorldStreamingVaultBuffer(
                    in _macroDatabaseEvictionScratchHandle,
                    MacroDatabaseEvictionScratchVaultBufferId,
                    MacroDatabaseEvictionScratchCapacity,
                    out NativeArray<ulong> macroDatabaseEvictionScratch) ||
                !TryCapturePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                return;
            }

            MacroDatabaseAup macroAup = ToMacroDatabaseAup(in playerAup);
            macroDatabase.EvictDistant(
                in macroAup,
                ResolveMacroDatabaseTier(),
                macroDatabaseEvictionScratch);
        }

        private static MacroDatabaseAup ToMacroDatabaseAup(in AbsoluteUniversePosition aup)
        {
            return new MacroDatabaseAup
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ
            };
        }

        private static MacroDatabaseTier ResolveMacroDatabaseTier()
        {
            float quality = ResolveSmoothGlobalQualityWeight01();
            if (quality < MacroDatabaseMiddleQualityThreshold)
                return MacroDatabaseTier.Low;

            if (quality < MacroDatabaseHighQualityThreshold)
                return MacroDatabaseTier.Middle;

            return quality < MacroDatabaseUltraQualityThreshold
                ? MacroDatabaseTier.High
                : MacroDatabaseTier.Ultra;
        }

        private void UpdateStorageDebtHysteresisStates()
        {
            _predictionConstrainedByStorageDebt = ResolveStorageDebtHysteresis(
                _predictionConstrainedByStorageDebt,
                _smoothedStorageDebt01,
                StorageDebtPredictionHalveThreshold,
                StorageDebtPredictionResetThreshold);
            _turbulenceActiveByStorageDebt = ResolveStorageDebtHysteresis(
                _turbulenceActiveByStorageDebt,
                _smoothedStorageDebt01,
                StorageDebtTurbulenceThreshold,
                StorageDebtTurbulenceResetThreshold);
            _proxyFallbackByStorageDebt = ResolveStorageDebtHysteresis(
                _proxyFallbackByStorageDebt,
                _smoothedStorageDebt01,
                StorageDebtProxyFallbackThreshold,
                StorageDebtProxyFallbackResetThreshold);
            _dataLinkDegradedByStorageDebt = ResolveStorageDebtHysteresis(
                _dataLinkDegradedByStorageDebt,
                _smoothedStorageDebt01,
                StorageDebtDataLinkThreshold,
                StorageDebtDataLinkResetThreshold);

            if (!_dataLinkDegradedByStorageDebt)
                _dataLinkDegradedNotificationPublished = false;
        }

        private static bool ResolveStorageDebtHysteresis(bool active, float value, float enterThreshold, float exitThreshold)
        {
            return active ? value > exitThreshold : value > enterThreshold;
        }

        private void PublishPdaDataLinkState(uint frame)
        {
            if (_dataLinkDegradedByStorageDebt)
            {
                if (_dataLinkDegradedNotificationPublished)
                    return;

                HUDNotificationSignal notification = default;
                notification.MessageHash = 0x444C4B44u; // "DLKD"
                notification.ContextHash = 0x5354494Fu; // "STIO"
                notification.Frame = frame;
                notification.Severity = 1;
                notification.Flags = StorageDebtSignal.DataLinkDegradedFlag;
                SignalBus<HUDNotificationSignal>.TryPushTracked(in notification, ref _signalPushDropCount);
                _dataLinkDegradedNotificationPublished = true;
            }
        }

        private void PromoteChunkResident(int index, long chunkId, GameObject loadedPrefab)
        {
            bool proxyFallback = _proxyFallbackByStorageDebt;
            ChunkState state = ChunkState.Resident | ChunkState.Staged | (proxyFallback ? ChunkState.LOD1 : ChunkState.LOD0);
            if (chunkDefinitions[index].pinned)
                state |= ChunkState.Pinned;

            SetChunkState(chunkId, state);
            PublishSectorHydratedSignal(index, chunkId, state);
            if (loadedPrefab != null && !proxyFallback)
                WarmPrefab(loadedPrefab, math.max(1, chunkDefinitions[index].warmupCountPerPrefab));
            else if (proxyFallback)
                WarmChunkPrefabDependencies(index);

            StartFade();
            if (_activationInProgress == null ||
                _activationVersions == null ||
                (uint)index >= (uint)_activationInProgress.Length ||
                (uint)index >= (uint)_activationVersions.Length)
            {
                WriteTelemetrySample(chunkId, TelemetryActivationFaultFlag);
                ClearStagedFlag(chunkId);
                return;
            }

            if (!_activationInProgress[index])
            {
                _ = ActivateChunkAsync(index, _activationVersions[index], destroyCancellationToken);
            }
        }

        private async Awaitable ActivateChunkAsync(int index, int version, CancellationToken cancellationToken)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0) ||
                _activationInProgress == null ||
                _chunkIdsByDefinitionIndex == null ||
                (uint)index >= (uint)_activationInProgress.Length ||
                (uint)index >= (uint)_chunkIdsByDefinitionIndex.Length)
            {
                return;
            }

            _activationInProgress[index] = true;
            try
            {
                long chunkId = _chunkIdsByDefinitionIndex[index];
                if (!IsActivationCurrent(index, version, chunkId))
                    return;

                if (_spawnedInstancesByChunk == null ||
                    _spawnedCountsByChunk == null ||
                    (uint)index >= (uint)_spawnedInstancesByChunk.Length ||
                    (uint)index >= (uint)_spawnedCountsByChunk.Length)
                {
                    WriteTelemetrySample(chunkId, TelemetryActivationFaultFlag);
                    ClearStagedFlag(chunkId);
                    return;
                }

                while (IsPredictivePrewarmBusy(index) || !IsChunkVoxelBakeReady(index, chunkId))
                {
                    if (!IsActivationCurrent(index, version, chunkId))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                    if (!IsActivationCurrent(index, version, chunkId))
                        return;
                }

                ChunkDefinition definition = chunkDefinitions[index];
                GameObject[] prefabs = definition.activationPrefabs;
                if (prefabs == null || prefabs.Length == 0)
                {
                    if (IsActivationCurrent(index, version, chunkId))
                        ClearStagedFlag(chunkId);
                    return;
                }

                if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                {
                    if (IsActivationCurrent(index, version, chunkId))
                        ClearStagedFlag(chunkId);
                    return;
                }

                int spawnedThisFrame = 0;
                int estimatedBytesThisFrame = 0;
                int copyBudgetBytes = ResolveHydrationCopyBudgetBytes();
                long copySliceStart = Stopwatch.GetTimestamp();
                GameObject[] slots = _spawnedInstancesByChunk[index];
                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (!IsActivationCurrent(index, version, chunkId))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    GameObject prefab = prefabs[i];
                    int estimatedPrefabBytes = EstimateHydrationApplyBytes(prefab);
                    if (estimatedBytesThisFrame > 0 && estimatedBytesThisFrame + estimatedPrefabBytes > copyBudgetBytes)
                    {
                        RecordHydrationApplySlice(copySliceStart, chunkId);
                        estimatedBytesThisFrame = 0;
                        spawnedThisFrame = 0;
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                        if (!IsActivationCurrent(index, version, chunkId))
                            return;

                        copySliceStart = Stopwatch.GetTimestamp();
                    }

                    CopyHydrationApplyRecordToVault(index, chunkId, i, prefab, estimatedPrefabBytes);
                    if (prefab != null && slots != null && TryBuildChunkScenePosition(index, chunkId, out Vector3 spawnPosition))
                    {
                        if (!TryResolveCachedObjectPool(out pool))
                        {
                            if (IsActivationCurrent(index, version, chunkId))
                                ClearStagedFlag(chunkId);
                            return;
                        }

                        GameObject instance = pool.Spawn(prefab, spawnPosition, Quaternion.identity);
                        if (instance != null)
                        {
                            int slotIndex = _spawnedCountsByChunk[index];
                            if ((uint)slotIndex < (uint)slots.Length)
                            {
                                slots[slotIndex] = instance;
                                _spawnedCountsByChunk[index] = slotIndex + 1;
                            }
                            else
                            {
                                _spawnedCountsByChunk[index] = slots.Length;
                                pool.Despawn(instance);
                                WriteTelemetrySample(chunkId, TelemetryActivationOverflowFlag);
                            }
                        }
                    }

                    estimatedBytesThisFrame += estimatedPrefabBytes;
                    spawnedThisFrame++;
                    if ((spawnedThisFrame >= MaxActivationsPerFrame || estimatedBytesThisFrame >= copyBudgetBytes) && i + 1 < prefabs.Length)
                    {
                        RecordHydrationApplySlice(copySliceStart, chunkId);
                        estimatedBytesThisFrame = 0;
                        spawnedThisFrame = 0;
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                        if (!IsActivationCurrent(index, version, chunkId))
                            return;

                        copySliceStart = Stopwatch.GetTimestamp();
                    }
                }

                RecordHydrationApplySlice(copySliceStart, chunkId);
                if (IsActivationCurrent(index, version, chunkId))
                    ClearStagedFlag(chunkId);
            }
            finally
            {
                if (_activationVersions != null &&
                    _activationInProgress != null &&
                    (uint)index < (uint)_activationVersions.Length &&
                    (uint)index < (uint)_activationInProgress.Length &&
                    _activationVersions[index] == version)
                {
                    _activationInProgress[index] = false;
                }
            }
        }

        private bool IsActivationCurrent(int index, int version, long chunkId)
        {
            if (_disposed ||
                _activationVersions == null ||
                _activationInProgress == null ||
                (uint)index >= (uint)_activationVersions.Length ||
                (uint)index >= (uint)_activationInProgress.Length ||
                _activationVersions[index] != version)
            {
                return false;
            }

            return TryGetChunkState(chunkId, out ChunkState state) &&
                   HasFlag(state, ChunkState.Resident) &&
                   HasFlag(state, ChunkState.Staged);
        }

        private bool IsPredictivePrewarmBusy(int index)
        {
            return _predictivePrewarmInProgress != null &&
                   (uint)index < (uint)_predictivePrewarmInProgress.Length &&
                   _predictivePrewarmInProgress[index];
        }

        private bool IsAdditiveSceneLoadPending(int index)
        {
            if (_additiveSceneLoaded == null ||
                _additiveSceneOperations == null ||
                chunkDefinitions == null ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)chunkDefinitions.Length)
            {
                return false;
            }

            ChunkDefinition definition = chunkDefinitions[index];
            return definition.useAdditiveScene &&
                   HasUsableAdditiveSceneName(in definition) &&
                   !_additiveSceneLoaded[index];
        }

        private void TryPromoteAfterAdditiveSceneReady(int index, long chunkId)
        {
            if (chunkId == 0L || !TryGetChunkState(chunkId, out ChunkState state))
                return;

            if (!HasFlag(state, ChunkState.Loading) ||
                HasFlag(state, ChunkState.Resident) ||
                HasFlag(state, ChunkState.Evicting))
            {
                return;
            }

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableLoadPending != null &&
                (uint)index < (uint)_addressableLoadPending.Length &&
                _addressableLoadPending[index])
            {
                return;
            }
#endif

            PromoteChunkResident(index, chunkId, null);
        }

        private void TryActivateReadySubScenes()
        {
            if (_additiveSceneOperations == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneLoaded == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                _chunkIdsByDefinitionIndex == null ||
                chunkDefinitions == null ||
                _pendingAdditiveSceneOperationCount <= 0)
            {
                return;
            }

            int count = math.min(
                _additiveSceneOperations.Length,
                math.min(
                    _additiveSceneActivationRequested.Length,
                    math.min(
                        _additiveSceneLoaded.Length,
                        math.min(
                            _additiveSceneUnloadWhenLoaded.Length,
                            math.min(_chunkIdsByDefinitionIndex.Length, chunkDefinitions.Length)))));
            for (int i = 0; i < count; i++)
            {
                AsyncOperation operation = _additiveSceneOperations[i];
                if (operation == null)
                    continue;

                if (!_additiveSceneActivationRequested[i])
                {
                    if (operation.progress < 0.9f)
                        continue;

                    if (!ShouldActivateAdditiveScene(i))
                        continue;

                    operation.allowSceneActivation = true;
                    _additiveSceneActivationRequested[i] = true;
                    return;
                }

                if (!operation.isDone)
                    continue;

                _additiveSceneLoaded[i] = true;
                _additiveSceneOperations[i] = null;
                _pendingAdditiveSceneOperationCount = math.max(0, _pendingAdditiveSceneOperationCount - 1);
                if (_additiveSceneUnloadWhenLoaded[i])
                {
                    UnloadAdditiveScene(i);
                    return;
                }

                TryPromoteAfterAdditiveSceneReady(i, _chunkIdsByDefinitionIndex[i]);
            }
        }

        private void WarmChunkPrefabDependencies(int index)
        {
            ChunkDefinition definition = chunkDefinitions[index];
            GameObject[] dependencies = definition.prefabDependencies;
            if (dependencies == null || dependencies.Length == 0)
                return;

            int warmupCount = math.max(1, definition.warmupCountPerPrefab);
            for (int i = 0; i < dependencies.Length; i++)
                WarmPrefab(dependencies[i], warmupCount);

            if (_predictivePrewarmComplete != null && (uint)index < (uint)_predictivePrewarmComplete.Length)
                _predictivePrewarmComplete[index] = true;
        }

        private void BeginPredictivePrewarm(int index)
        {
            if (_predictiveVramAborted || _predictivePrewarmInProgress == null || _predictivePrewarmComplete == null || _predictivePrewarmVersions == null)
                return;

            if ((uint)index >= (uint)_predictivePrewarmInProgress.Length || _predictivePrewarmInProgress[index] || _predictivePrewarmComplete[index])
                return;

            _predictivePrewarmInProgress[index] = true;
            int version = unchecked(++_predictivePrewarmVersions[index]);
            _ = PredictivePrewarmAsync(index, version, destroyCancellationToken);
        }

        private async Awaitable PredictivePrewarmAsync(int index, int version, CancellationToken cancellationToken)
        {
            bool completed = false;
            try
            {
                if (!TryResolveCachedObjectPool(out IObjectPoolService pool) ||
                    (uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                {
                    return;
                }

                ChunkDefinition definition = chunkDefinitions[index];
                TrySelectBiomeRecordForChunk(index, out _);
                GameObject[] prefabs = ResolvePredictivePrefabList(in definition);
                int count = prefabs != null ? math.min(MaxPredictiveBiomePrefabs, prefabs.Length) : 0;
                int warmupCount = math.max(1, definition.warmupCountPerPrefab);
                for (int i = 0; i < count; i++)
                {
                    if (!IsPredictivePrewarmCurrent(index, version))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    GameObject prefab = prefabs[i];
                    if (prefab != null && !HasEarlierPrefab(prefabs, i, prefab))
                    {
                        if (!TryResolveCachedObjectPool(out pool))
                            return;

                        await pool.WarmupPrefabAsync(prefab, warmupCount, 0.2d, cancellationToken);

                        if (!IsPredictivePrewarmCurrent(index, version))
                            return;
                    }

                    if (i + 1 < count)
                    {
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                        if (!IsPredictivePrewarmCurrent(index, version))
                            return;
                    }
                }

                completed = true;
            }
            catch (OperationCanceledException)
            {
                completed = false;
            }
            catch (Exception)
            {
                completed = false;
                long chunkId = 0L;
                if (TryResolveWorldStreamingVaultBuffer(in _chunkIdsHandle, ChunkIdsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<long> chunkIds) &&
                    (uint)index < (uint)chunkIds.Length)
                {
                    chunkId = chunkIds[index];
                }
                WriteTelemetrySample(chunkId, TelemetryPredictivePrewarmFaultFlag);
            }
            finally
            {
                if (_predictivePrewarmVersions != null &&
                    _predictivePrewarmInProgress != null &&
                    _predictivePrewarmComplete != null &&
                    (uint)index < (uint)_predictivePrewarmVersions.Length &&
                    _predictivePrewarmVersions[index] == version)
                {
                    _predictivePrewarmInProgress[index] = false;
                    _predictivePrewarmComplete[index] = completed;
                }
            }
        }

        private bool IsPredictivePrewarmCurrent(int index, int version)
        {
            return !_disposed &&
                   _predictivePrewarmVersions != null &&
                   (uint)index < (uint)_predictivePrewarmVersions.Length &&
                   _predictivePrewarmVersions[index] == version;
        }

        private static bool HasEarlierPrefab(GameObject[] prefabs, int index, GameObject prefab)
        {
            for (int i = 0; i < index; i++)
            {
                if (ReferenceEquals(prefabs[i], prefab))
                    return true;
            }

            return false;
        }

        private static GameObject[] ResolvePredictivePrefabList(in ChunkDefinition definition)
        {
            if (definition.predictivePrewarmPrefabs != null && definition.predictivePrewarmPrefabs.Length > 0)
                return definition.predictivePrewarmPrefabs;
            if (definition.prefabDependencies != null && definition.prefabDependencies.Length > 0)
                return definition.prefabDependencies;
            return definition.activationPrefabs;
        }

        private unsafe bool TrySelectBiomeRecordForChunk(int index, out H8BiomeRecord record)
        {
            record = default;
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return false;

            ChunkDefinition definition = chunkDefinitions[index];
            if (definition.biomeHash != 0u && TryResolveBiomeRecord(definition.biomeHash, out record))
                return true;

            if (!TryReadNativeChunkCenter(index, out AbsoluteUniversePositionBlit centerAup))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            double depthMeters = ResolveChunkDepthMeters(in centerAup);
            ReadOnlySpan<H8BiomeRecord> records = H8StaticDataArena.GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes);
            if (records.Length <= 0)
                return false;

            for (int i = 0; i < records.Length; i++)
            {
                H8BiomeRecord candidate = records[i];
                if (depthMeters < candidate.MinDepthMeters || depthMeters > candidate.MaxDepthMeters)
                    continue;

                record = candidate;
                return true;
            }

            return false;
        }

        private double ResolveChunkDepthMeters(in AbsoluteUniversePositionBlit centerAup)
        {
            double centerY = ToAbsoluteY(in centerAup);
            return math.isfinite(centerY) ? math.max(0d, ResolveChunkSeaLevelY() - centerY) : 0d;
        }

        private double ResolveChunkSeaLevelY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveChunkSeaLevelY(oceanKinematics.SeaLevel, out double seaLevelY))
            {
                return seaLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool TryResolveChunkSeaLevelY(float candidateSeaLevelY, out double seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private static unsafe bool TryResolveBiomeRecord(uint biomeHash, out H8BiomeRecord record)
        {
            record = default;
            if (biomeHash == 0u)
                return false;

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

        private bool IsChunkVoxelBakeReady(int index, long chunkId)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return true;

            if (chunkDefinitions[index].voxelBakeReadinessProvider is IChunkVoxelBakeReadiness readiness)
                return readiness.IsBaseVoxelMeshReady(chunkId);

            return true;
        }

        private void ClearStagedFlag(long chunkId)
        {
            if (!TryGetChunkState(chunkId, out ChunkState state) || !HasFlag(state, ChunkState.Staged))
                return;

            state &= unchecked((ChunkState)~(byte)ChunkState.Staged);
            SetChunkState(chunkId, state);
        }

        private void WarmPrefab(GameObject prefab, int count)
        {
            if (!TryResolveCachedObjectPool(out IObjectPoolService pool) || prefab == null)
                return;

            pool.Warmup(prefab, count);
        }

        private void DespawnChunkInstances(int index)
        {
            if (_spawnedInstancesByChunk == null ||
                _spawnedCountsByChunk == null ||
                (uint)index >= (uint)_spawnedInstancesByChunk.Length ||
                (uint)index >= (uint)_spawnedCountsByChunk.Length)
            {
                return;
            }

            GameObject[] slots = _spawnedInstancesByChunk[index];
            int count = _spawnedCountsByChunk[index];
            if (slots != null && count > slots.Length)
                count = slots.Length;
            if (TryResolveCachedObjectPool(out IObjectPoolService pool) && slots != null)
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = slots[i];
                    slots[i] = null;
                    if (instance != null)
                        pool.Despawn(instance);
                }
            }

            _spawnedCountsByChunk[index] = 0;
        }

#if UNITY_ADDRESSABLES_EXIST
        private bool TryStageExternalAddressableRelease<TObject>(AsyncOperationHandle<TObject> handle)
        {
            if (!handle.IsValid())
                return true;

            AssetLifecycleGovernor assetLifecycle = _assetLifecycleGovernor;
            return assetLifecycle != null && assetLifecycle.TryStageExternalAddressableRelease(handle);
        }

        private bool TryReleaseExternalAddressableFault<TObject>(AsyncOperationHandle<TObject> handle)
        {
            if (!handle.IsValid())
                return true;

            AssetLifecycleGovernor assetLifecycle = _assetLifecycleGovernor;
            return assetLifecycle != null && assetLifecycle.TryReleaseExternalAddressableFault(handle);
        }
#endif

        private void ReleaseChunkHandles(int index, bool clearAddressableCache = false)
        {
            bool hasDefinition = chunkDefinitions != null && (uint)index < (uint)chunkDefinitions.Length;
            using (_releaseMarker.Auto())
            {
#if UNITY_ADDRESSABLES_EXIST
                if (_hasAddressableHandle != null &&
                    _addressableHandles != null &&
                    (uint)index < (uint)_hasAddressableHandle.Length &&
                    (uint)index < (uint)_addressableHandles.Length &&
                    _hasAddressableHandle[index])
                {
                    ClearAddressableLoadPending(index);
                    AsyncOperationHandle<GameObject> handle = _addressableHandles[index];
                    if (handle.IsValid())
                    {
                        uint assetHash = ResolveAddressableAssetHash(index);
                        AssetLifecycleGovernor assetLifecycle = _assetLifecycleGovernor;
                        bool releaseAccepted = false;
                        if (assetLifecycle != null && assetHash != 0u)
                        {
                            assetLifecycle.ReleaseAddressableAsset(assetHash);
                            releaseAccepted = true;
                        }
                        else if (assetLifecycle != null)
                        {
                            releaseAccepted = assetLifecycle.TryStageExternalAddressableRelease(handle);
                        }

                        if (!releaseAccepted)
                        {
                            WriteTelemetrySample(_chunkIdsByDefinitionIndex != null && (uint)index < (uint)_chunkIdsByDefinitionIndex.Length
                                ? _chunkIdsByDefinitionIndex[index]
                                : 0L, TelemetryAddressablesFaultFlag);
                            return;
                        }
                    }

                    _addressableHandles[index] = default;
                    _hasAddressableHandle[index] = false;
                    ClearAddressableLoadTiming(index);
                }

                if (clearAddressableCache)
                    RequestAddressablesCacheClear(index);
#endif
                if (clearAddressableCache)
                    _assetLifecycleGovernor?.DrainPendingReleaseQueueBudgeted(AssetLifecycleFarBehindDrainBudget);

                if (_predictivePrewarmVersions != null && (uint)index < (uint)_predictivePrewarmVersions.Length)
                    _predictivePrewarmVersions[index] = unchecked(_predictivePrewarmVersions[index] + 1);
                if (_predictivePrewarmInProgress != null && (uint)index < (uint)_predictivePrewarmInProgress.Length)
                    _predictivePrewarmInProgress[index] = false;
                if (_predictivePrewarmComplete != null && (uint)index < (uint)_predictivePrewarmComplete.Length)
                    _predictivePrewarmComplete[index] = false;
                if (_activationVersions != null && (uint)index < (uint)_activationVersions.Length)
                    _activationVersions[index] = unchecked(_activationVersions[index] + 1);
                if (_activationInProgress != null && (uint)index < (uint)_activationInProgress.Length)
                    _activationInProgress[index] = false;

                if (_additiveSceneLoaded == null ||
                    _additiveSceneOperations == null ||
                    _additiveSceneActivationRequested == null ||
                    _additiveSceneUnloadWhenLoaded == null ||
                    !hasDefinition ||
                    (uint)index >= (uint)_additiveSceneLoaded.Length ||
                    (uint)index >= (uint)_additiveSceneOperations.Length ||
                    (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                    (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length ||
                    !HasUsableAdditiveSceneName(in chunkDefinitions[index]))
                {
                    return;
                }

                AsyncOperation operation = _additiveSceneOperations[index];
                if (operation != null && !_additiveSceneLoaded[index])
                {
                    _additiveSceneUnloadWhenLoaded[index] = true;
                    operation.allowSceneActivation = true;
                    _additiveSceneActivationRequested[index] = true;
                    return;
                }

                if (_additiveSceneLoaded[index])
                    UnloadAdditiveScene(index);
            }
        }

        private void UnloadAdditiveScene(int index)
        {
            if (chunkDefinitions == null ||
                _additiveSceneLoaded == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                _additiveSceneOperations == null ||
                (uint)index >= (uint)chunkDefinitions.Length ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length ||
                (uint)index >= (uint)_additiveSceneOperations.Length)
            {
                return;
            }

            string sceneName = ResolveUsableAdditiveSceneName(in chunkDefinitions[index]);
            if (!string.IsNullOrEmpty(sceneName))
                SceneManager.UnloadSceneAsync(sceneName);

            _additiveSceneLoaded[index] = false;
            _additiveSceneActivationRequested[index] = false;
            _additiveSceneUnloadWhenLoaded[index] = false;
            _additiveSceneOperations[index] = null;
        }

        private void RequestAddressablesCacheClear(int index)
        {
#if UNITY_ADDRESSABLES_EXIST
            if (chunkDefinitions == null ||
                _hasAddressableCacheClearHandle == null ||
                _addressableCacheClearHandles == null ||
                (uint)index >= (uint)chunkDefinitions.Length ||
                (uint)index >= (uint)_hasAddressableCacheClearHandle.Length ||
                (uint)index >= (uint)_addressableCacheClearHandles.Length ||
                _hasAddressableCacheClearHandle[index])
            {
                return;
            }

            string address = ResolveUsableAddressableAddress(in chunkDefinitions[index]);
            if (string.IsNullOrEmpty(address))
                return;

            _addressableCacheClearHandles[index] = Addressables.ClearDependencyCacheAsync(address, false);
            _hasAddressableCacheClearHandle[index] = true;
            _pendingAddressableCacheClearCount++;
#endif
        }

        private void PollAddressableCacheClears()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_hasAddressableCacheClearHandle == null ||
                _addressableCacheClearHandles == null ||
                _pendingAddressableCacheClearCount <= 0)
            {
                return;
            }

            int count = math.min(_hasAddressableCacheClearHandle.Length, _addressableCacheClearHandles.Length);
            for (int i = 0; i < count; i++)
            {
                if (!_hasAddressableCacheClearHandle[i])
                    continue;

                AsyncOperationHandle<bool> handle = _addressableCacheClearHandles[i];
                if (!handle.IsValid())
                {
                    _addressableCacheClearHandles[i] = default;
                    _hasAddressableCacheClearHandle[i] = false;
                    _pendingAddressableCacheClearCount = math.max(0, _pendingAddressableCacheClearCount - 1);
                    continue;
                }

                if (!handle.IsDone)
                    continue;

                if (!TryStageExternalAddressableRelease(handle))
                    continue;

                _addressableCacheClearHandles[i] = default;
                _hasAddressableCacheClearHandle[i] = false;
                _pendingAddressableCacheClearCount = math.max(0, _pendingAddressableCacheClearCount - 1);
            }
#endif
        }

        private void ReleaseAllChunks()
        {
            if (chunkDefinitions == null)
                return;

            for (int i = 0; i < chunkDefinitions.Length; i++)
            {
                DespawnChunkInstances(i);
                ReleaseChunkHandles(i);
                ResetChunkRuntimeStateAfterRelease(i);
            }

            DrainRuntimeQueuesAfterReleaseAll();
            ReleasePendingAddressablesCacheClearHandles();
            ClearActiveImpostors();
            _forceResidencyEvaluation = true;
            _stateDiagnosticsDirty = true;
            WriteTelemetrySample(0L, TelemetryReleaseAllResetFlag);
        }

        private void ResetChunkRuntimeStateAfterRelease(int index)
        {
            if (_loadRequestQueuedByChunk != null && (uint)index < (uint)_loadRequestQueuedByChunk.Length)
                _loadRequestQueuedByChunk[index] = false;
            if (_evictRequestQueuedByChunk != null && (uint)index < (uint)_evictRequestQueuedByChunk.Length)
                _evictRequestQueuedByChunk[index] = false;

            if (_chunkIdsByDefinitionIndex == null ||
                (uint)index >= (uint)_chunkIdsByDefinitionIndex.Length)
            {
                return;
            }

            long chunkId = _chunkIdsByDefinitionIndex[index];
            if (chunkId == 0L || !TryGetChunkDefinitionIndex(chunkId, out _))
                return;

            ChunkState resetState = chunkDefinitions[index].pinned ? ChunkState.Pinned : ChunkState.Unloaded;
            SetChunkState(chunkId, resetState);
        }

        private void DrainRuntimeQueuesAfterReleaseAll()
        {
            ClearLoadRequestQueue();
            ClearResidencyDecisionBuffer();

            _pendingLoadRequestCount = 0;
            _loadRequestReadIndex = 0;
            _loadRequestWriteIndex = 0;
            _loadDispatchFrame = -1;
            _loadDispatchBudgetTokens = 0f;
            _debugPendingLoadRequests = 0;
            _deferredEvictCount = 0;
        }

        private void ClearActiveImpostors()
        {
            if (TryResolveActiveImpostorBuffers(
                    out NativeArray<float4x4> activeImpostors,
                    out NativeArray<int> impostorTypes,
                    out NativeArray<long> chunkIds,
                    out NativeArray<float> spawnTimes,
                    out NativeArray<float3> centers,
                    out NativeArray<float3> sizes,
                    out NativeArray<uint> flags,
                    out NativeArray<StreamingHlodImpostorPoint> cartographyPoints,
                    out NativeArray<int> activeCount,
                    out NativeArray<int> fadeOutCount))
            {
                int count = math.min(_activeImpostorCount, activeImpostors.Length);
                for (int i = 0; i < count; i++)
                {
                    activeImpostors[i] = default;
                    impostorTypes[i] = 0;
                    chunkIds[i] = 0L;
                    spawnTimes[i] = 0f;
                    centers[i] = default;
                    sizes[i] = default;
                    flags[i] = 0u;
                    cartographyPoints[i] = default;
                }

                activeCount[0] = 0;
                fadeOutCount[0] = 0;
            }

            _activeImpostorCount = 0;
            _activeImpostorFadeOutCount = 0;
            _activeImpostorVersion++;
            _activeImpostorPointVersion++;
            _publishedActiveImpostorVersion = 0u;
            _activeImpostorGpuDirty = true;
            _debugActiveImpostorLod2Chunks = 0;
            IStreamingHlodMatrixRenderer renderer = ResolveLod2ImpostorRenderer();
            if (renderer != null)
                renderer.ClearBinding();
        }

        private void ReleasePendingAddressablesCacheClearHandles()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_hasAddressableCacheClearHandle == null || _addressableCacheClearHandles == null)
                return;

            int count = math.min(_hasAddressableCacheClearHandle.Length, _addressableCacheClearHandles.Length);
            int retainedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!_hasAddressableCacheClearHandle[i])
                    continue;

                AsyncOperationHandle<bool> handle = _addressableCacheClearHandles[i];
                if (handle.IsValid() && !TryReleaseExternalAddressableFault(handle))
                {
                    retainedCount++;
                    continue;
                }

                _addressableCacheClearHandles[i] = default;
                _hasAddressableCacheClearHandle[i] = false;
            }

            _pendingAddressableCacheClearCount = retainedCount;
#endif
        }

        private void DrainAupShiftSignals()
        {
            if (!reactToAupShiftSignals)
                return;

            bool sawShift = false;
            float3 totalShift = default;
            uint lastShiftFrame = _lastAppliedAupShiftFrameId;
            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shiftSignals.Length; i++)
            {
                AupShiftSignal signal = shiftSignals[i];
                if (!IsNewAupShift(signal.ShiftFrameId, _lastAppliedAupShiftFrameId) ||
                    !math.all(math.isfinite(signal.ShiftMeters)))
                {
                    continue;
                }

                _debugLastAupShiftFrameId = signal.ShiftFrameId;
                totalShift += signal.ShiftMeters;
                if (IsNewAupShift(signal.ShiftFrameId, lastShiftFrame))
                    lastShiftFrame = signal.ShiftFrameId;
                sawShift = true;
            }

            if (sawShift)
            {
                ApplyActiveImpostorAupShift(totalShift, lastShiftFrame);
                _lastAppliedAupShiftFrameId = lastShiftFrame;
                _forceResidencyEvaluation = true;
                WriteTelemetrySample(0L, TelemetryShiftFlag);
            }
        }

        private static bool IsNewAupShift(uint shiftFrameId, uint lastAppliedFrameId)
        {
            return shiftFrameId != 0u &&
                   shiftFrameId != lastAppliedFrameId &&
                   unchecked(shiftFrameId - lastAppliedFrameId) < 0x80000000u;
        }

        private void UpdateChunkFade(float deltaTime)
        {
            if (!_fadeActive)
                return;

            _fadeTimer += math.max(0f, deltaTime);
            float fade01 = math.saturate(_fadeTimer * ChunkFadeSecondsRcp);
            QueueChunkFadeMask(fade01);
            if (fade01 >= 1f)
                _fadeActive = false;
        }

        private void StartFade()
        {
            _fadeTimer = 0f;
            _fadeActive = true;
            QueueChunkFadeMask(0f);
        }

        private void QueueChunkFadeMask(float fade01)
        {
            _pendingChunkFadeMask = math.saturate(fade01);
            _chunkFadeMaskDirty = true;
        }

        private void FlushQueuedChunkFadeMask()
        {
            if (!_chunkFadeMaskDirty)
                return;

            _chunkFadeMaskDirty = false;
            Shader.SetGlobalFloat(_chunkFadeMaskId, _pendingChunkFadeMask);
        }

        private void AdvanceChunkResidencyRuntimeClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _chunkResidencyRuntimeSeconds = math.min(ChunkResidencyRuntimeClockMaxSeconds, _chunkResidencyRuntimeSeconds + deltaTime);
        }

        private float ResolveChunkResidencyRuntimeSeconds()
        {
            return _chunkResidencyRuntimeSeconds;
        }

        private void FlushAsyncUploadBudgetPolicySlow()
        {
            if (!applyAsyncUploadBudget)
                return;

            float smooth = ResolveAsyncUploadEffectiveQuality01();
            int uploadBufferSize = math.clamp((int)math.round(math.lerp(64f, 256f, smooth)), 64, 256);
            int uploadTimeSlice = math.clamp((int)math.ceil(math.lerp(1f, 4f, smooth)), 1, 4);
            int budgetHash = (uploadBufferSize << 8) ^ uploadTimeSlice;
            if (_activeAsyncUploadBudgetHash == budgetHash)
                return;

            _activeAsyncUploadBudgetHash = budgetHash;
            QualitySettings.asyncUploadBufferSize = uploadBufferSize;
            QualitySettings.asyncUploadTimeSlice = uploadTimeSlice;

            QualitySettings.asyncUploadPersistentBuffer = true;
        }

        private float ResolveAsyncUploadEffectiveQuality01()
        {
            float quality = ResolveSmoothGlobalQualityWeight01();
            float pressure = ResolveAsyncUploadPressure01();
            float pressureCollapse = math.smoothstep(0.55f, 0.98f, pressure);
            return math.saturate(math.lerp(quality, 0f, pressureCollapse));
        }

        private float ResolveAsyncUploadPressure01()
        {
            IVramPressureReadModel pressure = _vramPressure;
            if (pressure != null && pressure.HasSample)
            {
                float factor = pressure.PressureFactor;
                return math.saturate(math.select(0f, factor, math.isfinite(factor)));
            }

            IVramBudgetReadModel monitor = _vramMonitor;
            if (monitor == null)
                return 0f;

            return monitor.PressureStateCode == VramPressureStateCodes.Critical
                ? 1f
                : monitor.PressureStateCode == VramPressureStateCodes.Warning ? 0.75f : 0f;
        }

        private bool TryCapturePlayerMotionSnapshot(out AbsoluteUniversePosition playerAup, out float3 velocity)
        {
            velocity = default;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (IsPlayerRuntimeContextBound(runtimeContext))
            {
                return TryCapturePlayerMotionSnapshot(runtimeContext, out playerAup, out velocity);
            }

            playerAup = default;
            return false;
        }

        private static bool TryCapturePlayerMotionSnapshot(
            IPlayerRuntimeContext runtimeContext,
            out AbsoluteUniversePosition playerAup,
            out float3 velocity)
        {
            playerAup = default;
            velocity = default;
            if (!IsPlayerRuntimeContextBound(runtimeContext))
                return false;

            bool hasPoseAup = false;
            AbsoluteUniversePosition poseAup = default;
            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot) &&
                (poseSnapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                poseAup = poseSnapshot.Aup;
                hasPoseAup = poseAup.IsFinite();
            }

            bool hasMovementAup = false;
            AbsoluteUniversePosition movementAup = default;
            if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                velocity = movementState.Velocity;
                if (!IsFinite(velocity))
                    velocity = default;

                movementAup = movementState.PredictedAup;
                hasMovementAup = movementAup.IsFinite();
            }

            if (hasPoseAup)
            {
                playerAup = poseAup;
                return true;
            }

            if (!hasMovementAup)
                return false;

            playerAup = movementAup;
            return true;
        }

        private bool PredictiveStreamingPausedNow =>
            IsAdrenalinePurgeActive ||
            _externalPredictiveSuspended ||
            _transportPredictivePauseActive ||
            (suspendPredictiveStreamingInHabitat && (_habitatPredictivePauseActive || _habitatTransitionPauseFrames > 0));

        private bool IsAdrenalinePurgeActive =>
            _adrenalinePurgeUntilTime > 0f && (float)RuntimeNowSeconds() < _adrenalinePurgeUntilTime;

        private void DrainMetabolismSignals()
        {
            ReadOnlySpan<MemoryPressureSignal> pressureSignals = SignalBus<MemoryPressureSignal>.GetFrameSnapshot();
            for (int i = 0; i < pressureSignals.Length; i++)
            {
                MemoryPressureSignal signal = pressureSignals[i];
                if (signal.Severity >= CriticalMemoryPressureSeverity)
                    ActivateAdrenalinePurge(signal.Frame);
            }

            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthIndexSignal signal = healthSignals[i];
                float pressure01 = math.saturate(math.max(signal.Pressure01, 1f - signal.Health01));
                _systemHealthPressure01 = pressure01;
                bool squeeze = _healthRadiusSqueezeActive
                    ? pressure01 > 0.65f
                    : pressure01 > 0.8f;
                if (squeeze != _healthRadiusSqueezeActive)
                {
                    _healthRadiusSqueezeActive = squeeze;
                    _debugHealthRadiusSqueezed = squeeze;
                    _forceResidencyEvaluation = true;
                }

                if (signal.State >= SystemHealthIndexSignal.StateCritical ||
                    (signal.Flags & SystemHealthIndexSignal.FlagAdrenaline) != 0)
                {
                    ActivateAdrenalinePurge(signal.Frame);
                }
            }

            ReadOnlySpan<ResidencySectorDehydratedSignal> dehydratedSignals = SignalBus<ResidencySectorDehydratedSignal>.GetFrameSnapshot();
            for (int i = 0; i < dehydratedSignals.Length; i++)
                ForcePurgeDehydratedSector(in dehydratedSignals[i]);
        }

        private void ActivateAdrenalinePurge(uint frame)
        {
            if (frame != 0u && _lastAdrenalineSignalFrame == frame)
                return;

            _lastAdrenalineSignalFrame = frame;
            float until = (float)RuntimeNowSeconds() + AdrenalinePurgeSeconds;
            if (until > _adrenalinePurgeUntilTime)
                _adrenalinePurgeUntilTime = until;

            _adrenalinePoolTrimPending = true;

            _forceResidencyEvaluation = true;
        }

        private void FlushAdrenalinePoolTrim()
        {
            if (!_adrenalinePoolTrimPending)
                return;

            _adrenalinePoolTrimPending = false;
            if (TryResolveCachedObjectPool(out IObjectPoolService pool))
                pool.TrimInactivePoolsForMemoryPressure(0.5f);
        }

        private void ForcePurgeDehydratedSector(in ResidencySectorDehydratedSignal signal)
        {
            if (signal.ChunkId == 0L ||
                (signal.Flags & ResidencySectorDehydratedSignal.FlagPinned) != 0 ||
                !TryGetChunkDefinitionIndex(signal.ChunkId, out int index))
            {
                return;
            }

            if (TryGetChunkState(signal.ChunkId, out ChunkState state) &&
                HasFlag(state, ChunkState.Pinned))
            {
                return;
            }

            ReleaseChunkHandles(index, clearAddressableCache: true);
        }

        private byte ConvertChunkStateToResidencyFlags(ChunkState state, int index)
        {
            byte flags = 0;
            if (HasFlag(state, ChunkState.Resident))
                flags |= ChunkResidencyStateFlags.Hydrated;
            if (HasFlag(state, ChunkState.Loading))
                flags |= ChunkResidencyStateFlags.Loading;
            if (HasFlag(state, ChunkState.Staged))
                flags |= ChunkResidencyStateFlags.Staged;
            if (HasFlag(state, ChunkState.Pinned))
                flags |= ChunkResidencyStateFlags.Pinned;
            if (HasFlag(state, ChunkState.LOD1) || HasFlag(state, ChunkState.Pinned))
                flags |= ChunkResidencyStateFlags.LOD2Impostor;
            if (ShouldRetainThreatResidency(index))
                flags |= ChunkResidencyStateFlags.ThreatOverride;
            return flags;
        }

        private bool ShouldRetainThreatResidency(int index)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return false;

            IAmbientBiotaService service = _ambientBiotaService;

            if (service != null && service.IsInitialized && HasActiveAmbientBiotaInsideChunk(service, index))
                return true;

            if (streamingProfile == null)
                return false;

            WorldChunkStreamingProfile.LayerProfile largeThreats = streamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.LargeThreats);
            return !largeThreats.useChunkResidency && largeThreats.useVisualProxyLayer && largeThreats.useFullSimulationNearPlayer && service != null;
        }

        private bool HasActiveAmbientBiotaInsideChunk(IAmbientBiotaService service, int index)
        {
            NativeArray<AbsoluteUniversePosition>.ReadOnly biotaAups = service.BiotaAups;
            NativeArray<AmbientBiotaState>.ReadOnly biotaStates = service.BiotaStates;
            int count = math.min(service.Capacity, math.min(biotaAups.Length, biotaStates.Length));
            if (count <= 0)
                return false;

            if (!TryReadNativeChunkCenter(index, out AbsoluteUniversePositionBlit chunkCenterAup))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            ChunkDefinition definition = chunkDefinitions[index];
            double3 chunkCenter = ToAbsoluteDouble3(in chunkCenterAup);
            double radius = math.max(32d, definition.chunkSizeMeters > 0 ? definition.chunkSizeMeters : ResolveEffectiveUnloadRadiusMeters());
            double radiusSq = radius * radius;
            for (int i = 0; i < count; i++)
            {
                AmbientBiotaState state = biotaStates[i];
                if ((state.StateFlags & AmbientBiotaState.FlagActive) == 0u)
                    continue;

                double3 delta = ToAbsoluteDouble3(biotaAups[i]) - chunkCenter;
                if (math.lengthsq(delta) <= radiusSq)
                    return true;
            }

            return false;
        }

        private bool TryEnqueueDehydrationMetadata(long chunkId, ChunkState state)
        {
            IAsyncPersistenceService persistence = _asyncPersistenceService;
            if (!IsAsyncPersistenceUsable(persistence) ||
                !TryResolveWorldStreamingVaultBuffer(
                    in _dehydrationMetadataPayloadHandle,
                    DehydrationMetadataVaultBufferId,
                    DehydrationMetadataPayloadBytes,
                    out NativeArray<byte> dehydrationMetadataPayload))
                return false;

            WriteInt64LE(dehydrationMetadataPayload, 0, chunkId);
            WriteUInt32LE(dehydrationMetadataPayload, 8, Hecton8.Core.SystemDispatcher.CurrentFrameId);
            WriteUInt32LE(dehydrationMetadataPayload, 12, unchecked((uint)(byte)state));
            return persistence.TryEnqueueChunkPageWrite(
                chunkId,
                H8WorldPagePayloadTypes.ChunkDehydratedMetadata,
                dehydrationMetadataPayload,
                DehydrationMetadataPayloadBytes,
                StreamingDirectorSourceHash,
                Hecton8.Core.SystemDispatcher.CurrentFrameId);
        }

        private static void WriteInt64LE(NativeArray<byte> payload, int offset, long value)
        {
            ulong bits = unchecked((ulong)value);
            for (int i = 0; i < 8; i++)
                payload[offset + i] = (byte)(bits >> (i * 8));
        }

        private static void WriteUInt32LE(NativeArray<byte> payload, int offset, uint value)
        {
            for (int i = 0; i < 4; i++)
                payload[offset + i] = (byte)(value >> (i * 8));
        }

        private void RecordAddressablesRequestDto(int index, long chunkId, string address)
        {
            NativeArray<AddressablesRequestDTO> requests = ResolveAddressablesRequestDtos();
            if (!requests.IsCreated || (uint)index >= (uint)requests.Length)
                return;

            uint hash = StableHash(address, chunkId);
            MockAssetHandle mock = MockAddressables.LoadAsync(hash, index, Hecton8.Core.SystemDispatcher.CurrentFrameId, 1);
            requests[index] = new AddressablesRequestDTO
            {
                AssetHash = mock.AssetHash,
                TargetChunkIndex = index,
                HandlePtr = ((ulong)mock.StartFrame << 32) | mock.PayloadPages
            };
        }

        private uint ResolveAddressableAssetHash(int index)
        {
            if (chunkDefinitions == null || (uint)index >= (uint)chunkDefinitions.Length)
                return 0u;

            long chunkId = 0L;
            if (_chunkIdsByDefinitionIndex != null && (uint)index < (uint)_chunkIdsByDefinitionIndex.Length)
                chunkId = _chunkIdsByDefinitionIndex[index];

            return StableHash(ResolveUsableAddressableAddress(in chunkDefinitions[index]), chunkId);
        }

        private static uint StableHash(string value, long fallback)
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = value.Trim();
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }
            else
            {
                hash ^= unchecked((uint)fallback);
                hash *= 16777619u;
                hash ^= unchecked((uint)(fallback >> 32));
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private bool ShouldActivateAdditiveScene(int index)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return false;

            if (!TryCapturePlayerMotionSnapshot(out AbsoluteUniversePosition playerAup, out _))
                return false;

            if (!TryReadNativeChunkCenter(index, out AbsoluteUniversePositionBlit chunkCenter))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            ChunkDefinition definition = chunkDefinitions[index];
            double3 deltaD = ToAbsoluteDouble3(in chunkCenter) - ToAbsoluteDouble3(in playerAup);
            float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
            float distSq = math.lengthsq(delta);
            if (!math.isfinite(distSq))
                return false;

            float threshold = math.max(ReadRuntimeTuning().PhysicalHydrationRadiusMeters, math.max(32f, definition.chunkSizeMeters));
            float thresholdSq = threshold * threshold;
            if (distSq > thresholdSq)
                return false;

            float nearSq = thresholdSq * 0.16f;
            return distSq <= nearSq || _fadeActive || _proxyFallbackByStorageDebt || PredictiveStreamingPausedNow;
        }

        private int ResolveHydrationCopyBudgetBytes()
        {
            NativeArray<WorldStreamingRuntimeTuning> tuning = ResolveStreamingTuning();
            if (tuning.IsCreated && tuning.Length > 0)
                return math.max(1024, tuning[0].HydrationCopyBudgetBytes);

            return math.max(1024, hydrationCopyBudgetBytes);
        }

        private long EstimateAddressableChunkBytes(int index)
        {
            if (chunkDefinitions == null || (uint)index >= (uint)chunkDefinitions.Length)
                return 0L;

            ChunkDefinition definition = chunkDefinitions[index];
            int chunkSize = math.max(1, definition.chunkSizeMeters);
            long estimate = 64L * 1024L;
            estimate += (long)chunkSize * chunkSize * 4L;
            estimate += EstimatePrefabSetBytes(definition.prefabDependencies, math.max(1, definition.warmupCountPerPrefab));
            estimate += EstimatePrefabSetBytes(definition.predictivePrewarmPrefabs, 1);
            estimate += EstimatePrefabSetBytes(definition.activationPrefabs, 1);
            if (definition.useAdditiveScene && HasUsableAdditiveSceneName(in definition))
                estimate += 512L * 1024L;

            return math.max(0L, estimate);
        }

        private static long EstimatePrefabSetBytes(GameObject[] prefabs, int multiplier)
        {
            if (prefabs == null || prefabs.Length == 0)
                return 0L;

            int safeMultiplier = math.max(1, multiplier);
            long total = 0L;
            for (int i = 0; i < prefabs.Length; i++)
                total += (long)EstimateHydrationApplyBytes(prefabs[i]) * safeMultiplier;

            return total;
        }

        private static int EstimateHydrationApplyBytes(GameObject prefab)
        {
            if (prefab == null)
                return 0;

            int childWeight = math.min(16, prefab.transform.childCount + 1);
            return math.max(16 * 1024, childWeight * 32 * 1024);
        }

        private void CopyHydrationApplyRecordToVault(int chunkIndex, long chunkId, int prefabIndex, GameObject prefab, int estimatedBytes)
        {
            int safeChunkIndex = math.max(0, chunkIndex);
            if (!TryResolveWorldStreamingVaultBuffer(
                    in _hydrationApplyRecordsHandle,
                    HydrationApplyRecordVaultBufferId,
                    math.max(1, maxChunkCount),
                    out NativeArray<ChunkHydrationApplyRecord> hydrationApplyRecords) ||
                (uint)safeChunkIndex >= (uint)hydrationApplyRecords.Length)
                return;

            ChunkHydrationApplyRecord record = new ChunkHydrationApplyRecord
            {
                ChunkId = chunkId,
                PrefabStableHash = ((ulong)ComputeSectorHash(chunkId) << 32) | unchecked((uint)prefabIndex),
                TimeSeconds = RuntimeNowSeconds(),
                ChunkIndex = chunkIndex,
                PrefabIndex = prefabIndex,
                EstimatedBytes = estimatedBytes,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = (byte)(prefab != null ? 1 : 0),
                _pad0 = 0,
                _pad1 = 0,
                _pad2 = _hydrationApplySequence++
            };

            hydrationApplyRecords[safeChunkIndex] = record;
        }

        private void RecordHydrationApplySlice(long startTimestamp, long chunkId)
        {
            float ms = (float)((Stopwatch.GetTimestamp() - startTimestamp) * _StopwatchMillisecondsPerTick);
            if (!math.isfinite(ms) || ms < 0f)
                return;

            _lastHydrationApplyMs = ms;
            _debugLastHydrationApplyMs = ms;
            if (ms > 1.5f)
                DumpTelemetry(TelemetryHydrationCopySpikeFlag);
        }

        private static uint ComputeSectorHash(long chunkId)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, unchecked((uint)chunkId));
            hash = MixHash(hash, unchecked((uint)(chunkId >> 32)));
            return hash != 0u ? hash : 1u;
        }

        private bool ResolvePredictiveVramAbortState()
        {
            long usedBytes = VRAMBudgetTracker.EstimatedVRAMBytes;
            IVramBudgetReadModel monitor = _vramMonitor;
            if (monitor != null && monitor.TotalVRAMBytes > usedBytes)
                usedBytes = monitor.TotalVRAMBytes;

            long abortBytes = ResolvePredictiveVramAbortThresholdBytes();
            long resumeBytes = ResolvePredictiveVramResumeThresholdBytes(abortBytes);
            return _predictiveVramAborted
                ? usedBytes >= resumeBytes
                : usedBytes >= abortBytes;
        }

        private long ResolvePredictiveVramAbortThresholdBytes()
        {
            long ceilingBytes = _predictiveVramCeilingBytes > 0L
                ? _predictiveVramCeilingBytes
                : PredictiveVramAbortBytes;
            float quality = ResolveSmoothGlobalQualityWeight01();
            long scaledBytes = (long)Math.Round(PredictiveVramAbortBytes + ((ceilingBytes - PredictiveVramAbortBytes) * (double)quality));
            return Math.Max(PredictiveVramMinimumThresholdBytes, Math.Min(ceilingBytes, scaledBytes));
        }

        private static long ResolvePredictiveVramResumeThresholdBytes(long abortBytes)
        {
            long ratioBytes = (long)Math.Round(abortBytes * (double)PredictiveVramResumeRatio);
            long hysteresisBytes = Math.Max(64L * 1024L * 1024L, abortBytes - ratioBytes);
            return Math.Max(PredictiveVramMinimumThresholdBytes, abortBytes - hysteresisBytes);
        }

        private static long ComputePredictiveVramCeilingBytesCold()
        {
            HardwareTierDetector.EnsureInitialized();
            if (HardwareTierDetector.SharedMemoryModeActive)
            {
                long sharedBudgetBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
                return sharedBudgetBytes > 0L
                    ? Math.Max(PredictiveVramMinimumThresholdBytes, sharedBudgetBytes)
                    : PredictiveVramAbortBytes;
            }

            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            if (graphicsMemoryMb <= 0)
                return PredictiveVramAbortBytes;

            long reportedBytes = (long)graphicsMemoryMb * 1024L * 1024L;
            long ceilingBytes = Math.Max(PredictiveVramAbortBytes, reportedBytes - PredictiveVramReservedHeadroomBytes);
            return Math.Min(PredictiveVramVisualOverkillCeilingBytes, ceilingBytes);
        }

        private float ResolvePredictionDistanceMeters(float3 velocity)
        {
            float speedSq = math.lengthsq(velocity);
            if (speedSq <= 0.0001f)
                return 0f;

            float speed = speedSq * math.rsqrt(speedSq);
            float maxDistance = math.lerp(50f, 200f, ResolveSmoothGlobalQualityWeight01());
            return math.min(maxDistance, speed * PredictiveLookaheadSeconds * math.max(0f, predictiveVelocityStretch));
        }

        private float ResolveEffectiveLoadRadiusMeters()
        {
            float unload = ResolveEffectiveUnloadRadiusMeters();
            float lowLoad = unload * 0.85f;
            float configuredLoad = math.min(loadRadiusMeters * ResolveHealthRadiusScale(), unload - 1f);
            float load = math.lerp(lowLoad, configuredLoad, ResolveSmoothGlobalQualityWeight01());

            return math.max(1f, math.min(load, unload - 1f));
        }

        private float ResolveEffectiveUnloadRadiusMeters()
        {
            float radius = math.lerp(
                SurvivalUnloadRadiusMeters,
                math.max(unloadRadiusMeters, VisualOverkillUnloadRadiusMeters),
                ResolveSmoothGlobalQualityWeight01());

            return math.max(2f, radius * ResolveHealthRadiusScale());
        }

        private static float ResolveSmoothGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            return q * q * (3f - 2f * q);
        }

        private float ResolveHealthRadiusScale()
        {
            return _healthRadiusSqueezeActive ? 0.6f : 1f;
        }

        private float ResolveTailUnloadRadiusMeters(
            float predictionDistanceMeters,
            float effectiveLoadRadiusMeters,
            float effectiveUnloadRadiusMeters)
        {
            if (predictionDistanceMeters <= 0f)
                return effectiveUnloadRadiusMeters;

            float hysteresisFloor = effectiveLoadRadiusMeters * 1.05f;
            float shrink = math.min(effectiveUnloadRadiusMeters - hysteresisFloor, predictionDistanceMeters * 0.6f);
            return math.max(hysteresisFloor, effectiveUnloadRadiusMeters - math.max(0f, shrink));
        }

        private static AbsoluteUniversePosition BuildProjectedAup(in AbsoluteUniversePosition playerAup, float3 velocity, float predictionDistanceMeters)
        {
            float speedSq = math.lengthsq(velocity);
            if (predictionDistanceMeters <= 0f || speedSq <= 0.0001f)
                return playerAup;

            float invSpeed = math.rsqrt(speedSq);
            double3 playerAbs = ToAbsoluteDouble3(in playerAup);
            double3 direction = new double3(velocity.x * invSpeed, velocity.y * invSpeed, velocity.z * invSpeed);
            return AbsoluteUniversePosition.FromAbsolutePosition(playerAbs + (direction * predictionDistanceMeters));
        }

        private byte ResolveLoadFlagsForChunk(long chunkId)
        {
            if (_lastPredictionDistanceMeters <= 0f ||
                !TryGetChunkStorageIndex(chunkId, out int index) ||
                !TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                (uint)index >= (uint)chunkCenters.Length)
                return 0;

            AbsoluteUniversePositionBlit center = chunkCenters[index];
            AbsoluteUniversePositionBlit playerAup = _lastPlayerAup;
            double distSq = DistanceSq(in center, in playerAup);
            float effectiveLoadRadiusMeters = ResolveEffectiveLoadRadiusMeters();
            double loadRadiusSq = (double)effectiveLoadRadiusMeters * effectiveLoadRadiusMeters;
            return distSq > loadRadiusSq ? LoadRequestFlagPredictive : (byte)0;
        }

        private float ResolveProjectedDistanceSq(long chunkId)
        {
            if (!TryGetChunkStorageIndex(chunkId, out int index) ||
                !TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                (uint)index >= (uint)chunkCenters.Length)
                return float.MaxValue;

            AbsoluteUniversePositionBlit center = chunkCenters[index];
            AbsoluteUniversePositionBlit projectedAup = _lastProjectedAup;
            double distSq = DistanceSq(in center, in projectedAup);
            return (float)math.min(distSq, float.MaxValue);
        }

        private bool ShouldClearAddressableCacheOnEvict(long chunkId)
        {
            if (_lastPredictionDistanceMeters <= 0f ||
                !TryGetChunkStorageIndex(chunkId, out int index) ||
                !TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                (uint)index >= (uint)chunkCenters.Length)
                return false;

            float speedSq = math.lengthsq(_lastPlayerVelocity);
            if (speedSq <= 0.0001f)
                return false;

            float invSpeed = math.rsqrt(speedSq);
            double3 direction = new double3(_lastPlayerVelocity.x * invSpeed, _lastPlayerVelocity.y * invSpeed, _lastPlayerVelocity.z * invSpeed);
            AbsoluteUniversePositionBlit center = chunkCenters[index];
            AbsoluteUniversePositionBlit playerAup = _lastPlayerAup;
            double3 delta = ToAbsoluteDouble3(in center) - ToAbsoluteDouble3(in playerAup);
            double behind = math.dot(delta, direction);
            float effectiveLoadRadiusMeters = ResolveEffectiveLoadRadiusMeters();
            if (behind >= -effectiveLoadRadiusMeters)
                return false;

            double distSq = math.lengthsq(delta);
            double loadRadiusSq = (double)effectiveLoadRadiusMeters * effectiveLoadRadiusMeters;
            return distSq > loadRadiusSq;
        }

        private void UpdateStreamerStressMetric()
        {
            RefreshStateDiagnosticsIfDirty();
            float queuePressure = math.saturate(_pendingLoadRequestCount * _loadQueueCapacityRcp);
            float residentPressure = math.saturate(_debugResidentChunks * _maxChunkCountRcp);
            float speedPressure = math.saturate(math.lengthsq(_lastPlayerVelocity) * StreamerStressSpeedSqRcp);
            float suspendPressure = (_predictiveVramAborted || PredictiveStreamingPausedNow) ? 1f : 0f;
            _debugStreamerStress01 = math.saturate((queuePressure * 0.45f) + (residentPressure * 0.2f) + (speedPressure * 0.2f) + (suspendPressure * 0.15f));
            _debugPredictiveSuspended = _predictiveVramAborted || PredictiveStreamingPausedNow;
        }

        private bool TryCapturePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (IsPlayerRuntimeContextBound(runtimeContext))
            {
                return TryCapturePlayerAupSnapshot(runtimeContext, out playerAup);
            }

            playerAup = default;
            return false;
        }

        private static bool TryCapturePlayerAupSnapshot(
            IPlayerRuntimeContext runtimeContext,
            out AbsoluteUniversePosition playerAup)
        {
            if (!TryCapturePlayerMotionSnapshot(runtimeContext, out playerAup, out _))
            {
                playerAup = default;
                return false;
            }

            return true;
        }

        private void ClearLoadingFlag(long chunkId)
        {
            if (!TryGetChunkState(chunkId, out ChunkState state))
                return;

            state &= unchecked((ChunkState)~(byte)ChunkState.Loading);
            SetChunkState(chunkId, state);
        }

        private void SetChunkState(long chunkId, ChunkState state)
        {
            if (_residencyJobScheduled)
            {
                _forceResidencyEvaluation = true;
                return;
            }

            if (!TryFindChunkStateSlot(chunkId, out int slotIndex, out ChunkStateSlotDTO slot) ||
                !TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> slots) ||
                (uint)slotIndex >= (uint)slots.Length)
            {
                return;
            }

            slot.State = unchecked((byte)state);
            slot.Occupied = 1;
            slots[slotIndex] = slot;

            SyncChunkResidencyDtoState(slot.StorageIndex, slot.DefinitionIndex, state);
            _stateDiagnosticsDirty = true;
        }

        private void SyncChunkResidencyDtoState(int storageIndex, int definitionIndex, ChunkState state)
        {
            NativeArray<ChunkResidencyDTO> residencyDtos = ResolveChunkResidencyDtos();
            if (!residencyDtos.IsCreated || (uint)storageIndex >= (uint)residencyDtos.Length)
            {
                return;
            }

            ChunkResidencyDTO dto = residencyDtos[storageIndex];
            dto.StateFlags = ConvertChunkStateToResidencyFlags(state, definitionIndex);
            dto.Priority = HasFlag(state, ChunkState.HighPriority) ? (byte)3 : dto.Priority;
            dto._pad0 = 0;
            dto._pad1 = 0u;
            residencyDtos[storageIndex] = dto;
        }

        private void PublishSectorHydratedSignal(int index, long chunkId, ChunkState state)
        {
            if (!TryBuildChunkSignalPayload(index, state, out AbsoluteUniversePosition centerAup, out ushort radiusQ, out byte flags))
                return;

            SignalBus<ResidencySectorHydratedSignal>.TryPushTracked(new ResidencySectorHydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                RadiusMetersQ = radiusQ,
                Flags = flags,
                ResidencyState = unchecked((byte)state)
            }, ref _signalPushDropCount);
        }

        private void PublishSectorDehydratedSignal(int index, long chunkId, ChunkState state)
        {
            if (!TryBuildChunkSignalPayload(index, state, out AbsoluteUniversePosition centerAup, out ushort radiusQ, out byte flags))
                return;

            SignalBus<ResidencySectorDehydratedSignal>.TryPushTracked(new ResidencySectorDehydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                RadiusMetersQ = radiusQ,
                Flags = flags,
                ResidencyState = unchecked((byte)state)
            }, ref _signalPushDropCount);

            SignalBus<ChunkDehydratedSignal>.TryPushTracked(new ChunkDehydratedSignal
            {
                CenterAup = centerAup,
                SectorHash = chunkId,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                RadiusMetersQ = radiusQ,
                Flags = flags,
                ResidencyState = unchecked((byte)state)
            }, ref _signalPushDropCount);
        }

        private bool TryBuildChunkSignalPayload(
            int index,
            ChunkState state,
            out AbsoluteUniversePosition centerAup,
            out ushort radiusMetersQ,
            out byte flags)
        {
            centerAup = default;
            radiusMetersQ = 0;
            flags = 0;
            if (!TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                (uint)index >= (uint)chunkCenters.Length)
                return false;

            centerAup = chunkCenters[index].ToAup();
            int chunkSizeMeters = 0;
            if (chunkDefinitions != null && (uint)index < (uint)chunkDefinitions.Length)
                chunkSizeMeters = chunkDefinitions[index].chunkSizeMeters;

            int radiusMeters = math.max(1, chunkSizeMeters > 0 ? chunkSizeMeters : Mathf.RoundToInt(math.max(1f, loadRadiusMeters)));
            radiusMetersQ = (ushort)math.clamp(radiusMeters, 1, ushort.MaxValue);
            if (HasFlag(state, ChunkState.LOD1))
                flags |= ResidencySectorHydratedSignal.FlagProxyFallback;
            if (HasFlag(state, ChunkState.Pinned))
                flags |= ResidencySectorHydratedSignal.FlagPinned;
            return true;
        }

        private void DrainHlodSwapSignals()
        {
            if (!TryResolveWorldStreamingVaultBuffer(in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<float4x4> activeImpostors) ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorCountHandle, ActiveImpostorCountVaultBufferId, 1, out NativeArray<int> activeCount))
            {
                return;
            }
            _ = activeImpostors;
            _ = activeCount;

            ReadOnlySpan<ResidencySectorDehydratedSignal> dehydratedSignals =
                SignalBus<ResidencySectorDehydratedSignal>.GetFrameSnapshot();
            for (int i = 0; i < dehydratedSignals.Length; i++)
                TryAppendHlodImpostor(in dehydratedSignals[i]);

            ReadOnlySpan<ResidencySectorHydratedSignal> hydratedSignals =
                SignalBus<ResidencySectorHydratedSignal>.GetFrameSnapshot();
            for (int i = 0; i < hydratedSignals.Length; i++)
                TryRemoveHlodImpostor(hydratedSignals[i].ChunkId, permanentDestroy: false);
        }

        private void TryAppendHlodImpostor(in ResidencySectorDehydratedSignal signal)
        {
            if (signal.ChunkId == 0L ||
                !TryGetChunkDefinitionIndex(signal.ChunkId, out int index) ||
                !TryBuildChunkImpostorPayload(index, signal.ChunkId, out float3 center, out float3 size, out int type, out uint flags))
            {
                return;
            }

            RunHlodSwapJob(signal.ChunkId, center, size, type, flags | ActiveImpostorAudioMutedFlag, add: true);
            MuteAssociatedAcousticPortals(signal.ChunkId, muted: true);
        }

        private void TryRemoveHlodImpostor(long chunkId, bool permanentDestroy)
        {
            if (chunkId == 0L ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<float4x4> activeImpostors))
            {
                return;
            }
            _ = activeImpostors;

            RunHlodSwapJob(
                chunkId,
                default,
                default,
                0,
                permanentDestroy ? (uint)ActiveImpostorPermanentDestroyFlag : (uint)ActiveImpostorFadeOutFlag,
                add: false,
                fadeOut: !permanentDestroy);
            MuteAssociatedAcousticPortals(chunkId, muted: false);
        }

        public void PurgeImpostorForDestroyedChunk(long chunkId)
        {
            TryRemoveHlodImpostor(chunkId, permanentDestroy: true);
        }

        private void RunHlodSwapJob(long chunkId, float3 center, float3 size, int type, uint flags, bool add, bool fadeOut = false)
        {
            if (!TryResolveActiveImpostorBuffers(
                    out NativeArray<float4x4> activeImpostors,
                    out NativeArray<int> impostorTypes,
                    out NativeArray<long> activeImpostorChunkIds,
                    out NativeArray<float> activeImpostorSpawnTimes,
                    out NativeArray<float3> activeImpostorCenters,
                    out NativeArray<float3> activeImpostorSizes,
                    out NativeArray<uint> activeImpostorFlags,
                    out NativeArray<StreamingHlodImpostorPoint> activeImpostorCartographyPoints,
                    out NativeArray<int> activeImpostorCount,
                    out NativeArray<int> activeImpostorFadeOutCount))
            {
                return;
            }

            HlodImpostorSwapJob job = new HlodImpostorSwapJob
            {
                ActiveImpostors = activeImpostors,
                ImpostorTypes = impostorTypes,
                ChunkIds = activeImpostorChunkIds,
                SpawnTimes = activeImpostorSpawnTimes,
                Centers = activeImpostorCenters,
                Sizes = activeImpostorSizes,
                Flags = activeImpostorFlags,
                CartographyPoints = activeImpostorCartographyPoints,
                ActiveCount = activeImpostorCount,
                FadeOutCount = activeImpostorFadeOutCount,
                ChunkId = chunkId,
                Center = center,
                Size = size,
                SpawnTimeSeconds = ResolveChunkResidencyRuntimeSeconds(),
                ImpostorType = type,
                ImpostorFlags = flags,
                FadeOutFlag = ActiveImpostorFadeOutFlag,
                Operation = add ? (byte)0 : (fadeOut ? (byte)2 : (byte)1)
            };

            job.Execute();
            _activeImpostorCount = math.clamp(activeImpostorCount[0], 0, activeImpostors.Length);
            _activeImpostorFadeOutCount = math.max(0, activeImpostorFadeOutCount[0]);
            _activeImpostorVersion++;
            _activeImpostorPointVersion++;
            _activeImpostorGpuDirty = true;
            _debugActiveImpostorLod2Chunks = _activeImpostorCount;
            _stateDiagnosticsDirty = true;
        }

        private void CullExpiredHlodFadeouts()
        {
            if (!TryResolveActiveImpostorBuffers(
                    out NativeArray<float4x4> activeImpostors,
                    out NativeArray<int> impostorTypes,
                    out NativeArray<long> activeImpostorChunkIds,
                    out NativeArray<float> activeImpostorSpawnTimes,
                    out NativeArray<float3> activeImpostorCenters,
                    out NativeArray<float3> activeImpostorSizes,
                    out NativeArray<uint> activeImpostorFlags,
                    out NativeArray<StreamingHlodImpostorPoint> activeImpostorCartographyPoints,
                    out NativeArray<int> activeImpostorCount,
                    out NativeArray<int> activeImpostorFadeOutCount) ||
                _activeImpostorFadeOutCount <= 0 ||
                _activeImpostorCount <= 0)
            {
                return;
            }

            int previousCount = _activeImpostorCount;
            int previousFadeOutCount = _activeImpostorFadeOutCount;
            HlodImpostorFadeCullJob job = new HlodImpostorFadeCullJob
            {
                ActiveImpostors = activeImpostors,
                ImpostorTypes = impostorTypes,
                ChunkIds = activeImpostorChunkIds,
                SpawnTimes = activeImpostorSpawnTimes,
                Centers = activeImpostorCenters,
                Sizes = activeImpostorSizes,
                Flags = activeImpostorFlags,
                CartographyPoints = activeImpostorCartographyPoints,
                ActiveCount = activeImpostorCount,
                FadeOutCount = activeImpostorFadeOutCount,
                NowSeconds = ResolveChunkResidencyRuntimeSeconds(),
                FadeOutSeconds = ActiveImpostorFadeOutSeconds,
                FadeOutFlag = ActiveImpostorFadeOutFlag
            };

            job.Execute();
            _activeImpostorCount = math.clamp(activeImpostorCount[0], 0, activeImpostors.Length);
            _activeImpostorFadeOutCount = math.max(0, activeImpostorFadeOutCount[0]);
            _debugActiveImpostorLod2Chunks = _activeImpostorCount;
            if (_activeImpostorCount != previousCount)
            {
                _activeImpostorVersion++;
                _activeImpostorPointVersion++;
                _activeImpostorGpuDirty = true;
                _stateDiagnosticsDirty = true;
            }
            else if (_activeImpostorFadeOutCount != previousFadeOutCount)
            {
                _activeImpostorPointVersion++;
                _stateDiagnosticsDirty = true;
            }
            else if (_activeImpostorFadeOutCount > 0)
            {
                _activeImpostorPointVersion++;
                _stateDiagnosticsDirty = true;
            }
        }

        private void ApplyActiveImpostorAupShift(float3 shiftMeters, uint shiftFrameId)
        {
            int capacity = ResolveStreamingLedgerCapacity();
            if (!TryResolveWorldStreamingVaultBuffer(in _activeImpostorsHandle, ActiveImpostorsVaultBufferId, capacity, out NativeArray<float4x4> activeImpostors) ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorCentersHandle, ActiveImpostorCentersVaultBufferId, capacity, out NativeArray<float3> activeImpostorCenters) ||
                !TryResolveWorldStreamingVaultBuffer(in _activeImpostorCartographyPointsHandle, ActiveImpostorCartographyVaultBufferId, capacity, out NativeArray<StreamingHlodImpostorPoint> activeImpostorCartographyPoints) ||
                _activeImpostorCount <= 0 ||
                !math.all(math.isfinite(shiftMeters)) ||
                math.lengthsq(shiftMeters) <= 0.000001f)
            {
                return;
            }

            HlodImpostorAupShiftJob shiftJob = new HlodImpostorAupShiftJob
            {
                ActiveImpostors = activeImpostors,
                Centers = activeImpostorCenters,
                CartographyPoints = activeImpostorCartographyPoints,
                ShiftMeters = shiftMeters
            };
            for (int i = 0; i < _activeImpostorCount; i++)
                shiftJob.Execute(i);
            _activeImpostorVersion++;
            _activeImpostorPointVersion++;
            _activeImpostorGpuDirty = true;
            _debugLastAupShiftFrameId = shiftFrameId;
        }

        private bool TryBuildChunkImpostorPayload(
            int index,
            long chunkId,
            out float3 center,
            out float3 size,
            out int type,
            out uint flags)
        {
            center = default;
            size = default;
            type = 0;
            flags = HectonChunkImpostorResidency.FlagUseImpostor;

            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return false;

            ChunkDefinition definition = chunkDefinitions[index];
            type = ResolveChunkImpostorType(in definition);
            if (type == 0)
                return false;

            if (!_hasLastPlayerAup)
            {
                DumpHlodTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            AbsoluteUniversePositionBlit playerOrigin = _lastPlayerAup;
            double originX = ToAbsoluteX(in playerOrigin);
            double originY = ToAbsoluteY(in playerOrigin);
            double originZ = ToAbsoluteZ(in playerOrigin);

            if (!TryReadNativeChunkCenter(index, chunkId, out AbsoluteUniversePositionBlit chunkCenter))
            {
                DumpHlodTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            center = new float3(
                (float)(ToAbsoluteX(in chunkCenter) - originX),
                (float)(ToAbsoluteY(in chunkCenter) - originY),
                (float)(ToAbsoluteZ(in chunkCenter) - originZ));

            float chunkSize = definition.chunkSizeMeters > 0
                ? definition.chunkSizeMeters
                : math.max(1f, ResolveEffectiveUnloadRadiusMeters());
            size = new float3(
                math.max(1f, chunkSize),
                math.max(1f, chunkSize),
                math.max(1f, chunkSize));
            if (!math.all(math.isfinite(center)) || !math.all(math.isfinite(size)))
            {
                DumpHlodTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            flags |= HectonChunkImpostorResidency.FlagDitherBlend;
            return true;
        }

        private bool TryBuildChunkScenePosition(int index, long chunkId, out Vector3 position)
        {
            position = default;
            if (!_hasLastPlayerAup ||
                !TryReadNativeChunkCenter(index, chunkId, out AbsoluteUniversePositionBlit chunkCenter))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            AbsoluteUniversePositionBlit playerOrigin = _lastPlayerAup;
            double originX = ToAbsoluteX(in playerOrigin);
            double originY = ToAbsoluteY(in playerOrigin);
            double originZ = ToAbsoluteZ(in playerOrigin);
            float3 local = new float3(
                (float)(ToAbsoluteX(in chunkCenter) - originX),
                (float)(ToAbsoluteY(in chunkCenter) - originY),
                (float)(ToAbsoluteZ(in chunkCenter) - originZ));
            if (!math.all(math.isfinite(local)))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return false;
            }

            position = new Vector3(local.x, local.y, local.z);
            return true;
        }

        private bool TryReadNativeChunkCenter(int index, out AbsoluteUniversePositionBlit center)
        {
            center = default;
            if (!TryResolveWorldStreamingVaultBuffer(in _chunkCentersHandle, ChunkCentersVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<AbsoluteUniversePositionBlit> chunkCenters) ||
                (uint)index >= (uint)chunkCenters.Length)
            {
                return false;
            }

            center = chunkCenters[index];
            return true;
        }

        private bool TryReadNativeChunkCenter(int index, long chunkId, out AbsoluteUniversePositionBlit center)
        {
            if (!TryReadNativeChunkCenter(index, out center))
                return false;

            if (!TryResolveWorldStreamingVaultBuffer(in _chunkIdsHandle, ChunkIdsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<long> chunkIds) ||
                (uint)index >= (uint)chunkIds.Length ||
                chunkIds[index] != chunkId)
            {
                center = default;
                return false;
            }

            return true;
        }

        private static int ResolveChunkImpostorType(in ChunkDefinition definition)
        {
            if (ContainsToken(definition.label, "wreck") ||
                ContainsToken(definition.addressableAddress, "wreck") ||
                ContainsToken(definition.additiveSceneName, "wreck") ||
                ContainsToken(definition.label, "ruin") ||
                ContainsToken(definition.addressableAddress, "ruin"))
            {
                return ActiveImpostorWreckType;
            }

            if (ContainsToken(definition.label, "base") ||
                ContainsToken(definition.addressableAddress, "base") ||
                ContainsToken(definition.additiveSceneName, "base") ||
                ContainsToken(definition.label, "module") ||
                ContainsToken(definition.addressableAddress, "module"))
            {
                return ActiveImpostorBaseType;
            }

            return 0;
        }

        private static bool ContainsToken(string source, string token)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasUsableAdditiveSceneName(in ChunkDefinition definition)
        {
            return !string.IsNullOrEmpty(ResolveUsableAdditiveSceneName(in definition));
        }

        private static bool HasUsableAddressableAddress(in ChunkDefinition definition)
        {
            return !string.IsNullOrEmpty(ResolveUsableAddressableAddress(in definition));
        }

        private static string ResolveUsableAdditiveSceneName(in ChunkDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.additiveSceneName) ? string.Empty : definition.additiveSceneName.Trim();
        }

        private static string ResolveUsableAddressableAddress(in ChunkDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.addressableAddress) ? string.Empty : definition.addressableAddress.Trim();
        }

        private float ResolveActiveImpostorDefaultRadius()
        {
            return math.max(1f, ResolveEffectiveUnloadRadiusMeters() * 0.5f);
        }

        private void MuteAssociatedAcousticPortals(long chunkId, bool muted)
        {
            _ = chunkId;
            _ = muted;
        }

        private void RefreshStateDiagnosticsIfDirty()
        {
            if (!_stateDiagnosticsDirty || _residencyJobScheduled)
                return;

            UpdateStateDiagnostics();
        }

        private void UpdateStateDiagnostics()
        {
            if (!TryResolveChunkStateSlots(out NativeArray<ChunkStateSlotDTO> stateSlots) ||
                !TryResolveWorldStreamingVaultBuffer(in _chunkIdsHandle, ChunkIdsVaultBufferId, ResolveStreamingLedgerCapacity(), out NativeArray<long> chunkIds))
            {
                _stateDiagnosticsDirty = false;
                return;
            }

            int resident = 0;
            int loading = 0;
            int evicting = 0;
            uint stateHash = ChunkStateHashSeed;
            for (int i = 0; i < _chunkCount; i++)
            {
                long chunkId = chunkIds[i];
                if ((uint)i >= (uint)stateSlots.Length || stateSlots[i].Occupied == 0)
                    continue;

                ChunkState state = (ChunkState)stateSlots[i].State;
                stateHash = MixHash(stateHash, unchecked((uint)chunkId));
                stateHash = MixHash(stateHash, (uint)(byte)state);
                if (HasFlag(state, ChunkState.Resident))
                    resident++;
                if (HasFlag(state, ChunkState.Loading))
                    loading++;
                if (HasFlag(state, ChunkState.Evicting))
                    evicting++;
            }

            _debugResidentChunks = resident;
            _debugLoadingChunks = loading;
            _debugEvictingChunks = evicting;
            stateHash = MixHash(stateHash, unchecked((uint)_activeImpostorCount));
            _debugStateHash = stateHash;
            _stateDiagnosticsDirty = false;
        }

        private void WriteTelemetrySample(long focusChunkId, uint flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || _residencyTelemetryHandle.BufferID == 0u)
                return;

            if (!vault.TryAcquireWriteLock(in _residencyTelemetryHandle, VaultOwnerSystem, out NativeArray<ChunkResidencyTelemetryEntry> telemetryRing))
                return;

            try
            {
                if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                    return;

                RefreshStateDiagnosticsIfDirty();
                AbsoluteUniversePosition playerAup = default;
                TryCapturePlayerAupSnapshot(out playerAup);
                int resident = _debugResidentChunks;
                int loading = _debugLoadingChunks;
                int evicting = _debugEvictingChunks;
                uint stateHash = _debugStateHash;

                ChunkResidencyTelemetryEntry entry = default;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.Flags = flags;
                entry.FocusChunkId = focusChunkId;
                entry.PlayerGridX = playerAup.GridX;
                entry.PlayerGridY = playerAup.GridY;
                entry.PlayerGridZ = playerAup.GridZ;
                entry.PlayerLocal.x = playerAup.LocalX;
                entry.PlayerLocal.y = playerAup.LocalY;
                entry.PlayerLocal.z = playerAup.LocalZ;
                entry.PendingLoads = (ushort)math.min(ushort.MaxValue, _pendingLoadRequestCount);
                entry.ResidentCount = (ushort)math.min(ushort.MaxValue, resident);
                entry.LoadingCount = (ushort)math.min(ushort.MaxValue, loading);
                entry.EvictingCount = (ushort)math.min(ushort.MaxValue, evicting);
                entry.ActiveImpostorCount = (ushort)math.min(ushort.MaxValue, _activeImpostorCount);
                entry.StateHash = stateHash;

                telemetryRing[_telemetryCursor] = entry;
                _telemetryCursor++;
                if (_telemetryCursor >= TelemetryCapacity)
                    _telemetryCursor = 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _residencyTelemetryHandle, VaultOwnerSystem);
            }
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            WriteTelemetrySample(0L, reasonFlags);
            NativeArray<ChunkResidencyTelemetryEntry> telemetryRing = ResolveResidencyTelemetryRing();
            if (!telemetryRing.IsCreated)
                return;

            DumpTelemetryToPath(DumpRelativePath, telemetryRing, reasonFlags);
        }

        private void DumpBackpressureTelemetry(uint reasonFlags)
        {
            WriteTelemetrySample(0L, reasonFlags);
            NativeArray<ChunkResidencyTelemetryEntry> telemetryRing = ResolveResidencyTelemetryRing();
            if (!telemetryRing.IsCreated)
                return;

            DumpTelemetryToPath(BackpressureDumpRelativePath, telemetryRing, reasonFlags);
        }

        private void DumpHlodTelemetry(uint reasonFlags)
        {
            WriteTelemetrySample(0L, reasonFlags);
            NativeArray<ChunkResidencyTelemetryEntry> telemetryRing = ResolveResidencyTelemetryRing();
            if (!telemetryRing.IsCreated)
                return;

            DumpTelemetryToPath(HlodDumpRelativePath, telemetryRing, reasonFlags);
        }

        private static void DumpTelemetryToPath(string path, NativeArray<ChunkResidencyTelemetryEntry> telemetryRing, uint reasonFlags)
        {
            if (string.IsNullOrEmpty(path) || !telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = WorldChunkResidencyDumpHeaderBytes + TelemetryCapacity * ResidencyTelemetryEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(WorldChunkResidencyManager),
                    "worldChunkResidencyTelemetryDumpPayload");
                int cursor = 0;
                WriteUInt64LittleEndian(payload, ref cursor, HectonDumpMagic);
                WriteUInt32LittleEndian(payload, ref cursor, WorldChunkResidencyDumpVersion);
                WriteUInt32LittleEndian(payload, ref cursor, TelemetryCapacity);
                WriteUInt32LittleEndian(payload, ref cursor, ResidencyTelemetryEntrySizeBytes);
                WriteUInt32LittleEndian(payload, ref cursor, reasonFlags);
                WriteUInt32LittleEndian(payload, ref cursor, WorldChunkResidencyDumpLayoutHash);
                WriteUInt32LittleEndian(payload, ref cursor, 0u);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    ChunkResidencyTelemetryEntry entry = telemetryRing[i];
                    WriteChunkResidencyTelemetryEntry(payload, ref cursor, in entry);
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, cursor);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(WorldChunkResidencyManager),
                    "worldChunkResidencyTelemetryDumpPayload");
            }
        }

        private static void WriteChunkResidencyTelemetryEntry(NativeArray<byte> destination, ref int cursor, in ChunkResidencyTelemetryEntry entry)
        {
            WriteInt64LittleEndian(destination, ref cursor, entry.FocusChunkId);
            WriteInt64LittleEndian(destination, ref cursor, entry.PlayerGridX);
            WriteInt64LittleEndian(destination, ref cursor, entry.PlayerGridY);
            WriteInt64LittleEndian(destination, ref cursor, entry.PlayerGridZ);
            WriteSingleLittleEndian(destination, ref cursor, entry.PlayerLocal.x);
            WriteSingleLittleEndian(destination, ref cursor, entry.PlayerLocal.y);
            WriteSingleLittleEndian(destination, ref cursor, entry.PlayerLocal.z);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags | ((uint)entry.ActiveImpostorCount << 16));
            WriteUInt32LittleEndian(destination, ref cursor, entry.StateHash);
            WriteUInt16LittleEndian(destination, ref cursor, entry.PendingLoads);
            WriteUInt16LittleEndian(destination, ref cursor, entry.ResidentCount);
            WriteUInt16LittleEndian(destination, ref cursor, entry.LoadingCount);
            WriteUInt16LittleEndian(destination, ref cursor, entry.EvictingCount);
        }

        private static void WriteSingleLittleEndian(NativeArray<byte> destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> destination, ref int cursor, long value)
        {
            WriteUInt64LittleEndian(destination, ref cursor, unchecked((ulong)value));
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, ref int cursor, ulong value)
        {
            for (int i = 0; i < 8; i++)
                destination[cursor++] = (byte)(value >> (i * 8));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, ref int cursor, uint value)
        {
            for (int i = 0; i < 4; i++)
                destination[cursor++] = (byte)(value >> (i * 8));
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> destination, ref int cursor, ushort value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
        }

        private void DisposeNativeState()
        {
            ReleaseStreamingLedgerBuffers();
            _pendingLoadRequestCount = 0;
            _loadRequestReadIndex = 0;
            _loadRequestWriteIndex = 0;
            _pagerReadTicketCount = 0;
        }

        private void ReleaseStreamingLedgerBuffers()
        {
            ReleaseStreamingLedgerBuffers(null);
        }

        private void ReleaseStreamingLedgerBuffers(IDataVault releaseVault)
        {
            IDataVault vault = releaseVault ?? _streamingLedgerVault ?? _dataVault;
            ReleaseWorldStreamingVaultHandle(vault, ref _chunkIdsHandle, ChunkIdsVaultBufferId, ref _chunkIdsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _chunkCentersHandle, ChunkCentersVaultBufferId, ref _chunkCentersSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _chunkStateSlotsHandle, ChunkStateSlotsVaultBufferId, ref _chunkStateSlotsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _loadRequestsHandle, LoadRequestsVaultBufferId, ref _loadRequestsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _residencyDecisionsHandle, ResidencyDecisionsVaultBufferId, ref _residencyDecisionsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _chunkResidencyDtoHandle, ChunkResidencyVaultBufferId);
            ReleaseWorldStreamingVaultHandle(vault, ref _addressablesRequestDtoHandle, AddressablesRequestVaultBufferId);
            ReleaseWorldStreamingVaultHandle(vault, ref _hlodImpostorDtoHandle, HlodImpostorVaultBufferId);
            ReleaseWorldStreamingVaultHandle(vault, ref _streamingTuningHandle, StreamingTuningVaultBufferId);
            ReleaseWorldStreamingVaultHandle(vault, ref _mockAupShiftHandle, MockAupShiftVaultBufferId);
            ReleaseWorldStreamingVaultHandle(vault, ref _residencyTelemetryHandle, ResidencyTelemetryVaultBufferId, ref _residencyTelemetrySentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _loadStartTimesHandle, LoadStartTimesVaultBufferId, ref _loadStartTimesSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _loadImmediateRadiusFlagsHandle, LoadImmediateRadiusFlagsVaultBufferId, ref _loadImmediateRadiusFlagsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorsHandle, ActiveImpostorsVaultBufferId, ref _activeImpostorsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _impostorTypesHandle, ImpostorTypesVaultBufferId, ref _impostorTypesSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorChunkIdsHandle, ActiveImpostorChunkIdsVaultBufferId, ref _activeImpostorChunkIdsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorSpawnTimesHandle, ActiveImpostorSpawnTimesVaultBufferId, ref _activeImpostorSpawnTimesSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorCentersHandle, ActiveImpostorCentersVaultBufferId, ref _activeImpostorCentersSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorSizesHandle, ActiveImpostorSizesVaultBufferId, ref _activeImpostorSizesSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorFlagsHandle, ActiveImpostorFlagsVaultBufferId, ref _activeImpostorFlagsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorCartographyPointsHandle, ActiveImpostorCartographyVaultBufferId, ref _activeImpostorCartographyPointsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorCountHandle, ActiveImpostorCountVaultBufferId, ref _activeImpostorCountSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _activeImpostorFadeOutCountHandle, ActiveImpostorFadeOutCountVaultBufferId, ref _activeImpostorFadeOutCountSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _pagerReadTicketsHandle, PagerReadTicketsVaultBufferId, ref _pagerReadTicketsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _macroDatabaseEvictionScratchHandle, MacroDatabaseEvictionScratchVaultBufferId, ref _macroDatabaseEvictionScratchSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _hydrationApplyRecordsHandle, HydrationApplyRecordVaultBufferId, ref _hydrationApplyRecordsSentinelId);
            ReleaseWorldStreamingVaultHandle(vault, ref _dehydrationMetadataPayloadHandle, DehydrationMetadataVaultBufferId, ref _dehydrationMetadataPayloadSentinelId);
            _streamingLedgerVault = null;
            _streamingVaultBacked = false;
            _streamingLedgerCapacity = 0;
        }

        private bool EnsureWorldStreamingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryResolveWorldStreamingVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (handle.Generation != 0u)
            {
                if (ReferenceEquals(_streamingLedgerVault, vault))
                    ReleaseWorldStreamingVaultHandle(vault, ref handle, bufferId);
                else
                    handle = default;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return TryResolveWorldStreamingVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private bool EnsureWorldStreamingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            ref int sentinelId) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryResolveWorldStreamingVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (handle.Generation != 0u)
            {
                if (ReferenceEquals(_streamingLedgerVault, vault))
                    ReleaseWorldStreamingVaultHandle(vault, ref handle, bufferId, ref sentinelId);
                else
                    handle = default;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return TryResolveWorldStreamingVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private bool TryResolveWorldStreamingVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return TryResolveWorldStreamingVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveWorldStreamingVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsWorldStreamingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void ReleaseWorldStreamingVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsWorldStreamingVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void ReleaseWorldStreamingVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            ref int sentinelId) where T : struct
        {
            bool hasOwnedHandle = IsWorldStreamingVaultHandle(in handle, bufferId);
            bool released = !hasOwnedHandle;

            if (hasOwnedHandle && vault != null)
                released = vault.ReleaseBuffer(in handle);

            if (!released)
                return;

            handle = default;

            if (sentinelId <= 0)
                return;

            NativeMemorySentinel.Unregister(sentinelId);
            sentinelId = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWorldStreamingVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSq(in AbsoluteUniversePosition lhs, in AbsoluteUniversePosition rhs)
        {
            return AupPrecisionMath.DistanceSqSafeDouble(ToAbsoluteDouble3(in lhs), ToAbsoluteDouble3(in rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSq(in AbsoluteUniversePositionBlit lhs, in AbsoluteUniversePositionBlit rhs)
        {
            return AupPrecisionMath.DistanceSqSafeDouble(ToAbsoluteDouble3(in lhs), ToAbsoluteDouble3(in rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSq(in AbsoluteUniversePositionBlit lhs, in AbsoluteUniversePosition rhs)
        {
            return AupPrecisionMath.DistanceSqSafeDouble(ToAbsoluteDouble3(in lhs), ToAbsoluteDouble3(in rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.LocalX,
                (position.GridY * CellSize) + position.LocalY,
                (position.GridZ * CellSize) + position.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.Local.x,
                (position.GridY * CellSize) + position.Local.y,
                (position.GridZ * CellSize) + position.Local.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteX(in AbsoluteUniversePosition position)
        {
            return (position.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteY(in AbsoluteUniversePosition position)
        {
            return (position.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteZ(in AbsoluteUniversePosition position)
        {
            return (position.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteX(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteY(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteZ(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(ChunkState state, ChunkState flag)
        {
            return ((byte)state & (byte)flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(byte state, byte flag)
        {
            return (state & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }
}

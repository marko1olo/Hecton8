using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.AI.Ecosystem
{
    public sealed partial class ShinobuEcosystemBalancer
    {
        private static int s_x001DirectSignalPushDropCount_ShinobuEcosystemBalancer_FlockingAvoidance;

        private const uint FlockingThreatMovementHash = 0x4D564143u; // MVAC
        private const uint FlockingThreatImpactHash = 0x48494D50u; // HIMP
        private const uint FlockingThreatDamageHash = 0x43444D47u; // CDMG
        private const ulong FlockingDumpMagic = 0x5348333037464C4FUL; // SH307FLO
        private const int FlockingDumpVersion = 1;
        private const string FlockingDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_307_Flocking.bin";

        private void CaptureFlockingThreatSignals(
            NativeArray<FlockingThreatDTO> threats,
            NativeArray<int> threatCount,
            NativeArray<FlockingCounter64> counters,
            float globalQualityWeight)
        {
            if (!threats.IsCreated || threats.Length <= 0 || !threatCount.IsCreated || threatCount.Length <= 0)
                return;

            int limit = math.min(threats.Length, ResolveFlockingThreatBudget(globalQualityWeight));
            int written = 0;
            double3 cameraAbsolute = ToAbsoluteDouble3(in _cameraAup);

            ReadOnlySpan<MovementAcousticSignal> movementSignals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            for (int i = movementSignals.Length - 1; i >= 0 && written < limit; i--)
            {
                MovementAcousticSignal signal = movementSignals[i];
                if (!IsFiniteAup(in signal.PositionAup))
                    continue;

                float3 local = AupToLocal(in signal.PositionAup, in _cameraAup);
                float speed = math.sqrt(math.max(0f, signal.VelocitySq));
                float intensity = math.saturate(signal.Volume + (speed * 0.035f));
                float radius = math.lerp(10f, 82f, intensity) * math.lerp(0.82f, 1.28f, math.saturate(globalQualityWeight));
                TryAppendFlockingThreat(threats, ref written, local, radius, intensity, signal.SourceId, FlockingThreatMovementHash);
            }

            ReadOnlySpan<HighSpeedImpactSignal> impactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            for (int i = impactSignals.Length - 1; i >= 0 && written < limit; i--)
            {
                HighSpeedImpactSignal signal = impactSignals[i];
                if (!IsFiniteAup(in signal.PointAup))
                    continue;

                float energy01 = math.saturate((signal.ImpactSpeed * 0.045f) + (signal.KineticEnergy * 0.00035f));
                float3 local = AupToLocal(in signal.PointAup, in _cameraAup);
                float radius = math.lerp(8f, 96f, energy01);
                TryAppendFlockingThreat(threats, ref written, local, radius, energy01, signal.SourceHash, FlockingThreatImpactHash);
            }

            ReadOnlySpan<CombatDamageSignal> damageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            for (int i = damageSignals.Length - 1; i >= 0 && written < limit; i--)
            {
                CombatDamageSignal signal = damageSignals[i];
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (!CombatDamageSignalCodec.IsFiniteAup(signal.ImpactAup))
                    continue;

                float intensity = math.saturate(signal.Magnitude * 0.018f);
                float radius = math.lerp(14f, 110f, intensity);
                float3 local = ToFiniteLocalFloat3(signal.ImpactAup - cameraAbsolute);
                TryAppendFlockingThreat(threats, ref written, local, radius, intensity, signal.SourceHash, FlockingThreatDamageHash);
            }

            threatCount[0] = written;
            SetFlockingCounter(counters, FlockingCounterActiveThreats, written);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveFlockingThreatBudget(float globalQualityWeight)
        {
            return math.clamp((int)math.round(math.lerp(4f, 32f, math.saturate(globalQualityWeight))), 1, FlockingThreatCapacity);
        }

        private static void TryAppendFlockingThreat(
            NativeArray<FlockingThreatDTO> threats,
            ref int written,
            float3 localPosition,
            float radiusMeters,
            float intensity01,
            uint sourceId,
            uint typeHash)
        {
            if ((uint)written >= (uint)threats.Length ||
                !math.all(math.isfinite(localPosition)) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(intensity01))
            {
                return;
            }

            FlockingThreatDTO threat = default;
            threat.LocalPosition = localPosition;
            threat.RadiusMeters = math.max(0.25f, radiusMeters);
            threat.Intensity01 = math.saturate(intensity01);
            threat.SourceId = sourceId;
            threat.TypeHash = typeHash;
            threat.DirectionalBias = 0f;
            threats[written++] = threat;
        }

        private bool TryBuildFlockingTelemetryEntry(
            int activeBoidCount,
            int invalidMathCount,
            int overflowCount,
            out FlockingTelemetryEntry entry,
            out bool shouldDump)
        {
            entry = default;
            shouldDump = false;
            if (!_flockingCounterJobScratch.IsCreated)
                return false;

            int samples = ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterNeighborSamples);
            int evaluated = math.max(1, ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterEvaluatedBoids));
            int panicCount = ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterPanicBoids);
            int activeThreats = ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterActiveThreats);
            int maxNeighbors = ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterMaxNeighbors);
            float averageNeighbors = samples * math.rcp(evaluated);
            bool solveOverBudget = _lastFlockingMs > FlockingTelemetryFaultThresholdMs;
            uint flags = (invalidMathCount != 0 ? EntityFlagInvalidMath : 0u) |
                         (overflowCount != 0 ? 0x80000000u : 0u) |
                         (solveOverBudget ? TelemetryFlagSolveOverBudget : 0u);
            uint frame = ResolveCurrentSimulationFrame();

            entry = default;
            entry.Frame = frame;
            entry.StateHash = MixFlockingTelemetryHash(activeBoidCount, samples, activeThreats, panicCount, overflowCount);
            entry.SimulatedBoidCount = activeBoidCount;
            entry.NeighborSamplesTotal = samples;
            entry.AverageNeighbors = averageNeighbors;
            entry.ActiveThreatCount = activeThreats;
            entry.BurstExecutionMicroseconds = math.max(0f, _lastFlockingMs) * 1000f;
            entry.GlobalQualityWeight = _lastGlobalQualityWeight;
            entry.Flags = flags;
            entry.PanicBoidCount = panicCount;
            entry.MaxNeighborsPerBoid = maxNeighbors;
            entry.SpatialHashOverflowCount = overflowCount;
            entry.InvalidMathCount = invalidMathCount;
            entry.SpatialHashMicroseconds = math.max(0f, _lastSpatialHashMs) * 1000f;
            entry.MatrixUploadMicroseconds = math.max(0f, _lastMatrixUploadMs) * 1000f;
            entry.Pad0 = 0u;

            shouldDump = invalidMathCount != 0 || overflowCount != 0 || solveOverBudget;
            return true;
        }

        private unsafe void WriteFlockingCountersAfterRelease(IDataVault vault)
        {
            if (!_flockingCounterJobScratch.IsCreated ||
                vault == null ||
                !TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuFlockingCounters64))
            {
                return;
            }

            try
            {
                if (!TryOpenVaultView(vault, in _flockingCounterHandle, BufferID.ShinobuFlockingCounters64, FlockingCounterCapacity, out NativeArray<FlockingCounter64> counters) ||
                    counters.Length <= 0)
                {
                    return;
                }

                int count = math.min(counters.Length, _flockingCounterJobScratch.Length);
                if (count <= 0)
                    return;

                void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(counters);
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_flockingCounterJobScratch);
                UnsafeUtility.MemCpy(targetPtr, sourcePtr, (long)count * UnsafeUtility.SizeOf<FlockingCounter64>());
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuFlockingCounters64);
            }
        }

        private void WriteFlockingTelemetryAndFaultDump(
            IDataVault vault,
            in FlockingTelemetryEntry entry,
            bool shouldDump)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuFlockingTelemetryRing))
                return;

            bool dumpAfterRelease = false;
            int dumpCursor = 0;
            try
            {
                if (!TryOpenVaultView(vault, in _flockingTelemetryHandle, BufferID.ShinobuFlockingTelemetryRing, FlockingTelemetryCapacity, out NativeArray<FlockingTelemetryEntry> telemetry) ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int cursor = _flockingTelemetryCursor;
                if (cursor < 0 || cursor >= int.MaxValue - telemetry.Length)
                    cursor = 0;

                int index = cursor % telemetry.Length;
                int nextCursor = cursor + 1;
                _flockingTelemetryCursor = nextCursor;
                telemetry[index] = entry;
                if (_flockingTelemetryMirror.IsCreated && _flockingTelemetryMirror.Length == telemetry.Length)
                    _flockingTelemetryMirror[index] = entry;

                if (shouldDump && !_dumpedFlockingFault)
                {
                    _dumpedFlockingFault = true;
                    dumpAfterRelease = true;
                    dumpCursor = nextCursor;
                }
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuFlockingTelemetryRing);
            }

            if (dumpAfterRelease && _flockingTelemetryMirror.IsCreated)
                DumpFlockingBlackBox(_flockingTelemetryMirror, dumpCursor);
        }

        private void TryPublishFlockingDispersalSignal(
            int activeBoidCount,
            int activeThreatCount,
            int panicBoidCount,
            float globalQualityWeight,
            uint frame)
        {
            if (activeBoidCount <= 0 ||
                activeThreatCount <= 0 ||
                panicBoidCount <= 0 ||
                !IsFiniteAup(in _cameraAup) ||
                !SignalBus<SwarmDispersedSignal>.HasNativeStorage)
            {
                return;
            }

            float quality01 = math.saturate(globalQualityWeight);
            int publishStrideFrames = math.clamp((int)math.round(math.lerp(12f, 2f, Smooth01(quality01))), 2, 12);
            if (_lastFlockingDispersalSignalFrame != 0u &&
                frame - _lastFlockingDispersalSignalFrame < (uint)publishStrideFrames)
            {
                return;
            }

            float panic01 = math.saturate(panicBoidCount * math.rcp(math.max(1, activeBoidCount)));
            float threat01 = math.saturate(activeThreatCount * math.rcp(math.max(1, FlockingThreatCapacity)));
            float intensity01 = math.saturate((panic01 * 0.72f) + (threat01 * 0.28f));
            if (intensity01 <= 0.001f)
                return;

            SwarmDispersedSignal signal = default;
            signal.PositionAup = _cameraAup;
            signal.RadiusMeters = math.lerp(12f, 96f, math.saturate(intensity01 * math.lerp(0.75f, 1.35f, Smooth01(quality01))));
            signal.Intensity01 = intensity01;
            signal.SourceId = SourceHash ^ 0x00000307u;
            signal.EstimatedBoidCount = (ushort)math.clamp(panicBoidCount, 0, ushort.MaxValue);
            signal.Flags = 0;
            signal.QualityTier = (byte)math.clamp((int)math.round(quality01 * 255f), 0, 255);

            if (SignalBus<SwarmDispersedSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_ShinobuEcosystemBalancer_FlockingAvoidance))
                _lastFlockingDispersalSignalFrame = frame;
        }

        private static uint MixFlockingTelemetryHash(int active, int samples, int threats, int panic, int overflow)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)active) * 16777619u;
                hash = (hash ^ (uint)samples) * 16777619u;
                hash = (hash ^ (uint)threats) * 16777619u;
                hash = (hash ^ (uint)panic) * 16777619u;
                hash = (hash ^ (uint)overflow) * 16777619u;
                return hash != 0u ? hash : 1u;
            }
        }

        private static unsafe void DumpFlockingBlackBox(NativeArray<FlockingTelemetryEntry> telemetry, int cursor)
        {
            _ = telemetry;
            _ = cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveFlockHashId(uint speciesHash, int packIndex)
        {
            uint x = speciesHash ^ ((uint)packIndex * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x != 0u ? x : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int AccumulateNeighborBatch4(
            float3 position,
            float radiusMeters,
            int laneCount,
            int4 indices,
            AmbientEntityDTO* entitySnapshots,
            ref float3 separation,
            ref float3 alignment,
            ref float3 cohesion)
        {
            if (laneCount <= 0)
                return 0;

            int i0 = indices.x;
            int i1 = laneCount > 1 ? indices.y : i0;
            int i2 = laneCount > 2 ? indices.z : i0;
            int i3 = laneCount > 3 ? indices.w : i0;
            AmbientEntityDTO e0 = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + i0);
            AmbientEntityDTO e1 = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + i1);
            AmbientEntityDTO e2 = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + i2);
            AmbientEntityDTO e3 = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + i3);

            float4 px = new float4(e0.Position.x, e1.Position.x, e2.Position.x, e3.Position.x);
            float4 py = new float4(e0.Position.y, e1.Position.y, e2.Position.y, e3.Position.y);
            float4 pz = new float4(e0.Position.z, e1.Position.z, e2.Position.z, e3.Position.z);
            HectonSphere query = new HectonSphere { Center = position, Radius = math.max(0.001f, radiusMeters) };
            int mask = query.IntersectsMask4(px, py, pz, new float4(0f));
            mask &= (1 << math.min(laneCount, 4)) - 1;

            float4 dx = position.x - px;
            float4 dy = position.y - py;
            float4 dz = position.z - pz;
            float4 distSq = (dx * dx) + (dy * dy) + (dz * dz);
            mask &= math.bitmask(distSq > new float4(0.0001f));
            if (mask == 0)
                return 0;

            int accepted = 0;
            AccumulateNeighborLane(mask, 1, e0, new float3(dx.x, dy.x, dz.x), distSq.x, ref separation, ref alignment, ref cohesion, ref accepted);
            AccumulateNeighborLane(mask, 2, e1, new float3(dx.y, dy.y, dz.y), distSq.y, ref separation, ref alignment, ref cohesion, ref accepted);
            AccumulateNeighborLane(mask, 4, e2, new float3(dx.z, dy.z, dz.z), distSq.z, ref separation, ref alignment, ref cohesion, ref accepted);
            AccumulateNeighborLane(mask, 8, e3, new float3(dx.w, dy.w, dz.w), distSq.w, ref separation, ref alignment, ref cohesion, ref accepted);
            return accepted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AccumulateNeighborLane(
            int mask,
            int laneBit,
            AmbientEntityDTO entity,
            float3 delta,
            float distSq,
            ref float3 separation,
            ref float3 alignment,
            ref float3 cohesion,
            ref int accepted)
        {
            if ((mask & laneBit) == 0)
                return;

            float invDist = math.rsqrt(math.max(0.0001f, distSq));
            separation += delta * invDist * invDist;
            alignment += entity.Velocity;
            cohesion += entity.Position;
            accepted++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float ApplyFlockingThreats(
            float3 position,
            float3 forward,
            float panicScalar,
            FlockingThreatDTO* threats,
            int threatCount,
            float evasionWeight,
            float evasionRadiusMeters,
            float globalQualityWeight,
            ref float3 acceleration)
        {
            float panic = math.saturate(panicScalar);
            float qualityShape = Smooth01(globalQualityWeight);
            float radiusScale = math.clamp(math.max(4f, evasionRadiusMeters) * (1f / 48f), 0.25f, 3f);
            int safeCount = math.clamp(threatCount, 0, FlockingThreatCapacity);
            for (int i = 0; i < safeCount; i++)
            {
                FlockingThreatDTO threat = threats[i];
                float radius = math.max(0.25f, threat.RadiusMeters * radiusScale);
                float radiusSq = radius * radius;
                float3 delta = position - threat.LocalPosition;
                float distSq = math.lengthsq(delta);
                if (!math.isfinite(distSq) || distSq > radiusSq)
                    continue;

                float proximity = math.saturate(1f - (distSq * math.rcp(math.max(0.0001f, radiusSq))));
                float intensity = math.saturate(threat.Intensity01);
                float burst = intensity * (1.75f + proximity * math.lerp(3.5f, 7.5f, qualityShape));
                float3 away = SafeNormalize(delta, -forward);
                float3 swirl = SafeNormalize(math.cross(away, new float3(0f, 1f, 0f)), forward);
                acceleration += away * (evasionWeight * burst * (0.75f + proximity));
                acceleration += swirl * (evasionWeight * burst * math.lerp(0.08f, 0.42f, qualityShape));
                panic = math.max(panic, proximity * intensity);
            }

            return math.saturate(panic);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadFlockingCounter(NativeArray<FlockingCounter64> counters, int index)
        {
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index].Value : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetFlockingCounter(NativeArray<FlockingCounter64> counters, int index, int value)
        {
            if (!counters.IsCreated || (uint)index >= (uint)counters.Length)
                return;

            FlockingCounter64 counter = counters[index];
            counter.Value = value;
            counters[index] = counter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void AddFlockingCounterAtomic(NativeArray<FlockingCounter64> counters, int index, int delta)
        {
            if (!counters.IsCreated || delta == 0 || (uint)index >= (uint)counters.Length)
                return;

            FlockingCounter64* ptr = (FlockingCounter64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
            ref FlockingCounter64 row = ref UnsafeUtility.AsRef<FlockingCounter64>(ptr + index);
            Interlocked.Add(ref row.Value, delta);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FlockingThreatDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float RadiusMeters;
        [FieldOffset(16)] public float Intensity01;
        [FieldOffset(20)] public uint SourceId;
        [FieldOffset(24)] public uint TypeHash;
        [FieldOffset(28)] public float DirectionalBias;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FlockingTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int SimulatedBoidCount;
        [FieldOffset(12)] public int NeighborSamplesTotal;
        [FieldOffset(16)] public float AverageNeighbors;
        [FieldOffset(20)] public int ActiveThreatCount;
        [FieldOffset(24)] public float BurstExecutionMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public int PanicBoidCount;
        [FieldOffset(40)] public int MaxNeighborsPerBoid;
        [FieldOffset(44)] public int SpatialHashOverflowCount;
        [FieldOffset(48)] public int InvalidMathCount;
        [FieldOffset(52)] public float SpatialHashMicroseconds;
        [FieldOffset(56)] public float MatrixUploadMicroseconds;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FlockingCounter64
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Pad0;
        [FieldOffset(8)] public uint Pad1;
        [FieldOffset(12)] public uint Pad2;
        [FieldOffset(16)] public uint Pad3;
        [FieldOffset(20)] public uint Pad4;
        [FieldOffset(24)] public uint Pad5;
        [FieldOffset(28)] public uint Pad6;
        [FieldOffset(32)] public uint Pad7;
        [FieldOffset(36)] public uint Pad8;
        [FieldOffset(40)] public uint Pad9;
        [FieldOffset(44)] public uint Pad10;
        [FieldOffset(48)] public uint Pad11;
        [FieldOffset(52)] public uint Pad12;
        [FieldOffset(56)] public uint Pad13;
        [FieldOffset(60)] public uint Pad14;
    }
}

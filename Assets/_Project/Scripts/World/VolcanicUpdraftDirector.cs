using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.AI.Cognition;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Physics.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VfxDynamicWakeDTO = Hecton8.VFX.DynamicWakeDTO;
using VfxMockFlowField = Hecton8.VFX.MockFlowField;

namespace Hecton8.World
{
    /// <summary>
    /// One L1 cache-line vent truth row. Layout: double3 AUP 24B, float3 up 12B,
    /// radius/thrust/timer/pad 16B, implicit 4B alignment gap, ulong pad 8B.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VentStateDTO
    {
        public double3 AUP;
        public float3 UpVector;
        public float Radius;
        public float ThrustPower;
        public float EruptionTimer;
        public uint _pad0;
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VolcanicUpdraftSettingsDTO
    {
        public float MaxThrust;
        public float EruptionFrequency;
        public float CylinderRadius;
        public float MaxHeight;
        public float HeatOutput;
        public float GlobalQualityWeight;
        public float MaxVerticalVelocity;
        public float EruptionThreshold;
        public float EruptionGain;
        public float AcousticRadius;
        public float DebrisCommandIntensity;
        public float ThermalBlindnessScale;
        public uint Frame;
        public uint VentCount;
        public uint SourceHash;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VolcanicUpdraftTelemetryEntry
    {
        public double3 PrimaryVentAup;
        public float3 LastVector;
        public float CylinderComputeTimeMs;
        public uint Frame;
        public uint StateHash;
        public ushort ActiveEruptions;
        public ushort EntitiesLifted;
        public ushort DebrisLifted;
        public ushort LeviathansLifted;
        public uint Flags;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockSubmarineArray
    {
        public double3 AUP;
        public float3 Velocity;
        public float Radius;
        public uint Flags;
        public uint EntityId;
        public float MassKg;
        public float LiftScalar;
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockLeviathanVelocityDTO
    {
        public double3 AUP;
        public float3 Velocity;
        public float3 DesiredDirection;
        public float RideStaminaSaved01;
        public uint Flags;
        public ushort Slot;
        public ushort _pad0;
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockDebrisParticleDTO
    {
        public double3 AUP;
        public float3 Velocity;
        public float Radius;
        public uint Flags;
        public uint EntityId;
        public float MassKg;
        public float LiftWeight;
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VolcanicFloatStateSignal
    {
        public double3 AUP;
        public float3 UpVector;
        public float Intensity01;
        public uint Frame;
        public uint EntityHash;
        public ushort Slot;
        public byte StateKind;
        public byte Flags;
        public uint _pad0;
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VolcanicPlayerHeatSignalDTO
    {
        public double3 AUP;
        public float Heat01;
        public float Blindness01;
        public float Radius;
        public float Intensity01;
        public uint Frame;
        public uint SourceHash;
        public uint Flags;
        public uint _pad0;
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VolcanicUpdraftFrameCounter
    {
        [FieldOffset(0)] public int ActiveEruptions;
        [FieldOffset(4)] public int EntitiesLifted;
        [FieldOffset(8)] public int DebrisLifted;
        [FieldOffset(12)] public int LeviathansLifted;
        [FieldOffset(16)] public int NanFlag;
        [FieldOffset(20)] public uint StateHash;
        [FieldOffset(24)] public float EstimatedComputeMs;
        [FieldOffset(28)] public float PeakIntensity01;
        [FieldOffset(32)] public double3 PrimaryVentAup;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    public static class VolcanicUpdraftVault
    {
        public const int MaxVents = 64;
        public const int TelemetryFrames = 300;
        public const int CounterCapacity = 256;
        public const int MockSubmarineCapacity = 16;
        public const int MockLeviathanCapacity = 16;
        public const int MockDebrisCapacity = 64;
        public const int DynamicWakeCapacity = 16;
        public const int CsvScratchBytes = 4096;
        public const uint SourceHash = 0x564F4C43u; // VOLC
        public const uint TelemetryFlagNaN = 1u << 0;
        public const uint TelemetryFlagEmergencyVents = 1u << 1;
        public const uint TelemetryFlagDebrisCulled = 1u << 2;
        public const uint FloatStateKindThermalRide = 64;
        public const uint ForceFlagVolcanicUpdraft = 1u << 2;
        private static JobHandle _pendingVentReadHandle;
        private static JobHandle _pendingVentWriteHandle;

        public const BufferID VentsBuffer = (BufferID)70750;
        public const BufferID SettingsBuffer = (BufferID)70751;
        public const BufferID TelemetryBuffer = (BufferID)70752;
        public const BufferID MockSubmarinesBuffer = (BufferID)70753;
        public const BufferID MockLeviathansBuffer = (BufferID)70754;
        public const BufferID FloatSignalsBuffer = (BufferID)70755;
        public const BufferID DynamicWakesBuffer = (BufferID)70756;
        public const BufferID MockFlowFieldBuffer = (BufferID)70757;
        public const BufferID CsvScratchBuffer = (BufferID)70758;
        public const BufferID FrameCountersBuffer = (BufferID)70759;
        public const BufferID MockDebrisBuffer = (BufferID)70760;
        public const BufferID PlayerHeatBuffer = (BufferID)70761;

        private const int SubmarineCounterBase = 64;

        public static JobHandle ScheduleSubmarineInjection(
            IDataVault vault,
            NativeArray<SubmarineKinematicState> states,
            NativeArray<SubmarineForceAccumulator> forces,
            NativeArray<SubmarineKinematicConfig> configs,
            JobHandle dependency,
            float fixedDeltaTime,
            uint frame,
            int vehicleCount)
        {
            if (vault == null ||
                !states.IsCreated ||
                !forces.IsCreated ||
                !vault.TryGetBufferHandle<VentStateDTO>(VentsBuffer, out VaultBufferHandle<VentStateDTO> ventHandle) ||
                !vault.TryGetBufferHandle<VolcanicUpdraftSettingsDTO>(SettingsBuffer, out VaultBufferHandle<VolcanicUpdraftSettingsDTO> settingsHandle))
            {
                return dependency;
            }

            dependency = JobHandle.CombineDependencies(dependency, _pendingVentWriteHandle);
            NativeArray<VentStateDTO> vents = ventHandle.Resolve(vault);
            NativeArray<VolcanicUpdraftSettingsDTO> settingsArray = settingsHandle.Resolve(vault);
            if (!vents.IsCreated || !settingsArray.IsCreated || settingsArray.Length <= 0)
                return dependency;

            VolcanicSubmarineUpdraftInjectionJob job = new VolcanicSubmarineUpdraftInjectionJob
            {
                States = states,
                Forces = forces,
                Configs = configs,
                Vents = vents,
                Settings = settingsArray[0],
                FixedDeltaTime = fixedDeltaTime,
                Frame = frame,
                VehicleCount = vehicleCount
            };

            JobHandle handle = job.Schedule(math.max(1, vehicleCount), SubmarineDynamicsConstants.IntegratorBatchSize, dependency);
            _pendingVentReadHandle = JobHandle.CombineDependencies(_pendingVentReadHandle, handle);
            return handle;
        }

        public static JobHandle ConsumePendingVentReaders()
        {
            JobHandle handle = _pendingVentReadHandle;
            _pendingVentReadHandle = default;
            return handle;
        }

        public static void PublishVentWriteHandle(JobHandle handle)
        {
            _pendingVentWriteHandle = handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VolcanicUpdraftSettingsDTO DefaultSettings(float qualityWeight)
        {
            return new VolcanicUpdraftSettingsDTO
            {
                MaxThrust = 24f,
                EruptionFrequency = 0.18f,
                CylinderRadius = 18f,
                MaxHeight = 220f,
                HeatOutput = 1f,
                GlobalQualityWeight = math.saturate(qualityWeight),
                MaxVerticalVelocity = 72f,
                EruptionThreshold = 0.42f,
                EruptionGain = 2.75f,
                AcousticRadius = 360f,
                DebrisCommandIntensity = 1f,
                ThermalBlindnessScale = 1f,
                Frame = 0u,
                VentCount = 8u,
                SourceHash = SourceHash,
                Flags = TelemetryFlagEmergencyVents
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveDebrisLiftWeight(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            return math.step(0.3f, q) * SmoothStep(0.3f, 1f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveTurbulenceGate(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            return math.step(0.3f, q) * SmoothStep(0.3f, 1f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryEvaluateVent(
            in VentStateDTO vent,
            in VolcanicUpdraftSettingsDTO settings,
            double3 entityAup,
            uint salt,
            out float3 vector,
            out float intensity01)
        {
            vector = float3.zero;
            intensity01 = 0f;

            float radius = SafePositive(vent.Radius, settings.CylinderRadius);
            float maxHeight = SafePositive(settings.MaxHeight, 1f);
            float maxThrust = SafePositive(settings.MaxThrust, 1f);
            float active01 = math.saturate(vent.ThrustPower * math.rcp(maxThrust));
            if (active01 <= 0.0001f)
                return false;

            float turbulenceGate = ResolveTurbulenceGate(settings.GlobalQualityWeight);
            float3 strictUp = new float3(0f, 1f, 0f);
            float3 up = strictUp;
            if (turbulenceGate > 0.0001f)
            {
                float3 authoredUp = math.normalizesafe(vent.UpVector, strictUp);
                up = math.normalizesafe(math.lerp(strictUp, authoredUp, turbulenceGate), strictUp);
            }
            double3 deltaD = entityAup - vent.AUP;
            if (!math.all(math.isfinite(deltaD)))
                return false;

            float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
            float axial = math.dot(delta, up);
            float height01 = math.saturate(axial * math.rcp(maxHeight));
            float coneRadius = radius * math.lerp(1f, 0.35f, height01 * height01);
            float3 radial = delta - (up * axial);
            float radialSq = math.lengthsq(radial);
            float radiusSq = math.max(0.0001f, coneRadius * coneRadius);
            bool inside = radialSq < radiusSq && axial > 0f && axial < maxHeight;
            if (!inside)
                return false;

            float radial01 = math.saturate(1f - (radialSq * math.rcp(radiusSq)));
            float verticalFalloff = height01 * (1f - height01);
            verticalFalloff = math.saturate(verticalFalloff * 4f);
            intensity01 = math.saturate(active01 * radial01 * verticalFalloff);
            if (intensity01 <= 0.0001f)
                return false;

            if (turbulenceGate <= 0.0001f)
            {
                vector = strictUp;
                return true;
            }

            float turbulence01 = turbulenceGate * turbulenceGate;
            float3 radialDir = math.normalizesafe(radial, new float3(1f, 0f, 0f));
            float3 tangent = math.normalizesafe(math.cross(up, radialDir), new float3(0f, 0f, 1f));
            float twist = TriangleSigned((vent.EruptionTimer * 3.0f) + (Hash01(salt) * 0.73f));
            float3 turbulentVector = math.normalizesafe(
                up + (tangent * twist * 0.48f * turbulence01) + (radialDir * 0.16f * turbulence01),
                up);
            vector = math.normalizesafe(math.lerp(strictUp, turbulentVector, turbulenceGate), strictUp);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Hash01(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Triangle01(float phase)
        {
            float t = math.frac(phase);
            return 1f - math.abs((t * 2f) - 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float TriangleSigned(float phase)
        {
            return (Triangle01(phase) * 2f) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(0.0001f, edge1 - edge0)));
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SafePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) & value > 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & lengthSq > 0.0001f;
            return math.select(fallback, value * math.rsqrt(math.max(lengthSq, 0.0001f)), valid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicCountersResetJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;

        public void Execute(int index)
        {
            Counters[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicEruptionCycleJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VentStateDTO> Vents;
        [NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;
        public VolcanicUpdraftSettingsDTO Settings;
        public float FixedDeltaTime;
        public uint Frame;

        public void Execute(int index)
        {
            int ventCount = math.min((int)Settings.VentCount, Vents.Length);
            if ((uint)index >= (uint)ventCount)
                return;

            VentStateDTO vent = Vents[index];
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            float frequency = VolcanicUpdraftVault.SafePositive(Settings.EruptionFrequency, 0.1f);
            float phase = math.frac(vent.EruptionTimer + (dt * frequency * (1f + index * 0.0375f)));
            float waveA = VolcanicUpdraftVault.Triangle01(phase + index * 0.137f);
            float waveB = VolcanicUpdraftVault.Triangle01((phase * 0.6180339f) + VolcanicUpdraftVault.Hash01(Frame + (uint)index * 41u));
            float waveC = VolcanicUpdraftVault.Triangle01((phase * 1.9318516f) + 0.25f);
            float interference = (waveA * 0.54f) + (waveB * 0.31f) + (waveC * 0.15f);
            float pulse = math.saturate((interference - Settings.EruptionThreshold) * Settings.EruptionGain);

            vent.EruptionTimer = phase;
            vent.Radius = VolcanicUpdraftVault.SafePositive(vent.Radius, Settings.CylinderRadius);
            vent.UpVector = math.normalizesafe(vent.UpVector, new float3(0f, 1f, 0f));
            vent.ThrustPower = VolcanicUpdraftVault.SafePositive(Settings.MaxThrust, 1f) * pulse;
            Vents[index] = vent;

            if ((uint)index < (uint)Counters.Length)
            {
                VolcanicUpdraftFrameCounter counter = default;
                counter.ActiveEruptions = pulse > 0.65f ? 1 : 0;
                counter.PeakIntensity01 = pulse;
                counter.PrimaryVentAup = vent.AUP;
                counter.StateHash = VolcanicUpdraftVault.Mix(VolcanicUpdraftVault.SourceHash, math.asuint(pulse));
                counter.NanFlag = math.all(math.isfinite(vent.AUP)) & math.all(math.isfinite(vent.UpVector)) & math.isfinite(vent.ThrustPower) ? 0 : 1;
                Counters[index] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicSubmarineUpdraftInjectionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SubmarineKinematicState> States;
        [NoAlias] public NativeArray<SubmarineForceAccumulator> Forces;
        [ReadOnly, NoAlias] public NativeArray<SubmarineKinematicConfig> Configs;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        public VolcanicUpdraftSettingsDTO Settings;
        public float FixedDeltaTime;
        public uint Frame;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)Forces.Length)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineForceAccumulator force = Forces[index];
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            float3 deltaVelocity = float3.zero;
            float3 forceMirror = float3.zero;
            float peak = 0f;
            int lifted = 0;
            int ventCount = math.min((int)Settings.VentCount, Vents.Length);
            float mass = math.max(1f, state.TotalMassKg);

            for (int i = 0; i < ventCount; i++)
            {
                VentStateDTO vent = Vents[i];
                if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, state.Aup, Frame + (uint)(index * 131 + i * 17), out float3 vector, out float intensity))
                    continue;

                float acceleration = vent.ThrustPower * intensity;
                deltaVelocity += vector * acceleration * dt;
                forceMirror += vector * acceleration * mass;
                peak = math.max(peak, intensity);
                lifted = 1;
            }

            if (lifted != 0)
            {
                float verticalDrag = math.min(0f, force.LastDragWorld.y);
                state.LinearVelocity.y += (-verticalDrag * math.rcp(mass)) * dt * 0.9f;
                state.LinearVelocity += deltaVelocity;
                state.LinearVelocity.y = math.min(state.LinearVelocity.y, VolcanicUpdraftVault.SafePositive(Settings.MaxVerticalVelocity, 72f));
                state.LinearVelocity = math.clamp(state.LinearVelocity, new float3(-90f, -90f, -90f), new float3(90f, 140f, 90f));
                force.LinearForceWorld += forceMirror;
                force.Flags |= VolcanicUpdraftVault.ForceFlagVolcanicUpdraft;
            }

            bool finite = math.all(math.isfinite(state.Aup)) &&
                          math.all(math.isfinite(state.LinearVelocity)) &&
                          math.all(math.isfinite(force.LinearForceWorld));
            if (!finite)
            {
                state.Flags |= SubmarineDynamicsConstants.StateFlagFatalNan;
                state.LinearVelocity = float3.zero;
                force.LinearForceWorld = float3.zero;
            }

            Forces[index] = force;
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicMockEntityInjectionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MockSubmarineArray> MockSubmarines;
        [NoAlias] public NativeArray<MockLeviathanVelocityDTO> MockLeviathans;
        [NoAlias] public NativeArray<MockDebrisParticleDTO> MockDebris;
        [NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        public VolcanicUpdraftSettingsDTO Settings;
        public float FixedDeltaTime;
        public uint Frame;
        public int MockSubmarineCount;
        public int MockLeviathanCount;
        public int MockDebrisCount;

        public void Execute(int index)
        {
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            VolcanicUpdraftFrameCounter counter = default;
            int ventCount = math.min((int)Settings.VentCount, Vents.Length);

            if ((uint)index < (uint)MockSubmarineCount && (uint)index < (uint)MockSubmarines.Length)
            {
                MockSubmarineArray entity = MockSubmarines[index];
                float3 deltaVelocity = float3.zero;
                float peak = 0f;
                for (int i = 0; i < ventCount; i++)
                {
                    VentStateDTO vent = Vents[i];
                    if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, entity.AUP, Frame + (uint)(index * 17 + i * 31), out float3 vector, out float intensity))
                        continue;

                    deltaVelocity += vector * vent.ThrustPower * intensity * dt;
                    peak = math.max(peak, intensity);
                }

                if (peak > 0f)
                {
                    entity.Velocity += deltaVelocity;
                    entity.Velocity.y = math.min(entity.Velocity.y, Settings.MaxVerticalVelocity);
                    entity.LiftScalar = peak;
                    entity.Flags |= 1u;
                    MockSubmarines[index] = entity;
                    counter.EntitiesLifted += 1;
                    counter.PeakIntensity01 = math.max(counter.PeakIntensity01, peak);
                }
            }

            if ((uint)index < (uint)MockLeviathanCount && (uint)index < (uint)MockLeviathans.Length)
            {
                MockLeviathanVelocityDTO leviathan = MockLeviathans[index];
                float3 deltaVelocity = float3.zero;
                float peak = 0f;
                for (int i = 0; i < ventCount; i++)
                {
                    VentStateDTO vent = Vents[i];
                    if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, leviathan.AUP, Frame + (uint)(index * 43 + i * 19), out float3 vector, out float intensity))
                        continue;

                    deltaVelocity += vector * vent.ThrustPower * intensity * dt;
                    peak = math.max(peak, intensity);
                }

                if (peak > 0f)
                {
                    leviathan.Velocity += deltaVelocity;
                    leviathan.DesiredDirection = math.normalizesafe(math.lerp(leviathan.DesiredDirection, new float3(0f, 1f, 0f), peak), new float3(0f, 1f, 0f));
                    leviathan.RideStaminaSaved01 = math.saturate(peak);
                    leviathan.Flags |= 1u;
                    MockLeviathans[index] = leviathan;
                    counter.LeviathansLifted += 1;
                    counter.PeakIntensity01 = math.max(counter.PeakIntensity01, peak);
                }
            }

            if ((uint)index < (uint)MockDebrisCount && (uint)index < (uint)MockDebris.Length)
            {
                float debrisLiftWeight = VolcanicUpdraftVault.ResolveDebrisLiftWeight(Settings.GlobalQualityWeight);
                MockDebrisParticleDTO debris = MockDebris[index];
                float3 deltaVelocity = float3.zero;
                float peak = 0f;
                if (debrisLiftWeight > 0.0001f)
                {
                    for (int i = 0; i < ventCount; i++)
                    {
                        VentStateDTO vent = Vents[i];
                        if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, debris.AUP, Frame + (uint)(index * 7 + i * 61), out float3 vector, out float intensity))
                            continue;

                        deltaVelocity += vector * vent.ThrustPower * intensity * dt * debrisLiftWeight;
                        peak = math.max(peak, intensity * debrisLiftWeight);
                    }
                }

                debris.LiftWeight = debrisLiftWeight;
                if (peak > 0f)
                {
                    debris.Velocity += deltaVelocity;
                    debris.Flags |= 1u;
                    counter.DebrisLifted += 1;
                }
                else if (debrisLiftWeight <= 0.0001f)
                {
                    debris.Flags |= VolcanicUpdraftVault.TelemetryFlagDebrisCulled;
                }

                MockDebris[index] = debris;
            }

            int counterIndex = 160 + index;
            if ((uint)counterIndex < (uint)Counters.Length)
                Counters[counterIndex] = counter;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicPlayerUpdraftInjectionJob : IJob
    {
        [NoAlias] public NativeArray<PlayerKinematicState> PlayerState;
        [NoAlias] public NativeArray<VolcanicPlayerHeatSignalDTO> HeatSignal;
        [NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        public VolcanicUpdraftSettingsDTO Settings;
        public float FixedDeltaTime;
        public uint Frame;

        public void Execute()
        {
            if (!PlayerState.IsCreated || PlayerState.Length <= 0)
                return;

            PlayerKinematicState state = PlayerState[0];
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            float3 deltaVelocity = float3.zero;
            float peak = 0f;
            double3 primaryAup = default;
            int ventCount = math.min((int)Settings.VentCount, Vents.Length);

            for (int i = 0; i < ventCount; i++)
            {
                VentStateDTO vent = Vents[i];
                if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, state.Aup, Frame + (uint)(i * 29), out float3 vector, out float intensity))
                    continue;

                deltaVelocity += vector * vent.ThrustPower * intensity * dt;
                if (intensity > peak)
                {
                    peak = intensity;
                    primaryAup = vent.AUP;
                }
            }

            if (peak > 0f)
            {
                state.Velocity += deltaVelocity;
                state.Velocity.y = math.min(state.Velocity.y, VolcanicUpdraftVault.SafePositive(Settings.MaxVerticalVelocity, 72f));
                PlayerState[0] = state;
            }

            if (HeatSignal.IsCreated && HeatSignal.Length > 0)
            {
                HeatSignal[0] = new VolcanicPlayerHeatSignalDTO
                {
                    AUP = state.Aup,
                    Heat01 = math.saturate(peak * Settings.HeatOutput),
                    Blindness01 = math.saturate(peak * Settings.ThermalBlindnessScale),
                    Radius = Settings.CylinderRadius,
                    Intensity01 = peak,
                    Frame = Frame,
                    SourceHash = VolcanicUpdraftVault.SourceHash,
                    Flags = peak > 0f ? 1u : 0u
                };
            }

            if (Counters.IsCreated && Counters.Length > 128)
            {
                VolcanicUpdraftFrameCounter counter = default;
                counter.EntitiesLifted = peak > 0f ? 1 : 0;
                counter.PeakIntensity01 = peak;
                counter.PrimaryVentAup = primaryAup;
                Counters[128] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicLeviathanUpdraftInjectionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AlphaLeviathanCognitionState> States;
        [NoAlias] public NativeArray<AlphaLeviathanSteeringOutput> SteeringOutputs;
        [NoAlias] public NativeArray<VolcanicFloatStateSignal> FloatSignals;
        [NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        public VolcanicUpdraftSettingsDTO Settings;
        public float FixedDeltaTime;
        public uint Frame;
        public int LeviathanCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)LeviathanCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)SteeringOutputs.Length)
            {
                return;
            }

            AlphaLeviathanCognitionState state = States[index];
            AlphaLeviathanSteeringOutput output = SteeringOutputs[index];
            double3 aup = state.LeviathanAup.ToAbsoluteDouble3();
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            float3 liftVelocity = float3.zero;
            float3 liftVector = new float3(0f, 1f, 0f);
            float peak = 0f;
            int ventCount = math.min((int)Settings.VentCount, Vents.Length);

            for (int i = 0; i < ventCount; i++)
            {
                VentStateDTO vent = Vents[i];
                if (!VolcanicUpdraftVault.TryEvaluateVent(in vent, in Settings, aup, Frame + (uint)(index * 71 + i * 11), out float3 vector, out float intensity))
                    continue;

                liftVelocity += vector * vent.ThrustPower * intensity * dt;
                if (intensity > peak)
                {
                    peak = intensity;
                    liftVector = vector;
                }
            }

            if (peak > 0f)
            {
                output.TargetRuntimeOffsetMeters += liftVelocity;
                output.DesiredDirection = math.normalizesafe(math.lerp(output.DesiredDirection, liftVector, math.saturate(peak)), liftVector);
                output.WakeSiltIntensity01 = math.max(output.WakeSiltIntensity01, peak);
                output.VisualOverkill01 = math.max(output.VisualOverkill01, Settings.GlobalQualityWeight);
                output.ParticleOverkillBudget01 = math.max(output.ParticleOverkillBudget01, peak * Settings.GlobalQualityWeight);
                output.IntentFlags = (byte)(output.IntentFlags | (1 << 7));
                SteeringOutputs[index] = output;

                if ((uint)index < (uint)FloatSignals.Length)
                {
                    FloatSignals[index] = new VolcanicFloatStateSignal
                    {
                        AUP = aup,
                        UpVector = liftVector,
                        Intensity01 = peak,
                        Frame = Frame,
                        EntityHash = state.StateHash,
                        Slot = state.Slot,
                        StateKind = (byte)VolcanicUpdraftVault.FloatStateKindThermalRide,
                        Flags = 1
                    };
                }
            }

            int counterIndex = 96 + index;
            if ((uint)counterIndex < (uint)Counters.Length)
            {
                VolcanicUpdraftFrameCounter counter = default;
                counter.LeviathansLifted = peak > 0f ? 1 : 0;
                counter.PeakIntensity01 = peak;
                counter.PrimaryVentAup = aup;
                Counters[counterIndex] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicVisualFakesJob : IJob
    {
        [NoAlias] public NativeArray<VfxDynamicWakeDTO> DynamicWakes;
        [NoAlias] public NativeArray<VfxMockFlowField> MockFlowField;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        public VolcanicUpdraftSettingsDTO Settings;
        public double3 RuntimeOriginAup;

        public void Execute()
        {
            int wakeCount = DynamicWakes.IsCreated ? DynamicWakes.Length : 0;
            int ventCount = math.min(math.min((int)Settings.VentCount, Vents.Length), wakeCount);
            float3 flow = float3.zero;
            float peak = 0f;

            for (int i = 0; i < wakeCount; i++)
            {
                if (i >= ventCount)
                {
                    DynamicWakes[i] = default;
                    continue;
                }

                VentStateDTO vent = Vents[i];
                double3 localD = vent.AUP - RuntimeOriginAup;
                float3 local = new float3((float)localD.x, (float)localD.y, (float)localD.z);
                float maxThrust = VolcanicUpdraftVault.SafePositive(Settings.MaxThrust, 1f);
                float intensity = math.saturate(vent.ThrustPower * math.rcp(maxThrust));
                float3 up = math.normalizesafe(vent.UpVector, new float3(0f, 1f, 0f));
                DynamicWakes[i] = new VfxDynamicWakeDTO
                {
                    Position = local,
                    Radius = VolcanicUpdraftVault.SafePositive(vent.Radius, Settings.CylinderRadius),
                    Force = up * vent.ThrustPower * intensity,
                    Falloff = intensity
                };
                flow += up * intensity;
                peak = math.max(peak, intensity);
            }

            if (MockFlowField.IsCreated && MockFlowField.Length > 0)
            {
                MockFlowField[0] = new VfxMockFlowField
                {
                    GlobalFlow = flow,
                    CurlStrength = Settings.GlobalQualityWeight * peak,
                    NoiseAnchor = float3.zero,
                    DensityScale = math.lerp(0.15f, 1f, Settings.GlobalQualityWeight)
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VolcanicTelemetryFinalizeJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<VolcanicUpdraftFrameCounter> Counters;
        [ReadOnly, NoAlias] public NativeArray<VentStateDTO> Vents;
        [NoAlias] public NativeArray<VolcanicUpdraftTelemetryEntry> Telemetry;
        public VolcanicUpdraftSettingsDTO Settings;
        public uint Frame;

        public void Execute()
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int active = 0;
            int lifted = 0;
            int debris = 0;
            int leviathans = 0;
            int nan = 0;
            float peak = 0f;
            double3 primary = Vents.IsCreated && Vents.Length > 0 ? Vents[0].AUP : default;
            uint hash = VolcanicUpdraftVault.SourceHash;
            float computeMs = 0f;

            for (int i = 0; i < Counters.Length; i++)
            {
                VolcanicUpdraftFrameCounter counter = Counters[i];
                active += math.max(0, counter.ActiveEruptions);
                lifted += math.max(0, counter.EntitiesLifted);
                debris += math.max(0, counter.DebrisLifted);
                leviathans += math.max(0, counter.LeviathansLifted);
                nan |= counter.NanFlag;
                computeMs += math.max(0f, counter.EstimatedComputeMs);
                if (counter.PeakIntensity01 > peak)
                {
                    peak = counter.PeakIntensity01;
                    primary = counter.PrimaryVentAup;
                }

                hash = VolcanicUpdraftVault.Mix(hash, counter.StateHash);
            }

            uint flags = Settings.Flags;
            flags |= nan != 0 ? VolcanicUpdraftVault.TelemetryFlagNaN : 0u;
            flags |= VolcanicUpdraftVault.ResolveDebrisLiftWeight(Settings.GlobalQualityWeight) <= 0.0001f
                ? VolcanicUpdraftVault.TelemetryFlagDebrisCulled
                : 0u;

            int slot = (int)(Frame % VolcanicUpdraftVault.TelemetryFrames);
            if ((uint)slot >= (uint)Telemetry.Length)
                slot = 0;

            Telemetry[slot] = new VolcanicUpdraftTelemetryEntry
            {
                PrimaryVentAup = primary,
                LastVector = new float3(0f, peak, 0f),
                CylinderComputeTimeMs = computeMs + ((math.max(1, (int)Settings.VentCount) * 0.015f) * 0.001f),
                Frame = Frame,
                StateHash = hash,
                ActiveEruptions = (ushort)math.min(active, ushort.MaxValue),
                EntitiesLifted = (ushort)math.min(lifted, ushort.MaxValue),
                DebrisLifted = (ushort)math.min(debris, ushort.MaxValue),
                LeviathansLifted = (ushort)math.min(leviathans, ushort.MaxValue),
                Flags = flags
            };
        }
    }

    public sealed class VolcanicUpdraftDirector : MonoBehaviour, IDispatcherFixedSystem, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const SystemID OwnerSystem = SystemID.Fluid;
        private const uint FixedSystemHash = 0x56555044u; // VUPD
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_VOLCANO_SURGEON.bin";
        private const string CsvFileName = "volcanic_vents.csv";
        private const uint HashMaxThrust = 0xA65BEA1Cu;
        private const uint HashEruptionFrequency = 0xA5EA12B0u;
        private const uint HashCylinderRadius = 0xD258DB16u;
        private const uint HashMaxHeight = 0xB37541C1u;
        private const uint HashHeatOutput = 0x800A1669u;
        private const uint HashGlobalQualityWeight = 0xB00FB719u;
        private const uint HashVent = 0xF8173F5Eu;

        [Header("Vault")]
        [SerializeField, Range(1, VolcanicUpdraftVault.MaxVents)] private int maxVentCount = 8;
        [SerializeField, Range(0, VolcanicUpdraftVault.MockSubmarineCapacity)] private int mockSubmarineCount = 4;
        [SerializeField, Range(0, VolcanicUpdraftVault.MockLeviathanCapacity)] private int mockLeviathanCount = 2;
        [SerializeField, Range(0, VolcanicUpdraftVault.MockDebrisCapacity)] private int mockDebrisCount = 32;

        [Header("Updraft")]
        [SerializeField, Min(1f)] private float maxThrust = 24f;
        [SerializeField, Min(0.001f)] private float eruptionFrequency = 0.18f;
        [SerializeField, Min(0.25f)] private float cylinderRadius = 18f;
        [SerializeField, Min(1f)] private float maxHeight = 220f;
        [SerializeField, Min(0f)] private float heatOutput = 1f;
        [SerializeField, Range(0f, 1f)] private float editorQualityWeight = 1f;
        [SerializeField] private bool forceEditorQualityWeight;
        [SerializeField] private bool drawGizmos = true;

        private IDataVault _dataVault;
        private VaultBufferHandle<VentStateDTO> _ventHandle;
        private VaultBufferHandle<VolcanicUpdraftSettingsDTO> _settingsHandle;
        private VaultBufferHandle<VolcanicUpdraftTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<MockSubmarineArray> _mockSubmarineHandle;
        private VaultBufferHandle<MockLeviathanVelocityDTO> _mockLeviathanHandle;
        private VaultBufferHandle<MockDebrisParticleDTO> _mockDebrisHandle;
        private VaultBufferHandle<VolcanicFloatStateSignal> _floatSignalHandle;
        private VaultBufferHandle<VfxDynamicWakeDTO> _dynamicWakeHandle;
        private VaultBufferHandle<VfxMockFlowField> _mockFlowFieldHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<VolcanicUpdraftFrameCounter> _counterHandle;
        private VaultBufferHandle<VolcanicPlayerHeatSignalDTO> _playerHeatHandle;
        private VaultBufferHandle<PlayerKinematicState> _playerStateHandle;
        private VaultBufferHandle<AlphaLeviathanCognitionState> _leviathanStateHandle;
        private VaultBufferHandle<AlphaLeviathanSteeringOutput> _leviathanOutputHandle;
        private IThermodynamicsService _thermodynamicsService;

        private JobHandle _jobHandle;
        private bool _fixedPipelineScheduled;
        private bool _buffersReady;
        private bool _ownBuffersLocked;
        private bool _playerLocked;
        private bool _leviathanLocked;
        private bool _registeredFixedDispatcher;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _faultDumped;
        private uint _frame;
        private long _csvLastWriteTicks;
        private string _csvPath;

        public static VolcanicUpdraftDirector ActiveRuntimeInstance;

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            ResolveDataVault();
            ResolveColdRegistryDependencies();
            EnsureVaultBuffers();
            if (Application.isPlaying)
            {
                _registeredFixedDispatcher = GlobalRegistry.TryRegisterDispatcherFixedSystem(this);
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void OnDisable()
        {
            if (_fixedPipelineScheduled)
            {
                DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true);
                _fixedPipelineScheduled = false;
            }

            UnlockExternalBuffers();
            UnlockOwnBuffers();
            if (_registeredFixedDispatcher)
                GlobalRegistry.UnregisterDispatcherFixedSystem(this);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredHotSwap)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
            _thermodynamicsService = null;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public uint GetFixedSystemIdHash()
        {
            return FixedSystemHash;
        }

        public JobHandle ScheduleFixedSimulation(in DispatcherTimingDTO timing, JobHandle dependsOn)
        {
            if (_fixedPipelineScheduled || !_buffersReady || !ResolveDataVault())
                return dependsOn;

            if (!TryResolveCoreArrays(
                    out NativeArray<VentStateDTO> vents,
                    out NativeArray<VolcanicUpdraftSettingsDTO> settingsArray,
                    out NativeArray<VolcanicUpdraftTelemetryEntry> telemetry,
                    out NativeArray<MockSubmarineArray> mockSubmarines,
                    out NativeArray<MockLeviathanVelocityDTO> mockLeviathans,
                    out NativeArray<MockDebrisParticleDTO> mockDebris,
                    out NativeArray<VolcanicFloatStateSignal> floatSignals,
                    out NativeArray<VfxDynamicWakeDTO> dynamicWakes,
                    out NativeArray<VfxMockFlowField> mockFlowField,
                    out NativeArray<VolcanicUpdraftFrameCounter> counters,
                    out NativeArray<VolcanicPlayerHeatSignalDTO> playerHeat))
            {
                _buffersReady = false;
                return dependsOn;
            }

            if (!LockOwnBuffers())
                return dependsOn;

            VolcanicUpdraftSettingsDTO settings = SanitizeSettings(settingsArray[0]);
            settings.Frame = ++_frame;
            settings.GlobalQualityWeight = ResolveGlobalQualityWeight();
            settingsArray[0] = settings;
            float fixedDeltaTime = math.clamp(timing.FixedDelta, 0.001f, 0.05f);

            JobHandle initialDependency = JobHandle.CombineDependencies(dependsOn, VolcanicUpdraftVault.ConsumePendingVentReaders());
            JobHandle handle = new VolcanicCountersResetJob
            {
                Counters = counters
            }.Schedule(counters.Length, 64, initialDependency);

            handle = new VolcanicEruptionCycleJob
            {
                Vents = vents,
                Counters = counters,
                Settings = settings,
                FixedDeltaTime = fixedDeltaTime,
                Frame = _frame
            }.Schedule(math.max(1, (int)settings.VentCount), 8, handle);
            VolcanicUpdraftVault.PublishVentWriteHandle(handle);

            handle = new VolcanicMockEntityInjectionJob
            {
                MockSubmarines = mockSubmarines,
                MockLeviathans = mockLeviathans,
                MockDebris = mockDebris,
                Counters = counters,
                Vents = vents,
                Settings = settings,
                FixedDeltaTime = fixedDeltaTime,
                Frame = _frame,
                MockSubmarineCount = mockSubmarineCount,
                MockLeviathanCount = mockLeviathanCount,
                MockDebrisCount = mockDebrisCount
            }.Schedule(math.max(math.max(mockSubmarineCount, mockLeviathanCount), mockDebrisCount), 16, handle);

            if (TryLockPlayerBuffer(out NativeArray<PlayerKinematicState> playerState))
            {
                handle = new VolcanicPlayerUpdraftInjectionJob
                {
                    PlayerState = playerState,
                    HeatSignal = playerHeat,
                    Counters = counters,
                    Vents = vents,
                    Settings = settings,
                    FixedDeltaTime = fixedDeltaTime,
                    Frame = _frame
                }.Schedule(handle);
            }

            if (TryLockLeviathanBuffers(out NativeArray<AlphaLeviathanCognitionState> leviathanStates, out NativeArray<AlphaLeviathanSteeringOutput> leviathanOutputs))
            {
                int leviathanCount = math.min(leviathanStates.Length, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
                handle = new VolcanicLeviathanUpdraftInjectionJob
                {
                    States = leviathanStates,
                    SteeringOutputs = leviathanOutputs,
                    FloatSignals = floatSignals,
                    Counters = counters,
                    Vents = vents,
                    Settings = settings,
                    FixedDeltaTime = fixedDeltaTime,
                    Frame = _frame,
                    LeviathanCount = leviathanCount
                }.Schedule(math.max(1, leviathanCount), 16, handle);
            }

            handle = new VolcanicVisualFakesJob
            {
                DynamicWakes = dynamicWakes,
                MockFlowField = mockFlowField,
                Vents = vents,
                Settings = settings,
                RuntimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble
            }.Schedule(handle);

            handle = new VolcanicTelemetryFinalizeJob
            {
                Counters = counters,
                Vents = vents,
                Telemetry = telemetry,
                Settings = settings,
                Frame = _frame
            }.Schedule(handle);

            _jobHandle = handle;
            _fixedPipelineScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, handle);
            return handle;
        }

        public void PostFixedSimulation(in DispatcherTimingDTO timing)
        {
            if (!_fixedPipelineScheduled)
                return;

            _jobHandle = default;
            _fixedPipelineScheduled = false;
            UnlockExternalBuffers();
            UnlockOwnBuffers();
            DumpBlackBoxIfFaulted();
        }

        public void SlowTick()
        {
            ResolveDataVault();
            if (!_fixedPipelineScheduled && !_ownBuffersLocked)
                EnsureVaultBuffers();

            RefreshExternalHandles();
            TryApplyCsvOverrides();
        }

        public void LateFrameTick()
        {
            if (_fixedPipelineScheduled || !_buffersReady || !ResolveDataVault())
                return;

            if (!TryResolveCoreArrays(
                    out NativeArray<VentStateDTO> vents,
                    out NativeArray<VolcanicUpdraftSettingsDTO> settings,
                    out NativeArray<VolcanicUpdraftTelemetryEntry> telemetry,
                    out _,
                    out _,
                    out _,
                    out NativeArray<VolcanicFloatStateSignal> floatSignals,
                    out _,
                    out _,
                    out _,
                    out NativeArray<VolcanicPlayerHeatSignalDTO> playerHeat))
            {
                return;
            }

            VolcanicUpdraftTelemetryEntry entry = telemetry[(int)(_frame % VolcanicUpdraftVault.TelemetryFrames)];
            PublishPresentationSignals(in entry, vents, settings[0], floatSignals, playerHeat);
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
                _thermodynamicsService = currentService as IThermodynamicsService;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
                _thermodynamicsService = currentService as IThermodynamicsService;
        }

        public bool TryGetVentReadback(int index, out VentStateDTO vent, out VolcanicUpdraftSettingsDTO settings)
        {
            vent = default;
            settings = default;
            if (!ResolveDataVault())
                return false;

            NativeArray<VentStateDTO> vents = _ventHandle.Resolve(_dataVault);
            NativeArray<VolcanicUpdraftSettingsDTO> settingsArray = _settingsHandle.Resolve(_dataVault);
            if (!vents.IsCreated || !settingsArray.IsCreated || settingsArray.Length <= 0 || (uint)index >= (uint)vents.Length)
                return false;

            vent = vents[index];
            settings = settingsArray[0];
            return true;
        }

        public bool TryWriteSettingsFromEditor(float editorMaxThrust, float editorEruptionFrequency, float editorCylinderRadius, float editorHeatOutput)
        {
            if (!ResolveDataVault())
                return false;

            NativeArray<VolcanicUpdraftSettingsDTO> settingsArray = _settingsHandle.Resolve(_dataVault);
            if (!settingsArray.IsCreated || settingsArray.Length <= 0)
                return false;

            VolcanicUpdraftSettingsDTO settings = settingsArray[0];
            settings.MaxThrust = math.max(0.01f, editorMaxThrust);
            settings.EruptionFrequency = math.max(0.001f, editorEruptionFrequency);
            settings.CylinderRadius = math.max(0.25f, editorCylinderRadius);
            settings.HeatOutput = math.max(0f, editorHeatOutput);
            settings.SourceHash = VolcanicUpdraftVault.SourceHash;
            settingsArray[0] = SanitizeSettings(settings);
            return true;
        }

        public bool TryUpsertAuthoredVent(uint sourceHash, double3 aup, float radius, float thrustPower, float maxHeight, float heatOutput, float timer01)
        {
            if (_fixedPipelineScheduled || !ResolveDataVault())
                return false;

            NativeArray<VentStateDTO> vents = _ventHandle.Resolve(_dataVault);
            NativeArray<VolcanicUpdraftSettingsDTO> settingsArray = _settingsHandle.Resolve(_dataVault);
            if (!vents.IsCreated || vents.Length <= 0 || !settingsArray.IsCreated || settingsArray.Length <= 0)
                return false;

            VolcanicUpdraftSettingsDTO settings = SanitizeSettings(settingsArray[0]);
            int ventCount = math.clamp((int)settings.VentCount, 1, math.min(vents.Length, VolcanicUpdraftVault.MaxVents));
            int slot = (int)(sourceHash % (uint)ventCount);
            VentStateDTO vent = vents[slot];
            vent.AUP = aup;
            vent.UpVector = new float3(0f, 1f, 0f);
            vent.Radius = math.max(0.25f, radius);
            vent.ThrustPower = math.max(0f, thrustPower);
            vent.EruptionTimer = math.saturate(timer01);
            vent._pad0 = 0u;
            vent._pad1 = 0ul;
            vents[slot] = vent;

            settings.MaxThrust = math.max(settings.MaxThrust, math.max(0.01f, thrustPower));
            settings.CylinderRadius = math.max(settings.CylinderRadius, math.max(0.25f, radius));
            settings.MaxHeight = math.max(settings.MaxHeight, math.max(1f, maxHeight));
            settings.HeatOutput = math.max(settings.HeatOutput, math.saturate(heatOutput));
            settings.SourceHash = VolcanicUpdraftVault.SourceHash;
            settingsArray[0] = SanitizeSettings(settings);
            return true;
        }

        private bool ResolveDataVault()
        {
            if (_dataVault != null)
                return true;

            _dataVault = GlobalRegistry.DataVault;
            if (_dataVault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                _dataVault = latest;

            return _dataVault != null;
        }

        private void ResolveColdRegistryDependencies()
        {
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            if (Application.isPlaying && !_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private bool EnsureVaultBuffers()
        {
            if (!ResolveDataVault())
                return false;

            _ventHandle = _dataVault.GetBufferHandle<VentStateDTO>(VolcanicUpdraftVault.VentsBuffer, math.clamp(maxVentCount, 1, VolcanicUpdraftVault.MaxVents), OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _settingsHandle = _dataVault.GetBufferHandle<VolcanicUpdraftSettingsDTO>(VolcanicUpdraftVault.SettingsBuffer, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _dataVault.GetBufferHandle<VolcanicUpdraftTelemetryEntry>(VolcanicUpdraftVault.TelemetryBuffer, VolcanicUpdraftVault.TelemetryFrames, OwnerSystem, NativeArrayOptions.ClearMemory);
            _mockSubmarineHandle = _dataVault.GetBufferHandle<MockSubmarineArray>(VolcanicUpdraftVault.MockSubmarinesBuffer, VolcanicUpdraftVault.MockSubmarineCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _mockLeviathanHandle = _dataVault.GetBufferHandle<MockLeviathanVelocityDTO>(VolcanicUpdraftVault.MockLeviathansBuffer, VolcanicUpdraftVault.MockLeviathanCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _mockDebrisHandle = _dataVault.GetBufferHandle<MockDebrisParticleDTO>(VolcanicUpdraftVault.MockDebrisBuffer, VolcanicUpdraftVault.MockDebrisCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _floatSignalHandle = _dataVault.GetBufferHandle<VolcanicFloatStateSignal>(VolcanicUpdraftVault.FloatSignalsBuffer, AlphaLeviathanStalkConstants.MaxLeviathanSlots, OwnerSystem, NativeArrayOptions.ClearMemory);
            _dynamicWakeHandle = _dataVault.GetBufferHandle<VfxDynamicWakeDTO>(VolcanicUpdraftVault.DynamicWakesBuffer, VolcanicUpdraftVault.DynamicWakeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _mockFlowFieldHandle = _dataVault.GetBufferHandle<VfxMockFlowField>(VolcanicUpdraftVault.MockFlowFieldBuffer, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _dataVault.GetBufferHandle<byte>(VolcanicUpdraftVault.CsvScratchBuffer, VolcanicUpdraftVault.CsvScratchBytes, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _counterHandle = _dataVault.GetBufferHandle<VolcanicUpdraftFrameCounter>(VolcanicUpdraftVault.FrameCountersBuffer, VolcanicUpdraftVault.CounterCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _playerHeatHandle = _dataVault.GetBufferHandle<VolcanicPlayerHeatSignalDTO>(VolcanicUpdraftVault.PlayerHeatBuffer, 1, OwnerSystem, NativeArrayOptions.ClearMemory);

            if (!_ventHandle.IsCreated || !_settingsHandle.IsCreated || !_telemetryHandle.IsCreated ||
                !_mockSubmarineHandle.IsCreated || !_mockLeviathanHandle.IsCreated || !_mockDebrisHandle.IsCreated ||
                !_floatSignalHandle.IsCreated || !_dynamicWakeHandle.IsCreated || !_mockFlowFieldHandle.IsCreated ||
                !_csvScratchHandle.IsCreated || !_counterHandle.IsCreated || !_playerHeatHandle.IsCreated)
            {
                _buffersReady = false;
                return false;
            }

            NativeArray<VentStateDTO> vents = _ventHandle.Resolve(_dataVault);
            NativeArray<VolcanicUpdraftSettingsDTO> settings = _settingsHandle.Resolve(_dataVault);
            NativeArray<MockSubmarineArray> mockSubmarines = _mockSubmarineHandle.Resolve(_dataVault);
            NativeArray<MockLeviathanVelocityDTO> mockLeviathans = _mockLeviathanHandle.Resolve(_dataVault);
            NativeArray<MockDebrisParticleDTO> mockDebris = _mockDebrisHandle.Resolve(_dataVault);
            if (!vents.IsCreated || !settings.IsCreated || settings.Length <= 0)
                return false;

            if (settings[0].SourceHash == 0u)
            {
                VolcanicUpdraftSettingsDTO initial = VolcanicUpdraftVault.DefaultSettings(ResolveGlobalQualityWeight());
                initial.MaxThrust = maxThrust;
                initial.EruptionFrequency = eruptionFrequency;
                initial.CylinderRadius = cylinderRadius;
                initial.MaxHeight = maxHeight;
                initial.HeatOutput = heatOutput;
                initial.VentCount = (uint)math.clamp(maxVentCount, 1, VolcanicUpdraftVault.MaxVents);
                settings[0] = SanitizeSettings(initial);
                if (!TryLoadLegacyVentBinary(vents, settings[0]))
                    GenerateEmergencyMockVents(vents, settings[0]);
                GenerateMockEntities(mockSubmarines, mockLeviathans, mockDebris, vents, settings[0]);
            }

            _buffersReady = true;
            return true;
        }

        private bool TryResolveCoreArrays(
            out NativeArray<VentStateDTO> vents,
            out NativeArray<VolcanicUpdraftSettingsDTO> settings,
            out NativeArray<VolcanicUpdraftTelemetryEntry> telemetry,
            out NativeArray<MockSubmarineArray> mockSubmarines,
            out NativeArray<MockLeviathanVelocityDTO> mockLeviathans,
            out NativeArray<MockDebrisParticleDTO> mockDebris,
            out NativeArray<VolcanicFloatStateSignal> floatSignals,
            out NativeArray<VfxDynamicWakeDTO> dynamicWakes,
            out NativeArray<VfxMockFlowField> mockFlowField,
            out NativeArray<VolcanicUpdraftFrameCounter> counters,
            out NativeArray<VolcanicPlayerHeatSignalDTO> playerHeat)
        {
            vents = _ventHandle.Resolve(_dataVault);
            settings = _settingsHandle.Resolve(_dataVault);
            telemetry = _telemetryHandle.Resolve(_dataVault);
            mockSubmarines = _mockSubmarineHandle.Resolve(_dataVault);
            mockLeviathans = _mockLeviathanHandle.Resolve(_dataVault);
            mockDebris = _mockDebrisHandle.Resolve(_dataVault);
            floatSignals = _floatSignalHandle.Resolve(_dataVault);
            dynamicWakes = _dynamicWakeHandle.Resolve(_dataVault);
            mockFlowField = _mockFlowFieldHandle.Resolve(_dataVault);
            counters = _counterHandle.Resolve(_dataVault);
            playerHeat = _playerHeatHandle.Resolve(_dataVault);
            return vents.IsCreated &&
                   settings.IsCreated &&
                   settings.Length > 0 &&
                   telemetry.IsCreated &&
                   telemetry.Length >= VolcanicUpdraftVault.TelemetryFrames &&
                   mockSubmarines.IsCreated &&
                   mockLeviathans.IsCreated &&
                   mockDebris.IsCreated &&
                   floatSignals.IsCreated &&
                   dynamicWakes.IsCreated &&
                   mockFlowField.IsCreated &&
                   counters.IsCreated &&
                   counters.Length >= VolcanicUpdraftVault.CounterCapacity &&
                   playerHeat.IsCreated;
        }

        private bool LockOwnBuffers()
        {
            if (_ownBuffersLocked)
                return true;

            if (_dataVault == null)
                return false;

            if (!_dataVault.TryLockBuffer(VolcanicUpdraftVault.VentsBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.SettingsBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.TelemetryBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.MockSubmarinesBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.MockLeviathansBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.MockDebrisBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.FloatSignalsBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.DynamicWakesBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.MockFlowFieldBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.FrameCountersBuffer, OwnerSystem) ||
                !_dataVault.TryLockBuffer(VolcanicUpdraftVault.PlayerHeatBuffer, OwnerSystem))
            {
                UnlockOwnBuffers();
                return false;
            }

            _ownBuffersLocked = true;
            return true;
        }

        private void UnlockOwnBuffers()
        {
            if (_dataVault == null)
                return;

            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.VentsBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.SettingsBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.TelemetryBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.MockSubmarinesBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.MockLeviathansBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.MockDebrisBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.FloatSignalsBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.DynamicWakesBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.MockFlowFieldBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.FrameCountersBuffer, OwnerSystem);
            _dataVault.TryUnlockBuffer(VolcanicUpdraftVault.PlayerHeatBuffer, OwnerSystem);
            _ownBuffersLocked = false;
        }

        private void RefreshExternalHandles()
        {
            if (_dataVault == null)
                return;

            _dataVault.TryGetBufferHandle<PlayerKinematicState>(BufferID.ShinobuSomaticKinematicState, out _playerStateHandle);
            _dataVault.TryGetBufferHandle<AlphaLeviathanCognitionState>(BufferID.AlphaLeviathanCognitionState, out _leviathanStateHandle);
            _dataVault.TryGetBufferHandle<AlphaLeviathanSteeringOutput>(BufferID.AlphaLeviathanSteeringOutput, out _leviathanOutputHandle);
        }

        private bool TryLockPlayerBuffer(out NativeArray<PlayerKinematicState> playerState)
        {
            playerState = default;
            if (!_playerStateHandle.IsCreated ||
                !_dataVault.TryLockBuffer(BufferID.ShinobuSomaticKinematicState, OwnerSystem))
            {
                return false;
            }

            playerState = _playerStateHandle.Resolve(_dataVault);
            if (!playerState.IsCreated || playerState.Length <= 0)
            {
                _dataVault.TryUnlockBuffer(BufferID.ShinobuSomaticKinematicState, OwnerSystem);
                return false;
            }

            _playerLocked = true;
            return true;
        }

        private bool TryLockLeviathanBuffers(
            out NativeArray<AlphaLeviathanCognitionState> states,
            out NativeArray<AlphaLeviathanSteeringOutput> outputs)
        {
            states = default;
            outputs = default;
            if (!_leviathanStateHandle.IsCreated ||
                !_leviathanOutputHandle.IsCreated ||
                !_dataVault.TryLockBuffer(BufferID.AlphaLeviathanSteeringOutput, OwnerSystem))
            {
                return false;
            }

            states = _leviathanStateHandle.Resolve(_dataVault);
            outputs = _leviathanOutputHandle.Resolve(_dataVault);
            if (!states.IsCreated || !outputs.IsCreated)
            {
                _dataVault.TryUnlockBuffer(BufferID.AlphaLeviathanSteeringOutput, OwnerSystem);
                return false;
            }

            _leviathanLocked = true;
            return true;
        }

        private void UnlockExternalBuffers()
        {
            if (_dataVault == null)
                return;

            if (_playerLocked)
                _dataVault.TryUnlockBuffer(BufferID.ShinobuSomaticKinematicState, OwnerSystem);
            if (_leviathanLocked)
                _dataVault.TryUnlockBuffer(BufferID.AlphaLeviathanSteeringOutput, OwnerSystem);
            _playerLocked = false;
            _leviathanLocked = false;
        }

        private static VolcanicUpdraftSettingsDTO SanitizeSettings(VolcanicUpdraftSettingsDTO settings)
        {
            settings.MaxThrust = math.clamp(settings.MaxThrust, 0.01f, 240f);
            settings.EruptionFrequency = math.clamp(settings.EruptionFrequency, 0.001f, 4f);
            settings.CylinderRadius = math.clamp(settings.CylinderRadius, 0.25f, 512f);
            settings.MaxHeight = math.clamp(settings.MaxHeight, 1f, 2000f);
            settings.HeatOutput = math.clamp(settings.HeatOutput, 0f, 25f);
            settings.GlobalQualityWeight = math.saturate(math.isfinite(settings.GlobalQualityWeight) ? settings.GlobalQualityWeight : 1f);
            settings.MaxVerticalVelocity = math.clamp(settings.MaxVerticalVelocity, 1f, 140f);
            settings.EruptionThreshold = math.clamp(settings.EruptionThreshold, 0.01f, 0.95f);
            settings.EruptionGain = math.clamp(settings.EruptionGain, 0.1f, 24f);
            settings.AcousticRadius = math.clamp(settings.AcousticRadius, 1f, 4096f);
            settings.DebrisCommandIntensity = math.saturate(settings.DebrisCommandIntensity);
            settings.ThermalBlindnessScale = math.clamp(settings.ThermalBlindnessScale, 0f, 8f);
            settings.VentCount = (uint)math.clamp((int)settings.VentCount, 1, VolcanicUpdraftVault.MaxVents);
            settings.SourceHash = settings.SourceHash == 0u ? VolcanicUpdraftVault.SourceHash : settings.SourceHash;
            return settings;
        }

        private float ResolveGlobalQualityWeight()
        {
            float quality = forceEditorQualityWeight ? editorQualityWeight : HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private bool TryLoadLegacyVentBinary(NativeArray<VentStateDTO> vents, VolcanicUpdraftSettingsDTO settings)
        {
            string root = Application.dataPath;
            root = Directory.GetParent(root)?.FullName ?? root;
            return TryReadLegacyVentBinary(Path.Combine(root, "Docs", "Archive", "volcanic_vent_locations.h8bin"), vents, settings) ||
                   TryReadLegacyVentBinary(Path.Combine(root, "Assets", "StreamingAssets", "volcanic_vent_locations.h8bin"), vents, settings) ||
                   TryReadLegacyVentBinary(Path.Combine(root, "StreamingAssets", "volcanic_vent_locations.h8bin"), vents, settings);
        }

        private static bool TryReadLegacyVentBinary(string path, NativeArray<VentStateDTO> vents, VolcanicUpdraftSettingsDTO settings)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan);
                if (stream.Length < 64L)
                    return false;

                int count = math.min(vents.Length, (int)(stream.Length / 64L));
                byte[] record = new byte[64];
                for (int i = 0; i < count; i++)
                {
                    if (!TryReadExact(stream, record))
                        return i > 0;

                    VentStateDTO vent = default;
                    vent.AUP = new double3(
                        ReadDoubleLittleEndian(record, 0),
                        ReadDoubleLittleEndian(record, 8),
                        ReadDoubleLittleEndian(record, 16));
                    vent.UpVector = VolcanicUpdraftVault.SafeNormalize(new float3(
                        ReadFloatLittleEndian(record, 24),
                        ReadFloatLittleEndian(record, 28),
                        ReadFloatLittleEndian(record, 32)),
                        new float3(0f, 1f, 0f));
                    vent.Radius = math.max(0.25f, ReadFloatLittleEndian(record, 36));
                    vent.ThrustPower = math.max(0f, ReadFloatLittleEndian(record, 40));
                    vent.EruptionTimer = math.saturate(ReadFloatLittleEndian(record, 44));
                    vent._pad0 = 0u;
                    vent._pad1 = 0ul;
                    if (!math.all(math.isfinite(vent.AUP)) || !math.all(math.isfinite(vent.UpVector)))
                    {
                        vent.AUP = new double3(i * settings.CylinderRadius * 2.5, 0.0, 0.0);
                        vent.UpVector = new float3(0f, 1f, 0f);
                    }

                    vents[i] = vent;
                }

                return count > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryReadExact(FileStream stream, byte[] destination)
        {
            int offset = 0;
            while (offset < destination.Length)
            {
                int read = stream.Read(destination, offset, destination.Length - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }

            return true;
        }

        private static float ReadFloatLittleEndian(byte[] bytes, int offset)
        {
            uint value = bytes[offset] |
                         ((uint)bytes[offset + 1] << 8) |
                         ((uint)bytes[offset + 2] << 16) |
                         ((uint)bytes[offset + 3] << 24);
            return math.asfloat(value);
        }

        private static double ReadDoubleLittleEndian(byte[] bytes, int offset)
        {
            ulong value = bytes[offset] |
                          ((ulong)bytes[offset + 1] << 8) |
                          ((ulong)bytes[offset + 2] << 16) |
                          ((ulong)bytes[offset + 3] << 24) |
                          ((ulong)bytes[offset + 4] << 32) |
                          ((ulong)bytes[offset + 5] << 40) |
                          ((ulong)bytes[offset + 6] << 48) |
                          ((ulong)bytes[offset + 7] << 56);
            return BitConverter.Int64BitsToDouble(unchecked((long)value));
        }

        private static void GenerateEmergencyMockVents(NativeArray<VentStateDTO> vents, VolcanicUpdraftSettingsDTO settings)
        {
            int count = math.min((int)settings.VentCount, vents.Length);
            for (int i = 0; i < count; i++)
            {
                float lane = i - (count - 1) * 0.5f;
                vents[i] = new VentStateDTO
                {
                    AUP = new double3(lane * 48.0, -160.0 - (i % 3) * 12.0, (i & 1) == 0 ? 64.0 : -64.0),
                    UpVector = new float3(0f, 1f, 0f),
                    Radius = settings.CylinderRadius,
                    ThrustPower = 0f,
                    EruptionTimer = VolcanicUpdraftVault.Hash01((uint)(i + 1) * 0x9E3779B9u),
                    _pad0 = 0u,
                    _pad1 = 0ul
                };
            }

            for (int i = count; i < vents.Length; i++)
                vents[i] = default;
        }

        private static void GenerateMockEntities(
            NativeArray<MockSubmarineArray> submarines,
            NativeArray<MockLeviathanVelocityDTO> leviathans,
            NativeArray<MockDebrisParticleDTO> debris,
            NativeArray<VentStateDTO> vents,
            VolcanicUpdraftSettingsDTO settings)
        {
            int ventCount = math.max(1, math.min((int)settings.VentCount, vents.Length));
            for (int i = 0; i < submarines.Length; i++)
            {
                VentStateDTO vent = vents[i % ventCount];
                submarines[i] = new MockSubmarineArray
                {
                    AUP = vent.AUP + new double3(0.0, settings.MaxHeight * 0.25f, 0.0),
                    Radius = 4f,
                    MassKg = 18000f,
                    EntityId = (uint)(1000 + i)
                };
            }

            for (int i = 0; i < leviathans.Length; i++)
            {
                VentStateDTO vent = vents[i % ventCount];
                leviathans[i] = new MockLeviathanVelocityDTO
                {
                    AUP = vent.AUP + new double3(3.0, settings.MaxHeight * 0.35f, 3.0),
                    DesiredDirection = new float3(0f, 0f, 1f),
                    Slot = (ushort)i
                };
            }

            for (int i = 0; i < debris.Length; i++)
            {
                VentStateDTO vent = vents[i % ventCount];
                float lateral = (VolcanicUpdraftVault.Hash01((uint)i * 13u) - 0.5f) * settings.CylinderRadius;
                debris[i] = new MockDebrisParticleDTO
                {
                    AUP = vent.AUP + new double3(lateral, 2.0 + (i & 7), -lateral),
                    Radius = 0.2f,
                    MassKg = 5f,
                    EntityId = (uint)(2000 + i)
                };
            }
        }

        private void PublishPresentationSignals(
            in VolcanicUpdraftTelemetryEntry entry,
            NativeArray<VentStateDTO> vents,
            VolcanicUpdraftSettingsDTO settings,
            NativeArray<VolcanicFloatStateSignal> floatSignals,
            NativeArray<VolcanicPlayerHeatSignalDTO> playerHeat)
        {
            if (entry.Frame == 0u)
                return;

            int ventCount = math.min((int)settings.VentCount, vents.Length);
            float quality = settings.GlobalQualityWeight;
            int maxSignalVents = math.clamp((int)math.lerp(1f, math.min(ventCount, 8), quality), 1, math.max(1, ventCount));
            for (int i = 0; i < maxSignalVents; i++)
            {
                VentStateDTO vent = vents[i];
                float intensity = math.saturate(vent.ThrustPower * math.rcp(math.max(0.0001f, settings.MaxThrust)));
                if (intensity <= 0.65f)
                    continue;

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(vent.AUP);
                GlobalSignals.Publish(new FluidImpulseSignal
                {
                    PositionAup = aup,
                    Vector = math.normalizesafe(vent.UpVector, new float3(0f, 1f, 0f)) * vent.ThrustPower,
                    Radius = vent.Radius,
                    Lifetime = math.lerp(0.3f, 1.2f, quality),
                    Frame = entry.Frame,
                    SourceHash = VolcanicUpdraftVault.SourceHash,
                    Flags = 1u
                });

                GlobalSignals.Publish(new AcousticPingSignal
                {
                    PositionAup = aup,
                    RadiusMeters = settings.AcousticRadius,
                    Intensity01 = intensity,
                    SourceId = VolcanicUpdraftVault.SourceHash,
                    Channel = AcousticPingSignal.ChannelMetalStress,
                    Flags = AcousticPingSignal.FlagActiveSonar
                });

                float debrisWeight = VolcanicUpdraftVault.ResolveDebrisLiftWeight(quality);
                ushort quantity = (ushort)math.round(math.saturate(intensity * debrisWeight * settings.DebrisCommandIntensity) * 50f);
                if (quantity > 0)
                {
                    GlobalSignals.Publish(new DebrisSpawnSignal
                    {
                        PositionAup = aup,
                        SpeciesHash = VolcanicUpdraftVault.SourceHash,
                        SourceEntityId = VolcanicUpdraftVault.SourceHash,
                        Intensity01 = intensity,
                        DebrisKind = DebrisSpawnSignal.DebrisKindRockShard,
                        Flags = DebrisSpawnSignal.FlagComputeShard,
                        Quantity = quantity
                    });
                }

                Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(vent.AUP);
                IThermodynamicsService thermodynamics = _thermodynamicsService;
                if (thermodynamics != null)
                    thermodynamics.TryInjectTransientHeatSource(runtimePosition, vent.Radius, settings.HeatOutput * intensity, VolcanicUpdraftVault.SourceHash);
            }

            if (entry.ActiveEruptions > 0)
            {
                GlobalSignals.Publish(new SeismicSignal
                {
                    Direction = new float3(0f, 1f, 0f),
                    Intensity01 = math.saturate(entry.ActiveEruptions / 4f),
                    CameraJitter01 = math.saturate(entry.ActiveEruptions / 8f),
                    AudioIntensity01 = math.saturate(entry.ActiveEruptions / 4f),
                    ThermalEruptionProbabilityScalar = math.saturate(entry.ActiveEruptions / 8f),
                    Sequence = (ushort)(entry.Frame & 0xFFFFu),
                    DepthFlags = 1,
                    Flags = 1
                });
            }

            if (playerHeat.IsCreated && playerHeat.Length > 0)
            {
                VolcanicPlayerHeatSignalDTO heat = playerHeat[0];
                if (heat.Intensity01 > 0f)
                {
                    GlobalSignals.Publish(new PlayerStressSignal
                    {
                        Stress01 = heat.Blindness01,
                        OxygenDrainScale = 1f + heat.Heat01,
                        AggressionScale = heat.Blindness01,
                        Frame = heat.Frame,
                        Cause = 7,
                        Flags = 1
                    });

                    GlobalSignals.Publish(new PhysiologyStateSignal
                    {
                        PlayerStress01 = heat.Blindness01,
                        O2DrainMultiplier = 1f + heat.Heat01,
                        Recovery01 = 0f,
                        Frame = heat.Frame,
                        Cause = 7,
                        Flags = 1
                    });
                }
            }

            if (floatSignals.IsCreated)
            {
                int count = math.min(floatSignals.Length, AlphaLeviathanStalkConstants.MaxLeviathanSlots);
                for (int i = 0; i < count; i++)
                {
                    VolcanicFloatStateSignal signal = floatSignals[i];
                    if (signal.Frame != entry.Frame || signal.Intensity01 <= 0f)
                        continue;

                    AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(signal.AUP);
                    GlobalSignals.Publish(new FaunaStateChangedSignal
                    {
                        PositionAup = aup,
                        SpeciesHash = VolcanicUpdraftVault.SourceHash,
                        StateFlags = signal.Flags,
                        Frame = signal.Frame,
                        Slot = signal.Slot,
                        StateKind = signal.StateKind,
                        Flags = FaunaStateChangedSignalFlags.StateActive
                    });
                }
            }
        }

        private void TryApplyCsvOverrides()
        {
            if (!_buffersReady || _fixedPipelineScheduled || !ResolveDataVault())
                return;

            if (string.IsNullOrEmpty(_csvPath))
                _csvPath = Path.Combine(Application.streamingAssetsPath, CsvFileName);

            if (!File.Exists(_csvPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (ticks == _csvLastWriteTicks)
                return;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_dataVault);
            NativeArray<VolcanicUpdraftSettingsDTO> settings = _settingsHandle.Resolve(_dataVault);
            NativeArray<VentStateDTO> vents = _ventHandle.Resolve(_dataVault);
            if (!scratch.IsCreated || !settings.IsCreated || settings.Length <= 0 || !vents.IsCreated)
                return;

            int length = ReadCsvBytesCold(_csvPath, scratch);
            if (length <= 0)
                return;

            VolcanicUpdraftSettingsDTO value = settings[0];
            ParseCsvBytes(scratch, length, ref value, vents);
            settings[0] = SanitizeSettings(value);
            _csvLastWriteTicks = ticks;
        }

        private static int ReadCsvBytesCold(string path, NativeArray<byte> scratch)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                int max = math.min(scratch.Length, VolcanicUpdraftVault.CsvScratchBytes);
                int length = 0;
                while (length < max)
                {
                    int b = stream.ReadByte();
                    if (b < 0)
                        break;
                    scratch[length++] = (byte)b;
                }

                return length;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static void ParseCsvBytes(NativeArray<byte> bytes, int length, ref VolcanicUpdraftSettingsDTO settings, NativeArray<VentStateDTO> vents)
        {
            int index = 0;
            while (index < length)
            {
                uint keyHash = 2166136261u;
                bool hasKey = false;
                bool endedBeforeValue = false;
                while (index < length)
                {
                    byte b = bytes[index++];
                    if (b == (byte)',' || b == (byte)'=')
                        break;
                    if (b == (byte)'\n' || b == (byte)'\r')
                    {
                        endedBeforeValue = true;
                        break;
                    }

                    if (b >= (byte)'A' && b <= (byte)'Z')
                        b = (byte)(b + 32);
                    if (b > (byte)' ')
                    {
                        keyHash ^= b;
                        keyHash *= 16777619u;
                        hasKey = true;
                    }
                }

                if (hasKey && !endedBeforeValue)
                {
                    if (keyHash == HashVent)
                    {
                        ParseVentCsvRow(bytes, length, ref index, vents, settings);
                    }
                    else
                    {
                        float value0 = ParseFloat(bytes, length, ref index);
                        if (keyHash == HashMaxThrust)
                            settings.MaxThrust = value0;
                        else if (keyHash == HashEruptionFrequency)
                            settings.EruptionFrequency = value0;
                        else if (keyHash == HashCylinderRadius)
                            settings.CylinderRadius = value0;
                        else if (keyHash == HashMaxHeight)
                            settings.MaxHeight = value0;
                        else if (keyHash == HashHeatOutput)
                            settings.HeatOutput = value0;
                        else if (keyHash == HashGlobalQualityWeight)
                            settings.GlobalQualityWeight = value0;
                    }
                }

                while (index < length && bytes[index] != (byte)'\n')
                    index++;
                if (index < length)
                    index++;
            }
        }

        private static void ParseVentCsvRow(NativeArray<byte> bytes, int length, ref int index, NativeArray<VentStateDTO> vents, VolcanicUpdraftSettingsDTO settings)
        {
            int ventIndex = (int)math.round(ParseFloat(bytes, length, ref index));
            float x = ParseFloat(bytes, length, ref index);
            float y = ParseFloat(bytes, length, ref index);
            float z = ParseFloat(bytes, length, ref index);
            float radius = ParseFloat(bytes, length, ref index);
            float thrust = ParseFloat(bytes, length, ref index);
            if ((uint)ventIndex >= (uint)vents.Length)
                return;

            VentStateDTO vent = vents[ventIndex];
            vent.AUP = new double3(x, y, z);
            vent.UpVector = new float3(0f, 1f, 0f);
            vent.Radius = radius > 0f ? radius : settings.CylinderRadius;
            vent.ThrustPower = math.max(0f, thrust);
            vents[ventIndex] = vent;
        }

        private static float ParseFloat(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t' || bytes[index] == (byte)','))
                index++;

            float sign = 1f;
            if (index < length && bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float value = 0f;
            while (index < length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                value = (value * 10f) + (b - (byte)'0');
                index++;
            }

            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    value += (b - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            if (index < length && bytes[index] == (byte)',')
                index++;

            return value * sign;
        }

        private void DumpBlackBoxIfFaulted()
        {
            if (_faultDumped || !_buffersReady || !ResolveDataVault())
                return;

            NativeArray<VolcanicUpdraftTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            VolcanicUpdraftTelemetryEntry entry = telemetry[(int)(_frame % VolcanicUpdraftVault.TelemetryFrames)];
            if ((entry.Flags & VolcanicUpdraftVault.TelemetryFlagNaN) == 0u)
                return;

            try
            {
                string root = Application.dataPath;
                root = Directory.GetParent(root)?.FullName ?? root;
                string path = Path.Combine(root, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = File.Create(path);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(VolcanicUpdraftVault.SourceHash);
                writer.Write((int)UnsafeUtility.SizeOf<VolcanicUpdraftTelemetryEntry>());
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    VolcanicUpdraftTelemetryEntry value = telemetry[i];
                    writer.Write(value.PrimaryVentAup.x);
                    writer.Write(value.PrimaryVentAup.y);
                    writer.Write(value.PrimaryVentAup.z);
                    writer.Write(value.LastVector.x);
                    writer.Write(value.LastVector.y);
                    writer.Write(value.LastVector.z);
                    writer.Write(value.CylinderComputeTimeMs);
                    writer.Write(value.Frame);
                    writer.Write(value.StateHash);
                    writer.Write(value.ActiveEruptions);
                    writer.Write(value.EntitiesLifted);
                    writer.Write(value.DebrisLifted);
                    writer.Write(value.LeviathansLifted);
                    writer.Write(value.Flags);
                }

                _faultDumped = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !TryGetVentReadback(0, out _, out VolcanicUpdraftSettingsDTO settings))
                return;

            int count = math.min((int)settings.VentCount, maxVentCount);
            for (int i = 0; i < count; i++)
            {
                if (!TryGetVentReadback(i, out VentStateDTO vent, out settings))
                    continue;

                Vector3 pos = HectonFloatingOrigin.ToRuntimePosition(vent.AUP);
                float intensity = math.saturate(vent.ThrustPower * math.rcp(math.max(0.0001f, settings.MaxThrust)));
                Gizmos.color = Color.Lerp(new Color(0.1f, 0.45f, 1f, 0.7f), new Color(1f, 0.05f, 0.02f, 0.9f), intensity);
                Gizmos.DrawWireCube(pos + Vector3.up * (settings.MaxHeight * 0.5f), new Vector3(vent.Radius * 2f, settings.MaxHeight, vent.Radius * 2f));
            }
        }
    }
}

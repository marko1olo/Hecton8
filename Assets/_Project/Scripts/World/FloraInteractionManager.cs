using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Systems.AI;
using Hecton8.VFX.Wakes;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Publishes global vegetation interaction and environment shader inputs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class FloraInteractionManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IProceduralSwayDirector, IGlobalRegistryHotSwapListener
    {
        private static readonly WaitCallback s_floraMemoryTelemetryDumpCallback = WriteFloraMemoryTelemetryDumpQueued;

        private sealed class FloraMemoryTelemetryDumpWork
        {
            public string Path;
            public byte[] Payload;
        }

        private static int s_x001FloraInteractionManagerSignalPushDropCount;
        private const int MaxModuleParentResolveDepth = 16;
        private const uint KccVelocityFloraInteractionMaxAgeFrames = 12u;

        private static FloraInteractionManager s_ActiveRuntimeInstance;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FloraInteractionPointGpuData
        {
            [FieldOffset(0)] public Vector4 PositionRadius;
            [FieldOffset(16)] public Vector4 VelocitySpeed;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct WakeTrailStampCommand
        {
            [FieldOffset(0)]
            public Vector4 UvEllipse;
            [FieldOffset(16)]
            public Vector4 DirectionStrengthVertical;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct FloraDisplacementDTO
        {
            [FieldOffset(0)] public float3 ForceVector;
            [FieldOffset(12)] public float DecayTimer;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct FloraStiffnessRuleDTO
        {
            [FieldOffset(0)] public uint PlantHash;
            [FieldOffset(4)] public float Stiffness01;
            [FieldOffset(8)] public float Damping;
            [FieldOffset(12)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraSwayFieldTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint NonZeroCellsCount;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public float3 FieldCenterWS;
            [FieldOffset(24)] public float CellSize;
            [FieldOffset(28)] public float MaxMagnitude;
            [FieldOffset(32)] public float GlobalQualityWeight;
            [FieldOffset(36)] public float UpdateIntervalSeconds;
            [FieldOffset(40)] public float SystemStress01;
            [FieldOffset(44)] public uint StateHash;
            [FieldOffset(48)] public uint DataVaultGeneration;
            [FieldOffset(52)] public uint AupShiftSequence;
            [FieldOffset(56)] public uint CpuMicroseconds;
            [FieldOffset(60)] public ushort Resolution;
            [FieldOffset(62)] public ushort ActiveWakeSourcesCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraMemoryTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint EventHash;
            [FieldOffset(8)] public uint BufferID;
            [FieldOffset(12)] public uint SystemID;
            [FieldOffset(16)] public uint Generation;
            [FieldOffset(20)] public uint RequiredLength;
            [FieldOffset(24)] public uint ActualLength;
            [FieldOffset(28)] public uint Flags;
            [FieldOffset(32)] public uint ConsecutiveFailures;
            [FieldOffset(36)] public uint VaultGeneration;
            [FieldOffset(40)] public float GlobalQualityWeight;
            [FieldOffset(44)] public float SystemStress01;
            [FieldOffset(48)] public uint StateHash;
            [FieldOffset(52)] public uint AupShiftSequence;
            [FieldOffset(56)] public uint CpuMicroseconds;
            [FieldOffset(60)] private uint _reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct ModuleParasiteState
        {
            [FieldOffset(0)] public float PowerDrainWatts;
            [FieldOffset(4)] public float InfectionLevel;
            [FieldOffset(8)] public float RootPowerDrainWatts;
            [FieldOffset(12)] public float RootInfectionLevel;
            [FieldOffset(16)] public float MaxMatureAttachedSeconds;
            [FieldOffset(20)] public float AddedMassKilograms;
            [FieldOffset(24)] public float ThermalInsulation01;
            [FieldOffset(28)] public float BioReactorOverheatMultiplier;
            [FieldOffset(32)] public int ParasiteCount;
            [FieldOffset(36)] private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ParasiteNode
        {
            [FieldOffset(0)]
            public double BirthTimeSeconds;
            [FieldOffset(8)]
            public float3 PositionWS;
            [FieldOffset(20)]
            public int HostModuleRuntimeId;
            [FieldOffset(24)]
            public float GrowthLevel;
            [FieldOffset(28)]
            public float PowerDrainWatts;
            [FieldOffset(32)]
            public float InfectionStrength;
            [FieldOffset(36)]
            public float RootDrainMultiplier;
            [FieldOffset(40)]
            public float RadiusMeters;
            [FieldOffset(44)]
            public float PulseFrequency;
            [FieldOffset(48)]
            public float ThermalGrowthFlag;
            [FieldOffset(52)]
            public float MatureAttachedSeconds;
            [FieldOffset(56)]
            public float AddedMassKilograms;
            [FieldOffset(60)]
            public byte State;
            [FieldOffset(61)]
            private byte _pad0;
            [FieldOffset(62)]
            private byte _pad1;
            [FieldOffset(63)]
            private byte _pad2;
        }

        internal readonly struct ModuleParasiteTarget
        {
            public readonly BaseModule HostModule;
            public readonly Vector3 Position;
            public readonly float Radius;
            public readonly float InfectionLevel;
            public readonly float CriticalityWeight;

            public ModuleParasiteTarget(BaseModule hostModule, Vector3 position, float radius, float infectionLevel, float criticalityWeight)
            {
                HostModule = hostModule;
                Position = position;
                Radius = radius;
                InfectionLevel = infectionLevel;
                CriticalityWeight = criticalityWeight;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FloraCascadeEventPayload
        {
            [FieldOffset(0)] public float3 Center;
            [FieldOffset(12)] public float StartTimeSeconds;
            [FieldOffset(16)] public float RadiusMeters;
            [FieldOffset(20)] private uint _pad0;
            [FieldOffset(24)] private uint _pad1;
            [FieldOffset(28)] private uint _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct DefensiveSporeBurstState
        {
            [FieldOffset(0)] public Vector3 PositionWS;
            [FieldOffset(12)] public float Radius;
            [FieldOffset(16)] public float Intensity;
            [FieldOffset(20)] public float ExpireTimeSeconds;
            [FieldOffset(24)] private uint _pad0;
            [FieldOffset(28)] private uint _pad1;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct PopulateCascadePhaseSeedsJob : IJobParallelFor
        {
            private const float BaseAnimationFrameSeconds = 1f / 60f;

            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly, NoAlias] public NativeArray<HectonVegetationInstanceData> Metadata;
            [ReadOnly, NoAlias] public NativeArray<byte> ReactiveTemplateMask;
            [ReadOnly, NoAlias] public NativeArray<FloraCascadeEventPayload> Events;
            public int EventCount;
            public float PropagationSpeedMetersPerSecond;
            public float InactiveSeed;

            [WriteOnly, NoAlias] public NativeArray<float> PhaseSeeds;

            public void Execute(int index)
            {
                if (!PhaseSeeds.IsCreated ||
                    (uint)index >= (uint)PhaseSeeds.Length)
                {
                    return;
                }

                if (!Metadata.IsCreated ||
                    !ReactiveTemplateMask.IsCreated ||
                    index < 0 ||
                    index >= Metadata.Length ||
                    index >= Matrices.Length ||
                    EventCount <= 0)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                HectonVegetationInstanceData instanceData = Metadata[index];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(instanceData.HeightScale) <= 0.0001f)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                int templateIndex = (int)math.round(instanceData.TemplateIndex);
                if (templateIndex < 0 || templateIndex >= ReactiveTemplateMask.Length || ReactiveTemplateMask[templateIndex] == 0)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                float3 position = new float3(Matrices[index].m03, Matrices[index].m13, Matrices[index].m23);
                float bestSeed = InactiveSeed;
                bool found = false;
                float safeSpeed = math.max(0.01f, PropagationSpeedMetersPerSecond);
                int safeEventCount = math.min(EventCount, Events.Length);
                for (int eventIndex = 0; eventIndex < safeEventCount; eventIndex++)
                {
                    FloraCascadeEventPayload cascadeEvent = Events[eventIndex];
                    float radius = cascadeEvent.RadiusMeters;
                    float3 cascadeDelta = position - cascadeEvent.Center;
                    float distanceSq = math.lengthsq(cascadeDelta);
                    float radiusSq = radius * radius;
                    bool valid = radius >= 0f & math.isfinite(distanceSq) & distanceSq <= radiusSq;

                    float distance = distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f));
                    float activationTime = cascadeEvent.StartTimeSeconds + (distance / safeSpeed);
                    bestSeed = math.select(bestSeed, math.min(bestSeed, activationTime), valid);
                    found |= valid;
                }

                PhaseSeeds[index] = math.select(InactiveSeed, bestSeed + BuildDeterministicFrameOffset(index, instanceData), found);
            }

            private static float BuildDeterministicFrameOffset(int instanceIndex, HectonVegetationInstanceData instanceData)
            {
                uint phaseHash = math.hash(new uint3(
                    (uint)math.max(0, instanceIndex),
                    math.asuint(instanceData.Variation),
                    math.asuint(instanceData.TemplateIndex)));
                return ((phaseHash & 0x00FFFFFFu) * (1f / 16777215f)) * BaseAnimationFrameSeconds;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ParasiteGrowthJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<ParasiteNode> Nodes;
            public int NodeCount;
            public float DeltaTime;
            public double CurrentTimeSeconds;
            public float GrowthPerSecond;
            public float DefoliantKillThreshold;
            public float MatureGrowthThreshold;
            [ReadOnly, NoAlias] public NativeArray<float4>.ReadOnly ChemicalFrontGrid;
            [ReadOnly, NoAlias] public NativeArray<float4>.ReadOnly ChemicalOverlayGrid;
            public int3 ChemicalDimensions;
            public float3 ChemicalOrigin;
            public float3 ChemicalCellSize;

            public void Execute(int index)
            {
                if (index < 0 || index >= NodeCount || index >= Nodes.Length)
                    return;

                ParasiteNode node = Nodes[index];
                if (node.State == ParasiteNodeStateDead)
                    return;

                float defoliant = SampleDefoliant(node.PositionWS);
                if (defoliant >= DefoliantKillThreshold)
                {
                    node.GrowthLevel = 0f;
                    node.BirthTimeSeconds = 0d;
                    node.State = ParasiteNodeStateDead;
                    Nodes[index] = node;
                    return;
                }

                node.State = ParasiteNodeStateAlive;
                if (!(node.BirthTimeSeconds > 0d) || node.BirthTimeSeconds > 1.0e12d)
                {
                    node.BirthTimeSeconds = CurrentTimeSeconds;
                }
                node.GrowthLevel = math.saturate(node.GrowthLevel + math.max(0f, GrowthPerSecond) * math.max(0f, DeltaTime));
                node.MatureAttachedSeconds = node.GrowthLevel >= math.saturate(MatureGrowthThreshold)
                    ? math.max(0f, node.MatureAttachedSeconds) + math.max(0f, DeltaTime)
                    : 0f;
                Nodes[index] = node;
            }

            private float SampleDefoliant(float3 positionWS)
            {
                if (!ChemicalFrontGrid.IsCreated ||
                    !ChemicalOverlayGrid.IsCreated ||
                    ChemicalDimensions.x <= 0 ||
                    ChemicalDimensions.y <= 0 ||
                    ChemicalDimensions.z <= 0 ||
                    ChemicalCellSize.x <= 0f ||
                    ChemicalCellSize.y <= 0f ||
                    ChemicalCellSize.z <= 0f)
                {
                    return 0f;
                }

                int index = ResolveChemicalIndex(positionWS);
                if (index < 0 || index >= ChemicalFrontGrid.Length || index >= ChemicalOverlayGrid.Length)
                    return 0f;

                float toxicityLane = ChemicalFrontGrid[index].w + ChemicalOverlayGrid[index].w;
                return math.max(0f, -toxicityLane);
            }

            private int ResolveChemicalIndex(float3 positionWS)
            {
                float3 cell = (positionWS - ChemicalOrigin) / ChemicalCellSize;
                int x = (int)math.floor(cell.x);
                int y = (int)math.floor(cell.y);
                int z = (int)math.floor(cell.z);
                if (x < 0 || y < 0 || z < 0 ||
                    x >= ChemicalDimensions.x ||
                    y >= ChemicalDimensions.y ||
                    z >= ChemicalDimensions.z)
                {
                    return -1;
                }

                return x + (y * ChemicalDimensions.x) + (z * ChemicalDimensions.x * ChemicalDimensions.y);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct DecayFloraForcesJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDisplacementDTO> FieldValues;
            public int NodeCount;
            public int Resolution;
            public int3 RingOffset;
            public int3 CenterShiftCells;
            public float DeltaTime;
            public float DecayRate;
            public byte ResetField;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)NodeCount || index >= FieldValues.Length)
                    return;

                int safeResolution = math.max(1, Resolution);
                int xy = safeResolution * safeResolution;
                int z = index / xy;
                int rem = index - (z * xy);
                int y = rem / safeResolution;
                int x = rem - (y * safeResolution);
                int physicalIndex = ResolveFloraSwayFieldIndexJob(x, y, z, RingOffset, safeResolution);
                if ((uint)physicalIndex >= (uint)NodeCount || physicalIndex >= FieldValues.Length)
                    return;

                FloraDisplacementDTO value = FieldValues[physicalIndex];
                bool resetCell = ResetField != 0 || IsFloraSwayWrappedCellNewJob(x, y, z, CenterShiftCells, safeResolution);
                float safeDeltaTime = math.select(0f, math.clamp(DeltaTime, 0f, 0.2f), math.isfinite(DeltaTime));
                float safeDecayRate = math.select(0f, math.max(0f, DecayRate), math.isfinite(DecayRate));
                float decay = MathLodApproximation.ApproxExpNegPade33Wide40(safeDeltaTime * safeDecayRate);
                float3 force = math.select(float3.zero, value.ForceVector * decay, math.all(math.isfinite(value.ForceVector)));
                float magnitudeSq = math.lengthsq(force);
                bool active = !resetCell & math.isfinite(magnitudeSq) & magnitudeSq > 0.0000001f;
                value.ForceVector = math.select(float3.zero, force, active);
                value.DecayTimer = math.select(0f, math.saturate(value.DecayTimer * decay), active);

                FieldValues[physicalIndex] = value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AccumulateFloraForcesJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<WakeSource> WakeSources;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDisplacementDTO> FieldValues;
            public AbsoluteUniversePosition FieldCenterAup;
            public float3 AmbientCurrentWS;
            public int Resolution;
            public int NodeCount;
            public int SourceLimit;
            public int3 RingOffset;
            public float CellSize;
            public float QualityWeight;
            public float EntityMassMultiplier;
            public float GlobalCurrentInfluence;
            public float MaxDisplacement;
            public float FramePhase;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)NodeCount || index >= FieldValues.Length)
                    return;

                int safeResolution = math.max(1, Resolution);
                int xy = safeResolution * safeResolution;
                int z = index / xy;
                int rem = index - (z * xy);
                int y = rem / safeResolution;
                int x = rem - (y * safeResolution);
                int physicalIndex = ResolveFloraSwayFieldIndexJob(x, y, z, RingOffset, safeResolution);
                if ((uint)physicalIndex >= (uint)NodeCount || physicalIndex >= FieldValues.Length)
                    return;

                float halfIndex = (safeResolution - 1) * 0.5f;
                float safeCellSize = math.max(0.05f, CellSize);
                float3 sampleLocal = new float3(
                    (x - halfIndex) * safeCellSize,
                    (y - halfIndex) * safeCellSize,
                    (z - halfIndex) * safeCellSize);

                FloraDisplacementDTO value = FieldValues[physicalIndex];
                float3 displacement = math.select(float3.zero, value.ForceVector, math.all(math.isfinite(value.ForceVector)));
                float energy = math.select(0f, math.saturate(value.DecayTimer), math.isfinite(value.DecayTimer));
                float qualityCurve = SmoothStepJob(QualityWeight);
                float maxDisplacement = math.max(0.02f, MaxDisplacement);

                float3 ambient = ResolveAmbientCurrent(sampleLocal, AmbientCurrentWS, qualityCurve, GlobalCurrentInfluence, FramePhase);
                displacement += ambient;
                energy = math.max(energy, math.saturate(math.lengthsq(ambient) * 28f));

                int safeSourceLimit = math.min(math.min(SourceLimit, WakeSources.IsCreated ? WakeSources.Length : 0), MaxProceduralWakePoints);
                for (int sourceIndex = 0; sourceIndex < safeSourceLimit; sourceIndex++)
                {
                    WakeSource source = WakeSources[sourceIndex];
                    bool radiusFinite = math.isfinite(source.Radius);
                    bool intensityFinite = math.isfinite(source.Intensity);
                    bool velocityFinite = math.all(math.isfinite(source.VelocityWS));
                    float safeRadius = math.max(0.001f, math.select(0f, source.Radius, radiusFinite));
                    float safeIntensity = math.max(0f, math.select(0f, source.Intensity, intensityFinite));
                    float3 safeVelocity = math.select(float3.zero, source.VelocityWS, velocityFinite);
                    bool sourceValid = source.Active != 0 &
                                       safeIntensity > WakeMinimumPublishedIntensity &
                                       safeRadius > 0.01f &
                                       velocityFinite &
                                       radiusFinite &
                                       intensityFinite;

                    AbsoluteUniversePosition sourceAup = source.PositionAup;
                    double3 sampleLocalD = new double3(
                        (x - halfIndex) * (double)safeCellSize,
                        (y - halfIndex) * (double)safeCellSize,
                        (z - halfIndex) * (double)safeCellSize);
                    double3 sourceLocalD = ResolveAupLocalDeltaDouble(in sourceAup, in FieldCenterAup);
                    bool sourceLocalFinite = math.all(math.isfinite(sourceLocalD));
                    double3 safeSourceLocalD = math.select(sampleLocalD, sourceLocalD, sourceLocalFinite);
                    double3 deltaD = sampleLocalD - safeSourceLocalD;
                    deltaD.y *= 0.55d;
                    double distanceSqD = math.lengthsq(deltaD);
                    float distanceSq = (float)distanceSqD;
                    float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                    float radiusSq = safeRadius * safeRadius;
                    bool valid = sourceValid & sourceLocalFinite & math.isfinite(distanceSq) & distanceSq < radiusSq;
                    float validWeight = math.select(0f, 1f, valid);

                    float falloff = validWeight * (1f - SmoothStepJob(distanceSq / math.max(radiusSq, 0.0001f)));
                    float3 wakeDirection = ResolveSafeDirectionJob(safeVelocity, float3.zero);
                    float3 radialDirection = ResolveSafeDirectionJob(delta, -wakeDirection);
                    float3 bendDirection = ResolveSafeDirectionJob(wakeDirection * 0.72f + radialDirection * 0.28f, radialDirection);
                    float speedGain = math.saturate(EstimateLengthJob(safeVelocity) * 0.075f + 0.22f);
                    float sourceGain = ResolveSourceGainJob(source.SourceKind) * math.max(0f, EntityMassMultiplier);
                    float strength = falloff * math.saturate(safeIntensity) * sourceGain * speedGain * math.lerp(0.42f, 0.96f, qualityCurve);
                    displacement += bendDirection * (strength * maxDisplacement);
                    displacement.y -= strength * math.lerp(0.025f, 0.085f, qualityCurve) * falloff;
                    energy = math.max(energy, falloff * math.saturate(safeIntensity));
                }

                float magnitudeSq = math.lengthsq(displacement);
                float maxDisplacementSq = maxDisplacement * maxDisplacement;
                bool magnitudeFinite = math.isfinite(magnitudeSq);
                bool magnitudeOverCap = magnitudeFinite & magnitudeSq > maxDisplacementSq;
                float3 cappedDisplacement = displacement * (maxDisplacement * math.rsqrt(math.max(magnitudeSq, 0.0001f)));
                displacement = math.select(float3.zero, math.select(displacement, cappedDisplacement, magnitudeOverCap), magnitudeFinite);
                energy = math.select(0f, energy, magnitudeFinite);
                magnitudeSq = math.select(magnitudeSq, maxDisplacementSq, magnitudeOverCap);

                value.ForceVector = displacement;
                value.DecayTimer = math.saturate(energy);
                FieldValues[physicalIndex] = value;
            }

            private static float3 ResolveAmbientCurrent(float3 sampleLocal, float3 currentWS, float qualityCurve, float influence, float framePhase)
            {
                float safeInfluence = math.saturate(math.select(0f, influence, math.isfinite(influence)));
                float3 current = math.select(float3.zero, currentWS, math.all(math.isfinite(currentWS)));
                float currentMagnitudeSq = math.lengthsq(current);
                float3 currentDirection = math.select(
                    new float3(0.35f, 0.04f, 0.72f),
                    current * math.rsqrt(math.max(currentMagnitudeSq, 0.000001f)),
                    currentMagnitudeSq > 0.000001f);
                float currentMagnitude = math.min(1.75f, currentMagnitudeSq * math.rsqrt(math.max(currentMagnitudeSq, 0.0001f)));
                float noiseScale = math.lerp(0.032f, 0.074f, qualityCurve);
                float n0 = Triangle01(sampleLocal.x * noiseScale + sampleLocal.z * 0.013f + framePhase);
                float n1 = Triangle01(sampleLocal.z * noiseScale * 0.73f - sampleLocal.y * 0.017f + framePhase * 0.61f);
                float n2 = Triangle01(sampleLocal.y * noiseScale * 0.41f + sampleLocal.x * 0.011f - framePhase * 0.37f);
                float3 curlFake = new float3(n0 - 0.5f, (n2 - 0.5f) * 0.35f, n1 - 0.5f);
                float ambientMagnitude = safeInfluence * math.lerp(0.018f, 0.075f, qualityCurve);
                return (currentDirection * (currentMagnitude * 0.045f) + curlFake * math.lerp(0.28f, 1.0f, qualityCurve)) * ambientMagnitude;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct MockDisplacementInjectorJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDisplacementDTO> FieldValues;
            public int Resolution;
            public int NodeCount;
            public int3 RingOffset;
            public float CellSize;
            public float QualityWeight;
            public float FramePhase;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)NodeCount || index >= FieldValues.Length)
                    return;

                int safeResolution = math.max(1, Resolution);
                int xy = safeResolution * safeResolution;
                int z = index / xy;
                int rem = index - (z * xy);
                int y = rem / safeResolution;
                int x = rem - (y * safeResolution);
                int physicalIndex = ResolveFloraSwayFieldIndexJob(x, y, z, RingOffset, safeResolution);
                if ((uint)physicalIndex >= (uint)NodeCount || physicalIndex >= FieldValues.Length)
                    return;

                float halfIndex = (safeResolution - 1) * 0.5f;
                float safeCellSize = math.max(0.05f, CellSize);
                float3 sampleLocal = new float3(
                    (x - halfIndex) * safeCellSize,
                    (y - halfIndex) * safeCellSize,
                    (z - halfIndex) * safeCellSize);

                float q = SmoothStepJob(QualityWeight);
                float fieldRadius = safeResolution * safeCellSize * math.lerp(0.11f, 0.19f, q);
                float3 ghostCenter = new float3(
                    (Triangle01(FramePhase * 0.31f) - 0.5f) * safeResolution * safeCellSize * 0.42f,
                    (Triangle01(FramePhase * 0.19f + 0.27f) - 0.5f) * safeResolution * safeCellSize * 0.18f,
                    (Triangle01(FramePhase * 0.23f + 0.61f) - 0.5f) * safeResolution * safeCellSize * 0.42f);
                float3 delta = sampleLocal - ghostCenter;
                delta.y *= 0.55f;
                float distanceSq = math.lengthsq(delta);
                float radiusSq = math.max(fieldRadius * fieldRadius, 0.0001f);
                bool validSample = math.isfinite(distanceSq) & distanceSq < radiusSq;
                float validWeight = math.select(0f, 1f, validSample);

                float falloff = 1f - SmoothStepJob(distanceSq / radiusSq);
                float3 direction = ResolveSafeDirectionJob(new float3(0.42f, -0.08f, 0.82f), new float3(1f, 0f, 0f));
                FloraDisplacementDTO value = FieldValues[physicalIndex];
                value.ForceVector = math.select(float3.zero, value.ForceVector, math.all(math.isfinite(value.ForceVector)));
                value.DecayTimer = math.select(0f, math.saturate(value.DecayTimer), math.isfinite(value.DecayTimer));
                value.ForceVector += direction * (validWeight * falloff * math.lerp(0.08f, 0.24f, q));
                value.DecayTimer = math.max(value.DecayTimer, validWeight * falloff * 0.72f);
                float maxDisplacement = math.lerp(0.28f, FloraSwayFieldMaxDisplacementMeters, q);
                float magnitudeSq = math.lengthsq(value.ForceVector);
                float maxDisplacementSq = maxDisplacement * maxDisplacement;
                bool magnitudeFinite = math.isfinite(magnitudeSq);
                bool magnitudeOverCap = magnitudeFinite & magnitudeSq > maxDisplacementSq;
                float3 cappedForce = value.ForceVector * (maxDisplacement * math.rsqrt(math.max(magnitudeSq, 0.0001f)));
                value.ForceVector = math.select(float3.zero, math.select(value.ForceVector, cappedForce, magnitudeOverCap), magnitudeFinite);
                value.DecayTimer = math.select(0f, value.DecayTimer, magnitudeFinite);

                FieldValues[physicalIndex] = value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct UploadDisplacementTextureJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<FloraDisplacementDTO> FieldValues;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float4> FieldMeta;
            public int Resolution;
            public int NodeCount;
            public float CellSize;
            public float QualityWeight;
            public float UpdateIntervalSeconds;
            public int ActiveWakeCount;
            public float3 FieldCenterWS;
            public int FrameIndex;
            public uint AupShiftSequence;
            public int BlackBoxCursor;
            public uint Flags;

            public void Execute()
            {
                float maxMagnitudeSq = 0f;
                uint nonZeroCells = 0u;
                uint outputFlags = Flags;
                int safeCount = math.min(NodeCount, FieldValues.IsCreated ? FieldValues.Length : 0);
                for (int i = 0; i < safeCount; i++)
                {
                    FloraDisplacementDTO value = FieldValues[i];
                    bool forceFinite = math.all(math.isfinite(value.ForceVector));
                    float magnitudeSq = math.select(0f, math.lengthsq(value.ForceVector), forceFinite);
                    bool finite = forceFinite & math.isfinite(magnitudeSq);
                    bool nonZero = finite & magnitudeSq > 0.0000001f;
                    outputFlags |= math.select(0u, FloraSwayFieldNaNFlag, !finite);
                    nonZeroCells += math.select(0u, 1u, nonZero);
                    maxMagnitudeSq = math.max(maxMagnitudeSq, math.select(0f, math.max(magnitudeSq, 0f), nonZero));
                }

                if (!FieldMeta.IsCreated || FieldMeta.Length < FloraSwayFieldMetaVectorCount)
                    return;

                float maxMagnitude = math.sqrt(maxMagnitudeSq);
                FieldMeta[0] = new float4(FieldCenterWS.x, FieldCenterWS.y, FieldCenterWS.z, CellSize);
                FieldMeta[1] = new float4(Resolution, math.select(0f, 1f, maxMagnitude > 0.0001f), QualityWeight, maxMagnitude);
                FieldMeta[2] = new float4(NodeCount, UpdateIntervalSeconds, ActiveWakeCount, nonZeroCells);
                FieldMeta[3] = new float4(
                    math.max(0, FrameIndex),
                    AupShiftSequence,
                    math.max(0, BlackBoxCursor),
                    outputFlags);
            }
        }

        private static float SmoothStepJob(float value)
        {
            float t = math.saturate(math.select(0f, value, math.isfinite(value)));
            return t * t * (3f - 2f * t);
        }

        private static float Triangle01(float value)
        {
            float t = math.frac(value);
            return 1f - math.abs(t * 2f - 1f);
        }

        private static float EstimateLengthJob(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static float ResolveSourceGainJob(byte sourceKind)
        {
            float gain = math.select(0.72f, 1.05f, sourceKind == WakeSourceApexPredator);
            return math.select(gain, 1.22f, sourceKind == WakeSourceVehicle);
        }

        private static float3 ResolveSafeDirectionJob(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            float fallbackLengthSq = math.lengthsq(fallback);
            bool valueValid = math.isfinite(lengthSq) & lengthSq > 0.000001f;
            bool fallbackValid = math.isfinite(fallbackLengthSq) & fallbackLengthSq > 0.000001f;
            float3 valueDirection = value * math.rsqrt(math.max(lengthSq, 0.000001f));
            float3 fallbackDirection = fallback * math.rsqrt(math.max(fallbackLengthSq, 0.000001f));
            return math.select(math.select(float3.zero, fallbackDirection, fallbackValid), valueDirection, valueValid);
        }

        private static int WrapFloraSwayGridCoordJob(int value, int resolution)
        {
            int safeResolution = math.max(1, resolution);
            int wrapped = value % safeResolution;
            return wrapped + math.select(0, safeResolution, wrapped < 0);
        }

        private static int ResolveFloraSwayFieldIndexJob(int x, int y, int z, int3 ringOffset, int resolution)
        {
            int safeResolution = math.max(1, resolution);
            int physicalX = WrapFloraSwayGridCoordJob(x + ringOffset.x, safeResolution);
            int physicalY = WrapFloraSwayGridCoordJob(y + ringOffset.y, safeResolution);
            int physicalZ = WrapFloraSwayGridCoordJob(z + ringOffset.z, safeResolution);
            return physicalX + physicalY * safeResolution + physicalZ * safeResolution * safeResolution;
        }

        private static bool IsFloraSwayWrappedCellNewJob(int x, int y, int z, int3 centerShiftCells, int resolution)
        {
            int safeResolution = math.max(1, resolution);
            int oldX = x + centerShiftCells.x;
            int oldY = y + centerShiftCells.y;
            int oldZ = z + centerShiftCells.z;
            return oldX < 0 || oldX >= safeResolution ||
                   oldY < 0 || oldY >= safeResolution ||
                   oldZ < 0 || oldZ >= safeResolution;
        }

        private static double3 ResolveAupLocalDeltaDouble(in AbsoluteUniversePosition source, in AbsoluteUniversePosition origin)
        {
            const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            const double maxLocalDeltaMeters = 1048576d;
            double3 delta = new double3(
                (((double)source.GridX - origin.GridX) * cellSize) + ((double)source.LocalX - origin.LocalX),
                (((double)source.GridY - origin.GridY) * cellSize) + ((double)source.LocalY - origin.LocalY),
                (((double)source.GridZ - origin.GridZ) * cellSize) + ((double)source.LocalZ - origin.LocalZ));
            return math.clamp(delta, new double3(-maxLocalDeltaMeters), new double3(maxLocalDeltaMeters));
        }

        private static float3 ResolveAupLocalDelta(in AbsoluteUniversePosition source, in AbsoluteUniversePosition origin)
        {
            double3 delta = ResolveAupLocalDeltaDouble(in source, in origin);
            float3 result = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.select(float3.zero, result, math.all(math.isfinite(result)));
        }

        private const int MaxPublishedInteractionPoints = 12;
        private const int MaxExternalInteractionPoints = 4;
        private const int MaxProceduralWakePoints = 16;
        private const int MinProceduralWakeSlotLimit = 4;
        private const int WakeBlackBoxCapacity = 300;
        private const int FloraSwayFieldBlackBoxCapacity = 300;
        private const int FloraMemoryTelemetryCapacity = 300;
        private const int FloraMemoryTelemetryDumpFailureThreshold = 3;
        private const int MaxWakeSignalsPerFrame = 64;
        private const int MaxBubbleWakeSignalsPerFrame = 32;
        private const int MaxModuleQueryHits = 32;
        private const int MaxPredatorThreatQueryHits = 16;
        private const int MaxParasiteAnchors = 16;
        private const int MaxTrackedModuleParasiteStates = 512;
        private const int MaxTrackedThermophileDwellModules = 512;
        private const int DefaultHeadlessParasiteCapacity = 256;
        private const int MaxCascadeEvents = 4;
        private const int MaxDefensiveSporeBursts = 6;
        private const int ReactiveFloraHandleCapacity = 64;
        private const int InteractionPointStride = 32;
        private const int FlowFieldStride = sizeof(float) * 2;
        private const int FloraSwayFieldMinResolution = 16;
        private const int FloraSwayFieldMaxResolution = 64;
        private const int FloraSwayFieldMaxNodeCount = FloraSwayFieldMaxResolution * FloraSwayFieldMaxResolution * FloraSwayFieldMaxResolution;
        private const int CascadePhaseSeedCapacity = FloraSwayFieldMaxNodeCount;
        private const int FloraSwayFieldMetaVectorCount = 4;
        private const int FloraSwayFieldJobBatchSize = 64;
        private const int FloraStiffnessRuleCapacity = 16;
        private const int FloraCsvScratchBytes = 16384;
        private const int CascadePhaseSeedJobBatchSize = 64;
        private const float DefaultVegetationWaterLevel = 14.02f;
        private const float FlowFieldUploadIntervalSeconds = 0.1f;
        private const float FlowFieldRecenterThresholdCells = 0.5f;
        private const float FloraSwayFieldMinCellSize = 2.05f;
        private const float FloraSwayFieldMaxCellSize = 4.6f;
        private const float FloraSwayFieldMinUpdateIntervalSeconds = 1f / 60f;
        private const float FloraSwayFieldMaxUpdateIntervalSeconds = 0.2f;
        private const float FloraSwayFieldLayoutQualityHysteresis = 0.035f;
        private const float FloraSwayFieldMaxDisplacementMeters = 1.35f;
        private const int WakeTrailStampCommandCapacity = 4;
        private const int WakeTrailThreadGroupSize = 8;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const byte WakeSourcePlayer = 1;
        private const byte WakeSourceVehicle = 2;
        private const byte WakeSourceApexPredator = 3;
        private const byte WakeSourceBubble = 4;
        private const float WakeDecayPerSecond = 0.85f;
        private const float WakeFollowSharpness = 9.5f;
        private const float WakeMinimumPublishedIntensity = 0.01f;
        private const float WakeFluidImpulseThreshold = 0.55f;
        private const float BubbleWakeMinimumRadius = 0.2f;
        private const float BubbleWakeMaximumRadius = 8f;
        private const float BubbleWakeMinimumSpeed = 0.35f;
        private const float BubbleWakeMaximumSpeed = 3.2f;
        private const float FloraSimulationClockMaxSeconds = 16777215f;
        private const uint WakeBlackBoxInvalidInputFlag = 1u << 0;
        private const uint WakeBlackBoxNaNFlag = 1u << 1;
        private const uint WakeBlackBoxBudgetPressureFlag = 1u << 2;
        private const uint WakeBlackBoxThermalPressureFlag = 1u << 3;
        private const uint WakeBlackBoxSignalOverflowFlag = 1u << 4;
        private const uint WakeBlackBoxBubbleRejectedFlag = 1u << 5;
        private const uint FloraSwayFieldInvalidInputFlag = 1u << 0;
        private const uint FloraSwayFieldNaNFlag = 1u << 1;
        private const uint FloraSwayFieldVaultMissingFlag = 1u << 2;
        private const uint FloraSwayFieldEmptyWakeFlag = 1u << 3;
        private const uint FloraSwayFieldUploadStallFlag = 1u << 4;
        private const uint FloraSwayFieldWrappedShiftFlag = 1u << 5;
        private const uint FloraSwayFieldFullResetFlag = 1u << 6;
        private const uint FloraSwayFieldDiscardedUploadFlag = 1u << 7;
        private const uint FloraMemoryTelemetryMissingVaultFlag = 1u << 0;
        private const uint FloraMemoryTelemetryInvalidLengthFlag = 1u << 1;
        private const uint FloraMemoryTelemetryCompactionFenceFlag = 1u << 2;
        private const uint FloraMemoryTelemetryHandleMismatchFlag = 1u << 3;
        private const uint FloraMemoryTelemetryResolveFailedFlag = 1u << 4;
        private const uint FloraMemoryTelemetryInvalidBufferFlag = 1u << 5;
        private const uint FloraMemoryTelemetryWriteLockFailedFlag = 1u << 6;
        private const uint FloraMemoryTelemetryNaNFlag = 1u << 7;
        private const uint WakeFluidImpulseSourceHash = 0x57414B45u;
        private const uint FloraSwayFieldSourceHash = 0x46535759u;
        private const uint FloraMemoryTelemetryEventResolve = 0x46525652u;
        private const uint FloraMemoryTelemetryEventWriteLock = 0x4652574Cu;
        private const uint FloraMemoryTelemetryEventNaN = 0x46524E41u;
        private const string FloraMemoryTelemetryDumpPath = "Docs/AgentLogs/Dump_1327_FloraInteraction.bin";
        private const BufferID FloraSwayDisplacementFieldBufferId = BufferID.FloraInteractionManager_FloraSwayDisplacementFieldBufferId;
        private const BufferID FloraSwayFieldMetaBufferId = BufferID.FloraInteractionManager_FloraSwayFieldMetaBufferId;
        private const BufferID FloraSwayFieldBlackBoxBufferId = BufferID.FloraInteractionManager_FloraSwayFieldBlackBoxBufferId;
        private const BufferID FloraStiffnessRulesBufferId = BufferID.FloraInteractionManager_FloraStiffnessRulesBufferId;
        private const BufferID FloraStiffnessCsvScratchBufferId = BufferID.FloraInteractionManager_FloraStiffnessCsvScratchBufferId;
        private const BufferID FloraOceanFlowSamplePositionsBufferId = BufferID.FloraInteractionManager_FloraOceanFlowSamplePositionsBufferId;
        private const BufferID FloraOceanFlowSampleResultsBufferId = BufferID.FloraInteractionManager_FloraOceanFlowSampleResultsBufferId;
        private const BufferID FloraParasiteNodesBufferId = BufferID.FloraInteractionManager_FloraParasiteNodesBufferId;
        private const BufferID FloraCascadeReactiveTemplateMaskBufferId = BufferID.FloraInteractionManager_FloraCascadeReactiveTemplateMaskBufferId;
        private const BufferID FloraDefensiveSporeTemplateMaskBufferId = BufferID.FloraInteractionManager_FloraDefensiveSporeTemplateMaskBufferId;
        private const BufferID FloraBloodKelpTemplateMaskBufferId = BufferID.ShinobuSeaglideStates;
        private const BufferID FloraGhostWeedTemplateMaskBufferId = BufferID.ShinobuSeaglideRequests;
        private const ulong FloraOceanFlowSampleMutationGuardMask = (1UL << 7) | (1UL << 8);
        private const ulong FloraAllelopathicTemplateMutationGuardMask = (1UL << 12) | (1UL << 13);
        private const BufferID FloraSurfaceCascadePhaseSeedsBufferId = BufferID.ShinobuSeaglideForcePackets;
        private const BufferID FloraUnderwaterCascadePhaseSeedsBufferId = BufferID.ShinobuSeaglideFlowSamples;
        private const BufferID FloraSurfaceCascadeEventsBufferId = BufferID.ShinobuSeaglideTuning;
        private const BufferID FloraUnderwaterCascadeEventsBufferId = BufferID.ShinobuSeaglideTelemetryRing;
        private const BufferID FloraSurfaceReactiveHandlesBufferId = BufferID.ShinobuSeaglideTelemetryCursor;
        private const BufferID FloraUnderwaterReactiveHandlesBufferId = BufferID.ShinobuSeaglideCounters;
        private const BufferID FloraReactiveQueryHandlesBufferId = BufferID.ShinobuSeaglideBodyBindings;
        private const BufferID FloraMemoryTelemetryBufferId = BufferID.ShinobuSeaglideVisualStates;
        private const float ApexPredatorWakeMinSpeed = 3f;
        private const float DefaultPlayerWakeRadius = 2.4f;
        private const float DefaultMaxBendRadius = 2.9f;
        private const float DefaultSubmarineWakeStrength = 0.82f;
        private const float DefaultSubmarineWakeMinSpeed = 2.5f;
        private const float DefaultSubmarineWakeWhipSpeed = 15f;
        private const float DefaultSubmarineWakeRadius = 3.2f;
        private const int ReactiveFloraKindMask = 1;
        private const float InactiveCascadeSeed = -100000f;
        private const int ToxicSporeHazardSourceId = unchecked((int)0x6B13A7F1);
        private const float ToxicSporePoisonMinimumExposure = 0.08f;
        private const float ToxicSporePoisonDurationSeconds = 5f;
        private const float ToxicSporeToxemiaDeltaScale = 0.15f;
        private const uint ToxicSporeChemicalHash = 0x54535052u; // TSPR
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;
        private const float MatureToxicSporeEventIntervalSeconds = 10f;
        private const float MatureToxicSporeAgeThreshold01 = 0.999f;
        private const int DefensiveSporeHazardSourceId = unchecked((int)0x52F1063A);
        private const float MinimumAllelopathicToxicity01 = 0.005f;
        private const float FloraSlowTickDeltaSeconds = 0.1f;
        private const float MatureParasiteGrowthThreshold = 0.999f;
        private const float DefaultInGameDaySeconds = 3600f;
        private const float DefaultParasiteScaleGrowthDays = 3f;
        private const float MinimumParasiteScale = 0.1f;
        private const byte ParasiteNodeStateAlive = 1;
        private const byte ParasiteNodeStateDead = 2;
        private const float DamageReactionDurationSeconds = 0.55f;
        private const float DamageReactionDurationReciprocal = 1.8181819f;
#if UNITY_EDITOR
        private const string WakeTrailSimulationComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_VegetationWakeTrailSim.compute";
#endif

        private static readonly int _PropWashPosId = Shader.PropertyToID("_HectonPropWashPosition");
        private static readonly int _PropWashForceId = Shader.PropertyToID("_HectonPropWashForce");
        private static readonly int _InteractionBufferId = Shader.PropertyToID("_HectonFloraInteractionPoints");
        private static readonly int _InteractionCountId = Shader.PropertyToID("_HectonFloraInteractionCount");
        private static readonly int _MarineSnowFlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
        private static readonly int _MarineSnowFlowFieldCenterCellSizeId = Shader.PropertyToID("_MarineSnowFlowFieldCenterCellSize");
        private static readonly int _FloraFlowFieldResolutionId = Shader.PropertyToID("_HectonFloraFlowFieldResolution");
        private static readonly int _PlayerRuntimePositionId = Shader.PropertyToID("_HectonPlayerRuntimePosition");
        private static readonly int _PlayerFloraInteractionParamsId = Shader.PropertyToID("_HectonPlayerFloraInteractionParams");
        private static readonly int _VegetationFogColorId = Shader.PropertyToID("_HectonVegetationFogColor");
        private static readonly int _VegetationAmbientColorId = Shader.PropertyToID("_HectonVegetationAmbientColor");
        private static readonly int _VegetationDepthId = Shader.PropertyToID("_HectonVegetationDepth");
        private static readonly int _VegetationLightFactorId = Shader.PropertyToID("_HectonVegetationLightFactor");
        private static readonly int _VegetationTurbidityId = Shader.PropertyToID("_HectonVegetationTurbidity");
        private static readonly int _VegetationWaterLevelId = Shader.PropertyToID("_HectonVegetationWaterLevel");
        private static readonly int _VegetationCurrentVectorId = Shader.PropertyToID("_HectonVegetationCurrentVector");
        private static readonly int _GlobalOceanFlowId = Shader.PropertyToID("_GlobalOceanFlow");
        private static readonly int _VegetationCurrentStrengthId = Shader.PropertyToID("_HectonVegetationCurrentStrength");
        private static readonly int _VegetationCurrentNoiseScaleId = Shader.PropertyToID("_HectonVegetationCurrentNoiseScale");
        private static readonly int _VegetationCurrentTimeScaleId = Shader.PropertyToID("_HectonVegetationCurrentTimeScale");
        private static readonly int _VegetationCurrentVerticalFactorId = Shader.PropertyToID("_HectonVegetationCurrentVerticalFactor");
        private static readonly int _FloraPredatorThreatParamsId = Shader.PropertyToID("_HectonFloraPredatorThreatParams");
        private static readonly int _FloraPredatorThreatPositionRadiusId = Shader.PropertyToID("_HectonFloraPredatorThreatPositionRadius");
        private static readonly int _FloraLifecycleParamsId = Shader.PropertyToID("_HectonFloraLifecycleParams");
        private static readonly int _FloraCascadeParamsId = Shader.PropertyToID("_HectonFloraCascadeParams");
        private static readonly int _FloraDamageReactionId = Shader.PropertyToID("_HectonFloraDamageReaction");
        private static readonly int _CelestialRadiationStormId = Shader.PropertyToID("_HectonCelestialRadiationStorm");
        private static readonly int _SeasonCycleId = Shader.PropertyToID("_HectonSeasonCycle");
        private static readonly int _SeasonCycleAliasId = Shader.PropertyToID("_SeasonCycle");
        private static readonly int _SubmarineWashSphereId = Shader.PropertyToID("_HectonSubmarineWashSphere");
        private static readonly int _SubmarineWashVelocityId = Shader.PropertyToID("_HectonSubmarineWashVelocity");
        private static readonly int _SubmarinePropwashId = Shader.PropertyToID("SubmarinePropwash");
        private static readonly int _SubmarineWashAupGridId = Shader.PropertyToID("_HectonSubmarineWashAupGrid");
        private static readonly int _SubmarineWashAupLocalId = Shader.PropertyToID("_HectonSubmarineWashAupLocal");
        private static readonly int _WakeTrailTextureId = Shader.PropertyToID("_HectonVegetationWakeTrailRT");
        private static readonly int _WakeTrailWorldRectId = Shader.PropertyToID("_HectonVegetationWakeTrailWorldRect");
        private static readonly int _WakeTrailActiveId = Shader.PropertyToID("_HectonVegetationWakeTrailActive");
        private static readonly int _ShallowWaterFieldTextureId = Shader.PropertyToID("_HectonShallowWaterFieldRT");
        private static readonly int _ShallowWaterFieldWorldRectId = Shader.PropertyToID("_HectonShallowWaterFieldWorldRect");
        private static readonly int _ShallowWaterFieldActiveId = Shader.PropertyToID("_HectonShallowWaterFieldActive");
        private static readonly int _ShallowWaterFieldTexelSizeId = Shader.PropertyToID("_HectonShallowWaterFieldTexelSize");
        private static readonly int _WakeTrailSourceId = Shader.PropertyToID("_HectonWakeTrailSource");
        private static readonly int _WakeTrailResultId = Shader.PropertyToID("_HectonWakeTrailResult");
        private static readonly int _WakeTrailFadeDeltaId = Shader.PropertyToID("_HectonWakeTrailFadeDelta");
        private static readonly int _WakeTrailDiffusionId = Shader.PropertyToID("_HectonWakeTrailDiffusion");
        private static readonly int _WakeTrailWaveStrengthId = Shader.PropertyToID("_HectonWakeTrailWaveStrength");
        private static readonly int _WakeTrailDampingId = Shader.PropertyToID("_HectonWakeTrailDamping");
        private static readonly int _WakeTrailCurlStrengthId = Shader.PropertyToID("_HectonWakeTrailCurlStrength");
        private static readonly int _WakeTrailSimulationTimeId = Shader.PropertyToID("_HectonWakeTrailSimulationTime");
        private static readonly int _WakeTrailTexelSizeId = Shader.PropertyToID("_HectonWakeTrailTexelSize");
        private static readonly int _WakeTrailStampCommandsId = Shader.PropertyToID("_HectonWakeTrailStampCommands");
        private static readonly int _WakeTrailStampCountId = Shader.PropertyToID("_HectonWakeTrailStampCount");
        private static readonly int _WakeTrailScrollUvOffsetId = Shader.PropertyToID("_HectonWakeTrailScrollUvOffset");
        private static readonly int _ProceduralWakeBufferId = Shader.PropertyToID("_HectonFloraWakeBuffer");
        private static readonly int _ProceduralWakeCountId = Shader.PropertyToID("_HectonFloraWakeCount");
        private static readonly int _ProceduralWakeParamsId = Shader.PropertyToID("_HectonFloraWakeParams");
        private static readonly int _GlobalWakeBufferId = Shader.PropertyToID("_GlobalWakeBuffer");
        private static readonly int _GlobalWakeVectorBufferId = Shader.PropertyToID("_GlobalWakeVectors");
        private static readonly int _GlobalWakeParamsId = Shader.PropertyToID("_GlobalWakeParams");
        private static readonly int _FloraSwayDisplacementFieldId = Shader.PropertyToID("_HectonFloraSwayDisplacementField");
        private static readonly int _FloraSwayFieldCenterCellSizeId = Shader.PropertyToID("_HectonFloraSwayFieldCenterCellSize");
        private static readonly int _FloraSwayFieldParamsId = Shader.PropertyToID("_HectonFloraSwayFieldParams");
        private static readonly int _FloraSwayFieldRingOffsetId = Shader.PropertyToID("_HectonFloraSwayFieldRingOffset");
        private static readonly int _CulledFloraVisibleInstancesId = Shader.PropertyToID("_HectonCulledFloraVisibleInstances");
        private static readonly int _CulledFloraVisibleCountId = Shader.PropertyToID("_HectonCulledFloraVisibleCount");
        private static readonly int _ShearFoamAmountId = Shader.PropertyToID("_ShearFoamAmount");
        private static readonly int _ParasiteAnchorDataId = Shader.PropertyToID("_HectonParasiteAnchorData");
        private static readonly int _ParasiteAnchorParamsId = Shader.PropertyToID("_HectonParasiteAnchorParams");
        private static readonly int _ParasiteGlobalsId = Shader.PropertyToID("_HectonParasiteGlobals");

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Optional direct player override for direct scene play mode when BootstrapState has not published a runtime player yet.")]
        private Transform _playerTransformOverride;

        [SerializeField]
        [Tooltip("Optional direct scooter transform override for isolated prefab or broken-scene validation.")]
        private Transform _scooterTransformOverride;

        [SerializeField]
        [Tooltip("Optional vegetation bridge override used for dense-grass heuristics and sediment interaction bursts.")]
        private HectonMapMagicVegetationBridge _vegetationBridgeOverride;

        [SerializeField]
        [Tooltip("Optional organic destruction owner override used for chemical flora suppression in isolated scene validation.")]
        private DestructibleOrganicManager _destructibleOrganicManagerOverride;

        [Header("Interaction")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("Base radius around the player influence point for legacy prop-wash style vegetation response.")]
        private float _baseRadius = 3.5f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How much player speed increases the published legacy interaction radius.")]
        private float _velocityRadiusMultiplier = 0.45f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Maximum player interaction force pushed into legacy vegetation shader parameters.")]
        private float _maxInteractionForce = 4.2f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Position smoothing speed for the player interaction point.")]
        private float _positionSmoothSpeed = 12f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Radius and force smoothing speed for the player interaction point.")]
        private float _intensitySmoothSpeed = 8f;

        [Header("Velocity Bend")]
        [SerializeField, Range(1, MaxPublishedInteractionPoints)]
        [Tooltip("Maximum number of interaction points published to the global vegetation buffer, including the player.")]
        private int _maxInteractionPoints = 12;

        [SerializeField, Range(1.5f, 3f)]
        [Tooltip("Base true-bend radius used for the player interaction point.")]
        private float _playerBendRadius = 2.4f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Extra true-bend radius per meter per second of velocity.")]
        private float _dynamicVelocityRadiusMultiplier = 0.08f;

        [SerializeField, Range(2f, 3f)]
        [Tooltip("Maximum true-bend radius applied to interaction points.")]
        private float _maxBendRadius = 2.9f;

        [SerializeField, Range(1.5f, 4f)]
        [Tooltip("Base bend radius published for the active Manta scooter wake point.")]
        private float _scooterBendRadius = 2.8f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Velocity multiplier used for the active Manta scooter wake point.")]
        private float _scooterVelocityMultiplier = 1.35f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Forward offset used to move the scooter wake point ahead of the held tool transform.")]
        private float _scooterForwardOffset = 0.4f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Spring rise time for new or fast-changing vegetation interaction vectors.")]
        private float _interactionRiseSmoothTime = 0.12f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Spring recovery time used when interaction sources stop pushing the vegetation field.")]
        private float _interactionRecoverySmoothTime = 1.25f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Velocity threshold below which a recovered interaction point is dropped from publication.")]
        private float _interactionReleaseSpeed = 0.08f;

        [Header("Procedural Flora Sway Field")]
        [SerializeField, Range(0.1f, 12f)]
        [Tooltip("Exponential spring-back rate applied to the Vault-owned 3D displacement field.")]
        private float _floraSwaySpringDecayRate = 3.8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly abyssal/global current contributes to the base displacement field.")]
        private float _floraSwayGlobalCurrentInfluence = 0.18f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Multiplier applied to entity velocity/mass injection into the displacement field.")]
        private float _floraSwayEntityMassMultiplier = 1f;

        [SerializeField]
        [Tooltip("Editor and CI stress lane: deterministic invisible-object injection into the field.")]
        private bool _enableMockDisplacementInjector;

        [SerializeField]
        [Tooltip("Editor-only live x-ray of the Vault displacement field.")]
        private bool _drawFloraSwayDebugGizmos;

        [Header("Biolum Stealth")]
        [SerializeField, Range(2f, 48f)]
        [Tooltip("Search radius used to detect aggressive bioforms that should dim nearby flora emission around the player.")]
        private float _predatorThreatQueryRadius = 18f;

        [SerializeField, Range(1f, 32f)]
        [Tooltip("World radius around the player over which nearby flora emission is dimmed when predators are close.")]
        private float _predatorBiolumDimRadius = 10f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Global scalar applied to the predator-driven flora bioluminescence dimming effect.")]
        private float _predatorBiolumDimStrength = 0.75f;

        [SerializeField]
        [Tooltip("Physics layers considered when querying the nearest aggressive bioform for flora stealth dimming.")]
        private LayerMask _predatorThreatMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("Seasonal Lifecycle")]
        [SerializeField, Range(120f, 3600f)]
        [Tooltip("Simulation-time length in seconds of one flora bloom-to-decay lifecycle.")]
        private float _seasonCycleSeconds = 720f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized cycle position where the bloom phase peaks.")]
        private float _bloomPhaseCenterNormalized = 0.22f;

        [SerializeField, Range(0.05f, 0.45f)]
        [Tooltip("Normalized bloom phase half-width used to shape the seasonal bioluminescence window.")]
        private float _bloomPhaseWidthNormalized = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized cycle position where the decay phase peaks.")]
        private float _decayPhaseCenterNormalized = 0.76f;

        [SerializeField, Range(0.05f, 0.45f)]
        [Tooltip("Normalized decay phase half-width used to shape the seasonal wilt window.")]
        private float _decayPhaseWidthNormalized = 0.22f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum global bioluminescence multiplier applied during bloom windows.")]
        private float _bloomEmissionBoost = 2.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Global wilt deformation strength applied during decay windows.")]
        private float _decayWiltStrength = 0.82f;

        [Header("Allelopathic Toxin")]
        [SerializeField]
        [Tooltip("Stable flora template id that emits territorial toxin into the chemical influence grid.")]
        private string _bloodKelpAllelopathyStableId = "flora.blood_kelp";

        [SerializeField]
        [Tooltip("Stable flora template id that is suppressed by Blood Kelp toxicity in its chemical-grid cell.")]
        private string _ghostWeedAllelopathyStableId = "flora.ghost_weed";

        [SerializeField, Range(0.01f, 4f)]
        [Tooltip("Toxicity channel dose emitted by each active Blood Kelp during one SlowTick.")]
        private float _bloodKelpToxinDosePerSlowTick = 0.28f;

        [SerializeField, Range(0.001f, 1f)]
        [Tooltip("Normalized chemical-grid toxicity threshold where Ghost Weed enters bare/dead suppression.")]
        private float _ghostWeedSuppressionThreshold01 = 0.08f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Extra bloom weight injected while the encounter pacing director ramps or peaks.")]
        private float _encounterBloomBias = 0.16f;

        [SerializeField, Range(0f, 0.75f)]
        [Tooltip("Extra decay weight injected while the encounter pacing director enters Decay.")]
        private float _encounterDecayBias = 0.38f;

        [Header("Bioluminescent Cascades")]
        [SerializeField]
        [Tooltip("Stable flora template ids that participate in the reactive nerve-vine bioluminescent cascade network.")]
        private string[] _cascadeReactiveStableIds = { "flora.nerve_vine" };

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("World-space contact radius used to detect a player brushing into one reactive flora source.")]
        private float _cascadeContactRadius = 2.2f;

        [SerializeField, Range(8f, 320f)]
        [Tooltip("Maximum propagation radius used when a cascade wave spreads through the reactive flora network.")]
        private float _cascadePropagationRadius = 160f;

        [SerializeField, Range(1f, 40f)]
        [Tooltip("Wavefront speed in meters per second written into the reactive flora phase-seed buffer.")]
        private float _cascadePropagationSpeed = 15f;

        [SerializeField, Range(0.15f, 8f)]
        [Tooltip("Duration of the bright cascade crest once a reactive plant receives the propagated phase seed.")]
        private float _cascadePulseDurationSeconds = 2.4f;

        [SerializeField, Range(0.5f, 16f)]
        [Tooltip("Time window in seconds during which one cascade seed remains valid before fading back to idle.")]
        private float _cascadeReleaseDurationSeconds = 5.5f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra emission multiplier applied by the reactive flora shader while a cascade crest passes through an instance.")]
        private float _cascadeEmissionBoost = 2.2f;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Minimum time between retriggering the same cascade source while the player remains in contact.")]
        private float _cascadeRetriggerCooldownSeconds = 1.15f;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Cadence used to rebuild the reactive flora spatial hashes from the active streamed vegetation payloads.")]
        private float _cascadeSpatialRefreshIntervalSeconds = 0.5f;

        [Header("Toxic Spores")]
        [SerializeField]
        [Tooltip("Stable flora template id treated as a toxic-spore emitter when the player enters its near-field volume.")]
        private string _toxicSporeStableId = "flora.ghost_weed";

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("How often the flora runtime scans active instance payloads for toxic-spore emitters near the player.")]
        private float _toxicSporeScanIntervalSeconds = 0.2f;

        [SerializeField, Range(1f, 8f)]
        [Tooltip("Maximum world-space distance from the player to a toxic spore emitter before exposure begins.")]
        private float _toxicSporeDetectionRadius = 4.5f;

        [SerializeField, Range(1f, 8f)]
        [Tooltip("Hazard volume radius registered around the nearest toxic spore emitter.")]
        private float _toxicSporeHazardRadius = 3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Peak toxicity intensity registered into the trauma hazard pipeline while the player remains inside spores.")]
        private float _toxicSporeHazardIntensity = 0.78f;

        [SerializeField, Range(1f, 3f)]
        [Tooltip("External environmental drag multiplier applied to the player while spores remain active.")]
        private float _toxicSporeDragMultiplier = 1.3f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Extra visor glitch bias forwarded into the hazard runtime for toxic-spore interference.")]
        private float _toxicSporeVisorGlitchBias = 1.25f;

        [SerializeField, Range(8, 512)]
        [Tooltip("Maximum mature toxic flora instances sampled per lane during one 10s spore FrostTick.")]
        private int _matureToxicSporeEventScanBudget = 96;

        [Header("Base Parasitism")]
        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("How often the flora runtime rescans active module parasites and thermophilic growth state.")]
        private float _moduleParasiteScanIntervalSeconds = 0.5f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Maximum distance from a parasitic flora instance to a module contact before the latch is discarded.")]
        private float _moduleParasiteAttachmentRadius = 4f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Multiplier applied when a module parasite reaches full growth and exposes a logistics root node.")]
        private float _matureParasiteRootDrainMultiplier = 8f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Maximum distance from a module to the nearest active BioReactor used for thermophilic host validation.")]
        private float _thermophileReactorValidationRadius = 5f;

        [SerializeField]
        [Tooltip("Stable thermophilic flora id resolved for overheated module growth when the authored template is present.")]
        private string _thermalTubewormStableId = "flora.thermal_tubeworm";

        [SerializeField, Range(1, 4096)]
        [Tooltip("Maximum number of headless parasite nodes evaluated during one slow-tick job.")]
        private int _headlessParasiteCapacity = DefaultHeadlessParasiteCapacity;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Growth added per second to native parasite nodes before they expose CSR root drains.")]
        private float _headlessParasiteGrowthPerSecond = 0.035f;

        [SerializeField, Range(0.25f, 12f)]
        [Tooltip("In-game days required for one attached parasite to scale from seedling size to full overgrowth.")]
        private float _parasiteScaleGrowthDays = DefaultParasiteScaleGrowthDays;

        [SerializeField, Range(0.01f, 32f)]
        [Tooltip("Raw negative toxicity lane magnitude that kills a native parasite node as chemical defoliant.")]
        private float _headlessParasiteDefoliantKillThreshold = 1f;

        [SerializeField, Range(0f, 200f)]
        [Tooltip("Mass in kilograms contributed by one fully grown parasite per radius-cubed meter.")]
        private float _parasiteAddedMassKilogramsPerRadiusCubic = 45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum parasite insulation factor applied to infected modules at full growth.")]
        private float _parasiteThermalInsulationAtFullInfection = 0.85f;

        [SerializeField, Range(1f, 5f)]
        [Tooltip("Bio-reactor overheat multiplier applied when the host module is covered in mature parasites.")]
        private float _parasiteBioReactorOverheatMultiplier = 3f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Initial growth level used when a mature parasite spreads to the highest-potential CSR neighbor.")]
        private float _fungalMindSpreadSeedGrowth01 = 0.08f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Power-drain inheritance factor used for BFS-selected fungal mind spread targets.")]
        private float _fungalMindSpreadDrainScale = 0.5f;

        [Header("Defensive Spore Bursts")]
        [SerializeField]
        [Tooltip("Stable flora template ids that detonate into a defensive toxicity cloud when cut or drilled.")]
        private string[] _defensiveSporeBurstStableIds = { "flora.fungal_stalk", "flora.spore_cannon", "flora.acid_shroom" };

        [SerializeField, Range(1f, 20f)]
        [Tooltip("World-space radius of the defensive spore cloud injected after a reactive fungal stalk bursts.")]
        private float _defensiveSporeBurstRadius = 7f;

        [SerializeField, Range(0.25f, 16f)]
        [Tooltip("Chemical-grid toxicity dose injected at the burst origin when a reactive fungal stalk detonates.")]
        private float _defensiveSporeBurstDose = 8f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Lifetime in seconds of the localized blind/toxic zone created by a defensive spore burst.")]
        private float _defensiveSporeBurstLifetimeSeconds = 10f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Hazard intensity registered against the player while they remain inside a defensive spore cloud.")]
        private float _defensiveSporeHazardIntensity = 1.15f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Extra visor-trauma bias applied while the player remains inside a defensive spore cloud.")]
        private float _defensiveSporeVisorGlitchBias = 1.75f;

        [Header("Wake Trail")]
        [SerializeField, Range(64f, 192f)]
        [Tooltip("World-space coverage of the shallow-water field centered around the player.")]
        private float _wakeTrailWorldSize = 128f;

        [SerializeField, Range(8f, 16f)]
        [Tooltip("Seconds required for the persistent wake trail to fade out back to calm water.")]
        private float _wakeTrailFadeSeconds = 12f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Persistent wake intensity written by the player body when moving through vegetation.")]
        private float _wakeTrailPlayerStrength = 0.28f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Persistent wake intensity written by the active Manta scooter.")]
        private float _wakeTrailScooterStrength = 0.95f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base half-width of each wake trail stamp in world meters.")]
        private float _wakeTrailBaseRadius = 1.35f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum world-space trail length written per wake stamp.")]
        private float _wakeTrailMinLength = 2.4f;

        [SerializeField, Range(4f, 30f)]
        [Tooltip("Maximum world-space trail length written per wake stamp.")]
        private float _wakeTrailMaxLength = 15f;

        [SerializeField, Range(0.05f, 0.75f)]
        [Tooltip("Extra trail length written per meter per second of source velocity.")]
        private float _wakeTrailVelocityToLength = 0.28f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum player speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailPlayerMinSpeed = 0.75f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum scooter speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailScooterMinSpeed = 0.45f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Persistent wake intensity written by the active submarine while moving through macro-flora.")]
        private float _wakeTrailSubmarineStrength = 0.82f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum submarine speed required before persistent flora wake stamps start accumulating.")]
        private float _wakeTrailSubmarineMinSpeed = 2.5f;

        [SerializeField, Range(8f, 24f)]
        [Tooltip("Submarine speed threshold that upgrades the wake field into a kelp-whip stamp.")]
        private float _wakeTrailSubmarineWhipSpeed = 15f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Base world radius of the submarine flora wake stamp.")]
        private float _wakeTrailSubmarineRadius = 3.2f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Pixel stride used to quantize treadmill recentering and avoid sub-pixel wake shimmer.")]
        private float _wakeTrailCenterSnapPixelStride = 1f;

        [SerializeField]
        [Tooltip("Optional compute shader used to evolve the persistent wake trail into reactive spreading ripples.")]
        private ComputeShader _wakeTrailSimulationCompute;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Neighbor blending factor used by the wake ripple simulation.")]
        private float _wakeTrailDiffusion = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wave propagation strength used when the wake trail expands into surrounding water.")]
        private float _wakeTrailWaveStrength = 0.36f;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Per-step damping used by the wake ripple simulation.")]
        private float _wakeTrailWaveDamping = 0.94f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Curl-noise advection strength used to form micro-vortices inside the reactive wake field.")]
        private float _wakeTrailCurlStrength = 0.42f;

        [Header("Sediment Interaction")]
        [SerializeField]
        [Tooltip("Authored scene particle system used for sediment bursts kicked out of dense grass. Player runtime never creates this object.")]
        private ParticleSystem _sedimentBurstParticleSystem;

        [SerializeField, Range(1024, 65535)]
        [Tooltip("Minimum active surface instance count required before the manager considers the current area dense enough for grass sediment bursts.")]
        private int _denseGrassInstanceThreshold = 8192;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum speed required before player movement starts emitting sediment bursts in dense grass.")]
        private float _playerSedimentMinSpeed = 4.5f;

        [SerializeField, Range(1f, 30f)]
        [Tooltip("Minimum speed required before scooter wake starts emitting sediment bursts in dense grass.")]
        private float _scooterSedimentMinSpeed = 7.5f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between player sediment burst emissions.")]
        private float _playerSedimentCooldown = 0.16f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between scooter sediment burst emissions.")]
        private float _scooterSedimentCooldown = 0.09f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base world radius of one sediment burst stamp.")]
        private float _sedimentBurstRadius = 1.3f;

        [SerializeField, Range(2, 32)]
        [Tooltip("Maximum particle count emitted by one dense-grass sediment burst.")]
        private int _sedimentMaxBurstCount = 18;

        private Vector3 _smoothPosition;
        private float _smoothRadius;
        private float _smoothForce;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _playerToolManager;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private bool _playerReferenceRefreshRequested = true;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private Rigidbody _submarineHullRigidbody;
        private IFluidSurfaceCurrentReadModel _fluidReadModel;
        private MapMagicBridge _mapMagicRuntime;
        private HectonCelestialEngine _celestialEngine;
        private IAtmosphereReadModel _atmosphereReadModel;
        private IConstructionParasiteGraphService _constructionParasiteGraph;
        private ISaveService _saveService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private Transform _activeScooterTransform;
        private Vector3 _lastPlayerPosition;
        private Vector3 _lastPublishedPlayerVelocity;
        private Vector3 _lastPublishedScooterWakePosition;
        private Vector3 _lastPublishedSubmarineWakePosition;
        private Vector3 _lastVegetationCurrentVector;
        private Vector3 _smoothedPlayerVelocity;
        private Vector3 _smoothedPlayerVelocityDamp;
        private Vector3 _smoothedScooterVelocity;
        private Vector3 _smoothedScooterVelocityDamp;
        private Vector3 _smoothedScooterPosition;
        private Vector3 _smoothedScooterPositionDamp;
        private bool _hasLastPlayerPosition;
        private bool _hasSmoothedScooterPosition;
        private bool _hasActiveScooterWake;
        private bool _hasActiveSubmarineWake;
        private bool _isRegistered;
        private bool _isSlowTickRegistered;
        private bool _isLateFrameRegistered;
        private int _lastPublishedInteractionCount;
        private int _pendingVisualInteractionCount;
        private int _externalInteractionCount;
        private Vector4 _pendingPropWashPositionRadius;
        private Vector3 _damageReactionPositionWS;
        private float _pendingPropWashForce;
        private float _damageReactionStrength;
        private float _damageReactionRemainingSeconds;
        private bool _interactionGlobalsDirty;
        private bool _environmentGlobalsDirty;
        private bool _wakeTrailGlobalsDirty;
        private bool _submarineWashGlobalsDirty;
        private bool _damageReactionGlobalsDirty;
        private bool _flowFieldRefreshRequested;
        private bool _flowFieldGlobalsDirty;
        private bool _proceduralWakeTickRequested;
        private bool _proceduralWakeGlobalsDirty;
        private bool _proceduralWakeForceUpload;
        private bool _wakeTrailTickRequested;
        private bool _wakeTrailClearRequested;
        private bool _wakeTrailResourceRefreshRequested;
        private bool _floraSwayFieldGlobalsDirty;
        private bool _floraSwayFieldForceUpload;
        private bool _parasiteInfectionGlobalsDirty;
        private bool _playerRuntimePositionDirty;
        private bool _interactionResetGlobalsDirty;
        private bool _sedimentBurstVisualRequested;
        private Vector3 _pendingEnvironmentSamplePositionWS;
        private Vector3 _pendingSedimentBurstPositionWS;
        private Vector3 _pendingSedimentBurstVelocityWS;
        private Vector3 _pendingWakeTrailPlayerPositionWS;
        private Vector3 _pendingWakeTrailPlayerVelocityWS;
        private Vector4 _pendingPlayerRuntimePosition;
        private Vector4 _pendingPlayerFloraInteractionParams;
        private float _pendingDamageReactionDeltaTime;
        private float _pendingFlowFieldDeltaTime;
        private float _pendingProceduralWakeDeltaTime;
        private float _pendingWakeTrailDeltaTime;
        private uint _pendingWakeTelemetryFlags;

        private FloraInteractionPointGpuData[] _interactionPoints;
        private FloraInteractionPointGpuData[] _externalInteractionPoints;
        private GraphicsBuffer _interactionBufferA;
        private GraphicsBuffer _interactionBufferB;
        private GraphicsBuffer _activeInteractionBuffer;
        private int _interactionBufferWriteIndex;
        private GraphicsBuffer _flowFieldBufferA;
        private GraphicsBuffer _flowFieldBufferB;
        private GraphicsBuffer _activeFlowFieldBuffer;
        private int _flowFieldUploadBufferIndex;
        private GraphicsBuffer _floraSwayFieldBufferA;
        private GraphicsBuffer _floraSwayFieldBufferB;
        private GraphicsBuffer _floraSwayFieldReadBuffer;
        private GraphicsBuffer _wakeTrailStampCommandBufferA;
        private GraphicsBuffer _wakeTrailStampCommandBufferB;
        private GraphicsBuffer _activeWakeTrailStampCommandBuffer;
        private int _wakeTrailStampCommandUploadBufferIndex;
        private IInstanceCullingService _instanceCullingService;
        private bool _cullingHotSwapRegistered;
        private RenderTexture _wakeTrailRead;
        private RenderTexture _wakeTrailWrite;
        private Vector4 _wakeTrailWorldRect;
        private Vector2 _wakeTrailCenterXZ;
        private Vector2 _pendingWakeTrailScrollUv;
        private VaultGenerationHandle<WakeTrailStampCommand> _wakeTrailStampCommandsHandle;
        private VaultGenerationHandle<WakeSource> _proceduralWakePointsHandle;
        private VaultGenerationHandle<float4> _globalWakeBufferHandle;
        private VaultGenerationHandle<float4> _globalWakeVectorBufferHandle;
        private VaultGenerationHandle<WakeTelemetryEntry> _wakeBlackBoxHandle;
        private VaultGenerationHandle<FloraDisplacementDTO> _floraSwayFieldHandle;
        private VaultGenerationHandle<float4> _floraSwayFieldMetaHandle;
        private VaultGenerationHandle<FloraSwayFieldTelemetryEntry> _floraSwayFieldBlackBoxHandle;
        private VaultGenerationHandle<FloraStiffnessRuleDTO> _floraStiffnessRulesHandle;
        private VaultGenerationHandle<byte> _floraStiffnessCsvScratchHandle;
        private IDataVault _wakeDataVault;
        private Vector4[] _proceduralWakeShaderBuffer;
        private Vector4[] _globalWakeShaderBuffer;
        private Vector4[] _globalWakeVectorShaderBuffer;
        private int _publishedProceduralWakeCount;
        private float _publishedShearFoamAmount;
        private int _wakeBlackBoxCursor;
        private int _floraSwayFieldBlackBoxCursor;
        private uint _proceduralWakeSignalFrameCounter;
        private uint _floraSwayFieldPendingFlags;
        private int _floraSwayFieldPendingActiveWakeCount;
        private int _floraSwayFieldPendingNonZeroCells;
        private uint _floraSwayFieldLastCpuMicroseconds;
        private uint _floraSwaySimulationFrameCounter;
        private double _floraSwayFieldScheduleTimestampSeconds;
        private bool _wakeBlackBoxDumped;
        private bool _floraSwayFieldBlackBoxDumped;
        private bool _proceduralWakeGlobalsInitialized;
        private bool _floraSwayFieldGlobalsInitialized;
        private bool _floraSwayFieldActive;
        private bool _floraSwayFieldUseBufferA;
        private bool _floraSwayFieldDiscardScheduledUpload;
        private float _wakeTrailRuntimeWorldSize;
        private float _wakeTrailEnergy;
        private float _playerSedimentCooldownRemaining;
        private float _scooterSedimentCooldownRemaining;
        private bool _wakeTrailDisabled;
        private int _queuedWakeTrailStampCount;
        private uint _wakeTrailDispatchSerial;
        private uint _lastWakeTrailDispatchSerial;
        private int _wakeTrailRuntimeResolution;
        private int _wakeTrailQualityMilli = -1;
        private int _wakeTrailSimulationKernel = -1;
        private int _wakeTrailThreadGroupSizeX;
        private int _wakeTrailThreadGroupSizeY;
        private bool _supportsWakeTrailComputeCold;
        private bool _vegetationBridgeResolveRequested;
        private int _flowFieldResolution;
        private int _floraSwayFieldResolution;
        private int _floraSwayFieldNodeCount;
        private int3 _floraSwayFieldRingOffset;
        private int3 _floraSwayFieldLastCenterShiftCells;
        private int3 _floraSwayFieldPendingRingOffset;
        private int3 _floraSwayFieldPendingCenterShiftCells;
        private float _flowFieldCellSize;
        private float _floraSwayFieldCellSize;
        private float _floraSwayFieldQualityWeight;
        private float _floraSwayFieldLayoutQualityWeight = -1f;
        private float _floraSwayFieldMaxMagnitude;
        private float _floraSwayFieldUpdateTimer;
        private float _floraSwayFieldUpdateIntervalSeconds;
        private float _flowFieldUploadTimer;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private DestructibleOrganicManager _destructibleOrganicManager;
        private IHectonOceanKinematics _oceanKinematicsProvider;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _floraSwayFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        private Vector3 _lastUploadedFloraSwayFieldCenterWS;
        private AbsoluteUniversePosition _lastUploadedFloraSwayFieldCenterAup;
        private AbsoluteUniversePosition _floraSwayFieldPendingCenterAup;
        private bool _floraSwayFieldCenterAupValid;
        private VaultGenerationHandle<Vector3> _oceanFlowSamplePositionsHandle;
        private VaultGenerationHandle<Vector3> _oceanFlowSampleResultsHandle;
        private ParticleSystem.EmitParams _sedimentEmitParams;
        private bool[] _toxicSporeTemplateMask = Array.Empty<bool>();
        private int _cachedToxicSporeTemplateCount = -1;
        private int _toxicSporeStableHashId;
        private int _thermalTubewormStableHashId;
        private int _bloodKelpAllelopathyStableHashId;
        private int _ghostWeedAllelopathyStableHashId;
        private int _cachedAllelopathicTemplateCount = -1;
        private float _toxicSporeScanTimer;
        private float _nextMatureToxicSporeEventTime;
        private float _lastToxicSporeExposure01;
        private int _surfaceMatureToxicSporeScanCursor;
        private int _underwaterMatureToxicSporeScanCursor;
        private float _moduleParasiteScanTimer;
        private SpatialQueryHit[] _moduleQueryHits;
        private SpatialQueryHit[] _predatorThreatQueryHits;
        private readonly Vector4[] _parasiteAnchorData = new Vector4[MaxParasiteAnchors];
        private readonly Vector4[] _parasiteAnchorParams = new Vector4[MaxParasiteAnchors];
        private int _publishedParasiteAnchorCount;
        private BaseModule[] _moduleParasiteStateFrontModules = new BaseModule[MaxTrackedModuleParasiteStates];
        private ModuleParasiteState[] _moduleParasiteStateFrontValues = new ModuleParasiteState[MaxTrackedModuleParasiteStates];
        private int _moduleParasiteStateFrontCount;
        private BaseModule[] _moduleParasiteStateBackModules = new BaseModule[MaxTrackedModuleParasiteStates];
        private ModuleParasiteState[] _moduleParasiteStateBackValues = new ModuleParasiteState[MaxTrackedModuleParasiteStates];
        private int _moduleParasiteStateBackCount;
        private readonly BaseModule[] _thermophileDwellModules = new BaseModule[MaxTrackedThermophileDwellModules];
        private readonly float[] _thermophileDwellSeconds = new float[MaxTrackedThermophileDwellModules];
        private int _thermophileDwellCount;
        private VaultGenerationHandle<ParasiteNode> _parasiteNodesHandle;
        private JobHandle _parasiteGrowthHandle;
        private JobHandle _wakeDecayHandle;
        private int _parasiteNodeCount;
        private bool _parasiteGrowthScheduled;
        private bool _wakeDecayScheduled;
        private double _lastWakeDecayTimestampSeconds;
        private int[] _cascadeReactiveStableHashIds = Array.Empty<int>();
        private int[] _defensiveSporeBurstStableHashIds = Array.Empty<int>();
        private int _cachedCascadeReactiveTemplateCount = -1;
        private int _cachedDefensiveSporeBurstTemplateCount = -1;
        private float _cascadeSpatialRefreshTimer;
        private float _lastSurfaceCascadeTriggerTime = float.MinValue;
        private float _lastUnderwaterCascadeTriggerTime = float.MinValue;
        private int _lastSurfaceCascadeSourcePayloadIndex = -1;
        private int _lastUnderwaterCascadeSourcePayloadIndex = -1;
        private int _lastSurfacePlayerContactPayloadIndex = -1;
        private int _lastUnderwaterPlayerContactPayloadIndex = -1;
        private VaultGenerationHandle<byte> _cascadeReactiveTemplateMaskHandle;
        private VaultGenerationHandle<byte> _defensiveSporeBurstTemplateMaskHandle;
        private VaultGenerationHandle<byte> _allelopathicBloodKelpTemplateMaskHandle;
        private VaultGenerationHandle<byte> _allelopathicGhostWeedTemplateMaskHandle;
        private VaultGenerationHandle<float> _surfaceCascadePhaseSeedsHandle;
        private VaultGenerationHandle<float> _underwaterCascadePhaseSeedsHandle;
        private VaultGenerationHandle<FloraCascadeEventPayload> _surfaceCascadeEventsHandle;
        private VaultGenerationHandle<FloraCascadeEventPayload> _underwaterCascadeEventsHandle;
        private VaultGenerationHandle<int> _surfaceReactiveFloraHandlesHandle;
        private VaultGenerationHandle<int> _underwaterReactiveFloraHandlesHandle;
        private VaultGenerationHandle<int> _reactiveFloraQueryHandlesHandle;
        private VaultGenerationHandle<FloraMemoryTelemetryEntry> _floraMemoryTelemetryHandle;
        private IDataVault _parasiteNodesWriteVault;
        private IDataVault _cascadeReactiveTemplateMaskWriteVault;
        private IDataVault _defensiveSporeBurstTemplateMaskWriteVault;
        private IDataVault _surfaceCascadePhaseSeedsWriteVault;
        private IDataVault _underwaterCascadePhaseSeedsWriteVault;
        private IDataVault _surfaceCascadeEventsWriteVault;
        private IDataVault _underwaterCascadeEventsWriteVault;
        private IDataVault _surfaceReactiveFloraHandlesWriteVault;
        private IDataVault _underwaterReactiveFloraHandlesWriteVault;
        private IDataVault _reactiveFloraQueryHandlesWriteVault;
        private IDataVault _floraMemoryTelemetryWriteVault;
        private int _surfaceReactiveFloraHandleCount;
        private int _underwaterReactiveFloraHandleCount;
        private int _floraMemoryTelemetryCursor;
        private int _floraMemoryConsecutiveFailureCount;
        private bool _floraMemoryTelemetryDumped;
        private HectonSpatialHash _surfaceReactiveFloraHash;
        private HectonSpatialHash _underwaterReactiveFloraHash;
        private GraphicsBuffer _surfaceCascadePhaseSeedBuffer;
        private GraphicsBuffer _underwaterCascadePhaseSeedBuffer;
        private DefensiveSporeBurstState[] _defensiveSporeBursts;
        private int _defensiveSporeBurstCount;
        private int _surfaceCascadeEventCount;
        private int _underwaterCascadeEventCount;
        private JobHandle _surfaceCascadePhaseSeedHandle;
        private JobHandle _underwaterCascadePhaseSeedHandle;
        private JobHandle _floraSwayFieldBuildHandle;
        private int _surfaceCascadePhaseSeedUploadCount;
        private int _underwaterCascadePhaseSeedUploadCount;
        private int _surfaceCascadePhaseSeedQueuedUploadCount;
        private int _underwaterCascadePhaseSeedQueuedUploadCount;
        private bool _surfaceCascadePhaseSeedScheduled;
        private bool _underwaterCascadePhaseSeedScheduled;
        private bool _surfaceCascadePhaseSeedReleaseQueued;
        private bool _underwaterCascadePhaseSeedReleaseQueued;
        private bool _floraSwayFieldBuildScheduled;

        /// <summary>Last interaction point count pushed into the global flora buffer.</summary>
        public int PublishedInteractionCount => _lastPublishedInteractionCount;

        /// <summary>True when the active Manta scooter wake point is currently being published.</summary>
        public bool HasActiveScooterWake => _hasActiveScooterWake;

        public static FloraInteractionManager ActiveRuntimeInstance => s_ActiveRuntimeInstance;

#if UNITY_EDITOR
        public float FloraSwayMaxMagnitudeForEditor => _floraSwayFieldMaxMagnitude;
        public int FloraSwayResolutionForEditor => _floraSwayFieldResolution;
        public int FloraSwayNonZeroCellsForEditor => _floraSwayFieldPendingNonZeroCells;
#endif

        /// <summary>Last published player velocity vector.</summary>
        public Vector3 LastPublishedPlayerVelocity => _lastPublishedPlayerVelocity;

        /// <summary>Last published scooter wake anchor position.</summary>
        public Vector3 LastPublishedScooterWakePosition => _lastPublishedScooterWakePosition;

        /// <inheritdoc />
        public bool IsInitialized =>
            IsFloraVaultHandle(in _proceduralWakePointsHandle, BufferID.WakeSources) &&
            IsFloraVaultHandle(in _floraSwayFieldHandle, FloraSwayDisplacementFieldBufferId) &&
            _proceduralWakeShaderBuffer != null;

        /// <inheritdoc />
        public int ActiveWakeCount => _publishedProceduralWakeCount;

        /// <summary>Approximate VRAM footprint in bytes for the wake-trail ping-pong textures and interaction buffer.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_interactionBufferA);
            totalBytes += EstimateGraphicsBufferBytes(_interactionBufferB);
            totalBytes += EstimateGraphicsBufferBytes(_flowFieldBufferA);
            totalBytes += EstimateGraphicsBufferBytes(_flowFieldBufferB);
            totalBytes += EstimateGraphicsBufferBytes(_floraSwayFieldBufferA);
            totalBytes += EstimateGraphicsBufferBytes(_floraSwayFieldBufferB);
            totalBytes += EstimateGraphicsBufferBytes(_surfaceCascadePhaseSeedBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_underwaterCascadePhaseSeedBuffer);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailRead);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailWrite);
            return totalBytes;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ActiveRuntimeInstance = null;
            Volatile.Write(ref s_x001FloraInteractionManagerSignalPushDropCount, 0);
            DroneFleetManager.ClearFloraInteractionManager(null);
        }

        private void Awake()
        {
            s_ActiveRuntimeInstance = this;
            DroneFleetManager.BindFloraInteractionManager(this);
            _maxInteractionPoints = Mathf.Clamp(_maxInteractionPoints, 1, MaxPublishedInteractionPoints);
            _wakeTrailWorldSize = ClampFinite(_wakeTrailWorldSize, 128f, 64f, 192f);
            _wakeTrailFadeSeconds = ClampFinite(_wakeTrailFadeSeconds, 12f, 0.1f, 16f);
            _wakeTrailDiffusion = ClampFinite(_wakeTrailDiffusion, 0.22f, 0f, 1f);
            _wakeTrailWaveStrength = ClampFinite(_wakeTrailWaveStrength, 0.36f, 0f, 1f);
            _wakeTrailWaveDamping = ClampFinite(_wakeTrailWaveDamping, 0.92f, 0.5f, 1f);
            _denseGrassInstanceThreshold = Mathf.Max(1024, _denseGrassInstanceThreshold);
            _sedimentMaxBurstCount = Mathf.Clamp(_sedimentMaxBurstCount, 2, 32);
            _predatorThreatQueryRadius = Mathf.Max(15f, _predatorThreatQueryRadius);
            _predatorBiolumDimRadius = Mathf.Max(15f, _predatorBiolumDimRadius);
            _predatorBiolumDimStrength = Mathf.Clamp01(_predatorBiolumDimStrength);
            _seasonCycleSeconds = Mathf.Max(120f, _seasonCycleSeconds);
            _bloomPhaseWidthNormalized = Mathf.Clamp(_bloomPhaseWidthNormalized, 0.05f, 0.45f);
            _decayPhaseWidthNormalized = Mathf.Clamp(_decayPhaseWidthNormalized, 0.05f, 0.45f);
            _bloomEmissionBoost = Mathf.Max(1f, _bloomEmissionBoost);
            _decayWiltStrength = Mathf.Clamp01(_decayWiltStrength);
            _bloodKelpToxinDosePerSlowTick = Mathf.Max(0.01f, _bloodKelpToxinDosePerSlowTick);
            _ghostWeedSuppressionThreshold01 = Mathf.Clamp(_ghostWeedSuppressionThreshold01, 0.001f, 1f);
            _encounterBloomBias = Mathf.Clamp(_encounterBloomBias, 0f, 0.5f);
            _encounterDecayBias = Mathf.Clamp(_encounterDecayBias, 0f, 0.75f);
            _cascadeContactRadius = Mathf.Max(0.5f, _cascadeContactRadius);
            _cascadePropagationRadius = Mathf.Max(8f, _cascadePropagationRadius);
            _cascadePropagationSpeed = Mathf.Max(1f, _cascadePropagationSpeed);
            _cascadePulseDurationSeconds = Mathf.Max(0.15f, _cascadePulseDurationSeconds);
            _cascadeReleaseDurationSeconds = Mathf.Max(_cascadePulseDurationSeconds, _cascadeReleaseDurationSeconds);
            _cascadeEmissionBoost = Mathf.Max(0f, _cascadeEmissionBoost);
            _cascadeRetriggerCooldownSeconds = Mathf.Max(0.1f, _cascadeRetriggerCooldownSeconds);
            _cascadeSpatialRefreshIntervalSeconds = Mathf.Max(0.1f, _cascadeSpatialRefreshIntervalSeconds);
            SanitizeSporeHazardAuthoringValues();
            _matureToxicSporeEventScanBudget = Mathf.Clamp(_matureToxicSporeEventScanBudget, 8, 512);
            _moduleParasiteScanIntervalSeconds = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds);
            _moduleParasiteAttachmentRadius = Mathf.Max(0.5f, _moduleParasiteAttachmentRadius);
            _matureParasiteRootDrainMultiplier = Mathf.Max(1f, _matureParasiteRootDrainMultiplier);
            _thermophileReactorValidationRadius = Mathf.Max(0.5f, _thermophileReactorValidationRadius);
            _headlessParasiteCapacity = Mathf.Clamp(_headlessParasiteCapacity, 1, 4096);
            _headlessParasiteGrowthPerSecond = Mathf.Clamp01(_headlessParasiteGrowthPerSecond);
            _headlessParasiteDefoliantKillThreshold = Mathf.Max(0.01f, _headlessParasiteDefoliantKillThreshold);
            _parasiteAddedMassKilogramsPerRadiusCubic = Mathf.Max(0f, _parasiteAddedMassKilogramsPerRadiusCubic);
            _parasiteThermalInsulationAtFullInfection = Mathf.Clamp01(_parasiteThermalInsulationAtFullInfection);
            _parasiteBioReactorOverheatMultiplier = Mathf.Max(1f, _parasiteBioReactorOverheatMultiplier);
            _fungalMindSpreadSeedGrowth01 = Mathf.Clamp(_fungalMindSpreadSeedGrowth01, 0.01f, 0.25f);
            _fungalMindSpreadDrainScale = Mathf.Clamp(_fungalMindSpreadDrainScale, 0.1f, 1f);
            _floraSwaySpringDecayRate = Mathf.Clamp(_floraSwaySpringDecayRate, 0.1f, 12f);
            _floraSwayGlobalCurrentInfluence = Mathf.Clamp01(_floraSwayGlobalCurrentInfluence);
            _floraSwayEntityMassMultiplier = Mathf.Clamp(_floraSwayEntityMassMultiplier, 0f, 4f);
            _wakeTrailQualityMilli = ResolveWakeTrailQualityMilli();
            _wakeTrailRuntimeResolution = ResolveWakeTrailResolutionForQualityWeight(_wakeTrailQualityMilli * 0.001f);
            _supportsWakeTrailComputeCold = SystemInfo.supportsComputeShaders;
            RefreshVegetationBridgeCold();
            _destructibleOrganicManager = ResolveDestructibleOrganicManagerCold();
            _toxicSporeStableHashId = string.IsNullOrWhiteSpace(_toxicSporeStableId) ? 0 : LocHash.Compute(_toxicSporeStableId);
            _thermalTubewormStableHashId = string.IsNullOrWhiteSpace(_thermalTubewormStableId) ? 0 : LocHash.Compute(_thermalTubewormStableId);
            _bloodKelpAllelopathyStableHashId = string.IsNullOrWhiteSpace(_bloodKelpAllelopathyStableId) ? 0 : LocHash.Compute(_bloodKelpAllelopathyStableId);
            _ghostWeedAllelopathyStableHashId = string.IsNullOrWhiteSpace(_ghostWeedAllelopathyStableId) ? 0 : LocHash.Compute(_ghostWeedAllelopathyStableId);
            CacheStableHashIds(_cascadeReactiveStableIds, ref _cascadeReactiveStableHashIds);
            CacheStableHashIds(_defensiveSporeBurstStableIds, ref _defensiveSporeBurstStableHashIds);
            CacheEnvironmentRuntimeServicesCold();
            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute != null && _supportsWakeTrailComputeCold)
            {
                _wakeTrailSimulationKernel = ResolveKernel(_wakeTrailSimulationCompute, "SimulateWakeTrail");
                ResolveKernelThreadGroupSizes(
                    _wakeTrailSimulationCompute,
                    _wakeTrailSimulationKernel,
                    out _wakeTrailThreadGroupSizeX,
                    out _wakeTrailThreadGroupSizeY);
            }

            // COLD ALLOC: FloraInteractionPointGpuData[_maxInteractionPoints] - global vegetation interaction payload - owner: FloraInteractionManager
            _interactionPoints = new FloraInteractionPointGpuData[_maxInteractionPoints];
            // COLD ALLOC: FloraInteractionPointGpuData[4] - external tool-impact vegetation interaction payloads - owner: FloraInteractionManager
            _externalInteractionPoints = new FloraInteractionPointGpuData[MaxExternalInteractionPoints];
            // COLD ALLOC: Vector4[16] - fixed global procedural wake shader payload - owner: FloraInteractionManager
            _proceduralWakeShaderBuffer = new Vector4[MaxProceduralWakePoints];
            // COLD ALLOC: Vector4[16] - global wake source payload for cross-shader displacement - owner: FloraInteractionManager
            _globalWakeShaderBuffer = new Vector4[MaxProceduralWakePoints];
            // COLD ALLOC: Vector4[16] - global wake direction/radius payload for shader and compute consumers - owner: FloraInteractionManager
            _globalWakeVectorShaderBuffer = new Vector4[MaxProceduralWakePoints];
            // COLD ALLOC: SpatialQueryHit[32] - module-contact query results for parasitic flora host resolution - owner: FloraInteractionManager
            _moduleQueryHits = new SpatialQueryHit[MaxModuleQueryHits];
            // COLD ALLOC: SpatialQueryHit[16] - predator bioform query results for flora bioluminescence stealth - owner: FloraInteractionManager
            _predatorThreatQueryHits = new SpatialQueryHit[MaxPredatorThreatQueryHits];
            _interactionBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraInteractionPointGpuData>(_maxInteractionPoints); // COLD ALLOC: GraphicsBuffer[_maxInteractionPoints] A - global vegetation interaction StructuredBuffer - owner: FloraInteractionManager
            _interactionBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraInteractionPointGpuData>(_maxInteractionPoints); // COLD ALLOC: GraphicsBuffer[_maxInteractionPoints] B - global vegetation interaction StructuredBuffer - owner: FloraInteractionManager
            _activeInteractionBuffer = _interactionBufferA;
            ResolveProceduralWakeVaultBuffer(clearExisting: true);
            ResolveFloraSwayFieldVaultBuffer(clearExisting: false);
            ResolveFloraAuxiliaryVaultBuffers(clearExisting: true);
            GenerateEmergencyMockStiffness();
            EnsureFloraSwayFieldGraphicsBuffers();
            EnsureStructuredFloatBuffer(ref _surfaceCascadePhaseSeedBuffer, CascadePhaseSeedCapacity);
            EnsureStructuredFloatBuffer(ref _underwaterCascadePhaseSeedBuffer, CascadePhaseSeedCapacity);
            EnsureReactiveFloraHashCapacity(ref _surfaceReactiveFloraHash, ReactiveFloraHandleCapacity, allowCreate: true);
            EnsureReactiveFloraHashCapacity(ref _underwaterReactiveFloraHash, ReactiveFloraHandleCapacity, allowCreate: true);
            _defensiveSporeBursts = new DefensiveSporeBurstState[MaxDefensiveSporeBursts]; // COLD ALLOC: DefensiveSporeBurstState[6] - bounded active toxicity cloud descriptors - owner: FloraInteractionManager

            Shader.SetGlobalBuffer(_InteractionBufferId, _activeInteractionBuffer);
            PublishFlowFieldGlobals();
            PublishFloraSwayFieldGlobals(forceUpload: true);
            CreateWakeTrailResources();
            EnsureSedimentParticleSystem();
            RebuildToxicSporeTemplateMaskColdFromCurrentBridge();
            RebuildCascadeTemplateMaskColdFromCurrentBridge();
            RebuildDefensiveSporeBurstTemplateMaskColdFromCurrentBridge();
            RebuildAllelopathicTemplateMasksColdFromCurrentBridge();
            ResetInteractionGlobals();
            PublishEnvironmentGlobals(Vector3.zero);
            PublishParasiteInfectionGlobals();
        }

        private void OnEnable()
        {
            s_ActiveRuntimeInstance = this;
            DroneFleetManager.BindFloraInteractionManager(this);

            if (_activeInteractionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _activeInteractionBuffer);

            HectonFloatingOrigin.RegisterListener(this);
            GlobalRegistry.RegisterWakeDisplacementService(this);
            CacheEnvironmentRuntimeServicesCold();
            RefreshPlayerReferenceCacheCold();
            ResolveProceduralWakeVaultBuffer(clearExisting: false);
            ResolveFloraSwayFieldVaultBuffer(clearExisting: false);
            ResolveFloraAuxiliaryVaultBuffers(clearExisting: false);
            EnsureFloraSwayFieldGraphicsBuffers();
            EnsureStructuredFloatBuffer(ref _surfaceCascadePhaseSeedBuffer, CascadePhaseSeedCapacity);
            EnsureStructuredFloatBuffer(ref _underwaterCascadePhaseSeedBuffer, CascadePhaseSeedCapacity);
            EnsureReactiveFloraHashCapacity(ref _surfaceReactiveFloraHash, ReactiveFloraHandleCapacity, allowCreate: true);
            EnsureReactiveFloraHashCapacity(ref _underwaterReactiveFloraHash, ReactiveFloraHandleCapacity, allowCreate: true);
            CacheInstanceCullingServiceCold();
            TryRegisterCullingHotSwapListener();
            RefreshCachedSubmarineContext();
            PublishFlowFieldGlobals();
            PublishFloraSwayFieldGlobals(forceUpload: true);
            PublishWakeTrailGlobals();
            PublishProceduralWakeBuffer(forceUpload: true);
            TryRegister();
            PublishEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : Vector3.zero);
        }

        private void OnDisable()
        {
            if (s_ActiveRuntimeInstance == this)
                s_ActiveRuntimeInstance = null;

            DroneFleetManager.ClearFloraInteractionManager(this);
            HectonFloatingOrigin.UnregisterListener(this);
            GlobalRegistry.UnregisterWakeDisplacementService(this);
            TryUnregisterCullingHotSwapListener();
            CompleteWakeDecayJob(forceComplete: true, dispatcherSwapWindow: false);
            CompleteFloraSwayFieldJobForTeardown(uploadAfterComplete: true);
            _instanceCullingService = null;
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineHullRigidbody = null;
            _fluidReadModel = null;
            _celestialEngine = null;
            _atmosphereReadModel = null;
            _constructionParasiteGraph = null;
            _saveService = null;
            _oceanKinematicsService = null;
            _oceanKinematicsProvider = null;
            TryUnregister();
            ClearToxicSporeHazard();
            ClearDefensiveSporeHazard();
            ClearModuleParasiteState();
            ClearWakeBuffer();
            ResetInteractionGlobals();
            ClearReactiveFloraSpatialState(forceCompleteJobs: true);
        }

        private void OnDestroy()
        {
            if (s_ActiveRuntimeInstance == this)
                s_ActiveRuntimeInstance = null;

            DroneFleetManager.ClearFloraInteractionManager(this);
            HectonFloatingOrigin.UnregisterListener(this);
            GlobalRegistry.UnregisterWakeDisplacementService(this);
            TryUnregisterCullingHotSwapListener();
            CompleteWakeDecayJob(forceComplete: true, dispatcherSwapWindow: false);
            CompleteFloraSwayFieldJobForTeardown(uploadAfterComplete: true);
            _instanceCullingService = null;
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineHullRigidbody = null;
            _fluidReadModel = null;
            _celestialEngine = null;
            _atmosphereReadModel = null;
            _constructionParasiteGraph = null;
            _saveService = null;
            _oceanKinematicsService = null;
            _oceanKinematicsProvider = null;
            TryUnregister();
            ClearToxicSporeHazard();
            ClearDefensiveSporeHazard();
            ClearModuleParasiteState();
            ClearWakeBuffer();
            ResetInteractionGlobals();
            ClearReactiveFloraSpatialState(forceCompleteJobs: true);

            ReleaseFloraAuxiliaryVaultBuffers();
            _proceduralWakePointsHandle = default;
            _globalWakeBufferHandle = default;
            _globalWakeVectorBufferHandle = default;
            _wakeBlackBoxHandle = default;
            _floraSwayFieldHandle = default;
            _floraSwayFieldMetaHandle = default;
            _floraSwayFieldBlackBoxHandle = default;
            _floraStiffnessRulesHandle = default;
            _floraStiffnessCsvScratchHandle = default;
            _oceanFlowSamplePositionsHandle = default;
            _oceanFlowSampleResultsHandle = default;
            _parasiteNodesHandle = default;
            _cascadeReactiveTemplateMaskHandle = default;
            _defensiveSporeBurstTemplateMaskHandle = default;
            _allelopathicBloodKelpTemplateMaskHandle = default;
            _allelopathicGhostWeedTemplateMaskHandle = default;
            _surfaceCascadePhaseSeedsHandle = default;
            _underwaterCascadePhaseSeedsHandle = default;
            _surfaceCascadeEventsHandle = default;
            _underwaterCascadeEventsHandle = default;
            _surfaceReactiveFloraHandlesHandle = default;
            _underwaterReactiveFloraHandlesHandle = default;
            _reactiveFloraQueryHandlesHandle = default;
            _floraMemoryTelemetryHandle = default;
            _wakeDataVault = null;

            _parasiteNodeCount = 0;
            _surfaceReactiveFloraHandleCount = 0;
            _underwaterReactiveFloraHandleCount = 0;
            _floraMemoryTelemetryCursor = 0;
            _floraMemoryConsecutiveFailureCount = 0;
            _floraMemoryTelemetryDumped = false;
            ReleaseGraphicsBuffer(ref _surfaceCascadePhaseSeedBuffer);
            ReleaseGraphicsBuffer(ref _underwaterCascadePhaseSeedBuffer);

            _surfaceReactiveFloraHash?.Dispose();
            _surfaceReactiveFloraHash = null;
            _underwaterReactiveFloraHash?.Dispose();
            _underwaterReactiveFloraHash = null;

            if (_interactionBufferA != null)
            {
                _interactionBufferA.Release();
                _interactionBufferA = null;
            }

            if (_interactionBufferB != null)
            {
                _interactionBufferB.Release();
                _interactionBufferB = null;
            }

            _activeInteractionBuffer = null;
            _interactionBufferWriteIndex = 0;

            ReleaseFlowFieldBuffer();
            ReleaseFloraSwayFieldBuffers();
            ReleaseWakeTrailResources();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !IsFiniteVector3(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            CompleteWakeDecayJob(forceComplete: true, dispatcherSwapWindow: false);
            ClearFloraSwayDisplacementField(forceUpload: true);
            ApplyRuntimeOffsetToCachedState(-shiftOffset);
        }

        /// <summary>
        /// Updates published vegetation interaction and environment globals.
        /// </summary>
        /// <param name="deltaTime">Current frame delta.</param>
        public void Tick(float deltaTime)
        {
            RefreshQualityDependentResourcesIfNeeded();
            UpdateSedimentCooldowns(deltaTime);

            Transform runtimePlayerTransform = ResolveRuntimePlayerTransform();
            Vector3 targetPosition = ResolveRuntimePosition(runtimePlayerTransform);
            QueueEnvironmentGlobals(targetPosition);
            QueueSubmarineWashGlobals();
            QueueDamageReactionGlobal(deltaTime);
            QueueFlowFieldRefresh(deltaTime);
            if (runtimePlayerTransform == null)
            {
                QueueProceduralWakeTick(deltaTime);
                ClearDefensiveSporeHazard();
                ResetInteractionGlobals();
                ResetExternalInteractions();
                return;
            }

            ResolvePlayerState(runtimePlayerTransform, targetPosition);
            UpdateDefensiveSporeBursts(targetPosition);
            UpdateBioluminescentCascades(targetPosition, deltaTime);

            Vector3 playerVelocity = UpdatePlayerSpringVelocity(ResolvePlayerVelocity(targetPosition, deltaTime), deltaTime);
            float velocityMagnitude = EstimateLength3D(playerVelocity);
            _lastPublishedPlayerVelocity = playerVelocity;
            _hasActiveScooterWake = false;
            _hasActiveSubmarineWake = false;

            float targetRadius = _baseRadius + velocityMagnitude * _velocityRadiusMultiplier;
            float targetForce = Mathf.Clamp(velocityMagnitude * 0.85f, 0f, _maxInteractionForce);

            float positionBlend = math.saturate(deltaTime * _positionSmoothSpeed);
            _smoothPosition = new Vector3(
                math.lerp(_smoothPosition.x, targetPosition.x, positionBlend),
                math.lerp(_smoothPosition.y, targetPosition.y, positionBlend),
                math.lerp(_smoothPosition.z, targetPosition.z, positionBlend));
            float intensityBlend = math.saturate(deltaTime * _intensitySmoothSpeed);
            _smoothRadius = math.lerp(_smoothRadius, targetRadius, intensityBlend);
            _smoothForce = math.lerp(_smoothForce, targetForce, intensityBlend);

            int interactionCount = 0;
            float playerBendRadius = Mathf.Clamp(
                _playerBendRadius + velocityMagnitude * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            PublishPlayerRuntimePosition(targetPosition, playerBendRadius, velocityMagnitude, targetForce);
            interactionCount = AppendInteractionPoint(_smoothPosition, playerVelocity, playerBendRadius, interactionCount);
            interactionCount = AppendScooterInteractionPoint(playerVelocity, interactionCount, deltaTime);
            interactionCount = CollectDynamicInteractionPoints(targetPosition, interactionCount);
            interactionCount = AppendExternalInteractions(interactionCount);
            QueueWakeTrailTick(targetPosition, playerVelocity, deltaTime);
            PublishPlayerWakeSignal(targetPosition, playerVelocity, velocityMagnitude);
            PublishSubmarineWakeSignal();
            QueueProceduralWakeTick(deltaTime);
            QueueSedimentBurstVisualSync(targetPosition, playerVelocity);

            QueueInteractionVisualSync(
                new Vector4(_smoothPosition.x, _smoothPosition.y, _smoothPosition.z, _smoothRadius),
                _smoothForce,
                _activeInteractionBuffer != null ? interactionCount : 0);
            ResetExternalInteractions();
        }

        private static Vector3 ResolveRuntimePosition(Transform source)
        {
            return source != null ? source.position : Vector3.zero;
        }

        /// <summary>
        /// Commits deferred native parasite simulation results after Burst work has left the hot update lane.
        /// </summary>
        public void LateFrameTick()
        {
            CompleteWakeDecayJob(forceComplete: false, dispatcherSwapWindow: true);
            TryFinalizeFloraSwayFieldJobNoWait(uploadAfterComplete: true);
            CompleteCascadePhaseSeedJob(underwater: false, forceComplete: false, uploadAfterComplete: true);
            CompleteCascadePhaseSeedJob(underwater: true, forceComplete: false, uploadAfterComplete: true);
            FlushQueuedCascadePhaseSeedVisualSync(underwater: false);
            FlushQueuedCascadePhaseSeedVisualSync(underwater: true);
            TryFinalizeHeadlessParasiteSimulation();
            FlushQueuedTickVisualWork();
            FlushWakeTrailResourceRefreshVisualSync();
            FlushWakeTrailTextureClearVisualSync();
            FlushWakeTrailTickVisualSync();
            FlushQueuedVisualGlobals();
            FlushSubmarineWashGlobals();
            FlushDamageReactionGlobal();
            FlushFlowFieldGlobals();
            FlushProceduralWakeBuffer();
            FlushFloraSwayFieldGlobals();
            FlushParasiteInfectionGlobals();
            FlushPlayerRuntimePosition();
            FlushInteractionResetGlobals();
            FlushInteractionVisualSync();
        }

        /// <summary>
        /// Emits persistent plant-competition chemistry and applies chemical-cell suppression.
        /// </summary>
        public void SlowTick()
        {
            RefreshCachedSubmarineContext();
            FlushWakeTrailResourceRefreshSlow();
            ScheduleWakeDecayJob();
            if (_playerReferenceRefreshRequested || _playerTransform == null)
                RefreshPlayerReferenceCacheCold();

            if (_vegetationBridge == null || _vegetationBridgeResolveRequested)
                RefreshVegetationBridgeFromCachedService();

            RefreshModuleParasiteState(FloraSlowTickDeltaSeconds);
            Transform runtimePlayerTransform = ResolveRuntimePlayerTransform();
            if (runtimePlayerTransform != null)
                UpdateToxicSporeExposure(ResolveRuntimePosition(runtimePlayerTransform), FloraSlowTickDeltaSeconds);
            else
                ClearToxicSporeHazard();

            if (_destructibleOrganicManager == null)
                RefreshDestructibleOrganicManagerFromRuntime();

            if (_vegetationBridge == null)
                return;

            RefreshToxicSporeTemplateMask(force: false);
            QueueMatureToxicSporeEventsIfDue(GetCurrentSimulationTimeSeconds());

            if (_destructibleOrganicManager == null)
                return;

            RefreshAllelopathicTemplateMasks(force: false);
            EmitAllelopathicToxinsAndSuppressLane(underwater: true);
            EmitAllelopathicToxinsAndSuppressLane(underwater: false);
        }

        /// <summary>
        /// Queues one external vegetation interaction burst for publication during the next Tick.
        /// </summary>
        public void RegisterExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            if (_externalInteractionPoints == null ||
                _externalInteractionCount >= MaxExternalInteractionPoints ||
                !IsFiniteVector3(positionWS) ||
                !IsFiniteVector3(velocityWS) ||
                !float.IsFinite(radius))
                return;

            float safeRadius = Mathf.Max(0.05f, radius);
            _externalInteractionPoints[_externalInteractionCount++] = new FloraInteractionPointGpuData
            {
                PositionRadius = new Vector4(
                    positionWS.x,
                    positionWS.y,
                    positionWS.z,
                    safeRadius),
                VelocitySpeed = new Vector4(
                    velocityWS.x,
                    velocityWS.y,
                    velocityWS.z,
                    EstimateLength3D(velocityWS))
            };
        }

        /// <summary>
        /// Samples dense underwater flora near a vehicle using the existing spatial hash and returns normalized drag density.
        /// </summary>
        public bool TryResolveKelpPushback(Vector3 positionWS, float radiusMeters, out float density01, out float bendRadiusMeters)
        {
            density01 = 0f;
            bendRadiusMeters = 0f;
            if (_underwaterReactiveFloraHash == null ||
                !IsFiniteVector3(positionWS) ||
                !float.IsFinite(radiusMeters))
                return false;

            if (!TryAcquireReactiveFloraQueryHandles(out NativeArray<int> queryHandles))
                return false;

            try
            {
                float safeRadius = Mathf.Max(0.5f, radiusMeters);
                if (!TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition queryAup))
                    return false;

                int queryCount = _underwaterReactiveFloraHash.CollectSphere(
                    queryAup,
                    safeRadius,
                    ReactiveFloraKindMask,
                    queryHandles);
                if (queryCount <= 0)
                    return false;

                density01 = Mathf.Clamp01(queryCount / Mathf.Max(1f, safeRadius * safeRadius));
                bendRadiusMeters = safeRadius;
                return density01 > 0.001f;
            }
            finally
            {
                ReleaseReactiveFloraQueryHandlesWriteLock();
            }
        }

        private Transform ResolveRuntimePlayerTransform()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform contextTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (contextTransform != null)
                return contextTransform;

            Transform cachedTransform = _playerTransform;
            if (cachedTransform != null)
                return cachedTransform;

            if (_playerTransformOverride != null)
                return _playerTransformOverride;

            _playerReferenceRefreshRequested = true;
            return null;
        }

        private void ResolvePlayerState(Transform runtimePlayerTransform, Vector3 runtimePlayerPosition)
        {
            if (_playerTransform == runtimePlayerTransform)
            {
                RefreshCachedPlayerContracts(runtimePlayerTransform);
                ResolveScooterState();
                return;
            }

            _playerTransform = runtimePlayerTransform;
            RefreshCachedPlayerContracts(runtimePlayerTransform);
            _activeScooterTransform = _scooterTransformOverride;
            _smoothPosition = runtimePlayerPosition;
            _lastPlayerPosition = runtimePlayerPosition;
            _hasLastPlayerPosition = true;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPosition = _activeScooterTransform != null ? _activeScooterTransform.position : runtimePlayerPosition;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = _activeScooterTransform != null;
            ResolveScooterState();
        }

        private void RefreshCachedPlayerContracts(Transform runtimePlayerTransform)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.PlayerTransform == runtimePlayerTransform)
            {
                _playerMovement = playerContext.PlayerMovement;
                _playerToolManager = playerContext.ToolManager;
                return;
            }

            if (runtimePlayerTransform != _playerTransformOverride)
            {
                _playerMovement = null;
                _playerToolManager = null;
            }
        }

        private void RefreshQualityDependentResourcesIfNeeded()
        {
            int qualityMilli = ResolveWakeTrailQualityMilli();
            if (_wakeTrailQualityMilli == qualityMilli)
                return;

            _wakeTrailQualityMilli = qualityMilli;
            int desiredResolution = ResolveWakeTrailResolutionForQualityWeight(qualityMilli * 0.001f);
            if (_wakeTrailRuntimeResolution == desiredResolution)
                return;

            _wakeTrailRuntimeResolution = desiredResolution;
            _wakeTrailResourceRefreshRequested = true;
            QueueWakeTrailGlobals();
        }

        private static int ResolveWakeTrailQualityMilli()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            if (float.IsNaN(quality) || float.IsInfinity(quality))
                quality = 1f;

            return Mathf.RoundToInt(Mathf.Clamp01(quality) * 1000f);
        }

        private static int ResolveWakeTrailResolutionForQualityWeight(float quality01)
        {
            float t = SmoothQuality01(quality01);
            int resolution = Mathf.RoundToInt(Mathf.Lerp(128f, 256f, t) * 0.125f) * 8;
            return Mathf.Clamp(resolution, 128, 256);
        }

        private static float SmoothQuality01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private Vector3 ResolvePlayerVelocity(Vector3 targetPosition, float deltaTime)
        {
            if (CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityFloraInteractionMaxAgeFrames, out Vector3 kccVelocity))
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return HectonPlayerMotor.SafeVelocity(kccVelocity);
            }

            if (!_hasLastPlayerPosition || deltaTime <= 0.0001f)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return Vector3.zero;
            }

            if (!TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
            {
                _lastPlayerPosition = targetPosition;
                return Vector3.zero;
            }

            Vector3 velocity = HectonPlayerMotor.SafeVelocity((targetPosition - _lastPlayerPosition) * inverseDeltaTime);
            _lastPlayerPosition = targetPosition;
            return velocity;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = math.rcp(value);
            return float.IsFinite(reciprocal);
        }

        private int CollectDynamicInteractionPoints(Vector3 targetPosition, int interactionCount)
        {
            return interactionCount;
        }

        private void QueueInteractionVisualSync(Vector4 propWashPositionRadius, float propWashForce, int interactionCount)
        {
            _pendingPropWashPositionRadius = propWashPositionRadius;
            _pendingPropWashForce = propWashForce;
            _pendingVisualInteractionCount = Mathf.Max(0, interactionCount);
            _interactionGlobalsDirty = true;
        }

        private void FlushInteractionVisualSync()
        {
            if (!_interactionGlobalsDirty)
                return;

            Shader.SetGlobalVector(_PropWashPosId, _pendingPropWashPositionRadius);
            Shader.SetGlobalFloat(_PropWashForceId, _pendingPropWashForce);

            int interactionCount = _interactionPoints != null
                ? Mathf.Min(_pendingVisualInteractionCount, _interactionPoints.Length)
                : 0;
            if (interactionCount > 0)
            {
                GraphicsBuffer writeBuffer = ResolveInteractionWriteBuffer();
                if (writeBuffer == null)
                {
                    Shader.SetGlobalInt(_InteractionCountId, 0);
                    _lastPublishedInteractionCount = 0;
                    _interactionGlobalsDirty = false;
                    return;
                }

                GraphicsBufferUploadUtility.UploadArray(writeBuffer, _interactionPoints, interactionCount);
                _activeInteractionBuffer = writeBuffer;
                _interactionBufferWriteIndex ^= 1;
                Shader.SetGlobalBuffer(_InteractionBufferId, _activeInteractionBuffer);
                Shader.SetGlobalInt(_InteractionCountId, interactionCount);
                _lastPublishedInteractionCount = interactionCount;
            }
            else
            {
                Shader.SetGlobalInt(_InteractionCountId, 0);
                _lastPublishedInteractionCount = 0;
            }

            _interactionGlobalsDirty = false;
        }

        private GraphicsBuffer ResolveInteractionWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _interactionBufferWriteIndex == 0
                ? _interactionBufferA
                : _interactionBufferB;
            if (writeBuffer != null)
                return writeBuffer;

            return ReferenceEquals(_activeInteractionBuffer, _interactionBufferA)
                ? _interactionBufferB
                : _interactionBufferA;
        }

        private int AppendScooterInteractionPoint(Vector3 playerVelocity, int interactionCount, float deltaTime)
        {
            ResolveScooterState();

            if (interactionCount >= _maxInteractionPoints)
                return interactionCount;

            bool hasScooterSource = _activeScooterTransform != null;
            Vector3 targetVelocity = hasScooterSource ? playerVelocity * _scooterVelocityMultiplier : Vector3.zero;
            Vector3 smoothedVelocity = SmoothInteractionVector(
                _smoothedScooterVelocity,
                targetVelocity,
                ref _smoothedScooterVelocityDamp,
                deltaTime);
            float speed = EstimateLength3D(smoothedVelocity);
            if (speed <= _interactionReleaseSpeed)
                return interactionCount;

            Vector3 targetScooterPosition = _smoothedScooterPosition;
            if (hasScooterSource)
            {
                targetScooterPosition = _activeScooterTransform.position;
                if (_scooterForwardOffset > 0.0001f)
                    targetScooterPosition += _activeScooterTransform.forward * _scooterForwardOffset;
            }

            if (!_hasSmoothedScooterPosition)
            {
                _smoothedScooterPosition = targetScooterPosition;
                _hasSmoothedScooterPosition = true;
            }

            _smoothedScooterPosition = Vector3.SmoothDamp(
                _smoothedScooterPosition,
                targetScooterPosition,
                ref _smoothedScooterPositionDamp,
                hasScooterSource ? _interactionRiseSmoothTime : _interactionRecoverySmoothTime,
                Mathf.Infinity,
                deltaTime);

            float radius = Mathf.Clamp(
                _scooterBendRadius + speed * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            _hasActiveScooterWake = true;
            _lastPublishedScooterWakePosition = _smoothedScooterPosition;

            return AppendInteractionPoint(_smoothedScooterPosition, smoothedVelocity, radius, interactionCount);
        }

        private Vector3 UpdatePlayerSpringVelocity(Vector3 targetVelocity, float deltaTime)
        {
            _smoothedPlayerVelocity = SmoothInteractionVector(
                _smoothedPlayerVelocity,
                targetVelocity,
                ref _smoothedPlayerVelocityDamp,
                deltaTime);
            return _smoothedPlayerVelocity;
        }

        private Vector3 SmoothInteractionVector(
            Vector3 currentVelocity,
            Vector3 targetVelocity,
            ref Vector3 smoothVelocity,
            float deltaTime)
        {
            if (deltaTime <= 0.0001f)
                return currentVelocity;

            float smoothTime = targetVelocity.sqrMagnitude > 0.0001f
                ? _interactionRiseSmoothTime
                : _interactionRecoverySmoothTime;

            return Vector3.SmoothDamp(
                currentVelocity,
                targetVelocity,
                ref smoothVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private PlayerToolManager ResolvePlayerToolManager(Transform runtimePlayerTransform)
        {
            if (runtimePlayerTransform == null)
                return null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.ToolManager != null)
                return playerContext.ToolManager;

            return runtimePlayerTransform == _playerTransformOverride
                ? _playerToolManager
                : null;
        }

        private void ResolveScooterState()
        {
            _activeScooterTransform = _scooterTransformOverride;

            if (_playerTransform == null)
                return;

            if (_playerToolManager == null)
                _playerToolManager = ResolvePlayerToolManager(_playerTransform);

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return;

            if (_activeScooterTransform == null)
                _activeScooterTransform = scooter.transform;
        }

        private HectonMapMagicVegetationBridge ResolveVegetationBridge()
        {
            if (_vegetationBridgeOverride != null)
                return _vegetationBridgeOverride;

            if (TryGetComponent(out HectonMapMagicVegetationBridge directBridge) && directBridge != null)
                return directBridge;

            HectonMapMagicVegetationBridge childBridge = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<HectonMapMagicVegetationBridge>(transform);
            if (childBridge != null)
                return childBridge;

            return Hecton8.Core.ComponentReferenceUtility.ResolveParentService<HectonMapMagicVegetationBridge>(this);
        }

        private HectonMapMagicVegetationBridge GetCachedVegetationBridgeOrRequestColdResolve()
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridge;
            if (bridge != null && bridge.isActiveAndEnabled)
                return bridge;

            _vegetationBridge = null;
            _vegetationBridgeResolveRequested = true;
            return null;
        }

        private void RefreshVegetationBridgeFromCachedService()
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridgeOverride;
            if (bridge == null || !bridge.isActiveAndEnabled)
            {
                bridge = _vegetationBridge;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref bridge);
            }

            if (bridge == null || !bridge.isActiveAndEnabled)
            {
                _vegetationBridge = null;
                _vegetationBridgeResolveRequested = true;
                return;
            }

            _vegetationBridge = bridge;
            _vegetationBridgeResolveRequested = false;
        }

        private void RefreshVegetationBridgeCold()
        {
            HectonMapMagicVegetationBridge bridge = ResolveVegetationBridge();
            if (bridge == null || !bridge.isActiveAndEnabled)
            {
                bridge = _vegetationBridge;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref bridge);
            }

            if (bridge == null || !bridge.isActiveAndEnabled)
            {
                _vegetationBridge = null;
                _vegetationBridgeResolveRequested = true;
                return;
            }

            _vegetationBridge = bridge;
            _vegetationBridgeResolveRequested = false;
        }

        private DestructibleOrganicManager ResolveDestructibleOrganicManagerCold()
        {
            if (_destructibleOrganicManagerOverride != null && _destructibleOrganicManagerOverride.isActiveAndEnabled)
                return _destructibleOrganicManagerOverride;

            if (TryGetComponent(out DestructibleOrganicManager directManager) &&
                directManager != null &&
                directManager.isActiveAndEnabled)
            {
                return directManager;
            }

            DestructibleOrganicManager activeManager = null;
            WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref activeManager);
            return activeManager;
        }

        private void RefreshDestructibleOrganicManagerFromRuntime()
        {
            if (_destructibleOrganicManagerOverride != null && _destructibleOrganicManagerOverride.isActiveAndEnabled)
            {
                _destructibleOrganicManager = _destructibleOrganicManagerOverride;
                return;
            }

            _destructibleOrganicManager = null;
            WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref _destructibleOrganicManager);
        }

        private void EnsureSedimentParticleSystem()
        {
            if (_sedimentBurstParticleSystem == null)
                _sedimentBurstParticleSystem = ComponentReferenceUtility.ResolveOwnedComponent<ParticleSystem>(transform);

            if (_sedimentBurstParticleSystem == null)
                return;

            ConfigureSedimentParticleSystem(_sedimentBurstParticleSystem);
        }

        private static void ConfigureSedimentParticleSystem(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
                return;

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.58f, 0.64f, 0.56f, 0.34f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = false;

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
            noise.frequency = 0.28f;

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);

            if (particleSystem.TryGetComponent(out ParticleSystemRenderer renderer) && renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Author Missing Sediment Particle System")]
        private void AuthorMissingSedimentParticleSystem()
        {
            if (_sedimentBurstParticleSystem == null)
                _sedimentBurstParticleSystem = ComponentReferenceUtility.ResolveOwnedComponent<ParticleSystem>(transform);

            if (_sedimentBurstParticleSystem == null)
            {
                GameObject sedimentObject = new GameObject("__VegetationSedimentBursts");
                Undo.RegisterCreatedObjectUndo(sedimentObject, "Author Flora Sediment Particle System");
                sedimentObject.transform.SetParent(transform, false);
                _sedimentBurstParticleSystem = sedimentObject.AddComponent<ParticleSystem>();
            }

            ConfigureSedimentParticleSystem(_sedimentBurstParticleSystem);
            EditorUtility.SetDirty(this);
            if (_sedimentBurstParticleSystem != null)
                EditorUtility.SetDirty(_sedimentBurstParticleSystem);
        }
#endif

        private void UpdateSedimentCooldowns(float deltaTime)
        {
            if (_playerSedimentCooldownRemaining > 0f)
            {
                _playerSedimentCooldownRemaining -= deltaTime;
                if (_playerSedimentCooldownRemaining < 0f)
                    _playerSedimentCooldownRemaining = 0f;
            }

            if (_scooterSedimentCooldownRemaining > 0f)
            {
                _scooterSedimentCooldownRemaining -= deltaTime;
                if (_scooterSedimentCooldownRemaining < 0f)
                    _scooterSedimentCooldownRemaining = 0f;
            }
        }

        private void TryEmitSedimentBursts(Vector3 playerPosition, Vector3 playerVelocity)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            float playerSpeed = EstimateLength3D(playerVelocity);
            if (_playerSedimentCooldownRemaining <= 0f && playerSpeed >= _playerSedimentMinSpeed && IsInsideDenseGrassZone(playerPosition))
            {
                EmitSedimentBurst(playerPosition, playerVelocity, false);
                _playerSedimentCooldownRemaining = _playerSedimentCooldown;
            }

            float scooterSpeed = EstimateLength3D(_smoothedScooterVelocity);
            if (_hasActiveScooterWake &&
                _scooterSedimentCooldownRemaining <= 0f &&
                scooterSpeed >= _scooterSedimentMinSpeed &&
                IsInsideDenseGrassZone(_lastPublishedScooterWakePosition))
            {
                EmitSedimentBurst(_lastPublishedScooterWakePosition, _smoothedScooterVelocity, true);
                _scooterSedimentCooldownRemaining = _scooterSedimentCooldown;
            }
        }

        private float ResolveVegetationWaterLevel(IFluidSurfaceCurrentReadModel fluidReadModel)
        {
            float referenceWaterLevel = DefaultVegetationWaterLevel;

            if (_oceanKinematicsProvider != null && TryResolveOceanVegetationWaterLevel(_oceanKinematicsProvider.SeaLevel, out float oceanWaterLevel))
            {
                referenceWaterLevel = oceanWaterLevel;
            }
            else
            {
                MapMagicBridge mapMagicRuntime = _mapMagicRuntime;
                if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicRuntime) &&
                    TryResolveVegetationWaterLevel(mapMagicRuntime.WaterSurfaceLevel, out float mapMagicWaterLevel))
                {
                    _mapMagicRuntime = mapMagicRuntime;
                    referenceWaterLevel = mapMagicWaterLevel;
                }
            }

            if (fluidReadModel != null && TryResolveVegetationWaterLevel(fluidReadModel.WaterLevel, out float fluidWaterLevel))
            {
                if (math.abs(fluidWaterLevel - referenceWaterLevel) <= 128f)
                    return fluidWaterLevel;
            }

            return referenceWaterLevel;
        }

        private static bool TryResolveOceanVegetationWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultVegetationWaterLevel;
            return false;
        }

        private static bool TryResolveVegetationWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultVegetationWaterLevel;
            return false;
        }

        private bool IsInsideDenseGrassZone(Vector3 positionWS)
        {
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            if (vegetationBridge == null || vegetationBridge.ActiveSurfaceInstanceCount < _denseGrassInstanceThreshold)
                return false;

            Bounds surfaceBounds = vegetationBridge.ActiveSurfaceDrawBounds;
            if (surfaceBounds.size.sqrMagnitude <= 0.0001f || !surfaceBounds.Contains(positionWS))
                return false;

            IFluidSurfaceCurrentReadModel fluidReadModel = _fluidReadModel;
            float waterLevel = ResolveVegetationWaterLevel(fluidReadModel);
            return positionWS.y <= waterLevel - 0.25f;
        }

        private void EmitSedimentBurst(Vector3 positionWS, Vector3 velocityWS, bool scooterBurst)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            if (!_sedimentBurstParticleSystem.isPlaying)
                _sedimentBurstParticleSystem.Play(true);

            float speed = EstimateLength3D(velocityWS);
            float burstRadiusScale = Mathf.Clamp(_sedimentBurstRadius * 0.5f, 0.4f, 2f);
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(speed * (scooterBurst ? 0.9f : 0.55f)), 2, _sedimentMaxBurstCount);
            Vector3 planarVelocity = new Vector3(velocityWS.x, 0f, velocityWS.z);
            planarVelocity = NormalizeVector3Fast(planarVelocity, Vector3.forward);

            _sedimentEmitParams.position = positionWS + Vector3.down * 0.18f;
            _sedimentEmitParams.velocity = planarVelocity * Mathf.Min(speed * (0.16f + _sedimentBurstRadius * 0.015f), 3.2f) + Vector3.up * (scooterBurst ? 0.38f : 0.22f);
            float sedimentSpeedEnd = _scooterSedimentMinSpeed * 2f;
            float sedimentSpeed01 = sedimentSpeedEnd > _playerSedimentMinSpeed
                ? math.saturate((speed - _playerSedimentMinSpeed) * math.rcp(sedimentSpeedEnd - _playerSedimentMinSpeed))
                : 0f;
            _sedimentEmitParams.startSize = math.lerp(0.08f, 0.24f, sedimentSpeed01) * burstRadiusScale;
            _sedimentEmitParams.startLifetime = math.lerp(1.0f, 2.0f, sedimentSpeed01);
            _sedimentEmitParams.startColor = scooterBurst
                ? new Color(0.62f, 0.7f, 0.62f, 0.36f)
                : new Color(0.55f, 0.6f, 0.54f, 0.28f);
            _sedimentBurstParticleSystem.Emit(_sedimentEmitParams, burstCount);
        }

        private int AppendInteractionPoint(Vector3 position, Vector3 velocity, float radius, int interactionCount)
        {
            if (_interactionPoints == null || _maxInteractionPoints <= 0)
                return 0;

            if (interactionCount < 0)
                interactionCount = 0;

            int interactionCapacity = Mathf.Min(_maxInteractionPoints, _interactionPoints.Length);
            if (interactionCount >= interactionCapacity)
                return interactionCount;

            _interactionPoints[interactionCount] = new FloraInteractionPointGpuData
            {
                PositionRadius = new Vector4(
                    position.x,
                    position.y,
                    position.z,
                    Mathf.Max(0.05f, radius)),
                VelocitySpeed = new Vector4(
                    velocity.x,
                    velocity.y,
                    velocity.z,
                    EstimateLength3D(velocity))
            };
            return interactionCount + 1;
        }

        private int AppendExternalInteractions(int interactionCount)
        {
            if (_externalInteractionPoints == null || _externalInteractionCount <= 0)
                return interactionCount;

            int copyCount = Mathf.Min(_externalInteractionCount, _externalInteractionPoints.Length);
            for (int i = 0; i < copyCount && interactionCount < _maxInteractionPoints; i++)
            {
                _interactionPoints[interactionCount] = _externalInteractionPoints[i];
                interactionCount++;
            }

            return interactionCount;
        }

        private void ResetExternalInteractions()
        {
            _externalInteractionCount = 0;
        }

        private void QueueEnvironmentGlobals(Vector3 samplePositionWS)
        {
            _pendingEnvironmentSamplePositionWS = samplePositionWS;
            _environmentGlobalsDirty = true;
        }

        private void QueueWakeTrailGlobals()
        {
            _wakeTrailGlobalsDirty = true;
        }

        private void FlushQueuedVisualGlobals()
        {
            if (_environmentGlobalsDirty)
            {
                PublishEnvironmentGlobals(_pendingEnvironmentSamplePositionWS);
                _environmentGlobalsDirty = false;
            }

            if (_wakeTrailGlobalsDirty)
            {
                PublishWakeTrailGlobals();
                _wakeTrailGlobalsDirty = false;
            }
        }

        private void QueueFlowFieldRefresh(float deltaTime)
        {
            _pendingFlowFieldDeltaTime += math.max(0f, deltaTime);
            _flowFieldRefreshRequested = true;
        }

        private void QueueProceduralWakeTick(float deltaTime)
        {
            _pendingProceduralWakeDeltaTime += math.max(0f, deltaTime);
            _proceduralWakeTickRequested = true;
        }

        private void QueueWakeTrailTick(Vector3 playerPosition, Vector3 playerVelocity, float deltaTime)
        {
            _pendingWakeTrailPlayerPositionWS = playerPosition;
            _pendingWakeTrailPlayerVelocityWS = playerVelocity;
            _pendingWakeTrailDeltaTime += math.max(0f, deltaTime);
            _wakeTrailTickRequested = true;
        }

        private void QueueWakeTrailTextureClear()
        {
            _wakeTrailClearRequested = true;
            _wakeTrailEnergy = 0f;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
            QueueWakeTrailGlobals();
        }

        private void QueueSedimentBurstVisualSync(Vector3 playerPosition, Vector3 playerVelocity)
        {
            _pendingSedimentBurstPositionWS = playerPosition;
            _pendingSedimentBurstVelocityWS = playerVelocity;
            _sedimentBurstVisualRequested = true;
        }

        private void FlushQueuedTickVisualWork()
        {
            if (_flowFieldRefreshRequested)
            {
                float deltaTime = _pendingFlowFieldDeltaTime;
                _pendingFlowFieldDeltaTime = 0f;
                _flowFieldRefreshRequested = false;
                RefreshFlowFieldGlobals(deltaTime);
            }

            if (_proceduralWakeTickRequested)
            {
                float deltaTime = _pendingProceduralWakeDeltaTime;
                _pendingProceduralWakeDeltaTime = 0f;
                _proceduralWakeTickRequested = false;
                ProcessProceduralWakeTick(deltaTime);
            }

            if (_sedimentBurstVisualRequested)
            {
                _sedimentBurstVisualRequested = false;
                TryEmitSedimentBursts(_pendingSedimentBurstPositionWS, _pendingSedimentBurstVelocityWS);
            }
        }

        private void FlushWakeTrailResourceRefreshVisualSync()
        {
            if (!_wakeTrailResourceRefreshRequested)
                return;

            QueueWakeTrailGlobals();
        }

        private void FlushWakeTrailResourceRefreshSlow()
        {
            if (!_wakeTrailResourceRefreshRequested)
                return;

            _wakeTrailResourceRefreshRequested = false;
            ReleaseWakeTrailResources();
            CreateWakeTrailResources();
        }

        private void FlushWakeTrailTextureClearVisualSync()
        {
            if (!_wakeTrailClearRequested)
                return;

            _wakeTrailClearRequested = false;
            ClearWakeTrailTextures();
        }

        private void FlushWakeTrailTickVisualSync()
        {
            if (!_wakeTrailTickRequested)
                return;

            Vector3 playerPosition = _pendingWakeTrailPlayerPositionWS;
            Vector3 playerVelocity = _pendingWakeTrailPlayerVelocityWS;
            float deltaTime = _pendingWakeTrailDeltaTime;
            _pendingWakeTrailPlayerPositionWS = Vector3.zero;
            _pendingWakeTrailPlayerVelocityWS = Vector3.zero;
            _pendingWakeTrailDeltaTime = 0f;
            _wakeTrailTickRequested = false;
            UpdateWakeTrail(playerPosition, playerVelocity, deltaTime);
        }

        private void PublishEnvironmentGlobals(Vector3 samplePositionWS)
        {
            RefreshToxicSporeTemplateMask(force: false);
            RefreshCascadeTemplateMask(force: false);
            RefreshDefensiveSporeBurstTemplateMask(force: false);
            HectonUnderwaterVisuals underwaterVisuals = null;
            WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref underwaterVisuals);
            float depth = ResolveVegetationDepthMeters(underwaterVisuals);
            float lightFactor = underwaterVisuals != null ? underwaterVisuals.CurrentLightFactor : 1f;
            float turbidity = underwaterVisuals != null ? underwaterVisuals.CurrentTurbidity : 0f;

            IFluidSurfaceCurrentReadModel fluidReadModel = _fluidReadModel;
            float waterLevel = ResolveVegetationWaterLevel(fluidReadModel);
            Vector3 currentVector = SampleGlobalOceanFlow(samplePositionWS, fluidReadModel);
            _lastVegetationCurrentVector = IsFiniteVector3(currentVector) ? currentVector : Vector3.zero;
            float currentStrength = EstimateLength3D(currentVector);
            float currentNoiseScale = fluidReadModel != null && fluidReadModel.EnablePhantomCurrent ? fluidReadModel.CurrentNoiseScale : 0f;
            float currentTimeScale = fluidReadModel != null && fluidReadModel.EnablePhantomCurrent ? fluidReadModel.CurrentTimeScale : 0f;
            float currentVerticalFactor = fluidReadModel != null && fluidReadModel.EnablePhantomCurrent ? fluidReadModel.CurrentVerticalFactor : 0f;

            Shader.SetGlobalColor(_VegetationFogColorId, RenderSettings.fogColor);
            Shader.SetGlobalColor(_VegetationAmbientColorId, ResolveAmbientColor());
            Shader.SetGlobalFloat(_VegetationDepthId, depth);
            Shader.SetGlobalFloat(_VegetationLightFactorId, lightFactor);
            Shader.SetGlobalFloat(_VegetationTurbidityId, turbidity);
            Shader.SetGlobalFloat(_VegetationWaterLevelId, waterLevel);
            Shader.SetGlobalVector(_GlobalOceanFlowId, new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalVector(
                _VegetationCurrentVectorId,
                new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, currentStrength);
            Shader.SetGlobalFloat(_VegetationCurrentNoiseScaleId, currentNoiseScale);
            Shader.SetGlobalFloat(_VegetationCurrentTimeScaleId, currentTimeScale);
            Shader.SetGlobalFloat(_VegetationCurrentVerticalFactorId, currentVerticalFactor);
            PublishLifecycleGlobals();
            PublishCascadeGlobals();
            PublishPredatorThreatGlobals(samplePositionWS);
        }

        private float ResolveVegetationDepthMeters(HectonUnderwaterVisuals underwaterVisuals)
        {
            if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return math.max(0f, movementState.DepthMeters);

            if (HasPlayerRuntimeContext())
                return 0f;

            if (underwaterVisuals != null && math.isfinite(underwaterVisuals.CurrentDepth))
                return math.max(0f, underwaterVisuals.CurrentDepth);

            HectonPlayerMovement movement = _playerMovement;
            return movement != null && math.isfinite(movement.CurrentDepth)
                ? math.max(0f, movement.CurrentDepth)
                : 0f;
        }

        private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetMovementRuntimeState(out movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(movementState.DepthMeters))
            {
                movementState = default;
                return false;
            }

            return true;
        }

        private bool HasPlayerRuntimeContext()
        {
            return _playerRuntimeContext != null;
        }

        private void PublishLifecycleGlobals()
        {
            float cycleSeconds = Mathf.Max(120f, _seasonCycleSeconds);
            float simulationTime = GetCurrentSimulationTimeSeconds();
            float cyclePhase = Mathf.Repeat(simulationTime / cycleSeconds, 1f);
            int encounterPhaseIndex = ResolveEncounterPhaseIndex();

            float bloomWeight = ResolveLifecycleWindowWeight(cyclePhase, _bloomPhaseCenterNormalized, _bloomPhaseWidthNormalized);
            float decayWeight = ResolveLifecycleWindowWeight(cyclePhase, _decayPhaseCenterNormalized, _decayPhaseWidthNormalized);

            if (encounterPhaseIndex == 1)
                bloomWeight = Mathf.Clamp01(bloomWeight + (_encounterBloomBias * 0.55f));
            else if (encounterPhaseIndex == 2)
                bloomWeight = Mathf.Clamp01(bloomWeight + _encounterBloomBias);

            if (encounterPhaseIndex == 3)
            {
                decayWeight = Mathf.Clamp01(decayWeight + _encounterDecayBias);
                bloomWeight = Mathf.Clamp01(bloomWeight * 0.7f);
            }

            float radiationDecayWeight = Mathf.Clamp01(Shader.GetGlobalFloat(_CelestialRadiationStormId));
            if (radiationDecayWeight > 0.0001f)
                decayWeight = Mathf.Clamp01(decayWeight + radiationDecayWeight * 0.65f);

            Shader.SetGlobalVector(
                _FloraLifecycleParamsId,
                new Vector4(
                    bloomWeight,
                    decayWeight,
                    math.lerp(1f, _bloomEmissionBoost, bloomWeight),
                    _decayWiltStrength));
            Shader.SetGlobalFloat(_SeasonCycleId, cyclePhase);
            Shader.SetGlobalFloat(_SeasonCycleAliasId, cyclePhase);
        }

        private void PublishCascadeGlobals()
        {
            Shader.SetGlobalVector(
                _FloraCascadeParamsId,
                new Vector4(
                    GetCurrentSimulationTimeSeconds(),
                    _cascadePulseDurationSeconds,
                    _cascadeEmissionBoost,
                    _cascadeReleaseDurationSeconds));
        }

        private float GetCurrentSimulationTimeSeconds()
        {
            HectonCelestialEngine celestialEngine = _celestialEngine;
            if (celestialEngine != null)
                return Mathf.Max(0f, celestialEngine.GameTime);

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (IsSaveServiceUsable(saveService))
                return Mathf.Max(0f, saveService.CurrentPlayTimeSeconds);

            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(FloraSimulationClockMaxSeconds, timeSeconds);
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private static int ResolveEncounterPhaseIndex()
        {
            HectonDirectorAI director = null;
            HectonDirectorAI.TryResolveActiveRuntime(ref director);
            return director != null ? director.CurrentPhaseIndex : -1;
        }

        private static float ResolveLifecycleWindowWeight(float cyclePhase, float centerNormalized, float halfWidthNormalized)
        {
            float wrappedDelta = Mathf.Abs(Mathf.DeltaAngle(cyclePhase * 360f, Mathf.Repeat(centerNormalized, 1f) * 360f)) / 360f;
            return math.saturate(1f - wrappedDelta * math.rcp(math.max(0.001f, halfWidthNormalized)));
        }

        private void QueueSubmarineWashGlobals()
        {
            _submarineWashGlobalsDirty = true;
        }

        private void FlushSubmarineWashGlobals()
        {
            if (!_submarineWashGlobalsDirty)
                return;

            _submarineWashGlobalsDirty = false;
            Rigidbody submarineHull = _submarineHullRigidbody;
            if (submarineHull == null)
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            Vector3 velocity = submarineHull.linearVelocity;
            float3 velocityVector = new float3(velocity.x, velocity.y, velocity.z);
            float speedSq = math.lengthsq(velocityVector);
            if (!math.isfinite(speedSq) || speedSq <= 0.000001f)
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            float speed = EstimateLength3D(velocity);
            float submarineMinSpeed = ClampFinite(
                _wakeTrailSubmarineMinSpeed,
                DefaultSubmarineWakeMinSpeed,
                0.25f,
                4f);
            if (speed < submarineMinSpeed)
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            Vector3 worldCenterOfMass = submarineHull.worldCenterOfMass;
            if (!IsFiniteVector3(worldCenterOfMass))
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(worldCenterOfMass, out AbsoluteUniversePosition submarineAup))
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in runtimeOriginAup))
            {
                Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
                Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
                return;
            }

            float3 submarineAupDelta = ResolveAupLocalDelta(in submarineAup, in runtimeOriginAup);
            float3 safeVelocityDirection = velocityVector * math.rsqrt(math.max(speedSq, 0.000001f));
            float submarineRadius = ClampFinite(
                _wakeTrailSubmarineRadius,
                DefaultSubmarineWakeRadius,
                1f,
                6f);
            float normalizedPropwashStrength = math.saturate(
                (speed - submarineMinSpeed) * math.rcp(math.max(submarineRadius, 0.001f)));
            float radius = Mathf.Clamp(
                submarineRadius + speed * 0.16f,
                submarineRadius,
                submarineRadius * 2.1f);
            Shader.SetGlobalVector(
                _SubmarineWashSphereId,
                new Vector4(
                    worldCenterOfMass.x,
                    worldCenterOfMass.y,
                    worldCenterOfMass.z,
                    radius));
            Shader.SetGlobalVector(
                _SubmarineWashVelocityId,
                new Vector4(
                    safeVelocityDirection.x,
                    safeVelocityDirection.y,
                    safeVelocityDirection.z,
                    speed));
            Shader.SetGlobalVector(
                _SubmarinePropwashId,
                new Vector4(
                    -safeVelocityDirection.x,
                    -safeVelocityDirection.y,
                    -safeVelocityDirection.z,
                    normalizedPropwashStrength));
            Shader.SetGlobalVector(
                _SubmarineWashAupGridId,
                new Vector4(
                    submarineAupDelta.x,
                    submarineAupDelta.y,
                    submarineAupDelta.z,
                    AbsoluteUniversePosition.CellSizeMeters));
            Shader.SetGlobalVector(
                _SubmarineWashAupLocalId,
                new Vector4(
                    0f,
                    0f,
                    0f,
                    radius));
        }

        /// <inheritdoc />
        public void EmitWake(in WakeGeneratedSignal signal)
        {
            if (!CompleteWakeDecayJob(forceComplete: false, dispatcherSwapWindow: false))
                return;

            QueueProceduralWake(in signal);
        }

        /// <inheritdoc />
        public void ClearWakeBuffer()
        {
            if (!CompleteWakeDecayJob(forceComplete: false, dispatcherSwapWindow: false))
                return;

            if (TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
            {
                for (int i = 0; i < wakeSources.Length; i++)
                    wakeSources[i] = default;
            }

            if (TryResolveGlobalWakeBuffers(out NativeArray<float4> globalWakes, out NativeArray<float4> globalWakeVectors))
            {
                for (int i = 0; i < globalWakes.Length; i++)
                    globalWakes[i] = default;
                for (int i = 0; i < globalWakeVectors.Length; i++)
                    globalWakeVectors[i] = default;
            }

            if (_proceduralWakeShaderBuffer != null)
            {
                for (int i = 0; i < _proceduralWakeShaderBuffer.Length; i++)
                    _proceduralWakeShaderBuffer[i] = Vector4.zero;
            }

            if (_globalWakeShaderBuffer != null)
            {
                for (int i = 0; i < _globalWakeShaderBuffer.Length; i++)
                    _globalWakeShaderBuffer[i] = Vector4.zero;
            }

            if (_globalWakeVectorShaderBuffer != null)
            {
                for (int i = 0; i < _globalWakeVectorShaderBuffer.Length; i++)
                    _globalWakeVectorShaderBuffer[i] = Vector4.zero;
            }

            _publishedProceduralWakeCount = 0;
            _publishedShearFoamAmount = 0f;
            ClearFloraSwayDisplacementField(forceUpload: true);
            PublishProceduralWakeBuffer(forceUpload: true);
        }

        private void ProcessProceduralWakeTick(float deltaTime)
        {
            if (!CompleteWakeDecayJob(forceComplete: false, dispatcherSwapWindow: false))
                return;

            DrainWakeGeneratedSignals();
            DrainBubbleSpawnSignals();
            UpdateProceduralWakeBuffer(deltaTime);
            UpdateFloraSwayDisplacementField(deltaTime);
            PublishProceduralWakeBuffer();
        }

        private void PublishPlayerWakeSignal(Vector3 playerPosition, Vector3 playerVelocity, float velocityMagnitude)
        {
            if (!IsFiniteVector3(playerVelocity) || velocityMagnitude <= 0.05f)
                return;

            if (!TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                if (HasPlayerRuntimeContext())
                    return;

                HectonPlayerMovement movement = _playerMovement;
                if (movement != null)
                {
                    playerAup = movement.CurrentAup;
                    if (!IsFiniteAup(in playerAup))
                        return;
                }
                else
                {
                    if (!IsFiniteVector3(playerPosition))
                        return;

                    if (!TryResolveAupFromRuntimeOrigin(playerPosition, out playerAup))
                        return;
                }
            }

            PublishWakeGeneratedSignal(playerAup, playerVelocity, WakeSourcePlayer);
        }

        private void PublishSubmarineWakeSignal()
        {
            Rigidbody submarineHull = _submarineHullRigidbody;
            if (submarineHull == null)
                return;

            Vector3 velocity = submarineHull.linearVelocity;
            if (!IsFiniteVector3(velocity))
                return;

            float speed = EstimateLength3D(velocity);
            float submarineMinSpeed = ClampFinite(
                _wakeTrailSubmarineMinSpeed,
                DefaultSubmarineWakeMinSpeed,
                0.25f,
                4f);
            if (speed < submarineMinSpeed)
                return;

            Transform hullTransform = submarineHull.transform;
            Vector3 propellerPosition = submarineHull.worldCenterOfMass;
            float submarineRadius = ClampFinite(
                _wakeTrailSubmarineRadius,
                DefaultSubmarineWakeRadius,
                1f,
                6f);
            if (hullTransform != null)
                propellerPosition -= hullTransform.forward * Mathf.Max(2f, submarineRadius * 0.55f);

            if (!IsFiniteVector3(propellerPosition))
                return;

            if (TryResolveAupFromRuntimeOrigin(propellerPosition, out AbsoluteUniversePosition propellerAup))
                PublishWakeGeneratedSignal(propellerAup, velocity, WakeSourceVehicle);
        }

        private void PublishWakeGeneratedSignal(AbsoluteUniversePosition positionAup, Vector3 velocity, byte sourceKind)
        {
            if (!IsFiniteVector3(velocity) || !IsFiniteAup(in positionAup))
                return;

            WakeGeneratedSignal signal = new WakeGeneratedSignal
            {
                PositionAup = positionAup,
                Velocity = new float3(velocity.x, velocity.y, velocity.z),
                SourceFlags = sourceKind
            };
            SignalBus<WakeGeneratedSignal>.TryPushTracked(in signal, ref s_x001FloraInteractionManagerSignalPushDropCount);
        }

        private void DrainWakeGeneratedSignals()
        {
            ReadOnlySpan<WakeGeneratedSignal> signals = SignalBus<WakeGeneratedSignal>.GetFrameSnapshot();
            int drainCount = math.min(signals.Length, MaxWakeSignalsPerFrame);
            if (signals.Length > MaxWakeSignalsPerFrame)
                _pendingWakeTelemetryFlags |= WakeBlackBoxSignalOverflowFlag;
            for (int i = 0; i < drainCount; i++)
            {
                WakeGeneratedSignal signal = signals[i];
                QueueProceduralWake(in signal);
            }
        }

        private void DrainBubbleSpawnSignals()
        {
            ReadOnlySpan<BubbleSpawnSignal> signals = SignalBus<BubbleSpawnSignal>.GetFrameSnapshot();
            int drainCount = math.min(signals.Length, MaxBubbleWakeSignalsPerFrame);
            if (signals.Length > MaxBubbleWakeSignalsPerFrame)
                _pendingWakeTelemetryFlags |= WakeBlackBoxSignalOverflowFlag;
            for (int i = 0; i < drainCount; i++)
            {
                BubbleSpawnSignal signal = signals[i];
                QueueBubbleWake(in signal);
            }
        }

        private void QueueBubbleWake(in BubbleSpawnSignal signal)
        {
            if (signal.Frame == 0u ||
                !IsFiniteAup(in signal.PositionAup))
            {
                _pendingWakeTelemetryFlags |= WakeBlackBoxBubbleRejectedFlag;
                return;
            }

            float intensity = math.saturate(math.select(0f, signal.Intensity01, math.isfinite(signal.Intensity01)));
            float radius = math.clamp(
                math.select(0f, signal.RadiusMeters, math.isfinite(signal.RadiusMeters)),
                BubbleWakeMinimumRadius,
                BubbleWakeMaximumRadius);
            if (intensity <= WakeMinimumPublishedIntensity || radius <= 0f)
            {
                _pendingWakeTelemetryFlags |= WakeBlackBoxBubbleRejectedFlag;
                return;
            }

            float3 direction = ResolveBubbleWakeDirection(signal.Direction);
            float speed = math.lerp(BubbleWakeMinimumSpeed, BubbleWakeMaximumSpeed, intensity);
            WakeGeneratedSignal wake = new WakeGeneratedSignal
            {
                PositionAup = signal.PositionAup,
                Velocity = direction * speed,
                SourceFlags = (uint)WakeSourceBubble | ((signal.Flags & 0xFFu) << 8)
            };

            QueueProceduralWake(in wake, radius, intensity);
        }

        private static float3 ResolveBubbleWakeDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return math.select(new float3(0f, 1f, 0f), direction * math.rsqrt(math.max(lengthSq, 0.000001f)), math.isfinite(lengthSq) && lengthSq > 0.000001f);
        }

        private void QueueProceduralWake(in WakeGeneratedSignal signal, float radiusOverride = -1f, float intensityOverride = -1f)
        {
            if (!TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
                return;

            float3 velocity = signal.Velocity;
            if (!math.all(math.isfinite(velocity)))
            {
                RecordWakeBlackBox(0, ResolveWakeSlotLimit(), 0f, 0f, float3.zero, float3.zero, WakeBlackBoxInvalidInputFlag | WakeBlackBoxNaNFlag);
                return;
            }

            if (!IsFiniteAup(in signal.PositionAup))
            {
                RecordWakeBlackBox(0, ResolveWakeSlotLimit(), 0f, 0f, float3.zero, velocity, WakeBlackBoxInvalidInputFlag);
                return;
            }

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 position = AUPMath.ResolveCameraRelative(in signal.PositionAup, in runtimeOriginAup);
            if (!math.all(math.isfinite(position)))
            {
                RecordWakeBlackBox(0, ResolveWakeSlotLimit(), 0f, 0f, float3.zero, velocity, WakeBlackBoxInvalidInputFlag | WakeBlackBoxNaNFlag);
                return;
            }

            byte sourceKind = (byte)(signal.SourceFlags & 0xFFu);
            if (sourceKind == 0)
                sourceKind = WakeSourcePlayer;

            Vector3 velocityVector = new Vector3(velocity.x, velocity.y, velocity.z);
            float speed = EstimateLength3D(velocityVector);
            float radius = math.isfinite(radiusOverride) && radiusOverride > 0f
                ? math.clamp(radiusOverride, 0.01f, BubbleWakeMaximumRadius)
                : ResolveWakeRadius(sourceKind, speed);
            float intensity = math.isfinite(intensityOverride) && intensityOverride >= 0f
                ? math.saturate(intensityOverride)
                : ResolveWakeIntensity(sourceKind, speed);
            if (!float.IsFinite(radius) ||
                !float.IsFinite(intensity) ||
                intensity <= WakeMinimumPublishedIntensity ||
                radius <= 0.01f)
                return;

            uint signalFrame = AdvanceProceduralWakeSignalFrame();
            int slotLimit = ResolveWakeSlotLimit();
            int slot = FindProceduralWakeSlot(wakeSources, slotLimit, position, sourceKind, radius);
            WakeSource point = wakeSources[slot];
            float3 wakeDelta = point.PositionWS - position;
            if (point.Active == 0 ||
                math.lengthsq(wakeDelta) > math.max(radius * radius * 4f, 16f))
            {
                point.PositionWS = position;
                point.AgeSeconds = 0f;
            }

            point.TargetWS = position;
            point.PositionAup = signal.PositionAup;
            point.VelocityWS = velocity;
            point.Radius = math.max(point.Radius, radius);
            point.Intensity = math.max(point.Intensity, intensity);
            point.SourceFlags = signal.SourceFlags;
            point.FrameStamp = signalFrame;
            point.SourceKind = sourceKind;
            point.Active = 1;
            wakeSources[slot] = point;

            float impulseBudgetWeight = ResolveWakeBudgetWeight();
            float budgetedIntensity = intensity * math.lerp(0.45f, 1f, SmoothStep01(impulseBudgetWeight));
            if (budgetedIntensity >= WakeFluidImpulseThreshold)
                PublishWakeFluidImpulse(in signal, radius, budgetedIntensity, signalFrame);
        }

        private int FindProceduralWakeSlot(NativeArray<WakeSource> wakeSources, int slotLimit, float3 position, byte sourceKind, float radius)
        {
            int firstInactive = -1;
            int weakestIndex = 0;
            float weakestIntensity = float.MaxValue;
            float mergeRadiusSq = math.max(4f, radius * radius * 0.64f);
            int safeLimit = math.min(math.max(1, slotLimit), wakeSources.Length);

            for (int i = 0; i < safeLimit; i++)
            {
                WakeSource point = wakeSources[i];
                if (point.Active == 0)
                {
                    if (firstInactive < 0)
                        firstInactive = i;
                    continue;
                }

                float3 targetDelta = point.TargetWS - position;
                if (point.SourceKind == sourceKind && math.lengthsq(targetDelta) <= mergeRadiusSq)
                    return i;

                if (point.Intensity < weakestIntensity)
                {
                    weakestIntensity = point.Intensity;
                    weakestIndex = i;
                }
            }

            return firstInactive >= 0 ? firstInactive : weakestIndex;
        }

        private void UpdateProceduralWakeBuffer(float deltaTime)
        {
            if (!TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
                return;

            float safeDeltaTime = float.IsFinite(deltaTime) ? math.clamp(deltaTime, 0f, 0.1f) : 0f;
            float follow = math.saturate(safeDeltaTime * WakeFollowSharpness);
            int slotLimit = ResolveWakeSlotLimit();

            for (int i = 0; i < wakeSources.Length; i++)
            {
                if (i >= slotLimit)
                {
                    wakeSources[i] = default;
                    continue;
                }

                WakeSource point = wakeSources[i];
                if (point.Active == 0)
                    continue;

                point.PositionWS = math.lerp(point.PositionWS, point.TargetWS, follow);

                if (point.Intensity <= WakeMinimumPublishedIntensity ||
                    !float.IsFinite(point.Radius) ||
                    !float.IsFinite(point.Intensity) ||
                    !math.all(math.isfinite(point.PositionWS)) ||
                    !math.all(math.isfinite(point.TargetWS)) ||
                    !math.all(math.isfinite(point.VelocityWS)))
                {
                    RecordWakeBlackBox(0, slotLimit, point.Intensity, point.Radius, point.PositionWS, point.VelocityWS, WakeBlackBoxNaNFlag);
                    point = default;
                }

                wakeSources[i] = point;
            }
        }

        private void ScheduleWakeDecayJob()
        {
            if (_wakeDecayScheduled || !TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
                return;

            double now = Time.unscaledTimeAsDouble;
            double previous = _lastWakeDecayTimestampSeconds;
            _lastWakeDecayTimestampSeconds = now;
            float elapsedSeconds = previous > 0.0
                ? (float)math.clamp(now - previous, 0.0, 0.5)
                : 0.1f;

            _wakeDecayHandle = new WakeDecayJob
            {
                WakeSources = wakeSources,
                DeltaTime = elapsedSeconds,
                DecayRate = WakeDecayPerSecond,
                SlotLimit = ResolveWakeSlotLimit()
            }.Schedule(wakeSources.Length, 8);
            _wakeDecayScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private bool CompleteWakeDecayJob(bool forceComplete, bool dispatcherSwapWindow)
        {
            if (!_wakeDecayScheduled)
                return true;

            bool completed;
            if (forceComplete)
                completed = ForceCompleteWakeDecayJobInPostSimulationWindow();
            else if (dispatcherSwapWindow)
                completed = DispatcherJobSwap.TryComplete(ref _wakeDecayHandle, forceComplete: false);
            else
                completed = DispatcherJobSwap.TryFinalizeCompleted(ref _wakeDecayHandle);

            if (!completed)
                return false;

            _wakeDecayScheduled = false;
            return true;
        }

        private bool ForceCompleteWakeDecayJobInPostSimulationWindow()
        {
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobSwap.TryComplete(ref _wakeDecayHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private void PublishProceduralWakeBuffer(bool forceUpload = false)
        {
            _proceduralWakeForceUpload |= forceUpload;
            _proceduralWakeGlobalsDirty = true;
        }

        private void FlushProceduralWakeBuffer()
        {
            if (!_proceduralWakeGlobalsDirty)
                return;

            bool forceUpload = _proceduralWakeForceUpload;
            _proceduralWakeGlobalsDirty = false;
            _proceduralWakeForceUpload = false;

            if (_proceduralWakeShaderBuffer == null ||
                _globalWakeShaderBuffer == null ||
                _globalWakeVectorShaderBuffer == null)
                return;

            int publishedCount = 0;
            float maxWakeIntensity = 0f;
            float maxWakeRadius = 0f;
            float3 strongestPosition = float3.zero;
            float3 strongestVelocity = float3.zero;
            int slotLimit = ResolveWakeSlotLimit();
            if (TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources) &&
                TryResolveGlobalWakeBuffers(out NativeArray<float4> globalWakes, out NativeArray<float4> globalWakeVectors))
            {
                int safeLimit = math.min(math.min(wakeSources.Length, MaxProceduralWakePoints), slotLimit);
                for (int i = 0; i < safeLimit && publishedCount < MaxProceduralWakePoints; i++)
                {
                    WakeSource point = wakeSources[i];
                    if (point.Active == 0 ||
                        point.Intensity <= WakeMinimumPublishedIntensity ||
                        !float.IsFinite(point.Radius) ||
                        !float.IsFinite(point.Intensity) ||
                        !math.all(math.isfinite(point.PositionWS)))
                        continue;

                    float packedRadiusIntensity = PackWakeRadiusIntensity(point.Radius, point.Intensity);
                    _proceduralWakeShaderBuffer[publishedCount] = new Vector4(
                        point.PositionWS.x,
                        point.PositionWS.y,
                        point.PositionWS.z,
                        packedRadiusIntensity);
                    float3 wakeDirection = ResolveSafeWakeDirection(point.VelocityWS);
                    Vector4 globalWake = new Vector4(
                        point.PositionWS.x,
                        point.PositionWS.y,
                        point.PositionWS.z,
                        point.Intensity);
                    Vector4 globalWakeVector = new Vector4(
                        wakeDirection.x,
                        wakeDirection.y,
                        wakeDirection.z,
                        point.Radius);
                    _globalWakeShaderBuffer[publishedCount] = globalWake;
                    _globalWakeVectorShaderBuffer[publishedCount] = globalWakeVector;
                    if (publishedCount < globalWakes.Length)
                        globalWakes[publishedCount] = new float4(globalWake.x, globalWake.y, globalWake.z, globalWake.w);
                    if (publishedCount < globalWakeVectors.Length)
                        globalWakeVectors[publishedCount] = new float4(globalWakeVector.x, globalWakeVector.y, globalWakeVector.z, globalWakeVector.w);
                    if (point.Intensity > maxWakeIntensity)
                    {
                        strongestPosition = point.PositionWS;
                        strongestVelocity = point.VelocityWS;
                    }
                    maxWakeIntensity = math.max(maxWakeIntensity, point.Intensity);
                    maxWakeRadius = math.max(maxWakeRadius, point.Radius);
                    publishedCount++;
                }

                for (int i = publishedCount; i < globalWakes.Length; i++)
                    globalWakes[i] = default;
                for (int i = publishedCount; i < globalWakeVectors.Length; i++)
                    globalWakeVectors[i] = default;
            }

            float shearFoamAmount = maxWakeIntensity > 0.8f
                ? math.saturate((maxWakeIntensity - 0.8f) * 5f)
                : 0f;

            if (!forceUpload &&
                _proceduralWakeGlobalsInitialized &&
                publishedCount == 0 &&
                _publishedProceduralWakeCount == 0 &&
                _pendingWakeTelemetryFlags == 0u &&
                math.abs(_publishedShearFoamAmount - shearFoamAmount) <= 0.0001f)
            {
                return;
            }

            for (int i = publishedCount; i < _proceduralWakeShaderBuffer.Length; i++)
                _proceduralWakeShaderBuffer[i] = Vector4.zero;
            for (int i = publishedCount; i < _globalWakeShaderBuffer.Length; i++)
                _globalWakeShaderBuffer[i] = Vector4.zero;
            for (int i = publishedCount; i < _globalWakeVectorShaderBuffer.Length; i++)
                _globalWakeVectorShaderBuffer[i] = Vector4.zero;

            Shader.SetGlobalVectorArray(_ProceduralWakeBufferId, _proceduralWakeShaderBuffer);
            Shader.SetGlobalInt(_ProceduralWakeCountId, publishedCount);
            Shader.SetGlobalVector(_ProceduralWakeParamsId, new Vector4(publishedCount, MaxProceduralWakePoints, maxWakeIntensity, shearFoamAmount));
            Shader.SetGlobalVectorArray(_GlobalWakeBufferId, _globalWakeShaderBuffer);
            Shader.SetGlobalVectorArray(_GlobalWakeVectorBufferId, _globalWakeVectorShaderBuffer);
            Shader.SetGlobalVector(_GlobalWakeParamsId, new Vector4(slotLimit, ResolveWakeBudgetPressure01(), publishedCount, maxWakeIntensity));
            Shader.SetGlobalFloat(_ShearFoamAmountId, shearFoamAmount);
            _publishedProceduralWakeCount = publishedCount;
            _publishedShearFoamAmount = shearFoamAmount;
            _proceduralWakeGlobalsInitialized = true;
            RecordWakeBlackBox(
                publishedCount,
                slotLimit,
                maxWakeIntensity,
                maxWakeRadius,
                strongestPosition,
                strongestVelocity,
                ResolveWakeTelemetryFlags());
        }

        private void UpdateFloraSwayDisplacementField(float deltaTime)
        {
            if (_floraSwayFieldBuildScheduled && !_floraSwayFieldBuildHandle.IsCompleted)
                return;

            TryFinalizeFloraSwayFieldJobNoWait(uploadAfterComplete: true);

            float safeDeltaTime = float.IsFinite(deltaTime) ? math.clamp(deltaTime, 0f, 0.1f) : 0f;
            _floraSwayFieldUpdateTimer = math.max(0f, _floraSwayFieldUpdateTimer - safeDeltaTime);

            if (!EnsureFloraSwayFieldResources())
            {
                _floraSwayFieldActive = false;
                _floraSwayFieldResolution = 0;
                _floraSwayFieldNodeCount = 0;
                _floraSwayFieldMaxMagnitude = 0f;
                PublishFloraSwayFieldGlobals(forceUpload: true);
                return;
            }

            float qualityWeight = ResolveFloraSwayGlobalQualityWeight();
            float layoutQualityWeight = ResolveFloraSwayLayoutQualityWeight(qualityWeight);
            int resolution = ResolveFloraSwayResolution(layoutQualityWeight);
            float cellSize = ResolveFloraSwayCellSize(layoutQualityWeight);
            float updateInterval = ResolveFloraSwayUpdateInterval(qualityWeight);
            int slotLimit = ResolveWakeSlotLimit();

            if (!TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
            {
                ClearFloraSwayDisplacementField(forceUpload: true);
                RecordFloraSwayFieldBlackBox(0, 0, resolution, cellSize, qualityWeight, updateInterval, 0f, float3.zero, FloraSwayFieldVaultMissingFlag);
                return;
            }

            uint flags = 0u;
            if (!TryResolveFloraSwayFieldCenter(
                    wakeSources,
                    slotLimit,
                    qualityWeight,
                    cellSize,
                    out AbsoluteUniversePosition fieldCenterAup,
                    out float3 fieldCenter,
                    out int activeWakeCount,
                    out flags))
            {
                ClearFloraSwayDisplacementField(forceUpload: true);
                RecordFloraSwayFieldBlackBox(0, 0, resolution, cellSize, qualityWeight, updateInterval, 0f, float3.zero, flags | FloraSwayFieldEmptyWakeFlag);
                return;
            }

            bool resolutionChanged = resolution != _floraSwayFieldResolution || _floraSwayFieldNodeCount <= 0;
            bool cellSizeChanged = _floraSwayFieldCellSize > 0f && math.abs(cellSize - _floraSwayFieldCellSize) > 0.001f;
            int3 centerShiftCells = _floraSwayFieldCenterAupValid
                ? ResolveFloraSwayCenterShiftCells(in _lastUploadedFloraSwayFieldCenterAup, in fieldCenterAup, cellSize)
                : int3.zero;
            bool centerChanged = !_floraSwayFieldCenterAupValid ||
                                 centerShiftCells.x != 0 ||
                                 centerShiftCells.y != 0 ||
                                 centerShiftCells.z != 0;
            bool wakeStarted = !_floraSwayFieldActive && activeWakeCount > 0;
            bool dueForUpdate = _floraSwayFieldUpdateTimer <= 0f;
            if (!resolutionChanged && !cellSizeChanged && !centerChanged && !wakeStarted && !dueForUpdate)
                return;

            bool resetField = resolutionChanged ||
                              cellSizeChanged ||
                              !_floraSwayFieldCenterAupValid ||
                              RequiresFullFloraSwayFieldReset(centerShiftCells, resolution);
            int3 ringOffset = resetField
                ? int3.zero
                : ResolveFloraSwayRingOffset(_floraSwayFieldRingOffset, centerShiftCells, resolution);
            int3 wrappedShiftCells = resetField ? int3.zero : centerShiftCells;
            uint scheduleFlags = flags;
            if (resetField)
            {
                scheduleFlags |= FloraSwayFieldFullResetFlag;
            }
            else if (centerShiftCells.x != 0 || centerShiftCells.y != 0 || centerShiftCells.z != 0)
            {
                scheduleFlags |= FloraSwayFieldWrappedShiftFlag;
            }

            uint simulationFrame = AdvanceFloraSwaySimulationFrame();
            ScheduleFloraSwayDisplacementField(
                wakeSources,
                slotLimit,
                resolution,
                cellSize,
                qualityWeight,
                updateInterval,
                fieldCenterAup,
                fieldCenter,
                activeWakeCount,
                scheduleFlags,
                simulationFrame,
                safeDeltaTime,
                ringOffset,
                wrappedShiftCells,
                resetField);
        }

        private bool TryResolveFloraSwayFieldCenter(
            NativeArray<WakeSource> wakeSources,
            int slotLimit,
            float qualityWeight,
            float cellSize,
            out AbsoluteUniversePosition fieldCenterAup,
            out float3 fieldCenter,
            out int activeWakeCount,
            out uint flags)
        {
            fieldCenterAup = default;
            fieldCenter = float3.zero;
            activeWakeCount = 0;
            flags = 0u;

            if (!TryResolveFloraSwayAnchorAup(cellSize, out fieldCenterAup, out fieldCenter))
                return false;

            if (!wakeSources.IsCreated || wakeSources.Length == 0)
                return true;

            int sourceLimit = ResolveFloraSwaySourceLimit(slotLimit, qualityWeight);
            int safeLimit = math.min(wakeSources.Length, sourceLimit);
            for (int i = 0; i < safeLimit; i++)
            {
                WakeSource point = wakeSources[i];
                if (point.Active == 0)
                    continue;

                if (!math.all(math.isfinite(point.VelocityWS)) ||
                    !math.isfinite(point.Radius) ||
                    !math.isfinite(point.Intensity))
                {
                    flags |= FloraSwayFieldInvalidInputFlag | FloraSwayFieldNaNFlag;
                    continue;
                }

                if (point.Intensity <= WakeMinimumPublishedIntensity || point.Radius <= 0.01f)
                    continue;

                activeWakeCount++;
            }

            if (activeWakeCount == 0)
                flags |= FloraSwayFieldEmptyWakeFlag;

            return true;
        }

        private bool TryResolveFloraSwayAnchorAup(float cellSize, out AbsoluteUniversePosition fieldCenterAup, out float3 fieldCenter)
        {
            AbsoluteUniversePosition rawAup;
            if (TryResolvePlayerAupSnapshot(out rawAup))
            {
            }
            else if (HasPlayerRuntimeContext())
            {
                fieldCenterAup = AbsoluteUniversePosition.Invalid();
                fieldCenter = float3.zero;
                return false;
            }
            else if (_playerMovement != null)
            {
                rawAup = _playerMovement.CurrentAup;
                if (!IsFiniteAup(in rawAup))
                {
                    fieldCenterAup = AbsoluteUniversePosition.Invalid();
                    fieldCenter = float3.zero;
                    return false;
                }
            }
            else if (IsFiniteVector3(_floraSwayFieldCenterWS))
            {
                if (!TryResolveAupFromRuntimeOrigin(_floraSwayFieldCenterWS, out rawAup))
                {
                    fieldCenterAup = default;
                    fieldCenter = float3.zero;
                    return false;
                }
            }
            else
            {
                fieldCenterAup = default;
                fieldCenter = float3.zero;
                return false;
            }

            fieldCenterAup = QuantizeFloraSwayAup(rawAup, cellSize);
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            fieldCenter = AUPMath.ResolveCameraRelative(in fieldCenterAup, in runtimeOriginAup);
            return math.all(math.isfinite(fieldCenter));
        }

        private bool TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)
        {
            playerAup = AbsoluteUniversePosition.Invalid();

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup;
                return true;
            }

            return false;
        }

        private static AbsoluteUniversePosition QuantizeFloraSwayAup(in AbsoluteUniversePosition rawAup, float cellSize)
        {
            double3 absolute = rawAup.ToAbsoluteDouble3();
            double safeCellSize = math.max(0.125d, (double)(math.isfinite(cellSize) ? cellSize : FloraSwayFieldMaxCellSize));
            double3 quantized = math.floor(absolute / safeCellSize) * safeCellSize;
            return AbsoluteUniversePosition.FromAbsolutePosition(quantized);
        }

        private static int3 ResolveFloraSwayCenterShiftCells(
            in AbsoluteUniversePosition previousCenter,
            in AbsoluteUniversePosition nextCenter,
            float cellSize)
        {
            float safeCellSize = math.max(0.125f, math.isfinite(cellSize) ? cellSize : FloraSwayFieldMaxCellSize);
            float3 delta = ResolveAupLocalDelta(in nextCenter, in previousCenter);
            return new int3(
                ResolveFloraSwayShiftCell(delta.x, safeCellSize),
                ResolveFloraSwayShiftCell(delta.y, safeCellSize),
                ResolveFloraSwayShiftCell(delta.z, safeCellSize));
        }

        private static int ResolveFloraSwayShiftCell(float delta, float cellSize)
        {
            if (!math.isfinite(delta))
                return int.MaxValue;

            float shift = delta / math.max(0.125f, cellSize);
            if (!math.isfinite(shift))
                return int.MaxValue;

            if (shift >= 2147480000f)
                return int.MaxValue;
            if (shift <= -2147480000f)
                return int.MinValue;

            return (int)math.round(shift);
        }

        private static bool RequiresFullFloraSwayFieldReset(int3 centerShiftCells, int resolution)
        {
            int safeResolution = math.max(1, resolution);
            return RequiresFullFloraSwayFieldReset(centerShiftCells.x, safeResolution) ||
                   RequiresFullFloraSwayFieldReset(centerShiftCells.y, safeResolution) ||
                   RequiresFullFloraSwayFieldReset(centerShiftCells.z, safeResolution);
        }

        private static bool RequiresFullFloraSwayFieldReset(int centerShiftCell, int resolution)
        {
            if (centerShiftCell == int.MinValue || centerShiftCell == int.MaxValue)
                return true;

            return math.abs(centerShiftCell) >= math.max(1, resolution);
        }

        private static int3 ResolveFloraSwayRingOffset(int3 currentOffset, int3 centerShiftCells, int resolution)
        {
            int safeResolution = math.max(1, resolution);
            return new int3(
                WrapFloraSwayGridCoordJob(currentOffset.x + centerShiftCells.x, safeResolution),
                WrapFloraSwayGridCoordJob(currentOffset.y + centerShiftCells.y, safeResolution),
                WrapFloraSwayGridCoordJob(currentOffset.z + centerShiftCells.z, safeResolution));
        }

        private void ScheduleFloraSwayDisplacementField(
            NativeArray<WakeSource> wakeSources,
            int slotLimit,
            int resolution,
            float cellSize,
            float qualityWeight,
            float updateInterval,
            AbsoluteUniversePosition fieldCenterAup,
            float3 fieldCenter,
            int activeWakeCount,
            uint flags,
            uint simulationFrame,
            float deltaTime,
            int3 ringOffset,
            int3 centerShiftCells,
            bool resetField)
        {
            if (!TryResolveFloraSwayFieldBuffers(out NativeArray<FloraDisplacementDTO> fieldValues, out NativeArray<float4> fieldMeta) ||
                !EnsureFloraSwayFieldGraphicsBuffers(allowCreate: false))
            {
                RecordFloraSwayFieldBlackBox(activeWakeCount, 0, resolution, cellSize, qualityWeight, updateInterval, 0f, fieldCenter, flags | FloraSwayFieldVaultMissingFlag);
                return;
            }

            int safeResolution = math.clamp(resolution, FloraSwayFieldMinResolution, FloraSwayFieldMaxResolution);
            int nodeCount = math.min(safeResolution * safeResolution * safeResolution, math.min(fieldValues.Length, FloraSwayFieldMaxNodeCount));
            int sourceLimit = ResolveFloraSwaySourceLimit(slotLimit, qualityWeight);
            float qualityCurve = SmoothStep01(qualityWeight);
            float maxDisplacement = math.lerp(0.28f, FloraSwayFieldMaxDisplacementMeters, qualityCurve);
            float3 ambientCurrent = ResolveFloraSwayAmbientCurrent();
            float framePhase = simulationFrame * math.lerp(0.0075f, 0.0275f, qualityCurve);

            JobHandle decayHandle = new DecayFloraForcesJob
            {
                FieldValues = fieldValues,
                NodeCount = nodeCount,
                Resolution = safeResolution,
                RingOffset = ringOffset,
                CenterShiftCells = centerShiftCells,
                DeltaTime = deltaTime,
                DecayRate = _floraSwaySpringDecayRate,
                ResetField = resetField ? (byte)1 : (byte)0
            }.Schedule(nodeCount, FloraSwayFieldJobBatchSize);

            JobHandle accumulateHandle = new AccumulateFloraForcesJob
            {
                WakeSources = wakeSources,
                FieldValues = fieldValues,
                FieldCenterAup = fieldCenterAup,
                AmbientCurrentWS = ambientCurrent,
                Resolution = safeResolution,
                NodeCount = nodeCount,
                SourceLimit = sourceLimit,
                RingOffset = ringOffset,
                CellSize = cellSize,
                QualityWeight = qualityWeight,
                EntityMassMultiplier = _floraSwayEntityMassMultiplier,
                GlobalCurrentInfluence = _floraSwayGlobalCurrentInfluence,
                MaxDisplacement = maxDisplacement,
                FramePhase = framePhase
            }.Schedule(nodeCount, FloraSwayFieldJobBatchSize, decayHandle);

            if (_enableMockDisplacementInjector)
            {
                accumulateHandle = new MockDisplacementInjectorJob
                {
                    FieldValues = fieldValues,
                    Resolution = safeResolution,
                    NodeCount = nodeCount,
                    RingOffset = ringOffset,
                    CellSize = cellSize,
                    QualityWeight = qualityWeight,
                    FramePhase = framePhase
                }.Schedule(nodeCount, FloraSwayFieldJobBatchSize, accumulateHandle);
            }

            _floraSwayFieldBuildHandle = new UploadDisplacementTextureJob
            {
                FieldValues = fieldValues,
                FieldMeta = fieldMeta,
                Resolution = safeResolution,
                NodeCount = nodeCount,
                CellSize = cellSize,
                QualityWeight = qualityWeight,
                UpdateIntervalSeconds = updateInterval,
                ActiveWakeCount = activeWakeCount,
                FieldCenterWS = fieldCenter,
                FrameIndex = (int)math.min(simulationFrame, (uint)int.MaxValue),
                AupShiftSequence = HectonFloatingOrigin.CurrentShiftSequence,
                BlackBoxCursor = _floraSwayFieldBlackBoxCursor,
                Flags = flags
            }.Schedule(accumulateHandle);

            _floraSwayFieldBuildScheduled = true;
            _floraSwayFieldPendingFlags = flags;
            _floraSwayFieldPendingActiveWakeCount = activeWakeCount;
            _floraSwayFieldPendingRingOffset = ringOffset;
            _floraSwayFieldPendingCenterShiftCells = centerShiftCells;
            _floraSwayFieldPendingCenterAup = fieldCenterAup;
            _floraSwayFieldScheduleTimestampSeconds = Time.realtimeSinceStartupAsDouble;
            _floraSwayFieldUpdateTimer = updateInterval;
        }

        private bool TryFinalizeFloraSwayFieldJobNoWait(bool uploadAfterComplete)
        {
            if (!_floraSwayFieldBuildScheduled)
                return true;

            if (!_floraSwayFieldBuildHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _floraSwayFieldBuildHandle))
                return false;

            return FinishFloraSwayFieldJob(uploadAfterComplete);
        }

        private bool CompleteFloraSwayFieldJobForTeardown(bool uploadAfterComplete)
        {
            if (!_floraSwayFieldBuildScheduled)
                return true;

            if (!ForceCompleteFloraSwayFieldBuildInPostSimulationWindow())
                return false;

            return FinishFloraSwayFieldJob(uploadAfterComplete);
        }

        private bool ForceCompleteFloraSwayFieldBuildInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref _floraSwayFieldBuildHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private bool FinishFloraSwayFieldJob(bool uploadAfterComplete)
        {
            _floraSwayFieldBuildScheduled = false;
            double elapsedSeconds = math.max(0d, Time.realtimeSinceStartupAsDouble - _floraSwayFieldScheduleTimestampSeconds);
            _floraSwayFieldLastCpuMicroseconds = (uint)math.clamp(elapsedSeconds * 1000000d, 0d, uint.MaxValue);

            if (_floraSwayFieldDiscardScheduledUpload)
            {
                RecordDiscardedFloraSwayFieldUpload(FloraSwayFieldDiscardedUploadFlag);
                _floraSwayFieldDiscardScheduledUpload = false;
                _floraSwayFieldPendingActiveWakeCount = 0;
                _floraSwayFieldPendingNonZeroCells = 0;
                _floraSwayFieldPendingFlags = 0u;
                _floraSwayFieldPendingRingOffset = int3.zero;
                _floraSwayFieldPendingCenterShiftCells = int3.zero;
                _floraSwayFieldPendingCenterAup = default;
                _floraSwayFieldBuildHandle = default;
                return true;
            }

            if (!uploadAfterComplete)
            {
                RecordDiscardedFloraSwayFieldUpload(FloraSwayFieldDiscardedUploadFlag);
                _floraSwayFieldBuildHandle = default;
                return true;
            }

            if (!TryResolveFloraSwayFieldBuffers(out NativeArray<FloraDisplacementDTO> fieldValues, out NativeArray<float4> fieldMeta) ||
                fieldMeta.Length < FloraSwayFieldMetaVectorCount ||
                !EnsureFloraSwayFieldGraphicsBuffers(allowCreate: false))
            {
                _floraSwayFieldBuildHandle = default;
                return true;
            }

            int safeResolution = math.clamp((int)math.round(fieldMeta[1].x), 0, FloraSwayFieldMaxResolution);
            int nodeCount = math.clamp((int)math.round(fieldMeta[2].x), 0, math.min(fieldValues.Length, FloraSwayFieldMaxNodeCount));
            float maxMagnitude = math.max(0f, fieldMeta[1].w);
            uint flags = (uint)math.max(0, (int)math.round(fieldMeta[3].w));
            _floraSwayFieldPendingNonZeroCells = math.max(0, (int)math.round(fieldMeta[2].w));

            GraphicsBuffer writeBuffer = _floraSwayFieldUseBufferA ? _floraSwayFieldBufferA : _floraSwayFieldBufferB;
            if (writeBuffer == null)
            {
                flags |= FloraSwayFieldVaultMissingFlag | FloraSwayFieldUploadStallFlag;
                RecordFloraSwayFieldBlackBox(_floraSwayFieldPendingActiveWakeCount, _floraSwayFieldPendingNonZeroCells, safeResolution, _floraSwayFieldCellSize, _floraSwayFieldQualityWeight, _floraSwayFieldUpdateIntervalSeconds, maxMagnitude, new float3(_floraSwayFieldCenterWS.x, _floraSwayFieldCenterWS.y, _floraSwayFieldCenterWS.z), flags);
                _floraSwayFieldBuildHandle = default;
                return true;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, fieldValues, nodeCount);
            _floraSwayFieldReadBuffer = writeBuffer;
            _floraSwayFieldUseBufferA = !_floraSwayFieldUseBufferA;
            _floraSwayFieldResolution = safeResolution;
            _floraSwayFieldNodeCount = nodeCount;
            _floraSwayFieldCellSize = math.max(0f, fieldMeta[0].w);
            _floraSwayFieldQualityWeight = math.saturate(fieldMeta[1].z);
            _floraSwayFieldUpdateIntervalSeconds = math.max(0f, fieldMeta[2].y);
            _floraSwayFieldMaxMagnitude = maxMagnitude;
            _floraSwayFieldCenterWS = new Vector3(fieldMeta[0].x, fieldMeta[0].y, fieldMeta[0].z);
            _lastUploadedFloraSwayFieldCenterWS = _floraSwayFieldCenterWS;
            _lastUploadedFloraSwayFieldCenterAup = _floraSwayFieldPendingCenterAup;
            _floraSwayFieldCenterAupValid = true;
            _floraSwayFieldRingOffset = _floraSwayFieldPendingRingOffset;
            _floraSwayFieldLastCenterShiftCells = _floraSwayFieldPendingCenterShiftCells;
            _floraSwayFieldActive = fieldMeta[1].y > 0.0001f;
            PublishFloraSwayFieldGlobals(forceUpload: true);
            RecordFloraSwayFieldBlackBox(
                _floraSwayFieldPendingActiveWakeCount,
                _floraSwayFieldPendingNonZeroCells,
                safeResolution,
                _floraSwayFieldCellSize,
                _floraSwayFieldQualityWeight,
                _floraSwayFieldUpdateIntervalSeconds,
                maxMagnitude,
                new float3(_floraSwayFieldCenterWS.x, _floraSwayFieldCenterWS.y, _floraSwayFieldCenterWS.z),
                flags | _floraSwayFieldPendingFlags);
            _floraSwayFieldBuildHandle = default;
            return true;
        }

        private float3 ResolveFloraSwayAmbientCurrent()
        {
            Vector3 current = _lastVegetationCurrentVector;
            if (!IsFiniteVector3(current))
                current = Vector3.zero;

            return new float3(current.x, current.y, current.z);
        }

        private void RecordDiscardedFloraSwayFieldUpload(uint flags)
        {
            int resolution = _floraSwayFieldResolution;
            int nonZeroCells = _floraSwayFieldPendingNonZeroCells;
            float cellSize = _floraSwayFieldCellSize;
            float qualityWeight = _floraSwayFieldQualityWeight;
            float updateInterval = _floraSwayFieldUpdateIntervalSeconds;
            float maxMagnitude = _floraSwayFieldMaxMagnitude;
            float3 fieldCenter = new float3(_floraSwayFieldCenterWS.x, _floraSwayFieldCenterWS.y, _floraSwayFieldCenterWS.z);
            uint telemetryFlags = flags | _floraSwayFieldPendingFlags;

            if (TryResolveFloraSwayFieldBuffers(out _, out NativeArray<float4> fieldMeta) &&
                fieldMeta.Length >= FloraSwayFieldMetaVectorCount)
            {
                float4 meta0 = fieldMeta[0];
                float4 meta1 = fieldMeta[1];
                float4 meta2 = fieldMeta[2];
                float4 meta3 = fieldMeta[3];
                if (math.all(math.isfinite(meta0)) &&
                    math.all(math.isfinite(meta1)) &&
                    math.all(math.isfinite(meta2)) &&
                    math.all(math.isfinite(meta3)))
                {
                    resolution = math.clamp((int)math.round(meta1.x), 0, FloraSwayFieldMaxResolution);
                    nonZeroCells = math.max(0, (int)math.round(meta2.w));
                    cellSize = math.max(0f, meta0.w);
                    qualityWeight = math.saturate(meta1.z);
                    updateInterval = math.max(0f, meta2.y);
                    maxMagnitude = math.max(0f, meta1.w);
                    fieldCenter = new float3(meta0.x, meta0.y, meta0.z);
                    telemetryFlags |= (uint)math.max(0, (int)math.round(meta3.w));
                }
                else
                {
                    telemetryFlags |= FloraSwayFieldInvalidInputFlag | FloraSwayFieldNaNFlag;
                }
            }
            else
            {
                telemetryFlags |= FloraSwayFieldVaultMissingFlag;
            }

            RecordFloraSwayFieldBlackBox(
                _floraSwayFieldPendingActiveWakeCount,
                nonZeroCells,
                resolution,
                cellSize,
                qualityWeight,
                updateInterval,
                maxMagnitude,
                fieldCenter,
                _floraSwayFieldPendingRingOffset,
                _floraSwayFieldPendingCenterShiftCells,
                telemetryFlags);
        }

        private uint AdvanceFloraSwaySimulationFrame()
        {
            _floraSwaySimulationFrameCounter++;
            if (_floraSwaySimulationFrameCounter == 0u)
                _floraSwaySimulationFrameCounter = 1u;
            return _floraSwaySimulationFrameCounter;
        }

        private uint AdvanceProceduralWakeSignalFrame()
        {
            _proceduralWakeSignalFrameCounter++;
            if (_proceduralWakeSignalFrameCounter == 0u)
                _proceduralWakeSignalFrameCounter = 1u;
            return _proceduralWakeSignalFrameCounter;
        }

#if UNITY_EDITOR
        public static bool ValidateFloraDisplacementDtoLayout(out int size, out int forceOffset, out int decayOffset)
        {
            size = UnsafeUtility.SizeOf<FloraDisplacementDTO>();
            forceOffset = ResolveFieldOffset(typeof(FloraDisplacementDTO), nameof(FloraDisplacementDTO.ForceVector));
            decayOffset = ResolveFieldOffset(typeof(FloraDisplacementDTO), nameof(FloraDisplacementDTO.DecayTimer));
            return size == 16 && forceOffset == 0 && decayOffset == 12;
        }

        public static bool ValidateFloraStiffnessRuleDtoLayout(out int size, out int plantHashOffset, out int flagsOffset)
        {
            size = UnsafeUtility.SizeOf<FloraStiffnessRuleDTO>();
            plantHashOffset = ResolveFieldOffset(typeof(FloraStiffnessRuleDTO), nameof(FloraStiffnessRuleDTO.PlantHash));
            flagsOffset = ResolveFieldOffset(typeof(FloraStiffnessRuleDTO), nameof(FloraStiffnessRuleDTO.Flags));
            return size == 16 &&
                   plantHashOffset == 0 &&
                   flagsOffset == 12;
        }

        public static bool ValidateFloraSwayTelemetryLayout(
            out int size,
            out int fieldCenterOffset,
            out int cpuMicrosecondsOffset,
            out int resolutionOffset)
        {
            size = UnsafeUtility.SizeOf<FloraSwayFieldTelemetryEntry>();
            fieldCenterOffset = ResolveFieldOffset(typeof(FloraSwayFieldTelemetryEntry), nameof(FloraSwayFieldTelemetryEntry.FieldCenterWS));
            cpuMicrosecondsOffset = ResolveFieldOffset(typeof(FloraSwayFieldTelemetryEntry), nameof(FloraSwayFieldTelemetryEntry.CpuMicroseconds));
            resolutionOffset = ResolveFieldOffset(typeof(FloraSwayFieldTelemetryEntry), nameof(FloraSwayFieldTelemetryEntry.Resolution));
            return size == 64 &&
                   fieldCenterOffset == 12 &&
                   cpuMicrosecondsOffset == 56 &&
                   resolutionOffset == 60;
        }

        public static bool ValidateFloraMemoryTelemetryLayout(
            out int size,
            out int bufferIdOffset,
            out int flagsOffset,
            out int cpuMicrosecondsOffset)
        {
            size = UnsafeUtility.SizeOf<FloraMemoryTelemetryEntry>();
            bufferIdOffset = ResolveFieldOffset(typeof(FloraMemoryTelemetryEntry), nameof(FloraMemoryTelemetryEntry.BufferID));
            flagsOffset = ResolveFieldOffset(typeof(FloraMemoryTelemetryEntry), nameof(FloraMemoryTelemetryEntry.Flags));
            cpuMicrosecondsOffset = ResolveFieldOffset(typeof(FloraMemoryTelemetryEntry), nameof(FloraMemoryTelemetryEntry.CpuMicroseconds));
            return size == 64 &&
                   bufferIdOffset == 8 &&
                   flagsOffset == 28 &&
                   cpuMicrosecondsOffset == 56;
        }

        public static bool ValidateParasiteNodeLayout(out int size, out int birthOffset, out int stateOffset)
        {
            size = UnsafeUtility.SizeOf<ParasiteNode>();
            birthOffset = ResolveFieldOffset(typeof(ParasiteNode), nameof(ParasiteNode.BirthTimeSeconds));
            stateOffset = ResolveFieldOffset(typeof(ParasiteNode), nameof(ParasiteNode.State));
            return size == 64 &&
                   birthOffset == 0 &&
                   stateOffset == 60;
        }

        public static bool ValidateFloraCascadeEventPayloadLayout(out int size, out int centerOffset, out int radiusOffset)
        {
            size = UnsafeUtility.SizeOf<FloraCascadeEventPayload>();
            centerOffset = ResolveFieldOffset(typeof(FloraCascadeEventPayload), nameof(FloraCascadeEventPayload.Center));
            radiusOffset = ResolveFieldOffset(typeof(FloraCascadeEventPayload), nameof(FloraCascadeEventPayload.RadiusMeters));
            return size == 32 &&
                   centerOffset == 0 &&
                   radiusOffset == 16;
        }

        public static bool ValidateConsumedWakeSourceLayout(
            out int size,
            out int positionAupOffset,
            out int velocityOffset,
            out int radiusOffset,
            out int sourceKindOffset,
            out int padding4Offset)
        {
            size = UnsafeUtility.SizeOf<WakeSource>();
            positionAupOffset = ResolveFieldOffset(typeof(WakeSource), nameof(WakeSource.PositionAup));
            velocityOffset = ResolveFieldOffset(typeof(WakeSource), nameof(WakeSource.VelocityWS));
            radiusOffset = ResolveFieldOffset(typeof(WakeSource), nameof(WakeSource.Radius));
            sourceKindOffset = ResolveFieldOffset(typeof(WakeSource), nameof(WakeSource.SourceKind));
            padding4Offset = ResolveFieldOffset(typeof(WakeSource), nameof(WakeSource.Padding4));
            return size == 128 &&
                   positionAupOffset == 0 &&
                   velocityOffset == 72 &&
                   radiusOffset == 84 &&
                   sourceKindOffset == 104 &&
                   padding4Offset == 124;
        }

        public static bool ValidateConsumedWakeTelemetryLayout(out int size, out int budgetPressureOffset)
        {
            size = UnsafeUtility.SizeOf<WakeTelemetryEntry>();
            budgetPressureOffset = ResolveFieldOffset(typeof(WakeTelemetryEntry), nameof(WakeTelemetryEntry.BudgetPressure01));
            return size == 64 && budgetPressureOffset == 60;
        }

        private static int ResolveFieldOffset(Type type, string fieldName)
        {
            System.Reflection.FieldInfo field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
#endif

        private void GenerateEmergencyMockStiffness()
        {
            if (!TryResolveFloraStiffnessRules(out NativeArray<FloraStiffnessRuleDTO> rules) || rules.Length == 0)
                return;

            for (int i = 0; i < rules.Length; i++)
                rules[i] = default;

            rules[0] = BuildFloraStiffnessRule(HashAsciiLiteral("flora.grass"), 0.28f, 3.2f);
            if (rules.Length > 1)
                rules[1] = BuildFloraStiffnessRule(HashAsciiLiteral("flora.kelp"), 0.46f, 2.1f);
            if (rules.Length > 2)
                rules[2] = BuildFloraStiffnessRule(HashAsciiLiteral("flora.coral_soft"), 0.72f, 4.8f);
            if (rules.Length > 3)
                rules[3] = BuildFloraStiffnessRule(HashAsciiLiteral("flora.sargassum"), 0.34f, 2.7f);
        }

#if UNITY_EDITOR
        public bool TryReloadFloraStiffnessProfilesCsvForEditor(string path)
        {
            return TryIngestFloraStiffnessProfilesCsv(path);
        }

        private unsafe bool TryIngestFloraStiffnessProfilesCsv(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryResolveFloraStiffnessRules(out NativeArray<FloraStiffnessRuleDTO> rules) ||
                !TryResolveFloraCsvScratch(out NativeArray<byte> scratch) ||
                rules.Length == 0 ||
                scratch.Length == 0)
            {
                return false;
            }

            int byteCount;
            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int safeLength = math.min(scratch.Length, (int)math.min(stream.Length, int.MaxValue));
                void* scratchPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                byteCount = stream.Read(new Span<byte>(scratchPtr, safeLength));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            int cursor = 0;
            int ruleIndex = 0;
            while (cursor < byteCount && ruleIndex < rules.Length)
            {
                SkipCsvLineWhitespace(scratch, byteCount, ref cursor);
                if (cursor >= byteCount)
                    break;

                if (scratch[cursor] == (byte)'#')
                {
                    SkipCsvLine(scratch, byteCount, ref cursor);
                    continue;
                }

                uint hash = ParseCsvFnvHash(scratch, byteCount, ref cursor);
                if (!TryConsumeCsvComma(scratch, byteCount, ref cursor) ||
                    !TryParseCsvFloat(scratch, byteCount, ref cursor, out float stiffness) ||
                    !TryConsumeCsvComma(scratch, byteCount, ref cursor) ||
                    !TryParseCsvFloat(scratch, byteCount, ref cursor, out float damping))
                {
                    SkipCsvLine(scratch, byteCount, ref cursor);
                    continue;
                }

                SkipCsvLine(scratch, byteCount, ref cursor);
                if (hash == 0u || hash == HashAsciiLiteral("name"))
                    continue;

                rules[ruleIndex++] = BuildFloraStiffnessRule(hash, stiffness, damping);
            }

            for (int i = ruleIndex; i < rules.Length; i++)
                rules[i] = default;

            return ruleIndex > 0;
        }

        private bool TryResolveFloraStiffnessRules(out NativeArray<FloraStiffnessRuleDTO> rules)
        {
            rules = default;
            IDataVault dataVault = _wakeDataVault;
            if (dataVault == null)
                return false;

            return TryEnsureFloraVaultBuffer(
                dataVault,
                ref _floraStiffnessRulesHandle,
                FloraStiffnessRulesBufferId,
                FloraStiffnessRuleCapacity,
                NativeArrayOptions.UninitializedMemory,
                out rules);
        }

        private bool TryResolveFloraCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            IDataVault dataVault = _wakeDataVault;
            if (dataVault == null)
                return false;

            return TryEnsureFloraVaultBuffer(
                dataVault,
                ref _floraStiffnessCsvScratchHandle,
                FloraStiffnessCsvScratchBufferId,
                FloraCsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                out scratch);
        }
#endif

        private static FloraStiffnessRuleDTO BuildFloraStiffnessRule(uint plantHash, float stiffness, float damping)
        {
            return new FloraStiffnessRuleDTO
            {
                PlantHash = plantHash,
                Stiffness01 = math.saturate(math.isfinite(stiffness) ? stiffness : 0.5f),
                Damping = math.max(0.01f, math.isfinite(damping) ? damping : 1f),
                Flags = 1u
            };
        }

#if UNITY_EDITOR
        private static uint ParseCsvFnvHash(NativeArray<byte> bytes, int byteCount, ref int cursor)
        {
            uint hash = 2166136261u;
            bool consumed = false;
            while (cursor < byteCount)
            {
                byte value = bytes[cursor];
                if (value == (byte)',' || value == (byte)'\r' || value == (byte)'\n')
                    break;

                if (value > (byte)' ')
                {
                    hash ^= ToLowerAscii(value);
                    hash *= 16777619u;
                    consumed = true;
                }

                cursor++;
            }

            return consumed ? hash : 0u;
        }

        private static bool TryParseCsvFloat(NativeArray<byte> bytes, int byteCount, ref int cursor, out float value)
        {
            value = 0f;
            SkipCsvSpaces(bytes, byteCount, ref cursor);
            bool negative = false;
            if (cursor < byteCount && bytes[cursor] == (byte)'-')
            {
                negative = true;
                cursor++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (cursor < byteCount)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                integer = integer * 10f + (c - (byte)'0');
                hasDigit = true;
                cursor++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor < byteCount && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < byteCount)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction = fraction * 10f + (c - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    cursor++;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + (fraction / math.max(divisor, 1f));
            if (negative)
                value = -value;

            SkipCsvSpaces(bytes, byteCount, ref cursor);
            return math.isfinite(value);
        }

        private static bool TryConsumeCsvComma(NativeArray<byte> bytes, int byteCount, ref int cursor)
        {
            SkipCsvSpaces(bytes, byteCount, ref cursor);
            if (cursor >= byteCount || bytes[cursor] != (byte)',')
                return false;

            cursor++;
            return true;
        }

        private static void SkipCsvLineWhitespace(NativeArray<byte> bytes, int byteCount, ref int cursor)
        {
            while (cursor < byteCount)
            {
                byte value = bytes[cursor];
                if (value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n')
                {
                    cursor++;
                    continue;
                }

                break;
            }
        }

        private static void SkipCsvSpaces(NativeArray<byte> bytes, int byteCount, ref int cursor)
        {
            while (cursor < byteCount && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;
        }

        private static void SkipCsvLine(NativeArray<byte> bytes, int byteCount, ref int cursor)
        {
            while (cursor < byteCount && bytes[cursor] != (byte)'\n')
                cursor++;
            if (cursor < byteCount)
                cursor++;
        }
#endif

        private static uint HashAsciiLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= ToLowerAscii((byte)value[i]);
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }

        private void ClearFloraSwayDisplacementField(bool forceUpload)
        {
            bool metadataWritable = true;
            bool discardScheduledUpload = false;
            if (_floraSwayFieldBuildScheduled)
            {
                if (_floraSwayFieldBuildHandle.IsCompleted)
                {
                    TryFinalizeFloraSwayFieldJobNoWait(uploadAfterComplete: false);
                }
                else
                {
                    _floraSwayFieldDiscardScheduledUpload = true;
                    discardScheduledUpload = true;
                    metadataWritable = false;
                }
            }

            _floraSwayFieldActive = false;
            _floraSwayFieldResolution = 0;
            _floraSwayFieldNodeCount = 0;
            _floraSwayFieldCellSize = 0f;
            _floraSwayFieldMaxMagnitude = 0f;
            _floraSwayFieldUpdateTimer = 0f;
            _floraSwayFieldLayoutQualityWeight = -1f;
            _floraSwayFieldRingOffset = int3.zero;
            _floraSwayFieldLastCenterShiftCells = int3.zero;
            _lastUploadedFloraSwayFieldCenterAup = default;
            _floraSwayFieldCenterAupValid = false;
            if (!discardScheduledUpload)
            {
                _floraSwayFieldPendingRingOffset = int3.zero;
                _floraSwayFieldPendingCenterShiftCells = int3.zero;
                _floraSwayFieldPendingCenterAup = default;
            }

            if (metadataWritable && TryResolveFloraSwayFieldBuffers(out _, out NativeArray<float4> fieldMeta))
            {
                for (int i = 0; i < fieldMeta.Length; i++)
                    fieldMeta[i] = default;
            }

            PublishFloraSwayFieldGlobals(forceUpload);
        }

        private void PublishFloraSwayFieldGlobals(bool forceUpload = false)
        {
            _floraSwayFieldForceUpload |= forceUpload;
            _floraSwayFieldGlobalsDirty = true;
        }

        private void FlushFloraSwayFieldGlobals()
        {
            if (!_floraSwayFieldGlobalsDirty)
                return;

            bool forceUpload = _floraSwayFieldForceUpload;
            _floraSwayFieldGlobalsDirty = false;
            _floraSwayFieldForceUpload = false;

            if (!forceUpload &&
                _floraSwayFieldGlobalsInitialized &&
                !_floraSwayFieldActive &&
                _floraSwayFieldResolution == 0)
            {
                return;
            }

            GraphicsBuffer readBuffer = _floraSwayFieldReadBuffer ?? _floraSwayFieldBufferA;
            if (readBuffer != null)
                Shader.SetGlobalBuffer(_FloraSwayDisplacementFieldId, readBuffer);

            Shader.SetGlobalVector(
                _FloraSwayFieldCenterCellSizeId,
                new Vector4(_floraSwayFieldCenterWS.x, _floraSwayFieldCenterWS.y, _floraSwayFieldCenterWS.z, _floraSwayFieldCellSize));
            Shader.SetGlobalVector(
                _FloraSwayFieldParamsId,
                new Vector4(
                    _floraSwayFieldResolution,
                    _floraSwayFieldActive ? 1f : 0f,
                    _floraSwayFieldQualityWeight,
                    _floraSwayFieldMaxMagnitude));
            Shader.SetGlobalVector(
                _FloraSwayFieldRingOffsetId,
                new Vector4(_floraSwayFieldRingOffset.x, _floraSwayFieldRingOffset.y, _floraSwayFieldRingOffset.z, 0f));
            _floraSwayFieldGlobalsInitialized = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawFloraSwayDebugGizmos ||
                _floraSwayFieldBuildScheduled ||
                _floraSwayFieldResolution <= 1 ||
                _floraSwayFieldNodeCount <= 0 ||
                !TryResolveFloraSwayFieldBuffers(out NativeArray<FloraDisplacementDTO> fieldValues, out _))
            {
                return;
            }

            int resolution = math.clamp(_floraSwayFieldResolution, 1, FloraSwayFieldMaxResolution);
            int stride = math.max(1, resolution / 8);
            float halfIndex = (resolution - 1) * 0.5f;
            Vector3 center = _floraSwayFieldCenterWS;
            float cellSize = math.max(0.05f, _floraSwayFieldCellSize);
            float maxMagnitude = math.max(0.001f, _floraSwayFieldMaxMagnitude);
            for (int z = 0; z < resolution; z += stride)
            {
                for (int y = 0; y < resolution; y += stride)
                {
                    for (int x = 0; x < resolution; x += stride)
                    {
                        int index = ResolveFloraSwayFieldIndexJob(x, y, z, _floraSwayFieldRingOffset, resolution);
                        if ((uint)index >= (uint)fieldValues.Length)
                            continue;

                        float3 force = fieldValues[index].ForceVector;
                        float magnitudeSq = math.lengthsq(force);
                        if (!math.isfinite(magnitudeSq) || magnitudeSq <= 0.000001f)
                            continue;

                        float magnitude = math.sqrt(magnitudeSq);
                        Vector3 start = center + new Vector3(
                            (x - halfIndex) * cellSize,
                            (y - halfIndex) * cellSize,
                            (z - halfIndex) * cellSize);
                        Vector3 end = start + new Vector3(force.x, force.y, force.z);
                        float t = Mathf.Clamp01(magnitude / maxMagnitude);
                        Gizmos.color = Color.Lerp(Color.blue, Color.red, t);
                        Gizmos.DrawLine(start, end);
                    }
                }
            }
        }
#endif

        private float ResolveWakeRadius(byte sourceKind, float speed)
        {
            float safeSpeed = float.IsFinite(speed) ? Mathf.Max(0f, speed) : 0f;
            switch (sourceKind)
            {
                case WakeSourceVehicle:
                {
                    float submarineRadius = ClampFinite(
                        _wakeTrailSubmarineRadius,
                        DefaultSubmarineWakeRadius,
                        1f,
                        6f);
                    return Mathf.Clamp(submarineRadius + safeSpeed * 0.14f, 4f, Mathf.Max(4f, submarineRadius * 2.35f));
                }
                case WakeSourceApexPredator:
                    return Mathf.Clamp(5.5f + safeSpeed * 0.45f, 5.5f, 14f);
                default:
                {
                    float playerRadius = ClampFinite(
                        _playerBendRadius,
                        DefaultPlayerWakeRadius,
                        1.2f,
                        3f);
                    float maxRadius = ClampFinite(
                        _maxBendRadius,
                        DefaultMaxBendRadius,
                        playerRadius,
                        4f);
                    return Mathf.Clamp(playerRadius + safeSpeed * 0.16f, 1.2f, maxRadius);
                }
            }
        }

        private static float ResolveWakeIntensity(byte sourceKind, float speed)
        {
            float safeSpeed = float.IsFinite(speed) ? math.max(0f, speed) : 0f;
            switch (sourceKind)
            {
                case WakeSourceVehicle:
                    return math.saturate(0.62f + safeSpeed * 0.055f);
                case WakeSourceApexPredator:
                    return math.saturate(0.35f + safeSpeed * 0.12f);
                default:
                    return math.saturate(safeSpeed * 0.16f);
            }
        }

        private static float PackWakeRadiusIntensity(float radius, float intensity)
        {
            float safeRadius = float.IsFinite(radius) ? math.clamp(radius, 0f, 1023.9375f) : 0f;
            float safeIntensity = float.IsFinite(intensity) ? math.saturate(intensity) : 0f;
            int radiusQuantized = Mathf.Clamp(Mathf.RoundToInt(safeRadius * 16f), 0, 16383);
            int intensityQuantized = Mathf.Clamp(Mathf.RoundToInt(safeIntensity * 1023f), 0, 1023);
            return radiusQuantized * 1024f + intensityQuantized;
        }

        private static float3 ResolveSafeWakeDirection(float3 velocity)
        {
            float lengthSq = math.lengthsq(velocity);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return float3.zero;

            return velocity * math.rsqrt(math.max(lengthSq, 0.000001f));
        }

        private int ResolveWakeSlotLimit()
        {
            float budgetWeight = ResolveWakeBudgetWeight();
            int slotLimit = Mathf.RoundToInt(math.lerp(
                MinProceduralWakeSlotLimit,
                MaxProceduralWakePoints,
                SmoothStep01(budgetWeight)));
            return math.clamp(slotLimit, MinProceduralWakeSlotLimit, MaxProceduralWakePoints);
        }

        private static float ResolveWakeBudgetWeight()
        {
            float qualityWeight = ResolveFloraSwayGlobalQualityWeight();
            float stress01 = float.IsFinite(HomeostasisBrain.SystemHealthIndex01)
                ? math.saturate(HomeostasisBrain.SystemHealthIndex01)
                : 0f;
            float stressShed = math.lerp(1f, 0.35f, SmoothStep01(stress01));
            return math.saturate(qualityWeight * stressShed);
        }

        private static float ResolveWakeBudgetPressure01()
        {
            return math.saturate(1f - SmoothStep01(ResolveWakeBudgetWeight()));
        }

        private static float ResolveFloraSwayGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!float.IsFinite(weight))
                weight = 0.5f;

            float stress01 = float.IsFinite(HomeostasisBrain.SystemHealthIndex01)
                ? math.saturate(HomeostasisBrain.SystemHealthIndex01)
                : 0f;
            float stressRelief = math.lerp(1f, 0.72f, SmoothStep01(stress01));
            return math.saturate(weight * stressRelief);
        }

        private static int ResolveFloraSwayResolution(float qualityWeight)
        {
            float q = SmoothStep01(qualityWeight);
            return math.clamp(
                FloraSwayFieldMinResolution + Mathf.RoundToInt((FloraSwayFieldMaxResolution - FloraSwayFieldMinResolution) * q),
                FloraSwayFieldMinResolution,
                FloraSwayFieldMaxResolution);
        }

        private float ResolveFloraSwayLayoutQualityWeight(float qualityWeight)
        {
            float target = math.saturate(float.IsFinite(qualityWeight) ? qualityWeight : 0f);
            if (!float.IsFinite(_floraSwayFieldLayoutQualityWeight) || _floraSwayFieldLayoutQualityWeight < 0f)
            {
                _floraSwayFieldLayoutQualityWeight = target;
                return target;
            }

            float current = math.saturate(_floraSwayFieldLayoutQualityWeight);
            float delta = math.abs(target - current);
            float commit = math.step(FloraSwayFieldLayoutQualityHysteresis, delta);
            _floraSwayFieldLayoutQualityWeight = math.lerp(current, target, commit);
            return _floraSwayFieldLayoutQualityWeight;
        }

        private static int ResolveFloraSwaySourceLimit(int slotLimit, float qualityWeight)
        {
            int safeSlotLimit = math.clamp(slotLimit, 1, MaxProceduralWakePoints);
            float q = SmoothStep01(qualityWeight);
            return math.clamp(1 + Mathf.RoundToInt((safeSlotLimit - 1) * q), 1, safeSlotLimit);
        }

        private static float ResolveFloraSwayCellSize(float qualityWeight)
        {
            return math.lerp(FloraSwayFieldMaxCellSize, FloraSwayFieldMinCellSize, SmoothStep01(qualityWeight));
        }

        private static float ResolveFloraSwayUpdateInterval(float qualityWeight)
        {
            return math.lerp(FloraSwayFieldMaxUpdateIntervalSeconds, FloraSwayFieldMinUpdateIntervalSeconds, SmoothStep01(qualityWeight));
        }

        private static float ResolveFloraSwaySourceGain(byte sourceKind)
        {
            switch (sourceKind)
            {
                case WakeSourceVehicle:
                    return 1.22f;
                case WakeSourceApexPredator:
                    return 1.05f;
                default:
                    return 0.72f;
            }
        }

        private uint ResolveWakeTelemetryFlags()
        {
            uint flags = _pendingWakeTelemetryFlags;
            _pendingWakeTelemetryFlags = 0u;
            float budgetPressure01 = ResolveWakeBudgetPressure01();
            float stress01 = float.IsFinite(HomeostasisBrain.SystemHealthIndex01)
                ? math.saturate(HomeostasisBrain.SystemHealthIndex01)
                : 0f;
            if (budgetPressure01 >= 0.5f)
                flags |= WakeBlackBoxBudgetPressureFlag;
            if (stress01 >= 0.8f)
                flags |= WakeBlackBoxThermalPressureFlag;
            return flags;
        }

        private static void PublishWakeFluidImpulse(in WakeGeneratedSignal signal, float radius, float intensity, uint signalFrame)
        {
            if (!math.all(math.isfinite(signal.Velocity)) ||
                !math.isfinite(radius) ||
                !math.isfinite(intensity))
                return;

            FluidImpulseSignal impulse = new FluidImpulseSignal
            {
                PositionAup = signal.PositionAup,
                Vector = signal.Velocity,
                Radius = math.max(0.5f, radius),
                Lifetime = math.lerp(0.35f, 1.4f, math.saturate(intensity)),
                Frame = signalFrame,
                SourceHash = WakeFluidImpulseSourceHash,
                Flags = signal.SourceFlags
            };
            SignalBus<FluidImpulseSignal>.TryPushTracked(in impulse, ref s_x001FloraInteractionManagerSignalPushDropCount);
        }

        private void RecordWakeBlackBox(
            int activeCount,
            int slotLimit,
            float maxIntensity,
            float maxRadius,
            float3 strongestPosition,
            float3 strongestVelocity,
            uint flags)
        {
            if (!TryResolveWakeBlackBox(out NativeArray<WakeTelemetryEntry> wakeBlackBox) || wakeBlackBox.Length == 0)
                return;

            int cursor = _wakeBlackBoxCursor;
            if ((uint)cursor >= (uint)wakeBlackBox.Length)
                cursor = 0;

            uint stateHash = 2166136261u;
            stateHash = MixWakeHash(stateHash, (uint)math.max(0, activeCount));
            stateHash = MixWakeHash(stateHash, (uint)math.max(0, slotLimit));
            stateHash = MixWakeHash(stateHash, (uint)math.asint(maxIntensity));
            stateHash = MixWakeHash(stateHash, (uint)math.asint(maxRadius));

            WakeTelemetryEntry entry = default;
            entry.Frame = _proceduralWakeSignalFrameCounter;
            entry.ActiveWakeSourcesCount = (ushort)math.clamp(activeCount, 0, ushort.MaxValue);
            entry.SlotLimit = (ushort)math.clamp(slotLimit, 0, ushort.MaxValue);
            entry.StrongestWakePositionWS = strongestPosition;
            entry.StrongestIntensity = maxIntensity;
            entry.StrongestVelocityWS = strongestVelocity;
            entry.MaxRadius = maxRadius;
            entry.Flags = flags;
            entry.StateHash = stateHash;
            entry.DataVaultGeneration = _proceduralWakePointsHandle.Generation;
            entry.AupShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            entry.SystemStress01 = HomeostasisBrain.SystemHealthIndex01;
            entry.BudgetPressure01 = ResolveWakeBudgetPressure01();
            wakeBlackBox[cursor] = entry;

            cursor++;
            if (cursor >= wakeBlackBox.Length)
                cursor = 0;
            _wakeBlackBoxCursor = cursor;

            if ((flags & (WakeBlackBoxInvalidInputFlag | WakeBlackBoxNaNFlag)) != 0u)
                DumpWakeBlackBoxOnce();
        }

        private static uint MixWakeHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private unsafe void DumpWakeBlackBoxOnce()
        {
            if (_wakeBlackBoxDumped ||
                !TryResolveWakeBlackBox(out NativeArray<WakeTelemetryEntry> wakeBlackBox) ||
                !wakeBlackBox.IsCreated)
                return;

            NativeArray<byte> payload = default;
            try
            {
                const string path = "Docs/AgentLogs/Dump_INTERACTIVE_WAKE_VFX.bin";
                int headerBytes = 12;
                int rowBytes = UnsafeUtility.SizeOf<WakeTelemetryEntry>();
                int totalBytes = headerBytes + wakeBlackBox.Length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(FloraInteractionManager),
                    "InteractiveWakeTelemetryDumpPayload");
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteUInt32LittleEndian(bytes, 0, 0x57414B45u);
                WriteInt32LittleEndian(bytes, 4, wakeBlackBox.Length);
                WriteInt32LittleEndian(bytes, 8, _wakeBlackBoxCursor);
                int writeOffset = headerBytes;
                for (int i = 0; i < wakeBlackBox.Length; i++)
                {
                    WakeTelemetryEntry entry = wakeBlackBox[i];
                    WriteWakeTelemetryEntry(bytes.Slice(writeOffset, rowBytes), in entry);
                    writeOffset += rowBytes;
                }

                _wakeBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (UnauthorizedAccessException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FloraInteractionManager),
                    "InteractiveWakeTelemetryDumpPayload");
            }
        }

        private void RecordFloraSwayFieldBlackBox(
            int activeWakeCount,
            int nonZeroCells,
            int resolution,
            float cellSize,
            float qualityWeight,
            float updateInterval,
            float maxMagnitude,
            float3 fieldCenter,
            uint flags)
        {
            RecordFloraSwayFieldBlackBox(
                activeWakeCount,
                nonZeroCells,
                resolution,
                cellSize,
                qualityWeight,
                updateInterval,
                maxMagnitude,
                fieldCenter,
                _floraSwayFieldRingOffset,
                _floraSwayFieldLastCenterShiftCells,
                flags);
        }

        private void RecordFloraSwayFieldBlackBox(
            int activeWakeCount,
            int nonZeroCells,
            int resolution,
            float cellSize,
            float qualityWeight,
            float updateInterval,
            float maxMagnitude,
            float3 fieldCenter,
            int3 ringOffset,
            int3 centerShiftCells,
            uint flags)
        {
            if (!TryResolveFloraSwayFieldBlackBox(out NativeArray<FloraSwayFieldTelemetryEntry> blackBox) || blackBox.Length == 0)
                return;

            int cursor = _floraSwayFieldBlackBoxCursor;
            if ((uint)cursor >= (uint)blackBox.Length)
                cursor = 0;

            uint stateHash = 2166136261u;
            stateHash = MixWakeHash(stateHash, (uint)math.max(0, activeWakeCount));
            stateHash = MixWakeHash(stateHash, (uint)math.max(0, nonZeroCells));
            stateHash = MixWakeHash(stateHash, (uint)math.max(0, resolution));
            stateHash = MixWakeHash(stateHash, (uint)math.asint(cellSize));
            stateHash = MixWakeHash(stateHash, (uint)math.asint(qualityWeight));
            stateHash = MixWakeHash(stateHash, (uint)math.asint(maxMagnitude));
            stateHash = MixWakeHash(stateHash, flags);
            stateHash = MixWakeHash(stateHash, unchecked((uint)ringOffset.x));
            stateHash = MixWakeHash(stateHash, unchecked((uint)ringOffset.y));
            stateHash = MixWakeHash(stateHash, unchecked((uint)ringOffset.z));
            stateHash = MixWakeHash(stateHash, unchecked((uint)centerShiftCells.x));
            stateHash = MixWakeHash(stateHash, unchecked((uint)centerShiftCells.y));
            stateHash = MixWakeHash(stateHash, unchecked((uint)centerShiftCells.z));

            FloraSwayFieldTelemetryEntry entry = default;
            entry.Frame = _floraSwaySimulationFrameCounter;
            entry.Resolution = (ushort)math.clamp(resolution, 0, ushort.MaxValue);
            entry.ActiveWakeSourcesCount = (ushort)math.clamp(activeWakeCount, 0, ushort.MaxValue);
            entry.NonZeroCellsCount = (uint)math.max(0, nonZeroCells);
            entry.FieldCenterWS = fieldCenter;
            entry.CellSize = cellSize;
            entry.MaxMagnitude = maxMagnitude;
            entry.GlobalQualityWeight = qualityWeight;
            entry.UpdateIntervalSeconds = updateInterval;
            entry.SystemStress01 = HomeostasisBrain.SystemHealthIndex01;
            entry.Flags = flags;
            entry.StateHash = stateHash;
            entry.DataVaultGeneration = _floraSwayFieldHandle.Generation;
            entry.AupShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            entry.CpuMicroseconds = _floraSwayFieldLastCpuMicroseconds;
            blackBox[cursor] = entry;

            cursor++;
            if (cursor >= blackBox.Length)
                cursor = 0;
            _floraSwayFieldBlackBoxCursor = cursor;

            if ((flags & FloraSwayFieldNaNFlag) != 0u)
            {
                RecordFloraMemoryTelemetry(
                    FloraMemoryTelemetryEventNaN,
                    FloraSwayFieldBlackBoxBufferId,
                    _floraSwayFieldBlackBoxHandle.Generation,
                    FloraSwayFieldBlackBoxCapacity,
                    blackBox.Length,
                    FloraMemoryTelemetryNaNFlag);
            }

            if ((flags & (FloraSwayFieldInvalidInputFlag | FloraSwayFieldNaNFlag | FloraSwayFieldUploadStallFlag)) != 0u)
                DumpFloraSwayFieldBlackBoxOnce();
        }

        private unsafe void DumpFloraSwayFieldBlackBoxOnce()
        {
            if (_floraSwayFieldBlackBoxDumped ||
                !TryResolveFloraSwayFieldBlackBox(out NativeArray<FloraSwayFieldTelemetryEntry> blackBox) ||
                !blackBox.IsCreated)
                return;

            NativeArray<byte> payload = default;
            try
            {
                const string path = "Docs/AgentLogs/Dump_FLORA_SWAY_DIRECTOR.bin";
                int headerBytes = 12;
                int rowBytes = UnsafeUtility.SizeOf<FloraSwayFieldTelemetryEntry>();
                int totalBytes = headerBytes + blackBox.Length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(FloraInteractionManager),
                    "FloraSwayFieldTelemetryDumpPayload");
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteUInt32LittleEndian(bytes, 0, FloraSwayFieldSourceHash);
                WriteInt32LittleEndian(bytes, 4, blackBox.Length);
                WriteInt32LittleEndian(bytes, 8, _floraSwayFieldBlackBoxCursor);
                int writeOffset = headerBytes;
                for (int i = 0; i < blackBox.Length; i++)
                {
                    FloraSwayFieldTelemetryEntry entry = blackBox[i];
                    WriteFloraSwayFieldTelemetryEntry(bytes.Slice(writeOffset, rowBytes), in entry);
                    writeOffset += rowBytes;
                }

                _floraSwayFieldBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (UnauthorizedAccessException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FloraInteractionManager),
                    "FloraSwayFieldTelemetryDumpPayload");
            }
        }

        private void UpdateToxicSporeExposure(Vector3 playerPositionWS, float deltaTime)
        {
            if (_playerMovement == null || _toxicSporeStableHashId == 0)
            {
                ClearToxicSporeHazard();
                return;
            }

            if (_lastToxicSporeExposure01 > 0f)
                _playerMovement.ApplyEnvironmentalDrag(math.lerp(1f, _toxicSporeDragMultiplier, math.saturate(_lastToxicSporeExposure01)));

            _toxicSporeScanTimer -= deltaTime;
            if (_toxicSporeScanTimer > 0f)
                return;

            _toxicSporeScanTimer = _toxicSporeScanIntervalSeconds;

            float exposure01 = 0f;
            Vector3 hazardPosition = Vector3.zero;
            int hazardTemplateIndex = -1;
            int hazardPayloadIndex = -1;
            float hazardAge01 = 1f;
            bool hazardUnderwater = true;
            float toxicSporeDetectionRadius = ClampFinite(_toxicSporeDetectionRadius, 4.5f, 1f, 8f);
            TryResolveNearestToxicSporeEmitter(
                playerPositionWS,
                toxicSporeDetectionRadius,
                ref exposure01,
                ref hazardPosition,
                ref hazardTemplateIndex,
                ref hazardPayloadIndex,
                ref hazardAge01,
                underwater: true);

            if (exposure01 <= 0f)
            {
                hazardUnderwater = false;
                TryResolveNearestToxicSporeEmitter(
                    playerPositionWS,
                    toxicSporeDetectionRadius,
                    ref exposure01,
                    ref hazardPosition,
                    ref hazardTemplateIndex,
                    ref hazardPayloadIndex,
                    ref hazardAge01,
                    underwater: false);
            }

            if (exposure01 <= 0f)
            {
                ClearToxicSporeHazard();
                return;
            }

            _lastToxicSporeExposure01 = exposure01;
            TryApplyToxicSporePoisonStatus(hazardPosition, exposure01);
            HectonHazardManager.Register(
                ToxicSporeHazardSourceId,
                hazardPosition,
                _toxicSporeHazardIntensity * exposure01,
                _toxicSporeHazardRadius,
                HazardType.Toxicity,
                _toxicSporeVisorGlitchBias);
            HectonFloraSporeEvents.EnqueueMatureToxicSpore(
                hazardPosition,
                _toxicSporeHazardRadius,
                exposure01,
                hazardAge01,
                hazardTemplateIndex,
                hazardPayloadIndex,
                hazardUnderwater);
        }

        private void TryApplyToxicSporePoisonStatus(Vector3 hazardPositionWS, float exposure01)
        {
            if (exposure01 < ToxicSporePoisonMinimumExposure)
                return;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;

            Transform playerTransform = _playerTransform;
            if (playerTransform == null && playerContext != null)
                playerTransform = playerContext.PlayerTransform;
            if (playerTransform == null && playerHealth != null)
                playerTransform = playerHealth.transform;
            if (playerTransform == null)
                return;

            int targetId = playerHealth != null ? CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject) : 0;
            float3 playerPositionWS = (float3)playerTransform.position;
            uint signalEntityId = ResolveToxicSporeSignalEntityId(playerHealth);
            PublishToxicSporeToxicityExposure(signalEntityId, playerPositionWS, exposure01);

            if (playerHealth == null || targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Poisoned64,
                ToxicSporePoisonDurationSeconds * Mathf.Clamp01(exposure01),
                ToxicSporeHazardSourceId,
                Mathf.Clamp01(exposure01));
        }

        private uint ResolveToxicSporeSignalEntityId(HectonPlayerHealth playerHealth)
        {
            GameObject playerObject = playerHealth != null ? playerHealth.gameObject : null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerObject == null && playerContext != null)
                playerObject = playerContext.PlayerObject;
            if (playerObject == null && _playerTransform != null)
                playerObject = _playerTransform.gameObject;
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;

            uint entityHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;
        }

        private void PublishToxicSporeToxicityExposure(uint signalEntityId, Vector3 playerPositionWS, float exposure01)
        {
            float exposure = float.IsFinite(exposure01) ? math.saturate(exposure01) : 0f;
            if (signalEntityId == 0u || exposure <= 0.0001f)
                return;

            bool hasSourceAup = TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup);

            ToxicityExposureSignal signal = default;
            signal.Exposure01 = exposure;
            signal.ToxemiaDelta = math.saturate(exposure * ToxicSporeToxemiaDeltaScale);
            signal.EntityId = signalEntityId;
            signal.ChemicalHash = ToxicSporeChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            if (hasSourceAup)
            {
                signal.AUP = playerAup.ToAbsoluteDouble3();
                signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;
            }

            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001FloraInteractionManagerSignalPushDropCount);
        }

        private bool TryResolveToxicSporePlayerAup(Vector3 playerPositionWS, out AbsoluteUniversePosition playerAup)
        {
            if (TryResolvePlayerAupSnapshot(out playerAup))
                return true;

            if (HasPlayerRuntimeContext())
            {
                playerAup = AbsoluteUniversePosition.Invalid();
                return false;
            }

            HectonPlayerMovement movement = _playerMovement;
            if (movement != null)
            {
                playerAup = movement.CurrentAup;
                if (IsFiniteAup(in playerAup))
                    return true;
            }

            return TryResolveAupFromRuntimeOrigin(playerPositionWS, out playerAup);
        }

        private void TryResolveNearestToxicSporeEmitter(
            Vector3 playerPositionWS,
            float detectionRadius,
            ref float bestExposure01,
            ref Vector3 bestPositionWS,
            ref int bestTemplateIndex,
            ref int bestPayloadIndex,
            ref float bestAge01,
            bool underwater)
        {
            if (_vegetationBridge == null ||
                !IsFiniteVector3(playerPositionWS) ||
                !float.IsFinite(detectionRadius) ||
                detectionRadius <= 0f)
            {
                return;
            }

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            float detectionRadiusSq = detectionRadius * detectionRadius;
            float invDetectionRadiusSq = math.rcp(math.max(detectionRadiusSq, 0.0001f));
            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    instanceData.HeightScale <= 0.0001f)
                {
                    continue;
                }

                if (!IsMatureFloraGrowth(in instanceData, out float age01))
                    continue;

                int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
                if (!IsToxicSporeEmitter(in instanceData, templateIndex))
                    continue;

                Vector3 instancePositionWS = ExtractTranslation(matrices[i]);
                if (!IsFiniteVector3(instancePositionWS))
                    continue;

                float distanceSq = (instancePositionWS - playerPositionWS).sqrMagnitude;
                if (!math.isfinite(distanceSq) || distanceSq > detectionRadiusSq)
                    continue;

                float exposure01 = 1f - math.saturate(distanceSq * invDetectionRadiusSq);
                if (!math.isfinite(exposure01) || exposure01 <= bestExposure01)
                    continue;

                bestExposure01 = exposure01;
                bestPositionWS = instancePositionWS;
                bestTemplateIndex = templateIndex;
                bestPayloadIndex = i;
                bestAge01 = age01;
            }
        }

        private void RefreshToxicSporeTemplateMask(bool force)
        {
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (force)
                return;

            if (_cachedToxicSporeTemplateCount == templateCount &&
                ((vegetationBridge == null && templateCount == 0) || templateCount == _toxicSporeTemplateMask.Length))
            {
                return;
            }
        }

        private void RebuildToxicSporeTemplateMaskColdFromCurrentBridge()
        {
            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            RebuildToxicSporeTemplateMaskCold(floraTemplates, templateCount);
        }

        private void RebuildToxicSporeTemplateMaskCold(FloraDataTemplate[] floraTemplates, int templateCount)
        {
            _cachedToxicSporeTemplateCount = templateCount;
            if (templateCount <= 0 || _toxicSporeStableHashId == 0)
            {
                _toxicSporeTemplateMask = Array.Empty<bool>();
                return;
            }

            if (_toxicSporeTemplateMask == null || _toxicSporeTemplateMask.Length != templateCount)
            {
                _toxicSporeTemplateMask = new bool[templateCount]; // COLD ALLOC: bool[floraTemplates.Length] - toxic spore template membership cache - owner: FloraInteractionManager
            }
            else
            {
                Array.Clear(_toxicSporeTemplateMask, 0, _toxicSporeTemplateMask.Length);
            }

            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                    continue;

                _toxicSporeTemplateMask[i] = LocHash.Compute(template.StableId) == _toxicSporeStableHashId;
            }
        }

        private void ClearToxicSporeHazard()
        {
            _lastToxicSporeExposure01 = 0f;
            HectonHazardManager.Unregister(ToxicSporeHazardSourceId);
        }

        private void QueueMatureToxicSporeEventsIfDue(float simulationTime)
        {
            if (simulationTime < _nextMatureToxicSporeEventTime)
                return;

            _nextMatureToxicSporeEventTime = simulationTime + MatureToxicSporeEventIntervalSeconds;
            QueueMatureToxicSporeEventsInLane(underwater: true, ref _underwaterMatureToxicSporeScanCursor);
            QueueMatureToxicSporeEventsInLane(underwater: false, ref _surfaceMatureToxicSporeScanCursor);
        }

        private void QueueMatureToxicSporeEventsInLane(bool underwater, ref int scanCursor)
        {
            if (_vegetationBridge == null)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            if (safeCount <= 0)
                return;

            int budget = math.min(Mathf.Max(1, _matureToxicSporeEventScanBudget), safeCount);
            int startCursor = scanCursor >= 0 && scanCursor < safeCount ? scanCursor : 0;
            int nextCursor = startCursor;
            for (int scanOffset = 0; scanOffset < budget; scanOffset++)
            {
                if (HectonFloraSporeEvents.PendingCount >= HectonFloraSporeEvents.PendingEventCapacity)
                    break;

                int payloadIndex = (startCursor + scanOffset) % safeCount;
                nextCursor = (payloadIndex + 1) % safeCount;
                HectonVegetationInstanceData instanceData = metadata[payloadIndex];
                if (!IsLiveFloraInstance(instanceData) ||
                    !IsMatureFloraGrowth(in instanceData, out float age01))
                {
                    continue;
                }

                int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
                if (!IsToxicSporeEmitter(in instanceData, templateIndex))
                    continue;

                Vector3 positionWS = ExtractTranslation(matrices[payloadIndex]);
                HectonFloraSporeEvents.EnqueueMatureToxicSpore(
                    positionWS,
                    _toxicSporeHazardRadius,
                    _toxicSporeHazardIntensity,
                    age01,
                    templateIndex,
                    payloadIndex,
                    underwater);
            }

            scanCursor = nextCursor;
        }

        private void EmitAllelopathicToxinsAndSuppressLane(bool underwater)
        {
            if (!TryResolveAllelopathicTemplateMasks(
                    out NativeArray<byte> bloodKelpTemplateMask,
                    out NativeArray<byte> ghostWeedTemplateMask) ||
                _bloodKelpToxinDosePerSlowTick <= 0f)
            {
                return;
            }

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || !types.IsCreated || count <= 0)
                return;

            NativeArray<int>.ReadOnly semanticTypes;
            int semanticCount;
            bool hasSemanticPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount)
                : _vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            if (!hasSemanticPayload || !semanticTypes.IsCreated || semanticCount <= 0)
                return;

            int safeCount = math.min(count, math.min(matrices.Length, math.min(metadata.Length, math.min(types.Length, math.min(semanticTypes.Length, semanticCount)))));
            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (!IsLiveFloraInstance(instanceData))
                    continue;

                if (!IsTemplateInMask(instanceData, bloodKelpTemplateMask))
                    continue;

                ChemicalInfluenceGrid.QueueToxicityBurst(ExtractTranslation(matrices[i]), _bloodKelpToxinDosePerSlowTick);
            }

            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (!IsLiveFloraInstance(instanceData))
                    continue;

                if (!IsTemplateInMask(instanceData, ghostWeedTemplateMask))
                    continue;

                Vector3 instancePositionWS = ExtractTranslation(matrices[i]);
                if (!ChemicalInfluenceGrid.TrySampleNormalizedChannels(instancePositionWS, out float4 channels) ||
                    channels.w < _ghostWeedSuppressionThreshold01)
                {
                    continue;
                }

                float toxicity01 = Mathf.Clamp01((channels.w - _ghostWeedSuppressionThreshold01) /
                                                  Mathf.Max(MinimumAllelopathicToxicity01, 1f - _ghostWeedSuppressionThreshold01));
                _destructibleOrganicManager.TryApplyAllelopathicToxinSuppression(
                    matrices[i],
                    instanceData,
                    types[i],
                    semanticTypes[i],
                    toxicity01);
            }
        }

        private void RefreshAllelopathicTemplateMasks(bool force)
        {
            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (force)
                return;

            if (_cachedAllelopathicTemplateCount == templateCount &&
                TryResolveAllelopathicTemplateMasks(
                    out NativeArray<byte> existingBloodKelpTemplateMask,
                    out NativeArray<byte> existingGhostWeedTemplateMask) &&
                existingBloodKelpTemplateMask.Length == templateCount &&
                existingGhostWeedTemplateMask.Length == templateCount)
            {
                return;
            }
        }

        private void RebuildAllelopathicTemplateMasksColdFromCurrentBridge()
        {
            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            RebuildAllelopathicTemplateMasksCold(floraTemplates, templateCount);
        }

        private void RebuildAllelopathicTemplateMasksCold(FloraDataTemplate[] floraTemplates, int templateCount)
        {
            _cachedAllelopathicTemplateCount = templateCount;
            if (templateCount <= 0 || _bloodKelpAllelopathyStableHashId == 0 || _ghostWeedAllelopathyStableHashId == 0)
            {
                ReleaseFloraVaultBuffer(ref _allelopathicBloodKelpTemplateMaskHandle);
                ReleaseFloraVaultBuffer(ref _allelopathicGhostWeedTemplateMaskHandle);
                return;
            }

            if (!TryEnsureFloraVaultBuffer(
                    _wakeDataVault,
                    ref _allelopathicBloodKelpTemplateMaskHandle,
                    FloraBloodKelpTemplateMaskBufferId,
                    templateCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> _) ||
                !TryEnsureFloraVaultBuffer(
                    _wakeDataVault,
                    ref _allelopathicGhostWeedTemplateMaskHandle,
                    FloraGhostWeedTemplateMaskBufferId,
                    templateCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> _))
            {
                return;
            }

            IDataVault dataVault = _wakeDataVault;
            if (dataVault == null ||
                dataVault.IsCompactionFenceActive ||
                !dataVault.TryAcquireMutationGuard(FloraAllelopathicTemplateMutationGuardMask))
            {
                return;
            }

            try
            {
                if (!TryResolveFloraVaultBuffer(
                        dataVault,
                        in _allelopathicBloodKelpTemplateMaskHandle,
                        FloraBloodKelpTemplateMaskBufferId,
                        templateCount,
                        out NativeArray<byte> bloodKelpTemplateMask) ||
                    !TryResolveFloraVaultBuffer(
                        dataVault,
                        in _allelopathicGhostWeedTemplateMaskHandle,
                        FloraGhostWeedTemplateMaskBufferId,
                        templateCount,
                        out NativeArray<byte> ghostWeedTemplateMask))
                {
                    return;
                }

                for (int i = 0; i < templateCount; i++)
                {
                    bloodKelpTemplateMask[i] = 0;
                    ghostWeedTemplateMask[i] = 0;
                }

                for (int i = 0; i < templateCount; i++)
                {
                    FloraDataTemplate template = floraTemplates[i];
                    if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                        continue;

                    int stableHash = LocHash.Compute(template.StableId);
                    bloodKelpTemplateMask[i] = stableHash == _bloodKelpAllelopathyStableHashId ? (byte)1 : (byte)0;
                    ghostWeedTemplateMask[i] = stableHash == _ghostWeedAllelopathyStableHashId ? (byte)1 : (byte)0;
                }
            }
            finally
            {
                dataVault.ReleaseMutationGuard(FloraAllelopathicTemplateMutationGuardMask);
            }
        }

        private static bool IsLiveFloraInstance(HectonVegetationInstanceData instanceData)
        {
            return instanceData.RuntimeState < HectonVegetationInstanceData.RuntimeStateDying - 0.01f &&
                   math.abs(instanceData.HeightScale) > 0.0001f;
        }

        private static bool IsTemplateInMask(HectonVegetationInstanceData instanceData, NativeArray<byte> templateMask)
        {
            if (!templateMask.IsCreated)
                return false;

            int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
            return templateIndex >= 0 &&
                   templateIndex < templateMask.Length &&
                   templateMask[templateIndex] != 0;
        }

        private bool IsToxicSporeEmitter(in HectonVegetationInstanceData instanceData, int templateIndex)
        {
            bool stableTemplateMatch = _toxicSporeTemplateMask != null &&
                                       templateIndex >= 0 &&
                                       templateIndex < _toxicSporeTemplateMask.Length &&
                                       _toxicSporeTemplateMask[templateIndex];
            return stableTemplateMatch ||
                   HectonVegetationRuntimeFlagEncoding.HasGeneticTrait(
                       instanceData.RuntimeFlags,
                       HectonVegetationGeneticTraits.Poisonous);
        }

        private static bool IsMatureFloraGrowth(in HectonVegetationInstanceData instanceData, out float age01)
        {
            age01 = ResolveFloraGrowthAge01(in instanceData);
            return age01 >= MatureToxicSporeAgeThreshold01;
        }

        private static float ResolveFloraGrowthAge01(in HectonVegetationInstanceData instanceData)
        {
            if (instanceData.Reserved0 < 0f)
                return -1f;

            if (instanceData.Reserved0 > 0.0001f)
                return math.saturate(instanceData.Reserved0);

            return 1f;
        }

        private void RefreshModuleParasiteState(float deltaTime)
        {
            if (_parasiteGrowthScheduled)
                return;

            _moduleParasiteScanTimer -= deltaTime;
            if (_moduleParasiteScanTimer > 0f)
                return;

            float scanDeltaTime = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds - _moduleParasiteScanTimer);
            _moduleParasiteScanTimer = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds);
            ClearModuleParasiteStateBack();
            _publishedParasiteAnchorCount = 0;
            _parasiteNodeCount = 0;

            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            ScanActiveModuleParasites(underwater: true);
            ScanActiveModuleParasites(underwater: false);
            EvaluateThermophilicModuleGrowth(scanDeltaTime);
            ScheduleHeadlessParasiteSimulation(scanDeltaTime);
        }

        private void ScanActiveModuleParasites(bool underwater)
        {
            if (_vegetationBridge == null)
                return;

            FloraDataTemplate[] floraTemplates = _vegetationBridge.FloraTemplates;
            if (floraTemplates == null || floraTemplates.Length == 0)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    instanceData.HeightScale <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
                if (templateIndex < 0 || templateIndex >= floraTemplates.Length)
                    continue;

                FloraDataTemplate template = floraTemplates[templateIndex];
                if (template == null ||
                    !template.IsParasiticToModules ||
                    template.AttachmentSurfaceType != FloraDataTemplate.AttachmentSurface.Metal)
                {
                    continue;
                }

                Vector3 instancePositionWS = ExtractTranslation(matrices[i]);
                if (!TryResolveNearestBaseModule(instancePositionWS, _moduleParasiteAttachmentRadius, out BaseModule module))
                    continue;

                float growth01 = ResolveParasiteGrowth01(in instanceData);
                AppendHeadlessParasiteNode(
                    module,
                    instancePositionWS,
                    growth01,
                    template.ModulePowerDrainWatts,
                    template.ModuleInfectionStrength,
                    template.ModuleInfectionRadiusMeters,
                    template.ModuleInfectionPulseFrequency,
                    0f);
            }
        }

        private void EvaluateThermophilicModuleGrowth(float scanDeltaTime)
        {
            int activeModuleCount = BaseModule.ActiveModuleCount;
            if (activeModuleCount == 0)
            {
                ClearThermophileDwell();
                return;
            }

            FloraDataTemplate template = ResolveThermalTubewormTemplate();
            if (template == null || !template.IsThermophilicModuleGrowth)
            {
                ClearThermophileDwell();
                return;
            }

            for (int i = _thermophileDwellCount - 1; i >= 0; i--)
            {
                BaseModule module = _thermophileDwellModules[i];
                if (module == null || !module.isActiveAndEnabled)
                    RemoveThermophileDwellAt(i);
            }

            float validationRadiusSq = _thermophileReactorValidationRadius * _thermophileReactorValidationRadius;
            float thresholdTemperature = template.ThermalActivationTemperatureCelsius;
            float dwellThresholdSeconds = template.ThermalActivationDwellSeconds;
            for (int i = 0; i < activeModuleCount; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null || !module.isActiveAndEnabled || !module.TryGetHostedBioReactor(out BioReactor reactor) || reactor == null)
                {
                    RemoveThermophileDwell(module);
                    continue;
                }

                Vector3 modulePosition = module.transform.position;
                Vector3 reactorPosition = reactor.transform.position;
                if (reactor.PowerRating <= 0.0001f ||
                    (reactorPosition - modulePosition).sqrMagnitude > validationRadiusSq)
                {
                    RemoveThermophileDwell(module);
                    continue;
                }

                float roomTemperature = module.ResolveHostRoomTemperatureCelsius();
                if (roomTemperature < thresholdTemperature)
                {
                    RemoveThermophileDwell(module);
                    continue;
                }

                float nextDwellSeconds = scanDeltaTime;
                if (TryGetThermophileDwell(module, out float accumulatedDwellSeconds))
                    nextDwellSeconds += accumulatedDwellSeconds;

                if (!SetThermophileDwell(module, nextDwellSeconds))
                    continue;

                if (nextDwellSeconds < dwellThresholdSeconds)
                    continue;

                AppendHeadlessParasiteNode(
                    module,
                    module.ResolveBotanyAnchorWorldPosition(),
                    1f,
                    template.ModulePowerDrainWatts,
                    template.ModuleInfectionStrength,
                    template.ModuleInfectionRadiusMeters,
                    template.ModuleInfectionPulseFrequency,
                    1f);
            }
        }

        private FloraDataTemplate ResolveThermalTubewormTemplate()
        {
            if (_vegetationBridge == null)
                return null;

            FloraDataTemplate[] floraTemplates = _vegetationBridge.FloraTemplates;
            if (floraTemplates == null)
                return null;

            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || !template.IsThermophilicModuleGrowth)
                    continue;

                if (_thermalTubewormStableHashId == 0 || LocHash.Compute(template.StableId) == _thermalTubewormStableHashId)
                    return template;
            }

            return null;
        }

        private static float ResolveParasiteGrowth01(in HectonVegetationInstanceData instanceData)
        {
            if (instanceData.Reserved0 > 0.0001f)
                return Mathf.Clamp01(instanceData.Reserved0);

            return MinimumParasiteScale;
        }

        private void AppendHeadlessParasiteNode(
            BaseModule module,
            Vector3 positionWS,
            float authoredGrowth01,
            float drainWatts,
            float infectionLevel,
            float radiusMeters,
            float pulseFrequency,
            float thermalGrowthFlag)
        {
            if (module == null ||
                !TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes) ||
                _parasiteNodeCount >= parasiteNodes.Length)
            {
                ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
                return;
            }

            try
            {
                int moduleRuntimeId = ResolveModuleRuntimeId(module);
                if (moduleRuntimeId == 0)
                    return;

                int writeIndex = _parasiteNodeCount;
                ParasiteNode previous = parasiteNodes[writeIndex];
                float growth01 = Mathf.Clamp01(authoredGrowth01);
                byte state = ParasiteNodeStateAlive;
                float3 nextPosition = new float3(positionWS.x, positionWS.y, positionWS.z);
                float matureAttachedSeconds = 0f;
                double currentTimeSeconds = Time.timeAsDouble;
                double growthDurationSeconds = ResolveParasiteScaleGrowthSecondsDouble();
                double birthTimeSeconds = currentTimeSeconds - (growth01 * growthDurationSeconds);
                if (previous.HostModuleRuntimeId == moduleRuntimeId &&
                    math.lengthsq(previous.PositionWS - nextPosition) <= 0.25f)
                {
                    growth01 = Mathf.Max(growth01, Mathf.Clamp01(previous.GrowthLevel));
                    matureAttachedSeconds = Mathf.Max(0f, previous.MatureAttachedSeconds);
                    birthTimeSeconds = IsFiniteDouble(previous.BirthTimeSeconds) && previous.BirthTimeSeconds > 0d
                        ? previous.BirthTimeSeconds
                        : currentTimeSeconds - (growth01 * growthDurationSeconds);
                    if (previous.State == ParasiteNodeStateDead)
                        state = ParasiteNodeStateDead;
                }

                if (growth01 < MatureParasiteGrowthThreshold)
                    matureAttachedSeconds = 0f;

                float resolvedRadius = Mathf.Max(0.25f, radiusMeters);
                float addedMassKilograms = resolvedRadius * resolvedRadius * resolvedRadius *
                                           Mathf.Max(0f, _parasiteAddedMassKilogramsPerRadiusCubic);

                parasiteNodes[writeIndex] = new ParasiteNode
                {
                    PositionWS = nextPosition,
                    HostModuleRuntimeId = moduleRuntimeId,
                    GrowthLevel = growth01,
                    BirthTimeSeconds = birthTimeSeconds,
                    PowerDrainWatts = Mathf.Max(0f, drainWatts),
                    InfectionStrength = Mathf.Clamp01(infectionLevel),
                    RootDrainMultiplier = Mathf.Max(1f, _matureParasiteRootDrainMultiplier),
                    RadiusMeters = resolvedRadius,
                    PulseFrequency = Mathf.Max(0.01f, pulseFrequency),
                    ThermalGrowthFlag = Mathf.Clamp01(thermalGrowthFlag),
                    MatureAttachedSeconds = matureAttachedSeconds,
                    AddedMassKilograms = addedMassKilograms,
                    State = state
                };
                _parasiteNodeCount++;
            }
            finally
            {
                ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
            }
        }

        private void ScheduleHeadlessParasiteSimulation(float deltaTime)
        {
            if (!TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes) || _parasiteNodeCount <= 0)
            {
                ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
                ApplyHeadlessParasiteStateFromNodes();
                return;
            }

            ChemicalInfluenceGrid.TryGetActivePublishedSnapshot(
                out NativeArray<float4>.ReadOnly frontGrid,
                out NativeArray<float4>.ReadOnly overlayGrid,
                out int3 dimensions,
                out float3 origin,
                out float3 cellSize);

            try
            {
                _parasiteGrowthHandle = new ParasiteGrowthJob
                {
                    Nodes = parasiteNodes,
                    NodeCount = _parasiteNodeCount,
                    DeltaTime = Mathf.Max(0f, deltaTime),
                    CurrentTimeSeconds = Time.timeAsDouble,
                    GrowthPerSecond = ResolveHeadlessParasiteGrowthPerSecond(),
                    DefoliantKillThreshold = Mathf.Max(0.01f, _headlessParasiteDefoliantKillThreshold),
                    MatureGrowthThreshold = MatureParasiteGrowthThreshold,
                    ChemicalFrontGrid = frontGrid,
                    ChemicalOverlayGrid = overlayGrid,
                    ChemicalDimensions = dimensions,
                    ChemicalOrigin = origin,
                    ChemicalCellSize = cellSize
                }.Schedule(_parasiteNodeCount, 32);
                _parasiteGrowthScheduled = true;
            }
            finally
            {
                ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
            }
        }

        private void TryFinalizeHeadlessParasiteSimulation()
        {
            if (!_parasiteGrowthScheduled)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _parasiteGrowthHandle))
                return;

            _parasiteGrowthScheduled = false;
            ApplyHeadlessParasiteStateFromNodes();
        }

        private void ApplyHeadlessParasiteStateFromNodes()
        {
            ClearModuleParasiteStateBack();
            _publishedParasiteAnchorCount = 0;

            if (TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes))
            {
                try
                {
                    int safeCount = math.min(_parasiteNodeCount, parasiteNodes.Length);
                    for (int i = 0; i < safeCount; i++)
                    {
                        ParasiteNode node = parasiteNodes[i];
                        if (node.State == ParasiteNodeStateDead || node.GrowthLevel <= 0.001f)
                            continue;

                        BaseModule module = ResolveBaseModuleByRuntimeId(node.HostModuleRuntimeId);
                        if (module == null ||
                            !module.isActiveAndEnabled ||
                            module.HasImploded ||
                            module.IntegrityState == BaseModuleIntegrityState.Ruptured)
                        {
                            node.GrowthLevel = 0f;
                            node.BirthTimeSeconds = 0d;
                            node.MatureAttachedSeconds = 0f;
                            node.State = ParasiteNodeStateDead;
                            parasiteNodes[i] = node;
                            continue;
                        }

                        float growth01 = Mathf.Clamp01(node.GrowthLevel);
                        float scale01 = ResolveParasiteScale01(node.BirthTimeSeconds);
                        float rootDrainWatts = growth01 >= MatureParasiteGrowthThreshold
                            ? node.PowerDrainWatts * Mathf.Max(1f, node.RootDrainMultiplier)
                            : 0f;
                        float infection01 = Mathf.Clamp01(node.InfectionStrength * scale01);
                        float thermalInsulation01 = Mathf.Clamp01(growth01 * infection01 * _parasiteThermalInsulationAtFullInfection);
                        float overheatMultiplier = math.lerp(
                            1f,
                            Mathf.Max(1f, _parasiteBioReactorOverheatMultiplier),
                            math.saturate(growth01 * infection01));
                        AccumulateModuleParasiteState(
                            module,
                            node.PowerDrainWatts * scale01,
                            infection01,
                            rootDrainWatts * scale01,
                            growth01,
                            node.MatureAttachedSeconds,
                            node.AddedMassKilograms * growth01,
                            thermalInsulation01,
                            overheatMultiplier);
                        AppendParasiteAnchor(
                            new Vector3(node.PositionWS.x, node.PositionWS.y, node.PositionWS.z),
                            node.RadiusMeters * scale01,
                            infection01,
                            node.PulseFrequency,
                            node.ThermalGrowthFlag);
                    }
                }
                finally
                {
                    ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
                }
            }

            ApplyFungalMindSpreadFromCurrentState();
            ApplyModuleParasiteStateDiff();
            PublishParasiteInfectionGlobals();
        }

        private void AccumulateModuleParasiteState(
            BaseModule module,
            float drainWatts,
            float infectionLevel,
            float rootDrainWatts,
            float rootInfectionLevel,
            float matureAttachedSeconds,
            float addedMassKilograms,
            float thermalInsulation01,
            float bioReactorOverheatMultiplier)
        {
            if (module == null)
                return;

            int stateIndex = FindModuleIndex(_moduleParasiteStateBackModules, _moduleParasiteStateBackCount, module);
            if (stateIndex >= 0)
            {
                ModuleParasiteState state = _moduleParasiteStateBackValues[stateIndex];
                state.PowerDrainWatts += Mathf.Max(0f, drainWatts);
                state.InfectionLevel = Mathf.Clamp01(Mathf.Max(state.InfectionLevel, infectionLevel));
                state.RootPowerDrainWatts += Mathf.Max(0f, rootDrainWatts);
                state.RootInfectionLevel = Mathf.Clamp01(Mathf.Max(state.RootInfectionLevel, rootInfectionLevel));
                state.MaxMatureAttachedSeconds = Mathf.Max(state.MaxMatureAttachedSeconds, Mathf.Max(0f, matureAttachedSeconds));
                state.AddedMassKilograms += Mathf.Max(0f, addedMassKilograms);
                state.ThermalInsulation01 = Mathf.Clamp01(Mathf.Max(state.ThermalInsulation01, thermalInsulation01));
                state.BioReactorOverheatMultiplier = Mathf.Max(
                    Mathf.Max(1f, state.BioReactorOverheatMultiplier),
                    Mathf.Max(1f, bioReactorOverheatMultiplier));
                state.ParasiteCount++;
                _moduleParasiteStateBackValues[stateIndex] = state;
                return;
            }

            if (_moduleParasiteStateBackCount >= _moduleParasiteStateBackModules.Length)
                return;

            int writeIndex = _moduleParasiteStateBackCount;
            _moduleParasiteStateBackModules[writeIndex] = module;
            _moduleParasiteStateBackValues[writeIndex] = new ModuleParasiteState
            {
                PowerDrainWatts = Mathf.Max(0f, drainWatts),
                InfectionLevel = Mathf.Clamp01(infectionLevel),
                RootPowerDrainWatts = Mathf.Max(0f, rootDrainWatts),
                RootInfectionLevel = Mathf.Clamp01(rootInfectionLevel),
                MaxMatureAttachedSeconds = Mathf.Max(0f, matureAttachedSeconds),
                AddedMassKilograms = Mathf.Max(0f, addedMassKilograms),
                ThermalInsulation01 = Mathf.Clamp01(thermalInsulation01),
                BioReactorOverheatMultiplier = Mathf.Max(1f, bioReactorOverheatMultiplier),
                ParasiteCount = 1
            };
            _moduleParasiteStateBackCount++;
        }

        private void AppendParasiteAnchor(Vector3 positionWS, float radiusMeters, float infectionLevel, float pulseFrequency, float thermalGrowthFlag)
        {
            if (_publishedParasiteAnchorCount >= MaxParasiteAnchors)
                return;

            _parasiteAnchorData[_publishedParasiteAnchorCount] = new Vector4(
                positionWS.x,
                positionWS.y,
                positionWS.z,
                Mathf.Max(0.25f, radiusMeters));
            _parasiteAnchorParams[_publishedParasiteAnchorCount] = new Vector4(
                Mathf.Clamp01(infectionLevel),
                Mathf.Max(0.01f, pulseFrequency),
                thermalGrowthFlag,
                0f);
            _publishedParasiteAnchorCount++;
        }

        internal bool TryResolveNearestModuleParasite(BaseModule hostModule, Vector3 origin, out ModuleParasiteTarget target)
        {
            target = default;
            if (_publishedParasiteAnchorCount <= 0)
                return false;

            bool found = false;
            float bestDistanceSq = float.MaxValue;
            bool hasFixedHost = hostModule != null;
            Vector3 fixedHostPosition = hasFixedHost ? hostModule.transform.position : default;
            for (int i = 0; i < _publishedParasiteAnchorCount; i++)
            {
                Vector4 anchorData = _parasiteAnchorData[i];
                Vector3 anchorPosition = new Vector3(anchorData.x, anchorData.y, anchorData.z);
                float anchorRadius = Mathf.Max(0.25f, anchorData.w);
                BaseModule resolvedHost = hostModule;
                if (hasFixedHost)
                {
                    float attachmentRadius = _moduleParasiteAttachmentRadius + anchorRadius;
                    if ((anchorPosition - fixedHostPosition).sqrMagnitude > attachmentRadius * attachmentRadius)
                        continue;
                }
                else if (!TryResolveNearestBaseModule(anchorPosition, _moduleParasiteAttachmentRadius + anchorRadius, out resolvedHost))
                {
                    continue;
                }

                if (resolvedHost == null || resolvedHost.ParasiteInfectionLevel <= 0.0001f)
                    continue;

                float distanceSq = (anchorPosition - origin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                float infectionLevel = Mathf.Max(Mathf.Clamp01(_parasiteAnchorParams[i].x), resolvedHost.ParasiteInfectionLevel);
                float criticalityWeight = 1f + (infectionLevel * 5f);
                bestDistanceSq = distanceSq;
                target = new ModuleParasiteTarget(resolvedHost, anchorPosition, anchorRadius, infectionLevel, criticalityWeight);
                found = true;
            }

            return found;
        }

        internal bool TryApplyDroneParasiteCut(Vector3 hitPoint, Vector3 direction, float deliveredDamage, float normalizedPower)
        {
            return TryApplyModuleParasiteCut(
                hitPoint,
                Vector3.up,
                direction,
                deliveredDamage,
                normalizedPower,
                (uint)FloraDataTemplate.VulnerabilityMask.PlasmaCut);
        }

        internal bool TryApplyModuleParasiteCut(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (_destructibleOrganicManager == null)
                RefreshDestructibleOrganicManagerFromRuntime();

            if (_destructibleOrganicManager == null)
                return false;

            Vector3 resolvedDirection = NormalizeVector3Fast(direction, Vector3.down);
            Vector3 resolvedNormal = NormalizeVector3Fast(hitNormal, Vector3.up);
            bool applied = _destructibleOrganicManager.TryApplyToolHit(
                hitPoint,
                resolvedNormal,
                resolvedDirection,
                Mathf.Max(0.1f, deliveredDamage),
                Mathf.Clamp01(normalizedPower),
                toolCapabilityMask);

            if (applied)
            {
                RegisterFloraDamageReaction(hitPoint, deliveredDamage, normalizedPower);
                RequestModuleParasiteRescan();
            }

            return applied;
        }

        private void RegisterFloraDamageReaction(Vector3 positionWS, float deliveredDamage, float normalizedPower)
        {
            _damageReactionPositionWS = positionWS;
            _damageReactionStrength = math.saturate(Mathf.Max(0.1f, deliveredDamage) * 0.22f + Mathf.Clamp01(normalizedPower) * 0.78f);
            _damageReactionRemainingSeconds = DamageReactionDurationSeconds;
            QueueDamageReactionGlobal(0f);
        }

        private void QueueDamageReactionGlobal(float deltaTime)
        {
            _pendingDamageReactionDeltaTime += math.max(0f, deltaTime);
            _damageReactionGlobalsDirty = true;
        }

        private void FlushDamageReactionGlobal()
        {
            if (!_damageReactionGlobalsDirty)
                return;

            float deltaTime = _pendingDamageReactionDeltaTime;
            _pendingDamageReactionDeltaTime = 0f;
            _damageReactionGlobalsDirty = false;

            if (_damageReactionRemainingSeconds <= 0f || _damageReactionStrength <= 0f)
            {
                _damageReactionRemainingSeconds = 0f;
                _damageReactionStrength = 0f;
                Shader.SetGlobalVector(_FloraDamageReactionId, Vector4.zero);
                return;
            }

            _damageReactionRemainingSeconds = math.max(0f, _damageReactionRemainingSeconds - math.max(0f, deltaTime));
            float fade01 = math.saturate(_damageReactionRemainingSeconds * DamageReactionDurationReciprocal);
            float strength = _damageReactionStrength * fade01;
            Shader.SetGlobalVector(
                _FloraDamageReactionId,
                new Vector4(_damageReactionPositionWS.x, _damageReactionPositionWS.y, _damageReactionPositionWS.z, strength));
        }

        internal void RequestModuleParasiteRescan()
        {
            _moduleParasiteScanTimer = 0f;
        }

        internal bool KillAttachedParasites(BaseModule hostModule)
        {
            if (hostModule == null)
                return false;

            int moduleRuntimeId = ResolveModuleRuntimeId(hostModule);
            if (moduleRuntimeId == 0)
                return false;

            if (_parasiteGrowthScheduled)
            {
                TryFinalizeHeadlessParasiteSimulation();
                if (_parasiteGrowthScheduled)
                    return false;
            }

            bool killed = false;
            if (TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes))
            {
                try
                {
                    int safeCount = math.min(_parasiteNodeCount, parasiteNodes.Length);
                    for (int i = 0; i < safeCount; i++)
                    {
                        ParasiteNode node = parasiteNodes[i];
                        if (node.HostModuleRuntimeId != moduleRuntimeId || node.State == ParasiteNodeStateDead)
                            continue;

                        node.GrowthLevel = 0f;
                        node.BirthTimeSeconds = 0d;
                        node.MatureAttachedSeconds = 0f;
                        node.State = ParasiteNodeStateDead;
                        parasiteNodes[i] = node;
                        killed = true;
                    }
                }
                finally
                {
                    ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
                }
            }

            if (killed ||
                hostModule.AttachedParasiteCount > 0 ||
                hostModule.ParasiteRootPowerDrainWatts > 0.01f)
            {
                hostModule.SetParasiteInfestation(0f, 0f, 0f, 0f, 0);
                hostModule.SetParasiteStructuralEffects(0f, 0f, 1f);
                Hecton8.Construction.BaseDegradationSystem.ClearParasiteSporeHazard(hostModule);
                Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(hostModule);
                RequestModuleParasiteRescan();
            }

            return killed;
        }

        private bool TryResolveNearestBaseModule(Vector3 origin, float radius, out BaseModule module)
        {
            module = null;
            if (_moduleQueryHits == null || radius <= 0.0001f)
                return false;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(origin, radius, SpatialTargetKind.Module, _moduleQueryHits);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _moduleQueryHits[i].Transform;
                if (hitTransform == null)
                    continue;

                if (!TryResolveBaseModuleFromTransform(hitTransform, out BaseModule candidate))
                    continue;

                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (!IsMetalAttachmentLayer(hitTransform.gameObject.layer) &&
                    !IsMetalAttachmentLayer(candidate.gameObject.layer))
                {
                    continue;
                }

                Vector3 candidateVisualPosition = candidate.transform.position;
                Vector3 visualDelta = candidateVisualPosition - origin;
                float distanceSq = visualDelta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                module = candidate;
            }

            return module != null;
        }

        private static bool TryResolveBaseModuleFromTransform(Transform start, out BaseModule module)
        {
            module = null;
            Transform current = start;
            int depth = 0;
            int activeModuleCount = BaseModule.ActiveModuleCount;
            while (current != null && depth < MaxModuleParentResolveDepth)
            {
                for (int i = 0; i < activeModuleCount; i++)
                {
                    BaseModule candidate = BaseModule.GetActiveModuleAt(i);
                    if (candidate == null || !candidate.isActiveAndEnabled)
                        continue;

                    if (candidate.transform == current)
                    {
                        module = candidate;
                        return true;
                    }
                }

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool IsMetalAttachmentLayer(int layer)
        {
            return layer == HectonLayerMasks.BaseModule;
        }

        private float ResolveHeadlessParasiteGrowthPerSecond()
        {
            double growthDurationSeconds = ResolveParasiteScaleGrowthSecondsDouble();
            if (growthDurationSeconds <= 0.001d)
                return Mathf.Max(0f, _headlessParasiteGrowthPerSecond);

            return (float)(1d / growthDurationSeconds);
        }

        private float ResolveParasiteScaleGrowthSeconds()
        {
            return (float)Math.Min(float.MaxValue, ResolveParasiteScaleGrowthSecondsDouble());
        }

        private double ResolveParasiteScaleGrowthSecondsDouble()
        {
            IAtmosphereReadModel atmosphereManager = _atmosphereReadModel;
            double daySeconds = atmosphereManager != null
                ? Mathf.Max(1f, atmosphereManager.CycleDuration)
                : DefaultInGameDaySeconds;
            return Math.Max(0.001d, _parasiteScaleGrowthDays) * daySeconds;
        }

        private float ResolveParasiteScale01(double birthTimeSeconds)
        {
            double growthDurationSeconds = ResolveParasiteScaleGrowthSecondsDouble();
            double ageSeconds = IsFiniteDouble(birthTimeSeconds)
                ? Math.Max(0d, Time.timeAsDouble - birthTimeSeconds)
                : 0d;
            double growth01 = growthDurationSeconds > 0.001d
                ? ageSeconds / growthDurationSeconds
                : 1d;
            if (growth01 < 0d)
                growth01 = 0d;
            else if (growth01 > 1d)
                growth01 = 1d;

            return math.lerp(MinimumParasiteScale, 1f, (float)growth01);
        }

        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static BaseModule ResolveBaseModuleByRuntimeId(int moduleRuntimeId)
        {
            if (moduleRuntimeId == 0)
                return null;

            int count = BaseModule.ActiveModuleCount;
            for (int i = 0; i < count; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null)
                    continue;

                if (ResolveModuleRuntimeId(module) == moduleRuntimeId)
                    return module;
            }

            return null;
        }

        private static int ResolveModuleRuntimeId(BaseModule module)
        {
            return module != null
                ? unchecked((int)EntityId.ToULong(module.GetEntityId()))
                : 0;
        }

        private void ApplyFungalMindSpreadFromCurrentState()
        {
            if (!TryResolveParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes) || _parasiteNodeCount >= parasiteNodes.Length)
                return;

            IConstructionParasiteGraphService constructionParasiteGraph = _constructionParasiteGraph;
            if (constructionParasiteGraph == null)
                return;

            for (int i = 0; i < _moduleParasiteStateBackCount; i++)
            {
                BaseModule sourceModule = _moduleParasiteStateBackModules[i];
                if (sourceModule == null || !sourceModule.isActiveAndEnabled)
                    continue;

                ModuleParasiteState state = _moduleParasiteStateBackValues[i];
                if (state.RootPowerDrainWatts <= 0.01f || state.MaxMatureAttachedSeconds <= 0.001f)
                    continue;

                if (!constructionParasiteGraph.TryResolveFungalMindTarget(sourceModule, out BaseModule targetModule, out float targetPotential))
                    continue;

                if (targetModule == null ||
                    ReferenceEquals(targetModule, sourceModule) ||
                    !targetModule.isActiveAndEnabled)
                {
                    continue;
                }

                int targetRuntimeId = ResolveModuleRuntimeId(targetModule);
                if (targetRuntimeId == 0 || HasAliveParasiteForModule(targetRuntimeId))
                    continue;

                float potentialScale = Mathf.Clamp01(Mathf.Abs(targetPotential) / Mathf.Max(1f, Mathf.Abs(sourceModule.PowerRatingForHabitatGraph)));
                float inheritedDrainWatts = state.PowerDrainWatts * math.lerp(_fungalMindSpreadDrainScale, 1f, math.saturate(potentialScale));
                float inheritedInfection = Mathf.Clamp01(Mathf.Max(state.InfectionLevel, state.RootInfectionLevel) * 0.75f);
                AppendHeadlessParasiteNode(
                    targetModule,
                    targetModule.ResolveBotanyAnchorWorldPosition(),
                    _fungalMindSpreadSeedGrowth01,
                    inheritedDrainWatts,
                    inheritedInfection,
                    Mathf.Max(0.5f, _moduleParasiteAttachmentRadius * 0.5f),
                    0.7f + potentialScale,
                    0f);
            }
        }

        private bool HasAliveParasiteForModule(int moduleRuntimeId)
        {
            if (moduleRuntimeId == 0 || !TryResolveParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes))
                return false;

            int safeCount = math.min(_parasiteNodeCount, parasiteNodes.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ParasiteNode node = parasiteNodes[i];
                if (node.HostModuleRuntimeId == moduleRuntimeId &&
                    node.State != ParasiteNodeStateDead &&
                    node.GrowthLevel > 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyModuleParasiteStateDiff()
        {
            for (int i = 0; i < _moduleParasiteStateFrontCount; i++)
            {
                BaseModule module = _moduleParasiteStateFrontModules[i];
                if (module != null && FindModuleIndex(_moduleParasiteStateBackModules, _moduleParasiteStateBackCount, module) < 0)
                {
                    module.SetParasiteInfestation(0f, 0f, 0f, 0f, 0);
                    module.SetParasiteStructuralEffects(0f, 0f, 1f);
                    Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(module);
                }
            }

            for (int i = 0; i < _moduleParasiteStateBackCount; i++)
            {
                BaseModule module = _moduleParasiteStateBackModules[i];
                if (module == null)
                    continue;

                ModuleParasiteState nextState = _moduleParasiteStateBackValues[i];
                if (!TryGetModuleState(_moduleParasiteStateFrontModules, _moduleParasiteStateFrontValues, _moduleParasiteStateFrontCount, module, out ModuleParasiteState previousState) ||
                    !AreEquivalentParasiteStates(in previousState, in nextState))
                {
                    module.SetParasiteInfestation(
                        nextState.PowerDrainWatts,
                        nextState.InfectionLevel,
                        nextState.RootPowerDrainWatts,
                        nextState.RootInfectionLevel,
                        nextState.ParasiteCount);
                }

                module.SetParasiteStructuralEffects(
                    nextState.AddedMassKilograms,
                    nextState.ThermalInsulation01,
                    nextState.BioReactorOverheatMultiplier);
                Hecton8.Construction.BaseDegradationSystem.SynchronizeParasiteStructuralStress(
                    module,
                    nextState.MaxMatureAttachedSeconds,
                    Mathf.Max(nextState.InfectionLevel, nextState.RootInfectionLevel),
                    nextState.AddedMassKilograms);
            }

            SwapModuleParasiteStateBuffers();
        }

        private void SwapModuleParasiteStateBuffers()
        {
            BaseModule[] previousFrontModules = _moduleParasiteStateFrontModules;
            ModuleParasiteState[] previousFrontValues = _moduleParasiteStateFrontValues;
            int previousFrontCount = _moduleParasiteStateFrontCount;

            _moduleParasiteStateFrontModules = _moduleParasiteStateBackModules;
            _moduleParasiteStateFrontValues = _moduleParasiteStateBackValues;
            _moduleParasiteStateFrontCount = _moduleParasiteStateBackCount;

            _moduleParasiteStateBackModules = previousFrontModules;
            _moduleParasiteStateBackValues = previousFrontValues;
            _moduleParasiteStateBackCount = previousFrontCount;
            ClearModuleParasiteStateBack();
        }

        private void ClearModuleParasiteStateFront()
        {
            ClearModuleStateBuffer(
                _moduleParasiteStateFrontModules,
                _moduleParasiteStateFrontValues,
                ref _moduleParasiteStateFrontCount);
        }

        private void ClearModuleParasiteStateBack()
        {
            ClearModuleStateBuffer(
                _moduleParasiteStateBackModules,
                _moduleParasiteStateBackValues,
                ref _moduleParasiteStateBackCount);
        }

        private static void ClearModuleStateBuffer(BaseModule[] modules, ModuleParasiteState[] states, ref int count)
        {
            int safeCapacity = modules != null && states != null ? math.min(modules.Length, states.Length) : 0;
            int safeCount = math.min(math.max(0, count), safeCapacity);
            for (int i = 0; i < safeCount; i++)
            {
                modules[i] = null;
                states[i] = default;
            }

            count = 0;
        }

        private static bool TryGetModuleState(
            BaseModule[] modules,
            ModuleParasiteState[] states,
            int count,
            BaseModule module,
            out ModuleParasiteState state)
        {
            if (states == null)
            {
                state = default;
                return false;
            }

            int index = FindModuleIndex(modules, count, module);
            if (index >= 0)
            {
                state = states[index];
                return true;
            }

            state = default;
            return false;
        }

        private static int FindModuleIndex(BaseModule[] modules, int count, BaseModule module)
        {
            if (module == null || modules == null)
                return -1;

            int safeCount = math.min(math.max(0, count), modules.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (ReferenceEquals(modules[i], module))
                    return i;
            }

            return -1;
        }

        private void ClearThermophileDwell()
        {
            int safeCount = math.min(math.max(0, _thermophileDwellCount), _thermophileDwellModules.Length);
            for (int i = 0; i < safeCount; i++)
            {
                _thermophileDwellModules[i] = null;
                _thermophileDwellSeconds[i] = 0f;
            }

            _thermophileDwellCount = 0;
        }

        private bool TryGetThermophileDwell(BaseModule module, out float dwellSeconds)
        {
            int index = FindThermophileDwellIndex(module);
            if (index >= 0)
            {
                dwellSeconds = _thermophileDwellSeconds[index];
                return true;
            }

            dwellSeconds = 0f;
            return false;
        }

        private bool SetThermophileDwell(BaseModule module, float dwellSeconds)
        {
            if (module == null)
                return false;

            int index = FindThermophileDwellIndex(module);
            if (index >= 0)
            {
                _thermophileDwellSeconds[index] = Mathf.Max(0f, dwellSeconds);
                return true;
            }

            if (_thermophileDwellCount >= _thermophileDwellModules.Length)
                return false;

            int writeIndex = _thermophileDwellCount;
            _thermophileDwellModules[writeIndex] = module;
            _thermophileDwellSeconds[writeIndex] = Mathf.Max(0f, dwellSeconds);
            _thermophileDwellCount++;
            return true;
        }

        private void RemoveThermophileDwell(BaseModule module)
        {
            int index = FindThermophileDwellIndex(module);
            if (index >= 0)
                RemoveThermophileDwellAt(index);
        }

        private void RemoveThermophileDwellAt(int index)
        {
            if ((uint)index >= (uint)_thermophileDwellCount)
                return;

            int lastIndex = _thermophileDwellCount - 1;
            if (index != lastIndex)
            {
                _thermophileDwellModules[index] = _thermophileDwellModules[lastIndex];
                _thermophileDwellSeconds[index] = _thermophileDwellSeconds[lastIndex];
            }

            _thermophileDwellModules[lastIndex] = null;
            _thermophileDwellSeconds[lastIndex] = 0f;
            _thermophileDwellCount--;
        }

        private int FindThermophileDwellIndex(BaseModule module)
        {
            if (module == null)
                return -1;

            int safeCount = math.min(math.max(0, _thermophileDwellCount), _thermophileDwellModules.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (ReferenceEquals(_thermophileDwellModules[i], module))
                    return i;
            }

            return -1;
        }

        private static bool AreEquivalentParasiteStates(in ModuleParasiteState lhs, in ModuleParasiteState rhs)
        {
            return Mathf.Abs(lhs.PowerDrainWatts - rhs.PowerDrainWatts) <= 0.01f &&
                   Mathf.Abs(lhs.InfectionLevel - rhs.InfectionLevel) <= 0.001f &&
                   Mathf.Abs(lhs.RootPowerDrainWatts - rhs.RootPowerDrainWatts) <= 0.01f &&
                   Mathf.Abs(lhs.RootInfectionLevel - rhs.RootInfectionLevel) <= 0.001f &&
                   Mathf.Abs(lhs.MaxMatureAttachedSeconds - rhs.MaxMatureAttachedSeconds) <= 0.25f &&
                   Mathf.Abs(lhs.AddedMassKilograms - rhs.AddedMassKilograms) <= 0.1f &&
                   Mathf.Abs(lhs.ThermalInsulation01 - rhs.ThermalInsulation01) <= 0.001f &&
                   Mathf.Abs(lhs.BioReactorOverheatMultiplier - rhs.BioReactorOverheatMultiplier) <= 0.001f &&
                   lhs.ParasiteCount == rhs.ParasiteCount;
        }

        private void ClearModuleParasiteState()
        {
            for (int i = 0; i < _moduleParasiteStateFrontCount; i++)
            {
                BaseModule module = _moduleParasiteStateFrontModules[i];
                if (module != null)
                {
                    module.SetParasiteInfestation(0f, 0f, 0f, 0f, 0);
                    module.SetParasiteStructuralEffects(0f, 0f, 1f);
                    Hecton8.Construction.BaseDegradationSystem.ClearParasiteStructuralState(module);
                }
            }

            ClearModuleParasiteStateFront();
            ClearModuleParasiteStateBack();
            ClearThermophileDwell();
            _publishedParasiteAnchorCount = 0;
            PublishParasiteInfectionGlobals();
        }

        private void PublishParasiteInfectionGlobals()
        {
            _parasiteInfectionGlobalsDirty = true;
        }

        private void FlushParasiteInfectionGlobals()
        {
            if (!_parasiteInfectionGlobalsDirty)
                return;

            _parasiteInfectionGlobalsDirty = false;
            Shader.SetGlobalVectorArray(_ParasiteAnchorDataId, _parasiteAnchorData);
            Shader.SetGlobalVectorArray(_ParasiteAnchorParamsId, _parasiteAnchorParams);
            Shader.SetGlobalVector(
                _ParasiteGlobalsId,
                new Vector4(
                    _publishedParasiteAnchorCount,
                    GetCurrentSimulationTimeSeconds(),
                    0.35f,
                    1f));
        }

        public void RegisterDefensiveSporeBurst(Vector3 positionWS, float intensity01)
        {
            if (!IsFiniteVector3(positionWS) || !float.IsFinite(intensity01))
                return;

            float simulationTime = GetCurrentSimulationTimeSeconds();
            float clampedIntensity = Mathf.Max(0.1f, intensity01);
            ChemicalInfluenceGrid.QueueToxicityBurst(positionWS, _defensiveSporeBurstDose * clampedIntensity);
            ChemicalInfluenceGrid.QueueFearPheromone(positionWS, Mathf.Clamp01(clampedIntensity));
            HectonFloraSporeEvents.EnqueueDefensiveSporeBurst(
                positionWS,
                _defensiveSporeBurstRadius,
                clampedIntensity);

            if (_defensiveSporeBursts == null || _defensiveSporeBursts.Length == 0)
                return;

            int writeIndex = _defensiveSporeBurstCount < _defensiveSporeBursts.Length
                ? _defensiveSporeBurstCount
                : FindWeakestDefensiveSporeBurstIndex(simulationTime);

            _defensiveSporeBursts[writeIndex] = new DefensiveSporeBurstState
            {
                PositionWS = positionWS,
                Radius = _defensiveSporeBurstRadius,
                Intensity = clampedIntensity,
                ExpireTimeSeconds = simulationTime + _defensiveSporeBurstLifetimeSeconds
            };
            _defensiveSporeBurstCount = Mathf.Min(_defensiveSporeBursts.Length, Mathf.Max(_defensiveSporeBurstCount, writeIndex + 1));
        }

        private void UpdateDefensiveSporeBursts(Vector3 playerPositionWS)
        {
            if (_defensiveSporeBursts == null || _defensiveSporeBurstCount <= 0)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            float simulationTime = GetCurrentSimulationTimeSeconds();
            CompactDefensiveSporeBursts(simulationTime);
            if (_defensiveSporeBurstCount <= 0)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            float strongestExposure = 0f;
            Vector3 strongestPosition = Vector3.zero;
            for (int i = 0; i < _defensiveSporeBurstCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                float radius = burst.Radius;
                if (!float.IsFinite(radius) ||
                    radius <= 0f ||
                    !float.IsFinite(burst.Intensity) ||
                    burst.Intensity <= 0f ||
                    !IsFiniteVector3(burst.PositionWS))
                {
                    continue;
                }

                float radiusSq = radius * radius;
                float distanceSq = (playerPositionWS - burst.PositionWS).sqrMagnitude;
                if (!math.isfinite(distanceSq) || distanceSq > radiusSq)
                    continue;

                float exposure01 = (1f - math.saturate(distanceSq / math.max(0.001f, radiusSq))) * burst.Intensity;
                if (!math.isfinite(exposure01) || exposure01 <= strongestExposure)
                    continue;

                strongestExposure = exposure01;
                strongestPosition = burst.PositionWS;
            }

            if (strongestExposure <= 0f)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            HectonHazardManager.Register(
                DefensiveSporeHazardSourceId,
                strongestPosition,
                _defensiveSporeHazardIntensity * strongestExposure,
                _defensiveSporeBurstRadius,
                HazardType.Toxicity,
                _defensiveSporeVisorGlitchBias * strongestExposure);
        }

        private void ClearDefensiveSporeHazard()
        {
            HectonHazardManager.Unregister(DefensiveSporeHazardSourceId);
        }

        private void CompactDefensiveSporeBursts(float simulationTime)
        {
            if (_defensiveSporeBursts == null)
            {
                _defensiveSporeBurstCount = 0;
                return;
            }

            int writeIndex = 0;
            int safeCount = Mathf.Min(_defensiveSporeBurstCount, _defensiveSporeBursts.Length);
            for (int i = 0; i < safeCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                if (simulationTime >= burst.ExpireTimeSeconds)
                    continue;

                _defensiveSporeBursts[writeIndex] = burst;
                writeIndex++;
            }

            _defensiveSporeBurstCount = writeIndex;
        }

        private int FindWeakestDefensiveSporeBurstIndex(float simulationTime)
        {
            if (_defensiveSporeBursts == null || _defensiveSporeBursts.Length == 0)
                return 0;

            int bestIndex = 0;
            float weakestScore = float.MaxValue;
            int safeCount = Mathf.Min(_defensiveSporeBurstCount, _defensiveSporeBursts.Length);
            for (int i = 0; i < safeCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                float remainingLifetime = Mathf.Max(0f, burst.ExpireTimeSeconds - simulationTime);
                float score = remainingLifetime * Mathf.Max(0.01f, burst.Intensity);
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private void UpdateBioluminescentCascades(Vector3 playerPositionWS, float deltaTime)
        {
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            if (vegetationBridge == null ||
                !TryResolveCascadeReactiveTemplateMask(out NativeArray<byte> cascadeReactiveTemplateMask) ||
                cascadeReactiveTemplateMask.Length == 0)
                return;

            _cascadeSpatialRefreshTimer -= deltaTime;
            if (_cascadeSpatialRefreshTimer <= 0f)
            {
                RefreshReactiveFloraSpatialHashes();
                _cascadeSpatialRefreshTimer = _cascadeSpatialRefreshIntervalSeconds;
            }

            TryTriggerCascadeInLane(playerPositionWS, underwater: true);
            TryTriggerCascadeInLane(playerPositionWS, underwater: false);
        }

        private void RefreshReactiveFloraSpatialHashes()
        {
            RebuildReactiveFloraSpatialHash(underwater: false);
            RebuildReactiveFloraSpatialHash(underwater: true);
        }

        private void RebuildReactiveFloraSpatialHash(bool underwater)
        {
            if (_vegetationBridge == null)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);

            HectonSpatialHash spatialHash = underwater ? _underwaterReactiveFloraHash : _surfaceReactiveFloraHash;
            if (!TryAcquireRegisteredReactiveFloraHandles(underwater, out NativeArray<int> registeredHandles))
                return;

            bool registeredHandlesReleased = false;
            try
            {
                int registeredHandleCount = underwater ? _underwaterReactiveFloraHandleCount : _surfaceReactiveFloraHandleCount;
                int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;

                EnsureReactiveFloraHashCapacity(ref spatialHash, count, allowCreate: false);
                if (spatialHash == null)
                    return;

                ClearRegisteredReactiveFloraHandles(spatialHash, registeredHandles, registeredHandleCount);
                registeredHandleCount = 0;

                if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                {
                    QueueCascadePhaseSeedRelease(underwater);
                    if (underwater)
                        _underwaterReactiveFloraHash = spatialHash;
                    else
                        _surfaceReactiveFloraHash = spatialHash;
                    return;
                }

                int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
                for (int i = 0; i < safeCount; i++)
                {
                    if (!IsReactiveCascadeTemplate(metadata[i]))
                        continue;

                    if (metadata[i].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                        math.abs(metadata[i].HeightScale) <= 0.0001f)
                    {
                        continue;
                    }

                    if (registeredHandleCount >= registeredHandles.Length)
                        break;

                    Vector3 positionWS = ExtractTranslation(matrices[i]);
                    if (!IsFiniteVector3(positionWS))
                        continue;

                    float3 halfExtents = ResolveReactiveFloraHalfExtents(
                        metadata[i],
                        types.IsCreated && i < types.Length ? types[i] : 0);
                    if (!math.all(math.isfinite(halfExtents)))
                        continue;

                    if (!TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition positionAup))
                        continue;

                    int handle = spatialHash.Register(
                        positionAup,
                        halfExtents,
                        ReactiveFloraKindMask,
                        0u,
                        i);
                    if (handle > 0)
                        registeredHandles[registeredHandleCount++] = handle;
                }

                if (underwater)
                {
                    _underwaterReactiveFloraHash = spatialHash;
                    _underwaterReactiveFloraHandleCount = registeredHandleCount;
                }
                else
                {
                    _surfaceReactiveFloraHash = spatialHash;
                    _surfaceReactiveFloraHandleCount = registeredHandleCount;
                }

                ReleaseRegisteredReactiveFloraHandlesWriteLock(underwater);
                registeredHandlesReleased = true;

                if (!EnsureCascadePhaseSeedResources(underwater, safeCount))
                    return;

                if (eventCount > 0)
                {
                    RecomputeCascadePhaseSeeds(underwater, matrices, metadata, safeCount);
                }
                else
                {
                    ClearCascadePhaseSeeds(underwater, safeCount);
                    QueueCascadePhaseSeedUpload(underwater, safeCount);
                }
            }
            finally
            {
                if (!registeredHandlesReleased)
                    ReleaseRegisteredReactiveFloraHandlesWriteLock(underwater);
            }
        }

        private void TryTriggerCascadeInLane(Vector3 playerPositionWS, bool underwater)
        {
            if (!IsFiniteVector3(playerPositionWS))
                return;

            HectonSpatialHash spatialHash = underwater ? _underwaterReactiveFloraHash : _surfaceReactiveFloraHash;
            if (spatialHash == null || !TryAcquireReactiveFloraQueryHandles(out NativeArray<int> queryHandles))
                return;

            bool queryHandlesReleased = false;
            try
            {
                NativeArray<Matrix4x4> matrices;
                NativeArray<HectonVegetationInstanceData> metadata;
                NativeArray<int> types;
                int count;
                bool hasPayload = underwater
                    ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                    : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                    return;

                if (!TryResolveAupFromRuntimeOrigin(playerPositionWS, out AbsoluteUniversePosition playerPositionAup))
                    return;

                int queryCount = spatialHash.CollectSphere(
                    playerPositionAup,
                    _cascadeContactRadius,
                    ReactiveFloraKindMask,
                    queryHandles);

                int lastContactPayloadIndex = underwater ? _lastUnderwaterPlayerContactPayloadIndex : _lastSurfacePlayerContactPayloadIndex;
                int nearestPayloadIndex = -1;
                float nearestDistanceSq = float.MaxValue;
                for (int i = 0; i < queryCount; i++)
                {
                    if (!spatialHash.TryGetEntry(queryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                        continue;

                    int payloadIndex = entry.PayloadId;
                    if (payloadIndex < 0 || payloadIndex >= count || payloadIndex >= matrices.Length || payloadIndex >= metadata.Length)
                        continue;

                    Vector3 positionWS = ExtractTranslation(matrices[payloadIndex]);
                    if (!IsFiniteVector3(positionWS))
                        continue;

                    float distanceSq = (positionWS - playerPositionWS).sqrMagnitude;
                    if (distanceSq >= nearestDistanceSq)
                        continue;

                    nearestDistanceSq = distanceSq;
                    nearestPayloadIndex = payloadIndex;
                }

                if (nearestPayloadIndex < 0)
                {
                    if (lastContactPayloadIndex >= 0 && lastContactPayloadIndex < metadata.Length)
                        SetPlayerContactRuntimeFlag(metadata, lastContactPayloadIndex, false);

                    if (underwater)
                        _lastUnderwaterPlayerContactPayloadIndex = -1;
                    else
                        _lastSurfacePlayerContactPayloadIndex = -1;
                    return;
                }

                if (lastContactPayloadIndex >= 0 &&
                    lastContactPayloadIndex != nearestPayloadIndex &&
                    lastContactPayloadIndex < metadata.Length)
                {
                    SetPlayerContactRuntimeFlag(metadata, lastContactPayloadIndex, false);
                }

                SetPlayerContactRuntimeFlag(metadata, nearestPayloadIndex, true);
                if (underwater)
                    _lastUnderwaterPlayerContactPayloadIndex = nearestPayloadIndex;
                else
                    _lastSurfacePlayerContactPayloadIndex = nearestPayloadIndex;

                float simulationTime = GetCurrentSimulationTimeSeconds();
                float lastTriggerTime = underwater ? _lastUnderwaterCascadeTriggerTime : _lastSurfaceCascadeTriggerTime;
                int lastSourcePayloadIndex = underwater ? _lastUnderwaterCascadeSourcePayloadIndex : _lastSurfaceCascadeSourcePayloadIndex;
                if (nearestPayloadIndex == lastSourcePayloadIndex &&
                    simulationTime < lastTriggerTime + _cascadeRetriggerCooldownSeconds)
                {
                    return;
                }

                Vector3 sourcePositionWS = ExtractTranslation(matrices[nearestPayloadIndex]);
                if (!IsFiniteVector3(sourcePositionWS))
                    return;

                if (!TryResolveAupFromRuntimeOrigin(sourcePositionWS, out AbsoluteUniversePosition sourcePositionAup))
                    return;

                spatialHash.CollectSphere(
                    sourcePositionAup,
                    _cascadePropagationRadius,
                    ReactiveFloraKindMask,
                    queryHandles);

                ReleaseReactiveFloraQueryHandlesWriteLock();
                queryHandlesReleased = true;

                if (!RegisterCascadeEvent(underwater, sourcePositionWS, simulationTime))
                    return;

                RecomputeCascadePhaseSeeds(underwater, matrices, metadata, count);

                if (underwater)
                {
                    _lastUnderwaterCascadeTriggerTime = simulationTime;
                    _lastUnderwaterCascadeSourcePayloadIndex = nearestPayloadIndex;
                }
                else
                {
                    _lastSurfaceCascadeTriggerTime = simulationTime;
                    _lastSurfaceCascadeSourcePayloadIndex = nearestPayloadIndex;
                }
            }
            finally
            {
                if (!queryHandlesReleased)
                    ReleaseReactiveFloraQueryHandlesWriteLock();
            }
        }

        private bool RegisterCascadeEvent(bool underwater, Vector3 centerWS, float simulationTime)
        {
            if (HasPendingCascadePhaseSeedJob(underwater))
                return false;

            if (!TryAcquireCascadeEvents(underwater, out NativeArray<FloraCascadeEventPayload> cascadeEvents))
                return false;

            int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;
            try
            {
                CompactCascadeEvents(cascadeEvents, ref eventCount, simulationTime, _cascadeReleaseDurationSeconds);

                int writeIndex = eventCount < cascadeEvents.Length
                    ? eventCount
                    : FindOldestCascadeEventIndex(cascadeEvents, eventCount);

                cascadeEvents[writeIndex] = new FloraCascadeEventPayload
                {
                    Center = new float3(centerWS.x, centerWS.y, centerWS.z),
                    StartTimeSeconds = simulationTime,
                    RadiusMeters = _cascadePropagationRadius
                };

                eventCount = Mathf.Min(cascadeEvents.Length, Mathf.Max(eventCount, writeIndex + 1));
                if (underwater)
                    _underwaterCascadeEventCount = eventCount;
                else
                    _surfaceCascadeEventCount = eventCount;

                return true;
            }
            finally
            {
                ReleaseCascadeEventsWriteLock(underwater);
            }
        }

        private void RecomputeCascadePhaseSeeds(
            bool underwater,
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            int count)
        {
            if (HasPendingCascadePhaseSeedJob(underwater) ||
                !TryResolveCascadeReactiveTemplateMask(out NativeArray<byte> cascadeReactiveTemplateMask))
                return;

            int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;
            if (!TryAcquireCascadeEvents(underwater, out NativeArray<FloraCascadeEventPayload> cascadeEvents))
                return;

            try
            {
                CompactCascadeEvents(cascadeEvents, ref eventCount, GetCurrentSimulationTimeSeconds(), _cascadeReleaseDurationSeconds);
                if (underwater)
                    _underwaterCascadeEventCount = eventCount;
                else
                    _surfaceCascadeEventCount = eventCount;
            }
            finally
            {
                ReleaseCascadeEventsWriteLock(underwater);
            }

            if (HasPendingCascadePhaseSeedJob(underwater) ||
                !TryAcquireCascadePhaseSeeds(underwater, count, out NativeArray<float> phaseSeeds))
                return;

            try
            {
                if (!phaseSeeds.IsCreated)
                    return;

                int safeCount = math.min(count, phaseSeeds.Length);
                if (safeCount <= 0)
                    return;

                if (eventCount <= 0)
                {
                    ClearCascadePhaseSeeds(phaseSeeds, safeCount);
                    QueueCascadePhaseSeedUpload(underwater, safeCount);
                    return;
                }

                if (!TryResolveCascadeEvents(underwater, out NativeArray<FloraCascadeEventPayload> cascadeEventsRead))
                    return;

                PopulateCascadePhaseSeedsJob job = new PopulateCascadePhaseSeedsJob
                {
                    Matrices = matrices,
                    Metadata = metadata,
                    ReactiveTemplateMask = cascadeReactiveTemplateMask,
                    Events = cascadeEventsRead,
                    EventCount = eventCount,
                    PropagationSpeedMetersPerSecond = _cascadePropagationSpeed,
                    InactiveSeed = InactiveCascadeSeed,
                    PhaseSeeds = phaseSeeds
                };
                ScheduleCascadePhaseSeedJob(underwater, job, safeCount);
            }
            finally
            {
                ReleaseCascadePhaseSeedsWriteLock(underwater);
            }
        }

        private static void ClearCascadePhaseSeeds(NativeArray<float> phaseSeeds, int count)
        {
            if (!phaseSeeds.IsCreated)
                return;

            int safeCount = math.min(count, phaseSeeds.Length);
            for (int i = 0; i < safeCount; i++)
                phaseSeeds[i] = InactiveCascadeSeed;
        }

        private void ClearCascadePhaseSeeds(bool underwater, int count)
        {
            if (!TryAcquireCascadePhaseSeeds(underwater, count, out NativeArray<float> phaseSeeds))
                return;

            try
            {
                ClearCascadePhaseSeeds(phaseSeeds, count);
            }
            finally
            {
                ReleaseCascadePhaseSeedsWriteLock(underwater);
            }
        }

        private void UploadCascadePhaseSeedBuffer(bool underwater, int count)
        {
            if (!TryResolveCascadePhaseSeeds(underwater, out NativeArray<float> phaseSeeds) || count <= 0)
            {
                ReleaseCascadePhaseSeedChannel(underwater);
                return;
            }

            int safeCount = math.min(count, phaseSeeds.Length);
            GraphicsBuffer phaseSeedBuffer = underwater ? _underwaterCascadePhaseSeedBuffer : _surfaceCascadePhaseSeedBuffer;
            if (phaseSeedBuffer == null || phaseSeedBuffer.count < safeCount)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(phaseSeedBuffer, phaseSeeds, safeCount);
            _vegetationBridge.BindReactivePhaseSeedBuffer(underwater, phaseSeedBuffer);
        }

        private bool EnsureCascadePhaseSeedResources(bool underwater, int count)
        {
            if (HasPendingCascadePhaseSeedJob(underwater))
                return false;

            if (count <= 0)
            {
                QueueCascadePhaseSeedRelease(underwater);
                return true;
            }

            int safeCount = math.min(count, CascadePhaseSeedCapacity);
            return TryResolveCascadePhaseSeeds(underwater, out NativeArray<float> phaseSeeds) &&
                   phaseSeeds.Length >= safeCount;
        }

        private void QueueCascadePhaseSeedUpload(bool underwater, int count)
        {
            int safeCount = math.max(0, count);
            if (safeCount <= 0)
            {
                QueueCascadePhaseSeedRelease(underwater);
                return;
            }

            if (underwater)
            {
                _underwaterCascadePhaseSeedReleaseQueued = false;
                _underwaterCascadePhaseSeedQueuedUploadCount = math.max(_underwaterCascadePhaseSeedQueuedUploadCount, safeCount);
                return;
            }

            _surfaceCascadePhaseSeedReleaseQueued = false;
            _surfaceCascadePhaseSeedQueuedUploadCount = math.max(_surfaceCascadePhaseSeedQueuedUploadCount, safeCount);
        }

        private void QueueCascadePhaseSeedRelease(bool underwater)
        {
            if (underwater)
            {
                _underwaterCascadePhaseSeedReleaseQueued = true;
                _underwaterCascadePhaseSeedQueuedUploadCount = 0;
                return;
            }

            _surfaceCascadePhaseSeedReleaseQueued = true;
            _surfaceCascadePhaseSeedQueuedUploadCount = 0;
        }

        private void FlushQueuedCascadePhaseSeedVisualSync(bool underwater)
        {
            if (underwater)
            {
                if (_underwaterCascadePhaseSeedReleaseQueued)
                {
                    if (!ReleaseCascadePhaseSeedChannel(underwater: true))
                        return;

                    _underwaterCascadePhaseSeedReleaseQueued = false;
                    return;
                }

                int uploadCount = _underwaterCascadePhaseSeedQueuedUploadCount;
                if (uploadCount <= 0 || HasPendingCascadePhaseSeedJob(underwater: true))
                    return;

                _underwaterCascadePhaseSeedQueuedUploadCount = 0;
                UploadCascadePhaseSeedBuffer(underwater: true, uploadCount);
                return;
            }

            if (_surfaceCascadePhaseSeedReleaseQueued)
            {
                if (!ReleaseCascadePhaseSeedChannel(underwater: false))
                    return;

                _surfaceCascadePhaseSeedReleaseQueued = false;
                return;
            }

            int surfaceUploadCount = _surfaceCascadePhaseSeedQueuedUploadCount;
            if (surfaceUploadCount <= 0 || HasPendingCascadePhaseSeedJob(underwater: false))
                return;

            _surfaceCascadePhaseSeedQueuedUploadCount = 0;
            UploadCascadePhaseSeedBuffer(underwater: false, surfaceUploadCount);
        }

        private bool ReleaseCascadePhaseSeedChannel(bool underwater, bool forceComplete = false)
        {
            if (!CompleteCascadePhaseSeedJob(underwater, forceComplete, uploadAfterComplete: false))
                return false;

            if (_vegetationBridge != null)
                _vegetationBridge.BindReactivePhaseSeedBuffer(underwater, null);

            if (underwater)
            {
                return true;
            }

            return true;
        }

        private bool HasPendingCascadePhaseSeedJob(bool underwater)
        {
            return underwater ? _underwaterCascadePhaseSeedScheduled : _surfaceCascadePhaseSeedScheduled;
        }

        private void ScheduleCascadePhaseSeedJob(bool underwater, PopulateCascadePhaseSeedsJob job, int count)
        {
            int safeCount = math.max(0, count);
            if (safeCount <= 0)
                return;

            JobHandle handle = job.Schedule(safeCount, CascadePhaseSeedJobBatchSize);
            if (underwater)
            {
                _underwaterCascadePhaseSeedHandle = handle;
                _underwaterCascadePhaseSeedUploadCount = safeCount;
                _underwaterCascadePhaseSeedScheduled = true;
            }
            else
            {
                _surfaceCascadePhaseSeedHandle = handle;
                _surfaceCascadePhaseSeedUploadCount = safeCount;
                _surfaceCascadePhaseSeedScheduled = true;
            }

            JobHandle.ScheduleBatchedJobs();
        }

        private bool CompleteCascadePhaseSeedJob(bool underwater, bool forceComplete, bool uploadAfterComplete)
        {
            if (underwater)
            {
                if (!_underwaterCascadePhaseSeedScheduled)
                    return true;

                if (!DispatcherJobSwap.TryComplete(ref _underwaterCascadePhaseSeedHandle, forceComplete))
                    return false;

                _underwaterCascadePhaseSeedScheduled = false;
                int uploadCount = _underwaterCascadePhaseSeedUploadCount;
                _underwaterCascadePhaseSeedUploadCount = 0;
                if (uploadAfterComplete && uploadCount > 0)
                    UploadCascadePhaseSeedBuffer(underwater: true, uploadCount);

                return true;
            }

            if (!_surfaceCascadePhaseSeedScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _surfaceCascadePhaseSeedHandle, forceComplete))
                return false;

            _surfaceCascadePhaseSeedScheduled = false;
            int surfaceUploadCount = _surfaceCascadePhaseSeedUploadCount;
            _surfaceCascadePhaseSeedUploadCount = 0;
            if (uploadAfterComplete && surfaceUploadCount > 0)
                UploadCascadePhaseSeedBuffer(underwater: false, surfaceUploadCount);

            return true;
        }

        private void EnsureReactiveFloraHashCapacity(ref HectonSpatialHash spatialHash, int count, bool allowCreate)
        {
            int safeCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count));
            if (spatialHash != null)
                return;

            if (!allowCreate)
                return;

            spatialHash = new HectonSpatialHash(safeCapacity, safeCapacity * 4);
        }

        private static void ClearRegisteredReactiveFloraHandles(
            HectonSpatialHash spatialHash,
            NativeArray<int> registeredHandles,
            int registeredHandleCount)
        {
            if (spatialHash == null || !registeredHandles.IsCreated)
                return;

            int safeCount = math.min(math.max(0, registeredHandleCount), registeredHandles.Length);
            for (int i = 0; i < safeCount; i++)
                spatialHash.Unregister(registeredHandles[i]);
        }

        private static float3 ResolveReactiveFloraHalfExtents(HectonVegetationInstanceData instanceData, int instanceType)
        {
            float width01 = math.saturate(math.abs(instanceData.WidthScale));
            float height01 = math.saturate(math.abs(instanceData.HeightScale));
            float width = math.lerp(0.45f, 2.2f, width01);
            float height = instanceType == (int)HectonVegetationInstanceType.GiantKelp
                ? math.lerp(2.4f, 10f, height01)
                : math.lerp(0.45f, 2.4f, height01);
            return new float3(width, math.max(0.35f, height * 0.5f), width);
        }

        private bool IsReactiveCascadeTemplate(HectonVegetationInstanceData instanceData)
        {
            if (!TryResolveCascadeReactiveTemplateMask(out NativeArray<byte> cascadeReactiveTemplateMask))
                return false;

            int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
            return templateIndex >= 0 &&
                   templateIndex < cascadeReactiveTemplateMask.Length &&
                   cascadeReactiveTemplateMask[templateIndex] != 0;
        }

        private bool IsDefensiveSporeBurstTemplate(HectonVegetationInstanceData instanceData)
        {
            if (!TryResolveDefensiveSporeBurstTemplateMask(out NativeArray<byte> defensiveSporeBurstTemplateMask))
                return false;

            int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
            return templateIndex >= 0 &&
                   templateIndex < defensiveSporeBurstTemplateMask.Length &&
                   defensiveSporeBurstTemplateMask[templateIndex] != 0;
        }

        internal bool IsDefensiveSporeBurstTemplateIndex(int templateIndex)
        {
            return TryResolveDefensiveSporeBurstTemplateMask(out NativeArray<byte> defensiveSporeBurstTemplateMask) &&
                   templateIndex >= 0 &&
                   templateIndex < defensiveSporeBurstTemplateMask.Length &&
                   defensiveSporeBurstTemplateMask[templateIndex] != 0;
        }

        private void RefreshCascadeTemplateMask(bool force)
        {
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (force)
                return;

            if (_cachedCascadeReactiveTemplateCount == templateCount &&
                TryResolveCascadeReactiveTemplateMask(out NativeArray<byte> cascadeReactiveTemplateMask) &&
                cascadeReactiveTemplateMask.Length == templateCount)
                return;
        }

        private void RebuildCascadeTemplateMaskColdFromCurrentBridge()
        {
            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            CacheStableHashIds(_cascadeReactiveStableIds, ref _cascadeReactiveStableHashIds);
            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            RebuildCascadeTemplateMaskCold(floraTemplates, templateCount);
        }

        private void RebuildCascadeTemplateMaskCold(FloraDataTemplate[] floraTemplates, int templateCount)
        {
            _cachedCascadeReactiveTemplateCount = templateCount;
            if (templateCount <= 0 || _cascadeReactiveStableHashIds.Length == 0)
            {
                ReleaseFloraVaultBuffer(ref _cascadeReactiveTemplateMaskHandle);
                return;
            }

            if (!TryEnsureFloraVaultBuffer(
                    _wakeDataVault,
                    ref _cascadeReactiveTemplateMaskHandle,
                    FloraCascadeReactiveTemplateMaskBufferId,
                    templateCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> _))
                return;

            if (!TryAcquireFloraVaultWriteBuffer(
                    ref _cascadeReactiveTemplateMaskHandle,
                    FloraCascadeReactiveTemplateMaskBufferId,
                    templateCount,
                    out NativeArray<byte> cascadeReactiveTemplateMask))
                return;

            try
            {
                for (int i = 0; i < cascadeReactiveTemplateMask.Length; i++)
                    cascadeReactiveTemplateMask[i] = 0;

                for (int i = 0; i < templateCount; i++)
                {
                    FloraDataTemplate template = floraTemplates[i];
                    if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                        continue;

                    cascadeReactiveTemplateMask[i] = MatchesStableHash(LocHash.Compute(template.StableId), _cascadeReactiveStableHashIds) ? (byte)1 : (byte)0;
                }
            }
            finally
            {
                ReleaseFloraVaultWriteLock(in _cascadeReactiveTemplateMaskHandle);
            }
        }

        private void RefreshDefensiveSporeBurstTemplateMask(bool force)
        {
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (force)
                return;

            if (_cachedDefensiveSporeBurstTemplateCount == templateCount &&
                TryResolveDefensiveSporeBurstTemplateMask(out NativeArray<byte> defensiveSporeBurstTemplateMask) &&
                defensiveSporeBurstTemplateMask.Length == templateCount)
                return;
        }

        private void RebuildDefensiveSporeBurstTemplateMaskColdFromCurrentBridge()
        {
            if (_vegetationBridge == null)
                RefreshVegetationBridgeFromCachedService();

            CacheStableHashIds(_defensiveSporeBurstStableIds, ref _defensiveSporeBurstStableHashIds);
            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            RebuildDefensiveSporeBurstTemplateMaskCold(floraTemplates, templateCount);
        }

        private void RebuildDefensiveSporeBurstTemplateMaskCold(FloraDataTemplate[] floraTemplates, int templateCount)
        {
            _cachedDefensiveSporeBurstTemplateCount = templateCount;
            if (templateCount <= 0 || _defensiveSporeBurstStableHashIds.Length == 0)
            {
                ReleaseFloraVaultBuffer(ref _defensiveSporeBurstTemplateMaskHandle);
                return;
            }

            if (!TryEnsureFloraVaultBuffer(
                    _wakeDataVault,
                    ref _defensiveSporeBurstTemplateMaskHandle,
                    FloraDefensiveSporeTemplateMaskBufferId,
                    templateCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> _))
                return;

            if (!TryAcquireFloraVaultWriteBuffer(
                    ref _defensiveSporeBurstTemplateMaskHandle,
                    FloraDefensiveSporeTemplateMaskBufferId,
                    templateCount,
                    out NativeArray<byte> defensiveSporeBurstTemplateMask))
                return;

            try
            {
                for (int i = 0; i < defensiveSporeBurstTemplateMask.Length; i++)
                    defensiveSporeBurstTemplateMask[i] = 0;

                for (int i = 0; i < templateCount; i++)
                {
                    FloraDataTemplate template = floraTemplates[i];
                    if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                        continue;

                    defensiveSporeBurstTemplateMask[i] = MatchesStableHash(LocHash.Compute(template.StableId), _defensiveSporeBurstStableHashIds) ? (byte)1 : (byte)0;
                }
            }
            finally
            {
                ReleaseFloraVaultWriteLock(in _defensiveSporeBurstTemplateMaskHandle);
            }
        }

        private static void CompactCascadeEvents(
            NativeArray<FloraCascadeEventPayload> cascadeEvents,
            ref int eventCount,
            float simulationTime,
            float activeLifetimeSeconds)
        {
            if (!cascadeEvents.IsCreated)
            {
                eventCount = 0;
                return;
            }

            int writeIndex = 0;
            int safeCount = math.min(eventCount, cascadeEvents.Length);
            for (int i = 0; i < safeCount; i++)
            {
                FloraCascadeEventPayload cascadeEvent = cascadeEvents[i];
                if (simulationTime >= cascadeEvent.StartTimeSeconds + activeLifetimeSeconds)
                    continue;

                cascadeEvents[writeIndex] = cascadeEvent;
                writeIndex++;
            }

            eventCount = writeIndex;
        }

        private static int FindOldestCascadeEventIndex(NativeArray<FloraCascadeEventPayload> cascadeEvents, int eventCount)
        {
            if (!cascadeEvents.IsCreated || eventCount <= 0)
                return 0;

            int bestIndex = 0;
            float oldestTime = float.MaxValue;
            int safeCount = math.min(eventCount, cascadeEvents.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (cascadeEvents[i].StartTimeSeconds >= oldestTime)
                    continue;

                oldestTime = cascadeEvents[i].StartTimeSeconds;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void CacheStableHashIds(string[] stableIds, ref int[] hashIds)
        {
            int count = stableIds != null ? stableIds.Length : 0;
            if (hashIds == null || hashIds.Length != count)
                hashIds = new int[count];

            for (int i = 0; i < count; i++)
                hashIds[i] = string.IsNullOrWhiteSpace(stableIds[i]) ? 0 : LocHash.Compute(stableIds[i]);
        }

        private static bool MatchesStableHash(int stableHashId, int[] resolvedHashes)
        {
            if (stableHashId == 0 || resolvedHashes == null)
                return false;

            for (int i = 0; i < resolvedHashes.Length; i++)
            {
                if (resolvedHashes[i] == stableHashId)
                    return true;
            }

            return false;
        }

        private static void SetPlayerContactRuntimeFlag(NativeArray<HectonVegetationInstanceData> metadata, int index, bool enabled)
        {
            if (!metadata.IsCreated || index < 0 || index >= metadata.Length)
                return;

            HectonVegetationInstanceData instanceData = metadata[index];
            byte packedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(instanceData.RuntimeFlags);
            if (enabled)
                packedFlags |= (byte)HectonVegetationRuntimeFlags.PlayerContact;
            else
                packedFlags &= unchecked((byte)~(byte)HectonVegetationRuntimeFlags.PlayerContact);

            instanceData.RuntimeFlags = HectonVegetationRuntimeFlagEncoding.WithRuntimeFlags(
                instanceData.RuntimeFlags,
                packedFlags);
            metadata[index] = instanceData;
        }

        private void ClearReactiveFloraSpatialState(bool forceCompleteJobs)
        {
            if (_surfaceReactiveFloraHash != null &&
                TryResolveRegisteredReactiveFloraHandles(underwater: false, out NativeArray<int> surfaceHandles))
            {
                ClearRegisteredReactiveFloraHandles(_surfaceReactiveFloraHash, surfaceHandles, _surfaceReactiveFloraHandleCount);
                _surfaceReactiveFloraHandleCount = 0;
            }

            if (_underwaterReactiveFloraHash != null &&
                TryResolveRegisteredReactiveFloraHandles(underwater: true, out NativeArray<int> underwaterHandles))
            {
                ClearRegisteredReactiveFloraHandles(_underwaterReactiveFloraHash, underwaterHandles, _underwaterReactiveFloraHandleCount);
                _underwaterReactiveFloraHandleCount = 0;
            }

            ReleaseCascadePhaseSeedChannel(underwater: false, forceCompleteJobs);
            ReleaseCascadePhaseSeedChannel(underwater: true, forceCompleteJobs);
            _surfaceCascadeEventCount = 0;
            _underwaterCascadeEventCount = 0;
            _lastSurfaceCascadeSourcePayloadIndex = -1;
            _lastUnderwaterCascadeSourcePayloadIndex = -1;
            _lastSurfacePlayerContactPayloadIndex = -1;
            _lastUnderwaterPlayerContactPayloadIndex = -1;
            _defensiveSporeBurstCount = 0;
        }

        private static void EnsureStructuredFloatBuffer(ref GraphicsBuffer buffer, int requiredCount)
        {
            if (requiredCount <= 0)
            {
                ReleaseGraphicsBuffer(ref buffer);
                return;
            }

            if (buffer != null && buffer.count >= requiredCount)
                return;

            ReleaseGraphicsBuffer(ref buffer);
            buffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(requiredCount); // COLD ALLOC: GraphicsBuffer[instanceCount] - per-instance cascade phase seed GPU buffer - owner: FloraInteractionManager
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private void PublishPredatorThreatGlobals(Vector3 samplePositionWS)
        {
            float aggressiveBioformThreat = 0f;
            Vector4 predatorThreatPositionRadius = Vector4.zero;
            float predatorQueryRadius = Mathf.Max(15f, _predatorThreatQueryRadius);
            float predatorDimRadius = Mathf.Max(15f, _predatorBiolumDimRadius);
            if (_predatorThreatQueryHits != null)
            {
                int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                    samplePositionWS,
                    predatorQueryRadius,
                    SpatialTargetKind.Bioform,
                    _predatorThreatQueryHits);
                float bestDistanceSqr = predatorQueryRadius * predatorQueryRadius;
                for (int i = 0; i < hitCount; i++)
                {
                    SpatialQueryHit hit = _predatorThreatQueryHits[i];
                    if (hit.Transform == null || hit.Transform == _playerTransform)
                        continue;

                    if (_predatorThreatMask.value != 0 && (_predatorThreatMask.value & (1 << hit.Layer)) == 0)
                        continue;

                    bool leviathanThreat = hit.Transform.CompareTag("Leviathan");
                    if (!leviathanThreat && hit.Owner is IFaunaSpatialContact faunaContact)
                        leviathanThreat = faunaContact.IsLeviathanContact;
                    if (!leviathanThreat)
                        continue;

                    PublishApexPredatorWakeSignal(in hit);

                    if (hit.DistanceSqr >= bestDistanceSqr)
                        continue;

                    bestDistanceSqr = hit.DistanceSqr;
                    float predatorDimRadiusSq = predatorDimRadius * predatorDimRadius;
                    aggressiveBioformThreat = 1f - math.saturate(hit.DistanceSqr * math.rcp(math.max(0.001f, predatorDimRadiusSq)));
                    predatorThreatPositionRadius = new Vector4(hit.Position.x, hit.Position.y, hit.Position.z, predatorDimRadius);
                }
            }

            float bridgeThreat = _vegetationBridge != null ? Mathf.Clamp01(_vegetationBridge.GetThreatLevel(samplePositionWS)) : 0f;
            float threatExposure = Mathf.Max(aggressiveBioformThreat, bridgeThreat);
            Shader.SetGlobalVector(
                _FloraPredatorThreatParamsId,
                new Vector4(
                    threatExposure,
                    predatorDimRadius,
                    _predatorBiolumDimStrength,
                    aggressiveBioformThreat));
            Shader.SetGlobalVector(_FloraPredatorThreatPositionRadiusId, predatorThreatPositionRadius);
        }

        private void PublishApexPredatorWakeSignal(in SpatialQueryHit hit)
        {
            Rigidbody predatorBody = hit.Rigidbody;
            if (predatorBody == null)
                return;

            Vector3 velocity = predatorBody.linearVelocity;
            if (!IsFiniteVector3(velocity))
                return;

            float speed = EstimateLength3D(velocity);
            if (speed < ApexPredatorWakeMinSpeed)
                return;

            AbsoluteUniversePosition predatorAup;
            if (hit.HasAbsolutePosition)
            {
                predatorAup = hit.AbsolutePosition;
            }
            else
            {
                if (!IsFiniteVector3(hit.Position))
                    return;

                if (!TryResolveAupFromRuntimeOrigin(hit.Position, out predatorAup))
                    return;
            }

            PublishWakeGeneratedSignal(predatorAup, velocity, WakeSourceApexPredator);
        }

        private void RefreshFlowFieldGlobals(float deltaTime)
        {
            _flowFieldUploadTimer -= deltaTime;
            HectonMapMagicVegetationBridge vegetationBridge = GetCachedVegetationBridgeOrRequestColdResolve();

            if (vegetationBridge == null)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            bool hasPayload = vegetationBridge.TryGetEcosystemFlowFieldPayload(
                out NativeArray<float2>.ReadOnly flowVectors,
                out int gridResolution,
                out Vector3 gridCenter,
                out float cellSize);
            if (!hasPayload)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            _flowFieldResolution = gridResolution;
            _flowFieldCellSize = cellSize;
            _flowFieldCenterWS = gridCenter;

            float recenterThreshold = math.max(0.01f, cellSize * FlowFieldRecenterThresholdCells);
            bool forceUpload =
                _activeFlowFieldBuffer == null ||
                _flowFieldUploadTimer <= 0f ||
                _lastUploadedFlowFieldCenterWS == Vector3.zero ||
                (gridCenter - _lastUploadedFlowFieldCenterWS).sqrMagnitude >= recenterThreshold * recenterThreshold;

            if (forceUpload)
            {
                int requiredCount = math.max(1, flowVectors.Length);
                if (_flowFieldBufferA == null || _flowFieldBufferA.count != requiredCount ||
                    _flowFieldBufferB == null || _flowFieldBufferB.count != requiredCount)
                {
                    ReleaseFlowFieldBuffer();
                    _flowFieldBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(requiredCount); // COLD ALLOC: GraphicsBuffer[flowVectors.Length] - authoritative ecosystem flow-field GPU staging A for flora shading - owner: FloraInteractionManager
                    _flowFieldBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(requiredCount); // COLD ALLOC: GraphicsBuffer[flowVectors.Length] - authoritative ecosystem flow-field GPU staging B for flora shading - owner: FloraInteractionManager
                    _activeFlowFieldBuffer = _flowFieldBufferA;
                    _flowFieldUploadBufferIndex = 1;
                }

                GraphicsBuffer writeBuffer = (_flowFieldUploadBufferIndex & 1) == 0 ? _flowFieldBufferA : _flowFieldBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return;

                if (!vegetationBridge.TryUploadEcosystemFlowFieldPayload(writeBuffer, requiredCount))
                    return;

                _activeFlowFieldBuffer = writeBuffer;
                _flowFieldUploadBufferIndex ^= 1;
                _lastUploadedFlowFieldCenterWS = gridCenter;
                _flowFieldUploadTimer = FlowFieldUploadIntervalSeconds;
            }

            PublishFlowFieldGlobals();
        }

        private void PublishFlowFieldGlobals()
        {
            _flowFieldGlobalsDirty = true;
        }

        private void FlushFlowFieldGlobals()
        {
            if (!_flowFieldGlobalsDirty)
                return;

            _flowFieldGlobalsDirty = false;
            if (_activeFlowFieldBuffer != null)
                Shader.SetGlobalBuffer(_MarineSnowFlowFieldId, _activeFlowFieldBuffer);

            Shader.SetGlobalVector(
                _MarineSnowFlowFieldCenterCellSizeId,
                new Vector4(_flowFieldCenterWS.x, _flowFieldCenterWS.y, _flowFieldCenterWS.z, _flowFieldCellSize));
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, _flowFieldResolution);
            PublishCulledFloraGlobals();
        }

        private void PublishCulledFloraGlobals()
        {
            if (TryGetCulledFloraVisibleBuffer(out GraphicsBuffer visibleInstancesBuffer, out int visibleInstanceCount))
            {
                Shader.SetGlobalBuffer(_CulledFloraVisibleInstancesId, visibleInstancesBuffer);
                Shader.SetGlobalInt(_CulledFloraVisibleCountId, visibleInstanceCount);
                return;
            }

            Shader.SetGlobalInt(_CulledFloraVisibleCountId, 0);
        }

        public bool TryGetCulledFloraVisibleBuffer(out GraphicsBuffer visibleInstancesBuffer, out int visibleInstanceCount)
        {
            IInstanceCullingService instanceCulling = _instanceCullingService;
            if (instanceCulling != null && instanceCulling.VisibleInstancesBuffer != null)
            {
                visibleInstancesBuffer = instanceCulling.VisibleInstancesBuffer;
                visibleInstanceCount = math.max(0, instanceCulling.LastVisibleInstanceCount);
                return true;
            }

            visibleInstancesBuffer = null;
            visibleInstanceCount = 0;
            return false;
        }

        private void PublishPlayerRuntimePosition(
            Vector3 playerRuntimePosition,
            float playerBendRadius,
            float playerSpeed,
            float targetForce)
        {
            float normalizedForce = _maxInteractionForce > 0.0001f
                ? Mathf.Clamp01(targetForce / _maxInteractionForce)
                : 0f;

            _pendingPlayerRuntimePosition = new Vector4(
                playerRuntimePosition.x,
                playerRuntimePosition.y,
                playerRuntimePosition.z,
                Mathf.Max(0.05f, playerBendRadius));
            _pendingPlayerFloraInteractionParams = new Vector4(
                playerSpeed,
                normalizedForce,
                _hasActiveScooterWake ? 1f : 0f,
                1f);
            _playerRuntimePositionDirty = true;
        }

        private void FlushPlayerRuntimePosition()
        {
            if (!_playerRuntimePositionDirty)
                return;

            _playerRuntimePositionDirty = false;
            Shader.SetGlobalVector(
                _PlayerRuntimePositionId,
                _pendingPlayerRuntimePosition);
            Shader.SetGlobalVector(_PlayerFloraInteractionParamsId, _pendingPlayerFloraInteractionParams);
        }

        private Vector3 SampleGlobalOceanFlow(Vector3 samplePositionWS, IFluidSurfaceCurrentReadModel fluidReadModel)
        {
            IHectonOceanKinematics provider = _oceanKinematicsProvider;
            IDataVault dataVault = _wakeDataVault;
            if (provider != null &&
                provider.IsAvailable &&
                dataVault != null &&
                !dataVault.IsCompactionFenceActive &&
                dataVault.TryAcquireMutationGuard(FloraOceanFlowSampleMutationGuardMask))
            {
                try
                {
                    if (!TryResolveFloraVaultBuffer(
                            dataVault,
                            in _oceanFlowSamplePositionsHandle,
                            FloraOceanFlowSamplePositionsBufferId,
                            1,
                            out NativeArray<Vector3> oceanFlowSamplePositions) ||
                        !TryResolveFloraVaultBuffer(
                            dataVault,
                            in _oceanFlowSampleResultsHandle,
                            FloraOceanFlowSampleResultsBufferId,
                            1,
                            out NativeArray<Vector3> oceanFlowSampleResults))
                    {
                        return fluidReadModel != null ? fluidReadModel.CurrentVector : Vector3.zero;
                    }

                    oceanFlowSamplePositions[0] = samplePositionWS;
                    if (provider.GetSurfaceFlow(oceanFlowSamplePositions, 1, 1f, oceanFlowSampleResults))
                        return oceanFlowSampleResults[0];
                }
                finally
                {
                    dataVault.ReleaseMutationGuard(FloraOceanFlowSampleMutationGuardMask);
                }
            }

            return fluidReadModel != null ? fluidReadModel.CurrentVector : Vector3.zero;
        }

        private void CreateWakeTrailResources()
        {
            if (_wakeTrailDisabled)
                return;

            if (_wakeTrailRead == null)
                _wakeTrailRead = CreateWakeTrailTexture("__VegetationWakeTrail_A");

            if (_wakeTrailWrite == null)
                _wakeTrailWrite = CreateWakeTrailTexture("__VegetationWakeTrail_B");

            EnsureWakeTrailStampCommandBuffer(clearExisting: false);

            if (_wakeTrailStampCommandBufferA == null)
                _wakeTrailStampCommandBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WakeTrailStampCommand>(WakeTrailStampCommandCapacity); // COLD ALLOC: GraphicsBuffer[4] - queued vegetation wake-trail stamp buffer A for compute dispatch - owner: FloraInteractionManager
            if (_wakeTrailStampCommandBufferB == null)
                _wakeTrailStampCommandBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WakeTrailStampCommand>(WakeTrailStampCommandCapacity); // COLD ALLOC: GraphicsBuffer[4] - queued vegetation wake-trail stamp buffer B for compute dispatch - owner: FloraInteractionManager
            if (_activeWakeTrailStampCommandBuffer == null)
                _activeWakeTrailStampCommandBuffer = _wakeTrailStampCommandBufferA;

            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
#endif
                _wakeTrailDisabled = true;
                QueueWakeTrailGlobals();
                return;
            }

            if (_wakeTrailSimulationKernel < 0)
            {
                _wakeTrailSimulationKernel = ResolveKernel(_wakeTrailSimulationCompute, "SimulateWakeTrail");
                ResolveKernelThreadGroupSizes(
                    _wakeTrailSimulationCompute,
                    _wakeTrailSimulationKernel,
                    out _wakeTrailThreadGroupSizeX,
                    out _wakeTrailThreadGroupSizeY);
            }

            RefreshWakeTrailWorldRect(Vector3.zero, forceClear: true);
            QueueWakeTrailGlobals();
        }

        private bool HasWakeTrailResourcesReady()
        {
            return !_wakeTrailResourceRefreshRequested &&
                   !_wakeTrailDisabled &&
                   _supportsWakeTrailComputeCold &&
                   _wakeTrailRead != null &&
                   _wakeTrailWrite != null &&
                   _wakeTrailSimulationCompute != null &&
                   _wakeTrailSimulationKernel >= 0 &&
                   _wakeTrailThreadGroupSizeX > 0 &&
                   _wakeTrailThreadGroupSizeY > 0 &&
                   _wakeTrailStampCommandBufferA != null &&
                   _wakeTrailStampCommandBufferB != null &&
                   _activeWakeTrailStampCommandBuffer != null;
        }

        private void ReleaseWakeTrailResources()
        {
            ReleaseWakeTrailTexture(ref _wakeTrailRead);
            ReleaseWakeTrailTexture(ref _wakeTrailWrite);

            ReleaseGraphicsBuffer(ref _wakeTrailStampCommandBufferA);
            ReleaseGraphicsBuffer(ref _wakeTrailStampCommandBufferB);
            _activeWakeTrailStampCommandBuffer = null;
            _wakeTrailStampCommandUploadBufferIndex = 0;

            _wakeTrailStampCommandsHandle = default;

            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
            _wakeTrailDispatchSerial = 0u;
            _lastWakeTrailDispatchSerial = 0u;
            _wakeTrailThreadGroupSizeX = 0;
            _wakeTrailThreadGroupSizeY = 0;

            QueueWakeTrailGlobals();
        }

        private void UpdateWakeTrail(Vector3 playerPosition, Vector3 playerVelocity, float deltaTime)
        {
            _wakeTrailDispatchSerial++;
            if (_wakeTrailDispatchSerial == 0u)
                _wakeTrailDispatchSerial = 1u;

            if (_wakeTrailDisabled)
                return;

            _hasActiveSubmarineWake = false;
            if (!HasWakeTrailResourcesReady())
            {
                _wakeTrailResourceRefreshRequested = true;
                QueueWakeTrailGlobals();
                return;
            }

            RefreshWakeTrailWorldRect(playerPosition, forceClear: false);

            bool wrotePass = false;
            float safeDeltaTime = float.IsFinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
            float fadeSeconds = ClampFinite(_wakeTrailFadeSeconds, 12f, 0.1f, 16f);
            float fade = safeDeltaTime / fadeSeconds;
            float strongestStamp = 0f;
            float wakeTrailBaseRadius = ClampFinite(_wakeTrailBaseRadius, 1.35f, 0.5f, 4f);
            float wakeTrailMinLength = ClampFinite(_wakeTrailMinLength, 2.4f, 1f, 20f);
            float wakeTrailMaxLength = ClampFinite(_wakeTrailMaxLength, 15f, wakeTrailMinLength, 30f);
            float wakeTrailVelocityToLength = ClampFinite(_wakeTrailVelocityToLength, 0.28f, 0.05f, 0.75f);

            float playerSpeed = EstimateLength3D(playerVelocity);
            if (playerSpeed >= _wakeTrailPlayerMinSpeed)
            {
                float playerStrength = ClampFinite(_wakeTrailPlayerStrength, 0.28f, 0.1f, 2f);
                QueueWakeTrailStamp(
                    playerPosition,
                    playerVelocity,
                    wakeTrailBaseRadius,
                    Mathf.Clamp(wakeTrailMinLength + playerSpeed * wakeTrailVelocityToLength, wakeTrailMinLength, wakeTrailMaxLength),
                    playerStrength);
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, playerStrength);
            }

            float scooterSpeed = EstimateLength3D(_smoothedScooterVelocity);
            if (_hasActiveScooterWake && scooterSpeed >= _wakeTrailScooterMinSpeed)
            {
                float scooterStrength = ClampFinite(_wakeTrailScooterStrength, 0.95f, 0.25f, 2f);
                QueueWakeTrailStamp(
                    _lastPublishedScooterWakePosition,
                    _smoothedScooterVelocity,
                    wakeTrailBaseRadius * 1.15f,
                    Mathf.Clamp(wakeTrailMinLength + scooterSpeed * (wakeTrailVelocityToLength * 1.7f), wakeTrailMinLength * 1.25f, wakeTrailMaxLength),
                    scooterStrength);
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, scooterStrength);
            }

            Rigidbody submarineHull = _submarineHullRigidbody;
            if (submarineHull != null)
            {
                Vector3 submarineVelocity = submarineHull.linearVelocity;
                if (IsFiniteVector3(submarineVelocity))
                {
                    float submarineSpeed = EstimateLength3D(submarineVelocity);
                    float submarineMinSpeed = ClampFinite(
                        _wakeTrailSubmarineMinSpeed,
                        DefaultSubmarineWakeMinSpeed,
                        0.25f,
                        4f);
                    if (submarineSpeed >= submarineMinSpeed)
                    {
                        Vector3 submarinePosition = submarineHull.worldCenterOfMass;
                        if (IsFiniteVector3(submarinePosition))
                        {
                            _hasActiveSubmarineWake = true;
                            _lastPublishedSubmarineWakePosition = submarinePosition;
                            float submarineWhipSpeed = ClampFinite(
                                _wakeTrailSubmarineWhipSpeed,
                                DefaultSubmarineWakeWhipSpeed,
                                8f,
                                24f);
                            bool kelpWhipActive = submarineSpeed >= submarineWhipSpeed;
                            float submarineBaseStrength = ClampFinite(
                                _wakeTrailSubmarineStrength,
                                DefaultSubmarineWakeStrength,
                                0.25f,
                                2f);
                            float submarineStrength = kelpWhipActive
                                ? Mathf.Clamp01(submarineBaseStrength * 1.35f)
                                : Mathf.Clamp01(submarineBaseStrength);
                            float lengthScale = kelpWhipActive ? 2.35f : 1.55f;
                            float radiusScale = kelpWhipActive ? 1.55f : 1f;
                            float submarineRadius = ClampFinite(
                                _wakeTrailSubmarineRadius,
                                DefaultSubmarineWakeRadius,
                                1f,
                                6f);
                            QueueWakeTrailStamp(
                                _lastPublishedSubmarineWakePosition,
                                submarineVelocity,
                                submarineRadius * radiusScale,
                                Mathf.Clamp(wakeTrailMinLength + submarineSpeed * (wakeTrailVelocityToLength * lengthScale), wakeTrailMinLength * 1.5f, wakeTrailMaxLength * 1.6f),
                                submarineStrength);
                            wrotePass = true;
                            strongestStamp = Mathf.Max(strongestStamp, submarineStrength);
                        }
                    }
                }
            }

            if (wrotePass || _wakeTrailEnergy > 0.0001f || _pendingWakeTrailScrollUv.sqrMagnitude > 0.0000001f)
                ExecuteWakeTrailSimulation(fade);

            _wakeTrailEnergy = Mathf.Max(0f, wrotePass ? Mathf.Max(_wakeTrailEnergy - fade, strongestStamp) : (_wakeTrailEnergy - fade));
            QueueWakeTrailGlobals();
        }

        private void RefreshWakeTrailWorldRect(Vector3 anchorPosition, bool forceClear)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            if (!IsFiniteVector3(anchorPosition))
                return;

            float desiredWorldSize = ClampFinite(_wakeTrailWorldSize, 128f, 64f, 192f);
            float snapStride = ResolveWakeTrailSnapStride(desiredWorldSize);
            Vector2 desiredCenterXZ = QuantizeWakeTrailCenter(new Vector2(anchorPosition.x, anchorPosition.z), snapStride);

            bool wakeTrailRectInvalid =
                !float.IsFinite(_wakeTrailRuntimeWorldSize) ||
                !IsFiniteVector4(_wakeTrailWorldRect) ||
                _wakeTrailWorldRect.z <= 0f ||
                _wakeTrailWorldRect.w <= 0f;
            bool mustClear =
                forceClear ||
                wakeTrailRectInvalid ||
                _wakeTrailRuntimeWorldSize <= 0f ||
                Mathf.Abs(desiredWorldSize - _wakeTrailRuntimeWorldSize) > 0.001f;
            Vector2 centerDelta = desiredCenterXZ - _wakeTrailCenterXZ;
            if (!mustClear && centerDelta.sqrMagnitude <= 0.000001f)
                return;

            _wakeTrailCenterXZ = desiredCenterXZ;
            _wakeTrailRuntimeWorldSize = desiredWorldSize;
            float halfSize = desiredWorldSize * 0.5f;
            float wakeTrailInvWorldSize = math.rcp(Mathf.Max(desiredWorldSize, 0.001f));
            _wakeTrailWorldRect = new Vector4(
                desiredCenterXZ.x - halfSize,
                desiredCenterXZ.y - halfSize,
                wakeTrailInvWorldSize,
                wakeTrailInvWorldSize);

            if (mustClear)
            {
                ClearWakeTrailTextures();
                return;
            }

            QueueWakeTrailScroll(centerDelta);
        }

        private void QueueWakeTrailStamp(
            Vector3 positionWS,
            Vector3 directionWS,
            float radiusWS,
            float lengthWS,
            float strength)
        {
            if (!TryResolveWakeTrailStampCommands(out NativeArray<WakeTrailStampCommand> stampCommands) ||
                _queuedWakeTrailStampCount >= WakeTrailStampCommandCapacity ||
                !IsFiniteVector3(positionWS) ||
                !IsFiniteVector3(directionWS) ||
                !IsFiniteVector4(_wakeTrailWorldRect) ||
                _wakeTrailWorldRect.z <= 0f ||
                _wakeTrailWorldRect.w <= 0f ||
                !float.IsFinite(radiusWS) ||
                !float.IsFinite(lengthWS) ||
                !float.IsFinite(strength))
                return;

            float safeRadiusWS = Mathf.Max(0.001f, radiusWS);
            float safeLengthWS = Mathf.Max(0.001f, lengthWS);
            float safeStrength = Mathf.Clamp01(strength);
            Vector2 uvCenter = new Vector2(
                (positionWS.x - _wakeTrailWorldRect.x) * _wakeTrailWorldRect.z,
                (positionWS.z - _wakeTrailWorldRect.y) * _wakeTrailWorldRect.w);
            Vector2 directionXZ = new Vector2(directionWS.x, directionWS.z);
            float directionMagnitude = EstimateLength3D(directionWS);
            float verticalImpulse = directionMagnitude > 0.0001f
                ? Mathf.Clamp01(Mathf.Abs(directionWS.y) / directionMagnitude) * Mathf.Clamp01(directionMagnitude * 0.12f)
                : 0f;
            directionXZ = NormalizeVector2Fast(directionXZ, Vector2.up);

            float uvRadius = safeRadiusWS * _wakeTrailWorldRect.z;
            float uvLength = safeLengthWS * _wakeTrailWorldRect.z;

            stampCommands[_queuedWakeTrailStampCount] = new WakeTrailStampCommand
            {
                UvEllipse = new Vector4(uvCenter.x, uvCenter.y, uvRadius, uvLength),
                DirectionStrengthVertical = new Vector4(directionXZ.x, directionXZ.y, safeStrength, verticalImpulse)
            };
            _queuedWakeTrailStampCount++;
        }

        private void ExecuteWakeTrailSimulation(float fade)
        {
            if (_wakeTrailSimulationCompute == null ||
                _wakeTrailSimulationKernel < 0 ||
                _wakeTrailRead == null ||
                _wakeTrailWrite == null ||
                _wakeTrailStampCommandBufferA == null ||
                _wakeTrailStampCommandBufferB == null ||
                _lastWakeTrailDispatchSerial == _wakeTrailDispatchSerial)
            {
                return;
            }

            int groupCountX = CeilDividePositive(_wakeTrailRuntimeResolution, _wakeTrailThreadGroupSizeX);
            int groupCountY = CeilDividePositive(_wakeTrailRuntimeResolution, _wakeTrailThreadGroupSizeY);
            if (groupCountX <= 0 || groupCountY <= 0)
                return;

            if (_queuedWakeTrailStampCount > 0 && TryResolveWakeTrailStampCommands(out NativeArray<WakeTrailStampCommand> stampCommands))
            {
                GraphicsBuffer stampWriteBuffer = (_wakeTrailStampCommandUploadBufferIndex & 1) == 0
                    ? _wakeTrailStampCommandBufferA
                    : _wakeTrailStampCommandBufferB;
                if (stampWriteBuffer != null && stampWriteBuffer.IsValid())
                {
                    GraphicsBufferUploadUtility.UploadNativeArray(stampWriteBuffer, stampCommands, _queuedWakeTrailStampCount);
                    _activeWakeTrailStampCommandBuffer = stampWriteBuffer;
                    _wakeTrailStampCommandUploadBufferIndex ^= 1;
                }
            }

            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);
            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);
            _wakeTrailSimulationCompute.SetBuffer(_wakeTrailSimulationKernel, _WakeTrailStampCommandsId, _activeWakeTrailStampCommandBuffer);
            _wakeTrailSimulationCompute.SetInt(_WakeTrailStampCountId, _queuedWakeTrailStampCount);
            _wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, GetCurrentSimulationTimeSeconds());
            float wakeTrailInvResolution = math.rcp(Mathf.Max(_wakeTrailRuntimeResolution, 1));
            _wakeTrailSimulationCompute.SetVector(
                _WakeTrailTexelSizeId,
                new Vector4(
                    wakeTrailInvResolution,
                    wakeTrailInvResolution,
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));

            _wakeTrailSimulationCompute.Dispatch(_wakeTrailSimulationKernel, groupCountX, groupCountY, 1);

            RenderTexture temp = _wakeTrailRead;
            _wakeTrailRead = _wakeTrailWrite;
            _wakeTrailWrite = temp;
            _lastWakeTrailDispatchSerial = _wakeTrailDispatchSerial;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void QueueWakeTrailScroll(Vector2 centerDelta)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            float uvOffsetX = centerDelta.x / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            float uvOffsetY = centerDelta.y / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            if (Mathf.Abs(uvOffsetX) >= 1f || Mathf.Abs(uvOffsetY) >= 1f)
            {
                ClearWakeTrailTextures();
                return;
            }

            _pendingWakeTrailScrollUv.x += uvOffsetX;
            _pendingWakeTrailScrollUv.y += uvOffsetY;
        }

        private int ResolveKernel(ComputeShader computeShader, string kernelName)
        {
            if (computeShader == null || !_supportsWakeTrailComputeCold)
                return -1;

            try
            {
                if (!computeShader.HasKernel(kernelName))
                    return -1;

                int kernel = computeShader.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return computeShader.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private void ResolveKernelThreadGroupSizes(
            ComputeShader compute,
            int kernel,
            out int sizeX,
            out int sizeY)
        {
            sizeX = 0;
            sizeY = 0;
            if (compute == null || kernel < 0 || !_supportsWakeTrailComputeCold)
                return;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return;

                compute.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            catch (System.InvalidOperationException)
            {
                return;
            }
            catch (System.ArgumentException)
            {
                return;
            }
            catch (MissingReferenceException)
            {
                return;
            }
            catch (UnityException)
            {
                return;
            }
            if (queryX == 0u || queryY == 0u || queryZ != 1u || queryX > int.MaxValue || queryY > int.MaxValue)
                return;

            ulong totalThreads = queryX * (ulong)queryY * queryZ;
            if (totalThreads > PortableMaxComputeThreadsPerGroup)
                return;

            sizeX = (int)queryX;
            sizeY = (int)queryY;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private void ClearWakeTrailTextures()
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _wakeTrailRead;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = _wakeTrailWrite;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
            _wakeTrailEnergy = 0f;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void PublishWakeTrailGlobals()
        {
            if (_wakeTrailDisabled || _wakeTrailResourceRefreshRequested || _wakeTrailRead == null)
            {
                Shader.SetGlobalFloat(_WakeTrailActiveId, 0f);
                Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, 0f);
                return;
            }

            Shader.SetGlobalTexture(_WakeTrailTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_WakeTrailWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_WakeTrailActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            Shader.SetGlobalTexture(_ShallowWaterFieldTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_ShallowWaterFieldWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            float wakeTrailInvResolution = math.rcp(Mathf.Max(_wakeTrailRuntimeResolution, 1));
            Shader.SetGlobalVector(
                _ShallowWaterFieldTexelSizeId,
                new Vector4(
                    wakeTrailInvResolution,
                    wakeTrailInvResolution,
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));
        }

        private RenderTexture CreateWakeTrailTexture(string textureName)
        {
            RenderTexture texture = new RenderTexture(_wakeTrailRuntimeResolution, _wakeTrailRuntimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - persistent vegetation wake trail ping-pong target - owner: FloraInteractionManager
            texture.Create();
            return texture;
        }

        private static void ReleaseWakeTrailTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private float ResolveWakeTrailSnapStride(float worldSize)
        {
            float pixelWorldSize = worldSize * math.rcp(Mathf.Max(_wakeTrailRuntimeResolution, 1));
            return pixelWorldSize * Mathf.Max(0.1f, _wakeTrailCenterSnapPixelStride);
        }

        private static Vector2 QuantizeWakeTrailCenter(Vector2 centerXZ, float stride)
        {
            if (stride <= 0.0001f)
                return centerXZ;

            return new Vector2(
                Mathf.Round(centerXZ.x / stride) * stride,
                Mathf.Round(centerXZ.y / stride) * stride);
        }

        private static Color ResolveAmbientColor()
        {
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    return RenderSettings.ambientLight;
                case AmbientMode.Trilight:
                    return RenderSettings.ambientEquatorColor;
                default:
                    return RenderSettings.ambientSkyColor;
            }
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            if (!IsFiniteVector3(runtimeOffset))
                return;

            _smoothPosition += runtimeOffset;
            if (_hasLastPlayerPosition)
                _lastPlayerPosition += runtimeOffset;

            if (_hasSmoothedScooterPosition)
                _smoothedScooterPosition += runtimeOffset;

            if (_hasActiveScooterWake)
                _lastPublishedScooterWakePosition += runtimeOffset;

            if (_hasActiveSubmarineWake)
                _lastPublishedSubmarineWakePosition += runtimeOffset;

            if (_damageReactionRemainingSeconds > 0f)
                _damageReactionPositionWS += runtimeOffset;

            if (_interactionPoints != null &&
                _lastPublishedInteractionCount > 0)
            {
                int interactionCount = Mathf.Min(_lastPublishedInteractionCount, _interactionPoints.Length);
                for (int i = 0; i < interactionCount; i++)
                {
                    Vector4 positionRadius = _interactionPoints[i].PositionRadius;
                    positionRadius.x += runtimeOffset.x;
                    positionRadius.y += runtimeOffset.y;
                    positionRadius.z += runtimeOffset.z;
                    _interactionPoints[i].PositionRadius = positionRadius;
                }

                GraphicsBuffer writeBuffer = ResolveInteractionWriteBuffer();
                if (writeBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadArray(writeBuffer, _interactionPoints, interactionCount);
                    _activeInteractionBuffer = writeBuffer;
                    _interactionBufferWriteIndex ^= 1;
                    Shader.SetGlobalBuffer(_InteractionBufferId, _activeInteractionBuffer);
                }
            }

            if (_flowFieldResolution > 0 || _flowFieldCellSize > 0f)
            {
                _flowFieldCenterWS += runtimeOffset;
                _lastUploadedFlowFieldCenterWS += runtimeOffset;
                PublishFlowFieldGlobals();
            }

            if (_floraSwayFieldResolution > 0 || _floraSwayFieldCellSize > 0f || _floraSwayFieldActive)
            {
                _floraSwayFieldCenterWS += runtimeOffset;
                _lastUploadedFloraSwayFieldCenterWS += runtimeOffset;
                PublishFloraSwayFieldGlobals(forceUpload: true);
            }

            if (_wakeTrailWorldRect.z > 0f && _wakeTrailWorldRect.w > 0f)
            {
                _wakeTrailCenterXZ += new Vector2(runtimeOffset.x, runtimeOffset.z);
                _wakeTrailWorldRect.x += runtimeOffset.x;
                _wakeTrailWorldRect.y += runtimeOffset.z;
                QueueWakeTrailGlobals();
            }

            if (TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources))
            {
                float3 offset = new float3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                for (int i = 0; i < wakeSources.Length; i++)
                {
                    WakeSource point = wakeSources[i];
                    if (point.Active == 0)
                        continue;

                    point.PositionWS += offset;
                    point.TargetWS += offset;
                    wakeSources[i] = point;
                }

                PublishProceduralWakeBuffer();
            }

            if (_publishedParasiteAnchorCount > 0)
            {
                for (int i = 0; i < _publishedParasiteAnchorCount; i++)
                {
                    Vector4 anchor = _parasiteAnchorData[i];
                    anchor.x += runtimeOffset.x;
                    anchor.y += runtimeOffset.y;
                    anchor.z += runtimeOffset.z;
                    _parasiteAnchorData[i] = anchor;
                }

                PublishParasiteInfectionGlobals();
            }

            if (!_parasiteGrowthScheduled && _parasiteNodeCount > 0 && TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes))
            {
                try
                {
                    float3 offset = new float3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                    int safeCount = math.min(_parasiteNodeCount, parasiteNodes.Length);
                    for (int i = 0; i < safeCount; i++)
                    {
                        ParasiteNode node = parasiteNodes[i];
                        node.PositionWS += offset;
                        parasiteNodes[i] = node;
                    }
                }
                finally
                {
                    ReleaseFloraVaultWriteLock(in _parasiteNodesHandle);
                }
            }

            QueueEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : _smoothPosition);
        }

        private void ResetInteractionGlobals()
        {
            _interactionResetGlobalsDirty = true;
            _interactionGlobalsDirty = false;
            _playerRuntimePositionDirty = false;
            _damageReactionGlobalsDirty = false;
            _pendingDamageReactionDeltaTime = 0f;
            _lastPublishedInteractionCount = 0;
            _lastPublishedPlayerVelocity = Vector3.zero;
            _lastPublishedScooterWakePosition = Vector3.zero;
            _lastPublishedSubmarineWakePosition = Vector3.zero;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _damageReactionPositionWS = Vector3.zero;
            _damageReactionStrength = 0f;
            _damageReactionRemainingSeconds = 0f;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = false;
            _hasActiveScooterWake = false;
            _hasActiveSubmarineWake = false;
            _wakeTrailEnergy = 0f;
            _playerSedimentCooldownRemaining = 0f;
            _scooterSedimentCooldownRemaining = 0f;
            _toxicSporeScanTimer = 0f;
            _lastToxicSporeExposure01 = 0f;
            _moduleParasiteScanTimer = 0f;
            _publishedParasiteAnchorCount = 0;

            QueueWakeTrailTextureClear();
            QueueWakeTrailGlobals();
        }

        private void FlushInteractionResetGlobals()
        {
            if (!_interactionResetGlobalsDirty)
                return;

            _interactionResetGlobalsDirty = false;
            Shader.SetGlobalVector(_PropWashPosId, Vector4.zero);
            Shader.SetGlobalFloat(_PropWashForceId, 0f);
            Shader.SetGlobalInt(_InteractionCountId, 0);
            Shader.SetGlobalVector(_PlayerRuntimePositionId, Vector4.zero);
            Shader.SetGlobalVector(_PlayerFloraInteractionParamsId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalOceanFlowId, Vector4.zero);
            Shader.SetGlobalVector(_VegetationCurrentVectorId, Vector4.zero);
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, 0f);
            Shader.SetGlobalVector(_FloraPredatorThreatParamsId, Vector4.zero);
            Shader.SetGlobalVector(_FloraPredatorThreatPositionRadiusId, Vector4.zero);
            Shader.SetGlobalVector(_FloraLifecycleParamsId, new Vector4(0f, 0f, 1f, 0f));
            Shader.SetGlobalVector(_FloraDamageReactionId, Vector4.zero);
            Shader.SetGlobalFloat(_SeasonCycleId, 0f);
            Shader.SetGlobalFloat(_SeasonCycleAliasId, 0f);
            Shader.SetGlobalVector(_SubmarineWashSphereId, Vector4.zero);
            Shader.SetGlobalVector(_SubmarineWashVelocityId, Vector4.zero);
            Shader.SetGlobalVector(_SubmarinePropwashId, Vector4.zero);
            Shader.SetGlobalVector(_SubmarineWashAupGridId, Vector4.zero);
            Shader.SetGlobalVector(_SubmarineWashAupLocalId, Vector4.zero);
            Shader.SetGlobalVector(_MarineSnowFlowFieldCenterCellSizeId, Vector4.zero);
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, 0);
            Shader.SetGlobalInt(_CulledFloraVisibleCountId, 0);
            Shader.SetGlobalVector(_ParasiteGlobalsId, Vector4.zero);

            if (_activeInteractionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _activeInteractionBuffer);
        }

        private void RefreshCachedSubmarineContext()
        {
            _submarineHullRigidbody = _submarineRuntimeContext != null
                ? _submarineRuntimeContext.HullRigidbody
                : null;
        }

        private void CacheEnvironmentRuntimeServicesCold()
        {
            BindFloraDataVault(GlobalRegistry.DataVault);

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_submarineRuntimeContext == null)
                _submarineRuntimeContext = GlobalRegistry.Submarine;

            if (_fluidReadModel == null)
                _fluidReadModel = GlobalRegistry.FluidSurfaceCurrent;

            if (_mapMagicRuntime == null || !_mapMagicRuntime.isActiveAndEnabled)
            {
                _mapMagicRuntime = null;
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);
            }

            if (_celestialEngine == null)
                _celestialEngine = GlobalRegistry.CelestialEngine;

            if (_atmosphereReadModel == null)
                _atmosphereReadModel = GlobalRegistry.AtmosphereReadModel;

            if (_constructionParasiteGraph == null)
                _constructionParasiteGraph = GlobalRegistry.ConstructionParasiteGraph;

            if (_vegetationBridge == null || !_vegetationBridge.isActiveAndEnabled)
                RefreshVegetationBridgeFromCachedService();

            if (!IsSaveServiceUsable(_saveService))
                _saveService = GlobalRegistry.Save;

            if (_oceanKinematicsService == null)
            {
                _oceanKinematicsService = GlobalRegistry.OceanKinematics;
                _oceanKinematicsProvider = _oceanKinematicsService != null ? _oceanKinematicsService.ActiveProvider : null;
            }

            RefreshCachedSubmarineContext();
        }

        private void RefreshPlayerReferenceCacheCold()
        {
            _playerReferenceRefreshRequested = false;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                playerTransform = BootstrapState.CurrentPlayerTransform;
            if (playerTransform == null)
                playerTransform = _playerTransformOverride;

            _playerTransform = playerTransform;
            if (playerTransform == null)
            {
                _playerMovement = null;
                _playerToolManager = null;
                return;
            }

            if (playerContext != null && playerContext.PlayerTransform == playerTransform)
            {
                _playerMovement = playerContext.PlayerMovement;
                _playerToolManager = playerContext.ToolManager;
                return;
            }

            playerTransform.TryGetComponent(out _playerMovement);
            _playerToolManager = ResolvePlayerToolManagerCold(playerTransform);
        }

        private static PlayerToolManager ResolvePlayerToolManagerCold(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            return playerTransform.TryGetComponent(out PlayerToolManager directToolManager)
                ? directToolManager
                : null;
        }

        private void BindFloraDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_wakeDataVault, dataVault))
                return;

            ReleaseFloraAuxiliaryVaultBuffers();
            _wakeDataVault = dataVault;
            ClearFloraVaultHandles();
        }

        private void ClearFloraVaultHandles()
        {
            _proceduralWakePointsHandle = default;
            _globalWakeBufferHandle = default;
            _globalWakeVectorBufferHandle = default;
            _wakeBlackBoxHandle = default;
            _wakeTrailStampCommandsHandle = default;
            _floraSwayFieldHandle = default;
            _floraSwayFieldMetaHandle = default;
            _floraSwayFieldBlackBoxHandle = default;
            _floraStiffnessRulesHandle = default;
            _floraStiffnessCsvScratchHandle = default;
            _oceanFlowSamplePositionsHandle = default;
            _oceanFlowSampleResultsHandle = default;
            _parasiteNodesHandle = default;
            _cascadeReactiveTemplateMaskHandle = default;
            _defensiveSporeBurstTemplateMaskHandle = default;
            _allelopathicBloodKelpTemplateMaskHandle = default;
            _allelopathicGhostWeedTemplateMaskHandle = default;
            _surfaceCascadePhaseSeedsHandle = default;
            _underwaterCascadePhaseSeedsHandle = default;
            _surfaceCascadeEventsHandle = default;
            _underwaterCascadeEventsHandle = default;
            _surfaceReactiveFloraHandlesHandle = default;
            _underwaterReactiveFloraHandlesHandle = default;
            _reactiveFloraQueryHandlesHandle = default;
            _floraMemoryTelemetryHandle = default;
            _parasiteNodeCount = 0;
            _surfaceReactiveFloraHandleCount = 0;
            _underwaterReactiveFloraHandleCount = 0;
            _floraMemoryTelemetryCursor = 0;
            _floraMemoryConsecutiveFailureCount = 0;
            _floraMemoryTelemetryDumped = false;
        }

        private void ReleaseFloraAuxiliaryVaultBuffers()
        {
            ReleaseFloraVaultBuffer(ref _oceanFlowSamplePositionsHandle);
            ReleaseFloraVaultBuffer(ref _oceanFlowSampleResultsHandle);
            ReleaseFloraVaultBuffer(ref _parasiteNodesHandle);
            ReleaseFloraVaultBuffer(ref _cascadeReactiveTemplateMaskHandle);
            ReleaseFloraVaultBuffer(ref _defensiveSporeBurstTemplateMaskHandle);
            ReleaseFloraVaultBuffer(ref _allelopathicBloodKelpTemplateMaskHandle);
            ReleaseFloraVaultBuffer(ref _allelopathicGhostWeedTemplateMaskHandle);
            ReleaseFloraVaultBuffer(ref _surfaceCascadePhaseSeedsHandle);
            ReleaseFloraVaultBuffer(ref _underwaterCascadePhaseSeedsHandle);
            ReleaseFloraVaultBuffer(ref _surfaceCascadeEventsHandle);
            ReleaseFloraVaultBuffer(ref _underwaterCascadeEventsHandle);
            ReleaseFloraVaultBuffer(ref _surfaceReactiveFloraHandlesHandle);
            ReleaseFloraVaultBuffer(ref _underwaterReactiveFloraHandlesHandle);
            ReleaseFloraVaultBuffer(ref _reactiveFloraQueryHandlesHandle);
            ReleaseFloraVaultBuffer(ref _floraMemoryTelemetryHandle);
            _parasiteNodeCount = 0;
            _surfaceCascadeEventCount = 0;
            _underwaterCascadeEventCount = 0;
            _surfaceReactiveFloraHandleCount = 0;
            _underwaterReactiveFloraHandleCount = 0;
            _floraMemoryTelemetryCursor = 0;
            _floraMemoryConsecutiveFailureCount = 0;
            _floraMemoryTelemetryDumped = false;
        }

        private void CacheInstanceCullingServiceCold()
        {
            _instanceCullingService = GlobalRegistry.InstanceCulling;
        }

        private bool ResolveProceduralWakeVaultBuffer(bool clearExisting)
        {
            return ResolveProceduralWakeVaultBuffer(_wakeDataVault, clearExisting);
        }

        private bool ResolveProceduralWakeVaultBuffer(IDataVault dataVault, bool clearExisting)
        {
            BindFloraDataVault(dataVault);
            if (dataVault == null)
            {
                _proceduralWakePointsHandle = default;
                _globalWakeBufferHandle = default;
                _globalWakeVectorBufferHandle = default;
                _wakeBlackBoxHandle = default;
                _wakeTrailStampCommandsHandle = default;
                return false;
            }

            NativeArrayOptions options = clearExisting
                ? NativeArrayOptions.ClearMemory
                : NativeArrayOptions.UninitializedMemory;
            if (!TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _proceduralWakePointsHandle,
                    BufferID.WakeSources,
                    MaxProceduralWakePoints,
                    options,
                    out NativeArray<WakeSource> wakeSources) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _globalWakeBufferHandle,
                    BufferID.WakeGlobalBuffer,
                    MaxProceduralWakePoints,
                    options,
                    out NativeArray<float4> globalWakes) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _globalWakeVectorBufferHandle,
                    BufferID.WakeVectorBuffer,
                    MaxProceduralWakePoints,
                    options,
                    out NativeArray<float4> globalWakeVectors) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _wakeBlackBoxHandle,
                    BufferID.WakeBlackBox,
                    WakeBlackBoxCapacity,
                    options,
                    out NativeArray<WakeTelemetryEntry> wakeBlackBox) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _wakeTrailStampCommandsHandle,
                    BufferID.WakeTrailStampCommands,
                    WakeTrailStampCommandCapacity,
                    options,
                    out NativeArray<WakeTrailStampCommand> wakeTrailStampCommands))
            {
                return false;
            }

            if (clearExisting)
            {
                for (int i = 0; i < wakeSources.Length; i++)
                    wakeSources[i] = default;
                for (int i = 0; i < globalWakes.Length; i++)
                    globalWakes[i] = default;
                for (int i = 0; i < globalWakeVectors.Length; i++)
                    globalWakeVectors[i] = default;
                for (int i = 0; i < wakeBlackBox.Length; i++)
                    wakeBlackBox[i] = default;
                for (int i = 0; i < wakeTrailStampCommands.Length; i++)
                    wakeTrailStampCommands[i] = default;
                _wakeBlackBoxCursor = 0;
                _wakeBlackBoxDumped = false;
            }

            return true;
        }

        private bool TryResolveProceduralWakeBuffer(out NativeArray<WakeSource> wakeSources)
        {
            wakeSources = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                dataVault,
                in _proceduralWakePointsHandle,
                BufferID.WakeSources,
                MaxProceduralWakePoints,
                out wakeSources);
        }

        private bool TryResolveGlobalWakeBuffers(out NativeArray<float4> globalWakes, out NativeArray<float4> globalWakeVectors)
        {
            globalWakes = default;
            globalWakeVectors = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                       dataVault,
                       in _globalWakeBufferHandle,
                       BufferID.WakeGlobalBuffer,
                       MaxProceduralWakePoints,
                       out globalWakes) &&
                   TryResolveFloraVaultBuffer(
                       dataVault,
                       in _globalWakeVectorBufferHandle,
                       BufferID.WakeVectorBuffer,
                       MaxProceduralWakePoints,
                       out globalWakeVectors);
        }

        private bool TryResolveWakeBlackBox(out NativeArray<WakeTelemetryEntry> wakeBlackBox)
        {
            wakeBlackBox = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                dataVault,
                in _wakeBlackBoxHandle,
                BufferID.WakeBlackBox,
                WakeBlackBoxCapacity,
                out wakeBlackBox);
        }

        private bool EnsureFloraSwayFieldResources()
        {
            if (!IsFloraVaultHandle(in _floraSwayFieldHandle, FloraSwayDisplacementFieldBufferId) ||
                !IsFloraVaultHandle(in _floraSwayFieldMetaHandle, FloraSwayFieldMetaBufferId) ||
                !IsFloraVaultHandle(in _floraSwayFieldBlackBoxHandle, FloraSwayFieldBlackBoxBufferId))
            {
                return false;
            }

            return TryResolveFloraSwayFieldBuffers(out _, out _) &&
                   _floraSwayFieldBufferA != null &&
                   _floraSwayFieldBufferB != null;
        }

        private bool ResolveFloraSwayFieldVaultBuffer(bool clearExisting)
        {
            return ResolveFloraSwayFieldVaultBuffer(_wakeDataVault, clearExisting);
        }

        private bool ResolveFloraSwayFieldVaultBuffer(IDataVault dataVault, bool clearExisting)
        {
            BindFloraDataVault(dataVault);
            if (dataVault == null)
            {
                _floraSwayFieldHandle = default;
                _floraSwayFieldMetaHandle = default;
                _floraSwayFieldBlackBoxHandle = default;
                _floraStiffnessRulesHandle = default;
                _floraStiffnessCsvScratchHandle = default;
                return false;
            }

            NativeArrayOptions options = clearExisting
                ? NativeArrayOptions.ClearMemory
                : NativeArrayOptions.UninitializedMemory;
            if (!TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraSwayFieldHandle,
                    FloraSwayDisplacementFieldBufferId,
                    FloraSwayFieldMaxNodeCount,
                    options,
                    out NativeArray<FloraDisplacementDTO> fieldValues) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraSwayFieldMetaHandle,
                    FloraSwayFieldMetaBufferId,
                    FloraSwayFieldMetaVectorCount,
                    options,
                    out NativeArray<float4> fieldMeta) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraSwayFieldBlackBoxHandle,
                    FloraSwayFieldBlackBoxBufferId,
                    FloraSwayFieldBlackBoxCapacity,
                    options,
                    out NativeArray<FloraSwayFieldTelemetryEntry> blackBox) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraStiffnessRulesHandle,
                    FloraStiffnessRulesBufferId,
                    FloraStiffnessRuleCapacity,
                    options,
                    out NativeArray<FloraStiffnessRuleDTO> _) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraStiffnessCsvScratchHandle,
                    FloraStiffnessCsvScratchBufferId,
                    FloraCsvScratchBytes,
                    options,
                    out NativeArray<byte> _))
            {
                return false;
            }

            if (clearExisting)
            {
                for (int i = 0; i < fieldValues.Length; i++)
                    fieldValues[i] = default;
                for (int i = 0; i < fieldMeta.Length; i++)
                    fieldMeta[i] = default;
                for (int i = 0; i < blackBox.Length; i++)
                    blackBox[i] = default;
                _floraSwayFieldBlackBoxCursor = 0;
                _floraSwayFieldBlackBoxDumped = false;
            }

            return true;
        }

        private bool TryResolveFloraSwayFieldBuffers(out NativeArray<FloraDisplacementDTO> fieldValues, out NativeArray<float4> fieldMeta)
        {
            fieldValues = default;
            fieldMeta = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                       dataVault,
                       in _floraSwayFieldHandle,
                       FloraSwayDisplacementFieldBufferId,
                       FloraSwayFieldMaxNodeCount,
                       out fieldValues) &&
                   TryResolveFloraVaultBuffer(
                       dataVault,
                       in _floraSwayFieldMetaHandle,
                       FloraSwayFieldMetaBufferId,
                       FloraSwayFieldMetaVectorCount,
                       out fieldMeta);
        }

        private bool TryResolveFloraSwayFieldBlackBox(out NativeArray<FloraSwayFieldTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                dataVault,
                in _floraSwayFieldBlackBoxHandle,
                FloraSwayFieldBlackBoxBufferId,
                FloraSwayFieldBlackBoxCapacity,
                out blackBox);
        }

        private bool ResolveFloraAuxiliaryVaultBuffers(bool clearExisting)
        {
            IDataVault dataVault = _wakeDataVault;
            if (dataVault == null)
            {
                ClearFloraVaultHandles();
                return false;
            }

            NativeArrayOptions options = clearExisting
                ? NativeArrayOptions.ClearMemory
                : NativeArrayOptions.UninitializedMemory;
            if (!TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _oceanFlowSamplePositionsHandle,
                    FloraOceanFlowSamplePositionsBufferId,
                    1,
                    options,
                    out NativeArray<Vector3> oceanFlowSamplePositions) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _oceanFlowSampleResultsHandle,
                    FloraOceanFlowSampleResultsBufferId,
                    1,
                    options,
                    out NativeArray<Vector3> oceanFlowSampleResults) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _parasiteNodesHandle,
                    FloraParasiteNodesBufferId,
                    _headlessParasiteCapacity,
                    options,
                    out NativeArray<ParasiteNode> parasiteNodes) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _surfaceCascadeEventsHandle,
                    FloraSurfaceCascadeEventsBufferId,
                    MaxCascadeEvents,
                    options,
                    out NativeArray<FloraCascadeEventPayload> surfaceCascadeEvents) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _underwaterCascadeEventsHandle,
                    FloraUnderwaterCascadeEventsBufferId,
                    MaxCascadeEvents,
                    options,
                    out NativeArray<FloraCascadeEventPayload> underwaterCascadeEvents) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _surfaceCascadePhaseSeedsHandle,
                    FloraSurfaceCascadePhaseSeedsBufferId,
                    CascadePhaseSeedCapacity,
                    options,
                    out NativeArray<float> surfaceCascadePhaseSeeds) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _underwaterCascadePhaseSeedsHandle,
                    FloraUnderwaterCascadePhaseSeedsBufferId,
                    CascadePhaseSeedCapacity,
                    options,
                    out NativeArray<float> underwaterCascadePhaseSeeds) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _surfaceReactiveFloraHandlesHandle,
                    FloraSurfaceReactiveHandlesBufferId,
                    ReactiveFloraHandleCapacity,
                    options,
                    out NativeArray<int> surfaceReactiveHandles) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _underwaterReactiveFloraHandlesHandle,
                    FloraUnderwaterReactiveHandlesBufferId,
                    ReactiveFloraHandleCapacity,
                    options,
                    out NativeArray<int> underwaterReactiveHandles) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _reactiveFloraQueryHandlesHandle,
                    FloraReactiveQueryHandlesBufferId,
                    ReactiveFloraHandleCapacity,
                    options,
                    out NativeArray<int> reactiveQueryHandles) ||
                !TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _floraMemoryTelemetryHandle,
                    FloraMemoryTelemetryBufferId,
                    FloraMemoryTelemetryCapacity,
                    options,
                    out NativeArray<FloraMemoryTelemetryEntry> floraMemoryTelemetry))
            {
                return false;
            }

            if (clearExisting)
            {
                oceanFlowSamplePositions[0] = default;
                oceanFlowSampleResults[0] = default;
                for (int i = 0; i < parasiteNodes.Length; i++)
                    parasiteNodes[i] = default;
                for (int i = 0; i < surfaceCascadeEvents.Length; i++)
                    surfaceCascadeEvents[i] = default;
                for (int i = 0; i < underwaterCascadeEvents.Length; i++)
                    underwaterCascadeEvents[i] = default;
                for (int i = 0; i < surfaceCascadePhaseSeeds.Length; i++)
                    surfaceCascadePhaseSeeds[i] = InactiveCascadeSeed;
                for (int i = 0; i < underwaterCascadePhaseSeeds.Length; i++)
                    underwaterCascadePhaseSeeds[i] = InactiveCascadeSeed;
                for (int i = 0; i < surfaceReactiveHandles.Length; i++)
                    surfaceReactiveHandles[i] = 0;
                for (int i = 0; i < underwaterReactiveHandles.Length; i++)
                    underwaterReactiveHandles[i] = 0;
                for (int i = 0; i < reactiveQueryHandles.Length; i++)
                    reactiveQueryHandles[i] = 0;
                for (int i = 0; i < floraMemoryTelemetry.Length; i++)
                    floraMemoryTelemetry[i] = default;

                _parasiteNodeCount = 0;
                _surfaceCascadeEventCount = 0;
                _underwaterCascadeEventCount = 0;
                _surfaceReactiveFloraHandleCount = 0;
                _underwaterReactiveFloraHandleCount = 0;
                _floraMemoryTelemetryCursor = 0;
                _floraMemoryConsecutiveFailureCount = 0;
                _floraMemoryTelemetryDumped = false;
            }

            return true;
        }

        private bool TryAcquireReactiveFloraQueryHandles(out NativeArray<int> queryHandles)
        {
            return TryAcquireFloraVaultWriteBuffer(
                ref _reactiveFloraQueryHandlesHandle,
                FloraReactiveQueryHandlesBufferId,
                ReactiveFloraHandleCapacity,
                out queryHandles);
        }

        private void ReleaseReactiveFloraQueryHandlesWriteLock()
        {
            ReleaseFloraVaultWriteLock(in _reactiveFloraQueryHandlesHandle);
        }

        private bool TryAcquireRegisteredReactiveFloraHandles(bool underwater, out NativeArray<int> registeredHandles)
        {
            if (underwater)
            {
                return TryAcquireFloraVaultWriteBuffer(
                    ref _underwaterReactiveFloraHandlesHandle,
                    FloraUnderwaterReactiveHandlesBufferId,
                    ReactiveFloraHandleCapacity,
                    out registeredHandles);
            }

            return TryAcquireFloraVaultWriteBuffer(
                ref _surfaceReactiveFloraHandlesHandle,
                FloraSurfaceReactiveHandlesBufferId,
                ReactiveFloraHandleCapacity,
                out registeredHandles);
        }

        private void ReleaseRegisteredReactiveFloraHandlesWriteLock(bool underwater)
        {
            if (underwater)
            {
                ReleaseFloraVaultWriteLock(in _underwaterReactiveFloraHandlesHandle);
                return;
            }

            ReleaseFloraVaultWriteLock(in _surfaceReactiveFloraHandlesHandle);
        }

        private bool TryResolveRegisteredReactiveFloraHandles(bool underwater, out NativeArray<int> registeredHandles)
        {
            registeredHandles = default;
            if (underwater)
            {
                return TryResolveFloraVaultBuffer(
                    _wakeDataVault,
                    in _underwaterReactiveFloraHandlesHandle,
                    FloraUnderwaterReactiveHandlesBufferId,
                    ReactiveFloraHandleCapacity,
                    out registeredHandles);
            }

            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _surfaceReactiveFloraHandlesHandle,
                FloraSurfaceReactiveHandlesBufferId,
                ReactiveFloraHandleCapacity,
                out registeredHandles);
        }

        private bool TryAcquireParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes)
        {
            return TryAcquireFloraVaultWriteBuffer(
                ref _parasiteNodesHandle,
                FloraParasiteNodesBufferId,
                _headlessParasiteCapacity,
                out parasiteNodes);
        }

        private bool TryResolveParasiteNodes(out NativeArray<ParasiteNode> parasiteNodes)
        {
            parasiteNodes = default;
            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _parasiteNodesHandle,
                FloraParasiteNodesBufferId,
                _headlessParasiteCapacity,
                out parasiteNodes);
        }

        private bool TryResolveAllelopathicTemplateMasks(
            out NativeArray<byte> bloodKelpTemplateMask,
            out NativeArray<byte> ghostWeedTemplateMask)
        {
            bloodKelpTemplateMask = default;
            ghostWeedTemplateMask = default;
            return TryResolveFloraVaultBuffer(
                       _wakeDataVault,
                       in _allelopathicBloodKelpTemplateMaskHandle,
                       FloraBloodKelpTemplateMaskBufferId,
                       1,
                       out bloodKelpTemplateMask) &&
                   TryResolveFloraVaultBuffer(
                       _wakeDataVault,
                       in _allelopathicGhostWeedTemplateMaskHandle,
                       FloraGhostWeedTemplateMaskBufferId,
                       1,
                       out ghostWeedTemplateMask);
        }

        private bool TryResolveCascadeReactiveTemplateMask(out NativeArray<byte> cascadeReactiveTemplateMask)
        {
            cascadeReactiveTemplateMask = default;
            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _cascadeReactiveTemplateMaskHandle,
                FloraCascadeReactiveTemplateMaskBufferId,
                1,
                out cascadeReactiveTemplateMask);
        }

        private bool TryResolveDefensiveSporeBurstTemplateMask(out NativeArray<byte> defensiveSporeBurstTemplateMask)
        {
            defensiveSporeBurstTemplateMask = default;
            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _defensiveSporeBurstTemplateMaskHandle,
                FloraDefensiveSporeTemplateMaskBufferId,
                1,
                out defensiveSporeBurstTemplateMask);
        }

        private bool TryAcquireCascadeEvents(bool underwater, out NativeArray<FloraCascadeEventPayload> cascadeEvents)
        {
            if (underwater)
            {
                return TryAcquireFloraVaultWriteBuffer(
                    ref _underwaterCascadeEventsHandle,
                    FloraUnderwaterCascadeEventsBufferId,
                    MaxCascadeEvents,
                    out cascadeEvents);
            }

            return TryAcquireFloraVaultWriteBuffer(
                ref _surfaceCascadeEventsHandle,
                FloraSurfaceCascadeEventsBufferId,
                MaxCascadeEvents,
                out cascadeEvents);
        }

        private bool TryResolveCascadeEvents(bool underwater, out NativeArray<FloraCascadeEventPayload> cascadeEvents)
        {
            cascadeEvents = default;
            if (underwater)
            {
                return TryResolveFloraVaultBuffer(
                    _wakeDataVault,
                    in _underwaterCascadeEventsHandle,
                    FloraUnderwaterCascadeEventsBufferId,
                    1,
                    out cascadeEvents);
            }

            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _surfaceCascadeEventsHandle,
                FloraSurfaceCascadeEventsBufferId,
                1,
                out cascadeEvents);
        }

        private bool TryAcquireCascadePhaseSeeds(bool underwater, int requiredLength, out NativeArray<float> phaseSeeds)
        {
            int safeLength = math.clamp(requiredLength, 1, CascadePhaseSeedCapacity);
            if (underwater)
            {
                return TryAcquireFloraVaultWriteBuffer(
                    ref _underwaterCascadePhaseSeedsHandle,
                    FloraUnderwaterCascadePhaseSeedsBufferId,
                    safeLength,
                    out phaseSeeds);
            }

            return TryAcquireFloraVaultWriteBuffer(
                ref _surfaceCascadePhaseSeedsHandle,
                FloraSurfaceCascadePhaseSeedsBufferId,
                safeLength,
                out phaseSeeds);
        }

        private bool TryResolveCascadePhaseSeeds(bool underwater, out NativeArray<float> phaseSeeds)
        {
            phaseSeeds = default;
            if (underwater)
            {
                return TryResolveFloraVaultBuffer(
                    _wakeDataVault,
                    in _underwaterCascadePhaseSeedsHandle,
                    FloraUnderwaterCascadePhaseSeedsBufferId,
                    1,
                    out phaseSeeds);
            }

            return TryResolveFloraVaultBuffer(
                _wakeDataVault,
                in _surfaceCascadePhaseSeedsHandle,
                FloraSurfaceCascadePhaseSeedsBufferId,
                1,
                out phaseSeeds);
        }

        private void ReleaseCascadeEventsWriteLock(bool underwater)
        {
            if (underwater)
            {
                ReleaseFloraVaultWriteLock(in _underwaterCascadeEventsHandle);
                return;
            }

            ReleaseFloraVaultWriteLock(in _surfaceCascadeEventsHandle);
        }

        private void ReleaseCascadePhaseSeedsWriteLock(bool underwater)
        {
            if (underwater)
            {
                ReleaseFloraVaultWriteLock(in _underwaterCascadePhaseSeedsHandle);
                return;
            }

            ReleaseFloraVaultWriteLock(in _surfaceCascadePhaseSeedsHandle);
        }

        private bool TryAcquireFloraVaultWriteBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault dataVault = _wakeDataVault;
            if (!TryResolveFloraVaultBuffer(dataVault, in handle, bufferId, requiredLength, out _, recordFailure: true))
                return false;

            if (dataVault.IsCompactionFenceActive)
            {
                RecordFloraMemoryTelemetry(
                    FloraMemoryTelemetryEventWriteLock,
                    bufferId,
                    handle.Generation,
                    requiredLength,
                    0,
                    FloraMemoryTelemetryCompactionFenceFlag);
                return false;
            }

            if (!dataVault.TryAcquireWriteLock(in handle, SystemID.Vfx, out buffer))
            {
                RecordFloraMemoryTelemetry(
                    FloraMemoryTelemetryEventWriteLock,
                    bufferId,
                    handle.Generation,
                    requiredLength,
                    0,
                    FloraMemoryTelemetryWriteLockFailedFlag);
                buffer = default;
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (dataVault.IsCompactionFenceActive)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventWriteLock,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        buffer.IsCreated ? buffer.Length : 0,
                        FloraMemoryTelemetryCompactionFenceFlag);
                    buffer = default;
                    return false;
                }

                if (!buffer.IsCreated || buffer.Length < requiredLength)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventWriteLock,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        buffer.IsCreated ? buffer.Length : 0,
                        FloraMemoryTelemetryInvalidBufferFlag);
                    buffer = default;
                    return false;
                }

                _floraMemoryConsecutiveFailureCount = 0;
                SetFloraWriteVault(bufferId, dataVault);
                releaseOnFailure = false;
                return true;
            }
            finally
            {
                if (releaseOnFailure)
                    dataVault.ReleaseWriteLock(in handle, SystemID.Vfx);
            }
        }

        private void ReleaseFloraVaultWriteLock<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault dataVault = TakeFloraWriteVault(handle.BufferID);
            if (dataVault == null || handle.Generation == 0u)
                return;

            dataVault.ReleaseWriteLock(in handle, SystemID.Vfx);
        }

        private void ReleaseFloraVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            ClearFloraWriteVault(handle.BufferID);
            IDataVault dataVault = _wakeDataVault;
            if (dataVault != null && handle.Generation != 0u)
                dataVault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryAcquireFloraMemoryTelemetry(out NativeArray<FloraMemoryTelemetryEntry> telemetry)
        {
            telemetry = default;
            IDataVault dataVault = _wakeDataVault;
            if (!TryResolveFloraVaultBuffer(
                    dataVault,
                    in _floraMemoryTelemetryHandle,
                    FloraMemoryTelemetryBufferId,
                    FloraMemoryTelemetryCapacity,
                    out NativeArray<FloraMemoryTelemetryEntry> _,
                    recordFailure: false))
            {
                return false;
            }

            if (dataVault.IsCompactionFenceActive)
                return false;

            bool telemetryLockAcquired = dataVault.TryAcquireWriteLock(in _floraMemoryTelemetryHandle, SystemID.Vfx, out telemetry);
            if (!telemetryLockAcquired)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (dataVault.IsCompactionFenceActive ||
                    !telemetry.IsCreated ||
                    telemetry.Length < FloraMemoryTelemetryCapacity)
                {
                    telemetry = default;
                    return false;
                }

                _floraMemoryTelemetryWriteVault = dataVault;
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                    dataVault.ReleaseWriteLock(in _floraMemoryTelemetryHandle, SystemID.Vfx);
            }
        }

        private void SetFloraWriteVault(BufferID bufferId, IDataVault dataVault)
        {
            switch (bufferId)
            {
                case FloraParasiteNodesBufferId:
                    _parasiteNodesWriteVault = dataVault;
                    return;
                case FloraCascadeReactiveTemplateMaskBufferId:
                    _cascadeReactiveTemplateMaskWriteVault = dataVault;
                    return;
                case FloraDefensiveSporeTemplateMaskBufferId:
                    _defensiveSporeBurstTemplateMaskWriteVault = dataVault;
                    return;
                case FloraSurfaceCascadePhaseSeedsBufferId:
                    _surfaceCascadePhaseSeedsWriteVault = dataVault;
                    return;
                case FloraUnderwaterCascadePhaseSeedsBufferId:
                    _underwaterCascadePhaseSeedsWriteVault = dataVault;
                    return;
                case FloraSurfaceCascadeEventsBufferId:
                    _surfaceCascadeEventsWriteVault = dataVault;
                    return;
                case FloraUnderwaterCascadeEventsBufferId:
                    _underwaterCascadeEventsWriteVault = dataVault;
                    return;
                case FloraSurfaceReactiveHandlesBufferId:
                    _surfaceReactiveFloraHandlesWriteVault = dataVault;
                    return;
                case FloraUnderwaterReactiveHandlesBufferId:
                    _underwaterReactiveFloraHandlesWriteVault = dataVault;
                    return;
                case FloraReactiveQueryHandlesBufferId:
                    _reactiveFloraQueryHandlesWriteVault = dataVault;
                    return;
                case FloraMemoryTelemetryBufferId:
                    _floraMemoryTelemetryWriteVault = dataVault;
                    return;
            }
        }

        private IDataVault TakeFloraWriteVault(uint bufferId)
        {
            BufferID resolvedBufferId = unchecked((BufferID)(int)bufferId);
            IDataVault dataVault = null;
            switch (resolvedBufferId)
            {
                case FloraParasiteNodesBufferId:
                    dataVault = _parasiteNodesWriteVault;
                    _parasiteNodesWriteVault = null;
                    return dataVault;
                case FloraCascadeReactiveTemplateMaskBufferId:
                    dataVault = _cascadeReactiveTemplateMaskWriteVault;
                    _cascadeReactiveTemplateMaskWriteVault = null;
                    return dataVault;
                case FloraDefensiveSporeTemplateMaskBufferId:
                    dataVault = _defensiveSporeBurstTemplateMaskWriteVault;
                    _defensiveSporeBurstTemplateMaskWriteVault = null;
                    return dataVault;
                case FloraSurfaceCascadePhaseSeedsBufferId:
                    dataVault = _surfaceCascadePhaseSeedsWriteVault;
                    _surfaceCascadePhaseSeedsWriteVault = null;
                    return dataVault;
                case FloraUnderwaterCascadePhaseSeedsBufferId:
                    dataVault = _underwaterCascadePhaseSeedsWriteVault;
                    _underwaterCascadePhaseSeedsWriteVault = null;
                    return dataVault;
                case FloraSurfaceCascadeEventsBufferId:
                    dataVault = _surfaceCascadeEventsWriteVault;
                    _surfaceCascadeEventsWriteVault = null;
                    return dataVault;
                case FloraUnderwaterCascadeEventsBufferId:
                    dataVault = _underwaterCascadeEventsWriteVault;
                    _underwaterCascadeEventsWriteVault = null;
                    return dataVault;
                case FloraSurfaceReactiveHandlesBufferId:
                    dataVault = _surfaceReactiveFloraHandlesWriteVault;
                    _surfaceReactiveFloraHandlesWriteVault = null;
                    return dataVault;
                case FloraUnderwaterReactiveHandlesBufferId:
                    dataVault = _underwaterReactiveFloraHandlesWriteVault;
                    _underwaterReactiveFloraHandlesWriteVault = null;
                    return dataVault;
                case FloraReactiveQueryHandlesBufferId:
                    dataVault = _reactiveFloraQueryHandlesWriteVault;
                    _reactiveFloraQueryHandlesWriteVault = null;
                    return dataVault;
                case FloraMemoryTelemetryBufferId:
                    dataVault = _floraMemoryTelemetryWriteVault;
                    _floraMemoryTelemetryWriteVault = null;
                    return dataVault;
            }

            return null;
        }

        private void ClearFloraWriteVault(uint bufferId)
        {
            TakeFloraWriteVault(bufferId);
        }

        private bool TryResolveFloraMemoryTelemetryReadOnly(out NativeArray<FloraMemoryTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            IDataVault dataVault = _wakeDataVault;
            return dataVault != null &&
                   !dataVault.IsCompactionFenceActive &&
                   IsFloraVaultHandle(in _floraMemoryTelemetryHandle, FloraMemoryTelemetryBufferId) &&
                   dataVault.TryReadOnlyHandle(in _floraMemoryTelemetryHandle, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length >= FloraMemoryTelemetryCapacity &&
                   !dataVault.IsCompactionFenceActive;
        }

        private void RecordFloraMemoryTelemetry(
            uint eventHash,
            BufferID bufferId,
            uint generation,
            int requiredLength,
            int actualLength,
            uint flags)
        {
            if (!TryAcquireFloraMemoryTelemetry(out NativeArray<FloraMemoryTelemetryEntry> telemetry))
                return;

            try
            {
                int cursor = _floraMemoryTelemetryCursor;
                if ((uint)cursor >= (uint)telemetry.Length)
                    cursor = 0;

                if ((flags & (FloraMemoryTelemetryResolveFailedFlag |
                              FloraMemoryTelemetryWriteLockFailedFlag |
                              FloraMemoryTelemetryInvalidBufferFlag |
                              FloraMemoryTelemetryNaNFlag)) != 0u)
                {
                    _floraMemoryConsecutiveFailureCount = math.min(
                        _floraMemoryConsecutiveFailureCount + 1,
                        FloraMemoryTelemetryCapacity);
                }
                else
                {
                    _floraMemoryConsecutiveFailureCount = 0;
                }

                telemetry[cursor] = new FloraMemoryTelemetryEntry
                {
                    Frame = (uint)Time.frameCount,
                    EventHash = eventHash,
                    BufferID = (uint)bufferId,
                    SystemID = (uint)SystemID.Vfx,
                    Generation = generation,
                    RequiredLength = (uint)math.max(0, requiredLength),
                    ActualLength = (uint)math.max(0, actualLength),
                    Flags = flags,
                    ConsecutiveFailures = (uint)_floraMemoryConsecutiveFailureCount,
                    VaultGeneration = _wakeDataVault != null ? _wakeDataVault.VaultGenerationID : 0u,
                    GlobalQualityWeight = Mathf.Clamp01(HomeostasisBrain.GlobalQualityWeight),
                    SystemStress01 = HomeostasisBrain.SystemHealthIndex01,
                    StateHash = eventHash ^ (uint)bufferId ^ generation ^ flags,
                    AupShiftSequence = HectonFloatingOrigin.CurrentShiftSequence,
                    CpuMicroseconds = 0u
                };

                cursor++;
                if (cursor >= telemetry.Length)
                    cursor = 0;
                _floraMemoryTelemetryCursor = cursor;
            }
            finally
            {
                ReleaseFloraVaultWriteLock(in _floraMemoryTelemetryHandle);
            }

            if ((flags & FloraMemoryTelemetryNaNFlag) != 0u ||
                _floraMemoryConsecutiveFailureCount >= FloraMemoryTelemetryDumpFailureThreshold)
            {
                DumpFloraMemoryTelemetryOnce();
            }
        }

        private unsafe void DumpFloraMemoryTelemetryOnce()
        {
            if (_floraMemoryTelemetryDumped ||
                !TryResolveFloraMemoryTelemetryReadOnly(out NativeArray<FloraMemoryTelemetryEntry>.ReadOnly telemetry))
            {
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                int headerBytes = 8;
                int rowBytes = UnsafeUtility.SizeOf<FloraMemoryTelemetryEntry>();
                int totalBytes = headerBytes + telemetry.Length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(FloraInteractionManager),
                    "FloraMemoryTelemetryDumpPayload");
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteInt32LittleEndian(bytes, 0, telemetry.Length);
                WriteInt32LittleEndian(bytes, 4, _floraMemoryTelemetryCursor);
                int writeOffset = headerBytes;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    FloraMemoryTelemetryEntry entry = telemetry[i];
                    WriteFloraMemoryTelemetryEntry(bytes.Slice(writeOffset, rowBytes), in entry);
                    writeOffset += rowBytes;
                }

                _floraMemoryTelemetryDumped = NativeFaultDumpWriter.TryWriteAll(FloraMemoryTelemetryDumpPath, payload, totalBytes);
            }
            catch (IOException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (UnauthorizedAccessException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FloraInteractionManager),
                    "FloraMemoryTelemetryDumpPayload");
            }
        }

        private static void WriteFloraMemoryTelemetryDumpQueued(object state)
        {
            FloraMemoryTelemetryDumpWork work = state as FloraMemoryTelemetryDumpWork;
            if (work == null || string.IsNullOrEmpty(work.Path) || work.Payload == null)
                return;

            try
            {
                NativeFaultDumpWriter.TryWriteAll(work.Path, new ReadOnlySpan<byte>(work.Payload), work.Payload.Length);
            }
            catch (IOException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (UnauthorizedAccessException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
        }

        private static void WriteWakeTelemetryEntry(Span<byte> destination, in WakeTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, 0, entry.Frame);
            WriteUInt16LittleEndian(destination, 4, entry.ActiveWakeSourcesCount);
            WriteUInt16LittleEndian(destination, 6, entry.SlotLimit);
            WriteFloat3LittleEndian(destination, 8, entry.StrongestWakePositionWS);
            WriteSingleLittleEndian(destination, 20, entry.StrongestIntensity);
            WriteFloat3LittleEndian(destination, 24, entry.StrongestVelocityWS);
            WriteSingleLittleEndian(destination, 36, entry.MaxRadius);
            WriteUInt32LittleEndian(destination, 40, entry.Flags);
            WriteUInt32LittleEndian(destination, 44, entry.StateHash);
            WriteUInt32LittleEndian(destination, 48, entry.DataVaultGeneration);
            WriteUInt32LittleEndian(destination, 52, entry.AupShiftSequence);
            WriteSingleLittleEndian(destination, 56, entry.SystemStress01);
            WriteSingleLittleEndian(destination, 60, entry.BudgetPressure01);
        }

        private static void WriteFloraSwayFieldTelemetryEntry(Span<byte> destination, in FloraSwayFieldTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, 0, entry.Frame);
            WriteUInt16LittleEndian(destination, 4, entry.Resolution);
            WriteUInt16LittleEndian(destination, 6, entry.ActiveWakeSourcesCount);
            WriteUInt32LittleEndian(destination, 8, entry.NonZeroCellsCount);
            WriteUInt32LittleEndian(destination, 12, entry.Flags);
            WriteFloat3LittleEndian(destination, 16, entry.FieldCenterWS);
            WriteSingleLittleEndian(destination, 28, entry.CellSize);
            WriteSingleLittleEndian(destination, 32, entry.MaxMagnitude);
            WriteSingleLittleEndian(destination, 36, entry.GlobalQualityWeight);
            WriteSingleLittleEndian(destination, 40, entry.UpdateIntervalSeconds);
            WriteSingleLittleEndian(destination, 44, entry.SystemStress01);
            WriteUInt32LittleEndian(destination, 48, entry.StateHash);
            WriteUInt32LittleEndian(destination, 52, entry.DataVaultGeneration);
            WriteUInt32LittleEndian(destination, 56, entry.AupShiftSequence);
            WriteUInt32LittleEndian(destination, 60, entry.CpuMicroseconds);
        }

        private static void WriteFloraMemoryTelemetryEntry(Span<byte> destination, in FloraMemoryTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, 0, entry.Frame);
            WriteUInt32LittleEndian(destination, 4, entry.EventHash);
            WriteUInt32LittleEndian(destination, 8, entry.BufferID);
            WriteUInt32LittleEndian(destination, 12, entry.SystemID);
            WriteUInt32LittleEndian(destination, 16, entry.Generation);
            WriteUInt32LittleEndian(destination, 20, entry.RequiredLength);
            WriteUInt32LittleEndian(destination, 24, entry.ActualLength);
            WriteUInt32LittleEndian(destination, 28, entry.Flags);
            WriteUInt32LittleEndian(destination, 32, entry.ConsecutiveFailures);
            WriteUInt32LittleEndian(destination, 36, entry.VaultGeneration);
            WriteSingleLittleEndian(destination, 40, entry.GlobalQualityWeight);
            WriteSingleLittleEndian(destination, 44, entry.SystemStress01);
            WriteUInt32LittleEndian(destination, 48, entry.StateHash);
            WriteUInt32LittleEndian(destination, 52, entry.AupShiftSequence);
            WriteUInt32LittleEndian(destination, 56, entry.CpuMicroseconds);
            WriteUInt32LittleEndian(destination, 60, 0u);
        }

        private static void WriteFloat3LittleEndian(Span<byte> destination, int offset, float3 value)
        {
            WriteSingleLittleEndian(destination, offset, value.x);
            WriteSingleLittleEndian(destination, offset + 4, value.y);
            WriteSingleLittleEndian(destination, offset + 8, value.z);
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt16LittleEndian(Span<byte> destination, int offset, ushort value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
        }

        private bool EnsureFloraSwayFieldGraphicsBuffers(bool allowCreate = true)
        {
            if (_floraSwayFieldBufferA == null || _floraSwayFieldBufferA.count != FloraSwayFieldMaxNodeCount)
            {
                if (!allowCreate)
                    return false;

                ReleaseGraphicsBuffer(ref _floraSwayFieldBufferA);
                _floraSwayFieldBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraDisplacementDTO>(FloraSwayFieldMaxNodeCount); // COLD ALLOC: GraphicsBuffer[262144 x 16B] - Vault-backed 3D flora displacement field upload A - owner: FloraInteractionManager
            }

            if (_floraSwayFieldBufferB == null || _floraSwayFieldBufferB.count != FloraSwayFieldMaxNodeCount)
            {
                if (!allowCreate)
                    return false;

                ReleaseGraphicsBuffer(ref _floraSwayFieldBufferB);
                _floraSwayFieldBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraDisplacementDTO>(FloraSwayFieldMaxNodeCount); // COLD ALLOC: GraphicsBuffer[262144 x 16B] - Vault-backed 3D flora displacement field upload B - owner: FloraInteractionManager
            }

            if (_floraSwayFieldReadBuffer == null)
                _floraSwayFieldReadBuffer = _floraSwayFieldBufferA;

            return _floraSwayFieldBufferA != null && _floraSwayFieldBufferB != null;
        }

        private bool EnsureWakeTrailStampCommandBuffer(bool clearExisting)
        {
            IDataVault dataVault = _wakeDataVault;
            if (dataVault == null)
            {
                _wakeTrailStampCommandsHandle = default;
                return false;
            }

            NativeArrayOptions options = !IsFloraVaultHandle(in _wakeTrailStampCommandsHandle, BufferID.WakeTrailStampCommands) || clearExisting
                ? NativeArrayOptions.ClearMemory
                : NativeArrayOptions.UninitializedMemory;
            if (!TryEnsureFloraVaultBuffer(
                    dataVault,
                    ref _wakeTrailStampCommandsHandle,
                    BufferID.WakeTrailStampCommands,
                    WakeTrailStampCommandCapacity,
                    options,
                    out NativeArray<WakeTrailStampCommand> stampCommands))
            {
                return false;
            }

            if (clearExisting)
            {
                for (int i = 0; i < stampCommands.Length; i++)
                    stampCommands[i] = default;
            }

            return true;
        }

        private bool TryResolveWakeTrailStampCommands(out NativeArray<WakeTrailStampCommand> stampCommands)
        {
            stampCommands = default;
            IDataVault dataVault = _wakeDataVault;
            return TryResolveFloraVaultBuffer(
                dataVault,
                in _wakeTrailStampCommandsHandle,
                BufferID.WakeTrailStampCommands,
                WakeTrailStampCommandCapacity,
                out stampCommands);
        }

        private bool TryEnsureFloraVaultBuffer<T>(
            IDataVault dataVault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            bool recordFailure = true) where T : struct
        {
            buffer = default;
            if (dataVault == null)
            {
                if (recordFailure)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventResolve,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        0,
                        FloraMemoryTelemetryMissingVaultFlag);
                }

                return false;
            }

            if (requiredLength <= 0)
            {
                if (recordFailure)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventResolve,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        0,
                        FloraMemoryTelemetryInvalidLengthFlag);
                }

                return false;
            }

            if (dataVault.IsCompactionFenceActive)
            {
                if (recordFailure)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventResolve,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        0,
                        FloraMemoryTelemetryCompactionFenceFlag);
                }

                return false;
            }

            if (!TryResolveFloraVaultBuffer(dataVault, in handle, bufferId, requiredLength, out buffer, recordFailure: false))
            {
                handle = dataVault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.Vfx,
                    options);
            }

            if (!TryResolveFloraVaultBuffer(dataVault, in handle, bufferId, requiredLength, out buffer, recordFailure))
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryResolveFloraVaultBuffer<T>(
            IDataVault dataVault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer,
            bool recordFailure = true) where T : struct
        {
            buffer = default;
            if (dataVault == null)
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, 0, FloraMemoryTelemetryMissingVaultFlag);

                return false;
            }

            if (requiredLength <= 0)
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, 0, FloraMemoryTelemetryInvalidLengthFlag);

                return false;
            }

            if (dataVault.IsCompactionFenceActive)
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, 0, FloraMemoryTelemetryCompactionFenceFlag);

                return false;
            }

            if (!IsFloraVaultHandle(in handle, bufferId))
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, 0, FloraMemoryTelemetryHandleMismatchFlag);

                return false;
            }

            if (!dataVault.TryResolveHandle(in handle, out buffer))
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, 0, FloraMemoryTelemetryResolveFailedFlag);

                return false;
            }

            if (dataVault.IsCompactionFenceActive)
            {
                if (recordFailure)
                    RecordFloraMemoryTelemetry(FloraMemoryTelemetryEventResolve, bufferId, handle.Generation, requiredLength, buffer.IsCreated ? buffer.Length : 0, FloraMemoryTelemetryCompactionFenceFlag);

                buffer = default;
                return false;
            }

            if (!buffer.IsCreated || buffer.Length < requiredLength)
            {
                if (recordFailure)
                {
                    RecordFloraMemoryTelemetry(
                        FloraMemoryTelemetryEventResolve,
                        bufferId,
                        handle.Generation,
                        requiredLength,
                        buffer.IsCreated ? buffer.Length : 0,
                        FloraMemoryTelemetryInvalidBufferFlag);
                }

                buffer = default;
                return false;
            }

            _floraMemoryConsecutiveFailureCount = 0;
            return true;
        }

        private static bool IsFloraVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Vfx &&
                   handle.Generation != 0u;
        }

        private void TryRegisterCullingHotSwapListener()
        {
            if (_cullingHotSwapRegistered || !Application.isPlaying)
                return;

            _cullingHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterCullingHotSwapListener()
        {
            if (!_cullingHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _cullingHotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    ResolveProceduralWakeVaultBuffer(currentService as IDataVault, clearExisting: false);
                    ResolveFloraSwayFieldVaultBuffer(currentService as IDataVault, clearExisting: false);
                    PublishFloraSwayFieldGlobals(forceUpload: true);
                    break;
                case GlobalRegistryServiceSlot.InstanceCullingRuntime:
                    _instanceCullingService = currentService as IInstanceCullingService;
                    PublishCulledFloraGlobals();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    RefreshPlayerReferenceCacheCold();
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    RefreshCachedSubmarineContext();
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _fluidReadModel = currentService as IFluidSurfaceCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_mapMagicRuntime, previousService))
                        _mapMagicRuntime = null;
                    _mapMagicRuntime = currentService as MapMagicBridge;
                    WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _celestialEngine = currentService as HectonCelestialEngine;
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _atmosphereReadModel = currentService as IAtmosphereReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _vegetationBridge = _vegetationBridgeOverride != null
                        ? _vegetationBridgeOverride
                        : currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
                    _vegetationBridgeResolveRequested = _vegetationBridge == null;
                    break;
                case GlobalRegistryServiceSlot.Logistics:
                    _constructionParasiteGraph = currentService as IConstructionParasiteGraphService;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _saveService = currentService as ISaveService;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    _oceanKinematicsProvider = _oceanKinematicsService != null ? _oceanKinematicsService.ActiveProvider : null;
                    break;
            }
        }

        private void ReleaseFlowFieldBuffer()
        {
            ReleaseGraphicsBuffer(ref _flowFieldBufferA);
            ReleaseGraphicsBuffer(ref _flowFieldBufferB);
            _activeFlowFieldBuffer = null;
            _flowFieldUploadBufferIndex = 0;
        }

        private void ReleaseFloraSwayFieldBuffers()
        {
            ReleaseGraphicsBuffer(ref _floraSwayFieldBufferA);
            ReleaseGraphicsBuffer(ref _floraSwayFieldBufferB);
            _floraSwayFieldReadBuffer = null;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_isRegistered)
            {
                _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_isSlowTickRegistered)
            {
                _isSlowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_isLateFrameRegistered)
            {
                _isLateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryUnregister()
        {
            if (_isRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = false;
            }

            if (_isSlowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _isSlowTickRegistered = false;
            }

            if (_isLateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _isLateFrameRegistered = false;
            }
        }

        private static long EstimateGraphicsBufferBytes(GraphicsBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
        }

        private static long EstimateRenderTextureBytes(RenderTexture texture)
        {
            if (texture == null)
                return 0L;

            int bytesPerPixel = 4;
            return (long)texture.width * texture.height * bytesPerPixel;
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            float az = Mathf.Abs(value.z);
            float maxAxis = Mathf.Max(ax, Mathf.Max(ay, az));
            float minAxis = Mathf.Min(ax, Mathf.Min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static float EstimateLength3D(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(float.IsFinite(value) ? value : 0f);
            return t * t * (3f - 2f * t);
        }

        private static float3 ResolveSafeDirectionOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (math.isfinite(lengthSq) && lengthSq > 0.000001f)
                return value * math.rsqrt(math.max(lengthSq, 0.000001f));

            float fallbackLengthSq = math.lengthsq(fallback);
            if (math.isfinite(fallbackLengthSq) && fallbackLengthSq > 0.000001f)
                return fallback * math.rsqrt(math.max(fallbackLengthSq, 0.000001f));

            return float3.zero;
        }

        private static float ClampFinite(float value, float fallback, float min, float max)
        {
            float sanitized = float.IsFinite(value) ? value : fallback;
            float safeMin = float.IsFinite(min) ? min : 0f;
            float safeMax = float.IsFinite(max) ? max : safeMin;
            if (!float.IsFinite(sanitized))
                sanitized = safeMin;

            if (safeMax < safeMin)
            {
                float swap = safeMin;
                safeMin = safeMax;
                safeMax = swap;
            }

            return Mathf.Clamp(sanitized, safeMin, safeMax);
        }

        private void SanitizeSporeHazardAuthoringValues()
        {
            _toxicSporeScanIntervalSeconds = ClampFinite(_toxicSporeScanIntervalSeconds, 0.2f, 0.05f, 1f);
            _toxicSporeDetectionRadius = ClampFinite(_toxicSporeDetectionRadius, 4.5f, 1f, 8f);
            _toxicSporeHazardRadius = ClampFinite(_toxicSporeHazardRadius, 3f, 1f, 8f);
            _toxicSporeHazardIntensity = ClampFinite(_toxicSporeHazardIntensity, 0.78f, 0f, 1f);
            _toxicSporeDragMultiplier = ClampFinite(_toxicSporeDragMultiplier, 1.3f, 1f, 3f);
            _toxicSporeVisorGlitchBias = ClampFinite(_toxicSporeVisorGlitchBias, 1.25f, 0f, 2f);
            _defensiveSporeBurstRadius = ClampFinite(_defensiveSporeBurstRadius, 7f, 1f, 20f);
            _defensiveSporeBurstDose = ClampFinite(_defensiveSporeBurstDose, 8f, 0.25f, 16f);
            _defensiveSporeBurstLifetimeSeconds = ClampFinite(_defensiveSporeBurstLifetimeSeconds, 10f, 1f, 20f);
            _defensiveSporeHazardIntensity = ClampFinite(_defensiveSporeHazardIntensity, 1.15f, 0f, 2f);
            _defensiveSporeVisorGlitchBias = ClampFinite(_defensiveSporeVisorGlitchBias, 1.75f, 0f, 3f);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition value)
        {
            return float.IsFinite(value.LocalX) &&
                   float.IsFinite(value.LocalY) &&
                   float.IsFinite(value.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector3(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool IsFiniteVector4(Vector4 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(math.max(magnitudeSq, 0.0001f)) : fallback;
        }

        private static Vector2 NormalizeVector2Fast(Vector2 vector, Vector2 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(math.max(magnitudeSq, 0.0001f)) : fallback;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeSporeHazardAuthoringValues();
            TryAutoAssignWakeTrailSimulationCompute();
        }
#endif

        private void TryAutoAssignWakeTrailSimulationCompute()
        {
#if UNITY_EDITOR
            if (_wakeTrailSimulationCompute == null)
                _wakeTrailSimulationCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeTrailSimulationComputeAssetPath);
#endif
        }
    }
}

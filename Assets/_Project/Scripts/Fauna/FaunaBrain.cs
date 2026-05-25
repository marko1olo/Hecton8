using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI.Perception;
using Hecton8.Atmosphere;
using Hecton8.Caves;
using UnityEngine;
using UnityEngine.Serialization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using SignalAudioEvent = Hecton8.Core.Contracts.Signals.AudioEvent;

namespace Hecton8.AI
{
    /// <summary>
    /// Master controller for HECTON-8 Fauna AI.
    /// Handles subsystem lifecycle, Brain LOD, and legacy property migration.
    /// [RULE] ZERO GC IN HOT PATHS.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public partial class FaunaBrain : MonoBehaviour, IUpdatable, ITickable, IFixedTickable, ISlowTickable, IBucketedSlowTickable, ILateFrameTickable, IPoolable, ISerializationCallbackReceiver, ICuttable, IOriginShiftListener, ICombatMobilityModifierReceiver, IScannerFaunaScientificContact, IGlobalRegistryHotSwapListener, IFaunaSpatialContact, IFaunaPredationTarget, IFaunaNoiseSignalReceiver
    {
        /// <summary>
        /// Global state definition for all fauna.
        /// [REQ] Restored as nested enum for legacy tool compatibility.
        /// </summary>
        public enum AIState
        {
            Idle,
            Wander,
            Investigate,
            Threaten,
            Stalk,
            Loom,
            Feint,
            Escape,
            Aggressive,
            Flocking,
            Return,
            Retreat,
            ApexForcedRetreat,
            Sated,
            ThreatDisplay,
            Starving
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        internal struct PackCoordinator
        {
            [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
            [FieldOffset(48)] public float3 TargetVelocity;
            [FieldOffset(60)] public float InterceptTimeSeconds;
            [FieldOffset(64)] public float FlankDistanceMeters;
            [FieldOffset(68)] public uint Padding0;
            [FieldOffset(72)] public ulong Padding1;

            public float3 ResolveFlankRuntimePosition(float3 predatorPosition, int packOrdinal, double3 floatingOriginOffset)
            {
                float3 targetPosition = AUPMath.ToRuntimeFloat3(in TargetAup, floatingOriginOffset);
                float3 projectedTarget = targetPosition + (TargetVelocity * math.max(0f, InterceptTimeSeconds));
                if (packOrdinal <= 0 || FlankDistanceMeters <= 0f)
                    return projectedTarget;

                float3 approach = ResolveDominantAxis(projectedTarget - predatorPosition, new float3(0f, 0f, 1f));
                float3 lateral = math.cross(new float3(0f, 1f, 0f), approach);
                if (math.lengthsq(lateral) <= 0.0001f)
                    lateral = new float3(1f, 0f, 0f);
                else
                    lateral = ResolveDominantAxis(lateral, new float3(1f, 0f, 0f));
                float side = (packOrdinal & 1) == 0 ? -1f : 1f;
                float ring = 1f + math.floor((packOrdinal - 1) * 0.5f);
                return projectedTarget + lateral * (side * FlankDistanceMeters * ring);
            }

            private static float3 ResolveDominantAxis(float3 direction, float3 fallback)
            {
                float magnitudeSq = math.lengthsq(direction);
                if (magnitudeSq <= 0.0001f)
                    return fallback;

                float absX = math.abs(direction.x);
                float absY = math.abs(direction.y);
                float absZ = math.abs(direction.z);
                if (absX >= absY && absX >= absZ)
                    return new float3(math.select(1f, -1f, direction.x < 0f), 0f, 0f);

                if (absY >= absZ)
                    return new float3(0f, math.select(1f, -1f, direction.y < 0f), 0f);

                return new float3(0f, 0f, math.select(1f, -1f, direction.z < 0f));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 88)]
        private struct CorpseSinkKinematicInput
        {
            [FieldOffset(0)] public AbsoluteUniversePositionBlit128 PositionAup;
            [FieldOffset(48)] public double3 FloatingOriginOffset;
            [FieldOffset(72)] public float FloorY;
            [FieldOffset(76)] public float DeltaTime;
            [FieldOffset(80)] public float SinkSpeedMetersPerSecond;
            [FieldOffset(84)] public float FloorSettleOffsetMeters;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CorpseSinkKinematicOutput
        {
            [FieldOffset(0)] public AbsoluteUniversePositionBlit128 PositionAup;
            [FieldOffset(48)] public float3 RuntimePosition;
            [FieldOffset(60)] public int FreezeMotion;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CorpseSinkKinematicJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<CorpseSinkKinematicInput> Input;
            [NoAlias] public NativeArray<CorpseSinkKinematicOutput> Output;

            public void Execute()
            {
                if (!Input.IsCreated || Input.Length <= 0 || !Output.IsCreated || Output.Length <= 0)
                    return;

                CorpseSinkKinematicInput input = Input[0];
                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAlignedBlit(in input.PositionAup);
                float3 position = AUPMath.ToRuntimeFloat3(in aup, input.FloatingOriginOffset);
                float originalRuntimeY = position.y;
                float targetY = input.FloorY + input.FloorSettleOffsetMeters;
                int freezeMotion = 0;
                if (position.y <= targetY)
                {
                    position.y = targetY;
                    freezeMotion = 1;
                }
                else
                {
                    position.y = math.max(targetY, position.y - input.SinkSpeedMetersPerSecond * math.max(0f, input.DeltaTime));
                }

                double yDeltaMeters = (double)position.y - originalRuntimeY;
                AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                    in aup,
                    new double3(0d, yDeltaMeters, 0d));
                Output[0] = new CorpseSinkKinematicOutput
                {
                    PositionAup = resolvedAup.ToAlignedBlit(),
                    RuntimePosition = position,
                    FreezeMotion = freezeMotion
                };
            }
        }

        [Header("── Core Identity ────────────────────────────────")]
        public bool isAggressive = false;
        public bool canFlee = true;

        [Header("── Brain LOD ──────────────────────────────────────")]
        public bool enableBrainLOD = true;
        public float brainDisableDistance = 150f;
        public float brainOptimizationDistance = 80f;

        [Header("── Subsystems ───────────────────────────────────")]
        [SerializeField] private FaunaSpeciesProfile _speciesProfile;
        [SerializeField] private FaunaDataTemplate _faunaDataTemplate;
        [SerializeField] private FaunaSensorSuite _sensorSuite = new FaunaSensorSuite();
        [SerializeField] private FaunaSteeringEngine _steeringEngine = new FaunaSteeringEngine();
        [SerializeField] private FaunaStateMachine _stateMachine = FaunaStateMachine.CreateDefault();

        public AIState CurrentState => _stateMachine.currentState;
        public FaunaSpeciesProfile SpeciesProfile => _speciesProfile;
        public FaunaDataTemplate DataTemplate => _faunaDataTemplate;
        public int SpeciesId => ComputeStableSpeciesId();
        public bool HasActiveApexIntimidation => _apexIntimidationUntilTime > _cognitionTimeSeconds;
        public bool IsLeviathanContact => _speciesProfile != null && _speciesProfile.isLeviathan;
        public bool IsApexPredatorContact => IsApexPredator();
        public bool IsBiolumFlashBangPrey => IsBiolumFlashBangPreyRuntime();
        public bool RespondsToParentalDefenseSignal => _faunaDataTemplate != null && _faunaDataTemplate.RespondsToParentalDefenseSignal;
        public Transform ContactTransform => transform;
        internal bool IsApexPredatorRuntime => IsApexPredator();
        internal float ApexTerritoryRadiusMeters => ResolveApexTerritoryRadius();
        internal float ApexTerritoryMassScore => ResolveApexTerritoryMassScore();
        internal bool IsFlockingRuntime => ShouldApplySpatialDensityPenalty();
        /// <inheritdoc />
        public int SimulationBucketId => _simulationBucketId;
        public bool IsFlankingManeuverDetected => _flankingManeuverDetected;
        /// <summary>
        /// True while this predator is publishing a false PDA distress-beacon signal.
        /// </summary>
        public bool HasActiveEcholocationMimicry => _mimicSignalActive;
        public uint PredatorSquadStateBits => _predatorSquadStateBits;
        public byte PredatorSensoryStateBits => _predatorSensoryStateBits;
        public uint ThreatPredictionLoreHash => _faunaDataTemplate != null ? _faunaDataTemplate.FullLoreHash : 0u;

        public bool TryReadScannerFaunaScientificContact(out ScannerFaunaScientificContact contact)
        {
            contact = default;
            contact.ThreatPredictionLoreHash = ThreatPredictionLoreHash;
            contact.Flags = ScannerFaunaScientificContact.FlagContact;
            if (_flankingManeuverDetected)
                contact.Flags |= ScannerFaunaScientificContact.FlagFlankingManeuver;

            return true;
        }

        /// <summary>
        /// [REQ] Eye Tracking vector for procedural bone jobs.
        /// Feed this to the Vault-backed fauna kinematics owner.
        /// </summary>
        public Vector3 LookDirection { get; private set; }

        // --- INTERNAL ---
        private Rigidbody _rb;
        private bool _isDead;
        private bool _dispatcherRegistered;
        private int _spatialHandle;
        private int _faunaSpatialHandle;
        private CreatureUtilityBrain _utilityBrain;
        private FaunaKinematicsRuntime _faunaKinematicsRuntime;
        private FaunaSimplifiedRagdollHandoff _simplifiedRagdollHandoff;
        private ScannableTarget _scannableTarget;
        private PredatorPackRole _currentPackRole;
        private bool _flankingManeuverDetected;
        private float _combatMobilityScale = 1f;
        private float _combatMobilityUntilTime;
        private Vector3 _acousticHeadLookTarget;
        private float _acousticHeadLookWeight;
        private float _acousticHeadLookUntilTime;
        
        private static readonly int _FaunaBiolumDimShaderId = Shader.PropertyToID("_FaunaBiolumDim");
        private static readonly int _FaunaCamouflageTintShaderId = Shader.PropertyToID("_FaunaCamouflageTint");
        private static readonly int _FaunaCamouflageParamsShaderId = Shader.PropertyToID("_FaunaCamouflageParams");
        private static readonly int _FaunaCamouflageStrengthShaderId = Shader.PropertyToID("_FaunaCamouflageStrength");
        private static readonly int _DeathDitherFadeShaderId = Shader.PropertyToID("_DeathDitherFade");
        private static readonly int _CorpseBloatAgeShaderId = Shader.PropertyToID("_CorpseBloatAge01");
        private static readonly int _CorpseBloatStartTimeShaderId = Shader.PropertyToID("_CorpseBloatStartTime");
        private static readonly int _CorpseBloatDurationShaderId = Shader.PropertyToID("_CorpseBloatDuration");
        private static readonly int _DecayAmountShaderId = Shader.PropertyToID("_DecayAmount");
        private static readonly int _HitFlashShaderId = Shader.PropertyToID("_HitFlash");
        private static readonly int _FaunaMutationHueShaderId = Shader.PropertyToID("_FaunaMutationHueShift");
        private static readonly int _FaunaMutationTwitchShaderId = Shader.PropertyToID("_FaunaMutationTwitch");
        private static readonly int _H8FaunaGeneticMaskBytes0ShaderId = Shader.PropertyToID("_H8FaunaGeneticMaskBytes0");
        private static readonly int _H8FaunaGeneticMaskBytes1ShaderId = Shader.PropertyToID("_H8FaunaGeneticMaskBytes1");
        private const float SlowTickIntervalSeconds = 0.5f;
        private const int MaxSlowTicksPerDispatcherTick = 2;
        private const float AmbientCurrentInfluence = 0.22f;
        private const float AmbientCurrentMaxVelocity = 3.8f;
        private const uint KccVelocityFaunaMaxAgeFrames = 12u;
        private const float PlayerEquivalentMassKg = 80f;
        private const float AmbientCurrentCullDistance = 100f;
        private const float AmbientCurrentCullDistanceSqr = AmbientCurrentCullDistance * AmbientCurrentCullDistance;
        private const float PredatorHazardAvoidanceRadius = 14f;
        private const float PredatorHazardFearThreshold = 0.5f;
        private const int MaxVoxelRouteWaypointCount = 16;
        private const float VoxelRouteRefreshIntervalSeconds = 0.25f;
        private const float VoxelRouteRetargetDistanceSqr = 16f;
        private const float VoxelRouteWaypointReachDistanceSqr = 4f;
        private const float DynamicDodgeDistanceScale = 2.25f;
        private const float DynamicDodgeForceMultiplier = 2.75f;
        private const float DynamicDodgeSpeedMultiplier = 1.3f;
        private const float DynamicDodgeTurnMultiplier = 3.25f;
        private const float SpatialDensityPenaltyForceMultiplier = 3.85f;
        private const float SpatialDensityPenaltySpeedMultiplier = 1.45f;
        private const float SpatialDensityPenaltyTurnMultiplier = 3.4f;
        private const float SpatialDensityPenaltyDirectionWeight = 2.65f;
        private const float WallSlideTurnMultiplier = 2.1f;
        private const float WallSlideSpeedMultiplier = 1.1f;
        private const float DamageFearPheromoneFloor = 0.85f;
        private const float DamageFearPheromoneBoost = 1.35f;
        private const float DamageFlinchVelocityFloor = 6f;
        private const float DamageFlinchVelocityCeiling = 18f;
        private const float DamageFlinchVelocityMaxMetersPerSecond = 15f;
        private const float DamageMicroFaunaPanicRadiusMeters = 24f;
        private const float DamageMicroFaunaPanicDurationSeconds = 1.25f;
        private const float HitFlashDecayPerSecond = 9.5f;
        private const float PlayerImpactTraumaWeightPerForce = 0.0015f;
        private const float PredatorImpactSignalImpulseScale = 0.01f;
        private const float HerbivoreSatedDurationSeconds = 16f;
        private const float CleanerFormationMinRadius = 1.6f;
        private const float CleanerFormationMaxRadius = 4.1f;
        private const float CleanerForwardBias = 0.45f;
        private const float CleanerVerticalBiasMin = -0.85f;
        private const float CleanerVerticalBiasMax = 1.15f;
        private const float DefaultApexTerritoryRadiusMeters = 500f;
        private const float DefaultApexIntimidationRadiusMeters = 100f;
        private const float DefaultApexIntimidationDurationSeconds = 24f;
        private const float DefaultApexForcedRetreatDurationSeconds = 18f;
        private const float ParentalDefenseIntensityThreshold = 0.1f;
        private const float DefaultEmpAttackRadiusMeters = 18f;
        private const float DefaultDazzleLockDurationSeconds = 0.35f;
        private const float MimicPingOcclusionRetrySeconds = 0.5f;
        private const int MimicPingDeepOcclusionWallCount = 3;
        private const float FeedingObservationCooldownSeconds = 6f;
        private const float FeedingObservationRadiusMeters = 80f;
        private const float FeedingObservationRadiusMetersSqr = FeedingObservationRadiusMeters * FeedingObservationRadiusMeters;
        private const float HibernationStarvationHuntDurationSeconds = 24f;
        private const float HibernationStarvationOrganicConsumeRadiusMeters = 2.75f;
        private const float LargeCorpseResourceMinHealth = 45f;
        private const float PredatorKillAudioRadiusMeters = 90f;
        private const float PredatorKillAudioRadiusMetersSqr = PredatorKillAudioRadiusMeters * PredatorKillAudioRadiusMeters;
        private const float PredatorKillAudioInvRadiusMetersSqr = 1f / PredatorKillAudioRadiusMetersSqr;
        private const float PredatorKillAudioDurationSeconds = 0.18f;
        private const float LeviathanAttackTelegraphLeadSeconds = 0.8f;
        private const float LeviathanAttackTelegraphInvLeadSeconds = 1f / LeviathanAttackTelegraphLeadSeconds;
        private const float LeviathanAttackTelegraphAudioDurationSeconds = 0.42f;
        private const float LeviathanAttackTelegraphLowPassCutoffHz = 320f;
        private const float LeviathanAttackTelegraphPullbackSpeedScale = 0.28f;
        private const float LeviathanAttackTelegraphPullbackForceScale = 0.35f;
        private const float LeviathanAttackBurstDurationSeconds = 0.5f;
        private const float LeviathanAttackBurstMultiplier = 2.35f;
        private const float PredatorLungeCheatDistanceMultiplier = 1.35f;
        private const float PredatorLungeCcdSkinWidth = 0.08f;
        private const float PredatorLungeCcdFallbackRadius = 1.25f;
        private const float PredatorLungeTargetFallbackExtent = 0.75f;
        private const double PredatorLungeVerticalSlopeAbortRatioSq = 1.8225d;
        private const double PredatorLungeVerticalStepAbortMetersSq = 12.25d;
        private const float PredatorPhotophobiaDotThreshold = 0.95f;
        private const float PredatorPhotophobiaDotThresholdSqr = PredatorPhotophobiaDotThreshold * PredatorPhotophobiaDotThreshold;
        private const float PredatorPhotophobiaDistanceMeters = 40f;
        private const float PredatorPhotophobiaDistanceMetersSqr =
            PredatorPhotophobiaDistanceMeters * PredatorPhotophobiaDistanceMeters;
        private const float PredatorPhotophobiaStunSeconds = 3f;
        private const float DirectorAmbushCooldownSeconds = 8f;
        private const float DirectorAmbushBehindDistanceMeters = 65f;
        private const float DirectorAmbushLateralDistanceMeters = 24f;
        private const float DirectorHuntTargetDurationSeconds = 18f;
        private const float ScavengeToolLookOffsetMeters = 1.25f;
        private const float DirectorVoxelRouteMaxDistanceMeters = 240f;
        private const double DirectorVoxelRouteMaxDistanceMetersSqr =
            DirectorVoxelRouteMaxDistanceMeters * DirectorVoxelRouteMaxDistanceMeters;
        private const float PredatorPredictionMediumVelocitySqr = 64f;
        private const float PredatorPredictionFastVelocitySqr = 225f;
        private const float PredatorPredictionSlowLeadSeconds = 0.08f;
        private const float PredatorPredictionMediumLeadSeconds = 0.2f;
        private const float PredatorPredictionFastLeadSeconds = 0.35f;
        private const float AcousticPingLeviathanScatterRadiusMeters = 90f;
        private const float AcousticPingLeviathanScatterDurationSeconds = 0.85f;
        private const byte ProceduralAudioPingKindPredatorKill = 1;
        private const byte ProceduralAudioPingKindLeviathanRoar = 4;
        private const float AlphaLeviathanFalseChargeStress01 = 1f;
        private const float AlphaLeviathanFalseChargeOxygenDrainScale = 2.5f;
        private const float AlphaLeviathanFalseChargeAggressionScale = 2f;
        private const byte PlayerStressCauseApexPredator = 2;
        private const byte PlayerStressFlagApexPredator = 1 << 1;
        private const byte PlayerStressFlagAcoustic = 1 << 3;
        private const uint PredatorSquadStateHuntingBit = 1u << 0;
        private const uint PredatorSquadStateFleeingBit = 1u << 1;
        private const uint PredatorSquadStateFlankingBit = 1u << 2;
        private const uint PredatorStateStunnedBit = 1u << 3;
        private const int PredatorSquadAlphaShift = 8;
        private const uint PredatorSquadAlphaMask = 0x3u << PredatorSquadAlphaShift;
        private const byte PredatorSensoryCanSeePlayerBit = 1 << 0;
        private const byte PredatorSensoryHearsSonarBit = 1 << 1;
        private const byte PredatorSensoryPhotophobicBit = 1 << 2;
        private const byte PredatorSensoryAggroBit = 1 << 3;
        private const byte PredatorSensoryDeafenedBit = 1 << 4;
        internal const uint PredatorHuntingStateBits = PredatorSquadStateHuntingBit;
        internal const uint HunterSquadHuntingFlankStateBits = PredatorSquadStateHuntingBit | PredatorSquadStateFlankingBit;
        private const float Tier3LeviathanMinimumPingEnergyJoules = 1500f;
        private const float PredatorDeafenedWanderRadiusMeters = 42f;
        private const double LeviathanSectorScatterEdgeMeters = 1000.0;
        private const double LeviathanSectorScatterInvEdgeMeters = 1.0 / LeviathanSectorScatterEdgeMeters;
        private const int PredatorPhotophobiaCacheFrameInterval = 10;
        private const float DirectorColdTickIntervalSeconds = 1f;
        private const float DirectorColdTickHoldSeconds = 1.1f;
        private const float PredatorDeadZoneCullDistanceMeters = 250f;
        private const float PredatorGuidanceLeadSeconds = 0.65f;
        private const float PredatorLungeCloseDistanceMultiplier = 0.55f;
        private const float PlayerNoiseReferenceSpeedSqr = 72.25f;
        private const float ByteToUnitScale = 1f / 255f;
        private const float Random24BitInvScale = 1f / 16777215f;
        private const uint FaunaTickStaggerHashSalt = 0x71C45A6Du;
        private const uint FaunaEggJitterHashSalt = 0x00E66C7u;
        private const uint FaunaDeathSpiralHashSalt = 0x0D34D5A1u;
        private const uint FaunaDeathCorkscrewHashSalt = 0xB5297A4Du;
        private const uint FaunaLeviathanBiteHashSalt = 0xB17ECCD1u;
        private const uint FaunaCarrionDeathHashSalt = 0xCA2210DEu;
        private const float AmbientWanderNoiseWeight = 0.18f;
        private const float AmbientWanderNoiseFrequency = 0.42f;
        private const float AmbientWanderNoiseSpatialScale = 0.013f;
        private const float TailSurgeFrequency = 1.15f;
        private const float TailSurgeAmplitude = 0.16f;
        private const float TailSurgeSeedScale = 0.0009765625f;
        private const float PassiveFlashlightFleeDurationSeconds = 3f;
        private const float PassiveFlashlightDimSeconds = 3.5f;
        private const float PassiveFlashlightBiolumDimMultiplier = 0f;
        private const float PassiveFlashlightBiolumResponseSharpness = 12f;
        private const float RetinalBlindBiolumStrobeDurationSeconds = 1.25f;
        private const float RetinalBlindBiolumStrobeFrequency = 17f;
        private const float RetinalBlindBiolumSecondaryFrequency = 9.5f;
        private const float RetinalBlindBiolumMinimum01 = 0.08f;
        private const float CarrionLatchConsumeDistanceScale = 0.92f;
        private const float CarrionLatchTearingFrequency = 9.5f;
        private const float CarrionLatchTearingPitchDegrees = 17f;
        private const float DegreesToRadians = 0.0174532924f;
        private const float RadiansToDegrees = 57.29578f;
        private const float DeathSpiralFadeDelaySeconds = 60f;
        private const float WhaleFallDurationSeconds = 7200f;
        private const float DeathSpiralFadeDurationSeconds = 8f;
        private const float DeathSpiralFadeInvDurationSeconds = 1f / DeathSpiralFadeDurationSeconds;
        private const float DeathSpiralTorqueMin = 0.08f;
        private const float DeathSpiralTorqueMax = 0.26f;
        private const float DeathSpiralSteeringDurationSeconds = 2f;
        private const float DeathSpiralCorkscrewFrequency = 1.352817f;
        private const float DeathSpiralCorkscrewLateralSpeed = 1.25f;
        private const float DeathSpiralCorkscrewAxisSpeed = DeathSpiralCorkscrewLateralSpeed * 0.70710678f;
        private const float DeathSpiralCorkscrewDescentSpeed = 1.4f;
        private const float CorpseSinkSpeedMetersPerSecond = 0.5f;
        private const float CorpseFloorSettleOffsetMeters = 0.08f;
        private const int MaxBiolumPresentationLights = 4;
        private const ushort FaunaPresentationBiolumMask = 1;
        private const ushort FaunaPresentationDeathDitherMask = 2;
        private const ushort FaunaPresentationCorpseBloatMask = 4;
        private const ushort FaunaPresentationCorpseBloatTimerMask = 8;
        private const ushort FaunaPresentationCorpseBloatDurationMask = 16;
        private const ushort FaunaPresentationCamouflageTintMask = 32;
        private const ushort FaunaPresentationCamouflageParamsMask = 64;
        private const ushort FaunaPresentationCamouflageStrengthMask = 128;
        private const ushort FaunaPresentationHitFlashMask = 256;
        private const ushort FaunaPresentationDecayAmountMask = 512;
        private const ushort FaunaPresentationMutationTwitchMask = 1024;
        private const ushort FaunaPresentationMutationHueMask = 2048;
        private const ushort FaunaPresentationColorMask = 4096;
        private const ushort FaunaPresentationBaseColorMask = 8192;
        private const ushort FaunaPresentationEmissionColorMask = 16384;
        private const ushort FaunaPresentationGeneticMaskMask = 32768;
        private const float FaunaCamouflageStrength = 0.55f;
        private const float FaunaCamouflageDepthStartMeters = 35f;
        private const float FaunaCamouflageDepthEndMeters = 260f;
        private const float FaunaCamouflageDepthInvRange =
            1f / (FaunaCamouflageDepthEndMeters - FaunaCamouflageDepthStartMeters);
        private const float FaunaCamouflageAmbientResponse = 1.35f;
        private const float FaunaCamouflageAmbientFloor = 0.18f;
        private static readonly Color FaunaCamouflageTint = new Color(0.18f, 0.28f, 0.30f, 1f);
        private static readonly Vector4 FaunaCamouflageParams = new Vector4(
            FaunaCamouflageDepthStartMeters,
            FaunaCamouflageDepthInvRange,
            FaunaCamouflageAmbientResponse,
            FaunaCamouflageAmbientFloor);
        private const float BiolumFlashBangBlindDurationSeconds = 0.35f;
        private const float BiolumFlashBangShaderRadiusMeters = 42f;
        private const float LeviathanBreachHeightDeltaMeters = 50f;
        private const float LeviathanBreachVelocityMultiplier = 3.75f;
        private const float LeviathanBreachAirDragBypassSeconds = 1.5f;
        private const float MinimumEggClutchCooldownSeconds = 300f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _nextSlowTickWatchdogLogFrame;
#endif
        
        // --- LOD & Stagger ---
        private bool _lodDisabled;
        private FaunaLogicalLodTier _logicalLodTier = FaunaLogicalLodTier.FullSim;
        private bool _logicalLodPresentationSuppressed;
        private int _tier1LodProxyHandle;
        private uint _uniqueInstanceUid;
        private Renderer _renderer;
        // COLD ALLOC: List<Collider>[8] - logical LOD collider cache build scratch - owner: FaunaBrain
        private readonly List<Collider> _logicalLodColliderScratch = new List<Collider>(8);
        private Collider[] _logicalLodColliders = Array.Empty<Collider>();
        private CapsuleCollider _predatorLungeCcdCapsule;
        private SphereCollider _predatorLungeCcdSphere;
        private int _tickStaggerShift;
        private ISimulationBucketer _simulationBucketer;
        private int _simulationBucketEntityIndex = -1;
        private int _simulationBucketId;
        private int _simulationBucketSlowMask = -1;
        private float _simulationBucketInterpolationAlpha;
        private float _lastFixedTickDeltaSeconds = 0.02f;
        private Vector3 _cachedDesiredDirection;
        private AIState _currentStateCache;
        private Transform _currentCullingPlayerTransform;
        private Transform _playerNoiseEmitterTransform;
        private bool _tier2HibernationRecordWritten;
        private bool _tier2HibernationHandoffInProgress;
        
        // --- Buffers ---
        private static readonly SpatialQueryHit[] _panicBuffer = new SpatialQueryHit[10];
        // COLD ALLOC: SpatialQueryHit[12] - reusable cleaner host lookup buffer over fauna spatial registry - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _cleanerHostBuffer = new SpatialQueryHit[12];
        // COLD ALLOC: SpatialQueryHit[16] - reusable apex rivalry and intimidation lookup buffer over fauna spatial registry - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _apexContactBuffer = new SpatialQueryHit[16];
        // COLD ALLOC: SpatialQueryHit[16] - reusable same-species parental-defense response lookup buffer - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _parentalDefenseBuffer = new SpatialQueryHit[16];
        // COLD ALLOC: Vector3[16] - reusable 3D cave-voxel guidance route for predator steering - owner: FaunaBrain
        private readonly Vector3[] _voxelRouteWaypoints = new Vector3[MaxVoxelRouteWaypointCount];
        // COLD ALLOC: AbsoluteUniversePosition[16] - origin-shift-stable route ownership for predator steering - owner: FaunaBrain
        private readonly AbsoluteUniversePosition[] _voxelRouteWaypointAups = new AbsoluteUniversePosition[MaxVoxelRouteWaypointCount];
        // COLD ALLOC: Light[4] - owned biolum presentation light cache for flashlight/death response - owner: FaunaBrain
        private readonly Light[] _biolumPresentationLights = new Light[MaxBiolumPresentationLights];
        // COLD ALLOC: float[4] - base intensities for owned biolum presentation lights - owner: FaunaBrain
        private readonly float[] _biolumPresentationBaseIntensities = new float[MaxBiolumPresentationLights];
        // COLD ALLOC: List<Light>[4] - owned light discovery scratch without array allocations - owner: FaunaBrain
        private readonly List<Light> _biolumPresentationLightScratch = new List<Light>(MaxBiolumPresentationLights);
        // COLD ALLOC: List<Material>[4] - shared-material scratch for one-time fauna presentation material binding - owner: FaunaBrain
        private readonly List<Material> _faunaPresentationMaterialScratch = new List<Material>(4);
        // COLD ALLOC: List<Material>[4] - original fauna materials restored before destroying runtime material instances - owner: FaunaBrain
        private readonly List<Material> _faunaPresentationOriginalMaterials = new List<Material>(4);
        // COLD ALLOC: List<Material>[4] - owned runtime material instances for shader-side fauna presentation state - owner: FaunaBrain
        private readonly List<Material> _faunaPresentationRuntimeMaterials = new List<Material>(4);
        // COLD ALLOC: List<ushort>[4] - cached shader property support masks for fauna runtime materials - owner: FaunaBrain
        private readonly List<ushort> _faunaPresentationRuntimeMaterialMasks = new List<ushort>(4);

        // --- Event Hooks ---
        [Header("── Audio Hooks ─────────────────────────────────")]
        [Tooltip("Triggered when a Panic Pulse occurs. Hook audio agents here for zero-GC sound dispatch.")]
        public UnityEngine.Events.UnityEvent OnPanicTriggered;
        public UnityEngine.Events.UnityEvent OnBurrowBreach;

        private float _slowTickAccumulator;
        private int _voxelRouteWaypointCount;
        private float _nextVoxelRouteRefreshTime;
        private Vector3 _voxelRouteTargetPosition;
        private AbsoluteUniversePosition _voxelRouteTargetAup;
        private bool _hasVoxelRouteTarget;
        private bool _originShiftListenerRegistered;
        private bool _voxelRouteOriginShiftRefreshActive;
        private int _voxelRouteLastOriginShiftFrame = -1;
        private Transform _apexRivalTarget;
        private Transform _baitFeedingTarget;
        private Vector3 _forcedMigrationTarget;
        private AbsoluteUniversePosition _forcedMigrationTargetAup;
        private Vector3 _hibernationStarvationHuntTarget;
        private AbsoluteUniversePosition _hibernationStarvationHuntTargetAup;
        private float _apexIntimidationUntilTime;
        private float _forcedMigrationUntilTime;
        private float _hibernationStarvationHuntUntilTime;
        private float _nextBurrowBreachTime;
        private float _nextBestiaryObservationTime;
        private float _nextMimicPingTime;
        private float _mimicPingExpireTime;
        private bool _hasForcedMigrationTarget;
        private bool _hasHibernationStarvationHuntTarget;
        private bool _mimicSignalActive;
        private bool _mimicOcclusionRuntimeAcquired;
        private uint _cachedScanEntryHash;
        private float _breachDragBypassUntilTime;
        private float _baseLinearDamping;
        private float _baseAngularDamping;
        private bool _baseLinearDampingCaptured;
        private bool _baseGravityCaptured;
        private bool _baseUseGravity;
        private bool _baseIsKinematic;
        private bool _baseDetectCollisions;
        private float _nextEggClutchTimeSeconds;
        private uint _eggClutchSequence;
        private Transform _telegraphedAttackTarget;
        private float _attackTelegraphBurstTime;
        private float _attackBurstUntilTime;
        private bool _attackTelegraphActive;
        private bool _attackTelegraphAudioEmitted;
        private bool _lungeCheatActive;
        private float _lungeCheatStartTime;
        private float _lungeCheatDuration;
        private Vector3 _lungeCheatStartPosition;
        private Vector3 _lungeCheatTargetPosition;
        private Vector3 _lungeCheatDirection;
        private Vector3 _lungeContactTargetCenter;
        private Vector3 _lungeContactTargetExtents;
        private Vector3 _lungeContactTargetRight;
        private Vector3 _lungeContactTargetUp;
        private Vector3 _lungeContactTargetForward;
        private AbsoluteUniversePosition _lungeCheatStartAup;
        private AbsoluteUniversePosition _lungeCheatTargetAup;
        private uint _lungeContactTargetHash;
        private byte _lungeContactTargetMaterialId;
        private bool _lungeContactTargetActive;
        private bool _lungeTeleportIsolationActive;
        private bool _lungeTeleportRestoreKinematic;
        private bool _lungeTeleportRestoreCollisions;
        private uint _predatorSquadStateBits;
        private int _predatorSquadOrdinal;
        private byte _predatorSensoryStateBits;
        private float _predatorStunnedUntilTime;
        private float _predatorDeafenedUntilTime;
        private AbsoluteUniversePosition _predatorDeafenedWanderAup;
        private bool _hasPredatorDeafenedWanderAup;
        private float _nextDirectorAmbushTime;
        private float _directorColdTickUntilTime;
        private float _directorColdTickAccumulator;
        private float _cachedPredatorPhotophobiaDot;
        private double _cachedPredatorPhotophobiaDistanceSqr;
        private int _nextPredatorPhotophobiaCacheFrame;
        private int2 _leviathanScatterSector;
        private bool _hasLeviathanScatterSector;
        private Vector3 _directorHuntTargetPosition;
        private AbsoluteUniversePosition _directorHuntTargetAup;
        private AbsoluteUniversePosition _directorHuntPredictedAup;
        private Vector3 _directorHuntTargetVelocity;
        private float _directorHuntPredictionLeadSeconds;
        private float _directorHuntPredictionSampleTime;
        private float _directorHuntUntilTime;
        private bool _hasDirectorHuntTarget;
        private bool _hasDirectorHuntPrediction;
        private float _passiveFlashlightDimUntilTime;
        private float _retinalBlindBiolumUntilTime;
        private uint _lastRetinalBlindSignalFrame = uint.MaxValue;
        private float _faunaBiolumDim01 = 1f;
        private float _deathDitherFade01;
        private float _corpseBloatAge01;
        private float _whaleFallDecay01;
        private float _hitFlash01;
        private int _biolumPresentationLightCount;
        private float _lastAppliedBiolumLightScale01 = 1f;
        private float _lastAppliedFaunaBiolumShader01 = -1f;
        private float _lastAppliedDeathDitherShader01 = -1f;
        private float _lastAppliedCorpseBloatShader01 = -1f;
        private float _lastAppliedDecayAmountShader01 = -1f;
        private float _lastAppliedHitFlashShader01 = -1f;
        private float _lastAppliedMutationHueShader01 = -1f;
        private float _lastAppliedMutationTwitchShader01 = -1f;
        private ulong _lastAppliedGeneticMask = ulong.MaxValue;
        private float _lastAppliedInfectionShaderSeverity01 = -1f;
        private bool _lastAppliedInfectionShaderActive;
        private bool _pendingInfectionVisualsDirty;
        private bool _pendingFaunaPresentationShaderStateDirty;
        private float _pendingFaunaPresentationBiolumDim01 = 1f;
        private float _pendingFaunaPresentationDeathDitherFade01;
        private float _pendingFaunaPresentationCorpseBloatAge01;
        private float _pendingFaunaPresentationHitFlash01;
        private float _pendingFaunaPresentationDecayAmount01;
        private bool _pendingBiolumPresentationLightScaleDirty;
        private float _pendingBiolumPresentationLightScale01 = 1f;
        private bool _pendingCorpseBloatShaderTimerDirty;
        private float _pendingCorpseBloatShaderStartTimeSeconds = -1f;
        private bool _pendingAupPresentationPoseDirty;
        private Vector3 _pendingAupPresentationPosition;
        private bool _pendingSelfDespawnOrDeactivate;
        private GameObject _pendingExternalDespawnOrDeactivate;
        private uint _latchedCorpseNodeId;
        private Vector3 _corpseLatchOffset;
        private Vector3 _corpseLatchTargetPosition;
        private Vector3 _corpseLatchCenterPosition;
        private bool _corpseLatchActive;
        private float _corpseTearingPhase;
        private float _corpseTearingPitchRadians;
        private bool _deathSpiralActive;
        private float _deathSpiralStartTime;
        private Vector3 _deathSpiralTorque;
        private float _deathCorkscrewPhaseX;
        private float _deathCorkscrewPhaseZ;
        private AbsoluteUniversePosition _corpseSinkAup;
        private float _corpseFloorY;
        private bool _corpseFloorLatched;
        private IDataVault _corpseSinkVault;
        private VaultGenerationHandle<CorpseSinkKinematicInput> _corpseSinkInputHandle;
        private VaultGenerationHandle<CorpseSinkKinematicOutput> _corpseSinkOutputHandle;
        private JobHandle _corpseSinkJobHandle;
        private bool _corpseSinkJobScheduled;
        private bool _corpseSinkLateFrameRegistered;
        private float _corpseSinkPoseDeltaTime;
        private int _playerRuntimeContextCacheFrame = -1;
        private bool _playerRuntimeContextCacheValid;
        private PlayerRuntimeContext _playerRuntimeContextCache;
        private bool _hotSwapRegistered;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPhysicsService _physicsService;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private IObjectPoolService _objectPool;
        private IFaunaPersistentWorldStateService _persistentWorldRegistry;
        private IHazardZoneReadModel _hazardZones;
        private IAtmosphereReadModel _atmosphereRuntime;
        private IMicroFaunaPresentationPulseSink _sargassumMicroFauna;
        private IVegetationThreatPulseSink _vegetationThreatPulseSink;
        private ISimulationBucketer _simulationBucketerRuntime;

        // ══════════════════════════════════════════════════════════
        //  SERIALIZATION MIGRATION (Option B Data Preservation)
        // ══════════════════════════════════════════════════════════
        [SerializeField, HideInInspector] private bool _migratedV2;

        [SerializeField, HideInInspector, FormerlySerializedAs("swimForce")] private float le_swimForce = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("maxSpeed")] private float le_maxSpeed = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("turnSpeed")] private float le_turnSpeed = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("avoidanceRange")] private float le_avoidanceRange = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("lookAheadFactor")] private float le_lookAheadFactor = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("maxRayLength")] private float le_maxRayLength = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("spreadAngle")] private float le_spreadAngle = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("wanderRadius")] private float le_wanderRadius = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("waypointReachDistance")] private float le_waypointReachDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("escapeDistance")] private float le_escapeDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("escapeSafeDistance")] private float le_escapeSafeDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("sleepDistance")] private float le_sleepDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("aggroDistance")] private float le_aggroDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("deaggroDistance")] private float le_deaggroDistance = -1f;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!_migratedV2)
            {
                if (le_swimForce >= 0) _steeringEngine.swimForce = le_swimForce;
                if (le_maxSpeed >= 0) _steeringEngine.maxSpeed = le_maxSpeed;
                if (le_turnSpeed >= 0) _steeringEngine.turnSpeed = le_turnSpeed;
                if (le_avoidanceRange >= 0) _sensorSuite.avoidanceRange = le_avoidanceRange;
                if (le_lookAheadFactor >= 0) _sensorSuite.lookAheadFactor = le_lookAheadFactor;
                if (le_maxRayLength >= 0) _sensorSuite.maxRayLength = le_maxRayLength;
                if (le_spreadAngle >= 0) _sensorSuite.spreadAngle = le_spreadAngle;
                if (le_wanderRadius >= 0) _stateMachine.wanderRadius = le_wanderRadius;
                if (le_waypointReachDistance >= 0) _stateMachine.waypointReachDistance = le_waypointReachDistance;
                if (le_escapeDistance >= 0) _stateMachine.escapeDistance = le_escapeDistance;
                if (le_escapeSafeDistance >= 0) _stateMachine.escapeSafeDistance = le_escapeSafeDistance;
                if (le_sleepDistance >= 0) _sensorSuite.sleepDistance = le_sleepDistance;
                if (le_aggroDistance >= 0) _sensorSuite.aggroDistance = le_aggroDistance;
                if (le_deaggroDistance >= 0) _sensorSuite.deaggroDistance = le_deaggroDistance;

                le_swimForce = -1f; le_maxSpeed = -1f; le_turnSpeed = -1f;
                le_avoidanceRange = -1f; le_lookAheadFactor = -1f; le_maxRayLength = -1f; le_spreadAngle = -1f;
                le_wanderRadius = -1f; le_waypointReachDistance = -1f; le_escapeDistance = -1f;
                le_escapeSafeDistance = -1f; le_sleepDistance = -1f; le_aggroDistance = -1f; le_deaggroDistance = -1f;

                _migratedV2 = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════
        private void Awake()
        {
            TryGetComponent(out _rb);
            CaptureBaseRigidbodyPresentationState();
            if (!ValidatePrimitiveColliderRig())
            {
                enabled = false;
                return;
            }

            _renderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            CacheBiolumPresentationLights();
            EnsureFaunaPresentationMaterials();
            ApplyFaunaPresentationShaderState(1f, 0f, 0f, 0f);
            CacheLogicalLodComponents();
            TryGetComponent(out _faunaKinematicsRuntime);
            TryGetComponent(out _simplifiedRagdollHandoff);
            TryGetComponent(out _scannableTarget);
            ResolveFoveatedBindings();
            _tickStaggerShift = ResolveDeterministicTickStaggerShift();
            _simulationBucketId = _tickStaggerShift;

            // Inject profile into subsystems
            _steeringEngine.Init(_rb, transform, _speciesProfile);
            _sensorSuite.Init(this, _speciesProfile);
            _utilityBrain.Initialize(ResolveSelfRuntimePositionOrZero(), _speciesProfile, _archetype, _faunaDataTemplate);
            if (_archetype != null)
                ApplyArchetype(_archetype);
            else if (_faunaDataTemplate != null)
                ApplyFaunaDataTemplate(_faunaDataTemplate);
            ConfigureFaunaScanMetadata();
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            EnsureLeviathanPresentationOwner();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            CacheCorpseSinkVaultCold();
            RegisterOriginShiftListener();

            if (!_dispatcherRegistered)
            {
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_dispatcherRegistered)
                return;

            RegisterSpatialHandle();
            RefreshSimulationBucketerBinding();
            InvalidatePlayerRuntimeContextCache();
            RefreshCachedPlayerTransformReference();
            _utilityBrain.SetRuntimeActive(true);
            ResetDispatcherCadence();
            RefreshMimicOcclusionRuntimeOwner();
            TryRegisterCombatDamageTarget();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregisterCombatDamageTarget();
            UnregisterTier1LodProxy();

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            TryUnregisterHotSwapListener();

            ClearInfectionHazardRegistration();
            ClearCachedEcosystemDirectorReference();
            ClearSimulationBucketerBinding();
            UnregisterSpatialHandle();
            UnregisterOriginShiftListener();
            _utilityBrain.SetRuntimeActive(false);
            ResetDispatcherCadence();
            ClearVoxelPathGuidance();
            ClearDirectorHuntTarget();
            ClearHibernationStarvationHuntCommand();
            ClearEcholocationMimicSignal();
            ClearPredatorLungeCheat();
            ClearPredatorSquadState();
            ReleaseCorpseSinkingKinematicsBuffers();
            ReleaseMimicOcclusionRuntimeOwner();
            InvalidatePlayerRuntimeContextCache();
        }

        private void OnDestroy()
        {
            TryUnregisterCombatDamageTarget();

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            TryUnregisterHotSwapListener();
            ClearSimulationBucketerBinding();

            UnregisterTier1LodProxy();
            UnregisterSpatialHandle();
            ClearInfectionHazardRegistration();
            UnregisterOriginShiftListener();
            _utilityBrain.Dispose();
            ClearVoxelPathGuidance();
            ClearDirectorHuntTarget();
            ClearHibernationStarvationHuntCommand();
            ClearEcholocationMimicSignal();
            ClearPredatorLungeCheat();
            ClearPredatorSquadState();
            ReleaseCorpseSinkingKinematicsBuffers();
            ReleaseMimicOcclusionRuntimeOwner();
            ReleaseFaunaPresentationMaterials();
            InvalidatePlayerRuntimeContextCache();
        }

        public void OnSpawn()
        {
            _isDead = false;
            _deathSpiralActive = false;
            _deathSpiralStartTime = 0f;
            _deathSpiralTorque = Vector3.zero;
            _deathCorkscrewPhaseX = 0f;
            _deathCorkscrewPhaseZ = 0f;
            _deathDitherFade01 = 0f;
            _corpseBloatAge01 = 0f;
            _whaleFallDecay01 = 0f;
            _hitFlash01 = 0f;
            Vector3 corpseRuntimePosition = _rb != null ? _rb.position : Vector3.zero;
            _corpseFloorY = corpseRuntimePosition.y;
            _corpseFloorLatched = false;
            _corpseSinkJobScheduled = false;
            _corpseSinkJobHandle = default;
            _corpseSinkPoseDeltaTime = 0f;
            CacheCorpseSinkVaultCold();
            TryUnregisterCorpseSinkLateFrame();
            _passiveFlashlightDimUntilTime = 0f;
            _retinalBlindBiolumUntilTime = 0f;
            _lastRetinalBlindSignalFrame = uint.MaxValue;
            _faunaBiolumDim01 = 1f;
            _lastAppliedBiolumLightScale01 = -1f;
            _lastAppliedFaunaBiolumShader01 = -1f;
            _lastAppliedDeathDitherShader01 = -1f;
            _lastAppliedCorpseBloatShader01 = -1f;
            _lastAppliedDecayAmountShader01 = -1f;
            _lastAppliedHitFlashShader01 = -1f;
            _lastAppliedMutationHueShader01 = -1f;
            _lastAppliedMutationTwitchShader01 = -1f;
            _lastAppliedGeneticMask = ulong.MaxValue;
            _lastAppliedInfectionShaderSeverity01 = -1f;
            _lastAppliedInfectionShaderActive = false;
            ClearQueuedPresentationSyncState();
            ClearPredatorDeafening();
            ClearPredatorSquadState();
            ClearDirectorHuntTarget();
            ClearPredatorLungeCheat();
            ClearAttackTelegraphState();
            ClearCorpseLatchState();
            RestoreBaseRigidbodyPresentationState();
            if (!TryResolveAupFromRuntimeOrigin(corpseRuntimePosition, out _corpseSinkAup))
                _corpseSinkAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            ApplyBiolumPresentationLightScale(1f);
            ApplyFaunaPresentationShaderState(1f, 0f, 0f, 0f);
            ResetCorpseBloatShaderTimer();
            _tier2HibernationRecordWritten = false;
            _tier2HibernationHandoffInProgress = false;
            _breachDragBypassUntilTime = 0f;
            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            UnregisterTier1LodProxy();
            SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            SetDiseasedState(false, 0f);
            _currentHealth = _maxHealth;
            TryRegisterCombatDamageTarget();
            MarkCombatDamageSyncDirty();
            _utilityBrain.ResetRuntimeState(ResolveSelfRuntimePositionOrZero());
            _utilityBrain.SetRuntimeActive(true);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            ConfigureFaunaScanMetadata();
            RefreshRuntimeEcosystemState();
            RegisterSpatialHandle();
            RefreshSimulationBucketerBinding();
            RegisterOriginShiftListener();
            InvalidatePlayerRuntimeContextCache();
            RefreshCachedPlayerTransformReference();
            ResetDispatcherCadence();
            ClearProceduralStrikeIntent();
            ClearHibernationStarvationHuntCommand();
            ResetEggClutchCadence();
            RefreshMimicOcclusionRuntimeOwner();
            ApplyPassiveRigidbodyCastrationIfRequired();
        }

        public void OnDespawn()
        {
            TryUnregisterCombatDamageTarget();
            _isDead = true;
            _deathSpiralActive = false;
            _deathSpiralStartTime = 0f;
            _deathSpiralTorque = Vector3.zero;
            _deathCorkscrewPhaseX = 0f;
            _deathCorkscrewPhaseZ = 0f;
            _deathDitherFade01 = 0f;
            _corpseBloatAge01 = 0f;
            _whaleFallDecay01 = 0f;
            _hitFlash01 = 0f;
            _passiveFlashlightDimUntilTime = 0f;
            _retinalBlindBiolumUntilTime = 0f;
            _lastRetinalBlindSignalFrame = uint.MaxValue;
            _faunaBiolumDim01 = 1f;
            _lastAppliedBiolumLightScale01 = -1f;
            _lastAppliedFaunaBiolumShader01 = -1f;
            _lastAppliedDeathDitherShader01 = -1f;
            _lastAppliedCorpseBloatShader01 = -1f;
            _lastAppliedDecayAmountShader01 = -1f;
            _lastAppliedHitFlashShader01 = -1f;
            _lastAppliedMutationHueShader01 = -1f;
            _lastAppliedMutationTwitchShader01 = -1f;
            _lastAppliedGeneticMask = ulong.MaxValue;
            _lastAppliedInfectionShaderSeverity01 = -1f;
            _lastAppliedInfectionShaderActive = false;
            ClearQueuedPresentationSyncState();
            ClearPredatorDeafening();
            ClearPredatorSquadState();
            ClearDirectorHuntTarget();
            ClearPredatorLungeCheat();
            ClearAttackTelegraphState();
            ClearCorpseLatchState();
            ApplyBiolumPresentationLightScale(1f);
            ApplyFaunaPresentationShaderState(1f, 0f, 0f, 0f);
            ResetCorpseBloatShaderTimer();
            _playerNoiseEmitterTransform = null;
            _tier2HibernationHandoffInProgress = false;
            _breachDragBypassUntilTime = 0f;
            RestoreBaseRigidbodyPresentationState();
            UnregisterTier1LodProxy();
            SetLogicalLodTier(FaunaLogicalLodTier.Hibernating);
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            SetDiseasedState(false, 0f);
            TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
            TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
            _utilityBrain.ResetRuntimeState(ResolveSelfRuntimePositionOrZero());
            _utilityBrain.SetRuntimeActive(false);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            ClearInfectionHazardRegistration();
            ClearCachedEcosystemDirectorReference();
            ClearSimulationBucketerBinding();
            UnregisterSpatialHandle();
            UnregisterOriginShiftListener();
            ResetDispatcherCadence();
            InvalidatePlayerRuntimeContextCache();
            ClearProceduralStrikeIntent();
            ClearVoxelPathGuidance();
            ClearHibernationStarvationHuntCommand();
            _nextEggClutchTimeSeconds = 0f;
            _eggClutchSequence = 0u;
            ClearEcholocationMimicSignal();
            ReleaseMimicOcclusionRuntimeOwner();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK PIPELINE (Absolute Zero GC)
        // ══════════════════════════════════════════════════════════
        private FaunaPerceptionSnapshot BuildFaunaPerceptionSnapshot()
        {
            FaunaPerceptionSnapshot snapshot = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            bool hasRuntimeContext = TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                                     runtimeContext != null;
            PlayerMovementRuntimeState movementState = hasRuntimeContext ? runtimeContext.MovementState : default;
            bool hasMovementRoot = hasRuntimeContext &&
                                   (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            bool hasMovementState = hasRuntimeContext &&
                                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u;
            PlayerLookState lookState = hasRuntimeContext ? runtimeContext.LookState : default;
            bool hasLookState = hasRuntimeContext &&
                                (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                                math.lengthsq(lookState.AimForward) > 0.0001f;
            Transform playerTransform = hasRuntimeContext && runtimeContext.PlayerTransform != null
                ? runtimeContext.PlayerTransform
                : playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
                _currentCullingPlayerTransform = playerTransform;
            else
                playerTransform = _currentCullingPlayerTransform;

            if (hasLookState || hasMovementRoot)
            {
                snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayer;
                if (hasLookState)
                {
                    snapshot.PlayerPosition = ToVector3(lookState.EyePosition);
                    if (TryResolveAupFromRuntimeOrigin(snapshot.PlayerPosition, out AbsoluteUniversePosition playerAup))
                    {
                        snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayerAup;
                        snapshot.PlayerAup = playerAup;
                    }
                }
                else
                {
                    snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayerAup;
                    snapshot.PlayerAup = movementState.PredictedAup;
                    snapshot.PlayerPosition = ToVector3(snapshot.PlayerAup.ToRuntimeFloat3());
                }

                snapshot.PlayerForward = hasLookState
                    ? ToVector3(lookState.AimForward)
                    : ToVector3(movementState.Forward);
                if (hasLookState || snapshot.PlayerForward.sqrMagnitude > 0.0001f)
                    snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayerForward;
                if (playerTransform != null)
                    EnsurePlayerNoiseEmitterBound(playerTransform);
            }

            if (hasMovementState)
            {
                snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayerVelocity;
                snapshot.PlayerVelocity = ToVector3(movementState.Velocity);
            }
            else
            {
                if (TryGetLatestKccVelocityVector(KccVelocityFaunaMaxAgeFrames, out Vector3 kccVelocity))
                {
                    snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasPlayerVelocity;
                    snapshot.PlayerVelocity = kccVelocity;
                }
            }

            PlayerFlashlight flashlight = hasRuntimeContext && runtimeContext.Flashlight != null
                ? runtimeContext.Flashlight
                : playerContext != null ? playerContext.Flashlight : null;
            if (flashlight != null && flashlight.IsOn)
                snapshot.Flags |= FaunaPerceptionSnapshot.FlagPlayerFlashlightOn;

            PlayerToolManager toolManager = hasRuntimeContext && runtimeContext.ToolManager != null
                ? runtimeContext.ToolManager
                : playerContext != null ? playerContext.ToolManager : null;
            PlayerTool currentTool = toolManager != null ? toolManager.CurrentTool : null;
            if (currentTool != null && (hasLookState || hasMovementState))
            {
                Vector3 scavengeToolPosition = hasLookState
                    ? ToVector3(lookState.EyePosition + lookState.AimForward * ScavengeToolLookOffsetMeters)
                    : ToVector3(movementState.PredictedAup.ToRuntimeFloat3());
                snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasScavengeTool;
                snapshot.ScavengeToolPosition = scavengeToolPosition;
                if (hasLookState)
                {
                    if (TryResolveAupFromRuntimeOrigin(scavengeToolPosition, out AbsoluteUniversePosition toolAup))
                    {
                        snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasScavengeToolAup;
                        snapshot.ScavengeToolAup = toolAup;
                    }
                }
                else
                {
                    snapshot.Flags |= FaunaPerceptionSnapshot.FlagHasScavengeToolAup;
                    snapshot.ScavengeToolAup = movementState.PredictedAup;
                }
                snapshot.ScavengeToolOwner = currentTool;
            }

            return snapshot;
        }

        private void EnsurePlayerNoiseEmitterBound(Transform playerTransform)
        {
            if (playerTransform == null || _playerNoiseEmitterTransform == playerTransform)
                return;

            PlayerNoiseEmitter.EnsureAttached(playerTransform);
            _playerNoiseEmitterTransform = playerTransform;
        }

        private bool TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            int frame = Time.frameCount;
            if (_playerRuntimeContextCacheFrame != frame)
            {
                _playerRuntimeContextCacheFrame = frame;
                _playerRuntimeContextCacheValid =
                    PlayerRuntimeContextService.TryGetActiveRuntimeContext(out _playerRuntimeContextCache) &&
                    _playerRuntimeContextCache != null;
                if (!_playerRuntimeContextCacheValid)
                    _playerRuntimeContextCache = null;
            }

            runtimeContext = _playerRuntimeContextCache;
            return _playerRuntimeContextCacheValid;
        }

        private void InvalidatePlayerRuntimeContextCache()
        {
            _playerRuntimeContextCacheFrame = -1;
            _playerRuntimeContextCacheValid = false;
            _playerRuntimeContextCache = null;
        }

        private void RefreshColdRegistryDependencies()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _physicsService = GlobalRegistry.Physics;
            _steeringEngine.BindPhysicsService(_physicsService);
            _ambientCurrentReadModel = GlobalRegistry.AmbientCurrent;
            _objectPool = GlobalRegistry.ObjectPoolService;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _hazardZones = GlobalRegistry.HazardZoneReadModel;
            _atmosphereRuntime = GlobalRegistry.AtmosphereReadModel;
            _sargassumMicroFauna = GlobalRegistry.MicroFaunaPresentationPulses;
            _vegetationThreatPulseSink = GlobalRegistry.VegetationThreatPulses;
            _simulationBucketerRuntime = GlobalRegistry.SimulationBucketer;
            RefreshCachedEcosystemDirectorReference();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    InvalidatePlayerRuntimeContextCache();
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    _steeringEngine.BindPhysicsService(_physicsService);
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as IFaunaPersistentWorldStateService;
                    break;
                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    _hazardZones = currentService as IHazardZoneReadModel;
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _atmosphereRuntime = currentService as IAtmosphereReadModel;
                    break;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:
                    _sargassumMicroFauna = currentService as IMicroFaunaPresentationPulseSink;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _vegetationThreatPulseSink = currentService as IVegetationThreatPulseSink;
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketerRuntime = currentService as ISimulationBucketer;
                    if (!ReferenceEquals(_simulationBucketer, _simulationBucketerRuntime))
                        ClearSimulationBucketerBinding();
                    break;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    BindCachedEcosystemDirectorReference(currentService as IEcosystemDirectorService);
                    break;
            }
        }

        private static Transform ResolveSensorTargetTransform(Component owner)
        {
            return owner != null ? owner.transform : null;
        }

        private bool TryResolveDirectPlayerTransform(out Transform playerTransform)
        {
            playerTransform = null;
            if (!_sensorSuite.hasVisualPlayerContact)
                return false;

            bool hasRuntimeContext = TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                                     runtimeContext != null;
            playerTransform = hasRuntimeContext ? runtimeContext.PlayerTransform : null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            playerTransform ??= playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
                _currentCullingPlayerTransform = playerTransform;
            else
                playerTransform = _currentCullingPlayerTransform;

            return playerTransform != null;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            TryFlushCombatDamageSync();
            _cognitionTimeSeconds += dt;
            if (_isDead)
            {
                ApplyDeathSpiralFixedStep(dt);
                UpdateDeathSpiralPresentation(dt);
                return;
            }

            if (_foveatedSimulationTier == FoveatedSimulationTier.Frozen && !_foveatedTier0Locked)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                return;
            }

            bool forceAggroTick = ShouldForceAggroCognitionTick();
            ResolveLogicalLodTier();
            if (ShouldUseDeadZoneColdTick())
                ApplyDirectorColdTickCull(true);

            if (TryApplyDirectorColdTickGate(dt, forceAggroTick))
                return;

            if (_logicalLodTier != FaunaLogicalLodTier.FullSim && !forceAggroTick)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                if (_logicalLodTier == FaunaLogicalLodTier.DataOnly)
                {
                    RefreshTier1LodProxy(FaunaLogicalLodTier.DataOnly);
                    if (_rb != null && !_rb.IsSleeping())
                        _rb.Sleep();
                }

                return;
            }

            if (_foveatedTickRate == FoveatedTickRate.CulledEcosystemOnly && !forceAggroTick)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                return;
            }

            if (!TryResolveSelfLogicPosition(out Vector3 runtimeSelfPosition))
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimeSelfPosition, out AbsoluteUniversePosition runtimeSelfAup))
                return;

            Vector3 runtimeSelfForward = ResolveSelfLogicForward();
            FaunaPerceptionSnapshot perceptionSnapshot = BuildFaunaPerceptionSnapshot();
            _sensorSuite.Tick(
                dt,
                runtimeSelfPosition,
                runtimeSelfForward,
                _rb.linearVelocity,
                in runtimeSelfAup,
                in perceptionSnapshot,
                _cognitionTimeSeconds,
                forceAggroTick);
            _lodDisabled = _sensorSuite.lodDisabled;

            if (_lodDisabled || _sensorSuite.isSleeping)
            {
                FixedTick(dt);
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                return;
            }

            AIState oldState = _currentStateCache;
            float3 selfPosition = runtimeSelfPosition;
            CreatureUtilityEvaluation utilityEvaluation = EvaluateCognitionBrain(Time.frameCount, dt, selfPosition, out Transform attackTarget);
            ApplyCognitionEvaluation(in utilityEvaluation);
            bool predatorStunnedActive = TryApplyPredatorPhotophobia(selfPosition, in perceptionSnapshot);
            bool predatorDeafenedActive = !predatorStunnedActive && TryApplyPredatorDeafenedWander(selfPosition);
            bool passiveFlashlightOverrideActive = !predatorStunnedActive && TryApplyPassiveFlashlightReaction(selfPosition);
            if (predatorStunnedActive || predatorDeafenedActive || passiveFlashlightOverrideActive)
                attackTarget = null;

            TryPublishLeviathanSectorEntryScatter(selfPosition);
            ApplyVoxelPathGuidance(selfPosition, utilityEvaluation.LegacyState);
            bool ecologyOverrideActive = !predatorStunnedActive &&
                                          !predatorDeafenedActive &&
                                          !passiveFlashlightOverrideActive &&
                                          !IsApexPredator() &&
                                          ApplyEcologyChainOverrides(selfPosition, dt);
            if (ecologyOverrideActive)
                attackTarget = null;

            UpdateBioluminescentHypnosis();
            UpdateFaunaBiolumPresentation(dt);
            UpdateEcholocationMimicry();
            UpdateProceduralStrikeIntent(_currentStateCache, attackTarget);
            UpdateProceduralHeadLookIntent();
            EmitLeviathanThreatPulse(in utilityEvaluation);
            if (!predatorStunnedActive && !predatorDeafenedActive && !ecologyOverrideActive && CreatureUtilityEvaluation.ShouldAttack(in utilityEvaluation) && attackTarget != null)
            {
                if (TryAdvanceAttackTelegraph(attackTarget))
                {
                    HandleAttackPerform(attackTarget);
                    float attackCooldown = _speciesProfile != null ? _speciesProfile.attackCooldown : 1f;
                    _utilityBrain.NotifyAttackPerformed(_cognitionTimeSeconds, attackCooldown);
                    ClearAttackTelegraphState();
                }
            }
            else
            {
                ClearAttackTelegraphState();
            }

            if (_sensorSuite.isAvoidingObstacle)
            {
                Vector3 blendedAvoidanceDirection = ResolveDominantAxisDirection(ToVector3(math.lerp(
                    (float3)_cachedDesiredDirection,
                    (float3)_sensorSuite.bestFreeDirection,
                    0.7f)));
                _cachedDesiredDirection = blendedAvoidanceDirection;
                if (_sensorSuite.IsStuck && _sensorSuite.hasEscapePOI)
                {
                    Vector3 poiDir = ResolveDominantAxisDirection(_sensorSuite.currentEscapePOI - ToVector3(selfPosition));
                    _cachedDesiredDirection = ResolveDominantAxisDirection(ToVector3(math.lerp(
                        (float3)blendedAvoidanceDirection,
                        (float3)poiDir,
                        0.6f)));
                }
            }

            // [REQ] Procedural Eye Tracking (The "Stare")
            UpdateEyeTracking(dt, ToVector3(selfPosition));
            FixedTick(dt);
            AdvanceSlowTickCadence(dt);
        }

        private void UpdateEyeTracking(float dt, Vector3 selfPosition)
        {
            Vector3 selfForward = ResolveSelfLogicForward();
            if (_corpseLatchActive)
            {
                Vector3 toCorpse = _corpseLatchCenterPosition - selfPosition;
                if (toCorpse.sqrMagnitude <= 0.0001f)
                    toCorpse = selfForward;

                Vector3 baseDirection = ResolveDominantAxisDirection(toCorpse);
                LookDirection = ResolveDominantAxisDirection(baseDirection + (Vector3.up * _corpseTearingPitchRadians));
                return;
            }

            if (_speciesProfile == null || _speciesProfile.eyeTrackWeight <= 0.01f)
            {
                LookDirection = selfForward;
                return;
            }

            Vector3 targetPos = Vector3.zero;
            bool hasTarget = false;

            // Priority: Threat > Distractor > Player > Prey
            if (_sensorSuite.hasCurrentThreat) { targetPos = _sensorSuite.currentThreatPosition; hasTarget = true; }
            else if (_sensorSuite.hasCurrentDistractor) { targetPos = _sensorSuite.currentDistractorPosition; hasTarget = true; }
            else if (_sensorSuite.canSeePlayer && _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerTargetPos)) { targetPos = playerTargetPos; hasTarget = true; }
            else if (_sensorSuite.hasCurrentPrey) { targetPos = _sensorSuite.currentPreyPosition; hasTarget = true; }

            if (hasTarget)
            {
                float distSqr = (targetPos - selfPosition).sqrMagnitude;
                if (distSqr < _speciesProfile.eyeTrackRange * _speciesProfile.eyeTrackRange)
                {
                    Vector3 toTarget = ResolveDominantAxisDirection(targetPos - selfPosition);
                    LookDirection = ResolveDominantAxisDirection(ToVector3(math.lerp(
                        (float3)selfForward,
                        (float3)toTarget,
                        math.saturate(_speciesProfile.eyeTrackWeight))));
                    return;
                }
            }

            LookDirection = ResolveDominantAxisDirection(ToVector3(math.lerp(
                (float3)LookDirection,
                (float3)selfForward,
                math.saturate(5f * dt))));
        }

        private CreatureUtilityEvaluation EvaluateCognitionBrain(
            int frameId,
            float dt,
            float3 selfPosition,
            out Transform attackTarget)
        {
            bool hasPlayerTarget = _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition);
            bool hasPlayerVelocity = _sensorSuite.TryGetPerceivedPlayerVelocity(out Vector3 playerVelocity);
            bool hasPlayerForward = _sensorSuite.TryGetPerceivedPlayerForward(out Vector3 playerForward);
            bool hasDirectPlayerTransform = TryResolveDirectPlayerTransform(out Transform directPlayerTransform);
            bool hasThreatTarget = _sensorSuite.hasCurrentThreat;
            bool hasPreyTarget = _sensorSuite.hasCurrentPrey;
            bool hasScavengeTarget = _sensorSuite.hasCurrentScavengeTarget;
            float fearPressure01 = _sensorSuite.isThreatened ? 0.35f : 0f;
            bool hasHazardScatterDirection = _sensorSuite.isScattering;
            float3 scatterDirection = _sensorSuite.scatterDirection;
            if (hasThreatTarget)
                fearPressure01 += 0.2f;
            if (_utilityBrain.UsesPredatorRole != 0)
            {
                HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
                if (vegetationBridge != null)
                {
                    int speciesId = ComputeStableSpeciesId();
                    fearPressure01 += vegetationBridge.SamplePredatorFearPressure(selfPosition, speciesId);
                }

                IHazardZoneReadModel hazardZoneManager = _hazardZones;
                if (hazardZoneManager != null &&
                    hazardZoneManager.TrySampleHazardAvoidance(ToVector3(selfPosition), PredatorHazardAvoidanceRadius, out Vector3 hazardFleeDirection, out float hazardPressure01))
                {
                    fearPressure01 += hazardPressure01;
                    if (hazardPressure01 > PredatorHazardFearThreshold)
                    {
                        hasHazardScatterDirection = true;
                        scatterDirection = hazardFleeDirection;
                    }
                }
            }

            Vector3 rigidbodyVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            float3 selfVelocity = new float3(rigidbodyVelocity.x, rigidbodyVelocity.y, rigidbodyVelocity.z);
            float3 selfForward = ResolveSelfLogicForward();
            float attackRange = _speciesProfile != null ? _speciesProfile.attackRadius : math.max(1f, _stateMachine.attackRadius);
            float fogEndDistanceMeters = ResolveCurrentFogEndDistanceMeters();
            float baseMaxSpeedMetersPerSecond = math.max(0.1f, _steeringEngine.maxSpeed);
            float wanderRadius = math.max(1f, _stateMachine.wanderRadius);
            float patrolRadius = math.max(1f, _stateMachine.patrolRadius);
            bool isApexPredator = IsApexPredator();
            bool useAlphaLeviathanCognition = ShouldUseAlphaLeviathanCognition();
            float apexTerritoryRadius = ResolveApexTerritoryRadius();
            float apexAggressionMultiplier = ResolveApexAggressionMultiplier();
            float playerLightExposure01 = ResolvePlayerLightExposure01(
                selfPosition,
                directPlayerTransform,
                out Transform lightPlayerTransform,
                out Vector3 lightPlayerPosition);
            if (playerLightExposure01 > 0.01f && lightPlayerTransform != null)
            {
                hasPlayerTarget = true;
                hasDirectPlayerTransform = true;
                directPlayerTransform = lightPlayerTransform;
                playerPosition = lightPlayerPosition;

                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null &&
                    TryGetLatestKccVelocityVector(KccVelocityFaunaMaxAgeFrames, out Vector3 kccVelocity))
                {
                    hasPlayerVelocity = true;
                    playerVelocity = kccVelocity;
                }
            }

            bool hasApexRivalTarget = false;
            Vector3 apexRivalPosition = default;
            _apexRivalTarget = null;
            if (isApexPredator &&
                TryResolveNearestRivalApex(selfPosition, apexTerritoryRadius, out IFaunaSpatialContact rivalContact, out Vector3 rivalPosition))
            {
                _apexRivalTarget = rivalContact != null ? rivalContact.ContactTransform : null;
                if (_apexRivalTarget != null)
                {
                    hasThreatTarget = true;
                    hasApexRivalTarget = true;
                    apexRivalPosition = rivalPosition;
                }
            }
            else if (!hasThreatTarget &&
                     TryResolveApexIntimidationThreat(selfPosition, out Vector3 intimidationThreatPosition))
            {
                hasThreatTarget = true;
                fearPressure01 += 0.2f;
                scatterDirection = ResolveDominantAxisDirection(ToVector3(selfPosition) - intimidationThreatPosition);
                hasHazardScatterDirection = math.lengthsq(scatterDirection) > 0.0001f;
            }

            CreatureUtilityContext context = new CreatureUtilityContext(
                ToVector3(selfPosition),
                ToVector3(selfVelocity),
                ToVector3(selfForward),
                hasPlayerTarget ? playerPosition : default,
                hasPlayerForward ? playerForward : ToVector3(selfForward),
                hasPlayerVelocity ? playerVelocity : default,
                hasThreatTarget
                    ? (hasApexRivalTarget
                        ? apexRivalPosition
                        : _sensorSuite.currentThreatPosition)
                    : default,
                hasApexRivalTarget ? apexRivalPosition : default,
                hasPreyTarget ? _sensorSuite.currentPreyPosition : default,
                hasScavengeTarget ? _sensorSuite.currentScavengeTargetPosition : default,
                _sensorSuite.flockCenter,
                _sensorSuite.flockDirection,
                _sensorSuite.flockAvoidance,
                scatterDirection,
                HealthNormalized,
                _sensorSuite.distSqrToPlayer,
                attackRange,
                fogEndDistanceMeters,
                baseMaxSpeedMetersPerSecond,
                math.saturate(fearPressure01),
                ResolveFleeHealthThreshold(),
                _stateMachine.escapeDistance,
                _stateMachine.escapeSafeDistance,
                wanderRadius,
                patrolRadius,
                apexTerritoryRadius,
                apexAggressionMultiplier,
                playerLightExposure01,
                math.saturate(_foveatedImportanceScore),
                _sensorSuite.flockCount,
                canFlee,
                _sensorSuite.hasVisualPlayerContact,
                hasPlayerTarget,
                hasThreatTarget,
                hasApexRivalTarget,
                hasPreyTarget,
                hasScavengeTarget,
                _stateMachine.useTerritory,
                _stateMachine.isFlockingFish,
                hasHazardScatterDirection,
                isAggressive,
                isApexPredator,
                useAlphaLeviathanCognition);

            CreatureUtilityEvaluation evaluation = _utilityBrain.Evaluate(frameId, dt, _cognitionTimeSeconds, in context);
            if (CreatureUtilityEvaluation.HasAcousticHeadLook(in evaluation))
            {
                _acousticHeadLookTarget = evaluation.AcousticHeadLookTarget;
                _acousticHeadLookWeight = math.saturate(evaluation.AcousticHeadLookWeight);
                _acousticHeadLookUntilTime = _cognitionTimeSeconds + 0.25f;
            }
            else if (_cognitionTimeSeconds > _acousticHeadLookUntilTime)
            {
                _acousticHeadLookWeight = 0f;
            }

            Transform scavengeTargetTransform = ResolveSensorTargetTransform(_sensorSuite.currentScavengeTargetOwner);
            Transform distractorTargetTransform = ResolveSensorTargetTransform(_sensorSuite.currentDistractorOwner);
            Transform preyTargetTransform = ResolveSensorTargetTransform(_sensorSuite.currentPreyOwner);
            attackTarget = _apexRivalTarget ??
                           scavengeTargetTransform ??
                           _baitFeedingTarget ??
                           distractorTargetTransform ??
                           directPlayerTransform ??
                           preyTargetTransform;
            return evaluation;
        }

        private float ResolveCurrentFogEndDistanceMeters()
        {
            IAtmosphereReadModel atmosphere = _atmosphereRuntime;
            return atmosphere != null
                ? math.max(1f, atmosphere.CurrentFogAttenuationDistance)
                : 80f;
        }

        private float ResolvePlayerLightExposure01(
            float3 selfPosition,
            Transform directPlayerTransform,
            out Transform lightPlayerTransform,
            out Vector3 lightPosition)
        {
            lightPlayerTransform = null;
            lightPosition = default;
            if (_faunaDataTemplate == null || _faunaDataTemplate.LightReactionMode == FaunaLightReactionMode.None)
                return 0f;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            bool hasRuntimeContext = TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                                     runtimeContext != null;
            PlayerLookState lookState = hasRuntimeContext ? runtimeContext.LookState : default;
            bool hasLookState = hasRuntimeContext &&
                                (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                                math.lengthsq(lookState.AimForward) > 0.0001f;
            PlayerMovementRuntimeState movementState = hasRuntimeContext ? runtimeContext.MovementState : default;
            bool hasMovementRoot = hasRuntimeContext &&
                                   (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            bool hasMovementForward = hasRuntimeContext &&
                                      (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u &&
                                      math.lengthsq(movementState.Forward) > 0.0001f;
            Transform playerTransform = hasRuntimeContext && runtimeContext.PlayerTransform != null
                ? runtimeContext.PlayerTransform
                : playerContext != null ? playerContext.PlayerTransform : directPlayerTransform;
            PlayerFlashlight flashlight = hasRuntimeContext && runtimeContext.Flashlight != null
                ? runtimeContext.Flashlight
                : playerContext != null ? playerContext.Flashlight : null;
            if (flashlight == null || !flashlight.IsOn || (!hasLookState && !hasMovementRoot))
                return 0f;

            Vector3 listenerPosition = ToVector3(selfPosition);
            lightPosition = hasLookState
                ? ToVector3(lookState.EyePosition)
                : ToVector3(movementState.PredictedAup.ToRuntimeFloat3());
            float3 lightForward = hasLookState
                ? lookState.AimForward
                : hasMovementForward ? movementState.Forward : float3.zero;
            float forwardLenSq = math.lengthsq(lightForward);
            if (forwardLenSq <= 0.0001f)
                return 0f;

            if (!TryResolveAupFromRuntimeOrigin(listenerPosition, out AbsoluteUniversePosition listenerAup))
                return 0f;

            AbsoluteUniversePosition lightAup;
            if (hasLookState)
            {
                if (!TryResolveAupFromRuntimeOrigin(lightPosition, out lightAup))
                    return 0f;
            }
            else
            {
                lightAup = movementState.PredictedAup;
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in listenerAup, in lightAup);
            if (distanceSq <= 0.0001d)
            {
                lightPlayerTransform = playerTransform;
                return 1f;
            }

            float range = _faunaDataTemplate.LightReactionRangeMeters;
            double rangeSq = (double)range * range;
            if (distanceSq > rangeSq)
                return 0f;

            float dotThreshold = _faunaDataTemplate.LightReactionDotThreshold;
            double3 toListener = AbsoluteUniversePosition.DeltaMetersClamped(in listenerAup, in lightAup);
            double rawDot =
                ((double)lightForward.x * toListener.x) +
                ((double)lightForward.y * toListener.y) +
                ((double)lightForward.z * toListener.z);
            if (rawDot <= 0d)
                return 0f;

            double scaledConeSq = (double)dotThreshold * dotThreshold * (double)forwardLenSq * distanceSq;
            double rawDotSq = rawDot * rawDot;
            if (rawDotSq < scaledConeSq)
                return 0f;

            double maxConeSq = (double)forwardLenSq * distanceSq;
            float cone01 = math.saturate((float)((rawDotSq - scaledConeSq) * math.rcp(math.max(0.0001d, maxConeSq - scaledConeSq))));
            float distance01 = 1f - math.saturate((float)(distanceSq * math.rcp(rangeSq)));
            float exposure01 = math.saturate(cone01 * distance01);
            if (_utilityBrain.IsActivePredator != 0)
            {
                IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
                if (ecosystemDirector != null)
                    exposure01 *= 1f - ecosystemDirector.ResolveEclipsePredatorLightSuppression01(listenerPosition);
            }

            if (exposure01 <= 0.01f)
                return 0f;

            lightPlayerTransform = playerTransform;
            return exposure01;
        }

        private bool ShouldForceAggroCognitionTick()
        {
            if (_isDead || _utilityBrain.IsActivePredator == 0)
                return false;

            if (_sensorSuite.hasVisualPlayerContact || _sensorSuite.hasNoisePlayerContact)
                return true;

            PredatorUtilityState stateMask = _utilityBrain.CurrentStateMask;
            return stateMask == PredatorUtilityState.Stalking ||
                   stateMask == PredatorUtilityState.Attacking ||
                   _currentStateCache == AIState.Stalk ||
                   _currentStateCache == AIState.Aggressive;
        }

        private bool TryApplyDirectorColdTickGate(float dt, bool forceAggroTick)
        {
            if (_directorColdTickUntilTime <= _cognitionTimeSeconds || forceAggroTick)
            {
                _directorColdTickAccumulator = 0f;
                return false;
            }

            _directorColdTickAccumulator += dt;
            if (_directorColdTickAccumulator >= DirectorColdTickIntervalSeconds)
            {
                _directorColdTickAccumulator = 0f;
                return false;
            }

            AdvanceSlowTickCadence(dt);
            ClearProceduralStrikeIntent();
            ClearEcholocationMimicSignal();
            if (_rb != null && !_rb.IsSleeping())
                _rb.Sleep();
            return true;
        }

        private bool ShouldUseDeadZoneColdTick()
        {
            return !_isDead &&
                   _utilityBrain.IsActivePredator != 0 &&
                   !_foveatedInsideFrustum &&
                   _foveatedSimulationTier == FoveatedSimulationTier.Peripheral;
        }

        private void ApplyVoxelPathGuidance(float3 selfPosition, AIState resolvedState)
        {
            if (_utilityBrain.IsActivePredator == 0 ||
                _isDead ||
                (resolvedState != AIState.Stalk && resolvedState != AIState.Aggressive))
            {
                ClearVoxelPathGuidance();
                return;
            }

            if (ShouldPauseVoxelRouteConsumptionForOriginShift())
                return;

            if (!_sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition) &&
                !TryResolveDirectorHuntTarget(out playerPosition))
            {
                ClearVoxelPathGuidance();
                return;
            }

            if (TryApplyBurrowAmbushPathGuidance(selfPosition, playerPosition))
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
            {
                ClearVoxelPathGuidance();
                return;
            }

            Vector3 targetPosition = ResolvePredictedPlayerGuidanceTarget(selfPosition, playerPosition);
            bool requiresRefresh = !_hasVoxelRouteTarget ||
                                   (_voxelRouteTargetPosition - targetPosition).sqrMagnitude > VoxelRouteRetargetDistanceSqr ||
                                   _cognitionTimeSeconds >= _nextVoxelRouteRefreshTime;
            if (requiresRefresh)
            {
                if (vegetationBridge.TryBuildImmediateAbyssalVoxelRoute(ToVector3(selfPosition), targetPosition, _voxelRouteWaypoints, out int waypointCount))
                {
                    _voxelRouteWaypointCount = waypointCount;
                    _voxelRouteTargetPosition = targetPosition;
                    _hasVoxelRouteTarget = waypointCount >= 2;
                    CacheVoxelRouteAupState(waypointCount, targetPosition);
                    _nextVoxelRouteRefreshTime = _cognitionTimeSeconds + VoxelRouteRefreshIntervalSeconds;
                }
                else
                {
                    ClearVoxelPathGuidance();
                    return;
                }
            }

            if (_voxelRouteWaypointCount < 2)
                return;

            int waypointIndex = 1;
            while (waypointIndex < _voxelRouteWaypointCount - 1 &&
                   (_voxelRouteWaypoints[waypointIndex] - ToVector3(selfPosition)).sqrMagnitude <= VoxelRouteWaypointReachDistanceSqr)
            {
                waypointIndex++;
            }

            float3 toWaypoint = (float3)_voxelRouteWaypoints[waypointIndex] - selfPosition;
            if (math.lengthsq(toWaypoint) <= 0.0001f)
                return;

            _cachedDesiredDirection = ResolveDominantAxisDirection(ToVector3(toWaypoint));
        }

        private bool TryApplyBurrowAmbushPathGuidance(float3 selfPosition, Vector3 playerPosition)
        {
            if (_faunaDataTemplate == null ||
                !_faunaDataTemplate.CanBurrowAmbush ||
                HectonVoxelEngine.ActiveRuntimeInstance == null ||
                !HectonVoxelEngine.ActiveRuntimeInstance.TryGetNearestActiveVolume(playerPosition, out HectonVoxelVolume volume) ||
                volume == null)
            {
                return false;
            }

            if (!volume.TryResolveBurrowAmbushRoute(
                    ToVector3(selfPosition),
                    playerPosition,
                    _faunaDataTemplate.BurrowSeabedTriggerDistanceMeters,
                    _faunaDataTemplate.BurrowBreachDistanceMeters,
                    out Vector3 solidAnchorWorldPosition,
                    out Vector3 breachWorldPosition))
            {
                return false;
            }

            _voxelRouteWaypoints[0] = ToVector3(selfPosition);
            _voxelRouteWaypoints[1] = solidAnchorWorldPosition;
            _voxelRouteWaypoints[2] = breachWorldPosition;
            _voxelRouteWaypointCount = 3;
            _voxelRouteTargetPosition = breachWorldPosition;
            _hasVoxelRouteTarget = true;
            CacheVoxelRouteAupState(_voxelRouteWaypointCount, breachWorldPosition);
            _nextVoxelRouteRefreshTime = _cognitionTimeSeconds + VoxelRouteRefreshIntervalSeconds;

            Vector3 guidePoint = (solidAnchorWorldPosition - ToVector3(selfPosition)).sqrMagnitude > VoxelRouteWaypointReachDistanceSqr
                ? solidAnchorWorldPosition
                : breachWorldPosition;
            _cachedDesiredDirection = ResolveDominantAxisDirection(guidePoint - ToVector3(selfPosition));

            if (_cognitionTimeSeconds >= _nextBurrowBreachTime)
                TryTriggerBurrowAmbushGrab(playerPosition, breachWorldPosition);

            return true;
        }

        private void TryTriggerBurrowAmbushGrab(Vector3 playerPosition, Vector3 breachWorldPosition)
        {
            if (_faunaDataTemplate == null ||
                _cognitionTimeSeconds < _nextBurrowBreachTime ||
                (breachWorldPosition - playerPosition).sqrMagnitude >
                (_faunaDataTemplate.BurrowBreachDistanceMeters * _faunaDataTemplate.BurrowBreachDistanceMeters))
            {
                return;
            }

            if (!TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.PlayerMovement == null)
            {
                return;
            }

            runtimeContext.PlayerMovement.ApplyFaunaHypnosisPull(
                breachWorldPosition,
                _faunaDataTemplate.BurrowPullAcceleration,
                _faunaDataTemplate.BurrowLockDurationSeconds);
            _nextBurrowBreachTime = _cognitionTimeSeconds + math.max(2f, _faunaDataTemplate.BurrowLockDurationSeconds);
            OnBurrowBreach?.Invoke();
        }

        private void UpdateBioluminescentHypnosis()
        {
            if (_isDead ||
                _faunaDataTemplate == null ||
                !_faunaDataTemplate.CanDazzleHypnotize ||
                !TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.PlayerMovement == null)
            {
                return;
            }

            PlayerLookState lookState = runtimeContext.LookState;
            if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
                return;

            if (!TryResolveSelfLogicPosition(out Vector3 faunaPosition))
                return;

            float3 toFauna3 = (float3)faunaPosition - lookState.EyePosition;
            float maxRange = _faunaDataTemplate.DazzleRangeMeters;
            if (math.lengthsq(toFauna3) > maxRange * maxRange)
                return;

            float3 lookDirection = (float3)ResolveDominantAxisDirection(ToVector3(lookState.AimForward));
            float3 faunaAxis = (float3)ResolveDominantAxisDirection(ToVector3(toFauna3));
            float gazeDot = math.dot(lookDirection, faunaAxis);
            if (gazeDot < _faunaDataTemplate.DazzleLookDotThreshold)
                return;

            runtimeContext.PlayerMovement.ApplyFaunaHypnosisPull(
                faunaPosition,
                _faunaDataTemplate.DazzlePullAcceleration,
                DefaultDazzleLockDurationSeconds);
        }

        private void TryDispatchEmpAttack(Transform target)
        {
            if (_faunaDataTemplate == null ||
                target == null ||
                (!SupportsAttackPattern(FaunaAttackPattern.SonicPulse) && !SupportsAttackPattern(FaunaAttackPattern.Emp)))
            {
                return;
            }

            float radiusMeters = _sensorSuite != null
                ? math.max(DefaultEmpAttackRadiusMeters, _sensorSuite.aggroDistance * 0.45f)
                : DefaultEmpAttackRadiusMeters;
            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return;

            Hecton8.Core.Contracts.Signals.CombatDamageSignal empSignal = default;
            if (!TryResolveAupFromRuntimeOrigin(selfPosition, out AbsoluteUniversePosition selfAup))
                return;

            empSignal.ImpactAup = selfAup.ToAbsoluteDouble3();
            empSignal.Direction = (float3)ResolveSelfLogicForward();
            empSignal.Magnitude = math.max(0f, radiusMeters) * math.max(0.1f, _faunaDataTemplate.EmpClaritySuppression01);
            empSignal.DamageType = (uint)DamageTypeMask.Emp;
            empSignal.SourceHash = ResolveStableFaunaHash(FaunaLeviathanBiteHashSalt, DamageSourceIds.FaunaEmp);
            empSignal.Frame = unchecked((uint)Time.frameCount);
            empSignal.SourceId = DamageSourceIds.FaunaEmp;
            empSignal.Channel = 0;
            empSignal.Flags = 0;
            empSignal.IntegrityDelta = 1;
            SignalBus<CombatDamageSignal>.TryPush(in empSignal);
        }

        private void UpdateEcholocationMimicry()
        {
            if (_isDead ||
                !ShouldUseEcholocationMimicry() ||
                !TryResolveMimicPlayerPosition(out Vector3 playerPosition) ||
                IsRetreatState(_currentStateCache) ||
                _currentStateCache == AIState.Sated)
            {
                ClearEcholocationMimicSignal();
                return;
            }

            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
            {
                ClearEcholocationMimicSignal();
                return;
            }

            float vanishDistance = _faunaDataTemplate.MimicPingVanishDistanceMeters;
            float vanishDistanceSqr = vanishDistance * vanishDistance;
            double playerDistanceSqr = ResolveRuntimeAupDistanceSq(playerPosition, selfPosition);

            if (_mimicSignalActive)
            {
                if (playerDistanceSqr <= vanishDistanceSqr)
                {
                    CommitEcholocationMimicAmbush(playerPosition);
                    _nextMimicPingTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingCooldownSeconds;
                    ClearEcholocationMimicSignal();
                    return;
                }

                if (_cognitionTimeSeconds >= _mimicPingExpireTime || _currentStateCache == AIState.Aggressive)
                {
                    ClearEcholocationMimicSignal();
                    return;
                }

                return;
            }

            if (_currentStateCache == AIState.Aggressive || _cognitionTimeSeconds < _nextMimicPingTime)
                return;

            float pingRadius = _faunaDataTemplate.MimicPingRadiusMeters;
            if (playerDistanceSqr <= vanishDistanceSqr || playerDistanceSqr > pingRadius * pingRadius)
                return;

            if (!TryResolveMimicPingTransmission(selfPosition, playerPosition, out float acousticTransmission01))
                return;

            if (acousticTransmission01 <= AcousticOcclusionUtility.DeepShadowTransmissionThreshold)
            {
                _nextMimicPingTime = _cognitionTimeSeconds + MimicPingOcclusionRetrySeconds;
                return;
            }

            EmitEcholocationMimicPing(selfPosition, acousticTransmission01);
        }

        private bool ShouldUseEcholocationMimicry()
        {
            if (_faunaDataTemplate == null || !_faunaDataTemplate.CanEmitMimicDistressPing)
                return false;

            return IsApexPredator() || _faunaDataTemplate.FoodChainTier == FaunaFoodChainTier.Leviathan;
        }

        private void RefreshMimicOcclusionRuntimeOwner()
        {
            bool shouldAcquire = ShouldUseEcholocationMimicry();
            if (!shouldAcquire)
            {
                ReleaseMimicOcclusionRuntimeOwner();
                return;
            }

            if (_mimicOcclusionRuntimeAcquired)
                return;

            AcousticOcclusionUtility.AcquireRuntime();
            _mimicOcclusionRuntimeAcquired = true;
        }

        private void ReleaseMimicOcclusionRuntimeOwner()
        {
            if (!_mimicOcclusionRuntimeAcquired)
                return;

            AcousticOcclusionUtility.ReleaseRuntime();
            _mimicOcclusionRuntimeAcquired = false;
        }

        private bool TryResolveMimicPlayerPosition(out Vector3 playerPosition)
        {
            if (_sensorSuite.TryGetPerceivedPlayerPosition(out playerPosition))
                return true;

            if (!TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup))
            {
                playerPosition = default;
                return false;
            }

            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            playerPosition = new Vector3(playerRuntime.x, playerRuntime.y, playerRuntime.z);
            return true;
        }

        private void EmitEcholocationMimicPing(Vector3 selfPosition, float acousticTransmission01)
        {
            if (_faunaDataTemplate == null)
                return;

            float maskedTransmission01 = math.saturate(acousticTransmission01);
            _mimicSignalActive = true;
            _mimicPingExpireTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingLifetimeSeconds;
            _nextMimicPingTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingCooldownSeconds;

            AcousticPingSignal signal = default;
            if (!TryResolveSelfLogicAup(out signal.PositionAup))
                return;
            signal.RadiusMeters = math.max(0f, _faunaDataTemplate.MimicPingRadiusMeters * maskedTransmission01);
            signal.Intensity01 = math.saturate(_faunaDataTemplate.MimicPingIntensity01 * maskedTransmission01);
            signal.SourceId = unchecked((uint)ComputeStableSpeciesId());
            signal.Channel = 0;
            signal.Flags = 0;
            SignalBus<AcousticPingSignal>.TryPush(in signal);
        }

        private bool TryResolveMimicPingTransmission(Vector3 selfPosition, Vector3 playerPosition, out float acousticTransmission01)
        {
            acousticTransmission01 = 1f;
            Transform playerRoot = _currentCullingPlayerTransform;

            int sensoryMask = AcousticOcclusionUtility.BuildSensoryMask();
            if (!AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                    selfPosition,
                    playerPosition,
                    sensoryMask,
                    transform,
                    playerRoot,
                    out AcousticOcclusionResult occlusion))
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(
                    selfPosition,
                    playerPosition,
                    sensoryMask,
                    transform,
                    playerRoot);
                return false;
            }

            acousticTransmission01 = occlusion.HitCount >= MimicPingDeepOcclusionWallCount
                ? 0f
                : math.saturate(occlusion.Transmission01);
            return true;
        }

        private void CommitEcholocationMimicAmbush(Vector3 playerPosition)
        {
            Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                ? resolvedSelfPosition
                : playerPosition - ResolveSelfLogicForward();
            Vector3 attackDirection = playerPosition - selfPosition;
            if (attackDirection.sqrMagnitude <= 0.0001f)
                attackDirection = ResolveSelfLogicForward();
            else
                attackDirection = ResolveDominantAxisDirection(attackDirection);

            _cachedDesiredDirection = attackDirection;
            _utilityBrain.RecordAuditoryStimulus(playerPosition, _cognitionTimeSeconds);
            _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.Aggressive;
            _currentStateCache = AIState.Aggressive;
        }

        private void ClearEcholocationMimicSignal()
        {
            if (_mimicSignalActive)
                WorldSpatialHashGrid.ClearTransientSignal(FieldTargetRole.DistressBeacon, ComputeStableSpeciesId());
            _mimicSignalActive = false;
            _mimicPingExpireTime = 0f;
        }

        private void EmitParentalDefenseSignal(Vector3 sourcePosition, float normalizedDamage)
        {
            if (_faunaDataTemplate == null ||
                !_faunaDataTemplate.EmitsParentalDefenseSignal ||
                normalizedDamage < ParentalDefenseIntensityThreshold)
            {
                return;
            }

            int speciesId = ComputeStableSpeciesId();
            if (speciesId == 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(sourcePosition, out AbsoluteUniversePosition sourceAup))
                return;

            ChemicalInfluenceGrid.QueueFearPheromone(sourcePosition, math.saturate(normalizedDamage));

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in sourceAup,
                _faunaDataTemplate.ParentalDefenseRadiusMeters,
                SpatialTargetKind.Bioform,
                _parentalDefenseBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                if (!(_parentalDefenseBuffer[i].Owner is IFaunaSpatialContact alliedContact) ||
                    ReferenceEquals(alliedContact, this) ||
                    alliedContact.IsDead ||
                    alliedContact.SpeciesId != speciesId ||
                    !alliedContact.RespondsToParentalDefenseSignal)
                {
                    continue;
                }

                alliedContact.ApplyParentalDefenseStimulus(sourcePosition);
            }
        }

        private bool ApplyEcologyChainOverrides(float3 selfPosition, float dt)
        {
            _baitFeedingTarget = null;
            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            if (ecosystemDirector == null)
                return false;

            if (TryApplyHibernationStarvationHuntOverride(ecosystemDirector, selfPosition))
                return true;

            if (TryApplyForcedMigrationOverride(ecosystemDirector, selfPosition))
                return true;

            if (TryApplyCorpseScavengingOverride(ecosystemDirector, selfPosition, dt))
                return true;

            if (TryApplyBaitFeedingOverride(ecosystemDirector, selfPosition))
                return true;

            if (TryApplyHerbivoreGrazingOverride(ecosystemDirector, selfPosition))
                return true;

            return TryApplyCleanerHostOverride(ecosystemDirector, selfPosition, dt);
        }

        private bool TryApplyHibernationStarvationHuntOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition)
        {
            if (!_hasHibernationStarvationHuntTarget)
                return false;

            if (_isDead || _utilityBrain.IsActivePredator == 0 || _cognitionTimeSeconds > _hibernationStarvationHuntUntilTime)
            {
                ClearHibernationStarvationHuntCommand();
                return false;
            }

            Vector3 selfWorldPosition = selfPosition;
            Vector3 targetPosition = _hibernationStarvationHuntTarget;
            float consumeDistance = math.max(0.1f, HibernationStarvationOrganicConsumeRadiusMeters);
            if ((targetPosition - selfWorldPosition).sqrMagnitude <= consumeDistance * consumeDistance)
            {
                if (ecosystemDirector.TryConsumeOrganicMassAtPosition(targetPosition, consumeDistance))
                {
                    _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                    TryReportFaunaFeedingObservation();
                    ApplyDirectedStateOverride(selfPosition, targetPosition, AIState.Sated);
                    ClearHibernationStarvationHuntCommand();
                    return true;
                }

                if (!ecosystemDirector.TryResolveNearestOrganicMass(selfWorldPosition, out targetPosition))
                {
                    ClearHibernationStarvationHuntCommand();
                    return false;
                }

                _hibernationStarvationHuntTarget = targetPosition;
                if (!TryResolveAupFromRuntimeOrigin(targetPosition, out _hibernationStarvationHuntTargetAup))
                {
                    ClearHibernationStarvationHuntCommand();
                    return false;
                }
            }

            _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
            ApplyDirectedStateOverride(selfPosition, targetPosition, AIState.Aggressive);
            return true;
        }

        private bool TryApplyForcedMigrationOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition)
        {
            if (!_hasForcedMigrationTarget || _cognitionTimeSeconds > _forcedMigrationUntilTime)
            {
                _hasForcedMigrationTarget = false;
                _forcedMigrationTarget = default;
                _forcedMigrationTargetAup = default;
                return false;
            }

            ApplyDirectedStateOverride(selfPosition, _forcedMigrationTarget, AIState.Retreat);
            return true;
        }

        private bool TryApplyCorpseScavengingOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition, float dt)
        {
            if (_speciesProfile == null ||
                !_speciesProfile.isScavenger ||
                _utilityBrain.HungerScore < ecosystemDirector.ScavengerHungerThreshold)
            {
                ClearCorpseLatchState();
                return false;
            }

            Vector3 selfWorldPosition = selfPosition;
            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
            {
                ClearCorpseLatchState();
                return false;
            }

            if (!ecosystemDirector.TryResolveCorpseScavengeTarget(in selfAup, out Vector3 corpsePosition, out uint corpseNodeId))
            {
                ClearCorpseLatchState();
                return false;
            }

            _baitFeedingTarget = null;
            float consumeDistance = ecosystemDirector.ScavengerConsumeDistanceMeters;
            if ((corpsePosition - selfWorldPosition).sqrMagnitude <= consumeDistance * consumeDistance &&
                ecosystemDirector.TryConsumeCorpseScavengeTarget(corpseNodeId, ecosystemDirector.ScavengerConsumeUnitsPerSecond * dt))
            {
                UpdateCorpseLatchState(corpsePosition, corpseNodeId, consumeDistance);
                UpdateCarrionTearingAnimation(dt);
                _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                TryReportFaunaFeedingObservation();
                ApplyDirectedStateOverride(selfPosition, corpsePosition, AIState.Sated);
                return true;
            }

            ClearCorpseLatchState();
            _utilityBrain.ApplyExternalState(AIState.Investigate, _cognitionTimeSeconds);
            ApplyDirectedStateOverride(selfPosition, corpsePosition, AIState.Investigate);
            return true;
        }

        private bool TryApplyBaitFeedingOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition)
        {
            _baitFeedingTarget = null;
            if (_speciesProfile == null ||
                !ecosystemDirector.DoesSpeciesRespondToBait(SpeciesId, _speciesProfile.isScavenger, isAggressive, _speciesProfile.isLeviathan) ||
                !_sensorSuite.hasCurrentScavengeTarget ||
                !(_sensorSuite.currentScavengeTargetOwner is IFaunaBaitSource baitSource) ||
                !baitSource.IsFaunaBait)
            {
                return false;
            }

            _baitFeedingTarget = _sensorSuite.currentScavengeTargetOwner.transform;
            Vector3 baitPosition = _sensorSuite.currentScavengeTargetPosition;
            float consumeDistance = ecosystemDirector.BaitFeedingDistanceMeters;
            if ((baitPosition - ToVector3(selfPosition)).sqrMagnitude <= consumeDistance * consumeDistance)
            {
                _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                ApplyDirectedStateOverride(selfPosition, baitPosition, AIState.Sated);
                return true;
            }

            ApplyDirectedStateOverride(selfPosition, baitPosition, AIState.Investigate);
            return true;
        }

        private bool TryApplyHerbivoreGrazingOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition)
        {
            int speciesId = ComputeStableSpeciesId();
            if (!ecosystemDirector.IsHerbivoreSpecies(speciesId) ||
                _utilityBrain.HungerScore < ecosystemDirector.HerbivoreGrazeHungerThreshold)
            {
                return false;
            }

            Vector3 selfWorldPosition = selfPosition;
            if (IsThermophilicRuntime())
            {
                if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                    return false;

                float thermalSearchRadius = math.max(ecosystemDirector.HerbivoreGrazeSearchRadiusMeters, 1000f);
                if (ecosystemDirector.TryResolveNearestThermalVentAttractor(in selfAup, thermalSearchRadius, out Vector3 thermalTarget, out float heat01))
                {
                    _utilityBrain.ApplyExternalState(AIState.Return, _cognitionTimeSeconds);
                    ApplyDirectedStateOverride(selfPosition, thermalTarget, AIState.Return);
                    return heat01 > 0.001f;
                }
            }

            if (ecosystemDirector.TryResolveHerbivoreGrazeTarget(selfWorldPosition, out Vector3 floraPosition, out uint floraInstanceUid))
            {
                float consumeDistanceMeters = ecosystemDirector.HerbivoreConsumeDistanceMeters;
                if ((floraPosition - selfWorldPosition).sqrMagnitude <= consumeDistanceMeters * consumeDistanceMeters &&
                    ecosystemDirector.TryConsumeHerbivoreGrazeTarget(floraInstanceUid))
                {
                    _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                    TryReportFaunaFeedingObservation();
                    ApplyDirectedStateOverride(selfPosition, selfWorldPosition + ResolveSelfLogicForward(), AIState.Sated);
                    return true;
                }

                ApplyDirectedStateOverride(selfPosition, floraPosition, AIState.Wander);
                return true;
            }

            if (ecosystemDirector.TryResolveMigrationTarget(speciesId, selfWorldPosition, out Vector3 migrationTarget))
            {
                ApplyDirectedStateOverride(selfPosition, migrationTarget, AIState.Return);
                return true;
            }

            return false;
        }

        private bool IsThermophilicRuntime()
        {
            if (_archetype != null && _archetype.thermophilic)
                return true;

            string creatureId = _archetype != null ? _archetype.creatureId : string.Empty;
            if (!string.IsNullOrEmpty(creatureId) &&
                creatureId.IndexOf("therm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return _faunaDataTemplate != null &&
                   !string.IsNullOrEmpty(_faunaDataTemplate.name) &&
                   _faunaDataTemplate.name.IndexOf("therm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsBiolumFlashBangPreyRuntime()
        {
            string creatureId = _archetype != null ? _archetype.creatureId : string.Empty;
            if (ContainsFlashSquidToken(creatureId))
                return true;

            string displayName = _archetype != null ? _archetype.displayName : string.Empty;
            if (ContainsFlashSquidToken(displayName))
                return true;

            return _faunaDataTemplate != null && ContainsFlashSquidToken(_faunaDataTemplate.name);
        }

        private void TriggerBiolumFlashBang(Vector3 flashPosition)
        {
            if (IsLeviathan())
                _sensorSuite.ApplyFlashBlind(_cognitionTimeSeconds, BiolumFlashBangBlindDurationSeconds);

            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            if (ecosystemDirector != null)
            {
                if (!TryResolveAupFromRuntimeOrigin(flashPosition, out AbsoluteUniversePosition flashAup))
                    return;

                ecosystemDirector.PublishBiolumFlashBang(in flashAup, _cognitionTimeSeconds, BiolumFlashBangShaderRadiusMeters);
            }
        }

        private static bool ContainsFlashSquidToken(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   value.IndexOf("squid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal bool TryPersistEggClutch()
        {
            if (_archetype == null || !_archetype.laysEggClutches)
                return false;

            IFaunaPersistentWorldStateService registry = _persistentWorldRegistry;
            if (registry == null)
                return false;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition eggAup))
                return false;

            uint sequence = ++_eggClutchSequence;
            uint eggUid = _uniqueInstanceUid != 0u
                ? unchecked(_uniqueInstanceUid ^ 0x0E66C7u ^ (sequence * 2246822519u))
                : unchecked((uint)(PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in eggAup) & uint.MaxValue) ^ (sequence * 2246822519u));
            EntityDataRecord eggState = PersistentWorldRegistry.CreateFaunaEggState(
                eggUid,
                ComputeStableSpeciesId(),
                in eggAup,
                _cognitionTimeSeconds,
                _archetype.eggIncubationSeconds);
            return registry.TryCacheFaunaEggState(in eggState);
        }

        private void TryAdvanceEggClutchPersistence()
        {
            if (_isDead ||
                _logicalLodTier != FaunaLogicalLodTier.FullSim ||
                _archetype == null ||
                !_archetype.laysEggClutches)
            {
                return;
            }

            if (_nextEggClutchTimeSeconds <= 0f)
                ResetEggClutchCadence();

            if (_cognitionTimeSeconds < _nextEggClutchTimeSeconds)
                return;

            TryPersistEggClutch();
            ScheduleNextEggClutch();
        }

        private void ResetEggClutchCadence()
        {
            _eggClutchSequence = 0u;
            ScheduleNextEggClutch();
        }

        private void ScheduleNextEggClutch()
        {
            float cooldown = ResolveEggClutchCooldownSeconds();
            float jitter = ResolveDeterministicEggCooldownJitter();
            _nextEggClutchTimeSeconds = _cognitionTimeSeconds + cooldown * jitter;
        }

        private float ResolveEggClutchCooldownSeconds()
        {
            if (_archetype == null)
                return MinimumEggClutchCooldownSeconds;

            return math.max(
                MinimumEggClutchCooldownSeconds,
                math.max(1f, _archetype.eggIncubationSeconds) * 0.5f);
        }

        private bool TryApplyCleanerHostOverride(IEcosystemDirectorService ecosystemDirector, float3 selfPosition, float dt)
        {
            if (!ecosystemDirector.IsCleanerSpecies(ComputeStableSpeciesId()))
                return false;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return false;

            int hostCount = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in selfAup,
                ecosystemDirector.CleanerHostSearchRadiusMeters,
                SpatialTargetKind.Bioform,
                _cleanerHostBuffer);
            IFaunaSpatialContact bestHost = null;
            float bestDistanceSq = float.MaxValue;
            Vector3 bestHostPosition = default;
            for (int i = 0; i < hostCount; i++)
            {
                SpatialQueryHit hit = _cleanerHostBuffer[i];
                IFaunaSpatialContact hostContact = hit.Owner as IFaunaSpatialContact;
                if (hostContact == null ||
                    ReferenceEquals(hostContact, this) ||
                    hostContact.IsDead ||
                    !ecosystemDirector.IsCleanerHostSpecies(
                        hostContact.SpeciesId,
                        hostContact.IsLeviathanContact))
                {
                    continue;
                }

                if (hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                bestHost = hostContact;
                bestHostPosition = hit.Position;
            }

            if (bestHost == null)
                return false;

            Vector3 cleanerTarget = bestHostPosition + ResolveCleanerCompanionOffset(bestHost);
            ApplyDirectedStateOverride(selfPosition, cleanerTarget, AIState.Flocking);
            float symbiosisDistanceMeters = ecosystemDirector.CleanerSymbiosisDistanceMeters;
            if ((cleanerTarget - ToVector3(selfPosition)).sqrMagnitude <= symbiosisDistanceMeters * symbiosisDistanceMeters)
                bestHost.ApplyCleanerSymbiosis(ecosystemDirector.CleanerFatigueReliefPerSecond * dt);

            return true;
        }

        private void ApplyDirectedStateOverride(float3 selfPosition, Vector3 targetPosition, AIState state)
        {
            float3 desiredDirection = (float3)targetPosition - selfPosition;
            if (math.lengthsq(desiredDirection) > 0.0001f)
                _cachedDesiredDirection = ResolveDominantAxisDirection(ToVector3(desiredDirection));

            _currentStateCache = state;
            _stateMachine.currentState = state;
        }

        private Vector3 ResolveCleanerCompanionOffset(IFaunaSpatialContact hostContact)
        {
            uint seed = _uniqueInstanceUid != 0u
                ? _uniqueInstanceUid
                : (uint)(ComputeStableSpeciesId() * 73856093);
            seed ^= (uint)(hostContact != null ? hostContact.SpeciesId * 19349663 : 0);
            float radius01 = ((seed >> 8) & 0xFFu) * ByteToUnitScale;
            float vertical01 = ((seed >> 16) & 0xFFu) * ByteToUnitScale;
            int formationSlot = (int)(seed & 0x7u);
            float radius = math.lerp(CleanerFormationMinRadius, CleanerFormationMaxRadius, radius01);
            float verticalOffset = math.lerp(CleanerVerticalBiasMin, CleanerVerticalBiasMax, vertical01);

            Vector3 hostForward = hostContact != null ? hostContact.ResolveContactForward() : Vector3.forward;
            if (hostForward.sqrMagnitude <= 0.0001f)
                hostForward = Vector3.forward;
            else
                hostForward = ResolveDominantAxisDirection(hostForward);

            Vector3 hostRight = ResolvePlanarRightFromDominantForward(hostForward);

            float lateralSign = (formationSlot & 0x1) == 0 ? -1f : 1f;
            float forwardSign = (formationSlot & 0x2) == 0 ? -1f : 1f;
            float lateralWeight = (formationSlot & 0x4) == 0 ? 1f : 0.5f;
            Vector3 lateralOffset = (hostRight * lateralSign * radius * lateralWeight) +
                                    (hostForward * forwardSign * radius * CleanerForwardBias * (1f - lateralWeight * 0.25f));
            return lateralOffset + (Vector3.up * verticalOffset);
        }

        private bool IsApexPredator()
        {
            return (_speciesProfile != null && _speciesProfile.isLeviathan) ||
                   (_archetype != null && _archetype.roleType == CreatureRoleType.Leviathan);
        }

        private bool ShouldUseAlphaLeviathanCognition()
        {
            if (!IsApexPredator())
                return false;

            if (_archetype == null)
                return _speciesProfile != null && _speciesProfile.isLeviathan;

            if (_archetype.roleType != CreatureRoleType.Leviathan)
                return _speciesProfile != null && _speciesProfile.isLeviathan && _archetype.useLeviathanPresence;

            return _archetype.useFeintRush ||
                   (_archetype.useLeviathanPresence &&
                    _archetype.leviathanEncounterType == LeviathanEncounterType.PresenceCircle);
        }

        private bool IsLeviathan()
        {
            return IsApexPredator();
        }

        private float ResolveApexTerritoryRadius()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? math.max(25f, profile.territoryRadiusMeters)
                : DefaultApexTerritoryRadiusMeters;
        }

        private float ResolveApexTerritoryMassScore()
        {
            return math.max(1f, ResolveApexTerritoryRadius() * math.max(1f, _maxHealth) * math.max(0.05f, HealthNormalized));
        }

        private float ResolveApexAggressionMultiplier()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? math.max(1f, profile.aggressionMultiplierAgainstRivals)
                : 1.35f;
        }

        private float ResolveApexIntimidationRadius()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? math.max(1f, profile.intimidationRadiusMeters)
                : DefaultApexIntimidationRadiusMeters;
        }

        private float ResolveApexIntimidationDuration()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? math.max(1f, profile.intimidationDurationSeconds)
                : DefaultApexIntimidationDurationSeconds;
        }

        private float ResolveApexForcedRetreatDuration()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? math.max(1f, profile.forcedRetreatDurationSeconds)
                : DefaultApexForcedRetreatDurationSeconds;
        }

        private bool TryResolveNearestRivalApex(float3 selfPosition, float searchRadius, out IFaunaSpatialContact rivalContact, out Vector3 rivalPosition)
        {
            rivalContact = null;
            rivalPosition = default;
            if (!IsApexPredator())
                return false;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return false;

            int contactCount = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in selfAup,
                searchRadius,
                SpatialTargetKind.Bioform,
                _apexContactBuffer);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = _apexContactBuffer[i];
                IFaunaSpatialContact candidate = hit.Owner as IFaunaSpatialContact;
                if (candidate == null ||
                    ReferenceEquals(candidate, this) ||
                    candidate.IsDead ||
                    !candidate.IsApexPredatorContact)
                {
                    continue;
                }

                if (hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                rivalContact = candidate;
                rivalPosition = hit.Position;
            }

            return rivalContact != null;
        }

        private bool TryResolveApexIntimidationThreat(float3 selfPosition, out Vector3 threatPosition)
        {
            threatPosition = default;
            if (IsApexPredator())
                return false;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return false;

            int contactCount = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(
                in selfAup,
                DefaultApexIntimidationRadiusMeters,
                SpatialTargetKind.Bioform,
                _apexContactBuffer);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = _apexContactBuffer[i];
                IFaunaSpatialContact candidate = hit.Owner as IFaunaSpatialContact;
                if (candidate == null ||
                    ReferenceEquals(candidate, this) ||
                    candidate.IsDead ||
                    !candidate.HasActiveApexIntimidation)
                {
                    continue;
                }

                float intimidationRadius = candidate.ResolveApexIntimidationRadiusMeters();
                if (hit.DistanceSqr > intimidationRadius * intimidationRadius || hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                threatPosition = hit.Position;
            }

            return bestDistanceSq < float.MaxValue;
        }

        private Vector3 ResolvePredictedPlayerGuidanceTarget(float3 selfPosition, Vector3 playerPosition)
        {
            if (_hasDirectorHuntPrediction)
            {
                float3 directorTarget = _directorHuntPredictedAup.ToRuntimeFloat3();
                return new Vector3(directorTarget.x, directorTarget.y, directorTarget.z);
            }

            if (!_sensorSuite.TryGetPerceivedPlayerVelocity(out Vector3 playerVelocity))
                return playerPosition;

            if (!TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) &&
                !TryResolveAupFromRuntimeOrigin(playerPosition, out playerAup))
            {
                return playerPosition;
            }

            AbsoluteUniversePosition predictedAup = PredictTargetAup(in playerAup, playerVelocity, PredatorGuidanceLeadSeconds);
            float3 runtimeTarget = predictedAup.ToRuntimeFloat3();
            return new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        private static AbsoluteUniversePosition PredictTargetAup(in AbsoluteUniversePosition targetAup, Vector3 velocityVector, float deltaTime)
        {
            double leadSeconds = math.max(0d, (double)deltaTime);
            return AbsoluteUniversePosition.OffsetMeters(
                in targetAup,
                new double3(velocityVector.x, velocityVector.y, velocityVector.z) * leadSeconds);
        }

        private void ClearVoxelPathGuidance()
        {
            _voxelRouteWaypointCount = 0;
            _hasVoxelRouteTarget = false;
            _nextVoxelRouteRefreshTime = 0f;
            _voxelRouteTargetPosition = default;
            _voxelRouteTargetAup = default;
        }

        private bool TryResolveDirectorHuntTarget(out Vector3 targetPosition)
        {
            if (!_hasDirectorHuntTarget || _cognitionTimeSeconds > _directorHuntUntilTime)
            {
                ClearDirectorHuntTarget();
                targetPosition = default;
                return false;
            }

            AbsoluteUniversePosition targetAup = _directorHuntTargetAup;
            if (_hasDirectorHuntPrediction)
            {
                float predictionDelta = _directorHuntPredictionLeadSeconds +
                                        math.clamp(_cognitionTimeSeconds - _directorHuntPredictionSampleTime, 0f, 0.35f);
                _directorHuntPredictedAup = PredictTargetAup(in _directorHuntTargetAup, _directorHuntTargetVelocity, predictionDelta);
                targetAup = _directorHuntPredictedAup;
            }

            float3 runtimeTarget = targetAup.ToRuntimeFloat3();
            targetPosition = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
            _directorHuntTargetPosition = targetPosition;
            return true;
        }

        private void ClearDirectorHuntTarget()
        {
            _hasDirectorHuntTarget = false;
            _directorHuntTargetPosition = default;
            _directorHuntTargetAup = default;
            _directorHuntPredictedAup = default;
            _directorHuntTargetVelocity = default;
            _directorHuntPredictionLeadSeconds = 0f;
            _directorHuntPredictionSampleTime = 0f;
            _directorHuntUntilTime = 0f;
            _hasDirectorHuntPrediction = false;
        }

        private bool ShouldPauseVoxelRouteConsumptionForOriginShift()
        {
            return _voxelRouteOriginShiftRefreshActive ||
                   HectonFloatingOrigin.IsShiftInProgress ||
                   Time.frameCount == _voxelRouteLastOriginShiftFrame;
        }

        /// <summary>
        /// Rehydrates runtime route vectors from Absolute Universe Position storage after a floating-origin shift.
        /// </summary>
        /// <param name="shiftData">Committed shift payload.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _voxelRouteOriginShiftRefreshActive = true;
            try
            {
                RefreshVoxelRouteRuntimeCacheFromAup(in shiftData);
                RefreshHibernationStarvationHuntTargetFromAup(in shiftData);
                RefreshDirectorHuntTargetFromAup(in shiftData);
            }
            finally
            {
                _voxelRouteOriginShiftRefreshActive = false;
                _voxelRouteLastOriginShiftFrame = Time.frameCount;
            }

            RefreshForcedMigrationTargetFromAup(in shiftData);
        }

        private void RegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void CacheVoxelRouteAupState(int waypointCount, Vector3 targetPosition)
        {
            int clampedCount = math.clamp(waypointCount, 0, MaxVoxelRouteWaypointCount);
            for (int waypointIndex = 0; waypointIndex < clampedCount; waypointIndex++)
            {
                if (!TryResolveAupFromRuntimeOrigin(_voxelRouteWaypoints[waypointIndex], out _voxelRouteWaypointAups[waypointIndex]))
                {
                    ClearVoxelPathGuidance();
                    return;
                }
            }

            if (!TryResolveAupFromRuntimeOrigin(targetPosition, out _voxelRouteTargetAup))
                ClearVoxelPathGuidance();
        }

        private void RefreshVoxelRouteRuntimeCacheFromAup(in OriginShiftEventData shiftData)
        {
            if (!_hasVoxelRouteTarget || _voxelRouteWaypointCount <= 0)
                return;

            int clampedCount = math.clamp(_voxelRouteWaypointCount, 0, MaxVoxelRouteWaypointCount);
            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;
            for (int waypointIndex = 0; waypointIndex < clampedCount; waypointIndex++)
            {
                AbsoluteUniversePosition waypoint = _voxelRouteWaypointAups[waypointIndex];
                float3 runtimeWaypoint = AUPMath.ToRuntimeFloat3(in waypoint, committedOriginOffset);
                _voxelRouteWaypoints[waypointIndex] = new Vector3(runtimeWaypoint.x, runtimeWaypoint.y, runtimeWaypoint.z);
            }

            AbsoluteUniversePosition target = _voxelRouteTargetAup;
            float3 runtimeTarget = AUPMath.ToRuntimeFloat3(in target, committedOriginOffset);
            _voxelRouteTargetPosition = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        private void RefreshForcedMigrationTargetFromAup(in OriginShiftEventData shiftData)
        {
            if (!_hasForcedMigrationTarget)
                return;

            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;
            float3 runtimeTarget = AUPMath.ToRuntimeFloat3(in _forcedMigrationTargetAup, committedOriginOffset);
            _forcedMigrationTarget = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        private void RefreshHibernationStarvationHuntTargetFromAup(in OriginShiftEventData shiftData)
        {
            if (!_hasHibernationStarvationHuntTarget)
                return;

            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;
            float3 runtimeTarget = AUPMath.ToRuntimeFloat3(in _hibernationStarvationHuntTargetAup, committedOriginOffset);
            _hibernationStarvationHuntTarget = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        private void RefreshDirectorHuntTargetFromAup(in OriginShiftEventData shiftData)
        {
            if (!_hasDirectorHuntTarget)
                return;

            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;
            float3 runtimeTarget = AUPMath.ToRuntimeFloat3(in _directorHuntTargetAup, committedOriginOffset);
            _directorHuntTargetPosition = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        public void FixedTick(float fdt)
        {
            _lastFixedTickDeltaSeconds = math.max(math.select(0.02f, fdt, math.isfinite(fdt)), 0.0001f);

            if (_isDead)
            {
                ApplyDeathSpiralFixedStep(fdt);
                return;
            }

            if (_spatialHandle != 0)
                WorldSpatialHashGrid.Refresh(_spatialHandle);
            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);

            if (_lodDisabled) return;
            if (_corpseLatchActive)
            {
                ApplyCorpseLatchFixedStep();
                return;
            }

            if (IsPredatorStunnedActive())
            {
                if (_rb != null)
                {
                    TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
                    TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
                }

                return;
            }

            if (TryApplyPredatorLungeCheatFixedStep())
                return;

            Vector3 playerTargetPosition = default;
            if (_sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 perceivedPlayerPosition))
                playerTargetPosition = perceivedPlayerPosition;

            float runtimeSpeedScale = ResolveRuntimeSpeedMultiplierForState(_stateMachine.currentState);
            Vector3 desiredDirection = _cachedDesiredDirection;
            ApplyAmbientWanderNoise(ref desiredDirection);
            float forceMultiplier = _stateMachine.currentForceMultiplier;
            float speedMultiplier = _stateMachine.currentSpeedMultiplier * runtimeSpeedScale;
            speedMultiplier *= ResolveTailSurgeSpeedMultiplier();
            speedMultiplier *= ResolveCombatMobilitySpeedMultiplier();
            float turnMultiplier = _stateMachine.currentTurnMultiplier;
            if (TryResolveDynamicDodgeDirection(desiredDirection, out Vector3 dodgeDirection))
            {
                desiredDirection = dodgeDirection;
                forceMultiplier = math.max(forceMultiplier, DynamicDodgeForceMultiplier);
                speedMultiplier = math.max(speedMultiplier, DynamicDodgeSpeedMultiplier);
                turnMultiplier = math.max(turnMultiplier, DynamicDodgeTurnMultiplier);
            }

            if (TryResolveWallSlideDirection(desiredDirection, out Vector3 slideDirection))
            {
                desiredDirection = slideDirection;
                speedMultiplier = math.max(speedMultiplier, WallSlideSpeedMultiplier);
                turnMultiplier = math.max(turnMultiplier, WallSlideTurnMultiplier);
            }

            if (ShouldApplySpatialDensityPenalty() &&
                _faunaSpatialHandle != 0 &&
                FaunaSpatialHashRegistry.TryResolveDensityPenalty(_faunaSpatialHandle, out Vector3 densityPenaltyDirection, out _))
            {
                desiredDirection = ResolveDensityPenaltyDirection(desiredDirection, densityPenaltyDirection);
                forceMultiplier = math.max(forceMultiplier, SpatialDensityPenaltyForceMultiplier);
                speedMultiplier = math.max(speedMultiplier, SpatialDensityPenaltySpeedMultiplier);
                turnMultiplier = math.max(turnMultiplier, SpatialDensityPenaltyTurnMultiplier);
            }

            ApplyLeviathanBreachAttack(ref desiredDirection, ref forceMultiplier, ref speedMultiplier);
            ApplyLeviathanAttackTelegraphMotion(ref desiredDirection, ref forceMultiplier, ref speedMultiplier);

            if (TryApplyLeviathanVaultSteeringPresentation(desiredDirection, speedMultiplier))
            {
                RestoreLeviathanBreachDragIfReady();
                return;
            }
            
            _steeringEngine.FixedTick(
                fdt, 
                desiredDirection, 
                forceMultiplier, 
                speedMultiplier,
                turnMultiplier,
                IsRetreatState(_stateMachine.currentState),
                playerTargetPosition
            );
            UpdateLeviathanKinematicsMotionIntent(desiredDirection, speedMultiplier);

            ApplyAmbientCurrentDrift(fdt);
            RestoreLeviathanBreachDragIfReady();
        }

        private bool TryApplyLeviathanVaultSteeringPresentation(Vector3 desiredDirection, float speedMultiplier)
        {
            int utilitySlot = CreatureUtilityBrain.ResolveSlot(in _utilityBrain);
            if (utilitySlot < 0)
                utilitySlot = math.max(0, _simulationBucketId);

            if (!ShouldUseProceduralLeviathanPresentation() ||
                !PredatorCognitionDomain.TryCopyLeviathanKinematicState(utilitySlot, out KinematicStateDTO state))
            {
                return false;
            }

            Vector3 velocity = new Vector3(state.Velocity.x, state.Velocity.y, state.Velocity.z);
            if (velocity.sqrMagnitude <= 0.0001f)
                return false;

            _steeringEngine.velocity = velocity;
            _steeringEngine.currentSpeed = math.sqrt(math.max(0f, velocity.sqrMagnitude));
            _steeringEngine.currentDirection = ResolveDominantAxisDirection(velocity);
            UpdateLeviathanKinematicsMotionIntent(
                velocity.sqrMagnitude > 0.0001f ? velocity : desiredDirection,
                speedMultiplier);
            return true;
        }

        private void UpdateLeviathanKinematicsMotionIntent(Vector3 desiredDirection, float speedMultiplier)
        {
            if (_faunaKinematicsRuntime == null || !ShouldUseProceduralLeviathanPresentation())
                return;

            Vector3 direction = desiredDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(desiredDirection)
                : ResolveSelfLogicForward();
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            Vector3 velocity = _steeringEngine.velocity;
            if (velocity.sqrMagnitude <= 0.0001f && _rb != null)
                velocity = _rb.linearVelocity;
            if (velocity.sqrMagnitude <= 0.0001f)
                velocity = direction * math.max(0.1f, _steeringEngine.maxSpeed * math.max(0.1f, speedMultiplier));

            Vector3 origin = _rb != null ? _rb.position : transform.position;
            Vector3 headTarget = origin + direction * math.max(1f, _steeringEngine.maxSpeed * 0.35f);
            _faunaKinematicsRuntime.SetMotionIntent(velocity, headTarget);
        }

        private float ResolveTailSurgeSpeedMultiplier()
        {
            float seedPhase = (_uniqueInstanceUid == 0u ? 1u : _uniqueInstanceUid) * TailSurgeSeedScale;
            float surge01 = TrianglePulse01(_cognitionTimeSeconds, TailSurgeFrequency, seedPhase);
            float surge = surge01 * 2f - 1f;
            return math.max(0.05f, 1f + (surge * TailSurgeAmplitude));
        }

        public void SetCombatMobilityScale(float speedScale, float durationSeconds)
        {
            if (!math.isfinite(speedScale) || !math.isfinite(durationSeconds) || durationSeconds <= 0f)
                return;

            _combatMobilityScale = math.min(
                math.clamp(speedScale, 0.05f, 1f),
                math.clamp(_combatMobilityScale, 0.05f, 1f));
            _combatMobilityUntilTime = math.max(_combatMobilityUntilTime, _cognitionTimeSeconds + durationSeconds);
        }

        private float ResolveCombatMobilitySpeedMultiplier()
        {
            if (_combatMobilityUntilTime <= _cognitionTimeSeconds)
            {
                _combatMobilityScale = 1f;
                return 1f;
            }

            return math.clamp(_combatMobilityScale, 0.05f, 1f);
        }

        public void LateFrameTick()
        {
            UpdateSimulationBucketInterpolationAlpha();
            CompleteCorpseSinkingKinematicsIfReady();
            FlushQueuedAupPresentationPose();
            FlushBiolumPresentationLightScale();
            FlushCorpseBloatShaderTimer();
            FlushFaunaPresentationShaderState();
            FlushEcosystemInfectionVisuals();
            FlushQueuedDespawnOrDeactivate();
        }

        private void ApplyAmbientWanderNoise(ref Vector3 desiredDirection)
        {
            if (!ShouldApplyAmbientWanderNoise())
                return;

            Vector3 baseDirection = desiredDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(desiredDirection)
                : ResolveSelfLogicForward();
            if (!TryResolveSelfLogicPosition(out Vector3 position))
                return;

            float seed = (_uniqueInstanceUid == 0u ? 1u : _uniqueInstanceUid) * 0.0009765625f;
            float phase = _cognitionTimeSeconds * AmbientWanderNoiseFrequency + seed;
            Vector3 drift = new Vector3(
                CheapTriangleWaveSigned(phase + position.z * AmbientWanderNoiseSpatialScale),
                CheapTriangleWaveSigned(phase * 0.73f + position.x * AmbientWanderNoiseSpatialScale) * 0.35f,
                CheapTriangleWaveSigned(phase * 0.91f + position.y * AmbientWanderNoiseSpatialScale + 0.25f));
            if (drift.sqrMagnitude <= 0.0001f)
                return;

            Vector3 blended = baseDirection + ResolveDominantAxisDirection(drift) * AmbientWanderNoiseWeight;
            if (blended.sqrMagnitude > 0.0001f)
                desiredDirection = ResolveDominantAxisDirection(blended);
        }

        private bool ShouldApplyAmbientWanderNoise()
        {
            if (_utilityBrain.IsActivePredator != 0 || IsLeviathan() || isAggressive)
                return false;

            AIState state = _stateMachine.currentState;
            return state == AIState.Wander ||
                   state == AIState.Flocking ||
                   state == AIState.Sated ||
                   state == AIState.Return;
        }

        private void ApplyLeviathanBreachAttack(ref Vector3 desiredDirection, ref float forceMultiplier, ref float speedMultiplier)
        {
            if (!IsLeviathan() || _rb == null)
                return;

            if (!TryResolveLeviathanBreachTarget(out Vector3 targetPosition))
                return;

            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return;

            float verticalDelta = targetPosition.y - selfPosition.y;
            if (verticalDelta < LeviathanBreachHeightDeltaMeters)
                return;

            Vector3 breachDirection = ResolveDominantAxisDirection(targetPosition - selfPosition);
            if (breachDirection.sqrMagnitude <= 0.0001f)
                breachDirection = Vector3.up;

            desiredDirection = ResolveDominantAxisDirection(breachDirection + Vector3.up * 1.35f);
            forceMultiplier = math.max(forceMultiplier, LeviathanBreachVelocityMultiplier);
            speedMultiplier = math.max(speedMultiplier, LeviathanBreachVelocityMultiplier);
            if (selfPosition.y > 0f)
                DisableLeviathanWaterDragTemporarily();
        }

        private bool TryResolveLeviathanBreachTarget(out Vector3 targetPosition)
        {
            if (_sensorSuite.hasCurrentPrey)
            {
                targetPosition = _sensorSuite.currentPreyPosition;
                return true;
            }

            if (_sensorSuite.TryGetPerceivedPlayerPosition(out targetPosition))
                return true;

            targetPosition = default;
            return false;
        }

        private void DisableLeviathanWaterDragTemporarily()
        {
            if (_rb == null)
                return;

            if (!_baseLinearDampingCaptured)
            {
                _baseLinearDamping = _rb.linearDamping;
                _baseLinearDampingCaptured = true;
            }

            _breachDragBypassUntilTime = math.max(_breachDragBypassUntilTime, _cognitionTimeSeconds + LeviathanBreachAirDragBypassSeconds);
            _rb.linearDamping = 0f;
        }

        private void RestoreLeviathanBreachDragIfReady()
        {
            if (!_baseLinearDampingCaptured || _rb == null || _breachDragBypassUntilTime <= 0f || _cognitionTimeSeconds < _breachDragBypassUntilTime)
                return;

            _rb.linearDamping = _baseLinearDamping;
            _breachDragBypassUntilTime = 0f;
        }

        private void CaptureBaseRigidbodyPresentationState()
        {
            if (_rb == null || _baseGravityCaptured)
                return;

            _baseUseGravity = _rb.useGravity;
            _baseIsKinematic = _rb.isKinematic;
            _baseDetectCollisions = _rb.detectCollisions;
            _baseLinearDamping = _rb.linearDamping;
            _baseAngularDamping = _rb.angularDamping;
            _baseLinearDampingCaptured = true;
            _baseGravityCaptured = true;
        }

        private void RestoreBaseRigidbodyPresentationState()
        {
            if (_rb == null || !_baseGravityCaptured)
                return;

            _rb.useGravity = _baseUseGravity;
            _rb.isKinematic = _baseIsKinematic;
            _rb.detectCollisions = _baseDetectCollisions;
            _rb.linearDamping = _baseLinearDamping;
            _rb.angularDamping = _baseAngularDamping;
        }

        private void ApplyPassiveRigidbodyCastrationIfRequired()
        {
            if (_rb == null || _archetype == null)
                return;

            bool passiveKinematic =
                _archetype.roleType == CreatureRoleType.Ambient ||
                _archetype.locomotionType == CreatureLocomotionType.GpuBoidSchool;
            if (!passiveKinematic)
                return;

            if (!_rb.isKinematic)
            {
                TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
                TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
                _rb.Sleep();
            }

            _rb.useGravity = false;
            _rb.detectCollisions = false;
            _rb.isKinematic = true;
        }

        private bool TryAdvanceAttackTelegraph(Transform attackTarget)
        {
            if (IsPredatorStunnedActive())
                return false;

            if (!ShouldUseProceduralLeviathanPresentation())
                return true;

            if (attackTarget == null || !attackTarget.gameObject.activeInHierarchy)
            {
                ClearAttackTelegraphState();
                return false;
            }

            if (!TryResolveAttackTargetLogicPosition(attackTarget, out Vector3 attackTargetPosition))
            {
                ClearAttackTelegraphState();
                return false;
            }

            if (!_attackTelegraphActive || _telegraphedAttackTarget != attackTarget)
            {
                _telegraphedAttackTarget = attackTarget;
                _attackTelegraphBurstTime = _cognitionTimeSeconds + LeviathanAttackTelegraphLeadSeconds;
                _attackTelegraphActive = true;
                _attackTelegraphAudioEmitted = false;
            }

            if (!_attackTelegraphAudioEmitted)
            {
                EmitLeviathanAttackTelegraphPing(attackTargetPosition);
                _attackTelegraphAudioEmitted = true;
            }

            if (_cognitionTimeSeconds < _attackTelegraphBurstTime)
                return false;

            _attackBurstUntilTime = math.max(_attackBurstUntilTime, _cognitionTimeSeconds + LeviathanAttackBurstDurationSeconds);
            BeginPredatorLungeCheat(attackTarget, attackTargetPosition);
            return true;
        }

        private void ClearAttackTelegraphState()
        {
            _telegraphedAttackTarget = null;
            _attackTelegraphActive = false;
            _attackTelegraphAudioEmitted = false;
            _attackTelegraphBurstTime = 0f;
            if (_faunaKinematicsRuntime != null)
                _faunaKinematicsRuntime.SetAttackTelegraph(0f);
        }

        private void BeginPredatorLungeCheat(Transform target, Vector3 targetPosition)
        {
            if (_rb == null)
                return;

            Vector3 startPosition = _rb.position;
            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition startAup) ||
                !TryResolveAttackTargetLogicAup(target, targetPosition, out AbsoluteUniversePosition targetAup))
            {
                return;
            }

            double3 startAbsolute = startAup.ToAbsoluteDouble3();
            double3 targetAbsolute = targetAup.ToAbsoluteDouble3();
            double3 toTargetAbsolute = targetAbsolute - startAbsolute;
            float3 targetDelta = new float3(
                (float)toTargetAbsolute.x,
                (float)toTargetAbsolute.y,
                (float)toTargetAbsolute.z);
            Vector3 direction = math.lengthsq(targetDelta) > 0.0001f
                ? ResolveDominantAxisDirection(new Vector3(targetDelta.x, targetDelta.y, targetDelta.z))
                : ResolveSelfLogicForward();
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            float strikeRange = _speciesProfile != null
                ? _speciesProfile.attackRadius
                : math.max(1f, _stateMachine.attackRadius);
            float strikeRangeSq = strikeRange * strikeRange;
            float targetDistanceSq = math.lengthsq(targetDelta);
            float lungeDistance = targetDistanceSq <= strikeRangeSq
                ? strikeRange * PredatorLungeCloseDistanceMultiplier
                : strikeRange * PredatorLungeCheatDistanceMultiplier;
            _lungeCheatStartPosition = startPosition;
            _lungeCheatStartAup = startAup;
            double3 lungeTargetAbsolute = startAbsolute + new double3(direction.x, direction.y, direction.z) * lungeDistance;
            _lungeCheatTargetAup = AbsoluteUniversePosition.FromAbsolutePosition(lungeTargetAbsolute);
            float3 lungeTargetRuntime = _lungeCheatTargetAup.ToRuntimeFloat3();
            _lungeCheatTargetPosition = new Vector3(lungeTargetRuntime.x, lungeTargetRuntime.y, lungeTargetRuntime.z);
            _lungeCheatDirection = direction;
            _lungeCheatStartTime = 0f;
            _lungeCheatDuration = LeviathanAttackBurstDurationSeconds;
            _lungeCheatActive = false;
            CapturePredatorLungeContactTarget(target, targetPosition);
            if (ShouldAbortPredatorLungeDistanceGate(lungeDistance) || ShouldAbortPredatorLungeGeometryGate())
                AbortPredatorLungeForGlancingBlow();
            else
                ActivatePredatorLungeCheat();
        }

        private bool TryApplyPredatorLungeCheatFixedStep()
        {
            if (_rb == null)
                return false;

            if (!_lungeCheatActive)
                return false;

            if (IsPredatorStunnedActive())
            {
                ClearPredatorLungeCheat();
                return false;
            }

            float duration = math.max(0.001f, _lungeCheatDuration);
            float t = math.saturate((_cognitionTimeSeconds - _lungeCheatStartTime) * math.rcp(duration));
            float ease = t * t * (3f - 2f * t);
            double3 start = _lungeCheatStartAup.ToAbsoluteDouble3();
            double3 target = _lungeCheatTargetAup.ToAbsoluteDouble3();
            double3 nextAbsolute = start + ((target - start) * ease);
            AbsoluteUniversePosition nextAup = AbsoluteUniversePosition.FromAbsolutePosition(nextAbsolute);
            ApplyAupPresentationPosition(in nextAup);
            if (t >= 0.999f)
                ClearPredatorLungeCheat();

            return true;
        }

        private void ClearPredatorLungeCheat()
        {
            EndAupTeleportIsolation();
            _lungeCheatActive = false;
            _lungeCheatStartTime = 0f;
            _lungeCheatDuration = 0f;
            _lungeCheatStartPosition = Vector3.zero;
            _lungeCheatTargetPosition = Vector3.zero;
            _lungeCheatDirection = Vector3.zero;
            _lungeCheatStartAup = default;
            _lungeCheatTargetAup = default;
            _lungeContactTargetCenter = Vector3.zero;
            _lungeContactTargetExtents = Vector3.zero;
            _lungeContactTargetRight = Vector3.right;
            _lungeContactTargetUp = Vector3.up;
            _lungeContactTargetForward = Vector3.forward;
            _lungeContactTargetHash = 0u;
            _lungeContactTargetMaterialId = HighSpeedImpactSignal.MaterialMetal;
            _lungeContactTargetActive = false;
        }

        private bool ShouldAbortPredatorLungeDistanceGate(float requestedDistance)
        {
            double lungeDistanceSq = AbsoluteUniversePosition.DistanceSq(in _lungeCheatStartAup, in _lungeCheatTargetAup);
            double requestedDistanceSq = (double)math.max(0.001f, requestedDistance) * math.max(0.001f, requestedDistance);
            return lungeDistanceSq < requestedDistanceSq * 0.08d;
        }

        private bool ShouldAbortPredatorLungeGeometryGate()
        {
            double3 start = _lungeCheatStartAup.ToAbsoluteDouble3();
            double3 target = _lungeCheatTargetAup.ToAbsoluteDouble3();
            double3 delta = target - start;
            double horizontalDistanceSq = (delta.x * delta.x) + (delta.z * delta.z);
            double verticalDistanceSq = delta.y * delta.y;
            return verticalDistanceSq > (horizontalDistanceSq * PredatorLungeVerticalSlopeAbortRatioSq) +
                   PredatorLungeVerticalStepAbortMetersSq;
        }

        private void ActivatePredatorLungeCheat()
        {
            BeginAupTeleportIsolation();
            _lungeCheatStartTime = _cognitionTimeSeconds;
            _lungeCheatActive = true;
            PublishLeviathanScatterPulse(_lungeCheatStartPosition, _lungeCheatDirection, AcousticPingLeviathanScatterRadiusMeters, AcousticPingLeviathanScatterDurationSeconds);
            Hecton8.Systems.AI.DirectorAIEvents.TryRaiseThreatSpike(_lungeCheatStartPosition, 1f);
        }

        private void AbortPredatorLungeForGlancingBlow()
        {
            ClearPredatorLungeCheat();
            _attackBurstUntilTime = 0f;
            ClearProceduralStrikeIntent();
        }

        private void ClearPredatorDeafening()
        {
            _predatorDeafenedUntilTime = 0f;
            _predatorDeafenedWanderAup = default;
            _hasPredatorDeafenedWanderAup = false;
        }

        private void ApplyAupPresentationPosition(in AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            Vector3 nextPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (_rb != null)
            {
                if (_lungeCheatActive && TryResolvePredatorLungeCcdPosition(nextPosition, out Vector3 ccdPosition))
                    nextPosition = ccdPosition;

                ApplyIsolatedRigidbodyTeleport(nextPosition);
                return;
            }

            _pendingAupPresentationPosition = nextPosition;
            _pendingAupPresentationPoseDirty = true;
        }

        private void FlushQueuedAupPresentationPose()
        {
            if (!_pendingAupPresentationPoseDirty)
                return;

            _pendingAupPresentationPoseDirty = false;
            transform.SetPositionAndRotation(_pendingAupPresentationPosition, transform.rotation);
        }

        private bool TryResolvePredatorLungeCcdPosition(Vector3 targetPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = targetPosition;
            if (_rb == null)
                return false;

            Vector3 startPosition = _rb.position;
            Vector3 displacement = targetPosition - startPosition;
            float displacementSq = displacement.sqrMagnitude;
            if (!math.isfinite(displacementSq) || displacementSq <= 0.000001f)
                return false;

            float fixedDeltaTime = math.max(_lastFixedTickDeltaSeconds, 0.0001f);
            Vector3 impliedVelocity = displacement * math.rcp(fixedDeltaTime);
            if (!KinematicCcdContractMath.ShouldSchedule(new float3(impliedVelocity.x, impliedVelocity.y, impliedVelocity.z)))
                return false;

            float inverseDistance = math.rsqrt(displacementSq);
            float distance = displacementSq * inverseDistance;
            Vector3 direction = displacement * inverseDistance;
            if (!_lungeContactTargetActive)
                return false;

            float radius = ResolvePredatorLungeContactRadius();
            if (!TryResolvePredatorLungeObbSweep(
                    startPosition,
                    displacement,
                    radius + PredatorLungeCcdSkinWidth,
                    out float hitFraction,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal))
            {
                return false;
            }

            float3 normal3 = KinematicCcdContractMath.NormalizeOrFallback(
                new float3(hitNormal.x, hitNormal.y, hitNormal.z),
                new float3(-direction.x, -direction.y, -direction.z));
            Vector3 safeNormal = new Vector3(normal3.x, normal3.y, normal3.z);
            bool cornerHalt = false;
            float slideWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            bool lowTierStop = slideWeight <= 0.0001f;
            float safeDistance = KinematicCcdContractMath.ResolveRollbackDistance(
                distance * hitFraction,
                distance,
                PredatorLungeCcdSkinWidth);
            Vector3 resolvedDisplacement = direction * safeDistance;
            if (!cornerHalt && slideWeight > 0f)
            {
                Vector3 slide = displacement - safeNormal * Vector3.Dot(displacement, safeNormal);
                float slideSq = slide.sqrMagnitude;
                float remainingDistance = math.max(0f, distance - safeDistance);
                if (math.isfinite(slideSq) && slideSq > 0.000001f && remainingDistance > 0f)
                    resolvedDisplacement += slide * (remainingDistance * math.rsqrt(slideSq) * slideWeight);
            }

            resolvedPosition = startPosition + resolvedDisplacement;
            if (!TryResolveAupFromRuntimeOrigin(resolvedPosition, out _lungeCheatTargetAup))
                return false;

            EmitPredatorLungeCcdImpact(
                hitPoint,
                _lungeContactTargetHash,
                _lungeContactTargetMaterialId,
                safeNormal,
                impliedVelocity,
                lowTierStop,
                cornerHalt);
            return true;
        }

        private float ResolvePredatorLungeContactRadius()
        {
            if (_predatorLungeCcdCapsule != null)
            {
                Vector3 scale = _predatorLungeCcdCapsule.transform.lossyScale;
                float maxScale = math.max(math.abs(scale.x), math.max(math.abs(scale.y), math.abs(scale.z)));
                return math.max(0.01f, _predatorLungeCcdCapsule.radius * maxScale);
            }

            if (_predatorLungeCcdSphere != null)
            {
                Vector3 scale = _predatorLungeCcdSphere.transform.lossyScale;
                float maxScale = math.max(math.abs(scale.x), math.max(math.abs(scale.y), math.abs(scale.z)));
                return math.max(0.01f, _predatorLungeCcdSphere.radius * maxScale);
            }

            return PredatorLungeCcdFallbackRadius;
        }

        private void CapturePredatorLungeContactTarget(Transform target, Vector3 fallbackPosition)
        {
            _lungeContactTargetActive = false;
            _lungeContactTargetCenter = fallbackPosition;
            _lungeContactTargetExtents = Vector3.one * PredatorLungeTargetFallbackExtent;
            _lungeContactTargetRight = target != null ? NormalizeVectorOrFallback(target.right, Vector3.right) : Vector3.right;
            _lungeContactTargetUp = target != null ? NormalizeVectorOrFallback(target.up, Vector3.up) : Vector3.up;
            _lungeContactTargetForward = target != null ? NormalizeVectorOrFallback(target.forward, Vector3.forward) : Vector3.forward;
            _lungeContactTargetHash = 0u;
            _lungeContactTargetMaterialId = HighSpeedImpactSignal.MaterialMetal;
            if (target == null)
                return;

            Collider targetCollider = null;
            target.TryGetComponent(out targetCollider);
            if (targetCollider == null)
                targetCollider = target.GetComponentInChildren<Collider>(true);

            Bounds bounds = targetCollider != null
                ? targetCollider.bounds
                : new Bounds(fallbackPosition, Vector3.one * (PredatorLungeTargetFallbackExtent * 2f));
            if (!IsFiniteBounds(bounds) || bounds.extents.sqrMagnitude <= 0.000001f)
                bounds = new Bounds(fallbackPosition, Vector3.one * (PredatorLungeTargetFallbackExtent * 2f));

            _lungeContactTargetCenter = bounds.center;
            _lungeContactTargetExtents = new Vector3(
                math.max(0.05f, math.abs(bounds.extents.x)),
                math.max(0.05f, math.abs(bounds.extents.y)),
                math.max(0.05f, math.abs(bounds.extents.z)));
            _lungeContactTargetHash = targetCollider != null
                ? Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(targetCollider.GetEntityId()))
                : Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(target.GetEntityId()));
            if (_lungeContactTargetHash == 0u)
                _lungeContactTargetHash = 1u;
            _lungeContactTargetMaterialId = ResolveHighSpeedImpactTargetMaterialId(targetCollider, HighSpeedImpactSignal.MaterialMetal);
            _lungeContactTargetActive = true;
        }

        private bool TryResolvePredatorLungeObbSweep(
            Vector3 startPosition,
            Vector3 displacement,
            float padding,
            out float hitFraction,
            out Vector3 hitPoint,
            out Vector3 hitNormal)
        {
            hitFraction = 0f;
            hitPoint = startPosition;
            hitNormal = -NormalizeVectorOrFallback(displacement, ResolveSelfLogicForward());
            float3 center = (float3)_lungeContactTargetCenter;
            float3 axisX = (float3)NormalizeVectorOrFallback(_lungeContactTargetRight, Vector3.right);
            float3 axisY = (float3)NormalizeVectorOrFallback(_lungeContactTargetUp, Vector3.up);
            float3 axisZ = (float3)NormalizeVectorOrFallback(_lungeContactTargetForward, Vector3.forward);
            float3 startDelta = (float3)startPosition - center;
            float3 localStart = new float3(
                math.dot(startDelta, axisX),
                math.dot(startDelta, axisY),
                math.dot(startDelta, axisZ));
            float3 worldDelta = (float3)displacement;
            float3 localDelta = new float3(
                math.dot(worldDelta, axisX),
                math.dot(worldDelta, axisY),
                math.dot(worldDelta, axisZ));
            float3 extents = new float3(
                math.max(0.05f, math.abs(_lungeContactTargetExtents.x) + padding),
                math.max(0.05f, math.abs(_lungeContactTargetExtents.y) + padding),
                math.max(0.05f, math.abs(_lungeContactTargetExtents.z) + padding));

            bool startsInside = math.all(localStart >= -extents) && math.all(localStart <= extents);
            float tMin = 0f;
            float tMax = 1f;
            float3 localNormal = ResolveObbExitNormal(localStart, extents);
            if (!startsInside)
            {
                if (!AccumulateSweptAabbAxis(localStart.x, localDelta.x, extents.x, new float3(1f, 0f, 0f), ref tMin, ref tMax, ref localNormal) ||
                    !AccumulateSweptAabbAxis(localStart.y, localDelta.y, extents.y, new float3(0f, 1f, 0f), ref tMin, ref tMax, ref localNormal) ||
                    !AccumulateSweptAabbAxis(localStart.z, localDelta.z, extents.z, new float3(0f, 0f, 1f), ref tMin, ref tMax, ref localNormal))
                {
                    return false;
                }
            }

            hitFraction = math.saturate(startsInside ? 0f : tMin);
            float3 normalWorld = axisX * localNormal.x + axisY * localNormal.y + axisZ * localNormal.z;
            hitNormal = NormalizeVectorOrFallback(new Vector3(normalWorld.x, normalWorld.y, normalWorld.z), -NormalizeVectorOrFallback(displacement, Vector3.forward));
            Vector3 centerAtHit = startPosition + displacement * hitFraction;
            hitPoint = centerAtHit - hitNormal * padding;
            return IsFiniteVector(hitPoint) && IsFiniteVector(hitNormal);
        }

        private static bool AccumulateSweptAabbAxis(
            float localStart,
            float localDelta,
            float extent,
            float3 axisNormal,
            ref float tMin,
            ref float tMax,
            ref float3 localNormal)
        {
            if (!math.isfinite(localStart) || !math.isfinite(localDelta) || !math.isfinite(extent))
                return false;

            if (math.abs(localDelta) <= 0.000001f)
                return localStart >= -extent && localStart <= extent;

            float invDelta = math.rcp(localDelta);
            float nearT;
            float farT;
            float nearSign;
            if (localDelta > 0f)
            {
                nearT = (-extent - localStart) * invDelta;
                farT = (extent - localStart) * invDelta;
                nearSign = -1f;
            }
            else
            {
                nearT = (extent - localStart) * invDelta;
                farT = (-extent - localStart) * invDelta;
                nearSign = 1f;
            }

            if (nearT > tMin)
            {
                tMin = nearT;
                localNormal = axisNormal * nearSign;
            }

            tMax = math.min(tMax, farT);
            return tMin <= tMax && tMax >= 0f && tMin <= 1f;
        }

        private static float3 ResolveObbExitNormal(float3 localPoint, float3 extents)
        {
            float3 distanceToFace = extents - math.abs(localPoint);
            if (distanceToFace.x <= distanceToFace.y && distanceToFace.x <= distanceToFace.z)
                return new float3(math.select(1f, -1f, localPoint.x < 0f), 0f, 0f);
            if (distanceToFace.y <= distanceToFace.z)
                return new float3(0f, math.select(1f, -1f, localPoint.y < 0f), 0f);
            return new float3(0f, 0f, math.select(1f, -1f, localPoint.z < 0f));
        }

        private static byte ResolveHighSpeedImpactTargetMaterialId(Collider hitCollider, byte fallbackMaterialId)
        {
            if (hitCollider == null)
                return fallbackMaterialId;

            if (hitCollider.TryGetComponent(out IImpactMaterialProvider directProvider))
                return directProvider.ImpactAudioMaterialId;

            IImpactMaterialProvider parentProvider = hitCollider.GetComponentInParent<IImpactMaterialProvider>();
            return parentProvider != null ? parentProvider.ImpactAudioMaterialId : fallbackMaterialId;
        }

        private void EmitPredatorLungeCcdImpact(
            Vector3 point,
            uint targetHash,
            byte targetMaterialId,
            Vector3 safeNormal,
            Vector3 impliedVelocity,
            bool lowTierStop,
            bool cornerHalt)
        {
            if (!math.isfinite(point.x) || !math.isfinite(point.y) || !math.isfinite(point.z))
                point = _rb != null ? _rb.position : transform.position;

            float speedSq = impliedVelocity.sqrMagnitude;
            if (!math.isfinite(speedSq))
                speedSq = 0f;

            float impactSpeed = speedSq > 0.000001f ? speedSq * math.rsqrt(speedSq) : 0f;
            float lostKineticEnergy = KinematicCcdContractMath.KineticEnergy(_rb != null ? _rb.mass : 1f, speedSq);
            byte flags = 0;
            if (cornerHalt)
                flags |= HighSpeedImpactSignal.FlagCornerHalt;
            if (lowTierStop)
                flags |= HighSpeedImpactSignal.FlagLowTierStop;

            if (!TryResolveAupFromRuntimeOrigin(point, out AbsoluteUniversePosition pointAup))
                return;

            byte sourceMaterialId = HighSpeedImpactSignal.MaterialOrganic;
            HighSpeedImpactSignal signal = default;
            signal.PointAup = pointAup;
            signal.Normal = new float3(safeNormal.x, safeNormal.y, safeNormal.z);
            signal.LostKineticEnergy = lostKineticEnergy;
            signal.ImpactSpeed = impactSpeed;
            signal.SourceHash = ResolveStableFaunaHash(FaunaLeviathanBiteHashSalt, 0u);
            signal.TargetHash = targetHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.SourceKind = HighSpeedImpactSignal.SourceLeviathan;
            signal.Flags = flags;
            signal.PrimaryMaterialId = targetMaterialId;
            signal.SecondaryMaterialId = sourceMaterialId;
            signal.EffectiveMass = _rb != null ? math.max(0f, _rb.mass) : 0f;
            signal.MaterialHash = HighSpeedImpactSignal.ComposeMaterialHash(signal.TargetHash, targetMaterialId, sourceMaterialId);
            SignalBus<HighSpeedImpactSignal>.TryPush(in signal);

            ImpactSignal impact = default;
            impact.PointAup = pointAup;
            impact.Velocity = impactSpeed;
            impact.Intensity = math.saturate(impactSpeed * 0.045f + lostKineticEnergy * 0.00002f);
            impact.PrimaryBodyId = signal.SourceHash;
            impact.WeightClass = 3;
            impact.Flags = flags;
            SignalBus<ImpactSignal>.TryPush(in impact);
            CameraJuiceSignals.TryPublishImpact(in impact, signal.Normal);

            DebrisSpawnSignal debris = default;
            debris.PositionAup = pointAup;
            debris.SourceEntityId = signal.SourceHash;
            debris.Intensity01 = impact.Intensity;
            debris.DebrisKind = DebrisSpawnSignal.DebrisKindSparks;
            debris.Flags = flags;
            SignalBus<DebrisSpawnSignal>.TryPush(in debris);

            HapticRequest haptic = default;
            haptic.Intensity01 = math.saturate(lostKineticEnergy * 0.00005f);
            haptic.DurationSeconds = math.lerp(0.04f, 0.18f, haptic.Intensity01);
            haptic.Frequency01 = math.saturate(impactSpeed * 0.04f);
            haptic.SourceHash = signal.SourceHash;
            haptic.Frame = signal.Frame;
            haptic.Channel = HapticRequest.ChannelCollision;
            haptic.Flags = flags;
            SignalBus<HapticRequest>.TryPush(in haptic);

            if (signal.TargetHash != 0u && lostKineticEnergy >= KinematicCcdContractMath.MassiveLostKineticEnergyJoules)
            {
                Hecton8.Core.Contracts.Signals.CombatDamageSignal damage = default;
                damage.ImpactAup = pointAup.ToAbsoluteDouble3();
                damage.Direction = signal.Normal;
                damage.Magnitude = math.min(600f, lostKineticEnergy * 0.004f);
                damage.DamageType = (uint)DamageTypeMask.Impact;
                damage.TargetHash = signal.TargetHash;
                damage.SourceHash = signal.SourceHash;
                damage.Frame = signal.Frame;
                damage.SourceId = signal.SourceHash > ushort.MaxValue ? ushort.MaxValue : (ushort)signal.SourceHash;
                damage.TargetId = signal.TargetHash > ushort.MaxValue ? ushort.MaxValue : (ushort)signal.TargetHash;
                damage.Channel = 0;
                damage.Flags = 0;
                damage.IntegrityDelta = 1;
                SignalBus<CombatDamageSignal>.TryPush(in damage);
            }

        }

        private void ApplyIsolatedRigidbodyTeleport(Vector3 nextPosition)
        {
            if (!_lungeTeleportIsolationActive)
                BeginAupTeleportIsolation();
            _rb.position = nextPosition;
            TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
            TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
        }

        private void BeginAupTeleportIsolation()
        {
            if (_rb == null || _lungeTeleportIsolationActive)
                return;

            _lungeTeleportRestoreKinematic = _rb.isKinematic;
            _lungeTeleportRestoreCollisions = _rb.detectCollisions;
            if (_lungeTeleportRestoreCollisions)
                _rb.detectCollisions = false;
            if (!_lungeTeleportRestoreKinematic)
                _rb.isKinematic = true;
            _lungeTeleportIsolationActive = true;
        }

        private void EndAupTeleportIsolation()
        {
            if (!_lungeTeleportIsolationActive)
                return;

            if (_rb != null)
            {
                _rb.isKinematic = _lungeTeleportRestoreKinematic;
                _rb.detectCollisions = _lungeTeleportRestoreCollisions;
            }

            _lungeTeleportIsolationActive = false;
            _lungeTeleportRestoreKinematic = false;
            _lungeTeleportRestoreCollisions = false;
        }

        private void ApplyLeviathanAttackTelegraphMotion(ref Vector3 desiredDirection, ref float forceMultiplier, ref float speedMultiplier)
        {
            if (!ShouldUseProceduralLeviathanPresentation())
                return;

            if (_attackTelegraphActive && _telegraphedAttackTarget != null && _cognitionTimeSeconds < _attackTelegraphBurstTime)
            {
                if (!TryResolveAttackTargetLogicPosition(_telegraphedAttackTarget, out Vector3 attackTargetPosition))
                {
                    ClearAttackTelegraphState();
                    return;
                }

                Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                    ? resolvedSelfPosition
                    : attackTargetPosition + ResolveSelfLogicForward();
                Vector3 awayFromTarget = selfPosition - attackTargetPosition;
                if (awayFromTarget.sqrMagnitude > 0.0001f)
                    desiredDirection = ResolveDominantAxisDirection(awayFromTarget);

                forceMultiplier *= LeviathanAttackTelegraphPullbackForceScale;
                speedMultiplier *= LeviathanAttackTelegraphPullbackSpeedScale;
                return;
            }

            if (_cognitionTimeSeconds < _attackBurstUntilTime)
            {
                forceMultiplier = math.max(forceMultiplier, LeviathanAttackBurstMultiplier);
                speedMultiplier = math.max(speedMultiplier, LeviathanAttackBurstMultiplier);
            }
        }

        private void EmitLeviathanAttackTelegraphPing(Vector3 sourcePosition)
        {
            if (!TryResolvePlayerListenerPosition(out Vector3 listenerPosition, out Transform playerRoot))
                return;

            float radius = PredatorKillAudioRadiusMeters * 2.5f;
            double distanceSqr = ResolveRuntimeAupDistanceSq(listenerPosition, sourcePosition);
            double radiusSqr = (double)radius * radius;
            if (distanceSqr > radiusSqr)
                return;

            float proximity = 1f - math.saturate((float)(distanceSqr * math.rcp(radiusSqr)));
            float intensity = math.saturate(0.45f + proximity * 0.55f);
            float transmission01 = 1f;
            float lowPassCutoffHz = LeviathanAttackTelegraphLowPassCutoffHz;
            int sensoryMask = AcousticOcclusionUtility.BuildSensoryMask();
            if (AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                    sourcePosition,
                    listenerPosition,
                    sensoryMask,
                    transform,
                    playerRoot,
                    out AcousticOcclusionResult occlusion))
            {
                transmission01 = math.saturate(occlusion.Transmission01);
                lowPassCutoffHz = math.clamp(
                    math.min(occlusion.LowPassCutoffHz, LeviathanAttackTelegraphLowPassCutoffHz),
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            }
            else
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(sourcePosition, listenerPosition, sensoryMask, transform, playerRoot);
            }

            TryPushProceduralAudioPing(
                sourcePosition,
                intensity,
                LeviathanAttackTelegraphAudioDurationSeconds,
                transmission01,
                lowPassCutoffHz,
                ProceduralAudioPingKindLeviathanRoar);
        }

        private bool TryApplyPredatorPhotophobia(float3 selfPosition, in FaunaPerceptionSnapshot perceptionSnapshot)
        {
            bool currentlyStunned = IsPredatorStunnedActive();
            if (_isDead || !IsApexPredator())
                return currentlyStunned;

            if (currentlyStunned)
            {
                ApplyPredatorStunnedPresentation();
                return true;
            }

            if ((_predatorSensoryStateBits & PredatorSensoryPhotophobicBit) == 0)
                return false;

            if (_cachedPredatorPhotophobiaDistanceSqr > PredatorPhotophobiaDistanceMetersSqr ||
                _cachedPredatorPhotophobiaDot <= PredatorPhotophobiaDotThresholdSqr)
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return false;
            }

            _predatorSquadStateBits |= PredatorStateStunnedBit;
            _predatorSquadStateBits &= ~PredatorSquadStateHuntingBit;
            PromoteHunterSquadAlphaAfterLocalLoss();
            ApplyPredatorSensoryBits(PredatorSensoryPhotophobicBit, PredatorSensoryAggroBit);
            _predatorStunnedUntilTime = _cognitionTimeSeconds + PredatorPhotophobiaStunSeconds;
            ApplyPredatorStunnedPresentation();
            return true;
        }

        private void RefreshPredatorPhotophobiaCache()
        {
            int frame = Time.frameCount;
            if (frame < _nextPredatorPhotophobiaCacheFrame)
                return;

            _nextPredatorPhotophobiaCacheFrame = frame + PredatorPhotophobiaCacheFrameInterval;
            _cachedPredatorPhotophobiaDot = 0f;
            _cachedPredatorPhotophobiaDistanceSqr = double.MaxValue;
            if (_isDead || !IsApexPredator())
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            bool hasRuntimeContext = TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                                     runtimeContext != null;
            PlayerLookState lookState = hasRuntimeContext ? runtimeContext.LookState : default;
            bool hasLookState = hasRuntimeContext &&
                                (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                                math.lengthsq(lookState.AimForward) > 0.0001f;
            PlayerMovementRuntimeState movementState = hasRuntimeContext ? runtimeContext.MovementState : default;
            bool hasMovementRoot = hasRuntimeContext &&
                                   (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            bool hasMovementForward = hasRuntimeContext &&
                                      (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u &&
                                      math.lengthsq(movementState.Forward) > 0.0001f;
            PlayerFlashlight flashlight = hasRuntimeContext && runtimeContext.Flashlight != null
                ? runtimeContext.Flashlight
                : playerContext != null ? playerContext.Flashlight : null;
            if (flashlight == null || !flashlight.IsOn || (!hasLookState && !hasMovementRoot))
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return;
            }

            Vector3 lightPosition = hasLookState
                ? ToVector3(lookState.EyePosition)
                : ToVector3(movementState.PredictedAup.ToRuntimeFloat3());
            float3 lightForward = hasLookState
                ? lookState.AimForward
                : hasMovementForward ? movementState.Forward : float3.zero;
            float forwardLenSq = math.lengthsq(lightForward);
            if (forwardLenSq <= 0.0001f)
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return;
            }

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition predatorAup))
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return;
            }

            AbsoluteUniversePosition lightAup;
            if (hasLookState)
            {
                if (!TryResolveAupFromRuntimeOrigin(lightPosition, out lightAup))
                {
                    ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                    return;
                }
            }
            else
            {
                lightAup = movementState.PredictedAup;
            }

            double distanceSqr = AbsoluteUniversePosition.DistanceSq(in predatorAup, in lightAup);
            _cachedPredatorPhotophobiaDistanceSqr = distanceSqr;
            if (distanceSqr > PredatorPhotophobiaDistanceMetersSqr || distanceSqr <= 0.0001d)
            {
                ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
                return;
            }

            double3 toPredator = AbsoluteUniversePosition.DeltaMetersClamped(in predatorAup, in lightAup);
            double rawDot =
                ((double)lightForward.x * toPredator.x) +
                ((double)lightForward.y * toPredator.y) +
                ((double)lightForward.z * toPredator.z);
            _cachedPredatorPhotophobiaDot = rawDot > 0d
                ? (float)((rawDot * rawDot) * math.rcp(math.max(0.0001d, (double)forwardLenSq * distanceSqr)))
                : 0f;
            ApplyPredatorSensoryBits(
                _cachedPredatorPhotophobiaDot > PredatorPhotophobiaDotThresholdSqr ? PredatorSensoryPhotophobicBit : (byte)0,
                _cachedPredatorPhotophobiaDot > PredatorPhotophobiaDotThresholdSqr ? (byte)0 : PredatorSensoryPhotophobicBit);
        }

        private bool IsPredatorStunnedActive()
        {
            if ((_predatorSquadStateBits & PredatorStateStunnedBit) == 0u)
                return false;

            if (_cognitionTimeSeconds <= _predatorStunnedUntilTime)
                return true;

            _predatorSquadStateBits &= ~PredatorStateStunnedBit;
            ApplyPredatorSensoryBits(0, PredatorSensoryPhotophobicBit);
            _predatorStunnedUntilTime = 0f;
            return false;
        }

        private bool IsPredatorDeafenedActive()
        {
            if ((_predatorSensoryStateBits & PredatorSensoryDeafenedBit) == 0)
                return false;

            if (_cognitionTimeSeconds <= _predatorDeafenedUntilTime)
                return true;

            ClearPredatorDeafening();
            ApplyPredatorSensoryBits(0, PredatorSensoryDeafenedBit);
            return false;
        }

        private bool TryApplyPredatorDeafenedWander(float3 selfPosition)
        {
            if (!IsPredatorDeafenedActive())
                return false;

            if (!_hasPredatorDeafenedWanderAup)
                AssignPredatorDeafenedWanderAup(ToVector3(selfPosition));

            float3 runtimeTarget = _predatorDeafenedWanderAup.ToRuntimeFloat3();
            Vector3 target = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
            Vector3 direction = target - ToVector3(selfPosition);
            _cachedDesiredDirection = direction.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(direction) : ResolveSelfLogicForward();
            _currentStateCache = AIState.Wander;
            _stateMachine.currentState = AIState.Wander;
            _predatorSquadStateBits &= ~(PredatorSquadStateHuntingBit | PredatorSquadStateFlankingBit);
            ApplyPredatorSensoryBits(0, (byte)(PredatorSensoryHearsSonarBit | PredatorSensoryAggroBit));
            return true;
        }

        private void AssignPredatorDeafenedWanderAup(Vector3 sourcePosition)
        {
            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                selfPosition = sourcePosition;
            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup) &&
                !TryResolveAupFromRuntimeOrigin(selfPosition, out selfAup))
            {
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(sourcePosition, out AbsoluteUniversePosition sourceAup))
                return;

            double3 awayAbsolute = AbsoluteUniversePosition.DeltaMetersClamped(in selfAup, in sourceAup);
            float3 away = AupPrecisionMath.DowncastLocalDelta(awayAbsolute, float3.zero);
            float awaySq = math.lengthsq(away);
            if (awaySq <= 0.0001f)
            {
                uint seed = _uniqueInstanceUid == 0u ? 1u : _uniqueInstanceUid;
                away = ResolveOctantDirectionXZ(seed ^ 0x9E3779B9u);
            }
            else
            {
                Vector3 awayDirection = ResolveDominantAxisDirection(new Vector3(away.x, away.y, away.z));
                away = new float3(awayDirection.x, awayDirection.y, awayDirection.z);
            }

            _predatorDeafenedWanderAup = AbsoluteUniversePosition.OffsetMeters(
                in selfAup,
                new double3(away.x, away.y, away.z) * PredatorDeafenedWanderRadiusMeters);
            _hasPredatorDeafenedWanderAup = true;
            float3 runtimeTarget = _predatorDeafenedWanderAup.ToRuntimeFloat3();
            Vector3 target = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
            Vector3 direction = target - selfPosition;
            _cachedDesiredDirection = direction.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(direction) : ResolveSelfLogicForward();
        }

        private static float3 ResolveOctantDirectionXZ(uint seed)
        {
            switch ((int)(seed & 7u))
            {
                case 0:
                    return new float3(1f, 0f, 0f);
                case 1:
                    return new float3(0.70710678f, 0f, 0.70710678f);
                case 2:
                    return new float3(0f, 0f, 1f);
                case 3:
                    return new float3(-0.70710678f, 0f, 0.70710678f);
                case 4:
                    return new float3(-1f, 0f, 0f);
                case 5:
                    return new float3(-0.70710678f, 0f, -0.70710678f);
                case 6:
                    return new float3(0f, 0f, -1f);
                default:
                    return new float3(0.70710678f, 0f, -0.70710678f);
            }
        }

        private bool IsTier3LeviathanRuntime()
        {
            return IsApexPredator() && ShouldUseProceduralLeviathanPresentation();
        }

        private void ApplyPredatorStunnedPresentation()
        {
            ClearAttackTelegraphState();
            ClearPredatorLungeCheat();
            ClearProceduralStrikeIntent();
            _attackBurstUntilTime = 0f;
            _cachedDesiredDirection = Vector3.zero;
            _stateMachine.currentForceMultiplier = 0f;
            _stateMachine.currentSpeedMultiplier = 0f;
            _stateMachine.currentState = AIState.Idle;
            _currentStateCache = AIState.Idle;
        }

        private void ApplyPredatorSensoryBits(byte setBits, byte clearBits)
        {
            _predatorSensoryStateBits = (byte)((_predatorSensoryStateBits | setBits) & ~clearBits);
        }

        private void ClearPredatorSquadState()
        {
            _predatorSquadStateBits = 0u;
            _predatorSensoryStateBits = 0;
            _predatorSquadOrdinal = 0;
            _predatorStunnedUntilTime = 0f;
            _predatorDeafenedUntilTime = 0f;
            _predatorDeafenedWanderAup = default;
            _hasPredatorDeafenedWanderAup = false;
            _nextDirectorAmbushTime = 0f;
            _directorColdTickUntilTime = 0f;
            _directorColdTickAccumulator = 0f;
            _cachedPredatorPhotophobiaDot = 0f;
            _cachedPredatorPhotophobiaDistanceSqr = 0d;
            _nextPredatorPhotophobiaCacheFrame = 0;
            _leviathanScatterSector = default;
            _hasLeviathanScatterSector = false;
        }

        private void ClearPredatorSquadStatePreserveAlphaHandoff()
        {
            uint alphaBits = _predatorSquadStateBits & PredatorSquadAlphaMask;
            ClearPredatorSquadState();
            _predatorSquadStateBits = alphaBits;
        }

        private void PromoteHunterSquadAlphaAfterLocalLoss()
        {
            uint currentAlpha = (_predatorSquadStateBits & PredatorSquadAlphaMask) >> PredatorSquadAlphaShift;
            if ((uint)_predatorSquadOrdinal != currentAlpha)
                return;

            uint promotedAlpha = currentAlpha >= 2u ? 0u : currentAlpha + 1u;
            _predatorSquadStateBits = (_predatorSquadStateBits & ~PredatorSquadAlphaMask) |
                                      ((promotedAlpha & 0x3u) << PredatorSquadAlphaShift);
        }

        private bool TryApplyPassiveFlashlightReaction(float3 selfPosition)
        {
            if (!_sensorSuite.hasPlayerFlashlightConeHit ||
                !IsSmallPassiveFauna() ||
                _isDead)
            {
                return false;
            }

            Vector3 threatPosition = _sensorSuite.playerFlashlightThreatPosition;
            Vector3 awayDirection = ToVector3(selfPosition) - threatPosition;
            if (awayDirection.sqrMagnitude <= 0.0001f)
                awayDirection = ResolveSelfLogicForward();
            else
                awayDirection = ResolveDominantAxisDirection(awayDirection);

            _utilityBrain.ForceRetreat(threatPosition, _cognitionTimeSeconds, PassiveFlashlightFleeDurationSeconds);
            _utilityBrain.ApplyExternalState(AIState.Escape, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.Escape;
            _currentStateCache = AIState.Escape;
            _cachedDesiredDirection = awayDirection;
            _sensorSuite.isScattering = true;
            _sensorSuite.scatterDirection = awayDirection;
            _passiveFlashlightDimUntilTime = math.max(_passiveFlashlightDimUntilTime, _cognitionTimeSeconds + PassiveFlashlightDimSeconds);
            ClearAttackTelegraphState();
            ClearCorpseLatchState();
            return true;
        }

        private bool IsSmallPassiveFauna()
        {
            if (_faunaDataTemplate != null)
            {
                FaunaFoodChainTier tier = _faunaDataTemplate.FoodChainTier;
                bool smallTier = tier == FaunaFoodChainTier.Microfauna ||
                                 tier == FaunaFoodChainTier.SmallHerbivore ||
                                 tier == FaunaFoodChainTier.SwarmPassive;
                if (smallTier)
                    return _faunaDataTemplate.LightReactionMode == FaunaLightReactionMode.Aversion ||
                           _utilityBrain.IsActivePredator == 0;
            }

            if (_archetype != null)
                return !_archetype.isAggressive && _archetype.maxHealth < LargeCorpseResourceMinHealth;

            return !isAggressive && _utilityBrain.IsActivePredator == 0 && !IsLeviathan();
        }

        private void CacheBiolumPresentationLights()
        {
            _biolumPresentationLightCount = 0;
            _biolumPresentationLightScratch.Clear();
            GetComponentsInChildren(true, _biolumPresentationLightScratch);

            int sourceCount = _biolumPresentationLightScratch.Count;
            for (int i = 0; i < sourceCount && _biolumPresentationLightCount < MaxBiolumPresentationLights; i++)
            {
                Light candidate = _biolumPresentationLightScratch[i];
                if (candidate == null)
                    continue;

                _biolumPresentationLights[_biolumPresentationLightCount] = candidate;
                _biolumPresentationBaseIntensities[_biolumPresentationLightCount] = math.max(0f, candidate.intensity);
                _biolumPresentationLightCount++;
            }

            for (int i = _biolumPresentationLightCount; i < MaxBiolumPresentationLights; i++)
            {
                _biolumPresentationLights[i] = null;
                _biolumPresentationBaseIntensities[i] = 0f;
            }

            _biolumPresentationLightScratch.Clear();
        }

        private void EnsureFaunaPresentationMaterials()
        {
            _faunaPresentationOriginalMaterials.Clear();
            _faunaPresentationRuntimeMaterials.Clear();
            _faunaPresentationRuntimeMaterialMasks.Clear();
            _faunaPresentationMaterialScratch.Clear();
            if (_renderer == null)
                return;

            _renderer.GetSharedMaterials(_faunaPresentationMaterialScratch);
            int materialCount = _faunaPresentationMaterialScratch.Count;
            bool materialSlotsChanged = false;
            for (int i = 0; i < materialCount; i++)
            {
                Material sourceMaterial = _faunaPresentationMaterialScratch[i];
                if (sourceMaterial == null)
                    continue;

                ushort propertyMask = 0;
                if (sourceMaterial.HasProperty(_FaunaBiolumDimShaderId))
                    propertyMask |= FaunaPresentationBiolumMask;
                if (sourceMaterial.HasProperty(_FaunaCamouflageTintShaderId))
                    propertyMask |= FaunaPresentationCamouflageTintMask;
                if (sourceMaterial.HasProperty(_FaunaCamouflageParamsShaderId))
                    propertyMask |= FaunaPresentationCamouflageParamsMask;
                if (sourceMaterial.HasProperty(_FaunaCamouflageStrengthShaderId))
                    propertyMask |= FaunaPresentationCamouflageStrengthMask;
                if (sourceMaterial.HasProperty(_DeathDitherFadeShaderId))
                    propertyMask |= FaunaPresentationDeathDitherMask;
                if (sourceMaterial.HasProperty(_CorpseBloatAgeShaderId))
                    propertyMask |= FaunaPresentationCorpseBloatMask;
                if (sourceMaterial.HasProperty(_CorpseBloatStartTimeShaderId))
                    propertyMask |= FaunaPresentationCorpseBloatTimerMask;
                if (sourceMaterial.HasProperty(_CorpseBloatDurationShaderId))
                    propertyMask |= FaunaPresentationCorpseBloatDurationMask;
                if (sourceMaterial.HasProperty(_DecayAmountShaderId))
                    propertyMask |= FaunaPresentationDecayAmountMask;
                if (sourceMaterial.HasProperty(_HitFlashShaderId))
                    propertyMask |= FaunaPresentationHitFlashMask;
                if (sourceMaterial.HasProperty(_FaunaMutationHueShaderId))
                    propertyMask |= FaunaPresentationMutationHueMask;
                if (sourceMaterial.HasProperty(_FaunaMutationTwitchShaderId))
                    propertyMask |= FaunaPresentationMutationTwitchMask;
                if (sourceMaterial.HasProperty(_ColorId))
                    propertyMask |= FaunaPresentationColorMask;
                if (sourceMaterial.HasProperty(_BaseColorId))
                    propertyMask |= FaunaPresentationBaseColorMask;
                if (sourceMaterial.HasProperty(_EmissionColorId))
                    propertyMask |= FaunaPresentationEmissionColorMask;
                if (sourceMaterial.HasProperty(_H8FaunaGeneticMaskBytes0ShaderId) &&
                    sourceMaterial.HasProperty(_H8FaunaGeneticMaskBytes1ShaderId))
                    propertyMask |= FaunaPresentationGeneticMaskMask;
                if (propertyMask == 0)
                    continue;

                Material runtimeMaterial = new Material(sourceMaterial); // COLD ALLOC: Material[1] - per-fauna shader presentation state - owner: FaunaBrain
                runtimeMaterial.hideFlags = HideFlags.DontSave;
                if ((propertyMask & FaunaPresentationBiolumMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaBiolumDimShaderId, 1f);
                if ((propertyMask & FaunaPresentationCamouflageTintMask) != 0)
                    runtimeMaterial.SetVector(_FaunaCamouflageTintShaderId, ColorToVector(FaunaCamouflageTint));
                if ((propertyMask & FaunaPresentationCamouflageParamsMask) != 0)
                    runtimeMaterial.SetVector(_FaunaCamouflageParamsShaderId, FaunaCamouflageParams);
                if ((propertyMask & FaunaPresentationCamouflageStrengthMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaCamouflageStrengthShaderId, FaunaCamouflageStrength);
                if ((propertyMask & FaunaPresentationDeathDitherMask) != 0)
                    runtimeMaterial.SetFloat(_DeathDitherFadeShaderId, 0f);
                if ((propertyMask & FaunaPresentationCorpseBloatMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatAgeShaderId, 0f);
                if ((propertyMask & FaunaPresentationCorpseBloatTimerMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatStartTimeShaderId, -1f);
                if ((propertyMask & FaunaPresentationCorpseBloatDurationMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatDurationShaderId, ResolveCorpsePresentationDurationSeconds());
                if ((propertyMask & FaunaPresentationDecayAmountMask) != 0)
                    runtimeMaterial.SetFloat(_DecayAmountShaderId, 0f);
                if ((propertyMask & FaunaPresentationHitFlashMask) != 0)
                    runtimeMaterial.SetFloat(_HitFlashShaderId, 0f);
                if ((propertyMask & FaunaPresentationMutationHueMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaMutationHueShaderId, 0f);
                if ((propertyMask & FaunaPresentationMutationTwitchMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaMutationTwitchShaderId, 0f);
                if ((propertyMask & FaunaPresentationGeneticMaskMask) != 0)
                {
                    runtimeMaterial.SetVector(_H8FaunaGeneticMaskBytes0ShaderId, Vector4.zero);
                    runtimeMaterial.SetVector(_H8FaunaGeneticMaskBytes1ShaderId, Vector4.zero);
                }

                _faunaPresentationMaterialScratch[i] = runtimeMaterial;
                _faunaPresentationOriginalMaterials.Add(sourceMaterial);
                _faunaPresentationRuntimeMaterials.Add(runtimeMaterial);
                _faunaPresentationRuntimeMaterialMasks.Add(propertyMask);
                materialSlotsChanged = true;
            }

            if (materialSlotsChanged)
                _renderer.SetSharedMaterials(_faunaPresentationMaterialScratch);

            _faunaPresentationMaterialScratch.Clear();
        }

        private void ReleaseFaunaPresentationMaterials()
        {
            int runtimeMaterialCount = _faunaPresentationRuntimeMaterials.Count;
            if (_renderer != null && runtimeMaterialCount > 0)
            {
                _faunaPresentationMaterialScratch.Clear();
                _renderer.GetSharedMaterials(_faunaPresentationMaterialScratch);
                int slotCount = _faunaPresentationMaterialScratch.Count;
                bool restoredAnySlot = false;
                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    Material slotMaterial = _faunaPresentationMaterialScratch[slotIndex];
                    if (slotMaterial == null)
                        continue;

                    for (int runtimeIndex = 0; runtimeIndex < runtimeMaterialCount; runtimeIndex++)
                    {
                        if (!ReferenceEquals(slotMaterial, _faunaPresentationRuntimeMaterials[runtimeIndex]))
                            continue;

                        _faunaPresentationMaterialScratch[slotIndex] = _faunaPresentationOriginalMaterials[runtimeIndex];
                        restoredAnySlot = true;
                        break;
                    }
                }

                if (restoredAnySlot)
                    _renderer.SetSharedMaterials(_faunaPresentationMaterialScratch);
            }

            for (int i = 0; i < runtimeMaterialCount; i++)
            {
                Material runtimeMaterial = _faunaPresentationRuntimeMaterials[i];
                if (runtimeMaterial == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(runtimeMaterial);
                else
                    DestroyImmediate(runtimeMaterial);
            }

            _faunaPresentationMaterialScratch.Clear();
            _faunaPresentationOriginalMaterials.Clear();
            _faunaPresentationRuntimeMaterials.Clear();
            _faunaPresentationRuntimeMaterialMasks.Clear();
            _lastAppliedFaunaBiolumShader01 = -1f;
            _lastAppliedDeathDitherShader01 = -1f;
            _lastAppliedCorpseBloatShader01 = -1f;
            _lastAppliedDecayAmountShader01 = -1f;
            _lastAppliedHitFlashShader01 = -1f;
            _lastAppliedMutationHueShader01 = -1f;
            _lastAppliedMutationTwitchShader01 = -1f;
            _lastAppliedGeneticMask = ulong.MaxValue;
            _lastAppliedInfectionShaderSeverity01 = -1f;
            _lastAppliedInfectionShaderActive = false;
        }

        private static Vector4 PackGeneticMaskBytes0(ulong geneticMask)
        {
            return new Vector4(
                (byte)geneticMask,
                (byte)(geneticMask >> 8),
                (byte)(geneticMask >> 16),
                (byte)(geneticMask >> 24));
        }

        private static Vector4 PackGeneticMaskBytes1(ulong geneticMask)
        {
            return new Vector4(
                (byte)(geneticMask >> 32),
                (byte)(geneticMask >> 40),
                (byte)(geneticMask >> 48),
                (byte)(geneticMask >> 56));
        }

        private static Vector4 ColorToVector(Color color)
        {
            return new Vector4(color.r, color.g, color.b, color.a);
        }

        private void UpdateFaunaBiolumPresentation(float dt)
        {
            ConsumeRetinalBlindPresentationSignals();
            float targetDim = _cognitionTimeSeconds < _passiveFlashlightDimUntilTime
                ? PassiveFlashlightBiolumDimMultiplier
                : 1f;
            float retinalStrobe01 = ResolveRetinalBlindBiolumStrobe01();
            if (retinalStrobe01 > 0f)
                targetDim = retinalStrobe01;
            float responseX = math.max(0f, dt) * PassiveFlashlightBiolumResponseSharpness;
            float alpha = math.saturate(1f - math.rcp(1f + responseX + 0.5f * responseX * responseX));
            _faunaBiolumDim01 = math.lerp(_faunaBiolumDim01, targetDim, alpha);
            _hitFlash01 = math.max(0f, _hitFlash01 - (math.max(0f, dt) * HitFlashDecayPerSecond));
            float deathLightFade01 = 1f - math.saturate(_deathDitherFade01);
            ApplyBiolumPresentationLightScale(_faunaBiolumDim01 * deathLightFade01);
            ApplyFaunaPresentationShaderState(_faunaBiolumDim01, _deathDitherFade01, _corpseBloatAge01, _hitFlash01, _whaleFallDecay01);
        }

        private void ConsumeRetinalBlindPresentationSignals()
        {
            int retinalSlot = CreatureUtilityBrain.ResolveSlot(in _utilityBrain);
            if (retinalSlot < 0)
                return;

            ReadOnlySpan<FaunaStateChangedSignal> signals = SignalBus<FaunaStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                FaunaStateChangedSignal signal = signals[i];
                if (signal.StateKind != FaunaStateChangedSignalKinds.Blind ||
                    signal.Slot != (ushort)retinalSlot ||
                    (signal.Flags & FaunaStateChangedSignalFlags.StateActive) == 0 ||
                    signal.Frame == _lastRetinalBlindSignalFrame)
                {
                    continue;
                }

                _lastRetinalBlindSignalFrame = signal.Frame;
                _retinalBlindBiolumUntilTime = math.max(
                    _retinalBlindBiolumUntilTime,
                    _cognitionTimeSeconds + RetinalBlindBiolumStrobeDurationSeconds);
            }
        }

        private float ResolveRetinalBlindBiolumStrobe01()
        {
            if (_cognitionTimeSeconds >= _retinalBlindBiolumUntilTime)
                return 0f;

            int retinalSlot = math.max(0, CreatureUtilityBrain.ResolveSlot(in _utilityBrain));
            float phase = (_cognitionTimeSeconds * RetinalBlindBiolumStrobeFrequency) + ((retinalSlot & 31) * 0.6180339f);
            float primary = math.saturate(0.5f + (0.5f * RetinalExposureMath.SignedTriangle(phase)));
            float secondaryPhase = (_cognitionTimeSeconds * RetinalBlindBiolumSecondaryFrequency) + ((retinalSlot & 31) * 0.381966f) + 0.37f;
            float secondary = math.saturate(0.5f + (0.5f * RetinalExposureMath.SignedTriangle(secondaryPhase)));
            float quality = ResolveRetinalBlindPresentationQuality01();
            float intensityFloor = math.lerp(0.35f, 1f, quality);
            return math.saturate(
                RetinalBlindBiolumMinimum01 +
                ((1f - RetinalBlindBiolumMinimum01) * math.max(primary, secondary) * intensityFloor));
        }

        private static float ResolveRetinalBlindPresentationQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.isfinite(quality) ? math.saturate(quality) : 0f;
            return quality * quality * (3f - 2f * quality);
        }

        private void ApplyFaunaPresentationShaderState(float biolumDim01, float deathDitherFade01, float corpseBloatAge01, float hitFlash01, float decayAmount01 = 0f)
        {
            _pendingFaunaPresentationBiolumDim01 = math.saturate(biolumDim01);
            _pendingFaunaPresentationDeathDitherFade01 = math.saturate(deathDitherFade01);
            _pendingFaunaPresentationCorpseBloatAge01 = math.saturate(corpseBloatAge01);
            _pendingFaunaPresentationHitFlash01 = math.saturate(hitFlash01);
            _pendingFaunaPresentationDecayAmount01 = math.saturate(decayAmount01);
            _pendingFaunaPresentationShaderStateDirty = true;
        }

        private void FlushFaunaPresentationShaderState()
        {
            if (!_pendingFaunaPresentationShaderStateDirty)
                return;

            _pendingFaunaPresentationShaderStateDirty = false;
            ApplyFaunaPresentationShaderStateImmediate(
                _pendingFaunaPresentationBiolumDim01,
                _pendingFaunaPresentationDeathDitherFade01,
                _pendingFaunaPresentationCorpseBloatAge01,
                _pendingFaunaPresentationHitFlash01,
                _pendingFaunaPresentationDecayAmount01);
        }

        private void ApplyFaunaPresentationShaderStateImmediate(float biolumDim01, float deathDitherFade01, float corpseBloatAge01, float hitFlash01, float decayAmount01 = 0f)
        {
            int runtimeMaterialCount = _faunaPresentationRuntimeMaterials.Count;
            if (runtimeMaterialCount <= 0)
                return;

            float resolvedBiolumDim01 = math.saturate(biolumDim01);
            float resolvedDeathDitherFade01 = math.saturate(deathDitherFade01);
            float resolvedCorpseBloatAge01 = math.saturate(corpseBloatAge01);
            float resolvedDecayAmount01 = math.saturate(decayAmount01);
            float resolvedHitFlash01 = math.saturate(hitFlash01);
            float resolvedMutationHue01 = _hasGeneticTraits ? math.saturate(_geneticTraits.MutationHueShift01) : 0f;
            float resolvedMutationTwitch01 = _hasGeneticTraits ? math.saturate(_geneticTraits.MutationTwitch01) : 0f;
            ulong resolvedGeneticMask = _hasGeneticTraits ? _geneticTraits.Genome : 0UL;
            bool applyBiolum = math.abs(_lastAppliedFaunaBiolumShader01 - resolvedBiolumDim01) >= 0.001f;
            bool applyDeathDither = math.abs(_lastAppliedDeathDitherShader01 - resolvedDeathDitherFade01) >= 0.001f;
            bool applyCorpseBloat = math.abs(_lastAppliedCorpseBloatShader01 - resolvedCorpseBloatAge01) >= 0.001f;
            bool applyDecayAmount = math.abs(_lastAppliedDecayAmountShader01 - resolvedDecayAmount01) >= 0.001f;
            bool applyHitFlash = math.abs(_lastAppliedHitFlashShader01 - resolvedHitFlash01) >= 0.001f;
            bool applyMutationHue = math.abs(_lastAppliedMutationHueShader01 - resolvedMutationHue01) >= 0.001f;
            bool applyMutationTwitch = math.abs(_lastAppliedMutationTwitchShader01 - resolvedMutationTwitch01) >= 0.001f;
            bool applyGeneticMask = _lastAppliedGeneticMask != resolvedGeneticMask;
            if (!applyBiolum && !applyDeathDither && !applyCorpseBloat && !applyDecayAmount && !applyHitFlash && !applyMutationHue && !applyMutationTwitch && !applyGeneticMask)
                return;

            _lastAppliedFaunaBiolumShader01 = resolvedBiolumDim01;
            _lastAppliedDeathDitherShader01 = resolvedDeathDitherFade01;
            _lastAppliedCorpseBloatShader01 = resolvedCorpseBloatAge01;
            _lastAppliedDecayAmountShader01 = resolvedDecayAmount01;
            _lastAppliedHitFlashShader01 = resolvedHitFlash01;
            _lastAppliedMutationHueShader01 = resolvedMutationHue01;
            _lastAppliedMutationTwitchShader01 = resolvedMutationTwitch01;
            _lastAppliedGeneticMask = resolvedGeneticMask;
            Vector4 geneticMaskBytes0 = applyGeneticMask ? PackGeneticMaskBytes0(resolvedGeneticMask) : Vector4.zero;
            Vector4 geneticMaskBytes1 = applyGeneticMask ? PackGeneticMaskBytes1(resolvedGeneticMask) : Vector4.zero;
            for (int i = 0; i < runtimeMaterialCount; i++)
            {
                Material runtimeMaterial = _faunaPresentationRuntimeMaterials[i];
                if (runtimeMaterial == null)
                    continue;

                ushort propertyMask = _faunaPresentationRuntimeMaterialMasks[i];
                if (applyBiolum && (propertyMask & FaunaPresentationBiolumMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaBiolumDimShaderId, resolvedBiolumDim01);
                if (applyDeathDither && (propertyMask & FaunaPresentationDeathDitherMask) != 0)
                    runtimeMaterial.SetFloat(_DeathDitherFadeShaderId, resolvedDeathDitherFade01);
                if (applyCorpseBloat && (propertyMask & FaunaPresentationCorpseBloatMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatAgeShaderId, resolvedCorpseBloatAge01);
                if (applyDecayAmount && (propertyMask & FaunaPresentationDecayAmountMask) != 0)
                    runtimeMaterial.SetFloat(_DecayAmountShaderId, resolvedDecayAmount01);
                if (applyHitFlash && (propertyMask & FaunaPresentationHitFlashMask) != 0)
                    runtimeMaterial.SetFloat(_HitFlashShaderId, resolvedHitFlash01);
                if (applyMutationHue && (propertyMask & FaunaPresentationMutationHueMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaMutationHueShaderId, resolvedMutationHue01);
                if (applyMutationTwitch && (propertyMask & FaunaPresentationMutationTwitchMask) != 0)
                    runtimeMaterial.SetFloat(_FaunaMutationTwitchShaderId, resolvedMutationTwitch01);
                if (applyGeneticMask && (propertyMask & FaunaPresentationGeneticMaskMask) != 0)
                {
                    runtimeMaterial.SetVector(_H8FaunaGeneticMaskBytes0ShaderId, geneticMaskBytes0);
                    runtimeMaterial.SetVector(_H8FaunaGeneticMaskBytes1ShaderId, geneticMaskBytes1);
                }
            }
        }

        private void ResetCorpseBloatShaderTimer()
        {
            ApplyCorpseBloatShaderTimer(-1f);
        }

        private void ArmCorpseBloatShaderTimer()
        {
            ApplyCorpseBloatShaderTimer(ResolveCorpseBloatShaderClockSeconds());
        }

        private static float ResolveCorpseBloatShaderClockSeconds()
        {
            return Time.timeSinceLevelLoad;
        }

        private void ApplyCorpseBloatShaderTimer(float startTimeSeconds)
        {
            _pendingCorpseBloatShaderStartTimeSeconds = startTimeSeconds;
            _pendingCorpseBloatShaderTimerDirty = true;
        }

        private void FlushCorpseBloatShaderTimer()
        {
            if (!_pendingCorpseBloatShaderTimerDirty)
                return;

            _pendingCorpseBloatShaderTimerDirty = false;
            ApplyCorpseBloatShaderTimerImmediate(_pendingCorpseBloatShaderStartTimeSeconds);
        }

        private void ApplyCorpseBloatShaderTimerImmediate(float startTimeSeconds)
        {
            int runtimeMaterialCount = _faunaPresentationRuntimeMaterials.Count;
            if (runtimeMaterialCount <= 0)
                return;

            for (int i = 0; i < runtimeMaterialCount; i++)
            {
                Material runtimeMaterial = _faunaPresentationRuntimeMaterials[i];
                if (runtimeMaterial == null)
                    continue;

                ushort propertyMask = _faunaPresentationRuntimeMaterialMasks[i];
                if ((propertyMask & FaunaPresentationCorpseBloatMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatAgeShaderId, 0f);
                if ((propertyMask & FaunaPresentationCorpseBloatTimerMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatStartTimeShaderId, startTimeSeconds);
                if ((propertyMask & FaunaPresentationCorpseBloatDurationMask) != 0)
                    runtimeMaterial.SetFloat(_CorpseBloatDurationShaderId, ResolveCorpsePresentationDurationSeconds());
                if ((propertyMask & FaunaPresentationDecayAmountMask) != 0)
                    runtimeMaterial.SetFloat(_DecayAmountShaderId, 0f);
            }

            _lastAppliedCorpseBloatShader01 = 0f;
            _lastAppliedDecayAmountShader01 = 0f;
        }

        private void ApplyBiolumPresentationLightScale(float scale01)
        {
            float resolvedScale01 = math.saturate(scale01);
            _pendingBiolumPresentationLightScale01 = resolvedScale01;
            _pendingBiolumPresentationLightScaleDirty = true;
        }

        private void FlushBiolumPresentationLightScale()
        {
            if (!_pendingBiolumPresentationLightScaleDirty)
                return;

            _pendingBiolumPresentationLightScaleDirty = false;
            float resolvedScale01 = _pendingBiolumPresentationLightScale01;
            if (_biolumPresentationLightCount <= 0)
                return;

            if (math.abs(_lastAppliedBiolumLightScale01 - resolvedScale01) < 0.001f)
                return;

            _lastAppliedBiolumLightScale01 = resolvedScale01;
            for (int i = 0; i < _biolumPresentationLightCount; i++)
            {
                Light targetLight = _biolumPresentationLights[i];
                if (targetLight == null)
                    continue;

                targetLight.intensity = _biolumPresentationBaseIntensities[i] * resolvedScale01;
            }
        }

        private void UpdateCorpseLatchState(Vector3 corpsePosition, uint corpseNodeId, float consumeDistance)
        {
            if (corpseNodeId == 0u)
            {
                ClearCorpseLatchState();
                return;
            }

            if (_latchedCorpseNodeId != corpseNodeId)
            {
                _latchedCorpseNodeId = corpseNodeId;
                _corpseLatchOffset = ResolveCorpseSurfaceOffset(corpseNodeId, consumeDistance);
                _corpseTearingPhase = 0f;
            }

            _corpseLatchCenterPosition = corpsePosition;
            _corpseLatchTargetPosition = corpsePosition + _corpseLatchOffset;
            _corpseLatchActive = true;
        }

        private Vector3 ResolveCorpseSurfaceOffset(uint corpseNodeId, float consumeDistance)
        {
            uint state = corpseNodeId ^ (_uniqueInstanceUid * 747796405u) ^ 0x9E3779B9u;
            float x = NextSigned01(ref state);
            float y = NextSigned01(ref state);
            float z = NextSigned01(ref state);
            int faceAxis = (int)(Next01(ref state) * 3f);
            float faceSign = Next01(ref state) < 0.5f ? -1f : 1f;
            float halfX = math.max(0.35f, consumeDistance * CarrionLatchConsumeDistanceScale);
            float halfY = math.max(0.25f, consumeDistance * 0.58f);
            float halfZ = math.max(0.35f, consumeDistance * 0.84f);
            if (faceAxis == 0)
                x = faceSign;
            else if (faceAxis == 1)
                y = faceSign;
            else
                z = faceSign;

            return new Vector3(x * halfX, y * halfY, z * halfZ);
        }

        private static float Next01(ref uint state)
        {
            if (state == 0u)
                state = 0xA511E9B3u;

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * Random24BitInvScale;
        }

        private static float NextSigned01(ref uint state)
        {
            return Next01(ref state) * 2f - 1f;
        }

        private static float CheapTriangleWaveSigned(float phase)
        {
            float cycle = math.frac(phase * 0.15915494f);
            return 1f - math.abs(cycle * 4f - 2f);
        }

        private static float TrianglePulse01(float time, float frequency, float phase01)
        {
            return math.abs(math.frac((time * frequency) + phase01) * 2f - 1f);
        }

        private static float CheapSinSigned(float phase)
        {
            return -CheapTriangleWaveSigned(phase - 1.5707964f);
        }

        private static float CheapCosSigned(float phase)
        {
            return -CheapTriangleWaveSigned(phase);
        }

        private void UpdateCarrionTearingAnimation(float dt)
        {
            _corpseTearingPhase += math.max(0f, dt) * CarrionLatchTearingFrequency;
            _corpseTearingPitchRadians = CheapTriangleWaveSigned(_corpseTearingPhase) * DegreesToRadians * CarrionLatchTearingPitchDegrees;
        }

        private void ApplyCorpseLatchFixedStep()
        {
            if (_rb == null)
                return;

            _rb.MovePosition(_corpseLatchTargetPosition);
            TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
            TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
        }

        private void ClearCorpseLatchState()
        {
            _latchedCorpseNodeId = 0u;
            _corpseLatchActive = false;
            _corpseLatchOffset = Vector3.zero;
            _corpseLatchTargetPosition = Vector3.zero;
            _corpseLatchCenterPosition = Vector3.zero;
            _corpseTearingPitchRadians = 0f;
        }

        public void SlowTick()
        {
            RefreshCachedPlayerTransformReference();
            RefreshPredatorPhotophobiaCache();
            RefreshRuntimeEcosystemState();
            TryAdvanceEggClutchPersistence();
        }

        private void RefreshCachedPlayerTransformReference()
        {
            if (_currentCullingPlayerTransform != null)
                return;

            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.PlayerTransform != null)
            {
                _currentCullingPlayerTransform = runtimeContext.PlayerTransform;
                return;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
            {
                _currentCullingPlayerTransform = playerTransform;
                return;
            }

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _currentCullingPlayerTransform);
        }

        private void AdvanceSlowTickCadence(float dt)
        {
            if (dt <= 0f)
                return;

            _slowTickAccumulator += dt;
            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer == null || !bucketer.IsInitialized || bucketer.SlowBucketMask != _simulationBucketSlowMask)
            {
                RefreshSimulationBucketerBinding();
                bucketer = _simulationBucketer;
            }

            if (bucketer != null && bucketer.IsInitialized)
            {
                _simulationBucketInterpolationAlpha = bucketer.ResolveSlowBucketInterpolationAlpha(_simulationBucketId);
                if (_slowTickAccumulator < SlowTickIntervalSeconds)
                    return;

                if (!bucketer.IsSlowBucketActive(_simulationBucketId))
                {
                    if (_slowTickAccumulator > SlowTickIntervalSeconds)
                        _slowTickAccumulator = SlowTickIntervalSeconds;
                    return;
                }

                _slowTickAccumulator = 0f;
                SlowTick();
                return;
            }

            int iterationCount = 0;
            int whileWatchdog = 0;
            while (_slowTickAccumulator >= SlowTickIntervalSeconds &&
                   iterationCount < MaxSlowTicksPerDispatcherTick)
            {
                if (whileWatchdog++ > 10000)
                    break;

                _slowTickAccumulator -= SlowTickIntervalSeconds;
                SlowTick();
                iterationCount++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int frame = Time.frameCount;
            if (_slowTickAccumulator >= SlowTickIntervalSeconds &&
                iterationCount >= MaxSlowTicksPerDispatcherTick &&
                frame >= _nextSlowTickWatchdogLogFrame)
            {
                _nextSlowTickWatchdogLogFrame = frame + 300;
                Debug.LogError("FaunaBrain slow-tick watchdog tripped. Cadence backlog was clamped.", this);
            }
#endif

            if (_slowTickAccumulator > SlowTickIntervalSeconds)
                _slowTickAccumulator = SlowTickIntervalSeconds;
        }

        private void ResetDispatcherCadence()
        {
            _slowTickAccumulator = 0f;
            _simulationBucketInterpolationAlpha = 0f;
        }

        private void RefreshSimulationBucketerBinding()
        {
            ISimulationBucketer bucketer = _simulationBucketerRuntime;
            if (bucketer == null || !bucketer.IsInitialized)
            {
                _simulationBucketer = bucketer;
                return;
            }

            uint stableHash = ResolveStableFaunaHash(FaunaTickStaggerHashSalt, 0u);
            int entityIndex = bucketer.ResolveEntityIndex(stableHash);
            int bucketId = bucketer.ResolveSlowBucket(stableHash);
            _simulationBucketer = bucketer;
            _simulationBucketEntityIndex = entityIndex;
            _simulationBucketId = bucketId;
            _simulationBucketSlowMask = bucketer.SlowBucketMask;
            _tickStaggerShift = bucketId;
            bucketer.TryRegisterEntityBucket(entityIndex, stableHash);
        }

        private void ClearSimulationBucketerBinding()
        {
            if (_simulationBucketer != null && _simulationBucketEntityIndex >= 0)
                _simulationBucketer.TryUnregisterEntityBucket(_simulationBucketEntityIndex);

            _simulationBucketEntityIndex = -1;
            _simulationBucketer = null;
            _simulationBucketId = _tickStaggerShift;
            _simulationBucketSlowMask = -1;
            _simulationBucketInterpolationAlpha = 0f;
        }

        private void UpdateSimulationBucketInterpolationAlpha()
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer == null || !bucketer.IsInitialized)
                return;

            _simulationBucketInterpolationAlpha = bucketer.ResolveSlowBucketInterpolationAlpha(_simulationBucketId);
        }

        private void RegisterSpatialHandle()
        {
            if (!isActiveAndEnabled)
                return;

            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterBioform(this);

            if (_faunaSpatialHandle == 0)
                _faunaSpatialHandle = FaunaSpatialHashRegistry.RegisterBioform(this);
        }

        private void UnregisterSpatialHandle()
        {
            ClearEcholocationMimicSignal();

            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Unregister(_spatialHandle);
                _spatialHandle = 0;
            }

            if (_faunaSpatialHandle != 0)
            {
                FaunaSpatialHashRegistry.Unregister(_faunaSpatialHandle);
                _faunaSpatialHandle = 0;
            }
        }

        private void EnsureLeviathanPresentationOwner()
        {
            if (!ShouldUseProceduralLeviathanPresentation())
                return;

            if (_faunaKinematicsRuntime == null)
                TryGetComponent(out _faunaKinematicsRuntime);

            if (_faunaKinematicsRuntime == null)
                _faunaKinematicsRuntime = gameObject.AddComponent<FaunaKinematicsRuntime>();

            _faunaKinematicsRuntime.BindFromFauna(this, _rb);

            if (!TryGetComponent(out Hecton8.AI.CreatureDamageManager creatureDamageManager))
                creatureDamageManager = gameObject.AddComponent<Hecton8.AI.CreatureDamageManager>();

            creatureDamageManager.BindFromFauna(this);
        }

        private void UpdateProceduralStrikeIntent(AIState resolvedState, Transform strikeTarget)
        {
            if (_faunaKinematicsRuntime == null)
                return;

            bool strikeActive = resolvedState == AIState.Aggressive && strikeTarget != null && !_isDead;
            Vector3 strikeTargetPosition = default;
            if (strikeActive && !TryResolveAttackTargetLogicPosition(strikeTarget, out strikeTargetPosition))
                strikeActive = false;

            _faunaKinematicsRuntime.SetStrikeIntent(strikeTarget, strikeTargetPosition, strikeActive);
            float telegraphBlend = _attackTelegraphActive
                ? 1f - math.saturate((_attackTelegraphBurstTime - _cognitionTimeSeconds) * LeviathanAttackTelegraphInvLeadSeconds)
                : 0f;
            _faunaKinematicsRuntime.SetAttackTelegraph(telegraphBlend);
            PublishProceduralStrikeSignal(strikeActive);
        }

        private void ClearProceduralStrikeIntent()
        {
            if (_faunaKinematicsRuntime == null)
                return;

            _faunaKinematicsRuntime.SetStrikeIntent(null, default, false);
            _faunaKinematicsRuntime.SetAttackTelegraph(0f);
            _faunaKinematicsRuntime.SetHeadLookTarget(default, false);
            PublishProceduralStrikeSignal(false);
        }

        private void PublishProceduralStrikeSignal(bool strikeActive)
        {
            FaunaStateChangedSignal signal = default;
            if (!TryResolveSelfLogicAup(out signal.PositionAup))
                return;

            signal.SpeciesHash = unchecked((uint)ComputeStableSpeciesId());
            signal.StateFlags = strikeActive ? FaunaStateChangedSignalFlags.StateActive : 0u;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Slot = _simulationBucketId > ushort.MaxValue ? ushort.MaxValue : (ushort)math.max(0, _simulationBucketId);
            signal.StateKind = FaunaStateChangedSignalKinds.Strike;
            signal.Flags = strikeActive ? FaunaStateChangedSignalFlags.StateActive : (byte)0;
            SignalBus<FaunaStateChangedSignal>.TryPush(in signal);
        }

        private void UpdateProceduralHeadLookIntent()
        {
            if (_faunaKinematicsRuntime == null)
                return;

            bool hasPlayerTarget = _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition) && !_isDead;
            if (!hasPlayerTarget &&
                _acousticHeadLookWeight > 0.01f &&
                _cognitionTimeSeconds <= _acousticHeadLookUntilTime &&
                !_isDead)
            {
                _faunaKinematicsRuntime.SetHeadLookTarget(_acousticHeadLookTarget, true);
                return;
            }

            _faunaKinematicsRuntime.SetHeadLookTarget(playerPosition, hasPlayerTarget);
        }

        private void EmitLeviathanThreatPulse(in CreatureUtilityEvaluation evaluation)
        {
            if (!CreatureUtilityEvaluation.EmitThreatPulse(in evaluation) ||
                _isDead ||
                !ShouldUseProceduralLeviathanPresentation())
            {
                return;
            }

            if (!TryResolveSelfLogicPosition(out Vector3 pulsePosition))
                return;

            Vector3 pulseDirection = _rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(_rb.linearVelocity)
                : ResolveSelfLogicForward();
            if (pulseDirection.sqrMagnitude <= 0.0001f)
                pulseDirection = Vector3.forward;

            if (evaluation.LegacyState == AIState.Feint && ShouldUseAlphaLeviathanCognition())
            {
                PublishAlphaLeviathanRoarSignal(pulsePosition);
                EmitLeviathanAttackTelegraphPing(pulsePosition);
            }

            PublishLeviathanScatterPulse(pulsePosition, pulseDirection, 40f, 0.4f);
        }

        private void PublishAlphaLeviathanRoarSignal(Vector3 sourcePosition)
        {
            AcousticPingSignal roarSignal = default;
            if (!TryResolveAupFromRuntimeOrigin(sourcePosition, out roarSignal.PositionAup))
                return;

            roarSignal.RadiusMeters = AcousticPingLeviathanScatterRadiusMeters * 1.6f;
            roarSignal.Intensity01 = 1f;
            roarSignal.SourceId = _uniqueInstanceUid != 0u
                ? _uniqueInstanceUid
                : unchecked((uint)ComputeStableSpeciesId());
            roarSignal.Channel = AcousticPingSignal.ChannelLeviathanRoar;
            roarSignal.Flags = AcousticPingSignal.FlagLeviathanRoar;
            SignalBus<AcousticPingSignal>.TryPush(in roarSignal);
            PublishAlphaLeviathanStressSpike();
        }

        private static void PublishAlphaLeviathanStressSpike()
        {
            PlayerStressSignal stressSignal = new PlayerStressSignal
            {
                Stress01 = AlphaLeviathanFalseChargeStress01,
                OxygenDrainScale = AlphaLeviathanFalseChargeOxygenDrainScale,
                AggressionScale = AlphaLeviathanFalseChargeAggressionScale,
                Frame = unchecked((uint)Time.frameCount),
                Cause = PlayerStressCauseApexPredator,
                Flags = PlayerStressFlagApexPredator | PlayerStressFlagAcoustic
            };
            SignalBus<PlayerStressSignal>.TryPush(in stressSignal);
        }

        private void PublishLeviathanScatterPulse(Vector3 position, Vector3 direction, float radiusMeters, float durationSeconds)
        {
            if (!ShouldUseProceduralLeviathanPresentation())
                return;

            IMicroFaunaPresentationPulseSink boidSystem = _sargassumMicroFauna;
            if (boidSystem == null)
                return;

            Vector3 resolvedDirection = direction.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(direction) : ResolveSelfLogicForward();
            if (resolvedDirection.sqrMagnitude <= 0.0001f)
                resolvedDirection = Vector3.forward;

            boidSystem.RegisterLeviathanThreatPulse(
                position,
                resolvedDirection,
                radiusMeters,
                durationSeconds);
        }

        private void TryPublishLeviathanSectorEntryScatter(float3 selfPosition)
        {
            if (!ShouldUseProceduralLeviathanPresentation())
                return;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return;

            double3 absolutePosition = selfAup.ToAbsoluteDouble3();
            int2 sector = new int2(
                (int)math.floor(absolutePosition.x * LeviathanSectorScatterInvEdgeMeters),
                (int)math.floor(absolutePosition.z * LeviathanSectorScatterInvEdgeMeters));

            if (_hasLeviathanScatterSector &&
                _leviathanScatterSector.x == sector.x &&
                _leviathanScatterSector.y == sector.y)
            {
                return;
            }

            _leviathanScatterSector = sector;
            _hasLeviathanScatterSector = true;
            Vector3 direction = _rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f
                ? _rb.linearVelocity
                : ResolveSelfLogicForward();
            PublishLeviathanScatterPulse(
                ToVector3(selfPosition),
                direction,
                AcousticPingLeviathanScatterRadiusMeters,
                AcousticPingLeviathanScatterDurationSeconds);
        }

        private bool ShouldUseProceduralLeviathanPresentation()
        {
            if (_speciesProfile != null && _speciesProfile.isLeviathan)
                return true;

            return _archetype != null && _archetype.roleType == CreatureRoleType.Leviathan;
        }

        private void ApplyAmbientCurrentDrift(float fdt)
        {
            if (_rb == null || fdt <= 0f)
                return;

            if (_foveatedSimulationTier == FoveatedSimulationTier.Frozen)
            {
                if (_rb.linearVelocity.sqrMagnitude <= 0.04f && !_rb.IsSleeping())
                    _rb.Sleep();

                return;
            }

            IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
            if (ambientCurrent == null || !ambientCurrent.TrySampleCombinedCurrent(_rb.worldCenterOfMass, out Vector3 sampledCurrent))
                return;

            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            Vector3 velocityChange = Vector3.ClampMagnitude(sampledCurrent, AmbientCurrentMaxVelocity) * (AmbientCurrentInfluence * fdt);
            TryQueuePhysicsForce(_rb, velocityChange, ForceMode.VelocityChange);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════
        public void ReceivePlayerNoiseSignal(NoiseSystem.PlayerNoiseSignal signal)
        {
            if (_isDead || !isActiveAndEnabled)
                return;

            _sensorSuite.ReceivePlayerNoiseSignal(signal);
            _utilityBrain.RecordAuditoryStimulus(signal.Position, _cognitionTimeSeconds);
        }

        internal void ApplyAcousticPingAggro(Vector3 sourcePosition, float intensity01, float durationSeconds)
        {
            if (_isDead || !IsApexPredator())
                return;

            if (IsPredatorDeafenedActive())
                return;

            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return;

            _predatorSquadStateBits |= PredatorSquadStateHuntingBit;
            _predatorSquadStateBits &= ~(PredatorSquadStateFleeingBit | PredatorSquadStateFlankingBit);
            ApplyPredatorSensoryBits(
                (byte)(PredatorSensoryHearsSonarBit | PredatorSensoryAggroBit),
                PredatorSensoryCanSeePlayerBit);
            _utilityBrain.RecordAuditoryStimulus(sourcePosition, _cognitionTimeSeconds);
            if (!IsPredatorStunnedActive())
            {
                _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
                ApplyDirectedStateOverride((float3)selfPosition, sourcePosition, AIState.Aggressive);
            }

            ForceDirectorHuntTarget(sourcePosition, math.max(1f, durationSeconds));
            if (!TryResolveAupFromRuntimeOrigin(sourcePosition, out AbsoluteUniversePosition sourceAup) ||
                !TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
            {
                return;
            }

            double3 directionAbsolute = AbsoluteUniversePosition.DeltaMetersClamped(in sourceAup, in selfAup);
            Vector3 direction = new Vector3(
                (float)directionAbsolute.x,
                (float)directionAbsolute.y,
                (float)directionAbsolute.z);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = ResolveSelfLogicForward();
            PublishLeviathanScatterPulse(selfPosition, direction, AcousticPingLeviathanScatterRadiusMeters, AcousticPingLeviathanScatterDurationSeconds);
        }

        internal bool ShouldIgnoreAcousticPing(float energyJoules, float intensity01)
        {
            if (_isDead || !IsApexPredator())
                return true;

            if (IsPredatorStunnedActive() || IsPredatorDeafenedActive())
                return true;

            if (IsTier3LeviathanRuntime() &&
                energyJoules < Tier3LeviathanMinimumPingEnergyJoules)
            {
                return true;
            }

            return intensity01 <= 0.001f && energyJoules <= 0f;
        }

        internal void ApplyPredatorDeafening(Vector3 sourcePosition, float durationSeconds)
        {
            if (_isDead || !IsApexPredator() || durationSeconds <= 0f)
                return;

            _predatorSquadStateBits &= ~(PredatorSquadStateHuntingBit | PredatorSquadStateFlankingBit);
            ApplyPredatorSensoryBits(
                PredatorSensoryDeafenedBit,
                (byte)(PredatorSensoryCanSeePlayerBit | PredatorSensoryHearsSonarBit | PredatorSensoryAggroBit));
            _predatorDeafenedUntilTime = math.max(_predatorDeafenedUntilTime, _cognitionTimeSeconds + durationSeconds);
            AssignPredatorDeafenedWanderAup(sourcePosition);
            ClearAttackTelegraphState();
            ClearPredatorLungeCheat();
            ClearDirectorHuntTarget();
            _utilityBrain.ApplyExternalState(AIState.Wander, _cognitionTimeSeconds);
            _currentStateCache = AIState.Wander;
        }

        internal void ApplyDirectorLineOfSight(bool hasLineOfSight, Vector3 playerPosition, Vector3 playerForward)
        {
            ApplyDirectorLineOfSight(hasLineOfSight, playerPosition, playerForward, Vector3.zero);
        }

        internal void ApplyDirectorLineOfSight(bool hasLineOfSight, Vector3 playerPosition, Vector3 playerForward, Vector3 playerVelocity)
        {
            if (_isDead || (_utilityBrain.IsActivePredator == 0 && !IsApexPredator()))
                return;

            if (IsPredatorDeafenedActive())
                return;

            ApplyPredatorSensoryBits(
                hasLineOfSight ? PredatorSensoryCanSeePlayerBit : (byte)0,
                hasLineOfSight ? (byte)0 : PredatorSensoryCanSeePlayerBit);

            if (hasLineOfSight)
            {
                _predatorSquadStateBits |= PredatorSquadStateHuntingBit;
                _predatorSquadStateBits &= ~PredatorSquadStateFlankingBit;
                ApplyPredatorSensoryBits(PredatorSensoryAggroBit, 0);
                ForceDirectorHuntTarget(playerPosition, DirectorHuntTargetDurationSeconds, playerVelocity);
                return;
            }

            if (IsPredatorStunnedActive())
                return;

            TryApplyDirectorAmbushReposition(playerPosition, playerForward);
        }

        internal bool ApplyDirectorColdTickCull(bool enableColdTick)
        {
            if (_isDead)
                return false;

            if (enableColdTick)
            {
                float coldTickUntilTime = _cognitionTimeSeconds + DirectorColdTickHoldSeconds;
                if (_directorColdTickUntilTime >= coldTickUntilTime)
                    return false;

                _directorColdTickUntilTime = math.max(_directorColdTickUntilTime, coldTickUntilTime);
                return true;
            }

            bool wasColdTickActive = _directorColdTickUntilTime > 0f || _directorColdTickAccumulator > 0f;
            _directorColdTickUntilTime = 0f;
            _directorColdTickAccumulator = 0f;
            return wasColdTickActive;
        }

        internal void ForceDirectorHuntTarget(Vector3 targetPosition, float durationSeconds)
        {
            ForceDirectorHuntTarget(targetPosition, durationSeconds, Vector3.zero);
        }

        internal void ForceDirectorHuntTarget(Vector3 targetPosition, float durationSeconds, Vector3 targetVelocity)
        {
            if (_isDead || (_utilityBrain.IsActivePredator == 0 && !IsApexPredator()))
                return;

            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return;

            if (!TryResolveAupFromRuntimeOrigin(targetPosition, out AbsoluteUniversePosition targetAup))
                return;

            _directorHuntTargetPosition = targetPosition;
            _directorHuntTargetAup = targetAup;
            _directorHuntTargetVelocity = targetVelocity;
            float velocitySq = math.lengthsq((float3)targetVelocity);
            _directorHuntPredictionLeadSeconds = ResolvePredatorDeadReckoningLeadSeconds(velocitySq);
            _directorHuntPredictionSampleTime = _cognitionTimeSeconds;
            _hasDirectorHuntPrediction = velocitySq > 0.0001f;
            _directorHuntPredictedAup = _hasDirectorHuntPrediction
                ? PredictTargetAup(in _directorHuntTargetAup, targetVelocity, _directorHuntPredictionLeadSeconds)
                : _directorHuntTargetAup;
            _directorHuntUntilTime = _cognitionTimeSeconds + math.max(0.5f, durationSeconds);
            _hasDirectorHuntTarget = true;
            ApplyPredatorSensoryBits(PredatorSensoryAggroBit, 0);
            _utilityBrain.RecordAuditoryStimulus(targetPosition, _cognitionTimeSeconds);
            if (!IsPredatorStunnedActive())
            {
                _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
                ApplyDirectedStateOverride((float3)selfPosition, targetPosition, AIState.Aggressive);
            }

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null &&
                ResolveRuntimeAupDistanceSq(selfPosition, targetPosition) <= DirectorVoxelRouteMaxDistanceMetersSqr &&
                vegetationBridge.TryBuildImmediateAbyssalVoxelRoute(selfPosition, targetPosition, _voxelRouteWaypoints, out int waypointCount))
            {
                _voxelRouteWaypointCount = waypointCount;
                _voxelRouteTargetPosition = targetPosition;
                _hasVoxelRouteTarget = waypointCount >= 2;
                CacheVoxelRouteAupState(waypointCount, targetPosition);
                _nextVoxelRouteRefreshTime = _cognitionTimeSeconds + VoxelRouteRefreshIntervalSeconds;
            }
        }

        private static float ResolvePredatorDeadReckoningLeadSeconds(float velocitySq)
        {
            float leadSeconds = math.select(0f, PredatorPredictionSlowLeadSeconds, velocitySq > 0.0001f);
            leadSeconds = math.select(leadSeconds, PredatorPredictionMediumLeadSeconds, velocitySq >= PredatorPredictionMediumVelocitySqr);
            leadSeconds = math.select(leadSeconds, PredatorPredictionFastLeadSeconds, velocitySq >= PredatorPredictionFastVelocitySqr);
            return leadSeconds;
        }

        internal void ApplyHunterSquadDirective(Vector3 targetPosition, uint squadStateBits, int squadOrdinal, float durationSeconds)
        {
            if (_isDead || (_utilityBrain.IsActivePredator == 0 && !IsApexPredator()))
                return;

            uint stunBits = _predatorSquadStateBits & PredatorStateStunnedBit;
            uint sharedBits = squadStateBits & (PredatorSquadStateHuntingBit | PredatorSquadStateFleeingBit | PredatorSquadStateFlankingBit);
            if (squadOrdinal > 0)
                sharedBits |= PredatorSquadStateFlankingBit;
            _predatorSquadOrdinal = math.clamp(squadOrdinal, 0, 2);
            uint alphaOrdinal = (squadStateBits & PredatorSquadAlphaMask) != 0u
                ? (squadStateBits & PredatorSquadAlphaMask) >> PredatorSquadAlphaShift
                : 0u;
            _predatorSquadStateBits = stunBits | sharedBits | ((alphaOrdinal & 0x3u) << PredatorSquadAlphaShift);

            if ((_predatorSquadStateBits & PredatorSquadStateFleeingBit) == 0u)
                ForceDirectorHuntTarget(targetPosition, durationSeconds);
        }

        private bool TryApplyDirectorAmbushReposition(Vector3 playerPosition, Vector3 playerForward)
        {
            if (_cognitionTimeSeconds < _nextDirectorAmbushTime)
                return false;

            Vector3 safeForward = playerForward.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(playerForward) : Vector3.forward;
            Vector3 right = ResolvePlanarRightFromDominantForward(safeForward);

            float side = ((_uniqueInstanceUid ^ unchecked((uint)ComputeStableSpeciesId())) & 1u) == 0u ? -1f : 1f;
            Vector3 candidate = playerPosition -
                                safeForward * DirectorAmbushBehindDistanceMeters +
                                right * (DirectorAmbushLateralDistanceMeters * side);
            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return false;

            float3 selfRuntime = selfAup.ToRuntimeFloat3();
            candidate.y = selfRuntime.y;

            if (VoxelDynamicNavGridRuntime.TrySampleHybridNavigation((float3)candidate, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.SolidVoxel)
            {
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(candidate, out AbsoluteUniversePosition candidateAup))
                return false;

            ApplyAupPresentationPosition(in candidateAup);
            ForceDirectorHuntTarget(playerPosition, DirectorHuntTargetDurationSeconds);
            _predatorSquadStateBits |= PredatorSquadStateFlankingBit;
            _predatorSquadStateBits &= ~PredatorSquadStateFleeingBit;
            _nextDirectorAmbushTime = _cognitionTimeSeconds + DirectorAmbushCooldownSeconds;
            return true;
        }

        private static double ResolveRuntimeAupDistanceSq(Vector3 a, Vector3 b)
        {
            if (!TryResolveAupFromRuntimeOrigin(a, out AbsoluteUniversePosition aupA) ||
                !TryResolveAupFromRuntimeOrigin(b, out AbsoluteUniversePosition aupB))
            {
                return double.MaxValue;
            }

            return AbsoluteUniversePosition.DistanceSq(in aupA, in aupB);
        }

        public bool TryResolveLogicAup(out AbsoluteUniversePosition selfAup)
        {
            return TryResolveSelfLogicAup(out selfAup);
        }

        internal bool TryResolveLogicPosition(out Vector3 selfPosition)
        {
            return TryResolveSelfLogicPosition(out selfPosition);
        }

        private bool TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup)
        {
            selfAup = default;
            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return false;

            return TryResolveAupFromRuntimeOrigin(selfPosition, out selfAup);
        }

        private bool TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)
        {
            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                playerAup = runtimeContext.MovementState.PredictedAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private bool TryResolveSelfLogicPosition(out Vector3 selfPosition)
        {
            if (_rb == null)
            {
                selfPosition = default;
                return false;
            }

            selfPosition = _rb.position;
            return true;
        }

        private Vector3 ResolveSelfRuntimePositionOrZero()
        {
            return TryResolveSelfLogicPosition(out Vector3 selfPosition)
                ? selfPosition
                : Vector3.zero;
        }

        private static bool TryResolveFaunaPredationTarget(Transform target, out IFaunaPredationTarget predationTarget)
        {
            predationTarget = null;
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out predationTarget) && predationTarget != null)
                    return true;
            }

            return false;
        }

        private bool TryResolveAttackTargetLogicPosition(Transform target, out Vector3 targetPosition)
        {
            targetPosition = default;
            if (target == null)
                return false;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            bool targetIsPlayer = target == playerTransform || target.CompareTag("Player");
            if (targetIsPlayer)
            {
                if (_sensorSuite.TryGetPerceivedPlayerPosition(out targetPosition))
                    return true;

                if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                    runtimeContext != null &&
                    (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    targetPosition = ToVector3(runtimeContext.MovementState.PredictedAup.ToRuntimeFloat3());
                    return true;
                }

                if (TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup))
                {
                    targetPosition = ToVector3(playerAup.ToRuntimeFloat3());
                    return true;
                }
            }

            if (_sensorSuite.hasCurrentPrey && target.CompareTag("Prey"))
            {
                targetPosition = _sensorSuite.currentPreyPosition;
                return true;
            }

            if (TryResolveFaunaPredationTarget(target, out IFaunaPredationTarget faunaTarget) &&
                faunaTarget.TryResolveLogicAup(out AbsoluteUniversePosition faunaTargetAup))
            {
                targetPosition = ToVector3(faunaTargetAup.ToRuntimeFloat3());
                return true;
            }

            if (target.TryGetComponent(out Rigidbody targetBody))
            {
                targetPosition = targetBody.position;
                return true;
            }

            return false;
        }

        private bool TryResolveAttackTargetLogicAup(Transform target, Vector3 fallbackPosition, out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (target == null)
                return TryResolveAupFromRuntimeOrigin(fallbackPosition, out targetAup);

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (target == playerTransform || target.CompareTag("Player"))
            {
                return TryResolvePlayerPredictedAup(out targetAup) ||
                       TryResolveAupFromRuntimeOrigin(fallbackPosition, out targetAup);
            }

            if (TryResolveFaunaPredationTarget(target, out IFaunaPredationTarget faunaTarget) &&
                faunaTarget.TryResolveLogicAup(out targetAup))
            {
                return true;
            }

            if (target.TryGetComponent(out Rigidbody targetBody))
                return TryResolveAupFromRuntimeOrigin(targetBody.position, out targetAup);

            return TryResolveAupFromRuntimeOrigin(fallbackPosition, out targetAup);
        }

        private Vector3 ResolveSelfLogicForward()
        {
            if (_rb != null)
            {
                Vector3 forward = _rb.rotation * Vector3.forward;
                if (forward.sqrMagnitude > 0.0001f)
                    return forward;
            }

            if (_cachedDesiredDirection.sqrMagnitude > 0.0001f)
                return ResolveDominantAxisDirection(_cachedDesiredDirection);

            return Vector3.forward;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);

            if (absX >= absY && absX >= absZ)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absY >= absZ)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private bool TryResolvePlayerListenerPosition(out Vector3 listenerPosition, out Transform playerRoot)
        {
            listenerPosition = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            playerRoot = playerContext != null ? playerContext.PlayerTransform : null;
            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                if (playerRoot == null)
                    playerRoot = runtimeContext.PlayerTransform;

                PlayerLookState lookState = runtimeContext.LookState;
                if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    listenerPosition = ToVector3(lookState.EyePosition);
                    return true;
                }

                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    listenerPosition = ToVector3(movementState.PredictedAup.ToRuntimeFloat3());
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ResolvePlanarRightFromDominantForward(Vector3 forward)
        {
            float absX = math.abs(forward.x);
            float absZ = math.abs(forward.z);
            if (absX <= 0.0001f && absZ <= 0.0001f)
                return Vector3.right;

            if (absZ >= absX)
                return forward.z < 0f ? Vector3.left : Vector3.right;

            return forward.x < 0f ? Vector3.forward : Vector3.back;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static bool TryGetLatestKccVelocityVector(uint maxFrameAge, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
                signal.Sequence == 0u ||
                !IsKccVelocityFresh(in signal, unchecked((uint)SystemDispatcher.CurrentFrameIndex), maxFrameAge) ||
                !math.all(math.isfinite(signal.Velocity)))
            {
                return false;
            }

            velocity = ToVector3(signal.Velocity);
            return true;
        }

        private static bool IsKccVelocityFresh(in KccVelocitySignal signal, uint currentFrame, uint maxFrameAge)
        {
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            return currentFrame == 0u ||
                   signalFrame == 0u ||
                   (signalFrame <= currentFrame && currentFrame - signalFrame <= maxFrameAge);
        }

        private static Vector3 NormalizeVectorOrFallback(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
            {
                float fallbackSq = fallback.sqrMagnitude;
                return math.isfinite(fallbackSq) && fallbackSq > 0.000001f
                    ? fallback * math.rsqrt(fallbackSq)
                    : Vector3.forward;
            }

            return value * math.rsqrt(lengthSq);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(aup.ToAbsoluteDouble3()));
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector(bounds.center) &&
                   IsFiniteVector(bounds.extents) &&
                   bounds.extents.x >= 0f &&
                   bounds.extents.y >= 0f &&
                   bounds.extents.z >= 0f;
        }

        private static string ResolveScanRoleCategory(CreatureRoleType roleType)
        {
            switch (roleType)
            {
                case CreatureRoleType.Ambient:
                    return "Ambient";
                case CreatureRoleType.Territorial:
                    return "Territorial";
                case CreatureRoleType.Hunter:
                    return "Hunter";
                case CreatureRoleType.Leviathan:
                    return "Leviathan";
                case CreatureRoleType.DroneTrader:
                    return "DroneTrader";
                default:
                    return "Fauna";
            }
        }

        private void RaisePredationAudioPing(Vector3 killPosition, Transform preyRoot)
        {
            if (_utilityBrain.IsActivePredator == 0)
                return;

            if (!TryResolvePlayerListenerPosition(out Vector3 listenerPosition, out Transform playerRoot))
                return;

            double distanceSqr = ResolveRuntimeAupDistanceSq(listenerPosition, killPosition);
            if (distanceSqr > PredatorKillAudioRadiusMetersSqr)
                return;

            float distance01 = math.saturate((float)distanceSqr * PredatorKillAudioInvRadiusMetersSqr);
            float intensity = 1f - distance01;
            if (intensity <= 0.001f)
                return;

            int sensoryMask = AcousticOcclusionUtility.BuildSensoryMask();
            float acousticTransmission01 = 1f;
            float lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            Transform originRoot = preyRoot != null ? preyRoot : transform;
            if (AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                    killPosition,
                    listenerPosition,
                    sensoryMask,
                    originRoot,
                    playerRoot,
                    out AcousticOcclusionResult occlusion))
            {
                acousticTransmission01 = math.saturate(occlusion.Transmission01);
                lowPassCutoffHz = math.clamp(
                    occlusion.LowPassCutoffHz,
                    AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                    AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            }
            else
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(
                    killPosition,
                    listenerPosition,
                    sensoryMask,
                    originRoot,
                    playerRoot);
            }

            TryPushProceduralAudioPing(
                killPosition,
                intensity,
                PredatorKillAudioDurationSeconds,
                acousticTransmission01,
                lowPassCutoffHz,
                ProceduralAudioPingKindPredatorKill);
        }

        private void HandleAttackPerform(Transform target)
        {
            if (target == null || _isDead) return;
            
            float damage = ResolveCurrentAttackDamage();
            bool hasTargetLogicPosition = TryResolveAttackTargetLogicPosition(target, out Vector3 targetLogicPosition);

            // 1. PREY INTERACTION (Food Chain)
            if (target.CompareTag("Prey"))
            {
                bool hasPreyTarget = TryResolveFaunaPredationTarget(target, out IFaunaPredationTarget preyTarget);

                Vector3 predatorPosition = TryResolveSelfLogicPosition(out Vector3 resolvedPredatorPosition)
                    ? resolvedPredatorPosition
                    : hasTargetLogicPosition ? targetLogicPosition - ResolveSelfLogicForward() : -ResolveSelfLogicForward();

                // [RULE] Predators entering Sated state after eating
                float satedDur = _speciesProfile != null ? _speciesProfile.satedDuration : 45f;
                _utilityBrain.ForceSated(_cognitionTimeSeconds, satedDur);
                _utilityBrain.SetHunger01(0f);
                _stateMachine.currentState = AIState.Sated;
                _currentStateCache = AIState.Sated;
                
                // [REQ] SHOAL SCATTERING (Panic Pulse)
                // Trigger panic in all nearby prey within 10m
                Vector3 preyPosition = hasTargetLogicPosition ? targetLogicPosition : predatorPosition + ResolveSelfLogicForward();
                AbsoluteUniversePosition preyAup;
                if (!TryResolveAupFromRuntimeOrigin(preyPosition, out preyAup))
                {
                    return;
                }

                int count = FaunaSpatialHashRegistry.CollectAdjacentContactsNonAlloc(in preyAup, 10f, SpatialTargetKind.Bioform, _panicBuffer);
                for (int i = 0; i < count; i++)
                {
                    SpatialQueryHit panicHit = _panicBuffer[i];
                    Transform neighborTransform = panicHit.Transform;
                    if (neighborTransform == null ||
                        neighborTransform == target ||
                        !panicHit.IsPreyTag ||
                        !(panicHit.Owner is IFaunaSpatialContact neighborContact))
                    {
                        continue;
                    }

                    neighborContact.TriggerPanicPulse(predatorPosition);
                }

                Vector3 fearBurstDirection = preyPosition - predatorPosition;
                if (fearBurstDirection.sqrMagnitude <= 0.0001f)
                    fearBurstDirection = ResolveSelfLogicForward();
                else
                    fearBurstDirection = ResolveDominantAxisDirection(fearBurstDirection);

                ChemicalInfluenceGrid.QueueFearPheromone(preyPosition, 1f);

                IMicroFaunaPresentationPulseSink microFaunaBoids = _sargassumMicroFauna;
                if (microFaunaBoids != null)
                {
                    microFaunaBoids.RegisterPredatorFearBurst(
                        preyPosition,
                        fearBurstDirection,
                        10f,
                        0.45f,
                        0.85f);
                    float biteRangeMeters = _speciesProfile != null
                        ? math.max(1f, _speciesProfile.attackRadius)
                        : math.max(1f, _stateMachine.attackRadius);
                    microFaunaBoids.RegisterPredatorConsumptionBurst(
                        predatorPosition,
                        preyPosition,
                        biteRangeMeters,
                        _uniqueInstanceUid,
                        _cognitionTimeSeconds);
                }

                // Despawn/Pool the prey
                if (hasPreyTarget)
                {
                    if (preyTarget.IsBiolumFlashBangPrey)
                        TriggerBiolumFlashBang(preyPosition);

                    bool preyWasAlive = !preyTarget.IsDead;
                    preyTarget.ApplyPredationDamage(damage * 10f, predatorPosition); // Massive damage to ensure kill
                    if (preyWasAlive && preyTarget.IsDead)
                        RaisePredationAudioPing(preyPosition, target);
                }
                else
                {
                    RaisePredationAudioPing(preyPosition, target);
                    // Fallback for non-brain prey (e.g. static/simple pooled objects)
                    QueueExternalDespawnOrDeactivate(target.gameObject);
                }

                IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
                if (ecosystemDirector != null && ecosystemDirector.IsInitialized)
                    ecosystemDirector.ReportPredation(preyPosition, 1);

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.Log("[FAUNA] Feed event. Entering SATED state.", this);
                #endif
                return;
            }

            // 2. PLAYER / VEHICLE INTERACTION
            if (target.CompareTag("Player"))
            {
                TryDispatchEmpAttack(target);

                Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                    ? resolvedSelfPosition
                    : hasTargetLogicPosition ? targetLogicPosition - ResolveSelfLogicForward() : -ResolveSelfLogicForward();
                Vector3 impactPoint = hasTargetLogicPosition ? targetLogicPosition : target.position;
                Vector3 impactDir = ResolveDominantAxisDirection(impactPoint - selfPosition);

                if (!TryQueuePredatorBiteDamage(target, damage, impactPoint, impactDir))
                    ApplyPredatorBiteOwnerFallbackDamage(target, damage, impactPoint);

                // 3. JUICE (User REQ: Camera Shake + Physical Force)
                if (_speciesProfile != null && _speciesProfile.attackShakeProfile != null)
                {
                    float biteShakeSeverity = math.saturate(_speciesProfile.attackShakeProfile.MaxDisplacement * 2.5f);
                    CameraJuiceSignals.TryPublishImpact(biteShakeSeverity, impactPoint, impactDir);
                }

                DispatchPredatorBiteImpulseToPlayer(target, impactPoint, impactDir);

                if (_speciesProfile != null && _speciesProfile.impactForceToPlayer > 0f)
                    ApplyCinematicPlayerImpact(target, impactDir, _speciesProfile.impactForceToPlayer);
            }
            else if (TryResolveFaunaPredationTarget(target, out IFaunaPredationTarget otherTarget))
            {
                Vector3 predatorPosition = TryResolveSelfLogicPosition(out Vector3 resolvedPredatorPosition)
                    ? resolvedPredatorPosition
                    : hasTargetLogicPosition ? targetLogicPosition - ResolveSelfLogicForward() : -ResolveSelfLogicForward();
                otherTarget.ApplyPredationDamage(damage, predatorPosition);
                if (IsApexPredator() && otherTarget.IsApexPredatorContact)
                {
                    if (otherTarget.HealthNormalized <= 0.3f)
                    {
                        otherTarget.ForceApexRetreatFrom(predatorPosition);
                        GainApexIntimidation();
                    }
                    else if (otherTarget.IsDead)
                    {
                        GainApexIntimidation();
                    }
                }
            }
        }

        private static float3 SanitizeFiniteInputFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private bool TryQueuePredatorBiteDamage(Transform target, float damage, Vector3 impactPoint, Vector3 impactDir)
        {
            if (target == null || damage <= 0f)
                return false;

            HectonPlayerHealth playerHealth = target.GetComponentInParent<HectonPlayerHealth>();
            GameObject targetObject = playerHealth != null ? playerHealth.gameObject : target.gameObject;
            Transform targetTransform = playerHealth != null ? playerHealth.transform : target;
            int targetId = CombatDamageRuntime.ResolveTargetId(targetObject);
            if (!CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Vector3 targetPosition = targetTransform.position;
            float3 fallbackImpactPoint = IsFiniteVector(targetPosition)
                ? new float3(targetPosition.x, targetPosition.y, targetPosition.z)
                : float3.zero;
            float3 safeImpactPoint3 = SanitizeFiniteInputFloat3(
                new float3(impactPoint.x, impactPoint.y, impactPoint.z),
                fallbackImpactPoint);
            Vector3 safeImpactPoint = new Vector3(safeImpactPoint3.x, safeImpactPoint3.y, safeImpactPoint3.z);
            Vector3 localPoint = targetTransform.InverseTransformPoint(safeImpactPoint);
            float3 impactDirection = SanitizeFiniteInputFloat3(new float3(impactDir.x, impactDir.y, impactDir.z), new float3(0f, 0f, 1f));
            impactDirection = math.normalizesafe(impactDirection, new float3(0f, 0f, 1f));
            float3 localPoint3 = SanitizeFiniteInputFloat3(new float3(localPoint.x, localPoint.y, localPoint.z), float3.zero);
            double3 impactAup = double3.zero;
            if (TryResolveAupFromRuntimeOrigin(safeImpactPoint, out AbsoluteUniversePosition impactPointAup) &&
                impactPointAup.IsFinite())
            {
                double3 resolvedAup = impactPointAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolvedAup)))
                    impactAup = resolvedAup;
            }

            float impulseMagnitude = ResolvePredatorBiteImpulseMagnitude();
            uint statusBits = IsApexPredator() ? CombatStatusBits.Stunned : 0u;
            Hecton8.Gameplay.CombatDamageRequest signal = new Hecton8.Gameplay.CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = IsApexPredator() ? DamageSourceIds.FaunaLeviathanBite : DamageSourceIds.FaunaBite,
                Amount = damage,
                ImpulseMagnitude = impulseMagnitude,
                Direction = impactDirection,
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    CombatDamageTypes.Impact,
                    statusBits,
                    CombatWeakspotTier.None)
            };

            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = localPoint3,
                ArmorNormal = -impactDirection,
                LocalTemperatureCelsius = 20f,
                StatusDurationSeconds = 0f
            };

            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
            return true;
        }

        private void ApplyPredatorBiteOwnerFallbackDamage(Transform target, float damage, Vector3 impactPoint)
        {
            if (target == null || damage <= 0f || !math.isfinite(damage))
                return;

            HectonPlayerHealth playerHealth = target.GetComponentInParent<HectonPlayerHealth>();
            if (playerHealth == null)
                return;

            Transform targetTransform = playerHealth.transform != null ? playerHealth.transform : target;
            Vector3 targetPosition = targetTransform.position;
            float3 fallbackImpactPoint = IsFiniteVector(targetPosition)
                ? new float3(targetPosition.x, targetPosition.y, targetPosition.z)
                : float3.zero;
            float3 safeImpactPoint3 = SanitizeFiniteInputFloat3(
                new float3(impactPoint.x, impactPoint.y, impactPoint.z),
                fallbackImpactPoint);
            Vector3 safeImpactPoint = new Vector3(safeImpactPoint3.x, safeImpactPoint3.y, safeImpactPoint3.z);
            Vector3 localPoint = targetTransform.InverseTransformPoint(safeImpactPoint);
            float3 localPoint3 = SanitizeFiniteInputFloat3(new float3(localPoint.x, localPoint.y, localPoint.z), float3.zero);

            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = damage,
                LocalPoint = localPoint3,
                DamageType = CombatDamageTypes.Impact,
                IntegrityDelta = 0,
                Depth = 0f,
                SourceId = IsApexPredator() ? DamageSourceIds.FaunaLeviathanBite : DamageSourceIds.FaunaBite,
                TraumaLevel = 0
            };
            playerHealth.ReceiveDamage(in packet);
        }

        private static bool TryPushProceduralAudioPing(
            Vector3 sourcePosition,
            float intensity,
            float durationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            byte kind)
        {
            AudioPingTriggerPayload payload = new AudioPingTriggerPayload(
                0L,
                1,
                intensity,
                durationSeconds,
                sourcePosition,
                acousticTransmission01,
                lowPassCutoffHz,
                kind);
            SignalAudioEvent audioEvent = SignalAudioEvent.FromAudioPing(in payload);
            return SignalBus<SignalAudioEvent>.TryPush(in audioEvent);
        }

        private bool TryQueuePhysicsForce(Rigidbody body, Vector3 force, ForceMode mode)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueForce(body, force, mode);
        }

        private bool TryQueuePhysicsForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueForceAtPosition(body, force, worldPosition, mode);
        }

        private bool TryQueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueLinearVelocitySet(body, linearVelocity, wake);
        }

        private bool TryQueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, bool wake = true)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueAngularVelocitySet(body, angularVelocity, wake);
        }

        private float ResolvePredatorBiteImpulseMagnitude()
        {
            float speedApprox = math.max(0f, _steeringEngine.currentSpeed);
            if (speedApprox <= 0.001f && _rb != null)
                speedApprox = CinematicMath.ApproximateLength((float3)_rb.linearVelocity);

            if (speedApprox <= 0.001f)
                speedApprox = math.max(1f, _steeringEngine.maxSpeed);

            float predatorMass = _rb != null ? math.max(1f, _rb.mass) : 1f;
            return predatorMass * math.max(1f, speedApprox);
        }

        private void DispatchPredatorBiteImpulseToPlayer(Transform target, Vector3 impactPoint, Vector3 impactDir)
        {
            if (target == null)
                return;

            IPlayerMovementForceSink playerForceSink = null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && ReferenceEquals(playerContext.PlayerTransform, target))
                playerForceSink = playerContext.PlayerMovement as IPlayerMovementForceSink;

            if (playerForceSink == null && target.TryGetComponent(out IPlayerMovementForceSink movementSink))
                playerForceSink = movementSink;

            if (playerForceSink == null)
                return;

            Vector3 predatorVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            if (predatorVelocity.sqrMagnitude <= 0.0001f)
                predatorVelocity = impactDir * math.max(1f, _steeringEngine.maxSpeed);

            float predatorMass = _rb != null ? math.max(1f, _rb.mass) : 1f;
            Vector3 impulse = predatorVelocity * predatorMass;
            if (impulse.sqrMagnitude <= 0.0001f)
                return;

            PublishPredatorImpactSignal(impactPoint, impulse);
            playerForceSink.QueueExternalVelocityChange(impulse / PlayerEquivalentMassKg);
        }

        private void PublishPredatorImpactSignal(Vector3 impactPoint, Vector3 impulse)
        {
            float impulseMagnitude = CinematicMath.ApproximateLength(new float3(impulse.x, impulse.y, impulse.z));
            if (impulseMagnitude <= 0.001f)
                return;

            ImpactSignal signal = default;
            if (!TryResolveAupFromRuntimeOrigin(impactPoint, out signal.PointAup))
                return;

            signal.Force = impulseMagnitude;
            signal.Intensity = math.saturate(math.log10(1f + (impulseMagnitude * PredatorImpactSignalImpulseScale)));
            signal.PrimaryBodyId = _uniqueInstanceUid != 0u
                ? _uniqueInstanceUid
                : unchecked((uint)EntityId.ToULong(GetEntityId()));
            signal.WeightClass = IsApexPredator() ? (byte)2 : (byte)1;
            signal.PrimaryMaterialId = 0;
            signal.SecondaryMaterialId = 0;
            signal.Flags = 0;
            SignalBus<ImpactSignal>.TryPush(in signal);
        }

        private void ApplyCinematicPlayerImpact(Transform target, Vector3 impactDir, float force)
        {
            if (force <= 0f)
                return;

            IPlayerMovementTraumaSink movement = null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && ReferenceEquals(playerContext.PlayerTransform, target))
                movement = playerContext.PlayerMovement as IPlayerMovementTraumaSink;

            if (movement == null)
                target.TryGetComponent(out movement);

            if (movement == null)
                return;

            movement.ApplyPhysicalTrauma(impactDir * force, math.saturate(force * PlayerImpactTraumaWeightPerForce));
        }

        public void TakeDamage(float amount)
        {
            TakeDamageInternal(amount, default, false);
        }

        private void TakeDamageFromSource(float amount, Vector3 damageSourcePosition)
        {
            TakeDamageInternal(amount, damageSourcePosition, true);
        }

        private void TakeDamageInternal(float amount, Vector3 damageSourcePosition, bool hasDamageSource)
        {
            if (_isDead)
                return;

            float clampedDamage = math.max(0f, amount);
            if (clampedDamage <= 0f)
                return;

            float normalizedDamage = _maxHealth > 0.001f ? clampedDamage * math.rcp(_maxHealth) : 0f;
            _currentHealth = math.max(0f, _currentHealth - clampedDamage);
            MarkCombatDamageSyncDirty();
            NotifyFoveatedCombatDamageLock();
            TriggerHitFlash(normalizedDamage);

            Vector3 resolvedSourcePosition = hasDamageSource
                ? damageSourcePosition
                : ResolveFallbackDamageSourcePosition();

            if (_currentHealth > 0.001f)
                ApplyImmediateHitReaction(resolvedSourcePosition, normalizedDamage);

            EmitParentalDefenseSignal(resolvedSourcePosition, normalizedDamage);
            if (_utilityBrain.UsesPredatorRole != 0 && normalizedDamage >= 0.3f)
                TryRegisterPredatorFearNode(normalizedDamage);

            if (_currentHealth <= 0.001f)
                Die();
        }

        private void TriggerHitFlash(float normalizedDamage)
        {
            float impactFlash01 = math.saturate(0.28f + (normalizedDamage * 2.5f));
            _hitFlash01 = math.max(_hitFlash01, impactFlash01);
        }

        /// <summary>
        /// Applies cutter damage through the fauna interaction matrix.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            if (_isDead)
                return;

            TakeDamageFromSource(damage, hitPoint);
            ApplyFaunaInteraction(FaunaInteractionKind.Cut, hitPoint, damage);
            if (TryGetComponent(out CreatureDamageManager damageManager))
                damageManager.RegisterWoundWS(hitPoint, damage);
        }

        /// <summary>
        /// Applies one authored fauna interaction response.
        /// </summary>
        public void ApplyFaunaInteraction(FaunaInteractionKind interactionKind, Vector3 sourcePosition, float intensity)
        {
            if (_isDead || _faunaDataTemplate == null)
                return;

            if (!_faunaDataTemplate.TryGetInteractionResponse(interactionKind, out FaunaInteractionResponse response))
                return;

            if (FaunaInteractionResponse.ShouldForceRetreat(in response))
            {
                float retreatDuration = math.max(0.5f, response.RetreatDurationSeconds);
                _utilityBrain.ForceRetreat(sourcePosition, _cognitionTimeSeconds, retreatDuration);
                _stateMachine.currentState = AIState.Retreat;
                _currentStateCache = AIState.Retreat;
            }

            if (response.FearImpulse01 > 0f)
            {
                _sensorSuite.isScattering = true;
                Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                    ? resolvedSelfPosition
                    : sourcePosition + ResolveSelfLogicForward();
                Vector3 scatterDirection = selfPosition - sourcePosition;
                _sensorSuite.scatterDirection = scatterDirection.sqrMagnitude > 0.0001f
                    ? ResolveDominantAxisDirection(scatterDirection)
                    : ResolveSelfLogicForward();
            }

            if (response.DamageMultiplier > 1f && intensity > 0f)
            {
                float bonusDamage = intensity * (response.DamageMultiplier - 1f);
                if (bonusDamage > 0.001f)
                    TakeDamageFromSource(bonusDamage, sourcePosition);
            }
        }

        /// <summary>
        /// Instantly breaks boids alignment and applies evasion vector.
        /// [REQ] Zero-GC Panic Effect.
        /// </summary>
        public void TriggerPanicPulse(Vector3 predatorPos)
        {
            if (_isDead) return;
            
            _sensorSuite.isScattering = true;
            Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                ? resolvedSelfPosition
                : predatorPos + ResolveSelfLogicForward();
            Vector3 baseDir = selfPosition - predatorPos;
            _sensorSuite.scatterDirection = baseDir.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(baseDir)
                : ResolveSelfLogicForward();
            
            // [REQ] Audio Linking (Sound of Panic)
            OnPanicTriggered?.Invoke();
            
            // StateMachine will handle the timer via _scatterTimer if it's in Flocking state
        }

        public void ApplyParentalDefenseStimulus(Vector3 sourcePosition)
        {
            if (_isDead || _faunaDataTemplate == null || !_faunaDataTemplate.RespondsToParentalDefenseSignal)
                return;

            _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
            _currentStateCache = AIState.Aggressive;
            _stateMachine.currentState = AIState.Aggressive;
            _utilityBrain.RecordAuditoryStimulus(sourcePosition, _cognitionTimeSeconds);
        }

        public Vector3 ResolveContactForward()
        {
            return ResolveSelfLogicForward();
        }

        public void ApplyPredationDamage(float amount, Vector3 predatorPosition)
        {
            TakeDamageFromSource(amount, predatorPosition);
        }

        public void ForceApexRetreatFrom(Vector3 rivalPosition)
        {
            ForceApexRetreat(rivalPosition);
        }

        public float ResolveApexIntimidationRadiusMeters()
        {
            return ResolveApexIntimidationRadius();
        }

        /// <summary>
        /// [REQ] Final API Exposure for external tools (Propulsion Cannon, Flashlight).
        /// Forces the AI into a Retreat state strictly away from the threatPosition.
        /// </summary>
        public void Provoke(Vector3 threatPosition)
        {
            if (_isDead) return;

            if (_utilityBrain.IsActivePredator != 0)
            {
                _predatorSquadStateBits |= PredatorSquadStateFleeingBit;
                _predatorSquadStateBits &= ~(PredatorSquadStateHuntingBit | PredatorSquadStateFlankingBit);
                _utilityBrain.ForceRetreat(threatPosition, _cognitionTimeSeconds, 8f);
                _stateMachine.currentState = AIState.Retreat;
                return;
            }

            _utilityBrain.ForceRetreat(threatPosition, _cognitionTimeSeconds, 8f);
            _stateMachine.currentState = AIState.Retreat;
            _currentStateCache = AIState.Retreat;
        }

        internal void ForceApexRetreat(Vector3 rivalPosition)
        {
            if (_isDead)
                return;

            _predatorSquadStateBits |= PredatorSquadStateFleeingBit;
            _predatorSquadStateBits &= ~(PredatorSquadStateHuntingBit | PredatorSquadStateFlankingBit);
            float retreatDuration = ResolveApexForcedRetreatDuration();
            _utilityBrain.ForceRetreat(rivalPosition, _cognitionTimeSeconds, retreatDuration);
            _utilityBrain.ApplyExternalState(AIState.ApexForcedRetreat, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.ApexForcedRetreat;
            _currentStateCache = AIState.ApexForcedRetreat;

            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                ? resolvedSelfPosition
                : rivalPosition + ResolveSelfLogicForward();
            if (ecosystemDirector != null &&
                ecosystemDirector.TryResolveMigrationTarget(ComputeStableSpeciesId(), selfPosition, out Vector3 migrationTarget))
            {
                if (TryResolveAupFromRuntimeOrigin(migrationTarget, out AbsoluteUniversePosition migrationTargetAup))
                {
                    _forcedMigrationTarget = migrationTarget;
                    _forcedMigrationTargetAup = migrationTargetAup;
                    _forcedMigrationUntilTime = _cognitionTimeSeconds + retreatDuration;
                    _hasForcedMigrationTarget = true;
                }
            }
        }

        private void GainApexIntimidation()
        {
            _apexIntimidationUntilTime = math.max(_apexIntimidationUntilTime, _cognitionTimeSeconds + ResolveApexIntimidationDuration());
        }

        private bool ShouldApplySpatialDensityPenalty()
        {
            return _stateMachine.isFlockingFish ||
                   _stateMachine.currentState == AIState.Flocking ||
                   _currentStateCache == AIState.Flocking;
        }

        private static Vector3 ResolveDensityPenaltyDirection(Vector3 desiredDirection, Vector3 densityPenaltyDirection)
        {
            Vector3 resolvedPenalty = densityPenaltyDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(densityPenaltyDirection)
                : Vector3.zero;
            if (resolvedPenalty.sqrMagnitude <= 0.0001f)
                return desiredDirection;

            Vector3 blendedDirection = desiredDirection + resolvedPenalty * SpatialDensityPenaltyDirectionWeight;
            return blendedDirection.sqrMagnitude > 0.0001f ? ResolveDominantAxisDirection(blendedDirection) : resolvedPenalty;
        }

        private static bool IsRetreatState(AIState state)
        {
            return state == AIState.Retreat || state == AIState.ApexForcedRetreat;
        }

        private void Die()
        {
            PromoteHunterSquadAlphaAfterLocalLoss();
            _isDead = true;
            PublishCarrionDeathSignal();
            RegisterCorpseResourceNode();
            ReportApexPredatorKill();
            BeginDeathSpiralPresentation();
        }

        private void PublishCarrionDeathSignal()
        {
            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition corpseAup))
                return;

            uint speciesHash = unchecked((uint)ComputeStableSpeciesId());
            uint entityHash = ResolveStableFaunaHash(FaunaCarrionDeathHashSalt, 0u);

            SignalBus<EntityDeathSignal>.TryPush(new EntityDeathSignal
            {
                PositionAup = corpseAup,
                EntityHash = entityHash,
                SourceHash = speciesHash,
                Intensity01 = math.saturate(_maxHealth * math.rcp(math.max(1f, LargeCorpseResourceMinHealth))),
                Flags = EntityDeathSignal.FlagFaunaBrainCarrion
            });
        }

        private void BeginDeathSpiralPresentation()
        {
            ClearPredatorLungeCheat();
            ClearAttackTelegraphState();
            ClearCorpseLatchState();
            ClearProceduralStrikeIntent();
            ClearVoxelPathGuidance();
            ClearDirectorHuntTarget();
            ClearHibernationStarvationHuntCommand();
            ClearEcholocationMimicSignal();
            ClearPredatorSquadStatePreserveAlphaHandoff();
            UnregisterSpatialHandle();
            _utilityBrain.SetRuntimeActive(false);

            CaptureBaseRigidbodyPresentationState();
            ApplySimplifiedRagdollHandoff();
            _deathSpiralActive = true;
            _deathSpiralStartTime = _cognitionTimeSeconds;
            _deathDitherFade01 = 0f;
            _corpseBloatAge01 = 0f;
            _whaleFallDecay01 = 0f;
            _hitFlash01 = 0f;
            _passiveFlashlightDimUntilTime = 0f;
            _retinalBlindBiolumUntilTime = 0f;
            _lastRetinalBlindSignalFrame = uint.MaxValue;
            _faunaBiolumDim01 = 1f;
            _lastAppliedBiolumLightScale01 = -1f;
            _lastAppliedCorpseBloatShader01 = -1f;
            _lastAppliedDecayAmountShader01 = -1f;
            _lastAppliedHitFlashShader01 = -1f;
            ArmCorpseBloatShaderTimer();
            _deathSpiralTorque = ResolveDeathSpiralTorque();
            ResolveDeathCorkscrewPhases(out _deathCorkscrewPhaseX, out _deathCorkscrewPhaseZ);
            TryResolveCorpseSinkingKinematicsBuffers(out _, out _);
            TryRegisterCorpseSinkLateFrame();
            Vector3 corpseRuntimePosition = _rb != null
                ? _rb.position
                : ToVector3(_corpseSinkAup.ToRuntimeFloat3());
            if (!TryResolveAupFromRuntimeOrigin(corpseRuntimePosition, out _corpseSinkAup))
                _corpseSinkAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            _corpseFloorY = corpseRuntimePosition.y;
            _corpseFloorLatched = false;

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.detectCollisions = false;
                _rb.useGravity = false;
                TryQueueLinearVelocitySet(_rb, Vector3.zero, wake: false);
                TryQueueAngularVelocitySet(_rb, Vector3.zero, wake: false);
                _rb.Sleep();
            }
        }

        private Vector3 ResolveDeathSpiralTorque()
        {
            uint phase = ResolveStableFaunaHash(FaunaDeathSpiralHashSalt, 0u);
            Vector3 axis = ResolveDeathSpiralAxis((int)(phase & 7u));
            float magnitude01 = ((phase >> 8) & 255u) * 0.00392156863f;
            float magnitude = math.lerp(DeathSpiralTorqueMin, DeathSpiralTorqueMax, magnitude01);
            return axis * magnitude;
        }

        private void ApplySimplifiedRagdollHandoff()
        {
            if (_simplifiedRagdollHandoff == null)
                TryGetComponent(out _simplifiedRagdollHandoff);

            if (_simplifiedRagdollHandoff == null)
                return;

            Vector3 lastVertexVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            if (lastVertexVelocity.sqrMagnitude <= 0.0001f)
                lastVertexVelocity = ResolveSelfLogicForward() * math.max(0f, _steeringEngine.maxSpeed);

            _simplifiedRagdollHandoff.BeginHandoff(_renderer, lastVertexVelocity);
        }

        private void ApplyDeathSpiralFixedStep(float fdt)
        {
            if (!_deathSpiralActive || fdt <= 0f)
                return;

            float age = math.max(0f, _cognitionTimeSeconds - _deathSpiralStartTime);
            if (age < DeathSpiralSteeringDurationSeconds)
            {
                ApplyDeathSpiralCorkscrewStep(fdt, age);
                return;
            }

            ScheduleCorpseSinkingKinematicStep(fdt);
        }

        private void ApplyDeathSpiralCorkscrewStep(float fdt, float age)
        {
            if (_rb == null)
                return;

            Vector3 lateralVelocity = ResolveDeathSpiralLateralVelocity(age);
            Vector3 nextPosition = _rb.position + ((Vector3.down * DeathSpiralCorkscrewDescentSpeed) + lateralVelocity) * fdt;
            if (_deathSpiralTorque.sqrMagnitude > 0.0001f)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(_deathSpiralTorque * (RadiansToDegrees * fdt)));

            _rb.MovePosition(nextPosition);
            if (TryResolveAupFromRuntimeOrigin(nextPosition, out AbsoluteUniversePosition nextAup))
                _corpseSinkAup = nextAup;
        }

        private Vector3 ResolveDeathSpiralLateralVelocity(float age)
        {
            float x = TrianglePulse01(age, DeathSpiralCorkscrewFrequency, _deathCorkscrewPhaseX) * 2f - 1f;
            float z = TrianglePulse01(age, DeathSpiralCorkscrewFrequency * 0.73f, _deathCorkscrewPhaseZ) * 2f - 1f;
            return new Vector3(x * DeathSpiralCorkscrewAxisSpeed, 0f, z * DeathSpiralCorkscrewAxisSpeed);
        }

        private void ResolveDeathCorkscrewPhases(out float phaseX, out float phaseZ)
        {
            uint hash = ResolveStableFaunaHash(FaunaDeathCorkscrewHashSalt, 0u);
            phaseX = ((hash >> 8) & 255u) * 0.00392156863f;
            phaseZ = ((hash >> 16) & 255u) * 0.00392156863f;
        }

        private void ScheduleCorpseSinkingKinematicStep(float fdt)
        {
            if (_corpseSinkJobScheduled)
                return;

            if (!TryResolveCorpseSinkingKinematicsBuffers(
                    out NativeArray<CorpseSinkKinematicInput> corpseSinkInput,
                    out NativeArray<CorpseSinkKinematicOutput> corpseSinkOutput))
            {
                return;
            }

            double3 committedOriginOffset = ToCommittedOriginOffset();
            float3 position = AUPMath.ToRuntimeFloat3(in _corpseSinkAup, committedOriginOffset);
            if (!_corpseFloorLatched)
            {
                _corpseFloorY = ResolveCorpseFloorY(new Vector3(position.x, position.y, position.z));
                _corpseFloorLatched = true;
            }

            corpseSinkInput[0] = new CorpseSinkKinematicInput
            {
                PositionAup = _corpseSinkAup.ToAlignedBlit(),
                FloatingOriginOffset = committedOriginOffset,
                FloorY = _corpseFloorY,
                DeltaTime = fdt,
                SinkSpeedMetersPerSecond = CorpseSinkSpeedMetersPerSecond,
                FloorSettleOffsetMeters = CorpseFloorSettleOffsetMeters
            };

            _corpseSinkPoseDeltaTime = fdt;
            _corpseSinkJobHandle = new CorpseSinkKinematicJob
            {
                Input = corpseSinkInput,
                Output = corpseSinkOutput
            }.Schedule();
            _corpseSinkJobScheduled = true;
            TryRegisterCorpseSinkLateFrame();
            JobHandle.ScheduleBatchedJobs();
        }

        private float ResolveCorpseFloorY(Vector3 position)
        {
            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null &&
                vegetationBridge.TryGetCachedTerrainHeight(position.x, position.z, out float terrainHeight) &&
                math.isfinite(terrainHeight))
            {
                return terrainHeight;
            }

            return position.y - 512f;
        }

        private void ApplyCorpseKinematicPose(Vector3 position, float fdt, bool freezeMotion)
        {
            if (_rb == null)
                return;

            if (_deathSpiralTorque.sqrMagnitude > 0.0001f && !freezeMotion)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(_deathSpiralTorque * (RadiansToDegrees * fdt)));

            _rb.MovePosition(position);
            if (freezeMotion)
                _rb.Sleep();
        }

        private void CompleteCorpseSinkingKinematicsIfReady()
        {
            if (!_corpseSinkJobScheduled)
            {
                if (!_deathSpiralActive)
                    TryUnregisterCorpseSinkLateFrame();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _corpseSinkJobHandle, forceComplete: false))
                return;

            _corpseSinkJobScheduled = false;
            if (!TryResolveCorpseSinkingOutputBuffer(out NativeArray<CorpseSinkKinematicOutput> corpseSinkOutput))
                return;

            CorpseSinkKinematicOutput output = corpseSinkOutput[0];
            _corpseSinkAup = AbsoluteUniversePosition.FromAlignedBlit(in output.PositionAup);
            Vector3 runtimePosition = new Vector3(output.RuntimePosition.x, output.RuntimePosition.y, output.RuntimePosition.z);
            bool freezeMotion = output.FreezeMotion != 0;
            ApplyCorpseKinematicPose(runtimePosition, _corpseSinkPoseDeltaTime, freezeMotion);
            if (freezeMotion)
                TryUnregisterCorpseSinkLateFrame();
        }

        private bool TryResolveCorpseSinkingKinematicsBuffers(
            out NativeArray<CorpseSinkKinematicInput> corpseSinkInput,
            out NativeArray<CorpseSinkKinematicOutput> corpseSinkOutput)
        {
            corpseSinkInput = default;
            corpseSinkOutput = default;
            IDataVault vault = _corpseSinkVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearCorpseSinkingKinematicsDescriptors();
                return false;
            }

            if (TryValidateCorpseSinkingKinematicsHandles(
                    vault,
                    in _corpseSinkInputHandle,
                    in _corpseSinkOutputHandle,
                    out corpseSinkInput,
                    out corpseSinkOutput))
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<CorpseSinkKinematicInput>(
                    BufferID.FaunaCorpseSinkKinematicInput,
                    out VaultGenerationHandle<CorpseSinkKinematicInput> borrowedInput) &&
                vault.TryGetGenerationHandle<CorpseSinkKinematicOutput>(
                    BufferID.FaunaCorpseSinkKinematicOutput,
                    out VaultGenerationHandle<CorpseSinkKinematicOutput> borrowedOutput) &&
                TryValidateCorpseSinkingKinematicsHandles(vault, in borrowedInput, in borrowedOutput, out corpseSinkInput, out corpseSinkOutput))
            {
                _corpseSinkInputHandle = borrowedInput;
                _corpseSinkOutputHandle = borrowedOutput;
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                ClearCorpseSinkingKinematicsDescriptors();
                return false;
            }

            VaultGenerationHandle<CorpseSinkKinematicInput> acquiredInput = vault.EnsureGenerationHandle<CorpseSinkKinematicInput>(
                BufferID.FaunaCorpseSinkKinematicInput,
                1,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<CorpseSinkKinematicOutput> acquiredOutput = vault.EnsureGenerationHandle<CorpseSinkKinematicOutput>(
                BufferID.FaunaCorpseSinkKinematicOutput,
                1,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);

            if (!TryValidateCorpseSinkingKinematicsHandles(vault, in acquiredInput, in acquiredOutput, out corpseSinkInput, out corpseSinkOutput))
            {
                ClearCorpseSinkingKinematicsDescriptors();
                return false;
            }

            _corpseSinkInputHandle = acquiredInput;
            _corpseSinkOutputHandle = acquiredOutput;
            return true;
        }

        private bool TryResolveCorpseSinkingOutputBuffer(out NativeArray<CorpseSinkKinematicOutput> corpseSinkOutput)
        {
            corpseSinkOutput = default;
            return TryResolveCorpseSinkingKinematicsBuffers(out _, out corpseSinkOutput);
        }

        private static bool TryValidateCorpseSinkingKinematicsHandles(
            IDataVault vault,
            in VaultGenerationHandle<CorpseSinkKinematicInput> inputHandle,
            in VaultGenerationHandle<CorpseSinkKinematicOutput> outputHandle,
            out NativeArray<CorpseSinkKinematicInput> input,
            out NativeArray<CorpseSinkKinematicOutput> output)
        {
            input = default;
            output = default;
            return vault != null &&
                   IsVaultHandleCreated(in inputHandle) &&
                   IsVaultHandleCreated(in outputHandle) &&
                   vault.TryResolveHandle(in inputHandle, out input) &&
                   input.IsCreated &&
                   input.Length >= 1 &&
                   vault.TryResolveHandle(in outputHandle, out output) &&
                   output.IsCreated &&
                   output.Length >= 1;
        }

        private void ReleaseCorpseSinkingKinematicsBuffers()
        {
            bool hasNativeState =
                IsVaultHandleCreated(in _corpseSinkInputHandle) ||
                IsVaultHandleCreated(in _corpseSinkOutputHandle) ||
                _corpseSinkJobScheduled;
            TryUnregisterCorpseSinkLateFrame();
            if (!hasNativeState)
            {
                _corpseSinkJobHandle = default;
                _corpseSinkPoseDeltaTime = 0f;
                return;
            }

            if (_corpseSinkJobScheduled)
                DispatcherJobSwap.TryComplete(ref _corpseSinkJobHandle, forceComplete: true);

            ClearCorpseSinkingKinematicsDescriptors();
            _corpseSinkJobHandle = default;
            _corpseSinkJobScheduled = false;
            _corpseSinkPoseDeltaTime = 0f;
        }

        private void CacheCorpseSinkVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_corpseSinkVault, vault))
                return;

            ClearCorpseSinkingKinematicsDescriptors();
            _corpseSinkVault = vault;
        }

        private void ClearCorpseSinkingKinematicsDescriptors()
        {
            _corpseSinkInputHandle = default;
            _corpseSinkOutputHandle = default;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void TryRegisterCorpseSinkLateFrame()
        {
            if (_corpseSinkLateFrameRegistered || !Application.isPlaying)
                return;

            _corpseSinkLateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterCorpseSinkLateFrame()
        {
            if (!_corpseSinkLateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _corpseSinkLateFrameRegistered = false;
        }

        private static double3 ToCommittedOriginOffset()
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return double3.zero;

            double3 absoluteOrigin = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteOrigin)) ? absoluteOrigin : double3.zero;
        }

        private bool IsWhaleFallCorpseRuntime()
        {
            return IsApexPredator() ||
                   (_speciesProfile != null && _speciesProfile.isLeviathan) ||
                   (_archetype != null && _archetype.roleType == CreatureRoleType.Leviathan);
        }

        private float ResolveCorpsePresentationDurationSeconds()
        {
            return IsWhaleFallCorpseRuntime() ? WhaleFallDurationSeconds : DeathSpiralFadeDelaySeconds;
        }

        private void UpdateDeathSpiralPresentation(float dt)
        {
            if (!_deathSpiralActive)
                return;

            float age = math.max(0f, _cognitionTimeSeconds - _deathSpiralStartTime);
            _whaleFallDecay01 = IsWhaleFallCorpseRuntime()
                ? math.saturate(age * math.rcp(WhaleFallDurationSeconds))
                : 0f;
            ApplyFaunaPresentationShaderState(_faunaBiolumDim01, _deathDitherFade01, _corpseBloatAge01, _hitFlash01, _whaleFallDecay01);
            float fadeAge = age - ResolveCorpsePresentationDurationSeconds();

            if (fadeAge > 0f)
            {
                _deathDitherFade01 = math.saturate(fadeAge * DeathSpiralFadeInvDurationSeconds);
                ApplyBiolumPresentationLightScale(1f - _deathDitherFade01);
                ApplyFaunaPresentationShaderState(_faunaBiolumDim01, _deathDitherFade01, _corpseBloatAge01, 0f, _whaleFallDecay01);
            }

            if (_deathDitherFade01 < 0.999f)
                return;

            QueueSelfDespawnOrDeactivate();
        }

        private void RegisterCorpseResourceNode()
        {
            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            if (ecosystemDirector == null)
                return;

            if (!ShouldRegisterLargeCorpseResourceNode())
                return;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition corpseAup))
                return;

            ecosystemDirector.RegisterCorpseResourceNode(
                in corpseAup,
                ComputeStableSpeciesId(),
                math.max(12f, _maxHealth * 0.35f),
                ContaminatedMeatItemHash);
        }

        private bool ShouldRegisterLargeCorpseResourceNode()
        {
            if (IsApexPredator())
                return true;

            if (_archetype == null)
                return _maxHealth >= LargeCorpseResourceMinHealth;

            if (_archetype.maxHealth < LargeCorpseResourceMinHealth)
                return false;

            return _archetype.roleType == CreatureRoleType.Hunter ||
                   _archetype.roleType == CreatureRoleType.Territorial ||
                   _archetype.roleType == CreatureRoleType.Leviathan;
        }

        private void ReportApexPredatorKill()
        {
            if (_utilityBrain.IsActivePredator == 0)
                return;

            if (!TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) ||
                !TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return;

            if (AbsoluteUniversePosition.DistanceSq(in playerAup, in selfAup) > 22500d)
                return;

            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized)
                return;

            float hostilityDelta = 0.22f;
            if ((_speciesProfile != null && _speciesProfile.isLeviathan) ||
                (_archetype != null && _archetype.roleType == CreatureRoleType.Leviathan))
            {
                hostilityDelta = 0.35f;
            }

            float3 selfRuntime = selfAup.ToRuntimeFloat3();
            Vector3 selfPosition = new Vector3(selfRuntime.x, selfRuntime.y, selfRuntime.z);
            if (_uniqueInstanceUid != 0u)
            {
                ecosystemDirector.RegisterApexPredatorKill(_uniqueInstanceUid, selfPosition, hostilityDelta);
                return;
            }

            ecosystemDirector.ReportApexPredatorKilled(selfPosition, hostilityDelta);
        }

        internal void SetLogicalIdentity(uint uniqueInstanceUid)
        {
            _uniqueInstanceUid = uniqueInstanceUid;
            if (_logicalLodTier == FaunaLogicalLodTier.DataOnly)
                RefreshTier1LodProxy(FaunaLogicalLodTier.DataOnly);
        }

        public void ApplyCleanerSymbiosis(float fatigueRelief)
        {
            if (fatigueRelief <= 0f)
                return;

            _utilityBrain.ApplyFatigueRelief(fatigueRelief);
        }

        internal float CurrentHunger01 => CreatureUtilityBrain.ResolveCurrentHunger01(in _utilityBrain);

        internal void SetHibernationHunger01(float hunger01)
        {
            _utilityBrain.SetHunger01(hunger01);
        }

        internal void ForceStarvingState()
        {
            _stateMachine.currentState = AIState.Starving;
            _currentStateCache = AIState.Starving;
            _utilityBrain.ApplyExternalState(AIState.Starving, _cognitionTimeSeconds);
        }

        internal void ForceHighPriorityHibernationHunt(Vector3 targetPosition, float hunger01)
        {
            if (_isDead || _utilityBrain.IsActivePredator == 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(targetPosition, out AbsoluteUniversePosition targetAup))
                return;

            _utilityBrain.SetHunger01(hunger01);
            _hibernationStarvationHuntTarget = targetPosition;
            _hibernationStarvationHuntTargetAup = targetAup;
            _hibernationStarvationHuntUntilTime = _cognitionTimeSeconds + HibernationStarvationHuntDurationSeconds;
            _hasHibernationStarvationHuntTarget = true;
            _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
            if (TryResolveSelfLogicPosition(out Vector3 selfPosition))
                ApplyDirectedStateOverride((float3)selfPosition, targetPosition, AIState.Aggressive);
        }

        private void ClearHibernationStarvationHuntCommand()
        {
            _hasHibernationStarvationHuntTarget = false;
            _hibernationStarvationHuntTarget = default;
            _hibernationStarvationHuntTargetAup = default;
            _hibernationStarvationHuntUntilTime = 0f;
        }

        internal void SetLogicalLodTier(FaunaLogicalLodTier logicalLodTier)
        {
            if (_logicalLodTier == logicalLodTier)
            {
                if (logicalLodTier == FaunaLogicalLodTier.DataOnly)
                    RefreshTier1LodProxy(logicalLodTier);
                else
                    UnregisterTier1LodProxy();

                return;
            }

            _logicalLodTier = logicalLodTier;
            ApplyLogicalLodPresentationState(logicalLodTier);
            if (logicalLodTier == FaunaLogicalLodTier.DataOnly)
                RefreshTier1LodProxy(logicalLodTier);
            else
                UnregisterTier1LodProxy();
        }

        private void ResolveLogicalLodTier()
        {
            IEcosystemDirectorService ecosystemDirector = ResolveCachedEcosystemDirectorService();
            if (ecosystemDirector == null)
            {
                SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                return;
            }

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
            {
                SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                return;
            }

            bool hasRuntimeContext = TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                                     runtimeContext != null &&
                                     (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            if (!hasRuntimeContext)
            {
                SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                return;
            }

            AbsoluteUniversePosition playerAup = runtimeContext.MovementState.PredictedAup;
            FaunaLogicalLodTier resolvedTier = ecosystemDirector.ResolveLogicalLodTier(in playerAup, in selfAup);
            SetLogicalLodTier(resolvedTier);
            if (resolvedTier == FaunaLogicalLodTier.Hibernating)
                TryPersistTier2HibernationAndDespawn();
        }

        private void RefreshTier1LodProxy(FaunaLogicalLodTier logicalLodTier)
        {
            if (logicalLodTier != FaunaLogicalLodTier.DataOnly ||
                _isDead ||
                !Application.isPlaying ||
                !TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
            {
                UnregisterTier1LodProxy();
                return;
            }

            FaunaTier1LodProxyEntry entry = BuildTier1LodProxyEntry(in selfAup);
            _tier1LodProxyHandle = FaunaTier1LodProxyRegistry.RegisterOrUpdate(_tier1LodProxyHandle, in entry);
        }

        private void UnregisterTier1LodProxy()
        {
            if (!Application.isPlaying)
            {
                _tier1LodProxyHandle = 0;
                return;
            }

            FaunaTier1LodProxyRegistry.Unregister(ref _tier1LodProxyHandle);
        }

        private FaunaTier1LodProxyEntry BuildTier1LodProxyEntry(in AbsoluteUniversePosition selfAup)
        {
            bool isApex = IsApexPredator();
            bool isPredator = IsPredatorForHibernation();
            bool isLargeThreat = ShouldUseProceduralLeviathanPresentation() ||
                                 isApex ||
                                 (_speciesProfile != null && _speciesProfile.isLeviathan);

            byte flags = FaunaTier1LodProxyRegistry.FlagDataOnly;
            if (_isDead)
                flags |= FaunaTier1LodProxyRegistry.FlagDead;
            if (isPredator)
                flags |= FaunaTier1LodProxyRegistry.FlagPredator;
            if (isApex)
                flags |= FaunaTier1LodProxyRegistry.FlagApex;
            if (isLargeThreat)
                flags |= FaunaTier1LodProxyRegistry.FlagLargeThreat;

            return new FaunaTier1LodProxyEntry
            {
                PositionAup = selfAup.ToAlignedBlit(),
                InstanceUid = _uniqueInstanceUid,
                SpeciesId = PackTier1SpeciesId(ComputeStableSpeciesId()),
                StatusFlags = FaunaTier1LodProxyEntry.PackStatusFlags(
                    flags,
                    ResolveTier1HeadingOctant(),
                    PackTier1UnitByte(HealthNormalized),
                    PackTier1UnitByte(CurrentHunger01),
                    1),
                Reserved1 = 0u
            };
        }

        private byte ResolveTier1HeadingOctant()
        {
            Vector3 direction = _cachedDesiredDirection;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = ResolveSelfLogicForward();

            float absX = math.abs(direction.x);
            float absZ = math.abs(direction.z);
            if (absX <= 0.0001f && absZ <= 0.0001f)
                return 0;

            if (absX > absZ * 2f)
                return direction.x >= 0f ? (byte)2 : (byte)6;

            if (absZ > absX * 2f)
                return direction.z >= 0f ? (byte)0 : (byte)4;

            if (direction.x >= 0f)
                return direction.z >= 0f ? (byte)1 : (byte)3;

            return direction.z >= 0f ? (byte)7 : (byte)5;
        }

        private static ushort PackTier1SpeciesId(int speciesId)
        {
            if (speciesId <= 0)
                return 0;

            return speciesId >= ushort.MaxValue ? ushort.MaxValue : (ushort)speciesId;
        }

        private static byte PackTier1UnitByte(float value)
        {
            return (byte)math.clamp((int)(math.saturate(value) * 255f + 0.5f), 0, 255);
        }

        private void TryPersistTier2HibernationAndDespawn()
        {
            if (_tier2HibernationRecordWritten ||
                _tier2HibernationHandoffInProgress ||
                _isDead ||
                _uniqueInstanceUid == 0u ||
                !Application.isPlaying)
            {
                return;
            }

            IFaunaPersistentWorldStateService registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            if (!TryResolveSelfLogicAup(out AbsoluteUniversePosition positionAup))
                return;

            EntityDataRecord cachedState = PersistentWorldRegistry.CreateFaunaHibernationState(
                _uniqueInstanceUid,
                ComputeStableSpeciesId(),
                HealthNormalized,
                in positionAup,
                IsLargeThreatForHibernation(),
                IsPredatorForHibernation(),
                ReadDispatcherTimeSeconds(),
                CurrentHunger01);

            if (!registry.TryCacheFaunaHibernationState(in cachedState))
                return;

            _tier2HibernationRecordWritten = true;
            _tier2HibernationHandoffInProgress = true;

            QueueSelfDespawnOrDeactivate();

            _tier2HibernationHandoffInProgress = false;
        }

        private void ClearQueuedPresentationSyncState()
        {
            _pendingFaunaPresentationShaderStateDirty = false;
            _pendingFaunaPresentationBiolumDim01 = 1f;
            _pendingFaunaPresentationDeathDitherFade01 = 0f;
            _pendingFaunaPresentationCorpseBloatAge01 = 0f;
            _pendingFaunaPresentationHitFlash01 = 0f;
            _pendingFaunaPresentationDecayAmount01 = 0f;
            _pendingBiolumPresentationLightScaleDirty = false;
            _pendingBiolumPresentationLightScale01 = 1f;
            _pendingCorpseBloatShaderTimerDirty = false;
            _pendingCorpseBloatShaderStartTimeSeconds = -1f;
            _pendingAupPresentationPoseDirty = false;
            _pendingAupPresentationPosition = Vector3.zero;
            _pendingSelfDespawnOrDeactivate = false;
            _pendingExternalDespawnOrDeactivate = null;
        }

        private void QueueSelfDespawnOrDeactivate()
        {
            _pendingSelfDespawnOrDeactivate = true;
        }

        private void QueueExternalDespawnOrDeactivate(GameObject target)
        {
            if (target == null)
                return;

            _pendingExternalDespawnOrDeactivate = target;
        }

        private void FlushQueuedDespawnOrDeactivate()
        {
            GameObject externalTarget = _pendingExternalDespawnOrDeactivate;
            _pendingExternalDespawnOrDeactivate = null;
            if (externalTarget != null)
            {
                IObjectPoolService pool = _objectPool;
                if (pool != null)
                    pool.Despawn(externalTarget);
                else
                    externalTarget.SetActive(false);
            }

            if (!_pendingSelfDespawnOrDeactivate)
                return;

            _pendingSelfDespawnOrDeactivate = false;
            IObjectPoolService poolSelf = _objectPool;
            if (poolSelf != null)
                poolSelf.Despawn(gameObject);
            else
                gameObject.SetActive(false);
        }

        private bool IsLargeThreatForHibernation()
        {
            return ShouldUseProceduralLeviathanPresentation() ||
                   IsApexPredator() ||
                   (_speciesProfile != null && _speciesProfile.isLeviathan);
        }

        private bool IsPredatorForHibernation()
        {
            return isAggressive ||
                   _utilityBrain.IsActivePredator != 0 ||
                   (_speciesProfile != null && _speciesProfile.baseAggro >= 0.45f);
        }

        private static float ReadDispatcherTimeSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            double seconds = dispatcher != null ? dispatcher.DilatedTimeSeconds : 0d;
            if (!math.isfinite(seconds) || seconds <= 0d)
                return 0f;

            return seconds > float.MaxValue ? float.MaxValue : (float)seconds;
        }

        private void CacheLogicalLodComponents()
        {
            _logicalLodColliderScratch.Clear();
            GetComponentsInChildren(true, _logicalLodColliderScratch);
            if (_logicalLodColliderScratch.Count > 0)
            {
                _logicalLodColliders = new Collider[_logicalLodColliderScratch.Count]; // COLD ALLOC: Collider[_logicalLodColliderScratch.Count] - cached fauna colliders toggled by logical LOD - owner: FaunaBrain
                for (int i = 0; i < _logicalLodColliderScratch.Count; i++)
                    _logicalLodColliders[i] = _logicalLodColliderScratch[i];
            }

            _logicalLodColliderScratch.Clear();
        }

        private void ApplyLogicalLodPresentationState(FaunaLogicalLodTier logicalLodTier)
        {
            bool suppressPresentation = logicalLodTier != FaunaLogicalLodTier.FullSim;
            if (_logicalLodPresentationSuppressed == suppressPresentation)
                return;

            _logicalLodPresentationSuppressed = suppressPresentation;
            for (int i = 0; i < _logicalLodColliders.Length; i++)
            {
                Collider cachedCollider = _logicalLodColliders[i];
                if (cachedCollider != null)
                    cachedCollider.enabled = !suppressPresentation;
            }

            if (suppressPresentation && _rb != null && !_rb.IsSleeping())
                _rb.Sleep();
        }

        private void ApplyCognitionEvaluation(in CreatureUtilityEvaluation evaluation)
        {
            _cachedDesiredDirection = evaluation.DesiredDirection;
            _currentStateCache = evaluation.LegacyState;
            _stateMachine.currentState = evaluation.LegacyState;
            _stateMachine.currentForceMultiplier = evaluation.ForceMultiplier;
            _stateMachine.currentSpeedMultiplier = evaluation.SpeedMultiplier;
            _stateMachine.currentTurnMultiplier = evaluation.TurnMultiplier;
            _currentPackRole = (PredatorPackRole)evaluation.PackRoleCode;
            _flankingManeuverDetected = CreatureUtilityEvaluation.FlankingManeuverDetected(in evaluation);
            if (_flankingManeuverDetected)
                _predatorSquadStateBits |= PredatorSquadStateFlankingBit;
            else
                _predatorSquadStateBits &= ~PredatorSquadStateFlankingBit;
        }

        private void ResetStateCache()
        {
            AIState initialState = ResolveInitialState();
            _stateMachine.ResetRuntime(initialState);
            _currentStateCache = initialState;
            _cachedDesiredDirection = _rb != null ? ResolveDominantAxisDirection(_rb.rotation * Vector3.forward) : Vector3.forward;
            _currentPackRole = PredatorPackRole.None;
            _flankingManeuverDetected = false;
            _apexRivalTarget = null;
            _baitFeedingTarget = null;
            _forcedMigrationTarget = default;
            _forcedMigrationTargetAup = default;
            _apexIntimidationUntilTime = 0f;
            _forcedMigrationUntilTime = 0f;
            _nextBurrowBreachTime = 0f;
            _nextBestiaryObservationTime = 0f;
            _nextMimicPingTime = 0f;
            _mimicPingExpireTime = 0f;
            _hasForcedMigrationTarget = false;
            ClearEcholocationMimicSignal();
            ClearVoxelPathGuidance();
            ClearDirectorHuntTarget();
            ClearPredatorSquadState();
        }

        public bool SupportsAttackPattern(FaunaAttackPattern attackPattern)
        {
            return _faunaDataTemplate != null && _faunaDataTemplate.SupportsAttackPattern(attackPattern);
        }

        private bool TryResolveDynamicDodgeDirection(Vector3 desiredDirection, out Vector3 dodgeDirection)
        {
            dodgeDirection = default;
            if (ShouldUseProceduralLeviathanPresentation())
                return false;

            if (!_sensorSuite.TryGetDeferredObstacleAvoidance(out Vector3 avoidanceDirection, out float obstaclePressure01))
                return false;

            if (obstaclePressure01 <= 0f || avoidanceDirection.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 incoming = desiredDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(desiredDirection)
                : ResolveSelfLogicForward();
            Vector3 avoidance = avoidanceDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(avoidanceDirection)
                : incoming;
            float blend = math.saturate(obstaclePressure01);
            dodgeDirection = ResolveDominantAxisDirection(ToVector3(math.lerp((float3)incoming, (float3)avoidance, blend)));
            return dodgeDirection.sqrMagnitude > 0.0001f;
        }

        private bool TryResolveWallSlideDirection(Vector3 desiredDirection, out Vector3 slideDirection)
        {
            slideDirection = default;
            if (ShouldUseProceduralLeviathanPresentation())
                return false;

            if (_rb == null || !_sensorSuite.TryGetForwardObstacleSurface(out Vector3 obstacleNormal, out float obstaclePressure01))
                return false;

            Vector3 referenceVelocity = _rb.linearVelocity.sqrMagnitude > 0.0001f
                ? _rb.linearVelocity
                : desiredDirection;
            if (referenceVelocity.sqrMagnitude <= 0.0001f)
                referenceVelocity = ResolveSelfLogicForward();

            if (obstacleNormal.sqrMagnitude < 0.1f)
            {
                slideDirection = ResolveDegenerateWallTurnaroundDirection(desiredDirection, referenceVelocity);
                return slideDirection.sqrMagnitude > 0.0001f;
            }

            float3 projectedVelocity = ProjectVelocityAlongSurface(referenceVelocity, obstacleNormal);
            if (math.lengthsq(projectedVelocity) <= 0.0001f)
                return false;

            Vector3 incoming = desiredDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(desiredDirection)
                : ResolveDominantAxisDirection(referenceVelocity);
            Vector3 slide = ResolveDominantAxisDirection(ToVector3(projectedVelocity));
            float blend = math.max(0.5f, math.saturate(obstaclePressure01));
            slideDirection = ResolveDominantAxisDirection(ToVector3(math.lerp((float3)incoming, (float3)slide, blend)));
            return slideDirection.sqrMagnitude > 0.0001f;
        }

        private static float3 ProjectVelocityAlongSurface(float3 velocity, float3 surfaceNormal)
        {
            float3 safeVelocity = math.select(float3.zero, velocity, math.all(math.isfinite(velocity)));
            float normalMagnitudeSq = math.lengthsq(surfaceNormal);
            if (normalMagnitudeSq < 0.1f || !math.all(math.isfinite(surfaceNormal)))
                return float3.zero;

            float3 safeNormal = surfaceNormal * math.rsqrt(normalMagnitudeSq);
            return safeVelocity - (safeNormal * math.dot(safeVelocity, safeNormal));
        }

        private Vector3 ResolveDegenerateWallTurnaroundDirection(Vector3 desiredDirection, Vector3 referenceVelocity)
        {
            Vector3 incoming = desiredDirection.sqrMagnitude > 0.0001f
                ? desiredDirection
                : referenceVelocity;
            if (incoming.sqrMagnitude <= 0.0001f)
                incoming = ResolveSelfLogicForward();

            return incoming.sqrMagnitude > 0.0001f ? -ResolveDominantAxisDirection(incoming) : Vector3.back;
        }

        private void ApplyImmediateHitReaction(Vector3 damageSourcePosition, float normalizedDamage)
        {
            if (_rb == null)
                return;

            Vector3 awayDirection = ResolveDamageEscapeDirection(damageSourcePosition);
            float retreatDuration = _speciesProfile != null
                ? math.max(1f, _speciesProfile.retreatDuration)
                : 6f;

            _utilityBrain.ForceRetreat(damageSourcePosition, _cognitionTimeSeconds, retreatDuration);
            _utilityBrain.ApplyExternalState(AIState.Retreat, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.Retreat;
            _currentStateCache = AIState.Retreat;
            _cachedDesiredDirection = awayDirection;
            _sensorSuite.isScattering = true;
            _sensorSuite.scatterDirection = awayDirection;

            float targetFlinchVelocity = math.lerp(
                math.max(DamageFlinchVelocityFloor, _steeringEngine.maxSpeed),
                math.max(DamageFlinchVelocityCeiling, _steeringEngine.maxSpeed * 2.25f),
                math.saturate(normalizedDamage));
            targetFlinchVelocity = math.min(targetFlinchVelocity, DamageFlinchVelocityMaxMetersPerSecond);
            Vector3 targetVelocity = awayDirection * targetFlinchVelocity;
            Vector3 velocityChange = targetVelocity - _rb.linearVelocity;
            TryQueuePhysicsForce(_rb, velocityChange, ForceMode.VelocityChange);

            float fearIntensity = math.saturate(math.max(DamageFearPheromoneFloor, normalizedDamage * DamageFearPheromoneBoost));
            Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                ? resolvedSelfPosition
                : damageSourcePosition + awayDirection;
            ChemicalInfluenceGrid.QueueFearPheromone(selfPosition, fearIntensity);

            IMicroFaunaPresentationPulseSink microFaunaBoids = _sargassumMicroFauna;
            if (microFaunaBoids != null)
            {
                microFaunaBoids.RegisterPredatorFearBurst(
                    selfPosition,
                    awayDirection,
                    DamageMicroFaunaPanicRadiusMeters,
                    DamageMicroFaunaPanicDurationSeconds,
                    fearIntensity);
                microFaunaBoids.RegisterVatHitReaction(
                    selfPosition,
                    DamageMicroFaunaPanicRadiusMeters * 0.35f,
                    normalizedDamage);
            }
        }

        private Vector3 ResolveDamageEscapeDirection(Vector3 damageSourcePosition)
        {
            Vector3 selfPosition = TryResolveSelfLogicPosition(out Vector3 resolvedSelfPosition)
                ? resolvedSelfPosition
                : damageSourcePosition + ResolveSelfLogicForward();
            Vector3 awayDirection = selfPosition - damageSourcePosition;
            if (awayDirection.sqrMagnitude > 0.0001f)
                return ResolveDominantAxisDirection(awayDirection);

            if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f)
                return ResolveDominantAxisDirection(-_rb.linearVelocity);

            if (_cachedDesiredDirection.sqrMagnitude > 0.0001f)
                return ResolveDominantAxisDirection(-_cachedDesiredDirection);

            return -ResolveSelfLogicForward();
        }

        private Vector3 ResolveFallbackDamageSourcePosition()
        {
            if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f)
                return _rb.position + ResolveDominantAxisDirection(_rb.linearVelocity);

            if (_cachedDesiredDirection.sqrMagnitude > 0.0001f)
                return (TryResolveSelfLogicPosition(out Vector3 desiredFallbackPosition)
                    ? desiredFallbackPosition
                    : Vector3.zero) + ResolveDominantAxisDirection(_cachedDesiredDirection);

            return TryResolveSelfLogicPosition(out Vector3 fallbackPosition)
                ? fallbackPosition + ResolveSelfLogicForward()
                : ResolveSelfLogicForward();
        }

        private AIState ResolveInitialState()
        {
            if (_speciesProfile != null && _speciesProfile.isAmbusher)
                return AIState.Idle;

            return _stateMachine.isFlockingFish ? AIState.Flocking : AIState.Wander;
        }

        private int ComputeStableSpeciesId()
        {
            if (_faunaDataTemplate != null && _faunaDataTemplate.SpeciesId != 0)
                return _faunaDataTemplate.SpeciesId;

            if (_speciesProfile != null && _speciesProfile.speciesID != 0)
                return _speciesProfile.speciesID;

            if (_archetype != null && !string.IsNullOrWhiteSpace(_archetype.creatureId))
                return unchecked((int)Hecton.Localization.LocHash.Compute(_archetype.creatureId)) & int.MaxValue;

            return 0;
        }

        private void ApplyFaunaDataTemplate(FaunaDataTemplate faunaDataTemplate)
        {
            if (ReferenceEquals(_faunaDataTemplate, faunaDataTemplate))
            {
                ApplyTemplateRuntimeTuning();
                ConfigureFaunaScanMetadata();
                RefreshMimicOcclusionRuntimeOwner();
                return;
            }

            _faunaDataTemplate = faunaDataTemplate;
            ApplyTemplateRuntimeTuning();
            _utilityBrain.BindProfile(_speciesProfile, _archetype, _faunaDataTemplate);
            ConfigureFaunaScanMetadata();
            RefreshMimicOcclusionRuntimeOwner();
        }

        private void ApplyTemplateRuntimeTuning()
        {
            if (_faunaDataTemplate == null)
                return;

            _baseAggroDistance = _faunaDataTemplate.AggroRadius;
            _baseDeaggroDistance = math.max(_baseAggroDistance, _baseAggroDistance * 1.35f);
            _baseCruiseSpeed = _faunaDataTemplate.SwimSpeed;
            _baseBurstSpeed = math.max(_baseCruiseSpeed, _faunaDataTemplate.MaxSpeedMetersPerSecond);
            _baseTurnSpeed = _faunaDataTemplate.TurnRate;

            _sensorSuite.aggroDistance = _baseAggroDistance;
            _sensorSuite.deaggroDistance = _baseDeaggroDistance;
            _sensorSuite.visionConeAngle = _faunaDataTemplate.VisionConeAngle;

            _steeringEngine.moveSpeed = _baseCruiseSpeed;
            _steeringEngine.maxSpeed = _baseBurstSpeed;
            _steeringEngine.turnSpeed = _baseTurnSpeed;
            _steeringEngine.rotationSpeed = _baseTurnSpeed;
            _steeringEngine.swimForce = math.max(_baseCruiseSpeed, _baseBurstSpeed);
        }

        private float ResolveFleeHealthThreshold()
        {
            return _faunaDataTemplate != null
                ? _faunaDataTemplate.FleeHealthThreshold
                : 0.3f;
        }

        private bool ValidatePrimitiveColliderRig()
        {
            MeshCollider meshCollider = GetComponentInChildren<MeshCollider>(true);
            if (meshCollider != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("FaunaBrain requires primitive collider hygiene. MeshCollider detected on fauna hierarchy.", meshCollider);
#endif
                return false;
            }

            CapsuleCollider capsuleCollider = GetComponentInChildren<CapsuleCollider>(true);
            SphereCollider sphereCollider = GetComponentInChildren<SphereCollider>(true);
            _predatorLungeCcdCapsule = capsuleCollider;
            _predatorLungeCcdSphere = sphereCollider;
            if (capsuleCollider == null && sphereCollider == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("FaunaBrain requires a CapsuleCollider or SphereCollider on the fauna hierarchy.", this);
#endif
                return false;
            }

            return true;
        }

        public bool IsValidPreyFor(IFaunaSpatialContact predatorContact)
        {
            if (predatorContact == null || ReferenceEquals(predatorContact, this) || IsDead)
                return false;

            uint preyMaskBits = PreyMaskBits;
            return preyMaskBits != 0u && predatorContact.CanConsumePrey(preyMaskBits);
        }

        public bool CanConsumePrey(uint preyMaskBits)
        {
            return _faunaDataTemplate != null && _faunaDataTemplate.CanConsumePrey(preyMaskBits);
        }

        internal uint DietMaskBits => _faunaDataTemplate != null ? _faunaDataTemplate.DietMaskBits : 0u;

        internal uint PreyMaskBits => _faunaDataTemplate != null ? _faunaDataTemplate.PreyMaskBits : 0u;

        private void ConfigureFaunaScanMetadata()
        {
            _cachedScanEntryHash = 0u;
            if (_faunaDataTemplate == null)
                return;

            _cachedScanEntryHash = ScanEvents.ComputeEntryHash(_faunaDataTemplate.ScanEntryId);
            if (_scannableTarget == null)
                TryGetComponent(out _scannableTarget);

            FaunaScanRuntimeRegistry.Register(_faunaDataTemplate);
            if (_scannableTarget == null)
                return;

            string fallbackTitle = _archetype != null && !string.IsNullOrWhiteSpace(_archetype.displayName)
                ? _archetype.displayName
                : gameObject.name;
            string fallbackCategory = _archetype != null
                ? ResolveScanRoleCategory(_archetype.roleType)
                : "Fauna";
            string fallbackSummary = _archetype != null && !string.IsNullOrWhiteSpace(_archetype.gameplayPurpose)
                ? _archetype.gameplayPurpose
                : "Passive fauna contact. Manual classification pending.";

            _scannableTarget.Configure(
                _faunaDataTemplate.ScanEntryId,
                _faunaDataTemplate.ResolveScanTitle(fallbackTitle),
                _faunaDataTemplate.ResolveScanCategory(fallbackCategory),
                _faunaDataTemplate.ResolveScanSummary(fallbackSummary));
        }

        private void TryReportFaunaFeedingObservation()
        {
            if (_cachedScanEntryHash == 0u || _cognitionTimeSeconds < _nextBestiaryObservationTime)
                return;

            if (!TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) ||
                !TryResolveSelfLogicAup(out AbsoluteUniversePosition selfAup))
                return;

            if (AbsoluteUniversePosition.DistanceSq(in playerAup, in selfAup) > FeedingObservationRadiusMetersSqr)
            {
                return;
            }

            _nextBestiaryObservationTime = _cognitionTimeSeconds + FeedingObservationCooldownSeconds;
            float3 selfRuntime = selfAup.ToRuntimeFloat3();
            Vector3 selfPosition = new Vector3(selfRuntime.x, selfRuntime.y, selfRuntime.z);
            ScanEvents.TryRaiseFaunaFeedingObserved(_cachedScanEntryHash, selfPosition);
        }

        private int ResolveDeterministicTickStaggerShift()
        {
            return SimulationBucketMath.ResolveBucket(
                ResolveStableFaunaHash(FaunaTickStaggerHashSalt, 0u),
                SimulationBucketConstants.StandardSlowBucketMask);
        }

        private float ResolveDeterministicEggCooldownJitter()
        {
            uint phase = ResolveStableFaunaHash(FaunaEggJitterHashSalt, _eggClutchSequence);
            return 0.75f + ((phase & 1023u) * 0.000488758553f);
        }

        private uint ResolveStableFaunaHash(uint salt, uint sequence)
        {
            int speciesId = ComputeStableSpeciesId();
            uint ownerId = _uniqueInstanceUid != 0u
                ? _uniqueInstanceUid
                : unchecked((uint)EntityId.ToULong(GetEntityId()));
            uint hash = math.hash(new uint4(ownerId, unchecked((uint)speciesId), sequence, salt));
            return hash == 0u ? 1u : hash;
        }

        private static Vector3 ResolveDeathSpiralAxis(int phase)
        {
            switch (phase & 7)
            {
                case 0:
                    return Vector3.right;
                case 1:
                    return Vector3.left;
                case 2:
                    return Vector3.forward;
                case 3:
                    return Vector3.back;
                case 4:
                    return new Vector3(1f, 0.25f, 1f);
                case 5:
                    return new Vector3(-1f, 0.25f, 1f);
                case 6:
                    return new Vector3(1f, -0.25f, -1f);
                default:
                    return new Vector3(-1f, -0.25f, -1f);
            }
        }
    }
}

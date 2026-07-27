// ============================================================================
// HECTON-8 Ã¢â‚¬â€ HectonPlayerMovement.cs  v7.0
// Rigidbody-based hybrid player movement Ã¢â‚¬â€ FULL IMMERSION BUILD
//
// v7.0 ADDITIONS:
//   Ã¢â‚¬Â¢ Depth calculation + feeding to CameraJuiceInput
//   Ã¢â‚¬Â¢ Depth-based swim slowdown (pressure resistance)
//   Ã¢â‚¬Â¢ Collision camera shake via kinematic impact signals
//   Ã¢â‚¬Â¢ Splash / submerge events exposed as pollable properties
//   Ã¢â‚¬Â¢ FOV offset applied from CameraJuiceProcessor
//   Ã¢â‚¬Â¢ Visual pitch inertia fed through juice processor
//   Ã¢â‚¬Â¢ Exhale event exposed
//   Ã¢â‚¬Â¢ New diagnostic fields for depth, FOV, splash, exhale
//
// v6.3 PRESERVED:
//   Ã¢â‚¬Â¢ Crest dynamic height, smoothed immersion, single GroundCheck
//   Ã¢â‚¬Â¢ Surface lock, graduated gravity, ground snap, mode detection
//   Ã¢â‚¬Â¢ Zero-rotation Rigidbody, zero-jitter camera
// ============================================================================

using System;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Interaction;
using Hecton8.UI;
using Hecton8.Meta;
using Hecton8.Tools;
using Hecton8.Visor;
using Hecton8.World;
using Hecton8.Inventory;
using Hecton.Localization;
using NASAPunk.Visor;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;
using BuoyancyObject = Hecton8.Physics.BuoyancyObject;
using HectonPhysicsContract = Hecton8.Core.Contracts.HectonPhysicsContract;
using PhysicsOceanKinematics = Hecton8.Core.Contracts.IHectonOceanKinematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HectonPlayerMovement : MonoBehaviour, IUpdatable, IFixedTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener, ISargassumGlobalDragEventListener, ISonarPingEventListener, IInitializable, IGlobalRegistryHotSwapListener, IPlayerMovementContracts, IPlayerKinematicsMovementRuntime, IPlayerCuttingTensionService, IPlayerHypoxiaPresentationSink, IBootstrapProductionPlayerMovementAuthority
    {
        private int _signalPushDropCount;
        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct CinematicFocusTelemetryEntry
        {
            [FieldOffset(0)]
            public long PlayerGridX;
            [FieldOffset(8)]
            public long PlayerGridY;
            [FieldOffset(16)]
            public long PlayerGridZ;
            [FieldOffset(24)]
            public long TargetGridX;
            [FieldOffset(32)]
            public long TargetGridY;
            [FieldOffset(40)]
            public long TargetGridZ;
            [FieldOffset(48)]
            public float3 TargetDirection;
            [FieldOffset(60)]
            public float DistanceSq;
            [FieldOffset(64)]
            public float PullWeight;
            [FieldOffset(68)]
            public float SubtitleAlpha01;
            [FieldOffset(72)]
            public uint Frame;
            [FieldOffset(76)]
            public uint FocusHash;
            [FieldOffset(80)]
            public byte Flags;
            [FieldOffset(81)]
            private byte _pad0;
            [FieldOffset(82)]
            private ushort _pad1;
            [FieldOffset(84)]
            private uint _pad2;
            [FieldOffset(88)]
            private ulong _pad3;
        }

        private struct PresentationAudioEvent
        {
            public AudioClip Clip;
            public Vector3 Position;
            public float Volume;
            public float Pitch;
            public byte IsStatic2D;
        }

        public struct PlayerMovementSurfaceHit
        {
            public Vector3 point;
            public Vector3 normal;
            public float distance;
            public Collider collider;
            public int ColliderInstanceId;
        }

        private const float GroundCheckSkin = 0.02f;
        private static readonly ProfilerMarker _tickProfilerMarker = new ProfilerMarker("H8.PlayerMovement.Tick");
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.PlayerMovement.FixedTick");
        private static readonly ProfilerMarker _waterLevelResolveProfilerMarker = new ProfilerMarker("H8.Player.ResolveWaterLevel");
        private static readonly int _kccNanErrorCode = unchecked((int)LocHash.Compute("NAN_ERROR_HASH_KCC"));
        private const float InventoryLoadMinimumMovementMultiplier = 0.5f;
        private const float InventoryUpwardSwimMinimumMultiplier = 0.6f;
        private const float CriticalEncumbranceRatio = 1.5f;
        private const float CriticalStaminaFailureThreshold01 = 0.1f;
        private const float CriticalStaminaFailureDurationSeconds = 2f;
        private const float HeavyInventoryItemMassThresholdKg = 12f;
        private const float HeavyInventoryDragPerMaskBit = 0.08f;
        private const float InventoryLoadDragMultiplierMax = 0.35f;
        private const int LastValidAupRingCapacity = 16;
        private const int CinematicFocusBlackBoxCapacity = 300;
        private const int CinematicFocusSignalDrainBudget = 4;
        private const uint KccVelocityMovementMaxAgeFrames = 12u;
        private const int CinematicFocusBlackBoxDumpCooldownFrames = 120;
        private const float CinematicFocusDefaultFadeDistanceSq = 1600f;
        private const float CinematicFocusAmbientDuckingDb = -1.9382f;
        private const float MovementAcousticMinVelocitySq = 0.0001f;
        private const float MovementAcousticVolumeScale = 0.04f;
        private const float MovementStaminaDrainMultiplier = 0.15f;
        private static readonly uint _playerKinematicsSourceId = unchecked((uint)LocHash.Compute("PLAYER_KINEMATICS"));
        private static readonly uint _playerKinematicsNaNHash = unchecked((uint)LocHash.Compute("PLAYER_KINEMATICS_NAN"));
        private static readonly uint _playerKinematicsNoClipHash = unchecked((uint)LocHash.Compute("PLAYER_KINEMATICS_NOCLIP"));
        private static readonly uint _cinematicFocusTelemetryHash = unchecked((uint)LocHash.Compute("CINEMATIC_FOCUS_ACTIVE_HASH"));
        private static readonly uint _cinematicFocusFaultHash = unchecked((uint)LocHash.Compute("CINEMATIC_FOCUS_FAULT"));
        private static readonly uint _cinematicFocusDumpHash = unchecked((uint)LocHash.Compute("CINEMATIC_FOCUS_DUMP"));
        private static readonly uint _fatalPressureImplosionSourceHash = unchecked((uint)LocHash.Compute("HectonPlayerMovement.FatalPressureImplosion"));
        private static readonly uint _fatalPressureNotificationMissWarningHash = unchecked((uint)LocHash.Compute("HectonPlayerMovement.FatalPressureNotificationMiss"));
        private static readonly uint _fatalPressureNotificationContextHash = unchecked((uint)LocHash.Compute("HectonPlayerMovement.FatalPressureNotification"));
        private static ulong s_heavyInventoryDragMask;
        private static int s_heavyInventoryDragTemplateCount = -1;
        private static uint s_heavyInventoryDragRegistryRevision;
        private const float MovementProbeCachePositionEpsilonSq = 0.000001f;
        private const float MovementProbeCacheScalarEpsilon = 0.0001f;
        private const float CinematicCenterSupportNormalY = 0.92f;
        private const float ReusableGroundProbeMinNormalY = 0.05f;
        private const float ReferenceSeaWaterDensityKgPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float SpeculativeCcdImpulseThresholdMetersPerSecond = 20f;
        private const float SpeculativeCcdImpulseThresholdMetersPerSecondSq =
            SpeculativeCcdImpulseThresholdMetersPerSecond * SpeculativeCcdImpulseThresholdMetersPerSecond;
        private const float HydrostaticExitMassReferenceKg = 80f;
        private const float HydrostaticExitUpwardDampingMax = 0.65f;
        private const float HydrostaticExitDownwardVelocityKick = 1.35f;
        private const int SpeculativeHoverFixedTicksAfterAupShift = 1;
        private const float SpeculativeHoverBaseHeightMeters = 0.025f;
        private const float SpeculativeHoverBobbingFrequency = 0.5f;
        private const float SpeculativeHoverBobbingAmplitude = 0.00625f;
        private const int LegacyCollisionContactCapacity = 16;
        private const int KinematicContactMaxAgeFrames = 2;
        private const int KinematicContactColliderInstanceId = -2147483647;
        private float _runtimeSwimSpeedMultiplier = 1f;
        private float _runtimeVoxelBackpressureSwimSpeedMultiplier = 1f;
        private float _runtimeInjurySwimSpeedMultiplier = 1f;
        private float _runtimeEmergencyMovementMultiplier = 1f;
        private float _runtimeStaminaMultiplier = 1f;
        private float _criticalStaminaFailureTimer;
        private float _runtimeInventoryLoadMovementMultiplier = 1f;
        private float _runtimeInventoryUpwardSwimMultiplier = 1f;
        private float _runtimeInventoryLoad01;
        private float _runtimeInventoryLoadRatio;
        private float _runtimeInventoryTotalMassKg;
        private float _authoritativeBodyMassKg = HydrostaticExitMassReferenceKg;
        private const float TwoPi = 2f * math.PI;
        private const int DegreeSinCosLutBits = 10;
        private const int DegreeSinCosLutSize = 1 << DegreeSinCosLutBits;
        private const int DegreeSinCosLutMask = DegreeSinCosLutSize - 1;
        private const float DegreeSinCosLutScale = DegreeSinCosLutSize / 360f;
        private const float InvTwoPi = 0.15915494309189535f;
        private static readonly float[] _degreeSinLut = new float[DegreeSinCosLutSize]; // COLD ALLOC: float[1024] — hot-path degree sine LUT — owner: HectonPlayerMovement
        private static readonly float[] _degreeCosLut = new float[DegreeSinCosLutSize]; // COLD ALLOC: float[1024] — hot-path degree cosine LUT — owner: HectonPlayerMovement
        private static bool _degreeSinCosLutInitialized;
        private const float LocalGravityOverrideBlendSeconds = 1f;
        private const float VrComfortGravityTransitionSeconds = 1f;
        private const float VrComfortGravityTransitionTargetEpsilon = 0.015f;
        private const float VrHorizonLockReturnSeconds = 0.5f;
        private const float MinLocalGravitySqr = 0.000001f;
        private const float LocalGravityRetargetEpsilonSqr = 0.0001f;
        private const float VrComfortShaderPublishEpsilon = 0.0001f;
        private const float VrComfortMinimumFrameRateHz = 60f;
        private const float VrComfortTelemetryStep01 = 0.05f;
        private const uint VrComfortTelemetryContextHash = 0x56524346u; // VRCF
        private const uint VrComfortMaxVignetteHash = 0x4D565231u; // MVR1
        private const int MaxPendingPresentationAudioEvents = 8;
        private const int MaxPendingImpactBubbleCount = 256;
        private static readonly int VrComfortSignalsId = Shader.PropertyToID("_HectonVrComfortSignals");
        private static readonly int VrComfortSwayId = Shader.PropertyToID("_HectonVrComfortSway");
        private static readonly int VrComfortMotionId = Shader.PropertyToID("_HectonVrComfortMotion");
        private static readonly int VrComfortVignette01Id = Shader.PropertyToID("_VRComfortVignette01");
        private static readonly int BrineHeightYId = Shader.PropertyToID("_BrineHeightY");
        private static readonly int BrineColorId = Shader.PropertyToID("_BrineColor");
        private static readonly int BrineFogHardClipId = Shader.PropertyToID("_BrineFogHardClip");
        private const int CrestBodySampleCount = 5;
        private const int CrestSampleCenter = 0;
        private const int CrestSampleHead = 1;
        private const int CrestSampleFeet = 2;
        private const int CrestSampleLeft = 3;
        private const int CrestSampleRight = 4;
        private const float DefaultWaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private static readonly string[] _locomotionModeLabels =
        {
            "DryGroundWalk",
            "DryInteriorWalk",
            "ShallowWadeWalk",
            "SurfaceSwim",
            "UnderwaterSwim",
            "ExosuitLocomotion"
        }; // COLD ALLOC: string[6] Ã¢â‚¬â€ editor diagnostics labels Ã¢â‚¬â€ owner: HectonPlayerMovement

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ REFERENCES
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ References Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private SuitData currentSuitData;
        [SerializeField] private ControlScheme controlScheme;
        [SerializeField] private bool leanIntoTurn = true;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ WATER CONFIGURATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Water Configuration Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Fallback water surface Y when Crest is unavailable.")]
        [SerializeField] private float waterSurfaceY = DefaultWaterSurfaceY;

        [SerializeField] private float playerHeight = 1.8f;

        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Immersion ratio above which player switches from walking to swimming.")]
        private float swimTransitionThreshold = 0.7f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Surface Swim Realism Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Depth band near the waterline treated as surface swim instead of deep 3D swim.")]
        [SerializeField, Range(0.1f, 2.5f)] private float surfaceSwimDepthBand = 0.85f;
        [Tooltip("How strongly forward swim is flattened near the surface. 1 = strongly planar.")]
        [SerializeField, Range(0f, 1f)] private float surfaceForwardPitchSuppression = 0.85f;
        [Tooltip("Forward swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceForwardForceMultiplier = 0.82f;
        [Tooltip("Strafe swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceStrafeForceMultiplier = 0.72f;
        [Tooltip("Vertical swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceVerticalForceMultiplier = 0.4f;
        [Tooltip("Extra drag applied while surface swimming.")]
        [SerializeField, Range(1f, 3f)] private float surfaceDragMultiplier = 1.35f;
        [Tooltip("Max speed multiplier while surface swimming.")]
        [SerializeField, Range(0.2f, 1f)] private float surfaceMaxSpeedMultiplier = 0.72f;
        [Tooltip("Depth window where upward surface escape is strongly damped.")]
        [SerializeField, Range(0.02f, 0.6f)] private float surfaceAscendReleaseDepth = 0.18f;
        [Tooltip("Damping applied to upward velocity at the top of the water.")]
        [SerializeField, Range(0f, 20f)] private float surfaceAscendVelocityDamping = 5f;
        [Tooltip("Minimum camera look-down angle below the horizon that counts as deliberate surface dive intent. Runtime never allows values below 30 degrees.")]
        [SerializeField, Range(0f, 85f)] private float surfaceDivePitchCommit = 30f;
        [Tooltip("Minimum forward input that counts as deliberate surface dive intent.")]
        [SerializeField, Range(0f, 1f)] private float surfaceDiveForwardCommit = 0.35f;
        [Tooltip("How long deliberate dive input must stay committed before SurfaceSwim releases into full underwater motion.")]
        [SerializeField, Range(0f, 0.35f)] private float surfaceDiveCommitHoldTime = 0.12f;
        [Tooltip("How long a committed surface dive keeps the player out of surface-lock locomotion.")]
        [SerializeField, Range(0.04f, 0.5f)] private float surfaceDiveAssistDuration = 0.18f;
        [Tooltip("Extra downward swim-force multiplier applied while breaking through the surface into a dive.")]
        [SerializeField, Range(0f, 3f)] private float surfaceDiveAssistForceMultiplier = 1.15f;
        [Tooltip("World-space Y offset applied to the player root while SurfaceSwim sticks to the sampled wave height.")]
        [SerializeField, Range(-2f, 0.5f)] private float surfaceStickOffset = -0.62f;
        [Tooltip("How quickly surface snap blends in when entering SurfaceSwim.")]
        [SerializeField, Range(1f, 30f)] private float surfaceSnapEngageSpeed = 18f;
        [Tooltip("How quickly surface snap blends out when leaving SurfaceSwim.")]
        [SerializeField, Range(1f, 30f)] private float surfaceSnapReleaseSpeed = 12f;
        [Tooltip("How quickly the snapped player root follows Crest wave height while surface swimming.")]
        [SerializeField, Range(1f, 40f)] private float surfaceWaveFollowSharpness = 20f;
        [Tooltip("How deep below the surface the head must be before a deliberate dive unlocks free 3D swim.")]
        [SerializeField, Range(0.05f, 1.5f)] private float surfaceDiveBreakDepth = 0.28f;
        [Tooltip("How close the head must get to the surface to snap back into SurfaceSwim while ascending.")]
        [SerializeField, Range(0f, 0.25f)] private float surfaceHeadReattachDepth = 0.05f;
        [Tooltip("Upward velocity above which crossing the waterline keeps SurfaceSwim reattach disabled so dolphin breaches can clear the surface.")]
        [SerializeField, Range(0.5f, 12f)] private float surfaceBreachReleaseVelocity = 3.25f;
        [Tooltip("How long a fast upward breach keeps SurfaceSwim reattach disabled.")]
        [SerializeField, Range(0.05f, 0.6f)] private float surfaceBreachLockDuration = 0.24f;
        [Tooltip("Root upward speed required to punch through the waterline into a ballistic breach.")]
        [SerializeField, Range(0.5f, 20f)] private float surfaceBreachArcVelocity = 5f;
        [Tooltip("How long fluid drag is suppressed after a high-speed upward waterline breach.")]
        [SerializeField, Range(0.05f, 1.25f)] private float surfaceBreachFluidDragBypassDuration = 0.8f;
        [Tooltip("Delay before the heavy post-breach gravity spike starts after a fast upward waterline exit.")]
        [SerializeField, Range(0f, 1.5f)] private float surfaceBreachGravitySpikeDelay = 0.5f;
        [Tooltip("Downward acceleration injected after the breach delay so the suit crashes back into the surface.")]
        [SerializeField, Range(0f, 80f)] private float surfaceBreachGravitySpikeAcceleration = 32f;
        [Tooltip("How long the heavy breach gravity spike remains active after the delay expires.")]
        [SerializeField, Range(0.05f, 1f)] private float surfaceBreachGravitySpikeDuration = 0.8f;
        [Tooltip("Kinetic-energy multiplier sent to the splash feedback signal lane for high-speed upward breaches.")]
        [SerializeField, Range(1f, 12f)] private float surfaceBreachSplashEnergyScale = 4f;
        [Tooltip("Extra damping applied against downward velocity while breaking through the surface.")]
        [SerializeField, Range(0f, 20f)] private float surfaceDiveResistanceDamping = 4.5f;
        [Tooltip("How much Crest's sampled vertical surface velocity influences the player while surface-sticking.")]
        [SerializeField, Range(0f, 1f)] private float surfaceWaveVelocityInfluence = 0.75f;
        [Tooltip("Feet depth below the water surface at which grounded shoreline contact can hand off from swimming to walking.")]
        [SerializeField, Range(0.05f, 2f)] private float shoreWalkFootDepth = 1.05f;
        [Tooltip("Feet-to-bottom clearance where shoreline buoyancy has fully recovered from shallow-bottom interference.")]
        [SerializeField, Range(0.1f, 3f)] private float shoreBuoyancyRecoveryClearance = 1.35f;
        [Tooltip("How quickly shoreline buoyancy fades out near bottom contact and recovers in deeper water.")]
        [SerializeField, Range(1f, 30f)] private float shoreBuoyancyBlendSharpness = 10f;
        [Tooltip("Smoothed shoreline buoyancy blend below which shallow grounded contact hands control back to walking.")]
        [SerializeField, Range(0f, 1f)] private float shoreWalkHandoffBuoyancyThreshold = 0.42f;
        [Tooltip("Minimum downward speed required to trigger the heavy water-entry damping burst.")]
        [SerializeField, Range(0f, 20f)] private float waterEntryImpactMinSpeed = 5f;
        [Tooltip("Peak extra linear damping applied after an air-to-water impact.")]
        [SerializeField, Range(0f, 30f)] private float waterEntryImpactDamping = 14f;
        [Tooltip("How long the heavy water-entry damping burst lasts after an air-to-water impact.")]
        [SerializeField, Range(0.1f, 1.25f)] private float waterEntryImpactDuration = 0.7f;
        [Tooltip("Positive FOV kick applied on hard water entry before the rebound compression.")]
        [SerializeField, Range(0f, 15f)] private float waterEntryImpactFovExpand = 4.5f;
        [Tooltip("Negative FOV rebound applied after the initial water-entry expansion.")]
        [SerializeField, Range(0f, 12f)] private float waterEntryImpactFovCompress = 2.1f;
        [Tooltip("Minimum vertical speed required before a surface-pierce splash one-shot is played.")]
        [SerializeField, Range(0f, 20f)] private float surfacePierceSplashMinSpeed = 3.5f;
        [Tooltip("Vertical speed where surface-pierce splash volume reaches maximum.")]
        [SerializeField, Range(0.5f, 25f)] private float surfacePierceSplashMaxSpeed = 10f;
        [Tooltip("Minimum 3D splash volume at the playback threshold.")]
        [SerializeField, Range(0f, 1f)] private float surfacePierceSplashMinVolume = 0.45f;
        [Tooltip("Maximum 3D splash volume for the fastest surface-pierce events.")]
        [SerializeField, Range(0f, 1f)] private float surfacePierceSplashMaxVolume = 1f;
        [Tooltip("How quickly controller-level wet-lens intensity decays back toward dry after a pulse.")]
        [SerializeField, Range(0.25f, 10f)] private float wetLensSignalRecoverySpeed = 1.6f;
        [Tooltip("Storm intensity threshold before crest-over-camera can emit a wet-lens pulse.")]
        [SerializeField, Range(0f, 1f)] private float wetLensStormIntensityThreshold = 0.4f;
        [Tooltip("How far the water surface must overtake the camera before a storm crest counts as a wet-lens hit.")]
        [SerializeField, Range(0f, 0.25f)] private float wetLensWaveCoverDepth = 0.035f;
        [Tooltip("Cooldown between automatic storm-driven wet-lens pulses so rough water does not spam consumers.")]
        [SerializeField, Range(0.05f, 1f)] private float wetLensStormPulseCooldown = 0.18f;
        [Tooltip("Base wet-lens pulse intensity emitted when storm swell overtakes the camera at the surface.")]
        [SerializeField, Range(0f, 1f)] private float wetLensStormPulseIntensity = 0.24f;
        [Tooltip("Base wet-lens pulse intensity emitted on fast upward dolphin breaches.")]
        [SerializeField, Range(0f, 1f)] private float wetLensBreachPulseIntensity = 0.82f;

        [Header("â”€â”€ Surf Zone Extremes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Storm intensity threshold before shoreline backwash can resolve into a real undertow pull.")]
        [SerializeField, Range(0f, 1f)] private float shoreUndertowStormThreshold = 0.32f;
        [Tooltip("Depth below the surface where shoreline undertow is fully faded out.")]
        [SerializeField, Range(0.1f, 4f)] private float shoreUndertowMaxDepth = 1.7f;
        [Tooltip("Retreating water speed along the beach downslope where undertow begins to pull the player seaward.")]
        [SerializeField, Range(0.05f, 4f)] private float shoreUndertowRetreatVelocityStart = 0.35f;
        [Tooltip("Retreating water speed along the beach downslope where undertow reaches full force.")]
        [SerializeField, Range(0.1f, 6f)] private float shoreUndertowRetreatVelocityMax = 2.1f;
        [Tooltip("Base mass-scaled undertow force applied when storm backwash drags the player off the shoreline.")]
        [SerializeField, Range(0f, 320f)] private float shoreUndertowForce = 115f;
        [Tooltip("How much undertow force scales up while the player is still partly buoyant and fighting the shore handoff.")]
        [SerializeField, Range(1f, 3f)] private float shoreUndertowSurfaceBoost = 1.3f;
        [Tooltip("Feet-depth threshold below which shoreline undertow is suppressed so knee-deep water does not unrealistically drag the player offshore.")]
        [SerializeField, Range(0.05f, 1.5f)] private float shoreUndertowMinFeetDepth = 0.45f;
        [Tooltip("Feet-depth where shoreline undertow reaches full authored strength after the knee-deep suppression band.")]
        [SerializeField, Range(0.1f, 2f)] private float shoreUndertowFullFeetDepth = 1f;
        [Tooltip("How long control stays disabled after a hard wipeout impact.")]
        [SerializeField, Range(0.5f, 3f)] private float wipeoutDuration = 1.7f;
        [Tooltip("Extra damping applied while the player is recovering from a wipeout so the crash does not instantly re-stabilize.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutRecoveryDrag = 4.8f;
        [Tooltip("Impulse fired away from the impact normal when a wipeout starts.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutReboundImpulse = 6.5f;
        [Tooltip("How long velocity clamping is bypassed after a hard bailout or wipeout impulse so authored crash energy is not annihilated immediately.")]
        [SerializeField, Range(0f, 1.5f)] private float wipeoutImpulseBypassDuration = 0.22f;
        [Tooltip("Additional transport damage multiplier applied when a wipeout crash happens on an active scooter or mount.")]
        [SerializeField, Range(1f, 4f)] private float wipeoutTransportDamageScale = 1.55f;
        [Tooltip("Chance that a hard wipeout breaks one installed suit upgrade module and disables its runtime bonuses.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutSuitUpgradeBreakChance = 0.2f;
        [Tooltip("How long a fast breach exit keeps solid-land collision eligible for wipeout logic.")]
        [SerializeField, Range(0.1f, 2f)] private float wipeoutBreachLandingGraceTime = 1.15f;
        [Tooltip("Capsule inset used by the wipeout pre-sweep to avoid grazing false positives on the rider's own shell.")]
        [SerializeField, Range(0f, 0.1f)] private float wipeoutSweepCapsuleInset = 0.015f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ CREST OCEAN INTEGRATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Crest Ocean Integration Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Enable dynamic water height from Crest Ocean waves.")]
        [SerializeField] private bool useCrestOceanHeight = true;
        [Tooltip("Optional explicit ocean-kinematics provider. Scene scan prefers the active Crest adapter when no provider is assigned.")]
        [FormerlySerializedAs("crestOceanRenderer")]
        [SerializeField] private MonoBehaviour oceanKinematicsProvider;
        [Tooltip("How much Crest flow velocity is converted into passive player drift velocity.")]
        [SerializeField, Range(0f, 2f)] private float crestFlowVelocityScale = 0.35f;
        [Tooltip("How strongly Crest flow is applied as a drift force to the rigidbody.")]
        [SerializeField, Range(0f, 8f)] private float crestFlowForceResponsiveness = 1.6f;
        [Tooltip("How strongly active swim input opposing Crest flow suppresses passive drift.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowOppositionReduction = 0.82f;
        [Tooltip("How strongly cross-current swim input suppresses passive Crest drift.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowCrossCurrentReduction = 0.45f;
        [Tooltip("Fail-safe floor for Crest drift influence while the player is actively swimming.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowInputMinimumScale = 0.18f;
        [Tooltip("Blend speed for Crest drift attenuation when the player starts or stops fighting the current.")]
        [SerializeField, Range(0.5f, 20f)] private float crestFlowInputBlendSpeed = 8f;
        [Tooltip("Extra Crest drift response applied while surface swimming with no movement input so idle swimmers do not read as anchored to world space.")]
        [SerializeField, Range(1f, 4f)] private float crestFlowSurfaceIdleBoost = 1.45f;
        [Tooltip("Planar input magnitude below which the player is treated as idle for surface Crest drift.")]
        [SerializeField, Range(0f, 0.35f)] private float crestFlowIdleInputThreshold = 0.08f;
        [Tooltip("Minimum Crest wavelength used for player-body water sampling. 0 keeps full available detail.")]
        [SerializeField, Range(0f, 4f)] private float crestBodySampleMinLength = 0.4f;
        [Tooltip("Forward sample distance used for batched Crest body queries around the player.")]
        [SerializeField, Range(0.15f, 2f)] private float crestBodyForwardSampleDistance = 0.62f;
        [Tooltip("Lateral sample distance used for batched Crest body queries around the player.")]
        [SerializeField, Range(0.1f, 1.25f)] private float crestBodyLateralSampleDistance = 0.34f;
        [Tooltip("How quickly sampled wave pitch and roll settle onto the player swim presentation root.")]
        [SerializeField, Range(1f, 30f)] private float surfaceWaveAlignmentSharpness = 10f;
        [Tooltip("Maximum local pitch contributed by Crest multi-point surface alignment.")]
        [SerializeField, Range(0f, 30f)] private float surfaceWaveMaxPitch = 14f;
        [Tooltip("Maximum local roll contributed by Crest multi-point surface alignment.")]
        [SerializeField, Range(0f, 30f)] private float surfaceWaveMaxRoll = 18f;
        [Tooltip("Maximum depth below the surface where storm turbulence still meaningfully shoves the swimmer.")]
        [SerializeField, Range(1f, 15f)] private float underwaterTurbulenceMaxDepth = 10f;
        [Tooltip("Wave-height span across the player footprint where storm turbulence begins.")]
        [SerializeField, Range(0.05f, 3f)] private float underwaterTurbulenceHeightStart = 0.35f;
        [Tooltip("Wave-height span across the player footprint where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 4f)] private float underwaterTurbulenceHeightMax = 1.35f;
        [Tooltip("Horizontal Crest displacement magnitude where storm turbulence begins.")]
        [SerializeField, Range(0.05f, 3f)] private float underwaterTurbulenceDisplacementStart = 0.25f;
        [Tooltip("Horizontal Crest displacement magnitude where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 4f)] private float underwaterTurbulenceDisplacementMax = 1.2f;
        [Tooltip("Horizontal Crest water speed where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 8f)] private float underwaterTurbulenceVelocityMax = 3.2f;
        [Tooltip("Base lateral surf-zone turbulence force applied near the surface during heavy swell.")]
        [SerializeField, Range(0f, 300f)] private float underwaterTurbulenceForce = 95f;
        [Tooltip("Base vertical shove applied by storm turbulence under breaking waves.")]
        [SerializeField, Range(0f, 220f)] private float underwaterTurbulenceVerticalForce = 58f;
        [Tooltip("Oscillation frequency used to keep storm turbulence alive instead of static.")]
        [SerializeField, Range(0.1f, 6f)] private float underwaterTurbulenceFrequency = 1.55f;
        [Tooltip("Maximum additive pitch sent to swim presentation while surf turbulence throws the player around.")]
        [SerializeField, Range(0f, 18f)] private float underwaterTurbulencePitch = 5.5f;
        [Tooltip("Maximum additive roll sent to swim presentation while surf turbulence throws the player around.")]
        [SerializeField, Range(0f, 20f)] private float underwaterTurbulenceRoll = 8.5f;
        [Tooltip("How quickly turbulence pose settles toward the currently sampled storm target.")]
        [SerializeField, Range(1f, 30f)] private float underwaterTurbulencePoseSharpness = 9f;
        [Tooltip("Bottom clearance where surf-zone turbulence stops receiving shallow-bottom amplification.")]
        [SerializeField, Range(0.1f, 8f)] private float underwaterTurbulenceBottomInfluenceDepth = 2.6f;
        [Tooltip("Maximum extra turbulence multiplier applied when storm water is forced through shallow bottom clearance.")]
        [SerializeField, Range(1f, 4f)] private float underwaterTurbulenceBottomBoost = 1.65f;
        [Tooltip("Normalized turbulence level where underwater disorientation visuals begin to ramp in.")]
        [SerializeField, Range(0f, 1f)] private float underwaterStressSignalThreshold = 0.28f;
        [Tooltip("How quickly the underwater disorientation signal converges toward the current turbulence target.")]
        [SerializeField, Range(1f, 30f)] private float underwaterStressSignalBlendSharpness = 8f;
        [Tooltip("Depth below the surface where near-surface transport cavitation begins to fade out.")]
        [SerializeField, Range(0.1f, 2f)] private float transportCavitationStartDepth = 1f;
        [Tooltip("Depth below the surface where near-surface transport cavitation is fully recovered.")]
        [SerializeField, Range(0.2f, 3f)] private float transportCavitationRecoveryDepth = 1.5f;
        [Tooltip("Forward acceleration where near-surface transport cavitation starts reducing thrust efficiency.")]
        [SerializeField, Range(0f, 20f)] private float transportCavitationAccelerationStart = 2.5f;
        [Tooltip("Forward acceleration where near-surface transport cavitation reaches full authored loss.")]
        [SerializeField, Range(0.1f, 30f)] private float transportCavitationAccelerationMax = 11f;
        [Tooltip("Minimum propulsion efficiency retained during severe near-surface cavitation.")]
        [SerializeField, Range(0.05f, 1f)] private float transportCavitationMinEfficiency = 0.42f;
        [Tooltip("How quickly near-surface transport cavitation converges toward the current efficiency target.")]
        [SerializeField, Range(1f, 30f)] private float transportCavitationBlendSharpness = 10f;
        [Header("Dynamic Collision Deformation")]
        [Tooltip("How quickly the physical capsule converges toward the current wave-driven tuck target.")]
        [SerializeField, Range(1f, 30f)] private float dynamicCollisionDeformationBlendSharpness = 11f;
        [Tooltip("Downhill wave slope where dynamic capsule tuck reaches full authored strength.")]
        [SerializeField, Range(0.01f, 1f)] private float dynamicCollisionTuckSlopeForFull = 0.32f;
        [Tooltip("Immersion depth below the surface where wave-driven collision tuck reaches full influence.")]
        [SerializeField, Range(0.05f, 2f)] private float dynamicCollisionImmersionDepthForFull = 0.52f;
        [Tooltip("Minimum capsule height scale while the swimmer fully tucks on a steep descending wave face.")]
        [SerializeField, Range(0.3f, 1f)] private float dynamicCollisionMinHeightScale = 0.58f;
        [Tooltip("Maximum capsule radius scale while the swimmer fully tucks on a steep descending wave face.")]
        [SerializeField, Range(1f, 2f)] private float dynamicCollisionMaxRadiusScale = 1.32f;
        [Tooltip("Center offset applied while the swimmer tucks so the collider collapses downward toward a compact ball.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float dynamicCollisionCenterYOffset = -0.14f;
        [Header("Active Trauma Collision")]
        [Tooltip("How long active-trauma collision inflation stays armed before the collider starts recovering.")]
        [SerializeField, Range(0.02f, 1f)] private float physicalTraumaCollisionHoldTime = 0.24f;
        [Tooltip("How quickly active-trauma collision recovery settles back toward the baseline capsule.")]
        [SerializeField, Range(1f, 30f)] private float physicalTraumaCollisionRecoverySharpness = 9f;
        [Tooltip("Additional radius scale applied while active trauma keeps the body in a defensive tucked collision state.")]
        [SerializeField, Range(1f, 2f)] private float physicalTraumaCollisionRadiusScale = 1.18f;
        [Tooltip("Additional height scale applied while active trauma compresses the body away from nearby geometry.")]
        [SerializeField, Range(0.3f, 1f)] private float physicalTraumaCollisionHeightScale = 0.76f;
        [Tooltip("Additional downward center offset applied during active trauma so the collision capsule protects the bent torso.")]
        [SerializeField, Range(-0.5f, 0.2f)] private float physicalTraumaCollisionCenterYOffset = -0.1f;
        [Header("Abyssal Currents")]
        [Tooltip("Depth where abyssal downdrafts start arming below otherwise stable underwater swim.")]
        [SerializeField, Range(50f, 500f)] private float abyssalCurrentStartDepth = 100f;
        [Tooltip("Depth where abyssal downdrafts reach full authored strength.")]
        [SerializeField, Range(60f, 800f)] private float abyssalCurrentFullDepth = 220f;
        [Tooltip("Longest downtime between abyssal downdraft pulses near the arming depth.")]
        [SerializeField, Range(0.5f, 12f)] private float abyssalDowndraftIntervalMax = 4.8f;
        [Tooltip("Shortest downtime between abyssal downdraft pulses in the deepest armed water.")]
        [SerializeField, Range(0.15f, 8f)] private float abyssalDowndraftIntervalMin = 1.65f;
        [Tooltip("Minimum downward velocity-change applied by an abyssal downdraft pulse.")]
        [SerializeField, Range(0.1f, 8f)] private float abyssalDowndraftVelocityChangeMin = 1.65f;
        [Tooltip("Maximum downward velocity-change applied by an abyssal downdraft pulse.")]
        [SerializeField, Range(0.2f, 14f)] private float abyssalDowndraftVelocityChangeMax = 4.9f;
        [Tooltip("How much sampled biome/current flow can tilt an abyssal downdraft away from pure vertical.")]
        [SerializeField, Range(0f, 1f)] private float abyssalDowndraftHorizontalBias = 0.3f;
        [Tooltip("How long a downdraft pulse keeps draining energy while the player fights upward against it.")]
        [SerializeField, Range(0.1f, 2f)] private float abyssalDowndraftAftershockDuration = 0.85f;
        [Tooltip("Energy drained per second while the player actively resists an active abyssal downdraft.")]
        [SerializeField, Range(0f, 20f)] private float abyssalDowndraftCounterEnergyDrain = 5.2f;
        [Tooltip("Minimum suction-flow speed required before abyssal cave currents can overload transport or jump-jet energy drain.")]
        [SerializeField, Range(0f, 8f)] private float abyssalCounterDriveFlowThreshold = 0.85f;
        [Tooltip("Minimum opposition angle between suction and commanded thrust before the drive is considered to be fighting the cave current head-on.")]
        [SerializeField, Range(90f, 180f)] private float abyssalCounterDriveOppositionAngleDegrees = 125f;
        [Tooltip("Energy-drain multiplier applied when the player is driving transport or jump jets directly against an abyssal suction current.")]
        [SerializeField, Range(1f, 4f)] private float abyssalCounterDriveEnergyOverstrainMultiplier = 2f;
        [Tooltip("Maximum swim-speed ceiling retained when the player drives directly against the abyssal flow.")]
        [SerializeField, Range(0.2f, 1f)] private float abyssalCurrentShearMaxSpeedMultiplier = 0.5f;
        [Tooltip("Exponent used for oxygen/energy overstrain while swimming against abyssal flow.")]
        [SerializeField, Range(1f, 6f)] private float abyssalCurrentShearDrainExponent = 2f;
        [Tooltip("Additional oxygen drain per second at full abyssal current shear.")]
        [SerializeField, Range(0f, 25f)] private float abyssalCurrentShearOxygenDrainPerSecond = 3f;
        [Tooltip("Additional suit-energy drain per second at full abyssal current shear.")]
        [SerializeField, Range(0f, 25f)] private float abyssalCurrentShearEnergyDrainPerSecond = 4f;
        [Tooltip("Minimum noisy-flow delta treated as crossing a turbulent abyssal-current seam.")]
        [SerializeField, Range(0.05f, 8f)] private float abyssalFlowNoiseBoundaryThreshold = 1.1f;
        [Tooltip("VelocityChange applied to the rider when the scooter crosses a turbulent abyssal-current seam.")]
        [SerializeField, Range(0f, 6f)] private float abyssalFlowNoiseBoundaryVelocityChange = 0.9f;
        [Tooltip("Shortest time between abyssal turbulence seam hits so noisy currents read violent without degenerating into audio-camera spam.")]
        [SerializeField, Range(0.02f, 1f)] private float abyssalFlowNoiseBoundaryCooldown = 0.18f;
        [Tooltip("How slowly the rider's KCC velocity accepts sampled AbyssalFlowField advection.")]
        [SerializeField, Range(0.1f, 12f)] private float abyssalFlowAdvectionSharpness = 2.6f;
        [Tooltip("Signed camera-roll impulse fired when abyssal-current turbulence slams the rider across a noisy seam.")]
        [SerializeField, Range(0f, 12f)] private float abyssalFlowNoiseBoundaryRollImpulse = 2.8f;
        [Tooltip("Velocity-change torque injected into active transport control when abyssal turbulence snaps across a noisy seam.")]
        [SerializeField, Range(0f, 8f)] private float abyssalTransportTurbulenceTorqueVelocityChange = 0.7f;
        [Tooltip("Maximum temporary pitch deviation injected into transport thrust by abyssal turbulence seam hits.")]
        [SerializeField, Range(0f, 12f)] private float abyssalTransportTurbulencePitchDegrees = 2.8f;
        [Tooltip("Maximum temporary yaw deviation injected into transport thrust by abyssal turbulence seam hits.")]
        [SerializeField, Range(0f, 16f)] private float abyssalTransportTurbulenceYawDegrees = 5.5f;
        [Tooltip("How quickly abyssal turbulence steering offsets decay back to neutral once the seam hit passes.")]
        [SerializeField, Range(1f, 20f)] private float abyssalTransportTurbulenceRecoverySharpness = 8f;
        [Header("Crush Depth")]
        [Tooltip("Depth where hull stress starts accumulating from abyssal pressure and rapid depth changes.")]
        [SerializeField, Range(500f, 3000f)] private float crushDepthStart = 1000f;
        [Tooltip("Depth where hull stress reaches full authored strength before fatal overload.")]
        [SerializeField, Range(700f, 5000f)] private float crushDepthFullDepth = 1450f;
        [Tooltip("Vertical speed where rapid depth change contributes full extra hull stress.")]
        [SerializeField, Range(0.5f, 30f)] private float crushDepthRateForFullStress = 9f;
        [Tooltip("How quickly hull stress chases the current depth/rate target.")]
        [SerializeField, Range(0.5f, 20f)] private float crushDepthStressBlendSharpness = 3.2f;
        [Tooltip("Additional swim drag applied at full hull stress. This sells pressure as viscous resistance without corrupting rigidbody mass.")]
        [SerializeField, Range(1f, 4f)] private float crushDepthDragMultiplier = 1.55f;
        [Tooltip("How much active transport yaw responsiveness is suppressed at full hull stress.")]
        [SerializeField, Range(0f, 0.9f)] private float crushDepthTurnSuppression = 0.58f;
        [Tooltip("Hull stress threshold where camera micro-vibration starts.")]
        [SerializeField, Range(0f, 1f)] private float crushDepthShakeThreshold = 0.32f;
        [Tooltip("Hull stress threshold where metal-groan one-shots start.")]
        [SerializeField, Range(0f, 1f)] private float crushDepthGroanThreshold = 0.48f;
        [Tooltip("Longest interval between hull groans near the start of dangerous stress.")]
        [SerializeField, Range(0.2f, 10f)] private float crushDepthGroanIntervalMax = 4.2f;
        [Tooltip("Shortest interval between hull groans under extreme stress.")]
        [SerializeField, Range(0.05f, 5f)] private float crushDepthGroanIntervalMin = 1.25f;
        [Tooltip("Hull stress threshold that triggers fatal implosion wipeout.")]
        [SerializeField, Range(0.5f, 1f)] private float crushDepthImplosionThreshold = 0.985f;
        [Tooltip("Delay between fatal pressure lock-on and the actual implosion wipeout. This is the pre-death glitch window.")]
        [SerializeField, Range(0.25f, 3f)] private float fatalPressureSequenceDuration = 1.5f;
        [Tooltip("Slowest cadence between visor glitch pulses during the fatal pressure loop.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchPulseIntervalMax = 0.28f;
        [Tooltip("Fastest cadence between visor glitch pulses right before implosion.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchPulseIntervalMin = 0.07f;
        [Tooltip("Shortest visor glitch pulse duration during the fatal pressure loop.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchDurationMin = 0.08f;
        [Tooltip("Longest visor glitch pulse duration right before implosion.")]
        [SerializeField, Range(0.02f, 0.75f)] private float fatalPressureGlitchDurationMax = 0.26f;
        [Tooltip("Gameplay FOV floor reached near the end of the fatal pressure squeeze.")]
        [SerializeField, Range(15f, 25f)] private float fatalPressureMinFov = 18f;
        [Tooltip("Mouse-look sensitivity floor reached near the end of the fatal pressure squeeze.")]
        [SerializeField, Range(0f, 0.35f)] private float fatalPressureLookSensitivityFloor = 0.08f;
        [Tooltip("Initial yaw freedom around the locked neck pose when the fatal pressure sequence begins.")]
        [SerializeField, Range(5f, 90f)] private float fatalPressureYawFreedomStart = 42f;
        [Tooltip("Final yaw freedom right before the implosion fires.")]
        [SerializeField, Range(1f, 25f)] private float fatalPressureYawFreedomEnd = 8f;
        [Tooltip("Initial pitch freedom around the locked neck pose when the fatal pressure sequence begins.")]
        [SerializeField, Range(5f, 60f)] private float fatalPressurePitchFreedomStart = 26f;
        [Tooltip("Final pitch freedom right before the implosion fires.")]
        [SerializeField, Range(1f, 20f)] private float fatalPressurePitchFreedomEnd = 5f;
        [Header("Thermal Updrafts")]
        [Tooltip("Minimum sampled upward current speed treated as a black-smoker thermal updraft.")]
        [SerializeField, Range(0f, 10f)] private float thermalUpdraftSpeedThreshold = 1.8f;
        [Tooltip("Upward current speed where thermal updraft reaches full authored shove.")]
        [SerializeField, Range(0.1f, 20f)] private float thermalUpdraftSpeedMax = 6.4f;
        [Tooltip("VelocityChange per second applied by a full-strength thermal updraft. Multiplied by fixed delta time before injection.")]
        [SerializeField, Range(0f, 20f)] private float thermalUpdraftVelocityChangePerSecond = 8.5f;
        [Tooltip("Minimum depth where authored upward currents are allowed to behave like abyssal thermal vents.")]
        [SerializeField, Range(0f, 5000f)] private float thermalUpdraftStartDepth = 120f;
        [Tooltip("Normalized thermal-updraft intensity that is violent enough to force an active-trauma body bend.")]
        [SerializeField, Range(0f, 1f)] private float thermalUpdraftTraumaThreshold = 0.62f;
        [Tooltip("Minimum delay between repeated thermal-updraft trauma hits so continuous vents do not spam the blend state.")]
        [SerializeField, Range(0.05f, 2f)] private float thermalUpdraftTraumaCooldown = 0.45f;
        [Header("Active Sonar")]
        [Tooltip("Cooldown between controller-owned active sonar pings.")]
        [SerializeField, Range(0.1f, 10f)] private float activeSonarPingCooldown = 2.35f;
        [Tooltip("Radius used by the controller-owned active sonar ping.")]
        [SerializeField, Range(25f, 400f)] private float activeSonarPingRadius = 200f;
        [Tooltip("How long shader/VFX consumers should keep the active sonar reveal alive after a ping.")]
        [SerializeField, Range(0.5f, 5f)] private float activeSonarRevealDuration = 2.4f;
        [Header("Vegetation Density Drag")]
        [Tooltip("Optional direct cartographer bridge ref used for per-position vegetation density queries. When absent, the controller falls back to the bridge's player-scoped global density handoff.")]
        [SerializeField] private HectonMapMagicVegetationBridge vegetationDensityBridge;
        [Tooltip("Minimum sargassum density before stem viscosity begins increasing Rigidbody linear damping.")]
        [SerializeField, Range(0f, 1f)] private float vegetationDensityDragThreshold = 0.55f;
        [Tooltip("Extra Rigidbody linear damping applied at peak dense-sargassum density.")]
        [SerializeField, Range(0f, 8f)] private float vegetationDensityLinearDampingMax = 3.2f;
        [Tooltip("How quickly dense-vegetation damping blends toward and away from the sampled density target.")]
        [SerializeField, Range(0.5f, 20f)] private float vegetationDensityLinearDampingBlendSharpness = 5.5f;
        [Header("Bailout State")]
        [Tooltip("Transport impact speed where wipeout escalates into a forced bailout.")]
        [SerializeField, Range(5f, 40f)] private float wipeoutBailoutSpeedThreshold = 25f;
        [Tooltip("Normalized transport integrity threshold below which active transport is treated as critically failed and triggers bailout.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutBailoutCriticalIntegrityThreshold = 0.08f;
        [Tooltip("Additional horizontal bailout impulse fired when the controller ejects the player from an active scooter.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutBailoutImpulse = 7.5f;
        [Tooltip("Additional upward bailout impulse fired when the controller ejects the player from an active scooter.")]
        [SerializeField, Range(0f, 15f)] private float wipeoutBailoutUpwardImpulse = 3.4f;
        [Tooltip("How long bailout disorientation keeps the visor optics smeared after emergency ejection.")]
        [SerializeField, Range(0.1f, 3f)] private float wipeoutBailoutDisorientationDuration = 1.1f;
        [Tooltip("Signed camera-roll impulse fired when the player is violently ejected from a transport.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutBailoutRollImpulse = 8.5f;
        [Tooltip("Visor distortion strength applied during the bailout disorientation window. This sells blur without inventing a second post stack.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutBailoutVisorDistortion = 0.72f;
        [Tooltip("How quickly bailout visor distortion decays back to a clean image.")]
        [SerializeField, Range(0.1f, 12f)] private float wipeoutBailoutVisorRecovery = 4.2f;
        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Environmental Drag Integration Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("How long an external ApplyEnvironmentalDrag request stays active without refresh before recovering to baseline.")]
        [SerializeField, Range(0f, 0.35f)] private float externalEnvironmentalDragHoldTime = 0.12f;
        [Tooltip("How quickly external environmental drag blends toward the requested multiplier and recovers after release.")]
        [SerializeField, Range(1f, 24f)] private float externalEnvironmentalDragBlendSpeed = 9f;
        [Tooltip("Instant analytical drag multiplier applied while the player is submerged in generated toxic brine.")]
        [SerializeField, Range(1f, 8f)] private float brineViscosityDragMultiplier = 4f;
        [Header("Parasite Latch Physics")]
        [Tooltip("How long parasite COM and harvester pull stay active without refresh before recovering to neutral.")]
        [SerializeField, Range(0f, 0.35f)] private float parasiteLatchInfluenceHoldTime = 0.14f;
        [Tooltip("How quickly parasite COM and harvester pull blend toward the latest async GPU readback sample.")]
        [SerializeField, Range(1f, 24f)] private float parasiteLatchInfluenceBlendSpeed = 8.5f;
        [Tooltip("Base force applied from the parasite center of mass when the swarm is latched to one side of the active hull.")]
        [SerializeField, Range(0f, 80f)] private float parasiteCenterOfMassForce = 18f;
        [Tooltip("Additional force applied toward the nearest DeadZone massive-structure anchor once the hive enters harvester mode.")]
        [SerializeField, Range(0f, 120f)] private float parasiteHarvesterPullForce = 42f;
        [Tooltip("Latched parasite count treated as full parasite force on the active transport hull.")]
        [SerializeField, Range(1f, 64f)] private float parasiteLatchCountForFullForce = 18f;
        [Header("Sargassum Entanglement")]
        [Tooltip("Spring force applied when the player or scooter gets snared inside dense sargassum and loses momentum.")]
        [SerializeField, Range(0f, 40f)] private float sargassumEntanglementSpring = 11.5f;
        [Tooltip("Velocity damping applied along the snare spring so the player feels stem tension instead of a raw teleport pull.")]
        [SerializeField, Range(0f, 20f)] private float sargassumEntanglementDamping = 4.8f;
        [Tooltip("Absolute acceleration cap applied to the sargassum snare spring before it reaches the rigidbody.")]
        [SerializeField, Range(0f, 80f)] private float sargassumEntanglementMaxAcceleration = 18f;
        [Tooltip("How much the snare spring is allowed to pull vertically. Lower values keep the trap mostly planar and avoid ugly bobbing.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementVerticalInfluence = 0.18f;
        [Tooltip("Body-mass reference used to keep sargassum snaring stable across light and heavy suit profiles.")]
        [SerializeField, Range(40f, 500f)] private float sargassumEntanglementMassReference = 80f;
        [Tooltip("Maximum extra environmental drag requested by full sargassum entanglement while swimming without transport.")]
        [SerializeField, Range(0f, 3f)] private float sargassumEntanglementSwimEnvironmentalDrag = 0.45f;
        [Tooltip("Maximum extra environmental drag requested by full sargassum entanglement while a transport propeller is active.")]
        [SerializeField, Range(0f, 4f)] private float sargassumEntanglementTransportEnvironmentalDrag = 1.15f;
        [Tooltip("How much active transport propulsion can shave off spring tension so the player can still build escape momentum.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementEscapeRelief = 0.48f;
        [Tooltip("Base suit-energy drain per second applied while the player is actively fighting dense sargassum entanglement.")]
        [SerializeField, Range(0f, 10f)] private float sargassumEscapeEnergyDrainPerSecond = 0.85f;
        [Tooltip("Multiplier applied to escape-drain while entanglement is active. Default 3x per design request.")]
        [SerializeField, Range(1f, 6f)] private float sargassumEntanglementEscapeEnergyMultiplier = 3f;
        [Tooltip("Minimum combined movement or propulsion intent required before escape-drain is applied.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEscapeInputThreshold = 0.2f;
        [Tooltip("Optional 3D one-shot played when the player actively strains against a sargassum snare. Falls back to the underwater impact clip when null.")]
        [SerializeField] private AudioClip sargassumEntanglementStrainClip;
        [Tooltip("Cooldown between strain one-shots so continuous struggle reads as stem tension instead of audio spam.")]
        [SerializeField, Range(0.05f, 1f)] private float sargassumEntanglementAudioCooldown = 0.24f;
        [Tooltip("Minimum normalized escape intent required before entanglement strain emits camera/audio feedback.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementStrainThreshold = 0.22f;
        [Tooltip("Scales the normalized entanglement strain before it is forwarded into the camera-shake path.")]
        [SerializeField, Range(0f, 2f)] private float sargassumEntanglementCameraShakeScale = 0.9f;
        [Tooltip("Strain level where sargassum escape escalates into hard stress. At and above this point the controller amplifies shake and resource drain.")]
        [SerializeField, Range(0f, 1f)] private float sargassumHighStrainThreshold = 0.5f;
        [Tooltip("Extra shake multiplier applied while entanglement strain stays above the hard-stress threshold.")]
        [SerializeField, Range(1f, 4f)] private float sargassumHighStrainShakeBoost = 1.75f;
        [Tooltip("Extra energy-drain multiplier applied while entanglement strain stays above the hard-stress threshold.")]
        [SerializeField, Range(1f, 6f)] private float sargassumHighStrainEnergyMultiplier = 3f;
        [Tooltip("How long high-strain stress persists without a fresh strain pulse before relaxing back to baseline.")]
        [SerializeField, Range(0f, 0.5f)] private float sargassumHighStrainHoldTime = 0.18f;
        [Header("Abyssal Cable Entanglement")]
        [Tooltip("Spring force applied when the player or scooter gets snared inside abyssal bio-cables.")]
        [SerializeField, Range(0f, 80f)] private float abyssalCableEntanglementSpring = 28f;
        [Tooltip("Velocity damping applied along the bio-cable snare direction.")]
        [SerializeField, Range(0f, 30f)] private float abyssalCableEntanglementDamping = 9.5f;
        [Tooltip("Absolute acceleration cap applied to abyssal cable snare force before it reaches the rigidbody.")]
        [SerializeField, Range(0f, 120f)] private float abyssalCableEntanglementMaxAcceleration = 26f;
        [Tooltip("How much the cable snare is allowed to pull vertically.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCableEntanglementVerticalInfluence = 0.12f;
        [Tooltip("Body-mass reference used to scale abyssal bio-cable drag without borrowing sargassum tuning.")]
        [SerializeField, Range(40f, 500f)] private float abyssalCableEntanglementMassReference = 110f;
        [Tooltip("Maximum extra environmental drag requested by full abyssal cable tension while swimming without transport.")]
        [SerializeField, Range(0f, 5f)] private float abyssalCableEntanglementSwimEnvironmentalDrag = 1.25f;
        [Tooltip("Maximum extra environmental drag requested by full abyssal cable tension while active transport is engaged.")]
        [SerializeField, Range(0f, 8f)] private float abyssalCableEntanglementTransportEnvironmentalDrag = 2.85f;
        [Tooltip("Base suit-energy drain per second applied while the player fights an active abyssal cable snare.")]
        [SerializeField, Range(0f, 20f)] private float abyssalCableEscapeEnergyDrainPerSecond = 6.2f;
        [Tooltip("Multiplier applied to cable escape drain while the snare remains mostly uncut.")]
        [SerializeField, Range(1f, 8f)] private float abyssalCableEscapeEnergyMultiplier = 4.5f;
        [Tooltip("Minimum cable cut progress required before propulsion starts buying real relief against the snare.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCableCutReleaseThreshold = 0.68f;
        [Tooltip("Maximum propulsion relief unlocked once the cable knot is substantially severed.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCablePropulsionReliefAtFullCut = 0.72f;
        [Header("Sargassum Buoyancy Support")]
        [Tooltip("Global sargassum density threshold where a floating mat starts to support the player's body weight.")]
        [SerializeField, Range(0f, 1f)] private float sargassumMatBuoyancyDensityThreshold = 0.8f;
        [Tooltip("Depth below the water surface where dense floating-mat support has fully faded out.")]
        [SerializeField, Range(0.1f, 3f)] private float sargassumMatBuoyancyMaxDepth = 1.65f;
        [Tooltip("How quickly dense floating-mat support blends in and out.")]
        [SerializeField, Range(1f, 24f)] private float sargassumMatBuoyancyBlendSharpness = 9f;
        [Tooltip("Extra upward support applied in multiples of body weight while the player lies on a dense sargassum mat.")]
        [SerializeField, Range(0f, 2.5f)] private float sargassumMatBuoyancyForceScale = 0.85f;
        [Tooltip("Extra surface-lock authority granted by a dense sargassum mat.")]
        [SerializeField, Range(1f, 3f)] private float sargassumMatSurfaceLockBoost = 1.4f;
        [Tooltip("Additional lift applied to the surface-lock target while a dense mat is carrying the player.")]
        [SerializeField, Range(0f, 0.75f)] private float sargassumMatSurfaceLiftOffset = 0.16f;

        [Header("Surface Recovery Feedback")]
        [Tooltip("Optional 2D helmet-breath one-shot played when the player breaks back into open air after staying underwater for a while.")]
        [SerializeField] private AudioClip surfaceGaspClip;
        [Tooltip("Minimum continuous head-submerged time required before surfacing can trigger the greedy gasp one-shot.")]
        [SerializeField, Range(0f, 12f)] private float surfaceGaspMinUnderwaterTime = 2.4f;
        [Tooltip("Cooldown between gasp triggers so wave chop cannot spam breath recovery feedback.")]
        [SerializeField, Range(0f, 5f)] private float surfaceGaspCooldown = 1.2f;
        [Tooltip("Head depth below Crest required before the controller treats the player as fully submerged for gasp timing.")]
        [SerializeField, Range(0f, 0.35f)] private float surfaceGaspHeadEnterDepth = 0.04f;
        [Tooltip("Head depth below Crest where the gasp submerge latch finally releases back to open air.")]
        [SerializeField, Range(0f, 0.2f)] private float surfaceGaspHeadExitDepth = 0.01f;
        [Tooltip("Helmet-breath playback volume for the greedy gasp recovery one-shot.")]
        [SerializeField, Range(0f, 1f)] private float surfaceGaspVolume = 0.82f;
        [Tooltip("Short FOV expansion applied with the gasp so surfacing after pressure feels physical instead of cosmetic.")]
        [SerializeField, Range(0f, 12f)] private float surfaceGaspFovExpand = 2.6f;
        [Tooltip("Follow-up FOV compression applied after the gasp expansion settles.")]
        [SerializeField, Range(0f, 8f)] private float surfaceGaspFovCompress = 0.85f;
        [Tooltip("Duration of the gasp FOV kick.")]
        [SerializeField, Range(0.05f, 1f)] private float surfaceGaspFovDuration = 0.34f;
        [Tooltip("Optional splash-ring particle system restarted when a recent dolphin breach slams back into the water.")]
        [SerializeField] private ParticleSystem breachSplashRingParticles;
        [Tooltip("Minimum normalized re-entry intensity before the breach splash ring is allowed to fire.")]
        [SerializeField, Range(0f, 1f)] private float breachSplashRingMinIntensity = 0.18f;

        [Header("Sargassum Bed Recovery")]
        [Tooltip("Field density threshold where a sargassum mat becomes dense enough to function as a floating resting bed.")]
        [SerializeField, Range(0f, 1f)] private float sargassumRestDensityThreshold = 0.9f;
        [Tooltip("Maximum player speed allowed before dense-mat resting recovery is suppressed.")]
        [SerializeField, Range(0.05f, 3f)] private float sargassumRestMaxSpeed = 0.4f;
        [Tooltip("Maximum input intent allowed before dense-mat resting recovery is suppressed.")]
        [SerializeField, Range(0f, 1f)] private float sargassumRestMaxInputIntent = 0.18f;
        [Tooltip("Head depth below Crest where resting recovery is fully suppressed because the player is no longer breathing above the floating mat.")]
        [SerializeField, Range(0f, 0.5f)] private float sargassumRestMaxHeadDepth = 0.03f;
        [Tooltip("How quickly dense-mat resting recovery blends in and out.")]
        [SerializeField, Range(1f, 24f)] private float sargassumRestBlendSharpness = 6f;
        [Tooltip("Additional oxygen refill per second granted while the player lies still on a dense sargassum mat at the surface.")]
        [SerializeField, Range(0f, 25f)] private float sargassumRestOxygenRestorePerSecond = 8f;
        [Tooltip("Additional energy refill per second granted while the player lies still on a dense sargassum mat at the surface.")]
        [SerializeField, Range(0f, 10f)] private float sargassumRestEnergyRestorePerSecond = 1.35f;

        [Header("Impact Feedback")]
        [Tooltip("Optional particle burst emitted when a hard underwater wipeout happens.")]
        [SerializeField] private ParticleSystem wipeoutBubbleParticles;
        [Tooltip("Optional particle burst emitted when a violent dolphin breach tears through the surface.")]
        [SerializeField] private ParticleSystem breachBubbleParticles;
        [Tooltip("Optional muffled underwater impact clip used by wipeouts and violent breaches.")]
        [SerializeField] private AudioClip underwaterImpactClip;
        [Tooltip("Minimum normalized intensity before a bubble impact burst is allowed to emit.")]
        [SerializeField, Range(0f, 1f)] private float impactBubbleMinIntensity = 0.18f;
        [Tooltip("Minimum number of particles emitted by an impact bubble burst.")]
        [SerializeField, Range(0, 64)] private int impactBubbleMinCount = 10;
        [Tooltip("Maximum number of particles emitted by an impact bubble burst.")]
        [SerializeField, Range(0, 128)] private int impactBubbleMaxCount = 32;
        [Tooltip("Minimum volume used by the muffled underwater impact one-shot.")]
        [SerializeField, Range(0f, 1f)] private float underwaterImpactMinVolume = 0.42f;
        [Tooltip("Maximum volume used by the muffled underwater impact one-shot.")]
        [SerializeField, Range(0f, 1f)] private float underwaterImpactMaxVolume = 0.88f;

        [Header("Somatic KCC Movement")]
        [Tooltip("Low-frequency underwater effort bob cadence in cycles per second.")]
        [SerializeField, Range(0.1f, 2f)] private float underwaterSomaticHeadbobFrequency = 0.68f;
        [Tooltip("Maximum pitch offset applied by underwater effort bob at full swim intent.")]
        [SerializeField, Range(0f, 3f)] private float underwaterSomaticPitchDegrees = 0.72f;
        [Tooltip("Maximum yaw offset applied by underwater effort sway at full swim intent.")]
        [SerializeField, Range(0f, 3f)] private float underwaterSomaticYawDegrees = 0.48f;
        [Tooltip("Velocity where underwater effort bob reaches full amplitude.")]
        [SerializeField, Range(0.25f, 10f)] private float underwaterSomaticReferenceSpeed = 3.4f;
        [Tooltip("Blend sharpness for underwater effort bob starting and stopping.")]
        [SerializeField, Range(0.5f, 12f)] private float underwaterSomaticResponseSharpness = 3.6f;
        [Tooltip("Suit-energy fraction where underwater fatigue starts increasing breath cadence and helmet sway.")]
        [SerializeField, Range(0.01f, 0.6f)] private float underwaterSomaticFatigueStaminaThreshold01 = 0.2f;
        [Tooltip("Underwater effort-bob cadence multiplier when stamina is critically low.")]
        [SerializeField, Range(1f, 3f)] private float underwaterSomaticFatigueCadenceMultiplier = 1.85f;
        [Tooltip("Underwater effort camera-sway multiplier when stamina is critically low.")]
        [SerializeField, Range(1f, 4f)] private float underwaterSomaticFatigueSwayMultiplier = 2.25f;
        [Tooltip("Cooldown between low-stamina helmet-breath effort one-shots.")]
        [SerializeField, Range(0.2f, 3f)] private float underwaterSomaticFatigueBreathCooldown = 1.1f;
        [Tooltip("Volume scale applied to the existing helmet-breath one-shot while stamina is critically low.")]
        [SerializeField, Range(0f, 1f)] private float underwaterSomaticFatigueBreathVolumeScale = 0.45f;
        [Tooltip("Immediate VelocityChange applied when a wall kick reflects the player off a KCC wall normal.")]
        [SerializeField, Range(0f, 18f)] private float wallKickVelocityChange = 7.5f;
        [Tooltip("Normalized suit-energy/stamina fraction consumed by each wall kick.")]
        [SerializeField, Range(0f, 0.5f)] private float wallKickResourceCost01 = 0.15f;
        [Tooltip("Fixed-frame age allowed for a KCC wall contact to remain eligible for wall kick.")]
        [SerializeField, Range(0, 8)] private int wallKickContactFrameGrace = 3;
        [Tooltip("Cooldown after a wall kick so held sprint cannot repeatedly fire every physics tick.")]
        [SerializeField, Range(0f, 1f)] private float wallKickCooldown = 0.28f;
        [Tooltip("Tangent velocity retained after a voxel wall kick removes into-wall motion.")]
        [SerializeField, Range(0f, 1f)] private float wallKickTangentFriction = 0.78f;
        [Tooltip("KCC slide angle required before a wall scrape emits camera and physics feedback.")]
        [SerializeField, Range(1f, 89f)] private float suitScrapeSlideAngleThresholdDegrees = 45f;
        [Tooltip("Minimum KCC blocked speed required before a wall scrape emits feedback.")]
        [SerializeField, Range(0f, 6f)] private float suitScrapeMinBlockedSpeed = 0.45f;
        [Tooltip("Speed scale forwarded to the physics impact queue for low-amplitude scrape feedback.")]
        [SerializeField, Range(0f, 2f)] private float suitScrapeImpactBusSpeedScale = 0.38f;
        [Tooltip("Small acoustic radius emitted through the acoustic signal lane when suit plating scrapes a KCC wall.")]
        [SerializeField, Range(0f, 16f)] private float suitScrapeAcousticRadiusMeters = 5f;
        [Tooltip("Speed scale forwarded to camera collision shake for low-amplitude scrape feedback.")]
        [SerializeField, Range(0f, 2f)] private float suitScrapeCameraSpeedScale = 0.32f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ GRADUATED GRAVITY
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Graduated Gravity Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField, Range(1f, 3f)]
        private float gravityFadeRate = 1.4f;

        [SerializeField, Range(1f, 5f)]
        private float snapFadeRate = 2.5f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ MOUSE LOOK
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Mouse Look Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchMin = -85f;
        [SerializeField] private float pitchMax = 85f;

        [Header("VR Comfort Locomotion")]
        [SerializeField, Tooltip("Allows VR comfort rules in editor or desktop test sessions without an active XR device.")]
        private bool vrComfortAllowDesktopPreview;
        [SerializeField, Tooltip("Fallback if SettingsManager is not registered. SettingsManager still owns the persisted player option.")]
        private bool vrComfortModeDefaultEnabled = true;
        [SerializeField, Tooltip("Fallback snap-turn option if SettingsManager is not registered.")]
        private bool vrSnapTurnDefaultEnabled = true;
        [SerializeField, Range(15f, 60f)]
        private float vrSnapTurnDegrees = 30f;
        [SerializeField, Range(0.25f, 0.98f)]
        private float vrSnapTurnThreshold = 0.72f;
        [SerializeField, Range(0.01f, 0.6f)]
        private float vrSnapTurnRearmThreshold = 0.28f;
        [SerializeField, Range(0.05f, 0.2f)]
        private float vrSnapTurnFadeSeconds = 0.1f;
        [SerializeField, Range(15f, 180f)]
        private float vrSmoothTurnDegreesPerSecond = 90f;
        [SerializeField, Range(0f, 0.35f)]
        private float vrSmoothTurnDeadzone = 0.08f;
        [SerializeField, Range(0f, 1f)]
        private float vrHeadRelativeSwimBiasDefault = 0.55f;
        [SerializeField, Tooltip("Fallback horizon-lock option if SettingsManager is not registered.")]
        private bool vrHorizonLockDefaultEnabled = true;
        [SerializeField, Range(0f, 1f)]
        private float vrManualRollInputThreshold = 0.65f;
        [SerializeField, Range(0f, 1f)]
        private float vrCameraLocalMotionSuppression = 1f;
        [SerializeField, Range(1f, 20f)]
        private float vrComfortVignetteSharpness = 8f;
        [SerializeField, Range(0.5f, 20f)]
        private float vrComfortVisualDecaySharpness = 10f;
        [SerializeField, Range(0.25f, 25f)]
        private float vrComfortHighSpeedMetersPerSecond = 15f;
        [SerializeField, Range(15f, 360f)]
        private float vrComfortYawRateReference = 120f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ SWIM VERTICAL DEFAULTS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Control Scheme Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Input System Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Swim Vertical (fallback ÃÂµÃ‘ÂÃÂ»ÃÂ¸ ÃÂ½ÃÂµÃ‘â€š ControlScheme) Ã¢â€â‚¬Ã¢â€â‚¬")]





        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ GROUND DETECTION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Ground Detection Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField, Range(5f, 89f)] private float maxGroundAngle = 60f;
        [SerializeField] private LayerMask groundLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField, Range(1f, 2f)] private float slopeStabilityFactor = 1.1f;
        [SerializeField, Range(0f, 20f)] private float groundSnapForce = 8f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maxJumpTime = 0.3f;
        [SerializeField, Range(0f, 20f)] private float minJumpVelocity = 2f;
        [SerializeField, Range(0f, 30f)] private float maxJumpVelocity = 5f;
        [SerializeField, Range(0f, 0.3f)] private float dryGroundGraceTime = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float shoreGroundGraceTime = 0.14f;
        [SerializeField, Range(0f, 0.6f)] private float stepAssistHeight = 0.3f;
        [SerializeField, Range(0.05f, 0.8f)] private float stepAssistForwardDistance = 0.28f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistClearance = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistCooldownTime = 0.06f;
        [SerializeField, Range(0f, 3f)] private float stepAssistVerticalVelocityPulse = 1.15f;
        [SerializeField, Range(0f, 0.6f)] private float jumpHeadClearanceDistance = 0.18f;
        [SerializeField, Range(0.02f, 0.3f)] private float surfaceBreachDepthWindow = 0.12f;
        [SerializeField, Range(0.3f, 0.95f)] private float surfaceBreachMinImmersion = 0.45f;
        [SerializeField, Range(0.05f, 1f)] private float dryAirControlMultiplier = 0.4f;
        [SerializeField, Range(0f, 1f)] private float dryAirDampingMultiplier = 0.18f;
        [Tooltip("Dry-interior grounded walk force multiplier for heavy NASA-punk suit movement inside airlocks and wreck corridors.")]
        [SerializeField, Range(0.4f, 2f)] private float dryInteriorWalkForceMultiplier = 0.82f;
        [Tooltip("Dry-interior grounded walk speed multiplier for heavy NASA-punk suit movement inside airlocks and wreck corridors.")]
        [SerializeField, Range(0.3f, 1.5f)] private float dryInteriorWalkSpeedMultiplier = 0.76f;
        [Tooltip("How far to either side each dry-interior foot probe samples metal-floor support.")]
        [SerializeField, Range(0.05f, 0.6f)] private float dryInteriorFootProbeLateralOffset = 0.16f;
        [Tooltip("Forward offset applied to each dry-interior foot probe from body center.")]
        [SerializeField, Range(-0.25f, 0.5f)] private float dryInteriorFootProbeForwardOffset = 0.05f;
        [Tooltip("Height above the capsule bottom used by dry-interior foot-support surface probes.")]
        [SerializeField, Range(0.05f, 0.8f)] private float dryInteriorFootProbeHeight = 0.24f;
        [Tooltip("Maximum distance used by each dry-interior foot-support surface probe.")]
        [SerializeField, Range(0.1f, 1.6f)] private float dryInteriorFootProbeDistance = 0.7f;
        [Tooltip("Forward typed-probe range used to resolve hand IK repair snap points on interactables.")]
        [SerializeField, Range(0.2f, 3f)] private float kinematicRepairTargetProbeRange = 1.65f;
        [Tooltip("Surface offset applied to KCC hand IK repair snap points.")]
        [SerializeField, Range(0f, 0.2f)] private float kinematicRepairTargetSurfaceOffset = 0.06f;

        private const double KinematicRepairProbeAupReuseDistanceSq = 0.0144d;
        private const float KinematicRepairProbeDirectionReuseDot = 0.9962f;
        private const int KinematicRepairProbeMaxAupGateSkips = 2;
        private const uint KinematicRepairStateHasSnapBit = 1u << 0;
        private const uint KinematicRepairStateHasProbeCullAnchorBit = 1u << 1;
        private const float BatchedGroundProbeDownDot = 0.98f;
        private const float BatchedGroundProbeHorizontalSlack = 0.45f;
        private const float ExosuitJumpJetNoiseScale = 0.17f;
        private const float ExosuitJumpJetNoiseTimeScale = 0.73f;
        private const float ExosuitJumpJetNoiseVectorScale = 0.055f;
        private const float RuntimeNarcosisInputNoiseScale = 0.22f;
        private const float RuntimeNarcosisInputNoiseFrequency = 1.37f;
        private const float RuntimeNarcosisLookNoiseScale = 0.09f;
        private const float RuntimeNarcosisLookScaleFloor = 0.72f;
        private const uint RuntimeNarcosisLcgMultiplier = 1664525u;
        private const uint RuntimeNarcosisLcgIncrement = 1013904223u;

        [Header("Exosuit Locomotion")]
        [Tooltip("Extra grounded walk force multiplier while an exosuit transport owns locomotion.")]
        [SerializeField, Range(0.5f, 4f)] private float exosuitWalkForceMultiplier = 1.4f;
        [Tooltip("Ground-speed multiplier while an exosuit transport owns locomotion.")]
        [SerializeField, Range(0.5f, 3f)] private float exosuitWalkSpeedMultiplier = 0.82f;
        [Tooltip("Upward force applied by exosuit jump jets while the pilot commands vertical launch.")]
        [SerializeField, Range(0f, 120f)] private float exosuitJumpJetForce = 42f;
        [Tooltip("Immediate upward kick applied when jump jets ignite from the seabed.")]
        [SerializeField, Range(0f, 12f)] private float exosuitJumpJetLaunchImpulse = 3.2f;
        [Tooltip("Suit energy drained per second while exosuit jump jets are firing.")]
        [SerializeField, Range(0f, 40f)] private float exosuitJumpJetEnergyDrainPerSecond = 8.5f;
        [Tooltip("Multiplier applied against mounted transport energy drain when exosuit jump jets ignite.")]
        [SerializeField, Range(1f, 10f)] private float exosuitJumpJetScooterDrainMultiplier = 5f;
        [Tooltip("Normalized heat accumulated per second while exosuit jump jets are firing.")]
        [SerializeField, Range(0f, 4f)] private float exosuitJumpJetHeatPerSecond = 0.85f;
        [Tooltip("Normalized heat removed per second while exosuit jump jets are idle.")]
        [SerializeField, Range(0f, 4f)] private float exosuitJumpJetCoolRate = 0.55f;
        [Tooltip("Heat level below which overheated jump jets recover and can fire again.")]
        [SerializeField, Range(0f, 1f)] private float exosuitJumpJetRecoverThreshold = 0.32f;
        [Tooltip("Extra gravity scale applied while an exosuit transport owns locomotion underwater.")]
        [SerializeField, Range(1f, 3f)] private float exosuitNegativeBuoyancyScale = 1.2f;
        [Tooltip("How far to either side each exosuit foot probe samples slope support.")]
        [SerializeField, Range(0.1f, 1.5f)] private float exosuitFootProbeLateralOffset = 0.34f;
        [Tooltip("Forward offset applied to each exosuit foot probe from body center.")]
        [SerializeField, Range(-0.5f, 1f)] private float exosuitFootProbeForwardOffset = 0.12f;
        [Tooltip("Height above the capsule bottom used by exosuit foot-support surface probes.")]
        [SerializeField, Range(0.05f, 1.5f)] private float exosuitFootProbeHeight = 0.55f;
        [Tooltip("Maximum distance used by each exosuit foot-support surface probe.")]
        [SerializeField, Range(0.1f, 3f)] private float exosuitFootProbeDistance = 1.35f;
        [Tooltip("Minimum normal Y still accepted as footing while the exosuit grips steep cave slopes.")]
        [SerializeField, Range(0.05f, 0.8f)] private float exosuitMinGroundNormalY = 0.18f;
        [Tooltip("How quickly dual-foot slope probes overwrite the default ground normal while the exosuit is planted.")]
        [SerializeField, Range(1f, 40f)] private float exosuitFootSlopeBlendSharpness = 22f;
        [Tooltip("Additional slope-hold force multiplier applied while the exosuit is grounded.")]
        [SerializeField, Range(1f, 4f)] private float exosuitSlopeStickForceMultiplier = 2.25f;
        [Tooltip("Additional snap-force multiplier applied while the exosuit is grounded to keep it glued to cave slopes.")]
        [SerializeField, Range(1f, 4f)] private float exosuitGroundSnapForceMultiplier = 1.85f;
        [Tooltip("One-shot seabed wake strength injected while exosuit jump jets are firing close to the floor.")]
        [SerializeField, Range(0f, 2f)] private float exosuitJumpJetWakeTrailScale = 1.15f;
        [Tooltip("Pulse cadence for exosuit jump-jet wake bursts so the wake stays forceful without spamming every fixed step.")]
        [SerializeField, Range(0.02f, 0.4f)] private float exosuitJumpJetWakePulseInterval = 0.08f;
        [Tooltip("Local sonar radius emitted by heavy exosuit footsteps on rock.")]
        [SerializeField, Range(5f, 40f)] private float exosuitFootstepSonarPingRadius = 20f;
        [Tooltip("Reveal duration used by the exosuit footstep seismic ping.")]
        [SerializeField, Range(0.1f, 2f)] private float exosuitFootstepSonarRevealDuration = 0.75f;
        [Tooltip("Threat-grid pulse strength injected by each heavy exosuit step.")]
        [SerializeField, Range(0f, 2f)] private float exosuitFootstepThreatStrength = 0.9f;
        [Tooltip("How long a footstep threat pulse stays alive so the vegetation bridge can catch it on SlowTick.")]
        [SerializeField, Range(0.1f, 1f)] private float exosuitFootstepThreatHoldDuration = 0.65f;
        [Header("Base Floor Footsteps")]
        [Tooltip("Metal base-floor footstep one-shot emitted through SpatialAudioManager after typed surface material filtering.")]
        [SerializeField] private AudioClip baseFloorMetalFootstepClip;
        [Tooltip("SpatialAudioManager volume for typed-surface metal base-floor footsteps.")]
        [SerializeField, Range(0f, 1f)] private float baseFloorMetalFootstepVolume = 0.38f;
        [Tooltip("Pitch applied to typed-surface metal base-floor footsteps.")]
        [SerializeField, Range(0.5f, 1.5f)] private float baseFloorMetalFootstepPitch = 1f;
        [Tooltip("Slack allowed before the exosuit grapple starts pulling the rig toward the anchor.")]
        [SerializeField, Range(0.2f, 4f)] private float exosuitGrappleRestLength = 1.4f;
        [Tooltip("Continuous reel-in force applied by the exosuit grapple while the winch is loaded.")]
        [SerializeField, Range(0f, 180f)] private float exosuitGrappleReelForce = 56f;
        [Tooltip("Additional Hooke spring strength added on top of the reel-in force when the grapple line is overstretched.")]
        [SerializeField, Range(0f, 160f)] private float exosuitGrappleSpring = 34f;
        [Tooltip("Velocity damping along the grapple axis so the heavy suit does not oscillate forever while climbing.")]
        [SerializeField, Range(0f, 60f)] private float exosuitGrappleDamping = 12f;
        [Tooltip("Maximum force the grapple winch is allowed to inject into the exosuit each fixed step.")]
        [SerializeField, Range(0f, 220f)] private float exosuitGrappleMaxForce = 88f;
        [Tooltip("How long the grapple request remains alive without a fresh reel command from the harpoon tool.")]
        [SerializeField, Range(0f, 0.35f)] private float exosuitGrappleHoldTime = 0.1f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ FOV
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ FOV Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Base FOV of the camera. FOV compression applies relative to this.")]
        [SerializeField] private float baseFov = 70f;
        [Header("Narrative Soft Look-At")]
        [SerializeField] private bool cinematicFocusEnabled = true;
        [SerializeField, Range(0f, 12f)] private float cinematicFocusPullStrength = 3.5f;
        [SerializeField, Range(0.1f, 30f)] private float cinematicFocusInputBreakThreshold = 8f;
        [SerializeField, Range(0.25f, 20f)] private float cinematicFocusYieldRecoverySharpness = 6f;
        [SerializeField, Range(35f, 120f)] private float cinematicFocusFov = 75f;
        [SerializeField, Range(0.5f, 20f)] private float cinematicFocusFovSharpness = 4f;
        [SerializeField, Range(0.25f, 12f)] private float cinematicFocusDefaultDuration = 3.5f;
        [SerializeField, Range(4f, 120f)] private float cinematicFocusSubtitleFadeDistance = 40f;
        [Tooltip("How much underwater body-yaw responsiveness remains while dragging the heaviest heavy-carry object.")]
        [SerializeField, Range(0.1f, 1f)] private float maxHeavyCarryBodyYawSpringMultiplier = 0.58f;
        [Header("Heavy Tow Response")]
        [Tooltip("How much the camera pitches upward while a heavy tow line is loading the player from behind.")]
        [SerializeField, Range(0f, 20f)] private float heavyTowCameraPitchDegrees = 5.5f;
        [Tooltip("How much the camera rolls toward a laterally drifting tow payload.")]
        [SerializeField, Range(0f, 20f)] private float heavyTowCameraRollDegrees = 8.5f;
        [Tooltip("How far the camera shifts backward at peak tow load.")]
        [SerializeField, Range(0f, 0.4f)] private float heavyTowCameraBackwardOffset = 0.09f;
        [Tooltip("How far the camera shifts sideways toward the payload at peak tow load.")]
        [SerializeField, Range(0f, 0.25f)] private float heavyTowCameraSideOffset = 0.055f;
        [Tooltip("How quickly heavy-tow COM and camera response converge.")]
        [SerializeField, Range(1f, 24f)] private float heavyTowResponseBlendSharpness = 7f;
        [Tooltip("Rearward center-of-mass shift applied while the tow line is loaded.")]
        [SerializeField, Range(0f, 0.6f)] private float heavyTowCenterOfMassRearShift = 0.22f;
        [Tooltip("Lateral center-of-mass shift applied toward the towed mass.")]
        [SerializeField, Range(0f, 0.35f)] private float heavyTowCenterOfMassLateralShift = 0.14f;
        [Tooltip("Downward center-of-mass sink applied while the tow line is loaded.")]
        [SerializeField, Range(0f, 0.25f)] private float heavyTowCenterOfMassDownShift = 0.05f;
        [Header("Cutting Tension Physics")]
        [Tooltip("Maximum slack allowed between the player and the current cut anchor before virtual spring tension starts loading.")]
        [SerializeField, Range(0.2f, 3f)] private float cuttingTensionRestLength = 1.1f;
        [Tooltip("Hooke spring strength applied while the cutter anchor is loaded.")]
        [SerializeField, Range(0f, 120f)] private float cuttingTensionSpring = 24f;
        [Tooltip("Velocity damping applied along the cutter spring axis so the player does not oscillate forever while pulling metal free.")]
        [SerializeField, Range(0f, 40f)] private float cuttingTensionDamping = 8f;
        [Tooltip("Maximum force the virtual cutter spring is allowed to inject into the player body.")]
        [SerializeField, Range(0f, 120f)] private float cuttingTensionMaxForce = 34f;
        [Tooltip("How long a cutter anchor request is kept alive without a fresh tool update before the spring fully releases.")]
        [SerializeField, Range(0f, 0.25f)] private float cuttingTensionHoldTime = 0.08f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ DIAGNOSTICS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Diagnostics Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField] private bool _debugIsWalking;
        [SerializeField] private string _debugLocomotionMode;
        [SerializeField] private bool _debugIsGrounded;
        [SerializeField] private float _debugImmersionRatio;
        [SerializeField] private float _debugSmoothedImmersion;
        [SerializeField] private float _debugGravityScale;
        [SerializeField] private float _debugSnapScale;
        [SerializeField] private float _debugBodyYaw;
        [SerializeField] private float _debugCameraYaw;
        [SerializeField] private float _debugCurrentRoll;
#pragma warning disable CS0414
        [SerializeField] private bool _debugStepEvent;
#pragma warning restore CS0414
        [SerializeField] private string _debugSuitName;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private float _debugDynamicWaterY;
        [SerializeField] private bool _debugCrestAvailable;
        [SerializeField] private bool _debugCrestSampling;
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugFovOffset;
        [SerializeField] private bool _debugSplashThisFrame;
        [SerializeField] private bool _debugExhaleThisFrame;
        [SerializeField] private bool _debugIsSubmerged;
        [SerializeField] private bool _debugHasSwimPresentationController;
        [SerializeField] private int _debugLastSwimPresentationDriveFrame = -1;
#pragma warning disable CS0414
        [SerializeField] private bool _debugHeavyCarryActive;
        [SerializeField] private float _debugHeavyCarryForceMultiplier = 1f;
        [SerializeField] private float _debugHeavyCarrySpeedMultiplier = 1f;
        [SerializeField] private float _debugSargassumSpeedMultiplier = 1f;
        [SerializeField] private float _debugSargassumDragMultiplier = 1f;
        [SerializeField] private bool _debugSargassumEntangled;
        [SerializeField] private float _debugSargassumEntanglement01;
        [SerializeField] private float _debugSargassumEntanglementDragRequest = 1f;
        [SerializeField] private float _debugSargassumFieldDensity01;
        [SerializeField] private float _debugSargassumMatBuoyancy01;
        [SerializeField] private float _debugExternalEnvironmentalDragMultiplier = 1f;
        [SerializeField] private float _debugExternalEnvironmentalSpeedMultiplier = 1f;
        [SerializeField] private float _debugExternalEnvironmentalThrustMultiplier = 1f;
        [SerializeField] private int _debugParasiteLatchedCount;
        [SerializeField] private Vector3 _debugParasiteCenterOfMassLS;
        [SerializeField] private Vector3 _debugParasiteHarvesterPullWS;
        [SerializeField] private float _debugSurfaceWavePitch;
        [SerializeField] private float _debugSurfaceWaveRoll;
        [SerializeField] private float _debugStormIntensity01;
        [SerializeField] private float _debugWaveHeightSpan;
        [SerializeField] private float _debugTransportCavitationEfficiency = 1f;
        [SerializeField] private float _debugShoreBuoyancyBlend = 1f;
        [SerializeField] private float _debugBottomClearance = -1f;
        [SerializeField] private float _debugWetLensIntensity;
        [SerializeField] private float _debugWaveSlopeForward;
        [SerializeField] private float _debugWaveSlopeLateral;
        [SerializeField] private float _debugUndertowIntensity;
        [SerializeField] private float _debugWipeoutTimer;
        [SerializeField] private float _debugDynamicCollisionTuck;
        [SerializeField] private float _debugAbyssalCurrentIntensity;
        [SerializeField] private bool _debugHeavyTowActive;
        [SerializeField] private float _debugHeavyTowTension01;
        [SerializeField] private float _debugHeavyTowStress01;
        [SerializeField] private float _debugHeavyTowDragMultiplier = 1f;
        [SerializeField] private float _debugHeavyTowSignedLateralPull;
        [SerializeField] private float _debugHeavyTowBackwardPull;
#pragma warning restore CS0414

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CACHED REFERENCES
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private IBuoyancyAirStateReadModel _buoyancyAirState;
        private CapsuleCollider _capsuleCollider;
        private Transform _cachedTransform;
        private Camera _cameraComponent;
        private HectonPlayerCameraRig _cameraRig;
        private HectonPlayerMotor _playerMotor;
        private HectonPlayerEnvironmentHandler _environmentHandler;
        private HectonPlayerStateMachine _stateMachine;
        private WaterTransitionHandler _waterTransitionHandler;
        private HectonPlayerState _playerState;
        private HectonPlayerInputHandler _inputHandler;
        private IInputService _inputManager;
        private IInputService _subscribedInputManager;
        private IAudioService _audioService;
        private SettingsManager _settingsRuntime;
        private ILocalizationStressPresentationReadModel _localizationRuntime;
        private ILocalizationStressHudRefreshSink _localizationStressHudRefreshSink;
        private SpectrumSystem _spectrumRuntime;
        private ResourceDistributionDirector _resourceDistributionRuntime;
        private IGasDynamicsSolver _gasDynamicsRuntime;
        private IFluidSurfaceCurrentReadModel _fluidSurfaceRuntime;
        private ITerrainProvider _terrainProviderRuntime;
        private IAbyssalFlowGpuReadModel _abyssalFlowGpuRuntime;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private IPhysicsService _physicsService;
        private IPhysicsStateEventService _physicsStateEvents;
        private IInputService _inputServiceRuntime;
        private IPlayerInventoryService _playerInventoryService;
        private HectonVoxelEngine _voxelEngineRuntime;
        private SargassumGlobalDragManager _sargassumDragRuntime;
        private AbyssalThermalManager _thermodynamicsRuntime;
        private SuitUpgradeManager _suitUpgradeRuntime;
        private IHectonOceanKinematicsService _oceanKinematicsRuntime;
        private IPlayerSensoryService _playerSensoryRuntime;
        private PlayerToolManager _playerToolManager;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private ITransportPlatform _activeTransportPlatform;
        private MonoBehaviour _activeTransportPlatformBehaviour;
        private Transform _activeTransportPlatformTransform;
        private Vector3 _lastTransportPlatformPosition = Vector3.zero;
        private Vector3 _currentTransportPlatformPosition = Vector3.zero;
        private AbsoluteUniversePosition _lastTransportPlatformAup;
        private AbsoluteUniversePosition _currentTransportPlatformAup;
        private Quaternion _currentTransportPlatformRotation = Quaternion.identity;
        private Quaternion _transportPlatformDeltaRotation = Quaternion.identity;
        private Matrix4x4 _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
        private Matrix4x4 _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
        private Quaternion _cachedTransportPlatformBasisRotation = Quaternion.identity;
        private bool _cachedTransportPlatformSpatialFrameValid;
        private bool _transportPlatformAupFrameValid;
        private PlayerSwimPresentationController _swimPresentationController;
        private PhysicalInteractionHandler _physicalInteractionHandler;
        private HeavyTowWinch _heavyTowWinch;
        private SargassumMovementInfluence _sargassumMovementInfluence;
        private HectonSurvivalSystem _survivalSystem;
        private PlayerInventory _inventoryLoadSource;
        private PlayerInventory _boundInventoryLoadSignalSource;
        private uint _inventoryLoadSignalHash;
        private uint _lastInventoryLoadSignalRevision;
        private PlayerKinematicsNativeState _playerKinematicsNativeState;
        private bool _playerKinematicsTelemetryDumpedThisFault;
        private float3 _lastPlayerKinematicsIntendedMovement;
        private float3 _lastPlayerKinematicsBurstDragVelocity;
        private float _lastPlayerKinematicsDragCoefficient;
        private float _lastPlayerKinematicsWaterDensityScale = 1f;
        private BrineLayerSample _lastBrineLayerSample;
        private bool _isInsideBrineLayer;
        private bool _wasInsideBrineLayer;
        private float _brineSubmersionSeconds;
        private float _lastPublishedBrineRuntimeHeightY = float.NegativeInfinity;
        private float _lastPublishedBrineFogHardClip = -1f;
        private float _lastPublishedBrineColorAlpha = -1f;
        private bool _pendingBrineShaderGlobalsDirty;
        private bool _pendingBrineShaderGlobalsActive;
        private float _pendingBrineRuntimeHeightY = -1000000f;
        private float _pendingBrineFogHardClip;
        private Vector4 _pendingBrineColor;
        private readonly AbsoluteUniversePosition[] _lastValidAupRing = new AbsoluteUniversePosition[LastValidAupRingCapacity]; // COLD ALLOC: AbsoluteUniversePosition[16] - no-clip recovery ring - owner: HectonPlayerMovement
        private int _lastValidAupWriteIndex;
        private int _lastValidAupCount;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private bool _resolvedInputManager;
        private bool _resolvedPlayerToolManager;
        private bool _resolvedPlayerTransportCoordinator;
        private bool _resolvedSwimPresentationController;
        private bool _resolvedPhysicalInteractionHandler;
        private bool _resolvedHeavyTowWinch;
        private bool _resolvedUnderwaterVisuals;
        private int _playerColliderInstanceId;
        private int _instanceId;
        private KinematicRepairTargetProbe _lastKinematicRepairProbe;
        private KinematicRepairSnapPoint _lastKinematicRepairSnapPoint;
        private AbsoluteUniversePosition _lastKinematicRepairProbeCullAup;
        private Vector3 _lastKinematicRepairProbeCullDirection = Vector3.forward;
        private int _kinematicRepairProbeAupGateSkipCount;
        private uint _kinematicRepairStateBits;
        private float _sargassumInfluenceClockSeconds;
        private float _nextSargassumEntanglementAudioTime = float.NegativeInfinity;
        private float _basePlayerHeight;
        private float _baseCapsuleHeight;
        private float _baseCapsuleRadius;
        private Vector3 _baseCenterOfMass;
        private Vector3 _lastAppliedCenterOfMass;
        private Vector3 _baseCapsuleCenter;
        private float _appliedCollisionHeightScale = 1f;
        private float _appliedCollisionRadiusScale = 1f;
        private float _appliedCollisionCenterYOffset;
        private float _requestedTransportCollisionHeightScale = 1f;
        private float _requestedTransportCollisionRadiusScale = 1f;
        private float _requestedTransportCollisionCenterYOffset;
        private float _dynamicCollisionTuck01;
        private float _physicalTraumaCollisionWeight;
        private float _physicalTraumaCollisionHoldTimer;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CREST OCEAN Ã¢â‚¬â€ runtime state
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private PhysicsOceanKinematics _oceanKinematics;
        private bool _crestAvailable;
        private float _dynamicWaterSurfaceY;
        private Vector3 _dynamicWaterSurfaceNormal = Vector3.up;
        private Vector3 _dynamicWaterSurfaceVelocity;
        private Vector3 _dynamicWaterFlowVelocity;
        private Vector3 _dynamicWaterDisplacement;
        private Vector3 _dynamicAverageWaterVelocity;
        private Vector3 _dynamicAverageWaterDisplacement;
        private float _fallbackWaterSurfaceY;
        private float _dynamicWaveHeightSpan;
        private float _dynamicStormIntensity;
        private bool _crestSamplingSucceeded;
        private bool _crestFlowSamplingSucceeded;
        private readonly Vector3[] _crestQueryPoints = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] — batched Crest body-query points (center/head/feet/left/right) — owner: HectonPlayerMovement
        private readonly float[] _crestQueryHeights = new float[CrestBodySampleCount]; // COLD ALLOC: float[5] — batched Crest sampled water heights — owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryNormals = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] — batched Crest sampled normals — owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryVelocities = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] — batched Crest sampled water velocities — owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryDisplacements = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] — batched Crest sampled displacements — owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryFlows = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] — batched Crest flow samples — owner: HectonPlayerMovement

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CAMERA JUICE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        // COLD ALLOC: List<VisorHUDController>(4) â€” reused fatal-pressure visor glitch target list â€” owner: HectonPlayerMovement
        private static readonly List<VisorHUDController> s_fatalPressureGlitchControllers = new List<VisorHUDController>(4);
        private CameraJuiceProcessor _juiceProcessor;
        private CameraJuiceInput _juiceInput;
        private CameraJuiceOutput _juiceOutput;
        private Vector3 _cameraBaseLocalPos;
        private IDataVault _dataVault;
        private VaultGenerationHandle<CinematicFocusTelemetryEntry> _cinematicFocusBlackBoxHandle;
        private bool _ownsCinematicFocusBlackBox;
        private AbsoluteUniversePosition _cinematicFocusTargetAup;
        private int _cinematicFocusBlackBoxCursor;
        private int _cinematicFocusBlackBoxCount;
        private int _cinematicFocusLastDumpFrame = -CinematicFocusBlackBoxDumpCooldownFrames;
        private uint _cinematicFocusHash;
        private uint _cinematicFocusSubtitleHash;
        private float _cinematicFocusIntensity01;
        private float _cinematicFocusTimer;
        private float _cinematicFocusPullSuppression01;
        private float _cinematicFocusSubtitleFadeDistanceSq = CinematicFocusDefaultFadeDistanceSq;
        private float _cinematicFocusLastDistanceSq;
        private float _cinematicFocusLastSubtitleAlpha01;
        private byte _cinematicFocusFlags;
        private byte _cinematicFocusBoneTarget;
        private bool _cinematicFocusActive;
        private bool _cinematicFocusAudioDucked;
        private float _cinematicFocusFovQualityWeight01 = 1f;
        private Vector3 _feedbackVelocity;
        private float _underwaterSomaticPhase;
        private float _underwaterSomaticWeight;
        private float _underwaterSomaticPitchOffset;
        private float _underwaterSomaticYawOffset;
        private float _underwaterSomaticFatigue01;
        private float _underwaterSomaticFatigueBreathCooldownTimer;
        private readonly PresentationAudioEvent[] _pendingPresentationAudioEvents = new PresentationAudioEvent[MaxPendingPresentationAudioEvents]; // COLD ALLOC: fixed visual-sync audio queue - owner: HectonPlayerMovement
        private int _pendingPresentationAudioEventCount;
        private bool _pendingMovementAcousticSignalDirty;
        private MovementAcousticSignal _pendingMovementAcousticSignal;
        private bool _pendingKccWallScrapeAcousticDirty;
        private AcousticPingSignal _pendingKccWallScrapeAcoustic;
        private int _pendingBreachBubbleCount;
        private int _pendingWipeoutBubbleCount;
        private bool _pendingBreachSplashRingDirty;
        private Vector3 _pendingBreachSplashRingPosition;
        private bool _pendingWaterEntryFovImpulseDirty;
        private float _pendingWaterEntryFovExpand;
        private float _pendingWaterEntryFovCompress;
        private float _pendingWaterEntryFovDuration;
        private float _pendingCameraCollisionImpulse;
        private float _pendingCameraEntanglementStrain;
        private float _pendingCameraSonarPing;
        private float _pendingCameraExternalRollImpulse;
        private bool _pendingCameraExternalRollImpulseDirty;
        private float _pendingBottomSiltBurstIntensity;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INPUT STATE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float _inputH;
        private float _inputV;
        private float _inputVertical;
        private float _mouseXDelta;
        private PlayerInputState _currentInputState;
        private Vector2 _cachedMoveInput;
        private Vector2 _pendingLookInput;
        private float _cachedVerticalInput;
        private float _currentRenderDeltaTime = 0.0166667f;
        private bool _vrSnapTurnArmed = true;
        private float _vrSnapTurnFadeTimer;
        private float _vrComfortVignette01;
        private float _vrComfortVisualBounce01;
        private float _vrComfortPeripheralBlur01;
        private float _vrComfortKickSignal01;
        private Vector2 _vrComfortSway;
        private Vector2 _vrComfortMotionVector;
        private float _vrComfortVelocitySq01;
        private Vector4 _lastPublishedVrComfortSignals = Vector4.positiveInfinity;
        private Vector4 _lastPublishedVrComfortSway = Vector4.positiveInfinity;
        private Vector4 _lastPublishedVrComfortMotion = Vector4.positiveInfinity;
        private float _lastPublishedVrComfortVignette01 = float.PositiveInfinity;
        private bool _pendingVrComfortShaderDirty;
        private Vector4 _pendingVrComfortSignals;
        private Vector4 _pendingVrComfortSwaySignal;
        private Vector4 _pendingVrComfortMotionSignal;
        private float _pendingVrComfortVignette01;
        private float _maxVrComfortVignetteTelemetry01;
        private float _lastVrComfortVignetteTelemetry01;
        private float _vrHorizonRollDampedDegrees;
        private bool _vrHorizonRollDampingInitialized;
        private bool _vrComfortGravityScaleInitialized;
        private float _vrComfortGravityScaleCurrent;
        private float _vrComfortGravityScaleStart;
        private float _vrComfortGravityScaleTarget;
        private float _vrComfortGravityScaleTimer;
        private bool _vrComfortActiveCached;
        private bool _vrSnapTurnEnabledCached = true;
        private bool _vrHorizonLockEnabledCached = true;
        private bool _vrComfortVignetteEnabledCached = true;
        private float _vrHeadRelativeSwimBiasCached = 0.55f;

        private float _cameraYaw;
        private float _cameraPitch;
        private bool _transportPlatformRotationInitialized;
        private Quaternion _lastTransportPlatformRotation = Quaternion.identity;
        private RenderInterpolationState _previousRenderInterpolationState;
        private RenderInterpolationState _currentRenderInterpolationState;
        private Vector3 _renderInterpolatedLinearVelocity;
        private float _renderInterpolatedCameraYaw;
        private float _renderInterpolatedBodyYaw;
        private bool _renderInterpolationStateInitialized;

        private bool _inputCleared;
        private bool _jumpRequested;
        private bool _isSprinting;
        private float _jumpBufferTimer;
        private float _jumpHeldTimer;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  BODY YAW (decoupled from camera)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float _bodyYaw;
        private float _bodyYawVelocity;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  MODE STATE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private bool _isWalking;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private float _dryGroundGraceTimer;
        private float _shoreGroundGraceTimer;
        private float _stepAssistCooldownTimer;
        private float _currentFixedDeltaTime = 0.02f;
        private float _waterImmersionRatio;
        private float _smoothedImmersionRatio;
        private float _currentLinearDamping;
        private float _gravityScale;
        private float _snapScale;
        private float _currentDepth;  // v7.0: meters below water surface
        private bool _isSurfaceSwimming;
        private PlayerLocomotionMode _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        private float _surfaceBreachLockTimer;
        private float _surfaceBreachFluidDragBypassTimer;
        private float _surfaceDiveCommitTimer;
        private float _surfaceDiveAssistTimer;
        private float _surfaceLockBlend;
        private float _surfaceLockTargetY;
        private float _waterEntryImpactTimer;
        private float _waterEntryImpactStrength;
        private float _shoreBuoyancyBlend = 1f;
        private float _bottomClearance = float.PositiveInfinity;
        private Vector3 _bottomNormal = Vector3.up;
        private bool _isAirborne;
        private float _exosuitJumpJetHeat01;
        private bool _exosuitJumpJetsOverheated;
        private Vector3 _exosuitFootingNormal = Vector3.up;
        private bool _exosuitFootingValid;
        private float _exosuitJumpJetWakePulseTimer;
        private Vector3 _exosuitGrappleAnchorRequestedWS = Vector3.zero;
        private Vector3 _exosuitGrappleAnchorCurrentWS = Vector3.zero;
        private float _exosuitGrappleHoldTimer;
        private float _exosuitGrappleCurrentForce;
        private bool _exosuitGrappleRequestedThisStep;
        private float _wetLensSignalIntensity;
        private float _wetLensPulseCooldownTimer;
        private float _underwaterStressSignalIntensity;
        private float _crestFlowInputAttenuation = 1f;
        private float _externalEnvironmentalDragRequestedMultiplier = 1f;
        private float _externalEnvironmentalDragCurrentMultiplier = 1f;
        private float _externalEnvironmentalDragHoldTimer;
        private bool _externalEnvironmentalDragRequestedThisStep;
        private float _runtimeNarcosisInputNoise01;
        private Vector3 _cuttingTensionAnchorRequestedWS = Vector3.zero;
        private Vector3 _cuttingTensionAnchorCurrentWS = Vector3.zero;
        private Vector3 _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
        private Vector3 _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
        private float _cuttingTensionHoldTimer;
        private float _cuttingTensionCurrentForce;
        private bool _cuttingTensionRequestedThisStep;
        private float _vegetationDensityLinearDamping;
        private float _sargassumFieldDensity01;
        private float _sargassumMatBuoyancyBlend;
        private float _sargassumHighStrainIntensity;
        private float _sargassumHighStrainTimer;
        private AbyssalThermalManager.ThermalFlowSample _abyssalThermalFlowSample;
        private Vector3 _abyssalThermalFlowVelocityWS = Vector3.zero;
        private Quaternion _surfaceWavePoseRotation = Quaternion.identity;
        private Quaternion _underwaterTurbulencePoseRotation = Quaternion.identity;
        private float _transportCavitationEfficiency = 1f;
        private float _heavyTowCameraPitchOffset;
        private float _heavyTowCameraRollOffset;
        private float _kinematicInertiaCameraRollOffset;
        private Vector3 _heavyTowCameraLocalOffset;
        private Vector3 _heavyTowCenterOfMassOffset;
        private float _previousTransportForwardVelocity;
        private Vector2 _dynamicWaveLocalSlope = Vector2.zero;
        private Vector3 _dynamicWaveLongitudinalGradient = Vector3.zero;
        private Vector3 _dynamicWaveLateralGradient = Vector3.zero;
        private Vector3 _undertowVector = Vector3.zero;
        private float _undertowIntensity;
        private float _wipeoutTimer;
        private float _wipeoutSeverity;
        private float _impulseBypassTimer;
        private float _transportBailoutCooldownTimer;
        private int _transportEvaLockTicks;
        private float _recentBreachExitTimer;
        private float _abyssalDowndraftCooldownTimer;
        private float _abyssalDowndraftActiveTimer;
        private float _abyssalDowndraftIntensity;
        private Vector3 _abyssalDowndraftVelocityChange = Vector3.zero;
        private float _abyssalCounterDriveEnergyMultiplier = 1f;
        private float _abyssalShearSpeedMultiplier = 1f;
        private float _abyssalShearDrainMultiplier = 1f;
        private Vector3 _abyssalFlowWeatherCurrent = Vector3.zero;
        private Vector3 _abyssalFlowAdvectionVelocityWS = Vector3.zero;
        private float _abyssalFlowNoiseBoundaryCooldownTimer;
        private Vector3 _previousAbyssalNoisyFlow = Vector3.zero;
        private float _abyssalTransportTurbulencePitchOffset;
        private float _abyssalTransportTurbulenceYawOffset;
        private float _hullStressIntensity;
        private float _hullStressGroanCooldownTimer;
        private float _hullStressHudCorruptionRefreshTimer;
        private float _externalHullStressRequestedIntensity;
        private bool _externalHullStressRequestedThisStep;
        private float _fatalPressureSequenceTimer;
        private float _fatalPressureSequenceGlitchPulseTimer;
        private float _fatalPressureSequenceIntensity;
        private float _fatalPressureRearmTimer;
        private float _fatalPressureLookYawAnchor;
        private float _fatalPressureLookPitchAnchor;
        private int _fatalPressureNotificationMissCount;
        private float _activeSonarPingCooldownTimer;
        private float _thermalUpdraftIntensity;
        private Vector3 _thermalUpdraftVelocityChange = Vector3.zero;
        private Vector3 _externalThermalUpdraftVelocityChange = Vector3.zero;
        private bool _externalThermalUpdraftRequestedThisStep;
        private Vector3 _queuedExternalKinematicAcceleration = Vector3.zero;
        private Vector3 _queuedExternalKinematicVelocityChange = Vector3.zero;
        private float _wallKickCooldownTimer;
        private bool _ladderSplineSnapActive;
        private Vector3 _ladderSplineSnapAxisWorld = Vector3.zero;
        private int _aupSpeculativeHoverTicks;
        private float _aupSpeculativeHoverHeightMeters;
        private int _lastProcessedKccSlideFeedbackFrame = -1;
        private int _parasiteLatchedRequestedCount;
        private int _parasiteLatchedCurrentCount;
        private Vector3 _parasiteCenterOfMassRequestedLS = Vector3.zero;
        private Vector3 _parasiteCenterOfMassCurrentLS = Vector3.zero;
        private Vector3 _parasiteHarvesterPullRequestedWS = Vector3.zero;
        private Vector3 _parasiteHarvesterPullCurrentWS = Vector3.zero;
        private bool _parasiteLatchRequestedThisStep;
        private float _parasiteLatchHoldTimer;
        private float _thermalUpdraftTraumaCooldownTimer;
        private float _surfaceGaspUnderwaterTimer;
        private float _surfaceGaspCooldownTimer;
        private float _sargassumRestRecoveryBlend;
        private bool _surfaceGaspSubmergedLatch;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  AMBIENT CURRENT
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float _currentTimer;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SPEED TRACKING
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float _prevSpeed;
        private float _prevYawForMomentum;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  REGISTRATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredColdTick;
        private bool _registeredLateFrameTick;
        private bool _registeredOriginShiftListener;
        private bool _registeredPlayerMovementContracts;
        private bool _registeredHotSwapListener;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CACHED MATH
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _cameraWorldRotation;
        private bool _useFixedFrameSpatialCache;
        private Vector3 _fixedFrameBodyPosition;
        private Vector3 _fixedFrameCapsuleCenterWS;
        private Vector3 _fixedFrameLossyScale;
        private Quaternion _fixedFrameBodyRotation;
        private float _fixedFrameCapsuleRadius;
        private float _fixedFrameCapsuleHalfHeight;
        private float _fixedFrameBodyBottomY;
        private float _fixedFrameBodyTopY;
        private float _fixedFrameBodyEyeY;
        private int _fixedGroundSweepHitCount;
        private float _fixedGroundSweepMaxDistance;
        private int _cachedLadderSnapColliderInstanceId;
        private ClimbableLadder _cachedLadderSnapComponent;
        private int _cachedFootstepAudioColliderInstanceId;
        private byte _cachedFootstepAudioMaterialId;
        private bool _cachedFootstepAudioMaterialResolved;

        private PlayerMovementSurfaceHit _groundHit;
        private Vector3 _groundCheckOrigin;
        private Vector3 _cachedGravity;
        private Vector3 _localGravityOverride;
        private Vector3 _localGravityOverrideBlendStart;
        private float _cachedGravityMagnitude;
        private float _localGravityOverrideTimer;
        private float _localGravityOverrideBlendTimer;
        private bool _localGravityOverrideActive;
        private Vector3 _smoothedGroundNormal;
        private float _minGroundNormalY;
        private readonly PlayerMovementSurfaceHit[] _groundProbeHitBuffer = new PlayerMovementSurfaceHit[32]; // COLD ALLOC: PlayerMovementSurfaceHit[32] — ground-contact query buffer dedicated to grounding resolution — owner: HectonPlayerMovement
        private readonly PlayerMovementSurfaceHit[] _legacyCollisionProbeHitBuffer = new PlayerMovementSurfaceHit[LegacyCollisionContactCapacity]; // COLD ALLOC: PlayerMovementSurfaceHit[16] — Unity collision callback contact cache for legacy Rigidbody locomotion — owner: HectonPlayerMovement
        private readonly int[] _legacyCollisionProbeFrameBuffer = new int[LegacyCollisionContactCapacity]; // COLD ALLOC: int[16] — per-contact sample frames for legacy collision cache pruning — owner: HectonPlayerMovement
        private readonly ContactPoint[] _collisionContactBuffer = new ContactPoint[LegacyCollisionContactCapacity]; // COLD ALLOC: ContactPoint[16] — nonalloc Collision.GetContacts scratch for legacy contact capture — owner: HectonPlayerMovement
        private int _legacyCollisionProbeHitCount;
        private int _legacyCollisionProbeFrame = -1;
        private uint _legacyCollisionProbeShiftSequence;
        private int _movementProbeCacheFixedSequence;
        private int _movementProbeCacheSequence = -1;
        private int _movementProbeCacheLayerMask;
        private uint _movementProbeCacheShiftSequence;
        private Vector3 _movementProbeCacheOrigin;
        private Vector3 _movementProbeCacheDirection;
        private float _movementProbeCacheRadius;
        private float _movementProbeCacheDistance;
        private bool _movementProbeCacheHasHit;
        private PlayerMovementSurfaceHit _movementProbeCacheHit;

        internal Transform PlayerCameraTransform => playerCamera;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  EVENTS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private uint PlayerSignalSourceId => unchecked((uint)EntityId.ToULong(GetEntityId()));

        internal float ResolveAuthoritativeBodyMassKg()
        {
            float mass = _authoritativeBodyMassKg;
            return math.isfinite(mass) ? math.max(0.01f, mass) : HydrostaticExitMassReferenceKg;
        }

        private void CacheAuthoritativeBodyMassKg(float massKg)
        {
            _authoritativeBodyMassKg = math.isfinite(massKg)
                ? math.max(0.01f, massKg)
                : HydrostaticExitMassReferenceKg;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CONSTANTS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private const float DEG_TO_RAD = 0.01745329f;
        private const float ParasiteLatchMaxLeverArm = 0.85f;
        private const float ParasiteLatchMaxAngularAcceleration = 12f;

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct RenderInterpolationState
        {
            [FieldOffset(0)]
            public Vector3 BodyPosition;
            [FieldOffset(12)]
            public float CameraYaw;
            [FieldOffset(16)]
            public float BodyYaw;
            [FieldOffset(20)]
            public Vector3 LinearVelocity;
            [FieldOffset(32)]
            public float VerticalVelocity;
            [FieldOffset(36)]
            private uint _pad0;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  EFFECTIVE WATER SURFACE Ã¢â‚¬â€ Crest or fallback
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float EffectiveWaterSurfaceY => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceY
            : _fallbackWaterSurfaceY;

        private Vector3 EffectiveWaterSurfaceNormal => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceNormal
            : Vector3.up;

        private Vector3 EffectiveWaterSurfaceVelocity => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceVelocity
            : Vector3.zero;

        private Vector3 EffectiveWaterFlowVelocity => (_crestFlowSamplingSucceeded && useCrestOceanHeight)
            ? _dynamicWaterFlowVelocity
            : Vector3.zero;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  PUBLIC API
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        /// <summary>
        /// Replaces the active suit data and re-initializes rigidbody mass, drag,
        /// and the juice processor for the new suit profile.
        /// </summary>
        /// <param name="newSuit">Suit data to apply. Null is rejected with a warning.</param>
        public void SetSuit(SuitData newSuit)
        {
            if (newSuit == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[HectonPlayerMovement] null suit.", this);
#endif
                return;
            }

            currentSuitData = newSuit;
            ApplySuitToRigidbody();
            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);
            UpdateSuitDiagnostics();
        }

        /// <summary>
        /// Applies a runtime-only multiplier to underwater thrust and swim-speed ceilings.
        /// </summary>
        /// <param name="multiplier">Runtime swim-speed multiplier.</param>
        public void SetRuntimeSwimSpeedMultiplier(float multiplier)
        {
            _runtimeSwimSpeedMultiplier = math.clamp(multiplier, 0.5f, 3f);
        }

        /// <summary>
        /// Applies voxel streaming backpressure without overwriting buffs, injury penalties, or emergency movement gates.
        /// </summary>
        internal void SetRuntimeVoxelBackpressureSwimSpeedMultiplier(float multiplier)
        {
            _runtimeVoxelBackpressureSwimSpeedMultiplier = math.clamp(multiplier, 0.5f, 1f);
        }

        /// <summary>
        /// Applies a body-state penalty without overwriting external runtime swim buffs.
        /// </summary>
        internal void SetRuntimeInjurySwimSpeedMultiplier(float multiplier)
        {
            _runtimeInjurySwimSpeedMultiplier = math.clamp(multiplier, 0.35f, 1f);
        }

        /// <summary>
        /// Applies a temporary emergency locomotion multiplier without overwriting authored buffs or injury penalties.
        /// </summary>
        internal void SetRuntimeEmergencyMovementMultiplier(float multiplier)
        {
            _runtimeEmergencyMovementMultiplier = math.clamp(multiplier, 0.5f, 2f);
        }

        /// <summary>
        /// Applies body-state stamina loss without overwriting injury, emergency, or inventory gates.
        /// </summary>
        internal void SetRuntimeStaminaMultiplier(float multiplier)
        {
            _runtimeStaminaMultiplier = math.clamp(multiplier, 0.2f, 1f);
        }

        internal void SetRuntimeNarcosisInputNoise(float severity01)
        {
            _runtimeNarcosisInputNoise01 = math.saturate(severity01);
        }

        internal void TriggerCriticalStaminaFailure(float durationSeconds = CriticalStaminaFailureDurationSeconds)
        {
            _criticalStaminaFailureTimer = math.max(_criticalStaminaFailureTimer, math.max(0f, durationSeconds));
        }

        internal void ApplyRuntimeNarcosisConvulsion(float severity01, float durationSeconds)
        {
            float clampedSeverity = math.saturate(severity01);
            float clampedDuration = math.max(0f, durationSeconds);
            if (clampedSeverity <= 0f || clampedDuration <= 0f)
                return;

            _wipeoutSeverity = math.max(_wipeoutSeverity, clampedSeverity);
            _wipeoutTimer = math.max(_wipeoutTimer, clampedDuration);
            _stateMachine?.BeginWipeout(_wipeoutSeverity, _wipeoutTimer);

            QueueCameraEntanglementStrain(math.lerp(0.35f, 0.95f, clampedSeverity));
        }

        private void ApplyRuntimeNarcosisInputNoise(ref float inputH, ref float inputV, ref float inputVertical)
        {
            float severity01 = math.saturate(_runtimeNarcosisInputNoise01);
            if (severity01 <= 0f)
                return;

            float inputIntent = math.max(math.max(math.abs(inputH), math.abs(inputV)), math.abs(inputVertical));
            if (inputIntent <= 0.001f)
                return;

            uint timeTick = (uint)math.max(0, (int)math.min(2147483647f, _currentTimer * 60f));
            uint narcosisSeed = AdvanceRuntimeNarcosisLcg(unchecked((uint)_instanceId) ^ timeTick);

            float phase = _currentTimer * RuntimeNarcosisInputNoiseFrequency +
                ((narcosisSeed & 0xFFFFu) * 0.000015259022f) * TwoPi;

            System.Numerics.Vector2 drifted2D = Hecton8.PureLogic.Kinematics.NitrogenNarcosisInputDrifter.Calculate(
                new System.Numerics.Vector2(inputH, inputV),
                severity01,
                _currentTimer,
                unchecked((int)narcosisSeed)
            );

            inputH = drifted2D.X;
            inputV = drifted2D.Y;

            // Advance seed twice to account for the two advancements that happened inside Calculate (for X and Y)
            narcosisSeed = AdvanceRuntimeNarcosisLcg(AdvanceRuntimeNarcosisLcg(narcosisSeed));

            inputVertical = math.clamp(
                inputVertical + SignedTriangleRadians(phase * 1.231f + ((narcosisSeed & 0xFFFFu) * 0.000015259022f) * TwoPi) * RuntimeNarcosisInputNoiseScale * 0.35f * severity01,
                -1f,
                1f);
        }

        private static uint AdvanceRuntimeNarcosisLcg(uint state)
        {
            return state * RuntimeNarcosisLcgMultiplier + RuntimeNarcosisLcgIncrement;
        }

        /// <summary>
        /// Applies a runtime-only movement penalty sourced from carried inventory mass.
        /// </summary>
        /// <param name="multiplier">Runtime carry-load movement multiplier.</param>
        public void SetRuntimeInventoryLoadMovementMultiplier(float multiplier)
        {
            _runtimeInventoryLoadMovementMultiplier = math.clamp(multiplier, InventoryLoadMinimumMovementMultiplier, 1f);
            _playerState.SyncEncumbrance(_runtimeInventoryLoad01, _runtimeInventoryLoadMovementMultiplier);
        }

        public void ApplyRuntimeInventoryMassLoad(float totalMassKg, float carryCapacityKg)
        {
            _runtimeInventoryTotalMassKg = math.max(0f, totalMassKg);
            _runtimeInventoryLoadRatio = ResolveInventoryLoadRatio(totalMassKg, carryCapacityKg);
            _runtimeInventoryLoad01 = math.saturate(_runtimeInventoryLoadRatio);
            _runtimeInventoryUpwardSwimMultiplier = ResolveInventoryUpwardSwimMultiplierFromLoad(_runtimeInventoryLoad01);
            SetRuntimeInventoryLoadMovementMultiplier(ResolveInventoryLoadMovementMultiplier(totalMassKg, carryCapacityKg));
        }

        public void ApplyRuntimeInventoryMassLoad(float totalMassKg, float carryCapacityKg, float cachedMovementMultiplier, float cachedLoad01)
        {
            _runtimeInventoryTotalMassKg = math.max(0f, totalMassKg);
            _runtimeInventoryLoadRatio = ResolveInventoryLoadRatio(totalMassKg, carryCapacityKg);
            _runtimeInventoryLoad01 = math.saturate(cachedLoad01);
            _runtimeInventoryUpwardSwimMultiplier = ResolveInventoryUpwardSwimMultiplierFromLoad(_runtimeInventoryLoad01);
            SetRuntimeInventoryLoadMovementMultiplier(cachedMovementMultiplier);
        }

        /// <summary>Normalized 0-1 inventory mass load consumed by HUD and locomotion penalties.</summary>
        public float InventoryLoad01 => _runtimeInventoryLoad01;

        /// <summary>Unclamped carried-mass ratio against the resolved survival carry capacity.</summary>
        public float InventoryLoadRatio => _runtimeInventoryLoadRatio;

        /// <summary>Runtime-carried inventory mass in kilograms.</summary>
        public float InventoryCarriedMassKg => _runtimeInventoryTotalMassKg;

        /// <summary>Resolved upward-swim multiplier after inventory mass encumbrance.</summary>
        public float InventoryUpwardSwimMultiplier => _runtimeInventoryUpwardSwimMultiplier;

        /// <summary>Normalized 0-1 movement intent from the latest kinematic input snapshot.</summary>
        public float CurrentMovementIntent01 => math.saturate(math.sqrt(math.max(0f, math.lengthsq(_lastPlayerKinematicsIntendedMovement))));

        /// <summary>Movement-owned stamina drain multiplier forwarded to survival.</summary>
        public float CurrentMovementStaminaDrainMultiplier => MovementStaminaDrainMultiplier;

        /// <summary>Movement-owned emergency encumbrance ratio threshold.</summary>
        public float CurrentCriticalEncumbranceRatio => CriticalEncumbranceRatio;

        /// <summary>Movement-owned stamina threshold for critical encumbrance failure.</summary>
        public float CurrentCriticalStaminaFailureThreshold01 => CriticalStaminaFailureThreshold01;

        /// <summary>True when carried inventory mass exceeds the emergency locomotion cutoff.</summary>
        public bool IsCriticallyEncumbered => _runtimeInventoryLoadRatio >= CriticalEncumbranceRatio;

        public bool IsCriticalStaminaFailureActive => _criticalStaminaFailureTimer > 0f;
        public bool IsSprinting => _isSprinting;

        /// <summary>Resolved movement multiplier after inventory mass encumbrance.</summary>
        public float InventoryLoadMovementMultiplier => ResolveRuntimeInventoryLoadMovementMultiplier();

        /// <summary>Currently active suit data driving mass, drag, and swim parameters.</summary>
        public SuitData CurrentSuit => currentSuitData;
        public bool IsInsideBrineLayer => _isInsideBrineLayer;
        public float CurrentBrineDensityMultiplier => _isInsideBrineLayer
            ? math.max(1f, _lastBrineLayerSample.DensityMultiplier)
            : 1f;
        /// <summary>0â€“1 ratio of the player body submerged below the water surface.</summary>
        public float WaterImmersionRatio => _waterImmersionRatio;
        /// <summary>True when the player is on solid ground and in a walking locomotion mode.</summary>
        public bool IsGrounded => _isGrounded && _isWalking;
        /// <summary>True when the locomotion mode is any form of walking (dry or shallow).</summary>
        public bool IsWalking => _isWalking;
        /// <summary>Resolved locomotion mode for movement, camera, audio, and VFX consumers.</summary>
        public PlayerLocomotionMode CurrentLocomotionMode => _currentLocomotionMode;
        byte IPlayerKinematicsMovementRuntime.CurrentLocomotionModeCode => (byte)_currentLocomotionMode;
        /// <summary>Returns a recent async KCC surface hit for footstep audio without issuing a new physics query.</summary>
        public bool TryGetRecentFootstepSurfaceHit(float maxDistance, LayerMask layerMask, out PlayerMovementSurfaceHit hit)
        {
            hit = default;
            float safeMaxDistance = math.max(0.01f, maxDistance);

            if (_isGrounded && IsReusableFootstepSurfaceHit(in _groundHit, safeMaxDistance, layerMask))
            {
                hit = _groundHit;
                return true;
            }

            return false;
        }

        /// <summary>Current camera roll angle in degrees driven by juice effects.</summary>
        public float CurrentRoll => _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
        /// <summary>Render-interpolated body yaw in degrees. Falls back to physics yaw when interpolation is not initialized.</summary>
        public float BodyYaw => _renderInterpolationStateInitialized ? _renderInterpolatedBodyYaw : _bodyYaw;
        /// <summary>Render-interpolated camera yaw in degrees. Falls back to physics yaw when interpolation is not initialized.</summary>
        public float CameraYaw => _renderInterpolationStateInitialized ? _renderInterpolatedCameraYaw : _cameraYaw;
        /// <summary>Render-interpolated linear velocity vector. Falls back to safe rigidbody velocity.</summary>
        public Vector3 InterpolatedLinearVelocity => _renderInterpolationStateInitialized
            ? _renderInterpolatedLinearVelocity
            : ResolveAuthoritativeLinearVelocity(Vector3.zero);
        /// <summary>Current physics world velocity used by predictive streaming and runtime telemetry.</summary>
        public Vector3 CurrentWorldVelocity => ResolveAuthoritativeLinearVelocity(Vector3.zero);
        /// <summary>Authoritative player AUP from the locomotion snapshot.</summary>
        public AbsoluteUniversePosition CurrentAup => _playerState.AbsolutePosition;
        /// <summary>Dead-reckoned player AUP at +0.1 seconds for low-rate AI steering.</summary>
        public AbsoluteUniversePosition PredictedAup => _playerState.PredictedAbsolutePosition;
        /// <summary>Dead-reckoned runtime position at +0.1 seconds for low-rate AI steering.</summary>
        public float3 PredictedRuntimePosition => _playerState.PredictedRuntimePosition;
        /// <summary>Effective water surface Y from Crest or the serialized fallback.</summary>
        public float CurrentWaterSurfaceY => EffectiveWaterSurfaceY;
        /// <summary>Current depth below the effective water surface in metres.</summary>
        public float CurrentDepth => _currentDepth;
        /// <summary>True when movement-owned head/eye water depth treats the player as submerged.</summary>
        public bool IsPlayerSubmerged => ResolveMovementOwnedSubmergedState();
        private bool ResolveMovementOwnedSubmergedState()
        {
            if (IsInDryInterior() || _waterImmersionRatio <= 0.01f)
                return false;

            float enterDepth = math.max(0.001f, surfaceGaspHeadEnterDepth);
            float exitDepth = math.max(0f, surfaceGaspHeadExitDepth);
            return _currentDepth >= enterDepth ||
                   (_surfaceGaspSubmergedLatch && _currentDepth > exitDepth);
        }
        bool IPlayerKinematicsMovementRuntime.IsKinematicMovementActive => isActiveAndEnabled;
        /// <summary>True when the player is carrying a heavy object that restricts movement.</summary>
        public bool IsDraggingHeavyCargo => IsHeavyCarryActive();
        /// <summary>Normalized 0â€“1 load factor for the currently carried heavy object.</summary>
        public float HeavyCarryLoad => ResolveHeavyCarryLoad01();
        /// <summary>Local-space rotation contributed by Crest surface wave alignment while surface swimming.</summary>
        public Quaternion CurrentSurfaceWavePoseLocalRotation => _surfaceWavePoseRotation;
        /// <summary>Local-space rotation contributed by underwater turbulence while in storm-driven surf.</summary>
        public Quaternion CurrentUnderwaterTurbulencePoseLocalRotation => _underwaterTurbulencePoseRotation;
        /// <summary>Normalized 0â€“1 dynamic storm intensity from the weather director.</summary>
        public float CurrentStormIntensity01 => _dynamicStormIntensity;
        /// <summary>Normalized 0â€“1 shoreline buoyancy blend. 1 = full buoyancy, 0 = grounded contact.</summary>
        public float CurrentShoreBuoyancyBlend01 => _shoreBuoyancyBlend;
        /// <summary>Normalized 0â€“1 wet-lens intensity for visor/camera water droplet effects.</summary>
        public float CurrentWetLensIntensity01 => _wetLensSignalIntensity;
        /// <summary>Normalized near-surface storm stress signal consumed by camera and post-processing layers.</summary>
        public float CurrentUnderwaterStressIntensity01 => _underwaterStressSignalIntensity;
        /// <summary>Normalized crush-depth stress from extreme pressure and rapid depth change.</summary>
        public float CurrentHullStress01 => _hullStressIntensity;
        /// <summary>Normalized fatal-pressure pre-implosion loop intensity. 0 when inactive, 1 just before implosion.</summary>
        public float CurrentFatalPressureSequence01 => _fatalPressureSequenceIntensity;
        /// <summary>Number of fatal-pressure critical notifications refused by the UI queue since this owner was enabled.</summary>
        public int FatalPressureNotificationMissCount => _fatalPressureNotificationMissCount;
        /// <summary>Normalized thermal updraft intensity currently throwing the player upward.</summary>
        public float CurrentThermalUpdraftIntensity01 => _thermalUpdraftIntensity;
        /// <summary>True while the controller is in wipeout recovery and player movement input is suppressed.</summary>
        public bool IsInWipeoutState => _wipeoutTimer > 0f;
        internal float CurrentCuttingTensionForce => _cuttingTensionCurrentForce;

        private bool HydrodynamicKccOwnsCollisionAuthority =>
            _playerMotor != null && _playerMotor.HydrodynamicKccOwnsCollisionAuthority;

        private Vector3 ResolveAuthoritativeLinearVelocity(Vector3 fallback)
        {
            if (HydrodynamicKccOwnsCollisionAuthority &&
                CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMovementMaxAgeFrames, out Vector3 kccVelocity))
            {
                return HectonPlayerMotor.SafeVelocity(kccVelocity, fallback);
            }

            return MathGuard.IsFinite(_velocity)
                ? HectonPlayerMotor.SafeVelocity(_velocity, fallback)
                : fallback;
        }
        internal float CurrentCuttingTensionNormalized => math.saturate(_cuttingTensionCurrentForce / math.max(0.01f, cuttingTensionMaxForce));
        internal float CurrentAbyssalCounterDriveEnergyMultiplier => math.max(1f, _abyssalCounterDriveEnergyMultiplier);
        internal float CurrentAbyssalShearSpeedMultiplier => math.clamp(_abyssalShearSpeedMultiplier, abyssalCurrentShearMaxSpeedMultiplier, 1f);
        internal float CurrentAbyssalShearDrainMultiplier => math.max(1f, _abyssalShearDrainMultiplier);
        internal Vector3 CurrentAbyssalFlowWeatherCurrent => _abyssalFlowWeatherCurrent;
        internal bool HasActiveTowCable => EnsureHeavyTowWinchRuntime() && _heavyTowWinch != null && _heavyTowWinch.HasActiveTow;

        internal bool TryGetKinematicRepairSnap(
            out KinematicRepairTargetProbe probe,
            out KinematicRepairSnapPoint snapPoint)
        {
            probe = _lastKinematicRepairProbe;
            snapPoint = _lastKinematicRepairSnapPoint;
            return (_kinematicRepairStateBits & KinematicRepairStateHasSnapBit) != 0u &&
                   snapPoint.ColliderInstanceId != 0;
        }

        internal bool TryGetActiveTransportPlatform(out ITransportPlatform transportPlatform)
        {
            ResolveActiveTransportPlatform();
            if (_activeTransportPlatform != null && _activeTransportPlatformTransform != null)
            {
                transportPlatform = _activeTransportPlatform;
                return true;
            }

            transportPlatform = null;
            return false;
        }

        internal bool TryTransferHeavyTowToTransport(Rigidbody transportBody, Transform transportAnchor)
        {
            return EnsureHeavyTowWinchRuntime() &&
                   _heavyTowWinch != null &&
                   _heavyTowWinch.TryTransferTowToTransport(transportBody, transportAnchor);
        }

        /// <summary>
        /// Applies a sticky external drag request that smoothly suppresses swim thrust and swim max speed.
        /// Call this continuously while the player remains inside a dense medium. Pass 1 to release back toward neutral.
        /// </summary>
        /// <param name="dragMultiplier">1 = no extra drag. Values above 1 increase resistance.</param>
        public void ApplyEnvironmentalDrag(float dragMultiplier)
        {
            if (!math.isfinite(dragMultiplier))
                return;

            float clampedDragMultiplier = math.max(1f, dragMultiplier);
            if (clampedDragMultiplier > _externalEnvironmentalDragRequestedMultiplier)
                _externalEnvironmentalDragRequestedMultiplier = clampedDragMultiplier;

            _externalEnvironmentalDragRequestedThisStep = true;

            if (clampedDragMultiplier <= 1f)
            {
                _externalEnvironmentalDragHoldTimer = 0f;
                return;
            }

            _externalEnvironmentalDragHoldTimer = ResolveSargassumNonNegative(externalEnvironmentalDragHoldTime, 0f);
        }

        internal void RequestKinematicInertiaRoll(float signedRollDegrees)
        {
            _kinematicInertiaCameraRollOffset = math.clamp(signedRollDegrees, -12f, 12f);
        }

        void IPlayerKinematicsMovementRuntime.RequestKinematicInertiaRoll(float signedRollDegrees)
        {
            RequestKinematicInertiaRoll(signedRollDegrees);
        }

        /// <summary>
        /// Arms the fixed-step cutter spring so locomotion can pull the body back toward the active cut anchor.
        /// LaserCutter owns target acquisition; movement owns actual force application.
        /// </summary>
        internal void ApplyCuttingTensionAnchor(Vector3 anchorPointWS, Vector3 anchorNormalWS)
        {
            _cuttingTensionAnchorRequestedWS = anchorPointWS;
            _cuttingTensionAnchorNormalRequestedWS = anchorNormalWS.sqrMagnitude > 0.0001f
                ? NormalizeVectorRsqrt(anchorNormalWS, Vector3.up)
                : Vector3.up;
            _cuttingTensionRequestedThisStep = true;
            _cuttingTensionHoldTimer = cuttingTensionHoldTime;
        }

        /// <summary>
        /// Explicitly clears the current cutter spring request when the tool stops cutting.
        /// </summary>
        internal void ClearCuttingTensionAnchor()
        {
            _cuttingTensionRequestedThisStep = false;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
        }

        bool IPlayerCuttingTensionService.TryApplyCuttingTensionAnchor(float3 anchorPointWS, float3 anchorNormalWS)
        {
            if (!math.all(math.isfinite(anchorPointWS)) || !math.all(math.isfinite(anchorNormalWS)))
                return false;

            ApplyCuttingTensionAnchor(
                new Vector3(anchorPointWS.x, anchorPointWS.y, anchorPointWS.z),
                new Vector3(anchorNormalWS.x, anchorNormalWS.y, anchorNormalWS.z));
            return true;
        }

        void IPlayerCuttingTensionService.ClearCuttingTensionAnchor()
        {
            ClearCuttingTensionAnchor();
        }

        bool IPlayerCuttingTensionService.TryReadCuttingTensionNormalized(out float tension01)
        {
            tension01 = CurrentCuttingTensionNormalized;
            if (!math.isfinite(tension01))
            {
                tension01 = 0f;
                return false;
            }

            tension01 = math.saturate(tension01);
            return true;
        }

        /// <summary>
        /// Arms the fixed-step exosuit grapple so locomotion can reel the heavy rig toward a static anchor.
        /// Harpoon owns anchor acquisition; movement owns the actual pull force.
        /// </summary>
        internal void ApplyExosuitGrappleAnchor(Vector3 anchorPointWS)
        {
            _exosuitGrappleAnchorRequestedWS = anchorPointWS;
            _exosuitGrappleRequestedThisStep = true;
            _exosuitGrappleHoldTimer = exosuitGrappleHoldTime;
        }

        /// <summary>
        /// Explicitly clears the current exosuit grapple request.
        /// </summary>
        internal void ClearExosuitGrappleAnchor()
        {
            _exosuitGrappleRequestedThisStep = false;
            _exosuitGrappleHoldTimer = 0f;
            _exosuitGrappleCurrentForce = 0f;
        }

        /// <summary>
        /// Temporarily releases authored swim posing so a physical hit can bend the body through the active-trauma presentation path.
        /// Also inflates the collision capsule for a short defensive window so the bent pose does not clip through nearby geometry.
        /// </summary>
        public void ApplyPhysicalTrauma(Vector3 impulse, float weight)
        {
            float clampedWeight = math.saturate(weight);
            if (clampedWeight <= 0f)
                return;

            if (_swimPresentationController != null)
                _swimPresentationController.ApplyPhysicalTrauma(impulse, clampedWeight);

            if (clampedWeight > _physicalTraumaCollisionWeight)
                _physicalTraumaCollisionWeight = clampedWeight;

            _physicalTraumaCollisionHoldTimer = math.max(
                _physicalTraumaCollisionHoldTimer,
                physicalTraumaCollisionHoldTime * math.lerp(0.6f, 1f, clampedWeight));
        }

        /// <summary>
        /// Applies snap-release feedback when a heavy tow cable catastrophically fails.
        /// </summary>
        internal void ApplyTowCableSnapFeedback(Vector3 releasedVelocityChange, Vector3 traumaImpulse, float severity, float signedRoll)
        {
            float clampedSeverity = math.saturate(severity);
            if (clampedSeverity <= 0f)
                return;

            ApplyMotorVelocityChange(releasedVelocityChange);
            ApplyPhysicalTrauma(
                traumaImpulse.sqrMagnitude > 0.0001f
                    ? traumaImpulse
                    : -releasedVelocityChange * ResolveAuthoritativeBodyMassKg(),
                math.lerp(0.18f, 0.55f, clampedSeverity));

            QueueCameraEntanglementStrain(math.lerp(0.18f, 0.62f, clampedSeverity));
            QueueCameraExternalRollImpulse(signedRoll * math.lerp(2.5f, 8f, clampedSeverity));
        }

        /// <summary>
        /// Returns the current local wave slope derived from the batched Crest body samples.
        /// X = lateral slope (right side higher = positive). Y = longitudinal slope (head/forward side higher = positive).
        /// </summary>
        public Vector2 GetCurrentLocalWaveSlope()
        {
            return _dynamicWaveLocalSlope;
        }

        public Vector2 CurrentLocalWaveSlope => _dynamicWaveLocalSlope;

        public bool TryGetRuntimePosition(out Vector3 position)
        {
            position = ResolvePlayerAupRuntimePosition();
            if (!MathGuard.IsFinite(position) && _cachedTransform != null)
                position = _cachedTransform.position;

            position = HectonPlayerMotor.SafeVelocity(position);
            return MathGuard.IsFinite(position);
        }

        /// <summary>
        /// Fires a controller-owned active sonar ping through the existing visor sonar owner.
        /// This is an event-path action, not a per-frame scan.
        /// </summary>
        public bool TriggerActiveSonarPing()
        {
            if (_activeSonarPingCooldownTimer > 0f)
                return false;

            SpectrumSystem spectrumSystem = _spectrumRuntime;
            if (spectrumSystem == null)
                return false;

            if (!spectrumSystem.TriggerActiveSonarPing(activeSonarPingRadius, activeSonarRevealDuration))
                return false;

            _activeSonarPingCooldownTimer = activeSonarPingCooldown;
            return true;
        }

        public void QueueExternalAcceleration(Vector3 acceleration)
        {
            QueueSubsystemExternalAcceleration(acceleration);
        }

        public void QueueExternalVelocityChange(Vector3 velocityChange)
        {
            QueueSubsystemExternalVelocityChange(velocityChange);
        }

        /// <summary>
        /// Accepts a one-step external thermal updraft velocity-change injection from future vent managers.
        /// Call every fixed step while the player remains inside the authored thermal plume.
        /// </summary>
        /// <param name="velocityChange">World-space upward velocity change to inject this fixed step.</param>
        public void ApplyExternalThermalUpdraft(Vector3 velocityChange)
        {
            if (!IsFiniteVector(velocityChange) || velocityChange.y <= 0.0001f)
                return;

            if (!_externalThermalUpdraftRequestedThisStep ||
                velocityChange.sqrMagnitude > _externalThermalUpdraftVelocityChange.sqrMagnitude)
            {
                _externalThermalUpdraftVelocityChange = velocityChange;
            }

            _externalThermalUpdraftRequestedThisStep = true;
        }

        /// <summary>
        /// Accepts the latest parasite latch aggregate from the asynchronous GPU readback path.
        /// Center of mass is expected in player-local space so side-biased parasite clusters can pull the hull laterally.
        /// </summary>
        public void ApplyParasiteLatchInfluence(int latchedCount, Vector3 parasiteCenterOfMassLS, Vector3 harvesterPullWS)
        {
            int clampedCount = math.max(0, latchedCount);
            if (!_parasiteLatchRequestedThisStep || clampedCount > _parasiteLatchedRequestedCount)
                _parasiteLatchedRequestedCount = clampedCount;

            _parasiteCenterOfMassRequestedLS = parasiteCenterOfMassLS;
            _parasiteHarvesterPullRequestedWS = harvesterPullWS;
            _parasiteLatchRequestedThisStep = true;
            _parasiteLatchHoldTimer = parasiteLatchInfluenceHoldTime;
        }

        /// <summary>
        /// Applies a fauna-authored hypnosis pull toward one world-space lure source without bypassing the locomotion force pipeline.
        /// </summary>
        public void ApplyFaunaHypnosisPull(Vector3 sourcePosition, float acceleration, float lockDuration)
        {
            if (_rb == null)
                return;

            Vector3 toSource = sourcePosition - _rb.worldCenterOfMass;
            float sqrMagnitude = toSource.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f || acceleration <= 0.0001f)
                return;

            Vector3 centerOfMass = _rb.worldCenterOfMass;
            System.Numerics.Vector3 playerPos = new System.Numerics.Vector3(centerOfMass.x, centerOfMass.y, centerOfMass.z);
            System.Numerics.Vector3 sourcePos = new System.Numerics.Vector3(sourcePosition.x, sourcePosition.y, sourcePosition.z);

            System.Numerics.Vector3 computedForce = Hecton8.PureLogic.Kinematics.FaunaHypnosisPullForceCalculator.Compute(
                playerPos,
                sourcePos,
                acceleration,
                ResolveAuthoritativeBodyMassKg(),
                lockDuration,
                0.0001f,
                0.000001f);

            Vector3 finalForce = new Vector3(computedForce.X, computedForce.Y, computedForce.Z);
            QueueEnvironmentalForce(finalForce);

            float clampedLockDuration = math.max(0f, lockDuration);
            if (clampedLockDuration <= 0.0001f)
                return;

            _wipeoutSeverity = math.max(_wipeoutSeverity, math.saturate(acceleration / 12f));
            _wipeoutTimer = math.max(_wipeoutTimer, clampedLockDuration);
            _stateMachine?.BeginWipeout(_wipeoutSeverity, _wipeoutTimer);
        }

        /// <summary>
        /// Accepts a one-step external hull-stress request from non-pressure hazards such as parasite swarms.
        /// Call continuously while the hazard remains attached to the active hull.
        /// </summary>
        /// <param name="normalizedStress">Requested normalized hull-stress intensity in the 0..1 range.</param>
        public void RequestExternalHullStress(float normalizedStress)
        {
            float clampedStress = math.saturate(normalizedStress);
            if (clampedStress <= 0.0001f)
                return;

            if (!_externalHullStressRequestedThisStep || clampedStress > _externalHullStressRequestedIntensity)
                _externalHullStressRequestedIntensity = clampedStress;

            _externalHullStressRequestedThisStep = true;
        }

        /// <summary>
        /// Forces a transport bailout through the locomotion owner.
        /// Use this when an external damage or hazard system decides active transport control must be stripped immediately.
        /// </summary>
        /// <param name="worldImpulse">World-space bailout impulse. Zero falls back to the authored default ejection impulse.</param>
        /// <param name="severity">Normalized bailout severity 0-1.</param>
        public void ForceTransportBailout(Vector3 worldImpulse, float severity = 1f)
        {
            float clampedSeverity = math.saturate(severity);
            StartWipeout(
                math.max(clampedSeverity, 0.65f),
                ResolveBailoutSpeed(),
                ResolvePlayerAupRuntimePosition(),
                Vector3.up,
                null,
                true,
                worldImpulse);
        }

        private float ResolveBailoutSpeed()
        {
            float threshold = math.max(wipeoutBailoutSpeedThreshold, 0f);
            if (_rb == null)
                return threshold;

            Vector3 linearVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float speedSq = linearVelocity.sqrMagnitude;
            float thresholdSq = threshold * threshold;
            return speedSq > thresholdSq
                ? math.max(threshold, ApproximateVectorMagnitude(linearVelocity))
                : threshold;
        }

        private Vector3 ResolvePlayerAupRuntimePosition()
        {
            float3 runtime3 = _playerState.AbsolutePosition.ToRuntimeFloat3();
            return new Vector3(runtime3.x, runtime3.y, runtime3.z);
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (0.375f * mid) + (0.125f * min);
        }

        private static float ApproximatePlanarMagnitude(float x, float z)
        {
            float ax = math.abs(x);
            float az = math.abs(z);
            float max = math.max(ax, az);
            float min = math.min(ax, az);
            return max + (0.375f * min);
        }

        private static void EnsureDegreeSinCosLutInitialized()
        {
            if (_degreeSinCosLutInitialized)
                return;

            for (int i = 0; i < DegreeSinCosLutSize; i++)
            {
                float radians = i * TwoPi / DegreeSinCosLutSize;
                MathLodApproximation.ApproxSinCosBhaskara(radians, out _degreeSinLut[i], out _degreeCosLut[i]);
            }

            _degreeSinCosLutInitialized = true;
        }

        private static void ResolveDegreesSinCosFast(float degrees, out float sinValue, out float cosValue)
        {
            if (!_degreeSinCosLutInitialized)
                EnsureDegreeSinCosLutInitialized();

            if (!math.isfinite(degrees))
            {
                sinValue = 0f;
                cosValue = 1f;
                return;
            }

            float scaled = degrees * DegreeSinCosLutScale;
            int rounded = (int)(scaled >= 0f ? scaled + 0.5f : scaled - 0.5f);
            int index = rounded & DegreeSinCosLutMask;
            sinValue = _degreeSinLut[index];
            cosValue = _degreeCosLut[index];
        }

        private static float ResolveDegreesCosFast(float degrees)
        {
            ResolveDegreesSinCosFast(degrees, out _, out float cosValue);
            return cosValue;
        }

        private static float SignedTriangleRadians(float radians)
        {
            return SignedTriangle01(radians * InvTwoPi + 0.25f);
        }

        private static float MagnitudeFromRsqrt(Vector3 value)
        {
            float sqrMagnitude =
                value.x * value.x +
                value.y * value.y +
                value.z * value.z;
            return sqrMagnitude > 0.000001f ? sqrMagnitude * math.rsqrt(math.max(sqrMagnitude, 0.000001f)) : 0f;
        }

        private static Vector3 NormalizeVectorRsqrt(Vector3 value, Vector3 fallback)
        {
            float sqrMagnitude =
                value.x * value.x +
                value.y * value.y +
                value.z * value.z;
            if (sqrMagnitude <= 0.000001f)
                return fallback;

            float invMagnitude = math.rsqrt(math.max(sqrMagnitude, 0.000001f));
            return new Vector3(
                value.x * invMagnitude,
                value.y * invMagnitude,
                value.z * invMagnitude);
        }

        private static float DotVector(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        private static Vector3 CrossVector(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }

        private static Vector3 ProjectOnPlaneFast(Vector3 value, Vector3 normal)
        {
            float normalSqr = normal.x * normal.x + normal.y * normal.y + normal.z * normal.z;
            if (normalSqr <= 0.000001f)
                return value;

            return value - normal * (DotVector(value, normal) * math.rcp(normalSqr));
        }

        private static Vector3 ProjectVelocityOntoGroundPlanePreserveMagnitude(Vector3 velocity, Vector3 groundNormal)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float3 normal3 = new float3(groundNormal.x, groundNormal.y, groundNormal.z);
            if (!math.all(math.isfinite(velocity3)) || !math.all(math.isfinite(normal3)))
                return Vector3.zero;

            float normalSq = math.lengthsq(normal3);
            float velocitySq = math.lengthsq(velocity3);
            if (normalSq <= 0.000001f || velocitySq <= 0.000001f)
                return new Vector3(velocity3.x, velocity3.y, velocity3.z);

            normal3 *= math.rsqrt(math.max(normalSq, 0.000001f));
            float3 projected = velocity3 - math.project(velocity3, normal3);
            float projectedSq = math.lengthsq(projected);
            if (projectedSq <= 0.000001f)
                return Vector3.zero;

            float speed = velocitySq * math.rsqrt(math.max(velocitySq, 0.000001f));
            projected *= speed * math.rsqrt(math.max(projectedSq, 0.000001f));
            return new Vector3(projected.x, projected.y, projected.z);
        }

        private static Vector3 SuppressGroundPenetratingVelocity(Vector3 velocity, Vector3 groundNormal)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float3 normal3 = new float3(groundNormal.x, groundNormal.y, groundNormal.z);
            if (!math.all(math.isfinite(velocity3)) || !math.all(math.isfinite(normal3)))
                return Vector3.zero;

            float normalSq = math.lengthsq(normal3);
            if (normalSq <= 0.000001f)
                return new Vector3(velocity3.x, velocity3.y, velocity3.z);

            normal3 *= math.rsqrt(math.max(normalSq, 0.000001f));
            float normalVelocity = math.dot(velocity3, normal3);
            if (normalVelocity >= 0f)
                return new Vector3(velocity3.x, velocity3.y, velocity3.z);

            velocity3 -= normal3 * normalVelocity;
            return new Vector3(velocity3.x, velocity3.y, velocity3.z);
        }

        private static Vector3 FastLerpNormal(Vector3 from, Vector3 to, float t, Vector3 fallback)
        {
            float safeT = math.saturate(t);
            return NormalizeVectorRsqrt(
                new Vector3(
                    math.lerp(from.x, to.x, safeT),
                    math.lerp(from.y, to.y, safeT),
                    math.lerp(from.z, to.z, safeT)),
                fallback);
        }

        private static Quaternion FastLerpQuaternion(Quaternion from, Quaternion to, float t)
        {
            float safeT = math.saturate(t);
            float dot =
                from.x * to.x +
                from.y * to.y +
                from.z * to.z +
                from.w * to.w;
            if (dot < 0f)
            {
                to.x = -to.x;
                to.y = -to.y;
                to.z = -to.z;
                to.w = -to.w;
            }

            float x = math.lerp(from.x, to.x, safeT);
            float y = math.lerp(from.y, to.y, safeT);
            float z = math.lerp(from.z, to.z, safeT);
            float w = math.lerp(from.w, to.w, safeT);
            float lengthSq = x * x + y * y + z * z + w * w;
            if (lengthSq <= 0.000001f)
                return Quaternion.identity;

            float invLength = math.rsqrt(math.max(lengthSq, 0.000001f));
            return new Quaternion(x * invLength, y * invLength, z * invLength, w * invLength);
        }


        private void EnsurePlayerRuntimeSubsystems()
        {
            if (!TryGetComponent(out _playerMotor))
            {
                _playerMotor = gameObject.AddComponent<HectonPlayerMotor>(); // COLD ALLOC: HectonPlayerMotor[1] - locomotion rigidbody force bridge - owner: HectonPlayerMovement
            }

            if (!TryGetComponent(out _environmentHandler))
            {
                _environmentHandler = gameObject.AddComponent<HectonPlayerEnvironmentHandler>(); // COLD ALLOC: HectonPlayerEnvironmentHandler[1] - environment acceleration/velocity buffer - owner: HectonPlayerMovement
            }

            if (!TryGetComponent(out _stateMachine))
            {
                _stateMachine = gameObject.AddComponent<HectonPlayerStateMachine>(); // COLD ALLOC: HectonPlayerStateMachine[1] - locomotion mode and wipeout state mirror - owner: HectonPlayerMovement
            }

            if (!TryGetComponent(out _waterTransitionHandler))
            {
                _waterTransitionHandler = gameObject.AddComponent<WaterTransitionHandler>(); // COLD ALLOC: WaterTransitionHandler[1] - event-driven water entry/exit gravity owner - owner: HectonPlayerMovement
            }

            if (_playerMotor != null)
                _playerMotor.Bind(_rb, _capsuleCollider);

            if (_environmentHandler != null)
                _environmentHandler.Bind(this, _playerMotor);

            if (_stateMachine != null)
            {
                _stateMachine.SyncLocomotionMode(_currentLocomotionMode);
                if (_wipeoutTimer > 0f)
                    _stateMachine.BeginWipeout(_wipeoutSeverity, _wipeoutTimer);
            }

            ConfigureWaterTransitionHandler();
        }

        private void ConfigureWaterTransitionHandler()
        {
            if (_waterTransitionHandler == null)
                return;

            _waterTransitionHandler.Bind(this);
            _waterTransitionHandler.ConfigureSurfaceBreachGravity(
                surfaceBreachGravitySpikeDelay,
                surfaceBreachGravitySpikeAcceleration,
                surfaceBreachGravitySpikeDuration);
        }

        private void QueueEnvironmentalForce(Vector3 force)
        {
            if (_rb == null)
                return;

            if (_environmentHandler == null)
            {
                ApplyMotorForce(force);
                return;
            }

            float mass = ResolveAuthoritativeBodyMassKg();
            _environmentHandler.QueueExternalAcceleration(force / mass);
        }

        private void QueueEnvironmentalVelocityChange(Vector3 velocityChange)
        {
            if (_rb == null)
                return;

            if (_environmentHandler == null)
            {
                ApplyMotorVelocityChange(velocityChange);
                return;
            }

            _environmentHandler.QueueVelocityChange(velocityChange);
        }

        private void ApplyMotorForce(Vector3 force)
        {
            _playerMotor?.ApplyForce(force);
        }

        private void ApplyMotorAcceleration(Vector3 acceleration)
        {
            _playerMotor?.ApplyAcceleration(acceleration);
        }

        internal void QueueSubsystemExternalAcceleration(Vector3 acceleration)
        {
            Vector3 safeAcceleration = HectonPlayerMotor.SafeVelocity(acceleration);
            if (safeAcceleration.sqrMagnitude <= 0.000001f)
                return;

            _queuedExternalKinematicAcceleration += safeAcceleration;
        }

        internal void QueueSubsystemExternalVelocityChange(Vector3 velocityChange)
        {
            Vector3 safeVelocityChange = HectonPlayerMotor.SafeVelocity(velocityChange);
            if (safeVelocityChange.sqrMagnitude <= 0.000001f)
                return;

            _queuedExternalKinematicVelocityChange += safeVelocityChange;
        }

        public void RequestLocalGravityOverride(Vector3 gravityVector, float holdSeconds)
        {
            Vector3 safeGravity = HectonPlayerMotor.SafeVelocity(gravityVector);
            if (safeGravity.sqrMagnitude <= MinLocalGravitySqr || holdSeconds <= 0f)
                return;

            bool sameTarget = _localGravityOverrideActive &&
                              (_localGravityOverride - safeGravity).sqrMagnitude <= LocalGravityRetargetEpsilonSqr;
            if (!sameTarget)
            {
                _localGravityOverrideBlendStart = ResolveCurrentGravityForOverrideBlend();
                _localGravityOverrideBlendTimer = 0f;
            }

            _localGravityOverride = safeGravity;
            _localGravityOverrideTimer = math.max(_localGravityOverrideTimer, holdSeconds);
            _localGravityOverrideActive = true;
        }

        internal void TriggerHypoxiaVisorDistortion(float intensity, float holdDuration, float recoverySpeed)
        {
            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity <= 0f)
                return;

            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                    controller.TriggerEnvironmentalDistortion(clampedIntensity, math.max(0.01f, holdDuration), math.max(0.01f, recoverySpeed));
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private void ApplyMotorAccelerationFromForce(Vector3 force)
        {
            if (_rb == null)
                return;

            float invMass = math.rcp(ResolveAuthoritativeBodyMassKg());
            ApplyMotorAcceleration(force * invMass);
        }

        private void ApplyMotorVelocityChange(Vector3 velocityChange)
        {
            ArmSpeculativeCcdForExtremeVelocityChange(velocityChange);
            _playerMotor?.ApplyVelocityChange(velocityChange);
        }

        private void ApplyMotorImpulse(Vector3 impulse)
        {
            _playerMotor?.ApplyImpulse(impulse);
        }

        private void ApplyMotorAngularVelocityChange(Vector3 angularVelocityChange, float maxAngularAcceleration, float fixedDeltaTime)
        {
            _playerMotor?.ApplyAngularVelocityChange(angularVelocityChange, maxAngularAcceleration, fixedDeltaTime);
        }

        private void ApplyMotorOffCenterForce(Vector3 force, Vector3 applicationPoint)
        {
            _playerMotor?.ApplyForceAtPositionSplit(
                force,
                applicationPoint,
                ParasiteLatchMaxLeverArm,
                ParasiteLatchMaxAngularAcceleration);
        }

        private void ApplyMotorLinearVelocity(Vector3 velocity)
        {
            _velocity = HectonPlayerMotor.SafeVelocity(velocity, _velocity);
            _playerMotor?.SetLinearVelocity(_velocity);
        }

        private void ApplyQueuedExternalKinematicForces(float fixedDeltaTime, bool applyToMotor)
        {
            Vector3 queuedAcceleration = _queuedExternalKinematicAcceleration;
            Vector3 queuedVelocityChange = _queuedExternalKinematicVelocityChange;
            _playerState.SyncExternalKinematic(queuedAcceleration, queuedVelocityChange);
            _queuedExternalKinematicAcceleration = Vector3.zero;
            _queuedExternalKinematicVelocityChange = Vector3.zero;

            if (!applyToMotor || fixedDeltaTime <= 0f)
                return;

            Vector3 totalVelocityChange = queuedVelocityChange + (queuedAcceleration * fixedDeltaTime);
            totalVelocityChange = HectonPlayerMotor.SafeVelocity(totalVelocityChange);
            if (totalVelocityChange.sqrMagnitude <= 0.000001f)
                return;

            if (_playerMotor != null && _playerMotor.HydrodynamicKccOwnsCollisionAuthority)
            {
                ApplyMotorVelocityChange(totalVelocityChange);
                return;
            }

            if (_rb == null)
                return;

            ArmSpeculativeCcdForExtremeVelocityChange(totalVelocityChange);
            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 nextVelocity = HectonPlayerMotor.SafeVelocity(currentVelocity + totalVelocityChange, currentVelocity);
            ApplyMotorLinearVelocity(nextVelocity);
        }

        private void ArmSpeculativeCcdForExtremeVelocityChange(Vector3 velocityChange)
        {
            if (_rb == null)
                return;

            Vector3 safeVelocityChange = HectonPlayerMotor.SafeVelocity(velocityChange);
            if (safeVelocityChange.sqrMagnitude < SpeculativeCcdImpulseThresholdMetersPerSecondSq)
                return;

            IPhysicsStateEventService physicsStateEvents = _physicsStateEvents;
            physicsStateEvents?.ArmSpeculativeCcdForImpulse(_rb);
        }

        private float ResolveSpeculativeHoverHeightMeters(float baseHeightMeters, float timeSeconds)
        {
            IPhysicsStateEventService physicsStateEvents = _physicsStateEvents;
            return physicsStateEvents != null
                ? physicsStateEvents.ResolveSpeculativeHoverHeightMeters(baseHeightMeters, timeSeconds)
                : Hecton8.PureLogic.Kinematics.SinusoidalHoverBobbingCalculator.Compute(baseHeightMeters, timeSeconds, SpeculativeHoverBobbingFrequency, SpeculativeHoverBobbingAmplitude);
        }

        private void MoveMotorPosition(Vector3 position)
        {
            if (_playerMotor == null)
                return;

            _playerMotor.MovePosition(position);
            if (_useFixedFrameSpatialCache)
                SyncFixedFrameMotorPosition(position);
        }

        private void MoveMotorRotation(Quaternion rotation)
        {
            if (_playerMotor == null)
                return;

            _playerMotor.MoveRotation(rotation);
        }

        private void EnsurePlayerKinematicsNativeState()
        {
            _playerKinematicsNativeState.EnsureCreated(_dataVault);
        }

        private bool HasPlayerKinematicsNativeState()
        {
            return _playerKinematicsNativeState.IsCreated(_dataVault);
        }

        private void EnsureCinematicFocusBlackBox()
        {
            if (TryResolveCinematicFocusBlackBox(out _))
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (vault.IsCompactionFenceActive)
            {
                ClearCinematicFocusBlackBoxDescriptor();
                return;
            }

            if (vault.TryGetGenerationHandle<CinematicFocusTelemetryEntry>(
                    BufferID.PlayerCinematicFocusBlackBox,
                    out VaultGenerationHandle<CinematicFocusTelemetryEntry> borrowedHandle) &&
                vault.TryReadHandle(in borrowedHandle, out NativeArray<CinematicFocusTelemetryEntry> blackBox) &&
                blackBox.IsCreated &&
                blackBox.Length >= CinematicFocusBlackBoxCapacity)
            {
                _cinematicFocusBlackBoxHandle = borrowedHandle;
                _ownsCinematicFocusBlackBox = false;
                return;
            }

            if (vault.IsAllocationLocked)
            {
                ClearCinematicFocusBlackBoxDescriptor();
                return;
            }

            VaultGenerationHandle<CinematicFocusTelemetryEntry> acquiredHandle = vault.EnsureGenerationHandle<CinematicFocusTelemetryEntry>(
                BufferID.PlayerCinematicFocusBlackBox,
                CinematicFocusBlackBoxCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            bool ownsAcquiredHandle = true;

            if (!IsVaultHandleCreated(in acquiredHandle) ||
                !vault.TryReadHandle(in acquiredHandle, out blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < CinematicFocusBlackBoxCapacity)
            {
                if (IsVaultHandleCreated(in acquiredHandle) && ownsAcquiredHandle)
                    vault.ReleaseBuffer(in acquiredHandle);

                ClearCinematicFocusBlackBoxDescriptor();
                return;
            }

            _cinematicFocusBlackBoxHandle = acquiredHandle;
            _ownsCinematicFocusBlackBox = ownsAcquiredHandle;
        }

        NativeArray<CinematicFocusTelemetryEntry> ResolveCinematicFocusBlackBox()
        {
            return TryResolveCinematicFocusBlackBox(out NativeArray<CinematicFocusTelemetryEntry> blackBox)
                ? blackBox
                : default;
        }

        private bool TryResolveCinematicFocusBlackBox(out NativeArray<CinematicFocusTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in _cinematicFocusBlackBoxHandle))
                return false;

            if (!vault.TryReadHandle(in _cinematicFocusBlackBoxHandle, out blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < CinematicFocusBlackBoxCapacity)
            {
                ClearCinematicFocusBlackBoxDescriptor();
                return false;
            }

            return true;
        }

        private void DisposeCinematicFocusBlackBox()
        {
            ReleaseCinematicFocusBlackBoxBuffer(_dataVault);
            _cinematicFocusBlackBoxCursor = 0;
            _cinematicFocusBlackBoxCount = 0;
        }

        private void ReleaseCinematicFocusBlackBoxBuffer(IDataVault vault)
        {
            if (_ownsCinematicFocusBlackBox &&
                vault != null &&
                IsVaultHandleCreated(in _cinematicFocusBlackBoxHandle))
            {
                vault.ReleaseBuffer(in _cinematicFocusBlackBoxHandle);
            }

            ClearCinematicFocusBlackBoxDescriptor();
        }

        private void ClearCinematicFocusBlackBoxDescriptor()
        {
            _cinematicFocusBlackBoxHandle = default;
            _ownsCinematicFocusBlackBox = false;
        }

        private static bool IsVaultHandleCreated(in VaultGenerationHandle<CinematicFocusTelemetryEntry> handle)
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void RefreshCinematicFocusTierGateCold()
        {
            _cinematicFocusFovQualityWeight01 = SmoothQuality01(ResolveMovementQualityWeight01());
        }

        private static float ResolveMovementQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static float SmoothQuality01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - (2f * t));
        }

        private float3 ResolveRawInputIntentVector()
        {
            return new float3(_inputH, _inputVertical, _inputV);
        }

        private void WritePlayerKinematicsSnapshot(Vector3 position, Vector3 velocity, float3 intendedMovement)
        {
            if (!HasPlayerKinematicsNativeState())
                return;

            float3 position3 = new float3(position.x, position.y, position.z);
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            if (!math.all(math.isfinite(position3)) ||
                !math.all(math.isfinite(velocity3)) ||
                !math.all(math.isfinite(intendedMovement)))
            {
                DumpPlayerKinematicsBlackBox(_playerKinematicsNaNHash);
                return;
            }

            _playerKinematicsNativeState.WriteKinematicSnapshot(_dataVault, position3, velocity3, intendedMovement);
        }

        private Vector3 ResolvePlayerKinematicsBurstDragVelocity(
            Vector3 velocity,
            float3 intendedMovement,
            float dragCoefficient,
            float waterDensityScale,
            float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return velocity;

            if (!HasPlayerKinematicsNativeState())
                return velocity;

            WritePlayerKinematicsSnapshot(ResolveBodyRuntimePosition(), velocity, intendedMovement);
            if (!_playerKinematicsNativeState.TryResolveDragArrays(
                    _dataVault,
                    out NativeArray<float3> kinematicVelocities,
                    out NativeArray<float3> dragSolvedVelocities))
                return velocity;

            PlayerKinematicsLinearDragJob dragJob = new PlayerKinematicsLinearDragJob
            {
                Velocities = kinematicVelocities,
                SolvedVelocities = dragSolvedVelocities,
                DragCoefficient = dragCoefficient,
                WaterDensityScale = waterDensityScale,
                DeltaTime = fixedDeltaTime
            };
            // HOT SCALAR CONTROL KERNEL: same-frame player velocity is consumed immediately; Execute removes the synchronous job runner without a fake schedule fence.
            dragJob.Execute();

            float3 solvedVelocity = dragSolvedVelocities[0];
            if (!math.all(math.isfinite(solvedVelocity)))
            {
                DumpPlayerKinematicsBlackBox(_playerKinematicsNaNHash);
                return HectonPlayerMotor.SafeVelocity(velocity);
            }

            _lastPlayerKinematicsBurstDragVelocity = solvedVelocity;
            return HectonPlayerMotor.SafeVelocity(
                new Vector3(solvedVelocity.x, solvedVelocity.y, solvedVelocity.z),
                velocity);
        }

        private uint ResolvePlayerKinematicsTelemetryFlags()
        {
            uint flags = 0u;
            flags |= math.select(0u, 1u, !_isWalking);
            flags |= math.select(0u, 2u, _waterImmersionRatio > 0.01f);
            flags |= math.select(0u, 4u, _lastPlayerKinematicsDragCoefficient > 0f && _waterImmersionRatio > 0.01f);
            flags |= math.select(0u, 8u, _ladderSplineSnapActive);
            flags |= math.select(0u, 16u, _isSurfaceSwimming);
            flags |= math.select(0u, 32u, _isInsideBrineLayer);
            return flags;
        }

        private void PublishMovementAcousticSignal(Vector3 velocity)
        {
            float velocitySq = velocity.sqrMagnitude;
            if (velocitySq <= MovementAcousticMinVelocitySq)
                return;

            MovementAcousticSignal signal = default;
            signal.PositionAup = _playerState.AbsolutePosition;
            signal.Volume = math.saturate(velocitySq * MovementAcousticVolumeScale);
            signal.VelocitySq = velocitySq;
            signal.SourceId = _playerKinematicsSourceId;
            signal.LocomotionMode = (byte)_currentLocomotionMode;
            signal.SurfaceMode = (byte)math.select(0, 1, _isSurfaceSwimming);
            signal.Flags = (byte)(ResolvePlayerKinematicsTelemetryFlags() & 0xFFu);
            QueueMovementAcousticSignal(in signal);
        }

        private void UpdateBrineLayerState(float fixedDeltaTime)
        {
            _wasInsideBrineLayer = _isInsideBrineLayer;
            _isInsideBrineLayer = false;
            _lastBrineLayerSample = default;

            Vector3 runtimePosition = ResolveBodyRuntimePosition();
            ResourceDistributionDirector director = _resourceDistributionRuntime;
            if (!TryResolvePlayerRuntimeShiftOffsetY(runtimePosition, out float shiftOffsetY))
            {
                _brineSubmersionSeconds = 0f;
                PublishInactiveBrineShaderGlobals();
                return;
            }

            if (PlayerMovementBrineRuntimeSystem.TrySampleBrineLayer(
                    director,
                    runtimePosition,
                    IsInDryInterior(),
                    shiftOffsetY,
                    out BrineLayerSample sample,
                    out bool submerged))
            {
                _lastBrineLayerSample = sample;
                _isInsideBrineLayer = submerged;
                PublishBrineShaderGlobals(sample, shiftOffsetY);
                if (submerged)
                {
                    _brineSubmersionSeconds += math.max(0f, fixedDeltaTime);
                    TryApplyBrineGasToxicity();
                }
                else
                {
                    _brineSubmersionSeconds = 0f;
                }
            }
            else
            {
                _brineSubmersionSeconds = 0f;
                PublishInactiveBrineShaderGlobals();
            }

            if (_isInsideBrineLayer != _wasInsideBrineLayer)
                PublishFluidDensityChanged(runtimePosition, _lastBrineLayerSample, _isInsideBrineLayer);
        }

        private void TryApplyBrineGasToxicity()
        {
            IGasDynamicsSolver gas = _gasDynamicsRuntime;
            gas?.TryApplyPlayerRoomCarbonDioxideEquivalentPressure(BrineLayerConstants.CarbonDioxideEquivalentKPa);
        }

        private void PublishBrineShaderGlobals(BrineLayerSample sample, float shiftOffsetY)
        {
            float runtimeHeightY = BrineLayerMath.ResolveRuntimeHeightY(sample.AbsoluteHeightY, shiftOffsetY);
            if (!math.isfinite(runtimeHeightY))
                return;

            _pendingBrineRuntimeHeightY = runtimeHeightY;
            float4 color = BrineLayerConstants.DefaultBrineColor;
            _pendingBrineColor = new Vector4(color.x, color.y, color.z, color.w);
            _pendingBrineFogHardClip = PlayerMovementBrineRuntimeSystem.ResolveFogHardClip(ResolveMovementQualityWeight01());
            _pendingBrineShaderGlobalsActive = true;
            _pendingBrineShaderGlobalsDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void PublishInactiveBrineShaderGlobals()
        {
            _pendingBrineRuntimeHeightY = -1000000f;
            _pendingBrineColor = Vector4.zero;
            _pendingBrineFogHardClip = 0f;
            _pendingBrineShaderGlobalsActive = false;
            _pendingBrineShaderGlobalsDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void FlushBrineShaderGlobals()
        {
            if (!_pendingBrineShaderGlobalsDirty)
                return;

            _pendingBrineShaderGlobalsDirty = false;
            if (!_pendingBrineShaderGlobalsActive)
            {
                if (_lastPublishedBrineRuntimeHeightY > -999999f)
                {
                    Shader.SetGlobalFloat(BrineHeightYId, -1000000f);
                    _lastPublishedBrineRuntimeHeightY = float.NegativeInfinity;
                }

                if (_lastPublishedBrineColorAlpha != 0f)
                {
                    Shader.SetGlobalVector(BrineColorId, Vector4.zero);
                    _lastPublishedBrineColorAlpha = 0f;
                }

                if (_lastPublishedBrineFogHardClip != 0f)
                {
                    Shader.SetGlobalFloat(BrineFogHardClipId, 0f);
                    _lastPublishedBrineFogHardClip = 0f;
                }

                return;
            }

            float runtimeHeightY = _pendingBrineRuntimeHeightY;
            if (!math.isfinite(runtimeHeightY))
                return;

            if (math.abs(runtimeHeightY - _lastPublishedBrineRuntimeHeightY) > 0.001f)
            {
                Shader.SetGlobalFloat(BrineHeightYId, runtimeHeightY);
                _lastPublishedBrineRuntimeHeightY = runtimeHeightY;
            }

            Vector4 color = _pendingBrineColor;
            if (math.abs(color.w - _lastPublishedBrineColorAlpha) > 0.001f)
            {
                Shader.SetGlobalVector(BrineColorId, color);
                _lastPublishedBrineColorAlpha = color.w;
            }

            float hardClip = _pendingBrineFogHardClip;
            if (math.abs(hardClip - _lastPublishedBrineFogHardClip) > 0.001f)
            {
                Shader.SetGlobalFloat(BrineFogHardClipId, hardClip);
                _lastPublishedBrineFogHardClip = hardClip;
            }
        }

        private void PublishFluidDensityChanged(Vector3 runtimePosition, BrineLayerSample sample, bool entered)
        {
            if (!TryResolveAupFromPlayerStateRuntimeDelta(runtimePosition, out AbsoluteUniversePosition positionAup))
                return;

            FluidDensityChangedSignal signal = default;
            signal.PositionAup = positionAup;
            signal.DensityMultiplier = entered ? BrineLayerConstants.DensityMultiplier : 1f;
            signal.BrineHeightY = sample.AbsoluteHeightY;
            signal.SubmersionSeconds = entered ? _brineSubmersionSeconds : 0f;
            signal.Flags = entered
                ? (byte)(BrineLayerConstants.SampleValidFlag | BrineLayerConstants.SubmergedFlag | BrineLayerConstants.EnteredFlag)
                : BrineLayerConstants.ExitedFlag;
            signal.FluidKind = BrineLayerConstants.FluidKindBrine;
            signal.SectorHash = sample.SectorHash;
            SignalBus<FluidDensityChangedSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void SyncSwimVatSpeedScalar(Vector3 velocity, SuitData suit)
        {
            ResolveSwimPresentationController();
            if (_swimPresentationController == null || suit == null)
                return;

            float maxSpeed = math.max(0.01f, suit.maxSwimSpeed);
            float speedScalar = math.saturate(ApproximateVectorMagnitude(velocity) / maxSpeed);
            _swimPresentationController.SyncGpuVatSwimSpeedScalar(speedScalar);
        }

        private void PushMovementStaminaBurnInput()
        {
            if (_survivalSystem == null)
                return;

            float intendedLengthSq = math.lengthsq(_lastPlayerKinematicsIntendedMovement);
            _survivalSystem.SetMovementStaminaBurnInput(intendedLengthSq, MovementStaminaDrainMultiplier);
        }

        private float ResolveEquipmentDragCoefficientMultiplier()
        {
            ulong inventoryMask = _inventoryLoadSource != null ? _inventoryLoadSource.CurrentInventoryMask : 0UL;
            ulong heavyMask = inventoryMask & ResolveHeavyInventoryDragMask();
            int heavyBitCount = CountSetBits64(heavyMask);
            float maskDrag = math.min(0.65f, heavyBitCount * HeavyInventoryDragPerMaskBit);
            float loadDrag = _runtimeInventoryLoad01 * InventoryLoadDragMultiplierMax;
            return 1f + maskDrag + loadDrag;
        }

        private static ulong ResolveHeavyInventoryDragMask()
        {
            if (!ItemTemplateRegistry.IsInitialized)
            {
                s_heavyInventoryDragMask = 0UL;
                s_heavyInventoryDragTemplateCount = -1;
                s_heavyInventoryDragRegistryRevision = ItemTemplateRegistry.Revision;
                return 0UL;
            }

            int templateCount = ItemTemplateRegistry.Count;
            uint registryRevision = ItemTemplateRegistry.Revision;
            if (s_heavyInventoryDragTemplateCount == templateCount &&
                s_heavyInventoryDragRegistryRevision == registryRevision)
                return s_heavyInventoryDragMask;

            ulong mask = 0UL;
            System.ReadOnlySpan<ItemTemplate> templates = ItemTemplateRegistry.Templates;
            int count = math.min(templateCount, templates.Length);
            for (int i = 0; i < count; i++)
            {
                ItemTemplate template = templates[i];
                if (template.IsValid && template.MassKg >= HeavyInventoryItemMassThresholdKg)
                    mask |= InventoryMaterialMask.ResolveBit(template.HashID);
            }

            s_heavyInventoryDragMask = mask;
            s_heavyInventoryDragTemplateCount = templateCount;
            s_heavyInventoryDragRegistryRevision = registryRevision;
            return mask;
        }

        private static int CountSetBits64(ulong value)
        {
            return math.countbits((uint)value) + math.countbits((uint)(value >> 32));
        }

        private void ApplyVoxelNoClipFailsafe(bool suppressPhysicsMutation)
        {
            if (_rb == null)
                return;

            Vector3 runtimePosition = ResolveBodyRuntimePosition();
            float3 position3 = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(position3)))
            {
                if (suppressPhysicsMutation)
                {
                    DumpPlayerKinematicsBlackBox(_playerKinematicsNaNHash);
                    return;
                }

                RecoverPlayerKinematicsToLastValidAup(_playerKinematicsNaNHash);
                return;
            }

            bool solidNavGrid =
                VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position3, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.SolidVoxel;
            bool solidSdf = !solidNavGrid && TrySampleActiveVoxelSdfSolid(runtimePosition);
            if (!solidNavGrid && !solidSdf)
            {
                if (TryResolveAupFromPlayerStateRuntimeDelta(runtimePosition, out AbsoluteUniversePosition validAup))
                    RecordLastValidAup(validAup);
                _playerKinematicsTelemetryDumpedThisFault = false;
                return;
            }

            Vector3 safeRuntimePosition;
            if (!TryResolveLastValidAupRuntimePosition(out safeRuntimePosition) &&
                !VoxelDynamicNavGridRuntime.TryResolveNearestSafeNode(runtimePosition, out safeRuntimePosition))
            {
                DumpPlayerKinematicsBlackBox(_playerKinematicsNoClipHash);
                return;
            }

            if (!IsFiniteVector(safeRuntimePosition))
            {
                DumpPlayerKinematicsBlackBox(_playerKinematicsNoClipHash);
                return;
            }

            if (suppressPhysicsMutation)
            {
                DumpPlayerKinematicsBlackBox(_playerKinematicsNoClipHash);
                return;
            }

            MoveMotorPosition(safeRuntimePosition);
            ApplyMotorLinearVelocity(Vector3.zero);
            _playerState.SyncKinematic(safeRuntimePosition, Vector3.zero);
            WritePlayerKinematicsSnapshot(safeRuntimePosition, Vector3.zero, float3.zero);
            DumpPlayerKinematicsBlackBox(_playerKinematicsNoClipHash);
        }

        private void RecoverPlayerKinematicsToLastValidAup(uint anomalyHash)
        {
            if (TryResolveLastValidAupRuntimePosition(out Vector3 safeRuntimePosition) &&
                IsFiniteVector(safeRuntimePosition))
            {
                MoveMotorPosition(safeRuntimePosition);
                ApplyMotorLinearVelocity(Vector3.zero);
                _playerState.SyncKinematic(safeRuntimePosition, Vector3.zero);
                WritePlayerKinematicsSnapshot(safeRuntimePosition, Vector3.zero, float3.zero);
            }

            DumpPlayerKinematicsBlackBox(anomalyHash);
        }

        private bool TrySampleActiveVoxelSdfSolid(Vector3 runtimePosition)
        {
            HectonVoxelEngine voxelEngine = _voxelEngineRuntime;
            if (voxelEngine == null || voxelEngine.ActiveVolumeCount <= 0)
                return false;

            if (!voxelEngine.TryGetNearestActiveVolume(runtimePosition, out Hecton8.Caves.HectonVoxelVolume volume) ||
                volume == null)
            {
                return false;
            }

            if (!IsInsidePublishedVoxelSdfBounds(volume, runtimePosition))
                return false;

            return volume.TrySampleDensity(runtimePosition, out float density, out float density01) &&
                   (density > 0f || density01 >= 0.5f);
        }

        private static bool IsInsidePublishedVoxelSdfBounds(Hecton8.Caves.HectonVoxelVolume volume, Vector3 runtimePosition)
        {
            if (volume == null ||
                !volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out Unity.Collections.NativeArray<byte>.ReadOnly _,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float _,
                    out int _,
                    out Hecton8.Caves.HectonVoxelVolume.PublishedSonarSdfReadLease lease))
            {
                return false;
            }

            try
            {
                if (gridDimensions.x <= 1 ||
                    gridDimensions.y <= 1 ||
                    gridDimensions.z <= 1)
                {
                    return false;
                }

                float3 sample = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                float3 origin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z);
                float3 cellSize = new float3(
                    math.max(0.0001f, math.abs(voxelCellSize.x)),
                    math.max(0.0001f, math.abs(voxelCellSize.y)),
                    math.max(0.0001f, math.abs(voxelCellSize.z)));

                if (!math.all(math.isfinite(sample)) ||
                    !math.all(math.isfinite(origin)) ||
                    !math.all(math.isfinite(cellSize)))
                {
                    return false;
                }

                float3 min = origin - cellSize * 0.5f;
                float3 max = origin + cellSize * new float3(
                    gridDimensions.x - 0.5f,
                    gridDimensions.y - 0.5f,
                    gridDimensions.z - 0.5f);

                return sample.x >= min.x && sample.x <= max.x &&
                       sample.y >= min.y && sample.y <= max.y &&
                       sample.z >= min.z && sample.z <= max.z;
            }
            finally
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in lease);
            }
        }

        private void RecordLastValidAup(AbsoluteUniversePosition aup)
        {
            _lastValidAupRing[_lastValidAupWriteIndex] = aup;
            _lastValidAupWriteIndex = (_lastValidAupWriteIndex + 1) % LastValidAupRingCapacity;
            if (_lastValidAupCount < LastValidAupRingCapacity)
                _lastValidAupCount++;
        }

        private void RebaseLastValidAupRuntimeRing(Vector3 shiftOffset)
        {
            // Last-valid recovery stores AUPs, not runtime-space vectors. The origin shift
            // changes their runtime projection automatically through HectonFloatingOrigin.
            _ = shiftOffset;
        }

        private bool TryResolveLastValidAupRuntimePosition(out Vector3 runtimePosition)
        {
            runtimePosition = Vector3.zero;
            if (_lastValidAupCount <= 0)
                return false;

            int index = _lastValidAupWriteIndex - 1;
            if (index < 0)
                index = LastValidAupRingCapacity - 1;

            float3 runtime = _lastValidAupRing[index].ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtime)))
                return false;

            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private void DumpPlayerKinematicsBlackBox(uint anomalyHash)
        {
            if (_playerKinematicsTelemetryDumpedThisFault ||
                !_playerKinematicsNativeState.TryResolveTelemetryRing(
                    _dataVault,
                    out NativeArray<PlayerKinematicsTelemetryEntry> telemetryRing))
                return;

            TelemetryAnomalySignal anomaly = default;
            anomaly.SystemHash = _playerKinematicsSourceId;
            anomaly.AnomalyHash = anomalyHash;
            anomaly.Scalar = _playerKinematicsNativeState.TelemetryFrameSequence;
            anomaly.Frame = SystemDispatcher.CurrentFrameId;
            anomaly.Severity = 2;
            SignalBus<TelemetryAnomalySignal>.TryPushTracked(in anomaly, ref _signalPushDropCount);

            const int HeaderBytes = 8;
            const int RowBytes = 52;
            int totalBytes = HeaderBytes + telemetryRing.Length * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(HectonPlayerMovement),
                "PlayerKinematicsTelemetryDumpPayload");
            try
            {
                WriteUInt32LittleEndian(payload, 0, _playerKinematicsNativeState.TelemetryFrameSequence);
                WriteInt32LittleEndian(payload, 4, _playerKinematicsNativeState.TelemetryWriteIndex);
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    PlayerKinematicsTelemetryEntry entry = telemetryRing[i];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteFloat3LittleEndian(payload, offset, entry.Position);
                    WriteFloat3LittleEndian(payload, offset + 12, entry.Velocity);
                    WriteFloat3LittleEndian(payload, offset + 24, entry.IntendedMovement);
                    WriteFloat32LittleEndian(payload, offset + 36, entry.DragCoefficient);
                    WriteFloat32LittleEndian(payload, offset + 40, entry.WaterDensityScale);
                    WriteUInt32LittleEndian(payload, offset + 44, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 48, entry.Flags);
                }

                _playerKinematicsTelemetryDumpedThisFault = NativeFaultDumpWriter.TryWriteAll(
                    "Docs/AgentLogs/Dump_PLAYER_KINEMATICS.bin",
                    payload,
                    totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonPlayerMovement),
                    "PlayerKinematicsTelemetryDumpPayload");
            }
        }

        private static int GetHitColliderInstanceId(in PlayerMovementSurfaceHit hit)
        {
            return hit.ColliderInstanceId != 0
                ? hit.ColliderInstanceId
                : hit.collider != null
                    ? unchecked((int)EntityId.ToULong(hit.collider.GetEntityId()))
                    : 0;
        }

        private void BuildFixedFrameSweepCapsule(out Vector3 point1, out Vector3 point2, out float radius)
        {
            BuildFixedFrameSweepCapsuleAtPosition(_fixedFrameBodyPosition, out point1, out point2, out radius);
        }

        private void BuildFixedFrameSweepCapsuleAtPosition(Vector3 bodyPosition, out Vector3 point1, out Vector3 point2, out float radius)
        {
            Vector3 up = _fixedFrameBodyRotation * Vector3.up;
            radius = _fixedFrameCapsuleRadius;
            float segmentHalf = math.max(0f, _fixedFrameCapsuleHalfHeight - _fixedFrameCapsuleRadius);
            Vector3 centerOffset = _fixedFrameCapsuleCenterWS - _fixedFrameBodyPosition;
            Vector3 center = bodyPosition + centerOffset;
            point1 = center + up * segmentHalf;
            point2 = center - up * segmentHalf;
        }

        private static int ResolveStableEntitySeed32(GameObject owner)
        {
            if (owner == null)
                return 1;

            ulong entityId = EntityId.ToULong(owner.GetEntityId());
            uint folded = unchecked((uint)(entityId ^ (entityId >> 32)));
            int seed = unchecked((int)(folded & 0x7FFFFFFFu));
            return seed != 0 ? seed : 1;
        }

        private static float ResolveLinearBlendT(float sharpness, float deltaTime)
        {
            float safeSharpness = math.isfinite(sharpness) ? math.max(0f, sharpness) : 0f;
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            return math.saturate(safeSharpness * safeDeltaTime);
        }

        private void InitializeRenderInterpolationState()
        {
            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 currentPosition = ResolveCurrentRenderInterpolationBodyPosition();
            _previousRenderInterpolationState = new RenderInterpolationState
            {
                BodyPosition = currentPosition,
                CameraYaw = _cameraYaw,
                BodyYaw = _bodyYaw,
                LinearVelocity = currentVelocity,
                VerticalVelocity = currentVelocity.y
            };
            _currentRenderInterpolationState = _previousRenderInterpolationState;
            _renderInterpolatedCameraYaw = _cameraYaw;
            _renderInterpolatedBodyYaw = _bodyYaw;
            _renderInterpolatedLinearVelocity = currentVelocity;
            _renderInterpolationStateInitialized = true;
        }

        private void CaptureFixedInterpolationState()
        {
            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 currentPosition = ResolveCurrentRenderInterpolationBodyPosition();
            _previousRenderInterpolationState = _currentRenderInterpolationState;
            _currentRenderInterpolationState = new RenderInterpolationState
            {
                BodyPosition = currentPosition,
                CameraYaw = _cameraYaw,
                BodyYaw = _bodyYaw,
                LinearVelocity = currentVelocity,
                VerticalVelocity = currentVelocity.y
            };
        }

        private void UpdateRenderInterpolationState()
        {
            if (!_renderInterpolationStateInitialized)
            {
                InitializeRenderInterpolationState();
                return;
            }

            float alpha = ResolveFixedInterpolationAlpha();
            _renderInterpolatedCameraYaw = LerpAngleDegrees(_previousRenderInterpolationState.CameraYaw, _currentRenderInterpolationState.CameraYaw, alpha);
            _renderInterpolatedBodyYaw = LerpAngleDegrees(_previousRenderInterpolationState.BodyYaw, _currentRenderInterpolationState.BodyYaw, alpha);
            _renderInterpolatedLinearVelocity = _previousRenderInterpolationState.LinearVelocity
                + ((_currentRenderInterpolationState.LinearVelocity - _previousRenderInterpolationState.LinearVelocity) * alpha);
            _renderInterpolatedLinearVelocity.y = math.lerp(
                _previousRenderInterpolationState.VerticalVelocity,
                _currentRenderInterpolationState.VerticalVelocity,
                alpha);
        }

        private Vector3 ResolveCurrentRenderInterpolationBodyPosition()
        {
            return HectonPlayerMotor.SafeVelocity(ResolveBodyRuntimePosition());
        }

        private static float ResolveFixedInterpolationAlpha()
        {
            float alpha = HectonFloatingOrigin.CurrentFixedInterpolationAlpha;
            return math.isfinite(alpha) ? math.saturate(alpha) : 0f;
        }

        private void RefreshFixedFrameSpatialCache()
        {
            Vector3 bodyPosition = ResolveBodyRuntimePosition();
            Quaternion bodyRotation = _cachedTransform != null ? _cachedTransform.rotation : Quaternion.identity;
            Vector3 lossyScale = _cachedTransform != null ? _cachedTransform.lossyScale : Vector3.one;
            RefreshFixedFrameSpatialCache(bodyPosition, bodyRotation, lossyScale);
        }

        private void RefreshFixedFrameSpatialCache(Vector3 bodyPosition)
        {
            RefreshFixedFrameSpatialCache(bodyPosition, _fixedFrameBodyRotation, _fixedFrameLossyScale);
        }

        private void RefreshFixedFrameSpatialCache(Vector3 bodyPosition, Quaternion bodyRotation, Vector3 lossyScale)
        {
            _fixedFrameBodyPosition = bodyPosition;
            _fixedFrameBodyRotation = bodyRotation;
            _fixedFrameLossyScale = lossyScale;

            if (_capsuleCollider == null || _cachedTransform == null)
            {
                _fixedFrameCapsuleCenterWS = bodyPosition;
                _fixedFrameCapsuleRadius = math.max(groundCheckRadius, 0.01f);
                _fixedFrameCapsuleHalfHeight = math.max(playerHeight * 0.5f, _fixedFrameCapsuleRadius);
            }
            else
            {
                Vector3 localCenter = _capsuleCollider.center;
                Vector3 scaledLocalCenter = new Vector3(
                    localCenter.x * lossyScale.x,
                    localCenter.y * lossyScale.y,
                    localCenter.z * lossyScale.z);
                float radialScale = math.max(math.abs(lossyScale.x), math.abs(lossyScale.z));
                _fixedFrameCapsuleRadius = math.max(0.01f, _capsuleCollider.radius * radialScale);
                _fixedFrameCapsuleHalfHeight = math.max(_fixedFrameCapsuleRadius, _capsuleCollider.height * 0.5f * math.abs(lossyScale.y));
                _fixedFrameCapsuleCenterWS = bodyPosition + (bodyRotation * scaledLocalCenter);
            }

            _fixedFrameBodyBottomY = _fixedFrameCapsuleCenterWS.y - _fixedFrameCapsuleHalfHeight;
            _fixedFrameBodyTopY = _fixedFrameCapsuleCenterWS.y + _fixedFrameCapsuleHalfHeight;
            _fixedFrameBodyEyeY = math.lerp(_fixedFrameBodyBottomY, _fixedFrameBodyTopY, 0.85f);
        }

        private void RefreshSharedGroundSweepBuffer()
        {
            _groundCheckOrigin.x = _fixedFrameBodyPosition.x;
            _groundCheckOrigin.y = _fixedFrameBodyBottomY + groundCheckRadius + GroundCheckSkin;
            _groundCheckOrigin.z = _fixedFrameBodyPosition.z;
            _fixedGroundSweepHitCount = 0;
            _fixedGroundSweepMaxDistance = 0f;
            if (ResolveMovementGroundLayerMask() == 0 || HectonFloatingOrigin.IsShiftInProgress)
                return;

            float maxGroundDistance = math.max(
                groundCheckDistance,
                math.max(shoreBuoyancyRecoveryClearance, underwaterTurbulenceBottomInfluenceDepth)) + playerHeight;
            if (maxGroundDistance <= 0f)
                return;

            _fixedGroundSweepMaxDistance = maxGroundDistance + GroundCheckSkin;
            AppendRecentLegacyCollisionProbeHits();
            AppendRecentKinematicCollisionProbeHit();
        }

        private void AppendRecentLegacyCollisionProbeHits()
        {
            if (_legacyCollisionProbeHitCount <= 0 ||
                _legacyCollisionProbeShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
            {
                return;
            }

            int age = SystemDispatcher.CurrentFrameIndex - _legacyCollisionProbeFrame;
            if (age < 0 || age > KinematicContactMaxAgeFrames)
                return;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            int count = math.min(_legacyCollisionProbeHitCount, _legacyCollisionProbeHitBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                int slotAge = currentFrame - _legacyCollisionProbeFrameBuffer[i];
                if (slotAge < 0 || slotAge > KinematicContactMaxAgeFrames)
                    continue;

                TryAppendSharedMovementProbeHit(_legacyCollisionProbeHitBuffer[i]);
            }
        }

        private void AppendRecentKinematicCollisionProbeHit()
        {
            HectonPlayerMotor playerMotor = _playerMotor;
            if (playerMotor == null ||
                !playerMotor.TryGetRecentKinematicCollisionContact(
                    KinematicContactMaxAgeFrames,
                    out Vector3 normal,
                    out Vector3 point,
                    out float distance,
                    out _,
                    out _))
            {
                return;
            }

            if (!IsFiniteVector(point) || !IsFiniteVector(normal))
                return;

            Vector3 safeNormal = NormalizeVectorRsqrt(normal, Vector3.up);
            PlayerMovementSurfaceHit hit = new PlayerMovementSurfaceHit
            {
                point = HectonPlayerMotor.SafeVelocity(point, _fixedFrameBodyPosition),
                normal = safeNormal,
                distance = math.max(0f, math.isfinite(distance) ? distance : _groundCheckOrigin.y - point.y),
                collider = null,
                ColliderInstanceId = KinematicContactColliderInstanceId
            };
            TryAppendSharedMovementProbeHit(hit);
        }

        private bool TryAppendSharedMovementProbeHit(PlayerMovementSurfaceHit hit)
        {
            if (_fixedGroundSweepHitCount >= _groundProbeHitBuffer.Length)
                return false;

            int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
            if (hitColliderInstanceId == 0 || hitColliderInstanceId == _playerColliderInstanceId)
                return false;

            if (!IsFiniteReusableMovementProbeHit(in hit, _fixedGroundSweepMaxDistance))
                return false;

            _groundProbeHitBuffer[_fixedGroundSweepHitCount] = hit;
            _fixedGroundSweepHitCount++;
            return true;
        }

        private bool TryBuildMovementSweepStepDirection(out Vector3 stepDirection)
        {
            stepDirection = Vector3.zero;
            if (stepAssistHeight <= 0f || stepAssistForwardDistance <= 0f)
                return false;

            float inputX = _inputH;
            float inputZ = _inputV;
            float planarSqr = inputX * inputX + inputZ * inputZ;
            if (planarSqr <= 0.0001f)
                return false;

            float resolvedBodyYaw = _activeTransportPlatformTransform != null
                ? ResolveYawRelativeToTransportPlatform(_bodyYaw)
                : _bodyYaw;
            ResolveDegreesSinCosFast(resolvedBodyYaw, out float sinYaw, out float cosYaw);
            stepDirection.x = sinYaw * inputZ + cosYaw * inputX;
            stepDirection.y = 0f;
            stepDirection.z = cosYaw * inputZ - sinYaw * inputX;

            if (_activeTransportPlatformTransform != null)
            {
                Vector3 rawInputWorld = TransformTransportPlatformDirectionToWorld(stepDirection);
                stepDirection = ResolveTransportPlatformRelativeWorldDirection(rawInputWorld);
            }

            float directionSqr = stepDirection.x * stepDirection.x + stepDirection.z * stepDirection.z;
            if (directionSqr <= 0.0001f)
                return false;

            float invMagnitude = math.rsqrt(math.max(directionSqr, 0.000001f));
            stepDirection.x *= invMagnitude;
            stepDirection.z *= invMagnitude;
            return true;
        }

        private bool TryBuildMovementSweepSupportOrigins(
            bool useExosuitSupport,
            bool useDryInteriorSupport,
            out Vector3 leftOrigin,
            out Vector3 rightOrigin,
            out float probeDistance)
        {
            leftOrigin = Vector3.zero;
            rightOrigin = Vector3.zero;
            probeDistance = 0f;
            if (!useExosuitSupport && !useDryInteriorSupport)
                return false;

            float lateralOffset;
            float forwardOffsetDistance;
            float probeOriginY;
            if (useExosuitSupport)
            {
                lateralOffset = math.max(0.01f, exosuitFootProbeLateralOffset);
                forwardOffsetDistance = exosuitFootProbeForwardOffset;
                probeOriginY = _fixedFrameBodyBottomY + math.max(0.01f, exosuitFootProbeHeight);
                probeDistance = math.max(0.05f, exosuitFootProbeDistance);
            }
            else
            {
                lateralOffset = math.max(0.01f, dryInteriorFootProbeLateralOffset);
                forwardOffsetDistance = dryInteriorFootProbeForwardOffset;
                probeOriginY = _fixedFrameBodyBottomY + math.max(0.01f, dryInteriorFootProbeHeight);
                probeDistance = math.max(0.05f, dryInteriorFootProbeDistance);
            }

            ResolveDegreesSinCosFast(_bodyYaw, out float sinYaw, out float cosYaw);
            Vector3 bodyForward = new Vector3(sinYaw, 0f, cosYaw);
            Vector3 bodyRight = new Vector3(bodyForward.z, 0f, -bodyForward.x);
            Vector3 center = new Vector3(_fixedFrameBodyPosition.x, probeOriginY, _fixedFrameBodyPosition.z);
            Vector3 forwardOffset = bodyForward * forwardOffsetDistance;
            leftOrigin = center + forwardOffset - bodyRight * lateralOffset;
            rightOrigin = center + forwardOffset + bodyRight * lateralOffset;
            return true;
        }

        private bool TryResolveNearestMovementProbeHit(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            out PlayerMovementSurfaceHit hit)
        {
            hit = default;
            int groundLayerMask = ResolveMovementGroundLayerMask();
            if (distance <= 0f || groundLayerMask == 0 || HectonFloatingOrigin.IsShiftInProgress)
                return false;

            Vector3 safeDirection = NormalizeVectorRsqrt(direction, Vector3.down);
            float safeRadius = math.max(0.01f, radius);
            if (TryUseCachedMovementProbeResult(origin, safeRadius, safeDirection, distance, out bool cachedHitResolved, out hit))
                return cachedHitResolved;

            if (TryUseSharedGroundSweepAsMovementProbe(origin, safeRadius, safeDirection, distance, out hit))
            {
                CacheMovementProbeResult(origin, safeRadius, safeDirection, distance, true, hit);
                return true;
            }

            CacheMovementProbeResult(origin, safeRadius, safeDirection, distance, false, default);
            return false;
        }

        private int ResolveMovementGroundLayerMask()
        {
            int layerMask = groundLayers.value;
            return layerMask == 0 ? 0 : HectonLayerMasks.ResolveTerrainSdfProbeLayerMask(layerMask);
        }

        private bool TryUseSharedGroundSweepAsMovementProbe(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            out PlayerMovementSurfaceHit hit)
        {
            hit = default;
            if (_fixedGroundSweepHitCount <= 0 ||
                _fixedGroundSweepMaxDistance <= 0f ||
                distance > _fixedGroundSweepMaxDistance)
            {
                return false;
            }

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            float resolvedProbeDistance = 0f;
            bool downwardProbe = math.dot(new float3(direction.x, direction.y, direction.z), new float3(0f, -1f, 0f)) >= BatchedGroundProbeDownDot;
            float horizontalSlack = math.max(0.01f, radius) + math.max(0.01f, groundCheckRadius) + BatchedGroundProbeHorizontalSlack;
            float horizontalSlackSq = horizontalSlack * horizontalSlack;
            int hitCount = math.min(_fixedGroundSweepHitCount, _groundProbeHitBuffer.Length);
            for (int i = 0; i < hitCount; i++)
            {
                PlayerMovementSurfaceHit candidate = _groundProbeHitBuffer[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in candidate);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _playerColliderInstanceId)
                    continue;

                if (!IsFiniteReusableMovementProbeHit(in candidate, distance))
                    continue;

                float probeDistance;
                if (downwardProbe)
                {
                    if (candidate.normal.y < ReusableGroundProbeMinNormalY)
                        continue;

                    Vector3 planarDelta = candidate.point - origin;
                    float planarDistanceSq = planarDelta.x * planarDelta.x + planarDelta.z * planarDelta.z;
                    if (planarDistanceSq > horizontalSlackSq)
                        continue;

                    probeDistance = candidate.distance;
                }
                else
                {
                    Vector3 delta = candidate.point - origin;
                    float along = delta.x * direction.x + delta.y * direction.y + delta.z * direction.z;
                    if (along < -GroundCheckSkin || along > distance + GroundCheckSkin + radius)
                        continue;

                    Vector3 lateral = delta - direction * along;
                    if (lateral.sqrMagnitude > horizontalSlackSq)
                        continue;

                    float normalFacing = candidate.normal.x * direction.x +
                                         candidate.normal.y * direction.y +
                                         candidate.normal.z * direction.z;
                    if (normalFacing > 0.25f)
                        continue;

                    probeDistance = math.max(0f, along);
                }

                if (probeDistance < nearestDistance)
                {
                    nearestDistance = probeDistance;
                    resolvedProbeDistance = probeDistance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
                return false;

            hit = _groundProbeHitBuffer[nearestIndex];
            hit.distance = resolvedProbeDistance;
            return true;
        }

        private static bool IsReusableFootstepSurfaceHit(in PlayerMovementSurfaceHit hit, float maxDistance, LayerMask layerMask)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return false;

            int layerMaskValue = layerMask.value;
            int layerBit = 1 << hitCollider.gameObject.layer;
            if ((layerMaskValue & layerBit) == 0)
                return false;

            if (!math.isfinite(hit.distance) ||
                hit.distance < 0f ||
                hit.distance > maxDistance + GroundCheckSkin)
            {
                return false;
            }

            Vector3 normal = hit.normal;
            if (!math.isfinite(normal.x) || !math.isfinite(normal.y) || !math.isfinite(normal.z))
                return false;

            if (normal.y < ReusableGroundProbeMinNormalY)
                return false;

            Vector3 point = hit.point;
            return math.isfinite(point.x) && math.isfinite(point.y) && math.isfinite(point.z);
        }

        private static bool IsFiniteReusableGroundProbeHit(in PlayerMovementSurfaceHit hit, float distance)
        {
            if (!IsFiniteReusableMovementProbeHit(in hit, distance))
                return false;

            Vector3 normal = hit.normal;
            return normal.y >= ReusableGroundProbeMinNormalY;
        }

        private static bool IsFiniteReusableMovementProbeHit(in PlayerMovementSurfaceHit hit, float distance)
        {
            if (!math.isfinite(hit.distance) ||
                hit.distance < 0f ||
                hit.distance > distance + GroundCheckSkin)
            {
                return false;
            }

            Vector3 normal = hit.normal;
            if (!math.isfinite(normal.x) || !math.isfinite(normal.y) || !math.isfinite(normal.z))
                return false;

            Vector3 point = hit.point;
            return math.isfinite(point.x) && math.isfinite(point.y) && math.isfinite(point.z);
        }

        private void AdvanceMovementProbeCacheFrame()
        {
            unchecked
            {
                _movementProbeCacheFixedSequence++;
            }

            _movementProbeCacheSequence = -1;
            _movementProbeCacheHasHit = false;
            _movementProbeCacheHit = default;
        }

        private void InvalidateMovementProbeCaches()
        {
            _fixedGroundSweepHitCount = 0;
            _fixedGroundSweepMaxDistance = 0f;
            ResetLegacyCollisionProbeHits();
            _movementProbeCacheSequence = -1;
            _movementProbeCacheLayerMask = 0;
            _movementProbeCacheHasHit = false;
            _movementProbeCacheHit = default;
            _movementProbeCacheShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        }

        private bool TryUseCachedMovementProbeResult(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            out bool hitResolved,
            out PlayerMovementSurfaceHit hit)
        {
            hitResolved = false;
            hit = default;
            if (_movementProbeCacheSequence != _movementProbeCacheFixedSequence ||
                _movementProbeCacheLayerMask != ResolveMovementGroundLayerMask() ||
                _movementProbeCacheShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
            {
                return false;
            }

            if ((origin - _movementProbeCacheOrigin).sqrMagnitude > MovementProbeCachePositionEpsilonSq ||
                (direction - _movementProbeCacheDirection).sqrMagnitude > MovementProbeCachePositionEpsilonSq ||
                math.abs(radius - _movementProbeCacheRadius) > MovementProbeCacheScalarEpsilon ||
                math.abs(distance - _movementProbeCacheDistance) > MovementProbeCacheScalarEpsilon)
            {
                return false;
            }

            hitResolved = _movementProbeCacheHasHit;
            hit = _movementProbeCacheHit;
            return true;
        }

        private void CacheMovementProbeResult(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            bool hitResolved,
            PlayerMovementSurfaceHit hit)
        {
            _movementProbeCacheSequence = _movementProbeCacheFixedSequence;
            _movementProbeCacheLayerMask = ResolveMovementGroundLayerMask();
            _movementProbeCacheShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _movementProbeCacheOrigin = origin;
            _movementProbeCacheDirection = direction;
            _movementProbeCacheRadius = radius;
            _movementProbeCacheDistance = distance;
            _movementProbeCacheHasHit = hitResolved;
            _movementProbeCacheHit = hit;
        }

        private float ResolveMovementSupportProbeRadius()
        {
            return math.max(0.03f, groundCheckRadius * 0.15f);
        }

        private void SyncFixedFrameMotorPosition(Vector3 position)
        {
            RefreshFixedFrameSpatialCache(position);
            RefreshSharedGroundSweepBuffer();
        }

        private void CacheTransportPlatformSpatialFrame(Transform platformTransform)
        {
            if (platformTransform == null)
            {
                _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
                _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
                _cachedTransportPlatformBasisRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                _transportPlatformAupFrameValid = false;
                return;
            }

            _cachedTransportPlatformLocalToWorldMatrix = platformTransform.localToWorldMatrix;
            _cachedTransportPlatformWorldToLocalMatrix = _cachedTransportPlatformLocalToWorldMatrix.inverse;

            Vector3 up = _cachedTransportPlatformLocalToWorldMatrix.MultiplyVector(Vector3.up);
            Vector3 forward = ProjectOnPlaneFast(_cachedTransportPlatformLocalToWorldMatrix.MultiplyVector(Vector3.forward), up);
            if (forward.sqrMagnitude <= 0.0001f)
                forward = ProjectOnPlaneFast(up, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
                forward = _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

            forward = NormalizeVectorRsqrt(forward, _cachedTransform != null ? _cachedTransform.forward : Vector3.forward);
            _cachedTransportPlatformBasisRotation = Quaternion.LookRotation(forward, up);
            _cachedTransportPlatformSpatialFrameValid = true;
        }

        private void QueueEnvironmentalHullStress(float normalizedStress)
        {
            if (_environmentHandler != null)
            {
                _environmentHandler.QueueHullStress(normalizedStress);
                return;
            }

            RequestExternalHullStress(normalizedStress);
        }

        internal PlayerTransportPreset ResolveActiveTransportPresetForSubsystems()
        {
            return ResolveActiveTransportPreset();
        }

        internal void AccumulateBufferedEnvironmentalHullStress(float normalizedStress)
        {
            float clampedStress = math.saturate(normalizedStress);
            if (clampedStress <= 0.0001f)
                return;

            if (!_externalHullStressRequestedThisStep || clampedStress > _externalHullStressRequestedIntensity)
                _externalHullStressRequestedIntensity = clampedStress;

            _externalHullStressRequestedThisStep = true;
        }

        internal void ExecuteEnvironmentForcePhase(float fixedDeltaTime, PlayerTransportPreset activeTransportPreset)
        {
            bool exosuitActive = IsExosuitTransportActive();
            bool applySubmergedEnvironment = !IsInDryInterior() && (_waterImmersionRatio > 0.02f || exosuitActive);
            if (applySubmergedEnvironment)
            {
                if (_isWalking)
                {
                    AdvanceCurrentPhaseTimer(fixedDeltaTime);
                    AdvanceAbyssalThermalInfluence(fixedDeltaTime, activeTransportPreset);
                    AdvanceExternalEnvironmentalDrag(fixedDeltaTime);
                    AdvanceParasiteLatchInfluence(fixedDeltaTime);
                }

                ApplyUnderwaterTurbulence(fixedDeltaTime, activeTransportPreset);
                ApplyAbyssalCurrents(fixedDeltaTime, activeTransportPreset);
                ApplyThermalUpdrafts(fixedDeltaTime, activeTransportPreset);
                ApplyParasiteLatchForces(fixedDeltaTime);
            }
            else
            {
                _abyssalThermalFlowSample = default;
                _abyssalThermalFlowSample.DragMultiplier = 1f;
                _abyssalThermalFlowVelocityWS = Vector3.zero;
                _abyssalFlowAdvectionVelocityWS = Vector3.zero;
                ResetAbyssalCurrentShearRuntime();
                _thermalUpdraftIntensity = 0f;
                _thermalUpdraftVelocityChange = Vector3.zero;
                _externalThermalUpdraftVelocityChange = Vector3.zero;
                _externalThermalUpdraftRequestedThisStep = false;
            }

            if (_waterImmersionRatio > 0.02f)
                ApplyShoreUndertow(_smoothedImmersionRatio, activeTransportPreset);
            else
            {
                _undertowVector = Vector3.zero;
                _undertowIntensity = 0f;
            }
        }

        internal void ExecuteEnvironmentStressPhase(float fixedDeltaTime, PlayerTransportPreset activeTransportPreset)
        {
            UpdateHullStress(fixedDeltaTime, activeTransportPreset);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  LIFECYCLE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public void OnRegister()
        {
            if (_registeredPlayerMovementContracts &&
                ReferenceEquals(GlobalRegistry.PlayerMovementContracts, this))
            {
                TryRegisterToDispatchers();
                return;
            }

            IPlayerMovementContracts currentContracts = GlobalRegistry.PlayerMovementContracts;
            if (currentContracts == null || ReferenceEquals(currentContracts, this))
            {
                GlobalRegistry.RegisterPlayerMovementContracts(this);
                _registeredPlayerMovementContracts = ReferenceEquals(GlobalRegistry.PlayerMovementContracts, this);
            }

            TryRegisterToDispatchers();
        }

        public void OnDependencyInject()
        {
            _dataVault = GlobalRegistry.DataVault;
            CacheAudioService(GlobalRegistry.Audio);
            _settingsRuntime = GlobalRegistry.Settings;
            _localizationRuntime = GlobalRegistry.LocalizationStressPresentation;
            _localizationStressHudRefreshSink = GlobalRegistry.LocalizationStressHudRefreshSink;
            _spectrumRuntime = GlobalRegistry.Spectrum;
            _resourceDistributionRuntime = GlobalRegistry.ResourceDistribution;
            _gasDynamicsRuntime = GlobalRegistry.GasDynamics;
            _fluidSurfaceRuntime = GlobalRegistry.FluidSurfaceCurrent;
            _terrainProviderRuntime = GlobalRegistry.Terrain;
            _abyssalFlowGpuRuntime = GlobalRegistry.AbyssalFlowGpu;
            _ambientCurrentReadModel = GlobalRegistry.AmbientCurrent;
            _physicsService = GlobalRegistry.Physics;
            _physicsStateEvents = GlobalRegistry.PhysicsStateEvents;
            _inputServiceRuntime = GlobalRegistry.Input;
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _voxelEngineRuntime = GlobalRegistry.VoxelEngine;
            WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);
            _thermodynamicsRuntime = GlobalRegistry.Thermodynamics;
            _suitUpgradeRuntime = GlobalRegistry.SuitUpgrades;
            _oceanKinematicsRuntime = GlobalRegistry.OceanKinematics;
            _playerSensoryRuntime = GlobalRegistry.PlayerSensory;
            RefreshCinematicFocusTierGateCold();
            RefreshRuntimeDependencyBindings();
        }

        private void RefreshRuntimeDependencyBindings()
        {
            BindInventoryLoadSource();
            ResolvePlayerToolManagerCold();
            ResolvePlayerTransportCoordinatorCold();
            ResolveSwimPresentationControllerCold();
            ResolveUnderwaterVisualsCold();
            ResolveInputManagerBinding();
            EnsurePlayerRuntimeSubsystems();

            if (_environmentHandler != null)
                _environmentHandler.Bind(this, _playerMotor);

            if (_cameraRig != null)
                _cameraRig.Bind(playerCamera, _cameraComponent);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _playerKinematicsNativeState.Dispose();
                    DisposeCinematicFocusBlackBox();
                    _dataVault = currentService as IDataVault;
                    EnsurePlayerKinematicsNativeState();
                    EnsureCinematicFocusBlackBox();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherTicks();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterToDispatchers();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.SettingsRuntime:
                    _settingsRuntime = currentService as SettingsManager;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationRuntime = currentService as ILocalizationStressPresentationReadModel;
                    _localizationStressHudRefreshSink = currentService as ILocalizationStressHudRefreshSink;
                    break;
                case GlobalRegistryServiceSlot.SpectrumRuntime:
                    _spectrumRuntime = currentService as SpectrumSystem;
                    break;
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime:
                    _resourceDistributionRuntime = currentService as ResourceDistributionDirector;
                    break;
                case GlobalRegistryServiceSlot.GasDynamicsRuntime:
                    _gasDynamicsRuntime = currentService as IGasDynamicsSolver;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _fluidSurfaceRuntime = currentService as IFluidSurfaceCurrentReadModel;
                    _abyssalFlowGpuRuntime = currentService as IAbyssalFlowGpuReadModel;
                    _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _terrainProviderRuntime = currentService as ITerrainProvider;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    _physicsStateEvents = currentService as IPhysicsStateEventService;
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _inputServiceRuntime = currentService as IInputService;
                    _resolvedInputManager = false;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    _resolvedPlayerToolManager = false;
                    _playerToolManager = null;
                    UnbindInventoryLoadSource();
                    BindInventoryLoadSource();
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelEngineRuntime = currentService as HectonVoxelEngine;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _sargassumDragRuntime = currentService as SargassumGlobalDragManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime:
                    _thermodynamicsRuntime = currentService as AbyssalThermalManager;
                    break;
                case GlobalRegistryServiceSlot.SuitUpgradeRuntime:
                    _suitUpgradeRuntime = currentService as SuitUpgradeManager;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsRuntime = currentService as IHectonOceanKinematicsService;
                    _oceanKinematics = null;
                    break;
                case GlobalRegistryServiceSlot.PlayerSensory:
                    _playerSensoryRuntime = currentService as IPlayerSensoryService;
                    _underwaterVisuals = null;
                    _resolvedUnderwaterVisuals = false;
                    break;
            }

            RefreshRuntimeDependencyBindings();
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterDependencyRebinds()
        {
            if (!_registeredHotSwapListener)
                _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterDependencyRebinds()
        {
            if (_registeredHotSwapListener)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwapListener = false;
            }
        }

        private void UnregisterPlayerMovementContracts()
        {
            if (!_registeredPlayerMovementContracts)
                return;

            GlobalRegistry.UnregisterPlayerMovementContracts(this);
            _registeredPlayerMovementContracts = false;
        }

        private void ClearInjectedDependencies()
        {
            _audioService = null;
            _settingsRuntime = null;
            _localizationRuntime = null;
            _localizationStressHudRefreshSink = null;
            _spectrumRuntime = null;
            _resourceDistributionRuntime = null;
            _gasDynamicsRuntime = null;
            _fluidSurfaceRuntime = null;
            _terrainProviderRuntime = null;
            _abyssalFlowGpuRuntime = null;
            _inputServiceRuntime = null;
            _playerInventoryService = null;
            _voxelEngineRuntime = null;
            _sargassumDragRuntime = null;
            _thermodynamicsRuntime = null;
            _suitUpgradeRuntime = null;
            _oceanKinematicsRuntime = null;
            _playerSensoryRuntime = null;
        }

        Vector2 IPlayerMovementPoseReadModel.CurrentLocalWaveSlope => _dynamicWaveLocalSlope;

        bool IPlayerMovementPoseReadModel.TryGetRuntimePosition(out Vector3 position)
        {
            position = ResolveBodyRuntimePosition();
            return true;
        }

        void IPlayerMovementForceSink.QueueExternalAcceleration(Vector3 acceleration)
        {
            QueueSubsystemExternalAcceleration(acceleration);
        }

        void IPlayerMovementForceSink.QueueExternalVelocityChange(Vector3 velocityChange)
        {
            QueueSubsystemExternalVelocityChange(velocityChange);
        }

        void IPlayerMovementEnvironmentSink.RequestExternalHullStress(float normalizedStress)
        {
            RequestExternalHullStress(normalizedStress);
        }

        void IPlayerMovementEnvironmentSink.RequestLocalGravityOverride(Vector3 gravityVector, float holdSeconds)
        {
            RequestLocalGravityOverride(gravityVector, holdSeconds);
        }

        void IPlayerHypoxiaPresentationSink.RequestHypoxiaVisorDistortion(float intensity, float holdDuration, float recoverySpeed)
        {
            float oxygenLevel01 = _survivalSystem != null ? _survivalSystem.OxygenNormalized : 1f;
            float safeThreshold = 0.8f;
            System.Numerics.Vector2 hypoxiaVisorBlur = Hecton8.PureLogic.Systems.HypoxiaVisorBlurIntensityCalculator.Compute(oxygenLevel01, holdDuration, recoverySpeed, safeThreshold, 0.2f, 10f, 0.5f, 5f, 10f);
            TriggerHypoxiaVisorDistortion(Mathf.Max(intensity, hypoxiaVisorBlur.X), holdDuration, recoverySpeed);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnDestroy()
        {
            if (this != null && gameObject != null)
            {
                Hecton8.Core.H8Debug.Log($"[HectonPlayerMovement-DEBUG] OnDestroy called on player! InstanceID={gameObject.GetEntityId()}\n" + UnityEngine.StackTraceUtility.ExtractStackTrace());
            }
        }
#endif

        private void Awake()
        {
            EnsureDegreeSinCosLutInitialized();
            _cachedTransform = transform;

            TryGetComponent(out _rb);
            if (_rb == null)
            {
                enabled = false;
                return;
            }

            CacheAuthoritativeBodyMassKg(_rb.mass);
            TryGetComponent(out _capsuleCollider);
            _playerColliderInstanceId = _capsuleCollider != null ? unchecked((int)EntityId.ToULong(_capsuleCollider.GetEntityId())) : 0;
            _instanceId = ResolveStableEntitySeed32(gameObject);
            TryGetComponent(out _buoyancy);
            _buoyancyAirState = _buoyancy as IBuoyancyAirStateReadModel;
            TryGetComponent(out _swimPresentationController);
            if (!TryGetComponent(out _physicalInteractionHandler))
            {
                _physicalInteractionHandler = gameObject.AddComponent<PhysicalInteractionHandler>(); // COLD ALLOC: PhysicalInteractionHandler[1] - player-owned heavy-carry movement modifier bridge - owner: HectonPlayerMovement
            }
            if (!TryGetComponent(out _heavyTowWinch))
            {
                _heavyTowWinch = gameObject.AddComponent<HeavyTowWinch>(); // COLD ALLOC: HeavyTowWinch[1] â€” player-owned salvage tow runtime for harpoon/winch towing â€” owner: HectonPlayerMovement
            }
            TryGetComponent(out _survivalSystem);
            if (!TryGetComponent(out _sargassumMovementInfluence))
            {
                _sargassumMovementInfluence = gameObject.AddComponent<SargassumMovementInfluence>(); // COLD ALLOC: SargassumMovementInfluence[1] Ã¢â‚¬â€ player-owned sticky drag receiver for sargassum obstacles Ã¢â‚¬â€ owner: HectonPlayerMovement
            }

            EnsurePlayerRuntimeSubsystems();

            _resolvedSwimPresentationController = true;
            _resolvedPlayerToolManager = false;
            _resolvedPlayerTransportCoordinator = false;
            _resolvedPhysicalInteractionHandler = true;
            _resolvedHeavyTowWinch = true;
            CacheBaseCollisionProfile();
            ResolvePlayerToolManagerCold();
            ResolvePlayerTransportCoordinatorCold();

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.freezeRotation = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.useGravity = false;
            _baseCenterOfMass = _rb.centerOfMass;
            _lastAppliedCenterOfMass = _baseCenterOfMass;
            _playerState.SyncKinematic(_rb.position, ResolveAuthoritativeLinearVelocity(Vector3.zero));
            _playerState.SyncEncumbrance(_runtimeInventoryLoad01, _runtimeInventoryLoadMovementMultiplier);
            EnsurePlayerKinematicsNativeState();
            EnsureCinematicFocusBlackBox();
            RefreshCinematicFocusTierGateCold();
            RecordLastValidAup(_playerState.AbsolutePosition);

            // Cache camera component for FOV manipulation
            if (playerCamera != null)
                playerCamera.TryGetComponent(out _cameraComponent);
            if (!TryGetComponent(out _cameraRig))
            {
                _cameraRig = gameObject.AddComponent<HectonPlayerCameraRig>(); // COLD ALLOC: HectonPlayerCameraRig[1] â€” dedicated player camera owner consuming locomotion camera state â€” owner: HectonPlayerMovement
            }
            _cameraRig.Bind(playerCamera, _cameraComponent);
            EnsurePlayerRuntimeSubsystems();

            float initialYaw = ExtractWorldYaw(_cachedTransform.forward, 0f);
            _cameraYaw = initialYaw;
            _bodyYaw = initialYaw;
            _bodyYawVelocity = 0f;

            if (playerCamera != null)
            {
                _cameraPitch = -ExtractLocalPitchDegrees(playerCamera.localRotation);
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);
                _cameraBaseLocalPos = playerCamera.localPosition;
            }

            if (_cameraComponent != null)
                baseFov = _cameraComponent.fieldOfView;

            _cachedGravity = ResolveAupCenterGravity(UnityEngine.Physics.gravity);
            _cachedGravityMagnitude = MagnitudeFromRsqrt(_cachedGravity);
            _smoothedGroundNormal = Vector3.up;
            RefreshGroundSlopeCache();
            if (_rb.linearDamping != 0f)
                _rb.linearDamping = 0f;
            InitializeRenderInterpolationState();
            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);

            // Ã¢â€â‚¬Ã¢â€â‚¬ Crest integration Ã¢â€â‚¬Ã¢â€â‚¬
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterFlowVelocity = Vector3.zero;
            _crestSamplingSucceeded = false;
            _crestFlowSamplingSucceeded = false;
            InitOceanKinematics();

            _waterImmersionRatio = ComputeImmersionRatio();
            _smoothedImmersionRatio = _waterImmersionRatio;
            _currentDepth = ComputeDepth();
            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }
            _isWalking = _waterImmersionRatio < swimTransitionThreshold;
            _isSurfaceSwimming = !_isWalking && !IsInDryInterior() && _currentDepth <= surfaceSwimDepthBand;
            _currentLocomotionMode = ResolveLocomotionMode(_smoothedImmersionRatio);
            _stateMachine?.SyncLocomotionMode(_currentLocomotionMode);
            _prevSpeed = 0f;
            _prevYawForMomentum = _cameraYaw;
            _currentTimer = 0f;
            _crestFlowInputAttenuation = 1f;
            _underwaterSomaticPhase = 0f;
            _underwaterSomaticWeight = 0f;
            _underwaterSomaticPitchOffset = 0f;
            _underwaterSomaticYawOffset = 0f;
            _underwaterSomaticFatigue01 = 0f;
            _underwaterSomaticFatigueBreathCooldownTimer = 0f;
            _surfaceDiveCommitTimer = 0f;
            _surfaceBreachFluidDragBypassTimer = 0f;
            _waterTransitionHandler?.ResetRuntimeState();
            _surfaceLockBlend = _isSurfaceSwimming ? 1f : 0f;
            _surfaceLockTargetY = _isSurfaceSwimming
                ? EffectiveWaterSurfaceY + surfaceStickOffset
                : ResolveBodyRuntimePosition().y;
            _shoreBuoyancyBlend = _isWalking ? 0f : 1f;
            _bottomClearance = float.PositiveInfinity;
            _bottomNormal = Vector3.up;
            _surfaceWavePoseRotation = Quaternion.identity;
            _underwaterTurbulencePoseRotation = Quaternion.identity;
            _transportCavitationEfficiency = 1f;
            _previousTransportForwardVelocity = 0f;
            _wetLensSignalIntensity = 0f;
            _wetLensPulseCooldownTimer = 0f;
            _underwaterStressSignalIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
            _sargassumFieldDensity01 = 0f;
            _sargassumMatBuoyancyBlend = 0f;
            _sargassumHighStrainIntensity = 0f;
            _sargassumHighStrainTimer = 0f;
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;
            _abyssalFlowAdvectionVelocityWS = Vector3.zero;
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _stateMachine?.ResetRuntimeState();
            _impulseBypassTimer = 0f;
            _transportBailoutCooldownTimer = 0f;
            _transportEvaLockTicks = 0;
            _recentBreachExitTimer = 0f;
            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspCooldownTimer = 0f;
            _sargassumRestRecoveryBlend = 0f;
            _surfaceGaspSubmergedLatch = false;
            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragCurrentMultiplier = 1f;
            _externalEnvironmentalDragHoldTimer = 0f;
            _externalEnvironmentalDragRequestedThisStep = false;
            _cuttingTensionAnchorRequestedWS = Vector3.zero;
            _cuttingTensionAnchorCurrentWS = Vector3.zero;
            _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
            _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
            _cuttingTensionRequestedThisStep = false;
            _parasiteLatchedRequestedCount = 0;
            _parasiteLatchedCurrentCount = 0;
            _parasiteCenterOfMassRequestedLS = Vector3.zero;
            _parasiteCenterOfMassCurrentLS = Vector3.zero;
            _parasiteHarvesterPullRequestedWS = Vector3.zero;
            _parasiteHarvesterPullCurrentWS = Vector3.zero;
            _parasiteLatchRequestedThisStep = false;
            _parasiteLatchHoldTimer = 0f;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
            _physicalTraumaCollisionWeight = 0f;
            _physicalTraumaCollisionHoldTimer = 0f;
            _exosuitJumpJetWakePulseTimer = 0f;
            _exosuitGrappleAnchorRequestedWS = Vector3.zero;
            _exosuitGrappleAnchorCurrentWS = Vector3.zero;
            _exosuitGrappleHoldTimer = 0f;
            _exosuitGrappleCurrentForce = 0f;
            _exosuitGrappleRequestedThisStep = false;
            _isAirborne = false;
            ResetHeavyTowRuntimeResponse();
            _abyssalDowndraftCooldownTimer = 0f;
            _abyssalDowndraftActiveTimer = 0f;
            _abyssalDowndraftIntensity = 0f;
            _abyssalDowndraftVelocityChange = Vector3.zero;
            ResetAbyssalCurrentShearRuntime();
            _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            _previousAbyssalNoisyFlow = Vector3.zero;
            _abyssalTransportTurbulencePitchOffset = 0f;
            _abyssalTransportTurbulenceYawOffset = 0f;
            _hullStressIntensity = 0f;
            _hullStressGroanCooldownTimer = 0f;
            _hullStressHudCorruptionRefreshTimer = 0f;
            _externalHullStressRequestedIntensity = 0f;
            _externalHullStressRequestedThisStep = false;
            _fatalPressureSequenceTimer = 0f;
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0f;
            _fatalPressureRearmTimer = 0f;
            _activeSonarPingCooldownTimer = 0f;
            _thermalUpdraftIntensity = 0f;
            _thermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
            _queuedExternalKinematicAcceleration = Vector3.zero;
            _queuedExternalKinematicVelocityChange = Vector3.zero;
            _wallKickCooldownTimer = 0f;
            _ladderSplineSnapActive = false;
            _ladderSplineSnapAxisWorld = Vector3.zero;
            _aupSpeculativeHoverTicks = 0;
            _aupSpeculativeHoverHeightMeters = 0f;
            _lastProcessedKccSlideFeedbackFrame = -1;
            _thermalUpdraftTraumaCooldownTimer = 0f;
            _vegetationDensityLinearDamping = 0f;
            _debugSargassumEntanglementDragRequest = 1f;

            ApplySuitToRigidbody();

            _registeredTick = false;
            _registeredFixedTick = false;
            _registeredOriginShiftListener = false;
            _registeredPlayerMovementContracts = false;
            _registeredHotSwapListener = false;
            _useFixedFrameSpatialCache = false;
            InvalidateMovementProbeCaches();
            RefreshFixedFrameSpatialCache();

            ResolveInputManagerBinding();
            UpdateSuitDiagnostics();
        }

        private void EnsureJuiceProcessor()
        {
            if (_juiceProcessor != null)
                return;

            _juiceProcessor = new CameraJuiceProcessor();
        }

        private void PrepareRenderTickDependencies()
        {
            ResolveInputManagerBinding();
            ResolveSwimPresentationController();
            _debugHasSwimPresentationController = _swimPresentationController != null;
            PlayerTransportPreset activeTransportPreset = ResolveActiveTransportPreset();
            UpdateRequestedTransportCollisionProfile(activeTransportPreset);
        }

        private PlayerTransportPreset PrepareFixedTickDependencies()
        {
            return ResolveActiveTransportPreset();
        }

        private void OnEnable()
        {
            SargassumGlobalDragManager.Register(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            OnRegister();
            OnDependencyInject();
            TryRegisterDependencyRebinds();
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);
            BindInventoryLoadSource();
            ResolvePlayerToolManagerCold();
            ResolvePlayerTransportCoordinatorCold();
            ResolveSwimPresentationControllerCold();
            ResolveUnderwaterVisualsCold();
            ResolveInputManagerBinding();
            EnsurePlayerRuntimeSubsystems();
            EnsurePlayerKinematicsNativeState();
            EnsureCinematicFocusBlackBox();
            RefreshCinematicFocusTierGateCold();
            _playerKinematicsTelemetryDumpedThisFault = false;
            if (_rb != null)
            {
                _playerState.SyncKinematic(ResolveBodyRuntimePosition(), ResolveAuthoritativeLinearVelocity(Vector3.zero));
                RecordLastValidAup(_playerState.AbsolutePosition);
            }
            if (_cameraRig != null)
                _cameraRig.Bind(playerCamera, _cameraComponent);
            ToggleBuoyancy(true);
            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            }
            TryRegisterToDispatchers();
        }

        private void Start()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            OnDependencyInject();
            if (_registeredTick && _registeredFixedTick && _registeredLateFrameTick) return;
            TryRegisterToDispatchers();

            BindInventoryLoadSource();
            ResolveInputManagerBinding();
            if (useCrestOceanHeight && !_crestAvailable)
                InitOceanKinematics();
        }

        private void OnDisable()
        {
            SargassumGlobalDragManager.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterDependencyRebinds();
            UnregisterPlayerMovementContracts();
            UnbindInventoryLoadSource();
            UnsubscribeFromInput();
            _cachedMoveInput = Vector2.zero;
            _pendingLookInput = Vector2.zero;
            _cachedVerticalInput = 0f;
            ResetFootstepAudioMaterialCache();

            ResetVrComfortRuntimeState();
            ClearPendingPresentationSync();
            ResetUnderwaterSomaticRuntimeState();
            ResetEnvironmentalDragAndTensionRuntimeState();

            _stateMachine?.ResetRuntimeState();
            _waterTransitionHandler?.ResetRuntimeState();
            _playerState.ResetTransient();
            _playerMotor?.ResetRuntimeState();

            ApplyResolvedCollisionProfile(1f, 1f, 0f);
            ApplyCenterOfMassIfChanged(_baseCenterOfMass);

            ResetTransportPlatformRuntimeState();
            InvalidateMovementProbeCaches();
            ResetLegacyCollisionProbeHits();
            ResetRenderInterpolationRuntimeState();

            ToggleBuoyancy(true);

            UnregisterGlobalLifecycleListeners();

            _playerKinematicsNativeState.Dispose();
            ClearCinematicFocus(true);
            DisposeCinematicFocusBlackBox();
            _lastValidAupWriteIndex = 0;
            _lastValidAupCount = 0;
            ClearInjectedDependencies();
        }

        private void ResetVrComfortRuntimeState()
        {
            _vrSnapTurnArmed = true;
            _vrComfortVignette01 = 0f;
            _vrComfortVisualBounce01 = 0f;
            _vrComfortPeripheralBlur01 = 0f;
            _vrComfortKickSignal01 = 0f;
            _vrComfortSway = Vector2.zero;
            _vrComfortMotionVector = Vector2.zero;
            _vrComfortVelocitySq01 = 0f;
            _vrHorizonRollDampingInitialized = false;
            _vrComfortGravityScaleInitialized = false;
            InvalidateVrComfortShaderPublishCache();
            ApplyVrComfortShaderSignals(false, 0f, 0f, 0f, Vector2.zero, 0f, Vector2.zero);
            FlushVrComfortShaderSignals();
        }

        private void ResetUnderwaterSomaticRuntimeState()
        {
            _underwaterSomaticPhase = 0f;
            _underwaterSomaticWeight = 0f;
            _underwaterSomaticPitchOffset = 0f;
            _underwaterSomaticYawOffset = 0f;
            _underwaterSomaticFatigue01 = 0f;
            _underwaterSomaticFatigueBreathCooldownTimer = 0f;
        }

        private void ResetEnvironmentalDragAndTensionRuntimeState()
        {
            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragCurrentMultiplier = 1f;
            _externalEnvironmentalDragHoldTimer = 0f;
            _externalEnvironmentalDragRequestedThisStep = false;
            _cuttingTensionAnchorRequestedWS = Vector3.zero;
            _cuttingTensionAnchorCurrentWS = Vector3.zero;
            _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
            _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
            _cuttingTensionRequestedThisStep = false;
            _debugSargassumEntanglementDragRequest = 1f;
            _shoreBuoyancyBlend = 1f;
            _bottomClearance = float.PositiveInfinity;
            _bottomNormal = Vector3.up;
            _surfaceWavePoseRotation = Quaternion.identity;
            _underwaterTurbulencePoseRotation = Quaternion.identity;
            _transportCavitationEfficiency = 1f;
            _previousTransportForwardVelocity = 0f;
            _wetLensSignalIntensity = 0f;
            _wetLensPulseCooldownTimer = 0f;
            _underwaterStressSignalIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
            _sargassumFieldDensity01 = 0f;
            _sargassumMatBuoyancyBlend = 0f;
            _sargassumHighStrainIntensity = 0f;
            _sargassumHighStrainTimer = 0f;
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;
            _abyssalFlowAdvectionVelocityWS = Vector3.zero;
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _transportBailoutCooldownTimer = 0f;
            _transportEvaLockTicks = 0;
            _recentBreachExitTimer = 0f;
            _surfaceBreachFluidDragBypassTimer = 0f;
            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspCooldownTimer = 0f;
            _sargassumRestRecoveryBlend = 0f;
            _surfaceGaspSubmergedLatch = false;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
            _physicalTraumaCollisionWeight = 0f;
            _physicalTraumaCollisionHoldTimer = 0f;
            _exosuitJumpJetWakePulseTimer = 0f;
            _exosuitGrappleAnchorRequestedWS = Vector3.zero;
            _exosuitGrappleAnchorCurrentWS = Vector3.zero;
            _exosuitGrappleHoldTimer = 0f;
            _exosuitGrappleCurrentForce = 0f;
            _exosuitGrappleRequestedThisStep = false;
            ResetHeavyTowRuntimeResponse();
            _abyssalDowndraftCooldownTimer = 0f;
            _abyssalDowndraftActiveTimer = 0f;
            _abyssalDowndraftIntensity = 0f;
            _abyssalDowndraftVelocityChange = Vector3.zero;
            ResetAbyssalCurrentShearRuntime();
            _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            _previousAbyssalNoisyFlow = Vector3.zero;
            _abyssalTransportTurbulencePitchOffset = 0f;
            _abyssalTransportTurbulenceYawOffset = 0f;
            _hullStressIntensity = 0f;
            _hullStressGroanCooldownTimer = 0f;
            _hullStressHudCorruptionRefreshTimer = 0f;
            _fatalPressureSequenceTimer = 0f;
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0f;
            _fatalPressureRearmTimer = 0f;
            _fatalPressureLookYawAnchor = _cameraYaw;
            _fatalPressureLookPitchAnchor = _cameraPitch;
            ClearFatalPressureNotificationDiagnostics();
            _activeSonarPingCooldownTimer = 0f;
            _thermalUpdraftIntensity = 0f;
            _thermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
            _queuedExternalKinematicAcceleration = Vector3.zero;
            _queuedExternalKinematicVelocityChange = Vector3.zero;
            _wallKickCooldownTimer = 0f;
            _ladderSplineSnapActive = false;
            _ladderSplineSnapAxisWorld = Vector3.zero;
            _aupSpeculativeHoverTicks = 0;
            _aupSpeculativeHoverHeightMeters = 0f;
            _lastProcessedKccSlideFeedbackFrame = -1;
            _thermalUpdraftTraumaCooldownTimer = 0f;
            _vegetationDensityLinearDamping = 0f;
        }

        private void ResetTransportPlatformRuntimeState()
        {
            _activeTransportPlatform = null;
            _activeTransportPlatformBehaviour = null;
            _activeTransportPlatformTransform = null;
            _transportPlatformRotationInitialized = false;
            _lastTransportPlatformPosition = Vector3.zero;
            _currentTransportPlatformPosition = Vector3.zero;
            _lastTransportPlatformAup = default;
            _currentTransportPlatformAup = default;
            _lastTransportPlatformRotation = Quaternion.identity;
            _currentTransportPlatformRotation = Quaternion.identity;
            _transportPlatformDeltaRotation = Quaternion.identity;
            _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
            _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
            _cachedTransportPlatformBasisRotation = Quaternion.identity;
            _cachedTransportPlatformSpatialFrameValid = false;
            _transportPlatformAupFrameValid = false;
            _useFixedFrameSpatialCache = false;
        }

        private void ResetRenderInterpolationRuntimeState()
        {
            _renderInterpolatedLinearVelocity = Vector3.zero;
            _renderInterpolatedCameraYaw = _cameraYaw;
            _renderInterpolatedBodyYaw = _bodyYaw;
            _renderInterpolationStateInitialized = false;
        }

        private void UnregisterGlobalLifecycleListeners()
        {
            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
                _registeredColdTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }

            if (_registeredPlayerMovementContracts)
            {
                GlobalRegistry.UnregisterPlayerMovementContracts(this);
                _registeredPlayerMovementContracts = false;
            }

            if (_registeredHotSwapListener)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwapListener = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            CaptureLegacyCollisionContacts(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            CaptureLegacyCollisionContacts(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision == null)
                return;

            Collider collider = collision.collider;
            if (collider == null)
                return;

            int colliderId = unchecked((int)EntityId.ToULong(collider.GetEntityId()));
            if (colliderId == 0)
                return;

            for (int i = _legacyCollisionProbeHitCount - 1; i >= 0; i--)
            {
                if (GetHitColliderInstanceId(in _legacyCollisionProbeHitBuffer[i]) != colliderId)
                    continue;

                int last = _legacyCollisionProbeHitCount - 1;
                _legacyCollisionProbeHitBuffer[i] = _legacyCollisionProbeHitBuffer[last];
                _legacyCollisionProbeFrameBuffer[i] = _legacyCollisionProbeFrameBuffer[last];
                _legacyCollisionProbeHitBuffer[last] = default;
                _legacyCollisionProbeFrameBuffer[last] = -1;
                _legacyCollisionProbeHitCount = last;
            }
        }

        private void CaptureLegacyCollisionContacts(Collision collision)
        {
            if (collision == null ||
                HectonFloatingOrigin.IsShiftInProgress)
            {
                return;
            }

            int layerMask = ResolveMovementGroundLayerMask();
            if (layerMask == 0)
                return;

            int contactCount = collision.GetContacts(_collisionContactBuffer);
            if (contactCount <= 0)
                return;

            if (_legacyCollisionProbeShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                ResetLegacyCollisionProbeHits();

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            int safeContactCount = math.min(contactCount, _collisionContactBuffer.Length);
            for (int i = 0; i < safeContactCount; i++)
            {
                ContactPoint contact = _collisionContactBuffer[i];
                Collider hitCollider = ResolveOtherCollisionCollider(in contact, collision.collider);
                if (hitCollider == null)
                    continue;

                int hitColliderId = unchecked((int)EntityId.ToULong(hitCollider.GetEntityId()));
                if (hitColliderId == 0 || hitColliderId == _playerColliderInstanceId)
                    continue;

                int layerBit = 1 << hitCollider.gameObject.layer;
                if ((layerMask & layerBit) == 0)
                    continue;

                Vector3 point = contact.point;
                Vector3 normal = ResolveCollisionContactNormal(point, contact.normal);
                if (!IsFiniteVector(point) || !IsFiniteVector(normal))
                    continue;

                int writeIndex = ResolveLegacyCollisionProbeWriteIndex(hitColliderId, currentFrame);
                if (writeIndex < 0)
                    continue;

                float downwardDistance = math.max(0f, _groundCheckOrigin.y - point.y);
                _legacyCollisionProbeHitBuffer[writeIndex] = new PlayerMovementSurfaceHit
                {
                    point = HectonPlayerMotor.SafeVelocity(point, _fixedFrameBodyPosition),
                    normal = NormalizeVectorRsqrt(normal, Vector3.up),
                    distance = downwardDistance,
                    collider = hitCollider,
                    ColliderInstanceId = hitColliderId
                };
                _legacyCollisionProbeFrameBuffer[writeIndex] = currentFrame;
                _legacyCollisionProbeFrame = currentFrame;
                _legacyCollisionProbeShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            }
        }

        private int ResolveLegacyCollisionProbeWriteIndex(int colliderInstanceId, int currentFrame)
        {
            if (colliderInstanceId == 0)
                return -1;

            int count = math.min(_legacyCollisionProbeHitCount, _legacyCollisionProbeHitBuffer.Length);
            int staleIndex = -1;
            int oldestIndex = count > 0 ? 0 : -1;
            int oldestFrame = count > 0 ? _legacyCollisionProbeFrameBuffer[0] : int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (GetHitColliderInstanceId(in _legacyCollisionProbeHitBuffer[i]) == colliderInstanceId)
                    return i;

                int sampleFrame = _legacyCollisionProbeFrameBuffer[i];
                int age = currentFrame - sampleFrame;
                if ((age < 0 || age > KinematicContactMaxAgeFrames) && staleIndex < 0)
                    staleIndex = i;

                if (sampleFrame < oldestFrame)
                {
                    oldestFrame = sampleFrame;
                    oldestIndex = i;
                }
            }

            if (staleIndex >= 0)
                return staleIndex;

            if (count < _legacyCollisionProbeHitBuffer.Length)
            {
                _legacyCollisionProbeHitCount = count + 1;
                return count;
            }

            return oldestIndex;
        }

        private Collider ResolveOtherCollisionCollider(in ContactPoint contact, Collider fallback)
        {
            Collider candidate = contact.otherCollider;
            if (candidate == null || candidate == _capsuleCollider)
                candidate = contact.thisCollider;
            if (candidate == null || candidate == _capsuleCollider)
                candidate = fallback;
            if (candidate == _capsuleCollider)
                return null;
            return candidate;
        }

        private Vector3 ResolveCollisionContactNormal(Vector3 point, Vector3 rawNormal)
        {
            Vector3 normal = NormalizeVectorRsqrt(rawNormal, Vector3.up);
            Vector3 bodyCenter = _useFixedFrameSpatialCache ? _fixedFrameCapsuleCenterWS : ResolveBodyRuntimePosition();
            Vector3 toBody = bodyCenter - point;
            if (IsFiniteVector(toBody) && toBody.sqrMagnitude > 0.000001f)
            {
                float facing = normal.x * toBody.x + normal.y * toBody.y + normal.z * toBody.z;
                if (facing < 0f)
                    normal = -normal;
            }

            return normal;
        }

        private void ResetLegacyCollisionProbeHits()
        {
            int count = math.min(_legacyCollisionProbeHitCount, _legacyCollisionProbeHitBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                _legacyCollisionProbeHitBuffer[i] = default;
                _legacyCollisionProbeFrameBuffer[i] = -1;
            }

            _legacyCollisionProbeHitCount = 0;
            _legacyCollisionProbeFrame = -1;
            _legacyCollisionProbeShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        }

        private void BindInventoryLoadSource()
        {
            PlayerInventory resolvedInventory = ResolveInventoryLoadSource();
            if (resolvedInventory == null)
                return;

            if (_inventoryLoadSource == resolvedInventory)
            {
                BaselineInventoryLoadSignalRevision();
                HandleInventoryLoadChanged();
                return;
            }

            UnbindInventoryLoadSource();
            _inventoryLoadSource = resolvedInventory;
            BaselineInventoryLoadSignalRevision();
            HandleInventoryLoadChanged();
        }

        private void UnbindInventoryLoadSource()
        {
            _inventoryLoadSource = null;
            _boundInventoryLoadSignalSource = null;
            _inventoryLoadSignalHash = 0u;
            _lastInventoryLoadSignalRevision = 0u;
            _runtimeInventoryTotalMassKg = 0f;
            _runtimeInventoryLoadRatio = 0f;
            _runtimeInventoryLoad01 = 0f;
            _runtimeInventoryUpwardSwimMultiplier = 1f;
            SetRuntimeInventoryLoadMovementMultiplier(1f);
        }

        private PlayerInventory ResolveInventoryLoadSource()
        {
            if (_inventoryLoadSource != null)
                return _inventoryLoadSource;

            if (TryGetComponent(out PlayerInventory localInventory))
                return localInventory;

            return _playerInventoryService != null ? _playerInventoryService.Inventory : null;
        }

        private void RefreshInventoryLoadSignalFilter()
        {
            if (ReferenceEquals(_boundInventoryLoadSignalSource, _inventoryLoadSource))
                return;

            _boundInventoryLoadSignalSource = _inventoryLoadSource;
            _inventoryLoadSignalHash = ResolveInventoryLoadSignalHash(_inventoryLoadSource);
            _lastInventoryLoadSignalRevision = _inventoryLoadSource != null ? unchecked((uint)_inventoryLoadSource.InventoryVersion) : 0u;
        }

        private void BaselineInventoryLoadSignalRevision()
        {
            RefreshInventoryLoadSignalFilter();
            _lastInventoryLoadSignalRevision = _inventoryLoadSource != null ? unchecked((uint)_inventoryLoadSource.InventoryVersion) : 0u;
        }

        private bool ConsumeInventoryLoadSignals()
        {
            RefreshInventoryLoadSignalFilter();
            uint inventoryHash = _inventoryLoadSignalHash;
            if (inventoryHash == 0u)
                return false;

            bool changed = false;
            System.ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash ||
                    signal.Revision == 0u ||
                    (_lastInventoryLoadSignalRevision != 0u && signal.Revision <= _lastInventoryLoadSignalRevision))
                {
                    continue;
                }

                _lastInventoryLoadSignalRevision = signal.Revision;
                changed = true;
            }

            return changed;
        }

        private static uint ResolveInventoryLoadSignalHash(PlayerInventory inventory)
        {
            return inventory != null && inventory.gameObject != null
                ? unchecked((uint)EntityId.ToULong(inventory.gameObject.GetEntityId()))
                : 0u;
        }

        private void HandleInventoryLoadChanged()
        {
            float totalMassKg = 0f;
            if (_inventoryLoadSource != null)
            {
                ref readonly float currentWeightKg = ref _inventoryLoadSource.CurrentWeightKg;
                totalMassKg = currentWeightKg;
            }

            float carryCapacityKg = ResolveInventoryCarryCapacityKg();
            ApplyRuntimeInventoryMassLoad(totalMassKg, carryCapacityKg);
        }

        private float ResolveInventoryCarryCapacityKg()
        {
            return _survivalSystem != null && _survivalSystem.Stats != null
                ? math.max(0.01f, _survivalSystem.Stats.CarryCapacityKg)
                : 200f;
        }

        private static float ResolveInventoryLoadMovementMultiplier(float totalMassKg, float carryCapacityKg)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeMovementMultiplier(
                totalMassKg,
                carryCapacityKg,
                InventoryLoadMinimumMovementMultiplier);
        }

        private float ResolveRuntimeInventoryLoadMovementMultiplier()
        {
            return _runtimeInventoryLoadMovementMultiplier;
        }

        private static float ResolveInventoryLoad01(float totalMassKg, float carryCapacityKg)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeLoad01(totalMassKg, carryCapacityKg);
        }

        private static float ResolveInventoryLoadRatio(float totalMassKg, float carryCapacityKg)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeLoadRatio(totalMassKg, carryCapacityKg);
        }

        internal static bool IsCriticalInventoryLoad(float totalMassKg, float carryCapacityKg)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.IsCriticalInventoryLoad(
                ResolveInventoryLoadRatio(totalMassKg, carryCapacityKg),
                CriticalEncumbranceRatio);
        }

        internal static bool ShouldTriggerCriticalStaminaFailure(float encumbranceRatio, float stamina01)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(
                encumbranceRatio,
                stamina01,
                CriticalEncumbranceRatio,
                CriticalStaminaFailureThreshold01);
        }

        internal static Vector3 ResolveCriticalEncumbranceSwimForce(Vector3 swimForce, bool criticallyEncumbered)
        {
            if (!criticallyEncumbered || swimForce.y <= 0f)
                return swimForce;

            swimForce.y = 0f;
            return swimForce;
        }

        internal static float ResolveInventoryLoadMovementMultiplierFromLoad(float load01)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeMovementMultiplierFromLoad(
                load01,
                InventoryLoadMinimumMovementMultiplier);
        }

        internal static float ResolveInventoryUpwardSwimMultiplierFromLoad(float load01)
        {
            return Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(
                load01,
                InventoryUpwardSwimMinimumMultiplier);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!IsFiniteVector(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            InvalidateMovementProbeCaches();
            _ladderSplineSnapActive = false;
            _ladderSplineSnapAxisWorld = Vector3.zero;
            _aupSpeculativeHoverTicks = SpeculativeHoverFixedTicksAfterAupShift;
            IPhysicsStateEventService physicsStateEvents = _physicsStateEvents;
            _aupSpeculativeHoverHeightMeters = physicsStateEvents != null
                ? physicsStateEvents.ResolveSpeculativeHoverHeightMeters(
                    SpeculativeHoverBaseHeightMeters,
                    _currentTimer)
                : SpeculativeHoverBaseHeightMeters;

            _lastTransportPlatformPosition -= shiftOffset;
            _currentTransportPlatformPosition -= shiftOffset;
            _cuttingTensionAnchorRequestedWS -= shiftOffset;
            _cuttingTensionAnchorCurrentWS -= shiftOffset;
            _exosuitGrappleAnchorRequestedWS -= shiftOffset;
            _exosuitGrappleAnchorCurrentWS -= shiftOffset;
            _surfaceLockTargetY -= shiftOffset.y;
            _fallbackWaterSurfaceY -= shiftOffset.y;
            _dynamicWaterSurfaceY -= shiftOffset.y;
            float3 shiftOffset3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            _playerKinematicsNativeState.ApplyOriginShift(_dataVault, shiftOffset3);

            if (_abyssalThermalFlowSample.IsCableZone != 0)
                _abyssalThermalFlowSample.CableAnchorWS -= shiftOffset;

            if (_useFixedFrameSpatialCache)
            {
                _fixedFrameBodyPosition -= shiftOffset;
                _fixedFrameCapsuleCenterWS -= shiftOffset;
                _fixedFrameBodyBottomY -= shiftOffset.y;
                _fixedFrameBodyTopY -= shiftOffset.y;
                _fixedFrameBodyEyeY -= shiftOffset.y;
                _groundCheckOrigin -= shiftOffset;
            }

            if (_renderInterpolationStateInitialized)
            {
                _previousRenderInterpolationState.BodyPosition -= shiftOffset;
                _currentRenderInterpolationState.BodyPosition -= shiftOffset;
            }

            if (_cinematicFocusActive)
            {
                _cinematicFocusLastDistanceSq = 0f;
                GlobalTelemetryBus.PublishPerformanceWarning(_cinematicFocusTelemetryHash, _cinematicFocusHash, shiftData.Sequence);
            }

            _sargassumMovementInfluence?.ApplyOriginShiftOffset(shiftOffset);
        }

        private void TryRegisterToDispatchers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            if (!_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            }

            if (!_registeredColdTick)
            {
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
            }

            TryRegisterLateFrameTickable();

            if (!_registeredPlayerMovementContracts)
            {
                IPlayerMovementContracts currentContracts = GlobalRegistry.PlayerMovementContracts;
                if (currentContracts == null)
                    GlobalRegistry.RegisterPlayerMovementContracts(this);

                _registeredPlayerMovementContracts = ReferenceEquals(GlobalRegistry.PlayerMovementContracts, this);
            }

            if (!_registeredHotSwapListener)
                _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);

        }

        private void UnregisterDispatcherTicks()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
                _registeredColdTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void MarkLateFramePresentationDirty()
        {
            // Late-frame ticking is lifecycle-registered; hot queues only mutate pending presentation state.
        }

        public void ColdTick()
        {
            if (!Application.isPlaying)
                return;

            EnsurePlayerKinematicsNativeState();
            EnsureCinematicFocusBlackBox();
            RefreshCinematicFocusTierGateCold();
        }

        private void ResolvePlayerToolManager()
        {
            if (_resolvedPlayerToolManager)
                return;

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            if (playerInventoryService != null && playerInventoryService.ToolManager != null)
                _playerToolManager = playerInventoryService.ToolManager;

            _resolvedPlayerToolManager = true;
        }

        private void ResolvePlayerToolManagerCold()
        {
            if (_resolvedPlayerToolManager && _playerToolManager != null)
                return;

            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            if (playerInventoryService != null && playerInventoryService.ToolManager != null)
                _playerToolManager = playerInventoryService.ToolManager;

            if (_playerToolManager == null && !TryGetComponent(out _playerToolManager))
                _playerToolManager = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerToolManager>(transform);

            _resolvedPlayerToolManager = true;
        }

        private void ResolvePlayerTransportCoordinator()
        {
            if (_resolvedPlayerTransportCoordinator)
                return;

            _resolvedPlayerTransportCoordinator = true;
        }

        private void ResolvePlayerTransportCoordinatorCold()
        {
            if (_resolvedPlayerTransportCoordinator && _playerTransportCoordinator != null)
                return;

            TryGetComponent(out _playerTransportCoordinator);
            _resolvedPlayerTransportCoordinator = true;
        }

        private void ResolveActiveTransportPlatform()
        {
            if (_transportEvaLockTicks > 0)
            {
                _activeTransportPlatform = null;
                _activeTransportPlatformBehaviour = null;
                _activeTransportPlatformTransform = null;
                _transportPlatformRotationInitialized = false;
                _lastTransportPlatformPosition = Vector3.zero;
                _currentTransportPlatformPosition = Vector3.zero;
                _lastTransportPlatformAup = default;
                _currentTransportPlatformAup = default;
                _lastTransportPlatformRotation = Quaternion.identity;
                _currentTransportPlatformRotation = Quaternion.identity;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
                _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
                _cachedTransportPlatformBasisRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                _transportPlatformAupFrameValid = false;
                return;
            }

            ResolvePlayerTransportCoordinator();
            IPlayerTransportLifecycleOwner lifecycleOwner = null;
            ITransportPlatform ambientPlatform = null;
            MonoBehaviour ambientPlatformBehaviour = null;
            bool hasLifecycleOwner = _playerTransportCoordinator != null &&
                                     _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out lifecycleOwner);
            if (!hasLifecycleOwner && !PlayerTransportBinder.TryResolveAmbientSubmarinePlatform(IsInDryInterior(), out ambientPlatform, out ambientPlatformBehaviour))
            {
                _activeTransportPlatform = null;
                _activeTransportPlatformBehaviour = null;
                _activeTransportPlatformTransform = null;
                _transportPlatformRotationInitialized = false;
                _lastTransportPlatformPosition = Vector3.zero;
                _currentTransportPlatformPosition = Vector3.zero;
                _lastTransportPlatformAup = default;
                _currentTransportPlatformAup = default;
                _lastTransportPlatformRotation = Quaternion.identity;
                _currentTransportPlatformRotation = Quaternion.identity;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
                _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
                _cachedTransportPlatformBasisRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                _transportPlatformAupFrameValid = false;
                return;
            }

            ITransportPlatform platform = hasLifecycleOwner ? lifecycleOwner as ITransportPlatform : ambientPlatform;
            MonoBehaviour platformBehaviour = hasLifecycleOwner ? platform as MonoBehaviour : ambientPlatformBehaviour;
            if (platform == null || platformBehaviour == null || !platform.IsTransportPlatformActive)
            {
                _activeTransportPlatform = null;
                _activeTransportPlatformBehaviour = null;
                _activeTransportPlatformTransform = null;
                _transportPlatformRotationInitialized = false;
                _lastTransportPlatformPosition = Vector3.zero;
                _currentTransportPlatformPosition = Vector3.zero;
                _lastTransportPlatformAup = default;
                _currentTransportPlatformAup = default;
                _lastTransportPlatformRotation = Quaternion.identity;
                _currentTransportPlatformRotation = Quaternion.identity;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
                _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
                _cachedTransportPlatformBasisRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                _transportPlatformAupFrameValid = false;
                return;
            }

            if (!ReferenceEquals(_activeTransportPlatformBehaviour, platformBehaviour))
            {
                _transportPlatformRotationInitialized = false;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                _transportPlatformAupFrameValid = false;
            }

            _activeTransportPlatform = platform;
            _activeTransportPlatformBehaviour = platformBehaviour;
            _activeTransportPlatformTransform = platform.PlatformTransform;
        }

        private bool TryGetActiveTransportPlatformTransform(out Transform platformTransform)
        {
            ResolveActiveTransportPlatform();
            platformTransform = _activeTransportPlatformTransform;
            return platformTransform != null;
        }

        private void SyncTransportPlatformRotation()
        {
            if (!TryGetActiveTransportPlatformTransform(out Transform platformTransform))
            {
                _transportPlatformRotationInitialized = false;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _cachedTransportPlatformLocalToWorldMatrix = Matrix4x4.identity;
                _cachedTransportPlatformWorldToLocalMatrix = Matrix4x4.identity;
                _cachedTransportPlatformBasisRotation = Quaternion.identity;
                _cachedTransportPlatformSpatialFrameValid = false;
                return;
            }

            CacheTransportPlatformSpatialFrame(platformTransform);
            Quaternion currentPlatformRotation = platformTransform.rotation;
            Vector3 currentPlatformPosition = platformTransform.position;
            bool hasPlatformAup = TryResolveAupFromPlayerStateRuntimeDelta(
                currentPlatformPosition,
                out AbsoluteUniversePosition currentPlatformAup);

            if (!_transportPlatformRotationInitialized)
            {
                _lastTransportPlatformPosition = currentPlatformPosition;
                _currentTransportPlatformPosition = currentPlatformPosition;
                _lastTransportPlatformAup = currentPlatformAup;
                _currentTransportPlatformAup = currentPlatformAup;
                _currentTransportPlatformRotation = currentPlatformRotation;
                _lastTransportPlatformRotation = currentPlatformRotation;
                _transportPlatformDeltaRotation = Quaternion.identity;
                _transportPlatformRotationInitialized = true;
                _transportPlatformAupFrameValid = hasPlatformAup;
                return;
            }

            _currentTransportPlatformPosition = currentPlatformPosition;
            _currentTransportPlatformAup = currentPlatformAup;
            _currentTransportPlatformRotation = currentPlatformRotation;
            _transportPlatformDeltaRotation = currentPlatformRotation * ConjugateUnitQuaternion(_lastTransportPlatformRotation);
            _transportPlatformAupFrameValid = hasPlatformAup;

            if (_transportPlatformDeltaRotation == Quaternion.identity || _activeTransportPlatform == null || !_activeTransportPlatform.InheritPlatformRotation)
                return;

            ApplyInheritedTransportPlatformYaw(_transportPlatformDeltaRotation);
        }

        private void ApplyInheritedTransportPlatformYaw(Quaternion deltaRotation)
        {
            _cameraYaw = RotateYawByDelta(_cameraYaw, deltaRotation);
            _bodyYaw = RotateYawByDelta(_bodyYaw, deltaRotation);
            _fatalPressureLookYawAnchor = RotateYawByDelta(_fatalPressureLookYawAnchor, deltaRotation);
            _prevYawForMomentum = RotateYawByDelta(_prevYawForMomentum, deltaRotation);
            if (!_renderInterpolationStateInitialized)
                return;

            _previousRenderInterpolationState.CameraYaw = RotateYawByDelta(_previousRenderInterpolationState.CameraYaw, deltaRotation);
            _previousRenderInterpolationState.BodyYaw = RotateYawByDelta(_previousRenderInterpolationState.BodyYaw, deltaRotation);
            _currentRenderInterpolationState.CameraYaw = RotateYawByDelta(_currentRenderInterpolationState.CameraYaw, deltaRotation);
            _currentRenderInterpolationState.BodyYaw = RotateYawByDelta(_currentRenderInterpolationState.BodyYaw, deltaRotation);
            _renderInterpolatedCameraYaw = RotateYawByDelta(_renderInterpolatedCameraYaw, deltaRotation);
            _renderInterpolatedBodyYaw = RotateYawByDelta(_renderInterpolatedBodyYaw, deltaRotation);
        }

        private static float RotateYawByDelta(float currentYaw, Quaternion deltaRotation)
        {
            Quaternion rotatedYaw = deltaRotation * ResolveWorldYawRotation(currentYaw);
            return ExtractWorldYaw(rotatedYaw * Vector3.forward, currentYaw);
        }

        private float ResolveYawRelativeToTransportPlatform(float worldYaw)
        {
            if (!TryGetActiveTransportPlatformTransform(out _))
                return worldYaw;

            Quaternion basisRotation = ResolveTransportPlatformBasisRotation();
            Quaternion localRotation = ConjugateUnitQuaternion(basisRotation) * ResolveWorldYawRotation(worldYaw);
            Vector3 localForward = localRotation * Vector3.forward;
            float localPlanarMagnitudeSq = localForward.x * localForward.x + localForward.z * localForward.z;
            if (localPlanarMagnitudeSq <= 0.0001f)
                return worldYaw;

            return math.degrees(MathLodApproximation.ApproxAtan2Fast(localForward.x, localForward.z));
        }

        private static Quaternion ResolveWorldYawRotation(float worldYaw)
        {
            quaternion yaw = ApproximateRotationDegreesNoTrig(new float3(0f, 1f, 0f), worldYaw);
            return new Quaternion(yaw.value.x, yaw.value.y, yaw.value.z, yaw.value.w);
        }

        private static Quaternion ComposeAxisAngleDegrees(float pitchDegrees, float yawDegrees, float rollDegrees)
        {
            quaternion pitch = ApproximateRotationDegreesNoTrig(new float3(1f, 0f, 0f), pitchDegrees);
            quaternion yaw = ApproximateRotationDegreesNoTrig(new float3(0f, 1f, 0f), yawDegrees);
            quaternion roll = ApproximateRotationDegreesNoTrig(new float3(0f, 0f, 1f), rollDegrees);
            quaternion composed = math.mul(yaw, math.mul(pitch, roll));
            return new Quaternion(composed.value.x, composed.value.y, composed.value.z, composed.value.w);
        }

        private static quaternion ApproximateRotationDegreesNoTrig(float3 axis, float angleDegrees)
        {
            ResolveDegreesSinCosFast(math.select(0f, angleDegrees, math.isfinite(angleDegrees)) * 0.5f, out float sinHalf, out float cosHalf);
            float3 safeAxis = math.normalizesafe(axis, new float3(0f, 1f, 0f));
            quaternion rotation = new quaternion(new float4(safeAxis * sinHalf, cosHalf));
            float lengthSq = math.lengthsq(rotation.value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? new quaternion(rotation.value * math.rsqrt(lengthSq))
                : quaternion.identity;
        }

        private static Vector3 RotateVectorByAxisAnglesDegrees(Vector3 vector, float pitchDegrees, float yawDegrees, float rollDegrees)
        {
            return ComposeAxisAngleDegrees(pitchDegrees, yawDegrees, rollDegrees) * vector;
        }

        private static float ExtractLocalPitchDegrees(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            return FastAsinDegrees(-forward.y);
        }

        private static float ExtractLocalRollDegrees(Quaternion rotation)
        {
            Vector3 right = rotation * Vector3.right;
            return FastAsinDegrees(right.y);
        }

        private static float FastAsinDegrees(float value)
        {
            float x = math.clamp(value, -1f, 1f);
            float ax = math.abs(x);
            float oneMinus = math.max(0f, 1f - ax);
            float root = oneMinus * math.rsqrt(math.max(oneMinus, 0.000001f));
            float acosRadians = (((-0.0187293f * ax + 0.0742610f) * ax - 0.2121144f) * ax + 1.5707288f) * root;
            float asinRadians = 1.57079632679f - acosRadians;
            return math.select(asinRadians, -asinRadians, x < 0f) * 57.2957795131f;
        }

        private static float ExtractWorldYaw(Vector3 worldForward, float fallbackYaw)
        {
            Vector3 planarForward = ProjectOnPlaneFast(worldForward, Vector3.up);
            if (planarForward.sqrMagnitude <= 0.0001f)
                return fallbackYaw;

            return math.degrees(MathLodApproximation.ApproxAtan2Fast(planarForward.x, planarForward.z));
        }

        private Vector3 TransformTransportPlatformDirectionToWorld(Vector3 localDirection)
        {
            if (!TryGetActiveTransportPlatformTransform(out Transform platformTransform))
                return localDirection;

            if (!_cachedTransportPlatformSpatialFrameValid)
                CacheTransportPlatformSpatialFrame(platformTransform);

            return _cachedTransportPlatformLocalToWorldMatrix.MultiplyVector(localDirection);
        }

        private Vector3 TransformTransportPlatformDirectionToLocal(Vector3 worldDirection)
        {
            if (!TryGetActiveTransportPlatformTransform(out Transform platformTransform))
                return worldDirection;

            if (!_cachedTransportPlatformSpatialFrameValid)
                CacheTransportPlatformSpatialFrame(platformTransform);

            return _cachedTransportPlatformWorldToLocalMatrix.MultiplyVector(worldDirection);
        }

        private Vector3 ResolveTransportPlatformRelativeWorldDirection(Vector3 rawInputWorld)
        {
            if (_activeTransportPlatform == null)
                return rawInputWorld;

            Vector3 inputLocal = TransformTransportPlatformDirectionToLocal(rawInputWorld);
            float magnitude = ApproximateVectorMagnitude(rawInputWorld);
            if (inputLocal.sqrMagnitude > 0.0001f)
                inputLocal = NormalizeVectorRsqrt(inputLocal, Vector3.zero) * magnitude;

            Vector3 worldDirection = TransformTransportPlatformDirectionToWorld(inputLocal);
            return HectonPlayerMotor.SafeVelocity(worldDirection, rawInputWorld);
        }

        private Vector3 ResolveTransportPlatformRelativeWalkInputWorldDirection(float inputH, float inputV)
        {
            Vector3 rawInputWorld = ResolveWorldYawRotation(_bodyYaw) * new Vector3(inputH, 0f, inputV);
            if (_activeTransportPlatform == null)
                return rawInputWorld;

            Vector3 inputLocal = TransformTransportPlatformDirectionToLocal(rawInputWorld);
            float magnitude = ApproximateVectorMagnitude(rawInputWorld);
            if (inputLocal.sqrMagnitude > 0.0001f)
                inputLocal = NormalizeVectorRsqrt(inputLocal, Vector3.zero) * magnitude;

            Vector3 worldDirection = TransformTransportPlatformDirectionToWorld(inputLocal);
            return HectonPlayerMotor.SafeVelocity(worldDirection, rawInputWorld);
        }

        private Quaternion ResolveTransportPlatformBasisRotation()
        {
            if (!TryGetActiveTransportPlatformTransform(out Transform platformTransform))
                return Quaternion.identity;

            if (!_cachedTransportPlatformSpatialFrameValid)
                CacheTransportPlatformSpatialFrame(platformTransform);

            return _cachedTransportPlatformBasisRotation;
        }

        private void ApplyTransportPlatformCarrierMotion(float fixedDeltaTime, bool suppressPhysicsMutation)
        {
            if (_activeTransportPlatform == null || fixedDeltaTime <= 0f || !_transportPlatformRotationInitialized)
                return;

            Vector3 platformTranslation = _currentTransportPlatformPosition - _lastTransportPlatformPosition;
            if (platformTranslation.sqrMagnitude <= 0.000001f && _transportPlatformDeltaRotation == Quaternion.identity)
                return;

            if (suppressPhysicsMutation)
            {
                ConsumeTransportPlatformCarrierFrame();
                return;
            }

            Vector3 bodyPosition = _useFixedFrameSpatialCache ? _fixedFrameBodyPosition : ResolveBodyRuntimePosition();
            Vector3 bodyVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 platformVelocityAtSource = HectonPlayerMotor.SafeVelocity(_activeTransportPlatform.GetPlatformPointVelocity(bodyPosition));
            Vector3 localVelocity = IsAuthoritativeVehicleTransportActive()
                ? Vector3.zero
                : HectonPlayerMotor.SafeVelocity(bodyVelocity - platformVelocityAtSource, bodyVelocity);
            Vector3 targetPosition = ResolveTransportPlatformAupCarrierTargetPosition(bodyPosition);
            MoveMotorPosition(targetPosition);

            Vector3 platformVelocityAtTarget = HectonPlayerMotor.SafeVelocity(
                _activeTransportPlatform.GetPlatformPointVelocity(targetPosition),
                platformVelocityAtSource);
            Vector3 targetVelocity = ResolveSyncAttachedVelocity(
                platformVelocityAtTarget,
                _transportPlatformDeltaRotation * localVelocity,
                bodyVelocity);
            ApplyMotorLinearVelocity(targetVelocity);
            if (_activeTransportPlatform.InheritPlatformRotation)
                MoveMotorRotation(ResolveInheritedTransportWorldRotation(_cachedTransform != null ? _cachedTransform.rotation : Quaternion.identity));

            _lastTransportPlatformPosition = _currentTransportPlatformPosition;
            _lastTransportPlatformAup = _currentTransportPlatformAup;
            _lastTransportPlatformRotation = _currentTransportPlatformRotation;
            _transportPlatformDeltaRotation = Quaternion.identity;
        }

        private void ConsumeTransportPlatformCarrierFrame()
        {
            _lastTransportPlatformPosition = _currentTransportPlatformPosition;
            _lastTransportPlatformAup = _currentTransportPlatformAup;
            _lastTransportPlatformRotation = _currentTransportPlatformRotation;
            _transportPlatformDeltaRotation = Quaternion.identity;
        }

        private Vector3 ResolveTransportPlatformAupCarrierTargetPosition(Vector3 bodyPosition)
        {
            Vector3 fallbackTarget =
                _currentTransportPlatformPosition +
                (_transportPlatformDeltaRotation * (bodyPosition - _lastTransportPlatformPosition));
            if (!_transportPlatformAupFrameValid)
                return fallbackTarget;

            if (!TryResolveAupFromRuntimeDelta(bodyPosition, _lastTransportPlatformPosition, in _lastTransportPlatformAup, out AbsoluteUniversePosition bodyAup))
                return fallbackTarget;

            double3 bodyOffsetAbsolute = AbsoluteUniversePosition.DeltaMetersClamped(in bodyAup, in _lastTransportPlatformAup);
            float3 bodyOffset = new float3(
                (float)bodyOffsetAbsolute.x,
                (float)bodyOffsetAbsolute.y,
                (float)bodyOffsetAbsolute.z);
            if (!math.all(math.isfinite(bodyOffset)))
                return fallbackTarget;

            Vector3 rotatedOffset = _transportPlatformDeltaRotation * new Vector3(bodyOffset.x, bodyOffset.y, bodyOffset.z);
            AbsoluteUniversePosition targetAup = AbsoluteUniversePosition.OffsetMeters(
                in _currentTransportPlatformAup,
                new double3(rotatedOffset.x, rotatedOffset.y, rotatedOffset.z));
            float3 runtimeTarget = targetAup.ToRuntimeFloat3();
            Vector3 targetPosition = new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
            return IsFiniteVector(targetPosition) ? targetPosition : fallbackTarget;
        }

        private Quaternion ResolveInheritedTransportWorldRotation(Quaternion currentWorldRotation)
        {
            if (!IsFiniteQuaternion(currentWorldRotation) ||
                !IsFiniteQuaternion(_lastTransportPlatformRotation) ||
                !IsFiniteQuaternion(_currentTransportPlatformRotation))
            {
                return _transportPlatformDeltaRotation * currentWorldRotation;
            }

            Quaternion localRotationBeforeDelta = ConjugateUnitQuaternion(_lastTransportPlatformRotation) * currentWorldRotation;
            Quaternion inheritedWorldRotation = _currentTransportPlatformRotation * localRotationBeforeDelta;
            return IsFiniteQuaternion(inheritedWorldRotation)
                ? inheritedWorldRotation
                : _transportPlatformDeltaRotation * currentWorldRotation;
        }

        private static Vector3 ResolveSyncAttachedVelocity(Vector3 platformPointVelocity, Vector3 playerRelativeVelocity, Vector3 fallbackVelocity)
        {
            return HectonPlayerMotor.SafeVelocity(platformPointVelocity + playerRelativeVelocity, fallbackVelocity);
        }

        private void ExecuteTransportEvaHandoff(ITransportPlatform previousPlatform, Transform previousPlatformTransform)
        {
            if (_rb == null || previousPlatform == null)
                return;

            Vector3 exitPosition = ResolveBodyRuntimePosition();
            if (previousPlatformTransform != null)
                exitPosition = previousPlatformTransform.TransformPoint(previousPlatformTransform.InverseTransformPoint(exitPosition));

            Vector3 platformVelocity = HectonPlayerMotor.SafeVelocity(previousPlatform.GetPlatformPointVelocity(exitPosition));
            Vector3 playerWorldVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 playerRelativeVelocity = HectonPlayerMotor.SafeVelocity(playerWorldVelocity - platformVelocity, playerWorldVelocity);
            Vector3 finalVelocity = HectonPlayerMotor.SafeVelocity(platformVelocity + playerRelativeVelocity, playerWorldVelocity);
            ApplyMotorLinearVelocity(finalVelocity);
            _transportEvaLockTicks = 3;
        }

        private void ResolveSwimPresentationController()
        {
            if (_resolvedSwimPresentationController)
                return;

            _resolvedSwimPresentationController = true;
        }

        private void ResolveSwimPresentationControllerCold()
        {
            if (_resolvedSwimPresentationController && _swimPresentationController != null)
                return;

            TryGetComponent(out _swimPresentationController);
            _resolvedSwimPresentationController = true;
        }

        private IPlayerTransportSource ResolveActiveTransportSource()
        {
            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            return _playerToolManager != null && !_playerToolManager.IsSwapping
                ? _playerToolManager.CurrentToolTransportSource
                : null;
        }

        private bool IsAuthoritativeVehicleTransportActive()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator == null ||
                !_playerTransportCoordinator.TryResolveTransportSource(out IPlayerTransportSource transportSource))
            {
                return false;
            }

            return transportSource is IKinematicVehicleTransportSource kinematicVehicleTransportSource &&
                   kinematicVehicleTransportSource.IsVehicleMotionAuthoritative;
        }

        private float ResolveActiveTransportPropulsionForce()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportPropulsionForce();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.max(0f, transportSource.GetTransportPropulsionForce())
                : 0f;
        }

        private float ResolveActiveTransportSpeedMultiplier()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportSpeedMultiplier();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.max(0.01f, transportSource.GetTransportSpeedMultiplier())
                : 1f;
        }

        private float ResolveActiveTransportDragCoefficientMultiplier()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportDragCoefficientMultiplier();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.max(0.01f, transportSource.GetTransportDragCoefficientMultiplier())
                : 1f;
        }

        private float ResolveActiveTransportBoost01()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportBoost01();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.saturate(transportSource.GetTransportBoost01())
                : 0f;
        }

        private float ResolveActiveTransportCameraMotionScale()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportCameraMotionScale();

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager != null && !_playerToolManager.IsSwapping)
            {
                PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
                if (transportFeelContract != null)
                    return math.saturate(transportFeelContract.CameraMotionScale);
            }

            return 1f;
        }

        private PlayerTransportPreset ResolveActiveTransportPreset()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
            {
                PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
                if (transportPreset != null)
                    return transportPreset;
            }

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager != null && !_playerToolManager.IsSwapping)
            {
                PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
                if (transportFeelContract != null)
                    return transportFeelContract.Preset;
            }

            return null;
        }

        private void CacheBaseCollisionProfile()
        {
            _basePlayerHeight = math.max(0.5f, playerHeight);

            if (_capsuleCollider != null)
            {
                _baseCapsuleHeight = math.max(0.5f, _capsuleCollider.height);
                _baseCapsuleRadius = math.max(0.01f, _capsuleCollider.radius);
                _baseCapsuleCenter = _capsuleCollider.center;
            }
            else
            {
                _baseCapsuleHeight = _basePlayerHeight;
                _baseCapsuleRadius = math.max(0.01f, groundCheckRadius);
                _baseCapsuleCenter = Vector3.zero;
            }

            _appliedCollisionHeightScale = 1f;
            _appliedCollisionRadiusScale = 1f;
            _appliedCollisionCenterYOffset = 0f;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
        }

        private void UpdateRequestedTransportCollisionProfile(PlayerTransportPreset transportPreset)
        {
            _requestedTransportCollisionRadiusScale = ResolveTransportCollisionRadiusScale(transportPreset);
            _requestedTransportCollisionHeightScale = ResolveTransportCollisionHeightScale(transportPreset);
            _requestedTransportCollisionCenterYOffset = ResolveTransportCollisionCenterYOffset(transportPreset);
        }

        private void ApplyResolvedCollisionProfile(float radiusScale, float heightScale, float centerYOffset)
        {
            if (math.abs(_appliedCollisionRadiusScale - radiusScale) <= 0.0001f &&
                math.abs(_appliedCollisionHeightScale - heightScale) <= 0.0001f &&
                math.abs(_appliedCollisionCenterYOffset - centerYOffset) <= 0.0001f)
                return;

            _appliedCollisionRadiusScale = radiusScale;
            _appliedCollisionHeightScale = heightScale;
            _appliedCollisionCenterYOffset = centerYOffset;
            playerHeight = math.max(0.5f, _basePlayerHeight * heightScale);

            if (_capsuleCollider == null)
                return;

            float scaledRadius = math.max(0.01f, _baseCapsuleRadius * radiusScale);
            float scaledHeight = math.max(scaledRadius * 2f + 0.01f, _baseCapsuleHeight * heightScale);
            Vector3 scaledCenter = _baseCapsuleCenter;
            scaledCenter.y += centerYOffset;

            _capsuleCollider.radius = scaledRadius;
            _capsuleCollider.height = scaledHeight;
            _capsuleCollider.center = scaledCenter;
        }

        private static float ResolveTransportCollisionRadiusScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.5f, transportPreset.CollisionRadiusScale)
                : 1f;
        }

        private static float ResolveTransportCollisionHeightScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.5f, transportPreset.CollisionHeightScale)
                : 1f;
        }

        private static float ResolveTransportCollisionCenterYOffset(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? transportPreset.CollisionCenterYOffset
                : 0f;
        }

        private static float ResolveTransportForwardPitchInfluence(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.saturate(transportPreset.ForwardPitchInfluence)
                : 1f;
        }

        private static float ResolveTransportStrafeInputScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.StrafeInputScale)
                : 1f;
        }

        private static float ResolveTransportVerticalInputScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.VerticalInputScale)
                : 1f;
        }

        private static float ResolveTransportReverseThrustScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.ReverseThrustScale)
                : 1f;
        }

        private static float ResolveTransportBodyYawResponsivenessScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.1f, transportPreset.BodyYawResponsivenessScale)
                : 1f;
        }

        private float ResolveHullStressTurnResponsivenessScale(PlayerTransportPreset transportPreset)
        {
            if (transportPreset == null || _hullStressIntensity <= 0.0001f)
                return 1f;

            return math.lerp(1f, math.max(0.05f, 1f - crushDepthTurnSuppression), _hullStressIntensity);
        }

        private static float ResolveTransportSurfaceDiveAssistScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.SurfaceDiveAssistScale)
                : 1f;
        }

        private static float ResolveTransportAmbientCurrentInfluenceScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.AmbientCurrentInfluenceScale)
                : 1f;
        }

        private static float ResolveTransportSurfaceLockInfluenceScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.SurfaceLockInfluenceScale)
                : 1f;
        }

        private bool IsHeavyCarryActive()
        {
            return _physicalInteractionHandler != null && _physicalInteractionHandler.IsDraggingHeavyObject;
        }

        private float ResolveHeavyCarryForceMultiplier()
        {
            if (!IsHeavyCarryActive())
                return 1f;

            return _physicalInteractionHandler.ResolveHeavyCarryForceMultiplier();
        }

        private float ResolveHeavyCarrySpeedMultiplier()
        {
            if (!IsHeavyCarryActive())
                return 1f;

            return _physicalInteractionHandler.ResolveHeavyCarrySpeedMultiplier();
        }

        private float ResolveHeavyCarryLoad01()
        {
            if (!IsHeavyCarryActive())
                return 0f;

            return _physicalInteractionHandler.HeavyCarryLoad01;
        }

        private float ResolveHeavyCarryBodyYawSpringMultiplier()
        {
            float heavyCarryLoad = ResolveHeavyCarryLoad01();
            if (heavyCarryLoad <= 0f)
                return 1f;

            return math.lerp(1f, maxHeavyCarryBodyYawSpringMultiplier, heavyCarryLoad);
        }

        private bool RefreshHeavyTowActive()
        {
            EnsureHeavyTowWinchRuntime();
            return _heavyTowWinch != null && _heavyTowWinch.HasActiveTow;
        }

        private bool EnsureHeavyTowWinchRuntime()
        {
            return _heavyTowWinch != null;
        }

        private void UpdateHeavyTowRuntimeResponse(float fixedDeltaTime, bool suppressPhysicsMutation)
        {
            float targetPitchOffset = 0f;
            float targetRollOffset = 0f;
            Vector3 targetCameraOffset = Vector3.zero;
            Vector3 targetCenterOfMassOffset = Vector3.zero;

            if (RefreshHeavyTowActive())
            {
                float tension01 = _heavyTowWinch.CurrentTension01;
                float stress01 = _heavyTowWinch.CurrentStress01;
                float lateralPull = _heavyTowWinch.CurrentSignedLateralPull01;
                float backwardPull = _heavyTowWinch.CurrentBackwardPull01;
                float response01 = math.saturate(math.max(tension01, stress01 * 0.65f));

                targetPitchOffset = -backwardPull * heavyTowCameraPitchDegrees * response01;
                targetRollOffset = -lateralPull * heavyTowCameraRollDegrees * response01;
                targetCameraOffset.x = -lateralPull * heavyTowCameraSideOffset * response01;
                targetCameraOffset.z = -backwardPull * heavyTowCameraBackwardOffset * response01;

                targetCenterOfMassOffset.x = lateralPull * heavyTowCenterOfMassLateralShift * response01;
                targetCenterOfMassOffset.y = -heavyTowCenterOfMassDownShift * response01;
                targetCenterOfMassOffset.z = -backwardPull * heavyTowCenterOfMassRearShift * response01;
            }

            float blendT = ResolveLinearBlendT(math.max(heavyTowResponseBlendSharpness, 0.01f), fixedDeltaTime);
            _heavyTowCameraPitchOffset = math.lerp(_heavyTowCameraPitchOffset, targetPitchOffset, blendT);
            _heavyTowCameraRollOffset = math.lerp(_heavyTowCameraRollOffset, targetRollOffset, blendT);
            _heavyTowCameraLocalOffset += (targetCameraOffset - _heavyTowCameraLocalOffset) * blendT;
            _heavyTowCenterOfMassOffset += (targetCenterOfMassOffset - _heavyTowCenterOfMassOffset) * blendT;

            if (!suppressPhysicsMutation && _rb != null)
                ApplyCenterOfMassIfChanged(_baseCenterOfMass + _heavyTowCenterOfMassOffset);
        }

        private void ResetHeavyTowRuntimeResponse()
        {
            _heavyTowCameraPitchOffset = 0f;
            _heavyTowCameraRollOffset = 0f;
            _heavyTowCameraLocalOffset = Vector3.zero;
            _heavyTowCenterOfMassOffset = Vector3.zero;
            if (_rb != null)
                ApplyCenterOfMassIfChanged(_baseCenterOfMass);
        }

        private void ApplyCenterOfMassIfChanged(Vector3 targetCenterOfMass)
        {
            if (_rb == null)
                return;

            Vector3 centerOfMassDelta = _lastAppliedCenterOfMass - targetCenterOfMass;
            if (centerOfMassDelta.sqrMagnitude <= 0.0001f)
                return;

            _rb.centerOfMass = targetCenterOfMass;
            _lastAppliedCenterOfMass = targetCenterOfMass;
        }

        private void ToggleBuoyancy(bool active)
        {
            if (_buoyancy == null)
                return;

            bool suppressFluid = !active;
            if (_buoyancy.IsExternallySuppressed == suppressFluid)
                return;

            _buoyancy.SetExternalSuppression(suppressFluid);
        }

        private void AdvanceSargassumInfluence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            AdvanceSargassumInfluenceClock(fixedDeltaTime);

            if (_sargassumMovementInfluence == null)
                return;

            SargassumGlobalDragManager dragManager = _sargassumDragRuntime;
            if (dragManager != null)
            {
                Vector3 samplePosition = ResolvePlayerAupRuntimePosition();
                float rawSampleRadius = _capsuleCollider != null ? _capsuleCollider.radius : 0.5f;
                float sampleRadius = math.isfinite(rawSampleRadius) ? math.max(0.35f, rawSampleRadius) : 0.5f;
                Vector3 sampleVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
                float sampleSpeed = ApproximateVectorMagnitude(sampleVelocity);
                bool hasFieldInfluence = dragManager.SampleDetailedInfluence(
                    samplePosition,
                    sampleRadius,
                    sampleVelocity,
                    sampleSpeed,
                    out SargassumGlobalDragManager.SargassumFieldSample sample);
                _sargassumMovementInfluence.ApplyDetailedFieldInfluence(
                    hasFieldInfluence,
                    sample.SpeedMultiplier,
                    sample.DragMultiplier,
                    sample.Density01,
                    sample.AnchorWS,
                    sample.Entanglement01);
                _sargassumFieldDensity01 = hasFieldInfluence ? ResolveSargassum01(sample.Density01) : 0f;
            }
            else
            {
                _sargassumMovementInfluence.ApplyFieldInfluence(false, 1f, 1f, 0f);
                _sargassumFieldDensity01 = 0f;
            }

            _sargassumMovementInfluence.Advance(fixedDeltaTime);
            UpdateSargassumMatBuoyancyBlend(fixedDeltaTime);
            UpdateSargassumHighStrainState(fixedDeltaTime);
            ApplySargassumEnvironmentalDrag(fixedDeltaTime, transportPreset);
            ApplySargassumRestRecovery(fixedDeltaTime);
        }

        private void AdvanceAbyssalThermalInfluence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;

            if ((_isWalking && !IsExosuitTransportActive()) || IsInDryInterior())
                return;

            AbyssalThermalManager thermalManager = _thermodynamicsRuntime;
            if (thermalManager == null)
                return;

            Vector3 samplePosition = ResolvePlayerAupRuntimePosition();
            if (!IsFiniteVector(samplePosition))
                return;

            float sampleRadius = _capsuleCollider != null
                ? math.max(0.35f, ResolveAbyssalCablePositive(_capsuleCollider.radius, 0.5f))
                : 0.5f;
            bool hasPlayerSample = thermalManager.SampleThermalFlow(samplePosition, sampleRadius, out AbyssalThermalManager.ThermalFlowSample sample);
            AdvanceHeavyTowCableSnare(thermalManager);
            if (!hasPlayerSample)
                return;

            sample = SanitizeAbyssalThermalSample(sample, samplePosition);
            _abyssalThermalFlowSample = sample;
            _abyssalThermalFlowVelocityWS = sample.FlowVelocityWS;

            if (sample.DragMultiplier > 1f)
                ApplyEnvironmentalDrag(sample.DragMultiplier);

            if (sample.IsCableZone != 0)
                ApplyAbyssalCableEnvironmentalDrag(safeFixedDeltaTime, transportPreset, sample);

            if (sample.HasFlow != 0 && safeFixedDeltaTime > 0f)
                ApplyExternalThermalUpdraft(sample.FlowVelocityWS * safeFixedDeltaTime);
        }

        private void AdvanceHeavyTowCableSnare(AbyssalThermalManager thermalManager)
        {
            if (_heavyTowWinch == null || thermalManager == null)
                return;

            if (!_heavyTowWinch.TryGetTowPayloadSample(out Vector3 payloadPositionWS, out float payloadRadiusWS))
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            if (!IsFiniteVector(payloadPositionWS))
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            float safePayloadRadius = ResolveAbyssalCablePositive(payloadRadiusWS, 0.5f);
            if (!thermalManager.SampleThermalFlow(payloadPositionWS, safePayloadRadius, out AbyssalThermalManager.ThermalFlowSample payloadSample))
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            payloadSample = SanitizeAbyssalThermalSample(payloadSample, payloadPositionWS);
            if (payloadSample.IsCableZone == 0 || payloadSample.CableTension01 <= 0.0001f)
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            _heavyTowWinch.ApplyExternalCableSnare(
                payloadSample.CableAnchorWS,
                payloadSample.CableTension01,
                payloadSample.CableCutProgress01);
        }

        private float ResolveSargassumSpeedMultiplier()
        {
            if (_sargassumMovementInfluence == null)
            {
                return 1f;
            }

            float sargassumDensity01 = ResolveSargassum01(_sargassumFieldDensity01);
            float currentSpeed = _velocity.magnitude;
            float bodyTangleWeight = ResolveAuthoritativeBodyMassKg();

            return Hecton8.PureLogic.Kinematics.SargassumKelpDragCalculator.Compute(
                sargassumDensity01,
                currentSpeed,
                bodyTangleWeight);
        }

        private float ResolveSargassumDragMultiplier()
        {
            return _sargassumMovementInfluence != null
                ? ResolveSargassumDragMultiplier(_sargassumMovementInfluence.DragMultiplier)
                : 1f;
        }

        private float ResolveActiveTransportPropulsionReference(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.01f, ResolveSargassumPositive(transportPreset.PropulsionForceReference, 0.01f))
                : 0f;
        }

        private void ApplySargassumEnvironmentalDrag(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_sargassumMovementInfluence == null)
            {
                _debugSargassumEntanglementDragRequest = 1f;
                return;
            }

            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float tension = ResolveSargassum01(_sargassumMovementInfluence.Entanglement01);
            if (tension <= 0.0001f)
            {
                ApplyEnvironmentalDrag(1f);
                _debugSargassumEntanglementDragRequest = 1f;
                return;
            }

            float massReference = math.max(1f, ResolveSargassumPositive(sargassumEntanglementMassReference, 80f));
            float massRatio = math.saturate((ResolveAuthoritativeBodyMassKg() - massReference) / (massReference * 3f));
            float bodyMassScale = math.lerp(0.92f, 1.22f, massRatio);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float transportPresence01 = propulsionReference > 0f ? 1f : 0f;
            float maxExtraDrag = math.lerp(
                ResolveSargassumNonNegative(sargassumEntanglementSwimEnvironmentalDrag, 0.45f),
                ResolveSargassumNonNegative(sargassumEntanglementTransportEnvironmentalDrag, 1.15f),
                transportPresence01);
            float propulsionRelief = math.lerp(1f, 0.72f, propulsion01);
            float requestedDragMultiplier = 1f + maxExtraDrag * tension * bodyMassScale * propulsionRelief;
            requestedDragMultiplier = ResolveSargassumDragMultiplier(requestedDragMultiplier);
            ApplyEnvironmentalDrag(requestedDragMultiplier);
            ApplySargassumEscapeEnergyDrain(safeFixedDeltaTime, tension, propulsion01);
            _debugSargassumEntanglementDragRequest = requestedDragMultiplier;
        }

        private void ApplySargassumEscapeEnergyDrain(float fixedDeltaTime, float tension, float propulsion01)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float safeTension = ResolveSargassum01(tension);
            if (_survivalSystem == null || safeFixedDeltaTime <= 0f)
                return;

            float safeEnergyDrainPerSecond = ResolveSargassumNonNegative(sargassumEscapeEnergyDrainPerSecond, 0f);
            if (safeTension <= 0.0001f || safeEnergyDrainPerSecond <= 0f)
                return;

            float normalizedIntent = ResolveSargassumEscapeIntent01(propulsion01);
            if (normalizedIntent <= 0f)
                return;
            float drainAmount =
                safeEnergyDrainPerSecond *
                math.max(1f, ResolveSargassumPositive(sargassumEntanglementEscapeEnergyMultiplier, 3f)) *
                math.lerp(1f, math.max(1f, ResolveSargassumPositive(sargassumHighStrainEnergyMultiplier, 3f)), ResolveSargassum01(_sargassumHighStrainIntensity)) *
                safeTension *
                normalizedIntent *
                safeFixedDeltaTime;
            if (drainAmount <= 0.0001f)
                return;

            _survivalSystem.DrainEnergy(drainAmount);
        }

        private void ApplyAbyssalCableEnvironmentalDrag(float fixedDeltaTime, PlayerTransportPreset transportPreset, AbyssalThermalManager.ThermalFlowSample sample)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            sample = SanitizeAbyssalThermalSample(sample, ResolvePlayerAupRuntimePosition());
            float tension = ResolveAbyssalCable01(sample.CableTension01);
            if (tension <= 0.0001f)
                return;

            float massReference = math.max(1f, ResolveAbyssalCablePositive(abyssalCableEntanglementMassReference, 110f));
            float massRatio = math.saturate((ResolveAuthoritativeBodyMassKg() - massReference) / (massReference * 3f));
            float bodyMassScale = math.lerp(0.95f, 1.3f, massRatio);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float transportPresence01 = propulsionReference > 0f ? 1f : 0f;
            float maxExtraDrag = math.lerp(
                ResolveAbyssalCableNonNegative(abyssalCableEntanglementSwimEnvironmentalDrag, 1.25f),
                ResolveAbyssalCableNonNegative(abyssalCableEntanglementTransportEnvironmentalDrag, 2.85f),
                transportPresence01);
            float cutReleaseT = ResolveAbyssalCableCutRelease01(sample.CableCutProgress01);
            float propulsionRelief = math.lerp(1f, 1f - ResolveAbyssalCable01(abyssalCablePropulsionReliefAtFullCut), propulsion01 * cutReleaseT);
            float suppression = ResolveAbyssalCableSuppression01(sample.CableEscapeSuppression01);
            float requestedDragMultiplier = ResolveAbyssalCableDragMultiplier(1f + maxExtraDrag * tension * suppression * bodyMassScale * propulsionRelief);
            ApplyEnvironmentalDrag(requestedDragMultiplier);

            float drainPerSecond = ResolveAbyssalCableNonNegative(abyssalCableEscapeEnergyDrainPerSecond, 6.2f);
            if (_survivalSystem == null || safeFixedDeltaTime <= 0f || drainPerSecond <= 0f)
                return;

            float normalizedIntent = ResolveSargassumEscapeIntent01(propulsion01);
            if (normalizedIntent <= 0f)
                return;

            float drainAmount =
                drainPerSecond *
                math.max(1f, ResolveAbyssalCablePositive(abyssalCableEscapeEnergyMultiplier, 4.5f)) *
                tension *
                suppression *
                normalizedIntent *
                safeFixedDeltaTime;
            if (drainAmount <= 0.0001f)
                return;

            _survivalSystem.DrainEnergy(drainAmount);
        }

        private void UpdateSargassumHighStrainState(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            _sargassumHighStrainIntensity = ResolveSargassum01(_sargassumHighStrainIntensity);
            if (_sargassumHighStrainTimer > 0f)
            {
                _sargassumHighStrainTimer -= safeFixedDeltaTime;
                if (_sargassumHighStrainTimer < 0f)
                    _sargassumHighStrainTimer = 0f;
            }
            else if (!math.isfinite(_sargassumHighStrainTimer))
            {
                _sargassumHighStrainTimer = 0f;
            }

            if (_sargassumHighStrainTimer <= 0f)
            {
                float fadeT = ResolveLinearBlendT(12f, safeFixedDeltaTime);
                _sargassumHighStrainIntensity = math.lerp(_sargassumHighStrainIntensity, 0f, fadeT);
                if (_sargassumHighStrainIntensity < 0.0001f)
                    _sargassumHighStrainIntensity = 0f;
            }
        }

        private void UpdateSargassumMatBuoyancyBlend(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float targetBlend = 0f;
            if (!IsInDryInterior() && !_isWalking && _waterImmersionRatio > 0.05f)
            {
                float densityThreshold = math.isfinite(sargassumMatBuoyancyDensityThreshold)
                    ? math.saturate(sargassumMatBuoyancyDensityThreshold)
                    : 0.8f;
                float densityDenominator = math.max(1f - densityThreshold, 0.0001f);
                float densityT = math.saturate((ResolveSargassum01(_sargassumFieldDensity01) - densityThreshold) / densityDenominator);
                if (densityT > 0f)
                {
                    float maxDepth = math.max(0.01f, ResolveSargassumPositive(sargassumMatBuoyancyMaxDepth, 1.65f));
                    float depthT = 1f - math.saturate(_currentDepth / maxDepth);
                    targetBlend = densityT * depthT;
                }
            }

            float blendT = ResolveLinearBlendT(math.max(0.01f, ResolveSargassumPositive(sargassumMatBuoyancyBlendSharpness, 9f)), safeFixedDeltaTime);
            _sargassumMatBuoyancyBlend = math.lerp(_sargassumMatBuoyancyBlend, targetBlend, blendT);
            _sargassumMatBuoyancyBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);
        }

        private void ApplySargassumMatBuoyancySupport()
        {
            if (_sargassumMatBuoyancyBlend <= 0.001f || _isWalking || IsInDryInterior())
                return;

            float surfaceReleaseVelocity = math.max(0.01f, ResolveSargassumPositive(surfaceBreachReleaseVelocity, 1f));
            float upwardVelocityAllowance = 1f - math.saturate(math.max(0f, ResolveAuthoritativeLinearVelocity(Vector3.zero).y) / surfaceReleaseVelocity);
            if (upwardVelocityAllowance <= 0.001f)
                return;

            float buoyancyForce =
                ResolveSargassumNonNegative(_cachedGravityMagnitude, 0f) *
                ResolveAuthoritativeBodyMassKg() *
                ResolveSargassumNonNegative(sargassumMatBuoyancyForceScale, 0f) *
                ResolveSargassum01(_sargassumMatBuoyancyBlend) *
                upwardVelocityAllowance;
            if (buoyancyForce <= 0.001f)
                return;

            _forceVector.x = 0f;
            _forceVector.y = buoyancyForce;
            _forceVector.z = 0f;
            ApplyMotorAccelerationFromForce(_forceVector);
        }

        private void ApplyClampedAccelerationForce(Vector3 acceleration, float maxAcceleration)
        {
            if (_rb == null || maxAcceleration <= 0f)
                return;

            float3 acceleration3 = new float3(acceleration.x, acceleration.y, acceleration.z);
            if (!math.all(math.isfinite(acceleration3)))
                return;

            float sqrMagnitude = math.lengthsq(acceleration3);
            if (sqrMagnitude <= 0.000001f)
                return;

            System.Numerics.Vector3 currentNumVel = System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 targetNumVel = new System.Numerics.Vector3(acceleration3.x, acceleration3.y, acceleration3.z);

            System.Numerics.Vector3 result = Hecton8.PureLogic.Kinematics.KinematicAccelerationLimiter.Calculate(
                currentNumVel,
                targetNumVel,
                maxAcceleration,
                1f);

            ApplyMotorAcceleration(new Vector3(result.X, result.Y, result.Z));
        }

        private void ApplySargassumEntanglementForce(PlayerTransportPreset transportPreset)
        {
            if (_sargassumMovementInfluence == null)
                return;

            float tension = ResolveSargassum01(_sargassumMovementInfluence.Entanglement01);
            if (tension <= 0.0001f)
                return;

            Vector3 playerPosition = ResolvePlayerAupRuntimePosition();
            Vector3 anchor = _sargassumMovementInfluence.EntanglementAnchorWS;
            if (!IsFiniteVector(playerPosition) || !IsFiniteVector(anchor))
                return;

            Vector3 displacement = playerPosition - anchor;
            displacement.y *= ResolveSargassum01(sargassumEntanglementVerticalInfluence);
            float displacementSqr = displacement.sqrMagnitude;
            if (!math.isfinite(displacementSqr) || displacementSqr <= 0.00000001f)
                return;

            float inverseDisplacementMagnitude = math.rsqrt(math.max(displacementSqr, 0.000001f));
            float displacementMagnitude = displacementSqr * inverseDisplacementMagnitude;
            Vector3 springDirection = displacement * inverseDisplacementMagnitude;
            float velocityAlongSpring = DotVector(ResolveAuthoritativeLinearVelocity(Vector3.zero), springDirection);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float springRelief = math.lerp(1f, 1f - ResolveSargassum01(sargassumEntanglementEscapeRelief), propulsion01);
            float dampingRelief = math.lerp(1f, 0.82f, propulsion01);
            float springAccelerationMagnitude = displacementMagnitude * ResolveSargassumNonNegative(sargassumEntanglementSpring, 0f) * tension * springRelief;
            float dampingAccelerationMagnitude = velocityAlongSpring * ResolveSargassumNonNegative(sargassumEntanglementDamping, 0f) * tension * dampingRelief;
            float totalAccelerationMagnitude = springAccelerationMagnitude + dampingAccelerationMagnitude;
            if (!math.isfinite(totalAccelerationMagnitude) || totalAccelerationMagnitude <= 0f)
                return;

            ApplyClampedAccelerationForce(-springDirection * totalAccelerationMagnitude, ResolveSargassumNonNegative(sargassumEntanglementMaxAcceleration, 18f));

            float escapeIntent01 = ResolveSargassumEscapeIntent01(propulsion01);
            float strain01 = math.saturate(tension * escapeIntent01);
            float strainThreshold = math.isfinite(sargassumEntanglementStrainThreshold)
                ? math.saturate(sargassumEntanglementStrainThreshold)
                : 0.22f;
            if (strain01 >= strainThreshold)
            {
                SargassumGlobalDragManager.TryRaiseEntanglementStrain(
                    new SargassumGlobalDragManager.EntanglementStrainSignal
                    {
                        SourceInstanceId = _instanceId,
                        PositionWS = playerPosition,
                        AnchorWS = anchor,
                        Tension01 = tension,
                        EscapeIntent01 = escapeIntent01,
                        Shake01 = ResolveSargassum01(strain01 * ResolveSargassumNonNegative(sargassumEntanglementCameraShakeScale, 0.9f))
                    });
            }
        }

        private static float ResolveSargassumSpeedMultiplier(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.1f, 1f) : 1f;
        }

        private static float ResolveSargassumDragMultiplier(float value)
        {
            return math.isfinite(value) ? math.max(1f, value) : 1f;
        }

        private static float ResolveSargassum01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveSargassumNonNegative(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0f, fallback) : 0f;
            return math.isfinite(value) ? math.max(0f, value) : safeFallback;
        }

        private static float ResolveSargassumPositive(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0.0001f, fallback) : 0.0001f;
            return math.isfinite(value) ? math.max(0.0001f, value) : safeFallback;
        }

        private static AbyssalThermalManager.ThermalFlowSample SanitizeAbyssalThermalSample(
            AbyssalThermalManager.ThermalFlowSample sample,
            Vector3 fallbackAnchor)
        {
            if (!IsFiniteVector(sample.FlowVelocityWS))
                sample.FlowVelocityWS = Vector3.zero;
            sample.Heat01 = ResolveAbyssalCable01(sample.Heat01);
            sample.DragMultiplier = ResolveAbyssalCableDragMultiplier(sample.DragMultiplier);
            if (sample.HasFlow == 0 || sample.FlowVelocityWS.sqrMagnitude <= 0.00000001f)
                sample.HasFlow = 0;
            else
                sample.HasFlow = 1;

            if (!IsFiniteVector(sample.CableAnchorWS))
                sample.CableAnchorWS = IsFiniteVector(fallbackAnchor) ? fallbackAnchor : Vector3.zero;
            sample.CableCutProgress01 = ResolveAbyssalCable01(sample.CableCutProgress01);
            sample.CableEscapeSuppression01 = ResolveAbyssalCableSuppression01(sample.CableEscapeSuppression01);
            sample.CableTension01 = ResolveAbyssalCable01(sample.CableTension01);
            if (sample.IsCableZone == 0 || sample.CableTension01 <= 0.0001f)
            {
                sample.IsCableZone = 0;
                sample.CableTension01 = 0f;
                sample.CableEscapeSuppression01 = 1f;
                sample.CableCutProgress01 = math.max(0f, sample.CableCutProgress01);
            }
            else
            {
                sample.IsCableZone = 1;
            }

            return sample;
        }

        private static float ResolveAbyssalCableDragMultiplier(float value)
        {
            return math.isfinite(value) ? math.max(1f, value) : 1f;
        }

        private static float ResolveAbyssalCable01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveAbyssalCableSuppression01(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.25f, 1f) : 1f;
        }

        private static float ResolveAbyssalCableNonNegative(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0f, fallback) : 0f;
            return math.isfinite(value) ? math.max(0f, value) : safeFallback;
        }

        private static float ResolveAbyssalCablePositive(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0.0001f, fallback) : 0.0001f;
            return math.isfinite(value) ? math.max(0.0001f, value) : safeFallback;
        }

        private void ApplyAbyssalCableEntanglementForce(PlayerTransportPreset transportPreset)
        {
            if (_abyssalThermalFlowSample.IsCableZone == 0)
                return;

            _abyssalThermalFlowSample = SanitizeAbyssalThermalSample(_abyssalThermalFlowSample, ResolvePlayerAupRuntimePosition());
            float tension = ResolveAbyssalCable01(_abyssalThermalFlowSample.CableTension01);
            if (tension <= 0.0001f)
                return;

            Vector3 playerPosition = ResolvePlayerAupRuntimePosition();
            Vector3 anchor = _abyssalThermalFlowSample.CableAnchorWS;
            if (!IsFiniteVector(playerPosition) || !IsFiniteVector(anchor))
                return;

            Vector3 displacement = playerPosition - anchor;
            displacement.y *= ResolveAbyssalCable01(abyssalCableEntanglementVerticalInfluence);
            float displacementSqr = displacement.sqrMagnitude;
            if (!math.isfinite(displacementSqr) || displacementSqr <= 0.00000001f)
                return;

            float inverseDisplacementMagnitude = math.rsqrt(math.max(displacementSqr, 0.000001f));
            float displacementMagnitude = displacementSqr * inverseDisplacementMagnitude;
            Vector3 springDirection = displacement * inverseDisplacementMagnitude;
            float velocityAlongSpring = DotVector(ResolveAuthoritativeLinearVelocity(Vector3.zero), springDirection);
            if (!math.isfinite(velocityAlongSpring))
                velocityAlongSpring = 0f;

            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float cutReleaseT = ResolveAbyssalCableCutRelease01(_abyssalThermalFlowSample.CableCutProgress01);
            float reliefAtFullCut = ResolveAbyssalCable01(abyssalCablePropulsionReliefAtFullCut);
            float springRelief = math.lerp(1f, 1f - reliefAtFullCut, propulsion01 * cutReleaseT);
            float dampingRelief = math.lerp(1f, 0.82f, propulsion01 * cutReleaseT);
            float suppression = ResolveAbyssalCableSuppression01(_abyssalThermalFlowSample.CableEscapeSuppression01);
            float springAccelerationMagnitude =
                displacementMagnitude *
                ResolveAbyssalCableNonNegative(abyssalCableEntanglementSpring, 28f) *
                tension *
                suppression *
                springRelief;
            float dampingAccelerationMagnitude =
                velocityAlongSpring *
                ResolveAbyssalCableNonNegative(abyssalCableEntanglementDamping, 9.5f) *
                tension *
                suppression *
                dampingRelief;
            float totalAccelerationMagnitude = springAccelerationMagnitude + dampingAccelerationMagnitude;
            if (!math.isfinite(totalAccelerationMagnitude) || totalAccelerationMagnitude <= 0f)
                return;

            ApplyClampedAccelerationForce(-springDirection * totalAccelerationMagnitude, ResolveAbyssalCableNonNegative(abyssalCableEntanglementMaxAcceleration, 26f));
        }

        private float ResolveAbyssalCableCutRelease01(float cableCutProgress01)
        {
            float cutProgress = ResolveAbyssalCable01(cableCutProgress01);
            float threshold = ResolveAbyssalCable01(abyssalCableCutReleaseThreshold);
            if (cutProgress <= threshold)
                return 0f;

            return math.saturate(
                (cutProgress - threshold) /
                math.max(1f - threshold, 0.0001f));
        }

        private float ResolveSargassumEscapeIntent01(float propulsion01)
        {
            float planarInputMagnitude = ResolveSargassum01(ApproximatePlanarMagnitude(_inputH, _inputV));
            float verticalInputMagnitude = math.isfinite(_inputVertical) ? math.abs(_inputVertical) : 0f;
            float inputIntent = math.max(planarInputMagnitude, ResolveSargassum01(verticalInputMagnitude * 0.75f));
            float escapeIntent = math.max(inputIntent, ResolveSargassum01(propulsion01));
            float escapeThreshold = math.isfinite(sargassumEscapeInputThreshold)
                ? math.saturate(sargassumEscapeInputThreshold)
                : 0.2f;
            if (escapeIntent <= escapeThreshold)
                return 0f;

            return math.saturate((escapeIntent - escapeThreshold) / math.max(1f - escapeThreshold, 0.0001f));
        }

        void ISargassumGlobalDragEventListener.OnSargassumEntanglementStrain(in SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
            HandleSargassumEntanglementStrain(in signal);
        }

        void ISargassumGlobalDragEventListener.OnSargassumMassiveDisplacement(in SargassumGlobalDragManager.MassiveDisplacementSignal signal)
        {
        }

        private void HandleSargassumEntanglementStrain(in SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
            if (signal.SourceInstanceId != _instanceId)
                return;

            float shakeIntensity = ResolveSargassum01(signal.Shake01);
            float highStrainThreshold = math.isfinite(sargassumHighStrainThreshold)
                ? math.saturate(sargassumHighStrainThreshold)
                : 0.5f;
            if (shakeIntensity >= highStrainThreshold)
            {
                float highStrainDenominator = math.max(1f - highStrainThreshold, 0.0001f);
                float highStrainT = math.saturate((shakeIntensity - highStrainThreshold) / highStrainDenominator);
                _sargassumHighStrainIntensity = math.max(ResolveSargassum01(_sargassumHighStrainIntensity), highStrainT);
                _sargassumHighStrainTimer = ResolveSargassumNonNegative(sargassumHighStrainHoldTime, 0.18f);
                shakeIntensity *= math.lerp(1f, math.max(1f, ResolveSargassumPositive(sargassumHighStrainShakeBoost, 1.75f)), highStrainT);
            }

            QueueCameraEntanglementStrain(ResolveSargassum01(shakeIntensity));

            TryPlaySargassumEntanglementAudio(signal);
        }

        private void HandleSonarPingSent(float intensity)
        {
            QueueCameraSonarPing(intensity);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void TryPlaySargassumEntanglementAudio(SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
            float now = ResolveSargassumInfluenceClockSeconds();
            float shake01 = ResolveSargassum01(signal.Shake01);
            float nextAudioTime = math.isfinite(_nextSargassumEntanglementAudioTime) ? _nextSargassumEntanglementAudioTime : 0f;
            if (shake01 <= 0.0001f || now < nextAudioTime)
                return;

            AudioClip clip = sargassumEntanglementStrainClip != null
                ? sargassumEntanglementStrainClip
                : underwaterImpactClip;
            if (clip == null)
                return;

            float volume = math.lerp(0.12f, 0.42f, shake01);
            float pitch = math.lerp(0.72f, 0.94f, ResolveSargassum01(signal.EscapeIntent01));
            if (QueuePresentationAudioAtPoint(clip, signal.PositionWS, volume, pitch))
                _nextSargassumEntanglementAudioTime = now + math.max(0.05f, ResolveSargassumPositive(sargassumEntanglementAudioCooldown, 0.24f));
        }

        private void AdvanceSargassumInfluenceClock(float fixedDeltaTime)
        {
            float safeDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            _sargassumInfluenceClockSeconds = math.min(_sargassumInfluenceClockSeconds + safeDeltaTime, 86400f);
        }

        private float ResolveSargassumInfluenceClockSeconds()
        {
            return math.isfinite(_sargassumInfluenceClockSeconds) ? _sargassumInfluenceClockSeconds : 0f;
        }

        private void AdvanceExternalEnvironmentalDrag(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            if (_externalEnvironmentalDragRequestedThisStep)
            {
                _externalEnvironmentalDragRequestedMultiplier = ResolveSargassumDragMultiplier(_externalEnvironmentalDragRequestedMultiplier);
                if (_externalEnvironmentalDragRequestedMultiplier > 1f)
                    _externalEnvironmentalDragHoldTimer = ResolveSargassumNonNegative(externalEnvironmentalDragHoldTime, 0f);
            }
            else if (_externalEnvironmentalDragHoldTimer > 0f)
            {
                _externalEnvironmentalDragHoldTimer -= safeFixedDeltaTime;
                if (_externalEnvironmentalDragHoldTimer < 0f)
                    _externalEnvironmentalDragHoldTimer = 0f;
            }
            else if (!math.isfinite(_externalEnvironmentalDragHoldTimer))
            {
                _externalEnvironmentalDragHoldTimer = 0f;
            }

            float targetDragMultiplier =
                _externalEnvironmentalDragRequestedThisStep || _externalEnvironmentalDragHoldTimer > 0f
                    ? ResolveSargassumDragMultiplier(_externalEnvironmentalDragRequestedMultiplier)
                    : 1f;
            float blendT = ResolveLinearBlendT(math.max(0.01f, ResolveSargassumPositive(externalEnvironmentalDragBlendSpeed, 9f)), safeFixedDeltaTime);
            _externalEnvironmentalDragCurrentMultiplier = math.lerp(
                ResolveSargassumDragMultiplier(_externalEnvironmentalDragCurrentMultiplier),
                targetDragMultiplier,
                blendT);

            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragRequestedThisStep = false;
        }

        private float ResolveExternalEnvironmentalDragMultiplier()
        {
            float multiplier = ResolveSargassumDragMultiplier(_externalEnvironmentalDragCurrentMultiplier);
            multiplier = math.max(
                multiplier,
                PlayerSwimMotor.ResolveBrineViscosityDragMultiplier(
                    IsSubmergedInGeneratedBrine(),
                    ResolveSargassumDragMultiplier(brineViscosityDragMultiplier)));

            return multiplier;
        }

        private bool IsSubmergedInGeneratedBrine()
        {
            return HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in _playerState.AbsolutePosition);
        }

        private float ResolveExternalEnvironmentalSpeedMultiplier()
        {
            return math.rcp(ResolveExternalEnvironmentalDragMultiplier());
        }

        private float ResolveExternalEnvironmentalThrustMultiplier()
        {
            return math.rcp(ResolveExternalEnvironmentalDragMultiplier());
        }

        private void AdvanceCuttingTensionRequest(float fixedDeltaTime)
        {
            if (_cuttingTensionRequestedThisStep)
            {
                _cuttingTensionAnchorCurrentWS = _cuttingTensionAnchorRequestedWS;
                _cuttingTensionAnchorNormalCurrentWS = _cuttingTensionAnchorNormalRequestedWS;
            }
            else if (_cuttingTensionHoldTimer > 0f)
            {
                _cuttingTensionHoldTimer -= fixedDeltaTime;
                if (_cuttingTensionHoldTimer <= 0f)
                {
                    _cuttingTensionHoldTimer = 0f;
                    _cuttingTensionCurrentForce = 0f;
                }
            }
            else
            {
                _cuttingTensionCurrentForce = 0f;
            }

            _cuttingTensionRequestedThisStep = false;
        }

        private void ApplyCuttingTensionPhysics(float fixedDeltaTime)
        {
            AdvanceCuttingTensionRequest(fixedDeltaTime);
            if (_cuttingTensionHoldTimer <= 0f)
                return;

            Vector3 anchorOffset = _cuttingTensionAnchorCurrentWS - ResolvePlayerAupRuntimePosition();
            float anchorDistanceSqr = anchorOffset.sqrMagnitude;
            if (anchorDistanceSqr <= 0.00000001f)
                return;

            float inverseAnchorDistance = math.rsqrt(math.max(anchorDistanceSqr, 0.000001f));
            float anchorDistance = anchorDistanceSqr * inverseAnchorDistance;
            float extension = anchorDistance - math.max(0.01f, cuttingTensionRestLength);
            if (extension <= 0f)
            {
                _cuttingTensionCurrentForce = 0f;
                return;
            }

            Vector3 springDirection = anchorOffset * inverseAnchorDistance;
            float velocityAlongSpring = DotVector(ResolveAuthoritativeLinearVelocity(Vector3.zero), springDirection);
            float forceMagnitude = extension * cuttingTensionSpring;
            forceMagnitude -= velocityAlongSpring * cuttingTensionDamping;
            forceMagnitude = math.clamp(forceMagnitude, 0f, cuttingTensionMaxForce);
            _cuttingTensionCurrentForce = forceMagnitude;
            if (forceMagnitude <= 0.0001f)
                return;

            ApplyClampedAccelerationForce(springDirection * forceMagnitude, cuttingTensionMaxForce);
            QueueCameraEntanglementStrain(math.saturate(forceMagnitude / math.max(0.01f, cuttingTensionMaxForce)) * 0.55f);
        }

        private void AdvanceExosuitGrappleRequest(float fixedDeltaTime)
        {
            if (_exosuitGrappleRequestedThisStep)
            {
                _exosuitGrappleAnchorCurrentWS = _exosuitGrappleAnchorRequestedWS;
            }
            else if (_exosuitGrappleHoldTimer > 0f)
            {
                _exosuitGrappleHoldTimer -= fixedDeltaTime;
                if (_exosuitGrappleHoldTimer <= 0f)
                {
                    _exosuitGrappleHoldTimer = 0f;
                    _exosuitGrappleCurrentForce = 0f;
                }
            }
            else
            {
                _exosuitGrappleCurrentForce = 0f;
            }

            _exosuitGrappleRequestedThisStep = false;
        }

        private void ApplyExosuitGrapplePhysics(float fixedDeltaTime)
        {
            AdvanceExosuitGrappleRequest(fixedDeltaTime);
            if (ExosuitKinematicAuthority.HasActiveAuthority())
            {
                _exosuitGrappleCurrentForce = 0f;
                return;
            }

            if (!IsExosuitTransportActive() || _exosuitGrappleHoldTimer <= 0f)
                return;

            Vector3 anchorOffset = _exosuitGrappleAnchorCurrentWS - _rb.worldCenterOfMass;
            float anchorDistanceSqr = anchorOffset.sqrMagnitude;
            if (anchorDistanceSqr <= 0.00000001f)
                return;

            float inverseAnchorDistance = math.rsqrt(math.max(anchorDistanceSqr, 0.000001f));
            float anchorDistance = anchorDistanceSqr * inverseAnchorDistance;
            float extension = anchorDistance - math.max(0.01f, exosuitGrappleRestLength);
            if (extension <= 0f)
            {
                _exosuitGrappleCurrentForce = 0f;
                return;
            }

            Vector3 grappleDirection = anchorOffset * inverseAnchorDistance;
            float velocityAlongGrapple = DotVector(ResolveAuthoritativeLinearVelocity(Vector3.zero), grappleDirection);
            float forceMagnitude = exosuitGrappleReelForce + extension * exosuitGrappleSpring;
            forceMagnitude -= velocityAlongGrapple * exosuitGrappleDamping;
            forceMagnitude = math.clamp(forceMagnitude, 0f, exosuitGrappleMaxForce);
            _exosuitGrappleCurrentForce = forceMagnitude;
            if (forceMagnitude <= 0.0001f)
                return;

            ApplyClampedAccelerationForce(grappleDirection * forceMagnitude, exosuitGrappleMaxForce);
            QueueCameraEntanglementStrain(math.saturate(forceMagnitude / math.max(0.01f, exosuitGrappleMaxForce)) * 0.42f);
        }

        private bool TrySubmitExosuitKinematicAuthority(bool exosuitActive)
        {
            if (!exosuitActive || !ExosuitKinematicAuthority.HasActiveAuthority())
                return false;

            uint actionMask = 0u;
            if (_exosuitGrappleRequestedThisStep || _exosuitGrappleHoldTimer > 0f)
                actionMask |= ExosuitInputActions.Grab;
            if (_jumpRequested || _inputVertical > 0.0001f)
                actionMask |= ExosuitInputActions.Jump;

            float qualityWeight = ResolveExosuitKinematicQualityWeight();
            return ExosuitKinematicAuthority.TrySubmitFrameInput(
                new float2(_inputH, _inputV),
                _inputVertical,
                math.radians(_bodyYaw),
                actionMask,
                qualityWeight);
        }

        private static float ResolveExosuitKinematicQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private void AdvanceParasiteLatchInfluence(float fixedDeltaTime)
        {
            if (_parasiteLatchRequestedThisStep)
            {
                if (_parasiteLatchedRequestedCount > 0)
                    _parasiteLatchHoldTimer = parasiteLatchInfluenceHoldTime;
            }
            else if (_parasiteLatchHoldTimer > 0f)
            {
                _parasiteLatchHoldTimer -= fixedDeltaTime;
                if (_parasiteLatchHoldTimer < 0f)
                    _parasiteLatchHoldTimer = 0f;
            }

            bool keepInfluenceAlive = _parasiteLatchRequestedThisStep || _parasiteLatchHoldTimer > 0f;
            float targetCount = keepInfluenceAlive ? _parasiteLatchedRequestedCount : 0f;
            Vector3 targetCenterOfMass = keepInfluenceAlive ? _parasiteCenterOfMassRequestedLS : Vector3.zero;
            Vector3 targetHarvesterPull = keepInfluenceAlive ? _parasiteHarvesterPullRequestedWS : Vector3.zero;
            float blendT = ResolveLinearBlendT(math.max(parasiteLatchInfluenceBlendSpeed, 0.01f), fixedDeltaTime);

            _parasiteLatchedCurrentCount = RoundToIntNoMathf(math.lerp(_parasiteLatchedCurrentCount, targetCount, blendT));
            _parasiteCenterOfMassCurrentLS += (targetCenterOfMass - _parasiteCenterOfMassCurrentLS) * blendT;
            _parasiteHarvesterPullCurrentWS += (targetHarvesterPull - _parasiteHarvesterPullCurrentWS) * blendT;

            _parasiteLatchedRequestedCount = 0;
            _parasiteCenterOfMassRequestedLS = Vector3.zero;
            _parasiteHarvesterPullRequestedWS = Vector3.zero;
            _parasiteLatchRequestedThisStep = false;

            _debugParasiteLatchedCount = _parasiteLatchedCurrentCount;
            _debugParasiteCenterOfMassLS = _parasiteCenterOfMassCurrentLS;
            _debugParasiteHarvesterPullWS = _parasiteHarvesterPullCurrentWS;
        }

        private void ApplyParasiteLatchForces(float fixedDeltaTime)
        {
            if (_parasiteLatchedCurrentCount <= 0 || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            float latchForce01 = math.saturate(_parasiteLatchedCurrentCount / math.max(1f, parasiteLatchCountForFullForce));
            if (latchForce01 <= 0.0001f)
                return;

            Vector3 applicationPoint = _cachedTransform.TransformPoint(_parasiteCenterOfMassCurrentLS);
            Vector3 localCenter = _parasiteCenterOfMassCurrentLS;
            Vector3 localLateral = new Vector3(localCenter.x, 0f, localCenter.z);
            float localLateralSqr = localLateral.sqrMagnitude;
            Vector3 centerOfMassForce = Vector3.zero;
            if (localLateralSqr > 0.00000001f)
            {
                float inverseLocalLateralMagnitude = math.rsqrt(math.max(localLateralSqr, 0.00000001f));
                float localLateralMagnitude = localLateralSqr * inverseLocalLateralMagnitude;
                Vector3 localBiasDirection = localLateral * inverseLocalLateralMagnitude;
                float sideBias01 = math.saturate(localLateralMagnitude / math.max(0.25f, parasiteLatchCountForFullForce * 0.05f));
                centerOfMassForce = _cachedTransform.TransformDirection(localBiasDirection) * (parasiteCenterOfMassForce * latchForce01 * sideBias01);
            }

            Vector3 harvesterForce = Vector3.zero;
            if (_parasiteHarvesterPullCurrentWS.sqrMagnitude > 0.0001f)
                harvesterForce = NormalizeVectorRsqrt(_parasiteHarvesterPullCurrentWS, Vector3.zero) * (parasiteHarvesterPullForce * latchForce01);

            Vector3 totalForce = centerOfMassForce + harvesterForce;
            if (totalForce.sqrMagnitude <= 0.0001f)
                return;

            ApplyMotorOffCenterForce(totalForce, applicationPoint);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  COLLISION Ã¢â‚¬â€ camera shake integration
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private bool TryResolveAupFromPlayerStateRuntimeDelta(
            Vector3 targetRuntimePosition,
            out AbsoluteUniversePosition targetAup)
        {
            return TryResolveAupFromRuntimeDelta(
                targetRuntimePosition,
                ResolvePlayerAupRuntimePosition(),
                in _playerState.AbsolutePosition,
                out targetAup);
        }

        private static bool TryResolveAupFromRuntimeDelta(
            Vector3 targetRuntimePosition,
            Vector3 originRuntimePosition,
            in AbsoluteUniversePosition originAup,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!IsFiniteAup(in originAup) ||
                !IsFiniteVector(targetRuntimePosition) ||
                !IsFiniteVector(originRuntimePosition))
            {
                return false;
            }

            double3 deltaMeters = new double3(
                (double)targetRuntimePosition.x - originRuntimePosition.x,
                (double)targetRuntimePosition.y - originRuntimePosition.y,
                (double)targetRuntimePosition.z - originRuntimePosition.z);
            targetAup = AbsoluteUniversePosition.OffsetMeters(in originAup, deltaMeters);
            return IsFiniteAup(in targetAup);
        }

        private bool TryResolvePlayerRuntimeShiftOffsetY(Vector3 runtimePosition, out float shiftOffsetY)
        {
            shiftOffsetY = 0f;
            if (!IsFiniteAup(in _playerState.AbsolutePosition) || !IsFiniteVector(runtimePosition))
                return false;

            double absoluteY = _playerState.AbsolutePosition.ToAbsoluteDouble3().y;
            double shiftOffset = absoluteY - runtimePosition.y;
            if (!math.isfinite(shiftOffset) ||
                shiftOffset > float.MaxValue ||
                shiftOffset < -float.MaxValue)
            {
                return false;
            }

            shiftOffsetY = (float)shiftOffset;
            return true;
        }

        private void SanitizeKccFiniteState()
        {
            if (_rb == null)
                return;

            Vector3 linearVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float3 velocity = new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z);
            if (!MathGuard.IsFinite(velocity))
            {
                MathGuard.Check(velocity, _kccNanErrorCode);
                _velocity = Vector3.zero;
                ApplyMotorLinearVelocity(Vector3.zero);
            }

            float3 force = new float3(_forceVector.x, _forceVector.y, _forceVector.z);
            if (!MathGuard.IsFinite(force))
            {
                MathGuard.Check(force, _kccNanErrorCode);
                _forceVector = Vector3.zero;
            }
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w))) &&
                   (value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w) > 0.000001f;
        }

        private static Quaternion ConjugateUnitQuaternion(Quaternion value)
        {
            return new Quaternion(-value.x, -value.y, -value.z, value.w);
        }

        private void TryBreakSuitUpgradeFromWipeout()
        {
            if (wipeoutSuitUpgradeBreakChance <= 0f)
                return;

            SuitUpgradeManager suitUpgradeManager = _suitUpgradeRuntime;
            if (suitUpgradeManager == null)
                return;

            suitUpgradeManager.TryBreakRandomInstalledUpgrade(wipeoutSuitUpgradeBreakChance, out _);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CREST OCEAN HEIGHT SAMPLING
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void InitOceanKinematics()
        {
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterFlowVelocity = Vector3.zero;
            _dynamicWaterDisplacement = Vector3.zero;
            _dynamicAverageWaterVelocity = Vector3.zero;
            _dynamicAverageWaterDisplacement = Vector3.zero;
            _dynamicWaveHeightSpan = 0f;
            _dynamicStormIntensity = 0f;
            _crestAvailable = false;
            _crestFlowSamplingSucceeded = false;
            if (!useCrestOceanHeight)
            {
                UpdateCrestDiagnostics();
                return;
            }

            PhysicsOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out float oceanSeaLevel))
            {
                _crestAvailable = true;
                _dynamicWaterSurfaceY = oceanSeaLevel;
            }

            UpdateCrestDiagnostics();
        }

        private void UpdateOceanWaterHeight()
        {
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();

            if (!useCrestOceanHeight)
            {
                _crestAvailable = false;
                _crestSamplingSucceeded = false;
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                UpdateCrestDiagnostics();
                return;
            }

            PhysicsOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics == null || !oceanKinematics.IsAvailable)
            {
                _crestAvailable = false;
                _crestSamplingSucceeded = false;
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                UpdateCrestDiagnostics();
                return;
            }

            if (!TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out _))
            {
                _crestAvailable = false;
                _crestSamplingSucceeded = false;
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                UpdateCrestDiagnostics();
                return;
            }

            float forwardSampleDistance = ResolveCrestForwardSampleDistance();
            float lateralSampleDistance = ResolveCrestLateralSampleDistance();
            float minSpatialLength = ResolveCrestBodySampleMinLength(forwardSampleDistance, lateralSampleDistance);
            UpdateCrestQueryPoints(forwardSampleDistance, lateralSampleDistance);
            _crestAvailable = true;
            _crestSamplingSucceeded = oceanKinematics.GetWaterHeight(
                _crestQueryPoints,
                CrestBodySampleCount,
                minSpatialLength,
                _crestQueryHeights);

            if (_crestSamplingSucceeded)
            {
                bool waveQuerySucceeded = oceanKinematics.GetWaveNormal(
                    _crestQueryPoints,
                    CrestBodySampleCount,
                    minSpatialLength,
                    _crestQueryNormals,
                    _crestQueryVelocities,
                    _crestQueryDisplacements);
                if (!waveQuerySucceeded)
                {
                    for (int i = 0; i < CrestBodySampleCount; i++)
                    {
                        _crestQueryNormals[i] = Vector3.up;
                        _crestQueryVelocities[i] = Vector3.zero;
                        _crestQueryDisplacements[i] = Vector3.zero;
                    }
                }

                UpdateDynamicWaveSampling();
            }
            else
            {
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = ResolveOceanSeaLevel(oceanKinematics);
            }

            UpdateOceanFlowSampling(oceanKinematics, minSpatialLength);
            UpdateCrestDiagnostics();
        }

        private float ResolveFallbackWaterSurfaceY()
        {
            using (_waterLevelResolveProfilerMarker.Auto())
            {
                float serializedFallbackWaterSurface = ResolveSerializedFallbackWaterSurfaceY();
                bool hasOceanWaterSurface = TryResolveOceanFallbackWaterSurfaceY(out float oceanWaterSurface);

                bool hasTerrainProviderWaterSurface = false;
                float terrainProviderWaterSurface = DefaultWaterSurfaceY;
                ITerrainProvider terrainProvider = _terrainProviderRuntime;
                if (terrainProvider != null)
                    hasTerrainProviderWaterSurface = TryResolveWaterSurfaceY(terrainProvider.WaterSurfaceLevel, out terrainProviderWaterSurface);

                bool hasFluidWaterSurface = TryResolveFluidFallbackWaterSurfaceY(out float fluidWaterSurface);
                return ResolveEffectiveFallbackWaterSurfaceY(
                    serializedFallbackWaterSurface,
                    hasOceanWaterSurface,
                    oceanWaterSurface,
                    hasTerrainProviderWaterSurface,
                    terrainProviderWaterSurface,
                    hasFluidWaterSurface,
                    fluidWaterSurface);
            }
        }

        private static float ResolveEffectiveFallbackWaterSurfaceY(
            float serializedFallbackWaterSurface,
            bool hasOceanWaterSurface,
            float oceanWaterSurface,
            bool hasTerrainProviderWaterSurface,
            float terrainProviderWaterSurface,
            bool hasFluidWaterSurface,
            float fluidWaterSurface)
        {
            const float ProviderMismatchRejectMeters = 128f;

            float safeSerializedFallbackWaterSurface = TryResolveWaterSurfaceY(serializedFallbackWaterSurface, out float serializedWaterSurface)
                ? serializedWaterSurface
                : DefaultWaterSurfaceY;
            float referenceWaterSurface = safeSerializedFallbackWaterSurface;
            bool hasReferenceWaterSurface = false;

            if (hasOceanWaterSurface && TryResolveOceanWaterSurfaceY(oceanWaterSurface, out float safeOceanWaterSurface))
            {
                referenceWaterSurface = safeOceanWaterSurface;
                hasReferenceWaterSurface = true;
            }

            if (hasTerrainProviderWaterSurface &&
                TryResolveWaterSurfaceY(terrainProviderWaterSurface, out float safeTerrainProviderWaterSurface) &&
                !hasReferenceWaterSurface)
            {
                referenceWaterSurface = safeTerrainProviderWaterSurface;
                hasReferenceWaterSurface = true;
            }

            if (hasFluidWaterSurface && TryResolveWaterSurfaceY(fluidWaterSurface, out float safeFluidWaterSurface))
            {
                if (!hasReferenceWaterSurface || math.abs(safeFluidWaterSurface - referenceWaterSurface) <= ProviderMismatchRejectMeters)
                    return safeFluidWaterSurface;
            }

            return hasReferenceWaterSurface ? referenceWaterSurface : safeSerializedFallbackWaterSurface;
        }

        private bool TryResolveOceanFallbackWaterSurfaceY(out float waterSurfaceY)
        {
            PhysicsOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out waterSurfaceY))
            {
                return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private bool TryResolveFluidFallbackWaterSurfaceY(out float waterSurfaceY)
        {
            IFluidSurfaceCurrentReadModel fluidSurface = _fluidSurfaceRuntime;
            if (fluidSurface != null)
            {
                if (TryResolveWaterSurfaceY(fluidSurface.CurrentWaterLevelY, out waterSurfaceY))
                    return true;

                if (TryResolveWaterSurfaceY(fluidSurface.WaterLevel, out waterSurfaceY))
                    return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)
        {
            if (math.isfinite(candidateWaterSurfaceY) &&
                math.abs(candidateWaterSurfaceY) > 0.0001f &&
                math.abs(candidateWaterSurfaceY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceY = candidateWaterSurfaceY;
                return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private float ResolveSerializedFallbackWaterSurfaceY()
        {
            if (WorldWaterLevelCalibrationRuntimeRegistry.TryGetActiveSnapshot(out WorldWaterLevelCalibrationDTO calibrationSnapshot) &&
                TryResolveWaterSurfaceY(calibrationSnapshot.ResolvedWaterLevelY, out float calibratedWaterSurfaceY))
            {
                return calibratedWaterSurfaceY;
            }

            if (TryResolveWaterSurfaceY(waterSurfaceY, out float safeWaterSurfaceY))
                return safeWaterSurfaceY;

            return DefaultWaterSurfaceY;
        }

        private static bool TryResolveWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)
        {
            if (math.isfinite(candidateWaterSurfaceY) &&
                math.abs(candidateWaterSurfaceY) > 0.0001f &&
                math.abs(candidateWaterSurfaceY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceY = candidateWaterSurfaceY;
                return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private static float ComputeImmersionRatio(float surfaceY, float feetY, float headY, float bodyHeight)
        {
            if (!math.isfinite(surfaceY) || !math.isfinite(feetY) || !math.isfinite(headY))
                return 0f;

            if (feetY >= surfaceY)
                return 0f;

            if (headY <= surfaceY)
                return 1f;

            return math.clamp((surfaceY - feetY) / math.max(bodyHeight, 0.0001f), 0f, 1f);
        }

        private float ResolveOceanSeaLevel(PhysicsOceanKinematics oceanKinematics)
        {
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out float waterSurfaceY))
            {
                return waterSurfaceY;
            }

            return _fallbackWaterSurfaceY;
        }

        private void UpdateOceanFlowSampling(PhysicsOceanKinematics oceanKinematics, float minSpatialLength)
        {
            if (oceanKinematics == null || !oceanKinematics.IsAvailable)
            {
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                return;
            }

            _crestFlowSamplingSucceeded = oceanKinematics.GetSurfaceFlow(
                _crestQueryPoints,
                CrestBodySampleCount,
                minSpatialLength,
                _crestQueryFlows);
            if (_crestFlowSamplingSucceeded)
            {
                Vector3 averageFlow = Vector3.zero;
                for (int i = 0; i < CrestBodySampleCount; i++)
                    averageFlow += _crestQueryFlows[i];

                _dynamicWaterFlowVelocity = averageFlow / CrestBodySampleCount;
                _dynamicWaterFlowVelocity.y = 0f;
            }
            else
            {
                _dynamicWaterFlowVelocity = Vector3.zero;
            }
        }

        private void ResetDynamicWaveSampling()
        {
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterDisplacement = Vector3.zero;
            _dynamicAverageWaterVelocity = Vector3.zero;
            _dynamicAverageWaterDisplacement = Vector3.zero;
            _dynamicWaveHeightSpan = 0f;
            _dynamicStormIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
        }

        private float ResolveCrestForwardSampleDistance()
        {
            return math.max(crestBodyForwardSampleDistance, playerHeight * 0.26f);
        }

        private float ResolveCrestLateralSampleDistance()
        {
            float colliderRadius = _capsuleCollider != null ? _capsuleCollider.radius : groundCheckRadius;
            return math.max(crestBodyLateralSampleDistance, colliderRadius * 1.1f);
        }

        private float ResolveCrestBodySampleMinLength(float forwardDistance, float lateralDistance)
        {
            return math.max(crestBodySampleMinLength, math.max(forwardDistance, lateralDistance));
        }

        private void UpdateCrestQueryPoints(float forwardDistance, float lateralDistance)
        {
            Vector3 center = ResolveBodyRuntimePosition();
            ResolveDegreesSinCosFast(_bodyYaw, out float sinYaw, out float cosYaw);
            Vector3 bodyForward = new Vector3(sinYaw, 0f, cosYaw);
            Vector3 bodyRight = new Vector3(cosYaw, 0f, -sinYaw);

            _crestQueryPoints[CrestSampleCenter] = center;
            _crestQueryPoints[CrestSampleHead] = center + bodyForward * forwardDistance;
            _crestQueryPoints[CrestSampleFeet] = center - bodyForward * forwardDistance;
            _crestQueryPoints[CrestSampleLeft] = center - bodyRight * lateralDistance;
            _crestQueryPoints[CrestSampleRight] = center + bodyRight * lateralDistance;
        }

        private void UpdateDynamicWaveSampling()
        {
            Vector3 headPoint = _crestQueryPoints[CrestSampleHead];
            headPoint.y = _crestQueryHeights[CrestSampleHead];
            Vector3 feetPoint = _crestQueryPoints[CrestSampleFeet];
            feetPoint.y = _crestQueryHeights[CrestSampleFeet];
            Vector3 leftPoint = _crestQueryPoints[CrestSampleLeft];
            leftPoint.y = _crestQueryHeights[CrestSampleLeft];
            Vector3 rightPoint = _crestQueryPoints[CrestSampleRight];
            rightPoint.y = _crestQueryHeights[CrestSampleRight];

            Vector3 longitudinalAxis = headPoint - feetPoint;
            Vector3 lateralAxis = rightPoint - leftPoint;

            float longitudinalHorizontalDistance = ApproximatePlanarMagnitude(longitudinalAxis.x, longitudinalAxis.z);
            float lateralHorizontalDistance = ApproximatePlanarMagnitude(lateralAxis.x, lateralAxis.z);
            float longitudinalGradientValue = longitudinalAxis.y / math.max(longitudinalHorizontalDistance, 0.01f);
            float lateralGradientValue = lateralAxis.y / math.max(lateralHorizontalDistance, 0.01f);
            _dynamicWaveLongitudinalGradient = longitudinalHorizontalDistance > 0.01f
                ? new Vector3(longitudinalAxis.x, 0f, longitudinalAxis.z) * (longitudinalGradientValue / math.max(longitudinalHorizontalDistance, 0.01f))
                : Vector3.zero;
            _dynamicWaveLateralGradient = lateralHorizontalDistance > 0.01f
                ? new Vector3(lateralAxis.x, 0f, lateralAxis.z) * (lateralGradientValue / math.max(lateralHorizontalDistance, 0.01f))
                : Vector3.zero;
            _dynamicWaveLocalSlope.x = lateralGradientValue;
            _dynamicWaveLocalSlope.y = longitudinalGradientValue;

            Vector3 crestNormalSum = Vector3.zero;
            int crestNormalCount = 0;
            for (int i = 0; i < CrestBodySampleCount; i++)
            {
                Vector3 sampledNormal = _crestQueryNormals[i];
                float sampledNormalSq = sampledNormal.sqrMagnitude;
                if (sampledNormalSq <= 0.25f)
                    continue;

                if (sampledNormal.y < 0f)
                    sampledNormal = -sampledNormal;

                crestNormalSum += sampledNormal;
                crestNormalCount++;
            }

            Vector3 derivedNormal = crestNormalCount > 0
                ? NormalizeVectorRsqrt(crestNormalSum, Vector3.up)
                : Vector3.up;

            Vector3 averageVelocity = Vector3.zero;
            Vector3 averageDisplacement = Vector3.zero;
            for (int i = 0; i < CrestBodySampleCount; i++)
            {
                averageVelocity += _crestQueryVelocities[i];
                averageDisplacement += _crestQueryDisplacements[i];
            }

            averageVelocity /= CrestBodySampleCount;
            averageDisplacement /= CrestBodySampleCount;

            _dynamicWaterSurfaceY = _crestQueryHeights[CrestSampleCenter];
            _dynamicWaterSurfaceNormal = derivedNormal;
            _dynamicWaterSurfaceVelocity = _crestQueryVelocities[CrestSampleCenter];
            _dynamicWaterDisplacement = _crestQueryDisplacements[CrestSampleCenter];
            _dynamicAverageWaterVelocity = averageVelocity;
            _dynamicAverageWaterDisplacement = averageDisplacement;
            _dynamicWaveHeightSpan = math.max(
                math.abs(_crestQueryHeights[CrestSampleHead] - _crestQueryHeights[CrestSampleFeet]),
                math.abs(_crestQueryHeights[CrestSampleRight] - _crestQueryHeights[CrestSampleLeft]));

            float horizontalDisplacementMagnitude = ApproximatePlanarMagnitude(averageDisplacement.x, averageDisplacement.z);
            float horizontalVelocityMagnitude = ApproximatePlanarMagnitude(averageVelocity.x, averageVelocity.z);
            float heightStormT = math.saturate(
                (_dynamicWaveHeightSpan - underwaterTurbulenceHeightStart) /
                math.max(underwaterTurbulenceHeightMax - underwaterTurbulenceHeightStart, 0.01f));
            float displacementStormT = math.saturate(
                (horizontalDisplacementMagnitude - underwaterTurbulenceDisplacementStart) /
                math.max(underwaterTurbulenceDisplacementMax - underwaterTurbulenceDisplacementStart, 0.01f));
            float velocityStormT = math.saturate(horizontalVelocityMagnitude / math.max(underwaterTurbulenceVelocityMax, 0.01f));
            _dynamicStormIntensity = math.max(heightStormT, math.max(displacementStormT, velocityStormT));
        }

        private PhysicsOceanKinematics ResolveOceanKinematics()
        {
            if (oceanKinematicsProvider is PhysicsOceanKinematics assignedProvider)
            {
                _oceanKinematics = assignedProvider;
                return _oceanKinematics;
            }

            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsRuntime;
            _oceanKinematics = oceanKinematicsService != null
                ? oceanKinematicsService.ActiveProvider
                : null;
            return _oceanKinematics;
        }

        private Vector3 SampleAuthoredAmbientCurrent(Vector3 samplePosition)
        {
            IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
            if (ambientCurrent != null && ambientCurrent.TrySampleAuthoredCurrent(samplePosition, out Vector3 current))
                return current;

            return Vector3.zero;
        }

        private PlayerTransportOccupancyMode ResolveActiveTransportOccupancyMode()
        {
            ResolvePlayerTransportCoordinator();
            return _playerTransportCoordinator != null
                ? _playerTransportCoordinator.ResolveTransportOccupancyMode()
                : PlayerTransportOccupancyMode.Handheld;
        }

        private bool IsExosuitTransportActive()
        {
            if (ResolveActiveTransportPreset() == null)
                return false;

            return ResolveActiveTransportOccupancyMode() == PlayerTransportOccupancyMode.Exosuit;
        }

        private void ResolveUnderwaterVisuals()
        {
            if (_resolvedUnderwaterVisuals)
                return;

            IPlayerSensoryService playerSensoryService = _playerSensoryRuntime;
            if (playerSensoryService != null && playerSensoryService.UnderwaterVisuals != null)
                _underwaterVisuals = playerSensoryService.UnderwaterVisuals;

            _resolvedUnderwaterVisuals = true;
        }

        private void ResolveUnderwaterVisualsCold()
        {
            if (_resolvedUnderwaterVisuals && _underwaterVisuals != null)
                return;

            IPlayerSensoryService playerSensoryService = _playerSensoryRuntime;
            if (playerSensoryService != null && playerSensoryService.UnderwaterVisuals != null)
                _underwaterVisuals = playerSensoryService.UnderwaterVisuals;

            if (_underwaterVisuals == null)
                _underwaterVisuals = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<HectonUnderwaterVisuals>(transform);
            _resolvedUnderwaterVisuals = true;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INPUT SYSTEM INTEGRATION (Zero GC)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void SubscribeToInput()
        {
            if (_inputManager == null || _subscribedInputManager == _inputManager)
                return;

            _subscribedInputManager = _inputManager;
            SeedInputStateFromService(_inputManager);
        }

        private void UnsubscribeFromInput()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager = null;
            _currentInputState = default;
            _cachedMoveInput = Vector2.zero;
            _cachedVerticalInput = 0f;
            _pendingLookInput = Vector2.zero;
            SetSprintingState(false);
        }

        private void ResolveInputManagerBinding()
        {
            if (_resolvedInputManager && _inputManager != null && _subscribedInputManager == _inputManager)
                return;

            IInputService currentManager = _inputServiceRuntime;
            if (ReferenceEquals(_subscribedInputManager, currentManager) && ReferenceEquals(_inputManager, currentManager))
            {
                _resolvedInputManager = true;
                return;
            }

            UnsubscribeFromInput();
            _inputManager = currentManager;
            _resolvedInputManager = true;
            SubscribeToInput();
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SPRINT EVENTS (for CameraJuiceSystem integration)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void SetSprintingState(bool isSprinting)
        {
            if (isSprinting)
            {
                float stamina = _survivalSystem != null ? _survivalSystem.EnergyNormalized : 1f;
                isSprinting = Hecton8.PureLogic.Kinematics.SprintStaminaGate.EvaluateGate(stamina, 0.25f, 0.05f, _isSprinting);
            }

            if (_isSprinting == isSprinting)
                return;

            _isSprinting = isSprinting;
            PublishPlayerSprintStateSignal(isSprinting);
        }

        private void SeedInputStateFromService(IInputService inputService)
        {
            if (inputService == null || !inputService.IsPlayerInputEnabled)
            {
                _currentInputState = default;
                _cachedMoveInput = Vector2.zero;
                _cachedVerticalInput = 0f;
                _pendingLookInput = Vector2.zero;
                SetSprintingState(false);
                return;
            }

            _inputHandler.TryReadFrame(inputService, jumpBufferTime, false, out HectonPlayerInputFrame frame, out _);
            _currentInputState = frame.State;
            _cachedMoveInput = frame.MoveInput;
            _cachedVerticalInput = frame.VerticalInput;
            _pendingLookInput = Vector2.zero;
            SetSprintingState(frame.SprintHeld != 0);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  Tick Ã¢â‚¬â€ INPUT + CAMERA (render framerate)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public void Tick(float deltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;
            if (_juiceProcessor == null) return;

            using (_tickProfilerMarker.Auto())
            {
                _currentRenderDeltaTime = math.max(0.0001f, deltaTime);
                RefreshVrComfortSettingsCache();
                PrepareRenderTickDependencies();
                if (ConsumeInventoryLoadSignals())
                    HandleInventoryLoadChanged();

                DrainNarrativeFocusSignals();
                AdvanceCinematicFocus(deltaTime);
                if (_activeSonarPingCooldownTimer > 0f)
                {
                    _activeSonarPingCooldownTimer -= deltaTime;
                    if (_activeSonarPingCooldownTimer < 0f)
                        _activeSonarPingCooldownTimer = 0f;
                }

                if (IsGameplayInputBlockedByMenu())
                {
                    HandleMenuBlockedInput(deltaTime, suit);
                    return;
                }

                if (_inputCleared || Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    _inputCleared = false;
                }

                ProcessPlayerInputFrame();
                ProcessWipeoutInputOverride();
                ProcessLocomotionPresentationAndJuice(deltaTime, suit);
            }
        }

        private void HandleMenuBlockedInput(float deltaTime, SuitData suit)
        {
            _currentInputState = default;
            _pendingLookInput = Vector2.zero;
            _inputH = 0f; _inputV = 0f; _inputVertical = 0f; _mouseXDelta = 0f;
            SetSprintingState(false);
            _inputCleared = true;
            _lastKinematicRepairProbe = default;
            _lastKinematicRepairSnapPoint = default;
            _kinematicRepairStateBits &= ~KinematicRepairStateHasSnapBit;
            ResetKinematicRepairProbeReuseGate();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateRenderInterpolationState();
            BuildJuiceInput(deltaTime, suit);
            _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);
            UpdateUnderwaterSomaticCameraOffsets(deltaTime);
            ApplyCameraState();
            UpdateVrComfortSignals(deltaTime, Vector3.zero, 0f);
        }

        private void ProcessPlayerInputFrame()
        {
            if (_inputManager != null && _inputManager.IsPlayerInputEnabled)
            {
                _inputHandler.TryReadFrame(
                    _inputManager,
                    jumpBufferTime,
                    out HectonPlayerInputFrame inputFrame,
                    out bool jumpBuffered);
                _currentInputState = inputFrame.State;
                _cachedMoveInput = inputFrame.MoveInput;
                _cachedVerticalInput = inputFrame.VerticalInput;
                _pendingLookInput = inputFrame.LookInput;
                if (jumpBuffered)
                {
                    _jumpRequested = true;
                    _jumpBufferTimer = jumpBufferTime;
                }

                Vector2 lookDelta = _pendingLookInput;
                _pendingLookInput = Vector2.zero;
                ApplyLookInput(lookDelta);

                _inputH = _cachedMoveInput.x;
                _inputV = _cachedMoveInput.y;
                _inputVertical = _isWalking ? 0f : ResolveVerticalInput();
                if (IsAuthoritativeVehicleTransportActive())
                {
                    _inputH = 0f;
                    _inputV = 0f;
                    _inputVertical = 0f;
                }
                SetSprintingState(inputFrame.SprintHeld != 0);
            }
            else
            {
                _currentInputState = default;
                _pendingLookInput = Vector2.zero;
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                SetSprintingState(false);
                _mouseXDelta = 0f;
            }
        }

        private void ProcessWipeoutInputOverride()
        {
            if (_wipeoutTimer > 0f || _fatalPressureSequenceTimer > 0f)
            {
                _currentInputState = default;
                _pendingLookInput = Vector2.zero;
                _mouseXDelta = 0f;
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                SetSprintingState(false);
                _jumpRequested = false;
                _jumpBufferTimer = 0f;
            }
        }

        private void ProcessLocomotionPresentationAndJuice(float deltaTime, SuitData suit)
        {
            ConsumeKinematicRepairTargetProbe();
            ScheduleKinematicRepairTargetProbe();
            UpdateRenderInterpolationState();
            _feedbackVelocity = ResolveFeedbackVelocity(_renderInterpolatedLinearVelocity);
            _velocity = _feedbackVelocity;
            float currentSpeed = ApproximateVectorMagnitude(_velocity);
            float renderCameraYaw = CameraYaw;
            float yawDelta = DeltaAngleDegrees(_prevYawForMomentum, renderCameraYaw);
            UpdateVrComfortSignals(deltaTime, _velocity, yawDelta);

            if (_swimPresentationController != null)
            {
                _swimPresentationController.SyncFromLocomotion(deltaTime, true);
                _debugLastSwimPresentationDriveFrame = SystemDispatcher.CurrentFrameIndex;
            }

            BuildJuiceInput(deltaTime, suit);
            _juiceInput.speedDelta = currentSpeed - _prevSpeed;
            _juiceInput.yawDelta = yawDelta;
            _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);

            _prevSpeed = currentSpeed;
            _prevYawForMomentum = renderCameraYaw;

            if (_juiceOutput.stepEvent != 0)
            {
                PublishPlayerFootstepSignal();
                TryEmitSurfaceFootstepAudio();
                EmitExosuitFootstepSeismicPing();
                UpdateStepDiagnostics();
            }

            if (_juiceProcessor.SplashThisFrame)
            {
                PublishPlayerWaterSplashSignal(
                    _juiceProcessor.SplashIntensity,
                    _juiceProcessor.IsSubmerged,
                    EffectiveWaterSurfaceY,
                    math.abs(ResolveAuthoritativeLinearVelocity(Vector3.zero).y));
                PublishWaterTransitionSignal(
                    WaterTransitionKind.Splash,
                    _juiceProcessor.IsSubmerged,
                    _juiceProcessor.SplashIntensity,
                    EffectiveWaterSurfaceY,
                    math.abs(ResolveAuthoritativeLinearVelocity(Vector3.zero).y));
            }

            if (_juiceProcessor.SubmergeChangedThisFrame)
            {
                PublishWaterTransitionSignal(
                    WaterTransitionKind.SubmergeChanged,
                    _juiceProcessor.IsSubmerged,
                    _juiceProcessor.IsSubmerged ? 1f : 0.65f,
                    EffectiveWaterSurfaceY,
                    math.abs(ResolveAuthoritativeLinearVelocity(Vector3.zero).y));
            }

            if (_juiceProcessor.ExhaleThisFrame)
            {
                PublishPlayerExhaleSignal();
            }

            UpdateUnderwaterSomaticCameraOffsets(deltaTime);
            ApplyCameraState();
            UpdateDiagnostics(currentSpeed);
        }

        public void LateFrameTick()
        {
            FlushBrineShaderGlobals();
            FlushVrComfortShaderSignals();
            FlushCameraJuiceImpulses();
            FlushWaterEntryFovImpulses();
            FlushBottomSiltBurstVisuals();
            FlushImpactBubbleBursts();
            FlushBreachSplashRing();
            FlushPresentationAudioEvents();
            FlushQueuedFeedbackSignals();
        }

        private bool QueuePresentationAudioAtPoint(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null || volume <= 0f)
                return false;

            int writeIndex = _pendingPresentationAudioEventCount;
            if (writeIndex < MaxPendingPresentationAudioEvents)
            {
                _pendingPresentationAudioEventCount = writeIndex + 1;
            }
            else
            {
                writeIndex = MaxPendingPresentationAudioEvents - 1;
            }

            _pendingPresentationAudioEvents[writeIndex] = new PresentationAudioEvent
            {
                Clip = clip,
                Position = HectonPlayerMotor.SafeVelocity(position, ResolvePlayerAupRuntimePosition()),
                Volume = math.max(0f, volume),
                Pitch = math.max(0.01f, pitch),
                IsStatic2D = 0
            };
            MarkLateFramePresentationDirty();
            return true;
        }

        private bool QueuePresentationAudioStatic2D(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0f)
                return false;

            int writeIndex = _pendingPresentationAudioEventCount;
            if (writeIndex < MaxPendingPresentationAudioEvents)
            {
                _pendingPresentationAudioEventCount = writeIndex + 1;
            }
            else
            {
                writeIndex = MaxPendingPresentationAudioEvents - 1;
            }

            _pendingPresentationAudioEvents[writeIndex] = new PresentationAudioEvent
            {
                Clip = clip,
                Position = Vector3.zero,
                Volume = math.max(0f, volume),
                Pitch = 1f,
                IsStatic2D = 1
            };
            MarkLateFramePresentationDirty();
            return true;
        }

        private void FlushPresentationAudioEvents()
        {
            int count = _pendingPresentationAudioEventCount;
            if (count <= 0)
                return;

            _pendingPresentationAudioEventCount = 0;
            IAudioService audioManager = ResolveAudioService();
            for (int i = 0; i < count; i++)
            {
                PresentationAudioEvent audioEvent = _pendingPresentationAudioEvents[i];
                _pendingPresentationAudioEvents[i] = default;
                if (audioManager == null || audioEvent.Clip == null)
                    continue;

                if (audioEvent.IsStatic2D != 0)
                    audioManager.PlayStatic2D(audioEvent.Clip, audioEvent.Volume, audioManager.InterfaceGroup);
                else
                    audioManager.PlayAtPoint(audioEvent.Clip, audioEvent.Position, audioEvent.Volume, audioEvent.Pitch);
            }
        }

        private void QueueMovementAcousticSignal(in MovementAcousticSignal signal)
        {
            _pendingMovementAcousticSignal = signal;
            _pendingMovementAcousticSignalDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void QueueKccWallScrapeAcoustic(in AcousticPingSignal signal)
        {
            _pendingKccWallScrapeAcoustic = signal;
            _pendingKccWallScrapeAcousticDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void FlushQueuedFeedbackSignals()
        {
            if (_pendingMovementAcousticSignalDirty)
            {
                _pendingMovementAcousticSignalDirty = false;
                SignalBus<MovementAcousticSignal>.TryPushTracked(in _pendingMovementAcousticSignal, ref _signalPushDropCount);
                _pendingMovementAcousticSignal = default;
            }

            if (_pendingKccWallScrapeAcousticDirty)
            {
                _pendingKccWallScrapeAcousticDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingKccWallScrapeAcoustic, ref _signalPushDropCount);
                _pendingKccWallScrapeAcoustic = default;
            }
        }

        private void ClearPendingPresentationSync()
        {
            int audioCount = _pendingPresentationAudioEventCount;
            _pendingPresentationAudioEventCount = 0;
            for (int i = 0; i < audioCount; i++)
                _pendingPresentationAudioEvents[i] = default;

            _pendingMovementAcousticSignalDirty = false;
            _pendingMovementAcousticSignal = default;
            _pendingKccWallScrapeAcousticDirty = false;
            _pendingKccWallScrapeAcoustic = default;
            _pendingBreachBubbleCount = 0;
            _pendingWipeoutBubbleCount = 0;
            _pendingBreachSplashRingDirty = false;
            _pendingWaterEntryFovImpulseDirty = false;
            _pendingWaterEntryFovExpand = 0f;
            _pendingWaterEntryFovCompress = 0f;
            _pendingWaterEntryFovDuration = 0f;
            _pendingCameraCollisionImpulse = 0f;
            _pendingCameraEntanglementStrain = 0f;
            _pendingCameraSonarPing = 0f;
            _pendingCameraExternalRollImpulse = 0f;
            _pendingCameraExternalRollImpulseDirty = false;
            _pendingBottomSiltBurstIntensity = 0f;
            _pendingBrineShaderGlobalsDirty = false;
        }

        private void QueueWaterEntryFovImpulse(float expand, float compress, float duration)
        {
            _pendingWaterEntryFovExpand = math.max(_pendingWaterEntryFovExpand, math.max(0f, expand));
            _pendingWaterEntryFovCompress = math.max(_pendingWaterEntryFovCompress, math.max(0f, compress));
            _pendingWaterEntryFovDuration = math.max(_pendingWaterEntryFovDuration, math.max(0f, duration));
            _pendingWaterEntryFovImpulseDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void FlushWaterEntryFovImpulses()
        {
            if (!_pendingWaterEntryFovImpulseDirty)
                return;

            _pendingWaterEntryFovImpulseDirty = false;
            float expand = _pendingWaterEntryFovExpand;
            float compress = _pendingWaterEntryFovCompress;
            float duration = _pendingWaterEntryFovDuration;
            _pendingWaterEntryFovExpand = 0f;
            _pendingWaterEntryFovCompress = 0f;
            _pendingWaterEntryFovDuration = 0f;

            if (_juiceProcessor == null)
                return;

            _juiceProcessor.RegisterWaterEntryFovImpulse(expand, compress, duration);
        }

        private void QueueCameraCollisionImpulse(float speed)
        {
            if (speed <= 0f)
                return;

            _pendingCameraCollisionImpulse = math.max(_pendingCameraCollisionImpulse, speed);
            MarkLateFramePresentationDirty();
        }

        private void QueueCameraEntanglementStrain(float intensity)
        {
            float safeIntensity = math.saturate(intensity);
            if (safeIntensity <= 0f)
                return;

            _pendingCameraEntanglementStrain = math.max(_pendingCameraEntanglementStrain, safeIntensity);
            MarkLateFramePresentationDirty();
        }

        private void QueueCameraSonarPing(float intensity)
        {
            float safeIntensity = math.saturate(intensity);
            if (safeIntensity <= 0f)
                return;

            _pendingCameraSonarPing = math.max(_pendingCameraSonarPing, safeIntensity);
            MarkLateFramePresentationDirty();
        }

        private void QueueCameraExternalRollImpulse(float signedRoll)
        {
            if (!math.isfinite(signedRoll) || math.abs(signedRoll) <= 0.0001f)
                return;

            if (!_pendingCameraExternalRollImpulseDirty ||
                math.abs(signedRoll) > math.abs(_pendingCameraExternalRollImpulse))
            {
                _pendingCameraExternalRollImpulse = signedRoll;
                _pendingCameraExternalRollImpulseDirty = true;
            }

            MarkLateFramePresentationDirty();
        }

        private void FlushCameraJuiceImpulses()
        {
            float collisionImpulse = _pendingCameraCollisionImpulse;
            float entanglementStrain = _pendingCameraEntanglementStrain;
            float sonarPing = _pendingCameraSonarPing;
            float externalRoll = _pendingCameraExternalRollImpulse;
            bool hasExternalRoll = _pendingCameraExternalRollImpulseDirty;
            _pendingCameraCollisionImpulse = 0f;
            _pendingCameraEntanglementStrain = 0f;
            _pendingCameraSonarPing = 0f;
            _pendingCameraExternalRollImpulse = 0f;
            _pendingCameraExternalRollImpulseDirty = false;

            if (_juiceProcessor == null)
                return;

            if (collisionImpulse > 0f && currentSuitData != null)
                _juiceProcessor.RegisterCollisionImpulse(collisionImpulse, currentSuitData);

            if (entanglementStrain > 0f)
                _juiceProcessor.RegisterEntanglementStrain(entanglementStrain);

            if (sonarPing > 0f)
                _juiceProcessor.RegisterSonarPingImpulse(sonarPing);

            if (hasExternalRoll)
                _juiceProcessor.RegisterExternalRollImpulse(externalRoll);
        }

        private void QueueBottomSiltBurstVisual(float intensity)
        {
            float safeIntensity = math.saturate(intensity);
            if (safeIntensity <= 0f)
                return;

            _pendingBottomSiltBurstIntensity = math.max(_pendingBottomSiltBurstIntensity, safeIntensity);
            MarkLateFramePresentationDirty();
        }

        private void FlushBottomSiltBurstVisuals()
        {
            float intensity = _pendingBottomSiltBurstIntensity;
            _pendingBottomSiltBurstIntensity = 0f;
            if (intensity <= 0f)
                return;

            ResolveUnderwaterVisuals();
            _underwaterVisuals?.TriggerExternalBottomSiltBurst(intensity);
        }

        private Vector3 ResolveFeedbackVelocity(Vector3 actualVelocity)
        {
            Vector3 safeActualVelocity = HectonPlayerMotor.SafeVelocity(actualVelocity);
            if (_activeTransportPlatform == null || _activeTransportPlatformBehaviour == null)
                return safeActualVelocity;

            Vector3 riderPoint = _rb != null ? _rb.worldCenterOfMass : ResolvePlayerAupRuntimePosition();
            if (!float.IsFinite(riderPoint.x) || !float.IsFinite(riderPoint.y) || !float.IsFinite(riderPoint.z))
                riderPoint = ResolvePlayerAupRuntimePosition();

            Vector3 platformVelocity = HectonPlayerMotor.SafeVelocity(
                _activeTransportPlatform.GetPlatformPointVelocity(riderPoint),
                safeActualVelocity);
            return HectonPlayerMotor.SafeVelocity(safeActualVelocity + platformVelocity, safeActualVelocity);
        }

        internal void ResetKinematicTransientStateForTeleport()
        {
            _playerMotor?.ResetRuntimeState();
            _environmentHandler?.ResetRuntimeState();
            _stateMachine?.ResetRuntimeState();
            _playerState.ResetTransient();
            _underwaterSomaticWeight = 0f;
            _underwaterSomaticPitchOffset = 0f;
            _underwaterSomaticYawOffset = 0f;
            _underwaterSomaticFatigue01 = 0f;
            _underwaterSomaticFatigueBreathCooldownTimer = 0f;
            _surfaceBreachFluidDragBypassTimer = 0f;
            _waterTransitionHandler?.ResetRuntimeState();
            _abyssalFlowAdvectionVelocityWS = Vector3.zero;
            _queuedExternalKinematicAcceleration = Vector3.zero;
            _queuedExternalKinematicVelocityChange = Vector3.zero;
            _ladderSplineSnapActive = false;
            _ladderSplineSnapAxisWorld = Vector3.zero;
            _aupSpeculativeHoverTicks = 0;
            _aupSpeculativeHoverHeightMeters = 0f;
            _velocity = Vector3.zero;
            _feedbackVelocity = Vector3.zero;
            _lastKinematicRepairProbe = default;
            _lastKinematicRepairSnapPoint = default;
            _kinematicRepairStateBits &= ~KinematicRepairStateHasSnapBit;
            ResetKinematicRepairProbeReuseGate();

            if (_playerMotor != null)
                _playerMotor.SetLinearVelocity(Vector3.zero);

            IPhysicsService physicsService = _physicsService;
            if (_rb != null && physicsService != null)
                physicsService.QueueAngularVelocitySet(_rb, Vector3.zero, wake: false);

            ResetRenderInterpolationHistoryForTeleport();
        }

        private void ResetRenderInterpolationHistoryForTeleport()
        {
            Vector3 currentPosition = ResolveCurrentRenderInterpolationBodyPosition();
            _renderInterpolatedLinearVelocity = Vector3.zero;
            _previousRenderInterpolationState = new RenderInterpolationState
            {
                BodyPosition = currentPosition,
                CameraYaw = _cameraYaw,
                BodyYaw = _bodyYaw,
                LinearVelocity = Vector3.zero,
                VerticalVelocity = 0f
            };
            _currentRenderInterpolationState = _previousRenderInterpolationState;
            _renderInterpolatedCameraYaw = _cameraYaw;
            _renderInterpolatedBodyYaw = _bodyYaw;
            _renderInterpolationStateInitialized = false;
        }

        private void BuildJuiceInput(float deltaTime, SuitData suit)
        {
            _velocity = _feedbackVelocity;
            _juiceInput.isWalking = _isWalking ? (byte)1 : (byte)0;
            _juiceInput.locomotionMode = _currentLocomotionMode;
            _juiceInput.isGrounded = _isGrounded ? (byte)1 : (byte)0;
            _juiceInput.hasMovementInput = (_inputH != 0f || _inputV != 0f || _inputVertical != 0f) ? (byte)1 : (byte)0;
            _juiceInput.inputH = _inputH;
            _juiceInput.mouseXDelta = _mouseXDelta;
            _juiceInput.horizontalSpeed = ApproximatePlanarMagnitude(_velocity.x, _velocity.z);
            _juiceInput.verticalVelocity = _velocity.y;
            _juiceInput.wasGroundedLastFrame = _wasGroundedLastFrame ? (byte)1 : (byte)0;
            _juiceInput.deltaTime = deltaTime;
            _juiceInput.immersionRatio = _waterImmersionRatio;

            // v7.0 additions
            _juiceInput.depth = _currentDepth;
            _juiceInput.swimSpeed = ApproximateVectorMagnitude(_velocity);
            _juiceInput.cameraPitch = _cameraPitch;
            _juiceInput.swimVerticalInput = _inputVertical;
            _juiceInput.heavyCarryLoad = ResolveHeavyCarryLoad01();
            float wipeoutTransportControl = ResolveWipeoutTransportControl01();
            _juiceInput.transportBoost01 = ResolveActiveTransportBoost01() * wipeoutTransportControl;
            _juiceInput.transportCameraMotionScale = ResolveActiveTransportCameraMotionScale() * math.lerp(0.35f, 1f, wipeoutTransportControl);

            if (_swimPresentationController != null)
            {
                _juiceInput.swimPresentationMode = _swimPresentationController.CurrentMode;
                _juiceInput.swimStrokePhase = _swimPresentationController.CurrentStrokePhase;
                _juiceInput.swimPropulsionPulse = _swimPresentationController.CurrentPropulsionPulse;
                _juiceInput.swimStrokeImpulse = _swimPresentationController.CurrentStrokePowerImpulse;
                _juiceInput.swimGuideWeight = _swimPresentationController.CurrentGuideWeight;
            }
            else
            {
                _juiceInput.swimPresentationMode = PlayerSwimPresentationMode.None;
                _juiceInput.swimStrokePhase = 0f;
                _juiceInput.swimPropulsionPulse = 0f;
                _juiceInput.swimStrokeImpulse = 0f;
                _juiceInput.swimGuideWeight = 0f;
            }
        }

        private float ResolveVerticalInput()
        {
            return HectonPlayerInputHandler.ResolveVerticalInput(in _currentInputState);
        }

        private Vector2 ResolveRuntimeNarcosisLookDelta(Vector2 lookDelta)
        {
            float severity01 = math.saturate(_runtimeNarcosisInputNoise01);
            if (severity01 <= 0f)
                return lookDelta;

            float lookScale = math.lerp(1f, RuntimeNarcosisLookScaleFloor, severity01);
            Vector2 scaledLookDelta = lookDelta * lookScale;

            float lookIntent = math.max(math.abs(lookDelta.x), math.abs(lookDelta.y));
            if (lookIntent <= 0.001f)
                return scaledLookDelta;

            uint timeTick = (uint)math.max(0, (int)math.min(2147483647f, _currentTimer * 60f));
            uint narcosisSeed = AdvanceRuntimeNarcosisLcg(unchecked((uint)_instanceId) ^ timeTick ^ 0x9E3779B9u);
            float phase = _currentTimer * RuntimeNarcosisInputNoiseFrequency +
                ((narcosisSeed & 0xFFFFu) * 0.000015259022f) * TwoPi;
            float driftX = SignedTriangleRadians(phase) * RuntimeNarcosisLookNoiseScale * severity01;
            narcosisSeed = AdvanceRuntimeNarcosisLcg(narcosisSeed);
            float driftY = SignedTriangleRadians(phase * 1.4142f + ((narcosisSeed & 0xFFFFu) * 0.000015259022f) * TwoPi) *
                RuntimeNarcosisLookNoiseScale *
                0.45f *
                severity01;
            return new Vector2(scaledLookDelta.x + driftX, scaledLookDelta.y + driftY);
        }

        private void ApplyLookInput(Vector2 lookDelta)
        {
            lookDelta = ResolveRuntimeNarcosisLookDelta(lookDelta);
            ApplyCinematicFocusInputOverride(lookDelta);
            float squeeze01 = ResolveFatalPressureSqueeze01();
            float lookSensitivityScale = math.lerp(1f, fatalPressureLookSensitivityFloor, squeeze01);
            float scaledLookY = lookDelta.y * mouseSensitivity * lookSensitivityScale;

            if (_vrComfortActiveCached)
            {
                ApplyVrComfortLookInput(lookDelta, scaledLookY, squeeze01);
                return;
            }

            float scaledLookX = lookDelta.x * mouseSensitivity * lookSensitivityScale;
            _mouseXDelta = lookDelta.x * lookSensitivityScale;
            ApplyCameraYawDelta(scaledLookX);
            _cameraPitch -= scaledLookY;
            ApplyFatalPressureLookClamp(squeeze01);
        }

        private void DrainNarrativeFocusSignals()
        {
            int drained = 0;
            while (drained < CinematicFocusSignalDrainBudget &&
                   SignalBus<NarrativeFocusSignal>.TryConsumeFrame(out NarrativeFocusSignal signal))
            {
                if (cinematicFocusEnabled)
                    ApplyNarrativeFocusSignal(in signal);

                drained++;
            }

            if (!cinematicFocusEnabled && _cinematicFocusActive)
                ClearCinematicFocus(true);
        }

        private void ApplyNarrativeFocusSignal(in NarrativeFocusSignal signal)
        {
            if (signal.FocusHash == 0u || !math.isfinite(signal.Intensity01) || signal.Intensity01 <= 0f)
            {
                ClearCinematicFocus(true);
                return;
            }

            bool focusChanged = !_cinematicFocusActive || _cinematicFocusHash != signal.FocusHash;
            bool shouldPublishAudioDuck = !_cinematicFocusAudioDucked;
            _cinematicFocusTargetAup = signal.TargetAup;
            _cinematicFocusHash = signal.FocusHash;
            _cinematicFocusSubtitleHash = signal.SubtitleHash;
            _cinematicFocusIntensity01 = math.saturate(signal.Intensity01);
            _cinematicFocusTimer = math.max(0.01f, signal.DurationSeconds > 0f ? signal.DurationSeconds : cinematicFocusDefaultDuration);
            _cinematicFocusPullSuppression01 = 0f;
            _cinematicFocusSubtitleFadeDistanceSq = ResolveCinematicSubtitleFadeDistanceSq(signal.SubtitleFadeDistanceSq);
            _cinematicFocusFlags = signal.Flags;
            _cinematicFocusBoneTarget = signal.BoneTarget;
            _cinematicFocusActive = true;
            if (shouldPublishAudioDuck)
                PublishCinematicMixerState(_cinematicFocusIntensity01);
            if (focusChanged)
                GlobalTelemetryBus.PublishPerformanceWarning(_cinematicFocusTelemetryHash, _cinematicFocusHash, _cinematicFocusIntensity01);
        }

        private float ResolveCinematicSubtitleFadeDistanceSq(float signaledDistanceSq)
        {
            if (math.isfinite(signaledDistanceSq) && signaledDistanceSq > 0.01f)
                return signaledDistanceSq;

            float distance = math.max(0.1f, cinematicFocusSubtitleFadeDistance);
            return distance * distance;
        }

        private void AdvanceCinematicFocus(float deltaTime)
        {
            if (!_cinematicFocusActive)
                return;

            float safeDelta = math.max(0f, deltaTime);
            if (_cinematicFocusTimer > 0f)
            {
                _cinematicFocusTimer -= safeDelta;
                if (_cinematicFocusTimer <= 0f)
                {
                    ClearCinematicFocus(true);
                    return;
                }
            }

            float recoveryT = ResolveLinearBlendT(math.max(0.01f, cinematicFocusYieldRecoverySharpness), safeDelta);
            _cinematicFocusPullSuppression01 = math.lerp(_cinematicFocusPullSuppression01, 0f, recoveryT);
            if (_cinematicFocusPullSuppression01 <= 0.0001f)
                _cinematicFocusPullSuppression01 = 0f;
        }

        private void ApplyCinematicFocusInputOverride(Vector2 lookDelta)
        {
            if (!_cinematicFocusActive)
                return;

            float deltaSq = (lookDelta.x * lookDelta.x) + (lookDelta.y * lookDelta.y);
            float threshold = math.max(0.01f, cinematicFocusInputBreakThreshold);
            float thresholdSq = threshold * threshold;
            float invThresholdSq = math.rcp(thresholdSq);
            if (deltaSq >= thresholdSq)
            {
                BreakCinematicFocus(deltaSq);
                return;
            }

            float yieldStartSq = thresholdSq * 0.25f;
            if (deltaSq > yieldStartSq)
                _cinematicFocusPullSuppression01 = math.max(_cinematicFocusPullSuppression01, math.saturate(deltaSq * invThresholdSq));
        }

        private void BreakCinematicFocus(float inputDeltaSq)
        {
            if (!_cinematicFocusActive)
                return;

            FocusBrokenSignal signal = new FocusBrokenSignal
            {
                FocusHash = _cinematicFocusHash,
                PlayerInputDeltaSq = inputDeltaSq,
                Frame = SystemDispatcher.CurrentFrameId,
                Reason = FocusBrokenSignal.ReasonPlayerLookInput,
                Flags = _cinematicFocusFlags
            };
            SignalBus<FocusBrokenSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            GlobalTelemetryBus.PublishPerformanceWarning(_cinematicFocusFaultHash, _cinematicFocusHash, inputDeltaSq);
            ClearCinematicFocus(true);
        }

        private void ClearCinematicFocus(bool publishAudioRelease)
        {
            if (publishAudioRelease && _cinematicFocusAudioDucked)
                PublishCinematicMixerState(0f);

            _cinematicFocusActive = false;
            _cinematicFocusIntensity01 = 0f;
            _cinematicFocusTimer = 0f;
            _cinematicFocusPullSuppression01 = 0f;
            _cinematicFocusFlags = 0;
            _cinematicFocusBoneTarget = 0;
            _cinematicFocusHash = 0u;
            _cinematicFocusSubtitleHash = 0u;
            _cinematicFocusTargetAup = default;
            _cinematicFocusSubtitleFadeDistanceSq = CinematicFocusDefaultFadeDistanceSq;
            _cinematicFocusLastDistanceSq = 0f;
            _cinematicFocusLastSubtitleAlpha01 = 0f;
        }

        private void ApplyVrComfortLookInput(
            Vector2 lookDelta,
            float scaledLookY,
            float squeeze01)
        {
            float lookX = lookDelta.x;
            float absLookX = math.abs(lookX);

            if (_vrSnapTurnEnabledCached)
            {
                if (absLookX <= vrSnapTurnRearmThreshold)
                    _vrSnapTurnArmed = true;

                if (_vrSnapTurnArmed && absLookX >= vrSnapTurnThreshold)
                {
                    float yawDelta = math.sign(lookX) * math.max(1f, vrSnapTurnDegrees);
                    ApplyCameraYawDelta(yawDelta);
                    _mouseXDelta = yawDelta;
                    _vrSnapTurnArmed = false;
                    _vrSnapTurnFadeTimer = math.max(0.01f, vrSnapTurnFadeSeconds);
                    RegisterVrComfortVisualBounce(1f);
                }
                else
                {
                    _mouseXDelta = 0f;
                }
            }
            else
            {
                float turnAxis = ResolveVrSmoothTurnAxis(lookX);
                float turnDelta = turnAxis * turnAxis * math.sign(turnAxis) * math.max(1f, vrSmoothTurnDegreesPerSecond) * _currentRenderDeltaTime;
                ApplyCameraYawDelta(turnDelta);
                _mouseXDelta = turnDelta;
            }

            _cameraPitch -= scaledLookY;
            ApplyFatalPressureLookClamp(squeeze01);
        }

        private float ResolveVrSmoothTurnAxis(float rawAxis)
        {
            float absAxis = math.abs(rawAxis);
            float deadzone = math.saturate(vrSmoothTurnDeadzone);
            if (absAxis <= deadzone)
                return 0f;

            float normalized = (absAxis - deadzone) / math.max(1f - deadzone, 0.0001f);
            return math.sign(rawAxis) * math.saturate(normalized);
        }

        private void ApplyCameraYawDelta(float yawDegrees)
        {
            if (TryGetActiveTransportPlatformTransform(out _))
            {
                Quaternion basisRotation = ResolveTransportPlatformBasisRotation();
                Quaternion platformYawDelta =
                    basisRotation *
                    ResolveWorldYawRotation(yawDegrees) *
                    ConjugateUnitQuaternion(basisRotation);
                Quaternion rotatedWorldYaw = platformYawDelta * ResolveWorldYawRotation(_cameraYaw);
                _cameraYaw = ExtractWorldYaw(rotatedWorldYaw * Vector3.forward, _cameraYaw);
            }
            else
            {
                _cameraYaw += yawDegrees;
            }
        }

        private float ResolveFatalPressureSqueeze01()
        {
            if (_fatalPressureSequenceTimer <= 0f)
                return 0f;

            return math.saturate(_fatalPressureSequenceIntensity);
        }

        private void ApplyFatalPressureLookClamp(float squeeze01)
        {
            if (squeeze01 <= 0f)
            {
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);
                return;
            }

            float yawFreedom = math.lerp(fatalPressureYawFreedomStart, fatalPressureYawFreedomEnd, squeeze01);
            float pitchFreedom = math.lerp(fatalPressurePitchFreedomStart, fatalPressurePitchFreedomEnd, squeeze01);
            _cameraYaw = math.clamp(_cameraYaw, _fatalPressureLookYawAnchor - yawFreedom, _fatalPressureLookYawAnchor + yawFreedom);
            _cameraPitch = math.clamp(
                _cameraPitch,
                math.max(pitchMin, _fatalPressureLookPitchAnchor - pitchFreedom),
                math.min(pitchMax, _fatalPressureLookPitchAnchor + pitchFreedom));
        }

        private void UpdateUnderwaterSomaticCameraOffsets(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            if (safeDeltaTime <= 0f)
                return;

            if (_underwaterSomaticFatigueBreathCooldownTimer > 0f)
                _underwaterSomaticFatigueBreathCooldownTimer = math.max(0f, _underwaterSomaticFatigueBreathCooldownTimer - safeDeltaTime);

            float immersion = (!_isWalking && !IsInDryInterior())
                ? math.saturate(math.max(_smoothedImmersionRatio, _waterImmersionRatio))
                : 0f;
            float thrustIntent = math.saturate(math.max(
                ApproximatePlanarMagnitude(_inputH, _inputV),
                math.abs(_inputVertical)));
            thrustIntent = math.max(thrustIntent, math.saturate(ResolveActiveTransportBoost01()));

            float fatigueThreshold = math.max(0.01f, underwaterSomaticFatigueStaminaThreshold01);
            float stamina01 = _survivalSystem != null ? math.saturate(_survivalSystem.EnergyNormalized) : 1f;
            float targetFatigue01 = math.saturate((fatigueThreshold - stamina01) / fatigueThreshold);
            float fatigueBlendT = ResolveLinearBlendT(math.max(underwaterSomaticResponseSharpness, 0.01f), safeDeltaTime);
            _underwaterSomaticFatigue01 = math.lerp(_underwaterSomaticFatigue01, targetFatigue01, fatigueBlendT);

            if (immersion <= 0.0001f || thrustIntent <= 0.0001f)
            {
                float settleT = ResolveLinearBlendT(math.max(underwaterSomaticResponseSharpness, 0.01f), safeDeltaTime);
                _underwaterSomaticWeight = math.lerp(_underwaterSomaticWeight, 0f, settleT);
                _underwaterSomaticPitchOffset = math.lerp(_underwaterSomaticPitchOffset, 0f, settleT);
                _underwaterSomaticYawOffset = math.lerp(_underwaterSomaticYawOffset, 0f, settleT);
                if (_underwaterSomaticWeight <= 0.0001f)
                {
                    _underwaterSomaticWeight = 0f;
                    _underwaterSomaticPitchOffset = 0f;
                    _underwaterSomaticYawOffset = 0f;
                }
                return;
            }

            Vector3 velocity = HectonPlayerMotor.SafeVelocity(_feedbackVelocity);
            float speedSq = velocity.sqrMagnitude;
            float speed = speedSq > 0.000001f ? ApproximateVectorMagnitude(velocity) : 0f;
            float speed01 = math.saturate(speed / math.max(underwaterSomaticReferenceSpeed, 0.01f));
            float speedPresence = math.saturate(speed / 0.35f);
            float targetWeight = immersion * thrustIntent * speedPresence;
            float blendT = ResolveLinearBlendT(math.max(underwaterSomaticResponseSharpness, 0.01f), safeDeltaTime);
            _underwaterSomaticWeight = math.lerp(_underwaterSomaticWeight, targetWeight, blendT);
            TryEmitUnderwaterFatigueBreath(_underwaterSomaticFatigue01, immersion, thrustIntent);

            if (_underwaterSomaticWeight <= 0.0001f && targetWeight <= 0.0001f)
            {
                _underwaterSomaticWeight = 0f;
                _underwaterSomaticPitchOffset = 0f;
                _underwaterSomaticYawOffset = 0f;
                return;
            }

            float fatigueCadence = math.lerp(1f, math.max(1f, underwaterSomaticFatigueCadenceMultiplier), _underwaterSomaticFatigue01);
            float cadenceScale = math.lerp(0.55f, 1.25f, speed01) * fatigueCadence;
            _underwaterSomaticPhase += safeDeltaTime * underwaterSomaticHeadbobFrequency * TwoPi * cadenceScale;
            if (_underwaterSomaticPhase >= TwoPi)
                _underwaterSomaticPhase = math.fmod(_underwaterSomaticPhase, TwoPi);

            Vector3 localVelocity = _cachedTransform != null
                ? _cachedTransform.InverseTransformDirection(velocity)
                : velocity;
            float invSpeed = speed > 0.0001f ? 1f / speed : 0f;
            float lateralVelocity01 = math.clamp(localVelocity.x * invSpeed, -1f, 1f);
            float verticalVelocity01 = math.clamp(localVelocity.y * invSpeed, -1f, 1f);
            float forwardVelocity01 = math.clamp(localVelocity.z * invSpeed, -1f, 1f);

            float phase = _underwaterSomaticPhase;
            float primaryWave = SignedTriangleRadians(phase);
            float secondaryWave = SignedTriangleRadians(phase * 0.5f + 1.5707964f);
            float lateralWave = SignedTriangleRadians(phase * 0.73f + 1.5707964f);
            float amplitudeDamping = math.lerp(1f, 0.45f, speed01);
            float fatigueAmplitude = math.lerp(1f, math.max(1f, underwaterSomaticFatigueSwayMultiplier), _underwaterSomaticFatigue01);
            float weight = _underwaterSomaticWeight * amplitudeDamping * fatigueAmplitude;

            _underwaterSomaticPitchOffset =
                (primaryWave * underwaterSomaticPitchDegrees * math.max(0.35f, math.abs(forwardVelocity01)) +
                 secondaryWave * underwaterSomaticPitchDegrees * 0.35f * verticalVelocity01) * weight;
            _underwaterSomaticYawOffset =
                (lateralWave * underwaterSomaticYawDegrees * math.max(0.35f, math.abs(lateralVelocity01)) +
                 lateralVelocity01 * underwaterSomaticYawDegrees * 0.45f) * weight;
        }

        private void TryEmitUnderwaterFatigueBreath(float fatigue01, float immersion, float thrustIntent)
        {
            if (fatigue01 <= 0.35f ||
                immersion <= 0.35f ||
                thrustIntent <= 0.05f ||
                _underwaterSomaticFatigueBreathCooldownTimer > 0f ||
                surfaceGaspClip == null)
            {
                return;
            }

            float volume = surfaceGaspVolume * underwaterSomaticFatigueBreathVolumeScale * math.saturate(fatigue01);
            if (volume <= 0.001f)
                return;

            if (!QueuePresentationAudioStatic2D(surfaceGaspClip, volume))
                return;

            float fatigueCadenceCooldown = math.lerp(
                underwaterSomaticFatigueBreathCooldown,
                underwaterSomaticFatigueBreathCooldown * 0.58f,
                math.saturate(fatigue01));
            _underwaterSomaticFatigueBreathCooldownTimer = math.max(0.2f, fatigueCadenceCooldown);
        }

        private bool ResolveVrComfortModeEnabled(SettingsManager settings)
        {
            bool requested = settings != null ? settings.VrComfortModeEnabled : vrComfortModeDefaultEnabled;
            if (!requested)
                return false;

            return vrComfortAllowDesktopPreview || IsVrRuntimeActive();
        }

        private static bool IsVrRuntimeActive()
        {
            return HectonXRRuntimeState.IsXRActive;
        }

        private void RefreshVrComfortSettingsCache()
        {
            SettingsManager settings = _settingsRuntime;
            _vrComfortActiveCached = ResolveVrComfortModeEnabled(settings);
            _vrSnapTurnEnabledCached = settings != null ? settings.VrSnapTurnEnabled : vrSnapTurnDefaultEnabled;
            _vrHorizonLockEnabledCached = settings != null ? settings.VrHorizonLockEnabled : vrHorizonLockDefaultEnabled;
            _vrComfortVignetteEnabledCached = settings == null || settings.VrComfortVignetteEnabled;
            _vrHeadRelativeSwimBiasCached = math.saturate(settings != null ? settings.VrHeadRelativeSwimBias : vrHeadRelativeSwimBiasDefault);
            if (!_vrComfortActiveCached)
                _vrSnapTurnArmed = true;
        }

        private float ResolveVrSwimmingReferenceYawDegrees()
        {
            if (!_vrComfortActiveCached)
                return _bodyYaw;

            return math.lerp(_bodyYaw, _cameraYaw, _vrHeadRelativeSwimBiasCached);
        }

        private bool ShouldVrHorizonLockRoll()
        {
            if (!_vrComfortActiveCached || !_vrHorizonLockEnabledCached || _isWalking)
                return false;

            bool manualRollIntent = math.abs(_inputH) >= vrManualRollInputThreshold && math.abs(_inputV) <= 0.25f;
            return !manualRollIntent;
        }

        private float ResolveVrHorizonRoll(float rawRollDegrees, bool horizonLockActive, float deltaTime)
        {
            if (!horizonLockActive)
            {
                _vrHorizonRollDampedDegrees = rawRollDegrees;
                _vrHorizonRollDampingInitialized = true;
                return rawRollDegrees;
            }

            if (!_vrHorizonRollDampingInitialized)
            {
                _vrHorizonRollDampedDegrees = rawRollDegrees;
                _vrHorizonRollDampingInitialized = true;
            }

            _vrHorizonRollDampedDegrees = NlerpRollDegrees(
                _vrHorizonRollDampedDegrees,
                0f,
                math.max(0f, deltaTime),
                VrHorizonLockReturnSeconds);
            return _vrHorizonRollDampedDegrees;
        }

        private static float NlerpRollDegrees(float currentDegrees, float targetDegrees, float deltaTime, float duration)
        {
            float t = math.saturate(deltaTime / math.max(0.0001f, duration));
            float delta = targetDegrees - currentDegrees;
            if (delta > 180f)
                delta -= 360f;
            else if (delta < -180f)
                delta += 360f;

            return currentDegrees + delta * t;
        }

        private void RegisterVrComfortVisualBounce(float intensity01)
        {
            float clamped = math.saturate(intensity01);
            _vrComfortVisualBounce01 = math.max(_vrComfortVisualBounce01, clamped);
            _vrComfortKickSignal01 = math.max(_vrComfortKickSignal01, clamped * 0.65f);
        }

        private void UpdateVrComfortSignals(float deltaTime, Vector3 comfortVelocity, float yawDeltaDegrees)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0.0001f, deltaTime) : 0.0001f;
            float invSafeDeltaTime = math.rcp(safeDeltaTime);
            float frameRateVignette01 = IsVrRuntimeActive()
                ? ResolveVrComfortFrameRateVignette01(safeDeltaTime)
                : 0f;
            bool frameRateSafetyActive = frameRateVignette01 > 0f;
            bool comfortModeActive = _vrComfortActiveCached;
            bool active = comfortModeActive || frameRateSafetyActive;
            Vector3 safeComfortVelocity = IsFiniteVector(comfortVelocity) ? comfortVelocity : Vector3.zero;
            float safeYawDeltaDegrees = math.isfinite(yawDeltaDegrees) ? yawDeltaDegrees : 0f;
            float settleT = ResolveLinearBlendT(math.max(0.5f, vrComfortVisualDecaySharpness), safeDeltaTime);

            if (!active)
            {
                UpdateInactiveVrComfortSignals(settleT);
                return;
            }

            UpdateActiveVrComfortSignals(
                safeDeltaTime,
                invSafeDeltaTime,
                comfortModeActive,
                safeComfortVelocity,
                safeYawDeltaDegrees,
                settleT,
                frameRateVignette01);
        }

        private void UpdateInactiveVrComfortSignals(float settleT)
        {
            _vrSnapTurnFadeTimer = 0f;
            _vrComfortVignette01 = math.lerp(_vrComfortVignette01, 0f, settleT);
            _vrComfortVisualBounce01 = math.lerp(_vrComfortVisualBounce01, 0f, settleT);
            _vrComfortPeripheralBlur01 = math.lerp(_vrComfortPeripheralBlur01, 0f, settleT);
            _vrComfortKickSignal01 = math.lerp(_vrComfortKickSignal01, 0f, settleT);
            _vrComfortVelocitySq01 = math.lerp(_vrComfortVelocitySq01, 0f, settleT);
            _vrComfortSway.x = math.lerp(_vrComfortSway.x, 0f, settleT);
            _vrComfortSway.y = math.lerp(_vrComfortSway.y, 0f, settleT);
            _vrComfortMotionVector.x = math.lerp(_vrComfortMotionVector.x, 0f, settleT);
            _vrComfortMotionVector.y = math.lerp(_vrComfortMotionVector.y, 0f, settleT);
            ApplyVrComfortShaderSignals(
                false,
                _vrComfortVignette01,
                _vrComfortVisualBounce01,
                _vrComfortPeripheralBlur01,
                _vrComfortSway,
                _vrComfortVelocitySq01,
                _vrComfortMotionVector);
        }

        private void UpdateActiveVrComfortSignals(
            float safeDeltaTime,
            float invSafeDeltaTime,
            bool comfortModeActive,
            Vector3 safeComfortVelocity,
            float safeYawDeltaDegrees,
            float settleT,
            float frameRateVignette01)
        {
            float yawRate = comfortModeActive ? math.abs(safeYawDeltaDegrees) * invSafeDeltaTime : 0f;
            float yawRate01 = math.saturate(yawRate * math.rcp(math.max(1f, vrComfortYawRateReference)));
            float speedReference = math.max(0.25f, vrComfortHighSpeedMetersPerSecond);
            float invSpeedReferenceSq = math.rcp(speedReference * speedReference);
            float velocitySq =
                comfortModeActive
                    ? safeComfortVelocity.x * safeComfortVelocity.x +
                      safeComfortVelocity.y * safeComfortVelocity.y +
                      safeComfortVelocity.z * safeComfortVelocity.z
                    : 0f;
            float velocitySq01 = math.saturate(velocitySq * invSpeedReferenceSq);
            float thrusterIntent = comfortModeActive
                ? math.saturate(math.max(math.abs(_inputVertical), ResolveActiveTransportBoost01()))
                : 0f;
            float targetBlur = velocitySq01 * thrusterIntent;
            float motionVignette01 = comfortModeActive
                ? math.max(_vrComfortKickSignal01, math.max(velocitySq01, math.max(yawRate01, targetBlur * 0.85f)))
                : 0f;
            float targetVignette = _vrComfortVignetteEnabledCached
                ? math.max(frameRateVignette01, motionVignette01)
                : frameRateVignette01;
            float snapFade01 = 0f;
            if (!comfortModeActive)
            {
                _vrSnapTurnFadeTimer = 0f;
            }
            else if (_vrSnapTurnFadeTimer > 0f)
            {
                float snapFadeSeconds = math.max(0.01f, vrSnapTurnFadeSeconds);
                snapFade01 = math.saturate(_vrSnapTurnFadeTimer / snapFadeSeconds);
                _vrSnapTurnFadeTimer = math.max(0f, _vrSnapTurnFadeTimer - safeDeltaTime);
            }

            float vignetteT = ResolveLinearBlendT(math.max(1f, vrComfortVignetteSharpness), safeDeltaTime);
            _vrComfortVignette01 = math.lerp(_vrComfortVignette01, targetVignette, vignetteT);
            _vrComfortPeripheralBlur01 = math.lerp(_vrComfortPeripheralBlur01, targetBlur, vignetteT);
            _vrComfortVisualBounce01 = math.max(math.lerp(_vrComfortVisualBounce01, 0f, settleT), snapFade01);
            _vrComfortKickSignal01 = math.lerp(_vrComfortKickSignal01, 0f, settleT);
            _vrComfortVelocitySq01 = math.lerp(_vrComfortVelocitySq01, velocitySq01, vignetteT);

            CalculateVrComfortSwayAndMotion(
                velocitySq,
                safeComfortVelocity,
                out float targetSwayX,
                out float targetSwayY,
                out float targetMotionX,
                out float targetMotionY);

            _vrComfortSway.x = math.lerp(_vrComfortSway.x, targetSwayX, vignetteT);
            _vrComfortSway.y = math.lerp(_vrComfortSway.y, targetSwayY, vignetteT);
            _vrComfortMotionVector.x = math.lerp(_vrComfortMotionVector.x, targetMotionX, vignetteT);
            _vrComfortMotionVector.y = math.lerp(_vrComfortMotionVector.y, targetMotionY, vignetteT);

            ApplyVrComfortShaderSignals(
                true,
                _vrComfortVignette01,
                _vrComfortVisualBounce01,
                _vrComfortPeripheralBlur01,
                _vrComfortSway,
                _vrComfortVelocitySq01,
                _vrComfortMotionVector);
        }

        private void CalculateVrComfortSwayAndMotion(
            float velocitySq,
            Vector3 safeComfortVelocity,
            out float targetSwayX,
            out float targetSwayY,
            out float targetMotionX,
            out float targetMotionY)
        {
            targetSwayX = 0f;
            targetSwayY = 0f;
            targetMotionX = 0f;
            targetMotionY = 0f;

            if (velocitySq > 0.0001f)
            {
                Vector3 localVelocity = _cachedTransform != null
                    ? _cachedTransform.InverseTransformDirection(safeComfortVelocity)
                    : safeComfortVelocity;
                float invSpeed = math.rsqrt(math.max(velocitySq, 0.000001f));
                targetSwayX = -math.clamp(localVelocity.x * invSpeed, -1f, 1f) * 0.35f;
                targetSwayY = -math.clamp(localVelocity.y * invSpeed, -1f, 1f) * 0.22f;
                targetMotionX = math.clamp(localVelocity.x * invSpeed, -1f, 1f);
                targetMotionY = math.clamp(localVelocity.y * invSpeed, -1f, 1f);
            }
        }

        private void InvalidateVrComfortShaderPublishCache()
        {
            _lastPublishedVrComfortSignals = Vector4.positiveInfinity;
            _lastPublishedVrComfortSway = Vector4.positiveInfinity;
            _lastPublishedVrComfortMotion = Vector4.positiveInfinity;
            _lastPublishedVrComfortVignette01 = float.PositiveInfinity;
        }

        private void ApplyVrComfortShaderSignals(
            bool active,
            float vignette01,
            float bounce01,
            float peripheralBlur01,
            Vector2 sway,
            float velocitySq01,
            Vector2 motionVector)
        {
            Vector4 signals = new Vector4(
                SanitizeVrComfort01(vignette01),
                SanitizeVrComfort01(bounce01),
                SanitizeVrComfort01(peripheralBlur01),
                active ? 1f : 0f);
            Vector4 swaySignal = new Vector4(
                SanitizeVrComfortSigned(sway.x),
                SanitizeVrComfortSigned(sway.y),
                SanitizeVrComfort01(velocitySq01),
                0f);
            Vector4 motionSignal = new Vector4(
                SanitizeVrComfortSigned(motionVector.x),
                SanitizeVrComfortSigned(motionVector.y),
                SanitizeVrComfort01(velocitySq01),
                active ? 1f : 0f);

            float scalarVignette01 = active ? signals.x : 0f;
            _pendingVrComfortSignals = signals;
            _pendingVrComfortSwaySignal = swaySignal;
            _pendingVrComfortMotionSignal = motionSignal;
            _pendingVrComfortVignette01 = scalarVignette01;
            _pendingVrComfortShaderDirty = true;
            MarkLateFramePresentationDirty();

            PublishVrComfortMaxVignetteTelemetry(scalarVignette01);
        }

        private void FlushVrComfortShaderSignals()
        {
            if (!_pendingVrComfortShaderDirty)
                return;

            _pendingVrComfortShaderDirty = false;
            Vector4 signals = _pendingVrComfortSignals;
            Vector4 swaySignal = _pendingVrComfortSwaySignal;
            Vector4 motionSignal = _pendingVrComfortMotionSignal;

            if (!ApproximatelyVrComfortShaderVector(signals, _lastPublishedVrComfortSignals))
            {
                Shader.SetGlobalVector(VrComfortSignalsId, signals);
                _lastPublishedVrComfortSignals = signals;
            }

            if (!ApproximatelyVrComfortShaderVector(swaySignal, _lastPublishedVrComfortSway))
            {
                Shader.SetGlobalVector(VrComfortSwayId, swaySignal);
                _lastPublishedVrComfortSway = swaySignal;
            }

            if (!ApproximatelyVrComfortShaderVector(motionSignal, _lastPublishedVrComfortMotion))
            {
                Shader.SetGlobalVector(VrComfortMotionId, motionSignal);
                _lastPublishedVrComfortMotion = motionSignal;
            }

            float scalarVignette01 = _pendingVrComfortVignette01;
            if (math.abs(scalarVignette01 - _lastPublishedVrComfortVignette01) > VrComfortShaderPublishEpsilon)
            {
                Shader.SetGlobalFloat(VrComfortVignette01Id, scalarVignette01);
                _lastPublishedVrComfortVignette01 = scalarVignette01;
            }
        }

        private void PublishVrComfortMaxVignetteTelemetry(float vignette01)
        {
            float sanitized = SanitizeVrComfort01(vignette01);
            if (sanitized <= _maxVrComfortVignetteTelemetry01)
                return;

            _maxVrComfortVignetteTelemetry01 = sanitized;
            if (_maxVrComfortVignetteTelemetry01 - _lastVrComfortVignetteTelemetry01 < VrComfortTelemetryStep01)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                VrComfortMaxVignetteHash,
                VrComfortTelemetryContextHash,
                _maxVrComfortVignetteTelemetry01);
            _lastVrComfortVignetteTelemetry01 = _maxVrComfortVignetteTelemetry01;
        }

        private static float ResolveVrComfortFrameRateVignette01(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            float targetFrameSeconds = math.rcp(VrComfortMinimumFrameRateHz);
            float overBudgetSeconds = deltaTime - targetFrameSeconds;
            if (overBudgetSeconds <= 0f)
                return 0f;

            return math.saturate(overBudgetSeconds * math.rcp(targetFrameSeconds)) * 0.35f;
        }

        private static bool ApproximatelyVrComfortShaderVector(Vector4 left, Vector4 right)
        {
            return math.abs(left.x - right.x) <= VrComfortShaderPublishEpsilon &&
                   math.abs(left.y - right.y) <= VrComfortShaderPublishEpsilon &&
                   math.abs(left.z - right.z) <= VrComfortShaderPublishEpsilon &&
                   math.abs(left.w - right.w) <= VrComfortShaderPublishEpsilon;
        }

        private static float SanitizeVrComfort01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeVrComfortSigned(float value)
        {
            return math.isfinite(value) ? math.clamp(value, -1f, 1f) : 0f;
        }

        private void ApplyCameraState()
        {
            if (_cameraRig == null)
                return;

            float sargassumPitchOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraPitchOffset : 0f;
            float sargassumRollOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraRollOffset : 0f;
            Vector3 sargassumLocalOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraLocalOffset : Vector3.zero;
            bool vrComfortActive = _vrComfortActiveCached;
            float somaticPitchOffset = vrComfortActive ? 0f : _underwaterSomaticPitchOffset;
            float somaticYawOffset = vrComfortActive ? 0f : _underwaterSomaticYawOffset;
            float finalPitch = _cameraPitch + somaticPitchOffset + _juiceOutput.pitchOffset + sargassumPitchOffset + _heavyTowCameraPitchOffset;
            finalPitch = math.clamp(finalPitch, pitchMin - 5f, pitchMax + 5f);
            float finalYaw = CameraYaw + somaticYawOffset;
            float finalRoll = _juiceOutput.rollOffset + sargassumRollOffset + _heavyTowCameraRollOffset + _kinematicInertiaCameraRollOffset;
            bool horizonLockActive = ShouldVrHorizonLockRoll();
            finalRoll = ResolveVrHorizonRoll(finalRoll, horizonLockActive, _currentRenderDeltaTime);

            if (_activeTransportPlatform != null && _activeTransportPlatform.InheritPlatformRotation && TryGetActiveTransportPlatformTransform(out _))
            {
                Quaternion platformBasisRotation = ResolveTransportPlatformBasisRotation();
                float platformLocalYaw = ResolveYawRelativeToTransportPlatform(finalYaw);
                if (horizonLockActive)
                    platformBasisRotation = ResolveVrHorizonLockedTransportBasis(platformBasisRotation);

                _cameraWorldRotation = platformBasisRotation * ComposeAxisAngleDegrees(finalPitch, platformLocalYaw, finalRoll);
            }
            else
            {
                _cameraWorldRotation = ComposeAxisAngleDegrees(finalPitch, finalYaw, finalRoll);
            }

            Vector3 finalPos;
            float localMotionScale = vrComfortActive ? 1f - math.saturate(vrCameraLocalMotionSuppression) : 1f;
            finalPos.x = _cameraBaseLocalPos.x + (_juiceOutput.localPositionOffset.x + sargassumLocalOffset.x + _heavyTowCameraLocalOffset.x) * localMotionScale;
            finalPos.y = vrComfortActive
                ? _cameraBaseLocalPos.y
                : _cameraBaseLocalPos.y + (_juiceOutput.localPositionOffset.y + sargassumLocalOffset.y + _heavyTowCameraLocalOffset.y) * localMotionScale;
            finalPos.z = _cameraBaseLocalPos.z + (_juiceOutput.localPositionOffset.z + sargassumLocalOffset.z + _heavyTowCameraLocalOffset.z) * localMotionScale;

            float targetFov = vrComfortActive ? baseFov : baseFov + _juiceOutput.fovOffset;
            float pressureSqueeze01 = ResolveFatalPressureSqueeze01();
            if (!vrComfortActive && pressureSqueeze01 > 0f)
                targetFov = math.lerp(targetFov, fatalPressureMinFov, pressureSqueeze01);

            ApplyCinematicFocusCameraBias(ref _cameraWorldRotation, ref targetFov, vrComfortActive, _currentRenderDeltaTime);

            HectonCameraState cameraState = new HectonCameraState
            {
                TargetRotation = _cameraWorldRotation,
                TargetLocalPosition = finalPos,
                TargetFieldOfView = targetFov,
                PreviousFixedPosition = _previousRenderInterpolationState.BodyPosition,
                CurrentFixedPosition = _currentRenderInterpolationState.BodyPosition,
                KccLinearVelocity = _renderInterpolatedLinearVelocity,
                FixedDeltaTime = _currentFixedDeltaTime,
                DeltaTime = _juiceInput.deltaTime,
                Flags = vrComfortActive ? HectonCameraState.ApplyTransformDirectlyFlag : 0u,
                RotationSharpness = 30f,
                PositionSharpness = 30f,
                FieldOfViewSharpness = 8f
            };

            _cameraRig.SetAupAnchor(ResolveVrAupAnchor());
            _cameraRig.SetLocomotionState(cameraState);
        }

        private void ApplyCinematicFocusCameraBias(
            ref Quaternion targetRotation,
            ref float targetFov,
            bool vrComfortActive,
            float deltaTime)
        {
            if (!_cinematicFocusActive || !cinematicFocusEnabled)
                return;

            if (!TryResolveCinematicFocusDirection(out Vector3 targetDirection, out float distanceSq, out AbsoluteUniversePosition playerAup))
            {
                DumpCinematicFocusBlackBox(_cinematicFocusDumpHash);
                ClearCinematicFocus(true);
                return;
            }

            float pullWeight = _cinematicFocusIntensity01 * (1f - math.saturate(_cinematicFocusPullSuppression01));
            pullWeight = math.saturate(pullWeight);
            _cinematicFocusLastDistanceSq = distanceSq;
            _cinematicFocusLastSubtitleAlpha01 = ResolveCinematicSubtitleAlpha01(distanceSq);

            if (!vrComfortActive && pullWeight > 0f)
            {
                float blend = math.saturate(math.max(0f, cinematicFocusPullStrength) * math.max(0f, deltaTime) * pullWeight);
                targetRotation = CinematicMath.FastNlerp(targetRotation, targetDirection, blend, Vector3.up);

                float fovQualityWeight01 = ResolveCinematicFocusFovWeight01();
                if (fovQualityWeight01 > 0f)
                {
                    float desiredFov = math.lerp(targetFov, math.min(targetFov, cinematicFocusFov), fovQualityWeight01);
                    float fovT = ResolveLinearBlendT(math.max(0.01f, cinematicFocusFovSharpness) * pullWeight * fovQualityWeight01, deltaTime);
                    targetFov = math.lerp(targetFov, desiredFov, fovT);
                }
            }

            WriteCinematicFocusBlackBoxSample(in playerAup, targetDirection, distanceSq, pullWeight);
        }

        private bool TryResolveCinematicFocusDirection(
            out Vector3 targetDirection,
            out float distanceSq,
            out AbsoluteUniversePosition playerAup)
        {
            playerAup = _playerState.AbsolutePosition;
            double3 player = playerAup.ToAbsoluteDouble3();
            double3 target = _cinematicFocusTargetAup.ToAbsoluteDouble3();
            double3 delta = target - player;
            double distanceSqDouble = math.lengthsq(delta);
            if (!double.IsFinite(distanceSqDouble) || distanceSqDouble <= 0.000001d)
            {
                targetDirection = Vector3.forward;
                distanceSq = 0f;
                return false;
            }

            double invDistance = math.rsqrt(math.max(distanceSqDouble, 0.0001d));
            double3 normalized = delta * invDistance;
            if (!math.all(math.isfinite(normalized)))
            {
                targetDirection = Vector3.forward;
                distanceSq = 0f;
                return false;
            }

            targetDirection = new Vector3((float)normalized.x, (float)normalized.y, (float)normalized.z);
            distanceSq = distanceSqDouble > float.MaxValue ? float.MaxValue : (float)distanceSqDouble;
            return true;
        }

        private float ResolveCinematicFocusFovWeight01()
        {
            if ((_cinematicFocusFlags & NarrativeFocusSignal.FlagDisableFovNarrowing) != 0)
                return 0f;

            _cinematicFocusFovQualityWeight01 = SmoothQuality01(ResolveMovementQualityWeight01());
            return _cinematicFocusFovQualityWeight01;
        }

        private float ResolveCinematicSubtitleAlpha01(float distanceSq)
        {
            float fadeSq = math.max(0.01f, _cinematicFocusSubtitleFadeDistanceSq);
            return math.saturate(1f - (math.max(0f, distanceSq) * math.rcp(fadeSq)));
        }

        private void WriteCinematicFocusBlackBoxSample(
            in AbsoluteUniversePosition playerAup,
            Vector3 targetDirection,
            float distanceSq,
            float pullWeight)
        {
            NativeArray<CinematicFocusTelemetryEntry> blackBox = ResolveCinematicFocusBlackBox();
            if (!blackBox.IsCreated)
                return;

            blackBox[_cinematicFocusBlackBoxCursor] = new CinematicFocusTelemetryEntry
            {
                Frame = SystemDispatcher.CurrentFrameId,
                FocusHash = _cinematicFocusHash,
                PlayerGridX = playerAup.GridX,
                PlayerGridY = playerAup.GridY,
                PlayerGridZ = playerAup.GridZ,
                TargetGridX = _cinematicFocusTargetAup.GridX,
                TargetGridY = _cinematicFocusTargetAup.GridY,
                TargetGridZ = _cinematicFocusTargetAup.GridZ,
                TargetDirection = new float3(targetDirection.x, targetDirection.y, targetDirection.z),
                DistanceSq = distanceSq,
                PullWeight = pullWeight,
                SubtitleAlpha01 = _cinematicFocusLastSubtitleAlpha01,
                Flags = _cinematicFocusFlags
            };

            _cinematicFocusBlackBoxCursor++;
            if (_cinematicFocusBlackBoxCursor >= CinematicFocusBlackBoxCapacity)
                _cinematicFocusBlackBoxCursor = 0;
            if (_cinematicFocusBlackBoxCount < CinematicFocusBlackBoxCapacity)
                _cinematicFocusBlackBoxCount++;
        }

        private void DumpCinematicFocusBlackBox(uint reasonHash)
        {
            NativeArray<CinematicFocusTelemetryEntry> blackBox = ResolveCinematicFocusBlackBox();
            if (!blackBox.IsCreated)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (frame - _cinematicFocusLastDumpFrame < CinematicFocusBlackBoxDumpCooldownFrames)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(_cinematicFocusFaultHash, reasonHash, _cinematicFocusHash);
            const int HeaderBytes = 16;
            const int RowBytes = 81;
            int totalBytes = HeaderBytes + _cinematicFocusBlackBoxCount * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(HectonPlayerMovement),
                "CinematicFocusBlackBoxDumpPayload");
            try
            {
                WriteInt32LittleEndian(payload, 0, CinematicFocusBlackBoxCapacity);
                WriteInt32LittleEndian(payload, 4, _cinematicFocusBlackBoxCursor);
                WriteUInt32LittleEndian(payload, 8, reasonHash);
                WriteInt32LittleEndian(payload, 12, _cinematicFocusBlackBoxCount);
                int startIndex = _cinematicFocusBlackBoxCount >= CinematicFocusBlackBoxCapacity
                    ? _cinematicFocusBlackBoxCursor
                    : 0;
                for (int i = 0; i < _cinematicFocusBlackBoxCount; i++)
                {
                    int entryIndex = startIndex + i;
                    if (entryIndex >= CinematicFocusBlackBoxCapacity)
                        entryIndex -= CinematicFocusBlackBoxCapacity;

                    CinematicFocusTelemetryEntry entry = blackBox[entryIndex];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteUInt32LittleEndian(payload, offset, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 4, entry.FocusHash);
                    WriteInt64LittleEndian(payload, offset + 8, entry.PlayerGridX);
                    WriteInt64LittleEndian(payload, offset + 16, entry.PlayerGridY);
                    WriteInt64LittleEndian(payload, offset + 24, entry.PlayerGridZ);
                    WriteInt64LittleEndian(payload, offset + 32, entry.TargetGridX);
                    WriteInt64LittleEndian(payload, offset + 40, entry.TargetGridY);
                    WriteInt64LittleEndian(payload, offset + 48, entry.TargetGridZ);
                    WriteFloat3LittleEndian(payload, offset + 56, entry.TargetDirection);
                    WriteFloat32LittleEndian(payload, offset + 68, entry.DistanceSq);
                    WriteFloat32LittleEndian(payload, offset + 72, entry.PullWeight);
                    WriteFloat32LittleEndian(payload, offset + 76, entry.SubtitleAlpha01);
                    payload[offset + 80] = entry.Flags;
                }

                if (NativeFaultDumpWriter.TryWriteAll(
                        "Docs/AgentLogs/Dump_CINEMATIC_FRAMER.bin",
                        payload,
                        totalBytes))
                {
                    _cinematicFocusLastDumpFrame = frame;
                }
            }
            catch (System.Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[CinematicFocus] Failed to dump blackbox.");
#endif
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonPlayerMovement),
                    "CinematicFocusBlackBoxDumpPayload");
            }
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> payload, int offset, float3 value)
        {
            WriteFloat32LittleEndian(payload, offset, value.x);
            WriteFloat32LittleEndian(payload, offset + 4, value.y);
            WriteFloat32LittleEndian(payload, offset + 8, value.z);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> payload, int offset, long value)
        {
            ulong unsigned = unchecked((ulong)value);
            payload[offset] = (byte)unsigned;
            payload[offset + 1] = (byte)(unsigned >> 8);
            payload[offset + 2] = (byte)(unsigned >> 16);
            payload[offset + 3] = (byte)(unsigned >> 24);
            payload[offset + 4] = (byte)(unsigned >> 32);
            payload[offset + 5] = (byte)(unsigned >> 40);
            payload[offset + 6] = (byte)(unsigned >> 48);
            payload[offset + 7] = (byte)(unsigned >> 56);
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private void PublishCinematicMixerState(float intensity01)
        {
            float safeIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            MixerStateSignal signal = new MixerStateSignal
            {
                MixerStateHash = MixerStateSignal.FocusStateHash,
                SourceHash = _cinematicFocusHash,
                Intensity01 = safeIntensity,
                DuckingDb = safeIntensity > 0f ? CinematicFocusAmbientDuckingDb : 0f,
                Frame = SystemDispatcher.CurrentFrameId,
                Flags = _cinematicFocusFlags
            };
            SignalBus<MixerStateSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            _cinematicFocusAudioDucked = safeIntensity > 0f;
        }

        private static Quaternion ResolveVrHorizonLockedTransportBasis(Quaternion platformBasisRotation)
        {
            Vector3 platformForward = platformBasisRotation * Vector3.forward;
            Vector3 yawOnlyForward = ProjectOnPlaneFast(platformForward, Vector3.up);
            if (yawOnlyForward.sqrMagnitude <= 0.0001f)
                yawOnlyForward = ProjectOnPlaneFast(platformBasisRotation * Vector3.right, Vector3.up);
            if (yawOnlyForward.sqrMagnitude <= 0.0001f)
                return Quaternion.identity;

            yawOnlyForward = NormalizeVectorRsqrt(yawOnlyForward, Vector3.forward);
            return Quaternion.LookRotation(yawOnlyForward, Vector3.up);
        }

        private Transform ResolveVrAupAnchor()
        {
            if (!_vrComfortActiveCached || _activeTransportPlatform == null || !_activeTransportPlatform.IsTransportPlatformActive)
                return null;

            if (_activeTransportPlatform is ISubmarineRuntimeContext)
                return _activeTransportPlatform.PlatformTransform;

            return null;
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        private void RefreshActiveGravity(float fixedDeltaTime)
        {
            if (_localGravityOverrideActive && _localGravityOverrideTimer > 0f)
            {
                _localGravityOverrideTimer = math.max(0f, _localGravityOverrideTimer - math.max(0f, fixedDeltaTime));
                _cachedGravity = ResolveBlendedLocalGravity(fixedDeltaTime);
                if (_localGravityOverrideTimer <= 0f)
                    _localGravityOverrideActive = false;
            }
            else
            {
                _cachedGravity = ResolveAupCenterGravity(UnityEngine.Physics.gravity);
                _localGravityOverrideActive = false;
                _localGravityOverrideBlendTimer = 0f;
                _localGravityOverrideBlendStart = _cachedGravity;
            }

            _cachedGravityMagnitude = MagnitudeFromRsqrt(_cachedGravity);
            if (_cachedGravityMagnitude <= 0.0001f)
            {
                _cachedGravity = ResolveAupCenterGravity(UnityEngine.Physics.gravity);
                _cachedGravityMagnitude = MagnitudeFromRsqrt(_cachedGravity);
            }

            if (_localGravityOverrideActive && _cachedGravityMagnitude > 0.0001f)
                _smoothedGroundNormal = -_cachedGravity / _cachedGravityMagnitude;
        }

        private Vector3 ResolveAupCenterGravity(Vector3 fallbackGravity)
        {
            Vector3 safeFallback = HectonPlayerMotor.SafeVelocity(fallbackGravity);
            float gravityMagnitude = MagnitudeFromRsqrt(safeFallback);
            if (gravityMagnitude <= 0.0001f)
                gravityMagnitude = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;

            double3 absolutePosition = _playerState.PredictedAbsolutePosition.ToAbsoluteDouble3();
            double3 toAupCenter = -absolutePosition;
            double distanceSq = math.lengthsq(toAupCenter);
            if (!math.isfinite(distanceSq) || distanceSq <= 0.0001d)
                return safeFallback.sqrMagnitude > MinLocalGravitySqr ? safeFallback : Vector3.down * gravityMagnitude;

            double inverseDistance = math.rsqrt(math.max(distanceSq, 0.0001d));
            double3 gravityDirection = toAupCenter * inverseDistance;
            if (!math.all(math.isfinite(gravityDirection)))
                return safeFallback.sqrMagnitude > MinLocalGravitySqr ? safeFallback : Vector3.down * gravityMagnitude;

            Vector3 radialGravity = new Vector3(
                (float)(gravityDirection.x * gravityMagnitude),
                (float)(gravityDirection.y * gravityMagnitude),
                (float)(gravityDirection.z * gravityMagnitude));
            return IsFiniteVector(radialGravity) && radialGravity.sqrMagnitude > MinLocalGravitySqr
                ? radialGravity
                : (safeFallback.sqrMagnitude > MinLocalGravitySqr ? safeFallback : Vector3.down * gravityMagnitude);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  FixedTick Ã¢â‚¬â€ PHYSICS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float ResolveVrComfortGravityScale(float targetGravityScale, float fixedDeltaTime)
        {
            if (!_vrComfortActiveCached)
            {
                _vrComfortGravityScaleInitialized = false;
                return targetGravityScale;
            }

            if (!_vrComfortGravityScaleInitialized)
            {
                _vrComfortGravityScaleCurrent = targetGravityScale;
                _vrComfortGravityScaleStart = targetGravityScale;
                _vrComfortGravityScaleTarget = targetGravityScale;
                _vrComfortGravityScaleTimer = VrComfortGravityTransitionSeconds;
                _vrComfortGravityScaleInitialized = true;
                return targetGravityScale;
            }

            if (math.abs(targetGravityScale - _vrComfortGravityScaleTarget) > VrComfortGravityTransitionTargetEpsilon)
            {
                _vrComfortGravityScaleStart = _vrComfortGravityScaleCurrent;
                _vrComfortGravityScaleTarget = targetGravityScale;
                _vrComfortGravityScaleTimer = 0f;
            }

            _vrComfortGravityScaleTimer = math.min(
                VrComfortGravityTransitionSeconds,
                _vrComfortGravityScaleTimer + math.max(0f, fixedDeltaTime));
            float t = math.smoothstep(
                0f,
                1f,
                _vrComfortGravityScaleTimer / math.max(0.0001f, VrComfortGravityTransitionSeconds));
            _vrComfortGravityScaleCurrent = math.lerp(_vrComfortGravityScaleStart, _vrComfortGravityScaleTarget, t);
            return _vrComfortGravityScaleCurrent;
        }

        private Vector3 ResolveBlendedLocalGravity(float fixedDeltaTime)
        {
            Vector3 targetGravity = HectonPlayerMotor.SafeVelocity(_localGravityOverride);
            if (targetGravity.sqrMagnitude <= MinLocalGravitySqr)
                return ResolveCurrentGravityForOverrideBlend();

            Vector3 startGravity = HectonPlayerMotor.SafeVelocity(_localGravityOverrideBlendStart);
            if (startGravity.sqrMagnitude <= MinLocalGravitySqr)
                startGravity = targetGravity;

            _localGravityOverrideBlendTimer = math.min(
                LocalGravityOverrideBlendSeconds,
                _localGravityOverrideBlendTimer + math.max(0f, fixedDeltaTime));

            float blend01 = math.saturate(_localGravityOverrideBlendTimer / LocalGravityOverrideBlendSeconds);
            if (blend01 >= 1f)
                return targetGravity;

            return BlendGravityVectorCheap(startGravity, targetGravity, blend01);
        }

        private Vector3 ResolveCurrentGravityForOverrideBlend()
        {
            Vector3 currentGravity = HectonPlayerMotor.SafeVelocity(_cachedGravity);
            if (currentGravity.sqrMagnitude > MinLocalGravitySqr)
                return currentGravity;

            currentGravity = ResolveAupCenterGravity(UnityEngine.Physics.gravity);
            if (currentGravity.sqrMagnitude > MinLocalGravitySqr)
                return currentGravity;

            return Vector3.down * HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        }

        private static Vector3 BlendGravityVectorCheap(Vector3 startGravity, Vector3 targetGravity, float blend01)
        {
            float t = math.saturate(blend01);
            Vector3 blended = startGravity + ((targetGravity - startGravity) * t);
            if (blended.sqrMagnitude > MinLocalGravitySqr)
                return blended;

            return t < 0.5f ? startGravity : targetGravity;
        }

        private Vector3 ResolveBodyRuntimePosition()
        {
            if (_useFixedFrameSpatialCache && MathGuard.IsFinite(_fixedFrameBodyPosition))
                return HectonPlayerMotor.SafeVelocity(_fixedFrameBodyPosition);

            Vector3 statePosition = ResolvePlayerAupRuntimePosition();
            if (MathGuard.IsFinite(statePosition))
                return HectonPlayerMotor.SafeVelocity(statePosition);

            return _rb != null ? HectonPlayerMotor.SafeVelocity(_rb.position) : Vector3.zero;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;
            if (_juiceProcessor == null) return;

            using (_fixedTickProfilerMarker.Auto())
            {
                PlayerTransportPreset activeTransportPreset = PrepareTransportAndFrameState(fixedDeltaTime, out float currentVerticalVelocity, out bool initialExosuitKinematicAuthority);

                float previousWaterImmersionRatio = _waterImmersionRatio;
                bool wasGroundedLastFixedTick = _isGrounded;

                _rb.useGravity = false;

                if (_isWalking)
                {
                    _bodyYaw = _cameraYaw;
                    _bodyYawVelocity = 0f;
                }
                else
                {
                    float bodyYawOmega = suit.bodyYawSpringOmega *
                        ResolveHeavyCarryBodyYawSpringMultiplier() *
                        ResolveTransportBodyYawResponsivenessScale(activeTransportPreset) *
                        ResolveHullStressTurnResponsivenessScale(activeTransportPreset);
                    _bodyYaw = SpringDampAngle(_bodyYaw, _cameraYaw, ref _bodyYawVelocity, bodyYawOmega, fixedDeltaTime);
                }

                _juiceProcessor.TrackVerticalVelocity(currentVerticalVelocity);
                _wasGroundedLastFrame = _isGrounded;

                GroundCheck();
                _playerMotor?.SetGroundedState(_isGrounded);

                UpdateWaterImmersion(fixedDeltaTime);

                ApplyLadderSplineSnapFromAsyncProbe(initialExosuitKinematicAuthority);

                float physicsImmersion = _smoothedImmersionRatio;
                float feetDepth = GetFeetDepthBelowSurface(EffectiveWaterSurfaceY);
                bool hasImmediateShoreFooting = _isGrounded && feetDepth <= shoreWalkFootDepth;
                bool isShallowEnoughForShore = physicsImmersion < swimTransitionThreshold || hasImmediateShoreFooting;
                bool isDryLand = physicsImmersion <= 0.01f;

                if (_isGrounded && isDryLand)
                {
                    _dryGroundGraceTimer = dryGroundGraceTime;
                }
                else if (_dryGroundGraceTimer > 0f)
                {
                    _dryGroundGraceTimer -= fixedDeltaTime;
                    if (_dryGroundGraceTimer < 0f)
                        _dryGroundGraceTimer = 0f;
                }

                if (_isGrounded && isShallowEnoughForShore)
                {
                    _shoreGroundGraceTimer = shoreGroundGraceTime;
                }
                else if (_shoreGroundGraceTimer > 0f)
                {
                    _shoreGroundGraceTimer -= fixedDeltaTime;
                    if (_shoreGroundGraceTimer < 0f)
                        _shoreGroundGraceTimer = 0f;
                }

                AdvanceCooldownTimers(fixedDeltaTime);
                AdvanceFatalPressureSequence(fixedDeltaTime);
                UpdateWipeoutState(fixedDeltaTime);
                UpdateShoreBuoyancyBlend(fixedDeltaTime, physicsImmersion, feetDepth);
                UpdateVegetationDensityLinearDamping(fixedDeltaTime);

                bool hasDryGroundSupport = _isGrounded || (_dryGroundGraceTimer > 0f && isDryLand);
                bool hasShoreGroundSupport = hasImmediateShoreFooting || (_shoreGroundGraceTimer > 0f && isShallowEnoughForShore);
                bool groundedOnDryLand = hasDryGroundSupport && isDryLand;
                bool groundedOnShore = hasShoreGroundSupport && isShallowEnoughForShore;

                bool exosuitActive = IsExosuitTransportActive();
                bool exosuitKinematicAuthority = TrySubmitExosuitKinematicAuthority(exosuitActive);

                ToggleBuoyancy(!exosuitActive);
                RefreshSurfaceBreachLock(physicsImmersion);
                UpdateSurfaceDiveCommitTimer(fixedDeltaTime, activeTransportPreset);

                float targetGravityScale = exosuitActive
                    ? exosuitNegativeBuoyancyScale
                    : groundedOnShore ? 1f : 1f - math.saturate(physicsImmersion * gravityFadeRate);
                _gravityScale = ResolveVrComfortGravityScale(targetGravityScale, fixedDeltaTime);

                if (!exosuitKinematicAuthority && _gravityScale > 0.001f)
                {
                    float mass = ResolveAuthoritativeBodyMassKg();
                    _forceVector.x = _cachedGravity.x * mass * _gravityScale;
                    _forceVector.y = _cachedGravity.y * mass * _gravityScale;
                    _forceVector.z = _cachedGravity.z * mass * _gravityScale;
                    QueueEnvironmentalForce(_forceVector);
                }

                if ((exosuitActive && _isGrounded) || groundedOnShore)
                {
                    _snapScale = 1f;
                }
                else
                {
                    _snapScale = 1f - math.saturate(physicsImmersion * snapFadeRate);
                }

                bool shouldWalk = ShouldUseLandLocomotion(physicsImmersion, hasShoreGroundSupport, hasImmediateShoreFooting);
                bool shouldStartSurfaceDiveAssist =
                    !shouldWalk &&
                    !IsInDryInterior() &&
                    physicsImmersion >= surfaceBreachMinImmersion &&
                    GetHeadDepthBelowSurface(EffectiveWaterSurfaceY) <= surfaceDiveBreakDepth &&
                    HasCommittedSurfaceDive(activeTransportPreset);

                if (shouldWalk)
                {
                    _surfaceDiveAssistTimer = 0f;
                }
                else if (shouldStartSurfaceDiveAssist && _surfaceDiveAssistTimer <= 0f)
                {
                    _surfaceDiveAssistTimer = surfaceDiveAssistDuration;
                }

                bool wasWalkingBeforeModeResolution = _isWalking;
                if (shouldWalk != _isWalking)
                {
                    _isWalking = shouldWalk;
                    if (_isWalking)
                        ApplyShorelineWalkHandoffVelocity(wasWalkingBeforeModeResolution, hasShoreGroundSupport, hasImmediateShoreFooting);
                    ApplyModePhysics(suit);
                    UpdateModeDiagnostics();
                }

                _isSurfaceSwimming = !exosuitActive && !_isWalking && ResolveSurfaceSwimState(physicsImmersion, activeTransportPreset);
                _currentLocomotionMode = ResolveLocomotionMode(physicsImmersion);
                SyncStateMachineContext(exosuitActive, physicsImmersion, groundedOnDryLand, groundedOnShore);
                _environmentHandler?.ExecuteStep(fixedDeltaTime, !exosuitKinematicAuthority);
                ApplyQueuedExternalKinematicForces(fixedDeltaTime, !exosuitKinematicAuthority);
                ApplyHighSpeedWipeoutSweep(fixedDeltaTime, !exosuitKinematicAuthority);
                UpdateSurfaceLockState(fixedDeltaTime);
                UpdateWaterPresentationPose(fixedDeltaTime);
                UpdateDynamicCollisionProfile(fixedDeltaTime, exosuitKinematicAuthority);
                UpdateHeavyTowRuntimeResponse(fixedDeltaTime, exosuitKinematicAuthority);
                UpdateWetLensSignal(fixedDeltaTime);
                UpdateHeadSurfaceRecovery(fixedDeltaTime);
                UpdateTransportCriticalBailout();
                UpdateModeDiagnostics();
                TryStartWaterEntryImpact(previousWaterImmersionRatio, wasGroundedLastFixedTick, currentVerticalVelocity);
                TryPlaySurfacePierceSplashAudio(previousWaterImmersionRatio, currentVerticalVelocity);
                TryStartSurfaceBreachArc(previousWaterImmersionRatio, currentVerticalVelocity);
                ApplyHydrostaticExitWeighting(previousWaterImmersionRatio);

                SmoothDampingTransition(fixedDeltaTime, suit);
                TryApplyKinematicWallKick(exosuitKinematicAuthority);

                ProcessJumpInput(fixedDeltaTime, exosuitActive, groundedOnDryLand, groundedOnShore);

                ApplyLocomotionPhysics(fixedDeltaTime, suit, activeTransportPreset, exosuitActive, exosuitKinematicAuthority, groundedOnDryLand, groundedOnShore);

                FinalizeFixedTick(fixedDeltaTime, suit, exosuitKinematicAuthority);
            }
        }

        private PlayerTransportPreset PrepareTransportAndFrameState(float fixedDeltaTime, out float currentVerticalVelocity, out bool initialExosuitKinematicAuthority)
        {
            RefreshVrComfortSettingsCache();
            AdvanceMovementProbeCacheFrame();

            if (_transportEvaLockTicks > 0)
                _transportEvaLockTicks--;

            if (_transportBailoutCooldownTimer > 0f)
            {
                _transportBailoutCooldownTimer -= fixedDeltaTime;
                if (_transportBailoutCooldownTimer < 0f)
                    _transportBailoutCooldownTimer = 0f;
            }

            _currentFixedDeltaTime = fixedDeltaTime;
            RefreshActiveGravity(fixedDeltaTime);
            Vector3 bodyRuntimePosition = ResolveBodyRuntimePosition();
            Vector3 authoritativeVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            currentVerticalVelocity = authoritativeVelocity.y;

            _playerState.SyncKinematic(bodyRuntimePosition, authoritativeVelocity);
            _lastPlayerKinematicsIntendedMovement = ResolveRawInputIntentVector();
            WritePlayerKinematicsSnapshot(bodyRuntimePosition, authoritativeVelocity, _lastPlayerKinematicsIntendedMovement);
            _useFixedFrameSpatialCache = true;

            PlayerTransportPreset activeTransportPreset = PrepareFixedTickDependencies();
            _stateMachine?.AdvanceFixed(fixedDeltaTime);

            ITransportPlatform previousTransportPlatform = _activeTransportPlatform;
            Transform previousTransportPlatformTransform = _activeTransportPlatformTransform;
            ResolveActiveTransportPlatform();
            if (_transportEvaLockTicks <= 0 &&
                previousTransportPlatform != null &&
                _activeTransportPlatform == null)
            {
                ExecuteTransportEvaHandoff(previousTransportPlatform, previousTransportPlatformTransform);
            }

            bool exosuitActive = IsExosuitTransportActive();
            initialExosuitKinematicAuthority = exosuitActive && ExosuitKinematicAuthority.HasActiveAuthority();

            SyncTransportPlatformRotation();
            RefreshFixedFrameSpatialCache();
            ApplyTransportPlatformCarrierMotion(fixedDeltaTime, initialExosuitKinematicAuthority);
            RefreshFixedFrameSpatialCache();
            RefreshSharedGroundSweepBuffer();

            return activeTransportPreset;
        }

        private void UpdateWaterImmersion(float fixedDeltaTime)
        {
            UpdateOceanWaterHeight();
            _waterImmersionRatio = ComputeImmersionRatio();
            _currentDepth = ComputeDepth();
            UpdateBottomClearance();

            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }

            UpdateBrineLayerState(fixedDeltaTime);

            if (_waterImmersionRatio > _smoothedImmersionRatio)
            {
                float enterT = ResolveLinearBlendT(12f, fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, enterT);
            }
            else
            {
                float exitT = ResolveLinearBlendT(3f, fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, exitT);
            }
        }

        private void ApplyLocomotionPhysics(
            float fixedDeltaTime,
            SuitData suit,
            PlayerTransportPreset activeTransportPreset,
            bool exosuitActive,
            bool exosuitKinematicAuthority,
            bool groundedOnDryLand,
            bool groundedOnShore)
        {
            if (exosuitKinematicAuthority)
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                AdvanceExosuitGrappleRequest(fixedDeltaTime);
                _exosuitGrappleCurrentForce = 0f;
            }
            else if (_isWalking)
            {
                bool hasLandInput = _inputH != 0f || _inputV != 0f;
                ApplyEnvironmentalDrag(1f);
                AdvanceExternalEnvironmentalDrag(fixedDeltaTime);
                AdvanceParasiteLatchInfluence(fixedDeltaTime);
                _sargassumFieldDensity01 = 0f;
                UpdateSargassumMatBuoyancyBlend(fixedDeltaTime);
                WalkPhysics(suit, fixedDeltaTime);
                ApplyCuttingTensionPhysics(fixedDeltaTime);
                ApplyExosuitGrapplePhysics(fixedDeltaTime);
                ApplyExosuitJumpJets(fixedDeltaTime);
                if (!exosuitActive)
                    CoolExosuitJumpJets(fixedDeltaTime);

                if (hasLandInput)
                    TryApplyStepAssist(groundedOnDryLand, groundedOnShore);

                if (_isGrounded && _snapScale > 0.001f)
                    ApplyGroundStability(_snapScale);
            }
            else
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                AdvanceCuttingTensionRequest(fixedDeltaTime);
                ApplyExosuitGrapplePhysics(fixedDeltaTime);
                AdvanceCurrentPhaseTimer(fixedDeltaTime);
                AdvanceSargassumInfluence(fixedDeltaTime, activeTransportPreset);
                AdvanceAbyssalThermalInfluence(fixedDeltaTime, activeTransportPreset);
                AdvanceExternalEnvironmentalDrag(fixedDeltaTime);
                AdvanceParasiteLatchInfluence(fixedDeltaTime);
                SwimPhysics(suit, fixedDeltaTime, activeTransportPreset);

                if (_surfaceLockBlend > 0.001f)
                    ApplySurfaceLock(suit, activeTransportPreset);

                if (_waterImmersionRatio > 0.3f)
                    ApplyAmbientCurrent(suit, fixedDeltaTime, activeTransportPreset);
            }
        }

        private void FinalizeFixedTick(float fixedDeltaTime, SuitData suit, bool exosuitKinematicAuthority)
        {
            if (!exosuitKinematicAuthority)
            {
                TryProcessKccWallScrapeFeedback();
                ApplyWipeoutRecoveryForces(fixedDeltaTime);
                ApplyProceduralLinearDamping(fixedDeltaTime);
                ClampVelocity(suit);
            }
            SanitizeKccFiniteState();
            Vector3 safeVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 bodyRuntimePosition = ResolveBodyRuntimePosition();
            WritePlayerKinematicsSnapshot(bodyRuntimePosition, safeVelocity, _lastPlayerKinematicsIntendedMovement);
            _playerKinematicsNativeState.WriteTelemetry(
                _dataVault,
                _lastPlayerKinematicsDragCoefficient,
                _lastPlayerKinematicsWaterDensityScale,
                ResolvePlayerKinematicsTelemetryFlags());
            PublishMovementAcousticSignal(safeVelocity);
            SyncSwimVatSpeedScalar(safeVelocity, suit);
            PushMovementStaminaBurnInput();
            ApplyVoxelNoClipFailsafe(exosuitKinematicAuthority);
            CaptureFixedInterpolationState();
            UIStateStore.WriteValue(UIValueSlotId.MovementSpeed, ApproximateVectorMagnitude(safeVelocity), (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds);
            UpdateGroundDiagnostics();
            _useFixedFrameSpatialCache = false;
        }

        private void AdvanceCooldownTimers(float fixedDeltaTime)
        {
            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= fixedDeltaTime;
                if (_jumpBufferTimer <= 0f)
                {
                    _jumpBufferTimer = 0f;
                    _jumpRequested = false;
                }
            }

            if (_surfaceBreachLockTimer > 0f)
            {
                _surfaceBreachLockTimer -= fixedDeltaTime;
                if (_surfaceBreachLockTimer < 0f)
                    _surfaceBreachLockTimer = 0f;
            }

            if (_wallKickCooldownTimer > 0f)
            {
                _wallKickCooldownTimer -= fixedDeltaTime;
                if (_wallKickCooldownTimer < 0f)
                    _wallKickCooldownTimer = 0f;
            }

            AdvanceSurfaceBreachArcTimers(fixedDeltaTime);

            if (_surfaceDiveAssistTimer > 0f)
            {
                _surfaceDiveAssistTimer -= fixedDeltaTime;
                if (_surfaceDiveAssistTimer < 0f)
                    _surfaceDiveAssistTimer = 0f;
            }

            if (_waterEntryImpactTimer > 0f)
            {
                _waterEntryImpactTimer -= fixedDeltaTime;
                if (_waterEntryImpactTimer <= 0f)
                {
                    _waterEntryImpactTimer = 0f;
                    _waterEntryImpactStrength = 0f;
                }
            }

            if (_stepAssistCooldownTimer > 0f)
            {
                _stepAssistCooldownTimer -= fixedDeltaTime;
                if (_stepAssistCooldownTimer < 0f)
                    _stepAssistCooldownTimer = 0f;
            }

            if (_recentBreachExitTimer > 0f)
            {
                _recentBreachExitTimer -= fixedDeltaTime;
                if (_recentBreachExitTimer < 0f)
                    _recentBreachExitTimer = 0f;
            }

            if (_surfaceGaspCooldownTimer > 0f)
            {
                _surfaceGaspCooldownTimer -= fixedDeltaTime;
                if (_surfaceGaspCooldownTimer < 0f)
                    _surfaceGaspCooldownTimer = 0f;
            }

            if (_fatalPressureRearmTimer > 0f)
            {
                _fatalPressureRearmTimer -= fixedDeltaTime;
                if (_fatalPressureRearmTimer < 0f)
                    _fatalPressureRearmTimer = 0f;
            }

            if (_impulseBypassTimer > 0f)
            {
                _impulseBypassTimer -= fixedDeltaTime;
                if (_impulseBypassTimer < 0f)
                    _impulseBypassTimer = 0f;
            }

            if (_criticalStaminaFailureTimer > 0f)
            {
                _criticalStaminaFailureTimer -= fixedDeltaTime;
                if (_criticalStaminaFailureTimer < 0f)
                    _criticalStaminaFailureTimer = 0f;
            }
        }

        private void AdvanceFatalPressureSequence(float fixedDeltaTime)
        {
            if (_fatalPressureSequenceTimer > 0f)
            {
                _fatalPressureSequenceTimer -= fixedDeltaTime;
                float duration = math.max(fatalPressureSequenceDuration, 0.01f);
                _fatalPressureSequenceIntensity = math.saturate(1f - (_fatalPressureSequenceTimer / duration));

                ApplyFatalPressureVisorCorruption(ResolveFatalPressureCorruptionIntensity(_fatalPressureSequenceIntensity));
                _fatalPressureSequenceGlitchPulseTimer -= fixedDeltaTime;
                if (_fatalPressureSequenceGlitchPulseTimer <= 0f)
                {
                    PulseFatalPressureGlitch(_fatalPressureSequenceIntensity);
                    _fatalPressureSequenceGlitchPulseTimer = math.lerp(
                        fatalPressureGlitchPulseIntervalMax,
                        fatalPressureGlitchPulseIntervalMin,
                        _fatalPressureSequenceIntensity);
                }

                QueueCameraEntanglementStrain(math.lerp(0.22f, 0.8f, _fatalPressureSequenceIntensity));

                PublishFatalPressureSignal(_fatalPressureSequenceIntensity);

                if (_fatalPressureSequenceTimer <= 0f)
                {
                    TriggerFatalPressureImplosion();
                    _fatalPressureSequenceTimer = 0f;
                    _fatalPressureSequenceGlitchPulseTimer = 0f;
                    _fatalPressureSequenceIntensity = 0f;
                    _fatalPressureLookYawAnchor = _cameraYaw;
                    _fatalPressureLookPitchAnchor = _cameraPitch;
                }
            }
        }

        private void UpdateWipeoutState(float fixedDeltaTime)
        {
            if (_stateMachine != null)
            {
                _wipeoutTimer = _stateMachine.WipeoutTimer;
                _wipeoutSeverity = _stateMachine.WipeoutSeverity;
            }
            else if (_wipeoutTimer > 0f)
            {
                _wipeoutTimer -= fixedDeltaTime;
                if (_wipeoutTimer <= 0f)
                {
                    _wipeoutTimer = 0f;
                    _wipeoutSeverity = 0f;
                }
            }
        }

        private void ProcessJumpInput(float fixedDeltaTime, bool exosuitActive, bool groundedOnDryLand, bool groundedOnShore)
        {
            if (_jumpRequested)
            {
                if (!exosuitActive && (groundedOnDryLand || groundedOnShore) && _jumpBufferTimer > 0f)
                {
                    if (TryApplyJumpImpulse(maxJumpVelocity))
                    {
                        ConsumeJumpRequest();
                        _jumpHeldTimer = 0f;
                        _dryGroundGraceTimer = 0f;
                        _shoreGroundGraceTimer = 0f;
                        _surfaceBreachLockTimer = 0f;
                    }
                }
            }

            if (_isAirborne && !exosuitActive)
            {
                if (_currentInputState.HasAction(PlayerInputAction.Jump))
                {
                    _jumpHeldTimer += fixedDeltaTime;
                }
                else if (_jumpHeldTimer > 0f)
                {
                    // Button released early, cut velocity short
                    float computedVelocity = Hecton8.PureLogic.Kinematics.VariableHeightJumpCalculator.Compute(
                        _jumpHeldTimer, maxJumpTime, minJumpVelocity, maxJumpVelocity);

                    _velocity = ResolveAuthoritativeLinearVelocity(UnityEngine.Vector3.zero);
                    if (_velocity.y > computedVelocity)
                    {
                        _velocity.y = computedVelocity;
                        ApplyMotorLinearVelocity(_velocity);
                    }
                    _jumpHeldTimer = 0f; // Reset to avoid repeatedly cutting velocity
                }

                if (_jumpHeldTimer >= maxJumpTime)
                {
                    _jumpHeldTimer = 0f;
                }
            }
            else
            {
                _jumpHeldTimer = 0f;
            }        }


        private void ApplyLadderSplineSnapFromAsyncProbe(bool suppressPhysicsMutation)
        {
            _ladderSplineSnapActive = false;
            _ladderSplineSnapAxisWorld = Vector3.zero;
            if (suppressPhysicsMutation || _playerMotor == null || _rb == null)
                return;

            // Ladder spline snap must be restored from a ladder-owned contact registry, not a removed PhysX hit cache.
        }

        private void ApplyLadderSplineVelocityGate(float3 ladderAxis)
        {
            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float3 planarVelocity = new float3(currentVelocity.x, 0f, currentVelocity.z);
            float3 gatedPlanarVelocity = math.project(planarVelocity, ladderAxis);
            currentVelocity.x = gatedPlanarVelocity.x;
            currentVelocity.z = gatedPlanarVelocity.z;
            ApplyMotorLinearVelocity(currentVelocity);
        }

        private bool TryResolveLadderSnapFrame(Collider collider, out Vector3 origin, out Vector3 forward)
        {
            origin = Vector3.zero;
            forward = Vector3.forward;
            if (collider == null)
                return false;

            int colliderInstanceId = unchecked((int)EntityId.ToULong(collider.GetEntityId()));
            ClimbableLadder ladder = colliderInstanceId == _cachedLadderSnapColliderInstanceId
                ? _cachedLadderSnapComponent
                : null;

            if (ladder == null)
            {
                if (!ClimbableLadder.TryGetByColliderInstanceId(colliderInstanceId, out ladder) || ladder == null)
                {
                    if (colliderInstanceId == _cachedLadderSnapColliderInstanceId)
                    {
                        _cachedLadderSnapColliderInstanceId = 0;
                        _cachedLadderSnapComponent = null;
                    }

                    return false;
                }

                _cachedLadderSnapColliderInstanceId = colliderInstanceId;
                _cachedLadderSnapComponent = ladder;
            }

            Transform ladderTransform = ladder.transform;
            Transform entryPoint = ladder.EntryPoint;
            origin = entryPoint != null ? entryPoint.position : ladderTransform.position;
            forward = ladderTransform.forward;
            if (forward.sqrMagnitude <= 0.000001f && entryPoint != null)
                forward = entryPoint.forward;

            return math.isfinite(origin.x) &&
                   math.isfinite(origin.y) &&
                   math.isfinite(origin.z) &&
                   math.isfinite(forward.x) &&
                   math.isfinite(forward.y) &&
                   math.isfinite(forward.z);
        }

        private void ApplyHydrostaticExitWeighting(float previousWaterImmersionRatio)
        {
            if (_rb == null ||
                previousWaterImmersionRatio <= 0.01f ||
                _waterImmersionRatio > 0.01f)
            {
                return;
            }

            float mass01 = math.saturate(_runtimeInventoryTotalMassKg * math.rcp(HydrostaticExitMassReferenceKg));
            if (mass01 <= 0.0001f)
                return;

            Vector3 velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float targetY = velocity.y;
            if (targetY > 0f)
            {
                targetY *= 1f - (HydrostaticExitUpwardDampingMax * mass01);
            }
            else if (targetY < 0f)
            {
                targetY *= math.lerp(1f, 1.45f, mass01);
            }

            targetY -= HydrostaticExitDownwardVelocityKick * mass01;
            float deltaY = targetY - velocity.y;
            if (math.abs(deltaY) <= 0.0001f)
                return;

            ApplyMotorVelocityChange(new Vector3(0f, deltaY, 0f));
        }

        private void AdvanceCurrentPhaseTimer(float fixedDeltaTime)
        {
            _currentTimer += fixedDeltaTime;
            float maxFrequency = math.max(underwaterTurbulenceFrequency, 0.173f);
            float wrapDuration = math.max(64f, 2048f / math.max(maxFrequency, 0.01f));
            if (_currentTimer >= wrapDuration)
                _currentTimer = math.fmod(_currentTimer, wrapDuration);
        }

        private float ComputeImmersionRatio()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float feetY = GetBodyBottomY();
            float headY = GetBodyTopY();
            return ComputeImmersionRatio(surfaceY, feetY, headY, playerHeight);
        }

        /// <summary>
        /// Depth in meters below water surface. 0 = at surface. Positive = deeper.
        /// Returns 0 if above water.
        /// </summary>
        private float ComputeDepth()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float eyeY = GetBodyEyeY();
            float depth = surfaceY - eyeY;
            return depth > 0f ? depth : 0f;
        }

        private bool IsInDryInterior()
        {
            return _buoyancyAirState != null && _buoyancyAirState.IsInDryZone;
        }

        private bool ResolveSurfaceSwimState(float physicsImmersion, PlayerTransportPreset transportPreset)
        {
            if (_isWalking || IsInDryInterior())
                return false;

            float surfaceY = EffectiveWaterSurfaceY;
            float headSurfaceOffset = GetHeadSurfaceOffset(surfaceY);
            float headDepth = headSurfaceOffset > 0f ? headSurfaceOffset : 0f;
            bool headTouchingSurface = headSurfaceOffset >= 0f && headSurfaceOffset <= surfaceHeadReattachDepth;
            bool deliberateDive = HasCommittedSurfaceDive(transportPreset);
            bool insideSurfaceBand =
                physicsImmersion >= surfaceBreachMinImmersion &&
                (_currentDepth <= surfaceSwimDepthBand || headDepth <= surfaceDiveBreakDepth);

            if (_surfaceBreachLockTimer > 0f)
                return false;

            if (headTouchingSurface)
                return true;

            if (_surfaceDiveAssistTimer > 0f && !_isSurfaceSwimming)
                return false;

            if (_isSurfaceSwimming)
            {
                if (deliberateDive && headDepth >= surfaceDiveBreakDepth)
                    return false;

                return insideSurfaceBand;
            }

            if (!insideSurfaceBand)
                return false;

            return !deliberateDive;
        }

        private PlayerLocomotionMode ResolveLocomotionMode(float physicsImmersion)
        {
            if (IsInDryInterior())
                return PlayerLocomotionMode.DryInteriorWalk;

            if (IsExosuitTransportActive())
                return PlayerLocomotionMode.ExosuitLocomotion;

            if (_isWalking)
            {
                if (physicsImmersion > 0.01f)
                    return PlayerLocomotionMode.ShallowWadeWalk;

                return PlayerLocomotionMode.DryGroundWalk;
            }

            return _isSurfaceSwimming
                ? PlayerLocomotionMode.SurfaceSwim
                : PlayerLocomotionMode.UnderwaterSwim;
        }

        private void ApplyShorelineWalkHandoffVelocity(bool wasWalkingBeforeModeResolution, bool hasShoreGroundSupport, bool hasImmediateShoreFooting)
        {
            if (wasWalkingBeforeModeResolution || IsInDryInterior() || !_isGrounded)
                return;

            if (!hasShoreGroundSupport && !hasImmediateShoreFooting)
                return;

            Vector3 groundNormal = _smoothedGroundNormal.sqrMagnitude > 0.0001f
                ? NormalizeVectorRsqrt(_smoothedGroundNormal, Vector3.up)
                : Vector3.up;
            if (groundNormal.y < _minGroundNormalY)
                return;

            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 handoffVelocity = ProjectVelocityOntoGroundPlanePreserveMagnitude(currentVelocity, groundNormal);
            if (handoffVelocity.sqrMagnitude <= 0.000001f)
                handoffVelocity = SuppressGroundPenetratingVelocity(currentVelocity, groundNormal);

            handoffVelocity = HectonPlayerMotor.SafeVelocity(handoffVelocity, currentVelocity);
            if ((handoffVelocity - currentVelocity).sqrMagnitude > 0.000001f)
                ApplyMotorLinearVelocity(handoffVelocity);
        }


        private PlayerEnvironmentState ResolveEnvironmentState(bool exosuitActive, float physicsImmersion)
        {
            if (IsInDryInterior())
                return PlayerEnvironmentState.DryInterior;

            if (physicsImmersion <= 0.01f)
                return PlayerEnvironmentState.DryExterior;

            if (exosuitActive || physicsImmersion < swimTransitionThreshold)
                return PlayerEnvironmentState.ShallowExterior;

            return _isSurfaceSwimming
                ? PlayerEnvironmentState.SurfaceExterior
                : PlayerEnvironmentState.UnderwaterExterior;
        }

        private PlayerSupportState ResolveSupportState(bool groundedOnDryLand, bool groundedOnShore)
        {
            return groundedOnDryLand || groundedOnShore || _isGrounded
                ? PlayerSupportState.Grounded
                : PlayerSupportState.Unsupported;
        }

        private PlayerOverrideState ResolveOverrideState(bool exosuitActive)
        {
            if (_wipeoutTimer > 0f)
                return PlayerOverrideState.Wipeout;

            if (exosuitActive)
                return PlayerOverrideState.Exosuit;

            return PlayerOverrideState.None;
        }

        private void SyncStateMachineContext(
            bool exosuitActive,
            float physicsImmersion,
            bool groundedOnDryLand,
            bool groundedOnShore)
        {
            _stateMachine?.SyncContext(
                ResolveEnvironmentState(exosuitActive, physicsImmersion),
                ResolveSupportState(groundedOnDryLand, groundedOnShore),
                ResolveOverrideState(exosuitActive),
                _currentLocomotionMode);
        }

        private bool HasSurfaceDiveIntent(PlayerTransportPreset transportPreset)
        {
            if (ResolveTransportSurfaceDiveAssistScale(transportPreset) <= 0f)
                return false;

            float requiredDiveAngle = math.max(surfaceDivePitchCommit, 30f);
            if (ResolveCameraLookDownAngle() < requiredDiveAngle)
                return false;

            return _inputV > surfaceDiveForwardCommit;
        }

        private bool HasCommittedSurfaceDive(PlayerTransportPreset transportPreset)
        {
            if (!HasSurfaceDiveIntent(transportPreset))
                return false;

            if (surfaceDiveCommitHoldTime <= 0f)
                return true;

            return _surfaceDiveCommitTimer >= surfaceDiveCommitHoldTime;
        }

        private void UpdateSurfaceDiveCommitTimer(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            bool canCommitDive =
                !_isWalking &&
                !IsInDryInterior() &&
                _waterImmersionRatio > 0.01f;

            bool hasDiveIntent = canCommitDive && HasSurfaceDiveIntent(transportPreset);
            if (hasDiveIntent)
            {
                if (surfaceDiveCommitHoldTime <= 0f)
                {
                    _surfaceDiveCommitTimer = 0f;
                    return;
                }

                _surfaceDiveCommitTimer += fixedDeltaTime;
                if (_surfaceDiveCommitTimer > surfaceDiveCommitHoldTime)
                    _surfaceDiveCommitTimer = surfaceDiveCommitHoldTime;
                return;
            }

            if (_surfaceDiveCommitTimer <= 0f)
                return;

            _surfaceDiveCommitTimer -= fixedDeltaTime * 2f;
            if (_surfaceDiveCommitTimer < 0f)
                _surfaceDiveCommitTimer = 0f;
        }

        private void UpdateSurfaceLockState(float fixedDeltaTime)
        {
            if (_surfaceBreachLockTimer > 0f)
            {
                _surfaceLockBlend = 0f;
                _surfaceLockTargetY = ResolveBodyRuntimePosition().y;
                return;
            }

            float targetBlend = _isSurfaceSwimming ? _shoreBuoyancyBlend : 0f;
            float blendSpeed = targetBlend > _surfaceLockBlend
                ? surfaceSnapEngageSpeed
                : surfaceSnapReleaseSpeed;
            float blendT = ResolveLinearBlendT(math.max(blendSpeed, 0.01f), fixedDeltaTime);
            _surfaceLockBlend = math.lerp(_surfaceLockBlend, targetBlend, blendT);

            if (_isSurfaceSwimming)
            {
                float targetRootY = EffectiveWaterSurfaceY + surfaceStickOffset;
                float shorelineTargetRootY = math.lerp(ResolveBodyRuntimePosition().y, targetRootY, _shoreBuoyancyBlend);
                float followT = ResolveLinearBlendT(math.max(surfaceWaveFollowSharpness, 0.01f), fixedDeltaTime);
                _surfaceLockTargetY = math.lerp(_surfaceLockTargetY, shorelineTargetRootY, followT);
                return;
            }

            if (_surfaceLockBlend <= 0.001f)
            {
                _surfaceLockBlend = 0f;
                _surfaceLockTargetY = ResolveBodyRuntimePosition().y;
            }
        }

        private void UpdateWaterPresentationPose(float fixedDeltaTime)
        {
            Quaternion targetSurfacePose = Quaternion.identity;
            if (_isSurfaceSwimming && _crestSamplingSucceeded && _surfaceLockBlend > 0.001f)
            {
                ResolveDegreesSinCosFast(_bodyYaw, out float sinYaw, out float cosYaw);
                Vector3 bodyForward = new Vector3(sinYaw, 0f, cosYaw);
                Vector3 desiredForward = ProjectOnPlaneFast(bodyForward, EffectiveWaterSurfaceNormal);
                desiredForward = desiredForward.sqrMagnitude <= 0.0001f
                    ? bodyForward
                    : NormalizeVectorRsqrt(desiredForward, bodyForward);

                Quaternion yawRotation = ResolveWorldYawRotation(_bodyYaw);
                Quaternion waveRotation = Quaternion.LookRotation(desiredForward, EffectiveWaterSurfaceNormal);
                Quaternion localDelta = ConjugateUnitQuaternion(yawRotation) * waveRotation;
                float pitch = math.clamp(ExtractLocalPitchDegrees(localDelta), -surfaceWaveMaxPitch, surfaceWaveMaxPitch) * _shoreBuoyancyBlend;
                float roll = math.clamp(ExtractLocalRollDegrees(localDelta), -surfaceWaveMaxRoll, surfaceWaveMaxRoll) * _shoreBuoyancyBlend;
                targetSurfacePose = ComposeAxisAngleDegrees(pitch, 0f, roll);
            }

            float surfaceBlendT = ResolveLinearBlendT(math.max(surfaceWaveAlignmentSharpness, 0.01f), fixedDeltaTime);
            _surfaceWavePoseRotation = FastLerpQuaternion(_surfaceWavePoseRotation, targetSurfacePose, surfaceBlendT);
        }

        private void UpdateDynamicCollisionProfile(float fixedDeltaTime, bool suppressPhysicsMutation)
        {
            float targetTuck = 0f;
            if (_isSurfaceSwimming && _crestSamplingSucceeded && _surfaceLockBlend > 0.001f)
            {
                float downhillSlopeT = math.saturate(-_dynamicWaveLocalSlope.y / math.max(dynamicCollisionTuckSlopeForFull, 0.0001f));
                float descentPosePitch = -ExtractLocalPitchDegrees(_surfaceWavePoseRotation);
                float descentPoseT = math.saturate(descentPosePitch / math.max(surfaceWaveMaxPitch, 0.01f));
                float immersionDepthT = math.saturate(_currentDepth / math.max(dynamicCollisionImmersionDepthForFull, 0.01f));
                targetTuck = math.max(downhillSlopeT, descentPoseT) * immersionDepthT * _surfaceLockBlend * _shoreBuoyancyBlend;
            }

            if (_physicalTraumaCollisionHoldTimer > 0f)
            {
                _physicalTraumaCollisionHoldTimer -= fixedDeltaTime;
                if (_physicalTraumaCollisionHoldTimer < 0f)
                    _physicalTraumaCollisionHoldTimer = 0f;
            }

            float blendT = ResolveLinearBlendT(math.max(dynamicCollisionDeformationBlendSharpness, 0.01f), fixedDeltaTime);
            _dynamicCollisionTuck01 = math.lerp(_dynamicCollisionTuck01, targetTuck, blendT);

            float traumaCollisionTarget = _physicalTraumaCollisionHoldTimer > 0f ? _physicalTraumaCollisionWeight : 0f;
            float traumaBlendT = ResolveLinearBlendT(math.max(physicalTraumaCollisionRecoverySharpness, 0.01f), fixedDeltaTime);
            _physicalTraumaCollisionWeight = math.lerp(_physicalTraumaCollisionWeight, traumaCollisionTarget, traumaBlendT);

            float waveCollisionHeightScale = _requestedTransportCollisionHeightScale * math.lerp(1f, dynamicCollisionMinHeightScale, _dynamicCollisionTuck01);
            float waveCollisionRadiusScale = _requestedTransportCollisionRadiusScale * math.lerp(1f, dynamicCollisionMaxRadiusScale, _dynamicCollisionTuck01);
            float waveCollisionCenterYOffset = _requestedTransportCollisionCenterYOffset + math.lerp(0f, dynamicCollisionCenterYOffset, _dynamicCollisionTuck01);
            float traumaCollisionHeightScale = math.lerp(1f, physicalTraumaCollisionHeightScale, _physicalTraumaCollisionWeight);
            float traumaCollisionRadiusScale = math.lerp(1f, physicalTraumaCollisionRadiusScale, _physicalTraumaCollisionWeight);
            float traumaCollisionCenterYOffset = math.lerp(0f, physicalTraumaCollisionCenterYOffset, _physicalTraumaCollisionWeight);
            float collisionHeightScale = waveCollisionHeightScale * traumaCollisionHeightScale;
            float collisionRadiusScale = waveCollisionRadiusScale * traumaCollisionRadiusScale;
            float collisionCenterYOffset = waveCollisionCenterYOffset + traumaCollisionCenterYOffset;
            if (suppressPhysicsMutation)
                return;

            ApplyResolvedCollisionProfile(collisionRadiusScale, collisionHeightScale, collisionCenterYOffset);
        }

        private void ApplyUnderwaterTurbulence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            Quaternion targetTurbulencePose = Quaternion.identity;
            bool exosuitActive = IsExosuitTransportActive();
            if ((_isWalking && !exosuitActive) || _isSurfaceSwimming || IsInDryInterior() || !_crestSamplingSucceeded)
            {
                UpdateUnderwaterStressSignal(0f, fixedDeltaTime);
                float fadeT = ResolveLinearBlendT(math.max(underwaterTurbulencePoseSharpness, 0.01f), fixedDeltaTime);
                _underwaterTurbulencePoseRotation = FastLerpQuaternion(_underwaterTurbulencePoseRotation, targetTurbulencePose, fadeT);
                return;
            }

            if (_currentDepth <= 0f || _currentDepth > underwaterTurbulenceMaxDepth)
            {
                UpdateUnderwaterStressSignal(0f, fixedDeltaTime);
                float fadeT = ResolveLinearBlendT(math.max(underwaterTurbulencePoseSharpness, 0.01f), fixedDeltaTime);
                _underwaterTurbulencePoseRotation = FastLerpQuaternion(_underwaterTurbulencePoseRotation, targetTurbulencePose, fadeT);
                return;
            }

            float environmentalInfluenceScale = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float depthFade = 1f - math.saturate(_currentDepth / math.max(underwaterTurbulenceMaxDepth, 0.01f));
            float bottomBoost = 1f;
            if (!float.IsPositiveInfinity(_bottomClearance))
            {
                float bottomT = 1f - math.saturate(_bottomClearance / math.max(underwaterTurbulenceBottomInfluenceDepth, 0.01f));
                bottomBoost = math.lerp(1f, underwaterTurbulenceBottomBoost, bottomT);
            }

            float turbulenceIntensity = _dynamicStormIntensity * depthFade * environmentalInfluenceScale * bottomBoost;
            float stressDenominator = math.max(1f - underwaterStressSignalThreshold, 0.0001f);
            float stressTarget = math.saturate((turbulenceIntensity - underwaterStressSignalThreshold) / stressDenominator);
            UpdateUnderwaterStressSignal(stressTarget, fixedDeltaTime);
            if (turbulenceIntensity > 0.001f)
            {
                Vector3 horizontalWaveVelocity = _dynamicAverageWaterVelocity;
                horizontalWaveVelocity.y = 0f;
                Vector3 horizontalDisplacement = _dynamicAverageWaterDisplacement;
                horizontalDisplacement.y = 0f;
                Vector3 horizontalWaveVector = horizontalWaveVelocity + horizontalDisplacement * underwaterTurbulenceFrequency + EffectiveWaterFlowVelocity * 0.55f;
                ResolveDegreesSinCosFast(_bodyYaw, out float fallbackSinYaw, out float fallbackCosYaw);
                Vector3 fallbackWaveVector = new Vector3(fallbackSinYaw, 0f, fallbackCosYaw);
                if (horizontalWaveVector.sqrMagnitude <= 0.0001f)
                {
                    horizontalWaveVector = fallbackWaveVector;
                }
                else
                {
                    horizontalWaveVector = NormalizeVectorRsqrt(horizontalWaveVector, fallbackWaveVector);
                }

                Vector3 crossWave = new Vector3(-horizontalWaveVector.z, 0f, horizontalWaveVector.x);
                float turbulencePhase = _currentTimer * underwaterTurbulenceFrequency;
                float lateralOscillation = SignedTriangleRadians(turbulencePhase * TwoPi + _dynamicAverageWaterDisplacement.x * 1.65f + _dynamicAverageWaterVelocity.z * 0.3f);
                float verticalOscillation = SignedTriangleRadians(turbulencePhase * TwoPi * 1.37f + _dynamicAverageWaterDisplacement.z * 1.1f - _dynamicAverageWaterVelocity.x * 0.45f + 1.5707964f);
                float undertowOscillation = SignedTriangleRadians(turbulencePhase * TwoPi * 0.53f + _dynamicWaveHeightSpan * 1.6f);

                float mass = ResolveAuthoritativeBodyMassKg();
                float lateralForce = underwaterTurbulenceForce * turbulenceIntensity * mass;
                float verticalForce = underwaterTurbulenceVerticalForce * turbulenceIntensity * mass;
                _forceVector.x = (crossWave.x * lateralOscillation - horizontalWaveVector.x * undertowOscillation * 0.65f) * lateralForce;
                _forceVector.y = (-math.abs(undertowOscillation) * 0.55f + verticalOscillation * 0.45f) * verticalForce;
                _forceVector.z = (crossWave.z * lateralOscillation - horizontalWaveVector.z * undertowOscillation * 0.65f) * lateralForce;
                ApplyMotorAccelerationFromForce(_forceVector);

                float targetPitch = math.clamp(
                    (-undertowOscillation * underwaterTurbulencePitch) + verticalOscillation * underwaterTurbulencePitch * 0.35f,
                    -underwaterTurbulencePitch,
                    underwaterTurbulencePitch) * turbulenceIntensity;
                float targetRoll = math.clamp(
                    lateralOscillation * underwaterTurbulenceRoll,
                    -underwaterTurbulenceRoll,
                    underwaterTurbulenceRoll) * turbulenceIntensity;
                targetTurbulencePose = ComposeAxisAngleDegrees(targetPitch, 0f, targetRoll);
            }

            float turbulenceBlendT = ResolveLinearBlendT(math.max(underwaterTurbulencePoseSharpness, 0.01f), fixedDeltaTime);
            _underwaterTurbulencePoseRotation = FastLerpQuaternion(_underwaterTurbulencePoseRotation, targetTurbulencePose, turbulenceBlendT);
        }

        private void ApplyAbyssalCurrents(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            AdvanceAbyssalTransportTurbulenceSteering(fixedDeltaTime);
            bool exosuitActive = IsExosuitTransportActive();

            if (_abyssalDowndraftCooldownTimer > 0f)
            {
                _abyssalDowndraftCooldownTimer -= fixedDeltaTime;
                if (_abyssalDowndraftCooldownTimer < 0f)
                    _abyssalDowndraftCooldownTimer = 0f;
            }

            if (_abyssalDowndraftActiveTimer > 0f)
            {
                _abyssalDowndraftActiveTimer -= fixedDeltaTime;
                if (_abyssalDowndraftActiveTimer < 0f)
                    _abyssalDowndraftActiveTimer = 0f;
            }

            if (_abyssalFlowNoiseBoundaryCooldownTimer > 0f)
            {
                _abyssalFlowNoiseBoundaryCooldownTimer -= fixedDeltaTime;
                if (_abyssalFlowNoiseBoundaryCooldownTimer < 0f)
                    _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            }

            if ((_isWalking && !exosuitActive) || _isSurfaceSwimming || IsInDryInterior() || _currentDepth < abyssalCurrentStartDepth)
            {
                _abyssalDowndraftIntensity = 0f;
                _abyssalDowndraftVelocityChange = Vector3.zero;
                ResetAbyssalCurrentShearRuntime();
                _previousAbyssalNoisyFlow = Vector3.zero;
                return;
            }

            float depthT = math.saturate(
                (_currentDepth - abyssalCurrentStartDepth) /
                math.max(abyssalCurrentFullDepth - abyssalCurrentStartDepth, 0.01f));
            if (depthT <= 0f)
            {
                _abyssalDowndraftIntensity = 0f;
                _abyssalDowndraftVelocityChange = Vector3.zero;
                ResetAbyssalCurrentShearRuntime();
                _previousAbyssalNoisyFlow = Vector3.zero;
                return;
            }

            Vector3 abyssalNoisyFlow = ResolveAbyssalAmbientFlowWithNoise();
            UpdateAbyssalCurrentShear(abyssalNoisyFlow, depthT, fixedDeltaTime);
            UpdateAbyssalCounterDriveEnergyMultiplier(abyssalNoisyFlow, depthT, transportPreset);
            ApplyAbyssalFlowBoundaryTurbulence(abyssalNoisyFlow, depthT, transportPreset);

            if (_abyssalDowndraftCooldownTimer <= 0f)
            {
                Vector3 downdraftDirection = ResolveAbyssalDowndraftDirection(abyssalNoisyFlow);
                float velocityChangeMagnitude = math.lerp(
                    abyssalDowndraftVelocityChangeMin,
                    abyssalDowndraftVelocityChangeMax,
                    depthT);
                float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
                _abyssalDowndraftVelocityChange = downdraftDirection * velocityChangeMagnitude * transportInfluence;
                _abyssalDowndraftIntensity = depthT;
                _abyssalDowndraftActiveTimer = abyssalDowndraftAftershockDuration;
                QueueEnvironmentalVelocityChange(_abyssalDowndraftVelocityChange);
                _abyssalDowndraftCooldownTimer = ResolveAbyssalDowndraftInterval(depthT);
            }

            if (_abyssalDowndraftActiveTimer <= 0f || _survivalSystem == null)
                return;

            float upwardLookT = math.saturate((-_cameraPitch - 10f) / 30f);
            float counterIntent = math.max(math.saturate(_inputVertical), upwardLookT * math.saturate(_inputV));
            float transportCounter = math.saturate(ResolveActiveTransportBoost01()) * 0.65f;
            float drainIntent = math.max(counterIntent, transportCounter);
            if (drainIntent <= 0.001f)
                return;

            _survivalSystem.DrainEnergy(abyssalDowndraftCounterEnergyDrain * _abyssalDowndraftIntensity * drainIntent * fixedDeltaTime);
        }

        private void ResetAbyssalCurrentShearRuntime()
        {
            _abyssalCounterDriveEnergyMultiplier = 1f;
            _abyssalShearSpeedMultiplier = 1f;
            _abyssalShearDrainMultiplier = 1f;
            _abyssalFlowWeatherCurrent = Vector3.zero;
        }

        private void UpdateAbyssalCurrentShear(Vector3 abyssalNoisyFlow, float depthT, float fixedDeltaTime)
        {
            _abyssalFlowWeatherCurrent = HectonPlayerMotor.SafeVelocity(abyssalNoisyFlow);
            _abyssalShearSpeedMultiplier = 1f;
            _abyssalShearDrainMultiplier = 1f;

            if (_rb == null || fixedDeltaTime <= 0f)
                return;

            Vector3 velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float velocitySqr = velocity.sqrMagnitude;
            float flowSqr = _abyssalFlowWeatherCurrent.sqrMagnitude;
            if (velocitySqr <= 0.0001f || flowSqr <= 0.0001f)
                return;

            float invVelocity = math.rsqrt(math.max(velocitySqr, 0.000001f));
            float invFlow = math.rsqrt(math.max(flowSqr, 0.000001f));
            float flowAlignment = DotVector(velocity * invVelocity, _abyssalFlowWeatherCurrent * invFlow);
            if (flowAlignment >= 0f)
                return;

            float opposition01 = math.saturate(-flowAlignment);
            float flowThreshold = math.max(abyssalCounterDriveFlowThreshold, 0.01f);
            float flowStrength01 = math.saturate(flowSqr / (flowThreshold * flowThreshold));
            float shear01 = opposition01 * flowStrength01 * math.saturate(depthT);
            if (shear01 <= 0.0001f)
                return;

            float logarithmicCapT = shear01 * (2f - shear01);
            _abyssalShearSpeedMultiplier = math.lerp(1f, abyssalCurrentShearMaxSpeedMultiplier, logarithmicCapT);
            _abyssalShearDrainMultiplier = ResolveAbyssalShearDrainMultiplierApprox(
                shear01,
                abyssalCurrentShearDrainExponent);

            if (_survivalSystem == null)
                return;

            float extraDrain = math.max(0f, _abyssalShearDrainMultiplier - 1f);
            if (extraDrain <= 0f)
                return;

            _survivalSystem.DrainOxygen(abyssalCurrentShearOxygenDrainPerSecond * extraDrain * fixedDeltaTime);
            _survivalSystem.DrainEnergy(abyssalCurrentShearEnergyDrainPerSecond * extraDrain * fixedDeltaTime);
        }

        private static float ResolveAbyssalShearDrainMultiplierApprox(float shear01, float exponent)
        {
            float x = math.saturate(shear01);
            float e = math.clamp(exponent, 1f, 6f);
            float x2 = x * x;
            float x3 = x2 * x;
            float x4 = x3 * x;
            float c2 = 0.5f * e * (e - 1f);
            float c3 = 0.16666667f * e * (e - 1f) * (e - 2f);
            float c4 = 0.04166667f * e * (e - 1f) * (e - 2f) * (e - 3f);
            return math.max(1f, 1f + (e * x) + (c2 * x2) + (c3 * x3) + (c4 * x4));
        }

        private void UpdateAbyssalCounterDriveEnergyMultiplier(Vector3 abyssalNoisyFlow, float depthT, PlayerTransportPreset transportPreset)
        {
            _abyssalCounterDriveEnergyMultiplier = 1f;

            Vector3 suctionVector = _abyssalDowndraftActiveTimer > 0f
                ? _abyssalDowndraftVelocityChange
                : abyssalNoisyFlow * math.max(0.15f, depthT);
            float suctionThreshold = math.max(0.01f, abyssalCounterDriveFlowThreshold);
            float suctionSqr = suctionVector.sqrMagnitude;
            if (suctionSqr <= suctionThreshold * suctionThreshold)
                return;

            if (!TryResolveAbyssalCounterDriveDirection(transportPreset, out Vector3 counterDirection))
                return;

            float inverseSuctionMagnitude = math.rsqrt(math.max(suctionSqr, 0.000001f));
            float oppositionDot = DotVector(counterDirection, -suctionVector * inverseSuctionMagnitude);
            float oppositionThreshold = ResolveDegreesCosFast(180f - abyssalCounterDriveOppositionAngleDegrees);
            if (oppositionDot < oppositionThreshold)
                return;

            _abyssalCounterDriveEnergyMultiplier = math.max(1f, abyssalCounterDriveEnergyOverstrainMultiplier);
        }

        private bool TryResolveAbyssalCounterDriveDirection(PlayerTransportPreset transportPreset, out Vector3 counterDirection)
        {
            counterDirection = Vector3.zero;

            if (IsExosuitTransportActive())
            {
                float jumpIntent = ResolveExosuitJumpJetIntent();
                if (jumpIntent > 0.001f)
                {
                    counterDirection = Vector3.up;
                    return true;
                }
            }

            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            if (propulsionReference <= 0.01f)
                return false;

            Vector3 inputDirection = _cachedTransform.forward * _inputV +
                                     _cachedTransform.right * _inputH +
                                     Vector3.up * _inputVertical;
            if (inputDirection.sqrMagnitude <= 0.0001f)
            {
                if (ResolveActiveTransportBoost01() <= 0.01f)
                    return false;

                inputDirection = _cachedTransform.forward;
            }

            counterDirection = NormalizeVectorRsqrt(inputDirection, _cachedTransform.forward);
            return true;
        }

        private void ApplyThermalUpdrafts(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_thermalUpdraftTraumaCooldownTimer > 0f)
            {
                _thermalUpdraftTraumaCooldownTimer -= fixedDeltaTime;
                if (_thermalUpdraftTraumaCooldownTimer < 0f)
                    _thermalUpdraftTraumaCooldownTimer = 0f;
            }

            Vector3 totalVelocityChange = Vector3.zero;
            float strongestIntensity = 0f;

            if ((!_isWalking || IsExosuitTransportActive()) && !IsInDryInterior() && _currentDepth >= thermalUpdraftStartDepth)
            {
                float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
                if (transportInfluence > 0.0001f)
                {
                    Vector3 sampledCurrent = SampleAuthoredAmbientCurrent(ResolvePlayerAupRuntimePosition());
                    if (sampledCurrent.y > thermalUpdraftSpeedThreshold)
                    {
                        float currentIntensity = math.saturate(
                            (sampledCurrent.y - thermalUpdraftSpeedThreshold) /
                            math.max(thermalUpdraftSpeedMax - thermalUpdraftSpeedThreshold, 0.01f));
                        float verticalChange = thermalUpdraftVelocityChangePerSecond * currentIntensity * transportInfluence * fixedDeltaTime;
                        totalVelocityChange.y += verticalChange;
                        strongestIntensity = math.max(strongestIntensity, currentIntensity);
                    }
                }
            }

            if (_externalThermalUpdraftRequestedThisStep && _externalThermalUpdraftVelocityChange.y > 0.0001f)
            {
                totalVelocityChange += _externalThermalUpdraftVelocityChange;
                strongestIntensity = math.max(
                    strongestIntensity,
                    math.saturate(ApproximateVectorMagnitude(_externalThermalUpdraftVelocityChange) / math.max(thermalUpdraftVelocityChangePerSecond, 0.01f)));
            }

            _thermalUpdraftVelocityChange = totalVelocityChange;
            _thermalUpdraftIntensity = strongestIntensity;

            if (totalVelocityChange.sqrMagnitude > 0.0001f)
            {
                QueueEnvironmentalVelocityChange(totalVelocityChange);
                if (strongestIntensity >= thermalUpdraftTraumaThreshold && _thermalUpdraftTraumaCooldownTimer <= 0f)
                {
                    ApplyPhysicalTrauma(totalVelocityChange * ResolveAuthoritativeBodyMassKg(), strongestIntensity);
                    _thermalUpdraftTraumaCooldownTimer = thermalUpdraftTraumaCooldown;
                }
            }

            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
        }

        private void UpdateHullStress(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_hullStressHudCorruptionRefreshTimer > 0f)
            {
                _hullStressHudCorruptionRefreshTimer -= fixedDeltaTime;
                if (_hullStressHudCorruptionRefreshTimer < 0f)
                    _hullStressHudCorruptionRefreshTimer = 0f;
            }

            if (_hullStressGroanCooldownTimer > 0f)
            {
                _hullStressGroanCooldownTimer -= fixedDeltaTime;
                if (_hullStressGroanCooldownTimer < 0f)
                    _hullStressGroanCooldownTimer = 0f;
            }

            float targetStress = 0f;
            if (!IsInDryInterior() && (!_isWalking || IsExosuitTransportActive()) && _currentDepth > crushDepthStart)
            {
                float depthT = math.saturate(
                    (_currentDepth - crushDepthStart) /
                    math.max(crushDepthFullDepth - crushDepthStart, 0.01f));

                // World velocity is up-positive, so descending yields a negative Y. The rate-driven
                // stress term wants the magnitude - a fast pressure change strains the hull either
                // way - while the predictive urgency term below wants descent-positive m/s.
                float verticalVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero).y;
                float descentRate = -verticalVelocity;
                float depthRateT = math.saturate(math.abs(verticalVelocity) / math.max(crushDepthRateForFullStress, 0.01f));

                float transportProtection = transportPreset != null
                    ? math.max(0.1f, transportPreset.PressureDamageScale)
                    : 1f;

                float computedStress = Hecton8.PureLogic.Systems.PressureHullIntegrityStressCalculator.Compute(
                    depthT,
                    0f,
                    depthRateT,
                    transportProtection
                );

                // The urgency denominator is the crush LIMIT, not the depth where stress begins.
                // Dividing by crushDepthStart pinned this to 1.0 permanently, because this branch
                // only runs when _currentDepth already exceeds crushDepthStart. That made
                // targetStress a constant 1.0 and left crushDepthFullDepth,
                // crushDepthRateForFullStress and PressureDamageScale dead. crushDepthFullDepth is
                // the same limit already reported as CrushLimitMeters in TryPlayCrushDepthGroan,
                // and it carries the same inversion guard the signal uses.
                float warningUrgency = Hecton8.PureLogic.Systems.HudCrushDepthWarningUrgencyCalculator.Compute(
                    _currentDepth,
                    math.max(crushDepthFullDepth, crushDepthStart),
                    descentRate);

                targetStress = math.saturate(math.max(computedStress, warningUrgency));
            }

            if (_externalHullStressRequestedThisStep)
            {
                targetStress = math.max(targetStress, _externalHullStressRequestedIntensity);
                _externalHullStressRequestedIntensity = 0f;
                _externalHullStressRequestedThisStep = false;
            }

            float blendT = ResolveLinearBlendT(math.max(crushDepthStressBlendSharpness, 0.01f), fixedDeltaTime);
            _hullStressIntensity = math.lerp(_hullStressIntensity, targetStress, blendT);

            if (_hullStressIntensity > crushDepthShakeThreshold)
            {
                float normalizedShake = math.saturate(
                    (_hullStressIntensity - crushDepthShakeThreshold) /
                    math.max(1f - crushDepthShakeThreshold, 0.01f));
                QueueCameraEntanglementStrain(normalizedShake * 0.55f);
            }

            TryPlayCrushDepthGroan();
            RefreshFatalPressureHudCorruptionIfNeeded();

            if (_hullStressIntensity < crushDepthImplosionThreshold || _fatalPressureSequenceTimer > 0f || _fatalPressureRearmTimer > 0f)
                return;
            StartFatalPressureSequence();
        }

        private void RefreshFatalPressureHudCorruptionIfNeeded()
        {
            if (_hullStressIntensity <= 0.9f || _hullStressHudCorruptionRefreshTimer > 0f)
                return;

            ILocalizationStressHudRefreshSink localization = _localizationStressHudRefreshSink;
            if (localization == null)
                return;

            localization.RefreshHullStressHudCorruptionVisuals();
            _hullStressHudCorruptionRefreshTimer = 0.5f;
        }

        private void TryPlayCrushDepthGroan()
        {
            if (_hullStressIntensity < crushDepthGroanThreshold || _hullStressGroanCooldownTimer > 0f)
                return;

            float groanT = math.saturate(
                (_hullStressIntensity - crushDepthGroanThreshold) /
                math.max(1f - crushDepthGroanThreshold, 0.01f));
            CrushWarningSignal signal = new CrushWarningSignal
            {
                WarningHash = VocalWarningHashes.CrushDepth,
                SourceId = 0u,
                DepthMeters = math.max(0f, _currentDepth),
                CrushLimitMeters = math.max(crushDepthFullDepth, crushDepthStart),
                Severity01 = groanT,
                Frame = SystemDispatcher.CurrentFrameId,
                Priority = (byte)VocalWarningId.CrushDepth,
                Flags = VocalWarningSignalFlags.HabitatIntegrityCompromised
            };
            SignalBus<CrushWarningSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            _hullStressGroanCooldownTimer = math.lerp(crushDepthGroanIntervalMax, crushDepthGroanIntervalMin, groanT);
        }

        private void UpdateUnderwaterStressSignal(float targetIntensity, float fixedDeltaTime)
        {
            float blendT = ResolveLinearBlendT(math.max(underwaterStressSignalBlendSharpness, 0.01f), fixedDeltaTime);
            _underwaterStressSignalIntensity = math.lerp(_underwaterStressSignalIntensity, math.saturate(targetIntensity), blendT);
        }

        private void ApplyShoreUndertow(float physicsImmersion, PlayerTransportPreset transportPreset)
        {
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;

            if (IsInDryInterior() || physicsImmersion <= 0.02f)
                return;

            if (_dynamicStormIntensity <= shoreUndertowStormThreshold)
                return;

            if (float.IsPositiveInfinity(_bottomClearance))
                return;

            Vector3 downSlopeDirection = ProjectOnPlaneFast(Vector3.down, _bottomNormal);
            float downSlopeSqrMagnitude = downSlopeDirection.sqrMagnitude;
            if (downSlopeSqrMagnitude <= 0.0001f)
                return;

            downSlopeDirection *= math.rsqrt(math.max(downSlopeSqrMagnitude, 0.000001f));

            Vector3 retreatVelocity = _dynamicAverageWaterVelocity + EffectiveWaterFlowVelocity * 0.8f + _dynamicAverageWaterDisplacement * underwaterTurbulenceFrequency;
            float retreatSpeed = DotVector(retreatVelocity, downSlopeDirection);
            float retreatT = math.saturate(
                (retreatSpeed - shoreUndertowRetreatVelocityStart) /
                math.max(shoreUndertowRetreatVelocityMax - shoreUndertowRetreatVelocityStart, 0.01f));
            if (retreatT <= 0f)
                return;

            float stormT = math.saturate(
                (_dynamicStormIntensity - shoreUndertowStormThreshold) /
                math.max(1f - shoreUndertowStormThreshold, 0.01f));
            float shallowT = 1f - math.saturate(_currentDepth / math.max(shoreUndertowMaxDepth, 0.01f));
            float shorelineT = 1f - _shoreBuoyancyBlend;
            float bottomT = 1f - math.saturate(_bottomClearance / math.max(shoreBuoyancyRecoveryClearance, 0.01f));
            float feetDepth = GetFeetDepthBelowSurface(EffectiveWaterSurfaceY);
            float kneeDepthT = math.saturate(
                (feetDepth - shoreUndertowMinFeetDepth) /
                math.max(shoreUndertowFullFeetDepth - shoreUndertowMinFeetDepth, 0.01f));
            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float surfaceBoost = _isSurfaceSwimming ? shoreUndertowSurfaceBoost : 1f;
            float undertowIntensity = stormT * retreatT * shallowT * math.max(shorelineT, bottomT) * kneeDepthT * transportInfluence * surfaceBoost;
            if (undertowIntensity <= 0.001f)
                return;

            float undertowForce = shoreUndertowForce * undertowIntensity * ResolveAuthoritativeBodyMassKg();
            _undertowVector = downSlopeDirection * undertowForce;
            _undertowIntensity = undertowIntensity;
            QueueEnvironmentalForce(_undertowVector);
        }

        private bool ShouldRequestTransportBailout(float impactSpeed, IPlayerTransportLifecycleOwner transportLifecycleOwner)
        {
            if (transportLifecycleOwner == null || _transportBailoutCooldownTimer > 0f)
                return false;

            if (impactSpeed >= wipeoutBailoutSpeedThreshold)
                return true;

            return transportLifecycleOwner.IsTransportBroken ||
                   transportLifecycleOwner.TransportIntegrityNormalized <= wipeoutBailoutCriticalIntegrityThreshold;
        }

        private void UpdateTransportCriticalBailout()
        {
            if (_wipeoutTimer > 0f || _fatalPressureSequenceTimer > 0f || _transportBailoutCooldownTimer > 0f)
                return;

            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            if (!_playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner transportLifecycleOwner) ||
                transportLifecycleOwner == null)
                return;

            if (!transportLifecycleOwner.IsTransportBroken &&
                transportLifecycleOwner.TransportIntegrityNormalized > wipeoutBailoutCriticalIntegrityThreshold)
                return;

            StartWipeout(
                1f,
                ResolveBailoutSpeed(),
                ResolvePlayerAupRuntimePosition(),
                Vector3.up,
                transportLifecycleOwner,
                true,
                Vector3.zero);
        }

        private void StartWipeout(
            float severity,
            float impactSpeed,
            Vector3 hitPoint,
            Vector3 hitNormal,
            IPlayerTransportLifecycleOwner transportLifecycleOwner,
            bool requestTransportBailout,
            Vector3 bailoutImpulse)
        {
            bool wasAlreadyInWipeout = _wipeoutTimer > 0f;
            _wipeoutSeverity = math.max(_wipeoutSeverity, severity);
            _wipeoutTimer = math.max(_wipeoutTimer, wipeoutDuration);
            _stateMachine?.BeginWipeout(_wipeoutSeverity, _wipeoutTimer);
            _impulseBypassTimer = math.max(_impulseBypassTimer, wipeoutImpulseBypassDuration);
            _recentBreachExitTimer = 0f;
            _surfaceBreachLockTimer = 0f;
            _surfaceDiveAssistTimer = 0f;
            _surfaceDiveCommitTimer = 0f;
            _surfaceLockBlend = 0f;
            _jumpRequested = false;
            _jumpBufferTimer = 0f;

            if (currentSuitData != null)
                QueueCameraCollisionImpulse(impactSpeed * math.lerp(1.25f, 1.8f, severity));

            if (transportLifecycleOwner != null)
                transportLifecycleOwner.ApplyTransportCollisionImpact(impactSpeed * wipeoutTransportDamageScale, hitPoint, hitNormal);

            if (!wasAlreadyInWipeout)
                TryBreakSuitUpgradeFromWipeout();
            EmitWipeoutImpactFeedback(severity);

            Vector3 reboundDirection = hitNormal + Vector3.up * 0.28f;
            reboundDirection = reboundDirection.sqrMagnitude <= 0.0001f
                ? Vector3.up
                : NormalizeVectorRsqrt(reboundDirection, Vector3.up);

            float reboundImpulse = wipeoutReboundImpulse * math.lerp(0.75f, 1.35f, severity);
            float mass = ResolveAuthoritativeBodyMassKg();
            _forceVector = reboundDirection * reboundImpulse * mass;
            ApplyMotorImpulse(_forceVector);
            ApplyPhysicalTrauma(reboundDirection * reboundImpulse * mass, severity);

            _survivalSystem?.ReportPhysicalTrauma(impactSpeed, severity);

            if (requestTransportBailout)
                TriggerTransportBailout(severity, hitNormal, transportLifecycleOwner, bailoutImpulse);
        }

        private Vector3 ResolveTransportBailoutImpulse(Vector3 hitNormal, float severity)
        {
            Vector3 planarVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            planarVelocity.y = 0f;
            Vector3 lateralDirection = planarVelocity.sqrMagnitude > 0.0001f
                ? -NormalizeVectorRsqrt(planarVelocity, Vector3.zero)
                : ProjectOnPlaneFast(hitNormal, Vector3.up);

            ResolveDegreesSinCosFast(_bodyYaw, out float fallbackSinYaw, out float fallbackCosYaw);
            Vector3 fallbackLateralDirection = new Vector3(-fallbackSinYaw, 0f, -fallbackCosYaw);
            if (lateralDirection.sqrMagnitude <= 0.0001f)
            {
                lateralDirection = fallbackLateralDirection;
            }
            else
            {
                lateralDirection = NormalizeVectorRsqrt(lateralDirection, fallbackLateralDirection);
            }

            Vector3 bailoutImpulse = lateralDirection * (wipeoutBailoutImpulse * math.lerp(0.85f, 1.35f, severity));
            bailoutImpulse.y += wipeoutBailoutUpwardImpulse * math.lerp(0.75f, 1.2f, severity);
            return bailoutImpulse;
        }

        private void TriggerTransportBailout(
            float severity,
            Vector3 hitNormal,
            IPlayerTransportLifecycleOwner transportLifecycleOwner,
            Vector3 requestedBailoutImpulse)
        {
            Vector3 bailoutImpulse = requestedBailoutImpulse.sqrMagnitude > 0.0001f
                ? requestedBailoutImpulse
                : ResolveTransportBailoutImpulse(hitNormal, severity);

            ResolvePlayerToolManager();
            if (_playerToolManager != null &&
                _playerToolManager.CurrentTool != null &&
                transportLifecycleOwner != null &&
                ReferenceEquals(_playerToolManager.CurrentTool, transportLifecycleOwner))
            {
                if (transportLifecycleOwner is MantaScooter mantaScooter)
                    mantaScooter.TrySpawnEmergencyBailoutWreck(ResolveAuthoritativeLinearVelocity(Vector3.zero), bailoutImpulse, severity);

                _playerToolManager.Holster();
            }

            if (transportLifecycleOwner is ITransportEmergencyBailoutDriftSink bailoutDriftSink)
                bailoutDriftSink.TriggerEmergencyBailoutDrift(ResolveAuthoritativeLinearVelocity(Vector3.zero), severity);

            _transportBailoutCooldownTimer = wipeoutDuration + 0.35f;
            _impulseBypassTimer = math.max(_impulseBypassTimer, wipeoutImpulseBypassDuration);
            ApplyMotorImpulse(bailoutImpulse);
            TriggerBailoutDisorientation(severity, bailoutImpulse);
            PublishTransportBailoutSignal(severity, bailoutImpulse);
        }

        private Vector3 ResolveAbyssalAmbientFlowWithNoise()
        {
            Vector3 worldPosition = ResolvePlayerAupRuntimePosition();
            Vector3 horizontalBias = SampleAuthoredAmbientCurrent(worldPosition) + EffectiveWaterFlowVelocity;
            Unity.Mathematics.float3 phantomCurrent = CurrentManager.SampleCurrent(
                worldPosition,
                _currentTimer + _instanceId * 0.0131f,
                0.0042f,
                0.085f,
                1f,
                0.25f);
            horizontalBias.x += phantomCurrent.x;
            horizontalBias.z += phantomCurrent.z;
            horizontalBias += ResolveGpuAbyssalFlowVelocity(worldPosition);
            if (TryResolveVegetationBridge(out HectonMapMagicVegetationBridge bridge))
                horizontalBias = bridge.ApplyAbyssalFlowNoise(horizontalBias, worldPosition);

            return horizontalBias;
        }

        private Vector3 ResolveGpuAbyssalFlowVelocity(Vector3 worldPosition)
        {
            IAbyssalFlowGpuReadModel fluidGpu = _abyssalFlowGpuRuntime;
            if (fluidGpu == null ||
                !fluidGpu.TryGetGpuAbyssalFlowFieldBuffer(out _, out _, out _, out _) ||
                !fluidGpu.TrySampleModAbyssalFlow(worldPosition, out float3 flowVector) ||
                !math.all(math.isfinite(flowVector)))
            {
                return Vector3.zero;
            }

            return new Vector3(flowVector.x, flowVector.y, flowVector.z);
        }

        private void ApplyAbyssalFlowBoundaryTurbulence(Vector3 noisyFlow, float depthT, PlayerTransportPreset transportPreset)
        {
            Vector3 flowDelta = noisyFlow - _previousAbyssalNoisyFlow;
            _previousAbyssalNoisyFlow = noisyFlow;

            if (_abyssalFlowNoiseBoundaryCooldownTimer > 0f)
                return;

            float deltaSqr = flowDelta.sqrMagnitude;
            float boundaryThreshold = math.max(0f, abyssalFlowNoiseBoundaryThreshold);
            if (deltaSqr <= boundaryThreshold * boundaryThreshold)
                return;

            float inverseDeltaMagnitude = math.rsqrt(math.max(deltaSqr, 0.000001f));
            float deltaMagnitude = deltaSqr * inverseDeltaMagnitude;
            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float boundaryT = math.saturate(
                (deltaMagnitude - abyssalFlowNoiseBoundaryThreshold) /
                math.max(abyssalFlowNoiseBoundaryThreshold, 0.01f));
            Vector3 joltDirection = flowDelta * inverseDeltaMagnitude;
            Vector3 velocityChange = joltDirection * abyssalFlowNoiseBoundaryVelocityChange * boundaryT * depthT * transportInfluence;
            if (velocityChange.sqrMagnitude <= 0.0001f)
                return;

            ApplyMotorVelocityChange(velocityChange);
            ApplyAbyssalTransportTurbulenceTorque(joltDirection, boundaryT, depthT, transportPreset);
            QueueCameraCollisionImpulse(ApproximateVectorMagnitude(velocityChange) * 8f);

            float rollSign = math.sign(DotVector(joltDirection, _cachedTransform.right));
            if (rollSign == 0f)
                rollSign = 1f;

            QueueCameraExternalRollImpulse(rollSign * abyssalFlowNoiseBoundaryRollImpulse * boundaryT);

            _abyssalFlowNoiseBoundaryCooldownTimer = abyssalFlowNoiseBoundaryCooldown;
        }

        private void AdvanceAbyssalTransportTurbulenceSteering(float fixedDeltaTime)
        {
            float recoverySharpness = math.max(abyssalTransportTurbulenceRecoverySharpness, 0.01f);
            float blendT = ResolveLinearBlendT(recoverySharpness, fixedDeltaTime);
            _abyssalTransportTurbulencePitchOffset = math.lerp(_abyssalTransportTurbulencePitchOffset, 0f, blendT);
            _abyssalTransportTurbulenceYawOffset = math.lerp(_abyssalTransportTurbulenceYawOffset, 0f, blendT);
        }

        private void ApplyAbyssalTransportTurbulenceTorque(
            Vector3 joltDirection,
            float boundaryT,
            float depthT,
            PlayerTransportPreset transportPreset)
        {
            if (ResolveActiveTransportPropulsionReference(transportPreset) <= 0.01f)
                return;

            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float turbulenceT = boundaryT * depthT * transportInfluence;
            if (turbulenceT <= 0.0001f)
                return;

            Vector3 torqueAxis = CrossVector(_cachedTransform.forward, joltDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = CrossVector(_cachedTransform.up, joltDirection);
            if (torqueAxis.sqrMagnitude > 0.0001f)
            {
                torqueAxis = NormalizeVectorRsqrt(torqueAxis, Vector3.up);
                ApplyMotorAngularVelocityChange(
                    torqueAxis * abyssalTransportTurbulenceTorqueVelocityChange * turbulenceT,
                    _currentFixedDeltaTime > 0f
                        ? abyssalTransportTurbulenceTorqueVelocityChange / _currentFixedDeltaTime
                        : 0f,
                    _currentFixedDeltaTime);
            }

            Vector3 localJolt = _cachedTransform.InverseTransformDirection(joltDirection);
            _abyssalTransportTurbulencePitchOffset = math.clamp(
                _abyssalTransportTurbulencePitchOffset - localJolt.y * abyssalTransportTurbulencePitchDegrees * turbulenceT,
                -abyssalTransportTurbulencePitchDegrees,
                abyssalTransportTurbulencePitchDegrees);
            _abyssalTransportTurbulenceYawOffset = math.clamp(
                _abyssalTransportTurbulenceYawOffset + localJolt.x * abyssalTransportTurbulenceYawDegrees * turbulenceT,
                -abyssalTransportTurbulenceYawDegrees,
                abyssalTransportTurbulenceYawDegrees);
        }

        private Vector3 ResolveAbyssalDowndraftDirection(Vector3 noisyFlow)
        {
            Vector3 horizontalBias = noisyFlow;
            horizontalBias.y = 0f;

            if (horizontalBias.sqrMagnitude <= 0.0001f || abyssalDowndraftHorizontalBias <= 0f)
                return Vector3.down;

            horizontalBias = NormalizeVectorRsqrt(horizontalBias, Vector3.zero);
            Vector3 downdraftDirection = Vector3.down + horizontalBias * abyssalDowndraftHorizontalBias;
            return downdraftDirection.sqrMagnitude > 0.0001f
                ? NormalizeVectorRsqrt(downdraftDirection, Vector3.down)
                : Vector3.down;
        }

        private float ResolveAbyssalDowndraftInterval(float depthT)
        {
            float baseInterval = math.lerp(abyssalDowndraftIntervalMax, abyssalDowndraftIntervalMin, depthT);
            float phase = math.frac(_currentTimer * 0.173f + _instanceId * 0.00017f);
            return math.lerp(baseInterval * 0.82f, baseInterval * 1.18f, phase);
        }

        private void TriggerBailoutDisorientation(float severity, Vector3 bailoutImpulse)
        {
            float rollSign = math.sign(DotVector(bailoutImpulse, _cachedTransform.right));
            if (rollSign == 0f)
                rollSign = 1f;

            QueueCameraExternalRollImpulse(
                rollSign * wipeoutBailoutRollImpulse * math.lerp(0.7f, 1f, severity));

            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float distortionIntensity = wipeoutBailoutVisorDistortion * math.lerp(0.72f, 1f, severity);
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                {
                    controller.TriggerEnvironmentalDistortion(
                        distortionIntensity,
                        wipeoutBailoutDisorientationDuration,
                        wipeoutBailoutVisorRecovery);
                }
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private float ResolveWipeoutTransportControl01()
        {
            if (_wipeoutTimer <= 0f && _fatalPressureSequenceTimer <= 0f)
                return 1f;

            return 0f;
        }

        private void StartFatalPressureSequence()
        {
            if (_fatalPressureSequenceTimer > 0f)
                return;

            _fatalPressureSequenceTimer = math.max(0.01f, fatalPressureSequenceDuration);
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0.01f;
            _fatalPressureLookYawAnchor = _cameraYaw;
            _fatalPressureLookPitchAnchor = _cameraPitch;
            _jumpRequested = false;
            _jumpBufferTimer = 0f;

            float corruptionIntensity = ResolveFatalPressureCorruptionIntensity(_fatalPressureSequenceIntensity);
            ApplyFatalPressureVisorCorruption(corruptionIntensity);
            PushFatalPressureCorruptionWarning();
        }

        private float ResolveFatalPressureCorruptionIntensity(float sequenceIntensity)
        {
            ILocalizationStressPresentationReadModel localization = _localizationRuntime;
            float localizationIntensity = localization != null
                ? localization.GetHullStressCorruptionIntensity()
                : 0f;
            return math.saturate(math.max(localizationIntensity, sequenceIntensity));
        }

        private void ApplyFatalPressureVisorCorruption(float corruptionIntensity)
        {
            float clampedIntensity = math.saturate(corruptionIntensity);
            if (clampedIntensity <= 0f)
                return;

            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float holdDuration = math.lerp(0.08f, fatalPressureGlitchDurationMax, clampedIntensity);
            float recoverySpeed = math.lerp(6f, 1.4f, clampedIntensity);
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                    controller.TriggerEnvironmentalDistortion(clampedIntensity, holdDuration, recoverySpeed);
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private void PushFatalPressureCorruptionWarning()
        {
            ILocalizationStressPresentationReadModel localization = _localizationRuntime;
            ReadOnlySpan<char> message = localization != null
                ? localization.GetRawSpanOrFallback(
                    LocHash.Compute(LocalizationKeys.HUD_STATUS_PRESSURE_LIMIT_EXCEEDED),
                    "PRESSURE LIMIT EXCEEDED".AsSpan())
                : "PRESSURE LIMIT EXCEEDED".AsSpan();

            TryPushFatalPressureNotification(message);
        }

        private void TryPushFatalPressureNotification(ReadOnlySpan<char> message)
        {
            if (NotificationEvents.TryPushCritical(message))
                return;

            ReportFatalPressureNotificationMiss();
        }

        private void ReportFatalPressureNotificationMiss()
        {
            _fatalPressureNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _fatalPressureNotificationMissWarningHash,
                _fatalPressureNotificationContextHash,
                math.max(1, _fatalPressureNotificationMissCount));
        }

        private void ClearFatalPressureNotificationDiagnostics()
        {
            _fatalPressureNotificationMissCount = 0;
        }

        private void TriggerFatalPressureImplosion()
        {
            _fatalPressureRearmTimer = math.max(wipeoutDuration, fatalPressureSequenceDuration);
            Vector3 runtimePosition = ResolvePlayerAupRuntimePosition();
            FatalPressureImplosionEvent implosionEvent = new FatalPressureImplosionEvent(
                _fatalPressureImplosionSourceHash,
                -1,
                0f,
                runtimePosition);
            FatalPressureImplosionEvents.TryNotify(in implosionEvent);

            IPlayerTransportLifecycleOwner transportLifecycleOwner = null;
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive())
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out transportLifecycleOwner);

            StartWipeout(
                1f,
                ResolveBailoutSpeed(),
                runtimePosition,
                Vector3.up,
                transportLifecycleOwner,
                transportLifecycleOwner != null,
                Vector3.zero);
        }

        private void PulseFatalPressureGlitch(float intensity)
        {
            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float glitchDuration = math.lerp(fatalPressureGlitchDurationMin, fatalPressureGlitchDurationMax, math.saturate(intensity));
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                    controller.GlitchPulse(glitchDuration);
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private void UpdateVegetationDensityLinearDamping(float fixedDeltaTime)
        {
            float targetDamping = 0f;
            if (!IsInDryInterior())
            {
                float density = 0f;
                bool isSargassum = false;
                if (TryResolveVegetationBridge(out HectonMapMagicVegetationBridge bridge))
                {
                    HectonMapMagicVegetationBridge.VegetationDensitySample sample = bridge.GetVegetationDensity(ResolvePlayerAupRuntimePosition());
                    density = sample.Density;
                    isSargassum = sample.AcousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles;
                }
                else if (HectonMapMagicVegetationBridge.GlobalVegetationAcousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles)
                {
                    density = HectonMapMagicVegetationBridge.GlobalVegetationAudioDensity;
                    isSargassum = true;
                }

                if (isSargassum && density > vegetationDensityDragThreshold)
                {
                    float densityT = math.saturate(
                        (density - vegetationDensityDragThreshold) /
                        math.max(1f - vegetationDensityDragThreshold, 0.01f));
                    targetDamping = vegetationDensityLinearDampingMax * densityT;
                }
            }

            float blendT = ResolveLinearBlendT(math.max(vegetationDensityLinearDampingBlendSharpness, 0.01f), fixedDeltaTime);
            _vegetationDensityLinearDamping = math.lerp(_vegetationDensityLinearDamping, targetDamping, blendT);
        }

        private bool TryResolveVegetationBridge(out HectonMapMagicVegetationBridge bridge)
        {
            bridge = vegetationDensityBridge;
            if (bridge != null)
                return true;

            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationDensityBridge))
                return false;

            bridge = vegetationDensityBridge;
            return bridge != null;
        }

        private void ApplyWipeoutRecoveryForces(float fixedDeltaTime)
        {
            if (_wipeoutTimer <= 0f)
                return;

            _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float speedSq = _velocity.sqrMagnitude;
            if (speedSq <= 0.0001f)
                return;

            float speed = ApproximateVectorMagnitude(_velocity);
            float dampingScale = wipeoutRecoveryDrag * math.lerp(0.75f, 1.25f, _wipeoutSeverity);
            float mass = ResolveAuthoritativeBodyMassKg();
            float dampingForceMagnitude = dampingScale * speed * mass;
            float maxDampingForce = speed * mass * 0.9f / math.max(fixedDeltaTime, 0.0001f);
            if (dampingForceMagnitude > maxDampingForce)
                dampingForceMagnitude = maxDampingForce;

            float invSpeed = math.rsqrt(math.max(speedSq, 0.000001f));
            _forceVector.x = -_velocity.x * invSpeed * dampingForceMagnitude;
            _forceVector.y = -_velocity.y * invSpeed * dampingForceMagnitude;
            _forceVector.z = -_velocity.z * invSpeed * dampingForceMagnitude;
            ApplyMotorForce(_forceVector);
        }

        private void ApplyHighSpeedWipeoutSweep(float fixedDeltaTime, bool applyToMotor)
        {
            if (_playerMotor == null || !applyToMotor || fixedDeltaTime <= 0f || !math.isfinite(fixedDeltaTime))
                return;
        }

        private float ResolveTransportCavitationEfficiency(
            float fixedDeltaTime,
            bool hasTransportPropulsion,
            float forwardVelocity,
            float transportBoost01)
        {
            float targetEfficiency = 1f;
            if (hasTransportPropulsion && _currentDepth < transportCavitationRecoveryDepth)
            {
                float forwardAcceleration = (forwardVelocity - _previousTransportForwardVelocity) / math.max(fixedDeltaTime, 0.0001f);
                float accelerationT = math.saturate(
                    (forwardAcceleration - transportCavitationAccelerationStart) /
                    math.max(transportCavitationAccelerationMax - transportCavitationAccelerationStart, 0.01f));
                float depthT = 1f - math.saturate(
                    (_currentDepth - transportCavitationStartDepth) /
                    math.max(transportCavitationRecoveryDepth - transportCavitationStartDepth, 0.01f));
                float demandT = math.max(transportBoost01, math.saturate(_inputV));
                float lossT = depthT * accelerationT * demandT;
                targetEfficiency = math.lerp(1f, transportCavitationMinEfficiency, lossT);
            }

            float blendT = ResolveLinearBlendT(math.max(transportCavitationBlendSharpness, 0.01f), fixedDeltaTime);
            _transportCavitationEfficiency = math.lerp(_transportCavitationEfficiency, targetEfficiency, blendT);
            _previousTransportForwardVelocity = forwardVelocity;
            return _transportCavitationEfficiency;
        }

        private void UpdateWetLensSignal(float fixedDeltaTime)
        {
            if (_wetLensPulseCooldownTimer > 0f)
            {
                _wetLensPulseCooldownTimer -= fixedDeltaTime;
                if (_wetLensPulseCooldownTimer < 0f)
                    _wetLensPulseCooldownTimer = 0f;
            }

            float recoveryT = ResolveLinearBlendT(math.max(wetLensSignalRecoverySpeed, 0.01f), fixedDeltaTime);
            _wetLensSignalIntensity = math.lerp(_wetLensSignalIntensity, 0f, recoveryT);

            if (IsInDryInterior() || !_isSurfaceSwimming)
                return;

            float cameraY = playerCamera != null ? playerCamera.position.y : GetBodyEyeY();
            float coverDepth = EffectiveWaterSurfaceY - cameraY;
            if (coverDepth <= wetLensWaveCoverDepth || _dynamicStormIntensity < wetLensStormIntensityThreshold)
                return;

            float stormRange = math.max(1f - wetLensStormIntensityThreshold, 0.01f);
            float stormT = math.saturate((_dynamicStormIntensity - wetLensStormIntensityThreshold) / stormRange);
            float coverT = math.saturate((coverDepth - wetLensWaveCoverDepth) / math.max(wetLensWaveCoverDepth, 0.01f));
            EmitWetLensPulse(wetLensStormPulseIntensity * math.max(stormT, coverT), wetLensStormPulseCooldown);
        }

        private void EmitWetLensPulse(float intensity, float cooldown)
        {
            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity <= 0f)
                return;

            if (_wetLensPulseCooldownTimer > 0f && clampedIntensity <= _wetLensSignalIntensity)
                return;

            if (_wetLensSignalIntensity < clampedIntensity)
                _wetLensSignalIntensity = clampedIntensity;

            if (_wetLensPulseCooldownTimer < cooldown)
                _wetLensPulseCooldownTimer = cooldown;

            PublishWetLensDropletSignal(clampedIntensity);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            angle = math.fmod(angle + 180f, 360f);
            if (angle < 0f)
                angle += 360f;

            return angle - 180f;
        }

        private static float DeltaAngleDegrees(float current, float target)
        {
            return NormalizeSignedAngle(target - current);
        }

        private static float LerpAngleDegrees(float current, float target, float t)
        {
            return NormalizeSignedAngle(current + (DeltaAngleDegrees(current, target) * math.saturate(t)));
        }

        private static int RoundToIntNoMathf(float value)
        {
            return (int)math.round(value);
        }

        private float ResolveCameraLookDownAngle()
        {
            if (playerCamera != null)
            {
                float downwardComponent = math.clamp(-playerCamera.forward.y, 0f, 1f);
                return FastAsinDegrees(downwardComponent);
            }

            return math.max(0f, _cameraPitch);
        }

        private void RefreshSurfaceBreachLock(float physicsImmersion)
        {
            if (IsInDryInterior() || _isGrounded || physicsImmersion <= 0.01f)
                return;

            if (ResolveAuthoritativeLinearVelocity(Vector3.zero).y < surfaceBreachReleaseVelocity)
                return;

            float headSurfaceOffset = GetHeadSurfaceOffset(EffectiveWaterSurfaceY);
            if (headSurfaceOffset < -surfaceBreachDepthWindow || headSurfaceOffset > surfaceBreachDepthWindow)
                return;

            if (_surfaceBreachLockTimer < surfaceBreachLockDuration)
                _surfaceBreachLockTimer = surfaceBreachLockDuration;
        }

        private void AdvanceSurfaceBreachArcTimers(float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            if (safeDeltaTime <= 0f)
                return;

            if (_surfaceBreachFluidDragBypassTimer > 0f)
            {
                _surfaceBreachFluidDragBypassTimer -= safeDeltaTime;
                if (_surfaceBreachFluidDragBypassTimer < 0f)
                    _surfaceBreachFluidDragBypassTimer = 0f;
            }

            _waterTransitionHandler?.AdvanceSurfaceBreachGravity(safeDeltaTime, _cachedGravity, _cachedGravityMagnitude);
        }

        private void TryStartSurfaceBreachArc(float previousWaterImmersionRatio, float surfacePierceVerticalVelocity)
        {
            if (IsInDryInterior() || previousWaterImmersionRatio <= 0.01f)
                return;

            float upwardSpeed = math.max(0f, surfacePierceVerticalVelocity);
            if (upwardSpeed < surfaceBreachArcVelocity)
                return;

            Vector3 bodyPosition = ResolveBodyRuntimePosition();
            float surfaceY = EffectiveWaterSurfaceY;
            if (bodyPosition.y < surfaceY)
                return;

            ConfigureWaterTransitionHandler();
            if (_waterTransitionHandler != null && _waterTransitionHandler.HasPendingSurfaceBreachGravity)
                return;

            _surfaceBreachFluidDragBypassTimer = math.max(
                _surfaceBreachFluidDragBypassTimer,
                surfaceBreachFluidDragBypassDuration);

            _surfaceBreachLockTimer = math.max(_surfaceBreachLockTimer, surfaceBreachLockDuration);
            _recentBreachExitTimer = math.max(_recentBreachExitTimer, wipeoutBreachLandingGraceTime);

            PublishWaterTransitionSignal(WaterTransitionKind.SurfaceExit, false, 1f, surfaceY, upwardSpeed);
            PublishSurfaceBreachSplash(upwardSpeed, surfaceY);
        }

        private void PublishSurfaceBreachSplash(float upwardSpeed, float surfaceY)
        {
            Vector3 splashPosition = ResolvePlayerAupRuntimePosition();
            splashPosition.y = surfaceY;
            float mass = ResolveAuthoritativeBodyMassKg();
            float kineticEnergy = 0.5f * mass * upwardSpeed * upwardSpeed * math.max(1f, surfaceBreachSplashEnergyScale);
            if (TryResolveAupFromPlayerStateRuntimeDelta(splashPosition, out AbsoluteUniversePosition splashAup))
            {
                double3 absoluteUniversePosition = splashAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(absoluteUniversePosition)))
                {
                    SplashEvent splashEvent = new SplashEvent
                    {
                        RuntimePosition = new float3(splashPosition.x, splashPosition.y, splashPosition.z),
                        AbsoluteUniversePosition = new float3(
                            (float)absoluteUniversePosition.x,
                            (float)absoluteUniversePosition.y,
                            (float)absoluteUniversePosition.z),
                        SurfaceNormal = new float3(0f, 1f, 0f),
                        ImpactSpeedMetersPerSecond = upwardSpeed,
                        KineticEnergyJoules = kineticEnergy,
                        SubmersionFactor = 1f,
                        SampleIndex = -1
                    };

                    SignalBus<SplashEvent>.TryPushTracked(in splashEvent, ref _signalPushDropCount);
                }
            }

            PublishPlayerWaterSplashSignal(1f, true, surfaceY, upwardSpeed);
            if (_juiceProcessor != null)
                _juiceProcessor.RegisterSplash(1f, currentSuitData);

            EmitBreachImpactFeedback(1f);
            EmitBreachSplashRing(1f);
            EmitWetLensPulse(math.max(wetLensBreachPulseIntensity, 1f), wetLensStormPulseCooldown);
        }

        private void PublishPlayerFootstepSignal()
        {
            PlayerFootstepSignal signal = new PlayerFootstepSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Intensity01 = 1f,
                Flags = (byte)(_isGrounded ? 1 : 0)
            };
            SignalBus<PlayerFootstepSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishPlayerWaterSplashSignal(
            float intensity,
            bool isSubmerged,
            float surfaceY,
            float verticalSpeed)
        {
            float safeIntensity = math.isfinite(intensity) ? math.saturate(intensity) : 0f;
            if (safeIntensity <= 0f)
                return;

            PlayerWaterSplashSignal signal = new PlayerWaterSplashSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Intensity01 = safeIntensity,
                SurfaceY = TryResolveWaterSurfaceY(surfaceY, out float resolvedSurfaceY)
                    ? resolvedSurfaceY
                    : DefaultWaterSurfaceY,
                VerticalSpeed = math.isfinite(verticalSpeed) ? math.max(0f, verticalSpeed) : 0f,
                IsSubmerged = (byte)(isSubmerged ? 1 : 0),
                Flags = 0
            };
            SignalBus<PlayerWaterSplashSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishPlayerExhaleSignal()
        {
            PlayerExhaleSignal signal = new PlayerExhaleSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Flags = (byte)(_isSurfaceSwimming || IsPlayerSubmerged ? 1 : 0)
            };
            SignalBus<PlayerExhaleSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishPlayerSprintStateSignal(bool isSprinting)
        {
            PlayerSprintStateSignal signal = new PlayerSprintStateSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                IsSprinting = (byte)(isSprinting ? 1 : 0),
                Flags = 0
            };
            SignalBus<PlayerSprintStateSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishFatalPressureSignal(float intensity)
        {
            PlayerFatalPressureSignal signal = new PlayerFatalPressureSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Intensity01 = math.isfinite(intensity) ? math.saturate(intensity) : 0f,
                Flags = 0
            };
            SignalBus<PlayerFatalPressureSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishTransportBailoutSignal(float severity, Vector3 bailoutImpulse)
        {
            float3 safeImpulse = math.all(math.isfinite(new float3(bailoutImpulse.x, bailoutImpulse.y, bailoutImpulse.z)))
                ? new float3(bailoutImpulse.x, bailoutImpulse.y, bailoutImpulse.z)
                : float3.zero;
            PlayerTransportBailoutSignal signal = new PlayerTransportBailoutSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Severity01 = math.isfinite(severity) ? math.saturate(severity) : 0f,
                WorldImpulse = safeImpulse,
                Flags = 0
            };
            SignalBus<PlayerTransportBailoutSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishWetLensDropletSignal(float intensity)
        {
            float safeIntensity = math.isfinite(intensity) ? math.saturate(intensity) : 0f;
            if (safeIntensity <= 0f)
                return;

            if (!IsFiniteAup(in _playerState.AbsolutePosition))
                return;

            VisorDropletSignal signal = new VisorDropletSignal
            {
                PositionAup = _playerState.AbsolutePosition,
                Intensity01 = safeIntensity,
                DurationSeconds = math.max(0.1f, wetLensStormPulseCooldown),
                SourceHash = PlayerSignalSourceId,
                DropletKind = VisorDropletSignal.DropletKindMassiveSplash,
                Flags = VisorDropletSignal.FlagExternalSplash,
                Sequence = unchecked((ushort)SystemDispatcher.CurrentFrameIndex)
            };
            SignalBus<VisorDropletSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PublishWaterTransitionSignal(
            WaterTransitionKind kind,
            bool isSubmerged,
            float intensity,
            float surfaceY,
            float verticalSpeed)
        {
            Vector3 runtimePosition = ResolvePlayerAupRuntimePosition();
            Vector3 safeRuntimePosition = HectonPlayerMotor.SafeVelocity(runtimePosition);
            if (!TryResolveAupFromPlayerStateRuntimeDelta(safeRuntimePosition, out AbsoluteUniversePosition transitionAup))
                return;

            WaterTransitionSignal signal = new WaterTransitionSignal
            {
                SourceId = PlayerSignalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                Intensity01 = math.saturate(intensity),
                SurfaceY = surfaceY,
                VerticalSpeed = math.max(0f, verticalSpeed),
                Kind = (byte)kind,
                IsSubmerged = isSubmerged ? (byte)1 : (byte)0,
                RuntimePosition = new float3(safeRuntimePosition.x, safeRuntimePosition.y, safeRuntimePosition.z),
                AbsolutePosition = ToPlayerPresentationAup48(in transitionAup)
            };
            SignalBus<WaterTransitionSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private static PlayerPresentationAup48 ToPlayerPresentationAup48(in AbsoluteUniversePosition aup)
        {
            return new PlayerPresentationAup48
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ
            };
        }

        private void TryStartWaterEntryImpact(float previousWaterImmersionRatio, bool wasGroundedLastFixedTick, float entryVerticalVelocity)
        {
            if (IsInDryInterior() || wasGroundedLastFixedTick || previousWaterImmersionRatio > 0.01f || _waterImmersionRatio <= 0.01f)
                return;

            if (_surfaceBreachLockTimer > 0f && entryVerticalVelocity < 0f)
                _surfaceBreachLockTimer = 0f;

            if (_currentLocomotionMode != PlayerLocomotionMode.SurfaceSwim &&
                _currentLocomotionMode != PlayerLocomotionMode.UnderwaterSwim)
                return;

            float downwardEntrySpeed = math.max(0f, -entryVerticalVelocity);
            if (downwardEntrySpeed < waterEntryImpactMinSpeed)
                return;

            float impactRange = math.max(waterEntryImpactMinSpeed, 0.01f);
            float impactT = math.saturate((downwardEntrySpeed - waterEntryImpactMinSpeed) / impactRange);
            float impactDamping = math.lerp(waterEntryImpactDamping * 0.45f, waterEntryImpactDamping, impactT);

            if (_waterEntryImpactTimer < waterEntryImpactDuration)
                _waterEntryImpactTimer = waterEntryImpactDuration;

            if (_waterEntryImpactStrength < impactDamping)
                _waterEntryImpactStrength = impactDamping;

            float impactFovScale = math.lerp(0.35f, 1f, impactT);
            QueueWaterEntryFovImpulse(
                waterEntryImpactFovExpand * impactFovScale,
                waterEntryImpactFovCompress * impactFovScale,
                waterEntryImpactDuration);

            if (_recentBreachExitTimer > 0f)
                EmitBreachSplashRing(impactT);
        }

        private void TryPlaySurfacePierceSplashAudio(float previousWaterImmersionRatio, float surfacePierceVerticalVelocity)
        {
            if (IsInDryInterior())
                return;

            bool enteredWater = previousWaterImmersionRatio <= 0.01f && _waterImmersionRatio > 0.01f;
            bool exitedWater = previousWaterImmersionRatio > 0.01f && _currentDepth <= 0f && surfacePierceVerticalVelocity > 0f;
            if (!enteredWater && !exitedWater)
                return;

            float verticalSpeed = math.abs(surfacePierceVerticalVelocity);
            if (verticalSpeed < surfacePierceSplashMinSpeed)
                return;

            if (exitedWater)
                _recentBreachExitTimer = math.max(_recentBreachExitTimer, wipeoutBreachLandingGraceTime);

            float clampedMaxSpeed = math.max(surfacePierceSplashMaxSpeed, surfacePierceSplashMinSpeed + 0.01f);
            float speedT = math.saturate((verticalSpeed - surfacePierceSplashMinSpeed) / (clampedMaxSpeed - surfacePierceSplashMinSpeed));
            if (exitedWater)
                EmitBreachImpactFeedback(math.lerp(0.45f, 1f, speedT));

            float volume = math.lerp(surfacePierceSplashMinVolume, surfacePierceSplashMaxVolume, speedT);
            PublishPlayerWaterSplashSignal(volume, enteredWater, EffectiveWaterSurfaceY, verticalSpeed);

            if (exitedWater)
            {
                EmitWetLensPulse(math.max(wetLensBreachPulseIntensity, volume), wetLensStormPulseCooldown);
            }
        }

        private void EmitWipeoutImpactFeedback(float severity)
        {
            if (_waterImmersionRatio <= 0.01f && _currentDepth <= 0f)
                return;

            EmitImpactBubbleBurst(wipeoutBubbleParticles, severity);
            PlayUnderwaterImpactOneShot(severity);
        }

        private void EmitBreachImpactFeedback(float intensity)
        {
            EmitImpactBubbleBurst(breachBubbleParticles, intensity);
            PlayUnderwaterImpactOneShot(intensity * 0.88f);
        }

        private void EmitImpactBubbleBurst(ParticleSystem bubbleParticles, float intensity)
        {
            if (bubbleParticles == null)
                return;

            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity < impactBubbleMinIntensity)
                return;

            int bubbleCount = RoundToIntNoMathf(math.lerp(impactBubbleMinCount, impactBubbleMaxCount, clampedIntensity));
            if (bubbleCount <= 0)
                return;

            QueueImpactBubbleBurst(bubbleParticles, bubbleCount);
        }

        private void PlayUnderwaterImpactOneShot(float intensity)
        {
            if (underwaterImpactClip == null)
                return;

            float clampedIntensity = math.saturate(intensity);
            float volume = math.lerp(underwaterImpactMinVolume, underwaterImpactMaxVolume, clampedIntensity);
            float pitch = math.lerp(0.94f, 0.78f, clampedIntensity);
            QueuePresentationAudioAtPoint(underwaterImpactClip, ResolvePlayerAupRuntimePosition(), volume, pitch);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SUIT APPLICATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void UpdateHeadSurfaceRecovery(float fixedDeltaTime)
        {
            if (IsInDryInterior())
            {
                _surfaceGaspUnderwaterTimer = 0f;
                _surfaceGaspSubmergedLatch = false;
                return;
            }

            float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
            bool headSubmerged =
                headDepth >= surfaceGaspHeadEnterDepth ||
                (_surfaceGaspSubmergedLatch && headDepth > surfaceGaspHeadExitDepth);

            if (headSubmerged)
            {
                _surfaceGaspSubmergedLatch = true;
                _surfaceGaspUnderwaterTimer += fixedDeltaTime;
                return;
            }

            if (_surfaceGaspSubmergedLatch &&
                _surfaceGaspUnderwaterTimer >= surfaceGaspMinUnderwaterTime &&
                _surfaceGaspCooldownTimer <= 0f)
            {
                EmitSurfaceGasp();
                _surfaceGaspCooldownTimer = surfaceGaspCooldown;
            }

            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspSubmergedLatch = false;
        }

        private void EmitSurfaceGasp()
        {
            QueuePresentationAudioStatic2D(surfaceGaspClip, surfaceGaspVolume);
            QueueWaterEntryFovImpulse(
                surfaceGaspFovExpand,
                surfaceGaspFovCompress,
                surfaceGaspFovDuration);
        }

        private void EmitBreachSplashRing(float intensity)
        {
            if (breachSplashRingParticles == null || intensity < breachSplashRingMinIntensity)
                return;

            Vector3 ringPosition = ResolvePlayerAupRuntimePosition();
            ringPosition.y = EffectiveWaterSurfaceY;
            _pendingBreachSplashRingPosition = ringPosition;
            _pendingBreachSplashRingDirty = true;
            MarkLateFramePresentationDirty();
        }

        private void QueueImpactBubbleBurst(ParticleSystem bubbleParticles, int bubbleCount)
        {
            if (bubbleCount <= 0)
                return;

            if (ReferenceEquals(bubbleParticles, breachBubbleParticles))
            {
                _pendingBreachBubbleCount = math.min(
                    MaxPendingImpactBubbleCount,
                    _pendingBreachBubbleCount + bubbleCount);
                MarkLateFramePresentationDirty();
            }
            else if (ReferenceEquals(bubbleParticles, wipeoutBubbleParticles))
            {
                _pendingWipeoutBubbleCount = math.min(
                    MaxPendingImpactBubbleCount,
                    _pendingWipeoutBubbleCount + bubbleCount);
                MarkLateFramePresentationDirty();
            }
        }

        private void FlushImpactBubbleBursts()
        {
            int breachCount = _pendingBreachBubbleCount;
            int wipeoutCount = _pendingWipeoutBubbleCount;
            _pendingBreachBubbleCount = 0;
            _pendingWipeoutBubbleCount = 0;

            if (breachCount > 0 && breachBubbleParticles != null)
                breachBubbleParticles.Emit(breachCount);

            if (wipeoutCount > 0 && wipeoutBubbleParticles != null)
                wipeoutBubbleParticles.Emit(wipeoutCount);
        }

        private void FlushBreachSplashRing()
        {
            if (!_pendingBreachSplashRingDirty)
                return;

            _pendingBreachSplashRingDirty = false;
            if (breachSplashRingParticles == null)
                return;

            Transform ringTransform = breachSplashRingParticles.transform;
            ringTransform.position = _pendingBreachSplashRingPosition;
            ringTransform.rotation = Quaternion.identity;
            breachSplashRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            breachSplashRingParticles.Play(true);
        }

        private void ApplySuitToRigidbody()
        {
            if (currentSuitData == null) return;
            CacheAuthoritativeBodyMassKg(currentSuitData.mass);
            _rb.mass = currentSuitData.mass;
            _rb.useGravity = false;
            if (_rb.linearDamping != 0f)
                _rb.linearDamping = 0f;

            if (_isWalking)
            {
                _currentLinearDamping = IsInDryInterior() ? 0f : currentSuitData.walkDrag;
            }
            else
            {
                _currentLinearDamping = 0f;
            }
        }

        private void ApplyModePhysics(SuitData suit)
        {
            if (!_isWalking || IsInDryInterior())
            {
                _currentLinearDamping = 0f;
            }
        }

        private void SmoothDampingTransition(float fixedDeltaTime, SuitData suit)
        {
            float targetDamping;
            if (_isWalking)
            {
                if (IsInDryInterior())
                {
                    targetDamping = 0f;
                }
                else
                {
                    float wadeFactor = 1f + _waterImmersionRatio * suit.wadeSlowdownFactor;
                    targetDamping = suit.walkDrag * wadeFactor;

                    if (IsDryLandAirborne())
                        targetDamping *= dryAirDampingMultiplier;
                }
            }
            else
            {
                targetDamping = 0f;
            }

            bool waterEntryImpactActive = _waterEntryImpactTimer > 0f && _waterEntryImpactStrength > 0f;
            if (waterEntryImpactActive)
            {
                float normalizedImpactTime = math.saturate(_waterEntryImpactTimer / math.max(waterEntryImpactDuration, 0.01f));
                float impactReleaseT = normalizedImpactTime * normalizedImpactTime * (3f - 2f * normalizedImpactTime);
                targetDamping += _waterEntryImpactStrength * impactReleaseT;
            }

            float dampingTransitionSpeed = suit.dampingTransitionSpeed;
            if (waterEntryImpactActive)
            {
                bool impactRampUp = targetDamping > _currentLinearDamping;
                dampingTransitionSpeed = math.max(dampingTransitionSpeed, impactRampUp ? 30f : 12f);
            }

            targetDamping += _vegetationDensityLinearDamping;

            if (math.abs(_currentLinearDamping - targetDamping) > 0.01f)
            {
                float t = ResolveLinearBlendT(dampingTransitionSpeed, fixedDeltaTime);
                _currentLinearDamping = math.lerp(_currentLinearDamping, targetDamping, t);
            }
            else if (_currentLinearDamping != targetDamping)
            {
                _currentLinearDamping = targetDamping;
            }
        }

        private void ApplyProceduralLinearDamping(float fixedDeltaTime)
        {
            if (_rb == null || fixedDeltaTime <= 0f || _currentLinearDamping <= 0.0001f)
                return;

            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float denominator = math.max(1f + _currentLinearDamping * fixedDeltaTime, 0.001f);
            Vector3 dampedVelocity = currentVelocity / denominator;
            ApplyMotorLinearVelocity(HectonPlayerMotor.SafeVelocity(dampedVelocity, currentVelocity));
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  GROUND DETECTION + SMOOTHED NORMAL
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void GroundCheck()
        {
            GroundCheck(_currentFixedDeltaTime);
        }

        private void FindBestStandardGroundHit(float requiredGroundNormalY, float maxGroundDistance, ref float bestDistance, ref float bestNormalY)
        {
            for (int i = 0; i < _fixedGroundSweepHitCount; i++)
            {
                PlayerMovementSurfaceHit hit = _groundProbeHitBuffer[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _playerColliderInstanceId)
                    continue;

                if (hit.distance > maxGroundDistance)
                    continue;

                float normalY = hit.normal.y;
                if (normalY < requiredGroundNormalY)
                    continue;

                if (!_isGrounded || hit.distance < bestDistance || (math.abs(hit.distance - bestDistance) <= 0.001f && normalY > bestNormalY))
                {
                    _groundHit = hit;
                    bestDistance = hit.distance;
                    bestNormalY = normalY;
                    _isGrounded = true;
                }
            }
        }

        private void ResolveSpecialFooting(bool allowExosuitFootSlopeProbe, bool dryInteriorActive, float requiredGroundNormalY, ref float bestDistance, ref float bestNormalY, ref Vector3 resolvedGroundNormal)
        {
            _exosuitFootingValid = false;
            _exosuitFootingNormal = Vector3.up;
            if (allowExosuitFootSlopeProbe &&
                TryResolveExosuitFootSlope(requiredGroundNormalY, out PlayerMovementSurfaceHit exosuitSupportHit, out Vector3 exosuitSupportNormal))
            {
                resolvedGroundNormal = exosuitSupportNormal;
                _exosuitFootingNormal = exosuitSupportNormal;
                _exosuitFootingValid = true;

                if (!_isGrounded ||
                    exosuitSupportHit.distance < bestDistance ||
                    (math.abs(exosuitSupportHit.distance - bestDistance) <= 0.001f && exosuitSupportNormal.y > bestNormalY))
                {
                    _groundHit = exosuitSupportHit;
                    bestDistance = exosuitSupportHit.distance;
                    bestNormalY = exosuitSupportNormal.y;
                    _isGrounded = true;
                }
            }
            else if (dryInteriorActive &&
                TryResolveDryInteriorFootSlope(requiredGroundNormalY, out PlayerMovementSurfaceHit dryInteriorSupportHit, out Vector3 dryInteriorSupportNormal))
            {
                resolvedGroundNormal = dryInteriorSupportNormal;
                if (!_isGrounded ||
                    dryInteriorSupportHit.distance < bestDistance ||
                    (math.abs(dryInteriorSupportHit.distance - bestDistance) <= 0.001f && dryInteriorSupportNormal.y > bestNormalY))
                {
                    _groundHit = dryInteriorSupportHit;
                    bestDistance = dryInteriorSupportHit.distance;
                    bestNormalY = dryInteriorSupportNormal.y;
                    _isGrounded = true;
                }
            }
        }

        private void GroundCheck(float fixedDeltaTime)
        {
            bool exosuitActive = IsExosuitTransportActive();
            bool exosuitKinematicAuthority = exosuitActive && ExosuitKinematicAuthority.HasActiveAuthority();
            bool dryInteriorActive = IsInDryInterior();
            bool allowExosuitFootSlopeProbe = exosuitActive && !exosuitKinematicAuthority && ShouldRunExosuitFootProbes();
            float requiredGroundNormalY = exosuitActive
                ? math.min(_minGroundNormalY, exosuitMinGroundNormalY)
                : _minGroundNormalY;
            _groundCheckOrigin.x = _fixedFrameBodyPosition.x;
            _groundCheckOrigin.y = _fixedFrameBodyBottomY + groundCheckRadius + GroundCheckSkin;
            _groundCheckOrigin.z = _fixedFrameBodyPosition.z;

            bool speculativeHoverAllowed = _aupSpeculativeHoverTicks > 0 && _isGrounded;
            Vector3 speculativeHoverNormal = _smoothedGroundNormal;
            float speculativeHoverHeight = speculativeHoverAllowed
                ? math.max(
                    GroundCheckSkin,
                    ResolveSpeculativeHoverHeightMeters(
                        math.max(_aupSpeculativeHoverHeightMeters, SpeculativeHoverBaseHeightMeters),
                        _currentTimer))
                : 0f;
            _isGrounded = false;
            float bestDistance = float.MaxValue;
            float bestNormalY = requiredGroundNormalY;
            float shorelineSnapDistance = (!_isWalking && _smoothedImmersionRatio < swimTransitionThreshold)
                ? math.min(0.1f, math.max(0f, shoreBuoyancyRecoveryClearance))
                : 0f;
            float maxGroundDistance = math.max(groundCheckDistance, shorelineSnapDistance) + GroundCheckSkin + speculativeHoverHeight;

            FindBestStandardGroundHit(requiredGroundNormalY, maxGroundDistance, ref bestDistance, ref bestNormalY);

            Vector3 resolvedGroundNormal = _groundHit.normal;
            ResolveSpecialFooting(allowExosuitFootSlopeProbe, dryInteriorActive, requiredGroundNormalY, ref bestDistance, ref bestNormalY, ref resolvedGroundNormal);

            if (_isGrounded)
            {
                float blendSharpness = exosuitActive && _exosuitFootingValid ? exosuitFootSlopeBlendSharpness : 15f;
                float normalT = ResolveLinearBlendT(math.max(0.01f, blendSharpness), fixedDeltaTime);
                _smoothedGroundNormal = FastLerpNormal(_smoothedGroundNormal, resolvedGroundNormal, normalT, Vector3.up);

                float sqrMag = _smoothedGroundNormal.sqrMagnitude;
                if (sqrMag > 0.001f && math.abs(sqrMag - 1f) > 0.001f)
                {
                    _smoothedGroundNormal = NormalizeVectorRsqrt(_smoothedGroundNormal, Vector3.up);
                }
            }
            else
            {
                _groundHit = default;
                if (speculativeHoverAllowed)
                {
                    _isGrounded = true;
                    _smoothedGroundNormal = speculativeHoverNormal.sqrMagnitude > 0.000001f
                        ? NormalizeVectorRsqrt(speculativeHoverNormal, Vector3.up)
                        : Vector3.up;
                    _aupSpeculativeHoverTicks = 0;
                    _aupSpeculativeHoverHeightMeters = 0f;
                }
                else
                {
                    float resetT = ResolveLinearBlendT(5f, fixedDeltaTime);
                    _smoothedGroundNormal = FastLerpNormal(_smoothedGroundNormal, Vector3.up, resetT, Vector3.up);
                }
            }

            if (_aupSpeculativeHoverTicks > 0)
                _aupSpeculativeHoverTicks = 0;
            _aupSpeculativeHoverHeightMeters = 0f;
            _isAirborne = !_isGrounded;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  GROUND STABILITY
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplyGroundStability(float scale)
        {
            if (scale <= 0.001f) return;

            float mass = ResolveAuthoritativeBodyMassKg();
            bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            float slopeStickMultiplier = exosuitActive ? exosuitSlopeStickForceMultiplier : 1f;
            float snapForceMultiplier = exosuitActive ? exosuitGroundSnapForceMultiplier : 1f;
            float gravityAlongNormal = DotVector(_cachedGravity, _smoothedGroundNormal);
            _forceVector.x = (_smoothedGroundNormal.x * gravityAlongNormal) - _cachedGravity.x;
            _forceVector.y = (_smoothedGroundNormal.y * gravityAlongNormal) - _cachedGravity.y;
            _forceVector.z = (_smoothedGroundNormal.z * gravityAlongNormal) - _cachedGravity.z;

            float tangentSqr = _forceVector.x * _forceVector.x + _forceVector.y * _forceVector.y + _forceVector.z * _forceVector.z;
            if (tangentSqr > 0.000001f)
            {
                float slopeHoldForce = mass * _gravityScale * scale * slopeStickMultiplier;
                _forceVector.x *= slopeHoldForce;
                _forceVector.y *= slopeHoldForce;
                _forceVector.z *= slopeHoldForce;
                ApplyMotorForce(_forceVector);
            }

            float gravityIntoGround = DotVector(-_cachedGravity, _smoothedGroundNormal);
            if (gravityIntoGround > 0f)
            {
                float supportForce = gravityIntoGround * mass * slopeStabilityFactor * scale * slopeStickMultiplier;
                _forceVector.x = _smoothedGroundNormal.x * supportForce;
                _forceVector.y = _smoothedGroundNormal.y * supportForce;
                _forceVector.z = _smoothedGroundNormal.z * supportForce;
                ApplyMotorForce(_forceVector);
            }

            if (groundSnapForce > 0f)
            {
                float snapForce = groundSnapForce * mass * scale * snapForceMultiplier;
                _forceVector.x = -_smoothedGroundNormal.x * snapForce;
                _forceVector.y = -_smoothedGroundNormal.y * snapForce;
                _forceVector.z = -_smoothedGroundNormal.z * snapForce;
                ApplyMotorForce(_forceVector);
            }

            ApplyKinematicGroundSnap(scale);
        }

        private void ApplyKinematicGroundSnap(float scale)
        {
            if (!_isGrounded || scale <= 0.001f)
                return;

            Vector3 groundPoint = _groundHit.point;
            if (!math.isfinite(groundPoint.x) || !math.isfinite(groundPoint.y) || !math.isfinite(groundPoint.z))
                return;

            Vector3 groundNormal = _smoothedGroundNormal.sqrMagnitude > 0.0001f
                ? NormalizeVectorRsqrt(_smoothedGroundNormal, Vector3.up)
                : Vector3.up;
            if (groundNormal.y < _minGroundNormalY)
                return;

            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            Vector3 groundedVelocity = SuppressGroundPenetratingVelocity(currentVelocity, groundNormal);
            groundedVelocity = HectonPlayerMotor.SafeVelocity(groundedVelocity, currentVelocity);
            if ((groundedVelocity - currentVelocity).sqrMagnitude > 0.000001f)
                ApplyMotorLinearVelocity(groundedVelocity);

            float currentBottomY = GetBodyBottomY();
            float snapDeltaY = groundPoint.y - currentBottomY;
            float maxSnapDistance = math.min(0.1f, math.max(0f, groundCheckDistance)) + GroundCheckSkin;
            if (!math.isfinite(snapDeltaY) || math.abs(snapDeltaY) > maxSnapDistance)
                return;

            float scaledDeltaY = snapDeltaY * math.saturate(scale);
            if (math.abs(scaledDeltaY) <= GroundCheckSkin)
                return;

            Vector3 bodyPosition = _useFixedFrameSpatialCache ? _fixedFrameBodyPosition : ResolveBodyRuntimePosition();
            bodyPosition.y += scaledDeltaY;
            MoveMotorPosition(HectonPlayerMotor.SafeVelocity(bodyPosition, ResolveBodyRuntimePosition()));
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SURFACE LOCK
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplySurfaceLock(SuitData suit, PlayerTransportPreset transportPreset)
        {
            if (suit.surfaceLockStrength <= 0f) return;
            if (IsInDryInterior()) return;
            if (_isGrounded) return;
            if (_shoreGroundGraceTimer > 0f && _smoothedImmersionRatio < swimTransitionThreshold) return;
            float surfaceLockInfluenceScale = ResolveTransportSurfaceLockInfluenceScale(transportPreset);
            float diveCommitT = 0f;
            if (surfaceDiveCommitHoldTime > 0f)
                diveCommitT = math.saturate(_surfaceDiveCommitTimer / surfaceDiveCommitHoldTime);
            else if (HasSurfaceDiveIntent(transportPreset))
                diveCommitT = 1f;

            float diveIntentScale = HasCommittedSurfaceDive(transportPreset)
                ? 0.18f
                : math.lerp(1f, 0.72f, diveCommitT);
            float sargassumMatBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);
            float sargassumSupportScale = math.lerp(
                1f,
                math.max(1f, ResolveSargassumPositive(sargassumMatSurfaceLockBoost, 1.4f)),
                sargassumMatBlend);
            float effectiveLockScale = _shoreBuoyancyBlend * surfaceLockInfluenceScale * _surfaceLockBlend * diveIntentScale * sargassumSupportScale;
            if (effectiveLockScale <= 0.001f) return;

            float targetSurfaceLockY = _surfaceLockTargetY + ResolveSargassumNonNegative(sargassumMatSurfaceLiftOffset, 0.16f) * sargassumMatBlend;
            float positionError = ResolveBodyRuntimePosition().y - targetSurfaceLockY;
            float effectiveSurfaceLockRange = math.max(suit.surfaceLockRange, math.abs(surfaceStickOffset) + surfaceBreachDepthWindow);
            positionError = math.clamp(positionError, -effectiveSurfaceLockRange, effectiveSurfaceLockRange);

            float targetSurfaceVelocityY = _isSurfaceSwimming
                ? EffectiveWaterSurfaceVelocity.y * surfaceWaveVelocityInfluence
                : 0f;
            float velocityError = ResolveAuthoritativeLinearVelocity(Vector3.zero).y - targetSurfaceVelocityY;
            float springForce = -positionError * suit.surfaceLockStrength * effectiveLockScale;
            float dampForce = -velocityError * suit.surfaceLockDamping * effectiveLockScale;
            float totalForce = (springForce + dampForce) * ResolveAuthoritativeBodyMassKg();

            _forceVector.x = 0f;
            _forceVector.y = totalForce;
            _forceVector.z = 0f;
            ApplyMotorAccelerationFromForce(_forceVector);
        }

        private void GetCapsuleWorldMetrics(out Vector3 center, out float radius, out float halfHeight)
        {
            if (_useFixedFrameSpatialCache)
            {
                center = _fixedFrameCapsuleCenterWS;
                radius = _fixedFrameCapsuleRadius;
                halfHeight = _fixedFrameCapsuleHalfHeight;
                return;
            }

            if (_capsuleCollider == null)
            {
                center = ResolveBodyRuntimePosition();
                radius = math.max(groundCheckRadius, 0.01f);
                halfHeight = math.max(playerHeight * 0.5f, radius);
                return;
            }

            Vector3 lossyScale = _cachedTransform.lossyScale;
            float absScaleX = math.abs(lossyScale.x);
            float absScaleY = math.abs(lossyScale.y);
            float absScaleZ = math.abs(lossyScale.z);
            float radialScale = math.max(absScaleX, absScaleZ);
            radius = math.max(0.01f, _capsuleCollider.radius * radialScale);
            halfHeight = math.max(radius, _capsuleCollider.height * 0.5f * absScaleY);
            center = _cachedTransform.TransformPoint(_capsuleCollider.center);
        }

        private float GetBodyBottomY()
        {
            if (_useFixedFrameSpatialCache)
                return _fixedFrameBodyBottomY;

            GetCapsuleWorldMetrics(out Vector3 center, out _, out float halfHeight);
            return center.y - halfHeight;
        }

        private float GetBodyTopY()
        {
            if (_useFixedFrameSpatialCache)
                return _fixedFrameBodyTopY;

            GetCapsuleWorldMetrics(out Vector3 center, out _, out float halfHeight);
            return center.y + halfHeight;
        }

        private float GetBodyEyeY()
        {
            if (_useFixedFrameSpatialCache)
                return _fixedFrameBodyEyeY;

            return math.lerp(GetBodyBottomY(), GetBodyTopY(), 0.85f);
        }

        private float GetHeadSurfaceOffset(float surfaceY)
        {
            return surfaceY - GetBodyTopY();
        }

        private float GetHeadDepthBelowSurface(float surfaceY)
        {
            float depth = GetHeadSurfaceOffset(surfaceY);
            return depth > 0f ? depth : 0f;
        }

        private float GetFeetDepthBelowSurface(float surfaceY)
        {
            float depth = surfaceY - GetBodyBottomY();
            return depth > 0f ? depth : 0f;
        }

        private void UpdateBottomClearance()
        {
            if (IsInDryInterior())
            {
                _bottomClearance = float.PositiveInfinity;
                _bottomNormal = Vector3.up;
                return;
            }

            if (_isGrounded)
            {
                _bottomClearance = 0f;
                _bottomNormal = _smoothedGroundNormal.sqrMagnitude > 0.0001f ? NormalizeVectorRsqrt(_smoothedGroundNormal, Vector3.up) : Vector3.up;
                return;
            }

            float maxSampleDistance = math.max(
                groundCheckDistance,
                math.max(shoreBuoyancyRecoveryClearance, underwaterTurbulenceBottomInfluenceDepth)) + playerHeight;
            if (maxSampleDistance <= 0f)
            {
                _bottomClearance = float.PositiveInfinity;
                _bottomNormal = Vector3.up;
                return;
            }

            float bestClearance = float.PositiveInfinity;
            Vector3 bestNormal = Vector3.up;
            for (int i = 0; i < _fixedGroundSweepHitCount; i++)
            {
                PlayerMovementSurfaceHit hit = _groundProbeHitBuffer[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _playerColliderInstanceId)
                    continue;

                if (hit.normal.y < _minGroundNormalY)
                    continue;

                float clearance = hit.distance - GroundCheckSkin;
                if (clearance < 0f)
                    clearance = 0f;

                if (clearance < bestClearance)
                {
                    bestClearance = clearance;
                    bestNormal = hit.normal;
                }
            }

            _bottomClearance = bestClearance;
            _bottomNormal = bestNormal;
        }

        private void UpdateShoreBuoyancyBlend(float fixedDeltaTime, float physicsImmersion, float feetDepth)
        {
            float targetBlend = 1f;
            if (IsInDryInterior() || physicsImmersion <= 0.01f)
            {
                targetBlend = 0f;
            }
            else if (feetDepth <= shoreBuoyancyRecoveryClearance)
            {
                float depthBlend = math.saturate(
                    (feetDepth - shoreWalkFootDepth) /
                    math.max(shoreBuoyancyRecoveryClearance - shoreWalkFootDepth, 0.01f));
                if (!float.IsPositiveInfinity(_bottomClearance))
                {
                    float clearanceBlend = math.saturate(_bottomClearance / math.max(shoreBuoyancyRecoveryClearance, 0.01f));
                    targetBlend = math.min(depthBlend, clearanceBlend);
                }
                else
                {
                    targetBlend = depthBlend;
                }
            }

            float blendT = ResolveLinearBlendT(math.max(shoreBuoyancyBlendSharpness, 0.01f), fixedDeltaTime);
            _shoreBuoyancyBlend = math.lerp(_shoreBuoyancyBlend, targetBlend, blendT);
        }

        private bool TryGetCapsuleCastGeometry(float inset, out Vector3 point1, out Vector3 point2, out float radius)
        {
            GetCapsuleWorldMetrics(out Vector3 center, out float baseRadius, out float halfHeight);
            radius = math.max(0.01f, baseRadius - inset);
            float segmentHalf = math.max(0f, halfHeight - radius - inset);
            point1 = center + Vector3.up * segmentHalf;
            point2 = center - Vector3.up * segmentHalf;
            return true;
        }

        private void RefreshGroundSlopeCache()
        {
            _minGroundNormalY = ResolveDegreesCosFast(maxGroundAngle);
        }

        private void ConsumeJumpRequest()
        {
            _jumpRequested = false;
            _jumpBufferTimer = 0f;
            _jumpHeldTimer = 0f;
        }

        private void TryApplyKinematicWallKick(bool suppressPhysicsMutation)
        {
            if (suppressPhysicsMutation)
                return;

            if (_playerMotor == null ||
                _rb == null ||
                _isWalking ||
                IsInDryInterior() ||
                IsCriticallyEncumbered ||
                wallKickVelocityChange <= 0f ||
                _wallKickCooldownTimer > 0f)
            {
                return;
            }

            bool jumpIntent = _jumpRequested && _jumpBufferTimer > 0f;
            if (!jumpIntent)
                return;

            Vector3 wallPoint;
            float wallBlockedSpeed;
            float wallSlideAngleDegrees;
            float wallVelocityReduction01;
            int wallPhysicsFrame;
            if (!_playerMotor.TryGetRecentWallSlideContact(
                    wallKickContactFrameGrace,
                    out Vector3 wallNormal,
                    out wallPoint,
                    out wallBlockedSpeed,
                    out wallSlideAngleDegrees,
                    out wallVelocityReduction01,
                    out wallPhysicsFrame,
                    out bool isVoxelWall))
            {
                return;
            }

            wallNormal = HectonPlayerMotor.SafeVelocity(wallNormal, Vector3.up);
            if (!isVoxelWall || wallNormal.sqrMagnitude <= 0.000001f || wallNormal.y >= 0.75f)
                return;

            wallNormal = NormalizeVectorRsqrt(wallNormal, Vector3.up);
            Vector3 planarForward = Quaternion.Euler(0f, _cameraYaw, 0f) * Vector3.forward;
            if (DotVector(planarForward, -wallNormal) < 0.35f)
                return;

            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float inwardNormalSpeed = math.max(0f, -DotVector(currentVelocity, wallNormal));
            float3 wallNormal3 = new float3(wallNormal.x, wallNormal.y, wallNormal.z);
            float3 deltaVelocity = new float3(currentVelocity.x, currentVelocity.y, currentVelocity.z);
            deltaVelocity -= math.project(deltaVelocity, wallNormal3);
            deltaVelocity *= math.saturate(wallKickTangentFriction);
            _playerMotor.SetLinearVelocity(new Vector3(deltaVelocity.x, deltaVelocity.y, deltaVelocity.z));

            Vector3 outwardVelocityChange = wallNormal * (wallKickVelocityChange + inwardNormalSpeed);
            ApplyMotorVelocityChange(outwardVelocityChange);
            DrainWallKickResources();
            _isGrounded = false;
            _isAirborne = true;
            _wasGroundedLastFrame = false;
            _wallKickCooldownTimer = math.max(0f, wallKickCooldown);

            float outwardVelocityChangeSqr = outwardVelocityChange.sqrMagnitude;
            if (_vrComfortActiveCached)
            {
                float wallKickReference = math.max(0.01f, wallKickVelocityChange * 1.5f);
                RegisterVrComfortVisualBounce(math.saturate(outwardVelocityChangeSqr / (wallKickReference * wallKickReference)));
            }
            else if (currentSuitData != null)
            {
                float outwardVelocityChangeMagnitude = outwardVelocityChangeSqr * math.rsqrt(math.max(0.000001f, outwardVelocityChangeSqr));
                QueueCameraCollisionImpulse(outwardVelocityChangeMagnitude);
            }

            if (jumpIntent)
                ConsumeJumpRequest();
        }

        internal bool TryGetRecentPresentationWallContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float velocityReduction01)
        {
            normal = Vector3.zero;
            point = Vector3.zero;
            velocityReduction01 = 0f;
            if (_playerMotor == null)
                return false;

            if (!_playerMotor.TryGetRecentWallSlideContact(
                    maxPhysicsFrameAge,
                    out normal,
                    out point,
                    out _,
                    out _,
                    out velocityReduction01,
                    out _,
                    out _))
            {
                return false;
            }

            if (!math.isfinite(normal.x) ||
                !math.isfinite(normal.y) ||
                !math.isfinite(normal.z) ||
                normal.sqrMagnitude <= 0.000001f)
            {
                normal = Vector3.zero;
                point = Vector3.zero;
                velocityReduction01 = 0f;
                return false;
            }

            normal = NormalizeVectorRsqrt(normal, Vector3.forward);
            return true;
        }

        private void DrainWallKickResources()
        {
            if (wallKickResourceCost01 <= 0f)
                return;

            if (_survivalSystem == null)
                return;

            SurvivalStats stats = _survivalSystem.Stats;
            float energyCapacity = stats != null ? stats.MaxEnergy : math.max(1f, _survivalSystem.Energy);
            float cost01 = math.saturate(wallKickResourceCost01);
            _survivalSystem.DrainEnergy(energyCapacity * cost01);
        }

        private void TryProcessKccWallScrapeFeedback()
        {
            if (_playerMotor == null || _rb == null)
                return;

            if (!_playerMotor.TryGetRecentWallSlideContact(
                    0,
                    out Vector3 wallNormal,
                    out Vector3 hitPoint,
                    out float blockedSpeed,
                    out float slideAngleDegrees,
                    out float velocityReduction01,
                    out int physicsFrame))
            {
                return;
            }

            if (physicsFrame == _lastProcessedKccSlideFeedbackFrame)
                return;

            if (slideAngleDegrees < suitScrapeSlideAngleThresholdDegrees || blockedSpeed < suitScrapeMinBlockedSpeed)
                return;

            _lastProcessedKccSlideFeedbackFrame = physicsFrame;
            float angleT = math.saturate((slideAngleDegrees - suitScrapeSlideAngleThresholdDegrees) / math.max(90f - suitScrapeSlideAngleThresholdDegrees, 0.01f));
            float speedT = math.saturate(blockedSpeed / math.max(suitScrapeMinBlockedSpeed * 4f, 0.01f));
            float reductionT = math.saturate(velocityReduction01);
            float scrapeT = math.max(reductionT, math.max(angleT, speedT));

            if (_vrComfortActiveCached)
            {
                RegisterVrComfortVisualBounce(scrapeT);
            }
            else if (currentSuitData != null)
            {
                float cameraSpeed = math.max(
                    currentSuitData.collisionShakeThreshold + 0.01f,
                    blockedSpeed * math.max(0f, suitScrapeCameraSpeedScale) * scrapeT);
                QueueCameraCollisionImpulse(cameraSpeed);
            }

            float busSpeed = blockedSpeed * math.max(0f, suitScrapeImpactBusSpeedScale) * math.max(0.15f, scrapeT);
            IPhysicsStateEventService physicsStateEvents = _physicsStateEvents;
            physicsStateEvents?.QueueKinematicImpact(
                _rb,
                hitPoint,
                wallNormal,
                busSpeed);

            if (!TryResolveAupFromPlayerStateRuntimeDelta(hitPoint, out AbsoluteUniversePosition hitPointAup))
                return;

            AcousticPingSignal scrapePing = new AcousticPingSignal
            {
                PositionAup = hitPointAup,
                RadiusMeters = math.max(0f, suitScrapeAcousticRadiusMeters),
                Intensity01 = math.saturate(scrapeT * 0.35f),
                SourceId = PlayerSignalSourceId,
                Channel = AcousticPingSignal.ChannelFabricScrape,
                Flags = AcousticPingSignal.FlagFabricScrape
            };
            QueueKccWallScrapeAcoustic(in scrapePing);
        }

        private void ApplyExosuitJumpJets(float fixedDeltaTime)
        {
            if (!IsExosuitTransportActive())
                return;

            if (ExosuitKinematicAuthority.HasActiveAuthority())
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (_rb == null || fixedDeltaTime <= 0f)
                return;

            if (_exosuitJumpJetWakePulseTimer > 0f)
                _exosuitJumpJetWakePulseTimer = math.max(0f, _exosuitJumpJetWakePulseTimer - fixedDeltaTime);

            float jumpIntent = ResolveExosuitJumpJetIntent();
            if (jumpIntent <= 0.0001f)
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (IsCriticallyEncumbered)
            {
                ConsumeJumpRequest();
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (!HasJumpHeadClearance())
            {
                ConsumeJumpRequest();
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (_survivalSystem != null && _survivalSystem.Energy <= 0.01f)
            {
                ConsumeJumpRequest();
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (_exosuitJumpJetsOverheated)
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                ConsumeJumpRequest();
                return;
            }

            if (_isGrounded && _jumpRequested && exosuitJumpJetLaunchImpulse > 0f)
            {
                ApplyMotorVelocityChange(Vector3.up * exosuitJumpJetLaunchImpulse);
                _isGrounded = false;
                _isAirborne = true;
                _wasGroundedLastFrame = false;
                _dryGroundGraceTimer = 0f;
                _shoreGroundGraceTimer = 0f;
            }

            float thrustForce = exosuitJumpJetForce * jumpIntent;
            if (thrustForce > 0.001f)
            {
                Vector3 thrustDirection = ResolveExosuitJumpJetTurbulentDirection();
                ApplyMotorForce(thrustDirection * thrustForce);

                float energyDrainPerSecond = ResolveExosuitJumpJetEnergyDrainPerSecond();
                if (_survivalSystem != null && energyDrainPerSecond > 0f)
                    _survivalSystem.DrainEnergy(energyDrainPerSecond * jumpIntent * fixedDeltaTime);

                EmitExosuitJumpJetWakeTrail(jumpIntent);

                if (exosuitJumpJetHeatPerSecond > 0f)
                {
                    _exosuitJumpJetHeat01 = math.saturate(_exosuitJumpJetHeat01 + exosuitJumpJetHeatPerSecond * jumpIntent * fixedDeltaTime);
                    if (_exosuitJumpJetHeat01 >= 0.999f)
                        _exosuitJumpJetsOverheated = true;
                }
            }

            if (!_currentInputState.HasAction(PlayerInputAction.Jump))
                ConsumeJumpRequest();
        }

        private Vector3 ResolveExosuitJumpJetTurbulentDirection()
        {
            return ResolveProceduralThrusterNoiseDirectionUnit(Vector3.up);
        }

        private static float SignedTriangle01(float phase)
        {
            float wrapped = math.frac(phase);
            return (1f - math.abs(wrapped * 2f - 1f)) * 2f - 1f;
        }

        private Vector3 ResolveProceduralThrusterNoiseDirection(Vector3 baseDirection)
        {
            return ResolveProceduralThrusterNoiseDirectionUnit(baseDirection.sqrMagnitude > 0.000001f
                ? baseDirection
                : Vector3.up);
        }

        private Vector3 ResolveProceduralThrusterNoiseDirectionUnit(Vector3 safeBaseDirection)
        {
            Vector3 samplePosition = _useFixedFrameSpatialCache
                ? _fixedFrameBodyPosition
                : ResolveBodyRuntimePosition();
            float phase =
                _currentTimer * ExosuitJumpJetNoiseTimeScale +
                samplePosition.x * ExosuitJumpJetNoiseScale +
                samplePosition.y * 0.071f +
                samplePosition.z * 0.113f +
                _instanceId * 0.00017f;
            Vector3 jitter = new Vector3(
                SignedTriangle01(phase),
                SignedTriangle01(phase + 0.31f) * 0.5f,
                SignedTriangle01(phase + 0.57f));
            return safeBaseDirection + jitter * ExosuitJumpJetNoiseVectorScale;
        }

        private void CoolExosuitJumpJets(float fixedDeltaTime)
        {
            if (_exosuitJumpJetHeat01 <= 0f)
                return;

            _exosuitJumpJetHeat01 = math.max(0f, _exosuitJumpJetHeat01 - exosuitJumpJetCoolRate * fixedDeltaTime);
            if (_exosuitJumpJetsOverheated && _exosuitJumpJetHeat01 <= exosuitJumpJetRecoverThreshold)
                _exosuitJumpJetsOverheated = false;
        }

        private float ResolveExosuitJumpJetEnergyDrainPerSecond()
        {
            PlayerTransportPreset activeTransportPreset = ResolveActiveTransportPreset();
            if (activeTransportPreset != null && activeTransportPreset.EnergyDrainPerSecond > 0f)
            {
                return activeTransportPreset.EnergyDrainPerSecond *
                       math.max(1f, exosuitJumpJetScooterDrainMultiplier) *
                       CurrentAbyssalCounterDriveEnergyMultiplier;
            }

            return math.max(0f, exosuitJumpJetEnergyDrainPerSecond) * CurrentAbyssalCounterDriveEnergyMultiplier;
        }

        private float ResolveExosuitJumpJetIntent()
        {
            if (_currentInputState.HasAction(PlayerInputAction.Jump))
                return 1f;

            return _jumpRequested ? 1f : 0f;
        }

        private void EmitExosuitJumpJetWakeTrail(float jumpIntent)
        {
            if (_underwaterVisuals == null || exosuitJumpJetWakeTrailScale <= 0f || jumpIntent <= 0.0001f)
                return;

            bool nearSeabed = _isGrounded || _bottomClearance <= math.max(0.1f, exosuitFootProbeDistance * 1.15f);
            if (!nearSeabed || _exosuitJumpJetWakePulseTimer > 0f)
                return;

            QueueBottomSiltBurstVisual(jumpIntent * exosuitJumpJetWakeTrailScale);
            _exosuitJumpJetWakePulseTimer = math.max(0.02f, exosuitJumpJetWakePulseInterval);
        }

        private void TryEmitSurfaceFootstepAudio()
        {
            if (!_isWalking ||
                !_isGrounded ||
                baseFloorMetalFootstepClip == null ||
                baseFloorMetalFootstepVolume <= 0.001f)
            {
                return;
            }

            PlayerMovementSurfaceHit audioHit = _groundHit;

            Collider groundCollider = audioHit.collider;
            if (groundCollider == null)
                return;

            if (!TryResolveCachedFootstepAudioMaterialId(groundCollider, out byte materialId) ||
                materialId != (byte)ItemAudioMaterialId.Metal)
            {
                return;
            }

            Vector3 fallbackPosition = ResolvePlayerAupRuntimePosition();
            Vector3 playPosition = HectonPlayerMotor.SafeVelocity(audioHit.point, fallbackPosition);
            QueuePresentationAudioAtPoint(
                baseFloorMetalFootstepClip,
                playPosition,
                baseFloorMetalFootstepVolume,
                baseFloorMetalFootstepPitch);
        }

        private void ConsumeKinematicRepairTargetProbe()
        {
            if (_playerMotor == null)
                return;

            if (!_playerMotor.TryConsumeKinematicRepairSnap(
                    out KinematicRepairTargetProbe probe,
                    out KinematicRepairSnapPoint snapPoint))
            {
                return;
            }

            _lastKinematicRepairProbe = probe;
            _lastKinematicRepairSnapPoint = snapPoint;
            _kinematicRepairStateBits |= KinematicRepairStateHasSnapBit;
        }

        private void ScheduleKinematicRepairTargetProbe()
        {
            if (_playerMotor == null ||
                playerCamera == null ||
                kinematicRepairTargetProbeRange <= 0.05f)
            {
                return;
            }

            Transform cameraTransform = playerCamera;
            Vector3 cameraForward = HectonPlayerMotor.SafeVelocity(cameraTransform.forward, Vector3.forward);
            AbsoluteUniversePosition playerAup = _playerState.AbsolutePosition;
            if (ShouldReuseKinematicRepairProbe(in playerAup, cameraForward))
                return;

            if (!_playerMotor.ScheduleKinematicRepairTargetProbe(
                cameraTransform.position,
                cameraForward,
                kinematicRepairTargetProbeRange,
                HectonLayerMasks.StrictInteractionLayerMask,
                kinematicRepairTargetSurfaceOffset))
            {
                return;
            }

            _lastKinematicRepairProbeCullAup = playerAup;
            _lastKinematicRepairProbeCullDirection = cameraForward;
            _kinematicRepairProbeAupGateSkipCount = 0;
            _kinematicRepairStateBits |= KinematicRepairStateHasProbeCullAnchorBit;
        }

        private bool ShouldReuseKinematicRepairProbe(in AbsoluteUniversePosition playerAup, Vector3 rayDirection)
        {
            uint requiredBits = KinematicRepairStateHasSnapBit | KinematicRepairStateHasProbeCullAnchorBit;
            if ((_kinematicRepairStateBits & requiredBits) != requiredBits ||
                _kinematicRepairProbeAupGateSkipCount >= KinematicRepairProbeMaxAupGateSkips)
            {
                return false;
            }

            double movedSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastKinematicRepairProbeCullAup);
            if (movedSq > KinematicRepairProbeAupReuseDistanceSq)
                return false;

            float directionDot = math.dot(
                new float3(rayDirection.x, rayDirection.y, rayDirection.z),
                new float3(_lastKinematicRepairProbeCullDirection.x, _lastKinematicRepairProbeCullDirection.y, _lastKinematicRepairProbeCullDirection.z));
            if (directionDot < KinematicRepairProbeDirectionReuseDot)
                return false;

            _kinematicRepairProbeAupGateSkipCount++;
            return true;
        }

        private void ResetKinematicRepairProbeReuseGate()
        {
            _lastKinematicRepairProbeCullAup = default;
            _lastKinematicRepairProbeCullDirection = Vector3.forward;
            _kinematicRepairProbeAupGateSkipCount = 0;
            _kinematicRepairStateBits &= ~KinematicRepairStateHasProbeCullAnchorBit;
        }

        private bool TryResolveCachedFootstepAudioMaterialId(Collider collider, out byte materialId)
        {
            materialId = 0;
            if (collider == null)
                return false;

            int colliderInstanceId = unchecked((int)EntityId.ToULong(collider.GetEntityId()));
            if (colliderInstanceId == _cachedFootstepAudioColliderInstanceId)
            {
                materialId = _cachedFootstepAudioMaterialId;
                return _cachedFootstepAudioMaterialResolved;
            }

            bool resolved = TryResolveImpactAudioMaterialId(collider, out materialId);
            _cachedFootstepAudioColliderInstanceId = colliderInstanceId;
            _cachedFootstepAudioMaterialId = resolved ? materialId : (byte)0;
            _cachedFootstepAudioMaterialResolved = resolved;
            return resolved;
        }

        private void ResetFootstepAudioMaterialCache()
        {
            _cachedFootstepAudioColliderInstanceId = 0;
            _cachedFootstepAudioMaterialId = 0;
            _cachedFootstepAudioMaterialResolved = false;
        }

        private bool TryResolveImpactAudioMaterialId(Collider collider, out byte materialId)
        {
            materialId = 0;
            if (collider == null)
                return false;

            Rigidbody attachedBody = collider.attachedRigidbody;
            IPhysicsStateEventService physicsStateEvents = _physicsStateEvents;
            if (attachedBody == null || physicsStateEvents == null)
                return false;

            return physicsStateEvents.TryResolveImpactAudioMaterialId(attachedBody, out materialId);
        }

        private void EmitExosuitFootstepSeismicPing()
        {
            if (_currentLocomotionMode != PlayerLocomotionMode.ExosuitLocomotion ||
                !_isGrounded ||
                IsInDryInterior() ||
                exosuitFootstepSonarPingRadius <= 0.01f)
                return;

            SpectrumSystem spectrumSystem = _spectrumRuntime;
            if (spectrumSystem != null)
                spectrumSystem.TriggerActiveSonarPing(exosuitFootstepSonarPingRadius, exosuitFootstepSonarRevealDuration);

            if (exosuitFootstepThreatStrength > 0f &&
                TryResolveVegetationBridge(out HectonMapMagicVegetationBridge bridge))
            {
                bridge.ApplyExternalThreatPulse(
                    ResolvePlayerAupRuntimePosition(),
                    exosuitFootstepSonarPingRadius,
                    exosuitFootstepThreatStrength,
                    exosuitFootstepThreatHoldDuration);
            }
        }

        private bool ShouldRunExosuitFootProbes()
        {
            if (!_isAirborne)
                return false;

            float verticalVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero).y;
            if (verticalVelocity >= -0.01f)
                return false;

            if (float.IsPositiveInfinity(_bottomClearance))
                return true;

            float lowerSectorClearance = math.max(exosuitFootProbeDistance * 1.35f, groundCheckDistance * 0.6f);
            return _bottomClearance <= lowerSectorClearance;
        }

        private bool TryResolveDryInteriorFootSlope(
            float minimumNormalY,
            out PlayerMovementSurfaceHit supportHit,
            out Vector3 supportNormal)
        {
            return TryResolveMovementSweepSupportHits(
                useExosuitSupport: false,
                useDryInteriorSupport: true,
                minimumNormalY: minimumNormalY,
                supportHit: out supportHit,
                supportNormal: out supportNormal);
        }

        private bool TryResolveExosuitFootSlope(
            float minimumNormalY,
            out PlayerMovementSurfaceHit supportHit,
            out Vector3 supportNormal)
        {
            return TryResolveMovementSweepSupportHits(
                useExosuitSupport: true,
                useDryInteriorSupport: false,
                minimumNormalY: minimumNormalY,
                supportHit: out supportHit,
                supportNormal: out supportNormal);
        }

        private bool TryResolveMovementSweepSupportHits(
            bool useExosuitSupport,
            bool useDryInteriorSupport,
            float minimumNormalY,
            out PlayerMovementSurfaceHit supportHit,
            out Vector3 supportNormal)
        {
            supportHit = default;
            supportNormal = Vector3.up;
            if (!TryBuildMovementSweepSupportOrigins(
                    useExosuitSupport,
                    useDryInteriorSupport,
                    out Vector3 leftOrigin,
                    out Vector3 rightOrigin,
                    out float probeDistance))
            {
                return false;
            }

            float probeRadius = ResolveMovementSupportProbeRadius();
            if (TryUseCachedCenterSupportHit(minimumNormalY, probeDistance, out supportHit, out supportNormal))
                return true;

            bool leftValid = TryResolveNearestMovementProbeHit(leftOrigin, probeRadius, Vector3.down, probeDistance, out PlayerMovementSurfaceHit leftHit) &&
                             leftHit.normal.y >= minimumNormalY;
            bool rightValid = TryResolveNearestMovementProbeHit(rightOrigin, probeRadius, Vector3.down, probeDistance, out PlayerMovementSurfaceHit rightHit) &&
                              rightHit.normal.y >= minimumNormalY;
            if (!leftValid && !rightValid)
                return false;

            if (leftValid && rightValid)
            {
                Vector3 combinedNormal = leftHit.normal + rightHit.normal;
                supportNormal = combinedNormal.sqrMagnitude > 0.0001f
                    ? NormalizeVectorRsqrt(combinedNormal, Vector3.up)
                    : Vector3.up;
                supportHit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
                return true;
            }

            supportHit = leftValid ? leftHit : rightHit;
            supportNormal = supportHit.normal;
            return true;
        }

        private bool TryUseCachedCenterSupportHit(
            float minimumNormalY,
            float probeDistance,
            out PlayerMovementSurfaceHit supportHit,
            out Vector3 supportNormal)
        {
            supportHit = default;
            supportNormal = Vector3.up;
            if (!_isGrounded)
                return false;

            if (GetHitColliderInstanceId(in _groundHit) == 0)
                return false;

            float normalY = _groundHit.normal.y;
            if (normalY < math.max(minimumNormalY, CinematicCenterSupportNormalY))
                return false;

            if (_groundHit.distance > probeDistance + GroundCheckSkin)
                return false;

            supportHit = _groundHit;
            supportNormal = NormalizeVectorRsqrt(_groundHit.normal, Vector3.up);
            return true;
        }

        private bool TryApplyJumpImpulse(float impulse)
        {
            if (impulse <= 0f)
                return false;

            if (!HasJumpHeadClearance())
                return false;

            _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                ApplyMotorLinearVelocity(_velocity);
            }

            _isGrounded = false;
            _isAirborne = true;
            _wasGroundedLastFrame = false;
            _snapScale = 0f;
            _dryGroundGraceTimer = 0f;
            _shoreGroundGraceTimer = 0f;

            if (_juiceProcessor != null)
                _juiceProcessor.RegisterLandJumpLaunch();

            ApplyMotorVelocityChange(Vector3.up * impulse);
            return true;
        }

        private bool HasJumpHeadClearance()
        {
            if (jumpHeadClearanceDistance <= 0f)
                return true;

            BuildFixedFrameSweepCapsule(out Vector3 point1, out _, out float radius);
            float probeRadius = math.max(0.01f, radius - 0.02f);
            if (!TryResolveNearestMovementProbeHit(point1, probeRadius, Vector3.up, jumpHeadClearanceDistance, out _))
                return true;

            return false;
        }

        private bool TryApplyStepAssist(bool groundedOnDryLand, bool groundedOnShore)
        {
            if (stepAssistHeight <= 0f || stepAssistForwardDistance <= 0f || stepAssistVerticalVelocityPulse <= 0f)
                return false;

            if (_stepAssistCooldownTimer > 0f)
                return false;

            if (!(groundedOnDryLand || groundedOnShore || _isGrounded))
                return false;

            if (ResolveAuthoritativeLinearVelocity(Vector3.zero).y > 0.5f)
                return false;

            if (_inputV <= 0.05f)
                return false;

            if (!TryBuildMovementSweepStepDirection(out Vector3 stepDirection))
                return false;

            float probeRadius = math.max(groundCheckRadius * 0.85f, 0.05f);
            float currentBottomY = GetBodyBottomY();

            Vector3 currentBodyPosition = _useFixedFrameSpatialCache ? _fixedFrameBodyPosition : ResolveBodyRuntimePosition();
            _groundCheckOrigin.x = currentBodyPosition.x;
            _groundCheckOrigin.y = currentBottomY + probeRadius + GroundCheckSkin;
            _groundCheckOrigin.z = currentBodyPosition.z;

            float forwardDistance = stepAssistForwardDistance;
            if (forwardDistance <= 0f)
                return false;

            if (!TryFindStepObstacle(stepDirection, probeRadius, forwardDistance, out PlayerMovementSurfaceHit obstacleHit))
                return false;

            float obstacleHeightAboveBottom = obstacleHit.point.y - currentBottomY;
            if (obstacleHeightAboveBottom < -GroundCheckSkin ||
                obstacleHeightAboveBottom > stepAssistHeight + GroundCheckSkin)
            {
                return false;
            }

            if (HasForwardBlockAtHeight(stepDirection, probeRadius, forwardDistance))
                return false;

            if (!TryFindStepLanding(stepDirection, probeRadius, forwardDistance, out PlayerMovementSurfaceHit landingHit))
                return false;

            float stepDeltaY = landingHit.point.y - currentBottomY;
            if (stepDeltaY <= GroundCheckSkin ||
                stepDeltaY > stepAssistHeight + GroundCheckSkin)
            {
                return false;
            }

            if (!HasStepAssistHeadroom(currentBodyPosition, stepDirection, forwardDistance, stepDeltaY))
                return false;

            Vector3 currentVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float verticalPulse = math.max(0f, stepAssistVerticalVelocityPulse - currentVelocity.y);
            if (verticalPulse <= 0.001f)
                return false;

            ApplyMotorVelocityChange(Vector3.up * verticalPulse);

            _stepAssistCooldownTimer = stepAssistCooldownTime;
            _dryGroundGraceTimer = dryGroundGraceTime;
            if (groundedOnShore)
                _shoreGroundGraceTimer = shoreGroundGraceTime;

            return true;
        }

        private bool TryFindStepObstacle(
            Vector3 stepDirection,
            float probeRadius,
            float forwardDistance,
            out PlayerMovementSurfaceHit obstacleHit)
        {
            obstacleHit = default;
            if (forwardDistance <= 0f)
                return false;

            if (!TryResolveNearestMovementProbeHit(_groundCheckOrigin, probeRadius, stepDirection, forwardDistance, out PlayerMovementSurfaceHit hit))
                return false;

            if (hit.distance <= 0.001f || hit.normal.y >= _minGroundNormalY)
                return false;

            obstacleHit = hit;
            return true;
        }

        private bool HasForwardBlockAtHeight(
            Vector3 stepDirection,
            float probeRadius,
            float distance)
        {
            if (distance <= 0f)
                return false;

            Vector3 clearanceOrigin = _groundCheckOrigin;
            clearanceOrigin.y += stepAssistHeight;
            if (!TryResolveNearestMovementProbeHit(clearanceOrigin, probeRadius, stepDirection, distance, out PlayerMovementSurfaceHit hit))
                return false;

            return hit.distance <= distance + math.max(GroundCheckSkin, stepAssistClearance);
        }

        private bool TryFindStepLanding(
            Vector3 stepDirection,
            float probeRadius,
            float forwardDistance,
            out PlayerMovementSurfaceHit landingHit)
        {
            landingHit = default;
            if (forwardDistance <= 0f)
                return false;

            Vector3 landingOrigin = _groundCheckOrigin;
            landingOrigin.y += stepAssistHeight;
            landingOrigin += stepDirection * forwardDistance;
            float landingProbeDistance = stepAssistHeight + groundCheckDistance + GroundCheckSkin;
            if (!TryResolveNearestMovementProbeHit(landingOrigin, probeRadius, Vector3.down, landingProbeDistance, out PlayerMovementSurfaceHit hit))
                return false;

            if (hit.normal.y < _minGroundNormalY)
                return false;

            landingHit = hit;
            return true;
        }

        private bool HasStepAssistHeadroom(
            Vector3 currentBodyPosition,
            Vector3 stepDirection,
            float forwardDistance,
            float stepDeltaY)
        {
            if (stepDeltaY <= 0f)
                return true;

            Vector3 targetBodyPosition = currentBodyPosition + (stepDirection * forwardDistance);
            BuildFixedFrameSweepCapsuleAtPosition(targetBodyPosition, out Vector3 point1, out _, out float radius);
            float probeRadius = math.max(0.01f, radius - 0.02f);
            float headroomDistance = stepDeltaY + math.max(GroundCheckSkin, stepAssistClearance);
            return !TryResolveNearestMovementProbeHit(point1, probeRadius, Vector3.up, headroomDistance, out _);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SWIM PHYSICS Ã¢â‚¬â€ with depth pressure resistance
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void SwimPhysics(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            ApplyHeavyBrineSinkEffect();
            ApplyCriticalEncumbranceVerticalVelocityGate();

            float speedSq = _velocity.sqrMagnitude;
            bool isSurfaceSwim = _isSurfaceSwimming;
            bool hasSurfaceDiveIntent = isSurfaceSwim && HasCommittedSurfaceDive(transportPreset);
            float shoreSwimBlend = isSurfaceSwim ? _shoreBuoyancyBlend : 1f;
            float brineSwimSpeedMultiplier = _isInsideBrineLayer ? BrineLayerConstants.SwimSpeedMultiplier : 1f;
            float brineWaterDensityScale = _isInsideBrineLayer ? BrineLayerConstants.DensityMultiplier : 1f;

            // ─── Depth-based drag increase (v7.0) ───
            float effectiveDragCoeff = CalculateSwimEffectiveDragCoefficient(suit, isSurfaceSwim);

            _lastPlayerKinematicsDragCoefficient = effectiveDragCoeff;
            _lastPlayerKinematicsWaterDensityScale = brineWaterDensityScale;
            // Burst scalar water drag: presentation sells turbulence, authority stays replayable.
            ApplyBurstScalarWaterDrag(speedSq, effectiveDragCoeff, brineWaterDensityScale, fixedDeltaTime);

            // ─── Swim thrust ───
            float sargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
            float externalEnvironmentalThrustMultiplier = ResolveExternalEnvironmentalThrustMultiplier();

            float rawTransportPropulsionForce =
                ResolveActiveTransportPropulsionForce() *
                sargassumSpeedMultiplier *
                externalEnvironmentalThrustMultiplier *
                ResolveWipeoutTransportControl01();

            float gatedInputH = IsCriticalStaminaFailureActive ? 0f : _inputH;
            float gatedInputV = IsCriticalStaminaFailureActive ? 0f : _inputV;
            float gatedInputVertical = IsCriticalStaminaFailureActive ? 0f : _inputVertical;
            ApplyRuntimeNarcosisInputNoise(ref gatedInputH, ref gatedInputV, ref gatedInputVertical);
            _lastPlayerKinematicsIntendedMovement = new float3(gatedInputH, gatedInputVertical, gatedInputV);
            bool hasInput = gatedInputH != 0f || gatedInputV != 0f || gatedInputVertical != 0f;
            bool surfaceDiveAssistActive = _surfaceDiveAssistTimer > 0f;
            if (!hasInput && rawTransportPropulsionForce <= 0f && !surfaceDiveAssistActive)
                return;

            // ─── Depth-based swim force reduction (v7.0) ───
            float depthSlowdown = PlayerSwimMotor.ResolveDepthSlowdown(
                _currentDepth,
                suit.depthSwimSlowdownStart,
                suit.depthSwimSlowdownEnd,
                suit.depthSwimSlowdownMax);

            bool heavyCarryActive = IsHeavyCarryActive();
            float sprintMult = _isSprinting && !heavyCarryActive ? suit.sprintMultiplier : 1f;
            float runtimeSwimSpeedScale = _runtimeSwimSpeedMultiplier * _runtimeVoxelBackpressureSwimSpeedMultiplier * _runtimeInjurySwimSpeedMultiplier * _runtimeEmergencyMovementMultiplier * _runtimeStaminaMultiplier * ResolveRuntimeInventoryLoadMovementMultiplier() * brineSwimSpeedMultiplier;
            float heavyCarryForceMultiplier = ResolveHeavyCarryForceMultiplier();

            float effectiveSwimForce = CalculateEffectiveSwimForce(suit, depthSlowdown, sprintMult, runtimeSwimSpeedScale, heavyCarryForceMultiplier, externalEnvironmentalThrustMultiplier, sargassumSpeedMultiplier, shoreSwimBlend);
            float effectiveVerticalForce = CalculateEffectiveVerticalForce(suit, depthSlowdown, sprintMult, runtimeSwimSpeedScale, heavyCarryForceMultiplier, externalEnvironmentalThrustMultiplier, sargassumSpeedMultiplier, shoreSwimBlend);

            float transportForwardPitchInfluence = ResolveTransportForwardPitchInfluence(transportPreset);
            float transportStrafeInputScale = ResolveTransportStrafeInputScale(transportPreset);
            float transportVerticalInputScale = ResolveTransportVerticalInputScale(transportPreset);
            float transportReverseThrustScale = ResolveTransportReverseThrustScale(transportPreset);
            float transportSurfaceDiveAssistScale = ResolveTransportSurfaceDiveAssistScale(transportPreset);
            float hullStressTurnScale = ResolveHullStressTurnResponsivenessScale(transportPreset);
            transportStrafeInputScale *= hullStressTurnScale;

            CalculateSwimAxes(isSurfaceSwim, hasSurfaceDiveIntent, transportForwardPitchInfluence,
                out float fwdX, out float fwdY, out float fwdZ, out float rightX, out float rightZ);

            float forwardScale = isSurfaceSwim ? surfaceForwardForceMultiplier : 1f;
            float strafeScale = (isSurfaceSwim ? surfaceStrafeForceMultiplier : 1f) * transportStrafeInputScale;
            float forwardInput = gatedInputV;
            if (forwardInput < 0f)
                forwardInput *= transportReverseThrustScale;

            float forwardVelocity = _velocity.x * fwdX + _velocity.y * fwdY + _velocity.z * fwdZ;
            float transportPropulsionForce = rawTransportPropulsionForce;
            if (transportPropulsionForce > 0f)
            {
                transportPropulsionForce *= shoreSwimBlend;
                float cavitationEfficiency = ResolveTransportCavitationEfficiency(
                    fixedDeltaTime,
                    true,
                    forwardVelocity,
                    ResolveActiveTransportBoost01());
                transportPropulsionForce *= cavitationEfficiency;
            }
            else
            {
                ResolveTransportCavitationEfficiency(fixedDeltaTime, false, forwardVelocity, 0f);
            }

            float dirX = fwdX * (forwardInput * forwardScale) + rightX * (gatedInputH * strafeScale);
            float dirY = fwdY * (forwardInput * forwardScale);
            float dirZ = fwdZ * (forwardInput * forwardScale) + rightZ * (gatedInputH * strafeScale);

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = math.rsqrt(math.max(sqrMag, 0.000001f));
                dirX *= invMag; dirY *= invMag; dirZ *= invMag;
            }

            float verticalInput = gatedInputVertical;
            if (IsCriticallyEncumbered && verticalInput > 0f)
                verticalInput = 0f;

            if (isSurfaceSwim && verticalInput > 0f)
            {
                float ascendGate = math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                verticalInput *= ascendGate;
            }
            verticalInput *= transportVerticalInputScale;

            if (_activeTransportPlatform != null)
            {
                Vector3 platformUp = TransformTransportPlatformDirectionToWorld(Vector3.up);
                Vector3 rawInputWorld =
                    new Vector3(dirX, dirY, dirZ) +
                    (platformUp * verticalInput);
                Vector3 transformedInputWorld = ResolveTransportPlatformRelativeWorldDirection(rawInputWorld);
                dirX = transformedInputWorld.x;
                dirY = transformedInputWorld.y;
                dirZ = transformedInputWorld.z;
                verticalInput = 0f;
            }

            _lastPlayerKinematicsIntendedMovement = new float3(dirX, dirY + verticalInput, dirZ);

            _forceVector.x = dirX * effectiveSwimForce;
            _forceVector.y = dirY * effectiveSwimForce;
            _forceVector.z = dirZ * effectiveSwimForce;
            _forceVector.y += verticalInput * effectiveVerticalForce * (isSurfaceSwim ? surfaceVerticalForceMultiplier : 1f);

            ApplySurfaceDiveAssist(surfaceDiveAssistActive, effectiveVerticalForce, transportSurfaceDiveAssistScale);
            ApplySurfaceDiveResistance(isSurfaceSwim, hasSurfaceDiveIntent);
            ApplyTransportPropulsionForce(transportPropulsionForce, fwdX, fwdY, fwdZ);

            _forceVector = ResolveCriticalEncumbranceSwimForce(_forceVector, IsCriticallyEncumbered);
            Vector3 swimAcceleration = HectonPlayerMotor.ResolveHydrodynamicAddedMassStatelessAcceleration(
                _forceVector,
                _velocity,
                ResolveAuthoritativeBodyMassKg());

            ApplyMotorAcceleration(swimAcceleration);
            ApplySargassumEntanglementForce(transportPreset);
            ApplyAbyssalCableEntanglementForce(transportPreset);
            ApplySargassumMatBuoyancySupport();

            ApplySurfaceAscendDamping(isSurfaceSwim);
        }

        private void CalculateSwimAxes(bool isSurfaceSwim, bool hasSurfaceDiveIntent, float transportForwardPitchInfluence, out float fwdX, out float fwdY, out float fwdZ, out float rightX, out float rightZ)
        {
            ResolveDegreesSinCosFast(ResolveVrSwimmingReferenceYawDegrees(), out float sinBodyYaw, out float cosBodyYaw);
            ResolveDegreesSinCosFast(_cameraPitch, out float sinPitch, out float cosPitch);

            if (isSurfaceSwim && !hasSurfaceDiveIntent)
            {
                Vector3 bodyForward = new Vector3(sinBodyYaw, 0f, cosBodyYaw);
                Vector3 bodyRight = new Vector3(cosBodyYaw, 0f, -sinBodyYaw);
                Vector3 surfaceNormal = EffectiveWaterSurfaceNormal;
                Vector3 surfaceForward = ProjectOnPlaneFast(bodyForward, surfaceNormal);
                Vector3 surfaceRight = ProjectOnPlaneFast(bodyRight, surfaceNormal);

                if (surfaceForward.sqrMagnitude <= 0.0001f)
                    surfaceForward = bodyForward;
                else
                    surfaceForward = NormalizeVectorRsqrt(surfaceForward, bodyForward);

                if (surfaceRight.sqrMagnitude <= 0.0001f)
                    surfaceRight = bodyRight;
                else
                    surfaceRight = NormalizeVectorRsqrt(surfaceRight, bodyRight);

                fwdX = surfaceForward.x;
                fwdY = surfaceForward.y;
                fwdZ = surfaceForward.z;
                rightX = surfaceRight.x;
                rightZ = surfaceRight.z;
            }
            else
            {
                float surfaceDepthT = isSurfaceSwim
                    ? math.saturate(_currentDepth / math.max(surfaceSwimDepthBand, 0.01f))
                    : 1f;
                float surfacePitchBlend = isSurfaceSwim
                    ? math.lerp(1f - surfaceForwardPitchSuppression, 1f, surfaceDepthT)
                    : 1f;
                surfacePitchBlend *= transportForwardPitchInfluence;

                float fwdPlanarScale = math.lerp(1f, cosPitch, surfacePitchBlend);
                fwdX = sinBodyYaw * fwdPlanarScale;
                fwdY = -sinPitch * transportForwardPitchInfluence;
                fwdZ = cosBodyYaw * fwdPlanarScale;
                rightX = cosBodyYaw;
                rightZ = -sinBodyYaw;
            }
        }

        private void ApplySurfaceDiveAssist(bool surfaceDiveAssistActive, float effectiveVerticalForce, float transportSurfaceDiveAssistScale)
        {
            if (surfaceDiveAssistActive)
            {
                float diveAssistT = math.saturate(_surfaceDiveAssistTimer / math.max(surfaceDiveAssistDuration, 0.01f));
                _forceVector.y -= effectiveVerticalForce * surfaceDiveAssistForceMultiplier * transportSurfaceDiveAssistScale * diveAssistT;
            }
        }

        private void ApplySurfaceDiveResistance(bool isSurfaceSwim, bool hasSurfaceDiveIntent)
        {
            if (isSurfaceSwim && hasSurfaceDiveIntent && surfaceDiveResistanceDamping > 0f && _velocity.y < 0f)
            {
                float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
                float surfaceResistanceT = 1f - math.saturate(headDepth / math.max(surfaceDiveBreakDepth, 0.01f));
                if (surfaceResistanceT > 0f)
                {
                    _forceVector.y -= _velocity.y * ResolveAuthoritativeBodyMassKg() * surfaceDiveResistanceDamping * surfaceResistanceT;
                }
            }
        }

        private void ApplyTransportPropulsionForce(float transportPropulsionForce, float fwdX, float fwdY, float fwdZ)
        {
            if (transportPropulsionForce > 0f)
            {
                Vector3 transportPropulsionDirection = new Vector3(_forceVector.x, _forceVector.y, _forceVector.z);
                if (transportPropulsionDirection.sqrMagnitude <= 0.0001f)
                    transportPropulsionDirection = new Vector3(fwdX, fwdY, fwdZ);
                else
                    transportPropulsionDirection = NormalizeVectorRsqrt(transportPropulsionDirection, new Vector3(fwdX, fwdY, fwdZ));

                if (ResolveActiveTransportSource() is MantaScooter mantaScooter &&
                    mantaScooter.TryGetHullStressMisfireDeviation(out Vector2 misfireDeviationDegrees))
                {
                    transportPropulsionDirection = RotateVectorByAxisAnglesDegrees(
                        transportPropulsionDirection,
                        misfireDeviationDegrees.x,
                        misfireDeviationDegrees.y,
                        0f);
                }

                if (math.abs(_abyssalTransportTurbulencePitchOffset) > 0.001f ||
                    math.abs(_abyssalTransportTurbulenceYawOffset) > 0.001f)
                {
                    transportPropulsionDirection = RotateVectorByAxisAnglesDegrees(
                        transportPropulsionDirection,
                        _abyssalTransportTurbulencePitchOffset,
                        _abyssalTransportTurbulenceYawOffset,
                        0f);
                }

                transportPropulsionDirection = ResolveProceduralThrusterNoiseDirection(transportPropulsionDirection);
                _forceVector.x += transportPropulsionDirection.x * transportPropulsionForce;
                _forceVector.y += transportPropulsionDirection.y * transportPropulsionForce;
                _forceVector.z += transportPropulsionDirection.z * transportPropulsionForce;
            }
        }

        private void ApplySurfaceAscendDamping(bool isSurfaceSwim)
        {
            if (isSurfaceSwim && surfaceAscendVelocityDamping > 0f && _velocity.y > 0f)
            {
                if (_velocity.y >= surfaceBreachReleaseVelocity)
                    return;

                float upwardDampingT = 1f - math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                if (upwardDampingT > 0f)
                {
                    _forceVector.x = 0f;
                    _forceVector.y = -_velocity.y * ResolveAuthoritativeBodyMassKg() * surfaceAscendVelocityDamping * upwardDampingT;
                    _forceVector.z = 0f;
                    ApplyMotorAccelerationFromForce(_forceVector);
                }
            }
        }

        private void ApplyHeavyBrineSinkEffect()
        {
            if (TryResolveHeavyBrineSinkMultiplier(ResolvePlayerAupRuntimePosition(), out float brineSinkMultiplier))
            {
                bool thrusterActive = math.abs(_inputVertical) > 0.01f || ResolveActiveTransportPropulsionForce() > 0.01f;
                Vector3 brineVelocity = HectonPlayerMotor.ResolveBuoyancyInversionVelocity(
                    _velocity,
                    true,
                    thrusterActive,
                    brineSinkMultiplier);
                Vector3 brineDelta = brineVelocity - _velocity;
                if (brineDelta.sqrMagnitude > 0.000001f)
                {
                    ApplyMotorVelocityChange(brineDelta);
                    _velocity = brineVelocity;
                }
            }
        }

        private void ApplyCriticalEncumbranceVerticalVelocityGate()
        {
            if (IsCriticallyEncumbered && _velocity.y > 0f)
            {
                _velocity.y = 0f;
                ApplyMotorLinearVelocity(_velocity);
            }
        }

        private float CalculateSwimEffectiveDragCoefficient(SuitData suit, bool isSurfaceSwim)
        {
            float depthDragAdd = PlayerSwimMotor.ResolveDepthDragAdd(
                _currentDepth,
                suit.depthSwimSlowdownStart,
                suit.depthSwimSlowdownEnd,
                suit.depthDragIncreaseMax);

            float effectiveDragCoeff = suit.swimDragCoefficient + depthDragAdd;
            if (isSurfaceSwim)
                effectiveDragCoeff *= surfaceDragMultiplier;

            float sargassumDragMultiplier = ResolveSargassumDragMultiplier();
            float externalEnvironmentalDragMultiplier = ResolveExternalEnvironmentalDragMultiplier();
            effectiveDragCoeff *= sargassumDragMultiplier;
            effectiveDragCoeff *= externalEnvironmentalDragMultiplier;
            effectiveDragCoeff *= ResolveActiveTransportDragCoefficientMultiplier();
            effectiveDragCoeff *= math.lerp(1f, crushDepthDragMultiplier, _hullStressIntensity);
            effectiveDragCoeff *= ResolveEquipmentDragCoefficientMultiplier();

            return effectiveDragCoeff;
        }

        private void ApplyBurstScalarWaterDrag(float speedSq, float effectiveDragCoeff, float brineWaterDensityScale, float fixedDeltaTime)
        {
            if (speedSq > 0.0001f && _surfaceBreachFluidDragBypassTimer <= 0f)
            {
                Vector3 dampedVelocity = ResolvePlayerKinematicsBurstDragVelocity(
                    _velocity,
                    _lastPlayerKinematicsIntendedMovement,
                    effectiveDragCoeff,
                    brineWaterDensityScale,
                    fixedDeltaTime);
                ApplyMotorVelocityChange(dampedVelocity - _velocity);
                _velocity = dampedVelocity;
            }
        }

        private float CalculateEffectiveSwimForce(SuitData suit, float depthSlowdown, float sprintMult, float runtimeSwimSpeedScale, float heavyCarryForceMultiplier, float externalEnvironmentalThrustMultiplier, float sargassumSpeedMultiplier, float shoreSwimBlend)
        {
            float effectiveSwimForce = suit.swimForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            effectiveSwimForce *= heavyCarryForceMultiplier;
            effectiveSwimForce *= externalEnvironmentalThrustMultiplier;
            effectiveSwimForce *= sargassumSpeedMultiplier;
            effectiveSwimForce *= shoreSwimBlend;
            return effectiveSwimForce;
        }

        private float CalculateEffectiveVerticalForce(SuitData suit, float depthSlowdown, float sprintMult, float runtimeSwimSpeedScale, float heavyCarryForceMultiplier, float externalEnvironmentalThrustMultiplier, float sargassumSpeedMultiplier, float shoreSwimBlend)
        {
            float effectiveVerticalForce = suit.swimVerticalForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            effectiveVerticalForce *= _runtimeInventoryUpwardSwimMultiplier;
            effectiveVerticalForce *= heavyCarryForceMultiplier;
            effectiveVerticalForce *= math.lerp(1f, externalEnvironmentalThrustMultiplier, 0.7f);
            effectiveVerticalForce *= math.lerp(1f, sargassumSpeedMultiplier, 0.55f);
            effectiveVerticalForce *= math.lerp(0.45f, 1f, shoreSwimBlend);
            return effectiveVerticalForce;
        }


        private bool TryResolveHeavyBrineSinkMultiplier(Vector3 worldPosition, out float sinkMultiplier)
        {
            sinkMultiplier = 0f;
            ResourceDistributionDirector director = _resourceDistributionRuntime;
            if (!TryResolvePlayerRuntimeShiftOffsetY(worldPosition, out float shiftOffsetY))
                return false;

            if (!PlayerMovementBrineRuntimeSystem.TrySampleBrineLayer(
                    director,
                    worldPosition,
                    false,
                    shiftOffsetY,
                    out BrineLayerSample sample,
                    out bool submerged) ||
                !submerged)
            {
                return false;
            }

            float fluidDensityKgPerCubicMeter = ReferenceSeaWaterDensityKgPerCubicMeter * math.max(1f, sample.DensityMultiplier);
            sinkMultiplier = HectonPlayerMotor.ResolveHeavyBrineSinkMultiplier(
                fluidDensityKgPerCubicMeter,
                ReferenceSeaWaterDensityKgPerCubicMeter);
            return sinkMultiplier < 0f;
        }

        private void ApplySargassumRestRecovery(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float fieldDensity01 = ResolveSargassum01(_sargassumFieldDensity01);
            float matBuoyancyBlend = ResolveSargassum01(_sargassumMatBuoyancyBlend);
            float restDensityThreshold = math.isfinite(sargassumRestDensityThreshold)
                ? math.saturate(sargassumRestDensityThreshold)
                : 0.9f;
            float targetBlend = 0f;
            if (_survivalSystem != null &&
                safeFixedDeltaTime > 0f &&
                !IsInDryInterior() &&
                _isSurfaceSwimming &&
                _wipeoutTimer <= 0f &&
                fieldDensity01 > restDensityThreshold &&
                matBuoyancyBlend > 0.05f)
            {
                float densityRange = math.max(1f - restDensityThreshold, 0.0001f);
                float densityT = math.saturate((fieldDensity01 - restDensityThreshold) / densityRange);
                float maxRestSpeed = math.max(0.01f, ResolveSargassumPositive(sargassumRestMaxSpeed, 0.4f));
                float speedSq = ResolveAuthoritativeLinearVelocity(Vector3.zero).sqrMagnitude;
                if (!math.isfinite(speedSq))
                    speedSq = 0f;

                float stillnessT = 1f - math.saturate(speedSq / (maxRestSpeed * maxRestSpeed));
                float absInputH = math.isfinite(_inputH) ? math.abs(_inputH) : 0f;
                float absInputV = math.isfinite(_inputV) ? math.abs(_inputV) : 0f;
                float planarInputIntent = math.max(absInputH, absInputV) + (0.375f * math.min(absInputH, absInputV));
                float inputIntent = math.max(planarInputIntent, math.isfinite(_inputVertical) ? math.abs(_inputVertical) : 0f);
                float inputCalmT = 1f - math.saturate(inputIntent / math.max(0.01f, ResolveSargassumPositive(sargassumRestMaxInputIntent, 0.18f)));
                float headDepth = ResolveSargassumNonNegative(GetHeadDepthBelowSurface(EffectiveWaterSurfaceY), 0f);
                float breathingT = 1f - math.saturate(headDepth / math.max(0.0001f, ResolveSargassumPositive(sargassumRestMaxHeadDepth, 0.03f)));
                targetBlend = densityT * stillnessT * inputCalmT * breathingT * matBuoyancyBlend;
            }

            float blendT = ResolveLinearBlendT(math.max(0.01f, ResolveSargassumPositive(sargassumRestBlendSharpness, 6f)), safeFixedDeltaTime);
            _sargassumRestRecoveryBlend = math.lerp(_sargassumRestRecoveryBlend, targetBlend, blendT);
            _sargassumRestRecoveryBlend = ResolveSargassum01(_sargassumRestRecoveryBlend);
            if (_sargassumRestRecoveryBlend <= 0.001f)
            {
                _sargassumRestRecoveryBlend = 0f;
                return;
            }

            if (_survivalSystem == null)
                return;

            float oxygenRestorePerSecond = ResolveSargassumNonNegative(sargassumRestOxygenRestorePerSecond, 8f);
            if (oxygenRestorePerSecond > 0f)
                _survivalSystem.RefillOxygen(oxygenRestorePerSecond * _sargassumRestRecoveryBlend * safeFixedDeltaTime);

            float energyRestorePerSecond = ResolveSargassumNonNegative(sargassumRestEnergyRestorePerSecond, 1.35f);
            if (energyRestorePerSecond > 0f)
                _survivalSystem.RechargeEnergy(energyRestorePerSecond * _sargassumRestRecoveryBlend * safeFixedDeltaTime);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  WALK PHYSICS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void WalkPhysics(SuitData suit, float fixedDeltaTime)
        {
            if (IsCriticalStaminaFailureActive)
                return;

            if (_inputH == 0f && _inputV == 0f) return;

            bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            bool dryInteriorActive = _currentLocomotionMode == PlayerLocomotionMode.DryInteriorWalk;

            if (_activeTransportPlatformTransform != null)
            {
                _moveDirection = ResolveTransportPlatformRelativeWalkInputWorldDirection(_inputH, _inputV);
            }
            else
            {
                ResolveDegreesSinCosFast(_bodyYaw, out float sinYaw, out float cosYaw);

                _moveDirection.x = sinYaw * _inputV + cosYaw * _inputH;
                _moveDirection.y = 0f;
                _moveDirection.z = cosYaw * _inputV - sinYaw * _inputH;
            }

            float sqrMag = _moveDirection.sqrMagnitude;
            if (sqrMag > 1.0001f)
            {
                float invMag = math.rsqrt(math.max(sqrMag, 0.000001f));
                _moveDirection.x *= invMag;
                _moveDirection.y *= invMag;
                _moveDirection.z *= invMag;
            }

            if (_ladderSplineSnapActive)
            {
                float3 move3 = new float3(_moveDirection.x, 0f, _moveDirection.z);
                float3 axis3 = new float3(_ladderSplineSnapAxisWorld.x, 0f, _ladderSplineSnapAxisWorld.z);
                float axisSq = math.lengthsq(axis3);
                if (axisSq > 0.000001f)
                {
                    move3 = math.project(move3, axis3);
                    _moveDirection.x = move3.x;
                    _moveDirection.y = 0f;
                    _moveDirection.z = move3.z;
                }
            }

            _lastPlayerKinematicsIntendedMovement = new float3(_moveDirection.x, 0f, _moveDirection.z);

            if (_isGrounded)
            {
                _moveDirection = ProjectOnPlaneFast(_moveDirection, _smoothedGroundNormal);
                float projSqr = _moveDirection.sqrMagnitude;
                if (projSqr > 0.0001f)
                {
                    float invMag = math.rsqrt(math.max(projSqr, 0.0001f));
                    _moveDirection.x *= invMag;
                    _moveDirection.y *= invMag;
                    _moveDirection.z *= invMag;
                }
            }

            float wadeMultiplier = exosuitActive ? 1f : 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
            wadeMultiplier = math.max(wadeMultiplier, 0.2f);
            float sprintMult = CanUseLandSprint() ? suit.sprintMultiplier : 1f;
            float force =
                suit.walkForce *
                wadeMultiplier *
                sprintMult *
                _runtimeStaminaMultiplier *
                ResolveRuntimeInventoryLoadMovementMultiplier() *
                ResolveHeavyCarryForceMultiplier() *
                ResolveExternalEnvironmentalThrustMultiplier();

            if (exosuitActive)
                force *= exosuitWalkForceMultiplier;
            else if (dryInteriorActive)
                force *= dryInteriorWalkForceMultiplier;

            if (IsDryLandAirborne())
                force *= dryAirControlMultiplier;

            _forceVector.x = _moveDirection.x * force;
            _forceVector.y = _moveDirection.y * force;
            _forceVector.z = _moveDirection.z * force;
            ApplyMotorForce(_forceVector);
        }

        private bool ShouldUseLandLocomotion(float physicsImmersion, bool hasShoreGroundSupport, bool hasImmediateShoreFooting)
        {
            if (IsInDryInterior())
                return true;

            if (IsExosuitTransportActive())
                return true;

            if (physicsImmersion <= 0.01f)
                return true;

            if (hasImmediateShoreFooting && _shoreBuoyancyBlend <= shoreWalkHandoffBuoyancyThreshold)
                return true;

            if (physicsImmersion >= swimTransitionThreshold)
                return false;

            return hasShoreGroundSupport && _shoreBuoyancyBlend <= shoreWalkHandoffBuoyancyThreshold;
        }

        private bool IsDryLandAirborne()
        {
            if (!_isWalking || _isGrounded)
                return false;

            if (_dryGroundGraceTimer > 0f)
                return false;

            if (_shoreGroundGraceTimer > 0f)
                return false;

            return _waterImmersionRatio <= 0.01f;
        }

        private bool CanUseLandSprint()
        {
            if (!_isSprinting || !_isWalking)
                return false;

            if (IsHeavyCarryActive())
                return false;

            if (_isGrounded)
                return true;

            if (_dryGroundGraceTimer > 0f)
                return true;

            return _shoreGroundGraceTimer > 0f;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  AMBIENT CURRENT
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplyAmbientCurrent(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
            {
                _abyssalFlowAdvectionVelocityWS = Vector3.zero;
                return;
            }

            float currentInfluenceScale = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            if (currentInfluenceScale <= 0f) return;

            AbsoluteUniversePosition playerAup = _playerState.AbsolutePosition;
            float3 currentPosition3 = playerAup.ToRuntimeFloat3();
            Vector3 currentPosition = new Vector3(currentPosition3.x, currentPosition3.y, currentPosition3.z);
            float shoreCurrentScale = _isSurfaceSwimming
                ? math.lerp(0.15f, 1f, _shoreBuoyancyBlend)
                : 1f;
            float ambientAdvectionScale = _waterImmersionRatio * currentInfluenceScale * shoreCurrentScale;

            float ambientCurrentX = 0f;
            float ambientCurrentY = 0f;
            float ambientCurrentZ = 0f;
            if (suit.ambientCurrentStrength > 0f && ambientAdvectionScale > 0.0001f)
            {
                float strength = suit.ambientCurrentStrength * ambientAdvectionScale;
                Unity.Mathematics.float3 phantom = CurrentManager.SampleCurrent(
                    new Unity.Mathematics.float3(currentPosition.x, currentPosition.y, currentPosition.z),
                    _currentTimer,
                    0.018f,
                    0.12f,
                    strength,
                    0.2f);
                Vector3 localVolumeCurrent = SampleAuthoredAmbientCurrent(currentPosition) * ambientAdvectionScale;
                ambientCurrentX = phantom.x + localVolumeCurrent.x;
                ambientCurrentY = phantom.y + localVolumeCurrent.y;
                ambientCurrentZ = phantom.z + localVolumeCurrent.z;
            }

            if (_abyssalThermalFlowVelocityWS.sqrMagnitude > 0.0001f && ambientAdvectionScale > 0.0001f)
            {
                ambientCurrentX += _abyssalThermalFlowVelocityWS.x * ambientAdvectionScale;
                ambientCurrentY += _abyssalThermalFlowVelocityWS.y * ambientAdvectionScale;
                ambientCurrentZ += _abyssalThermalFlowVelocityWS.z * ambientAdvectionScale;
            }

            ApplyAbyssalFlowAdvection(
                currentPosition,
                fixedDeltaTime,
                ambientAdvectionScale);

            float crestFlowForceX = 0f;
            float crestFlowForceZ = 0f;
            if (_crestFlowSamplingSucceeded && crestFlowVelocityScale > 0f && crestFlowForceResponsiveness > 0f)
            {
                float inputDriftScale = ResolveCrestFlowInputAttenuation(fixedDeltaTime);
                float modeDriftScale = _isSurfaceSwimming ? _shoreBuoyancyBlend * shoreCurrentScale : 0.8f;
                float planarInputMagnitude = ApproximatePlanarMagnitude(_inputH, _inputV);
                bool surfaceIdleDrift = _isSurfaceSwimming && planarInputMagnitude <= crestFlowIdleInputThreshold && math.abs(_inputVertical) <= crestFlowIdleInputThreshold;
                if (surfaceIdleDrift)
                    modeDriftScale *= crestFlowSurfaceIdleBoost;
                float crestForceScale =
                    crestFlowVelocityScale *
                    crestFlowForceResponsiveness *
                    currentInfluenceScale *
                    inputDriftScale *
                    modeDriftScale;
                float mass = ResolveAuthoritativeBodyMassKg();
                crestFlowForceX = EffectiveWaterFlowVelocity.x * mass * crestForceScale;
                crestFlowForceZ = EffectiveWaterFlowVelocity.z * mass * crestForceScale;
            }

            _forceVector.x = ambientCurrentX + crestFlowForceX;
            _forceVector.y = ambientCurrentY;
            _forceVector.z = ambientCurrentZ + crestFlowForceZ;
            ApplyMotorAccelerationFromForce(_forceVector);
        }

        private void ApplyAbyssalFlowAdvection(
            Vector3 flowSamplePosition,
            float fixedDeltaTime,
            float advectionScale)
        {
            Vector3 targetAdvectionVelocity = Vector3.zero;
            bool hasFlowSample = false;
            if (advectionScale > 0.0001f &&
                TryResolveVegetationBridge(out HectonMapMagicVegetationBridge bridge))
            {
                if (bridge.TrySampleAbyssalFlow(flowSamplePosition, out Vector3 sampledFlow))
                {
                    sampledFlow = bridge.ApplyAbyssalFlowNoise(sampledFlow, flowSamplePosition);
                    targetAdvectionVelocity = HectonPlayerMotor.SafeVelocity(sampledFlow * advectionScale);
                    hasFlowSample = targetAdvectionVelocity.sqrMagnitude > 0.000001f;
                }
            }

            if (!hasFlowSample)
            {
                _abyssalFlowAdvectionVelocityWS = Vector3.zero;
                return;
            }

            float wetMassKg = ResolveAuthoritativeBodyMassKg() + _runtimeInventoryTotalMassKg;
            float massGripScale = math.rcp(1f + wetMassKg * math.rcp(120f));
            if (IsExosuitTransportActive())
                massGripScale *= 0.55f;
            float flowGrip = math.max(abyssalFlowAdvectionSharpness, 0.01f) * massGripScale;
            float blendT = math.saturate(flowGrip * fixedDeltaTime);
            Vector3 playerVelocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
            float3 playerVelocity3 = new float3(playerVelocity.x, playerVelocity.y, playerVelocity.z);
            float3 targetFlowVelocity3 = new float3(targetAdvectionVelocity.x, targetAdvectionVelocity.y, targetAdvectionVelocity.z);
            float3 resolvedVelocity3 = math.lerp(playerVelocity3, targetFlowVelocity3, blendT);
            float3 flowVelocityChange3 = resolvedVelocity3 - playerVelocity3;
            Vector3 flowVelocityChange = new Vector3(flowVelocityChange3.x, flowVelocityChange3.y, flowVelocityChange3.z);
            _abyssalFlowAdvectionVelocityWS = flowVelocityChange;
            if (flowVelocityChange.sqrMagnitude > 0.000001f)
                QueueEnvironmentalVelocityChange(flowVelocityChange);
        }

        private float ResolveCrestFlowInputAttenuation(float fixedDeltaTime)
        {
            float desiredScale = 1f;
            float planarInputMagnitude = ApproximatePlanarMagnitude(_inputH, _inputV);
            float verticalInputMagnitude = math.abs(_inputVertical);
            Vector3 flowVelocity = EffectiveWaterFlowVelocity;
            float flowSqrMagnitude = flowVelocity.x * flowVelocity.x + flowVelocity.z * flowVelocity.z;

            if (flowSqrMagnitude > 0.0001f)
            {
                if (planarInputMagnitude > 0.001f)
                {
                    ResolveDegreesSinCosFast(_bodyYaw, out float sinYaw, out float cosYaw);

                    float desiredX = sinYaw * _inputV + cosYaw * _inputH;
                    float desiredZ = cosYaw * _inputV - sinYaw * _inputH;
                    float desiredSqrMagnitude = desiredX * desiredX + desiredZ * desiredZ;

                    if (desiredSqrMagnitude > 0.0001f)
                    {
                        float desiredInvMagnitude = math.rsqrt(math.max(desiredSqrMagnitude, 0.0001f));
                        desiredX *= desiredInvMagnitude;
                        desiredZ *= desiredInvMagnitude;

                        float flowInvMagnitude = math.rsqrt(math.max(flowSqrMagnitude, 0.0001f));
                        float flowDirX = flowVelocity.x * flowInvMagnitude;
                        float flowDirZ = flowVelocity.z * flowInvMagnitude;
                        float alignment = math.clamp(desiredX * flowDirX + desiredZ * flowDirZ, -1f, 1f);
                        float opposingFactor = math.saturate(-alignment);
                        float neutralFactor = 1f - math.abs(alignment);
                        float inputT = math.saturate(planarInputMagnitude);
                        float directionalScale = 1f - (crestFlowOppositionReduction * opposingFactor + crestFlowCrossCurrentReduction * neutralFactor) * inputT;
                        desiredScale = math.clamp(directionalScale, crestFlowInputMinimumScale, 1f);
                    }
                }

                if (verticalInputMagnitude > 0.001f)
                {
                    float verticalScale = 1f - crestFlowCrossCurrentReduction * math.saturate(verticalInputMagnitude);
                    if (verticalScale < desiredScale)
                        desiredScale = math.clamp(verticalScale, crestFlowInputMinimumScale, 1f);
                }
            }

            float blendT = ResolveLinearBlendT(crestFlowInputBlendSpeed, fixedDeltaTime);
            _crestFlowInputAttenuation = math.lerp(_crestFlowInputAttenuation, desiredScale, blendT);
            return _crestFlowInputAttenuation;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  VELOCITY CLAMP
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ClampVelocity(SuitData suit)
        {
            if (_impulseBypassTimer > 0f)
                return;

            _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);

            if (_isWalking)
            {
                bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
                bool dryInteriorActive = _currentLocomotionMode == PlayerLocomotionMode.DryInteriorWalk;
                float maxSpd = suit.maxWalkSpeed;
                float wadeMultiplier = exosuitActive ? 1f : 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
                maxSpd *= math.max(wadeMultiplier, 0.2f);
                if (CanUseLandSprint()) maxSpd *= suit.sprintMultiplier;
                maxSpd *= _runtimeStaminaMultiplier;
                maxSpd *= ResolveRuntimeInventoryLoadMovementMultiplier();
                maxSpd *= ResolveHeavyCarrySpeedMultiplier();
                maxSpd *= ResolveExternalEnvironmentalSpeedMultiplier();
                maxSpd *= HectonPlayerMotor.ResolveStorageBackpressureSpeedMultiplier(SystemDispatcher.StreamingStorageDebt01);
                maxSpd *= _runtimeEmergencyMovementMultiplier;
                if (exosuitActive)
                    maxSpd *= exosuitWalkSpeedMultiplier;
                else if (dryInteriorActive)
                    maxSpd *= dryInteriorWalkSpeedMultiplier;

                if (maxSpd > 0f)
                {
                    if (_isGrounded)
                    {
                        Vector3 planarVelocity = ProjectOnPlaneFast(_velocity, _smoothedGroundNormal);
                        float planarSqr = planarVelocity.sqrMagnitude;
                        float maxSqr = maxSpd * maxSpd;
                        if (planarSqr > maxSqr)
                        {
                            float scale = maxSpd * math.rsqrt(math.max(planarSqr, 0.000001f));
                            Vector3 normalVelocity = _velocity - planarVelocity;
                            planarVelocity.x *= scale;
                            planarVelocity.y *= scale;
                            planarVelocity.z *= scale;
                            ApplyMotorLinearVelocity(planarVelocity + normalVelocity);
                        }
                    }
                    else
                    {
                        float xzSqr = _velocity.x * _velocity.x + _velocity.z * _velocity.z;
                        float maxSqr = maxSpd * maxSpd;
                        if (xzSqr > maxSqr)
                        {
                            float scale = maxSpd * math.rsqrt(math.max(xzSqr, 0.000001f));
                            _velocity.x *= scale; _velocity.z *= scale;
                            ApplyMotorLinearVelocity(_velocity);
                        }
                    }
                }
            }
            else
            {
                float maxSpd = suit.maxSwimSpeed * (_runtimeSwimSpeedMultiplier * _runtimeVoxelBackpressureSwimSpeedMultiplier * _runtimeInjurySwimSpeedMultiplier * _runtimeEmergencyMovementMultiplier * _runtimeStaminaMultiplier * ResolveRuntimeInventoryLoadMovementMultiplier());
                if (_isSurfaceSwimming)
                {
                    maxSpd *= surfaceMaxSpeedMultiplier;
                    maxSpd *= math.lerp(0.45f, 1f, _shoreBuoyancyBlend);
                }
                if (_isSprinting && !IsHeavyCarryActive()) maxSpd *= suit.sprintMultiplier;
                maxSpd *= ResolveHeavyCarrySpeedMultiplier();
                maxSpd *= ResolveSargassumSpeedMultiplier();
                maxSpd *= ResolveExternalEnvironmentalSpeedMultiplier();
                maxSpd *= HectonPlayerMotor.ResolveStorageBackpressureSpeedMultiplier(SystemDispatcher.StreamingStorageDebt01);
                maxSpd *= _transportCavitationEfficiency;
                maxSpd *= CurrentAbyssalShearSpeedMultiplier;
                maxSpd *= ResolveActiveTransportSpeedMultiplier() * ResolveWipeoutTransportControl01();
                if (maxSpd > 0f)
                {
                    float fullSqr = _velocity.x * _velocity.x + _velocity.y * _velocity.y + _velocity.z * _velocity.z;
                    float maxSqr = maxSpd * maxSpd;
                    if (fullSqr > maxSqr)
                    {
                        float scale = maxSpd * math.rsqrt(math.max(fullSqr, 0.000001f));
                        _velocity.x *= scale; _velocity.y *= scale; _velocity.z *= scale;
                        ApplyMotorLinearVelocity(_velocity);
                    }
                }
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SPRING UTILITY
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private static float SpringDamp(float current, float target, ref float velocity, float omega, float dt)
        {
            float n1 = velocity - (current - target) * (omega * omega * dt);
            float n2 = 1f + omega * dt;
            velocity = n1 / (n2 * n2);
            return current + velocity * dt;
        }

        private static float SpringDampAngle(float current, float target, ref float velocity, float omega, float dt)
        {
            float adjustedTarget = current + DeltaAngleDegrees(current, target);
            float dampedAngle = SpringDamp(current, adjustedTarget, ref velocity, omega, dt);
            return NormalizeSignedAngle(dampedAngle);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  DIAGNOSTICS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateModeDiagnostics()
        {
            _debugIsWalking = _isWalking;
            int modeIndex = (int)_currentLocomotionMode;
            _debugLocomotionMode = (uint)modeIndex < (uint)_locomotionModeLabels.Length
                ? _locomotionModeLabels[modeIndex]
                : "Unknown";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateGroundDiagnostics() { _debugIsGrounded = _isGrounded; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateStepDiagnostics() { _debugStepEvent = true; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSuitDiagnostics()
        {
            _debugSuitName = currentSuitData != null ? currentSuitData.name : "NONE";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateCrestDiagnostics()
        {
            _debugCrestAvailable = _crestAvailable;
            _debugDynamicWaterY = _dynamicWaterSurfaceY;
            _debugCrestSampling = _crestSamplingSucceeded;
            _debugSurfaceWavePitch = ExtractLocalPitchDegrees(_surfaceWavePoseRotation);
            _debugSurfaceWaveRoll = ExtractLocalRollDegrees(_surfaceWavePoseRotation);
            _debugStormIntensity01 = _dynamicStormIntensity;
            _debugWaveHeightSpan = _dynamicWaveHeightSpan;
            _debugTransportCavitationEfficiency = _transportCavitationEfficiency;
            _debugShoreBuoyancyBlend = _shoreBuoyancyBlend;
            _debugBottomClearance = float.IsPositiveInfinity(_bottomClearance) ? -1f : _bottomClearance;
            _debugWetLensIntensity = _wetLensSignalIntensity;
            _debugWaveSlopeForward = _dynamicWaveLocalSlope.y;
            _debugWaveSlopeLateral = _dynamicWaveLocalSlope.x;
            _debugUndertowIntensity = _undertowIntensity;
            _debugWipeoutTimer = _wipeoutTimer;
            _debugDynamicCollisionTuck = _dynamicCollisionTuck01;
            _debugAbyssalCurrentIntensity = _abyssalDowndraftIntensity;
            bool heavyTowActive = RefreshHeavyTowActive();
            _debugHeavyTowActive = heavyTowActive;
            _debugHeavyTowTension01 = heavyTowActive ? _heavyTowWinch.CurrentTension01 : 0f;
            _debugHeavyTowStress01 = heavyTowActive ? _heavyTowWinch.CurrentStress01 : 0f;
            _debugHeavyTowDragMultiplier = heavyTowActive ? _heavyTowWinch.CurrentTowDragMultiplier : 1f;
            _debugHeavyTowSignedLateralPull = heavyTowActive ? _heavyTowWinch.CurrentSignedLateralPull01 : 0f;
            _debugHeavyTowBackwardPull = heavyTowActive ? _heavyTowWinch.CurrentBackwardPull01 : 0f;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float speed)
        {
            _debugCurrentRoll = _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
            _debugImmersionRatio = _waterImmersionRatio;
            _debugSmoothedImmersion = _smoothedImmersionRatio;
            _debugGravityScale = _gravityScale;
            _debugSnapScale = _snapScale;
            _debugBodyYaw = _bodyYaw;
            _debugCameraYaw = _cameraYaw;
            _debugSpeed = speed;
            _debugDynamicWaterY = EffectiveWaterSurfaceY;
            _debugCrestAvailable = _crestAvailable;
            _debugCrestSampling = _crestSamplingSucceeded;
            _debugDepth = _currentDepth;
            _debugFovOffset = _juiceOutput.fovOffset;
            _debugSplashThisFrame = _juiceProcessor != null && _juiceProcessor.SplashThisFrame;
            _debugExhaleThisFrame = _juiceProcessor != null && _juiceProcessor.ExhaleThisFrame;
            _debugIsSubmerged = IsPlayerSubmerged;
            _debugHeavyCarryActive = IsHeavyCarryActive();
            _debugHeavyCarryForceMultiplier = ResolveHeavyCarryForceMultiplier();
            _debugHeavyCarrySpeedMultiplier = ResolveHeavyCarrySpeedMultiplier();
            _debugSargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
            _debugSargassumDragMultiplier = ResolveSargassumDragMultiplier();
            _debugSargassumEntangled = _sargassumMovementInfluence != null && _sargassumMovementInfluence.Entanglement01 > 0.01f;
            _debugSargassumEntanglement01 = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.Entanglement01 : 0f;
            _debugSargassumFieldDensity01 = ResolveSargassum01(_sargassumFieldDensity01);
            _debugSargassumMatBuoyancy01 = ResolveSargassum01(_sargassumMatBuoyancyBlend);
            if (_sargassumMovementInfluence == null)
                _debugSargassumEntanglementDragRequest = 1f;
            _debugExternalEnvironmentalDragMultiplier = ResolveExternalEnvironmentalDragMultiplier();
            _debugExternalEnvironmentalSpeedMultiplier = ResolveExternalEnvironmentalSpeedMultiplier();
            _debugExternalEnvironmentalThrustMultiplier = ResolveExternalEnvironmentalThrustMultiplier();
            _debugSurfaceWavePitch = ExtractLocalPitchDegrees(_surfaceWavePoseRotation);
            _debugSurfaceWaveRoll = ExtractLocalRollDegrees(_surfaceWavePoseRotation);
            _debugStormIntensity01 = _dynamicStormIntensity;
            _debugWaveHeightSpan = _dynamicWaveHeightSpan;
            _debugTransportCavitationEfficiency = _transportCavitationEfficiency;
            _debugShoreBuoyancyBlend = _shoreBuoyancyBlend;
            _debugBottomClearance = float.IsPositiveInfinity(_bottomClearance) ? -1f : _bottomClearance;
            _debugWetLensIntensity = _wetLensSignalIntensity;
            _debugWaveSlopeForward = _dynamicWaveLocalSlope.y;
            _debugWaveSlopeLateral = _dynamicWaveLocalSlope.x;
            _debugUndertowIntensity = _undertowIntensity;
            _debugWipeoutTimer = _wipeoutTimer;
            _debugDynamicCollisionTuck = _dynamicCollisionTuck01;
            _debugAbyssalCurrentIntensity = _abyssalDowndraftIntensity;
            bool heavyTowActive = RefreshHeavyTowActive();
            _debugHeavyTowActive = heavyTowActive;
            _debugHeavyTowTension01 = heavyTowActive ? _heavyTowWinch.CurrentTension01 : 0f;
            _debugHeavyTowStress01 = heavyTowActive ? _heavyTowWinch.CurrentStress01 : 0f;
            _debugHeavyTowDragMultiplier = heavyTowActive ? _heavyTowWinch.CurrentTowDragMultiplier : 1f;
            _debugHeavyTowSignedLateralPull = heavyTowActive ? _heavyTowWinch.CurrentSignedLateralPull01 : 0f;
            _debugHeavyTowBackwardPull = heavyTowActive ? _heavyTowWinch.CurrentBackwardPull01 : 0f;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  EDITOR
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ValidateMovementAndVRSettings();
            ValidateExosuitAndInteractionSettings();
            ValidateSargassumAndAbyssalSettings();
            ValidateImpactAndSurfaceSettings();
            ValidateHazardAndMiscSettings();

            RefreshGroundSlopeCache();
            CacheBaseCollisionProfile();
        }

        private void ValidateMovementAndVRSettings()
        {

            mouseSensitivity = math.max(mouseSensitivity, 0.01f);
            groundCheckRadius = math.max(groundCheckRadius, 0.01f);
            groundCheckDistance = math.max(groundCheckDistance, 0.01f);
            dryGroundGraceTime = math.clamp(dryGroundGraceTime, 0f, 0.3f);
            maxGroundAngle = math.clamp(maxGroundAngle, 5f, 89f);
            pitchMin = math.max(pitchMin, -89.9f);
            pitchMax = math.min(pitchMax, 89.9f);
            pitchMin = math.min(pitchMin, pitchMax);
            vrSnapTurnDegrees = math.clamp(vrSnapTurnDegrees, 15f, 60f);
            vrSnapTurnThreshold = math.clamp(vrSnapTurnThreshold, 0.25f, 0.98f);
            vrSnapTurnRearmThreshold = math.clamp(vrSnapTurnRearmThreshold, 0.01f, 0.6f);
            if (vrSnapTurnRearmThreshold >= vrSnapTurnThreshold)
                vrSnapTurnRearmThreshold = math.max(0.01f, vrSnapTurnThreshold * 0.5f);
            vrSnapTurnFadeSeconds = math.clamp(vrSnapTurnFadeSeconds, 0.05f, 0.2f);
            vrSmoothTurnDegreesPerSecond = math.clamp(vrSmoothTurnDegreesPerSecond, 15f, 180f);
            vrSmoothTurnDeadzone = math.clamp(vrSmoothTurnDeadzone, 0f, 0.35f);
            vrHeadRelativeSwimBiasDefault = math.saturate(vrHeadRelativeSwimBiasDefault);
            vrManualRollInputThreshold = math.saturate(vrManualRollInputThreshold);
            vrCameraLocalMotionSuppression = math.saturate(vrCameraLocalMotionSuppression);
            vrComfortVignetteSharpness = math.clamp(vrComfortVignetteSharpness, 1f, 20f);
            vrComfortVisualDecaySharpness = math.clamp(vrComfortVisualDecaySharpness, 0.5f, 20f);
            vrComfortHighSpeedMetersPerSecond = math.clamp(vrComfortHighSpeedMetersPerSecond, 0.25f, 25f);
            vrComfortYawRateReference = math.clamp(vrComfortYawRateReference, 15f, 360f);
            playerHeight = math.max(playerHeight, 0.5f);
            baseFov = math.clamp(baseFov, 30f, 120f);
            cinematicFocusPullStrength = math.clamp(cinematicFocusPullStrength, 0f, 12f);
            cinematicFocusInputBreakThreshold = math.clamp(cinematicFocusInputBreakThreshold, 0.1f, 30f);
            cinematicFocusYieldRecoverySharpness = math.clamp(cinematicFocusYieldRecoverySharpness, 0.25f, 20f);
            cinematicFocusFov = math.clamp(cinematicFocusFov, 35f, 120f);
            cinematicFocusFovSharpness = math.clamp(cinematicFocusFovSharpness, 0.5f, 20f);
            cinematicFocusDefaultDuration = math.clamp(cinematicFocusDefaultDuration, 0.25f, 12f);
            cinematicFocusSubtitleFadeDistance = math.clamp(cinematicFocusSubtitleFadeDistance, 4f, 120f);
            exosuitJumpJetScooterDrainMultiplier = math.clamp(exosuitJumpJetScooterDrainMultiplier, 1f, 10f);
            exosuitNegativeBuoyancyScale = math.clamp(exosuitNegativeBuoyancyScale, 1f, 3f);
            exosuitFootProbeLateralOffset = math.clamp(exosuitFootProbeLateralOffset, 0.1f, 1.5f);
            exosuitFootProbeForwardOffset = math.clamp(exosuitFootProbeForwardOffset, -0.5f, 1f);
            exosuitFootProbeHeight = math.clamp(exosuitFootProbeHeight, 0.05f, 1.5f);
            exosuitFootProbeDistance = math.clamp(exosuitFootProbeDistance, 0.1f, 3f);
            exosuitMinGroundNormalY = math.clamp(exosuitMinGroundNormalY, 0.05f, 0.8f);
            exosuitFootSlopeBlendSharpness = math.clamp(exosuitFootSlopeBlendSharpness, 1f, 40f);
            exosuitSlopeStickForceMultiplier = math.clamp(exosuitSlopeStickForceMultiplier, 1f, 4f);
            exosuitGroundSnapForceMultiplier = math.clamp(exosuitGroundSnapForceMultiplier, 1f, 4f);
            exosuitJumpJetWakeTrailScale = math.clamp(exosuitJumpJetWakeTrailScale, 0f, 2f);
            exosuitJumpJetWakePulseInterval = math.clamp(exosuitJumpJetWakePulseInterval, 0.02f, 0.4f);
            exosuitFootstepSonarPingRadius = math.clamp(exosuitFootstepSonarPingRadius, 5f, 40f);
            exosuitFootstepSonarRevealDuration = math.clamp(exosuitFootstepSonarRevealDuration, 0.1f, 2f);
            exosuitFootstepThreatStrength = math.clamp(exosuitFootstepThreatStrength, 0f, 2f);
            exosuitFootstepThreatHoldDuration = math.clamp(exosuitFootstepThreatHoldDuration, 0.1f, 1f);
            baseFloorMetalFootstepVolume = math.clamp(baseFloorMetalFootstepVolume, 0f, 1f);
            baseFloorMetalFootstepPitch = math.clamp(baseFloorMetalFootstepPitch, 0.5f, 1.5f);
            exosuitGrappleRestLength = math.clamp(exosuitGrappleRestLength, 0.2f, 4f);
            exosuitGrappleReelForce = math.clamp(exosuitGrappleReelForce, 0f, 180f);
        }

        private void ValidateExosuitAndInteractionSettings()
        {
            exosuitGrappleSpring = math.clamp(exosuitGrappleSpring, 0f, 160f);
            exosuitGrappleDamping = math.clamp(exosuitGrappleDamping, 0f, 60f);
            exosuitGrappleMaxForce = math.clamp(exosuitGrappleMaxForce, 0f, 220f);
            exosuitGrappleHoldTime = math.clamp(exosuitGrappleHoldTime, 0f, 0.35f);
            dryInteriorWalkForceMultiplier = math.clamp(dryInteriorWalkForceMultiplier, 0.1f, 2f);
            dryInteriorWalkSpeedMultiplier = math.clamp(dryInteriorWalkSpeedMultiplier, 0.1f, 2f);
            dryInteriorFootProbeLateralOffset = math.clamp(dryInteriorFootProbeLateralOffset, 0.05f, 1f);
            dryInteriorFootProbeForwardOffset = math.clamp(dryInteriorFootProbeForwardOffset, -0.25f, 0.5f);
            dryInteriorFootProbeHeight = math.clamp(dryInteriorFootProbeHeight, 0.05f, 1f);
            dryInteriorFootProbeDistance = math.clamp(dryInteriorFootProbeDistance, 0.1f, 2f);
            cuttingTensionRestLength = math.clamp(cuttingTensionRestLength, 0.2f, 3f);
            cuttingTensionSpring = math.clamp(cuttingTensionSpring, 0f, 120f);
            cuttingTensionDamping = math.clamp(cuttingTensionDamping, 0f, 40f);
            cuttingTensionMaxForce = math.clamp(cuttingTensionMaxForce, 0f, 120f);
            cuttingTensionHoldTime = math.clamp(cuttingTensionHoldTime, 0f, 0.25f);
            fatalPressureMinFov = math.clamp(fatalPressureMinFov, 15f, 25f);
            wipeoutSuitUpgradeBreakChance = math.clamp(wipeoutSuitUpgradeBreakChance, 0f, 1f);
            fatalPressureLookSensitivityFloor = math.clamp(fatalPressureLookSensitivityFloor, 0f, 0.35f);
            fatalPressureYawFreedomStart = math.max(fatalPressureYawFreedomStart, 5f);
            fatalPressureYawFreedomEnd = math.max(fatalPressureYawFreedomEnd, 1f);
            fatalPressureYawFreedomStart = math.max(fatalPressureYawFreedomStart, fatalPressureYawFreedomEnd);
            fatalPressurePitchFreedomStart = math.max(fatalPressurePitchFreedomStart, 5f);
            fatalPressurePitchFreedomEnd = math.max(fatalPressurePitchFreedomEnd, 1f);
            fatalPressurePitchFreedomStart = math.max(fatalPressurePitchFreedomStart, fatalPressurePitchFreedomEnd);
            surfaceSwimDepthBand = math.max(surfaceSwimDepthBand, 0.1f);
            surfaceAscendReleaseDepth = math.max(surfaceAscendReleaseDepth, 0.02f);
            surfaceDivePitchCommit = math.clamp(surfaceDivePitchCommit, 0f, 85f);
            surfaceDiveForwardCommit = math.clamp(surfaceDiveForwardCommit, 0f, 1f);
            abyssalCounterDriveFlowThreshold = math.clamp(abyssalCounterDriveFlowThreshold, 0f, 8f);
            abyssalCounterDriveOppositionAngleDegrees = math.clamp(abyssalCounterDriveOppositionAngleDegrees, 90f, 180f);
            abyssalCounterDriveEnergyOverstrainMultiplier = math.clamp(abyssalCounterDriveEnergyOverstrainMultiplier, 1f, 4f);
            abyssalCurrentShearMaxSpeedMultiplier = math.clamp(abyssalCurrentShearMaxSpeedMultiplier, 0.2f, 1f);
            abyssalCurrentShearDrainExponent = math.clamp(abyssalCurrentShearDrainExponent, 1f, 6f);
            abyssalCurrentShearOxygenDrainPerSecond = math.max(abyssalCurrentShearOxygenDrainPerSecond, 0f);
            abyssalCurrentShearEnergyDrainPerSecond = math.max(abyssalCurrentShearEnergyDrainPerSecond, 0f);
            surfaceDiveCommitHoldTime = math.clamp(surfaceDiveCommitHoldTime, 0f, 0.35f);
            surfaceDiveAssistDuration = math.max(surfaceDiveAssistDuration, 0.04f);
            surfaceDiveAssistForceMultiplier = math.max(surfaceDiveAssistForceMultiplier, 0f);
            surfaceSnapEngageSpeed = math.max(surfaceSnapEngageSpeed, 1f);
            surfaceSnapReleaseSpeed = math.max(surfaceSnapReleaseSpeed, 1f);
            surfaceWaveFollowSharpness = math.max(surfaceWaveFollowSharpness, 1f);
            surfaceDiveBreakDepth = math.max(surfaceDiveBreakDepth, 0.05f);
            surfaceHeadReattachDepth = math.clamp(surfaceHeadReattachDepth, 0f, surfaceDiveBreakDepth);
            surfaceBreachReleaseVelocity = math.max(surfaceBreachReleaseVelocity, 0.5f);
            surfaceBreachLockDuration = math.max(surfaceBreachLockDuration, 0.05f);
            surfaceBreachArcVelocity = math.max(surfaceBreachArcVelocity, 0.5f);
            surfaceBreachFluidDragBypassDuration = math.max(surfaceBreachFluidDragBypassDuration, 0.05f);
            surfaceBreachGravitySpikeDelay = math.clamp(surfaceBreachGravitySpikeDelay, 0f, 1.5f);
        }

        private void ValidateSargassumAndAbyssalSettings()
        {
            surfaceBreachGravitySpikeAcceleration = math.max(surfaceBreachGravitySpikeAcceleration, 0f);
            surfaceBreachGravitySpikeDuration = math.max(surfaceBreachGravitySpikeDuration, 0.05f);
            surfaceBreachSplashEnergyScale = math.max(surfaceBreachSplashEnergyScale, 1f);
            surfaceDiveResistanceDamping = math.max(surfaceDiveResistanceDamping, 0f);
            surfaceWaveVelocityInfluence = math.clamp(surfaceWaveVelocityInfluence, 0f, 1f);
            shoreWalkFootDepth = math.max(shoreWalkFootDepth, 0.05f);
            shoreBuoyancyRecoveryClearance = math.max(shoreBuoyancyRecoveryClearance, shoreWalkFootDepth);
            shoreBuoyancyBlendSharpness = math.max(shoreBuoyancyBlendSharpness, 1f);
            shoreWalkHandoffBuoyancyThreshold = math.clamp(shoreWalkHandoffBuoyancyThreshold, 0f, 1f);
            waterEntryImpactMinSpeed = math.max(waterEntryImpactMinSpeed, 0f);
            waterEntryImpactDamping = math.max(waterEntryImpactDamping, 0f);
            waterEntryImpactDuration = math.max(waterEntryImpactDuration, 0.1f);
            waterEntryImpactFovExpand = math.max(waterEntryImpactFovExpand, 0f);
            waterEntryImpactFovCompress = math.max(waterEntryImpactFovCompress, 0f);
            surfacePierceSplashMinSpeed = math.max(surfacePierceSplashMinSpeed, 0f);
            surfacePierceSplashMaxSpeed = math.max(surfacePierceSplashMaxSpeed, surfacePierceSplashMinSpeed);
            surfacePierceSplashMinVolume = math.clamp(surfacePierceSplashMinVolume, 0f, 1f);
            surfacePierceSplashMaxVolume = math.clamp(surfacePierceSplashMaxVolume, 0f, 1f);
            surfacePierceSplashMinVolume = math.min(surfacePierceSplashMinVolume, surfacePierceSplashMaxVolume);
            wetLensSignalRecoverySpeed = math.max(wetLensSignalRecoverySpeed, 0.25f);
            wetLensStormIntensityThreshold = math.clamp(wetLensStormIntensityThreshold, 0f, 1f);
            wetLensWaveCoverDepth = math.clamp(wetLensWaveCoverDepth, 0f, 0.25f);
            wetLensStormPulseCooldown = math.clamp(wetLensStormPulseCooldown, 0.05f, 1f);
            wetLensStormPulseIntensity = math.clamp(wetLensStormPulseIntensity, 0f, 1f);
            wetLensBreachPulseIntensity = math.clamp(wetLensBreachPulseIntensity, 0f, 1f);
            shoreUndertowStormThreshold = math.clamp(shoreUndertowStormThreshold, 0f, 1f);
            shoreUndertowMaxDepth = math.max(shoreUndertowMaxDepth, 0.1f);
            shoreUndertowRetreatVelocityStart = math.max(shoreUndertowRetreatVelocityStart, 0.05f);
            shoreUndertowRetreatVelocityMax = math.max(shoreUndertowRetreatVelocityMax, shoreUndertowRetreatVelocityStart);
            shoreUndertowForce = math.max(shoreUndertowForce, 0f);
            shoreUndertowSurfaceBoost = math.max(shoreUndertowSurfaceBoost, 1f);
            shoreUndertowMinFeetDepth = math.max(shoreUndertowMinFeetDepth, 0.05f);
            shoreUndertowFullFeetDepth = math.max(shoreUndertowFullFeetDepth, shoreUndertowMinFeetDepth);
            wipeoutDuration = math.max(wipeoutDuration, 0.5f);
            wipeoutRecoveryDrag = math.max(wipeoutRecoveryDrag, 0f);
            wipeoutReboundImpulse = math.max(wipeoutReboundImpulse, 0f);
            wipeoutTransportDamageScale = math.max(wipeoutTransportDamageScale, 1f);
            wipeoutBreachLandingGraceTime = math.max(wipeoutBreachLandingGraceTime, 0.1f);
            wipeoutSweepCapsuleInset = math.clamp(wipeoutSweepCapsuleInset, 0f, 0.1f);
            surfaceBreachDepthWindow = math.max(surfaceBreachDepthWindow, SurfaceStateUtility.ExitUnderwaterDepth);
            if (surfaceBreachMinImmersion >= 0.98f)
                surfaceBreachMinImmersion = 0.97f;
            crestFlowVelocityScale = math.max(crestFlowVelocityScale, 0f);
            crestFlowForceResponsiveness = math.max(crestFlowForceResponsiveness, 0f);
            crestFlowOppositionReduction = math.clamp(crestFlowOppositionReduction, 0f, 1f);
            crestFlowCrossCurrentReduction = math.clamp(crestFlowCrossCurrentReduction, 0f, 1f);
            crestFlowInputMinimumScale = math.clamp(crestFlowInputMinimumScale, 0f, 1f);
            crestFlowInputBlendSpeed = math.max(crestFlowInputBlendSpeed, 0.5f);
            crestFlowSurfaceIdleBoost = math.max(crestFlowSurfaceIdleBoost, 1f);
            crestFlowIdleInputThreshold = math.clamp(crestFlowIdleInputThreshold, 0f, 0.35f);
            crestBodySampleMinLength = math.max(crestBodySampleMinLength, 0f);
            crestBodyForwardSampleDistance = math.max(crestBodyForwardSampleDistance, 0.15f);
            crestBodyLateralSampleDistance = math.max(crestBodyLateralSampleDistance, 0.1f);
            surfaceWaveAlignmentSharpness = math.max(surfaceWaveAlignmentSharpness, 1f);
            surfaceWaveMaxPitch = math.max(surfaceWaveMaxPitch, 0f);
            surfaceWaveMaxRoll = math.max(surfaceWaveMaxRoll, 0f);
        }

        private void ValidateImpactAndSurfaceSettings()
        {
            underwaterTurbulenceMaxDepth = math.max(underwaterTurbulenceMaxDepth, 1f);
            underwaterTurbulenceHeightStart = math.max(underwaterTurbulenceHeightStart, 0.05f);
            underwaterTurbulenceHeightMax = math.max(underwaterTurbulenceHeightMax, underwaterTurbulenceHeightStart);
            underwaterTurbulenceDisplacementStart = math.max(underwaterTurbulenceDisplacementStart, 0.05f);
            underwaterTurbulenceDisplacementMax = math.max(underwaterTurbulenceDisplacementMax, underwaterTurbulenceDisplacementStart);
            underwaterTurbulenceVelocityMax = math.max(underwaterTurbulenceVelocityMax, 0.1f);
            underwaterTurbulenceForce = math.max(underwaterTurbulenceForce, 0f);
            underwaterTurbulenceVerticalForce = math.max(underwaterTurbulenceVerticalForce, 0f);
            underwaterTurbulenceFrequency = math.max(underwaterTurbulenceFrequency, 0.1f);
            underwaterTurbulencePitch = math.max(underwaterTurbulencePitch, 0f);
            underwaterTurbulenceRoll = math.max(underwaterTurbulenceRoll, 0f);
            underwaterTurbulencePoseSharpness = math.max(underwaterTurbulencePoseSharpness, 1f);
            underwaterTurbulenceBottomInfluenceDepth = math.max(underwaterTurbulenceBottomInfluenceDepth, 0.1f);
            underwaterTurbulenceBottomBoost = math.max(underwaterTurbulenceBottomBoost, 1f);
            underwaterStressSignalThreshold = math.clamp(underwaterStressSignalThreshold, 0f, 1f);
            underwaterStressSignalBlendSharpness = math.max(underwaterStressSignalBlendSharpness, 1f);
            abyssalFlowAdvectionSharpness = math.max(abyssalFlowAdvectionSharpness, 0.1f);
            abyssalTransportTurbulenceTorqueVelocityChange = math.max(abyssalTransportTurbulenceTorqueVelocityChange, 0f);
            abyssalTransportTurbulencePitchDegrees = math.max(abyssalTransportTurbulencePitchDegrees, 0f);
            abyssalTransportTurbulenceYawDegrees = math.max(abyssalTransportTurbulenceYawDegrees, 0f);
            abyssalTransportTurbulenceRecoverySharpness = math.max(abyssalTransportTurbulenceRecoverySharpness, 1f);
            transportCavitationStartDepth = math.max(transportCavitationStartDepth, 0.1f);
            transportCavitationRecoveryDepth = math.max(transportCavitationRecoveryDepth, transportCavitationStartDepth);
            transportCavitationAccelerationStart = math.max(transportCavitationAccelerationStart, 0f);
            transportCavitationAccelerationMax = math.max(transportCavitationAccelerationMax, transportCavitationAccelerationStart + 0.01f);
            transportCavitationMinEfficiency = math.clamp(transportCavitationMinEfficiency, 0.05f, 1f);
            transportCavitationBlendSharpness = math.max(transportCavitationBlendSharpness, 1f);
            externalEnvironmentalDragHoldTime = math.clamp(externalEnvironmentalDragHoldTime, 0f, 0.35f);
            externalEnvironmentalDragBlendSpeed = math.max(externalEnvironmentalDragBlendSpeed, 1f);
            brineViscosityDragMultiplier = math.clamp(brineViscosityDragMultiplier, 1f, 8f);
            parasiteLatchInfluenceHoldTime = math.clamp(parasiteLatchInfluenceHoldTime, 0f, 0.35f);
            parasiteLatchInfluenceBlendSpeed = math.max(parasiteLatchInfluenceBlendSpeed, 1f);
            parasiteCenterOfMassForce = math.clamp(parasiteCenterOfMassForce, 0f, 80f);
            parasiteHarvesterPullForce = math.clamp(parasiteHarvesterPullForce, 0f, 120f);
            parasiteLatchCountForFullForce = math.clamp(parasiteLatchCountForFullForce, 1f, 64f);
            sargassumEntanglementMassReference = math.clamp(sargassumEntanglementMassReference, 40f, 500f);
            sargassumEntanglementMaxAcceleration = math.clamp(sargassumEntanglementMaxAcceleration, 0f, 80f);
            sargassumEntanglementSwimEnvironmentalDrag = math.clamp(sargassumEntanglementSwimEnvironmentalDrag, 0f, 3f);
            sargassumEntanglementTransportEnvironmentalDrag = math.clamp(sargassumEntanglementTransportEnvironmentalDrag, 0f, 4f);
            sargassumEntanglementEscapeRelief = math.clamp(sargassumEntanglementEscapeRelief, 0f, 1f);
            sargassumEscapeEnergyDrainPerSecond = math.clamp(sargassumEscapeEnergyDrainPerSecond, 0f, 10f);
            sargassumEntanglementEscapeEnergyMultiplier = math.clamp(sargassumEntanglementEscapeEnergyMultiplier, 1f, 6f);
            sargassumEscapeInputThreshold = math.clamp(sargassumEscapeInputThreshold, 0f, 1f);
            sargassumHighStrainThreshold = math.clamp(sargassumHighStrainThreshold, 0f, 1f);
            sargassumHighStrainShakeBoost = math.clamp(sargassumHighStrainShakeBoost, 1f, 4f);
            sargassumHighStrainEnergyMultiplier = math.clamp(sargassumHighStrainEnergyMultiplier, 1f, 6f);
            sargassumHighStrainHoldTime = math.clamp(sargassumHighStrainHoldTime, 0f, 0.5f);
            abyssalCableEntanglementSpring = math.clamp(abyssalCableEntanglementSpring, 0f, 80f);
            abyssalCableEntanglementDamping = math.clamp(abyssalCableEntanglementDamping, 0f, 30f);
            abyssalCableEntanglementMaxAcceleration = math.max(abyssalCableEntanglementMaxAcceleration, 0f);
        }

        private void ValidateHazardAndMiscSettings()
        {
            abyssalCableEntanglementMaxAcceleration = math.min(abyssalCableEntanglementMaxAcceleration, 120f);
            abyssalCableEntanglementVerticalInfluence = math.clamp(abyssalCableEntanglementVerticalInfluence, 0f, 1f);
            if (!math.isfinite(abyssalCableEntanglementMassReference)) abyssalCableEntanglementMassReference = 110f;
            abyssalCableEntanglementMassReference = math.clamp(abyssalCableEntanglementMassReference, 40f, 500f);
            abyssalCableEntanglementSwimEnvironmentalDrag = math.clamp(abyssalCableEntanglementSwimEnvironmentalDrag, 0f, 5f);
            abyssalCableEntanglementTransportEnvironmentalDrag = math.clamp(abyssalCableEntanglementTransportEnvironmentalDrag, 0f, 8f);
            abyssalCableEscapeEnergyDrainPerSecond = math.clamp(abyssalCableEscapeEnergyDrainPerSecond, 0f, 20f);
            abyssalCableEscapeEnergyMultiplier = math.clamp(abyssalCableEscapeEnergyMultiplier, 1f, 8f);
            abyssalCableCutReleaseThreshold = math.clamp(abyssalCableCutReleaseThreshold, 0f, 1f);
            abyssalCablePropulsionReliefAtFullCut = math.clamp(abyssalCablePropulsionReliefAtFullCut, 0f, 1f);
            sargassumMatBuoyancyDensityThreshold = math.clamp(sargassumMatBuoyancyDensityThreshold, 0f, 1f);
            sargassumMatBuoyancyMaxDepth = math.max(sargassumMatBuoyancyMaxDepth, 0.1f);
            sargassumMatBuoyancyBlendSharpness = math.max(sargassumMatBuoyancyBlendSharpness, 1f);
            sargassumMatBuoyancyForceScale = math.clamp(sargassumMatBuoyancyForceScale, 0f, 2.5f);
            sargassumMatSurfaceLockBoost = math.clamp(sargassumMatSurfaceLockBoost, 1f, 3f);
            sargassumMatSurfaceLiftOffset = math.clamp(sargassumMatSurfaceLiftOffset, 0f, 0.75f);
            impactBubbleMinIntensity = math.clamp(impactBubbleMinIntensity, 0f, 1f);
            impactBubbleMinCount = math.max(impactBubbleMinCount, 0);
            impactBubbleMaxCount = math.max(impactBubbleMaxCount, impactBubbleMinCount);
            underwaterImpactMinVolume = math.clamp(underwaterImpactMinVolume, 0f, 1f);
            underwaterImpactMaxVolume = math.clamp(underwaterImpactMaxVolume, 0f, 1f);
            underwaterImpactMinVolume = math.min(underwaterImpactMinVolume, underwaterImpactMaxVolume);
            underwaterSomaticHeadbobFrequency = math.max(underwaterSomaticHeadbobFrequency, 0.1f);
            underwaterSomaticPitchDegrees = math.max(underwaterSomaticPitchDegrees, 0f);
            underwaterSomaticYawDegrees = math.max(underwaterSomaticYawDegrees, 0f);
            underwaterSomaticReferenceSpeed = math.max(underwaterSomaticReferenceSpeed, 0.25f);
            underwaterSomaticResponseSharpness = math.max(underwaterSomaticResponseSharpness, 0.5f);
            underwaterSomaticFatigueStaminaThreshold01 = math.clamp(underwaterSomaticFatigueStaminaThreshold01, 0.01f, 0.6f);
            underwaterSomaticFatigueCadenceMultiplier = math.clamp(underwaterSomaticFatigueCadenceMultiplier, 1f, 3f);
            underwaterSomaticFatigueSwayMultiplier = math.clamp(underwaterSomaticFatigueSwayMultiplier, 1f, 4f);
            underwaterSomaticFatigueBreathCooldown = math.max(underwaterSomaticFatigueBreathCooldown, 0.2f);
            underwaterSomaticFatigueBreathVolumeScale = math.clamp(underwaterSomaticFatigueBreathVolumeScale, 0f, 1f);
            stepAssistVerticalVelocityPulse = math.clamp(stepAssistVerticalVelocityPulse, 0f, 3f);
            wallKickVelocityChange = math.max(wallKickVelocityChange, 0f);
            wallKickResourceCost01 = math.clamp(wallKickResourceCost01, 0f, 0.5f);
            wallKickContactFrameGrace = math.clamp(wallKickContactFrameGrace, 0, 8);
            wallKickCooldown = math.max(wallKickCooldown, 0f);
            wallKickTangentFriction = math.clamp(wallKickTangentFriction, 0f, 1f);
            suitScrapeSlideAngleThresholdDegrees = math.clamp(suitScrapeSlideAngleThresholdDegrees, 1f, 89f);
            suitScrapeMinBlockedSpeed = math.max(suitScrapeMinBlockedSpeed, 0f);
            suitScrapeImpactBusSpeedScale = math.max(suitScrapeImpactBusSpeedScale, 0f);
            suitScrapeCameraSpeedScale = math.max(suitScrapeCameraSpeedScale, 0f);
            maxHeavyCarryBodyYawSpringMultiplier = math.clamp(maxHeavyCarryBodyYawSpringMultiplier, 0.1f, 1f);
            heavyTowCameraPitchDegrees = math.max(heavyTowCameraPitchDegrees, 0f);
            heavyTowCameraRollDegrees = math.max(heavyTowCameraRollDegrees, 0f);
            heavyTowCameraBackwardOffset = math.max(heavyTowCameraBackwardOffset, 0f);
            heavyTowCameraSideOffset = math.max(heavyTowCameraSideOffset, 0f);
            heavyTowResponseBlendSharpness = math.max(heavyTowResponseBlendSharpness, 1f);
            heavyTowCenterOfMassRearShift = math.max(heavyTowCenterOfMassRearShift, 0f);
            heavyTowCenterOfMassLateralShift = math.max(heavyTowCenterOfMassLateralShift, 0f);
            heavyTowCenterOfMassDownShift = math.max(heavyTowCenterOfMassDownShift, 0f);

        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector3 bodyPos = ResolvePlayerAupRuntimePosition();
            float bodyBottomY = GetBodyBottomY();
            Vector3 origin = new Vector3(bodyPos.x, bodyBottomY + groundCheckRadius + GroundCheckSkin, bodyPos.z);
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + GroundCheckSkin);

            // Water level
            float effectiveY = EffectiveWaterSurfaceY;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 waterCenter = bodyPos;
            waterCenter.y = effectiveY;
            Gizmos.DrawWireCube(waterCenter, new Vector3(6f, 0.02f, 6f));

            // Immersion indicator
            if (_waterImmersionRatio > 0.01f)
            {
                Gizmos.color = new Color(0f, 0.3f, 1f, 0.5f);
                float immersedHeight = playerHeight * _waterImmersionRatio;
                Vector3 immCenter = bodyPos;
                immCenter.y += immersedHeight * 0.5f;
                Gizmos.DrawWireCube(immCenter, new Vector3(0.5f, immersedHeight, 0.5f));
            }

            if (_isGrounded)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_groundHit.point, groundCheckRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_groundHit.point, _groundHit.point + _groundHit.normal * 1.5f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_groundHit.point,
                    _groundHit.point + _smoothedGroundNormal * 1.2f);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(castEnd, groundCheckRadius);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(origin, castEnd);

            // Body vs camera yaw
            if (!_isWalking)
            {
                Vector3 pos = bodyPos + Vector3.up * 1.5f;
                ResolveDegreesSinCosFast(_cameraYaw, out float cameraSinYaw, out float cameraCosYaw);
                ResolveDegreesSinCosFast(_bodyYaw, out float bodySinYaw, out float bodyCosYaw);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, pos + new Vector3(cameraSinYaw, 0f, cameraCosYaw) * 2f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + new Vector3(bodySinYaw, 0f, bodyCosYaw) * 1.5f);
            }

            // Depth indicator
            if (_currentDepth > 0.5f)
            {
                Gizmos.color = new Color(0f, 0f, 0.8f, 0.4f);
                Vector3 depthStart = bodyPos;
                depthStart.y = effectiveY;
                Gizmos.DrawLine(depthStart, bodyPos);
            }
        }
#endif
    
        #region JulesLink_CrouchCapsuleLerp
        private static void JulesLink_CrouchCapsuleLerp() { _ = typeof(Hecton8.PureLogic.Kinematics.CrouchCapsuleLerp); }
        #endregion

        #region JulesLink_WallSlideFrictionCalculator
        private static void JulesLink_WallSlideFrictionCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.WallSlideFrictionCalculator); }
        #endregion

        #region JulesLink_LedgeGrabImpulseCalculator
        private static void JulesLink_LedgeGrabImpulseCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.LedgeGrabImpulseCalculator); }
        #endregion

        #region JulesLink_WaterSurfaceTransitionDragCalculator
        private static void JulesLink_WaterSurfaceTransitionDragCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.WaterSurfaceTransitionDragCalculator); }
        #endregion

        #region JulesLink_EquipmentHydrodynamicDragCalculator
        private static void JulesLink_EquipmentHydrodynamicDragCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.EquipmentHydrodynamicDragCalculator); }
        #endregion

        #region JulesLink_ThermalVentUpdraftForce
        private static void JulesLink_ThermalVentUpdraftForce() { _ = typeof(Hecton8.PureLogic.Kinematics.ThermalVentUpdraftForce); }
        #endregion

        #region JulesLink_ParasiteLatchDragCalculator
        private static void JulesLink_ParasiteLatchDragCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.ParasiteLatchDragCalculator); }
        #endregion

        #region JulesLink_VehicleEmergencyEjectionVector
        private static void JulesLink_VehicleEmergencyEjectionVector() { _ = typeof(Hecton8.PureLogic.Kinematics.VehicleEmergencyEjectionVector); }
        #endregion

#region JulesLink_NitrogenNarcosisCriticalDepthCalculator
        private static void JulesLink_NitrogenNarcosisCriticalDepthCalculator() { _ = typeof(Hecton8.PureLogic.Systems.NitrogenNarcosisCriticalDepthCalculator); }
        #endregion

}
}

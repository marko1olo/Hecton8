using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.UI;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    [System.Flags]
    public enum PlayerRuntimeSnapshotFlags : uint
    {
        None = 0u,
        HasPlayerRoot = 1u << 0,
        HasMovement = 1u << 1,
        HasRigidbody = 1u << 2,
        HasSurvival = 1u << 3,
        HasToolManager = 1u << 4,
        HasInventory = 1u << 5,
        HasTransport = 1u << 6,
        HasTrauma = 1u << 7,
        ToolEquipped = 1u << 8,
        HandheldToolBlocked = 1u << 9,
        PlayerAlive = 1u << 10,
        OxygenGraceActive = 1u << 11,
        Underwater = 1u << 12,
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PlayerMovementRuntimeState
    {
        [FieldOffset(0)] public float3 WorldPosition;
        [FieldOffset(12)] public float3 PredictedWorldPosition;
        [FieldOffset(24)] public AbsoluteUniversePosition PredictedAup;
        [FieldOffset(72)] public float3 Velocity;
        [FieldOffset(84)] public float3 Forward;
        [FieldOffset(96)] public float3 CameraForward;
        [FieldOffset(108)] public float DepthMeters;
        [FieldOffset(112)] public float TransportSpeedMultiplier;
        [FieldOffset(116)] public float UnderwaterStressIntensity01;
        [FieldOffset(120)] public uint Flags;
        [FieldOffset(124)] private uint _padding0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PlayerMovementStressRuntimeState
    {
        [FieldOffset(0)] public float HullStress01;
        [FieldOffset(4)] public float UnderwaterStressIntensity01;
        [FieldOffset(8)] public float AbyssalCounterDriveEnergyMultiplier;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Headless-safe player gaze snapshot. Presentation cameras may seed it, but gameplay reads only this data.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerLookState
    {
        [FieldOffset(0)] public float3 EyePosition;
        [FieldOffset(12)] public float3 AimForward;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _padding0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PlayerSurvivalRuntimeState
    {
        [FieldOffset(0)] public float OxygenNormalized;
        [FieldOffset(4)] public float EnergyNormalized;
        [FieldOffset(8)] public float IntegrityNormalized;
        [FieldOffset(12)] public float PressureExposureSeverity01;
        [FieldOffset(16)] public float ThermalStressSeverity01;
        [FieldOffset(20)] public float HungerNormalized;
        [FieldOffset(24)] public float ThirstNormalized;
        [FieldOffset(28)] public float OxygenGraceVisionBlur01;
        [FieldOffset(32)] public float ColdStressSeverity01;
        [FieldOffset(36)] public float HeatStressSeverity01;
        [FieldOffset(40)] public float RapidAscentRisk01;
        [FieldOffset(44)] public float NitrogenBuildUp01;
        [FieldOffset(48)] public float NitrogenLoad01;
        [FieldOffset(52)] public float NitrogenNarcosis01;
        [FieldOffset(56)] public float Toxicity01;
        [FieldOffset(60)] public float CoreTemperatureCelsius;
        [FieldOffset(64)] public float RadiationDose;
        [FieldOffset(68)] public float RadiationIntensity01;
        [FieldOffset(72)] public float RadiationMaxHealthPenalty01;
        [FieldOffset(76)] public uint StatusMask;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] private uint _padding0;
        [FieldOffset(88)] private ulong _padding1;
        [FieldOffset(96)] private ulong _padding2;
        [FieldOffset(104)] private ulong _padding3;
        [FieldOffset(112)] private ulong _padding4;
        [FieldOffset(120)] private ulong _padding5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerInteractionRuntimeState
    {
        [FieldOffset(0)] public int ActiveToolSlot;
        [FieldOffset(4)] public int PendingToolSlot;
        [FieldOffset(8)] public float SwapProgress01;
        [FieldOffset(12)] public float TransportBoost01;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private uint _padding0;
        [FieldOffset(24)] private uint _padding1;
        [FieldOffset(28)] private uint _padding2;
    }

    /// <summary>
    /// Central runtime context extracted from the player god object. Blittable state snapshots are grouped
    /// by ownership domain while reference wiring remains centralized on the bootstrap-owned service.
    /// </summary>
    public sealed class PlayerRuntimeContext
    {
        private const float SurvivalDamageEpsilon = 0.0001f;
        private const float MinTransportSpeedMultiplier = 0.01f;
        private const uint PlayerTargetHash = 0x504C5952u;
        private const uint SurvivalSourceHash = 0x53525656u;

        public GameObject PlayerObject { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public HectonPlayerMovement PlayerMovement { get; private set; }
        public Rigidbody PlayerRigidbody { get; private set; }
        public HectonSurvivalSystem SurvivalSystem { get; private set; }
        public HectonPlayerHealth PlayerHealth { get; private set; }
        public PlayerToolManager ToolManager { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public PlayerTransportCoordinator PlayerTransportCoordinator { get; private set; }
        public TraumaDispatcher TraumaDispatcher { get; private set; }
        public Camera PlayerCamera { get; private set; }
        public PlayerPDA PlayerPDA { get; private set; }
        public PlayerBuilder PlayerBuilder { get; private set; }
        public VisorHUDController VisorController { get; private set; }
        public PlayerFlashlight Flashlight { get; private set; }
        public PlayerThrusterAudio ThrusterAudio { get; private set; }
        public HectonUnderwaterVisuals UnderwaterVisuals { get; private set; }
        public Transform HandAnchor { get; private set; }
        public Collider PlayerCollider { get; private set; }
        public HUDNotification HudNotification { get; private set; }

        public PlayerMovementRuntimeState MovementState;
        public PlayerMovementStressRuntimeState MovementStressState;
        public PlayerLookState LookState;
        public PlayerSurvivalRuntimeState SurvivalState;
        public PlayerInteractionRuntimeState InteractionState;
        public float RadiationDose;
        public float RadiationIntensity01;
        public float RadiationMaxHealthPenalty01;

        public bool IsBound => PlayerObject != null && PlayerTransform != null;

        public void Clear()
        {
            PlayerObject = null;
            PlayerTransform = null;
            PlayerMovement = null;
            PlayerRigidbody = null;
            SurvivalSystem = null;
            PlayerHealth = null;
            ToolManager = null;
            Inventory = null;
            PlayerTransportCoordinator = null;
            TraumaDispatcher = null;
            PlayerCamera = null;
            PlayerPDA = null;
            PlayerBuilder = null;
            VisorController = null;
            Flashlight = null;
            ThrusterAudio = null;
            UnderwaterVisuals = null;
            HandAnchor = null;
            PlayerCollider = null;
            HudNotification = null;
            MovementState = default;
            MovementStressState = default;
            LookState = default;
            SurvivalState = default;
            InteractionState = default;
            RadiationDose = 0f;
            RadiationIntensity01 = 0f;
            RadiationMaxHealthPenalty01 = 0f;
        }

        public void SyncReferences(
            GameObject playerObject,
            Transform playerTransform,
            HectonPlayerMovement playerMovement,
            Rigidbody playerRigidbody,
            HectonSurvivalSystem survivalSystem,
            HectonPlayerHealth playerHealth,
            PlayerToolManager toolManager,
            PlayerInventory inventory,
            PlayerTransportCoordinator playerTransportCoordinator,
            TraumaDispatcher traumaDispatcher,
            Camera playerCamera,
            PlayerPDA playerPda,
            PlayerBuilder playerBuilder,
            VisorHUDController visorController,
            PlayerFlashlight flashlight,
            PlayerThrusterAudio thrusterAudio,
            HectonUnderwaterVisuals underwaterVisuals,
            Transform handAnchor,
            Collider playerCollider,
            HUDNotification hudNotification)
        {
            PlayerObject = playerObject;
            PlayerTransform = playerTransform;
            PlayerMovement = playerMovement;
            PlayerRigidbody = playerRigidbody;
            SurvivalSystem = survivalSystem;
            PlayerHealth = playerHealth;
            ToolManager = toolManager;
            Inventory = inventory;
            PlayerTransportCoordinator = playerTransportCoordinator;
            TraumaDispatcher = traumaDispatcher;
            PlayerCamera = playerCamera;
            PlayerPDA = playerPda;
            PlayerBuilder = playerBuilder;
            VisorController = visorController;
            Flashlight = flashlight;
            ThrusterAudio = thrusterAudio;
            UnderwaterVisuals = underwaterVisuals;
            HandAnchor = handAnchor;
            PlayerCollider = playerCollider;
            HudNotification = hudNotification;
        }

        public void PublishMovementState(in PlayerMovementRuntimeState state)
        {
            MovementState = SanitizeMovementState(in state, in MovementState);
        }

        public void PublishMovementStressState(in PlayerMovementStressRuntimeState state)
        {
            PlayerMovementStressRuntimeState sanitized = state;
            sanitized.HullStress01 = MathGuard.Sanitize01(state.HullStress01, MovementStressState.HullStress01);
            sanitized.UnderwaterStressIntensity01 = MathGuard.Sanitize01(
                state.UnderwaterStressIntensity01,
                MovementStressState.UnderwaterStressIntensity01);
            sanitized.AbyssalCounterDriveEnergyMultiplier = math.max(
                1f,
                MathGuard.SanitizeFinite(
                    state.AbyssalCounterDriveEnergyMultiplier,
                    MovementStressState.AbyssalCounterDriveEnergyMultiplier > 0f
                        ? MovementStressState.AbyssalCounterDriveEnergyMultiplier
                        : 1f));
            sanitized.Flags = state.Flags;
            MovementStressState = sanitized;
        }

        /// <summary>
        /// Publishes the headless-safe gaze snapshot for systems that must not depend on camera components.
        /// </summary>
        /// <param name="state">Latest player look snapshot.</param>
        public void PublishLookState(in PlayerLookState state)
        {
            LookState = state;
        }

        public void PublishSurvivalState(in PlayerSurvivalRuntimeState state)
        {
            float previousIntegrity = SurvivalState.IntegrityNormalized;
            SurvivalState = state;
            float integrityDelta = previousIntegrity - SurvivalState.IntegrityNormalized;
            if (previousIntegrity > 0f && integrityDelta > SurvivalDamageEpsilon)
                PublishSurvivalDamageSignal(in state, integrityDelta);
        }

        public void PublishInteractionState(in PlayerInteractionRuntimeState state)
        {
            InteractionState = state;
        }

        private void PublishSurvivalDamageSignal(in PlayerSurvivalRuntimeState state, float integrityDelta)
        {
            float3 worldPoint = MovementState.WorldPosition;
            if (!math.all(math.isfinite(worldPoint)))
                worldPoint = float3.zero;

            Hecton8.Core.Contracts.Signals.CombatDamageSignal signal = new Hecton8.Core.Contracts.Signals.CombatDamageSignal
            {
                ImpactAup = Hecton8.Core.Contracts.Signals.CombatDamageSignalCodec.FromRuntimePoint(worldPoint),
                Direction = float3.zero,
                Magnitude = math.max(0f, integrityDelta),
                DamageType = state.StatusMask,
                TargetHash = PlayerTargetHash,
                SourceHash = SurvivalSourceHash,
                Frame = unchecked((uint)SystemDispatcher.CurrentFrameIndex),
                SourceId = 0,
                TargetId = 0,
                Channel = 0,
                Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag
            };
            SignalBus<Hecton8.Core.Contracts.Signals.CombatDamageSignal>.TryPush(in signal);
        }

        private static PlayerMovementRuntimeState SanitizeMovementState(
            in PlayerMovementRuntimeState value,
            in PlayerMovementRuntimeState fallback)
        {
            PlayerMovementRuntimeState sanitized = value;
            sanitized.WorldPosition = MathGuard.SanitizeFinite(value.WorldPosition, fallback.WorldPosition);
            sanitized.PredictedWorldPosition = MathGuard.SanitizeFinite(value.PredictedWorldPosition, sanitized.WorldPosition);
            sanitized.PredictedAup = AbsoluteUniversePosition.Sanitize(in value.PredictedAup, in fallback.PredictedAup);
            sanitized.Velocity = MathGuard.SanitizeFinite(value.Velocity, fallback.Velocity);
            sanitized.Forward = MathGuard.SanitizeDirection(value.Forward, fallback.Forward);
            sanitized.CameraForward = MathGuard.SanitizeDirection(value.CameraForward, sanitized.Forward);
            sanitized.DepthMeters = MathGuard.SanitizeNonNegative(value.DepthMeters, fallback.DepthMeters);
            sanitized.TransportSpeedMultiplier = math.max(
                MinTransportSpeedMultiplier,
                MathGuard.SanitizeFinite(value.TransportSpeedMultiplier, fallback.TransportSpeedMultiplier));
            sanitized.UnderwaterStressIntensity01 = MathGuard.Sanitize01(
                value.UnderwaterStressIntensity01,
                fallback.UnderwaterStressIntensity01);
            return sanitized;
        }
    }
}

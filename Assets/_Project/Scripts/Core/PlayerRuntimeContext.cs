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

    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerMovementRuntimeState
    {
        public float3 WorldPosition;
        public float3 PredictedWorldPosition;
        public AbsoluteUniversePosition PredictedAup;
        public float3 Velocity;
        public float3 Forward;
        public float3 CameraForward;
        public float DepthMeters;
        public float TransportSpeedMultiplier;
        public float UnderwaterStressIntensity01;
        public uint Flags;
    }

    /// <summary>
    /// Headless-safe player gaze snapshot. Presentation cameras may seed it, but gameplay reads only this data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerLookState
    {
        public float3 EyePosition;
        public float3 AimForward;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerSurvivalRuntimeState
    {
        public float OxygenNormalized;
        public float EnergyNormalized;
        public float IntegrityNormalized;
        public float PressureExposureSeverity01;
        public float ThermalStressSeverity01;
        public float HungerNormalized;
        public float ThirstNormalized;
        public float OxygenGraceVisionBlur01;
        public float ColdStressSeverity01;
        public float HeatStressSeverity01;
        public float RapidAscentRisk01;
        public float NitrogenBuildUp01;
        public float NitrogenLoad01;
        public float NitrogenNarcosis01;
        public float Toxicity01;
        public float CoreTemperatureCelsius;
        public float RadiationDose;
        public float RadiationIntensity01;
        public float RadiationMaxHealthPenalty01;
        public uint StatusMask;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct PlayerInteractionRuntimeState
    {
        public int ActiveToolSlot;
        public int PendingToolSlot;
        public float SwapProgress01;
        public float TransportBoost01;
        public uint Flags;
        private uint _padding0;
        private uint _padding1;
        private uint _padding2;
    }

    /// <summary>
    /// Central runtime context extracted from the player god object. Blittable state snapshots are grouped
    /// by ownership domain while reference wiring remains centralized on the bootstrap-owned service.
    /// </summary>
    public sealed class PlayerRuntimeContext
    {
        private const float SurvivalDamageEpsilon = 0.0001f;
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
            MovementState = MathGuard.SanitizePlayerMovementRuntimeState(in state, in MovementState);
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
                WorldPoint = worldPoint,
                Direction = float3.zero,
                Magnitude = math.max(0f, integrityDelta),
                DamageType = state.StatusMask,
                TargetHash = PlayerTargetHash,
                SourceHash = SurvivalSourceHash,
                Frame = unchecked((uint)Time.frameCount),
                SourceId = 0,
                TargetId = 0,
                Channel = 0,
                Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag
            };
            SignalBus<Hecton8.Core.Contracts.Signals.CombatDamageSignal>.Push(in signal);
        }
    }
}

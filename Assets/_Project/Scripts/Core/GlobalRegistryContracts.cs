using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Interaction;
using Hecton8.SaveSystem;
using Hecton8.Construction;
using Hecton8.Building;
using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Meta;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed bootstrap and dispatch layers used by the registry-backed runtime core.
    /// </summary>
    public enum PriorityLayer : byte
    {
        Core = 0x00,
        Environment = 0x20,
        Player = 0x40,
        UI = 0x60,
    }

    /// <summary>
    /// Zero-allocation update contract used by <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// End-of-frame callback executed by <see cref="SystemDispatcher"/> after the main update lanes.
    /// Intended for deferred job ownership recovery and readback commits that must stay out of hot-path ticks.
    /// </summary>
    public interface ILateFrameTickable
    {
        /// <summary>
        /// Executes the owner's end-of-frame swap-window work.
        /// </summary>
        void LateFrameTick();
    }

    /// <summary>
    /// Post-fixed callback executed by <see cref="SystemDispatcher"/> after the fixed lanes complete.
    /// Intended for deferred job ownership recovery that must stay out of hot-path fixed ticks.
    /// </summary>
    public interface IPostFixedTickable
    {
        /// <summary>
        /// Executes the owner's post-fixed-step swap-window work.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed delta supplied by the dispatcher.</param>
        void PostFixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// Minimal render callback contract for registry-managed render systems.
    /// </summary>
    public interface IRenderable
    {
        /// <summary>
        /// Executes one render-side callback.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the caller.</param>
        void Render(float deltaTime);
    }

    /// <summary>
    /// Canonical damage-channel discriminator used by the global packet-based damage receiver contract.
    /// </summary>
    public enum DamageChannel : byte
    {
        Integrity = 0,
        Power = 1,
        Clarity = 2,
        Trauma = 3,
        HullBreach = 4
    }

    /// <summary>
    /// Canonical damage packet routed through the global packet-based damage receiver contract.
    /// </summary>
    public struct DamagePacket
    {
        /// <summary>
        /// Packet channel resolved by the emitting owner.
        /// </summary>
        public DamageChannel Channel;

        /// <summary>
        /// Previous normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        public float PreviousValue;

        /// <summary>
        /// Next normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        public float NextValue;

        /// <summary>
        /// Primary physical magnitude associated with the event.
        /// Integrity and clarity send normalized magnitudes, hull breaches send pressure delta.
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// Local-space point relative to the emitting owner.
        /// </summary>
        public float3 LocalPoint;

        /// <summary>
        /// Damage-type bitmask authored by the emitter.
        /// </summary>
        public uint DamageType;

        /// <summary>
        /// Quantized integrity delta used by structural diffusion consumers.
        /// </summary>
        public byte IntegrityDelta;

        /// <summary>
        /// Depth in meters associated with the damage event when relevant.
        /// </summary>
        public float Depth;

        /// <summary>
        /// Stable emitter-local source identifier.
        /// </summary>
        public ushort SourceId;

        /// <summary>
        /// Encoded trauma threshold when <see cref="Channel"/> is <see cref="DamageChannel.Trauma"/>.
        /// </summary>
        public byte TraumaLevel;
    }

    /// <summary>
    /// Canonical packet-based damage receiver contract.
    /// All first-party damage ingress must terminate here before subsystem-specific fanout.
    /// </summary>
    public interface IDamageReceiver
    {
        /// <summary>
        /// Receives one authoritative damage packet.
        /// </summary>
        /// <param name="packet">Immutable damage payload copied by the caller.</param>
        void ReceiveDamage(in DamagePacket packet);
    }

    /// <summary>
    /// Immutable authored debris definition consumed by the runtime debris manager.
    /// </summary>
    public interface IDebrisDefinition
    {
        /// <summary>
        /// True when the definition has at least one authored chunk and material binding.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Total authored Voronoi chunk count.
        /// </summary>
        int ChunkCount { get; }

        /// <summary>
        /// Shared material used for all runtime chunk draws.
        /// </summary>
        Material SharedMaterial { get; }

        /// <summary>
        /// Shadow-casting mode used by the debris burst.
        /// </summary>
        ShadowCastingMode ShadowCastingMode { get; }

        /// <summary>
        /// True when the runtime chunks should receive shadows.
        /// </summary>
        bool ReceiveShadows { get; }

        /// <summary>
        /// Rendering layer mask applied to the runtime chunk draw calls.
        /// </summary>
        uint RenderingLayerMask { get; }

        /// <summary>
        /// Base outward impulse multiplier applied on burst spawn.
        /// </summary>
        float BaseImpulse { get; }

        /// <summary>
        /// Linear damping applied while collision remains enabled.
        /// </summary>
        float LinearDamping { get; }

        /// <summary>
        /// Angular damping applied per simulated chunk.
        /// </summary>
        float AngularDamping { get; }

        /// <summary>
        /// Extra kinematic sink distance applied after the collision phase ends.
        /// </summary>
        float SinkDistance { get; }

        /// <summary>
        /// Duration of the sink phase after collision is disabled.
        /// </summary>
        float SinkDuration { get; }

        /// <summary>
        /// Offset below the spawn origin used as the simple ground collision plane.
        /// </summary>
        float GroundPlaneOffset { get; }

        /// <summary>
        /// Bounce attenuation used by the simple collision response.
        /// </summary>
        float BounceDamping { get; }

        /// <summary>
        /// Returns the authored mesh for one chunk index.
        /// </summary>
        Mesh GetChunkMesh(int index);

        /// <summary>
        /// Returns the authored local transform matrix for one chunk index.
        /// </summary>
        Matrix4x4 GetLocalMatrix(int index);

        /// <summary>
        /// Returns the authored mass scale for one chunk index.
        /// </summary>
        float GetMassScale(int index);
    }

    /// <summary>
    /// Canonical abyssal weather state bitmask published through <see cref="GlobalRegistry"/>.
    /// </summary>
    [System.Flags]
    public enum WeatherState : uint
    {
        Calm = 1u << 0,
        Storm = 1u << 1,
        UpdraftActive = 1u << 2,
        ThermoclineActive = 1u << 3,
        HaloclineActive = 1u << 4,
        BiolumeSurge = 1u << 5,
    }

    /// <summary>
    /// Shared current-metadata payload mandated for flow-field-derived systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CurrentMeta
    {
        /// <summary>
        /// Base world-space current vector before local modifiers.
        /// </summary>
        public float3 GlobalBaseVector;

        /// <summary>
        /// Scalar applied to the base vector.
        /// </summary>
        public float GlobalScale;

        /// <summary>
        /// Thermocline / halocline response strength.
        /// </summary>
        public float ThermalIntensity;

        /// <summary>
        /// Monotonic weather-side time accumulator for wave phase evolution.
        /// </summary>
        public float TimeAccumulator;
    }

    /// <summary>
    /// Blittable Gerstner-wave component consumed by Burst jobs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GerstnerWaveComponent
    {
        /// <summary>
        /// Normalized XZ travel direction.
        /// </summary>
        public float2 DirectionXZ;

        /// <summary>
        /// Vertical amplitude in meters.
        /// </summary>
        public float Amplitude;

        /// <summary>
        /// Wavelength in meters.
        /// </summary>
        public float Wavelength;

        /// <summary>
        /// Horizontal-displacement factor.
        /// </summary>
        public float Steepness;

        /// <summary>
        /// Authoring-time phase offset in radians.
        /// </summary>
        public float PhaseOffset;

        /// <summary>
        /// Speed multiplier applied to the analytic phase velocity.
        /// </summary>
        public float SpeedMultiplier;
    }

    /// <summary>
    /// Zero-allocation weather snapshot consumed by physics and VFX systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WeatherRuntimeSnapshot
    {
        /// <summary>
        /// Active weather-state flags for this frame.
        /// </summary>
        public WeatherState StateMask;

        /// <summary>
        /// Transition alpha across the active weather-state change.
        /// </summary>
        public float WeatherIntensity;

        /// <summary>
        /// Resolved world-space global current vector.
        /// </summary>
        public float3 GlobalCurrentVector;

        /// <summary>
        /// Resolved world-space global wind vector.
        /// </summary>
        public float3 GlobalWindVector;

        /// <summary>
        /// Shared metadata for current-driven consumers.
        /// </summary>
        public CurrentMeta CurrentMeta;

        /// <summary>
        /// First wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave0;

        /// <summary>
        /// Second wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave1;

        /// <summary>
        /// Third wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave2;
    }

    /// <summary>
    /// Minimal input service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// True when the authoritative player-input map is enabled and safe for gameplay reads.
        /// </summary>
        bool IsPlayerInputEnabled { get; }

        /// <summary>
        /// Discrete interact input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnInteract;

        /// <summary>
        /// Discrete tool-slot-one input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot1;

        /// <summary>
        /// Discrete tool-slot-two input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot2;

        /// <summary>
        /// Discrete tool-slot-three input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot3;

        /// <summary>
        /// Discrete tool-slot-four input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot4;

        /// <summary>
        /// Discrete primary-action input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnPrimaryAction;

        /// <summary>
        /// Discrete secondary-action input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnSecondaryAction;

        /// <summary>
        /// Discrete PDA toggle input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnPDA;

        /// <summary>
        /// Discrete inventory input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnInventory;

        /// <summary>
        /// Discrete cancel/back input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnCancel;

        /// <summary>
        /// Discrete next-tab input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnTabNext;

        /// <summary>
        /// Discrete previous-tab input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnTabPrevious;

        /// <summary>
        /// Returns the cached input snapshot captured once at the start of the current frame.
        /// </summary>
        /// <returns>Zero-GC input snapshot for the current frame.</returns>
        PlayerInputState GetState();

        /// <summary>
        /// Adds a buffered action token into the bounded input ring for delayed consumption.
        /// </summary>
        /// <param name="action">Buffered action token to record.</param>
        void BufferAction(PlayerBufferedAction action);

        /// <summary>
        /// Consumes the newest valid buffered action matching the requested token.
        /// </summary>
        /// <param name="action">Buffered action token to resolve.</param>
        /// <param name="maxAgeSeconds">Maximum valid input age in seconds. Values below zero fall back to the service default.</param>
        /// <returns>True when a valid buffered action was consumed.</returns>
        bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds);

        /// <summary>
        /// Switches native input routing to gameplay.
        /// </summary>
        void SwitchToPlayerInput();

        /// <summary>
        /// Switches native input routing to UI.
        /// </summary>
        void SwitchToUIInput();
    }

    /// <summary>
    /// Registry-owned global meta profile service.
    /// </summary>
    public interface IProfileService
    {
        event System.Action ProfileChanged;
        int ExplorerPoints { get; }
        float MaxDepthMeters { get; }
        float LongestLifeWithoutDeathSeconds { get; }
        int HighestBiomeDiscoveriesInSingleRun { get; }
        bool HasUnlockedAchievement(string achievementId);
        int GetUpgradeLevel(string upgradeId);
        bool TryPurchaseUpgrade(string upgradeId, out string error);
        GlobalProfileData GetSnapshot();
    }

    /// <summary>
    /// Authoritative PDA logbook append service exposed through <see cref="GlobalRegistry"/>.
    /// Stored entries are event/localization hashes, not persistent strings.
    /// </summary>
    public interface IPDALogbookService
    {
        /// <summary>
        /// Total number of retained PDA logbook events.
        /// </summary>
        int EntryCount { get; }

        /// <summary>
        /// Appends one deduplicated PDA log event by source keys.
        /// </summary>
        bool TryAppendEntry(string originKey, string titleKey, string messageKey);
    }

    /// <summary>
    /// Authoritative physics routing service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPhysicsService
    {
        /// <summary>
        /// True once the physics routing owner is initialized and ready to accept packets.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Queues a force packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues a force packet for deferred main-thread application at a specific world-space position.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="worldPosition">World-space application point.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues a torque packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true);

        /// <summary>
        /// Clears all queued packets and cached body slots.
        /// </summary>
        void ClearQueuedPackets();
    }

    /// <summary>
    /// Minimal audio service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Mixer group used for helmet/UI playback.
        /// </summary>
        AudioMixerGroup InterfaceGroup { get; }

        /// <summary>
        /// Mixer group used for ambient bed playback.
        /// </summary>
        AudioMixerGroup AmbientGroup { get; }

        /// <summary>
        /// Plays one world-space clip through the authored 3D pool.
        /// </summary>
        void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f);

        /// <summary>
        /// Plays one world-space clip through the authored 3D pool and explicit mixer route.
        /// </summary>
        void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup);

        /// <summary>
        /// Plays one helmet/UI clip through the authored 2D pool.
        /// </summary>
        void PlayStatic2D(AudioClip clip, float volume = 1f);

        /// <summary>
        /// Plays one helmet/UI clip through the authored 2D pool and explicit mixer route.
        /// </summary>
        void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup);

        /// <summary>
        /// Returns the 360-degree acoustic radar ring payload when available.
        /// </summary>
        bool TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution);

        /// <summary>
        /// Returns the 8x4 acoustic radar grid payload when available.
        /// </summary>
        bool TryGetAcousticRadarGridPayload(
            out NativeArray<float> energyGrid,
            out int azimuthBins,
            out int elevationBins,
            out ComputeBuffer gridBuffer);

        /// <summary>
        /// Emits one sandboxed mod acoustic ping through the engine-owned sensory path.
        /// </summary>
        /// <param name="runtimePosition">Frame-space ping origin.</param>
        /// <param name="intensity01">Normalized signal intensity.</param>
        /// <returns>True when the ping was accepted into the sensory path.</returns>
        bool TryEmitModAcousticPing(Vector3 runtimePosition, float intensity01);

        /// <summary>
        /// Stops every active world and UI voice immediately.
        /// </summary>
        void StopAll();
    }

    /// <summary>
    /// Authoritative scene transition service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>
        /// True when scene transitions are permitted by bootstrap state.
        /// </summary>
        bool CanLoadScene { get; }

        /// <summary>
        /// Performs a guarded scene transition.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        void LoadScene(string sceneName);

        /// <summary>
        /// Performs a guarded asynchronous scene transition with activation gating.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        Awaitable LoadSceneAsync(string sceneName);
    }

    /// <summary>
    /// Authoritative save-system contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// True once the save owner has completed runtime initialization and registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// True while a save or load transaction is in flight.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Current accumulated play time in seconds.
        /// </summary>
        float CurrentPlayTimeSeconds { get; }

        /// <summary>
        /// Registers one saveable owner into the ordered persistence registry.
        /// </summary>
        /// <param name="saveable">Persistence owner to register.</param>
        void Register(ISaveable saveable);

        /// <summary>
        /// Removes one saveable owner from the ordered persistence registry.
        /// </summary>
        /// <param name="saveable">Persistence owner to unregister.</param>
        void Unregister(ISaveable saveable);

        /// <summary>
        /// Starts an asynchronous save transaction for the requested slot.
        /// </summary>
        /// <param name="slotName">Persistent slot identifier.</param>
        Awaitable SaveGameAsync(string slotName);

        /// <summary>
        /// Starts an asynchronous load transaction for the requested slot.
        /// </summary>
        /// <param name="slotName">Persistent slot identifier.</param>
        Awaitable LoadGameAsync(string slotName);
    }

    /// <summary>
    /// Minimal UI service contract exposed through <see cref="GlobalRegistry"/>.
    /// Exactly one authoritative UI root may occupy the registry slot at runtime.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Authoritative player runtime-context contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPlayerRuntimeContext
    {
        /// <summary>
        /// True once the context owner completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current bootstrap-published player object.
        /// </summary>
        GameObject PlayerObject { get; }

        /// <summary>
        /// Current bootstrap-published player transform.
        /// </summary>
        Transform PlayerTransform { get; }

        /// <summary>
        /// Player locomotion owner resolved from the current player root.
        /// </summary>
        HectonPlayerMovement PlayerMovement { get; }

        /// <summary>
        /// Player rigidbody resolved from the current player root.
        /// </summary>
        Rigidbody PlayerRigidbody { get; }

        /// <summary>
        /// Authoritative handheld-tool owner on the current player root.
        /// </summary>
        PlayerToolManager ToolManager { get; }

        /// <summary>
        /// Authoritative player inventory on the current player root.
        /// </summary>
        PlayerInventory Inventory { get; }

        /// <summary>
        /// Authoritative player camera resolved from player-owned movement state.
        /// </summary>
        Camera PlayerCamera { get; }

        /// <summary>
        /// Cached PDA owner bound to the active player when available.
        /// </summary>
        PlayerPDA PlayerPDA { get; }

        /// <summary>
        /// Cached builder backend bound to the active player when available.
        /// </summary>
        PlayerBuilder PlayerBuilder { get; }

        /// <summary>
        /// Cached visor controller bound to the active player when available.
        /// </summary>
        VisorHUDController VisorController { get; }

        /// <summary>
        /// Cached player flashlight owner when available.
        /// </summary>
        PlayerFlashlight Flashlight { get; }

        /// <summary>
        /// Cached player thruster-audio owner when available.
        /// </summary>
        PlayerThrusterAudio ThrusterAudio { get; }

        /// <summary>
        /// Cached underwater-visual owner bound to the active player when available.
        /// </summary>
        HectonUnderwaterVisuals UnderwaterVisuals { get; }

        /// <summary>
        /// Hand-anchor transform used by held tools.
        /// </summary>
        Transform HandAnchor { get; }

        /// <summary>
        /// Root collider used by player-centric environment systems.
        /// </summary>
        Collider PlayerCollider { get; }

        /// <summary>
        /// Active HUD notification sink when one is available.
        /// </summary>
        HUDNotification HudNotification { get; }
    }

    /// <summary>
    /// Focused player inventory/tooling context extracted from the player god object.
    /// Consumers should prefer this service over root-player component scraping.
    /// </summary>
    public interface IPlayerInventoryService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current authoritative handheld-tool owner.
        /// </summary>
        PlayerToolManager ToolManager { get; }

        /// <summary>
        /// Current authoritative player inventory.
        /// </summary>
        PlayerInventory Inventory { get; }

        /// <summary>
        /// Current builder backend bound to the active player when available.
        /// </summary>
        PlayerBuilder PlayerBuilder { get; }

        /// <summary>
        /// Hand-anchor transform used by held tools.
        /// </summary>
        Transform HandAnchor { get; }
    }

    /// <summary>
    /// Focused modular-equipment runtime service exposed through <see cref="GlobalRegistry"/>.
    /// Hot-path consumers read compiled tool stats and state from this owner only.
    /// </summary>
    public interface IModularEquipmentService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Registers or refreshes one active handheld tool and returns its stable runtime ID.
        /// </summary>
        uint RegisterTool(PlayerTool tool);

        /// <summary>
        /// Unregisters one active handheld tool.
        /// </summary>
        void UnregisterTool(PlayerTool tool, uint toolId);

        /// <summary>
        /// Returns the current tool-state snapshot when the tool is active.
        /// </summary>
        bool TryGetToolState(uint toolId, out ToolState state);

        /// <summary>
        /// Returns the current compiled stat snapshot when the tool is active.
        /// </summary>
        bool TryGetToolStats(uint toolId, out ToolRuntimeStats stats);

        /// <summary>
        /// Installs one module into the active tool runtime and recompiles its bitmask-backed stats.
        /// </summary>
        bool TryInstallModule(uint toolId, ToolModuleData module);

        /// <summary>
        /// Removes one module from the active tool runtime and recompiles its bitmask-backed stats.
        /// </summary>
        bool TryRemoveModule(uint toolId, string moduleId);

        /// <summary>
        /// Returns true when the supplied upgrade flag is active on the tool.
        /// </summary>
        bool HasUpgrade(uint toolId, ToolUpgradeBits flag);

        float GetMaxRange(uint toolId, float fallback);
        float GetPowerScalar(uint toolId, float fallback);
        float GetEfficiencyScalar(uint toolId, float fallback);
        float GetSpeedScalar(uint toolId, float fallback);
        float GetHeatGenerationRate(uint toolId, float fallback);
        float GetCooldownRate(uint toolId, float fallback);
        float GetBatteryDrainPerSecond(uint toolId, float fallback);
        float GetDurabilityDrainMultiplier(uint toolId, float fallback);
        float GetRecoilImpulse(uint toolId, float fallback);
        float GetBatteryNormalized(uint toolId, float fallback);
        void SetBattery(uint toolId, float normalizedBattery);
        void ConsumeBattery(uint toolId, float normalizedBatteryDelta);
        void SetHeat(uint toolId, float normalizedHeat);
        void SetDurability(uint toolId, float normalizedDurability);
    }

    /// <summary>
    /// Focused player sensory/presentation context extracted from the player god object.
    /// Consumers should prefer this service over root-player component scraping.
    /// </summary>
    public interface IPlayerSensoryService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Authoritative player camera.
        /// </summary>
        Camera PlayerCamera { get; }

        /// <summary>
        /// Cached player flashlight owner when available.
        /// </summary>
        PlayerFlashlight Flashlight { get; }

        /// <summary>
        /// Cached player thruster-audio owner when available.
        /// </summary>
        PlayerThrusterAudio ThrusterAudio { get; }

        /// <summary>
        /// Cached underwater-visual owner when available.
        /// </summary>
        HectonUnderwaterVisuals UnderwaterVisuals { get; }

        /// <summary>
        /// Cached visor controller when available.
        /// </summary>
        VisorHUDController VisorController { get; }

        /// <summary>
        /// Active HUD notification sink when available.
        /// </summary>
        HUDNotification HudNotification { get; }
    }

    /// <summary>
    /// Authoritative environment runtime-context contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEnvironmentRuntimeContext
    {
        /// <summary>
        /// True once the context owner completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Authoritative construction manager for module placement and integrity checks.
        /// </summary>
        ConstructionManager ConstructionManager { get; }

        /// <summary>
        /// Authoritative buildable catalog resolved from the construction manager.
        /// </summary>
        ModuleCatalog ModuleCatalog { get; }

        /// <summary>
        /// Authoritative runtime hazard registry.
        /// </summary>
        HazardZoneManager HazardZones { get; }
    }

    /// <summary>
    /// Authoritative global weather contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Active weather-state flags for the current frame.
        /// </summary>
        WeatherState CurrentWeatherState { get; }

        /// <summary>
        /// Global world-space current vector in meters per second.
        /// </summary>
        Vector3 GlobalCurrentVector { get; }

        /// <summary>
        /// Global world-space wind vector in meters per second.
        /// </summary>
        Vector3 GlobalWindVector { get; }

        /// <summary>
        /// Transition alpha used by consumers for smooth blending.
        /// </summary>
        float WeatherIntensity { get; }

        /// <summary>
        /// Returns the latest zero-allocation runtime snapshot.
        /// </summary>
        WeatherRuntimeSnapshot GetRuntimeSnapshot();
    }

    /// <summary>
    /// Authoritative thermodynamics service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IThermodynamicsService
    {
        /// <summary>
        /// True once the thermodynamics owner is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Samples hydrothermal flow and cable entanglement without allocating.
        /// </summary>
        /// <param name="positionWS">World-space sample point.</param>
        /// <param name="radiusWS">Additional sample radius.</param>
        /// <param name="sample">Resolved flow and cable payload.</param>
        /// <returns>True when any updraft or cable influence is active at the sample point.</returns>
        bool SampleThermalFlow(Vector3 positionWS, float radiusWS, out AbyssalThermalManager.ThermalFlowSample sample);
    }

    /// <summary>
    /// Authoritative logistics/build-network service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ILogisticsService
    {
        /// <summary>
        /// True once the logistics owner is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Total number of runtime modules tracked by the logistics owner.
        /// </summary>
        int ModuleCount { get; }

        /// <summary>
        /// Authoritative buildable catalog used for module restoration and placement.
        /// </summary>
        ModuleCatalog Catalog { get; }

        /// <summary>
        /// Read-only live module registry.
        /// </summary>
        IReadOnlyList<GameObject> SpawnedModules { get; }

        /// <summary>
        /// Creates a temporary bypass edge between two placed base modules when permitted.
        /// </summary>
        /// <param name="sourceModule">Source base module.</param>
        /// <param name="destinationModule">Destination base module.</param>
        /// <returns>True when a bypass edge was added.</returns>
        bool TryCreateTemporaryBypass(BaseModule sourceModule, BaseModule destinationModule);
    }

    /// <summary>
    /// Authoritative world-generation service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IWorldGenService
    {
        /// <summary>
        /// True once the world-generation owner is registered and ready to process bootstrap/world refresh work.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Primes the cold bootstrap scatter pass when the world scene is ready.
        /// </summary>
        /// <returns>True when the pass was accepted.</returns>
        bool TryPrimeBootstrapScatterPass();

        /// <summary>
        /// Rebuilds the current scatter preview.
        /// </summary>
        void RebuildScatterPreview();

        /// <summary>
        /// Clears the current scatter preview.
        /// </summary>
        void ClearScatterPreview();
    }

    /// <summary>
    /// Deterministic world-seed provider exposed through <see cref="GlobalRegistry"/> for save/header validation.
    /// </summary>
    public interface IWorldSeedProvider
    {
        /// <summary>
        /// True once the seed owner is registered and ready to answer save/load validation queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Runtime world seed used by procedural geology and save-header consistency checks.
        /// </summary>
        int RuntimeWorldSeed { get; }
    }

    /// <summary>
    /// Authoritative encounter-direction service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEncounterDirectorService
    {
        /// <summary>
        /// True once the encounter director is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current normalized tension score in the legacy 0..100 presentation range.
        /// </summary>
        float TensionScore { get; }

        /// <summary>
        /// True while predator pressure is allowed to escalate.
        /// </summary>
        bool IsPredatorPressureEnabled { get; }

        /// <summary>
        /// Human-readable current phase name for diagnostics consumers.
        /// </summary>
        string CurrentPhaseName { get; }

        /// <summary>
        /// Forces the next completed encounter tick into the peak phase.
        /// </summary>
        void ForcePeak();

        /// <summary>
        /// Forces the next completed encounter tick into the relax phase.
        /// </summary>
        void ForceRelax();

        /// <summary>
        /// Resets the runtime encounter state.
        /// </summary>
        void ResetDirector();
    }

    /// <summary>
    /// Authoritative quest-system service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IQuestSystem
    {
        /// <summary>
        /// True once the quest runtime owner is registered and available for gameplay queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Activates the authored quest when it exists in the registry.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        void ActivateQuest(string questId);

        /// <summary>
        /// Completes the authored quest when it exists in the registry.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        void CompleteQuest(string questId);

        /// <summary>
        /// Returns true when the quest is currently active.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsActive(string questId);

        /// <summary>
        /// Returns true when the quest is currently completed.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsCompleted(string questId);

        /// <summary>
        /// Resolves the authored quest identifier from a stable quest hash.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        /// <param name="questId">Resolved authored quest identifier.</param>
        /// <returns>True when the hash maps to an authored quest.</returns>
        bool TryGetQuestIdByHash(uint questHash, out string questId);
    }

    /// <summary>
    /// Data-only fauna simulation contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IFaunaSim
    {
        /// <summary>
        /// True once the simulation owner has allocated its native residency buffers.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Maximum resident/dehydrated fauna slots available to the simulation.
        /// </summary>
        int ResidentSlotCapacity { get; }
    }

    /// <summary>
    /// Data-only fluid simulation math contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IFluidSim
    {
        /// <summary>
        /// True once the math service is ready for deterministic simulation calls.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Resolved water density used by submarine flood mass calculations.
        /// </summary>
        float WaterDensityKilogramsPerCubicMeter { get; }

        /// <summary>
        /// Resolves Torricelli ingress velocity at the supplied depth.
        /// </summary>
        /// <param name="depthMeters">External water depth in meters.</param>
        /// <returns>Ingress velocity in meters per second.</returns>
        float ResolveIngressVelocity(float depthMeters);
    }

    /// <summary>
    /// Boot-time quality tier resolved from immutable hardware facts.
    /// </summary>
    public enum HectonQualityTier : byte
    {
        Unknown = 0,
        Low = 1,
        Mx350 = 2,
        Mid = 3,
        High = 4,
        Ultra = 5
    }

    /// <summary>
    /// Immutable hardware profile captured during the bootstrap HardwareCheck phase.
    /// </summary>
    public readonly struct HectonHardwareProfile
    {
        /// <summary>
        /// Creates a boot-time hardware profile.
        /// </summary>
        public HectonHardwareProfile(
            int graphicsMemoryMegabytes,
            int systemMemoryMegabytes,
            int processorCount,
            HectonQualityTier qualityTier)
        {
            GraphicsMemoryMegabytes = graphicsMemoryMegabytes;
            SystemMemoryMegabytes = systemMemoryMegabytes;
            ProcessorCount = processorCount;
            QualityTier = qualityTier;
        }

        /// <summary>Detected graphics memory in megabytes.</summary>
        public int GraphicsMemoryMegabytes { get; }

        /// <summary>Detected system memory in megabytes.</summary>
        public int SystemMemoryMegabytes { get; }

        /// <summary>Detected logical CPU core count.</summary>
        public int ProcessorCount { get; }

        /// <summary>Resolved runtime quality tier.</summary>
        public HectonQualityTier QualityTier { get; }
    }

    /// <summary>
    /// Immutable service rebound payload published after a live registry slot is replaced.
    /// </summary>
    public readonly struct ServiceReboundEvent
    {
        /// <summary>
        /// Creates a service rebound notification.
        /// </summary>
        public ServiceReboundEvent(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ServiceSlot = serviceSlot;
            PreviousService = previousService;
            CurrentService = currentService;
        }

        /// <summary>Registry slot that was replaced.</summary>
        public GlobalRegistryServiceSlot ServiceSlot { get; }

        /// <summary>Previous service instance, or null when the slot was empty.</summary>
        public object PreviousService { get; }

        /// <summary>Current service instance, or null when the slot was cleared.</summary>
        public object CurrentService { get; }
    }

    public enum RegistryEventType : byte
    {
        ServiceRebound = 0
    }

    /// <summary>
    /// Unmanaged registry event payload drained by <see cref="SystemDispatcher"/>.
    /// Managed service references are carried by GlobalRegistry sidecar slots during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RegistryEventPayload
    {
        public uint PreviousServiceHash;
        public uint CurrentServiceHash;
        public int ReferenceSlot;
        public uint FrameIndex;
        public ushort ServiceSlot;
        public ushort EventType;
    }

    /// <summary>
    /// Listener contract for registry service-rebound payloads.
    /// </summary>
    public interface IRegistryEventListener
    {
        void OnRegistryEvent(in RegistryEventPayload payload);
    }

    /// <summary>
    /// Typed registry service slot identifiers used by GlobalRegistry hot-swap notifications.
    /// </summary>
    public enum GlobalRegistryServiceSlot : byte
    {
        Input = 0,
        Physics = 1,
        Audio = 2,
        Scene = 3,
        Save = 4,
        UI = 5,
        ObjectPool = 6,
        Player = 7,
        PlayerInventory = 8,
        ModularEquipment = 9,
        PlayerSensory = 10,
        Environment = 11,
        Weather = 12,
        OceanKinematics = 13,
        PowerGrid = 14,
        Submarine = 15,
        SubmarineHullBreach = 16,
        InteractionSignals = 17,
        Debris = 18,
        EcosystemDirector = 19,
        ThermodynamicsService = 20,
        Logistics = 21,
        WorldGen = 22,
        EncounterDirector = 23,
        QuestSystem = 24,
        FluidRuntime = 25,
        ThermodynamicsRuntime = 26,
        NarrativeDirectorRuntime = 27,
        QuestRuntime = 28,
        TickManager = 29,
        Dispatcher = 30,
        RenderDispatcher = 31,
        PhysicsStateManager = 32,
        FaunaSimulation = 33,
        FluidSimulation = 34,
        PersistentWorldRegistry = 35,
        PDALogbook = 36,
        PlayerMotor = 37,
        Profile = 38,
        InputBinding = 39,
        CullingRuntime = 40,
        LODSystemRuntime = 41,
        DynamicResolutionRuntime = 42,
        ImpostorRuntime = 43,
        DepthZoneRuntime = 44,
        LocalizationRuntime = 45,
        AudioLogRuntime = 46,
        AtlasSignalRuntime = 47,
        FirstHourRuntime = 48,
        EmergencyRelayRuntime = 49,
        AtmosphereRuntime = 50,
        BeaconNetworkRuntime = 51,
        ScanLogRuntime = 52,
        ToolDurabilityRuntime = 53,
        LoreDatabaseRuntime = 54,
        AssetLifecycleRuntime = 55,
        AssetLoadDispatcherRuntime = 56,
        VRAMMonitorRuntime = 57,
        VRAMPressureRuntime = 58,
        RenderTextureLifecycleRuntime = 59,
        RenderTexturePoolRuntime = 60,
        WorldStateRuntime = 61,
        UserOptionsRuntime = 62,
        BiolumManagerRuntime = 63,
        AbyssalFluidDecalRuntime = 64,
        SargassumDragRuntime = 65,
        SargassumCutRuntime = 66,
        PlayerExpressionRuntime = 67,
        SpectrumRuntime = 68,
        SoundscapeRuntime = 69,
        AcousticZoneRuntime = 70,
        SurfaceWeatherRuntime = 71,
        EnvironmentalStrainRuntime = 72,
        EcosystemHealthRuntime = 73,
        FaunaGeneticsRuntime = 74,
        PlayerExplorationRuntime = 75,
        DiscoveryRuntime = 76,
        ResourceScarcityRuntime = 77,
        PDAExchangeRuntime = 78,
        PlayerActionRuntime = 79,
        PDAMarkerRuntime = 80,
        AmbientWaterMotionRuntime = 81,
        SuitUpgradeRuntime = 82,
        EndingRuntime = 83,
        Atlas6DirectiveRuntime = 84,
        HazardZoneRuntime = 85,
        MissionRuntime = 86,
        RockManagerRuntime = 87,
        CameraJuiceRuntime = 88,
        MusicDirectorRuntime = 89,
        SubtitleRuntime = 90,
        AtlasSignalDecoderRuntime = 91,
        ScrapRuntime = 92,
        AutonomousExtractorRuntime = 93,
        VisorRTRuntime = 94,
        CameraRTRuntime = 95,
        PostFXRTRuntime = 96,
        UIRTRuntime = 97,
        SettingsRuntime = 98,
        RuntimeWatchdogRuntime = 99,
        CrashTelemetryRuntime = 100,
        PlayerCriticalAudioRuntime = 101,
        MapMagicRuntime = 102,
        ProceduralFieldSamplerRuntime = 103,
        ResourceDistributionRuntime = 104,
        RandomEventRuntime = 105,
        EclipseGameplayRuntime = 106,
        WorldSeedProvider = 107,
        GeologyTerrainSeamRuntime = 108,
        GeologyVoxelBridgeRuntime = 109,
        Unknown = 255
    }

    /// <summary>
    /// Explicit dependency-rebind hook used when <see cref="GlobalRegistry"/> safely replaces a live service.
    /// Implementers must re-run the dependency portion of their enable-time wiring without manually invoking Unity lifecycle methods.
    /// </summary>
    public interface IGlobalRegistryHotSwapListener
    {
        /// <summary>
        /// Called after a service slot has been replaced or cleared at runtime.
        /// </summary>
        /// <param name="serviceSlot">Registry slot that changed.</param>
        /// <param name="previousService">Previous service instance, or null if the slot was empty.</param>
        /// <param name="currentService">Current service instance, or null if the slot was cleared.</param>
        void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService);
    }

    /// <summary>
    /// Registry-backed ocean provider selector published through <see cref="GlobalRegistry"/>.
    /// Gameplay systems must query this service instead of talking to Crest-adapter singletons directly.
    /// </summary>
    public interface IHectonOceanKinematicsService
    {
        /// <summary>
        /// True once the selector service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Highest-priority currently available ocean kinematics provider.
        /// </summary>
        IHectonOceanKinematics ActiveProvider { get; }
    }

    /// <summary>
    /// Authoritative queued interaction-signal service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInteractionSignalService
    {
        /// <summary>
        /// True once the service is registered and ready to accept interaction packets.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Publishes one queued interaction signal for deferred late-frame dispatch.
        /// </summary>
        /// <param name="signal">Signal payload copied into the queue.</param>
        /// <param name="targetCollider">Resolved collider reference associated with the signal target.</param>
        /// <returns>True when the signal was accepted.</returns>
        bool Publish(in Hecton8.Interaction.InteractionSignal signal, Collider targetCollider);

        /// <summary>
        /// Performs the shared zero-allocation tool hit query from a preformatted interaction packet.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="packet">Blittable tool request packet copied by value into the service-owned raycast lane.</param>
        /// <param name="layerMask">Physics layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool TryRaycastPrimary(ulong requesterId, in Hecton8.Interaction.InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit);

        /// <summary>
        /// Performs the shared zero-allocation tool hit query using the service-owned buffers.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="origin">Runtime-space ray origin.</param>
        /// <param name="direction">Runtime-space ray direction.</param>
        /// <param name="range">Maximum query range.</param>
        /// <param name="layerMask">Physics layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool TryRaycastPrimary(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit);

        /// <summary>
        /// Clears all queued interaction signals and associated transient target references.
        /// </summary>
        void ClearQueuedSignals();
    }

    /// <summary>
    /// Runtime debris burst service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IDebrisService
    {
        /// <summary>
        /// True once the service is registered and ready to accept debris spawn requests.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Spawns one debris burst using a pre-baked chunk definition.
        /// </summary>
        /// <param name="definition">Authoritative chunk definition.</param>
        /// <param name="runtimeOrigin">Runtime-space origin of the intact object.</param>
        /// <param name="runtimeRotation">Runtime-space rotation of the intact object.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="runtimeHitNormal">Runtime-space impact normal.</param>
        /// <param name="power01">Normalized tool power.</param>
        /// <param name="seed">Deterministic burst seed.</param>
        /// <returns>True when the burst was accepted.</returns>
        bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed);

        /// <summary>
        /// Spawns one bounded debris burst using a pre-baked chunk definition.
        /// </summary>
        /// <param name="definition">Authoritative chunk definition.</param>
        /// <param name="runtimeOrigin">Runtime-space origin of the intact object.</param>
        /// <param name="runtimeRotation">Runtime-space rotation of the intact object.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="runtimeHitNormal">Runtime-space impact normal.</param>
        /// <param name="power01">Normalized tool power.</param>
        /// <param name="seed">Deterministic burst seed.</param>
        /// <param name="maxChunkCount">Maximum authored chunks to activate. Values below one use all valid chunks.</param>
        /// <param name="lifetimeSeconds">Optional pooled lifetime override. Values at or below zero use the default profile lifetime.</param>
        /// <returns>True when the burst was accepted.</returns>
        bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed,
            int maxChunkCount,
            float lifetimeSeconds);

        /// <summary>
        /// Clears all active chunk bursts immediately.
        /// </summary>
        void ClearActiveDebris();
    }

    /// <summary>
    /// Immutable ecosystem population sample returned by <see cref="IEcosystemDirectorService"/>.
    /// </summary>
    public struct EcosystemSectorPopulationSample
    {
        /// <summary>
        /// Quantized 1 km sector coordinate on the X axis.
        /// </summary>
        public int SectorX;

        /// <summary>
        /// Quantized 1 km sector coordinate on the Z axis.
        /// </summary>
        public int SectorZ;

        /// <summary>
        /// Current prey population carried by the sector simulation.
        /// </summary>
        public int PreyPopulation;

        /// <summary>
        /// Current predator population carried by the sector simulation.
        /// </summary>
        public int PredatorPopulation;

        /// <summary>
        /// Normalized prey fitness derived from sustained sector stress and survivor adaptation.
        /// </summary>
        public float Fitness;

        /// <summary>
        /// Sector-authored prey speed multiplier applied to spawned swarm agents.
        /// </summary>
        public float SpeedMultiplier;

        /// <summary>
        /// Sector-authored prey camouflage bias applied to spawned swarm agents.
        /// </summary>
        public float CamouflageIndex;
    }

    /// <summary>
    /// Sector-level ecosystem population service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEcosystemDirectorService
    {
        /// <summary>
        /// True once the director initialized its runtime buffers and is ready to answer population queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Normalized hostility score representing how aggressively the biome is responding to player ecological damage.
        /// </summary>
        float BiomeHostility01 { get; }

        /// <summary>
        /// Resolves the sector population sample for the supplied world position.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position to classify into a 1 km sector.</param>
        /// <param name="sample">Resolved predator/prey population sample for the containing sector.</param>
        /// <returns>True when the sector sample is available.</returns>
        bool TryGetSectorPopulation(Vector3 worldPosition, out EcosystemSectorPopulationSample sample);

        /// <summary>
        /// Registers prey consumption inside the containing sector so the next cold-tick solve includes the loss.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position where predation occurred.</param>
        /// <param name="preyConsumed">Number of prey removed from the sector population.</param>
        void ReportPredation(Vector3 worldPosition, int preyConsumed);

        /// <summary>
        /// Registers one player-attributed apex predator kill and escalates the biome hostility response.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position where the apex predator was killed.</param>
        /// <param name="hostilityDelta">Hostility increase applied before clamping.</param>
        void ReportApexPredatorKilled(Vector3 worldPosition, float hostilityDelta);

        /// <summary>
        /// Opens or clears the eclipse predator migration window that suppresses shallow light aversion.
        /// </summary>
        /// <param name="intensity01">Normalized predator migration pressure.</param>
        /// <param name="holdSeconds">Seconds to keep the migration window active.</param>
        void ApplyEclipsePredatorShallowMigration(float intensity01, float holdSeconds);

        /// <summary>
        /// Returns normalized eclipse suppression applied to predator light reactions at the supplied position.
        /// </summary>
        float ResolveEclipsePredatorLightSuppression01(Vector3 worldPosition);
    }

    /// <summary>
    /// Deterministic owner-local component resolution helper used to remove runtime hierarchy search APIs.
    /// </summary>
    public static class ComponentReferenceUtility
    {
        /// <summary>
        /// Resolves the first matching component on the supplied owner or its children.
        /// </summary>
        public static T ResolveOwnedComponent<T>(Component owner) where T : Component
        {
            return owner != null ? ResolveOwnedComponent<T>(owner.transform) : null;
        }

        /// <summary>
        /// Resolves the first matching component on the supplied transform or its children.
        /// </summary>
        public static T ResolveOwnedComponent<T>(Transform root) where T : Component
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out T component))
                return component;

            for (int i = 0; i < root.childCount; i++)
            {
                component = ResolveOwnedComponent<T>(root.GetChild(i));
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}

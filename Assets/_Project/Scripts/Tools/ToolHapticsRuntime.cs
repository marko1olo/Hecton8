using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// DataVault-backed haptic command lane. Device dispatch remains external; this owner only builds bounded payload views.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9916)]
    public sealed class ToolHapticsRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int BufferCapacity = 16;
        private const float DefaultDecayRate = 1.5f;
        private const float DefaultDurationSeconds = 0.18f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const float PhysicsImpulseHapticMinimumVolume = 0.08f;
        private const float PhysicsImpulseHapticDurationSeconds = 0.12f;
        private const float PhysicsImpulseHapticDecayRate = 4.2f;
        private const float HapticDebounceWindowSeconds = 0.05f;
        private const float MaxCommandDurationSeconds = 2f;
        private const float MaxCommandDecayRate = 64f;
        private const float MaxCommandFrequencyHz = 60f;
        private const ushort PhysicsEventTypeAcousticImpulse = 4;
        private const uint AcousticImpulseFlagCritical = 1u;
        internal const byte PriorityCritical = 3;
        internal const byte BlendModeOverride = 0;
        internal const byte BlendModeAdditive = 1;
        internal const byte BlendModeMax = 2;
        private static ToolHapticsRuntime s_runtime;
        private static int s_powerSaveMute;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private VaultGenerationHandle<HapticCommand> _frontBufferHandle;
        private VaultGenerationHandle<HapticCommand> _backBufferHandle;
        private int _frontCount;
        private int _backCount;
        private int _lastPhysicsEventSnapshotGeneration;
        private float _leftHapticCooldownTimer;
        private float _rightHapticCooldownTimer;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _registeredHotSwap;

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct HapticCommand
        {
            [FieldOffset(0)] public float LowFreqIntensity;
            [FieldOffset(4)] public float HighFreqIntensity;
            [FieldOffset(8)] public float DurationRemaining;
            [FieldOffset(12)] public float DecayRate;
            [FieldOffset(16)] public float BaseLowFreqIntensity;
            [FieldOffset(20)] public float BaseHighFreqIntensity;
            [FieldOffset(24)] public float ElapsedSeconds;
            [FieldOffset(28)] public float FrequencyHz;
            [FieldOffset(32)] public byte Priority;
            [FieldOffset(33)] public byte MotorMask;
            [FieldOffset(34)] public byte BlendMode;
            [FieldOffset(35)] public byte Reserved;
            [FieldOffset(36)] private uint _pad0;
            [FieldOffset(40)] private ulong _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        [Obsolete("Use TryEnqueueToolFeedback(float,float,byte) so bounded refusal stays visible at the producer.", true)]
        public static void EnqueueToolFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            TryEnqueueToolFeedback(powerDelivered, ratedPower, priority);
        }

        public static bool TryEnqueueToolFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return false;

            return runtime.TryEnqueueBackBuffer(powerDelivered, ratedPower, priority);
        }

        [Obsolete("Use TryEnqueueCommand(...) so bounded refusal stays visible at the producer.", true)]
        public static void EnqueueCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float decayRate,
            byte priority,
            byte motorMask,
            byte blendMode)
        {
            TryEnqueueCommand(
                lowFreqIntensity,
                highFreqIntensity,
                durationSeconds,
                decayRate,
                priority,
                motorMask,
                blendMode);
        }

        public static bool TryEnqueueCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float decayRate,
            byte priority,
            byte motorMask,
            byte blendMode)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return false;

            return runtime.TryEnqueueBackBufferCommand(
                lowFreqIntensity,
                highFreqIntensity,
                durationSeconds,
                decayRate,
                priority,
                motorMask,
                blendMode,
                0f);
        }

        /// <summary>
        /// Enqueues a bounded sinusoidal rumble envelope for critical UI and tool warnings.
        /// </summary>
        [Obsolete("Use TryEnqueueSinusoidalCommand(...) so bounded refusal stays visible at the producer.", true)]
        public static void EnqueueSinusoidalCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float frequencyHz,
            byte priority,
            byte motorMask)
        {
            TryEnqueueSinusoidalCommand(
                lowFreqIntensity,
                highFreqIntensity,
                durationSeconds,
                frequencyHz,
                priority,
                motorMask);
        }

        public static bool TryEnqueueSinusoidalCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float frequencyHz,
            byte priority,
            byte motorMask)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return false;

            return runtime.TryEnqueueBackBufferCommand(
                lowFreqIntensity,
                highFreqIntensity,
                durationSeconds,
                0f,
                priority,
                motorMask,
                BlendModeAdditive,
                frequencyHz);
        }

        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.ToolHaptics owner for player builds.
        /// Zero live scene/prefab GUID hits; previous EnsureRuntimeInstance only returned
        /// s_runtime and never constructed, so OnEnable registration never ran.
        /// </summary>
        public static ToolHapticsRuntime EnsureRuntimeInstance()
        {
            ToolHapticsRuntime active = s_runtime;
            if (active != null && active.isActiveAndEnabled)
                return active;

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[ToolHapticsRuntime]"); // COLD ALLOC
            return runtimeRoot.AddComponent<ToolHapticsRuntime>();
        }


        public static bool TryGetRuntime(out ToolHapticsRuntime runtime)
        {
            runtime = s_runtime;
            return runtime != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtime = null;
            Volatile.Write(ref s_powerSaveMute, 0);
        }

        public static bool PowerSaveMuteActive => Volatile.Read(ref s_powerSaveMute) != 0;

        public static void SetPowerSaveMuteGlobal(bool muted)
        {
            Volatile.Write(ref s_powerSaveMute, muted ? 1 : 0);
        }

        public void SetPowerSaveMute(bool muted)
        {
            int value = muted ? 1 : 0;
            Interlocked.Exchange(ref s_powerSaveMute, value);

            if (muted)
                ClearBuffers();
        }

        public void Tick(float deltaTime)
        {
            if (PowerSaveMuteActive)
            {
                ClearBuffers();
                return;
            }

            float safeDeltaTime = ClampHapticDeltaTime(deltaTime);
            _leftHapticCooldownTimer = math.max(0f, _leftHapticCooldownTimer - safeDeltaTime);
            _rightHapticCooldownTimer = math.max(0f, _rightHapticCooldownTimer - safeDeltaTime);

            if (!TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer) || _frontCount <= 0)
            {
                return;
            }

            int compactedCount = 0;
            int frontCount = math.min(math.max(0, _frontCount), BufferCapacity);
            for (int i = 0; i < frontCount; i++)
            {
                HapticCommand command = frontBuffer[i];
                if (command.DurationRemaining <= 0f)
                    continue;

                command.DurationRemaining = math.max(0f, command.DurationRemaining - safeDeltaTime);
                command.ElapsedSeconds = math.max(0f, command.ElapsedSeconds + safeDeltaTime);
                if (command.BaseLowFreqIntensity <= 0f && command.LowFreqIntensity > 0f)
                    command.BaseLowFreqIntensity = command.LowFreqIntensity;
                if (command.BaseHighFreqIntensity <= 0f && command.HighFreqIntensity > 0f)
                    command.BaseHighFreqIntensity = command.HighFreqIntensity;

                float decayFactor = ResolveHapticDecayFactor(command.DecayRate, safeDeltaTime);
                command.BaseLowFreqIntensity = math.saturate(command.BaseLowFreqIntensity * decayFactor);
                command.BaseHighFreqIntensity = math.saturate(command.BaseHighFreqIntensity * decayFactor);
                float wave = command.FrequencyHz > 0.001f
                    ? ResolveHapticTriangleWave(command.ElapsedSeconds, command.FrequencyHz)
                    : 1f;
                command.LowFreqIntensity = math.saturate(command.BaseLowFreqIntensity * wave);
                command.HighFreqIntensity = math.saturate(command.BaseHighFreqIntensity * wave);
                if (command.DurationRemaining <= 0f)
                    continue;

                if (command.LowFreqIntensity <= 0f && command.HighFreqIntensity <= 0f)
                    continue;

                frontBuffer[compactedCount++] = command;
            }

            for (int i = compactedCount; i < frontCount; i++)
            {
                frontBuffer[i] = default;
            }

            _frontCount = compactedCount;
        }

        private static float ResolveHapticDecayFactor(float decayRate, float deltaTime)
        {
            float x = math.min(ClampFiniteNonNegative(decayRate) * ClampFiniteNonNegative(deltaTime), 3f);
            float x2 = x * x;
            return math.saturate(math.rcp(1f + x + (0.5f * x2)));
        }

        private static float ResolveHapticTriangleWave(float elapsedSeconds, float frequencyHz)
        {
            float safeFrequencyHz = math.min(ClampFiniteNonNegative(frequencyHz), MaxCommandFrequencyHz);
            return HapticWaveformLibrary.EvaluateTriangle01(ClampFiniteNonNegative(elapsedSeconds), safeFrequencyHz);
        }

        public void LateFrameTick()
        {
            if (PowerSaveMuteActive)
            {
                ClearBuffers();
                return;
            }

            DrainPhysicsEventPayloads();

            if (!TryResolveBuffers(out NativeArray<HapticCommand> frontBuffer, out NativeArray<HapticCommand> backBuffer))
                return;

            int commandCount = math.min(math.max(0, _backCount), BufferCapacity);
            if (commandCount <= 0)
                return;

            for (int i = 0; i < commandCount; i++)
            {
                HapticCommand command = backBuffer[i];
                MergeCommandIntoFrontBuffer(frontBuffer, in command);
            }

            ClearBackBuffer(commandCount);
        }

        public unsafe ReadOnlySpan<HapticCommand> GetFrontBuffer()
        {
            if (!TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer))
                return ReadOnlySpan<HapticCommand>.Empty;

            int count = math.min(math.max(0, _frontCount), frontBuffer.Length);
            return count > 0
                ? new ReadOnlySpan<HapticCommand>(frontBuffer.GetUnsafeReadOnlyPtr(), count)
                : ReadOnlySpan<HapticCommand>.Empty;
        }

        internal unsafe bool TryGetFrontBufferSnapshot(out ReadOnlySpan<HapticCommand> frontBuffer, out int count)
        {
            frontBuffer = ReadOnlySpan<HapticCommand>.Empty;
            if (PowerSaveMuteActive)
            {
                count = 0;
                return false;
            }

            count = FrontCount;
            if (count <= 0)
                return false;

            if (!TryResolveFrontBuffer(out NativeArray<HapticCommand> buffer))
            {
                count = 0;
                return false;
            }

            count = math.min(count, buffer.Length);
            frontBuffer = new ReadOnlySpan<HapticCommand>(buffer.GetUnsafeReadOnlyPtr(), count);
            return true;
        }

        public int FrontCount => ResolveFrontCount();

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            CacheRegistryDependenciesCold();
            EnsureBuffers();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheRegistryDependenciesCold();
            s_runtime = this;
            EnsureBuffers();
            TryRegisterService();
            TryRegisterHotSwap();
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                DisposeBuffers();
                return;
            }

            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterHotSwap();
            TryUnregisterService();
            if (ReferenceEquals(s_runtime, this))
                s_runtime = null;
            _lastPhysicsEventSnapshotGeneration = 0;
            ClearBuffers();
            DisposeBuffers();
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                DisposeBuffers();
                return;
            }

            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterHotSwap();
            TryUnregisterService();
            if (ReferenceEquals(s_runtime, this))
                s_runtime = null;
            _lastPhysicsEventSnapshotGeneration = 0;
            ClearBuffers();
            DisposeBuffers();
        }

        private void DrainPhysicsEventPayloads()
        {
            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType != PhysicsEventTypeAcousticImpulse)
                    continue;

                HandlePhysicsAcousticImpulse(in payload);
            }
        }

        private void HandlePhysicsAcousticImpulse(in PhysicsEventPayload impulseEvent)
        {
            float impulseVolume = ClampFinite01(impulseEvent.Scalar1);
            if ((impulseEvent.StatusBits & AcousticImpulseFlagCritical) == 0u ||
                impulseVolume < PhysicsImpulseHapticMinimumVolume)
            {
                return;
            }

            Vector3 localDirection = impulseEvent.Direction;
            float3 direction3 = new float3(localDirection.x, localDirection.y, localDirection.z);
            if (!math.all(math.isfinite(direction3)))
                localDirection = Vector3.zero;

            IPlayerRuntimeContext player = _playerRuntimeContext;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform != null)
                localDirection = playerTransform.InverseTransformDirection(localDirection);

            float side = math.clamp(localDirection.x, -1f, 1f);
            float intensity = impulseVolume;
            byte motorMask;
            float leftIntensity;
            float rightIntensity;
            if (side < -0.15f)
            {
                motorMask = LeftMotorMask;
                leftIntensity = intensity;
                rightIntensity = 0f;
            }
            else if (side > 0.15f)
            {
                motorMask = RightMotorMask;
                leftIntensity = 0f;
                rightIntensity = intensity;
            }
            else
            {
                motorMask = BothMotorMask;
                leftIntensity = intensity * 0.65f;
                rightIntensity = intensity * 0.65f;
            }

            TryEnqueueBackBufferCommand(
                leftIntensity,
                rightIntensity,
                PhysicsImpulseHapticDurationSeconds,
                PhysicsImpulseHapticDecayRate,
                PriorityCritical,
                motorMask,
                BlendModeMax,
                0f);
        }

        private bool TryResolveBuffers(
            out NativeArray<HapticCommand> frontBuffer,
            out NativeArray<HapticCommand> backBuffer)
        {
            bool frontResolved = TryResolveFrontBuffer(out frontBuffer);
            bool backResolved = TryResolveBackBuffer(out backBuffer);
            return frontResolved && backResolved;
        }

        private bool TryOpenOrCreateBuffers(
            out NativeArray<HapticCommand> frontBuffer,
            out NativeArray<HapticCommand> backBuffer)
        {
            bool frontResolved = TryOpenOrCreateFrontBuffer(out frontBuffer);
            bool backResolved = TryOpenOrCreateBackBuffer(out backBuffer);
            return frontResolved && backResolved;
        }

        private int ResolveFrontCount()
        {
            return TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer)
                ? math.min(math.max(0, _frontCount), frontBuffer.Length)
                : 0;
        }

        private bool TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer)
        {
            return TryResolveHapticBuffer(BufferID.ToolHapticFrontCommands, ref _frontBufferHandle, out frontBuffer);
        }

        private bool TryOpenOrCreateFrontBuffer(out NativeArray<HapticCommand> frontBuffer)
        {
            return TryOpenOrCreateHapticBuffer(BufferID.ToolHapticFrontCommands, ref _frontBufferHandle, out frontBuffer);
        }

        private bool TryResolveBackBuffer(out NativeArray<HapticCommand> backBuffer)
        {
            return TryResolveHapticBuffer(BufferID.ToolHapticBackCommands, ref _backBufferHandle, out backBuffer);
        }

        private bool TryOpenOrCreateBackBuffer(out NativeArray<HapticCommand> backBuffer)
        {
            return TryOpenOrCreateHapticBuffer(BufferID.ToolHapticBackCommands, ref _backBufferHandle, out backBuffer);
        }

        private bool TryResolveHapticBuffer(
            BufferID bufferId,
            ref VaultGenerationHandle<HapticCommand> handle,
            out NativeArray<HapticCommand> buffer)
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (IsToolHapticHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= BufferCapacity)
            {
                return true;
            }

            return false;
        }

        private bool TryOpenOrCreateHapticBuffer(
            BufferID bufferId,
            ref VaultGenerationHandle<HapticCommand> handle,
            out NativeArray<HapticCommand> buffer)
        {
            if (TryResolveHapticBuffer(bufferId, ref handle, out buffer))
                return true;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (IsToolHapticHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);
            handle = default;

            VaultGenerationHandle<HapticCommand> acquired = vault.EnsureGenerationHandle<HapticCommand>(
                bufferId,
                BufferCapacity,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);
            if (!IsToolHapticHandle(in acquired, bufferId) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < BufferCapacity)
            {
                if (IsToolHapticHandle(in acquired, bufferId))
                    vault.ReleaseBuffer(in acquired);
                return false;
            }

            handle = acquired;
            return true;
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private static bool IsToolHapticHandle(in VaultGenerationHandle<HapticCommand> handle, BufferID expectedBufferId)
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.GameplayTools &&
                   handle.Generation != 0u;
        }

        private void EnsureBuffers()
        {
            TryOpenOrCreateBuffers(out _, out _);
        }

        private void DisposeBuffers()
        {
            ReleaseVaultHandles();
            _frontBufferHandle = default;
            _backBufferHandle = default;
            _dataVault = null;
            _frontCount = 0;
            _backCount = 0;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, BufferID.ToolHapticFrontCommands, ref _frontBufferHandle);
            ReleaseVaultHandle(vault, BufferID.ToolHapticBackCommands, ref _backBufferHandle);
        }

        private static void ReleaseVaultHandle(IDataVault vault, BufferID expectedBufferId, ref VaultGenerationHandle<HapticCommand> handle)
        {
            if (!IsToolHapticHandle(in handle, expectedBufferId))
            {
                handle = default;
                return;
            }

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void ClearBuffers()
        {
            ClearFrontBuffer();
            ClearBackBuffer();
            _leftHapticCooldownTimer = 0f;
            _rightHapticCooldownTimer = 0f;
        }

        private void ClearFrontBuffer()
        {
            if (TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer))
            {
                for (int i = 0; i < BufferCapacity; i++)
                    frontBuffer[i] = default;
            }

            _frontCount = 0;
        }

        private void ClearBackBuffer()
        {
            ClearBackBuffer(BufferCapacity);
        }

        private void ClearBackBuffer(int clearCount)
        {
            if (TryResolveBackBuffer(out NativeArray<HapticCommand> backBuffer))
            {
                int boundedClearCount = math.min(math.max(0, clearCount), BufferCapacity);
                for (int i = 0; i < boundedClearCount; i++)
                    backBuffer[i] = default;
            }

            _backCount = 0;
        }

        private bool TryEnqueueBackBuffer(float powerDelivered, float ratedPower, byte priority)
        {
            if (PowerSaveMuteActive)
                return false;

            float normalizedPower = math.isfinite(powerDelivered) && math.isfinite(ratedPower) && ratedPower > 0.0001f
                ? ClampFinite01(powerDelivered * math.rcp(ratedPower))
                : 0f;
            if (normalizedPower <= 0f)
                return false;

            byte motorMask = RightMotorMask;
            if (!TrySelectBackBufferSlot(priority, out int slotIndex))
                return false;

            if (!TryApplyHapticDebounce(ref motorMask, priority))
                return false;

            HapticCommand command = default;
            command.LowFreqIntensity = 0f;
            command.HighFreqIntensity = normalizedPower;
            command.BaseLowFreqIntensity = 0f;
            command.BaseHighFreqIntensity = normalizedPower;
            command.DurationRemaining = DefaultDurationSeconds;
            command.DecayRate = DefaultDecayRate;
            command.Priority = priority;
            command.MotorMask = motorMask;
            command.BlendMode = BlendModeAdditive;
            command.FrequencyHz = 0f;
            StoreBackBufferCommand(slotIndex, in command);
            return true;
        }

        private bool TryEnqueueBackBufferCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float decayRate,
            byte priority,
            byte motorMask,
            byte blendMode,
            float frequencyHz)
        {
            if (PowerSaveMuteActive)
                return false;

            byte resolvedMotorMask = (byte)(motorMask & BothMotorMask);
            if (resolvedMotorMask == 0)
                return false;

            float resolvedLow = math.isfinite(lowFreqIntensity)
                ? math.saturate(lowFreqIntensity)
                : 0f;
            float resolvedHigh = math.isfinite(highFreqIntensity)
                ? math.saturate(highFreqIntensity)
                : 0f;
            float resolvedDuration = math.isfinite(durationSeconds)
                ? math.clamp(durationSeconds, 0f, MaxCommandDurationSeconds)
                : 0f;
            float resolvedDecay = math.isfinite(decayRate)
                ? math.clamp(decayRate, 0f, MaxCommandDecayRate)
                : 0f;
            if ((resolvedLow <= 0f && resolvedHigh <= 0f) || resolvedDuration <= 0f)
                return false;

            if (!TrySelectBackBufferSlot(priority, out int slotIndex))
                return false;

            if (!TryApplyHapticDebounce(ref resolvedMotorMask, priority))
                return false;

            HapticCommand command = default;
            command.LowFreqIntensity = resolvedLow;
            command.HighFreqIntensity = resolvedHigh;
            command.BaseLowFreqIntensity = resolvedLow;
            command.BaseHighFreqIntensity = resolvedHigh;
            command.DurationRemaining = resolvedDuration;
            command.DecayRate = resolvedDecay;
            command.Priority = priority;
            command.MotorMask = resolvedMotorMask;
            command.BlendMode = (byte)math.clamp((int)blendMode, BlendModeOverride, BlendModeMax);
            command.FrequencyHz = math.isfinite(frequencyHz) ? math.clamp(frequencyHz, 0f, MaxCommandFrequencyHz) : 0f;
            StoreBackBufferCommand(slotIndex, in command);
            return true;
        }

        private bool TrySelectBackBufferSlot(byte priority, out int slotIndex)
        {
            slotIndex = -1;
            return TryResolveBackBuffer(out NativeArray<HapticCommand> backBuffer) &&
                   TrySelectBufferSlot(backBuffer, _backCount, priority, out slotIndex);
        }

        private bool TrySelectFrontBufferSlot(NativeArray<HapticCommand> frontBuffer, byte priority, out int slotIndex)
        {
            return TrySelectBufferSlot(frontBuffer, _frontCount, priority, out slotIndex);
        }

        private static bool TrySelectBufferSlot(
            NativeArray<HapticCommand> buffer,
            int count,
            byte priority,
            out int slotIndex)
        {
            slotIndex = -1;
            if (!buffer.IsCreated)
                return false;

            int activeCount = math.min(math.max(0, count), BufferCapacity);
            if (activeCount < BufferCapacity)
            {
                slotIndex = activeCount;
                return true;
            }

            byte lowestPriority = byte.MaxValue;
            float shortestRemaining = float.MaxValue;
            for (int i = 0; i < BufferCapacity; i++)
            {
                HapticCommand existing = buffer[i];
                if (existing.DurationRemaining <= 0f)
                {
                    slotIndex = i;
                    return true;
                }

                if (existing.Priority > lowestPriority)
                    continue;

                if (existing.Priority == lowestPriority && existing.DurationRemaining >= shortestRemaining)
                    continue;

                lowestPriority = existing.Priority;
                shortestRemaining = existing.DurationRemaining;
                slotIndex = i;
            }

            return slotIndex >= 0 && priority >= lowestPriority;
        }

        private void StoreBackBufferCommand(int slotIndex, in HapticCommand command)
        {
            if (!TryResolveBackBuffer(out NativeArray<HapticCommand> backBuffer) ||
                slotIndex < 0 ||
                slotIndex >= BufferCapacity)
            {
                return;
            }

            backBuffer[slotIndex] = command;
            _backCount = math.min(BufferCapacity, math.max(_backCount, slotIndex + 1));
        }

        private void MergeCommandIntoFrontBuffer(NativeArray<HapticCommand> frontBuffer, in HapticCommand command)
        {
            if (!frontBuffer.IsCreated || command.DurationRemaining <= 0f)
                return;

            if (!TrySelectFrontBufferSlot(frontBuffer, command.Priority, out int slotIndex))
                return;

            frontBuffer[slotIndex] = command;
            _frontCount = math.min(BufferCapacity, math.max(_frontCount, slotIndex + 1));
        }

        private bool TryApplyHapticDebounce(ref byte motorMask, byte priority)
        {
            if (priority >= PriorityCritical)
                return true;

            bool blocksLeft = (motorMask & LeftMotorMask) != 0 && _leftHapticCooldownTimer > 0f;
            bool blocksRight = (motorMask & RightMotorMask) != 0 && _rightHapticCooldownTimer > 0f;
            if (blocksLeft && blocksRight)
                return false;

            if (blocksLeft)
                motorMask = (byte)(motorMask & ~LeftMotorMask);
            if (blocksRight)
                motorMask = (byte)(motorMask & ~RightMotorMask);
            if (motorMask == 0)
                return false;

            if ((motorMask & LeftMotorMask) != 0)
                _leftHapticCooldownTimer = HapticDebounceWindowSeconds;
            if ((motorMask & RightMotorMask) != 0)
                _rightHapticCooldownTimer = HapticDebounceWindowSeconds;
            return true;
        }

        private void CacheRegistryDependenciesCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void RebindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            ReleaseVaultHandles();
            _dataVault = dataVault;
            _frontBufferHandle = default;
            _backBufferHandle = default;
            _frontCount = 0;
            _backCount = 0;
            if (Application.isPlaying && isActiveAndEnabled)
                EnsureBuffers();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService is IDataVault currentVault ? currentVault : null);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrame();
                    TryUnregisterUpdate();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterUpdate();
                        TryRegisterLateFrame();
                    }

                    break;
            }
        }

        private bool HasActiveHapticCooldown()
        {
            return _leftHapticCooldownTimer > 0f || _rightHapticCooldownTimer > 0f;
        }

        private static float ClampFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ClampHapticDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.min(math.max(0f, deltaTime), 0.05f) : 0f;
        }

        private static float ClampFiniteNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterToolHapticsRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ToolHaptics, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.ToolHaptics, this))
                GlobalRegistry.UnregisterToolHapticsRuntime(this);
            _serviceRegistered = false;
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

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }
    }
}

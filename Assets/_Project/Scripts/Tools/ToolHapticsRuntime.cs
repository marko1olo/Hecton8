using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Hecton8.World;
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
    public sealed class ToolHapticsRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IPhysicsAcousticImpulseEventListener
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
        internal const byte PriorityCritical = 3;
        internal const byte BlendModeOverride = 0;
        internal const byte BlendModeAdditive = 1;
        internal const byte BlendModeMax = 2;
        private static int s_powerSaveMute;

        private VaultBufferHandle<HapticCommand> _frontBufferHandle;
        private VaultBufferHandle<HapticCommand> _backBufferHandle;
        private int _frontCount;
        private int _backCount;
        private float _leftHapticCooldownTimer;
        private float _rightHapticCooldownTimer;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HapticCommand
        {
            public float LowFreqIntensity;
            public float HighFreqIntensity;
            public float DurationRemaining;
            public float DecayRate;
            public byte Priority;
            public byte MotorMask;
            public byte BlendMode;
            public byte Reserved;
            public float BaseLowFreqIntensity;
            public float BaseHighFreqIntensity;
            public float ElapsedSeconds;
            public float FrequencyHz;
        }

        public static void EnqueueToolFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return;

            runtime.EnqueueBackBuffer(powerDelivered, ratedPower, priority);
        }

        public static void EnqueueCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float decayRate,
            byte priority,
            byte motorMask,
            byte blendMode)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return;

            runtime.EnqueueBackBufferCommand(
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
        public static void EnqueueSinusoidalCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float frequencyHz,
            byte priority,
            byte motorMask)
        {
            if (!TryGetRuntime(out ToolHapticsRuntime runtime))
                return;

            runtime.EnqueueBackBufferCommand(
                lowFreqIntensity,
                highFreqIntensity,
                durationSeconds,
                0f,
                priority,
                motorMask,
                BlendModeAdditive,
                frequencyHz);
        }

        public static ToolHapticsRuntime EnsureRuntimeInstance()
        {
            return GlobalRegistry.ToolHaptics;
        }

        public static bool TryGetRuntime(out ToolHapticsRuntime runtime)
        {
            runtime = GlobalRegistry.ToolHaptics;
            return runtime != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volatile.Write(ref s_powerSaveMute, 0);
        }

        public static bool PowerSaveMuteActive => Volatile.Read(ref s_powerSaveMute) != 0;

        public void SetPowerSaveMute(bool muted)
        {
            int value = muted ? 1 : 0;
            if (Interlocked.Exchange(ref s_powerSaveMute, value) == value)
                return;

            if (!muted)
                return;

            ClearBuffers();
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
        }

        public void Tick(float deltaTime)
        {
            if (PowerSaveMuteActive)
            {
                ClearBuffers();
                TryUnregisterUpdate();
                return;
            }

            float safeDeltaTime = ClampHapticDeltaTime(deltaTime);
            _leftHapticCooldownTimer = math.max(0f, _leftHapticCooldownTimer - safeDeltaTime);
            _rightHapticCooldownTimer = math.max(0f, _rightHapticCooldownTimer - safeDeltaTime);

            if (!TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer) || _frontCount <= 0)
            {
                if (_backCount <= 0 && !HasActiveHapticCooldown())
                    TryUnregisterUpdate();
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
            if (_frontCount <= 0 && _backCount <= 0 && !HasActiveHapticCooldown())
                TryUnregisterUpdate();
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
                TryUnregisterLateFrame();
                return;
            }

            if (!TryResolveBuffers(out NativeArray<HapticCommand> frontBuffer, out NativeArray<HapticCommand> backBuffer))
            {
                TryUnregisterLateFrame();
                return;
            }

            int commandCount = math.min(math.max(0, _backCount), BufferCapacity);
            if (commandCount <= 0)
            {
                if (_frontCount <= 0)
                    TryUnregisterLateFrame();
                return;
            }

            for (int i = 0; i < commandCount; i++)
            {
                HapticCommand command = backBuffer[i];
                MergeCommandIntoFrontBuffer(frontBuffer, in command);
            }

            ClearBackBuffer(commandCount);
            if (_frontCount > 0)
                TryRegisterUpdate();
            if (_backCount <= 0)
                TryUnregisterLateFrame();
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

        public int FrontCount => TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer)
            ? math.min(math.max(0, _frontCount), frontBuffer.Length)
            : 0;

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            EnsureBuffers();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureBuffers();
            TryRegisterService();
            PhysicsEventBus.Register(this);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                DisposeBuffers();
                return;
            }

            PhysicsEventBus.Unregister(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
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

            PhysicsEventBus.Unregister(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
            ClearBuffers();
            DisposeBuffers();
        }

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            float impulseVolume = ClampFinite01(impulseEvent.Volume01);
            if ((impulseEvent.Flags & AcousticImpulseFlags.Critical) == 0 ||
                impulseVolume < PhysicsImpulseHapticMinimumVolume)
            {
                return;
            }

            Vector3 localDirection = impulseEvent.Direction;
            float3 direction3 = new float3(localDirection.x, localDirection.y, localDirection.z);
            if (!math.all(math.isfinite(direction3)))
                localDirection = Vector3.zero;

            IPlayerRuntimeContext player = GlobalRegistry.Player;
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

            EnqueueBackBufferCommand(
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

        private bool TryResolveFrontBuffer(out NativeArray<HapticCommand> frontBuffer)
        {
            return TryResolveBuffer(
                ref _frontBufferHandle,
                BufferID.ToolHapticFrontCommands,
                out frontBuffer);
        }

        private bool TryResolveBackBuffer(out NativeArray<HapticCommand> backBuffer)
        {
            return TryResolveBuffer(
                ref _backBufferHandle,
                BufferID.ToolHapticBackCommands,
                out backBuffer);
        }

        private static bool TryResolveBuffer(
            ref VaultBufferHandle<HapticCommand> handle,
            BufferID bufferId,
            out NativeArray<HapticCommand> buffer)
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!handle.IsCreated ||
                !vault.ResolveBuffer(ref handle) ||
                handle.Length < BufferCapacity)
            {
                handle = vault.GetBufferHandle<HapticCommand>(
                    bufferId,
                    BufferCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.ClearMemory);
            }

            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= BufferCapacity;
        }

        private void EnsureBuffers()
        {
            TryResolveBuffers(out _, out _);
        }

        private void DisposeBuffers()
        {
            _frontBufferHandle = default;
            _backBufferHandle = default;
            _frontCount = 0;
            _backCount = 0;
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

        private void EnqueueBackBuffer(float powerDelivered, float ratedPower, byte priority)
        {
            if (PowerSaveMuteActive)
                return;

            EnsureBuffers();

            float normalizedPower = math.isfinite(powerDelivered) && math.isfinite(ratedPower) && ratedPower > 0.0001f
                ? ClampFinite01(powerDelivered * math.rcp(ratedPower))
                : 0f;
            if (normalizedPower <= 0f)
                return;

            byte motorMask = RightMotorMask;
            if (!TrySelectBackBufferSlot(priority, out int slotIndex))
                return;

            if (!TryApplyHapticDebounce(ref motorMask, priority))
                return;

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
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void EnqueueBackBufferCommand(
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
                return;

            EnsureBuffers();
            byte resolvedMotorMask = (byte)(motorMask & BothMotorMask);
            if (resolvedMotorMask == 0)
                return;

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
                return;

            if (!TrySelectBackBufferSlot(priority, out int slotIndex))
                return;

            if (!TryApplyHapticDebounce(ref resolvedMotorMask, priority))
                return;

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
            TryRegisterUpdate();
            TryRegisterLateFrame();
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
            TryRegisterUpdate();
            return true;
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

using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Tool-local haptic command queue. Device dispatch remains external; this owner only builds the bounded double-buffered payload.
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
        internal const byte PriorityCritical = 3;

        private NativeArray<HapticCommand> _frontBuffer;
        private NativeArray<HapticCommand> _backBuffer;
        private int _frontCount;
        private int _backCount;
        private float _nextLeftHapticCommandTime;
        private float _nextRightHapticCommandTime;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;

        [StructLayout(LayoutKind.Sequential)]
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
                1,
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

        public void Tick(float deltaTime)
        {
            if (!_frontBuffer.IsCreated || _frontCount <= 0)
                return;

            int compactedCount = 0;
            for (int i = 0; i < _frontCount; i++)
            {
                HapticCommand command = _frontBuffer[i];
                if (command.DurationRemaining <= 0f)
                    continue;

                float safeDeltaTime = math.max(0f, deltaTime);
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

                _frontBuffer[compactedCount++] = command;
            }

            for (int i = compactedCount; i < _frontCount; i++)
            {
                _frontBuffer[i] = default;
            }

            _frontCount = compactedCount;
        }

        private static float ResolveHapticDecayFactor(float decayRate, float deltaTime)
        {
            float x = math.min(math.max(0f, decayRate) * math.max(0f, deltaTime), 3f);
            float x2 = x * x;
            return math.saturate(math.rcp(1f + x + (0.5f * x2)));
        }

        private static float ResolveHapticTriangleWave(float elapsedSeconds, float frequencyHz)
        {
            float phase = math.max(0f, elapsedSeconds) * math.max(0f, frequencyHz);
            float shiftedPhase = phase + 0.25f;
            float cycle = shiftedPhase - math.floor(shiftedPhase);
            return 1f - math.abs((cycle * 2f) - 1f);
        }

        public void LateFrameTick()
        {
            if (!_frontBuffer.IsCreated || !_backBuffer.IsCreated)
                return;

            int appendedCount = math.min(BufferCapacity - _frontCount, _backCount);
            for (int i = 0; i < appendedCount; i++)
            {
                _frontBuffer[_frontCount + i] = _backBuffer[i];
            }

            _frontCount += appendedCount;
            _backCount = 0;

            for (int i = 0; i < BufferCapacity; i++)
                _backBuffer[i] = default;
        }

        public NativeArray<HapticCommand>.ReadOnly GetFrontBuffer()
        {
            return _frontBuffer.IsCreated ? _frontBuffer.AsReadOnly() : default;
        }

        public int FrontCount => _frontCount;

        private void Awake()
        {
            EnsureBuffers();
        }

        private void OnEnable()
        {
            EnsureBuffers();
            TryRegisterService();
            PhysicsEventBus.Register(this);
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            PhysicsEventBus.Unregister(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            PhysicsEventBus.Unregister(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
            DisposeBuffers();
        }

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            if ((impulseEvent.Flags & AcousticImpulseFlags.Critical) == 0 ||
                impulseEvent.Volume01 < PhysicsImpulseHapticMinimumVolume)
            {
                return;
            }

            Vector3 localDirection = impulseEvent.Direction;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform != null)
                localDirection = playerTransform.InverseTransformDirection(impulseEvent.Direction);

            float side = math.clamp(localDirection.x, -1f, 1f);
            float intensity = math.saturate(impulseEvent.Volume01);
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
                2,
                0f);
        }

        private void EnsureBuffers()
        {
            if (!_frontBuffer.IsCreated)
            {
                _frontBuffer = new NativeArray<HapticCommand>(BufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HapticCommand>[16] - active haptic buffer consumed by input dispatch - owner: ToolHapticsRuntime

                NativeMemorySentinel.RegisterNativeArray(
                    _frontBuffer,
                    nameof(ToolHapticsRuntime),
                    nameof(_frontBuffer),
                    NativeAllocationLifetime.Scene);
            }

            if (!_backBuffer.IsCreated)
            {
                _backBuffer = new NativeArray<HapticCommand>(BufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HapticCommand>[16] - frame-local haptic write buffer merged in LateFrameTick - owner: ToolHapticsRuntime
                NativeMemorySentinel.RegisterNativeArray(
                    _backBuffer,
                    nameof(ToolHapticsRuntime),
                    nameof(_backBuffer),
                    NativeAllocationLifetime.Scene);
            }
        }

        private void DisposeBuffers()
        {
            if (_frontBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_frontBuffer);
                _frontBuffer.Dispose();
            }

            if (_backBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_backBuffer);
                _backBuffer.Dispose();
            }

            _frontBuffer = default;
            _backBuffer = default;
            _frontCount = 0;
            _backCount = 0;
        }

        private void EnqueueBackBuffer(float powerDelivered, float ratedPower, byte priority)
        {
            EnsureBuffers();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            if (_backCount >= BufferCapacity)
                return;

            float normalizedPower = ratedPower > 0.0001f
                ? math.saturate(powerDelivered / ratedPower)
                : 0f;
            if (normalizedPower <= 0f)
                return;

            _backBuffer[_backCount++] = new HapticCommand
            {
                LowFreqIntensity = 0f,
                HighFreqIntensity = normalizedPower,
                BaseLowFreqIntensity = 0f,
                BaseHighFreqIntensity = normalizedPower,
                DurationRemaining = DefaultDurationSeconds,
                DecayRate = DefaultDecayRate,
                Priority = priority,
                MotorMask = RightMotorMask,
                BlendMode = 1,
                FrequencyHz = 0f
            };
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
            EnsureBuffers();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            if (_backCount >= BufferCapacity || motorMask == 0)
                return;

            float resolvedLow = math.isfinite(lowFreqIntensity)
                ? math.saturate(lowFreqIntensity)
                : 0f;
            float resolvedHigh = math.isfinite(highFreqIntensity)
                ? math.saturate(highFreqIntensity)
                : 0f;
            float resolvedDuration = math.isfinite(durationSeconds)
                ? math.max(0f, durationSeconds)
                : 0f;
            float resolvedDecay = math.isfinite(decayRate)
                ? math.max(0f, decayRate)
                : 0f;
            if ((resolvedLow <= 0f && resolvedHigh <= 0f) || resolvedDuration <= 0f)
                return;

            float now = Time.unscaledTime;
            bool blocksLeft = (motorMask & LeftMotorMask) != 0 && now < _nextLeftHapticCommandTime;
            bool blocksRight = (motorMask & RightMotorMask) != 0 && now < _nextRightHapticCommandTime;
            if (blocksLeft && blocksRight)
                return;

            if (blocksLeft)
                motorMask = (byte)(motorMask & ~LeftMotorMask);
            if (blocksRight)
                motorMask = (byte)(motorMask & ~RightMotorMask);
            if (motorMask == 0)
                return;

            float nextCommandTime = now + HapticDebounceWindowSeconds;
            if ((motorMask & LeftMotorMask) != 0)
                _nextLeftHapticCommandTime = nextCommandTime;
            if ((motorMask & RightMotorMask) != 0)
                _nextRightHapticCommandTime = nextCommandTime;

            _backBuffer[_backCount++] = new HapticCommand
            {
                LowFreqIntensity = resolvedLow,
                HighFreqIntensity = resolvedHigh,
                BaseLowFreqIntensity = resolvedLow,
                BaseHighFreqIntensity = resolvedHigh,
                DurationRemaining = resolvedDuration,
                DecayRate = resolvedDecay,
                Priority = priority,
                MotorMask = motorMask,
                BlendMode = (byte)math.clamp((int)blendMode, 0, 2),
                FrequencyHz = math.isfinite(frequencyHz) ? math.max(0f, frequencyHz) : 0f
            };
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = GlobalRegistry.Updatables.Contains(this);
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
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

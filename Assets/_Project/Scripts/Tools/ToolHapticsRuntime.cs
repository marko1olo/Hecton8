using System.Runtime.InteropServices;
using Hecton8.Core;
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
    public sealed class ToolHapticsRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable
    {
        private const int BufferCapacity = 16;
        private const float DefaultDecayRate = 1.5f;
        private const float DefaultDurationSeconds = 0.18f;
        private const byte RightMotorMask = 0b0010;
        private const float TwoPi = 6.28318530718f;

        private static ToolHapticsRuntime _instance;

        private NativeArray<HapticCommand> _frontBuffer;
        private NativeArray<HapticCommand> _backBuffer;
        private int _frontCount;
        private int _backCount;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static void EnqueueToolFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            ToolHapticsRuntime runtime = EnsureRuntimeInstance();
            if (runtime == null)
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
            ToolHapticsRuntime runtime = EnsureRuntimeInstance();
            if (runtime == null)
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

        public static void EnqueueSinusoidalCommand(
            float lowFreqIntensity,
            float highFreqIntensity,
            float durationSeconds,
            float frequencyHz,
            byte priority,
            byte motorMask)
        {
            ToolHapticsRuntime runtime = EnsureRuntimeInstance();
            if (runtime == null)
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
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[ToolHapticsRuntime]"); // COLD ALLOC: GameObject[1] — tool-side haptic queue owner — owner: ToolHapticsRuntime
            return runtimeRoot.AddComponent<ToolHapticsRuntime>();
        }

        public static bool TryGetRuntime(out ToolHapticsRuntime runtime)
        {
            runtime = _instance;
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

                float decayFactor = math.exp(-math.max(0f, command.DecayRate) * safeDeltaTime);
                command.BaseLowFreqIntensity = math.saturate(command.BaseLowFreqIntensity * decayFactor);
                command.BaseHighFreqIntensity = math.saturate(command.BaseHighFreqIntensity * decayFactor);
                float wave = command.FrequencyHz > 0.001f
                    ? 0.5f + (0.5f * math.sin(command.ElapsedSeconds * command.FrequencyHz * TwoPi))
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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureBuffers();
        }

        private void OnEnable()
        {
            EnsureBuffers();
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            DisposeBuffers();

            if (_instance == this)
                _instance = null;
        }

        private void EnsureBuffers()
        {
            if (!_frontBuffer.IsCreated)
                _frontBuffer = new NativeArray<HapticCommand>(BufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HapticCommand>[16] — active haptic buffer consumed by input dispatch — owner: ToolHapticsRuntime

            if (!_backBuffer.IsCreated)
                _backBuffer = new NativeArray<HapticCommand>(BufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HapticCommand>[16] — frame-local haptic write buffer merged in LateFrameTick — owner: ToolHapticsRuntime
        }

        private void DisposeBuffers()
        {
            if (_frontBuffer.IsCreated)
                _frontBuffer.Dispose();

            if (_backBuffer.IsCreated)
                _backBuffer.Dispose();

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
            _registeredUpdate = true;
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
            _registeredLateFrame = true;
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

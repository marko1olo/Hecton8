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

                command.DurationRemaining = math.max(0f, command.DurationRemaining - deltaTime);
                float decayFactor = math.exp(-math.max(0f, command.DecayRate) * math.max(0f, deltaTime));
                command.LowFreqIntensity = math.saturate(command.LowFreqIntensity * decayFactor);
                command.HighFreqIntensity = math.saturate(command.HighFreqIntensity * decayFactor);
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
                DurationRemaining = DefaultDurationSeconds,
                DecayRate = DefaultDecayRate,
                Priority = priority,
                MotorMask = RightMotorMask,
                BlendMode = 1
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

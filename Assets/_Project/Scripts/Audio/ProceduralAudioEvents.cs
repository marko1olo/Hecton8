using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Zero-allocation payload for sample-accurate procedural audio triggers.
    /// </summary>
    public readonly struct AudioPingTriggerInfo
    {
        /// <summary>
        /// Creates a new audio ping trigger payload.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Audible chirp duration in seconds.</param>
        public AudioPingTriggerInfo(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = intensity;
            ChirpDurationSeconds = chirpDurationSeconds;
        }

        /// <summary>Exact output-sample frame where the ping started rendering.</summary>
        public long StartSampleFrame { get; }

        /// <summary>Audio output sample rate used to resolve the frame timestamp.</summary>
        public int SampleRate { get; }

        /// <summary>Exact start time in seconds derived from the sample-frame clock.</summary>
        public double StartTimeSeconds => StartSampleFrame / (double)SampleRate;

        /// <summary>Normalized ping intensity in the 0..1 range.</summary>
        public float Intensity { get; }

        /// <summary>Primary chirp duration in seconds.</summary>
        public float ChirpDurationSeconds { get; }
    }

    /// <summary>
    /// Zero-allocation payload for habitat structural stress groan synthesis.
    /// </summary>
    public readonly struct StructuralStressAudioInfo
    {
        /// <summary>
        /// Creates a structural stress audio payload.
        /// </summary>
        public StructuralStressAudioInfo(Vector3 worldPosition, float stress01, float pitchScale)
        {
            WorldPosition = worldPosition;
            Stress01 = Mathf.Clamp01(stress01);
            PitchScale = Mathf.Max(0.1f, pitchScale);
        }

        /// <summary>World-space origin for spatial routing.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Normalized edge stress in the [0..1] range.</summary>
        public float Stress01 { get; }

        /// <summary>Pitch multiplier consumed by the renderer.</summary>
        public float PitchScale { get; }
    }

    /// <summary>
    /// Listener contract for deferred procedural audio notifications.
    /// </summary>
    public interface IProceduralAudioEventListener
    {
        /// <summary>Called after the procedural renderer starts a sonar ping.</summary>
        /// <param name="info">Sample-accurate ping payload.</param>
        void OnAudioPingTriggered(in AudioPingTriggerInfo info);

        /// <summary>Called when a structure emits audible stress.</summary>
        /// <param name="info">Structural stress audio payload.</param>
        void OnStructuralStressTriggered(in StructuralStressAudioInfo info);
    }

    /// <summary>
    /// Queue-backed main-thread bridge for sample-accurate procedural audio triggers.
    /// </summary>
    public static class ProceduralAudioEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingAudioPingCapacity = 8;
        private const int PendingStructuralStressCapacity = 8;

        // COLD ALLOC: RegistryBucket<IProceduralAudioEventListener>[8] - deferred procedural-audio listeners - owner: ProceduralAudioEvents
        private static readonly RegistryBucket<IProceduralAudioEventListener> _listeners = new RegistryBucket<IProceduralAudioEventListener>(ListenerCapacity);
        private static NativeQueue<AudioPingTriggerInfo> _pendingAudioPings;
        private static NativeQueue<AudioPingTriggerInfo> _nextFrameAudioPings;
        private static NativeQueue<StructuralStressAudioInfo> _pendingStructuralStress;
        private static NativeQueue<StructuralStressAudioInfo> _nextFrameStructuralStress;
        private static int _pendingAudioPingCount;
        private static int _nextFrameAudioPingCount;
        private static int _pendingStructuralStressCount;
        private static int _nextFrameStructuralStressCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of procedural audio events waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                return _pendingAudioPingCount
                    + _nextFrameAudioPingCount
                    + _pendingStructuralStressCount
                    + _nextFrameStructuralStressCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingAudioPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingAudioPings));
                _pendingAudioPings.Dispose();
                _pendingAudioPings = default;
            }

            if (_nextFrameAudioPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_nextFrameAudioPings));
                _nextFrameAudioPings.Dispose();
                _nextFrameAudioPings = default;
            }

            if (_pendingStructuralStress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingStructuralStress));
                _pendingStructuralStress.Dispose();
                _pendingStructuralStress = default;
            }

            if (_nextFrameStructuralStress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_nextFrameStructuralStress));
                _nextFrameStructuralStress.Dispose();
                _nextFrameStructuralStress = default;
            }

            _pendingAudioPingCount = 0;
            _nextFrameAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
            _nextFrameStructuralStressCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        /// <summary>
        /// Registers one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IProceduralAudioEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IProceduralAudioEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued procedural audio notifications on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
                {
                    completed = DrainWithoutDispatch();
                }
                else
                {
                    completed = FlushAudioPings();
                    if (completed)
                        completed = FlushStructuralStress();
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        /// <summary>
        /// Queues the sample-accurate sonar-ping notification on the main thread.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Primary chirp duration in seconds.</param>
        public static void RaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            EnsureInitialized();
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
                return;

            AudioPingTriggerInfo info = new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds);
            if (_isDispatching)
            {
                _nextFrameAudioPings.Enqueue(info);
                _nextFrameAudioPingCount++;
            }
            else
            {
                _pendingAudioPings.Enqueue(info);
                _pendingAudioPingCount++;
            }
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        public static void RaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            EnsureInitialized();
            if (_pendingStructuralStressCount + _nextFrameStructuralStressCount >= PendingStructuralStressCapacity)
                return;

            StructuralStressAudioInfo info = new StructuralStressAudioInfo(worldPosition, stress01, pitchScale);
            if (_isDispatching)
            {
                _nextFrameStructuralStress.Enqueue(info);
                _nextFrameStructuralStressCount++;
            }
            else
            {
                _pendingStructuralStress.Enqueue(info);
                _pendingStructuralStressCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingAudioPings.IsCreated)
            {
                _pendingAudioPings = new NativeQueue<AudioPingTriggerInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioPingTriggerInfo>[8] - deferred procedural ping lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingAudioPings,
                    PendingAudioPingCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_pendingAudioPings),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameAudioPings.IsCreated)
            {
                _nextFrameAudioPings = new NativeQueue<AudioPingTriggerInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioPingTriggerInfo>[8] - next-frame procedural ping lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameAudioPings,
                    PendingAudioPingCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_nextFrameAudioPings),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingStructuralStress.IsCreated)
            {
                _pendingStructuralStress = new NativeQueue<StructuralStressAudioInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StructuralStressAudioInfo>[8] - deferred structural stress audio lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingStructuralStress,
                    PendingStructuralStressCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_pendingStructuralStress),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameStructuralStress.IsCreated)
            {
                _nextFrameStructuralStress = new NativeQueue<StructuralStressAudioInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StructuralStressAudioInfo>[8] - next-frame structural stress audio lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameStructuralStress,
                    PendingStructuralStressCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_nextFrameStructuralStress),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool FlushAudioPings()
        {
            if (!_pendingAudioPings.IsCreated)
                return true;

            int scanBudget = _pendingAudioPingCount > 0 ? _pendingAudioPingCount : PendingAudioPingCapacity;
            while (scanBudget > 0 && !_pendingAudioPings.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingAudioPings.TryDequeue(out AudioPingTriggerInfo info))
                    return true;

                _pendingAudioPingCount--;
                scanBudget--;
                IProceduralAudioEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnAudioPingTriggered(in info);
                }
            }

            if (_pendingAudioPings.IsEmpty())
                _pendingAudioPingCount = 0;

            return true;
        }

        private static bool FlushStructuralStress()
        {
            if (!_pendingStructuralStress.IsCreated)
                return true;

            int scanBudget = _pendingStructuralStressCount > 0 ? _pendingStructuralStressCount : PendingStructuralStressCapacity;
            while (scanBudget > 0 && !_pendingStructuralStress.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingStructuralStress.TryDequeue(out StructuralStressAudioInfo info))
                    return true;

                _pendingStructuralStressCount--;
                scanBudget--;
                IProceduralAudioEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnStructuralStressTriggered(in info);
                }
            }

            if (_pendingStructuralStress.IsEmpty())
                _pendingStructuralStressCount = 0;

            return true;
        }

        private static bool DrainWithoutDispatch()
        {
            if (_pendingAudioPings.IsCreated)
            {
                int scanBudget = _pendingAudioPingCount > 0 ? _pendingAudioPingCount : PendingAudioPingCapacity;
                while (scanBudget > 0 && !_pendingAudioPings.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingAudioPings.TryDequeue(out _))
                        return true;

                    _pendingAudioPingCount--;
                    scanBudget--;
                }

                if (_pendingAudioPings.IsEmpty())
                    _pendingAudioPingCount = 0;
            }

            if (_pendingStructuralStress.IsCreated)
            {
                int scanBudget = _pendingStructuralStressCount > 0 ? _pendingStructuralStressCount : PendingStructuralStressCapacity;
                while (scanBudget > 0 && !_pendingStructuralStress.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingStructuralStress.TryDequeue(out _))
                        return true;

                    _pendingStructuralStressCount--;
                    scanBudget--;
                }

                if (_pendingStructuralStress.IsEmpty())
                    _pendingStructuralStressCount = 0;
            }

            return true;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingAudioPings.IsCreated && !_pendingAudioPings.IsEmpty())
                || (_pendingStructuralStress.IsCreated && !_pendingStructuralStress.IsEmpty());
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameAudioPings.IsCreated)
            {
                while (_nextFrameAudioPingCount > 0 && _nextFrameAudioPings.TryDequeue(out AudioPingTriggerInfo info))
                {
                    _nextFrameAudioPingCount--;
                    _pendingAudioPings.Enqueue(info);
                    _pendingAudioPingCount++;
                }
            }

            if (_nextFrameStructuralStress.IsCreated)
            {
                while (_nextFrameStructuralStressCount > 0 && _nextFrameStructuralStress.TryDequeue(out StructuralStressAudioInfo info))
                {
                    _nextFrameStructuralStressCount--;
                    _pendingStructuralStress.Enqueue(info);
                    _pendingStructuralStressCount++;
                }
            }
        }
    }
}

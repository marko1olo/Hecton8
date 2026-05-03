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
        private static NativeQueue<StructuralStressAudioInfo> _pendingStructuralStress;
        private static int _pendingAudioPingCount;
        private static int _pendingStructuralStressCount;

        /// <summary>
        /// Number of procedural audio events waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                return _pendingAudioPingCount + _pendingStructuralStressCount;
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

            if (_pendingStructuralStress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingStructuralStress));
                _pendingStructuralStress.Dispose();
                _pendingStructuralStress = default;
            }

            _pendingAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
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
            if (_listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            if (!FlushAudioPings())
                return;
            FlushStructuralStress();
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
            if (_pendingAudioPingCount >= PendingAudioPingCapacity)
                return;

            _pendingAudioPings.Enqueue(new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds));
            _pendingAudioPingCount++;
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        public static void RaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            EnsureInitialized();
            if (_pendingStructuralStressCount >= PendingStructuralStressCapacity)
                return;

            _pendingStructuralStress.Enqueue(new StructuralStressAudioInfo(worldPosition, stress01, pitchScale));
            _pendingStructuralStressCount++;
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
                    rawArray[i].OnAudioPingTriggered(in info);
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
                    rawArray[i].OnStructuralStressTriggered(in info);
            }

            if (_pendingStructuralStress.IsEmpty())
                _pendingStructuralStressCount = 0;

            return true;
        }

        private static void DrainWithoutDispatch()
        {
            if (_pendingAudioPings.IsCreated)
            {
                int scanBudget = _pendingAudioPingCount > 0 ? _pendingAudioPingCount : PendingAudioPingCapacity;
                while (scanBudget > 0 && !_pendingAudioPings.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingAudioPings.TryDequeue(out _))
                        return;

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
                        return;

                    if (!_pendingStructuralStress.TryDequeue(out _))
                        return;

                    _pendingStructuralStressCount--;
                    scanBudget--;
                }

                if (_pendingStructuralStress.IsEmpty())
                    _pendingStructuralStressCount = 0;
            }
        }
    }
}

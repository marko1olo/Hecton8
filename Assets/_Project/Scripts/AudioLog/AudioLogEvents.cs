using System.Diagnostics;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Narrative
{
    public enum AudioLogEventType : byte
    {
        Discovered = 0,
        PlaybackStarted = 1,
        PlaybackStopped = 2,
        PlaybackCompleted = 3
    }

    public struct AudioLogEventPayload
    {
        public AudioLogEventType Type;
        public ulong TimestampTicks;
        public uint LogHash;
        public float DurationSeconds;
    }

    public interface IAudioLogEventListener
    {
        void OnAudioLogEvent(in AudioLogEventPayload payload);
    }

    public static class AudioLogEvents
    {
        // COLD ALLOC: RegistryBucket<IAudioLogEventListener>[16] - audio log event listener registry drained on dispatcher LateUpdate - owner: AudioLogEvents
        private static readonly RegistryBucket<IAudioLogEventListener> _listeners = new RegistryBucket<IAudioLogEventListener>(16);
        private static NativeQueue<AudioLogEventPayload> _pendingEvents;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
        }

        public static void Register(IAudioLogEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IAudioLogEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out AudioLogEventPayload payload))
                    return;

                IAudioLogEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnAudioLogEvent(in payload);
            }
        }

        public static void RaiseLogDiscovered(uint logHash)
        {
            Enqueue(AudioLogEventType.Discovered, logHash, 0f);
        }

        public static void RaisePlaybackStarted(uint logHash, float durationSeconds)
        {
            Enqueue(AudioLogEventType.PlaybackStarted, logHash, durationSeconds);
        }

        public static void RaisePlaybackStopped(uint logHash)
        {
            Enqueue(AudioLogEventType.PlaybackStopped, logHash, 0f);
        }

        public static void RaisePlaybackCompleted(uint logHash)
        {
            Enqueue(AudioLogEventType.PlaybackCompleted, logHash, 0f);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AudioLogEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] - deferred audio-log event lane flushed by SystemDispatcher LateUpdate - owner: AudioLogEvents
            }
        }

        private static void Enqueue(AudioLogEventType type, uint logHash, float durationSeconds)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(new AudioLogEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                LogHash = logHash,
                DurationSeconds = durationSeconds
            });
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}

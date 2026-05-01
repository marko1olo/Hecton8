using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Environment
{
    public enum WeatherEventType : byte
    {
        SnapshotUpdated = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WeatherEventPayload
    {
        public float3 GlobalCurrentVector;
        public float3 GlobalWindVector;
        public CurrentMeta CurrentMeta;
        public uint StateMask;
        public float WeatherIntensity;
        public ushort EventType;
        public ushort Reserved;
    }

    public interface IWeatherEventListener
    {
        void OnWeatherEvent(in WeatherEventPayload payload);
    }

    public static class WeatherEvents
    {
        private const int ListenerCapacity = 32;

        // COLD ALLOC: RegistryBucket<IWeatherEventListener>[32] - deferred weather event listeners - owner: WeatherEvents
        private static readonly RegistryBucket<IWeatherEventListener> _listeners = new RegistryBucket<IWeatherEventListener>(ListenerCapacity);
        private static NativeQueue<WeatherEventPayload> _pendingEvents;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEvents.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
        }

        public static void Register(IWeatherEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IWeatherEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
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

                if (!_pendingEvents.TryDequeue(out WeatherEventPayload payload))
                    return;

                IWeatherEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnWeatherEvent(in payload);
            }
        }

        public static void RaiseSnapshotUpdated(in WeatherRuntimeSnapshot snapshot)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(new WeatherEventPayload
            {
                GlobalCurrentVector = snapshot.GlobalCurrentVector,
                GlobalWindVector = snapshot.GlobalWindVector,
                CurrentMeta = snapshot.CurrentMeta,
                StateMask = (uint)snapshot.StateMask,
                WeatherIntensity = snapshot.WeatherIntensity,
                EventType = (ushort)WeatherEventType.SnapshotUpdated,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<WeatherEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<WeatherEventPayload>[32] - deferred weather event lane - owner: WeatherEvents
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

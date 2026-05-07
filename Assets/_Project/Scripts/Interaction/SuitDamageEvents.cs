namespace Hecton8.Interaction
{
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Physical suit-contact damage emitted by somatic hand collision.
    /// </summary>
    public readonly struct SuitDamageEvent
    {
        public SuitDamageEvent(
            PhysicalHandSide handSide,
            AbsoluteUniversePosition contactAup,
            float3 contactNormal,
            float magnitude01,
            int sourceColliderInstanceId,
            uint frameIndex)
        {
            HandSide = handSide;
            ContactAup = contactAup;
            ContactNormal = contactNormal;
            Magnitude01 = math.saturate(magnitude01);
            SourceColliderInstanceId = sourceColliderInstanceId;
            FrameIndex = frameIndex;
        }

        public PhysicalHandSide HandSide { get; }
        public AbsoluteUniversePosition ContactAup { get; }
        public float3 ContactNormal { get; }
        public float Magnitude01 { get; }
        public int SourceColliderInstanceId { get; }
        public uint FrameIndex { get; }
    }

    public interface ISuitDamageEventListener
    {
        void OnSuitDamage(in SuitDamageEvent damageEvent);
    }

    /// <summary>
    /// Fixed-capacity suit damage event fan-out. Listener registration is cold path only.
    /// </summary>
    public static class SuitDamageEvents
    {
        private const int ListenerCapacity = 16;
        private static readonly ISuitDamageEventListener[] _listeners = new ISuitDamageEventListener[ListenerCapacity]; // COLD ALLOC: ISuitDamageEventListener[16] - suit damage listeners - owner: SuitDamageEvents
        private static int _listenerCount;

        public static void Register(ISuitDamageEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i], listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SuitDamageEvents] Listener capacity exceeded; registration rejected.");
#endif
                return;
            }

            _listeners[_listenerCount++] = listener;
        }

        public static void Unregister(ISuitDamageEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i], listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex] = null;
                _listenerCount = lastIndex;
                return;
            }
        }

        public static void Publish(in SuitDamageEvent damageEvent)
        {
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i]?.OnSuitDamage(in damageEvent);
        }
    }
}

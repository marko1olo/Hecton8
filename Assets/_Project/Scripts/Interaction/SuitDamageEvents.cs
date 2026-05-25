namespace Hecton8.Interaction
{
    using System.Runtime.InteropServices;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    internal static class SuitDamageEventLayout
    {
        internal const int EventStrideBytes = 80;
    }

    /// <summary>
    /// Physical suit-contact damage emitted by somatic hand collision.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = SuitDamageEventLayout.EventStrideBytes)]
    public readonly struct SuitDamageEvent
    {
        [FieldOffset(0)]
        public readonly AbsoluteUniversePosition ContactAup;
        [FieldOffset(48)]
        public readonly float3 ContactNormal;
        [FieldOffset(60)]
        public readonly float Magnitude01;
        [FieldOffset(64)]
        public readonly int SourceColliderInstanceId;
        [FieldOffset(68)]
        public readonly uint FrameIndex;
        [FieldOffset(72)]
        public readonly PhysicalHandSide HandSide;
        [FieldOffset(73)]
        private readonly byte _pad0;
        [FieldOffset(74)]
        private readonly ushort _pad1;
        [FieldOffset(76)]
        private readonly uint _pad2;

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
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
        }
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
        private struct ListenerSlot
        {
            public ISuitDamageEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - suit damage listeners - owner: SuitDamageEvents
        private static int _listenerCount;

        public static void Register(ISuitDamageEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SuitDamageEvents] Listener capacity exceeded; registration rejected.");
#endif
                return;
            }

            _listeners[_listenerCount++].Listener = listener;
        }

        public static void Unregister(ISuitDamageEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return;
            }
        }

        public static void Publish(in SuitDamageEvent damageEvent)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                ISuitDamageEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    listener.OnSuitDamage(in damageEvent);
            }
        }
    }
}

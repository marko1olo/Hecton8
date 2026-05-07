namespace Hecton8.Interaction
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Shared collider-to-hand-receiver table for physical cockpit controls.
    /// </summary>
    internal static class PhysicalHandReceiverRegistry
    {
        private const int InitialCapacity = 128;

        private static readonly Dictionary<Collider, IPhysicalPanelButtonReceiver> _receiversByCollider =
            new Dictionary<Collider, IPhysicalPanelButtonReceiver>(InitialCapacity); // COLD ALLOC: Dictionary<Collider,IPhysicalPanelButtonReceiver>[128] - physical hand receiver registry - owner: PhysicalHandReceiverRegistry

        public static void Register(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null || receiver == null)
                return;

            _receiversByCollider[collider] = receiver;
        }

        public static void Unregister(Collider collider, IPhysicalPanelButtonReceiver receiver)
        {
            if (collider == null)
                return;

            if (_receiversByCollider.TryGetValue(collider, out IPhysicalPanelButtonReceiver registered) &&
                ReferenceEquals(registered, receiver))
            {
                _receiversByCollider.Remove(collider);
            }
        }

        public static bool TryResolve(Collider collider, out IPhysicalPanelButtonReceiver receiver)
        {
            receiver = null;
            return collider != null &&
                   _receiversByCollider.TryGetValue(collider, out receiver) &&
                   receiver != null;
        }
    }
}

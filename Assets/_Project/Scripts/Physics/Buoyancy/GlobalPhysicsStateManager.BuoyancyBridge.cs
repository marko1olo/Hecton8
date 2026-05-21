using UnityEngine;

namespace Hecton8.Physics
{
    public sealed partial class GlobalPhysicsStateManager
    {
        internal static bool TryFindTrackedBodyByFoldedEntityHash(uint entityHashID, out Rigidbody body)
        {
            int bodyIndex;
            return TryFindTrackedBodyByFoldedEntityHash(entityHashID, out body, out bodyIndex);
        }

        internal static bool TryFindTrackedBodyByFoldedEntityHash(uint entityHashID, out Rigidbody body, out int bodyIndex)
        {
            body = null;
            bodyIndex = -1;
            if (entityHashID == 0u || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return false;

            return manager.TryFindTrackedBodyByFoldedEntityHashInternal(entityHashID, out body, out bodyIndex);
        }

        internal static bool TryGetBuoyancyBodyResolver(out GlobalPhysicsStateManager manager)
        {
            return TryGetRuntimeManager(out manager);
        }

        internal static bool TryFindTrackedBodyByFoldedEntityHash(
            GlobalPhysicsStateManager manager,
            uint entityHashID,
            out Rigidbody body,
            out int bodyIndex)
        {
            body = null;
            bodyIndex = -1;
            if (entityHashID == 0u || manager == null)
                return false;

            return manager.TryFindTrackedBodyByFoldedEntityHashInternal(entityHashID, out body, out bodyIndex);
        }

        internal static bool TryResolveTrackedBodyByIndex(int bodyIndex, uint entityHashID, out Rigidbody body)
        {
            body = null;
            if (entityHashID == 0u || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return false;

            return manager.TryResolveTrackedBodyByIndexInternal(bodyIndex, entityHashID, out body);
        }

        internal static bool TryResolveTrackedBodyByIndex(
            GlobalPhysicsStateManager manager,
            int bodyIndex,
            uint entityHashID,
            out Rigidbody body)
        {
            body = null;
            if (entityHashID == 0u || manager == null)
                return false;

            return manager.TryResolveTrackedBodyByIndexInternal(bodyIndex, entityHashID, out body);
        }

        private bool TryFindTrackedBodyByFoldedEntityHashInternal(uint entityHashID, out Rigidbody body, out int bodyIndex)
        {
            body = null;
            bodyIndex = -1;
            ulong exactKey = entityHashID;
            if (_trackedBodyIndexByEntityId.TryGetValue(exactKey, out int exactIndex) &&
                (uint)exactIndex < (uint)_trackedBodyCount)
            {
                Rigidbody candidate = _trackedBodies[exactIndex];
                if (candidate != null)
                {
                    body = candidate;
                    bodyIndex = exactIndex;
                    return true;
                }
            }

            for (int i = 0; i < _trackedBodyCount; i++)
            {
                Rigidbody candidate = _trackedBodies[i];
                if (candidate == null)
                    continue;

                uint folded = unchecked((uint)_bodyStates[i].EntityId);
                if (folded != entityHashID)
                    continue;

                body = candidate;
                bodyIndex = i;
                return true;
            }

            return false;
        }

        private bool TryResolveTrackedBodyByIndexInternal(int bodyIndex, uint entityHashID, out Rigidbody body)
        {
            body = null;
            if ((uint)bodyIndex >= (uint)_trackedBodyCount)
                return false;

            Rigidbody candidate = _trackedBodies[bodyIndex];
            if (candidate == null)
                return false;

            uint folded = unchecked((uint)_bodyStates[bodyIndex].EntityId);
            if (folded != entityHashID)
                return false;

            body = candidate;
            return true;
        }
    }
}

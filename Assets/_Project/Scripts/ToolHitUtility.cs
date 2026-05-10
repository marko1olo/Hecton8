using Hecton8.Bootstrap;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ToolHitUtility
    {
        private static HUDNotification s_notification;
        private static Transform s_playerTransform;
        private static PlayerToolManager s_playerToolManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_notification = null;
            s_playerTransform = null;
            s_playerToolManager = null;
        }

        public static bool ApplyDamage(
            Collider hitCollider,
            float damage,
            Vector3 hitPoint,
            Vector3 forceDirection,
            float impulse)
        {
            if (hitCollider == null || damage <= 0f)
                return false;

            bool applied = false;

            if (hitCollider.TryGetComponent(out ICuttable cuttable))
            {
                cuttable.ApplyCutDamage(damage, hitPoint);
                applied = true;
            }
            else if (hitCollider.TryGetComponent(out FaunaBrain ai))
            {
                ai.TakeDamage(damage);
                if (ai.TryGetComponent(out Hecton8.AI.CreatureDamageManager damageManager))
                    damageManager.RegisterWoundWS(hitPoint, damage);
                applied = true;
            }
            else if (hitCollider.TryGetComponent(out HectonSurvivalSystem survival))
            {
                survival.TakeDamage(damage);
                applied = true;
            }
            else
            {
                FaunaBrain aiParent = hitCollider.GetComponentInParent<FaunaBrain>();
                if (aiParent != null)
                {
                    aiParent.TakeDamage(damage);
                    if (aiParent.TryGetComponent(out Hecton8.AI.CreatureDamageManager damageManager))
                        damageManager.RegisterWoundWS(hitPoint, damage);
                    applied = true;
                }
            }

            if (impulse > 0f)
                ApplyImpulse(hitCollider, forceDirection, impulse);

            return applied;
        }

        public static bool TryCollectItem(Collider hitCollider, Transform interactor)
        {
            return TryCollectItem(hitCollider, interactor, out _);
        }

        public static bool TryPeekCollectible(Collider hitCollider, out ItemData itemData, out int quantity)
        {
            itemData = null;
            quantity = 0;

            if (hitCollider == null)
                return false;

            HectonItem item = hitCollider.GetComponent<HectonItem>();
            if (item == null)
                item = hitCollider.GetComponentInParent<HectonItem>();

            if (item != null)
            {
                itemData = item.Data;
                quantity = 1;
                return itemData != null;
            }

            PickupItem pickup = hitCollider.GetComponent<PickupItem>();
            if (pickup == null)
                pickup = hitCollider.GetComponentInParent<PickupItem>();

            if (pickup == null)
                return false;

            itemData = pickup.ItemData;
            quantity = pickup.Quantity;
            return itemData != null;
        }

        public static bool TryCollectItem(Collider hitCollider, Transform interactor, out ItemData collectedItem)
        {
            collectedItem = null;

            if (hitCollider == null || interactor == null)
                return false;

            HectonItem item = hitCollider.GetComponent<HectonItem>();
            if (item == null)
                item = hitCollider.GetComponentInParent<HectonItem>();

            if (item != null)
            {
                collectedItem = item.Data;
                item.Interact(interactor);
                return true;
            }

            PickupItem pickup = hitCollider.GetComponent<PickupItem>();
            if (pickup == null)
                pickup = hitCollider.GetComponentInParent<PickupItem>();

            if (pickup == null)
                return false;

            collectedItem = pickup.ItemData;
            pickup.Interact(interactor);
            return true;
        }

        public static bool TryGetRigidbody(Collider hitCollider, out Rigidbody body)
        {
            body = null;
            if (hitCollider == null)
                return false;

            body = hitCollider.attachedRigidbody;
            if (body != null)
                return true;

            body = hitCollider.GetComponentInParent<Rigidbody>();
            return body != null;
        }

        public static void ApplyImpulse(Collider hitCollider, Vector3 direction, float impulse)
        {
            if (impulse <= 0f || !TryGetRigidbody(hitCollider, out Rigidbody body))
                return;

            Vector3 normalizedDirection = NormalizeOrForward(direction);
            PhysicsForceRouter.QueueForce(body, normalizedDirection * impulse, ForceMode.Impulse);
            TryApplyRelativeCarrierImpulse(normalizedDirection, impulse);
        }

        internal static bool TryApplyRelativeCarrierImpulse(Vector3 direction, float impulse)
        {
            if (impulse <= 0f || direction.sqrMagnitude < 0.0001f)
                return false;

            if (!TryResolvePlayerToolManager(out PlayerToolManager toolManager) ||
                !toolManager.TryResolveInteriorCarrierBody(out Rigidbody carrierBody) ||
                carrierBody == null)
            {
                return false;
            }

            Vector3 normalizedDirection = NormalizeOrForward(direction);
            PhysicsForceRouter.QueueForce(carrierBody, -normalizedDirection * impulse, ForceMode.Impulse);
            return true;
        }

        private static Vector3 NormalizeOrForward(Vector3 direction)
        {
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                return direction;

            return direction * math.rsqrt(sqrMagnitude);
        }

        private static bool TryResolvePlayerToolManager(out PlayerToolManager toolManager)
        {
            if (s_playerToolManager != null)
            {
                toolManager = s_playerToolManager;
                return true;
            }

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
            {
                s_playerTransform = null;
                toolManager = null;
                return false;
            }

            if (!ReferenceEquals(s_playerTransform, playerTransform))
            {
                s_playerTransform = playerTransform;
                s_playerToolManager = null;
            }

            if (s_playerToolManager == null)
                playerTransform.TryGetComponent(out s_playerToolManager);

            toolManager = s_playerToolManager;
            return toolManager != null;
        }

        public static void ShowInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowInfo(message);
            else
                LogToolInfo(message);
        }

        public static void ShowInfo(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowInfo(in messageBuffer);
            else
                LogToolInfo(in messageBuffer);
        }

        public static void ShowWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowWarning(message);
            else
                LogToolWarning(message);
        }

        public static void ShowWarning(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowWarning(in messageBuffer);
            else
                LogToolWarning(in messageBuffer);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogToolInfo(string message)
        {
            Debug.Log($"[ToolInfo] {message}");
        }

        private static void LogToolInfo(in FixedCharBuffer messageBuffer)
        {
            Debug.Log($"[ToolInfo] {messageBuffer.ToString()}");
        }

        private static void LogToolWarning(string message)
        {
            Debug.LogWarning($"[ToolWarning] {message}");
        }

        private static void LogToolWarning(in FixedCharBuffer messageBuffer)
        {
            Debug.LogWarning($"[ToolWarning] {messageBuffer.ToString()}");
        }
#else
        private static void LogToolInfo(string message) { }
        private static void LogToolInfo(in FixedCharBuffer messageBuffer) { }
        private static void LogToolWarning(string message) { }
        private static void LogToolWarning(in FixedCharBuffer messageBuffer) { }
#endif
    }
}


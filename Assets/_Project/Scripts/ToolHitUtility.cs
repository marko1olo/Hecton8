using Hecton8.AI;
using Hecton8.Items;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ToolHitUtility
    {
        private static HUDNotification s_notification;

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
            else if (hitCollider.TryGetComponent(out HectonBaseAI ai))
            {
                ai.TakeDamage(damage);
                applied = true;
            }
            else if (hitCollider.TryGetComponent(out HectonSurvivalSystem survival))
            {
                survival.TakeDamage(damage);
                applied = true;
            }
            else
            {
                HectonBaseAI aiParent = hitCollider.GetComponentInParent<HectonBaseAI>();
                if (aiParent != null)
                {
                    aiParent.TakeDamage(damage);
                    applied = true;
                }
            }

            if (impulse > 0f)
                ApplyImpulse(hitCollider, forceDirection, impulse);

            return applied;
        }

        public static bool TryCollectItem(Collider hitCollider, Transform interactor)
        {
            if (hitCollider == null || interactor == null)
                return false;

            HectonItem item = hitCollider.GetComponent<HectonItem>();
            if (item == null)
                item = hitCollider.GetComponentInParent<HectonItem>();

            if (item == null)
                return false;

            item.Interact(interactor);
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

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            body.AddForce(direction.normalized * impulse, ForceMode.Impulse);
        }

        public static void ShowInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                s_notification = Object.FindFirstObjectByType<HUDNotification>();

            if (s_notification != null)
                s_notification.ShowInfo(message);
            else
                Debug.Log($"[ToolInfo] {message}");
        }

        public static void ShowWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                s_notification = Object.FindFirstObjectByType<HUDNotification>();

            if (s_notification != null)
                s_notification.ShowWarning(message);
            else
                Debug.LogWarning($"[ToolWarning] {message}");
        }
    }
}

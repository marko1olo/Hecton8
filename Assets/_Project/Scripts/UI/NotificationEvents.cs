// ============================================================================
// HECTON-8 — NotificationEvents.cs
// Zero-GC Event bus for HUD notifications.
// ============================================================================

using System;

namespace Hecton8.UI
{
    public static class NotificationEvents
    {
        /// <summary>
        /// Global event for pushing HUD notifications.
        /// arg1 = text message, arg2 = severity (0=Info, 1=Warning, 2=Critical)
        /// </summary>
        public static event Action<string, int> OnPushNotification;

        public static void PushInfo(string message) => OnPushNotification?.Invoke(message, 0);
        public static void PushWarning(string message) => OnPushNotification?.Invoke(message, 1);
        public static void PushCritical(string message) => OnPushNotification?.Invoke(message, 2);
    }
}

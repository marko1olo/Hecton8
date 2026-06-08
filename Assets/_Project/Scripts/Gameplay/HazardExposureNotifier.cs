// ============================================================================
// HECTON-8 — HazardExposureNotifier.cs
// Zero-GC bridge from hazard enter/exit to HUD notification events.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Tracks active local hazard exposure counts and emits one HUD notification
    /// on first enter and final exit for each hazard type.
    /// </summary>
    internal static class HazardExposureNotifier
    {
        private const string MsgRadiationEnter = "RADIATION EXPOSURE DETECTED";
        private const string MsgRadiationExit = "RADIATION EXPOSURE CLEARED";
        private const string MsgHeatEnter = "THERMAL HAZARD DETECTED";
        private const string MsgHeatExit = "THERMAL HAZARD CLEARED";
        private const string MsgToxicityEnter = "TOXIC INCURSION";
        private const string MsgToxicityExit = "TOXIC INCURSION CLEARED";
        private const string MsgBiohazardEnter = "BIOHAZARD EXPOSURE DETECTED";
        private const string MsgBiohazardExit = "BIOHAZARD EXPOSURE CLEARED";
        private const int MaxActiveExposureCount = 32767;
        private static readonly uint s_notificationMissWarningHash = unchecked((uint)LocHash.Compute("HazardExposureNotifier.NotificationMiss"));
        private static readonly uint s_notificationContextHash = unchecked((uint)LocHash.Compute("HazardExposureNotifier.Notification"));
        private static readonly uint s_enterContextHash = unchecked((uint)LocHash.Compute("HazardExposureNotifier.Enter"));
        private static readonly uint s_exitContextHash = unchecked((uint)LocHash.Compute("HazardExposureNotifier.Exit"));

        // COLD ALLOC: one exposure counter slot per HazardType enum value.
        private static readonly int[] s_activeExposureCounts = new int[4];
        private static int s_notificationMissCount;
        public static int NotificationMissCount => s_notificationMissCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(s_activeExposureCounts, 0, s_activeExposureCounts.Length);
            s_notificationMissCount = 0;
        }

        /// <summary>Marks the player as exposed to a hazard type.</summary>
        /// <param name="type">The hazard type.</param>
        public static void Enter(HazardType type)
        {
            int index = (int)type;
            if ((uint)index >= (uint)s_activeExposureCounts.Length)
                return;

            int previousCount = s_activeExposureCounts[index];
            if (previousCount >= MaxActiveExposureCount)
                return;

            s_activeExposureCounts[index] = previousCount + 1;

            if (previousCount == 0)
                TryPushExposureNotification(GetEnterMessage(type), type, warning: true);
        }

        /// <summary>Marks the player as no longer exposed to a hazard type.</summary>
        /// <param name="type">The hazard type.</param>
        public static void Exit(HazardType type)
        {
            int index = (int)type;
            if ((uint)index >= (uint)s_activeExposureCounts.Length)
                return;

            int previousCount = s_activeExposureCounts[index];
            if (previousCount <= 0)
            {
                s_activeExposureCounts[index] = 0;
                return;
            }

            int nextCount = previousCount - 1;
            s_activeExposureCounts[index] = nextCount;

            if (nextCount == 0)
                TryPushExposureNotification(GetExitMessage(type), type, warning: false);
        }

        private static void TryPushExposureNotification(ReadOnlySpan<char> message, HazardType type, bool warning)
        {
            bool pushed = warning
                ? NotificationEvents.TryPushWarning(message)
                : NotificationEvents.TryPushInfo(message);
            if (pushed)
                return;

            ReportExposureNotificationMiss(type, warning);
        }

        private static void ReportExposureNotificationMiss(HazardType type, bool warning)
        {
            s_notificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                s_notificationMissWarningHash,
                ResolveExposureNotificationContext(type, warning),
                Mathf.Max(1, s_notificationMissCount));
        }

        private static uint ResolveExposureNotificationContext(HazardType type, bool warning)
        {
            uint hazardHash = unchecked((uint)(int)type);
            return s_notificationContextHash ^ (warning ? s_enterContextHash : s_exitContextHash) ^ hazardHash;
        }

        private static ReadOnlySpan<char> GetEnterMessage(HazardType type)
        {
            switch (type)
            {
                case HazardType.Radiation:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_RADIATION_ENTER, MsgRadiationEnter);
                case HazardType.Heat:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_HEAT_ENTER, MsgHeatEnter);
                case HazardType.Toxicity:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_TOXICITY_ENTER, MsgToxicityEnter);
                case HazardType.Biohazard:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_BIOHAZARD_ENTER, MsgBiohazardEnter);
                default:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_BIOHAZARD_ENTER, MsgBiohazardEnter);
            }
        }

        private static ReadOnlySpan<char> GetExitMessage(HazardType type)
        {
            switch (type)
            {
                case HazardType.Radiation:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_RADIATION_EXIT, MsgRadiationExit);
                case HazardType.Heat:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_HEAT_EXIT, MsgHeatExit);
                case HazardType.Toxicity:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_TOXICITY_EXIT, MsgToxicityExit);
                case HazardType.Biohazard:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_BIOHAZARD_EXIT, MsgBiohazardExit);
                default:
                    return ResolveLocalizedSpan(LocalizationKeys.HAZARD_BIOHAZARD_EXIT, MsgBiohazardExit);
            }
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel localization = GlobalRegistry.LocalizationText;
            return localization != null
                ? localization.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }
    }
}

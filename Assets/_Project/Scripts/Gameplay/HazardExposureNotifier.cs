// ============================================================================
// HECTON-8 — HazardExposureNotifier.cs
// Zero-GC bridge from hazard enter/exit to HUD notification events.
// ============================================================================

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
        private const string MsgToxicityEnter = "TOXIN EXPOSURE DETECTED";
        private const string MsgToxicityExit = "TOXIN EXPOSURE CLEARED";
        private const string MsgBiohazardEnter = "BIOHAZARD EXPOSURE DETECTED";
        private const string MsgBiohazardExit = "BIOHAZARD EXPOSURE CLEARED";

        // COLD ALLOC: one exposure counter slot per HazardType enum value.
        private static readonly int[] s_activeExposureCounts = new int[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(s_activeExposureCounts, 0, s_activeExposureCounts.Length);
        }

        /// <summary>Marks the player as exposed to a hazard type.</summary>
        /// <param name="type">The hazard type.</param>
        public static void Enter(HazardType type)
        {
            int index = (int)type;
            if ((uint)index >= (uint)s_activeExposureCounts.Length)
                return;

            int previousCount = s_activeExposureCounts[index];
            s_activeExposureCounts[index] = previousCount + 1;

            if (previousCount == 0)
                NotificationEvents.PushWarning(GetEnterMessage(type));
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
                NotificationEvents.PushInfo(GetExitMessage(type));
        }

        private static string GetEnterMessage(HazardType type)
        {
            switch (type)
            {
                case HazardType.Radiation:
                    return MsgRadiationEnter;
                case HazardType.Heat:
                    return MsgHeatEnter;
                case HazardType.Toxicity:
                    return MsgToxicityEnter;
                case HazardType.Biohazard:
                    return MsgBiohazardEnter;
                default:
                    return MsgBiohazardEnter;
            }
        }

        private static string GetExitMessage(HazardType type)
        {
            switch (type)
            {
                case HazardType.Radiation:
                    return MsgRadiationExit;
                case HazardType.Heat:
                    return MsgHeatExit;
                case HazardType.Toxicity:
                    return MsgToxicityExit;
                case HazardType.Biohazard:
                    return MsgBiohazardExit;
                default:
                    return MsgBiohazardExit;
            }
        }
    }
}

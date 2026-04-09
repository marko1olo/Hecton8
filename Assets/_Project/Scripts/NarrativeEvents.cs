// ============================================================================
// HECTON-8 — NarrativeEvents.cs
// Статический событийный автобус для системы повествования.
// ============================================================================

using System;

namespace Hecton8.Core
{
    public static class NarrativeEvents
    {
        /// <summary>
        /// Вызывается при нахождении нового лорного объекта (DataPad, Wreckage).
        /// string: ID открытия.
        /// </summary>
        public static event Action<string> OnDiscoveryMade;

        /// <summary>
        /// Вызывается при достижении новой "Прогрессионной Глубины".
        /// int: Уровень тира (1-4).
        /// </summary>
        public static event Action<int> OnDepthTierReached;

        public static void RaiseDiscoveryMade(string discoveryId)
        {
            if (string.IsNullOrEmpty(discoveryId)) return;
            OnDiscoveryMade?.Invoke(discoveryId);
        }

        public static void RaiseDepthTierReached(int tier)
        {
            OnDepthTierReached?.Invoke(tier);
        }
    }
}

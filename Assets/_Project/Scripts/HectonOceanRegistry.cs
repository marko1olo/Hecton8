using System.Collections.Generic;

namespace Hecton8.Physics
{
    /// <summary>
    /// Global registry for ocean-kinematics providers so gameplay controllers never scan the scene.
    /// </summary>
    public static class HectonOceanRegistry
    {
        // COLD ALLOC: List<IHectonOceanKinematics>(4) — registered ocean providers sorted by priority on demand — owner: HectonOceanRegistry
        private static readonly List<IHectonOceanKinematics> s_providers = new List<IHectonOceanKinematics>(4);

        /// <summary>
        /// Highest-priority registered provider currently available to gameplay systems.
        /// </summary>
        public static IHectonOceanKinematics ActiveProvider { get; private set; }

        /// <summary>
        /// Registers an ocean provider and recomputes the active backend.
        /// </summary>
        public static void Register(IHectonOceanKinematics provider)
        {
            if (provider == null || s_providers.Contains(provider))
                return;

            s_providers.Add(provider);
            RecomputeActiveProvider();
        }

        /// <summary>
        /// Unregisters an ocean provider and recomputes the active backend.
        /// </summary>
        public static void Unregister(IHectonOceanKinematics provider)
        {
            if (provider == null)
                return;

            if (!s_providers.Remove(provider))
                return;

            RecomputeActiveProvider();
        }

        private static void RecomputeActiveProvider()
        {
            IHectonOceanKinematics bestProvider = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < s_providers.Count; i++)
            {
                IHectonOceanKinematics candidate = s_providers[i];
                if (candidate == null)
                    continue;

                int candidatePriority = candidate.Priority;
                if (candidatePriority <= bestPriority)
                    continue;

                bestPriority = candidatePriority;
                bestProvider = candidate;
            }

            ActiveProvider = bestProvider;
        }
    }
}

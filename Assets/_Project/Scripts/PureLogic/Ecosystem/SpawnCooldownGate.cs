using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for SpawnCooldownGate.
    /// Extracted from EncounterDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SpawnCooldownGate
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="lastSpawnTime">Parameter representing the lastSpawnTime (float).</param>
        /// <param name="currentTime">Parameter representing the currentTime (float).</param>
        /// <param name="cooldownBase">Parameter representing the cooldownBase (float).</param>
        /// <param name="currentPopulationDensity">Parameter representing the currentPopulationDensity (float).</param>
        /// <param name="densityMultiplier">Parameter representing the densityMultiplier (float).</param>
        /// <returns>Returns canSpawn of type bool.</returns>
        public static bool EvaluateGate(float lastSpawnTime, float currentTime, float cooldownBase, float currentPopulationDensity, float densityMultiplier)
        {
            if (float.IsNaN(lastSpawnTime) || float.IsInfinity(lastSpawnTime)) lastSpawnTime = 0f;
            if (float.IsNaN(currentTime) || float.IsInfinity(currentTime)) currentTime = 0f;
            if (float.IsNaN(cooldownBase) || float.IsInfinity(cooldownBase)) cooldownBase = 0f;
            if (float.IsNaN(currentPopulationDensity) || float.IsInfinity(currentPopulationDensity)) currentPopulationDensity = 0f;
            if (float.IsNaN(densityMultiplier) || float.IsInfinity(densityMultiplier)) densityMultiplier = 0f;

            lastSpawnTime = Math.Max(0f, lastSpawnTime);
            currentTime = Math.Max(0f, currentTime);
            cooldownBase = Math.Max(0f, cooldownBase);
            currentPopulationDensity = Math.Max(0f, currentPopulationDensity);
            densityMultiplier = Math.Max(0f, densityMultiplier);

            if (currentTime < lastSpawnTime) currentTime = lastSpawnTime;

            float effectiveCooldown = cooldownBase + (currentPopulationDensity * densityMultiplier);
            if (float.IsInfinity(effectiveCooldown)) effectiveCooldown = float.MaxValue;

            float timeSinceLastSpawn = currentTime - lastSpawnTime;

            return timeSinceLastSpawn >= effectiveCooldown;
        }
    }
}

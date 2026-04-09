// ============================================================================
// HECTON-8 — ToxinHazard.cs
// Toxin hazard that damages player over time.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    #pragma warning disable CS0414 // Placeholder serialized toxin tuning is intentionally retained until toxin subsystem is wired.
    /// <summary>Toxin hazard zone that applies toxin damage to players.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Toxin Hazard")]
    public sealed class ToxinHazard : EnvironmentalHazard
    {
        /// <summary>Rate at which toxin builds up.</summary>
        [Header("Toxin Settings")]
        [SerializeField] private float toxinBuildupRate = 0.3f;

        /// <summary>Maximum toxin level.</summary>
        [SerializeField] private float maxToxinLevel = 50f;

        /// <summary>Applies toxin effect to health.</summary>
        /// <param name="health">The health component.</param>
        protected override void ApplyHazardEffect(HectonPlayerHealth health)
        {
            // Assume health has toxin system
            // health.AddToxin(damagePerSecond * Time.deltaTime);
            base.ApplyHazardEffect(health);
        }

        /// <summary>Called when entering toxin zone.</summary>
        /// <param name="health">The player health component.</param>
        protected override void OnEnterHazard(HectonPlayerHealth health)
        {
            HazardExposureNotifier.Enter(HazardType.Toxicity);
        }

        /// <summary>Called when exiting toxin zone.</summary>
        /// <param name="health">The player health component.</param>
        protected override void OnExitHazard(HectonPlayerHealth health)
        {
            HazardExposureNotifier.Exit(HazardType.Toxicity);
        }
    }
    #pragma warning restore CS0414
}

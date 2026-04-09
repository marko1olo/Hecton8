// ============================================================================
// HECTON-8 — RadiationHazard.cs
// Radiation hazard that damages player over time.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    #pragma warning disable CS0414 // Placeholder serialized radiation tuning is intentionally retained until radiation subsystem is wired.
    /// <summary>Radiation hazard zone that applies radiation damage to players.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard")]
    public sealed class RadiationHazard : EnvironmentalHazard
    {
        /// <summary>Rate at which radiation builds up.</summary>
        [Header("Radiation Settings")]
        [SerializeField] private float radiationBuildupRate = 0.5f;

        /// <summary>Maximum radiation level.</summary>
        [SerializeField] private float maxRadiationLevel = 100f;

        /// <summary>Applies radiation effect to health.</summary>
        /// <param name="health">The health component.</param>
        protected override void ApplyHazardEffect(HectonPlayerHealth health)
        {
            // Assume health has radiation system
            // health.AddRadiation(damagePerSecond * Time.deltaTime);
            base.ApplyHazardEffect(health);
        }

        /// <summary>Called when entering radiation zone.</summary>
        /// <param name="health">The player health component.</param>
        protected override void OnEnterHazard(HectonPlayerHealth health)
        {
            HazardExposureNotifier.Enter(HazardType.Radiation);
        }

        /// <summary>Called when exiting radiation zone.</summary>
        /// <param name="health">The player health component.</param>
        protected override void OnExitHazard(HectonPlayerHealth health)
        {
            HazardExposureNotifier.Exit(HazardType.Radiation);
        }
    }
    #pragma warning restore CS0414
}

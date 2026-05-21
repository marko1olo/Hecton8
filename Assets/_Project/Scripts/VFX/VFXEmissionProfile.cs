using UnityEngine;

namespace Hecton8.VFX
{
    /// <summary>
    /// Authoring profile for GPU fluid-particle emission classes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VFXEmissionProfile",
        menuName = "Hecton8/VFX/Emission Profile",
        order = 141)]
    public sealed class VFXEmissionProfile : ScriptableObject
    {
        /// <summary>Hardware-facing budget tiers used by lightweight VFX systems.</summary>
        public enum HardwareTier
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        /// <summary>Supported GPU particle-fluid classes.</summary>
        public enum FluidType
        {
            Snow = 0,
            Bubble = 1,
            Debris = 2,
            Plankton = 3
        }

        /// <summary>
        /// Per-fluid simulation coefficients consumed by GPU particle owners.
        /// </summary>
        [System.Serializable]
        public struct FluidSettings
        {
            [Tooltip("Frame-rate independent anisotropic drag coefficient for this fluid type.")]
            [Min(0f)] public float baseDragCoeff;

            [Tooltip("Signed buoyancy modifier applied by the compute kernel. Positive values rise, negative values sink.")]
            public float buoyancyModifier;

            [Tooltip("Scales the injected turbulence noise after anisotropic drag is applied.")]
            [Min(0f)] public float turbulenceScale;

            [Tooltip("Optional lateral wobble scale used by bubble-like particles.")]
            [Min(0f)] public float wobbleScale;
        }

        /// <summary>
        /// Hardware-tier raymarch step budgets for compute-driven god rays.
        /// </summary>
        [System.Serializable]
        public struct VolumetricLightBudget
        {
            [Tooltip("Low-tier god ray step count. Mandate baseline = 8.")]
            [Min(1)] public int lowTierSteps;

            [Tooltip("Medium-tier god ray step count. Mandate baseline = 16.")]
            [Min(1)] public int mediumTierSteps;

            [Tooltip("High-tier god ray step count. Mandate baseline = 32.")]
            [Min(1)] public int highTierSteps;
        }

        [Header("Fluid Presets")]
        [SerializeField]
        [Tooltip("Default marine-snow coefficients. Mandate baseline drag = 0.15.")]
        private FluidSettings snow = new FluidSettings
        {
            baseDragCoeff = 0.15f,
            buoyancyModifier = -0.02f,
            turbulenceScale = 1.0f,
            wobbleScale = 0.0f
        };

        [SerializeField]
        [Tooltip("Default bubble coefficients. Mandate baseline drag = 0.08.")]
        private FluidSettings bubble = new FluidSettings
        {
            baseDragCoeff = 0.08f,
            buoyancyModifier = 1.0f,
            turbulenceScale = 0.45f,
            wobbleScale = 1.0f
        };

        [SerializeField]
        [Tooltip("Default debris coefficients. Mandate baseline drag = 0.22.")]
        private FluidSettings debris = new FluidSettings
        {
            baseDragCoeff = 0.22f,
            buoyancyModifier = -0.15f,
            turbulenceScale = 0.65f,
            wobbleScale = 0.0f
        };

        [SerializeField]
        [Tooltip("Default plankton coefficients. Uses a gentle upward-neutral drift with mild turbulence.")]
        private FluidSettings plankton = new FluidSettings
        {
            baseDragCoeff = 0.12f,
            buoyancyModifier = 0.08f,
            turbulenceScale = 1.15f,
            wobbleScale = 0.2f
        };

        [Header("Volumetric Budgets")]
        [SerializeField]
        [Tooltip("Hardware-tier raymarch budgets for compute-driven volumetric god rays.")]
        private VolumetricLightBudget volumetricLightBudget = new VolumetricLightBudget
        {
            lowTierSteps = 8,
            mediumTierSteps = 16,
            highTierSteps = 32
        };

        /// <summary>Returns the resolved settings for the requested fluid class.</summary>
        public FluidSettings GetSettings(FluidType fluidType)
        {
            switch (fluidType)
            {
                case FluidType.Bubble:
                    return bubble;
                case FluidType.Debris:
                    return debris;
                case FluidType.Plankton:
                    return plankton;
                default:
                    return snow;
            }
        }

        /// <summary>Returns the god-ray raymarch step budget for the requested hardware tier.</summary>
        public int GetVolumetricGodRaySteps(HardwareTier hardwareTier)
        {
            float fallbackWeight = Mathf.Clamp01((int)hardwareTier * 0.5f);
            return GetVolumetricGodRaySteps(fallbackWeight);
        }

        /// <summary>Returns the god-ray raymarch step budget for a continuous quality weight.</summary>
        public int GetVolumetricGodRaySteps(float globalQualityWeight)
        {
            float q = float.IsNaN(globalQualityWeight) || float.IsInfinity(globalQualityWeight)
                ? 0.5f
                : Mathf.Clamp01(globalQualityWeight);
            float low = Mathf.Max(1, volumetricLightBudget.lowTierSteps);
            float middle = Mathf.Max(1, volumetricLightBudget.mediumTierSteps);
            float high = Mathf.Max(1, volumetricLightBudget.highTierSteps);
            float lowToMiddle = SmoothStep01(q * 1.82f);
            float middleToHigh = SmoothStep01((q - 0.45f) * 1.82f);
            float steps = low +
                (middle - low) * lowToMiddle +
                (high - middle) * middleToHigh;
            return Mathf.Clamp(Mathf.RoundToInt(steps), 1, Mathf.RoundToInt(high));
        }

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ClampSettings(ref snow);
            ClampSettings(ref bubble);
            ClampSettings(ref debris);
            ClampSettings(ref plankton);
            volumetricLightBudget.lowTierSteps = Mathf.Max(1, volumetricLightBudget.lowTierSteps);
            volumetricLightBudget.mediumTierSteps = Mathf.Max(1, volumetricLightBudget.mediumTierSteps);
            volumetricLightBudget.highTierSteps = Mathf.Max(1, volumetricLightBudget.highTierSteps);
        }

        private static void ClampSettings(ref FluidSettings settings)
        {
            settings.baseDragCoeff = Mathf.Max(0f, settings.baseDragCoeff);
            settings.turbulenceScale = Mathf.Max(0f, settings.turbulenceScale);
            settings.wobbleScale = Mathf.Max(0f, settings.wobbleScale);
        }
#endif
    }
}

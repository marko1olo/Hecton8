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

#if UNITY_EDITOR
        private void OnValidate()
        {
            ClampSettings(ref snow);
            ClampSettings(ref bubble);
            ClampSettings(ref debris);
            ClampSettings(ref plankton);
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

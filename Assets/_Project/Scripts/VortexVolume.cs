using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Semantic authoring wrapper over <see cref="CurrentVolume"/> for vortex traps.
    /// CurrentVolume remains the runtime flow owner; this component only stamps a stable preset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurrentVolume))]
    [AddComponentMenu("Hecton/Physics/Vortex Volume")]
    public sealed class VortexVolume : MonoBehaviour
    {
        [Header("Preset")]
        [Tooltip("If true the vortex rotates clockwise around the local up axis.")]
        [SerializeField] private bool clockwise = true;

        [Tooltip("Tangential flow strength injected into the backing CurrentVolume.")]
        [SerializeField, Min(0f)] private float vortexStrength = 4.2f;

        [Tooltip("How hard the vortex drags rigidbodies and fauna into the eye.")]
        [SerializeField, Range(0f, 1f)] private float inwardPull = 0.62f;

        [Tooltip("Optional vertical lift applied while the object is trapped in the spiral.")]
        [SerializeField, Range(-1f, 1f)] private float verticalLift = 0.18f;

        private CurrentVolume _currentVolume;

        private void Awake()
        {
            TryGetComponent(out _currentVolume);
            ApplyPreset();
        }

        private void OnEnable()
        {
            ApplyPreset();
        }

        private void ApplyPreset()
        {
            if (_currentVolume == null)
                return;

            _currentVolume.ApplySemanticFlowPreset(
                clockwise
                    ? CurrentVolume.FlowPattern.VortexClockwise
                    : CurrentVolume.FlowPattern.VortexCounterClockwise,
                Vector3.forward,
                vortexStrength,
                verticalLift,
                inwardPull);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_currentVolume == null)
                TryGetComponent(out _currentVolume);

            inwardPull = Mathf.Clamp01(inwardPull);
            verticalLift = Mathf.Clamp(verticalLift, -1f, 1f);
            vortexStrength = Mathf.Max(0f, vortexStrength);
            ApplyPreset();
        }
#endif
    }
}

using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Physics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Thin runtime root for the active submarine transport frame.
    /// </summary>
    /// <remarks>
    /// This owner exposes hull motion and subsystem references through a narrow runtime contract.
    /// It does not simulate flooding, atmosphere, damage diffusion, or presentation directly.
    /// Those behaviors remain owned by their dedicated components to prevent a submarine God Object.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Core Director")]
    public sealed class SubmarineCoreDirector : MonoBehaviour, ISubmarineRuntimeContext
    {
        [Header("── Frame ──────────────────")]
        [Tooltip("Optional explicit transform used as the rider-space reference frame. Defaults to this root transform.")]
        [SerializeField] private Transform platformFrame;

        [Tooltip("When true, player yaw inherits submarine hull rotation through the shared transport pipeline.")]
        [SerializeField] private bool inheritPlatformRotation = true;

        [Header("── References ──────────────────")]
        [Tooltip("Optional explicit rigidbody driving hull motion. Defaults to the owned Rigidbody on this root.")]
        [SerializeField] private Rigidbody hullRigidbody;

        [Tooltip("Optional explicit flooding owner. Defaults to the owned SubmarineFluidDynamics on this root.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;

        [Tooltip("Optional explicit atmosphere owner. Defaults to the owned SubmarineAtmosphereSystem on this root.")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;

        [Tooltip("Optional explicit structural owner. Defaults to the owned SubmarineStructuralGrid on this root.")]
        [SerializeField] private SubmarineStructuralGrid structuralGrid;

        private Transform _cachedTransform;

        /// <inheritdoc />
        public bool IsTransportPlatformActive => isActiveAndEnabled && PlatformTransform != null && hullRigidbody != null;

        /// <inheritdoc />
        public Transform PlatformTransform => platformFrame != null ? platformFrame : _cachedTransform;

        /// <inheritdoc />
        public bool InheritPlatformRotation => inheritPlatformRotation;

        /// <inheritdoc />
        public Rigidbody HullRigidbody => hullRigidbody;

        /// <inheritdoc />
        public SubmarineFluidDynamics FluidDynamics => fluidDynamics;

        /// <inheritdoc />
        public SubmarineAtmosphereSystem AtmosphereSystem => atmosphereSystem;

        /// <inheritdoc />
        public SubmarineStructuralGrid StructuralGrid => structuralGrid;

        private void Awake()
        {
            _cachedTransform = transform;
            CacheReferences();
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            CacheReferences();
            GlobalRegistry.RegisterSubmarine(this);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(GlobalRegistry.Submarine, this))
                GlobalRegistry.UnregisterSubmarine(this);
        }

        /// <inheritdoc />
        public Vector3 GetPlatformPointVelocity(Vector3 worldPoint)
        {
            Rigidbody body = hullRigidbody;
            if (body == null)
                return Vector3.zero;

            Vector3 centerOfMass = body.worldCenterOfMass;
            Vector3 radialOffset = worldPoint - centerOfMass;
            Vector3 pointVelocity = body.linearVelocity + Vector3.Cross(body.angularVelocity, radialOffset);
            return IsFinite(pointVelocity) ? pointVelocity : Vector3.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cachedTransform = transform;
            CacheReferences();
        }
#endif

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (platformFrame == null)
                platformFrame = _cachedTransform;

            if (hullRigidbody == null)
                TryGetComponent(out hullRigidbody);

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);

            if (structuralGrid == null)
                TryGetComponent(out structuralGrid);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }
}

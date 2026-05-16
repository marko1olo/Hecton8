using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI.Navigation
{
    /// <summary>
    /// Cold-path authoring bridge that maps the gyro compass runtime to a physical 3D tool.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Navigation/Diegetic Gyro Compass Physical Binding")]
    public sealed class DiegeticGyroCompassPhysicalBinding : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField, Tooltip("Runtime that owns vault-backed gyro compass state. Defaults to this object.")]
        private DiegeticGyroCompassRuntime runtime;

        [SerializeField, Tooltip("Adds the runtime to this physical tool when no runtime is assigned.")]
        private bool createRuntimeIfMissing = true;

        [Header("Physical Tool")]
        [SerializeField, Tooltip("Physical hand tool or cockpit instrument root. Defaults to this transform.")]
        private Transform toolRoot;

        [SerializeField, Tooltip("Physical dial pivot rotated by the false bearing.")]
        private Transform dialPivot;

        [SerializeField, Tooltip("Diegetic TMP label for snapped cardinal output. TextMeshProUGUI must be on a World Space Canvas.")]
        private TMP_Text cardinalText;

        [Header("High Tier Dial")]
        [SerializeField, Tooltip("Optional High/Ultra indirect dial mesh.")]
        private Mesh dialMesh;

        [SerializeField, Tooltip("Optional High/Ultra indirect dial material.")]
        private Material dialIndirectMaterial;

        [Header("High Tier Failure VFX")]
        [SerializeField, Tooltip("Optional local salt/static particle emitter around the physical compass glass.")]
        private ParticleSystem anomalyFailureParticles;

        [SerializeField, Min(0), Tooltip("Authored particle burst budget. Runtime clamps to its safety cap.")]
        private int anomalyParticleBurst = 64;

        private bool _started;

        private void Awake()
        {
            if (toolRoot == null)
                toolRoot = transform;
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            ResolveRuntime();
            InjectDependencies();
            ApplyBinding();
        }

        private void Start()
        {
            _started = true;
            ResolveRuntime();
            InjectDependencies();
            ApplyBinding();
        }

        /// <summary>
        /// Re-applies the physical compass binding from serialized authoring fields.
        /// </summary>
        /// <returns>True when a runtime was available and received the binding.</returns>
        public bool TryApplyBinding()
        {
            ResolveRuntime();
            if (runtime == null)
                return false;

            ApplyBinding();
            return true;
        }

        private void ResolveRuntime()
        {
            if (runtime != null)
                return;

            if (TryGetComponent(out runtime))
                return;

            if (createRuntimeIfMissing)
                runtime = gameObject.AddComponent<DiegeticGyroCompassRuntime>(); // COLD ALLOC: Component[1] - physical compass runtime fallback - owner: DiegeticGyroCompassPhysicalBinding
        }

        private void InjectDependencies()
        {
            if (runtime == null || !Application.isPlaying)
                return;

            runtime.InjectDependencies(
                GlobalRegistry.Player,
                GlobalRegistry.DataVault,
                GlobalRegistry.ScalabilityTier);
        }

        private void ApplyBinding()
        {
            if (runtime == null)
                return;

            Transform resolvedToolRoot = toolRoot != null ? toolRoot : transform;
            runtime.ConfigurePhysicalBinding(
                resolvedToolRoot,
                dialPivot,
                cardinalText,
                dialMesh,
                dialIndirectMaterial);
            runtime.ConfigureFailureVfx(anomalyFailureParticles, anomalyParticleBurst);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (toolRoot == null)
                toolRoot = transform;

            if (anomalyParticleBurst < 0)
                anomalyParticleBurst = 0;
        }
#endif
    }
}

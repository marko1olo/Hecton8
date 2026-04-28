using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Resolves the currently active player transport source.
    /// </summary>
    /// <remarks>
    /// Tool-driven transport is resolved from <see cref="PlayerToolManager"/>.
    /// External mounted transport can temporarily claim ownership through <see cref="SetExternalTransportSource"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Transport Coordinator")]
    public sealed class PlayerTransportCoordinator : MonoBehaviour
    {
        private const float DefaultTransportPropulsionReference = 800f;

        [Header("References")]
        [Tooltip("Optional explicit tool owner used for handheld transport resolution.")]
        [SerializeField] private PlayerToolManager playerToolManager;

        private IPlayerTransportSource _externalTransportSource;
        private MonoBehaviour _externalTransportBehaviour;
        private PlayerTransportFeelContract _externalTransportFeelContract;
        private IPlayerTransportLifecycleOwner _externalTransportLifecycleOwner;
        private PlayerToolManager _subscribedToolManager;
        private IPlayerTransportLifecycleOwner _publishedLifecycleOwner;

        /// <summary>
        /// Raised when the resolved runtime transport lifecycle owner changes.
        /// </summary>
        public event Action<IPlayerTransportLifecycleOwner> ActiveTransportLifecycleChanged;

        private void Awake()
        {
            ResolveReferences();
            RefreshToolManagerSubscription();
            PublishActiveTransportLifecycleChanged();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshToolManagerSubscription();
            PublishActiveTransportLifecycleChanged();
        }

        private void OnDisable()
        {
            UnsubscribeFromToolManager();
            _publishedLifecycleOwner = null;
        }

        /// <summary>
        /// Registers an external transport source such as a mounted vehicle.
        /// </summary>
        public bool SetExternalTransportSource(IPlayerTransportSource source)
        {
            if (source == null)
            {
                _externalTransportSource = null;
                _externalTransportBehaviour = null;
                _externalTransportFeelContract = null;
                return false;
            }

            MonoBehaviour sourceBehaviour = source as MonoBehaviour;
            if (sourceBehaviour == null)
                return false;

            _externalTransportSource = source;
            _externalTransportBehaviour = sourceBehaviour;
            sourceBehaviour.TryGetComponent(out _externalTransportFeelContract);
            _externalTransportLifecycleOwner = sourceBehaviour as IPlayerTransportLifecycleOwner;
            PublishActiveTransportLifecycleChanged();
            return true;
        }

        /// <summary>
        /// Clears the current external transport source if the owner matches.
        /// </summary>
        public void ClearExternalTransportSource(IPlayerTransportSource source)
        {
            if (source == null || !ReferenceEquals(_externalTransportSource, source))
                return;

            _externalTransportSource = null;
            _externalTransportBehaviour = null;
            _externalTransportFeelContract = null;
            _externalTransportLifecycleOwner = null;
            PublishActiveTransportLifecycleChanged();
        }

        /// <summary>
        /// Returns true when any transport source is currently resolved.
        /// </summary>
        public bool HasActiveTransportSource()
        {
            return TryResolveTransportSource(out _);
        }

        /// <summary>
        /// Resolves the active transport lifecycle owner for collision, charging, and failure logic.
        /// </summary>
        public bool TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (TryResolveExternalTransportLifecycleOwner(out lifecycleOwner))
                return true;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
            {
                lifecycleOwner = null;
                return false;
            }

            lifecycleOwner = playerToolManager.CurrentTool as IPlayerTransportLifecycleOwner;
            return lifecycleOwner != null;
        }

        /// <summary>
        /// Returns true when the resolved transport is actively engaged.
        /// </summary>
        public bool IsTransportActive()
        {
            return TryResolveTransportSource(out IPlayerTransportSource source) && source.IsTransportActive;
        }

        /// <summary>
        /// Returns true when an external mounted transport currently owns handheld-tool suppression.
        /// </summary>
        public bool BlocksHandheldToolUsage()
        {
            if (!TryResolveExternalTransportSource(out _))
                return false;

            PlayerTransportPreset preset = ResolveTransportPreset();
            return preset == null || preset.HolsterToolOnMount;
        }

        /// <summary>
        /// Resolves current transport propulsion force.
        /// </summary>
        public float ResolveTransportPropulsionForce()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 0f;

            if (source is IKinematicVehicleTransportSource kinematicVehicleSource && kinematicVehicleSource.IsVehicleMotionAuthoritative)
                return 0f;

            return Mathf.Max(0f, source.GetTransportPropulsionForce());
        }

        /// <summary>
        /// Resolves current transport speed multiplier.
        /// </summary>
        public float ResolveTransportSpeedMultiplier()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 1f;

            if (source is IKinematicVehicleTransportSource kinematicVehicleSource && kinematicVehicleSource.IsVehicleMotionAuthoritative)
                return 1f;

            return Mathf.Max(0.01f, source.GetTransportSpeedMultiplier());
        }

        /// <summary>
        /// Resolves current normalized transport boost.
        /// </summary>
        public float ResolveTransportBoost01()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 0f;

            float transportBoost = Mathf.Clamp01(source.GetTransportBoost01());
            if (transportBoost > 0f)
                return transportBoost;

            PlayerTransportFeelContract transportFeelContract = ResolveTransportFeelContract();
            float reference = transportFeelContract != null
                ? Mathf.Max(0.01f, transportFeelContract.PropulsionForceReference)
                : DefaultTransportPropulsionReference;
            return Mathf.Clamp01(source.GetTransportPropulsionForce() / reference);
        }

        /// <summary>
        /// Resolves the active transport feel contract for presentation and audio consumers.
        /// </summary>
        internal PlayerTransportFeelContract ResolveTransportFeelContract()
        {
            if (TryResolveExternalTransportFeelContract(out PlayerTransportFeelContract contract))
                return contract;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        internal PlayerTransportPreset ResolveTransportPreset()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null ? contract.Preset : null;
        }

        internal PlayerTransportOccupancyMode ResolveTransportOccupancyMode()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null
                ? contract.OccupancyMode
                : PlayerTransportOccupancyMode.Handheld;
        }

        internal float ResolveTransportCameraMotionScale()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null
                ? Mathf.Clamp01(contract.CameraMotionScale)
                : 1f;
        }

        internal bool TryResolveTransportSource(out IPlayerTransportSource source)
        {
            if (TryResolveExternalTransportSource(out source))
                return true;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
            {
                source = null;
                return false;
            }

            source = playerToolManager.CurrentToolTransportSource;
            return source != null;
        }

        private bool TryResolveExternalTransportSource(out IPlayerTransportSource source)
        {
            if ((object)_externalTransportBehaviour != null && _externalTransportBehaviour != null)
            {
                source = _externalTransportSource;
                return source != null;
            }

            _externalTransportSource = null;
            _externalTransportBehaviour = null;
            source = null;
            return false;
        }

        private bool TryResolveExternalTransportFeelContract(out PlayerTransportFeelContract contract)
        {
            if ((object)_externalTransportBehaviour != null &&
                _externalTransportBehaviour != null &&
                _externalTransportFeelContract != null)
            {
                contract = _externalTransportFeelContract;
                return true;
            }

            _externalTransportFeelContract = null;
            contract = null;
            return false;
        }

        private bool TryResolveExternalTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if ((object)_externalTransportBehaviour != null &&
                _externalTransportBehaviour != null &&
                _externalTransportLifecycleOwner != null)
            {
                lifecycleOwner = _externalTransportLifecycleOwner;
                return true;
            }

            _externalTransportLifecycleOwner = null;
            lifecycleOwner = null;
            return false;
        }

        private void ResolveReferences()
        {
            if (playerToolManager == null)
                gameObject.TryGetComponent(out playerToolManager);

            if (!ReferenceEquals(_subscribedToolManager, playerToolManager))
                RefreshToolManagerSubscription();
        }

        private void RefreshToolManagerSubscription()
        {
            if (ReferenceEquals(_subscribedToolManager, playerToolManager))
                return;

            UnsubscribeFromToolManager();
            if (playerToolManager == null)
                return;

            playerToolManager.ActiveSlotChanged += HandleToolSlotChanged;
            playerToolManager.ToolAssignmentsChanged += HandleToolAssignmentsChanged;
            _subscribedToolManager = playerToolManager;
        }

        private void UnsubscribeFromToolManager()
        {
            if (_subscribedToolManager == null)
                return;

            _subscribedToolManager.ActiveSlotChanged -= HandleToolSlotChanged;
            _subscribedToolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
            _subscribedToolManager = null;
        }

        private void HandleToolSlotChanged(int _)
        {
            PublishActiveTransportLifecycleChanged();
        }

        private void HandleToolAssignmentsChanged()
        {
            PublishActiveTransportLifecycleChanged();
        }

        private void PublishActiveTransportLifecycleChanged()
        {
            IPlayerTransportLifecycleOwner lifecycleOwner;
            if (!TryResolveTransportLifecycleOwner(out lifecycleOwner))
                lifecycleOwner = null;

            if (ReferenceEquals(_publishedLifecycleOwner, lifecycleOwner))
                return;

            _publishedLifecycleOwner = lifecycleOwner;
            ActiveTransportLifecycleChanged?.Invoke(lifecycleOwner);
        }
    }
}

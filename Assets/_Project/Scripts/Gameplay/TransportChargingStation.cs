using Hecton8.Core;
using Hecton8.Power;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Trigger-based charging station for nearby player-owned transports.
    /// </summary>
    /// <remarks>
    /// This owner is intentionally narrow.
    /// Existing <see cref="BatteryCharger"/> handles removable cells and direct interaction.
    /// This station handles in-world transport property: parked vehicles, active mounted transports, and the currently owned handheld transport when the player docks inside the trigger.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Transport Charging Station")]
    public sealed class TransportChargingStation : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IPowerComponent, IGlobalRegistryHotSwapListener
    {
        [Header("-- Docking -------------------------")]
        [Tooltip("Maximum number of simultaneously tracked transports inside this trigger.")]
        [SerializeField, Range(1, 16)] private int maxDockedTransports = 6;

        [Tooltip("Normalized transport charge restored per second while powered.")]
        [SerializeField, Range(0f, 1f)] private float chargeRatePerSecond = 0.18f;

        [Tooltip("Optional transport tag filter. Leave empty to accept all supported transports.")]
        [SerializeField] private string requiredTransportTag = string.Empty;

        [Header("-- Power ---------------------------")]
        [Tooltip("Power draw while at least one transport is actively charging.")]
        [SerializeField, Range(0f, 400f)] private float powerConsumption = 90f;

        [Tooltip("Power priority used by the base grid. Higher values shed earlier on deficit.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 50;

        [Header("-- Visuals -------------------------")]
        [Tooltip("Optional indicator renderers updated while the station is powered or actively charging.")]
        [SerializeField] private Renderer[] statusRenderers;

        [Tooltip("Emission color while powered but idle.")]
        [SerializeField] private Color idleColor = new Color(0f, 0.45f, 0.8f);

        [Tooltip("Emission color while actively charging.")]
        [SerializeField] private Color chargingColor = new Color(0.15f, 1f, 0.6f);

        [Tooltip("Emission color while the station has no power.")]
        [SerializeField] private Color noPowerColor = new Color(0.18f, 0.05f, 0.05f);

        private Collider _triggerCollider;
        private Transform _cachedTransform;
        private CachedTriggerVolume _cachedVolume;
        private MaterialPropertyBlock _mpb;
        private IPlayerTransportLifecycleOwner[] _trackedTransports;
        private MonoBehaviour[] _trackedBehaviours;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _indicatorDirty;
        private Color _pendingIndicatorColor;
        private bool _hasPower = true;
        private int _activeChargingCount;
        private Color _lastIndicatorColor = new Color(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        /// <summary>Power draw while actively charging at least one transport.</summary>
        public float PowerRating => _activeChargingCount > 0 ? -powerConsumption : 0f;

        /// <summary>Priority used by the power grid for this charging station.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached power state supplied by the power grid.</summary>
        public bool HasPower => _hasPower;

        private void Awake()
        {
            _cachedTransform = transform;
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, 2f);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] -- transport charging station emission state -- owner: TransportChargingStation

            int capacity = Mathf.Clamp(maxDockedTransports, 1, 16);
            _trackedTransports = new IPlayerTransportLifecycleOwner[capacity]; // COLD ALLOC: IPlayerTransportLifecycleOwner[capacity] -- tracked docking transports -- owner: TransportChargingStation
            _trackedBehaviours = new MonoBehaviour[capacity]; // COLD ALLOC: MonoBehaviour[capacity] -- tracked docking behaviours -- owner: TransportChargingStation
            UpdateIndicators();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
            UpdateIndicators();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearTrackedTransports();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        /// <summary>
        /// Tick owner for powered transport charging.
        /// </summary>
        public void Tick(float deltaTime)
        {
            RefreshTrackedTransportsFromRegistry();

            int nextActiveChargingCount = 0;
            if (_hasPower && chargeRatePerSecond > 0f)
            {
                float normalizedChargeDelta = chargeRatePerSecond * deltaTime;
                for (int i = 0; i < _trackedTransports.Length; i++)
                {
                    MonoBehaviour behaviour = _trackedBehaviours[i];
                    IPlayerTransportLifecycleOwner lifecycleOwner = _trackedTransports[i];
                    if ((object)behaviour == null || behaviour == null || lifecycleOwner == null)
                    {
                        _trackedTransports[i] = null;
                        _trackedBehaviours[i] = null;
                        continue;
                    }

                    if (!lifecycleOwner.CanReceiveTransportCharge)
                        continue;

                    float chargeBefore = lifecycleOwner.TransportChargeNormalized;
                    lifecycleOwner.RechargeTransport(normalizedChargeDelta);
                    if (lifecycleOwner.TransportChargeNormalized > chargeBefore + 0.0001f)
                        nextActiveChargingCount++;
                }
            }

            if (_activeChargingCount != nextActiveChargingCount)
            {
                _activeChargingCount = nextActiveChargingCount;
                UpdateIndicators();
            }
        }

        public void LateFrameTick()
        {
            FlushIndicators();
        }

        /// <summary>
        /// Called by the power grid when station power changes.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            if (!hasPower)
                _activeChargingCount = 0;

            UpdateIndicators();
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void RefreshTrackedTransportsFromRegistry()
        {
            for (int i = 0; i < _trackedTransports.Length; i++)
            {
                MonoBehaviour behaviour = _trackedBehaviours[i];
                IPlayerTransportLifecycleOwner owner = _trackedTransports[i];
                if (owner == null || (object)behaviour == null || behaviour == null ||
                    !PassesTransportFilter(behaviour) ||
                    !IsTransportInsideStation(behaviour))
                {
                    _trackedTransports[i] = null;
                    _trackedBehaviours[i] = null;
                }
            }

            for (int i = 0; i < PlayerTransportLifecycleRegistry.SlotCapacity; i++)
            {
                if (!PlayerTransportLifecycleRegistry.TryGetAt(i, out IPlayerTransportLifecycleOwner owner, out MonoBehaviour behaviour))
                    continue;

                if (!PassesTransportFilter(behaviour) || !IsTransportInsideStation(behaviour))
                    continue;

                AddTrackedTransport(owner, behaviour);
            }
        }

        private bool PassesTransportFilter(MonoBehaviour lifecycleBehaviour)
        {
            return lifecycleBehaviour != null &&
                   (string.IsNullOrEmpty(requiredTransportTag) || lifecycleBehaviour.CompareTag(requiredTransportTag));
        }

        private bool IsTransportInsideStation(MonoBehaviour lifecycleBehaviour)
        {
            if (lifecycleBehaviour == null)
                return false;

            Transform transportTransform = lifecycleBehaviour.transform;
            return transportTransform != null &&
                   _cachedVolume.Contains(_cachedTransform, transportTransform.position);
        }

        private bool TryResolveTransportLifecycleOwner(Collider other, out IPlayerTransportLifecycleOwner lifecycleOwner, out MonoBehaviour lifecycleBehaviour)
        {
            lifecycleOwner = other.GetComponentInParent<IPlayerTransportLifecycleOwner>();
            lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
            if (lifecycleOwner != null && lifecycleBehaviour != null)
                return true;

            IPlayerTransportLifecycleResolver transportResolver = other.GetComponentInParent<IPlayerTransportLifecycleResolver>();
            if (transportResolver != null && transportResolver.TryResolveTransportLifecycleOwner(out lifecycleOwner))
            {
                lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
                return lifecycleBehaviour != null;
            }

            lifecycleOwner = null;
            lifecycleBehaviour = null;
            return false;
        }

        private void AddTrackedTransport(IPlayerTransportLifecycleOwner lifecycleOwner, MonoBehaviour lifecycleBehaviour)
        {
            for (int i = 0; i < _trackedTransports.Length; i++)
            {
                if (ReferenceEquals(_trackedTransports[i], lifecycleOwner))
                    return;
            }

            for (int i = 0; i < _trackedTransports.Length; i++)
            {
                if (_trackedTransports[i] != null)
                    continue;

                _trackedTransports[i] = lifecycleOwner;
                _trackedBehaviours[i] = lifecycleBehaviour;
                return;
            }
        }

        private void RemoveTrackedTransport(IPlayerTransportLifecycleOwner lifecycleOwner, MonoBehaviour lifecycleBehaviour)
        {
            for (int i = 0; i < _trackedTransports.Length; i++)
            {
                if (!ReferenceEquals(_trackedTransports[i], lifecycleOwner) &&
                    !ReferenceEquals(_trackedBehaviours[i], lifecycleBehaviour))
                    continue;

                _trackedTransports[i] = null;
                _trackedBehaviours[i] = null;
                return;
            }
        }

        private void ClearTrackedTransports()
        {
            _activeChargingCount = 0;
            if (_trackedTransports == null || _trackedBehaviours == null)
                return;

            for (int i = 0; i < _trackedTransports.Length; i++)
            {
                _trackedTransports[i] = null;
                _trackedBehaviours[i] = null;
            }
        }

        private void UpdateIndicators()
        {
            if (statusRenderers == null || statusRenderers.Length == 0)
                return;

            Color targetColor = !_hasPower
                ? noPowerColor
                : _activeChargingCount > 0
                    ? chargingColor
                    : idleColor;

            if (targetColor == _lastIndicatorColor)
                return;

            _lastIndicatorColor = targetColor;
            _pendingIndicatorColor = targetColor;
            _indicatorDirty = true;
        }

        private void FlushIndicators()
        {
            if (!_indicatorDirty)
                return;

            _indicatorDirty = false;
            for (int i = 0; i < statusRenderers.Length; i++)
            {
                Renderer targetRenderer = statusRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_EmissionColorID, _pendingIndicatorColor);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}

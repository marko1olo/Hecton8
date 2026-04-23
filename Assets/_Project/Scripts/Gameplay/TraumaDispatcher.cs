using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Player-side trauma router that subscribes to active habitat and transport damage owners.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonSurvivalSystem))]
    [RequireComponent(typeof(HectonPlayerMovement))]
    public sealed class TraumaDispatcher : MonoBehaviour, ITickable, IDamageReceiver
    {
        private const float IntegrityChannelDecayPerSecond = 0.35f;
        private const float PowerChannelDecayPerSecond = 0.28f;
        private const float ClarityChannelDecayPerSecond = 0.75f;
        private const float ImpactStressTransferFactor = 0.15f;
        private const float ImpactStressNormalizationSpeed = 20f;
        private const float VehicleIntegrityLeakThreshold = 0.4f;
        private const float VehicleLeakOxygenDrainMultiplier = 1.25f;
        private const float FloodThermalThreshold = 0.3f;
        private const float FloodedInsulationFactor = 0.2f;

        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private bool _registered;
        private float _integrityChannel01;
        private float _powerChannel01;
        private float _clarityChannel01;
        private float _activeTransportIntegrityNormalized = 1f;
        private HabitatIntegrityManager _activeHabitatManager;
        private IDamageSignalEmitter _activeHabitatEmitter;
        private MonoBehaviour _activeHabitatEmitterBehaviour;
        private IPlayerTransportLifecycleOwner _activeTransportOwner;
        private IDamageSignalEmitter _activeTransportEmitter;
        private MonoBehaviour _activeTransportEmitterBehaviour;

        /// <summary>Current integrity trauma channel intensity.</summary>
        public float IntegrityChannel01 => _integrityChannel01;

        /// <summary>Current power trauma channel intensity.</summary>
        public float PowerChannel01 => _powerChannel01;

        /// <summary>Current clarity trauma channel intensity.</summary>
        public float ClarityChannel01 => _clarityChannel01;

        /// <summary>Normalized flood ratio of the currently occupied habitat module.</summary>
        public float FloodLevelNormalized => _activeHabitatManager != null
            ? _activeHabitatManager.FloodLevelNormalized
            : 0f;

        /// <summary>
        /// Runtime insulation factor consumed by survival thermal exchange.
        /// </summary>
        public float FloodedThermalInsulationFactor => FloodLevelNormalized > FloodThermalThreshold
            ? FloodedInsulationFactor
            : 1f;

        /// <summary>
        /// Extra oxygen-drain multiplier caused by transport leaks below the integrity threshold.
        /// </summary>
        public float AdditionalVehicleOxygenDrainScale => _activeTransportIntegrityNormalized < VehicleIntegrityLeakThreshold
            ? VehicleLeakOxygenDrainMultiplier
            : 1f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ModuleStatusEvents.OnModuleEnter += HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit += HandleModuleExit;

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged += HandleTransportLifecycleChanged;

            TryRegister();

            if (_playerTransportCoordinator != null &&
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner))
            {
                HandleTransportLifecycleChanged(lifecycleOwner);
            }
        }

        private void OnDisable()
        {
            ModuleStatusEvents.OnModuleEnter -= HandleModuleEnter;
            ModuleStatusEvents.OnModuleExit -= HandleModuleExit;

            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.ActiveTransportLifecycleChanged -= HandleTransportLifecycleChanged;

            ClearHabitatBinding();
            ClearTransportBinding();
            TryUnregister();
            ResetChannels();
        }

        /// <summary>
        /// Decays trauma channels and guards destroyed ownership references without polling the damage path itself.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _integrityChannel01 = DecayChannel(_integrityChannel01, IntegrityChannelDecayPerSecond, deltaTime);
            _powerChannel01 = DecayChannel(_powerChannel01, PowerChannelDecayPerSecond, deltaTime);
            _clarityChannel01 = DecayChannel(_clarityChannel01, ClarityChannelDecayPerSecond, deltaTime);

            if ((object)_activeHabitatEmitterBehaviour != null && _activeHabitatEmitterBehaviour == null)
                ClearHabitatBinding();

            if ((object)_activeTransportEmitterBehaviour != null && _activeTransportEmitterBehaviour == null)
                ClearTransportBinding();
        }

        /// <summary>
        /// Applies incoming integrity-channel trauma.
        /// </summary>
        public void OnIntegrityChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _integrityChannel01, Mathf.Max(Mathf.Abs(next - prev), src.integrityDelta / (float)byte.MaxValue));

            if (src.sourceID == DamageSourceIds.MountableTransport ||
                src.sourceID == DamageSourceIds.MantaScooter)
            {
                _activeTransportIntegrityNormalized = Mathf.Clamp01(next);
                if ((src.damageType & (uint)DamageTypeMask.Impact) != 0u)
                {
                    float transferredStress01 = Mathf.Clamp01(
                        (Mathf.Max(0f, src.magnitude) * ImpactStressTransferFactor) /
                        ImpactStressNormalizationSpeed);
                    if (transferredStress01 > 0.0001f && _playerMovement != null)
                        _playerMovement.RequestExternalHullStress(transferredStress01);

                    PromoteChannel(ref _clarityChannel01, transferredStress01);
                }
            }
        }

        /// <summary>
        /// Applies incoming power-channel trauma.
        /// </summary>
        public void OnPowerChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _powerChannel01, Mathf.Max(Mathf.Abs(next - prev), next));
        }

        /// <summary>
        /// Applies incoming clarity-channel trauma.
        /// </summary>
        public void OnClarityChanged(float prev, float next, DamageSignal src)
        {
            PromoteChannel(ref _clarityChannel01, Mathf.Max(Mathf.Abs(next - prev), next));
        }

        /// <summary>
        /// Converts discrete trauma thresholds into channel impulses.
        /// </summary>
        public void OnTraumaThresholdCrossed(TraumaLevel level)
        {
            float impulse = ResolveTraumaImpulse(level);
            if (impulse <= 0f)
                return;

            PromoteChannel(ref _integrityChannel01, impulse);
            PromoteChannel(ref _clarityChannel01, Mathf.Clamp01(impulse * 0.9f));
        }

        /// <summary>
        /// Breach packets hit integrity and clarity together.
        /// </summary>
        public void OnHullBreach(Unity.Mathematics.float3 localPoint, float depth, float pressureDelta)
        {
            PromoteChannel(ref _integrityChannel01, 1f);
            PromoteChannel(ref _clarityChannel01, Mathf.Clamp01(0.35f + pressureDelta * 0.35f));
        }

        private void HandleModuleEnter(BaseModule module)
        {
            HabitatIntegrityManager habitatManager = null;
            if (module != null)
                module.TryGetComponent(out habitatManager);

            BindHabitat(habitatManager);
        }

        private void HandleModuleExit(BaseModule module)
        {
            if (_activeHabitatManager == null || module == null)
                return;

            if (ReferenceEquals(_activeHabitatManager.GetComponent<BaseModule>(), module))
                ClearHabitatBinding();
        }

        private void HandleTransportLifecycleChanged(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            BindTransport(lifecycleOwner);
        }

        private void ResolveReferences()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_playerTransportCoordinator == null)
                TryGetComponent(out _playerTransportCoordinator);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registered = false;
        }

        private void BindHabitat(HabitatIntegrityManager habitatManager)
        {
            if (ReferenceEquals(_activeHabitatManager, habitatManager))
                return;

            ClearHabitatBinding();
            if (habitatManager == null)
                return;

            _activeHabitatManager = habitatManager;
            _activeHabitatEmitter = habitatManager;
            _activeHabitatEmitterBehaviour = habitatManager;
            _activeHabitatEmitter.RegisterDamageReceiver(this);
        }

        private void ClearHabitatBinding()
        {
            if (_activeHabitatEmitter != null)
                _activeHabitatEmitter.UnregisterDamageReceiver(this);

            _activeHabitatManager = null;
            _activeHabitatEmitter = null;
            _activeHabitatEmitterBehaviour = null;
        }

        private void BindTransport(IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (ReferenceEquals(_activeTransportOwner, lifecycleOwner))
                return;

            ClearTransportBinding();
            _activeTransportOwner = lifecycleOwner;
            _activeTransportIntegrityNormalized = lifecycleOwner != null
                ? Mathf.Clamp01(lifecycleOwner.TransportIntegrityNormalized)
                : 1f;

            if (!(lifecycleOwner is IDamageSignalEmitter emitter) || !(lifecycleOwner is MonoBehaviour behaviour))
                return;

            _activeTransportEmitter = emitter;
            _activeTransportEmitterBehaviour = behaviour;
            _activeTransportEmitter.RegisterDamageReceiver(this);
        }

        private void ClearTransportBinding()
        {
            if (_activeTransportEmitter != null)
                _activeTransportEmitter.UnregisterDamageReceiver(this);

            _activeTransportOwner = null;
            _activeTransportEmitter = null;
            _activeTransportEmitterBehaviour = null;
            _activeTransportIntegrityNormalized = 1f;
        }

        private void ResetChannels()
        {
            _integrityChannel01 = 0f;
            _powerChannel01 = 0f;
            _clarityChannel01 = 0f;
        }

        private static float DecayChannel(float current, float decayPerSecond, float deltaTime)
        {
            return Mathf.Max(0f, current - decayPerSecond * Mathf.Max(0f, deltaTime));
        }

        private static void PromoteChannel(ref float channel, float candidate)
        {
            if (candidate > channel)
                channel = Mathf.Clamp01(candidate);
        }

        private static float ResolveTraumaImpulse(TraumaLevel level)
        {
            switch (level)
            {
                case TraumaLevel.Minor:
                    return 0.2f;
                case TraumaLevel.Significant:
                    return 0.45f;
                case TraumaLevel.Critical:
                    return 0.75f;
                case TraumaLevel.Catastrophic:
                    return 1f;
                default:
                    return 0f;
            }
        }
    }
}

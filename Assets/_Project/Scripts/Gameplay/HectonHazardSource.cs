// ============================================================================
// HECTON-8 — HectonHazardSource.cs  v1.0
// Komponent lokalnogo istochnika opasnosti.
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Izluchatel opasnosti (radiatsiya, teplo).
    /// Prikreplyaetsya k prefabam (geyzery, oblomki reaktorov).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonHazardSource : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const float HazardSourceSlowTickDeltaSeconds = 0.1f;
        // ══════════════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════════════

        [Header("── Hazard Settings ───────────────────────────")]
        [Tooltip("Tip izluchaemoy opasnosti.")]
        [SerializeField] private HazardType _type = HazardType.Radiation;
        [Tooltip("Optional authored profile for hazard metadata and visor corruption bias.")]
        [SerializeField] private HazardZoneProfile _profile;

        [Tooltip("Bazovaya intensivnost v tsentre (0-100+).")]
        [SerializeField] private float _intensity = 50f;

        [Tooltip("Radius vozdeystviya (metry).")]
        [SerializeField] private float _radius = 15f;

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Esli true, polozhenie istochnika schitaetsya fiksirovannym. " +
                 "Ekonomit CPU na obnovlenii pozitsii v menedzhere.")]
        [SerializeField] private bool _isStatic = true;

        [Tooltip("Interval obnovleniya pozitsii dlya dinamicheskih istochnikov (sekundy).")]
        [SerializeField, Range(0.1f, 2f)] private float _updateInterval = 0.5f;

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════════════

        private int _instanceID;
        private Transform _tr;
        private float _timer;
        private bool _registeredSlowTick;
        private bool _hotSwapListenerRegistered;
        private bool _registeredRadiationSource;
        private IThermodynamicsService _thermodynamicsService;

        // ══════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _instanceID = unchecked((int)EntityId.ToULong(GetEntityId()));
            _tr = transform;
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InternalUpdateRegistry();

            if (!_isStatic)
                TryRegisterToSlowTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterAuthority();
            TryUnregisterHotSwapListener();
            TryUnregisterFromSlowTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterAuthority();
            TryUnregisterHotSwapListener();
            TryUnregisterFromSlowTickManager();
        }

        // ══════════════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_isStatic) return;

            _timer -= HazardSourceSlowTickDeltaSeconds;
            if (_timer <= 0f)
            {
                _timer = _updateInterval;
                InternalUpdateRegistry(); 
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════════════

        private void InternalUpdateRegistry()
        {
            HazardType resolvedType = ResolveHazardType();
            if (resolvedType == HazardType.Radiation)
            {
                HectonHazardManager.Unregister(_instanceID);
                RadiationHazardGrid.RegisterSource(_instanceID, _tr.position, _intensity, _radius);
                _registeredRadiationSource = true;
                return;
            }

            if (_registeredRadiationSource)
            {
                RadiationHazardGrid.UnregisterSource(_instanceID);
                _registeredRadiationSource = false;
            }

            if (resolvedType == HazardType.Heat)
            {
                HectonHazardManager.Unregister(_instanceID);
                IThermodynamicsService thermodynamics = _thermodynamicsService;
                if (thermodynamics != null && thermodynamics.IsInitialized)
                    thermodynamics.TryInjectTransientHeatSource(_tr.position, _radius, _intensity, unchecked((uint)_instanceID));
                return;
            }

            HectonHazardManager.Register(
                _instanceID,
                _tr.position,
                _intensity,
                _radius,
                resolvedType,
                ResolveVisorGlitchBias(),
                _profile);
        }

        private void TryUnregisterAuthority()
        {
            if (_registeredRadiationSource)
            {
                RadiationHazardGrid.UnregisterSource(_instanceID);
                _registeredRadiationSource = false;
            }

            HectonHazardManager.Unregister(_instanceID);
        }

        private HazardType ResolveHazardType()
        {
            return _profile != null ? _profile.HazardType : _type;
        }

        private float ResolveVisorGlitchBias()
        {
            return _profile != null ? _profile.VisorGlitchBias : 1f;
        }

        private void TryRegisterToSlowTickManager()
        {
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromSlowTickManager()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
                _thermodynamicsService = currentService as IThermodynamicsService;
        }

        // ══════════════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Color c = ResolveHazardType() switch
            {
                HazardType.Radiation => Color.cyan,
                HazardType.Heat => Color.yellow,
                HazardType.Toxicity => Color.green,
                _ => Color.white
            };

            c.a = 0.2f;
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, _radius);
            
            c.a = 0.4f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
#endif
    }
}

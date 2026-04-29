// ============================================================================
// HECTON-8 — HectonHazardSource.cs  v1.0
// Компонент локального источника опасности.
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Излучатель опасности (радиация, тепло).
    /// Прикрепляется к префабам (гейзеры, обломки реакторов).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonHazardSource : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════════════

        [Header("── Hazard Settings ───────────────────────────")]
        [Tooltip("Тип излучаемой опасности.")]
        [SerializeField] private HazardType _type = HazardType.Radiation;
        [Tooltip("Optional authored profile for hazard metadata and visor corruption bias.")]
        [SerializeField] private HazardZoneProfile _profile;

        [Tooltip("Базовая интенсивность в центре (0-100+).")]
        [SerializeField] private float _intensity = 50f;

        [Tooltip("Радиус воздействия (метры).")]
        [SerializeField] private float _radius = 15f;

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Если true, положение источника считается фиксированным. " +
                 "Экономит CPU на обновлении позиции в менеджере.")]
        [SerializeField] private bool _isStatic = true;

        [Tooltip("Интервал обновления позиции для динамических источников (секунды).")]
        [SerializeField, Range(0.1f, 2f)] private float _updateInterval = 0.5f;

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════════════

        private int _instanceID;
        private Transform _tr;
        private float _timer;
        private bool _isRegisteredInTick;

        // ══════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _instanceID = unchecked((int)EntityId.ToULong(GetEntityId()));
            _tr = transform;
        }

        private void OnEnable()
        {
            InternalUpdateRegistry();

            if (!_isStatic)
                TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            HectonHazardManager.Unregister(_instanceID);
            TryUnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            HectonHazardManager.Unregister(_instanceID);
            TryUnregisterFromTickManager();
        }

        // ══════════════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_isStatic) return;

            _timer -= deltaTime;
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
            HectonHazardManager.Register(
                _instanceID,
                _tr.position,
                _intensity,
                _radius,
                ResolveHazardType(),
                ResolveVisorGlitchBias());
        }

        private HazardType ResolveHazardType()
        {
            return _profile != null ? _profile.HazardType : _type;
        }

        private float ResolveVisorGlitchBias()
        {
            return _profile != null ? _profile.VisorGlitchBias : 1f;
        }

        private void TryRegisterToTickManager()
        {
            if (_isRegisteredInTick)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegisteredInTick = true;
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_isRegisteredInTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegisteredInTick = false;
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

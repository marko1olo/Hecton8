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
    public sealed class HectonHazardSource : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════════════

        [Header("── Hazard Settings ───────────────────────────")]
        [Tooltip("Тип излучаемой опасности.")]
        [SerializeField] private HazardType _type = HazardType.Radiation;

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
            #pragma warning disable CS0618
            _instanceID = GetInstanceID();
            #pragma warning restore CS0618
            _tr = transform;
        }

        private void OnEnable()
        {
            InternalUpdateRegistry();

            if (!_isStatic)
            {
                if (GameTickManager.Instance != null && !_isRegisteredInTick)
                {
                    GameTickManager.Instance.Register(this);
                    _isRegisteredInTick = true;
                }
            }
        }

        private void OnDisable()
        {
            HectonHazardManager.Unregister(_instanceID);

            if (_isRegisteredInTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _isRegisteredInTick = false;
            }
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
            HectonHazardManager.Register(_instanceID, _tr.position, _intensity, _radius, _type);
        }

        // ══════════════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Color c = _type switch
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

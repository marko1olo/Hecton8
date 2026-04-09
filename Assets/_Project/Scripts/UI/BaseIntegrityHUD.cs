// ============================================================================
// HECTON-8 — BaseIntegrityHUD.cs
// HUD-компонент: индикатор состояния базы.
//
// ЛОР (лор1 — База как персонаж):
//   При 75%: первые визуальные сигналы (трещины на иллюминаторах)
//   При 50%: звуковые сигналы (скрип корпуса)
//   При 25%: Hecton-OS предупреждает постоянно
//   При 0%:  разгерметизация — вода входит за 30 секунд
//
// АРХИТЕКТУРА:
//   • Слушает BaseModule события через статический event bus.
//   • Показывает состояние ближайшего модуля базы.
//   • Интегрируется с NotificationEvents.
//   • ISlowTickable — обновление раз в 0.5с.
// ============================================================================

using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.UI
{
    public static class BaseIntegrityEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnModuleIntegrityWarning = null;
            OnModuleBreached = null;
        }

        /// <summary>Предупреждение о состоянии модуля. float: integrity [0..1].</summary>
        public static event System.Action<float> OnModuleIntegrityWarning;

        /// <summary>Модуль разгерметизирован.</summary>
        public static event System.Action OnModuleBreached;

        public static void RaiseIntegrityWarning(float integrity)
            => OnModuleIntegrityWarning?.Invoke(integrity);

        public static void RaiseBreached()
            => OnModuleBreached?.Invoke();
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-55)]
    public sealed class BaseIntegrityHUD : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Thresholds ──────────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float warningThreshold  = 0.75f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold   = 0.10f;

        [Header("── Scan Radius ─────────────────────────────")]
        [Tooltip("Радиус поиска ближайшего модуля базы (метры).")]
        [SerializeField] private float scanRadius = 50f;

        [SerializeField] private LayerMask moduleLayerMask = ~0;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static BaseIntegrityHUD Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private bool _registered;
        private float _lastWarningIntegrity = 1f;

        // NonAlloc buffer для поиска модулей
        private readonly Collider[] _scanBuffer = new Collider[8]; // COLD ALLOC

        // Throttle для предупреждений
        private float _nextWarningTime;
        private const float WarningCooldown = 30f;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform()) return;

            // Ищем ближайший модуль базы
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                _playerTransform.position,
                scanRadius,
                _scanBuffer,
                moduleLayerMask,
                QueryTriggerInteraction.Ignore);

            BaseModule nearestModule = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                if (!_scanBuffer[i].TryGetComponent(out BaseModule module)) continue;

                float dist = Vector3.Distance(_playerTransform.position,
                    _scanBuffer[i].transform.position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestModule = module;
                }
            }

            if (nearestModule == null) return;

            float integrity = nearestModule.MaxIntegrity > 0f
                ? nearestModule.CurrentIntegrity / nearestModule.MaxIntegrity
                : 0f;
            CheckIntegrityWarnings(integrity);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void CheckIntegrityWarnings(float integrity)
        {
            if (Time.time < _nextWarningTime) return;
            if (Mathf.Abs(integrity - _lastWarningIntegrity) < 0.05f) return;

            _lastWarningIntegrity = integrity;

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.3f;
                NotificationEvents.PushWarning(
                    $"КРИТИЧЕСКОЕ СОСТОЯНИЕ БАЗЫ: {integrity * 100f:F0}% — РАЗГЕРМЕТИЗАЦИЯ НЕИЗБЕЖНА!");
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
            }
            else if (integrity <= criticalThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.5f;
                NotificationEvents.PushWarning(
                    $"HECTON-OS: ЦЕЛОСТНОСТЬ МОДУЛЯ {integrity * 100f:F0}% — ТРЕБУЕТСЯ РЕМОНТ.");
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
            }
            else if (integrity <= warningThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown;
                NotificationEvents.PushInfo(
                    $"МОДУЛЬ БАЗЫ: ЦЕЛОСТНОСТЬ {integrity * 100f:F0}%.");
            }
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            return true;
        }
    }
}

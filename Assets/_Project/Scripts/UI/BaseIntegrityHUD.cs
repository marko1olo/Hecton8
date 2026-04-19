// ============================================================================
// HECTON-8 - BaseIntegrityHUD.cs
// HUD component: nearest base-module integrity warning bridge.
// ============================================================================

using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
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

        /// <summary>Integrity warning for the nearest base module. float: integrity [0..1].</summary>
        public static event System.Action<float> OnModuleIntegrityWarning;

        /// <summary>The tracked base module is considered breached.</summary>
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
        [Header("── Thresholds ─────────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold = 0.10f;

        [Header("── Scan Radius ───────────────────────────")]
        [Tooltip("Search radius for the nearest base module in meters.")]
        [SerializeField] private float scanRadius = 50f;

        [SerializeField] private LayerMask moduleLayerMask = ~0;

        public static BaseIntegrityHUD Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance() => Instance = null;

        private Transform _playerTransform;
        private bool _registered;
        private float _lastWarningIntegrity = 1f;
        private float _nextWarningTime;

        // COLD ALLOC: Collider[8] - nearest-module overlap scan buffer - owner: BaseIntegrityHUD
        private readonly Collider[] _scanBuffer = new Collider[8];

        private const float WarningCooldown = 30f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();
            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

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
                Collider hit = _scanBuffer[i];
                if (hit == null || !hit.TryGetComponent(out BaseModule module))
                    continue;

                float dist = Vector3.Distance(_playerTransform.position, hit.transform.position);
                if (dist >= nearestDist)
                    continue;

                nearestDist = dist;
                nearestModule = module;
            }

            if (nearestModule == null)
                return;

            float integrity = nearestModule.MaxIntegrity > 0f
                ? nearestModule.CurrentIntegrity / nearestModule.MaxIntegrity
                : 0f;
            CheckIntegrityWarnings(integrity);
        }

        private void CheckIntegrityWarnings(float integrity)
        {
            if (Time.time < _nextWarningTime)
                return;

            if (Mathf.Abs(integrity - _lastWarningIntegrity) < 0.05f)
                return;

            _lastWarningIntegrity = integrity;
            float integrityPercent = integrity * 100f;

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.3f;
                NotificationEvents.PushWarning(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_DANGER, "BASE CRITICAL: {0}% - BREACH IMMINENT!"),
                    integrityPercent));
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= criticalThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.5f;
                NotificationEvents.PushWarning(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_CRITICAL, "HECTON-OS: MODULE INTEGRITY {0}% - REPAIRS REQUIRED."),
                    integrityPercent));
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= warningThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown;
                NotificationEvents.PushInfo(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_WARNING, "BASE MODULE: INTEGRITY {0}%."),
                    integrityPercent));
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

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }
    }
}

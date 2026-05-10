// ============================================================================
// HECTON-8 — SurvivalHUDController.cs
// HUD bars for survival stats (O2, Health, Hunger, Thirst).
//
// ARCHITECTURE:
//   • ITickable for updates (no Update)
//   • Zero GC: reads directly from HectonSurvivalSystem
//   • UI Image fill patterns for bars
//
// FEATURES:
//   • Displays O2, Health, Hunger, Thirst as fill bars
//   • Color changes at critical levels
//   • Flash effect when critical
// ============================================================================

namespace Hecton8.UI
{
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// HUD controller for survival stat bars.
    /// Reads directly from HectonSurvivalSystem. Zero GC in hot paths.
    /// </summary>
    public class SurvivalHUDController : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Bar References ─────────────────────────────")]
        [Tooltip("Oxygen bar fill image.")]
        [SerializeField] private Image oxygenBar;

        [Tooltip("Health/Integrity bar fill image.")]
        [SerializeField] private Image healthBar;

        [Tooltip("Hunger bar fill image.")]
        [SerializeField] private Image hungerBar;

        [Tooltip("Thirst bar fill image.")]
        [SerializeField] private Image thirstBar;

        [Header("── Colors ──────────────────────────────────────")]
        [Tooltip("Normal bar color.")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 1f);

        [Tooltip("Warning color (below 30%).")]
        [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0f);

        [Tooltip("Critical color (below 15%).")]
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f);

        [Header("── Oxygen Specific ────────────────────────────")]
        [SerializeField] private Color oxygenNormalColor = new Color(0f, 0.8f, 1f);
        [SerializeField] private Color oxygenWarningColor = new Color(0f, 0.5f, 1f);
        [SerializeField] private Color oxygenCriticalColor = new Color(1f, 0.2f, 0.2f);

        [Header("── Health Specific ────────────────────────────")]
        [SerializeField] private Color healthNormalColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color healthWarningColor = new Color(1f, 0.7f, 0f);
        [SerializeField] private Color healthCriticalColor = new Color(1f, 0.2f, 0.2f);

        [Header("── Hunger Specific ────────────────────────────")]
        [SerializeField] private Color hungerNormalColor = new Color(0.9f, 0.6f, 0.2f);
        [SerializeField] private Color hungerWarningColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private Color hungerCriticalColor = new Color(1f, 0.2f, 0f);

        [Header("── Thirst Specific ────────────────────────────")]
        [SerializeField] private Color thirstNormalColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] private Color thirstWarningColor = new Color(0.5f, 0.5f, 1f);
        [SerializeField] private Color thirstCriticalColor = new Color(0.8f, 0.2f, 0.8f);

        [Header("── Flash Settings ─────────────────────────────")]
        [Tooltip("Enable flashing when critical.")]
        [SerializeField] private bool enableFlash = true;

        [Tooltip("Flash speed (cycles per second).")]
        [SerializeField, Range(0.5f, 5f)] private float flashSpeed = 2f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private HectonSurvivalSystem _survivalSystem;
        private bool _registered;
        private int _nextSurvivalResolveFrame;

        // Cached previous values to avoid unnecessary UI updates
        private float _lastOxygen = -1f;
        private float _lastHealth = -1f;
        private float _lastHunger = -1f;
        private float _lastThirst = -1f;

        private float _flashTimer;
        private bool _barsForcedEmpty;

        private const float WarningThreshold = 0.3f;
        private const float CriticalThreshold = 0.15f;
        private const float Epsilon = 0.001f;
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const int SurvivalResolveRetryFrames = 30;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            ResolveSurvivalSystem(true);
            RegisterToTick();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_survivalSystem == null)
                ResolveSurvivalSystem(false);

            if (_survivalSystem == null || !_survivalSystem.IsAlive)
            {
                SetAllBarsEmpty();
                return;
            }

            _barsForcedEmpty = false;

            // Update bars only if values changed
            float oxygen = _survivalSystem.OxygenNormalized;
            float health = _survivalSystem.IntegrityNormalized;
            float hunger = _survivalSystem.HungerNormalized;
            float thirst = _survivalSystem.ThirstNormalized;

            bool shouldFlash =
                enableFlash &&
                (oxygen <= CriticalThreshold ||
                 health <= CriticalThreshold ||
                 hunger <= CriticalThreshold ||
                 thirst <= CriticalThreshold);
            float flashValue = 1f;
            if (shouldFlash)
            {
                _flashTimer += deltaTime * flashSpeed;
                flashValue = EvaluateCheapFlash01(_flashTimer * TwoPi);
            }

            if (math.abs(oxygen - _lastOxygen) > Epsilon || ShouldRefreshCriticalFlash(oxygen))
            {
                UpdateBar(oxygenBar, oxygen, oxygenNormalColor, oxygenWarningColor, oxygenCriticalColor, flashValue);
                _lastOxygen = oxygen;
            }

            if (math.abs(health - _lastHealth) > Epsilon || ShouldRefreshCriticalFlash(health))
            {
                UpdateBar(healthBar, health, healthNormalColor, healthWarningColor, healthCriticalColor, flashValue);
                _lastHealth = health;
            }

            if (math.abs(hunger - _lastHunger) > Epsilon || ShouldRefreshCriticalFlash(hunger))
            {
                UpdateBar(hungerBar, hunger, hungerNormalColor, hungerWarningColor, hungerCriticalColor, flashValue);
                _lastHunger = hunger;
            }

            if (math.abs(thirst - _lastThirst) > Epsilon || ShouldRefreshCriticalFlash(thirst))
            {
                UpdateBar(thirstBar, thirst, thirstNormalColor, thirstWarningColor, thirstCriticalColor, flashValue);
                _lastThirst = thirst;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void UpdateBar(Image bar, float normalized, Color normal, Color warning, Color critical, float flashValue)
        {
            if (bar == null)
                return;

            float safeNormalized = math.saturate(normalized);
            bar.fillAmount = safeNormalized;

            // Determine color based on level
            Color targetColor;
            if (safeNormalized <= CriticalThreshold)
            {
                // Critical - flash between critical and darker
                targetColor = LerpColorClamped(critical * 0.3f, critical, flashValue);
            }
            else if (safeNormalized <= WarningThreshold)
            {
                targetColor = warning;
            }
            else
            {
                targetColor = normal;
            }

            bar.color = targetColor;
        }

        private void SetAllBarsEmpty()
        {
            if (_barsForcedEmpty)
                return;

            if (oxygenBar != null) oxygenBar.fillAmount = 0f;
            if (healthBar != null) healthBar.fillAmount = 0f;
            if (hungerBar != null) hungerBar.fillAmount = 0f;
            if (thirstBar != null) thirstBar.fillAmount = 0f;
            _lastOxygen = 0f;
            _lastHealth = 0f;
            _lastHunger = 0f;
            _lastThirst = 0f;
            _barsForcedEmpty = true;
        }

        private static float EvaluateCheapFlash01(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return triangle * triangle;
        }

        private bool ShouldRefreshCriticalFlash(float normalized)
        {
            return enableFlash && normalized <= CriticalThreshold;
        }

        private static Color LerpColorClamped(Color from, Color to, float t)
        {
            float u = math.saturate(t);
            return new Color(
                from.r + (to.r - from.r) * u,
                from.g + (to.g - from.g) * u,
                from.b + (to.b - from.b) * u,
                from.a + (to.a - from.a) * u);
        }

        private void ResolveSurvivalSystem(bool force)
        {
            if (_survivalSystem != null)
                return;

            int frame = Time.frameCount;
            if (!force && frame < _nextSurvivalResolveFrame)
                return;

            _nextSurvivalResolveFrame = frame + SurvivalResolveRetryFrames;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.SurvivalSystem != null)
            {
                _survivalSystem = playerContext.SurvivalSystem;
                return;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null &&
                playerTransform.TryGetComponent(out HectonSurvivalSystem survival))
            {
                _survivalSystem = survival;
            }
        }

        private void RegisterToTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}

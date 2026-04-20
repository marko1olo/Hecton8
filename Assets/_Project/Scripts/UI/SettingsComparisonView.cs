using UnityEngine;
using TMPro;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Settings comparison view — shows before/after performance estimates.
    /// EXCEEDS SUBNAUTICA: Subnautica has no performance comparison, only apply/revert.
    /// Estimates FPS impact based on quality preset changes.
    /// Zero-GC: ITickable, cached strings, dirty flags.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Comparison View")]
    public sealed class SettingsComparisonView : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== COMPARISON PANEL ===")]
        [SerializeField] private CanvasGroup comparisonPanel;
        [SerializeField] private TMP_Text txtCurrentFPS;
        [SerializeField] private TMP_Text txtEstimatedFPS;
        [SerializeField] private TMP_Text txtPerformanceImpact;

        [Header("=== SETTINGS ===")]
        [SerializeField] private float updateInterval = 0.5f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private SettingsManager _settings;
        private bool _registered;
        private float _timer;
        private int _pendingGraphicsPreset = -1;
        private int _lastRenderedCurrentGraphicsPreset = -1;
        private int _lastRenderedPendingGraphicsPreset = -1;
        private float _cachedCurrentFPS;
        private float _cachedEstimatedFPS;
        private string _cachedImpactText = string.Empty;

        // COLD ALLOC: StringBuilder[128] — FPS text assembly — owner: SettingsComparisonView
        private readonly System.Text.StringBuilder _fpsBuilder = new System.Text.StringBuilder(128);

        // FPS estimates per quality level (Low/Medium/High/Ultra)
        private static readonly float[] FPSEstimates = { 60f, 50f, 40f, 30f };

        private const string NoChangeText = "No change";
        private const string BetterSuffix = " FPS (Better)";
        private const string WorseSuffix = " FPS (Worse)";

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _settings = SettingsManager.Instance;
            TryRegister();
            RefreshComparison();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            _timer += dt;
            if (_timer >= updateInterval)
            {
                _timer = 0f;
                RefreshComparison();
            }
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Update comparison with pending graphics preset.
        /// </summary>
        public void UpdateComparison(int pendingGraphicsPreset)
        {
            if (_settings == null)
                return;

            _pendingGraphicsPreset = Mathf.Clamp(pendingGraphicsPreset, 0, FPSEstimates.Length - 1);
            RefreshComparison();
        }

        /// <summary>
        /// Show comparison panel.
        /// </summary>
        public void Show()
        {
            if (comparisonPanel == null)
                return;

            comparisonPanel.alpha = 1f;
            comparisonPanel.interactable = false;
            comparisonPanel.blocksRaycasts = false;
        }

        /// <summary>
        /// Hide comparison panel.
        /// </summary>
        public void Hide()
        {
            if (comparisonPanel == null)
                return;

            comparisonPanel.alpha = 0f;
            comparisonPanel.interactable = false;
            comparisonPanel.blocksRaycasts = false;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void RefreshComparison()
        {
            if (_settings == null)
                return;

            int currentGraphicsPreset = Mathf.Clamp(_settings.GraphicsPreset, 0, FPSEstimates.Length - 1);
            int pendingGraphicsPreset = _pendingGraphicsPreset >= 0 ? _pendingGraphicsPreset : currentGraphicsPreset;

            if (_lastRenderedCurrentGraphicsPreset == currentGraphicsPreset &&
                _lastRenderedPendingGraphicsPreset == pendingGraphicsPreset)
                return;

            _lastRenderedCurrentGraphicsPreset = currentGraphicsPreset;
            _lastRenderedPendingGraphicsPreset = pendingGraphicsPreset;

            // Estimate FPS
            float currentFPS = EstimateFPS(currentGraphicsPreset);
            float estimatedFPS = EstimateFPS(pendingGraphicsPreset);

            // Update UI if changed
            if (_cachedCurrentFPS != currentFPS)
            {
                _cachedCurrentFPS = currentFPS;
                if (txtCurrentFPS != null)
                {
                    _fpsBuilder.Clear();
                    _fpsBuilder.Append(Mathf.RoundToInt(currentFPS));
                    _fpsBuilder.Append(" FPS");
                    txtCurrentFPS.SetText(_fpsBuilder);
                }
            }

            if (_cachedEstimatedFPS != estimatedFPS)
            {
                _cachedEstimatedFPS = estimatedFPS;
                if (txtEstimatedFPS != null)
                {
                    _fpsBuilder.Clear();
                    _fpsBuilder.Append(Mathf.RoundToInt(estimatedFPS));
                    _fpsBuilder.Append(" FPS");
                    txtEstimatedFPS.SetText(_fpsBuilder);
                }
            }

            // Calculate impact
            string impactText = CalculateImpactText(currentFPS, estimatedFPS);
            if (_cachedImpactText != impactText)
            {
                _cachedImpactText = impactText;
                if (txtPerformanceImpact != null)
                    txtPerformanceImpact.SetText(impactText);
            }
        }

        private static float EstimateFPS(int qualityLevel)
        {
            if (qualityLevel < 0 || qualityLevel >= FPSEstimates.Length)
                return 60f;

            return FPSEstimates[qualityLevel];
        }

        private string CalculateImpactText(float currentFPS, float estimatedFPS)
        {
            float delta = estimatedFPS - currentFPS;
            if (Mathf.Abs(delta) < 1f)
                return NoChangeText;

            _fpsBuilder.Clear();
            if (delta > 0f)
            {
                _fpsBuilder.Append('+');
                _fpsBuilder.Append(Mathf.RoundToInt(delta));
                _fpsBuilder.Append(BetterSuffix);
            }
            else
            {
                _fpsBuilder.Append(Mathf.RoundToInt(delta));
                _fpsBuilder.Append(WorseSuffix);
            }

            return _fpsBuilder.ToString();
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister(this);
            }

            _registered = false;
        }
    }
}

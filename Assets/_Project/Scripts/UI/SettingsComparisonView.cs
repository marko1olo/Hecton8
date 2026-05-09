using UnityEngine;
using TMPro;
using Hecton8.Core;
using System;

namespace Hecton8.UI
{
    /// <summary>
    /// Settings comparison view â€” shows before/after performance estimates.
    /// EXCEEDS SUBNAUTICA: Subnautica has no performance comparison, only apply/revert.
    /// Estimates FPS impact based on quality preset changes.
    /// Zero-GC: ITickable, cached strings, dirty flags.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Comparison View")]
    public sealed class SettingsComparisonView : MonoBehaviour, ITickable, IUpdatable
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("=== COMPARISON PANEL ===")]
        [SerializeField] private CanvasGroup comparisonPanel;
        [SerializeField] private TMP_Text txtCurrentFPS;
        [SerializeField] private TMP_Text txtEstimatedFPS;
        [SerializeField] private TMP_Text txtPerformanceImpact;

        [Header("=== SETTINGS ===")]
        [SerializeField] private float updateInterval = 0.5f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // FIELDS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private SettingsManager _settings;
        private bool _registered;
        private float _timer;
        private int _pendingGraphicsPreset = -1;
        private int _lastRenderedCurrentGraphicsPreset = -1;
        private int _lastRenderedPendingGraphicsPreset = -1;
        private float _cachedCurrentFPS;
        private float _cachedEstimatedFPS;
        private int _cachedImpactDelta = int.MinValue;

        private readonly char[] _currentFpsText = new char[16]; // COLD ALLOC: char[16] - current FPS TMP buffer - owner: SettingsComparisonView
        private readonly char[] _estimatedFpsText = new char[16]; // COLD ALLOC: char[16] - estimated FPS TMP buffer - owner: SettingsComparisonView
        private readonly char[] _impactText = new char[32]; // COLD ALLOC: char[32] - FPS impact TMP buffer - owner: SettingsComparisonView

        // FPS estimates per quality level (Low/Medium/High/Ultra)
        private static readonly float[] FPSEstimates = { 60f, 50f, 40f, 30f };

        private static readonly char[] FpsSuffix = { ' ', 'F', 'P', 'S' };
        private static readonly char[] NoChangeText = { 'N', 'o', ' ', 'c', 'h', 'a', 'n', 'g', 'e' };
        private static readonly char[] BetterSuffix = { ' ', 'F', 'P', 'S', ' ', '(', 'B', 'e', 't', 't', 'e', 'r', ')' };
        private static readonly char[] WorseSuffix = { ' ', 'F', 'P', 'S', ' ', '(', 'W', 'o', 'r', 's', 'e', ')' };

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnEnable()
        {
            _settings = GlobalRegistry.Settings;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_settings == null)
                Debug.LogWarning("[SettingsComparisonView] Settings runtime is not registered. Comparison panel disabled.");
#endif
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ITICKABLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float dt)
        {
            _timer += dt;
            if (_timer >= updateInterval)
            {
                _timer = 0f;
                RefreshComparison();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                    int length = WriteFpsText(Mathf.RoundToInt(currentFPS), _currentFpsText);
                    txtCurrentFPS.SetCharArray(_currentFpsText, 0, length);
                }
            }

            if (_cachedEstimatedFPS != estimatedFPS)
            {
                _cachedEstimatedFPS = estimatedFPS;
                if (txtEstimatedFPS != null)
                {
                    int length = WriteFpsText(Mathf.RoundToInt(estimatedFPS), _estimatedFpsText);
                    txtEstimatedFPS.SetCharArray(_estimatedFpsText, 0, length);
                }
            }

            // Calculate impact
            int impactDelta = CalculateImpactDelta(currentFPS, estimatedFPS);
            if (_cachedImpactDelta != impactDelta)
            {
                _cachedImpactDelta = impactDelta;
                if (txtPerformanceImpact != null)
                {
                    int length = WriteImpactText(impactDelta, _impactText);
                    txtPerformanceImpact.SetCharArray(_impactText, 0, length);
                }
            }
        }

        private static float EstimateFPS(int qualityLevel)
        {
            if (qualityLevel < 0 || qualityLevel >= FPSEstimates.Length)
                return 60f;

            return FPSEstimates[qualityLevel];
        }

        private static int CalculateImpactDelta(float currentFPS, float estimatedFPS)
        {
            float delta = estimatedFPS - currentFPS;
            if (Mathf.Abs(delta) < 1f)
                return 0;

            return Mathf.RoundToInt(delta);
        }

        private static int WriteFpsText(int fps, char[] buffer)
        {
            int cursor = 0;
            if (!fps.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int written))
                return 0;

            cursor += written;
            return AppendChars(FpsSuffix, buffer, cursor);
        }

        private static int WriteImpactText(int delta, char[] buffer)
        {
            if (delta == 0)
                return CopyChars(NoChangeText, buffer);

            int cursor = 0;
            if (delta > 0)
                buffer[cursor++] = '+';

            if (!delta.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int written))
                return 0;

            cursor += written;
            return AppendChars(delta > 0 ? BetterSuffix : WorseSuffix, buffer, cursor);
        }

        private static int CopyChars(char[] source, char[] destination)
        {
            int length = source.Length <= destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < length; i++)
                destination[i] = source[i];

            return length;
        }

        private static int AppendChars(char[] source, char[] destination, int offset)
        {
            int available = destination.Length - offset;
            int length = source.Length <= available ? source.Length : available;
            for (int i = 0; i < length; i++)
                destination[offset + i] = source[i];

            return offset + length;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}

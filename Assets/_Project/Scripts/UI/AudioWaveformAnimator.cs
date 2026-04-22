using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Lightweight waveform shim that reacts to subtitle cue changes during audio-log playback.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Audio Waveform Animator")]
    public sealed class AudioWaveformAnimator : MonoBehaviour, ITickable
    {
        [Header("References")]
        [SerializeField] private RectTransform[] waveformBars;
        [SerializeField] private TMP_Text optionalCueText;

        [Header("Animation")]
        [SerializeField, Range(0.05f, 1f)] private float idleScaleY = 0.18f;
        [SerializeField, Range(0.1f, 2f)] private float activeMinScaleY = 0.45f;
        [SerializeField, Range(0.2f, 3f)] private float activeMaxScaleY = 1.15f;
        [SerializeField, Range(0.5f, 24f)] private float waveformSpeedMin = 5f;
        [SerializeField, Range(0.5f, 24f)] private float waveformSpeedMax = 12f;
        [SerializeField, Range(1f, 20f)] private float decaySharpness = 8f;
        [SerializeField, Range(0.1f, 2f)] private float subtitleManagerPollInterval = 0.5f;

        private SubtitleManager _subtitleManager;
        private bool _tickRegistered;
        private bool _subscribed;
        private float _cueTimer;
        private float _cueDuration;
        private float _noisePhase;
        private float _amplitude;
        private float _speakerIntensity = 1f;
        private float _pollTimer;
        private int _cueSeed;
        private RectTransform _selfRect;
        private float[] _baseScaleX;
        private float[] _baseScaleZ;

        /// <summary>
        /// Injects runtime-created waveform bars and optional cue text without requiring scene-side wiring.
        /// </summary>
        public void ConfigureWaveformTargets(RectTransform[] bars, TMP_Text cueText = null)
        {
            waveformBars = bars;
            optionalCueText = cueText;
            _selfRect = transform as RectTransform;
            EnsureWaveformTargets();
            ApplyIdlePose();
        }

        private void Awake()
        {
            EnsureWaveformTargets();
        }

        private void OnEnable()
        {
            EnsureWaveformTargets();
            TrySubscribeToSubtitleManager();
            RegisterToTickManager();
            ApplyIdlePose();
        }

        private void OnDisable()
        {
            UnsubscribeFromSubtitleManager();
            UnregisterFromTickManager();
            ApplyIdlePose();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_subscribed)
            {
                _pollTimer -= deltaTime;
                if (_pollTimer <= 0f)
                {
                    _pollTimer = subtitleManagerPollInterval;
                    TrySubscribeToSubtitleManager();
                }
            }

            float targetAmplitude = _cueTimer > 0f ? Mathf.Lerp(0.22f, 1f, _speakerIntensity) : 0f;
            if (_cueTimer > 0f)
            {
                _cueTimer -= deltaTime;
                _noisePhase += deltaTime * Mathf.Lerp(waveformSpeedMin, waveformSpeedMax, targetAmplitude);
                _amplitude = Mathf.Lerp(_amplitude, targetAmplitude, 1f - Mathf.Exp(-decaySharpness * deltaTime));
            }
            else
            {
                _amplitude = 0f;
            }

            ApplyWaveformPose();
        }

        private void HandleCueChanged(float duration, string text, float speakerIntensity)
        {
            _cueDuration = Mathf.Max(0f, duration);
            _cueTimer = _cueDuration;
            _cueSeed = string.IsNullOrEmpty(text) ? 0 : text.GetHashCode();
            _speakerIntensity = Mathf.Clamp01(speakerIntensity);
            _amplitude = _cueDuration > 0f ? Mathf.Lerp(0.22f, 1f, _speakerIntensity) : 0f;
            if (_cueDuration <= 0f)
            {
                _noisePhase = 0f;
                _speakerIntensity = 0f;
            }

            if (optionalCueText != null && !string.Equals(optionalCueText.text, text, System.StringComparison.Ordinal))
                optionalCueText.text = text ?? string.Empty;
        }

        private void EnsureWaveformTargets()
        {
            if (waveformBars != null && waveformBars.Length > 0)
            {
                EnsureBaseScaleBuffers(waveformBars.Length);
                return;
            }

            _selfRect = transform as RectTransform;
            if (_selfRect == null)
                return;

            waveformBars = new[] { _selfRect }; // COLD ALLOC: RectTransform[1] — waveform fallback target — owner: AudioWaveformAnimator
            EnsureBaseScaleBuffers(1);
        }

        private void EnsureBaseScaleBuffers(int count)
        {
            if (_baseScaleX != null && _baseScaleX.Length == count &&
                _baseScaleZ != null && _baseScaleZ.Length == count)
            {
                return;
            }

            _baseScaleX = new float[count]; // COLD ALLOC: float[count] — waveform X scale cache — owner: AudioWaveformAnimator
            _baseScaleZ = new float[count]; // COLD ALLOC: float[count] — waveform Z scale cache — owner: AudioWaveformAnimator
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = waveformBars[i];
                if (rect == null)
                    continue;

                Vector3 localScale = rect.localScale;
                _baseScaleX[i] = Mathf.Approximately(localScale.x, 0f) ? 1f : localScale.x;
                _baseScaleZ[i] = Mathf.Approximately(localScale.z, 0f) ? 1f : localScale.z;
            }
        }

        private void ApplyIdlePose()
        {
            if (waveformBars == null)
                return;

            for (int i = 0; i < waveformBars.Length; i++)
            {
                RectTransform rect = waveformBars[i];
                if (rect == null)
                    continue;

                rect.localScale = new Vector3(_baseScaleX[i], idleScaleY, _baseScaleZ[i]);
            }
        }

        private void ApplyWaveformPose()
        {
            if (waveformBars == null)
                return;

            float liveBlend = Mathf.Clamp01(_amplitude);
            for (int i = 0; i < waveformBars.Length; i++)
            {
                RectTransform rect = waveformBars[i];
                if (rect == null)
                    continue;

                float noise = Mathf.PerlinNoise((i + 1) * 0.37f + (_cueSeed & 255) * 0.001f, _noisePhase + i * 0.21f);
                float intensityBlend = Mathf.Lerp(0.2f, 1f, _speakerIntensity);
                float activeMin = Mathf.Lerp(idleScaleY, activeMinScaleY, intensityBlend);
                float activeMax = Mathf.Lerp(Mathf.Max(idleScaleY, activeMinScaleY), activeMaxScaleY, intensityBlend);
                float targetY = Mathf.Lerp(idleScaleY, Mathf.Lerp(activeMin, activeMax, noise), liveBlend);
                rect.localScale = new Vector3(_baseScaleX[i], targetY, _baseScaleZ[i]);
            }
        }

        private void TrySubscribeToSubtitleManager()
        {
            SubtitleManager manager = SubtitleManager.Instance;
            if (manager == null)
                return;

            if (ReferenceEquals(_subtitleManager, manager) && _subscribed)
                return;

            UnsubscribeFromSubtitleManager();
            _subtitleManager = manager;
            _subtitleManager.OnCueChanged += HandleCueChanged;
            _subscribed = true;
        }

        private void UnsubscribeFromSubtitleManager()
        {
            if (_subtitleManager != null && _subscribed)
                _subtitleManager.OnCueChanged -= HandleCueChanged;

            _subtitleManager = null;
            _subscribed = false;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _tickRegistered = false;
        }
    }
}

using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Lightweight waveform shim that reacts to subtitle cue changes during audio-log playback.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Audio Waveform Animator")]
    public sealed class AudioWaveformAnimator : MonoBehaviour, ITickable, IUpdatable
    {
        private const int MaxCueTextChars = 1024;
        private const float AmplitudeIdleEpsilon = 0.001f;

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
        private int _optionalCueTextLength = -1;
        private RectTransform _selfRect;
        private float[] _baseScaleX;
        private float[] _baseScaleZ;
        private readonly char[] _optionalCueTextCache = new char[MaxCueTextChars]; // COLD ALLOC: char[1024] - optional waveform cue text cache for zero-GC TMP updates - owner: AudioWaveformAnimator

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
            ApplyIdlePose();
            RefreshTickRegistration();
        }

        private void OnDisable()
        {
            UnsubscribeFromSubtitleManager();
            UnregisterFromTickManager();
            _cueTimer = 0f;
            _cueDuration = 0f;
            _amplitude = 0f;
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

            if (_cueTimer <= 0f)
            {
                if (_amplitude > AmplitudeIdleEpsilon)
                {
                    _amplitude = 0f;
                    ApplyIdlePose();
                }

                RefreshTickRegistration();
                return;
            }

            float targetAmplitude = math.lerp(0.22f, 1f, math.saturate(_speakerIntensity));
            _cueTimer = math.max(0f, _cueTimer - math.max(0f, deltaTime));
            _noisePhase += deltaTime * math.lerp(waveformSpeedMin, waveformSpeedMax, targetAmplitude);
            _amplitude = math.lerp(_amplitude, targetAmplitude, FastDecayBlend(decaySharpness, deltaTime));

            ApplyWaveformPose();
            if (_cueTimer <= 0f)
            {
                _amplitude = 0f;
                ApplyIdlePose();
                RefreshTickRegistration();
            }
        }

        private void HandleCueChanged(float duration, char[] textBuffer, int textStart, int textLength, float speakerIntensity)
        {
            _cueDuration = math.max(0f, duration);
            _cueTimer = _cueDuration;
            _cueSeed = ComputeCueSeed(textBuffer, textStart, textLength);
            _speakerIntensity = math.saturate(speakerIntensity);
            _amplitude = _cueDuration > 0f ? math.lerp(0.22f, 1f, _speakerIntensity) : 0f;
            if (_cueDuration <= 0f)
            {
                _noisePhase = 0f;
                _speakerIntensity = 0f;
            }

            ApplyOptionalCueText(textBuffer, textStart, textLength);
            RefreshTickRegistration();
        }

        private static float FastDecayBlend(float sharpness, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;

            float x = math.max(0f, sharpness) * deltaTime;
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
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

            float liveBlend = math.saturate(_amplitude);
            for (int i = 0; i < waveformBars.Length; i++)
            {
                RectTransform rect = waveformBars[i];
                if (rect == null)
                    continue;

                float noise = ResolveWaveformNoise(i, _cueSeed, _noisePhase);
                float intensityBlend = math.lerp(0.2f, 1f, math.saturate(_speakerIntensity));
                float activeMin = math.lerp(idleScaleY, activeMinScaleY, intensityBlend);
                float activeMax = math.lerp(math.max(idleScaleY, activeMinScaleY), activeMaxScaleY, intensityBlend);
                float targetY = math.lerp(idleScaleY, math.lerp(activeMin, activeMax, noise), liveBlend);
                rect.localScale = new Vector3(_baseScaleX[i], targetY, _baseScaleZ[i]);
            }
        }

        private static float ResolveWaveformNoise(int barIndex, int cueSeed, float noisePhase)
        {
            float sample = (noisePhase * 8f) + (barIndex * 1.73f) + ((cueSeed & 255) * 0.013f);
            float frame = math.floor(sample);
            float blend = SmoothStep01(math.frac(sample));
            float hashA = Hash01(frame, cueSeed + barIndex * 37);
            float hashB = Hash01(frame + 1f, cueSeed + barIndex * 37);
            return math.lerp(hashA, hashB, blend);
        }

        private static float SmoothStep01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        private static float Hash01(float x, int seed)
        {
            return math.frac(52.9829189f * math.frac(math.dot(new float2(x, seed), new float2(0.06711056f, 0.00583715f))));
        }

        private void TrySubscribeToSubtitleManager()
        {
            SubtitleManager manager = GlobalRegistry.Subtitles;
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

        private void RefreshTickRegistration()
        {
            if (!isActiveAndEnabled)
            {
                UnregisterFromTickManager();
                return;
            }

            if (!_subscribed || _cueTimer > 0f || _amplitude > AmplitudeIdleEpsilon)
            {
                RegisterToTickManager();
                return;
            }

            UnregisterFromTickManager();
        }

        private void ApplyOptionalCueText(char[] textBuffer, int textStart, int textLength)
        {
            if (optionalCueText == null)
                return;

            int safeStart = textBuffer == null ? 0 : Mathf.Clamp(textStart, 0, textBuffer.Length);
            int safeLength = textBuffer == null
                ? 0
                : Mathf.Clamp(textLength, 0, Mathf.Min(textBuffer.Length - safeStart, _optionalCueTextCache.Length));
            if (_optionalCueTextLength == safeLength &&
                BufferMatches(textBuffer, safeStart, _optionalCueTextCache, safeLength))
            {
                return;
            }

            for (int i = 0; i < safeLength; i++)
                _optionalCueTextCache[i] = textBuffer[safeStart + i];

            _optionalCueTextLength = safeLength;
            optionalCueText.SetCharArray(
                textBuffer ?? System.Array.Empty<char>(),
                safeStart,
                safeLength);
        }

        private static bool BufferMatches(char[] source, int sourceStart, char[] cached, int length)
        {
            if (source == null)
                return length == 0;

            for (int i = 0; i < length; i++)
            {
                if (source[sourceStart + i] != cached[i])
                    return false;
            }

            return true;
        }

        private static int ComputeCueSeed(char[] textBuffer, int textStart, int textLength)
        {
            if (textBuffer == null || textLength <= 0)
                return 0;

            int safeStart = Mathf.Clamp(textStart, 0, textBuffer.Length);
            int safeLength = Mathf.Clamp(textLength, 0, textBuffer.Length - safeStart);
            unchecked
            {
                int seed = 17;
                for (int i = 0; i < safeLength; i++)
                    seed = (seed * 31) + textBuffer[safeStart + i];

                return seed;
            }
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }
    }
}

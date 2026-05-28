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
    public sealed class AudioWaveformAnimator : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxCueTextChars = 1024;
        private const int MaxWaveformBars = 4;
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
        private SubtitleManager _cachedSubtitleManager;
        private bool _tickRegistered;
        private bool _hasSubtitleManager;
        private bool _hotSwapRegistered;
        private float _cueTimer;
        private float _cueDuration;
        private float _noisePhase;
        private float _amplitude;
        private float _speakerIntensity = 1f;
        private float _pollTimer;
        private int _cueSeed;
        private int _lastCueChangeVersion;
        private int _optionalCueTextLength = -1;
        private RectTransform _selfRect;
        private int _waveformBarCount;
        private readonly RectTransform[] _runtimeWaveformBars = new RectTransform[MaxWaveformBars]; // COLD ALLOC: RectTransform[4] - fixed waveform target cache - owner: AudioWaveformAnimator
        private readonly float[] _baseScaleX = new float[MaxWaveformBars]; // COLD ALLOC: float[4] - waveform X scale cache - owner: AudioWaveformAnimator
        private readonly float[] _baseScaleZ = new float[MaxWaveformBars]; // COLD ALLOC: float[4] - waveform Z scale cache - owner: AudioWaveformAnimator
        private readonly char[] _optionalCueTextCache = new char[MaxCueTextChars]; // COLD ALLOC: char[1024] - optional waveform cue text cache for zero-GC TMP updates - owner: AudioWaveformAnimator

        /// <summary>
        /// Injects runtime-created waveform bars and optional cue text without requiring scene-side wiring.
        /// </summary>
        public void ConfigureWaveformTargets(RectTransform[] bars, TMP_Text cueText = null)
        {
            waveformBars = bars;
            optionalCueText = cueText;
            _waveformBarCount = 0;
            _selfRect = transform as RectTransform;
            EnsureWaveformTargets();
            ApplyIdlePose();
        }

        public void ConfigureWaveformTargets(
            RectTransform bar0,
            RectTransform bar1,
            RectTransform bar2,
            RectTransform bar3,
            TMP_Text cueText = null)
        {
            waveformBars = null;
            optionalCueText = cueText;
            _selfRect = transform as RectTransform;
            _runtimeWaveformBars[0] = bar0;
            _runtimeWaveformBars[1] = bar1;
            _runtimeWaveformBars[2] = bar2;
            _runtimeWaveformBars[3] = bar3;
            _waveformBarCount = CountNonNullWaveformBars();
            CacheBaseScales(_waveformBarCount);
            ApplyIdlePose();
        }

        private void Awake()
        {
            EnsureWaveformTargets();
        }

        private void OnEnable()
        {
            EnsureWaveformTargets();
            CacheSubtitleManagerCold();
            TryRegisterHotSwapListener();
            TryBindSubtitleManager();
            ApplyIdlePose();
            RefreshTickRegistration();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            ClearSubtitleManagerBinding();
            UnregisterFromTickManager();
            _cueTimer = 0f;
            _cueDuration = 0f;
            _amplitude = 0f;
            ApplyIdlePose();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.SubtitleRuntime)
                return;

            _cachedSubtitleManager = currentService as SubtitleManager;
            if (_cachedSubtitleManager == null)
            {
                ClearSubtitleManagerBinding();
                RefreshTickRegistration();
                return;
            }

            TryBindSubtitleManager();
            RefreshTickRegistration();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (!_hasSubtitleManager)
            {
                _pollTimer -= deltaTime;
                if (_pollTimer <= 0f)
                {
                    _pollTimer = subtitleManagerPollInterval;
                    TryBindSubtitleManager();
                }
            }

            ConsumeCueSnapshot();

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

        private void ConsumeCueSnapshot()
        {
            SubtitleManager manager = _subtitleManager;
            if (manager == null)
                return;

            if (!manager.TryGetAudioLogCueSnapshot(
                    _lastCueChangeVersion,
                    out int version,
                    out float duration,
                    out char[] textBuffer,
                    out int textStart,
                    out int textLength,
                    out float speakerIntensity))
            {
                return;
            }

            _lastCueChangeVersion = version;
            HandleCueChanged(duration, textBuffer, textStart, textLength, speakerIntensity);
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
            if (_waveformBarCount > 0)
            {
                CacheBaseScales(_waveformBarCount);
                return;
            }

            if (waveformBars != null && waveformBars.Length > 0)
            {
                _waveformBarCount = math.min(waveformBars.Length, MaxWaveformBars);
                for (int i = 0; i < _waveformBarCount; i++)
                    _runtimeWaveformBars[i] = waveformBars[i];

                for (int i = _waveformBarCount; i < MaxWaveformBars; i++)
                    _runtimeWaveformBars[i] = null;

                CacheBaseScales(_waveformBarCount);
                return;
            }

            _selfRect = transform as RectTransform;
            if (_selfRect == null)
                return;

            _runtimeWaveformBars[0] = _selfRect;
            for (int i = 1; i < MaxWaveformBars; i++)
                _runtimeWaveformBars[i] = null;

            _waveformBarCount = 1;
            CacheBaseScales(1);
        }

        private int CountNonNullWaveformBars()
        {
            int count = 0;
            for (int i = 0; i < MaxWaveformBars; i++)
            {
                if (_runtimeWaveformBars[i] != null)
                    count = i + 1;
            }

            return count;
        }

        private void CacheBaseScales(int count)
        {
            int safeCount = math.clamp(count, 0, MaxWaveformBars);
            for (int i = 0; i < safeCount; i++)
            {
                RectTransform rect = _runtimeWaveformBars[i];
                if (rect == null)
                {
                    _baseScaleX[i] = 1f;
                    _baseScaleZ[i] = 1f;
                    continue;
                }

                Vector3 localScale = rect.localScale;
                _baseScaleX[i] = Mathf.Approximately(localScale.x, 0f) ? 1f : localScale.x;
                _baseScaleZ[i] = Mathf.Approximately(localScale.z, 0f) ? 1f : localScale.z;
            }

            for (int i = safeCount; i < MaxWaveformBars; i++)
            {
                _baseScaleX[i] = 1f;
                _baseScaleZ[i] = 1f;
            }
        }

        private void ApplyIdlePose()
        {
            if (_waveformBarCount <= 0)
                return;

            for (int i = 0; i < _waveformBarCount; i++)
            {
                RectTransform rect = _runtimeWaveformBars[i];
                if (rect == null)
                    continue;

                rect.localScale = new Vector3(_baseScaleX[i], idleScaleY, _baseScaleZ[i]);
            }
        }

        private void ApplyWaveformPose()
        {
            if (_waveformBarCount <= 0)
                return;

            float liveBlend = math.saturate(_amplitude);
            for (int i = 0; i < _waveformBarCount; i++)
            {
                RectTransform rect = _runtimeWaveformBars[i];
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

        private void TryBindSubtitleManager()
        {
            SubtitleManager manager = _cachedSubtitleManager;
            if (manager == null)
                return;

            if (ReferenceEquals(_subtitleManager, manager) && _hasSubtitleManager)
                return;

            ClearSubtitleManagerBinding();
            _subtitleManager = manager;
            _hasSubtitleManager = true;
            _lastCueChangeVersion = 0;
        }

        private void CacheSubtitleManagerCold()
        {
            _cachedSubtitleManager = GlobalRegistry.Subtitles;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ClearSubtitleManagerBinding()
        {
            _subtitleManager = null;
            _hasSubtitleManager = false;
            _lastCueChangeVersion = 0;
        }

        private void RefreshTickRegistration()
        {
            if (!isActiveAndEnabled)
            {
                UnregisterFromTickManager();
                return;
            }

            RegisterToTickManager();
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
                _optionalCueTextCache,
                0,
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

            _tickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }
    }
}

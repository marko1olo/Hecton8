using System;
using Hecton8.Core;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// Event-driven localized text bridge for world-space signs and authored TMP labels.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Localization/Localized World Sign")]
    public sealed class LocalizedWorldSign : MonoBehaviour, ILocalizationLanguageChangedListener, IOriginShiftListener
    {
        [Header("── References ───────────────────────────────────────────────")]
        [Tooltip("Target TMP text owner. Defaults to TMP_Text on the same GameObject.")]
        [SerializeField] private TMP_Text targetText;

        [Header("── Localization ─────────────────────────────────────────────")]
        [Tooltip("Localization table key resolved through LocalizationManager.")]
        [SerializeField] private string tableKey;

        [Tooltip("Fallback text used when the table key is missing.")]
        [SerializeField] private string fallbackText;

        [Tooltip("For signage that should stay in all-caps regardless of language.")]
        [SerializeField] private bool forceUppercase = true;

        private const int DefaultSignBufferCapacity = 64;

        private Transform _cachedTransform;
        private Vector3 _absoluteUniversePosition;
        private double3 _absoluteUniversePositionDouble;
        private char[] _fallbackBuffer;
        private char[] _signBuffer;
        private int _tableKeyHash;
        private int _fallbackLength;
        private bool _hasAupPosition;

        private void Awake()
        {
            ResolveTargetText();
            CacheLocalizationBuffers();
            CacheTransformAndAup();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            HectonFloatingOrigin.RegisterListener(this);
            CacheLocalizationBuffers();
            CacheTransformAndAup();
            RefreshLocalizedText();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            HectonFloatingOrigin.UnregisterListener(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTargetText();
            CacheLocalizationBuffers();
            if (!Application.isPlaying)
                RefreshLocalizedText();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshLocalizedText();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_hasAupPosition)
                CacheTransformAndAup();

            if (_cachedTransform != null && _hasAupPosition)
                _cachedTransform.position = shiftData.ToRuntimePosition(_absoluteUniversePositionDouble);

            if (targetText != null)
            {
                targetText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
                targetText.SetVerticesDirty();
                targetText.SetLayoutDirty();
            }
        }

        private void RefreshLocalizedText()
        {
            if (targetText == null)
                return;

            char[] sourceBuffer = null;
            int sourceLength = 0;
            bool found = _tableKeyHash != 0 &&
                         LocRegistry.TryGetVisualBufferFromUtf8(_tableKeyHash, out sourceBuffer, out sourceLength);
            if (!found)
            {
                sourceBuffer = EnsureFallbackBuffer();
                sourceLength = _fallbackLength;
            }

            PrepareDisplayBuffer(sourceBuffer, sourceLength, found, out char[] displayBuffer, out int displayLength);
            targetText.isRightToLeftText = false;
            targetText.SetCharArray(displayBuffer, 0, displayLength);
            targetText.SetVerticesDirty();
            targetText.SetLayoutDirty();
        }

        private void ResolveTargetText()
        {
            if (targetText == null)
                TryGetComponent(out targetText);
        }

        private void CacheLocalizationBuffers()
        {
            _tableKeyHash = string.IsNullOrWhiteSpace(tableKey) ? 0 : LocHash.Compute(tableKey);
            ReadOnlySpan<char> fallback;
            if (!string.IsNullOrWhiteSpace(fallbackText))
                fallback = fallbackText.AsSpan();
            else if (!string.IsNullOrWhiteSpace(tableKey))
                fallback = tableKey.AsSpan();
            else
                fallback = ReadOnlySpan<char>.Empty;

            _fallbackBuffer = EnsureBuffer(_fallbackBuffer, fallback.Length);
            fallback.CopyTo(_fallbackBuffer);
            _fallbackLength = fallback.Length;
        }

        private char[] EnsureFallbackBuffer()
        {
            if (_fallbackBuffer == null)
                CacheLocalizationBuffers();

            _fallbackBuffer = EnsureBuffer(_fallbackBuffer, _fallbackLength);
            return _fallbackBuffer;
        }

        private void PrepareDisplayBuffer(
            char[] sourceBuffer,
            int sourceLength,
            bool sourceAlreadyBabelVisual,
            out char[] displayBuffer,
            out int displayLength)
        {
            displayLength = sourceLength < 0 ? 0 : sourceLength;
            if (sourceBuffer != null && displayLength > sourceBuffer.Length)
                displayLength = sourceBuffer.Length;

            bool needsCopy = forceUppercase ||
                             (!sourceAlreadyBabelVisual && LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage));
            if (!needsCopy)
            {
                if (sourceBuffer == null)
                    _signBuffer = EnsureBuffer(_signBuffer, 1);

                displayBuffer = sourceBuffer ?? _signBuffer;
                return;
            }

            _signBuffer = EnsureBuffer(_signBuffer, displayLength);
            for (int i = 0; i < displayLength; i++)
            {
                char current = sourceBuffer != null && i < sourceBuffer.Length ? sourceBuffer[i] : '\0';
                _signBuffer[i] = forceUppercase ? char.ToUpperInvariant(current) : current;
            }

            if (!sourceAlreadyBabelVisual && LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage))
                RTLProcessor.TryReverseVisualOrderInPlace(_signBuffer, displayLength);

            displayBuffer = _signBuffer;
        }

        private static char[] EnsureBuffer(char[] buffer, int requiredLength)
        {
            int required = requiredLength <= 0 ? 1 : requiredLength;
            if (buffer != null && buffer.Length >= required)
                return buffer;

            int capacity = DefaultSignBufferCapacity;
            while (capacity < required)
                capacity <<= 1;

            return new char[capacity]; // COLD ALLOC: char[capacity] - localized world sign fallback/display buffer - owner: LocalizedWorldSign
        }

        private void CacheTransformAndAup()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_cachedTransform == null)
            {
                _hasAupPosition = false;
                return;
            }

            if (!TryResolveAbsoluteAupFromRuntimeOrigin(_cachedTransform.position, out _absoluteUniversePositionDouble))
            {
                _hasAupPosition = false;
                return;
            }

            _absoluteUniversePosition = new Vector3(
                (float)_absoluteUniversePositionDouble.x,
                (float)_absoluteUniversePositionDouble.y,
                (float)_absoluteUniversePositionDouble.z);
            _hasAupPosition = true;
        }

        private static bool TryResolveAbsoluteAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = originAup.ToAbsoluteDouble3() + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(absoluteAup));
        }
    }
}

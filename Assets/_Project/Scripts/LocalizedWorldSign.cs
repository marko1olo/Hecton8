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

        private const int DefaultSignBufferCapacity = 128;
        private const int EllipsisWidth = 3;

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

            PrepareDisplayBuffer(sourceBuffer, sourceLength, out char[] displayBuffer, out int displayLength);
            targetText.richText = Hecton8.UI.BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();
            targetText.isRightToLeftText = LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage);
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

            _fallbackBuffer = EnsureBuffer(_fallbackBuffer);
            _fallbackLength = CopySpanFailClosed(fallback, _fallbackBuffer, _tableKeyHash);
        }

        private char[] EnsureFallbackBuffer()
        {
            if (_fallbackBuffer == null)
                CacheLocalizationBuffers();

            return _fallbackBuffer;
        }

        private void PrepareDisplayBuffer(
            char[] sourceBuffer,
            int sourceLength,
            out char[] displayBuffer,
            out int displayLength)
        {
            int sourceCapacity = sourceBuffer != null ? sourceBuffer.Length : 0;
            int safeLength = math.clamp(sourceLength, 0, sourceCapacity);
            bool needsTruncation = safeLength > DefaultSignBufferCapacity;
            displayLength = needsTruncation ? DefaultSignBufferCapacity : safeLength;

            bool needsCopy = forceUppercase || needsTruncation || sourceBuffer == null;
            if (!needsCopy)
            {
                displayBuffer = sourceBuffer;
                return;
            }

            _signBuffer = EnsureBuffer(_signBuffer);
            int copyLimit = needsTruncation && DefaultSignBufferCapacity > EllipsisWidth
                ? DefaultSignBufferCapacity - EllipsisWidth
                : displayLength;
            int cursor = 0;
            for (; cursor < copyLimit; cursor++)
            {
                char current = sourceBuffer != null && cursor < sourceBuffer.Length ? sourceBuffer[cursor] : '\0';
                _signBuffer[cursor] = forceUppercase ? char.ToUpperInvariant(current) : current;
            }

            if (needsTruncation)
            {
                AppendAsciiEllipsis(_signBuffer, ref cursor);
                Hecton8.UI.BabelSubtitleSyncRuntime.RecordUIOptimizationFailure(
                    unchecked((uint)_tableKeyHash),
                    Hecton8.UI.UIOptimizationFailureCode.TextBufferOverflow,
                    safeLength,
                    cursor,
                    DefaultSignBufferCapacity,
                    0u);
            }

            displayLength = cursor;
            displayBuffer = _signBuffer;
        }

        private static char[] EnsureBuffer(char[] buffer)
        {
            if (buffer != null && buffer.Length == DefaultSignBufferCapacity)
                return buffer;

            return new char[DefaultSignBufferCapacity]; // COLD ALLOC: char[128] - fixed localized world sign staging buffer - owner: LocalizedWorldSign
        }

        private static int CopySpanFailClosed(ReadOnlySpan<char> source, char[] destination, int keyHash)
        {
            int safeLength = math.min(source.Length, destination.Length);
            int copyLimit = source.Length > destination.Length && destination.Length > EllipsisWidth
                ? destination.Length - EllipsisWidth
                : safeLength;

            for (int i = 0; i < copyLimit; i++)
                destination[i] = source[i];

            int cursor = copyLimit;
            if (source.Length > destination.Length)
            {
                AppendAsciiEllipsis(destination, ref cursor);
                Hecton8.UI.BabelSubtitleSyncRuntime.RecordUIOptimizationFailure(
                    unchecked((uint)keyHash),
                    Hecton8.UI.UIOptimizationFailureCode.TextBufferOverflow,
                    source.Length,
                    cursor,
                    destination.Length,
                    0u);
            }

            return cursor;
        }

        private static void AppendAsciiEllipsis(char[] destination, ref int cursor)
        {
            int capacity = destination != null ? destination.Length : 0;
            if (capacity <= 0)
            {
                cursor = 0;
                return;
            }

            int ellipsisCount = math.min(EllipsisWidth, capacity);
            int start = capacity - ellipsisCount;
            cursor = math.clamp(cursor, 0, start);
            for (int i = 0; i < ellipsisCount; i++)
                destination[cursor++] = '.';
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

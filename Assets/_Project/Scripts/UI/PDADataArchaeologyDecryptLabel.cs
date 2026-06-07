using System;
using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Hash-bound PDA label for scanner archaeology names. Writes TMP text from pooled char buffers only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PDADataArchaeologyDecryptLabel : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int RevealBucketCount = 64;
        private const float ScrambleSpeed = 37f;
        private static readonly char[] EmptyText = Array.Empty<char>();

        [Header("Data Archaeology")]
        [Tooltip("TMP label that receives hash-resolved scanner text via SetCharArray.")]
        [SerializeField] private TMP_Text targetText;

        private uint _entityHash;
        private float _progress01;
        private float _scramblePhase;
        private int _lastHash;
        private int _lastProgressBucket = -1;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _dirty;
        private float _scrambleIntensity01 = 1f;

        /// <summary>
        /// Binds this PDA label to a scanner entity hash and progress value.
        /// </summary>
        public void Bind(uint entityHash, float progress01)
        {
            float clampedProgress = math.saturate(progress01);
            if (_entityHash == entityHash && math.abs(_progress01 - clampedProgress) <= 0.0001f)
                return;

            if (entityHash == 0u)
            {
                Clear();
                return;
            }

            _entityHash = entityHash;
            _progress01 = clampedProgress;
            _dirty = true;
        }

        private void Awake()
        {
            if (targetText == null)
                TryGetComponent(out targetText);

            if (targetText != null)
            {
                targetText.richText = false;
                targetText.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            RefreshCachedQualityWeight();
            TryRegisterHotSwapListener();
            TryRegister();
            _dirty = true;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            Unregister();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (targetText == null || _entityHash == 0u)
                return;

            RefreshCachedQualityWeight();
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            float scrambleIntensity01 = ShouldScramble(_progress01) ? _scrambleIntensity01 : 0f;
            _scramblePhase += deltaTime * ScrambleSpeed * scrambleIntensity01;
            int hash = unchecked((int)_entityHash);
            int progressBucket = (int)math.floor(_progress01 * RevealBucketCount);
            bool scrambleAnimating = scrambleIntensity01 > 0.001f;
            if (!_dirty && _lastHash == hash && _lastProgressBucket == progressBucket && !scrambleAnimating)
                return;

            if (!RenderHash(hash, scrambleIntensity01))
            {
                _dirty = true;
                return;
            }

            _lastHash = hash;
            _lastProgressBucket = progressBucket;
            _dirty = false;

            if (!scrambleAnimating && _progress01 >= 0.999f)
                _dirty = false;
        }

        private bool RenderHash(int hash, float scrambleIntensity01)
        {
            if (!LocRegistry.TryGetVisualBuffer(hash, out char[] source, out int length))
            {
                if (source == null || length <= 0)
                    return false;
            }

            int sourceCapacity = source != null ? source.Length : 0;
            int writeLength = math.min(math.min(length, sourceCapacity), CharBufferPool.SlotCapacity);
            if (writeLength <= 0 || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return false;

            try
            {
                Span<char> destination = lease.Buffer.AsSpan(0, writeLength);
                ReadOnlySpan<char> sourceSpan = source.AsSpan(0, writeLength);
                Scramble(sourceSpan, destination, _entityHash, _progress01, _scramblePhase, scrambleIntensity01);

                targetText.richText = false;
                targetText.isRightToLeftText = LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage);
                targetText.SetCharArray(lease.Buffer, 0, writeLength);
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }

            return true;
        }

        private static void Scramble(
            ReadOnlySpan<char> source,
            Span<char> destination,
            uint hash,
            float progress01,
            float phase,
            float scrambleIntensity01)
        {
            float effectiveReveal01 = math.lerp(1f, math.saturate(progress01), Sanitize01(scrambleIntensity01));
            int revealCount = math.clamp((int)math.floor(source.Length * effectiveReveal01), 0, source.Length);
            for (int i = 0; i < revealCount; i++)
                destination[i] = source[i];

            uint seed = hash ^ (uint)math.max(1, (int)phase);
            for (int i = revealCount; i < source.Length; i++)
            {
                seed = (seed * LocHash.FnvPrime) ^ (uint)(i + 17);
                destination[i] = source[i] == ' ' ? ' ' : (char)('A' + (seed % 26u));
            }
        }

        private static bool ShouldScramble(float progress01)
        {
            return progress01 < 0.999f;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            Unregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        private bool RefreshCachedQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            float scaled = math.saturate((quality - 0.2f) * 1.25f);
            float nextScrambleIntensity01 = SmoothStep01(scaled);
            if (math.abs(nextScrambleIntensity01 - _scrambleIntensity01) <= 0.001f)
                return false;

            _scrambleIntensity01 = nextScrambleIntensity01;
            _dirty = true;
            return true;
        }

        private static float SmoothStep01(float value)
        {
            float x = Sanitize01(value);
            return x * x * (3f - 2f * x);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }

        private void Clear()
        {
            _entityHash = 0u;
            _progress01 = 0f;
            _scramblePhase = 0f;
            _lastHash = 0;
            _lastProgressBucket = -1;
            _dirty = false;
            _scrambleIntensity01 = 0f;

            if (targetText != null)
            {
                targetText.richText = false;
                targetText.SetCharArray(EmptyText, 0, 0);
            }
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
    }
}

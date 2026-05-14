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
    public sealed class PDADataArchaeologyDecryptLabel : MonoBehaviour, ILateFrameTickable
    {
        private const int RevealBucketCount = 64;
        private const float ScrambleSpeed = 37f;
        private const float ScrambleTierProbeIntervalSeconds = 0.5f;
        private const float ScrambleTierHysteresisSeconds = 2f;
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
        private bool _dirty;
        private bool _scrambleTierInitialized;
        private bool _scrambleAllowed;
        private bool _scrambleCandidate;
        private float _scrambleProbeCountdown;
        private float _scrambleCandidateAge;

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
            _scrambleProbeCountdown = 0f;
            TryRegister();
        }

        private void Awake()
        {
            if (targetText == null)
                TryGetComponent(out targetText);

            if (targetText != null)
                targetText.raycastTarget = false;
        }

        private void OnEnable()
        {
            TryRegister();
            _dirty = true;
        }

        private void OnDisable()
        {
            Unregister();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (targetText == null || _entityHash == 0u)
            {
                Unregister();
                return;
            }

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            RefreshScrambleAllowed(deltaTime);
            bool scramble = _scrambleAllowed && ShouldScramble(_progress01);
            _scramblePhase += deltaTime * ScrambleSpeed;
            int hash = unchecked((int)_entityHash);
            int progressBucket = (int)math.floor(_progress01 * RevealBucketCount);
            if (!_dirty && _lastHash == hash && _lastProgressBucket == progressBucket && !scramble)
                return;

            if (!RenderHash(hash, scramble))
            {
                _dirty = true;
                return;
            }

            _lastHash = hash;
            _lastProgressBucket = progressBucket;
            _dirty = false;

            if (!scramble && _progress01 >= 0.999f)
                Unregister();
        }

        private bool RenderHash(int hash, bool scramble)
        {
            int sourceLength = LocRegistry.GetLength(hash);
            if (sourceLength <= 0)
                return false;

            if (!LocRegistry.TryGetVisualBuffer(hash, out char[] source, out int length))
                length = sourceLength;

            int sourceCapacity = source != null ? source.Length : 0;
            int writeLength = math.min(math.min(length, sourceCapacity), CharBufferPool.SlotCapacity);
            if (writeLength <= 0 || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return false;

            Span<char> destination = lease.Buffer.AsSpan(0, writeLength);
            ReadOnlySpan<char> sourceSpan = source.AsSpan(0, writeLength);
            if (scramble)
                Scramble(sourceSpan, destination, _entityHash, _progress01, _scramblePhase);
            else
                sourceSpan.CopyTo(destination);

            targetText.SetCharArray(lease.Buffer, 0, writeLength);
            CharBufferPool.Release(in lease);
            return true;
        }

        private static void Scramble(
            ReadOnlySpan<char> source,
            Span<char> destination,
            uint hash,
            float progress01,
            float phase)
        {
            int revealCount = math.clamp((int)math.floor(source.Length * math.saturate(progress01)), 0, source.Length);
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

        private void RefreshScrambleAllowed(float deltaTime)
        {
            if (!_scrambleTierInitialized)
            {
                bool requested = IsScrambleAllowedForCurrentTier();
                _scrambleAllowed = requested;
                _scrambleCandidate = requested;
                _scrambleCandidateAge = 0f;
                _scrambleProbeCountdown = ScrambleTierProbeIntervalSeconds;
                _scrambleTierInitialized = true;
                return;
            }

            _scrambleProbeCountdown -= math.max(0f, deltaTime);
            if (_scrambleProbeCountdown > 0f)
                return;

            _scrambleProbeCountdown = ScrambleTierProbeIntervalSeconds;
            bool requestedAllowed = IsScrambleAllowedForCurrentTier();
            if (requestedAllowed == _scrambleAllowed)
            {
                _scrambleCandidate = requestedAllowed;
                _scrambleCandidateAge = 0f;
                return;
            }

            if (requestedAllowed != _scrambleCandidate)
            {
                _scrambleCandidate = requestedAllowed;
                _scrambleCandidateAge = 0f;
                return;
            }

            _scrambleCandidateAge += ScrambleTierProbeIntervalSeconds;
            if (_scrambleCandidateAge >= ScrambleTierHysteresisSeconds)
            {
                _scrambleAllowed = requestedAllowed;
                _scrambleCandidateAge = 0f;
            }
        }

        private static bool IsScrambleAllowedForCurrentTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier != HectonQualityTier.Unknown &&
                   tier != HectonQualityTier.Low &&
                   tier != HectonQualityTier.Mx350;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || _entityHash == 0u)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
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
            _scrambleTierInitialized = false;
            _scrambleAllowed = false;
            _scrambleCandidate = false;
            _scrambleProbeCountdown = 0f;
            _scrambleCandidateAge = 0f;

            if (targetText != null)
                targetText.SetCharArray(EmptyText, 0, 0);

            Unregister();
        }
    }
}

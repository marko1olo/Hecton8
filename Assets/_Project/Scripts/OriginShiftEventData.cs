using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Immutable payload describing a committed floating-origin shift.
    /// </summary>
    public readonly struct OriginShiftEventData
    {
        /// <summary>
        /// Creates a new shift event payload.
        /// </summary>
        /// <param name="shiftOffset">Offset subtracted from all root transforms.</param>
        /// <param name="previousTotalOffset">Absolute-universe offset before the shift.</param>
        /// <param name="newTotalOffset">Absolute-universe offset after the shift.</param>
        /// <param name="sequence">Monotonic shift sequence.</param>
        /// <param name="frame">Frame when the shift committed.</param>
        /// <param name="fixedInterpolationAlpha">Fractional fixed-step interpolation alpha captured before the shift.</param>
        /// <param name="isSafeTeleport">True when the shift was part of an instantaneous travel protocol.</param>
        public OriginShiftEventData(
            Vector3 shiftOffset,
            Vector3 previousTotalOffset,
            Vector3 newTotalOffset,
            uint sequence,
            int frame,
            float fixedInterpolationAlpha = 0f,
            bool isSafeTeleport = false)
        {
            ShiftOffset = shiftOffset;
            PreviousTotalOffset = previousTotalOffset;
            NewTotalOffset = newTotalOffset;
            Sequence = sequence;
            Frame = frame;
            FixedInterpolationAlpha = Mathf.Clamp01(fixedInterpolationAlpha);
            IsSafeTeleport = isSafeTeleport;
        }

        /// <summary>Offset subtracted from all loaded-scene root transforms.</summary>
        public Vector3 ShiftOffset { get; }

        /// <summary>Absolute-universe offset before the shift committed.</summary>
        public Vector3 PreviousTotalOffset { get; }

        /// <summary>Absolute-universe offset after the shift committed.</summary>
        public Vector3 NewTotalOffset { get; }

        /// <summary>Monotonic shift sequence number.</summary>
        public uint Sequence { get; }

        /// <summary>Frame when the shift committed.</summary>
        public int Frame { get; }

        /// <summary>Fractional fixed-step interpolation alpha captured before the shift committed.</summary>
        public float FixedInterpolationAlpha { get; }

        /// <summary>True when this payload was emitted by the safe teleport protocol.</summary>
        public bool IsSafeTeleport { get; }

        /// <summary>
        /// Converts a runtime-space position captured under <paramref name="capturedTotalOffset"/>
        /// into the correct runtime-space position after this shift has committed.
        /// </summary>
        /// <param name="capturedRuntimePosition">Runtime-space position at capture time.</param>
        /// <param name="capturedTotalOffset">Absolute-universe offset active at capture time.</param>
        /// <returns>Runtime-space position under <see cref="NewTotalOffset"/>.</returns>
        public Vector3 RebaseCapturedRuntimePosition(Vector3 capturedRuntimePosition, Vector3 capturedTotalOffset)
        {
            return capturedRuntimePosition + capturedTotalOffset - NewTotalOffset;
        }

        /// <summary>
        /// Converts an absolute-universe position into runtime space after this shift.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position under <see cref="NewTotalOffset"/>.</returns>
        public Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition)
        {
            return absoluteUniversePosition - NewTotalOffset;
        }
    }
}

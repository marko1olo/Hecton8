using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    internal static class OriginShiftEventLayout
    {
        public const int EventDataStrideBytes = 112;
    }

    /// <summary>
    /// Immutable payload describing a committed floating-origin shift.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OriginShiftEventLayout.EventDataStrideBytes)]
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
            : this(
                shiftOffset,
                previousTotalOffset,
                newTotalOffset,
                global::Hecton8.World.AUPMath.ToDouble3(previousTotalOffset),
                global::Hecton8.World.AUPMath.ToDouble3(newTotalOffset),
                sequence,
                frame,
                fixedInterpolationAlpha,
                isSafeTeleport)
        {
        }

        /// <summary>
        /// Creates a new shift event payload with double-precision committed offsets.
        /// </summary>
        public OriginShiftEventData(
            Vector3 shiftOffset,
            Vector3 previousTotalOffset,
            Vector3 newTotalOffset,
            double3 previousTotalOffsetDouble,
            double3 newTotalOffsetDouble,
            uint sequence,
            int frame,
            float fixedInterpolationAlpha = 0f,
            bool isSafeTeleport = false)
        {
            ShiftOffset = shiftOffset;
            PreviousTotalOffset = previousTotalOffset;
            NewTotalOffset = newTotalOffset;
            PreviousTotalOffsetDouble = previousTotalOffsetDouble;
            NewTotalOffsetDouble = newTotalOffsetDouble;
            Sequence = sequence;
            Frame = frame;
            FixedInterpolationAlpha = Mathf.Clamp01(fixedInterpolationAlpha);
            IsSafeTeleport = isSafeTeleport ? (byte)1 : (byte)0;
            _alignPad0 = 0u;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0UL;
        }

        /// <summary>Offset subtracted from all loaded-scene root transforms.</summary>
        [FieldOffset(0)] public readonly Vector3 ShiftOffset;

        /// <summary>Absolute-universe offset before the shift committed.</summary>
        [FieldOffset(12)] public readonly Vector3 PreviousTotalOffset;

        /// <summary>Absolute-universe offset after the shift committed.</summary>
        [FieldOffset(24)] public readonly Vector3 NewTotalOffset;

        /// <summary>Double-precision absolute-universe offset before the shift committed.</summary>
        [FieldOffset(40)] public readonly double3 PreviousTotalOffsetDouble;

        /// <summary>Double-precision absolute-universe offset after the shift committed.</summary>
        [FieldOffset(64)] public readonly double3 NewTotalOffsetDouble;

        /// <summary>Monotonic shift sequence number.</summary>
        [FieldOffset(88)] public readonly uint Sequence;

        /// <summary>Frame when the shift committed.</summary>
        [FieldOffset(92)] public readonly int Frame;

        /// <summary>Fractional fixed-step interpolation alpha captured before the shift committed.</summary>
        [FieldOffset(96)] public readonly float FixedInterpolationAlpha;

        /// <summary>1 when this payload was emitted by the safe teleport protocol.</summary>
        [FieldOffset(100)] public readonly byte IsSafeTeleport;

        [FieldOffset(36)] private readonly uint _alignPad0;
        [FieldOffset(101)] private readonly byte _pad0;
        [FieldOffset(102)] private readonly ushort _pad1;
        [FieldOffset(104)] private readonly ulong _pad2;

        /// <summary>
        /// Converts a runtime-space position captured under <paramref name="capturedTotalOffset"/>
        /// into the correct runtime-space position after this shift has committed.
        /// </summary>
        /// <param name="capturedRuntimePosition">Runtime-space position at capture time.</param>
        /// <param name="capturedTotalOffset">Absolute-universe offset active at capture time.</param>
        /// <returns>Runtime-space position under <see cref="NewTotalOffset"/>.</returns>
        public Vector3 RebaseCapturedRuntimePosition(Vector3 capturedRuntimePosition, Vector3 capturedTotalOffset)
        {
            return RebaseCapturedRuntimePosition(capturedRuntimePosition, global::Hecton8.World.AUPMath.ToDouble3(capturedTotalOffset));
        }

        /// <summary>
        /// Converts a runtime-space position captured under <paramref name="capturedTotalOffset"/>
        /// into the correct runtime-space position after this shift has committed.
        /// </summary>
        public Vector3 RebaseCapturedRuntimePosition(Vector3 capturedRuntimePosition, double3 capturedTotalOffset)
        {
            double3 capturedRuntime = global::Hecton8.World.AUPMath.ToDouble3(capturedRuntimePosition);
            double3 runtime = capturedRuntime + capturedTotalOffset - NewTotalOffsetDouble;
            return ToVector3(runtime);
        }

        /// <summary>
        /// Converts an absolute-universe position into runtime space after this shift.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position under <see cref="NewTotalOffset"/>.</returns>
        public Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition)
        {
            return ToRuntimePosition(global::Hecton8.World.AUPMath.ToDouble3(absoluteUniversePosition));
        }

        /// <summary>
        /// Converts an absolute-universe position into runtime space after this shift.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position under <see cref="NewTotalOffset"/>.</returns>
        public Vector3 ToRuntimePosition(double3 absoluteUniversePosition)
        {
            return ToVector3(absoluteUniversePosition - NewTotalOffsetDouble);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }
    }
}

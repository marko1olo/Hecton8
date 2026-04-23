using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Action bits exposed through the frame-cached player input snapshot.
    /// </summary>
    [Flags]
    public enum PlayerInputAction : uint
    {
        None = 0u,
        Jump = 1u << 0,
        Interact = 1u << 1,
        PrimaryFire = 1u << 2,
        SecondaryFire = 1u << 3,
        Sprint = 1u << 4,
        Dash = 1u << 5,
    }

    /// <summary>
    /// Buffered discrete-action tokens retained by the input dispatcher ring buffer.
    /// </summary>
    public enum PlayerBufferedAction : byte
    {
        None = 0,
        Jump = 1,
        Dash = 2,
    }

    /// <summary>
    /// Zero-allocation player input snapshot captured once at the start of each frame.
    /// </summary>
    public struct PlayerInputState
    {
        /// <summary>
        /// Cached planar movement vector for the current frame.
        /// </summary>
        public Vector2 MoveDelta;

        /// <summary>
        /// Cached accumulated look delta for the current frame.
        /// </summary>
        public Vector2 LookDelta;

        /// <summary>
        /// Cached vertical ascend/descend input for the current frame.
        /// </summary>
        public float VerticalDelta;

        /// <summary>
        /// Frame-cached action flags for held and latched gameplay actions.
        /// </summary>
        public uint ActionsBitmask;

        /// <summary>
        /// Returns true when the cached frame snapshot contains the requested action flag.
        /// </summary>
        /// <param name="action">Action flag to test.</param>
        /// <returns>True when the flag is set.</returns>
        public readonly bool HasAction(PlayerInputAction action)
        {
            return (ActionsBitmask & (uint)action) != 0u;
        }
    }
}

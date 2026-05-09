using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
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
    /// OpenXR controller button bits exposed through the frame-cached XR input snapshot.
    /// </summary>
    [Flags]
    internal enum XRInputButton : uint
    {
        None = 0u,
        Trigger = 1u << 0,
        Grip = 1u << 1,
        JoystickClick = 1u << 2,
        Primary = 1u << 3,
        Secondary = 1u << 4,
    }

    /// <summary>
    /// Per-controller activity bits used to skip idle XR hand work.
    /// </summary>
    [Flags]
    internal enum XRInputActiveBit : uint
    {
        None = 0u,
        Trigger = 1u << 0,
        Grip = 1u << 1,
        Joystick = 1u << 2,
        Primary = 1u << 3,
        Secondary = 1u << 4,
    }

    /// <summary>
    /// Zero-allocation player input snapshot captured once at the start of each frame.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
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

    /// <summary>
    /// Blittable OpenXR controller state captured once per dispatcher frame.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct XRInputState
    {
        /// <summary>Frame that produced this snapshot.</summary>
        public int Frame;

        /// <summary>Controller slot: 0 left, 1 right.</summary>
        public byte ControllerIndex;

        /// <summary>Non-zero when the OpenXR controller reports a tracked pose.</summary>
        public byte IsTracked;

        /// <summary>Trigger axis normalized to 0..1.</summary>
        public float Trigger;

        /// <summary>Grip axis normalized to 0..1.</summary>
        public float Grip;

        /// <summary>Primary joystick or primary 2D axis value.</summary>
        public float2 Joystick;

        /// <summary>Grip-pose position in runtime world space.</summary>
        public float3 GripPositionWS;

        /// <summary>Grip-pose rotation in runtime world space.</summary>
        public quaternion GripRotationWS;

        /// <summary>Cached OpenXR button flags.</summary>
        public uint ButtonsBitmask;

        /// <summary>Shifted active-control mask: left uses bits 0-4, right uses bits 5-9.</summary>
        public uint ActiveMask;

        /// <summary>True when any analog/button input survived the deadzone gate.</summary>
        public readonly bool HasActiveInput => ActiveMask != 0u;

        /// <summary>
        /// Returns true when the cached XR snapshot contains the requested button flag.
        /// </summary>
        public readonly bool HasButton(XRInputButton button)
        {
            return (ButtonsBitmask & (uint)button) != 0u;
        }
    }
}

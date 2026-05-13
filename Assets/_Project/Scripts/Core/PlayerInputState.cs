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
    /// Platform-specific input bits appended to the frame input snapshot.
    /// </summary>
    [Flags]
    public enum PlatformInputFlag : uint
    {
        None = 0u,
        SteamDeckGyro = 1u << 0,
        SteamDeckLeftTrackpad = 1u << 1,
        SteamDeckRightTrackpad = 1u << 2,
        SteamDeckEmulatedTrackpads = 1u << 3
    }

    [Flags]
    public enum InputStateFlags : ushort
    {
        None = 0,
        AutomationOverride = 1 << 0,
        DelayApplied = 1 << 1,
        NonFiniteSanitized = 1 << 2
    }

    /// <summary>
    /// Bit-packed deterministic input sample consumed by simulation and replay.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
    public struct InputState
    {
        public const float AxisQuantizeScale = 32767.0f;
        public const float AxisInvQuantizeScale = 1.0f / AxisQuantizeScale;
        public const float LookQuantizeScale = 1024.0f;
        public const float LookInvQuantizeScale = 1.0f / LookQuantizeScale;
        public const float LookQuantizeLimit = 31.999f;

        public uint Frame;
        public uint Sequence;
        public short MoveX;
        public short MoveY;
        public short LookX;
        public short LookY;
        public short Vertical;
        public ushort Flags;
        public uint ButtonsBitmask;

        public readonly float2 Move => new float2(MoveX * AxisInvQuantizeScale, MoveY * AxisInvQuantizeScale);
        public readonly float2 Look => new float2(LookX * LookInvQuantizeScale, LookY * LookInvQuantizeScale);
        public readonly float VerticalAxis => Vertical * AxisInvQuantizeScale;

        public readonly bool HasFlag(InputStateFlags flag)
        {
            return (Flags & (ushort)flag) != 0;
        }

        public readonly bool HasButton(PlayerInputAction action)
        {
            return (ButtonsBitmask & (uint)action) != 0u;
        }

        public static short QuantizeUnit(float value, ref ushort flags)
        {
            if (!math.isfinite(value))
            {
                flags |= (ushort)InputStateFlags.NonFiniteSanitized;
                return 0;
            }

            float clamped = math.clamp(value, -1.0f, 1.0f);
            return (short)math.round(clamped * AxisQuantizeScale);
        }

        public static short QuantizeLook(float value, ref ushort flags)
        {
            if (!math.isfinite(value))
            {
                flags |= (ushort)InputStateFlags.NonFiniteSanitized;
                return 0;
            }

            float clamped = math.clamp(value, -LookQuantizeLimit, LookQuantizeLimit);
            return (short)math.round(clamped * LookQuantizeScale);
        }
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
        /// Cached UI scroll delta for diegetic analog controls.
        /// </summary>
        public Vector2 ScrollDelta;

        /// <summary>
        /// Cached vertical ascend/descend input for the current frame.
        /// </summary>
        public float VerticalDelta;

        /// <summary>
        /// Steam Deck gyro contribution already folded into <see cref="LookDelta"/>.
        /// </summary>
        public Vector2 SteamDeckGyroAimDelta;

        /// <summary>
        /// Left Steam Deck trackpad axis, or the mapped left-stick proxy when Steam Input is unavailable.
        /// </summary>
        public Vector2 SteamDeckLeftTrackpad;

        /// <summary>
        /// Right Steam Deck trackpad axis, or the mapped right-stick proxy when Steam Input is unavailable.
        /// </summary>
        public Vector2 SteamDeckRightTrackpad;

        /// <summary>
        /// Frame-cached action flags for held and latched gameplay actions.
        /// </summary>
        public uint ActionsBitmask;

        /// <summary>
        /// Platform-specific input flags for Steam Deck and future PAL devices.
        /// </summary>
        public uint PlatformInputFlags;

        /// <summary>
        /// Stable hash of the input scheme that produced this frame snapshot.
        /// </summary>
        public uint CurrentInputSchemeHash;

        /// <summary>
        /// Returns true when the cached frame snapshot contains the requested action flag.
        /// </summary>
        /// <param name="action">Action flag to test.</param>
        /// <returns>True when the flag is set.</returns>
        public readonly bool HasAction(PlayerInputAction action)
        {
            return (ActionsBitmask & (uint)action) != 0u;
        }

        /// <summary>
        /// Returns true when a platform-specific input flag is set.
        /// </summary>
        public readonly bool HasPlatformFlag(PlatformInputFlag flag)
        {
            return (PlatformInputFlags & (uint)flag) != 0u;
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

using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Immutable camera target state produced by locomotion and consumed by the camera rig owner.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HectonCameraState
    {
        public const uint ApplyTransformDirectlyFlag = 1u << 0;

        /// <summary>
        /// Desired world-space camera rotation after locomotion, recoil, and traversal offsets are composed.
        /// </summary>
        [FieldOffset(0)] public Quaternion TargetRotation;

        /// <summary>
        /// Desired camera local position inside the player rig after locomotion offsets are composed.
        /// </summary>
        [FieldOffset(16)] public Vector3 TargetLocalPosition;

        /// <summary>
        /// Desired field of view after locomotion and pressure effects are composed.
        /// </summary>
        [FieldOffset(28)] public float TargetFieldOfView;

        /// <summary>
        /// Previous fixed-step KCC body position used by the late-frame camera interpolation window.
        /// </summary>
        [FieldOffset(32)] public Vector3 PreviousFixedPosition;

        /// <summary>
        /// Current fixed-step KCC body position used by the late-frame camera interpolation window.
        /// </summary>
        [FieldOffset(44)] public Vector3 CurrentFixedPosition;

        /// <summary>
        /// KCC-owned world velocity used by the late-frame camera position predictor.
        /// </summary>
        [FieldOffset(56)] public Vector3 KccLinearVelocity;

        /// <summary>
        /// Current fixed-step duration used to resolve the remaining render fraction.
        /// </summary>
        [FieldOffset(68)] public float FixedDeltaTime;

        /// <summary>
        /// Delta time used to smooth camera transitions for this frame.
        /// </summary>
        [FieldOffset(72)] public float DeltaTime;

        /// <summary>
        /// Bitfield for hot camera-state switches.
        /// </summary>
        [FieldOffset(76)] public uint Flags;

        /// <summary>
        /// Exponential sharpness used for rotation smoothing.
        /// </summary>
        [FieldOffset(80)] public float RotationSharpness;

        /// <summary>
        /// Exponential sharpness used for local-position smoothing.
        /// </summary>
        [FieldOffset(84)] public float PositionSharpness;

        /// <summary>
        /// Exponential sharpness used for FOV smoothing.
        /// </summary>
        [FieldOffset(88)] public float FieldOfViewSharpness;
        [FieldOffset(92)] private uint _pad0;

        /// <summary>
        /// Returns true when transform pose must be applied without interpolation, e.g. VR comfort/head-pose stability.
        /// </summary>
        public static bool RequiresDirectTransform(uint flags)
        {
            return (flags & ApplyTransformDirectlyFlag) != 0u;
        }
    }
}

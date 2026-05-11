using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Immutable camera target state produced by locomotion and consumed by the camera rig owner.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct HectonCameraState
    {
        public const uint ApplyTransformDirectlyFlag = 1u << 0;

        /// <summary>
        /// Desired world-space camera rotation after locomotion, recoil, and traversal offsets are composed.
        /// </summary>
        public Quaternion TargetRotation;

        /// <summary>
        /// Desired camera local position inside the player rig after locomotion offsets are composed.
        /// </summary>
        public Vector3 TargetLocalPosition;

        /// <summary>
        /// Desired field of view after locomotion and pressure effects are composed.
        /// </summary>
        public float TargetFieldOfView;

        /// <summary>
        /// Previous fixed-step KCC body position used by the late-frame camera interpolation window.
        /// </summary>
        public Vector3 PreviousFixedPosition;

        /// <summary>
        /// Current fixed-step KCC body position used by the late-frame camera interpolation window.
        /// </summary>
        public Vector3 CurrentFixedPosition;

        /// <summary>
        /// KCC-owned world velocity used by the late-frame camera position predictor.
        /// </summary>
        public Vector3 KccLinearVelocity;

        /// <summary>
        /// Current fixed-step duration used to resolve the remaining render fraction.
        /// </summary>
        public float FixedDeltaTime;

        /// <summary>
        /// Delta time used to smooth camera transitions for this frame.
        /// </summary>
        public float DeltaTime;

        /// <summary>
        /// Bitfield for hot camera-state switches.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// True when transform pose must be applied without interpolation, e.g. VR comfort/head-pose stability.
        /// </summary>
        public bool ApplyTransformDirectly => (Flags & ApplyTransformDirectlyFlag) != 0u;

        /// <summary>
        /// Exponential sharpness used for rotation smoothing.
        /// </summary>
        public float RotationSharpness;

        /// <summary>
        /// Exponential sharpness used for local-position smoothing.
        /// </summary>
        public float PositionSharpness;

        /// <summary>
        /// Exponential sharpness used for FOV smoothing.
        /// </summary>
        public float FieldOfViewSharpness;
    }
}

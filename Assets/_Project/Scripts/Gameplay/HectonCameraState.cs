using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Immutable camera target state produced by locomotion and consumed by the camera rig owner.
    /// </summary>
    public struct HectonCameraState
    {
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
        /// Delta time used to smooth camera transitions for this frame.
        /// </summary>
        public float DeltaTime;

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

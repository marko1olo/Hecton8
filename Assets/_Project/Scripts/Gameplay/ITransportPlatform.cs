using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Runtime contract for transports that define a moving local reference frame for player locomotion.
    /// </summary>
    /// <remarks>
    /// Used by the player controller to translate look, movement, and corrective motion through platform space.
    /// </remarks>
    public interface ITransportPlatform
    {
        /// <summary>True while this platform should currently contribute local-space carrier motion.</summary>
        bool IsTransportPlatformActive { get; }

        /// <summary>Transform defining the authoritative local frame for rider-space kinematics.</summary>
        Transform PlatformTransform { get; }

        /// <summary>
        /// True when camera/body yaw should inherit platform rotation additively.
        /// Platforms that already slave their hull rotation to the rider should return false to avoid feedback.
        /// </summary>
        bool InheritPlatformRotation { get; }

        /// <summary>
         /// Returns current world-space point velocity at the given rider point.
        /// </summary>
        /// <param name="worldPoint">World-space point on or inside the platform.</param>
        Vector3 GetPlatformPointVelocity(Vector3 worldPoint);
    }

    /// <summary>
    /// Narrow predictive voxel proxy route for transports with an authored vehicle motor.
    /// </summary>
    public interface ITransportPredictiveVoxelProxySource
    {
        bool TryResolvePredictiveVoxelProxy(out Rigidbody body, out Vector3 velocity);

        void ApplyPredictiveVoxelProxyDampener(float strength01);
    }
}

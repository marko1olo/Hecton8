using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed bootstrap and dispatch layers used by the registry-backed runtime core.
    /// </summary>
    public enum PriorityLayer : byte
    {
        Core = 0x00,
        Environment = 0x20,
        Player = 0x40,
        UI = 0x60,
    }

    /// <summary>
    /// Zero-allocation update contract used by <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Minimal render callback contract for registry-managed render systems.
    /// </summary>
    public interface IRenderable
    {
        /// <summary>
        /// Executes one render-side callback.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the caller.</param>
        void Render(float deltaTime);
    }

    /// <summary>
    /// Minimal input service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Authoritative physics routing service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPhysicsService
    {
        /// <summary>
        /// True once the physics routing owner is initialized and ready to accept packets.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Queues a force packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues a torque packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true);

        /// <summary>
        /// Clears all queued packets and cached body slots.
        /// </summary>
        void ClearQueuedPackets();
    }

    /// <summary>
    /// Minimal audio service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Authoritative scene transition service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>
        /// True when scene transitions are permitted by bootstrap state.
        /// </summary>
        bool CanLoadScene { get; }

        /// <summary>
        /// Performs a guarded scene transition.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        void LoadScene(string sceneName);
    }

    /// <summary>
    /// Minimal UI service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }
}

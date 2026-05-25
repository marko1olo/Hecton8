namespace Hecton8.Core
{
    /// <summary>
    /// Receives one deferred dispatcher-owned surface probe result in LateUpdate.
    /// </summary>
    internal interface IDispatcherSurfaceProbeReceiver
    {
        /// <summary>
        /// Consumes one dispatcher-owned deferred surface probe result.
        /// </summary>
        void ConsumeDispatcherSurfaceHit(int requestId, in KinematicSurfaceHit hit);
    }
}

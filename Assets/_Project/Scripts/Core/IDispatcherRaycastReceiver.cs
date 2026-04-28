using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Receives one deferred dispatcher-owned raycast result in LateUpdate.
    /// </summary>
    internal interface IDispatcherRaycastReceiver
    {
        /// <summary>
        /// Consumes one dispatcher-owned deferred raycast result.
        /// </summary>
        void ConsumeDispatcherRaycastHit(int requestId, in RaycastHit hit);
    }
}

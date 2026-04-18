using UnityEngine;

namespace Hecton.Interaction
{
    /// <summary>
    /// Legacy compatibility stub for recovery-scene interaction data.
    /// </summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public float reachDistance = 3.5f;
        public float raycastInterval = 0.2f;
        public LayerMask interactionMask = ~0;
        public KeyCode interactKey = KeyCode.E;
        public Camera playerCamera;
    }
}

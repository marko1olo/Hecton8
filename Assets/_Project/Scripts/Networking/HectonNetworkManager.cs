// ============================================================================
// HECTON-8 — HectonNetworkManager.cs
// Basic networking manager for multiplayer prep.
// ============================================================================

using UnityEngine;

namespace Hecton8.Networking
{
    #pragma warning disable CS0414 // Serialized networking placeholders are intentionally retained until multiplayer wiring exists.
    public sealed class HectonNetworkManager : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private bool isServer = false;
        [SerializeField] private bool isClient = false;
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private int port = 7777;

        private void Start()
        {
            // TODO: Initialize networking (e.g., Mirror, Netcode)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("HectonNetworkManager initialized - multiplayer prep");
#endif
        }

        public void StartServer()
        {
            // TODO: Start server
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Starting server...");
#endif
        }

        public void StartClient()
        {
            // TODO: Start client
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Starting client...");
#endif
        }

        public void StopNetwork()
        {
            // TODO: Stop network
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Stopping network...");
#endif
        }

        // TODO: Add network messages, player sync, etc.
    }
    #pragma warning restore CS0414
}

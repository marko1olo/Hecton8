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
        public static HectonNetworkManager Instance { get; private set; }

        [Header("Network Settings")]
        [SerializeField] private bool isServer = false;
        [SerializeField] private bool isClient = false;
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private int port = 7777;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // TODO: Initialize networking (e.g., Mirror, Netcode)
            Debug.Log("HectonNetworkManager initialized - multiplayer prep");
        }

        public void StartServer()
        {
            // TODO: Start server
            Debug.Log("Starting server...");
        }

        public void StartClient()
        {
            // TODO: Start client
            Debug.Log("Starting client...");
        }

        public void StopNetwork()
        {
            // TODO: Stop network
            Debug.Log("Stopping network...");
        }

        // TODO: Add network messages, player sync, etc.
    }
    #pragma warning restore CS0414
}

using UnityEngine;

namespace Hecton8.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonRollbackNetcodeRuntime))]
    public sealed class HectonNetworkManager : MonoBehaviour
    {
        [Header("Lockstep Settings")]
        [SerializeField] private bool isServer;
        [SerializeField] private bool isClient;
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private int port = 7777;

        private HectonRollbackNetcodeRuntime _runtime;

        private void Awake()
        {
            TryGetComponent(out _runtime);
        }

        public void StartServer()
        {
            isServer = true;
            isClient = false;
            EnsureRuntime();
            HectonRollbackNetcodeRuntime.TrySetMode(server: true, client: false);
        }

        public void StartClient()
        {
            isServer = false;
            isClient = true;
            EnsureRuntime();
            HectonRollbackNetcodeRuntime.TrySetMode(server: false, client: true);
        }

        public void StopNetwork()
        {
            isServer = false;
            isClient = false;
            HectonRollbackNetcodeRuntime.TryStopMode();
        }

        public void ApplySerializedMode()
        {
            EnsureRuntime();
            HectonRollbackNetcodeRuntime.TrySetMode(isServer, isClient);
        }

        public string ServerAddress => serverAddress;

        public int Port => port;

        private HectonRollbackNetcodeRuntime EnsureRuntime()
        {
            if (_runtime != null)
                return _runtime;

            if (!TryGetComponent(out _runtime))
                _runtime = gameObject.AddComponent<HectonRollbackNetcodeRuntime>();

            return _runtime;
        }
    }
}

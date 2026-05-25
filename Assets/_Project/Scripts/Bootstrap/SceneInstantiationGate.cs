using System.Threading;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Blocks gameplay handoff until bootstrap, player instantiation and pressure verification have all completed.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20010)]
    public sealed class SceneInstantiationGate : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private const int GateOpenWatchdogFrames = 50000;

        private static SceneInstantiationGate s_activeRuntime;
        private bool _worldPrimed;
        private bool _playerInstantiated;
        private bool _memorySnapshotCaptured;
        private bool _gateOpen;
        private bool _hotSwapRegistered;
        private IVramPressureReadModel _vramPressure;
        private string _sceneName = string.Empty;

        internal static SceneInstantiationGate ActiveRuntime => s_activeRuntime;
        internal bool IsOpen => _gateOpen;
        internal string LastFailureReason { get; private set; } = "UNINITIALIZED";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
        }

        internal static SceneInstantiationGate EnsureRuntimeInstance()
        {
            SceneInstantiationGate runtime = s_activeRuntime;
            if (runtime != null)
                return runtime;

            GameObject runtimeRoot = new GameObject("[SceneInstantiationGate]"); // COLD ALLOC: GameObject[1] - bootstrap-owned async scene activation gate root - owner: SceneInstantiationGate
            return runtimeRoot.AddComponent<SceneInstantiationGate>();
        }

        private void Awake()
        {
            SceneInstantiationGate runtime = GlobalRegistry.SceneInstantiationGateRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSceneInstantiationGateRuntime(this);
            if (ReferenceEquals(GlobalRegistry.SceneInstantiationGateRuntime, this))
                s_activeRuntime = this;
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            GlobalRegistry.ClearSceneInstantiationGateRuntime(this);
        }

        internal void BeginSceneLoad(string sceneName)
        {
            _sceneName = string.IsNullOrEmpty(sceneName) ? string.Empty : sceneName;
            _worldPrimed = false;
            _playerInstantiated = false;
            _memorySnapshotCaptured = false;
            _gateOpen = false;
            LastFailureReason = "BOOT_PENDING";
        }

        internal void MarkWorldPrimed()
        {
            _worldPrimed = true;
        }

        internal void MarkPlayerInstantiated(GameObject playerObject)
        {
            _playerInstantiated = playerObject != null;
            if (!_playerInstantiated)
                LastFailureReason = "PLAYER_NULL";
        }

        internal void CaptureMemorySnapshot(float textureMemoryMb, float reservedMemoryMb, float totalVramMb)
        {
            _memorySnapshotCaptured = true;
        }

        internal async Awaitable WaitForOpenAsync(CancellationToken cancellationToken)
        {
            int watchdog = 0;
            while (!_gateOpen)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (watchdog++ > GateOpenWatchdogFrames)
                {
                    LastFailureReason = "WATCHDOG_TIMEOUT";
                    Hecton8.Core.H8Debug.LogError(
                        $"[SceneInstantiationGate] WaitForOpenAsync timed out after {GateOpenWatchdogFrames} frames. " +
                        $"Scene='{_sceneName}' LastFailure='{LastFailureReason}'.");
                    return;
                }

                if (TryValidateGate(out string failureReason))
                {
                    _gateOpen = true;
                    LastFailureReason = "NONE";
                    return;
                }

                LastFailureReason = failureReason;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private bool TryValidateGate(out string failureReason)
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                failureReason = "SCENE_NAME_MISSING";
                return false;
            }

            if (!_worldPrimed)
            {
                failureReason = "WORLD_PRIME_PENDING";
                return false;
            }

            if (!_playerInstantiated)
            {
                failureReason = "PLAYER_INSTANTIATION_PENDING";
                return false;
            }

            if (!_memorySnapshotCaptured)
            {
                failureReason = "MEMORY_SNAPSHOT_PENDING";
                return false;
            }

            IVramPressureReadModel pressureMonitor = _vramPressure;
            if (pressureMonitor == null || !pressureMonitor.HasSample)
            {
                failureReason = "PRESSURE_SAMPLE_PENDING";
                return false;
            }

            if (pressureMonitor.VramPressureFactor >= 0.99f)
            {
                failureReason = "VRAM_GATE_REJECT";
                return false;
            }

            if (pressureMonitor.RamPressureFactor >= 0.95f)
            {
                failureReason = "RAM_GATE_REJECT";
                return false;
            }

            failureReason = "NONE";
            return true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.VRAMPressureRuntime)
                _vramPressure = currentService as IVramPressureReadModel;
        }

        private void CacheRegistryServicesCold()
        {
            _vramPressure = GlobalRegistry.VRAMPressureReadModel;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}

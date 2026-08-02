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

        /// <summary>
        /// Which authority condition the offered player failed, or null when none was ever offered.
        /// Distinguishes "a player was judged and rejected, for this reason" from "no player has arrived
        /// yet" - two states the gate used to report with the same word.
        /// </summary>
        private string _playerRejectionReason;
        private bool _memorySnapshotCaptured;
        private bool _gateOpen;
        private bool _hotSwapRegistered;
        private bool _runtimeOwnerAborted;
        private IVramPressureReadModel _vramPressure;
        private string _sceneName = string.Empty;

        internal static SceneInstantiationGate ActiveRuntime => s_activeRuntime;
        internal bool IsOpen => _gateOpen;
        internal string LastFailureReason { get; private set; } = "UNINITIALIZED";

        /// <summary>
        /// True once bootstrap has finished Runtime World Prime for the active scene load.
        /// GPU systems that bind buffers from LateFrame must wait for this before touching
        /// live GraphicsBuffers / compute kernels — during Step 8 the world scene can be
        /// loaded while menu unload and buffer teardown still race the dispatcher.
        /// False when no gate owns the registry slot (pre-boot / torn-down).
        /// </summary>
        public static bool IsWorldPrimed =>
            s_activeRuntime != null && s_activeRuntime._worldPrimed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
            GlobalRegistry.ClearSceneInstantiationGateRuntime(null);
        }

        internal static SceneInstantiationGate EnsureRuntimeInstance()
        {
            SceneInstantiationGate runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Gate owns async scene activation ordering; without create, LoadSceneAsync
            // consumers stall when bootstrap reorders or skips the CoreServices wire.
            GameObject runtimeRoot = new GameObject("[SceneInstantiationGate]"); // COLD ALLOC: GameObject[1] - bootstrap-owned async scene activation gate root - owner: SceneInstantiationGate
            return runtimeRoot.AddComponent<SceneInstantiationGate>();
        }

        private void Awake()
        {
            if (!EnsureRuntimeOwnership())
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(s_activeRuntime, this))
                    s_activeRuntime = null;

                GlobalRegistry.ClearSceneInstantiationGateRuntime(this);
                return;
            }

            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            GlobalRegistry.ClearSceneInstantiationGateRuntime(this);
        }

        internal void BeginSceneLoad(string sceneName)
        {
            _sceneName = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
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
            _playerInstantiated = ProductionPlayerAuthorityUtility.IsProductionPlayerAuthorityObject(playerObject);
            if (_playerInstantiated)
            {
                _playerRejectionReason = null;
                return;
            }

            // Keep WHICH of the five authority conditions failed. "PLAYER_AUTHORITY_INVALID" named the
            // check, never the cause, and the poll loop below then overwrote even that with the generic
            // PLAYER_INSTANTIATION_PENDING every frame - so the one fact needed to fix the boot was
            // destroyed twice before anyone could read it.
            _playerRejectionReason =
                ProductionPlayerAuthorityUtility.DescribeProductionPlayerAuthorityFailure(playerObject);
            LastFailureReason = _playerRejectionReason;
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
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                failureReason = "SCENE_NAME_MISSING";
                return false;
            }

#if UNITY_EDITOR
            if (_sceneName.IndexOf("sandbox", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                failureReason = "NONE";
                return true;
            }
#endif

            if (!_worldPrimed)
            {
                failureReason = "WORLD_PRIME_PENDING";
                return false;
            }

            if (!_playerInstantiated)
            {
                // Report the specific rejection when MarkPlayerInstantiated already judged a candidate.
                // Only a player that was never offered at all is genuinely still "pending".
                failureReason = _playerRejectionReason ?? "PLAYER_INSTANTIATION_PENDING";
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
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.VRAMPressureRuntime)
                _vramPressure = currentService as IVramPressureReadModel;
        }

        private bool EnsureRuntimeOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            // Ask before aborting anyone. SceneInstantiationGate has no GlobalRegistryServiceSlot, so its
            // slot resolves to Unknown, which is never scene-runtime hot-swappable. Once the registry is
            // ready-locked the RegisterSceneInstantiationGateRuntime call below is guaranteed to throw -
            // and both AbortRuntimeOwner calls between here and there would already have torn down the
            // live gate, leaving the scene with no instantiation gate at all. Stand down instead.
            //
            // Only when a real takeover is needed: if this instance already owns the registry slot, the
            // registration early-returns on reference equality and never reaches the guard.
            if (!ReferenceEquals(GlobalRegistry.SceneInstantiationGateRuntime, this) &&
                !GlobalRegistry.IsRuntimeServicePublicationOpen<SceneInstantiationGate>())
            {
                AbortRuntimeOwner();
                return false;
            }

            SceneInstantiationGate runtime = s_activeRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime.AbortRuntimeOwner();
            }

            runtime = GlobalRegistry.SceneInstantiationGateRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime.AbortRuntimeOwner();
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterSceneInstantiationGateRuntime(this);
            if (ReferenceEquals(GlobalRegistry.SceneInstantiationGateRuntime, this))
                s_activeRuntime = this;

            bool ownsRuntime =
                ReferenceEquals(s_activeRuntime, this) &&
                ReferenceEquals(GlobalRegistry.SceneInstantiationGateRuntime, this);
            if (!ownsRuntime)
                AbortRuntimeOwner();
            return ownsRuntime;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SceneInstantiationGate runtime = s_activeRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsSceneInstantiationGateRuntimeUsable(runtime))
                {
                    AbortRuntimeOwner();
                    return true;
                }

                runtime.AbortRuntimeOwner();
            }

            runtime = GlobalRegistry.SceneInstantiationGateRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsSceneInstantiationGateRuntimeUsable(runtime))
                {
                    AbortRuntimeOwner();
                    return true;
                }

                runtime.AbortRuntimeOwner();
            }

            return false;
        }

        private void AbortRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            GlobalRegistry.ClearSceneInstantiationGateRuntime(this);
            _vramPressure = null;
            _runtimeOwnerAborted = true;
            enabled = false;
        }

        private static SceneInstantiationGate ResolveUsableRuntime()
        {
            SceneInstantiationGate runtime = s_activeRuntime;
            if (IsSceneInstantiationGateRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                runtime.AbortRuntimeOwner();
            }

            runtime = GlobalRegistry.SceneInstantiationGateRuntime;
            if (IsSceneInstantiationGateRuntimeUsable(runtime))
            {
                s_activeRuntime = runtime;
                return runtime;
            }

            if (!ReferenceEquals(runtime, null))
            {
                runtime.AbortRuntimeOwner();
            }

            return null;
        }

        private static bool IsSceneInstantiationGateRuntimeUsable(SceneInstantiationGate runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
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

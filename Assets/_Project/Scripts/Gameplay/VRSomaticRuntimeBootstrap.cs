using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class VRSomaticRuntimeBootstrap : MonoBehaviour, ISlowTickable, IGameBootstrapperEventListener, IGlobalRegistryHotSwapListener
    {
        private const string RuntimeOwnerName = "[VRSomaticRuntimeBootstrap]";
        private const string DecoupledRootName = "VR_Somatic_DecoupledRoot";
        private const string PdaSocketName = "VR_SomaticSocket_PDA";
        private const string FlareToolSocketName = "VR_SomaticSocket_FlareTool";

        private static VRSomaticRuntimeBootstrap _runtime;
        private static Transform _decoupledRootTransform;

        private Transform _boundPlayerTransform;
        private Transform _vrRootTransform;
        private Transform _pdaSocketTransform;
        private Transform _flareSocketTransform;
        private bool _createdVrRootTransform;
        private bool _createdPdaSocketTransform;
        private bool _createdFlareSocketTransform;
        private bool _registeredSlowTick;
        private bool _registeredBootstrap;
        private bool _hotSwapRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _runtime = null;
            _decoupledRootTransform = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterXRActivationHook()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
        }

        private static void HandleXRActiveChanged(bool isActive)
        {
            VRSomaticRuntimeBootstrap runtime = _runtime;
            if (runtime == null)
                return;

            if (isActive)
            {
                if (!TryResolveAndBindProvider(true))
                    runtime.TryRegisterSlowTick();
                return;
            }

            runtime.ReleaseRuntimeBindings();
        }

        internal static VRSomaticRuntimeBootstrap EnsureRegisteredByBootstrap()
        {
            VRSomaticRuntimeBootstrap runtime = EnsureRuntime();
            if (runtime == null)
                return null;

            runtime.TryRegisterBootstrap();
            if (HectonXRRuntimeState.IsXRActive)
                EnsureRuntimeAndBind();
            return runtime;
        }

        private static VRSomaticRuntimeBootstrap EnsureRuntime()
        {
            if (_runtime != null)
                return _runtime;

            GameObject runtimeObject = new GameObject(RuntimeOwnerName); // COLD ALLOC: GameObject[1] - XR-only somatic provider installer - owner: VRSomaticRuntimeBootstrap
            _runtime = runtimeObject.AddComponent<VRSomaticRuntimeBootstrap>(); // COLD ALLOC: VRSomaticRuntimeBootstrap[1] - XR-only somatic provider installer - owner: VRSomaticRuntimeBootstrap
            GameBootstrapper.PersistRuntimeService(_runtime);
            return _runtime;
        }

        private static void EnsureRuntimeAndBind()
        {
            VRSomaticRuntimeBootstrap runtime = EnsureRuntime();
            if (runtime == null)
                return;

            if (!TryResolveAndBindProvider(true))
                runtime.TryRegisterSlowTick();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _runtime = this;
            TryRegisterHotSwapListener();
            TryRegisterBootstrap();
            if (!HectonXRRuntimeState.IsXRActive)
            {
                ClearBoundSocketState();
                return;
            }

            if (!TryResolveAndBindProvider(true))
                TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                if (ReferenceEquals(_runtime, this))
                    _runtime = null;
                return;
            }

            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            TryUnregisterBootstrap();
            if (ReferenceEquals(_runtime, this))
                _runtime = null;

            ClearBoundSocketState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService != null && _registeredSlowTick)
                {
                    TryUnregisterSlowTick();
                    TryRegisterSlowTick();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player && HectonXRRuntimeState.IsXRActive)
            {
                if (TryResolveAndBindProvider(true))
                    TryUnregisterSlowTick();
                else
                    TryRegisterSlowTick();
            }
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            if (!HectonXRRuntimeState.IsXRActive)
            {
                ReleaseRuntimeBindings();
                return;
            }

            if (TryResolveAndBindProvider(false))
                TryUnregisterSlowTick();
        }

        public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
        {
            if (!Application.isPlaying)
                return;

            if ((GameBootstrapperEventType)payload.EventType != GameBootstrapperEventType.GameReady)
                return;

            if (!HectonXRRuntimeState.IsXRActive)
                return;

            if (!TryResolveAndBindProvider(true))
                TryRegisterSlowTick();
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
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

        private void TryRegisterBootstrap()
        {
            if (_registeredBootstrap)
                return;

            GameBootstrapper.Register(this);
            _registeredBootstrap = true;
        }

        private void TryUnregisterBootstrap()
        {
            if (!_registeredBootstrap)
                return;

            GameBootstrapper.Unregister(this);
            _registeredBootstrap = false;
        }

        private static bool TryResolveAndBindProvider(bool allowSocketHierarchyLookup)
        {
            if (!HectonXRRuntimeState.IsXRActive)
                return false;

            VRSomaticRuntimeBootstrap runtime = _runtime;
            if (runtime == null)
                return false;

            if (!TryResolvePlayerContext(out PlayerRuntimeContext runtimeContext, out GameObject playerObject))
                return false;

            if (!playerObject.TryGetComponent(out VRSomaticProvider provider))
                provider = playerObject.AddComponent<VRSomaticProvider>(); // COLD ALLOC: VRSomaticProvider[1] - XR-only somatic suit provider attached to player root - owner: VRSomaticRuntimeBootstrap

            SomaticKinematicsRuntime.EnsureOnPlayerRoot(playerObject);

            Transform hmdTransform = ResolveHmdTransform(runtimeContext, playerObject.transform);
            if (hmdTransform == null)
                return false;

            Transform visorRoot = runtimeContext != null && runtimeContext.VisorController != null
                ? runtimeContext.VisorController.transform
                : null;
            Transform playerTransform = playerObject.transform;
            Transform pdaSocket = runtime.EnsureSocketTransformCached(
                playerTransform,
                PdaSocketName,
                allowSocketHierarchyLookup,
                ref runtime._pdaSocketTransform,
                ref runtime._createdPdaSocketTransform);
            Transform flareSocket = runtime.EnsureSocketTransformCached(
                playerTransform,
                FlareToolSocketName,
                allowSocketHierarchyLookup,
                ref runtime._flareSocketTransform,
                ref runtime._createdFlareSocketTransform);
            Transform vrRoot = runtime.EnsureDecoupledRootTransform();

            provider.BindRig(
                hmdTransform,
                visorRoot,
                pdaSocket,
                flareSocket,
                null,
                null);
            provider.BindDecoupledRoot(vrRoot);
            return true;
        }

        private static bool TryResolvePlayerContext(out PlayerRuntimeContext runtimeContext, out GameObject playerObject)
        {
            runtimeContext = null;
            playerObject = null;

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.PlayerObject != null)
            {
                playerObject = runtimeContext.PlayerObject;
                return true;
            }

            playerObject = GameBootstrapper.CurrentPlayerObject;
            if (playerObject != null)
                return true;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                playerObject = playerTransform.gameObject;
                return true;
            }

            return false;
        }

        private static Transform ResolveHmdTransform(PlayerRuntimeContext runtimeContext, Transform playerTransform)
        {
            if (runtimeContext != null)
            {
                if (runtimeContext.PlayerCamera != null)
                    return runtimeContext.PlayerCamera.transform;

                if (runtimeContext.PlayerMovement != null && runtimeContext.PlayerMovement.PlayerCameraTransform != null)
                    return runtimeContext.PlayerMovement.PlayerCameraTransform;
            }

            return playerTransform;
        }

        private Transform EnsureDecoupledRootTransform()
        {
            if (_vrRootTransform != null)
                return _vrRootTransform;

            if (_decoupledRootTransform != null)
            {
                _vrRootTransform = _decoupledRootTransform;
                _createdVrRootTransform = false;
                return _vrRootTransform;
            }

            if (_createdVrRootTransform && _vrRootTransform != null)
                Destroy(_vrRootTransform.gameObject);

            _vrRootTransform = null;
            _createdVrRootTransform = false;

            GameObject rootObject = new GameObject(DecoupledRootName); // COLD ALLOC: GameObject[1] - decoupled VR somatic root - owner: VRSomaticRuntimeBootstrap
            _vrRootTransform = rootObject.transform;
            _decoupledRootTransform = _vrRootTransform;
            _createdVrRootTransform = true;
            GameBootstrapper.PersistRuntimeService(_vrRootTransform);
            return _vrRootTransform;
        }

        private Transform EnsureSocketTransformCached(
            Transform playerTransform,
            string socketName,
            bool allowHierarchyLookup,
            ref Transform cachedSocket,
            ref bool createdSocket)
        {
            if (!ReferenceEquals(_boundPlayerTransform, playerTransform))
            {
                ClearBoundSocketState();
                _boundPlayerTransform = playerTransform;
            }

            if (cachedSocket != null && ReferenceEquals(cachedSocket.parent, playerTransform))
                return cachedSocket;

            DestroyCreatedSocket(ref cachedSocket, ref createdSocket);

            // COLD LOOKUP: only OnEnable/GameReady pass true; SlowTick fallback never scans hierarchy.
            if (allowHierarchyLookup)
            {
                Transform existingSocketTransform = playerTransform.Find(socketName);
                if (existingSocketTransform != null)
                {
                    cachedSocket = existingSocketTransform;
                    createdSocket = false;
                    return cachedSocket;
                }
            }

            GameObject socketObject = new GameObject(socketName); // COLD ALLOC: GameObject[1] - XR-only virtual chest socket anchor - owner: VRSomaticRuntimeBootstrap
            Transform socketTransform = socketObject.transform;
            socketTransform.SetParent(playerTransform, false);
            cachedSocket = socketTransform;
            createdSocket = true;
            return cachedSocket;
        }

        private void ClearBoundSocketState()
        {
            DestroyCreatedSocket(ref _pdaSocketTransform, ref _createdPdaSocketTransform);
            DestroyCreatedSocket(ref _flareSocketTransform, ref _createdFlareSocketTransform);
            _boundPlayerTransform = null;
        }

        private void ReleaseRuntimeBindings()
        {
            TryUnregisterSlowTick();
            ClearBoundSocketState();
        }

        private static void DestroyCreatedSocket(ref Transform socketTransform, ref bool createdSocket)
        {
            if (createdSocket && socketTransform != null)
                Destroy(socketTransform.gameObject);

            socketTransform = null;
            createdSocket = false;
        }
    }
}

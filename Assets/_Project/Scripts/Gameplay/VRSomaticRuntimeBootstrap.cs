using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9916)]
    internal sealed class VRSomaticRuntimeBootstrap : MonoBehaviour, ISlowTickable, ISceneBootstrapEventListener
    {
        private const string RuntimeOwnerName = "[VRSomaticRuntimeBootstrap]";
        private const string PdaSocketName = "VR_SomaticSocket_PDA";
        private const string FlareToolSocketName = "VR_SomaticSocket_FlareTool";

        private static VRSomaticRuntimeBootstrap _runtime;

        private Transform _boundPlayerTransform;
        private Transform _pdaSocketTransform;
        private Transform _flareSocketTransform;
        private bool _createdPdaSocketTransform;
        private bool _createdFlareSocketTransform;
        private bool _createdRuntimeObject;
        private bool _registeredSlowTick;
        private bool _registeredBootstrap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _runtime = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterXRActivationHook()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;

            if (HectonXRRuntimeState.IsXRActive)
                EnsureRuntimeAndBind();
        }

        private static void HandleXRActiveChanged(bool isActive)
        {
            if (isActive)
            {
                EnsureRuntimeAndBind();
                return;
            }

            ShutdownRuntime();
        }

        private static VRSomaticRuntimeBootstrap EnsureRuntime()
        {
            if (_runtime != null)
                return _runtime;

            GameObject runtimeObject = new GameObject(RuntimeOwnerName); // COLD ALLOC: GameObject[1] - XR-only somatic provider installer - owner: VRSomaticRuntimeBootstrap
            _runtime = runtimeObject.AddComponent<VRSomaticRuntimeBootstrap>(); // COLD ALLOC: VRSomaticRuntimeBootstrap[1] - XR-only somatic provider installer - owner: VRSomaticRuntimeBootstrap
            _runtime._createdRuntimeObject = true;
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

        private static void ShutdownRuntime()
        {
            VRSomaticRuntimeBootstrap runtime = _runtime;
            if (runtime == null)
                return;

            if (!runtime._createdRuntimeObject)
            {
                runtime.ReleaseRuntimeBindings();
                return;
            }

            _runtime = null;
            Destroy(runtime.gameObject);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _runtime = this;
            if (!HectonXRRuntimeState.IsXRActive)
            {
                ReleaseRuntimeBindings();
                return;
            }

            TryRegisterBootstrap();
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
            TryUnregisterBootstrap();
            if (ReferenceEquals(_runtime, this))
                _runtime = null;

            ClearBoundSocketState();
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            if (!HectonXRRuntimeState.IsXRActive)
            {
                ShutdownRuntime();
                return;
            }

            if (TryResolveAndBindProvider(false))
                TryUnregisterSlowTick();
        }

        public void OnSceneBootstrapEvent(in SceneBootstrapEventPayload payload)
        {
            if (!Application.isPlaying)
                return;

            if ((SceneBootstrapEventType)payload.EventType != SceneBootstrapEventType.GameReady)
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

        private void TryRegisterBootstrap()
        {
            if (_registeredBootstrap)
                return;

            SceneBootstrap.Register(this);
            _registeredBootstrap = true;
        }

        private void TryUnregisterBootstrap()
        {
            if (!_registeredBootstrap)
                return;

            SceneBootstrap.Unregister(this);
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

            provider.BindRig(
                hmdTransform,
                visorRoot,
                pdaSocket,
                flareSocket,
                null,
                null);
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

            playerObject = SceneBootstrap.CurrentPlayerObject;
            if (playerObject != null)
                return true;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
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
                Transform socketTransform = playerTransform.Find(socketName);
                if (socketTransform != null)
                {
                    cachedSocket = socketTransform;
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
            TryUnregisterBootstrap();
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

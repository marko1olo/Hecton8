using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    internal static class H8BridgeLiveSyncScheduler
    {
        private const int DesignRequestCapacity = 32;
        private const int InputRequestCapacity = 32;
        private const int PrefabRequestCapacity = 32;

        private static readonly Runner s_runner = new Runner(); // COLD ALLOC: Runner[1] - bridge live-sync LateFrame dispatcher - owner: H8BridgeLiveSyncScheduler
        private static readonly DesignRequest[] s_designRequests = new DesignRequest[DesignRequestCapacity]; // COLD ALLOC: DesignRequest[32] - fixed live design sync queue - owner: H8BridgeLiveSyncScheduler
        private static readonly InputRequest[] s_inputRequests = new InputRequest[InputRequestCapacity]; // COLD ALLOC: InputRequest[32] - fixed live input sync queue - owner: H8BridgeLiveSyncScheduler
        private static readonly PrefabRequest[] s_prefabRequests = new PrefabRequest[PrefabRequestCapacity]; // COLD ALLOC: PrefabRequest[32] - fixed live prefab bind queue - owner: H8BridgeLiveSyncScheduler

        private static int s_designRequestCount;
        private static int s_inputRequestCount;
        private static int s_prefabRequestCount;
        private static bool s_registered;
        private static bool s_hotSwapRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearDesignRequests();
            ClearInputRequests();
            ClearPrefabRequests();
            s_registered = false;
            s_hotSwapRegistered = false;
        }

        public static bool RequestDesignSync(
            H8DesignDataFacade facade,
            IDataVault vault,
            IMacroDatabaseService macroDatabase)
        {
            if (facade == null || vault == null || !Application.isPlaying)
                return false;

            if (!RegisterRunnerCold())
                return false;

            for (int i = 0; i < s_designRequestCount; i++)
            {
                if (ReferenceEquals(s_designRequests[i].Facade, facade))
                {
                    s_designRequests[i] = new DesignRequest(facade, vault, macroDatabase);
                    return true;
                }
            }

            if (s_designRequestCount >= DesignRequestCapacity)
                return false;

            s_designRequests[s_designRequestCount++] = new DesignRequest(facade, vault, macroDatabase);
            return true;
        }

        public static bool RequestInputSync(H8InputMappingFacade facade, IDataVault vault)
        {
            if (facade == null || vault == null || !Application.isPlaying)
                return false;

            if (!RegisterRunnerCold())
                return false;

            for (int i = 0; i < s_inputRequestCount; i++)
            {
                if (ReferenceEquals(s_inputRequests[i].Facade, facade))
                {
                    s_inputRequests[i] = new InputRequest(facade, vault);
                    return true;
                }
            }

            if (s_inputRequestCount >= InputRequestCapacity)
                return false;

            s_inputRequests[s_inputRequestCount++] = new InputRequest(facade, vault);
            return true;
        }

        public static bool RequestPrefabBind(
            H8PrefabRegistry registry,
            IDataVault vault,
            PrefabRegistry runtimeRegistry)
        {
            if (registry == null || vault == null || !Application.isPlaying)
                return false;

            if (!RegisterRunnerCold())
                return false;

            for (int i = 0; i < s_prefabRequestCount; i++)
            {
                if (ReferenceEquals(s_prefabRequests[i].Registry, registry))
                {
                    s_prefabRequests[i] = new PrefabRequest(registry, vault, runtimeRegistry);
                    return true;
                }
            }

            if (s_prefabRequestCount >= PrefabRequestCapacity)
                return false;

            s_prefabRequests[s_prefabRequestCount++] = new PrefabRequest(registry, vault, runtimeRegistry);
            return true;
        }

        private static bool RegisterRunnerCold()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

            if (!s_registered)
                s_registered = GlobalRegistry.TryRegisterLateFrameTickable(s_runner, PriorityLayer.Core);

            if (!s_hotSwapRegistered)
                s_hotSwapRegistered = GlobalRegistry.IsHotSwapListenerRegistered(s_runner) ||
                                      GlobalRegistry.TryRegisterHotSwapListener(s_runner);

            return s_registered && s_hotSwapRegistered;
        }

        private static bool HasPendingRequests()
        {
            return s_designRequestCount > 0 ||
                   s_inputRequestCount > 0 ||
                   s_prefabRequestCount > 0;
        }

        private static void HandleGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ClearDesignRequests();
                ClearInputRequests();
                ClearPrefabRequests();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MacroDatabase)
            {
                ClearDesignRequests();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                s_registered = false;
                if (HasPendingRequests())
                    RegisterRunnerCold();
            }
        }

        private static void FlushLateFrame()
        {
            FlushDesignRequests();
            FlushInputRequests();
            FlushPrefabRequests();
        }

        private static void FlushDesignRequests()
        {
            int count = s_designRequestCount;
            s_designRequestCount = 0;

            for (int i = 0; i < count; i++)
            {
                DesignRequest request = s_designRequests[i];
                s_designRequests[i] = default;

                if (request.Facade != null && request.Vault != null)
                    request.Facade.SyncToVaultExistingBuffer(request.Vault, request.MacroDatabase);
            }
        }

        private static void FlushInputRequests()
        {
            int count = s_inputRequestCount;
            s_inputRequestCount = 0;

            for (int i = 0; i < count; i++)
            {
                InputRequest request = s_inputRequests[i];
                s_inputRequests[i] = default;

                if (request.Facade != null && request.Vault != null)
                    request.Facade.SyncToVaultExistingBuffer(request.Vault);
            }
        }

        private static void FlushPrefabRequests()
        {
            int count = s_prefabRequestCount;
            s_prefabRequestCount = 0;

            for (int i = 0; i < count; i++)
            {
                PrefabRequest request = s_prefabRequests[i];
                s_prefabRequests[i] = default;

                if (request.Registry != null && request.Vault != null)
                    H8PrefabRegistryRuntimeBinder.BindExistingBuffers(request.Registry, request.Vault, request.RuntimeRegistry);
            }
        }

        private static void ClearDesignRequests()
        {
            for (int i = 0; i < s_designRequestCount; i++)
                s_designRequests[i] = default;

            s_designRequestCount = 0;
        }

        private static void ClearInputRequests()
        {
            for (int i = 0; i < s_inputRequestCount; i++)
                s_inputRequests[i] = default;

            s_inputRequestCount = 0;
        }

        private static void ClearPrefabRequests()
        {
            for (int i = 0; i < s_prefabRequestCount; i++)
                s_prefabRequests[i] = default;

            s_prefabRequestCount = 0;
        }

        private readonly struct DesignRequest
        {
            public readonly H8DesignDataFacade Facade;
            public readonly IDataVault Vault;
            public readonly IMacroDatabaseService MacroDatabase;

            public DesignRequest(
                H8DesignDataFacade facade,
                IDataVault vault,
                IMacroDatabaseService macroDatabase)
            {
                Facade = facade;
                Vault = vault;
                MacroDatabase = macroDatabase;
            }
        }

        private readonly struct InputRequest
        {
            public readonly H8InputMappingFacade Facade;
            public readonly IDataVault Vault;

            public InputRequest(H8InputMappingFacade facade, IDataVault vault)
            {
                Facade = facade;
                Vault = vault;
            }
        }

        private readonly struct PrefabRequest
        {
            public readonly H8PrefabRegistry Registry;
            public readonly IDataVault Vault;
            public readonly PrefabRegistry RuntimeRegistry;

            public PrefabRequest(
                H8PrefabRegistry registry,
                IDataVault vault,
                PrefabRegistry runtimeRegistry)
            {
                Registry = registry;
                Vault = vault;
                RuntimeRegistry = runtimeRegistry;
            }
        }

        private sealed class Runner : ILateFrameTickable, IGlobalRegistryHotSwapListener
        {
            public void LateFrameTick()
            {
                FlushLateFrame();
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                HandleGlobalRegistryServiceReplaced(serviceSlot);
            }
        }
    }
}

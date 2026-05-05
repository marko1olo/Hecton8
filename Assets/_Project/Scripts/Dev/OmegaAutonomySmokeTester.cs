#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton.UI.MainMenu;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Hecton8.Dev
{
    /// <summary>
    /// Dev-only smoke for the bounded Omega autonomy hardening pass.
    /// </summary>
    public static class OmegaAutonomySmokeTester
    {
        private const string NativeMemoryOwner = nameof(OmegaAutonomySmokeTester);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.TempJob;

        public static bool Run(out string json)
        {
            bool routePass = RunRouteBfsSmoke(
                out int routedNode,
                out int noStorageNode,
                out int cycleRouteNode);
            bool audioPass = RunProceduralAudioOverflowSmoke(
                out int pendingAfterAudioOverflow,
                out int droppedAudioPings,
                out int droppedStructuralStress);
            bool audioReentryPass = RunProceduralAudioReentrySmoke(
                out int pendingAfterFirstReentryFlush,
                out int pendingAfterSecondReentryFlush,
                out int reentryDispatchCount);
            bool inputGuardPass = RunMainMenuInputRoutingGuardSmoke(
                out bool inputModulePresent,
                out bool inputActionsBound,
                out bool legacyModuleRemoved);
            bool watchdogRegistryPass = RunRuntimeWatchdogRegistrySmoke(
                out bool watchdogRegistered,
                out bool watchdogUnregistered);
            bool burstClearPass = RunBurstClearSmoke(out int burstClearChecksum);

            bool pass = routePass &&
                        audioPass &&
                        audioReentryPass &&
                        inputGuardPass &&
                        watchdogRegistryPass &&
                        burstClearPass;
            json = "{"
                + "\"tester\":\"OmegaAutonomySmokeTester\","
                + "\"status\":\"" + (pass ? "PASS" : "FAIL") + "\","
                + "\"routeBfs\":{\"pass\":" + ToJsonBool(routePass)
                + ",\"routedNode\":" + routedNode
                + ",\"noStorageNode\":" + noStorageNode
                + ",\"cycleRouteNode\":" + cycleRouteNode + "},"
                + "\"proceduralAudioOverflow\":{\"pass\":" + ToJsonBool(audioPass)
                + ",\"pendingAfterAudioOverflow\":" + pendingAfterAudioOverflow
                + ",\"droppedAudioPings\":" + droppedAudioPings
                + ",\"droppedStructuralStress\":" + droppedStructuralStress + "},"
                + "\"proceduralAudioReentry\":{\"pass\":" + ToJsonBool(audioReentryPass)
                + ",\"pendingAfterFirstFlush\":" + pendingAfterFirstReentryFlush
                + ",\"pendingAfterSecondFlush\":" + pendingAfterSecondReentryFlush
                + ",\"dispatchCount\":" + reentryDispatchCount + "},"
                + "\"mainMenuInputGuard\":{\"pass\":" + ToJsonBool(inputGuardPass)
                + ",\"inputModulePresent\":" + ToJsonBool(inputModulePresent)
                + ",\"inputActionsBound\":" + ToJsonBool(inputActionsBound)
                + ",\"legacyModuleRemoved\":" + ToJsonBool(legacyModuleRemoved) + "},"
                + "\"runtimeWatchdogRegistry\":{\"pass\":" + ToJsonBool(watchdogRegistryPass)
                + ",\"registered\":" + ToJsonBool(watchdogRegistered)
                + ",\"unregistered\":" + ToJsonBool(watchdogUnregistered) + "},"
                + "\"burstClear\":{\"pass\":" + ToJsonBool(burstClearPass)
                + ",\"checksum\":" + burstClearChecksum + "}"
                + "}";
            return pass;
        }

        private static bool RunRouteBfsSmoke(
            out int routedNode,
            out int noStorageNode,
            out int cycleRouteNode)
        {
            routedNode = -1;
            noStorageNode = -1;
            cycleRouteNode = -1;
            NativeArray<int> edgeOffsets = new NativeArray<int>(5, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> edgeDestinations = new NativeArray<int>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> storageCapacityByNode = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<byte> visited = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> queue = new NativeArray<int>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> result = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(edgeOffsets, nameof(edgeOffsets));
            RegisterNativeArray(edgeDestinations, nameof(edgeDestinations));
            RegisterNativeArray(storageCapacityByNode, nameof(storageCapacityByNode));
            RegisterNativeArray(visited, nameof(visited));
            RegisterNativeArray(queue, nameof(queue));
            RegisterNativeArray(result, nameof(result));

            try
            {
                ConfigureSimpleRoute(edgeOffsets, edgeDestinations, storageCapacityByNode, hasStorage: true);
                ExecuteRouteBfs(3, edgeOffsets, edgeDestinations, storageCapacityByNode, visited, queue, result);
                routedNode = result[0];

                ConfigureSimpleRoute(edgeOffsets, edgeDestinations, storageCapacityByNode, hasStorage: false);
                ExecuteRouteBfs(3, edgeOffsets, edgeDestinations, storageCapacityByNode, visited, queue, result);
                noStorageNode = result[0];

                ConfigureCycleRoute(edgeOffsets, edgeDestinations, storageCapacityByNode);
                ExecuteRouteBfs(4, edgeOffsets, edgeDestinations, storageCapacityByNode, visited, queue, result);
                cycleRouteNode = result[0];

                return routedNode == 2 && noStorageNode == -1 && cycleRouteNode == 3;
            }
            finally
            {
                DisposeTrackedNativeArray(ref edgeOffsets);
                DisposeTrackedNativeArray(ref edgeDestinations);
                DisposeTrackedNativeArray(ref storageCapacityByNode);
                DisposeTrackedNativeArray(ref visited);
                DisposeTrackedNativeArray(ref queue);
                DisposeTrackedNativeArray(ref result);
            }
        }

        private static bool RunProceduralAudioOverflowSmoke(
            out int pendingAfterAudioOverflow,
            out int droppedAudioPings,
            out int droppedStructuralStress)
        {
            ProceduralAudioEvents.ResetForSmokeTest();

            for (int i = 0; i < 9; i++)
                ProceduralAudioEvents.RaiseAudioPingTriggered(Vector3.zero, 1f, 0.05f, 1f, 22000f, ProceduralAudioPingKind.Sonar);

            pendingAfterAudioOverflow = ProceduralAudioEvents.PendingCount;
            droppedAudioPings = ProceduralAudioEvents.DroppedAudioPingCount;
            bool audioOverflowPass = pendingAfterAudioOverflow == 8 && droppedAudioPings == 1;

            ProceduralAudioEvents.FlushPending();
            for (int i = 0; i < 9; i++)
                ProceduralAudioEvents.RaiseStructuralStressTriggered(Vector3.zero, 1f, 1f);

            droppedStructuralStress = ProceduralAudioEvents.DroppedStructuralStressCount;
            bool structuralOverflowPass = ProceduralAudioEvents.PendingCount == 8 && droppedStructuralStress == 1;
            ProceduralAudioEvents.ResetForSmokeTest();
            return audioOverflowPass && structuralOverflowPass;
        }

        private static bool RunProceduralAudioReentrySmoke(
            out int pendingAfterFirstFlush,
            out int pendingAfterSecondFlush,
            out int dispatchCount)
        {
            ProceduralAudioEvents.ResetForSmokeTest();
            // COLD ALLOC: ReentrantAudioListener[1] - dev-only procedural audio reentry probe - owner: OmegaAutonomySmokeTester
            ReentrantAudioListener listener = new ReentrantAudioListener();
            ProceduralAudioEvents.Register(listener);
            ProceduralAudioEvents.RaiseAudioPingTriggered(Vector3.zero, 1f, 0.05f, 1f, 22000f, ProceduralAudioPingKind.Sonar);
            ProceduralAudioEvents.FlushPending();
            pendingAfterFirstFlush = ProceduralAudioEvents.PendingCount;
            ProceduralAudioEvents.FlushPending();
            pendingAfterSecondFlush = ProceduralAudioEvents.PendingCount;
            dispatchCount = listener.PingDispatchCount;
            ProceduralAudioEvents.ResetForSmokeTest();
            return pendingAfterFirstFlush == 1 && pendingAfterSecondFlush == 0 && dispatchCount == 2;
        }

        private static bool RunMainMenuInputRoutingGuardSmoke(
            out bool inputModulePresent,
            out bool inputActionsBound,
            out bool legacyModuleRemoved)
        {
            inputModulePresent = false;
            inputActionsBound = false;
            legacyModuleRemoved = false;

            GameObject eventSystemRoot = null;
            try
            {
                eventSystemRoot = new GameObject(
                    "OmegaSmoke_EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule)); // COLD ALLOC: GameObject[1] - editor smoke EventSystem route probe - owner: OmegaAutonomySmokeTester

                MainMenuInputRoutingGuard.EnsureInputSystemEventRouting();

                InputSystemUIInputModule inputModule = eventSystemRoot.GetComponent<InputSystemUIInputModule>();
                StandaloneInputModule legacyInputModule = eventSystemRoot.GetComponent<StandaloneInputModule>();
                inputModulePresent = inputModule != null && inputModule.enabled;
                inputActionsBound = MainMenuInputRoutingGuard.HasUsableUiModuleActions(inputModule);
                legacyModuleRemoved = legacyInputModule == null || !legacyInputModule.enabled;
                return inputModulePresent && inputActionsBound && legacyModuleRemoved;
            }
            finally
            {
                if (eventSystemRoot != null)
                    Object.DestroyImmediate(eventSystemRoot);
            }
        }

        private static bool RunRuntimeWatchdogRegistrySmoke(
            out bool registered,
            out bool unregistered)
        {
            registered = false;
            unregistered = false;

            if (GlobalRegistry.RuntimeWatchdog != null)
            {
                registered = true;
                return true;
            }

            GameObject watchdogRoot = null;
            try
            {
                watchdogRoot = new GameObject("OmegaSmoke_RuntimeWatchdog"); // COLD ALLOC: GameObject[1] - editor smoke watchdog registry owner - owner: OmegaAutonomySmokeTester
                RuntimeWatchdog watchdog = watchdogRoot.AddComponent<RuntimeWatchdog>();
                watchdog.InitializeService();
                registered = ReferenceEquals(GlobalRegistry.RuntimeWatchdog, watchdog);
            }
            finally
            {
                if (watchdogRoot != null)
                    Object.DestroyImmediate(watchdogRoot);
            }

            unregistered = GlobalRegistry.RuntimeWatchdog == null;
            return registered && unregistered;
        }

        private static bool RunBurstClearSmoke(out int checksum)
        {
            checksum = 0;
            NativeArray<int> intValues = new NativeArray<int>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> byteValues = new NativeArray<byte>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            RegisterNativeArray(intValues, nameof(intValues));
            RegisterNativeArray(byteValues, nameof(byteValues));

            try
            {
                for (int i = 0; i < intValues.Length; i++)
                {
                    intValues[i] = i + 1;
                    byteValues[i] = (byte)(i + 1);
                }

                ClearIntArray(intValues);
                ClearByteArray(byteValues);

                for (int i = 0; i < intValues.Length; i++)
                    checksum += intValues[i] + byteValues[i];

                return checksum == 0;
            }
            finally
            {
                DisposeTrackedNativeArray(ref intValues);
                DisposeTrackedNativeArray(ref byteValues);
            }
        }

        private static void ConfigureSimpleRoute(
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> storageCapacityByNode,
            bool hasStorage)
        {
            ClearIntArray(edgeOffsets);
            ClearIntArray(edgeDestinations);
            ClearByteArray(storageCapacityByNode);
            edgeOffsets[0] = 0;
            edgeOffsets[1] = 1;
            edgeOffsets[2] = 2;
            edgeOffsets[3] = 2;
            edgeDestinations[0] = 1;
            edgeDestinations[1] = 2;
            storageCapacityByNode[2] = hasStorage ? (byte)1 : (byte)0;
        }

        private static void ConfigureCycleRoute(
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> storageCapacityByNode)
        {
            ClearIntArray(edgeOffsets);
            ClearIntArray(edgeDestinations);
            ClearByteArray(storageCapacityByNode);
            edgeOffsets[0] = 0;
            edgeOffsets[1] = 1;
            edgeOffsets[2] = 2;
            edgeOffsets[3] = 4;
            edgeOffsets[4] = 4;
            edgeDestinations[0] = 1;
            edgeDestinations[1] = 2;
            edgeDestinations[2] = 1;
            edgeDestinations[3] = 3;
            storageCapacityByNode[3] = 1;
        }

        private static void ExecuteRouteBfs(
            int nodeCount,
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> storageCapacityByNode,
            NativeArray<byte> visited,
            NativeArray<int> queue,
            NativeArray<int> result)
        {
            result[0] = -1;
            BaseLogisticsNetwork.ExecuteLogisticsRouteBfs(
                nodeCount,
                0,
                edgeOffsets,
                edgeDestinations,
                storageCapacityByNode,
                visited,
                queue,
                result);
        }

        private static void ClearIntArray(NativeArray<int> values)
        {
            new ClearIntArrayJob
            {
                Values = values
            }.Schedule(values.Length, 32).Complete();
        }

        private static void ClearByteArray(NativeArray<byte> values)
        {
            new ClearByteArrayJob
            {
                Values = values
            }.Schedule(values.Length, 32).Complete();
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        [BurstCompile]
        private struct ClearIntArrayJob : IJobParallelFor
        {
            public NativeArray<int> Values;

            public void Execute(int index)
            {
                Values[index] = 0;
            }
        }

        [BurstCompile]
        private struct ClearByteArrayJob : IJobParallelFor
        {
            public NativeArray<byte> Values;

            public void Execute(int index)
            {
                Values[index] = 0;
            }
        }

        private sealed class ReentrantAudioListener : IProceduralAudioEventListener
        {
            private bool _raisedReentrantPing;

            public int PingDispatchCount { get; private set; }

            public void OnAudioPingTriggered(in AudioPingTriggerInfo info)
            {
                PingDispatchCount++;
                if (_raisedReentrantPing)
                    return;

                _raisedReentrantPing = true;
                ProceduralAudioEvents.RaiseAudioPingTriggered(Vector3.zero, 0.5f, 0.025f, 1f, 22000f, ProceduralAudioPingKind.Sonar);
            }

            public void OnStructuralStressTriggered(in StructuralStressAudioInfo info)
            {
            }
        }
    }
}
#endif

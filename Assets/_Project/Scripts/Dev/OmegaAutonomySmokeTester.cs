#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.World;
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
        private static readonly uint s_NativeSentinelImbalanceWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("OmegaAutonomySmokeTester.NativeSentinelImbalance"));
        private static readonly uint s_NativeSentinelBalanceContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("OmegaAutonomySmokeTester.NativeSentinelBalance"));
        // COLD ALLOC: List<InputSystemUIInputModule>[4] - editor smoke component scratch - owner: OmegaAutonomySmokeTester
        private static readonly List<InputSystemUIInputModule> s_InputSystemModulesScratch = new List<InputSystemUIInputModule>(4);

        public static bool Run(out string json)
        {
            int nativeAllocationsBefore = NativeMemorySentinel.ActiveAllocationCount;
            long nativeBytesBefore = NativeMemorySentinel.TrackedBytes;
            bool globalTelemetryWasInitialized = GlobalTelemetryBus.IsInitializedForSmoke;

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
                out bool legacyModuleRemoved,
                out bool inputRepairTelemetryPublished,
                out int inputRepairTelemetryCode,
                out int inputRepairTelemetryPublishCount,
                out bool inputRepairTelemetryDeduped,
                out int inputModuleCountAfterSecondRepair);
            bool watchdogRegistryPass = RunRuntimeWatchdogRegistrySmoke(
                out bool watchdogRegistered,
                out bool watchdogUnregistered);
            bool registryHijackPass = RunRegistrySlotHijackSmoke(out bool registryHijackBlocked);
            bool burstClearPass = RunBurstClearSmoke(
                out int burstClearChecksum,
                out int burstClearNativeAllocationDelta);
            Hecton8.Debugging.AutomationOmegaSmokeResult constructionAutomationResult =
                Hecton8.Debugging.AutomationOmegaSmokeTester.RunLogisticsRouteStressSmoke();
            bool constructionAutomationPass = constructionAutomationResult.Passed != 0;
            if (!globalTelemetryWasInitialized)
                GlobalTelemetryBus.ResetForSmokeTest();

            int nativeAllocationDelta = NativeMemorySentinel.ActiveAllocationCount - nativeAllocationsBefore;
            long nativeTrackedByteDelta = NativeMemorySentinel.TrackedBytes - nativeBytesBefore;
            bool nativeSentinelBalancePass = nativeAllocationDelta == 0 && nativeTrackedByteDelta == 0L;
            bool nativeSentinelWarningRequested = false;
            if (!nativeSentinelBalancePass)
            {
                nativeSentinelWarningRequested = true;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    s_NativeSentinelImbalanceWarningHash,
                    s_NativeSentinelBalanceContextHash,
                    nativeAllocationDelta);
                if (!globalTelemetryWasInitialized)
                    GlobalTelemetryBus.ResetForSmokeTest();
            }

            bool pass = routePass &&
                        audioPass &&
                        audioReentryPass &&
                        inputGuardPass &&
                        watchdogRegistryPass &&
                        registryHijackPass &&
                        burstClearPass &&
                        constructionAutomationPass &&
                        nativeSentinelBalancePass;
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
                + ",\"legacyModuleRemoved\":" + ToJsonBool(legacyModuleRemoved)
                + ",\"repairTelemetryPublished\":" + ToJsonBool(inputRepairTelemetryPublished)
                + ",\"repairTelemetryCode\":" + inputRepairTelemetryCode
                + ",\"repairTelemetryPublishCount\":" + inputRepairTelemetryPublishCount
                + ",\"repairTelemetryDeduped\":" + ToJsonBool(inputRepairTelemetryDeduped)
                + ",\"moduleCountAfterSecondRepair\":" + inputModuleCountAfterSecondRepair + "},"
                + "\"runtimeWatchdogRegistry\":{\"pass\":" + ToJsonBool(watchdogRegistryPass)
                + ",\"registered\":" + ToJsonBool(watchdogRegistered)
                + ",\"unregistered\":" + ToJsonBool(watchdogUnregistered) + "},"
                + "\"registrySlotHijack\":{\"pass\":" + ToJsonBool(registryHijackPass)
                + ",\"blocked\":" + ToJsonBool(registryHijackBlocked) + "},"
                + "\"burstClear\":{\"pass\":" + ToJsonBool(burstClearPass)
                + ",\"checksum\":" + burstClearChecksum
                + ",\"nativeAllocationDelta\":" + burstClearNativeAllocationDelta + "},"
                + "\"constructionAutomation\":{\"pass\":" + ToJsonBool(constructionAutomationPass)
                + ",\"nodes\":" + constructionAutomationResult.NodeCount
                + ",\"edges\":" + constructionAutomationResult.EdgeCount
                + ",\"routedNode\":" + constructionAutomationResult.RoutedNode
                + ",\"expectedStorageNode\":" + constructionAutomationResult.ExpectedStorageNode
                + ",\"noStorageRouteNode\":" + constructionAutomationResult.NoStorageRouteNode
                + ",\"invalidStartRouteNode\":" + constructionAutomationResult.InvalidStartRouteNode + "},"
                + "\"nativeSentinelBalance\":{\"pass\":" + ToJsonBool(nativeSentinelBalancePass)
                + ",\"allocationDelta\":" + nativeAllocationDelta
                + ",\"trackedByteDelta\":" + nativeTrackedByteDelta
                + ",\"telemetryWarningRequested\":" + ToJsonBool(nativeSentinelWarningRequested)
                + ",\"telemetryWasInitializedBefore\":" + ToJsonBool(globalTelemetryWasInitialized) + "}"
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
            NativeArray<int> edgeOffsets = AllocateTrackedNativeArray<int>(5, nameof(edgeOffsets), NativeArrayOptions.ClearMemory);
            NativeArray<int> edgeDestinations = AllocateTrackedNativeArray<int>(4, nameof(edgeDestinations), NativeArrayOptions.ClearMemory);
            NativeArray<byte> storageCapacityByNode = AllocateTrackedNativeArray<byte>(4, nameof(storageCapacityByNode), NativeArrayOptions.ClearMemory);
            NativeArray<byte> visited = AllocateTrackedNativeArray<byte>(4, nameof(visited), NativeArrayOptions.ClearMemory);
            NativeArray<int> queue = AllocateTrackedNativeArray<int>(4, nameof(queue), NativeArrayOptions.ClearMemory);
            NativeArray<int> result = AllocateTrackedNativeArray<int>(1, nameof(result), NativeArrayOptions.ClearMemory);

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
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(Vector3.zero, 1f, 0.05f, 1f, 22000f, ProceduralAudioPingKind.Sonar);

            pendingAfterAudioOverflow = ProceduralAudioEvents.PendingCount;
            droppedAudioPings = ProceduralAudioEvents.DroppedAudioPingCount;
            bool audioOverflowPass = pendingAfterAudioOverflow == 8 && droppedAudioPings == 1;

            ProceduralAudioEvents.FlushPending();
            for (int i = 0; i < 9; i++)
                ProceduralAudioEvents.TryRaiseStructuralStressTriggered(Vector3.zero, 1f, 1f);

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
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(Vector3.zero, 1f, 0.05f, 1f, 22000f, ProceduralAudioPingKind.Sonar);
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
            out bool legacyModuleRemoved,
            out bool repairTelemetryPublished,
            out int repairTelemetryCode,
            out int repairTelemetryPublishCount,
            out bool repairTelemetryDeduped,
            out int moduleCountAfterSecondRepair)
        {
            inputModulePresent = false;
            inputActionsBound = false;
            legacyModuleRemoved = false;
            repairTelemetryPublished = false;
            repairTelemetryCode = 0;
            repairTelemetryPublishCount = 0;
            repairTelemetryDeduped = false;
            moduleCountAfterSecondRepair = 0;

            GameObject eventSystemRoot = null;
            try
            {
                MainMenuInputRoutingGuard.ResetForSmokeTest();
                eventSystemRoot = new GameObject(
                    "OmegaSmoke_EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule)); // COLD ALLOC: GameObject[1] - editor smoke EventSystem route probe - owner: OmegaAutonomySmokeTester

                eventSystemRoot.TryGetComponent(out EventSystem eventSystem);
                MainMenuInputRoutingGuard.EnsureInputSystemEventRouting(eventSystem);
                int publishCountAfterFirstRepair = MainMenuInputRoutingGuard.RepairTelemetryPublishCountForSmoke;
                MainMenuInputRoutingGuard.EnsureInputSystemEventRouting(eventSystem);
                eventSystemRoot.GetComponents(s_InputSystemModulesScratch);
                moduleCountAfterSecondRepair = s_InputSystemModulesScratch.Count;
                s_InputSystemModulesScratch.Clear();

                eventSystemRoot.TryGetComponent(out InputSystemUIInputModule inputModule);
                eventSystemRoot.TryGetComponent(out StandaloneInputModule legacyInputModule);
                inputModulePresent = inputModule != null && inputModule.enabled;
                inputActionsBound = MainMenuInputRoutingGuard.HasUsableUiModuleActions(inputModule);
                legacyModuleRemoved = legacyInputModule == null || !legacyInputModule.enabled;
                repairTelemetryPublished = MainMenuInputRoutingGuard.RepairTelemetryPublishedForSmoke;
                repairTelemetryCode = MainMenuInputRoutingGuard.LastRepairTelemetryCodeForSmoke;
                repairTelemetryPublishCount = MainMenuInputRoutingGuard.RepairTelemetryPublishCountForSmoke;
                repairTelemetryDeduped = publishCountAfterFirstRepair == 1 && repairTelemetryPublishCount == 1;
                return inputModulePresent &&
                       inputActionsBound &&
                       legacyModuleRemoved &&
                       repairTelemetryPublished &&
                       repairTelemetryCode == 14 &&
                       repairTelemetryDeduped &&
                       moduleCountAfterSecondRepair == 1;
            }
            finally
            {
                s_InputSystemModulesScratch.Clear();
                if (eventSystemRoot != null)
                    UnityEngine.Object.DestroyImmediate(eventSystemRoot);

                MainMenuInputRoutingGuard.ResetForSmokeTest();
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
            RuntimeWatchdog watchdog = null;
            try
            {
                watchdogRoot = new GameObject("OmegaSmoke_RuntimeWatchdog"); // COLD ALLOC: GameObject[1] - editor smoke watchdog registry owner - owner: OmegaAutonomySmokeTester
                watchdog = watchdogRoot.AddComponent<RuntimeWatchdog>();
                watchdog.InitializeService();
                registered = ReferenceEquals(GlobalRegistry.RuntimeWatchdog, watchdog);
            }
            finally
            {
                if (watchdog != null && ReferenceEquals(GlobalRegistry.RuntimeWatchdog, watchdog))
                    GlobalRegistry.UnregisterRuntimeWatchdogRuntime(watchdog);

                if (watchdogRoot != null)
                    UnityEngine.Object.DestroyImmediate(watchdogRoot);
            }

            unregistered = GlobalRegistry.RuntimeWatchdog == null;
            return registered && unregistered;
        }

        private static bool RunRegistrySlotHijackSmoke(out bool hijackBlocked)
        {
            hijackBlocked = false;
            if (GlobalRegistry.RuntimeWatchdog != null)
                return false;

            GameObject primaryRoot = null;
            GameObject hijackRoot = null;
            RuntimeWatchdog primary = null;
            RuntimeWatchdog hijack = null;
            try
            {
                primaryRoot = new GameObject("OmegaSmoke_RuntimeWatchdog_Primary"); // COLD ALLOC: GameObject[1] - editor smoke registry hijack primary - owner: OmegaAutonomySmokeTester
                primary = primaryRoot.AddComponent<RuntimeWatchdog>();
                primary.InitializeService();
                bool primaryRegistered = ReferenceEquals(GlobalRegistry.RuntimeWatchdog, primary);

                hijackRoot = new GameObject("OmegaSmoke_RuntimeWatchdog_Hijack"); // COLD ALLOC: GameObject[1] - editor smoke registry hijack attempt - owner: OmegaAutonomySmokeTester
                hijack = hijackRoot.AddComponent<RuntimeWatchdog>();
                try
                {
                    hijack.InitializeService();
                }
                catch (InvalidOperationException)
                {
                    hijackBlocked = true;
                }

                return primaryRegistered &&
                       hijackBlocked &&
                       ReferenceEquals(GlobalRegistry.RuntimeWatchdog, primary);
            }
            finally
            {
                if (hijack != null && ReferenceEquals(GlobalRegistry.RuntimeWatchdog, hijack))
                    GlobalRegistry.UnregisterRuntimeWatchdogRuntime(hijack);

                if (primary != null && ReferenceEquals(GlobalRegistry.RuntimeWatchdog, primary))
                    GlobalRegistry.UnregisterRuntimeWatchdogRuntime(primary);

                if (hijackRoot != null)
                    UnityEngine.Object.DestroyImmediate(hijackRoot);

                if (primaryRoot != null)
                    UnityEngine.Object.DestroyImmediate(primaryRoot);
            }
        }

        private static bool RunBurstClearSmoke(out int checksum, out int nativeAllocationDelta)
        {
            checksum = 0;
            nativeAllocationDelta = 0;
            int allocationsBefore = NativeMemorySentinel.ActiveAllocationCount;
            bool arraysCleared = false;
            NativeArray<int> intValues = AllocateTrackedNativeArray<int>(8, nameof(intValues), NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> byteValues = AllocateTrackedNativeArray<byte>(8, nameof(byteValues), NativeArrayOptions.UninitializedMemory);
            NativeArray<int> checksumTerms = AllocateTrackedNativeArray<int>(8, nameof(checksumTerms), NativeArrayOptions.UninitializedMemory);
            NativeArray<int> checksumResult = AllocateTrackedNativeArray<int>(1, nameof(checksumResult), NativeArrayOptions.ClearMemory);

            try
            {
                FillBurstClearArrays(intValues, byteValues);

                ClearIntArray(intValues);
                ClearByteArray(byteValues);

                ComputeBurstClearChecksum(intValues, byteValues, checksumTerms, checksumResult);
                checksum = checksumResult[0];
                arraysCleared = checksum == 0;
            }
            finally
            {
                DisposeTrackedNativeArray(ref intValues);
                DisposeTrackedNativeArray(ref byteValues);
                DisposeTrackedNativeArray(ref checksumTerms);
                DisposeTrackedNativeArray(ref checksumResult);
            }

            nativeAllocationDelta = NativeMemorySentinel.ActiveAllocationCount - allocationsBefore;
            return arraysCleared && nativeAllocationDelta == 0;
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
            LogisticsPipeRoutingKernel.ExecuteRouteBfs(
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
            JobHandle handle = new ClearIntArrayJob
            {
                Values = values
            }.Schedule(values.Length, 32);
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        }

        private static void ClearByteArray(NativeArray<byte> values)
        {
            JobHandle handle = new ClearByteArrayJob
            {
                Values = values
            }.Schedule(values.Length, 32);
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        }

        private static void FillBurstClearArrays(NativeArray<int> intValues, NativeArray<byte> byteValues)
        {
            JobHandle handle = new FillBurstClearArraysJob
            {
                IntValues = intValues,
                ByteValues = byteValues
            }.Schedule(intValues.Length, 32);
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        }

        private static void ComputeBurstClearChecksum(
            NativeArray<int> intValues,
            NativeArray<byte> byteValues,
            NativeArray<int> checksumTerms,
            NativeArray<int> checksumResult)
        {
            JobHandle handle = new BurstClearChecksumTermsJob
            {
                IntValues = intValues,
                ByteValues = byteValues,
                ChecksumTerms = checksumTerms
            }.Schedule(intValues.Length, 32);

            handle = new BurstClearChecksumSummaryJob
            {
                ChecksumTerms = checksumTerms,
                ChecksumResult = checksumResult
            }.Schedule(handle);
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, string label, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
                if (sentinelId > 0)
                    return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();

                throw;
            }

            array.Dispose();
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct FillBurstClearArraysJob : IJobParallelFor
        {
            public NativeArray<int> IntValues;
            public NativeArray<byte> ByteValues;

            public void Execute(int index)
            {
                int value = index + 1;
                IntValues[index] = value;
                ByteValues[index] = (byte)value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearIntArrayJob : IJobParallelFor
        {
            public NativeArray<int> Values;

            public void Execute(int index)
            {
                Values[index] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearByteArrayJob : IJobParallelFor
        {
            public NativeArray<byte> Values;

            public void Execute(int index)
            {
                Values[index] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BurstClearChecksumTermsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> IntValues;
            [ReadOnly] public NativeArray<byte> ByteValues;
            [WriteOnly] public NativeArray<int> ChecksumTerms;

            public void Execute(int index)
            {
                ChecksumTerms[index] = IntValues[index] + ByteValues[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BurstClearChecksumSummaryJob : IJob
        {
            [ReadOnly] public NativeArray<int> ChecksumTerms;
            [WriteOnly] public NativeArray<int> ChecksumResult;

            public void Execute()
            {
                int checksum = 0;
                for (int i = 0; i < ChecksumTerms.Length; i++)
                    checksum += ChecksumTerms[i];

                ChecksumResult[0] = checksum;
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
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(Vector3.zero, 0.5f, 0.025f, 1f, 22000f, ProceduralAudioPingKind.Sonar);
            }

            public void OnStructuralStressTriggered(in StructuralStressAudioInfo info)
            {
            }
        }
    }
}
#endif

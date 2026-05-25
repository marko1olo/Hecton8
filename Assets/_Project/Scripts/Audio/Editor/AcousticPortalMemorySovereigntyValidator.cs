#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Audio.Propagation;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.Audio.Editor
{
    [InitializeOnLoad]
    public static class AcousticPortalMemorySovereigntyValidator
    {
        private const int StressSourceCount = 4096;
        private const int StressProbeCount = 64;
        private const uint StressSeed = 0x13071307u;
        private const uint FailureLayout = 1u << 0;
        private const uint FailureHandle = 1u << 1;
        private const uint FailureLock = 1u << 2;
        private const uint FailureStressJob = 1u << 3;
        private const uint FailureDefrag = 1u << 4;

        static AcousticPortalMemorySovereigntyValidator()
        {
            ValidateLayoutsOrThrow();
        }

        [MenuItem("HECTON-8/Audio/Run Acoustic Portal Memory Sovereignty Validator 1307")]
        public static void RunMenu()
        {
            ValidateLayoutsOrThrow();
            if (!RunDefragRaceFuzzer(out uint failureFlags))
                throw new FatalArchitectureException("1307 acoustic portal memory sovereignty validator failed.");

            H8Debug.Log("[1307] Acoustic portal memory sovereignty validator passed.");
        }

        public static bool RunDefragRaceFuzzer(out uint failureFlags)
        {
            failureFlags = 0u;
            NativeArray<AcousticPathQuery> queries = default;

            using GlobalDataVault vault = GlobalDataVault.Create(128, 16L * 1024L * 1024L);
            VaultGenerationHandle<AcousticPortalNode> nodeHandle = vault.EnsureGenerationHandle<AcousticPortalNode>(
                BufferID.SpatialAudioPortalNodes,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AcousticPortalEdge> edgeHandle = vault.EnsureGenerationHandle<AcousticPortalEdge>(
                BufferID.SpatialAudioPortalEdges,
                AcousticPortalConstants.MaxPathEdges,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AcousticPathResult> resultHandle = vault.EnsureGenerationHandle<AcousticPathResult>(
                BufferID.SpatialAudioPortalResult,
                1,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<int> openHandle = vault.EnsureGenerationHandle<int>(
                BufferID.SpatialAudioPortalOpenSet,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<int> closedHandle = vault.EnsureGenerationHandle<int>(
                BufferID.SpatialAudioPortalClosedSet,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<float> costHandle = vault.EnsureGenerationHandle<float>(
                BufferID.SpatialAudioPortalCosts,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<int> cameFromHandle = vault.EnsureGenerationHandle<int>(
                BufferID.SpatialAudioPortalCameFrom,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<byte> stateHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SpatialAudioPortalStates,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);

            if (nodeHandle.BufferID == 0u ||
                edgeHandle.BufferID == 0u ||
                resultHandle.BufferID == 0u ||
                openHandle.BufferID == 0u ||
                closedHandle.BufferID == 0u ||
                costHandle.BufferID == 0u ||
                cameFromHandle.BufferID == 0u ||
                stateHandle.BufferID == 0u)
            {
                failureFlags |= FailureHandle;
                return false;
            }

            bool nodesLocked = false;
            bool edgesLocked = false;
            bool resultLocked = false;
            bool openLocked = false;
            bool closedLocked = false;
            bool costsLocked = false;
            bool cameFromLocked = false;
            bool statesLocked = false;

            try
            {
                if (!vault.TryAcquireWriteLock(in nodeHandle, SystemID.Audio, out NativeArray<AcousticPortalNode> nodes))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                nodesLocked = true;
                if (!vault.TryAcquireWriteLock(in edgeHandle, SystemID.Audio, out NativeArray<AcousticPortalEdge> edges))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                edgesLocked = true;
                if (!vault.TryAcquireWriteLock(in resultHandle, SystemID.Audio, out NativeArray<AcousticPathResult> result))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                resultLocked = true;
                if (!vault.TryAcquireWriteLock(in openHandle, SystemID.Audio, out NativeArray<int> openSet))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                openLocked = true;
                if (!vault.TryAcquireWriteLock(in closedHandle, SystemID.Audio, out NativeArray<int> closedSet))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                closedLocked = true;
                if (!vault.TryAcquireWriteLock(in costHandle, SystemID.Audio, out NativeArray<float> costs))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                costsLocked = true;
                if (!vault.TryAcquireWriteLock(in cameFromHandle, SystemID.Audio, out NativeArray<int> cameFrom))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                cameFromLocked = true;
                if (!vault.TryAcquireWriteLock(in stateHandle, SystemID.Audio, out NativeArray<byte> states))
                {
                    failureFlags |= FailureLock;
                    return false;
                }

                statesLocked = true;
                queries = new NativeArray<AcousticPathQuery>(
                    StressSourceCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                GenerateMockAcousticLoadJob loadJob = new GenerateMockAcousticLoadJob
                {
                    Nodes = nodes,
                    Edges = edges,
                    QueryOutput = queries,
                    RequestedNodeCount = AcousticPortalConstants.MaxPathNodes,
                    Seed = StressSeed,
                    GlobalQualityWeight = 1f,
                    DisablePortalPath = 0
                };

                JobHandle loadHandle = loadJob.Schedule();
                vault.RequestEditorForceDefragmentation();
                vault.FrostTickDefrag(1f / 60f, 1f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                loadHandle.Complete();

                int queryCount = queries.Length;
                if (queryCount <= 0)
                {
                    failureFlags |= FailureStressJob;
                    return false;
                }

                AcousticPathJob pathJob = new AcousticPathJob
                {
                    Nodes = nodes,
                    Edges = edges,
                    OpenSet = openSet,
                    ClosedSet = closedSet,
                    Costs = costs,
                    CameFrom = cameFrom,
                    States = states,
                    Result = result
                };

                int probeCount = math.min(StressProbeCount, queryCount);
                for (int i = 0; i < probeCount; i++)
                {
                    AcousticPathQuery query = queries[(i * 67) % queryCount];
                    if (!AcousticAup.IsFinite(in query.SourceAup) ||
                        !AcousticAup.IsFinite(in query.ListenerAup) ||
                        query.NodeCount != AcousticPortalConstants.MaxPathNodes)
                    {
                        failureFlags |= FailureStressJob;
                        return false;
                    }

                    pathJob.Query = query;
                    JobHandle pathHandle = pathJob.Schedule();
                    vault.RequestEditorForceDefragmentation();
                    vault.FrostTickDefrag(1f / 60f, 1f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
                    pathHandle.Complete();
                    if (result[0].Status != AcousticPathStatus.PathFound ||
                        !math.isfinite(result[0].TrueDistanceMeters))
                    {
                        failureFlags |= FailureStressJob;
                        return false;
                    }
                }
            }
            finally
            {
                if (queries.IsCreated)
                    queries.Dispose();
                if (statesLocked)
                    vault.ReleaseWriteLock(in stateHandle, SystemID.Audio);
                if (cameFromLocked)
                    vault.ReleaseWriteLock(in cameFromHandle, SystemID.Audio);
                if (costsLocked)
                    vault.ReleaseWriteLock(in costHandle, SystemID.Audio);
                if (closedLocked)
                    vault.ReleaseWriteLock(in closedHandle, SystemID.Audio);
                if (openLocked)
                    vault.ReleaseWriteLock(in openHandle, SystemID.Audio);
                if (resultLocked)
                    vault.ReleaseWriteLock(in resultHandle, SystemID.Audio);
                if (edgesLocked)
                    vault.ReleaseWriteLock(in edgeHandle, SystemID.Audio);
                if (nodesLocked)
                    vault.ReleaseWriteLock(in nodeHandle, SystemID.Audio);
            }

            bool relocated = vault.GenerateMockVaultRelocationForValidation(
                StressSeed,
                AcousticPortalConstants.MaxPathNodes,
                MemoryDefragPhase.PreSimulation,
                vault.ActiveBurstLockMask);
            nodeHandle = vault.EnsureGenerationHandle<AcousticPortalNode>(
                BufferID.SpatialAudioPortalNodes,
                AcousticPortalConstants.MaxPathNodes,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            edgeHandle = vault.EnsureGenerationHandle<AcousticPortalEdge>(
                BufferID.SpatialAudioPortalEdges,
                AcousticPortalConstants.MaxPathEdges,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            if (!relocated ||
                !vault.TryReadOnlyHandle(in nodeHandle, out NativeArray<AcousticPortalNode>.ReadOnly refreshedNodes) ||
                !vault.TryReadOnlyHandle(in edgeHandle, out NativeArray<AcousticPortalEdge>.ReadOnly refreshedEdges) ||
                refreshedNodes.Length < AcousticPortalConstants.MaxPathNodes ||
                refreshedEdges.Length < AcousticPortalConstants.MaxPathEdges)
            {
                failureFlags |= FailureDefrag;
            }

            return failureFlags == 0u;
        }

        private static void ValidateLayoutsOrThrow()
        {
            uint failureFlags = 0u;
            AssertExplicit<AcousticAup>(40, ref failureFlags);
            AssertOffset<AcousticAup>(nameof(AcousticAup.GridX), 0, ref failureFlags);
            AssertOffset<AcousticAup>(nameof(AcousticAup.GridY), 8, ref failureFlags);
            AssertOffset<AcousticAup>(nameof(AcousticAup.GridZ), 16, ref failureFlags);
            AssertOffset<AcousticAup>(nameof(AcousticAup.Local), 24, ref failureFlags);
            AssertOffset<AcousticAup>("_pad0", 36, ref failureFlags);

            AssertExplicit<AcousticPortalNode>(56, ref failureFlags);
            AssertOffset<AcousticPortalNode>(nameof(AcousticPortalNode.Position), 0, ref failureFlags);
            AssertOffset<AcousticPortalNode>(nameof(AcousticPortalNode.FirstEdge), 40, ref failureFlags);
            AssertOffset<AcousticPortalNode>(nameof(AcousticPortalNode.EdgeCount), 44, ref failureFlags);
            AssertOffset<AcousticPortalNode>(nameof(AcousticPortalNode.RoomVolumeCubicMeters), 48, ref failureFlags);
            AssertOffset<AcousticPortalNode>(nameof(AcousticPortalNode.Flags), 52, ref failureFlags);
            AssertOffset<AcousticPortalNode>("_pad0", 53, ref failureFlags);
            AssertOffset<AcousticPortalNode>("_pad1", 54, ref failureFlags);
            AssertOffset<AcousticPortalNode>("_pad2", 55, ref failureFlags);

            AssertExplicit<AcousticPortalEdge>(16, ref failureFlags);
            AssertOffset<AcousticPortalEdge>(nameof(AcousticPortalEdge.ToNode), 0, ref failureFlags);
            AssertOffset<AcousticPortalEdge>(nameof(AcousticPortalEdge.DistanceMeters), 4, ref failureFlags);
            AssertOffset<AcousticPortalEdge>(nameof(AcousticPortalEdge.Flags), 8, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad0", 9, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad1", 10, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad2", 11, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad3", 12, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad4", 13, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad5", 14, ref failureFlags);
            AssertOffset<AcousticPortalEdge>("_pad6", 15, ref failureFlags);

            AssertExplicit<AcousticPathQuery>(112, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.SourceAup), 0, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.ListenerAup), 40, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.ListenerRight), 80, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.NodeCount), 92, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.EdgeCount), 96, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.MaxNodeExpansions), 100, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.GlobalQualityWeight), 104, ref failureFlags);
            AssertOffset<AcousticPathQuery>(nameof(AcousticPathQuery.DisablePortalPath), 108, ref failureFlags);
            AssertOffset<AcousticPathQuery>("_pad0", 109, ref failureFlags);
            AssertOffset<AcousticPathQuery>("_pad1", 110, ref failureFlags);
            AssertOffset<AcousticPathQuery>("_pad2", 111, ref failureFlags);

            AssertExplicit<SoundEmissionSignal>(64, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.SourceAup), 0, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.Volume), 40, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.Pitch), 44, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.EventID), 48, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.StationaryCacheKey), 52, ref failureFlags);
            AssertOffset<SoundEmissionSignal>(nameof(SoundEmissionSignal.Flags), 56, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad0", 57, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad1", 58, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad2", 59, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad3", 60, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad4", 61, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad5", 62, ref failureFlags);
            AssertOffset<SoundEmissionSignal>("_pad6", 63, ref failureFlags);

            AssertExplicit<AcousticPathResult>(104, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.LastPortalAup), 0, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.TrueDistanceMeters), 40, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.DelaySeconds), 44, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.Transmission01), 48, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.LowPassCutoffHz), 52, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.ItdSeconds), 56, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.RoomVolumeCubicMeters), 60, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.PathfindingMs), 64, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.NodeCount), 68, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.CornerCount), 72, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.ExpandedNodeCount), 76, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.SourceNodeIndex), 80, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.ListenerNodeIndex), 84, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.StateHash), 88, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.Status), 92, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.UsedPortalPath), 93, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.UsedSealedBulkhead), 94, ref failureFlags);
            AssertOffset<AcousticPathResult>(nameof(AcousticPathResult.UsedReprojectionCache), 95, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad0", 96, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad1", 97, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad2", 98, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad3", 99, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad4", 100, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad5", 101, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad6", 102, ref failureFlags);
            AssertOffset<AcousticPathResult>("_pad7", 103, ref failureFlags);

            AssertExplicit<AcousticTelemetryEntry>(64, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.StopwatchTicks), 0, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.Frame), 8, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.NodeCount), 12, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.CornerCount), 16, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.ExpandedNodeCount), 20, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.PathfindingMs), 24, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.TrueDistanceMeters), 28, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.DelaySeconds), 32, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.LowPassCutoffHz), 36, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.Flags), 40, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.StateHash), 44, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.BufferId), 48, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.Generation), 52, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>(nameof(AcousticTelemetryEntry.FailureCode), 56, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>("_pad0", 60, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>("_pad1", 61, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>("_pad2", 62, ref failureFlags);
            AssertOffset<AcousticTelemetryEntry>("_pad3", 63, ref failureFlags);

            if (failureFlags != 0u)
                throw new FatalArchitectureException("1307 acoustic portal DTO layout violation.");
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureFlags)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int offset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }
    }
}
#endif

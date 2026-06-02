#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Editor.Structures;

namespace Hecton8.Tests.Editor.Structures
{
    public sealed class DeepReachStationArchitectEditTests
    {
        [Test]
        public void DtoStrides_AreExplicitAndArm64Aligned()
        {
            Assert.AreEqual(DeepReachStationConstants.SocketStrideBytes, UnsafeUtility.SizeOf<StationSocketDTO>());
            Assert.AreEqual(DeepReachStationConstants.ModuleRuleStrideBytes, UnsafeUtility.SizeOf<StationModuleRuleDTO>());
            Assert.AreEqual(DeepReachStationConstants.WfcCellStrideBytes, UnsafeUtility.SizeOf<StationWfcCellDTO>());
            Assert.AreEqual(DeepReachStationConstants.PlacementStrideBytes, UnsafeUtility.SizeOf<StationPlacementDTO>());
            Assert.AreEqual(DeepReachStationConstants.MeshSliceStrideBytes, UnsafeUtility.SizeOf<StationMeshSliceDTO>());
            Assert.AreEqual(DeepReachStationConstants.TriangleStrideBytes, UnsafeUtility.SizeOf<StationTriangleDTO>());
            Assert.AreEqual(DeepReachStationConstants.MeshVertexStrideBytes, UnsafeUtility.SizeOf<StationMeshVertexDTO>());
            Assert.AreEqual(DeepReachStationConstants.RenderVertexStrideBytes, UnsafeUtility.SizeOf<StationRenderVertexDTO>());
            Assert.AreEqual(DeepReachStationConstants.WeldBucketStrideBytes, UnsafeUtility.SizeOf<StationWeldBucketDTO>());
            Assert.AreEqual(DeepReachStationConstants.BakeCounterStrideBytes, UnsafeUtility.SizeOf<StationBakeCountersDTO>());
        }

        [Test]
        public void SocketCompatibility_HonorsGenericUniversalLane()
        {
            const ushort generic = (ushort)DeepReachStationConstants.GenericConnectorMask;
            const ushort typedA = 1 << 2;
            const ushort typedB = 1 << 3;
            Assert.True(DeepReachStationMath.SocketsCompatible(generic, typedA));
            Assert.True(DeepReachStationMath.SocketsCompatible(typedA, generic));
            Assert.True(DeepReachStationMath.SocketsCompatible(typedA, typedA));
            Assert.False(DeepReachStationMath.SocketsCompatible(typedA, typedB));
            Assert.False(DeepReachStationMath.SocketsCompatible(0, generic));
            Assert.False(DeepReachStationMath.SocketsCompatible(generic, 0));
        }

        [Test]
        public void SocketRotation_MapsAuthoredSocketsIntoStationSpace()
        {
            var rule = new StationModuleRuleDTO
            {
                SocketNorth = 1 << 2,
                SocketSouth = 1 << 3,
                SocketTop = 1 << 4,
                SocketBottom = 1 << 5
            };

            Assert.AreEqual(rule.SocketNorth, DeepReachStationMath.SocketAtRotated(rule, DeepReachStationDirections.East, 1));
            Assert.AreEqual(rule.SocketSouth, DeepReachStationMath.SocketAtRotated(rule, DeepReachStationDirections.West, 1));
            Assert.AreEqual(rule.SocketTop, DeepReachStationMath.SocketAtRotated(rule, DeepReachStationDirections.Top, 3));
            Assert.AreEqual(DeepReachStationDirections.North, DeepReachStationMath.UnrotateHorizontalDirection(DeepReachStationDirections.East, 1));
        }

        [Test]
        public void WfcSolver_IsDeterministicForSameSeed()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> gridA = default;
            NativeArray<StationWfcCellDTO> gridB = default;
            NativeArray<StationPlacementDTO> placementsA = default;
            NativeArray<StationPlacementDTO> placementsB = default;
            NativeArray<StationBakeCountersDTO> countersA = default;
            NativeArray<StationBakeCountersDTO> countersB = default;
            try
            {
                rules = CreateRules(Allocator.TempJob);
                gridA = new NativeArray<StationWfcCellDTO>(7 * 2 * 9, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                gridB = new NativeArray<StationWfcCellDTO>(7 * 2 * 9, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placementsA = new NativeArray<StationPlacementDTO>(48, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placementsB = new NativeArray<StationPlacementDTO>(48, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                countersA = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                countersB = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                RunWfc(rules, gridA, placementsA, countersA);
                RunWfc(rules, gridB, placementsB, countersB);

                Assert.AreEqual(countersA[0].PlacementCount, countersB[0].PlacementCount);
                Assert.AreEqual(countersA[0].StateHash, countersB[0].StateHash);
                for (int i = 0; i < countersA[0].PlacementCount; i++)
                    Assert.AreEqual(placementsA[i].StableHash, placementsB[i].StableHash);
            }
            finally
            {
                DisposeIfCreated(countersB);
                DisposeIfCreated(countersA);
                DisposeIfCreated(placementsB);
                DisposeIfCreated(placementsA);
                DisposeIfCreated(gridB);
                DisposeIfCreated(gridA);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_HandlesOneHundredModuleBudgetWithoutCapacityFault()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(13 * 3 * 13, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(100, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(13, 3, 13),
                    Seed = 1001607u,
                    MaxPlacements = 100,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.Greater(counters[0].PlacementCount, 0u);
                Assert.LessOrEqual(counters[0].PlacementCount, 100u);
                Assert.AreEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultCapacity);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_DoesNotStackModulesAcrossClosedFaces()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(5 * 2 * 5, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(50, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(5, 2, 5),
                    Seed = 1607001u,
                    MaxPlacements = 50,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.Greater(counters[0].PlacementCount, 0u);
                for (int i = 0; i < counters[0].PlacementCount; i++)
                {
                    int3 a = placements[i].GridCoord;
                    for (int j = i + 1; j < counters[0].PlacementCount; j++)
                    {
                        int3 b = placements[j].GridCoord;
                        bool sameColumn = a.x == b.x && a.z == b.z;
                        Assert.False(sameColumn && math.abs(a.y - b.y) == 1, $"Closed vertical faces stacked at {a} and {b}.");
                    }
                }
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_RejectsExternalSocketLeaks()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateLineOnlyRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(1, 1, 1),
                    Seed = 1607117u,
                    MaxPlacements = 1,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.AreEqual(0u, counters[0].PlacementCount);
                Assert.AreEqual((byte)DeepReachStationConstants.EmptyModuleId, grid[0].CollapsedModuleId);
                Assert.AreEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultContradiction);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_AllowsSealedModulesAtExteriorBoundary()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateSealedOnlyRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(1, 1, 1),
                    Seed = 1607118u,
                    MaxPlacements = 1,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.AreEqual(1u, counters[0].PlacementCount);
                Assert.AreEqual(1, placements[0].ModuleId);
                Assert.AreEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultContradiction);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_RotatesLineModulesToConnectEastWest()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateLineOnlyRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(5, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(5, 1, 1),
                    Seed = 1607222u,
                    MaxPlacements = 2,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.AreEqual(2u, counters[0].PlacementCount);
                Assert.AreEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultContradiction);
                for (int i = 0; i < counters[0].PlacementCount; i++)
                {
                    byte rotation = placements[i].RotationQuarterTurns;
                    Assert.True(rotation == 1 || rotation == 3, $"Expected yawed corridor placement, got rotation {rotation}.");
                    Assert.AreNotEqual(0, placements[i].ConnectedDirectionMask & ((1 << DeepReachStationDirections.North) | (1 << DeepReachStationDirections.South)));
                    Assert.AreEqual(0, placements[i].ConnectedDirectionMask & ((1 << DeepReachStationDirections.East) | (1 << DeepReachStationDirections.West)));
                }
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_FailsClosedWhenStructuralGraphIsDisconnected()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateSealedOnlyRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(9, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(9, 1, 1),
                    Seed = 1607551u,
                    MaxPlacements = 2,
                    CellSize = 6f,
                    GlobalQualityWeight = 1f
                }.Run();

                Assert.AreEqual(0u, counters[0].PlacementCount);
                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultInvalidTopology);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void WfcSolver_FailsClosedWhenQualityIsNonFinite()
        {
            NativeArray<StationModuleRuleDTO> rules = default;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                rules = CreateSealedOnlyRules(Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = new int3(1, 1, 1),
                    Seed = 1607667u,
                    MaxPlacements = 1,
                    CellSize = 6f,
                    GlobalQualityWeight = float.NaN
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].PlacementCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(placements);
                DisposeIfCreated(grid);
                DisposeIfCreated(rules);
            }
        }

        [Test]
        public void Fusion_CullsInternalFacesAndWeldsSharedSeam()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(12, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(72, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(72, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(16, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(64, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                FillCube(sourceVertices, sourceTriangles);
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 8, TriangleStart = 0, TriangleCount = 12 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.TRS(new float3(0f, 0f, 0f), quaternion.identity, new float3(1f)),
                    ModuleId = 1,
                    ConnectedDirectionMask = (ushort)(1 << DeepReachStationDirections.North)
                };
                placements[1] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.TRS(new float3(0f, 0f, 2f), quaternion.identity, new float3(1f)),
                    ModuleId = 1,
                    ConnectedDirectionMask = (ushort)(1 << DeepReachStationDirections.South)
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 2 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreEqual(4u, counters[0].CulledTriangleCount);
                Assert.AreEqual(60u, counters[0].SourceIndexCount);

                new StationVertexWeldingJob
                {
                    SourceVertices = transformed,
                    SourceIndices = rawIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreEqual(60u, counters[0].WeldedIndexCount);
                Assert.Greater(counters[0].MergedVertexCount, 0u);
                Assert.Less(counters[0].WeldedVertexCount, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultCapacity);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_PreservesVisibleTriangleMaterialSlots()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<ushort> rawTriangleMaterials = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawTriangleMaterials = new NativeArray<ushort>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(1f, 0f), Flags = 1u };
                sourceVertices[2] = new StationMeshVertexDTO { Position = new float3(0f, 1f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f, 1f), Flags = 1u };
                sourceTriangles[0] = new StationTriangleDTO { Index0 = 0, Index1 = 1, Index2 = 2, SubMesh = 7, Flags = 1u };
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 3, TriangleStart = 0, TriangleCount = 1 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    RawTriangleMaterials = rawTriangleMaterials,
                    Counters = counters
                }.Run();

                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(3u, counters[0].SourceIndexCount);
                Assert.AreEqual(7, rawTriangleMaterials[0]);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawTriangleMaterials);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void BoxSurrogate_PreservesResolvedStructuralMaterialSlot()
        {
            var vertices = new List<StationMeshVertexDTO>(8);
            var triangles = new List<StationTriangleDTO>(12);
            ushort[] socketMasks = new ushort[DeepReachStationConstants.DirectionCount];
            socketMasks[DeepReachStationDirections.North] = (ushort)DeepReachStationConstants.GenericConnectorMask;

            DeepReachStationModuleLibraryBuilder.AppendBoxSurrogate(
                new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f)),
                socketMasks,
                0,
                0,
                vertices,
                triangles,
                5);

            Assert.AreEqual(8, vertices.Count);
            Assert.AreEqual(12, triangles.Count);
            for (int i = 0; i < triangles.Count; i++)
                Assert.AreEqual(5, triangles[i].SubMesh);
            Assert.AreNotEqual(0, triangles[0].CullDirectionMask & (1 << DeepReachStationDirections.North));
        }

        [Test]
        public void Welding_PreservesTriangleMaterialSlots()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<ushort> sourceTriangleMaterials = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<ushort> weldedTriangleMaterials = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangleMaterials = new NativeArray<ushort>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedTriangleMaterials = new NativeArray<ushort>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(1f, 0f), Flags = 1u };
                sourceVertices[2] = new StationMeshVertexDTO { Position = new float3(0f, 1f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f, 1f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                sourceIndices[2] = 2;
                sourceTriangleMaterials[0] = 5;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 3, SourceIndexCount = 3 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    SourceTriangleMaterials = sourceTriangleMaterials,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    WeldedTriangleMaterials = weldedTriangleMaterials,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(3u, counters[0].WeldedIndexCount);
                Assert.AreEqual(5, weldedTriangleMaterials[0]);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedTriangleMaterials);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceTriangleMaterials);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenIndexCapacityIsTooSmall()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(12, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                FillCube(sourceVertices, sourceTriangles);
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 8, TriangleStart = 0, TriangleCount = 12 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.TRS(new float3(0f), quaternion.identity, new float3(1f)),
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultCapacity);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenSliceExceedsSourceBuffers()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(6, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 8, TriangleStart = 0, TriangleCount = 12 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultInvalidTopology);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenTriangleIndexEscapesSlice()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < sourceVertices.Length; i++)
                    sourceVertices[i] = new StationMeshVertexDTO { Position = new float3(i, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };

                sourceTriangles[0] = new StationTriangleDTO { Index0 = 0, Index1 = 1, Index2 = 4 };
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 4, TriangleStart = 0, TriangleCount = 1 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultInvalidTopology);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenTriangleAreaIsDegenerate()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceVertices[2] = new StationMeshVertexDTO { Position = new float3(2f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceTriangles[0] = new StationTriangleDTO { Index0 = 0, Index1 = 1, Index2 = 2 };
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 3, TriangleStart = 0, TriangleCount = 1 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultInvalidTopology);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenPlacementMatrixIsNonFinite()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceVertices[2] = new StationMeshVertexDTO { Position = new float3(0f, 1f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceTriangles[0] = new StationTriangleDTO { Index0 = 0, Index1 = 1, Index2 = 2 };
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 3, TriangleStart = 0, TriangleCount = 1 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                StationPlacementDTO invalidPlacement = placements[0];
                invalidPlacement.LocalToStation.c3.x = float.NaN;
                placements[0] = invalidPlacement;
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Fusion_FailsClosedWhenSourceVertexIsNonFinite()
        {
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationMeshSliceDTO> slices = default;
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<StationTriangleDTO> sourceTriangles = default;
            NativeArray<StationMeshVertexDTO> transformed = default;
            NativeArray<int> rawIndices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                placements = new NativeArray<StationPlacementDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                slices = new NativeArray<StationMeshSliceDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceVertices = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceTriangles = new NativeArray<StationTriangleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                transformed = new NativeArray<StationMeshVertexDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(float.PositiveInfinity, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[2] = new StationMeshVertexDTO { Position = new float3(0f, 1f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceTriangles[0] = new StationTriangleDTO { Index0 = 0, Index1 = 1, Index2 = 2 };
                slices[1] = new StationMeshSliceDTO { VertexStart = 0, VertexCount = 3, TriangleStart = 0, TriangleCount = 1 };
                placements[0] = new StationPlacementDTO
                {
                    LocalToStation = float4x4.identity,
                    ModuleId = 1
                };
                counters[0] = new StationBakeCountersDTO { PlacementCount = 1 };

                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = slices,
                    SourceVertices = sourceVertices,
                    SourceTriangles = sourceTriangles,
                    TransformedVertices = transformed,
                    RawIndices = rawIndices,
                    Counters = counters
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].SourceVertexCount);
                Assert.AreEqual(0u, counters[0].SourceIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(rawIndices);
                DisposeIfCreated(transformed);
                DisposeIfCreated(sourceTriangles);
                DisposeIfCreated(sourceVertices);
                DisposeIfCreated(slices);
                DisposeIfCreated(placements);
            }
        }

        [Test]
        public void Welding_FailsClosedWhenRemapCapacityIsTooSmall()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                sourceIndices[2] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 2, SourceIndexCount = 3 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultCapacity);
                Assert.AreEqual(0u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].WeldedIndexCount);
                Assert.AreEqual(0u, counters[0].MergedVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_FailsClosedWhenBucketCountIsNotPowerOfTwo()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 1, SourceIndexCount = 1 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultCapacity);
                Assert.AreEqual(0u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].WeldedIndexCount);
                Assert.AreEqual(0u, counters[0].MergedVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_FailsClosedWhenEpsilonIsNonFinite()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 1, SourceIndexCount = 1 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = float.NaN
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].WeldedIndexCount);
                Assert.AreEqual(0u, counters[0].MergedVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_FailsClosedWhenSourceIndexCountIsNotTriangleAligned()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 2, SourceIndexCount = 2 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultInvalidTopology);
                Assert.AreEqual(0u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].WeldedIndexCount);
                Assert.AreEqual(0u, counters[0].MergedVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_MergesVerticesAcrossNeighborQuantizedCells()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(32, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0.00149f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(0.00247f, 0f, 0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                sourceIndices[2] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 2, SourceIndexCount = 3 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(1u, counters[0].WeldedVertexCount);
                Assert.AreEqual(3u, counters[0].WeldedIndexCount);
                Assert.AreEqual(1u, counters[0].MergedVertexCount);
                Assert.AreEqual(weldedIndices[0], weldedIndices[1]);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_PreservesHardEdgesWithDivergentNormals()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(32, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(1f, 0f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                sourceIndices[2] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 2, SourceIndexCount = 3 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(2u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].MergedVertexCount);
                Assert.AreNotEqual(weldedIndices[0], weldedIndices[1]);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void Welding_FailsClosedWhenSourceVertexIsNonFinite()
        {
            NativeArray<StationMeshVertexDTO> sourceVertices = default;
            NativeArray<int> sourceIndices = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                sourceVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sourceIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                sourceVertices[0] = new StationMeshVertexDTO { Position = new float3(0f), Normal = new float3(0f, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceVertices[1] = new StationMeshVertexDTO { Position = new float3(1f, 0f, 0f), Normal = new float3(float.NaN, 1f, 0f), Uv0 = new float2(0f), Flags = 1u };
                sourceIndices[0] = 0;
                sourceIndices[1] = 1;
                sourceIndices[2] = 0;
                counters[0] = new StationBakeCountersDTO { SourceVertexCount = 2, SourceIndexCount = 3 };

                new StationVertexWeldingJob
                {
                    SourceVertices = sourceVertices,
                    SourceIndices = sourceIndices,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = 0.001f
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].WeldedVertexCount);
                Assert.AreEqual(0u, counters[0].WeldedIndexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(buckets);
                DisposeIfCreated(remap);
                DisposeIfCreated(weldedIndices);
                DisposeIfCreated(weldedVertices);
                DisposeIfCreated(sourceIndices);
                DisposeIfCreated(sourceVertices);
            }
        }

        [Test]
        public void DamageBake_FailsClosedWhenQualityIsNonFinite()
        {
            NativeArray<StationMeshVertexDTO> vertices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                vertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices[0] = new StationMeshVertexDTO
                {
                    Position = new float3(0f),
                    Normal = new float3(0f, 1f, 0f),
                    Uv0 = new float2(0f),
                    Flags = 1u
                };
                counters[0] = new StationBakeCountersDTO { WeldedVertexCount = 1 };

                new StationProceduralDamageJob
                {
                    Vertices = vertices,
                    Counters = counters,
                    Seed = 1607331u,
                    GlobalQualityWeight = float.NaN,
                    StationHalfExtents = new float3(4f)
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].DamageVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(vertices);
            }
        }

        [Test]
        public void DamageBake_FailsClosedWhenExtentsAreNonFinite()
        {
            NativeArray<StationMeshVertexDTO> vertices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                vertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices[0] = new StationMeshVertexDTO
                {
                    Position = new float3(0f),
                    Normal = new float3(0f, 1f, 0f),
                    Uv0 = new float2(0f),
                    Flags = 1u
                };
                counters[0] = new StationBakeCountersDTO { WeldedVertexCount = 1 };

                new StationProceduralDamageJob
                {
                    Vertices = vertices,
                    Counters = counters,
                    Seed = 1607332u,
                    GlobalQualityWeight = 1f,
                    StationHalfExtents = new float3(float.NaN, 4f, 4f)
                }.Run();

                Assert.AreNotEqual(0u, counters[0].FaultFlags & DeepReachStationConstants.FaultNonFinite);
                Assert.AreEqual(0u, counters[0].DamageVertexCount);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(vertices);
            }
        }

        [Test]
        public void DamageBake_NormalizesOversizedInputNormalsBeforeDeforming()
        {
            NativeArray<StationMeshVertexDTO> vertices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                vertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices[0] = new StationMeshVertexDTO
                {
                    Position = new float3(0f),
                    Normal = new float3(0f, 10f, 0f),
                    Uv0 = new float2(0f),
                    Flags = 1u
                };
                counters[0] = new StationBakeCountersDTO { WeldedVertexCount = 1 };

                new StationProceduralDamageJob
                {
                    Vertices = vertices,
                    Counters = counters,
                    Seed = 1607333u,
                    GlobalQualityWeight = 1f,
                    StationHalfExtents = new float3(1f)
                }.Run();

                StationMeshVertexDTO vertex = vertices[0];
                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(1f, math.length(vertex.Normal), 0.0001f);
                Assert.AreEqual(1f, vertex.Normal.y, 0.0001f);
                Assert.GreaterOrEqual(vertex.Position.y, -0.421f);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(vertices);
            }
        }

        [Test]
        public void DamageBake_WritesRustAlgaeAndWearBlockerChannels()
        {
            NativeArray<StationMeshVertexDTO> vertices = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            try
            {
                vertices = new NativeArray<StationMeshVertexDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices[0] = new StationMeshVertexDTO
                {
                    Position = new float3(50f, 50f, 50f),
                    Normal = new float3(0f, 1f, 0f),
                    Uv0 = new float2(0f),
                    Flags = 1u
                };
                counters[0] = new StationBakeCountersDTO { WeldedVertexCount = 1 };

                new StationProceduralDamageJob
                {
                    Vertices = vertices,
                    Counters = counters,
                    Seed = 1607331u,
                    GlobalQualityWeight = 0.5f,
                    StationHalfExtents = new float3(1f)
                }.Run();

                uint color = vertices[0].ColorRgba;
                byte rust = (byte)(color & 0xFFu);
                byte algae = (byte)((color >> 8) & 0xFFu);
                byte wearBlocker = (byte)((color >> 16) & 0xFFu);
                byte alpha = (byte)((color >> 24) & 0xFFu);
                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(0u, counters[0].DamageVertexCount);
                Assert.Greater(rust, 0);
                Assert.Less(rust, 255);
                Assert.Greater(algae, 0);
                Assert.Less(algae, rust);
                Assert.Greater(wearBlocker, 0);
                Assert.Less(wearBlocker, 128);
                Assert.AreEqual(255, alpha);
            }
            finally
            {
                DisposeIfCreated(counters);
                DisposeIfCreated(vertices);
            }
        }

        [Test]
        public void GeneratorSource_DoesNotUseRuntimeRandomOrFrameLoops()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Editor", "Generators", "Structures");
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.GreaterOrEqual(files.Length, 4);
            string updateToken = "void " + "Update(";
            string fixedUpdateToken = "void " + "FixedUpdate(";
            string lateUpdateToken = "void " + "LateUpdate(";

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                Assert.False(text.Contains("UnityEngine.Random"), files[i]);
                Assert.False(text.Contains("System.Random"), files[i]);
                Assert.False(text.Contains(updateToken), files[i]);
                Assert.False(text.Contains(fixedUpdateToken), files[i]);
                Assert.False(text.Contains(lateUpdateToken), files[i]);
            }
        }

        private static void RunWfc(
            NativeArray<StationModuleRuleDTO> rules,
            NativeArray<StationWfcCellDTO> grid,
            NativeArray<StationPlacementDTO> placements,
            NativeArray<StationBakeCountersDTO> counters)
        {
            new StationWfcSolverJob
            {
                Grid = grid,
                Rules = rules,
                Placements = placements,
                Counters = counters,
                GridDims = new int3(7, 2, 9),
                Seed = 1607u,
                MaxPlacements = 48,
                CellSize = 6f,
                GlobalQualityWeight = 0.7f
            }.Run();
        }

        private static void DisposeIfCreated<T>(NativeArray<T> array)
            where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
        }

        private static NativeArray<StationModuleRuleDTO> CreateRules(Allocator allocator)
        {
            var rules = new NativeArray<StationModuleRuleDTO>(3, allocator, NativeArrayOptions.ClearMemory);
            rules[0] = new StationModuleRuleDTO
            {
                ModuleHash = 1u,
                Weight = 1f,
                ModuleId = 0
            };
            rules[1] = new StationModuleRuleDTO
            {
                ModuleHash = 2u,
                SocketNorth = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketEast = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketSouth = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketWest = (ushort)DeepReachStationConstants.GenericConnectorMask,
                BoundsExtents = new float3(1f),
                Weight = 2f,
                ModuleId = 1,
                SourceVertexCount = 8,
                SourceTriangleCount = 12
            };
            rules[2] = new StationModuleRuleDTO
            {
                ModuleHash = 3u,
                SocketNorth = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketEast = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketSouth = (ushort)DeepReachStationConstants.GenericConnectorMask,
                SocketWest = (ushort)DeepReachStationConstants.GenericConnectorMask,
                BoundsExtents = new float3(1f, 0.5f, 1f),
                Weight = 1f,
                ModuleId = 2,
                SourceVertexCount = 8,
                SourceTriangleCount = 12
            };
            return rules;
        }

        private static NativeArray<StationModuleRuleDTO> CreateSealedOnlyRules(Allocator allocator)
        {
            var rules = new NativeArray<StationModuleRuleDTO>(2, allocator, NativeArrayOptions.ClearMemory);
            rules[0] = new StationModuleRuleDTO
            {
                ModuleHash = 1u,
                Weight = 1f,
                ModuleId = 0
            };
            rules[1] = new StationModuleRuleDTO
            {
                ModuleHash = 2u,
                BoundsExtents = new float3(1f),
                Weight = 1f,
                ModuleId = 1,
                SourceVertexCount = 8,
                SourceTriangleCount = 12
            };
            return rules;
        }

        private static NativeArray<StationModuleRuleDTO> CreateLineOnlyRules(Allocator allocator)
        {
            var rules = new NativeArray<StationModuleRuleDTO>(2, allocator, NativeArrayOptions.ClearMemory);
            rules[0] = new StationModuleRuleDTO
            {
                ModuleHash = 1u,
                Weight = 1f,
                ModuleId = 0
            };
            rules[1] = new StationModuleRuleDTO
            {
                ModuleHash = 2u,
                SocketNorth = (ushort)DeepReachStationConstants.GenericConnectorMask,
                BoundsExtents = new float3(1f),
                Weight = 1f,
                ModuleId = 1,
                SourceVertexCount = 8,
                SourceTriangleCount = 12
            };
            return rules;
        }

        private static void FillCube(NativeArray<StationMeshVertexDTO> vertices, NativeArray<StationTriangleDTO> triangles)
        {
            float3[] positions =
            {
                new float3(-1f, -1f, -1f),
                new float3(1f, -1f, -1f),
                new float3(1f, -1f, 1f),
                new float3(-1f, -1f, 1f),
                new float3(-1f, 1f, -1f),
                new float3(1f, 1f, -1f),
                new float3(1f, 1f, 1f),
                new float3(-1f, 1f, 1f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                vertices[i] = new StationMeshVertexDTO
                {
                    Position = positions[i],
                    Normal = math.normalizesafe(positions[i], new float3(0f, 1f, 0f)),
                    Uv0 = new float2(0f),
                    ColorRgba = DeepReachStationMath.EncodeColor(255, 128, 64, 255),
                    Flags = 1u
                };
            }

            int index = 0;
            AppendQuad(triangles, ref index, 3, 2, 6, 7, (ushort)(1 << DeepReachStationDirections.North));
            AppendQuad(triangles, ref index, 1, 0, 4, 5, (ushort)(1 << DeepReachStationDirections.South));
            AppendQuad(triangles, ref index, 2, 1, 5, 6, 0);
            AppendQuad(triangles, ref index, 0, 3, 7, 4, 0);
            AppendQuad(triangles, ref index, 7, 6, 5, 4, 0);
            AppendQuad(triangles, ref index, 0, 1, 2, 3, 0);
        }

        private static void AppendQuad(NativeArray<StationTriangleDTO> triangles, ref int index, int i0, int i1, int i2, int i3, ushort cullMask)
        {
            triangles[index++] = new StationTriangleDTO { Index0 = i0, Index1 = i1, Index2 = i2, CullDirectionMask = cullMask, Flags = 1u };
            triangles[index++] = new StationTriangleDTO { Index0 = i0, Index1 = i2, Index2 = i3, CullDirectionMask = cullMask, Flags = 1u };
        }
    }
}
#endif

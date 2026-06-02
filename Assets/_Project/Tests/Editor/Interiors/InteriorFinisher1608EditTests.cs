using System;
using System.IO;
using Hecton8.Editor.Interiors;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor.Interiors
{
    public sealed class InteriorFinisher1608EditTests
    {
        [Test]
        public void DtoLayouts_AreArm64Aligned()
        {
            Assert.AreEqual(InteriorFinisherConstants1608.SocketStrideBytes, UnsafeUtility.SizeOf<InteriorSocketDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.InstrumentRuleStrideBytes, UnsafeUtility.SizeOf<InstrumentRuleDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.PlacementStrideBytes, UnsafeUtility.SizeOf<InstrumentPlacementDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.MeshVertexStrideBytes, UnsafeUtility.SizeOf<InteriorMeshVertexDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.RenderVertexStrideBytes, UnsafeUtility.SizeOf<InteriorRenderVertexDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.TriangleStrideBytes, UnsafeUtility.SizeOf<InteriorTriangleDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.AtlasRectStrideBytes, UnsafeUtility.SizeOf<InteriorAtlasRectDTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.Rgba32StrideBytes, UnsafeUtility.SizeOf<InteriorRgba32DTO1608>());
            Assert.AreEqual(InteriorFinisherConstants1608.CountersStrideBytes, UnsafeUtility.SizeOf<InteriorBakeCountersDTO1608>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InteriorSocketDTO1608>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InstrumentRuleDTO1608>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InstrumentPlacementDTO1608>() & 7);
        }

        [Test]
        public void PopulateAndFuse_OneThousandSockets_EliminatesStaticBaseGameObjects()
        {
            const int SocketCount = 1000;
            NativeArray<InteriorSocketDTO1608> sockets = default;
            NativeArray<InstrumentRuleDTO1608> rules = default;
            NativeArray<InstrumentPlacementDTO1608> placements = default;
            NativeArray<InteriorMeshVertexDTO1608> vertices = default;
            NativeArray<InteriorTriangleDTO1608> triangles = default;
            NativeList<InteriorMeshVertexDTO1608> fusedVertices = default;
            NativeList<int> fusedIndices = default;
            NativeArray<InteriorBakeCountersDTO1608> counters = default;

            try
            {
                sockets = new NativeArray<InteriorSocketDTO1608>(SocketCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < SocketCount; i++)
                {
                    sockets[i] = new InteriorSocketDTO1608
                    {
                        LocalPosition = new float3(i % 40, (i / 40) % 10, i / 400),
                        Radius = 0.25f,
                        LocalRotation = quaternion.identity,
                        LocalNormal = new float3(0f, 0f, 1f),
                        SurfaceArea = 0.0625f,
                        StableHash = InteriorFinisherMath1608.Hash((uint)(i + 1)),
                        TagHash = 0u,
                        AllowedInstrumentMask = 0u,
                        SocketKind = InteriorSocketKind1608.WallPanel,
                        DensityHint = 255,
                        Flags = 1u,
                        PairIndex = -1
                    };
                }

                rules = new NativeArray<InstrumentRuleDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices = new NativeArray<InteriorMeshVertexDTO1608>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                triangles = new NativeArray<InteriorTriangleDTO1608>(12, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                FillMockCube(vertices, triangles);
                rules[0] = new InstrumentRuleDTO1608
                {
                    InstrumentHash = 0x1608u,
                    TypeHash = 0xFFFFFFFFu,
                    TextureHash = 0xA7A5u,
                    Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag | InteriorFinisherConstants1608.InstrumentMovableFlag,
                    BoundsExtents = new float3(0.1f, 0.08f, 0.04f),
                    MinSocketRadius = 0.02f,
                    Weight = 1f,
                    StaticVertexStart = 0u,
                    StaticVertexCount = 8u,
                    StaticIndexStart = 0u,
                    StaticIndexCount = 36u,
                    MovingVertexStart = 0u,
                    MovingVertexCount = 0u,
                    UvMin = new float2(0.25f, 0.5f),
                    UvMax = new float2(0.5f, 0.75f)
                };

                placements = new NativeArray<InstrumentPlacementDTO1608>(SocketCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<InteriorBakeCountersDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                new PopulateSocketsJob1608
                {
                    Sockets = sockets,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    Seed = 1608u,
                    GlobalQualityWeight = 1f,
                    DensityWeight = 1f
                }.Run();

                fusedVertices = new NativeList<InteriorMeshVertexDTO1608>(SocketCount * 8, Allocator.TempJob);
                fusedIndices = new NativeList<int>(SocketCount * 36, Allocator.TempJob);
                new WeldInstrumentBasesJob1608
                {
                    Placements = placements,
                    Rules = rules,
                    SourceVertices = vertices,
                    SourceTriangles = triangles,
                    FusedVertices = fusedVertices,
                    FusedIndices = fusedIndices,
                    Counters = counters
                }.Run();

                InteriorBakeCountersDTO1608 result = counters[0];
                Assert.AreEqual(0u, result.FaultFlags);
                Assert.AreEqual((uint)SocketCount, result.PlacementCount);
                Assert.AreEqual((uint)SocketCount, result.StaticBaseFusionCount);
                Assert.AreEqual((uint)SocketCount, result.MovingPartCount);
                Assert.AreEqual((uint)SocketCount, result.GameObjectsEliminated);
                Assert.AreEqual(SocketCount * 8, fusedVertices.Length);
                Assert.AreEqual(SocketCount * 36, fusedIndices.Length);
                Assert.AreEqual(0, placements[0].MovingVertexStart);
                Assert.AreEqual(0, placements[0].MovingVertexCount);
                for (int i = 0; i < fusedVertices.Length; i++)
                {
                    float2 uv = fusedVertices[i].Uv0;
                    Assert.GreaterOrEqual(uv.x, 0.25f);
                    Assert.LessOrEqual(uv.x, 0.5f);
                    Assert.GreaterOrEqual(uv.y, 0.5f);
                    Assert.LessOrEqual(uv.y, 0.75f);
                }
            }
            finally
            {
                if (counters.IsCreated)
                    counters.Dispose();
                if (fusedIndices.IsCreated)
                    fusedIndices.Dispose();
                if (fusedVertices.IsCreated)
                    fusedVertices.Dispose();
                if (triangles.IsCreated)
                    triangles.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
                if (rules.IsCreated)
                    rules.Dispose();
                if (sockets.IsCreated)
                    sockets.Dispose();
            }
        }

        [Test]
        public void PopulateSockets_RespectsSocketDensityHint()
        {
            NativeArray<InteriorSocketDTO1608> sockets = default;
            NativeArray<InstrumentRuleDTO1608> rules = default;
            NativeArray<InstrumentPlacementDTO1608> placements = default;
            NativeArray<InteriorBakeCountersDTO1608> counters = default;

            try
            {
                sockets = new NativeArray<InteriorSocketDTO1608>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < sockets.Length; i++)
                {
                    sockets[i] = new InteriorSocketDTO1608
                    {
                        LocalPosition = new float3(i, 0f, 0f),
                        Radius = 0.25f,
                        LocalRotation = quaternion.identity,
                        LocalNormal = new float3(0f, 0f, 1f),
                        SurfaceArea = 0.0625f,
                        StableHash = InteriorFinisherMath1608.Hash((uint)(i + 11)),
                        SocketKind = InteriorSocketKind1608.WallPanel,
                        DensityHint = i == 0 ? (byte)0 : (byte)255,
                        Flags = 1u,
                        PairIndex = -1
                    };
                }

                rules = new NativeArray<InstrumentRuleDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rules[0] = new InstrumentRuleDTO1608
                {
                    InstrumentHash = 0x1608u,
                    TypeHash = 0xFFFFFFFFu,
                    Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag,
                    BoundsExtents = new float3(0.1f, 0.08f, 0.04f),
                    MinSocketRadius = 0.02f,
                    Weight = 1f,
                    StaticVertexCount = 8u,
                    StaticIndexCount = 36u,
                    UvMin = new float2(0f, 0f),
                    UvMax = new float2(1f, 1f)
                };

                placements = new NativeArray<InstrumentPlacementDTO1608>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<InteriorBakeCountersDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                new PopulateSocketsJob1608
                {
                    Sockets = sockets,
                    Rules = rules,
                    Placements = placements,
                    Counters = counters,
                    Seed = 1608u,
                    GlobalQualityWeight = 1f,
                    DensityWeight = 1f
                }.Run();

                Assert.AreEqual(0u, counters[0].FaultFlags);
                Assert.AreEqual(1u, counters[0].PlacementCount);
                Assert.AreEqual(1, placements[0].SocketIndex);
            }
            finally
            {
                if (counters.IsCreated)
                    counters.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
                if (rules.IsCreated)
                    rules.Dispose();
                if (sockets.IsCreated)
                    sockets.Dispose();
            }
        }

        [Test]
        public void CollectSockets_ParsesDensityHintsFromMarkerNames()
        {
            GameObject root = new GameObject("TEST_DensityRoot_1608");
            var sockets = new System.Collections.Generic.List<InteriorSocketDTO1608>(4);
            var microSockets = new System.Collections.Generic.List<InteriorSocketDTO1608>(4);
            try
            {
                CreateChild(root.transform, "Socket_Wall_Panel_Sparse");
                CreateChild(root.transform, "Socket_Wall_Panel_NoAuto");
                CreateChild(root.transform, "Socket_Wall_Panel_Hero");

                InteriorSocketParser1608.CollectSockets(root, sockets, microSockets);

                bool sparse = false;
                bool noAuto = false;
                bool hero = false;
                for (int i = 0; i < sockets.Count; i++)
                {
                    sparse |= sockets[i].DensityHint == 96;
                    noAuto |= sockets[i].DensityHint == 0;
                    hero |= sockets[i].DensityHint == 255;
                }

                Assert.AreEqual(3, sockets.Count);
                Assert.IsTrue(sparse);
                Assert.IsTrue(noAuto);
                Assert.IsTrue(hero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FallbackMovableRules_DoNotAdvertiseMissingMovingVertexSlices()
        {
            InteriorInstrumentLibrary1608 library = null;
            try
            {
                library = InteriorInstrumentLibraryBuilder1608.Build("Assets/__Missing_1608_Instruments", Allocator.TempJob);
                Assert.Greater(library.Rules.Length, 0);
                uint wall = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Wall_Panel");
                int movable = 0;
                for (int i = 0; i < library.Rules.Length; i++)
                {
                    InstrumentRuleDTO1608 rule = library.Rules[i];
                    Assert.AreEqual(wall, rule.TypeHash);
                    if ((rule.Flags & InteriorFinisherConstants1608.InstrumentMovableFlag) == 0u)
                        continue;

                    movable++;
                    Assert.AreEqual(0u, rule.MovingVertexStart);
                    Assert.AreEqual(0u, rule.MovingVertexCount);
                    Assert.AreEqual(1, rule.Interactivity);
                }

                Assert.Greater(movable, 0);
                Assert.AreEqual(movable, library.MovableRuleCount);
            }
            finally
            {
                library?.Dispose();
            }
        }

        [Test]
        public void InstrumentTypeHash_ConstrainsControlsAwayFromCableSockets()
        {
            uint wall = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Wall_Panel");
            uint ceiling = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Ceiling_Cable");
            uint floor = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Floor_Conduit");

            Assert.AreEqual(wall, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Switch_Heavy"));
            Assert.AreEqual(wall, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Gauge_Oxygen"));
            Assert.AreEqual(wall, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Valve_Rotary"));
            Assert.AreEqual(ceiling, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Cable_Hanger"));
            Assert.AreEqual(floor, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Floor_Conduit_Junction"));
            Assert.AreEqual(floor, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Floor_Cable_Conduit"));
            Assert.AreEqual(0xFFFFFFFFu, InteriorInstrumentLibraryBuilder1608.ResolveInstrumentTypeHash("Unknown_Prop"));
        }

        [Test]
        public void RemapInstrumentUvs_StaysInsideAssignedAtlasRectangle()
        {
            NativeArray<float2> uvs = default;
            try
            {
                uvs = new NativeArray<float2>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                uvs[0] = new float2(0f, 0f);
                uvs[1] = new float2(1f, 0f);
                uvs[2] = new float2(1f, 1f);
                uvs[3] = new float2(0f, 1f);
                InteriorAtlasRectDTO1608 rect = new InteriorAtlasRectDTO1608
                {
                    ScaleOffset = new float4(0.25f, 0.25f, 0.5f, 0.25f),
                    Width = 1024,
                    Height = 1024,
                    X = 2048,
                    Y = 1024,
                    TextureHash = 0x1608u
                };

                new RemapInstrumentUVsJob1608
                {
                    Uvs = uvs,
                    AtlasRect = rect,
                    PadPixels = 0f,
                    AtlasSize = 4096f
                }.Run(uvs.Length);

                for (int i = 0; i < uvs.Length; i++)
                {
                    Assert.GreaterOrEqual(uvs[i].x, 0.5f);
                    Assert.LessOrEqual(uvs[i].x, 0.75f);
                    Assert.GreaterOrEqual(uvs[i].y, 0.25f);
                    Assert.LessOrEqual(uvs[i].y, 0.5f);
                    Assert.GreaterOrEqual(uvs[i].x, 0f);
                    Assert.LessOrEqual(uvs[i].x, 1f);
                    Assert.GreaterOrEqual(uvs[i].y, 0f);
                    Assert.LessOrEqual(uvs[i].y, 1f);
                }
            }
            finally
            {
                if (uvs.IsCreated)
                    uvs.Dispose();
            }
        }

        [Test]
        public void NormalMapStamping_OcclusionStartsWhiteAndDarkensStampedPixels()
        {
            const int Size = 32;
            NativeArray<InteriorRgba32DTO1608> normalPixels = default;
            NativeArray<InteriorRgba32DTO1608> grimePixels = default;
            NativeArray<InteriorSocketDTO1608> microSockets = default;

            try
            {
                normalPixels = new NativeArray<InteriorRgba32DTO1608>(Size * Size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                grimePixels = new NativeArray<InteriorRgba32DTO1608>(Size * Size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                microSockets = new NativeArray<InteriorSocketDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                InteriorRgba32DTO1608 neutral = InteriorFinisherMath1608.EncodeNormal(new float3(0f, 0f, 1f));
                InteriorRgba32DTO1608 openOcclusion = default;
                openOcclusion.R = 255;
                openOcclusion.G = 255;
                openOcclusion.B = 255;
                openOcclusion.A = 255;
                for (int i = 0; i < normalPixels.Length; i++)
                {
                    normalPixels[i] = neutral;
                    grimePixels[i] = openOcclusion;
                }

                microSockets[0] = new InteriorSocketDTO1608
                {
                    LocalPosition = new float3(0f, 2.8901734f, 7.0422535f),
                    Radius = 0.12f,
                    LocalRotation = quaternion.identity,
                    LocalNormal = new float3(0f, 0f, 1f),
                    SocketKind = InteriorSocketKind1608.MicroStamp,
                    StableHash = 1608u
                };

                new NormalMapStampingJob1608
                {
                    NormalPixels = normalPixels,
                    GrimePixels = grimePixels,
                    MicroSockets = microSockets,
                    Width = Size,
                    Height = Size,
                    GlobalQualityWeight = 1f
                }.Run();

                bool sawDarkenedStamp = false;
                bool sawUntouchedWhite = false;
                for (int i = 0; i < grimePixels.Length; i++)
                {
                    if (grimePixels[i].R < 255)
                        sawDarkenedStamp = true;
                    if (grimePixels[i].R == 255)
                        sawUntouchedWhite = true;
                }

                Assert.IsTrue(sawDarkenedStamp);
                Assert.IsTrue(sawUntouchedWhite);
            }
            finally
            {
                if (microSockets.IsCreated)
                    microSockets.Dispose();
                if (grimePixels.IsCreated)
                    grimePixels.Dispose();
                if (normalPixels.IsCreated)
                    normalPixels.Dispose();
            }
        }

        [Test]
        public void NormalMapStamping_PlacementAtlasRectReceivesWear()
        {
            const int Size = 64;
            NativeArray<InteriorRgba32DTO1608> normalPixels = default;
            NativeArray<InteriorRgba32DTO1608> grimePixels = default;
            NativeArray<InstrumentPlacementDTO1608> placements = default;

            try
            {
                normalPixels = new NativeArray<InteriorRgba32DTO1608>(Size * Size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                grimePixels = new NativeArray<InteriorRgba32DTO1608>(Size * Size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                placements = new NativeArray<InstrumentPlacementDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                InteriorRgba32DTO1608 neutral = InteriorFinisherMath1608.EncodeNormal(new float3(0f, 0f, 1f));
                InteriorRgba32DTO1608 openOcclusion = default;
                openOcclusion.R = 255;
                openOcclusion.G = 255;
                openOcclusion.B = 255;
                openOcclusion.A = 255;
                for (int i = 0; i < normalPixels.Length; i++)
                {
                    normalPixels[i] = neutral;
                    grimePixels[i] = openOcclusion;
                }

                placements[0] = new InstrumentPlacementDTO1608
                {
                    Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag,
                    InstrumentHash = 0x1608u,
                    SocketHash = 0xA11CEu,
                    PlacementHash = 0u,
                    AtlasScaleOffset = new float4(0.25f, 0.25f, 0.5f, 0.25f)
                };

                new NormalMapStampingJob1608
                {
                    NormalPixels = normalPixels,
                    GrimePixels = grimePixels,
                    Placements = placements,
                    Width = Size,
                    Height = Size,
                    GlobalQualityWeight = 1f
                }.Run();

                bool darkInsideRect = false;
                bool whiteOutsideRect = false;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        int index = y * Size + x;
                        bool inside = x >= 32 && x < 48 && y >= 16 && y < 32;
                        if (inside && grimePixels[index].R < 255)
                            darkInsideRect = true;
                        if (!inside && grimePixels[index].R == 255)
                            whiteOutsideRect = true;
                    }
                }

                Assert.IsTrue(darkInsideRect);
                Assert.IsTrue(whiteOutsideRect);
            }
            finally
            {
                if (placements.IsCreated)
                    placements.Dispose();
                if (grimePixels.IsCreated)
                    grimePixels.Dispose();
                if (normalPixels.IsCreated)
                    normalPixels.Dispose();
            }
        }

        [Test]
        public void WeldInstrumentBases_InvalidVertexRejectsWholePlacement()
        {
            NativeArray<InstrumentPlacementDTO1608> placements = default;
            NativeArray<InstrumentRuleDTO1608> rules = default;
            NativeArray<InteriorMeshVertexDTO1608> vertices = default;
            NativeArray<InteriorTriangleDTO1608> triangles = default;
            NativeList<InteriorMeshVertexDTO1608> fusedVertices = default;
            NativeList<int> fusedIndices = default;
            NativeArray<InteriorBakeCountersDTO1608> counters = default;

            try
            {
                placements = new NativeArray<InstrumentPlacementDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rules = new NativeArray<InstrumentRuleDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices = new NativeArray<InteriorMeshVertexDTO1608>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                triangles = new NativeArray<InteriorTriangleDTO1608>(12, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                FillMockCube(vertices, triangles);
                InteriorMeshVertexDTO1608 broken = vertices[3];
                broken.Position = new float3(float.NaN, 0f, 0f);
                vertices[3] = broken;

                placements[0] = new InstrumentPlacementDTO1608
                {
                    LocalToRoom = float4x4.identity,
                    InstrumentHash = 0x1608u,
                    RuleIndex = 0
                };
                rules[0] = new InstrumentRuleDTO1608
                {
                    InstrumentHash = 0x1608u,
                    Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag,
                    StaticVertexStart = 0u,
                    StaticVertexCount = 8u,
                    StaticIndexStart = 0u,
                    StaticIndexCount = 36u,
                    UvMax = new float2(1f, 1f)
                };
                counters = new NativeArray<InteriorBakeCountersDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters[0] = new InteriorBakeCountersDTO1608 { PlacementCount = 1u };
                fusedVertices = new NativeList<InteriorMeshVertexDTO1608>(8, Allocator.TempJob);
                fusedIndices = new NativeList<int>(36, Allocator.TempJob);

                new WeldInstrumentBasesJob1608
                {
                    Placements = placements,
                    Rules = rules,
                    SourceVertices = vertices,
                    SourceTriangles = triangles,
                    FusedVertices = fusedVertices,
                    FusedIndices = fusedIndices,
                    Counters = counters
                }.Run();

                Assert.AreEqual(0, fusedVertices.Length);
                Assert.AreEqual(0, fusedIndices.Length);
                Assert.AreNotEqual(0u, counters[0].FaultFlags & InteriorFinisherConstants1608.FaultNonFinite);
            }
            finally
            {
                if (counters.IsCreated)
                    counters.Dispose();
                if (fusedIndices.IsCreated)
                    fusedIndices.Dispose();
                if (fusedVertices.IsCreated)
                    fusedVertices.Dispose();
                if (triangles.IsCreated)
                    triangles.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                if (rules.IsCreated)
                    rules.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
            }
        }

        [Test]
        public void WeldInstrumentBases_InvalidTriangleRejectsWholePlacement()
        {
            NativeArray<InstrumentPlacementDTO1608> placements = default;
            NativeArray<InstrumentRuleDTO1608> rules = default;
            NativeArray<InteriorMeshVertexDTO1608> vertices = default;
            NativeArray<InteriorTriangleDTO1608> triangles = default;
            NativeList<InteriorMeshVertexDTO1608> fusedVertices = default;
            NativeList<int> fusedIndices = default;
            NativeArray<InteriorBakeCountersDTO1608> counters = default;

            try
            {
                placements = new NativeArray<InstrumentPlacementDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rules = new NativeArray<InstrumentRuleDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                vertices = new NativeArray<InteriorMeshVertexDTO1608>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                triangles = new NativeArray<InteriorTriangleDTO1608>(12, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                FillMockCube(vertices, triangles);
                InteriorTriangleDTO1608 broken = triangles[4];
                broken.Index2 = 32;
                triangles[4] = broken;

                placements[0] = new InstrumentPlacementDTO1608
                {
                    LocalToRoom = float4x4.identity,
                    InstrumentHash = 0x1608u,
                    RuleIndex = 0
                };
                rules[0] = new InstrumentRuleDTO1608
                {
                    InstrumentHash = 0x1608u,
                    Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag,
                    StaticVertexStart = 0u,
                    StaticVertexCount = 8u,
                    StaticIndexStart = 0u,
                    StaticIndexCount = 36u,
                    UvMax = new float2(1f, 1f)
                };
                counters = new NativeArray<InteriorBakeCountersDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters[0] = new InteriorBakeCountersDTO1608 { PlacementCount = 1u };
                fusedVertices = new NativeList<InteriorMeshVertexDTO1608>(8, Allocator.TempJob);
                fusedIndices = new NativeList<int>(36, Allocator.TempJob);

                new WeldInstrumentBasesJob1608
                {
                    Placements = placements,
                    Rules = rules,
                    SourceVertices = vertices,
                    SourceTriangles = triangles,
                    FusedVertices = fusedVertices,
                    FusedIndices = fusedIndices,
                    Counters = counters
                }.Run();

                Assert.AreEqual(0, fusedVertices.Length);
                Assert.AreEqual(0, fusedIndices.Length);
                Assert.AreNotEqual(0u, counters[0].FaultFlags & InteriorFinisherConstants1608.FaultInvalidMesh);
            }
            finally
            {
                if (counters.IsCreated)
                    counters.Dispose();
                if (fusedIndices.IsCreated)
                    fusedIndices.Dispose();
                if (fusedVertices.IsCreated)
                    fusedVertices.Dispose();
                if (triangles.IsCreated)
                    triangles.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                if (rules.IsCreated)
                    rules.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
            }
        }

        [Test]
        public void CableBundleGenerator_ProducesStaticMeshWithoutPhysics()
        {
            Mesh mesh = InteriorFinisherPipeline1608.GenerateCableBundles(
                "TEST_CableBundle_1608",
                new Vector3(-1f, 2f, 0f),
                new Vector3(1f, 2f, 0.5f),
                3,
                12,
                0.025f,
                0.35f,
                1608u);
            try
            {
                Assert.Greater(mesh.vertexCount, 0);
                Assert.Greater(mesh.GetIndexCount(0), 0u);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CableSocketDensityGate_RejectsDisabledCableMarkers()
        {
            InteriorSocketDTO1608 socket = new InteriorSocketDTO1608
            {
                StableHash = InteriorFinisherMath1608.Hash(1608u),
                SocketKind = InteriorSocketKind1608.CeilingCable,
                DensityHint = 0,
                Flags = 1u
            };

            Assert.IsFalse(InteriorFinisherPipeline1608.CableSocketPassesDensity(socket, 1f, 1608u));

            socket.DensityHint = 255;
            Assert.IsTrue(InteriorFinisherPipeline1608.CableSocketPassesDensity(socket, 1f, 1608u));

            socket.SocketKind = InteriorSocketKind1608.WallPanel;
            Assert.IsFalse(InteriorFinisherPipeline1608.CableSocketPassesDensity(socket, 1f, 1608u));
        }

        [Test]
        public void SourceAudit_BurstMathCoreHasNoManagedContainersOrRandom()
        {
            string source = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherContracts1608.cs");
            Assert.That(source, Does.Not.Contain("new List<"));
            Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(source, Does.Not.Contain("System.Random"));
            Assert.That(source, Does.Not.Contain("StartCoroutine"));
            Assert.That(source, Does.Not.Contain("GlobalRegistry"));
            Assert.That(source, Does.Not.Contain("GetComponent"));
            Assert.That(source, Does.Not.Contain("TryGetComponent"));
            Assert.That(source, Does.Not.Contain("TryAcquireWriteLock"));
            Assert.That(source, Does.Not.Contain("ReleaseWriteLock"));
            Assert.That(source, Does.Contain("PopulateSocketsJob1608"));
            Assert.That(source, Does.Contain("WeldInstrumentBasesJob1608"));
            Assert.That(source, Does.Contain("NormalMapStampingJob1608 : IJob"));
            Assert.That(source, Does.Contain("Placements.IsCreated"));
            Assert.That(source, Does.Contain("StampAtUv(center, radius, stampGain"));
            Assert.That(source, Does.Contain("HasEarlierAtlasPlacement(p, placement)"));
            Assert.That(source, Does.Contain("earlier.RuleIndex == placement.RuleIndex"));
            Assert.That(source, Does.Contain("RemapInstrumentUVsJob1608"));
            Assert.That(source, Does.Not.Contain("return maxRules > 0 ? 0 : -1"));
            Assert.That(source, Does.Contain("MaxRuleWeightUnits"));
            Assert.That(source, Does.Contain("float socketFitScale = math.clamp(socket.Radius / math.max(fitRadius, 0.001f), 0.55f, 2.25f)"));
            Assert.That(source, Does.Contain("scale = socketFitScale * math.lerp"));
            Assert.That(source, Does.Contain("!InteriorFinisherMath1608.IsFinite(rule.BoundsExtents)"));
            Assert.That(source, Does.Contain("math.any(rule.BoundsExtents < 0f)"));
            Assert.That(source, Does.Not.Contain("return (uint)math.max(1, (int)math.round"));
        }

        [Test]
        public void ApexProtocol_HotJobBodiesContainNoRegistrySceneLookupOrDataVaultLock()
        {
            string contracts = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherContracts1608.cs");
            string[] forbidden =
            {
                "GlobalRegistry",
                "GetComponent",
                "TryGetComponent",
                "GameObject.Find",
                "FindObjectOfType",
                "TryAcquireWriteLock",
                "ReleaseWriteLock",
                "Process.Start",
                "dotnet build",
                ".Complete("
            };

            AssertMethodBodiesDoNotContain(contracts, "Execute", forbidden);
        }

        [Test]
        public void ApexProtocol_EditorPipelineHasNoRuntimePhaseSurfaceOrJsonProofFiles()
        {
            string studio = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherStudio1608.cs");
            string contracts = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherContracts1608.cs");
            Assert.That(studio, Does.Contain("#if UNITY_EDITOR"));
            Assert.That(studio, Does.Not.Contain("ITickable"));
            Assert.That(studio, Does.Not.Contain("ILateFrameTickable"));
            Assert.That(studio, Does.Not.Contain("FixedTick("));
            Assert.That(studio, Does.Not.Contain("LateFrameTick("));
            Assert.That(studio, Does.Not.Contain("Update()"));
            Assert.That(studio, Does.Not.Contain("LateUpdate()"));
            Assert.That(studio, Does.Not.Contain("FixedUpdate()"));
            Assert.That(studio, Does.Not.Contain("GlobalRegistry"));
            Assert.That(studio, Does.Not.Contain("IDataVault"));
            Assert.That(studio, Does.Not.Contain("TryAcquireWriteLock"));
            Assert.That(studio, Does.Not.Contain("ReleaseWriteLock"));
            Assert.That(studio, Does.Not.Contain("JsonUtility"));
            Assert.That(studio, Does.Not.Contain(".json"));
            Assert.That(studio, Does.Not.Contain("WriteAllText"));
            Assert.That(studio, Does.Not.Contain("GetComponentsInChildren<Renderer>(true)"));
            Assert.That(studio, Does.Not.Contain("GetComponentsInChildren<Transform>(true).Length"));
            Assert.That(studio, Does.Not.Contain("var transforms = new List<Transform>"));
            Assert.That(studio, Does.Not.Contain("GetComponentsInChildren(true, transforms)"));
            Assert.That(studio, Does.Not.Contain("where T : struct"));
            Assert.That(studio, Does.Contain("where T : unmanaged"));
            Assert.That(studio, Does.Not.Contain("Math.Max(1, values.Count)"));
            Assert.That(studio, Does.Not.Contain("Process.Start"));
            Assert.That(studio, Does.Not.Contain("dotnet build"));
            Assert.That(studio, Does.Not.Contain("public static void ApplyAtlasRects"));
            Assert.That(studio, Does.Contain("ResolveAtlasGrid"));
            Assert.That(studio, Does.Contain("gridSide * cell"));
            Assert.That(studio, Does.Contain("cell >>= 1"));
            Assert.That(studio, Does.Contain("AppendPrefabStaticGeometry"));
            Assert.That(studio, Does.Contain("TryResolveStaticLocalBounds(prefab, out Bounds staticLocalBounds)"));
            Assert.That(studio, Does.Contain("? staticLocalBounds"));
            Assert.That(studio, Does.Contain(": ResolveLocalBounds(prefab)"));
            Assert.That(studio, Does.Contain("IsFiniteBounds(staticBounds)"));
            Assert.That(studio, Does.Contain("if (!IsFiniteBounds(localBounds))"));
            Assert.That(studio, Does.Contain("localBounds = DefaultInstrumentBounds()"));
            Assert.That(studio, Does.Contain("DefaultInstrumentBounds()"));
            Assert.That(studio, Does.Contain("if (!IsFiniteBounds(b))"));
            Assert.That(studio, Does.Contain("if (!IsFiniteBounds(local))"));
            Assert.That(studio, Does.Contain("s_meshVertexScratch.Clear();"));
            Assert.That(studio, Does.Contain("source.GetVertices(s_meshVertexScratch)"));
            Assert.That(studio, Does.Contain("source.GetTriangles(s_meshIndexScratch"));
            Assert.That(studio, Does.Contain("rootInverse * filter.transform.localToWorldMatrix"));
            Assert.That(studio, Does.Contain("ShouldSkipMovableMesh"));
            Assert.That(studio, Does.Contain("current != null && current != root"));
            Assert.That(studio, Does.Contain("name.Contains(\"Handle\""));
            Assert.That(studio, Does.Contain("name.Contains(\"Lever\""));
            Assert.That(studio, Does.Contain("ShouldSkipMicroDetailMesh"));
            Assert.That(studio, Does.Contain("name.Contains(\"Rivet\""));
            Assert.That(studio, Does.Contain("name.Contains(\"Engrave\""));
            Assert.That(studio, Does.Contain("< 0.05f"));
            Assert.That(studio, Does.Not.Contain("AppendBox(localBounds, typeHash"));
            Assert.That(studio, Does.Not.Contain("Bounds localBounds = ResolveLocalBounds(prefab);"));
            Assert.That(studio, Does.Not.Contain("source.vertices"));
            Assert.That(studio, Does.Not.Contain("source.triangles"));
            Assert.That(contracts, Does.Contain("transformed.Uv0 = rule.UvMin + sourceUv * atlasScale"));
        }

        [Test]
        public void ApexProtocol_TextureRolesKeepDataMapsLinear()
        {
            string studio = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherStudio1608.cs");
            Assert.That(studio, Does.Contain("InteriorTextureRole1608.Grime"));
            Assert.That(studio, Does.Contain("InteriorTextureRole1608.Normal"));
            Assert.That(studio, Does.Contain("InteriorTextureRole1608.Atlas"));
            Assert.That(studio, Does.Contain("role == InteriorTextureRole1608.Atlas"));
            Assert.That(studio, Does.Contain("openOcclusion.R = 255"));
            Assert.That(studio, Does.Contain("atlasPath = InteriorAtlasPacker1608.PackInstrumentAtlas"));
            Assert.That(studio, Does.Not.Contain("InteriorAtlasPacker1608.ApplyAtlasRects(library, settings.TextureSize)"));
            Assert.That(studio.IndexOf("atlasPath = InteriorAtlasPacker1608.PackInstrumentAtlas", StringComparison.Ordinal), Is.LessThan(studio.IndexOf("new PopulateSocketsJob1608", StringComparison.Ordinal)));
            Assert.That(studio.IndexOf("new PopulateSocketsJob1608", StringComparison.Ordinal), Is.LessThan(studio.IndexOf("new WeldInstrumentBasesJob1608", StringComparison.Ordinal)));
            Assert.That(studio, Does.Contain("TX_{safeName}_InstrumentAtlas_1608.png"));
            Assert.That(studio, Does.Contain("CreateOrUpdateMaterial(settings.OutputFolder, settings.OutputName"));
            Assert.That(studio, Does.Contain("MAT_{SanitizeAssetName(outputName, \"InteriorDetailPack\")}_InteriorFinisher_1608"));
            Assert.That(studio, Does.Contain("finally"));
            Assert.That(studio, Does.Contain("DestroyImmediate(texture)"));
            Assert.That(studio, Does.Contain("DestroyImmediate(atlas)"));
            Assert.That(studio, Does.Contain("new NativeArray<Color32>(pixels.Length"));
            Assert.That(studio, Does.Contain("new NativeArray<Color32>(size * size"));
            Assert.That(studio, Does.Contain("texture.SetPixelData(colors, 0)"));
            Assert.That(studio, Does.Contain("atlas.SetPixelData(clear, 0)"));
            Assert.That(studio, Does.Contain("TryFillAuthoredTextureBlock"));
            Assert.That(studio, Does.Contain("TryResolvePrimaryTexturePath(material, out string path)"));
            Assert.That(studio, Does.Contain("material.GetTexture(propertyName)"));
            Assert.That(studio, Does.Contain("\"_BaseMap\""));
            Assert.That(studio, Does.Contain("\"_AlbedoMap\""));
            Assert.That(studio, Does.Contain("AssetDatabase.LoadAssetAtPath<Texture2D>(path)"));
            Assert.That(studio, Does.Contain("Graphics.Blit(source, rt)"));
            Assert.That(studio, Does.Contain("sampleScratch.GetPixelData<Color32>(0)"));
            Assert.That(studio, Does.Contain("TryResolveAlphaBounds"));
            Assert.That(studio, Does.Contain("CopyCroppedAlphaBlock"));
            Assert.That(studio, Does.Contain("ResolveAuthoredPaddingColor(pixels, cell, minX, minY, maxX, maxY)"));
            Assert.That(studio, Does.Contain("FillBlock(block, paddingColor)"));
            Assert.That(studio, Does.Contain("opaqueCount * 100u < cropArea * 35u"));
            Assert.That(studio, Does.Contain("return AuthoredPaddingFallbackColor()"));
            Assert.That(studio, Does.Contain("ApplyPackedAtlasRect(library, i, x + writeX, y + writeY, writeWidth, writeHeight, size)"));
            Assert.That(studio, Does.Contain("private static void ApplyPackedAtlasRect"));
            Assert.That(studio, Does.Contain("rule.UvMin = uvMin"));
            Assert.That(studio, Does.Contain("rule.UvMax = uvMax"));
            Assert.That(studio, Does.Contain("float scale = Mathf.Min(available / (float)cropWidth, available / (float)cropHeight)"));
            Assert.That(studio, Does.Contain("writeWidth = Mathf.Clamp(Mathf.RoundToInt(cropWidth * scale), 1, available)"));
            Assert.That(studio, Does.Contain("writeHeight = Mathf.Clamp(Mathf.RoundToInt(cropHeight * scale), 1, available)"));
            Assert.That(studio, Does.Contain("writeX = (cell - writeWidth) >> 1"));
            Assert.That(studio, Does.Contain("writeY = (cell - writeHeight) >> 1"));
            Assert.That(studio, Does.Contain("Color32 pixel = source[sourceY * cell + sourceX]"));
            Assert.That(studio, Does.Contain("if (pixel.a <= 3)"));
            Assert.That(studio, Does.Contain("block[y * cell + x] = pixel"));
            Assert.That(studio, Does.Contain("out uint visibleArea"));
            Assert.That(studio, Does.Contain("visibleArea = (uint)(writeWidth * writeHeight)"));
            Assert.That(studio, Does.Contain("used += visibleArea"));
            Assert.That(studio, Does.Contain("RenderTexture.ReleaseTemporary(rt)"));
            Assert.That(studio, Does.Contain("ApplyTexturePlatform(importer, \"Standalone\""));
            Assert.That(studio, Does.Contain("ApplyTexturePlatform(importer, \"Android\""));
            Assert.That(studio, Does.Contain("ApplyTexturePlatform(importer, \"iPhone\""));
            Assert.That(studio, Does.Contain("TextureImporterFormat.ASTC_6x6"));
            Assert.That(studio, Does.Contain("mobileMaxTextureSize"));
            Assert.That(studio, Does.Contain("_OcclusionStrength"));
            Assert.That(studio, Does.Contain("_BumpScale"));
            Assert.That(studio, Does.Contain("_Metallic"));
            Assert.That(studio, Does.Contain("_Smoothness"));
            Assert.That(studio, Does.Contain("material.SetTexture(\"_BaseMap\", atlas)"));
            Assert.That(studio, Does.Contain("material.SetTexture(\"_MainTex\", atlas)"));
            Assert.That(studio, Does.Contain("material.SetColor(\"_BaseColor\", Color.white)"));
            Assert.That(studio, Does.Contain("material.SetTexture(\"_BaseMap\", null)"));
            Assert.That(studio, Does.Contain("counterValue.PlacementCount > 0u"));
            Assert.That(studio, Does.Not.Contain("TX_InteriorInstrumentAtlas_1608.png"));
            Assert.That(studio, Does.Not.Contain("MAT_InteriorFinisher_1608.mat"));
            Assert.That(studio, Does.Not.Contain("Mathf.Sqrt((size * size) / (float)count)"));
            Assert.That(studio, Does.Not.Contain("socket.Radius = kind == InteriorSocketKind1608.MicroStamp ? 0.018f : 0.18f"));
            Assert.That(studio, Does.Not.Contain("AppendQuad(triangles, 0, 1, 2, 3)"));
            Assert.That(studio, Does.Not.Contain("AppendBox(b, typeHash, vertexStart, vertices, triangles)"));
            Assert.That(studio, Does.Not.Contain("sRGBTexture = !normal"));
            Assert.That(studio, Does.Not.Contain("SetTextureImportSettings(grimePath, false)"));
            Assert.That(studio, Does.Not.Contain("var colors = new Color32[pixels.Length]"));
            Assert.That(studio, Does.Not.Contain("var clear = new Color32[size * size]"));
            Assert.That(studio, Does.Not.Contain("atlas.SetPixels32(clear)"));
            Assert.That(studio, Does.Not.Contain("sampleScratch.GetPixels32()"));
            Assert.That(studio, Does.Not.Contain("int writeSize = Mathf.Max(1, cell - pad * 2)"));
            Assert.That(studio, Does.Not.Contain("for (int y = pad; y < cell - pad; y++)"));
            Assert.That(studio, Does.Not.Contain("ClearBlock(block)"));
            Assert.That(studio, Does.Not.Contain("block[i] = default;"));
        }

        [Test]
        public void ApexProtocol_MovableHandlesAreVisibleStaticMeshProxies()
        {
            string studio = File.ReadAllText("Assets/_Project/Editor/Generators/Interiors/InteriorFinisherStudio1608.cs");
            Assert.That(studio, Does.Contain("CreateOrUpdateMovableHandleMeshAsset"));
            Assert.That(studio, Does.Contain("CreateCableBundleMeshAsset"));
            Assert.That(studio, Does.Contain("CreateOrUpdateCableMaterial"));
            Assert.That(studio, Does.Contain("CreateOrUpdateHandleMaterial"));
            Assert.That(studio, Does.Contain("GEN_CableBundles_1608"));
            Assert.That(studio, Does.Contain("MAT_InteriorCable_1608"));
            Assert.That(studio, Does.Contain("MAT_InteriorHandle_1608"));
            Assert.That(studio, Does.Contain("AddComponent<MeshFilter>"));
            Assert.That(studio, Does.Contain("AddComponent<MeshRenderer>"));
            Assert.That(studio, Does.Contain("ExtractUniformScale(placement.LocalToRoom)"));
            Assert.That(studio, Does.Contain("moving.transform.localScale = new Vector3(handleScale, handleScale, handleScale)"));
            Assert.That(studio, Does.Contain("shadowCastingMode = ShadowCastingMode.Off"));
            Assert.That(studio, Does.Contain("UploadMeshData(true)"));
            Assert.That(studio, Does.Contain("movingRenderer.sharedMaterial = handleMaterial != null ? handleMaterial : material"));
            Assert.That(studio, Does.Contain("MovingVertexStart = 0u"));
            Assert.That(studio, Does.Contain("MovingVertexCount = 0u"));
            Assert.That(studio, Does.Contain("Socket_Floor_Conduit"));
            Assert.That(studio, Does.Contain("((uint)kind << 24)"));
            Assert.That(studio, Does.Contain("socket.DensityHint = ResolveDensityHint(string.Empty, kind)"));
            Assert.That(studio, Does.Contain("socket.Radius = ResolveSocketRadius(rootInverse * tr.localToWorldMatrix, kind)"));
            Assert.That(studio, Does.Contain("ResolveAxisScale(localToRoot.GetColumn(0))"));
            Assert.That(studio, Does.Contain("Mathf.Clamp(radius, 0.05f, 0.45f)"));
            Assert.That(studio, Does.Contain("int localBaseIndex = vertices.Count - ruleVertexStart"));
            Assert.That(studio, Does.Contain("AppendQuad(triangles, localBaseIndex + 0, localBaseIndex + 1, localBaseIndex + 2, localBaseIndex + 3)"));
            Assert.That(studio, Does.Contain("uint instrumentHash = HashString(name)"));
            Assert.That(studio, Does.Contain("AppendBox(b, instrumentHash, vertexStart, vertices, triangles)"));
            Assert.That(studio, Does.Contain("hasFloorRoute"));
            Assert.That(studio, Does.Contain("CableSocketPassesDensity(socket, settings.DensityWeight, settings.Seed)"));
            Assert.That(studio, Does.Contain("PassesDensityGate(socket.StableHash"));
            Assert.That(studio, Does.Contain("ceilingSockets[i % ceilingSockets.Length]"));
            Assert.That(studio, Does.Contain("floorSockets[(i * 3) % floorSockets.Length]"));
            Assert.That(studio, Does.Contain("cableRenderer.sharedMaterial = cableMaterial != null ? cableMaterial : material"));
            Assert.That(studio, Does.Not.Contain("InteriorSocketDTO1608 a = cableSockets[i * 2]"));
            Assert.That(studio, Does.Not.Contain("cableRenderer.sharedMaterial = material;"));
            Assert.That(studio, Does.Not.Contain("MovingVertexCount = movable ? 8u"));
            Assert.That(studio, Does.Not.Contain("UploadMeshData(false)"));
        }

        private static void CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
        }

        private static void AssertMethodBodiesDoNotContain(string source, string methodName, string[] forbidden)
        {
            int count = 0;
            int offset = 0;
            string needle = " " + methodName + "(";
            while (true)
            {
                int signature = source.IndexOf(needle, offset, StringComparison.Ordinal);
                if (signature < 0)
                    break;

                int openBrace = source.IndexOf('{', signature);
                Assert.GreaterOrEqual(openBrace, 0);
                int depth = 0;
                int end = -1;
                for (int i = openBrace; i < source.Length; i++)
                {
                    char c = source[i];
                    if (c == '{')
                        depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end = i;
                            break;
                        }
                    }
                }

                Assert.Greater(end, openBrace);
                string body = source.Substring(openBrace, end - openBrace + 1);
                for (int i = 0; i < forbidden.Length; i++)
                    Assert.That(body, Does.Not.Contain(forbidden[i]));

                count++;
                offset = end + 1;
            }

            Assert.Greater(count, 0);
        }

        private static void FillMockCube(NativeArray<InteriorMeshVertexDTO1608> vertices, NativeArray<InteriorTriangleDTO1608> triangles)
        {
            float3[] p =
            {
                new float3(-0.05f, -0.05f, -0.05f),
                new float3(0.05f, -0.05f, -0.05f),
                new float3(0.05f, 0.05f, -0.05f),
                new float3(-0.05f, 0.05f, -0.05f),
                new float3(-0.05f, -0.05f, 0.05f),
                new float3(0.05f, -0.05f, 0.05f),
                new float3(0.05f, 0.05f, 0.05f),
                new float3(-0.05f, 0.05f, 0.05f)
            };
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new InteriorMeshVertexDTO1608
                {
                    Position = p[i],
                    Normal = math.normalizesafe(p[i], new float3(0f, 0f, 1f)),
                    Tangent = new float4(1f, 0f, 0f, 1f),
                    Uv0 = new float2((i & 1) == 0 ? 0f : 1f, (i & 2) == 0 ? 0f : 1f),
                    ColorRgba = InteriorFinisherMath1608.EncodeColor(255, 255, 255, 255),
                    Flags = 1u
                };
            }

            int write = 0;
            AppendTri(triangles, ref write, 0, 1, 2);
            AppendTri(triangles, ref write, 0, 2, 3);
            AppendTri(triangles, ref write, 5, 4, 7);
            AppendTri(triangles, ref write, 5, 7, 6);
            AppendTri(triangles, ref write, 4, 0, 3);
            AppendTri(triangles, ref write, 4, 3, 7);
            AppendTri(triangles, ref write, 1, 5, 6);
            AppendTri(triangles, ref write, 1, 6, 2);
            AppendTri(triangles, ref write, 3, 2, 6);
            AppendTri(triangles, ref write, 3, 6, 7);
            AppendTri(triangles, ref write, 4, 5, 1);
            AppendTri(triangles, ref write, 4, 1, 0);
        }

        private static void AppendTri(NativeArray<InteriorTriangleDTO1608> triangles, ref int write, int a, int b, int c)
        {
            triangles[write++] = new InteriorTriangleDTO1608
            {
                Index0 = a,
                Index1 = b,
                Index2 = c,
                Flags = 1,
                SourceHash = InteriorFinisherMath1608.Hash((uint)write)
            };
        }
    }
}

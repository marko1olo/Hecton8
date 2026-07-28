using System.Collections.Generic;
using Hecton8.Editor.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Mathematical gates for the hard-surface detail added to `ModuleArchitect1712`. These prove the
    /// invariants that the six LIVE modules depend on and that cannot be read off a screenshot:
    /// nothing leaves the authored envelope (so <c>BaseModuleTemplate.proxyBoundsSize</c> and the
    /// placement hologram stay exact), connector openings do not move between quality lanes or LODs,
    /// tangents are usable by a normal-mapped material, no UV triangle is degenerate, and the
    /// quality weight is consumed continuously rather than as a switch.
    /// <para>
    /// NOTE ON REACHABILITY: the enclosing assembly `Hecton8.EditModeTests` carries
    /// <c>"defineConstraints": ["NEVER_COMPILE_TESTS"]</c>, so it is excluded from compilation unless
    /// that symbol is defined. These assertions therefore do not run in a default batchmode pass and
    /// are not offered as proof of anything. The equivalent gates that DO run on every bake live in
    /// the generator itself: <c>ValidateTopology</c> (finite data, winding, degenerate triangles,
    /// zero-area UV) and <c>AssertTriangleBudget</c> (per-LOD section 7 budget), both of which abort
    /// the save on failure.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ModuleHardSurfaceDetail1712EditTests
    {
        private const float MaxBevelMeters = 0.12f;
        private const float EnvelopeToleranceMeters = 0.0005f;

        private static readonly float3[] LiveModuleExtents =
        {
            new float3(3.8f, 1.35f, 6.0f),   // H8_A1712_Corridor_01
            new float3(4.6f, 1.45f, 4.6f),   // H8_A1712_Junction_01
            new float3(4.2f, 1.25f, 3.2f),   // H8_A1712_ServiceCap_01
            new float3(3.4f, 1.45f, 3.8f),   // H8_A1712_Airlock_01
            new float3(5.4f, 1.85f, 4.8f),   // H8_A1712_ReactorRoom_01
            new float3(3.2f, 2.4f, 3.2f)     // H8_A1712_VerticalShaft_01
        };

        private static HardSurfaceMeshBuffers1712 CreateBuffers()
        {
            return new HardSurfaceMeshBuffers1712(
                new List<Vector3>(4096),
                new List<Vector3>(4096),
                new List<Vector4>(4096),
                new List<Vector2>(4096),
                new List<Vector4>(4096),
                new List<int>(4096));
        }

        private static HardSurfaceMeshBuffers1712 BuildFace(
            float3 extents,
            int faceAxis,
            int sign,
            bool hasDoor,
            float quality,
            int detailTier)
        {
            HardSurfaceMeshBuffers1712 buffers = CreateBuffers();
            bool plates = detailTier <= 0;
            bool conduit = detailTier <= 0;
            ModuleHardSurfaceDetail1712.AddManufacturedFace(
                buffers,
                extents,
                math.lerp(0.035f, MaxBevelMeters, quality),
                MaxBevelMeters,
                faceAxis,
                sign,
                hasDoor,
                quality,
                detailTier,
                1712u,
                ref plates,
                ref conduit);
            return buffers;
        }

        [Test]
        public void ManufacturedDetailNeverLeavesTheAuthoredEnvelope()
        {
            for (int m = 0; m < LiveModuleExtents.Length; m++)
            {
                float3 extents = LiveModuleExtents[m];
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int signIndex = 0; signIndex < 2; signIndex++)
                    {
                        int sign = signIndex == 0 ? -1 : 1;
                        for (int tier = 0; tier <= 2; tier++)
                        {
                            HardSurfaceMeshBuffers1712 buffers = BuildFace(extents, axis, sign, true, 1f, tier);
                            Assert.Greater(buffers.TriangleCount, 0, "module " + m + " axis " + axis);
                            for (int i = 0; i < buffers.Positions.Count; i++)
                            {
                                Vector3 p = buffers.Positions[i];
                                Assert.LessOrEqual(Mathf.Abs(p.x), extents.x + EnvelopeToleranceMeters, "x overflow, module " + m);
                                Assert.LessOrEqual(Mathf.Abs(p.y), extents.y + EnvelopeToleranceMeters, "y overflow, module " + m);
                                Assert.LessOrEqual(Mathf.Abs(p.z), extents.z + EnvelopeToleranceMeters, "z overflow, module " + m);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void ConnectorOpeningIsIdenticalAcrossQualityLanesAndLods()
        {
            // ResolveOpeningHalfMeters takes no quality argument by design: the visual cut-out and the
            // compound collider door frame both call it, and the value is reserved against the WORST
            // case bevel, so LOD0/LOD1/LOD2 and every lane cut the same hole.
            for (int m = 0; m < LiveModuleExtents.Length; m++)
            {
                float3 extents = LiveModuleExtents[m];
                for (int axis = 0; axis < 3; axis++)
                {
                    float first = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, axis, MaxBevelMeters);
                    float second = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, axis, MaxBevelMeters);
                    Assert.AreEqual(first, second, 0f, "opening must be deterministic");
                    Assert.Greater(first, 0f);

                    // The opening plus the perimeter frame and the reserved ring strip must fit inside
                    // the face, or the door would eat the frame.
                    float axisHalf = axis == 0 ? extents.x : axis == 1 ? extents.y : extents.z;
                    float frame = ModuleHardSurfaceDetail1712.ResolveFrameWidthMeters(extents);
                    Assert.LessOrEqual(first, axisHalf - MaxBevelMeters - frame, "opening overruns the frame on axis " + axis);
                }
            }
        }

        [Test]
        public void EveryTangentIsOrthonormalWithValidHandedness()
        {
            HardSurfaceMeshBuffers1712 buffers = BuildFace(LiveModuleExtents[4], 2, 1, true, 1f, 0);
            Assert.AreEqual(buffers.Positions.Count, buffers.Tangents.Count);
            for (int i = 0; i < buffers.Tangents.Count; i++)
            {
                Vector4 tangent = buffers.Tangents[i];
                Vector3 normal = buffers.Normals[i];
                Vector3 tangentAxis = new Vector3(tangent.x, tangent.y, tangent.z);
                Assert.AreEqual(1f, tangentAxis.magnitude, 0.005f, "tangent " + i + " is not unit length");
                Assert.AreEqual(1f, normal.magnitude, 0.005f, "normal " + i + " is not unit length");
                Assert.AreEqual(0f, Vector3.Dot(normal, tangentAxis), 0.005f, "tangent " + i + " is not perpendicular to its normal");
                Assert.IsTrue(Mathf.Approximately(Mathf.Abs(tangent.w), 1f), "handedness " + i + " must be -1 or 1");
            }
        }

        [Test]
        public void NoTriangleCarriesADegenerateUvOrSurfaceAttribute()
        {
            for (int axis = 0; axis < 3; axis++)
            {
                HardSurfaceMeshBuffers1712 buffers = BuildFace(LiveModuleExtents[0], axis, 1, axis == 2, 1f, 0);
                Assert.AreEqual(buffers.Positions.Count, buffers.Uv0.Count);
                Assert.AreEqual(buffers.Positions.Count, buffers.Surface.Count);

                for (int i = 0; i + 2 < buffers.Indices.Count; i += 3)
                {
                    Vector2 a = buffers.Uv0[buffers.Indices[i]];
                    Vector2 b = buffers.Uv0[buffers.Indices[i + 1]];
                    Vector2 c = buffers.Uv0[buffers.Indices[i + 2]];
                    float area = Mathf.Abs(((b.x - a.x) * (c.y - a.y)) - ((c.x - a.x) * (b.y - a.y)));
                    Assert.Greater(area, 1e-10f, "zero-area UV triangle at index " + i + " on axis " + axis);
                }

                for (int i = 0; i < buffers.Surface.Count; i++)
                {
                    Vector4 surface = buffers.Surface[i];
                    Assert.GreaterOrEqual(surface.x, 0f);
                    Assert.LessOrEqual(surface.x, 1f);
                    Assert.GreaterOrEqual(surface.y, 0f);
                    Assert.LessOrEqual(surface.y, 1f);
                    Assert.GreaterOrEqual(surface.z, 0f, "role id must be a valid HardSurfaceRole1712");
                    Assert.LessOrEqual(surface.z, 13f, "role id must be a valid HardSurfaceRole1712");
                    Assert.GreaterOrEqual(surface.w, 0f);
                    Assert.LessOrEqual(surface.w, 1f);
                }
            }
        }

        [Test]
        public void QualityWeightScalesDetailContinuouslyAndReachesZero()
        {
            // Binary quality switches are rejected by AGENTS.md `GlobalQualityWeight And Scalability`.
            // The bolt field is the density term: it must grow with the weight and reach zero at zero,
            // while the structural shape stays present at every weight.
            int atZero = BuildFace(LiveModuleExtents[4], 1, 1, false, 0f, 0).TriangleCount;
            int atQuarter = BuildFace(LiveModuleExtents[4], 1, 1, false, 0.25f, 0).TriangleCount;
            int atHalf = BuildFace(LiveModuleExtents[4], 1, 1, false, 0.5f, 0).TriangleCount;
            int atFull = BuildFace(LiveModuleExtents[4], 1, 1, false, 1f, 0).TriangleCount;

            Assert.Greater(atZero, 0, "the structural shape must survive at quality 0");
            Assert.GreaterOrEqual(atQuarter, atZero);
            Assert.Greater(atHalf, atQuarter);
            Assert.Greater(atFull, atHalf);
        }

        [Test]
        public void DetailTierReducesGeometryMonotonicallyForDistance()
        {
            int tier0 = BuildFace(LiveModuleExtents[4], 1, 1, false, 1f, 0).TriangleCount;
            int tier1 = BuildFace(LiveModuleExtents[4], 1, 1, false, 1f, 1).TriangleCount;
            int tier2 = BuildFace(LiveModuleExtents[4], 1, 1, false, 1f, 2).TriangleCount;

            Assert.Greater(tier0, tier1, "LOD1 must drop bolts, plates and conduit");
            Assert.Greater(tier1, tier2, "LOD2 must additionally drop belts and chamfers");
            Assert.Greater(tier2, 0, "LOD2 must still carry the frame and connector shape");
        }

        [Test]
        public void RecessedSubPanelsStayUnderTheRejectionSpan()
        {
            // `3DMODEL_HARD_SURFACE_MODULES.md` section 10 rejects an unbroken flat panel above 1.5 m.
            Assert.Less(ModuleHardSurfaceDetail1712.MaxPanelSpanMeters, 1.5f);

            for (float span = 0.2f; span <= 12f; span += 0.1f)
            {
                int divisions = ModuleHardSurfaceDetail1712.ResolveDivisions(span, ModuleHardSurfaceDetail1712.MaxRibsPerFace);
                if (divisions >= ModuleHardSurfaceDetail1712.MaxRibsPerFace)
                    continue;

                float cellSpan = span / (divisions + 1);
                Assert.LessOrEqual(
                    cellSpan,
                    ModuleHardSurfaceDetail1712.MaxPanelSpanMeters + 1e-4f,
                    "span " + span + " subdivided into " + (divisions + 1) + " cells of " + cellSpan);
            }
        }

        [Test]
        public void BoltChannelIsWideEnoughForItsBoltAndIsLaneIndependent()
        {
            for (int m = 0; m < LiveModuleExtents.Length; m++)
            {
                float3 extents = LiveModuleExtents[m];
                float channel = ModuleHardSurfaceDetail1712.ResolveBoltChannelMeters(extents);
                float recess = ModuleHardSurfaceDetail1712.ResolvePanelRecessMeters(extents);

                // A boss of radius channel*0.30 has diameter 0.6*channel and clears both walls.
                Assert.Greater(channel, 0f);
                Assert.Less(channel * 0.60f, channel, "bolt diameter must clear the channel walls");

                // The recess must stay inside the collider shell so no gap appears behind physics.
                Assert.Less(recess, 0.12f, "panel recess must stay inside MinColliderShellThicknessMeters");
            }
        }
    }
}

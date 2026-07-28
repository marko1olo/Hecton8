#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Editor.Interiors;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.Structures
{
    /// <summary>
    /// Surface role of a generated triangle. Stored per vertex in
    /// <see cref="HardSurfaceMeshBuffers1712.Surface"/>.z so the offline wear bake can apply the
    /// per-material wear coefficient from `3DMODEL_HARD_SURFACE_MODULES.md` section 5
    /// (`wear = convexity * exposureMask * materialWearCoefficient`) instead of guessing surface
    /// identity back out of a position, and so `3dmodel.md` section 3 smoothing/material class
    /// assignment has a real source.
    /// </summary>
    public enum HardSurfaceRole1712 : byte
    {
        Panel = 0,
        Frame = 1,
        Rib = 2,
        Chamfer = 3,
        StepWall = 4,
        DoorFlange = 5,
        DoorLip = 6,
        Collar = 7,
        Gasket = 8,
        Bolt = 9,
        Plate = 10,
        Conduit = 11,
        Bevel = 12,
        Rim = 13
    }

    /// <summary>
    /// Per-triangle surface attributes baked offline for the vertex colour contract in
    /// `3dmodel.md` section 4 and `3DMODEL_HARD_SURFACE_MODULES.md` section 5.
    /// <para>
    /// <see cref="Convexity"/> is the literal bible term `saturate((angleDeg - 35) / 120)`.
    /// <see cref="Cavity"/> is an analytic pocket-occlusion estimate (depth over opening width),
    /// NOT a ray-traced bake - the generator has no ray tracer and an honest approximation is
    /// declared rather than a fake AO claim.
    /// <see cref="DecalOrEmissive"/> is the alpha channel: values at or above
    /// <see cref="ModuleHardSurfaceDetail1712.EmissiveAlphaThreshold"/> mark an emissive seam
    /// strip, lower non-zero values are decal/warning-paint eligibility weight, zero forbids both.
    /// </para>
    /// </summary>
    public struct HardSurfaceAttributes1712
    {
        public float Convexity;
        public float Cavity;
        public float DecalOrEmissive;
        public HardSurfaceRole1712 Role;

        public HardSurfaceAttributes1712(HardSurfaceRole1712 role, float convexity, float cavity, float decalOrEmissive)
        {
            Role = role;
            Convexity = math.saturate(convexity);
            Cavity = math.saturate(cavity);
            DecalOrEmissive = math.saturate(decalOrEmissive);
        }

        public HardSurfaceAttributes1712 WithCavity(float cavity)
        {
            return new HardSurfaceAttributes1712(Role, Convexity, cavity, DecalOrEmissive);
        }
    }

    /// <summary>
    /// Planar UV frame. UV is a pure function of world position given the two in-surface axes, so
    /// every quad that shares a frame tiles seamlessly with its neighbours and no seam handling is
    /// required. Perpendicular surfaces (step walls, bolt shanks) must be given their OWN frame or
    /// they collapse to a zero-area UV triangle, which `3dmodel.md` section 10 rejects.
    /// </summary>
    public struct HardSurfaceUvFrame1712
    {
        public float3 UAxis;
        public float3 VAxis;
        public float MetersPerTile;

        public float2 Project(float3 position)
        {
            float inv = 1f / math.max(0.0001f, MetersPerTile);
            return new float2(math.dot(position, UAxis) * inv, math.dot(position, VAxis) * inv);
        }
    }

    /// <summary>
    /// Face-local coordinate frame. A point is addressed as (u, v, depth) where depth grows inward
    /// from the module's outer extent plane, so no detail emitted through this frame can ever leave
    /// the authored envelope and <c>BaseModuleTemplate.proxyBoundsSize</c> stays exact.
    /// </summary>
    public struct HardSurfaceFaceFrame1712
    {
        public float3 Origin;
        public float3 Outward;
        public float3 UAxis;
        public float3 VAxis;
        public int UWorldAxis;
        public int VWorldAxis;
        public float UHalf;
        public float VHalf;

        public float3 Point(float u, float v, float depth)
        {
            return Origin + (UAxis * u) + (VAxis * v) - (Outward * depth);
        }
    }

    /// <summary>
    /// Vertex/index accumulation buffers for offline hard-surface generation. Owns normals,
    /// tangents, UVs and the surface-attribute side channel because the generator owns the
    /// geometry (`3dmodel.md` section 3). Tangents are solved per triangle from the UV gradient,
    /// which is exact here because no vertex is shared between triangles.
    /// </summary>
    public sealed class HardSurfaceMeshBuffers1712
    {
        public readonly List<Vector3> Positions;
        public readonly List<Vector3> Normals;
        public readonly List<Vector4> Tangents;
        public readonly List<Vector2> Uv0;
        public readonly List<Vector4> Surface;
        public readonly List<int> Indices;

        public HardSurfaceMeshBuffers1712(
            List<Vector3> positions,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uv0,
            List<Vector4> surface,
            List<int> indices)
        {
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Normals = normals ?? throw new ArgumentNullException(nameof(normals));
            Tangents = tangents ?? throw new ArgumentNullException(nameof(tangents));
            Uv0 = uv0 ?? throw new ArgumentNullException(nameof(uv0));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        }

        public int VertexCount => Positions.Count;

        public int TriangleCount => Indices.Count / 3;

        public void Clear()
        {
            Positions.Clear();
            Normals.Clear();
            Tangents.Clear();
            Uv0.Clear();
            Surface.Clear();
            Indices.Clear();
        }

        public void AddTriangleFlat(
            float3 a,
            float3 b,
            float3 c,
            float3 normal,
            HardSurfaceUvFrame1712 frame,
            HardSurfaceAttributes1712 attributes)
        {
            EmitTriangle(
                a, normal, frame.Project(a),
                b, normal, frame.Project(b),
                c, normal, frame.Project(c),
                attributes);
        }

        public void AddQuadFlat(
            float3 a,
            float3 b,
            float3 c,
            float3 d,
            float3 normal,
            HardSurfaceUvFrame1712 frame,
            HardSurfaceAttributes1712 attributes)
        {
            AddTriangleFlat(a, b, c, normal, frame, attributes);
            AddTriangleFlat(a, c, d, normal, frame, attributes);
        }

        public void AddTriangleSmooth(
            float3 a,
            float3 na,
            float3 b,
            float3 nb,
            float3 c,
            float3 nc,
            HardSurfaceUvFrame1712 frame,
            HardSurfaceAttributes1712 attributes)
        {
            EmitTriangle(
                a, na, frame.Project(a),
                b, nb, frame.Project(b),
                c, nc, frame.Project(c),
                attributes);
        }

        public void AddQuadExplicitUv(
            float3 a,
            float3 na,
            float2 ua,
            float3 b,
            float3 nb,
            float2 ub,
            float3 c,
            float3 nc,
            float2 uc,
            float3 d,
            float3 nd,
            float2 ud,
            HardSurfaceAttributes1712 attributes)
        {
            EmitTriangle(a, na, ua, b, nb, ub, c, nc, uc, attributes);
            EmitTriangle(a, na, ua, c, nc, uc, d, nd, ud, attributes);
        }

        private void EmitTriangle(
            float3 pa,
            float3 na,
            float2 ua,
            float3 pb,
            float3 nb,
            float2 ub,
            float3 pc,
            float3 nc,
            float2 uc,
            HardSurfaceAttributes1712 attributes)
        {
            na = math.normalizesafe(na, new float3(0f, 1f, 0f));
            nb = math.normalizesafe(nb, na);
            nc = math.normalizesafe(nc, na);
            float3 authored = math.normalizesafe(na + nb + nc, na);
            if (math.dot(math.cross(pb - pa, pc - pa), authored) < 0f)
            {
                float3 swapPosition = pb;
                pb = pc;
                pc = swapPosition;
                float3 swapNormal = nb;
                nb = nc;
                nc = swapNormal;
                float2 swapUv = ub;
                ub = uc;
                uc = swapUv;
            }

            float3 edge1 = pb - pa;
            float3 edge2 = pc - pa;
            float2 delta1 = ub - ua;
            float2 delta2 = uc - ua;
            float determinant = (delta1.x * delta2.y) - (delta2.x * delta1.y);
            float3 tangentRaw;
            float3 bitangentRaw;
            if (math.abs(determinant) > 1e-12f)
            {
                float inverse = 1f / determinant;
                tangentRaw = ((edge1 * delta2.y) - (edge2 * delta1.y)) * inverse;
                bitangentRaw = ((edge2 * delta1.x) - (edge1 * delta2.x)) * inverse;
            }
            else
            {
                tangentRaw = OrthogonalAxis(authored);
                bitangentRaw = math.cross(authored, tangentRaw);
            }

            AppendVertex(pa, na, ua, tangentRaw, bitangentRaw, attributes);
            AppendVertex(pb, nb, ub, tangentRaw, bitangentRaw, attributes);
            AppendVertex(pc, nc, uc, tangentRaw, bitangentRaw, attributes);
            int start = Positions.Count - 3;
            Indices.Add(start);
            Indices.Add(start + 1);
            Indices.Add(start + 2);
        }

        private void AppendVertex(
            float3 position,
            float3 normal,
            float2 uv,
            float3 tangentRaw,
            float3 bitangentRaw,
            HardSurfaceAttributes1712 attributes)
        {
            float3 tangent = tangentRaw - (normal * math.dot(normal, tangentRaw));
            tangent = math.normalizesafe(tangent, OrthogonalAxis(normal));
            float handedness = math.dot(math.cross(normal, tangent), bitangentRaw) < 0f ? -1f : 1f;
            Positions.Add(new Vector3(position.x, position.y, position.z));
            Normals.Add(new Vector3(normal.x, normal.y, normal.z));
            Tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, handedness));
            Uv0.Add(new Vector2(uv.x, uv.y));
            Surface.Add(new Vector4(
                attributes.Convexity,
                attributes.Cavity,
                (float)(byte)attributes.Role,
                attributes.DecalOrEmissive));
        }

        public static float3 OrthogonalAxis(float3 normal)
        {
            float3 helper = math.abs(normal.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(helper, normal), new float3(1f, 0f, 0f));
        }
    }

    /// <summary>
    /// Offline manufactured-face builder for `ModuleArchitect1712`. Replaces the flat cut-out face
    /// with the meso form required by `3DMODEL_HARD_SURFACE_MODULES.md` section 1: inset panel
    /// field, perimeter inset frame, reinforcement ribs, bolted flange ring and gasket lip at every
    /// connector, recessed service plates, and a cable-gland conduit run.
    /// <para>
    /// Envelope law: every feature is addressed in face-local (u, v, depth) with depth growing
    /// INWARD from the outer extent plane. Ribs and frames are the un-recessed original surface, so
    /// the outer silhouette and therefore <c>BaseModuleTemplate.proxyBoundsSize</c> are bit-identical
    /// to the beveled-box version. Nothing protrudes, so two socketed modules cannot interpenetrate
    /// at a seam (section 10 rejection gate).
    /// </para>
    /// <para>
    /// Lane law (section 9 / `3dmodel.md` section 8): the mating geometry - opening size, flange
    /// width, collar depth, frame width, panel recess, rib pitch - is a pure function of the module
    /// extents and never of <c>GlobalQualityWeight</c>, so a module baked on the compact lane mates
    /// exactly with one baked on ultra and the silhouette does not step between lanes. Quality
    /// scales only density: bolt count, bolt facet count, and chamfer presence.
    /// </para>
    /// </summary>
    public static class ModuleHardSurfaceDetail1712
    {
        /// <summary>Hard maximum recessed sub-panel span. Section 10 rejects any unbroken flat panel above 1.5 m.</summary>
        public const float MaxPanelSpanMeters = 1.45f;

        public const int MaxRibsPerFace = 8;
        public const int MaxBeltsPerFace = 6;
        public const int MaxFrameBoltsPerFace = 20;
        public const int MaxDoorBoltsPerFace = 16;
        public const int MinBoltSides = 4;
        public const int MaxBoltSides = 6;
        public const int ConduitSides = 6;

        /// <summary>Smallest feature that may be emitted. Below this the quad is skipped so the topology validator never sees a degenerate triangle. Matches the smallest authored bevel in `3dmodel.md` section 4.</summary>
        public const float MinFeatureMeters = 0.006f;

        public const float MinFrameWidthMeters = 0.12f;
        public const float MaxFrameWidthMeters = 0.28f;

        /// <summary>
        /// Panel recess depth band. The upper bound is deliberately below
        /// `ModuleArchitect1712.MinColliderShellThicknessMeters` (0.12 m) so the recessed panel
        /// field always stays inside the box-collider shell and never reveals a gap behind physics.
        /// </summary>
        public const float MinPanelRecessMeters = 0.045f;
        public const float MaxPanelRecessMeters = 0.095f;

        public const float DoorFlangeWidthMeters = 0.13f;
        public const float MinRingStripMeters = 0.075f;
        public const float UvMetersPerTile = 1f;

        /// <summary>Vertex colour alpha at or above this marks an emissive seam strip rather than decal eligibility.</summary>
        public const float EmissiveAlphaThreshold = 0.94f;

        private const float RightAngleConvexity = (90f - 35f) / 120f;

        /// <summary>
        /// Attributes for the structural edge and corner bevel chains. Convexity is the literal
        /// `saturate((angleDeg - 35) / 120)` term of `3DMODEL_HARD_SURFACE_MODULES.md` section 5 for
        /// the 90-degree box edge these chains replace, and the bevel carries the highest wear
        /// coefficient because it is the exposed convex rim.
        /// </summary>
        public static readonly HardSurfaceAttributes1712 BevelAttributes =
            new HardSurfaceAttributes1712(HardSurfaceRole1712.Bevel, RightAngleConvexity, 0.05f, 0f);

        /// <summary>Planar UV frame from two in-surface directions, for callers outside this class.</summary>
        public static HardSurfaceUvFrame1712 CreateUvFrame(float3 uDirection, float3 vDirection)
        {
            return MakeUvFrame(uDirection, vDirection);
        }

        private static readonly HardSurfaceAttributes1712 PanelAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Panel, 0f, 0.30f, 0.45f);
        private static readonly HardSurfaceAttributes1712 FrameAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Frame, RightAngleConvexity, 0.10f, 0.30f);
        private static readonly HardSurfaceAttributes1712 RibAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Rib, RightAngleConvexity, 0.12f, 0.20f);
        private static readonly HardSurfaceAttributes1712 ChamferAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Chamfer, RightAngleConvexity, 0.08f, 0f);
        private static readonly HardSurfaceAttributes1712 StepWallAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.StepWall, 0f, 0.62f, 0f);
        private static readonly HardSurfaceAttributes1712 DoorFlangeAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.DoorFlange, RightAngleConvexity, 0.15f, 0.55f);
        private static readonly HardSurfaceAttributes1712 DoorLipAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.DoorLip, RightAngleConvexity, 0.22f, 0f);
        private static readonly HardSurfaceAttributes1712 CollarAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Collar, 0f, 0.80f, 0f);
        private static readonly HardSurfaceAttributes1712 GasketAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Gasket, 0.30f, 0.55f, 1f);
        private static readonly HardSurfaceAttributes1712 BoltCapAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Bolt, RightAngleConvexity, 0.20f, 0f);
        private static readonly HardSurfaceAttributes1712 BoltSideAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Bolt, 0.20f, 0.48f, 0f);
        private static readonly HardSurfaceAttributes1712 PlateAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Plate, RightAngleConvexity, 0.25f, 0.85f);
        private static readonly HardSurfaceAttributes1712 ConduitAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Conduit, 0.35f, 0.55f, 0f);
        private static readonly HardSurfaceAttributes1712 RimAttributes = new HardSurfaceAttributes1712(HardSurfaceRole1712.Rim, RightAngleConvexity, 0.48f, 0f);

        private struct FaceRegion1712
        {
            public float UMin;
            public float UMax;
            public float VMin;
            public float VMax;

            public float SpanU => UMax - UMin;
            public float SpanV => VMax - VMin;
            public bool IsUsable => SpanU > MinFeatureMeters && SpanV > MinFeatureMeters;
        }

        public static float ResolveFrameWidthMeters(float3 extents)
        {
            float shortest = math.cmin(math.max(extents, new float3(0.5f)));
            return math.clamp(shortest * 0.10f, MinFrameWidthMeters, MaxFrameWidthMeters);
        }

        public static float ResolvePanelRecessMeters(float3 extents)
        {
            float shortest = math.cmin(math.max(extents, new float3(0.5f)));
            return math.clamp(shortest * 0.05f, MinPanelRecessMeters, MaxPanelRecessMeters);
        }

        public static float ResolveRibWidthMeters(float3 extents)
        {
            return math.clamp(ResolveFrameWidthMeters(extents) * 0.62f, 0.075f, 0.18f);
        }

        public static float ResolveCollarDepthMeters(float3 extents)
        {
            float shortest = math.cmin(math.max(extents, new float3(0.5f)));
            return math.clamp(shortest * 0.10f, 0.14f, 0.32f);
        }

        /// <summary>
        /// Single source of truth for a connector opening half-size on one world axis. Both the
        /// visual door cut-out and the compound box-collider door frame call this, so the visible
        /// opening and the walkable opening can no longer diverge. Quality-independent by
        /// construction: it reserves the WORST-case bevel, not the current one, so LOD0/LOD1/LOD2
        /// and every quality lane cut the identical hole (`3DMODEL_HARD_SURFACE_MODULES.md`
        /// sections 7 and 9).
        /// </summary>
        public static float ResolveOpeningHalfMeters(float3 extents, int worldAxis, float maxBevelMeters)
        {
            float3 safeExtents = math.max(extents, new float3(0.5f));
            float axisHalf = ComponentAt(safeExtents, worldAxis);
            bool vertical = worldAxis == 1;
            float target = axisHalf * (vertical ? 0.66f : 0.42f);
            float maximum = vertical ? 0.95f : 1.15f;
            float available = axisHalf - maxBevelMeters - ResolveFrameWidthMeters(safeExtents) - MinRingStripMeters;
            return math.clamp(math.min(target, available), 0.35f, maximum);
        }

        /// <summary>
        /// Builds one manufactured module face. <paramref name="detailTier"/> is the LOD feature
        /// axis and is independent of <paramref name="quality"/>: tier 0 emits everything, tier 1
        /// drops bolts, service plates and conduit, tier 2 additionally drops horizontal belts and
        /// chamfers. Connector geometry is emitted at every tier so attach affordances never move
        /// between LODs.
        /// </summary>
        public static void AddManufacturedFace(
            HardSurfaceMeshBuffers1712 buffers,
            float3 extents,
            float bevel,
            float maxBevelMeters,
            int faceAxis,
            int sign,
            bool hasDoor,
            float quality,
            int detailTier,
            uint seed,
            ref bool platesRemaining,
            ref bool conduitRemaining)
        {
            if (buffers == null)
                throw new ArgumentNullException(nameof(buffers));

            float q = math.saturate(quality);
            int tier = math.clamp(detailTier, 0, 2);
            HardSurfaceFaceFrame1712 face = BuildFaceFrame(extents, bevel, faceAxis, sign);
            float frameWidth = ResolveFrameWidthMeters(extents);
            float recess = ResolvePanelRecessMeters(extents);
            float ribWidth = ResolveRibWidthMeters(extents);
            float chamfer = tier >= 2 ? 0f : math.min(recess * 0.5f, ribWidth * 0.35f);
            if (chamfer < MinFeatureMeters)
                chamfer = 0f;

            HardSurfaceUvFrame1712 faceUv = MakeUvFrame(face.UAxis, face.VAxis);
            float innerU = face.UHalf - frameWidth;
            float innerV = face.VHalf - frameWidth;
            if (innerU <= MinRingStripMeters || innerV <= MinRingStripMeters)
            {
                throw new InvalidOperationException(
                    "ModuleHardSurfaceDetail1712: module extents " + extents +
                    " are too small for a " + frameWidth.ToString("0.###") +
                    " m perimeter frame on face axis " + faceAxis + ".");
            }

            AddFlushRing(buffers, face, faceUv, face.UHalf, face.VHalf, innerU, innerV, 0f, FrameAttributes);
            AddInwardRecessRing(buffers, face, innerU, innerV, chamfer, recess);

            float floorU = innerU - chamfer;
            float floorV = innerV - chamfer;

            // Reserved bolt channel: a strip of BARE recessed floor between the frame step and the
            // rib lattice. Bolt bosses live only here, so a boss can never intersect a rib or belt
            // and leave interior faces buried inside the merge. That matters beyond wasted triangles:
            // buried interior faces create non-manifold edges, and Quadric Edge Collapse refuses to
            // collapse across one, which pins any future decimation pass at a fixed floor. This
            // generator rebuilds each LOD parametrically rather than decimating, so nothing is pinned
            // today, but the meshes must stay decimatable for the collider hull and HLOD routes.
            // Channel width is extents-derived, so it is identical on every lane and every LOD.
            float channel = ResolveBoltChannelMeters(extents);
            float latticeU = floorU - channel;
            float latticeV = floorV - channel;
            bool channelFits = latticeU > MinRingStripMeters && latticeV > MinRingStripMeters;
            if (!channelFits)
            {
                channel = 0f;
                latticeU = floorU;
                latticeV = floorV;
            }
            else
            {
                AddFlushRing(
                    buffers, face, faceUv, floorU, floorV, latticeU, latticeV, recess,
                    PanelAttributes.WithCavity(math.saturate(0.22f + ((recess / math.max(0.25f, channel)) * 0.85f))));
            }

            FaceRegion1712 latticeRegion = new FaceRegion1712 { UMin = -latticeU, UMax = latticeU, VMin = -latticeV, VMax = latticeV };

            if (!hasDoor)
            {
                AddPanelLattice(buffers, face, faceUv, latticeRegion, recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
            }
            else
            {
                float doorHalfU = ResolveOpeningHalfMeters(extents, face.UWorldAxis, maxBevelMeters);
                float doorHalfV = ResolveOpeningHalfMeters(extents, face.VWorldAxis, maxBevelMeters);
                float flangeU = doorHalfU + DoorFlangeWidthMeters;
                float flangeV = doorHalfV + DoorFlangeWidthMeters;

                // On a short face the flange merges outward until it meets the lattice boundary rather
                // than leaving a sliver strip. The limit reserves the flange's own chamfer width, so
                // `flangeU + chamfer <= latticeU` always holds: the flange step lands exactly on the
                // bolt-channel edge with neither a gap in the shell nor a buried overlap.
                float flangeLimitU = latticeU - chamfer;
                float flangeLimitV = latticeV - chamfer;
                if (flangeLimitU - flangeU < MinRingStripMeters)
                    flangeU = flangeLimitU;
                if (flangeLimitV - flangeV < MinRingStripMeters)
                    flangeV = flangeLimitV;

                if (flangeU <= doorHalfU || flangeV <= doorHalfV)
                {
                    throw new InvalidOperationException(
                        "ModuleHardSurfaceDetail1712: connector opening " + doorHalfU + " x " + doorHalfV +
                        " leaves no room for a flange ring on face axis " + faceAxis + " for extents " + extents + ".");
                }

                float doorChannelInnerU = flangeU + chamfer;
                float doorChannelInnerV = flangeV + chamfer;
                float doorChannelOuterU = math.min(doorChannelInnerU + channel, latticeU);
                float doorChannelOuterV = math.min(doorChannelInnerV + channel, latticeV);
                bool doorChannelFits =
                    doorChannelOuterU - doorChannelInnerU >= MinFeatureMeters &&
                    doorChannelOuterV - doorChannelInnerV >= MinFeatureMeters;
                if (doorChannelFits)
                {
                    AddFlushRing(
                        buffers, face, faceUv, doorChannelOuterU, doorChannelOuterV, doorChannelInnerU, doorChannelInnerV, recess,
                        PanelAttributes.WithCavity(math.saturate(0.22f + ((recess / math.max(0.25f, channel)) * 0.85f))));
                }
                else
                {
                    doorChannelOuterU = doorChannelInnerU;
                    doorChannelOuterV = doorChannelInnerV;
                }

                AddDoorAssembly(
                    buffers, face, faceUv, extents, doorHalfU, doorHalfV, flangeU, flangeV, recess, chamfer,
                    floorU, floorV, latticeU, latticeV,
                    doorChannelFits ? (doorChannelInnerU + doorChannelOuterU) * 0.5f : 0f,
                    doorChannelFits ? (doorChannelInnerV + doorChannelOuterV) * 0.5f : 0f,
                    q, tier);
                AppendDoorRingRegions(
                    buffers, face, faceUv, latticeRegion, doorChannelOuterU, doorChannelOuterV,
                    recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
            }

            if (tier <= 0 && channelFits)
            {
                int boltSides = math.clamp(
                    MinBoltSides + (int)math.round(q * (MaxBoltSides - MinBoltSides)),
                    MinBoltSides,
                    MaxBoltSides);
                float boltRadius = math.min(channel * 0.30f, ribWidth * 0.34f);
                float pathU = (floorU + latticeU) * 0.5f;
                float pathV = (floorV + latticeV) * 0.5f;
                AddBoltRing(
                    buffers,
                    face,
                    pathU,
                    pathV,
                    recess,
                    ResolveBoltCount(pathU, pathV, q, MaxFrameBoltsPerFace),
                    boltSides,
                    boltRadius,
                    recess * 0.72f);
            }
        }

        /// <summary>
        /// Width of the bare recessed strip reserved for the bolt field. Extents-derived so it is
        /// identical on every quality lane and every LOD, and wide enough that a bolt boss at
        /// <c>channel * 0.30</c> radius clears both channel walls.
        /// </summary>
        public static float ResolveBoltChannelMeters(float3 extents)
        {
            return math.clamp(ResolvePanelRecessMeters(extents) * 1.9f, 0.10f, 0.20f);
        }

        private static int ResolveBoltCount(float uHalf, float vHalf, float quality, int maximum)
        {
            if (uHalf <= MinFeatureMeters || vHalf <= MinFeatureMeters)
                return 0;

            float q = math.saturate(quality);
            return math.clamp((int)math.round(q * q * maximum), 0, maximum);
        }

        private static HardSurfaceFaceFrame1712 BuildFaceFrame(float3 extents, float bevel, int faceAxis, int sign)
        {
            float3 safeExtents = math.max(extents, new float3(0.5f));
            int firstAxis = faceAxis == 0 ? 2 : 0;
            int secondAxis = faceAxis == 1 ? 2 : 1;
            float firstHalf = ComponentAt(safeExtents, firstAxis) - bevel;
            float secondHalf = ComponentAt(safeExtents, secondAxis) - bevel;
            int uAxis = firstHalf >= secondHalf ? firstAxis : secondAxis;
            int vAxis = firstHalf >= secondHalf ? secondAxis : firstAxis;

            HardSurfaceFaceFrame1712 face = default;
            face.Outward = AxisVector(faceAxis) * sign;
            face.Origin = AxisVector(faceAxis) * (ComponentAt(safeExtents, faceAxis) * sign);
            face.UAxis = AxisVector(uAxis);
            face.VAxis = AxisVector(vAxis);
            face.UWorldAxis = uAxis;
            face.VWorldAxis = vAxis;
            face.UHalf = ComponentAt(safeExtents, uAxis) - bevel;
            face.VHalf = ComponentAt(safeExtents, vAxis) - bevel;
            return face;
        }

        private static float ComponentAt(float3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static float3 AxisVector(int axis)
        {
            return axis == 0
                ? new float3(1f, 0f, 0f)
                : axis == 1
                    ? new float3(0f, 1f, 0f)
                    : new float3(0f, 0f, 1f);
        }

        private static HardSurfaceUvFrame1712 MakeUvFrame(float3 uDirection, float3 vDirection)
        {
            return new HardSurfaceUvFrame1712
            {
                UAxis = math.normalizesafe(uDirection, new float3(1f, 0f, 0f)),
                VAxis = math.normalizesafe(vDirection, new float3(0f, 1f, 0f)),
                MetersPerTile = UvMetersPerTile
            };
        }

        /// <summary>Flat rectangular ring at a constant depth, decomposed into four non-overlapping quads.</summary>
        private static void AddFlushRing(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 uv,
            float outerU,
            float outerV,
            float innerU,
            float innerV,
            float depth,
            HardSurfaceAttributes1712 attributes)
        {
            if (outerU - innerU < MinFeatureMeters && outerV - innerV < MinFeatureMeters)
                return;

            if (outerV - innerV >= MinFeatureMeters)
            {
                buffers.AddQuadFlat(
                    face.Point(-outerU, -outerV, depth),
                    face.Point(outerU, -outerV, depth),
                    face.Point(outerU, -innerV, depth),
                    face.Point(-outerU, -innerV, depth),
                    face.Outward, uv, attributes);
                buffers.AddQuadFlat(
                    face.Point(-outerU, innerV, depth),
                    face.Point(outerU, innerV, depth),
                    face.Point(outerU, outerV, depth),
                    face.Point(-outerU, outerV, depth),
                    face.Outward, uv, attributes);
            }

            if (outerU - innerU >= MinFeatureMeters)
            {
                buffers.AddQuadFlat(
                    face.Point(-outerU, -innerV, depth),
                    face.Point(-innerU, -innerV, depth),
                    face.Point(-innerU, innerV, depth),
                    face.Point(-outerU, innerV, depth),
                    face.Outward, uv, attributes);
                buffers.AddQuadFlat(
                    face.Point(innerU, -innerV, depth),
                    face.Point(outerU, -innerV, depth),
                    face.Point(outerU, innerV, depth),
                    face.Point(innerU, innerV, depth),
                    face.Outward, uv, attributes);
            }
        }

        /// <summary>
        /// Chamfer plus step wall dropping from the flush plane into the recessed panel field. Walls
        /// face inward toward the face centre. The chamfer is mitred, so the four slanted bands meet
        /// on the corner diagonals with no overlap.
        /// </summary>
        private static void AddInwardRecessRing(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float outerU,
            float outerV,
            float chamfer,
            float recess)
        {
            float innerU = outerU - chamfer;
            float innerV = outerV - chamfer;
            if (chamfer >= MinFeatureMeters)
            {
                // Descends inward, so the free surface faces AWAY from the ring side: normal sign is -side.
                AddMitredChamferBand(buffers, face, outerU, outerV, innerU, innerV, 0f, chamfer, -1, 1f, false, ChamferAttributes);
                AddMitredChamferBand(buffers, face, outerU, outerV, innerU, innerV, 0f, chamfer, 1, -1f, false, ChamferAttributes);
                AddMitredChamferBand(buffers, face, outerU, outerV, innerU, innerV, 0f, chamfer, -1, 1f, true, ChamferAttributes);
                AddMitredChamferBand(buffers, face, outerU, outerV, innerU, innerV, 0f, chamfer, 1, -1f, true, ChamferAttributes);
            }

            float wallTop = chamfer;
            if (recess - wallTop < MinFeatureMeters)
                return;

            AddAxisWall(buffers, face, -innerU, -innerV, innerV, wallTop, recess, true, 1f, StepWallAttributes);
            AddAxisWall(buffers, face, innerU, -innerV, innerV, wallTop, recess, true, -1f, StepWallAttributes);
            AddAxisWall(buffers, face, -innerV, -innerU, innerU, wallTop, recess, false, 1f, StepWallAttributes);
            AddAxisWall(buffers, face, innerV, -innerU, innerU, wallTop, recess, false, -1f, StepWallAttributes);
        }

        /// <summary>
        /// One mitred 45-degree chamfer band. <paramref name="alongV"/> selects whether the band runs
        /// along V (a u-constant side) or along U (a v-constant side); <paramref name="side"/> is the
        /// sign of that constant coordinate.
        /// </summary>
        private static void AddMitredChamferBand(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float outerU,
            float outerV,
            float innerU,
            float innerV,
            float outerDepth,
            float innerDepth,
            int side,
            float normalLateralSign,
            bool alongV,
            HardSurfaceAttributes1712 attributes)
        {
            float3 lateral = alongV ? face.UAxis : face.VAxis;
            float3 normal = math.normalize((lateral * normalLateralSign) + face.Outward);
            HardSurfaceUvFrame1712 uv = MakeUvFrame(alongV ? face.VAxis : face.UAxis, normal);
            if (alongV)
            {
                float u0 = side * outerU;
                float u1 = side * innerU;
                buffers.AddQuadFlat(
                    face.Point(u0, -outerV, outerDepth),
                    face.Point(u0, outerV, outerDepth),
                    face.Point(u1, innerV, innerDepth),
                    face.Point(u1, -innerV, innerDepth),
                    normal, uv, attributes);
                return;
            }

            float v0 = side * outerV;
            float v1 = side * innerV;
            buffers.AddQuadFlat(
                face.Point(-outerU, v0, outerDepth),
                face.Point(outerU, v0, outerDepth),
                face.Point(innerU, v1, innerDepth),
                face.Point(-innerU, v1, innerDepth),
                normal, uv, attributes);
        }

        /// <summary>
        /// Wall at a constant in-plane coordinate, spanning depth. <paramref name="alongV"/> true
        /// means the wall sits at constant u and spans v.
        /// </summary>
        private static void AddAxisWall(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float constantCoordinate,
            float spanMin,
            float spanMax,
            float depthNear,
            float depthFar,
            bool alongV,
            float normalSign,
            HardSurfaceAttributes1712 attributes)
        {
            if (spanMax - spanMin < MinFeatureMeters || depthFar - depthNear < MinFeatureMeters)
                return;

            float3 lateral = alongV ? face.UAxis : face.VAxis;
            float3 normal = lateral * normalSign;
            HardSurfaceUvFrame1712 uv = MakeUvFrame(alongV ? face.VAxis : face.UAxis, face.Outward);
            float3 a;
            float3 b;
            float3 c;
            float3 d;
            if (alongV)
            {
                a = face.Point(constantCoordinate, spanMin, depthNear);
                b = face.Point(constantCoordinate, spanMax, depthNear);
                c = face.Point(constantCoordinate, spanMax, depthFar);
                d = face.Point(constantCoordinate, spanMin, depthFar);
            }
            else
            {
                a = face.Point(spanMin, constantCoordinate, depthNear);
                b = face.Point(spanMax, constantCoordinate, depthNear);
                c = face.Point(spanMax, constantCoordinate, depthFar);
                d = face.Point(spanMin, constantCoordinate, depthFar);
            }

            buffers.AddQuadFlat(a, b, c, d, normal, uv, attributes);
        }

        /// <summary>
        /// Lattice division count for a span. Public so the panel-span rejection gate of
        /// `3DMODEL_HARD_SURFACE_MODULES.md` section 10 can be asserted directly rather than inferred
        /// from a triangle count.
        /// </summary>
        public static int ResolveDivisions(float span, int maximum)
        {
            if (span <= MaxPanelSpanMeters)
                return 0;

            int divisions = (int)math.ceil(span / MaxPanelSpanMeters) - 1;
            return math.clamp(divisions, 0, maximum);
        }

        /// <summary>Reinforcement band standing proud of the recessed field, with a chamfer and step wall on both flanks.</summary>
        private static void AddProudBand(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            float centre,
            float halfWidth,
            float spanMin,
            float spanMax,
            float recess,
            float chamfer,
            bool alongV,
            HardSurfaceAttributes1712 topAttributes)
        {
            if (halfWidth * 2f < MinFeatureMeters || spanMax - spanMin < MinFeatureMeters)
                return;

            float3 a;
            float3 b;
            float3 c;
            float3 d;
            if (alongV)
            {
                a = face.Point(centre - halfWidth, spanMin, 0f);
                b = face.Point(centre + halfWidth, spanMin, 0f);
                c = face.Point(centre + halfWidth, spanMax, 0f);
                d = face.Point(centre - halfWidth, spanMax, 0f);
            }
            else
            {
                a = face.Point(spanMin, centre - halfWidth, 0f);
                b = face.Point(spanMax, centre - halfWidth, 0f);
                c = face.Point(spanMax, centre + halfWidth, 0f);
                d = face.Point(spanMin, centre + halfWidth, 0f);
            }

            buffers.AddQuadFlat(a, b, c, d, face.Outward, faceUv, topAttributes);
            AddBandFlank(buffers, face, centre - halfWidth, spanMin, spanMax, recess, chamfer, alongV, -1f);
            AddBandFlank(buffers, face, centre + halfWidth, spanMin, spanMax, recess, chamfer, alongV, 1f);
        }

        private static void AddBandFlank(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float edgeCoordinate,
            float spanMin,
            float spanMax,
            float recess,
            float chamfer,
            bool alongV,
            float outwardSign)
        {
            float wallCoordinate = edgeCoordinate + (outwardSign * chamfer);
            if (chamfer >= MinFeatureMeters)
            {
                float3 lateral = alongV ? face.UAxis : face.VAxis;
                float3 normal = math.normalize((lateral * outwardSign) + face.Outward);
                HardSurfaceUvFrame1712 uv = MakeUvFrame(alongV ? face.VAxis : face.UAxis, normal);
                float3 a;
                float3 b;
                float3 c;
                float3 d;
                if (alongV)
                {
                    a = face.Point(edgeCoordinate, spanMin, 0f);
                    b = face.Point(edgeCoordinate, spanMax, 0f);
                    c = face.Point(wallCoordinate, spanMax, chamfer);
                    d = face.Point(wallCoordinate, spanMin, chamfer);
                }
                else
                {
                    a = face.Point(spanMin, edgeCoordinate, 0f);
                    b = face.Point(spanMax, edgeCoordinate, 0f);
                    c = face.Point(spanMax, wallCoordinate, chamfer);
                    d = face.Point(spanMin, wallCoordinate, chamfer);
                }

                buffers.AddQuadFlat(a, b, c, d, normal, uv, ChamferAttributes);
            }

            AddAxisWall(buffers, face, wallCoordinate, spanMin, spanMax, chamfer, recess, alongV, outwardSign, StepWallAttributes);
        }

        private static float ResolveBandCentre(float rangeMin, float span, int index, int count)
        {
            return rangeMin + (span * ((float)(index + 1) / (count + 1)));
        }

        /// <summary>
        /// Rib/belt lattice plus recessed sub-panels for one rectangular region. Division counts come
        /// from the region span against <see cref="MaxPanelSpanMeters"/>, never from quality, so no
        /// lane and no LOD can leave an unbroken flat panel above the section 10 rejection size.
        /// </summary>
        private static void AddPanelLattice(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            FaceRegion1712 region,
            float recess,
            float chamfer,
            float ribWidth,
            int tier,
            uint seed,
            ref bool platesRemaining,
            ref bool conduitRemaining)
        {
            if (!region.IsUsable)
                return;

            int ribs = ResolveDivisions(region.SpanU, MaxRibsPerFace);
            int belts = tier >= 2 ? 0 : ResolveDivisions(region.SpanV, MaxBeltsPerFace);
            float halfBand = ribWidth * 0.5f;
            float bandOuter = halfBand + chamfer;

            for (int rib = 0; rib < ribs; rib++)
            {
                float centre = ResolveBandCentre(region.UMin, region.SpanU, rib, ribs);
                AddProudBand(buffers, face, faceUv, centre, halfBand, region.VMin, region.VMax, recess, chamfer, true, RibAttributes);
            }

            for (int column = 0; column <= ribs; column++)
            {
                float columnMin = column == 0
                    ? region.UMin
                    : ResolveBandCentre(region.UMin, region.SpanU, column - 1, ribs) + bandOuter;
                float columnMax = column == ribs
                    ? region.UMax
                    : ResolveBandCentre(region.UMin, region.SpanU, column, ribs) - bandOuter;
                if (columnMax - columnMin < MinFeatureMeters)
                    continue;

                for (int belt = 0; belt < belts; belt++)
                {
                    float centre = ResolveBandCentre(region.VMin, region.SpanV, belt, belts);
                    AddProudBand(buffers, face, faceUv, centre, halfBand, columnMin, columnMax, recess, chamfer, false, RibAttributes);
                }

                for (int row = 0; row <= belts; row++)
                {
                    float rowMin = row == 0
                        ? region.VMin
                        : ResolveBandCentre(region.VMin, region.SpanV, row - 1, belts) + bandOuter;
                    float rowMax = row == belts
                        ? region.VMax
                        : ResolveBandCentre(region.VMin, region.SpanV, row, belts) - bandOuter;
                    if (rowMax - rowMin < MinFeatureMeters)
                        continue;

                    AddRecessedCell(buffers, face, faceUv, columnMin, columnMax, rowMin, rowMax, recess);
                    if (tier > 0)
                        continue;

                    float cellSpanU = columnMax - columnMin;
                    float cellSpanV = rowMax - rowMin;
                    if (platesRemaining && cellSpanU >= 0.55f && cellSpanV >= 0.55f)
                    {
                        // Deterministic per-seed panel breakup, `3DMODEL_HARD_SURFACE_MODULES.md`
                        // section 4: the plate slot is fixed but its proportion varies by seed, so
                        // two modules of the same family are not identical and no run-to-run drift
                        // can occur.
                        float slot = InteriorFinisherMath1608.Hash01(
                            InteriorFinisherMath1608.Hash((column * 73) + row + 1, seed));
                        AddAccessPlate(
                            buffers, face, faceUv,
                            (columnMin + columnMax) * 0.5f,
                            (rowMin + rowMax) * 0.5f,
                            math.min(math.min(cellSpanU, cellSpanV) * 0.34f, 0.42f) * math.lerp(0.78f, 1f, slot),
                            recess,
                            chamfer,
                            slot);
                        platesRemaining = false;
                        continue;
                    }

                    if (!platesRemaining && conduitRemaining && cellSpanV >= 0.6f && cellSpanU >= 0.18f)
                    {
                        AddConduitRun(
                            buffers, face,
                            columnMin + (cellSpanU * 0.22f),
                            rowMin + (cellSpanV * 0.12f),
                            rowMax - (cellSpanV * 0.12f),
                            recess);
                        conduitRemaining = false;
                    }
                }
            }
        }

        /// <summary>
        /// Recessed sub-panel floor. Cavity is an analytic rectangular-pocket estimate: recess depth
        /// over the smaller opening span, which is the ratio that actually drives pocket occlusion.
        /// It is not a traced bake and is not reported as one.
        /// </summary>
        private static void AddRecessedCell(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            float uMin,
            float uMax,
            float vMin,
            float vMax,
            float recess)
        {
            float minimumSpan = math.min(uMax - uMin, vMax - vMin);
            float cavity = math.saturate(0.22f + ((recess / math.max(0.25f, minimumSpan)) * 0.85f));
            buffers.AddQuadFlat(
                face.Point(uMin, vMin, recess),
                face.Point(uMax, vMin, recess),
                face.Point(uMax, vMax, recess),
                face.Point(uMin, vMax, recess),
                face.Outward, faceUv, PanelAttributes.WithCavity(cavity));
        }

        /// <summary>Splits the recessed field around the connector flange into up to four clean rectangles and runs the lattice on each.</summary>
        private static void AppendDoorRingRegions(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            FaceRegion1712 region,
            float exclusionU,
            float exclusionV,
            float recess,
            float chamfer,
            float ribWidth,
            int tier,
            uint seed,
            ref bool platesRemaining,
            ref bool conduitRemaining)
        {
            FaceRegion1712 lower = new FaceRegion1712 { UMin = region.UMin, UMax = region.UMax, VMin = region.VMin, VMax = -exclusionV };
            FaceRegion1712 upper = new FaceRegion1712 { UMin = region.UMin, UMax = region.UMax, VMin = exclusionV, VMax = region.VMax };
            FaceRegion1712 left = new FaceRegion1712 { UMin = region.UMin, UMax = -exclusionU, VMin = -exclusionV, VMax = exclusionV };
            FaceRegion1712 right = new FaceRegion1712 { UMin = exclusionU, UMax = region.UMax, VMin = -exclusionV, VMax = exclusionV };

            AddPanelLattice(buffers, face, faceUv, lower, recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
            AddPanelLattice(buffers, face, faceUv, upper, recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
            AddPanelLattice(buffers, face, faceUv, left, recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
            AddPanelLattice(buffers, face, faceUv, right, recess, chamfer, ribWidth, tier, seed, ref platesRemaining, ref conduitRemaining);
        }

        /// <summary>
        /// Bolted flange ring, chamfered door lip, inward gasket collar and inner rim cap at a
        /// connector face. This is `3DMODEL_HARD_SURFACE_MODULES.md` section 4 - "Inset frame around
        /// every connector face", "Gasket or flange ring around airlocks and pipe sockets" - and it
        /// is the only geometry here the player sees from inside the doorway, so it gets the closest
        /// treatment. Every dimension is extents-derived, so the mating seam is identical on every
        /// lane and every LOD and no crack or overlap can appear (section 10).
        /// </summary>
        private static void AddDoorAssembly(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            float3 extents,
            float doorHalfU,
            float doorHalfV,
            float flangeU,
            float flangeV,
            float recess,
            float chamfer,
            float floorU,
            float floorV,
            float latticeU,
            float latticeV,
            float boltPathU,
            float boltPathV,
            float quality,
            int tier)
        {
            float lip = math.min(DoorFlangeWidthMeters * 0.42f, math.min(doorHalfU, doorHalfV) * 0.16f);
            // The flange merges outward on a short face, which can leave only a thin band between the
            // opening and the flange edge. Clamp the lip to that band, or the lip ring overshoots the
            // flange and pokes into the reserved bolt channel.
            lip = math.min(lip, math.min(flangeU - doorHalfU, flangeV - doorHalfV) * 0.55f);
            if (lip < MinFeatureMeters)
                lip = 0f;

            float lipOuterU = doorHalfU + lip;
            float lipOuterV = doorHalfV + lip;
            AddFlushRing(buffers, face, faceUv, flangeU, flangeV, lipOuterU, lipOuterV, 0f, DoorFlangeAttributes);

            if (chamfer >= MinFeatureMeters)
            {
                if (floorU - flangeU >= MinFeatureMeters)
                {
                    AddMitredChamferBand(buffers, face, flangeU + chamfer, flangeV + chamfer, flangeU, flangeV, chamfer, 0f, -1, -1f, true, ChamferAttributes);
                    AddMitredChamferBand(buffers, face, flangeU + chamfer, flangeV + chamfer, flangeU, flangeV, chamfer, 0f, 1, 1f, true, ChamferAttributes);
                }

                if (floorV - flangeV >= MinFeatureMeters)
                {
                    AddMitredChamferBand(buffers, face, flangeU + chamfer, flangeV + chamfer, flangeU, flangeV, chamfer, 0f, -1, -1f, false, ChamferAttributes);
                    AddMitredChamferBand(buffers, face, flangeU + chamfer, flangeV + chamfer, flangeU, flangeV, chamfer, 0f, 1, 1f, false, ChamferAttributes);
                }
            }

            // Guarded on floorU, not latticeU: when the flange has merged out to the lattice edge
            // there is still recessed floor beyond it - the bolt channel - so the step wall is
            // required. Guarding on latticeU here left an unwalled depth discontinuity.
            if (floorU - flangeU >= MinFeatureMeters)
            {
                AddAxisWall(buffers, face, -(flangeU + chamfer), -(flangeV + chamfer), flangeV + chamfer, chamfer, recess, true, -1f, StepWallAttributes);
                AddAxisWall(buffers, face, flangeU + chamfer, -(flangeV + chamfer), flangeV + chamfer, chamfer, recess, true, 1f, StepWallAttributes);
            }

            if (floorV - flangeV >= MinFeatureMeters)
            {
                AddAxisWall(buffers, face, -(flangeV + chamfer), -(flangeU + chamfer), flangeU + chamfer, chamfer, recess, false, -1f, StepWallAttributes);
                AddAxisWall(buffers, face, flangeV + chamfer, -(flangeU + chamfer), flangeU + chamfer, chamfer, recess, false, 1f, StepWallAttributes);
            }

            if (lip >= MinFeatureMeters)
            {
                AddMitredChamferBand(buffers, face, lipOuterU, lipOuterV, doorHalfU, doorHalfV, 0f, lip, -1, 1f, true, DoorLipAttributes);
                AddMitredChamferBand(buffers, face, lipOuterU, lipOuterV, doorHalfU, doorHalfV, 0f, lip, 1, -1f, true, DoorLipAttributes);
                AddMitredChamferBand(buffers, face, lipOuterU, lipOuterV, doorHalfU, doorHalfV, 0f, lip, -1, 1f, false, DoorLipAttributes);
                AddMitredChamferBand(buffers, face, lipOuterU, lipOuterV, doorHalfU, doorHalfV, 0f, lip, 1, -1f, false, DoorLipAttributes);
            }

            float collarDepth = ResolveCollarDepthMeters(extents);
            float gasketRise = math.min(0.026f, math.min(doorHalfU, doorHalfV) * 0.05f);
            float gasketStart = math.min(collarDepth * 0.42f, collarDepth - (gasketRise * 2f) - MinFeatureMeters);
            bool gasketFits = gasketRise >= MinFeatureMeters && gasketStart > lip + MinFeatureMeters;

            // Tunnel wall from the lip down to the gasket, then the gasket band, then down to the rim.
            float tunnelBreak = gasketFits ? gasketStart : collarDepth;
            AddCollarWalls(buffers, face, doorHalfU, doorHalfV, lip, tunnelBreak, CollarAttributes);

            if (gasketFits)
            {
                float gasketU = doorHalfU - gasketRise;
                float gasketV = doorHalfV - gasketRise;
                float gasketEnd = gasketStart + (gasketRise * 2f);
                AddCollarChamferRing(buffers, face, doorHalfU, doorHalfV, gasketU, gasketV, gasketStart, gasketStart + gasketRise, GasketAttributes);
                AddCollarWalls(buffers, face, gasketU, gasketV, gasketStart + gasketRise, gasketEnd - gasketRise, GasketAttributes);
                AddCollarChamferRing(buffers, face, gasketU, gasketV, doorHalfU, doorHalfV, gasketEnd - gasketRise, gasketEnd, GasketAttributes);
                AddCollarWalls(buffers, face, doorHalfU, doorHalfV, gasketEnd, collarDepth, CollarAttributes);
            }

            // Inner rim cap: closes the collar's open border as a design feature (section 2).
            float rimWidth = math.min(0.09f, DoorFlangeWidthMeters * 0.7f);
            AddFlushRing(buffers, face, MakeUvFrame(face.UAxis, face.VAxis), doorHalfU + rimWidth, doorHalfV + rimWidth, doorHalfU, doorHalfV, collarDepth, RimAttributes);

            if (tier > 0)
                return;

            // Bolt path comes from the caller: it is the centre line of the reserved recessed channel
            // around the flange, so the ring never intersects a rib. Zero means no channel fitted on
            // this face and the connector bolt ring is suppressed rather than forced into the lattice.
            if (boltPathU <= MinFeatureMeters || boltPathV <= MinFeatureMeters)
                return;

            if (boltPathU >= latticeU || boltPathV >= latticeV)
                return;

            int boltSides = math.clamp(
                MinBoltSides + (int)math.round(math.saturate(quality) * (MaxBoltSides - MinBoltSides)),
                MinBoltSides,
                MaxBoltSides);
            float boltRadius = math.min(
                math.min(boltPathU - flangeU - chamfer, boltPathV - flangeV - chamfer) * 0.62f,
                DoorFlangeWidthMeters * 0.26f);
            AddBoltRing(
                buffers, face, boltPathU, boltPathV, recess,
                ResolveBoltCount(boltPathU, boltPathV, quality, MaxDoorBoltsPerFace),
                boltSides, boltRadius, recess * 0.72f);
        }

        /// <summary>Four inward-facing tunnel walls of a connector collar, visible from inside the doorway.</summary>
        private static void AddCollarWalls(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float halfU,
            float halfV,
            float depthNear,
            float depthFar,
            HardSurfaceAttributes1712 attributes)
        {
            if (depthFar - depthNear < MinFeatureMeters)
                return;

            AddAxisWall(buffers, face, -halfU, -halfV, halfV, depthNear, depthFar, true, 1f, attributes);
            AddAxisWall(buffers, face, halfU, -halfV, halfV, depthNear, depthFar, true, -1f, attributes);
            AddAxisWall(buffers, face, -halfV, -halfU, halfU, depthNear, depthFar, false, 1f, attributes);
            AddAxisWall(buffers, face, halfV, -halfU, halfU, depthNear, depthFar, false, -1f, attributes);
        }

        /// <summary>Sloped ring inside a collar, used for the gasket band's two chamfers.</summary>
        private static void AddCollarChamferRing(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float nearHalfU,
            float nearHalfV,
            float farHalfU,
            float farHalfV,
            float depthNear,
            float depthFar,
            HardSurfaceAttributes1712 attributes)
        {
            if (math.abs(depthFar - depthNear) < MinFeatureMeters)
                return;

            AddCollarChamferBand(buffers, face, nearHalfU, nearHalfV, farHalfU, farHalfV, depthNear, depthFar, -1, true, attributes);
            AddCollarChamferBand(buffers, face, nearHalfU, nearHalfV, farHalfU, farHalfV, depthNear, depthFar, 1, true, attributes);
            AddCollarChamferBand(buffers, face, nearHalfU, nearHalfV, farHalfU, farHalfV, depthNear, depthFar, -1, false, attributes);
            AddCollarChamferBand(buffers, face, nearHalfU, nearHalfV, farHalfU, farHalfV, depthNear, depthFar, 1, false, attributes);
        }

        private static void AddCollarChamferBand(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float nearHalfU,
            float nearHalfV,
            float farHalfU,
            float farHalfV,
            float depthNear,
            float depthFar,
            int side,
            bool alongV,
            HardSurfaceAttributes1712 attributes)
        {
            float3 lateral = alongV ? face.UAxis : face.VAxis;
            float3 slope = alongV
                ? (face.UAxis * ((farHalfU - nearHalfU) * side)) - (face.Outward * (depthFar - depthNear))
                : (face.VAxis * ((farHalfV - nearHalfV) * side)) - (face.Outward * (depthFar - depthNear));
            float3 normal = math.normalizesafe(
                math.cross(alongV ? face.VAxis : face.UAxis, slope),
                lateral * -side);
            if (math.dot(normal, lateral * -side) < 0f)
                normal = -normal;

            HardSurfaceUvFrame1712 uv = MakeUvFrame(alongV ? face.VAxis : face.UAxis, math.normalizesafe(slope, face.Outward));
            float3 a;
            float3 b;
            float3 c;
            float3 d;
            if (alongV)
            {
                a = face.Point(side * nearHalfU, -nearHalfV, depthNear);
                b = face.Point(side * nearHalfU, nearHalfV, depthNear);
                c = face.Point(side * farHalfU, farHalfV, depthFar);
                d = face.Point(side * farHalfU, -farHalfV, depthFar);
            }
            else
            {
                a = face.Point(-nearHalfU, side * nearHalfV, depthNear);
                b = face.Point(nearHalfU, side * nearHalfV, depthNear);
                c = face.Point(farHalfU, side * farHalfV, depthFar);
                d = face.Point(-farHalfU, side * farHalfV, depthFar);
            }

            buffers.AddQuadFlat(a, b, c, d, normal, uv, attributes);
        }

        private static void AddBoltRing(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float uHalfPath,
            float vHalfPath,
            float depthFloor,
            int count,
            int sides,
            float radius,
            float height)
        {
            if (count <= 0 || sides < 3 || radius < MinFeatureMeters || height < MinFeatureMeters)
                return;

            if (uHalfPath <= radius || vHalfPath <= radius)
                return;

            float perimeter = 4f * (uHalfPath + vHalfPath);
            for (int i = 0; i < count; i++)
            {
                float distance = perimeter * ((i + 0.5f) / count);
                ResolveRectanglePathPoint(uHalfPath, vHalfPath, distance, out float u, out float v);
                AddBoltBoss(buffers, face, u, v, depthFloor, sides, radius, height);
            }
        }

        private static void ResolveRectanglePathPoint(float uHalf, float vHalf, float distance, out float u, out float v)
        {
            float width = 2f * uHalf;
            float span = 2f * vHalf;
            float t = distance;
            if (t < width)
            {
                u = -uHalf + t;
                v = -vHalf;
                return;
            }

            t -= width;
            if (t < span)
            {
                u = uHalf;
                v = -vHalf + t;
                return;
            }

            t -= span;
            if (t < width)
            {
                u = uHalf - t;
                v = vHalf;
                return;
            }

            t -= width;
            u = -uHalf;
            v = vHalf - math.min(t, span);
        }

        /// <summary>
        /// One bolt boss: an n-gon frustum rising from the recessed floor. Its head stops short of
        /// the flush plane, so a bolt field can never push the module past its authored envelope.
        /// Facet count and population both scale continuously with quality and reach zero at 0.
        /// </summary>
        private static void AddBoltBoss(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float u,
            float v,
            float depthFloor,
            int sides,
            float radius,
            float height)
        {
            float topRadius = radius * 0.78f;
            float topDepth = depthFloor - height;
            float arcStep = (2f * math.PI) / sides;
            for (int i = 0; i < sides; i++)
            {
                float angle0 = arcStep * i;
                float angle1 = arcStep * (i + 1);
                float cos0 = math.cos(angle0);
                float sin0 = math.sin(angle0);
                float cos1 = math.cos(angle1);
                float sin1 = math.sin(angle1);
                float3 radial0 = math.normalizesafe((face.UAxis * cos0) + (face.VAxis * sin0), face.UAxis);
                float3 radial1 = math.normalizesafe((face.UAxis * cos1) + (face.VAxis * sin1), face.UAxis);
                float3 normal0 = math.normalize(radial0 + (face.Outward * 0.35f));
                float3 normal1 = math.normalize(radial1 + (face.Outward * 0.35f));
                buffers.AddQuadExplicitUv(
                    face.Point(u + (cos0 * radius), v + (sin0 * radius), depthFloor), normal0, new float2(radius * angle0, 0f),
                    face.Point(u + (cos1 * radius), v + (sin1 * radius), depthFloor), normal1, new float2(radius * angle1, 0f),
                    face.Point(u + (cos1 * topRadius), v + (sin1 * topRadius), topDepth), normal1, new float2(radius * angle1, height),
                    face.Point(u + (cos0 * topRadius), v + (sin0 * topRadius), topDepth), normal0, new float2(radius * angle0, height),
                    BoltSideAttributes);
            }

            HardSurfaceUvFrame1712 capUv = MakeUvFrame(face.UAxis, face.VAxis);
            for (int i = 1; i < sides - 1; i++)
            {
                float angleI = arcStep * i;
                float angleJ = arcStep * (i + 1);
                buffers.AddTriangleFlat(
                    face.Point(u + topRadius, v, topDepth),
                    face.Point(u + (math.cos(angleI) * topRadius), v + (math.sin(angleI) * topRadius), topDepth),
                    face.Point(u + (math.cos(angleJ) * topRadius), v + (math.sin(angleJ) * topRadius), topDepth),
                    face.Outward, capUv, BoltCapAttributes);
            }
        }

        /// <summary>
        /// Bolted service plate raised from the recessed panel field with its own mitred edge
        /// treatment. This is the "service cuts / maintenance plates" item of section 1 and the
        /// alpha channel marks it as the preferred decal/label surface.
        /// </summary>
        private static void AddAccessPlate(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            HardSurfaceUvFrame1712 faceUv,
            float centreU,
            float centreV,
            float half,
            float recess,
            float chamfer,
            float seedSlot)
        {
            if (half < 0.09f)
                return;

            float plateHeight = recess * 0.78f;
            float topDepth = recess - plateHeight;
            float plateChamfer = math.min(chamfer, plateHeight * 0.45f);
            if (plateChamfer < MinFeatureMeters)
                plateChamfer = 0f;

            buffers.AddQuadFlat(
                face.Point(centreU - half, centreV - half, topDepth),
                face.Point(centreU + half, centreV - half, topDepth),
                face.Point(centreU + half, centreV + half, topDepth),
                face.Point(centreU - half, centreV + half, topDepth),
                face.Outward, faceUv, PlateAttributes);

            float outerHalf = half + plateChamfer;
            for (int i = 0; i < 4; i++)
            {
                bool alongV = i < 2;
                float sign = (i % 2) == 0 ? -1f : 1f;
                float3 lateral = alongV ? face.UAxis : face.VAxis;
                float innerEdge = (alongV ? centreU : centreV) + (sign * half);
                float outerEdge = (alongV ? centreU : centreV) + (sign * outerHalf);
                float innerSpanMin = (alongV ? centreV : centreU) - half;
                float innerSpanMax = (alongV ? centreV : centreU) + half;
                float outerSpanMin = (alongV ? centreV : centreU) - outerHalf;
                float outerSpanMax = (alongV ? centreV : centreU) + outerHalf;

                if (plateChamfer >= MinFeatureMeters)
                {
                    float3 normal = math.normalize((lateral * sign) + face.Outward);
                    HardSurfaceUvFrame1712 uv = MakeUvFrame(alongV ? face.VAxis : face.UAxis, normal);
                    float3 a = alongV ? face.Point(innerEdge, innerSpanMin, topDepth) : face.Point(innerSpanMin, innerEdge, topDepth);
                    float3 b = alongV ? face.Point(innerEdge, innerSpanMax, topDepth) : face.Point(innerSpanMax, innerEdge, topDepth);
                    float3 c = alongV ? face.Point(outerEdge, outerSpanMax, topDepth + plateChamfer) : face.Point(outerSpanMax, outerEdge, topDepth + plateChamfer);
                    float3 d = alongV ? face.Point(outerEdge, outerSpanMin, topDepth + plateChamfer) : face.Point(outerSpanMin, outerEdge, topDepth + plateChamfer);
                    buffers.AddQuadFlat(a, b, c, d, normal, uv, ChamferAttributes);
                }

                AddAxisWall(buffers, face, outerEdge, outerSpanMin, outerSpanMax, topDepth + plateChamfer, recess, alongV, sign, StepWallAttributes);
            }

            int cornerSides = MinBoltSides;
            float cornerRadius = math.min(half * 0.16f, plateHeight * 0.5f);
            float cornerOffset = half * math.lerp(0.66f, 0.78f, math.saturate(seedSlot));
            if (cornerRadius < MinFeatureMeters)
                return;

            for (int i = 0; i < 4; i++)
            {
                float signU = (i & 1) == 0 ? -1f : 1f;
                float signV = (i & 2) == 0 ? -1f : 1f;
                AddBoltBoss(
                    buffers, face,
                    centreU + (signU * cornerOffset),
                    centreV + (signV * cornerOffset),
                    topDepth,
                    cornerSides,
                    cornerRadius,
                    plateHeight * 0.45f);
            }
        }

        /// <summary>
        /// Conduit run seated in the recessed channel with a cable gland at each end. Radius is
        /// bounded by the recess depth so the run stays inside the envelope. `InteriorFinisherPipeline1608
        /// .GenerateCableBundles` was rejected for this job: it returns a standalone `Mesh` with no
        /// append-into-buffers overload and its frame degenerates on a vertical run.
        /// </summary>
        private static void AddConduitRun(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float u,
            float vStart,
            float vEnd,
            float recess)
        {
            float radius = math.min(recess * 0.42f, 0.03f);
            if (radius < MinFeatureMeters || vEnd - vStart < 0.2f)
                return;

            float axisDepth = recess - radius;
            float glandRadius = radius * 1.55f;
            float glandLength = math.min(0.055f, (vEnd - vStart) * 0.18f);
            AddConduitTube(buffers, face, u, vStart + glandLength, vEnd - glandLength, axisDepth, radius, ConduitAttributes);
            AddConduitGland(buffers, face, u, vStart, 1f, glandLength, axisDepth, radius, glandRadius);
            AddConduitGland(buffers, face, u, vEnd, -1f, glandLength, axisDepth, radius, glandRadius);
        }

        private static void AddConduitTube(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float u,
            float vStart,
            float vEnd,
            float axisDepth,
            float radius,
            HardSurfaceAttributes1712 attributes)
        {
            if (vEnd - vStart < MinFeatureMeters)
                return;

            float arcStep = (2f * math.PI) / ConduitSides;
            float length = vEnd - vStart;
            for (int i = 0; i < ConduitSides; i++)
            {
                float angle0 = arcStep * i;
                float angle1 = arcStep * (i + 1);
                float cos0 = math.cos(angle0);
                float sin0 = math.sin(angle0);
                float cos1 = math.cos(angle1);
                float sin1 = math.sin(angle1);
                float3 normal0 = math.normalizesafe((face.UAxis * cos0) - (face.Outward * sin0), face.UAxis);
                float3 normal1 = math.normalizesafe((face.UAxis * cos1) - (face.Outward * sin1), face.UAxis);
                buffers.AddQuadExplicitUv(
                    face.Point(u + (cos0 * radius), vStart, axisDepth + (sin0 * radius)), normal0, new float2(radius * angle0, 0f),
                    face.Point(u + (cos1 * radius), vStart, axisDepth + (sin1 * radius)), normal1, new float2(radius * angle1, 0f),
                    face.Point(u + (cos1 * radius), vEnd, axisDepth + (sin1 * radius)), normal1, new float2(radius * angle1, length),
                    face.Point(u + (cos0 * radius), vEnd, axisDepth + (sin0 * radius)), normal0, new float2(radius * angle0, length),
                    attributes);
            }
        }

        private static void AddConduitGland(
            HardSurfaceMeshBuffers1712 buffers,
            HardSurfaceFaceFrame1712 face,
            float u,
            float v,
            float direction,
            float length,
            float axisDepth,
            float tubeRadius,
            float glandRadius)
        {
            if (length < MinFeatureMeters || glandRadius <= tubeRadius)
                return;

            float vInner = v + (direction * length);
            AddConduitTube(buffers, face, u, math.min(v, vInner), math.max(v, vInner), axisDepth, glandRadius, ConduitAttributes);

            float arcStep = (2f * math.PI) / ConduitSides;
            float3 annulusNormal = face.VAxis * direction;
            float3 capNormal = face.VAxis * -direction;
            HardSurfaceUvFrame1712 ringUv = MakeUvFrame(face.UAxis, face.Outward);
            for (int i = 0; i < ConduitSides; i++)
            {
                float angle0 = arcStep * i;
                float angle1 = arcStep * (i + 1);
                float cos0 = math.cos(angle0);
                float sin0 = math.sin(angle0);
                float cos1 = math.cos(angle1);
                float sin1 = math.sin(angle1);
                buffers.AddQuadFlat(
                    face.Point(u + (cos0 * glandRadius), vInner, axisDepth + (sin0 * glandRadius)),
                    face.Point(u + (cos1 * glandRadius), vInner, axisDepth + (sin1 * glandRadius)),
                    face.Point(u + (cos1 * tubeRadius), vInner, axisDepth + (sin1 * tubeRadius)),
                    face.Point(u + (cos0 * tubeRadius), vInner, axisDepth + (sin0 * tubeRadius)),
                    annulusNormal, ringUv, ConduitAttributes);
            }

            for (int i = 1; i < ConduitSides - 1; i++)
            {
                float angleI = arcStep * i;
                float angleJ = arcStep * (i + 1);
                buffers.AddTriangleFlat(
                    face.Point(u + glandRadius, v, axisDepth),
                    face.Point(u + (math.cos(angleI) * glandRadius), v, axisDepth + (math.sin(angleI) * glandRadius)),
                    face.Point(u + (math.cos(angleJ) * glandRadius), v, axisDepth + (math.sin(angleJ) * glandRadius)),
                    capNormal, ringUv, ConduitAttributes);
            }
        }
    }
}
#endif

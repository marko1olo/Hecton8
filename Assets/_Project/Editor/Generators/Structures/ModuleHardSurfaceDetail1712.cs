#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

        public void AddQuadSmooth(
            float3 a,
            float3 na,
            float3 b,
            float3 nb,
            float3 c,
            float3 nc,
            float3 d,
            float3 nd,
            HardSurfaceUvFrame1712 frame,
            HardSurfaceAttributes1712 attributes)
        {
            AddTriangleSmooth(a, na, b, nb, c, nc, frame, attributes);
            AddTriangleSmooth(a, na, c, nc, d, nd, frame, attributes);
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

        public void AddTriangleExplicitUv(
            float3 a,
            float3 na,
            float2 ua,
            float3 b,
            float3 nb,
            float2 ub,
            float3 c,
            float3 nc,
            float2 uc,
            HardSurfaceAttributes1712 attributes)
        {
            EmitTriangle(a, na, ua, b, nb, ub, c, nc, uc, attributes);
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
}
#endif

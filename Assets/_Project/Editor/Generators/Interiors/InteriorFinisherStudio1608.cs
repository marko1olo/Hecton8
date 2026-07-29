#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Hecton8.Editor.ColliderOptimization1716;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.Editor.Interiors
{
    public struct InteriorFinisherSettings1608
    {
        public GameObject ModulePrefab;
        public string InstrumentPrefabFolder;
        public string OutputFolder;
        public string OutputName;
        public uint Seed;
        public float GlobalQualityWeight;
        public float DensityWeight;
        public int TextureSize;

        /// <summary>
        /// Opt-in for a diagnostic bake that accepts the procedural fallback instrument kit
        /// and/or the bounding-box socket grid. Default false, so an unfed bake fails closed
        /// with the exact missing input instead of writing box art into the project.
        /// </summary>
        public bool AllowFallbackKit;

        public static InteriorFinisherSettings1608 Default
        {
            get
            {
                return new InteriorFinisherSettings1608
                {
                    InstrumentPrefabFolder = InteriorInstrumentLibraryBuilder1608.DefaultInstrumentFolder,
                    OutputFolder = "Assets/_Project/Art/Baked/Interiors",
                    OutputName = "GEN_InteriorDetailPack_1608",
                    Seed = 1608u,
                    GlobalQualityWeight = 0.72f,
                    DensityWeight = 0.85f,
                    TextureSize = 1024
                };
            }
        }
    }

    public struct InteriorFinisherResult1608
    {
        public bool Success;
        public string PrefabPath;
        public string MeshPath;
        public string CableMeshPath;
        public string AtlasPath;
        public string NormalPath;
        public string GrimePath;
        public string FailureReason;
        public InteriorBakeCountersDTO1608 Counters;
        public int SocketCount;
        public int MicroSocketCount;
        public float AtlasEfficiency01;

        /// <summary>
        /// True when the instrument library came from the six procedural boxes in
        /// AppendFallbackRules instead of authored prefabs. Bible-rejected as final visuals.
        /// </summary>
        public bool UsedFallbackInstrumentKit;

        /// <summary>
        /// True when the socket layout came from AppendFallbackSockets - a grid derived from
        /// the module renderer AABB - instead of authored Socket_* / DecorativeSocket markers.
        /// The AABB grid ignores doorways, wall thickness, and frames.
        /// </summary>
        public bool UsedFallbackSocketLayout;
    }

    internal enum InteriorTextureRole1608
    {
        Atlas,
        Normal,
        Grime
    }

    public sealed class InteriorInstrumentLibrary1608 : IDisposable
    {
        public NativeArray<InstrumentRuleDTO1608> Rules;
        public NativeArray<InteriorMeshVertexDTO1608> Vertices;
        public NativeArray<InteriorTriangleDTO1608> Triangles;
        public string[] Names = Array.Empty<string>();
        public string[] Paths = Array.Empty<string>();
        public string[] TexturePaths = Array.Empty<string>();
        public Bounds[] Bounds = Array.Empty<Bounds>();
        public int MaxStaticVertices;
        public int MaxStaticIndices;
        public int MovableRuleCount;

        /// <summary>
        /// True when Build could not read a single authored instrument prefab and substituted
        /// the six procedural boxes from AppendFallbackRules.
        /// </summary>
        public bool UsedFallbackKit;

        public void Dispose()
        {
            if (Rules.IsCreated)
                Rules.Dispose();
            if (Vertices.IsCreated)
                Vertices.Dispose();
            if (Triangles.IsCreated)
                Triangles.Dispose();
        }
    }

    public static class InteriorInstrumentLibraryBuilder1608
    {
        public const string DefaultInstrumentFolder = "Assets/_Project/Prefabs/Instruments";
        private const uint TypeAny = 0xFFFFFFFFu;
        private static readonly string[] s_primaryTextureProperties =
        {
            "_BaseMap",
            "_MainTex",
            "_BaseColorMap",
            "_AlbedoMap"
        };
        // COLD ALLOC: List<Renderer>[64] - editor-only prefab renderer scan scratch - owner: InteriorInstrumentLibraryBuilder1608
        private static readonly List<Renderer> s_rendererScratch = new List<Renderer>(64);
        // COLD ALLOC: Mesh extraction scratch lists - editor-only authored prefab fusion - owner: InteriorInstrumentLibraryBuilder1608
        private static readonly List<MeshFilter> s_meshFilterScratch = new List<MeshFilter>(64);
        private static readonly List<Vector3> s_meshVertexScratch = new List<Vector3>(1024);
        private static readonly List<Vector3> s_meshNormalScratch = new List<Vector3>(1024);
        private static readonly List<Vector4> s_meshTangentScratch = new List<Vector4>(1024);
        private static readonly List<Vector2> s_meshUvScratch = new List<Vector2>(1024);
        private static readonly List<int> s_meshIndexScratch = new List<int>(2048);

        public static InteriorInstrumentLibrary1608 Build(string folder, Allocator allocator)
        {
            string safeFolder = string.IsNullOrWhiteSpace(folder) ? DefaultInstrumentFolder : folder.Trim().Replace('\\', '/');
            var rules = new List<InstrumentRuleDTO1608>(InteriorFinisherConstants1608.MaxInstrumentRules);
            var vertices = new List<InteriorMeshVertexDTO1608>(512);
            var triangles = new List<InteriorTriangleDTO1608>(512);
            var names = new List<string>(InteriorFinisherConstants1608.MaxInstrumentRules);
            var paths = new List<string>(InteriorFinisherConstants1608.MaxInstrumentRules);
            var texturePaths = new List<string>(InteriorFinisherConstants1608.MaxInstrumentRules);
            var bounds = new List<Bounds>(InteriorFinisherConstants1608.MaxInstrumentRules);

            if (AssetDatabase.IsValidFolder(safeFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { safeFolder });
                Array.Sort(guids, StringComparer.Ordinal);
                for (int i = 0; i < guids.Length && rules.Count < InteriorFinisherConstants1608.MaxInstrumentRules; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    AppendPrefabRule(prefab, path, rules, vertices, triangles, names, paths, texturePaths, bounds);
                }
            }

            bool usedFallbackKit = rules.Count == 0;
            if (usedFallbackKit)
                AppendFallbackRules(rules, vertices, triangles, names, paths, texturePaths, bounds);

            var library = new InteriorInstrumentLibrary1608
            {
                Rules = ToNative(rules, allocator),
                Vertices = ToNative(vertices, allocator),
                Triangles = ToNative(triangles, allocator),
                Names = names.ToArray(),
                Paths = paths.ToArray(),
                TexturePaths = texturePaths.ToArray(),
                Bounds = bounds.ToArray(),
                UsedFallbackKit = usedFallbackKit
            };

            ResolveCapacityStats(library);
            return library;
        }

        private static void AppendPrefabRule(
            GameObject prefab,
            string path,
            List<InstrumentRuleDTO1608> rules,
            List<InteriorMeshVertexDTO1608> vertices,
            List<InteriorTriangleDTO1608> triangles,
            List<string> names,
            List<string> paths,
            List<string> texturePaths,
            List<Bounds> bounds)
        {
            Bounds localBounds = TryResolveStaticLocalBounds(prefab, out Bounds staticLocalBounds)
                ? staticLocalBounds
                : ResolveLocalBounds(prefab);
            if (!IsFiniteBounds(localBounds))
                localBounds = DefaultInstrumentBounds();

            int vertexStart = vertices.Count;
            int triangleStart = triangles.Count;
            uint typeHash = ResolveInstrumentTypeHash(prefab.name);
            uint instrumentHash = HashString(path);
            bool movable = IsMovableInstrument(prefab.name);
            if (!AppendPrefabStaticGeometry(prefab, instrumentHash, vertexStart, vertices, triangles))
                AppendBox(localBounds, instrumentHash, vertexStart, vertices, triangles);
            string texturePath = ResolvePrimaryTexturePath(prefab);
            int staticVertexCount = vertices.Count - vertexStart;
            int staticIndexCount = (triangles.Count - triangleStart) * 3;

            InstrumentRuleDTO1608 rule = default;
            rule.InstrumentHash = instrumentHash;
            rule.TypeHash = typeHash == 0u ? TypeAny : typeHash;
            rule.TextureHash = HashString(texturePath);
            rule.Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag | (movable ? InteriorFinisherConstants1608.InstrumentMovableFlag : 0u);
            rule.BoundsExtents = new float3(localBounds.extents.x, localBounds.extents.y, localBounds.extents.z);
            rule.MinSocketRadius = Mathf.Max(0.05f, Mathf.Max(localBounds.extents.x, localBounds.extents.y) * 0.35f);
            rule.Weight = ResolveWeight(prefab.name, movable);
            rule.StaticVertexStart = (uint)vertexStart;
            rule.StaticVertexCount = (uint)staticVertexCount;
            rule.StaticIndexStart = (uint)triangleStart;
            rule.StaticIndexCount = (uint)staticIndexCount;
            rule.MovingVertexStart = 0u;
            rule.MovingVertexCount = 0u;
            rule.AtlasSourceIndex = (ushort)Mathf.Clamp(texturePaths.Count, 0, ushort.MaxValue);
            rule.Interactivity = movable ? (ushort)1 : (ushort)0;
            rule.UvMax = new float2(1f, 1f);
            rules.Add(rule);
            names.Add(prefab.name);
            paths.Add(path);
            texturePaths.Add(texturePath);
            bounds.Add(localBounds);
        }

        private static bool AppendPrefabStaticGeometry(
            GameObject prefab,
            uint instrumentHash,
            int ruleVertexStart,
            List<InteriorMeshVertexDTO1608> vertices,
            List<InteriorTriangleDTO1608> triangles)
        {
            int sourceVertexStart = vertices.Count;
            int sourceTriangleStart = triangles.Count;
            s_meshFilterScratch.Clear();
            prefab.GetComponentsInChildren(true, s_meshFilterScratch);
            try
            {
                Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
                int appendedTriangleCount = 0;
                for (int i = 0; i < s_meshFilterScratch.Count; i++)
                {
                    MeshFilter filter = s_meshFilterScratch[i];
                    if (ShouldSkipMovableMesh(filter != null ? filter.transform : null, prefab.transform))
                        continue;

                    Mesh source = filter != null ? filter.sharedMesh : null;
                    if (source == null || source.vertexCount <= 0)
                        continue;

                    Matrix4x4 localToRoot = rootInverse * filter.transform.localToWorldMatrix;
                    if (ShouldSkipMicroDetailMesh(filter.transform, prefab.transform, localToRoot, source.bounds))
                        continue;

                    bool flippedWinding = localToRoot.determinant < 0f;
                    int meshVertexStart = vertices.Count;
                    int meshTriangleStart = triangles.Count;
                    int localVertexBase = meshVertexStart - ruleVertexStart;
                    s_meshVertexScratch.Clear();
                    s_meshNormalScratch.Clear();
                    s_meshTangentScratch.Clear();
                    s_meshUvScratch.Clear();
                    source.GetVertices(s_meshVertexScratch);
                    source.GetNormals(s_meshNormalScratch);
                    source.GetTangents(s_meshTangentScratch);
                    source.GetUVs(0, s_meshUvScratch);

                    int vertexCount = s_meshVertexScratch.Count;
                    bool meshValid = true;
                    for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                    {
                        Vector3 sourcePosition = s_meshVertexScratch[vertexIndex];
                        Vector3 transformedPosition = localToRoot.MultiplyPoint3x4(sourcePosition);
                        Vector3 sourceNormal = vertexIndex < s_meshNormalScratch.Count ? s_meshNormalScratch[vertexIndex] : Vector3.forward;
                        Vector3 transformedNormal = localToRoot.MultiplyVector(sourceNormal).normalized;
                        if (transformedNormal.sqrMagnitude <= 0.000001f)
                            transformedNormal = Vector3.forward;

                        Vector4 sourceTangent = vertexIndex < s_meshTangentScratch.Count ? s_meshTangentScratch[vertexIndex] : new Vector4(1f, 0f, 0f, 1f);
                        Vector3 tangentVector = localToRoot.MultiplyVector(new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z)).normalized;
                        if (tangentVector.sqrMagnitude <= 0.000001f)
                            tangentVector = Vector3.right;

                        Vector2 sourceUv = vertexIndex < s_meshUvScratch.Count ? s_meshUvScratch[vertexIndex] : Vector2.zero;
                        float3 position = new float3(transformedPosition.x, transformedPosition.y, transformedPosition.z);
                        float3 normal = new float3(transformedNormal.x, transformedNormal.y, transformedNormal.z);
                        float4 tangent = new float4(tangentVector.x, tangentVector.y, tangentVector.z, sourceTangent.w * (flippedWinding ? -1f : 1f));
                        float2 uv = new float2(sourceUv.x, sourceUv.y);
                        if (!InteriorFinisherMath1608.IsFinite(position) ||
                            !InteriorFinisherMath1608.IsFinite(normal) ||
                            !InteriorFinisherMath1608.IsFinite(tangent) ||
                            !InteriorFinisherMath1608.IsFinite(uv))
                        {
                            meshValid = false;
                            break;
                        }

                        vertices.Add(new InteriorMeshVertexDTO1608
                        {
                            Position = position,
                            Normal = normal,
                            Tangent = tangent,
                            Uv0 = uv,
                            ColorRgba = InteriorFinisherMath1608.EncodeColor(140, 132, 112, 255),
                            Flags = 1u,
                            InstrumentHash = instrumentHash
                        });
                    }

                    if (!meshValid)
                    {
                        vertices.RemoveRange(meshVertexStart, vertices.Count - meshVertexStart);
                        continue;
                    }

                    for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                    {
                        s_meshIndexScratch.Clear();
                        source.GetTriangles(s_meshIndexScratch, subMesh, true);
                        for (int index = 0; index + 2 < s_meshIndexScratch.Count; index += 3)
                        {
                            int a = s_meshIndexScratch[index];
                            int b = s_meshIndexScratch[index + 1];
                            int c = s_meshIndexScratch[index + 2];
                            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
                                continue;

                            uint hash = InteriorFinisherMath1608.Hash(instrumentHash ^ (uint)(triangles.Count + 1) ^ (uint)(subMesh + 1));
                            triangles.Add(new InteriorTriangleDTO1608
                            {
                                Index0 = localVertexBase + a,
                                Index1 = localVertexBase + (flippedWinding ? c : b),
                                Index2 = localVertexBase + (flippedWinding ? b : c),
                                SourceHash = hash,
                                Flags = 1
                            });
                            appendedTriangleCount++;
                        }
                    }

                    if (triangles.Count == meshTriangleStart)
                        vertices.RemoveRange(meshVertexStart, vertices.Count - meshVertexStart);
                }

                if (appendedTriangleCount > 0)
                    return true;

                vertices.RemoveRange(sourceVertexStart, vertices.Count - sourceVertexStart);
                triangles.RemoveRange(sourceTriangleStart, triangles.Count - sourceTriangleStart);
                return false;
            }
            finally
            {
                s_meshIndexScratch.Clear();
                s_meshUvScratch.Clear();
                s_meshTangentScratch.Clear();
                s_meshNormalScratch.Clear();
                s_meshVertexScratch.Clear();
                s_meshFilterScratch.Clear();
            }
        }

        private static bool TryResolveStaticLocalBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            s_meshFilterScratch.Clear();
            prefab.GetComponentsInChildren(true, s_meshFilterScratch);
            try
            {
                Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
                bool has = false;
                for (int i = 0; i < s_meshFilterScratch.Count; i++)
                {
                    MeshFilter filter = s_meshFilterScratch[i];
                    if (filter == null || ShouldSkipMovableMesh(filter.transform, prefab.transform))
                        continue;

                    Mesh source = filter.sharedMesh;
                    if (source == null || source.vertexCount <= 0)
                        continue;

                    Matrix4x4 localToRoot = rootInverse * filter.transform.localToWorldMatrix;
                    if (ShouldSkipMicroDetailMesh(filter.transform, prefab.transform, localToRoot, source.bounds))
                        continue;

                    Bounds staticBounds = TransformBounds(localToRoot, source.bounds);
                    if (!IsFiniteBounds(staticBounds))
                        continue;

                    if (!has)
                    {
                        bounds = staticBounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(staticBounds);
                    }
                }

                return has;
            }
            finally
            {
                s_meshFilterScratch.Clear();
            }
        }

        private static bool ShouldSkipMovableMesh(Transform transform, Transform root)
        {
            for (Transform current = transform; current != null && current != root; current = current.parent)
            {
                string name = current.name;
                if (name.StartsWith("MOV_", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("_MOV", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Moving", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Lever", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Knob", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Actuator", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Needle", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("ValveWheel", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ShouldSkipMicroDetailMesh(Transform transform, Transform root, Matrix4x4 localToRoot, Bounds meshBounds)
        {
            if (transform == null || transform == root)
                return false;

            for (Transform current = transform; current != null && current != root; current = current.parent)
            {
                string name = current.name;
                if (name.Contains("Screw", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Rivet", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Bolt", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Seam", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Label", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Text", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Engrave", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Decal", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            Bounds rootBounds = TransformBounds(localToRoot, meshBounds);
            Vector3 size = rootBounds.size;
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.05f;
        }

        private static void AppendFallbackRules(
            List<InstrumentRuleDTO1608> rules,
            List<InteriorMeshVertexDTO1608> vertices,
            List<InteriorTriangleDTO1608> triangles,
            List<string> names,
            List<string> paths,
            List<string> texturePaths,
            List<Bounds> bounds)
        {
            string[] fallbackNames =
            {
                "Dial_Pressure",
                "Switch_Heavy",
                "Indicator_Light",
                "Gauge_Oxygen",
                "Panel_Service",
                "Valve_Rotary"
            };

            for (int i = 0; i < fallbackNames.Length; i++)
            {
                string name = fallbackNames[i];
                bool movable = name.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("Dial", StringComparison.OrdinalIgnoreCase);
                Bounds b = new Bounds(Vector3.zero, new Vector3(0.22f + i * 0.025f, 0.12f, 0.08f));
                int vertexStart = vertices.Count;
                int triangleStart = triangles.Count;
                uint typeHash = ResolveInstrumentTypeHash(name);
                uint instrumentHash = HashString(name);
                AppendBox(b, instrumentHash, vertexStart, vertices, triangles);

                InstrumentRuleDTO1608 rule = default;
                rule.InstrumentHash = instrumentHash;
                rule.TypeHash = typeHash;
                rule.TextureHash = HashString(name + "_GeneratedTexture");
                rule.Flags = InteriorFinisherConstants1608.InstrumentStaticBaseFlag | (movable ? InteriorFinisherConstants1608.InstrumentMovableFlag : 0u);
                rule.BoundsExtents = new float3(b.extents.x, b.extents.y, b.extents.z);
                rule.MinSocketRadius = 0.035f + i * 0.006f;
                rule.Weight = 1f + i * 0.18f;
                rule.StaticVertexStart = (uint)vertexStart;
                rule.StaticVertexCount = (uint)(vertices.Count - vertexStart);
                rule.StaticIndexStart = (uint)triangleStart;
                rule.StaticIndexCount = (uint)((triangles.Count - triangleStart) * 3);
                rule.MovingVertexStart = 0u;
                rule.MovingVertexCount = 0u;
                rule.AtlasSourceIndex = (ushort)i;
                rule.Interactivity = movable ? (ushort)1 : (ushort)0;
                rule.UvMax = new float2(1f, 1f);
                rules.Add(rule);
                names.Add(name);
                paths.Add("FALLBACK_SCHEMA");
                texturePaths.Add(string.Empty);
                bounds.Add(b);
            }
        }

        private static void AppendBox(Bounds bounds, uint instrumentHash, int ruleVertexStart, List<InteriorMeshVertexDTO1608> vertices, List<InteriorTriangleDTO1608> triangles)
        {
            int localBaseIndex = vertices.Count - ruleVertexStart;
            Vector3 e = bounds.extents;
            e.x = Mathf.Max(e.x, 0.025f);
            e.y = Mathf.Max(e.y, 0.025f);
            e.z = Mathf.Max(e.z, 0.0125f);
            Vector3 c = bounds.center;
            Vector3[] p =
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(-e.x, e.y, -e.z),
                c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, e.z),
                c + new Vector3(-e.x, e.y, e.z)
            };

            for (int i = 0; i < p.Length; i++)
            {
                float3 pos = new float3(p[i].x, p[i].y, p[i].z);
                vertices.Add(new InteriorMeshVertexDTO1608
                {
                    Position = pos,
                    Normal = math.normalizesafe(pos - new float3(c.x, c.y, c.z), new float3(0f, 0f, 1f)),
                    Tangent = new float4(1f, 0f, 0f, 1f),
                    Uv0 = new float2((i & 1) == 0 ? 0f : 1f, (i & 2) == 0 ? 0f : 1f),
                    ColorRgba = InteriorFinisherMath1608.EncodeColor(140, 132, 112, 255),
                    Flags = 1u,
                    InstrumentHash = instrumentHash
                });
            }

            AppendQuad(triangles, localBaseIndex + 0, localBaseIndex + 1, localBaseIndex + 2, localBaseIndex + 3);
            AppendQuad(triangles, localBaseIndex + 5, localBaseIndex + 4, localBaseIndex + 7, localBaseIndex + 6);
            AppendQuad(triangles, localBaseIndex + 4, localBaseIndex + 0, localBaseIndex + 3, localBaseIndex + 7);
            AppendQuad(triangles, localBaseIndex + 1, localBaseIndex + 5, localBaseIndex + 6, localBaseIndex + 2);
            AppendQuad(triangles, localBaseIndex + 3, localBaseIndex + 2, localBaseIndex + 6, localBaseIndex + 7);
            AppendQuad(triangles, localBaseIndex + 4, localBaseIndex + 5, localBaseIndex + 1, localBaseIndex + 0);
        }

        private static void AppendQuad(List<InteriorTriangleDTO1608> triangles, int a, int b, int c, int d)
        {
            uint hash = InteriorFinisherMath1608.Hash((uint)(triangles.Count + 1));
            triangles.Add(new InteriorTriangleDTO1608 { Index0 = a, Index1 = b, Index2 = c, SourceHash = hash, Flags = 1 });
            triangles.Add(new InteriorTriangleDTO1608 { Index0 = a, Index1 = c, Index2 = d, SourceHash = InteriorFinisherMath1608.Hash(hash), Flags = 1 });
        }

        private static Bounds ResolveLocalBounds(GameObject prefab)
        {
            s_rendererScratch.Clear();
            prefab.GetComponentsInChildren(true, s_rendererScratch);
            try
            {
                Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
                bool has = false;
                Bounds result = default;
                for (int i = 0; i < s_rendererScratch.Count; i++)
                {
                    Renderer renderer = s_rendererScratch[i];
                    if (renderer == null)
                        continue;

                    Bounds b = TransformBounds(rootInverse, renderer.bounds);
                    if (!IsFiniteBounds(b))
                        continue;

                    if (!has)
                    {
                        result = b;
                        has = true;
                    }
                    else
                    {
                        result.Encapsulate(b);
                    }
                }

                return has ? result : DefaultInstrumentBounds();
            }
            finally
            {
                s_rendererScratch.Clear();
            }
        }

        private static Bounds DefaultInstrumentBounds()
        {
            return new Bounds(Vector3.zero, new Vector3(0.2f, 0.12f, 0.08f));
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(c + new Vector3(e.x * x, e.y * y, e.z * z)));
            return result;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return math.isfinite(center.x) &&
                   math.isfinite(center.y) &&
                   math.isfinite(center.z) &&
                   math.isfinite(extents.x) &&
                   math.isfinite(extents.y) &&
                   math.isfinite(extents.z) &&
                   extents.x >= 0f &&
                   extents.y >= 0f &&
                   extents.z >= 0f;
        }

        private static string ResolvePrimaryTexturePath(GameObject prefab)
        {
            s_rendererScratch.Clear();
            prefab.GetComponentsInChildren(true, s_rendererScratch);
            try
            {
                for (int i = 0; i < s_rendererScratch.Count; i++)
                {
                    Renderer renderer = s_rendererScratch[i];
                    Material material = renderer != null ? renderer.sharedMaterial : null;
                    if (material == null)
                        continue;

                    if (TryResolvePrimaryTexturePath(material, out string path))
                        return path;
                }

                return string.Empty;
            }
            finally
            {
                s_rendererScratch.Clear();
            }
        }

        private static bool TryResolvePrimaryTexturePath(Material material, out string path)
        {
            path = string.Empty;
            for (int i = 0; i < s_primaryTextureProperties.Length; i++)
            {
                string propertyName = s_primaryTextureProperties[i];
                if (!material.HasProperty(propertyName))
                    continue;

                Texture texture = material.GetTexture(propertyName);
                if (TryResolveTextureAssetPath(texture, out path))
                    return true;
            }

            return TryResolveTextureAssetPath(material.mainTexture, out path);
        }

        private static bool TryResolveTextureAssetPath(Texture texture, out string path)
        {
            path = string.Empty;
            if (texture == null)
                return false;

            path = AssetDatabase.GetAssetPath(texture);
            return !string.IsNullOrEmpty(path);
        }

        private static void ResolveCapacityStats(InteriorInstrumentLibrary1608 library)
        {
            for (int i = 0; i < library.Rules.Length; i++)
            {
                InstrumentRuleDTO1608 rule = library.Rules[i];
                library.MaxStaticVertices = Math.Max(library.MaxStaticVertices, (int)rule.StaticVertexCount);
                library.MaxStaticIndices = Math.Max(library.MaxStaticIndices, (int)rule.StaticIndexCount);
                if ((rule.Flags & InteriorFinisherConstants1608.InstrumentMovableFlag) != 0u)
                    library.MovableRuleCount++;
            }
        }

        private static NativeArray<T> ToNative<T>(List<T> values, Allocator allocator) where T : unmanaged
        {
            var native = new NativeArray<T>(values.Count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < values.Count; i++)
                native[i] = values[i];
            return native;
        }

        public static uint HashString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 2166136261u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                if (c == ' ' || c == '\t')
                    continue;
                hash ^= c;
                hash *= 16777619u;
            }

            return InteriorFinisherMath1608.Hash(hash);
        }

        public static uint ResolveInstrumentTypeHash(string name)
        {
            if (name.Contains("Conduit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Floor", StringComparison.OrdinalIgnoreCase))
                return HashString("Socket_Floor_Conduit");
            if (name.Contains("Cable", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Ceiling", StringComparison.OrdinalIgnoreCase))
                return HashString("Socket_Ceiling_Cable");
            if (name.Contains("Panel", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Lever", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Dial", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Gauge", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Indicator", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Meter", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Screen", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
                return HashString("Socket_Wall_Panel");
            return TypeAny;
        }

        private static bool IsMovableInstrument(string name)
        {
            return name.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Lever", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Dial", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Button", StringComparison.OrdinalIgnoreCase);
        }

        private static float ResolveWeight(string name, bool movable)
        {
            float weight = movable ? 1.2f : 1f;
            if (name.Contains("Panel", StringComparison.OrdinalIgnoreCase))
                weight += 0.35f;
            if (name.Contains("Indicator", StringComparison.OrdinalIgnoreCase))
                weight += 0.2f;
            return weight;
        }

        private static void EnsureAssetFolder(string folder)
        {
            string safe = folder.Trim().Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(safe))
                return;

            string[] segments = safe.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }

    public static class InteriorSocketParser1608
    {
        // COLD ALLOC: List<Renderer>[64] - editor-only module bounds scan scratch - owner: InteriorSocketParser1608
        private static readonly List<Renderer> s_rendererScratch = new List<Renderer>(64);
        // COLD ALLOC: List<Transform>[128] - editor-only socket marker scan scratch - owner: InteriorSocketParser1608
        private static readonly List<Transform> s_transformScratch = new List<Transform>(128);

        /// <summary>
        /// Collects authored decorative sockets from the module prefab.
        /// Returns true when at least one authored Socket_* / DecorativeSocket marker was
        /// parsed, false when the bounding-box fallback grid was substituted instead. The
        /// return value is the only signal that separates an authored interior from an
        /// AABB guess - the socket list is non-empty either way.
        /// </summary>
        public static bool CollectSockets(GameObject prefab, List<InteriorSocketDTO1608> sockets, List<InteriorSocketDTO1608> microSockets)
        {
            sockets.Clear();
            microSockets.Clear();
            if (prefab == null)
                return false;

            s_transformScratch.Clear();
            prefab.GetComponentsInChildren(true, s_transformScratch);
            try
            {
                Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
                for (int i = 0; i < s_transformScratch.Count; i++)
                {
                    Transform tr = s_transformScratch[i];
                    if (tr == null || tr == prefab.transform)
                        continue;

                    string name = tr.name;
                    if (!IsSocketName(name))
                        continue;

                    InteriorSocketDTO1608 socket = BuildSocket(prefab, tr, rootInverse, i);
                    if (socket.SocketKind == InteriorSocketKind1608.MicroStamp)
                        microSockets.Add(socket);
                    else
                        sockets.Add(socket);
                }
            }
            finally
            {
                s_transformScratch.Clear();
            }

            if (sockets.Count > 0)
                return true;

            AppendFallbackSockets(prefab, sockets, microSockets);
            return false;
        }

        /// <summary>
        /// Interior surface tokens. Deliberately the SAME vocabulary <see cref="ClassifyKind"/> switches
        /// on, so any name that qualifies as an interior socket here is guaranteed to classify into a
        /// real kind there - one list, not two that can drift apart.
        /// </summary>
        private static readonly string[] s_InteriorSurfaceTokens =
        {
            "Wall", "Panel", "Ceiling", "Cable", "Floor", "Conduit", "Rivet", "Seam", "Micro"
        };

        /// <summary>
        /// True when a child transform name denotes an INTERIOR decorative socket.
        /// <para>
        /// THE DEFECT THIS FIXES WAS LIVE AND HAD NOTHING TO DO WITH INTERIORS. The previous body was
        /// <c>Contains("DecorativeSocket") || StartsWith("Socket_") || Contains("Socket_")</c> - the
        /// third clause making the second redundant - so it matched ANY child whose name contains
        /// "Socket_". This project already ships CONSTRUCTION sockets under exactly that spelling:
        /// PFB_Module_Foundation carries Socket_PosZ / Socket_NegX / Socket_NegZ / Socket_PosX
        /// (authored by ConstructionBootstrapAuthoring.cs:91-94, plus Socket_Front / Socket_Back at
        /// :105-106), and DronePrefabFactory emits Socket_Tool / Socket_Sensor / Socket_StatusLight
        /// (:1007-1021). Those are module-to-module connection and attachment topology, not decoration.
        /// Under the old predicate the Foundation's four construction sockets read as four interior
        /// WallPanel anchors - which also made CollectSockets return true, so
        /// <see cref="FailClosedOnFallbackKit"/> saw an "authored" interior nobody authored and the
        /// provenance gate passed on a lie. Socket_StatusLight was the worst case: "Light" is in the
        /// panel list at <see cref="ResolveInstrumentTypeHash"/>, so a drone status light read as a wall
        /// instrument mount.
        /// </para>
        /// <para>
        /// THE DISCRIMINATOR, NOT THE SYMPTOM. "DecorativeSocket" is unambiguous and its behaviour is
        /// unchanged. The bare "Socket_" form is ambiguous by construction, so it must now ALSO name an
        /// interior surface feature. That is the question this predicate was always trying to ask.
        /// </para>
        /// <para>
        /// NO ASSET REGRESSES. Censused 2026-07-29: zero children named DecorativeSocket* or Socket_*
        /// exist across all six H8_A1712_* module prefabs - their children are COL_*Proxy, VIS_LOD1,
        /// VIS_LOD2 and InteriorTrigger - and the only Socket_* children anywhere in the project are the
        /// construction and drone-attachment sockets listed above, every one of which this predicate
        /// SHOULD reject. The AABB fallback grid builds its DTOs in code and never passes through here.
        /// </para>
        /// </summary>
        private static bool IsSocketName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (name.Contains("DecorativeSocket", StringComparison.OrdinalIgnoreCase))
                return true;

            if (name.IndexOf("Socket_", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            for (int i = 0; i < s_InteriorSurfaceTokens.Length; i++)
            {
                if (name.IndexOf(s_InteriorSurfaceTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static InteriorSocketDTO1608 BuildSocket(GameObject root, Transform tr, Matrix4x4 rootInverse, int ordinal)
        {
            Vector3 localPosition = rootInverse.MultiplyPoint3x4(tr.position);
            Quaternion localRotation = Quaternion.Inverse(root.transform.rotation) * tr.rotation;
            byte kind = ClassifyKind(tr.name);
            uint tag = InteriorInstrumentLibraryBuilder1608.HashString(
                kind == InteriorSocketKind1608.CeilingCable ? "Socket_Ceiling_Cable" :
                kind == InteriorSocketKind1608.FloorConduit ? "Socket_Floor_Conduit" :
                "Socket_Wall_Panel");
            InteriorSocketDTO1608 socket = default;
            socket.LocalPosition = new float3(localPosition.x, localPosition.y, localPosition.z);
            socket.Radius = ResolveSocketRadius(rootInverse * tr.localToWorldMatrix, kind);
            socket.LocalRotation = math.normalize(new quaternion(localRotation.x, localRotation.y, localRotation.z, localRotation.w));
            socket.LocalNormal = math.rotate(socket.LocalRotation, new float3(0f, 0f, 1f));
            socket.SurfaceArea = socket.Radius * socket.Radius;
            socket.StableHash = InteriorFinisherMath1608.Hash(InteriorInstrumentLibraryBuilder1608.HashString(root.name) ^ InteriorInstrumentLibraryBuilder1608.HashString(tr.name) ^ (uint)ordinal);
            socket.TagHash = tag;
            socket.AllowedInstrumentMask = tag;
            socket.SocketKind = kind;
            socket.DensityHint = ResolveDensityHint(tr.name, kind);
            socket.Flags = 1u;
            socket.PairIndex = -1;
            return socket;
        }

        /// <summary>
        /// Socket kind from the marker name.
        /// <para>
        /// ORDERING TRAP - THE MICRO TEST RUNS FIRST AND WINS. "Rivet" / "Seam" / "Micro" are checked
        /// before every other token, so a name that carries one of them AND a surface token classifies
        /// as MicroStamp regardless of the surface. A marker named "..._Ceiling_Cable_Micro" is a
        /// MicroStamp, NOT a CeilingCable. That matters well beyond kind selection:
        /// <see cref="InteriorSocketParser1608.CollectSockets"/> routes MicroStamp markers into the
        /// separate <c>microSockets</c> list and returns <c>true</c> only when the NON-micro
        /// <c>sockets</c> list is non-empty - so a module whose every marker happens to contain "Micro"
        /// still counts as having zero authored sockets, still gets the AABB fallback grid, and still
        /// trips <see cref="FailClosedOnFallbackKit"/>. Do not put those three words in a marker name
        /// unless a micro stamp is what you mean.
        /// </para>
        /// </summary>
        private static byte ClassifyKind(string name)
        {
            if (name.Contains("Rivet", StringComparison.OrdinalIgnoreCase) || name.Contains("Seam", StringComparison.OrdinalIgnoreCase) || name.Contains("Micro", StringComparison.OrdinalIgnoreCase))
                return InteriorSocketKind1608.MicroStamp;
            if (name.Contains("Floor", StringComparison.OrdinalIgnoreCase) || name.Contains("Conduit", StringComparison.OrdinalIgnoreCase))
                return InteriorSocketKind1608.FloorConduit;
            if (name.Contains("Cable", StringComparison.OrdinalIgnoreCase) || name.Contains("Ceiling", StringComparison.OrdinalIgnoreCase))
                return InteriorSocketKind1608.CeilingCable;
            return InteriorSocketKind1608.WallPanel;
        }

        private static float ResolveSocketRadius(Matrix4x4 localToRoot, byte kind)
        {
            float baseRadius = kind == InteriorSocketKind1608.MicroStamp ? 0.018f : 0.18f;
            float markerScale = Mathf.Max(
                ResolveAxisScale(localToRoot.GetColumn(0)),
                Mathf.Max(ResolveAxisScale(localToRoot.GetColumn(1)), ResolveAxisScale(localToRoot.GetColumn(2))));
            if (float.IsNaN(markerScale) || float.IsInfinity(markerScale) || markerScale <= 0.0001f)
                return baseRadius;

            float radius = baseRadius * markerScale;
            return kind == InteriorSocketKind1608.MicroStamp
                ? Mathf.Clamp(radius, 0.004f, 0.05f)
                : Mathf.Clamp(radius, 0.05f, 0.45f);
        }

        private static float ResolveAxisScale(Vector4 axis)
        {
            return Mathf.Sqrt(axis.x * axis.x + axis.y * axis.y + axis.z * axis.z);
        }

        private static byte ResolveDensityHint(string name, byte kind)
        {
            if (name.Contains("NoAuto", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Disabled", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (name.Contains("Sparse", StringComparison.OrdinalIgnoreCase))
                return 96;
            if (name.Contains("MediumDensity", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MidDensity", StringComparison.OrdinalIgnoreCase))
                return 180;
            if (name.Contains("Dense", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Hero", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("HighDensity", StringComparison.OrdinalIgnoreCase))
                return 255;
            if (kind == InteriorSocketKind1608.FloorConduit)
                return 192;
            if (kind == InteriorSocketKind1608.CeilingCable)
                return 224;
            return 255;
        }

        private static void AppendFallbackSockets(GameObject prefab, List<InteriorSocketDTO1608> sockets, List<InteriorSocketDTO1608> microSockets)
        {
            Bounds bounds = ResolveBounds(prefab);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 center = bounds.center;
            uint wallTag = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Wall_Panel");
            uint cableTag = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Ceiling_Cable");
            uint floorTag = InteriorInstrumentLibraryBuilder1608.HashString("Socket_Floor_Conduit");

            for (int wall = 0; wall < 4; wall++)
            {
                for (int i = 0; i < 6; i++)
                {
                    float u = (i + 1f) / 7f;
                    float y = Mathf.Lerp(min.y + 0.45f, max.y - 0.45f, (i & 1) == 0 ? 0.35f : 0.68f);
                    Vector3 p;
                    Quaternion r;
                    if (wall == 0)
                    {
                        p = new Vector3(Mathf.Lerp(min.x, max.x, u), y, max.z);
                        r = Quaternion.LookRotation(Vector3.back, Vector3.up);
                    }
                    else if (wall == 1)
                    {
                        p = new Vector3(Mathf.Lerp(min.x, max.x, u), y, min.z);
                        r = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    }
                    else if (wall == 2)
                    {
                        p = new Vector3(max.x, y, Mathf.Lerp(min.z, max.z, u));
                        r = Quaternion.LookRotation(Vector3.left, Vector3.up);
                    }
                    else
                    {
                        p = new Vector3(min.x, y, Mathf.Lerp(min.z, max.z, u));
                        r = Quaternion.LookRotation(Vector3.right, Vector3.up);
                    }

                    sockets.Add(BuildFallbackSocket(prefab.name, p, r, wallTag, InteriorSocketKind1608.WallPanel, sockets.Count));
                }
            }

            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(min.x + 0.25f, max.x - 0.25f, (i + 0.5f) / 8f);
                Vector3 p = new Vector3(x, max.y - 0.12f, Mathf.Lerp(min.z, max.z, (i & 1) == 0 ? 0.28f : 0.72f));
                sockets.Add(BuildFallbackSocket(prefab.name, p, Quaternion.LookRotation(Vector3.down, Vector3.forward), cableTag, InteriorSocketKind1608.CeilingCable, sockets.Count));
            }

            for (int i = 0; i < 4; i++)
            {
                float z = Mathf.Lerp(min.z + 0.35f, max.z - 0.35f, (i + 0.5f) / 4f);
                float x = (i & 1) == 0 ? min.x + 0.18f : max.x - 0.18f;
                Vector3 p = new Vector3(x, min.y + 0.08f, z);
                Quaternion r = Quaternion.LookRotation((i & 1) == 0 ? Vector3.right : Vector3.left, Vector3.up);
                sockets.Add(BuildFallbackSocket(prefab.name, p, r, floorTag, InteriorSocketKind1608.FloorConduit, sockets.Count));
            }

            for (int i = 0; i < 96; i++)
            {
                float t = (i + 0.5f) / 96f;
                Vector3 p = new Vector3(Mathf.Lerp(min.x, max.x, t), Mathf.Lerp(min.y, max.y, (i % 11) / 10f), center.z + ((i & 1) == 0 ? bounds.extents.z : -bounds.extents.z));
                microSockets.Add(BuildFallbackSocket(prefab.name, p, Quaternion.identity, wallTag, InteriorSocketKind1608.MicroStamp, i));
            }
        }

        private static InteriorSocketDTO1608 BuildFallbackSocket(string rootName, Vector3 p, Quaternion r, uint tag, byte kind, int ordinal)
        {
            InteriorSocketDTO1608 socket = default;
            socket.LocalPosition = new float3(p.x, p.y, p.z);
            socket.Radius = kind == InteriorSocketKind1608.MicroStamp ? 0.018f : 0.2f;
            socket.LocalRotation = math.normalize(new quaternion(r.x, r.y, r.z, r.w));
            socket.LocalNormal = math.rotate(socket.LocalRotation, new float3(0f, 0f, 1f));
            socket.SurfaceArea = socket.Radius * socket.Radius;
            socket.StableHash = InteriorFinisherMath1608.Hash(
                InteriorInstrumentLibraryBuilder1608.HashString(rootName) ^
                tag ^
                ((uint)kind << 24) ^
                (uint)(ordinal * 977));
            socket.TagHash = tag;
            socket.AllowedInstrumentMask = tag;
            socket.SocketKind = kind;
            socket.DensityHint = ResolveDensityHint(string.Empty, kind);
            socket.Flags = 1u;
            socket.PairIndex = -1;
            return socket;
        }

        private static Bounds ResolveBounds(GameObject prefab)
        {
            s_rendererScratch.Clear();
            prefab.GetComponentsInChildren(true, s_rendererScratch);
            try
            {
                Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
                bool has = false;
                Bounds bounds = default;
                for (int i = 0; i < s_rendererScratch.Count; i++)
                {
                    Renderer renderer = s_rendererScratch[i];
                    if (renderer == null)
                        continue;

                    Bounds local = TransformBounds(rootInverse, renderer.bounds);
                    if (!IsFiniteBounds(local))
                        continue;

                    if (!has)
                    {
                        bounds = local;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }

                return has ? bounds : new Bounds(Vector3.zero, new Vector3(4f, 3f, 6f));
            }
            finally
            {
                s_rendererScratch.Clear();
            }
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(c), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(c + new Vector3(e.x * x, e.y * y, e.z * z)));
            return result;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return math.isfinite(center.x) &&
                   math.isfinite(center.y) &&
                   math.isfinite(center.z) &&
                   math.isfinite(extents.x) &&
                   math.isfinite(extents.y) &&
                   math.isfinite(extents.z) &&
                   extents.x >= 0f &&
                   extents.y >= 0f &&
                   extents.z >= 0f;
        }
    }

    public static class InteriorFinisherPipeline1608
    {
        // COLD ALLOC: List<Transform>[256] - editor-only hierarchy count scratch - owner: InteriorFinisherPipeline1608
        private static readonly List<Transform> s_transformScratch = new List<Transform>(256);

        private static readonly VertexAttributeDescriptor[] s_vertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1)
        };

        public static bool FinishInterior(InteriorFinisherSettings1608 settings, out InteriorFinisherResult1608 result)
        {
            result = default;
            settings = Sanitize(settings);
            if (settings.ModulePrefab == null)
            {
                result.FailureReason = "Interior Finisher requires a generated module prefab.";
                return false;
            }

            InteriorInstrumentLibrary1608 library = null;
            NativeArray<InteriorSocketDTO1608> sockets = default;
            NativeArray<InteriorSocketDTO1608> microSockets = default;
            NativeArray<InstrumentPlacementDTO1608> placements = default;
            NativeArray<InteriorBakeCountersDTO1608> counters = default;
            NativeList<InteriorMeshVertexDTO1608> fusedVertices = default;
            NativeList<int> fusedIndices = default;
            string atlasPath = string.Empty;
            float atlasEfficiency = 0f;
            uint atlasAreaUsed = 0u;
            uint atlasAreaTotal = 0u;
            uint textureCount = 0u;
            float atlasMilliseconds = 0f;

            try
            {
                EnsureAssetFolder(settings.OutputFolder);
                library = InteriorInstrumentLibraryBuilder1608.Build(settings.InstrumentPrefabFolder, Allocator.TempJob);

                Stopwatch atlasWatch = Stopwatch.StartNew();
                atlasPath = InteriorAtlasPacker1608.PackInstrumentAtlas(
                    library,
                    settings.OutputFolder,
                    settings.OutputName,
                    settings.TextureSize,
                    out atlasEfficiency,
                    out atlasAreaUsed,
                    out atlasAreaTotal,
                    out textureCount);
                atlasWatch.Stop();
                atlasMilliseconds = (float)atlasWatch.Elapsed.TotalMilliseconds;

                var socketList = new List<InteriorSocketDTO1608>(128);
                var microSocketList = new List<InteriorSocketDTO1608>(256);
                bool authoredSockets = InteriorSocketParser1608.CollectSockets(settings.ModulePrefab, socketList, microSocketList);
                if (socketList.Count == 0)
                    throw new InvalidOperationException("Interior Finisher found zero decorative sockets.");

                result.UsedFallbackInstrumentKit = library.UsedFallbackKit;
                result.UsedFallbackSocketLayout = !authoredSockets;

                sockets = ToNative(socketList, Allocator.TempJob);
                microSockets = ToNative(microSocketList, Allocator.TempJob);
                placements = new NativeArray<InstrumentPlacementDTO1608>(socketList.Count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<InteriorBakeCountersDTO1608>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                InteriorBakeCountersDTO1608 provenance = counters[0];
                if (library.UsedFallbackKit)
                    provenance.FaultFlags |= InteriorFinisherConstants1608.FaultFallbackInstrumentKit;
                if (!authoredSockets)
                    provenance.FaultFlags |= InteriorFinisherConstants1608.FaultFallbackSocketLayout;
                counters[0] = provenance;
                FailClosedOnFallbackKit(settings, library, authoredSockets);

                Stopwatch watch = Stopwatch.StartNew();
                new PopulateSocketsJob1608
                {
                    Sockets = sockets,
                    Rules = library.Rules,
                    Placements = placements,
                    Counters = counters,
                    Seed = settings.Seed,
                    GlobalQualityWeight = settings.GlobalQualityWeight,
                    DensityWeight = settings.DensityWeight
                }.Run();
                watch.Stop();
                InteriorBakeCountersDTO1608 counterValue = counters[0];
                counterValue.PlacementMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counterValue.MicroDetailStampCount = (uint)microSocketList.Count;
                counters[0] = counterValue;
                FailClosedIfRequired(counters[0], "placement");

                int placementCount = (int)counters[0].PlacementCount;
                int fusedVertexCapacity = Math.Max(1, placementCount * Math.Max(1, library.MaxStaticVertices));
                int fusedIndexCapacity = Math.Max(3, placementCount * Math.Max(3, library.MaxStaticIndices));
                fusedVertices = new NativeList<InteriorMeshVertexDTO1608>(fusedVertexCapacity, Allocator.TempJob);
                fusedIndices = new NativeList<int>(fusedIndexCapacity, Allocator.TempJob);

                watch.Restart();
                new WeldInstrumentBasesJob1608
                {
                    Placements = placements,
                    Rules = library.Rules,
                    SourceVertices = library.Vertices,
                    SourceTriangles = library.Triangles,
                    FusedVertices = fusedVertices,
                    FusedIndices = fusedIndices,
                    Counters = counters
                }.Run();
                watch.Stop();
                counterValue = counters[0];
                counterValue.FusionMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;
                FailClosedIfRequired(counters[0], "static base fusion");

                watch.Restart();
                if (fusedVertices.Length > 0)
                {
                    new BakeGrimeVertexColorJob1608
                    {
                        Vertices = fusedVertices.AsArray(),
                        GlobalQualityWeight = settings.GlobalQualityWeight,
                        Seed = settings.Seed
                    }.Run(fusedVertices.Length);
                }
                watch.Stop();
                counterValue = counters[0];
                counterValue.GrimeMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;

                string meshPath;
                Mesh mesh = CreateMeshAsset(settings, fusedVertices.AsArray(), fusedIndices.AsArray(), counters[0], out meshPath);
                Mesh cableMesh = CreateCableBundleMeshAsset(settings, socketList, out string cableMeshPath);

                watch.Restart();
                var bakeArgs = new BakeStampedTextureArgs1608
                {
                    Settings = settings,
                    MicroSockets = microSockets,
                    Placements = placements,
                    Counters = counters
                };
                BakeStampedTextureAssets(bakeArgs, out string normalPath, out string grimePath);
                watch.Stop();
                counterValue = counters[0];
                counterValue.NormalStampMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counterValue.AtlasMilliseconds = atlasMilliseconds;
                counterValue.AtlasAreaUsed = atlasAreaUsed;
                counterValue.AtlasAreaTotal = atlasAreaTotal;
                counterValue.TextureCount = textureCount;
                counters[0] = counterValue;

                Material material = CreateOrUpdateMaterial(settings.OutputFolder, settings.OutputName, atlasPath, normalPath, grimePath);
                Material cableMaterial = cableMesh != null ? CreateOrUpdateCableMaterial(settings.OutputFolder) : null;
                Material handleMaterial = counters[0].MovingPartCount > 0u ? CreateOrUpdateHandleMaterial(settings.OutputFolder) : null;
                string prefabPath = CreatePrefabAsset(settings, mesh, cableMesh, material, cableMaterial, handleMaterial, placements, counters[0]);
                counterValue = counters[0];
                counterValue.HierarchyBefore = CountTransforms(settings.ModulePrefab);
                counterValue.HierarchyAfter = 1f + counterValue.MovingPartCount + (cableMesh != null ? 1f : 0f);
                counterValue.PolygonsSaved = counterValue.MicroDetailStampCount * 12u;
                counters[0] = counterValue;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // PopulateSocketsJob1608.Execute clears FaultFlags on entry, so the provenance
                // bits set before the jobs do not survive into the counters the caller reads.
                // Re-apply them here so a diagnostic bake carries its own rejection reason.
                counterValue = counters[0];
                if (result.UsedFallbackInstrumentKit)
                    counterValue.FaultFlags |= InteriorFinisherConstants1608.FaultFallbackInstrumentKit;
                if (result.UsedFallbackSocketLayout)
                    counterValue.FaultFlags |= InteriorFinisherConstants1608.FaultFallbackSocketLayout;
                counters[0] = counterValue;

                result.Success = true;
                result.PrefabPath = prefabPath;
                result.MeshPath = meshPath;
                result.CableMeshPath = cableMeshPath;
                result.AtlasPath = atlasPath;
                result.NormalPath = normalPath;
                result.GrimePath = grimePath;
                result.Counters = counters[0];
                result.SocketCount = socketList.Count;
                result.MicroSocketCount = microSocketList.Count;
                result.AtlasEfficiency01 = atlasEfficiency;
                return true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailureReason = ex.Message;
                return false;
            }
            finally
            {
                if (fusedIndices.IsCreated)
                    fusedIndices.Dispose();
                if (fusedVertices.IsCreated)
                    fusedVertices.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
                if (microSockets.IsCreated)
                    microSockets.Dispose();
                if (sockets.IsCreated)
                    sockets.Dispose();
                library?.Dispose();
            }
        }

        public static Mesh GenerateCableBundles(string meshName, Vector3 start, Vector3 end, int strands, int segments, float radius, float slack, uint seed, bool uploadMeshData = true)
        {
            int safeStrands = Mathf.Clamp(strands, 1, 12);
            int safeSegments = Mathf.Clamp(segments, 4, 64);
            int ring = 6;
            int vertexCount = safeStrands * (safeSegments + 1) * ring;
            int indexCount = safeStrands * safeSegments * ring * 6;
            var vertices = new NativeArray<InteriorMeshVertexDTO1608>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var indices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<InteriorRenderVertexDTO1608> renderVertices = default;

            try
            {
                Vector3 axis = end - start;
                Vector3 tangent = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(tangent, right).normalized;
                int vWrite = 0;
                int iWrite = 0;
                for (int s = 0; s < safeStrands; s++)
                {
                    float phase = (s + 1) * 0.6180339f;
                    float strandRadius = radius * Mathf.Lerp(0.75f, 1.25f, InteriorFinisherMath1608.Hash01(seed ^ (uint)s));
                    for (int p = 0; p <= safeSegments; p++)
                    {
                        float t = p / (float)safeSegments;
                        Vector3 center = Vector3.Lerp(start, end, t);
                        center.y += InteriorFinisherMath1608.CatenaryApproxY(t, slack);
                        center += right * (InteriorFinisherMath1608.FastSignedTriangle(t * 2.7f + phase) * radius * 1.8f);
                        center += up * (InteriorFinisherMath1608.FastSignedTriangle(t * 3.1f - phase) * radius * 1.2f);
                        for (int r = 0; r < ring; r++)
                        {
                            float a = (r / (float)ring) * Mathf.PI * 2f;
                            Vector3 n = (Mathf.Cos(a) * right + Mathf.Sin(a) * up).normalized;
                            Vector3 pos = center + n * strandRadius;
                            vertices[vWrite++] = new InteriorMeshVertexDTO1608
                            {
                                Position = new float3(pos.x, pos.y, pos.z),
                                Normal = new float3(n.x, n.y, n.z),
                                Tangent = new float4(tangent.x, tangent.y, tangent.z, 1f),
                                Uv0 = new float2(t, r / (float)ring),
                                ColorRgba = InteriorFinisherMath1608.EncodeColor(42, 39, 34, 255),
                                Flags = 1u
                            };
                        }
                    }

                    int strandBase = s * (safeSegments + 1) * ring;
                    for (int p = 0; p < safeSegments; p++)
                    {
                        int row = strandBase + p * ring;
                        int next = row + ring;
                        for (int r = 0; r < ring; r++)
                        {
                            int rNext = (r + 1) % ring;
                            indices[iWrite++] = row + r;
                            indices[iWrite++] = next + r;
                            indices[iWrite++] = next + rNext;
                            indices[iWrite++] = row + r;
                            indices[iWrite++] = next + rNext;
                            indices[iWrite++] = row + rNext;
                        }
                    }
                }

                renderVertices = new NativeArray<InteriorRenderVertexDTO1608>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                Bounds bounds = PackRenderVertices(vertices, renderVertices, vertexCount);
                Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
                mesh.SetVertexBufferParams(vertexCount, s_vertexLayout);
                mesh.SetIndexBufferParams(indexCount, mesh.indexFormat);
                mesh.SetVertexBufferData(renderVertices, 0, 0, vertexCount, 0, flags);
                mesh.SetIndexBufferData(indices, 0, 0, indexCount, flags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, flags);
                mesh.bounds = bounds;
                mesh.UploadMeshData(uploadMeshData);
                return mesh;
            }
            finally
            {
                if (renderVertices.IsCreated)
                    renderVertices.Dispose();
                indices.Dispose();
                vertices.Dispose();
            }
        }

        private static Mesh CreateCableBundleMeshAsset(InteriorFinisherSettings1608 settings, List<InteriorSocketDTO1608> sockets, out string cableMeshPath)
        {
            cableMeshPath = string.Empty;
            int ceilingCount = 0;
            int floorCount = 0;
            for (int i = 0; i < sockets.Count; i++)
            {
                InteriorSocketDTO1608 socket = sockets[i];
                if (!CableSocketPassesDensity(socket, settings.DensityWeight, settings.Seed))
                    continue;

                byte kind = socket.SocketKind;
                if (kind == InteriorSocketKind1608.CeilingCable)
                    ceilingCount++;
                else if (kind == InteriorSocketKind1608.FloorConduit)
                    floorCount++;
            }

            int cableSocketCount = ceilingCount + floorCount;
            if (cableSocketCount < 2)
                return null;

            float q = InteriorFinisherMath1608.Smooth01(settings.GlobalQualityWeight);
            bool hasFloorRoute = ceilingCount > 0 && floorCount > 0;
            int candidatePairs = hasFloorRoute ? Math.Max(ceilingCount, floorCount) : cableSocketCount >> 1;
            int pairBudget = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 12f, q)), 1, 12);
            int pairCount = Math.Min(candidatePairs, pairBudget);
            int strands = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 7f, q)), 2, 8);
            int segments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(6f, 28f, q)), 6, 32);
            float radius = Mathf.Lerp(0.011f, 0.024f, q);
            float slack = Mathf.Lerp(0.08f, 0.38f, q);
            if (pairCount <= 0)
                return null;

            // COLD ALLOC: InteriorSocketDTO[ceiling/floor counts] - editor-only cable route scratch - owner: InteriorFinisherPipeline1608
            var ceilingSockets = ceilingCount > 0 ? new InteriorSocketDTO1608[ceilingCount] : Array.Empty<InteriorSocketDTO1608>();
            var floorSockets = floorCount > 0 ? new InteriorSocketDTO1608[floorCount] : Array.Empty<InteriorSocketDTO1608>();
            var looseCableSockets = hasFloorRoute ? Array.Empty<InteriorSocketDTO1608>() : new InteriorSocketDTO1608[cableSocketCount];
            int ceilingWrite = 0;
            int floorWrite = 0;
            int looseWrite = 0;
            for (int i = 0; i < sockets.Count; i++)
            {
                InteriorSocketDTO1608 socket = sockets[i];
                if (!CableSocketPassesDensity(socket, settings.DensityWeight, settings.Seed))
                    continue;

                if (socket.SocketKind == InteriorSocketKind1608.CeilingCable)
                {
                    ceilingSockets[ceilingWrite++] = socket;
                    if (!hasFloorRoute)
                        looseCableSockets[looseWrite++] = socket;
                }
                else if (socket.SocketKind == InteriorSocketKind1608.FloorConduit)
                {
                    floorSockets[floorWrite++] = socket;
                    if (!hasFloorRoute)
                        looseCableSockets[looseWrite++] = socket;
                }
            }

            // COLD ALLOC: Mesh[pairCount] + CombineInstance[pairCount] - editor-only cable bundle combine scratch - owner: InteriorFinisherPipeline1608
            var meshes = new Mesh[pairCount];
            var combine = new CombineInstance[pairCount];
            try
            {
                for (int i = 0; i < pairCount; i++)
                {
                    InteriorSocketDTO1608 a;
                    InteriorSocketDTO1608 b;
                    if (hasFloorRoute)
                    {
                        a = ceilingSockets[i % ceilingSockets.Length];
                        b = floorSockets[(i * 3) % floorSockets.Length];
                    }
                    else
                    {
                        a = looseCableSockets[i * 2];
                        b = looseCableSockets[i * 2 + 1];
                    }

                    Vector3 start = new Vector3(a.LocalPosition.x, a.LocalPosition.y, a.LocalPosition.z);
                    Vector3 end = new Vector3(b.LocalPosition.x, b.LocalPosition.y, b.LocalPosition.z);
                    if (hasFloorRoute && start.y < end.y)
                    {
                        Vector3 tmp = start;
                        start = end;
                        end = tmp;
                    }

                    if ((end - start).sqrMagnitude < 0.04f)
                        end += Vector3.right * 0.35f;

                    meshes[i] = GenerateCableBundles(
                        settings.OutputName + "_CableBundlePart_" + i.ToString("D2"),
                        start,
                        end,
                        strands,
                        segments,
                        radius,
                        slack,
                        settings.Seed ^ (uint)(i * 7919),
                        uploadMeshData: false);
                    combine[i] = new CombineInstance
                    {
                        mesh = meshes[i],
                        transform = Matrix4x4.identity
                    };
                }

                Mesh combined = new Mesh
                {
                    name = settings.OutputName + "_CableBundles",
                    indexFormat = IndexFormat.UInt32
                };
                combined.CombineMeshes(combine, true, true, false);
                combined.OptimizeIndexBuffers();
                combined.UploadMeshData(true);

                cableMeshPath = $"{settings.OutputFolder}/{combined.name}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(cableMeshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(combined, existing);
                    UnityEngine.Object.DestroyImmediate(combined);
                    return existing;
                }

                AssetDatabase.CreateAsset(combined, cableMeshPath);
                return combined;
            }
            finally
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] != null)
                        UnityEngine.Object.DestroyImmediate(meshes[i]);
                }
            }
        }

        public static bool CableSocketPassesDensity(InteriorSocketDTO1608 socket, float densityWeight, uint seed)
        {
            if (socket.SocketKind != InteriorSocketKind1608.CeilingCable &&
                socket.SocketKind != InteriorSocketKind1608.FloorConduit)
                return false;

            return InteriorFinisherMath1608.PassesDensityGate(socket.StableHash, seed ^ 0xCA8E1608u, densityWeight, socket.DensityHint);
        }

        private static InteriorFinisherSettings1608 Sanitize(InteriorFinisherSettings1608 settings)
        {
            settings.OutputFolder = SanitizeAssetFolder(settings.OutputFolder, "Assets/_Project/Art/Baked/Interiors");
            settings.InstrumentPrefabFolder = SanitizeAssetFolder(settings.InstrumentPrefabFolder, InteriorInstrumentLibraryBuilder1608.DefaultInstrumentFolder);
            settings.OutputName = SanitizeAssetName(settings.OutputName, "GEN_InteriorDetailPack_1608");
            settings.Seed = settings.Seed == 0u ? 1608u : settings.Seed;
            settings.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            settings.DensityWeight = math.saturate(settings.DensityWeight);
            settings.TextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(256, settings.TextureSize)), 256, InteriorFinisherConstants1608.MaxAtlasSize);
            return settings;
        }

        private static string SanitizeAssetFolder(string folder, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(folder) ? fallback : folder.Trim().Replace('\\', '/').TrimEnd('/');
            if (!value.Equals("Assets", StringComparison.Ordinal) && !value.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException($"Interior Finisher asset folder must be under Assets/: {value}");
            string[] segments = value.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "." || segments[i] == "..")
                    throw new InvalidOperationException($"Interior Finisher asset folder contains invalid segment: {value}");
            }

            return value;
        }

        private static string SanitizeAssetName(string name, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(invalid, c) >= 0)
                    buffer[i] = '_';
            }

            string result = new string(buffer).Trim('_', ' ');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private static NativeArray<T> ToNative<T>(List<T> values, Allocator allocator) where T : unmanaged
        {
            var native = new NativeArray<T>(values.Count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < values.Count; i++)
                native[i] = values[i];
            return native;
        }

        /// <summary>
        /// Fails the bake before any asset is written when the pipeline had no authored input
        /// and would emit the procedural box kit and/or an AABB socket grid. Opt out only with
        /// <see cref="InteriorFinisherSettings1608.AllowFallbackKit"/>, which marks the bake as
        /// diagnostic. Without this gate an unfed bake reported Success with cardboard, because
        /// FaultNoRules and FaultNoSockets are structurally unreachable once the fallbacks fill
        /// the arrays.
        /// </summary>
        private static void FailClosedOnFallbackKit(InteriorFinisherSettings1608 settings, InteriorInstrumentLibrary1608 library, bool authoredSockets)
        {
            if (settings.AllowFallbackKit)
                return;
            if (!library.UsedFallbackKit && authoredSockets)
                return;

            string modulePrefabName = settings.ModulePrefab != null ? settings.ModulePrefab.name : "<null>";
            string kitReason = library.UsedFallbackKit
                ? " No authored instrument prefab was readable under '" + settings.InstrumentPrefabFolder +
                  "', so the six procedural boxes in AppendFallbackRules would have been baked as final visuals."
                : string.Empty;
            string socketReason = authoredSockets
                ? string.Empty
                : " Module prefab '" + modulePrefabName +
                  "' carries no child named DecorativeSocket* or Socket_*, so an axis-aligned bounding-box socket grid would have been substituted for authored placement.";

            throw new InvalidOperationException(
                "Interior Finisher refused to bake fallback content." + kitReason + socketReason +
                " Author the missing input, or set AllowFallbackKit for an explicitly diagnostic bake.");
        }

        private static void FailClosedIfRequired(InteriorBakeCountersDTO1608 counters, string stage)
        {
            uint fatal = InteriorFinisherConstants1608.FaultNoSockets |
                         InteriorFinisherConstants1608.FaultNoRules |
                         InteriorFinisherConstants1608.FaultCapacity |
                         InteriorFinisherConstants1608.FaultNonFinite |
                         InteriorFinisherConstants1608.FaultAtlasOverflow |
                         InteriorFinisherConstants1608.FaultInvalidMesh;
            if ((counters.FaultFlags & fatal) != 0u)
                throw new InvalidOperationException($"Interior Finisher {stage} failed closed with fault mask 0x{counters.FaultFlags:X8}.");
        }

        private static Mesh CreateMeshAsset(InteriorFinisherSettings1608 settings, NativeArray<InteriorMeshVertexDTO1608> vertices, NativeArray<int> indices, InteriorBakeCountersDTO1608 counters, out string meshPath)
        {
            int vertexCount = (int)counters.FusedVertexCount;
            int indexCount = (int)counters.FusedIndexCount;
            if (vertexCount <= 0 || indexCount < 3)
                throw new InvalidOperationException("Interior Finisher produced empty fused static mesh.");
            if (vertexCount > vertices.Length || indexCount > indices.Length || indexCount % 3 != 0)
                throw new InvalidOperationException("Interior Finisher mesh counters exceed buffer bounds.");

            NativeArray<InteriorRenderVertexDTO1608> renderVertices = default;
            try
            {
                renderVertices = new NativeArray<InteriorRenderVertexDTO1608>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                Bounds bounds = PackRenderVertices(vertices, renderVertices, vertexCount);
                Mesh mesh = new Mesh { name = settings.OutputName + "_StaticBases", indexFormat = IndexFormat.UInt32 };
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
                mesh.SetVertexBufferParams(vertexCount, s_vertexLayout);
                mesh.SetIndexBufferParams(indexCount, mesh.indexFormat);
                mesh.SetVertexBufferData(renderVertices, 0, 0, vertexCount, 0, flags);
                mesh.SetIndexBufferData(indices, 0, 0, indexCount, flags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, flags);
                mesh.bounds = bounds;
                mesh.OptimizeIndexBuffers();
                mesh.UploadMeshData(true);

                meshPath = $"{settings.OutputFolder}/{mesh.name}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    return existing;
                }

                AssetDatabase.CreateAsset(mesh, meshPath);
                return mesh;
            }
            finally
            {
                if (renderVertices.IsCreated)
                    renderVertices.Dispose();
            }
        }

        private static Bounds PackRenderVertices(
            NativeArray<InteriorMeshVertexDTO1608> source,
            NativeArray<InteriorRenderVertexDTO1608> destination,
            int vertexCount)
        {
            bool hasBounds = false;
            float3 min = default;
            float3 max = default;
            int count = math.min(vertexCount, math.min(source.Length, destination.Length));
            for (int i = 0; i < count; i++)
            {
                InteriorMeshVertexDTO1608 vertex = source[i];
                InteriorRenderVertexDTO1608 packed = default;
                packed.Position = vertex.Position;
                packed.ColorRgba = vertex.ColorRgba;
                packed.Normal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
                packed.Tangent = vertex.Tangent;
                packed.Uv0 = vertex.Uv0;
                packed.InstrumentHash = vertex.InstrumentHash != 0u ? vertex.InstrumentHash : vertex.Flags;
                destination[i] = packed;

                if (!hasBounds)
                {
                    min = vertex.Position;
                    max = vertex.Position;
                    hasBounds = true;
                }
                else
                {
                    min = math.min(min, vertex.Position);
                    max = math.max(max, vertex.Position);
                }
            }

            if (!hasBounds)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }


    public struct BakeStampedTextureArgs1608
    {
        public InteriorFinisherSettings1608 Settings;
        public NativeArray<InteriorSocketDTO1608> MicroSockets;
        public NativeArray<InstrumentPlacementDTO1608> Placements;
        public NativeArray<InteriorBakeCountersDTO1608> Counters;
    }

        private static void BakeStampedTextureAssets(BakeStampedTextureArgs1608 args, out string normalPath, out string grimePath)
        {
            int size = args.Settings.TextureSize;
            int pixelCount = size * size;
            var normalPixels = new NativeArray<InteriorRgba32DTO1608>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var grimePixels = new NativeArray<InteriorRgba32DTO1608>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
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

                new NormalMapStampingJob1608
                {
                    NormalPixels = normalPixels,
                    GrimePixels = grimePixels,
                    MicroSockets = args.MicroSockets,
                    Placements = args.Placements,
                    Width = size,
                    Height = size,
                    GlobalQualityWeight = args.Settings.GlobalQualityWeight
                }.Run();

                if (args.Counters.IsCreated && args.Counters.Length > 0)
                {
                    InteriorBakeCountersDTO1608 counterValue = args.Counters[0];
                    bool wroteAnyStamp =
                        (args.MicroSockets.IsCreated && args.MicroSockets.Length > 0) ||
                        counterValue.PlacementCount > 0u;
                    if (wroteAnyStamp)
                    {
                        counterValue.NormalPixelsWritten = (uint)pixelCount;
                        counterValue.GrimePixelsWritten = (uint)pixelCount;
                    }

                    args.Counters[0] = counterValue;
                }

                normalPath = $"{args.Settings.OutputFolder}/TX_{args.Settings.OutputName}_Normal.png";
                grimePath = $"{args.Settings.OutputFolder}/TX_{args.Settings.OutputName}_Grime.png";
                WriteTexture(normalPath, normalPixels, size, InteriorTextureRole1608.Normal);
                WriteTexture(grimePath, grimePixels, size, InteriorTextureRole1608.Grime);
                SetTextureImportSettings(normalPath, InteriorTextureRole1608.Normal);
                SetTextureImportSettings(grimePath, InteriorTextureRole1608.Grime);
            }
            finally
            {
                grimePixels.Dispose();
                normalPixels.Dispose();
            }
        }

        private static void WriteTexture(string path, NativeArray<InteriorRgba32DTO1608> pixels, int size, InteriorTextureRole1608 role)
        {
            var colors = new NativeArray<Color32>(pixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            bool linear = role != InteriorTextureRole1608.Atlas;
            try
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    InteriorRgba32DTO1608 p = pixels[i];
                    colors[i] = new Color32(p.R, p.G, p.B, p.A);
                }

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
                try
                {
                    texture.SetPixelData(colors, 0);
                    texture.Apply(true, false);
                    File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                colors.Dispose();
            }

            AssetDatabase.ImportAsset(path);
        }

        private static Material CreateOrUpdateMaterial(string outputFolder, string outputName, string atlasPath, string normalPath, string grimePath)
        {
            string materialName = $"MAT_{SanitizeAssetName(outputName, "InteriorDetailPack")}_InteriorFinisher_1608";
            string materialPath = $"{outputFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D grime = AssetDatabase.LoadAssetAtPath<Texture2D>(grimePath);
            if (atlas != null)
            {
                material.mainTexture = atlas;
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", atlas);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", atlas);
            }
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", 0.82f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (grime != null && material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", grime);
                if (material.HasProperty("_OcclusionStrength"))
                    material.SetFloat("_OcclusionStrength", 0.78f);
            }
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.06f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.34f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateCableMaterial(string outputFolder)
        {
            string materialPath = $"{outputFolder}/MAT_InteriorCable_1608.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = "MAT_InteriorCable_1608" };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Color cableColor = new Color(0.035f, 0.033f, 0.029f, 1f);
            material.mainTexture = null;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", cableColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", cableColor);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.02f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.24f);
            if (material.HasProperty("_OcclusionStrength"))
                material.SetFloat("_OcclusionStrength", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateHandleMaterial(string outputFolder)
        {
            string materialPath = $"{outputFolder}/MAT_InteriorHandle_1608.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = "MAT_InteriorHandle_1608" };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Color handleColor = new Color(0.19f, 0.16f, 0.105f, 1f);
            material.mainTexture = null;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", handleColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", handleColor);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.18f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.31f);
            if (material.HasProperty("_OcclusionStrength"))
                material.SetFloat("_OcclusionStrength", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static string CreatePrefabAsset(InteriorFinisherSettings1608 settings, Mesh mesh, Mesh cableMesh, Material material, Material cableMaterial, Material handleMaterial, NativeArray<InstrumentPlacementDTO1608> placements, InteriorBakeCountersDTO1608 counters)
        {
            GameObject root = new GameObject(settings.OutputName);
            try
            {
                var filter = root.AddComponent<MeshFilter>();
                var renderer = root.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                root.isStatic = true;

                if (cableMesh != null)
                {
                    GameObject cables = new GameObject("GEN_CableBundles_1608");
                    cables.transform.SetParent(root.transform, false);
                    var cableFilter = cables.AddComponent<MeshFilter>();
                    var cableRenderer = cables.AddComponent<MeshRenderer>();
                    cableFilter.sharedMesh = cableMesh;
                    cableRenderer.sharedMaterial = cableMaterial != null ? cableMaterial : material;
                    cableRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    cableRenderer.receiveShadows = true;
                    cables.isStatic = true;
                }

                int movingCount = 0;
                int count = (int)Math.Min(counters.PlacementCount, (uint)placements.Length);
                Mesh movableHandleMesh = counters.MovingPartCount > 0u ? CreateOrUpdateMovableHandleMeshAsset(settings) : null;
                for (int i = 0; i < count; i++)
                {
                    InstrumentPlacementDTO1608 placement = placements[i];
                    if ((placement.Flags & InteriorFinisherConstants1608.InstrumentMovableFlag) == 0u)
                        continue;

                    GameObject moving = new GameObject("MOV_InstrumentHandle_" + movingCount.ToString("D4"));
                    moving.transform.SetParent(root.transform, false);
                    float3 p = placement.LocalToRoom.c3.xyz;
                    moving.transform.localPosition = new Vector3(p.x, p.y, p.z);
                    moving.transform.localRotation = ToQuaternion(placement.LocalToRoom);
                    float handleScale = ExtractUniformScale(placement.LocalToRoom);
                    moving.transform.localScale = new Vector3(handleScale, handleScale, handleScale);
                    if (movableHandleMesh != null)
                    {
                        var movingFilter = moving.AddComponent<MeshFilter>();
                        var movingRenderer = moving.AddComponent<MeshRenderer>();
                        movingFilter.sharedMesh = movableHandleMesh;
                        movingRenderer.sharedMaterial = handleMaterial != null ? handleMaterial : material;
                        movingRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        movingRenderer.receiveShadows = true;
                    }

                    movingCount++;
                }

                string prefabPath = $"{settings.OutputFolder}/{settings.OutputName}.prefab";
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("Collider topology rejected before interior prefab save. path=" + prefabPath + " reason=" + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("Collider topology rejected after interior prefab save. path=" + prefabPath + " reason=" + colliderFailure);

                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Mesh CreateOrUpdateMovableHandleMeshAsset(InteriorFinisherSettings1608 settings)
        {
            const int BoxCount = 2;
            const int VerticesPerBox = 24;
            const int IndicesPerBox = 36;
            const int VertexCount = BoxCount * VerticesPerBox;
            const int IndexCount = BoxCount * IndicesPerBox;

            // COLD ALLOC: fixed mesh arrays[48/72] - editor-only shared movable handle proxy - owner: InteriorFinisherPipeline1608
            var vertices = new Vector3[VertexCount];
            var normals = new Vector3[VertexCount];
            var tangents = new Vector4[VertexCount];
            var uvs = new Vector2[VertexCount];
            var colors = new Color32[VertexCount];
            var indices = new int[IndexCount];

            int vertexWrite = 0;
            int indexWrite = 0;
            Color32 shaftColor = new Color32(48, 52, 46, 255);
            Color32 gripColor = new Color32(92, 78, 54, 255);
            AppendHandleBox(new Vector3(0f, 0f, 0.06f), new Vector3(0.018f, 0.018f, 0.06f), shaftColor, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleBox(new Vector3(0f, 0f, 0.132f), new Vector3(0.055f, 0.028f, 0.018f), gripColor, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);

            string meshPath = $"{settings.OutputFolder}/{settings.OutputName}_MovableHandle.asset";
            Mesh mesh = new Mesh
            {
                name = settings.OutputName + "_MovableHandle",
                indexFormat = IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.07f), new Vector3(0.13f, 0.07f, 0.18f));
            mesh.OptimizeIndexBuffers();
            mesh.UploadMeshData(true);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        private static void AppendHandleBox(
            Vector3 center,
            Vector3 extents,
            Color32 color,
            Vector3[] vertices,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
            ref int vertexWrite,
            ref int indexWrite)
        {
            Vector3 min = center - extents;
            Vector3 max = center + extents;
            AppendHandleFace(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, new Vector4(1f, 0f, 0f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleFace(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, new Vector4(-1f, 0f, 0f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleFace(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, new Vector4(1f, 0f, 0f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleFace(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, new Vector4(1f, 0f, 0f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleFace(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, new Vector4(0f, 0f, -1f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
            AppendHandleFace(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, new Vector4(0f, 0f, 1f, 1f), color, vertices, normals, tangents, uvs, colors, indices, ref vertexWrite, ref indexWrite);
        }

        private static void AppendHandleFace(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            Vector4 tangent,
            Color32 color,
            Vector3[] vertices,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
            ref int vertexWrite,
            ref int indexWrite)
        {
            int baseVertex = vertexWrite;
            vertices[vertexWrite] = a;
            normals[vertexWrite] = normal;
            tangents[vertexWrite] = tangent;
            uvs[vertexWrite] = new Vector2(0.12f, 0.12f);
            colors[vertexWrite++] = color;

            vertices[vertexWrite] = b;
            normals[vertexWrite] = normal;
            tangents[vertexWrite] = tangent;
            uvs[vertexWrite] = new Vector2(0.88f, 0.12f);
            colors[vertexWrite++] = color;

            vertices[vertexWrite] = c;
            normals[vertexWrite] = normal;
            tangents[vertexWrite] = tangent;
            uvs[vertexWrite] = new Vector2(0.88f, 0.88f);
            colors[vertexWrite++] = color;

            vertices[vertexWrite] = d;
            normals[vertexWrite] = normal;
            tangents[vertexWrite] = tangent;
            uvs[vertexWrite] = new Vector2(0.12f, 0.88f);
            colors[vertexWrite++] = color;

            indices[indexWrite++] = baseVertex;
            indices[indexWrite++] = baseVertex + 1;
            indices[indexWrite++] = baseVertex + 2;
            indices[indexWrite++] = baseVertex;
            indices[indexWrite++] = baseVertex + 2;
            indices[indexWrite++] = baseVertex + 3;
        }

        private static Quaternion ToQuaternion(float4x4 matrix)
        {
            Vector3 forward = new Vector3(matrix.c2.x, matrix.c2.y, matrix.c2.z);
            Vector3 up = new Vector3(matrix.c1.x, matrix.c1.y, matrix.c1.z);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;
            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private static float ExtractUniformScale(float4x4 matrix)
        {
            float3 scale = new float3(
                math.length(matrix.c0.xyz),
                math.length(matrix.c1.xyz),
                math.length(matrix.c2.xyz));
            if (!math.all(math.isfinite(scale)))
                return 1f;
            return math.max(0.0001f, math.cmax(scale));
        }

        internal static void SetTextureImportSettings(string path, InteriorTextureRole1608 role)
        {
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            bool normal = role == InteriorTextureRole1608.Normal;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = role == InteriorTextureRole1608.Atlas;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = InteriorFinisherConstants1608.MaxAtlasSize;
            ApplyTexturePlatform(importer, "Standalone", InteriorFinisherConstants1608.MaxAtlasSize, normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7);
            int mobileMaxTextureSize = Math.Min(2048, InteriorFinisherConstants1608.MaxAtlasSize);
            ApplyTexturePlatform(importer, "Android", mobileMaxTextureSize, TextureImporterFormat.ASTC_6x6);
            ApplyTexturePlatform(importer, "iPhone", mobileMaxTextureSize, TextureImporterFormat.ASTC_6x6);
            importer.SaveAndReimport();
        }

        private static void ApplyTexturePlatform(TextureImporter importer, string platform, int maxTextureSize, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.format = format;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
        }

        private static int CountTransforms(GameObject root)
        {
            if (root == null)
                return 0;
            s_transformScratch.Clear();
            root.GetComponentsInChildren(true, s_transformScratch);
            try
            {
                return s_transformScratch.Count;
            }
            finally
            {
                s_transformScratch.Clear();
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }

    public static class InteriorAtlasPacker1608
    {
        public static string PackInstrumentAtlas(InteriorInstrumentLibrary1608 library, string outputFolder, string outputName, int targetSize, out float efficiency01, out uint areaUsed, out uint areaTotal, out uint textureCount)
        {
            ResolveAtlasGrid(library.Names.Length, targetSize, out int size, out int count, out int cell, out int columns, out int rows);
            if (rows * cell > size)
                throw new InvalidOperationException("Atlas Overflow - Too many unique instruments requested");

            uint used = 0u;
            string safeName = SanitizeAtlasName(outputName);
            string path = $"{outputFolder}/TX_{safeName}_InstrumentAtlas_1608.png";
            Texture2D atlas = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            try
            {
                var clear = new NativeArray<Color32>(size * size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                try
                {
                    for (int i = 0; i < clear.Length; i++)
                        clear[i] = new Color32(0, 0, 0, 0);
                    atlas.SetPixelData(clear, 0);
                }
                finally
                {
                    clear.Dispose();
                }

                // COLD ALLOC: Color32[cell*cell] - reusable editor-only atlas cell buffer - owner: InteriorAtlasPacker1608
                var block = new Color32[cell * cell];
                Texture2D sampleScratch = new Texture2D(cell, cell, TextureFormat.RGBA32, false, false)
                {
                    name = "TMP_InteriorAtlasSample_1608"
                };
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        int x = (i % columns) * cell;
                        int y = (i / columns) * cell;
                        FillTextureBlock(library, i, cell, block, sampleScratch, out uint visibleArea, out int writeX, out int writeY, out int writeWidth, out int writeHeight);
                        atlas.SetPixels32(x, y, cell, cell, block);
                        ApplyPackedAtlasRect(library, i, x + writeX, y + writeY, writeWidth, writeHeight, size);
                        used += visibleArea;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sampleScratch);
                }

                atlas.Apply(true, false);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(atlas));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
            }

            AssetDatabase.ImportAsset(path);
            SetAtlasImport(path);
            areaUsed = used;
            areaTotal = (uint)(size * size);
            textureCount = (uint)count;
            efficiency01 = areaTotal == 0u ? 0f : areaUsed / (float)areaTotal;
            return path;
        }

        private static string SanitizeAtlasName(string outputName)
        {
            string value = string.IsNullOrWhiteSpace(outputName) ? "InteriorInstrumentAtlas" : outputName.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(invalid, c) >= 0)
                    buffer[i] = '_';
            }

            string result = new string(buffer).Trim('_', ' ');
            return string.IsNullOrWhiteSpace(result) ? "InteriorInstrumentAtlas" : result;
        }

        private static void FillTextureBlock(
            InteriorInstrumentLibrary1608 library,
            int index,
            int cell,
            Color32[] block,
            Texture2D sampleScratch,
            out uint visibleArea,
            out int writeX,
            out int writeY,
            out int writeWidth,
            out int writeHeight)
        {
            visibleArea = (uint)(cell * cell);
            writeX = 0;
            writeY = 0;
            writeWidth = cell;
            writeHeight = cell;
            if (block == null || block.Length < cell * cell)
                throw new InvalidOperationException("Interior atlas block buffer is smaller than requested cell.");
            if (TryFillAuthoredTextureBlock(library, index, cell, block, sampleScratch, out visibleArea, out writeX, out writeY, out writeWidth, out writeHeight))
                return;

            visibleArea = (uint)(cell * cell);
            uint h = InteriorInstrumentLibraryBuilder1608.HashString(index < library.Names.Length ? library.Names[index] : "fallback");
            byte r = (byte)(80 + (h & 63u));
            byte g = (byte)(88 + ((h >> 8) & 63u));
            byte b = (byte)(78 + ((h >> 16) & 47u));
            int center = cell >> 1;
            int ringRadius = Mathf.Max(4, cell / 3);
            int ringWidth = Mathf.Max(1, cell / 48);
            int screwRadius = Mathf.Max(1, cell / 28);
            int needleX = center + ((int)((h >> 4) & 15u) - 7) * Mathf.Max(1, cell / 96);
            for (int y = 0; y < cell; y++)
            {
                for (int x = 0; x < cell; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= cell - 2 || y >= cell - 2;
                    int dx = x - center;
                    int dy = y - center;
                    int distSq = dx * dx + dy * dy;
                    bool screw = distSq <= screwRadius * screwRadius;
                    bool ring = distSq >= (ringRadius - ringWidth) * (ringRadius - ringWidth) &&
                                distSq <= (ringRadius + ringWidth) * (ringRadius + ringWidth);
                    bool tick = (x == center || y == center || math.abs(dx - dy) <= 1 || math.abs(dx + dy) <= 1) &&
                                distSq >= (ringRadius - ringWidth * 6) * (ringRadius - ringWidth * 6) &&
                                distSq <= (ringRadius + ringWidth * 3) * (ringRadius + ringWidth * 3);
                    bool needle = math.abs(x - needleX) <= 1 && y >= center - ringRadius / 2 && y <= center + ringRadius / 2;
                    byte grime = (byte)(edge ? 42 : (screw ? 54 : 0));
                    byte light = (byte)(ring ? 46 : (tick ? 34 : (needle ? 58 : 0)));
                    block[y * cell + x] = new Color32(
                        (byte)Mathf.Clamp(r - grime + light, 0, 255),
                        (byte)Mathf.Clamp(g - grime + light, 0, 255),
                        (byte)Mathf.Clamp(b - grime + light, 0, 255),
                        255);
                }
            }
        }

        private static bool TryFillAuthoredTextureBlock(
            InteriorInstrumentLibrary1608 library,
            int index,
            int cell,
            Color32[] block,
            Texture2D sampleScratch,
            out uint visibleArea,
            out int writeX,
            out int writeY,
            out int writeWidth,
            out int writeHeight)
        {
            visibleArea = 0u;
            writeX = 0;
            writeY = 0;
            writeWidth = cell;
            writeHeight = cell;
            if (library == null || library.TexturePaths == null || index < 0 || index >= library.TexturePaths.Length)
                return false;
            string path = library.TexturePaths[index];
            if (string.IsNullOrWhiteSpace(path))
                return false;

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null || sampleScratch == null)
                return false;

            RenderTexture rt = RenderTexture.GetTemporary(cell, cell, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                UnityEngine.Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                sampleScratch.ReadPixels(new Rect(0, 0, cell, cell), 0, 0, false);
                sampleScratch.Apply(false, false);
                NativeArray<Color32> pixels = sampleScratch.GetPixelData<Color32>(0);
                int max = math.min(block.Length, pixels.Length);
                if (!TryResolveAlphaBounds(pixels, max, cell, out int minX, out int minY, out int maxX, out int maxY))
                    return false;

                Color32 paddingColor = ResolveAuthoredPaddingColor(pixels, cell, minX, minY, maxX, maxY);
                FillBlock(block, paddingColor);
                CopyCroppedAlphaBlock(pixels, block, cell, minX, minY, maxX, maxY, out writeX, out writeY, out writeWidth, out writeHeight);
                visibleArea = (uint)(writeWidth * writeHeight);
                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static bool TryResolveAlphaBounds(NativeArray<Color32> pixels, int pixelCount, int cell, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = cell;
            minY = cell;
            maxX = -1;
            maxY = -1;
            for (int i = 0; i < pixelCount; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a <= 3)
                    continue;

                int x = i % cell;
                int y = i / cell;
                minX = math.min(minX, x);
                minY = math.min(minY, y);
                maxX = math.max(maxX, x);
                maxY = math.max(maxY, y);
            }

            return maxX >= minX && maxY >= minY;
        }

        private static Color32 ResolveAuthoredPaddingColor(NativeArray<Color32> pixels, int cell, int minX, int minY, int maxX, int maxY)
        {
            uint cropArea = (uint)((maxX - minX + 1) * (maxY - minY + 1));
            uint opaqueCount = 0u;
            uint edgeCount = 0u;
            uint r = 0u;
            uint g = 0u;
            uint b = 0u;
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * cell;
                for (int x = minX; x <= maxX; x++)
                {
                    Color32 pixel = pixels[row + x];
                    if (pixel.a <= 3)
                        continue;

                    opaqueCount++;
                    bool edgePixel = x == minX || x == maxX || y == minY || y == maxY;
                    if (!edgePixel)
                        continue;

                    edgeCount++;
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                }
            }

            if (opaqueCount == 0u || edgeCount == 0u || opaqueCount * 100u < cropArea * 35u)
                return AuthoredPaddingFallbackColor();

            return new Color32((byte)(r / edgeCount), (byte)(g / edgeCount), (byte)(b / edgeCount), 255);
        }

        private static Color32 AuthoredPaddingFallbackColor()
        {
            return new Color32(42, 46, 42, 255);
        }

        private static void FillBlock(Color32[] block, Color32 color)
        {
            for (int i = 0; i < block.Length; i++)
                block[i] = color;
        }

        private static void CopyCroppedAlphaBlock(
            NativeArray<Color32> source,
            Color32[] block,
            int cell,
            int minX,
            int minY,
            int maxX,
            int maxY,
            out int writeX,
            out int writeY,
            out int writeWidth,
            out int writeHeight)
        {
            int cropWidth = maxX - minX + 1;
            int cropHeight = maxY - minY + 1;
            int pad = Mathf.Max(1, cell / 64);
            int available = Mathf.Max(1, cell - pad * 2);
            float scale = Mathf.Min(available / (float)cropWidth, available / (float)cropHeight);
            writeWidth = Mathf.Clamp(Mathf.RoundToInt(cropWidth * scale), 1, available);
            writeHeight = Mathf.Clamp(Mathf.RoundToInt(cropHeight * scale), 1, available);
            writeX = (cell - writeWidth) >> 1;
            writeY = (cell - writeHeight) >> 1;
            int writeX1 = writeX + writeWidth;
            int writeY1 = writeY + writeHeight;
            for (int y = writeY; y < writeY1; y++)
            {
                int sourceY = minY + ((y - writeY) * cropHeight) / writeHeight;
                sourceY = Mathf.Clamp(sourceY, minY, maxY);
                for (int x = writeX; x < writeX1; x++)
                {
                    int sourceX = minX + ((x - writeX) * cropWidth) / writeWidth;
                    sourceX = Mathf.Clamp(sourceX, minX, maxX);
                    Color32 pixel = source[sourceY * cell + sourceX];
                    if (pixel.a <= 3)
                        continue;

                    block[y * cell + x] = pixel;
                }
            }
        }

        private static void ApplyPackedAtlasRect(InteriorInstrumentLibrary1608 library, int sourceIndex, int x, int y, int width, int height, int atlasSize)
        {
            if (library == null || !library.Rules.IsCreated || atlasSize <= 0)
                return;

            float invSize = 1f / atlasSize;
            float pad = invSize;
            float2 uvMin = new float2(x * invSize + pad, y * invSize + pad);
            float2 uvMax = new float2((x + width) * invSize - pad, (y + height) * invSize - pad);
            uvMax = math.max(uvMax, uvMin + new float2(0.0001f));
            for (int i = 0; i < library.Rules.Length; i++)
            {
                InstrumentRuleDTO1608 rule = library.Rules[i];
                if (rule.AtlasSourceIndex != sourceIndex)
                    continue;

                rule.UvMin = uvMin;
                rule.UvMax = uvMax;
                library.Rules[i] = rule;
            }
        }

        private static void SetAtlasImport(string path)
        {
            InteriorFinisherPipeline1608.SetTextureImportSettings(path, InteriorTextureRole1608.Atlas);
        }

        private static void ResolveAtlasGrid(int textureCount, int targetSize, out int size, out int count, out int cell, out int columns, out int rows)
        {
            count = Mathf.Clamp(textureCount, 1, InteriorFinisherConstants1608.MaxInstrumentRules);
            int maxSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(256, targetSize)), 256, InteriorFinisherConstants1608.MaxAtlasSize);
            int gridSide = Mathf.CeilToInt(Mathf.Sqrt(count));
            cell = Mathf.Min(512, maxSize);
            size = maxSize;
            while (cell > 32)
            {
                int requiredSize = Mathf.NextPowerOfTwo(Mathf.Max(256, gridSide * cell));
                if (requiredSize <= maxSize)
                {
                    size = requiredSize;
                    break;
                }

                cell >>= 1;
            }

            columns = Mathf.Max(1, size / cell);
            rows = Mathf.CeilToInt(count / (float)columns);
        }
    }

    public sealed class InteriorFinisherStudio1608 : EditorWindow
    {
        private ObjectField _modulePrefabField;
        private TextField _instrumentFolderField;
        private TextField _outputFolderField;
        private TextField _outputNameField;
        private IntegerField _seedField;
        private Slider _qualityField;
        private Slider _densityField;
        private SliderInt _textureSizeField;
        private Toggle _allowFallbackKitField;
        private Label _lastResultLabel;
        private Label _metricsLabel;
        private InteriorFinisherResult1608 _lastResult;

        [MenuItem("Hecton8/Interiors/Interior Finisher Studio")]
        public static void Open()
        {
            GetWindow<InteriorFinisherStudio1608>("Interior Finisher Studio");
        }

        public void CreateGUI()
        {
            InteriorFinisherSettings1608 defaults = InteriorFinisherSettings1608.Default;
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            Label title = new Label("Interior Finisher Studio");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            _modulePrefabField = new ObjectField("Generated Module Prefab") { objectType = typeof(GameObject), allowSceneObjects = false };
            _instrumentFolderField = new TextField("Instrument Prefab Folder") { value = defaults.InstrumentPrefabFolder };
            _outputFolderField = new TextField("Output Folder") { value = defaults.OutputFolder };
            _outputNameField = new TextField("Output Name") { value = defaults.OutputName };
            _seedField = new IntegerField("Detailing Seed") { value = (int)defaults.Seed };
            _qualityField = new Slider("Global Quality Weight", 0f, 1f) { value = defaults.GlobalQualityWeight, showInputField = true };
            _densityField = new Slider("Density", 0f, 1f) { value = defaults.DensityWeight, showInputField = true };
            _textureSizeField = new SliderInt("Texture Size", 256, InteriorFinisherConstants1608.MaxAtlasSize) { value = defaults.TextureSize, showInputField = true };
            _allowFallbackKitField = new Toggle("Allow Fallback Kit (DIAGNOSTIC)") { value = defaults.AllowFallbackKit };
            _allowFallbackKitField.tooltip =
                "Off: the bake fails closed when the instrument folder has no authored prefab or the module " +
                "carries no Socket_* / DecorativeSocket markers. On: bakes the procedural box kit and/or an " +
                "AABB socket grid, which PROCEDURAL_ASSET_PIPELINE.md rejects as final visuals. Use a separate " +
                "output folder for a diagnostic bake.";
            root.Add(_modulePrefabField);
            root.Add(_instrumentFolderField);
            root.Add(_outputFolderField);
            root.Add(_outputNameField);
            root.Add(_seedField);
            root.Add(_qualityField);
            root.Add(_densityField);
            root.Add(_textureSizeField);
            root.Add(_allowFallbackKitField);

            Button run = new Button(RunFromUi) { text = "Finish Interior" };
            run.style.height = 32f;
            run.style.marginTop = 8f;
            root.Add(run);

            _lastResultLabel = new Label("No interior finish executed.");
            _metricsLabel = new Label("Placement: 0 ms | Atlas: 0% | Removed: 0");
            _lastResultLabel.style.marginTop = 8f;
            root.Add(_lastResultLabel);
            root.Add(_metricsLabel);
        }

        private void RunFromUi()
        {
            InteriorFinisherSettings1608 settings = new InteriorFinisherSettings1608
            {
                ModulePrefab = _modulePrefabField.value as GameObject,
                InstrumentPrefabFolder = _instrumentFolderField.value,
                OutputFolder = _outputFolderField.value,
                OutputName = _outputNameField.value,
                Seed = (uint)Mathf.Max(1, _seedField.value),
                GlobalQualityWeight = _qualityField.value,
                DensityWeight = _densityField.value,
                TextureSize = _textureSizeField.value,
                AllowFallbackKit = _allowFallbackKitField.value
            };

            InteriorFinisherPipeline1608.FinishInterior(settings, out _lastResult);
            _lastResultLabel.text = _lastResult.Success ? _lastResult.PrefabPath : _lastResult.FailureReason;
            string provenance = _lastResult.UsedFallbackInstrumentKit || _lastResult.UsedFallbackSocketLayout
                ? " | SOURCE: DIAGNOSTIC FALLBACK, kit=" + _lastResult.UsedFallbackInstrumentKit +
                  " aabbSockets=" + _lastResult.UsedFallbackSocketLayout
                : " | SOURCE: authored";
            _metricsLabel.text = "Placement: " + _lastResult.Counters.PlacementMilliseconds.ToString("F2") +
                                 " ms | Atlas: " + (_lastResult.AtlasEfficiency01 * 100f).ToString("F1") +
                                 "% | Removed: " + _lastResult.Counters.GameObjectsEliminated +
                                 provenance;
        }
    }
}
#endif

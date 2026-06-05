#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.ProductFace
{
    /// <summary>
    /// Editor-only source authoring for future resource pickup mesh assets.
    /// This does not replace prefabs and does not create item data. CopperOre maps to canonical Data_Copper.asset.
    /// Item_Titanium is intentionally not generated here; it is quarantine/canonical-route only.
    /// </summary>
    public static class ProductFaceResourcePickupMeshSourceAuthoring
    {
        private const string OutputFolder = "Assets/_Project/Art/Generated/ProductFace/Resources";
        private const float DefaultGlobalQualityWeight = 0.62f;
        private const float ValidationAreaEpsilon = 0.0000001f;

        private static readonly ResourceSpec[] s_specs =
        {
            new ResourceSpec(
                "CopperOre",
                "Data_Copper",
                GeometryKind.OreChunk,
                0xC0AA1875u,
                new Vector3(0.42f, 0.29f, 0.34f),
                "Copper-bearing fractured host-rock chunk; oxide streak mask uses vertex color R and must not become a recolored cube."),
            new ResourceSpec(
                "FiberKelp",
                "Data_FiberKelp",
                GeometryKind.FrondBundle,
                0xF1B31875u,
                new Vector3(0.33f, 0.92f, 0.22f),
                "Harvested folded kelp strips with ragged cut ends and thickness; clip-card silhouette only after mesh source proof."),
            new ResourceSpec(
                "HydrocarbonResin",
                "Data_HydrocarbonResin",
                GeometryKind.ResinClump,
                0xA9B51875u,
                new Vector3(0.38f, 0.25f, 0.31f),
                "Sticky dark amber resin lobes with grit and sagging pod break-up; no flat transparent plane route."),
            new ResourceSpec(
                "MembraneTissue",
                "Data_MembraneTissue",
                GeometryKind.MembraneFold,
                0xB10B1875u,
                new Vector3(0.48f, 0.18f, 0.32f),
                "Torn wet membrane sheet with vein/fold channels and cut edges; not a sphere or generic blob."),
            new ResourceSpec(
                "SilicaShards",
                "Data_SilicaShards",
                GeometryKind.ShardCluster,
                0x511C1875u,
                new Vector3(0.43f, 0.36f, 0.35f),
                "Milky angular shard cluster with varied fracture planes and edge glint masks; not a ball."),
            new ResourceSpec(
                "SilverOre",
                "Data_SilverOre",
                GeometryKind.SilverOreChunk,
                0x51A91875u,
                new Vector3(0.39f, 0.27f, 0.32f),
                "Darker host-rock ore with narrow silver seams and conductive vein ridges distinct from copper oxide streaks."),
            new ResourceSpec(
                "SulfurClumps",
                "Data_SulfurClumps",
                GeometryKind.SulfurNodules,
                0x5A1F1875u,
                new Vector3(0.42f, 0.28f, 0.36f),
                "Brittle vent sulfur nodule cluster with porous lumps and soot/residue base; no toy-yellow sphere."),
            new ResourceSpec(
                "TitaniumScrap",
                "Data_TitaniumScrap",
                GeometryKind.ScrapPlate,
                0x71A91875u,
                new Vector3(0.58f, 0.16f, 0.31f),
                "Bent cut titanium salvage plate with torn edges, bolt holes, paint mask, salt/oil wear; canonical route for Item_Titanium if retained.")
        };

        [MenuItem("HECTON-8/Product Face/Author Resource Pickup Source Meshes", false, 1875)]
        private static void AuthorAllMeshes()
        {
            EnsureAssetFolder(OutputFolder);
            float q = Mathf.Clamp01(DefaultGlobalQualityWeight);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            int saved = 0;

            for (int i = 0; i < s_specs.Length; i++)
            {
                ResourceSpec spec = s_specs[i];
                Mesh mesh = BuildMesh(spec, q);
                ValidateMesh(spec, mesh, signatures);
                SaveMesh(mesh, OutputFolder + "/MESH_ProductFace_" + spec.ResourceId + "_Source_LOD0.asset");
                saved++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[1875 ResourcePickupMeshSourceAuthoring] Authored source meshes only. Meshes=" + saved + ", Output=" + OutputFolder + ", Unity proof still requires import/capture/prefab relink.");
        }

        [MenuItem("HECTON-8/Product Face/Author Resource Pickup Source Meshes", true)]
        private static bool ValidateAuthorAllMeshes()
        {
            return true;
        }

        private static Mesh BuildMesh(ResourceSpec spec, float globalQualityWeight)
        {
            float q = Mathf.Clamp01(IsFinite(globalQualityWeight) ? globalQualityWeight : 0f);
            var builder = new MeshBuilder("MESH_ProductFace_" + spec.ResourceId + "_Source_LOD0");
            switch (spec.Kind)
            {
                case GeometryKind.OreChunk:
                    AddOreChunk(builder, spec, q, copperBias: true);
                    break;
                case GeometryKind.SilverOreChunk:
                    AddOreChunk(builder, spec, q, copperBias: false);
                    break;
                case GeometryKind.FrondBundle:
                    AddFrondBundle(builder, spec, q);
                    break;
                case GeometryKind.ResinClump:
                    AddResinClump(builder, spec, q);
                    break;
                case GeometryKind.MembraneFold:
                    AddMembraneFold(builder, spec, q);
                    break;
                case GeometryKind.ShardCluster:
                    AddShardCluster(builder, spec, q);
                    break;
                case GeometryKind.SulfurNodules:
                    AddSulfurNodules(builder, spec, q);
                    break;
                case GeometryKind.ScrapPlate:
                    AddScrapPlate(builder, spec, q);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled resource mesh kind: " + spec.Kind);
            }

            return builder.ToMesh();
        }

        private static void AddOreChunk(MeshBuilder builder, ResourceSpec spec, float q, bool copperBias)
        {
            int rings = Mathf.RoundToInt(Mathf.Lerp(5f, copperBias ? 8f : 7f, q));
            int columns = Mathf.RoundToInt(Mathf.Lerp(8f, copperBias ? 15f : 13f, q));
            AddIrregularEllipsoid(builder, spec, rings, columns, copperBias ? 0.22f : 0.16f, copperBias ? 1.0f : 0.55f, q);

            int veinCount = Mathf.RoundToInt(Mathf.Lerp(3f, copperBias ? 8f : 6f, q));
            for (int i = 0; i < veinCount; i++)
            {
                float angle = (i + Hash01(spec.Seed, i)) * Mathf.PI * 2f / veinCount;
                float height = Mathf.Lerp(-0.12f, 0.14f, Hash01(spec.Seed ^ 19u, i));
                float length = Mathf.Lerp(0.28f, 0.58f, Hash01(spec.Seed ^ 41u, i)) * (copperBias ? 1f : 0.72f);
                Vector3 center = new Vector3(Mathf.Cos(angle) * spec.Scale.x * 0.54f, height, Mathf.Sin(angle) * spec.Scale.z * 0.54f);
                Vector3 axis = new Vector3(-Mathf.Sin(angle), copperBias ? 0.22f : 0.08f, Mathf.Cos(angle)).normalized;
                AddRaisedVein(builder, center, axis, length, copperBias ? 0.022f : 0.015f, copperBias ? new Color32(188, 94, 38, 190) : new Color32(190, 196, 196, 180));
            }
        }

        private static void AddFrondBundle(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int fronds = Mathf.RoundToInt(Mathf.Lerp(5f, 13f, q));
            for (int i = 0; i < fronds; i++)
            {
                float angle = i * Mathf.PI * 2f / fronds + HashSigned(spec.Seed, i) * 0.23f;
                float length = Mathf.Lerp(0.45f, spec.Scale.y, Hash01(spec.Seed ^ 7u, i));
                float width = Mathf.Lerp(0.035f, 0.085f, Hash01(spec.Seed ^ 11u, i)) * Mathf.Lerp(0.8f, 1.35f, q);
                Vector3 root = new Vector3(Mathf.Cos(angle) * 0.035f, -spec.Scale.y * 0.32f, Mathf.Sin(angle) * 0.035f);
                Vector3 direction = new Vector3(Mathf.Cos(angle) * 0.28f, 1f, Mathf.Sin(angle) * 0.28f).normalized;
                AddRibbonFrond(builder, spec.Seed + (uint)i * 17u, root, direction, length, width, Mathf.RoundToInt(Mathf.Lerp(4f, 10f, q)));
            }
        }

        private static void AddResinClump(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int lobes = Mathf.RoundToInt(Mathf.Lerp(4f, 9f, q));
            for (int i = 0; i < lobes; i++)
            {
                float angle = i * Mathf.PI * 2f / lobes;
                Vector3 center = new Vector3(
                    Mathf.Cos(angle) * spec.Scale.x * Mathf.Lerp(0.05f, 0.42f, Hash01(spec.Seed, i)),
                    HashSigned(spec.Seed ^ 17u, i) * spec.Scale.y * 0.24f - i * 0.002f,
                    Mathf.Sin(angle) * spec.Scale.z * Mathf.Lerp(0.05f, 0.38f, Hash01(spec.Seed ^ 23u, i)));
                Vector3 radius = new Vector3(
                    spec.Scale.x * Mathf.Lerp(0.18f, 0.38f, Hash01(spec.Seed ^ 31u, i)),
                    spec.Scale.y * Mathf.Lerp(0.32f, 0.72f, Hash01(spec.Seed ^ 47u, i)),
                    spec.Scale.z * Mathf.Lerp(0.16f, 0.34f, Hash01(spec.Seed ^ 59u, i)));
                AddLobe(builder, spec.Seed + (uint)i * 101u, center, radius, Mathf.RoundToInt(Mathf.Lerp(5f, 9f, q)), new Color32(96, 61, 24, 210));
            }
        }

        private static void AddMembraneFold(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int strips = Mathf.RoundToInt(Mathf.Lerp(3f, 7f, q));
            int segments = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, q));
            for (int s = 0; s < strips; s++)
            {
                float zOffset = Mathf.Lerp(-spec.Scale.z, spec.Scale.z, (s + 0.5f) / strips) * 0.42f;
                float width = spec.Scale.x * Mathf.Lerp(0.42f, 0.68f, Hash01(spec.Seed, s));
                Vector3 start = new Vector3(-width * 0.5f, HashSigned(spec.Seed ^ 13u, s) * 0.03f, zOffset);
                Vector3 end = new Vector3(width * 0.5f, HashSigned(spec.Seed ^ 29u, s) * 0.03f, -zOffset * 0.35f);
                AddFoldedSheet(builder, spec.Seed + (uint)s * 43u, start, end, segments, spec.Scale.y * 0.08f, new Color32(139, 82, 82, 185));
            }
        }

        private static void AddShardCluster(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int shards = Mathf.RoundToInt(Mathf.Lerp(6f, 17f, q));
            for (int i = 0; i < shards; i++)
            {
                float angle = i * Mathf.PI * 2f / shards + HashSigned(spec.Seed, i) * 0.28f;
                Vector3 baseCenter = new Vector3(Mathf.Cos(angle) * spec.Scale.x * 0.36f, -spec.Scale.y * 0.22f, Mathf.Sin(angle) * spec.Scale.z * 0.36f);
                float height = spec.Scale.y * Mathf.Lerp(0.45f, 1.22f, Hash01(spec.Seed ^ 71u, i));
                float radius = Mathf.Lerp(0.035f, 0.075f, Hash01(spec.Seed ^ 79u, i)) * Mathf.Lerp(0.9f, 1.35f, q);
                AddShard(builder, spec.Seed + (uint)i * 73u, baseCenter, height, radius, new Color32(205, 216, 216, 205));
            }
        }

        private static void AddSulfurNodules(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int nodules = Mathf.RoundToInt(Mathf.Lerp(7f, 18f, q));
            for (int i = 0; i < nodules; i++)
            {
                float angle = i * Mathf.PI * 2f / nodules + HashSigned(spec.Seed, i) * 0.42f;
                Vector3 center = new Vector3(
                    Mathf.Cos(angle) * spec.Scale.x * Mathf.Lerp(0.08f, 0.55f, Hash01(spec.Seed ^ 3u, i)),
                    -spec.Scale.y * 0.18f + Hash01(spec.Seed ^ 5u, i) * spec.Scale.y * 0.24f,
                    Mathf.Sin(angle) * spec.Scale.z * Mathf.Lerp(0.08f, 0.55f, Hash01(spec.Seed ^ 9u, i)));
                Vector3 radius = Vector3.one * Mathf.Lerp(0.055f, 0.12f, Hash01(spec.Seed ^ 15u, i));
                radius.y *= Mathf.Lerp(0.55f, 1.3f, Hash01(spec.Seed ^ 21u, i));
                AddLobe(builder, spec.Seed + (uint)i * 97u, center, radius, Mathf.RoundToInt(Mathf.Lerp(4f, 7f, q)), new Color32(188, 166, 42, 220));
            }

            AddLowBaseRubble(builder, spec, q, new Color32(55, 47, 38, 160));
        }

        private static void AddScrapPlate(MeshBuilder builder, ResourceSpec spec, float q)
        {
            int plates = Mathf.RoundToInt(Mathf.Lerp(2f, 5f, q));
            for (int i = 0; i < plates; i++)
            {
                float angle = HashSigned(spec.Seed, i) * 0.75f + i * 0.31f;
                Vector3 center = new Vector3(HashSigned(spec.Seed ^ 12u, i) * 0.08f, HashSigned(spec.Seed ^ 14u, i) * 0.035f, HashSigned(spec.Seed ^ 16u, i) * 0.06f);
                Vector3 half = new Vector3(spec.Scale.x * Mathf.Lerp(0.32f, 0.58f, Hash01(spec.Seed ^ 33u, i)), spec.Scale.y * 0.16f, spec.Scale.z * Mathf.Lerp(0.28f, 0.52f, Hash01(spec.Seed ^ 37u, i)));
                AddBentPlate(builder, spec.Seed + (uint)i * 131u, center, half, angle, Mathf.RoundToInt(Mathf.Lerp(4f, 9f, q)), new Color32(116, 124, 128, 220));
            }
        }

        private static void AddIrregularEllipsoid(MeshBuilder builder, ResourceSpec spec, int rings, int columns, float jaggedness, float veinMask, float q)
        {
            int[,] ids = new int[rings + 1, columns];
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float y = Mathf.Sin(phi);
                float ring = Mathf.Cos(phi);
                for (int c = 0; c < columns; c++)
                {
                    float u = c / (float)columns;
                    float theta = u * Mathf.PI * 2f;
                    float noise = 1f + HashSigned(spec.Seed + (uint)(r * 131 + c * 17), 0) * jaggedness;
                    float chip = 1f - Mathf.Pow(Mathf.Abs(HashSigned(spec.Seed ^ 0x6C8E9CF5u, r * 41 + c)), 4f) * Mathf.Lerp(0.08f, 0.18f, q);
                    Vector3 p = new Vector3(Mathf.Cos(theta) * ring * spec.Scale.x, y * spec.Scale.y, Mathf.Sin(theta) * ring * spec.Scale.z) * noise * chip;
                    Vector3 normal = p.normalized;
                    Color32 color = new Color32((byte)Mathf.RoundToInt(70 + 120 * veinMask * Mathf.Abs(HashSigned(spec.Seed, c))), 82, 76, 210);
                    ids[r, c] = builder.AddVertex(p, normal, new Vector2(u, v), color);
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int next = (c + 1) % columns;
                    builder.AddQuad(ids[r, c], ids[r, next], ids[r + 1, next], ids[r + 1, c]);
                }
            }
        }

        private static void AddRibbonFrond(MeshBuilder builder, uint seed, Vector3 root, Vector3 direction, float length, float width, int segments)
        {
            Vector3 side = Vector3.Cross(direction, Vector3.forward);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();

            int lastLeft = -1;
            int lastRight = -1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 center = root + direction * (length * t);
                center += side * (HashSigned(seed, i) * 0.025f) + Vector3.up * (Mathf.Sin(t * Mathf.PI * 2f + Hash01(seed, 7) * 3f) * 0.035f);
                float localWidth = width * Mathf.Lerp(1.15f, 0.22f, t) * (1f + HashSigned(seed ^ 31u, i) * 0.18f);
                Vector3 normal = Vector3.Cross(side, direction).normalized;
                Color32 color = new Color32(67, (byte)Mathf.RoundToInt(108 + t * 78), 58, (byte)Mathf.RoundToInt(80 + t * 170));
                int left = builder.AddVertex(center - side * localWidth, normal, new Vector2(0f, t), color);
                int right = builder.AddVertex(center + side * localWidth, normal, new Vector2(1f, t), color);
                if (lastLeft >= 0)
                    builder.AddQuad(lastLeft, lastRight, right, left);
                lastLeft = left;
                lastRight = right;
            }
        }

        private static void AddLobe(MeshBuilder builder, uint seed, Vector3 center, Vector3 radius, int segments, Color32 color)
        {
            int rings = Mathf.Max(3, segments);
            int columns = Mathf.Max(5, segments + 2);
            int[,] ids = new int[rings + 1, columns];
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float y = Mathf.Sin(phi);
                float ring = Mathf.Cos(phi);
                for (int c = 0; c < columns; c++)
                {
                    float u = c / (float)columns;
                    float theta = u * Mathf.PI * 2f;
                    float wobble = 1f + HashSigned(seed + (uint)(r * 53 + c * 97), 0) * 0.18f;
                    Vector3 p = center + new Vector3(Mathf.Cos(theta) * ring * radius.x, y * radius.y, Mathf.Sin(theta) * ring * radius.z) * wobble;
                    Vector3 n = (p - center).normalized;
                    ids[r, c] = builder.AddVertex(p, n, new Vector2(u, v), color);
                }
            }

            for (int r = 0; r < rings; r++)
                for (int c = 0; c < columns; c++)
                    builder.AddQuad(ids[r, c], ids[r, (c + 1) % columns], ids[r + 1, (c + 1) % columns], ids[r + 1, c]);
        }

        private static void AddFoldedSheet(MeshBuilder builder, uint seed, Vector3 start, Vector3 end, int segments, float thickness, Color32 color)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.right;
            int lastA = -1;
            int lastB = -1;
            int lastC = -1;
            int lastD = -1;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 center = Vector3.Lerp(start, end, t);
                center.y += Mathf.Sin(t * Mathf.PI * 2f + Hash01(seed, 0) * Mathf.PI) * thickness * 1.8f;
                float width = Mathf.Lerp(0.09f, 0.035f, Mathf.Abs(t - 0.5f) * 2f) * (1f + HashSigned(seed, i) * 0.25f);
                Vector3 normal = (Vector3.up + side * HashSigned(seed ^ 9u, i) * 0.32f).normalized;
                int a = builder.AddVertex(center - side * width, normal, new Vector2(0f, t), color);
                int b = builder.AddVertex(center + side * width, normal, new Vector2(1f, t), color);
                int c = builder.AddVertex(center - side * (width + thickness), -normal, new Vector2(0f, t), color);
                int d = builder.AddVertex(center + side * (width + thickness), -normal, new Vector2(1f, t), color);
                if (lastA >= 0)
                {
                    builder.AddQuad(lastA, lastB, b, a);
                    builder.AddQuad(lastD, lastC, c, d);
                    builder.AddQuad(lastA, a, c, lastC);
                    builder.AddQuad(lastB, lastD, d, b);
                }
                lastA = a;
                lastB = b;
                lastC = c;
                lastD = d;
            }
        }

        private static void AddShard(MeshBuilder builder, uint seed, Vector3 baseCenter, float height, float radius, Color32 color)
        {
            Vector3 tip = baseCenter + new Vector3(HashSigned(seed, 1) * radius, height, HashSigned(seed, 2) * radius);
            int sides = 3 + Mathf.RoundToInt(Hash01(seed, 3) * 2f);
            int[] baseIds = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides + HashSigned(seed, i) * 0.12f;
                Vector3 p = baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius * 0.72f);
                baseIds[i] = builder.AddVertex(p, Vector3.down, new Vector2(i / (float)sides, 0f), color);
            }

            int tipId = builder.AddVertex(tip, Vector3.up, new Vector2(0.5f, 1f), color);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                builder.AddTriangle(baseIds[i], baseIds[next], tipId);
            }

            for (int i = 1; i < sides - 1; i++)
                builder.AddTriangle(baseIds[0], baseIds[i], baseIds[i + 1]);
        }

        private static void AddLowBaseRubble(MeshBuilder builder, ResourceSpec spec, float q, Color32 color)
        {
            int rubble = Mathf.RoundToInt(Mathf.Lerp(4f, 10f, q));
            for (int i = 0; i < rubble; i++)
            {
                Vector3 center = new Vector3(HashSigned(spec.Seed ^ 81u, i) * spec.Scale.x * 0.54f, -spec.Scale.y * 0.35f, HashSigned(spec.Seed ^ 91u, i) * spec.Scale.z * 0.54f);
                AddLobe(builder, spec.Seed + (uint)i * 29u, center, Vector3.one * Mathf.Lerp(0.025f, 0.055f, Hash01(spec.Seed ^ 101u, i)), 4, color);
            }
        }

        private static void AddRaisedVein(MeshBuilder builder, Vector3 center, Vector3 axis, float length, float width, Color32 color)
        {
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.Cross(axis, Vector3.forward);
            side.Normalize();
            Vector3 a = center - axis * length * 0.5f;
            Vector3 b = center + axis * length * 0.5f;
            Vector3 lift = Vector3.up * width * 0.8f;
            int p0 = builder.AddVertex(a - side * width + lift, Vector3.up, Vector2.zero, color);
            int p1 = builder.AddVertex(a + side * width + lift, Vector3.up, Vector2.right, color);
            int p2 = builder.AddVertex(b + side * width * 0.55f + lift, Vector3.up, Vector2.one, color);
            int p3 = builder.AddVertex(b - side * width * 0.55f + lift, Vector3.up, Vector2.up, color);
            builder.AddQuad(p0, p1, p2, p3);
        }

        private static void AddBentPlate(MeshBuilder builder, uint seed, Vector3 center, Vector3 half, float angle, int segments, Color32 color)
        {
            Quaternion rotation = Quaternion.Euler(HashSigned(seed, 8) * 11f, angle * Mathf.Rad2Deg, HashSigned(seed, 10) * 18f);
            int lastTopL = -1;
            int lastTopR = -1;
            int lastBotL = -1;
            int lastBotR = -1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = Mathf.Lerp(-half.x, half.x, t);
                float bend = Mathf.Sin((t + Hash01(seed, 1) * 0.2f) * Mathf.PI) * half.y * 2.2f;
                float tear = HashSigned(seed ^ 49u, i) * half.z * 0.12f;
                Vector3 left = center + rotation * new Vector3(x, bend, -half.z + tear);
                Vector3 right = center + rotation * new Vector3(x, -bend * 0.35f, half.z - tear);
                Vector3 normal = (rotation * Vector3.up).normalized;
                int topL = builder.AddVertex(left + normal * half.y, normal, new Vector2(t, 0f), color);
                int topR = builder.AddVertex(right + normal * half.y, normal, new Vector2(t, 1f), color);
                int botL = builder.AddVertex(left - normal * half.y, -normal, new Vector2(t, 0f), color);
                int botR = builder.AddVertex(right - normal * half.y, -normal, new Vector2(t, 1f), color);
                if (lastTopL >= 0)
                {
                    builder.AddQuad(lastTopL, lastTopR, topR, topL);
                    builder.AddQuad(lastBotR, lastBotL, botL, botR);
                    builder.AddQuad(lastTopL, topL, botL, lastBotL);
                    builder.AddQuad(lastTopR, lastBotR, botR, topR);
                }
                lastTopL = topL;
                lastTopR = topR;
                lastBotL = botL;
                lastBotR = botR;
            }
        }

        private static void ValidateMesh(ResourceSpec spec, Mesh mesh, HashSet<string> silhouetteSignatures)
        {
            if (string.IsNullOrWhiteSpace(spec.ResourceId) || string.IsNullOrWhiteSpace(spec.DataOwnerId) || string.IsNullOrWhiteSpace(spec.SourceComment))
                throw new InvalidOperationException("Resource mesh spec is missing identity metadata.");

            if (mesh == null)
                throw new InvalidOperationException(spec.ResourceId + " produced null mesh.");

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || vertices.Length == 0)
                throw new InvalidOperationException(spec.ResourceId + " produced no vertices.");
            if (triangles == null || triangles.Length == 0 || triangles.Length % 3 != 0)
                throw new InvalidOperationException(spec.ResourceId + " produced invalid triangle indices.");

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!IsFinite(vertices[i]))
                    throw new InvalidOperationException(spec.ResourceId + " produced non-finite vertex at " + i.ToString(CultureInfo.InvariantCulture));
                bounds.Encapsulate(vertices[i]);
            }

            if (!IsFinite(bounds.center) || !IsFinite(bounds.size) || bounds.size.sqrMagnitude < 0.0001f)
                throw new InvalidOperationException(spec.ResourceId + " produced invalid bounds.");

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                    throw new InvalidOperationException(spec.ResourceId + " produced out-of-range triangle index.");

                float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude;
                if (!IsFinite(area) || area <= ValidationAreaEpsilon)
                    throw new InvalidOperationException(spec.ResourceId + " produced degenerate triangle.");
            }

            string signature = ResolveSilhouetteSignature(mesh, spec.Kind);
            if (!silhouetteSignatures.Add(signature))
                throw new InvalidOperationException(spec.ResourceId + " silhouette signature duplicates another resource: " + signature);
        }

        private static string ResolveSilhouetteSignature(Mesh mesh, GeometryKind kind)
        {
            Bounds b = mesh.bounds;
            int vx = Mathf.RoundToInt(b.size.x * 100f);
            int vy = Mathf.RoundToInt(b.size.y * 100f);
            int vz = Mathf.RoundToInt(b.size.z * 100f);
            int triBucket = Mathf.RoundToInt(mesh.triangles.Length / 18f);
            return kind + ":" + vx.ToString(CultureInfo.InvariantCulture) + ":" + vy.ToString(CultureInfo.InvariantCulture) + ":" + vz.ToString(CultureInfo.InvariantCulture) + ":" + triBucket.ToString(CultureInfo.InvariantCulture);
        }

        private static void SaveMesh(Mesh sourceMesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(sourceMesh, existing);
                existing.name = sourceMesh.name;
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(sourceMesh);
                return;
            }

            AssetDatabase.CreateAsset(sourceMesh, path);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float HashSigned(uint seed, int index)
        {
            return Hash01(seed, index) * 2f - 1f;
        }

        private static float Hash01(uint seed, int index)
        {
            uint h = seed ^ unchecked((uint)index * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }

        private readonly struct ResourceSpec
        {
            public ResourceSpec(string resourceId, string dataOwnerId, GeometryKind kind, uint seed, Vector3 scale, string sourceComment)
            {
                ResourceId = resourceId;
                DataOwnerId = dataOwnerId;
                Kind = kind;
                Seed = seed;
                Scale = scale;
                SourceComment = sourceComment;
            }

            public readonly string ResourceId;
            public readonly string DataOwnerId;
            public readonly GeometryKind Kind;
            public readonly uint Seed;
            public readonly Vector3 Scale;
            public readonly string SourceComment;
        }

        private enum GeometryKind
        {
            OreChunk,
            FrondBundle,
            ResinClump,
            MembraneFold,
            ShardCluster,
            SilverOreChunk,
            SulfurNodules,
            ScrapPlate
        }

        private sealed class MeshBuilder
        {
            private readonly string _name;
            private readonly List<Vector3> _vertices = new List<Vector3>(512);
            private readonly List<Vector3> _normals = new List<Vector3>(512);
            private readonly List<Vector2> _uvs = new List<Vector2>(512);
            private readonly List<Color32> _colors = new List<Color32>(512);
            private readonly List<int> _triangles = new List<int>(1536);

            public MeshBuilder(string name)
            {
                _name = name;
            }

            public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Color32 color)
            {
                if (normal.sqrMagnitude < 0.0001f || !IsFinite(normal))
                    normal = Vector3.up;
                normal.Normalize();
                int index = _vertices.Count;
                _vertices.Add(position);
                _normals.Add(normal);
                _uvs.Add(uv);
                _colors.Add(color);
                return index;
            }

            public void AddTriangle(int a, int b, int c)
            {
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            public void AddQuad(int a, int b, int c, int d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh
                {
                    name = _name,
                    indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0, true);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

#endif

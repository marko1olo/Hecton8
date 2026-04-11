// ============================================================================
// HECTON-8 — WorldGenerativeGeologyMeshBuilder.cs
// Процедурный генератор реальной геологической геометрии.
//
// РОЛЬ:
//   Единственный owner формы для всей геологии проекта.
//   Используется и editor authoring, и runtime generation.
//   Один hash = одна форма. Детерминировано.
//
// КАТЕГОРИИ:
//   RockFloor       — мелкие камни (10 вариантов)
//   RockCluster     — средние кластеры (8 вариантов)
//   RockShelf       — уступы / cliff shelves (6 вариантов)
//   RockArch        — большие арки (6 вариантов)
//   CaveEntrance    — входы в пещеры (5 вариантов)
//   LandmarkSpire   — высокие шпили (5 вариантов)
//
// ZERO UV: вся геометрия под трипланарный шейдер, UV не нужны.
// LOD: LOD0 полный, LOD1 упрощённый, LOD2 силуэт.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    public enum GeologyArchetype
    {
        RockFloor       = 0,
        RockCluster     = 1,
        RockShelf       = 2,
        RockArch        = 3,
        CaveEntrance    = 4,
        LandmarkSpire   = 5
    }

    public sealed class GeologyMeshBundle
    {
        public Mesh Lod0;
        public Mesh Lod1;
        public Mesh Lod2;
        public Mesh Collider;
        public Bounds Bounds;
    }

    public static class WorldGenerativeGeologyMeshBuilder
    {
        // ── Public entry point ────────────────────────────────────

        /// <summary>
        /// Генерирует полный LOD-набор для указанного архетипа.
        /// stableHash определяет вариант формы детерминированно.
        /// scale — мировой масштаб (1.0 = стандарт).
        /// </summary>
        public static GeologyMeshBundle Build(
            GeologyArchetype archetype,
            int stableHash,
            float scale = 1f)
        {
            // Нормализуем hash чтобы не было отрицательных
            int h = Mathf.Abs(stableHash);

            GeologyMeshBundle bundle = archetype switch
            {
                GeologyArchetype.RockFloor    => BuildRockFloor(h, scale),
                GeologyArchetype.RockCluster  => BuildRockCluster(h, scale),
                GeologyArchetype.RockShelf    => BuildRockShelf(h, scale),
                GeologyArchetype.RockArch     => BuildRockArch(h, scale),
                GeologyArchetype.CaveEntrance => BuildCaveEntrance(h, scale),
                GeologyArchetype.LandmarkSpire => BuildLandmarkSpire(h, scale),
                _                             => BuildRockCluster(h, scale)
            };

            return bundle;
        }

        // ── RockFloor — мелкие камни ──────────────────────────────

        private static GeologyMeshBundle BuildRockFloor(int h, float s)
        {
            // 10 вариантов мелких камней
            int variant = h % 10;
            float w = s * Mathf.Lerp(0.4f, 1.1f, (variant * 0.11f) % 1f);
            float ht = s * Mathf.Lerp(0.2f, 0.65f, (variant * 0.17f) % 1f);
            float d = s * Mathf.Lerp(0.35f, 0.9f, (variant * 0.13f) % 1f);

            // Базовая форма — деформированный куб с шумом
            Mesh lod0 = BuildRockFloorMesh(w, ht, d, h, 3, 0.22f, true);
            Mesh lod1 = BuildRockFloorMesh(w, ht, d, h, 2, 0.14f, true);
            Mesh lod2 = BuildRockFloorMesh(w, ht, d, h, 1, 0.06f, false);
            Mesh col  = BuildDeformedBox(w * 1.05f, ht * 0.7f, d * 1.05f, h, 1, 0f);

            SetMeshName(lod0, $"RockFloor_v{variant}_LOD0");
            SetMeshName(lod1, $"RockFloor_v{variant}_LOD1");
            SetMeshName(lod2, $"RockFloor_v{variant}_LOD2");
            SetMeshName(col,  $"RockFloor_v{variant}_COL");

            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, ht * 0.35f, 0), new Vector3(w * 1.35f, ht, d * 1.35f))
            };
        }

        // ── RockCluster — средние кластеры ────────────────────────

        private static GeologyMeshBundle BuildRockCluster(int h, float s)
        {
            int variant = h % 10;
            // Кластер из 2-4 масс
            int count = 2 + (variant % 3);
            float baseSize = s * Mathf.Lerp(1.2f, 2.8f, (variant * 0.14f) % 1f);
            Vector3[] offsets = new Vector3[count];
            float[] sizes = new float[count];
            float[] heights = new float[count];

            List<Vector3> verts0 = new List<Vector3>(256);
            List<int> tris0 = new List<int>(512);
            List<Vector3> verts1 = new List<Vector3>(128);
            List<int> tris1 = new List<int>(256);

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (h * 0.37f);
                float radius = baseSize * Mathf.Lerp(0.3f, 0.7f, ((h + i * 31) * 0.19f) % 1f);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                float sz = baseSize * Mathf.Lerp(0.55f, 1.0f, ((h + i * 17) * 0.23f) % 1f);
                float szY = sz * Mathf.Lerp(0.6f, 1.1f, ((h + i * 43) * 0.31f) % 1f);
                offsets[i] = offset;
                sizes[i] = sz;
                heights[i] = szY;

                AppendDeformedEllipsoid(verts0, tris0, offset + new Vector3(0f, szY * 0.1f, 0f),
                    sz, szY, sz * Mathf.Lerp(0.82f, 1.18f, ((h + i * 61) * 0.09f) % 1f),
                    h + i * 7, 4 + i, 6 + i * 2, 0.24f, 0.42f);
                AppendDeformedEllipsoid(verts1, tris1, offset + new Vector3(0f, szY * 0.1f, 0f),
                    sz, szY, sz * Mathf.Lerp(0.82f, 1.18f, ((h + i * 61) * 0.09f) % 1f),
                    h + i * 7, 3 + (i % 2), 5 + i, 0.13f, 0.45f);
            }

            for (int i = 1; i < count; i++)
            {
                Vector3 from = offsets[i - 1];
                Vector3 to = offsets[i];
                Vector3 mid = (from + to) * 0.5f;
                float bridgeW = Vector3.Distance(from, to) + Mathf.Min(sizes[i - 1], sizes[i]) * 0.35f;
                float bridgeH = Mathf.Min(heights[i - 1], heights[i]) * 0.3f;
                float bridgeD = Mathf.Min(sizes[i - 1], sizes[i]) * 0.5f;
                Vector3 bridgeCenter = new Vector3(mid.x, bridgeH * 0.55f, mid.z);
                AppendDeformedEllipsoid(verts0, tris0, bridgeCenter,
                    bridgeW, bridgeH, bridgeD,
                    h + 71 + i * 9, 4, 7, 0.10f, 0.35f);
                AppendDeformedEllipsoid(verts1, tris1, bridgeCenter,
                    bridgeW, bridgeH, bridgeD,
                    h + 71 + i * 9, 3, 5, 0.05f, 0.35f);
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 top = offsets[i] + new Vector3(
                    Mathf.Sin(h + i * 1.17f) * sizes[i] * 0.18f,
                    heights[i] * 0.55f,
                    Mathf.Cos(h + i * 1.31f) * sizes[i] * 0.18f);
                AppendDeformedEllipsoid(verts0, tris0, top,
                    sizes[i] * 0.42f, heights[i] * 0.34f, sizes[i] * 0.30f,
                    h + 131 + i * 11, 3, 5, 0.17f, 0.2f);
            }

            Mesh lod0 = BuildMeshFromLists(verts0, tris0);
            Mesh lod1 = BuildMeshFromLists(verts1, tris1);
            Mesh lod2 = BuildDeformedEllipsoid(baseSize * 1.15f, baseSize * 0.82f, baseSize * 1.05f, h, 3, 5, 0.08f, 0.45f);
            Mesh col  = BuildDeformedBox(baseSize * 1.0f, baseSize * 0.75f, baseSize * 1.0f, h, 1, 0f);

            SetMeshName(lod0, $"RockCluster_v{variant}_LOD0");
            SetMeshName(lod1, $"RockCluster_v{variant}_LOD1");
            SetMeshName(lod2, $"RockCluster_v{variant}_LOD2");
            SetMeshName(col,  $"RockCluster_v{variant}_COL");

            float bh = baseSize * 0.9f;
            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, bh * 0.5f, 0), new Vector3(baseSize * 2.2f, bh, baseSize * 2.2f))
            };
        }

        // ── RockShelf — уступы / cliff shelves ────────────────────

        private static GeologyMeshBundle BuildRockShelf(int h, float s)
        {
            int variant = h % 8;
            float width  = s * Mathf.Lerp(5f, 12f, (variant * 0.18f) % 1f);
            float height = s * Mathf.Lerp(2.5f, 6f, (variant * 0.22f) % 1f);
            float depth  = s * Mathf.Lerp(2f, 5f, (variant * 0.15f) % 1f);
            float overhang = s * Mathf.Lerp(0.5f, 2.5f, (variant * 0.27f) % 1f);

            Mesh lod0 = BuildShelfMesh(width, height, depth, overhang, h, 5, 0.18f);
            Mesh lod1 = BuildShelfMesh(width, height, depth, overhang, h, 3, 0.10f);
            Mesh lod2 = BuildShelfMesh(width, height, depth, overhang * 0.5f, h, 2, 0.05f);
            Mesh col  = BuildShelfMesh(width * 0.95f, height, depth * 0.9f, overhang * 0.7f, h, 2, 0f);

            SetMeshName(lod0, $"RockShelf_v{variant}_LOD0");
            SetMeshName(lod1, $"RockShelf_v{variant}_LOD1");
            SetMeshName(lod2, $"RockShelf_v{variant}_LOD2");
            SetMeshName(col,  $"RockShelf_v{variant}_COL");

            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, height * 0.5f, 0), new Vector3(width, height, depth + overhang))
            };
        }

        // ── RockArch — большие арки ───────────────────────────────

        private static GeologyMeshBundle BuildRockArch(int h, float s)
        {
            int variant = h % 6;
            float span   = s * Mathf.Lerp(8f, 18f, (variant * 0.19f) % 1f);
            float height = s * Mathf.Lerp(5f, 12f, (variant * 0.23f) % 1f);
            float thick  = s * Mathf.Lerp(1.2f, 3.0f, (variant * 0.17f) % 1f);
            float asym   = Mathf.Lerp(-0.15f, 0.15f, ((h * 0.41f) % 1f));

            Mesh lod0 = BuildArchMesh(span, height, thick, asym, h, 6, 0.20f);
            Mesh lod1 = BuildArchMesh(span, height, thick, asym, h, 4, 0.12f);
            Mesh lod2 = BuildArchMesh(span, height, thick * 1.1f, asym * 0.5f, h, 2, 0.06f);
            Mesh col  = BuildArchCollider(span, height, thick * 1.2f);

            SetMeshName(lod0, $"RockArch_v{variant}_LOD0");
            SetMeshName(lod1, $"RockArch_v{variant}_LOD1");
            SetMeshName(lod2, $"RockArch_v{variant}_LOD2");
            SetMeshName(col,  $"RockArch_v{variant}_COL");

            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, height * 0.5f, 0), new Vector3(span + thick, height + thick, thick * 2.5f))
            };
        }

        // ── CaveEntrance — входы в пещеры ─────────────────────────

        private static GeologyMeshBundle BuildCaveEntrance(int h, float s)
        {
            int variant = h % 6;
            float w  = s * Mathf.Lerp(6f, 14f, (variant * 0.21f) % 1f);
            float ht = s * Mathf.Lerp(4f, 10f, (variant * 0.19f) % 1f);
            float d  = s * Mathf.Lerp(3f, 7f,  (variant * 0.17f) % 1f);
            float openW = w * Mathf.Lerp(0.35f, 0.55f, ((h * 0.37f) % 1f));
            float openH = ht * Mathf.Lerp(0.40f, 0.65f, ((h * 0.43f) % 1f));

            Mesh lod0 = BuildCaveEntranceMesh(w, ht, d, openW, openH, h, 6, 0.22f);
            Mesh lod1 = BuildCaveEntranceMesh(w, ht, d, openW, openH, h, 4, 0.12f);
            Mesh lod2 = BuildCaveEntranceMesh(w, ht, d, openW * 1.05f, openH * 1.05f, h, 2, 0.06f);
            // Collider сохраняет проём — не закрывает вход
            Mesh col  = BuildCaveEntranceCollider(w, ht, d, openW, openH);

            SetMeshName(lod0, $"CaveEntrance_v{variant}_LOD0");
            SetMeshName(lod1, $"CaveEntrance_v{variant}_LOD1");
            SetMeshName(lod2, $"CaveEntrance_v{variant}_LOD2");
            SetMeshName(col,  $"CaveEntrance_v{variant}_COL");

            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, ht * 0.5f, 0), new Vector3(w, ht, d))
            };
        }

        // ── LandmarkSpire — высокие шпили ─────────────────────────

        private static GeologyMeshBundle BuildLandmarkSpire(int h, float s)
        {
            int variant = h % 6;
            float baseW  = s * Mathf.Lerp(2.5f, 5f,  (variant * 0.22f) % 1f);
            float totalH = s * Mathf.Lerp(10f,  22f,  (variant * 0.19f) % 1f);
            float taper  = Mathf.Lerp(0.08f, 0.22f, ((h * 0.31f) % 1f));
            int   secondaryCount = 1 + (variant % 3);

            Mesh lod0 = BuildSpireMesh(baseW, totalH, taper, secondaryCount, h, 6, 0.20f);
            Mesh lod1 = BuildSpireMesh(baseW, totalH, taper, secondaryCount, h, 4, 0.12f);
            Mesh lod2 = BuildSpireMesh(baseW, totalH * 0.95f, taper * 1.1f, 0, h, 2, 0.06f);
            Mesh col  = BuildDeformedBox(baseW * 0.9f, totalH * 0.85f, baseW * 0.9f, h, 1, 0f);

            SetMeshName(lod0, $"LandmarkSpire_v{variant}_LOD0");
            SetMeshName(lod1, $"LandmarkSpire_v{variant}_LOD1");
            SetMeshName(lod2, $"LandmarkSpire_v{variant}_LOD2");
            SetMeshName(col,  $"LandmarkSpire_v{variant}_COL");

            return new GeologyMeshBundle
            {
                Lod0 = lod0, Lod1 = lod1, Lod2 = lod2, Collider = col,
                Bounds = new Bounds(new Vector3(0, totalH * 0.5f, 0), new Vector3(baseW * 2f, totalH, baseW * 2f))
            };
        }

        // ── Mesh builders — реальная геометрия ────────────────────

        /// <summary>
        /// Деформированный параллелепипед с шумовым смещением вершин.
        /// subdivisions: количество делений по каждой оси (1-5).
        /// noiseAmp: амплитуда шума (0 = чистый куб).
        /// </summary>
        private static Mesh BuildDeformedBox(
            float w, float h, float d,
            int seed, int subdivisions, float noiseAmp)
        {
            List<Vector3> verts = new List<Vector3>(256);
            List<int> tris = new List<int>(512);
            AppendDeformedBox(verts, tris, Vector3.zero, w, h, d, seed, subdivisions, noiseAmp);
            return BuildMeshFromLists(verts, tris);
        }

        private static void AppendDeformedBox(
            List<Vector3> verts, List<int> tris,
            Vector3 center,
            float w, float h, float d,
            int seed, int subdivisions, float noiseAmp)
        {
            int sub = Mathf.Clamp(subdivisions, 1, 5);
            int baseIndex = verts.Count;

            // Генерируем вершины по 6 граням с subdivision
            // Каждая грань: (sub+1)*(sub+1) вершин
            // Грани: +X, -X, +Y, -Y, +Z, -Z
            Vector3 half = new Vector3(w * 0.5f, h * 0.5f, d * 0.5f);

            // Для каждой грани строим сетку
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 0); // +X
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 1); // -X
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 2); // +Y
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 3); // -Y
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 4); // +Z
            BuildBoxFace(verts, tris, center, half, seed, noiseAmp, sub, 5); // -Z
        }

        private static void BuildBoxFace(
            List<Vector3> verts, List<int> tris,
            Vector3 center, Vector3 half,
            int seed, float noiseAmp, int sub, int faceIndex)
        {
            int baseIdx = verts.Count;
            int n = sub + 1;

            // Определяем оси грани
            Vector3 normal, tangent, bitangent;
            float faceOffset;
            GetFaceAxes(faceIndex, half, out normal, out tangent, out bitangent, out faceOffset);

            for (int j = 0; j <= sub; j++)
            {
                for (int i = 0; i <= sub; i++)
                {
                    float u = i / (float)sub - 0.5f;
                    float v = j / (float)sub - 0.5f;

                    Vector3 pos = center
                        + normal * faceOffset
                        + tangent * (u * 2f)
                        + bitangent * (v * 2f);

                    // Шумовое смещение вдоль нормали
                    if (noiseAmp > 0f)
                    {
                        float n1 = Noise3D(pos * 1.7f + Vector3.one * seed * 0.13f);
                        float n2 = Noise3D(pos * 3.1f + Vector3.one * seed * 0.27f) * 0.4f;
                        float n3 = Noise3D(pos * 5.3f + Vector3.one * seed * 0.41f) * 0.15f;
                        float displacement = (n1 + n2 + n3) * noiseAmp;
                        pos += normal * displacement;
                    }

                    verts.Add(pos);
                }
            }

            // Треугольники
            for (int j = 0; j < sub; j++)
            {
                for (int i = 0; i < sub; i++)
                {
                    int a = baseIdx + j * n + i;
                    int b = baseIdx + j * n + i + 1;
                    int c = baseIdx + (j + 1) * n + i;
                    int d2 = baseIdx + (j + 1) * n + i + 1;

                    // Нормаль грани определяет порядок обхода
                    if (faceIndex % 2 == 0)
                    {
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d2);
                    }
                    else
                    {
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d2); tris.Add(c);
                    }
                }
            }
        }

        private static void GetFaceAxes(
            int faceIndex, Vector3 half,
            out Vector3 normal, out Vector3 tangent, out Vector3 bitangent,
            out float faceOffset)
        {
            switch (faceIndex)
            {
                case 0: normal = Vector3.right;   tangent = Vector3.forward; bitangent = Vector3.up;    faceOffset = half.x; break;
                case 1: normal = Vector3.left;    tangent = Vector3.back;    bitangent = Vector3.up;    faceOffset = half.x; break;
                case 2: normal = Vector3.up;      tangent = Vector3.right;   bitangent = Vector3.forward; faceOffset = half.y; break;
                case 3: normal = Vector3.down;    tangent = Vector3.left;    bitangent = Vector3.forward; faceOffset = half.y; break;
                case 4: normal = Vector3.forward; tangent = Vector3.right;   bitangent = Vector3.up;    faceOffset = half.z; break;
                default: normal = Vector3.back;   tangent = Vector3.left;    bitangent = Vector3.up;    faceOffset = half.z; break;
            }
            // Масштабируем tangent/bitangent под размер грани
            tangent   *= (faceIndex < 2 ? half.z : (faceIndex < 4 ? half.x : half.x));
            bitangent *= (faceIndex < 2 ? half.y : (faceIndex < 4 ? half.z : half.y));
        }

        // ── Shelf mesh ────────────────────────────────────────────

        private static Mesh BuildShelfMesh(
            float width, float height, float depth, float overhang,
            int seed, int sub, float noiseAmp)
        {
            List<Vector3> verts = new List<Vector3>(512);
            List<int> tris = new List<int>(1024);

            // Основная стена
            Mesh wallA = BuildDeformedEllipsoid(width * 0.62f, height * 0.82f, depth * 0.56f, seed, 4 + sub, 7 + sub, noiseAmp * 0.9f, 0.15f);
            Mesh wallB = BuildDeformedEllipsoid(width * 0.55f, height * 0.9f, depth * 0.52f, seed + 9, 4 + sub, 7 + sub, noiseAmp * 0.9f, 0.15f);
            Mesh wallC = BuildDeformedEllipsoid(width * 0.48f, height * 0.76f, depth * 0.48f, seed + 17, 4 + sub, 6 + sub, noiseAmp * 0.85f, 0.2f);
            AppendMeshTransformed(verts, tris, wallA, new Vector3(-width * 0.18f, height * 0.46f, -depth * 0.28f), Quaternion.Euler(4f, -12f, -8f), Vector3.one);
            AppendMeshTransformed(verts, tris, wallB, new Vector3(width * 0.14f, height * 0.5f, -depth * 0.18f), Quaternion.Euler(-6f, 10f, 6f), Vector3.one);
            AppendMeshTransformed(verts, tris, wallC, new Vector3(0f, height * 0.62f, -depth * 0.42f), Quaternion.Euler(0f, 22f, -4f), Vector3.one);

            // Выступающий shelf
            Mesh shelfLip = BuildDeformedEllipsoid(width * 0.92f, height * 0.24f, depth * 0.42f + overhang, seed + 7, 4 + sub, 8 + sub, noiseAmp * 0.7f, 0.25f);
            AppendMeshTransformed(verts, tris, shelfLip,
                new Vector3(0f, height * 0.84f, depth * 0.24f + overhang * 0.48f),
                Quaternion.Euler(-5f, 0f, 0f), Vector3.one);

            // Нижний уступ
            Mesh lowerLedge = BuildDeformedEllipsoid(width * 0.72f, height * 0.16f, depth * 0.34f, seed + 13, 3 + sub, 6 + sub, noiseAmp * 0.45f, 0.28f);
            AppendMeshTransformed(verts, tris, lowerLedge,
                new Vector3(0f, height * 0.34f, depth * 0.12f),
                Quaternion.Euler(0f, seed % 17 - 8f, 0f), Vector3.one);

            // Слоистость — горизонтальные полосы
            int layers = 2 + (seed % 3);
            for (int i = 0; i < layers; i++)
            {
                float layerY = height * (0.2f + i * 0.22f);
                float layerThick = height * 0.04f;
                float layerProtrude = depth * 0.08f * (1f + (seed + i * 11) % 3 * 0.3f);
                AppendDeformedEllipsoid(verts, tris,
                    new Vector3(0, layerY, layerProtrude),
                    width * 0.82f, layerThick * 1.3f, depth * 0.48f + layerProtrude,
                    seed + i * 17, 3, 6, noiseAmp * 0.18f, 0.4f);
            }

            float buttressW = width * 0.16f;
            float buttressH = height * 0.78f;
            float buttressD = depth * 0.7f;
            AppendDeformedEllipsoid(verts, tris, new Vector3(-width * 0.38f, buttressH * 0.45f, -depth * 0.15f),
                buttressW, buttressH, buttressD, seed + 31, 4 + sub, 6 + sub, noiseAmp * 0.55f, 0.18f);
            AppendDeformedEllipsoid(verts, tris, new Vector3(width * 0.38f, buttressH * 0.5f, -depth * 0.1f),
                buttressW * 1.1f, buttressH * 0.92f, buttressD, seed + 37, 4 + sub, 6 + sub, noiseAmp * 0.55f, 0.18f);

            int ribs = 2 + (seed % 2);
            for (int i = 0; i < ribs; i++)
            {
                float t = ribs == 1 ? 0.5f : i / (float)(ribs - 1);
                float x = Mathf.Lerp(-width * 0.28f, width * 0.28f, t);
                AppendDeformedEllipsoid(verts, tris, new Vector3(x, height * 0.7f, depth * 0.34f + overhang * 0.22f),
                    width * 0.13f, height * 0.24f, depth * 0.12f, seed + 53 + i * 7, 3, 5, noiseAmp * 0.25f, 0.2f);
            }

            int fractures = 2 + (seed % 3);
            for (int i = 0; i < fractures; i++)
            {
                float fx = Mathf.Lerp(-width * 0.32f, width * 0.32f, ((seed + i * 13) * 0.19f) % 1f);
                float fh = height * Mathf.Lerp(0.3f, 0.72f, ((seed + i * 7) * 0.23f) % 1f);
                AppendDeformedBox(verts, tris, new Vector3(fx, fh, -depth * 0.42f),
                    width * 0.045f, height * 0.32f, depth * 0.22f, seed + 71 + i * 13, 1, noiseAmp * 0.2f);
            }

            int outcropCount = 2 + (seed % 2);
            for (int i = 0; i < outcropCount; i++)
            {
                float t = outcropCount == 1 ? 0.5f : i / (float)(outcropCount - 1);
                Vector3 outcropPos = new Vector3(
                    Mathf.Lerp(-width * 0.24f, width * 0.24f, t),
                    height * Mathf.Lerp(0.48f, 0.8f, ((seed + i * 37) * 0.09f) % 1f),
                    depth * Mathf.Lerp(0.12f, 0.38f, ((seed + i * 41) * 0.11f) % 1f) + overhang * 0.24f);
                Mesh outcrop = BuildDeformedEllipsoid(
                    width * 0.22f,
                    height * 0.16f,
                    depth * 0.24f,
                    seed + 131 + i * 19,
                    4 + Mathf.Max(1, sub - 1),
                    5 + sub,
                    noiseAmp * 0.4f,
                    0.12f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-18f, 16f, ((seed + i * 43) * 0.13f) % 1f),
                    Mathf.Lerp(-32f, 32f, ((seed + i * 47) * 0.07f) % 1f),
                    Mathf.Lerp(-12f, 12f, ((seed + i * 53) * 0.05f) % 1f));
                AppendMeshTransformed(verts, tris, outcrop, outcropPos, tilt, Vector3.one);
            }

            return BuildMeshFromLists(verts, tris);
        }

        // ── Arch mesh ─────────────────────────────────────────────

        private static Mesh BuildArchMesh(
            float span, float height, float thick, float asym,
            int seed, int sub, float noiseAmp)
        {
            List<Vector3> verts = new List<Vector3>(1024);
            List<int> tris = new List<int>(2048);

            // Левая опора
            float leftX = -span * 0.5f + asym * span * 0.1f;
            float rightX = span * 0.5f + asym * span * 0.1f;
            float legH = height * 0.55f;
            float legW = thick * Mathf.Lerp(0.9f, 1.3f, ((seed * 0.23f) % 1f));

            AppendDeformedEllipsoid(verts, tris, new Vector3(leftX, legH * 0.54f, 0f),
                legW, legH, thick * 1.18f, seed, 4 + sub, 6 + sub, noiseAmp * 0.8f, 0.2f);

            // Правая опора
            AppendDeformedEllipsoid(verts, tris, new Vector3(rightX, legH * 0.52f, 0f),
                legW * 1.02f, legH, thick * 1.12f, seed + 5, 4 + sub, 6 + sub, noiseAmp * 0.8f, 0.2f);

            // Свод арки — строим из сегментов по дуге
            int archSegs = Mathf.Max(4, sub * 2);
            BuildArchBridge(verts, tris, leftX, rightX, height, thick, seed, noiseAmp, archSegs);

            // Дополнительные массы у основания
            AppendDeformedEllipsoid(verts, tris, new Vector3(leftX * 0.7f, legH * 0.16f, thick * 0.22f),
                legW * 1.55f, legH * 0.34f, thick * 1.55f, seed + 11, 4, 6, noiseAmp * 0.5f, 0.25f);
            AppendDeformedEllipsoid(verts, tris, new Vector3(rightX * 0.7f, legH * 0.16f, -thick * 0.22f),
                legW * 1.45f, legH * 0.34f, thick * 1.55f, seed + 19, 4, 6, noiseAmp * 0.5f, 0.25f);

            // Трещины и ребра на своде
            int ridgeCount = 2 + (seed % 3);
            for (int i = 0; i < ridgeCount; i++)
            {
                float t = (i + 1f) / (ridgeCount + 1f);
                float rx = Mathf.Lerp(leftX, rightX, t);
                float ry = height - (span * 0.5f - Mathf.Abs(rx)) * 0.15f;
                AppendDeformedEllipsoid(verts, tris, new Vector3(rx, ry + thick * 0.3f, 0f),
                    thick * 0.28f, thick * 0.34f, thick * 1.18f, seed + i * 23, 3, 5, noiseAmp * 0.22f, 0.1f);
            }

            int crownCount = 2 + (seed % 2);
            for (int i = 0; i < crownCount; i++)
            {
                float t = (i + 1f) / (crownCount + 1f);
                float rx = Mathf.Lerp(leftX, rightX, t);
                float ry = height + thick * Mathf.Lerp(0.28f, 0.52f, ((seed + i * 17) * 0.21f) % 1f);
                AppendDeformedBox(verts, tris, new Vector3(rx, ry, Mathf.Sin(seed + i) * thick * 0.18f),
                    thick * 0.5f, thick * 0.55f, thick * 0.72f, seed + 61 + i * 13, 2, noiseAmp * 0.4f);
            }

            int underTeeth = 2 + (seed % 3);
            for (int i = 0; i < underTeeth; i++)
            {
                float t = (i + 1f) / (underTeeth + 1f);
                float rx = Mathf.Lerp(leftX * 0.7f, rightX * 0.7f, t);
                float ry = height * Mathf.Lerp(0.58f, 0.72f, ((seed + i * 19) * 0.17f) % 1f);
                AppendDeformedBox(verts, tris, new Vector3(rx, ry, 0f),
                    thick * 0.2f, thick * 0.45f, thick * 0.42f, seed + 83 + i * 11, 1, noiseAmp * 0.35f);
            }

            int flankCount = 2 + (seed % 2);
            for (int i = 0; i < flankCount; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 flankPos = new Vector3(
                    Mathf.Lerp(leftX * 0.78f, rightX * 0.78f, ((seed + i * 29) * 0.13f) % 1f),
                    height * Mathf.Lerp(0.52f, 0.78f, ((seed + i * 31) * 0.17f) % 1f),
                    side * thick * Mathf.Lerp(0.42f, 0.68f, ((seed + i * 37) * 0.09f) % 1f));
                Mesh flank = BuildDeformedEllipsoid(
                    thick * 0.34f,
                    thick * 0.7f,
                    thick * 0.56f,
                    seed + 111 + i * 17,
                    4,
                    5,
                    noiseAmp * 0.32f,
                    0.08f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-14f, 18f, ((seed + i * 41) * 0.11f) % 1f),
                    side < 0f ? -28f : 28f,
                    Mathf.Lerp(-22f, 22f, ((seed + i * 43) * 0.13f) % 1f));
                AppendMeshTransformed(verts, tris, flank, flankPos, tilt, Vector3.one);
            }

            return BuildMeshFromLists(verts, tris);
        }

        private static void BuildArchBridge(
            List<Vector3> verts, List<int> tris,
            float leftX, float rightX, float height, float thick,
            int seed, float noiseAmp, int segments)
        {
            float midX = (leftX + rightX) * 0.5f;
            float halfSpan = (rightX - leftX) * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1f) / (float)segments;

                float a0 = Mathf.PI * t0;
                float a1 = Mathf.PI * t1;

                float x0 = midX + Mathf.Cos(a0) * halfSpan;
                float y0 = height - Mathf.Sin(a0) * halfSpan * 0.55f;
                float x1 = midX + Mathf.Cos(a1) * halfSpan;
                float y1 = height - Mathf.Sin(a1) * halfSpan * 0.55f;

                Vector3 center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0);
                float segLen = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
                float angle = Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg;

                // Шумовое смещение сегмента
                float noise = noiseAmp > 0f ? Noise3D(center * 1.3f + Vector3.one * seed * 0.17f) * noiseAmp * 0.5f : 0f;
                center.y += noise;

                AppendDeformedEllipsoid(verts, tris, center,
                    segLen + thick * 0.14f, thick * 1.02f, thick * 1.08f,
                    seed + i * 7, 3, 5, noiseAmp * 0.2f, 0.12f);
            }
        }

        private static Mesh BuildArchCollider(float span, float height, float thick)
        {
            // Collider арки: две ноги + упрощённый свод, проём открыт
            List<Vector3> verts = new List<Vector3>(64);
            List<int> tris = new List<int>(128);

            float legH = height * 0.55f;
            AppendDeformedBox(verts, tris, new Vector3(-span * 0.5f, legH * 0.5f, 0),
                thick * 1.3f, legH, thick * 1.3f, 0, 1, 0f);
            AppendDeformedBox(verts, tris, new Vector3(span * 0.5f, legH * 0.5f, 0),
                thick * 1.3f, legH, thick * 1.3f, 0, 1, 0f);
            // Свод — один широкий блок сверху
            AppendDeformedBox(verts, tris, new Vector3(0, height + thick * 0.3f, 0),
                span + thick, thick * 1.2f, thick * 1.3f, 0, 1, 0f);

            return BuildMeshFromLists(verts, tris);
        }

        // ── Cave Entrance mesh ────────────────────────────────────

        private static Mesh BuildCaveEntranceMesh(
            float w, float h, float d, float openW, float openH,
            int seed, int sub, float noiseAmp)
        {
            List<Vector3> verts = new List<Vector3>(1024);
            List<int> tris = new List<int>(2048);

            // Левая боковая масса
            float sideW = (w - openW) * 0.5f;
            AppendDeformedEllipsoid(verts, tris, new Vector3(-(openW * 0.5f + sideW * 0.58f), h * 0.5f, -d * 0.04f),
                sideW * 1.18f, h, d * 1.06f, seed, 4 + sub, 6 + sub, noiseAmp * 0.9f, 0.18f);

            // Правая боковая масса
            AppendDeformedEllipsoid(verts, tris, new Vector3(openW * 0.5f + sideW * 0.58f, h * 0.48f, d * 0.02f),
                sideW * 1.15f, h * 0.96f, d, seed + 3, 4 + sub, 6 + sub, noiseAmp * 0.9f, 0.18f);

            // Верхняя перемычка (над проёмом)
            float topH = h - openH;
            if (topH > 0.1f)
            {
                AppendDeformedEllipsoid(verts, tris, new Vector3(0f, openH + topH * 0.5f, -d * 0.05f),
                    openW * 1.04f, topH * 1.05f, d * 0.96f, seed + 7, 4 + sub, 6 + sub, noiseAmp * 0.75f, 0.12f);
            }

            // Губы входа — выступающие края
            float lipDepth = d * 0.35f;
            AppendDeformedEllipsoid(verts, tris, new Vector3(-(openW * 0.5f + sideW * 0.22f), openH * 0.52f, d * 0.28f),
                sideW * 0.64f, openH * 0.92f, lipDepth, seed + 11, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.55f, 0.1f);
            AppendDeformedEllipsoid(verts, tris, new Vector3(openW * 0.5f + sideW * 0.22f, openH * 0.5f, d * 0.3f),
                sideW * 0.62f, openH * 0.9f, lipDepth, seed + 17, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.55f, 0.1f);

            // Верхняя губа
            if (topH > 0.1f)
            {
                AppendDeformedEllipsoid(verts, tris, new Vector3(0, openH + topH * 0.28f, d * 0.35f),
                    openW * 0.86f, topH * 0.64f, lipDepth * 0.82f, seed + 23, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.45f, 0.08f);
            }

            float shoulderW = sideW * 0.7f;
            float shoulderH = h * 0.42f;
            AppendDeformedEllipsoid(verts, tris, new Vector3(-(openW * 0.5f + sideW * 0.62f), h * 0.72f, -d * 0.12f),
                shoulderW, shoulderH, d * 0.8f, seed + 41, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.55f, 0.12f);
            AppendDeformedEllipsoid(verts, tris, new Vector3(openW * 0.5f + sideW * 0.62f, h * 0.68f, -d * 0.08f),
                shoulderW * 1.08f, shoulderH * 0.92f, d * 0.82f, seed + 47, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.55f, 0.12f);

            int rimTeeth = 2 + (seed % 3);
            for (int i = 0; i < rimTeeth; i++)
            {
                float t = (i + 1f) / (rimTeeth + 1f);
                float rx = Mathf.Lerp(-openW * 0.36f, openW * 0.36f, t);
                AppendDeformedEllipsoid(verts, tris, new Vector3(rx, openH + topH * 0.18f, d * 0.22f),
                    openW * 0.12f, topH * 0.38f, lipDepth * 0.45f, seed + 59 + i * 7, 3, 4, noiseAmp * 0.24f, 0.05f);
            }

            if (sub >= 3)
            {
                AppendDeformedBox(verts, tris, new Vector3(0, openH * 0.35f, -d * 0.28f),
                    openW * 0.72f, openH * 0.2f, d * 0.28f, seed + 97, 1, noiseAmp * 0.2f);
            }

            // Debris ring вокруг входа
            int sideBreakers = 2 + (seed % 2);
            for (int i = 0; i < sideBreakers; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 breakerPos = new Vector3(
                    side * (openW * 0.5f + sideW * Mathf.Lerp(0.42f, 0.78f, ((seed + i * 23) * 0.17f) % 1f)),
                    h * Mathf.Lerp(0.2f, 0.54f, ((seed + i * 29) * 0.11f) % 1f),
                    d * Mathf.Lerp(0.08f, 0.34f, ((seed + i * 31) * 0.07f) % 1f));
                Mesh breaker = BuildDeformedEllipsoid(
                    sideW * 0.34f,
                    h * 0.18f,
                    d * 0.28f,
                    seed + 139 + i * 13,
                    4,
                    5,
                    noiseAmp * 0.32f,
                    0.1f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-18f, 12f, ((seed + i * 37) * 0.13f) % 1f),
                    side < 0f ? -32f : 32f,
                    Mathf.Lerp(-14f, 14f, ((seed + i * 41) * 0.19f) % 1f));
                AppendMeshTransformed(verts, tris, breaker, breakerPos, tilt, Vector3.one);
            }

            int debrisCount = 3 + (seed % 4);
            for (int i = 0; i < debrisCount; i++)
            {
                float angle = (i / (float)debrisCount) * Mathf.PI * 2f;
                float r = w * 0.55f + (seed + i * 13) % 10 * 0.1f * w;
                Vector3 debrisPos = new Vector3(
                    Mathf.Cos(angle) * r * 0.5f,
                    0.15f * h,
                    Mathf.Sin(angle) * r * 0.3f + d * 0.2f);
                float ds = w * Mathf.Lerp(0.08f, 0.18f, ((seed + i * 7) * 0.19f) % 1f);
                AppendDeformedBox(verts, tris, debrisPos, ds, ds * 0.6f, ds, seed + i * 31, 1, noiseAmp * 0.5f);
            }

            return BuildMeshFromLists(verts, tris);
        }

        private static Mesh BuildCaveEntranceCollider(
            float w, float h, float d, float openW, float openH)
        {
            // Collider: только боковые массы и верх, проём открыт
            List<Vector3> verts = new List<Vector3>(64);
            List<int> tris = new List<int>(128);

            float sideW = (w - openW) * 0.5f;
            AppendDeformedBox(verts, tris, new Vector3(-(openW * 0.5f + sideW * 0.5f), h * 0.5f, 0),
                sideW, h, d, 0, 1, 0f);
            AppendDeformedBox(verts, tris, new Vector3(openW * 0.5f + sideW * 0.5f, h * 0.5f, 0),
                sideW, h, d, 0, 1, 0f);

            float topH = h - openH;
            if (topH > 0.1f)
            {
                AppendDeformedBox(verts, tris, new Vector3(0, openH + topH * 0.5f, 0),
                    openW, topH, d, 0, 1, 0f);
            }

            return BuildMeshFromLists(verts, tris);
        }

        // ── Spire mesh ────────────────────────────────────────────

        private static Mesh BuildSpireMesh(
            float baseW, float totalH, float taper,
            int secondaryCount, int seed, int sub, float noiseAmp)
        {
            List<Vector3> verts = new List<Vector3>(1024);
            List<int> tris = new List<int>(2048);

            // Главный ствол — конусообразный с шумом
            int sections = Mathf.Max(3, sub * 2);
            for (int i = 0; i < sections; i++)
            {
                float t0 = i / (float)sections;
                float t1 = (i + 1f) / (float)sections;
                float y0 = totalH * t0;
                float y1 = totalH * t1;
                float w0 = baseW * (1f - t0 * (1f - taper));
                float w1 = baseW * (1f - t1 * (1f - taper));
                float wAvg = (w0 + w1) * 0.5f;
                float yAvg = (y0 + y1) * 0.5f;
                float segH = y1 - y0;

                Vector3 drift = new Vector3(
                    Noise3D(new Vector3(seed * 0.13f, t0 * 4.1f, 0f)) * baseW * 0.16f,
                    0f,
                    Noise3D(new Vector3(0f, t0 * 3.7f, seed * 0.19f)) * baseW * 0.16f);
                AppendDeformedEllipsoid(verts, tris, new Vector3(drift.x, yAvg, drift.z),
                    wAvg, segH + 0.08f, wAvg * Mathf.Lerp(0.86f, 1.14f, ((seed + i * 29) * 0.11f) % 1f),
                    seed + i * 7, 4 + Mathf.Max(1, sub - 1), 6 + sub, noiseAmp * (1f - t0 * 0.45f), 0.08f);
            }

            // Вторичные выступы
            for (int i = 0; i < secondaryCount; i++)
            {
                float angle = (i / (float)Mathf.Max(1, secondaryCount)) * Mathf.PI * 2f + seed * 0.37f;
                float heightFrac = Mathf.Lerp(0.2f, 0.65f, ((seed + i * 19) * 0.23f) % 1f);
                float secY = totalH * heightFrac;
                float secW = baseW * Mathf.Lerp(0.25f, 0.5f, ((seed + i * 11) * 0.17f) % 1f);
                float secH = totalH * Mathf.Lerp(0.12f, 0.28f, ((seed + i * 13) * 0.21f) % 1f);
                float secR = baseW * Mathf.Lerp(0.4f, 0.8f, ((seed + i * 7) * 0.19f) % 1f);

                Vector3 secPos = new Vector3(
                    Mathf.Cos(angle) * secR,
                    secY,
                    Mathf.Sin(angle) * secR);

                Mesh shard = BuildDeformedEllipsoid(secW, secH, secW * 0.82f, seed + i * 29, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.55f, 0.1f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-18f, 18f, ((seed + i * 41) * 0.13f) % 1f),
                    Mathf.Rad2Deg * angle,
                    Mathf.Lerp(-14f, 14f, ((seed + i * 53) * 0.11f) % 1f));
                AppendMeshTransformed(verts, tris, shard, secPos, tilt, Vector3.one);
            }

            // Тяжёлая база
            AppendDeformedEllipsoid(verts, tris, new Vector3(0, baseW * 0.28f, 0),
                baseW * 1.7f, baseW * 0.72f, baseW * 1.58f, seed + 41, 4 + Mathf.Max(1, sub - 1), 6 + sub, noiseAmp * 0.42f, 0.3f);

            int ledges = 2 + (seed % 3);
            for (int i = 0; i < ledges; i++)
            {
                float y = totalH * Mathf.Lerp(0.18f, 0.72f, ((seed + i * 23) * 0.19f) % 1f);
                float ringW = baseW * Mathf.Lerp(0.75f, 1.15f, ((seed + i * 29) * 0.17f) % 1f);
                AppendDeformedEllipsoid(verts, tris, new Vector3(0, y, 0),
                    ringW, totalH * 0.045f, ringW, seed + 67 + i * 17, 3, 5, noiseAmp * 0.16f, 0.25f);
            }

            int shardCount = 1 + (seed % 2);
            for (int i = 0; i < shardCount; i++)
            {
                float angle = seed * 0.19f + i * Mathf.PI;
                float y = totalH * Mathf.Lerp(0.48f, 0.82f, ((seed + i * 13) * 0.21f) % 1f);
                float radius = baseW * Mathf.Lerp(0.65f, 0.95f, ((seed + i * 31) * 0.13f) % 1f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Mesh shard = BuildDeformedEllipsoid(baseW * 0.28f, totalH * 0.22f, baseW * 0.24f, seed + 101 + i * 19, 4 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.35f, 0.08f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-26f, 26f, ((seed + i * 61) * 0.17f) % 1f),
                    Mathf.Rad2Deg * angle,
                    Mathf.Lerp(-22f, 22f, ((seed + i * 67) * 0.19f) % 1f));
                AppendMeshTransformed(verts, tris, shard, pos, tilt, Vector3.one);
            }

            return BuildMeshFromLists(verts, tris);
        }

        // ── Mesh utilities ────────────────────────────────────────

        private static Mesh BuildRockFloorMesh(
            float w, float h, float d,
            int seed, int sub, float noiseAmp, bool addChips)
        {
            List<Vector3> verts = new List<Vector3>(384);
            List<int> tris = new List<int>(768);

            AppendDeformedEllipsoid(verts, tris, new Vector3(0f, h * 0.34f, 0f),
                w, h * 0.7f, d, seed, 4 + sub, 6 + sub, noiseAmp * 0.9f, 0.45f);

            int lobeCount = 2 + (seed % 3);
            for (int i = 0; i < lobeCount; i++)
            {
                float angle = seed * 0.23f + i * (Mathf.PI * 2f / lobeCount);
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * w * 0.22f,
                    h * Mathf.Lerp(0.26f, 0.42f, ((seed + i * 13) * 0.17f) % 1f),
                    Mathf.Sin(angle) * d * 0.22f);

                AppendDeformedEllipsoid(verts, tris, pos,
                    w * 0.44f, h * 0.38f, d * 0.4f,
                    seed + 11 + i * 7, 3 + Mathf.Max(1, sub - 1), 5 + sub, noiseAmp * 0.65f, 0.35f);
            }

            if (addChips)
            {
                int chipCount = 2 + (seed % 2);
                for (int i = 0; i < chipCount; i++)
                {
                    Vector3 pos = new Vector3(
                        Mathf.Lerp(-w * 0.2f, w * 0.2f, ((seed + i * 31) * 0.19f) % 1f),
                        h * Mathf.Lerp(0.52f, 0.72f, ((seed + i * 17) * 0.11f) % 1f),
                        Mathf.Lerp(-d * 0.2f, d * 0.2f, ((seed + i * 23) * 0.17f) % 1f));
                    AppendDeformedEllipsoid(verts, tris, pos,
                        w * 0.19f, h * 0.18f, d * 0.16f,
                        seed + 47 + i * 13, 3, 4, noiseAmp * 0.28f, 0.15f);
                }
            }

            int shards = 1 + (seed % 2);
            for (int i = 0; i < shards; i++)
            {
                Vector3 pos = new Vector3(
                    Mathf.Lerp(-w * 0.16f, w * 0.16f, ((seed + i * 29) * 0.21f) % 1f),
                    h * Mathf.Lerp(0.34f, 0.56f, ((seed + i * 37) * 0.09f) % 1f),
                    Mathf.Lerp(-d * 0.18f, d * 0.18f, ((seed + i * 41) * 0.15f) % 1f));
                Mesh shard = BuildDeformedBox(w * 0.22f, h * 0.28f, d * 0.14f, seed + 89 + i * 17, 1, noiseAmp * 0.5f);
                Quaternion tilt = Quaternion.Euler(
                    Mathf.Lerp(-24f, 24f, ((seed + i * 47) * 0.13f) % 1f),
                    Mathf.Lerp(0f, 180f, ((seed + i * 53) * 0.07f) % 1f),
                    Mathf.Lerp(-16f, 16f, ((seed + i * 59) * 0.11f) % 1f));
                AppendMeshTransformed(verts, tris, shard, pos, tilt, Vector3.one);
            }

            return BuildMeshFromLists(verts, tris);
        }

        private static Mesh BuildDeformedEllipsoid(
            float w, float h, float d,
            int seed, int rings, int segments, float noiseAmp, float flattenBottom)
        {
            List<Vector3> verts = new List<Vector3>(256);
            List<int> tris = new List<int>(512);
            AppendDeformedEllipsoid(verts, tris, Vector3.zero, w, h, d, seed, rings, segments, noiseAmp, flattenBottom);
            return BuildMeshFromLists(verts, tris);
        }

        private static void AppendDeformedEllipsoid(
            List<Vector3> verts, List<int> tris,
            Vector3 center,
            float w, float h, float d,
            int seed, int rings, int segments, float noiseAmp, float flattenBottom)
        {
            int ringCount = Mathf.Clamp(rings, 3, 12);
            int segmentCount = Mathf.Clamp(segments, 4, 18);
            int baseIndex = verts.Count;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float halfD = d * 0.5f;

            for (int y = 0; y <= ringCount; y++)
            {
                float v = y / (float)ringCount;
                float latitude = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float sinLat = Mathf.Sin(latitude);
                float cosLat = Mathf.Cos(latitude);

                for (int x = 0; x <= segmentCount; x++)
                {
                    float u = x / (float)segmentCount;
                    float longitude = u * Mathf.PI * 2f;
                    float sinLon = Mathf.Sin(longitude);
                    float cosLon = Mathf.Cos(longitude);

                    Vector3 normal = new Vector3(cosLat * cosLon, sinLat, cosLat * sinLon);
                    Vector3 pos = new Vector3(
                        normal.x * halfW,
                        normal.y * halfH,
                        normal.z * halfD);

                    if (noiseAmp > 0f)
                    {
                        Vector3 sample = pos + Vector3.one * seed * 0.071f;
                        float primary = Noise3D(sample * 1.2f);
                        float secondary = Noise3D(sample * 2.7f + normal * 0.83f) * 0.45f;
                        float tertiary = Noise3D(sample * 5.1f + new Vector3(0.37f, 0.91f, 0.53f)) * 0.18f;
                        float displacement = (primary + secondary + tertiary) * noiseAmp;
                        pos += normal * displacement;

                        float shearX = Noise3D(new Vector3(sample.y, sample.z, seed * 0.11f)) * noiseAmp * halfW * 0.18f * cosLat;
                        float shearZ = Noise3D(new Vector3(seed * 0.07f, sample.x, sample.y)) * noiseAmp * halfD * 0.18f * cosLat;
                        pos.x += shearX;
                        pos.z += shearZ;
                    }

                    if (flattenBottom > 0f && pos.y < -halfH * 0.08f)
                    {
                        float t = Mathf.InverseLerp(-halfH * 0.08f, -halfH, pos.y);
                        pos.y = Mathf.Lerp(pos.y, -halfH * 0.96f, t * flattenBottom);
                    }

                    verts.Add(center + pos);
                }
            }

            for (int y = 0; y < ringCount; y++)
            {
                for (int x = 0; x < segmentCount; x++)
                {
                    int a = baseIndex + y * (segmentCount + 1) + x;
                    int b = a + 1;
                    int c = a + segmentCount + 1;
                    int d2 = c + 1;

                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d2);
                }
            }
        }

        private static void AppendMeshTransformed(
            List<Vector3> verts, List<int> tris,
            Mesh mesh, Vector3 offset, Quaternion rotation, Vector3 scale)
        {
            if (mesh == null)
                return;

            Vector3[] sourceVertices = mesh.vertices;
            int[] sourceTriangles = mesh.triangles;
            int baseIndex = verts.Count;

            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 vertex = Vector3.Scale(sourceVertices[i], scale);
                verts.Add(rotation * vertex + offset);
            }

            for (int i = 0; i < sourceTriangles.Length; i++)
                tris.Add(baseIndex + sourceTriangles[i]);
        }

        private static Mesh BuildMeshFromLists(List<Vector3> verts, List<int> tris)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }

        private static void SetMeshName(Mesh mesh, string name)
        {
            if (mesh != null) mesh.name = name;
        }

        // ── Deterministic noise ───────────────────────────────────

        /// <summary>
        /// Детерминированный 3D шум на основе sin-хэша.
        /// Возвращает [-1, 1]. Без аллокаций.
        /// </summary>
        private static float Noise3D(Vector3 p)
        {
            float x = Mathf.Sin(p.x * 127.1f + p.y * 311.7f + p.z * 74.7f) * 43758.5453f;
            float y = Mathf.Sin(p.x * 269.5f + p.y * 183.3f + p.z * 246.1f) * 43758.5453f;
            float z = Mathf.Sin(p.x * 419.2f + p.y * 371.9f + p.z * 168.3f) * 43758.5453f;
            x -= Mathf.Floor(x);
            y -= Mathf.Floor(y);
            z -= Mathf.Floor(z);
            return (x + y + z) / 3f * 2f - 1f;
        }
    }
}

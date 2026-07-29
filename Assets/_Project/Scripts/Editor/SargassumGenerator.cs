using System.Collections.Generic;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    /// <summary>
    /// Builds the standalone procedural Type 2 sargassum gameplay prefab, meshes, and material.
    /// </summary>
    public static class SargassumGenerator
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/Hecton_SargassumMaster.shader";
        private const string MaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_type2.mat";
        private const string ParticleMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_leaf_scraps.mat";
        private const string MeshFolderPath = "Assets/_Project/Art/Meshes/Generated/Sargassum";
        private const string PrefabFolderPath = "Assets/_Project/Prefabs/Nature/Flora";
        private const string Lod0MeshPath = MeshFolderPath + "/GEN_Sargassum_Type2_LOD0.asset";
        private const string Lod1MeshPath = MeshFolderPath + "/GEN_Sargassum_Type2_LOD1.asset";
        private const string Lod2MeshPath = MeshFolderPath + "/GEN_Sargassum_Type2_LOD2.asset";
        private const string PrefabPath = PrefabFolderPath + "/PFB_Sargassum_Type2.prefab";
        private const float Lod0Threshold = 0.5f;
        private const float Lod1Threshold = 0.16f;
        private const float Lod2Threshold = 0.025f;

        [MenuItem("Hecton8/Authoring/Build Type 2 Sargassum", priority = 178)]
        public static void BuildType2Sargassum()
        {
            EnsureFolders();

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("[SargassumGenerator] Missing shader asset. Expected at " + ShaderPath + ".");
                return;
            }

            Material sharedMaterial = LoadOrCreateMaterial(MaterialPath, shader);
            if (sharedMaterial == null)
            {
                Debug.LogError("[SargassumGenerator] Failed to create or load sargassum material.");
                return;
            }

            ConfigureSharedMaterial(sharedMaterial);

            Material particleMaterial = LoadOrCreateParticleMaterial();
            Mesh generatedLod0 = null;
            Mesh generatedLod1 = null;
            Mesh generatedLod2 = null;
            GameObject prefabRoot = null;

            try
            {
                generatedLod0 = BuildClusterMesh(lodLevel: 0);
                generatedLod1 = BuildClusterMesh(lodLevel: 1);
                generatedLod2 = BuildImpostorMesh();

                Mesh bakedLod0 = CreateOrUpdateMeshAsset(Lod0MeshPath, generatedLod0);
                Mesh bakedLod1 = CreateOrUpdateMeshAsset(Lod1MeshPath, generatedLod1);
                Mesh bakedLod2 = CreateOrUpdateMeshAsset(Lod2MeshPath, generatedLod2);

                prefabRoot = new GameObject("PFB_Sargassum_Type2");
                LODGroup lodGroup = prefabRoot.AddComponent<LODGroup>();
                lodGroup.animateCrossFading = true;
                lodGroup.fadeMode = LODFadeMode.CrossFade;

                Renderer lod0Renderer = CreateLodRenderer(prefabRoot.transform, "__LOD0", bakedLod0, sharedMaterial, true);
                Renderer lod1Renderer = CreateLodRenderer(prefabRoot.transform, "__LOD1", bakedLod1, sharedMaterial, true);
                Renderer lod2Renderer = CreateLodRenderer(prefabRoot.transform, "__LOD2", bakedLod2, sharedMaterial, false);

                lodGroup.SetLODs(new[]
                {
                    new LOD(Lod0Threshold, new[] { lod0Renderer }),
                    new LOD(Lod1Threshold, new[] { lod1Renderer }),
                    new LOD(Lod2Threshold, new[] { lod2Renderer })
                });
                lodGroup.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[SargassumGenerator] Built Type 2 sargassum prefab at " + PrefabPath + " without per-instance physics or cut responders.");
            }
            finally
            {
                if (generatedLod0 != null)
                    Object.DestroyImmediate(generatedLod0);

                if (generatedLod1 != null)
                    Object.DestroyImmediate(generatedLod1);

                if (generatedLod2 != null)
                    Object.DestroyImmediate(generatedLod2);

                if (prefabRoot != null)
                    Object.DestroyImmediate(prefabRoot);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/WorldProceduralProxy");
            EnsureFolder("Assets/_Project/Art/Meshes");
            EnsureFolder("Assets/_Project/Art/Meshes/Generated");
            EnsureFolder(MeshFolderPath);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Nature");
            EnsureFolder(PrefabFolderPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(shader)
            {
                name = "MAT_sargassum_type2"
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LoadOrCreateParticleMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            material = new Material(shader)
            {
                name = "MAT_sargassum_leaf_scraps"
            };
            material.SetColor("_BaseColor", new Color(0.53f, 0.35f, 0.12f, 1f));
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            AssetDatabase.CreateAsset(material, ParticleMaterialPath);
            return material;
        }

        private static void ConfigureSharedMaterial(Material material)
        {
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_DryColor", new Color(0.60f, 0.42f, 0.18f, 1f));
            material.SetColor("_WetColor", new Color(0.34f, 0.25f, 0.10f, 1f));
            material.SetColor("_BubbleColor", new Color(1.00f, 0.78f, 0.34f, 1f));
            material.SetColor("_RimColor", new Color(0.84f, 0.64f, 0.28f, 1f));
            material.SetColor("_SSSColor", new Color(1.00f, 0.88f, 0.48f, 1f));
            material.SetColor("_CutEdgeColor", new Color(1.00f, 0.74f, 0.32f, 1f));
            material.SetFloat("_AlphaClip", 0.36f);
            material.SetFloat("_Smoothness", 0.44f);
            material.SetFloat("_NormalInfluence", 0.22f);
            material.SetFloat("_RimStrength", 0.32f);
            material.SetFloat("_RimPower", 3.2f);
            material.SetFloat("_SSSStrength", 1.4f);
            material.SetFloat("_SSSPower", 5.6f);
            material.SetFloat("_BubbleGlow", 0.28f);
            material.SetFloat("_SwayAmplitude", 0.12f);
            material.SetFloat("_SwayFrequency", 1.8f);
            material.SetFloat("_SwaySpeed", 0.82f);
            material.SetFloat("_PhaseScale", 6.5f);
            material.SetFloat("_BeardSwingMultiplier", 1.3f);
            material.SetFloat("_InteractionRadius", 0.8f);
            material.SetFloat("_InteractionCutStrength", 0f);
            material.SetFloat("_InteractionEdgeBoost", 1.2f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(material);
        }

        private static Renderer CreateLodRenderer(Transform parent, string childName, Mesh mesh, Material material, bool castShadows)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);

            MeshFilter meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = castShadows;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            meshRenderer.allowOcclusionWhenDynamic = true;
            return meshRenderer;
        }

        private static Mesh CreateOrUpdateMeshAsset(string path, Mesh generatedMesh)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(Object.Instantiate(generatedMesh), path);
                return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            EditorUtility.CopySerialized(generatedMesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh BuildClusterMesh(int lodLevel)
        {
            MeshBuilder builder = new MeshBuilder(lodLevel == 0 ? 8192 : 4096);
            DeterministicRng rng = new DeterministicRng((uint)(0x91E10DA5u + lodLevel * 977u));

            int topRibbonCount = lodLevel == 0 ? 18 : 10;
            int sideRibbonCount = lodLevel == 0 ? 12 : 6;
            int beardRibbonCount = lodLevel == 0 ? 18 : 8;
            int bladderCount = lodLevel == 0 ? 28 : 8;
            int topSegments = lodLevel == 0 ? 7 : 4;
            int sideSegments = lodLevel == 0 ? 6 : 4;
            int beardSegments = lodLevel == 0 ? 8 : 5;

            for (int i = 0; i < topRibbonCount; i++)
            {
                float yaw = (i / (float)topRibbonCount) * Mathf.PI * 2f + rng.Range(-0.18f, 0.18f);
                Vector3 dir = new Vector3(Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(yaw), rng.Range(0.05f, 0.22f), Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(yaw)).normalized;
                Vector3 origin = new Vector3(rng.Range(-0.18f, 0.18f), rng.Range(0.15f, 0.42f), rng.Range(-0.18f, 0.18f));
                float length = rng.Range(1.3f, 2.05f);
                float width = rng.Range(0.16f, 0.24f);
                float curl = rng.Range(0.18f, 0.46f);
                float droop = rng.Range(0.12f, 0.38f);
                float phase = rng.Value();
                BuildRibbon(builder, origin, dir, length, width, topSegments, curl, droop, phase, ao: 0.72f, sssMask: 0.5f, rigidity: 0.9f);
            }

            for (int i = 0; i < sideRibbonCount; i++)
            {
                float yaw = (i / (float)sideRibbonCount) * Mathf.PI * 2f + rng.Range(-0.26f, 0.26f);
                Vector3 dir = new Vector3(Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(yaw), rng.Range(-0.04f, 0.08f), Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(yaw)).normalized;
                Vector3 origin = new Vector3(rng.Range(-0.42f, 0.42f), rng.Range(0.02f, 0.22f), rng.Range(-0.42f, 0.42f));
                float length = rng.Range(1.0f, 1.65f);
                float width = rng.Range(0.12f, 0.18f);
                float curl = rng.Range(0.14f, 0.36f);
                float droop = rng.Range(0.08f, 0.22f);
                float phase = rng.Value();
                BuildRibbon(builder, origin, dir, length, width, sideSegments, curl, droop, phase, ao: 0.58f, sssMask: 0.5f, rigidity: 0.72f);
            }

            for (int i = 0; i < beardRibbonCount; i++)
            {
                float yaw = (i / (float)beardRibbonCount) * Mathf.PI * 2f + rng.Range(-0.32f, 0.32f);
                Vector3 origin = new Vector3(Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(yaw) * rng.Range(0.24f, 0.86f), rng.Range(-0.1f, 0.08f), Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(yaw) * rng.Range(0.24f, 0.86f));
                Vector3 dir = new Vector3(rng.Range(-0.12f, 0.12f), -1f, rng.Range(-0.12f, 0.12f)).normalized;
                float length = rng.Range(1.45f, 2.7f);
                float width = rng.Range(0.03f, 0.07f);
                float curl = rng.Range(0.05f, 0.16f);
                float droop = rng.Range(0.02f, 0.1f);
                float phase = rng.Value();
                BuildRibbon(builder, origin, dir, length, width, beardSegments, curl, droop, phase, ao: 0.26f, sssMask: 0.12f, rigidity: 0.1f);
            }

            for (int i = 0; i < bladderCount; i++)
            {
                float yaw = rng.Range(0f, Mathf.PI * 2f);
                float radius = rng.Range(0.18f, 0.96f);
                Vector3 center = new Vector3(
                    Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(yaw) * radius,
                    rng.Range(0.08f, 0.6f),
                    Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(yaw) * radius);
                center += new Vector3(rng.Range(-0.12f, 0.12f), 0f, rng.Range(-0.12f, 0.12f));
                float bladderRadius = lodLevel == 0 ? rng.Range(0.04f, 0.1f) : rng.Range(0.03f, 0.06f);
                BuildOctaSphere(builder, center, bladderRadius, new Color(0.84f, 1f, rng.Value(), 0.96f));
            }

            Mesh mesh = builder.CreateMesh(lodLevel == 0 ? "GEN_Sargassum_Type2_LOD0" : "GEN_Sargassum_Type2_LOD1");
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildImpostorMesh()
        {
            MeshBuilder builder = new MeshBuilder(256);
            Color impostorColor = new Color(0.56f, 0.48f, 0.35f, 0.48f);

            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 45f;
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                BuildDiamondCard(
                    builder,
                    Vector3.zero,
                    rotation * Vector3.right,
                    Vector3.up,
                    2.6f,
                    2.0f,
                    impostorColor,
                    0.38f + i * 0.11f);
            }

            BuildDiamondCard(
                builder,
                new Vector3(0f, 0.15f, 0f),
                Vector3.right,
                Vector3.forward,
                2.1f,
                1.6f,
                new Color(0.48f, 0.42f, 0.62f, 0.55f),
                0.77f);

            Mesh mesh = builder.CreateMesh("GEN_Sargassum_Type2_LOD2");
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildRibbon(
            MeshBuilder builder,
            Vector3 origin,
            Vector3 direction,
            float length,
            float width,
            int segments,
            float curlAmplitude,
            float droop,
            float phase,
            float ao,
            float sssMask,
            float rigidity)
        {
            Vector3 lateralAxis = Vector3.Cross(direction, Vector3.up);
            if (lateralAxis.sqrMagnitude < 0.001f)
                lateralAxis = Vector3.Cross(direction, Vector3.right);
            lateralAxis.Normalize();

            Vector3 secondaryAxis = Vector3.Cross(lateralAxis, direction).normalized;
            int vertexStart = builder.Vertices.Count;
            Color vertexColor = new Color(Mathf.Clamp01(ao), Mathf.Clamp01(sssMask), Mathf.Clamp01(phase), Mathf.Clamp01(rigidity));

            for (int segment = 0; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float sine = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((t * 1.9f + phase) * Mathf.PI * 2f);
                float cosine = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara((t * 1.4f + phase * 0.7f) * Mathf.PI * 2f);
                Vector3 curveOffset =
                    lateralAxis * (sine * curlAmplitude * (1f - t * 0.28f)) +
                    secondaryAxis * (cosine * curlAmplitude * 0.42f * (1f - t * 0.18f)) -
                    Vector3.up * (droop * t * t);
                Vector3 point = origin + direction * (length * t) + curveOffset;
                Vector3 nextPoint = origin + direction * (length * Mathf.Min(1f, t + 0.08f));
                Vector3 tangent = (nextPoint + curveOffset - point).normalized;
                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = direction;

                Vector3 side = Vector3.Cross(Vector3.up, tangent);
                if (side.sqrMagnitude < 0.0001f)
                    side = Vector3.Cross(Vector3.forward, tangent);
                side.Normalize();

                float widthScale = Mathf.Lerp(1f, 0.16f, t);
                Vector3 offset = side * (width * widthScale);
                builder.Vertices.Add(point - offset);
                builder.Vertices.Add(point + offset);
                Vector3 normal = Vector3.Cross(side, tangent).normalized;
                builder.Normals.Add(normal);
                builder.Normals.Add(normal);
                builder.Colors.Add(vertexColor);
                builder.Colors.Add(vertexColor);
                builder.Uvs.Add(new Vector2(0f, t));
                builder.Uvs.Add(new Vector2(1f, t));
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int baseIndex = vertexStart + segment * 2;
                builder.AddTriangle(baseIndex + 0, baseIndex + 2, baseIndex + 1);
                builder.AddTriangle(baseIndex + 1, baseIndex + 2, baseIndex + 3);
            }
        }

        private static void BuildOctaSphere(MeshBuilder builder, Vector3 center, float radius, Color vertexColor)
        {
            int start = builder.Vertices.Count;
            builder.Vertices.Add(center + Vector3.up * radius);
            builder.Vertices.Add(center + Vector3.down * radius);
            builder.Vertices.Add(center + Vector3.right * radius);
            builder.Vertices.Add(center + Vector3.left * radius);
            builder.Vertices.Add(center + Vector3.forward * radius);
            builder.Vertices.Add(center + Vector3.back * radius);

            for (int i = 0; i < 6; i++)
            {
                Vector3 normal = (builder.Vertices[start + i] - center).normalized;
                builder.Normals.Add(normal);
                builder.Colors.Add(vertexColor);
                // UV0 is a control channel here, not a texture coordinate: Hecton_SargassumMaster samples no
                // albedo/normal map at all (its only TEXTURE2Ds are the world-space buoyancy and cut RTs), and it
                // reads uv.y as heightMask - the anchor-distance leverage behind sway, prop wash, pulsation and
                // cut warp. The previous normal projection put the octahedron equator at uv.y 0.5 and the poles at
                // 0 and 1, so the equator pumped radially while both poles stayed pinned. Gas bladders are the
                // rigid class in 3DMODEL_FLORA_CORAL.md line 24, so leverage is 0. uv.x sits at the centre of the
                // EvaluateLeafMask band; bladder alpha is forced to 1 by the isBubble branch regardless.
                builder.Uvs.Add(new Vector2(0.5f, 0f));
            }

            builder.AddTriangle(start + 0, start + 2, start + 4);
            builder.AddTriangle(start + 0, start + 4, start + 3);
            builder.AddTriangle(start + 0, start + 3, start + 5);
            builder.AddTriangle(start + 0, start + 5, start + 2);
            builder.AddTriangle(start + 1, start + 4, start + 2);
            builder.AddTriangle(start + 1, start + 3, start + 4);
            builder.AddTriangle(start + 1, start + 5, start + 3);
            builder.AddTriangle(start + 1, start + 2, start + 5);
        }

        private static void BuildDiamondCard(
            MeshBuilder builder,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            float width,
            float height,
            Color color,
            float phase)
        {
            int start = builder.Vertices.Count;
            Vector3 halfRight = right.normalized * (width * 0.5f);
            Vector3 halfUp = up.normalized * (height * 0.5f);
            Vector3 normal = Vector3.Cross(right, up).normalized;
            Color vertexColor = new Color(color.r, color.g, phase, color.a);

            builder.Vertices.Add(center + halfUp);
            builder.Vertices.Add(center + halfRight);
            builder.Vertices.Add(center - halfUp);
            builder.Vertices.Add(center - halfRight);

            for (int i = 0; i < 4; i++)
            {
                builder.Normals.Add(normal);
                builder.Colors.Add(vertexColor);
            }

            // Diamond corner coordinates are not valid UV0 for this shader. Two separate contracts apply:
            //  - uv.x feeds EvaluateLeafMask, which fades out on abs(uv.x * 2 - 1) and is then alpha-clipped at
            //    _AlphaClip 0.36. Corner values of 0f and 1f put the card's own left and right points at
            //    leafMask 0, so the impostor lost roughly a quarter of its authored width - a silhouette/mass
            //    loss at the one LOD where mass is all that survives (3DMODEL_FLORA_CORAL.md line 99). Ribbons
            //    can afford edge values of 0 and 1 because their quad deliberately overshoots the visible blade;
            //    an impostor card has no such margin, its geometry IS the intended silhouette. Keeping the
            //    corners inside the surviving band leaves a faint serrated fade instead of a cut.
            //  - uv.y feeds heightMask, the sway/pulsation leverage, so it must track real vertical extent rather
            //    than a corner index. The second card authored below lies flat at a single Y yet previously got a
            //    full 0..1 leverage sweep across a level plate.
            const float cardEdgeU = 0.24f;
            float topY = (center + halfUp).y;
            float rightY = (center + halfRight).y;
            float bottomY = (center - halfUp).y;
            float leftY = (center - halfRight).y;
            float minY = Mathf.Min(Mathf.Min(topY, rightY), Mathf.Min(bottomY, leftY));
            float spanY = Mathf.Max(Mathf.Max(topY, rightY), Mathf.Max(bottomY, leftY)) - minY;
            float invSpanY = spanY > 0.0001f ? 1f / spanY : 0f;
            builder.Uvs.Add(new Vector2(0.5f, (topY - minY) * invSpanY));
            builder.Uvs.Add(new Vector2(0.5f + cardEdgeU, (rightY - minY) * invSpanY));
            builder.Uvs.Add(new Vector2(0.5f, (bottomY - minY) * invSpanY));
            builder.Uvs.Add(new Vector2(0.5f - cardEdgeU, (leftY - minY) * invSpanY));

            builder.AddTriangle(start + 0, start + 1, start + 3);
            builder.AddTriangle(start + 1, start + 2, start + 3);
        }

        private sealed class MeshBuilder
        {
            public readonly List<Vector3> Vertices;
            public readonly List<Vector3> Normals;
            public readonly List<Color> Colors;
            public readonly List<Vector2> Uvs;
            public readonly List<int> Indices;

            public MeshBuilder(int capacity)
            {
                Vertices = new List<Vector3>(capacity); // COLD ALLOC: List<Vector3>(capacity) — procedural mesh vertex buffer — owner: SargassumGenerator
                Normals = new List<Vector3>(capacity); // COLD ALLOC: List<Vector3>(capacity) — procedural mesh normal buffer — owner: SargassumGenerator
                Colors = new List<Color>(capacity); // COLD ALLOC: List<Color>(capacity) — procedural mesh vertex color buffer — owner: SargassumGenerator
                Uvs = new List<Vector2>(capacity); // COLD ALLOC: List<Vector2>(capacity) — procedural mesh UV buffer — owner: SargassumGenerator
                Indices = new List<int>(capacity * 3); // COLD ALLOC: List<int>(capacity*3) — procedural mesh index buffer — owner: SargassumGenerator
            }

            public void AddTriangle(int a, int b, int c)
            {
                Indices.Add(a);
                Indices.Add(b);
                Indices.Add(c);
            }

            public Mesh CreateMesh(string meshName)
            {
                Mesh mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetColors(Colors);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Indices, 0, true);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private struct DeterministicRng
        {
            private uint _state;

            public DeterministicRng(uint seed)
            {
                _state = seed == 0u ? 1u : seed;
            }

            public float Value()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0x00FFFFFFu) / 16777215f;
            }

            public float Range(float min, float max)
            {
                return Mathf.Lerp(min, max, Value());
            }
        }
    }
}

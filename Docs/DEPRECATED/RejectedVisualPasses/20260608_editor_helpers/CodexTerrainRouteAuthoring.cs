#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class CodexTerrainRouteAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string RootName = "H8_CODEX_TERRAIN_PROCEDURAL_ROUTE_20260608";
        private const string MeshRoot = "Assets/_Project/Art/Meshes/Codex/TerrainProceduralRoute_20260608";
        private const string ScreenshotRoot = "Docs/Screenshots/CodexTerrainProceduralRoute";
        private const string ReportPath = "Docs/AgentLogs/CODEX_TERRAIN_PROCEDURAL_ROUTE_20260608.txt";
        private const int TerrainSeed = 862608;
        private static readonly Vector3 Origin = new Vector3(0f, 0f, 72f);

        private static readonly string[] RejectedRecentRoots =
        {
            "H8_CODEX_WATER_SKY_FIRST_PASS_20260608",
            "H8_CODEX_VISUAL_ROUTE_RECOVERY_20260608",
            "H8_CODEX_TERRAIN_PROCEDURAL_ROUTE_20260608"
        };

        public static void ApplyAndExit()
        {
            int exitCode = 0;
            try
            {
                ApplyInternal();
            }
            catch (Exception exception)
            {
                exitCode = 1;
                WriteFailureReport(exception);
                Debug.LogException(exception);
            }

            EditorApplication.Exit(exitCode);
        }

        [MenuItem("HECTON-8/Codex/Apply Terrain Procedural Route")]
        public static void ApplyFromMenu()
        {
            ApplyInternal();
        }

        private static void ApplyInternal()
        {
            EnsureAssetDirectory(MeshRoot);
            Directory.CreateDirectory(AbsoluteProjectPath(ScreenshotRoot));

            Scene scene = EnsureTargetSceneLoaded();
            int deprecatedCount = DeprecateRejectedRecentRoots(scene);
            RemoveExistingRoot(scene);

            Mesh terrainMesh = UpsertMesh(
                MeshRoot + "/MESH_Codex_PhoticShelfCanyonTerrain_20260608.asset",
                BuildTerrainMesh("MESH_Codex_PhoticShelfCanyonTerrain_20260608", 151, 145, false));
            Mesh collisionMesh = UpsertMesh(
                MeshRoot + "/COL_Codex_PhoticShelfCanyonProxy_20260608.asset",
                BuildTerrainMesh("COL_Codex_PhoticShelfCanyonProxy_20260608", 45, 43, true));
            Mesh ridgeMesh = UpsertMesh(
                MeshRoot + "/MESH_Codex_FracturedRouteRidges_20260608.asset",
                BuildRouteRidgeMesh("MESH_Codex_FracturedRouteRidges_20260608"));
            Mesh ledgeMesh = UpsertMesh(
                MeshRoot + "/MESH_Codex_CollapsedShelfLedges_20260608.asset",
                BuildCollapsedLedgeMesh("MESH_Codex_CollapsedShelfLedges_20260608"));
            Mesh strataMesh = UpsertMesh(
                MeshRoot + "/MESH_Codex_WaterlineStrataCuts_20260608.asset",
                BuildWaterlineStrataMesh("MESH_Codex_WaterlineStrataCuts_20260608"));

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Origin;

            Material terrainMaterial = LoadMaterial(
                "Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_PhoticHeroTerrain_1453.mat",
                new Color(0.70f, 0.78f, 0.73f, 1f));
            Material basaltMaterial = LoadMaterial(
                "Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat",
                new Color(0.21f, 0.28f, 0.27f, 1f));
            Material reefMaterial = LoadMaterial(
                "Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_PhoticReefRockAndPlate_1465.mat",
                new Color(0.55f, 0.66f, 0.58f, 1f));

            GameObject terrain = CreateMeshObject("terrain_macro_photic_shelf_canyon", root.transform, terrainMesh, terrainMaterial);
            terrain.transform.localPosition = Vector3.zero;
            terrain.transform.localRotation = Quaternion.identity;
            terrain.transform.localScale = Vector3.one;

            GameObject ridges = CreateMeshObject("terrain_meso_fractured_route_ridges", root.transform, ridgeMesh, basaltMaterial);
            GameObject ledges = CreateMeshObject("terrain_meso_collapsed_shelf_ledges", root.transform, ledgeMesh, reefMaterial);
            GameObject strata = CreateMeshObject("terrain_waterline_strata_cut_faces", root.transform, strataMesh, basaltMaterial);

            GameObject proxy = new GameObject("COL_lowpoly_route_proxy_no_lod0_meshcollider");
            proxy.transform.SetParent(root.transform, false);
            MeshCollider collider = proxy.AddComponent<MeshCollider>();
            collider.sharedMesh = collisionMesh;
            collider.convex = false;

            TuneLighting();
            Camera sourceCamera = ResolveSourceCamera();
            TuneCamera(sourceCamera);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            List<string> proofPaths = CaptureProofs(sourceCamera);
            WriteSuccessReport(deprecatedCount, terrainMesh, collisionMesh, ridgeMesh, ledgeMesh, strataMesh, proofPaths);
            Debug.Log("[CodexTerrainRouteAuthoring] Applied " + RootName + " deprecated=" + deprecatedCount);
        }

        private static Mesh BuildTerrainMesh(string name, int xCount, int zCount, bool collisionProxy)
        {
            const float width = 310f;
            const float depth = 295f;
            List<Vector3> vertices = new List<Vector3>(xCount * zCount);
            List<Vector2> uvs = new List<Vector2>(xCount * zCount);
            List<Color> colors = new List<Color>(xCount * zCount);
            int[,] index = new int[xCount, zCount];
            for (int z = 0; z < zCount; z++)
            {
                float vz = z / (float)(zCount - 1);
                float localZ = Mathf.Lerp(-128f, depth - 128f, vz);
                for (int x = 0; x < xCount; x++)
                {
                    float vx = x / (float)(xCount - 1);
                    float localX = Mathf.Lerp(-width * 0.5f, width * 0.5f, vx);
                    if (!InsideTerrainMask(localX, localZ))
                    {
                        index[x, z] = -1;
                        continue;
                    }

                    float height = EvaluateTerrainHeight(localX, localZ, collisionProxy);
                    index[x, z] = vertices.Count;
                    vertices.Add(new Vector3(localX, height, localZ));
                    uvs.Add(new Vector2(localX / 52f, localZ / 52f));
                    colors.Add(EvaluateTerrainMaskColor(localX, localZ, height));
                }
            }

            List<int> triangles = new List<int>((xCount - 1) * (zCount - 1) * 6);
            for (int z = 0; z < zCount - 1; z++)
            {
                for (int x = 0; x < xCount - 1; x++)
                {
                    int a = index[x, z];
                    int b = index[x + 1, z];
                    int c = index[x, z + 1];
                    int d = index[x + 1, z + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0)
                        continue;

                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(55f);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRouteRidgeMesh(string name)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                int start = vertices.Count;
                for (int i = 0; i < 36; i++)
                {
                    float t = i / 35f;
                    float z = Mathf.Lerp(-92f, 172f, t);
                    float center = RouteCenterX(z);
                    float lateral = side * Mathf.Lerp(38f, 68f, Mathf.SmoothStep(0f, 1f, t));
                    float x = center + lateral + side * (RidgedNoise(z * 0.053f, 18.2f) * 5f);
                    float baseY = EvaluateTerrainHeight(x, z, false) + 0.5f;
                    float topY = baseY + Mathf.Lerp(6f, 16f, Mathf.Sin(t * Mathf.PI)) + RidgedNoise(z * 0.071f, 4.1f) * 3f;
                    float thickness = Mathf.Lerp(3.5f, 8.5f, RidgedNoise(z * 0.097f, 2.3f));
                    vertices.Add(new Vector3(x - side * thickness, baseY, z));
                    vertices.Add(new Vector3(x + side * 1.6f, topY, z + Mathf.Sin(t * 16f) * 2f));
                    colors.Add(new Color(0.82f, 0.46f, 0.28f, 0.35f));
                    colors.Add(new Color(1f, 0.58f, 0.18f, 0.62f));
                }

                for (int i = 0; i < 35; i++)
                {
                    int a = start + i * 2;
                    int b = a + 1;
                    int c = a + 2;
                    int d = a + 3;
                    if (side < 0)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(65f);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCollapsedLedgeMesh(string name)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            for (int ledge = 0; ledge < 10; ledge++)
            {
                float z = Mathf.Lerp(-72f, 150f, ledge / 9f);
                float center = RouteCenterX(z);
                float side = ledge % 2 == 0 ? -1f : 1f;
                float x = center + side * Mathf.Lerp(26f, 88f, Halton(ledge + 4, 3));
                float y = EvaluateTerrainHeight(x, z, false) + 0.25f;
                float radiusX = Mathf.Lerp(10f, 23f, Halton(ledge + 5, 2));
                float radiusZ = Mathf.Lerp(7f, 18f, Halton(ledge + 8, 5));
                int start = vertices.Count;
                vertices.Add(new Vector3(x, y + 0.7f, z));
                colors.Add(new Color(0.58f, 0.76f, 0.32f, 0.48f));
                int segments = 12;
                for (int i = 0; i < segments; i++)
                {
                    float a = (i / (float)segments) * Mathf.PI * 2f;
                    float r = 0.72f + RidgedNoise(ledge * 7.1f + i * 1.3f, 0.5f) * 0.38f;
                    Vector3 p = new Vector3(
                        x + Mathf.Cos(a) * radiusX * r,
                        y + Mathf.Sin(a * 3f + ledge) * 0.9f - Halton(i + ledge + 1, 7) * 1.2f,
                        z + Mathf.Sin(a) * radiusZ * r);
                    vertices.Add(p);
                    colors.Add(new Color(0.62f, 0.68f, 0.42f, 0.55f));
                }

                for (int i = 0; i < segments; i++)
                {
                    int a = start;
                    int b = start + 1 + i;
                    int c = start + 1 + ((i + 1) % segments);
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(50f);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildWaterlineStrataMesh(string name)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            for (int strip = 0; strip < 7; strip++)
            {
                float z = Mathf.Lerp(-118f, -8f, strip / 6f);
                float baseWidth = Mathf.Lerp(128f, 68f, strip / 6f);
                int start = vertices.Count;
                for (int i = 0; i < 30; i++)
                {
                    float t = i / 29f;
                    float x = Mathf.Lerp(-baseWidth, baseWidth, t);
                    float wave = Mathf.Sin(t * Mathf.PI * 4f + strip * 0.8f) * 1.6f;
                    float y = EvaluateTerrainHeight(x, z + wave, false) + 0.45f;
                    vertices.Add(new Vector3(x, y, z + wave));
                    vertices.Add(new Vector3(x, y - Mathf.Lerp(1.1f, 2.8f, Halton(i + strip + 2, 3)), z + wave + 1.8f));
                    float wet = Mathf.Clamp01(1f - Mathf.Abs(y) / 8f);
                    colors.Add(new Color(0.86f, wet, 0.22f, 0.62f));
                    colors.Add(new Color(0.55f, wet * 0.8f, 0.42f, 0.46f));
                }

                for (int i = 0; i < 29; i++)
                {
                    int a = start + i * 2;
                    int b = a + 1;
                    int c = a + 2;
                    int d = a + 3;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(55f);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool InsideTerrainMask(float x, float z)
        {
            float ellipse = (x * x) / (162f * 162f) + ((z - 18f) * (z - 18f)) / (168f * 168f);
            float biteA = Mathf.Exp(-((x - 132f) * (x - 132f) + (z - 76f) * (z - 76f)) / (2f * 44f * 44f));
            float biteB = Mathf.Exp(-((x + 148f) * (x + 148f) + (z - 12f) * (z - 12f)) / (2f * 38f * 38f));
            float boundaryNoise = (RidgedNoise(x * 0.027f + 2.1f, z * 0.021f - 7.2f) - 0.5f) * 0.17f;
            return ellipse + biteA * 0.52f + biteB * 0.45f + boundaryNoise < 1.03f;
        }

        private static float EvaluateTerrainHeight(float x, float z, bool simplified)
        {
            float depthT = Mathf.InverseLerp(-118f, 178f, z);
            float y = Mathf.Lerp(16f, -24f, depthT);
            float center = RouteCenterX(z);
            float routeDistance = Mathf.Abs(x - center);
            float canyon = Mathf.Exp(-(routeDistance * routeDistance) / (2f * 23f * 23f)) * Mathf.SmoothStep(-52f, 128f, z);
            y -= canyon * Mathf.Lerp(5.5f, 14f, depthT);
            float ridgeBand = Mathf.Exp(-Mathf.Pow((routeDistance - 57f) / 19f, 2f));
            y += ridgeBand * Mathf.Lerp(3.5f, 11f, Mathf.Sin(depthT * Mathf.PI));
            float shoreLift = Mathf.SmoothStep(-10f, -88f, z);
            y += shoreLift * (6f + Mathf.Sin(x * 0.045f) * 2.4f);
            float collapsedShelf = Mathf.Exp(-Mathf.Pow((z - 24f) / 38f, 2f)) * Mathf.Exp(-Mathf.Pow((routeDistance - 38f) / 31f, 2f));
            y -= collapsedShelf * 4.8f;
            float terrace = Mathf.Sin((z + x * 0.22f) * 0.083f);
            y += Mathf.Sign(terrace) * Mathf.Pow(Mathf.Abs(terrace), 0.62f) * (simplified ? 0.7f : 1.55f);
            if (!simplified)
            {
                y += (RidgedNoise(x * 0.062f, z * 0.055f) - 0.5f) * 3.3f;
                y += (RidgedNoise(x * 0.18f + 4f, z * 0.16f - 3f) - 0.5f) * 0.85f;
            }
            return y;
        }

        private static Color EvaluateTerrainMaskColor(float x, float z, float y)
        {
            float center = RouteCenterX(z);
            float routeDistance = Mathf.Abs(x - center);
            float chip = Mathf.Clamp01(Mathf.Abs(RidgedNoise(x * 0.11f, z * 0.10f) - 0.5f) * 2.4f + Mathf.SmoothStep(42f, 72f, routeDistance) * 0.5f);
            float wet = Mathf.Clamp01(1f - Mathf.Abs(y) / 8f);
            float cavity = Mathf.Clamp01(Mathf.SmoothStep(0f, 26f, -y) * 0.65f + Mathf.SmoothStep(0f, 18f, 24f - routeDistance) * 0.35f);
            float blend = Mathf.Clamp01(Mathf.InverseLerp(-18f, 10f, -y) * 0.55f + Mathf.SmoothStep(-88f, -20f, z) * 0.35f);
            return new Color(chip, wet, cavity, blend);
        }

        private static float RouteCenterX(float z)
        {
            return Mathf.Sin((z + 42f) * 0.034f) * 28f - Mathf.Cos((z - 18f) * 0.019f) * 10f;
        }

        private static float RidgedNoise(float x, float y)
        {
            float n = Mathf.PerlinNoise(x + TerrainSeed * 0.0017f, y - TerrainSeed * 0.0023f);
            return 1f - Mathf.Abs(n * 2f - 1f);
        }

        private static float Halton(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;
            int value = Mathf.Max(index, 1);
            while (value > 0)
            {
                result += (value % radix) * fraction;
                value /= radix;
                fraction /= radix;
            }
            return result;
        }

        private static void DeprecateObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;
            if (!gameObject.name.StartsWith("DEPRECATED_", StringComparison.Ordinal))
                gameObject.name = "DEPRECATED_REJECTED_VISUAL_20260608__" + gameObject.name;
            gameObject.SetActive(false);
            EditorUtility.SetDirty(gameObject);
        }

        private static int DeprecateRejectedRecentRoots(Scene scene)
        {
            int count = 0;
            for (int i = 0; i < RejectedRecentRoots.Length; i++)
            {
                Transform transform = FindSceneTransform(scene, RejectedRecentRoots[i]);
                if (transform == null)
                    continue;
                DeprecateObject(transform.gameObject);
                count++;
            }
            return count;
        }

        private static void RemoveExistingRoot(Scene scene)
        {
            Transform existing = FindSceneTransform(scene, RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return gameObject;
        }

        private static Material LoadMaterial(string path, Color fallbackColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader);
            material.name = "MAT_Codex_TerrainFallback";
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", fallbackColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", fallbackColor);
            return material;
        }

        private static Scene EnsureTargetSceneLoaded()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                return activeScene;

            if (activeScene.IsValid() && activeScene.isDirty)
                EditorSceneManager.SaveOpenScenes();
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || transform.gameObject == null)
                    continue;
                if (transform.gameObject.scene != scene || EditorUtility.IsPersistent(transform.gameObject))
                    continue;
                if (string.Equals(transform.name, name, StringComparison.Ordinal))
                    return transform;
            }
            return null;
        }

        private static Camera ResolveSourceCamera()
        {
            Camera main = Camera.main;
            if (main != null)
                return main;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].gameObject.scene.IsValid() && !EditorUtility.IsPersistent(cameras[i]))
                    return cameras[i];
            }
            return null;
        }

        private static void TuneCamera(Camera camera)
        {
            if (camera == null)
                return;
            camera.transform.position = Origin + new Vector3(-112f, 34f, -112f);
            camera.transform.LookAt(Origin + new Vector3(4f, -2f, 28f), Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.14f, 0.16f, 1f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1600f;
            camera.fieldOfView = 54f;
            camera.useOcclusionCulling = false;
            camera.cullingMask = ~0;
            EditorUtility.SetDirty(camera);
        }

        private static void TuneLighting()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            bool tuned = false;
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || !light.gameObject.scene.IsValid() || EditorUtility.IsPersistent(light.gameObject))
                    continue;
                if (light.type != LightType.Directional)
                    continue;
                light.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
                light.color = new Color(0.74f, 0.93f, 0.96f, 1f);
                light.intensity = 1.28f;
                EditorUtility.SetDirty(light);
                tuned = true;
                break;
            }

            if (tuned)
                return;

            GameObject key = new GameObject("H8_CODEX_TERRAIN_CYAN_DAY_KEY_20260608");
            key.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            Light created = key.AddComponent<Light>();
            created.type = LightType.Directional;
            created.color = new Color(0.74f, 0.93f, 0.96f, 1f);
            created.intensity = 1.28f;
        }

        private static List<string> CaptureProofs(Camera sourceCamera)
        {
            List<string> paths = new List<string>();
            paths.Add(CaptureProof("terrain_macro_route_oblique", Origin + new Vector3(-122f, 48f, -128f), Origin + new Vector3(8f, -5f, 46f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("terrain_gameplay_height_route", Origin + new Vector3(-38f, 5.5f, -58f), Origin + new Vector3(14f, -7f, 62f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("terrain_shoreline_ledge_read", Origin + new Vector3(82f, 18f, -94f), Origin + new Vector3(-18f, 3f, -26f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("terrain_canyon_cross_section", Origin + new Vector3(116f, 23f, 44f), Origin + new Vector3(-18f, -12f, 84f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("terrain_topdown_mask_shape", Origin + new Vector3(0f, 235f, 32f), Origin + new Vector3(0f, -8f, 42f), 1280, 720, sourceCamera));
            return paths;
        }

        private static string CaptureProof(string name, Vector3 position, Vector3 target, int width, int height, Camera sourceCamera)
        {
            string absoluteRoot = AbsoluteProjectPath(ScreenshotRoot);
            Directory.CreateDirectory(absoluteRoot);
            string absolutePath = Path.Combine(absoluteRoot, name + "_20260608.png");

            GameObject cameraObject = new GameObject("H8_CODEX_TEMP_TERRAIN_PROOF_CAMERA");
            Camera camera = cameraObject.AddComponent<Camera>();
            if (sourceCamera != null)
                camera.CopyFrom(sourceCamera);
            camera.enabled = false;
            camera.cameraType = CameraType.Game;
            camera.cullingMask = ~0;
            camera.useOcclusionCulling = false;
            camera.forceIntoRenderTexture = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.14f, 0.16f, 1f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1600f;
            camera.fieldOfView = name.Contains("topdown") ? 42f : 55f;
            camera.transform.position = position;
            camera.transform.LookAt(target, Vector3.up);

            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, camera.backgroundColor);
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            return absolutePath;
        }

        private static Mesh UpsertMesh(string assetPath, Mesh source)
        {
            EnsureAssetDirectory(assetPath);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                UnityEngine.Object.DestroyImmediate(source);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(source, assetPath);
            return source;
        }

        private static void WriteSuccessReport(int deprecatedCount, Mesh terrainMesh, Mesh collisionMesh, Mesh ridgeMesh, Mesh ledgeMesh, Mesh strataMesh, List<string> proofPaths)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("status=TERRAIN_PROCEDURAL_ROUTE_AUTHORED_PENDING_VISUAL_REVIEW");
            builder.AppendLine("date=2026-06-08");
            builder.AppendLine("scene=" + ScenePath);
            builder.AppendLine("root=" + RootName);
            builder.AppendLine("seed=" + TerrainSeed.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("deprecatedRecentRoots=" + deprecatedCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("terrainSource=editor-authored deterministic procedural mesh, not runtime MapMagic proof");
            builder.AppendLine("routeGrammar=photic shelf -> shoreline ledges -> collapsed shelf ramp -> S canyon funnel -> exposed route ridges");
            builder.AppendLine("depthBand=0-250m photic salvage shelf visual baseline");
            builder.AppendLine("collisionProxy=COL_Codex_PhoticShelfCanyonProxy_20260608 MeshCollider low-poly proxy, separate from visual LOD0 mesh");
            AppendMeshSummary(builder, "terrainMesh", terrainMesh);
            AppendMeshSummary(builder, "collisionMesh", collisionMesh);
            AppendMeshSummary(builder, "ridgeMesh", ridgeMesh);
            AppendMeshSummary(builder, "ledgeMesh", ledgeMesh);
            AppendMeshSummary(builder, "strataMesh", strataMesh);
            builder.AppendLine("vertexColorContract=R exposed chip/mineral reveal; G wetness/waterline; B canyon/cavity AO; A sediment/material blend");
            builder.AppendLine("rejectionWatch=reject if flat, square-map, smooth blob, hidden by fog, or not route-readable from gameplay height");
            builder.AppendLine("[proofs]");
            for (int i = 0; i < proofPaths.Count; i++)
                builder.AppendLine(proofPaths[i]);

            string absolutePath = AbsoluteProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendMeshSummary(StringBuilder builder, string label, Mesh mesh)
        {
            builder.Append(label);
            builder.Append(" verts=");
            builder.Append(mesh != null ? mesh.vertexCount.ToString(CultureInfo.InvariantCulture) : "0");
            builder.Append(" tris=");
            builder.Append(mesh != null ? (mesh.triangles.Length / 3).ToString(CultureInfo.InvariantCulture) : "0");
            builder.Append(" bounds=");
            if (mesh != null)
            {
                Bounds bounds = mesh.bounds;
                builder.Append(bounds.center.ToString("F2"));
                builder.Append(" extents=");
                builder.Append(bounds.extents.ToString("F2"));
            }
            builder.AppendLine();
        }

        private static void WriteFailureReport(Exception exception)
        {
            string absolutePath = AbsoluteProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, "status=FAILED\n" + exception, Encoding.UTF8);
        }

        private static string AbsoluteProjectPath(string relativePath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory = assetPath.EndsWith("/", StringComparison.Ordinal) ? assetPath.TrimEnd('/') : Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif

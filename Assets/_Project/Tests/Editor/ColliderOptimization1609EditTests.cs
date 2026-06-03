using System;
using System.IO;
using Hecton8.Editor.ColliderOptimization1609;
using Hecton8.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Tests.Editor
{
    public sealed class ColliderOptimization1609EditTests
    {
        private const string EnginePath = "Assets/_Project/Editor/Physics/ColliderOptimizationEngine1609.cs";
        private const string WindowPath = "Assets/_Project/Editor/Physics/ColliderOptimizationWindow1609.cs";
        private const string RuntimeBakerPath = "Assets/_Project/Scripts/Physics/RuntimePhysicsBaker1609.cs";

        [Test]
        public void EngineIsEditorOnlyAndOwnsPrefabMutation()
        {
            AssertPathContainsEditorSegment(EnginePath);
            string source = ReadProjectFile(EnginePath);
            StringAssert.Contains("PrefabUtility.LoadPrefabContents", source);
            StringAssert.Contains("PrefabUtility.SaveAsPrefabAsset", source);
            StringAssert.Contains("Object.DestroyImmediate", source);
            StringAssert.Contains("MeshColliderFatalTriangleLimit = 500", source);
            StringAssert.Contains("ProxyMeshTriangleLimit = 200", source);
            StringAssert.Contains("ColliderOptimizationSettings1609", source);
            StringAssert.Contains("GlobalQualityWeight", source);
            StringAssert.Contains("MaxPrimitiveCollidersPerPrefab", source);
            StringAssert.Contains("ProxyPaddingMeters", source);
            StringAssert.Contains("VertexScratchCapacity = 65536", source);
            StringAssert.Contains("IndexScratchCapacity = 131072", source);
            StringAssert.Contains("ClearScratch(s_VertexScratch, VertexScratchCapacity)", source);
            StringAssert.Contains("scratch.Capacity = maxCapacity", source);
            StringAssert.Contains("GetComponentsInChildren(true, s_MeshColliderScratch)", source);
            StringAssert.Contains("CountMeshTrianglesNoAlloc", source);
            StringAssert.Contains("mesh.GetTriangles(s_IndexScratch, subMeshIndex, true)", source);
            Assert.IsFalse(source.Contains("GetComponentsInChildren<MeshCollider>(true)", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token(".", "triangles"), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("mesh.", "Get", "Indices(", "subMeshIndex)"), StringComparison.Ordinal));
        }

        [Test]
        public void RuntimeBakerHasNoPrivateUnityUpdateScheduler()
        {
            string source = ReadProjectFile(RuntimeBakerPath);
            StringAssert.Contains("RuntimePhysicsBakeCommitPhase1609", source);
            StringAssert.Contains("IsCommitPhaseAllowed", source);
            StringAssert.Contains("RuntimePhysicsBakeCommitPhase1609.PostSimulation", source);
            StringAssert.Contains("RuntimePhysicsBakeCommitPhase1609.VisualSync", source);
            StringAssert.Contains("RefreshBakeIdentityCold", source);
            StringAssert.Contains("meshEntityId = cachedCollisionProxyMeshEntityId", source);
            StringAssert.Contains("Runtime sharedMesh reassignment is forbidden.", source);
            Assert.IsFalse(source.Contains("RuntimePhysicsBakeJob1609", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Physics.BakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("UnityEngine.Physics.BakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("MeshColliderCookingOptions", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("Global", "Registry.", "Get"), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("Get", "Component("), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("void ", "Update("), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("void ", "Fixed", "Update("), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("void ", "Late", "Update("), StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token("Start", "Coroutine"), StringComparison.Ordinal));
            Assert.AreEqual(1, CountOccurrences(source, "GetEntityId()"));
        }

        [Test]
        public void RuntimeHotAndPhaseBodiesStayLookupFreeAndAllocationFree()
        {
            string source = ReadProjectFile(RuntimeBakerPath);

            AssertRuntimeBodyIsLookupLockAndAllocationFree(source, "public bool TryResolveBakeRequest");
            AssertRuntimeBodyIsLookupLockAndAllocationFree(source, "public bool CommitBakedCollider");
        }

        [Test]
        public void RuntimeBakerCommitGateIsPhaseSafeAndFailClosed()
        {
            Assert.IsFalse(RuntimePhysicsBaker1609.IsCommitPhaseAllowed(RuntimePhysicsBakeCommitPhase1609.Invalid));
            Assert.IsTrue(RuntimePhysicsBaker1609.IsCommitPhaseAllowed(RuntimePhysicsBakeCommitPhase1609.PostSimulation));
            Assert.IsTrue(RuntimePhysicsBaker1609.IsCommitPhaseAllowed(RuntimePhysicsBakeCommitPhase1609.VisualSync));

            string source = ReadProjectFile(RuntimeBakerPath);
            StringAssert.Contains("cachedCollisionProxyMeshKey == 0ul", source);
            StringAssert.Contains("EntityId.ToULong(cachedCollisionProxyMeshEntityId) != cachedCollisionProxyMeshKey", source);
            StringAssert.Contains("lastCommitPhase = (byte)phase", source);
            StringAssert.Contains("targetCollider.sharedMesh != collisionProxyMesh", source);

            int commitIndex = source.IndexOf("public bool CommitBakedCollider", StringComparison.Ordinal);
            int keyGuardIndex = source.IndexOf("cachedCollisionProxyMeshKey == 0ul", commitIndex, StringComparison.Ordinal);
            int assignmentIndex = source.IndexOf("targetCollider.sharedMesh = collisionProxyMesh", commitIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(commitIndex, 0);
            Assert.Greater(keyGuardIndex, commitIndex);
            Assert.AreEqual(-1, assignmentIndex);
        }

        [Test]
        public void QualitySettingsScaleContinuouslyAndStayInBounds()
        {
            ColliderOptimizationSettings1609 low = ColliderOptimizationSettings1609.FromGlobalQualityWeight(0f);
            ColliderOptimizationSettings1609 mid = ColliderOptimizationSettings1609.FromGlobalQualityWeight(0.5f);
            ColliderOptimizationSettings1609 high = ColliderOptimizationSettings1609.FromGlobalQualityWeight(1f);
            ColliderOptimizationSettings1609 nan = ColliderOptimizationSettings1609.FromGlobalQualityWeight(float.NaN);
            ColliderOptimizationSettings1609 infinity = ColliderOptimizationSettings1609.FromGlobalQualityWeight(float.PositiveInfinity);
            string engine = ReadProjectFile(EnginePath);

            Assert.AreEqual(0f, low.GlobalQualityWeight);
            Assert.AreEqual(0.5f, mid.GlobalQualityWeight);
            Assert.AreEqual(1f, high.GlobalQualityWeight);
            Assert.AreEqual(ColliderOptimizationEngine1609.DefaultGlobalQualityWeight, nan.GlobalQualityWeight);
            Assert.AreEqual(ColliderOptimizationEngine1609.DefaultGlobalQualityWeight, infinity.GlobalQualityWeight);
            StringAssert.Contains("ColliderOptimizationSettings1609.FromGlobalQualityWeight(settings.GlobalQualityWeight)", engine);
            StringAssert.Contains("float quality = defaults.GlobalQualityWeight", engine);
            Assert.AreEqual(ColliderOptimizationEngine1609.MinPrimitiveCollidersPerPrefab, low.MaxPrimitiveCollidersPerPrefab);
            Assert.AreEqual(ColliderOptimizationEngine1609.MaxPrimitiveCollidersPerPrefab, high.MaxPrimitiveCollidersPerPrefab);
            Assert.Greater(mid.MaxPrimitiveCollidersPerPrefab, low.MaxPrimitiveCollidersPerPrefab);
            Assert.Less(mid.MaxPrimitiveCollidersPerPrefab, high.MaxPrimitiveCollidersPerPrefab);
            Assert.AreEqual(ColliderOptimizationEngine1609.MaxProxyPaddingMeters, low.ProxyPaddingMeters, 0.0001f);
            Assert.AreEqual(ColliderOptimizationEngine1609.MinProxyPaddingMeters, high.ProxyPaddingMeters, 0.0001f);
            Assert.Greater(low.ProxyPaddingMeters, mid.ProxyPaddingMeters);
            Assert.Greater(mid.ProxyPaddingMeters, high.ProxyPaddingMeters);
        }

        [Test]
        public void ToolSupportsRequiredStrategiesAndFloraPurge()
        {
            string engine = ReadProjectFile(EnginePath);
            string window = ReadProjectFile(WindowPath);
            StringAssert.Contains("AggressivePrimitives", engine);
            StringAssert.Contains("ConvexHullWrapper", engine);
            StringAssert.Contains("PurgeAll", engine);
            StringAssert.Contains("PurgeFloraColliders", engine);
            StringAssert.Contains("FloraPurgeScopeLabel", engine);
            StringAssert.Contains("FindPrefabPaths(PrefabRoot)", engine);
            StringAssert.Contains("strategy == ColliderOptimizationStrategy1609.PurgeAll && !IsFloraPath(prefabPaths[i])", engine);
            StringAssert.Contains("if (!IsFloraPath(prefabPaths[i]))", engine);
            StringAssert.Contains("OptimizePrefabAsset(prefabPaths[i], ColliderOptimizationStrategy1609.PurgeAll, settings, ref report)", engine);
            StringAssert.Contains("IsNonFloraInteractablePath", engine);
            StringAssert.Contains("IsNonFloraPhysicalEnvironmentPath", engine);
            StringAssert.Contains("/Resources/Pickups/", engine);
            StringAssert.Contains("/PorousRock/", engine);
            StringAssert.Contains("_Rock_", engine);
            StringAssert.Contains("family_coral_low", engine);
            StringAssert.Contains("family_coral_brittle", engine);
            StringAssert.Contains("family_coral_branching", engine);
            StringAssert.Contains("1609 Purge Flora Colliders", window);
            StringAssert.Contains("FloraPurgeScopeLabel", window);
            StringAssert.Contains("PurgeAll is flora-filtered", window);
            StringAssert.Contains("lastReport = ColliderOptimizationEngine1609.PurgeFloraColliders()", window);
            StringAssert.Contains("EditorGUILayout.Slider", window);
            StringAssert.Contains("FromGlobalQualityWeight(globalQualityWeight)", window);
        }

        [Test]
        public void LayerMatrixAuditIsReadOnly()
        {
            string source = ReadProjectFile(EnginePath);
            StringAssert.Contains("Physics.GetIgnoreLayerCollision", source);
            StringAssert.Contains("ResolveLayerIndex(\"Flora\", fallback)", source);
            Assert.IsFalse(source.Contains("Physics.IgnoreLayerCollision", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("TagManager.asset", StringComparison.Ordinal));
        }

        [Test]
        public void ProxyMeshIsRootSpaceAndHasNoSameFrameJobReadback()
        {
            string source = ReadProjectFile(EnginePath);
            StringAssert.Contains("BuildRootAabbProxyMesh", source);
            StringAssert.Contains("LegacyCompoundRootName = \"__CompoundCollider_1609\"", source);
            StringAssert.Contains("GeneratedCompoundRootName = \"COL_CompoundProxy_1716\"", source);
            StringAssert.Contains("GeneratedConvexRootName = \"COL_ConvexProxy_1716\"", source);
            StringAssert.Contains("rootTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix", source);
            StringAssert.Contains("BuildRootAabbProxyMesh(root, root.name, settings.ProxyPaddingMeters)", source);
            StringAssert.Contains("float safePadding = Mathf.Clamp(paddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters)", source);
            StringAssert.Contains("ExpandMeshColliderRootBounds(meshCollider, rootTransform, ref fallbackMin, ref fallbackMax, ref hasFallbackBounds)", source);
            StringAssert.Contains("fallback.center = (fallbackMin + fallbackMax) * 0.5f", source);
            StringAssert.Contains("fallback.size = ClampColliderSize(fallbackMax - fallbackMin)", source);
            StringAssert.Contains("c >= 'A' && c <= 'Z'", source);
            StringAssert.Contains("hasStableCharacter ? new string(chars) : \"ColliderProxy\"", source);
            Assert.IsFalse(source.Contains(Token("char.", "IsLetter", "OrDigit"), StringComparison.Ordinal));
            StringAssert.Contains("!IsPrimaryCollisionVisual(filter)", source);
            StringAssert.Contains("Object.DestroyImmediate(proxy)", source);
            StringAssert.Contains("RemoveExistingProxyBakeArtifacts", source);
            StringAssert.Contains("RemoveGeneratedChildRoot(rootTransform, GeneratedConvexRootName)", source);
            StringAssert.Contains("AssetDatabase.DeleteAsset(oldProxyPath)", source);
            StringAssert.Contains("RemoveProxyBakerComponent(baker)", source);
            StringAssert.Contains("IsOwnedByBakerRoot", source);
            StringAssert.Contains("MeshCollider collider = colliderRoot.AddComponent<MeshCollider>()", source);
            Assert.IsFalse(source.Contains("MeshCollider collider = root.AddComponent<MeshCollider>()", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains(Token(".", "Complete()"), StringComparison.Ordinal));
        }

        [Test]
        public void OptimizerRejectsHotDependenciesLocksAndDuplicateLodColliders()
        {
            string engine = ReadProjectFile(EnginePath);
            string runtime = ReadProjectFile(RuntimeBakerPath);
            string combined = engine + "\n" + runtime;

            StringAssert.Contains("IsPrimaryCollisionVisual", engine);
            StringAssert.Contains("lods[0].renderers", engine);
            StringAssert.Contains("IsGeneratedCollisionName", engine);
            StringAssert.Contains("UnityEngine.PhysicsMaterial material = null", engine);
            StringAssert.Contains("ProxyMeshesDeleted", engine);
            StringAssert.Contains("collider.sharedMesh = proxy", engine);
            StringAssert.Contains("baker.ConfigureAuthoring(proxy, collider, bootstrap, true)", engine);

            Assert.IsFalse(combined.Contains(Token("Global", "Registry.", "Get"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("Data", "Vault"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("Write", "Lock"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("Monitor", ".Enter"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("lock", " ("), StringComparison.Ordinal));
        }

        [Test]
        public void EngineKeepsMetricsInMemoryAndAvoidsDiskJsonProof()
        {
            string engine = ReadProjectFile(EnginePath);
            string window = ReadProjectFile(WindowPath);
            string combined = engine + "\n" + window;

            StringAssert.Contains("ColliderOptimizationReport1609", engine);
            StringAssert.Contains("PrefabsFailed", engine);
            StringAssert.Contains("TryOptimizePrefabAsset", engine);
            StringAssert.Contains("catch (Exception exception)", engine);
            StringAssert.Contains("if (root != null)", engine);
            StringAssert.Contains("Prefabs Failed", window);
            Assert.IsFalse(combined.Contains(Token("PHYSICS_COLLIDER", "_OPTIMIZATION_1609", ".json"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("FormatReport", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("WriteReport", StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("File.", "WriteAllText"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains(Token("Directory.", "CreateDirectory"), StringComparison.Ordinal));
            Assert.IsFalse(combined.Contains("System.IO", StringComparison.Ordinal));
        }

        [Test]
        [Explicit("Creates 10000 temporary prefabs; run only on a dedicated collider CI/editor node.")]
        public void ExplicitMock10kPrefabFuzzer()
        {
            const string folder = "Assets/_Project/Tests/Generated/ColliderOptimization1609Fuzzer";
            EnsureFolder(folder);

            string meshPath = folder + "/MockHighPolyColliderMesh1609.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = CreateGridMesh1609("MockHighPolyColliderMesh1609", 16);
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < 10000; i++)
                {
                    string path = folder + "/PFB_MockCollider1609_" + i.ToString("00000") + ".prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                        continue;

                    GameObject gameObject = new GameObject("PFB_MockCollider1609_" + i.ToString("00000"));
                    MeshFilter filter = gameObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    gameObject.AddComponent<MeshRenderer>();
                    MeshCollider collider = gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(gameObject, path);
                    Object.DestroyImmediate(gameObject);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            ColliderOptimizationReport1609 report = ColliderOptimizationEngine1609.OptimizeFolder(folder, ColliderOptimizationStrategy1609.AggressivePrimitives);
            Assert.AreEqual(10000, report.PrefabsVisited);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            Assert.AreEqual(10000, guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.IsTrue(ColliderOptimizationEngine1609.ValidatePrefabMeshColliderBudget(prefab, out string failure), failure);
            }
        }

        private static void AssertPathContainsEditorSegment(string projectRelativePath)
        {
            string normalized = projectRelativePath.Replace('\\', '/');
            Assert.IsTrue(normalized.Contains("/Editor/"), projectRelativePath);
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int cursor = 0;
            while (cursor < source.Length)
            {
                int next = source.IndexOf(token, cursor, StringComparison.Ordinal);
                if (next < 0)
                    break;

                count++;
                cursor = next + token.Length;
            }

            return count;
        }

        private static void AssertRuntimeBodyIsLookupLockAndAllocationFree(string source, string signature)
        {
            string body = ExtractMethodBody(source, signature);
            Assert.IsFalse(body.Contains(Token("Global", "Registry.", "Get"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Get", "Component("), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Try", "Get", "Component("), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Data", "Vault"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Try", "Acquire", "Write", "Lock"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Release", "Write", "Lock"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("Monitor", ".Enter"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token("lock", " ("), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(Token(".", "Complete()"), StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains("new ", StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains("List<", StringComparison.Ordinal), signature);
            Assert.IsFalse(body.Contains(".ToString(", StringComparison.Ordinal), signature);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int openBrace = source.IndexOf((char)123, signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, signature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == (char)123)
                {
                    depth++;
                    continue;
                }

                if (source[i] != (char)125)
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(openBrace + 1, i - openBrace - 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }

        private static string Token(string a, string b)
        {
            return string.Concat(a, b);
        }

        private static string Token(string a, string b, string c)
        {
            return string.Concat(a, b, c);
        }

        private static string Token(string a, string b, string c, string d)
        {
            return string.Concat(a, b, c, d);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string child = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static Mesh CreateGridMesh1609(string name, int segments)
        {
            int vertexSide = segments + 1;
            Vector3[] vertices = new Vector3[vertexSide * vertexSide];
            int[] triangles = new int[segments * segments * 6];

            int vertexIndex = 0;
            for (int y = 0; y < vertexSide; y++)
            {
                for (int x = 0; x < vertexSide; x++)
                {
                    vertices[vertexIndex++] = new Vector3(x * 0.05f, 0f, y * 0.05f);
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < segments; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * vertexSide + x;
                    int b = a + 1;
                    int c = a + vertexSide;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}

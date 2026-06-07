using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hecton8.Editor.GeologyForge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GeologyForge1606StaticAuditTests
    {
        private const string GeneratorPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs";
        private const string JobsPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeJobs.cs";
        private const string TypesPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeTypes.cs";
        private const string LayoutValidatorPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyVertexLayoutValidator.cs";
        private const string WindowPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeWindow.cs";
        private const string TopographyForgeGeneratorPath = "Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs";
        private const string CsvPath = "Assets/_Project/Data/Geology/geology_generation_profiles.csv";
        private const string PrefabFolder = "Assets/_Project/BakedGeometry/Geology/Prefabs";
        private const string GeologyForgeFolder = "Assets/_Project/Scripts/Editor/GeologyForge";
        private const string GeologyGeneratorEntryFolder = "Assets/_Project/Editor/Generators/Geology";
        private const string InterpolatedStringAllocationToken = "$I";
        private const char OpenBraceChar = (char)123;
        private const char CloseBraceChar = (char)125;

        private static readonly string[] HotMethodNames =
        {
            "Execute",
            "Tick",
            "FixedTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "LateFrameTick",
            "OnUpdate"
        };

        private static readonly string[] HotPathBannedTokens =
        {
            "GlobalRegistry.Get<",
            "GlobalRegistry.Get(",
            ".GetComponent<",
            ".GetComponent(",
            ".TryGetComponent<",
            ".TryGetComponent(",
            "FindObject",
            "GameObject.Find",
            "Resources.Load",
            "GlobalDataVault",
            "DataVault",
            "AcquireWrite",
            "WriteLock",
            "EnterWrite",
            "new List<",
            "new Dictionary<",
            "new HashSet<",
            "new StringBuilder",
            ".ToArray(",
            "string.Format",
            InterpolatedStringAllocationToken,
            ".Select(",
            ".Where(",
            ".OrderBy(",
            "Activator.CreateInstance",
            "Marshal.Alloc",
            "GC.Alloc"
        };

        private static readonly string[] DataVaultWriteTokens =
        {
            "GlobalDataVault",
            "DataVault",
            "AcquireWrite",
            "WriteLock",
            "EnterWrite"
        };

        private static readonly string[] RuntimeEntryTokens =
        {
            "RuntimeInitializeOnLoadMethod",
            "ExecuteAlways",
            "ExecuteInEditMode",
            "MonoBehaviour"
        };

        [Test]
        public void GeneratorSerializesCollisionProxyAndPrefab()
        {
            string source = ReadProjectFile(GeneratorPath);
            StringAssert.Contains("COL_{stem}.asset", source);
            StringAssert.Contains("SavePrefabAsset", source);
            StringAssert.Contains("MeshCollider", source);
            StringAssert.Contains("CollisionProxyTriangleCount", source);
            StringAssert.Contains("CalculateCombinedVisualBounds", source);
            StringAssert.Contains("TryCleanupFailedCollisionAndPrefabSave", source);
            StringAssert.Contains("FileUtil.ReplaceFile", source);
            StringAssert.Contains("Physics.BakeMesh", source);
            StringAssert.Contains("CollisionCookingOptions", source);
            StringAssert.Contains("ResolveRendererStaticFlags", source);
            StringAssert.Contains("OccluderStaticMinimumVolumeCubicMeters", source);
            StringAssert.Contains("ConfigureStaticRockRenderer", source);
            StringAssert.Contains("MotionVectorGenerationMode.ForceNoMotion", source);
        }

        [Test]
        public void SelfAuditTreatsCollisionProxyAsPhysicsAsset()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeSelfAudit.cs");
            StringAssert.Contains("ValidateCollisionProxyMesh", source);
            StringAssert.Contains("ValidateGeneratedPrefabs", source);
            StringAssert.Contains("PREFAB_COLLIDER_TRIANGLE_BUDGET", source);
            StringAssert.Contains("COL_PROXY_TRIANGLE_BUDGET", source);
            StringAssert.Contains("PREFAB_COLLIDER_NOT_CONVEX", source);
            StringAssert.Contains("PREFAB_COLLIDER_BOUNDS_UNDER_VISUAL", source);
            StringAssert.Contains("PREFAB_COLLIDER_BAD_COOKING_OPTIONS", source);
            StringAssert.Contains("PREFAB_RENDERER_OCCLUDER_TOO_SMALL", source);
            StringAssert.Contains("PREFAB_RENDERER_MOTION_VECTOR", source);
            StringAssert.Contains("PREFAB_RENDERER_PROBE_USAGE", source);
            StringAssert.Contains("TryEncapsulateVisualMeshBounds", source);
            StringAssert.Contains("CalculateLocalToRootMatrix", source);
            StringAssert.Contains("ValidateOccluderStaticGate(path, filters, collider.sharedMesh", source);
            StringAssert.Contains("filter.sharedMesh == colliderMesh", source);
        }

        [Test]
        public void VertexColorPacksSedimentAndAoInRequiredChannels()
        {
            string source = ReadProjectFile(JobsPath);
            StringAssert.Contains("sedimentMask", source);
            StringAssert.Contains("aoDarkness", source);
            StringAssert.Contains("byte green", source);
            StringAssert.Contains("byte blue", source);
        }

        [Test]
        public void SeedDtoPinsQualityAsContinuousArm64Layout()
        {
            string types = ReadProjectFile(TypesPath);
            string validator = ReadProjectFile(LayoutValidatorPath);
            string generator = ReadProjectFile(GeneratorPath);
            string jobs = ReadProjectFile(JobsPath);

            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 64)]", types);
            StringAssert.Contains("internal struct GeologySeedDTO", types);
            StringAssert.Contains("[FieldOffset(56)]", types);
            StringAssert.Contains("public float GlobalQualityWeight", types);
            StringAssert.Contains("[FieldOffset(60)]", types);
            StringAssert.Contains("public uint ProfileHash", types);
            StringAssert.Contains("SeedDtoStrideBytes = 64", validator);
            StringAssert.Contains("ValidateOffset<GeologySeedDTO>(nameof(GeologySeedDTO.GlobalQualityWeight), 56)", validator);
            StringAssert.Contains("ValidateOffset<GeologySeedDTO>(nameof(GeologySeedDTO.ProfileHash), 60)", validator);
            StringAssert.Contains("profile.GlobalQualityWeight = math.saturate(FiniteOr(profile.GlobalQualityWeight, 0f))", generator);
            StringAssert.Contains("math.smoothstep(0f, 1f, q)", generator);
            StringAssert.Contains("math.saturate(GlobalQualityWeight)", jobs);
        }

        [Test]
        public void GeneratorClampsUnsafeBoundingVolumeInputs()
        {
            string types = ReadProjectFile(TypesPath);
            string generator = ReadProjectFile(GeneratorPath);
            string window = ReadProjectFile(WindowPath);

            StringAssert.Contains("MaximumRadiusMeters", types);
            StringAssert.Contains("MaximumHeightScale", types);
            StringAssert.Contains("MaximumFrequency", types);
            StringAssert.Contains("MaximumNoiseAmplitudeMeters", types);
            StringAssert.Contains("math.clamp(FiniteOr(profile.RadiusMeters, 2f), 0.25f, GeologyForgeConstants.MaximumRadiusMeters)", generator);
            StringAssert.Contains("math.clamp(FiniteOr(profile.HeightScale, 1f), 0.15f, GeologyForgeConstants.MaximumHeightScale)", generator);
            StringAssert.Contains("math.clamp(FiniteOr(profile.Frequency, 1f), 0.001f, GeologyForgeConstants.MaximumFrequency)", generator);
            StringAssert.Contains("math.clamp(FiniteOr(profile.NoiseAmplitude, 0f), 0f, GeologyForgeConstants.MaximumNoiseAmplitudeMeters)", generator);
            StringAssert.Contains("copiedProfiles.Add(SanitizeProfile(profiles[i]))", generator);
            StringAssert.Contains("internal static GeologyBakeProfile SanitizeForEditor", generator);
            StringAssert.Contains("return GeologyForgeGenerator.SanitizeForEditor(profile)", window);
            int reloadProfilesIndex = window.IndexOf("private void ReloadProfiles()");
            Assert.GreaterOrEqual(reloadProfilesIndex, 0);
            StringAssert.Contains("SanitizeProfilesInPlace(_profiles);", window.Substring(reloadProfilesIndex));
            int sanitizeProfilesIndex = window.IndexOf("private static void SanitizeProfilesInPlace(List<GeologyBakeProfile> profiles)");
            Assert.GreaterOrEqual(sanitizeProfilesIndex, 0);
            StringAssert.Contains("profiles[i] = GeologyForgeGenerator.SanitizeForEditor(profiles[i]);", window.Substring(sanitizeProfilesIndex));
            int selectProfileIndex = window.IndexOf("private void SelectProfile(int index)");
            Assert.GreaterOrEqual(selectProfileIndex, 0);
            string selectProfileBody = window.Substring(selectProfileIndex);
            StringAssert.Contains("profile = GeologyForgeGenerator.SanitizeForEditor(profile);", selectProfileBody);
            StringAssert.Contains("_profiles[_selectedProfileIndex] = profile;", selectProfileBody);
            int previewBuildIndex = window.IndexOf("public static void Build(GeologyBakeProfile profile)");
            Assert.GreaterOrEqual(previewBuildIndex, 0);
            StringAssert.Contains("profile = GeologyForgeGenerator.SanitizeForEditor(profile);", window.Substring(previewBuildIndex));
        }

        [Test]
        public void HotMethodsDoNotUseColdLookupsOrDataVaultWrites()
        {
            AssertHotMethodsCleanInFolder(GeologyForgeFolder);
            AssertHotMethodsCleanInFolder(GeologyGeneratorEntryFolder);
        }

        [Test]
        public void GeologyForgeDoesNotAcquireDataVaultWriteLocks()
        {
            AssertTokensAbsentInFolder(GeologyForgeFolder, DataVaultWriteTokens);
            AssertTokensAbsentInFolder(GeologyGeneratorEntryFolder, DataVaultWriteTokens);
        }

        [Test]
        public void GeologyGenerationSourcesRemainEditorOnly()
        {
            AssertFolderIsEditorOnly(GeologyForgeFolder);
            AssertFolderIsEditorOnly(GeologyGeneratorEntryFolder);
            AssertTokensAbsentInFolder(GeologyForgeFolder, RuntimeEntryTokens);
            AssertTokensAbsentInFolder(GeologyGeneratorEntryFolder, RuntimeEntryTokens);
        }

        private static void AssertHotMethodsCleanInFolder(string projectRelativeFolder)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), projectRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(Directory.Exists(root), projectRelativeFolder);
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                string sanitizedSource = RemoveCommentsAndStringLiterals(source);
                for (int m = 0; m < HotMethodNames.Length; m++)
                    AssertHotMethodBodyClean(files[i], sanitizedSource, HotMethodNames[m]);
            }
        }

        private static void AssertTokensAbsentInFolder(string projectRelativeFolder, string[] bannedTokens)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), projectRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(Directory.Exists(root), projectRelativeFolder);
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int f = 0; f < files.Length; f++)
            {
                string source = RemoveCommentsAndStringLiterals(File.ReadAllText(files[f]));
                for (int i = 0; i < bannedTokens.Length; i++)
                {
                    string banned = bannedTokens[i];
                    if (source.IndexOf(banned, System.StringComparison.Ordinal) >= 0)
                        Assert.Fail(files[f] + ": contains banned token " + banned);
                }
            }
        }

        private static void AssertFolderIsEditorOnly(string projectRelativeFolder)
        {
            string normalizedFolder = projectRelativeFolder.Replace('\\', '/');
            Assert.IsTrue(normalizedFolder.Contains("/Editor/"), projectRelativeFolder);

            string root = Path.Combine(Directory.GetCurrentDirectory(), projectRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(Directory.Exists(root), projectRelativeFolder);
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalizedFile = files[i].Replace('\\', '/');
                Assert.IsTrue(normalizedFile.Contains("/Editor/"), normalizedFile);
            }
        }

        [Test]
        public void CsvContainsAbyssalGeology1606Presets()
        {
            string source = ReadProjectFile(CsvPath);
            StringAssert.Contains("Sedimentary_Boulder", source);
            StringAssert.Contains("Volcanic_Basalt", source);
            StringAssert.Contains("Thermal_Vent_Spire", source);
        }

        [Test]
        public void EditorWindowFailsClosedAndSchedulesPreviewJob()
        {
            string generator = ReadProjectFile(GeneratorPath);
            string source = ReadProjectFile(WindowPath);
            StringAssert.Contains("TryLoadCsvProfiles", generator);
            StringAssert.Contains("bake request rejected", generator);
            StringAssert.Contains("GeologyForgeGenerator.TryLoadCsvProfiles(_profiles, \"using 1606 validation profiles\")", source);
            StringAssert.Contains("using 1606 validation profiles", source);
            StringAssert.Contains("AddAgent1606ValidationProfiles(_profiles)", source);
            StringAssert.Contains(".Schedule(count, 64)", source);
            StringAssert.Contains("previewHandle.Complete()", source);
        }

        [Test]
        public void HotMethodScannerIgnoresCommentsAndStringLiterals()
        {
            string source =
                "internal sealed class Probe\n" +
                "{\n" +
                "    public void Execute()\n" +
                "    {\n" +
                "        string text = \"GlobalRegistry.Get<IFoo>() { }\";\n" +
                "        string verbatim = @\"DataVault { }\";\n" +
                "        string raw = \"\"\"AcquireWrite { }\"\"\";\n" +
                "        char open = '{';\n" +
                "        // GetComponent<Renderer>() { }\n" +
                "        /* AcquireWrite { } */\n" +
                "    }\n" +
                "}\n";

            AssertHotMethodBodyClean("sanitizer-probe.cs", RemoveCommentsAndStringLiterals(source), "Execute");
        }

        [Test]
        public void HotMethodScannerRejectsInterpolatedStrings()
        {
            string source =
                "internal sealed class Probe\n" +
                "{\n" +
                "    public void Execute()\n" +
                "    {\n" +
                "        string text = $\"{42}\";\n" +
                "    }\n" +
                "}\n";

            Assert.Throws<AssertionException>(() =>
                AssertHotMethodBodyClean("interpolation-probe.cs", RemoveCommentsAndStringLiterals(source), "Execute"));
        }

        [Test]
        public void RuntimeMeshGenerationScannerDowngradesOnlyEditorGuardedFindings()
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "hecton_runtime_mesh_scanner_probe_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(
                tempPath,
                "using UnityEngine;\n" +
                "internal sealed class Probe : MonoBehaviour\n" +
                "{\n" +
                "#if UNITY_EDITOR\n" +
                "    private void EditorOnly()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "#if HECTON_AUTHORING_PREVIEW\n" +
                "    private void NestedEditorOnly()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "#endif\n" +
                "#if UNITY_EDITOR && HECTON_AUTHORING_PREVIEW\n" +
                "    private void CompoundEditorOnly()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "#endif\n" +
                "#endif\n" +
                "#if UNITY_EDITOR || DEVELOPMENT_BUILD\n" +
                "    private void EditorOrDevelopmentBuild()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "#endif\n" +
                "#if NOT_UNITY_EDITOR\n" +
                "    private void NotUnityEditorCustomSymbol()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "#endif\n" +
                "    [ContextMenu(\"Preview\")]\n" +
                "    private void Preview()\n" +
                "    {\n" +
                "        if (Application.isPlaying)\n" +
                "            return;\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "    private void RuntimeBuild()\n" +
                "    {\n" +
                "        Mesh mesh = new Mesh();\n" +
                "    }\n" +
                "    private void RuntimeMaterialReference()\n" +
                "    {\n" +
                "        asset.material = null;\n" +
                "    }\n" +
                "}\n");

            try
            {
                IList findings = ScanRuntimeMeshProbe(tempPath);

                AssertFinding(findings, "EditorOnly", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: true, editorPlayModeBlocked: false);
                AssertFinding(findings, "NestedEditorOnly", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: true, editorPlayModeBlocked: false);
                AssertFinding(findings, "CompoundEditorOnly", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: true, editorPlayModeBlocked: false);
                AssertFinding(findings, "EditorOrDevelopmentBuild", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: false, editorPlayModeBlocked: false);
                AssertFinding(findings, "NotUnityEditorCustomSymbol", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: false, editorPlayModeBlocked: false);
                AssertFinding(findings, "Preview", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: false, editorPlayModeBlocked: true);
                AssertFinding(findings, "RuntimeBuild", "RUNTIME_MESH_ALLOCATION", editorCompileGuarded: false, editorPlayModeBlocked: false);
                AssertFinding(findings, "RuntimeMaterialReference", "MATERIAL_PROPERTY_REFERENCE", editorCompileGuarded: false, editorPlayModeBlocked: false);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void TopographyForgeNativeArraysUseSentinelWrapper()
        {
            string source = ReadProjectFile(TopographyForgeGeneratorPath);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterNativeArray(array)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<"));
            StringAssert.Contains("NativeArray<T> array = new NativeArray<T>(length, allocator, options);", source);
            StringAssert.DoesNotContain("heights = new NativeArray<float>", source);
        }

        [Test]
        public void GeneratedPrefabsUseSeparateCollisionMeshWhenPresent()
        {
            string[] guids = AssetDatabase.FindAssets("GEN_Geology_ t:Prefab", new[] { PrefabFolder });
            if (guids == null || guids.Length == 0)
                Assert.Ignore("No generated geology prefabs present; run HECTON-8/Geology Forge/Bake 1606 Abyssal Validation Set.");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path);

                MeshCollider collider = prefab.GetComponent<MeshCollider>();
                Assert.NotNull(collider, path);
                Assert.NotNull(collider.sharedMesh, path);
                StringAssert.StartsWith("COL_", collider.sharedMesh.name);
                Assert.IsTrue(collider.convex, path);
                Assert.IsTrue((collider.cookingOptions & MeshColliderCookingOptions.CookForFasterSimulation) != 0, path);
                Assert.IsTrue((collider.cookingOptions & MeshColliderCookingOptions.EnableMeshCleaning) != 0, path);
                Assert.IsTrue((collider.cookingOptions & MeshColliderCookingOptions.WeldColocatedVertices) != 0, path);
                Assert.LessOrEqual(collider.sharedMesh.triangles.Length / 3, 192, path);

                MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                bool hasSeparateVisualMesh = false;
                bool hasVisualBounds = false;
                Bounds visualBounds = default;
                for (int f = 0; f < filters.Length; f++)
                {
                    Mesh visualMesh = filters[f].sharedMesh;
                    if (visualMesh != null && visualMesh != collider.sharedMesh)
                    {
                        hasSeparateVisualMesh = true;
                        Assert.IsTrue(
                            TryEncapsulateVisualMeshBounds(prefab.transform, filters[f].transform, visualMesh.bounds, ref visualBounds, ref hasVisualBounds),
                            path);
                    }
                }

                Assert.IsTrue(hasSeparateVisualMesh, path);
                Assert.IsTrue(hasVisualBounds, path);
                Bounds colliderBounds = collider.sharedMesh.bounds;
                Assert.LessOrEqual(colliderBounds.min.x, visualBounds.min.x + 0.001f, path);
                Assert.LessOrEqual(colliderBounds.min.y, visualBounds.min.y + 0.001f, path);
                Assert.LessOrEqual(colliderBounds.min.z, visualBounds.min.z + 0.001f, path);
                Assert.GreaterOrEqual(colliderBounds.max.x, visualBounds.max.x - 0.001f, path);
                Assert.GreaterOrEqual(colliderBounds.max.y, visualBounds.max.y - 0.001f, path);
                Assert.GreaterOrEqual(colliderBounds.max.z, visualBounds.max.z - 0.001f, path);

                float visualVolume = Mathf.Max(0f, visualBounds.size.x) * Mathf.Max(0f, visualBounds.size.y) * Mathf.Max(0f, visualBounds.size.z);
                for (int f = 0; f < filters.Length; f++)
                {
                    if (filters[f].sharedMesh == collider.sharedMesh)
                        continue;

                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(filters[f].gameObject);
                    Assert.IsTrue((flags & StaticEditorFlags.BatchingStatic) != 0, path);
                    Assert.IsTrue((flags & StaticEditorFlags.OccludeeStatic) != 0, path);
                    if (visualVolume < 2f)
                        Assert.IsFalse((flags & StaticEditorFlags.OccluderStatic) != 0, path);

                    MeshRenderer renderer = filters[f].GetComponent<MeshRenderer>();
                    Assert.NotNull(renderer, path);
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, renderer.shadowCastingMode, path);
                    Assert.IsTrue(renderer.receiveShadows, path);
                    Assert.AreEqual(MotionVectorGenerationMode.ForceNoMotion, renderer.motionVectorGenerationMode, path);
                    Assert.AreEqual(UnityEngine.Rendering.LightProbeUsage.BlendProbes, renderer.lightProbeUsage, path);
                    Assert.AreEqual(UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes, renderer.reflectionProbeUsage, path);
                }
            }
        }

        private static bool TryEncapsulateVisualMeshBounds(
            Transform root,
            Transform meshTransform,
            Bounds meshBounds,
            ref Bounds combinedBounds,
            ref bool hasBounds)
        {
            if (!IsFiniteBounds(meshBounds))
                return false;

            Matrix4x4 localToRoot = CalculateLocalToRootMatrix(root, meshTransform);
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 transformed = localToRoot.MultiplyPoint3x4(corner);
                        if (!IsFiniteVector(transformed))
                            return false;

                        if (!hasBounds)
                        {
                            combinedBounds = new Bounds(transformed, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(transformed);
                        }
                    }
                }
            }

            return true;
        }

        private static Matrix4x4 CalculateLocalToRootMatrix(Transform root, Transform node)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            Transform current = node;
            while (current != null && current != root)
            {
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
                current = current.parent;
            }

            return matrix;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector(bounds.center) &&
                   IsFiniteVector(bounds.extents) &&
                   bounds.extents.x >= 0f &&
                   bounds.extents.y >= 0f &&
                   bounds.extents.z >= 0f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFiniteFloat(value.x) &&
                   IsFiniteFloat(value.y) &&
                   IsFiniteFloat(value.z);
        }

        private static bool IsFiniteFloat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void AssertHotMethodBodyClean(string path, string source, string methodName)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int methodIndex = source.IndexOf(methodName, searchIndex, System.StringComparison.Ordinal);
                if (methodIndex < 0)
                    return;

                searchIndex = methodIndex + methodName.Length;
                if (!LooksLikeMethodNameAt(source, methodIndex, methodName))
                    continue;

                int openBrace = source.IndexOf(OpenBraceChar, searchIndex);
                if (openBrace < 0)
                    continue;

                int closeBrace = FindMatchingBrace(source, openBrace);
                if (closeBrace < 0)
                    Assert.Fail(path + ": unbalanced hot method body for " + methodName);

                string body = source.Substring(openBrace, closeBrace - openBrace + 1);
                for (int i = 0; i < HotPathBannedTokens.Length; i++)
                {
                    string banned = HotPathBannedTokens[i];
                    if (body.IndexOf(banned, System.StringComparison.Ordinal) >= 0)
                        Assert.Fail(path + ": hot method " + methodName + " contains banned token " + banned);
                }

                searchIndex = closeBrace + 1;
            }
        }

        private static bool LooksLikeMethodNameAt(string source, int index, string methodName)
        {
            if (index > 0)
            {
                char previous = source[index - 1];
                if (IsIdentifierChar(previous) || previous == '.')
                    return false;
            }

            int lineStart = source.LastIndexOf('\n', index);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int comment = source.IndexOf("//", lineStart, index - lineStart, System.StringComparison.Ordinal);
            if (comment >= 0)
                return false;

            int cursor = index + methodName.Length;
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
                cursor++;

            return cursor < source.Length && source[cursor] == '(';
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string RemoveCommentsAndStringLiterals(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            char[] sanitized = source.ToCharArray();
            int length = source.Length;
            int i = 0;
            while (i < length)
            {
                char c = source[i];
                if (c == '/' && i + 1 < length && source[i + 1] == '/')
                {
                    int start = i;
                    i += 2;
                    while (i < length && source[i] != '\n' && source[i] != '\r')
                        i++;
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                if (c == '/' && i + 1 < length && source[i + 1] == '*')
                {
                    int start = i;
                    i += 2;
                    while (i + 1 < length && !(source[i] == '*' && source[i + 1] == '/'))
                        i++;
                    i = i + 1 < length ? i + 2 : length;
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                if (IsInterpolatedRawStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeInterpolatedRawStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    StampSanitizedToken(sanitized, start, i, InterpolatedStringAllocationToken);
                    continue;
                }

                if (IsRawStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeRawStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                if (IsInterpolatedVerbatimStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeVerbatimStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    StampSanitizedToken(sanitized, start, i, InterpolatedStringAllocationToken);
                    continue;
                }

                if (IsVerbatimStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeVerbatimStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                if (IsInterpolatedRegularStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeRegularStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    StampSanitizedToken(sanitized, start, i, InterpolatedStringAllocationToken);
                    continue;
                }

                if (IsRegularStringStart(source, i))
                {
                    int start = i;
                    i = ConsumeRegularStringLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                if (c == '\'')
                {
                    int start = i;
                    i = ConsumeCharLiteral(source, i);
                    BlankRangePreserveNewlines(sanitized, start, i);
                    continue;
                }

                i++;
            }

            return new string(sanitized);
        }

        private static bool IsRawStringStart(string source, int index)
        {
            return CountQuoteRun(source, index) >= 3;
        }

        private static bool IsInterpolatedRawStringStart(string source, int index)
        {
            int cursor = index;
            int dollarCount = 0;
            while (cursor < source.Length && source[cursor] == '$')
            {
                dollarCount++;
                cursor++;
            }

            return dollarCount > 0 && CountQuoteRun(source, cursor) >= 3;
        }

        private static int ConsumeInterpolatedRawStringLiteral(string source, int index)
        {
            int cursor = index;
            while (cursor < source.Length && source[cursor] == '$')
                cursor++;
            return ConsumeRawStringLiteral(source, cursor);
        }

        private static int ConsumeRawStringLiteral(string source, int index)
        {
            int quoteCount = CountQuoteRun(source, index);
            int i = index + quoteCount;
            while (i < source.Length)
            {
                if (CountQuoteRun(source, i) >= quoteCount)
                    return i + quoteCount;
                i++;
            }

            return source.Length;
        }

        private static int CountQuoteRun(string source, int index)
        {
            int count = 0;
            while (index + count < source.Length && source[index + count] == '"')
                count++;
            return count;
        }

        private static bool IsVerbatimStringStart(string source, int index)
        {
            if (index + 1 < source.Length && source[index] == '@' && source[index + 1] == '"')
                return true;
            if (index + 2 < source.Length && source[index] == '$' && source[index + 1] == '@' && source[index + 2] == '"')
                return true;
            return index + 2 < source.Length && source[index] == '@' && source[index + 1] == '$' && source[index + 2] == '"';
        }

        private static bool IsInterpolatedVerbatimStringStart(string source, int index)
        {
            if (index + 2 < source.Length && source[index] == '$' && source[index + 1] == '@' && source[index + 2] == '"')
                return true;
            return index + 2 < source.Length && source[index] == '@' && source[index + 1] == '$' && source[index + 2] == '"';
        }

        private static int ConsumeVerbatimStringLiteral(string source, int index)
        {
            int i = source[index] == '@' && index + 1 < source.Length && source[index + 1] == '"' ? index + 2 : index + 3;
            while (i < source.Length)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return source.Length;
        }

        private static bool IsRegularStringStart(string source, int index)
        {
            if (source[index] == '"')
                return true;
            return index + 1 < source.Length && source[index] == '$' && source[index + 1] == '"';
        }

        private static bool IsInterpolatedRegularStringStart(string source, int index)
        {
            return index + 1 < source.Length && source[index] == '$' && source[index + 1] == '"';
        }

        private static int ConsumeRegularStringLiteral(string source, int index)
        {
            int i = source[index] == '$' ? index + 2 : index + 1;
            bool escaped = false;
            while (i < source.Length)
            {
                char c = source[i];
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    return i + 1;
                }

                i++;
            }

            return source.Length;
        }

        private static int ConsumeCharLiteral(string source, int index)
        {
            int i = index + 1;
            bool escaped = false;
            while (i < source.Length)
            {
                char c = source[i];
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '\'')
                {
                    return i + 1;
                }

                i++;
            }

            return source.Length;
        }

        private static void BlankRangePreserveNewlines(char[] target, int start, int end)
        {
            for (int i = start; i < end && i < target.Length; i++)
            {
                if (target[i] != '\n' && target[i] != '\r')
                    target[i] = ' ';
            }
        }

        private static void StampSanitizedToken(char[] target, int start, int end, string token)
        {
            int written = 0;
            for (int i = start; i < end && i < target.Length && written < token.Length; i++)
            {
                if (target[i] == '\n' || target[i] == '\r')
                    continue;
                target[i] = token[written];
                written++;
            }
        }

        private static int FindMatchingBrace(string source, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == OpenBraceChar)
                    depth++;
                else if (c == CloseBraceChar)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int searchStart = 0;
            while (searchStart < haystack.Length)
            {
                int index = haystack.IndexOf(needle, searchStart, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                searchStart = index + needle.Length;
            }

            return count;
        }

        private static IList ScanRuntimeMeshProbe(string path)
        {
            Type scannerType = typeof(GeologyForgeConstants).Assembly.GetType("Hecton8.Editor.GeologyForge.RuntimeMeshGenerationScanner");
            Assert.NotNull(scannerType);
            Type findingType = scannerType.GetNestedType("Finding", BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(findingType);
            Type listType = typeof(List<>).MakeGenericType(findingType);
            IList findings = (IList)Activator.CreateInstance(listType);
            MethodInfo scanFile = scannerType.GetMethod("ScanFile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(scanFile);

            scanFile.Invoke(null, new object[] { path, findings });
            return findings;
        }

        private static void AssertFinding(
            IList findings,
            string method,
            string kind,
            bool editorCompileGuarded,
            bool editorPlayModeBlocked)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                object finding = findings[i];
                Type findingType = finding.GetType();
                if (!string.Equals((string)findingType.GetField("Method").GetValue(finding), method, StringComparison.Ordinal) ||
                    !string.Equals((string)findingType.GetField("Kind").GetValue(finding), kind, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.AreEqual(editorCompileGuarded, (bool)findingType.GetField("EditorCompileGuarded").GetValue(finding), method);
                Assert.AreEqual(editorPlayModeBlocked, (bool)findingType.GetField("EditorPlayModeBlocked").GetValue(finding), method);
                return;
            }

            Assert.Fail("Missing scanner finding " + method + " / " + kind);
        }
    }
}

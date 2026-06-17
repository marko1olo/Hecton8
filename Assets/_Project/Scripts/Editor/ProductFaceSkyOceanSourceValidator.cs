using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class ProductFaceSkyOceanSourceValidator
    {
        private const string SkySystemPrefabPath = "Assets/_Project/Prefabs/Sky_System.prefab";
        private const string OceanCrestPrefabPath = "Assets/_Project/Prefabs/Ocean_Crest.prefab";
        private const string BuiltInPrimitiveMeshGuid = "0000000000000000e000000000000000";
        private const string SargassumMicroFaunaBoidsFullName = "Hecton8.World.SargassumMicroFaunaBoids";
        private const string SurfaceVisualFloorFailure =
            "Surface, sky, Aegir, moons, ocean skin, waterline, and photic shallows cannot be darkened, fogged, storm-hidden, or noir-graded to conceal weak primitive art.";

        private static readonly CrestInputPrimitiveException[] CrestInputPrimitiveExceptions =
        {
            new CrestInputPrimitiveException("Ocean_Crest/SargassumOilFilmInput", "Crest.RegisterAlbedoInput"),
            new CrestInputPrimitiveException("Ocean_Crest/SargassumWaveDampingInput", "Crest.RegisterAnimWavesInput"),
            new CrestInputPrimitiveException("Ocean_Crest/SargassumFoamDampingInput", "Crest.RegisterFoamInput"),
        };

        [MenuItem("Hecton8/Validation/Sky-Ocean Source Primitive Gate")]
        public static void ValidateFromMenu()
        {
            ProductFaceSkyOceanSourceValidationReport report = ValidateSources();
            for (int i = 0; i < report.Findings.Count; i++)
            {
                ProductFaceSkyOceanSourceValidationFinding finding = report.Findings[i];
                string line =
                    $"[ProductFaceSkyOceanSourceValidator] {finding.Severity} | {finding.Category} | {finding.AssetPath} | {finding.ComponentPath} | {finding.Message}";

                if (finding.Severity == ProductFaceSkyOceanSourceFindingSeverity.Fail)
                    Debug.LogError(line);
                else if (finding.Severity == ProductFaceSkyOceanSourceFindingSeverity.Warning)
                    Debug.LogWarning(line);
                else
                    Debug.Log(line);
            }

            if (report.FailureCount > 0)
            {
                Debug.LogError(
                    $"[ProductFaceSkyOceanSourceValidator] FAILED. CheckedPrefabs={report.CheckedPrefabCount}, Failures={report.FailureCount}, Warnings={report.WarningCount}. "
                    + SurfaceVisualFloorFailure);
            }
            else
            {
                Debug.Log(
                    $"[ProductFaceSkyOceanSourceValidator] No source-failure findings. CheckedPrefabs={report.CheckedPrefabCount}, Warnings={report.WarningCount}. "
                    + "This is static source inspection only; Unity screenshots, Frame Debugger, profiler, GC, and active scene proof remain pending.");
            }
        }

        public static ProductFaceSkyOceanSourceValidationReport ValidateSources()
        {
            ProductFaceSkyOceanSourceValidationReport report = new ProductFaceSkyOceanSourceValidationReport();
            ValidatePrefabSource(SkySystemPrefabPath, report);
            ValidatePrefabSource(OceanCrestPrefabPath, report);
            ValidateSargassumBoidMesh(report);
            ValidateAegirGasGiantSource(report);
            AddSceneOverrideBoundaryFindings(report);
            return report;
        }

        public static bool ValidateSources(out ProductFaceSkyOceanSourceValidationReport report)
        {
            report = ValidateSources();
            return report.FailureCount == 0;
        }

        private static void ValidatePrefabSource(string prefabPath, ProductFaceSkyOceanSourceValidationReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.AddFail(
                    ProductFaceSkyOceanSourceFindingCategory.MissingSourcePrefab,
                    prefabPath,
                    string.Empty,
                    "Missing required sky/ocean source prefab. Missing source is a failure, not a pass.");
                return;
            }

            report.CheckedPrefabCount++;

            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters == null || meshFilters.Length == 0)
            {
                report.AddWarning(
                    ProductFaceSkyOceanSourceFindingCategory.SourcePrefabPrimitiveRisk,
                    prefabPath,
                    prefab.name,
                    "No MeshFilter hierarchy found in source prefab. This validator cannot prove sky/ocean visual source quality from non-mesh render routes.");
                return;
            }

            for (int i = 0; i < meshFilters.Length; i++)
            {
                ValidateMeshFilter(prefabPath, prefab.transform, meshFilters[i], report);
            }
        }

        private static void ValidateMeshFilter(
            string prefabPath,
            Transform prefabRoot,
            MeshFilter meshFilter,
            ProductFaceSkyOceanSourceValidationReport report)
        {
            if (meshFilter == null)
                return;

            Mesh sharedMesh = meshFilter.sharedMesh;
            if (!IsUnityBuiltInPrimitiveMesh(sharedMesh, out long localFileId, out string meshName))
                return;

            string componentPath = BuildTransformPath(prefabRoot, meshFilter.transform);
            bool active = IsActiveInPrefabSource(meshFilter.transform);
            Renderer renderer = meshFilter.GetComponent<Renderer>();
            bool rendererEnabled = renderer != null && renderer.enabled;
            bool visibleActivePrimitive = active && rendererEnabled;

            if (TryResolveCrestInputException(componentPath, meshFilter.gameObject, renderer, out string exceptionReason))
            {
                report.AddInfo(
                    ProductFaceSkyOceanSourceFindingCategory.AcceptedCrestHiddenInputPrimitive,
                    prefabPath,
                    componentPath,
                    $"Accepted narrow Crest input-source primitive mesh '{meshName}' localFileID={localFileId}. {exceptionReason}");
                return;
            }

            if (visibleActivePrimitive)
            {
                ProductFaceSkyOceanSourceFindingCategory category =
                    prefabPath.Equals(SkySystemPrefabPath, StringComparison.OrdinalIgnoreCase)
                        ? ProductFaceSkyOceanSourceFindingCategory.SkyDomeBodyPrimitiveRisk
                        : ProductFaceSkyOceanSourceFindingCategory.SourcePrefabPrimitiveRisk;

                report.AddFail(
                    category,
                    prefabPath,
                    componentPath,
                    $"Visible active Unity built-in primitive mesh '{meshName}' localFileID={localFileId} is present in source prefab. {SurfaceVisualFloorFailure}");
                return;
            }

            report.AddWarning(
                ProductFaceSkyOceanSourceFindingCategory.SourcePrefabPrimitiveRisk,
                prefabPath,
                componentPath,
                $"Hidden or inactive Unity built-in primitive mesh '{meshName}' localFileID={localFileId} remains in source. Static hidden state is not runtime visual acceptance.");
        }

        private static void ValidateSargassumBoidMesh(ProductFaceSkyOceanSourceValidationReport report)
        {
            GameObject oceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OceanCrestPrefabPath);
            if (oceanPrefab == null)
                return;

            Component[] boids = ResolveComponentsByFullName(oceanPrefab, SargassumMicroFaunaBoidsFullName);
            if (boids == null || boids.Length == 0)
            {
                report.AddFail(
                    ProductFaceSkyOceanSourceFindingCategory.SargassumBoidPrimitiveRisk,
                    OceanCrestPrefabPath,
                    "Ocean_Crest",
                    "Missing SargassumMicroFaunaBoids component on Ocean_Crest source. The validator cannot prove the micro-fauna source mesh route.");
                return;
            }

            for (int i = 0; i < boids.Length; i++)
            {
                Component boidComponent = boids[i];
                string componentPath = BuildTransformPath(oceanPrefab.transform, boidComponent.transform);
                SerializedObject serializedBoids = new SerializedObject(boidComponent);
                SerializedProperty boidMeshProperty = serializedBoids.FindProperty("boidMesh");
                Mesh boidMesh = boidMeshProperty != null ? boidMeshProperty.objectReferenceValue as Mesh : null;

                if (boidMesh == null)
                {
                    report.AddFail(
                        ProductFaceSkyOceanSourceFindingCategory.SargassumBoidPrimitiveRisk,
                        OceanCrestPrefabPath,
                        componentPath,
                        "SargassumMicroFaunaBoids.boidMesh is missing. Missing source is a failure, not a pass.");
                    continue;
                }

                if (IsUnityBuiltInPrimitiveMesh(boidMesh, out long localFileId, out string meshName))
                {
                    report.AddFail(
                        ProductFaceSkyOceanSourceFindingCategory.SargassumBoidPrimitiveRisk,
                        OceanCrestPrefabPath,
                        componentPath,
                        $"SargassumMicroFaunaBoids.boidMesh points to Unity built-in primitive mesh '{meshName}' localFileID={localFileId}. Micro-fauna cannot ship as visible primitive cards; authored/generated mesh, VAT, or designed impostor proof is required.");
                    continue;
                }

                report.AddInfo(
                    ProductFaceSkyOceanSourceFindingCategory.SargassumBoidPrimitiveRisk,
                    OceanCrestPrefabPath,
                    componentPath,
                    $"SargassumMicroFaunaBoids.boidMesh uses non-built-in mesh source '{AssetDatabase.GetAssetPath(boidMesh)}'. This does not prove runtime visual quality.");
            }
        }

        private static void ValidateAegirGasGiantSource(ProductFaceSkyOceanSourceValidationReport report)
        {
            AegirGasGiantSourceValidationReport aegirReport = AegirGasGiantSourceValidator.ValidateSources();
            report.CheckedPrefabCount += aegirReport.CheckedPrefabCount;

            for (int i = 0; i < aegirReport.Findings.Count; i++)
            {
                AegirGasGiantSourceValidationFinding finding = aegirReport.Findings[i];
                ProductFaceSkyOceanSourceFindingCategory category =
                    finding.Category == AegirGasGiantSourceFindingCategory.SceneOverrideRisk
                        ? ProductFaceSkyOceanSourceFindingCategory.SceneOverrideRisk
                        : ProductFaceSkyOceanSourceFindingCategory.SkyDomeBodyPrimitiveRisk;

                string message = "Aegir gas giant source contract: " + finding.Message;
                if (finding.Severity == AegirGasGiantSourceFindingSeverity.Fail)
                {
                    report.AddFail(category, finding.AssetPath, finding.ComponentPath, message);
                }
                else if (finding.Severity == AegirGasGiantSourceFindingSeverity.Warning)
                {
                    report.AddWarning(category, finding.AssetPath, finding.ComponentPath, message);
                }
                else
                {
                    report.AddInfo(category, finding.AssetPath, finding.ComponentPath, message);
                }
            }
        }

        private static void AddSceneOverrideBoundaryFindings(ProductFaceSkyOceanSourceValidationReport report)
        {
            report.AddWarning(
                ProductFaceSkyOceanSourceFindingCategory.SceneOverrideRisk,
                SkySystemPrefabPath,
                "Sky_System",
                "Scene overrides may replace source sky mesh/material, but static prefab cleanup is not runtime visual acceptance. Future Unity owner must inspect the live scene instance, screenshots, Frame Debugger, profiler, and GC artifacts.");

            report.AddWarning(
                ProductFaceSkyOceanSourceFindingCategory.SceneOverrideRisk,
                OceanCrestPrefabPath,
                "Ocean_Crest",
                "Scene overrides may disable Crest input renderers, but static source and scene text do not prove first-frame hidden state, ocean beauty, waterline quality, or micro-fauna presentation.");
        }

        private static bool TryResolveCrestInputException(
            string componentPath,
            GameObject gameObject,
            Renderer renderer,
            out string reason)
        {
            reason = string.Empty;

            for (int i = 0; i < CrestInputPrimitiveExceptions.Length; i++)
            {
                CrestInputPrimitiveException exception = CrestInputPrimitiveExceptions[i];
                if (!componentPath.Equals(exception.ComponentPath, StringComparison.Ordinal))
                    continue;

                Component crestInput = ResolveComponentByFullName(gameObject, exception.RequiredComponentFullName);
                if (crestInput == null)
                {
                    reason = $"Path matches exception, but required component '{exception.RequiredComponentFullName}' is missing.";
                    return false;
                }

                bool rendererDisabled = renderer == null || !renderer.enabled;
                bool crestDisablesRenderer = SerializedBool(crestInput, "_disableRenderer");
                if (!rendererDisabled && !crestDisablesRenderer)
                {
                    reason = $"Path and component match '{exception.RequiredComponentFullName}', but renderer is enabled and _disableRenderer is false.";
                    return false;
                }

                reason =
                    $"Exact path '{exception.ComponentPath}' has '{exception.RequiredComponentFullName}' with rendererDisabled={rendererDisabled}, _disableRenderer={crestDisablesRenderer}. "
                    + "This is data-input source only, not visible product-face art.";
                return true;
            }

            return false;
        }

        private static Component ResolveComponentByFullName(GameObject gameObject, string fullName)
        {
            if (gameObject == null || string.IsNullOrEmpty(fullName))
                return null;

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                Type type = component != null ? component.GetType() : null;
                if (type != null && type.FullName == fullName)
                    return component;
            }

            return null;
        }

        private static Component[] ResolveComponentsByFullName(GameObject root, string fullName)
        {
            if (root == null || string.IsNullOrEmpty(fullName))
                return Array.Empty<Component>();

            Component[] allComponents = root.GetComponentsInChildren<Component>(true);
            List<Component> matches = new List<Component>(2);
            for (int i = 0; i < allComponents.Length; i++)
            {
                Component component = allComponents[i];
                Type type = component != null ? component.GetType() : null;
                if (type != null && type.FullName == fullName)
                    matches.Add(component);
            }

            return matches.ToArray();
        }

        private static bool SerializedBool(UnityEngine.Object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return false;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
        }

        private static bool IsUnityBuiltInPrimitiveMesh(Mesh mesh, out long localFileId, out string meshName)
        {
            localFileId = 0L;
            meshName = mesh != null ? mesh.name : "<null>";
            if (mesh == null)
                return false;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out localFileId) &&
                guid.Equals(BuiltInPrimitiveMeshGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            return IsUnityDefaultPrimitiveFallback(assetPath, mesh.name);
        }

        private static bool IsUnityDefaultPrimitiveFallback(string assetPath, string meshName)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return meshName == "Cube" ||
                   meshName == "Sphere" ||
                   meshName == "Capsule" ||
                   meshName == "Cylinder" ||
                   meshName == "Plane" ||
                   meshName == "Quad";
        }

        private static bool IsActiveInPrefabSource(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;

                current = current.parent;
            }

            return true;
        }

        private static string BuildTransformPath(Transform root, Transform leaf)
        {
            if (leaf == null)
                return string.Empty;

            Stack<string> names = new Stack<string>();
            Transform current = leaf;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private readonly struct CrestInputPrimitiveException
        {
            public readonly string ComponentPath;
            public readonly string RequiredComponentFullName;

            public CrestInputPrimitiveException(string componentPath, string requiredComponentFullName)
            {
                ComponentPath = componentPath;
                RequiredComponentFullName = requiredComponentFullName;
            }
        }
    }

    public sealed class ProductFaceSkyOceanSourceValidationReport
    {
        private readonly List<ProductFaceSkyOceanSourceValidationFinding> _findings =
            new List<ProductFaceSkyOceanSourceValidationFinding>(16);

        public IReadOnlyList<ProductFaceSkyOceanSourceValidationFinding> Findings => _findings;
        public int CheckedPrefabCount { get; internal set; }
        public int FailureCount { get; private set; }
        public int WarningCount { get; private set; }

        public void AddFail(
            ProductFaceSkyOceanSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            FailureCount++;
            Add(ProductFaceSkyOceanSourceFindingSeverity.Fail, category, assetPath, componentPath, message);
        }

        public void AddWarning(
            ProductFaceSkyOceanSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            WarningCount++;
            Add(ProductFaceSkyOceanSourceFindingSeverity.Warning, category, assetPath, componentPath, message);
        }

        public void AddInfo(
            ProductFaceSkyOceanSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            Add(ProductFaceSkyOceanSourceFindingSeverity.Info, category, assetPath, componentPath, message);
        }

        private void Add(
            ProductFaceSkyOceanSourceFindingSeverity severity,
            ProductFaceSkyOceanSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            _findings.Add(new ProductFaceSkyOceanSourceValidationFinding(
                severity,
                category,
                assetPath ?? string.Empty,
                componentPath ?? string.Empty,
                message ?? string.Empty));
        }
    }

    public readonly struct ProductFaceSkyOceanSourceValidationFinding
    {
        public readonly ProductFaceSkyOceanSourceFindingSeverity Severity;
        public readonly ProductFaceSkyOceanSourceFindingCategory Category;
        public readonly string AssetPath;
        public readonly string ComponentPath;
        public readonly string Message;

        public ProductFaceSkyOceanSourceValidationFinding(
            ProductFaceSkyOceanSourceFindingSeverity severity,
            ProductFaceSkyOceanSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            Severity = severity;
            Category = category;
            AssetPath = assetPath;
            ComponentPath = componentPath;
            Message = message;
        }
    }

    public enum ProductFaceSkyOceanSourceFindingSeverity
    {
        Info = 0,
        Warning = 1,
        Fail = 2
    }

    public enum ProductFaceSkyOceanSourceFindingCategory
    {
        MissingSourcePrefab = 0,
        SourcePrefabPrimitiveRisk = 1,
        SkyDomeBodyPrimitiveRisk = 2,
        AcceptedCrestHiddenInputPrimitive = 3,
        SargassumBoidPrimitiveRisk = 4,
        SceneOverrideRisk = 5
    }
}

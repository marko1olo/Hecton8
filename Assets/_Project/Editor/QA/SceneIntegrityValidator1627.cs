#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Guardian;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor.QA
{
    public static class SceneIntegrityValidator1627
    {
        public const string AgentId = "1627";
        public const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        public const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        public const string PrefabRootPath = "Assets/_Project/Prefabs";
        public const string StaticDataPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";

        private const string DataMonolithCompilerTypeName = "Hecton8.EditorValidation.H8DataMonolithCompiler";
        private const string MissingScriptNeedle = "m_Script: {fileID: 0";
        private const string MonoBehaviourNeedle = "--- !u!114";
        private const int TransformScratchCapacity = 4096;
        private const int ComponentScratchCapacity = 128;
        private const int RootScratchCapacity = 512;
        private const int MaxFindings = 512;

        // COLD ALLOC: editor validator scratch; never touched by runtime player loop.
        private static readonly List<GameObject> RootScratch = new List<GameObject>(RootScratchCapacity);
        // COLD ALLOC: editor validator traversal stack; reused across scene/prefab scans.
        private static readonly List<Transform> TransformScratch = new List<Transform>(TransformScratchCapacity);
        // COLD ALLOC: editor validator component scratch; prevents per-GameObject component arrays.
        private static readonly List<Component> ComponentScratch = new List<Component>(ComponentScratchCapacity);
        // COLD ALLOC: bootstrap graph output scratch for command/menu validation.
        private static readonly GlobalRegistryServiceSlot[] BootstrapOrderScratch =
            new GlobalRegistryServiceSlot[BootstrapRegistryCycleValidator.StartupNodeCount];

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute",
            "VisualSyncTick",
        };

        private static readonly string[] HotDependencyLookupTokens =
        {
            "GlobalRegistry.Get<",
            "GlobalRegistry.TryGet<",
            "GlobalRegistry.Resolve<",
            "GlobalRegistry.Get(",
            "GlobalRegistry.TryGet(",
            "GlobalRegistry.Resolve(",
            "GetComponent<",
            "TryGetComponent<",
            "GetComponents<",
            "GetComponentInChildren<",
            "GetComponentInParent<",
            "FindObjectOfType<",
            "FindObjectsOfType<",
            "GameObject.Find(",
            "GameObject.FindWithTag(",
            "Camera.main",
        };

        private static readonly string[] PresentationWriteTokens =
        {
            "Shader.SetGlobal",
            ".SetPropertyBlock(",
            ".SetFloat(",
            ".SetInt(",
            ".SetVector(",
            ".SetColor(",
            ".SetTexture(",
            ".SetBuffer(",
            ".SetMatrix(",
            "Graphics.Draw",
            "Graphics.RenderMesh",
            "Graphics.Blit",
            "CommandBuffer",
            ".material =",
            ".sharedMaterial =",
        };

        private static readonly string[] DataVaultWriteLockAcquireTokens =
        {
            ".TryAcquireWriteLock(",
            ".AcquireWriteLock(",
            ".TryLockBuffer(",
        };

        private static readonly string[] DataVaultWriteLockReleaseTokens =
        {
            ".ReleaseWriteLock(",
            ".TryUnlockBuffer(",
        };

        private static readonly string[] RequiredCampaignAssetPaths =
        {
            "Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset",
            "Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset",
            "Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset",
            "Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset",
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset",
            "Assets/_Project/Data/Tools/ToolMetadata_LaserCutter.asset",
            "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset",
            "Assets/_Project/Data/Crafting/Recipes/Recipe_BatteryCell.asset",
            "Assets/_Project/Data/Items/Tools/Item_Tool_LaserCutter.asset",
            "Assets/_Project/Data/Tools/Modules/ToolModule_StandardBattery.asset",
            "Assets/_Project/Data/Tools/tool_hardware_specs.csv",
        };

        private static readonly string[] ForbiddenLegacyPathFragments =
        {
            "/EasySave3/",
            "/DOTween/",
            "/MasterAudio/",
            "\\EasySave3\\",
            "\\DOTween\\",
            "\\MasterAudio\\",
        };

        [MenuItem("Hecton8/QA/1627/Run Campaign 00 Migration Validation", false, 16270)]
        public static void RunMenuValidation()
        {
            SceneIntegrityValidationResult result = RunValidation(repairMissingScriptShells: false);
            LogResult(result);
        }

        [MenuItem("Hecton8/QA/1627/Repair Missing Script Shells Then Validate", false, 16271)]
        public static void RepairMissingScriptShellsThenValidate()
        {
            SceneIntegrityValidationResult result = RunValidation(repairMissingScriptShells: true);
            LogResult(result);
        }

        public static void RunCommandLineValidation()
        {
            SceneIntegrityValidationResult result = RunValidation(repairMissingScriptShells: false);
            LogResult(result);
            if (!result.passed)
                EditorApplication.Exit(1627);
        }

        public static SceneIntegrityValidationResult RunValidation(bool repairMissingScriptShells)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            SceneIntegrityValidationResult result = new SceneIntegrityValidationResult();
            result.agentId = AgentId;
            result.evidenceClass = "UNITY_EDITOR_API_AND_STATIC_SOURCE";
            result.worldScenePath = WorldScenePath;
            result.bootstrapScenePath = BootstrapScenePath;
            result.prefabRootPath = PrefabRootPath;
            result.staticDataPath = StaticDataPath;

            ValidateSerializedYamlFile(WorldScenePath, "world_scene_yaml", result);
            ValidateSerializedYamlFiles(PrefabRootPath, result);
            ValidateCampaign00Data(result);
            ValidateStaticDataBlob(result);
            ValidateBootstrapGraph(result);
            ValidateForbiddenLegacyResidue(result);
            ScanApexIntegratorContracts(result);

            if (TryPrepareSceneAccess(result))
            {
                SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
                try
                {
                    ValidateSceneWithEditorApi(BootstrapScenePath, false, repairMissingScriptShells, result);
                    ValidateSceneWithEditorApi(WorldScenePath, true, repairMissingScriptShells, result);
                }
                finally
                {
                    RestoreSceneSetup(setup, result);
                }
            }

            ValidatePrefabsWithEditorApi(repairMissingScriptShells, result);

            stopwatch.Stop();
            result.elapsedMicroseconds = StopwatchTicksToMicroseconds(stopwatch.ElapsedTicks);
            result.passed = result.fatalCount == 0;

            return result;
        }

        public static bool ValidateSerializedYamlTextForMissingScripts(
            string yaml,
            out int monoBehaviourCount,
            out int missingScriptCount)
        {
            monoBehaviourCount = 0;
            missingScriptCount = 0;
            if (string.IsNullOrEmpty(yaml))
                return true;

            int cursor = 0;
            while (cursor < yaml.Length)
            {
                int monoIndex = yaml.IndexOf(MonoBehaviourNeedle, cursor, StringComparison.Ordinal);
                if (monoIndex < 0)
                    break;

                monoBehaviourCount++;
                int nextIndex = yaml.IndexOf("\n--- !u!", monoIndex + MonoBehaviourNeedle.Length, StringComparison.Ordinal);
                int blockEnd = nextIndex >= 0 ? nextIndex : yaml.Length;
                int missingIndex = yaml.IndexOf(MissingScriptNeedle, monoIndex, blockEnd - monoIndex, StringComparison.Ordinal);
                if (missingIndex >= 0)
                    missingScriptCount++;

                cursor = blockEnd;
            }

            return missingScriptCount == 0;
        }

        public static bool TryValidateBootstrapGraphForTest(
            GlobalRegistryServiceSlot[] nodes,
            BootstrapRegistryDependencyEdge[] edges,
            GlobalRegistryServiceSlot[] executionOrder,
            out int executionOrderCount)
        {
            return BootstrapRegistryCycleValidator.TryBuildExecutionOrder(
                nodes,
                edges,
                executionOrder,
                out executionOrderCount);
        }

        private static void ValidateSceneWithEditorApi(
            string scenePath,
            bool requireSceneGuard,
            bool repairMissingScriptShells,
            SceneIntegrityValidationResult result)
        {
            if (!File.Exists(scenePath))
            {
                AddFinding(result, "FATAL", "STATIC_SOURCE", "SCENE_FILE_MISSING", scenePath, 0, "Scene file is absent.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "SCENE_OPEN_FAILED", scenePath, 0, "EditorSceneManager did not load scene.");
                return;
            }

            RootScratch.Clear();
            scene.GetRootGameObjects(RootScratch);
            result.sceneRootCount += RootScratch.Count;

            bool sawBootstrapController = false;
            bool sawGameBootstrapper = false;
            bool sawAnySceneGuard = false;
            bool sawCamera = false;
            bool sawMainCamera = false;
            bool sawSceneGuardOnMainCamera = false;

            for (int i = 0; i < RootScratch.Count; i++)
            {
                GameObject root = RootScratch[i];
                if (root == null)
                    continue;

                TransformScratch.Clear();
                CollectTransforms(root.transform, TransformScratch);

                for (int t = 0; t < TransformScratch.Count; t++)
                {
                    Transform transform = TransformScratch[t];
                    if (transform == null)
                        continue;

                    GameObject go = transform.gameObject;
                    ScanGameObjectForMissingScripts(scenePath, transform, go, repairMissingScriptShells, result);

                    if (go.TryGetComponent(out BootstrapController _))
                        sawBootstrapController = true;
                    if (go.TryGetComponent(out GameBootstrapper _))
                        sawGameBootstrapper = true;
                    if (go.TryGetComponent(out SceneGuard _))
                        sawAnySceneGuard = true;
                    if (go.TryGetComponent(out Camera _))
                    {
                        sawCamera = true;
                        bool isMain = go.CompareTag("MainCamera") ||
                            string.Equals(go.name, "Main Camera", StringComparison.Ordinal) ||
                            string.Equals(go.name, "World_Observer_Camera", StringComparison.Ordinal);
                        if (isMain)
                        {
                            sawMainCamera = true;
                            if (go.TryGetComponent(out SceneGuard _))
                                sawSceneGuardOnMainCamera = true;
                        }
                    }
                }
            }

            if (string.Equals(scenePath, BootstrapScenePath, StringComparison.Ordinal) &&
                !sawBootstrapController &&
                !sawGameBootstrapper)
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "BOOTSTRAP_OWNER_MISSING", scenePath, 0, "00_BOOTSTRAP has no BootstrapController/GameBootstrapper route owner.");
            }

            if (requireSceneGuard)
            {
                if (!sawAnySceneGuard)
                    AddFinding(result, "FATAL", "UNITY_EDITOR_API", "SCENE_GUARD_MISSING", scenePath, 0, "World scene has no SceneGuard component.");
                if (!sawCamera)
                    AddFinding(result, "FATAL", "UNITY_EDITOR_API", "WORLD_CAMERA_MISSING", scenePath, 0, "World scene has no camera.");
                else if (!sawMainCamera)
                    AddFinding(result, "WARNING", "UNITY_EDITOR_API", "WORLD_MAIN_CAMERA_ALIAS", scenePath, 0, "No tagged MainCamera found; accepted alias requires explicit review.");
                else if (!sawSceneGuardOnMainCamera)
                    AddFinding(result, "FATAL", "UNITY_EDITOR_API", "SCENE_GUARD_NOT_ON_MAIN_CAMERA", scenePath, 0, "Main/world observer camera lacks SceneGuard.");
            }

            if (repairMissingScriptShells && scene.isDirty)
                EditorSceneManager.SaveScene(scene);

            RootScratch.Clear();
            TransformScratch.Clear();
        }

        private static void ValidatePrefabsWithEditorApi(bool repairMissingScriptShells, SceneIntegrityValidationResult result)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRootPath });
            result.prefabCount = prefabGuids != null ? prefabGuids.Length : 0;
            if (prefabGuids == null)
                return;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                GameObject root = null;
                bool dirty = false;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null)
                    {
                        AddFinding(result, "FATAL", "UNITY_EDITOR_API", "PREFAB_LOAD_FAILED", path, 0, "PrefabUtility returned null.");
                        continue;
                    }

                    TransformScratch.Clear();
                    CollectTransforms(root.transform, TransformScratch);
                    for (int t = 0; t < TransformScratch.Count; t++)
                    {
                        Transform transform = TransformScratch[t];
                        if (transform == null)
                            continue;

                        int before = result.prefabMissingScripts;
                        ScanGameObjectForMissingScripts(path, transform, transform.gameObject, repairMissingScriptShells, result);
                        dirty |= repairMissingScriptShells && result.prefabMissingScripts > before;
                    }

                    if (dirty)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    TransformScratch.Clear();
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (repairMissingScriptShells)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void ScanGameObjectForMissingScripts(
            string ownerPath,
            Transform transform,
            GameObject go,
            bool repairMissingScriptShells,
            SceneIntegrityValidationResult result)
        {
            if (go == null)
                return;

            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0)
            {
                if (ownerPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    result.prefabMissingScripts += missing;
                else
                    result.sceneMissingScripts += missing;

                AddFinding(
                    result,
                    "FATAL",
                    "UNITY_EDITOR_API",
                    "MISSING_SCRIPT_COMPONENT",
                    ownerPath,
                    0,
                    BuildTransformPath(transform) + " has " + missing.ToString(CultureInfo.InvariantCulture) + " missing script component(s).");

                if (repairMissingScriptShells)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }

            ComponentScratch.Clear();
            go.GetComponents<Component>(ComponentScratch);
            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                Component component = ComponentScratch[i];
                if (component == null)
                    continue;

                MonoBehaviour monoBehaviour = component as MonoBehaviour;
                if (monoBehaviour == null)
                    continue;

                MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (script == null)
                {
                    AddFinding(
                        result,
                        "FATAL",
                        "UNITY_EDITOR_API",
                        "MONOSCRIPT_UNRESOLVED",
                        ownerPath,
                        0,
                        BuildTransformPath(transform) + " component " + component.GetType().FullName + " has null MonoScript.");
                }
            }

            ComponentScratch.Clear();
        }

        private static void ValidateSerializedYamlFiles(string rootPath, SceneIntegrityValidationResult result)
        {
            if (!Directory.Exists(rootPath))
            {
                AddFinding(result, "FATAL", "STATIC_SOURCE", "PREFAB_ROOT_MISSING", rootPath, 0, "Prefab root is absent.");
                return;
            }

            string[] paths = Directory.GetFiles(rootPath, "*.prefab", SearchOption.AllDirectories);
            for (int i = 0; i < paths.Length; i++)
            {
                string projectPath = NormalizeProjectPath(paths[i]);
                ValidateSerializedYamlFile(projectPath, "prefab_yaml", result);
            }
        }

        private static void ValidateSerializedYamlFile(
            string path,
            string evidenceLabel,
            SceneIntegrityValidationResult result)
        {
            if (!File.Exists(path))
                return;

            int lineNumber = 0;
            bool inMonoBehaviour = false;
            int monoStartLine = 0;
            string scriptGuid = null;
            string classIdentifier = null;
            int scriptFileId = int.MinValue;

            foreach (string line in File.ReadLines(path))
            {
                lineNumber++;
                if (line.StartsWith("--- !u!114 ", StringComparison.Ordinal))
                {
                    FlushYamlMonoRecord(path, monoStartLine, scriptFileId, scriptGuid, classIdentifier, evidenceLabel, result);
                    inMonoBehaviour = true;
                    monoStartLine = lineNumber;
                    scriptGuid = null;
                    classIdentifier = null;
                    scriptFileId = int.MinValue;
                    continue;
                }

                if (inMonoBehaviour && line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    FlushYamlMonoRecord(path, monoStartLine, scriptFileId, scriptGuid, classIdentifier, evidenceLabel, result);
                    inMonoBehaviour = false;
                }

                if (!inMonoBehaviour)
                    continue;

                if (TryReadScriptReference(line, out int parsedScriptFileId, out string parsedScriptGuid))
                {
                    scriptFileId = parsedScriptFileId;
                    scriptGuid = parsedScriptGuid;
                }

                if (line.TrimStart().StartsWith("m_EditorClassIdentifier:", StringComparison.Ordinal))
                    classIdentifier = line.Substring(line.IndexOf(':') + 1).Trim();
            }

            if (inMonoBehaviour)
                FlushYamlMonoRecord(path, monoStartLine, scriptFileId, scriptGuid, classIdentifier, evidenceLabel, result);
        }

        private static void FlushYamlMonoRecord(
            string path,
            int lineNumber,
            int scriptFileId,
            string scriptGuid,
            string classIdentifier,
            string evidenceLabel,
            SceneIntegrityValidationResult result)
        {
            if (lineNumber <= 0)
                return;

            result.yamlMonoBehaviourCount++;
            if (scriptFileId == 0)
            {
                result.yamlMissingScriptFileId0++;
                AddFinding(result, "FATAL", "STATIC_SOURCE", "YAML_MISSING_SCRIPT_FILEID_ZERO", path, lineNumber, evidenceLabel + " missing script shell.");
                return;
            }

            if (!string.IsNullOrEmpty(classIdentifier) &&
                classIdentifier.StartsWith("Assembly-CSharp::", StringComparison.Ordinal))
            {
                result.yamlAssemblyCSharpIdentifiers++;
            }

            if (string.IsNullOrEmpty(scriptGuid) || IsExternalPackageClassIdentifier(classIdentifier))
                return;

            string resolvedPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                result.yamlUnresolvedProjectScriptGuids++;
                AddFinding(
                    result,
                    "FATAL",
                    "STATIC_SOURCE",
                    "YAML_UNRESOLVED_PROJECT_SCRIPT_GUID",
                    path,
                    lineNumber,
                    "guid=" + scriptGuid + " class=" + (classIdentifier ?? string.Empty));
            }
        }

        private static void ValidateCampaign00Data(SceneIntegrityValidationResult result)
        {
            for (int i = 0; i < RequiredCampaignAssetPaths.Length; i++)
            {
                string path = RequiredCampaignAssetPaths[i];
                if (!File.Exists(path))
                    AddFinding(result, "FATAL", "STATIC_SOURCE", "CAMPAIGN_ASSET_MISSING", path, 0, "Required Campaign 00 source asset is missing.");
            }

            string copperGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");
            string copperWireGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset");
            string batteryGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset");

            RequireFileContains(result, "Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset", "stableId: Data_Copper", "CAMPAIGN_DATA_COPPER_STABLE_ID");
            RequireFileContains(result, "Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset", "stableId: Comp_CopperWire", "CAMPAIGN_COPPER_WIRE_STABLE_ID");
            RequireFileContains(result, "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset", "stableId: Comp_BatteryCell", "CAMPAIGN_BATTERY_CELL_STABLE_ID");
            RequireFileContains(result, "Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset", "questId: quest_copper_sample", "CAMPAIGN_QUEST_ID");
            RequireFileContains(result, "Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset", "completionId: Data_Copper", "CAMPAIGN_QUEST_COMPLETION");
            RequireFileContains(result, "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset", "stableId: resource.node.copper_vein", "CAMPAIGN_COPPER_VEIN_ID");
            RequireFileContains(result, "Assets/_Project/Data/Tools/ToolMetadata_LaserCutter.asset", "toolID: tool_laser_cutter", "CAMPAIGN_LASER_CUTTER_ID");
            RequireFileContains(result, "Assets/_Project/Data/Tools/tool_hardware_specs.csv", "tool_laser_cutter,", "CAMPAIGN_LASER_CUTTER_HARDWARE_ROW");

            if (!string.IsNullOrEmpty(copperGuid))
                RequireFileContains(result, "Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset", "guid: " + copperGuid, "CAMPAIGN_RECIPE_COPPER_INPUT");
            if (!string.IsNullOrEmpty(copperWireGuid))
                RequireFileContains(result, "Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset", "guid: " + copperWireGuid, "CAMPAIGN_RECIPE_COPPER_WIRE_OUTPUT");
            if (!string.IsNullOrEmpty(copperGuid))
                RequireFileContains(result, "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset", "guid: " + copperGuid, "CAMPAIGN_RESOURCE_NODE_COPPER_YIELD");
            if (!string.IsNullOrEmpty(batteryGuid))
                RequireFileContains(result, "Assets/_Project/Data/Crafting/Recipes/Recipe_BatteryCell.asset", "guid: " + batteryGuid, "CAMPAIGN_BATTERY_RECIPE_OUTPUT");
        }

        private static void ValidateStaticDataBlob(SceneIntegrityValidationResult result)
        {
            if (!File.Exists(StaticDataPath))
            {
                AddFinding(result, "FATAL", "STATIC_SOURCE", "STATIC_DATA_BLOB_MISSING", StaticDataPath, 0, "Active static_data.h8bin is absent.");
                return;
            }

            FileInfo info = new FileInfo(StaticDataPath);
            result.staticDataBytes = info.Length;
            if (info.Length <= 0L)
            {
                AddFinding(result, "FATAL", "STATIC_SOURCE", "STATIC_DATA_BLOB_EMPTY", StaticDataPath, 0, "Active static_data.h8bin is empty.");
                return;
            }

            Type compilerType = FindType(DataMonolithCompilerTypeName);
            if (compilerType == null)
            {
                AddFinding(result, "WARNING", "UNITY_EDITOR_API", "STATIC_DATA_COMPILER_TYPE_MISSING", StaticDataPath, 0, "H8DataMonolithCompiler type was not loaded; blob semantic validation skipped.");
                return;
            }

            MethodInfo method = compilerType.GetMethod(
                "TryValidateBlobFile",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(string).MakeByRefType() },
                null);

            if (method == null)
            {
                AddFinding(result, "WARNING", "UNITY_EDITOR_API", "STATIC_DATA_VALIDATOR_METHOD_MISSING", StaticDataPath, 0, "H8DataMonolithCompiler.TryValidateBlobFile was not found.");
                return;
            }

            object[] args = { StaticDataPath, null };
            bool valid;
            try
            {
                valid = (bool)method.Invoke(null, args);
            }
            catch (TargetInvocationException ex)
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "STATIC_DATA_VALIDATOR_THROW", StaticDataPath, 0, ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                return;
            }

            result.staticDataCompilerValidated = valid;
            if (!valid)
            {
                string error = args[1] as string;
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "STATIC_DATA_BLOB_INVALID", StaticDataPath, 0, string.IsNullOrEmpty(error) ? "TryValidateBlobFile returned false." : error);
            }
        }

        private static void ValidateBootstrapGraph(SceneIntegrityValidationResult result)
        {
            bool graphValid = BootstrapRegistryCycleValidator.TryValidateStartupGraph(
                BootstrapOrderScratch,
                out int executionOrderCount);
            result.bootstrapNodeCount = BootstrapRegistryCycleValidator.StartupNodeCount;
            result.bootstrapOrderCount = executionOrderCount;

            if (!graphValid || executionOrderCount != BootstrapRegistryCycleValidator.StartupNodeCount)
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "BOOTSTRAP_GRAPH_INVALID", "BootstrapRegistryCycleValidator", 0, "Startup graph did not produce full topological order.");
                return;
            }

            if (executionOrderCount != 26)
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "BOOTSTRAP_NODE_COUNT_UNEXPECTED", "BootstrapRegistryCycleValidator", 0, "Expected current source truth count 26; got " + executionOrderCount.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (!OrderBefore(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistryServiceSlot.TickManager, BootstrapOrderScratch, executionOrderCount) ||
                !OrderBefore(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistryServiceSlot.Save, BootstrapOrderScratch, executionOrderCount) ||
                !OrderBefore(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistryServiceSlot.ObjectPool, BootstrapOrderScratch, executionOrderCount) ||
                !OrderBefore(GlobalRegistryServiceSlot.FloatingOriginRuntime, GlobalRegistryServiceSlot.PhysicsStateManager, BootstrapOrderScratch, executionOrderCount) ||
                !OrderBefore(GlobalRegistryServiceSlot.PhysicsStateManager, GlobalRegistryServiceSlot.Physics, BootstrapOrderScratch, executionOrderCount) ||
                !OrderBefore(GlobalRegistryServiceSlot.Input, GlobalRegistryServiceSlot.Player, BootstrapOrderScratch, executionOrderCount))
            {
                AddFinding(result, "FATAL", "UNITY_EDITOR_API", "BOOTSTRAP_ORDER_CONTRACT_FAILED", "BootstrapRegistryCycleValidator", 0, "Core registry owner/dependency order is invalid.");
            }
        }

        public static int CountHotDependencyLookupViolationsForTest(string source)
        {
            SceneIntegrityValidationResult result = new SceneIntegrityValidationResult();
            string scrubbed = StripCommentsAndStrings(source);
            for (int h = 0; h < HotMethodNames.Length; h++)
                ScanHotMethodForDependencyLookup("test_source.cs", scrubbed, HotMethodNames[h], result);
            return result.hotDependencyLookupCount;
        }

        public static int CountPresentationPhaseViolationsForTest(string source)
        {
            SceneIntegrityValidationResult result = new SceneIntegrityValidationResult();
            string scrubbed = StripCommentsAndStrings(source);
            for (int h = 0; h < HotMethodNames.Length; h++)
                ScanHotMethodForPresentationWrites("test_source.cs", scrubbed, HotMethodNames[h], result);
            return result.presentationPhaseViolationCount;
        }

        public static int CountDataVaultWriteLockViolationsForTest(string source)
        {
            SceneIntegrityValidationResult result = new SceneIntegrityValidationResult();
            ScanDataVaultWriteLocks("test_source.cs", StripCommentsAndStrings(source), result);
            return result.dataVaultWriteLockViolationCount;
        }

        private static void ScanApexIntegratorContracts(SceneIntegrityValidationResult result)
        {
            string sourceRoot = "Assets/_Project/Scripts";
            if (!Directory.Exists(sourceRoot))
                return;

            string[] paths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < paths.Length; i++)
            {
                string path = NormalizeProjectPath(paths[i]);
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.EndsWith("GlobalRegistry.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = File.ReadAllText(path);
                string scrubbed = StripCommentsAndStrings(source);
                for (int h = 0; h < HotMethodNames.Length; h++)
                {
                    ScanHotMethodForDependencyLookup(path, scrubbed, HotMethodNames[h], result);
                    ScanHotMethodForPresentationWrites(path, scrubbed, HotMethodNames[h], result);
                }

                ScanDataVaultWriteLocks(path, scrubbed, result);
            }
        }

        private static void ScanHotMethodForDependencyLookup(
            string path,
            string source,
            string methodName,
            SceneIntegrityValidationResult result)
        {
            int cursor = 0;
            string needle = methodName + "(";
            while (cursor < source.Length)
            {
                int index = source.IndexOf(needle, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                cursor = index + needle.Length;
                if (index > 0 && (IsIdentifierPart(source[index - 1]) || source[index - 1] == '.'))
                    continue;

                if (!LooksLikeMethodDeclaration(source, index))
                    continue;

                int openBrace = source.IndexOf('{', cursor);
                if (openBrace < 0)
                    continue;

                int closeBrace = FindMatchingBrace(source, openBrace);
                if (closeBrace < 0)
                    continue;

                if (TryFindToken(source, openBrace, closeBrace, HotDependencyLookupTokens, out string token))
                {
                    result.hotDependencyLookupCount++;
                    AddFinding(
                        result,
                        "FATAL",
                        "STATIC_SOURCE",
                        "HOT_DEPENDENCY_LOOKUP",
                        path,
                        CountLineNumber(source, index),
                        methodName + " contains " + token + "; cache it during cold owner/bootstrap phase.");
                }

                cursor = closeBrace + 1;
            }
        }

        private static void ScanHotMethodForPresentationWrites(
            string path,
            string source,
            string methodName,
            SceneIntegrityValidationResult result)
        {
            if (string.Equals(methodName, "LateFrameTick", StringComparison.Ordinal) ||
                string.Equals(methodName, "VisualSyncTick", StringComparison.Ordinal))
                return;

            int cursor = 0;
            string needle = methodName + "(";
            while (cursor < source.Length)
            {
                int index = source.IndexOf(needle, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                cursor = index + needle.Length;
                if (index > 0 && (IsIdentifierPart(source[index - 1]) || source[index - 1] == '.'))
                    continue;

                if (!LooksLikeMethodDeclaration(source, index))
                    continue;

                int openBrace = source.IndexOf('{', cursor);
                if (openBrace < 0)
                    continue;

                int closeBrace = FindMatchingBrace(source, openBrace);
                if (closeBrace < 0)
                    continue;

                if (TryFindToken(source, openBrace, closeBrace, PresentationWriteTokens, out string token))
                {
                    result.presentationPhaseViolationCount++;
                    AddFinding(
                        result,
                        "FATAL",
                        "STATIC_SOURCE",
                        "PRESENTATION_WRITE_OUTSIDE_VISUAL_SYNC",
                        path,
                        CountLineNumber(source, index),
                        methodName + " contains " + token + "; presentation writes must be deferred to LateFrameTick or VISUAL_SYNC.");
                }

                cursor = closeBrace + 1;
            }
        }

        private static void ScanDataVaultWriteLocks(string path, string source, SceneIntegrityValidationResult result)
        {
            int cursor = 0;
            while (TryFindNextMethodBody(source, ref cursor, out string methodName, out int methodLine, out int openBrace, out int closeBrace))
            {
                int acquireCount = CountTokens(source, openBrace, closeBrace, DataVaultWriteLockAcquireTokens);
                if (acquireCount == 0)
                    continue;

                bool releaseInBody = ContainsAnyToken(source, openBrace, closeBrace, DataVaultWriteLockReleaseTokens);
                bool finallyInBody = source.IndexOf("finally", openBrace, closeBrace - openBrace, StringComparison.Ordinal) >= 0;
                bool acquireHelper = methodName.StartsWith("TryAcquire", StringComparison.Ordinal) ||
                    methodName.StartsWith("TryPin", StringComparison.Ordinal) ||
                    methodName.StartsWith("TryLock", StringComparison.Ordinal);

                if (acquireCount > 1)
                {
                    result.dataVaultWriteLockViolationCount++;
                    AddFinding(
                        result,
                        "FATAL",
                        "STATIC_SOURCE",
                        "DATAVAULT_MULTIPLE_WRITE_LOCKS_IN_METHOD",
                        path,
                        methodLine,
                        methodName + " acquires " + acquireCount.ToString(CultureInfo.InvariantCulture) + " DataVault write/buffer locks in one method.");
                }

                if (!acquireHelper && (!releaseInBody || !finallyInBody))
                {
                    result.dataVaultWriteLockViolationCount++;
                    AddFinding(
                        result,
                        "FATAL",
                        "STATIC_SOURCE",
                        "DATAVAULT_LOCK_WITHOUT_TRY_FINALLY",
                        path,
                        methodLine,
                        methodName + " acquires a DataVault write/buffer lock without release inside try/finally.");
                }
            }
        }

        private static bool TryFindToken(
            string source,
            int openBrace,
            int closeBrace,
            string[] tokens,
            out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(source) || tokens == null || closeBrace <= openBrace)
                return false;

            int length = closeBrace - openBrace;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (source.IndexOf(tokens[i], openBrace, length, StringComparison.Ordinal) >= 0)
                {
                    token = tokens[i];
                    return true;
                }
            }

            return false;
        }

        private static int CountTokens(string source, int openBrace, int closeBrace, string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null || closeBrace <= openBrace)
                return 0;

            int count = 0;
            int length = closeBrace - openBrace;
            for (int t = 0; t < tokens.Length; t++)
            {
                int cursor = openBrace;
                while (cursor < closeBrace)
                {
                    int index = source.IndexOf(tokens[t], cursor, length - (cursor - openBrace), StringComparison.Ordinal);
                    if (index < 0 || index >= closeBrace)
                        break;

                    count++;
                    cursor = index + tokens[t].Length;
                }
            }

            return count;
        }

        private static bool ContainsAnyToken(string source, int openBrace, int closeBrace, string[] tokens)
        {
            return TryFindToken(source, openBrace, closeBrace, tokens, out _);
        }

        private static bool TryFindNextMethodBody(
            string source,
            ref int cursor,
            out string methodName,
            out int methodLine,
            out int openBrace,
            out int closeBrace)
        {
            methodName = null;
            methodLine = 0;
            openBrace = -1;
            closeBrace = -1;

            while (cursor < source.Length)
            {
                int openParen = source.IndexOf('(', cursor);
                if (openParen < 0)
                {
                    cursor = source.Length;
                    return false;
                }

                int nameEnd = openParen - 1;
                while (nameEnd >= 0 && char.IsWhiteSpace(source[nameEnd]))
                    nameEnd--;

                int nameStart = nameEnd;
                while (nameStart >= 0 && IsIdentifierPart(source[nameStart]))
                    nameStart--;
                nameStart++;

                if (nameStart > nameEnd)
                {
                    cursor = openParen + 1;
                    continue;
                }

                string candidateName = source.Substring(nameStart, nameEnd - nameStart + 1);
                if (!LooksLikePotentialMethodDeclaration(source, candidateName, nameStart))
                {
                    cursor = openParen + 1;
                    continue;
                }

                int closeParen = FindMatchingParenthesis(source, openParen);
                if (closeParen < 0)
                {
                    cursor = openParen + 1;
                    continue;
                }

                int brace = source.IndexOf('{', closeParen + 1);
                if (brace < 0)
                {
                    cursor = closeParen + 1;
                    continue;
                }

                if (!OnlyMethodTailBetween(source, closeParen + 1, brace))
                {
                    cursor = closeParen + 1;
                    continue;
                }

                int end = FindMatchingBrace(source, brace);
                if (end < 0)
                {
                    cursor = brace + 1;
                    continue;
                }

                methodName = candidateName;
                methodLine = CountLineNumber(source, nameStart);
                openBrace = brace;
                closeBrace = end;
                cursor = end + 1;
                return true;
            }

            return false;
        }

        private static bool LooksLikePotentialMethodDeclaration(string source, string methodName, int methodNameIndex)
        {
            if (methodName == "if" ||
                methodName == "for" ||
                methodName == "foreach" ||
                methodName == "while" ||
                methodName == "switch" ||
                methodName == "catch" ||
                methodName == "using" ||
                methodName == "lock" ||
                methodName == "fixed" ||
                methodName == "return" ||
                methodName == "new")
            {
                return false;
            }

            if (methodNameIndex > 0 && source[methodNameIndex - 1] == '.')
                return false;

            int lineStart = source.LastIndexOf('\n', Math.Max(0, methodNameIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            string prefix = source.Substring(lineStart, methodNameIndex - lineStart);
            if (prefix.IndexOf("=>", StringComparison.Ordinal) >= 0)
                return false;

            string trimmed = prefix.TrimStart();
            return trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                trimmed.StartsWith("private ", StringComparison.Ordinal) ||
                trimmed.StartsWith("protected ", StringComparison.Ordinal) ||
                trimmed.StartsWith("internal ", StringComparison.Ordinal) ||
                trimmed.StartsWith("static ", StringComparison.Ordinal) ||
                trimmed.StartsWith("unsafe ", StringComparison.Ordinal) ||
                trimmed.StartsWith("void ", StringComparison.Ordinal);
        }

        private static bool OnlyMethodTailBetween(string source, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                char c = source[i];
                if (char.IsWhiteSpace(c))
                    continue;

                if (c == ':')
                    return false;

                if (c == 'w' && i + 5 <= end && string.CompareOrdinal(source, i, "where", 0, 5) == 0)
                    return true;

                return false;
            }

            return true;
        }

        private static void ValidateForbiddenLegacyResidue(SceneIntegrityValidationResult result)
        {
            string[] allFiles = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            for (int i = 0; i < allFiles.Length; i++)
            {
                string normalized = NormalizeProjectPath(allFiles[i]);
                for (int f = 0; f < ForbiddenLegacyPathFragments.Length; f++)
                {
                    if (normalized.IndexOf(ForbiddenLegacyPathFragments[f], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.forbiddenLegacyFileCount++;
                        AddFinding(result, "FATAL", "STATIC_SOURCE", "FORBIDDEN_LEGACY_RESIDUE", normalized, 0, "Legacy plugin residue exists in active Assets tree.");
                        break;
                    }
                }

                if (result.forbiddenLegacyFileCount >= 64)
                {
                    AddFinding(result, "WARNING", "STATIC_SOURCE", "FORBIDDEN_LEGACY_RESIDUE_TRUNCATED", "Assets", 0, "Legacy residue report truncated at 64 files.");
                    break;
                }
            }
        }

        private static void RequireFileContains(
            SceneIntegrityValidationResult result,
            string path,
            string token,
            string code)
        {
            if (!File.Exists(path))
                return;

            string text = File.ReadAllText(path);
            if (text.IndexOf(token, StringComparison.Ordinal) < 0)
                AddFinding(result, "FATAL", "STATIC_SOURCE", code, path, 0, "Missing token: " + token);
        }

        private static bool TryPrepareSceneAccess(SceneIntegrityValidationResult result)
        {
            if (Application.isBatchMode)
                return true;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isDirty)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        AddFinding(result, "FATAL", "UNITY_EDITOR_API", "DIRTY_SCENE_ACCESS_BLOCKED", scene.path, 0, "User cancelled dirty-scene save prompt.");
                        return false;
                    }

                    return true;
                }
            }

            return true;
        }

        private static void RestoreSceneSetup(SceneSetup[] setup, SceneIntegrityValidationResult result)
        {
            if (setup == null || setup.Length == 0)
                return;

            try
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            catch (Exception ex)
            {
                AddFinding(result, "WARNING", "UNITY_EDITOR_API", "SCENE_SETUP_RESTORE_FAILED", "EditorSceneManager", 0, ex.Message);
            }
        }

        private static void CollectTransforms(Transform root, List<Transform> output)
        {
            if (root == null)
                return;

            output.Add(root);
            for (int cursor = 0; cursor < output.Count; cursor++)
            {
                Transform current = output[cursor];
                if (current == null)
                    continue;

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    output.Add(current.GetChild(i));
            }
        }

        private static bool TryReadScriptReference(string line, out int fileId, out string guid)
        {
            fileId = int.MinValue;
            guid = null;
            int scriptIndex = line.IndexOf("m_Script:", StringComparison.Ordinal);
            if (scriptIndex < 0)
                return false;

            int fileIndex = line.IndexOf("fileID:", scriptIndex, StringComparison.Ordinal);
            if (fileIndex < 0)
                return false;

            int fileStart = fileIndex + "fileID:".Length;
            while (fileStart < line.Length && char.IsWhiteSpace(line[fileStart]))
                fileStart++;

            int fileEnd = fileStart;
            while (fileEnd < line.Length && (char.IsDigit(line[fileEnd]) || line[fileEnd] == '-'))
                fileEnd++;

            if (fileEnd > fileStart)
                int.TryParse(line.Substring(fileStart, fileEnd - fileStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId);

            int guidIndex = line.IndexOf("guid:", scriptIndex, StringComparison.Ordinal);
            if (guidIndex < 0)
                return true;

            int guidStart = guidIndex + "guid:".Length;
            while (guidStart < line.Length && char.IsWhiteSpace(line[guidStart]))
                guidStart++;

            int guidEnd = guidStart;
            while (guidEnd < line.Length && IsHexChar(line[guidEnd]))
                guidEnd++;

            if (guidEnd > guidStart)
                guid = line.Substring(guidStart, guidEnd - guidStart);

            return true;
        }

        private static bool IsExternalPackageClassIdentifier(string classIdentifier)
        {
            if (string.IsNullOrWhiteSpace(classIdentifier))
                return false;

            return classIdentifier.StartsWith("Unity.", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("Cinemachine", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("Crest::", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("MapMagic::", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("Den.Tools::", StringComparison.Ordinal) ||
                classIdentifier.StartsWith("VolumetricLightBeam::", StringComparison.Ordinal);
        }

        private static string StripCommentsAndStrings(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            StringBuilder builder = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;
            bool verbatimString = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        builder.Append(c);
                    }
                    else
                    {
                        builder.Append(' ');
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        blockComment = false;
                        builder.Append(' ');
                        builder.Append(' ');
                        i++;
                    }
                    else
                    {
                        builder.Append(c == '\n' ? '\n' : ' ');
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && next == '"')
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        i++;
                        continue;
                    }

                    bool end = c == '"' && (verbatimString || !IsEscaped(source, i));
                    builder.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    bool end = c == '\'' && !IsEscaped(source, i);
                    builder.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    lineComment = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    blockComment = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    stringLiteral = true;
                    verbatimString = true;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    stringLiteral = true;
                    builder.Append(' ');
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static int FindMatchingBrace(string source, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int FindMatchingParenthesis(string source, int openParen)
        {
            int depth = 0;
            for (int i = openParen; i < source.Length; i++)
            {
                if (source[i] == '(')
                    depth++;
                else if (source[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool OrderBefore(
            GlobalRegistryServiceSlot before,
            GlobalRegistryServiceSlot after,
            GlobalRegistryServiceSlot[] order,
            int count)
        {
            int beforeIndex = -1;
            int afterIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (order[i] == before)
                    beforeIndex = i;
                if (order[i] == after)
                    afterIndex = i;
            }

            return beforeIndex >= 0 && afterIndex >= 0 && beforeIndex < afterIndex;
        }

        private static bool LooksLikeMethodDeclaration(string source, int methodNameIndex)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(0, methodNameIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int length = methodNameIndex - lineStart;
            if (length <= 0)
                return false;

            string prefix = source.Substring(lineStart, length);
            return prefix.IndexOf(" void ", StringComparison.Ordinal) >= 0 ||
                prefix.TrimStart().StartsWith("void ", StringComparison.Ordinal);
        }

        private static Type FindType(string fullName)
        {
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void AddFinding(
            SceneIntegrityValidationResult result,
            string severity,
            string evidence,
            string code,
            string path,
            int line,
            string message)
        {
            if (string.Equals(severity, "FATAL", StringComparison.Ordinal))
                result.fatalCount++;
            else
                result.warningCount++;

            if (result.findings.Count >= MaxFindings)
                return;

            result.findings.Add(new SceneIntegrityFinding
            {
                severity = severity,
                evidence = evidence,
                code = code,
                path = path,
                line = line,
                message = message,
            });
        }

        private static void LogResult(SceneIntegrityValidationResult result)
        {
            string message = "[1627] Campaign 00 migration validation " +
                (result.passed ? "PASS" : "FAIL") +
                " fatal=" + result.fatalCount.ToString(CultureInfo.InvariantCulture) +
                " warning=" + result.warningCount.ToString(CultureInfo.InvariantCulture) +
                " sourceOnly=1";

            if (result.passed)
                UnityEngine.Debug.Log(message);
            else
                UnityEngine.Debug.LogError(message);
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder(128);
            Transform current = transform;
            while (current != null)
            {
                if (builder.Length == 0)
                    builder.Insert(0, current.name);
                else
                    builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/');
            string root = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(root.Length + 1);

            return normalized;
        }

        private static int CountLineNumber(string source, int index)
        {
            int line = 1;
            int max = Math.Min(index, source.Length);
            for (int i = 0; i < max; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static long StopwatchTicksToMicroseconds(long ticks)
        {
            return (long)((ticks * 1000000.0d) / Stopwatch.Frequency);
        }

        private static bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }
    }

    [Serializable]
    public sealed class SceneIntegrityValidationResult
    {
        public string agentId;
        public string evidenceClass;
        public string worldScenePath;
        public string bootstrapScenePath;
        public string prefabRootPath;
        public string staticDataPath;
        public bool passed;
        public bool staticDataCompilerValidated;
        public int fatalCount;
        public int warningCount;
        public int sceneRootCount;
        public int sceneMissingScripts;
        public int prefabCount;
        public int prefabMissingScripts;
        public int yamlMonoBehaviourCount;
        public int yamlMissingScriptFileId0;
        public int yamlAssemblyCSharpIdentifiers;
        public int yamlUnresolvedProjectScriptGuids;
        public int bootstrapNodeCount;
        public int bootstrapOrderCount;
        public int hotDependencyLookupCount;
        public int presentationPhaseViolationCount;
        public int dataVaultWriteLockViolationCount;
        public int forbiddenLegacyFileCount;
        public long staticDataBytes;
        public long elapsedMicroseconds;
        public List<SceneIntegrityFinding> findings = new List<SceneIntegrityFinding>(128);
    }

    [Serializable]
    public sealed class SceneIntegrityFinding
    {
        public string severity;
        public string evidence;
        public string code;
        public string path;
        public int line;
        public string message;
    }
}
#endif

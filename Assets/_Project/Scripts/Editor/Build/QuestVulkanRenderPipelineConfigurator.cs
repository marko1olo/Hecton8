#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Creates and validates the Quest-only URP asset chain without mutating PC renderer assets.
    /// </summary>
    internal sealed class QuestVulkanRenderPipelineConfigurator : IPreprocessBuildWithReport
    {
        private const string SourceUrpAssetPath = "Assets/_Project/Data/Mobile_RPAsset.asset";
        private const string SourceRendererAssetPath = "Assets/_Project/Data/Mobile_Renderer.asset";
        private const string QuestUrpAssetPath = "Assets/_Project/Data/URP_Quest_VR.asset";
        private const string QuestRendererAssetPath = "Assets/_Project/Data/Quest_VR_Renderer.asset";
        private const string AuditReportPath = "Docs/AgentLogs/Report_QUEST_VULKAN_RENDER_PIPELINE_Audit.md";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string AndroidBuildTargetGroupName = "Android";
        private const string QuestQualityName = "Quest (VR)";
        private const string ConfigureMenuPath = "HECTON-8/Platform/Configure Quest Vulkan Render Pipeline";
        private const string WireQualityMenuPath = "HECTON-8/Platform/Wire Quest Android Quality Route";

        public int callbackOrder => -4590;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android)
                return;

            EnsureQuestAssets(logSummary: true);
            ForceSinglePassInstanced();
            WriteAuditReport();
        }

        [MenuItem(ConfigureMenuPath, priority = 420)]
        private static void ConfigureFromMenu()
        {
            ConfigureQuestAssetsForCi();
        }

        [MenuItem(WireQualityMenuPath, priority = 421)]
        private static void WireQualityFromMenu()
        {
            WireQuestAndroidQualityRouteForCi();
        }

        public static void ConfigureQuestAssetsForCi()
        {
            EnsureQuestAssets(logSummary: true);
            ForceSinglePassInstanced();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            WriteAuditReport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void WireQuestAndroidQualityRouteForCi()
        {
            UniversalRenderPipelineAsset urpAsset = EnsureQuestAssets(logSummary: true);
            int questIndex = EnsureQuestQualityRow(urpAsset);
            IsolateAndroidQualityLevel(questIndex);

            WriteAuditReport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static UniversalRenderPipelineAsset EnsureQuestAssets(bool logSummary)
        {
            EnsureCopiedAsset(SourceRendererAssetPath, QuestRendererAssetPath);
            EnsureCopiedAsset(SourceUrpAssetPath, QuestUrpAssetPath);

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(QuestRendererAssetPath);
            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(QuestUrpAssetPath);
            if (rendererData == null)
                throw new BuildFailedException("Quest renderer asset was not created: " + QuestRendererAssetPath);
            if (urpAsset == null)
                throw new BuildFailedException("Quest URP asset was not created: " + QuestUrpAssetPath);

            ConfigureRenderer(rendererData);
            ConfigureUrpAsset(urpAsset, rendererData);
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(urpAsset);

            if (logSummary)
            {
                Debug.Log(
                    "[QUEST_VULKAN_RENDER_PIPELINE] Configured " +
                    QuestUrpAssetPath +
                    " depth=0 opaque=0 msaa=4 hdr=0 renderer=" +
                    QuestRendererAssetPath +
                    ".");
            }

            return urpAsset;
        }

        private static void EnsureCopiedAsset(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destinationPath) != null)
                return;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourcePath) == null)
                throw new BuildFailedException("Source asset missing: " + sourcePath);

            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory) && !AssetDatabase.IsValidFolder(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                throw new BuildFailedException("AssetDatabase.CopyAsset failed: " + sourcePath + " -> " + destinationPath);
        }

        private static void ConfigureUrpAsset(UniversalRenderPipelineAsset urpAsset, UniversalRendererData rendererData)
        {
            SerializedObject serialized = new SerializedObject(urpAsset);
            SetBool(serialized, "m_RequireDepthTexture", false);
            SetBool(serialized, "m_RequireOpaqueTexture", false);
            SetBool(serialized, "m_SupportsHDR", false);
            SetBool(serialized, "m_SoftShadowsSupported", false);
            SetBool(serialized, "m_MixedLightingSupported", false);
            SetBool(serialized, "m_PrefilterXRKeywords", true);
            SetBool(serialized, "m_PrefilterNativeRenderPass", true);
            SetInt(serialized, "m_MSAA", 4);
            SetFloat(serialized, "m_RenderScale", 1f);
            SetInt(serialized, "m_UpscalingFilter", 1);
            SetBool(serialized, "m_FsrOverrideSharpness", false);
            SetInt(serialized, "m_StoreActionsOptimization", 1);
            SetObjectInFirstArraySlot(serialized, "m_RendererDataList", rendererData);
            SetInt(serialized, "m_DefaultRendererIndex", 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRenderer(UniversalRendererData rendererData)
        {
            SerializedObject serialized = new SerializedObject(rendererData);
            SetBool(serialized, "m_UseNativeRenderPass", true);
            SetInt(serialized, "m_DepthPrimingMode", 0);
            SetInt(serialized, "m_CopyDepthMode", 0);
            SetInt(serialized, "m_IntermediateTextureMode", 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.Collections.Generic.List<ScriptableRendererFeature> features = rendererData.rendererFeatures;
            for (int i = 0; i < features.Count; i++)
            {
                ScriptableRendererFeature feature = features[i];
                if (feature == null)
                    continue;

                bool shouldBeActive = !IsQuestDepthResolveFeature(feature);
                if (feature.isActive != shouldBeActive)
                {
                    feature.SetActive(shouldBeActive);
                    EditorUtility.SetDirty(feature);
                }
            }
        }

        private static bool IsQuestDepthResolveFeature(ScriptableRendererFeature feature)
        {
            string name = feature.GetType().Name;
            return Contains(name, "DepthFog") ||
                   Contains(name, "Ssdo") ||
                   Contains(name, "SSGI") ||
                   Contains(name, "VoxelSsao") ||
                   Contains(name, "VolumetricShafts") ||
                   Contains(name, "HalfResParticles") ||
                   Contains(name, "StochasticSsr") ||
                   Contains(name, "FillrateDepthPrepass") ||
                   Contains(name, "SonarPointCloud") ||
                   Contains(name, "ScannerProjection") ||
                   Contains(name, "DeferredDecal") ||
                   Contains(name, "DryVolume") ||
                   Contains(name, "BiosDiagnostic") ||
                   Contains(name, "HolographicEdge") ||
                   Contains(name, "VolumetricLight");
        }

        private static void ForceSinglePassInstanced()
        {
            if (PlayerSettings.stereoRenderingPath != StereoRenderingPath.Instancing)
                PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
        }

        private static void WriteAuditReport()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# QUEST_VULKAN_RENDER_PIPELINE Audit");
            builder.AppendLine();
            builder.AppendLine("- Quest URP asset: `" + QuestUrpAssetPath + "`");
            builder.AppendLine("- Quest renderer asset: `" + QuestRendererAssetPath + "`");
            builder.AppendLine("- Stereo rendering path: `" + PlayerSettings.stereoRenderingPath + "`");
            AppendGraphicsApiAudit(builder);
            AppendQualityRouteAudit(builder);
            AppendRenderTextureAudit(builder);
            AppendComputeAudit(builder);

            string absolutePath = Path.GetFullPath(AuditReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendGraphicsApiAudit(StringBuilder builder)
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            builder.AppendLine();
            builder.AppendLine("## Android Graphics API");
            if (apis == null || apis.Length == 0)
            {
                builder.AppendLine("- BLOCKED: Android graphics API list is empty.");
                return;
            }

            for (int i = 0; i < apis.Length; i++)
                builder.AppendLine("- [" + i + "] " + apis[i]);

            if (apis[0] != GraphicsDeviceType.Vulkan)
                builder.AppendLine("- BLOCKED: Vulkan is not first.");
        }

        private static void AppendQualityRouteAudit(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("## Android Quality / Quest URP Route");

            string questGuid = AssetDatabase.AssetPathToGUID(QuestUrpAssetPath);
            builder.AppendLine("- Quest URP GUID: `" + FormatMissing(questGuid) + "`");
            builder.AppendLine("- Quest quality row name: `" + QuestQualityName + "`");

            string absoluteQualityPath = Path.GetFullPath(QualitySettingsPath);
            if (!File.Exists(absoluteQualityPath))
            {
                builder.AppendLine("- BLOCKED: `" + QualitySettingsPath + "` missing.");
                return;
            }

            string qualityText = File.ReadAllText(absoluteQualityPath);
            int androidDefaultIndex = ParseAndroidDefaultQualityIndex(qualityText);
            string androidQualityName;
            int qualityRowCount;
            string androidRenderPipelineGuid = ReadQualityRenderPipelineGuid(
                qualityText,
                androidDefaultIndex,
                out androidQualityName,
                out qualityRowCount);

            builder.AppendLine("- Quality row count: `" + qualityRowCount + "`");
            builder.AppendLine("- Android default quality index: `" + FormatInt(androidDefaultIndex) + "`");
            builder.AppendLine("- Android default quality name: `" + FormatMissing(androidQualityName) + "`");
            builder.AppendLine("- Android default render pipeline GUID: `" + FormatMissing(androidRenderPipelineGuid) + "`");

            bool wired = !string.IsNullOrEmpty(questGuid) &&
                         string.Equals(androidRenderPipelineGuid, questGuid, StringComparison.OrdinalIgnoreCase);
            builder.AppendLine(wired
                ? "- PASS: Android default quality resolves to the Quest URP asset."
                : "- BLOCKED: Android default quality does not resolve to the Quest URP asset. Use a Unity import-aware QualitySettings route fix before claiming Quest runtime render readiness.");
        }

        private static int EnsureQuestQualityRow(UniversalRenderPipelineAsset urpAsset)
        {
            if (urpAsset == null)
                throw new BuildFailedException("Quest URP asset is missing: " + QuestUrpAssetPath);

            UnityEngine.Object qualityObject = QualitySettings.GetQualitySettings();
            if (qualityObject == null)
                throw new BuildFailedException("QualitySettings.GetQualitySettings() returned null.");

            SerializedObject serialized = new SerializedObject(qualityObject);
            SerializedProperty qualityRows = serialized.FindProperty("m_QualitySettings");
            if (qualityRows == null || !qualityRows.isArray)
                throw new BuildFailedException("QualitySettings `m_QualitySettings` array not found.");

            int questIndex = FindQuestQualityRow(qualityRows, urpAsset);
            if (questIndex < 0)
            {
                int oldSize = qualityRows.arraySize;
                if (oldSize <= 0)
                    throw new BuildFailedException("QualitySettings has no source row to duplicate for Quest.");

                qualityRows.arraySize = oldSize + 1;
                questIndex = oldSize;
            }

            SerializedProperty questRow = qualityRows.GetArrayElementAtIndex(questIndex);
            ConfigureQuestQualityRow(questRow, urpAsset);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized.Update();
            string mapError;
            if (!TrySetAndroidDefaultQualityIndex(serialized, questIndex, out mapError))
                throw new BuildFailedException(mapError);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return questIndex;
        }

        private static int FindQuestQualityRow(SerializedProperty qualityRows, UniversalRenderPipelineAsset urpAsset)
        {
            for (int i = 0; i < qualityRows.arraySize; i++)
            {
                SerializedProperty row = qualityRows.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = row.FindPropertyRelative("name");
                SerializedProperty pipelineProperty = row.FindPropertyRelative("customRenderPipeline");
                bool nameMatches = nameProperty != null &&
                                   string.Equals(nameProperty.stringValue, QuestQualityName, StringComparison.Ordinal);
                bool pipelineMatches = pipelineProperty != null &&
                                       pipelineProperty.objectReferenceValue == urpAsset;
                if (nameMatches || pipelineMatches)
                    return i;
            }

            return -1;
        }

        private static void ConfigureQuestQualityRow(SerializedProperty row, UniversalRenderPipelineAsset urpAsset)
        {
            SetString(row, "name", QuestQualityName);
            SetInt(row, "pixelLightCount", 1);
            SetInt(row, "shadows", 1);
            SetInt(row, "shadowResolution", 1);
            SetInt(row, "shadowProjection", 1);
            SetInt(row, "shadowCascades", 1);
            SetFloat(row, "shadowDistance", 18f);
            SetFloat(row, "shadowNearPlaneOffset", 3f);
            SetInt(row, "skinWeights", 2);
            SetInt(row, "globalTextureMipmapLimit", 1);
            SetInt(row, "anisotropicTextures", 1);
            SetInt(row, "antiAliasing", 0);
            SetInt(row, "softParticles", 0);
            SetInt(row, "softVegetation", 0);
            SetInt(row, "realtimeReflectionProbes", 0);
            SetInt(row, "billboardsFaceCameraPosition", 1);
            SetInt(row, "vSyncCount", 0);
            SetFloat(row, "lodBias", 1.15f);
            SetInt(row, "maximumLODLevel", 0);
            SetInt(row, "enableLODCrossFade", 1);
            SetInt(row, "streamingMipmapsActive", 1);
            SetInt(row, "streamingMipmapsAddAllCameras", 1);
            SetInt(row, "streamingMipmapsMemoryBudget", 384);
            SetInt(row, "streamingMipmapsRenderersPerFrame", 256);
            SetInt(row, "streamingMipmapsMaxLevelReduction", 2);
            SetInt(row, "streamingMipmapsMaxFileIORequests", 256);
            SetInt(row, "particleRaycastBudget", 96);
            SetInt(row, "asyncUploadTimeSlice", 1);
            SetInt(row, "asyncUploadBufferSize", 8);
            SetInt(row, "asyncUploadPersistentBuffer", 1);
            SetFloat(row, "resolutionScalingFixedDPIFactor", 1f);
            SetObject(row, "customRenderPipeline", urpAsset);
            SetInt(row, "terrainQualityOverrides", 0);
            SetFloat(row, "terrainPixelError", 1.5f);
            SetFloat(row, "terrainDetailDensityScale", 0.6f);
            SetFloat(row, "terrainBasemapDistance", 800f);
            SetFloat(row, "terrainDetailDistance", 45f);
            SetFloat(row, "terrainTreeDistance", 700f);
            SetFloat(row, "terrainBillboardStart", 35f);
            SetFloat(row, "terrainFadeLength", 5f);
            SetInt(row, "terrainMaxTrees", 24);
        }

        private static void IsolateAndroidQualityLevel(int questIndex)
        {
            for (int i = 0; i < QualitySettings.count; i++)
            {
                Exception error;
                bool ok = i == questIndex
                    ? QualitySettings.TryIncludePlatformAt(AndroidBuildTargetGroupName, i, out error)
                    : QualitySettings.TryExcludePlatformAt(AndroidBuildTargetGroupName, i, out error);

                if (!ok)
                {
                    string message = error != null ? error.Message : "unknown Unity QualitySettings platform error";
                    throw new BuildFailedException("Failed to update Android quality platform route for row " + i + ": " + message);
                }
            }
        }

        private static bool TrySetAndroidDefaultQualityIndex(SerializedObject serialized, int questIndex, out string error)
        {
            SerializedProperty defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults == null)
            {
                error = "QualitySettings `m_PerPlatformDefaultQuality` map not found.";
                return false;
            }

            SerializedProperty cursor = defaults.Copy();
            SerializedProperty end = cursor.GetEndProperty();
            bool enterChildren = true;
            while (cursor.Next(enterChildren))
            {
                enterChildren = true;
                if (SerializedProperty.EqualContents(cursor, end))
                    break;

                if (cursor.propertyType == SerializedPropertyType.Integer &&
                    string.Equals(cursor.name, AndroidBuildTargetGroupName, StringComparison.Ordinal))
                {
                    cursor.intValue = questIndex;
                    error = string.Empty;
                    return true;
                }
            }

            error = "QualitySettings `m_PerPlatformDefaultQuality.Android` integer entry not found.";
            return false;
        }

        private static void AppendRenderTextureAudit(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("## Manual RenderTexture Recon");
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            AppendSourceHits(builder, scriptsRoot, "targetTexture");
            AppendSourceHits(builder, scriptsRoot, "new RenderTexture");
            AppendSourceHits(builder, scriptsRoot, "RenderTextureDescriptor");
        }

        private static void AppendComputeAudit(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("## HLOD Vulkan Compute Audit");
            string computePath = Path.Combine(Application.dataPath, "_Project", "Art", "Shaders", "InstanceCulling.compute");
            if (!File.Exists(computePath))
            {
                builder.AppendLine("- BLOCKED: `InstanceCulling.compute` missing.");
                return;
            }

            string source = File.ReadAllText(computePath);
            builder.AppendLine("- `InstanceCulling.compute` uses `AppendStructuredBuffer<float4x4>` and `GraphicsBuffer.CopyCount`; this is Vulkan-compatible in Unity compute.");
            builder.AppendLine(source.IndexOf("#pragma only_renderers d3d", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               source.IndexOf("#pragma exclude_renderers vulkan", StringComparison.OrdinalIgnoreCase) >= 0
                ? "- BLOCKED: renderer pragma excludes Vulkan."
                : "- No DirectX-only renderer pragma found.");
            builder.AppendLine(source.IndexOf("RWByteAddressBuffer", StringComparison.Ordinal) >= 0
                ? "- WARNING: ByteAddress path found; check binding stride."
                : "- No ByteAddress buffer in HLOD culling compute.");
        }

        private static void AppendSourceHits(StringBuilder builder, string root, string token)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            int hitCount = 0;
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = files[fileIndex];
                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (line.IndexOf(token, StringComparison.Ordinal) < 0)
                        continue;

                    hitCount++;
                    if (hitCount <= 80)
                    {
                        string relativePath = file.Replace(Path.GetFullPath(Application.dataPath + Path.DirectorySeparatorChar), "Assets/");
                        builder.Append("- ").Append(relativePath.Replace('\\', '/')).Append(':').Append(lineIndex + 1).Append(" `")
                            .Append(line.Trim().Replace("`", "'")).AppendLine("`");
                    }
                }
            }

            if (hitCount == 0)
                builder.AppendLine("- No `" + token + "` hits.");
            else if (hitCount > 80)
                builder.AppendLine("- `" + token + "` hits truncated at 80 of " + hitCount + ".");
        }

        private static int ParseAndroidDefaultQualityIndex(string qualityText)
        {
            if (string.IsNullOrEmpty(qualityText))
                return -1;

            int mapIndex = qualityText.IndexOf("m_PerPlatformDefaultQuality:", StringComparison.Ordinal);
            if (mapIndex < 0)
                return -1;

            string[] lines = qualityText.Substring(mapIndex).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("Android:", StringComparison.Ordinal))
                    continue;

                string value = trimmed.Substring("Android:".Length).Trim();
                int parsed;
                return int.TryParse(value, out parsed) ? parsed : -1;
            }

            return -1;
        }

        private static string ReadQualityRenderPipelineGuid(
            string qualityText,
            int targetIndex,
            out string qualityName,
            out int rowCount)
        {
            qualityName = string.Empty;
            rowCount = 0;
            if (string.IsNullOrEmpty(qualityText))
                return string.Empty;

            string[] lines = qualityText.Split('\n');
            int rowIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("m_TextureMipmapLimitGroupNames:", StringComparison.Ordinal))
                    break;

                if (trimmed.StartsWith("- serializedVersion:", StringComparison.Ordinal))
                {
                    rowIndex++;
                    rowCount++;
                    continue;
                }

                if (rowIndex != targetIndex)
                    continue;

                if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                    qualityName = trimmed.Substring("name:".Length).Trim();
                else if (trimmed.StartsWith("customRenderPipeline:", StringComparison.Ordinal))
                    return ExtractGuid(trimmed);
            }

            return string.Empty;
        }

        private static string ExtractGuid(string text)
        {
            int guidIndex = text.IndexOf("guid:", StringComparison.Ordinal);
            if (guidIndex < 0)
                return string.Empty;

            int valueStart = guidIndex + "guid:".Length;
            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            while (valueEnd < text.Length)
            {
                char c = text[valueEnd];
                if (c == ',' || c == '}' || char.IsWhiteSpace(c))
                    break;

                valueEnd++;
            }

            return valueEnd > valueStart ? text.Substring(valueStart, valueEnd - valueStart) : string.Empty;
        }

        private static string FormatInt(int value)
        {
            return value >= 0 ? value.ToString() : "<missing>";
        }

        private static string FormatMissing(string value)
        {
            return string.IsNullOrEmpty(value) ? "<missing>" : value;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetString(SerializedProperty root, string propertyName, string value)
        {
            SerializedProperty property = root.FindPropertyRelative(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetInt(SerializedProperty root, string propertyName, int value)
        {
            SerializedProperty property = root.FindPropertyRelative(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedProperty root, string propertyName, float value)
        {
            SerializedProperty property = root.FindPropertyRelative(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetObject(SerializedProperty root, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = root.FindPropertyRelative(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetObjectInFirstArraySlot(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return;

            if (property.arraySize < 1)
                property.arraySize = 1;

            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }

        private static bool Contains(string value, string token)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif

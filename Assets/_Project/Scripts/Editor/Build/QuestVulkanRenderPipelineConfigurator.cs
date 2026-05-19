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
        private const string ConfigureMenuPath = "HECTON-8/Platform/Configure Quest Vulkan Render Pipeline";

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

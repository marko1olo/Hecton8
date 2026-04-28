#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hecton8.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// HECTON-8 Project Health Dashboard.
    /// Protects VRAM budget (1800MB MX350) and enforces rendering standards.
    /// COLD ALLOC: Auditor lists and buffers for editor-only use.
    /// </summary>
    public static class HectonProjectAuditor
    {
        private const int MaxTextureSize = 2048;
        private const int HighResTextureThreshold = 1024;
        private const long MaxBundleSize = 200 * 1024 * 1024; // 200MB
        private const string RequiredShaderName = "Hecton8/CoreLit";
        private const int MaxConsoleEntriesPerSection = 24;

        [MenuItem("Hecton/Audit/Run Full Integrity Check")]
        public static void RunFullAudit()
        {
            Debug.Log("<color=cyan>[HECTON-8 AUDITOR] Starting Full Integrity Check...</color>");
            
            AuditTextures();
            AuditMaterials();
            AuditAddressables();
            AuditLodGroups();
            AuditShaderVariantPolicy();
            AuditQuarantineCandidates();

            Debug.Log("<color=cyan>[HECTON-8 AUDITOR] Audit Complete.</color>");
        }

        private static void AuditTextures()
        {
            Debug.Log("<b>--- Texture Budget Audit ---</b>");
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" });
            long totalVramEstimate = 0;
            int criticalErrors = 0;

            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                // 1. Resolution > 2048px check
                if (tex.width > MaxTextureSize || tex.height > MaxTextureSize)
                {
                    Debug.LogError($"[CRITICAL ERROR] Texture resolution > {MaxTextureSize}px: {path} ({tex.width}x{tex.height})");
                    criticalErrors++;
                }

                // 2. Compression check for > 1024px
                if (tex.width > HighResTextureThreshold || tex.height > HighResTextureThreshold)
                {
                    TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
                    if (settings.format != TextureImporterFormat.BC7 && settings.format != TextureImporterFormat.BC5)
                    {
                        Debug.LogWarning($"[OPTIMIZATION] Texture > {HighResTextureThreshold}px does not use BC7/BC5: {path} (Current: {settings.format})");
                    }
                }

                // 3. VRAM estimate
                totalVramEstimate += CalculateTextureVram(tex, importer);
            }

            Debug.Log($"Total potential VRAM usage (Assets/_Project): {totalVramEstimate / (1024f * 1024f):F2} MB");
            if (criticalErrors > 0) Debug.LogError($"Found {criticalErrors} critical texture errors!");
        }

        private static void AuditMaterials()
        {
            Debug.Log("<b>--- Material/Shader Hygiene Audit ---</b>");
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            int bannedShaderCount = 0;
            int instancingViolationCount = 0;

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // 1. Banned URP/Lit check
                if (mat.shader.name == "Universal Render Pipeline/Lit")
                {
                    Debug.LogError($"[BANNED SHADER] Material uses standard URP/Lit: {path}. Use {RequiredShaderName} instead.");
                    bannedShaderCount++;
                }

                // 2. GPU Instancing check
                if (!mat.enableInstancing)
                {
                    Debug.LogWarning($"[PERF CRIME] GPU Instancing is OFF: {path}");
                    instancingViolationCount++;
                }
            }

            HectonMaterialChannelPackValidator.AuditResult channelPackResult = HectonMaterialChannelPackValidator.RunAudit();
            Debug.Log(
                $"[HECTON-8 AUDITOR] Channel packing: Targeted={channelPackResult.TargetMaterialCount}, " +
                $"AnalysedMasks={channelPackResult.AnalysedMaskCount}, VRAMViolations={channelPackResult.VramViolations.Count}, " +
                $"PackedMaskViolations={channelPackResult.PackedMaskViolations.Count}, QuarantineCandidates={channelPackResult.QuarantineCandidatePaths.Count}.");
            Debug.Log(
                $"[HECTON-8 AUDITOR] Material hygiene summary: BannedShaders={bannedShaderCount}, " +
                $"InstancingViolations={instancingViolationCount}, CompliantPackedMaterials={channelPackResult.CompliantMaterials.Count}.");

            LogEntries("Channel-pack VRAM violation", channelPackResult.VramViolations);
            LogEntries("Channel-pack mask violation", channelPackResult.PackedMaskViolations);
        }

        private static void AuditAddressables()
        {
            Debug.Log("<b>--- Addressables Integrity Audit ---</b>");
            object settings = ResolveAddressableSettings();
            if (settings == null)
            {
                Debug.LogWarning("Addressables package/settings not found. Skipping addressables audit.");
                return;
            }

            IEnumerable groups = GetMemberValue<IEnumerable>(settings, "groups");
            if (groups == null)
            {
                Debug.LogWarning("Addressables settings have no readable groups collection. Skipping addressables audit.");
                return;
            }

            foreach (object group in groups)
            {
                if (group == null || InvokeBoolMember(group, "IsDefaultGroup")) continue;

                long groupSize = 0;
                IEnumerable entries = GetMemberValue<IEnumerable>(group, "entries");
                if (entries == null)
                    continue;

                foreach (object entry in entries)
                {
                    if (entry == null) continue;
                    string path = GetMemberValue<string>(entry, "AssetPath");
                    if (File.Exists(path))
                    {
                        groupSize += new FileInfo(path).Length;
                    }
                }

                if (groupSize > MaxBundleSize)
                {
                    string groupName = GetMemberValue<string>(group, "name") ?? "<unnamed>";
                    Debug.LogWarning($"[ADDRESSABLES] Group '{groupName}' exceeds {MaxBundleSize / (1024 * 1024)}MB: {groupSize / (1024f * 1024f):F2} MB. Flag for split.");
                }
            }
        }

        private static void AuditLodGroups()
        {
            Debug.Log("<b>--- LOD Group Validation ---</b>");
            HectonLodGroupAudit.AuditResult lodResult = HectonLodGroupAudit.RunAudit();
            Debug.Log(
                $"[HECTON-8 AUDITOR] LOD validation: ScannedAssets={lodResult.ScannedAssetCount}, " +
                $"Violations={lodResult.Violations.Count}, BrokenAssets={lodResult.BrokenAssets.Count}, " +
                $"QuarantineCandidates={lodResult.QuarantineCandidatePaths.Count}.");
            LogEntries("LOD violation", lodResult.Violations);
            LogEntries("Broken high-poly asset", lodResult.BrokenAssets);
        }

        private static void AuditShaderVariantPolicy()
        {
            Debug.Log("<b>--- Shader Variant Stripping Policy ---</b>");
            Debug.Log(BuildShaderVariantPolicySummary());
        }

        private static void AuditQuarantineCandidates()
        {
            Debug.Log("<b>--- Quarantine Preview ---</b>");
            HectonMaterialChannelPackValidator.AuditResult materialResult = HectonMaterialChannelPackValidator.RunAudit();
            HectonLodGroupAudit.AuditResult lodResult = HectonLodGroupAudit.RunAudit();

            HashSet<string> quarantinePaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < materialResult.QuarantineCandidatePaths.Count; i++)
                quarantinePaths.Add(materialResult.QuarantineCandidatePaths[i]);

            for (int i = 0; i < lodResult.QuarantineCandidatePaths.Count; i++)
                quarantinePaths.Add(lodResult.QuarantineCandidatePaths[i]);

            Debug.Log($"[HECTON-8 AUDITOR] Quarantine candidates ready for '{nameof(HectonAssetQuarantineUtility)}': {quarantinePaths.Count}.");
            if (quarantinePaths.Count <= 0)
                return;

            int emitted = 0;
            foreach (string assetPath in quarantinePaths)
            {
                if (emitted >= MaxConsoleEntriesPerSection)
                    break;

                Debug.LogWarning($"[HECTON-8 AUDITOR] Quarantine candidate: {assetPath}");
                emitted++;
            }

            if (quarantinePaths.Count > emitted)
                Debug.LogWarning($"[HECTON-8 AUDITOR] Quarantine candidate list truncated by {quarantinePaths.Count - emitted} entries.");
        }

        private static long CalculateTextureVram(Texture2D tex, TextureImporter importer)
        {
            // Heuristic VRAM calculation based on common Hecton8 formats
            int bpp = 4;
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            
            switch (settings.format)
            {
                case TextureImporterFormat.BC7:
                case TextureImporterFormat.DXT5:
                case TextureImporterFormat.RGBA32:
                    bpp = 4; break; // ~4 bytes per pixel for uncompressed or BC7 (estimate)
                case TextureImporterFormat.DXT1:
                case TextureImporterFormat.RGB24:
                    bpp = 3; break;
                default:
                    bpp = 4; break;
            }

            // [RULE] MX350 constraint: no uncompressed in VRAM unless RenderTexture.
            // This is a rough estimation. 
            long baseSize = (long)tex.width * tex.height * bpp;
            if (importer.mipmapEnabled) baseSize = (long)(baseSize * 1.33f);
            return baseSize;
        }

        private static object ResolveAddressableSettings()
        {
            Type settingsDefaultObjectType = Type.GetType(
                "UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor",
                throwOnError: false);
            if (settingsDefaultObjectType == null)
                return null;

            PropertyInfo settingsProperty = settingsDefaultObjectType.GetProperty(
                "Settings",
                BindingFlags.Public | BindingFlags.Static);
            return settingsProperty != null ? settingsProperty.GetValue(null) : null;
        }

        internal static string BuildShaderVariantPolicySummary()
        {
            Type stripperType = Type.GetType(
                "Hecton8.EditorTools.HectonShaderVariantStripper, Hecton8.Editor",
                throwOnError: false);
            if (stripperType == null)
                return "[HECTON-8 AUDITOR] Shader variant policy summary unavailable: HectonShaderVariantStripper is not imported into the editor assembly.";

            MethodInfo summaryMethod = stripperType.GetMethod(
                "BuildCurrentPolicySummary",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (summaryMethod == null || summaryMethod.ReturnType != typeof(string) || summaryMethod.GetParameters().Length != 0)
                return "[HECTON-8 AUDITOR] Shader variant policy summary unavailable: BuildCurrentPolicySummary() was not found.";

            object result = summaryMethod.Invoke(null, null);
            return result as string ?? "[HECTON-8 AUDITOR] Shader variant policy summary unavailable: BuildCurrentPolicySummary() returned null.";
        }

        private static T GetMemberValue<T>(object target, string memberName) where T : class
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type targetType = target.GetType();
            PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
                return property.GetValue(target) as T;

            FieldInfo field = targetType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? field.GetValue(target) as T : null;
        }

        private static bool InvokeBoolMember(object target, string methodName)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
                return false;

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0)
                return false;

            object result = method.Invoke(target, null);
            return result is bool boolResult && boolResult;
        }

        private static void LogEntries(string label, List<string> entries)
        {
            if (entries == null || entries.Count <= 0)
                return;

            int count = Mathf.Min(MaxConsoleEntriesPerSection, entries.Count);
            for (int i = 0; i < count; i++)
                Debug.LogWarning($"[HECTON-8 AUDITOR] {label}: {entries[i]}");

            if (entries.Count > count)
                Debug.LogWarning($"[HECTON-8 AUDITOR] {label}: truncated {entries.Count - count} additional entries.");
        }
    }
}
#endif

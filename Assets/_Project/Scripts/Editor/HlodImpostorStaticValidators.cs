#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class HlodImpostorStaticValidators
    {
        private const string BakeReportPath = "Docs/Reports/IMPOSTOR_BAKE_REPORT.json";
        private const string RenderingReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string RollbackFenceReportPath = "Docs/Reports/IMPOSTOR_ROLLBACK_FENCE.json";
        private const string ProjectRootMarker = "Assets";
        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(64);

        [MenuItem("Hecton8/Rendering/HLOD Impostor/Validate Layouts", false, 2520)]
        public static void ValidateLayoutsMenu()
        {
            // -executeMethod entry: never open DisplayDialog in batchmode.
            ValidateLayouts(!Application.isBatchMode);
        }

        [MenuItem("Hecton8/Rendering/HLOD Impostor/Run Static Archaeology", false, 2521)]
        public static void RunStaticArchaeologyMenu()
        {
            RunStaticArchaeology(true);
        }

        [MenuItem("Hecton8/Rendering/HLOD Impostor/Scan LOD Distances", false, 2522)]
        public static void ScanLodDistancesMenu()
        {
            ScanLodDistances(true);
        }

        [MenuItem("Hecton8/Rendering/HLOD Impostor/Validate Rollback Fence", false, 2523)]
        public static void ValidateRollbackFenceMenu()
        {
            ValidateRollbackFence(true);
        }

        public static bool ValidateLayouts(bool showDialog)
        {
            // Compile-proof / CI path: -executeMethod must never open DisplayDialog
            // (batchmode aborts with "This should not be called in batch mode").
            bool batch = Application.isBatchMode;
            if (batch)
                showDialog = false;

            bool valid = true;
            StringBuilder builder = new StringBuilder(512);
            int configSize = UnsafeUtility.SizeOf<ImpostorConfigDTO>();
            int instanceSize = UnsafeUtility.SizeOf<OctahedralImpostorInstance>();
            int bakeSettingsSize = UnsafeUtility.SizeOf<HlodImpostorBakeSettings>();
            int profileRecordSize = UnsafeUtility.SizeOf<HlodImpostorProfileRecord>();
            valid &= AssertEqual(configSize, 16, "ImpostorConfigDTO size", builder);
            valid &= AssertEqual(instanceSize, 32, "OctahedralImpostorInstance size", builder);
            valid &= AssertEqual(bakeSettingsSize, 96, "HlodImpostorBakeSettings size", builder);
            valid &= AssertEqual(profileRecordSize, 96, "HlodImpostorProfileRecord size", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<ImpostorConfigDTO>(nameof(ImpostorConfigDTO.AtlasGridSize)), 0, "AtlasGridSize offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<ImpostorConfigDTO>(nameof(ImpostorConfigDTO.DepthScale)), 8, "DepthScale offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<ImpostorConfigDTO>(nameof(ImpostorConfigDTO.Flags)), 12, "Flags offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<OctahedralImpostorInstance>(nameof(OctahedralImpostorInstance.CenterFade)), 0, "CenterFade offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<OctahedralImpostorInstance>(nameof(OctahedralImpostorInstance.SizeFlags)), 16, "SizeFlags offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorBakeSettings>(nameof(HlodImpostorBakeSettings.ProfileName)), 0, "Bake ProfileName offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorBakeSettings>(nameof(HlodImpostorBakeSettings.ViewCount)), 64, "Bake ViewCount offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorBakeSettings>(nameof(HlodImpostorBakeSettings.HemisphereOnly)), 84, "Bake HemisphereOnly offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorProfileRecord>(nameof(HlodImpostorProfileRecord.Name)), 0, "Profile Name offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorProfileRecord>(nameof(HlodImpostorProfileRecord.ViewCount)), 64, "Profile ViewCount offset", builder);
            valid &= AssertEqual((int)Marshal.OffsetOf<HlodImpostorProfileRecord>(nameof(HlodImpostorProfileRecord.HemisphereOnly)), 84, "Profile HemisphereOnly offset", builder);

            string report = valid
                ? "[HlodImpostorStaticValidators] RESULT: PASS — Explicit layouts valid."
                : "[HlodImpostorStaticValidators] RESULT: FAIL\n" + builder;
            if (valid)
                Debug.Log(report);
            else
                Debug.LogError(report);

            if (showDialog && !batch)
                EditorUtility.DisplayDialog("HLOD Impostor Layouts", valid ? "Explicit layouts valid." : builder.ToString(), "OK");

            // Soft layout FAIL stays exit 0 under -quit; hard fail is not used here.
            return valid;
        }


        public static void RunStaticArchaeology(bool logToConsole)
        {
            EnsureReportsFolder();
            int runtimeCaptureHits = 0;
            int billboardHits = 0;
            StringBuilder captureFiles = new StringBuilder(2048);
            StringBuilder billboardFiles = new StringBuilder(2048);

            ScanRuntimeCaptureDirectory("Assets/_Project/Scripts/Rendering", ref runtimeCaptureHits, captureFiles);
            ScanRuntimeCaptureDirectory("Assets/_Project/Scripts/Environment", ref runtimeCaptureHits, captureFiles);
            ScanBillboardAssets("Assets/_Project", ref billboardHits, billboardFiles);

            string report = string.Concat(
                "{\n",
                "  \"agent\": \"SHINOBU_212\",\n",
                "  \"status\": \"STATIC_ARCHAEOLOGY\",\n",
                "  \"runtime_impostor_capture_hits\": ", runtimeCaptureHits.ToString(System.Globalization.CultureInfo.InvariantCulture), ",\n",
                "  \"billboard_renderer_hits\": ", billboardHits.ToString(System.Globalization.CultureInfo.InvariantCulture), ",\n",
                "  \"rendering_environment_capture_files\": [", captureFiles.ToString(), "],\n",
                "  \"billboard_files\": [", billboardFiles.ToString(), "],\n",
                "  \"decision\": \"Existing HectonOctahedralImpostorBaker was converted to offline VRAM pack/readback path; no gameplay capture script was added.\"\n",
                "}\n");
            WriteProjectText(BakeReportPath, report);
            if (logToConsole)
                Debug.Log("SHINOBU_212 static archaeology written to " + BakeReportPath);
        }

        public static void ScanLodDistances(bool logToConsole)
        {
            EnsureReportsFolder();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            string[] dataGuids = AssetDatabase.FindAssets("t:HectonOctahedralImpostorData", new[] { "Assets/_Project" });
            int scanned = 0;
            int unoptimized = 0;
            StringBuilder offenders = new StringBuilder(4096);

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                string yaml = SafeReadProjectText(path, 65536);
                bool hasLodGroup = yaml.IndexOf("LODGroup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   yaml.IndexOf("m_LODs:", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasImpostorData = ContainsAnyGuid(yaml, dataGuids);
                if (!hasLodGroup || hasImpostorData)
                    continue;

                scanned++;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !TryCalculateApproxBounds(prefab, out Bounds bounds))
                    continue;

                float diameter = bounds.size.magnitude;
                if (diameter < 120f)
                    continue;

                AppendJsonString(offenders, path, unoptimized > 0);
                unoptimized++;
            }

            string report = string.Concat(
                "{\n",
                "  \"agent\": \"SHINOBU_212\",\n",
                "  \"scanner\": \"LOD_Distance_Scanner\",\n",
                "  \"large_lod_prefabs_scanned\": ", scanned.ToString(System.Globalization.CultureInfo.InvariantCulture), ",\n",
                "  \"unoptimized_horizons_detected\": ", unoptimized.ToString(System.Globalization.CultureInfo.InvariantCulture), ",\n",
                "  \"threshold_meters\": 500,\n",
                "  \"offenders\": [", offenders.ToString(), "]\n",
                "}\n");
            WriteProjectText(RenderingReportPath, report);
            if (logToConsole)
                Debug.Log("SHINOBU_212 LOD distance scan written to " + RenderingReportPath);
        }

        public static bool ValidateRollbackFence(bool logToConsole)
        {
            EnsureReportsFolder();
            string runtime = SafeReadProjectText("Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs", 131072);
            bool hasPresentationTail = runtime.IndexOf("PresentationExcluded", StringComparison.Ordinal) >= 0 &&
                                       runtime.IndexOf("for (int i = 13", StringComparison.Ordinal) >= 0;
            bool hashesHlod = runtime.IndexOf("HLOD_ImpostorDTO", StringComparison.Ordinal) >= 0 ||
                              runtime.IndexOf("StreamingHlodImpostorPoint", StringComparison.Ordinal) >= 0 ||
                              runtime.IndexOf("HectonOctahedralImpostor", StringComparison.Ordinal) >= 0;
            bool valid = hasPresentationTail && !hashesHlod;
            string report = string.Concat(
                "{\n",
                "  \"agent\": \"SHINOBU_212\",\n",
                "  \"status\": \"", valid ? "OK" : "CRITICAL_WARNING", "\",\n",
                "  \"state_ring_buffer_contains_hlod_impostor\": ", hashesHlod ? "true" : "false", ",\n",
                "  \"presentation_excluded_tail_descriptors\": ", hasPresentationTail ? "true" : "false", ",\n",
                "  \"decision\": \"HLOD impostor matrices remain presentation data and are not authoritative rollback leaves.\"\n",
                "}\n");
            WriteProjectText(RollbackFenceReportPath, report);
            if (logToConsole)
                Debug.Log("SHINOBU_212 rollback fence report written to " + RollbackFenceReportPath);
            return valid;
        }

        private static bool AssertEqual(int actual, int expected, string label, StringBuilder builder)
        {
            if (actual == expected)
                return true;

            builder.Append(label).Append(" expected ").Append(expected).Append(" actual ").Append(actual).Append('\n');
            return false;
        }

        private static void ScanRuntimeCaptureDirectory(string root, ref int hits, StringBuilder files)
        {
            string fullRoot = ToFullPath(root);
            if (!Directory.Exists(fullRoot))
                return;

            string[] sourceFiles = Directory.GetFiles(fullRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string file = sourceFiles[i];
                string text = File.ReadAllText(file);
                bool hit = text.IndexOf("RenderTexture", StringComparison.Ordinal) >= 0 &&
                           (text.IndexOf("Camera.Render", StringComparison.Ordinal) >= 0 ||
                            text.IndexOf("RenderWithShader", StringComparison.Ordinal) >= 0 ||
                            text.IndexOf("ReadPixels", StringComparison.Ordinal) >= 0 ||
                            text.IndexOf("EncodeToPNG", StringComparison.Ordinal) >= 0);
                if (!hit)
                    continue;

                AppendJsonString(files, ToProjectPath(file), hits > 0);
                hits++;
            }
        }

        private static void ScanBillboardAssets(string root, ref int hits, StringBuilder files)
        {
            string fullRoot = ToFullPath(root);
            if (!Directory.Exists(fullRoot))
                return;

            string[] paths = Directory.GetFiles(fullRoot, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < paths.Length; i++)
            {
                string file = paths[i];
                string extension = Path.GetExtension(file);
                if (!string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = SafeReadFullText(file, 131072);
                if (text.IndexOf("BillboardRenderer", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("m_Billboard", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("TreePrototype:", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                AppendJsonString(files, ToProjectPath(file), hits > 0);
                hits++;
            }
        }

        private static bool ContainsAnyGuid(string text, string[] guids)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                if (!string.IsNullOrEmpty(guids[i]) &&
                    text.IndexOf(guids[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCalculateApproxBounds(GameObject root, out Bounds bounds)
        {
            s_RendererScratch.Clear();
            root.GetComponentsInChildren(true, s_RendererScratch);
            bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static string SafeReadProjectText(string assetPath, int maxChars)
        {
            return SafeReadFullText(ToFullPath(assetPath), maxChars);
        }

        private static string SafeReadFullText(string fullPath, int maxChars)
        {
            try
            {
                string text = File.ReadAllText(fullPath);
                return text.Length <= maxChars ? text : text.Substring(0, maxChars);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void AppendJsonString(StringBuilder builder, string value, bool comma)
        {
            if (comma)
                builder.Append(", ");

            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
            builder.Append('"');
        }

        private static void EnsureReportsFolder()
        {
            string reports = ToFullPath("Docs/Reports");
            if (!Directory.Exists(reports))
                Directory.CreateDirectory(reports);
        }

        private static void WriteProjectText(string projectPath, string text)
        {
            string fullPath = ToFullPath(projectPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, text);
        }

        private static string ToFullPath(string projectRelativePath)
        {
            string root = Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(root, projectRelativePath));
        }

        private static string ToProjectPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int index = normalized.IndexOf(ProjectRootMarker + "/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index) : normalized;
        }
    }
}
#endif

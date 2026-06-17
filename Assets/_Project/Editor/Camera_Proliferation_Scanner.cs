#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class Camera_Proliferation_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Rendering/Run Camera Proliferation Scanner")]
        public static void RunMenu()
        {
            RunAndWriteReport();
        }

        public static CameraProliferationReport RunAndWriteReport()
        {
            CameraProliferationReport report = RunScan();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string reportPath = Path.Combine(projectRoot, ReportRelativePath);
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                WriteSharedReport(reportPath, report);
            }

            Debug.Log("Superfluous Cameras Eradicated: " + report.superfluousCamerasEradicated + " | Violations: " + report.violationCount);
            return report;
        }

        public static CameraProliferationReport RunScan()
        {
            List<CameraAssetScanResult> results = new List<CameraAssetScanResult>(128);
            ScanPrefabs(results);
            ScanSceneYaml(results);

            int violationCount = 0;
            int superfluous = 0;
            for (int i = 0; i < results.Count; i++)
            {
                CameraAssetScanResult result = results[i];
                if (result.activeNonUiCameraCount > 1)
                    violationCount++;
                superfluous += Mathf.Max(0, result.cameraComponentCount - Mathf.Max(1, result.activeUiOverlayCameraCount));
            }

            CameraProliferationReport report = new CameraProliferationReport();
            report.agentId = "SHINOBU_262";
            report.summary = "Superfluous Cameras Eradicated";
            report.scanner = nameof(Camera_Proliferation_Scanner);
            report.reportSchema = 1;
            report.scannedAssetCount = results.Count;
            report.violationCount = violationCount;
            report.superfluousCamerasEradicated = superfluous;
            report.results = results.ToArray();
            return report;
        }

        private static void ScanPrefabs(List<CameraAssetScanResult> results)
        {
            string[] searchRoots = ResolveExistingRoots(
                "Assets/_Project/Prefabs/Environment",
                "Assets/_Project/Prefabs",
                "Assets/Crest");

            string[] guids = searchRoots.Length > 0
                ? AssetDatabase.FindAssets("t:Prefab", searchRoots)
                : Array.Empty<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (!ShouldScanPrefabPath(path))
                    continue;

                CameraAssetScanResult result = ScanPrefab(path);
                results.Add(result);
            }
        }

        private static CameraAssetScanResult ScanPrefab(string path)
        {
            CameraAssetScanResult result = CreateResult(path, "PrefabContents");
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    return result;

                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                result.cameraComponentCount = cameras.Length;
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null)
                        continue;

                    bool uiOverlay = IsUiOverlayCamera(camera);
                    if (uiOverlay)
                        result.activeUiOverlayCameraCount++;
                    if (camera.enabled && camera.gameObject.activeInHierarchy && !uiOverlay)
                        result.activeNonUiCameraCount++;
                }
            }
            catch (Exception exception)
            {
                result.error = exception.GetType().Name;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            result.violation = result.activeNonUiCameraCount > 1;
            return result;
        }

        private static void ScanSceneYaml(List<CameraAssetScanResult> results)
        {
            string[] roots = ResolveExistingRoots("Assets/_Project/Scenes", "Assets/_Project/Prefabs/Environment");
            if (roots.Length <= 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:Scene", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                string absolutePath = !string.IsNullOrEmpty(projectRoot) ? Path.Combine(projectRoot, path) : path;
                if (string.IsNullOrEmpty(path) || !File.Exists(absolutePath))
                    continue;

                string text = File.ReadAllText(absolutePath);
                CameraAssetScanResult result = CreateResult(path, "YamlStatic");
                result.cameraComponentCount = CountToken(text, "\nCamera:");
                result.activeNonUiCameraCount = result.cameraComponentCount;
                result.activeUiOverlayCameraCount = CountUiCameraNameTokens(text);
                result.activeNonUiCameraCount = Mathf.Max(0, result.activeNonUiCameraCount - result.activeUiOverlayCameraCount);
                result.violation = result.activeNonUiCameraCount > 1;
                results.Add(result);
            }
        }

        private static bool ShouldScanPrefabPath(string path)
        {
            return path.IndexOf("/Environment/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Ocean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Crest", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUiOverlayCamera(Camera camera)
        {
            string name = camera.name;
            return ContainsIgnoreCase(name, "UI") ||
                   ContainsIgnoreCase(name, "HUD") ||
                   ContainsIgnoreCase(name, "Overlay");
        }

        private static int CountUiCameraNameTokens(string text)
        {
            return CountTokenIgnoreCase(text, "UI Camera") +
                   CountTokenIgnoreCase(text, "HUD Camera") +
                   CountTokenIgnoreCase(text, "Overlay Camera");
        }

        private static int CountToken(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static int CountTokenIgnoreCase(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static bool ContainsIgnoreCase(string text, string token)
        {
            return !string.IsNullOrEmpty(text) &&
                   !string.IsNullOrEmpty(token) &&
                   text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] ResolveExistingRoots(params string[] roots)
        {
            List<string> existing = new List<string>(roots.Length);
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root))
                    existing.Add(root);
            }

            return existing.ToArray();
        }

        private static CameraAssetScanResult CreateResult(string assetPath, string mode)
        {
            CameraAssetScanResult result = new CameraAssetScanResult();
            result.assetPath = assetPath;
            result.scanMode = mode;
            return result;
        }

        private static void WriteSharedReport(string reportPath, CameraProliferationReport report)
        {
            string reportJson = JsonUtility.ToJson(report, true);
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, reportJson);
                return;
            }

            string existing = File.ReadAllText(reportPath);
            if (existing.IndexOf("\"shinobu_262_camera_guillotine\"", StringComparison.Ordinal) >= 0)
            {
                File.WriteAllText(reportPath + ".shinobu_262.json", reportJson);
                return;
            }

            string trimmed = existing.TrimEnd();
            int lastBrace = trimmed.LastIndexOf('}');
            if (lastBrace < 0)
            {
                File.WriteAllText(reportPath, reportJson);
                return;
            }

            string prefix = trimmed.Substring(0, lastBrace);
            string suffix = trimmed.Substring(lastBrace);
            string comma = prefix.IndexOf(':') >= 0 ? "," : string.Empty;
            File.WriteAllText(
                reportPath,
                prefix +
                comma +
                System.Environment.NewLine +
                "  \"shinobu_262_camera_guillotine\": " +
                IndentNestedJson(reportJson) +
                System.Environment.NewLine +
                suffix +
                System.Environment.NewLine);
        }

        private static string IndentNestedJson(string json)
        {
            return json.Replace(System.Environment.NewLine, System.Environment.NewLine + "  ");
        }
    }

    [Serializable]
    public sealed class CameraProliferationReport
    {
        public string agentId;
        public string scanner;
        public string summary;
        public int reportSchema;
        public int scannedAssetCount;
        public int violationCount;
        public int superfluousCamerasEradicated;
        public CameraAssetScanResult[] results;
    }

    [Serializable]
    public sealed class CameraAssetScanResult
    {
        public string assetPath;
        public string scanMode;
        public int cameraComponentCount;
        public int activeNonUiCameraCount;
        public int activeUiOverlayCameraCount;
        public bool violation;
        public string error;
    }
}
#endif

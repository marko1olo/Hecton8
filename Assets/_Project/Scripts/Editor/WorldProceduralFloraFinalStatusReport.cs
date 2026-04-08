using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralFloraFinalStatusReport
    {
        private const string ReportFileName = "PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md";
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string AutomationFolderName = "CodexFloraAutomation";
        private const string AutomationRequestFileName = "flora_request.json";
        private const string AutomationResponseFileName = "flora_response.json";
        private const string AutomationPreviewFolder = "Assets/Screenshots/Automation";
        private const double AutomationPollIntervalSeconds = 0.5d;
        private const double AutomationPreviewTimeoutSeconds = 20d;
        private const int AutomationPreviewWidth = 512;
        private const int AutomationPreviewHeight = 512;
        private const int AutomationPreviewTasksPerUpdate = 2;

        private static readonly List<AutomationPreviewTask> _automationPreviewTasks = new List<AutomationPreviewTask>(8); // COLD ALLOC: editor automation queue, bounded by explicit request payload

        private static double _automationNextPollTime;
        private static bool _automationRequestActive;
        private static AutomationResponse _activeAutomationResponse;
        private static double _automationPreviewDeadline;

        [InitializeOnLoadMethod]
        private static void RegisterAutomationBridge()
        {
            EditorApplication.update -= UpdateAutomationBridge;
            EditorApplication.update += UpdateAutomationBridge;
            _automationNextPollTime = EditorApplication.timeSinceStartup + AutomationPollIntervalSeconds;
            Debug.Log("[WorldProceduralFloraFinalStatusReport] Automation bridge registered. RequestPath=" + GetAutomationRequestFilePath().Replace('\\', '/'));
        }

        [MenuItem("Hecton/Validation/Generate Procedural Flora Final Status Report", priority = 241)]
        public static void GenerateReport()
        {
            string rootFolder = WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder;
            Dictionary<string, FamilyStatus> statusByFamily = InitializeStatuses();
            PopulateLinkedFamilyState(statusByFamily);
            PopulatePrefabState(statusByFamily, rootFolder);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string reportPath = Path.Combine(projectRoot, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(rootFolder, statusByFamily), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralFloraFinalStatusReport] Wrote report to {reportPath}");
        }

        private static void UpdateAutomationBridge()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (_automationRequestActive)
            {
                UpdateAutomationPreviewQueue(now);
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (now < _automationNextPollTime)
                return;

            _automationNextPollTime = now + AutomationPollIntervalSeconds;
            TryBeginAutomationRequest();
        }

        private static void TryBeginAutomationRequest()
        {
            string requestFilePath = GetAutomationRequestFilePath();
            if (!File.Exists(requestFilePath))
                return;

            Debug.Log("[WorldProceduralFloraFinalStatusReport] Automation request detected.");
            EnsureAutomationFolderExists();

            AutomationRequest request;
            try
            {
                request = JsonUtility.FromJson<AutomationRequest>(File.ReadAllText(requestFilePath));
            }
            catch (Exception ex)
            {
                WriteAutomationFailureResponse("request_read_failed", ex.Message);
                SafeDeleteFile(requestFilePath);
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.requestId))
            {
                WriteAutomationFailureResponse("request_invalid", "Automation request is empty or missing requestId.");
                SafeDeleteFile(requestFilePath);
                return;
            }

            _activeAutomationResponse = new AutomationResponse
            {
                requestId = request.requestId,
                success = false,
                stage = "started",
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            _automationPreviewTasks.Clear();
            _automationRequestActive = true;

            SafeDeleteFile(GetAutomationResponseFilePath());
            SafeDeleteFile(requestFilePath);

            try
            {
                EnsureAssetFolder("Assets/Screenshots");
                EnsureAssetFolder(AutomationPreviewFolder);

                WorldProceduralFloraBakedStarterGenerator.Generate();
                WorldProceduralFloraFinalVariantAuthoring.ApplyBakedFloraFinals();
                WorldProceduralFloraFinalVariantValidator.Validate();
                GenerateReport();

                BuildAutomationPreviewQueue(request);
                _activeAutomationResponse.stage = _automationPreviewTasks.Count > 0 ? "capturing_previews" : "completed";
                _automationPreviewDeadline = EditorApplication.timeSinceStartup + AutomationPreviewTimeoutSeconds;

                if (_automationPreviewTasks.Count == 0)
                    CompleteAutomationRequest();
            }
            catch (Exception ex)
            {
                _activeAutomationResponse.stage = "failed";
                _activeAutomationResponse.error = ex.ToString();
                FinishAutomationRequest();
            }
        }

        private static void BuildAutomationPreviewQueue(AutomationRequest request)
        {
            if (request.capturePrefabPaths == null || request.capturePrefabPaths.Length == 0)
                return;

            for (int i = 0; i < request.capturePrefabPaths.Length; i++)
            {
                string prefabPath = request.capturePrefabPaths[i];
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    _automationPreviewTasks.Add(AutomationPreviewTask.CreateMissing(prefabPath));
                    continue;
                }

                _automationPreviewTasks.Add(AutomationPreviewTask.Create(prefabPath, prefab));
            }
        }

        private static void UpdateAutomationPreviewQueue(double now)
        {
            bool anyPending = false;
            int processedTaskCount = 0;

            for (int i = 0; i < _automationPreviewTasks.Count; i++)
            {
                AutomationPreviewTask task = _automationPreviewTasks[i];
                if (task.isDone)
                    continue;

                anyPending = true;
                if (processedTaskCount >= AutomationPreviewTasksPerUpdate)
                    continue;

                processedTaskCount++;
                try
                {
                    task = ProcessAutomationPreviewTask(task);
                }
                catch (Exception ex)
                {
                    task.previewPath = null;
                    task.error = "preview_exception: " + ex.GetType().Name;
                    task.isDone = true;
                }

                _automationPreviewTasks[i] = task;
            }

            if (!anyPending || now >= _automationPreviewDeadline)
            {
                for (int i = 0; i < _automationPreviewTasks.Count; i++)
                {
                    AutomationPreviewTask task = _automationPreviewTasks[i];
                    if (task.isDone)
                        continue;

                    task.isDone = true;
                    if (string.IsNullOrWhiteSpace(task.error))
                        task.error = "preview_timeout";

                    _automationPreviewTasks[i] = task;
                }

                CompleteAutomationRequest();
            }
        }

        private static AutomationPreviewTask ProcessAutomationPreviewTask(AutomationPreviewTask task)
        {
            if (task.prefabAsset == null)
            {
                task.error = string.IsNullOrWhiteSpace(task.error) ? "prefab_missing" : task.error;
                task.isDone = true;
                return task;
            }

            if (!task.directCaptureAttempted)
            {
                task.directCaptureAttempted = true;

                string capturedPreviewPath = CaptureAutomationPrefabPreview(task.prefabAsset, task.prefabPath);
                if (!string.IsNullOrWhiteSpace(capturedPreviewPath))
                {
                    task.previewPath = capturedPreviewPath;
                    task.prefabAsset = null;
                    task.isDone = true;
                    return task;
                }
            }

            if (!task.assetPreviewRequested)
            {
                task.assetPreviewRequested = true;
                AssetPreview.GetAssetPreview(task.prefabAsset);
            }

            Texture2D preview = AssetPreview.GetAssetPreview(task.prefabAsset);
            if (preview != null)
            {
                task.previewPath = SaveAutomationPreview(task.prefabPath, preview);
                task.prefabAsset = null;
                task.isDone = true;
                return task;
            }

            if (!AssetPreview.IsLoadingAssetPreview(task.prefabAsset.GetEntityId()))
            {
                Texture2D miniPreview = AssetPreview.GetMiniThumbnail(task.prefabAsset);
                task.previewPath = miniPreview != null ? SaveAutomationPreview(task.prefabPath, miniPreview) : null;
                task.error = miniPreview == null ? "preview_unavailable" : null;
                task.prefabAsset = null;
                task.isDone = true;
            }

            return task;
        }

        private static void CompleteAutomationRequest()
        {
            _activeAutomationResponse.stage = "completed";
            _activeAutomationResponse.success = true;
            _activeAutomationResponse.reportPath = Path.Combine(GetProjectRootPath(), ReportFileName).Replace('\\', '/');
            _activeAutomationResponse.previewPaths = CollectAutomationPreviewPaths();
            _activeAutomationResponse.previewErrors = CollectAutomationPreviewErrors();
            FinishAutomationRequest();
        }

        private static string[] CollectAutomationPreviewPaths()
        {
            if (_automationPreviewTasks.Count == 0)
                return Array.Empty<string>();

            List<string> previewPaths = new List<string>(_automationPreviewTasks.Count); // COLD ALLOC: editor automation response payload, bounded by request count
            for (int i = 0; i < _automationPreviewTasks.Count; i++)
            {
                string previewPath = _automationPreviewTasks[i].previewPath;
                if (!string.IsNullOrWhiteSpace(previewPath))
                    previewPaths.Add(previewPath);
            }

            return previewPaths.ToArray();
        }

        private static string[] CollectAutomationPreviewErrors()
        {
            if (_automationPreviewTasks.Count == 0)
                return Array.Empty<string>();

            List<string> previewErrors = new List<string>(_automationPreviewTasks.Count); // COLD ALLOC: editor automation response payload, bounded by request count
            for (int i = 0; i < _automationPreviewTasks.Count; i++)
            {
                AutomationPreviewTask task = _automationPreviewTasks[i];
                if (!string.IsNullOrWhiteSpace(task.error))
                    previewErrors.Add(task.prefabPath + ": " + task.error);
            }

            return previewErrors.ToArray();
        }

        private static string CaptureAutomationPrefabPreview(GameObject prefabAsset, string prefabPath)
        {
            PreviewRenderUtility previewUtility = null;
            Texture2D contactSheet = null;
            Texture2D[] viewTextures = null;

            try
            {
                if (prefabAsset == null)
                    return null;

                previewUtility = new PreviewRenderUtility();
                previewUtility.cameraFieldOfView = 32f;
                previewUtility.ambientColor = new Color(0.44f, 0.48f, 0.54f, 1f);

                GameObject prefabRoot = previewUtility.InstantiatePrefabInScene(prefabAsset);
                if (prefabRoot == null)
                    return null;

                if (!prefabRoot.activeSelf)
                    prefabRoot.SetActive(true);

                PrepareAutomationPreviewHierarchy(prefabRoot);

                Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return null;

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                Camera camera = previewUtility.camera;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.34f, 0.38f, 0.42f, 1f);
                camera.fieldOfView = 32f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.enabled = false;
                camera.orthographic = true;

                Light keyLight = previewUtility.lights[0];
                keyLight.intensity = 1.35f;
                keyLight.color = new Color(1f, 0.97f, 0.92f, 1f);
                keyLight.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

                Light fillLight = previewUtility.lights[1];
                fillLight.intensity = 0.92f;
                fillLight.color = new Color(0.78f, 0.9f, 1f, 1f);
                fillLight.transform.rotation = Quaternion.Euler(324f, 132f, 0f);

                viewTextures = new Texture2D[4]; // COLD ALLOC: editor-only contact sheet generation, fixed 4-view payload
                viewTextures[0] = RenderAutomationPreviewView(previewUtility, bounds, new Vector3(0f, 0.12f, -1f), 0.06f, 1.58f);
                viewTextures[1] = RenderAutomationPreviewView(previewUtility, bounds, new Vector3(-0.62f, 0.18f, -1f), 0.08f, 1.52f);
                viewTextures[2] = RenderAutomationPreviewView(previewUtility, bounds, new Vector3(-1f, 0.12f, 0f), 0.04f, 1.48f);
                viewTextures[3] = RenderAutomationPreviewView(previewUtility, bounds, new Vector3(-0.48f, 0.08f, -1f), -0.18f, 0.96f);

                for (int i = 0; i < viewTextures.Length; i++)
                {
                    if (viewTextures[i] == null)
                        return null;
                }

                contactSheet = BuildAutomationPreviewContactSheet(viewTextures);
                if (!IsAutomationPreviewMeaningful(contactSheet, camera.backgroundColor))
                    return null;

                return SaveAutomationPreview(prefabPath, contactSheet);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (viewTextures != null)
                {
                    for (int i = 0; i < viewTextures.Length; i++)
                    {
                        if (viewTextures[i] != null)
                            UnityObject.DestroyImmediate(viewTextures[i]);
                    }
                }

                if (contactSheet != null)
                    UnityObject.DestroyImmediate(contactSheet);

                if (previewUtility != null)
                    previewUtility.Cleanup();
            }
        }

        private static Texture2D RenderAutomationPreviewView(PreviewRenderUtility previewUtility, Bounds bounds, Vector3 viewDirection, float focusYOffsetNormalized, float zoomScale)
        {
            if (previewUtility == null)
                return null;

            Camera camera = previewUtility.camera;
            camera.cullingMask = ~0;
            camera.nearClipPlane = 0.01f;

            Vector3 normalizedViewDirection = viewDirection.sqrMagnitude > 0.0001f
                ? viewDirection.normalized
                : new Vector3(-0.42f, 0.24f, -1f).normalized;
            Vector3 worldUp = Mathf.Abs(Vector3.Dot(normalizedViewDirection, Vector3.up)) > 0.96f
                ? Vector3.forward
                : Vector3.up;
            Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, normalizedViewDirection));
            Vector3 up = Vector3.Normalize(Vector3.Cross(normalizedViewDirection, right));

            float focusYOffset = bounds.extents.y * focusYOffsetNormalized;
            Vector3 focus = bounds.center + Vector3.up * focusYOffset;

            float aspect = AutomationPreviewWidth / (float)AutomationPreviewHeight;
            float projectedVertical = Mathf.Max(EvaluateProjectedBoundsHalfExtent(bounds, up), bounds.extents.y * 1.04f);
            float projectedHorizontal = Mathf.Max(
                EvaluateProjectedBoundsHalfExtent(bounds, right),
                Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.08f);
            float maxHorizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float slenderness = bounds.size.y / Mathf.Max(0.08f, maxHorizontalExtent * 2f);
            float tallCompensation = Mathf.Lerp(1f, 1.12f, Mathf.Clamp01((slenderness - 2.2f) / 4.2f));
            float effectiveZoomScale = zoomScale >= 1.2f
                ? zoomScale * tallCompensation
                : zoomScale * Mathf.Lerp(1f, tallCompensation, 0.32f);
            float orthographicSize = Mathf.Max(
                projectedVertical * Mathf.Max(1.1f, effectiveZoomScale),
                (projectedHorizontal / Mathf.Max(0.1f, aspect)) * Mathf.Max(1.1f, effectiveZoomScale));
            orthographicSize = Mathf.Max(orthographicSize, 0.24f);
            float fitDistance = Mathf.Max(bounds.extents.magnitude * 3.2f, orthographicSize * 3.1f);

            camera.transform.position = focus - normalizedViewDirection * fitDistance;
            camera.transform.rotation = Quaternion.LookRotation(normalizedViewDirection, up);
            camera.orthographicSize = orthographicSize;
            camera.farClipPlane = fitDistance * 3.6f + bounds.extents.magnitude * 2.6f + 8f;

            previewUtility.BeginStaticPreview(new Rect(0f, 0f, AutomationPreviewWidth, AutomationPreviewHeight));
            previewUtility.Render(true, true);
            return previewUtility.EndStaticPreview();
        }

        private static float EvaluateProjectedBoundsHalfExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            float ax = Mathf.Abs(Vector3.Dot(new Vector3(extents.x, 0f, 0f), axis));
            float ay = Mathf.Abs(Vector3.Dot(new Vector3(0f, extents.y, 0f), axis));
            float az = Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, extents.z), axis));
            return ax + ay + az;
        }

        private static Texture2D BuildAutomationPreviewContactSheet(Texture2D[] viewTextures)
        {
            if (viewTextures == null || viewTextures.Length != 4)
                return null;

            int tileWidth = AutomationPreviewWidth;
            int tileHeight = AutomationPreviewHeight;
            int sheetWidth = tileWidth * 2;
            int sheetHeight = tileHeight * 2;
            Texture2D contactSheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false, false);
            Color32[] blankPixels = new Color32[sheetWidth * sheetHeight]; // COLD ALLOC: editor-only contact sheet assembly, bounded 1024x1024
            Color32 background = new Color32(71, 71, 77, 255);
            for (int i = 0; i < blankPixels.Length; i++)
                blankPixels[i] = background;

            contactSheet.SetPixels32(blankPixels);
            CopyAutomationPreviewTile(contactSheet, viewTextures[0], 0, tileHeight);
            CopyAutomationPreviewTile(contactSheet, viewTextures[1], tileWidth, tileHeight);
            CopyAutomationPreviewTile(contactSheet, viewTextures[2], 0, 0);
            CopyAutomationPreviewTile(contactSheet, viewTextures[3], tileWidth, 0);
            contactSheet.Apply(false, false);
            return contactSheet;
        }

        private static void CopyAutomationPreviewTile(Texture2D contactSheet, Texture2D source, int startX, int startY)
        {
            if (contactSheet == null || source == null)
                return;

            Color[] sourcePixels = source.GetPixels(0, 0, source.width, source.height); // COLD ALLOC: editor-only tile copy for bounded preview textures
            contactSheet.SetPixels(startX, startY, source.width, source.height, sourcePixels);
        }

        private static string SaveAutomationPreview(string prefabPath, Texture2D source)
        {
            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            string safeName = prefabName.Replace('.', '_');
            string assetPath = AutomationPreviewFolder + "/auto_" + safeName + ".png";
            string absolutePath = Path.Combine(GetProjectRootPath(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.WriteAllBytes(absolutePath, source.EncodeToPNG());
            return assetPath;
        }

        private static bool IsAutomationPreviewMeaningful(Texture2D texture, Color backgroundColor)
        {
            if (texture == null)
                return false;

            Color32[] pixels = texture.GetPixels32(); // COLD ALLOC: editor-only preview validation for bounded 512x512 capture
            if (pixels == null || pixels.Length == 0)
                return false;

            Color32 background = backgroundColor;
            int width = texture.width;
            int height = texture.height;
            int stepX = Mathf.Max(8, width / 12);
            int stepY = Mathf.Max(8, height / 12);
            int informativeSamples = 0;
            int sampleCount = 0;

            for (int y = stepY / 2; y < height; y += stepY)
            {
                int rowStart = y * width;
                for (int x = stepX / 2; x < width; x += stepX)
                {
                    Color32 pixel = pixels[rowStart + x];
                    if (pixel.a < 8)
                    {
                        sampleCount++;
                        continue;
                    }

                    int diff = Mathf.Abs(pixel.r - background.r)
                        + Mathf.Abs(pixel.g - background.g)
                        + Mathf.Abs(pixel.b - background.b);
                    if (diff > 18)
                        informativeSamples++;

                    sampleCount++;
                }
            }

            if (sampleCount == 0)
                return false;

            return informativeSamples >= Mathf.Max(2, sampleCount / 20);
        }

        private static void PrepareAutomationPreviewHierarchy(GameObject prefabRoot)
        {
            if (prefabRoot == null)
                return;

            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = true;
            }

            LODGroup[] lodGroups = prefabRoot.GetComponentsInChildren<LODGroup>(true);
            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LOD[] lods = lodGroups[groupIndex].GetLODs();
                for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
                {
                    Renderer[] lodRenderers = lods[lodIndex].renderers;
                    for (int rendererIndex = 0; rendererIndex < lodRenderers.Length; rendererIndex++)
                    {
                        Renderer lodRenderer = lodRenderers[rendererIndex];
                        if (lodRenderer != null)
                            lodRenderer.enabled = false;
                    }
                }
            }
        }

        private static void FinishAutomationRequest()
        {
            try
            {
                EnsureAutomationFolderExists();
                File.WriteAllText(GetAutomationResponseFilePath(), JsonUtility.ToJson(_activeAutomationResponse, true));
                Debug.Log("[WorldProceduralFloraFinalStatusReport] Automation response written to " + GetAutomationResponseFilePath().Replace('\\', '/'));
            }
            finally
            {
                _automationPreviewTasks.Clear();
                _automationRequestActive = false;
                _activeAutomationResponse = null;
                _automationNextPollTime = EditorApplication.timeSinceStartup + AutomationPollIntervalSeconds;
            }
        }

        private static void WriteAutomationFailureResponse(string stage, string error)
        {
            EnsureAutomationFolderExists();
            AutomationResponse response = new AutomationResponse
            {
                requestId = "unknown",
                success = false,
                stage = stage,
                error = error,
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            File.WriteAllText(GetAutomationResponseFilePath(), JsonUtility.ToJson(response, true));
        }

        private static string GetProjectRootPath()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string GetAutomationFolderPath()
        {
            return Path.Combine(GetProjectRootPath(), "Temp", AutomationFolderName);
        }

        private static string GetAutomationRequestFilePath()
        {
            return Path.Combine(GetAutomationFolderPath(), AutomationRequestFileName);
        }

        private static string GetAutomationResponseFilePath()
        {
            return Path.Combine(GetAutomationFolderPath(), AutomationResponseFileName);
        }

        private static void EnsureAutomationFolderExists()
        {
            string automationFolderPath = GetAutomationFolderPath();
            if (!Directory.Exists(automationFolderPath))
                Directory.CreateDirectory(automationFolderPath);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }

        private static void SafeDeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static Dictionary<string, FamilyStatus> InitializeStatuses()
        {
            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            Dictionary<string, FamilyStatus> statusByFamily = new Dictionary<string, FamilyStatus>(supportedFamilies.Count, StringComparer.Ordinal);

            for (int i = 0; i < supportedFamilies.Count; i++)
            {
                string familyId = supportedFamilies[i];
                statusByFamily[familyId] = new FamilyStatus(familyId);
            }

            return statusByFamily;
        }

        private static void PopulateLinkedFamilyState(IDictionary<string, FamilyStatus> statusByFamily)
        {
            string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { ProceduralFamilyFolder });
            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                    continue;

                FamilyStatus status;
                if (!statusByFamily.TryGetValue(family.familyId, out status))
                    continue;

                status.FamilyLabel = family.familyLabel;

                WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
                for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++)
                {
                    WorldPrefabFamilyProfile.VariantEntry variant = variants[variantIndex];
                    if (variant == null || !variant.finalReady || variant.proxyOnly)
                        continue;

                    status.LinkedFinalReadyCount++;

                    if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    {
                        status.LinkedPlaceholderCount++;
                        continue;
                    }

                    status.LinkedRealFinalCount++;

                    string prefabName = variant.prefab != null ? variant.prefab.name : string.Empty;
                    if (WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(prefabName))
                        status.LinkedGeneratedCount++;
                    else
                        status.LinkedAuthoredCount++;
                }
            }
        }

        private static void PopulatePrefabState(IDictionary<string, FamilyStatus> statusByFamily, string rootFolder)
        {
            if (!AssetDatabase.IsValidFolder(rootFolder))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                string familyId = WorldProceduralFloraFinalVariantAuthoring.ResolveFamilyIdFromAsset(prefabPath, prefabName);
                if (!WorldProceduralFloraFinalVariantAuthoring.IsSupportedFloraFamily(familyId))
                    continue;

                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                bool isGenerated = WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(prefabName);
                if (isGenerated)
                    status.GeneratedPrefabCount++;
                else
                    status.AuthoredPrefabCount++;

                PrefabStatus prefabStatus = InspectPrefab(prefabPath, prefabName, isGenerated);
                status.Prefabs.Add(prefabStatus);
                if (prefabStatus.HasLodGroup)
                    status.PrefabsWithLodCount++;

                if (prefabStatus.MaterialStateOk)
                    status.MaterialReadyPrefabCount++;

                if (prefabStatus.HasValidLodCascade)
                    status.PrefabsWithValidLodCascadeCount++;

                if (prefabStatus.MeetsFidelityFloor)
                    status.PrefabsMeetingFidelityFloorCount++;

                if (prefabStatus.BudgetTriangleCount > status.MaxBudgetTriangles)
                    status.MaxBudgetTriangles = prefabStatus.BudgetTriangleCount;

                if (prefabStatus.RendererCount > status.MaxRendererCount)
                    status.MaxRendererCount = prefabStatus.RendererCount;
            }

            foreach (KeyValuePair<string, FamilyStatus> pair in statusByFamily)
            {
                FamilyStatus status = pair.Value;
                WorldProceduralFloraFinalBudgetCatalog.Budget budget = WorldProceduralFloraFinalBudgetCatalog.Resolve(status.FamilyId);
                status.Prefabs.Sort(ComparePrefabStatus);
                status.TriangleBudgetLimit = budget.MaxTriangles;
                status.TriangleFidelityFloor = budget.MinRecommendedTriangles;
                status.RendererBudgetLimit = budget.MaxRenderers;
                status.ExpectedLinkedRealFinalCount = status.AuthoredPrefabCount > 0
                    ? status.AuthoredPrefabCount
                    : status.GeneratedPrefabCount + status.AuthoredPrefabCount;
            }
        }

        private static int ComparePrefabStatus(PrefabStatus left, PrefabStatus right)
        {
            int generatedComparison = left.IsGenerated.CompareTo(right.IsGenerated);
            if (generatedComparison != 0)
                return generatedComparison;

            return string.CompareOrdinal(left.Name, right.Name);
        }

        private static PrefabStatus InspectPrefab(string prefabPath, string prefabName, bool isGenerated)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                string familyId = WorldProceduralFloraFinalVariantAuthoring.ResolveFamilyIdFromAsset(prefabPath, prefabName);
                WorldProceduralFloraFinalVariantAuthoring.PrefabMetadata metadata =
                    WorldProceduralFloraFinalVariantAuthoring.ResolvePrefabMetadata(familyId, prefabName);
                Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                LODGroup[] lodGroups = prefabRoot.GetComponentsInChildren<LODGroup>(true);
                Renderer[] budgetRenderers = ResolveBudgetRenderers(renderers, lodGroups);

                return new PrefabStatus(
                    prefabName,
                    prefabPath,
                    isGenerated,
                    WorldProceduralFloraFinalVariantAuthoring.ResolveVariantIdForPrefab(familyId, prefabName),
                    renderers != null ? renderers.Length : 0,
                    lodGroups != null ? lodGroups.Length : 0,
                    CountLodLevels(lodGroups),
                    CountTriangles(budgetRenderers),
                    lodGroups != null && lodGroups.Length > 0,
                    metadata.Weight,
                    metadata.UniformScaleRange,
                    metadata.HasCustomWeight,
                    metadata.HasCustomScaleRange,
                    BuildLodTriangleCascade(lodGroups),
                    WorldProceduralFloraFinalBudgetCatalog.Resolve(familyId).MaxTriangles,
                    WorldProceduralFloraFinalBudgetCatalog.Resolve(familyId).MinRecommendedTriangles,
                    EvaluateMaterialState(familyId, renderers),
                    EvaluateRendererState(renderers),
                    metadata.HasError,
                    metadata.Error);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Renderer[] ResolveBudgetRenderers(Renderer[] allRenderers, LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return allRenderers ?? Array.Empty<Renderer>();

            List<Renderer> budgetRenderers = new List<Renderer>(8);
            HashSet<Renderer> seen = new HashSet<Renderer>();
            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null || lods.Length == 0 || lods[0].renderers == null)
                    continue;

                Renderer[] lod0Renderers = lods[0].renderers;
                for (int rendererIndex = 0; rendererIndex < lod0Renderers.Length; rendererIndex++)
                {
                    Renderer renderer = lod0Renderers[rendererIndex];
                    if (renderer == null || !seen.Add(renderer))
                        continue;

                    budgetRenderers.Add(renderer);
                }
            }

            return budgetRenderers.Count > 0 ? budgetRenderers.ToArray() : allRenderers ?? Array.Empty<Renderer>();
        }

        private static int CountTriangles(Renderer[] renderers)
        {
            int triangleCount = 0;
            if (renderers == null)
                return 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    triangleCount += CountTriangles(meshFilter.sharedMesh);
                    continue;
                }

                SkinnedMeshRenderer skinnedMesh = renderer as SkinnedMeshRenderer;
                if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
                    triangleCount += CountTriangles(skinnedMesh.sharedMesh);
            }

            return triangleCount;
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int triangles = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);

            return triangles;
        }

        private static int CountLodLevels(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return 0;

            int maxLodLevels = 0;
            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length > maxLodLevels)
                    maxLodLevels = lods.Length;
            }

            return maxLodLevels;
        }

        private static int[] BuildLodTriangleCascade(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return Array.Empty<int>();

            int maxLevels = 0;
            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length > maxLevels)
                    maxLevels = lods.Length;
            }

            if (maxLevels <= 0)
                return Array.Empty<int>();

            int[] cascade = new int[maxLevels];
            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null)
                    continue;

                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                    cascade[lodIndex] += CountTriangles(lods[lodIndex].renderers);
            }

            return cascade;
        }

        private static bool HasStrictLodCascade(int[] cascade)
        {
            if (cascade == null || cascade.Length <= 1)
                return true;

            for (int i = 1; i < cascade.Length; i++)
            {
                if (cascade[i] >= cascade[i - 1])
                    return false;
            }

            return true;
        }

        private static string FormatLodTriangleCascade(int[] cascade)
        {
            if (cascade == null || cascade.Length == 0)
                return "none";

            StringBuilder builder = new StringBuilder(24);
            for (int i = 0; i < cascade.Length; i++)
            {
                if (i > 0)
                    builder.Append('/');

                builder.Append(cascade[i]);
            }

            return builder.ToString();
        }

        private static MaterialState EvaluateMaterialState(string familyId, Renderer[] renderers)
        {
            string expectedShaderName = ResolveExpectedShaderName(familyId);
            bool instancingOk = true;
            bool shaderOk = true;
            bool textureStackOk = true;
            bool anyMaterial = false;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        instancingOk = false;
                        shaderOk = false;
                        textureStackOk = false;
                        continue;
                    }

                    for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                    {
                        Material material = sharedMaterials[materialIndex];
                        if (material == null)
                        {
                            instancingOk = false;
                            shaderOk = false;
                            textureStackOk = false;
                            continue;
                        }

                        anyMaterial = true;
                        if (!material.enableInstancing)
                            instancingOk = false;

                        if (string.IsNullOrEmpty(expectedShaderName))
                            continue;

                        if (material.shader == null || material.shader.name != expectedShaderName)
                            shaderOk = false;

                        if (material.GetTexture("_BaseMap") == null
                            || material.GetTexture("_DetailMap") == null
                            || material.GetTexture("_NormalMap") == null
                            || material.GetTexture("_MaskMap") == null)
                        {
                            textureStackOk = false;
                        }
                    }
                }
            }

            if (!anyMaterial)
                return new MaterialState(false, false, false, "missing-materials");

            if (string.IsNullOrEmpty(expectedShaderName))
                return new MaterialState(instancingOk, true, true, instancingOk ? "ok" : "instancing-off");

            if (instancingOk && shaderOk && textureStackOk)
                return new MaterialState(true, true, true, "ok");

            if (!shaderOk)
                return new MaterialState(instancingOk, false, textureStackOk, "shader-mismatch");

            if (!textureStackOk)
                return new MaterialState(instancingOk, true, false, "texture-stack-missing");

            return new MaterialState(false, true, true, "instancing-off");
        }

        private static string ResolveExpectedShaderName(string familyId)
        {
            if (familyId.StartsWith("family.kelp.", StringComparison.Ordinal))
                return KelpShaderName;

            if (familyId.StartsWith("family.coral.", StringComparison.Ordinal))
                return CoralShaderName;

            return string.Empty;
        }

        private static RendererState EvaluateRendererState(Renderer[] renderers)
        {
            bool defaultsOk = true;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off
                        || renderer.receiveShadows
                        || renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off
                        || renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off
                        || renderer.motionVectorGenerationMode != UnityEngine.MotionVectorGenerationMode.ForceNoMotion)
                    {
                        defaultsOk = false;
                        break;
                    }
                }
            }

            return new RendererState(defaultsOk, defaultsOk ? "ok" : "renderer-defaults-dirty");
        }

        private static string BuildMarkdown(string rootFolder, IReadOnlyDictionary<string, FamilyStatus> statusByFamily)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Flora Final Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(rootFolder).AppendLine("`");
            builder.Append("- Generated: `GEN_` prefabs are starter finals only.").AppendLine();
            builder.Append("- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.").AppendLine();
            builder.Append("- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.").AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Ready | LOD Cascade | Fidelity Floor |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            for (int familyIndex = 0; familyIndex < supportedFamilies.Count; familyIndex++)
            {
                string familyId = supportedFamilies[familyIndex];
                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                builder.Append("| ")
                    .Append(status.FamilyId)
                    .Append(" | a").Append(status.AuthoredPrefabCount).Append("/g").Append(status.GeneratedPrefabCount)
                    .Append(" | ").Append(status.ExpectedLinkedRealFinalCount)
                    .Append(" | ").Append(status.LinkedRealFinalCount)
                    .Append(" (authored ").Append(status.LinkedAuthoredCount).Append(", gen ").Append(status.LinkedGeneratedCount).Append(')')
                    .Append(" | ").Append(status.LinkedPlaceholderCount)
                    .Append(" | ").Append(status.MaxBudgetTriangles)
                    .Append(" | ").Append(status.TriangleBudgetLimit - status.MaxBudgetTriangles)
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.PrefabsWithLodCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.MaterialReadyPrefabCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.PrefabsWithValidLodCascadeCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.PrefabsMeetingFidelityFloorCount).Append('/').Append(status.Prefabs.Count)
                    .AppendLine(" |");
            }

            for (int familyIndex = 0; familyIndex < supportedFamilies.Count; familyIndex++)
            {
                string familyId = supportedFamilies[familyIndex];
                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                builder.AppendLine();
                builder.Append("## ").Append(status.FamilyId);
                if (!string.IsNullOrWhiteSpace(status.FamilyLabel))
                    builder.Append(" - ").Append(status.FamilyLabel);
                builder.AppendLine();
                builder.AppendLine();
                builder.Append("- Coverage: `a").Append(status.AuthoredPrefabCount).Append("/g").Append(status.GeneratedPrefabCount).AppendLine("`");
                builder.Append("- Expected linked real finals: `").Append(status.ExpectedLinkedRealFinalCount).Append("`").AppendLine();
                builder.Append("- Linked final-ready: `").Append(status.LinkedFinalReadyCount).Append("`").AppendLine();
                builder.Append("- Linked real finals: `").Append(status.LinkedRealFinalCount).Append("`").AppendLine();
                builder.Append("- Linked placeholders: `").Append(status.LinkedPlaceholderCount).Append("`").AppendLine();
                builder.Append("- Max budget triangles: `").Append(status.MaxBudgetTriangles).Append("`").AppendLine();
                builder.Append("- Triangle budget limit: `").Append(status.TriangleBudgetLimit).Append("`").AppendLine();
                builder.Append("- Triangle headroom: `").Append(status.TriangleBudgetLimit - status.MaxBudgetTriangles).Append("`").AppendLine();
                builder.Append("- Minimum recommended triangles: `").Append(status.TriangleFidelityFloor).Append("`").AppendLine();
                builder.Append("- Max renderer count: `").Append(status.MaxRendererCount).Append("`").AppendLine();
                builder.Append("- Renderer budget limit: `").Append(status.RendererBudgetLimit).Append("`").AppendLine();
                builder.Append("- Material-ready prefabs: `").Append(status.MaterialReadyPrefabCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
                builder.Append("- Strict LOD cascade prefabs: `").Append(status.PrefabsWithValidLodCascadeCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
                builder.Append("- Prefabs meeting fidelity floor: `").Append(status.PrefabsMeetingFidelityFloorCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();

                if (status.Prefabs.Count == 0)
                {
                    builder.AppendLine("- No baked prefabs found.");
                    continue;
                }

                builder.AppendLine("- Prefabs:");
                for (int prefabIndex = 0; prefabIndex < status.Prefabs.Count; prefabIndex++)
                {
                    PrefabStatus prefab = status.Prefabs[prefabIndex];
                    builder.Append("  - `").Append(prefab.Name).Append("`");
                    builder.Append(prefab.IsGenerated ? " generated" : " authored");
                    builder.Append(" | variantId=`").Append(prefab.VariantId).Append('`');
                    builder.Append(" | renderers=").Append(prefab.RendererCount);
                    builder.Append(" | lodGroups=").Append(prefab.LodGroupCount);
                    builder.Append(" | lodLevels=").Append(prefab.LodLevelCount);
                    builder.Append(" | budgetTriangles=").Append(prefab.BudgetTriangleCount);
                    builder.Append(" | weight=").Append(prefab.Weight);
                    if (prefab.HasCustomWeight)
                        builder.Append('*');
                    builder.Append(" | scale=").Append(FormatScaleRange(prefab.ScaleRange));
                    if (prefab.HasCustomScaleRange)
                        builder.Append('*');
                    builder.Append(" | lodTriangles=").Append(FormatLodTriangleCascade(prefab.LodTriangleCascade));
                    builder.Append(" | material=").Append(prefab.MaterialStateLabel);
                    builder.Append(" | renderState=").Append(prefab.RendererStateLabel);
                    builder.Append(" | fidelity=").Append(prefab.FidelityLabel);
                    builder.Append(" | path=`").Append(prefab.Path).AppendLine("`");
                    if (prefab.HasMetadataError)
                        builder.Append("    - metadataError=`").Append(prefab.MetadataError).AppendLine("`");
                }
            }

            return builder.ToString();
        }

        private static string FormatScaleRange(Vector2 scaleRange)
        {
            int minPercent = Mathf.RoundToInt(scaleRange.x * 100f);
            int maxPercent = Mathf.RoundToInt(scaleRange.y * 100f);
            return minPercent.ToString() + "-" + maxPercent.ToString();
        }

        private sealed class FamilyStatus
        {
            public FamilyStatus(string familyId)
            {
                FamilyId = familyId;
                FamilyLabel = string.Empty;
                Prefabs = new List<PrefabStatus>(4);
            }

            public string FamilyId { get; }
            public string FamilyLabel { get; set; }
            public int AuthoredPrefabCount { get; set; }
            public int GeneratedPrefabCount { get; set; }
            public int LinkedFinalReadyCount { get; set; }
            public int LinkedRealFinalCount { get; set; }
            public int LinkedPlaceholderCount { get; set; }
            public int LinkedGeneratedCount { get; set; }
            public int LinkedAuthoredCount { get; set; }
            public int ExpectedLinkedRealFinalCount { get; set; }
            public int PrefabsWithLodCount { get; set; }
            public int MaxBudgetTriangles { get; set; }
            public int MaxRendererCount { get; set; }
            public int TriangleBudgetLimit { get; set; }
            public int TriangleFidelityFloor { get; set; }
            public int RendererBudgetLimit { get; set; }
            public int MaterialReadyPrefabCount { get; set; }
            public int PrefabsWithValidLodCascadeCount { get; set; }
            public int PrefabsMeetingFidelityFloorCount { get; set; }
            public List<PrefabStatus> Prefabs { get; }
        }

        [Serializable]
        private sealed class AutomationRequest
        {
            public string requestId;
            public string[] capturePrefabPaths;
        }

        [Serializable]
        private sealed class AutomationResponse
        {
            public string requestId;
            public bool success;
            public string stage;
            public string error;
            public string generatedAtUtc;
            public string reportPath;
            public string[] previewPaths;
            public string[] previewErrors;
        }

        private readonly struct PrefabStatus
        {
            public PrefabStatus(
                string name,
                string path,
                bool isGenerated,
                string variantId,
                int rendererCount,
                int lodGroupCount,
                int lodLevelCount,
                int budgetTriangleCount,
                bool hasLodGroup,
                int weight,
                Vector2 scaleRange,
                bool hasCustomWeight,
                bool hasCustomScaleRange,
                int[] lodTriangleCascade,
                int triangleBudgetLimit,
                int triangleFidelityFloor,
                MaterialState materialState,
                RendererState rendererState,
                bool hasMetadataError,
                string metadataError)
            {
                Name = name;
                Path = path;
                IsGenerated = isGenerated;
                VariantId = variantId ?? string.Empty;
                RendererCount = rendererCount;
                LodGroupCount = lodGroupCount;
                LodLevelCount = lodLevelCount;
                BudgetTriangleCount = budgetTriangleCount;
                HasLodGroup = hasLodGroup;
                Weight = weight;
                ScaleRange = scaleRange;
                HasCustomWeight = hasCustomWeight;
                HasCustomScaleRange = hasCustomScaleRange;
                LodTriangleCascade = lodTriangleCascade ?? Array.Empty<int>();
                TriangleBudgetLimit = triangleBudgetLimit;
                TriangleFidelityFloor = triangleFidelityFloor;
                MeetsFidelityFloor = budgetTriangleCount >= triangleFidelityFloor;
                HasValidLodCascade = HasStrictLodCascade(LodTriangleCascade);
                MaterialStateOk = materialState.IsOk;
                MaterialStateLabel = materialState.Label ?? string.Empty;
                RendererStateOk = rendererState.IsOk;
                RendererStateLabel = rendererState.Label ?? string.Empty;
                FidelityLabel = MeetsFidelityFloor ? "ok" : "underbuilt";
                HasMetadataError = hasMetadataError;
                MetadataError = metadataError ?? string.Empty;
            }

            public string Name { get; }
            public string Path { get; }
            public bool IsGenerated { get; }
            public string VariantId { get; }
            public int RendererCount { get; }
            public int LodGroupCount { get; }
            public int LodLevelCount { get; }
            public int BudgetTriangleCount { get; }
            public bool HasLodGroup { get; }
            public int Weight { get; }
            public Vector2 ScaleRange { get; }
            public bool HasCustomWeight { get; }
            public bool HasCustomScaleRange { get; }
            public int[] LodTriangleCascade { get; }
            public int TriangleBudgetLimit { get; }
            public int TriangleFidelityFloor { get; }
            public bool MeetsFidelityFloor { get; }
            public bool HasValidLodCascade { get; }
            public bool MaterialStateOk { get; }
            public string MaterialStateLabel { get; }
            public bool RendererStateOk { get; }
            public string RendererStateLabel { get; }
            public string FidelityLabel { get; }
            public bool HasMetadataError { get; }
            public string MetadataError { get; }
        }

        private struct AutomationPreviewTask
        {
            public string prefabPath;
            public GameObject prefabAsset;
            public string previewPath;
            public string error;
            public bool isDone;
            public bool directCaptureAttempted;
            public bool assetPreviewRequested;

            public static AutomationPreviewTask Create(string prefabPath, GameObject prefabAsset)
            {
                return new AutomationPreviewTask
                {
                    prefabPath = prefabPath,
                    prefabAsset = prefabAsset,
                    previewPath = null,
                    error = null,
                    isDone = false,
                    directCaptureAttempted = false,
                    assetPreviewRequested = false
                };
            }

            public static AutomationPreviewTask CreateMissing(string prefabPath)
            {
                return new AutomationPreviewTask
                {
                    prefabPath = prefabPath,
                    prefabAsset = null,
                    previewPath = null,
                    error = "prefab_missing",
                    isDone = true,
                    directCaptureAttempted = false,
                    assetPreviewRequested = false
                };
            }
        }

        private readonly struct MaterialState
        {
            public MaterialState(bool instancingOk, bool shaderOk, bool textureStackOk, string label)
            {
                InstancingOk = instancingOk;
                ShaderOk = shaderOk;
                TextureStackOk = textureStackOk;
                Label = label ?? string.Empty;
            }

            public bool InstancingOk { get; }
            public bool ShaderOk { get; }
            public bool TextureStackOk { get; }
            public string Label { get; }
            public bool IsOk => InstancingOk && ShaderOk && TextureStackOk;
        }

        private readonly struct RendererState
        {
            public RendererState(bool isOk, string label)
            {
                IsOk = isOk;
                Label = label ?? string.Empty;
            }

            public bool IsOk { get; }
            public string Label { get; }
        }
    }
}

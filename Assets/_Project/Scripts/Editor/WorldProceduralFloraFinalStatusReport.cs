using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Hecton8.Editor
{
    public static class WorldProceduralFloraFinalStatusReport
    {
        private const string AutomationArmMenuPath = "Tools/Hecton/Dev/Flora/Arm Procedural Flora Automation Bridge";
        private const string AutomationDisarmMenuPath = "Tools/Hecton/Dev/Flora/Disarm Procedural Flora Automation Bridge";
        private const string AutomationPollMenuPath = "Tools/Hecton/Dev/Flora/Process Procedural Flora Automation Request";
        private const string ReportFileName = "PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md";
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string AutomationFolderName = "CodexFloraAutomation";
        private const string AutomationRequestFileName = "flora_request.json";
        private const string AutomationResponseFileName = "flora_response.json";
        private const string AutomationPreviewFolder = "Screenshots/Automation";
        private const double AutomationPollIntervalSeconds = 0.5d;
        private const double AutomationPreviewTimeoutSeconds = 75d;
        private const int AutomationPreviewWidth = 512;
        private const int AutomationPreviewHeight = 512;
        private const int AutomationPreviewTasksPerUpdate = 4;
        private const float LodThresholdTolerance = 0.0005f;
        private const float RequiredLod0Threshold = 0.6f;
        private const float RequiredLod1Threshold = 0.15f;
        private const float RequiredLod2Threshold = 0.04f;
        private static readonly Vector3[] AutomationPreviewDirections =
        {
            new Vector3(0f, 0.12f, -1f),
            new Vector3(-0.72f, 0.16f, -1f),
            new Vector3(0.72f, 0.16f, -1f),
            new Vector3(-0.24f, 0.62f, -0.88f)
        };

        private static readonly List<AutomationPreviewTask> _automationPreviewTasks = new List<AutomationPreviewTask>(8); // COLD ALLOC: editor automation queue, bounded by explicit request payload

        private static double _automationNextPollTime;
        private static bool _automationRequestActive;
        private static bool _automationBridgeRegistered;
        private static AutomationResponse _activeAutomationResponse;
        private static double _automationPreviewDeadline;

        [MenuItem(AutomationArmMenuPath, priority = 242)]
        private static void ArmAutomationBridge()
        {
            RegisterAutomationBridge();
        }

        [MenuItem(AutomationDisarmMenuPath, priority = 243)]
        private static void DisarmAutomationBridge()
        {
            UnregisterAutomationBridge();
        }

        [MenuItem(AutomationPollMenuPath, priority = 244)]
        private static void ProcessAutomationRequest()
        {
            TryBeginAutomationRequest();
        }

        [MenuItem(AutomationArmMenuPath, true)]
        private static bool ArmAutomationBridgeValidate()
        {
            return !_automationBridgeRegistered;
        }

        [MenuItem(AutomationDisarmMenuPath, true)]
        private static bool DisarmAutomationBridgeValidate()
        {
            return _automationBridgeRegistered;
        }

        private static void RegisterAutomationBridge()
        {
            if (_automationBridgeRegistered)
                return;

            EditorApplication.update += UpdateAutomationBridge;
            _automationNextPollTime = EditorApplication.timeSinceStartup + AutomationPollIntervalSeconds;
            _automationBridgeRegistered = true;
        }

        private static void UnregisterAutomationBridge()
        {
            if (!_automationBridgeRegistered)
                return;

            EditorApplication.update -= UpdateAutomationBridge;
            _automationBridgeRegistered = false;
        }

        [MenuItem("Hecton8/Validation/Generate Procedural Flora Final Status Report", priority = 241)]
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
                EnsureAutomationPreviewFolderExists();

                WorldProceduralFloraMaterialAuthoring.Apply();
                WorldProceduralFloraBakedStarterGenerator.Generate();
                WorldProceduralFloraFinalVariantAuthoring.ApplyBakedFloraFinals();
                WorldProceduralFloraFinalVariantValidator.Validate();
                WorldProceduralFloraTextureAuthoring.ReportImportedTextureLibrary();
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
            string[] capturePaths = request.capturePrefabPaths != null && request.capturePrefabPaths.Length > 0
                ? request.capturePrefabPaths
                : request.prefabPaths;
            if (capturePaths == null || capturePaths.Length == 0)
                return;

            for (int i = 0; i < capturePaths.Length; i++)
            {
                string prefabPath = capturePaths[i];
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
            if (preview != null
                && preview.width >= AutomationPreviewWidth
                && preview.height >= AutomationPreviewHeight)
            {
                task.previewPath = SaveAutomationPreview(task.prefabPath, preview);
                task.prefabAsset = null;
                task.isDone = true;
                return task;
            }

            if (!AssetPreview.IsLoadingAssetPreview(task.prefabAsset.GetEntityId()))
            {
                Texture2D miniPreview = AssetPreview.GetMiniThumbnail(task.prefabAsset);
                task.previewPath = null;
                task.error = miniPreview == null ? "preview_unavailable" : "preview_fallback_too_small";
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
                previewUtility.ambientColor = new Color(0.5f, 0.56f, 0.62f, 1f);

                GameObject prefabRoot = previewUtility.InstantiatePrefabInScene(prefabAsset);
                if (prefabRoot == null)
                    return null;

                if (!prefabRoot.activeSelf)
                    prefabRoot.SetActive(true);

                PrepareAutomationPreviewHierarchy(prefabRoot);

                Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return null;

                ApplyAutomationPreviewPresentationYaw(prefabRoot, renderers);
                Bounds bounds = CalculateAutomationPreviewBounds(renderers);

                Camera camera = previewUtility.camera;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.16f, 0.2f, 0.24f, 1f);
                camera.fieldOfView = 32f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.enabled = false;
                camera.orthographic = true;

                Light keyLight = previewUtility.lights[0];
                keyLight.intensity = 1.52f;
                keyLight.color = new Color(1f, 0.98f, 0.94f, 1f);
                keyLight.transform.rotation = Quaternion.Euler(36f, -26f, 0f);

                Light fillLight = previewUtility.lights[1];
                fillLight.intensity = 1.08f;
                fillLight.color = new Color(0.72f, 0.9f, 1f, 1f);
                fillLight.transform.rotation = Quaternion.Euler(328f, 148f, 0f);

                viewTextures = new Texture2D[4]; // COLD ALLOC: editor-only contact sheet generation, fixed 4-view payload
                viewTextures[0] = RenderAutomationPreviewView(previewUtility, bounds, AutomationPreviewDirections[0], 0.06f, 1.42f);
                viewTextures[1] = RenderAutomationPreviewView(previewUtility, bounds, AutomationPreviewDirections[1], 0.08f, 1.38f);
                viewTextures[2] = RenderAutomationPreviewView(previewUtility, bounds, AutomationPreviewDirections[2], 0.08f, 1.38f);
                viewTextures[3] = RenderAutomationPreviewView(previewUtility, bounds, AutomationPreviewDirections[3], -0.08f, 1.02f);

                for (int i = 0; i < viewTextures.Length; i++)
                {
                    if (viewTextures[i] == null)
                        return null;
                }

                contactSheet = BuildAutomationPreviewContactSheet(viewTextures);
                return contactSheet != null ? SaveAutomationPreview(prefabPath, contactSheet) : null;
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
            camera.cullingMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
            camera.nearClipPlane = 0.01f;

            Vector3 normalizedViewDirection = ResolveDominantAxisDirection(viewDirection, new Vector3(-0.42f, 0.24f, -1f));
            Vector3 worldUp = Mathf.Abs(Vector3.Dot(normalizedViewDirection, Vector3.up)) > 0.96f
                ? Vector3.forward
                : Vector3.up;
            Vector3 right = DominantAxisVector(Vector3.Cross(worldUp, normalizedViewDirection), Vector3.right);
            Vector3 up = DominantAxisVector(Vector3.Cross(normalizedViewDirection, right), Vector3.up);

            float focusYOffset = bounds.extents.y * focusYOffsetNormalized;
            Vector3 focus = bounds.center + Vector3.up * focusYOffset;

            float aspect = AutomationPreviewWidth / (float)AutomationPreviewHeight;
            float maxHorizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float slenderness = bounds.size.y / Mathf.Max(0.08f, maxHorizontalExtent * 2f);
            float tallBias = Mathf.Clamp01((slenderness - 1.35f) / 3.1f);
            float crownBias = Mathf.Clamp01((slenderness - 2.05f) / 3.2f);
            float projectedVertical = Mathf.Max(EvaluateProjectedBoundsHalfExtent(bounds, up), bounds.extents.y * 1.04f);
            float projectedHorizontal = Mathf.Max(
                EvaluateProjectedBoundsHalfExtent(bounds, right),
                Mathf.Max(bounds.extents.x, bounds.extents.z) * Mathf.Lerp(1.08f, 1.01f, tallBias));
            focus += Vector3.up * (bounds.extents.y * Mathf.Lerp(0.06f, 0.34f, crownBias) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(zoomScale - 0.82f)));
            float preferredVerticalFit = Mathf.Max(
                bounds.extents.y * Mathf.Lerp(0.76f, 0.6f, tallBias),
                projectedVertical * Mathf.Lerp(0.8f, 0.66f, crownBias));
            projectedVertical = Mathf.Lerp(projectedVertical, preferredVerticalFit, Mathf.Lerp(0.18f, 0.94f, tallBias));
            float tallCompensation = Mathf.Lerp(1f, 1.09f, tallBias);
            float crownZoomTightening = Mathf.Lerp(1f, 0.76f, crownBias * Mathf.Clamp01(zoomScale - 0.82f));
            float effectiveZoomScale = zoomScale >= 1.2f
                ? zoomScale * tallCompensation * crownZoomTightening
                : zoomScale * Mathf.Lerp(1f, tallCompensation, 0.32f) * crownZoomTightening;
            float framingPadding = Mathf.Lerp(1.08f, 0.94f, tallBias);
            float orthographicSize = Mathf.Max(
                projectedVertical * Mathf.Max(framingPadding, effectiveZoomScale * framingPadding),
                (projectedHorizontal / Mathf.Max(0.1f, aspect)) * Mathf.Max(framingPadding, effectiveZoomScale * framingPadding));
            orthographicSize = Mathf.Max(orthographicSize, 0.24f);
            float tallZoomTightening = Mathf.Lerp(1f, 0.7f, tallBias);
            float crownFramingTightening = Mathf.Lerp(1f, 0.84f, crownBias);
            orthographicSize = Mathf.Max(0.18f, orthographicSize * tallZoomTightening * crownFramingTightening);
            float boundsRadius = FastExtentRadius(bounds.extents);
            float fitDistance = Mathf.Max(boundsRadius * 3.2f, orthographicSize * 3.1f);

            camera.transform.position = focus - normalizedViewDirection * fitDistance;
            camera.transform.rotation = Quaternion.LookRotation(normalizedViewDirection, up);
            camera.orthographicSize = orthographicSize;
            camera.farClipPlane = fitDistance * 3.6f + boundsRadius * 2.6f + 8f;

            previewUtility.BeginStaticPreview(new Rect(0f, 0f, AutomationPreviewWidth, AutomationPreviewHeight));
            previewUtility.Render(true, true);
            return previewUtility.EndStaticPreview();
        }

        private static void ApplyAutomationPreviewPresentationYaw(GameObject prefabRoot, Renderer[] renderers)
        {
            if (prefabRoot == null || renderers == null || renderers.Length == 0)
                return;

            Quaternion originalRotation = prefabRoot.transform.rotation;
            float bestYaw = 0f;
            float bestScore = float.MinValue;

            for (int sampleIndex = 0; sampleIndex < 24; sampleIndex++)
            {
                float yaw = sampleIndex * 15f;
                prefabRoot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Bounds bounds = CalculateAutomationPreviewBounds(renderers);
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                float depth = Mathf.Min(bounds.size.x, bounds.size.z);
                float frontHeight = bounds.size.y;
                float frontCoverage = EvaluateAutomationPreviewCoverage(bounds, AutomationPreviewDirections[0], false);
                float leftCoverage = EvaluateAutomationPreviewCoverage(bounds, AutomationPreviewDirections[1], false);
                float rightCoverage = EvaluateAutomationPreviewCoverage(bounds, AutomationPreviewDirections[2], false);
                float heroCoverage = EvaluateAutomationPreviewCoverage(bounds, AutomationPreviewDirections[3], true);
                float minimumReadableCoverage = Mathf.Min(frontCoverage, Mathf.Min(leftCoverage, rightCoverage));
                float verticalPreference = Mathf.Clamp01(width / Mathf.Max(0.08f, frontHeight));
                float aspect = depth <= 0.0001f ? 0f : width / depth;
                float thinPenalty = Mathf.Clamp01(Mathf.InverseLerp(5f, 1.5f, aspect));
                float score = (width * 0.92f + depth * 0.58f + frontCoverage * 2.4f + leftCoverage * 1.68f + rightCoverage * 1.68f + heroCoverage * 1.2f + minimumReadableCoverage * 2.1f + frontHeight * 0.06f)
                    * Mathf.Lerp(0.58f, 1f, thinPenalty)
                    * Mathf.Lerp(0.86f, 1f, verticalPreference);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestYaw = yaw;
                }
            }

            prefabRoot.transform.rotation = originalRotation * Quaternion.Euler(0f, bestYaw, 0f);
        }

        private static Bounds CalculateAutomationPreviewBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static float EvaluateProjectedBoundsHalfExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            float ax = Mathf.Abs(Vector3.Dot(new Vector3(extents.x, 0f, 0f), axis));
            float ay = Mathf.Abs(Vector3.Dot(new Vector3(0f, extents.y, 0f), axis));
            float az = Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, extents.z), axis));
            return ax + ay + az;
        }

        private static float EvaluateAutomationPreviewCoverage(Bounds bounds, Vector3 viewDirection, bool heroView)
        {
            Vector3 normalizedViewDirection = ResolveDominantAxisDirection(viewDirection, new Vector3(0f, 0.12f, -1f));
            Vector3 worldUp = Mathf.Abs(Vector3.Dot(normalizedViewDirection, Vector3.up)) > 0.96f
                ? Vector3.forward
                : Vector3.up;
            Vector3 right = DominantAxisVector(Vector3.Cross(worldUp, normalizedViewDirection), Vector3.right);
            Vector3 up = DominantAxisVector(Vector3.Cross(normalizedViewDirection, right), Vector3.up);
            float projectedVertical = Mathf.Max(EvaluateProjectedBoundsHalfExtent(bounds, up), bounds.extents.y * 1.04f);

            float maxHorizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float slenderness = bounds.size.y / Mathf.Max(0.08f, maxHorizontalExtent * 2f);
            float tallBias = Mathf.Clamp01((slenderness - 1.35f) / 3.1f);
            float crownBias = Mathf.Clamp01((slenderness - 2.05f) / 3.2f);
            float projectedHorizontal = Mathf.Max(
                EvaluateProjectedBoundsHalfExtent(bounds, right),
                Mathf.Max(bounds.extents.x, bounds.extents.z) * Mathf.Lerp(1.08f, 1.01f, tallBias));
            float zoomScale = heroView ? 1.02f : 1.38f;
            float preferredVerticalFit = Mathf.Max(
                bounds.extents.y * Mathf.Lerp(0.76f, 0.6f, tallBias),
                projectedVertical * Mathf.Lerp(0.8f, 0.66f, crownBias));
            projectedVertical = Mathf.Lerp(projectedVertical, preferredVerticalFit, Mathf.Lerp(0.18f, 0.94f, tallBias));
            float tallCompensation = Mathf.Lerp(1f, 1.09f, tallBias);
            float crownZoomTightening = Mathf.Lerp(1f, 0.76f, crownBias * Mathf.Clamp01(zoomScale - 0.82f));
            float effectiveZoomScale = zoomScale >= 1.2f
                ? zoomScale * tallCompensation * crownZoomTightening
                : zoomScale * Mathf.Lerp(1f, tallCompensation, 0.32f) * crownZoomTightening;
            float aspect = AutomationPreviewWidth / (float)AutomationPreviewHeight;
            float framingPadding = Mathf.Lerp(1.08f, 0.94f, tallBias);
            float orthographicSize = Mathf.Max(
                projectedVertical * Mathf.Max(framingPadding, effectiveZoomScale * framingPadding),
                (projectedHorizontal / Mathf.Max(0.1f, aspect)) * Mathf.Max(framingPadding, effectiveZoomScale * framingPadding));
            orthographicSize = Mathf.Max(orthographicSize, 0.24f);
            float tallZoomTightening = Mathf.Lerp(1f, 0.7f, tallBias);
            float crownFramingTightening = Mathf.Lerp(1f, 0.84f, crownBias);
            orthographicSize = Mathf.Max(0.18f, orthographicSize * tallZoomTightening * crownFramingTightening);

            float normalizedHorizontal = projectedHorizontal / Mathf.Max(0.001f, orthographicSize * aspect);
            float normalizedVertical = projectedVertical / Mathf.Max(0.001f, orthographicSize);
            return normalizedHorizontal * normalizedVertical;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 value, Vector3 fallback)
        {
            if (value.sqrMagnitude <= 0.0001f)
                value = fallback;

            return DominantAxisVector(value, Vector3.forward);
        }

        private static Vector3 DominantAxisVector(Vector3 value, Vector3 fallback)
        {
            float dominant = Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
            if (dominant <= 0.0001f)
                return fallback;

            return value / dominant;
        }

        private static float FastExtentRadius(Vector3 extents)
        {
            float x = Mathf.Abs(extents.x);
            float y = Mathf.Abs(extents.y);
            float z = Mathf.Abs(extents.z);
            float max = Mathf.Max(x, Mathf.Max(y, z));
            float min = Mathf.Min(x, Mathf.Min(y, z));
            float mid = x + y + z - max - min;
            return max + mid * 0.5f + min * 0.25f;
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
            NativeArray<Color32> sheetPixels = contactSheet.GetRawTextureData<Color32>();
            Color32 background = new Color32(71, 71, 77, 255);
            for (int i = 0; i < sheetPixels.Length; i++)
                sheetPixels[i] = background;

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

            if (!TryGetRawPixels(source, out NativeArray<Color32> sourcePixels) ||
                !TryGetRawPixels(contactSheet, out NativeArray<Color32> sheetPixels))
            {
                return;
            }

            int sheetWidth = contactSheet.width;
            int copyWidth = Mathf.Min(source.width, contactSheet.width - startX);
            int copyHeight = Mathf.Min(source.height, contactSheet.height - startY);
            for (int y = 0; y < copyHeight; y++)
            {
                int sourceRow = y * source.width;
                int targetRow = (startY + y) * sheetWidth + startX;
                for (int x = 0; x < copyWidth; x++)
                    sheetPixels[targetRow + x] = sourcePixels[sourceRow + x];
            }
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

            byte[] pngBytes = source.EncodeToPNG(); // COLD ALLOC: byte[] - editor-only automation preview PNG encode output - owner: WorldProceduralFloraFinalStatusReport
            using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(pngBytes, 0, pngBytes.Length);
                stream.Flush(true);
            }

            return assetPath;
        }

        private static bool IsAutomationPreviewMeaningful(Texture2D texture, Color backgroundColor)
        {
            if (texture == null)
                return false;

            if (!TryGetRawPixels(texture, out NativeArray<Color32> pixels) || pixels.Length == 0)
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

        private static bool TryGetRawPixels(Texture2D texture, out NativeArray<Color32> pixels)
        {
            pixels = default;
            if (texture == null)
                return false;

            TextureFormat format = texture.format;
            if (format != TextureFormat.RGBA32 &&
                format != TextureFormat.ARGB32 &&
                format != TextureFormat.BGRA32)
            {
                return false;
            }

            pixels = texture.GetRawTextureData<Color32>();
            return pixels.Length == texture.width * texture.height;
        }

        private static bool HasMeaningfulAutomationView(Texture2D[] viewTextures, Color backgroundColor)
        {
            if (viewTextures == null)
                return false;

            for (int i = 0; i < viewTextures.Length; i++)
            {
                if (IsAutomationPreviewMeaningful(viewTextures[i], backgroundColor))
                    return true;
            }

            return false;
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

        private static string GetAutomationPreviewFolderPath()
        {
            return Path.Combine(GetProjectRootPath(), AutomationPreviewFolder.Replace('/', Path.DirectorySeparatorChar));
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

        private static void EnsureAutomationPreviewFolderExists()
        {
            string previewFolderPath = GetAutomationPreviewFolderPath();
            if (!Directory.Exists(previewFolderPath))
                Directory.CreateDirectory(previewFolderPath);
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
                    EvaluateMaterialState(familyId, renderers, isGenerated),
                    EvaluateLodState(lodGroups),
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
            HashSet<Renderer> seen = new HashSet<Renderer>(allRenderers != null ? allRenderers.Length : 8);
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

        private static MaterialState EvaluateMaterialState(string familyId, Renderer[] renderers, bool isGenerated)
        {
            string expectedShaderLabel = WorldProceduralFloraMaterialAuthoring.DescribeExpectedShaderVariant(familyId);
            bool instancingOk = true;
            bool shaderOk = true;
            bool shaderContractOk = true;
            bool textureStackOk = true;
            bool importedTextureContractOk = true;
            bool textureSourceOk = true;
            bool textureStackSourceOk = true;
            bool generatedTextureSourceUsed = false;
            bool anyMaterial = false;
            string shaderContractFailure = string.Empty;
            string importedTextureContractFailure = string.Empty;
            string textureSourceFailure = string.Empty;
            string textureStackSourceFailure = string.Empty;

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
                        shaderContractOk = false;
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
                            shaderContractOk = false;
                            textureStackOk = false;
                            continue;
                        }

                        anyMaterial = true;
                        if (!material.enableInstancing)
                            instancingOk = false;

                        if (string.IsNullOrEmpty(expectedShaderLabel))
                            continue;

                        if (!WorldProceduralFloraMaterialAuthoring.IsAcceptedFloraShader(material.shader, familyId))
                        {
                            shaderOk = false;
                        }
                        else
                        {
                            string currentFailure;
                            if (WorldProceduralFloraMaterialAuthoring.TryGetShaderContractFailure(material, out currentFailure))
                            {
                                shaderContractOk = false;
                                if (string.IsNullOrEmpty(shaderContractFailure))
                                    shaderContractFailure = currentFailure;
                            }
                        }

                        if (material.GetTexture("_BaseMap") == null
                            || material.GetTexture("_DetailMap") == null
                            || material.GetTexture("_NormalMap") == null
                            || material.GetTexture("_MaskMap") == null)
                        {
                            textureStackOk = false;
                        }

                        generatedTextureSourceUsed |=
                            WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(material.GetTexture("_BaseMap"))
                            || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(material.GetTexture("_DetailMap"))
                            || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(material.GetTexture("_NormalMap"))
                            || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(material.GetTexture("_MaskMap"));

                        string currentTextureSourceFailure;
                        if (WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(material.GetTexture("_BaseMap"), familyId, "albedo", out currentTextureSourceFailure)
                            || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(material.GetTexture("_DetailMap"), familyId, "detail", out currentTextureSourceFailure)
                            || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(material.GetTexture("_NormalMap"), familyId, "normal", out currentTextureSourceFailure)
                            || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(material.GetTexture("_MaskMap"), familyId, "mask", out currentTextureSourceFailure))
                        {
                            textureSourceOk = false;
                            if (string.IsNullOrEmpty(textureSourceFailure))
                                textureSourceFailure = currentTextureSourceFailure;
                        }

                        string currentTextureStackFailure;
                        if (WorldProceduralFloraTextureAuthoring.TryGetTextureStackSourceFailure(
                                material.GetTexture("_BaseMap"),
                                material.GetTexture("_DetailMap"),
                                material.GetTexture("_NormalMap"),
                                material.GetTexture("_MaskMap"),
                                out currentTextureStackFailure))
                        {
                            textureStackSourceOk = false;
                            if (string.IsNullOrEmpty(textureStackSourceFailure))
                                textureStackSourceFailure = currentTextureStackFailure;
                        }

                        string currentImportedTextureFailure;
                        if (TryGetImportedTextureContractFailure(material, familyId, out currentImportedTextureFailure))
                        {
                            importedTextureContractOk = false;
                            if (string.IsNullOrEmpty(importedTextureContractFailure))
                                importedTextureContractFailure = currentImportedTextureFailure;
                        }
                    }
                }
            }

            if (!anyMaterial)
                return new MaterialState(false, false, false, false, false, "missing-materials");

            if (string.IsNullOrEmpty(expectedShaderLabel))
                return new MaterialState(instancingOk, true, true, true, !generatedTextureSourceUsed, instancingOk ? "ok" : "instancing-off");

            if (instancingOk && shaderOk && shaderContractOk && textureStackOk && importedTextureContractOk && !generatedTextureSourceUsed)
                return new MaterialState(true, true, true, true, true, "ok");

            if (!shaderOk)
                return new MaterialState(instancingOk, false, true, textureStackOk, !generatedTextureSourceUsed, "shader-mismatch");

            if (!shaderContractOk)
                return new MaterialState(instancingOk, true, false, textureStackOk, !generatedTextureSourceUsed, "shader-contract-stale:" + shaderContractFailure);

            if (!textureStackOk)
                return new MaterialState(instancingOk, true, true, false, !generatedTextureSourceUsed, "texture-stack-missing");

            if (!textureSourceOk)
                return new MaterialState(instancingOk, true, true, false, !generatedTextureSourceUsed, "texture-source-unmanaged:" + textureSourceFailure);

            if (!textureStackSourceOk)
                return new MaterialState(instancingOk, true, true, false, !generatedTextureSourceUsed, "texture-stack-source-mixed:" + textureStackSourceFailure);

            if (!importedTextureContractOk)
                return new MaterialState(instancingOk, true, true, false, !generatedTextureSourceUsed, "imported-texture-contract-stale:" + importedTextureContractFailure);

            if (generatedTextureSourceUsed)
                return new MaterialState(instancingOk, true, true, true, false, isGenerated ? "starter-generated-textures" : "authored-generated-textures");

            return new MaterialState(false, true, true, true, true, "instancing-off");
        }

        private static bool TryGetImportedTextureContractFailure(Material material, string familyId, out string failureLabel)
        {
            failureLabel = string.Empty;
            if (material == null || string.IsNullOrEmpty(familyId))
                return false;

            if (TryGetImportedTextureFailure(material.GetTexture("_BaseMap"), familyId, "albedo", out failureLabel))
                return true;

            if (TryGetImportedTextureFailure(material.GetTexture("_DetailMap"), familyId, "detail", out failureLabel))
                return true;

            if (TryGetImportedTextureFailure(material.GetTexture("_NormalMap"), familyId, "normal", out failureLabel))
                return true;

            if (TryGetImportedTextureFailure(material.GetTexture("_MaskMap"), familyId, "mask", out failureLabel))
                return true;

            return false;
        }

        private static bool TryGetImportedTextureFailure(Texture texture, string familyId, string mapToken, out string failureLabel)
        {
            if (texture == null)
            {
                failureLabel = string.Empty;
                return false;
            }

            string contractFailure;
            if (WorldProceduralFloraTextureAuthoring.TryGetImportedTextureContractFailure(texture, familyId, mapToken, out contractFailure))
            {
                failureLabel = mapToken + ":" + contractFailure;
                return true;
            }

            failureLabel = string.Empty;
            return false;
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

        private static LODState EvaluateLodState(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return new LODState(false, "missing-lodgroup");

            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                if (lodGroup.fadeMode != LODFadeMode.CrossFade)
                    return new LODState(false, "fade-not-crossfade");

                if (!lodGroup.animateCrossFading)
                    return new LODState(false, "crossfade-disabled");

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null || lods.Length != 3)
                    return new LODState(false, "lod-count-mismatch");

                if (!MatchesLodTransition(lods[0].screenRelativeTransitionHeight, RequiredLod0Threshold)
                    || !MatchesLodTransition(lods[1].screenRelativeTransitionHeight, RequiredLod1Threshold)
                    || !MatchesLodTransition(lods[2].screenRelativeTransitionHeight, RequiredLod2Threshold))
                {
                    return new LODState(false, "threshold-mismatch");
                }
            }

            return new LODState(true, "ok");
        }

        private static bool MatchesLodTransition(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= LodThresholdTolerance;
        }

        private static string BuildMarkdown(string rootFolder, IReadOnlyDictionary<string, FamilyStatus> statusByFamily)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Flora Final Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(rootFolder).AppendLine("`");
            builder.Append("- Generated: `GEN_` prefabs are starter finals only.").AppendLine();
            builder.Append("- Texture proof: procedural editor-generated `.asset` textures do not count as authored photoreal final proof.").AppendLine();
            builder.Append("- Shader proof: material contract rejects `_QUALITY_MX350`/`_QUALITY_HIGH` and requires positive triplanar/normal/fresnel/parallax properties.").AppendLine();
            builder.Append("- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.").AppendLine();
            builder.Append("- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.").AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Contract | LOD Contract | Fidelity Floor |");
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
                builder.Append("- Material-contract prefabs: `").Append(status.MaterialReadyPrefabCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
                builder.Append("- Exact LOD contract prefabs: `").Append(status.PrefabsWithValidLodCascadeCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
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
                    builder.Append(" | lodContract=").Append(prefab.LodContractLabel);
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
            public string requestId = null;
            public string[] capturePrefabPaths = null;
            public string[] prefabPaths = null;
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
                LODState lodState,
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
                HasValidLodCascade = lodState.IsOk;
                LodContractLabel = lodState.Label ?? string.Empty;
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
            public string LodContractLabel { get; }
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
            public MaterialState(bool instancingOk, bool shaderOk, bool shaderContractOk, bool textureStackOk, bool finalTextureSourceOk, string label)
            {
                InstancingOk = instancingOk;
                ShaderOk = shaderOk;
                ShaderContractOk = shaderContractOk;
                TextureStackOk = textureStackOk;
                FinalTextureSourceOk = finalTextureSourceOk;
                Label = label ?? string.Empty;
            }

            public bool InstancingOk { get; }
            public bool ShaderOk { get; }
            public bool ShaderContractOk { get; }
            public bool TextureStackOk { get; }
            public bool FinalTextureSourceOk { get; }
            public string Label { get; }
            public bool IsOk => InstancingOk && ShaderOk && ShaderContractOk && TextureStackOk && FinalTextureSourceOk;
        }

        private readonly struct LODState
        {
            public LODState(bool isOk, string label)
            {
                IsOk = isOk;
                Label = label ?? string.Empty;
            }

            public bool IsOk { get; }
            public string Label { get; }
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

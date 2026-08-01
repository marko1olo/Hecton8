#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using System.IO;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor utility to create a standardized loading screen prefab.
    /// Run via: Tools > HECTON-8 > Create Loading Screen Prefab
    /// </summary>
    public static class LoadingScreenPrefabCreator
    {
        [MenuItem("Tools/HECTON-8/Create Loading Screen Prefab")]
        public static void CreateLoadingScreenPrefab()
        {
            // -executeMethod / CI: never open DisplayDialog in batchmode.
            bool batch = Application.isBatchMode;
            string prefabPath = "Assets/_Project/Prefabs/UI/LoadingScreen.prefab";

            // Ensure directory exists
            string directory = Path.GetDirectoryName(prefabPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Check if prefab already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                // Batchmode: leave shipping prefab intact (no overwrite prompt).
                if (batch)
                {
                    Debug.Log("[LoadingScreenPrefabCreator] RESULT: PASS — Prefab already exists, skipped overwrite in batchmode: " + prefabPath);
                    return;
                }

                if (!EditorUtility.DisplayDialog("Overwrite Existing Prefab?",
                    "A LoadingScreen prefab already exists. Overwrite it?", "Yes", "No"))
                {
                    return;
                }
            }

            // Create root GameObject
            GameObject root = new GameObject("LoadingScreen");
            root.AddComponent<CanvasGroup>();

            // Add LoadingScreenController
            var controller = root.AddComponent<Hecton8.UI.LoadingScreenController>();
            SerializedObject controllerSerializedObject = new SerializedObject(controller);

            // Create Canvas
            GameObject canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(root.transform);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Above other UI

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create loading panel
            GameObject panelGO = new GameObject("LoadingPanel");
            panelGO.transform.SetParent(canvasGO.transform);

            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.05f, 0.1f, 0.95f); // Dark blue semi-transparent

            panelGO.TryGetComponent(out RectTransform panelRect);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            CanvasGroup panelGroup = panelGO.AddComponent<CanvasGroup>();
            controllerSerializedObject.FindProperty("_loadingPanel").objectReferenceValue = panelGroup;

            // Create progress bar
            GameObject progressGO = new GameObject("ProgressBar");
            progressGO.transform.SetParent(panelGO.transform);

            Image progressBg = progressGO.AddComponent<Image>();
            progressBg.color = Color.gray;

            Slider slider = progressGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            controllerSerializedObject.FindProperty("_progressBar").objectReferenceValue = slider;

            progressGO.TryGetComponent(out RectTransform progressRect);
            progressRect.anchorMin = new Vector2(0.1f, 0.4f);
            progressRect.anchorMax = new Vector2(0.9f, 0.5f);
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = Vector2.zero;

            // Progress bar fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(progressGO.transform);

            Image fillImage = fillGO.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 1f); // Cyan

            fillGO.TryGetComponent(out RectTransform fillRect);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            slider.fillRect = fillRect;

            // Progress text
            GameObject progressTextGO = new GameObject("ProgressText");
            progressTextGO.transform.SetParent(panelGO.transform);

            TextMeshProUGUI progressText = progressTextGO.AddComponent<TextMeshProUGUI>();
            progressText.text = "0%";
            progressText.fontSize = 24;
            progressText.color = Color.white;
            progressText.alignment = TextAlignmentOptions.Center;
            controllerSerializedObject.FindProperty("_progressText").objectReferenceValue = progressText;

            progressTextGO.TryGetComponent(out RectTransform progressTextRect);
            progressTextRect.anchorMin = new Vector2(0.4f, 0.35f);
            progressTextRect.anchorMax = new Vector2(0.6f, 0.4f);
            progressTextRect.offsetMin = Vector2.zero;
            progressTextRect.offsetMax = Vector2.zero;

            // Status text
            GameObject statusTextGO = new GameObject("StatusText");
            statusTextGO.transform.SetParent(panelGO.transform);

            TextMeshProUGUI statusText = statusTextGO.AddComponent<TextMeshProUGUI>();
            statusText.text = "Loading...";
            statusText.fontSize = 18;
            statusText.color = new Color(0.8f, 0.8f, 0.8f);
            statusText.alignment = TextAlignmentOptions.Center;
            controllerSerializedObject.FindProperty("_statusText").objectReferenceValue = statusText;

            statusTextGO.TryGetComponent(out RectTransform statusTextRect);
            statusTextRect.anchorMin = new Vector2(0.2f, 0.55f);
            statusTextRect.anchorMax = new Vector2(0.8f, 0.65f);
            statusTextRect.offsetMin = Vector2.zero;
            statusTextRect.offsetMax = Vector2.zero;

            // Tip text
            GameObject tipTextGO = new GameObject("TipText");
            tipTextGO.transform.SetParent(panelGO.transform);

            TextMeshProUGUI tipText = tipTextGO.AddComponent<TextMeshProUGUI>();
            tipText.text = "The ocean holds many secrets...";
            tipText.fontSize = 14;
            tipText.color = new Color(0.6f, 0.6f, 0.6f);
            tipText.alignment = TextAlignmentOptions.Center;
            controllerSerializedObject.FindProperty("_tipText").objectReferenceValue = tipText;

            tipTextGO.TryGetComponent(out RectTransform tipTextRect);
            tipTextRect.anchorMin = new Vector2(0.1f, 0.1f);
            tipTextRect.anchorMax = new Vector2(0.9f, 0.2f);
            tipTextRect.offsetMin = Vector2.zero;
            tipTextRect.offsetMax = Vector2.zero;

            controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            // Create prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"[LoadingScreenPrefabCreator] RESULT: PASS — Created loading screen prefab at {prefabPath}");

            // Select the prefab in project window (interactive only)
            if (!batch)
            {
                Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
            }
        }
    }
}
#endif

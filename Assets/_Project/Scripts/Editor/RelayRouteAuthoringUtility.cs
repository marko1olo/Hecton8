#if UNITY_EDITOR
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton8.UI;
using Hecton8.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace Hecton8.Editor
{
    internal static class RelayRouteAuthoringUtility
    {
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string RelayOneName = "Relay_Test_01";
        private const string RelayTwoName = "Relay_Test_02";
        private const string RelayChainId = "intro_service_route";
        private const string ManagersRootName = "[MANAGERS]";
        private const string ActiveHudOverlayName = "Suit_HUD_Canvas";
        private const string HudRootName = "HUD_V4_CanvasRoot";
        private const string MarkerLayerName = "HUD_RouteMarkerLayer";
        private const string MarkerRootName = "RelayRouteMarker";
        private const string SuitHudPrefabPath = "Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab";
        private const string O2CanisterPath = "Assets/_Project/Data/Items/Resources/Processed/Data_EmergencyO2Canister.asset";
        private const string BatteryCellPath = "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset";
        private const string AudioLogPath = "Assets/_Project/Data/Lore/AudioLogs/AudioLog_biologist_samples.asset";

        public static string CreateOrUpdateTestRelayRoute()
        {
            return CreateTestRelayRouteInternal();
        }

        public static string VerifyRelayHudRouteNow()
        {
            return VerifyRelayHudRouteInternal();
        }

        [MenuItem("HECTON8/Onboarding/Create Test Relay Route")]
        private static void CreateTestRelayRoute()
        {
            CreateTestRelayRouteInternal();
        }

        [MenuItem("HECTON8/Onboarding/Verify Relay HUD Route")]
        private static void VerifyRelayHudRoute()
        {
            VerifyRelayHudRouteInternal();
        }

        [MenuItem("HECTON8/Onboarding/Author Relay Runtime Owners")]
        private static void AuthorRelayRuntimeOwners()
        {
            AuthorRelayRuntimeOwnersInternal();
        }

        private static string CreateTestRelayRouteInternal()
        {
            if (!EnsureTargetSceneOpen())
                return "scene_unavailable";

            Transform player = GameObject.Find("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] Player object not found in scene.");
                return "player_missing";
            }

            ItemData firstReward = AssetDatabase.LoadAssetAtPath<ItemData>(O2CanisterPath);
            ItemData secondReward = AssetDatabase.LoadAssetAtPath<ItemData>(BatteryCellPath);
            AudioLogData audioLog = AssetDatabase.LoadAssetAtPath<AudioLogData>(AudioLogPath);
            if (firstReward == null || secondReward == null || audioLog == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] Required relay assets are missing.");
                return "asset_missing";
            }

            EmergencyServiceRelay relayOne = CreateOrUpdateRelay(RelayOneName, player.position + player.forward * 8f + player.right * 3f);
            EmergencyServiceRelay relayTwo = CreateOrUpdateRelay(RelayTwoName, player.position + player.forward * 34f + player.right * 6f + Vector3.down * 50f);

            ConfigureRelay(relayOne, "relay_test_01", 0, firstReward, null, relayTwo);
            ConfigureRelay(relayTwo, "relay_test_02", 1, secondReward, audioLog, null);

            EditorSceneManager.MarkSceneDirty(relayOne.gameObject.scene);
            Debug.Log("[RelayRouteAuthoringUtility] Test relay route created/updated.");
            return "ok";
        }

        private static string AuthorRelayRuntimeOwnersInternal()
        {
            if (!EnsureTargetSceneOpen())
                return "scene_unavailable";

            EnsureRelayHudPrefabMarker();

            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] [MANAGERS] root not found.");
                return "managers_missing";
            }

            if (!managersRoot.TryGetComponent(out WorldReadabilityDirector _))
                managersRoot.AddComponent<WorldReadabilityDirector>();

            if (!managersRoot.TryGetComponent(out EmergencyServiceRelayDirector _))
                managersRoot.AddComponent<EmergencyServiceRelayDirector>();

            SuitHUDV4CanvasOverlay overlay = ResolvePreferredOverlay();
            if (overlay == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] SuitHUDV4CanvasOverlay not found.");
                return "overlay_missing";
            }

            RectTransform parent = ResolveMarkerParent(overlay.transform);

            if (parent == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] Relay marker parent could not be resolved.");
                return "hud_root_missing";
            }

            RemoveMarkersFromNonPreferredOverlays(overlay);
            RelayHUDElement marker = ResolveOverlayMarker(overlay);
            if (marker != null && marker.transform.parent != parent)
                marker.transform.SetParent(parent, false);

            if (marker == null)
                marker = CreateMarker(parent);

            EditorSceneManager.MarkSceneDirty(overlay.gameObject.scene);
            Debug.Log("[RelayRouteAuthoringUtility] Relay runtime owners authored into scene.");
            return marker != null ? "ok" : "marker_failed";
        }

        private static void EnsureRelayHudPrefabMarker()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SuitHudPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] Suit HUD prefab could not be loaded.");
                return;
            }

            try
            {
                RectTransform parent = ResolveMarkerParent(prefabRoot.transform);
                if (parent == null)
                {
                    Debug.LogError("[RelayRouteAuthoringUtility] Suit HUD prefab is missing the relay marker parent.");
                    return;
                }

                prefabRoot.TryGetComponent(out SuitHUDV4CanvasOverlay prefabOverlay);
                RelayHUDElement marker = ResolveOverlayMarker(prefabOverlay);
                if (marker != null && marker.transform.parent != parent)
                    marker.transform.SetParent(parent, false);

                if (marker == null)
                    CreateMarker(parent);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, SuitHudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string VerifyRelayHudRouteInternal()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[RelayRouteAuthoringUtility] Enter Play Mode before relay HUD verification.");
                return "not_playing";
            }

            Transform player = GameObject.Find("Player")?.transform;
            EmergencyServiceRelay relayOne = TryFindComponent<EmergencyServiceRelay>(RelayOneName);
            EmergencyServiceRelay relayTwo = TryFindComponent<EmergencyServiceRelay>(RelayTwoName);
            SuitHUDV4CanvasOverlay overlay = ResolvePreferredOverlay();
            RelayHUDElement marker = ResolveOverlayMarker(overlay);
            EmergencyServiceRelayDirector director = null;
            WorldRuntimeReferenceUtility.TryResolveEmergencyServiceRelayDirector(ref director);
            if (director == null)
                director = UObject.FindAnyObjectByType<EmergencyServiceRelayDirector>(FindObjectsInactive.Include);
            if (player == null || relayOne == null || relayTwo == null || marker == null || director == null || overlay == null)
            {
                Debug.LogError("[RelayRouteAuthoringUtility] Relay HUD verification prerequisites are missing.");
                return "missing_prerequisites";
            }

            relayOne.Interact(player);
            marker.LateFrameTick();

            marker.TryGetComponent(out CanvasGroup canvasGroup);
            EmergencyServiceRelay activeTarget = director.GetActiveRouteTarget();
            bool ok = activeTarget == relayTwo && canvasGroup != null && canvasGroup.alpha > 0.5f;
            Debug.Log(
                "[RelayRouteAuthoringUtility] Relay HUD verification => " +
                "target=" + (activeTarget != null ? activeTarget.name : "null") +
                ", markerAlpha=" + (canvasGroup != null ? canvasGroup.alpha.ToString("0.00") : "missing") +
                ", markerState=" + marker.DescribeDebugState() +
                ", ok=" + ok);
            return ok ? "ok" : "failed";
        }

        private static SuitHUDV4CanvasOverlay ResolvePreferredOverlay()
        {
            GameObject namedActiveOverlay = GameObject.Find(ActiveHudOverlayName);
            if (namedActiveOverlay != null &&
                namedActiveOverlay.TryGetComponent(out SuitHUDV4CanvasOverlay namedOverlay))
            {
                return namedOverlay;
            }

            SuitHUDV4CanvasOverlay[] overlays = Resources.FindObjectsOfTypeAll<SuitHUDV4CanvasOverlay>();
            SuitHUDV4CanvasOverlay fallback = null;

            for (int i = 0; i < overlays.Length; i++)
            {
                SuitHUDV4CanvasOverlay overlay = overlays[i];
                if (overlay == null || overlay.gameObject == null || !overlay.gameObject.scene.IsValid())
                    continue;

                fallback ??= overlay;
                if (!overlay.gameObject.activeInHierarchy)
                    continue;

                if (overlay.name.IndexOf("Projection", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                return overlay;
            }

            return fallback;
        }

        private static void RemoveMarkersFromNonPreferredOverlays(SuitHUDV4CanvasOverlay preferredOverlay)
        {
            if (preferredOverlay == null)
                return;

            SuitHUDV4CanvasOverlay[] overlays = Resources.FindObjectsOfTypeAll<SuitHUDV4CanvasOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                SuitHUDV4CanvasOverlay overlay = overlays[i];
                if (overlay == null || overlay == preferredOverlay || overlay.gameObject == null || !overlay.gameObject.scene.IsValid())
                    continue;

                RelayHUDElement[] markers = overlay.GetComponentsInChildren<RelayHUDElement>(true);
                for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    RelayHUDElement marker = markers[markerIndex];
                    if (marker == null)
                        continue;

                    Object.DestroyImmediate(marker.gameObject);
                }
            }
        }

        private static RectTransform ResolveMarkerParent(Transform overlayTransform)
        {
            if (overlayTransform == null)
                return null;

            RectTransform markerLayer = overlayTransform.Find(MarkerLayerName) as RectTransform;
            if (markerLayer != null)
                return markerLayer;

            RectTransform overlayRect = overlayTransform as RectTransform;
            if (overlayRect == null)
                return null;

            markerLayer = CreateMarkerLayer(overlayRect);

            RectTransform legacyRoot = overlayTransform.Find(HudRootName) as RectTransform;
            if (legacyRoot != null)
            {
                RelayHUDElement legacyMarker = legacyRoot.GetComponentInChildren<RelayHUDElement>(true);
                if (legacyMarker != null)
                    legacyMarker.transform.SetParent(markerLayer, false);
            }

            return markerLayer;
        }

        private static RectTransform CreateMarkerLayer(RectTransform overlayRect)
        {
            GameObject markerLayerObject = new GameObject(MarkerLayerName, typeof(RectTransform));
            markerLayerObject.transform.SetParent(overlayRect, false);

            markerLayerObject.TryGetComponent(out RectTransform markerLayer);
            markerLayer.anchorMin = Vector2.zero;
            markerLayer.anchorMax = Vector2.one;
            markerLayer.offsetMin = Vector2.zero;
            markerLayer.offsetMax = Vector2.zero;
            markerLayer.anchoredPosition = Vector2.zero;
            markerLayer.localScale = Vector3.one;
            markerLayer.SetAsLastSibling();
            return markerLayer;
        }

        private static RelayHUDElement ResolveOverlayMarker(SuitHUDV4CanvasOverlay overlay)
        {
            if (overlay == null)
                return null;

            Transform markerLayer = overlay.transform.Find(MarkerLayerName);
            if (markerLayer != null)
            {
                RelayHUDElement layeredMarker = markerLayer.GetComponentInChildren<RelayHUDElement>(true);
                if (layeredMarker != null)
                    return layeredMarker;
            }

            RelayHUDElement[] markers = overlay.GetComponentsInChildren<RelayHUDElement>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                RelayHUDElement marker = markers[i];
                if (marker == null)
                    continue;

                if (marker.gameObject.activeInHierarchy)
                    return marker;
            }

            return markers.Length > 0 ? markers[0] : null;
        }

        private static bool EnsureTargetSceneOpen()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath)
                return true;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
                return true;
            }

            return false;
        }

        private static EmergencyServiceRelay CreateOrUpdateRelay(string objectName, Vector3 position)
        {
            GameObject relayObject = GameObject.Find(objectName);
            if (relayObject == null)
            {
                relayObject = new GameObject(objectName);
                relayObject.AddComponent<SphereCollider>();
                relayObject.AddComponent<EmergencyServiceRelay>();
            }

            relayObject.transform.position = position;

            relayObject.TryGetComponent(out SphereCollider collider);
            collider.isTrigger = true;
            collider.radius = 1.8f;

            relayObject.TryGetComponent(out EmergencyServiceRelay relay);
            return relay;
        }

        private static void ConfigureRelay(
            EmergencyServiceRelay relay,
            string relayId,
            int relayOrder,
            ItemData rewardItem,
            AudioLogData audioLog,
            EmergencyServiceRelay nextRelay)
        {
            SerializedObject serializedRelay = new SerializedObject(relay);
            serializedRelay.FindProperty("relayId").stringValue = relayId;
            serializedRelay.FindProperty("chainId").stringValue = RelayChainId;
            serializedRelay.FindProperty("relayOrder").intValue = relayOrder;
            serializedRelay.FindProperty("nextRelay").objectReferenceValue = nextRelay;
            serializedRelay.FindProperty("linkedAudioLog").objectReferenceValue = audioLog;

            SerializedProperty rewards = serializedRelay.FindProperty("rewards");
            rewards.arraySize = 1;
            SerializedProperty rewardEntry = rewards.GetArrayElementAtIndex(0);
            rewardEntry.FindPropertyRelative("item").objectReferenceValue = rewardItem;
            rewardEntry.FindPropertyRelative("quantity").intValue = 1;

            serializedRelay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(relay);
        }

        private static RelayHUDElement CreateMarker(RectTransform parent)
        {
            GameObject markerRoot = new GameObject(
                MarkerRootName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(RelayHUDElement));
            markerRoot.transform.SetParent(parent, false);

            markerRoot.TryGetComponent(out RectTransform rootRect);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(260f, 72f);

            markerRoot.TryGetComponent(out CanvasGroup canvasGroup);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            markerRoot.TryGetComponent(out Image background);
            background.color = new Color(0.02f, 0.08f, 0.12f, 0.18f);
            background.raycastTarget = false;

            Image markerIcon = CreateIcon(markerRoot.transform);
            TMP_Text labelText = CreateText(markerRoot.transform, "Label", new Vector2(200f, 28f), new Vector2(16f, 12f), 20f, new Color(0.72f, 0.92f, 1f, 0.96f));
            TMP_Text distanceText = CreateText(markerRoot.transform, "Distance", new Vector2(160f, 24f), new Vector2(16f, -14f), 16f, new Color(0.52f, 0.82f, 0.96f, 0.9f));

            labelText.text = "EMERGENCY SERVICE RELAY";
            distanceText.text = "0M";

            markerRoot.TryGetComponent(out RelayHUDElement marker);
            marker.ConfigureRuntimeBindings(markerIcon, distanceText, labelText);
            return marker;
        }

        private static Image CreateIcon(Transform parent)
        {
            GameObject iconObject = CreateChild(parent, "MarkerIcon", new Vector2(18f, 18f), new Vector2(-96f, 0f));
            iconObject.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image image = iconObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.86f, 1f, 0.95f);
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 anchoredPosition,
            float fontSize,
            Color color)
        {
            GameObject textObject = CreateChild(parent, name, size, anchoredPosition);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = ResolveDefaultFont(parent);
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            return text;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);

            child.TryGetComponent(out RectTransform rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return child;
        }

        private static TMP_FontAsset ResolveDefaultFont(Transform parent)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font != null)
                return font;

            TextMeshProUGUI existingText = parent.GetComponentInChildren<TextMeshProUGUI>(true);
            return existingText != null ? existingText.font : null;
        }

        private static T TryFindComponent<T>(string objectName)
            where T : Component
        {
            GameObject found = GameObject.Find(objectName);
            if (found == null)
                return null;

            found.TryGetComponent(out T component);
            return component;
        }
    }
}
#endif

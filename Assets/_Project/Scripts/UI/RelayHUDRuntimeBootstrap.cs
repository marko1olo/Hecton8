using TMPro;
using Hecton8.Bootstrap;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Runtime fail-safe that injects the relay HUD marker into the active suit HUD overlay when authoring is missing.
    /// </summary>
    internal static class RelayHUDRuntimeBootstrap
    {
        private const string ActiveHudOverlayName = "Suit_HUD_Canvas";
        private const string MarkerRootName = "RelayRouteMarker";
        private const string HudRootName = "HUD_V4_CanvasRoot";
        private const string MarkerLayerName = "HUD_RouteMarkerLayer";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRelayHudMarker()
        {
            if (!Application.isPlaying)
                return;

            SuitHUDV4CanvasOverlay overlay = ResolvePreferredOverlay();
            if (overlay == null)
                return;

            RectTransform parent = ResolveMarkerParent(overlay.transform);
            RelayHUDElement marker = ResolveOverlayMarker(overlay);
            if (marker != null && marker.transform.parent != parent)
                marker.transform.SetParent(parent, false);

            if (marker != null)
                return;

            if (parent == null)
                return;

            CreateMarker(parent);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[RelayHUDRuntimeBootstrap] Spawned RelayRouteMarker at runtime because the active HUD had none. This is a fail-safe, not a substitute for authored HUD setup.");
#endif
        }

        private static SuitHUDV4CanvasOverlay ResolvePreferredOverlay()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null)
                return overlay;

            if (Hecton8.Bootstrap.GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                Transform overlayTransform = playerTransform.root.Find(ActiveHudOverlayName);
                if (overlayTransform != null &&
                    overlayTransform.TryGetComponent(out SuitHUDV4CanvasOverlay namedOverlay))
                {
                    return namedOverlay;
                }
            }

            return null;
        }

        private static RelayHUDElement ResolveOverlayMarker(SuitHUDV4CanvasOverlay overlay)
        {
            if (overlay == null)
                return null;

            Transform markerLayer = overlay.transform.Find(MarkerLayerName);
            if (markerLayer != null)
            {
                RelayHUDElement layeredMarker;
                if (!markerLayer.TryGetComponent(out layeredMarker))
                    TryResolveDescendantComponent(markerLayer, out layeredMarker);

                if (layeredMarker != null)
                    return layeredMarker;
            }

            RelayHUDElement fallbackMarker;
            return TryResolveDescendantComponent(overlay.transform, out fallbackMarker)
                ? fallbackMarker
                : null;
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
                RelayHUDElement legacyMarker;
                TryResolveDescendantComponent(legacyRoot, out legacyMarker);
                if (legacyMarker != null)
                    legacyMarker.transform.SetParent(markerLayer, false);
            }

            return markerLayer;
        }

        private static RectTransform CreateMarkerLayer(RectTransform overlayRect)
        {
            // COLD ALLOC: GameObject[1] - dedicated relay HUD marker layer outside the overlay-owned layout root - owner: RelayHUDRuntimeBootstrap
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

        private static void CreateMarker(RectTransform parent)
        {
            // COLD ALLOC: GameObject[4] - runtime relay HUD fail-safe hierarchy - owner: RelayHUDRuntimeBootstrap
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

            TmpTextNoAlloc.Set(labelText, "EMERGENCY SERVICE RELAY");
            TmpTextNoAlloc.Set(distanceText, "0M");

            markerRoot.TryGetComponent(out RelayHUDElement marker);
            marker.ConfigureRuntimeBindings(markerIcon, distanceText, labelText);
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

            TextMeshProUGUI existingText;
            TryResolveDescendantComponent(parent, out existingText);
            return existingText != null ? existingText.font : null;
        }

        private static bool TryResolveDescendantComponent<T>(Transform root, out T component) where T : Component
        {
            component = null;
            if (root == null)
                return false;

            if (root.TryGetComponent(out component))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryResolveDescendantComponent(root.GetChild(i), out component))
                    return true;
            }

            return false;
        }
    }
}

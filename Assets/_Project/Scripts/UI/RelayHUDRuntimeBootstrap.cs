using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Runtime relay HUD authoring validator. Missing marker structure fails closed instead of mutating the Canvas hierarchy.
    /// </summary>
    internal static class RelayHUDRuntimeBootstrap
    {
        private const string ActiveHudOverlayName = "Suit_HUD_Canvas";
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
            {
                if (parent == null)
                    return;

                marker.transform.SetParent(parent, false);
            }

            if (marker != null)
                return;

            if (parent == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[RelayHUDRuntimeBootstrap] RelayRouteMarker is missing from the authored HUD overlay. Runtime marker fabrication is disabled.");
#endif
        }

        private static SuitHUDV4CanvasOverlay ResolvePreferredOverlay()
        {
            SuitHUDV4CanvasOverlay overlay = null;
            SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref overlay);
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

            RectTransform legacyRoot = overlayTransform.Find(HudRootName) as RectTransform;
            if (legacyRoot != null)
                return legacyRoot;

            return overlayTransform as RectTransform;
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

using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal static class DiegeticMenuCanvasUtility
    {
        internal const int ReferenceWidth = 1920;
        internal const int ReferenceHeight = 1080;

        private const float DefaultPanelDistanceMeters = 1.55f;
        private const float DefaultPanelVerticalOffsetMeters = -0.03f;
        private const float WorldScaleMetersPerPixel = 0.00105f;
        private const float ColliderDepthMeters = 0.05f;
        private const string WorldGeometrySortingLayer = "WorldGeometry";

        private static readonly Quaternion s_canvasFacesCameraRotation = Quaternion.identity;
        private static readonly List<TMP_Text> s_readableTextScratch = new List<TMP_Text>(128);

        internal static bool ApplyWorldSpaceCanvas(
            Canvas canvas,
            Camera camera,
            out RectTransform root,
            out BoxCollider panelCollider)
        {
            root = null;
            panelCollider = null;
            if (canvas == null || camera == null || !camera.isActiveAndEnabled)
                return false;

            root = canvas.transform as RectTransform;
            if (root == null)
                return false;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.pixelPerfect = false;
            canvas.overrideSorting = false;
            canvas.sortingLayerName = WorldGeometrySortingLayer;
            canvas.sortingOrder = 0;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            if (canvas.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
                scaler.dynamicPixelsPerUnit = 1f;
            }

            if (canvas.TryGetComponent(out GraphicRaycaster raycaster))
                raycaster.enabled = false;

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            root.localScale = new Vector3(WorldScaleMetersPerPixel, WorldScaleMetersPerPixel, WorldScaleMetersPerPixel);

            SyncCameraRelativePose(root, camera);
            NormalizeReadableText(root);

            panelCollider = ResolveOrCreatePanelCollider(root);
            return panelCollider != null;
        }

        internal static Camera ResolveCamera(Camera preferred)
        {
            if (preferred != null && preferred.isActiveAndEnabled)
                return preferred;

            return null;
        }

        internal static void SyncCameraRelativePose(RectTransform root, Camera camera)
        {
            if (root == null || camera == null)
                return;

            Transform cameraTransform = camera.transform;
            Vector3 position =
                cameraTransform.position +
                (cameraTransform.forward * DefaultPanelDistanceMeters) +
                (cameraTransform.up * DefaultPanelVerticalOffsetMeters);

            root.SetPositionAndRotation(position, cameraTransform.rotation * s_canvasFacesCameraRotation);
        }

        internal static void NormalizeReadableText(RectTransform root)
        {
            if (root == null)
                return;

            s_readableTextScratch.Clear();
            root.GetComponentsInChildren(true, s_readableTextScratch); // COLD SCAN: main-menu setup only, never per-frame.

            for (int i = 0; i < s_readableTextScratch.Count; i++)
            {
                TMP_Text text = s_readableTextScratch[i];
                if (text == null)
                    continue;

                Transform textTransform = text.transform;
                Vector3 scale = textTransform.localScale;
                float x = scale.x;
                if (!math.isfinite(x) || math.abs(x) < 0.0001f)
                    x = 1f;

                textTransform.localScale = new Vector3(math.abs(x), scale.y, scale.z);
            }

            s_readableTextScratch.Clear();
        }

        private static BoxCollider ResolveOrCreatePanelCollider(RectTransform root)
        {
            if (!root.TryGetComponent(out BoxCollider panelCollider))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                panelCollider = root.gameObject.AddComponent<BoxCollider>(); // COLD ALLOC: main-menu diegetic panel collider.
            }

            panelCollider.isTrigger = true;
            panelCollider.center = Vector3.zero;
            panelCollider.size = new Vector3(
                math.max(1f, root.rect.width),
                math.max(1f, root.rect.height),
                ColliderDepthMeters);
            return panelCollider;
        }
    }
}

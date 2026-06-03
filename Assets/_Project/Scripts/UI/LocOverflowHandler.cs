using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Overflow scaler for localized labels when TMP auto-sizing reaches its floor.
    /// Keeps RectTransform scale untouched; residual clamp is applied to TMP mesh vertices.
    /// </summary>
    internal static class LocOverflowHandler
    {
        private const float MinUniformScale = 0.8f;
        private const float MaxUniformScale = 1f;

        public static float ApplyScale(TMP_Text text, float previousUniformScale, float uniformScale)
        {
            if (text == null)
                return MaxUniformScale;

            float clampedScale = Mathf.Clamp(uniformScale, MinUniformScale, MaxUniformScale);
            float previousScale = Mathf.Clamp(previousUniformScale, MinUniformScale, MaxUniformScale);
            bool wasScaled = !Mathf.Approximately(previousScale, MaxUniformScale);
            bool shouldScale = !Mathf.Approximately(clampedScale, MaxUniformScale);
            if (!wasScaled && !shouldScale)
                return MaxUniformScale;

            if (!TryRefreshMeshInfo(text, out TMP_TextInfo textInfo))
                return wasScaled ? previousScale : MaxUniformScale;

            float targetScale = shouldScale ? clampedScale : MaxUniformScale;
            float scaleRatio = targetScale / (wasScaled ? previousScale : MaxUniformScale);
            if (Mathf.Approximately(scaleRatio, MaxUniformScale))
                return targetScale;

            if (!HasWritableVertexPayload(textInfo))
            {
                text.SetVerticesDirty();
                return wasScaled ? previousScale : MaxUniformScale;
            }

            Bounds bounds = text.bounds;
            Vector3 pivot = bounds.center;
            Vector3 scale = new Vector3(scaleRatio, scaleRatio, 1f);
            Matrix4x4 scaleMatrix = Matrix4x4.Scale(scale);
            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[meshIndex];
                Vector3[] vertices = meshInfo.vertices;
                if (vertices == null)
                    continue;

                int vertexCount = meshInfo.vertexCount;
                for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                {
                    Vector3 offset = vertices[vertexIndex] - pivot;
                    vertices[vertexIndex] = pivot + scaleMatrix.MultiplyPoint3x4(offset);
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            return targetScale;
        }

        private static bool TryRefreshMeshInfo(TMP_Text text, out TMP_TextInfo textInfo)
        {
            if (text == null)
            {
                textInfo = null;
                return false;
            }

            textInfo = text.textInfo;
            if (textInfo == null || textInfo.meshInfo == null || textInfo.meshInfo.Length == 0)
            {
                text.SetVerticesDirty();
                return false;
            }

            return true;
        }

        private static bool HasWritableVertexPayload(TMP_TextInfo textInfo)
        {
            if (textInfo == null || textInfo.meshInfo == null || textInfo.meshInfo.Length == 0)
                return false;

            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[meshIndex];
                Vector3[] vertices = meshInfo.vertices;
                if (vertices == null)
                    continue;

                int requiredVertexCount = meshInfo.vertexCount;
                if (meshInfo.mesh != null)
                    requiredVertexCount = Mathf.Max(requiredVertexCount, meshInfo.mesh.vertexCount);

                if (requiredVertexCount > vertices.Length)
                    return false;
            }

            return true;
        }

        public static float ResolveUniformScale(TMP_Text text)
        {
            if (text == null || text.rectTransform == null)
                return MaxUniformScale;

            Rect rect = text.rectTransform.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return MaxUniformScale;

            float preferredWidth = text.preferredWidth;
            float preferredHeight = text.preferredHeight;
            if (preferredWidth <= rect.width && preferredHeight <= rect.height)
                return MaxUniformScale;

            float scaleX = preferredWidth > 0.001f ? rect.width / preferredWidth : MaxUniformScale;
            float scaleY = preferredHeight > 0.001f ? rect.height / preferredHeight : MaxUniformScale;
            return Mathf.Clamp(Mathf.Min(scaleX, scaleY), MinUniformScale, MaxUniformScale);
        }
    }
}

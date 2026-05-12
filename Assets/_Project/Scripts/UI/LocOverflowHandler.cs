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

        public static void ApplyScale(TMP_Text text, Vector3 baselineScale, float uniformScale)
        {
            if (text == null)
                return;

            float clampedScale = Mathf.Clamp(uniformScale, MinUniformScale, MaxUniformScale);
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            if (Mathf.Approximately(clampedScale, MaxUniformScale))
            {
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                return;
            }

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null)
                return;

            Bounds bounds = text.bounds;
            Vector3 pivot = bounds.center;
            Vector3 scale = new Vector3(clampedScale, clampedScale, 1f);
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

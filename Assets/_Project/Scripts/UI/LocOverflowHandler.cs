using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Overflow scaler for localized labels when TMP auto-sizing reaches its floor.
    /// Unity does not expose a writable RectTransform localMatrix, so the matrix-derived
    /// XY scale is applied back through localScale on the text rect.
    /// </summary>
    internal static class LocOverflowHandler
    {
        private const float MinUniformScale = 0.8f;
        private const float MaxUniformScale = 1f;

        public static void ApplyScale(TMP_Text text, Vector3 baselineScale, float uniformScale)
        {
            if (text == null || text.rectTransform == null)
                return;

            RectTransform rect = text.rectTransform;
            float clampedScale = Mathf.Clamp(uniformScale, MinUniformScale, MaxUniformScale);
            Matrix4x4 scaleMatrix = Matrix4x4.Scale(new Vector3(clampedScale, clampedScale, 1f));
            Vector3 resolvedScale = ExtractScale(scaleMatrix);
            Vector3 currentScale = rect.localScale;
            if (Mathf.Approximately(currentScale.x, resolvedScale.x) &&
                Mathf.Approximately(currentScale.y, resolvedScale.y))
            {
                return;
            }

            rect.localScale = new Vector3(
                baselineScale.x * resolvedScale.x,
                baselineScale.y * resolvedScale.y,
                baselineScale.z);
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

        private static Vector3 ExtractScale(Matrix4x4 matrix)
        {
            Vector4 column0 = matrix.GetColumn(0);
            Vector4 column1 = matrix.GetColumn(1);
            Vector4 column2 = matrix.GetColumn(2);
            return new Vector3(column0.magnitude, column1.magnitude, column2.magnitude);
        }
    }
}

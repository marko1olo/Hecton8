using TMPro;
using UnityEngine;

namespace Hecton8.Build
{
    /// <summary>
    /// Cold-path build watermark presenter. The HUD text is assigned once; no per-frame formatting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildInfoHudPresenter : MonoBehaviour
    {
        private const int WatermarkBufferLength = 32;

        [SerializeField] private BuildInfo buildInfo;
        [SerializeField] private TMP_Text label;

        // COLD ALLOC: char[32] - build watermark staging buffer - owner: BuildInfoHudPresenter
        private readonly char[] _watermarkBuffer = new char[WatermarkBufferLength];

        private void Awake()
        {
            ApplyWatermark();
        }

        private void OnValidate()
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
        }

        public void ApplyWatermark()
        {
            if (label == null)
                return;

            int count = buildInfo != null
                ? buildInfo.WriteVersionWatermark(_watermarkBuffer)
                : WriteFallbackVersion(_watermarkBuffer);

            if (count > 0)
                label.SetCharArray(_watermarkBuffer, 0, count);
        }

        private static int WriteFallbackVersion(char[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0;

            string version = Application.version;
            if (string.IsNullOrEmpty(version))
                return 0;

            int count = Mathf.Min(version.Length, buffer.Length);
            for (int i = 0; i < count; i++)
                buffer[i] = version[i];

            return count;
        }
    }
}

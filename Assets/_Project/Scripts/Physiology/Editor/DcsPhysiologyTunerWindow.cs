#if UNITY_EDITOR
using UnityEditor;

namespace Hecton8.Physiology.Editor
{
    /// <summary>
    /// Legacy menu shim. The active DCS editor facade is HaldaneanDecompressionTunerWindow.
    /// </summary>
    public sealed class DcsPhysiologyTunerWindow : EditorWindow
    {
        [MenuItem("Hecton/Physiology/DCS Physiology Tuner")]
        public static void Open()
        {
            HaldaneanDecompressionTunerWindow.Open();
        }

        private void CreateGUI()
        {
            HaldaneanDecompressionTunerWindow.Open();
            Close();
        }
    }
}
#endif

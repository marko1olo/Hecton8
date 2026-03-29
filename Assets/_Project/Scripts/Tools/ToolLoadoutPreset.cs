using UnityEngine;

namespace Hecton8.Tools
{
    [CreateAssetMenu(
        fileName = "ToolLoadoutPreset",
        menuName = "Hecton8/Tools/Tool Loadout Preset")]
    public sealed class ToolLoadoutPreset : ScriptableObject
    {
        [Header("Identity")]
        public string presetName = "EXPEDITION";

        [TextArea(2, 4)]
        public string description =
            "Default expedition tool mix.";

        [Header("Quick Slots 1-4")]
        public GameObject[] slotPrefabs = new GameObject[4];
    }
}

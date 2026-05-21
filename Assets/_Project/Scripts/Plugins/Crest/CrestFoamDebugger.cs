using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Placeholder for plugin-owned Crest foam forensics.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class CrestFoamDebugger : MonoBehaviour
    {
        [Header("Forensics")]
        [SerializeField]
        [Tooltip("If enabled, reports that Crest foam probing must run from the plugin ACL.")]
        private bool forceFoamFadeRate = true;

        [SerializeField, UnityEngine.RangeAttribute(0f, 20f)]
        [Tooltip("Retained for serialized compatibility with prior forensic scenes.")]
        private float forcedFoamFadeRate = 20f;

        private void Awake()
        {
            if (!forceFoamFadeRate && forcedFoamFadeRate <= 0f)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CrestFoamDebugger] Disabled in Core. Crest probes must live in Hecton8.Plugins.");
#endif
        }
    }
}

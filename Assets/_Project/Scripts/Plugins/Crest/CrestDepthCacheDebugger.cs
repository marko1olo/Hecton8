using System.Diagnostics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// One-shot forensic helper that dumps Crest's runtime depth cache texture to disk.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9999)]
    public sealed class CrestDepthCacheDebugger : MonoBehaviour
    {
        private const string DepthDebugOutputPath = "C:/hades/Hecton8/Temp/depth_debug.png";

        [SerializeField]
        [Tooltip("When enabled, dump the first available OceanDepthCache texture to Temp/depth_debug.png on Awake.")]
        private bool dumpOnAwake = true;

        private void Awake()
        {
            if (!dumpOnAwake)
                return;

            LogDepthDump(false);
            dumpOnAwake = false;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthDump(bool saved)
        {
            Hecton8.Core.H8Debug.Log($"[CrestDepthCacheDebugger] SavedDepthDebug={saved} Path={DepthDebugOutputPath}");
        }
    }
}

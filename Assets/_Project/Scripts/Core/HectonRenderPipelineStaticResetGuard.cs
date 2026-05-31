using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core
{
    /// <summary>
    /// Clears SRP package statics before Unity constructs a fresh URP instance.
    /// </summary>
    internal static class HectonRenderPipelineStaticResetGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            CleanupBlitterCold();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void ResetForEditorDomainReload()
        {
            CleanupBlitterCold();
        }
#endif

        private static void CleanupBlitterCold()
        {
            Blitter.Cleanup();
        }
    }
}

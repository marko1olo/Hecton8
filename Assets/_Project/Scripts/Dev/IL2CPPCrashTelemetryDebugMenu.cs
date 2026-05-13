using Hecton8.Core;
using UnityEngine;
using UnityEngine.Diagnostics;

namespace Hecton8.Dev
{
    /// <summary>
    /// Development-only bridge for forcing native crashes after crash telemetry is armed.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/IL2CPP Crash Telemetry Debug Menu")]
    public sealed class IL2CPPCrashTelemetryDebugMenu : MonoBehaviour
    {
        [Header("Crash Test")]
        [SerializeField, Tooltip("Crash category passed to UnityEngine.Diagnostics.Utils.ForceCrash in Editor or Development builds.")]
        private ForcedCrashCategory crashCategory = ForcedCrashCategory.FatalError;

        /// <summary>
        /// Returns true only when the crash hook is compiled into the current player.
        /// </summary>
        public bool CanForceCrash
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Context-menu entry for an explicit crash telemetry survival test.
        /// </summary>
        [ContextMenu("Force IL2CPP Crash Telemetry Test")]
        public void ForceCrashTelemetryTest()
        {
            TryForceCrashTelemetryTest(crashCategory);
        }

        /// <summary>
        /// Arms crash telemetry and forces a native crash in Editor or Development builds.
        /// </summary>
        /// <param name="category">Unity forced crash category.</param>
        /// <returns>False in release players where the hook is compiled inert.</returns>
        public static bool TryForceCrashTelemetryTest(ForcedCrashCategory category = ForcedCrashCategory.FatalError)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CrashTelemetryBuffer.EnsureRuntimeInstance();
            Utils.ForceCrash(category);
            return true;
#else
            return false;
#endif
        }
    }
}

using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Disabled legacy bridge for mod localization files.
    /// Text-facing mods must use a future binary/hash seam instead of JSON language injection in the player.
    /// </summary>
    internal static class ModLocalizationBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        /// <summary>
        /// Rejects legacy localization files discovered for a mod directory.
        /// </summary>
        internal static void RegisterLocalizationFiles(string modId, string[] filePaths)
        {
            _ = modId;
            _ = filePaths;
        }

        /// <summary>
        /// Legacy JSON localization injection is disabled in the player.
        /// </summary>
        internal static void FlushPendingInjections()
        {
        }

    }
}

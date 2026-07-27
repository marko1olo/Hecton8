using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Bootstrap
{
    internal static class ProductionPlayerAuthorityUtility
    {
        internal static bool IsProductionPlayerAuthorityObject(GameObject playerObject)
        {
            return BootstrapState.IsProductionPlayerAuthorityObject(playerObject);
        }

        /// <summary>
        /// Names the first authority condition <paramref name="playerObject"/> fails, or "NONE".
        /// Companion to the boolean above, for callers that must report WHY they rejected a player
        /// rather than only that they did.
        /// </summary>
        /// <param name="playerObject">Candidate player authority object.</param>
        /// <returns>A stable reason token from <see cref="BootstrapState"/>.</returns>
        internal static string DescribeProductionPlayerAuthorityFailure(GameObject playerObject)
        {
            return BootstrapState.DescribeProductionPlayerAuthorityFailure(playerObject);
        }
    }
}

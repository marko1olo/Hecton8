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
    }
}

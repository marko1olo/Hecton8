using Hecton8.Building;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Construction
{
    internal static class ConstructionRuntimeProxyFactory
    {
        private static bool s_proxyFabricationFaultLogged;

        internal static bool TryCreatePlacedProxy(BuildableData data, Vector3 position, Quaternion rotation, out GameObject proxyRoot)
        {
            proxyRoot = null;
            if (s_proxyFabricationFaultLogged)
                return false;

            s_proxyFabricationFaultLogged = true;
            H8Debug.LogError("[ConstructionRuntimeProxyFactory] Missing finalPrefab. Runtime proxy mesh fabrication is forbidden; bake the module with ModuleArchitect1712.");
            return false;
        }
    }
}

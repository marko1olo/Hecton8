using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local prefab resolver bridge for scatter backend reconciliation.
        /// Kept outside the main backend integration file to reduce merge pressure.
        /// </summary>
        private sealed class ScatterBackendPrefabResolver : IScatterPrefabResolver
        {
            private readonly WorldProceduralScatterDirector _owner;

            public ScatterBackendPrefabResolver(WorldProceduralScatterDirector owner)
            {
                _owner = owner;
            }

            public bool TryResolvePrefab(int familyIndex, int layerIndex, out GameObject prefab)
            {
                if (_owner == null)
                {
                    prefab = null;
                    return false;
                }

                return _owner.TryResolveScatterBackendPrefab(familyIndex, layerIndex, out prefab);
            }
        }
    }
}

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local support bundle for scatter backend orchestration helpers.
        /// Keeps resolver, binding bridge, and request factory under one cold-path owner contract.
        /// </summary>
        private sealed class ScatterBackendSupportContext
        {
            public ScatterBackendSupportContext(WorldProceduralScatterDirector owner)
            {
                // COLD ALLOC: ScatterBackendPrefabResolver[1] - scatter backend prefab lookup adapter - owner: ScatterBackendSupportContext
                PrefabResolver = new ScatterBackendPrefabResolver(owner);
                // COLD ALLOC: ScatterBackendBindingBridge[1] - scatter backend binding orchestration bridge - owner: ScatterBackendSupportContext
                BindingBridge = new ScatterBackendBindingBridge(owner);
                // COLD ALLOC: ScatterBackendRequestFactory[1] - scatter backend request shaping - owner: ScatterBackendSupportContext
                RequestFactory = new ScatterBackendRequestFactory(owner);
            }

            public ScatterBackendPrefabResolver PrefabResolver { get; }
            public ScatterBackendBindingBridge BindingBridge { get; }
            public ScatterBackendRequestFactory RequestFactory { get; }
        }
    }
}

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local support bundle for scatter backend orchestration helpers.
        /// Keeps binding bridge and request factory under one cold-path owner contract.
        /// </summary>
        private sealed class ScatterBackendSupportContext
        {
            public ScatterBackendSupportContext(WorldProceduralScatterDirector owner)
            {
                // COLD ALLOC: ScatterBackendBindingBridge[1] - scatter backend binding orchestration bridge - owner: ScatterBackendSupportContext
                BindingBridge = new ScatterBackendBindingBridge();
                // COLD ALLOC: ScatterBackendRequestFactory[1] - scatter backend request shaping - owner: ScatterBackendSupportContext
                RequestFactory = new ScatterBackendRequestFactory(owner);
            }

            public ScatterBackendBindingBridge BindingBridge { get; }
            public ScatterBackendRequestFactory RequestFactory { get; }
        }
    }
}

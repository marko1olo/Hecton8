using Hecton8.Core.Contracts;

namespace Hecton8.Thermodynamics
{
    public readonly struct ThermodynamicsAssemblyMarker
    {
        public readonly CoreContractsAssemblyMarker Contracts;

        public ThermodynamicsAssemblyMarker(CoreContractsAssemblyMarker contracts)
        {
            Contracts = contracts;
        }
    }
}

namespace Hecton8.Power.Generators.Contracts
{
    /// <summary>
    /// Read-only RTG output contract for logistics, UI, and diagnostics consumers.
    /// </summary>
    public interface IRtgDecayOutputReader
    {
        int ActiveRtgCount { get; }
        float AverageRtgHealth01 { get; }
        bool TryGetRtgCurrentOutput(uint sourceId, out float watts, out float normalized01);
    }

    /// <summary>
    /// Crafting-facing contract for extracting depleted isotopes from dead RTGs.
    /// </summary>
    public interface IRadioisotopeThermalReprocessable
    {
        bool IsDeadRtg { get; }
        uint DepletedIsotopeHash { get; }
        bool TryMarkReprocessed();
    }
}

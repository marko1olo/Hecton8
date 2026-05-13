namespace Hecton8.UI.Diegetic.Contracts
{
    /// <summary>
    /// Read-only status surface for diegetic inventory diagnostics and tests.
    /// </summary>
    public interface IDiegeticInventoryHologramReadModel
    {
        bool IsOpen { get; }
        int VisibleSlotCount { get; }
        int HoveredSlotIndex { get; }
        uint SourceInventoryRevision { get; }
        bool LowTierFlatProjection { get; }
    }
}

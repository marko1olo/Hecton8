namespace Hecton8.UI.Diegetic.Contracts
{
    public interface IDiegeticDamageHologramReadModel
    {
        int HoloDamagePoints { get; }
        int HoloProxyVertexCount { get; }
        float HologramFlood01 { get; }
        byte HologramFlags { get; }
    }
}

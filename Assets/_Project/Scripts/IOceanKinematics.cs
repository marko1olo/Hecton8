namespace Hecton8.Physics
{
    /// <summary>
    /// Canonical ocean kinematics anti-corruption interface.
    /// Gameplay uses this name; legacy runtime services keep <see cref="IHectonOceanKinematics"/> compatibility.
    /// </summary>
    public interface IOceanKinematics : IHectonOceanKinematics
    {
    }
}

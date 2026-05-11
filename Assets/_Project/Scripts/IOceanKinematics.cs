namespace Hecton8.Physics
{
    using Unity.Mathematics;

    /// <summary>
    /// Canonical ocean kinematics anti-corruption interface.
    /// Gameplay uses this name; legacy runtime services keep <see cref="IHectonOceanKinematics"/> compatibility.
    /// </summary>
    public interface IOceanKinematics : IHectonOceanKinematics
    {
        float3 GetFlowAt(float3 position);

        float GetWaveHeight(float3 position);
    }
}

// ============================================================================
//  CurrentManager.cs - deterministic triangle-wave current field.
//  Static, allocation-free, and Burst-compatible.
// ============================================================================
using Unity.Mathematics;

public static class CurrentManager
{
    // Offsets decorrelate axis channels without noise calls.
    private const float OFFSET_A = 31.71f;
    private const float OFFSET_B = 67.30f;
    private const float OFFSET_C = 149.20f;
    private const float OFFSET_D = 211.50f;

    private static float FastTriangleSigned(float phase)
    {
        float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
        return triangle01 * 2f - 1f;
    }

    /// <summary>
    /// Full 3D current vector at a world-space point.
    /// The vertical channel is scaled by verticalFactor.
    /// </summary>
    public static float3 SampleCurrent(
        float3 worldPos,
        float  time,
        float  noiseScale,
        float  timeScale,
        float  strength,
        float  verticalFactor)
    {
        float t  = time * timeScale;
        float sx = worldPos.x * noiseScale;
        float sz = worldPos.z * noiseScale;

        // Two deterministic triangle channels: no noise, no trig.
        float nx = FastTriangleSigned(sx * 2.41f + sz * 0.73f + t + OFFSET_A);
        float nz = FastTriangleSigned(sz * 2.17f - sx * 0.61f + t * 1.23f + OFFSET_C);

        // Vertical channel: slower and weaker.
        float ny = FastTriangleSigned(sx * 0.43f + sz * 0.29f + t * 0.5f + OFFSET_D) * verticalFactor;

        return new float3(nx, ny, nz) * strength;
    }

    /// <summary>
    /// Horizontal-only current, with Y fixed to zero.
    /// </summary>
    public static float3 SampleHorizontal(
        float3 worldPos,
        float  time,
        float  noiseScale,
        float  timeScale,
        float  strength)
    {
        float t  = time * timeScale;
        float sx = worldPos.x * noiseScale;
        float sz = worldPos.z * noiseScale;

        float nx = FastTriangleSigned(sx * 2.41f + sz * 0.73f + t + OFFSET_A);
        float nz = FastTriangleSigned(sz * 2.17f - sx * 0.61f + t * 1.23f + OFFSET_C);

        return new float3(nx, 0f, nz) * strength;
    }
}

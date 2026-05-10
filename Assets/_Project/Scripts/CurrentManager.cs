// ============================================================================
//  CurrentManager.cs — Generator vektornogo polya techeniy (triangle fake).
//  Vse metody static, bez allokatsiy, Burst-sovmestimy.
// ============================================================================
using Unity.Mathematics;

public static class CurrentManager
{
    // Smescheniya raznosyat pattern-kanaly, isklyuchaya korrelyatsiyu mezhdu osyami.
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
    /// Polnyy 3D-vektor techeniya v mirovoy tochke.
    /// Vertikalnaya sostavlyayuschaya oslablena verticalFactor.
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

        // Dva deterministicheskih triangle-kanala: bez dorogih funktsiy.
        float nx = FastTriangleSigned(sx * 2.41f + sz * 0.73f + t + OFFSET_A);
        float nz = FastTriangleSigned(sz * 2.17f - sx * 0.61f + t * 1.23f + OFFSET_C);

        // Vertikalnyy kanal — medlennee, slabee.
        float ny = FastTriangleSigned(sx * 0.43f + sz * 0.29f + t * 0.5f + OFFSET_D) * verticalFactor;

        return new float3(nx, ny, nz) * strength;
    }

    /// <summary>
    /// Tolko gorizontalnoe techenie (Y = 0). Deshevle na ~30 %.
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

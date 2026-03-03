// ============================================================================
//  CurrentManager.cs — Генератор векторного поля течений (simplex noise).
//  Все методы static, без аллокаций, Burst-совместимы.
// ============================================================================
using Unity.Mathematics;

public static class CurrentManager
{
    // Смещения разносят каналы шума, исключая корреляцию между осями.
    private const float OFFSET_A = 31.71f;
    private const float OFFSET_B = 67.30f;
    private const float OFFSET_C = 149.20f;
    private const float OFFSET_D = 211.50f;

    /// <summary>
    /// Полный 3D-вектор течения в мировой точке.
    /// Вертикальная составляющая ослаблена verticalFactor.
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

        // Два независимых горизонтальных канала
        float nx = noise.snoise(new float2(sx + t,             sz + OFFSET_A));
        float nz = noise.snoise(new float2(sz + t + OFFSET_B,  sx + OFFSET_C));

        // Вертикальный канал — медленнее, слабее
        float ny = noise.snoise(new float2(
                       sx + t * 0.7f + OFFSET_D,
                       sz + t * 0.3f))
                   * verticalFactor;

        return new float3(nx, ny, nz) * strength;
    }

    /// <summary>
    /// Только горизонтальное течение (Y = 0). Дешевле на ~30 %.
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

        float nx = noise.snoise(new float2(sx + t,             sz + OFFSET_A));
        float nz = noise.snoise(new float2(sz + t + OFFSET_B,  sx + OFFSET_C));

        return new float3(nx, 0f, nz) * strength;
    }
}
# Hecton Gas Band Bake Job Reference

Date: 2026-05-05
Status: REFERENCE

Clean-room Burst texture bake reference from exposed SpaceEngine gas giant parameter names. This is not SpaceEngine source.

If this file is moved into `Assets/`, every persistent NativeCollection must be registered with `NativeMemorySentinel` using an explicit lifetime label. As written, this is documentation, not compiled runtime code.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct H8GasBandParams
{
    public float stripeZones;
    public float stripeFluct;
    public float stripeTwist;
    public float cycloneMagn;
    public float cycloneFreq;
    public float cycloneDensity;
    public int cycloneOctaves;
    public float mainFreq;
    public int mainOctaves;
    public float coverage;
    public float3 randomize;
    public float4 colorA;
    public float4 colorB;
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
public struct H8GasBandBakeJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<Color32> Output;
    public int Width;
    public int Height;
    public H8GasBandParams Params;

    public void Execute(int index)
    {
        int x = index % Width;
        int y = index / Width;
        float2 uv = (new float2(x + 0.5f, y + 0.5f) / new float2(Width, Height));

        float4 c = SampleGasBand(uv, Params);
        Output[index] = new Color32(
            (byte)math.clamp(c.x * 255f, 0f, 255f),
            (byte)math.clamp(c.y * 255f, 0f, 255f),
            (byte)math.clamp(c.z * 255f, 0f, 255f),
            255);
    }

    static float Hash21(float2 p)
    {
        p = math.frac(p * new float2(123.34f, 456.21f));
        p += math.dot(p, p + 45.32f);
        return math.frac(p.x * p.y);
    }

    static float ValueNoise(float2 p)
    {
        float2 i = math.floor(p);
        float2 f = math.frac(p);
        float2 u = f * f * (3f - 2f * f);

        float a = Hash21(i);
        float b = Hash21(i + new float2(1f, 0f));
        float c = Hash21(i + new float2(0f, 1f));
        float d = Hash21(i + new float2(1f, 1f));

        return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
    }

    static float Fbm(float2 p, int octaves)
    {
        float value = 0f;
        float amp = 0.5f;
        float freq = 1f;
        int count = math.clamp(octaves, 1, 6);

        for (int i = 0; i < count; i++)
        {
            value += ValueNoise(p * freq) * amp;
            freq *= 2.03f;
            amp *= 0.5f;
        }

        return value;
    }

    static float4 SampleGasBand(float2 uv, H8GasBandParams p)
    {
        const float Tau = 6.28318530718f;

        float lon = uv.x;
        float lat = uv.y * 2f - 1f;

        float2 warpUv = new float2(
            lon * p.mainFreq + p.randomize.x,
            lat * 2f + p.randomize.y);

        float warp = (Fbm(warpUv, p.mainOctaves) - 0.5f) * p.stripeFluct;
        float twist = p.stripeTwist * lat * lat * math.sign(lat);
        float bandPhase = lat * p.stripeZones + lon * twist + warp;

        float bands = 0.5f + 0.5f * math.sin(Tau * bandPhase);
        bands = math.smoothstep(p.coverage * 0.35f, 1f, bands);

        float2 cycloneUv = new float2(
            lon * p.cycloneFreq + warp * p.cycloneMagn,
            lat * p.cycloneFreq);

        float cyclone = Fbm(cycloneUv + p.randomize.zz, p.cycloneOctaves);
        float cycloneMask = math.smoothstep(1f - p.cycloneDensity, 1f, cyclone);

        float mixValue = math.saturate(bands + cycloneMask * 0.35f);
        return math.lerp(p.colorA, p.colorB, mixValue);
    }
}
```

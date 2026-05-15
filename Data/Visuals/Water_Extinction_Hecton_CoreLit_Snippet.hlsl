// HECTON-8 water extinction sampling snippet for Hecton_CoreLit.hlsl.
// Data source: Data/Visuals/Water_Extinction_Matrix.bin uploaded as R16F 4096x4096.
// Axis order in raw source: depth[256], turbidity[256], wavelength[256].

TEXTURE2D(_H8WaterExtinctionMatrix);
SAMPLER(sampler_H8WaterExtinctionMatrix);

CBUFFER_START(H8WaterExtinctionCB)
float _H8SeaSurfaceY;
float _H8WaterExtinctionMaxDepthM;
float _H8WaterTurbidityMax;
float _H8WaterExtinctionStrength;
CBUFFER_END

#define H8_WATER_EXTINCTION_AXIS 256u
#define H8_WATER_EXTINCTION_AXIS_MAX 255.0
#define H8_WATER_EXTINCTION_PACK_WIDTH 4096u

half H8SampleWaterExtinction(float3 worldPos, half turbidityMultiplier, half wavelength01)
{
    float depthM = max(0.0, _H8SeaSurfaceY - worldPos.y);
    float depth01 = saturate(depthM * rcp(max(_H8WaterExtinctionMaxDepthM, 0.001)));
    float turbidity01 = saturate((float)turbidityMultiplier * rcp(max(_H8WaterTurbidityMax, 0.001)));

    uint depthIndex = (uint)(depth01 * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint turbidityIndex = (uint)(turbidity01 * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint wavelengthIndex = (uint)(saturate((float)wavelength01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);

    uint flatIndex = ((depthIndex * H8_WATER_EXTINCTION_AXIS) + turbidityIndex) * H8_WATER_EXTINCTION_AXIS + wavelengthIndex;
    uint2 texel = uint2(flatIndex & (H8_WATER_EXTINCTION_PACK_WIDTH - 1u), flatIndex >> 12);
    half extinction = LOAD_TEXTURE2D(_H8WaterExtinctionMatrix, int2(texel)).r;
    return lerp((half)1.0, extinction, (half)saturate(_H8WaterExtinctionStrength));
}

half3 H8SampleWaterExtinctionRgb(float3 worldPos, half turbidityMultiplier)
{
    // 700nm red, 530nm green, 470nm blue over a 470-700nm packed wavelength axis.
    const half greenWavelength01 = (half)((530.0 - 470.0) / (700.0 - 470.0));
    return half3(
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, (half)1.0),
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, greenWavelength01),
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, (half)0.0)
    );
}

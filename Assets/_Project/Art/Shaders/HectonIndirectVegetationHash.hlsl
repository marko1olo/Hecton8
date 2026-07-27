#ifndef HECTON_INDIRECT_VEGETATION_HASH_INCLUDED
#define HECTON_INDIRECT_VEGETATION_HASH_INCLUDED

// Per-instance hash shared by ALL FOUR indirect-vegetation passes:
//   Hecton_IndirectVegetation (ForwardLit), ...DepthOnly, ...MotionVectors, ...Shadow.
//
// Why this file exists. Hash21 feeds `instanceNoise`, which feeds ResolveFlowSynchronyPhase,
// which is a VERTEX OFFSET - it is the wind phase each plant sways on. The four passes had each
// grown their own Hash21: the lit pass used the integer hash below, DepthOnly and Shadow used a
// float `frac(value * 0.1031...)` hash, and MotionVectors used a third, 2D variant. Three
// different hashes over the same instance mean three different wind phases, so a plant, its
// shadow, its depth and its motion vectors were each swaying out of step with one another.
//
// A shadow, depth or motion-vector pass may simplify SHADING. It must never compute a different
// vertex POSITION than the lit pass. Anything on the path to a vertex offset therefore has to be
// physically shared, not copy-pasted and hoped over - these functions had already drifted once.
//
// Pure math, no globals, no texture or CBUFFER dependencies: safe to include from any stage.
// The lit pass is canonical; the other three were changed to match it, never the reverse.

uint MathHashUint3(uint3 value)
{
    value ^= value.yzx * uint3(0x9E3779B9u, 0x85EBCA6Bu, 0xC2B2AE35u);
    value = (value ^ (value >> 16)) * uint3(0x85EBCA6Bu, 0xC2B2AE35u, 0x27D4EB2Fu);
    value ^= value.zxy * uint3(0x165667B1u, 0xD3A2646Cu, 0x9E3779B9u);
    value ^= value >> 13;
    return value.x ^ value.y ^ value.z;
}

float Hash01FromUint(uint value)
{
    return (float)(value & 0x00FFFFFFu) * (1.0 / 16777215.0);
}

uint3 QuantizeHashSeed3(float3 value)
{
    int3 quantized = (int3)floor(value * 16.0);
    return (uint3)(quantized + int3(1048576, 1048576, 1048576));
}

float Hash21(float2 value)
{
    uint3 seed = QuantizeHashSeed3(float3(value, 0.0));
    return Hash01FromUint(MathHashUint3(seed ^ uint3(0xA511E9B3u, 0x63D83595u, 0xB6C4A793u)));
}

float Hash31(float3 value)
{
    return Hash01FromUint(MathHashUint3(QuantizeHashSeed3(value)));
}

#endif // HECTON_INDIRECT_VEGETATION_HASH_INCLUDED

#ifndef HECTON_MATH_LOD_INCLUDED
#define HECTON_MATH_LOD_INCLUDED

// Continuous math-LOD scaling, and the safe-normalize built on it.
//
// This is the project's "dear-lie math" idiom: a cheap result and an exact result, blended by one
// C#-driven weight, so quality scales continuously instead of switching in binary steps.
// _HectonMathLodWeight 0 = cheap (direction snapped to its dominant cardinal axis),
//                      1 = exact (true rsqrt normalise).
//
// This lives in its own file, rather than inside Hecton_CoreLit.hlsl where it started, because it
// is load-bearing for VERTEX POSITION and not just for shading. Any pass that builds geometry -
// a billboard basis, a particle basis, a deformation direction - has to snap or not snap in
// exactly the same way as the ForwardLit pass that the player actually sees. A pass that keeps
// the exact normalise while ForwardLit snaps (or the reverse) is drawing its geometry somewhere
// else, which is silent in a depth prepass and shows up as smeared motion vectors under TAA.
//
// That had already happened twice by the time this file was extracted:
//   - Hecton_IndirectVegetationMotionVectors had a local SafeNormalize3 hardcoded to the CHEAP
//     branch with no lerp at all, so vegetation billboards were axis-snapped in the motion-vector
//     pass and exact in the lit pass (fixed in 12b169290).
//   - Hecton_CarveDebrisIndirect's MotionVectors pass had a local DebrisSafeNormalize hardcoded to
//     the EXACT branch, so debris quads diverged from ForwardLit whenever the weight dropped
//     below 1 - i.e. precisely when the math-LOD system was doing its job.
// Both were copies that could not see this weight. Include this file instead of writing another.
//
// Hecton_CoreLit.hlsl includes this, so the ~20 shaders that include CoreLit keep these symbols
// with unchanged visibility. Passes that do not want the whole of CoreLit include this directly.

float _HectonMathLodMode;              // Legacy mirror of the continuous math LOD weight.
float _HectonMathLodWeight;            // 0=cheap dear-lie math, 1=exact visual overkill.

float3 HectonCoreLitDominantAxisOrDefault(float3 value, float3 fallbackValue)
{
    if (!all(isfinite(value)))
        return fallbackValue;

    float3 absValue = abs(value);
    float maxAxis = max(max(absValue.x, absValue.y), absValue.z);
    if (maxAxis <= 0.0001)
        return fallbackValue;

    float3 axisX = float3(value.x < 0.0 ? -1.0 : 1.0, 0.0, 0.0);
    float3 axisY = float3(0.0, value.y < 0.0 ? -1.0 : 1.0, 0.0);
    float3 axisZ = float3(0.0, 0.0, value.z < 0.0 ? -1.0 : 1.0);
    return absValue.x >= absValue.y && absValue.x >= absValue.z
        ? axisX
        : (absValue.y >= absValue.z ? axisY : axisZ);
}

float HectonCoreLitMathLodWeight()
{
    float weight = isfinite(_HectonMathLodWeight) ? _HectonMathLodWeight : _HectonMathLodMode;
    return saturate(weight);
}

float3 HectonCoreLitSafeNormalize(float3 value)
{
    float lenSq = dot(value, value);
    if (!isfinite(lenSq) || lenSq <= 0.0001)
        return float3(0.0, 1.0, 0.0);

    float3 cheap = HectonCoreLitDominantAxisOrDefault(value, float3(0.0, 1.0, 0.0));
    float3 exact = value * rsqrt(lenSq);
    return lerp(cheap, exact, HectonCoreLitMathLodWeight());
}

#endif // HECTON_MATH_LOD_INCLUDED

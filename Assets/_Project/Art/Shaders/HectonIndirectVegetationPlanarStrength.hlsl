#ifndef HECTON_INDIRECT_VEGETATION_PLANAR_STRENGTH_INCLUDED
#define HECTON_INDIRECT_VEGETATION_PLANAR_STRENGTH_INCLUDED

// Planar ocean-flow STRENGTH, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// This is the amplitude of the main sway - currentStrength feeds
// `currentVector * (currentStrength * 0.28 * bendMask * swayWave * healthSwayScale)` - so getting it
// wrong tilts every plant in the world by the wrong amount in three of the four passes.
//
// It was wrong in four separate ways at once. lit:
//     float2 flow = _GlobalOceanFlow.xz if non-zero else fallbackFlow;   // PLANAR, xz
//     return max(saturate(dot(flow, flow)), fallbackStrength);           // SQUARED and SATURATED
// the three:
//     return max(ApproxMagnitude3(_GlobalOceanFlow.xyz), _HectonVegetationCurrentStrength);
//
//   1. 3D (xyz) where the lit pass is PLANAR (xz). A "planar current strength" that counts the
//      vertical component is measuring something else.
//   2. an approximate LENGTH where the lit pass uses the SQUARED, SATURATED magnitude. Below 1 the
//      square is smaller, above 1 the saturate clamps - so the three overshot across the whole
//      range. Same trap as the wake trail's velocityMagnitude in 80923601a.
//   3. no fallback-vector selection at all: the three always read _GlobalOceanFlow and could never
//      fall back to the authored vector the way the lit pass does.
//   4. and the three did not even agree with each other - ApproxMagnitude3 in Shadow and DepthOnly
//      (major + mid*0.375 + minor*0.125) against FastLength3 in MotionVectors
//      (max + (sum - max)*0.375).
//
// The sampled term at the call site had the same length-versus-square problem:
// ApproxMagnitude2/FastLength2(sampledCurrentVector) where the lit pass uses
// saturate(dot(sampledCurrentVector, sampledCurrentVector)).
//
// Requires _GlobalOceanFlow to be declared first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

float ResolvePlanarOceanFlowStrength(float2 fallbackFlow, float fallbackStrength)
{
    float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001 ? _GlobalOceanFlow.xz : fallbackFlow;
    float flowStrengthSq = dot(flow, flow);
    return max(saturate(flowStrengthSq), fallbackStrength);
}

#endif // HECTON_INDIRECT_VEGETATION_PLANAR_STRENGTH_INCLUDED

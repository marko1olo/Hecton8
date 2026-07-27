#ifndef HECTON_INDIRECT_VEGETATION_WAKE_TRAIL_INCLUDED
#define HECTON_INDIRECT_VEGETATION_WAKE_TRAIL_INCLUDED

// Shallow-water wake trail, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// The three non-lit copies had drifted from the lit pass in five separate ways, four of which change
// the vertex position:
//
//   1. velocityMagnitude was a LENGTH (ApproxMagnitude2 / FastLength2) where the lit pass uses the
//      SQUARED magnitude, dot(v,v). Below 1 m/s squaring shrinks it, so the non-lit passes bent
//      roughly twice as hard as the lit pass over most of the useful velocity range.
//   2. typeScale was 0.7 / 1.0 / 0.3 against the lit pass's 0.72 / 1.05 / 0.38 - art tuning that was
//      applied to the lit pass and never propagated. Worst case (kelp) is 27%.
//   3. The whip branch for instanceType 1 was absent entirely. It multiplies flattening by up to
//      2.35, so a mid-type blade in fast water laid nearly flat in ForwardLit while its shadow,
//      depth and motion vectors stayed upright. This is the largest of the five.
//   4. The normal-relief term (baseNormalWS * 0.02) and the heightMask-weighted downward bias were
//      absent. heightMask was in scope at every call site; only the parameter was missing.
//   5. The bendMask early-out was absent. That one is numerically neutral - every term is scaled by
//      bendMask - but it costs a shallow-water RT sample per vertex for a guaranteed zero.
//
// Note on (1): the lit pass stores a squared magnitude in a variable named velocityMagnitude, which
// reads like a bug. It is not corrected here. The whip threshold (0.58) and the 0.5 weight below are
// tuned against the value the lit pass actually produces, and ForwardLit is what ships to the eye -
// "fixing" the name's arithmetic would change the authored look of every plant in moving water. The
// reference is the lit pass, so the reference is copied.
//
// Requires EvaluateShallowWaterFieldData, DecodeShallowWaterVelocity and SafeNormalize3 to be
// declared first. All three are already byte-identical across the four passes.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

float3 ResolveWakeTrailOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float heightMask, float instanceType)
{
    if (bendMask <= 0.0001)
        return float3(0.0, 0.0, 0.0);

    float4 shallowWaterData = EvaluateShallowWaterFieldData(evaluationPositionWS);
    float displacement = saturate(shallowWaterData.b);
    float2 planarVelocity = DecodeShallowWaterVelocity(shallowWaterData.rg);
    float velocityMagnitudeSq = dot(planarVelocity, planarVelocity);
    float velocityMagnitude = saturate(velocityMagnitudeSq);
    if (displacement <= 0.0001 && velocityMagnitude <= 0.0001)
        return float3(0.0, 0.0, 0.0);

    float3 wakeDirection = SafeNormalize3(float3(planarVelocity.x, 0.0, planarVelocity.y));
    float3 planarWakeDirection = wakeDirection - baseNormalWS * dot(wakeDirection, baseNormalWS);
    planarWakeDirection = SafeNormalize3(planarWakeDirection);
    float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.05 : 0.38);
    float flattening = (displacement + velocityMagnitude * 0.5) * bendMask * typeScale;
    if (instanceType > 0.5 && instanceType < 1.5)
    {
        float whipFactor = saturate((velocityMagnitude - 0.58) * 2.8 + displacement * 0.75);
        flattening *= lerp(1.0, 2.35, whipFactor);
    }
    float downwardBias = lerp(0.04, 0.18, heightMask) * flattening;
    return (planarWakeDirection + baseNormalWS * 0.02) * flattening + float3(0.0, -downwardBias, 0.0);
}

#endif // HECTON_INDIRECT_VEGETATION_WAKE_TRAIL_INCLUDED

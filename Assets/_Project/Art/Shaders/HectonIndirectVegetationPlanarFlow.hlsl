#ifndef HECTON_INDIRECT_VEGETATION_PLANAR_FLOW_INCLUDED
#define HECTON_INDIRECT_VEGETATION_PLANAR_FLOW_INCLUDED

// Planar ocean-flow direction, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// The two spellings that existed were nearly the same function:
//
//   lit:   ResolvePlanarOceanFlowDirection(float2 fallbackFlow)
//            _GlobalOceanFlow.xz if non-zero, else the CALLER'S fallback
//   three: ResolvePlanarCurrentDirection()      // no parameter
//            _GlobalOceanFlow.xz if non-zero, else _HectonVegetationCurrentVector.xz, always
//
// So they agree whenever _GlobalOceanFlow is non-zero, and diverge only when it is zero AND the
// locally sampled flow is not - because the lit pass falls back to the sampled field while the three
// ignored it and went straight to the authored vector. Narrow, but it is a different direction, and
// the parameterless form cannot express the lit behaviour at all.
//
// Keeping the lit signature is what makes the agitated-lean term expressible in the other three:
//     animatedPositionWS.xz += currentDirection * (agitatedWeight * bendMask * instanceHeight * 0.035)
// That term is unconditional in the lit pass and was absent from all three. It needs the lit
// direction, not the parameterless one, which is exactly why it was left out of c7ef2c71d rather
// than guessed.
//
// Call it with _HectonVegetationCurrentVector.xz to reproduce the old parameterless behaviour
// exactly.
//
// Requires SafeNormalize2, _GlobalOceanFlow and _HectonVegetationCurrentVector to be declared first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

float2 ResolvePlanarOceanFlowDirection(float2 fallbackFlow)
{
    float2 flow = dot(_GlobalOceanFlow.xz, _GlobalOceanFlow.xz) > 0.0001 ? _GlobalOceanFlow.xz : fallbackFlow;
    return SafeNormalize2(flow);
}

#endif // HECTON_INDIRECT_VEGETATION_PLANAR_FLOW_INCLUDED

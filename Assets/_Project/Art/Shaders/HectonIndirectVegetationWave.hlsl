#ifndef HECTON_INDIRECT_VEGETATION_WAVE_INCLUDED
#define HECTON_INDIRECT_VEGETATION_WAVE_INCLUDED

// Canonical sway wave, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// It lives here because the passes had each grown their OWN cheaper wave - TrianglePulse01 in
// Shadow and DepthOnly, FastTriangleSigned in MotionVectors - and fed it into the vertex
// displacement. A triangle wave and this sine are not interchangeable at the amplitudes involved:
// aligned on the same phase and period they part company by up to 0.10 of full amplitude (at
// theta = pi/4 the normalised sine is 0.854 where the triangle is 0.750). That is a different
// vertex POSITION, not a different shade, so the shadow detached from the blade that cast it, the
// depth prepass disagreed with the forward pass, and motion vectors reported a velocity the
// geometry never had.
//
// The standing rule this enforces: a shadow, depth or motion-vector pass may simplify SHADING,
// but must never compute a different vertex POSITION than ForwardLit.
//
// Bodies are copied verbatim from the ForwardLit pass, which is the reference. Keep them that way -
// a "harmless" cheaper variant here is exactly the defect this include exists to remove.

float WrapPhasePi(float phase)
{
    const float twoPi = 6.28318530718;
    const float invTwoPi = 0.15915494309;
    return phase - floor((phase + 3.14159265359) * invTwoPi) * twoPi;
}

float FastSinApprox(float phase)
{
    float x = WrapPhasePi(phase);
    float x2 = x * x;
    return x * (1.0 - x2 * (0.1666666716 - x2 * (0.0083333310 - x2 * 0.0001984127)));
}

float FastCosApprox(float phase)
{
    return FastSinApprox(phase + 1.57079632679);
}

#endif // HECTON_INDIRECT_VEGETATION_WAVE_INCLUDED

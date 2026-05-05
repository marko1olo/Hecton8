#ifndef HECTON_AUP_DEPTH_REFERENCE_INCLUDED
#define HECTON_AUP_DEPTH_REFERENCE_INCLUDED

// Status: REFERENCE
// Reversed-Z is preferred. Log depth is fallback for distant visual-only objects.

float H8LogDepth01(float clipW, float farPlane)
{
    float fcoef = 2.0 / log2(farPlane + 1.0);
    return log2(max(1e-6, clipW + 1.0)) * fcoef * 0.5;
}

#endif

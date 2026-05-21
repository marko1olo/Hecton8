#ifndef HECTON_ACCRETION_DISK_REFERENCE_INCLUDED
#define HECTON_ACCRETION_DISK_REFERENCE_INCLUDED

// Status: REFERENCE
// Visual-only twisted disk UV helper. Not GR gameplay simulation.

float2 H8TwistedDiskUv(float3 localPos, float twistMagnitude, float radialScale)
{
    float r = max(length(localPos.xz), 1e-3);
    float a = atan2(localPos.z, localPos.x);
    a += twistMagnitude / max(r * radialScale, 1e-3);
    return float2(a * (1.0 / 6.28318530718), r);
}

#endif

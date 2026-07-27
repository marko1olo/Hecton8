#ifndef HECTON_INDIRECT_VEGETATION_BILLBOARD_INCLUDED
#define HECTON_INDIRECT_VEGETATION_BILLBOARD_INCLUDED

// Far-LOD billboard position, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// The lit and DepthOnly copies were already byte-identical and the MotionVectors copy differed only
// in taking the camera position as a parameter instead of reading _WorldSpaceCameraPos - it needs
// the PREVIOUS frame's camera to produce a correct motion vector, which is legitimate, not drift.
// Merging them on an explicit parameter therefore loses nothing.
//
// The Shadow pass had no billboard path AT ALL - no `_HectonVegetationRuntimeLodParams.x >= 0.5`
// branch anywhere in the file. Past the far-LOD threshold the visible flora is a camera-facing quad
// while its shadow was still cast from the full detail-mesh pose, so the silhouette on the ground
// could not match the thing casting it.
//
// It could not be fixed by copying either: a billboard must face the CAMERA, and during a shadow
// pass _WorldSpaceCameraPos is the LIGHT. Orienting the quad to the light would have produced a
// silhouette with no relationship to what the player sees. The caller now supplies the position -
// ResolveVegetationViewPositionWS() for lit, depth and shadow, the historical camera for motion -
// which is what makes a shadow-pass billboard expressible at all.
//
// Requires SafeNormalize3 and _HectonVegetationRuntimeDrawParams to be declared first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

float3 ResolveBillboardPositionWS(
    float3 originWS,
    float3 localPosition,
    float instanceHeight,
    float instanceWidth,
    float heightMask,
    float3 viewPositionWS)
{
    float3 cameraDelta = viewPositionWS - originWS;
    float3 cameraForwardXZ = SafeNormalize3(float3(cameraDelta.x, 0.0, cameraDelta.z));
    float3 billboardRight = SafeNormalize3(float3(cameraForwardXZ.z, 0.0, -cameraForwardXZ.x));
    float3 billboardUp = float3(0.0, 1.0, 0.0);
    float widthAtHeight = instanceWidth * lerp(1.0, 0.42, heightMask) * max(_HectonVegetationRuntimeDrawParams.y, 0.25);
    float heightScale = instanceHeight * max(_HectonVegetationRuntimeDrawParams.z, 0.25);

    return originWS +
        billboardRight * (localPosition.x * widthAtHeight) +
        billboardUp * (heightMask * heightScale);
}

#endif // HECTON_INDIRECT_VEGETATION_BILLBOARD_INCLUDED

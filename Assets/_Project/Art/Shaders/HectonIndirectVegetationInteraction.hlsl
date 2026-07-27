#ifndef HECTON_INDIRECT_VEGETATION_INTERACTION_INCLUDED
#define HECTON_INDIRECT_VEGETATION_INTERACTION_INCLUDED

// Flora interaction bend, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// This one had drifted further than any of the others, and unlike them it had a real reason to.
//
// WHY IT COULD NOT SIMPLY BE COPIED. The lit pass culls interaction beyond
// ResolveInteractionDistance() using distance to _WorldSpaceCameraPos. During a SHADOW pass
// _WorldSpaceCameraPos is the LIGHT, not the camera, so replicating that cull from the shadow pass's
// own view would cull by distance-to-light and compute a different vertex position than ForwardLit -
// exactly the defect class this whole series is closing. Nobody could express a camera-dependent
// cull in a shadow pass with what the shader had.
//
// The fix is a view-INDEPENDENT camera position, published by HectonIndirectVegetationRenderer into
// every one of its seven property blocks from the same _cachedCullCameraPosition it already culls
// with. _HectonVegetationViewPositionWS.w is 1 when it has been written; the fallback keeps the old
// behaviour if it has not, so a pass without the renderer degrades instead of culling everything.
//
// Note the deliberate asymmetry: SHADING may be view-dependent (the lit pass keeps its own
// ResolveCameraDistanceSq for biolum dimming and pixel gating, which SHOULD follow the real view).
// VERTEX POSITION may not. Only the position path uses the global.
//
// The other five differences were plain drift, all of them changing the vertex:
//   1. falloff was saturate(1 - d2/r2) instead of 1 - smoothstep(0, r2, d2) followed by
//      FastVegetationPower01 with _InteractionDistancePower.
//   2. bend direction used planarVelocityDir alone - no radial term, no _InteractionVelocityBias
//      blend, so the plant bent along a different AXIS, not merely by a different amount.
//   3. the directionalBias term (0.65 + 0.35 * saturate(dot(-radial, velocity))) was absent.
//   4. the baseNormalWS * 0.04 relief was absent.
//   5. at the CALL SITE the three passes applied weight 1.0 where the lit pass applies
//      _InteractionPushStrength * interactionTypeScale - up to 1.55x with the authored defaults.
//
// historyDelta is a real parameter, not padding: the MotionVectors pass rewinds every interaction
// position by velocity * historyDelta so its two evaluations (at _Time.y and at previousTime) differ
// by the actual movement. The other three pass 0, which makes the term vanish, so one body serves
// all four without flattening that away.
//
// Requires SafeNormalize3, SanitizeNonNegativeFinite, SanitizePositiveFinite,
// FloraInteractionPointGpuData, _HectonFloraInteractionPoints, _HectonFloraInteractionCount,
// HECTON_MAX_INTERACTION_POINTS and _HectonVegetationRuntimeLodParams to be declared first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

// xyz: the culling camera's position in runtime space, w: 1 when written by the renderer.
float4 _HectonVegetationViewPositionWS;

float3 ResolveVegetationViewPositionWS()
{
    return _HectonVegetationViewPositionWS.w >= 0.5 ? _HectonVegetationViewPositionWS.xyz : _WorldSpaceCameraPos;
}

// For VERTEX POSITION work only. Shading that wants the real view must not use this.
float ResolveVegetationViewDistanceSq(float3 positionWS)
{
    float3 viewDelta = positionWS - ResolveVegetationViewPositionWS();
    return dot(viewDelta, viewDelta);
}

half FastVegetationPower01(half value, half exponent)
{
    half v = saturate(value);
    half v2 = v * v;
    half v4 = v2 * v2;
    half v8 = v4 * v4;
    half v16 = v8 * v8;
    half low = lerp(v, v4, saturate((exponent - 1.0h) * 0.33333333h));
    half high = lerp(v4, v16, saturate((exponent - 4.0h) * 0.08333333h));
    return lerp(low, high, step(4.0h, exponent));
}

float ResolveInteractionDistance()
{
    return max(12.0, min(_HectonVegetationRuntimeLodParams.y + _HectonVegetationRuntimeLodParams.w, 55.0));
}

// Matches the lit pass's per-type weighting of the accumulated offset.
float ResolveInteractionTypeScale(float instanceType)
{
    return instanceType < 0.5 ? 0.7 : (instanceType < 1.5 ? 1.15 : 0.85);
}

float3 ResolveInteractionOffset(
    float3 evaluationPositionWS,
    float3 baseNormalWS,
    float bendMask,
    float distanceToCameraSq,
    float historyDelta)
{
    float interactionDistance = ResolveInteractionDistance();
    if (bendMask <= 0.0001 || distanceToCameraSq > interactionDistance * interactionDistance)
        return float3(0.0, 0.0, 0.0);

    float3 interactionOffset = float3(0.0, 0.0, 0.0);
    int activeInteractionCount = min(_HectonFloraInteractionCount, HECTON_MAX_INTERACTION_POINTS);
    float rewindSeconds = SanitizeNonNegativeFinite(historyDelta);

    [loop]
    for (int i = 0; i < activeInteractionCount; i++)
    {
        FloraInteractionPointGpuData interactionPoint = _HectonFloraInteractionPoints[i];
        float3 velocity = interactionPoint.velocitySpeed.xyz;
        if (!all(isfinite(velocity)) || !all(isfinite(interactionPoint.positionRadius.xyz)))
            continue;

        float speed = SanitizeNonNegativeFinite(interactionPoint.velocitySpeed.w);
        float speedFactor = saturate(speed * 0.18);
        if (speedFactor <= 0.0001)
            continue;

        float3 delta = evaluationPositionWS - (interactionPoint.positionRadius.xyz - velocity * rewindSeconds);
        delta.y *= 0.22;

        float bendRadius = SanitizePositiveFinite(interactionPoint.positionRadius.w, 0.05);
        float bendRadiusSq = bendRadius * bendRadius;
        float distSq = dot(delta, delta);
        float proximity = 1.0 - smoothstep(0.0, bendRadiusSq, distSq);
        proximity = FastVegetationPower01((half)proximity, max(_InteractionDistancePower, 1.0h));
        if (proximity <= 0.0001)
            continue;

        float3 planarVelocityDir = velocity - baseNormalWS * dot(velocity, baseNormalWS);
        planarVelocityDir = SafeNormalize3(planarVelocityDir);
        float3 radialDirection = SafeNormalize3(float3(delta.x, 0.0, delta.z));
        float3 bendDirection = SafeNormalize3(lerp(radialDirection, planarVelocityDir, _InteractionVelocityBias));
        float directionalBias = 0.65 + 0.35 * saturate(dot(-radialDirection, planarVelocityDir));

        interactionOffset += (bendDirection + baseNormalWS * 0.04) * (proximity * speedFactor * directionalBias);
    }

    return interactionOffset * bendMask;
}

#endif // HECTON_INDIRECT_VEGETATION_INTERACTION_INCLUDED

#ifndef HECTON_INDIRECT_VEGETATION_PLAYER_BEND_INCLUDED
#define HECTON_INDIRECT_VEGETATION_PLAYER_BEND_INCLUDED

// Player bend, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// This was the worst measured case in the whole series: all FOUR passes had a different body.
// No two agreed. It is also the most player-visible displacement there is - it is the flora
// reacting to the player standing in it.
//
// The three non-lit copies shared the same two real divergences from the lit pass:
//
//   1. FALLOFF. `saturate(1 - d2/r2)` in Shadow and DepthOnly, `saturate(1 - d2 * rcp(r2))` in
//      MotionVectors, against the lit pass's `1 - smoothstep(0, r2, d2)`. Then squared, which
//      amplifies the difference. Three spellings of "not the reference".
//   2. THE LIFT TERM WAS ABSENT, in all three. lerp(0.01, 0.05, bendMask) * proximity * typeScale
//      pushes the plant DOWN as the player wades through it. So in ForwardLit the plant crouches
//      and its shadow, depth and motion vectors stayed standing - directly under the player, where
//      the eye is.
//
// The remaining difference was the position of the isfinite guard, which is behaviourally
// identical; the lit ordering is kept because the lit pass is the reference.
//
// NOT INCLUDED HERE, deliberately: the lit pass multiplies this offset by
// (1 - saturate(_HectonFloraSwayFieldParams.y)) before applying it, and _HectonFloraSwayFieldParams
// exists only in the lit shader. That suppression is only CORRECT because the motion it removes is
// replaced by ResolveFloraSwayFieldOffset, which the three non-lit passes do not compute at all.
// Adding the suppression alone would take bend away and put nothing back, making the mismatch worse
// in the other direction. It belongs with the sway-field port, not here.
//
// Requires SanitizeNonNegativeFinite, SafeNormalize3, _HectonPlayerRuntimePosition and
// _HectonPlayerFloraInteractionParams to be declared first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

float3 ResolvePlayerBendOffset(float3 evaluationPositionWS, float3 baseNormalWS, float bendMask, float instanceType)
{
    float playerRadius = SanitizeNonNegativeFinite(_HectonPlayerRuntimePosition.w);
    if (bendMask <= 0.0001 ||
        SanitizeNonNegativeFinite(_HectonPlayerFloraInteractionParams.w) < 0.5 ||
        playerRadius <= 0.0001)
    {
        return float3(0.0, 0.0, 0.0);
    }

    // _HectonPlayerRuntimePosition is already in runtime space. An earlier version of the shadow
    // copy added _GlobalFloatingOffset here to reconstruct runtime space from an AUP global that was
    // never populated, which double-counted the offset even in the hypothetical case where something
    // had set it. Do not reintroduce that term.
    float3 playerRuntimePosition = _HectonPlayerRuntimePosition.xyz;
    float playerSpeed = SanitizeNonNegativeFinite(_HectonPlayerFloraInteractionParams.x);
    float playerPush = SanitizeNonNegativeFinite(_HectonPlayerFloraInteractionParams.y);
    if (playerSpeed <= 0.0001 || playerPush <= 0.0001)
        return float3(0.0, 0.0, 0.0);

    if (!all(isfinite(playerRuntimePosition)))
        return float3(0.0, 0.0, 0.0);

    float3 delta = evaluationPositionWS - playerRuntimePosition;
    delta.y *= 0.22;
    float radiusSq = playerRadius * playerRadius;
    float distSq = dot(delta, delta);
    if (distSq >= radiusSq)
        return float3(0.0, 0.0, 0.0);

    float proximity = 1.0 - smoothstep(0.0, radiusSq, distSq);
    proximity *= proximity;
    float typeScale = instanceType < 0.5 ? 0.72 : (instanceType < 1.5 ? 1.08 : 0.52);
    float lift = lerp(0.01, 0.05, bendMask) * proximity * typeScale;
    float pushStrength = saturate(playerSpeed * 0.16) * playerPush * typeScale;
    return (SafeNormalize3(float3(delta.x, 0.0, delta.z)) + baseNormalWS * 0.04) *
        (proximity * pushStrength * bendMask) + float3(0.0, -lift, 0.0);
}

#endif // HECTON_INDIRECT_VEGETATION_PLAYER_BEND_INCLUDED

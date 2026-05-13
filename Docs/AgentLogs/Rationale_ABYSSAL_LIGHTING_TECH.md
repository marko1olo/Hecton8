# Rationale - ABYSSAL_LIGHTING_TECH

Status: PENDING VERIFICATION

## Decision 0 - Use Screen-Space Fake, Not Volumetric Truth
Problem: Full volumetric fog/light marching is explicitly too expensive for i3/MX350 and the task asks for cinematic shafts from headlights and bioluminescence.
Solution: Use the DOD visual-fake ladder: emissive mask plus depth-aware radial blur in the existing visor post path, with low-tier quarter-resolution/disable gates.
Rejected Alternatives: Full volumetric raymarch, polygon beam meshes, and third-party VolumetricLightBeam scripts. They burn fill-rate/polygon budget and duplicate the requested fake.
Scalability potential: Low disables or quarter-res 8 taps; Middle uses quarter/half-res 8 taps plus history; High uses half-res 16 taps; Ultra spends saved cycles on richer tint/flicker/source count while keeping predictable screen-space cost.
Hardware Impact: Estimated MX350 gain versus 64-step volumetric raymarch is 0.4-1.5 ms GPU saved depending on resolution. Measured proof absent; status remains PENDING VERIFICATION.

## Decision 1 - Build Around Existing Visor Post
Problem: Adding a separate post stack risks extra blits, RenderGraph debt, and VR vignette ordering bugs.
Solution: Extend HectonVisorUberPost where possible, because current renderer data already references the feature.
Rejected Alternatives: New independent renderer feature by default, URP Bloom/LensFlare stack, or material clones.
Scalability potential: Low keeps one post pass and cheaper shader branches; High/Ultra can increase taps inside the same pass without new architecture.
Hardware Impact: Avoids at least one fullscreen pass/blit on MX350 if integration stays inside current pass. Measured proof absent.

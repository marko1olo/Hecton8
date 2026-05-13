# Rationale_ACTIVE_SONAR_ILLUMINATION

Status: PENDING VERIFICATION

## Decision 0 - Presentation Fake Instead Of Sonar Post Process
Problem: Active sonar currently risks becoming a fullscreen post-process or singleton-driven presentation path, which conflicts with the prompt and RenderGraph/Blit mandates.
Solution: Drive geometry illumination through global shader state consumed by `Hecton_CoreLit.hlsl`; keep ping expansion as scalar CPU state and fixed-size GPU uniforms.
Rejected Alternatives: Fullscreen `Graphics.Blit` ring pass is explicitly forbidden and wastes fillrate on MX350; dynamic lights would cost shadows/culling and fail the fake-first shader mandate.
Scalability potential: Low uses cyan ring only; Middle adds procedural grid; High adds richer grid response; Ultra keeps four visible pings with stronger topological detail.
Hardware Impact: MX350/i3 avoids fullscreen pass and dynamic-light submission; estimated saving versus blit ring is 80-250 us GPU depending on resolution, pending profiler proof.

## Decision 1 - Fixed Ping Capacity
Problem: Multiple sonar pings must coexist without managed allocation or variable-length uploads in gameplay cadence.
Solution: Use a fixed capacity of four `Vector4` entries as requested by prompt, where xyz=center and w=radius/intensity payload depending on shader contract discovered in code.
Rejected Alternatives: `List<T>`/dynamic arrays allocate or resize risk; per-object components would create scene coupling and more renderer state.
Scalability potential: Low can shade only first ping or skip grid; Middle/High/Ultra shade all four with tier-gated detail.
Hardware Impact: Fixed four-loop unrolled shader cost is predictable; C# memory is constant and hot path remains scalar.

## Decision 2 - SignalBus Mirror For AcousticPingSignal
Problem: Active sonar geo illumination must consume `AcousticPingSignal(ActiveSonar)` without hard coupling to a VFX manager.
Solution: Add ActiveSonar channel/flag constants to `AcousticPingSignal`; publish active sonar from `SpectrumSystem`; mirror legacy `GlobalSignals.Publish(in AcousticPingSignal)` into `SignalBus<AcousticPingSignal>`.
Rejected Alternatives: Direct `SonarVfxManager.Instance` or polling audio runtime was rejected because it recreates singleton rot and breaks parallel-agent boundaries.
Scalability potential: Low/Middle/High/Ultra all use one signal path; richer tiers only change shader detail, not architecture.
Hardware Impact: NativeQueue push is deterministic and bounded; expected CPU cost below 12 us on i3-class hardware during ping frames.

## Decision 3 - Squared Spherical Ring In CoreLit
Problem: Darkness needs geometry-local illumination, not a post-process ring detached from mesh material response.
Solution: `Hecton_CoreLit.hlsl` declares `_ActiveSonarCenterAUP`, `_ActiveSonarRadius`, a fixed four-ping array, and computes `dot(delta, delta)` against `radius * radius` with the mandated ring equation.
Rejected Alternatives: `distance()`/sqrt path and dynamic lights were rejected; fullscreen blits were explicitly disallowed and waste fillrate.
Scalability potential: Low uses cyan shell only; Middle/High/Ultra add triplanar topological grid and all four pings.
Hardware Impact: Removes fullscreen sonar-ring fill cost; estimated 80-250 us GPU saved on MX350 at 1080p when legacy history would otherwise run.

## Decision 4 - Low-Tier Grid Kill Switch
Problem: Triplanar grid sells the scan but costs extra ALU on weak GPUs.
Solution: C# publishes `_ActiveSonarGeoParams.z = 0` on scalability tier byte 0, and HLSL also bypasses grid under `_MATH_LOD_LOW`.
Rejected Alternatives: Always-on grid was rejected because it spends per-pixel ALU where the plain cyan shell already communicates contact timing.
Scalability potential: Low plain shell; Middle procedural grid; High/Ultra keep grid on all four pings with stronger visual overkill from saved fullscreen cost.
Hardware Impact: MX350 avoids roughly 15-30 ALU per active sonar shaded pixel during the ring.

## Decision 5 - Blackbox Ring And Dump
Problem: Active sonar radius/center state can tear during AUP shifts or bad signal input; failure must be reconstructable.
Solution: `SpectrumSystem` owns a 300-entry `NativeArray<ActiveSonarGeoTelemetryEntry>` and dumps `Dump_ACTIVE_SONAR_ILLUMINATION.bin` on non-finite detection.
Rejected Alternatives: Managed string logs were rejected for GC and poor crash survivability.
Scalability potential: Same blackbox across all tiers; higher tiers only increase visible shader richness, not diagnostic overhead.
Hardware Impact: One fixed native write per frame, expected below 2 us; no managed allocation in steady state.

## Decision 6 - PDA Radius Contract
Problem: PDA sonar map used an independent time-based ring, so it could visually disagree with active sonar.
Solution: PDA now reads global `_ActiveSonarRadius` and `_ActiveSonarGeoParams`; its point-cloud shader receives the same radius and normalizes it by max range.
Rejected Alternatives: Maintaining the old `frac(_animationTime)` sweep during active sonar was rejected because it lies about ping position.
Scalability potential: Low still renders a cheap normalized 2D ring; higher tiers can layer richer point coloring without changing timing.
Hardware Impact: Two global reads and two material scalar sets while map renders; no heap allocation.

## Decision 7 - Compile Wall Classification
Problem: `dotnet build Hecton8.Core.csproj` fails before final verification due unrelated missing project references and types.
Solution: Fixed the one local namespace issue (`Hecton8.Core.Signals` for `AcousticPingSignal`) and classified remaining failures as dependency wall.
Rejected Alternatives: Reverting sonar work to mask unrelated project assembly gaps was rejected; changing other domains would violate the domain boundary.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; verification remains blocked until project references are restored by owning agents.

## Decision 8 - OMEGA Shader Hot Path Cut
Problem: The active sonar grid sold the topological scan but still used a procedural value-noise call in the active ring path.
Solution: Replace the active-sonar noise sample with a triangle-wave dot fake and remove redundant shader-side `round()` on `_ActiveSonarGeoParams.x`.
Rejected Alternatives: Keeping honest value noise was rejected because immersion does not require stochastic accuracy; a deterministic triangle shimmer reads as sonar scan at lower ALU cost.
Scalability potential: Low keeps grid disabled; Middle uses triangle grid; High/Ultra can spend saved cycles on four simultaneous pings and stronger cyan emission without dynamic lights.
Hardware Impact: MX350/i3 saves an estimated 6-12 ALU per active sonar shaded pixel for the noise cut and 1 ALU for count rounding; no CPU or GC cost added.

## Decision 9 - Final Verification Remains Pending
Problem: Final project verification is blocked by failures outside the active sonar domain.
Solution: Record exact blockers instead of falsifying a green state: `dotnet build` reports 104 missing dependency/type errors and `git diff --check` reports trailing whitespace in `Assets/_Project/Scripts/BoidFishInstanced.shader:520`.
Rejected Alternatives: Editing unrelated boid or global dependency files was rejected because the batch domain is VFX sonar illumination and parallel agents own those systems.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; status remains `PENDING VERIFICATION` until owning domains restore compile and diff hygiene.

## Decision 10 - High-Tier Visual Currency Spend
Problem: The OMEGA pass reduced active-sonar shader cost but did not yet spend the saved budget on premium high-tier visual response.
Solution: Publish `_ActiveSonarGeoParams.z = 2` on non-low hardware and add uniform-gated fine grid/rib detail in `HectonCoreLitEvaluateActiveSonarTriplanarGrid`.
Rejected Alternatives: Texture samples, fullscreen overlays, and dynamic lights were rejected because they add bandwidth/fill cost and violate the geometry-local contract.
Scalability potential: Low/MX350 detail 0: plain cyan shell; Middle absent in current two-tier profile; High/Ultra detail 2: coarse triplanar grid plus fine scan ribs across all four pings.
Hardware Impact: MX350/i3 keeps the existing early return and pays no added ALU. High tier pays roughly 6-10 extra ALU per active sonar shaded pixel to buy more legible AAA scan detail.

## Decision 11 - Verification Hygiene Boundary
Problem: `git diff --check` initially failed on one shader line and then exposed unrelated `.meta` whitespace owned by other agent domains.
Solution: Fix the single shader whitespace line already in the VFX/rendering surface and record the remaining `.meta` blockers without raw-editing unrelated YAML metadata.
Rejected Alternatives: Broad cleanup of GroundRadar, Inventory Corrosion, and Thermodynamics `.meta` files was rejected because it would cross domain ownership for no active-sonar runtime gain.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; touched active/VFX files now pass targeted diff hygiene.

## Decision 12 - Wider Geometry Coverage Without Post Process
Problem: Static scatter and dry-zone surfaces could remain flat because their shaders did not add active-sonar geometry emission directly.
Solution: Add `HectonCoreLitEvaluateActiveSonarGeoEmission(input.positionWS)` to `Hecton_ScatterIndirectLit.shader` and `Hecton_DryZoneLit.shader` emission composition.
Rejected Alternatives: Reintroducing a fullscreen sonar overlay was rejected by prompt and fill-rate budget; relying on material emission masks was rejected because non-emissive rocks/debris should still read under sonar.
Scalability potential: Low still exits grid detail and shows plain shell; High/Ultra show fine scan ribs across rocks, wrecks, scatter props, and module surfaces.
Hardware Impact: Adds one bounded active-ring call per affected shaded pixel during pings, but no texture samples, no dynamic lights, and no managed allocations.

## Decision 13 - Grid Cost Hoisted Out Of Ping Loop
Problem: The active sonar grid was independent of ping index but evaluated inside the up-to-four-ping loop.
Solution: Accumulate max ring response across pings first, then multiply by the grid exactly once.
Rejected Alternatives: Per-ping grid evaluation was rejected because it pays duplicate ALU with no visual difference.
Scalability potential: Low remains a plain shell; High/Ultra keep fine grid/rib detail with lower worst-case overlap cost.
Hardware Impact: Worst-case four-ping overlap saves up to three triplanar/fine-grid evaluations per active sonar shaded pixel.

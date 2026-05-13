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

## Decision 2 - Isolate Shafts, But Reference Actual Contract Owners
Problem: The prompt demanded `Hecton8.Lighting.Shafts -> Contracts`, but the live project keeps `GlobalSignals`, `SignalBus<T>`, `ILateFrameTickable`, `GlobalRegistry`, `AbsoluteUniversePosition`, and `H8Memory` outside the thin `Hecton8.Core.Contracts` asmdef.
Solution: Create `Hecton8.Lighting.Shafts` as its own assembly and reference `Hecton8.Core` plus `Hecton8.Core.Memory` because those are the current owners of the required zero-GC signal, registry, AUP, and memory APIs.
Rejected Alternatives: Dumping the shaft runtime into Core; inventing duplicate contracts; changing public contracts mid-batch; direct dependencies on submarine, fauna, or lighting managers.
Scalability potential: Low isolates only two shaft scripts; Middle/High can expand the assembly without dragging the visor feature into world systems; Ultra can add richer source authoring behind the same isolated boundary.
Hardware Impact: Assembly isolation has no frame-time gain by itself. It prevents dependency churn and avoids accidental manager polling; estimated hot-path CPU risk avoided is 3-8 us/frame on i3/MX350.

## Decision 3 - AUP Distance, Screen-UV Output
Problem: The effect must track top light sources via AUP but the shader can only scatter from screen-space coordinates.
Solution: Resolve source and camera positions into `AbsoluteUniversePosition`, score distance fade with `AbsoluteUniversePosition.DistanceSq`, then pass only clamped screen UV/color/intensity to the shader.
Rejected Alternatives: Transform-only scoring; feeding world positions to the shader; extra rebase math after projection; concrete submarine headlight references.
Scalability potential: Low uses 3 fixed sources and 8 taps; Middle keeps the same AUP score with stronger history; High/Ultra can spend saved precision on richer source colors and burst thresholds.
Hardware Impact: AUP distance adds estimated 1-3 us CPU for three sources but prevents long-range precision failure. Avoided shader/world-position reconstruction is estimated 15-30 us GPU/CPU mixed cost on MX350.

## Decision 4 - Typed Signals For Flicker And Burst
Problem: Shafts must react to brownout instantly and emit a burst flare without string events or concrete system dependencies.
Solution: Preserve legacy `GlobalSignals.Publish(BrownoutSignal)` queue behavior and additionally push the same payload into `SignalBus<BrownoutSignal>`. Add fixed-size `VisualFlareSignal` for burst-grade emitters.
Rejected Alternatives: String RPCs, polling submarine power managers, `UnityEvent`, or a single global monolithic event bus lane.
Scalability potential: Low reads a capped typed snapshot; Middle/High/Ultra can let other VFX/audio systems consume `VisualFlareSignal` without additional source coupling.
Hardware Impact: Typed snapshot read is estimated under 2 us/frame for the capped lane. Avoided manager polling/string dispatch is estimated 10-25 us/frame and 0 B/frame GC on i3/MX350.

## Decision 5 - Fixed Native Buffers And Blackbox
Problem: Top-3 selection, temporal history, and telemetry must not allocate in the late-frame hot path and must leave post-mortem evidence on NaN.
Solution: Allocate three `NativeArray` buffers through `H8Memory`: top contributions[3], history[3], telemetry[300]. Dump `Docs/AgentLogs/Dump_ABYSSAL_LIGHTING_TECH.bin` on non-finite contribution detection.
Rejected Alternatives: `List<T>` sort, LINQ, `FindObjects*`, Debug.Log telemetry, dynamic arrays, or no crash dump.
Scalability potential: Low has identical fixed memory with lower taps; Middle/High/Ultra reuse the same buffers and push visual quality through shader taps/intensity, not more CPU allocation.
Hardware Impact: Avoids estimated 0.02-0.08 ms CPU and 100-600 B/frame GC versus dynamic source scans/sorts on i3/MX350. Measured proof absent.

## Decision 6 - Prefab Cleanup Blocked By Tooling, Not Ignored
Problem: `Player.prefab` still contains four `VolumetricLightBeam` component records. Raw YAML deletion risks prefab corruption and violates the prefab/YAML mutation guard.
Solution: Removed first-party code/docs/asmdef VLB dependencies and left prefab mutation blocked until Unity API access reconnects and compiles. The abandoned editor repair script was deleted with its `.meta` so no first-party script keeps VLB references.
Rejected Alternatives: Blind YAML find-and-delete, blanket prefab apply, or keeping an unexecuted editor repair script with VLB reflection strings.
Scalability potential: Low/Middle/High/Ultra all benefit from removing polygon beams once Unity API mutation is available; current prefab state is not verified clean.
Hardware Impact: Expected gain after prefab cleanup is 200-700 us GPU/CPU combined on MX350 depending on beam polygon/material cost. Current gain is not realized because prefab VLB records remain.

## Decision 7 - Verification Wall Is External To Shaft Code
Problem: Full Unity compile and console verification are blocked by existing unrelated compile errors and MCP reports zero live Unity instances, while the editor process remains open.
Solution: Ran a manual C# compile of `Hecton8.Lighting.Shafts` against the last built Core DLL and ran `dotnet build Hecton8.Core.csproj`; recorded failures as PENDING VERIFICATION instead of claiming green.
Rejected Alternatives: Claiming Unity verification from stale logs, killing the user editor process, batchmode prefab mutation while the project is already open, or fixing other agents' domains.
Scalability potential: Low/Middle/High/Ultra shader quality paths are code-present but require Unity shader/profile validation before runtime acceptance.
Hardware Impact: No verified frame-time number. Static estimate remains 400-1500 us saved versus 64-step volumetric raymarch if prefab VLB cleanup and shader import both verify.

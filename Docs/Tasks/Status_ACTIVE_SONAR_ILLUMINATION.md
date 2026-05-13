# Status_ACTIVE_SONAR_ILLUMINATION

Prompt: ACTIVE_SONAR_ILLUMINATION
Role: VFX_TECHNICAL_ARTIST
Domain: VFX / Presentation & UX
Task Count: 19
Status: PENDING VERIFICATION

## Mandates Read Before Coding
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Loop 0 - Intake
- [x] Extract prompt from CURRENT_BATCH.md | Justification: strict batch parsing, XML block only | Alternative rejected: neighboring prompt inference | Estimate: 80 us
- [x] Verify fresh status/rationale state | Justification: batch hygiene gate | Alternative rejected: reading archived batch logs | Estimate: 20 us
- [x] Read domain map and relevant mandates | Justification: domain boundary + DOD constraints before code | Alternative rejected: coding from prompt memory | Estimate: 220 us

## Loop 1 - Tasks 1-5
- [x] Task 1. Purge SonarVfxManager.Instance | Justification: `rg` found no first-party `SonarVfxManager`/`SonarPostProcess` singleton to preserve | Alternative rejected: inventing replacement singleton | Estimate: 35 us/frame saved by no manager dispatch
- [x] Task 2. Consume AcousticPingSignal(ActiveSonar) | Justification: `SpectrumSystem` now publishes and consumes `AcousticPingSignal.ChannelActiveSonar`; `GlobalSignals.Publish` mirrors into `SignalBus<AcousticPingSignal>` | Alternative rejected: direct component dependency | Estimate: 12 us signal path
- [x] Task 3. ASMDEF isolation Hecton8.VFX.Sonar -> Contracts | Justification: added `Assets/_Project/Scripts/VFX/Sonar/Hecton8.VFX.Sonar.asmdef` referencing only `Hecton8.Core.Contracts` | Alternative rejected: moving active sonar owner into monolithic Core cycle | Estimate: 0 us runtime
- [x] Task 4. Eradicate fullscreen Graphics.Blit sonar ring passes | Justification: no sonar `Graphics.Blit` found; legacy fullscreen sonar history/composite RenderGraph path is gated off by default | Alternative rejected: deleting renderer feature asset class and breaking serialized URP references | Estimate: 80-250 us GPU saved at active ping
- [x] Task 5. Define _ActiveSonarCenterAUP and _ActiveSonarRadius | Justification: `SpectrumSystem` publishes both globals; `Hecton_CoreLit.hlsl` declares both | Alternative rejected: material property blocks per renderer | Estimate: 4 us CPU upload
- [!] Compile checkpoint 1 blocked by pre-existing dependency wall | Details: `dotnet build Hecton8.Core.csproj` still fails on unrelated missing assemblies/types (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Physics.CCD`, etc.); no `SpectrumSystem` error remains after adding `Hecton8.Core.Signals`

## Loop 2 - Tasks 6-10
- [x] Task 6. Radius expansion at 1480 m/s on FastTick | Justification: active geo ring radius increments by `dt * 1480f` in the existing tick owner | Alternative rejected: coroutine/tween allocation | Estimate: 1 us scalar loop
- [x] Task 7. Squared-distance mask in Hecton_CoreLit.hlsl | Justification: shader uses `distSq = dot(delta, delta)` against center/radius | Alternative rejected: `distance()`/sqrt path | Estimate: 4-10 ALU saved per ping
- [x] Task 8. Ring math using squared radius delta | Justification: implemented `1.0 - saturate(abs(distSq - radiusSq) * 0.05)` | Alternative rejected: physically thick sphere shell | Estimate: 6 us visual lie
- [x] Task 9. Cyan emissive injection | Justification: `HectonCoreLitEvaluateActiveSonarGeoEmission` returns bright cyan and feeds glow/wreck emission | Alternative rejected: dynamic light source | Estimate: 0.15-0.4 ms GPU saved versus lights/shadows
- [x] Task 10. Triplanar/topological grid overlay | Justification: ring multiplies procedural triplanar grid/noise in CoreLit | Alternative rejected: texture lookup or fullscreen overlay | Estimate: 15-30 ALU only on non-low tier
- [!] Compile checkpoint 2 blocked by same external dependency wall | Details: repeated `dotnet build` with errors-only output still reports unrelated assembly gaps, not new active sonar symbols.

## Loop 3 - Tasks 11-14
- [x] Task 11. Up to 4 simultaneous pings | Justification: fixed `Vector4[4]` center/radius and param arrays with oldest overwrite | Alternative rejected: `List<T>` resize/GC | Estimate: 0 B/frame, bounded 4 loop
- [x] Task 12. Max-range decay | Justification: shader fades intensity as radius approaches 400m max range; CPU culls at max | Alternative rejected: lifetime timer independent of distance | Estimate: 2 ALU per ping
- [x] Task 13. AUP shift safety | Justification: reads `SignalBus<AupShiftSignal>.GetFrameSnapshot()` and subtracts `ShiftMeters` from active centers | Alternative rejected: shader-side origin guessing | Estimate: 4 us only on shift frames
- [x] Task 14. Low-tier triplanar disable | Justification: C# publishes grid flag 0 on tier byte 0; HLSL also compiles `_MATH_LOD_LOW` as grid bypass | Alternative rejected: runtime branch only | Estimate: 15-30 ALU saved per shaded pixel on MX350

## Loop 4 - Tasks 15-18
- [x] Task 15. Zero-GC radius expansion proof | Justification: fixed arrays, scalar math, no per-frame heap containers; only cold NativeArray/Vector4 buffers | Alternative rejected: coroutines/events with closure payloads | Estimate: 0 B/frame
- [x] Task 16. Push ActiveSonarRings count to telemetry | Justification: count changes publish to `GlobalTelemetryBus.PublishModTelemetry`; 300-entry NativeArray blackbox ring records frames | Alternative rejected: managed log strings | Estimate: 3 us on count change, 0 B/frame
- [x] Task 17. Audio echo delay sync | Justification: `PingReturnSignal.EchoDelaySeconds` feeds delayed geo ring start for echo returns | Alternative rejected: visual-only unsynced timer | Estimate: 0 extra allocation
- [x] Task 18. PDA map draws same radius | Justification: PDA reads global `_ActiveSonarRadius`/`_ActiveSonarGeoParams` and mirrors exact active radius into its 2D ring material | Alternative rejected: independent `frac(_animationTime)` sweep while ping active | Estimate: 2 global reads

## Loop 5 - Task 19 & Re-Verification
- [x] Task 19. Shader unroll compile check | Justification: 4-ping loop is explicitly `[unroll]`; prompt re-read; `rg "distance\\(" Hecton_CoreLit.hlsl` returned no matches | Alternative rejected: dynamic loop count | Estimate: bounded four-iteration shader path
- [x] Unity refresh attempted | Justification: MCP `refresh_unity` requested compile and waited | Alternative rejected: claiming Unity console success without session | Estimate: verification only
- [!] Unity console blocked | Details: MCP timed out waiting for editor readiness, then `read_console` returned `Unity session not available`; no Unity compiler result available.

## OMEGA Polish - Final Anti-Bloat
- [x] OMEGA_POLISH parsed only after all 19 tasks were checked | Justification: mandate ordering maintained | Alternative rejected: reading polish before core completion | Estimate: 0 us runtime
- [x] Active sonar grid noise cut to triangle-wave fake | Justification: replaced active sonar `ValueNoise2` call with `abs(frac(dot(...)) - 0.5) * 2.0` | Alternative rejected: honest procedural noise inside active ring | Estimate: 6-12 ALU saved per active sonar shaded pixel
- [x] Active sonar ping count no longer uses shader `round()` | Justification: C# publishes integer count; shader casts directly and clamps | Alternative rejected: redundant float rounding | Estimate: 1 ALU saved per shaded pixel in active path
- [x] Targeted OMEGA grep clean for active sonar | Justification: no `distance(`, `round(_ActiveSonarGeoParams`, or active-sonar `ValueNoise2(stablePosition` matches remain in `Hecton_CoreLit.hlsl` | Alternative rejected: trusting visual inspection | Estimate: verification only
- [!] Final compile still blocked by dependency wall | Details: `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly` produced 104 unrelated missing-type/namespace errors before timeout (`Core.Scheduling`, `Core.Memory.Layout`, `Audio.Propagation`, `Physics.CCD`, `IGroundRadarService`, `BinaryBlittableSafe`, etc.).
- [!] Diff hygiene blocked by unrelated file | Details: `git diff --check` reports trailing whitespace in `Assets/_Project/Scripts/BoidFishInstanced.shader:520`; not edited for this domain.

## Verification Commands
- `rg "distance\\(" Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl` -> no matches.
- `rg "SonarVfxManager|SonarPostProcess|Graphics\\.Blit.*Sonar|Sonar.*Graphics\\.Blit" Assets/_Project -n` -> no forbidden first-party sonar singleton/postprocess/Graphics.Blit matches.
- `rg "distance\\(|round\\(_ActiveSonarGeoParams|HectonCoreLitValueNoise2\\(stablePosition" Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl` -> no active-sonar hot-path matches.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly` -> blocked by unrelated dependency wall listed above; last run produced 104 errors before timeout.
- `git diff --check` -> blocked by unrelated trailing whitespace in `Assets/_Project/Scripts/BoidFishInstanced.shader:520` plus line-ending warnings.

## Iteration Log
- Loop 0: Prompt extracted. Mandates selected. No code edited.
- Loop 1: Signal route and shader global contract added. Compile checkpoint blocked by existing project dependency wall.
- Loop 2: Squared ring, cyan emission, and grid fake added to CoreLit. Compile checkpoint still blocked externally.
- Loop 3: Four-ping array, max-range fade, AUP shift handling, low-tier grid bypass added.
- Loop 4: Zero-GC fixed buffers, telemetry/blackbox dump, echo delay sync, PDA radius mirror added.
- Loop 5: Prompt re-read. `distance()` ban verified. Shader unroll statically verified. Unity session unavailable for final console.
- OMEGA: Active sonar grid noise and ping-count rounding cut from shader hot path. Status remains PENDING VERIFICATION because compile/diff hygiene are blocked outside this domain.

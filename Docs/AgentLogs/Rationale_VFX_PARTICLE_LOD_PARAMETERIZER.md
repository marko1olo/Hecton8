# Rationale - VFX_PARTICLE_LOD_PARAMETERIZER

## Decision 1 - Tier Budget Ceilings

Problem: The prompt requested Low/Mid/High/Ultra compute particle budgets, but mandates conflict: URP low path caps particles hard, VFX compute mandate defines a 37,888 canonical pool, and the compute mandate permits larger desktop particle maxima.

Solution: Low/Mid/High/Ultra were defined as 4,096 / 16,384 / 32,768 / 37,888 total compute particles. Low remains visual fake first. Ultra maxes the current VFX pool instead of inventing a larger pool. High hits the 512-group MX350 soft cap; Ultra is explicitly split per pool.

Rejected Alternatives: A 100K Ultra compute pool was rejected because the current VFX mandate's canonical pool is 32,768 snow + 4,096 bubbles + 1,024 debris. A Low-tier compute-off answer was rejected because the prompt required budgets, but Low uses the cheapest impostor-like compute shape.

Scalability potential: Low = sparse marine snow, no depth collision, no flow resample. Middle = coarse depth collision, flow every 8 frames. High = full near-field compute at 32,768 count. Ultra = canonical max pool with tighter stepping, 4 fake occlusion taps, and higher visual density without changing gameplay truth.

Hardware Impact: On i3/MX350 the Low path is 64 groups at 64 threads. High stays at 512 groups. Ultra is not monolithic on MX350; split dispatch avoids the >512 group soft gate. Expected gain is risk avoidance, not measured frame time. GPU capture absent.

## Decision 2 - JSON Integration Shape

Problem: The dynamic-resolution adapter needs system bits without hard coupling VFX code to DRS internals.

Solution: Added a static JSON TextAsset at `Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` with existing kill-switch bit names: ParticleAdvection, VolumetricFogHighRes, and NonCriticalVfx.

Rejected Alternatives: New EventBus IDs were rejected because these are local scaler bits, not decoupled broadcasts. Direct C# public API edits were rejected because the prompt only needed budgets and a handoff config.

Scalability potential: Low can flip ParticleAdvection and NonCriticalVfx under pressure. Middle keeps one fake occlusion tap. High/Ultra restore richer visual-overkill settings only when the adapter is clear.

Hardware Impact: Integer mask checks are effectively free compared with compute dispatch. No runtime implementation was added, so hot-path delta is 0 us in this change.

## Decision 3 - ShadowTaps Meaning

Problem: The prompt asked for `ShadowTaps`, while project mandates forbid particle shadow casting.

Solution: Defined `ShadowTaps` as screen-space/depth/fog fake occlusion taps only. Particle shadow casters stay disabled.

Rejected Alternatives: Real particle shadow casting was rejected because `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` says particle shadow casting is forbidden and MX350 transparent/shadow budgets are already tight.

Scalability potential: Low = 0 taps. Mid = 1 depth/fog tap. High = 2 taps. Ultra = 4 taps to spend saved simulation cost on better visual grounding.

Hardware Impact: Low saves all particle occlusion sample cost. Ultra spends samples only on high-tier hardware. No measured microseconds; capture required.

## Decision 4 - MarineSnow 50% VRAM Model

Problem: The prompt required theoretical VRAM saving if MarineSnow is cut by 50%.

Solution: Used mandate data: 32,768 MarineSnow at 48 B each = 1.5 MiB. Cutting to 16,384 saves 786,432 B = 0.75 MiB per single resident buffer, 1.5 MiB with ping-pong. Current canonical 64 B struct caveat: 1.0 MiB single, 2.0 MiB ping-pong.

Rejected Alternatives: A single number without buffer topology was rejected because the VFX mandate requires double-buffered UAV particle state.

Scalability potential: Low and emergency pressure can halve MarineSnow and buy back bandwidth for fog/noir readability.

Hardware Impact: On MX350 this is small VRAM but meaningful bandwidth relief during transparent overdraw pressure. Exact frame-time gain is pending GPU capture.

## Decision 5 - 4x4 Blue Noise Matrix

Problem: The prompt requested 4x4 Blue Noise values ready for `Hecton_CoreLit.hlsl`.

Solution: Used the existing deterministic project baker, `Tools/NoiseBaker/GenerateBlueNoise.py`, with `size=4`, seed `0x4845384E`, swaps `2048`. The matrix is stored in the JSON as ranks, bytes, normalized thresholds, and an HLSL snippet.

Rejected Alternatives: A Bayer matrix was rejected because it is not blue-noise generated. A random matrix was rejected because it is not deterministic evidence.

Scalability potential: Low can use the 4x4 fallback when texture sampling is too expensive or unavailable; Middle+ should still use the baked blue-noise texture family for lower shimmer.

Hardware Impact: Fallback can replace one texture fetch in constrained paths, but quality is inferior to the verified full texture. Measured microseconds saved: PENDING CAPTURE.

## Decision 6 - Runtime Catalog and Renderer Binding

Problem: The first pass left the budgets as static handoff data only, while `HectonMarineSnowRenderer` still used stale hard-coded capacities of 32,768 / 65,536 / 100,000. That contradicted the new MX350 budget and kept the real runtime free to overspend.

Solution: Added `VfxComputeParticleBudgetCatalog` as an allocation-free runtime mirror of the JSON and wired `HectonMarineSnowRenderer` to use it. The renderer now reads `HomeostasisBrain.PressureLevel` and `HomeostasisBrain.CurrentKillSwitchMask`, clamps active particles by pressure, disables bubbles/debris under `NonCriticalVfx`, and halves snow/plankton in emergency.

Rejected Alternatives: Loading/parsing JSON in gameplay was rejected because managed JSON parsing allocates and requires asset lifecycle wiring. A new DRS public API was rejected because the existing homeostasis mask already owns these system bits.

Scalability potential: Low/MX350 now allocates only the low marine-snow pool. High and Ultra retain visual overkill without the old 100k pool. Pressure state lowers active count without forcing a buffer resize every frame.

Hardware Impact: Static model at 64 B particle stride: Low saves 1.781 MiB per buffer / 3.562 MiB ping-pong; Mid saves 3.125 / 6.250 MiB; High saves 4.354 / 8.707 MiB; Ultra saves 4.104 / 8.207 MiB compared with the old renderer constants.

## Decision 7 - Drift Validator

Problem: JSON, C# constants, shader values, and renderer bindings can diverge silently.

Solution: Added `Tools/ValidateVfxParticleBudgetCatalog.py`. It validates JSON status, pool sums, MX350 thread limits, C# catalog constants, renderer binding to the catalog, absence of stale renderer literals, and HLSL blue-noise fallback parity.

Rejected Alternatives: Manual review only was rejected because it is not repeatable and fails under batch pressure.

Scalability potential: Future tier changes have one cheap validation path before Unity import.

Hardware Impact: No runtime cost. It prevents budget regressions that would reintroduce oversized compute buffers on MX350.

## Decision 8 - Shader Fallback Hardening

Problem: The first HLSL fallback shape risked local array indexing in shader code, which is compiler-sensitive across URP targets and weak mobile-class GPUs.

Solution: `HectonCoreLitBlueNoise4x4` uses a switch-based literal return path. The validator now rejects the old `static const half blueNoise4x4[16]` pattern.

Rejected Alternatives: A dynamic local array was rejected because shader backends can lower it poorly. A Bayer fallback was rejected again because it fails the prompt's blue-noise requirement.

Scalability potential: Low/MX350 can use the 4x4 fallback with no texture fetch when constrained. High/Ultra can keep richer temporal/noise paths where the shader keyword is not enabled.

Hardware Impact: Expected saving is one avoided texture sample only when the fallback keyword is enabled. Exact microseconds remain pending GPU capture.

## Decision 9 - Legacy Syntax Compatibility

Problem: The scoped legacy compiler check failed on `readonly struct`, meaning the catalog was too modern for the safest local compile probe even if Unity 6000 may accept it.

Solution: Converted `VfxComputeParticleBudget` to a plain struct with readonly fields and removed `in VfxComputeParticleBudget` from the renderer helper signature. Scoped catalog syntax now passes with `C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and local Unity/Core stubs.

Rejected Alternatives: Keeping C# 7.2-only syntax was rejected because this repository has no generated `.csproj` or Unity Editor available for a stronger compile. The safer syntax costs no runtime allocation.

Scalability potential: Same budgets, fewer integration hazards. Low/MX350 behavior is unchanged; High/Ultra still retain the visual-overkill pool.

Hardware Impact: Runtime cost unchanged. Integration risk reduced.

## Decision 10 - Batch File Rotation

Problem: A continuation re-read found that `Docs/Tasks/CURRENT_BATCH.md` had been replaced and no longer contains `VFX_PARTICLE_LOD_PARAMETERIZER`. Archive search found no original prompt copy, only this agent's status file.

Solution: Continued from `Docs/Tasks/Status_VFX_PARTICLE_LOD_PARAMETERIZER.md` and this rationale file as the on-disk anti-amnesia source, then recorded the batch hygiene fault explicitly.

Rejected Alternatives: Borrowing a neighboring prompt from the new batch was rejected because it would violate strict XML parsing and domain ownership. Stopping without finishing the local verification loop was rejected because the user explicitly ordered continuation.

Scalability potential: No runtime impact. It preserves the audit chain for the VFX budget handoff despite batch rotation.

Hardware Impact: No runtime cost. Prevents process-level false reporting.

## Decision 11 - Volumetric Gate Literal Implementation

Problem: The JSON claimed `VolumetricFogHighRes` would cap fake occlusion work, but the renderer only consumed `ParticleAdvection` and `NonCriticalVfx`. That was a report/code mismatch.

Solution: `HectonMarineSnowRenderer` now checks `VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask`. When set, High/Ultra scalability parameters collapse to Mid cadence and debug shadow taps are capped to one through `ResolveEffectiveShadowTaps`.

Rejected Alternatives: Leaving the bit as documentation-only was rejected because batch protocol requires literal implementation. Dropping the whole particle pool was rejected because Mid cadence preserves underwater depth belief with less sampling pressure.

Scalability potential: Low remains cheapest. Mid remains the pressure-safe visual baseline. High/Ultra can still allocate dense pools, but the high-res volumetric/fake-occlusion kill bit makes them behave like Mid until pressure clears.

Hardware Impact: Expected savings are fewer abyssal-flow/fog-density staggered updates and capped fake occlusion taps under pressure. Exact microseconds remain pending GPU capture.

## Decision 12 - JSON HLSL Snippet Parity

Problem: The JSON handoff still advertised a static HLSL array snippet after the real shader fallback was hardened to a switch-based literal path.

Solution: Replaced the JSON `hlslSnippet` with the switch-based `HectonCoreLitBlueNoise4x4` form and expanded `Tools/ValidateVfxParticleBudgetCatalog.py` to reject static-array snippets in both shader and JSON.

Rejected Alternatives: Allowing the JSON to remain a weaker sample was rejected because downstream shader work would copy the wrong pattern.

Scalability potential: Low/MX350 fallback stays texture-free and compiler-stable. High/Ultra are unaffected unless the fallback keyword is enabled.

Hardware Impact: No runtime cost from JSON. Prevents shader-backend drift that could reintroduce local-array indexing on constrained hardware.

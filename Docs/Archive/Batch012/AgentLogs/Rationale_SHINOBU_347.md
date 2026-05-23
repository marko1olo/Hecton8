# Rationale_SHINOBU_347

Agent: SHINOBU_347
Role: DAY_NIGHT_GI_LIGHTING_RELAY
Status: PENDING VERIFICATION

## Preflight

Problem: CPU-side ambient lighting mutation was requested for removal, but existing ownership is unknown.
Solution: Perform source archaeology before creating any relay type. Integration target must be an existing lighting runtime if present; otherwise add isolated presentation-domain runtime files and avoid direct sibling-domain dependencies.
Rejected Alternatives: Creating a standalone day-night manager from the prompt text would duplicate ownership and increase compile-wall risk.
Scalability potential: Low uses flat ambient and cheap depth/gloom scalar; Middle uses blended ambient/fog/directional lanes; High uses full SH upload; Ultra spends saved CPU on richer shader-side SH/noir fog response.
Hardware Impact: Expected low-end i3/MX350 gain is from deleting RenderSettings/DynamicGI/material mutation pressure; static estimate pending after source scan.

Problem: Data route must not become gameplay truth.
Solution: Treat EnvironmentLightingDTO and SH coefficients as presentation-only VISUAL_SYNC data. Exclude from StateRingBuffer/netcode by keeping types in rendering/lighting surface and marking reports accordingly.
Rejected Alternatives: Hashing lighting DTOs for rollback would inject cosmetic drift into authoritative state.
Scalability potential: Visual buffer can vary continuously with GlobalQualityWeight without changing save/netcode identity.
Hardware Impact: Avoids unnecessary hashing and resimulation churn on low-end CPU; estimate pending after implementation.

## Implementation Decisions

Problem: Existing GI relay already owned SH buffers, slow/late frame scheduling, and shader relay state.
Solution: Convert `HectonGIRelaySystem` to partial and add `HectonLightingRuntime_DayNightRelay.cs` as the new day/night CBuffer extension.
Rejected Alternatives: A second MonoBehaviour manager would create double authority and race existing GI relay writes.
Scalability potential: Low tier consumes L0/L1-weighted SH and flat gloom; Middle adds biome color/fog blending; High enables L2 SH response; Ultra uses saved CPU for richer shader-side probe/grid response.
Hardware Impact: Low-end i3/MX350 avoids per-frame RenderSettings/material churn; estimated saved CPU is 35-90 us per visual tick plus reduced SRP batcher invalidation risk.

Problem: The prompt requires exact 64-byte ARM64 DTO mapping with no hot DTO properties.
Solution: `EnvironmentLightingDTO` uses explicit offsets: `AmbientColor` 0, `FogColor` 16, `DirectionalLightColor` 32, `SunIntensity` 48, `MoonIntensity` 52, `SHCoefficientCount` 56, and `SHQualityWeight` 60. Gloom and biome weight are raw lanes: `FogColor.w` and `DirectionalLightColor.w`.
Rejected Alternatives: Adding convenience property getters or a separate SH metadata vector would violate the ready-CBuffer route even if it was convenient for shader scalar params.
Scalability potential: One 64-byte upload is stable from toaster tier to Ultra; quality changes alter values, not layout.
Hardware Impact: 16-byte float4 lanes preserve ARM64/SIMD alignment and avoid misaligned constant-buffer reads.

Problem: Unity `RenderSettings` mutations in the relay hot path violate GPU sovereignty.
Solution: Removed the relay custom-reflection `RenderSettings` mutation and left only `_WaterVolume` shader texture binding. Ambient/fog are now delivered through `HectonEnvironmentLighting` CBuffer.
Rejected Alternatives: Keeping custom reflection as "not ambient" still leaves the relay dependent on Unity global render state.
Scalability potential: Low tier reads ambient fallback; Ultra can combine the same CBuffer with custom probe grid trilinear sampling.
Hardware Impact: Avoids Unity global state validation and reflection texture churn on low-end CPU; static estimate 5-20 us on frames where reflection state would be checked/mutated.

Problem: Biome lighting needs spatial precision without float-origin artifacts.
Solution: Resolve player and biome center as AUP `double3`, subtract in double precision, then cast the small local delta to float for weighting.
Rejected Alternatives: Direct world-space float subtraction would create biome blend jitter far from origin.
Scalability potential: Low tier still uses the local scalar weight; higher tiers use the same stable weight to push richer ambient/fog variation.
Hardware Impact: One double subtraction per visual tick is cheaper than visible lighting pops and does not scale with entity count.

Problem: Visual lighting must not become gameplay/netcode truth.
Solution: Route is documented as `VISUAL_SYNC`; buffers `0x630820..0x63082C` are `SystemID.GraphicsScalability`; rollback/netcode grep shows no `EnvironmentLightingDTO` or `LightingRelay` in StateRing/Merkle code.
Rejected Alternatives: Adding lighting DTO hashes to rollback would make cosmetic quality changes cause desync work.
Scalability potential: `GlobalQualityWeight` can continuously change SH order, gloom, cadence, and debug visibility without affecting authority.
Hardware Impact: Low-end devices avoid Merkle/hash pressure; expected saved cost depends on rollback leaf budget but is non-zero every hash cadence.

Problem: Deep-sea gloom must look convincing without simulating physical light transport.
Solution: Use a controlled "Dear Lie" blend: cheap depth ramp at low quality and a Pade-style reciprocal extinction fake at higher quality, both scaled by `WaterExtinctionConstant`.
Rejected Alternatives: Volumetric photon/raymarch lighting for global GI transitions is over budget and unpredictable.
Scalability potential: Low uses cheap ramp; Middle blends ramp/reciprocal extinction; High and Ultra spend the same scalar in shader/probe resolve.
Hardware Impact: Burst scalar math cost is under 1 us; replaces large CPU/render-state transitions.

Problem: CSV gradient profiles must not allocate during the visual path.
Solution: Cold file read feeds a `ReadOnlySpan<byte>` parser that writes fixed `LightingGradientProfileDTO` slots; VISUAL_SYNC reads only native arrays.
Rejected Alternatives: `string.Split`, LINQ, dictionaries, or ScriptableObject lookups on the hot path.
Scalability potential: 32 capped profiles support weak devices with sparse profiles and Ultra with richer biome palette values without layout changes.
Hardware Impact: Zero hot-path GC; cold parser cost is editor/maintenance only.

Problem: Failures need forensic proof, not "unknown crash" language.
Solution: Added 300-entry `LightingRelayTelemetryEntry` ring and raw `ReadOnlySpan<byte>` dump to `Docs/AgentLogs/Dump_SHINOBU_347.bin` on non-finite DTO or over-budget upload/job timing.
Rejected Alternatives: `Debug.Log` spam, BinaryWriter row serialization, or unbounded managed history.
Scalability potential: Same fixed ring works across Low/Middle/High/Ultra; only recorded scalar values vary.
Hardware Impact: Fixed 64-byte entries produce predictable cache behavior; hot write is one indexed struct assignment.

## Ultra Polish Decisions

Problem: The first pass wrote `Depth01` into `FogColor.w` while the shader interpreted that lane as deep gloom.
Solution: `EvaluateGlobalIlluminationJob` now writes the actual `gloom` scalar into `FogColor.w`; editor and telemetry read the raw field directly.
Rejected Alternatives: Renaming shader semantics to "depth" would preserve the bug and break the assigned deep-gloom output.
Scalability potential: Low/Middle/High/Ultra all consume one consistent scalar; high tiers use it to modulate SH/probe response instead of CPU fog mutation.
Hardware Impact: Removes diagnostic ambiguity with no extra CPU cost; expected saved debug time is higher than runtime microseconds.

Problem: `GlobalRegistry.CelestialRuntimeSnapshot` polling in the visual cadence violated the cold-registry route.
Solution: Cache Agent 345 `CelestialStateDTO` read handle during cold dependency refresh and read it through `GlobalDataVault.TryReadHandle` in the relay phase.
Rejected Alternatives: Continuing to poll the registry every SlowTick would make GlobalRegistry a hot data bus.
Scalability potential: Celestial truth remains one owner; the lighting relay only consumes immutable read state.
Hardware Impact: One generation-checked native read avoids managed registry traversal and keeps the relay route predictable on i3/MX350.

Problem: Runtime assignment to `QualitySettings.shadowCascades` was a global Unity render-setting mutation outside the CBuffer relay.
Solution: Removed the assignment. The relay now keeps local cascade telemetry only; actual global project quality remains owner-controlled elsewhere.
Rejected Alternatives: Treating shadow cascades as a harmless "visual" tweak still mutates global Unity render state from the wrong owner.
Scalability potential: Quality can still drive shader SH/probe detail continuously without project-setting jumps.
Hardware Impact: Avoids possible SRP/quality validation churn and prevents cascade popping on constrained GPUs.

Problem: SH coefficients were uploaded but UberNoir had no direct reader for `_HectonGIRelaySHBuffer`.
Solution: `Hecton_CustomLightProbeGrid.hlsl` now declares the SH buffer, reads SH metadata from `_H8EnvironmentScalarParams.zw`, evaluates L0/L1/L2 with continuous `GlobalQualityWeight`, and blends it into UberNoir ambient fallback before custom probe grid overdraws it.
Rejected Alternatives: CBuffer-only flat ambient would pass the color requirement but leave the assigned SH interpolation visually unused.
Scalability potential: Low collapses toward flat CBuffer ambient; Middle admits L1; High/Ultra admit L2 and trilinear probe detail smoothly.
Hardware Impact: Mobile can skip higher-order SH by weight; desktop spends saved CPU on richer shader ambient response.

Problem: The CSV bridge used numeric float rows, missing the assigned human-readable profile-name and hex-color route.
Solution: Parser now accepts FNV-1a profile names and `#RRGGBBAA` colors while preserving numeric compatibility; the authored CSV was converted to name + hex rows.
Rejected Alternatives: `ColorUtility.TryParseHtmlString`, `string.Split`, and LINQ were rejected for managed allocations and non-Burst-friendly authoring habits.
Scalability potential: Designers can widen biome palettes without C# recompiles; runtime buffer capacity remains fixed at 32 rows.
Hardware Impact: Cold/editor parse only; hot VISUAL_SYNC path remains native-array reads.

Problem: The profile reload facade still compiled managed file IO into development players.
Solution: Fence `RequestLightingGradientProfilesReload()` behind `UNITY_EDITOR` only; automated authoring and CI editor runs keep the bridge, player runtime loses the managed allocation/IO entry point.
Rejected Alternatives: Keeping `DEVELOPMENT_BUILD` access would make a public runtime facade capable of allocating `byte[]` during gameplay diagnostics.
Scalability potential: Low/Middle/High/Ultra all use the same fixed profile Vault buffer; only editor tooling can refill it from CSV.
Hardware Impact: Removes accidental player-side file read stalls and GC pressure; runtime microsecond path unchanged.

## Residual Risk Auditor Decisions

Problem: The mock environment facade still compiled into development players and executed `IJobParallelFor.Run` from a public runtime method.
Solution: Fence `GenerateMockLightingEnvironment()` behind `UNITY_EDITOR`; the CI/editor bridge remains, but player/development-player runtime cannot run same-frame mock Burst work from a public call.
Rejected Alternatives: Keeping `DEVELOPMENT_BUILD` access would preserve an accidental main-thread Burst execution route during diagnostics.
Scalability potential: Low/Middle/High/Ultra player builds consume only owner-scheduled VISUAL_SYNC data; editor can still seed deterministic mock rows for tuning and CI.
Hardware Impact: Prevents debug-player stalls on i3/MX350; runtime cost removed from player builds is bounded by 128 mock rows and immediate job dispatch overhead.

Problem: Public `SetEditor*` methods could still mutate lighting tuning and shader debug state if called by player runtime code.
Solution: Keep method signatures for the Editor assembly but compile bodies only under `UNITY_EDITOR`.
Rejected Alternatives: Trusting the method name as a guard is not an architectural boundary.
Scalability potential: Runtime quality and lighting math remain owned by Vault/GlobalQualityWeight, not ad hoc public setter calls.
Hardware Impact: Removes an accidental shader global write path from player runtime; normal VISUAL_SYNC upload cost is unchanged.

Problem: The scanner proof route could lose fields after future editor execution.
Solution: Retain dedicated and shared-report writes, and extend scanner-generated fields with native buffer, rollback boundary, and black-box dump proof.
Rejected Alternatives: Manual JSON edits without scanner parity would be erased by the next menu run.
Scalability potential: Proof artifacts stay stable across devices and editor reruns; runtime payload layout is unchanged.
Hardware Impact: Editor-only I/O; no player runtime cost.

Problem: A residual audit note incorrectly claimed `Docs/Tasks/CURRENT_BATCH.md` no longer contained the `SHINOBU_347` XML block.
Solution: Re-extracted the full block by exact `<AGENT_PROMPT id="SHINOBU_347">` regex. The apparent 21-task count was a naive regex false positive from Task 10's text reference to `Task 07:`; actual task headings remain Tasks 01-20.
Rejected Alternatives: Keeping the false missing-batch note would make disk state less reliable than chat memory.
Scalability potential: No runtime impact; preserves assignment truth for future context compaction.
Hardware Impact: None.

Problem: `OOP_Lighting_Scanner` overwrote the shared `RENDERING_OPTIMIZATION_REPORT.json`, erasing neighboring agents' proof artifacts.
Solution: Scanner now writes `RENDERING_OPTIMIZATION_REPORT_SHINOBU_347.json` as its owned artifact and upserts only the `shinobu_347_day_night_gi_relay` object into the shared report through a `.tmp` + `.bak` atomic write path.
Rejected Alternatives: Keeping a full-file overwrite would be faster to code but violates simultaneous-agent report sovereignty.
Scalability potential: Tooling change has no runtime visual tier effect; it preserves proof stability from weak-device lanes to Ultra because evidence objects are not lost between scans.
Hardware Impact: Runtime impact is zero. Editor scan IO is cold tooling only; prevention value is avoiding stale/falsified optimization reports.

Problem: Final CPU/compiler guard turned green, but generated Unity `.csproj` files do not include `HectonGIRelaySystem.cs` or the new SHINOBU_347 scripts.
Solution: Do not run `dotnet build` as proof. Record stale-project blocker and require Unity import/regeneration before external compile coverage is meaningful.
Rejected Alternatives: Running a solution build that cannot compile the changed lighting files would create a false green/false red report and waste IO.
Scalability potential: No runtime impact; protects iteration speed and proof integrity.
Hardware Impact: Avoided one large stale solution build on the developer machine.

Problem: Residual audit found that relay upload helpers could lazily create replacement `GraphicsBuffer` objects if a buffer was missing or invalid.
Solution: Restrict `EnsureShUploadBuffers()` and `EnsureEnvironmentLightingCBuffer()` to cold storage setup, add pure readiness checks, and make hot upload paths fail closed without allocating or publishing compatibility vector globals.
Rejected Alternatives: Lazy hot GPU-buffer recreation would hide device-loss/misordered-boot errors behind driver allocations and late-frame stalls.
Scalability potential: Low/Middle/High/Ultra all use the same precreated double-buffer pairs; `GlobalQualityWeight` changes math values and shader weights, not buffer lifecycle or ABI.
Hardware Impact: Avoids runtime graphics-driver allocation spikes on i3/MX350/Quest-class devices; expected avoided hitch is 20-120 us per accidental recovery frame plus reduced GC/driver synchronization risk.

Problem: Verification cannot legally use a solution build right now.
Solution: Keep validation source-static: hot allocation scans, forbidden lighting scans, JSON parse, DTO property scan, brace count, whitespace scan, asmdef reference scan, and stale `.csproj` coverage scan. Record active `csc`/`dotnet` processes as the current build-policy blocker.
Rejected Alternatives: Launching another build while `csc`/`dotnet` are already active would violate the explicit hardware/compile-wall guard and still would not cover stale Unity project files.
Scalability potential: No runtime change; protects iteration speed and proof integrity across concurrent agent work.
Hardware Impact: Avoided compounding compiler CPU/IO pressure on the developer machine.

Problem: The compatibility fallback branch still released the environment CBuffer pair from the late-frame upload path.
Solution: First removed `ReleaseEnvironmentLightingCBuffer()` from fallback, then removed the fallback vector route entirely. Invalid or unsupported CBuffer state now records `CBufferUnavailable` telemetry and fails closed without creating, releasing, or publishing duplicate vector globals.
Rejected Alternatives: Releasing invalid buffers or publishing fallback vectors in VISUAL_SYNC looks tidy but preserves a second scene-color route and can trigger driver synchronization exactly when the frame is trying to recover.
Scalability potential: All quality tiers share the same stable buffer lifecycle and 64-byte CBuffer ABI; quality changes values, not publication topology.
Hardware Impact: Avoids a potential 10-80 us driver/resource-release spike plus four shader-global vector writes on fallback frames.

Problem: Subagent audit found SH buffer upload did not guarantee unlock if the mapped-copy path aborts before `UnlockBufferAfterWrite`.
Solution: Wrap the SH `LockBufferForWrite` copy in `try/finally` and always call `UnlockBufferAfterWrite` before binding the buffer.
Rejected Alternatives: Relying on the current `UnsafeUtility.MemCpy` happy path leaves a GPU buffer lock leak in exceptional or future-edited code paths.
Scalability potential: No quality-tier behavior change; the same 27-float SH upload is safer across all devices.
Hardware Impact: Avoids a possible persistent GPU upload stall/resource fault after an exceptional upload frame.

Problem: Subagent audit found SH shader state could keep a nonzero coefficient count after upload buffers were released.
Solution: Superseded the separate `_HectonGIRelaySHState` route entirely by packing SH coefficient count and quality into `EnvironmentLightingDTO` offsets `56/60`.
Rejected Alternatives: Keeping a separate vector global plus a teardown clear still preserves a second hot shader-state route.
Scalability potential: No visual tier identity change; the same CBuffer row gates SH order continuously.
Hardware Impact: Avoids one vector-global write per SH upload and removes a stale-state teardown hazard.

Problem: The legacy GI relay black-box path used a generic render filename while the assigned route requires SHINOBU_347-owned forensic artifacts.
Solution: Keep `Dump_SHINOBU_347.bin` for the new day/night lighting telemetry format and rename the legacy SH sync ring to `Dump_SHINOBU_347_GI_RELAY_SYNC.bin` to avoid binary-format overwrite.
Rejected Alternatives: Sending both ring formats to one file would corrupt forensic interpretation; leaving the generic name weakens agent ownership.
Scalability potential: No runtime visual tier change; fault artifacts remain route-specific.
Hardware Impact: Runtime steady-state cost is zero; fault dump path remains explicit and parseable.

Problem: The GI relay still crossed into `HectonUnderwaterVisuals.ApplyGIRelaySurfaceEmission`, which can trigger ocean material binding from the lighting cadence.
Solution: Remove the cached `GlobalRegistry.UnderwaterVisuals` route and remove the direct material bridge call. Surface emission remains in the day/night CBuffer/SH buffer path consumed by UberNoir.
Rejected Alternatives: Letting a presentation owner patch ocean materials from the GI relay would preserve CPU-side material validation and violate the CBuffer-only lighting relay mandate.
Scalability potential: Low/Middle/High/Ultra all receive the same surface-emission scalar through shader-visible lanes; quality affects shader math, not material ownership or binding churn.
Hardware Impact: Avoids a 5-60 us material-binding spike on change frames and removes a visual-owner accessor fallback that could search camera depth on weak CPUs.

Problem: `ApplyShaderRelayState` still computed atmosphere, surface emission, and depth palette as `UnityEngine.Color` values on the CPU, duplicating the Burst-written CBuffer color route.
Solution: Remove the CPU color caches, helper lerp methods, `Shader.SetGlobalColor` calls, and obsolete color property IDs from `HectonGIRelaySystem`. Follow-up residual hardening removed the remaining scalar/vector relay globals; scene color truth now comes from `EnvironmentLightingDTO` and `_HectonGIRelaySHBuffer`.
Rejected Alternatives: Keeping "no allocation" manual `new Color` math would still preserve a second color owner and violate the single CBuffer route requested by the assignment.
Scalability potential: Low/Middle/High/Ultra now scale color fidelity through the Burst job and shader quality weights only; quality cannot diverge between old globals and the CBuffer.
Hardware Impact: Avoids roughly 8-35 us on color-change frames from CPU color comparisons/global color uploads and removes a maintenance path that could reintroduce material-state churn.

Problem: Residual audit found duplicate `GlobalRegistry.RegisterGIRelayRuntime` calls from `Awake()` and `OnEnable()`.
Solution: Keep `Awake()` as cold dependency capture only and register once from `OnEnable()` behind `_registeredGIRelayRuntime`; shutdown unregisters only when this owner actually registered the route.
Rejected Alternatives: Relying on registry overwrite/idempotence hides boot-order bugs and makes ownership proof depend on GlobalRegistry internals.
Scalability potential: Registration identity is fixed across Low/Middle/High/Ultra; quality changes visual math only, not service ownership.
Hardware Impact: Avoids duplicated boot registry mutation and removes a route that could generate false owner-state churn during enable cycles.

Problem: `SlowTick()` could finalize a completed SH job before the dispatcher late-frame swap window.
Solution: `SlowTick()` now returns while an SH job is pending. `CompleteAndPushPendingSHJob()` is reachable from `LateFrameTick()` only; SystemDispatcher wraps late-frame tickables with `DispatcherJobFence.BeginLateFrameSwapWindow()`.
Rejected Alternatives: Treating `IsCompleted` as enough proof still leaves hidden `.Complete()` outside the explicit owner swap phase.
Scalability potential: Low-quality devices may run lower visual cadence, but readback ownership remains stable; high tiers still upload richer SH data in the same swap window.
Hardware Impact: Prevents phase drift and surprise main-thread sync placement; the immediate microsecond gain is small, but the frame-time proof is stronger.

Problem: Environment lighting still had a `Shader.SetGlobalVector` compatibility fallback beside the CBuffer path.
Solution: Remove the fallback vector route. If the precreated CBuffer pair is missing or unsupported, the relay records `CBufferUnavailable` telemetry and fails closed instead of publishing duplicate `_H8Environment*` globals.
Rejected Alternatives: Keeping fallback vectors preserves a second scene-color route and makes shader truth split by runtime resource state.
Scalability potential: Low/Middle/High/Ultra all use the same 64-byte CBuffer ABI; quality alters values only, not publication topology.
Hardware Impact: Avoids four shader-global vector writes on fallback frames and removes a maintenance path for CPU-side lighting drift.

Problem: SH metadata used a separate hot `_HectonGIRelaySHState` vector global.
Solution: Pack `SHCoefficientCount` and `SHQualityWeight` into `EnvironmentLightingDTO` offsets `56` and `60`, read them from `_H8EnvironmentScalarParams.zw` in HLSL, and remove `_HectonGIRelaySHState` from C# and shader source.
Rejected Alternatives: A separate state vector is convenient, but it violates the ready-CBuffer route and adds an extra hot global write per SH upload.
Scalability potential: The same CBuffer controls L0/L1/L2 admission continuously; no binary feature flag or separate shader state route is required.
Hardware Impact: Avoids one shader-global vector write per SH upload and keeps metadata cache-local with the 64-byte CBuffer row.

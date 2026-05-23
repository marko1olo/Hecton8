# Rationale SHINOBU_326

Status: IMPLEMENTED / COMPILE BLOCKED BY CPU GUARD

## 2026-05-22 Initial Decision Record

Problem: VR horizon stabilization must decouple physical KCC roll/pitch from the rendered camera without managed allocations or transform hierarchy coupling.
Solution: Start with isolated unmanaged DTOs and Burst jobs under Player/VR domain, then adapt to discovered KCC/Vault/dispatcher contracts instead of inventing cross-domain dependencies.
Rejected Alternatives: Direct camera parenting, Transform.rotation smoothing, PostProcessVolume mutation, Mathf.Lerp, and local Persistent NativeArray ownership are rejected by task and mandates.
Scalability potential: Low uses same deterministic kernel with cheaper cadence and lower optional telemetry density; Middle keeps full comfort response; High increases debug/telemetry fidelity; Ultra can spend saved presentation budget on richer shader-side vignette/foveation while gameplay truth stays unchanged.
Hardware Impact: Expected runtime cost target is below 100 microseconds on i3/MX350 because work is flat NativeArray traversal, Burst quaternion math, and shader scalar publication. Measured proof absent.

## 2026-05-22 Horizon Lock Implementation Decisions

Problem: Prompt path `Assets/_Project/Scripts/Player/` / `VR/` is absent; active owner is `Gameplay/VRSomaticProvider`.
Solution: Implemented `VRSomaticProvider.HorizonLock.cs` partial and reused existing Vault/shader/provider lifecycle.
Rejected Alternatives: New camera hierarchy owner or direct KCC polling; both would break owner route and raise coupling risk.
Scalability potential: Low keeps one-entity solver and stronger comfort scalar; Middle uses normal profile; High/Ultra can spend saved budget on shader-side presentation without altering authority.
Hardware Impact: Avoids extra scene searches and Transform parenting; estimated i3/MX350 cost remains below 0.1 ms because jobs touch 32B state + 96B telemetry.

Problem: KCC DTO has AUP/velocity/angular velocity but no raw quaternion rotation field.
Solution: Added visual-only Vault mirror lanes: KCC-shaped state mirror, raw rotation, horizon write/read, horizon telemetry. Raw rotation is presentation input, not KCC truth. The mirror uses a local explicit-layout `VRSomaticKinematicStateMirrorDTO` to avoid a direct Gameplay-to-Physics runtime assembly dependency.
Rejected Alternatives: Modify physics `KinematicStateDTO` ABI, import `Hecton8.Physics.KCC`, or read `ShinobuHydroKccStates` hot from Gameplay; all cross domain boundaries.
Scalability potential: Low/Middle write one mirrored row; High/Ultra can raise visual diagnostics without changing physics DTO.
Hardware Impact: 64B KCC mirror + 16B raw rotation + 32B horizon row, all sequential cache-local.

Problem: VR horizon must suppress pitch/roll without corrupting player yaw.
Solution: `EvaluateHorizonStabilizationJob` computes yaw-only world-up target and applies damped quaternion slerp from prior read state.
Rejected Alternatives: `Transform.rotation` smoothing, `Quaternion.Lerp`, and full-body roll lock; Unity main-thread math is not Burst and causes sickness coupling.
Scalability potential: Low: aggressive gravity weight; Middle: default profile; High: softer visual lock; Ultra: extra shader polish funded by same stable scalar.
Hardware Impact: Single quaternion normalize/slerp path estimated 8-18 us on i3/MX350 for one entity.

Problem: FOV tunneling must be a visual Dear Lie, not a PostProcess managed object.
Solution: `CalculateFovTunnelingJob` writes a scalar into `VRSomaticComfortDTO`; publish merges into existing `_VRComfortVignette` / `_HectonVRSomaticComfortState`.
Rejected Alternatives: Runtime `PostProcessVolume`, UI overlay geometry, or `Mathf.Lerp`.
Scalability potential: Low ramps tunnel hard; Middle follows comfort profile; High/Ultra lowers tunnel but can increase foveated/post shader richness.
Hardware Impact: Scalar math and one shader-global path, no managed allocation in hot loop.

Problem: Crash proof must explain NaN/quaternion spikes and budget breaches.
Solution: Added `SomaticTelemetryEntry` 300-frame Vault ring and raw `ReadOnlySpan<byte>` dump to `Docs/AgentLogs/Dump_SHINOBU_326.bin`.
Rejected Alternatives: BinaryWriter field-by-field dump only or chat report.
Scalability potential: Low keeps ring fixed; Middle/High/Ultra can display richer editor graph from same ring.
Hardware Impact: 96B/frame write; dump is cold fault path.

Problem: Shared rendering report is a multi-agent file and must not be flattened.
Solution: Added/kept `shinobu_326_vr_horizon_lock` section and changed scanner to upsert that section into the shared report.
Rejected Alternatives: Overwrite root JSON with a single-agent object; that destroys other agents' evidence.
Scalability potential: Static proof is editor-only across all hardware tiers.
Hardware Impact: None at runtime.

Problem: Binary payload ledger lacked a route card for the new visual-only Vault lanes.
Solution: Added a concise SHINOBU_326 section to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with BufferIDs, ABI, Dear Lie, scalability, fault route, and verification gate.
Rejected Alternatives: Chat-only proof or hidden buffer IDs with no architecture ledger entry.
Scalability potential: Documentation states Low/Middle/High/Ultra behavior through continuous `GlobalQualityWeight`, not a tier switch.
Hardware Impact: None at runtime; reduces integration ambiguity for concurrent agents.

Problem: Compile verification needed but build guard forbids contention.
Solution: Checked `csc/dotnet/VBCSCompiler` and CPU. Earlier guard sampled CPU 87% with active `VBCSCompiler.exe`; an intermediate guard sampled CPU 15% but seven active `dotnet.exe` processes; current guard sampled CPU 97% with no active compiler processes, so no dotnet build was launched.
Rejected Alternatives: Forcing build under active Unity/dotnet load.
Scalability potential: Verification remains pending until CPU <50 and no `dotnet.exe`/`csc.exe`/`VBCSCompiler.exe` competing compile.
Hardware Impact: Avoided starving concurrent agents and user editor process.

Problem: `ValidateNativeLayouts()` is compiled in release players, while the new horizon ABI validator exists only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Solution: Wrapped the call to `ValidateHorizonLockLayouts()` in the same compile symbols, preserving editor/dev ABI checks without creating a release-build unresolved method.
Rejected Alternatives: Make ABI reflection validation live in release, or remove the validation entirely. Release reflection is unnecessary cold overhead; removing validation weakens ARM64 layout proof.
Scalability potential: Low/Middle/High/Ultra all keep the same DTO ABI; validation is proof-only and does not alter quality behavior.
Hardware Impact: Prevents a player-build compile fault with 0 runtime cost on i3/MX350.

Problem: Horizon stabilization was using a first-order exponential blend, which is stable but under-specifies the requested critically damped quaternion spring behavior.
Solution: Replaced the blend coefficient with a closed-form critical damping response `1 - (1 + omega * dt) * exp(-omega * dt)`, blended against a cheap polynomial approximation through continuous `GlobalQualityWeight`. At quality <= 0.3 the solver returns the polynomial approximation and bypasses the scalar `exp`; from 0.3..0.85 it smoothsteps into exact response. The DTO ABI stays fixed at 32B and no spring velocity buffer is added.
Rejected Alternatives: Add another persistent spring velocity Vault lane or keep the first-order filter. A new lane would increase core BufferID churn and H-Phi surface without a measured need; keeping first-order math leaves the batch's spring wording weak.
Scalability potential: Low uses the polynomial critical-response approximation and stronger omega; Middle blends exact and cheap response; High/Ultra use more exact critical response while reducing visible tunnel intensity through existing profile/quality curves.
Hardware Impact: Low-tier path skips the scalar `exp`; exact path remains one scalar exponential for one player row, estimated under 1 us on i3/MX350.

Problem: Subagent audit found the rendering report scanner was a token scanner, not an AST scanner, and the shared JSON upsert could delete a separator or narrow the shared report to one section.
Solution: Upgraded `Camera_Hierarchy_Scanner` to Roslyn `CSharpSyntaxTree` scanning with token fallback, parser failure accounting, and a top-level JSON property range remover that preserves sibling sections. Rebuilt the current shared report from committed baseline plus current foreign sections and the SHINOBU_326 section, then validated `ConvertFrom-Json`.
Rejected Alternatives: Leave the original line scanner and rely on chat proof; overwrite the shared report with a single SHINOBU_326 object; revert the entire shared report and discard concurrent agents' sections.
Scalability potential: Editor-only proof path; runtime Low/Middle/High/Ultra behavior is unchanged. The scanner prevents future report overwrite loops without adding runtime cost.
Hardware Impact: 0 runtime cost. Editor scan allocates managed AST objects only under `#if UNITY_EDITOR`.

Problem: `Tick()` could reach `ResolveDataVault()` and `EnsureSomaticComfortBuffers()` through `ScheduleSomaticComfortKernel()` if cold bootstrap missed a Vault-ready window.
Solution: Removed hot-path registry/Vault allocation fallback from `ScheduleSomaticComfortKernel()`. Runtime now fail-closes unless cached Vault handles already exist; buffer creation stays in owner cold registration/hot-swap paths.
Rejected Alternatives: Keep opportunistic hot allocation for resilience, or call `EnsureNativeBuffers()` every frame. Both violate GlobalRegistry cold-only and Vault ownership discipline.
Scalability potential: All quality tiers use the same cached handles; weak devices avoid surprise frame spikes from late Vault lane creation.
Hardware Impact: Removes a possible main-thread allocation/registry branch from VR frame path; worst-case missed bootstrap becomes no comfort update instead of a hitch.

Problem: The original strict XML extraction regex looked only for `<AGENT_PROMPT id="SHINOBU_326">`, while the actual batch tag is `<AGENT_PROMPT id="SHINOBU_326" role="SOMATIC_COMFORT_VR_HORIZON_LOCK" chat_name="SHINOBU_326">`.
Solution: Re-read the batch with `Select-String` over `SHINOBU_326|SOMATIC_COMFORT|HORIZON_LOCK`, capturing the true tag and Tasks 01-20. Status tracking stays aligned with the actual prompt, not a malformed local regex.
Rejected Alternatives: Treat the failed regex as missing prompt data or continue from memory only.
Scalability potential: Documentation-only; no runtime impact.
Hardware Impact: 0 runtime cost.

Problem: The editor-only VR comfort tuner refreshed Vault views from `GlobalRegistry.DataVault` on `OnInspectorUpdate`, which is diagnostic code but still violates the cold-only registry doctrine when the window is left open.
Solution: Routed editor diagnostics through `GlobalDataVault.TryGetLatestCreated()` and kept all runtime Vault handles cached in the provider owner phase.
Rejected Alternatives: Keep editor registry polling because it is outside player runtime. That hides a doctrine breach and trains future tools to poll the registry.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; editor readout remains a diagnostic observer of the same fixed Vault lanes.
Hardware Impact: 0 runtime cost. Editor-only polling no longer touches the registry service path.

Problem: Compile proof is the only remaining Task 20 evidence, but the guard sampled CPU at 97 even though `dotnet.exe`, `csc.exe`, and `VBCSCompiler.exe` were absent.
Solution: Deferred `dotnet build` until CPU is below 50 and compiler processes are absent simultaneously.
Rejected Alternatives: Run the build during CPU contention to satisfy paperwork.
Scalability potential: Verification-only; protects local iteration for all concurrent agents.
Hardware Impact: Avoided forcing a compile spike on an already loaded workstation.

# Rationale_SHADER_OVERKILL_ARCHITECT

Status: SHADERS CRYSTALLIZED / VISUAL ORGASM READY - COMPILE BLOCKED BY EXTERNAL WORLD-GPR DEPENDENCY
Agent: SHADER_OVERKILL_ARCHITECT

## Decision 001 - Active Dependency Logs Missing
Problem: The prompt mandates reading `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md`, but both active files are absent.
Solution: Record the absence as an evidence gap, inspect current shader/C# implementation sources, and avoid claiming inherited proof from missing logs.
Rejected Alternatives: Reading archived batch logs as authority was rejected because current AGENTS hygiene forbids stale-batch log use unless explicitly current; treating absent logs as read was rejected as fake reporting.
Scalability potential: Low/Middle/High/Ultra remain shader-tier driven; missing logs do not block implementing tier gates, but they block claiming historical runtime proof.
Hardware Impact: Estimated runtime gain from this decision is 0 us; it prevents incorrect dependency assumptions on i3/MX350.

## Decision 002 - Mandate Set
Problem: The shader library crosses SRP batching, AUP precision, Resident Drawer, GraphicsBuffer instance data, dither transparency, caustics, and zero-GC C# property IDs.
Solution: Read `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `REND_DescriptorBinding_Reality_Check`, `REND_GPU_Sovereignty`, `MATH_AUP_Determinism_Sync`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, and `OPT_Zero_GC_Policy_AllocFree_Mandate`.
Rejected Alternatives: Generic URP shader implementation was rejected because the project requires specific AUP and SRP/Resident Drawer constraints; adding a render pass was rejected because the prompt asks for shader core library first.
Scalability potential: Low disables POM/caustics/bending; Middle enables cheaper caustic/detail; High enables POM/caustics/bending; Ultra increases visual overkill through stricter samples and richer emission without changing CPU path.
Hardware Impact: Estimated low-end gain versus fragmented shader/material path is 30-120 us CPU SetPass overhead pending Frame Debugger proof; shader GPU cost remains tier-gated.

## Decision 003 - Uber Library Instead Of Fragmented Shader Passes
Problem: Caustics, rust, deformation, fog cutout, and emission were requested as one shader core to reduce pass and SetPass fragmentation.
Solution: Created `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl` as a single URP-compatible HLSL library with feature branches and `_MATH_LOD_LOW` stripping. All material parameters live in one `UnityPerMaterial` CBUFFER.
Rejected Alternatives: Separate rust/deformation/caustics shaders were rejected because they multiply SetPass calls; runtime material mutation was rejected because it damages SRP Batcher and Resident Drawer behavior.
Scalability potential: Low returns albedo/roughness with no POM, caustics, bending, or biolum. Middle enables cheap surface detail. High enables caustics and deformation. Ultra/GOD spends saved CPU budget on POM, spectral emission, and denser fake lighting.
Hardware Impact: Estimated i3/MX350 gain is 30-120 us CPU by avoiding pass churn; GPU load on low silicon is reduced by stripping the heavy branches.

## Decision 004 - AUP Before Matrix World Position
Problem: At 100 km scale, computing world position before applying the universe offset preserves float jitter in the matrix multiply.
Solution: Subtract `_TotalUniverseOffset.xyz` from the instance/object matrix translation before multiplying object-space position.
Rejected Alternatives: Subtracting after world position calculation was rejected because it cleans the final value but not the precision loss inside the multiply.
Scalability potential: Low through Ultra use the same deterministic coordinate baseline; higher visual tiers do not buy precision with unstable world coordinates.
Hardware Impact: Cost is three subtracts per vertex; estimated 0-5 us GPU per dense visible batch. Visual gain is stable sub-pixel hull/detail positioning on low-end and high-end hardware.

## Decision 005 - GraphicsBuffer Path For Resident Drawing
Problem: Per-renderer material values and CPU property writes fragment batches and create GC/driver pressure.
Solution: Added `StructuredBuffer<H8UberNoirInstanceData>` carrying object/world matrices plus seed/fade/flags, gated by `H8_UBERNOIR_USE_INSTANCE_BUFFER`.
Rejected Alternatives: MaterialPropertyBlock was rejected as a hot-path batch breaker; direct dependency on a future drawer class was rejected because 20+ agents are working in parallel.
Scalability potential: Low devices use the same binding path with cheaper material math. Ultra devices can push more resident instances and per-instance seed variation without material clones.
Hardware Impact: Estimated i3/MX350 benefit is 20-80 us CPU in dense resident draws, pending Profiler proof.

## Decision 006 - Fake-First Caustics, Bending, Rust, And Biolum
Problem: Physically correct refraction, corrosion growth, hull deformation, and spectral emission would exceed the 0.1 ms suspicion threshold.
Solution: Use controlled cinematic cheats: analytical caustic waves with optional lookup texture, shader vertex bowing from stress fields, 16-tap rust POM only in high tiers, and phase-driven spectral emission.
Rejected Alternatives: Screen-space fluid/refraction simulation, CPU mesh deformation, decal stacks, and script-animated emission were rejected as slower and less predictable.
Scalability potential: Low disables all overkill. Middle can retain tint/detail. High/Ultra can spend ALU/texture budget where the camera sees it.
Hardware Impact: Low-end avoids 80-500 us GPU pressure from heavy material branches; Ultra intentionally spends those cycles on visible surface richness.

## Decision 007 - NaN And Texture Stall Discipline
Problem: High-contrast lighting and POM can poison the frame with NaNs or repeated texture stalls if raw math and repeated ORM fetches are used.
Solution: Wrapped all `pow()`/`rsqrt()` calls in safe helpers and sampled `_MaskMap` exactly once before distributing metallic/occlusion/smoothness.
Rejected Alternatives: Raw HLSL intrinsics and separate M/R/O texture samples were rejected as fragile and bandwidth-wasteful.
Scalability potential: Low uses the single packed ORM sample and avoids POM. Ultra keeps the packed ORM discipline while adding rust-detail taps only where the tier allows it.
Hardware Impact: Estimated i3/MX350 savings from packed ORM discipline is 10-60 us GPU in material-heavy views; NaN guards are correctness insurance, not a measurable win.

## Decision 008 - Compile Verification Boundary
Problem: Unity batchmode could not complete because the project has existing `GroundPenetratingRadarRuntime.cs` compile errors in the World/GPR domain.
Solution: Ran Unity 6000.4.1f1 batchmode, scanned the log for owned shader/C# names and shader errors, and marked Task 15 as blocked by dependency rather than editing another domain.
Rejected Alternatives: Fixing World/GPR from the rendering task was rejected as domain breach; claiming a clean compile was rejected as fake reporting.
Scalability potential: None until the external compile blocker is removed. The shader remains tiered and static-audited.
Hardware Impact: 0 us direct gain. This prevents introducing unverified cross-domain edits on low-end targets.

## Decision 009 - Polish Mandate Missing
Problem: The required post-core `<POLISH_MANDATE>` tag is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Mark the polish phase as blocked by batch-file input, record the absence, and keep the implemented shader under static anti-bloat checks instead of inventing a hidden mandate.
Rejected Alternatives: Parsing unrelated neighboring batch content or fabricating polish requirements was rejected as prompt contamination.
Scalability potential: Existing Low/Middle/High/Ultra gates remain intact; no additional polish commands can be executed without the missing tag.
Hardware Impact: 0 us runtime gain. The decision prevents unscoped churn.

## Decision 010 - Low-Tier UberNoir Cost Collapse
Problem: `_MATH_LOD_LOW` still paid for normal-map sampling, shadow-coordinate setup, view half-vector/specular math, and unconditional blue-noise evaluation even though the low path only needs base albedo, packed roughness/occlusion, ambient, and main diffuse.
Solution: Moved low-tier surface sampling into an early base+ORM-only path, changed low-tier lighting to `GetMainLight()` without shadow coordinates/specular/caustics, and gated blue-noise dither out of `_MATH_LOD_LOW`.
Rejected Alternatives: Keeping a common fragment body was rejected because it hid discarded work on MX350. Full material split was rejected because it reintroduces SetPass/material fragmentation.
Scalability potential: Low/TOASTER now buys stability with fewer samples and less ALU. High/Ultra retain normal maps, rust POM, caustics, spectral emission, and exact normals.
Hardware Impact: Estimated low-end gain is 30-200 us GPU across dense material views, pending Unity/Profiler proof after the external compile blocker is removed.

## Decision 011 - Rust And Caustic Texture Stall Gates
Problem: Clean materials sampled `_RustDetailMap` before proving rust was active, and all non-low variants compiled optional caustic texture sampling.
Solution: Added a rust scalar early-out before rust detail sampling and wrapped `_HectonCausticsMap` sampling behind `H8_UBERNOIR_CAUSTICS_TEXTURED`.
Rejected Alternatives: Always sampling rust/caustics and trusting runtime multipliers was rejected because zero contribution still consumes texture bandwidth and contributes to variant pressure.
Scalability potential: Low skips both paths. Middle/High can run procedural caustics only. Ultra/GOD can opt into texture caustics and 16-tap rust POM when visual payoff is visible.
Hardware Impact: Estimated MX350 gain is 10-90 us on clean materials and 5-40 us where procedural caustics replace a texture lookup, pending GPU capture.

## Decision 012 - Materials Asmdef H-Phi Cleanup
Problem: `Hecton8.Graphics.Materials.asmdef` referenced `Hecton8.World.Contracts`, but the current materials code uses only `Hecton8.Core`, `Hecton8.Core.Signals`, and Unity shader APIs.
Solution: Removed the unused World contract reference to reduce false domain coupling.
Rejected Alternatives: Leaving the reference "just in case" was rejected because H-Phi tracks architectural coupling as debt, and future world dependencies should enter through explicit signals/contracts.
Scalability potential: Runtime path unchanged; compile graph remains narrower for rendering materials.
Hardware Impact: 0 us runtime. Static H-Phi audit after the no-rebuild pass reported `RuntimeHPhiNarrow=0.010534799` and `RuntimeHPhiRisk=0.000573240`.

## Decision 013 - Low-Tier Caustic Publisher Gate
Problem: Low-tier caustic compute resources were correctly released, but `AnalyticalCausticsService` could still publish nonzero `_HectonProjectedCausticsParams.x`, allowing procedural caustics in CoreLit consumers that do not compile the UberNoir low variant.
Solution: Thread the `lowTier` decision into `PublishShaderGlobals` and force caustic intensity to zero when low tier or disabled depth is active.
Rejected Alternatives: Shader-only stripping was rejected because global shader parameters are shared by multiple consumers; leaving intensity active after resource release was rejected as an invisible low-tier GPU tax.
Scalability potential: Low/TOASTER gets no caustic ALU or texture pressure from global consumers. Middle/High/Ultra retain analytical caustics when budget and scene conditions justify them.
Hardware Impact: Estimated i3/MX350 gain is 15-80 us GPU in water/hull views with caustic receivers, pending GPU capture after the external compile blocker is removed.

## Decision 014 - Binary Layout And Native Lifetime Hardening
Problem: Caustic GPU upload data, black-box telemetry entries, and the AUP culling job payload were structurally important but relied on implicit managed layout, and disposed NativeArray scratch handles were not reset to default.
Solution: Added explicit sequential pack/size to caustic GPU and telemetry structs, explicit sequential layout to the AUP shift job payload, and default-reset disposed NativeArray fields after release.
Rejected Alternatives: Relying on C# default layout was rejected because H-Phi rewards binary-safe rendering code and GPU upload payloads must be deterministic; allocation churn was rejected in favor of keeping existing persistent NativeArrays.
Scalability potential: Low through Ultra share the same predictable upload and black-box memory model. Higher tiers can push more waves/instances without adding managed allocations or layout ambiguity.
Hardware Impact: Runtime microsecond gain is 0 us claimed. The benefit is lower integration risk and cleaner H-Phi memory-alignment evidence; latest no-rebuild audit reports `MemoryAlignment=0.504761905` and `AupPrecisionRisk=0`.

## Decision 015 - Instance Buffer Zero-Count Guard
Problem: The UberNoir resident-drawer path could read `_H8UberNoirInstanceData[bufferOffset]` when the instance buffer keyword was compiled but `_UberNoirInstanceParams.y` was zero or `_UberNoirInstanceParams.z` disabled the path.
Solution: Build a default Unity matrix instance first, then only index the `StructuredBuffer` when the use flag is set and the declared count is positive.
Rejected Alternatives: Assuming the drawer always binds a non-empty buffer was rejected because disabled variants, editor import, and fallback materials must not perform undefined GPU reads.
Scalability potential: Low/Middle fallback materials render safely without a resident buffer. High/Ultra resident batches keep the same fast path when the buffer is valid.
Hardware Impact: Estimated cost is 0-2 us vertex-side branch overhead in fallback cases; benefit is avoiding undefined GPU memory reads and hard-to-triage platform instability on Vulkan/Metal/DX12.

## Decision 016 - No-Rebuild H-Phi Evidence Boundary
Problem: The user explicitly forbade dotnet rebuilds, but the final shader safety patch changed source state after the previous H-Phi reading.
Solution: Re-ran only the static `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` audit with a longer timeout after the first 120-second attempt expired.
Rejected Alternatives: Running `dotnet build` or Unity compilation was rejected because it violates the direct user order and remains blocked by external World/GPR compile errors.
Scalability potential: Static audit confirms `AupPrecisionRisk=0`, `UnityUpdateMethods=2`, and increased `StructLayoutAttributes=953`; runtime scalability still requires profiler capture after the external compile blocker is cleared.
Hardware Impact: 0 us runtime. Evidence quality improved; latest static audit reports `RuntimeHPhiNarrow=0.010750800`, `RuntimeHPhiRisk=0.000587147`, and `MemoryAlignment=0.504761905`.

## Decision 017 - Dynamic Resolution Hysteresis And Asset Mutation Guard
Problem: `ThermalDynamicResolutionAdapter` could react to a single pressure frame and mutate the active URP asset `renderScale`/`upscalingFilter` as a fallback, creating state flicker and ScriptableObject/project-setting dirtiness risk.
Solution: Added a 3-frame pressure hysteresis and 15-frame recovery hysteresis, packed counters into existing telemetry reserve bits, removed runtime upscaling-filter mutation, and changed the fallback path to resize scalable buffers without writing URP asset fields.
Rejected Alternatives: Leaving instant scale flips was rejected by the state hysteresis mandate. Mutating the URP asset in code was rejected because the project requires authored pipeline assets and no runtime project-setting drift.
Scalability potential: Low/MX350 sheds resolution only after sustained pressure and recovers slowly. High/Ultra keep full scale unless real sustained pressure appears, preserving visual stability.
Hardware Impact: Estimated gain is 5-40 us of avoided render-state churn/jitter during unstable pressure windows; measured proof absent until profiler capture.

## Decision 018 - Global Flora Tint Finite And Play-Mode Guard
Problem: Serialized flora tint/strength values could become non-finite and then be published as global shader vectors; the bridge could also attempt runtime registry registration outside play mode.
Solution: Sanitize tint and strength before `Shader.SetGlobalVector`, preserve the cached-change early-out, and guard registry registration with `Application.isPlaying`.
Rejected Alternatives: Trusting inspector data was rejected because rendering globals feed many materials and NaN propagation is a project-level failure mode. Edit-mode registry side effects were rejected because shader preview globals do not require runtime tick ownership.
Scalability potential: All tiers receive stable globals; the visual tint fake remains cheap on low hardware and usable as biome richness on high hardware.
Hardware Impact: 0 us speed claimed. Correctness gain is preventing shader global poisoning and edit-mode runtime-registry noise.

## Decision 019 - Post-Follow-Up H-Phi Reverification
Problem: The DRS/flora safety pass changed rendering source state after the previous H-Phi measurement, while the user explicitly forbade dotnet rebuilds.
Solution: Reran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` with a 300s timeout and recorded the latest static-source metrics.
Rejected Alternatives: Running `dotnet build`, `dotnet rebuild`, or a Unity compilation was rejected because it violates the direct user order and remains blocked by external World/GPR compile errors. Reusing stale H-Phi numbers was rejected as fake evidence.
Scalability potential: Low-tier rendering now keeps hysteretic DRS behavior and finite shader globals; High/Ultra keep the overkill path without one-frame scale oscillation or shader global NaN risk.
Hardware Impact: 0 us runtime claimed from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010750800`, `RuntimeHPhiRisk=0.000587147`, `AllSourceHPhiNarrow=0.009572479`, `AllSourceHPhiRisk=0.000482295`, `ArchitecturalPurity=0.996447602`, `MemoryAlignment=0.504761905`, `StructLayoutAttributes=954`, `AupPrecisionRisk=0`.

## Decision 020 - Underwater Visuals Component Lookup Hygiene
Problem: `HectonUnderwaterVisuals` carried runtime `GetComponent<T>` and `GetComponentInParent<T>` lookup debt in camera recovery paths, increasing H-Phi static coupling risk and violating the preferred `TryGetComponent` pattern.
Solution: Replaced runtime camera/component probes with `TryGetComponent(out T)` and added a zero-allocation parent `Transform` walk that preserves first-parent-camera semantics without `GetComponentInParent<T>`.
Rejected Alternatives: A full camera-stack rewrite was rejected because this presentation hub owns Crest fallback, editor preview, gameplay camera composition, and underwater ownership. Editing Crest wrappers or moving camera ownership to another domain was rejected as higher-risk cross-domain churn.
Scalability potential: Low/MX350 avoids extra Unity lookup debt during cold camera recovery and keeps the same visual ownership path. High/Ultra remain behavior-equivalent while cleaner static coupling leaves room for later RenderGraph/camera-stack work.
Hardware Impact: Runtime speed gain is estimated at 0-5 us on rare camera recovery frames, not hot path. Static H-Phi evidence after the pass: `GetComponentCalls=532`, `MemoryAlignment=0.505023797`, `AupPrecisionRisk=0`.

## Decision 021 - Flashlight Voxel Shadow Provider Lookup And Native Handle Hygiene
Problem: `HectonFlashlightVoxelShadowProvider` still used `GetComponent<PlayerFlashlight>()` in cold setup/retry paths and left disposed native volume handles non-default after release.
Solution: Resolve the required flashlight through `TryGetComponent(out _flashlight)` and reset `_occupancyVolume` / `_sdfVolume` to `default` immediately after unregistering and disposing them.
Rejected Alternatives: Rewriting the voxel SDF algorithm or moving its buffers to a new vault owner was rejected because the existing provider is small, bounded, and already registers native allocations with `NativeMemorySentinel`; a vault migration needs a separate integration ticket.
Scalability potential: Low/MX350 keeps the same 12-20 voxel-resolution clamp and incremental slice refresh. High/Ultra keep the same visual fake, with cleaner cold-path lookup and safer long-session native handle state.
Hardware Impact: Estimated runtime gain is 0-2 us on rare flashlight component recovery frames; no steady-state Tick gain claimed. Latest static audit: `GetComponentCalls=530`, `NativeArrayRefs=7001`, `DataSovereignty=0.021386637`, `AupPrecisionRisk=0`.

## Decision 022 - Presentation UI Component Lookup Hygiene
Problem: Multiple clean Echelon 8 UI setup scripts still used bounded `GetComponent<T>`, `GetComponentInParent<T>`, or `GetComponentInChildren<T>` calls in cold setup/retry paths, keeping H-Phi lookup debt high without adding useful behavior.
Solution: Replaced those calls with `TryGetComponent(out T)` where the target is on the same object, and with explicit parent/child `Transform` walks where first-parent or active-child semantics were required.
Rejected Alternatives: A broad UI framework rewrite was rejected because several UI files are being edited by other agents and many generated UI builders still need a separate ownership pass. Registry-only lookup was rejected for local required components because it would add global coupling for component relationships Unity already owns.
Scalability potential: Low/MX350 gets lower cold-start/recovery lookup pressure and cleaner static coupling. Middle/High/Ultra retain identical UI construction, PDA focus, visor mesh, localization autosize, and pause-menu behavior.
Hardware Impact: Estimated runtime gain is 0-10 us on cold UI setup/recovery frames, not a steady-state Tick win. Static H-Phi improved `GetComponentCalls` from 530 to 503, `MemoryAlignment` from 0.505023797 to 0.506081438, and `ArchitecturalPurity` to 1.

## Decision 023 - No-Rebuild H-Phi UI Reverification
Problem: The presentation lookup pass changed source state after the previous H-Phi reading, while the user explicitly forbade dotnet rebuilds.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg` checks, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, or Unity rebuild/import was executed.
Rejected Alternatives: Running a build was rejected because it violates the direct user order and the known external World/GPR compile blocker still exists. Reusing stale H-Phi metrics was rejected as fake evidence.
Scalability potential: Low-tier presentation now has less startup/recovery lookup debt, while high-tier visual systems keep their overkill rendering features unchanged.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.01082338`, `RuntimeHPhiRisk=0.00060621`, `AllSourceHPhiNarrow=0.009634899`, `AllSourceHPhiRisk=0.000495259`, `GetComponentCalls=503`, `UnityUpdateMethods=0`, `StructLayoutAttributes=957`, `AupPrecisionRisk=0`.

## Decision 024 - Diegetic PDA And Relay Lookup Consolidation
Problem: After the first UI pass, the remaining clean Presentation & UX lookup debt was concentrated in PDA shell/panel setup, relay HUD fail-safe construction, settings preview camera recovery, suit advisory binding, and UI particle setup.
Solution: Replaced same-object probes with `TryGetComponent(out T)`, parent probes with bounded parent walks, and descendant probes with the existing `ComponentReferenceUtility.ResolveOwnedComponent<T>` instead of adding another local traversal utility.
Rejected Alternatives: Duplicating generic descendant traversal helpers was rejected after H-Phi showed helper code added source debt. Rewriting large generated PDA/menu builders was rejected because that is higher-risk than the remaining local setup probes and several UI owners are active in parallel.
Scalability potential: Low/MX350 removes more cold setup/recovery lookup pressure in diegetic UI and HUD fail-safe paths. High/Ultra keep the same PDA RT, cursor, relay marker, settings preview, and suit advisory behavior while reducing static coupling debt.
Hardware Impact: Estimated runtime gain is 0-10 us on cold setup/recovery frames only. Static `GetComponentCalls` improved from 503 to 481 after this pass.

## Decision 025 - No-Rebuild Second UI H-Phi Reverification
Problem: The second UI lookup pass changed source state after the `12:38:17` H-Phi run.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg`, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, Unity compile, or Unity import was executed.
Rejected Alternatives: Running a build was rejected by the direct no-rebuild order. Expanding into non-presentation domains was rejected by the domain boundary and active multi-agent churn.
Scalability potential: Low-tier UI/visor/PDA setup has less hierarchy lookup debt; high-tier rendering and visual overkill features remain unchanged.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010821867`, `RuntimeHPhiRisk=0.000610985`, `AllSourceHPhiNarrow=0.009633634`, `AllSourceHPhiRisk=0.000498924`, `GetComponentCalls=481`, `UnityUpdateMethods=0`, `StructLayoutAttributes=957`, `AupPrecisionRisk=0`.

## Decision 026 - Procedural Overlay Lookup Consolidation
Problem: Boot, death dump, subtitle, builder status, and temporary debug overlays still used `GetComponent<T>` in procedural UI construction and canvas fallback paths, leaving avoidable H-Phi lookup debt in Echelon 8.
Solution: Replaced same-object and freshly-created-object probes with `TryGetComponent(out T)`, added null-safe canvas fallback recovery, and kept all generated hierarchy, TMP registration, tick cadence, and visual styling unchanged.
Rejected Alternatives: Rewriting the procedural UI builders was rejected because it would add churn while other agents own adjacent UI. Moving overlay resolution into a global registry was rejected because these are local component relationships, not cross-domain services.
Scalability potential: Low/MX350 gets lower cold construction/recovery lookup debt and no additional allocations. Middle/High/Ultra retain identical boot/death/subtitle/debug visual presentation, leaving shader and post budget untouched for overkill visuals.
Hardware Impact: Estimated runtime gain is 0-10 us on cold UI construction/recovery frames only; no steady-state Tick win claimed. Static `GetComponentCalls` improved from 481 to 448.

## Decision 027 - No-Rebuild Third UI H-Phi Reverification
Problem: The procedural overlay lookup pass changed source state after the `12:55:12` H-Phi run.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg`, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, Unity compile, or Unity import was executed.
Rejected Alternatives: Running a build was rejected by the direct no-rebuild order. Editing World/GPR or gameplay files was rejected by the Echelon 8 domain boundary and known external compile blocker.
Scalability potential: Low-tier UI construction remains cheap and zero-GC styled; high-tier visual systems keep the same Hecton-OS/death-dump/subtitle polish without extra per-frame cost.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010671906`, `RuntimeHPhiRisk=0.000607563`, `AllSourceHPhiNarrow=0.009509931`, `AllSourceHPhiRisk=0.000496385`, `GetComponentCalls=448`, `UnityUpdateMethods=0`, `StructLayoutAttributes=960`, `AupPrecisionRisk=0`.

## Decision 028 - Pause And PDA Tab Lookup Consolidation
Problem: Pause controls and multiple PDA tab builders still carried `GetComponent<T>` / `GetComponentInParent<T>` calls in cold construction and owner-resolution paths.
Solution: Replaced local component probes with `TryGetComponent(out T)` and replaced parent lookups with explicit bounded `Transform` walks that preserve nearest-parent semantics without Unity hierarchy search APIs.
Rejected Alternatives: Rewriting the PDA tab framework was rejected as high-churn during parallel UI work. Adding a new shared Core helper was rejected because this pass can stay inside Echelon 8 without changing cross-domain APIs.
Scalability potential: Low/MX350 gets lower cold tab construction/recovery lookup debt. Middle/High/Ultra keep identical PDA atlas/data-log/barter/construction/controls/loadout visuals and pause controls behavior.
Hardware Impact: Estimated runtime gain is 0-10 us on cold tab construction/recovery frames only. Static `GetComponentCalls` improved from 448 to 416; `RuntimeHPhiRisk` moved from `0.000607563` to `0.000610856` because the parent-walk code adds source lines, so this is recorded as lookup hygiene rather than a full scalar-score win.

## Decision 029 - No-Rebuild Fourth UI H-Phi Reverification
Problem: The pause/PDA lookup pass changed source state after the `13:27:58` H-Phi run.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg`, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, Unity compile, or Unity import was executed.
Rejected Alternatives: Running a build was rejected by the direct no-rebuild order. Expanding to dirty files owned by other agents was rejected by the parallel-execution rule.
Scalability potential: Low-tier tab setup remains cheaper and deterministic; high-tier diegetic PDA/pause presentation remains visually unchanged with no extra per-frame cost.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010671906`, `RuntimeHPhiRisk=0.000610856`, `AllSourceHPhiNarrow=0.009509931`, `AllSourceHPhiRisk=0.000498829`, `GetComponentCalls=416`, `UnityUpdateMethods=0`, `StructLayoutAttributes=960`, `AupPrecisionRisk=0`.

## Decision 030 - Late Large UI Owner Lookup Consolidation
Problem: After the tab pass, the remaining clean runtime UI lookup debt was concentrated in `PauseMenuController`, `PDAShellChrome`, and `SettingsManager`; these are broad owners, so careless edits could break menu or PDA boot behavior.
Solution: Kept edits mechanical: local/self lookups now use `TryGetComponent(out T)`, parent camera/PDA/volume/canvas discovery uses explicit `Transform` walks, and generated menu/PDA construction stays behavior-equivalent.
Rejected Alternatives: Editing editor-only fallback scans was rejected because it does not affect runtime H-Phi. Rewriting pause/PDA ownership was rejected as a refactor loop during parallel agent work.
Scalability potential: Low/MX350 gets lower cold pause/PDA/settings recovery debt. Middle/High/Ultra keep the same premium diegetic shell, pause menu, controls, settings preview, and PDA visual behavior.
Hardware Impact: Estimated runtime gain is 0-10 us on cold menu/shell/settings recovery frames only. Static `GetComponentCalls` improved from 416 to 384; full-source risk scores also include concurrent checkpoint changes from other agents.

## Decision 031 - No-Rebuild Fifth UI H-Phi Reverification
Problem: The large UI owner lookup pass changed source state after the `13:32:15` H-Phi run.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg`, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, Unity compile, or Unity import was executed.
Rejected Alternatives: Running a build was rejected by the direct no-rebuild order. Claiming runtime readiness from static evidence was rejected by AGENTS.
Scalability potential: Low-tier runtime UI recovery avoids the last large avoidable lookup cluster; high-tier presentation remains unchanged and can spend budget on actual visual richness rather than setup churn.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010752435`, `RuntimeHPhiRisk=0.000618924`, `AllSourceHPhiNarrow=0.009581932`, `AllSourceHPhiRisk=0.00050517`, `GetComponentCalls=384`, `UnityUpdateMethods=0`, `StructLayoutAttributes=962`, `AupPrecisionRisk=0`.

## Decision 032 - Root PDA Menu Progression And VFX Lookup Consolidation
Problem: After the large UI-owner pass, safe remaining Echelon 8 lookup debt lived in root-level PDA/menu/localization/save-thumbnail installers, progression/narrative presentation installers, marker HUD pooling, and VFX/celestial camera binding.
Solution: Replaced same-object probes with `TryGetComponent(out T)`, replaced parent camera/PDA/player lookups with bounded `Transform` walks, and preserved generated UI, VFX binding, marker pooling, localization, and camera-follow behavior.
Rejected Alternatives: Editing cross-domain tool/World/physics lookup debt was rejected by domain boundary. Replacing editor-only Crest fallback scans was rejected because those are editor-only and type-name based. A shared parent-walk utility was rejected because adding a new Core API for this local cleanup would increase integration surface.
Scalability potential: Low/MX350 presentation setup avoids more Unity hierarchy lookup debt. Middle/High/Ultra keep identical PDA inventory, marker HUD, main menu, save thumbnail, camera juice, marine snow, sky follow, and celestial observer behavior.
Hardware Impact: Estimated runtime gain is 0-10 us on cold presentation setup/recovery frames only. Static `GetComponentCalls` improved from 384 to 321; scoped runtime presentation scan leaves only the editor-only Crest fallback in `HectonUnderwaterVisuals`.

## Decision 033 - No-Rebuild Sixth Presentation H-Phi Reverification
Problem: The root PDA/menu/VFX lookup pass changed source state after the `14:11:53` H-Phi reading, while the active instruction still forbids dotnet rebuilds.
Solution: Ran only `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, scoped `rg`, brace counts, and `git diff --check`. No `dotnet build`, `dotnet rebuild`, Unity compile, or Unity import was executed.
Rejected Alternatives: Running a build was rejected by the direct no-rebuild order and the known external World/GPR compile blocker. Reusing stale H-Phi numbers was rejected as fake evidence.
Scalability potential: Low-tier UI/VFX binding is cleaner and cheaper on cold paths; high-tier visual systems retain the same overkill presentation behavior.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010755694`, `RuntimeHPhiRisk=0.000626365`, `AllSourceHPhiNarrow=0.009584727`, `AllSourceHPhiRisk=0.000510846`, `FindObjectCalls=0`, `GetComponentCalls=321`, `UnityUpdateMethods=0`, `StructLayoutAttributes=962`, `AupPrecisionRisk=0`.

## Decision 034 - Diegetic Panel Phosphor Low-Tier Gate
Problem: `DiegeticPanelController` still ran a blit-backed phosphor persistence fake when enabled, even though low/MX350 tiers should spend terminal budget on legibility and direct RT output rather than a history-buffer CRT afterimage.
Solution: Added a low-tier/unknown/low-memory gate around phosphor resource allocation, late-frame registration, material output selection, and composite execution. Low tiers release the phosphor history buffers and render the direct panel texture; high tiers retain the CRT persistence fake.
Rejected Alternatives: A full MonoBehaviour-to-RenderGraph migration was rejected inside this pass because the current phosphor composite is tied to a per-panel RT lifecycle, and a correct migration needs a renderer-feature owner plus Frame Debugger/RenderGraph Viewer proof. Leaving the blit path active on MX350 was rejected as unnecessary visual tax.
Scalability potential: Low/TOASTER uses the cheapest readable terminal surface. Middle/High/Ultra keep the richer CRT persistence where the extra RT bandwidth is a visual purchase, not baseline cost.
Hardware Impact: Estimated 20-120 us GPU/RT bandwidth avoided in active terminal views on low-tier hardware pending capture. Static H-Phi cannot prove the GPU win; it records the no-rebuild source state only.

## Decision 035 - No-Rebuild Seventh Presentation H-Phi Reverification
Problem: The phosphor LOD gate changed Presentation & UX source after the `15:17:41` H-Phi pass.
Solution: Reran static H-Phi and scoped render-debt scans only. No `dotnet build`, `dotnet rebuild`, Unity import, or Unity player build was executed.
Rejected Alternatives: Claiming RenderGraph completion was rejected because the remaining `Graphics.Blit` is still present for high-tier phosphor persistence and needs a separate renderer-feature migration. Running Unity verification was rejected by the no-rebuild order and external compile blocker.
Scalability potential: Low/MX350 now avoids the phosphor history buffer and blit-backed persistence; High/Ultra retain the effect. Broader RenderGraph `AddUnsafePass` debt remains visible and should be handled as a feature-level migration, not a local MonoBehaviour patch.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010787439`, `RuntimeHPhiRisk=0.000634336`, `AllSourceHPhiNarrow=0.009611624`, `AllSourceHPhiRisk=0.00051719`, `FindObjectCalls=0`, `GetComponentCalls=321`, `LinqSurface=3`, `ManagedFormatSurface=564`, `PrimaryManagedRuntimeRisk=177`, `MemoryAlignment=0.506309148`, `StructLayoutAttributes=963`, `AupPrecisionRisk=0`.

## Decision 036 - Visor Fullscreen Blit RenderGraph Migration
Problem: The visor renderer still carried many first-party fullscreen `AddUnsafePass` blocks whose only job was a material blit from one graph texture to another. That keeps Unity 6 RenderGraph dependencies less explicit and violates the project preference for graph utility blits when no custom command-buffer work is required.
Solution: Converted the simple fullscreen chains to `RenderGraphUtils.AddBlitPass`: atmosphere soot, VR brownout, retina distortion, BIOS diagnostic, scanner projection, noir depth fog, visor fluid distortion, deferred decal composite, reflection sheen mask/composite, biolum SSGI composite, half-res particles composite, sonar history/world/composite, abyssal SSDO gather/blur/composite, and underwater noir shafts/blur/composite. Depth, history, half-res, occlusion, and exposure-buffer dependencies are still declared through returned `IBaseRenderGraphBuilder` handles.
Rejected Alternatives: A blanket rewrite of all remaining unsafe passes was rejected. Dry-volume needs stencil/depth writes in one sequence, holographic edge uses custom renderer draws, and fluid advection is a compute bridge with cross-domain binding helpers. Rewriting those without Frame Debugger/RenderGraph Viewer proof would be architectural theater.
Scalability potential: Low/MX350 gets less manual native-command-buffer surface in fullscreen post chains and clearer graph visibility for dependency pruning. Middle/High/Ultra keep the same noir/visor/sonar/SSDO/shaft visuals while the graph owns the blit plumbing.
Hardware Impact: Estimated 5-60 us CPU/render-graph scheduling hygiene in heavy visor stacks pending capture. Scoped Visor `AddUnsafePass` count reduced from 28 to 4. Remaining 4 are intentionally documented, not hidden.

## Decision 037 - No-Rebuild Eighth Presentation H-Phi Reverification
Problem: The RenderGraph blit migration changed many visor render features after the phosphor H-Phi pass, while the user explicitly forbade dotnet rebuilds.
Solution: Reran only static H-Phi, scoped `rg`, brace counting, and `git diff --check`. No `dotnet build`, no `dotnet rebuild`, no Unity import, and no player build were executed.
Rejected Alternatives: Running a compiler was rejected by user order and the external World/GPR blocker. Reporting the pass as a full H-Phi scalar improvement was rejected because `RuntimeHPhiRisk` moved slightly upward even though primary managed risk improved.
Scalability potential: Low tier now relies on official RenderGraph utility blits for the cheap visor fullscreen stack, while high/ultra preserve layered noir features and the overkill post chain.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010787439`, `RuntimeHPhiRisk=0.000636091`, `AllSourceHPhiRisk=0.000518488`, `FindObjectCalls=0`, `GetComponentCalls=321`, `ManagedFormatSurface=534`, `PrimaryManagedRuntimeRisk=147`, `AupPrecisionRisk=0`.

## Decision 038 - Holographic Edge RasterGraph Migration
Problem: `HectonHolographicEdgeFeature` still used `AddUnsafePass` solely to issue custom scan-renderer draws, which kept a native command-buffer unwrap in the Visor stack.
Solution: Added a raster-command overload to `HectonScanRenderRegistry.DrawRenderers` and recorded the edge pass through `AddRasterRenderPass`, with color/depth attachments declared through RenderGraph.
Rejected Alternatives: Leaving the native unwrap was rejected because Unity 6 `IRasterCommandBuffer.DrawRenderer` covers the required operation. Moving scan registration into a new renderer system was rejected as unnecessary cross-domain churn.
Scalability potential: Low tier keeps the same cheap edge mask path with graph-visible dependencies. High/Ultra keep the stylized holographic edge draw while removing one unsafe scheduling island.
Hardware Impact: Estimated 1-10 us CPU/render-graph scheduling hygiene in scan-heavy views pending Frame Debugger capture; no visual algorithm change.

## Decision 039 - Fluid Advection ComputeGraph Migration
Problem: `HectonFluidAdvectionRenderFeature` used `AddUnsafePass` and native command-buffer access to dispatch a compute kernel that already had stable payload data.
Solution: Added `IComputeCommandBuffer` bind/unbind overloads in `HectonFluidEngine` and moved the feature to `AddComputePass`, importing graph textures for flow, SDF, and fallback empty SDF handles.
Rejected Alternatives: Keeping the bridge unsafe was rejected after project/package APIs proved compute buffer, texture, and dispatch methods exist on `IComputeCommandBuffer`. Rewriting the fluid solver was rejected as outside presentation RenderGraph debt.
Scalability potential: Low/MX350 keeps the same feature gating and compute payload size. High/Ultra keep the same fluid distortion/advection visuals with explicit graph ownership of texture dependencies.
Hardware Impact: Estimated 1-10 us CPU scheduling hygiene; GPU cost unchanged. The main gain is removing native-command-buffer surface from a compute bridge.

## Decision 040 - Dry Volume Explicit Raster/Blit Sequence
Problem: Dry-volume restore and underwater resolve bundled stencil writes, render-target switches, copies, fullscreen resolves, and stencil clears inside unsafe RenderGraph passes.
Solution: Split the logic into explicit graph-visible phases: raster stencil write, graph blit color copy, raster restore/resolve, and stencil clear. `DrawDryStencil` now accepts `IRasterCommandBuffer`, and the underwater resolve declares source/depth/composite resources directly.
Rejected Alternatives: A single unsafe pass with mid-pass `CoreUtils.SetRenderTarget` was rejected because it hides resource transitions from RenderGraph. Removing dry-volume stencil behavior was rejected because it is a core underwater/noir composition fake.
Scalability potential: Low tier keeps predictable dry-volume masking without extra simulation. High/Ultra keep cinematic dry interiors and underwater noir composition while RenderGraph sees the dependency chain.
Hardware Impact: Estimated 2-20 us scheduling/debuggability improvement pending Frame Debugger capture; GPU visual work is intentionally unchanged.

## Decision 041 - Visor Uber Post BlitPass Migration
Problem: `HectonVisorUberPostFeature` still suppressed obsolete RenderGraph warnings around `AddRenderPass<PassData>` and called `Blitter.BlitCameraTexture` for a plain fullscreen material pass.
Solution: Replaced the obsolete pass with `RenderGraphUtils.AddBlitPass` and retained the depth dependency through the returned builder when depthless TBDR mode is off.
Rejected Alternatives: Keeping the 0618 suppression was rejected because the pass has no custom command-buffer requirement. Removing the depth dependency was rejected because pressure, waterline, and wet-lens shader paths can depend on camera depth.
Scalability potential: Low/Quest-style depthless TBDR keeps the cheaper no-depth dependency path. Middle/High/Ultra keep the full visor post stack with graph-visible depth reads.
Hardware Impact: Estimated 1-5 us CPU/render-graph hygiene pending capture; no shader or material feature changed.

## Decision 042 - No-Rebuild Ninth Presentation H-Phi Reverification
Problem: The final RenderGraph pass changed Visor and fluid rendering source after the `21:16:28` H-Phi reading, while the active user order still forbids dotnet rebuilds.
Solution: Reran only static H-Phi, scoped `rg`, brace counting, and `git diff --check`. The scoped runtime Visor debt scan now reports zero project-owned `AddUnsafePass`, obsolete `AddRenderPass<`, native command-buffer unwraps, `CoreUtils.SetRenderTarget`, or `Blitter.BlitCameraTexture`.
Rejected Alternatives: Running a dotnet/Unity build was rejected by the direct user order and known external World/GPR compile blocker. Claiming runtime proof from static RenderGraph edits was rejected; Frame Debugger/Profiler proof remains pending a clean project compile.
Scalability potential: Low tier gets clearer graph pruning for cheap visor passes and no hidden unsafe scheduling islands. High/Ultra keep all noir, scanner, sonar, dry-volume, edge, and fluid visuals with explicit raster/compute/blit graph ownership.
Hardware Impact: 0 us runtime from the audit itself. Latest static audit: `RuntimeHPhiNarrow=0.010787439`, `RuntimeHPhiRisk=0.000636091`, `AllSourceHPhiNarrow=0.009611624`, `AllSourceHPhiRisk=0.000518488`, `FindObjectCalls=0`, `GetComponentCalls=321`, `ManagedFormatSurface=534`, `PrimaryManagedRuntimeRisk=147`, `AupPrecisionRisk=0`.

# Rationale_SHINOBU_235

Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE until Unity import, Console, Play Mode, Frame Debugger, Profiler/GCMonitor, and player artifacts exist.

## Decision 000 - Domain Intake

Problem: SHINOBU_235 must replace managed post-processing with a RenderGraph-native fullscreen pass without touching gameplay truth or unrelated systems.
Solution: Constrain work to Presentation/UX rendering files, shader assets, cold editor validation, and concise docs/log artifacts. Read Vault/quality services before adding adapters.
Rejected Alternatives: Reusing Unity Volume runtime profile mutation; adding PostProcessVolume wrappers; adding broad global registry surface before proving existing routes.
Scalability potential: Low uses single-pass tint, vignette, ACES, cheap noise; Middle adds controlled glitch and edge CA; High adds heavier grain/detail; Ultra spends saved cycles on visual-overkill pixel math without changing gameplay truth.
Hardware Impact: i3/MX350 expected gain is managed-GC avoidance and removal of standard volume/profile churn; exact microseconds pending profiler proof.

## Decision 001 - Mandate Selection

Problem: Rendering, DTO layout, Vault authority, and telemetry constraints overlap.
Solution: Loaded Zero-GC, ARM64 layout, URP RenderGraph, Noir shader, GPU binding, fake-first, telemetry, registry, and budget mandates before source edits.
Rejected Alternatives: Coding from prompt alone; relying on Unity default post stack doctrine from older docs where it conflicts with current batch assignment.
Scalability potential: Continuous GlobalQualityWeight will scale shader ALU and cadence from survival visuals to overkill.
Hardware Impact: Static-source only; expected reduction is avoiding per-frame string/material/volume work on low-end silicon.

## Decision 002 - Active Route Through Existing Renderer Feature

Problem: Adding a new ScriptableRendererFeature sub-asset manually would require fragile renderer YAML surgery and feature-map bytes while four renderer assets already own `HectonVisorUberPostFeature`.
Solution: Keep the existing serialized feature owner and add a `deepSeaNoirUnifiedPass` branch that bypasses the legacy material-param pass and emits one RenderGraph fullscreen pass bound to `NoirPostProcessDTO`.
Rejected Alternatives: Creating a second renderer feature not wired into renderer assets; deleting the legacy visor class; mutating renderer feature maps by guesswork.
Scalability potential: Low uses ACES, tint, vignette, cheap hash grain; Middle adds controlled block glitch; High adds chroma/detail grain; Ultra increases block/noise ALU and wave detail without changing DTO layout.
Hardware Impact: MX350 avoids Unity Volume evaluation and hot material parameter churn in the active route; exact microseconds pending Profiler/Frame Debugger proof.

## Decision 003 - Vault DTO Layout

Problem: Shader constants, input scalars, tuning, CSV profiles, and telemetry need one owner route without managed properties or rollback contamination.
Solution: Added explicit-layout DTOs in `DrsContracts.cs`: `NoirPostProcessDTO` 64 bytes, `NoirPostProcessInputDTO` 64 bytes, `NoirPostProcessTuningDTO` 64 bytes, `NoirTelemetryEntry` 64 bytes, `NoirColorProfileDTO` 64 bytes. Runtime validates offsets before allocating GPU/Vault buffers.
Rejected Alternatives: C# properties; classes; Shader.SetGlobalFloat lanes; VolumeProfile mutation; dictionary-based runtime tuning.
Scalability potential: One constant buffer lets GlobalQualityWeight scale ALU detail continuously while preserving a fixed GPU layout from weak devices to ultra hardware.
Hardware Impact: 64-byte CBuffer double buffer is predictable and cache-line aligned; i3/MX350 benefit is lower CPU dispatch overhead versus managed Volume and string/global param paths.

## Decision 004 - Physiology/Depth Source Boundary

Problem: Physiology Vault buffers are owned by `Hecton8.Physiology`, which references Core; Core cannot add a reverse asmdef reference without a cycle. `GlobalDataVault` enforces TypeHash, so mirror-typed reads of `ShinobuPhysiologyScalars` can fatal on type mismatch.
Solution: The post pass owns `NoirPostProcessInputDTO` in Vault, builds it from cached player/survival owner snapshots when available, then the Burst parameter job reads the Vault pointer. If no owner data exists, `GenerateMockPsychologicalStressJob` writes deterministic mock stress/depth into the same Vault buffer.
Rejected Alternatives: Referencing `Hecton8.Physiology` from Core; using `TryGetLatestCreated`; using mirror structs against typed Vault buffers; swallowing Vault type exceptions.
Scalability potential: Low/Middle still get plausible stress/depth visuals from deterministic mock or survival snapshots; High/Ultra spend shader ALU, not gameplay authority changes.
Hardware Impact: Avoids a fatal TypeHash route on low-end devices; cost is one scalar owner read before a 64-byte Vault write, pending profiler proof.

## Decision 005 - Single-Pass Visual Fake

Problem: Deep sea stress visuals can become expensive if treated as physical fog/refraction/deformation.
Solution: Shader uses ACES fitted curve, procedural hash grain, triangle/block glitch, single-axis chroma, and depth tint from scalar constants. No physical particles, no volume stack, no extra history pass in the active path.
Rejected Alternatives: URP PostProcessVolume; multi-pass reconstruction for this task; screen-space fog raymarch for stress; per-pixel physical pressure simulation.
Scalability potential: Low uses one source sample plus cheap grain; Middle uses block offsets; High/Ultra use extra chroma samples and higher-detail grain/glitch math.
Hardware Impact: Expected MX350 gain is fewer passes and no VolumeComponent profile work; exact GPU microseconds require Frame Debugger and Profiler.

## Decision 006 - Continuous ALU Shedding

Problem: The mandate forbids low/high binary quality switches and also requires branchless shader math. The initial stochastic quality gates avoided hardware-tier switches but still used dynamic `if` branches.
Solution: Replace shader-side stochastic branches with `step`/`lerp` masks driven by `GlobalQualityWeight`, stress, toxicity, and wrapped time. Low quality mathematically collapses high/detail contributions to zero while preserving one shader variant, one CBuffer layout, and no branch divergence.
Rejected Alternatives: `if (quality > 0.5)` tier branches; keeping stochastic `if` gates; always routing back to URP Volume ACES; separate shader variants that risk warmup churn.
Scalability potential: Low uses base ACES, tint, vignette, cheap grain, and zeroed high/detail masks; Middle ramps glitch/chroma masks; High/Ultra spend stronger grain/glitch/chroma math without changing gameplay truth or DTO layout.
Hardware Impact: MX350 avoids branch divergence and variant churn. Exact GPU microseconds require Frame Debugger and Profiler capture because static source cannot prove driver-level ALU elimination.

## Decision 007 - CSV Profiles Through Vault Arrays

Problem: Task requested CSV-driven color profiles in unmanaged storage, but the present `IDataVault` API exposes NativeArray-style creation/resolve paths, not a first-party NativeHashMap ownership route.
Solution: Parse `noir_color_grading_profiles.csv` cold with a byte cursor into `NativeArray<NoirColorProfileDTO>` and select profiles by hashed id/range during parameter calculation. This preserves one Vault owner route and avoids managed dictionaries.
Rejected Alternatives: Runtime ScriptableObject profiles; `string.Split`; managed `Dictionary`; inventing a custom NativeHashMap Vault lane not supported by the current interface.
Scalability potential: Low/Middle use the same table with cheaper effect scales; High/Ultra consume profile multipliers to buy denser grain/glitch/chroma visuals.
Hardware Impact: i3/MX350 pays cold file parse only; hot path reads fixed-size structs from unmanaged memory. Exact cold parse time pending Editor profiler.

## Decision 008 - Compile-Wall Polish Pass

Problem: Static audit found the active Noir branch still had compile-wall and hot-path debt: `Unity.Jobs` was missing from `Hecton8.Core.asmdef`, Burst flags were incomplete, BufferID constants were locally cast from integer contracts, and active frame code could cold-grow Vault handles or query `GlobalRegistry`.
Solution: Added central `BufferID` enum entries for reconstruction/noir buffers, added `Unity.Jobs` to the Core asmdef, upgraded both Noir jobs to `FloatMode.Fast` / `FloatPrecision.Standard`, registered `HectonVisorUberPostFeature` as an `IGlobalRegistryHotSwapListener`, cached Vault/Player/ResolutionScaler/Fluid services, changed active RenderGraph entry to readiness checks only, and resolved active Noir Vault handles through `IDataVault.TryResolveHandle` phase-local NativeArrays before passing raw pointers to Burst.
Rejected Alternatives: Leaving `AddRenderPasses` as an allocation fallback; polling GlobalRegistry from the active branch; persisting raw Vault pointers across phases; fixing the unrelated deleted Gameplay scanner file to force a green build.
Scalability potential: Low devices avoid surprise cold allocations during rendering; middle/high/ultra still consume the same continuous `GlobalQualityWeight` shader curve and fixed DTO layout without feature popping.
Hardware Impact: i3/MX350 impact is lower frame-time variance from avoiding cold Vault/CSV work in the RenderGraph path. Compile proof is blocked by `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs` missing from a stale/deleted Gameplay source reference.

## Decision 009 - Active Vault Lock Reduction

Problem: The active Noir pass still mutated GlobalDataVault lock counters every rendered camera frame for input, tuning, constants, telemetry, and color-profile selection. Those locks are intended for external scheduled pointer ownership, while the Noir jobs execute synchronously and do not outlive the method.
Solution: Resolve phase-local NativeArray views through `IDataVault.TryResolveHandle` and pass raw pointers only to immediate `IJob.Run()` calls. Telemetry writes now use the same phase-local view without a Vault lock. Color-profile lookup now caches the selected `NoirColorProfileDTO` and refreshes by a continuous quality-scaled cadence: 18 frames at low quality down to 2 frames near visual-overkill quality. Player survival/movement references are cached from cold/hot-swap rather than re-reading `IPlayerRuntimeContext.SurvivalSystem` every active frame.
Rejected Alternatives: Keeping per-frame lock/unlock mutations; storing persistent private NativeArrays; moving ACES back to URP Volume because an older noir aesthetics mandate says Volume owns tonemapping. The current SHINOBU_235 batch explicitly requires shader-side ACES and PostProcessVolume removal, so batch scope wins.
Scalability potential: Low devices avoid per-frame profile scans and lock metadata churn; middle/high/ultra refresh profile selection more frequently while preserving the same DTO layout and shader route.
Hardware Impact: i3/MX350 expected gain is lower CPU variance in the render update path: four active Noir lock/unlock pairs removed plus color-profile scan amortized. Exact microseconds require Unity Profiler/GCMonitor after the external compile blocker is fixed.

## Decision 010 - Branchless Single-Sample Chroma

Problem: Replacing shader branches with `step`/`lerp` masks removed divergence but initially left the chroma path paying two extra camera-color samples even when quality masked the effect to zero. That undermined the low-quality ALU/fill-rate claim.
Solution: Convert chroma to a Dear Lie channel-phase fake derived from the already-sampled source color and one hash value. Collapse grain detail to one hash plus arithmetic folding, and reuse existing block noise for glitch detail gating. The shader remains one pass, one source texture sample, no variants, no `_Time`, and no dynamic `if` branches.
Rejected Alternatives: True multi-tap chromatic aberration; runtime quality branches; separate low/high shader variants; dropping shader-side ACES.
Scalability potential: Low quality keeps one source sample, base ACES, vignette, one-hash grain, and zeroed chroma/glitch masks. Middle/High/Ultra increase visible glitch/chroma/detail strength through continuous masks without adding texture taps or changing DTO layout.
Hardware Impact: i3/MX350 static delta is two texture samples removed per fullscreen pixel and shader hash call sites reduced from seven to three. Exact GPU microseconds still require Frame Debugger/GPU profiler after compile/import is unblocked.

## Decision 011 - Report Artifact Preservation

Problem: The editor `Volume_Component_Inquisition` menu could regenerate `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with a weaker minimal schema, erasing the active feature, shader GUID, and hot-path correction proof already recorded by SHINOBU_235.
Solution: Extend the editor scanner output to include scopes, `managedPostProcessingEradicated`, shader path, active feature, shader GUID, current hot-path correction list, and the current external compile-blocker marker. The current JSON artifact also carries `managedPostProcessingEradicated=true` and `externalBlocker=true`.
Rejected Alternatives: Treating report degradation as harmless editor-only churn; leaving the CTO-facing artifact dependent on chat history or manual patching.
Scalability potential: Runtime unchanged. Better proof stability protects future low/mid/high/ultra validation work from losing the static route facts.
Hardware Impact: Runtime 0 us. Editor-only cold file scan/write.

## Decision 012 - Audit Patch: Tuner Churn And Late Player Context

Problem: The editor tuner graph wrote a formatted label every update, the scanner could still drop preserved report fields, and a same-instance player context initialized after `Create()` could leave the Noir input lane on mock data until a registry replacement event happened.
Solution: Preserve existing report member blocks when `Volume_Component_Inquisition` rewrites the JSON; hash the tuner readout after 0.001 quantization so the fixed-array graph can sample every editor update while the label allocates only on display changes; retry player reference binding through the already cached `IPlayerRuntimeContext` on a continuous `GlobalQualityWeight` cadence from 90 frames at low quality to 18 frames at high quality.
Rejected Alternatives: Per-frame `GlobalRegistry.Player` polling; adding a new core player-ready signal or interface method outside SHINOBU_235 ownership; leaving editor allocation churn under a strict zero-GC graph wording; overwriting compile-attempt evidence with a scanner-only placeholder.
Scalability potential: Low devices retry the late context rarely and continue deterministic mock visuals if the player owner is absent; middle/high/ultra bind faster without changing DTO layout or visual authority.
Hardware Impact: Runtime adds one integer frame gate while player refs are absent and no cost after binding. Editor label allocation rate falls from every update to quantized value changes; runtime unchanged.

## Decision 013 - Recovery And ABI Lane Hardening

Problem: A local recovery pass exposed two correctness risks: the canonical visor feature file had to be restored without trampling concurrent agent edits, and the Noir CBuffer writer had drifted from the shader ABI by placing toxicity and block-scale values in different lanes than the HLSL consumer expected. The immediate scalar jobs also used direct `Execute()` calls, bypassing the explicit `IJob.Run()` entry point expected by the Burst route.
Solution: Restore the main feature body from source control, keep all SHINOBU_235 Noir deltas in the partial `HectonVisorUberPostFeature.Noir.cs`, remove duplicate hot-swap ownership, invoke `GenerateMockPsychologicalStressJob` and `CalculateNoirParametersJob` through `Run()`, and document/fix the 64-byte CBuffer lane contract: `QualityAndLimits.z = toxicity`, `AberrationParams.z = block scale`.
Rejected Alternatives: Rewriting the whole visor feature again; reverting unrelated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` changes from other agents; keeping a shader/CPU lane mismatch and relying on visual inspection; creating a new sibling assembly to dodge the current Core monolith.
Scalability potential: Low devices now collapse block glitch scale from the intended block-scale lane while toxicity still drives grade/noir response; middle/high/ultra regain predictable visible overkill without changing DTO size or shader variants.
Hardware Impact: Runtime microseconds are not measured. Static impact is correctness and Burst route hardening: no duplicate hot-swap field, no managed direct job entry calls, no wasted ALU from reading toxicity as block scale.

## Decision 014 - Prompt-Literal ABI And Pure Player Snapshots

Problem: The interim CBuffer repair made CPU and shader agree, but it did not match the original SHINOBU_235 lane contract because `AberrationParams.z` carried block scale instead of Y offset. Noir also kept concrete `HectonSurvivalSystem` and `HectonPlayerMovement` references, and `IPlayerRuntimeContext` read accessors synced scene state.
Solution: Restore `AberrationParams` to prompt-literal semantics: chroma intensity, X offset amplitude, Y offset amplitude, vignette. The shader derives block scale from `GlobalQualityWeight` locally. Add pure cached movement/survival snapshot accessors to `IPlayerRuntimeContext`, make `PlayerRuntimeContextService` getters return cached refs without `SyncPlayerContext()`, and make Noir consume only snapshot DTOs through the cached context. The RenderGraph render function is now a static lambda.
Rejected Alternatives: Documenting the lane drift as acceptable; keeping direct player component reads for convenience; adding a new physiology dependency; letting getters mutate scene state during reads; leaving the capture-free lambda non-static.
Scalability potential: Low devices still collapse glitch offsets and block frequency through continuous shader math; middle/high/ultra regain stronger X/Y offset response without changing DTO size or gameplay authority.
Hardware Impact: Runtime microseconds are not measured. Static impact is lower hidden scene-sync risk, no active Noir concrete gameplay component refs, no block-scale CBuffer lane, and stricter RenderGraph delegate allocation posture.

## Decision 015 - Snapshot Depth Publisher And Active Transform Purge

Problem: After Noir stopped reading concrete movement/survival components, the movement snapshot publisher still collapsed depth to zero when `HectonSurvivalSystem` was absent. The Noir input builder also kept a `renderCamera.transform.position.y` fallback, which reintroduced scene-state reads into the active post-process path.
Solution: Publish movement depth from `HectonPlayerMovement.CurrentDepth` when survival depth is unavailable, then remove the active camera-transform fallback from `TryBuildNoirInputSnapshot`. A source snapshot with zero depth now remains a valid zero-depth fact; no scene transform is queried by the Noir route.
Rejected Alternatives: Keep camera Y as an inferred depth proxy; query `Transform` or gameplay components directly from the render path; add a new cross-domain depth dependency for this pass.
Scalability potential: Low devices keep deterministic mock visuals when no owner snapshot exists; middle/high/ultra consume the same owner-published scalar route and spend quality-weighted shader ALU only after the scalar route is established.
Hardware Impact: Runtime microseconds are not measured. Static impact is removing one active render-path scene transform read and preserving depth continuity when the survival owner is absent.

## Decision 016 - Noir Partial Compile-Wall Tightening

Problem: The SHINOBU_235 partial still imported `Hecton8.Physics` only to refresh the legacy fluid binding during GlobalRegistry hot-swap. It also carried an unused `Camera` parameter on `TryUpdateNoirConstants()` after the active input builder stopped reading camera transforms.
Solution: Remove the `Hecton8.Physics` import from `HectonVisorUberPostFeature.Noir.cs`, route FluidRuntime hot-swap through the existing cold `RefreshFluidBinding(force: true)` method owned by the canonical visor feature, and drop the unused `Camera` parameter from the Noir constants update path.
Rejected Alternatives: Keep a sibling-domain type reference in SHINOBU_235 partial code; duplicate fluid binding logic; leave dead camera parameters as harmless.
Scalability potential: Low/middle/high/ultra behavior is unchanged; this is compile-wall and route hygiene around the same GlobalQualityWeight-driven shader path.
Hardware Impact: Runtime microseconds are not measured. Static impact is zero active ALU change, lower domain coupling in the Noir partial, and one less parameter carried through the active render setup path.

## Decision 017 - Inquisition String-Parameter Counter

Problem: The CTO-facing scanner proved standard Volume removal, but Task 02 also requires evidence that SHINOBU post-effect scalar lanes are not driven through string-based material/global shader setters. The report lacked a separate counter for that class of residue.
Solution: Extend `Volume_Component_Inquisition` with `stringShaderParameterResidueCount` for chromatic/vignette/grain/glitch `SetFloat`/`SetVector` and `Shader.SetGlobal*` string patterns. Keep it scoped to SHINOBU post-effect lanes so unrelated heatmap/scatter material properties do not become false positives. Fix findings comma emission to use the shared findings buffer length rather than per-category counters. Preserve the `IJob.Run()` hot-path correction in the generator so rerunning the menu does not degrade the report.
Rejected Alternatives: Treating Volume removal as sufficient proof for Task 02; scanning every string material setter in all Rendering scripts and falsely failing unrelated debug/rendering domains; chat-only claims.
Scalability potential: Runtime behavior unchanged. The proof artifact now tracks the exact zero-GC post parameter route needed for low/middle/high/ultra quality scaling through the 64-byte CBuffer.
Hardware Impact: Runtime 0 us; editor-only scanner cost increases by a few string searches per scoped file.

## Decision 018 - Dead Concrete Field Reference Removal

Problem: The active Noir route removed direct `_noirSurvivalSystem` and `_noirPlayerMovement` fields, but the canonical feature `Dispose()` still attempted to null those deleted concrete references. That would create a compile error once the unrelated external scanner blocker is cleared.
Solution: Remove the two dead dispose assignments and keep only `_noirPlayerContext` plus `_noirResolutionScaler` cleanup for the snapshot-based route.
Rejected Alternatives: Reintroducing concrete survival/movement fields to satisfy cleanup code; leaving the compile fault hidden behind the external missing scanner source.
Scalability potential: Runtime behavior unchanged; this preserves the pure snapshot scalar route for all quality tiers.
Hardware Impact: Runtime 0 us. Static impact is eliminating a guaranteed compile error in the SHINOBU-owned integration diff.

## Decision 019 - Color Profile Negative Lookup Cache

Problem: The cold CSV profile table is bounded, but the active selector still rescanned all 32 rows every frame when the current depth/stress lookup missed. Hits were cadence-cached; misses were not.
Solution: Add `_hasCachedNoirColorProfileLookup` so both hit and miss results are cached under the same depth/stress/toxicity lookup hash and continuous GlobalQualityWeight cadence. The selector now returns cached false until the quality-scaled cadence expires instead of performing a hidden linear miss scan each frame.
Rejected Alternatives: Keeping repeated miss scans because the table is small; adding a managed Dictionary; inventing a new NativeHashMap Vault lane without first-party Vault ownership support.
Scalability potential: Low quality keeps the 18-frame refresh cadence; high/ultra refresh as fast as 2 frames. The cache cadence remains continuous and does not change visual authority.
Hardware Impact: Worst-case miss path drops from O(32 rows per rendered frame) to O(32 rows per cadence window), i.e. up to 18x fewer cold profile row reads at low quality.

## Decision 020 - Binary Payload Ledger Route Proof

Problem: The Noir Vault IDs, DTO layout, rollback exclusion, and Data Monolith non-readiness were proven in local docs/status/report but were missing from the shared binary payload ledger.
Solution: Add a SHINOBU_235 row to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` that names Vault IDs `71040..71045`, 64-byte DTO anchors, source files, proof artifacts, route summary, rollback/save boundary, and the external compile blocker.
Rejected Alternatives: Treating `DEEP_SEA_NOIR_POST_PROCESSOR_SHINOBU_235.md` as sufficient for shared payload ownership; claiming `noir_color_grading_profiles.csv` as Data Monolith readiness; editing unrelated ledger rows owned by other agents.
Scalability potential: Runtime behavior unchanged. The ledger records that `GlobalQualityWeight` scales visual detail and profile cadence continuously without changing DTO layout, save identity, or authority route across low/middle/high/ultra tiers.
Hardware Impact: Runtime 0 us. Static governance impact is lower integration risk: downstream agents can see the exact CBuffer/Vault payload boundary without adding sibling dependencies or duplicate buffers.

## Decision 021 - Active Branch Camera Read Prune

Problem: After the Noir input builder stopped accepting `Camera`, `AddRenderPasses()` still read `renderingData.cameraData.camera` before the `deepSeaNoirUnifiedPass` early return, leaving dead camera plumbing in the active branch.
Solution: Move the camera reference extraction below the Noir early return so the active branch only clears history, checks Noir readiness, updates constants, enqueues the RenderGraph pass, and exits.
Rejected Alternatives: Leaving the read because it is not a transform read; using the camera again for fallback depth; adding a render-camera dependency to Noir telemetry.
Scalability potential: Runtime visuals unchanged. Low/middle/high/ultra continue to use the same Vault scalar route and continuous shader quality math without camera-derived presentation facts.
Hardware Impact: Measured runtime gain is unavailable and likely below profiler resolution; static impact is stricter active-path dependency hygiene and one less managed camera reference read before the legacy branch.

## Decision 022 - Global Authority Route Card

Problem: SHINOBU_235 now owns a GlobalDataVault/telemetry route, but a ledger row is not enough under the Global Authority route-card doctrine.
Solution: Add `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md` using the route-card template fields: owner, instruments, producer/consumer phases, fixed capacities, expected reads per frame, `GlobalQualityWeight` behavior, accessor purity, payload layout, failure modes, telemetry, black-box dump, shutdown, scene unload, stale-handle behavior, rejected alternatives, H-Phi impact, proof required before GREEN, and `YELLOW / STATIC_SOURCE_ONLY` disposition.
Rejected Alternatives: Storing route proof only in status/rationale/log; marking the route `GREEN` without Unity import/profiler/player proof; creating a new SignalBus lane for one render consumer.
Scalability potential: Runtime behavior unchanged. The route card records the continuous low/middle/high/ultra quality behavior and states that quality never changes layout/save/rollback ownership.
Hardware Impact: Runtime 0 us. Integration impact is lower global-monolith risk because fixed Vault capacities, stale-handle behavior, and disposal ownership are explicit.

## Decision 023 - RenderGraph Buffer Import And Scoped Proof Honesty

Problem: Read-only audit found two source risks and three proof risks: the RenderGraph raster path passed `SetGlobalConstantBuffer` arguments in the wrong order, raw `GraphicsBuffer` constants were used without `ImportBuffer`/`UseBuffer`, docs claimed SHINOBU ownership while source used `SystemID.GraphicsScalability`, the report sounded project-wide despite out-of-domain Volume residue, and DTO layout proof covered only the CBuffer DTO.
Solution: Convert Noir and reconstruction pass data to `BufferHandle`, import constant buffers into RenderGraph, declare `UseBuffer(Read)`, bind with `SetGlobalConstantBuffer(buffer, nameID, offset, size)`, document `SystemID.GraphicsScalability` as the native-memory owner tag for SHINOBU GPU scalability lanes, change the report to scoped eradication with project-wide=false and known out-of-domain residue examples, broaden the scanner to generic string shader setters in the SHINOBU route files, and add offset proofs for every Noir DTO.
Rejected Alternatives: Leaving undeclared RenderGraph buffers because the pass might compile; changing source to a nonexistent Echelon 8 `SystemID`; claiming whole-project Volume eradication; accepting narrow `_Chromatic/_Vignette/_Grain/_Glitch` scans as full string-parameter proof.
Scalability potential: Runtime math remains continuous: low keeps one texture sample, cheap grain, and zeroed detail masks; middle/high/ultra spend more shader ALU through the same CBuffer and declared RenderGraph resource route. Proof now distinguishes SHINOBU scope from unrelated scene/UI Volume systems.
Hardware Impact: Runtime microseconds are not measured. Static impact is correctness: RenderGraph can schedule/import the CBuffer dependency explicitly, and the argument order now matches Unity 6 `RasterCommandBuffer` API. CPU guard was 100%, so rebuild was not launched.

## Decision 024 - Player Runtime TryGet Purity

Problem: `PlayerRuntimeContextService.TryGetActiveRuntimeContext` was a read-looking accessor but executed `SyncPlayerContext()`, which can traverse hierarchy and resolve components. That violates the global rule for `TryGet*` accessors and can turn broad hot consumers, including legacy visor paths, into hidden scene-sync triggers.
Solution: Remove the sync from `TryGetActiveRuntimeContext`. The accessor now returns only the last context published by the owner-phase sync. Initialization, explicit refresh, enable, and tick remain the owner publication routes.
Rejected Alternatives: Keeping the sync to preserve old pull-and-refresh behavior; adding a SHINOBU-specific player context fallback; editing every consumer to work around an impure accessor.
Scalability potential: Low devices avoid hidden player hierarchy traversal from unrelated visual/AI/world consumers. Middle/high/ultra still receive the same published snapshot route; no quality tier changes gameplay ownership.
Hardware Impact: Exact microseconds are not measured. Static impact is removal of a hidden hot `TryGet*` -> `SyncPlayerContext` path across dozens of call sites. Rebuild was not launched because CPU guard remained at 100%.

## Decision 025 - Noir Parameter NaN Vaccination

Problem: The post-job finite check caught invalid constants after construction, but editor overrides, CSV profile multipliers, and tuning rows could still pass NaN through `math.clamp`/`math.saturate` and poison the CBuffer for one evaluation before failsafe replacement.
Solution: Sanitize editor override inputs, CSV profile grade/response fields, tuning DTO creation, and `CalculateNoirParametersJob` input/tuning reads before clamp/saturate/lerp math. The existing invalid-constant telemetry and dump path remains the final proof/failsafe layer.
Rejected Alternatives: Trusting UI slider ranges and CSV rows; relying only on `NoirConstantsFinite` after the output DTO is already poisoned; adding exceptions or managed validation in the render path.
Scalability potential: Low/middle/high/ultra quality curves remain unchanged. Bad tuning now collapses to finite defaults instead of changing visual authority, DTO layout, or save/rollback identity.
Hardware Impact: Runtime microseconds are not measured. Static cost is a handful of scalar finite checks in a one-row parameter prep path; the hardware impact is avoiding NaN propagation into shader constants and telemetry.

## Decision 026 - Time And Split Finite Closure

Problem: The first NaN hardening pass still allowed `WrappedTimeSeconds` and `input.AbSplit01` to flow into `GrainParams.w` and `QualityAndLimits.w` after upstream wrapping/editor code. A NaN in either lane would still make the output DTO invalid for one evaluation.
Solution: Sanitize mock global quality, mock wrapped time, final wrapped time, and final A/B split before writing `NoirPostProcessDTO`.
Rejected Alternatives: Trusting `Mathf.Repeat`, editor range controls, or the final `NoirConstantsFinite` replacement alone.
Scalability potential: Low/middle/high/ultra visual curves are unchanged; bad time/split data collapses to finite defaults without changing DTO layout or authority route.
Hardware Impact: Runtime microseconds are not measured. Static cost is two finite scalar guards and one normalized split guard in the one-row parameter path.

## Decision 027 - Durable Self Audit Artifact

Problem: The prompt requires an explicit self-audit with task reconciliation, layout proof, scalability proof, Vault status, dependency graph, compile guard, and Dear Lie confirmation. Chat-only XML is not durable under compaction and does not satisfy the project reporting protocol.
Solution: Add `Docs/Reports/SHINOBU_235_SELF_AUDIT.xml` with the required evidence and keep the review disposition as `PENDING_VERIFICATION` / `STATIC_SOURCE`.
Rejected Alternatives: Printing the self-audit only in the final response; marking the route green without Unity import/profiler proof.
Scalability potential: Runtime unchanged. The artifact records low/middle/high/ultra continuous quality behavior and explicitly states quality does not mutate DTO layout or authority route.
Hardware Impact: Runtime 0 us. Governance impact is durable proof for the integrator and CTO.

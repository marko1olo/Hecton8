# Rationale_SHINOBU_267

Agent: SHINOBU_267
Domain: Echelon 3 / Flora Procedural Sway
Status: POLISH PASS ACTIVE - BUILD DEFERRED BY CPU RULE

## Session Start
Problem: Static flora needs ambient current motion without CPU bones, per-object Update loops, material mutation, or gameplay-state ownership.
Solution: Pending archaeology and mandate read. Candidate route is a GPU vertex-displacement presentation path using explicit 32-byte DTOs, Vertex Color red stiffness, global flow/time parameters, alpha clipping, and continuous GlobalQualityWeight.
Rejected Alternatives: CPU Transform animation, SkinnedMeshRenderer bone animation, per-renderer material property updates, and binary quality switches violate task constraints.
Scalability potential: Low uses crushed displacement and static alpha-clipped silhouettes; Middle uses global sine phase; High uses stronger spatial phase and optional impulse blend; Ultra spends saved CPU on richer GPU-side visual response.
Hardware Impact: Target is 0 B managed hot-path allocation and no per-flora CPU loop; expected low-end gain versus CPU animation is proportional to removed object count. Exact microseconds remain PENDING VERIFICATION.

## Loop 1 Decisions - Tasks 01-05
Problem: Requested flora CPU sway path `Assets/_Project/Scripts/Environment/Flora/` does not exist; deleting broad `AmbientWaterMotionManager` would cross into physics/decorative props and risk unrelated behavior.
Solution: Treat missing flora path as no owned CPU sway to eradicate; add SHINOBU_267 scanner/validator lane to catch future SkinnedMeshRenderer, Animator, and CPU sway regressions.
Rejected Alternatives: Removing `AmbientWaterMotionManager` was rejected because it is `Hecton8.Physics` decorative-prop motion, not the assigned flora domain route.
Scalability potential: Low/Middle/High/Ultra all keep flora movement on GPU; scanner prevents asset authors from reintroducing CPU bones.
Hardware Impact: No direct runtime gain from deletion because no owned files existed; avoided a cross-domain regression. Estimated saved cost for future invalid flora: 3-12 us per 1k CPU-animated transforms.

Problem: Abyssal Flow owner is not a stable hard dependency for SHINOBU_267.
Solution: Added `FloraAmbientFlowStateDTO` and `GenerateMockAmbientFlowJob` as a decoupled Vault bridge; future flow owner can publish the same route without the shader/runtime waiting on another agent.
Rejected Alternatives: Direct reads of another agent's unfinished buffer or scene searches were rejected; both violate one-route ownership and hot-path determinism.
Scalability potential: Low uses the same global mock vector with amplitude crushed by quality; Middle/High/Ultra increase visual motion through continuous shader weight without CPU entity loops.
Hardware Impact: Mock generation is one 32-byte DTO write per frame, estimated under 2 us on i3/MX350; replacing 100k transform updates avoids millisecond-scale CPU work.

## Loop 2 Decisions - Tasks 06-10
Problem: The dispatcher can only call a master system in its declared phase; one `IDispatcherSystem` cannot legally own both PRE_SIMULATION and VISUAL_SYNC.
Solution: `FloraAmbientSwayRuntime` owns the PRE_SIMULATION parameter job; a private `VisualSyncUploadSystem` adapter owns VISUAL_SYNC constant-buffer upload.
Rejected Alternatives: LateUpdate upload and same-object fake dual phase were rejected because they bypass the dispatcher proof artifact.
Scalability potential: Low/Middle/High/Ultra keep one 32-byte CBuffer path; shader quality gates decide visible cost.
Hardware Impact: PRE_SIMULATION writes one 32-byte DTO and VISUAL_SYNC copies one 32-byte DTO; estimated <5 us combined on i3/MX350 excluding Unity driver overhead.

Problem: Existing interaction field already owns submarine impulse displacement; ambient sway must not replace it or double-author gameplay truth.
Solution: Added ambient GPU offset as a separate visual term and left `ResolveFloraSwayFieldOffset` intact, so wake impulses and global current sine blend in the vertex shader.
Rejected Alternatives: CPU bones, per-renderer material property blocks, direct shader vectors, and deleting the interaction field were rejected.
Scalability potential: Low gates ambient displacement to zero via `smoothstep(0.1,0.4,quality)`; Middle uses global sine; High/Ultra retain impulse blend plus spatial phase.
Hardware Impact: Removes per-flora CPU animation; shader adds one dot, sine approximation, and vector multiply per vertex, paid only by visible flora.

## Loop 3 Decisions - Tasks 11-15
Problem: Ambient flora sway must scale continuously without a low/ultra branch and without becoming gameplay truth.
Solution: Shader displacement multiplies by `smoothstep(0.1, 0.4, GlobalQualityWeight)`; the runtime is documented as presentation-only and excluded from save, rollback, and netcode authority.
Rejected Alternatives: Quality keywords, hardware-tier ifdefs, and rollback snapshots were rejected because they create binary behavior or false authority.
Scalability potential: Low becomes static alpha-clipped silhouettes; Middle restores cheap current sway; High/Ultra keep impulse blend and spatial phase while CPU remains flat.
Hardware Impact: Low-end devices skip visible displacement through a scalar gate with no CPU topology change; estimated CPU cost remains one 32-byte DTO copy.

Problem: Long sessions need stable phase math and crash evidence.
Solution: Parameter job wraps time with `math.fmod(t, 1000f)` and writes a 300-entry `SwayTelemetryEntry` ring; NaN/invalid payloads dump to `Docs/AgentLogs/Dump_SHINOBU_267.bin`.
Rejected Alternatives: Unbounded `_Time.y`, managed per-frame logs, and dynamic telemetry lists were rejected for precision drift and GC risk.
Scalability potential: All device tiers use identical wrapped time and telemetry ABI; only visible amplitude changes continuously.
Hardware Impact: Telemetry is one fixed 32-byte write per frame, estimated <1 us; dump cost exists only on fault path.

## Loop 4 Decisions - Tasks 16-20
Problem: Sway tuning and asset validation must be cold/editor-only without leaking runtime allocation patterns into the player loop.
Solution: Added UI Toolkit `AmbientFloraSwayTunerWindow`, `FloraAnimationScanner`, vertex-color debug toggle, and source-level self-audit under editor assembly; runtime parser uses Vault scratch plus `ReadOnlySpan<byte>`.
Rejected Alternatives: Runtime IMGUI, managed CSV `string.Split`, and manual asset deletion without a report were rejected.
Scalability potential: Low/Middle/High/Ultra profile rows can tune amplitude/frequency/clip continuously without changing the shader ABI.
Hardware Impact: Editor tools have no player-frame cost; CSV load is cold and bounded by a 64KB Vault scratch buffer.

Problem: The CTO requires a proof artifact instead of chat-only claims.
Solution: Added `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with current static scan result and a Unity menu scanner that regenerates the same report from prefabs/scenes.
Rejected Alternatives: Chat-only reporting and unstructured console spam were rejected.
Scalability potential: Prevents invalid CPU-bone flora assets from entering any tier.
Hardware Impact: Current static text scan found 0 flora prefab/scene SkinnedMeshRenderer/Animator findings; no asset purge was required.

## Loop 5 Self-Review
Problem: Initial SHINOBU_267 BufferID choice overlapped adjacent 716xx lanes owned by flora interaction, ocean kinematics, and seaglide work.
Solution: Moved ambient sway IDs to 72900-72906 and updated the architecture document.
Rejected Alternatives: Keeping 71655-71661 was rejected because hidden cross-domain buffer aliasing would corrupt unrelated systems.
Scalability potential: Stable IDs keep all device tiers on one route and prevent random buffer ownership failures.
Hardware Impact: Correct ID routing has no frame cost; it prevents catastrophic Vault alias faults.

Problem: Compile could not be launched under the local rules.
Solution: Repeated CPU/csc/dotnet preflight and static evidence checks; build remains deferred because CPU sampled above 50%.
Rejected Alternatives: Launching `dotnet build` at CPU 80-100% was rejected by AGENTS.
Scalability potential: No runtime claim depends on an unperformed compile.
Hardware Impact: No extra host load was added during high CPU pressure.

## Polish Pass 6 Decisions
Problem: Adding SHINOBU_267 BufferID names to `H8Memory.cs` widened a core header and violated compile-wall isolation for a visual-only flora lane.
Solution: Removed the global enum entries and kept the numeric lane as local `const BufferID` casts in `FloraAmbientSwayRuntime` (`72900-72906`), matching the existing local-ID style used by adjacent flora visual systems.
Rejected Alternatives: Keeping global enum members was rejected because a visual tuning pass should not force every assembly depending on core memory contracts to recompile.
Scalability potential: Low/Middle/High/Ultra all keep the same Vault route; compile isolation improves iteration without changing runtime behavior.
Hardware Impact: Runtime cost is unchanged; build graph churn is reduced by avoiding a core API surface change.

Problem: The first DTO names drifted from the XML-mandated ABI and stored `PhaseSpatialOffset` in the speed lane.
Solution: Renamed the 32-byte ABI to exact lanes: `GlobalFlowVector` offset 0 and `SwayMathParams` offset 16. `GlobalFlowVector.w` now stores flow speed. Editor `PhaseSpatialOffset` is folded into `SwayMathParams.z` as effective spatial frequency, preserving the 32-byte CBuffer without a third lane.
Rejected Alternatives: Adding another float4 for phase controls was rejected because it expands the CBuffer and weakens ARM64/constant-buffer proof. Reusing speed as phase was rejected because it contradicted the task ABI.
Scalability potential: Low collapses the sine displacement through quality gate; Middle/High/Ultra scale phase density via one continuous frequency scalar.
Hardware Impact: No byte increase. 32B copy remains one cache-aligned DTO; shader math loses one multiply versus separate frequency and phase multiplier.

Problem: Vertex-color debug added `TEXCOORD23`, increasing interpolator pressure for a scene-view-only proof path.
Solution: Removed the extra debug varying and reused existing `biolumColor` only when `_HectonFloraVertexColorDebug` is enabled. Pass 23 later reintroduces `TEXCOORD23` as a required alpha-mask UV, not as a debug interpolator.
Rejected Alternatives: Dedicated debug interpolator was rejected because mobile fragment bandwidth and varying slots are not free.
Scalability potential: Debug remains editor-controlled; production visual tiers do not pay the extra interpolator.
Hardware Impact: Saves one half4 varying lane from the production shader ABI.

Problem: `PreSimulationTick` could reacquire Vault handles and rewrote tuning every frame.
Solution: Moved Vault acquisition to cold `OnEnable`/`Start`, left hot phase to resolve only required handles, and added a dirty gate for tuning writes.
Rejected Alternatives: Hot `GetGenerationHandle` fallback was rejected because handle acquisition is not presentation math and may mutate Vault state.
Scalability potential: All tiers keep fixed 32B parameter updates; editor changes still push immediately through the dirty path.
Hardware Impact: Removes avoidable steady-state Vault handle checks and tuning writes on i3/MX350-class CPUs.

Problem: Task 06 required explicit job structs and deterministic Burst attributes, while the global dispatcher doctrine rejects tiny same-frame Job System runners.
Solution: Kept the two one-DTO jobs as `[BurstCompile(FloatMode.Deterministic)]` structs with complete `[NoAlias]` proof and corrected the execution route to direct `Execute()` in the PRE_SIMULATION owner phase.
Rejected Alternatives: Scheduling tiny jobs and immediately completing them was rejected because dispatcher overhead would exceed the one-element workload. Ordinary runtime `.Run()` was rejected after SHINOBU_206 scanner evidence classified it as stall debt for scalar presentation kernels.
Scalability potential: The job math remains one DTO independent of flora count, so device tier only changes shader amplitude/frequency response.
Hardware Impact: Direct scalar execution avoids the Job System synchronous runner path, estimated 3-150 us saved per scalar runner on i3/MX350-class hardware by SHINOBU_206 static model. The actual math remains sub-microsecond scale relative to per-flora CPU animation.

Problem: Shared rendering report had been overwritten by another active agent after the first SHINOBU_267 write.
Solution: Added a nested `shinobu_267_flora_ambient_sway` report object and validated the root JSON with `ConvertFrom-Json`.
Rejected Alternatives: Replacing the root report was rejected because it would delete SHINOBU_265 and SHINOBU_262 evidence.
Scalability potential: Shared reporting remains multi-owner and evidence-preserving.
Hardware Impact: Documentation-only; runtime cost 0 us.

Problem: The editor scanner itself would have overwritten the shared report on the next manual run.
Solution: Reworked `FloraAnimationScanner` to remove/replace only its top-level `shinobu_267_flora_ambient_sway` section and preserve all other report keys.
Rejected Alternatives: Writing a separate report only was rejected because Task 19 specifically names `RENDERING_OPTIMIZATION_REPORT.json`; destructive root overwrite was rejected as multi-agent evidence loss.
Scalability potential: Future scanner runs remain safe under 20+ concurrent agents.
Hardware Impact: Editor-only string merge; player runtime cost 0 us.

## Polish Pass 8 Decisions
Problem: Runtime still read `Time.frameCount` and `Time.deltaTime`, contradicting Task 06 dispatcher-time discipline and rollback exclusion hygiene.
Solution: Replaced Unity time reads with `DispatcherTimingDTO.FrameId`, `FixedDelta`, and `FrameDelta`; only zero dispatcher data falls back to an owner-local frame counter and `1/60f` visual delta.
Rejected Alternatives: Keeping Unity time was rejected because it creates a second clock route. Reading global simulation state elsewhere was rejected because dispatcher timing is already the owner route.
Scalability potential: All device tiers use one timing route; quality still scales only visual amplitude/frequency, not truth ownership.
Hardware Impact: Same arithmetic cost; removes hidden engine clock dependency.

Problem: VISUAL_SYNC could allocate replacement `GraphicsBuffer` resources if buffers were missing.
Solution: Constant buffers are created during cold bootstrap after Vault acquisition. VISUAL_SYNC now validates buffer readiness and records upload-skip telemetry instead of allocating.
Rejected Alternatives: Hot buffer recreation was rejected because active frame resource creation can hitch and violates zero-allocation presentation updates.
Scalability potential: Low/Middle/High/Ultra use the same 32-byte upload path; device loss is recorded instead of silently allocating in-frame.
Hardware Impact: Removes worst-case managed/GPU resource allocation from the active frame path.

Problem: The UI Toolkit tuner searched for runtime objects every editor update, allocating an array through `Resources.FindObjectsOfTypeAll`.
Solution: The window caches the runtime on pull/push and the graph tick reads only the cached reference; the graph now samples both sway params and `GlobalFlowVector`.
Rejected Alternatives: Per-tick scene/resource search was rejected because Task 16 explicitly requires a zero-GC graph path.
Scalability potential: Editor-only, but keeps technical-artist tooling from producing false allocation noise.
Hardware Impact: Editor cost drops from recurring search allocation to one cached reference read per tick.

Problem: The shader multiplied displacement by a zero quality gate but still evaluated the sine wave.
Solution: Added an early return when `smoothstep(0.1,0.4,quality)` is effectively zero, before `FastSinApprox`.
Rejected Alternatives: Keeping branchless sine evaluation was rejected because the severe thermal path would still pay ALU for no visible motion. Hardware-tier keywords were rejected; this remains driven by the continuous quality scalar.
Scalability potential: Low devices at quality <=0.1 become static alpha-clipped silhouettes without sine cost; Middle/High/Ultra keep continuous restored motion.
Hardware Impact: Saves one sine approximation per flora vertex in severe thermal mode.

## Polish Pass 9 Decisions
Problem: `GenerateMockAmbientFlowJob` used `math.normalize(rawDirection)` before the post-check, allowing a transient NaN vector if the synthetic raw direction ever collapsed to zero.
Solution: Replaced it with explicit `lengthSq`, guarded `math.rsqrt(math.max(lengthSq, 0.0001f))`, and the same deterministic fallback direction when the raw length is below epsilon.
Rejected Alternatives: Leaving post-sanitize-only logic was rejected because the NaN vaccination rule forbids generating invalid intermediates in Burst math. Adding random perturbation was rejected because mock flow must stay deterministic. Keeping the parameter job's second `rsqrt` protected only by a prior branch was rejected because static gates may not follow control flow, so it now also uses an in-expression `math.max` guard.
Scalability potential: Low/Middle/High/Ultra all keep the same mock-flow route; quality only affects shader amplitude and does not change DTO layout or authority.
Hardware Impact: Same O(1) one-DTO mock job; avoids invalid SIMD lane propagation with no measurable extra frame cost.

Problem: SHINOBU_267 owned Vault payloads `72900-72906` but the binary payload ledger had no explicit owner row for the flora ambient sway lane.
Solution: Added a static-source ledger section naming BufferIDs, DTO sizes/offsets, shader route, rollback/save exclusion, scalability route, Dear Lie route, and dump target.
Rejected Alternatives: Relying only on runtime constants, architecture doc prose, or chat report was rejected because payload integration needs a central ledger proof.
Scalability potential: The ledger locks one route for all device classes and prevents later agents from reusing the numeric lane.
Hardware Impact: Documentation-only; runtime cost 0 us.

Problem: Cold allocations for the VISUAL_SYNC adapter and constant buffers were architecturally valid but not marked in the canonical AGENTS format.
Solution: Added `COLD ALLOC` comments at the `VisualSyncUploadSystem` construction and both `GraphicsBuffer.Target.Constant` constructions.
Rejected Alternatives: Leaving the allocation sites unannotated was rejected because static review cannot infer bootstrap-only lifecycle from intent.
Scalability potential: No quality behavior changes; the annotations protect the zero-GC proof across all device tiers.
Hardware Impact: Documentation-only; runtime cost 0 us.

## Polish Pass 10 Decisions
Problem: `TryReadLatestParams` is a read accessor used by the editor graph, but it previously shared the runtime phase resolver backed by `IDataVault.TryResolveHandle`.
Solution: Added a dedicated `TryRead<T>` helper that calls `IDataVault.TryReadHandle`, and routed `TryReadLatestParams` through it.
Rejected Alternatives: Leaving the read accessor on the general resolver was rejected because the global doctrine requires read accessors to be pure and not publish, allocate, complete jobs, or mutate fault/global telemetry.
Scalability potential: All quality tiers keep the same 32-byte CBuffer route; editor graph sampling remains a pure observer and cannot alter runtime authority state.
Hardware Impact: O(1) read path unchanged; removes a global-authority audit risk with no measurable frame cost.

Problem: Cold CSV floats could overflow into `Infinity` if an authored row contained hostile or corrupt numeric text with excessive digits.
Solution: `ParseFloat(ReadOnlySpan<byte>)` now computes the parsed scalar, checks `math.isfinite(parsed)`, and falls back when the value is not finite.
Rejected Alternatives: Trusting authored text was rejected because cold import data still feeds unmanaged profile DTOs; throwing exceptions was rejected because cold boot should fail closed to fallback values.
Scalability potential: All quality tiers keep profile-driven tuning within finite bounds; malformed rows cannot push shader amplitude/frequency into non-finite values.
Hardware Impact: Cold boot parser only; player hot path cost is 0 us.

Problem: Biome profile rows are Vault-backed with `NativeArrayOptions.UninitializedMemory`; a short CSV could leave unused profile slots with stale bytes.
Solution: `TryLoadBiomeProfilesCsv` clears the 64-row profile table immediately before cold parsing, then writes parsed rows over that scrubbed table.
Rejected Alternatives: Clearing profiles every frame was rejected as wasted hot work. Leaving unused rows uninitialized was rejected because future profile consumers could read stale hashes/scalars.
Scalability potential: Low/Middle/High/Ultra all get deterministic profile table contents; unused rows are `BiomeHash=0` instead of undefined memory.
Hardware Impact: Cold boot only; 64 explicit 32-byte writes = 2048 bytes, 0 hot-path cost.

## Polish Pass 11 Decisions
Problem: Static source scans still reported the exact forbidden vector-upload API name from editor-only self-audit text, even though the runtime upload path uses `LockBufferForWrite`, `UnsafeUtility.MemCpy`, and global constant-buffer binding.
Solution: Removed the exact forbidden literal from the editor self-audit while keeping the runtime-source validation check active through a split string token.
Rejected Alternatives: Leaving the false-positive grep result was rejected because it weakens future regression audits; removing the upload validation entirely was rejected because Task 10 needs an editor proof hook.
Scalability potential: No visual-tier behavior changes. Low/Middle/High/Ultra continue through the same 32-byte CBuffer route and shader-side quality scalar.
Hardware Impact: Editor-only source hygiene; player runtime cost is 0 us. Verification after the patch: owned runtime/editor forbidden scan returned no hits for vector upload API, Unity time, random, LINQ, hot NativeArray allocation, `Pack=1`, or DTO auto-properties.

## Polish Pass 12 Decisions
Problem: `World/FloraAmbientSway` and `Editor/FloraAmbientSway` had no local asmdefs, so Unity could absorb the new files into a broad parent assembly and widen recompilation blast radius.
Solution: Added `Hecton8.World.FloraAmbientSway.asmdef` for runtime and `Hecton8.World.FloraAmbientSway.Editor.asmdef` for tooling. Runtime references are limited to `Hecton8.Core`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics. Editor references only the SHINOBU_267 runtime assembly.
Rejected Alternatives: Leaving the files under a parent assembly was rejected because it violates compile-wall isolation. Adding references to sibling world/rendering domains was rejected because the route already communicates through Vault and shader CBuffer contracts.
Scalability potential: No quality-tier behavior changes. This protects iteration speed while keeping Low/Middle/High/Ultra on the same visual DTO route.
Hardware Impact: Runtime cost is 0 us; development compile graph churn is reduced. Static proof: both asmdefs parse as JSON and reference scan reports no sibling domain assemblies.

## Polish Pass 13 Decisions
Problem: The shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` no longer contained the SHINOBU_267 section after parallel SHINOBU_270/275 report writes.
Solution: Restored only the top-level `shinobu_267_flora_ambient_sway` object while preserving existing report fields. Re-ran a targeted flora scan: `Assets/_Project/Scripts/Environment/Flora` is absent, `Assets/_Project/Prefabs/Nature/Flora` contains 1131 files, and no active `SkinnedMeshRenderer`, `Animator`, Seaweed/Kelp CPU sway script, `Mathf.Sin`, or `Update()` findings were found in the flora prefab tree. The sole token hit is README policy text.
Rejected Alternatives: Overwriting the whole JSON root was rejected because it would destroy active SHINOBU_270/275 evidence. Marking Task 19 proof as present without restoring the artifact was rejected because the CTO reads files, not chat memory.
Scalability potential: No runtime quality behavior changes. The report blocks future CPU-bone flora regressions across every device tier.
Hardware Impact: Documentation/static audit only; player runtime cost is 0 us.

## Polish Pass 14 Decisions
Problem: `FloraAmbientSwayRuntime` implements `IServiceShutdown`, but that interface is declared in `Hecton8.Bootstrap.Contracts`; the isolated runtime asmdef did not reference that contract assembly.
Solution: Added `Hecton8.Bootstrap.Contracts` to `Hecton8.World.FloraAmbientSway.asmdef` and updated the route docs/logs to state the exact reference set.
Rejected Alternatives: Removing `IServiceShutdown` was rejected because the runtime owns GPU buffers and Vault handles that need an explicit shutdown path. Depending on a broad sibling runtime assembly was rejected; Bootstrap.Contracts is a core contract assembly.
Scalability potential: No visual-tier behavior changes. The fix preserves compile isolation while making Unity import viable.
Hardware Impact: 0 runtime us; removes a compile/import blocker.

Problem: The Burst parameter/telemetry paths used `float4.xyz` swizzles, which are property-like accessors even if they are Unity.Mathematics value types.
Solution: Replaced swizzles with explicit `new float3(x, y, z)` construction and guarded magnitude calculation.
Rejected Alternatives: Leaving swizzles was rejected because the CS1612 audit is about eliminating hidden accessor ambiguity in hot paths.
Scalability potential: Same low/middle/high/ultra CBuffer route; no shader ABI change.
Hardware Impact: Same scalar math class; the source is clearer for Burst/static review.

## Polish Pass 15 Decisions
Problem: The biome profile bridge still attempted a player-runtime text lookup through `Application.streamingAssetsPath`, which violates the DataMonolith readiness rule and risks cold IO/stutter on Android/Quest-style `StreamingAssets` routes.
Solution: Moved file-backed CSV hydration behind `UNITY_EDITOR`, renamed it to `TryLoadBiomeProfilesFromEditorCsv`, and restricted the file source to `Docs/flora_biome_sway_profiles.csv`. The parser remains `ReadOnlySpan<byte>` over Vault scratch, finite-checks scalars, scrubs profile rows once at cold editor load, and writes the same 32-byte `FloraBiomeSwayProfileDTO` table into Vault.
Rejected Alternatives: Keeping a runtime `StreamingAssets` CSV fallback was rejected because SHINOBU_267 is a presentation lane and production static data must arrive through the DataMonolith/static-data route. Removing the parser entirely was rejected because Task 17 requires an allocation-free human tuning bridge.
Scalability potential: Low/Middle/High/Ultra all consume identical finite profile DTOs and unchanged BufferIDs; quality still changes visual amplitude/frequency only, not data ownership or shader ABI.
Hardware Impact: Player hot path remains 0 us. Cold player text IO is removed; editor-only load still costs bounded file read plus at most 2048 bytes of profile scrub before Vault commit.

## Polish Pass 16 Decisions
Problem: Current source still contained two ordinary runtime `.Run()` calls for one-row presentation jobs, contradicting SHINOBU_206's dispatcher stall scanner proof and adding synchronous Job System runner overhead for work consumed in the same phase.
Solution: Replaced `mockFlowJob.Run()` and `parametersJob.Run()` with direct `Execute()` in `PreSimulationTick`, while retaining the deterministic `[BurstCompile]` job structs, explicit DTO layouts, and `[NoAlias]` field proof for static audit and future batch scheduling if the workload ever becomes amortized.
Rejected Alternatives: `Schedule().Complete()` was rejected as a fake fix and hidden fence. Leaving `.Run()` was rejected because the global stall gate treats ordinary runtime one-row `IJob.Run()` as debt. Removing the job structs entirely was rejected because Task 06 names those kernels and the editor self-audit validates their source route.
Scalability potential: Device tier behavior is unchanged. Low still gates displacement to static silhouettes, Middle restores cheap sine sway, High/Ultra retain spatial phase; CPU workload remains O(1) independent of flora count.
Hardware Impact: Removes Job System runner/fence overhead from two scalar kernels; using SHINOBU_206 static model, this avoids roughly 3-150 us on i3/MX350-class CPUs. No new gameplay state or allocation route is introduced.

## Polish Pass 17 Decisions
Problem: Static source search found no scene, prefab, or bootstrap reference to `FloraAmbientSwayRuntime`; without a host, the Vault/CBuffer route could import but never publish parameters.
Solution: Added a cold `RuntimeInitializeOnLoadMethod` lifecycle: SubsystemRegistration clears a static runtime claim, AfterSceneLoad creates one scene-local fallback GameObject only if no runtime has claimed the lane, and `OnEnable`/shutdown acquire and release the claim. The host uses `HideFlags.DontSave` and does not call `DontDestroyOnLoad`.
Rejected Alternatives: Requiring manual scene placement was rejected because Task 10/16 runtime proof must not depend on undocumented scene authoring. A DDOL singleton was rejected because this is a scene-local presentation owner and AGENTS forbids expanding persistent roots without a bootstrap mandate.
Scalability potential: No visual-tier behavior changes. The bootstrap only guarantees the same O(1) DTO route is alive on every scene; quality still scales shader displacement continuously.
Hardware Impact: 0 hot us. Worst case is one cold `GameObject` plus one component allocation after scene load when authored placement is absent; per-frame work remains unchanged.

## Polish Pass 18 Decisions
Problem: The current SHINOBU_258 h8bin validation artifact still contained a stale `RUNTIME_TEXT_STREAMINGASSETS_LOAD` finding for `FloraAmbientSwayRuntime`, even after SHINOBU_267 source no longer contained that route.
Solution: Re-ran `Tools/h8bin_validator.py` against `Assets\StreamingAssets` and refreshed `Docs/Reports/SHINOBU_258_h8bin_validation_current.json` plus JUnit output. The gate still fails globally, but FloraAmbientSway is no longer listed.
Rejected Alternatives: Editing the JSON by hand was rejected because the proof must come from the canonical validator. Claiming DataMonolith readiness was rejected because `static_data.h8bin` is still absent and unrelated WaterOptics/Visor text routes remain.
Scalability potential: No runtime behavior change. The refresh prevents stale evidence from incorrectly blocking SHINOBU_267 while preserving global red status for real remaining owners.
Hardware Impact: Evidence-only; validator processed 0.034424 MB in 1.013045 seconds. Runtime cost is 0 us.

## Polish Pass 19 Decisions
Problem: The cold fallback host covered initial scene load, but the route needed explicit proof for scene reload and late/replaced `IDataVault` ownership without violating the no-hot-polling doctrine.
Solution: Documented and audited the runtime patch that subscribes `SceneManager.sceneLoaded` from the cold `AfterSceneLoad` hook, unregisters it at `SubsystemRegistration`, and implements `IGlobalRegistryHotSwapListener` for DataVault replacement. On replacement, old Vault generation handles are released and cleared before cold reacquisition from the new vault event.
Rejected Alternatives: A hot `PreSimulationTick` retry against `GlobalRegistry.DataVault` was rejected because it would poll the service locator in the frame loop. Calling `EnsureShaderParamsBuffers()` from `VisualSyncTick` was rejected because it can allocate `GraphicsBuffer` resources in an active frame; upload skip telemetry is the correct failure proof until cold bootstrap/hot-swap provides buffers.
Scalability potential: Low/Middle/High/Ultra visual behavior is unchanged. Lifecycle hardening keeps the same 32-byte CBuffer route alive across scene reloads and DataVault swaps without changing DTO layout, quality curves, save identity, rollback exclusion, or shader ABI.
Hardware Impact: 0 hot us. The pass removes inert-owner and stale-vault failure modes while preserving the existing one-DTO CPU cost and avoiding active-frame GPU allocation.

## Polish Pass 20 Decisions
Problem: Generic source scans can flag every `new` token, editor debug allocation, and fault dump allocation as if it were hot-path debt, hiding the actual runtime allocation surface.
Solution: Classified owned `new`/allocation-like hits by execution route. Runtime steady-state hits are Unity.Mathematics value-type construction or one-row job struct initialization. Reference allocations are cold fallback host/adapter/GPU buffer creation or fault-only telemetry dump file writers. Editor hits are confined to UI Toolkit windows, scanner `StringBuilder`, debug logs, and editor-only runtime discovery.
Rejected Alternatives: Treating all `new` tokens as equal was rejected because it produces false positives. Moving editor-only UI allocations into runtime was rejected. Removing the fault dump writer was rejected because Task 15 requires a black-box artifact on invalid math.
Scalability potential: No visual-tier behavior changes. Low/Middle/High/Ultra still use one CBuffer route; the scan confirms no hidden per-flora CPU loop or managed allocation tier exists.
Hardware Impact: Evidence-only, 0 hot us. The classification supports the 0 B/frame claim for the SHINOBU_267 steady path pending Unity profiler proof.

## Polish Pass 21 Decisions
Problem: The editor asmdef referenced only `Hecton8.World.FloraAmbientSway`, but the editor consumes public runtime surfaces whose visible base/interface/member signatures include `Hecton8.Core`, `Hecton8.Bootstrap.Contracts`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`.
Solution: Added those direct public-surface references to `Hecton8.World.FloraAmbientSway.Editor.asmdef` and updated the architecture/ledger text. Runtime references remain unchanged; no sibling domain assembly reference was added.
Rejected Alternatives: Adding unrelated `Hecton8.Core.Memory` or sibling domain references to the editor asmdef was rejected because editor code does not directly consume those contracts. Leaving transitive runtime references to carry public signature assemblies was rejected because field/interface access is a compile boundary, not a runtime route.
Scalability potential: No quality behavior changes. Low/Middle/High/Ultra still consume the same 32-byte CBuffer and editor-only graph reads the same DTO lanes.
Hardware Impact: Editor import risk removed; player runtime cost is 0 us.

Problem: The UI Toolkit tuner slider callback used a static `GetWindow` lookup, and additive scene scanning closed the opened scene only on the successful path.
Solution: Made slider callbacks instance-local (`PushTuning`) and wrapped each additive scene scan in a per-scene `finally` that closes valid opened scenes before restoring the original scene setup.
Rejected Alternatives: Keeping static `GetWindow` in value callbacks was rejected because it can create/focus editor windows from a data-change path. Trusting the outer setup restore alone was rejected because additive scenes should be released at the narrowest owner scope.
Scalability potential: Editor-only tooling remains a proof surface; runtime visual tiers and DTO ABI are unchanged.
Hardware Impact: Editor-only 0 player us. It removes avoidable editor window lookup and additive scene lifetime debt.

## Polish Pass 22 Decisions
Problem: SHINOBU_267 introduced new Unity asset folders, `.cs`, and `.asmdef` files without source-controlled `.meta` files. Unity would generate GUIDs per workstation, creating import identity drift and unstable assembly/script references.
Solution: Added explicit `.meta` files for `Assets/_Project/Scripts/World/FloraAmbientSway`, `FloraAmbientSwayRuntime.cs`, `Hecton8.World.FloraAmbientSway.asmdef`, `Assets/_Project/Scripts/Editor/FloraAmbientSway`, `FloraAmbientSwayEditorTools.cs`, and `Hecton8.World.FloraAmbientSway.Editor.asmdef`. GUIDs were derived from the SHINOBU_267 asset paths and verified unique under `Assets`.
Rejected Alternatives: Letting Unity generate metas was rejected because it creates nondeterministic import identity across collaborators. Touching unrelated folder metas was rejected because this pass owns only the SHINOBU_267 asset lane.
Scalability potential: No visual-tier behavior changes. This protects Unity import and compile-wall identity while Low/Middle/High/Ultra still use the same 32-byte CBuffer route.
Hardware Impact: 0 runtime us. It removes asset import instability only.

Problem: The editor self-audit did not check the asmdef/meta route added during polish.
Solution: Extended `FloraAmbientSwaySelfAudit` to validate runtime/editor asmdef boundary and presence of all six SHINOBU_267 `.meta` files.
Rejected Alternatives: Leaving meta verification only in chat/logs was rejected because Task 20 requires a runnable proof hook. Adding runtime checks was rejected because asset identity is editor/import proof, not player-frame work.
Scalability potential: Editor-only proof; runtime quality behavior and DTO ABI unchanged.
Hardware Impact: Editor-only file existence checks, 0 player us.

## Polish Pass 23 Decisions
Problem: Read-only shader audit found `_QUALITY_MX350/_QUALITY_HIGH` variants still affected vegetation motion/noise/payload/dither behavior, contradicting the no-binary-quality route. It also found `_AlphaClip` applied to procedural coverage only, not a texture-authored torn leaf edge mask.
Solution: Removed the local `_QUALITY_MX350/_QUALITY_HIGH` shader feature and replaced affected branches with continuous curves from `_GlobalFloraSwaySwayMathParams.w`: high-tap current noise blends by `smoothstep(0.45,0.95,q)`, scatter payload reads by `smoothstep(0.55,0.9,q)`, kelp amplitude by `lerp(0.8,1.0,smoothstep(0.1,0.65,q))`, and low-quality dither fades by `1-smoothstep(0.1,0.35,q)`. Added `_FloraAlphaMask` default-white texture sampling and multiplied `coverageVisibility` by its alpha before `_AlphaClip`.
Rejected Alternatives: Narrowing documentation to say "ambient overlay only" was rejected because the shader source still had binary vegetation behavior. Keeping procedural-only coverage was rejected because Task 08 requires texture-authored torn morphology. Alpha blending was rejected because the lane must remain alpha-test/cutout.
Scalability potential: Low quality keeps cheap trigonometric noise and skips high-tap `ValueNoise3D`/scatter-buffer reads; middle quality blends detail in smoothly; high/ultra restore richer procedural noise and payload detail without changing DTO layout, BufferIDs, save identity, or authority route.
Hardware Impact: CPU cost remains 0 us. GPU cost at low quality is reduced by early high-tap skips; alpha mask adds one texture alpha sample per fragment but enables torn silhouettes without geometry or CPU bones.

## Polish Pass 24 Decisions
Problem: The first `<SELF_AUDIT>` block in `LOG_SHINOBU_267.md` was written before pass 23 and therefore did not mention `_FloraAlphaMask`, the removal of `_QUALITY_MX350/_QUALITY_HIGH`, or the expanded editor asmdef public-surface refs.
Solution: Appended a new bottom-of-log `<SELF_AUDIT revision="2026-05-21-P24">` with the current 20-task reconciliation, 32-byte DTO layout, continuous quality curves, Vault IDs, NoAlias/dependency route, compile guard, and Dear Lie proof.
Rejected Alternatives: Editing historical audit text was rejected because the log is top-old/bottom-new. Relying on stale audit plus scattered pass notes was rejected because Task 20 requires one current proof block.
Scalability potential: Documentation-only; runtime quality behavior remains low static alpha-test, middle cheap sine sway, high/ultra richer shader detail through continuous curves.
Hardware Impact: 0 runtime us. This removes audit drift only.

## Polish Pass 25 Decisions
Problem: Shader quality resolution fail-opened non-finite quality to `1.0`, the interaction field quality lane needed the same finite guard, and the first editor self-audit order check for early alpha clip matched the varying-struct `half3 normalWS` instead of the fragment lighting path.
Solution: Quality now fail-closes to `0.0` in both the global vegetation quality resolver and the interaction-field quality read. Fragment coverage samples `_FloraAlphaMask.a`, multiplies coverage, then clips before `half3 normalWS = SafeNormalize3(...)`. The editor self-audit now searches for that exact lighting statement after the early clip.
Rejected Alternatives: Failing open to high quality on corrupt data was rejected because it spends ALU during invalid thermal/scalability input. Late-only alpha discard was rejected because killed fragments would still pay normal/light/caustic setup. Keeping the broad self-audit search was rejected because it produced a false negative against the corrected shader.
Scalability potential: Low/corrupt quality collapses to static alpha-tested silhouettes and cheap noise; middle quality blends detail back by smooth curves; high/ultra still restore rich shader detail when quality is finite and explicitly owned.
Hardware Impact: CPU cost remains 0 us. GPU cost drops on weak/corrupt-quality paths by skipping high-tap detail and by discarding masked fragments before lighting work.

Problem: Runtime helper names still used `Resolve*` for mutating operations, contradicting the doctrine that read accessors are pure.
Solution: Mutating helpers were renamed to `AdvanceShaderParamsBuffer`, `AdvanceFrameId`, and `AdvanceVisualFrameId`; editor self-audit rejects the old names.
Rejected Alternatives: Keeping mutation hidden under `Resolve*` was rejected because static review cannot distinguish pure reads from owner-phase state advancement.
Scalability potential: No quality behavior change; authority proof improves without changing DTO layout or shader route.
Hardware Impact: 0 runtime us. This is naming/authority hygiene only.

## Polish Pass 26 Decisions
Problem: The two one-row Burst kernels still used `NativeArray` indexers for mutation (`FlowState[0] = state`, `Params[0] = next`), leaving a hidden element setter route where the XML mandate asks for direct memory mutation through `UnsafeUtility.AsRef<T>`.
Solution: Both job structs are now `unsafe` and acquire row pointers through `NativeArrayUnsafeUtility`; writes go through `UnsafeUtility.AsRef<FloraAmbientFlowStateDTO>(flowPtr) = state` and `UnsafeUtility.AsRef<FloraSwayParamsDTO>(paramsPtr) = next`. Read-only flow/tuning rows are copied from unsafe refs after the existing `[ReadOnly, NoAlias]` proof.
Rejected Alternatives: Leaving indexer writes was rejected because it weakens the CS1612 audit lane even if the generated code may inline. Scheduling the jobs to hide mutation behind Job System execution was rejected as a same-frame fence risk for one-row presentation math.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged: one 32-byte params row feeds the shader and quality controls only visible detail.
Hardware Impact: Same O(1) scalar workload; expected player-frame gain is below measurement noise, but the source now matches the direct-memory mutation mandate and avoids future defensive-copy regressions.

## Polish Pass 27 Decisions
Problem: The pass-25/pass-26 log block was inserted above older pass-9 evidence because the patch anchor matched the first historical `</SELF_AUDIT>` tag, not the physical end of the file.
Solution: Appended a bottom-of-log P27 audit delta that restates the latest pass-25/pass-26 proof and records the ordering repair.
Rejected Alternatives: Rewriting or deleting the earlier misplaced block was rejected because the agent log is an audit trail; appending a correction preserves evidence and restores bottom-new ordering.
Scalability potential: No quality behavior change.
Hardware Impact: Documentation-only, 0 runtime us.

## Polish Pass 28 Decisions
Problem: Shader quality fail-closed correctly, but the C# `ResolveGlobalQualityWeight()` fallback still converted non-finite `HomeostasisBrain.GlobalQualityWeight` to `1f`, opening the visual-overkill path before CBuffer packing.
Solution: Changed the runtime fallback to `0f` and extended editor self-audit to require the runtime quality fail-closed route.
Rejected Alternatives: Keeping C# fail-open was rejected because corrupt thermal/scalability input must shed cost, not maximize it. Relying only on shader-side fail-closed was rejected because the CPU was already replacing non-finite input with finite `1f`.
Scalability potential: Low/corrupt quality becomes survival-cost static alpha-tested flora; middle/high/ultra still restore detail only from finite owner-published quality.
Hardware Impact: CPU cost unchanged. GPU cost on corrupt quality collapses instead of opening high-tap noise/payload detail.

## Polish Pass 29 Decisions
Problem: `CalculateFloraSwayParametersJob` still used `SanitizeFinite(GlobalQualityWeight, 1f)` internally, so a future direct job call with NaN quality could fail open despite the runtime caller now passing `0f`. The same job also acquired the optional `FlowState` read pointer before proving `FlowState.IsCreated && FlowState.Length > 0`.
Solution: Changed the job-local quality fallback to `0f` and moved `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(FlowState)` inside the guarded branch.
Rejected Alternatives: Trusting the caller was rejected because the job is a standalone kernel and must be internally NaN-vaccinated. Taking the optional pointer before the branch was rejected because optional buffers must not be touched before their validity is proven.
Scalability potential: Invalid quality remains survival-cost static flora at every call boundary; finite low/middle/high/ultra quality behavior is unchanged.
Hardware Impact: No measurable frame-time change. The pass removes fail-open and invalid-pointer audit risk from one scalar kernel.

## Polish Pass 30 Decisions
Problem: Broad static scans for `_QUALITY_MX350/_QUALITY_HIGH` still matched editor self-audit text, even though that text existed only to validate the shader had no such keywords.
Solution: Split the forbidden keyword literals inside editor self-audit (`"_QUALITY" + "_MX350"`, `"_QUALITY" + "_HIGH"`), preserving validation while making broad scans meaningful.
Rejected Alternatives: Ignoring the false positive was rejected because it can hide a real shader regression in scanner noise. Removing the check was rejected because Task 20 requires a runnable proof hook.
Scalability potential: No runtime behavior change.
Hardware Impact: Editor/source hygiene only, 0 runtime us.

## Polish Pass 31 Decisions
Problem: Delegated shader/docs audit found P30 lacked its own bottom-of-log `<STATIC_VERIFICATION>` even though P30 was now the physical tail audit. Runtime/editor delegated audit found no concrete source findings.
Solution: Added P30 guarded verification to `Status_SHINOBU_267.md` and to the bottom P30 audit block in `LOG_SHINOBU_267.md`; recorded the delegated audit reconciliation here.
Rejected Alternatives: Leaving delegated findings in chat notifications was rejected because the CTO reads disk artifacts, not chat history.
Scalability potential: No runtime behavior change.
Hardware Impact: Documentation-only, 0 runtime us.

## Polish Pass 32 Decisions
Problem: Broad old-mutating-`Resolve*` scans still matched editor self-audit validator text even though runtime no longer contains the old mutating accessor names.
Solution: Split the validator literals for `ResolveNextShaderParamsBuffer`, `ResolveFrameId`, and `ResolveVisualFrameId` while preserving the runtime-source rejection check.
Rejected Alternatives: Ignoring the false positives was rejected because it can hide a real read-accessor regression. Removing the validator was rejected because Task 20 needs a runnable proof hook.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra still use the same 32-byte CBuffer and continuous quality curves.
Hardware Impact: Editor/source hygiene only, 0 runtime us.

## Polish Pass 33 Decisions
Problem: `WriteTuningToVault` and `RecordTelemetry` still used `NativeArray` indexer writes in owner-phase hot paths, and telemetry flow magnitude used `math.sqrt` despite only needing a bounded presentation scalar.
Solution: Replaced hot tuning, telemetry-ring, and telemetry-cursor writes with `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` plus `UnsafeUtility.AsRef<T>`. Replaced telemetry magnitude with `lengthSq * math.rsqrt(math.max(lengthSq, 0.0001f))`.
Rejected Alternatives: Leaving indexer writes was rejected because the XML direct-memory mutation rule should cover every hot owner-phase mutation, not only the two Burst kernels. Keeping `math.sqrt` was rejected because the rsqrt mandate already covers hot visual/telemetry vectors.
Scalability potential: No visual-tier behavior change. Quality still controls only presentation cost and shader detail; telemetry route and DTO layout are unchanged.
Hardware Impact: Expected CPU gain is below measurement noise, but the hot source path now avoids hidden setter and sqrt debt.

## Polish Pass 34 Decisions
Problem: P33 proof lived in static scans and source review, while the runnable editor self-audit still checked only the original two Burst-kernel pointer writes.
Solution: Extended `FloraAmbientSwaySelfAudit` to verify hot owner-phase pointer writes for tuning, telemetry entries, telemetry cursor updates, and guarded `rsqrt` telemetry magnitude. Forbidden tokens are split in the validator so broad scans stay meaningful.
Rejected Alternatives: Leaving this as log-only evidence was rejected because Task 20 requires a runnable proof hook. Adding exact forbidden literals to the editor file was rejected because it would recreate P30/P32 false-positive scan debt.
Scalability potential: No runtime behavior change. Continuous quality, DTO layout, BufferIDs, and shader route are unchanged.
Hardware Impact: Editor-only audit logic, 0 runtime us.

## Polish Pass 35 Decisions
Problem: `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` requires every product/runtime task to state the first-20-minutes route moment it improves or the blocker it removes. SHINOBU_267 had strong ABI/GPU/Vault proof but no explicit route binding in its owned status/rationale/log/architecture card.
Solution: Added a SHINOBU_267 route binding that scopes ambient flora sway to World load and swim readability on the selected Copper Wire route biome. The lane removes the early-route blocker of CPU-bone/per-flora animation by keeping kelp/grass motion as one visual-only CBuffer and alpha-tested shader deformation.
Rejected Alternatives: Claiming route readiness from static source was rejected because the contract requires Unity import, Console, Play Mode/player run, profiler/GC, visual capture, and save/load proof. Expanding into flora ecology, harvesting, colliders, or extra biome content was rejected because that is parked until the selected route is proven.
Scalability potential: Low route quality keeps static alpha-tested silhouettes; middle restores cheap current sway; high/ultra add richer shader detail through the same continuous quality lane if captured on the route.
Hardware Impact: Documentation-only, 0 runtime us. The runtime route remains the existing one 32B CBuffer upload plus shader fake; proof remains pending Unity route evidence.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Removes CPU-bone/per-flora-sway route blocker while keeping flora motion visual-only and out of save/rollback state.
Proof required: Unity import/Console, Play Mode or player run through the selected route, 60-second profiler/GC capture, Frame Debugger or equivalent shader-route evidence, screenshot/clip, and save/load diff.
Parked work rejected: Net-new flora ecology, harvest gameplay, CPU vegetation colliders, extra biome spread, and visual-overkill work not captured in the selected route.
Static verification: route-binding fields are present in status, rationale, log, and architecture doc; SHINOBU_267 XML extraction remains 15929 chars with 20 task labels; owned forbidden scan is clean; runtime/editor/shader preprocessor balance is zero; `git diff --check` reports no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=0, csc=0.

## Polish Pass 36 Decisions
Problem: P16 removed ordinary runtime `.Run()` and same-frame job fences, but direct managed `mockFlowJob.Execute()` / `parametersJob.Execute()` did not prove that the one-row mathematical kernels were actually entering Burst.
Solution: Added cold-compiled Burst `FunctionPointer` entrypoints for `GenerateMockAmbientFlowJob` and `CalculateFloraSwayParametersJob`. `TryColdBootstrapVault()` compiles the pointers once during cold bootstrap; `PreSimulationTick` invokes the pointers and records a fixed telemetry fault flag if the kernel pointers are unavailable.
Rejected Alternatives: Reintroducing `Schedule().Complete()` or `.Run()` was rejected because the work is one DTO and the dispatcher doctrine rejects tiny synchronous jobs. Leaving direct managed `Execute()` was rejected because it made the Burst attributes an import-time hint rather than a runtime proof route.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The same 32B CBuffer and continuous quality curves remain; this only changes how the scalar presentation kernels are entered.
Hardware Impact: Expected runtime delta is below measurement noise for one-row math, but the route avoids Job System runner overhead and removes Burst-proof ambiguity. Build/import proof remains pending CPU-gated Unity/dotnet verification.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route flora presentation fake in Burst-backed scalar math without adding scheduling fences or CPU animation.
Proof required: Unity import/Console, route Play Mode/player run, profiler/GC, Burst inspector/import proof if available, Frame Debugger shader-route proof, and save/load diff.
Parked work rejected: Jobified broad flora simulation, CPU vegetation physics, gameplay flora ecology, and unmeasured visual-overkill breadth.
Static verification: function-pointer compile/invoke/self-audit checks all returned true; direct managed scalar-kernel calls are absent; owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reports no whitespace errors. Build not launched because CPU preflight reported 65%, dotnet=0, csc=0.

## Polish Pass 37 Decisions
Problem: The central binary payload ledger still described the SHINOBU_267 runtime route as direct one-row `Execute()` even after P36 moved PRE_SIMULATION kernel entry to cold-compiled Burst `FunctionPointer`s.
Solution: Updated the SHINOBU_267 ledger row to state cold FunctionPointer compilation, PRE_SIMULATION pointer invocation, no `.Run()`, no same-frame `Schedule().Complete()`, and unchanged 32B CBuffer upload.
Rejected Alternatives: Leaving stale ledger prose was rejected because integrators read the central payload ledger before chat history. Rewriting historical agent-log entries was rejected because those entries are chronological audit evidence; P36/P37 supersede them at the tail.
Scalability potential: No quality behavior change. Low/Middle/High/Ultra keep the same CBuffer and shader quality curves.
Hardware Impact: Documentation-only, 0 runtime us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the central payload record aligned with the Burst-backed visual fake used by the first route.
Proof required: Static ledger/source scan now; Unity import, route run, profiler/GC, Burst/import evidence, Frame Debugger, screenshot/clip, and save/load diff still required for runtime proof.
Parked work rejected: Rewriting historical log blocks, expanding flora gameplay, and running build under CPU gate.
Static verification: Ledger and flora architecture docs now show the FunctionPointer route; `git diff --check` reports no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=0, csc=0.

## Polish Pass 38 Decisions
Problem: The new FunctionPointer invocation helpers take stack addresses. The containing `FloraAmbientSwayRuntime` class is already unsafe, but ambient class-level unsafe context makes the pointer boundary less explicit to static reviewers.
Solution: Marked `RunMockAmbientFlowKernel` and `RunCalculateFloraSwayParametersKernel` as `unsafe` directly.
Rejected Alternatives: Relying on the class-level unsafe context was rejected because the pointer-taking methods are the only new native-call boundary in P36. Reintroducing scheduled jobs was rejected for the same one-row stall reason.
Scalability potential: No quality behavior change. Low/Middle/High/Ultra use the same CBuffer and shader quality curves.
Hardware Impact: Source clarity only, 0 runtime us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the Burst-backed scalar fake auditable for early-route flora presentation.
Proof required: Static scan now; Unity import, route run, profiler/GC, Burst/import evidence, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Broad job scheduling, flora gameplay expansion, and build launch under CPU gate.
Static verification: FunctionPointer helpers are explicitly unsafe; no `.Run()`, `.Complete()`, same-frame `Schedule().Complete()`, `mockFlowJob.Execute()`, or `parametersJob.Execute()` tokens remain in runtime owner path; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reports no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=0, csc=0.

## Polish Pass 39 Decisions
Problem: P36/P38 proved FunctionPointer compile/invoke and explicit unsafe callsites, but the runnable self-audit did not yet require the IL2CPP/AOT callback ABI metadata.
Solution: Extended `FloraAmbientSwaySelfAudit` to require `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` and both `MonoPInvokeCallback` attributes for the mock-flow and parameter kernel entrypoints.
Rejected Alternatives: Checking only `BurstCompiler.CompileFunctionPointer` and `.Invoke` tokens was rejected because IL2CPP callback metadata is part of the native-call route. Adding runtime validation was rejected because this is import/AOT metadata proof, not player-frame logic.
Scalability potential: No quality behavior change. Low/Middle/High/Ultra use the same CBuffer and shader quality curves.
Hardware Impact: Editor audit only, 0 runtime us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route flora presentation fake compatible with IL2CPP native callback requirements.
Proof required: Static self-audit now; Unity import, route run, Burst/import evidence, profiler/GC, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Runtime AOT probing, build launch under CPU gate, and flora gameplay expansion.
Static verification: Runtime contains Cdecl delegate and both MonoPInvokeCallback attributes; editor self-audit contains `aotFunctionPointerAbi`; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reports no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 66%, dotnet=0, csc=0.

## Polish Pass 40 Decisions
Problem: The broad owned forbidden scan matched the editor self-audit's exact forbidden `mockFlowJob.Execute()` and `parametersJob.Execute()` strings, not runtime residue.
Solution: Split those literals inside `FloraAmbientSwaySelfAudit` while preserving the runtime-source rejection check.
Rejected Alternatives: Ignoring the false positives was rejected because scanner noise can hide a real direct managed kernel regression. Removing the validator was rejected because P36 needs a runnable proof hook.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra use the same CBuffer and shader quality curves.
Hardware Impact: Editor audit hygiene only, 0 runtime us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps direct-managed-kernel regression scans meaningful for early-route flora presentation.
Proof required: Static scan now; Unity import, route run, Burst/import evidence, profiler/GC, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Removing self-audit coverage, runtime behavior changes, and build launch under CPU gate.
Static verification: Owned P40 forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reports no whitespace errors. Build not launched because CPU preflight reported 65%, dotnet=0, csc=0.

## Polish Pass 41 Decisions
Problem: Hot parameter reads were already routed through `ReadFirstParamsReadonly(parameters)`, but the runnable self-audit did not explicitly reject a future `parameters[0]` regression. The cold black-box dump helper also returned telemetry entries through pointer index syntax, which is legal unsafe code but weaker evidence than the byte-offset `UnsafeUtility.AsRef<T>` pattern used elsewhere.
Solution: Extended `FloraAmbientSwaySelfAudit` to require `ReadFirstParamsReadonly(parameters)` and reject any runtime `parameters[0]` token. Reworked `ReadTelemetryEntryReadonly` to compute the byte offset from the ring base pointer and return `UnsafeUtility.AsRef<SwayTelemetryEntry>(entry)`.
Rejected Alternatives: Leaving this as a log-only source review was rejected because Task 20 requires a runnable proof hook. Rewriting the dump loop into managed array copies was rejected because the black-box path must stay fixed-size native memory.
Scalability potential: No visual-tier behavior change. Low/Middle/High/Ultra still use one 32-byte CBuffer and continuous shader quality curves; this pass hardens the source proof around that route.
Hardware Impact: 0 measurable hot runtime us. The telemetry-entry change affects only the fault dump path; the self-audit change is editor-only.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route flora presentation fake free of hidden `NativeArray` indexer read regressions.
Proof required: Static source scan now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Build launch under CPU gate, runtime behavior expansion, and flora gameplay scope.
Static verification: Owned forbidden scan reports no `parameters[0]`, `cursorArray[0]`, `ring[i]`, `.Run()`, `.Complete()`, same-frame `Schedule().Complete()`, direct kernel `Execute()`, vector upload, `UnityEngine.Random`, `foreach`, or `Pack=1`; runtime/editor/shader brace and full preprocessor balances are zero; Cdecl delegate count is 2 with both MonoPInvokeCallback attributes; asmdef/report JSON parse passed; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=0, csc=0.

## Polish Pass 42 Decisions
Problem: `PreSimulationTick` still contained `new` tokens for value-type job initializers, and Burst math used `new float3/new float4` constructors. These do not allocate GC memory, but the mandate and static scans treat hot `new` as suspicious unless proven.
Solution: Replaced hot job construction with `default` struct assignment and replaced vector constructors with `math.float3/math.float4`. Added `hotValueNewHygiene` to the editor self-audit so the default-init route and absence of `new float3/new float4` remain checked.
Rejected Alternatives: Keeping the value-type `new` tokens was rejected because scanner ambiguity wastes review time and can mask real reference allocations. Removing the Burst math structs was rejected because math vectors are the correct SIMD payload.
Scalability potential: No visual-tier behavior change. Low/Middle/High/Ultra still use the same DTO/CBuffer/shader fake; this pass only removes source ambiguity around hot allocations.
Hardware Impact: 0 measurable hot runtime us. The change is syntax-level allocation-proof hygiene; generated code should remain equivalent for value-type construction.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route PRE_SIMULATION lane free of GC-looking hot allocation tokens.
Proof required: Static source scan now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Build launch under CPU gate, expanding flora gameplay, and replacing SIMD vector math with scalar arrays.
Static verification: `PreSimulationTick` contains no `new ` token, runtime contains zero `new float3/new float4` constructors and zero hot job `new` constructors, editor self-audit contains `hotValueNewHygiene`, runtime/editor/shader brace and full preprocessor balances are zero, and `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 67%, dotnet=0, csc=0.

## Polish Pass 43 Decisions
Problem: The global mandate normally prefers `FloatMode.Fast` for visual math, but the extracted SHINOBU_267 XML Task 06 explicitly mandates `CompileSynchronously=true` and `FloatMode.Deterministic` so the sway time scalar remains identical across network clients. A future generic optimization pass could incorrectly flip this visual-only domain to Fast mode.
Solution: Preserved the four deterministic Burst attributes on the two job structs and two FunctionPointer entrypoints, and extended `FloraAmbientSwaySelfAudit` with `burstFloatMode` requiring deterministic/synchronous/standard-precision coverage while rejecting `FloatMode.Fast` in owned runtime.
Rejected Alternatives: Replacing deterministic mode with Fast was rejected because the batch XML is the domain authority. Removing the self-audit check was rejected because Task 20 requires a runnable proof hook.
Scalability potential: No DTO, shader, or quality curve changes. Low/Middle/High/Ultra still scale visible shader cost continuously; deterministic mode protects the global time scalar route only.
Hardware Impact: No measured CPU delta. Deterministic mode may cost more than Fast, but the workload is two one-row scalar kernels; the alternative violates the task directive.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route flora phase scalar stable across clients without adding gameplay authority or rollback hashing.
Proof required: Static source/self-audit now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Generic Fast-mode substitution, build launch under CPU/csc/dotnet gate, and rewriting historical log entries.
Static verification: SHINOBU_267 XML extraction remains 15929 chars; runtime has `DeterministicCount=4`, `FastCount=0`, `StandardPrecisionCount=4`, `CompileSyncCount=4`; owned forbidden scan is clean for `FloatMode.Fast`, `parameters[0]`, `.Run()`, `.Complete()`, direct kernel `Execute()`, vector upload, random, `foreach`, and `Pack=1`; runtime/editor/shader brace and full preprocessor balances are zero; asmdef/report JSON parse passed; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=1, csc=1.

## Polish Pass 44 Decisions
Problem: P43 locked deterministic Burst mode in source and self-audit, but the central flora route docs still only implied deterministic attributes or omitted the XML-specific reason. Integrators read `FLORA_PROCEDURAL_SWAY_FIELD.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before agent logs.
Solution: Updated both central SHINOBU_267 route cards to state that Task 06 locks all four owned Burst surfaces to `CompileSynchronously=true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`, while explicitly preserving the save/WAL/rollback/gameplay-authority exclusion.
Rejected Alternatives: Leaving the explanation in logs only was rejected because central route docs are the integration surface. Rewriting unrelated ledger entries was rejected.
Scalability potential: No runtime behavior change. Quality curves and shader ALU gates remain continuous; the doc patch only protects the deterministic time-scalar route from incorrect future optimization.
Hardware Impact: Documentation-only, 0 runtime us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Central docs now match the source/self-audit lock used by the early-route flora presentation lane.
Proof required: Static doc/source scan now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Rewriting unrelated payload ledger sections, launching build under CPU gate, and changing DTO/shader ABI.
Static verification: Doc scan shows both central SHINOBU_267 docs contain `CompileSynchronously=true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard` on the flora route; asmdef/report JSON parse passed; focused `git diff --check` reported no whitespace errors. Build not launched because CPU preflight reported 100%, dotnet=0, csc=0.

## Polish Pass 45 Decisions
Problem: The black-box fault path still used `BinaryWriter`, which made the dump ABI depend on framework writer behavior instead of an explicit HECTON-8 little-endian contract.
Solution: Replaced `DumpTelemetryOnce()` output with `FileStream` plus explicit 4-byte little-endian scalar helpers. `WriteSingleLittleEndian` serializes floats through `math.asuint`; the 12-byte header is `TelemetrySourceHash`, row count, and cursor, followed by 300 fixed 32-byte `SwayTelemetryEntry` rows.
Rejected Alternatives: Keeping `BinaryWriter` was rejected because binary telemetry is a forensic ABI, not a convenience path. Writing raw struct bytes was rejected because that would couple dump files to platform endian/packing assumptions and weaken field-level compatibility.
Scalability potential: No Low/Middle/High/Ultra visual behavior change. The hot path remains one 32B CBuffer and continuous shader quality curves; the dump path is fault-only and exists to prove postmortem data.
Hardware Impact: 0 hot runtime us. Fault-path disk output uses simple byte stores; no frame-loop allocation or gameplay route is added.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: If the route hits invalid flora math, the exported 300-frame ring is stable across x86 and ARM64 readers.
Proof required: Static source/self-audit now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, save/load diff, and actual dump-read smoke test remain pending.
Parked work rejected: Launching build under CPU/dotnet/csc gate, moving disk I/O into the hot owner frame beyond existing fault-only trigger, and changing telemetry DTO size.
Static verification: Runtime `BinaryWriter` count is 0; explicit little-endian helpers and `math.asuint` float serialization are present; editor self-audit contains `littleEndianDump`; owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; asmdef/report JSON parse passed; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 97.3%, dotnet=1, csc=1.

## Polish Pass 46 Decisions
Problem: P45 removed `BinaryWriter`, but the dump header still depended on out-of-band knowledge of field order. A forensic reader needs a magic/version/row-size guard before trusting telemetry bytes.
Solution: Added a fixed 24-byte little-endian header: `"S267"` magic (`0x37363253`), version `1`, `TelemetrySourceHash`, row size `32`, row count, and cursor. The row payload remains 300 fixed `SwayTelemetryEntry` records, field-by-field little-endian.
Rejected Alternatives: Keeping the 12-byte P45 header was rejected because it lacked magic/version/row-size. Dumping raw DTO bytes was rejected because it would couple forensic files to platform endian and struct-copy assumptions.
Scalability potential: No visual-tier behavior change. Low/Middle/High/Ultra remain one 32B CBuffer plus continuous shader gates; this change affects fault evidence only.
Hardware Impact: 0 hot runtime us. The added header is six scalar writes on a fault-only file path.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Route fault dumps can be rejected or parsed deterministically by tooling before reading rows, avoiding silent postmortem misreads.
Proof required: Static source/self-audit now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, save/load diff, and actual dump-read smoke test remain pending.
Parked work rejected: Build launch under CPU/dotnet/csc gate, telemetry DTO size changes, and gameplay-state inclusion.
Static verification: Runtime header constants and writer calls for magic/version/source/row-size are present; runtime `BinaryWriter` count remains 0; editor self-audit requires the header through `littleEndianDump`; owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; asmdef/report JSON parse passed; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 95.7%, dotnet=1, csc=1.

## Polish Pass 47 Decisions
Problem: P46 added `SwayTelemetryEntrySizeBytes` to the dump header, but `ValidateFloraSwayLayouts()` still used independent `32` literals. That left a possible future drift between advertised dump row size and actual DTO validation.
Solution: Changed the validator to compare `paramsSize` against `FloraSwayParamsSizeBytes` and `telemetrySize` against `(int)SwayTelemetryEntrySizeBytes`. The existing editor self-audit already calls this validator, so row-size/header drift now fails the audit.
Rejected Alternatives: Leaving the numeric literals was rejected because forensic row-size is now part of the file ABI. Adding a separate duplicate editor check was rejected because the runtime validator is the single owner of DTO size proof.
Scalability potential: No visual-tier behavior change. Low/Middle/High/Ultra remain shader-quality curves and one CBuffer; this is ABI proof only.
Hardware Impact: 0 hot runtime us. Validator-only source hardening.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Route fault dumps now advertise a row size tied to the same validator that protects the actual telemetry DTO.
Proof required: Static source/self-audit now; Unity import/Console, selected route run, profiler/GC, Burst/import proof, Frame Debugger, screenshot/clip, save/load diff, and dump-read smoke test remain pending.
Parked work rejected: Build launch while dotnet/csc are active, telemetry DTO size changes, and runtime file parsing.
Static verification: Validator source compares params size to `FloraSwayParamsSizeBytes` and telemetry size to `(int)SwayTelemetryEntrySizeBytes`; owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 46.3%, dotnet=1, csc=1.

## Polish Pass 48 Decisions
Problem: Task 18's live vertex-color debug path existed, but `FloraAmbientSwaySelfAudit` did not explicitly require it. That left a documentation-only proof gap for technical-artist validation.
Solution: Added `vertexColorDebug` to the self-audit. It now checks the editor toggle, `Shader.SetGlobalFloat(DebugId...)`, `SceneView.RepaintAll()`, shader `_HectonFloraVertexColorDebug`, the vertex-color payload assignment, and the raw debug fragment return.
Rejected Alternatives: Leaving Task 18 in status/log only was rejected because Task 20 requires a rigorous proof hook. Adding runtime replacement materials was rejected because the debug lane must be editor-only and non-invasive.
Scalability potential: No player visual-tier behavior change. The debug path is editor-only; Low/Middle/High/Ultra runtime rendering remains the same continuous shader route.
Hardware Impact: 0 player us. Editor-only self-audit and toggle proof.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Technical artists can prove Vertex Color red stiffness authoring without adding CPU animation or replacement runtime materials.
Proof required: Static source/self-audit now; Unity import/Console and actual SceneView toggle proof remain pending under Unity execution.
Parked work rejected: Runtime material swapping, replacement shader instantiation, build launch under CPU/dotnet/csc gate.
Static verification: Static scan shows `Toggle Vertex Color Debug`, `Shader.SetGlobalFloat(DebugId...)`, `_HectonFloraVertexColorDebug`, `half4(input.color)`, and `return half4(input.biolumColor.rgb, 1.0h)` are present; owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 70.1%, dotnet=1, csc=1.

## Polish Pass 49 Decisions
Problem: The editor layout validator and self-audit success messages still printed hardcoded `32` sizes. After P46/P47 made row size a file ABI, stale proof strings became misleading evidence.
Solution: Changed both success messages to print the actual `paramsSize`, `telemetrySize`, and `profileSize` returned by `ValidateFloraSwayLayouts()`.
Rejected Alternatives: Keeping hardcoded success text was rejected because the audit output must reflect measured layout values. Adding another separate size formatter was rejected as unnecessary editor-only complexity.
Scalability potential: No player visual-tier behavior change. This is editor evidence hygiene.
Hardware Impact: 0 player us.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Editor validation output now reflects actual DTO sizes when artists or integrators run the flora tools.
Proof required: Static source scan now; Unity Editor menu invocation remains pending.
Parked work rejected: Build launch under CPU/dotnet/csc gate and DTO size changes.
Static verification: Editor scan reports no hardcoded `Params=32`, `Telemetry=32`, or `Profile=32` success strings; owned forbidden scan is clean; editor brace/preprocessor balance is zero; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 72.8%, dotnet=1, csc=1.

## Polish Pass 50 Decisions
Problem: `ValidateFloraSwayLayouts()` still used `Marshal.OffsetOf` for the primary DTO offset proof. The runtime layout mandate names `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` as the required verifier surface, and interop-offset calls in a runtime assembly are a weaker proof than Unity's native layout API.
Solution: Replaced the `FloraSwayParamsDTO` offset checks with `GetFieldOffset<FloraSwayParamsDTO>()` backed by `UnsafeUtility.GetFieldOffset(field)`. Extended `FloraAmbientSwaySelfAudit` with `layoutOffsetApi`, requiring both DTO offset checks to use the helper and rejecting any `Marshal.OffsetOf` regression in owned runtime source.
Rejected Alternatives: Leaving `Marshal.OffsetOf` was rejected because it violates the selected ARM64 layout mandate style. Moving all layout validation into chat/log prose was rejected because Task 20 requires a runnable editor proof hook.
Scalability potential: No Low/Middle/High/Ultra visual behavior changes. The route still scales through continuous shader quality gates and one 32-byte CBuffer; this pass only hardens the binary-layout proof.
Hardware Impact: 0 hot runtime us. Editor/static validator only; runtime frame path is unchanged.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Keeps the early-route flora CBuffer ABI auditable through the same Unity native layout API used by the wider ARM64 verifier surface.
Proof required: Static source/self-audit now; Unity import/Console, editor menu invocation, route run, profiler/GC, Frame Debugger, screenshot/clip, and save/load diff remain pending.
Parked work rejected: Build launch under CPU/dotnet gate, unrelated `Marshal.OffsetOf` cleanup in other agents' domains, and DTO ABI changes.
Static verification: SHINOBU_267 XML extraction remains 15929 chars with 20 task labels; owned forbidden scan reports no `Marshal.OffsetOf`, `BinaryWriter`, `FloatMode.Fast`, `parameters[0]`, `.Run()`, `.Complete()`, direct scalar-kernel `Execute()`, vector upload, random, `foreach`, `Pack=1`, hot vector constructors, or hardcoded layout success strings; runtime/editor brace and preprocessor balances are zero; `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because CPU preflight reported 100%, dotnet=1, csc=0.

## Polish Pass 51 Decisions
Problem: The runtime layout validator proved `FloraSwayParamsDTO` offsets, but the same domain now owns binary ABI contracts for flow state, tuning, biome profiles, and telemetry dump rows. A partial offset matrix could allow a profile or dump-reader drift while the self-audit still passed.
Solution: Expanded `ValidateFloraSwayLayouts()` to verify sizes, minimum alignment, and every field offset for all five SHINOBU_267 DTOs using `UnsafeUtility.GetFieldOffset`. Added explicit size constants for flow state, tuning, and biome profile DTOs so the validator no longer has an isolated `profileSize == 32` literal. Extended `layoutOffsetApi` in the editor self-audit to require representative full-matrix coverage.
Rejected Alternatives: Checking only the CBuffer DTO was rejected because telemetry/profile bytes now cross disk and Vault boundaries. Adding a second editor-only validator was rejected because runtime source already owns the single layout proof route. Raw struct dumps were rejected earlier and remain rejected because field-wise little-endian serialization is the safer forensic ABI.
Scalability potential: No Low/Middle/High/Ultra behavior changes. Continuous shader quality gates and the 32-byte CBuffer remain untouched; this pass only hardens binary proof.
Hardware Impact: 0 hot runtime us. Static/editor validator work only; no frame-loop math or upload path changed.
First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome.
Route impact: Profile hydration, Vault rows, CBuffer upload, and dump-reader expectations now share one full DTO offset matrix instead of a primary-DTO-only proof.
Proof required: Static source/self-audit now; Unity import/Console, editor self-audit invocation, selected route run, profiler/GC, Frame Debugger, screenshot/clip, save/load diff, and dump-read smoke test remain pending.
Parked work rejected: Build launch before it is useful, unrelated DTO cleanup in sibling domains, changing DTO sizes, and adding managed reflection to hot phases.
Static verification: Owned forbidden scan is clean; runtime/editor/shader brace and preprocessor balances are zero; runtime has 29 `GetFieldOffset<...>` checks, explicit size constants for flow/tuning/profile DTOs, no `profileSize == 32` literal, no `Marshal.OffsetOf`, asmdef JSON parse passed, and `git diff --check` reported no whitespace errors beyond LF/CRLF warnings. Build not launched because this was static ABI proof work.

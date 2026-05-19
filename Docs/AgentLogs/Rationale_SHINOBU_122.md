# SHINOBU_122 Rationale - Biome Transition Manager

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="SHINOBU_122">`.
Solution: Treat XML task count as 0, record the batch defect, and constrain implementation to user-provided objective plus domain 18 from `Docs/Actual Domains of Project.txt`.
Rejected Alternatives: Do not borrow neighboring agent prompts; strict parsing forbids architectural leakage from adjacent XML blocks.
Scalability potential: Low, Middle, High, and Ultra all require the same deterministic owner-local blend contract; quality changes only alter how many biome centers are interpolated and how much visual/audio richness is consumed.
Hardware Impact: Prevents wasted implementation against the wrong prompt. Runtime impact unchanged.

Problem: A later CLI extraction returned the actual `<AGENT_PROMPT id="SHINOBU_122">` with 20 tasks after the initial extraction failed.
Solution: Corrected the working state from task count 0 to 20 and promoted the XML block to primary directive.
Rejected Alternatives: Continuing from the fallback user summary would miss mandatory DTO alignment, mock traversal, Vault publication, acoustic staging, and editor tooling tasks.
Scalability potential: Low blends 1 biome, Middle blends 2, High blends 3, Ultra blends 4 through continuous `GlobalQualityWeight` math.
Hardware Impact: Correct directive prevents rework; runtime target remains zero physics broadphase and 0 B/frame GC.

Problem: `biome_transition_matrix.h8bin` is not present in `Assets`, `Data`, or `StreamingAssets`, and the binary payload ledger does not classify it as active runtime content.
Solution: Treat CSV plus deterministic mock biomes as the current source lane. `BiomeAtmosphereCsvIngestJob` parses `Assets/_Project/Data/World/biome_atmosphere_rules.csv` from a Vault byte scratchpad; `BuildEmergencyMockBiomesJob` writes four deterministic centers when CSV/payload data is absent.
Rejected Alternatives: Do not fabricate or hand-patch an `.h8bin`; binary payloads with headers/checksums must come from their baker. Do not crash boot while waiting for a future baker.
Scalability potential: Low reads the same data but evaluates fewer centers and blends fewer weights; Middle/High/Ultra progressively allow larger scan/blend/cadence budgets.
Hardware Impact: Missing-payload path becomes a cold scheduled job instead of repeated file probes or managed fallbacks. i3/MX350 avoids boot stalls and per-frame IO.

Problem: Legacy biome switching could regress into BoxCollider/OnTrigger routes.
Solution: Static scans found no `BiomeVolume.cs`, `AtmosphereChanger.cs`, or biome-transition `OnTrigger*` scripts. The new owner route is `BiomeTransitionManagerRuntime` -> Vault DTOs -> `BiomeChangedSignal`/shader globals. Existing collision triggers in audio, hazards, construction, and nav proxies are outside the biome-transition owner and were not deleted.
Rejected Alternatives: Deleting unrelated BoxColliders would break gameplay collision and violates owner-local boundaries.
Scalability potential: All hardware tiers use mathematical proximity; weak hardware reduces ALU work through quality curves, not physics broadphase.
Hardware Impact: Biome switching no longer depends on thousands of trigger AABB checks. Estimated saved cost: 20-200 us/frame in dense scenes depending trigger count.

Problem: Old biome transition fog DTOs used packed sequential layout and the new task mandates explicit ARM64 layout.
Solution: Replaced hot DTOs with explicit layouts. Primary `BiomeStateDTO` is 64B: `BiomeHash` offset 0, `FogColor` offset 16, `AbsorptionParams` offset 32, `AmbientAudioVolume` offset 48, explicit pad through byte 63. Added `BiomeTransitionNativeLayout.Validate()` and editor guard.
Rejected Alternatives: `Pack=1`, auto-layout, or C# properties. Those create unaligned ARM64 reads and can trigger CS1612/defensive copies when NativeArray elements are mutated.
Scalability potential: Low/Middle/High/Ultra share one DTO ABI; quality changes math volume only, not memory layout.
Hardware Impact: 64B stride maps to one L1 cache line for the primary state DTO and avoids unaligned NEON fetch penalties on Quest-class ARM64.

Problem: The first implementation completed CSV, mock, and traversal jobs synchronously.
Solution: Seed jobs are now scheduled and completed only after `IsCompleted`; mock traversal is chained as an input dependency and read by `EvaluateBiomeProximityJob`/`RecordBiomeTransitionTelemetryJob` through a Vault AUP buffer. Shutdown remains allowed to complete outstanding handles.
Rejected Alternatives: Blocking `Complete()` in `FastTick`; direct CPU mock position writes; Transform-only fallback.
Scalability potential: Low hardware can run the solver at 5Hz without main-thread stalls; Ultra can schedule every frame.
Hardware Impact: Removes hot-path wait risk. Estimated saved latency spike: 50-500 us when Burst worker scheduling is busy.

Problem: `UninitializedMemory` buffers can contain arbitrary bits and cannot be read before deterministic initialization.
Solution: CSV and emergency mock seed jobs now write counters from `default` instead of reading previous garbage. Tuning is set to a deterministic default before runtime reads. No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields were added.
Rejected Alternatives: `ClearMemory` on every Vault buffer; local component-owned persistent NativeArrays.
Scalability potential: Same data sovereignty across tiers; low devices avoid OS zero-fill cost.
Hardware Impact: Avoids clearing roughly 11 KB on boot/reload and prevents random active-count reads from uninitialized memory.

Problem: Biome atmosphere must reach visual and audio consumers without cross-domain concrete calls.
Solution: Publish path is split: Burst writes `CurrentAtmosphereDTO`, `BiomeBlendMaskDTO`, `BiomeAcousticStageDTO`, and a six-slot `float4` shader payload in Vault; the runtime mirrors completed payload into global shader vectors and standard fog/extinction globals. Audio consumes the staged DTO or the existing `BiomeChangedSignal`, not direct `AudioSource` mutation.
Rejected Alternatives: Direct references to rendering/audio managers, managed delegates, or per-biome `AudioSource` fade scripts.
Scalability potential: Low uses one dominant biome and slower cadence; Ultra exposes four weights, dither parameters, and full atmospheric coefficients to shaders.
Hardware Impact: Presentation handoff is six `float4` copies and a handful of shader globals after job completion; no physics or managed event fanout in the solver.

Problem: Rollback/determinism mandates reject Unity time and random state in authoritative blend math.
Solution: Removed `Time.*` from the new biome manager path. Frame comes from `SystemDispatcher` snapshot when available, with a local monotonic fallback only when dispatcher is unavailable. Jobs use `FloatMode.Deterministic`; no UnityEngine.Random or managed RNG is used.
Rejected Alternatives: `Time.frameCount`, `Time.deltaTime`, or nondeterministic noise in the solver. Shader-side dither remains presentation-only.
Scalability potential: All tiers use the same deterministic state hash; only cadence and active weights vary by `GlobalQualityWeight`.
Hardware Impact: Deterministic float mode costs more than Fast mode but protects net sync; saved physics work pays for it.

Problem: Designers need control without recompilation.
Solution: Added `Biome Transition Tuner` UI Toolkit window with radius, hardware-quality override, cadence, dither strength, gizmo, mock traversal, layout validation, CSV reload, and black-box dump controls. CSV rules are human-readable and hot-reloadable through the editor button.
Rejected Alternatives: Hardcoded constants only; runtime IMGUI/debug overlay.
Scalability potential: Low/Middle/High/Ultra can be tuned live through continuous sliders rather than binary tier toggles.
Hardware Impact: Editor-only allocations do not enter gameplay hot path; runtime reads one 64B tuning DTO.

Problem: Build verification is required but the CPU gate forbids compiling while the machine is under load.
Solution: Checked CPU and compiler processes before build: CPU average `53.26%`, `dotnet/csc` count `0`. Build was deferred per AGENTS rule. Static scans and `git diff --check` were run instead; diff check reports only CRLF normalization warnings.
Rejected Alternatives: Launching `dotnet build` against the explicit >50% CPU prohibition.
Scalability potential: No runtime impact.
Hardware Impact: Protects the developer machine from compile contention; compile proof remains pending until CPU <=50%.

Problem: `NativeArrayOptions.UninitializedMemory` made the tuning DTO unsafe if read before deterministic seed.
Solution: Added `_tuningInitialized` and `EnsureTuningDefaultNoRead()` so `BiomeTransitionTuningDTO` is written once after Vault handle creation without reading previous bytes. Editor writes mark the runtime tuning initialized so user sliders are not overwritten by a later seed pass.
Rejected Alternatives: Reading `tuning[0].LowCadenceHz` as a validity test; that touches undefined Vault bytes. Switching the tuning buffer back to `ClearMemory` would violate Task 15.
Scalability potential: Low/Middle/High/Ultra now start from deterministic cadence, dither, radius, and scan-scale values; designers can still override continuously.
Hardware Impact: Keeps zero-init savings while removing undefined branch behavior on i3/MX350 and ARM64.

Problem: Signal lane and scalability-limit plumbing had cold/hot ambiguity.
Solution: Forced `BiomeChangedSignalWriter` creation during Vault preparation instead of first evaluator schedule, added `MaxCenterScanScale` to `EvaluateBiomeProximityJob`, and exposed it through the editor tuner. The evaluator chooses a nearby sector start index before applying the quality-scaled scan budget.
Rejected Alternatives: Lazy queue allocation in first FastTick; unused DTO field; always scanning from index zero on low quality.
Scalability potential: Low devices can collapse distance evaluation toward one center from a sector-relevant start point; Ultra scans the full authored budget and blends up to four weights.
Hardware Impact: Removes a first-transition allocation spike and preserves deterministic low-tier ALU shedding.

Problem: The first SHINOBU_122 signal producer used `GlobalSignals.BiomeChangedSignalWriter`, but static source shows current consumers read `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()` and no owner drains `_biomeChangedSignals` direct queue into that snapshot.
Solution: Changed the cold lane warmup and evaluator writer to `SignalBus<BiomeChangedSignal>.ParallelWriter`. The job still writes a 64-byte unmanaged `BiomeChangedSignal` from Burst, but now it enters the typed lane that consumers already observe.
Rejected Alternatives: Adding a new bridge drain in `GlobalSignals.cs` would modify core global infrastructure from the biome domain. Keeping direct queue output would silently drop the signal for most modern consumers. Managed C# events remain rejected.
Scalability potential: Low emits at the same dirty-only cadence as the solver, while middle/high/ultra consumers can read the existing typed snapshot without polling the biome runtime.
Hardware Impact: No additional per-frame allocation; the typed lane already owns bounded capacity/load shedding. This fixes correctness without adding a new global route or broad core edit.

Problem: The biome transition route added and consumed global surfaces without a formal route-card artifact.
Solution: Added `SHINOBU_122_BIOME_TRANSITION_STATE` route-card and self-review result `YELLOW / PENDING VERIFICATION` to `Docs/Tasks/Status_SHINOBU_122.md`. It names the existing `SignalBus<BiomeChangedSignal>` lane, Vault BufferIDs `71220..71231`, capacities, failure modes, telemetry fields, shutdown behavior, stale-handle behavior, and runtime proof still missing.
Rejected Alternatives: Reporting the route as `GREEN` from static scans only; hiding route-card evidence in chat; adding a new registry slot.
Scalability potential: Same continuous quality curve; route-card prevents later low/high binary forks or catch-all signal misuse.
Hardware Impact: Documentation/static governance only; no gameplay cost.

Problem: Task 20 required an embedded self-audit, not just a chat/log claim.
Solution: Added `TryRunSelfAudit(out faultFlags, out weightSumError)` to validate native layout, snapshot readiness, normalized weight error, and blend-count bounds. The UI Toolkit validator now runs this routine after layout validation.
Rejected Alternatives: Relying only on final markdown. A report cannot catch a later bad CSV reload or uninitialized payload.
Scalability potential: Same audit applies across low, middle, high, and ultra because the expected invariant is weight sum = 1.0 regardless of active blend count.
Hardware Impact: Editor/cold validation only; 0 us gameplay hot path.

Problem: Second and third build-gate checks remained above the compile threshold.
Solution: Attempted guarded build command, but the guard threw before `dotnet build` launched at CPU `75.75%`. A later recheck showed CPU `56.91%`, `dotnet/csc` count `0`; compile remains deferred by policy.
Rejected Alternatives: Ignoring the AGENTS >50% CPU rule to force a proof artifact.
Scalability potential: No runtime impact.
Hardware Impact: Prevents compile contention on the shared workstation.

Problem: Guarded build eventually launched and failed outside the BIOME_TRANSITION_MANAGER domain.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore /m:1 /v:minimal /p:UseSharedCompilation=false` only after the CPU gate opened. Build errors are missing external DTOs in `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` (`UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`) and `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs` (`VrComfortProfileDTO`, `ComfortTelemetryEntry`). `BiomeTransitionFogBlendJobs.cs` is included in `Hecton8.Core.csproj` and produced no reported compiler error; the new biome runtime/editor files are absent from stale generated csproj files and require Unity regeneration/import for direct compile proof.
Rejected Alternatives: Patching Visor or Somatic ownership from the biome domain; editing generated csproj metadata to fake Unity import proof.
Scalability potential: No runtime impact.
Hardware Impact: Compile wall is external; SHINOBU_122 code remains statically verified, but full build proof is blocked until upstream domain dependencies are repaired.

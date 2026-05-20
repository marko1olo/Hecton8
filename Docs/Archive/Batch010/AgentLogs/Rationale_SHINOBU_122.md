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

Problem: `BiomeTransitionManagerRuntime` still had `GlobalRegistry.DataVault` and `GlobalRegistry.Player` lookup paths reachable from ordinary `FastTick` through `EnsureVaultBuffers`, `TryResolveRuntimeBuffers`, and `TryResolvePlayerAup`.
Solution: Added cached `_vault` and `_playerContext` dependencies, bound them during cold lifecycle calls, and subscribed to `IGlobalRegistryHotSwapRefListener` so DataVault/Player replacement rebinds without polling. `TryResolveRuntimeBuffers` now resolves only against cached `_vault`; `TryResolvePlayerAup` reads cached `IPlayerRuntimeContext` or the serialized/cold-resolved transform. `OnDrawGizmos` now uses cached Vault tuning instead of the static editor facade.
Rejected Alternatives: Per-frame registry fallback hidden inside `TryResolve*`; adding a new registry slot; changing Core registry or DataVault APIs from the biome domain; retry polling after the first gameplay frame.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged: scan budget, blend count, and cadence still scale continuously through `GlobalQualityWeight`. The change only removes authority lookup overhead from the runtime path.
Hardware Impact: Removes two to three static service reads from every eligible biome solver tick and prevents service-locator drift from becoming the live player/Vault state bus. Estimated direct saving is sub-microsecond per tick, but the important gain is architectural: no hidden registry coupling inside the solver loop.

Problem: The registry fix still left a cold-work leak into `FastTick`: the tick could call `EnsureVaultBuffers()` and the shared seed method could reach CSV path/file IO if initialization was delayed.
Solution: Removed `EnsureVaultBuffers()` from `FastTick` and split seed flow into `TrySeedBiomeData()` for cold scheduling and `TryFinalizeSeedBiomeData()` for hot completion. CSV file probes and file reads remain in Start/editor reload/hot-swap paths. The tick path now only completes an already-finished seed chain.
Rejected Alternatives: Keeping one convenience seed method for both lifecycle and tick; lazy Vault handle creation inside `FastTick`; per-frame retries for missing DataVault.
Scalability potential: Low/Middle/High/Ultra cadence behavior is unchanged after seed; weak hardware no longer risks cold file-system work during the first gameplay tick.
Hardware Impact: Removes potential cold IO and Vault handle creation spikes from the gameplay frame. Worst-case avoided hitch is file-system dependent; on i3/MX350 this is a correctness requirement, not a microsecond-only optimization.

Problem: The previous seed split still allowed `FastTick` to schedule the emergency mock fallback after a completed CSV parse produced zero active biomes, and `BuildEmergencyMockBiomesJob` wrote tuning defaults that could overwrite editor-authored tuning.
Solution: Chained `BuildEmergencyMockBiomesJob` behind `BiomeAtmosphereCsvIngestJob` during cold scheduling with `OnlyWhenCounterEmpty`. The conditional fallback reads the CSV counter after the dependency completes and writes deterministic mock biome states only when no CSV row was accepted. Tuning is now initialized solely by `EnsureTuningDefaultNoRead()` or the editor facade, not by the mock biome job.
Rejected Alternatives: Completing CSV in Start to branch synchronously; leaving fallback scheduling in `FastTick`; allowing the mock biome writer to reset designer tuning.
Scalability potential: Low/Middle/High/Ultra seed source is still the same unmanaged state table; only the runtime evaluation cadence and blend count scale through `GlobalQualityWeight`.
Hardware Impact: Removes the last cold seed scheduling branch from the solver tick and prevents accidental tuning reset. Direct steady-state saving is sub-microsecond; avoided hitch is first-run Burst/file-order dependent.

Problem: SHINOBU_122 scheduled Vault-writing seed/pipeline jobs but did not register those handles in the global owner-job ledger, and local finalization still used direct `.Complete()` calls.
Solution: Added `OwnerSystem = SystemID.WorldStreaming`, registered seed and pipeline handles through `H8Memory.RegisterActiveJob`, and replaced direct completion with `DispatcherJobSwap.TryFinalizeCompleted` in ready-only finalize paths plus `DispatcherJobSwap.TryComplete(..., forceComplete: true)` in teardown/vault replacement.
Rejected Alternatives: Leaving only private `_seedHandle`/`_pipelineHandle` ownership; adding a new Core owner enum; blocking inside `FastTick`; editing H8Memory from the biome domain.
Scalability potential: Low/Middle/High/Ultra math is unchanged, but owner-job tracking now gives scene transition and DataVault release one authoritative fence path regardless of cadence.
Hardware Impact: Runtime ALU cost unchanged; teardown correctness improves. Static scan now reports no direct `.Complete()` calls in SHINOBU_122 files, removing a class of accidental main-thread stall regressions.

Problem: Solver cadence used `FastTick` delta-time accumulation. It did not call Unity `Time.*`, but rollback-sensitive scheduling should be based on deterministic simulation frames rather than variable frame duration.
Solution: Replaced `_cadenceAccumulator`/seconds cadence with `_lastScheduledFrame` and `ResolveCadenceFrameStep()`. The continuous quality curve still interpolates 5Hz to 60Hz, then maps to a deterministic frame step from 12 frames to 1 frame using the dispatcher frame snapshot.
Rejected Alternatives: Keeping a delta-time accumulator because it is "not Unity Time"; binary low/high tick-rate branches; scheduling every frame on all hardware.
Scalability potential: Low/Middle/High/Ultra keep the same continuous curve while cadence becomes rollback-friendly: weak hardware sheds updates by deterministic frame skip, high hardware approaches every-frame atmospheric solve.
Hardware Impact: ALU savings unchanged, but frame cadence is reproducible. Weak devices still reach the 5Hz target without introducing nondeterministic accumulator drift.

Problem: Mock traversal still used a mutable serialized float accumulator and was unreachable in forced mock mode when no player AUP/Transform existed, which breaks rollback repeatability and CI/editor fallback coverage.
Solution: Added `MockTraversalPeriodFrames = 600` and derive mock phase as `(simulationFrame % 600) / 600`. `FastTick` now resolves tuning first, permits forced/editor mock traversal without a player Transform, and uses a zero AUP placeholder only until `MockCameraTraversalJob` writes the authoritative mock AUP into the Vault buffer.
Rejected Alternatives: Keeping float accumulation because it is "debug only"; requiring a scene Player object for the fallback path; using Unity `Time.time` or editor frame counters.
Scalability potential: Low/Middle/High/Ultra keep the same mathematical mock route. Weak hardware still runs at quality-cadenced frame steps; Ultra can sample the same deterministic traversal every frame.
Hardware Impact: Removes accumulator drift and one hard dependency from the fallback path. Direct runtime saving is sub-microsecond; correctness gain is deterministic replay and CI coverage without scene physics/Transform setup.

Problem: A sparse center table or low-quality scan could produce zero positive weights after sector/radius gates, leaving `DominantBiomeHash = 0` and forcing visual/audio consumers into fallback constants instead of the nearest biome.
Solution: `EvaluateBiomeProximityJob` now tracks the nearest scanned biome center in AUP-local float space and inserts it at weight 1 when all radius/sector weights collapse to zero. The event/counter flags mark `FlagNearestFallback`, while normal multi-biome interpolation is unchanged.
Rejected Alternatives: Full active-center scan on weak hardware; widening every biome radius to hide the gap; returning hash 0 and depending on shader defaults.
Scalability potential: Low quality now collapses to an explicit nearest-neighbor biome, Middle/High/Ultra still use 2-4 weighted lanes when valid weights exist. No binary tier switch was added.
Hardware Impact: Adds two scalar writes and one conditional per scanned center, plus one state lookup only on all-zero collapse. This is cheaper than increasing the scan budget and keeps weak-device behavior valid.

Problem: The deterministic cadence gate wrote `_lastScheduledFrame` before proving that a real player AUP or forced mock AUP path existed. A missing-player frame with mock disabled could consume the cadence slot without scheduling the solver.
Solution: Moved `_lastScheduledFrame = frame` after the player/mock eligibility check and immediately before the pipeline input blit/schedule path.
Rejected Alternatives: Accepting missed retries as harmless; scheduling with zero AUP when mock is disabled; reverting to seconds accumulation.
Scalability potential: Low/Middle/High/Ultra cadence remains unchanged for valid schedules. Invalid frames no longer mask the next eligible frame.
Hardware Impact: No extra ALU. Removes a small starvation edge case during player service startup or scene transition.

Problem: `EvaluateBiomeProximityJob` could insert a center candidate even when `FindStateIndex(center.BiomeHash)` returned `-1`. That allowed a biome hash with no fog/audio state to become dominant and emit through `SignalBus<BiomeChangedSignal>`.
Solution: Require `stateIndex >= 0` before `InsertCandidate`; otherwise mark `FlagInvalidInput` and continue scanning. The nearest fallback already performs the same state validation.
Rejected Alternatives: Letting `BlendAtmosphereJob` silently fail to accumulate and rely on fallback colors; emitting state-less biome hashes and forcing consumers to guard them.
Scalability potential: Low/Middle/High/Ultra are unchanged for valid data; malformed or partial Vault/CSV content now degrades to nearest valid state or explicit invalid flag.
Hardware Impact: Adds one branch after the existing state lookup only for positive-weight candidates. It prevents downstream shader/audio churn caused by invalid dominant hashes.

Problem: The shader payload Vault buffer was sized for eight `float4` slots, but `PublishAtmosphereDataJob` wrote only six. Because the buffer is deliberately `UninitializedMemory`, slots 6-7 could contain stale data if a future CBuffer consumer copied the whole payload.
Solution: Require `ShaderPayloadFloat4Count` before publishing, write slots 0-5 as before, add slot 6 for dominant hash/frame/flags, and deterministically zero slot 7. Runtime shader-global mirroring also refuses partial payloads.
Rejected Alternatives: Leaving slots 6-7 unwritten because current mirror reads only 0-5; switching the buffer to `ClearMemory`; shrinking the payload and breaking future CBuffer layout.
Scalability potential: Low/Middle/High/Ultra are unchanged; all tiers now publish a deterministic fixed-width payload.
Hardware Impact: Adds two `float4` `MemCpy` operations per solver publish. Cost is negligible compared to the value of preserving zero-init and avoiding stale shader data.

Problem: Black-box dump serialization used `BinaryWriter`, hiding byte order and initially risking mismatch against the explicit telemetry DTO offsets.
Solution: Replaced the writer with a stack `Span<byte>` record and explicit little-endian integer/float encoders. The record is exactly 64 bytes: AUP grid offsets 0/8/16, local floats 24/28/32, padding 36-47 zeroed, dominant hash 48, blend count 52, CPU microseconds 56, state hash 60.
Rejected Alternatives: Keeping `BinaryWriter` because dump is cold; memcpying raw struct bytes without endianness; writing variable-length records.
Scalability potential: Low/Middle/High/Ultra dump format is identical, so QA crash forensics can parse every hardware tier with one decoder.
Hardware Impact: Cold crash/editor path only. It removes managed writer abstraction and makes the 300-record dump deterministic at 19.2 KB.

Problem: `EvaluateBiomeProximityJob` still used a hash scan over `BiomeStateDTO[]` for normal weighted candidates after the sector/center scan. With 64 active centers this remains bounded, but it violates the spirit of Task 12 because the hot candidate path is O(scanCount * stateCount) instead of using the center record as the owner-local route to its state payload.
Solution: Added `BiomeCenterDTO.StateIndex` at byte offset 48 inside the existing 64-byte center record. CSV and emergency mock seed jobs write this index. The evaluator validates `States[StateIndex].BiomeHash == Center.BiomeHash` and only falls back to a hash scan for stale or malformed buffers. Nearest fallback now tracks only a nearest center whose state index has already validated.
Rejected Alternatives: Adding a NativeHashMap from biome hash to state index would create another Vault surface and a new init/failure path. Keeping the hash scan was simpler but wasted ALU on every contributing center. Embedding a managed dictionary is rejected outright.
Scalability potential: Low quality still scans a quality-scaled center subset and collapses to one nearest biome; Middle/High/Ultra increase scan/blend lanes without multiplying by state-table length in the normal path.
Hardware Impact: On i3/MX350, worst-case normal lookup drops from up to 64 state comparisons per candidate to one indexed load plus one hash compare. At Ultra with 64 centers, this removes hundreds of scalar compares per scheduled solve.

Problem: Raw `HomeostasisBrain.GlobalQualityWeight` fed cadence, scan count, and blend gates directly. The curve was continuous, but small thermal oscillations could still flip rounded frame-step/scan ceilings between adjacent values on consecutive eligible frames.
Solution: Added deterministic frame-based quality slew in `BiomeTransitionManagerRuntime`: 0.015 hysteresis band, downgrade ramp up to 1.0 over 60 simulation frames, and upgrade ramp up to 1.0 over 180 frames. It uses dispatcher frame identity and no `Time.deltaTime`; the evaluator still receives a continuous float. If the dispatcher frame rewinds under rollback, the filter resynchronizes to the target quality instead of unsigned-underflow stepping.
Rejected Alternatives: Binary low/high tiers; seconds accumulator; raw quality pass-through; delaying all downgrades for three seconds, which protects visuals but risks thermal runaway.
Scalability potential: Low, Middle, High, and Ultra remain one continuum. Weak-device downgrades still shed cost quickly; high-end recovery is deliberately slower to avoid visual/cadence flicker.
Hardware Impact: Adds a few scalar operations on scheduled frames only. It prevents cadence thrash and redundant job scheduling transitions under thermal noise.

Problem: Task 16 required a 300-frame black box, but quality cadence intentionally skips solver frames at low `GlobalQualityWeight`. The previous telemetry ring advanced only when the full solver chain ran, leaving forensic gaps during the exact weak-hardware mode where cadence drops toward 5Hz.
Solution: Added `RecordCadenceSkippedTelemetry()` in the host tick. When a frame is inside the deterministic cadence gate, it writes one 64-byte telemetry record using the current player AUP or cached mock AUP, cached dominant hash/blend count, frame-specific state hash, `CpuMicroseconds=0`, and `FlagCadenceReused`.
Rejected Alternatives: Scheduling the full Burst solver just to fill telemetry; scheduling a separate telemetry job every skipped frame without a dependency owner; managed log lines; accepting 5Hz black-box coverage on weak devices.
Scalability potential: Low quality now keeps 60Hz forensic continuity while solver math remains 5Hz. Middle/High/Ultra naturally record either reused or newly solved frames as cadence approaches every frame.
Hardware Impact: Adds one fixed 64-byte NativeArray write on cadence-skipped frames. On i3/MX350 this is cheaper than waking the evaluator/blend/publish job graph and preserves crash forensics.

Problem: `TryRunSelfAudit` used `max(maskSum, atmosphereSum)` before measuring weight-sum error. If one vector summed to 1.0 and the other was malformed below 1.0, the bad side could be hidden.
Solution: Compute `abs(maskSum - 1)` and `abs(atmosphereSum - 1)` independently, then report the max error and fail if either sum or the final error is non-finite or exceeds `0.001`.
Rejected Alternatives: Trusting only the atmosphere DTO; trusting only the shader mask; keeping the resolved max-sum shortcut because the blender normally writes both together.
Scalability potential: All quality levels share the same invariant: normalized weights sum to 1.0 whether one, two, three, or four biome lanes are active.
Hardware Impact: Editor/cold self-audit only. Runtime solver cost is unchanged.

Problem: Cadence-reused telemetry for forced/editor mock traversal could read the last scheduled `MockCameraAup` Vault cell. At low quality the solver intentionally skips up to 11 frames, so the black-box AUP could lag behind the deterministic mock phase even while the ring cursor advanced every frame.
Solution: `RecordCadenceSkippedTelemetry()` now calls the same endpoint/phase math used by `MockCameraTraversalJob`, derives AUP from `frame % 600`, and writes that blit back into the mock AUP Vault cell when no SHINOBU pipeline job is active.
Rejected Alternatives: Scheduling the full mock/evaluate/blend job graph just to refresh telemetry; accepting stale mock AUP in crash forensics; adding a separate per-frame telemetry job with another dependency owner.
Scalability potential: Low quality keeps 60Hz black-box spatial truth while solver math remains near 5Hz. Middle, High, and Ultra converge toward every-frame solver updates but retain identical mock replay math.
Hardware Impact: Adds O(1) host scalar math and one 128-bit AUP Vault write only on cadence-skipped mock frames. It avoids waking the Burst chain and prevents misleading QA dumps on weak devices.

Problem: Task 09 required shader Constant Buffer publication, but the runtime visual-sync bridge still treated `Shader.SetGlobalVector` calls as the final shader handoff. The Vault payload was complete, yet the GPU-facing route lacked a prewarmed CBuffer upload lane.
Solution: Added explicit `BiomeTransitionShaderPayloadCBufferDTO` at 128 bytes (`float4[8]`) and validated its offsets in `BiomeTransitionNativeLayout`. `LateFrameTick` now uploads the completed Vault shader payload through a double-buffered `GraphicsBuffer.Target.Constant` named `H8BiomeTransitionPayload`, using `LockBufferForWrite` and `UnsafeUtility.MemCpy`; legacy scalar globals remain only as compatibility mirrors.
Rejected Alternatives: Leaving the vector globals as the sole route; using `GraphicsBuffer.SetData`; creating per-frame managed arrays; moving the upload into a rendering sibling domain without an owner-local contract; shrinking the eight-slot payload.
Scalability potential: Low quality still uploads one 128B payload after the 5Hz solver cadence while reusing presentation state between solves; Middle/High/Ultra can consume the same fixed CBuffer with richer shader-side dither, caustic, and fog logic without changing CPU authority math.
Hardware Impact: Adds one 128B mapped GPU copy only after completed solver publishes and only on platforms supporting constant buffers. It removes the architectural risk of per-vector global churn becoming the only shader route; Quest-class devices pay bounded visual-sync bandwidth, RTX tier gets a single packed payload for overkill shader work.

Problem: `RecordBiomeTransitionTelemetryJob` wrote an estimated CPU microsecond value before the scheduled pipeline actually completed. That was useful as a model, but it was not the black-box frame compute time required by Task 16.
Solution: Store a `Stopwatch` timestamp when the evaluate/blend/publish/acoustic/telemetry chain is scheduled. After `LateFrameTick` observes `_pipelineHandle.IsCompleted` and finalizes through `DispatcherJobSwap`, patch the most recent 64B telemetry row and `BiomeTransitionCounterDTO.LastCpuMicroseconds` with measured schedule-to-finalize elapsed microseconds. Cadence-reused frames remain explicit zero-cost reuse records.
Rejected Alternatives: Calling `Complete()` to measure synchronously; trusting the estimate as forensic truth; adding managed profiler samples inside Burst jobs; recording timing only in editor logs.
Scalability potential: Low quality still records 0 us reuse rows on skipped cadence frames and measured timings on actual 5Hz solver frames. Middle/High/Ultra get measured timings as cadence approaches every frame, so thermal tuning can compare real solver wall time across the continuum.
Hardware Impact: Adds one `Stopwatch.GetTimestamp()` at schedule, one timestamp read at finalize, and one existing Vault-row scalar patch. It does not block the main thread and prevents misleading microsecond reports on i3/MX350 or Quest-class hardware.

Problem: The first timing patch still let `FastTick` return immediately while `_pipelineScheduled` was true, even if the job had already completed before the next fast phase. That delayed ready-only finalization until `LateFrameTick`, left one avoidable telemetry gap, and made a naive fix risky because rescheduling before visual sync could overwrite the shader payload before `H8BiomeTransitionPayload` was uploaded.
Solution: Added `TryFinalizeCompletedPipeline()` and `_pendingShaderPayloadUpload`. `FastTick` now finalizes only already-completed pipeline handles without blocking; if a completed payload is waiting for LateFrame upload, it writes a 64B reuse telemetry row and refuses to schedule a new solver over the same Vault shader payload. `LateFrameTick` performs the CBuffer upload and clears the pending flag. Shutdown and Vault rebinding clear the pending flag.
Rejected Alternatives: Moving shader upload into `FastTick`; scheduling a new pipeline over the previous shader payload; writing telemetry while the job is still running; calling `Complete()` to force the timing proof.
Scalability potential: Low quality keeps 60Hz forensic coverage around the 5Hz solver cadence; Middle/High/Ultra avoid a visual-sync race while still allowing ready-only finalization as soon as worker jobs finish.
Hardware Impact: Adds one bool branch in `FastTick`/`LateFrameTick` and one 64B reuse telemetry write only when a completed payload is waiting for visual sync. It avoids an extra frame of stale forensic state without adding a main-thread stall.

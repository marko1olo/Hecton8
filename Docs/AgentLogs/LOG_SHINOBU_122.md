# LOG_SHINOBU_122 - BIOME_TRANSITION_MANAGER

## 2026-05-19 - BoxCollider Biome Transition Removal

What was wrong:
- Biome transition authority had to be protected from any future `BoxCollider` / `OnTriggerEnter` route. Static archaeology found no active biome-owned trigger scripts named `BiomeVolume.cs` or `AtmosphereChanger.cs`; unrelated trigger users in audio, hazards, construction, and nav proxies were left alone.
- Existing transition DTOs were not sufficient for the XML mandate: explicit 64-byte ARM64 layout, AUP-local math, Vault-owned buffers, quality-continuous scan/blend work, audio staging, shader payload, editor control, CSV ingest, and black-box telemetry were missing.
- The expected `biome_transition_matrix.h8bin` payload is absent and not listed as active in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

What was done:
- Added a Vault-backed `BiomeTransitionManagerRuntime` that resolves player AUP, schedules Burst jobs, writes `CurrentAtmosphereDTO`, `BiomeBlendMaskDTO`, `BiomeAcousticStageDTO`, shader payload vectors, `BiomeChangedSignal`, and a 300-entry telemetry ring.
- Rebuilt `BiomeTransitionFogBlendJobs.cs` around explicit unmanaged DTOs and deterministic Burst jobs: emergency mock seed, mock camera traversal, proximity evaluation, atmosphere blend, shader publication, acoustic staging, telemetry, and CSV ingest.
- Added `Assets/_Project/Data/World/biome_atmosphere_rules.csv` as the current human-readable source lane and a deterministic mock fallback when CSV/payload data is absent.
- Added `Biome Transition Tuner` editor facade with radius, quality override, center scan scale, cadence, dither, gizmo, mock traversal, CSV reload, self-audit, and black-box dump controls.
- Added `TryRunSelfAudit` to verify native layout, snapshot readiness, normalized weight sum, and blend-count bounds.

Cinematic cheats used:
- Physical trigger volumes were replaced by direct distance-to-biome-center math in AUP local space.
- Terrain/flora biome border detail is staged as hashed biome IDs plus four weights for shader-side dither, not CPU texture-map blending.
- Audio is staged as scalar DTO data, not per-`AudioSource` transitions.

Exact microseconds saved:
- Physics broadphase/event route removed from biome switching: estimated 20-200 us/frame in dense scenes.
- Hot-path `Complete()` removal for seed/mock traversal: avoids estimated 50-500 us spikes under worker contention.
- `UninitializedMemory` Vault buffers with deterministic seed: avoids clearing roughly 11 KB on boot/reload, estimated 5-30 us cold path on weak CPUs.
- Shader publication is six `float4` copies plus global vector mirrors, estimated below 1 us per completed cadence.

Build verification:
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /v:minimal /p:UseSharedCompilation=false` was launched only after the CPU/dotnet gate opened.
- Build failed outside SHINOBU_122: missing external DTOs in `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` and `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs`.
- `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` is included in `Hecton8.Core.csproj` and produced no reported compiler error before the external compile wall. New biome runtime/editor files are absent from stale generated csproj files and require Unity regeneration/import for direct compile proof.

<SELF_AUDIT agent_id="SHINOBU_122" domain="BIOME_TRANSITION_MANAGER" date="2026-05-19">
  <TASK_RECONCILIATION>
    <TASK id="01" status="[PASS]">`biome_transition_matrix.h8bin` absent; deterministic CSV/mock fallback implemented through Vault buffers.</TASK>
    <TASK id="02" status="[PASS]">No biome-owned trigger scripts found; new route uses math/Vault/signals, not physics triggers.</TASK>
    <TASK id="03" status="[PASS]">Hot biome DTOs use public fields only; no getter/setter DTO surface in the new hot path.</TASK>
    <TASK id="04" status="[PASS]">`BiomeStateDTO` explicit 64B layout plus editor/runtime layout validation.</TASK>
    <TASK id="05" status="[PASS]">`MockCameraTraversalJob` writes deterministic AUP to Vault and chains by `JobHandle`.</TASK>
    <TASK id="06" status="[PASS]">`EvaluateBiomeProximityJob` uses deterministic Burst, AUP local deltas, sector start, scan budget, and `[NoAlias]` arrays.</TASK>
    <TASK id="07" status="[PASS]">`BlendAtmosphereJob` normalizes weights and writes `CurrentAtmosphereDTO`.</TASK>
    <TASK id="08" status="[PASS]">Dear Lie dither mask published as hashes/weights instead of CPU texture blending.</TASK>
    <TASK id="09" status="[PASS]">`PublishAtmosphereDataJob` writes packed shader payload by `UnsafeUtility.MemCpy`.</TASK>
    <TASK id="10" status="[PASS]">`GlobalQualityWeight` drives scan budget, blend gates, and cadence continuously.</TASK>
    <TASK id="11" status="[PASS]">Dominant biome change enqueues `BiomeChangedSignal` through `SignalBus&lt;BiomeChangedSignal&gt;.ParallelWriter`.</TASK>
    <TASK id="12" status="[PASS]">Centers store sector X/Z/hash; evaluator starts from a current/adjacent sector when available.</TASK>
    <TASK id="13" status="[PASS]">`StageAcousticParametersJob` writes audio/DSP scalar DTO; no `AudioSource` mutation.</TASK>
    <TASK id="14" status="[PASS]">Jobs use deterministic Burst float mode and no Unity `Time` or `Random` authority.</TASK>
    <TASK id="15" status="[PASS]">All new Vault buffers use `UninitializedMemory`; tuning/counters are deterministically written before reads.</TASK>
    <TASK id="16" status="[PASS]">300-entry 64B telemetry ring and dump path `Docs/AgentLogs/Dump_BIOME_MANAGER.bin` added.</TASK>
    <TASK id="17" status="[PASS]">UI Toolkit tuner facade added under `World/Biomes/Editor`.</TASK>
    <TASK id="18" status="[PASS]">Allocation-free byte CSV parser job added for `biome_atmosphere_rules.csv`.</TASK>
    <TASK id="19" status="[PASS]">`OnDrawGizmos` reads Vault centers/mask and draws radii/contribution lines.</TASK>
    <TASK id="20" status="[PASS]">Embedded `TryRunSelfAudit` verifies layout, weight normalization, snapshot readiness, and blend-count bounds. Full compile proof is `[BLOCKED_BY_DEPENDENCY]` by external Visor/Somatic errors.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION primary="BiomeStateDTO">
    <FIELD name="BiomeHash" offset="0" size="4" />
    <FIELD name="AuthoringIndex" offset="4" size="4" />
    <FIELD name="Flags" offset="8" size="4" />
    <FIELD name="_pad0" offset="12" size="4" />
    <FIELD name="FogColor" offset="16" size="16" />
    <FIELD name="AbsorptionParams" offset="32" size="16" />
    <FIELD name="AmbientAudioVolume" offset="48" size="4" />
    <FIELD name="_pad1" offset="52" size="4" />
    <FIELD name="_pad2" offset="56" size="4" />
    <FIELD name="_pad3" offset="60" size="4" />
    <MATH>4+4+4+4+16+16+4+4+4+4 = 64 bytes; `FogColor` and `AbsorptionParams` start on 16-byte boundaries; final stride is one 64-byte cache line.</MATH>
    <COUNTER_LAYOUT>`BiomeTransitionCounterDTO` is explicit 64B. It is single-writer in the scheduled graph, not a concurrent atomic counter, but still padded to one cache line.</COUNTER_LAYOUT>
    <TELEMETRY_LAYOUT>`BiomeTransitionTelemetryEntry` is explicit 64B: `AbsoluteUniversePosition` 48B + hash/count/cpu/state 16B.</TELEMETRY_LAYOUT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    When `GlobalQualityWeight` drops below 0.3, `EvaluateBiomeProximityJob` lowers distance samples by `ceil(lerp(1, activeCount * MaxCenterScanScale, smooth01(q)))` after choosing a sector-relevant start index. `BlendAtmosphereJob` lowers active interpolation through gates `saturate(maxBlendFloat - laneIndex)` where `maxBlendFloat = lerp(1,4,smooth01(q))`, so lanes 2-4 fade out instead of popping. Runtime cadence lerps from `LowCadenceHz` to `UltraCadenceHz`, default 5Hz to 60Hz.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_arrays="0">
    <BUFFER id="BiomeTransitionStates" />
    <BUFFER id="BiomeTransitionCenters" />
    <BUFFER id="BiomeTransitionInfluences" />
    <BUFFER id="BiomeTransitionCurrentAtmosphere" />
    <BUFFER id="BiomeTransitionBlendMask" />
    <BUFFER id="BiomeTransitionShaderPayload" />
    <BUFFER id="BiomeTransitionAcousticStage" />
    <BUFFER id="BiomeTransitionTelemetryRing" />
    <BUFFER id="BiomeTransitionCounters" />
    <BUFFER id="BiomeTransitionTuning" />
    <BUFFER id="BiomeTransitionCsvScratch" />
    <BUFFER id="BiomeTransitionMockCameraAup" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NOALIAS>All NativeArray fields in Burst jobs are marked `[NoAlias]` where the data lanes are distinct.</NOALIAS>
    <GRAPH>optional `MockCameraTraversalJob` -> `EvaluateBiomeProximityJob` -> `BlendAtmosphereJob` -> parallel `PublishAtmosphereDataJob` and `StageAcousticParametersJob` -> `RecordBiomeTransitionTelemetryJob`.</GRAPH>
    <JOBHANDLE_POLICY>No hot-path blocking; `Complete()` is gated by `IsCompleted` in `LateFrameTick` or used only during shutdown.</JOBHANDLE_POLICY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    The biome runtime does not add direct sibling-domain assembly references. It routes through cold `GlobalRegistry`, typed `SignalBus&lt;BiomeChangedSignal&gt;`, and `GlobalDataVault`. The only core edit is a narrow `BufferID` extension for Vault ownership.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The specific fake is center-distance atmospheric blending plus shader dither masks. Before: physics broadphase trigger volume checks plus abrupt managed event transitions, O(trigger broadphase + event fanout). After: bounded math over up to 64 centers with quality-reduced distance samples and up to four blend lanes, O(min(active, qualityBudget)) for expensive distance work and O(1) shader payload publication.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Polish Pass 02 - Signal Route Correction

What was wrong:
- The evaluator initially used `GlobalSignals.BiomeChangedSignalWriter`. Static source shows current consumers use `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()`, and no owner drains the legacy direct `_biomeChangedSignals` queue into that snapshot.
- The global authority route existed in code but did not have a route-card artifact in `Status_SHINOBU_122.md`.

What was done:
- Changed the cold lane warmup and Burst job writer to `SignalBus<BiomeChangedSignal>.ParallelWriter`.
- Added `SHINOBU_122_BIOME_TRANSITION_STATE` route card with review result `YELLOW / PENDING VERIFICATION`.

Cinematic cheats used:
- No new physical route. The same center-distance blend and shader dither mask remain the atmospheric transition fake.

Exact microseconds saved:
- Correctness fix, not a speed claim. It avoids adding a new bridge/drain pass in `GlobalSignals.cs` and keeps the dirty-only signal at one 64B enqueue per dominant biome change.

Verification:
- Static scan now shows no `GlobalSignals.` usage in the SHINOBU_122 runtime/job files.
- Full compile proof remains blocked by external Visor/Somatic compile-wall errors and stale generated csproj coverage for new Unity files.

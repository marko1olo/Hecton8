# Base Structural Warning Dispatcher

Owner: SHINOBU_339

Runtime route:
- StructuralIntegrityCalculatorRuntime remains the owner of structural stress truth.
- POST_SIMULATION schedules `EvaluateStructuralStressJob -> CoalesceWarningsJob -> RouteStructuralWarningsJob -> WriteStructuralWarningTelemetryJob`.
- Aggregation uses `double3` AUP in `GroupedWarningDTO` before any audio/runtime float conversion.
- Coalescence is one pass over active raw warnings against a bounded 64-group table. Worst-case node work is `O(N * 64)`, not raw-pair `O(N^2)`.
- Audio consumes `SignalBus<Hecton8.Core.Contracts.Signals.BaseStructuralWarningSignal>` snapshots in `PlayerCriticalProceduralAudioRenderer` and resolves distance through player AUP, not absolute runtime float conversion.

Signal lane:
- Owner: `StructuralIntegrityCalculatorRuntime` / `SystemID.HullIntegrity`.
- Producer phase: POST_SIMULATION owner job chain after collapse signal extraction.
- Consumer phase: VISUAL_SYNC/audio snapshot read; power/lighting/physiology consume only the typed signal bit/scalar payload in their owner phases.
- Lane payload: `BaseStructuralWarningSignal=64` in `Hecton8.Core.Contracts.Signals`, lane hash `0x42535744` (`BSWD`), capacity `64`, low-tier frame budget `8`, max frame budget `64`, one-frame retention.
- Overflow route: `RouteStructuralWarningsJob` emits highest-stress groups first and applies a continuous smoothstep producer budget from `4..64` signals before touching the queue. `SignalBus<T>` bounded frame snapshot/load shedding is the second wall.
- Duplicate-name audit: resolved. Construction's foundation-pylon warning lane is now `Hecton8.Construction.FoundationStructuralWarningSignal` (`FWNG`); the only public `BaseStructuralWarningSignal` is the Core Contracts audio/visor payload (`BSWD`).

Non-authority fence:
- `RawWarningDTO`, `GroupedWarningDTO`, sector timers, alarm profiles, and telemetry are presentation-only.
- These buffers are not rollback truth and must not be added to lockstep state hashing.
- Red alert is carried as `BaseStructuralWarningSignal.FlagRedAlert` for power/lighting consumers; this dispatcher does not mutate Power DTOs directly.

Vault route:
- `70498 BaseStructuralWarningRawWarnings`: `RawWarningDTO[StructuralIntegrityConstants.MaxNodeCapacity]`, explicit 64-byte stride for parallel job writes.
- `70499 BaseStructuralWarningGroups`: `GroupedWarningDTO[64]`, explicit 32-byte cluster output.
- `70503 BaseStructuralWarningTimers`: `BaseStructuralWarningTimerDTO[128]`, explicit 32-byte sector cooldown rows.
- `70504 BaseStructuralWarningCounters`: `int[72]`, first 8 are frame counters, next 64 are per-group counts for bounded one-pass averaging; no parallel atomics.
- `70505 BaseStructuralWarningTelemetryRing`: `BaseStructuralWarningTelemetryEntry[300]`, explicit 64-byte black-box rows.
- `70506 BaseStructuralWarningTelemetryCursor`: `int[1]`.
- `70507 BaseStructuralWarningTuning`: `BaseStructuralWarningTuningDTO[1]`, explicit 64 bytes.
- `70508 BaseStructuralWarningProfiles`: `BaseAlarmProfileDTO[16]`, explicit 32 bytes.
- `70509 BaseStructuralWarningCsvScratch`: `byte[16384]`, cold authoring bridge scratch.

Scalability:
- Cluster radius is continuous: `lerp(5m, 100m, 1 - GlobalQualityWeight)`.
- Signal emission budget is continuous: `round(lerp(4,64,smoothstep(GlobalQualityWeight)))`, highest-stress groups first.
- Low: wide sectors, 4-ish producer emits, and an 8-row SignalBus survival budget for pressure spikes.
- Middle: room/wing-level clusters, cooldown still sector-local.
- High: tighter room-level localization.
- Ultra: tight clusters with stronger audio/panic payload, same gameplay truth and unchanged DTO layout.

Dear Lie:
- CPU does not run fracture acoustics, propagation graphs, Canvas alarm emitters, or per-crack audio logic.
- Stress points are reduced to one centroid and scalar packet per cluster; the audio renderer turns the scalar into localized procedural impact/echo.
- Complexity changes from `O(warnings * consumers/audio voices)` spam to `O(nodes * 64 + groups * timers)` bounded aggregation with `groups <= 64`.

Black box:
- Last 300 warning telemetry frames live in `BufferID.BaseStructuralWarningTelemetryRing`.
- NaN or >0.2 ms estimate dumps `Docs/AgentLogs/Dump_SHINOBU_339.bin`.
- Cold CSV ingestion reads `base_alarm_profiles.csv` into Vault scratch bytes and parses `ReadOnlySpan<byte>` directly into unmanaged `BaseAlarmProfileDTO` rows.
- Editor gizmo reads locked warning buffers and draws both epicenter spheres and bounded raw-node connection lines.

## Compile guard

- Runtime assembly references remain `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and habitat deformation contracts; no sibling runtime assembly is imported for audio, construction, power, or physiology.
- Payload identity lives in `HectonSignalLaneContract.cs` under the existing Core Contracts Signals namespace.
- `AcousticAup` was folded into the same included contract source to avoid stale generated-csproj misses.
- `GlobalSignals.cs` only registers/flushes the typed lane. No sibling runtime assembly reference was added.
- New runtime/editor script assets have committed `.meta` GUIDs. `BaseStructuralWarningLayout.Validate()` enforces exact source offsets for `RawWarningDTO`, `GroupedWarningDTO`, and `BaseStructuralWarningSignal`; the XML audit is not the only ABI proof.
- Editor acoustic DSP smoke tester checks `AcousticAup` in active `HectonSignalLaneContract.cs`.
- It asserts 40-byte explicit layout plus `Local@24`.
- It no longer points at deleted standalone contract source.
- Build-attempt boundary:
  - No errors from included route files: `HectonSignalLaneContract.cs`, `PlayerCriticalProceduralAudioRenderer.cs`.
  - Covered patches: contract source fold, cutter-boil purge, smoke-test source-path polish.
  - Missing from generated project: new Habitat Deformation sources, `ShinobuAcousticDspSmokeTester`.
  - Pending proof: Unity import/project regeneration.
  - Current compile wall: external Narrative interface implementation and missing Solar DTOs.

Residual audio-owner note:
- `OOP_Audio_Scanner` reports allowlisted central Audio-owner `.Play(` matches with file/line evidence.
- Read-only audit found boiling-water calls in `PlayerCriticalProceduralAudioRenderer`.
- Legacy `AudioSource` loop/pool fallback was removed.
- Cutter boil now uses `BubbleBoilIntensity -> RenderBubbleBlock`; `PlayerCriticalProceduralAudioRenderer` is no longer scanner-allowlisted.

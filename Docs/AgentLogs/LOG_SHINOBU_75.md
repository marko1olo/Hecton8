# LOG_SHINOBU_75

## 2026-05-18 - Diegetic Glitch Corruptor

What was wrong:
- UI glitch work had no SHINOBU_75-owned zero-GC runtime path for `GlitchTable.bytes` pointer substitution, vault-backed hologram/radar buffers, or black-box evidence.
- Canvas-style overlay damage was the wrong architecture for anomaly corruption; it hides the problem behind managed UI batching and does not touch diegetic instruments.
- Buffer IDs initially overlapped the `708xx` AudioStem range and were moved to `70900-70914`.

What was done:
- Added `DiegeticGlitchSurgeonRuntime` with `GlitchStateDTO`, `ScrambledCharacterDTO`, mock corruption/depth/breach signals, glitch tuning, 112-byte quad DTO, radar DTO, synth mirror DTO, 300-frame telemetry, and fault dump header.
- Loaded `Assets/_Project/Data/UI/GlitchTable.bytes` once into DataVault memory; fallback writes `GenerateEmergencyMockGlitchTable()` into the same `byte*`.
- Added Burst jobs for corruption signal synthesis, ASCII pointer scrambling, holographic matrix/UV shatter, radar ghost injection, synth pitch bending, and telemetry writes.
- Added `ApplyTerminalUvTearing(ref TerminalStateDTO, float)` so Terminal OS can route UV tearing through its existing shader scalar instead of Canvas noise.
- Added `Diegetic Glitch Tuner` EditorWindow with Play Mode vault sliders and `GUI.Label` preview of the post-job mock text buffer.
- Added byte-parsed `Assets/_Project/Data/UI/glitch_profiles.csv` override and `.meta`.
- Added `ShinobuDiegeticGlitchSynthBridge` beside the real `SynthParametersDTO` for actual audio-buffer pitch/grain bending without a UI-to-audio dependency.

Cinematic cheats used:
- No simulated static, particles, or Canvas damage. The lie is pointer-level glyph corruption, matrix jitter, UV shifts, fake radar coordinates, and shallow audio parameter bending.
- Depth and breach influence are scalar mocks feeding intensity; no radiation/proton simulation.
- `GlobalQualityWeight` scales probability/density continuously: low tier keeps UV tear and sparse work, ultra spends cycles on dense matrix shatter and radar ghosts.

Exact microseconds saved:
- Text scrambling: expected 8-18 us for 128 chars, 0 B GC, replacing managed string/TMP assignment paths that typically cost allocation plus layout rebuild.
- Matrix shatter: expected 12-35 us for 128 quads when quality permits; low quality skips most writes by probability.
- Radar ghosts: expected 5-14 us for 64 unmanaged ghosts, replacing managed blip object churn.
- Synth bend: expected 2-6 us for 8 DTOs, replacing managed `AudioSource` pitch mutation.
- Telemetry: fixed 64 B/frame ring, no managed list growth; dump is cold fault-path IO only.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore` passed before external Economy changes with 0 errors.
- Remaining warnings are pre-existing: duplicate `PhysicsWakeSignalContracts.cs` compile item and unassigned `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` fields.
- At that earlier verification point, global build was blocked by untracked `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs`: `TryResolveAllViews` arity mismatch and `MarauderPaddedCounterDTO` assigned to `NativeArray<int>`. Not SHINOBU_75 domain; not modified.
- `ShinobuDiegeticGlitchSynthBridge.cs` was separately compiled with Roslyn against `Library/ScriptAssemblies/Hecton8.Audio.Synthesis.dll`, 0 errors.
- Hot path grep found no `string.Replace`, TMP `.text`, Canvas overlay, runtime `new char[]`, `File.ReadAllBytes`, `double3`, or AUP seed usage in SHINOBU_75 runtime/audio files. Editor-only preview intentionally creates a GUI string for `GUI.Label`.

## 2026-05-18 - Ultra Polish Reopen

What was wrong:
- Prior report over-claimed polish. The CSV fallback path pointed at project root, not `Assets/_Project/Data/UI/glitch_profiles.csv`.
- `TextScrambleRate` was written by the editor but not consumed by the Burst ASCII probability, making a human-facing slider partially cosmetic.
- The readability mask used a global digit budget; in `O2 98%`, the label digit could consume budget before the oxygen value.
- `FrameSeed` was overwritten from frame-only hash. That is deterministic, but it was not the required sector hash plus simulation frame model.
- The job chain was invisible to the global active-job registry even though `IUpdatable.Tick(float)` cannot return a `JobHandle`.

What was done:
- Added `deterministicSectorHash` and `ApplyDeterministicSectorHash(uint)`; `MockCorruptionSignalJob` now mixes stable sector seed with `_frameIndex`.
- Removed `_simulationSeconds` from critical state; mock seconds are derived from frame count inside Burst.
- Changed the CSV default to `Assets/_Project/Data/UI/glitch_profiles.csv`.
- Passed `GlitchTuningDTO*` into `AsciiScramblerPointerJob` with `[NoAlias]` and folded `TextScrambleRate` plus `GlobalQualityWeight` into substitution probability.
- Added `CriticalReadabilityPrefixChars=5`; the mock vital stat `O2 98` is protected until 0.9 intensity by fixed prefix, not a fragile digit scan.
- Registered the final scheduled chain through `H8Memory.RegisterActiveJob(SystemID.UI, _activeHandle)` and explicitly batched jobs.

Cinematic cheats used:
- Still no Canvas damage. Low quality keeps shader UV tearing and sparse glyph writes; high quality raises matrix shatter, radar ghosts, and audio pitch bends by continuous math.
- The sector-frame seed gives repeatable horror beats without Unity random state or wall-clock timing.

Exact microseconds saved:
- Readability prefix path replaces digit scanning for the protected O2 region: under 1 us saved on the mock span and removes a correctness failure.
- Low-quality text path now multiplies author rate by a 0.2-to-1.0 quality curve, avoiding avoidable writes on weak hardware.
- `H8Memory.RegisterActiveJob` does not reduce compute cost directly; it reduces teardown/fence risk by making UI-owned jobs visible to the global memory owner map.

Verification:
- Static grep after polish found no runtime `UnityEngine.Random`, `string.Replace`, TMP `.text`, `File.ReadAllBytes`, `string.Format`, `foreach`, `Pack=1`, hot DTO `get; set;`, `double3`, `Time.deltaTime`, or `Time.time` in SHINOBU_75 runtime/audio files.
- Only allocation hit is editor-only retained `char[128]` preview buffer plus cached IMGUI preview string on content change.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked externally by SaveSystem files: `H8BinaryWorldPager.cs` is missing `ResolveWriteCommands`, `_readQueue`, `_writeQueue`, and `_readResults`; `SaveDeltaCompression.cs` is missing `sectorOriginMeters` and `sectorOrigin`. SHINOBU_75 files were not reported.
- Roslyn compile of `ShinobuDiegeticGlitchSynthBridge.cs` against `Hecton8.Audio.Synthesis.dll` remains 0 errors.

## 2026-05-18 - Direct Pointer Kernel Reopen

What was wrong:
- The runtime proved corruption through SHINOBU-owned mock buffers, but Task 06 requires interception of caller-owned text before it hits UI. Without a direct pointer API, the later Babel/CharBufferPool bridge would likely stage through a mock buffer or create managed adapter churn.
- Random branches used deterministic integer hash samples. That is stable, but the mandate explicitly requires `Unity.Mathematics.Random` seeded from sector hash plus simulation frame.

What was done:
- Initially added `TryResolveGlitchTableBytes` to expose resident `GlitchTable.bytes` as `byte*` plus length/hash; later Loop 10 replaced public raw exposure with `TryLeaseGlitchTableBytes`.
- Added the external schedule path for caller-owned `ushort*` source/destination buffers. The current hardened version consumes a leased vault table pointer, tuning snapshot, sector hash, simulation frame, and returns the chained `JobHandle` through `ExternalAsciiScrambleLease`.
- Added static `ScheduleAsciiScrambleDirect` and `AsciiScramblerDirectJob` so future Babel/CharBufferPool glue can schedule the Burst kernel without a `MonoBehaviour` mock buffer dependency.
- Converted ASCII substitution and radar ghost random branches to `Unity.Mathematics.Random` with nonzero deterministic seeds from sector/frame/index/source.

Cinematic cheats used:
- The fake remains data corruption: bytes, UVs, matrices, ghost radar DTOs, and pitch/grain DTOs. No Canvas, no physical radiation/static simulation, no managed text mutation.

Exact microseconds saved:
- Direct pointer scheduling avoids one future staging copy from caller span into SHINOBU-owned mock text. Expected saving depends on caller span size; for 128 chars, the avoided copy is sub-microsecond to low-microsecond but removes the allocation-risk adapter path.
- `Unity.Mathematics.Random` adds a small ALU cost versus raw hash sampling, but keeps deterministic rollback semantics explicit and Burst-friendly.

Verification:
- Static grep after direct-kernel patch found no runtime `UnityEngine.Random`, `string.Replace`, TMP `.text`, `File.ReadAllBytes`, `string.Format`, `foreach`, `Pack=1`, hot DTO `get; set;`, `double3`, `Time.deltaTime`, `Time.time`, or LINQ in SHINOBU_75 runtime/audio files.
- Only allocation hit remains editor-only retained `char[128]` preview buffer.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` passed with 0 errors and 0 warnings in 5.15s.
- `ShinobuDiegeticGlitchSynthBridge.cs` Roslyn compile against `Hecton8.Audio.Synthesis.dll` returned 0 errors.

## 2026-05-18 - External Pointer Lease Hardening

What was wrong:
- The external direct text API returned a bare `JobHandle` while the job still read from vault-owned `byte* GlitchTable.bytes`. That left ownership ambiguous during vault reload, rebind, or defrag maintenance.
- The parallel direct job rejected `source == destination`, so it did not satisfy true in-place caller span mutation.

What was done:
- Added `ExternalAsciiScrambleLease` with `JobHandle`, owner, and the exact leased `IDataVault`.
- `TryScheduleExternalAsciiScramble` now locks `GlitchTableBufferId` before resolving the table pointer and returns a lease.
- Added `TryReleaseExternalAsciiScramble(ref lease)` for nonblocking release after `IsCompleted`, plus teardown-only `CompleteAndReleaseExternalAsciiScramble(ref lease)`.
- Added `TryScheduleExternalAsciiScrambleInPlace`, static `ScheduleAsciiScrambleInPlaceDirect`, and Burst `AsciiScramblerInPlaceJob` for true same-buffer text mutation without parallel read/write races.

Cinematic cheats used:
- Same Dear Lie: corrupt data and shader/matrix inputs, not simulated UI static or Canvas overlays.

Exact microseconds saved:
- Lease itself is not a speed feature; it prevents stale-pointer failures. In-place sequential Burst avoids staging/copy cost for small caller spans while keeping 0 B runtime GC.

Verification:
- Focused Roslyn compile of `GlitchTable.cs` plus `DiegeticGlitchSurgeonRuntime.cs` with Unity/Core references and `UNITY_EDITOR` define passed with 0 errors.
- Static banned-pattern grep still reports only editor-only retained `char[128]` preview buffer.
- Latest global `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked externally by `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` AUP type mismatches; SHINOBU_75 files were not reported.

## 2026-05-18 - CSV Path And Bridge Audit Reconciliation

What was wrong:
- The runtime constant for `glitch_profiles.csv` had regressed to the project root while the authored UI data file and the status report point to `Assets/_Project/Data/UI/glitch_profiles.csv`.
- The H-PHI report listed only SHINOBU-owned vault buffers `70900-70914`, but the runtime also borrows Terminal OS buffer `70520` for UV tear scalar writes.
- `Tick(float)` contained a dead `safeDeltaTime` local. It did not affect behavior, but it weakened the determinism audit because it looked like critical state might consume delta time.

What was done:
- Restored `DefaultCsvRelativePath` to `Assets/_Project/Data/UI/glitch_profiles.csv`.
- Corrected the CSV inspector tooltip to project-relative.
- Removed the dead `safeDeltaTime` local; `_frameIndex` remains the simulation driver.
- Updated the self-audit with borrowed Terminal OS buffer `70520` and the lock-only bridge rule.

Cinematic cheats used:
- The terminal path still uses one scalar in `TerminalStateDTO.Value2` to make the shader tear UVs. There is no Canvas static pass and no CPU geometry/noise generation.

Exact microseconds saved:
- Runtime frame cost is materially unchanged; the patch removes one dead local and prevents designer reload misses. The bridge remains bounded to at most 64 DTO scalar writes when intensity/quality requires it.

Verification:
- Re-extracted `CURRENT_BATCH.md` lines 1929-1980 with the SHINOBU_75 XML block.
- Static grep after this patch reports only editor-only retained `char[128]` in `DiegeticGlitchTunerWindow`.
- No new dotnet build was launched because active `dotnet`/`csc` processes were present, matching the AGENTS build-throttle rule.

## 2026-05-18 - Vault Pointer Lock-Order Hardening

What was wrong:
- Internal Tick resolved vault pointers before `TryLockScheduledBuffers`, which left a theoretical relocation/stale-pointer window.
- Public `TryResolveGlitchTableBytes` exposed a raw table pointer without proving caller ownership.
- CSV reload and editor preview paths were cold/editor-facing but still touched vault memory without buffer locks.
- Terminal OS bridge buffer `70520` was resolved before the borrowed buffer was locked.

What was done:
- Added `TryLeaseGlitchTableBytes` so raw `GlitchTable.bytes` pointer access is tied to `ExternalAsciiScrambleLease`.
- Rewired external direct and in-place schedules to lease the table first, then attach their `JobHandle` to that lease.
- Moved Tick to lock scheduled buffers before resolving job pointers; tuning refresh now writes through the locked `GlitchTuningDTO*`.
- Added a locked `GlitchTuningDTO` snapshot for external schedules.
- Locked `CsvScratchBufferId` + `GlitchTableBufferId` during CSV override, `WorkTextBufferId` during editor preview copy, and Terminal OS `70520` before pointer resolve.

Cinematic cheats used:
- Unchanged: data corruption and shader/matrix input tearing. The pass hardens memory ownership; it does not add Canvas static, particles, or CPU geometry noise.

Exact microseconds saved:
- No frame-time win claimed. This is pointer lifetime safety. External live reload and text jobs now avoid stale-pointer failures while keeping the Burst inner loops allocation-free.

Verification:
- Static banned-pattern scan still reports only editor-only retained `char[128]`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked externally by `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` missing `ISignal`; SHINOBU_75 files were not reported.

## 2026-05-18 - Editor CSV Retry Hardening

What was wrong:
- After lock-order hardening, CSV reload could fail when an external text job held the `GlitchTable.bytes` lease. Because the editor watcher had already accepted the file timestamp, that designer edit could be missed.

What was done:
- Changed `TryApplyCsvOverride` to return a retry flag.
- Re-armed `_pendingCsvReload` on vault lock contention or transient file IO failure.
- Avoided endless retry for missing or malformed CSV content.

Cinematic cheats used:
- No rendering path change. The CSV still authors the byte table used by pointer text corruption and shader/UV fakes.

Exact microseconds saved:
- None claimed. This is editor reliability and pointer ownership safety, not a frame-time optimization.

Verification:
- Static banned-pattern scan still reports only editor-only retained `char[128]`.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked externally by `HomeostasisBrain.ApplyMockFrameSpikeToFrameMs`, `AssetLifecycleGovernor.AssetTtlEvaluationJob.Run`, and `SaveBinaryPayloadCodec.DataArchaeologyDiscoveryBitMask`; SHINOBU_75 files were not reported.

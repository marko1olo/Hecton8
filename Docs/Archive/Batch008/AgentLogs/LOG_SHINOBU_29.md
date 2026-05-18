# LOG_SHINOBU_29

## 2026-05-17 - Granular Synth DTO/Tuner/No-WAV Pass

What was wrong -> The SHINOBU_29 prompt required a pressure-metal procedural granular synth path with 16-byte DTOs, no massive WAV dependency, no GC/audio-thread blocking, live tuning, CSV override, oscilloscope feedback, and black-box dumps. The repository already rejected `OnAudioFilterRead` synthesis in favor of the native producer/SPSC contract, so adding a managed callback would have broken current audio smoke tests.

What was done -> Added 16-byte `SynthParametersDTO`, 16-byte `GrainPlaybackStateDTO`, `MockHullStressSignal`, `MockHullStressSignalJob`, `HanningWindowBuildJob`, and `GenerateEmergencyMockGrains()` helpers in the synthesis kernel. Routed live granular tuning through `AudioParameterSnapshot`, added pitch/length/density/FM controls, added sonar FM sideband modulation, rejected metal-stress clips longer than 2 seconds before PCM read, and added `Dump_PROCEDURAL_SYNTH.bin` to the granular black-box dump set. Added `GranularSynthTunerWindow` with sliders, span/hash CSV parsing for `audio_synth_profiles.csv`, and a fixed-buffer telemetry oscilloscope.

Cinematic Cheats used -> Replaced impossible hull-collapse acoustics with deterministic metallic grit plus randomized granular overlap. Replaced sonar files with FM oscillator sidebands. Reused scalar pressure/depth/fatigue inputs and existing low-pass/limiter paths instead of simulating material deformation, acoustic propagation, or large PCM libraries.

Exact Microseconds saved -> No Unity Profiler capture was run, so no measured exact frame/audio-thread delta is claimed. Static hot-path savings: 0 us runtime for editor tuner/CSV/oscilloscope in player builds; 0 us runtime for oversized WAV rejection after initialization; avoids unbounded GC stalls from managed callback synthesis by keeping the existing native producer/SPSC path. Estimated low-end impact: avoids millisecond-scale GC/decode stalls from 20-34 MB WAV misuse; new FM sideband adds two oscillator advances only during active sonar chirp; grain tuning adds scalar multiplies only when arming voices.

Verification -> `dotnet build Hecton8.Core.csproj --no-restore` failed on unrelated missing symbols: `MockNarrativeTriggerSignal` and `ShinobuLogisticsRouter`. `DepthStressGranularSynthesisKernel.cs` was separately Roslyn-compiled against Unity Burst/Collections/Mathematics references. `GranularSynthTunerWindow.cs` was separately Roslyn-compiled against UnityEditor/UnityEngine references with a temporary renderer stub; temp outputs were removed. `git diff --check` reported no whitespace errors, only CRLF-normalization warnings. Static scan found no new `OnAudioFilterRead` or `Submarine_Groan` dependency in touched runtime files.

Polish mandate -> `Docs/Tasks/CURRENT_BATCH.md` contains no `<POLISH_MANDATE>` tag. Anti-bloat pass executed against touched files by static scan: no managed callback synthesis, no lock/LINQ/Split parser in the new editor CSV path, no new audio package or cross-domain asmdef reference.

## 2026-05-17 - Ultra Polish Alignment Pass

What was wrong -> The prior pass left legacy `Pack = 1` layouts inside the procedural-audio surface and the editor tuner resolved by object search before registry lookup. That was not titanium-grade under the new mandate.

What was done -> Removed `Pack = 1` from SHINOBU_29-touched runtime structs and added explicit sizes/padding where needed: synthesis voice/spawn/oscillator state, sonar tap, granular/prologue telemetry, impact event, and audio snapshot cache slot. Updated the DSP thread smoke tester to enforce the unpacked explicit snapshot-slot string. Changed the tuner to resolve `GlobalRegistry.PlayerCriticalAudio` before editor-only object search fallback.

Cinematic Cheats used -> No new physical simulation. The pressure collapse remains scalar granular overlap plus deterministic metallic grit; sonar remains oscillator/FM math.

Exact Microseconds saved -> No profiler capture, so no measured us claim. Static savings: removed packed-layout risk from touched audio structs and kept all new control surfaces out of runtime/player hot paths. Runtime arrays remain DataVault-backed aliases.

Verification -> Targeted Roslyn compile passed for `DepthStressGranularSynthesisKernel.cs`. Targeted Roslyn compile passed for `GranularSynthTunerWindow.cs` with UnityEditor references and a temporary stub removed afterward. `dotnet build Hecton8.Core.csproj --no-restore` still fails on non-audio missing symbols: `MockNarrativeTriggerSignal` and `MockDamageSignal`. Touched SHINOBU_29 files scan clean for `Pack = 1`; `git diff --check` reports only CRLF normalization warnings.

## 2026-05-17 - Continue Pass / Truth Recheck

What was wrong -> The current dirty workspace changed under concurrent agents. The old full-Core blocker list was stale, and a strict prompt regex missed the current SHINOBU_29 opening tag because the tag includes `role` and `chat_name` attributes.

What was done -> Re-read `Status_SHINOBU_29.md`, `Rationale_SHINOBU_29.md`, `CURRENT_BATCH.md`, `PROJECT_STATE_STATIC_XRAY.md`, `AGENTS.md`, `Actual Domains of Project.txt`, and the relevant audio/zero-GC/native-memory/global-registry/blackbox/cinematic-cheat/AUP mandates. Re-extracted SHINOBU_29 with an attribute-aware regex and confirmed the same 20-task prompt. Re-ran SHINOBU_29 path-specific whitespace gate and a full Core build attempt.

Cinematic Cheats used -> No new simulation. The pressure-metal collapse remains deterministic metallic grit plus granular overlap, scalar pitch scatter, FM harshness, and existing depth muffling. Low tier keeps capped voices and cheap envelopes; high tier spends the saved budget on denser overlap/FM harshness without WAV residency.

Exact Microseconds saved -> No profiler/GCMonitor capture exists, so no measured microseconds are claimed. Static hot-path savings remain architectural: no managed audio callback, no blocking lock, no runtime WAV residency, no `Pack = 1` on SHINOBU_29 touched structs, and no new persistent local `NativeArray`.

Verification -> SHINOBU_29 path-specific `git diff --check` has no whitespace errors, only CRLF normalization warnings. Full-tree `git diff --check` is red on unrelated `Docs/Tasks/CURRENT_BATCH.md` trailing whitespace/new blank line at EOF. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails outside SHINOBU_29: `HectonIndirectVegetationRenderer.ReleaseCpuCullingScratchBuffers(deferActiveJobs)`, `GlobalTelemetryBus.RequestBlackboxMmfFlushAsync`, and SHINOBU_03 `ShinobuEcosystemBalancer` missing buffer/spatial-hash fields and `TryResolveBuffers` arguments.

## 2026-05-17 - Alignment Expansion Pass

What was wrong -> The first alignment polish fixed the obvious 16-byte DTOs and packed telemetry structs, but several live audio state structs in `PlayerCriticalProceduralAudioRenderer` still relied on implicit runtime layout. `PendingImpactEchoProbe` also stored a managed `bool` in an audio runtime state struct.

What was done -> Added explicit 8-byte-multiple `StructLayout(Size=...)` to the player-critical audio state cluster: sonar composite/dedupe/trigger/diagnostics, the 256-byte audio parameter snapshot, hull/sonar/ambient/impact/heartbeat/VWS/tinnitus/leviathan/reverb/thruster state structs, and the pending impact probe. Replaced `PendingImpactEchoProbe.Valid` with a byte flag and updated the only hot checks/assignment.

Cinematic Cheats used -> No extra simulation. Pressure collapse remains scalar granular synthesis, not finite-element acoustics.

Exact Microseconds saved -> No measured profiler value. The static gain is deterministic layout and removal of managed bool layout ambiguity from runtime audio state.

Verification -> SHINOBU_29 path-specific `git diff --check` remains clean except CRLF warnings. Static scan shows no `Pack = 1` in the touched SHINOBU_29 audio files. PowerShell `Add-Type`/`Marshal.OffsetOf` layout probe confirmed: `SynthParametersDTO` 16 bytes, `GrainPlaybackStateDTO` 16 bytes, `DepthStressGranularVoice` 32 bytes, `GranularAudioTelemetryEntry` 48 bytes, `AudioParameterSnapshot` 256 bytes, and `PendingImpactEchoProbe` 16 bytes. `dotnet build Hecton8.Core.csproj -v:minimal` restored and reached compile, then failed outside SHINOBU_29 in `DroneFleetManager` missing `ResolveDroneVaultBuffer`, `RegisterNativeArrayIfFallback`, and `ReleaseDroneVaultBuffer`; no SHINOBU_29 compile error appeared before the external compile wall.

## 2026-05-17 - L1 Field-Order Forensic Pass

What was wrong -> Explicit `Size` was not enough. Several private procedural-audio runtime state structs still had 8-byte fields after 4-byte fields, and prologue telemetry used `uint` padding after byte flags. That satisfies a byte count but not the ARM64 field-order mandate.

What was done -> Reordered SHINOBU_29 audio state structs so `long`/`double` fields lead the struct, 4-byte scalar fields follow, and byte flags/padding sit at the end. Corrected `PrologueAudioTransitionTelemetryEntry` padding to byte pads. Corrected `PendingImpactEchoProbe` to `Excitation@0`, `ExpireAt@4`, `Valid@8`.

Cinematic Cheats used -> No added physics. Pressure collapse remains the cheap audio lie: deterministic metallic grain texture, scalar pressure-to-density mapping, randomized pitch scatter, FM harshness, and existing depth muffling/limiting.

Exact Microseconds saved -> No profiler or GCMonitor capture was run, so no measured microseconds are claimed. Static effect: removes unaligned-field and packed-layout risk from the touched procedural-audio surface; no new hot-path work.

Verification -> Field-order scan reports `ORDER_SCAN_PASS`. Packed-layout scan reports `PACK_SCAN_PASS`. SHINOBU_29 path-specific `git diff --check` is clean except CRLF normalization warnings. `Marshal.OffsetOf` proof: `SonarTriggerState.StartFrame@0`, `AudioThreadDiagnostics.ProducedSampleCount@0`, `HullSynthesisState.LastGranularImpactClusterSampleFrame@96`, `SonarSynthesisState.ActiveSequence@80`, `ImpactEchoSynthesisState.CarrierPhaseA@0`, `PendingImpactEchoProbe.Valid@8`, `ThrusterSynthesisState.CavitationCarrierPhase@48`.

Compile boundary -> `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 outside SHINOBU_29: `HomeostasisBrain` missing scalability-dictator partial methods and `DroneFleetManager.DroneFleetBlackBoxEntry.Reserved0` drift. No SHINOBU_29 source file appears in the captured error list.

## 2026-05-18 - Literal Mock Signal Correction

What was wrong -> The XML assignment explicitly demanded `MockPressureSignal` and `MockTensionSignal`. The earlier implementation exposed pressure/tension inside `MockHullStressSignal`, which was not a literal pass.

What was done -> Added `MockPressureSignal` and `MockTensionSignal` as 16-byte unmanaged DTOs in `DepthStressGranularSynthesisKernel.cs`. Extended `MockHullStressSignalJob` with optional pressure/tension NativeArray outputs while preserving the aggregate hull-stress output for existing validation paths.

Cinematic Cheats used -> The mocks remain deterministic scalar oscillators. They fake pressure, tension, depth, and strain coupling without hull simulation, cable simulation, or WAV files.

Exact Microseconds saved -> No profiler capture. Runtime live path cost is 0 us unless this validation job is explicitly scheduled. The job writes only caller-owned NativeArrays and returns immediately if no output buffers are created.

Verification -> `ORDER_SCAN_PASS`, `PACK_SCAN_PASS`, and SHINOBU_29 path `git diff --check` are clean except CRLF normalization warnings. `Marshal.OffsetOf` proof: `MockPressureSignal size=16 PressureScalar@0 DepthScalar@4 VelocityScalar@8 Sequence@12`; `MockTensionSignal size=16 TensionScalar@0 StrainRateScalar@4 PressureCouplingScalar@8 Sequence@12`.

Compile boundary -> `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 outside SHINOBU_29: `GlobalPhysicsStateManager` missing `WakeRequestSignal`. No SHINOBU_29 source file appears in the captured error list.

## 2026-05-18 - Native DSP Descriptor Padding Proof

What was wrong -> The native DSP ring descriptor in the SHINOBU_29 audio surface still relied on implicit padding between a 4-byte magic and 8-byte `IntPtr` lanes. That is ABI-safe in practice but weak forensic evidence under the current L1/ARM64 mandate.

What was done -> Converted `NativeAudioKernelRingBufferDescriptor` to explicit 56-byte layout with preserved ABI offsets and manual padding. Pointer validation is 8-byte aligned. Re-ran runtime-audio `Pack=1` scan, scoped whitespace gate, descriptor layout probe, and a Core compile attempt.

Cinematic Cheats used -> No physical simulation added. The pressure-metal synth remains a scalar granular fake; this pass only removes alignment ambiguity at the native float-stream handoff.

Exact Microseconds saved -> No measured microseconds are claimed. The static gain is removal of implicit padding risk and stronger ARM64/native bridge validation.

Verification -> `AUDIO_RUNTIME_PACK_SCAN_PASS`. Descriptor proof: size 56, `DescriptorMagic@0`, `Frames@8`, `SharedState@16`, `ReadIndex@24`, `WriteIndex@32`, `CapacityFrames@40`, `CapacityMask@44`, `SharedStateLengthInts@48`. SHINOBU_29 scoped `git diff --check` is clean except CRLF normalization warnings. Full `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 outside SHINOBU_29: `InputDispatcher.cs` syntax errors at 2391, 3530, 3694 and duplicate `PhysicsWakeSignalContracts.cs` warning.

## 2026-05-18 - H8Dump Blackbox Alias

What was wrong -> The latest mandate requires `.h8dump` on fatal state. The SHINOBU_29 blackbox path preserved `.bin` dumps but did not write the mandated extension.

What was done -> Added `Dump_PROCEDURAL_SYNTH.h8dump` to `DumpGranularTelemetryCold()` while preserving `Dump_PROCEDURAL_SYNTH.bin` and legacy acoustic `.bin` aliases.

Cinematic Cheats used -> None. This is post-mortem telemetry plumbing only.

Exact Microseconds saved -> 0 us steady-state. The added write is a cold fault-path file export after `_granularTelemetryDumpRequested` is tripped.

Verification -> Static source readback confirms `.h8dump` and `.bin` targets are both present in the cold dump path. No file I/O was added to the producer/DSP loop.

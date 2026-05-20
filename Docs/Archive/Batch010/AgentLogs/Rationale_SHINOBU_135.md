# SHINOBU_135 Rationale

Status: POLISH STATIC VERIFIED / SIGNAL LANE PROMOTED / VAULT ALIAS REFRESH HARDENED / COMPILE BLOCKED BY CPU GATE

## Initial Architecture Decision

Problem: Legacy adaptive music usually crossfades long AudioSource clips and manipulates AudioMixer string parameters. That path is memory-heavy, string-bound, and not reactive at DSP cadence.

Solution: Build an owner-local procedural audio presentation system with explicit 64-byte `SynthVoiceDTO`, Burst jobs for sample/grain generation, scalar-only input snapshots, depth low-pass filtering, continuous polyphony scaling from `GlobalQualityWeight`, and a 300-frame DSP telemetry ring.

Rejected Alternatives: Static WAV stems and AudioMixer string parameters were rejected because they keep memory/I/O pressure and string hash lookup in the control path. Direct world/AUP queries in audio DSP were rejected because the audio thread must consume scalar snapshots only.

Scalability potential: Low uses 16 active grains, sparser density, LPF-heavy pressure feel. Middle raises grain density and stereo width. High increases detune/LFO detail and overlap. Ultra reaches 128 voices and richer procedural shimmer while staying presentation-only.

Hardware Impact: On i3/MX350, replacing static music crossfades with procedural grains targets lower resident audio memory and avoids disk streaming stalls. Static estimate before profiling: 0.3-1.5 ms per 512-sample synth block depending on active voices, with 16-voice low tier expected below 500 us/block. Exact proof remains PENDING VERIFICATION.

## Mandate Binding

Problem: Audio synthesis touches hot paths, native memory, ARM64 layout, AUP context, dispatcher phase, and blackbox telemetry.

Solution: Apply these mandates before coding: AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Execution_Phases, ARCH_Global_Registry_ServiceLocator_DI_Init, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.

Rejected Alternatives: Local unmanaged allocation without ownership records was rejected. Global registry polling in hot paths was rejected. Presentation music state entering gameplay rollback/hash state was rejected.

Scalability potential: The synth spends performance on perception, not simulation truth; continuous quality controls voice count, density, LFO depth, and filter detail instead of binary tiers.

Hardware Impact: Reduced managed allocation target is 0 B/call in audio and tick hot paths. Static target is less than 0.1 ms main-thread scheduling overhead and less than 1.5 ms DSP block time before telemetry dump.

## Vault Lane And Core ID Decision

Problem: The synth needs persistent voice/output/telemetry buffers, but private `NativeArray` ownership inside a MonoBehaviour would violate Data Sovereignty and fragment lifetime control.

Solution: Added owner-local `SystemID.AudioDynamicSynth` and BufferIDs `AudioDynamicSynthVoices` through `AudioDynamicSynthSharedState` in `H8Memory.cs`, then resolved all runtime arrays from `GlobalDataVault` with `NativeArrayOptions.UninitializedMemory`.

Rejected Alternatives: Reusing the older stem mixer buffers was rejected because one fact needs one owner; sharing would create aliasing between legacy stem telemetry and new DSP output. Private persistent `NativeArray` fields were rejected because the Vault must own backing memory.

Scalability potential: The lane supports low/middle/high/ultra by keeping fixed-capacity buffers and changing active voice count through `GlobalQualityWeight`, not by reallocating or loading different assets.

Hardware Impact: Avoids per-scene native allocator churn and keeps the hot `SynthVoiceDTO` stride at exactly one 64-byte cache line for Quest-class ARM64 and desktop SIMD prefetch.

## Buffer ID Collision Correction

Problem: The first synth Vault lane used `70810..70821`, but static scan proved those numeric IDs are already used by `ToxicOutgassingChemistryRuntime` and `TBDRPipelineSurgeonTypes` through local `(BufferID)` constants.

Solution: Moved the dynamic synth owner-local lane to `71700..71711` in `H8Memory.cs` and updated the binary payload ledger. The synth code refers to enum names, so the route remains stable while the underlying numbers become collision-free.

Rejected Alternatives: Rejected keeping the collided IDs with owner separation because `GlobalDataVault` resolves by `BufferID`, not by intention. Rejected editing the other systems because they are outside SHINOBU_135 ownership and already documented by their local constants.

Scalability potential: Fixed IDs preserve one owner per buffer across low/middle/high/ultra modes; quality changes still modify active work, not memory routing.

Hardware Impact: Prevents false buffer aliasing that could corrupt DSP voice/output memory or force defensive runtime validation in the audio path. Static scan target is zero `71700..71711` collisions outside the synth enum.

## Static WAV Transport Removal Decision

Problem: `HectonMusicDirector` and `AdaptiveStemAudioMixer` still represented music as clip/stem transport, with `AudioSource.Play`, clip residency touches, and source volume/cutoff mutation as the runtime pathway.

Solution: Added procedural ownership gates. The director now publishes scalar tension/depth through `DynamicMusicScalarSignal`, injects stingers as mathematical impulses, and does not require a music voice pool. The adaptive stem mixer disables serialized stem sources cold and publishes its computed tension/depth/quality scalars through the same contract instead of starting clips.

Rejected Alternatives: Deleting the entire legacy director/profile code was rejected for this pass because it is entangled with scene profile selection and would widen the compile surface. Leaving AudioSources active at zero volume was rejected because it still allows clip residency and hidden transport work.

Scalability potential: Low-tier devices hear the same emotional route with fewer active grains; high/ultra devices spend saved RAM/I/O budget on richer grain density and detune, not on more loaded stems.

Hardware Impact: Static estimate is removal of long music clip streaming/decompression pressure from the active path and replacement with a bounded `MemCpy` from a Vault output buffer on the audio thread.

## Compile Wall Assembly Isolation Correction

Problem: The first procedural music patch kept the synth type in the broad audio/Core compile region and let `HectonMusicDirector`/`AdaptiveStemAudioMixer` call `DynamicMusicGranularSynthesizer` directly. That solved playback but failed the compile-wall standard: Core-level legacy audio code would know the concrete synth implementation.

Solution: Moved the runtime synth into `Hecton8.Audio.Synthesis` under `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic` and the editor facade into `Hecton8.Audio.Synthesis.Editor`. Added a 64-byte `DynamicMusicScalarSignal` contract in `Hecton8.Core.Contracts.Signals`; Core producers push scalar context to SignalBus, and the synth consumes the snapshot. The synth self-creates after scene load, so Core no longer needs a direct type reference.

Rejected Alternatives: Rejected adding a Core asmdef reference to `Hecton8.Audio.Synthesis` because that would invert ownership and make Core recompile against a presentation implementation. Rejected keeping the synth in the broad Core assembly because that protects no compile boundary.

Scalability potential: Low/middle/high/ultra behavior still changes only through `GlobalQualityWeight` and scalar DTOs; assembly isolation does not alter runtime math. It does reduce iteration blast radius for future synth work.

Hardware Impact: Runtime cost is one bounded SignalBus scalar packet per producer update plus one bounded snapshot scan in the synth scheduling pass. No audio-thread cost is added; `OnAudioFilterRead` remains copy/zero-fill only.

## Scalar-Only AUP Boundary Decision

Problem: Music must react to AUP-derived depth/threat context without putting `double3` math or world queries into DSP loops or the Unity audio callback.

Solution: Added `DynamicMusicScalarSignal` as the cross-assembly scalar route. Producers resolve AUP/world context outside the synth; the synth consumes only finite local floats and the Burst jobs map those scalars to pitch, density, LPF cutoff, and active voices.

Rejected Alternatives: Polling player movement, predator positions, or `WorldSpatialHashGrid` directly from the synth was rejected because it would couple audio presentation to sibling runtime domains and risk AUP jitter in the high-frequency DSP lane.

Scalability potential: The scalar contract is stable across weak and high-end hardware; only the cost curve changes inside the synth.

Hardware Impact: Keeps audio scheduling to scalar copies and Burst jobs; no spatial query work is introduced into the audio route.

## External Scalar Precedence Correction

Problem: The first mock tension job draft used `max(externalDepth, mockDepthWave)`, which could overwrite a valid shallow AUP-derived depth scalar with emergency mock depth. The first external quality path also multiplied by zero when no external publisher had run yet.

Solution: Added an explicit `HasExternalScalars` value into `GenerateMockTensionJob`. External depth now has absolute precedence when published; the sine-wave depth is fallback only. External quality defaults to `1.0` when absent so the synth remains audible before upstream systems publish. `HectonMusicDirector` now forwards `HomeostasisBrain.GlobalQualityWeight` instead of fixed `1.0`, preserving the continuous quality law.

Rejected Alternatives: Rejected always blending external and mock depth because that creates non-authoritative audio pressure changes. Rejected polling world depth inside the synth because that violates the scalar-only AUP boundary.

Scalability potential: The quality curve remains continuous across low/middle/high/ultra, while external depth now changes timbre predictably instead of being masked by mock data.

Hardware Impact: Adds one integer branch inside the scalar job only. It prevents false LPF depth work and preserves deterministic scalar ownership.

## CSV And Editor Facade Decision

Problem: Designers need tuning without recompilation, but a runtime text parser or UI overlay would violate the hot-path allocation budget.

Solution: Added cold `Docs/Audio/synth_presets.csv` ingestion into Vault scratch bytes and an editor-only `AbyssalSynthTunerWindow` with layout validation, sliders, oscilloscope, and 60-second tension/voice graph.

Rejected Alternatives: Runtime Canvas/TMP graphs and managed per-frame string parsing were rejected. Per-line preset reset in the first parser draft was rejected because it prevented a complete biome/narrative rule from accumulating across the CSV file.

Scalability potential: Low/middle/high/ultra behavior can be retuned by CSV and editor sliders while the compiled DSP kernel remains unchanged.

Hardware Impact: Player runtime cost is 0 us for the editor window. CSV parsing runs cold and reads bytes into Vault scratch rather than allocating split strings.

## Final Static Audit And CPU Gate Decision

Problem: The last pass needed proof that the CSV parser, layout claims, and Burst directives were not paper-only, while the batch forbids launching `dotnet build` under high CPU load or while another compiler is active.

Solution: Verified every CSV key hash against the exact lower-ASCII FNV-1a parser; changed CSV reading to a bounded loop over Vault scratch bytes so partial `FileStream.Read(Span<byte>)` cannot silently truncate a preset. Re-ran targeted static scans for forbidden hot-path constructs, Burst attributes, `.Complete()` sites, and BufferID collisions. Sampled process and CPU gates before any compile attempt.

Rejected Alternatives: Rejected launching `dotnet build` at 70.52% total CPU, 88.27% on recheck, then 86.92% on final recheck, because the governing rule explicitly blocks builds above 50%. Rejected claiming compile success from static analysis. Rejected a single `FileStream.Read` call because it can legally return fewer bytes than requested.

Scalability potential: The quality curve remains data-driven from CSV/editor and `GlobalQualityWeight`; low devices reduce active voices and density, middle/high raise density and stereo width, ultra reaches the fixed 128-voice ceiling without memory churn.

Hardware Impact: Static verification preserves the build machine and avoids a forbidden compile wall under load. Runtime hot path remains unchanged: OnAudioFilterRead is copy/zero-fill only, while Burst DSP remains bounded by active voice count.

## Polish Pass: Signal Truth And File-System Gate

Problem: The self-audit was too generous in two places. `FlagUsingMockTension` was set even when real external scalar context had precedence, which would corrupt telemetry interpretation. CSV hot-reload polling also ran from `SlowTick`, which is acceptable in editor tooling but not in player runtime where filesystem probes can cause MicroSD or mobile storage stalls.

Solution: Changed the scalar job so `FlagUsingMockTension` is emitted only when `HasExternalScalars == 0`. Restricted `PollCsvRulesCold()` body to `UNITY_EDITOR || DEVELOPMENT_BUILD`; cold boot CSV ingestion remains available, while repeated file timestamp checks are removed from shipping player cadence. Expanded stinger ingestion through existing typed lanes: `CombatDamageSignal`, `HullDeformedSignal`, and `WaterlineBreachSignal`.

Rejected Alternatives: Rejected inventing a local `HullBreachSignal` because `HullDeformedSignal` and `WaterlineBreachSignal` already exist in the SignalBus nervous system. Rejected polling file timestamps in release player because designer hot-reload is not worth storage jitter in a 60 FPS VR target.

Scalability potential: Low devices now shed filesystem polling entirely in release builds. Middle/high/ultra retain editor/development hot tuning, while the shipped DSP route remains scalar-only and quality-weighted.

Hardware Impact: Removes recurring release-player filesystem probes from slow tick. Adds two bounded `ReadOnlySpan<T>` SignalBus scans on the main scheduling path; both consume existing frame snapshots and do not allocate.

## Post-Isolation Static Verification

Problem: Moving the synth into `Hecton8.Audio.Synthesis` could have silently left a Core-to-implementation reference or introduced an asmdef/editor compile risk.

Solution: Re-ran targeted static checks after the move. `HectonMusicDirector`, `AdaptiveStemAudioMixer`, and Core files contain no `DynamicMusicGranularSynthesizer` or `Hecton8.Audio.Synthesis` reference. `git diff --check` passed for SHINOBU_135 touched files. Forbidden-pattern scan stayed clean. Burst directive scan still reports 3/3 exact required attributes. Compiler processes were absent, but total CPU was 100%, so build remains intentionally blocked.

Rejected Alternatives: Rejected running `dotnet build` under 100% CPU. Rejected leaving the old compile-guard report unchanged because it falsely claimed no asmdef was added.

Scalability potential: This is an iteration-speed correction, not a runtime quality change; future low/middle/high/ultra synth tuning can compile in the audio synthesis assembly without widening Core.

Hardware Impact: Build machine protection was honored. Runtime overhead remains one scalar SignalBus lane and no audio-thread change.

## Signal Lane Promotion And NaN Guard

Problem: `DynamicMusicScalarSignal` worked through SignalBus fallback registration, but fallback was a weak contract for this domain. It had no central lane capacity entry, no direct dispatch/clear line item, no explicit size validation, and no finite payload guard. A bad external producer could inject NaN tension/depth/quality scalars into the music lane before synth-side clamps saw the packet.

Solution: Promoted `DynamicMusicScalarSignal` into `GlobalSignals` direct dispatch. Added explicit capacity, direct pre-simulation flush, direct post-simulation clear, 64-byte validation, and finite guard `0x51A10060`. The guard clamps the mutable copy and rejects corrupted payloads through existing SignalBus corruption accounting, preserving the synth's scalar-only AUP boundary.

Rejected Alternatives: Rejected leaving the route as fallback because it makes the music lane invisible to central signal audits. Rejected a Core-to-synth direct callback because that would break the compile wall. Rejected accepting sanitized NaN packets silently because upstream producers need measurable corruption telemetry.

Scalability potential: Low/middle/high/ultra behavior remains controlled by `GlobalQualityWeight`; the lane promotion only hardens routing and validation. Low-tier still receives bounded 8-frame scalar snapshots, while higher quality can consume the full 64-frame snapshot budget without reallocating.

Hardware Impact: Adds one direct SignalBus lane entry and finite checks on producer push only. Audio thread cost remains unchanged; `OnAudioFilterRead` is still copy/zero-fill only.

Verification: Static grep confirms `DynamicMusicScalarSignal` appears in direct flush, direct clear, direct dispatch policy, finite guard resolver, sanitizer, 64-byte validation, and central configure. `git diff --check` passed for the lane/docs patch with LF-to-CRLF warnings only. Direct reference grep found no `Hecton8.Audio.Synthesis` or `DynamicMusicGranularSynthesizer` in Core, `HectonMusicDirector`, or `AdaptiveStemAudioMixer`. Compile remains blocked: no compiler processes were present, but CPU sampled at 100%.

## Grain Interpolation Math LOD

Problem: The previous DSP kernel scaled active voice count with `GlobalQualityWeight`, but each active voice still performed the same grain-bank interpolation. That satisfied continuous polyphony but did not satisfy the stricter low-quality collapse rule for the sampling math itself.

Solution: Added a quality-controlled interpolation admission scalar inside `GranularSynthesisJob`: `math.step(0.3f, qualityWeight)` gates the second grain tap, and a `Smooth01` polynomial scales the fractional interpolation weight from q=0.3 to q=1.0. Below q=0.3, `nextIndex == baseIndex` and `frac == 0`, so grain sampling is nearest-neighbor without an `IsLowEndHardware` branch.

Rejected Alternatives: Rejected a direct `if (quality < 0.3f)` because binary quality switches are forbidden. Rejected removing interpolation entirely because high/ultra should spend saved cycles on smoother grain texture.

Scalability potential: Low-tier now uses 16-ish active voices plus nearest-neighbor grain lookup. Middle gradually restores fractional interpolation. High/ultra reach the full linear grain texture while still sharing the same Burst kernel and Vault buffers.

Hardware Impact: On weak devices the second grain tap resolves to the same address and interpolation weight is zero; the main win remains active-voice reduction, but the sample path now has a formal mathematical collapse point for q<0.3.

Verification: Static grep found `interpolationAdmission`, `interpolationCurve`, same-index tap collapse, and the unchanged 3/3 required Burst directives. `git diff --check` passed for the synth/docs patch with LF-to-CRLF warnings only. No forbidden hot-path pattern was found in the touched SHINOBU audio files.

## Neighbor Kernel Burst Hygiene

Problem: `DepthStressGranularSynthesisKernel.cs` sits in the same `Hecton8.Audio.Synthesis` asmdef. Its Burst attributes used the correct flag values but not the exact mandated form, its job NativeArray fields lacked `[NoAlias]`, and several Burst jobs used struct object initializers. That left SIMD aliasing proof and source-level zero-allocation discipline weaker than the domain standard.

Solution: Reordered all five Burst attributes to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`, marked every NativeArray job field in the file with `[NoAlias]`, and replaced Burst job struct object initializers with `default` plus direct public-field assignment.

Rejected Alternatives: Rejected ignoring the file because it compiles into the same audio synthesis assembly and can influence Burst safety/performance expectations. Rejected adding new abstractions or changing runtime ownership because this pass needed hygiene inside the local domain, not a behavior rewrite.

Scalability potential: The hardening does not change low/middle/high/ultra audio behavior. It gives Burst clearer non-aliasing facts so weak devices retain vectorization opportunities, while high/ultra can spend higher voice counts without hidden aliasing pessimism.

Hardware Impact: Runtime output should be unchanged. Expected gain is compile-time SIMD eligibility and removal of defensive alias assumptions in adjacent audio jobs; no new allocations, callbacks, or cross-domain dependencies were introduced.

Verification: `git diff --check` passed on `DepthStressGranularSynthesisKernel.cs` with LF-to-CRLF warning only. Static grep found 5/5 exact Burst attributes and `[NoAlias]` on all NativeArray job fields. Forbidden-pattern scan over touched audio synthesis/Core contract files found no `Pack=1`, hot DTO properties, `UnityEngine.Random`, `foreach`, runtime `new NativeArray/List/HashMap`, or `AudioMixer.SetFloat`. Build remains blocked by CPU gate: no compiler processes, total CPU 100%.

## Vault Alias Refresh Hardening

Problem: The synth already requested memory through `VaultBufferHandle<T>`, but `EnsureVaultStorage()` could early-return on cached `NativeArray` aliases. After a legal Vault generation bump, that path risked keeping stale array views and stale raw output pointers until a full disposal path occurred.

Solution: Added `TryRefreshVaultAliases()` so every non-fenced `EnsureVaultStorage()` pass resolves all synth views through generation-checked handles and refreshes `_outputPtrA/_outputPtrB` before reuse. Added a compaction-fence guard that keeps the currently valid aliases during an active Vault fence instead of resolving handles through `ResolveBuffer`, which intentionally treats fenced resolution as a stale-handle fatal path.

Rejected Alternatives: Rejected calling `Resolve` blindly during a compaction fence because `GlobalDataVault.ResolveBuffer` is designed to fail fast there. Rejected keeping the cached-alias early return because it made handle generation metadata cosmetic instead of authoritative.

Scalability potential: Low/middle/high/ultra audio math is unchanged. The fix protects all tiers against stale pointer reuse while preserving the same bounded buffer capacities and quality-weighted voice counts.

Hardware Impact: Adds bounded handle validation on the main scheduling path, not on `OnAudioFilterRead`. Audio callback still copies from raw ready-buffer pointers only. Expected cost is below telemetry noise compared with the DSP block and avoids catastrophic stale pointer failure after Vault relocation.

Verification: `git diff --check` passed on `DynamicMusicGranularSynthesizer.cs`. Static grep confirms `TryRefreshVaultAliases()` is used before `EnsureVaultStorage()` returns and refreshes both raw output pointers. Forbidden-pattern scan over touched audio synthesis/Core contract files stayed clean. Build remains blocked by CPU gate.

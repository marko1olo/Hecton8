# Rationale_SHINOBU_29

## Decision 001 - Keep Managed Callback Out Of Runtime DSP

Problem: The batch prompt asks for float[] audio-stream rendering, but the current project architecture and smoke tests forbid managed callback synthesis in PlayerCriticalProceduralAudioRenderer. Managed callback synthesis risks GC spikes, blocking, and crackle.

Solution: Treat the existing native producer plus SPSC frame ring as the authoritative float-stream path. Any editor oscilloscope will read telemetry only. Runtime synthesis remains in native arrays and published blocks.

Rejected Alternatives: Standard Unity AudioSource/OnAudioFilterRead synthesis and direct AudioClip WAV playback. Both increase hot-path GC risk and violate the local DSP thread-safety mandate.

Scalability potential: Low uses cheap procedural grain bank and capped voices. Middle increases overlap density. High increases pressure modulation detail. Ultra spends saved cycles on harsher spectral overkill while staying under the 0.1 ms suspicion threshold.

Hardware Impact: On i3/MX350-class silicon, avoiding managed callback allocations prevents crackle from GC pauses and should save unpredictable millisecond-scale stalls rather than a stable microsecond amount.

## Decision 002 - Status Memory Is Disk-First

Problem: SHINOBU_29 had no existing status/rationale files, so context compression would erase task state.

Solution: Create Status_SHINOBU_29.md and Rationale_SHINOBU_29.md before code edits, with 20 task slots and five loop gates.

Rejected Alternatives: Relying on chat history or IDE tabs. Those are volatile and fail the anti-amnesia protocol.

Scalability potential: Disk-based task memory has no runtime cost and allows later agents to audit exact decisions.

Hardware Impact: 0 us runtime. Editor shell/file IO only.

## Decision 003 - Sixteen-Byte DTO Boundary

Problem: The prompt requires ARM64-friendly DTOs, and property-wrapped structs would recreate CS1612 copy/update hazards.

Solution: Added `SynthParametersDTO` and `GrainPlaybackStateDTO` with exactly four 4-byte fields each, `StructLayout(Size = 16)`, no `Pack=1`, raw fields, and `UnsafeUtility.AsRef` helpers.

Rejected Alternatives: C# properties, 28-byte voice structs as thread DTOs, or Pack=1 byte packing. Those add copies or create bad alignment for the audio thread.

Scalability potential: Low/Middle devices get predictable cache lines; High/Ultra can raise density without paying DTO marshaling waste.

Hardware Impact: i3/MX350 benefit is alignment and fewer cache misses; exact profiler us is pending Unity Profiler, but the DTO path remains one 16-byte load/store.

## Decision 004 - Emergency Grain Fake Instead Of PCM Hunt

Problem: Archive scans found rationale evidence for old granular work but no usable synth_grain_banks.h8bin or audio_oscillator_profiles.bin PCM payload. The project also contains large ambience WAVs, which are not acceptable as pressure-metal grains.

Solution: Added explicit `GenerateEmergencyMockGrains()` entry points and kept the live renderer on the existing GlobalDataVault-backed metallic grain bank. Oversized authored metal-stress clips are rejected before `AudioClip.GetData`.

Rejected Alternatives: Loading Atmos/Underwater Ambient WAVs as grains, adding Submarine_Groan_01..50, or trusting a designer to assign only small clips.

Scalability potential: Low uses deterministic procedural grit. Middle/High/Ultra can still use a tiny <=2s authored grain if intentionally assigned.

Hardware Impact: Prevents 20-34 MB source WAV residency from entering this synth path; hot-path cost stays 0 us for asset rejection.

## Decision 005 - Tuning Through Existing Snapshot

Problem: The editor facade needs live control without racing the audio producer or adding locks.

Solution: Added four tuning scalars to `AudioParameterSnapshot`; editor calls `ApplyGranularSynthTuning`, and the audio producer consumes them after the existing double-buffered swap.

Rejected Alternatives: Direct field reads in DSP, lock-based communication, or GlobalRegistry lookup inside the producer loop.

Scalability potential: Low clamps density/length down; Middle uses balanced density; High/Ultra can push overlap and FM harshness while the voice cap still gates work.

Hardware Impact: One-time snapshot copy per block. Expected i3/MX350 savings come from avoiding lock stalls and callback races rather than lowering steady-state arithmetic.

## Decision 006 - Sonar FM And Pressure Metal Are Scalar Fakes

Problem: Physical tearing metal and sonar propagation are too expensive and uncontrollable for the audio thread.

Solution: Kept granular pressure metal as randomized scalar grain playback and added FM sideband modulation to sonar chirp using existing oscillator state.

Rejected Alternatives: WAV sonar cues, finite-element hull acoustics, acoustic ray solvers in the synth.

Scalability potential: Low uses fewer active grains and lower density. Middle adds overlap. High/Ultra use stronger FM/sideband harshness and interpolation already gated by voice count.

Hardware Impact: FM sideband adds two oscillator advances only while sonar chirp is active. Granular changes are per-armed-grain scalar multipliers.

## Decision 007 - Editor Tuner And CSV Are Cold Control Surfaces

Problem: Human sound design needs live control and oscilloscope feedback, but editor UI must not contaminate runtime DSP with managed allocations.

Solution: Added `GranularSynthTunerWindow` with preallocated scope arrays, timestamp-based CSV monitoring, and span/hash parsing. Runtime audio only receives sanitized scalar values via the snapshot.

Rejected Alternatives: Allocating scope buffers in OnGUI, using string Split/LINQ for profile parsing, or reading a managed audio callback buffer.

Scalability potential: Low-end defaults stay conservative; designers can author Low/Middle/High/Ultra profiles in CSV without touching synth code.

Hardware Impact: 0 us runtime when editor window is closed or in player builds. Editor-only allocations are cold and documented.

## Decision 008 - Black Box Alias For This Agent

Problem: Existing granular telemetry dumps went to structural/acoustic agent names, not the current procedural synth owner requested by the prompt.

Solution: Added `Dump_PROCEDURAL_SYNTH.h8dump` and `Dump_PROCEDURAL_SYNTH.bin` as additional cold fault dump targets while preserving existing dump names.

Rejected Alternatives: Renaming existing dumps and breaking other agents' diagnostics, or logging text from the audio thread.

Scalability potential: Same 300-entry ring for all tiers; no per-tier memory growth.

Hardware Impact: 0 us steady state except existing telemetry stride writes; binary dump happens only after invalid state.

## Decision 009 - Verification Boundary

Problem: Full project build is currently red from non-audio missing symbols/signature drift, so claiming a green build would be false.

Solution: Ran Hecton8.Core build, recorded the unrelated blockers, then separately kept SHINOBU_29 scoped verification as the authority for this surface. The latest restore/build attempt reaches project compile and fails in construction drone code, not in the procedural audio files.

Rejected Alternatives: Editing construction/drone dependencies outside the assigned domain; hiding the compile wall; or claiming a green build from targeted scans.

Scalability potential: Verification isolation lets audio changes proceed without coupling to other agents' unfinished code.

Hardware Impact: 0 us runtime; prevents integration churn.

## Decision 011 - Continue Pass Truth Correction

Problem: The active batch prompt uses attributes on the opening tag, so a strict `<AGENT_PROMPT id="SHINOBU_29">` regex falsely reports absence even though the SHINOBU_29 prompt is present at the current batch source.

Solution: Re-extracted with an attribute-aware regex: `<AGENT_PROMPT\s+id="SHINOBU_29"[^>]*>.*?</AGENT_PROMPT>`. The recovered prompt still contains exactly 20 tasks and the self-reflection mandate. `<POLISH_MANDATE>` remains absent as a separate tag.

Rejected Alternatives: Trusting the earlier strict-regex miss, relying on chat memory, or reading neighboring prompts.

Scalability potential: 0 runtime change. The value is architectural containment under concurrent agent churn.

Hardware Impact: 0 us runtime. Cold CLI verification only.

## Decision 012 - Audio State Cluster Alignment Expansion

Problem: DTO alignment alone was too narrow. The player-critical renderer also holds DSP state structs copied and mutated around the producer path; several had implicit runtime layout and one pending-impact probe used a managed `bool`.

Solution: Added explicit `StructLayout(Size=...)` with 8-byte-multiple sizes to the SHINOBU_29 audio state cluster: `SonarEchoCompositeGroup` 72, `HighSpeedImpactDuplicateEntry` 16, `SonarTriggerState` 32, `AudioThreadDiagnostics` 32, `AudioParameterSnapshot` 256, `HullSynthesisState` 256, `SonarSynthesisState` 96, `AmbientCurrentSynthesisState` 72, `ImpactEchoSynthesisState` 48, `HeartbeatSynthesisState` 24, `CriticalSidechainCompressorState` 8, `VwsPlaybackState` 56, `TinnitusSynthesisState` 16, `LeviathanGranularSynthesisState` 40, `InteriorFdnReverbSynthesisState` 32, `PendingImpactEchoProbe` 16, `ThrusterSynthesisState` 136, `SabineReverbSynthesisState` 40, and `CaveConvolutionReverbSynthesisState` 16. `PendingImpactEchoProbe.Valid` is now a byte flag.

Rejected Alternatives: Stopping at the two 16-byte DTOs or applying a project-wide Pack=1 purge across world/save/AI structs. Broad cross-domain repacking is outside this agent and would create integration risk under concurrent edits.

Scalability potential: Low/ARM64 avoids implicit layout drift in the audio state cluster. High/Ultra keep deterministic cache-sized state while using saved budget for denser pressure-metal layering.

Hardware Impact: No profiler microseconds are claimed. Static effect is deterministic struct size and removal of a managed bool from an audio runtime state struct.

<SELF_AUDIT>
  <OnAudioFilterReadAllocations>No OnAudioFilterRead method was added. Runtime synthesis remains in preallocated NativeArray/SPSC producer buffers; editor oscilloscope arrays are cold EditorWindow fields.</OnAudioFilterReadAllocations>
  <SynthParametersDTOLayout>16 bytes: BaseFrequency float 0-3, ModulationIndex float 4-7, GrainSize float 8-11, PressureScalar float 12-15. StructLayout Sequential Size=16, no Pack=1.</SynthParametersDTOLayout>
  <PropertiesAvoided>DTOs use raw public fields and UnsafeUtility.AsRef. No get/set properties are used for synth DTO mutation.</PropertiesAvoided>
  <Mocks>MockHullStressSignal and MockHullStressSignalJob provide MockStress, MockTension, MockDepth, and MockSubmarineVelocity without Hull Integrity dependencies.</Mocks>
  <EditorFacade>Granular Synth Tuner exists with Base Pitch, Grain Length, Overlap Density, FM Modulation Index, CSV profile reload, and oscilloscope telemetry readback.</EditorFacade>
  <Result>PASS</Result>
</SELF_AUDIT>

## Polish Mandate Result

Problem: The protocol requires reading `<POLISH_MANDATE>` only after every core task is checked.

Solution: After Status_SHINOBU_29.md reached 20/20 checked, extracted the tag from CURRENT_BATCH.md. The tag was absent, so the final anti-bloat pass used the existing mandate set and touched-file static scans.

Rejected Alternatives: Reading polish instructions early, inventing a mandate that is not in the batch file, or claiming a Unity profiler result that was not captured.

Scalability potential: No additional runtime code.

Hardware Impact: 0 us runtime.

## Decision 010 - Ultra Polish Alignment Pass

Problem: The second mandate correctly identified a remaining defect: several SHINOBU_29-surface audio structs still used `Pack = 1`, including legacy synthesis voice state, granular telemetry, sonar taps, impact events, and the audio snapshot cache slot.

Solution: Removed packed runtime layout from the touched procedural-audio surface. Added explicit `Size` values and padding fields where needed: DepthStressGranularVoice 32 bytes, DepthStressGranularSpawnState 16 bytes, KineticImpactSineOscillatorState 24 bytes, SonarEchoTap 64 bytes, GranularAudioTelemetryEntry 48 bytes, PrologueAudioTransitionTelemetryEntry 56 bytes, ImpactAudioEvent 64 bytes, AudioParameterSnapshotSlot 320 bytes, and AudioParameterSnapshotCacheLinePad 64 bytes.

Rejected Alternatives: Leaving old `Pack = 1` because existing smoke tests tolerated it, or sweeping unrelated AI/save/world packed structs outside the assigned audio domain. Broad cross-domain repacking would be an integration hazard and belongs to an architecture-wide mandate.

Scalability potential: Low/Quest/Steam Deck avoid unnecessary packed-layout penalties in this audio surface. High/Ultra keep the same cache-line sized surfaces while spending cycles on density/FM harshness, not marshaling waste.

Hardware Impact: Exact profiler microseconds are not claimed. Static improvement is removal of packed runtime layout and restoration of 8-byte-multiple struct sizes on the touched SHINOBU_29 surface.

<SELF_AUDIT_ULTRA>
  <TaskMatrix>
    <Task01 status="PASS">Archive/binary reconnaissance completed; no usable PCM bank found; emergency generator present.</Task01>
    <Task02 status="PASS">Submarine_Groan WAV dependency absent; pressure metal source is DataVault NativeArray grain bank with oversized WAV rejection.</Task02>
    <Task03 status="PASS">SynthParametersDTO uses raw fields and UnsafeUtility.AsRef, no properties.</Task03>
    <Task04 status="PASS">GrainPlaybackStateDTO is 16 bytes, no Pack=1.</Task04>
    <Task05 status="PASS">MockHullStressSignal and Burst job provide local stress/tension/depth/velocity.</Task05>
    <Task06 status="PASS">No managed OnAudioFilterRead synthesis added; current native producer/SPSC contract preserved.</Task06>
    <Task07 status="PASS">Granular solver uses fixed SOA voice lanes and NativeArrays.</Task07>
    <Task08 status="PASS">Pressure groan uses randomized granular fake, not physical FEM.</Task08>
    <Task09 status="PASS">Sonar FM sideband added with scalar modulation.</Task09>
    <Task10 status="PASS">Tuning travels through double-buffered snapshot with one block-level Volatile.Read.</Task10>
    <Task11 status="PASS">Pitch bending remains scalar; MockSubmarineVelocity exists for isolated validation.</Task11>
    <Task12 status="PASS">GranularMaxVoiceCount tier gate preserved for polyphony throttling.</Task12>
    <Task13 status="PASS">No double3/AUP enters audio DTOs.</Task13>
    <Task14 status="PASS">Existing depth LPF/muffling path remains active.</Task14>
    <Task15 status="PASS">Granular output remains under FastSoftClip/master limiter path.</Task15>
    <Task16 status="PASS">HanningWindowBuildJob and HanningLut read path added.</Task16>
    <Task17 status="PASS">300-entry telemetry ring active; Dump_PROCEDURAL_SYNTH.h8dump and .bin aliases added.</Task17>
    <Task18 status="PASS">Granular Synth Tuner editor facade added.</Task18>
    <Task19 status="PASS">CSV profile monitor/parser added; no Split/LINQ token arrays.</Task19>
    <Task20 status="PASS">Oscilloscope uses fixed editor buffers and telemetry readback.</Task20>
  </TaskMatrix>
  <StructLayout>
    <SynthParametersDTO size="16">0 BaseFrequency f32, 4 ModulationIndex f32, 8 GrainSize f32, 12 PressureScalar f32.</SynthParametersDTO>
    <GrainPlaybackStateDTO size="16">0 CurrentPhase f32, 4 Pitch f32, 8 Amplitude f32, 12 GrainStartIndex u32.</GrainPlaybackStateDTO>
    <DepthStressGranularVoice size="32">0 Cursor f32, 4 PlaybackRate f32, 8 Gain f32, 12 StartSample i32, 16 LengthSamples i32, 20 Seed u32, 24 Active u8, 25-31 padding.</DepthStressGranularVoice>
    <GranularAudioTelemetryEntry size="48">0 SampleIndex u32, 4-24 six f32 telemetry values, 28 ActiveVoices i32, 32 VoiceLimit i32, 36 ActiveEchoTaps i32, 40 Flags u32, 44-47 padding.</GranularAudioTelemetryEntry>
    <PrologueAudioTransitionTelemetryEntry size="56">0 Frame u32, 4 Sequence u32, 8 DspFlags u32, 12-40 eight f32 values, 44 SplashdownSamplesRemaining i32, 48-51 four u8 flags, 52-55 padding.</PrologueAudioTransitionTelemetryEntry>
    <AudioParameterSnapshot size="256">0 HullStress f32, 140 GranularBasePitchScale f32, 152 GranularFmModulationIndex f32, 216 PrologueFlags i32, 220-255 padding.</AudioParameterSnapshot>
    <AudioParameterSnapshotSlot size="320">0 AudioParameterSnapshot, 256 cache-line pad, no Pack=1.</AudioParameterSnapshotSlot>
    <PendingImpactEchoProbe size="16">0 Excitation f32, 4 ExpireAt f32, 8 Valid u8, 9-15 padding.</PendingImpactEchoProbe>
  </StructLayout>
  <ZeroGC>No Tick() hot-path allocations, no LINQ/Split in runtime; editor arrays are cold and editor-only.</ZeroGC>
  <AUP>Audio DTOs are scalar only. Sonar/AUP logic remains in existing systems; no absolute double3 was added to the synth buffers.</AUP>
  <HPhi>Runtime audio arrays are DataVault aliases or caller-supplied NativeArrays. No persistent private NativeArray was added.</HPhi>
  <Dependency>No new asmdef, no Contracts change, no sibling runtime reference. Editor facade resolves GlobalRegistry.PlayerCriticalAudio first.</Dependency>
  <DearLie>Pressure metal collapse is faked with deterministic metallic grit plus randomized granular overlap and scalar FM, not physical acoustics.</DearLie>
  <Blackbox>Granular 300-entry NativeArray ring remains active and now writes Dump_PROCEDURAL_SYNTH.h8dump plus Dump_PROCEDURAL_SYNTH.bin on invalid state.</Blackbox>
  <CompileGuard>Targeted Roslyn compiles passed. Latest Core no-restore build is blocked outside SHINOBU_29 by HomeostasisBrain scalability-dictator partial methods and DroneFleetManager Reserved0 drift.</CompileGuard>
</SELF_AUDIT_ULTRA>

## Decision 013 - L1 Field-Order Correction

Problem: `StructLayout(Size=...)` alone did not prove the L1/cache-line mandate. Several private audio runtime state structs still had 8-byte phase/frame fields after 4-byte fields, and `PrologueAudioTransitionTelemetryEntry` used a `uint` padding field after byte flags.

Solution: Reordered the private SHINOBU_29 audio state fields so `long`/`double` fields lead, 4-byte `float`/`int`/`uint` fields follow, and byte flags/padding sit at the tail. `PendingImpactEchoProbe.Valid` now sits after two floats at offset 8, and prologue telemetry uses byte pad fields instead of `uint` padding after bytes.

Rejected Alternatives: Leaving field order unchanged because `Size` was already a multiple of 8, or changing external world/save/AI structs outside the procedural-audio domain. The first is insufficient for the mandate; the second is a cross-domain compile-risk under concurrent agents.

Scalability potential: Low/ARM64 gets deterministic natural alignment for phase/frame fields. High/Ultra keep the same fixed state sizes while spending audio budget on denser granular overlap and harsher FM texture.

Hardware Impact: No profiler microseconds are claimed. Static proof: field-order scan passed, `Pack=1` scan passed, and `Marshal.OffsetOf` confirmed `HullSynthesisState.LastGranularImpactClusterSampleFrame@96`, `SonarSynthesisState.ActiveSequence@80`, `ImpactEchoSynthesisState.CarrierPhaseA@0`, `PendingImpactEchoProbe.Valid@8`, and `ThrusterSynthesisState.CavitationCarrierPhase@48`.

<SELF_AUDIT_L1>
  <StructLayout>
    <SonarTriggerState size="32">0 StartFrame i64, 8 Sequence i32, 12 EchoRevision i32, 16 Intensity f32, 20 EchoTapCount i32, 24 Flags i32.</SonarTriggerState>
    <AudioThreadDiagnostics size="32">0 ProducedSampleCount i64, 8 BufferedFrames i32, 12 WritableFrames i32, 16 OverflowDropCount i32, 20 ImpactEventQueueDropCount i32, 24 ProducerRunning i32.</AudioThreadDiagnostics>
    <HullSynthesisState size="256">0 PressureLfoPhase f64, 96 LastGranularImpactClusterSampleFrame i64, 104 first 4-byte scalar, 240 final 4-byte scalar.</HullSynthesisState>
    <SonarSynthesisState size="96">0 first f64 phase, 64 first f32 filter state, 80 ActiveSequence i32, 84 EchoWriteIndex i32.</SonarSynthesisState>
    <PendingImpactEchoProbe size="16">0 Excitation f32, 4 ExpireAt f32, 8 Valid u8, 9-15 padding.</PendingImpactEchoProbe>
    <ThrusterSynthesisState size="136">0 Hum1Phase f64, 48 CavitationCarrierPhase f64, 72 PinkB0 f32, 128 VehicleCavitationHighPassOutput f32.</ThrusterSynthesisState>
  </StructLayout>
  <ZeroGC>Field reordering introduced no allocations, locks, callbacks, delegates, LINQ, strings, or runtime searches.</ZeroGC>
  <CompileGuard>Core no-restore build currently fails only on non-audio GlobalPhysicsStateManager missing WakeRequestSignal.</CompileGuard>
</SELF_AUDIT_L1>

## Decision 014 - Literal Pressure And Tension Mocks

Problem: The SHINOBU_29 XML mandate literally required `MockPressureSignal` and `MockTensionSignal`. The previous implementation provided an aggregate `MockHullStressSignal` with pressure/tension/depth/speed fields, which was functionally useful but not literal enough for the task contract.

Solution: Added 16-byte unmanaged `MockPressureSignal` and `MockTensionSignal` DTOs, both `StructLayout(Size=16)` with only 4-byte fields. Extended `MockHullStressSignalJob` with optional `NativeArray<MockPressureSignal>` and `NativeArray<MockTensionSignal>` outputs while preserving the existing aggregate output for compatibility.

Rejected Alternatives: Renaming the existing aggregate signal, creating global signal lanes, or coupling to Hull Integrity/Cable/Depth systems. Renaming would break current callers, global lanes would fragment the nervous system for a local mock, and direct coupling would violate the blind-dependency rule.

Scalability potential: Low/MX350 keeps the mocks completely out of runtime DSP unless scheduled for validation. High/Ultra can use the same deterministic scalar mocks to stress dense granular/FM tuning without loading WAV files.

Hardware Impact: No runtime cost in live audio unless the validation job is scheduled. Static layout proof: `MockPressureSignal size=16 PressureScalar@0 DepthScalar@4 VelocityScalar@8 Sequence@12`; `MockTensionSignal size=16 TensionScalar@0 StrainRateScalar@4 PressureCouplingScalar@8 Sequence@12`.

<SELF_AUDIT_MOCKS>
  <StructLayout>
    <MockHullStressSignal size="16">0 MockStress f32, 4 MockTension f32, 8 MockDepth f32, 12 MockSubmarineVelocity f32.</MockHullStressSignal>
    <MockPressureSignal size="16">0 PressureScalar f32, 4 DepthScalar f32, 8 VelocityScalar f32, 12 Sequence u32.</MockPressureSignal>
    <MockTensionSignal size="16">0 TensionScalar f32, 4 StrainRateScalar f32, 8 PressureCouplingScalar f32, 12 Sequence u32.</MockTensionSignal>
  </StructLayout>
  <ZeroGC>No managed allocation was added. The job writes only caller-owned NativeArray outputs and returns when no outputs are created.</ZeroGC>
  <Dependency>No new GlobalSignals, no direct Hull Integrity/Cable/Depth reference, no Contracts or asmdef change.</Dependency>
  <CompileGuard>`dotnet build Hecton8.Core.csproj --no-restore -v:minimal` currently exits 1 outside SHINOBU_29 at `GlobalPhysicsStateManager.WakeRequestSignal`.</CompileGuard>
</SELF_AUDIT_MOCKS>

## Decision 015 - H8Dump Blackbox Alias

Problem: The ultra polish mandate requires `.h8dump` emission on fatal state, while the existing SHINOBU_29 blackbox alias only wrote `Dump_PROCEDURAL_SYNTH.bin` plus legacy owner `.bin` files.

Solution: Added `Dump_PROCEDURAL_SYNTH.h8dump` to the same cold `DumpGranularTelemetryCold()` path before the compatibility `.bin` aliases. The source data remains the same fixed 300-entry `NativeArray<GranularAudioTelemetryEntry>` ring.

Rejected Alternatives: Renaming existing `.bin` dumps or adding hot-path logging. Renaming would break existing diagnostics consumers; hot-path text logging would violate DSP safety.

Scalability potential: No tier cost. The dump happens only after the existing invalid-state flag trips and is outside the audio producer loop.

Hardware Impact: 0 us steady-state. Cold fault path writes one additional file only after a dump request.

<SELF_AUDIT_BLACKBOX>
  <RingBuffer>300-entry granular telemetry ring remains fixed and DataVault-backed.</RingBuffer>
  <DumpTargets>Dump_PROCEDURAL_SYNTH.h8dump, Dump_PROCEDURAL_SYNTH.bin, and preserved legacy acoustic dump aliases.</DumpTargets>
  <HotPath>No file I/O, strings, or Directory/FileStream calls were added to the DSP producer loop.</HotPath>
</SELF_AUDIT_BLACKBOX>

## Decision 016 - Native DSP Descriptor Explicit Padding

Problem: The SHINOBU_29 audio surface still had one native DSP bridge descriptor whose ABI layout depended on implicit padding between a 4-byte magic and 8-byte `IntPtr` fields. The current mandate asks for L1-visible alignment proof, not "the runtime probably pads it."

Solution: Changed `NativeAudioKernelRingBufferDescriptor` to `LayoutKind.Explicit, Size = 56`, preserved the existing ABI offsets, set pointer alignment validation to 8 bytes, and made the descriptor gap and tail padding explicit: `DescriptorMagic@0`, pad `@4`, `Frames@8`, `SharedState@16`, `ReadIndex@24`, `WriteIndex@32`, `CapacityFrames@40`, `CapacityMask@44`, `SharedStateLengthInts@48`, tail pad `@52`.

Rejected Alternatives: Reordering the descriptor to put pointers before the magic would satisfy a generic field-order rule but would silently break the native plugin ABI. Leaving implicit padding would fail the forensic proof requirement.

Scalability potential: Low/ARM64 gets deterministic pointer alignment at the native audio handoff. Middle/High/Ultra keep the same 56-byte bridge payload while the saved risk budget stays reserved for denser procedural pressure-metal texture.

Hardware Impact: No profiler microseconds are claimed. Static proof: `AUDIO_RUNTIME_PACK_SCAN_PASS`, `NativeAudioKernelRingBufferDescriptor size=56`, pointer fields at 8-byte offsets, and no SHINOBU_29 audio runtime `Pack=1` hits.

<SELF_AUDIT_NATIVE_DESCRIPTOR>
  <StructLayout>NativeAudioKernelRingBufferDescriptor size=56: 0 DescriptorMagic u32, 4 descriptor pad u32, 8 Frames IntPtr, 16 SharedState IntPtr, 24 ReadIndex IntPtr, 32 WriteIndex IntPtr, 40 CapacityFrames i32, 44 CapacityMask i32, 48 SharedStateLengthInts i32, 52 tail pad u32.</StructLayout>
  <ZeroGC>No allocation, lock, callback, delegate, string, or runtime search was added. The descriptor remains a value payload for the existing native ring registration path.</ZeroGC>
  <Dependency>No Contracts/asmdef change and no sibling runtime dependency. ABI offsets were preserved.</Dependency>
  <CompileGuard>`dotnet build Hecton8.Core.csproj --no-restore -v:minimal` currently exits 1 outside SHINOBU_29 at `Assets/_Project/Scripts/Core/InputDispatcher.cs` syntax drift.</CompileGuard>
</SELF_AUDIT_NATIVE_DESCRIPTOR>

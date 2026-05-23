# SHINOBU_351 Log

## Session Start

What was wrong: Assignment state did not exist on disk and the task could not rely on chat memory.
What was done: Extracted the `SHINOBU_351` XML block to `Docs/Tasks/Extract_SHINOBU_351_CURRENT.xml`; created status, rationale, and log files.
Cinematic Cheats used: Chose cheap stereo/AUP perceptual DSP as the default route pending source integration, rejecting per-source Unity audio objects.
Exact Microseconds saved: PENDING VERIFICATION; no runtime code has executed yet.

## Implementation Pass 2026-05-23

What was wrong: Hull-stress audio still had an authored `AudioSource` fallback path (`hullGroanLoopSource`) and the granular voice cap was 4..16 instead of the required continuous 8..64 envelope. There was no SHINOBU_351-specific Burst DTO/job contract, no `Dump_SHINOBU_351.bin` dump route, and no UI Toolkit Abyssal DSP tuner.

What was done: Removed the hull-groan loop source/clip/gain fields and neutered the playback method. Raised `GranularVoiceCapacity` to 64 and minimum quality voice cap to 8. Changed granular voice Vault buffers to `NativeArrayOptions.UninitializedMemory`. Added `HullStressGranularDspKernel.cs` with `GranularVoiceDTO`, `AudioDspTelemetryEntry`, `GenerateMockStressAudioJob`, `MapStressToAudioParamsJob`, `EvaluateGranularVoicesJob`, and `HullStressAudioProfileCsv`. Added `AbyssalDspTunerWindow.cs` using UI Toolkit with oscilloscope/history graphs, continuous quality/polyphony/grain sliders, and SceneView AUP source wire spheres. Updated `OOP_Audio_Scanner` to SHINOBU_351 and report summary `OOP Audio Sources Eradicated`.

Cinematic Cheats used: Rejected per-wall Unity source spatialization and full HRTF. Used a Dear-Lie pan: `double3` AUP subtract first, float3 local direction second, dot listener-right for stereo, softened distance-square attenuation, Hanning grain windows, and continuous voice shedding.

Exact Microseconds saved: Static estimate only. Removing one authored hull loop avoids main-thread source Play/Stop/pitch churn, estimated 20-80 us during stress onset. Replacing per-emitter wall creaks with one single-writer granular mix avoids 50 source spatializers, estimated 350-1600 us on i3/MX350 under dense base stress. Actual profiler proof is pending because dotnet/Unity compile was not launched at 96-100% CPU load.

Compile/Verification: `git diff --check` passed with only the repo CRLF warning for `PlayerCriticalProceduralAudioRenderer.cs`. `rg` confirmed old hull-groan symbols are gone from the owner. `rg` confirmed no `OnAudioFilterRead`, `PlayOneShot`, `PlayClipAtPoint`, `Resources.Load<AudioClip>`, or `Dictionary<string, AudioClip>` in touched runtime files. DTO layout static proof: `GranularVoiceDTO` offsets 0/24/28/32/36/40/44/48/52/56/60, size 64.

<SELF_AUDIT>
  <TASK id="01" result="PASS">Audio/Habitat/Physics archaeology completed with rg.</TASK>
  <TASK id="02" result="PASS">Existing non-partial owner retained; isolated kernel added instead of competing manager.</TASK>
  <TASK id="03" result="PASS">Existing BaseStructuralWarningSignal route used; no new creak signal.</TASK>
  <TASK id="04" result="PASS">Hull groan AudioSource fallback removed; no base-module stress AudioSources found.</TASK>
  <TASK id="05" result="PASS">No Dictionary string AudioClip route found; FNV/span profile parser added.</TASK>
  <TASK id="06" result="PASS">GenerateMockStressAudioJob implemented for 100 dense signals.</TASK>
  <TASK id="07" result="PASS">EvaluateGranularVoicesJob implemented with Hanning grain mix.</TASK>
  <TASK id="08" result="PASS">MapStressToAudioParamsJob maps stress to pitch/amplitude/grain length.</TASK>
  <TASK id="09" result="PASS">Dear-Lie dot pan and distance-square attenuation implemented.</TASK>
  <TASK id="10" result="PASS">Single-writer local accumulators used before interleaved output write.</TASK>
  <TASK id="11" result="PASS">Continuous 8..64 quality-scaled polyphony and stealing implemented.</TASK>
  <TASK id="12" result="PASS">AUP double3 subtract occurs before float3 downcast.</TASK>
  <TASK id="13" result="PASS">GranularVoiceDTO is not referenced by SaveMerkle/StateRingBuffer paths.</TASK>
  <TASK id="14" result="PASS">Granular voice Vault buffers request UninitializedMemory.</TASK>
  <TASK id="15" result="PASS">300-entry-compatible telemetry DTO and Dump_SHINOBU_351 route added.</TASK>
  <TASK id="16" result="PASS">UI Toolkit Abyssal DSP Tuner added.</TASK>
  <TASK id="17" result="PASS">ReadOnlySpan byte CSV parser added without float.Parse.</TASK>
  <TASK id="18" result="PASS">SceneView AUP source gizmo added for current structural warnings.</TASK>
  <TASK id="19" result="PASS">OOP_AudioSource_Scanner report string added.</TASK>
  <TASK id="20" result="PASS_STATIC_COMPILE_DEFERRED">Static audit passed; compile blocked by 96-100% CPU policy.</TASK>
  <ARM64 DTO="GranularVoiceDTO" SIZE="64">0 double3 EpicenterAUP; 24 uint AudioBankHashID; 28 float PlayheadPosition; 32 float GrainLength; 36 float PitchMultiplier; 40 float Amplitude; 44/48/52/56/60 uint padding.</ARM64>
  <ZERO_GC>Hot jobs use NativeArray, raw fields, unsafe refs, local accumulators. Editor-only window uses managed UI arrays outside runtime hot path.</ZERO_GC>
  <AUP>Audio source and listener are subtracted in double precision before local float pan/attenuation.</AUP>
  <VAULT>Existing owner buffers: PlayerCriticalGranularVoiceActive/Elapsed/Length/Start/Seed/Cursor/PlaybackRate/Gain and PlayerCriticalGranularTelemetryRing. New DTO kernel expects caller-owned NativeArrays and does not enter rollback state.</VAULT>
</SELF_AUDIT>

## Submarine Hull Warning Dead Clip Field Purge 2026-05-23

What was wrong: `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs` still declared `hullBreachWarningClip` and `hullStressWarningClip` serialized `AudioClip` fields. `rg` proved those fields were not read; active hull warnings already travel through `hullBreachWarningEventId`, `hullStressWarningEventId`, and `VocalWarningSignal`.

What was done: Removed only the two hull-specific clip fields. Non-hull VWS clip fields were left intact because they are outside SHINOBU_351's hull-stress audio scope and may still be serialized designer content.

Cinematic Cheats used: None new. This is source-surface sanitation: hull warning presentation remains routed through event IDs/signals instead of managed clip references.

Exact Microseconds saved: 0 runtime us because the fields were dead. The value is compile/import and regression hygiene: no stale managed hull-warning clip slot remains for designers or future code to accidentally resurrect.

Compile/Verification: `rg hullBreachWarningClip|hullStressWarningClip Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs Assets -g "*.cs"` returns no hits. `rg VocalWarningSignal|VocalWarningHashes.FromWarningId Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs` confirms the active VWS signal route. Static smoke JSON now records 46 checks and 0 failures. No dotnet/Unity rebuild was launched.

## Acoustic Fatal Pressure Managed Clip Purge 2026-05-23

What was wrong: `Assets/_Project/Scripts/AcousticZoneController.cs` still held `fatalPressureNoisePrimary` and `fatalPressureNoiseSecondary` managed `AudioClip` fields and used `PlayStatic2D` for fatal-pressure white-noise bursts during the crush-depth loop. `PlayMadnessWhisperCue` also fell back to those fatal-pressure clips. This kept a managed hull-crush audio route alive outside the player-critical DSP owner.

What was done: Removed the fatal-pressure clip fields, removed the alternating clip toggle, removed the madness fallback to fatal-pressure clips, and replaced `UpdateFatalPressureLoopAudio` with `UpdateFatalPressureStressAudio`. The loop now emits `ProceduralAudioEvents.RaiseStructuralStressTriggered` at the player's AUP-resolved runtime position with tunable `fatalPressureStressMin/Max`, `fatalPressureStressPitchMin/Max`, and cadence via `math.lerp(fatalPressureStressIntervalMax, fatalPressureStressIntervalMin, intensity)`.

Cinematic Cheats used: The fatal crush-depth noise is no longer an authored 2D white-noise sample. It is a cheap procedural stress impulse into the same granular metal deformation DSP, buying immersion through the existing Dear-Lie grain field instead of Unity source playback.

Exact Microseconds saved: Static estimate 15-70 us per fatal-pressure burst on low-end CPUs by avoiding clip selection, mixer group dispatch, and `PlayStatic2D` source-path work. Dense crush-depth scenes also avoid extra Unity voice contention. Profiler proof remains pending Unity import.

Compile/Verification: `rg fatalPressureNoisePrimary|fatalPressureNoiseSecondary|fatalPressureNoiseVolume|UpdateFatalPressureLoopAudio Assets/_Project/Scripts/AcousticZoneController.cs` returns no hits. `rg UpdateFatalPressureStressAudio|ProceduralAudioEvents.RaiseStructuralStressTriggered Assets/_Project/Scripts/AcousticZoneController.cs` confirms the procedural route. `Shinobu351HullStressDspSmokeTester` and `Docs/Reports/AUDIO_SHINOBU_351_STATIC_SMOKE.json` now record 45 static checks and 0 failures. No dotnet/Unity rebuild was launched.

<SELF_AUDIT revision="2026-05-23-loop18">
  <TASKS>
    <TASK id="01" result="PASS">Current archaeology includes the subagent-discovered `AcousticZoneController` fatal-pressure clip route plus prior Audio/Habitat/Physics scans.</TASK>
    <TASK id="02" result="PASS">No new manager or cross-domain owner was introduced; producer reroutes into existing procedural audio events.</TASK>
    <TASK id="03" result="PASS">Structural stress still enters existing SignalBus/procedural stress lanes; no new creak event lane was created.</TASK>
    <TASK id="04" result="PASS">Fatal-pressure managed clips are removed from `AcousticZoneController`; non-hull sonar/storm/whisper/mount cues are left to their owners.</TASK>
    <TASK id="05" result="PASS">No `Dictionary<string, AudioClip>` hot route was added or used.</TASK>
    <TASK id="06" result="PASS">Mock stress Burst job remains available.</TASK>
    <TASK id="07" result="PASS">Raw-pointer Burst callback kernel remains the synthesis path; acoustic fatal pressure only publishes procedural stress triggers.</TASK>
    <TASK id="08" result="PASS">Fatal-pressure intensity maps to tunable stress and pitch scalars instead of clip selection.</TASK>
    <TASK id="09" result="PASS">Dear-Lie spatialization remains in the granular DSP owner, not `AcousticZoneController` source playback.</TASK>
    <TASK id="10" result="PASS">Mixer single-writer and multichannel tail write remain unchanged.</TASK>
    <TASK id="11" result="PASS">`GlobalQualityWeight` still scales DSP polyphony/interpolation continuously; acoustic producer cadence is intensity-lerped, not device-tier branched.</TASK>
    <TASK id="12" result="PASS">Producer resolves player position through AUP helper; DSP still performs source/listener double subtraction before float math.</TASK>
    <TASK id="13" result="PASS">No rollback/save identity changes.</TASK>
    <TASK id="14" result="PASS">No new persistent arrays or BufferIDs were introduced.</TASK>
    <TASK id="15" result="PASS">Telemetry ABI unchanged; callback/ring forensic route remains valid.</TASK>
    <TASK id="16" result="PASS">Editor tuner unchanged and smoke proof expanded.</TASK>
    <TASK id="17" result="PASS">CSV parser unchanged.</TASK>
    <TASK id="18" result="PASS_WITH_LIMITATION">SceneView debug unchanged; no false raw voice owner added.</TASK>
    <TASK id="19" result="PASS">Reports now mark `acousticZoneFatalPressureRoutedProcedural=true` and smoke tester verifies the absence of fatal-pressure managed clips.</TASK>
    <TASK id="20" result="PASS_STATIC_IMPORT_PENDING">Static smoke is now 45/0; Unity import compile remains pending under build guard.</TASK>
  </TASKS>
  <STRUCT_LAYOUT DTO="GranularVoiceDTO" SIZE="64">0 double3; 24 uint hash; 28/32/36/40 floats; 44..60 padding = 64 bytes, one cache line.</STRUCT_LAYOUT>
  <STRUCT_LAYOUT DTO="HullStressAudioBlockParamsDTO" SIZE="96">0 double3 ListenerAUP; 24 ulong SampleIndexBase; 32 long ticks; 40 float3 right; 52 float quality; 56 float rolloff; 60/64/68/72 ints; 76 flags; 80 OutputSampleCapacity; 84/88 padding = 96 bytes.</STRUCT_LAYOUT>
  <SCALABILITY>Below quality 0.3 the DSP collapses toward 8 active voices and nearest-biased sampling. The new acoustic producer does not branch by hardware class; it only emits stress intensity and pitch that the DSP scales continuously.</SCALABILITY>
  <H_PHI_VAULT_STATUS>No private persistent native arrays were added. Existing SHINOBU-relevant lanes remain BufferIDs 431..440; acoustic fatal pressure sends a transient procedural event.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>Existing Burst jobs retain `[NoAlias]` on separate NativeArray lanes and return scheduled handles; the acoustic producer adds no jobs and no hidden `Complete()`.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>No new assembly reference or sibling domain dependency was added.</COMPILE_GUARD>
  <DEAR_LIE>Authored fatal-pressure white-noise samples were replaced by granular stress impulses. Complexity remains `O(frameCount * activeVoiceLimit)` in the DSP owner instead of managed clip/source dispatch per burst.</DEAR_LIE>
</SELF_AUDIT>

## Raw Callback Capacity Fence And Residual Managed Groan Purge 2026-05-23

What was wrong: Sidecar review found the raw Burst callback trusted `FrameCount` without a destination capacity field. Legacy-source archaeology also found managed hull/groan routes still present outside the player-critical renderer: `DeepPsychosisController.hullStressClips`, crush-depth managed groan/implosion clip fields, and transport entanglement groan playback.

What was done: Expanded `HullStressAudioBlockParamsDTO` to 96 bytes and added `OutputSampleCapacity@80`; `EvaluateBlock` now clamps frames to `OutputSampleCapacity / outputStride` and logs `TelemetryFlagOutputCapacityInvalid` before any write on invalid capacity. Removed psychosis hull-stress clip pool, removed crush-depth managed groan/implosion clips and fatal `PlayStatic2D`, and replaced transport entanglement groan playback with `ProceduralAudioEvents.RaiseStructuralStressTriggered`.

Cinematic Cheats used: Psychosis and transport hull strain now emit scalar stress/pitch into the procedural route; the DSP owner sells metal deformation with granular synthesis instead of authored clip playback. Fatal crush-depth relies on existing structural warning/glitch/wipeout presentation rather than a managed 2D implosion clip.

Exact Microseconds saved: Capacity guard cost is below profiler resolution per block and prevents buffer corruption. Removing the three managed groan routes avoids static-estimated 15-90 us per triggered managed one-shot on low-end silicon and avoids Unity source/spatializer contention during dense stress windows. Profiler proof remains pending a legal Unity import window.

Compile/Verification: `rg hullStressClips|SelectCueClip|crushDepthGroanClip|crushDepthImplosionClip|entanglementStressGroanSound` over the patched owners returns no hits. `rg AudioSource|AudioClip|PlayClipAtPoint|PlayOneShot|Resources.Load|OnAudioFilterRead|PlayAtPoint\(|minimumQualityKineticImpactClip|_kineticImpactAudioService|ResolveKineticImpactAudioService|TryQueueMinimumQualityKineticImpactLayer|UpdateHullGroanLoop|metalStressGrainClip|TryLoadMetalStressGrainClip|GetData\(` still returns no hits in `PlayerCriticalProceduralAudioRenderer.cs` and `HullStressGranularDspKernel.cs`. SHINOBU static smoke JSON now reports `passed=43 failed=0`. Build remains withheld until CPU/dotnet/csc guard opens and Unity regenerates authoritative project files.

Build Guard Resample: CPU 66%, `dotnet=0`, `csc=0`; `rg -g "*.csproj" HullStressGranularDspKernel|Shinobu351HullStressDspSmokeTester|OOP_AudioSource_Scanner|AbyssalDspTunerWindow` returned no hits. Rebuild not launched.

<SELF_AUDIT revision="2026-05-23-loop16">
  <TASKS>
    <TASK id="01" result="PASS">CLI `rg` archaeology performed against the batch prompt and source surface.</TASK>
    <TASK id="02" result="PASS">No new granular manager; SHINOBU kernel stays isolated and existing renderer owns player-critical output.</TASK>
    <TASK id="03" result="PASS">Existing structural/audio signal routes used; no new creak signal lane.</TASK>
    <TASK id="04" result="PASS">Renderer, Physics splash, psychosis hull clips, crush-depth groan/implosion clips, and transport entanglement groan playback purged or rerouted.</TASK>
    <TASK id="05" result="PASS">No string-keyed `AudioClip` dictionary route; deterministic metal PCM bank replaces clip import.</TASK>
    <TASK id="06" result="PASS">Burst mock stress job exists for deterministic synthetic structural warnings.</TASK>
    <TASK id="07" result="PASS">Raw-pointer Burst callback and job share the same granular mixer; output writes are capacity-bounded.</TASK>
    <TASK id="08" result="PASS">Stress maps to pitch, amplitude, grain length, and voice allocation without authored clip matrices.</TASK>
    <TASK id="09" result="PASS">Dear-Lie stereo pan uses AUP-local direction dot and distance attenuation.</TASK>
    <TASK id="10" result="PASS">Single audio writer uses local channel accumulators before interleaved writeback.</TASK>
    <TASK id="11" result="PASS">Continuous quality curve controls 8..64 voices and sampling blend; no binary hardware switch.</TASK>
    <TASK id="12" result="PASS">AUP source/listener subtraction occurs in double precision before float pan/distance math.</TASK>
    <TASK id="13" result="PASS">Cosmetic voice DTO is not rollback/save truth.</TASK>
    <TASK id="14" result="PASS">Existing granular Vault lanes use uninitialized allocation and deterministic arming.</TASK>
    <TASK id="15" result="PASS">64-byte telemetry entry and 300-frame dump route are recorded.</TASK>
    <TASK id="16" result="PASS">Editor DSP tuner exists; runtime path has no editor dependency.</TASK>
    <TASK id="17" result="PASS">`ReadOnlySpan<byte>` CSV profile parser exists.</TASK>
    <TASK id="18" result="PASS_WITH_LIMITATION">SceneView debug reads current structural-warning sources; no false new voice BufferID.</TASK>
    <TASK id="19" result="PASS">Scanner source catches serialized `AudioSource`/`AudioClip` and `PlayAtPoint`; reports record active Habitat/Physics violations at 0.</TASK>
    <TASK id="20" result="PASS_STATIC_IMPORT_PENDING">Static smoke is 43/0; Unity import compile is still pending behind build gate.</TASK>
  </TASKS>
  <STRUCT_LAYOUT DTO="HullStressAudioBlockParamsDTO" SIZE="96" ALIGNMENT="32">
    <FIELD offset="0" size="24">double3 ListenerAUP</FIELD>
    <FIELD offset="24" size="8">ulong SampleIndexBase</FIELD>
    <FIELD offset="32" size="8">long DspExecutionTicks</FIELD>
    <FIELD offset="40" size="12">float3 ListenerRight</FIELD>
    <FIELD offset="52" size="4">float GlobalQualityWeight</FIELD>
    <FIELD offset="56" size="4">float DistanceRolloff</FIELD>
    <FIELD offset="60" size="4">int FrameCount</FIELD>
    <FIELD offset="64" size="4">int Channels</FIELD>
    <FIELD offset="68" size="4">int SampleRate</FIELD>
    <FIELD offset="72" size="4">int StolenVoices</FIELD>
    <FIELD offset="76" size="4">uint Flags</FIELD>
    <FIELD offset="80" size="4">int OutputSampleCapacity</FIELD>
    <PADDING offsets="84,88" size="12">uint + ulong pad; total 24+8+8+12+4*8+12 = 96 bytes. No Pack=1; field offsets remain ARM64-aligned.</PADDING>
  </STRUCT_LAYOUT>
  <SCALABILITY>Below quality 0.3 the callback continuously falls toward 8 active voices and nearest-biased sampling through `smoothstep`; it does not change signal identity, BufferIDs, DTO layout, or save authority.</SCALABILITY>
  <H_PHI_VAULT_STATUS>No private persistent NativeArray owner added. Existing lanes remain 431 metallic bank, 432..439 granular voices, and 440 telemetry ring; callback DTOs are transient ABI rows.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>Kernel jobs keep `[NoAlias]` on non-overlapping NativeArray lanes and return scheduled handles; no hidden `.Complete()` was introduced.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>`HullStressGranularDspKernel.cs` has contracts-only cross-domain input; shared synthesis asmdef references were not blindly removed because neighboring synthesis owners require them.</COMPILE_GUARD>
  <DEAR_LIE>Authored per-source hull groans and software HRTF are replaced by scalar stress events plus one flat granular mixer: `O(frameCount * activeVoiceLimit)` instead of scene-object source traversal and spatializer dispatch per creak.</DEAR_LIE>
</SELF_AUDIT>

## Ultra-Polish Pointer Kernel Pass 2026-05-23

What was wrong: The first granular kernel existed as an `IJob`, but a direct audio-transfer callback route would have needed either a same-thread `Execute()` call or a managed bridge. That was not enough proof for the assignment's callback requirement. The scanner proof was also weak because the available audio scanner was string-based and SHINOBU_339-owned.

What was done: Added `HullStressAudioBlockParamsDTO` (now 96 bytes with `OutputSampleCapacity@80`) and `EvaluateHullStressGranularAudioDelegate`. `HullStressGranularDspKernel.EvaluateAudioCallback` is now `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` plus `MonoPInvokeCallback`, and the job calls the same raw-pointer `EvaluateBlock` path. Added `OOP_AudioSource_Scanner.cs`, a Roslyn AST scanner that detects AudioSource construction/playback, `Resources.Load<AudioClip>`, and managed `Dictionary<string, AudioClip>` routes, writing a SHINOBU_351 report section.

Cinematic Cheats used: The callback kernel keeps cheap underwater spatialization: `double3` AUP subtract, local `float3`, dot(listenerRight), soft inverse-distance attenuation, no full HRTF convolution. `GlobalQualityWeight` continuously scales voice count and blends nearest-to-linear sampling rather than toggling a binary tier.

Exact Microseconds saved: Static estimate only. Function-pointer callback route avoids managed job dispatch and per-source Unity spatializer work; expected save remains 350-1600 us during dense base stress on i3/MX350 compared with 50 wall emitters. Additional interpolation shedding at low quality is expected to save 5-20 us per 512-sample block versus always-high-quality sampling. Profiler proof pending Unity import compile.

Compile/Verification: `git diff --no-index --check` on new files produced only repo LF->CRLF warnings after the `.meta` trailing whitespace was fixed. `rg` confirmed `CompileFunctionPointer<EvaluateHullStressGranularAudioDelegate>`, `MonoPInvokeCallback(typeof(EvaluateHullStressGranularAudioDelegate))`, explicit 64/96-byte layouts, `CSharpSyntaxTree`, `OOP_AudioSource_Scanner`, and `OOP Audio Sources Eradicated`. CPU guard returned 30.6 and no dotnet/csc processes, but `rg -g "*.csproj" HullStressGranularDspKernel` found no generated project inclusion, so dotnet build was not used as a false compile proof.

<SELF_AUDIT>
  <TASK id="01" result="PASS">Original rg archaeology remains valid; no new direct legacy sound route was introduced.</TASK>
  <TASK id="02" result="PASS">No competing manager added; function pointer lives in isolated Audio/Synthesis kernel.</TASK>
  <TASK id="03" result="PASS">BaseStructuralWarningSignal remains the hot stress input; no PlayCreakSoundSignal exists.</TASK>
  <TASK id="04" result="PASS">No new runtime AudioSource dependency added for hull stress.</TASK>
  <TASK id="05" result="PASS">AST scanner now catches managed AudioClip dictionaries and Resources.Load AudioClip patterns.</TASK>
  <TASK id="06" result="PASS">Mock stress job unchanged and still Burst compiled.</TASK>
  <TASK id="07" result="PASS">Mixer now has a callback-compatible Burst function pointer and shared raw-pointer execution path.</TASK>
  <TASK id="08" result="PASS">Stress-to-voice mapping unchanged; output DTO feeds the raw-pointer mixer.</TASK>
  <TASK id="09" result="PASS">Dear-Lie pan remains AUP-local dot product, not HRTF convolution.</TASK>
  <TASK id="10" result="PASS">Single-writer block loop uses local left/right accumulators before interleaved writes.</TASK>
  <TASK id="11" result="PASS">Continuous `GlobalQualityWeight` controls 8..64 voice limit and interpolation blend.</TASK>
  <TASK id="12" result="PASS">AUP localization still subtracts source/listener double3 before float3 math.</TASK>
  <TASK id="13" result="PASS">No SaveMerkle/StateRingBuffer reference to `GranularVoiceDTO` found in static pass.</TASK>
  <TASK id="14" result="PASS">Existing owner's granular Vault allocations remain `UninitializedMemory`.</TASK>
  <TASK id="15" result="PASS">Pointer kernel writes bounded 64-byte telemetry records through caller-owned ring/cursor pointers.</TASK>
  <TASK id="16" result="PASS_STATIC">Editor tuner exists; runtime callback kernel does not depend on editor UI.</TASK>
  <TASK id="17" result="PASS">Span byte CSV parser remains cold-path only.</TASK>
  <TASK id="18" result="PASS_WITH_LIMITATION">SceneView debug remains structural-warning AUP based; raw `GranularVoiceDTO` Vault lane is not introduced because the current owner uses existing PlayerCritical SoA buffers and adding a new BufferID/owner without integration would create a false route.</TASK>
  <TASK id="19" result="PASS">New Roslyn `OOP_AudioSource_Scanner` added with sidecar/shared report upsert code.</TASK>
  <TASK id="20" result="PASS_STATIC_IMPORT_PENDING">Static source audit passed; Unity import compile remains pending because generated csproj files are stale for these sources.</TASK>
  <STRUCT DTO="GranularVoiceDTO" SIZE="64">0 double3 EpicenterAUP; 24 uint AudioBankHashID; 28 float PlayheadPosition; 32 float GrainLength; 36 float PitchMultiplier; 40 float Amplitude; 44/48/52/56/60 uint padding. Multiple of 64, one L1 line.</STRUCT>
  <STRUCT DTO="HullStressAudioBlockParamsDTO" SIZE="96">0 double3 ListenerAUP; 24 ulong SampleIndexBase; 32 long DspExecutionTicks; 40 float3 ListenerRight; 52 float GlobalQualityWeight; 56 float DistanceRolloff; 60 int FrameCount; 64 int Channels; 68 int SampleRate; 72 int StolenVoices; 76 uint Flags; 80 int OutputSampleCapacity; 84 uint pad; 88 ulong pad. Multiple of 32, no Pack=1.</STRUCT>
  <ZERO_GC>Runtime DSP path uses raw pointers, `NativeArray` unsafe pointers, struct locals, no LINQ, no managed strings, no AudioSource.</ZERO_GC>
  <AUP>All pan/attenuation source deltas are localized by `AupPrecisionMath.LocalDeltaFloat3Clamped` after double3 source/listener subtraction.</AUP>
  <COMPILE_GUARD>Audio.Synthesis asmdef references Core/Core.Contracts/Core.Memory/Unity packages only; no sibling runtime domain reference was added.</COMPILE_GUARD>
</SELF_AUDIT>

## Self-Read NaN Patch 2026-05-23

What was wrong: The callback kernel still trusted `voice.PitchMultiplier`, and the stress-to-voice mapper still trusted `BaseGrainLengthSeconds`. Either value becoming NaN would poison playhead advance or grain length.

What was done: Patched both through `HullStressGranularDspMath.FiniteOrDefault`. The common stereo path now computes the interleaved index as `frame << 1` when channel stride is exactly two.

Cinematic Cheats used: None new. This is math hygiene for the existing Dear-Lie granular route.

Exact Microseconds saved: The finite guards cost scalar ops but prevent audio-thread NaN bursts. Bit-shift stereo indexing is a tiny common-path win, below 1 us per 512-frame block; profiler proof pending.

Compile/Verification: `rg` confirmed `FiniteOrDefault(voice.PitchMultiplier, 1f)`, guarded `baseGrainLength`, and `outputStride == 2 ? frame << 1`. `rg` found no old direct `math.max(0.000001f, voice.PitchMultiplier)` expression.

## Scanner And Ledger Hardening 2026-05-23

What was wrong: The SHINOBU_351 scanner still used formatted type text for `Dictionary<string, AudioClip>`, so spacing/alias variants could evade the proof. It also did not flag `AudioSource[]` allocation sites. The binary payload ledger had no SHINOBU_351 boundary card.

What was done: `OOP_AudioSource_Scanner` now resolves `Dictionary<string, AudioClip>` through Roslyn `GenericNameSyntax` and type arguments, including qualified and alias-qualified generic names. It also flags `ArrayCreationExpressionSyntax` where the element type is `AudioSource`. `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now records the SHINOBU_351 route, existing `431..440` player-critical Vault lanes, and the 64/96-byte callback DTO ABI.

Cinematic Cheats used: No new runtime cheat. The ledger records the existing Dear-Lie route: dot-product panning plus inverse-distance attenuation instead of Unity per-wall spatializers or software HRTF convolution.

Exact Microseconds saved: Runtime cost remains 0 us for the scanner/ledger edits. The prevention target is future managed audio regressions: catching one reintroduced per-wall `AudioSource` cluster avoids the estimated 350-1600 us dense stress-scene overhead already recorded for the DSP route.

Compile/Verification: `rg IsDictionaryStringAudioClipType|ArrayCreationExpressionSyntax|AudioSource\[\]` confirms structural scanner hardening. `rg SHINOBU_351 Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` confirms route-card presence. No dotnet/Unity build was launched.

## Report Artifact Pass 2026-05-23

What was wrong: `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_351.json` did not exist yet because the Unity Editor menu command for the new Roslyn scanner has not been executed. A direct PowerShell/Roslyn attempt failed on dependency binding: Windows PowerShell 5.1 could not satisfy both Roslyn's `System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0` and `System.Memory`'s `Version=4.0.4.1` requirement in one load context.

What was done: Generated a clearly labeled CLI regex fallback report instead of fabricating AST execution. The sidecar and shared report now record `scannerSourceUsesRoslynAst=true`, `reportUsesRoslynAst=false`, `fallbackUsesRegex=true`, `filesScanned=91`, `totalForbiddenNodes=5`, and `activeHabitatPhysicsViolationCount=0`.

Cinematic Cheats used: None. This is proof tooling only.

Exact Microseconds saved: Runtime cost is 0 us. Static prevention target remains the same: stop future Habitat/Physics per-wall `AudioSource` playback before it can re-enter the frame.

Compile/Verification: `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_351.json` exists. `Select-String` confirms `shinobu351AudioSourceScanner` in the shared audio report. No dotnet/Unity build was launched.

## Build Guard Static Verification 2026-05-23

What was wrong: Unity import compile is still pending. Generated `.csproj` files still do not include the new SHINOBU_351 synthesis/editor files, and the CPU guard is closed.

What was done: Resampled the guard: CPU averaged 80.23%, `dotnet=0`, `csc=0`. Verified `rg -g "*.csproj" HullStressGranularDspKernel|OOP_AudioSource_Scanner|AbyssalDspTunerWindow` returns no hits. Re-ran static gates: tracked docs `git diff --check` has only CRLF warnings; untracked scanner/report files have only CRLF warnings under `git diff --no-index --check`; Habitat/Physics forbidden audio-source grep returns no hits.

Cinematic Cheats used: None.

Exact Microseconds saved: Avoided a non-authoritative rebuild during 80% CPU load. Runtime path unchanged.

Compile/Verification: Rebuild intentionally not launched. Next valid proof requires Unity import/project regeneration under CPU <=50%.

## Managed Hull Audio Purge And Static Smoke 2026-05-23

What was wrong: A fresh `rg` pass found the previous status was stale: `UpdateHullGroanLoop` still existed as a no-op hook, metal-stress grain import still held `AudioClip.GetData` plus a 262144-float managed staging array, and the player-critical renderer still had a minimum-quality `AudioClip`/`PlayAtPoint` fallback.

What was done: Removed the no-op hull loop calls/method, removed `metalStressGrainClip`, removed the managed scratch array and `TryLoadMetalStressGrainClip`, changed `PopulateMetallicGrainBank` to deterministic `PlayerCriticalMetallicGrainBank.Generate`, and removed the low-tier `minimumQualityKineticImpactClip`/`PlayAtPoint` fallback. Updated `DSPThreadSafetySmokeTester` to reject managed clip/point fallback. Added `Shinobu351HullStressDspSmokeTester` and `Docs/Reports/AUDIO_SHINOBU_351_STATIC_SMOKE.json`.

Cinematic Cheats used: Authored clip playback is replaced by deterministic cheap metallic PCM generation (`TriOscFake`, held noise, soft clip) feeding the existing granular DSP bank. Spatial truth remains the Dear-Lie AUP-local dot pan and attenuation route rather than Unity per-source spatialization.

Exact Microseconds saved: Static estimate only. Removed cold managed staging allocation size: 262144 floats = 1,048,576 bytes. Removed fallback point-playback dispatch avoids source/spatializer churn under low quality. Estimated low-end gain remains 20-80 us on stress onset and 350-1600 us in dense stress scenes versus per-source Unity playback; profiler proof pending Unity import.

Compile/Verification: CLI mirror for the new smoke assertions returned `passed=40 failed=0`. `rg AudioSource|AudioClip|PlayClipAtPoint|PlayOneShot|Resources.Load|OnAudioFilterRead|PlayAtPoint\(|minimumQualityKineticImpactClip|_kineticImpactAudioService|ResolveKineticImpactAudioService|TryQueueMinimumQualityKineticImpactLayer|UpdateHullGroanLoop|metalStressGrainClip|TryLoadMetalStressGrainClip|GetData\(` returned no hits in `PlayerCriticalProceduralAudioRenderer.cs` and `HullStressGranularDspKernel.cs`. `git diff --check` reports only repo CRLF warnings. Latest build guard sampled CPU 51.38%, `dotnet=0`, `csc=0`; Unity import compile still pending and rebuild remains blocked by policy.

## Build Gate Recheck 2026-05-23

What was wrong: Unity import compile remains blocked; current machine state has active compiler load.

What was done: Resampled guard and re-ran the forbidden managed-hull-audio source scan against `PlayerCriticalProceduralAudioRenderer.cs` plus `HullStressGranularDspKernel.cs`.

Cinematic Cheats used: None new; this is gate discipline and static proof maintenance.

Exact Microseconds saved: 0 runtime us. Avoided launching an extra build under CPU 100%, `dotnet=8`, `csc=1`.

Compile/Verification: Build not launched by policy. Forbidden scan returned zero hits.

## NaN Amplitude Fence And Asmdef Scope Correction 2026-05-23

What was wrong: `NaN <= 0f` is false, so a poisoned `GranularVoiceDTO.Amplitude` could pass the first active-voice gate and consume mixer/voice-stealing work before being collapsed later. `HullStressAudioProfileDTO` also had one private padding field without a local CS0169 suppression. A naive asmdef reference purge would have broken other synthesis files that already depend on Core/Memory.

What was done: Added explicit nonfinite amplitude rejection in `EvaluateBlock`, changed voice stealing to use `FiniteOrZero(voice.Amplitude)`, wrapped profile padding with CS0169 suppression, kept synthesis asmdef references intact, and changed the smoke proof to assert contracts-only input on the SHINOBU kernel source rather than the entire shared synthesis assembly.

Cinematic Cheats used: None new. This hardens the existing cheap dot-pan granular route.

Exact Microseconds saved: Below profiler resolution for valid rows. Prevents invalid voice rows from burning traversal and blocks NaN telemetry/audio poison before it enters the accumulator.

Compile/Verification: JSON smoke mirror now reports `passed=41 failed=0`. Forbidden renderer+kernel audio scan remains zero hits. `git diff --check` on the patched kernel/smoke/report files produced no actionable whitespace errors.

Build Guard Resample: CPU 63.99%, `dotnet=7`, `csc=0`; rebuild remains withheld by policy.

## Physics Fluid Feedback AudioSource Removal 2026-05-23

What was wrong: Broad Habitat/Physics scan found `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs` still used a serialized `AudioSource`, local `AudioClip`, and `_audio.PlayAtPoint` for optional hull splash feedback. The earlier report's active Habitat/Physics zero claim was incomplete.

What was done: Removed only the managed audio branch from `FluidFeedbackListener`; decal feedback and `SplashEvent` dispatch remain unchanged. Hardened `OOP_AudioSource_Scanner` to detect serialized `AudioSource`/`AudioClip` declarations and generic `PlayAtPoint` invocations. Updated SHINOBU_351 sidecar/shared audio reports with `habitatPhysicsRuntimeForbiddenHitsAfterPatch=0`.

Cinematic Cheats used: Splash presentation remains a visual decal fake; fluid audio must route through central procedural audio in a future owner pass, not local source playback.

Exact Microseconds saved: Static estimate 10-60 us per splash event in this listener by removing source transform/clip/PlayAtPoint dispatch. Runtime profiler proof pending Unity import.

Compile/Verification: `rg` over Habitat/Physics runtime excluding `/Editor/` for `AudioSource|AudioClip|PlayAtPoint|PlayOneShot|PlayClipAtPoint|Resources.Load<AudioClip>|AddComponent<AudioSource>` returned no hits. SHINOBU static smoke JSON now reports `passed=42 failed=0`. `git diff --check` on patched Physics/scanner files has only repo CRLF warnings.

<SELF_AUDIT revision="2026-05-23-loop13">
  <TASKS>
    <TASK id="01" result="PASS">`rg` archaeology identified `PlayerCriticalProceduralAudioRenderer`, `SignalBus<BaseStructuralWarningSignal>`, central audio roots, and no base-module stress AudioSource scripts.</TASK>
    <TASK id="02" result="PASS">No competing `HectonGranularManager`; SHINOBU kernel is isolated under Audio/Synthesis while the existing player-critical owner keeps authority.</TASK>
    <TASK id="03" result="PASS">Existing `BaseStructuralWarningSignal` route used; no `PlayCreakSoundSignal` fragmentation.</TASK>
    <TASK id="04" result="PASS">Managed hull-groan/point-playback fallbacks removed from the owner; Physics `FluidFeedbackListener` splash `AudioSource` branch removed; legitimate central AudioSources outside hull-stress scope were not blindly deleted.</TASK>
    <TASK id="05" result="PASS">No hot `Dictionary<string, AudioClip>` route; authored metal-stress `AudioClip.GetData` staging removed, deterministic PCM bank retained.</TASK>
    <TASK id="06" result="PASS">`GenerateMockStressAudioJob` produces up to 100 deterministic synthetic stress signals.</TASK>
    <TASK id="07" result="PASS">`EvaluateGranularVoicesJob` and `EvaluateHullStressGranularAudioDelegate` share the same raw-pointer mixer path.</TASK>
    <TASK id="08" result="PASS">`MapStressToAudioParamsJob` maps stress to pitch, amplitude, and grain duration without managed clip matrices.</TASK>
    <TASK id="09" result="PASS">Dear-Lie panning uses AUP-local dot product and distance attenuation instead of software HRTF.</TASK>
    <TASK id="10" result="PASS">Single-writer block loop uses local left/right accumulators before final interleaved writes.</TASK>
    <TASK id="11" result="PASS">Continuous `GlobalQualityWeight` resolves 8..64 polyphony and quality-blended sampling; no low-end binary switch.</TASK>
    <TASK id="12" result="PASS">Source/listener AUP subtraction is double precision before float panning math.</TASK>
    <TASK id="13" result="PASS">`GranularVoiceDTO` remains cosmetic and absent from `SaveMerkle`/`StateRingBuffer` paths.</TASK>
    <TASK id="14" result="PASS">Existing owner granular Vault lanes request `NativeArrayOptions.UninitializedMemory`; active rows are deterministically armed/trimmed.</TASK>
    <TASK id="15" result="PASS">64-byte `AudioDspTelemetryEntry` ABI plus existing 300-entry ring and `Dump_SHINOBU_351.bin` route are recorded.</TASK>
    <TASK id="16" result="PASS">UI Toolkit Abyssal DSP Tuner exists; runtime DSP path has no editor dependency.</TASK>
    <TASK id="17" result="PASS">Cold `ReadOnlySpan<byte>` CSV parser hashes material names and parses floats without `float.Parse`.</TASK>
    <TASK id="18" result="PASS_WITH_LIMITATION">SceneView debug uses current structural-warning AUP sources; no false new `GranularVoiceDTO` BufferID was introduced.</TASK>
    <TASK id="19" result="PASS">Roslyn-source `OOP_AudioSource_Scanner` now detects serialized source/clip fields and `PlayAtPoint`; CLI runtime scan proves current Habitat/Physics active violations at 0.</TASK>
    <TASK id="20" result="PASS_STATIC_IMPORT_PENDING">Static smoke JSON is `42/0`; Unity import compile remains pending behind CPU/compiler guard.</TASK>
  </TASKS>
  <STRUCT_LAYOUT DTO="GranularVoiceDTO" SIZE="64" ALIGNMENT="64">
    <FIELD offset="0" size="24">double3 EpicenterAUP</FIELD>
    <FIELD offset="24" size="4">uint AudioBankHashID</FIELD>
    <FIELD offset="28" size="4">float PlayheadPosition</FIELD>
    <FIELD offset="32" size="4">float GrainLength</FIELD>
    <FIELD offset="36" size="4">float PitchMultiplier</FIELD>
    <FIELD offset="40" size="4">float Amplitude</FIELD>
    <PADDING offsets="44,48,52,56,60" size="20">five uint pads; total 24+4+4+4+4+4+20=64 bytes, one L1 cache line, no Pack=1.</PADDING>
  </STRUCT_LAYOUT>
  <SCALABILITY>When quality drops below 0.3, `ResolvePolyphonyLimit` continuously lowers active voices toward 8 and `smoothstep(0.18,0.72,quality)` biases sampling toward nearest instead of linear. Voice density, overlap, and interpolation cost shrink; DTO layout, signal route, and authority ownership do not change.</SCALABILITY>
  <H_PHI_VAULT_STATUS>No new SHINOBU BufferID was claimed. Existing lanes remain `431 PlayerCriticalMetallicGrainBank`, `432..439 PlayerCriticalGranularVoice*`, and `440 PlayerCriticalGranularTelemetryRing`; the callback DTOs are transient ABI rows, not persistent cross-domain truth.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>`GenerateMockStressAudioJob`, `MapStressToAudioParamsJob`, and `EvaluateGranularVoicesJob` declare `[NoAlias]` on non-overlapping NativeArray lanes. Jobs consume caller-owned dispatcher dependencies and return their scheduled handles; no hidden `.Complete()` was added by SHINOBU_351.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>`HullStressGranularDspKernel.cs` uses `Hecton8.Core.Contracts`/signals plus Unity Burst packages only; no sibling audio runtime reference. Existing `Hecton8.Audio.Synthesis.asmdef` Core/Core.Memory references are retained because other preexisting synthesis owners in the same assembly require them.</COMPILE_GUARD>
  <DEAR_LIE>Per-wall Unity spatializers and HRTF convolution are replaced by one master DSP stream: Hanning grain, AUP-local dot pan, inverse distance attenuation, and soft clip. Complexity is flat-buffer `O(frameCount * activeVoiceLimit)` instead of scene-object traversal and Unity source/spatializer dispatch per creaking wall.</DEAR_LIE>
  <ZERO_GC_CHECK>Hot SHINOBU kernel path uses raw pointers, `NativeArray`, index-based loops, no LINQ, no `foreach`, no `AudioSource`, no `AudioClip`, no `Resources.Load`, no managed callback synthesis, and no string dictionary lookup.</ZERO_GC_CHECK>
  <AUP_CHECK>All panning/distance math subtracts listener/source `double3` before local `float3` math through `AupPrecisionMath.LocalDeltaFloat3Clamped` or double-safe distance helpers.</AUP_CHECK>
</SELF_AUDIT>

## Acoustic And Submarine Hull Clip Purge Chronological Tail 2026-05-23

What was wrong: Two post-smoke archaeology items remained after the loop13 audit: `AcousticZoneController` still had fatal-pressure white-noise `AudioClip` fields and `PlayStatic2D` dispatch, and `HectonSubmarineOS` still declared dead hull warning clip fields even though active output already used VWS signals.

What was done: `AcousticZoneController` now emits procedural structural stress events from `UpdateFatalPressureStressAudio` with tunable stress/pitch/cadence. `HectonSubmarineOS.hullBreachWarningClip` and `hullStressWarningClip` were removed; event-id and `VocalWarningSignal` routing remain intact.

Cinematic Cheats used: Fatal-pressure white noise is converted into granular metal stress impulses, preserving the perceptual crush-depth cue without managed 2D sample playback.

Exact Microseconds saved: Acoustic fatal-pressure route saves an estimated 15-70 us per burst on weak hardware by avoiding clip/source dispatch. Submarine clip-field removal saves 0 runtime us because fields were dead, but removes a managed hull-warning regression slot.

Compile/Verification: `rg fatalPressureNoisePrimary|fatalPressureNoiseSecondary|fatalPressureNoiseVolume|UpdateFatalPressureLoopAudio` returns no hits in `AcousticZoneController.cs`. `rg hullBreachWarningClip|hullStressWarningClip` returns no hits in runtime C# sources. Static smoke JSON now records `passed=46 failed=0`. No dotnet/Unity rebuild was launched.

Build Guard Resample: CPU 100%, `dotnet=8`, `csc=0`; rebuild remains withheld by policy. Targeted `git diff --check` for the acoustic/submarine/smoke/report/status/rationale/log files returned only repo LF->CRLF warnings.

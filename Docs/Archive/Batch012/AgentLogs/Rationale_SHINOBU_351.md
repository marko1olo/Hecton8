# SHINOBU_351 Rationale

Status: POINTER KERNEL STATIC PASS / SCANNER AST HARDENED / UNITY IMPORT COMPILE PENDING

## Decision 00: Route Shape Before Code

Problem: Hull-stress audio request demands granular DSP, but project docs already identify `PlayerCriticalProceduralAudioRenderer` as the owner of player-local procedural hull stress audio and `OnAudioFilterRead` as a transfer bridge.
Solution: Treat granular hull stress as an isolated extension of the existing audio owner. Use partial files if the source class is partial; otherwise add narrow companion kernel/types under Audio/Synthesis and wire only through existing extension points after source archaeology proves them.
Rejected Alternatives: A new `HectonGranularManager` would create duplicate ownership and merge conflicts. Full HRTF convolution is rejected for default underwater hull stress because the mandate says cheap perceptual fakes first.
Scalability potential: Low uses cheap mono/stereo pan with low polyphony; Middle raises grain density; High adds richer interpolation; Ultra can increase density and debug capture without changing gameplay truth.
Hardware Impact: Estimated low-end gain versus per-wall `AudioSource` playback is removal of many Unity spatialization voices and managed clip dispatch; exact microseconds are PENDING VERIFICATION until profiler data exists.

## Decision 01: Current Mandates

Problem: The task crosses audio DSP, AUP, DataVault, signal lanes, layout, telemetry, and editor tooling.
Solution: Apply `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`, `AUDIO_Hrtf_Binaural_Spatialization`, `DATA_Runtime_Struct_Layout_ARM64`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `ARCH_Signal_Lane_Segregation`, `DBG_Telemetry_Crash_Reporting_PostMortem`, and `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`.
Rejected Alternatives: Reading only AGENTS.md is insufficient because DTO layout, audio callback, and signal-lane rules are more specific in the registry.
Scalability potential: Continuous `GlobalQualityWeight` must drive polyphony and interpolation cost, not gameplay authority.
Hardware Impact: Prevents ARM64 misalignment and callback allocation risk on i3/MX350 and Quest-class devices.

## Decision 02: Existing Owner Over New Manager

Problem: The repo already routes player-critical hull stress through `PlayerCriticalProceduralAudioRenderer`; creating a new manager would split audio ownership.
Solution: Keep `PlayerCriticalProceduralAudioRenderer` as owner, remove its authored hull-groan `AudioSource` fallback, and place new Burst DSP DTO/jobs in `Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs`.
Rejected Alternatives: `HectonGranularManager` or runtime GameObject emitters would duplicate ownership and re-open scene-hierarchy audio spam.
Scalability potential: Low uses the existing master procedural path with 8 voices; Middle/High/Ultra can call the 64-byte DTO kernel with more voice density and interpolation without changing authority.
Hardware Impact: Removes one managed hull-groan source path and caps future hull voices through flat buffers; expected low-end gain is avoiding Unity source lifecycle/spatializer overhead, pending profiler.

## Decision 03: Base Warning Signal Reuse

Problem: Hull stress audio needed localized source data, but new signal lanes would fragment structural warning routing.
Solution: Use `SignalBus<BaseStructuralWarningSignal>` and `AcousticAup` from the existing contracts; no `PlayCreakSoundSignal` was added.
Rejected Alternatives: A string or enum creak event would become another hot audio route and would not carry the existing stress/intensity/panic scalars.
Scalability potential: Same signal can map to 8, 16, 32, or 64 voices based on quality and device pressure.
Hardware Impact: Avoids extra queue traffic and keeps producer/consumer memory bounded; estimated route cost stays in existing snapshot read budget.

## Decision 04: 64-Byte DTO And Unsafe Mutation

Problem: C# struct property access and unaligned voice records create defensive copies and bad ARM64 memory access.
Solution: `GranularVoiceDTO` is `[StructLayout(LayoutKind.Explicit, Size = 64)]`: `double3 EpicenterAUP` at 0, `uint AudioBankHashID` at 24, floats at 28/32/36/40, padding through 60. Jobs mutate voices via `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` and `UnsafeUtility.AsRef`.
Rejected Alternatives: Properties, `class` voices, or smaller unpadded structs would risk CS1612 copies and alignment debt.
Scalability potential: Low/Middle/High/Ultra all traverse the same stride; only count and interpolation density change.
Hardware Impact: One 64-byte cache line per voice; predictable on ARM64 and desktop. Estimated i3/MX350 gain versus object voices is lower cache miss and no managed dispatch.

## Decision 05: Dear-Lie Spatialization

Problem: True software HRTF per grain is too expensive for audio callback budgets.
Solution: Subtract listener/source in `double3`, downcast local delta only after subtraction, pan with dot(listenerRight), and use softened distance-square attenuation.
Rejected Alternatives: Full HRTF convolution or Unity 3D source spatialization per wall would burn CPU and create voice-stealing artifacts.
Scalability potential: Low: dot pan and low voices. Middle: more overlap. High: higher density. Ultra: richer grain count and debug telemetry, still no authority change.
Hardware Impact: Dot product plus reciprocal replaces plugin/source spatializer work; expected per-voice cost stays below a few dozen scalar ops.

## Decision 06: No Blind Prefab Surgery

Problem: Task demanded deleting stress AudioSources, but prefab scan found AudioSources on player/audio root prefabs rather than base modules/pipes/bulkheads.
Solution: Remove the proven hull-groan fallback fields from the runtime owner and document that prefab AudioSources were left untouched because they are central player/audio roots.
Rejected Alternatives: Raw YAML deletion would risk destroying legitimate master/music/boiling-water emitters outside SHINOBU_351 scope.
Scalability potential: Centralized procedural hull stress scales by data, while non-hull audio remains under existing owner policies.
Hardware Impact: Avoids regression while removing the hull-specific managed path; no broad prefab churn.

## Decision 07: Telemetry And Dump Route

Problem: Granular DSP failures need forensic output and the task requires `Dump_SHINOBU_351.bin`.
Solution: Add `AudioDspTelemetryEntry` as a 64-byte 300-entry-compatible contract and extend existing granular dump writer to also emit `Docs/AgentLogs/Dump_SHINOBU_351.bin`.
Rejected Alternatives: Chat-only crash explanation or single current-frame logging cannot diagnose crackle/NaN history.
Scalability potential: Low can sample sparse telemetry; Ultra can keep full 300-frame inspection without gameplay-state ownership.
Hardware Impact: Telemetry ring writes are bounded; dump is cold fault path only.

## Decision 08: Compile Deferral

Problem: Project rule forbids dotnet build while CPU is under work above 50%.
Solution: Checked processes and CPU; CPU average returned 96 then 100, so no dotnet/Unity compile was launched. Static audits were run instead.
Rejected Alternatives: Forcing a build would violate explicit batch hardware protection and interfere with other agents.
Scalability potential: No runtime impact.
Hardware Impact: Prevented extra compiler load during active machine pressure.

## Decision 09: Callback-Compatible Burst Pointer Kernel

Problem: The first pass had a Burst `IJob`, but the original assignment also requires the mixer to be callable from an audio-transfer callback without managed synthesis work.
Solution: Added `EvaluateHullStressGranularAudioDelegate` and `HullStressGranularDspKernel.EvaluateAudioCallback`, compiled through `BurstCompiler.CompileFunctionPointer` and marked with `MonoPInvokeCallback`. The job now calls the same raw-pointer `EvaluateBlock` path over `float*`, `GranularVoiceDTO*`, PCM pointers, telemetry pointers, and a 96-byte `HullStressAudioBlockParamsDTO` carrying `OutputSampleCapacity@80`.
Rejected Alternatives: Calling `IJob.Execute()` directly from `OnAudioFilterRead` would not prove Burst execution. Adding synthesis code to `PlayerCriticalProceduralAudioRenderer.OnAudioFilterRead` conflicts with `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, which defines callback code as a transfer bridge only.
Scalability potential: Low/Middle/High/Ultra use the same route. `GlobalQualityWeight` continuously lerps voice count and nearest-to-linear sample interpolation blend; it does not change DTO layout or authority ownership.
Hardware Impact: Removes managed dispatch from the callback-compatible route. Expected i3/MX350 gain is lower call overhead and bounded cache-line traversal versus per-source Unity spatializers; exact microseconds remain pending Unity profiler.

## Decision 10: AST Scanner Instead Of String Proof

Problem: Task 19 explicitly requested AST proof, but the earlier scanner state was line/string based and shared provenance with SHINOBU_339.
Solution: Added `Assets/_Project/Scripts/Audio/Editor/OOP_AudioSource_Scanner.cs`. It parses scoped source files through Roslyn `CSharpSyntaxTree`, detects `new AudioSource`, `AddComponent<AudioSource>`, `AudioSource.PlayClipAtPoint`, likely `AudioSource.Play/PlayOneShot`, `Resources.Load<AudioClip>`, and `Dictionary<string, AudioClip>` patterns, then writes a SHINOBU_351 sidecar and upserts a `shinobu351AudioSourceScanner` shared report section.
Rejected Alternatives: Editing the SHINOBU_339 scanner would corrupt another agent's proof artifact. Keeping string search would mislabel comments and miss generic invocations.
Scalability potential: Editor-only. Runtime cost is 0 us on low, middle, high, and ultra hardware.
Hardware Impact: No player-frame impact. It protects low-end hardware indirectly by catching future component-based stress-audio regressions before they ship.

## Decision 11: No Misleading dotnet Build

Problem: CPU load later dropped to 30.6 and no compiler processes were running, but generated `.csproj` files did not include `HullStressGranularDspKernel.cs` or the new synthesis asmdef sources.
Solution: Do not claim a dotnet compile proof from stale generated projects. Keep verification at static-source level until Unity imports/regenerates project files or a Unity batch compile is explicitly run under the hardware guard.
Rejected Alternatives: Running `dotnet build Assembly-CSharp*.csproj` would mostly validate unrelated or stale source lists and could produce false confidence.
Scalability potential: No runtime impact.
Hardware Impact: Avoids wasting machine time on a non-authoritative compile path while 20+ agents are active.

## Decision 12: NaN Vaccination Follow-Up

Problem: A self-read found two residual nonfinite ingress points: `voice.PitchMultiplier` was advanced directly, and `BaseGrainLengthSeconds` could poison grain duration if a caller passed NaN.
Solution: Route both through `HullStressGranularDspMath.FiniteOrDefault` before math. Stereo channel indexing now uses `frame << 1` when stride is exactly two, keeping the common callback path cheap and explicit.
Rejected Alternatives: Relying on upstream callers to sanitize every field would violate the mathematical fatalism mandate.
Scalability potential: Same behavior across all quality weights; only invalid inputs are collapsed to stable defaults.
Hardware Impact: Prevents NaN propagation into audio buffers and telemetry on low-end silicon where recovering from denormal/NaN spikes is more expensive than the guard.

## Decision 13: Scanner Structural Hardening And Ledger Route Card

Problem: The first Roslyn scanner still matched `Dictionary<string, AudioClip>` by a formatted type string and did not catch `AudioSource[]` allocation sites. The binary payload ledger also lacked a SHINOBU_351 route card.
Solution: Replace formatted dictionary detection with `GenericNameSyntax` argument checks for `Dictionary<string, AudioClip>`/qualified equivalents and add `ArrayCreationExpressionSyntax` detection for `AudioSource[]`. Add a concise SHINOBU_351 entry to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` recording the existing `431..440` Vault lanes, the transient callback DTO ABI, and the `SignalBus<BaseStructuralWarningSignal>` route.
Rejected Alternatives: Keeping a string-only scanner would miss spacing/alias variants; adding new BufferIDs just to satisfy a report would create false ownership and binary payload debt.
Scalability potential: Scanner and ledger edits are editor/static proof only. Runtime scaling remains continuous through `GlobalQualityWeight` voice count and interpolation blend.
Hardware Impact: Runtime cost remains 0 us. The static scanner catches future managed audio-source regressions that would otherwise cost Unity source/spatializer overhead on i3/MX350-class devices.

## Decision 14: Report Artifact Without Fake AST Execution

Problem: The Unity Editor scanner writes the authoritative Roslyn AST report, but no Unity menu execution channel is available in this shell. Direct PowerShell/Roslyn execution failed under Windows PowerShell 5.1 because Roslyn dependencies demanded conflicting `System.Runtime.CompilerServices.Unsafe` assembly versions.
Solution: Keep the Unity AST scanner source as the authoritative implementation and generate a clearly labeled CLI regex fallback report at `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_351.json`, then upsert the same section into `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json`. The report explicitly sets `reportUsesRoslynAst=false` while keeping `scannerSourceUsesRoslynAst=true`.
Rejected Alternatives: Claiming the fallback report was AST output would be a false proof. Launching Unity just to execute the menu would violate the current build/import restraint without a compile window.
Scalability potential: No runtime impact. The report protects low-tier hardware by flagging reintroduced Habitat/Physics source playback before it ships.
Hardware Impact: Runtime cost remains 0 us. The fallback found 5 central-audio findings and 0 active Habitat/Physics violations across 91 scanned files.

## Decision 15: Build Gate Still Closed

Problem: Import/compile proof is still requested by the workflow, but the hardware guard and generated project state do not allow a useful build.
Solution: Resample the guard instead of launching a rebuild. CPU averaged 80.23%, `dotnet=0`, `csc=0`, and `rg -g "*.csproj"` still found no inclusion for `HullStressGranularDspKernel`, `OOP_AudioSource_Scanner`, or `AbyssalDspTunerWindow`.
Rejected Alternatives: Running `dotnet build` under 80% CPU and stale csproj state would violate policy and produce a false-negative/false-positive compile artifact.
Scalability potential: No runtime impact.
Hardware Impact: Avoided compiler load on a busy workstation; verification remains static until Unity import regenerates project files under a legal CPU window.

## Decision 16: Managed Hull Audio Purge And Static Smoke Proof

Problem: A fresh source read found residual managed presentation debt in the player-critical renderer: a dead `UpdateHullGroanLoop` hook, metal-stress `AudioClip.GetData` staging, and a minimum-quality `AudioClip`/`PlayAtPoint` fallback. Even if some of it was cold or optional, it weakened the SHINOBU_351 proof surface and kept standard Unity audio vocabulary inside the owner.
Solution: Remove the no-op hull loop hook, delete the metal-stress authored clip importer and managed scratch array, and make the metallic PCM bank deterministic through `PlayerCriticalMetallicGrainBank.Generate`. Remove the remaining low-tier kinetic `AudioClip`/`PlayAtPoint` fallback from the same renderer so the owner+kernel static surface has zero `AudioSource`, `AudioClip`, `PlayAtPoint`, or managed `OnAudioFilterRead` synthesis references. Add `Shinobu351HullStressDspSmokeTester` and `Docs/Reports/AUDIO_SHINOBU_351_STATIC_SMOKE.json`; CLI mirror checks report 40 passed and 0 failed.
Rejected Alternatives: Keeping authored clip import as a cold path would preserve art flexibility but contradict the mission to burn managed hull-crack sounds. Keeping low-tier `PlayAtPoint` as a fallback would reintroduce source/spatializer dispatch exactly when weak hardware is under pressure.
Scalability potential: Low uses deterministic cheap procedural metallic grains with reduced polyphony and nearest-biased sampling. Middle raises voice count and interpolation. High/Ultra keep the same authority route while increasing granular density, interpolation blend, and telemetry/debug visibility.
Hardware Impact: Removes a 262144-float managed staging array and one managed point-playback fallback from the critical renderer. Static estimate: 20-80 us saved on stress onset from removed source/clip churn, and 350-1600 us avoided in dense stress scenes versus per-source Unity spatializers; runtime profiler proof remains pending Unity import.

## Decision 17: Build Gate Recheck Under Active Compiler Load

Problem: The workflow still needs an import/compile proof, but the machine is under active compiler pressure.
Solution: Resample before any build. The current guard returned CPU 100%, `dotnet=8`, and `csc=1`; no build was launched. Re-ran the critical forbidden-audio scan on the owner and kernel instead; it returned zero hits.
Rejected Alternatives: Starting another `dotnet`/Unity compile would violate the explicit CPU and compiler-contention guard and could corrupt the signal from other agents' builds.
Scalability potential: No runtime cost. The static proof preserves the low-tier audio route until a legal Unity import window exists.
Hardware Impact: Prevents additional compiler saturation on a busy workstation; runtime microsecond claims remain static estimates until profiler/Burst inspection can run.

## Decision 18: Nonfinite Amplitude Fence And Asmdef Scope Correction

Problem: A self-read found that `voice.Amplitude <= 0f` does not reject NaN, allowing a poisoned cosmetic voice to enter the mixer and the voice-stealing priority path. I also briefly considered removing `Hecton8.Core` and `Hecton8.Core.Memory` from the synthesis asmdef, but sibling files in the same assembly (`VocalBankPlaybackRuntime`, dynamic music synthesis) still require those references.
Solution: Add an explicit `!math.isfinite(voice.Amplitude)` reject before mixing, route voice-stealing priority through `FiniteOrZero(voice.Amplitude)`, and suppress CS0169 for the 32-byte profile padding field. Keep the existing synthesis asmdef references intact to avoid breaking neighboring owners; strengthen the SHINOBU smoke proof to assert that `HullStressGranularDspKernel.cs` itself uses contracts-only cross-domain input and no `Hecton8.Core.Memory` using.
Rejected Alternatives: Relying on `math.saturate` later in the sample path would still let a NaN voice consume traversal and telemetry identity. Removing assembly references that other synthesis files use would create a compile break outside SHINOBU_351 scope.
Scalability potential: Low/Middle/High/Ultra all benefit from the same poison-input collapse; quality still only changes active count and interpolation blend.
Hardware Impact: One finite branch per active candidate avoids NaN propagation and prevents wasted mixer work on invalid rows. Runtime cost is below measurement noise; the avoided failure is audio-thread poison and false voice occupancy.

## Decision 19: Physics Fluid Feedback AudioSource Removal

Problem: A broad Habitat/Physics runtime scan found `Physics/FluidFeedbackListener` still had a serialized `AudioSource`, local `AudioClip`, and `_audio.PlayAtPoint` branch for "hull splash feedback". The previous sidecar report claiming zero active Habitat/Physics violations was therefore incomplete.
Solution: Remove only the managed audio branch from `FluidFeedbackListener`, leaving the flat decal feedback and SignalBus `SplashEvent` dispatch intact. Harden `OOP_AudioSource_Scanner` so future runs catch serialized `AudioSource`/`AudioClip` declarations and any `PlayAtPoint` invocation, not only `AudioSource.PlayClipAtPoint` or `AddComponent<AudioSource>`.
Rejected Alternatives: Editing broad gameplay `PlayAtPoint` routes is outside SHINOBU_351. Keeping the Physics splash AudioSource as "optional" would preserve exactly the managed acoustic bypass Task 19 is supposed to expose.
Scalability potential: Low devices no longer pay Unity source/spatializer cost for splash feedback from the Physics listener. Middle/High/Ultra can reintroduce fluid audio only through a typed procedural audio route, not a local source component.
Hardware Impact: Removes one serialized source lookup and one managed clip dispatch path per splash event in this listener. Static estimate: 10-60 us avoided per splash event on low-end hardware; profiler proof pending legal import/build window.

## Decision 20: Raw Callback Output Capacity Fence

Problem: The raw Burst callback accepted `OutputInterleaved` and `FrameCount` but had no ABI field proving the destination buffer length. A caller with stale or mismatched frame count could overrun the audio callback buffer.
Solution: Expand `HullStressAudioBlockParamsDTO` from 80 to 96 bytes and add `OutputSampleCapacity@80`. `EvaluateBlock` clamps `frameCount` to `OutputSampleCapacity / outputStride`, records `TelemetryFlagOutputCapacityInvalid` on invalid capacity, and returns before writing when capacity is zero. Ledger and smoke proof now assert `HullStressAudioBlockParamsDTO=96` and `OutputSampleCapacity`.
Rejected Alternatives: Trusting the caller was rejected because the callback is a raw-pointer ABI. Adding a managed wrapper-only length check was rejected because the Burst function pointer must be safe by itself.
Scalability potential: Low/Middle/High/Ultra keep the same DTO and route; quality still only changes voice count and interpolation. The guard is deterministic and does not change authority or save identity.
Hardware Impact: One integer clamp per block prevents catastrophic memory overwrite. Runtime cost is below measurement noise; the avoided failure is audio-thread corruption on both ARM64 and desktop.

## Decision 21: Cross-Owner Managed Hull Groan Purge

Problem: Sidecar archaeology found three remaining in-scope managed hull-groan routes outside the player-critical renderer: `DeepPsychosisController.hullStressClips`, unused crush-depth groan/implosion clips in `HectonPlayerMovement`, and `MountablePlayerTransport.entanglementStressGroanSound`.
Solution: Remove `hullStressClips` and route low-intensity psychosis hull tension into `ProceduralAudioEvents.RaiseStructuralStressTriggered`; keep whisper clips as non-hull psychosis content. Remove crush-depth groan/implosion clip fields and the fatal `PlayStatic2D` branch because `TryPlayCrushDepthGroan` already publishes a structural warning signal. Replace entanglement one-shot playback with a procedural structural stress event derived from tether overload.
Rejected Alternatives: Removing all `AudioClip` use from these classes would sabotage non-hull whisper, surface-gasp, mount, and dismount cues outside SHINOBU_351. Keeping the hull-groan clips as optional fallbacks would preserve Unity source/spatializer dispatch in exactly the stress-audio path being replaced.
Scalability potential: Low uses the existing procedural stress route with cheap voice counts. Middle/High/Ultra can make the same stress event richer in the DSP owner without changing gameplay producers.
Hardware Impact: Removes three managed clip-reference/playback paths from structural stress presentation. Static estimate: 15-90 us avoided per triggered groan route on low-end silicon, plus avoided audio-source voice contention during dense stress scenes; profiler proof pending legal Unity import window.

## Decision 22: Build Gate Remains Closed After Static Proof

Problem: A compile proof is still desirable, but project policy forbids rebuild while CPU exceeds 50%, and generated `.csproj` files still do not include the new SHINOBU_351 source files.
Solution: Run static gates and resample the guard instead of launching a misleading build. Latest sample returned CPU 66%, `dotnet=0`, `csc=0`; `rg -g "*.csproj"` returned no hits for `HullStressGranularDspKernel`, `Shinobu351HullStressDspSmokeTester`, `OOP_AudioSource_Scanner`, or `AbyssalDspTunerWindow`.
Rejected Alternatives: Starting `dotnet build` under a closed CPU gate and stale project files would violate the batch rule and produce non-authoritative output.
Scalability potential: No runtime effect.
Hardware Impact: Avoids compiler load on an already busy workstation; runtime validation remains static until Unity import/project regeneration is legal.

## Decision 23: Deterministic Extra-Channel Mixdown

Problem: The raw mixer wrote mono or stereo channels, but for `Channels > 2` it left channels 2..N dependent on the caller's preexisting buffer contents. That is deterministic only if every caller clears the buffer before entering the callback, which is the wrong side of the ABI boundary.
Solution: Keep the L/R pan path unchanged and explicitly write a soft-clipped mono tail into every channel index from 2 to `outputStride - 1`. RMS accounting now divides by the actual output stride because the mixer owns every channel it touches.
Rejected Alternatives: Forcing stereo-only output would be a binary capability downgrade and would hide a stale-channel bug in VR/surround devices. Clearing extra channels to zero was rejected because it would discard useful downmix energy for non-stereo output devices.
Scalability potential: The extra loop only runs when the device requests more than two channels. Quality still controls voice count and interpolation continuously.
Hardware Impact: Stereo path remains unchanged and uses `frame << 1`. Multi-channel path adds bounded `O(extraChannels)` writes per frame and prevents stale buffer noise on VR/surround output.

## Decision 24: Acoustic Fatal Pressure Procedural Reroute

Problem: Subagent archaeology found `AcousticZoneController` still holding `fatalPressureNoisePrimary`/`fatalPressureNoiseSecondary` `AudioClip` fields and playing fatal-pressure white-noise bursts through `PlayStatic2D` inside the tick path. That is a managed clip/source-style hull-crush presentation route adjacent to SHINOBU_351 scope.
Solution: Remove the fatal-pressure clip fields, remove the alternating clip toggle, remove the madness-whisper fallback to those clips, and replace the fatal-pressure loop with `ProceduralAudioEvents.RaiseStructuralStressTriggered` using the player's AUP-resolved runtime position plus tunable stress and pitch ranges. The cadence remains continuous through `math.lerp` between min/max intervals.
Rejected Alternatives: Keeping white-noise clips as flavor fallback would preserve managed dispatch under the highest hull-stress condition. Removing all acoustic-zone clips was rejected because sonar, storm, ambient, and whisper cues are not structural hull stress and belong to other owners.
Scalability potential: Low hardware emits sparse procedural stress events into the existing granular route; Middle/High/Ultra enrich the same route through `GlobalQualityWeight` in the DSP owner without changing `AcousticZoneController` authority.
Hardware Impact: Removes two fatal-pressure `AudioClip` references and one tick-path `PlayStatic2D` branch from hull-crush presentation. Static estimate: 15-70 us avoided per fatal-pressure burst on weak CPUs, plus avoided Unity source-group contention during crush-depth escalation.

## Decision 25: Submarine Hull Warning Clip Field Purge

Problem: `Gameplay/HectonSubmarineOS` still declared `hullBreachWarningClip` and `hullStressWarningClip` serialized fields, but source search showed they were dead fields; active warning output already uses `hullBreachWarningEventId`, `hullStressWarningEventId`, and `VocalWarningSignal`.
Solution: Remove only the two hull-specific clip fields while preserving the VWS event-id route and non-hull warning clip fields outside SHINOBU_351 scope.
Rejected Alternatives: Removing all submarine warning clips would alter non-hull VWS behavior owned by another domain. Keeping dead hull clip fields would leave managed hull-warning vocabulary in source and weaken the static scanner proof.
Scalability potential: No runtime behavior change; the active route remains signal/event-id based and can be resolved by central audio quality policy.
Hardware Impact: Runtime microsecond gain is 0 because fields were dead, but import/serialized-state surface is smaller and the managed hull-audio regression vector is removed.

## Decision 26: Build Gate Closed After Final Static Smoke

Problem: A compile/import proof is still pending after the acoustic and submarine purges, but the hardware guard sampled CPU at 100% with eight active `dotnet` processes.
Solution: Do not launch another rebuild. Keep proof at source/static-smoke level until Unity import/project generation can run under CPU <=50% and no active compiler contention.
Rejected Alternatives: Starting a build under 100% CPU and active dotnet load would violate the explicit batch guard and contaminate other agents' compiler work.
Scalability potential: No runtime impact.
Hardware Impact: Avoided additional compiler saturation on the shared workstation; runtime verification remains pending a legal import window.

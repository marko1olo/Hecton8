# Rationale_SHINOBU_15

Date: 2026-05-17
Agent: SHINOBU_15
Domain: ECHELON 8 PRESENTATION & UX / DSP Acoustic Radar
Status: COMPLETE_WITH_EXTERNAL_BUILD_BLOCKER

## Mandate Selection

Problem: Audio-channel overload from physical `AudioSource` proliferation.
Solution: Fixed virtual-voice DTO pool, Burst math, fixed hydration cap, cheap SDF midpoint occlusion, Sabine RT60 scalar.
Rejected Alternatives: Per-object `AudioSource`, ray-bounced acoustic tracing, List.Sort/LINQ, per-frame managed clip lookup.
Scalability potential: Low uses 16 hydrated voices and no SDF occlusion; Middle uses 24 voices and midpoint SDF; High uses 32 voices plus richer filter state; Ultra keeps 32 engine voices but adds heavier reverb/IR authoring outside the hot path.
Hardware Impact: Estimated MX350/i3 gain is removal of hundreds of Unity audio source updates; exact profiler proof absent, status PENDING VERIFICATION.

Problem: Dear Lie mandate for occlusion.
Solution: One midpoint SDF distance sample between listener and emitter; negative distance applies -12 dB and low-pass target.
Rejected Alternatives: Unity Physics raycast chains, acoustic raymarch, polygon bounce simulation.
Scalability potential: Low disables SDF occlusion; Middle samples midpoint; High samples midpoint plus cavern volume class; Ultra can add author-authored IR table selection.
Hardware Impact: Expected per-voice occlusion cost drops from physics query overhead to scalar math; exact profiler proof absent.

## Loop 1 Decisions

Problem: Task 01 required old Sabine and occlusion binaries before runtime math changes.
Solution: Scanned `Docs/Archive`, active `Data`, and the missing `Assets/StreamingAssets` path; consumed `Rationale_SABINE_REVERB_MATRIX_GEN.md` and `Rationale_SOUNDSCAPE_SABINE_BAKER.md`. Active data authority is `Data/Audio/Acoustic_LUT.bin` (raw `<ff>` RT60+damping, 524288 bytes) plus `Data/Precomputed/Reverb_LUT.bin` (headered 256x256 float32, 262400 bytes). `audio_occlusion_curves.bin` and `sabine_reverb_matrix.h8bin` were not found in active data.
Rejected Alternatives: Emergency mock RT60 coefficients were rejected because verified active LUTs already exist. Reading unrelated archive agent logs was rejected after the Sabine owners supplied the needed RT60 layout.
Scalability potential: Low uses nearest LUT/control scalar; Middle/High can bilerp outside the DSP sample loop; Ultra keeps the same authority data and spends budget on richer presentation.
Hardware Impact: Existing LUT authority avoids runtime Sabine/damping recomputation; inherited estimate is 4-20 us saved per acoustic-zone update on i3/MX350, profiler proof still absent.

Problem: Tasks 02-05 required a virtual DTO surface without relying on other agents' samplers or Unity's per-object audio components.
Solution: Rebuilt the audio virtualization contracts around raw-field structs: `VirtualVoiceDTO` is exactly 48 bytes with `double3 + float + float + uint + uint + float + uint`, `VirtualVoiceRequest/VirtualVoice/VirtualVoiceSelection` use explicit padded strides, and the final Burst job mutates a GlobalDataVault-backed `NativeArray<VirtualVoice>` with explicit `VoiceCount`. Added `MockSDFSampler`, `MockTerrainSampler`, `MockAcousticEmitterSignal`, and padded `AcousticEchoTap`.
Rejected Alternatives: `List.Sort`, LINQ, dynamic `AudioSource` creation, property-wrapped DTOs, Unity raycasts, and direct terrain-domain dependencies were rejected. Changing legacy `AcousticAup` or AI echo structs outside this agent's domain was also rejected.
Scalability potential: Low/MX350 keeps 16 hydrated sources and disables SDF checks; Middle/High/Ultra retain the 1000-voice math pool with the same DTO ABI and can spend only the hydrated 32 voices on Unity audio presentation.
Hardware Impact: The core collapse is 1000 virtual inputs to 32 or 16 authored `AudioSource`s. Estimated saving is hundreds of Unity component updates and channel arbitration calls per dense audio frame; exact profiler proof pending.

Problem: Loop 1 required compile evidence before Phase 2.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; result 0 warnings, 0 errors. Ran scoped `git diff --check`; only CRLF normalization warnings were emitted.
Rejected Alternatives: Skipping compile until the end was rejected because the batch protocol requires strict iterative proof.
Scalability potential: Compile pass proves the new DTO ABI and authored pool hooks integrate with the current core assembly before adding more DSP controls.
Hardware Impact: No runtime impact; prevents carrying a broken audio assembly into later loops.

Problem: Tasks 06-10 required the mathematical sound core: rank 1000 voices, hydrate 32, compute Doppler, fake occlusion, apply Sabine, and muffle hull-audible exterior sounds.
Solution: `VirtualVoiceSortJob` now compacts audible voices, computes inverse-square effective volume, Doppler pitch, delay, midpoint SDF occlusion, and Sabine RT60, then sorts the vault-backed unmanaged voice array descending and exports only the selected physical channels. `SpatialAudioManager` hydrates authored sources with effective volume, pitch, LPF, RT60-derived reverb mix, and speed-of-sound delay. Low tier caps physical voices at 16 and disables SDF.
Rejected Alternatives: Unity falloff, Unity doppler-only, AudioReverbZone truth, acoustic ray bounces, native convolution for hull muffling, and direct submarine/world-sampler dependencies were rejected. Full acoustic realism is a lie; this pass uses controlled math fakes.
Scalability potential: Low/toaster: 16 hydrated, no SDF, RT60 fallback. Middle: 32 hydrated with midpoint SDF and Sabine scalar. High: same voice cap with richer filter/reverb presentation. Ultra: spend downstream DSP budget on authored IR/early reflection layers without increasing Unity voice count.
Hardware Impact: Expected low-end gain is the removal of Unity updates for up to 968 non-hydrated voices plus replacing raycasts with one midpoint scalar. Sort and hydration costs remain pending profiler; blackbox will dump if sort wait exceeds 0.5 ms.

Problem: Loop 2 compile verification hit a non-audio compile wall.
Solution: Scoped source checks passed; `python -B Tools/VerifySabineBaker.py` reported `STATUS: SABINE_LUT_VERIFIED`. `dotnet build Hecton8.Core.csproj` fails in `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` with CS0535 and CS0246 for a missing `LateFrameTick()` and `MockNarrativeTriggerSignal`; this file is outside SHINOBU domain and appears as unrelated modified work.
Rejected Alternatives: Editing environment/tide code or reverting another agent was rejected as cross-domain sabotage. Ignoring the failure as audio success was rejected; it is recorded as dependency-blocked compile evidence.
Scalability potential: Audio source-level gates still hold; full assembly proof waited on the owner of the environment compile errors during this loop and later passed in R3.
Hardware Impact: No runtime impact from the blocker. Audio profiler proof remains unavailable until the shared project compiles.

Problem: Tasks 11-15 needed AI acoustic handoff, inverse-square culling, hardware throttles, AUP-safe positioning, and water-speed delay.
Solution: Hydrated high-intensity virtual voices publish `AcousticPingSignal` through the existing configured SignalBus lane; portal paths still enqueue echo taps. The Burst job computes effective volume with `rcp(max(1, distanceSq))`, stores delay as `distance/1500`, and low-tier disables SDF. Hydrated virtual sources use listener-relative AUP conversion before assigning Unity transforms.
Rejected Alternatives: Creating a brand-new unconfigured `AcousticEchoTap` SignalBus lane was rejected because it would require cross-domain core signal registry edits. Absolute world floats were rejected for jitter. Instant sound was rejected for far-field events.
Scalability potential: Low gets fewer hydrated voices and no SDF; Middle/High retain precise local positions and delayed hydration; Ultra can add richer downstream ping interpretation without changing audio voice count.
Hardware Impact: AI ping is one configured signal write when a sound starts, not a per-frame flood. AUP conversion and delay cost are bounded by hydrated/audible virtual voice count.

Problem: Tasks 16-20 required the human control facade and zero-GC clip routing without moving audio authority into managed/editor-only state.
Solution: Added `NativeParallelHashMap<uint,int>` from clip hash to preloaded `_audioEventClipTable` index, registered and disposed through `NativeMemorySentinel`. Added `VirtualVoiceTuningSnapshot` in vault-backed unmanaged memory, Burst job knobs for sound speed, occlusion gain, occluded low-pass, Sabine scale, and hydrated voice cap, plus `SabineReverbDspTunerWindow` to edit those values during Play Mode. Added span/cursor CSV parsing for `audio_profiles.csv` and editor scene gizmos for green hydrated, yellow virtual, and red falloff lines.
Rejected Alternatives: `Resources.Load`, managed Dictionary, string-split CSV parsing, ScriptableObject live churn, runtime debug GameObjects, and managed static globals were rejected. A brand-new BufferID enum edit was also rejected to avoid cross-domain memory ID collisions; SHINOBU uses a private cast BufferID and owner `SystemID.Audio` for the vault-backed tuning cell.
Scalability potential: Low/toaster clamps hydrated voices to 16, can set SDF disabled from CSV/tuner, and keeps only scalar LPF/RT60 controls. Middle/High keep 32 hydrated voices and live Dear-Lie occlusion. Ultra keeps the same 32 engine voices but can exaggerate decay and occlusion presentation from the tuner without increasing channel count.
Hardware Impact: Clip lookup stays uint -> int in native memory, avoiding per-frame load/Dictionary churn. Tuning is one 32-byte unmanaged snapshot read per sort pass. CSV reload cost is editor-only; player hot path remains Burst math plus pre-authored AudioSources.

Problem: Loop 3/4 compile verification was blocked outside the audio domain after the shared worktree changed.
Solution: Scoped SHINOBU checks pass: `git diff --check` reports only CRLF normalization warnings, source scan shows no `List.Sort`, LINQ, `Resources.Load`, dynamic `AudioSource`, `Pack=1`, or `Physics.Raycast` in the SHINOBU audio files, and Sabine verifier already passed. Current `dotnet build Hecton8.Core.csproj` fails in cross-domain files: `GlobalTelemetryBus.cs` missing `RequestBlackboxMmfFlushAsync`, `HectonIndirectVegetationRenderer.cs` stale `ReleaseCpuCullingScratchBuffers(deferActiveJobs)` call, and `AI/Ecosystem/ShinobuEcosystemBalancer.cs` missing fields/arguments (`_spatialHash`, `_legacyProfileBuffer`, `_csvReadBuffer`, etc.).
Rejected Alternatives: Editing telemetry, rendering, ecosystem, gameplay, or tide code or reverting another agent was rejected under the domain boundary. Ignoring the build failure was rejected; it is recorded as dependency-blocked compile evidence.
Scalability potential: No change to DSP scalability; full-project proof was blocked at this loop and later passed in R3.
Hardware Impact: No runtime impact from the external compile wall.

<SELF_AUDIT agent_id="SHINOBU_15">
  <TASK_MATRIX>
    <TASK id="01" status="PASS">Sabine/occlusion binary archaeology completed; active authority is `Data/Audio/Acoustic_LUT.bin` and `Data/Precomputed/Reverb_LUT.bin`.</TASK>
    <TASK id="02" status="PASS">World sound emissions are virtualized before hydration; no dynamic AudioSource creation was added.</TASK>
    <TASK id="03" status="PASS">Virtual DTO/request/voice/selection structs use raw fields, not get/set properties.</TASK>
    <TASK id="04" status="PASS">Virtual voice and echo tap structs have explicit 8-byte/multiple-of-16 sizes and no Pack=1 in SHINOBU audio files.</TASK>
    <TASK id="05" status="PASS">MockSDFSampler and MockTerrainSampler isolate the audio math from Agent 04/world sampler dependencies.</TASK>
    <TASK id="06" status="PASS">1000 virtual voices collapse to 32/16 hydrated authored AudioSources using Burst in-place ranking.</TASK>
    <TASK id="07" status="PASS">Burst Doppler uses listener/source velocity projected onto AUP-relative direction.</TASK>
    <TASK id="08" status="PASS">Dear Lie occlusion uses one midpoint SDF sample, -12 dB default gain, and LPF.</TASK>
    <TASK id="09" status="PASS">Sabine RT60 is read from fallback LUT where available and computed as scalar math in Burst.</TASK>
    <TASK id="10" status="PASS">Inside-hull virtual voices clamp high frequencies with hull LPF state, not convolution.</TASK>
    <TASK id="11" status="PASS">Hydrated high-intensity voices publish existing AcousticPingSignal through GlobalSignals.</TASK>
    <TASK id="12" status="PASS">Inverse-square EffectiveVolume culls below 0.01 before hydration.</TASK>
    <TASK id="13" status="PASS">Low/MX350 path caps to 16 physical voices and disables SDF checks.</TASK>
    <TASK id="14" status="PASS">Hydrated source transforms use listener-relative AUP conversion to prevent float jitter.</TASK>
    <TASK id="15" status="PASS">Water-speed delay uses distance / speed snapshot, default 1500 m/s.</TASK>
    <TASK id="16" status="PASS">NativeParallelHashMap uint clip hash -> preloaded clip index replaces managed clip loading/Dictionary lookup.</TASK>
    <TASK id="17" status="PASS">300-frame blackbox ring records state and dumps on NaN or sort wait >0.5 ms.</TASK>
    <TASK id="18" status="PASS">Sabine Reverb & DSP Tuner EditorWindow writes tuning into vault-backed unmanaged memory during Play Mode.</TASK>
    <TASK id="19" status="PASS">audio_profiles.csv is monitored by the editor facade and parsed through span/cursor zero-split parser.</TASK>
    <TASK id="20" status="PASS">Editor gizmos draw hydrated green, virtual yellow, and red listener falloff lines.</TASK>
  </TASK_MATRIX>
  <ARM64_CHECK>
    <VirtualVoiceDTO total_bytes="48">offset 0 double3 AupMeters 24b; offset 24 float Volume 4b; offset 28 float Pitch 4b; offset 32 uint ClipHash 4b; offset 36 uint SourceEntityID 4b; offset 40 float Importance 4b; offset 44 uint Padding 4b.</VirtualVoiceDTO>
    <RuntimeStructs>No Pack=1 remains in `Assets/_Project/Scripts/Audio/Virtualization/*`, `SpatialAudioManager.cs`, or `SabineReverbDspTunerWindow.cs` after polish scan.</RuntimeStructs>
  </ARM64_CHECK>
  <ZERO_GC_CHECK>FastTick, SlowTick, and LateFrameTick scan clean for ToString/StringBuilder/interpolation/foreach patterns. Sort uses Burst custom in-place sort over vault-backed NativeArray data and no managed allocation.</ZERO_GC_CHECK>
  <AUP_CHECK>All virtual distance/Doppler math subtracts listener AUP from source AUP before casting to float3; hydrated Unity transforms use listener-relative runtime positions.</AUP_CHECK>
  <DEAR_LIE_CHECK>Real acoustic ray bounce and raycast occlusion were replaced by midpoint SDF sign, gain scalar, LPF cutoff, and Sabine RT60 scalar.</DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>Cross-domain communication stays through `IAudioVirtualizationService`, `GlobalRegistry`, `GlobalSignals`, and local mock structs; no new sibling runtime assembly references were added.</DEPENDENCY_CHECK>
  <BLACKBOX_CHECK>VirtualVoiceBlackBoxFrameCount is 300, stored in GlobalDataVault buffer `SpatialAudioVirtualVoiceBlackBox`, with dump path `Docs/AgentLogs/Dump_ACOUSTIC_DSP.bin`.</BLACKBOX_CHECK>
  <COMPILE_GUARD>Scoped SHINOBU static checks pass; final `dotnet build Hecton8.Core.csproj` passes with 0 warnings and 0 errors.</COMPILE_GUARD>
</SELF_AUDIT>

## Ultra-Think Polish R2 Decisions

Problem: The virtual voice implementation still owned local persistent `NativeList<VirtualVoice>` queues, contradicting the GlobalDataVault sovereignty rule under the polish mandate.
Solution: Replaced the SHINOBU write/sort queues with GlobalDataVault-backed `NativeArray<VirtualVoice>` double buffers using private audio BufferIDs 70016/70017, plus a vault-backed `NativeArray<VirtualVoiceDTO>` mirror using BufferID 70018. The Burst sort job now receives `NativeArray<VirtualVoice>` plus an explicit `VoiceCount` and compacts/sorts in-place without resizing the container.
Rejected Alternatives: Keeping local `NativeList` ownership was rejected as H-Phi drift. Copying every frame into a managed DTO array was rejected as GC pressure and false authority. Editing the shared BufferID enum was rejected to avoid a cross-domain API churn point in a dirty batch.
Scalability potential: Low keeps the same 1000 virtual / 16 hydrated policy with no local container growth. Middle/High/Ultra keep 1000 virtual / 32 hydrated and can spend saved channel pressure on richer reverb/LPF presentation, not more Unity AudioSources.
Hardware Impact: Removes native ownership ambiguity and growth risk from the audio manager. Exact microseconds saved are not claimed; profiler proof is still absent.

Problem: The first polish pass left `Pack=1` in the core acoustic AUP contract and neighboring audio runtime payloads. That violated the ARM64 alignment mandate and made the previous audit overstated.
Solution: `AcousticAup` is now `[StructLayout(LayoutKind.Sequential, Size = 40)]`: offsets 0/8/16 for `long GridX/Y/Z`, offset 24 for `float3 Local`, offset 36 for `uint _pad0`. Audio propagation structs now use natural packing and explicit sizes: `AcousticPortalNode=56`, `AcousticPortalEdge=16`, `AcousticPathQuery=112`, `SoundEmissionSignal=64`, `AcousticPathResult=104`, `AcousticTelemetryEntry=40`. `AcousticEchoTap` is now 144 bytes to fit two aligned AUPs plus explicit tail padding. Echolocation hit is 56 bytes; native audio ring descriptor is 56 bytes and requires 8-byte pointer alignment.
Rejected Alternatives: Leaving packed layouts because some smoke tests looked for `Pack=1` was rejected. Patching all `GlobalSignals.cs` payloads was rejected as a separate Core signal-lane migration outside this audio task; only the two acoustic/music signal layouts touched by the audio smoke test were normalized without changing their public fields.
Scalability potential: Low/Quest/Steam Deck avoid unaligned acoustic payload reads. High/Ultra retain the same ABI clarity and can layer heavier visual/audio presentation without extra channel truth.
Hardware Impact: Alignment fix is correctness and CPU-safety work, not a measured frame-time win. The expected low-end gain is avoiding ARM64 unaligned-load penalties in DSP/acoustic payload scans.

Problem: Final verification needed to distinguish SHINOBU defects from transient shared worktree compile walls.
Solution: Static checks now show no `Pack=1` in `Assets/_Project/Scripts/Audio`, `SpatialAudioManager.cs`, `AcousticAup.cs`, or `HectonDirectorAI.cs`, and no old `NativeList<VirtualVoice>` queues or forbidden sort/load/raycast calls in SHINOBU virtualization/propagation paths. Sabine LUT verification passed. Final `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with 0 warnings and 0 errors.
Rejected Alternatives: Claiming Unity/runtime readiness was rejected because no Unity import, Play Mode, profiler, GCMonitor, Memory Profiler, or player build was run.
Scalability potential: Audio math path remains tiered: Low disables SDF and hydrates 16 voices; Middle/High/Ultra hydrate 32 voices and enrich presentation through RT60/LPF/Doppler parameters only.
Hardware Impact: Verification-only. Exact runtime savings remain pending profiler evidence.

<SELF_AUDIT_R2 agent_id="SHINOBU_15">
  <TASK_01 status="PASS">Archaeology unchanged; Sabine verifier re-run and passed.</TASK_01>
  <TASK_02 status="PASS">VirtualVoiceDTO now has an actual GlobalDataVault-backed mirror array, not only a contract type.</TASK_02>
  <TASK_03 status="PASS">Virtual structs remain raw-field DTOs; no get/set properties were added.</TASK_03>
  <TASK_04 status="PASS">AcousticAup 40b, VirtualVoiceDTO 48b, AcousticEchoTap 144b, portal/query/result payloads naturally packed and 8-byte aligned.</TASK_04>
  <TASK_05 status="PASS">MockSDFSampler/MockTerrainSampler remain local and dependency-free.</TASK_05>
  <TASK_06 status="PASS">Burst sort now mutates a vault-backed NativeArray with explicit VoiceCount; old virtual NativeList queues removed.</TASK_06>
  <TASK_07 status="PASS">Doppler denominator remains clamped through VirtualVoiceUtility.</TASK_07>
  <TASK_08 status="PASS">Dear Lie midpoint SDF occlusion remains one scalar sample and low-tier-disablable.</TASK_08>
  <TASK_09 status="PASS">Sabine LUT verified by `Tools/VerifySabineBaker.py`.</TASK_09>
  <TASK_10 status="PASS">Hull muffling remains LPF scalar state, no convolution on low tier.</TASK_10>
  <TASK_11 status="PASS">Acoustic ping handoff still uses existing typed SignalBus/GlobalSignals route.</TASK_11>
  <TASK_12 status="PASS">Inverse-square culling still occurs before channel hydration.</TASK_12>
  <TASK_13 status="PASS">Low-tier 16-voice cap and SDF disable remain active.</TASK_13>
  <TASK_14 status="PASS">AUP math subtracts listener/source before float cast; DTO stores double absolute meters only for interchange.</TASK_14>
  <TASK_15 status="PASS">Water delay remains distance / speed with finite guards.</TASK_15>
  <TASK_16 status="PASS">Clip hash lookup remains NativeParallelHashMap to preloaded clip table.</TASK_16>
  <TASK_17 status="PASS">300-frame virtual voice blackbox remains vault-backed and dump-capable.</TASK_17>
  <TASK_18 status="PASS">Editor tuner still writes `VirtualVoiceTuningSnapshot` into vault memory.</TASK_18>
  <TASK_19 status="PASS">CSV parser remains span/cursor based.</TASK_19>
  <TASK_20 status="PASS">Gizmo visualizer reads current vault-backed virtual/sorted voice state.</TASK_20>
  <ARM64_LAYOUT>
    AcousticAup: offset 0 long GridX; offset 8 long GridY; offset 16 long GridZ; offset 24 float3 Local; offset 36 uint pad; total 40.
    VirtualVoiceDTO: offset 0 double3 AupMeters; 24 Volume; 28 Pitch; 32 ClipHash; 36 SourceEntityID; 40 Importance; 44 Padding; total 48.
    AcousticEchoTap: total 144, with SourceAup at aligned offset 24 and ListenerAup at aligned offset 64.
  </ARM64_LAYOUT>
  <ZERO_GC>FastTick/SlowTick/LateFrameTick hot scans found no SHINOBU LINQ sort, List.Sort, Resources.Load, dynamic AudioSource creation, or Physics.Raycast in virtualization/propagation paths.</ZERO_GC>
  <DEPENDENCY>Cross-domain audio paths still use GlobalRegistry interfaces, typed SignalBus lanes, and DataVault handles. No sibling concrete runtime dependency was added.</DEPENDENCY>
  <COMPILE>Full Core build passed with 0 warnings and 0 errors; Sabine verification passed.</COMPILE>
</SELF_AUDIT_R2>

## Final Verification R3

Problem: R2 documentation still recorded an external compile wall after the shared worktree later reached a clean Core build.
Solution: Re-ran verification instead of trusting stale status. `python -B Tools\VerifySabineBaker.py` returned `STATUS: SABINE_LUT_VERIFIED`. `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded`, 0 warnings, 0 errors, elapsed 00:01:37.48. Scoped static scans still returned no SHINOBU `Pack=1`, no old virtual queues, no `NativeList<VirtualVoice>`, no `List.Sort`, no LINQ sort, no `Resources.Load`, and no `Physics.Raycast` in virtualization/propagation paths.
Rejected Alternatives: Updating chat only was rejected because CTO-facing truth lives in `Status`, `Rationale`, and `LOG`. Running Unity Play Mode or profiler was not done; there is no claim of profiler microsecond proof.
Scalability potential: Low remains 16 hydrated voices with SDF disabled; Middle/High/Ultra remain 32 hydrated voices with richer RT60/LPF/Doppler presentation parameters and no additional Unity channel truth.
Hardware Impact: Compile/static verification only. Architectural savings remain the prevention of up to 968 Unity `AudioSource` updates at 1000 virtual voices, but exact runtime microseconds are still unmeasured.

## Ultra-Think Polish R4 Decisions

Problem: R3 still used full-payload sorting semantics inside the Burst voice ranking job. `VirtualVoice` is 160 bytes, so QuickSort swaps moved far more data than the 32-channel decision needs and could thrash L1 cache on low-end CPUs.
Solution: Added `VirtualVoiceSortKey` as a 16-byte sequential struct: offset 0 `float Weight`, offset 4 `int VoiceIndex`, offset 8 `uint StableKey`, offset 12 `uint Padding`. `VirtualVoiceSortJob` now compacts audible voices once, writes one sort key per audible voice, sorts the key stream, and builds the selected 16/32 hydrated candidates through indexed voice reads. The sort-key array is GlobalDataVault-backed through `SpatialAudioVirtualVoiceSortKeyPoolBufferId`.
Rejected Alternatives: Keeping full `VirtualVoice` swaps was rejected as cache-line waste. A managed priority queue was rejected for GC. A partial top-32 insertion-only path was rejected because the original prompt required sorted prioritization evidence; sorted compact keys preserve that evidence without moving 160-byte payloads.
Scalability potential: Low/MX350 sorts at most 1000 compact keys and hydrates 16 sources. Middle/High/Ultra keep 32 Unity voices and spend saved CPU on RT60/LPF/Doppler presentation, not extra channel truth.
Hardware Impact: Worst-case swap payload drops from 160-byte voice structs to 16-byte keys. Exact microsecond savings are not claimed without profiler proof.

Problem: `CompleteVirtualVoiceSort()` was a single blocking path used from both late-frame handoff and the start of `FastTick`. Blocking in `LateFrameTick` is the selected handoff boundary; blocking at `FastTick` start is not acceptable under the native job discipline.
Solution: Split completion into `TryCompleteVirtualVoiceSort(bool allowBlocking)`. `FastTick` calls it with `false`: if the previous sort is still running, SHINOBU drops the new write-frame, pushes telemetry, and lets late-frame complete the old handoff. `LateFrameTick`, origin-shift, AUP rebase, and teardown keep explicit blocking completion because those are structural ownership boundaries.
Rejected Alternatives: Spinning, waiting, or blindly scheduling another sort over aliased buffers was rejected. Skipping telemetry on overrun was rejected because blackbox is mandatory.
Scalability potential: Low tier sheds the new virtual write-frame instead of stalling. High/Ultra keep deterministic handoff and can still observe sort overruns through telemetry.
Hardware Impact: Removes a possible main-thread stall at `FastTick` entry. Exact saved time depends on worker contention and is unmeasured.

Problem: The prior proof relied on broad logs and a large generic acoustics smoke tester, not a focused SHINOBU invariant check.
Solution: Added editor-only `ShinobuAcousticDspSmokeTester` to assert the 48-byte DTO, 16-byte sort key, vault-backed key buffer, non-blocking `FastTick` completion, stable 300-frame blackbox path, and no `Pack=1` in the immediate acoustic DSP contracts.
Rejected Alternatives: Embedding reflection/runtime checks into player code was rejected. Chat-only assertion was rejected.
Scalability potential: No runtime change; editor-only guard prevents future regression back to full-payload sort or packed structs.
Hardware Impact: Editor-only verification, zero player-frame cost.

Problem: Full compile verification regressed after unrelated worktree changes.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. The earlier `WakeRequestSignal` blocker is no longer current; `PhysicsWakeSignalContracts.cs` exists under `Hecton8.Core.Contracts.Signals` and `GlobalPhysicsStateManager.cs` imports it. The active compile wall is outside SHINOBU audio DSP: `UI/SubtitleManager.cs(530)` missing `DrainGlobalSubtitleSignals`, `GlobalPhysicsStateManager.cs` missing the SHINOBU_37 physics-culling partial state/methods/jobs, and `Physics/Vehicles/SubmarineDynamicsRuntime.cs(425)` ambiguous `math.min`. Scoped SHINOBU static scans and Sabine verifier pass.
Rejected Alternatives: Editing UI subtitle plumbing, Physics SHINOBU_37 culling, or vehicle dynamics was rejected under the domain boundary. These are not acoustic DSP ownership, and hiding them would corrupt cross-agent accountability.
Scalability potential: SHINOBU DSP tiering remains intact; full-project compile waits on UI/Physics/Vehicle owners.
Hardware Impact: No SHINOBU runtime impact from the external compile wall.

<SELF_AUDIT_R4 agent_id="SHINOBU_15">
  <TASKS_01_20 status="PASS">All original tasks remain represented in `Status_SHINOBU_15.md`; R4 changes refine Task 04, Task 06, Task 17, and Task 18 evidence.</TASKS_01_20>
  <ARM64_LAYOUT>VirtualVoiceDTO: offset 0 double3 AupMeters, 24 Volume, 28 Pitch, 32 ClipHash, 36 SourceEntityID, 40 Importance, 44 Padding, total 48. VirtualVoiceSortKey: 0 Weight, 4 VoiceIndex, 8 StableKey, 12 Padding, total 16. AcousticAup remains 40. AcousticEchoTap remains 144.</ARM64_LAYOUT>
  <ZERO_GC>FastTick/LateFrame virtual path uses vault-backed NativeArrays, no LINQ/List.Sort/Resources.Load/Physics.Raycast, and no full-payload sort. No profiler/GCMonitor proof was run.</ZERO_GC>
  <AUP>Distance, Doppler, delay, and hydration still subtract listener/source AUP before float math; DTO double absolute meters are interchange data, not hot distance math.</AUP>
  <DEAR_LIE>Occlusion remains a midpoint SDF sign check with gain/LPF penalty; low tier disables SDF.</DEAR_LIE>
  <H_PHI>Virtual voice write/sort/DTO/sort-key/selection/statistics/blackbox/tuning arrays are GlobalDataVault-backed aliases with handles.</H_PHI>
  <BLACKBOX>300-frame virtual voice ring remains active; non-finite values, sort wait over 0.5 ms, and non-blocking completion overrun push telemetry and can dump `Docs/AgentLogs/Dump_ACOUSTIC_DSP.bin`.</BLACKBOX>
  <DEPENDENCY>Cross-domain route remains `IAudioVirtualizationService`, `GlobalRegistry`, typed `SignalBus`, and local mocks. No new sibling runtime concrete dependency was added.</DEPENDENCY>
  <COMPILE>Full Core build is blocked outside SHINOBU by UI subtitle, SHINOBU_37 physics-culling partial, and vehicle dynamics errors; scoped SHINOBU static checks and Sabine verifier pass.</COMPILE>
</SELF_AUDIT_R4>

## Ultra-Think Polish R5 Decisions

Problem: R4 removed packed layouts and proved primary DTO size, but several AUP-bearing audio structs still placed 4-byte identifiers before `AcousticAup` fields. CLR padding kept them aligned, but the mandate requires explicit 8-byte/AUP-first ordering, not accidental alignment.
Solution: Reordered fields without changing public field names or call sites. `VirtualVoiceRequest`, `VirtualVoice`, `VirtualVoiceSelection`, `AcousticEchoTap`, `SoundEmissionSignal`, `AcousticPathResult`, and `AcousticPortalCacheEntry` now place `AcousticAup`/AUP payloads first, then float/int/uint fields, then byte flags and explicit padding. `VirtualVoiceTelemetryEntry` now places 4-byte state/float values before ushort counters.
Rejected Alternatives: Leaving the order alone and relying on implicit padding was rejected. Switching to `Pack=1` or explicit byte overlays was rejected because it would damage ARM64 runtime loads and make the payloads harder to audit.
Scalability potential: Low/Quest/Steam Deck avoid hidden padding assumptions in dense DSP/acoustic scans. Middle/High/Ultra keep the same 32-channel truth and can spend headroom on presentation only.
Hardware Impact: No measured microsecond claim. The correction is ABI hygiene: fewer ambiguous cache-line reads and a defensible ARM64 layout report.

Problem: Latest compile verification changed again while other agents were editing the shared worktree.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` after the R5 layout patch. The active compile wall is now outside SHINOBU: `Assets/_Project/Scripts/LocRegistry.cs(404)` cannot see `ISignal`, plus a duplicate-source warning for the untracked `PhysicsWakeSignalContracts.cs`. No SHINOBU audio source file appears in the compiler errors.
Rejected Alternatives: Editing localization/core signal ownership was rejected because SHINOBU_15 owns acoustic DSP, not localization signal contracts.
Scalability potential: No change to DSP tiering; full-project compile waits on localization/core signal ownership.
Hardware Impact: No SHINOBU runtime impact from the external compile wall.

<SELF_AUDIT_R5 agent_id="SHINOBU_15">
  <ARM64_FIELD_ORDER status="PASS">AUP-bearing audio structs now start with AUP payloads: `VirtualVoiceRequest`, `VirtualVoice`, `VirtualVoiceSelection`, `AcousticEchoTap`, `SoundEmissionSignal`, `AcousticPathResult`, `AcousticPortalCacheEntry`, `ActiveEmitterSample`, `ActiveImpactEmitterSample`, `DelayedAudioEvent`, `ImpactEmitterSample`, and `AudioCaptionPayload`.</ARM64_FIELD_ORDER>
  <PRIMARY_LAYOUT>VirtualVoiceDTO: offset 0 double3 AupMeters, 24 Volume, 28 Pitch, 32 ClipHash, 36 SourceEntityID, 40 Importance, 44 Padding, total 48. VirtualVoiceSortKey: offset 0 Weight, 4 VoiceIndex, 8 StableKey, 12 Padding, total 16.</PRIMARY_LAYOUT>
  <ZERO_GC>Scoped hot-path scan remains clean for old virtual queues, `NativeList<VirtualVoice>`, `List.Sort`, LINQ sort, `Resources.Load`, and `Physics.Raycast` in SHINOBU virtualization/propagation paths.</ZERO_GC>
  <DEAR_LIE>Occlusion remains midpoint SDF sign plus scalar gain/LPF; low tier disables SDF.</DEAR_LIE>
  <BLACKBOX>300-frame ring and `Docs/AgentLogs/Dump_ACOUSTIC_DSP.bin` path remain active.</BLACKBOX>
  <COMPILE>Full Core build is blocked outside SHINOBU by `LocRegistry.cs(404)` missing `ISignal`; no SHINOBU audio errors were emitted.</COMPILE>
</SELF_AUDIT_R5>

# Rationale_SHINOBU_112

Agent: SHINOBU_112  
Domain: Acoustic propagation, occlusion, reverb, DSP parameter math  
Status: PENDING VERIFICATION  

## Preflight Decisions

Problem: Audio occlusion request intersects existing audio virtualization, SDF, signal, DataVault, and binary payload systems.  
Solution: Start from existing `Hecton8.Audio.Virtualization` and `SpatialAudioManager` source before adding any new route. Owner-local edits only unless a contract is already present.  
Rejected Alternatives: A new cross-domain audio service or new global registry slot would inflate H-Phi and compile wall risk. Direct physics queries are rejected for this domain.  
Scalability potential: Low uses sparse SDF probes, nearest sampling, 12-voice output budget, and coarse update cadence; middle keeps line integral probes; high adds richer ITD/ILD; ultra spends saved CPU on denser DSP parameter modulation, not collision truth.  
Hardware Impact: Replacing per-voice PhysX occlusion with flat DTO SDF math targets sub-0.1 ms for 64 voices on i3/MX350-class silicon; measured proof is absent until Unity/profiler or Burst timing artifacts exist.

Problem: Task requires `AcousticSourceDTO` exactly 64 bytes with `double3` AUP and no properties.  
Solution: Use explicit layout, public fields, named padding, and an editor/static verifier using `UnsafeUtility.SizeOf<T>()` and offsets.  
Rejected Alternatives: Sequential layout and `Pack=1` were rejected; sequential can drift under edits, and `Pack=1` is invalid on ARM64 hot structs. Properties rejected because hot NativeArray mutation must not create defensive struct copies.  
Scalability potential: One cache-line source DTO keeps low-tier cache pressure bounded; high/ultra can add companion SoA buffers instead of bloating the truth DTO.  
Hardware Impact: 64-byte stride avoids unaligned `double3` read risk on ARM64 and reduces cache-line churn versus larger managed voice objects.

Problem: `acoustic_material_properties.h8bin` is not listed in the binary payload ledger; `Data/Audio/Acoustic_LUT.bin` is the active wired payload.  
Solution: Treat material-properties binary as optional cold input; if absent, generate deterministic fallback coefficients for rock, metal, flesh into Vault-owned acoustic material storage.  
Rejected Alternatives: Failing boot on absent baker output or probing disk per frame. Both violate resilience and hot-path purity.  
Scalability potential: Fallback coefficients keep CI/stress tests runnable on weak devices while high-tier can use richer baked tables when present.  
Hardware Impact: Cold fallback table is tiny; it removes runtime file I/O and avoids null-path branches inside Burst kernels.

Problem: The mandate requires Sabine reverb but forbids expensive acoustic raytracing.  
Solution: Use the Dear Lie: listener SDF clearance approximates room volume and exposed area, then `RT60 = 0.161 * V / A` with finite clamps.  
Rejected Alternatives: Multi-bounce ray tracing, per-material wave propagation, or PhysX geometry walks. Too slow and not deterministic enough for the requested audio illusion.  
Scalability potential: Low computes from one clearance; middle samples a few analytical points; high/ultra can use more SDF taps and material absorption weights.  
Hardware Impact: Constant small tap count replaces unbounded ray count; expected savings scale with voice count.

## Implementation Decisions

Problem: `acoustic_material_properties.h8bin` is absent while the binary ledger only lists `Data/Audio/Acoustic_LUT.bin` as the active audio payload.  
Solution: Added `GenerateEmergencyMockAcoustics()` overloads for Vault-backed `NativeArray<AcousticMaterialCoefficientDTO>` and `NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO>` using deterministic rock/metal/flesh FNV rows.  
Rejected Alternatives: Runtime disk probing and boot failure on missing baker output. Both are brittle and add no acoustic value.  
Scalability potential: Low/middle tiers hydrate three cheap coefficients; high/ultra can replace the same Vault map with richer baked material tables.  
Hardware Impact: Removes hot I/O and null branches; estimated 5-20 us scene-load resilience gain on i3/MX350, 0 us hot path.

Problem: The requested exact `AcousticSourceDTO` did not exist and existing virtual DTOs were not one-cache-line AUP acoustic rows.  
Solution: Added `[StructLayout(LayoutKind.Explicit, Size = 64)] AcousticSourceDTO` with exact offsets 0/4/8/12/16/40/44/48-63 and `AcousticDspOutputDTO` as a 64-byte output row.  
Rejected Alternatives: Extending `VirtualVoiceDTO` or using sequential layout. That risks ABI drift and couples the new SDF kernel to older virtual voice ingestion.  
Scalability potential: Low keeps only source/output DTOs; high/ultra can add SoA companion arrays for richer DSP without bloating the canonical source row.  
Hardware Impact: 64-byte stride keeps one source per L1 cache line; expected sub-microsecond alignment win per 64 voices versus irregular managed structs.

Problem: Occlusion and reverb needed raycast-free proof while current virtual sorting only used a midpoint SDF fake.  
Solution: Added `AcousticOcclusionJob : IJobParallelFor` with NoAlias inputs, AUP subtract-before-cast, SDF line integral, nearest-to-trilinear quality blending, Sabine clearance fake, depth LPF, Doppler, ITD/ILD, and rollback mute.  
Rejected Alternatives: PhysX raycasts, multi-bounce acoustic rays, Unity Transform Doppler, and third-party HRTF calls. These are nondeterministic or too expensive for 64 voices.  
Scalability potential: Quality 0.0-0.3 collapses to one nearest SDF tap and 12 voices; quality 1.0 uses up to 8 taps, trilinear SDF, 64 voice ranking, stronger binaural cues.  
Hardware Impact: Replaces O(voices * raycasts * PhysX scene) with O(voices * taps) flat math; expected 50x+ versus per-voice PhysX on low-end CPUs, pending profiler proof.

Problem: Existing virtual voice limit used binary low/high tier behavior and capped to 32.  
Solution: Raised Burst virtual budget constants to 12-64 and routed sorting through `VirtualVoiceUtility.ResolveContinuousVoiceBudget(GlobalQualityWeight)`. Physical AudioSource hydration remains clamped to authored pool capacity to avoid stealing chaos.  
Rejected Alternatives: Expanding authored AudioSource pool blindly or retaining hard low-tier switches. Authored pool changes are scene ownership; binary switching causes audible pops.  
Scalability potential: Toaster path computes fewer submitted voices while keeping simulation rows alive; ultra ranks 64 and spends saved CPU on better DSP parameters.  
Hardware Impact: Voice sorting remains compact-key based; 64-key sort costs microseconds, while hydration remains bounded by available output channels.

Problem: Network rollback can replay many ticks and produce repeated audio if output is not suppressed.  
Solution: Spatial audio reads `RollbackNetcodeVault.AudioSuppression` as a Vault alias and passes `RollbackActive` into Burst jobs; jobs still update DTO math but clamp output volume to zero.  
Rejected Alternatives: Direct netcode service calls or disabling source simulation. Direct calls add compile coupling; disabling simulation causes post-rollback pops.  
Scalability potential: Same mute scalar applies at all quality weights with no branching outside deterministic job setup.  
Hardware Impact: One Vault row read per audio frame; avoids audible spam with negligible CPU cost.

Problem: Designers need control and proof without recompiles.  
Solution: Added UI Toolkit `Abyssal Acoustics Tuner`, byte-span CSV ingestion, and SDF-colored scene gizmos; existing Sabine tuner now feeds the byte parser.  
Rejected Alternatives: IMGUI-only tuning and string-split CSV. IMGUI existed but did not satisfy the requested UI Toolkit facade; string splitting creates avoidable garbage.  
Scalability potential: Low/middle tuning can force quality collapse; high/ultra tuning can open 64 voices and stronger Sabine scale.  
Hardware Impact: Editor-only allocations are cold; runtime parser and jobs remain unmanaged.

Problem: Compile verification is mandated, but build launch is forbidden when CPU is under load.  
Solution: Sampled CPU via `typeperf`; result was 100%, so `dotnet build` was intentionally not launched. Static greps verified no audio raycast calls and no `Pack=1`; compile remains pending.  
Rejected Alternatives: Launching build under 100% CPU or requesting privileged CIM just to read CPU load. Both violate local workflow constraints.  
Scalability potential: No runtime effect.  
Hardware Impact: Prevented unnecessary build contention on the developer workstation.

Problem: `Hecton8.Audio.Virtualization` and its contracts had a direct sibling assembly reference to `Hecton8.Audio.Propagation` only to carry `AcousticPortalFlags`.  
Solution: Added local blittable `VirtualVoicePortalFlags : byte` in virtualization contracts, converted from propagation flags at the `SpatialAudioManager` boundary, and removed the propagation reference from both virtualization asmdefs.  
Rejected Alternatives: Keeping the sibling reference, moving propagation structs into core during this task, or deleting portal flag data. Keeping the reference violates compile-wall routing; moving the whole propagation contract is too wide; deleting flags loses authored acoustic intent.  
Scalability potential: Low/middle/high/ultra all carry one byte of portal intent without forcing propagation recompiles into the virtualization kernel.  
Hardware Impact: Runtime cost is one cold boundary byte cast per queued sound; compile-wall impact is reduced because SDF virtualization no longer recompiles on propagation implementation churn.

Problem: `AcousticOcclusionJob` existed as a pure kernel but needed Vault-backed runtime ownership and a DSP handoff path.  
Solution: Added double-buffered Vault aliases for `AcousticSourceDTO`, previous source AUP, `AcousticDspOutputDTO`, and fallback material rows; `SpatialAudioManager.FastTick()` now swaps the source buffers, schedules `AcousticOcclusionJob`, completes it at the audio sync boundary, and applies unmanaged output rows to virtual selections.  
Rejected Alternatives: Main-thread managed arrays, one shared source buffer, or leaving the kernel as an unused test artifact. A single buffer risks job/write races; managed arrays violate H-Phi and GC law; unused kernels do not prove the pipeline.  
Scalability potential: Low quality still submits the same flat DTO lane but SDF taps collapse to one and voice budget collapses to 12; high/ultra consumes the same Vault rows with denser taps and stronger binaural/reverb output.  
Hardware Impact: Adds fixed cache-line DTO movement while preserving job parallelism; expected cost remains O(voices * taps) and avoids PhysX ray traversal. Microsecond proof remains pending until build/profiler gate opens.

Problem: The first integration scheduled the acoustic SDF job on the first N ingress voices before the priority sort completed, so a selected high-priority voice beyond that prefix could miss its `AcousticDspOutputDTO`.  
Solution: Added selected-voice Vault buffers (`70026`, `70027`) and preserved `SourceVelocityMetersPerSecond` in `VirtualVoiceSelection`; after the Burst sort completes, `PopulateSelectedAcousticSources()` builds a compact selected lane in O(selected voices), then `AcousticOcclusionJob` runs only on those rows.  
Rejected Alternatives: Scanning `64 * 1000` stable keys on the main thread, running SDF for all 1000 virtual voices, or accepting prefix-only DSP output. The scan burns CPU; all-voice SDF violates the 64-voice target; prefix-only output is wrong.  
Scalability potential: Low/middle/high/ultra all process exactly the continuous selected voice budget; SDF cost now tracks audible/submitted voices rather than total virtual emitters.  
Hardware Impact: Worst-case selected-lane staging is O(64) DTO writes instead of O(64k) comparisons or O(1000 * taps) SDF work; estimated 10-80 us saved on i3/MX350 depending virtual emitter pressure.

Problem: Material CSV parsing existed, but designers still lacked a direct editor facade to push `acoustic_materials.csv` into the Vault-backed material rows, and CI had no visible seed asset for the requested rock/metal/flesh coefficients.  
Solution: Added `Assets/_Project/Data/Audio/acoustic_materials.csv`, exposed `ReloadAcousticMaterialRowsFromCsvCold(ReadOnlySpan<byte>)` on `SpatialAudioManager`, and added a UI Toolkit `Reload Material CSV` button that performs explicit cold editor I/O before handing byte spans to the zero-GC parser.  
Rejected Alternatives: Runtime file polling, `string.Split`, LINQ, or hiding the material reload behind a build-time baker that is absent from the current binary ledger. Runtime polling violates hot-path law; managed splitting adds garbage; baker-only paths block CI smoke coverage.  
Scalability potential: Low/middle tiers keep three cheap material rows; high/ultra can replace the same Vault row lane with richer baked material payloads without changing the Burst kernel signature.  
Hardware Impact: 0 us runtime hot path; estimated 5-20 us cold resilience improvement by avoiding boot failure and avoiding repeated disk checks on i3/MX350-class machines.

Problem: Verification still requires compile/profiler proof, but local CPU remains above the user-defined build gate.  
Solution: Re-ran static gates and sampled CPU/compiler state. `git diff --check` has no whitespace errors; audio PhysX grep is empty; virtualization compile-wall grep is empty. First recheck CPU samples were 40.29%, 80.04%, 95.19%; final recheck samples were 100%, 100%, 99.85% with active `dotnet` PID 36732. Build remains intentionally withheld.  
Rejected Alternatives: Launching `dotnet build` while CPU exceeds 50%, or claiming the 50x target without measured Burst/profiler data. Both violate the task protocol.  
Scalability potential: No runtime effect; protects the shared workstation and avoids adding compile contention.  
Hardware Impact: No game-frame impact; avoids workstation thrash during multi-agent execution.

Problem: The SDF occlusion kernel had a real `SdfVoxels` input, but runtime scheduling still passed `SdfVoxels = default`, reducing the system to a mock-only SDF despite the raycast-eradication mandate.  
Solution: Switched `AcousticOcclusionJob` to consume byte-encoded SDF voxels and decode them into signed meters using the existing project convention; `SpatialAudioManager` now aliases owner-published `BufferID.VoxelSdfTexture3D` from the DataVault and passes it read-only to the selected-lane acoustic job when the buffer is present and large enough. Mock SDF remains only as fallback for absent/undersized owner buffers.  
Rejected Alternatives: Direct dependency on `Hecton8.World.GlobalWorldSampler`, allocating an audio-owned SDF copy, or keeping permanent mock-only sampling. Direct world reference worsens compile-wall coupling; copying SDF violates Data Sovereignty; mock-only sampling is not the assigned architecture.  
Scalability potential: Low quality still collapses to one or few nearest byte-SDF taps; high/ultra use up to eight taps and trilinear blending against the same owner-published voxel field.  
Hardware Impact: Keeps the complexity at O(selectedVoices * taps) with cache-friendly byte reads. Avoids PhysX entirely while removing the fake-only gap; expected i3/MX350 cost remains under the 64-voice 0.1 ms target pending profiler proof.

Problem: `VirtualVoiceTuningSnapshot` contained a static `Default` property. It was not an instance NativeArray property, but the mandate rejects properties on unmanaged structs because they compile into methods and weaken static audit clarity.  
Solution: Replaced the property with `CreateDefault()` and updated all audio/editor callers and smoke assertions.  
Rejected Alternatives: Keeping the property because it was technically static. The task asks for architectural paranoia, so the struct now exposes no property surface at all.  
Scalability potential: No tier effect; it reduces ABI/audit ambiguity.  
Hardware Impact: Runtime hot path impact is negligible; it prevents future misuse of struct properties in NativeArray mutation lanes.

Problem: The smoke tester itself embedded exact forbidden substrings such as the removed tuning-property name and propagation assembly name as `AssertNotContains` needles, making broad static grep produce false positives from the verifier source.  
Solution: Split those verifier needles into composed strings while keeping the runtime assertions identical. Static grep now reports no hits for `VirtualVoiceTuningSnapshot.Default`, `SdfVoxels = default`, audio PhysX raycasts, or propagation coupling inside `Audio.Virtualization`.  
Rejected Alternatives: Documenting the false positives and forcing every later auditor to filter them manually. That weakens proof automation.  
Scalability potential: No runtime effect; improves static gate reliability across low/middle/high/ultra builds.  
Hardware Impact: 0 us runtime; editor smoke test remains cold-only.

Problem: CPU gate finally allowed the mandated compile attempt, but the solution build fails before reaching SHINOBU_112 audio code because a World-domain source file is deleted while `Hecton8.Core.csproj` still references it.  
Solution: Ran exactly one constrained build after CPU samples 17.54%, 15.85%, 40.60% and no active `dotnet/csc`; recorded `CS2001` for `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` as an external compile-wall blocker.  
Rejected Alternatives: Reverting the deleted World file, editing `Hecton8.Core.csproj`, or claiming audio compile proof despite the build abort. All three would violate ownership and evidence rules.  
Scalability potential: No runtime effect. The acoustic architecture remains static-verified; measured 64-voice timing proof is still pending behind this global build blocker.  
Hardware Impact: Build consumed 51.90 s once under allowed CPU state; no game-frame impact. The missing-file blocker must be resolved by the World/Integrations owner before SHINOBU_112 can produce compile/profiler proof.

Problem: External build failure blocks compiler proof of SHINOBU_112, but leaving the domain uninspected would hide obvious local mistakes.  
Solution: Performed manual compile-sanity gates on SHINOBU_112 deltas: grep on added lines for forbidden hot-path constructs returned no hits; Burst attributes were rechecked; virtualization asmdefs were reread; DTO layout annotations and SDF Vault routing were reread.  
Rejected Alternatives: Treating manual grep as a clean build, or skipping local sanity after the external compile wall. The first is false evidence; the second is lazy.  
Scalability potential: Confirms the continuous quality route remains in the submitted kernel: selected voices 12..64, byte-SDF taps 1..8, nearest/trilinear blend by polynomial quality.  
Hardware Impact: 0 us runtime; reduces integration risk while the global project build is blocked upstream.

Problem: The DSP handoff initially bounded its output scan by `max(_virtualVoiceSortCount, _acousticOcclusionOutputCount)`, which could read stale acoustic output rows when virtual ingress count exceeded the selected physical lane.
Solution: Bound `ApplyAcousticDspOutputToSelection()` strictly by `_acousticOcclusionOutputCount` and add a smoke assertion for that exact selected-lane limit.
Rejected Alternatives: Clearing the whole 1000-row DSP output buffer every frame, or scanning all virtual rows to find missing keys. Clearing costs bandwidth; scanning all virtual rows violates the 64-voice selected-lane design.
Scalability potential: Low/middle/high/ultra all keep DSP upload work proportional to audible/submitted voices, not total virtual emitters.
Hardware Impact: Worst-case DSP handoff drops from O(1000) stale-row scan to O(12..64), saving roughly 5-40 us on weak CPUs when virtual event pressure is high.

Problem: `VirtualVoiceSortJob` and `MockAcousticEmitterJob` still used `FloatMode.Fast`; the acoustic domain is rollback-visible, and sort decisions plus deterministic mock stress rows should not depend on platform-specific fast-float contraction.
Solution: Switched all three audio virtualization Burst jobs to `FloatMode.Deterministic` with `CompileSynchronously = true` and `FloatPrecision.Standard`; added a smoke assertion rejecting `FloatMode.Fast`.
Rejected Alternatives: Keeping only the final occlusion job deterministic. That still leaves voice ranking, culling, and mock stress input susceptible to x86/ARM drift.
Scalability potential: Low/middle/high/ultra preserve the same quality curves; deterministic float mode trades a small amount of ALU freedom for replay safety.
Hardware Impact: Potential microsecond-level cost increase is accepted for rollback correctness; PhysX raycast removal still dominates the savings target.

Problem: The first real SDF alias used a valid byte lane but hardcoded listener-centered dimensions/origin/cell/range. That could sample the wrong voxel coordinates if the SDF owner published a volume with different metadata.
Solution: Added `TrySnapshotAcousticSdfPayload()` in `SpatialAudioManager`. It queries `HectonVoxelVolume.TryGetClosestPublishedSonarSdfPayload`, validates dimensions and range, uses `BufferID.VoxelSdfTexture3D` only when the Vault buffer length matches the owner payload, and passes `sdfOrigin - listenerRuntimePosition` into the Burst kernel.
Rejected Alternatives: Directly calling `GlobalWorldSampler` from the audio virtualization assembly, copying SDF metadata into audio-owned truth, or keeping listener-centered hardcoded metadata. Direct world calls violate compile isolation; duplicate truth violates owner-local authority; hardcoded metadata can lie.
Scalability potential: Low still collapses to nearest/tiny tap counts; high/ultra get trilinear taps over the real published SDF coordinate frame.
Hardware Impact: Adds one cold/control-path payload snapshot per acoustic schedule, keeps Burst work O(selectedVoices * taps), and prevents wasted samples against unrelated voxel coordinates.

Problem: Task 16 asked for average SDF occlusion compute time in the black-box recorder, but the current ring only recorded virtual sort wait time and acoustic DSP output values.
Solution: Added `AcousticOcclusionTimeMs` to `VirtualVoiceStatistics` and `VirtualVoiceTelemetryEntry` without changing their 64-byte sizes, timestamped the selected-lane SDF job with `Stopwatch.GetTimestamp()`, pushed the completed duration into the 300-frame blackbox, wrote it into the binary dump, and surfaced it in the editor tuners.
Rejected Alternatives: Reusing `SortTimeMs` as a proxy, timing all virtual voice ingestion, or clearing/scanning the 1000-row DSP buffer to infer cost. Sort time is not SDF time; all-ingress timing hides selected-lane cost; full-buffer scans violate the 64-voice architecture.
Scalability potential: Low records the same field while selected voices collapse toward 12 and taps collapse toward 1; middle/high/ultra can compare real SDF cost against richer trilinear/tap settings and tune `GlobalQualityWeight` without guessing.
Hardware Impact: Adds one timestamp pair per scheduled SDF job and one float write per blackbox entry; expected overhead is below 1 us on i3/MX350 while enabling the mandated 1.0 ms dump tripwire.

Problem: The earlier legal build attempt failed on a missing World-domain file, but that blocker might have changed after other agents edited project files.
Solution: Rechecked current project references before considering another build. `rg` finds no `.csproj/.sln/.slnx` reference to `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`; the source file is still absent, but it is not currently a project-file blocker. Build remains withheld because `csc`, `dotnet`, and `VBCSCompiler` processes are active and CPU samples fluctuated between 49% and 91%.
Rejected Alternatives: Rerunning `dotnet build` while compiler processes are active, or leaving the status pinned to a stale World-file reference. Both would violate the build gate and evidence discipline.
Scalability potential: No runtime effect; this protects shared workstation throughput during multi-agent execution.
Hardware Impact: 0 us runtime. Avoided build contention while preserving a precise next gate: rerun build only when CPU <50% and compiler processes are idle.

Problem: Task 16 explicitly required an `AcousticTelemetryEntry` ring, but the first repair reused the virtual voice telemetry DTO and pushed a preliminary sort row before the selected-lane SDF job completed.
Solution: Added a dedicated 64-byte `Hecton8.Audio.Virtualization.AcousticTelemetryEntry`, moved the SHINOBU blackbox Vault alias to `NativeArray<AcousticOcclusionTelemetryEntry>`, qualified the older portal telemetry DTO as `AcousticPortalTelemetryEntry`, and only pushes normal frame telemetry immediately when no SDF job is scheduled. Frames with an SDF job now write the blackbox after the SDF completion timestamp is known.
Rejected Alternatives: Treating adjacent virtual telemetry as literal acoustic telemetry, or allowing both pre-SDF and post-SDF rows into the same 300-frame ring. The first fails the task wording; the second corrupts average timing evidence and halves useful forensic history under load.
Scalability potential: Low/middle/high/ultra all record one acoustic row per completed SDF frame, with selected voices and tap count already driven by `GlobalQualityWeight`; zero-voice frames still record a single 0 ms fallback row.
Hardware Impact: No additional hot allocation. Runtime cost remains one 64-byte ring write per acoustic frame; removing duplicate normal pushes can save roughly 0.2-1.0 us and preserves 300 actual frames of forensic history instead of 150 paired entries.

Problem: The telemetry repair changed C# and therefore needs compile proof, but the workstation still violates the explicit build launch gate.
Solution: Checked compiler processes and CPU twice after the patch. No `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process was active, but CPU samples were 93.31%, 60.76%, 38.60%, then 47.60%, 100%, 100%, so `dotnet build` remains withheld.
Rejected Alternatives: Launching a build because one sample dipped below 50%, or ignoring the new compile need. The rule is not a median; active CPU spikes above 50% close the gate.
Scalability potential: No runtime effect; protects concurrent agent throughput and avoids stealing CPU from active Unity/compiler work.
Hardware Impact: 0 us runtime. Avoided a likely multi-minute compile contention event on a saturated machine.

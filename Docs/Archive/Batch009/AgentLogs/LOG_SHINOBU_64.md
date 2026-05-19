# LOG_SHINOBU_64

## Session Start
What was wrong: No SHINOBU_64 status/rationale/log existed for this batch; `CURRENT_BATCH.md` also contains duplicate `SHINOBU_64` prompt IDs.
What was done: Created fresh SHINOBU_64 tracking files and selected the volcanic prompt by role, not by ID alone.
Cinematic Cheats used: Chose deterministic vector-injection planning over real boiling-water simulation.
Exact Microseconds saved: PENDING VERIFICATION; no runtime profiling yet.

## Volcanic Updraft Implementation Audit - 2026-05-18
What was wrong: Project had no deterministic volcanic updraft director for the active SHINOBU_64 volcanic prompt. Existing geyser-style behavior relied on generic Unity-side physics patterns and did not provide Vault-owned 64-byte DTOs, direct submarine velocity injection, quality-scaled debris culling, CSV tuning, or blackbox telemetry for geyser math.

What was done: Added `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs`, `Assets/_Project/Scripts/Editor/VolcanicUpdraftTunerWindow.cs`, and a `SubmarineDynamicsRuntime` hook that schedules volcanic vector injection after the submarine 6D integrator. Added generated project includes so Core/Editor builds see the new files.

Cinematic Cheats used: Replaced physical boiling-water simulation with deterministic cone/cylinder math, triangle-wave eruption interference, `DynamicWakeDTO` / `MockFlowField` presentation scalars, acoustic/seismic/debris command signals, and editor wire cylinders. The CPU computes gameplay truth only; shaders/VFX carry the water violence.

Exact Microseconds saved: Estimated 30-200 us per active geyser cluster versus Unity broadphase volume/trigger/Rigidbody dispatch. Low-quality debris culling removes O(debris * vents) intersection tests; with 64 debris and 8 vents this avoids up to 512 cylinder checks per fixed tick. No profiler capture was run, so these are engineering estimates, not measured frame timings.

<SELF_AUDIT agent_id="SHINOBU_64" role="THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR">
  <twenty_task_reconciliation>
    <task id="01" status="PASS">Archive scan found no volcanic_vent_locations.h8bin; cold 64-byte little-endian reader and GenerateEmergencyMockVents fallback implemented.</task>
    <task id="02" status="PASS">No WindZone or ConstantForce in SHINOBU files; Burst math writes velocity and force arrays directly.</task>
    <task id="03" status="PASS">VentStateDTO has public fields only; no hot DTO property accessors.</task>
    <task id="04" status="PASS">VentStateDTO and related DTOs are explicit 64-byte layouts with no Pack=1.</task>
    <task id="05" status="PASS">partial MockSubmarineArray plus mock entity job proves blind vector injection.</task>
    <task id="06" status="PASS">Cone/cylinder solver injects into submarine LinearVelocity and LinearForceWorld; player/mock/leviathan lanes also receive bounded lift.</task>
    <task id="07" status="PASS">Eruption timers use Burst triangle-wave interference plus deterministic hash phase; seismic signal emitted on peaks.</task>
    <task id="08" status="PASS">Player heat and blindness are scalar Vault/signal fakes for shader distortion.</task>
    <task id="09" status="PASS">DebrisSpawnSignal is emitted for erupting vents when quality curve allows debris lift.</task>
    <task id="10" status="PASS">Leviathan steering output is lifted and VolcanicFloatStateSignal is written; no nonexistent velocity buffer was invented.</task>
    <task id="11" status="PASS">GlobalQualityWeight drives SmoothStep debris lift collapse below 0.3 and turbulence ALU collapse below the same gate.</task>
    <task id="12" status="PASS">AcousticPingSignal produces rocket-engine roar metadata.</task>
    <task id="13" status="PASS">Cylinder math subtracts double3 AUP first and then casts the local delta to float3.</task>
    <task id="14" status="PASS">Thermodynamics heat is injected through GlobalRegistry service, not direct sibling ownership.</task>
    <task id="15" status="PASS">Submarine vertical drag is reduced by 90 percent while inside the updraft.</task>
    <task id="16" status="PASS">VentStateDTO Vault lane uses NativeArrayOptions.UninitializedMemory.</task>
    <task id="17" status="PASS">300-frame telemetry ring and Dump_VOLCANO_SURGEON.bin NaN dump path implemented.</task>
    <task id="18" status="PASS">Volcanic Updraft Tuner EditorWindow added with required sliders.</task>
    <task id="19" status="PASS">volcanic_vents.csv parser hashes keys from Vault byte scratch without split, regex, or LINQ.</task>
    <task id="20" status="PASS">Editor SceneView and runtime selected gizmos draw blue/red updraft cylinders.</task>
  </twenty_task_reconciliation>

  <struct_layout_verification>
    <struct name="VentStateDTO" size_bytes="64" alignment="8">
      <field name="AUP" offset="0" size="24" type="double3" />
      <field name="UpVector" offset="24" size="12" type="float3" />
      <field name="Radius" offset="36" size="4" type="float" />
      <field name="ThrustPower" offset="40" size="4" type="float" />
      <field name="EruptionTimer" offset="44" size="4" type="float" />
      <field name="_pad0" offset="48" size="4" type="uint" />
      <field name="implicit_alignment_gap" offset="52" size="4" type="padding" />
      <field name="_pad1" offset="56" size="8" type="ulong" />
      <math>24 + 12 + 4 + 4 + 4 + 4 + 4 + 8 = 64 bytes, exactly one L1 cache line.</math>
    </struct>
    <struct name="VolcanicUpdraftSettingsDTO" size_bytes="64">12 floats at offsets 0-44 plus 4 uints at 48-60 equals 64 bytes.</struct>
    <struct name="VolcanicUpdraftTelemetryEntry" size_bytes="64">double3 0-23, float3 24-35, float 36-39, uints 40-47, four ushorts 48-55, uint flags 56-59, uint pad 60-63.</struct>
    <struct name="VolcanicUpdraftFrameCounter" size_bytes="64" layout="Explicit">Counters occupy 0-31, PrimaryVentAup occupies 32-55, Flags 56-59, pad 60-63; one counter per cache line to prevent false sharing.</struct>
  </struct_layout_verification>

  <scalability_curve>
    GlobalQualityWeight is read from HomeostasisBrain each fixed tick. Below 0.3, debris lift weight is zero, so debris indices skip vent-cylinder intersections and only set a culled flag. The updraft vector solver still performs the cheap cylinder containment test for massive bodies, but turbulenceGate is SmoothStep(0.3, 1.0, weight), so weak devices return strict +Y after intensity calculation and bypass tangent/twist turbulence ALU. From middle to ultra, turbulence, visual wakes, particle budgets, acoustic/seismic intensity, and debris chimney commands rise continuously through math.lerp, math.step-style gates, and polynomial SmoothStep curves.
  </scalability_curve>

  <h_phi_vault_status persistent_private_native_arrays="0">
    <buffer id="70750" name="VolcanicUpdraft.Vents" />
    <buffer id="70751" name="VolcanicUpdraft.Settings" />
    <buffer id="70752" name="VolcanicUpdraft.Telemetry300" />
    <buffer id="70753" name="VolcanicUpdraft.MockSubmarines" />
    <buffer id="70754" name="VolcanicUpdraft.MockLeviathans" />
    <buffer id="70755" name="VolcanicUpdraft.FloatSignals" />
    <buffer id="70756" name="VolcanicUpdraft.DynamicWakes" />
    <buffer id="70757" name="VolcanicUpdraft.MockFlowField" />
    <buffer id="70758" name="VolcanicUpdraft.CsvScratchBytes" />
    <buffer id="70759" name="VolcanicUpdraft.FrameCounters" />
    <buffer id="70760" name="VolcanicUpdraft.MockDebris" />
    <buffer id="70761" name="VolcanicUpdraft.PlayerHeat" />
  </h_phi_vault_status>

  <pointer_aliasing_and_dependency_graph>
    <noalias>All Burst job NativeArray fields in VolcanicUpdraftDirector use NoAlias with ReadOnly where applicable.</noalias>
    <job name="VolcanicCountersResetJob" consumes="pending vent readers" outputs="counter reset handle" />
    <job name="VolcanicEruptionCycleJob" consumes="counter reset handle" outputs="vent write handle published through VolcanicUpdraftVault" />
    <job name="VolcanicSubmarineUpdraftInjectionJob" consumes="submarine integrator handle and pending vent write handle" outputs="submarine integrator/updraft handle and pending vent read handle" />
    <job name="Mock/Player/Leviathan/Visual/Telemetry jobs" consumes="eruption handle chain" outputs="VolcanicUpdraftDirector._jobHandle" />
  </pointer_aliasing_and_dependency_graph>

  <compile_guard>
    No asmdef reference to a sibling runtime assembly was added. The project currently uses explicit generated csproj compile lists; these were updated only so the new runtime/editor files compile in local verification. The controlled submarine hook stays inside the existing monolithic Core assembly and does not create a new assembly edge.
  </compile_guard>

  <dear_lie_confirmation>
    Before: real boiling water, trigger volumes, Rigidbody force routing, and particle collisions would be O(bodies * broadphase + particles * collisions) with nondeterministic Unity scheduling. After: gameplay truth is O(entities * activeVents), and weak hardware collapses debris to O(debris) flagging with no vent intersections. Visual violence is pushed as DynamicWakeDTO, MockFlowField, acoustic, seismic, debris, and heat scalars for GPU/shader systems.
  </dear_lie_confirmation>

  <verification>
    Core build command succeeded with 0 errors. Editor build command succeeded with 0 warnings and 0 errors. Static banned-pattern scan was clean for SHINOBU files.
  </verification>
</SELF_AUDIT>

## Rollback Netcode ARM64 Lockstep DTO Bottom Recheck - 2026-05-19
What was wrong: The rollback files were clean, but the state surface they copy was not. `LockstepPlayerKinematicState` and adjacent lockstep replay/hash DTOs in `LockstepStateValidator.cs` used `Pack=1`, and rollback snapshots `LockstepPlayerKinematicState` bytes directly. That is ARM64 alignment debt inside the rollback validation surface, not an unrelated cosmetic issue.

What was done: Removed packed layout from the lockstep validator DTOs and hash job structs while keeping explicit sizes where replay ABI needs them. Added edit-test offset guards for `LockstepPlayerKinematicState`, `LockstepReplayInputFrame`, and `LockstepReplayBlockHeader`. Upgraded lockstep hash jobs to deterministic synchronous Burst and added `[NoAlias]` on their `NativeArray` fields.

Cinematic Cheats used: No transform sync was introduced. The simulation still uses input-only lockstep plus exact byte snapshots; the visible correction remains the AUP-local interpolation lie.

Exact Microseconds saved: No measured number claimed. The structural gain is removing packed-layout unaligned access risk from player-state hash/snapshot data. Build after this polish is deferred by guard sample `CPU=100.0; CSC=0; DOTNET=0`.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="arm64_lockstep_dto_bottom_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Fallback mock netcode remains deterministic; current `CURRENT_BATCH.md` no longer exposes the XML prompt, so disk status/rationale remain the active assignment record.</task>
    <task id="02" status="PASS">No RPC or `NetworkTransform`; authority is input DTOs plus hashes.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` stays public-field and ref-mutated; lockstep player state now has aligned sequential layout without `Pack=1`.</task>
    <task id="04" status="PASS">Rollback and lockstep DTOs in the rollback state surface are 8/16-byte aligned; `LockstepPlayerKinematicState` remains 96 bytes.</task>
    <task id="05" status="PASS">`MockTickCommand` still emits SIMULATION|POST_SIMULATION only.</task>
    <task id="06" status="PASS">Snapshots still use `UnsafeUtility.MemCpy` plus XXHash3-64 over exact bytes.</task>
    <task id="07" status="PASS">Remote input mismatch still compares against predicted journal frames.</task>
    <task id="08" status="PASS">Restore still uses `UnsafeUtility.MemCpy` into vault state.</task>
    <task id="09" status="PASS">Journal correction and headless resim command remain wired.</task>
    <task id="10" status="PASS">Visual correction remains AUP-local: one anchor plus two local `float3` vectors.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` still throttles rollback through continuous curves.</task>
    <task id="12" status="PASS">60-frame hash fence compares XXHash3-64 and triggers blackbox response.</task>
    <task id="13" status="PASS">AUP truth remains exact-byte hashed; local visual deltas are post-anchor only.</task>
    <task id="14" status="PASS">Audio suppression DTO marks resim windows.</task>
    <task id="15" status="PASS">MODP quarantine excludes mod-only input from authority.</task>
    <task id="16" status="PASS">Fully overwritten buffers use `UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame telemetry and dump path remain wired.</task>
    <task id="18" status="PASS">Editor tuner facade remains present.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch and zero-LINQ.</task>
    <task id="20" status="PASS">200 ms ping simulation and gizmos visualize local correction space.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <LockstepPlayerKinematicState size="96" alignment="8">0 SectorX long(8); 8 SectorY long(8); 16 SectorZ long(8); 24 LocalPosition float3(12); 36 Velocity float3(12); 48 Forward float3(12); 60 Frame uint(4); 64 Flags uint(4); 68 InputActions uint(4); 72 StableId uint(4); 76 HashCadenceFrames uint(4); 80..95 four uint reserved lanes. 96 % 16 = 0.</LockstepPlayerKinematicState>
    <LockstepReplayInputFrame size="48" alignment="8">0 Frame uint(4); 4 ActionsBitmask uint(4); 8 MoveDelta float2(8); 16 LookDelta float2(8); 24 VerticalDelta float(4); 28 CurrentInputSchemeHash uint(4); 32 Flags; 36 Sequence; 40 Reserved0; 44 Reserved1. 48 % 16 = 0.</LockstepReplayInputFrame>
    <LockstepReplayBlockHeader size="128" alignment="16">0 Magic ulong(8); 8 Version; 12 HeaderSizeBytes; 16 StartFrame; 20 HashFrame; 24 InputCount; 28 Flags; 32 MasterHash ulong(8); 40..87 uint hash/count/mask lanes; 88..127 five ulong reserved lanes. 128 % 16 = 0.</LockstepReplayBlockHeader>
    <VisualStateDTO size="64" alignment="16">0 AnchorAupAbsolute double3(24); 24 TrueLocalMeters float3(12); 36 InterpolatedLocalMeters float3(12); 48 Blend01; 52 BlendStep01; 56 EntityId; 60 Flags. 64 % 16 = 0.</VisualStateDTO>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, rollback depth still eases toward 22 percent of max with `math.lerp`/`Smooth01`, look-only rollback is gated by `math.step`, and visual work is a fixed local `float3` lerp. The ARM64 DTO polish changes layout safety only; it does not introduce a quality branch.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Rollback vault IDs unchanged: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed/created 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Rollback fixed path still outputs one registered `RollbackFixedPipelineJob`; lockstep hash jobs now use `[NoAlias]` on Source/ElementHashes/ElementFlags/ArrayHashes/MasterHash/MasterFlags.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge was added. There is no `Hecton8.Networking.Runtime.asmdef`; rollback files live in existing `Hecton8.Core`, and this pass did not add sibling references.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform correction or absolute rubber band. After: O(players) input sync plus exact local rollback; visible correction remains local interpolation.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No `Pack=1`/`StructLayout(...Pack...)` remains in rollback networking or `LockstepStateValidator.cs`.</static_scan>
    <static_scan status="PASS">Rollback runtime still has no `.Run()`, `.Complete()`, forced barrier, RPC, `NetworkTransform`, stale absolute visual fields, hot DTO properties, LINQ, debug logging, or `UnityEngine.Random`.</static_scan>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched after this polish because guard sampled `CPU=100.0; CSC=0; DOTNET=0`.</build>
  </verification>
</SELF_AUDIT>

## Rollback Netcode AUP-Local Visual DTO Recheck - 2026-05-19
What was wrong: The dispatcher audit was still carrying the earlier visual DTO model: true/interpolated absolute `double3` positions. That presentation shape is mathematically suspicious for a 100 km world because the editor/render lane can accidentally convert absolute AUP values into `Vector3` and recreate jitter.

What was done: Converted visual correction state to an AUP anchor plus local meters: `AnchorAupAbsolute`, `TrueLocalMeters`, `InterpolatedLocalMeters`. `RollbackFixedPipelineJob` subtracts the pre-rollback anchor in double precision once, sanitizes the local delta, and stores only local `float3` correction vectors for presentation. The editor gizmo draws the red/green correction vector in local space, not clamped absolute world coordinates. Edit tests now assert the 64-byte DTO offsets through the final `Flags` field and verify `LocalMetersFromAnchor()`.

Cinematic Cheats used: Simulation truth remains exact MemCpy rollback. Presentation is still a deliberate lie: a local vector blend hides the correction instead of network-transform rubber-banding.

Exact Microseconds saved: No profiler measurement claimed. The structural saving is avoiding absolute-world float conversion and preserving the 16-slot fixed visual buffer. Latest build was not launched because guard sampled `CPU=97.9; CSC=0; DOTNET=0`.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="aup_local_visual_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Archive scan for `netcode_latency_profiles.h8bin` still falls back to deterministic emergency mock.</task>
    <task id="02" status="PASS">No RPC, ClientRpc, ServerRpc, `OnSerializeNetworkView`, or `NetworkTransform`; authority is input DTOs and hashes.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` has public fields, 24-byte layout, and ref mutation via `UnsafeUtility.AsRef` access.</task>
    <task id="04" status="PASS">Snapshot pages are 8-byte aligned; no `[StructLayout(Pack=1)]` in rollback networking.</task>
    <task id="05" status="PASS">`MockTickCommand` remains partial and emits SIMULATION|POST_SIMULATION only.</task>
    <task id="06" status="PASS">Snapshot path uses `UnsafeUtility.MemCpy` and full XXHash3-64 over exact bytes.</task>
    <task id="07" status="PASS">Input mismatch compares remote received input against the predicted input journal.</task>
    <task id="08" status="PASS">Restore path rewinds authoritative vault arrays from the state ring with `UnsafeUtility.MemCpy`.</task>
    <task id="09" status="PASS">Remote input correction overwrites the journal and emits headless resim commands without visual sync.</task>
    <task id="10" status="PASS">Visual correction is now AUP-local: anchor `double3` plus true/interpolated local `float3` vectors.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` throttles rollback depth through `math.lerp`, `math.step`, and `Smooth01`.</task>
    <task id="12" status="PASS">60-frame hash fence compares local/remote XXHash3-64 and triggers pause/dump/full-state overwrite marker.</task>
    <task id="13" status="PASS">AUP hash path preserves exact 64-bit coordinate bytes; presentation delta is derived after anchor subtraction.</task>
    <task id="14" status="PASS">Audio suppression DTO marks headless resim windows.</task>
    <task id="15" status="PASS">MODP quarantine flags prevent mod-only input from becoming hash authority.</task>
    <task id="16" status="PASS">State/input/command/visual/CSV buffers use `UninitializedMemory` where fully overwritten.</task>
    <task id="17" status="PASS">300-frame telemetry ring and `Dump_NETCODE_SURGEON.bin` remain wired.</task>
    <task id="18" status="PASS">Editor tuner facade remains active.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch based with no split/regex/LINQ.</task>
    <task id="20" status="PASS">200 ms ping simulation and red/green scene gizmos remain present, now in local correction space.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24" alignment="8">0: FrameHash64 ulong(8); 8: InputMaskP1 uint(4); 12: InputMaskP2 uint(4); 16: MemoryOffset uint(4); 20: Reserved0 uint(4). Total 24; 24 % 8 = 0.</FrameSnapshotDTO>
    <VisualStateDTO size="64" alignment="16">0: AnchorAupAbsolute double3(24); 24: TrueLocalMeters float3(12); 36: InterpolatedLocalMeters float3(12); 48: Blend01 float(4); 52: BlendStep01 float(4); 56: EntityId uint(4); 60: Flags uint(4). Total 64; 64 % 16 = 0; one L1 cache line.</VisualStateDTO>
    <NetcodeTelemetryEntry size="80" alignment="16">0: FrameHash64 ulong(8); 8: RemoteHash64 ulong(8); 16..68: uint/float telemetry lanes; 72: Reserved2 ulong(8). Total 80; 80 % 16 = 0.</NetcodeTelemetryEntry>
    <false_sharing status="PASS">No concurrent atomic counter DTO was introduced. Visual corrections and telemetry are fixed records, not adjacent atomics.</false_sharing>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, rollback scan depth eases toward the 0.22 floor, look-only rollback is rejected by the scalar `math.step` gate, and presentation work remains one bounded local `float3` lerp per active visual correction. Button/move mismatches still roll back because they are simulation truth.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed/created 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes the dispatcher dependency and outputs one `RollbackFixedPipelineJob` handle registered through `H8Memory.RegisterActiveJob`. Job fields use `[NoAlias]` and read-only inputs use `[ReadOnly, NoAlias]`. Internal order: detect mismatch, restore snapshot, apply input correction, emit headless command, write visual correction, snapshot, hash fence, telemetry.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No asmdef edge, no sibling runtime direct dependency, no new core rollback enum. AUP-local visual polish touched only rollback/editor/test/docs files.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform sync or absolute-position rubber banding. After: O(players) input sync plus local exact-state rollback; the visible correction is an AUP-local interpolation fake.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No stale `TrueAupAbsolute` or `InterpolatedAupAbsolute` references remain.</static_scan>
    <static_scan status="PASS">Runtime rollback files have no `.Run()`, `.Complete()`, forced barrier, RPC, `NetworkTransform`, packed structs, hot DTO properties, LINQ conversions, debug logging, or `UnityEngine.Random`.</static_scan>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched after this polish because guard sampled `CPU=97.9; CSC=0; DOTNET=0`.</build>
  </verification>
</SELF_AUDIT>

## Rollback Netcode Dispatcher Pipeline Recheck - 2026-05-19
What was wrong: The prior rollback runtime still used a local `ExecutePostSimulationBarrier<TJob>()` path. It scheduled jobs, registered handles, then forced completion from inside the rollback owner. That was better than `.Run()`, but still violated the Native Memory Jobs rule against Schedule+Complete inside tick-like runtime code.

What was done: Replaced the rollback owner interface from `IPostFixedTickable` to `IDispatcherFixedSystem`. `ScheduleFixedSimulation()` now emits one `RollbackFixedPipelineJob` handle into the master fixed bridge. That single deterministic Burst job performs input mismatch detection, snapshot restore, remote input correction, headless resim command emission, MemCpy snapshot, 64-bit hash fence, and 300-frame telemetry write. `PostFixedSimulation()` only performs cold side effects after the dispatcher completion window: pause signal, blackbox dump, and remote hash overwrite marker. Late visual smoothing is a fixed 16-slot zero-GC loop, not a fake job barrier.

Cinematic Cheats used: The core Dear Lie is unchanged and now cleaner: network truth is input-only lockstep, while player-facing correction is a red/green visual interpolation fake over three frames. No transform authority, no RPC, no `NetworkTransform`, no physics GameObject replay.

Exact Microseconds saved: 0 measured microseconds claimed. No profiler capture was run in this pass. Structural saving: three to four forced main-thread job completion points were removed from rollback frames and delegated to the dispatcher fixed completion window. Low-quality rollback still scans about 22 percent of a configured 120-frame window, cutting roughly 94 candidate frames before resim.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="dispatcher_pipeline_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Archive scan found no `netcode_latency_profiles.h8bin`; deterministic emergency mock remains active.</task>
    <task id="02" status="PASS">No RPC, `OnSerializeNetworkView`, or `NetworkTransform`; remote authority is input DTOs and hashes only.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` uses public fields and ref mutation via `UnsafeUtility.AsRef`; no hot properties.</task>
    <task id="04" status="PASS">Snapshot pages use 8-byte aligned header+payload stride.</task>
    <task id="05" status="PASS">`MockTickCommand` is partial, 16 bytes, and emits SIMULATION|POST_SIMULATION without VISUAL_SYNC.</task>
    <task id="06" status="PASS">`StateSnapshotJob` copies hot state with `UnsafeUtility.MemCpy` and hashes exact bytes with full XXHash3-64.</task>
    <task id="07" status="PASS">`DetectInputMismatchJob` compares remote received input against predicted journal frames.</task>
    <task id="08" status="PASS">`RestoreSnapshotJob` restores authoritative vault buffers with `UnsafeUtility.MemCpy`.</task>
    <task id="09" status="PASS">`ApplyRemoteInputCorrectionJob` and `HeadlessResimulationCommandJob` resim the truth lane only.</task>
    <task id="10" status="PASS">`VisualStateDTO` holds true/interpolated AUP and blends presentation without mutating simulation truth.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` continuously throttles rollback depth and gates look-only rollback.</task>
    <task id="12" status="PASS">60-frame hash fence compares local/remote XXHash3-64 and triggers pause/dump/full-state overwrite marker.</task>
    <task id="13" status="PASS">`HashExactAupDouble3()` hashes exact 24-byte `double3` payloads.</task>
    <task id="14" status="PASS">`RollbackAudioSuppressionDTO` marks resim frames for audio suppression.</task>
    <task id="15" status="PASS">MODP quarantine flags skip modded input frames from desync authority.</task>
    <task id="16" status="PASS">State, input, command, visual, and CSV scratch vault buffers use `NativeArrayOptions.UninitializedMemory` where clearing is not required.</task>
    <task id="17" status="PASS">`NetcodeTelemetryEntry[300]` ring and `Dump_NETCODE_SURGEON.bin` blackbox dump are wired.</task>
    <task id="18" status="PASS">`Rollback Netcode Tuner` editor facade exposes rollback knobs.</task>
    <task id="19" status="PASS">`netcode_profiles.csv` parser tokenizes bytes from vault scratch without split/regex/LINQ.</task>
    <task id="20" status="PASS">Editor ping simulation and red true/green interpolated scene gizmos are present.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24" alignment="8">0 ulong FrameHash64(8); 8 uint InputMaskP1(4); 12 uint InputMaskP2(4); 16 uint MemoryOffset(4); 20 uint Reserved0(4). Total 24, 24 % 8 = 0.</FrameSnapshotDTO>
    <StatePageHeaderDTO size="64" alignment="64">0 ulong FrameHash64(8); 8 Frame; 12 PayloadBytes; 16 RigidbodyAupCount; 20 PlayerStateCount; 24 EntityAupCount; 28 EntityVelocityCount; 32 RoomWaterCount; 36 Flags; 40 MemoryOffset; 44 ModQuarantineMask; 48 Reserved0; 52 Reserved1; 56 Reserved2; 60 Reserved3. Total 64.</StatePageHeaderDTO>
    <RollbackRuntimeStateDTO size="80" alignment="16">0 LastFrameHash64(8); 8 LastRemoteHash64(8); 16 CurrentFrame; 20 LastRollbackFrame; 24 LastRemoteFrame; 28 LastMismatchFrame; 32 FramesResimulated; 36 RollbacksTriggered; 40 ResimComputeTimeMs; 44 GlobalQualityWeight; 48 MismatchSeverity01; 52 Flags; 56 StateSnapshotBytes; 60 StateMemoryOffset; 64 DesyncCount; 68 Reserved0; 72 Reserved1(8). Total 80, 80 % 16 = 0.</RollbackRuntimeStateDTO>
    <VisualStateDTO size="64" alignment="16">0 TrueAupAbsolute double3(24); 24 InterpolatedAupAbsolute double3(24); 48 Blend01; 52 BlendStep01; 56 EntityId; 60 Flags. Total 64.</VisualStateDTO>
    <NetcodeTelemetryEntry size="80" alignment="16">0 FrameHash64(8); 8 RemoteHash64(8); 16 Frame; 20 LastRollbackFrame; 24 RollbacksTriggered; 28 FramesResimulated; 32 ResimComputeTimeMs; 36 GlobalQualityWeight; 40 Flags; 44 InputMaskP1; 48 InputMaskP2; 52 StateMemoryOffset; 56 SnapshotBytes; 60 MismatchFrame; 64 Reserved0; 68 Reserved1; 72 Reserved2(8). Total 80, 80 % 16 = 0.</NetcodeTelemetryEntry>
  </struct_layout_verification>
  <scalability_curve>
    `ResolveBudgetedRollbackFrames()` uses `math.step`, `math.lerp`, and `Smooth01`. Below `GlobalQualityWeight` 0.3, rollback depth eases toward 22 percent of max and look-only rollback is rejected by a scalar gate; button/move mismatches stay authoritative. At 1.0 the full configured rollback depth is used and visual interpolation gets richer, still outside simulation truth.
  </scalability_curve>
  <h_phi_vault_status persistent_private_native_collections="0">
    The runtime owns zero private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields. Boot requests `VaultBufferHandle` IDs: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; it borrows or creates existing 70521 ShinobuInputJournalRing.
  </h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>
    `ScheduleFixedSimulation()` consumes the dispatcher fixed `dependsOn` handle and outputs one `RollbackFixedPipelineJob` handle registered with `H8Memory.RegisterActiveJob`. The job fields use `[NoAlias]` and `[ReadOnly, NoAlias]` where applicable. Internal deterministic order: DetectInputMismatch -> RestoreSnapshot -> ApplyRemoteInputCorrection -> HeadlessResimulationCommand -> StateSnapshot -> CheckRemoteHashFence -> WriteTelemetry. `PostFixedSimulation()` consumes the completed dispatcher window and emits only cold pause/dump side effects.
  </pointer_aliasing_and_dependency_graph>
  <compile_guard>
    No asmdef reference was added, no sibling runtime assembly edge was introduced, and rollback buffer IDs remain domain-local constants rather than new core enum members.
  </compile_guard>
  <dear_lie_confirmation>
    Before: O(networked_entities) transform replication and correction. After: O(players) input sync plus O(snapshot_payload_bytes + rollback_window) local MemCpy/hash/resim command work. The visual correction is an interpolation lie; the physics truth is exact restored state.
  </dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No forced rollback barrier, `.Complete()`, runtime `.Run()`, RPC, `NetworkTransform`, packed structs, hot DTO properties, LINQ conversions, `string.Format`, debug logging, `UnityEngine.Random`, or direct `SystemID.Networking`/`BufferID.ShinobuRollback` in rollback networking files.</static_scan>
    <build status="PASS_WITH_EXTERNAL_WARNINGS">`dotnet build Hecton8.Core.csproj --no-restore /m:1` launched after guard `CPU=36.5; CSC=0; DOTNET=0` and succeeded. Remaining 8 warnings are external `CS0649` in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`.</build>
    <diff_check status="PASS">Manual trailing-whitespace scan on untracked rollback/docs files is clean; `git diff --check` reported no tracked whitespace diagnostics.</diff_check>
  </verification>
</SELF_AUDIT>

## Rollback Netcode AUP-Local Visual DTO Bottom Recheck - 2026-05-19
What was wrong: The latest rollback audit still described visual correction as absolute `double3` presentation state. That is a precision leak: debug/render code can convert absolute AUP into `Vector3` and reintroduce large-world jitter.

What was done: `VisualStateDTO` now stores `AnchorAupAbsolute` plus `TrueLocalMeters` and `InterpolatedLocalMeters`. The rollback pipeline subtracts the pre-rollback anchor once in double precision, stores sanitized local `float3` deltas, and the editor gizmo draws the local correction vector.

Cinematic Cheats used: Exact-state MemCpy rollback remains the truth. The visible correction is a local interpolation fake, not transform authority.

Exact Microseconds saved: No profiler number claimed. Fixed 16-slot local vector blend remains the hot presentation cost. Build after this polish is deferred because the guard sampled `CPU=97.9; CSC=0; DOTNET=0`.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="aup_local_visual_bottom_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">No archive `netcode_latency_profiles.h8bin`; emergency mock path remains deterministic.</task>
    <task id="02" status="PASS">No RPC or `NetworkTransform`; input DTOs and hashes are the network authority.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` uses public fields and ref mutation through `UnsafeUtility.AsRef` access.</task>
    <task id="04" status="PASS">Snapshot pages are 8-byte aligned; no packed rollback DTOs.</task>
    <task id="05" status="PASS">`MockTickCommand` is partial and excludes VISUAL_SYNC from resim.</task>
    <task id="06" status="PASS">State snapshot uses `UnsafeUtility.MemCpy` and full XXHash3-64.</task>
    <task id="07" status="PASS">Remote input mismatch is checked against the predicted input journal.</task>
    <task id="08" status="PASS">Snapshot restore uses `UnsafeUtility.MemCpy` into vault-owned state arrays.</task>
    <task id="09" status="PASS">Input correction overwrites journal frames and emits headless resim command.</task>
    <task id="10" status="PASS">Visual correction is AUP-local: one absolute anchor and two local `float3` vectors.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` continuously throttles rollback depth and look-only rollback.</task>
    <task id="12" status="PASS">60-frame hash fence compares local/remote XXHash3-64 and triggers blackbox response.</task>
    <task id="13" status="PASS">Exact AUP hashing remains byte-true; local visual deltas are derived after anchor subtraction.</task>
    <task id="14" status="PASS">Audio resim suppression DTO remains wired.</task>
    <task id="15" status="PASS">MODP quarantine excludes mod-only input from hash authority.</task>
    <task id="16" status="PASS">Fully overwritten vault buffers use `UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame telemetry and `Dump_NETCODE_SURGEON.bin` remain wired.</task>
    <task id="18" status="PASS">Editor tuner facade remains available.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch, no split/regex/LINQ.</task>
    <task id="20" status="PASS">200 ms ping simulation and scene gizmos remain available in local correction space.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24" alignment="8">0 ulong FrameHash64(8); 8 uint InputMaskP1(4); 12 uint InputMaskP2(4); 16 uint MemoryOffset(4); 20 uint Reserved0(4). 24 % 8 = 0.</FrameSnapshotDTO>
    <VisualStateDTO size="64" alignment="16">0 double3 AnchorAupAbsolute(24); 24 float3 TrueLocalMeters(12); 36 float3 InterpolatedLocalMeters(12); 48 float Blend01(4); 52 float BlendStep01(4); 56 uint EntityId(4); 60 uint Flags(4). 64 % 16 = 0.</VisualStateDTO>
    <NetcodeTelemetryEntry size="80" alignment="16">0 ulong FrameHash64(8); 8 ulong RemoteHash64(8); 16..68 telemetry uint/float lanes; 72 ulong Reserved2(8). 80 % 16 = 0.</NetcodeTelemetryEntry>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, rollback depth eases toward 22 percent of max with `Smooth01` and `math.lerp`; look-only rollback is gated by `math.step`. Visual work stays a fixed local `float3` lerp, while button/move mismatches keep exact rollback.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed/created 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes dispatcher dependency and outputs one registered `RollbackFixedPipelineJob`; fields use `[NoAlias]` and read-only inputs use `[ReadOnly, NoAlias]`.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge, sibling runtime reference, or core rollback enum was added.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform correction or absolute position rubber banding. After: O(players) input sync plus exact local rollback; the user sees a local interpolation lie.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No stale absolute visual fields remain.</static_scan>
    <static_scan status="PASS">Runtime rollback scan has no `.Run()`, `.Complete()`, forced barrier, RPC, `NetworkTransform`, packed structs, hot DTO properties, LINQ, debug logging, or `UnityEngine.Random`.</static_scan>
    <diff_check status="PASS">Scoped `git diff --check` returned clean.</diff_check>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched because guard sampled `CPU=97.9; CSC=0; DOTNET=0`.</build>
  </verification>
</SELF_AUDIT>

## Volcanic Updraft Polish Recheck - 2026-05-18
What was wrong: `ThermalGeyser` had already been converted away from Unity physics, but its fixed-tick bridge still performed a live `VolcanicUpdraftDirector.ActiveRuntimeInstance` lookup before publishing the authored cave vent. That was not a force nondeterminism bug, but it was still a hot global access.

What was done: Added a cold `_volcanicDirector` cache in `ThermalGeyser` and refresh it only from `Awake`, `OnEnable`, `Start`, and `Configure`. `FixedTick` now publishes cave geyser AUP/radius/thrust/height/phase through the cached director pointer only. Re-ran the SHINOBU static scans against `VolcanicUpdraftDirector`, `ThermalGeyser`, `SubmarineDynamicsRuntime`, and `VolcanicUpdraftTunerWindow`: no WindZone, ConstantForce, UnityEngine.Physics, Rigidbody, ForceMode, UnityEngine.Random, LINQ, foreach, Pack=1, hot DTO properties, or hot NativeArray/List/HashMap allocation appeared.

Cinematic Cheats used: Physical boiling water remains a mathematical lie: a 64-byte vent DTO, AUP-local cylinder/cone test, direct velocity/force injection, mock wake/flow scalar emission, heat/blindness scalar signal, and editor gizmo cylinders. No mesh collider water, no WindZone, no particle physics as truth.

Exact Microseconds saved: Removed one static director lookup per active cave geyser fixed tick after cold wiring; sub-microsecond per marker. The large savings remain the broadphase removal from old `ThermalGeyser` and quality-weighted debris culling below `GlobalQualityWeight < 0.3`.

Build Evidence: Guard allowed one Core build (`CPU=19.8/22.9/20.4`, no active `dotnet/csc`). Build failed outside volcanic ownership: `Assets/_Project/Scripts/Construction/ConstructionSignals.cs(13,47)` and `(36,42)` cannot resolve `ISignal`. Log: `Temp/shinobu64_core_polish4.log`.

<SELF_AUDIT agent_id="SHINOBU_64" pass="volcanic_updraft_polish_recheck">
  <twenty_task_reconciliation>
    <task id="01" status="PASS">Archive scan found no `volcanic_vent_locations.h8bin`; emergency mock vents remain the fallback.</task>
    <task id="02" status="PASS">No WindZone, ConstantForce, UnityEngine.Physics, Rigidbody, ForceMode, or PhysicsForceRouter remains in SHINOBU volcanic files.</task>
    <task id="03" status="PASS">`VentStateDTO` uses public fields only; no hot accessor properties.</task>
    <task id="04" status="PASS">`VentStateDTO` is 64 bytes with manual padding and no Pack=1.</task>
    <task id="05" status="PASS">`MockSubmarineArray` exists as a 64-byte partial mock for blind submarine proof.</task>
    <task id="06" status="PASS">Burst cylinder/cone kernel injects `ThrustPower * UpVector` into external force/velocity buffers.</task>
    <task id="07" status="PASS">Eruption oscillator updates timer/thrust and publishes seismic/acoustic scalar signals.</task>
    <task id="08" status="PASS">Thermal blindness is a scalar/signal fake, not polygonal water simulation.</task>
    <task id="09" status="PASS">Debris chimney commands are emitted; debris lift is quality-gated.</task>
    <task id="10" status="PASS">Leviathan thermal riding writes updraft influence and float-state signal data.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` continuously culls small debris lifting below 0.3.</task>
    <task id="12" status="PASS">Acoustic roar tap is emitted from eruption peak state.</task>
    <task id="13" status="PASS">Cylinder math subtracts vent AUP first and evaluates in local `float3` space.</task>
    <task id="14" status="PASS">Thermodynamics heat injection uses a cached `IThermodynamicsService`, not a LateFrame registry poll.</task>
    <task id="15" status="PASS">Vertical drag is reduced by 0.1x for entities inside active updraft columns.</task>
    <task id="16" status="PASS">Vent and hot buffers use `NativeArrayOptions.UninitializedMemory` where fully overwritten.</task>
    <task id="17" status="PASS">300-frame volcanic telemetry ring and dump path are wired for NaN/forensic faults.</task>
    <task id="18" status="PASS">`Volcanic Updraft Tuner` EditorWindow exists for max thrust, frequency, radius, and heat.</task>
    <task id="19" status="PASS">`volcanic_vents.csv` parser uses byte/token parsing without LINQ/split/regex in gameplay.</task>
    <task id="20" status="PASS">Editor gizmo visualizer draws active geyser cylinders blue/red.</task>
  </twenty_task_reconciliation>
  <struct_layout_verification>
    <struct name="VentStateDTO" size_bytes="64" alignment_policy="8-byte-safe-no-Pack1">
      <field name="AUP" offset="0" size="24" type="double3" />
      <field name="UpVector" offset="24" size="12" type="float3" />
      <field name="Radius" offset="36" size="4" type="float" />
      <field name="ThrustPower" offset="40" size="4" type="float" />
      <field name="EruptionTimer" offset="44" size="4" type="float" />
      <field name="_pad0" offset="48" size="4" type="uint" />
      <field name="implicit_alignment_gap" offset="52" size="4" type="compiler gap before ulong" />
      <field name="_pad1" offset="56" size="8" type="ulong" />
      <math>24 + 12 + 4 + 4 + 4 + 4 + 4 + 8 = 64; 64 % 16 = 0; one L1 cache line.</math>
    </struct>
    <false_sharing status="PASS">`VolcanicUpdraftFrameCounter` is `LayoutKind.Explicit, Size=64`; telemetry entries are 64 bytes.</false_sharing>
  </struct_layout_verification>
  <scalability_curve_explanation>
    Below `GlobalQualityWeight` 0.3, debris lift collapses through a smooth gate toward zero and turbulence collapses to strict +Y. Expensive tangent/twist math is bypassed when the polynomial turbulence gate is effectively zero. Submarines, players, and leviathans still receive deterministic vertical truth; small debris is allowed to ignore the geyser on weak devices.
  </scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_arrays="0">
    <buffer id="70750" name="VolcanicUpdraftVault.VentStates" />
    <buffer id="70751" name="VolcanicUpdraftVault.VentSettings" />
    <buffer id="70752" name="VolcanicUpdraftVault.MockSubmarines" />
    <buffer id="70753" name="VolcanicUpdraftVault.MockLeviathans" />
    <buffer id="70754" name="VolcanicUpdraftVault.DebrisParticles" />
    <buffer id="70755" name="VolcanicUpdraftVault.TelemetryRing" />
    <buffer id="70756" name="VolcanicUpdraftVault.FloatStateSignals" />
    <buffer id="70757" name="VolcanicUpdraftVault.PlayerHeatSignals" />
    <buffer id="70758" name="VolcanicUpdraftVault.MockFlowField" />
    <buffer id="70759" name="VolcanicUpdraftVault.DynamicWake" />
    <buffer id="70760" name="VolcanicUpdraftVault.CsvScratch" />
    <buffer id="70761" name="VolcanicUpdraftVault.FrameCounter" />
  </h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>
    <noalias status="PASS">Burst job array fields use `[NoAlias]`; read-only buffers use `[ReadOnly, NoAlias]`.</noalias>
    <job name="VolcanicEruptionCycleJob" consumes="VentStates,VentSettings" outputs="VentStates" />
    <job name="VolcanicSubmarineUpdraftInjectionJob" consumes="VentStates,VentSettings,SubmarineConfigs" outputs="SubmarineStates,SubmarineForces" />
    <job name="VolcanicPlayerUpdraftInjectionJob" consumes="VentStates,VentSettings" outputs="PlayerStates" />
    <job name="VolcanicLeviathanUpdraftInjectionJob" consumes="VentStates,VentSettings,LeviathanState" outputs="LeviathanSteeringOutput,FloatStateSignals" />
    <job name="VolcanicVisualFakesJob" consumes="VentStates,VentSettings" outputs="DebrisParticles,MockFlowField,DynamicWake,PlayerHeatSignals" />
  </pointer_aliasing_and_dependency_graph>
  <compile_guard>
    SHINOBU volcanic files did not add new asmdef edges. Current Core build failure is outside this domain in `ConstructionSignals.cs` unresolved `ISignal`.
  </compile_guard>
  <dear_lie_confirmation>
    Before: broadphase/geyser force callbacks and possible water-object physics scale with colliders and scene state. After: O(vents * tracked_hot_entities) AUP-local scalar math, with visual boiling delegated to wake/flow/heat shader data. Low tier removes debris lifting; high tier spends the saved CPU on turbulence and presentation scalars.
  </dear_lie_confirmation>
</SELF_AUDIT>

## Rollback Netcode Build Wall - 2026-05-18
What was wrong: After the CPU/build guard cleared, Core compilation was attempted and failed before rollback-specific diagnostics because `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` cannot resolve `ISignal`.

What was done: Recorded the exact compile wall and kept the fix boundary inside SHINOBU rollback/netcode. I did not patch Construction from this agent because the task domain is cooperative rollback netcode and the error is in a sibling construction domain.

Cinematic Cheats used: None. This is compile-wall triage, not runtime behavior.

Exact Microseconds saved: No runtime claim. The build attempt was launched only after guard cleared (`CPU=18.9; CSC=0; DOTNET=0`). Command: `dotnet build Hecton8.Core.csproj --no-restore /m:1`. Errors: `ConstructionSignals.cs(13,47)` and `(36,42)` missing `ISignal`.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="compile_wall_boundary">
  <rollback_static_status status="PASS">Networking scan remains clean for RPC/NetworkTransform/DontDestroyOnLoad/runtime new GameObject/job Run/Pack=1/hot DTO properties.</rollback_static_status>
  <build_status status="BLOCKED_BY_DEPENDENCY">Core build fails in `Hecton8.Construction.ConstructionSignals`, outside rollback/netcode ownership.</build_status>
  <next_integrator_action>Construction owner should import or expose `Hecton8.Core.Contracts.Signals.ISignal` for `ConstructionPreviewSignal` and `FloraExclusionSignal`, then rerun Core build.</next_integrator_action>
</SELF_AUDIT_RECHECK>

## Rollback Netcode 64-bit Hash Pass - 2026-05-19
What was wrong: The rollback lane was functionally input-only, but the desync fence still folded XXHash3 into 32 bits, stale docs reported a 16-byte `FrameSnapshotDTO`, and Core verification was blocked by small external compile-wall defects before the compiler could prove the netcode changes.

What was done: Promoted rollback hashes to full 64-bit XXHash3 (`FrameHash64`, `LastFrameHash64`, `LastRemoteHash64`, `RemoteHash64`), added `FullStateOverwriteRequested`, switched rollback budget/cost curves to `math.step` + polynomial `Smooth01`, added endian-safe legacy profile hydration, updated editor/tests/docs, and applied minimal external compile-wall imports needed for verification. Core build now passes.

Cinematic Cheats used: Still input-only lockstep. No RPC, no NetworkTransform, no transform correction. The visible correction is a 3-frame red/green math-vs-interpolated facade while simulation truth is restored by MemCpy and resim command emission.

Exact Microseconds saved: RPC/transform path remains eliminated; estimate is tens to hundreds of us per dense co-op frame depending on networked entity count. `GlobalQualityWeight=0.1` now collapses rollback scan budget to roughly 22 percent instead of full-depth scan. 64-bit hash widening adds negligible cadence cost: one 64-bit compare every 60 frames and +16 bytes in runtime/telemetry DTOs.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" build="PASS_WITH_EXTERNAL_WARNINGS">
  <task_reconciliation>
    <task id="01" status="PASS">Archive scan found no `netcode_latency_profiles.h8bin`; emergency mock netcode fallback marks runtime state.</task>
    <task id="02" status="PASS">Networking surface is input journal only; banned RPC/NetworkTransform scan is clean.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` uses public fields, ref mutation, and 64-bit hash storage.</task>
    <task id="04" status="PASS">Snapshot pages use 8-byte aligned stride over vault byte ring pages.</task>
    <task id="05" status="PASS">`MockTickCommand` emits SIMULATION|POST_SIMULATION only, never VISUAL_SYNC.</task>
    <task id="06" status="PASS">`StateSnapshotJob` copies hot arrays into `StateRingBuffer` via `UnsafeUtility.MemCpy` and hashes exact bytes.</task>
    <task id="07" status="PASS">`DetectInputMismatchJob` compares remote received input against predicted input journal frames.</task>
    <task id="08" status="PASS">`RestoreSnapshotJob` restores vault buffers with `UnsafeUtility.MemCpy`.</task>
    <task id="09" status="PASS">Input journal is overwritten R..Current; headless resim command skips visual sync.</task>
    <task id="10" status="PASS">`VisualStateDTO` stores true/interpolated AUP and blends presentation over configured frames.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` throttles rollback depth and look-only correction continuously.</task>
    <task id="12" status="PASS">60-frame hash fence compares 64-bit local/remote hashes, pauses, dumps, and requests full-state overwrite.</task>
    <task id="13" status="PASS">`HashExactAupDouble3()` consumes exact 24-byte `double3` payload and returns 64-bit XXHash3.</task>
    <task id="14" status="PASS">`RollbackAudioSuppressionDTO` flags resim frames for audio systems.</task>
    <task id="15" status="PASS">MODP-quarantined input frames are skipped from mismatch-driven rollback.</task>
    <task id="16" status="PASS">Large rollback buffers use `NativeArrayOptions.UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame `NetcodeTelemetryEntry` ring and `Dump_NETCODE_SURGEON.bin` record 64-bit hash/resim state.</task>
    <task id="18" status="PASS">EditorWindow `Rollback Netcode Tuner` exposes runtime tuning.</task>
    <task id="19" status="PASS">`netcode_profiles.csv` parser reads bytes into vault scratch without split/regex/LINQ.</task>
    <task id="20" status="PASS">Editor button simulates 200 ms ping; Scene gizmo draws red true math and green interpolated presentation.</task>
  </task_reconciliation>

  <struct_layout_verification>
    <dto name="FrameSnapshotDTO" size="24" alignment="8-byte multiple">
      <field offset="0" size="8" name="ulong FrameHash64" />
      <field offset="8" size="4" name="uint InputMaskP1" />
      <field offset="12" size="4" name="uint InputMaskP2" />
      <field offset="16" size="4" name="uint MemoryOffset" />
      <field offset="20" size="4" name="uint Reserved0" />
      <math>8+4+4+4+4 = 24; 24 % 8 == 0.</math>
    </dto>
    <dto name="StatePageHeaderDTO" size="64" alignment="64-byte cache line">
      <field offset="0" size="8" name="ulong FrameHash64" />
      <field offset="8" size="40" name="10 x uint frame/count/flags/memory/quarantine fields" />
      <field offset="48" size="16" name="4 x uint reserved padding" />
      <math>8+40+16 = 64; one L1 cache line.</math>
    </dto>
    <dto name="RollbackRuntimeStateDTO" size="80" alignment="16-byte multiple">
      <field offset="0" size="16" name="2 x ulong hash fields" />
      <field offset="16" size="56" name="uint/float runtime state fields" />
      <field offset="72" size="8" name="ulong Reserved1" />
      <math>16+56+8 = 80; 80 % 16 == 0.</math>
    </dto>
    <dto name="NetcodeTelemetryEntry" size="80" alignment="16-byte multiple">
      <field offset="0" size="16" name="2 x ulong hash fields" />
      <field offset="16" size="56" name="frame/resim/input/offset fields" />
      <field offset="72" size="8" name="ulong Reserved2" />
      <math>16+56+8 = 80; 80 % 16 == 0.</math>
    </dto>
    <false_sharing status="N/A">No concurrent atomic counters were added by rollback. Telemetry entries are fixed 80-byte records; writes are single-lane through the runtime.</false_sharing>
  </struct_layout_verification>

  <scalability_curve_explanation>
    Below `GlobalQualityWeight` 0.3, `ResolveBudgetedRollbackFrames()` uses an emergency floor around 22 percent and a `Smooth01` polynomial curve, so scan/resim depth sheds before weak hardware stutters. Look-only rollback is gated with `math.step(minQuality, weight)` and skipped under thermal pressure; button/move mismatches remain truth-critical. `EstimateResimulationCostMs()` lerps per-frame cost through the same polynomial curve, tripping the black-box dump earlier on low quality. No binary hardware tier branch was added.
  </scalability_curve_explanation>

  <h_phi_vault_status persistent_private_native_arrays="0">
    <owner_system>RollbackNetcodeVault.OwnerSystem = SystemID.CoreDeterminism</owner_system>
    <buffer id="70750" name="StateRingBuffer" options="UninitializedMemory" />
    <buffer id="70751" name="FrameSnapshots" options="UninitializedMemory" />
    <buffer id="70752" name="RuntimeState" options="ClearMemory" />
    <buffer id="70753" name="RemoteInputRing" options="UninitializedMemory" />
    <buffer id="70754" name="TickCommands" options="UninitializedMemory" />
    <buffer id="70755" name="VisualStates" options="UninitializedMemory" />
    <buffer id="70756" name="TelemetryRing" options="ClearMemory" />
    <buffer id="70757" name="Tuning" options="ClearMemory" />
    <buffer id="70758" name="AudioSuppression" options="ClearMemory" />
    <buffer id="70759" name="CsvScratch" options="UninitializedMemory" />
    <buffer id="70769" name="LatencyProfile" options="ClearMemory" />
    <borrowed_buffer id="70521" name="ShinobuInputJournalRing" />
  </h_phi_vault_status>

  <pointer_aliasing_and_dependency_graph>
    <noalias status="PASS">Rollback jobs use `[NoAlias]`; source arrays use `[ReadOnly, NoAlias]` where applicable.</noalias>
    <job name="DetectInputMismatchJob" consumes="PredictedJournal, RemoteInputRing, RuntimeState" outputs="RuntimeState" />
    <job name="RestoreSnapshotJob" consumes="StateRingBuffer" outputs="RigidbodyAups, PlayerStates, EntityAups, EntityVelocities, RoomWaterLevels, RuntimeState" />
    <job name="ApplyRemoteInputCorrectionJob" consumes="RemoteInputRing" outputs="PredictedJournal" />
    <job name="HeadlessResimulationCommandJob" consumes="RuntimeState" outputs="MockTickCommand, RollbackAudioSuppressionDTO, RuntimeState" />
    <job name="StateSnapshotJob" consumes="live vault hot arrays" outputs="StateRingBuffer, FrameSnapshotDTO, RuntimeState" />
    <job name="VisualStateBlendJob" consumes="VisualStates" outputs="VisualStates" />
    <handles>Each job is scheduled, registered with `H8Memory.RegisterActiveJob`, batched, and completed through `DispatcherJobSwap` inside the dispatcher post-fixed/late-frame barrier.</handles>
  </pointer_aliasing_and_dependency_graph>

  <compile_guard>
    No rollback asmdef or sibling runtime assembly dependency was added. Rollback buffer IDs remain domain-local constants, not core enum churn. External edits were limited to compile-wall imports required for build verification.
  </compile_guard>

  <dear_lie_confirmation>
    The fake is visual interpolation, not networked transform truth. Before: O(networked_entities) transform/RPC synchronization and physics rubber-banding. After: O(players) input DTO sync plus local O(snapshot_payload_bytes + rollback_window) MemCpy/hash/resim command work; presentation hides correction over three frames.
  </dear_lie_confirmation>

  <verification>
    <static_scan status="PASS">No RPC, NetworkTransform, OnSerializeNetworkView, ClientRpc, ServerRpc, runtime `new GameObject`, `DontDestroyOnLoad`, `Pack=1`, `UnityEngine.Random`, `SystemID.Networking`, or SHINOBU rollback core enum references in networking files.</static_scan>
    <diff_check status="PASS">`git diff --check` reports no whitespace errors; only CRLF normalization warnings on external files.</diff_check>
    <build status="PASS_WITH_WARNINGS">`dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeded. Warnings: 8 existing `CS0649` warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, outside rollback ownership.</build>
  </verification>
</SELF_AUDIT>

## Volcanic Updraft Bottom Recheck - 2026-05-18
What was wrong: The active user directive is volcanic updrafts, while the shared SHINOBU_64 log also contains rollback entries because `CURRENT_BATCH.md` has duplicate IDs. The latest volcanic polish additionally found one hot static bridge in `ThermalGeyser`.

What was done: Preserved the full volcanic self-audit above under `pass="volcanic_updraft_polish_recheck"` and appended this bottom entry for chronological integrity. `ThermalGeyser` now resolves `_volcanicDirector` in cold lifecycle methods and fixed tick reads that cached field only. SHINOBU volcanic static scan is clean for WindZone, ConstantForce, UnityEngine.Physics, PhysicsForceRouter, Rigidbody, ForceMode, UnityEngine.Random, LINQ, foreach, Pack=1, hot accessor DTOs, and hot NativeArray/List/HashMap allocation.

Cinematic Cheats used: Geysers remain AUP-local cylinder math plus velocity/force injection and shader-facing scalar wake/heat data. No Unity force component or physical boiling-water simulation is used as truth.

Exact Microseconds saved: One static director lookup removed per active cave geyser fixed tick after cold wiring; sub-microsecond per marker. Major retained savings: no broadphase overlap geyser physics, quality-weighted debris lift collapse below 0.3, and strict +Y vector collapse on low `GlobalQualityWeight`.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="volcanic_bottom_recheck">
  <active_prompt role="THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR" source="Docs/Tasks/CURRENT_BATCH.md:1292-1347" task_count="20" />
  <static_scan status="PASS">Banned Unity force paths, Pack=1, LINQ/foreach, UnityEngine.Random, hot DTO properties, and hot NativeArray allocation are absent from SHINOBU volcanic files.</static_scan>
  <hot_path_cache status="PASS">`ThermalGeyser.SubmitVolcanicDirectorVent()` uses `_volcanicDirector`; `ActiveRuntimeInstance` is read only from cold `ResolveRuntimeWiring()`.</hot_path_cache>
  <global_quality status="PASS">Small debris lift is continuously gated by `GlobalQualityWeight`, with collapse under 0.3 and no low/high hardware boolean.</global_quality>
  <struct_layout status="PASS">`VentStateDTO` remains 64 bytes: double3 24 + float3 12 + three floats 12 + uint 4 + implicit gap 4 + ulong 8.</struct_layout>
  <build_status status="BLOCKED_BY_DEPENDENCY">Legal Core build failed outside volcanic ownership in `Assets/_Project/Scripts/Construction/ConstructionSignals.cs`: unresolved `ISignal` at lines 13 and 36. Log: `Temp/shinobu64_core_polish4.log`.</build_status>
</SELF_AUDIT_RECHECK>

## Volcanic Compile-Wall Audit - 2026-05-18
What was wrong: The volcanic director still has a direct code dependency on `Hecton8.AI.Cognition` for real leviathan lift. That is not clean contract routing.

What was done: Verified the dependency and left it unchanged because Task 10 requires real leviathan updraft influence, the AI owner registers the vault buffers with `AlphaLeviathanCognitionState` / `AlphaLeviathanSteeringOutput`, and `Hecton8.Core.asmdef` already references `Hecton8.AI.Cognition`. The AI DTOs are `LayoutKind.Explicit` and not `Pack=1`. A local mirror would be a worse binary-contract lie unless the owner moves these DTOs into a contracts assembly.

Cinematic Cheats used: None in this audit. Runtime still uses the existing Dear Lie: scalar/force-vector injection, not physical fluid simulation.

Exact Microseconds saved: No runtime claim. This is risk containment: no second alias interpretation of the same vault memory and no new asmdef edge from this polish pass.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="volcanic_compile_wall_audit">
  <using_audit status="KNOWN_DEBT">`Hecton8.AI.Cognition` remains because actual leviathan buffers are typed by the AI owner. `Hecton8.Core.asmdef` already has the reference; this pass did not widen the assembly graph.</using_audit>
  <rejected_alias status="PASS">Local mirror DTOs were rejected because `GlobalDataVault` typed handles are safer when matched to the owner struct and AI has explicit layouts already.</rejected_alias>
  <contract_recommendation>Move Alpha Leviathan state/output DTOs to an AI contracts assembly in a coordinated owner pass; then SHINOBU volcanic can remove the concrete AI using without losing Task 10.</contract_recommendation>
</SELF_AUDIT_RECHECK>

## Volcanic Updraft Polish Recheck - 2026-05-18
What was wrong: A legacy cave `ThermalGeyser` still contained a Unity physics force path: overlap query, Rigidbody extraction, `ForceMode.Acceleration`, and `PhysicsForceRouter.QueueForce`. That left a second geyser truth source outside the volcanic Burst/Vault director.

What was done: Rewrote `Assets/_Project/Scripts/ThermalGeyser.cs` as an authored marker that submits AUP/radius/thrust/height/heat/phase to `VolcanicUpdraftDirector.TryUpsertAuthoredVent()`. The component no longer applies forces, no longer queries Unity Physics, and stamps its backing `CurrentVolume` flow strength to `0f` to avoid hidden vertical transport.

Cinematic Cheats used: Cave geysers now use the same Dear Lie as volcanic vents: authored markers feed scalar vent data; Burst jobs mutate submarine/player/leviathan/mock velocity lanes; VFX/audio/heat/debris systems receive scalar signals.

Exact Microseconds saved: Removes one `OverlapSphereNonAlloc` plus up to 24 collider/body branches and managed force queue calls per erupting cave geyser fixed tick. Estimated 10-80 us per active cave geyser depending on collider density; profiler proof still pending.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="legacy_geyser_force_excision">
  <legacy_unity_physics_path status="PASS">`ThermalGeyser.cs` grep is clean for OverlapSphere, UnityEngine.Physics, PhysicsForceRouter, Rigidbody, ForceMode, WindZone, and ConstantForce.</legacy_unity_physics_path>
  <single_physics_truth status="PASS">Physical vertical lift now routes through `VolcanicUpdraftDirector` and its Burst jobs. Cave geysers only upsert authored vent data.</single_physics_truth>
  <global_quality_weight status="PASS">Authored cave geysers inherit the same `GlobalQualityWeight` debris cull and turbulence collapse used by volcanic vents.</global_quality_weight>
  <compile_status status="BLOCKED_BY_DEPENDENCY">Latest Core build fails outside volcanic files: missing `HectonRollbackNetcodeRuntime` in `Networking/HectonNetworkManager.cs` and missing `WaterlineBreachSignal` in untracked `Core/Signals/SignalWardenRuntime.cs`. No ThermalGeyser/VolcanicUpdraft/SubmarineDynamics errors appear in `Temp/shinobu64_core_polish3.log`.</compile_status>
  <state_collision status="DOCUMENTED">Duplicate `SHINOBU_64` prompt IDs cause the rollback lane to overwrite shared Status/Rationale files. Volcanic mirrors were created at `Docs/Tasks/Status_SHINOBU_64_VOLCANIC_UPDRAFT.md` and `Docs/AgentLogs/Rationale_SHINOBU_64_VOLCANIC_UPDRAFT.md`.</state_collision>
</SELF_AUDIT_RECHECK>

## Rollback Netcode Final Reassertion - 2026-05-18
What was wrong: Concurrent duplicate `SHINOBU_64` volcanic logging appended after the netcode report. The active user directive for this session remains rollback netcode.

What was done: Reasserted final netcode status at the bottom of this shared log. Implemented input-only rollback runtime, `UnsafeUtility.MemCpy` state snapshots/restores, XXHash3 exact-byte hashing, remote input mismatch detection, simulation-only resim command, quality-weight rollback throttling, audio suppression flag, MODP quarantine lane, 300-frame telemetry dump, CSV tuning, editor tuner, 200 ms ping simulator, and red/green visual correction gizmos.

Cinematic Cheats used: No position replication. Determinism remains the lie; visual interpolation hides correction while simulation truth stays in the vault/state ring.

Exact Microseconds saved: ~11 MB cold clear avoided by uninitialized state ring. Runtime rollback cost is linear MemCpy/hash over actual payload bytes, not managed serialization. Low-tier rollback depth is continuously reduced to about 35 percent before look-only rollback is skipped. Compile remains pending because CPU guard stayed above 50 percent.

## Rollback Netcode Implementation - 2026-05-18
What was wrong: `HectonNetworkManager` was a placeholder with debug logging and transform/message-oriented TODOs. There was no rollback state ring, no input mismatch detector, no exact AUP hash path, no resim audio suppression, no 300-frame netcode black box, and no human-facing tuner for latency simulation.

What was done: Added `RollbackNetcodeContracts.cs` with aligned DTOs, `FrameSnapshotDTO` size 16, `MockTickCommand`, rollback tuning/runtime/audio/visual/telemetry DTOs, and Burst jobs for snapshot, restore, input correction, mismatch detection, simulation-only resim command emission, and visual smoothing. Added `HectonRollbackNetcodeRuntime.cs` to allocate vault buffers, use `NativeArrayOptions.UninitializedMemory` for state pages, copy state with `UnsafeUtility.MemCpy`, hash exact state bytes with XXHash3, compare 60-frame remote hashes, suppress audio during resim, dump `Docs/AgentLogs/Dump_NETCODE_SURGEON.bin`, and parse `netcode_profiles.csv` without split/regex/LINQ. Replaced the networking manager with an input-only facade. Added `Rollback Netcode Tuner` and edit tests for DTO layout, AUP double3 hashing, quality-weight mismatch behavior, and MemCpy snapshot/restore.

Cinematic Cheats used: The system does not replicate positions. It preserves the Dear Lie: remote authority is input state, math truth is local deterministic vault state, and visual error is hidden by a three-frame `VisualStateDTO` interpolation path. Weak hardware skips look-only rollback and reduces rollback depth through continuous `GlobalQualityWeight` instead of a binary quality switch.

Exact Microseconds saved: Avoids object-transform network correction and render/audio duplicate replay during rollback. The default state ring bypasses about 11 MB of cold zero-fill. Low-tier rollback scan budget is about 35 percent of configured depth. Fixed telemetry cost is one 64-byte write per frame. Hot-path MemCpy/hash cost remains payload-size dependent and requires profiler proof.

<SELF_AUDIT agent_id="SHINOBU_64" pass="lockstep_rollback_netcode">
  <prompt_disambiguation status="PASS">Selected `LOCKSTEP_ROLLBACK_NETCODE_ROUTER` from duplicate `SHINOBU_64` blocks.</prompt_disambiguation>
  <banned_networking_patterns status="PASS">`Assets/_Project/Scripts/Networking` scan is clean for transform sync and remote-call patterns.</banned_networking_patterns>
  <frame_snapshot_layout status="PASS">`FrameSnapshotDTO` is direct-field sequential 16-byte layout: hash, P1 input mask, P2 input mask, memory offset.</frame_snapshot_layout>
  <zero_gc_hot_path status="PASS">Hot rollback paths use vault/native arrays, `UnsafeUtility.MemCpy`, XXHash3, and Burst jobs. File IO is cold/fault/editor only.</zero_gc_hot_path>
  <global_quality_weight status="PASS">Rollback depth and look-only mismatch handling consume continuous `GlobalQualityWeight`.</global_quality_weight>
  <black_box status="PASS">`NetcodeTelemetryEntry[300]` and `Dump_NETCODE_SURGEON.bin` are wired.</black_box>
  <compile_status status="BLOCKED_BY_CPU_GUARD">No `dotnet build` launched. Repeated guard samples stayed above 50 percent CPU, including `CPU=100.0; BUILD_PROCS=0`. Static scans passed; compile remains pending by project rule.</compile_status>
</SELF_AUDIT>

## Volcanic Updraft Polish Recheck - Registry Hot-Path Excision - 2026-05-18
What was wrong: `VolcanicUpdraftDirector.PublishPresentationSignals()` still read `GlobalRegistry.ThermodynamicsService` inside the `LateFrameTick` chain while publishing heat sources. That is not a physics bug, but it is registry-as-bus rot.

What was done: Added a cached `IThermodynamicsService` field, populated it during cold enable, and rebound it through `IGlobalRegistryHotSwapRefListener` / `IGlobalRegistryHotSwapListener`. The LateFrame vent loop now uses `_thermodynamicsService` only.

Cinematic Cheats used: No new simulation. Heat injection remains a scalar thermodynamic fake attached to vent intensity; shaders and thermodynamics own the expensive presentation/heat diffusion response.

Exact Microseconds saved: Removes one registry property lookup per emitted vent heat packet. Estimated <1 us per LateFrame in normal vent counts; profiler proof pending. The main value is compile-wall and hot-path dependency discipline.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="thermodynamics_registry_hot_path">
  <registry_hot_path status="PASS">`LateFrameTick` no longer reads `GlobalRegistry.ThermodynamicsService`; it uses the cached `_thermodynamicsService` pointer.</registry_hot_path>
  <rebinding status="PASS">`VolcanicUpdraftDirector` implements `IGlobalRegistryHotSwapRefListener` and `IGlobalRegistryHotSwapListener` so service replacement updates the cached pointer without per-frame polling.</rebinding>
  <static_scan status="PASS">Forbidden-pattern scan remains clean for WindZone, ConstantForce, UnityEngine.Physics, Rigidbody, ForceMode, PhysicsForceRouter, UnityEngine.Random, LINQ, hot foreach, Pack=1, hot DTO properties, and hot NativeArray allocation in SHINOBU files.</static_scan>
  <build_status status="BLOCKED_BY_CPU_GUARD">Fresh build launch skipped because `Get-Counter` sampled CPU 100/100/100 and active compiler processes were present (`dotnet=1`, `csc=1`). Previous global Core compile failure remains unrelated to volcanic files and is recorded in `Temp/shinobu64_core_polish3.log`.</build_status>
</SELF_AUDIT_RECHECK>

## Rollback Netcode Bottom Report - 2026-05-18
What was wrong: The shared `SHINOBU_64` log is contested by a duplicate volcanic role. This bottom entry is the current rollback-netcode report for the user-directed task.

What was done: Implemented the lockstep rollback lane in `Assets/_Project/Scripts/Networking`: input journal correction, `UnsafeUtility.MemCpy` state snapshot/restore, XXHash3 exact-byte hash checks, `GlobalQualityWeight` rollback throttle, headless SIMULATION|POST_SIMULATION command, audio suppression DTO, MODP quarantine marker, 300-frame telemetry, desync dump, CSV tuning, editor tuner, 200 ms ping simulator, and red/green correction gizmos.

Cinematic Cheats used: No position replication. The deterministic input-only lie is preserved, and visual smoothing hides correction over a configurable three-frame window.

Exact Microseconds saved: About 11 MB of cold ring clear avoided; low-tier rollback depth reduced to about 35 percent; duplicate VISUAL_SYNC/audio replay suppressed during resim. Compile not run because repeated guard samples stayed above 50 percent CPU.

## Rollback Netcode Titanium Polish - 2026-05-18
What was wrong: The first rollback pass still carried standard Unity rot: hidden `DontDestroyOnLoad` bootstrap, direct SHINOBU additions to the core memory enum, job `.Run()` calls without registered fences, and Burst jobs missing the mandated `CompileSynchronously` flag. That was not acceptable for a lockstep rollback domain.

What was done: Removed the hidden runtime bootstrap and made `HectonNetworkManager` explicitly own/require `HectonRollbackNetcodeRuntime`. Moved rollback vault IDs into `RollbackNetcodeVault` instead of widening `H8Memory.cs`. Added deterministic synchronous Burst directives to every rollback job. Added `RollbackNetcodeBufferAccess.FrameSnapshotAt()` ref access and rewired snapshot writes through `UnsafeUtility.AsRef`. Replaced runtime job `.Run()` calls with `JobHandle` scheduling, `H8Memory.RegisterActiveJob`, `JobHandle.ScheduleBatchedJobs`, and `DispatcherJobSwap` completion inside the dispatcher-owned barrier.

Cinematic Cheats used: Same core lie: no replicated positions, no RPC strings, no `NetworkTransform`. Deterministic input-only rollback is the authority; `VisualStateDTO` hides correction as presentation interpolation only.

Exact Microseconds saved: Removes hidden duplicate runtime risk and the per-scene bootstrap object. Avoids core-memory enum churn that forces wider rebuilds. Hot-path microseconds remain pending profiler proof; static budget still comes from MemCpy linear state copy, 35 percent low-tier rollback depth, skipped look-only rollback under pressure, and suppressed VISUAL_SYNC/audio during resim.

<SELF_AUDIT agent_id="SHINOBU_64" pass="lockstep_rollback_titanium_polish">
  <twenty_task_reconciliation>
    <task id="01" status="PASS">Archive/rationale scan found no authoritative `netcode_latency_profiles.h8bin`; fallback `GenerateEmergencyMockNetcode()` remains active.</task>
    <task id="02" status="PASS">Networking scan is clean for RPC, ClientRpc, ServerRpc, OnSerializeNetworkView, and NetworkTransform.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` has direct fields only; snapshot mutation now uses `ref FrameSnapshotDTO` via `RollbackNetcodeBufferAccess.FrameSnapshotAt()`.</task>
    <task id="04" status="PASS">Snapshot pages use `Align8(64 + payload)` and 16-byte/64-byte DTOs. No Pack=1 exists in networking.</task>
    <task id="05" status="PASS">`MockTickCommand` remains partial, 16 bytes, and emits only SIMULATION|POST_SIMULATION phase bits.</task>
    <task id="06" status="PASS">`StateSnapshotJob` copies AUP/player/entity/velocity/water hot arrays with `UnsafeUtility.MemCpy` and hashes exact bytes.</task>
    <task id="07" status="PASS">`DetectInputMismatchJob` compares remote received input against predicted input journal frames.</task>
    <task id="08" status="PASS">`RestoreSnapshotJob` rewinds live vault arrays from `StateRingBuffer[R]` with MemCpy.</task>
    <task id="09" status="PASS">`ApplyRemoteInputCorrectionJob` overwrites R..Current input journal and `HeadlessResimulationCommandJob` stages headless replay.</task>
    <task id="10" status="PASS">`VisualStateDTO` stores true/interpolated AUP and `VisualStateBlendJob` applies presentation-only smoothing.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` continuously reduces rollback depth and skips look-only rollback below the configured threshold.</task>
    <task id="12" status="PASS">60-frame hash fence compares remote/local hashes, publishes pause signal, dumps black box, and marks RAM overwrite intent by accepting the local snapshot hash.</task>
    <task id="13" status="PASS">`HashExactAupDouble3()` hashes the exact 24 bytes of `double3`; snapshot hashing copies exact AUP storage bytes.</task>
    <task id="14" status="PASS">`RollbackAudioSuppressionDTO` is written during headless resim so audio lanes can suppress duplicate sound events.</task>
    <task id="15" status="PASS">Remote input MODP quarantine is honored and state header carries `ModQuarantineMask`; no mod-only mismatch can force a core rollback.</task>
    <task id="16" status="PASS">State ring, frame snapshots, remote input, tick commands, visual states, CSV scratch, and fallback input journal use `NativeArrayOptions.UninitializedMemory` where fully overwritten.</task>
    <task id="17" status="PASS">`NetcodeTelemetryEntry[300]` and `Docs/AgentLogs/Dump_NETCODE_SURGEON.bin` are wired for desync and >5 ms resim estimate.</task>
    <task id="18" status="PASS">`Rollback Netcode Tuner` EditorWindow exists and reads/writes tuning DTOs through runtime API.</task>
    <task id="19" status="PASS">`netcode_profiles.csv` parser tokenizes byte scratch in place without split, regex, or LINQ.</task>
    <task id="20" status="PASS">Editor 200 ms ping button and red/green true-vs-interpolated SceneView gizmos exist.</task>
  </twenty_task_reconciliation>

  <struct_layout_verification evidence="STATIC_SOURCE_AND_EDIT_TESTS">
    <struct name="FrameSnapshotDTO" size_bytes="16" multiple_of_8="true">
      <field name="FrameHash" offset="0" size="4" type="uint" />
      <field name="InputMaskP1" offset="4" size="4" type="uint" />
      <field name="InputMaskP2" offset="8" size="4" type="uint" />
      <field name="MemoryOffset" offset="12" size="4" type="uint" />
      <math>4 + 4 + 4 + 4 = 16 bytes; 16 % 8 = 0; 16-byte SIMD-aligned DTO.</math>
    </struct>
    <struct name="StatePageHeaderDTO" size_bytes="64" multiple_of_8="true">
      <field_range name="uint lanes" offsets="0,4,8,12,16,20,24,28,32,36,40,44,48,52,56,60" size_each="4" />
      <math>16 uint fields * 4 = 64 bytes; exactly one L1 cache line.</math>
    </struct>
    <struct name="RemoteInputFrameDTO" size_bytes="32" multiple_of_8="true">
      <field name="Input" offset="0" size="24" type="InputStateDTO" />
      <field name="Frame" offset="24" size="4" type="uint" />
      <field name="Flags" offset="28" size="4" type="uint" />
      <math>24 + 4 + 4 = 32 bytes; no runtime bools or managed refs.</math>
    </struct>
    <struct name="MockTickCommand" size_bytes="16" multiple_of_8="true">
      <field name="CurrentFrame" offset="0" size="4" type="uint" />
      <field name="RollbackFrame" offset="4" size="4" type="uint" />
      <field name="InputMaskP1" offset="8" size="4" type="uint" />
      <field name="FramesToSimulate" offset="12" size="2" type="ushort" />
      <field name="PhaseMask" offset="14" size="1" type="byte" />
      <field name="Flags" offset="15" size="1" type="byte" />
    </struct>
    <struct name="VisualStateDTO" size_bytes="64" multiple_of_8="true">
      <field name="TrueAupAbsolute" offset="0" size="24" type="double3" />
      <field name="InterpolatedAupAbsolute" offset="24" size="24" type="double3" />
      <field name="Blend01" offset="48" size="4" type="float" />
      <field name="BlendStep01" offset="52" size="4" type="float" />
      <field name="EntityId" offset="56" size="4" type="uint" />
      <field name="Flags" offset="60" size="4" type="uint" />
    </struct>
    <struct name="NetcodeTelemetryEntry" size_bytes="64" multiple_of_8="true">16 four-byte lanes: frame, rollback, counters, cost, quality, hashes, flags, masks, offsets, mismatch, pads.</struct>
    <false_sharing status="N/A">No concurrent atomic counters were introduced. Telemetry entries are 64 bytes, one cache line per frame record.</false_sharing>
  </struct_layout_verification>

  <scalability_curve_explanation>
    `ResolveBudgetedRollbackFrames()` computes `budget = math.lerp(0.35, 1.0, saturate(GlobalQualityWeight))`. At weight 0.1, a 120-frame configured window collapses to roughly 50 frames and look-only rollback is skipped by `ShouldRollback()` unless quality crosses `MinQualityForLookRollback`. Button and movement mismatches still roll back because they are gameplay truth. `EstimateResimulationCostMs()` uses quality-weighted microsecond estimates so weak devices trip the >5 ms forensic dump earlier. Visual smoothing remains cheap: a fixed 16-entry visual DTO buffer, no transform sync, no object replay.
  </scalability_curve_explanation>

  <h_phi_vault_status persistent_private_native_arrays="0">
    <owner_system>SystemID.CoreDeterminism via RollbackNetcodeVault.OwnerSystem; no new core enum entry.</owner_system>
    <buffer id="70750" name="RollbackNetcodeVault.StateRingBuffer" options="UninitializedMemory" />
    <buffer id="70751" name="RollbackNetcodeVault.FrameSnapshots" options="UninitializedMemory" />
    <buffer id="70752" name="RollbackNetcodeVault.RuntimeState" options="ClearMemory" />
    <buffer id="70753" name="RollbackNetcodeVault.RemoteInputRing" options="UninitializedMemory" />
    <buffer id="70754" name="RollbackNetcodeVault.TickCommands" options="UninitializedMemory" />
    <buffer id="70755" name="RollbackNetcodeVault.VisualStates" options="UninitializedMemory" />
    <buffer id="70756" name="RollbackNetcodeVault.TelemetryRing" options="ClearMemory" />
    <buffer id="70757" name="RollbackNetcodeVault.Tuning" options="ClearMemory" />
    <buffer id="70758" name="RollbackNetcodeVault.AudioSuppression" options="ClearMemory" />
    <buffer id="70759" name="RollbackNetcodeVault.CsvScratch" options="UninitializedMemory" />
    <buffer id="70769" name="RollbackNetcodeVault.LatencyProfile" options="ClearMemory" />
    <borrowed_buffer id="70521" name="BufferID.ShinobuInputJournalRing" owner="Agent36 input journal; reused if present" />
  </h_phi_vault_status>

  <pointer_aliasing_and_dependency_graph>
    <noalias status="PASS">Rollback Burst job NativeArray fields use `[NoAlias]`; read-only inputs use `[ReadOnly, NoAlias]`.</noalias>
    <job name="DetectInputMismatchJob" consumes="PredictedJournal, RemoteInputRing, RuntimeState" outputs="RuntimeState" handle="registered via H8Memory + DispatcherJobSwap" />
    <job name="RestoreSnapshotJob" consumes="StateRingBuffer" outputs="RigidbodyAups, PlayerStates, EntityAups, EntityVelocities, RoomWaterLevels, RuntimeState" handle="registered" />
    <job name="ApplyRemoteInputCorrectionJob" consumes="RemoteInputRing" outputs="PredictedJournal" handle="registered" />
    <job name="HeadlessResimulationCommandJob" consumes="RuntimeState" outputs="MockTickCommand, RollbackAudioSuppressionDTO, RuntimeState" handle="registered" />
    <job name="StateSnapshotJob" consumes="live vault hot arrays" outputs="StateRingBuffer, FrameSnapshotDTO, RuntimeState" handle="registered" />
    <job name="VisualStateBlendJob" consumes="VisualStates" outputs="VisualStates" handle="registered" />
    <dispatcher_boundary>Current `IPostFixedTickable`/`ILateFrameTickable` APIs are void, so the domain cannot return a dependency without changing shared dispatcher interfaces. The patch schedules jobs and completes through the dispatcher-owned barrier instead of untracked `.Run()`.</dispatcher_boundary>
  </pointer_aliasing_and_dependency_graph>

  <compile_guard>
    No new asmdef reference was added. No sibling runtime assembly edge was introduced. Rollback vault IDs are domain-local constants, so the compile wall is not widened by new core enum members. `H8Memory.cs` still shows unrelated concurrent modifications from other agents; SHINOBU rollback enum lines were removed.
  </compile_guard>

  <dear_lie_confirmation>
    Before: transform/RPC sync is O(networked_entities) state replication plus rubber-band correction and render/audio duplicate replay. After: input-only lockstep sends O(players) input DTOs; rollback cost is O(snapshot_payload_bytes + rollback_window) local MemCpy/hash/resim command work. The player sees a three-frame visual interpolation fake while simulation truth rewinds exactly.
  </dear_lie_confirmation>

  <verification>
    <static_scan status="PASS">No RPC/NetworkTransform patterns in networking folder.</static_scan>
    <static_scan status="PASS">No Pack=1, hot DTO properties, runtime `new GameObject`, `DontDestroyOnLoad`, or job `.Run()` in networking runtime.</static_scan>
    <diff_check status="PASS">`git diff --check` clean for touched rollback files, with CRLF warnings only.</diff_check>
    <build status="BLOCKED_BY_DEPENDENCY">Core build was launched after guard cleared and failed in `Assets/_Project/Scripts/Construction/ConstructionSignals.cs`: missing `ISignal` at lines 13 and 36.</build>
  </verification>
</SELF_AUDIT>

## Rollback Netcode Build Wall - 2026-05-18
What was wrong: After the CPU/build guard cleared, Core compilation failed before rollback-specific diagnostics because `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` cannot resolve `ISignal`.

What was done: Recorded the compile wall and kept the fix boundary inside SHINOBU rollback/netcode. I did not patch Construction from this agent because this task domain is cooperative rollback netcode.

Cinematic Cheats used: None. This is compile-wall triage, not runtime behavior.

Exact Microseconds saved: No runtime claim. Build attempt was legal before launch (`CPU=18.9; CSC=0; DOTNET=0`). Command: `dotnet build Hecton8.Core.csproj --no-restore /m:1`. Errors: `ConstructionSignals.cs(13,47)` and `(36,42)` missing `ISignal`.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="compile_wall_boundary">
  <rollback_static_status status="PASS">Networking scan remains clean for RPC/NetworkTransform/DontDestroyOnLoad/runtime new GameObject/job Run/Pack=1/hot DTO properties.</rollback_static_status>
  <build_status status="BLOCKED_BY_DEPENDENCY">Core build fails in `Hecton8.Construction.ConstructionSignals`, outside rollback/netcode ownership.</build_status>
  <next_integrator_action>Construction owner should import or expose `Hecton8.Core.Contracts.Signals.ISignal` for `ConstructionPreviewSignal` and `FloraExclusionSignal`, then rerun Core build.</next_integrator_action>
</SELF_AUDIT_RECHECK>

## Volcanic Latest Bottom Entry - 2026-05-18
What was wrong: The latest active request is volcanic updrafts, but the shared SHINOBU_64 log had rollback material at the bottom because of the duplicate ID collision.

What was done: Reasserted the volcanic state at the bottom. Latest volcanic code change is still the `ThermalGeyser` cold `_volcanicDirector` cache; latest volcanic architecture audit is the known `Hecton8.AI.Cognition` DTO dependency for real leviathan force injection.

Cinematic Cheats used: Updraft truth remains a Burst AUP-local cylinder/cone solver and scalar wake/heat/vision data. No WindZone, no ConstantForce, no collider-driven boiling water.

Exact Microseconds saved: Sub-microsecond per active cave geyser fixed tick from removing the hot static director read; large savings remain no old broadphase force path and `GlobalQualityWeight` debris/turbulence collapse.

<SELF_AUDIT_RECHECK agent_id="SHINOBU_64" pass="volcanic_latest_bottom_entry">
  <active_prompt role="THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR" task_count="20" />
  <static_scan status="PASS">SHINOBU volcanic files are clean for banned Unity force paths, Pack=1, LINQ/foreach, UnityEngine.Random, hot DTO properties, and hot NativeArray/List/HashMap allocation.</static_scan>
  <known_compile_wall_debt status="DOCUMENTED">Real leviathan lift still uses AI-owned DTOs because those buffers are registered by the AI owner and the existing Core asmdef already references AI Cognition. No new asmdef reference was added.</known_compile_wall_debt>
  <build_status status="BLOCKED_BY_DEPENDENCY">Latest legal Core build fails outside volcanic ownership in `ConstructionSignals.cs`: unresolved `ISignal` at lines 13 and 36.</build_status>
</SELF_AUDIT_RECHECK>

## Volcanic Debris Quality Gate Polish - 2026-05-19
What was wrong: Task 11 required low-quality hardware to avoid cylinder intersections for small debris. The previous mock debris path set lift weight to zero under the quality threshold but still called `TryEvaluateVent()` for every debris/vent pair, spending AUP-local cone/cylinder ALU and then discarding the result.

What was done: `ResolveDebrisLiftWeight()` now explicitly multiplies the polynomial smooth curve by `math.step(0.3f, q)`. `ResolveTurbulenceGate()` uses the same step-polynomial curve so low quality collapses turbulence deterministically. The mock debris path now checks `debrisLiftWeight > 0.0001f` before entering the vent loop; at low quality it writes the cull flag and skips all debris intersections.

Cinematic Cheats used: Small rock lift is now deliberately allowed to be visually wrong on weak devices. The lethal geyser truth remains for players, submarines, and leviathans; debris chimney spectacle returns continuously above the 0.3 quality gate.

Exact Microseconds saved: With default `MockDebrisCapacity=64` and 8 active vents, low quality skips up to 512 `TryEvaluateVent()` calls per mock injection pass. Each skipped call avoids AUP subtraction, finite checks, axial/radial cone math, falloff, and potential turbulence vector work.

<SELF_AUDIT agent_id="SHINOBU_64" pass="volcanic_debris_quality_gate">
  <twenty_task_reconciliation>
    <task id="01" status="PASS">Legacy vent binary fallback remains emergency mock vents.</task>
    <task id="02" status="PASS">No WindZone or ConstantForce found in SHINOBU volcanic files.</task>
    <task id="03" status="PASS">`VentStateDTO` has public fields, no hot properties.</task>
    <task id="04" status="PASS">`VentStateDTO` remains 64 bytes with no Pack=1.</task>
    <task id="05" status="PASS">Mock submarine path remains active.</task>
    <task id="06" status="PASS">Burst cylinder solver remains direct force/velocity injection.</task>
    <task id="07" status="PASS">Eruption oscillator remains Burst-controlled.</task>
    <task id="08" status="PASS">Thermal blindness remains scalar/shader-facing fake.</task>
    <task id="09" status="PASS">Debris chimney remains quality-gated, with low-quality intersection skip added.</task>
    <task id="10" status="PASS">Leviathan riding remains wired through AI-owned buffers and float-state signals.</task>
    <task id="11" status="PASS">`GlobalQualityWeight < 0.3` now skips small debris cylinder intersections, not just lift application.</task>
    <task id="12" status="PASS">Acoustic/seismic eruption publication remains intact.</task>
    <task id="13" status="PASS">Cylinder math remains AUP-local `float3` after double subtraction.</task>
    <task id="14" status="PASS">Thermodynamics heat injection remains cached-service based.</task>
    <task id="15" status="PASS">Vertical drag reduction remains applied for lifted submarines.</task>
    <task id="16" status="PASS">Vent buffer still uses `NativeArrayOptions.UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame volcanic telemetry remains wired.</task>
    <task id="18" status="PASS">Editor tuner remains present.</task>
    <task id="19" status="PASS">CSV byte parser remains present.</task>
    <task id="20" status="PASS">Gizmo visualizer remains present.</task>
  </twenty_task_reconciliation>
  <struct_layout_verification>
    <struct name="VentStateDTO" size_bytes="64">
      <field name="AUP" offset="0" size="24" />
      <field name="UpVector" offset="24" size="12" />
      <field name="Radius" offset="36" size="4" />
      <field name="ThrustPower" offset="40" size="4" />
      <field name="EruptionTimer" offset="44" size="4" />
      <field name="_pad0" offset="48" size="4" />
      <field name="implicit_alignment_gap" offset="52" size="4" />
      <field name="_pad1" offset="56" size="8" />
      <math>64 % 16 = 0; no Pack=1; one cache line.</math>
    </struct>
  </struct_layout_verification>
  <scalability_curve_explanation>
    `ResolveDebrisLiftWeight(q)` and `ResolveTurbulenceGate(q)` now use `math.step(0.3, q) * SmoothStep(0.3, 1.0, q)`. Below 0.3, small debris skips the vent loop and turbulence collapses to strict +Y. Above 0.3, the polynomial curve ramps ALU and spectacle continuously instead of using a hardware-tier boolean.
  </scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_arrays="0">No private persistent NativeArray/List/HashMap allocations were introduced. Buffers remain `VolcanicUpdraftVault` IDs 70750-70761.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>
    <noalias status="PASS">Touched jobs keep `[NoAlias]` fields; no new containers were added.</noalias>
    <job name="VolcanicMockEntityInjectionJob" change="Low-quality debris branch now bypasses vent loop before `TryEvaluateVent()`." />
  </pointer_aliasing_and_dependency_graph>
  <compile_guard>
    No asmdef or sibling assembly reference was added. Fresh build was deferred by guard: CPU sampled 100/100/100 with active `dotnet` process 50592.
  </compile_guard>
  <dear_lie_confirmation>
    Before low-quality debris still paid O(debris * vents) query cost. After low-quality debris is O(debris) flag writes with no vent intersection; high-quality remains O(debris * vents) for spectacle.
  </dear_lie_confirmation>
</SELF_AUDIT>

## Rollback Netcode Verified Bottom Entry - 2026-05-19
What was wrong: The bottom of this shared SHINOBU_64 log still pointed at the duplicate volcanic lane, and the rollback lane had stale 32-bit/folded hash reporting in older audit text.

What was done: Reasserted the current user directive (`SHINOBU_LOCKSTEP_ROLLBACK_NETCODE`) at the bottom, documented the 64-bit XXHash3 upgrade, and recorded the passing Core build after guard-compliant retries.

Cinematic Cheats used: Input-only lockstep remains the simulation truth; three-frame visual interpolation is the Dear Lie. No RPC, no `NetworkTransform`, no transform rubber-band authority.

Exact Microseconds saved: Full transform sync remains removed. At low thermal quality, rollback scan budget drops to about 22 percent of configured max; for a 120-frame window this cuts scan candidates by roughly 94 frames. Hash widening costs one 64-bit cadence compare every 60 frames.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" build="PASS_WITH_WARNINGS">
  <task_reconciliation>01 PASS; 02 PASS; 03 PASS; 04 PASS; 05 PASS; 06 PASS; 07 PASS; 08 PASS; 09 PASS; 10 PASS; 11 PASS; 12 PASS; 13 PASS; 14 PASS; 15 PASS; 16 PASS; 17 PASS; 18 PASS; 19 PASS; 20 PASS.</task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24">0:ulong FrameHash64(8), 8:uint InputMaskP1(4), 12:uint InputMaskP2(4), 16:uint MemoryOffset(4), 20:uint Reserved0(4). Total 24; 24 % 8 == 0.</FrameSnapshotDTO>
    <StatePageHeaderDTO size="64">0:ulong FrameHash64(8), 8..47:10 uint frame/count/flags/memory/quarantine fields, 48..63:4 uint padding. Total 64.</StatePageHeaderDTO>
    <RollbackRuntimeStateDTO size="80">0..15:2 ulong hashes, 16..71:uint/float state, 72..79:ulong padding. Total 80; 80 % 16 == 0.</RollbackRuntimeStateDTO>
    <NetcodeTelemetryEntry size="80">0..15:2 ulong hashes, 16..71:uint/float telemetry, 72..79:ulong padding. Total 80; 80 % 16 == 0.</NetcodeTelemetryEntry>
  </struct_layout_verification>
  <scalability_curve>Below quality 0.3, `ResolveBudgetedRollbackFrames()` eases toward a 0.22 budget floor with `Smooth01`; look-only rollback is gated by `math.step`; button/move mismatches remain deterministic truth.</scalability_curve>
  <h_phi_vault_status persistent_private_native_arrays="0">Vault buffers: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>[NoAlias] on rollback jobs; handles scheduled, registered through `H8Memory.RegisterActiveJob`, batched, and completed at dispatcher barrier. Jobs: DetectInputMismatch, RestoreSnapshot, ApplyRemoteInputCorrection, HeadlessResimulationCommand, StateSnapshot, VisualStateBlend.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No rollback asmdef edge or sibling runtime dependency was added. Buffer IDs are domain-local constants. External edits were only mechanical compile-wall imports.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform sync and correction. After: O(players) input sync plus local MemCpy/hash/resim; presentation lies via interpolation.</dear_lie_confirmation>
  <verification>Core build command `dotnet build Hecton8.Core.csproj --no-restore /m:1` passed. Remaining 8 warnings are `CS0649` in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, outside rollback ownership.</verification>
</SELF_AUDIT>

## Rollback Netcode Dispatcher Pipeline Recheck - 2026-05-19
What was wrong: The previous bottom audit still described a dispatcher barrier path. The code now had to prove the rollback owner no longer schedules and force-completes jobs inside its own tick path.

What was done: Replaced the rollback fixed lane with `IDispatcherFixedSystem`. `ScheduleFixedSimulation()` returns one `RollbackFixedPipelineJob` handle to the master fixed bridge; the job executes detect, restore, input correction, headless command, snapshot, hash fence, and telemetry. `PostFixedSimulation()` only performs cold pause/dump/hash-marker side effects after the dispatcher completion window.

Cinematic Cheats used: Input-only lockstep remains the truth. Three-frame AUP interpolation remains the Dear Lie that hides correction without mutating physics truth.

Exact Microseconds saved: 0 measured microseconds claimed. No profiler capture was run. Structural saving is removal of three to four forced main-thread job completion points from rollback frames; low-quality rollback still cuts a 120-frame scan to about 26 candidates.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="dispatcher_pipeline_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Archive scan found no `netcode_latency_profiles.h8bin`; deterministic emergency mock remains active.</task>
    <task id="02" status="PASS">No RPC, `OnSerializeNetworkView`, or `NetworkTransform`; remote authority is input DTOs and hashes only.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` uses public fields and ref mutation via `UnsafeUtility.AsRef`; no hot properties.</task>
    <task id="04" status="PASS">Snapshot pages use 8-byte aligned header+payload stride.</task>
    <task id="05" status="PASS">`MockTickCommand` is partial, 16 bytes, and emits SIMULATION|POST_SIMULATION without VISUAL_SYNC.</task>
    <task id="06" status="PASS">`StateSnapshotJob` copies hot state with `UnsafeUtility.MemCpy` and hashes exact bytes with full XXHash3-64.</task>
    <task id="07" status="PASS">`DetectInputMismatchJob` compares remote received input against predicted journal frames.</task>
    <task id="08" status="PASS">`RestoreSnapshotJob` restores authoritative vault buffers with `UnsafeUtility.MemCpy`.</task>
    <task id="09" status="PASS">`ApplyRemoteInputCorrectionJob` and `HeadlessResimulationCommandJob` resim the truth lane only.</task>
    <task id="10" status="PASS">`VisualStateDTO` holds true/interpolated AUP and blends presentation without mutating simulation truth.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` continuously throttles rollback depth and gates look-only rollback.</task>
    <task id="12" status="PASS">60-frame hash fence compares local/remote XXHash3-64 and triggers pause/dump/full-state overwrite marker.</task>
    <task id="13" status="PASS">`HashExactAupDouble3()` hashes exact 24-byte `double3` payloads.</task>
    <task id="14" status="PASS">`RollbackAudioSuppressionDTO` marks resim frames for audio suppression.</task>
    <task id="15" status="PASS">MODP quarantine flags skip modded input frames from desync authority.</task>
    <task id="16" status="PASS">State, input, command, visual, and CSV scratch vault buffers use `NativeArrayOptions.UninitializedMemory` where clearing is not required.</task>
    <task id="17" status="PASS">`NetcodeTelemetryEntry[300]` ring and `Dump_NETCODE_SURGEON.bin` blackbox dump are wired.</task>
    <task id="18" status="PASS">`Rollback Netcode Tuner` editor facade exposes rollback knobs.</task>
    <task id="19" status="PASS">`netcode_profiles.csv` parser tokenizes bytes from vault scratch without split/regex/LINQ.</task>
    <task id="20" status="PASS">Editor ping simulation and red true/green interpolated scene gizmos are present.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24" alignment="8">0 ulong FrameHash64(8); 8 uint InputMaskP1(4); 12 uint InputMaskP2(4); 16 uint MemoryOffset(4); 20 uint Reserved0(4). Total 24, 24 % 8 = 0.</FrameSnapshotDTO>
    <StatePageHeaderDTO size="64" alignment="64">0 ulong FrameHash64(8); 8 Frame; 12 PayloadBytes; 16 RigidbodyAupCount; 20 PlayerStateCount; 24 EntityAupCount; 28 EntityVelocityCount; 32 RoomWaterCount; 36 Flags; 40 MemoryOffset; 44 ModQuarantineMask; 48 Reserved0; 52 Reserved1; 56 Reserved2; 60 Reserved3. Total 64.</StatePageHeaderDTO>
    <RollbackRuntimeStateDTO size="80" alignment="16">0 LastFrameHash64(8); 8 LastRemoteHash64(8); 16 CurrentFrame; 20 LastRollbackFrame; 24 LastRemoteFrame; 28 LastMismatchFrame; 32 FramesResimulated; 36 RollbacksTriggered; 40 ResimComputeTimeMs; 44 GlobalQualityWeight; 48 MismatchSeverity01; 52 Flags; 56 StateSnapshotBytes; 60 StateMemoryOffset; 64 DesyncCount; 68 Reserved0; 72 Reserved1(8). Total 80, 80 % 16 = 0.</RollbackRuntimeStateDTO>
    <VisualStateDTO size="64" alignment="16">0 TrueAupAbsolute double3(24); 24 InterpolatedAupAbsolute double3(24); 48 Blend01; 52 BlendStep01; 56 EntityId; 60 Flags. Total 64.</VisualStateDTO>
    <NetcodeTelemetryEntry size="80" alignment="16">0 FrameHash64(8); 8 RemoteHash64(8); 16 Frame; 20 LastRollbackFrame; 24 RollbacksTriggered; 28 FramesResimulated; 32 ResimComputeTimeMs; 36 GlobalQualityWeight; 40 Flags; 44 InputMaskP1; 48 InputMaskP2; 52 StateMemoryOffset; 56 SnapshotBytes; 60 MismatchFrame; 64 Reserved0; 68 Reserved1; 72 Reserved2(8). Total 80, 80 % 16 = 0.</NetcodeTelemetryEntry>
  </struct_layout_verification>
  <scalability_curve>`ResolveBudgetedRollbackFrames()` uses `math.step`, `math.lerp`, and `Smooth01`. Below `GlobalQualityWeight` 0.3, rollback depth eases toward 22 percent of max and look-only rollback is rejected by a scalar gate; button/move mismatches stay authoritative.</scalability_curve>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed/created 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes dispatcher `dependsOn` and outputs one `RollbackFixedPipelineJob` handle registered with `H8Memory.RegisterActiveJob`. Fields use `[NoAlias]`; read-only fields use `[ReadOnly, NoAlias]`. Internal order: DetectInputMismatch -> RestoreSnapshot -> ApplyRemoteInputCorrection -> HeadlessResimulationCommand -> StateSnapshot -> CheckRemoteHashFence -> WriteTelemetry.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No asmdef reference was added, no sibling runtime assembly edge was introduced, and rollback buffer IDs remain domain-local constants rather than core enum members.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform replication and correction. After: O(players) input sync plus O(snapshot_payload_bytes + rollback_window) local MemCpy/hash/resim command work. The visual correction is an interpolation lie; physics truth is exact restored state.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No forced rollback barrier, `.Complete()`, runtime `.Run()`, RPC, `NetworkTransform`, packed structs, hot DTO properties, LINQ conversions, `string.Format`, debug logging, `UnityEngine.Random`, or direct `SystemID.Networking`/`BufferID.ShinobuRollback` in rollback networking files.</static_scan>
    <build status="PASS_WITH_EXTERNAL_WARNINGS">`dotnet build Hecton8.Core.csproj --no-restore /m:1` launched after guard `CPU=36.5; CSC=0; DOTNET=0` and succeeded. Remaining 8 warnings are external `CS0649` in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`.</build>
    <diff_check status="PASS">Scoped `git diff --check` returned clean.</diff_check>
  </verification>
</SELF_AUDIT>

## Rollback Netcode AUP-Local Visual DTO True Bottom Recheck - 2026-05-19
What was wrong: The last visible rollback audit still ended with the pre-polish dispatcher DTO wording. Absolute visual `double3` presentation is a precision leak in a 100 km AUP world.

What was done: `VisualStateDTO` now stores `AnchorAupAbsolute`, `TrueLocalMeters`, and `InterpolatedLocalMeters`. The rollback pipeline subtracts the anchor before presentation, editor gizmos draw local correction vectors, and tests assert the new 64-byte field offsets.

Cinematic Cheats used: Simulation truth is still exact MemCpy rollback. Presentation is a local interpolation fake, not transform authority.

Exact Microseconds saved: No profiler claim. Hot visual cost is a bounded local `float3` lerp over the fixed visual buffer. Build after this polish is deferred by guard samples `CPU=97.9; CSC=0; DOTNET=0`, `CPU=93.8; CSC=0; DOTNET=0`, and `CPU=100.0; CSC=1; DOTNET=1`.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="aup_local_visual_true_bottom_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Fallback mock netcode remains deterministic because no latency archive exists.</task>
    <task id="02" status="PASS">No RPC or `NetworkTransform`; authority is input DTOs plus hashes.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` stays field-only and ref-mutated.</task>
    <task id="04" status="PASS">Snapshot pages remain aligned; no packed rollback DTOs.</task>
    <task id="05" status="PASS">`MockTickCommand` emits SIMULATION|POST_SIMULATION only.</task>
    <task id="06" status="PASS">Snapshots use `UnsafeUtility.MemCpy` plus XXHash3-64.</task>
    <task id="07" status="PASS">Remote input mismatch compares against predicted journal frames.</task>
    <task id="08" status="PASS">Restore uses `UnsafeUtility.MemCpy` into vault state.</task>
    <task id="09" status="PASS">Journal correction and headless resim command remain wired.</task>
    <task id="10" status="PASS">Visual correction is AUP-local: one anchor plus two local `float3` vectors.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` throttles rollback through continuous curves.</task>
    <task id="12" status="PASS">60-frame hash fence compares XXHash3-64 and triggers blackbox response.</task>
    <task id="13" status="PASS">AUP truth remains exact-byte hashed; presentation deltas are post-anchor local.</task>
    <task id="14" status="PASS">Audio suppression DTO marks resim windows.</task>
    <task id="15" status="PASS">MODP quarantine excludes mod-only input from authority.</task>
    <task id="16" status="PASS">Fully overwritten buffers use `UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame telemetry and dump path remain wired.</task>
    <task id="18" status="PASS">Editor tuner facade remains present.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch and zero-LINQ.</task>
    <task id="20" status="PASS">200 ms ping simulation and gizmos now visualize local correction space.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <FrameSnapshotDTO size="24" alignment="8">0 FrameHash64 ulong(8); 8 InputMaskP1 uint(4); 12 InputMaskP2 uint(4); 16 MemoryOffset uint(4); 20 Reserved0 uint(4). 24 % 8 = 0.</FrameSnapshotDTO>
    <VisualStateDTO size="64" alignment="16">0 AnchorAupAbsolute double3(24); 24 TrueLocalMeters float3(12); 36 InterpolatedLocalMeters float3(12); 48 Blend01 float(4); 52 BlendStep01 float(4); 56 EntityId uint(4); 60 Flags uint(4). 64 % 16 = 0.</VisualStateDTO>
    <NetcodeTelemetryEntry size="80" alignment="16">0 FrameHash64 ulong(8); 8 RemoteHash64 ulong(8); 16..68 telemetry lanes; 72 Reserved2 ulong(8). 80 % 16 = 0.</NetcodeTelemetryEntry>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, rollback depth eases toward 22 percent of max with `math.lerp` and `Smooth01`; look-only rollback is gated by `math.step`. Visual work stays one bounded local `float3` lerp per active correction.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 StateRingBuffer, 70751 FrameSnapshots, 70752 RuntimeState, 70753 RemoteInputRing, 70754 TickCommands, 70755 VisualStates, 70756 TelemetryRing, 70757 Tuning, 70758 AudioSuppression, 70759 CsvScratch, 70769 LatencyProfile; borrowed/created 70521 ShinobuInputJournalRing.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes dispatcher dependency and returns one registered `RollbackFixedPipelineJob`; job fields use `[NoAlias]`, read-only fields use `[ReadOnly, NoAlias]`.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge, sibling runtime reference, or core rollback enum was introduced.</compile_guard>
  <dear_lie_confirmation>Before: O(networked_entities) transform correction or absolute rubber band. After: O(players) input sync plus exact local rollback; the visible correction is local interpolation.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No stale `TrueAupAbsolute` or `InterpolatedAupAbsolute` references remain.</static_scan>
    <static_scan status="PASS">Runtime rollback files have no `.Run()`, `.Complete()`, forced barrier, RPC, `NetworkTransform`, packed structs, hot DTO properties, LINQ, debug logging, or `UnityEngine.Random`.</static_scan>
    <diff_check status="PASS">Manual trailing-whitespace scan on untracked rollback/docs files is clean; `git diff --check` reported no tracked whitespace diagnostics.</diff_check>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched after this polish because guard sampled `CPU=97.9; CSC=0; DOTNET=0`, then `CPU=93.8; CSC=0; DOTNET=0`, then `CPU=100.0; CSC=1; DOTNET=1` after waiting.</build>
  </verification>
</SELF_AUDIT>

## Rollback Netcode ARM64 Lockstep DTO True Bottom Recheck - 2026-05-19
What was wrong: The AUP-local rollback lane was clean, but the lockstep state validator still had packed DTOs on the exact player/replay/hash state surface copied or compared by rollback. That meant the previous audit could truthfully pass networking files while still leaving ARM64 packed-layout debt in the deterministic state path.

What was done: Removed `Pack=1` from `LockstepPlayerKinematicState`, replay frame/header DTOs, array hash telemetry DTOs, and lockstep hash job structs in `LockstepStateValidator.cs`. Kept explicit sizes for replay ABI. Added edit-test offset guards for player/replay DTOs. Upgraded lockstep hash jobs with `CompileSynchronously=true`, deterministic Burst, and `[NoAlias]` NativeArray fields.

Cinematic Cheats used: No transform authority. Input-only rollback remains the truth; AUP-local visual interpolation remains the Dear Lie.

Exact Microseconds saved: No profiler number claimed. This removes unaligned-access risk and gives Burst clearer aliasing facts for hash jobs. Build is still deferred by guard samples: `CPU=100.0; CSC=0; DOTNET=0`, `CPU=100.0; CSC=1; DOTNET=1`, `CPU=100.0; CSC=0; DOTNET=0`.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Cooperative Multiplayer Lockstep Rollback Netcode" task_count="20" pass="arm64_lockstep_dto_true_bottom_recheck">
  <task_reconciliation>
    <task id="01" status="PASS">Emergency mock fallback remains active because the current batch XML is absent and no latency archive was found.</task>
    <task id="02" status="PASS">No RPC or `NetworkTransform`; input DTOs and hashes remain authority.</task>
    <task id="03" status="PASS">`FrameSnapshotDTO` and lockstep player DTO are field-only aligned structs.</task>
    <task id="04" status="PASS">Rollback state DTOs and lockstep replay/player DTOs are aligned; no packed layout remains in the rollback validation surface.</task>
    <task id="05" status="PASS">Headless replay command still excludes visual sync.</task>
    <task id="06" status="PASS">State snapshots still use `UnsafeUtility.MemCpy` plus XXHash3-64.</task>
    <task id="07" status="PASS">Remote input mismatch remains journal-based.</task>
    <task id="08" status="PASS">Restore remains MemCpy-based.</task>
    <task id="09" status="PASS">Journal correction and resim command remain wired.</task>
    <task id="10" status="PASS">Visual correction remains AUP-local.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` still throttles rollback continuously.</task>
    <task id="12" status="PASS">60-frame hash fence remains XXHash3-64.</task>
    <task id="13" status="PASS">AUP truth remains exact-byte hashed.</task>
    <task id="14" status="PASS">Audio suppression remains wired.</task>
    <task id="15" status="PASS">MODP quarantine remains excluded from authority.</task>
    <task id="16" status="PASS">Fully overwritten buffers still use `UninitializedMemory`.</task>
    <task id="17" status="PASS">300-frame telemetry and dump path remain wired.</task>
    <task id="18" status="PASS">Editor tuner remains present.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch and zero-LINQ.</task>
    <task id="20" status="PASS">Ping simulation and local correction gizmos remain present.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <LockstepPlayerKinematicState size="96">0 SectorX long(8); 8 SectorY long(8); 16 SectorZ long(8); 24 LocalPosition float3(12); 36 Velocity float3(12); 48 Forward float3(12); 60 Frame; 64 Flags; 68 InputActions; 72 StableId; 76 HashCadenceFrames; 80..95 reserved. 96 % 16 = 0.</LockstepPlayerKinematicState>
    <LockstepReplayInputFrame size="48">0 Frame; 4 ActionsBitmask; 8 MoveDelta float2; 16 LookDelta float2; 24 VerticalDelta; 28 CurrentInputSchemeHash; 32 Flags; 36 Sequence; 40 Reserved0; 44 Reserved1. 48 % 16 = 0.</LockstepReplayInputFrame>
    <LockstepReplayBlockHeader size="128">0 Magic ulong; 8 Version; 12 HeaderSizeBytes; 16 StartFrame; 20 HashFrame; 24 InputCount; 28 Flags; 32 MasterHash ulong; 40..87 uint lanes; 88..127 five ulong reserved lanes. 128 % 16 = 0.</LockstepReplayBlockHeader>
    <VisualStateDTO size="64">0 AnchorAupAbsolute double3; 24 TrueLocalMeters float3; 36 InterpolatedLocalMeters float3; 48 Blend01; 52 BlendStep01; 56 EntityId; 60 Flags. 64 % 16 = 0.</VisualStateDTO>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, rollback scan depth eases to the 22 percent floor, look-only rollback is scalar-gated, and visual correction stays a bounded local `float3` lerp. The ARM64 patch changes alignment safety, not quality behavior.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Rollback vault IDs unchanged: 70750..70759 and 70769, plus borrowed/created 70521 input journal.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>Rollback fixed path outputs one registered `RollbackFixedPipelineJob`; lockstep hash jobs now expose `[NoAlias]` on source/output arrays.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge was added. Rollback files remain in existing `Hecton8.Core`; this pass did not add sibling references.</compile_guard>
  <dear_lie_confirmation>Before: entity transform sync/rubber-band correction. After: player input sync plus local MemCpy rollback and local interpolation presentation.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No `Pack=1` remains in rollback networking or `LockstepStateValidator.cs`.</static_scan>
    <static_scan status="PASS">No stale absolute visual fields, RPC, `NetworkTransform`, runtime `.Run()`, or runtime `.Complete()` in rollback networking/editor runtime scope.</static_scan>
    <diff_check status="PASS">Manual trailing-whitespace scan is clean; `git diff --check` reported only the tracked-file CRLF normalization warning for `LockstepStateValidator.cs`.</diff_check>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched because guard sampled `CPU=100.0; CSC=0; DOTNET=0`, then `CPU=100.0; CSC=1; DOTNET=1`, then `CPU=100.0; CSC=0; DOTNET=0`.</build>
  </verification>
</SELF_AUDIT>

## Volcanic Updraft Dispatcher Fixed-Pipeline Bottom Reassertion - 2026-05-19
What was wrong: The volcanic audit needed to be the newest bottom entry after duplicate `SHINOBU_64` rollback traffic. The code also had stale fixed-tick residue after the dispatcher conversion and needed a final debris quality proof.

What was done: `VolcanicUpdraftDirector` is now an `IDispatcherFixedSystem`. It returns its fixed simulation `JobHandle` to the master bridge, combines dispatcher dependencies with pending submarine read handles, and unlocks vault buffers only from `PostFixedSimulation()`. `GlobalQualityWeight < 0.3` now makes debris lift zero and skips the debris vent loop entirely. Static scans are clean for Unity force paths, hot LINQ/foreach/string formatting, `Pack=1`, hot DTO properties, and hot native allocations.

Cinematic Cheats used: Analytic AUP-local cylinder/cone updrafts plus curl-noise turbulence and scalar wake/heat/acoustic outputs replace Unity physics force fields and any fluid simulation. Low quality uses strict vertical thrust and no debris intersection work; high quality restores turbulence, debris chimney, wakes, heat, and leviathan riding.

Exact Microseconds saved: No profiler capture. Low quality skips up to `64 * ventCount` mock debris cylinder/cone tests per pass; at 8 vents that is 512 analytic tests avoided. Dispatcher polish removes the volcanic owner's hot fixed-batch wait and leaves synchronization to the single master fixed bridge.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Thermal Updrafts / Volcanic Geysers" task_count="20" pass="volcanic_bottom_reassertion_dispatcher_quality_gate">
  <task_reconciliation>
    <task id="01" status="PASS">No `volcanic_vent_locations.h8bin` was found; emergency mock vents remain the deterministic fallback.</task>
    <task id="02" status="PASS">No Unity `WindZone`, `ConstantForce`, `Rigidbody`, `ForceMode`, `UnityEngine.Physics`, or `PhysicsForceRouter` path remains in the volcanic touch set.</task>
    <task id="03" status="PASS">`VentStateDTO` and hot DTOs are public-field structs with no hot properties.</task>
    <task id="04" status="PASS">Primary DTOs are 64-byte layouts with no `Pack=1`.</task>
    <task id="05" status="PASS">`MockSubmarineArray` remains the proof lane for direct velocity injection.</task>
    <task id="06" status="PASS">Burst jobs inject updraft vectors into submarine/player/leviathan force or velocity arrays, not Unity components.</task>
    <task id="07" status="PASS">Eruption uses deterministic frame/fixed-delta oscillator inputs.</task>
    <task id="08" status="PASS">Thermal blindness is scalar presentation data, not fluid or physics simulation.</task>
    <task id="09" status="PASS">Debris chimney exists only above the continuous quality gate; low quality skips debris vent intersections.</task>
    <task id="10" status="PASS">Leviathan riding writes steering/float-state data and signals.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` uses `math.step`, `math.lerp`, and polynomial smoothing, not binary hardware switches.</task>
    <task id="12" status="PASS">Acoustic roar is signal/scalar presentation output.</task>
    <task id="13" status="PASS">AUP-local math subtracts `double3` vent/entity origins before casting local deltas to `float3`.</task>
    <task id="14" status="PASS">Thermodynamics bridge is cold-cached and scalar.</task>
    <task id="15" status="PASS">Vertical drag/downward suppression remains scalar integration math.</task>
    <task id="16" status="PASS">Vent and CSV scratch vault buffers use `NativeArrayOptions.UninitializedMemory` where fully overwritten.</task>
    <task id="17" status="PASS">300-frame telemetry and dump path remain wired.</task>
    <task id="18" status="PASS">Editor tuner facade remains present.</task>
    <task id="19" status="PASS">CSV parser remains byte-scratch and zero-LINQ.</task>
    <task id="20" status="PASS">Gizmo cylinder/cone visualization remains editor-only.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <VentStateDTO size="64" alignment="16">0 AUP double3(24); 24 UpVector float3(12); 36 Radius float(4); 40 ThrustPower float(4); 44 EruptionTimer float(4); 48 _pad0 uint(4); 52 implicit gap(4); 56 _pad1 ulong(8). Total 64, 64 % 16 = 0.</VentStateDTO>
    <VolcanicUpdraftSettingsDTO size="64" alignment="16">Sixteen 4-byte lanes from MaxThrust through Flags. Total 64, 64 % 16 = 0.</VolcanicUpdraftSettingsDTO>
    <VolcanicUpdraftFrameCounter size="64" false_sharing="padded">Explicit 64-byte counter row; parallel writes do not share a smaller counter line.</VolcanicUpdraftFrameCounter>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, debris lift resolves to zero through `math.step(0.3, q) * SmoothStep(0.3, 1, q)` and debris vent intersections are bypassed. Turbulence uses the same gate, collapsing to strict +Y thrust. Higher weights restore authored-up turbulence, debris lift, wake density, heat, acoustic intensity, and leviathan riding continuously.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 Vents, 70751 Settings, 70752 Telemetry, 70753 MockSubmarines, 70754 MockLeviathans, 70755 FloatSignals, 70756 DynamicWakes, 70757 MockFlowField, 70758 CsvScratch, 70759 FrameCounters, 70760 MockDebris, 70761 PlayerHeat.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes dispatcher `dependsOn` and pending submarine vent readers, schedules reset -> eruption -> entity/player/leviathan injection -> visual fake -> telemetry, registers the final handle with `H8Memory`, and returns it. `ScheduleSubmarineInjection()` consumes pending vent writes and records read handles. Jobs use `[NoAlias]`, with `[ReadOnly, NoAlias]` on read-only arrays.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge was introduced. The system routes through `GlobalRegistry`, `GlobalDataVault`, fixed dispatcher interfaces, and signals. Existing leviathan DTO use is retained because the vault owner registers those exact unmanaged buffer types.</compile_guard>
  <dear_lie_confirmation>Before: O(physics_broadphase + entities * vents * solver_cost) force-field or fluid-style work. After: O(entities * activeVents) analytic cylinder/cone math, and O(1) debris lane under low quality because the vent loop is skipped. Visual overkill is pushed as scalar GPU-facing wake/heat data.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No forbidden Unity force path, hot LINQ/foreach/string.Format, `UnityEngine.Random`, `Pack=1`, hot DTO property, or hot native allocation matched.</static_scan>
    <dispatcher_scan status="PASS">No stale `_jobPending`, `IFixedTickable`, `IPostFixedTickable`, fixed/post-fixed registration, or legacy fixed tick symbol remains in `VolcanicUpdraftDirector`.</dispatcher_scan>
    <cold_complete status="PASS">The only `.Complete()` in the volcanic director is cold `OnDisable()` teardown to avoid unlocking buffers while a job is live.</cold_complete>
    <whitespace status="PASS">Manual trailing-whitespace scan over volcanic code and audit files returned no matches.</whitespace>
    <build status="DEFERRED_BY_CPU_GUARD">Fresh build not launched after dispatcher polish because latest guard sampled `CPU=100,100,100` with active `dotnet:30376`.</build>
  </verification>
</SELF_AUDIT>

## Volcanic Updraft XML Recheck True Bottom Reassertion - 2026-05-19
What was wrong: The prompt was repeated after previous volcanic work, and one fresh volcanic audit entry was accidentally inserted near the top of this shared duplicate-ID log. The CTO protocol requires oldest entries at the top and newest evidence at the bottom.

What was done: Removed the misplaced near-top volcanic XML recheck block, re-extracted the volcanic `<AGENT_PROMPT id="SHINOBU_64">` from `Docs/Tasks/CURRENT_BATCH.md`, confirmed the 20-task `THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR` matrix, and bottom-appended this corrected audit. Source scans were rerun for forbidden Unity force paths, packed layouts, hot accessors, hot native allocations, Burst directives, `[NoAlias]`, AUP-local math, dispatcher dependency chaining, and debris quality gating.

Cinematic Cheats used: No WindZone, ConstantForce, fluid solver, Rigidbody impulse path, or broadphase geyser volume. Gameplay truth is analytic AUP-local cylinder/cone thrust; presentation receives scalar wake, heat, acoustic, seismic, blindness, and debris data. Low quality collapses debris and turbulence work before vent intersections; high quality restores the richer lanes continuously.

Exact Microseconds saved: No profiler capture. Low quality skips the mock debris vent loop entirely: up to `64 * ventCount` cylinder/cone evaluations per pass, 512 evaluations at 8 active vents. Dispatcher chaining removes the volcanic owner's hot fixed-batch completion point; synchronization stays at the master fixed bridge.

<SELF_AUDIT agent_id="SHINOBU_64" domain="Thermal Updrafts / Volcanic Geysers" task_count="20" pass="volcanic_xml_recheck_true_bottom">
  <task_reconciliation>
    <task id="01" status="PASS">No active `volcanic_vent_locations.h8bin` was found; emergency mock vents remain deterministic fallback data.</task>
    <task id="02" status="PASS">Static scan found no `WindZone`, `ConstantForce`, `Rigidbody`, `ForceMode`, `UnityEngine.Physics`, or `PhysicsForceRouter` in the volcanic touch set.</task>
    <task id="03" status="PASS">`VentStateDTO` and hot volcanic DTOs are public-field structs; no hot `{ get; set; }` pattern matched.</task>
    <task id="04" status="PASS">Runtime DTOs are 64-byte aligned layouts with no `Pack=1`.</task>
    <task id="05" status="PASS">`MockSubmarineArray` remains the direct velocity-injection proof lane.</task>
    <task id="06" status="PASS">Submarine path writes `SubmarineKinematicState.LinearVelocity` and `SubmarineForceAccumulator.LinearForceWorld`; mock submarine and mock leviathan lanes mutate `Velocity` fields directly.</task>
    <task id="07" status="PASS">Eruption oscillator is Burst/job based and driven by deterministic fixed simulation inputs.</task>
    <task id="08" status="PASS">Thermal blindness is scalar heat/visibility signal data, not simulated boiling fluid or trigger physics.</task>
    <task id="09" status="PASS">Debris chimney is active only above the continuous quality gate; weak quality skips debris vent intersections.</task>
    <task id="10" status="PASS">Real leviathan lane writes registered `AlphaLeviathanSteeringOutput.TargetRuntimeOffsetMeters` plus `VolcanicFloatStateSignal`; no nonexistent velocity buffer was invented.</task>
    <task id="11" status="PASS">`GlobalQualityWeight` uses `math.step`, `math.lerp`, and polynomial smoothing; debris intersections are bypassed below 0.3.</task>
    <task id="12" status="PASS">Acoustic roar remains scalar signal output for the audio lane.</task>
    <task id="13" status="PASS">Cylinder math subtracts `entityAup - vent.AUP` and casts the local delta to `float3` before dot/radius work.</task>
    <task id="14" status="PASS">Thermodynamics bridge is cold-cached and scalar, with no hot registry polling.</task>
    <task id="15" status="PASS">Vertical drag compensation remains bounded scalar integration math in the submarine updraft job.</task>
    <task id="16" status="PASS">Vent and CSV scratch vault buffers use `NativeArrayOptions.UninitializedMemory` where fully overwritten.</task>
    <task id="17" status="PASS">300-frame telemetry and `Dump_VOLCANO_SURGEON.bin` NaN dump path remain wired.</task>
    <task id="18" status="PASS">`Volcanic Updraft Tuner` editor facade remains present.</task>
    <task id="19" status="PASS">CSV ingest remains byte-scratch parsing with hash-token dispatch, no split/regex/LINQ hot path.</task>
    <task id="20" status="PASS">Editor gizmo cylinders/cones remain the visual proof of invisible thrust bounds.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <VentStateDTO size="64" alignment="16">0 AUP double3(24); 24 UpVector float3(12); 36 Radius float(4); 40 ThrustPower float(4); 44 EruptionTimer float(4); 48 _pad0 uint(4); 52 implicit gap(4); 56 _pad1 ulong(8). Total 64, 64 % 16 = 0.</VentStateDTO>
    <VolcanicUpdraftSettingsDTO size="64" alignment="16">Sixteen 4-byte lanes from MaxThrust through Flags. Total 64, 64 % 16 = 0.</VolcanicUpdraftSettingsDTO>
    <VolcanicUpdraftTelemetryEntry size="64" alignment="16">0 CurrentTargetAup double3(24); 24 AccumulatedLift float3(12); 36 FrameComputeMicros float(4); 40 StateHash uint(4); 44 Frame uint(4); 48/50/52/54 ushort counters; 56 Flags uint(4); 60 pad uint(4). Total 64, 64 % 16 = 0.</VolcanicUpdraftTelemetryEntry>
    <VolcanicUpdraftFrameCounter size="64" false_sharing="padded">Explicit 64-byte row; hot counter writes cannot share a smaller cache line.</VolcanicUpdraftFrameCounter>
  </struct_layout_verification>
  <scalability_curve_explanation>Below `GlobalQualityWeight` 0.3, `ResolveDebrisLiftWeight()` returns zero through `math.step(0.3, q) * SmoothStep(0.3, 1, q)` and the debris job never enters the vent-intersection loop. `ResolveTurbulenceGate()` uses the same gate, so updraft vectors collapse to strict +Y after the cheap cylinder/cone containment test. Middle, high, and ultra tiers restore turbulence, debris lift, wake density, thermal blindness, acoustic intensity, heat output, and leviathan riding by continuous scalar curves.</scalability_curve_explanation>
  <h_phi_vault_status persistent_private_native_collections="0">Vault IDs: 70750 Vents, 70751 Settings, 70752 Telemetry300, 70753 MockSubmarines, 70754 MockLeviathans, 70755 FloatSignals, 70756 DynamicWakes, 70757 MockFlowField, 70758 CsvScratchBytes, 70759 FrameCounters, 70760 MockDebris, 70761 PlayerHeat. Borrowed handles: PlayerKinematicState, AlphaLeviathanCognitionState, AlphaLeviathanSteeringOutput.</h_phi_vault_status>
  <pointer_aliasing_and_dependency_graph>`ScheduleFixedSimulation()` consumes dispatcher `dependsOn` plus pending submarine vent readers, then schedules reset -> eruption -> mock entity injection -> player injection -> leviathan injection -> VFX fakes -> telemetry. The final handle is registered with `H8Memory` and returned to the master fixed bridge. `ScheduleSubmarineInjection()` consumes pending vent writes and records read handles. Job arrays use `[NoAlias]`, with `[ReadOnly, NoAlias]` on read-only inputs.</pointer_aliasing_and_dependency_graph>
  <compile_guard>No new asmdef edge was introduced. The volcanic path stays in the existing `Hecton8.Core` compile surface and communicates through `GlobalRegistry`, `GlobalDataVault`, dispatcher fixed-system interface, vault handles, and signals. The retained AI DTO usage matches the existing registered unmanaged vault buffer types.</compile_guard>
  <dear_lie_confirmation>Before: O(physics_broadphase + entities * vents * solver_cost) force-field or fluid-style work, plus nondeterministic Unity scheduling. After: O(entities * activeVents) analytic cylinder/cone math, and O(1) debris lane under low quality because the vent loop is skipped. Visual overkill is scalar GPU-facing wake/heat/acoustic data.</dear_lie_confirmation>
  <verification>
    <static_scan status="PASS">No forbidden Unity force path, hot LINQ/foreach/string.Format, `UnityEngine.Random`, `Pack=1`, hot DTO property, private native collection field, or hot native allocation matched.</static_scan>
    <burst_scan status="PASS">All eight volcanic jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`; job arrays carry `[NoAlias]` or `[ReadOnly, NoAlias]`.</burst_scan>
    <dispatcher_scan status="PASS">`VolcanicUpdraftDirector` implements `IDispatcherFixedSystem`; no stale `_jobPending`, `IFixedTickable`, `IPostFixedTickable`, or legacy fixed/post-fixed registration remains.</dispatcher_scan>
    <cold_complete status="PASS">The only `.Complete()` in the volcanic director is cold `OnDisable()` teardown.</cold_complete>
    <log_order status="PASS">Misplaced near-top volcanic XML recheck block was removed; this entry is bottom-appended.</log_order>
    <build status="DEFERRED_BY_CPU_GUARD">Build not launched because latest guard sampled `CPU=73.7,88.6,86.8` with zero compiler processes; CPU remained above the 50 percent project threshold.</build>
  </verification>
</SELF_AUDIT>

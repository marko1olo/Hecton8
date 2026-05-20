# LOG_SHINOBU_133

## 2026-05-19 Sonar Cartography Audit Append

What was wrong:
- 3D map truth needed to be a dense 1-bit voxel payload, but the surrounding PDA map path still had legacy point-cloud visualization assumptions and no proven packed-R8 hologram bridge.
- The first packed upload seam was not enough; runtime quality had to clamp against `HomeostasisBrain.GlobalQualityWeight`, not only local Vault tuning.
- The pending sonar reveal lane could not remain a private `NativeQueue<MapRevealSignal>` without violating the Vault law.

What was done:
- Added Vault-backed cartography buffers `71420..71436` for discovery words, sector table, packed R8 upload staging, telemetry, tuning, scanner profiles, CSV scratch, fixed pending/mock pings, staged pending pings, pending counts, counters, active sector hashes, debug voxels, RLE runs, surface mask, and rollback snapshot.
- Added deterministic Burst jobs for clearing, initialization, atomic bit reveal, sphere reveal, mock sonar clusters, surface mask generation, packed-R8 formatting, telemetry, rollback snapshot, RLE staging, and editor debug voxel extraction.
- Added `Hecton_HologramMap.shader` and `PDAMapTab` packed-R8 `GraphicsBuffer` binding through `_CartographyVoxelR8`.
- Replaced pending map reveal `NativeQueue` with Vault `MockPings[16]` producer lane, `PendingPings[16]` dispatcher consumer lane, and `PendingSignalCounts[1]` input count. `CartographyCounterDTO.PendingSignalCount` is now telemetry snapshot only.
- Replaced the PDA map binary low/high overlay path with continuous quality budgets derived from `min(HomeostasisBrain.GlobalQualityWeight, CartographyTuningDTO.GlobalQualityWeight)`.

Cinematic cheats used:
- Fog-of-war truth is only 1 bit per voxel. The hologram shader fabricates wireframe cells, scanlines, chromatic offset, and flicker from packed R8 lanes instead of CPU mesh extraction.
- Terrain surface visualization is a bitmask shell seam. The cartography owner samples `SurfaceMaskWords` or deterministic mock SDF shell data instead of physics raycasts or mesh colliders.
- Runtime debug draws editor gizmo wire cubes from fixed debug DTOs only; no debug cube GameObjects are spawned.

Exact microseconds saved estimate:
- Avoided `Texture3D.SetPixels()/Apply()` managed color array copy: expected 500-3000 us per visual upload on low-end hardware, profiler proof pending.
- Avoided point-cloud/vector truth storage: prevents O(n) vector scans and managed growth; steady-state savings depend on discovered volume, profiler proof pending.
- Continuous overlay extraction: quality 0.1 uses word stride 8 and 1 emitted bit per word versus quality 1.0 stride 1 and 4 emitted bits; compute-side overlay work can drop by up to 8x on weak devices.
- Atomic 1-bit reveal path: one aligned 64-bit CAS lane per newly discovered voxel; avoids bool/Vector3 storage expansion by roughly 24-32 bytes per voxel.

<SELF_AUDIT agent_id="SHINOBU_133" domain="SONAR_CARTOGRAPHY_MAPPER" status="PENDING_COMPILE_RUNTIME_PROOF">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">No `List&lt;Vector3&gt;`/`HashSet&lt;Vector3&gt;` cartography truth remains in owned scan surface.</TASK>
    <TASK id="02" result="PASS">No `Texture3D.SetPixels()`/`Texture3D.Apply()` route remains in owned map update path.</TASK>
    <TASK id="03" result="PASS">Cartography DTOs use public fields; 3D native truth routes through Vault handles.</TASK>
    <TASK id="04" result="PASS">Explicit ARM64 layouts added and checked by `CartographyLayoutVerifier`.</TASK>
    <TASK id="05" result="PASS">Deterministic mock sonar cluster job exists for isolated renderer/profile testing.</TASK>
    <TASK id="06" result="PASS">`ApplySonarDiscoveryJob` uses deterministic Burst and aligned CAS atomic OR on `ulong` words.</TASK>
    <TASK id="07" result="PASS">AUP `double3` converts to deterministic integer macro cells before flat word/bit addressing.</TASK>
    <TASK id="08" result="PASS">Dear Lie shader route renders virtual hologram cells instead of real geometry.</TASK>
    <TASK id="09" result="PASS">Packed R8 staging uploads through `GraphicsBufferUploadUtility`/`LockBufferForWrite` seam.</TASK>
    <TASK id="10" result="PASS">Upload cadence and overlay extraction consume continuous effective quality; binary tier branch removed.</TASK>
    <TASK id="11" result="PASS">`SurfaceMaskWords` SDF-shell seam gates reveal bits; mock SDF fallback exists.</TASK>
    <TASK id="12" result="PASS">3x3 resident-sector constants and sector DTO table bound memory to nine sectors.</TASK>
    <TASK id="13" result="PASS">Rollback snapshot `ulong` buffer exists and DTOs are blittable.</TASK>
    <TASK id="14" result="PASS">RLE run DTO staging exists for save-compression owner handoff.</TASK>
    <TASK id="15" result="PASS">Vault buffers request uninitialized memory and are explicitly Burst-cleared.</TASK>
    <TASK id="16" result="PASS">300-entry 64-byte telemetry ring and binary dump route exist.</TASK>
    <TASK id="17" result="PASS">Editor tuner exists under editor asmdef and writes Vault-backed tuning.</TASK>
    <TASK id="18" result="PASS">Cold CSV parser writes fixed scanner profile DTO table without `string.Split`.</TASK>
    <TASK id="19" result="PASS">Editor gizmo reads bitmask words into fixed debug voxel DTOs.</TASK>
    <TASK id="20" result="PASS">Self-audit written; compile/runtime proof is blocked by CPU guard, not claimed.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="CartographySectorDTO" size="32" alignment="8">
      <FIELD offset="0" size="8" name="SectorHash" type="ulong" />
      <FIELD offset="8" size="4" name="BaseDataOffset" type="int" />
      <FIELD offset="12" size="4" name="DiscoveredVoxelCount" type="uint" />
      <FIELD offset="16" size="4" name="Flags" type="uint" />
      <FIELD offset="20" size="4" name="_pad0" type="uint" />
      <FIELD offset="24" size="8" name="_pad1" type="ulong" />
      <MATH>8+4+4+4+4+8=32; exact multiple of 16 and no Pack=1.</MATH>
    </STRUCT>
    <STRUCT name="CartographyCounterDTO" size="64" alignment="64_false_sharing_pad">
      <FIELD offset="0" size="4" name="Changed" type="int" />
      <FIELD offset="4" size="4" name="DiscoveredDelta" type="int" />
      <FIELD offset="8" size="4" name="Revision" type="uint" />
      <FIELD offset="12" size="4" name="LastBitIndex" type="uint" />
      <FIELD offset="16" size="8" name="LastSectorHash" type="ulong" />
      <FIELD offset="24" size="4" name="TotalDiscoveredVoxels" type="int" />
      <FIELD offset="28" size="4" name="PendingSignalCount" type="uint" />
      <FIELD offset="32" size="8" name="_padCounter1" type="ulong" />
      <FIELD offset="40" size="8" name="_padCounter2" type="ulong" />
      <FIELD offset="48" size="8" name="_padCounter3" type="ulong" />
      <FIELD offset="56" size="8" name="_padCounter4" type="ulong" />
      <MATH>4+4+4+4+8+4+4+8+8+8+8=64; one L1 cache line per counter.</MATH>
    </STRUCT>
    <STRUCT name="CartographyTelemetryEntry" size="64" alignment="8">
      <MATH>Grid longs 0/8/16, local floats 24/28/32, quality 36, frame/revision/last-bit/total 40/44/48/52, counts 56/58, state hash 60 = 64 bytes.</MATH>
    </STRUCT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Effective quality is `saturate(min(HomeostasisBrain.GlobalQualityWeight, VaultTuning.GlobalQualityWeight))`. Below 0.3, packed-R8 uploads stretch toward `math.lerp(1,60,1-quality)`, shader ray steps collapse toward 8, visual decimation keeps roughly 35-45 percent of set voxels, surface shell broadens for cheaper mock SDF, and point-cloud overlay extraction lerps toward word stride 8 with one emitted bit per word. At 1.0, upload cadence is every frame, shader ray steps reach 64, overlay stride is 1, and up to 4 bits per word are emitted.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <ZERO_PRIVATE_3D_ARRAYS>true</ZERO_PRIVATE_3D_ARRAYS>
    <NOTE>Pre-existing 2D PDA `NativeBitArray`/`NativeList&lt;int&gt;` chunk cache remains outside the 3D sonar cartography truth.</NOTE>
    <BUFFER id="71420" name="DiscoveryWords" />
    <BUFFER id="71421" name="SectorTable" />
    <BUFFER id="71422" name="UploadPackedR8" />
    <BUFFER id="71423" name="TelemetryRing" />
    <BUFFER id="71424" name="TelemetryCursor" />
    <BUFFER id="71425" name="Tuning" />
    <BUFFER id="71426" name="ScannerProfiles" />
    <BUFFER id="71427" name="CsvScratch" />
    <BUFFER id="71428" name="MockPings" />
    <BUFFER id="71429" name="Counters" />
    <BUFFER id="71430" name="ActiveSectorHashes" />
    <BUFFER id="71431" name="DebugVoxels" />
    <BUFFER id="71432" name="RleRuns" />
    <BUFFER id="71433" name="SurfaceMaskWords" />
    <BUFFER id="71434" name="RollbackSnapshotWords" />
    <BUFFER id="71435" name="PendingPings" />
    <BUFFER id="71436" name="PendingSignalCounts" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    <NO_ALIAS>All Burst job native array fields in cartography jobs are annotated with `[NoAlias]` where applicable; read-only sources use `[ReadOnly]`.</NO_ALIAS>
    <CONSUMES>Vault `DiscoveryWords`, `SurfaceMaskWords`, staged `PendingPings`, tuning, POI snapshots, frame counter, and caller quality scalar.</CONSUMES>
    <PRODUCES>`DiscoveryWords`, `Counters`, `UploadPackedR8`, `RollbackSnapshotWords`, `TelemetryRing`, `RleRuns`, and `DebugVoxels`.</PRODUCES>
    <RESOLVED_STATIC>The live bitmask mutation path now registers owner-local pre/sim/post `IDispatcherSystem` adapters and schedules `ApplyCartographyFrameDiscoveryJob.Schedule(dependsOn)`. Immediate `.Run()` calls remain only for cold boot clears, save/RLE/debug/upload staging, editor operations, and fallback `SlowTick()` when dispatcher registration is unavailable. Kahn graph runtime proof remains pending Unity import/profiler logs.</RESOLVED_STATIC>
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Cartography runtime asmdef references contracts/memory/math/burst/jobs only: no direct sibling runtime reference was found by static asmdef scan. CPU guard blocked compile: sampled CPU was 100 percent, with no active `dotnet` or `csc.exe` process.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The expensive path would be a discovered-voxel point cloud or cube mesh, O(n) managed/vector storage plus O(n) renderer or mesh build cost per visible update. The implemented path stores O(n/64) `ulong` truth, formats O(n/4) packed R8 lanes, and renders one hologram draw where the GPU fabricates wire cells and terminal artifacts. CPU geometry generation is removed.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Dispatcher Seam Polish Append

What was wrong:
- The first self-audit correctly flagged that the live cartography bitmask mutation route still lacked a master-dispatcher `JobHandle` seam.
- That left the Vault bitmask architecture strong but the execution phase contract weak: mutation could still happen as a synchronous `SlowTick().Run()` fallback in normal play.

What was done:
- Added `ApplyCartographyFrameDiscoveryJob`, a deterministic Burst frame kernel that combines player-cell reveal, fixed Vault pending sonar signals, surface mask gating, and counter reset/commit in one scheduled job.
- Added three owner-local `IDispatcherSystem` phase adapters inside `PlayerExplorationTracker`: pre-simulation stages AUP and pending count, simulation returns `job.Schedule(dependsOn)`, post-simulation consumes the false-sharing-padded counter and writes telemetry.
- Changed `SlowTick()` to exit when dispatcher registration succeeds, keeping synchronous mutation as a bootstrap fallback instead of the normal route.
- Split black-box signal counts so POI reveals are not double-counted as explicit sonar/acoustic pings.
- Split pending pings into `MockPings` producer lane, `PendingPings` dispatcher consumer lane, and `PendingSignalCounts` producer count to prevent `Counters[0]` races while the scheduled job writes discovery telemetry.

Cinematic cheats used:
- No new geometry, no mesh extraction, no additional real simulation. The dispatcher polish only moves the existing 1-bit truth mutation into the proper execution lane.

Exact microseconds saved estimate:
- Expected gain is not a fake numeric benchmark. The static improvement is removal of uncontrolled main-thread bitmask mutation in the registered dispatcher path; profiler proof is still pending because CPU guard blocked build/runtime validation.
- Pending-lane copy cost is capped at 16 `MapRevealSignal` structs per dispatcher frame and replaces an unbounded/racy producer-consumer counter seam.

# Procedural Coral Growth Engine



Date: 2026-05-19



Owner: SHINOBU_139



Status: STATIC SOURCE, COMPILE/RUNTIME PROOF PENDING



## Source Anchors



Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not coral generation runtime, shader import, Frame Debugger, profiler, GC, or player-build proof.



- `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs`



- `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs`



- `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralGpuUploadDispatcher.cs`



- `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralContracts.cs`



- `Assets/_Project/Scripts/World/ProceduralCoral/Hecton8.World.ProceduralCoral.asmdef`



- `Assets/_Project/Scripts/World/ProceduralCoral/Editor/ProceduralCoralTunerWindow.cs`



## Boundary



Coral growth ownership:

- Echelon: 2 World Generation.
- Owns: deterministic reef synthesis.
- Scope: integer L-System expansion, local collision constraints, render-matrix staging, bioluminescent pulse staging, collision proxy staging, telemetry.
- Does not own: VFX playback, Physics Apply, World Streaming, Save Archivist, shader variants, scene hierarchy creation.



## Data Route



- All persistent coral data is requested from `GlobalDataVault` through local `BufferID` values `71390..71409`.
- Generator writes `CoralBranchDTO`, camera-relative render matrices, `SyncPulseDTO`, `CapsuleColliderDTO`, `CoralGpuSwayDTO`, `CoralHzbTileDTO`, self-audit record, and telemetry ring.
- Dispatcher timing overwrites are finite-checked before entering the ring.
- Runtime generation creates no `GameObject` and does not call `Instantiate`.



- `CoralPaddedCounterDTO` is the logical-count, fault, and effective-quality authority.
- `BranchCount`, `SpatialCellCount`, `RenderMatrixCount`, `SyncPulseCount`, and `CollisionProxyCount` define the valid windows inside larger uninitialized Vault buffers; tail slots are stale and must not be consumed.
- `SpatialCellCount` is a compact live-cell window, not a branch-count alias after pruning.
- `EffectiveQualityWeight` at offset 60 carries the resolved sector-trigger/tuning quality through the downstream constraint, render, pulse, proxy, and self-audit jobs so exact `0.0f` remains a valid minimum-survival input.
- Self-audit results carry `Counter.FaultFlags` forward before adding audit-local faults.
- First hydration does not blanket-clear large buffers; it writes only small sentinel records, fallback rules, default tuning, and the default effective quality.
- Live branch/turtle math is finite-first: poisoned rotation/step/radius/local state is clamped or replaced before it can publish matrices, debug segments, sync pulses, collision proxies, or audit overlap payloads.


## Determinism



Authoritative persistence surface: `CoralSectorSaveDTO`.

- Fields: sector hash, deterministic seed, rule payload hash, flags.
- Branch arrays and matrices regenerate from seed/rules.
- Burst jobs use deterministic float mode because reef layout is rollback-visible.



## Rendering



Matrix extraction:

- Subtract camera AUP from branch AUP before float casting.
- HZB tile checks may cull occluded branches before matrix emission.
- Non-finite branch matrices are rejected.
- Radius clamps before HZB bias math.
- Dear Lie: CPU matrices stay stable.
- Shader sway input: `CoralGpuSwayDTO` flow amplitude, density, fault, frame scalars.



- The GPU upload dispatcher is no-grow by default during `UploadFromVault()`.
- Double-buffered `GraphicsBuffer` resources must be allocated in a cold prewarm path through `EnsureGraphicsResources()` unless an explicit `allowAllocation:true` call is documented by the integrator.
- Prewarm capacity is clamped to `MaxRenderMatrices`, and partial driver allocation failure releases all created buffers before returning false.
- `LockBufferForWrite` ranges are guarded by `try/finally` unlocks.
- The dispatcher clamps indirect instance counts against matrix capacity before upload, suppresses zero-instance draws, and finite-checks sway float4 lanes before shader global publication.
- The Vault `CoralGpuSwayDTO` carries the live reef sector hash plus state hash for debug/owner proof, while the current shader global route publishes only float4 sway vectors.


## Payloads



- `coral_growth_rules.h8bin` is searched in `Assets/StreamingAssets` first, then as a cold project-tree reconnaissance.
- If absent, `GenerateEmergencyMockCoralRules()` hydrates deterministic hardcoded integer rules.
- `coral_lsystem_rules.csv` is an editor/slow-tick tuning source parsed from a single Vault scratch byte window into unmanaged rule DTOs.
- CSV/H8BIN rule loading is transactional: parsed records stage into stack memory and commit to the live rule buffer only after at least one valid record exists.
- Rule angle, length, and radius scalars are finite-clamped on ingest and consumed per opcode by the interpreter; they are not decorative metadata.



Editor layout validation checks critical ABI offsets for branch, rule scalar, telemetry, counter, GPU sway, and self-audit DTO fields, not only total struct sizes.



## Proof Gaps



Unity import, Burst compile, Frame Debugger, profiler, runtime GC, shader variant warmup, and visual-route proof are pending. Runtime performance proof absent.

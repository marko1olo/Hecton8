# Flora Procedural Sway Field



Owner: `SHINOBU_124` / `FloraInteractionManager`.

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Source anchor: `Assets/_Project/Scripts/World/FloraInteractionManager.cs`.



Ambient current overlay owner: `SHINOBU_267` / `FloraAmbientSwayRuntime`.

Ambient source anchor: `Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs`.

Ambient runtime assembly: `Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef`.

Ambient editor assembly: `Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef`.

Shader anchor: `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`.

Binary payload ledger anchor: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` / `2026-05-21 SHINOBU_267 Flora Ambient Sway Payload Boundary`.



First 20 Minutes route binding:



- First 20 Minutes moment: world load and swim readability in the Copper Wire route biome.
- Required look: terrain, water, fog, lighting, nearby flora alive.
- Forbidden cost: CPU bones or per-flora `Update`.

- Route impact: removes a route blocker for early underwater traversal by replacing `SkinnedMeshRenderer`/`Animator`/CPU sway risk with one visual-only 32B CBuffer and alpha-tested shader deformation.

- Proof required: Unity import and Console; Play Mode or player run through selected route; 60-second profiler/GC capture.
- Render proof: Frame Debugger or RenderDoc evidence for `_GlobalFloraSway`; screenshot/clip from route gameplay.
- Persistence proof: save/load diff proving the visual-only lane does not enter route state.

- Parked work rejected: net-new flora ecology, gameplay harvesting logic, extra biome spread, CPU colliders for vegetation, and shader visual-overkill not captured on the selected route.



Submarine-to-flora bend is a visual displacement field, not physics interaction.

Vehicles and movers publish `WakeGeneratedSignal`; `FloraInteractionManager` resolves wake sources into Vault-owned 3D `FloraDisplacementDTO` field:



- `71650`: displacement nodes, 16 bytes each, `float3 ForceVector` at offset 0 and `float DecayTimer` at offset 12.

- `71651`: field metadata, center/cell/resolution/quality.

- `71652`: 300-frame black box ring.

- `71653`: unmanaged stiffness rules fallback/CSV target.

- `71654`: unmanaged CSV byte scratchpad.



The ambient current overlay is also visual-only and is excluded from save, rollback, and netcode truth. It owns these Vault buffers:



- `72900`: `FloraSwayParamsDTO`, 32 bytes, uploaded to shader CBuffer `_GlobalFloraSway`.

- `72901`: `FloraAmbientFlowStateDTO`, 32 bytes, decoupled mock/future Abyssal Flow bridge.

- `72902`: `SwayTelemetryEntry[300]`, 32 bytes each.

- `72903`: telemetry cursor.

- `72904`: `FloraSwayTuningDTO`, 32 bytes, cold/editor tuning.

- `72905`: `FloraBiomeSwayProfileDTO[64]`, unmanaged CSV profiles.

- `72906`: unmanaged CSV byte scratchpad.



- Biome profile CSV is a cold authoring bridge only.
- Player runtime does not read `StreamingAssets` or perform text file IO for this lane.
- Current `flora_biome_sway_profiles.csv` ingest is behind `UNITY_EDITOR`.
- Editor ingest reads `Docs/Data/Profiles/flora_biome_sway_profiles.csv`, parses bytes through `ReadOnlySpan<byte>`, and commits finite 32-byte profile DTOs into Vault.
- Cold profile/generic Vault clearing uses `UnsafeUtility.MemClear`, not NativeArray indexer setter loops.
- The eventual production source remains the project DataMonolith/static-data route without changing BufferIDs or shader ABI.



- `FloraAmbientSwayRuntime` registers one PRE_SIMULATION dispatcher system for `GenerateMockAmbientFlowJob` and `CalculateFloraSwayParametersJob`, and one VISUAL_SYNC adapter for double-buffered `GraphicsBuffer.Target.Constant` upload.

- Two PRE_SIMULATION kernels are one-row presentation jobs.
- They use cold-compiled Burst `FunctionPointer`s to avoid runtime `IJob.Run()`/same-frame fence debt.
- Proof retained: Burst entrypoints, `[NoAlias]`, XML Task 06 lock.
- Burst lock: `CompileSynchronously=true`, `FloatMode.Deterministic`, `FloatPrecision.Standard`.

- That deterministic visual-time route is excluded from save, WAL, rollback hashing, and gameplay authority.

- The upload path uses `LockBufferForWrite` and `UnsafeUtility.MemCpy`; it does not use `Shader.SetGlobalVector`, per-renderer material mutation, CPU bones, or per-flora `Update` loops.

- `IDataVault` replacement is event-driven through `IGlobalRegistryHotSwapListener`; old generation handles are released and cleared before cold reacquisition from the new vault.

- `FloraAmbientSwaySelfAudit.ownerPhasePurity` slices `PreSimulationTick` and `VisualSyncTick` and rejects hot `new`, `GlobalRegistry`, `File`, `.Run`, `.Complete`, or scene-search tokens inside those exact owner methods.

- `PreSimulationTick` does not poll `GlobalRegistry`, and `VisualSyncTick` does not allocate replacement `GraphicsBuffer` objects if cold bootstrap has not produced ready buffers.

- Runtime installation is scene-local.

- Authored placement is preferred; if absent, `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` subscribes `SceneManager.sceneLoaded` and creates one `H8_FloraAmbientSwayRuntime` host guarded by a static claim, `HideFlags.DontSave`, and no `DontDestroyOnLoad`.

- `SubsystemRegistration` unsubscribes the scene callback and clears the claim, so domain reload and scene reload do not leave stale static lifecycle state.

- This fallback is a cold lifecycle fence only and does not add per-frame scene searches or persistent root ownership.

- Compile-wall boundary: runtime assembly `Hecton8.World.FloraAmbientSway` has `autoReferenced=false`, `allowUnsafeCode=true`, and only Core/Bootstrap/Memory plus Unity Burst/Collections/Jobs/Mathematics references.
- No sibling domain assembly references.

- The editor facade assembly references SHINOBU_267 runtime plus public surfaces: `Hecton8.Core`, `Hecton8.Bootstrap.Contracts`, `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`.
- No sibling domain reference.

- All SHINOBU_267 script folders, `.cs`, and `.asmdef` assets carry explicit `.meta` GUIDs so Unity import identity is source-controlled instead of generated per workstation.

- Simulation time and frame IDs come from `DispatcherTimingDTO` only.

- The runtime does not read `UnityEngine.Time.deltaTime` or `Time.frameCount`; if dispatcher timing is absent, it uses a bounded owner-local fallback frame counter and `1/60f` visual fallback delta.

- Constant buffers are created during cold bootstrap; VISUAL_SYNC only validates and uploads, and does not allocate replacement GPU buffers in the steady frame path.



`FloraSwayParamsDTO` maps to `_GlobalFloraSway` as two float4 lanes:



- `GlobalFlowVector`: x/y/z normalized flow direction, w flow speed.

- `SwayMathParams`: x wrapped time via `fmod(t,1000)`, y amplitude, z effective spatial frequency, w continuous `GlobalQualityWeight`.



Ambient overlay ABI proof:

- Matrix: `FloraSwayParamsDTO`, `FloraAmbientFlowStateDTO`, `FloraSwayTuningDTO`, `FloraBiomeSwayProfileDTO`, `SwayTelemetryEntry`.
- `ValidateFloraSwayLayouts()`: size, minimum alignment, every `UnsafeUtility.GetFieldOffset` lane.
- Editor layout menu/self-audit: Params/Flow/Tuning/Telemetry/Profile sizes.
- Audit failure lanes: `layoutOffsetApi`, `layoutProofOutput`, `coldVaultMutation`.



- The Task 19 proof route is `FloraAnimationScanner` -> `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` -> `FloraAmbientSwaySelfAudit.reportProofArtifact`.
- The scanner upserts only the `shinobu_267_flora_ambient_sway` section.
- Report write path: `.tmp` + `.bak` + `File.Replace`.
- Preserved fields: `timestampUtc`, `activeViolationCount`, `findingCount`, scanned flora prefab/scene counts, evidence class, eradication boolean.



- The runtime folds editor `PhaseSpatialOffset` into `SwayMathParams.z`, preserving the mandated 32-byte CBuffer ABI instead of adding a third lane.
- The shader computes `sin(time + dot(worldPosition, flowDirection) * effectiveSpatialFrequency)` and scales the result by Vertex Color red stiffness and height.
- Final displacement gate: `smoothstep(0.1, 0.4, GlobalQualityWeight)`.
- Zero gate returns before `FastSinApprox`; weak devices fade to static silhouettes.
- Middle tier keeps cheap global sway; high tier keeps stronger spatial phase; ultra tier keeps the same route and spends saved CPU elsewhere.
- Non-finite quality input fail-closes to `0.0` in C# before CBuffer packing and again in shader-side quality resolvers, so corrupt thermal/scalability data cannot open the expensive shader path.
- The existing 3D interaction field remains additive in the vertex path, so submarine wake impulses and ambient current sway blend without changing gameplay authority.


- The ambient black-box dump format is explicit little-endian.
- `Dump_SHINOBU_267.bin` writes a 24-byte header.
- Header fields: `"S267"` magic, version, `TelemetrySourceHash`, row size, row count, cursor.
- Then serializes all 300 fixed 32-byte telemetry rows field-by-field.
- Float lanes use `math.asuint`; no `BinaryWriter`.



- Alpha-clipped morphology now samples `_FloraAlphaMask.a`, multiplies coverage, and performs an early `clip` before normal/light/caustic work in the indirect vegetation fragment path.
- A final post-necrosis clip remains as a safety clamp.
- This is still alpha-test, not alpha-blend; it exists to support torn kelp/grass texture edges without sort, extra geometry, or bone cost.
- The texture defaults to white, so authored materials without a mask preserve existing coverage.



- The field is generated by Burst gather jobs: `DecayFloraForcesJob`, `AccumulateFloraForcesJob`, optional `MockDisplacementInjectorJob`, and `UploadDisplacementTextureJob` for stats/upload readiness.
- Source positions are resolved from AUP by subtracting the quantized grid-origin AUP before casting to `float3`.
- Localized grid uses a toroidal ring offset.
- Center motion: quantized AUP delta to integer cell shift.
- Physical storage: modulo mapping.
- `DecayFloraForcesJob` clears only newly exposed wrapped rows/layers unless reset requires a full active-range clear.
- Shader receives `_HectonFloraSwayFieldRingOffset`, resolves it once per field-offset evaluation, and samples the same modulo mapping.
- Persistent wake energy stays spatially stable without physically shuffling the 64^3 buffer.
- Mock injector sanitizes prior cell values and re-clamps to the quality-scaled max displacement after synthetic force.
- CI/editor stress path cannot bypass production magnitude guard.
- The field is uploaded through double-buffered `GraphicsBuffer` staging and sampled by `Hecton_IndirectVegetation.shader` via `_HectonFloraSwayDisplacementField`.
- When this field is active, the shader fades out the old direct submarine sphere and direct player/interaction offsets to avoid double-bending.


Sway-frame metadata:

- Rejected source: Unity frame counter.
- Owner: `FloraInteractionManager`.
- Counters: sway simulation frames, wake-source signal stamps, wake-trail dispatch guards.
- Hot buffer resolve: cached boot-time Vault service.
- `GlobalRegistry.DataVault`: cold initial Vault handle acquisition only.



Wake source budgeting:

- Input: `HomeostasisBrain.GlobalQualityWeight` plus thermal stress.
- Active slots: smooth curve from `4` to `16`.
- `_GlobalWakeParams.y`: budget pressure.
- Rejected meaning: minimum-quality boolean.
- Consumers: shader/compute may lerp toward cheaper sampling under pressure.
- Rejected branch: hard hardware profile split.



- Disabling the field does not clear or upload the 64^3 node buffer on the main thread.
- Runtime clears only metadata and publishes inactive shader globals.
- If a flora field job is in flight, pending upload is marked for discard.
- Discard occurs after natural completion; no main-thread forced wait.
- Discard path writes a black-box event with `FloraSwayFieldDiscardedUploadFlag`.
- Pending ring offset and center-shift cells are preserved until event recording.
- Postmortems can distinguish discarded stale upload from normal quiet frame.
- Stale node values are ignored while inactive and are reset inside the Burst decay pass when the next valid resolution/origin schedule starts.


- Scalability is driven by continuous `HomeostasisBrain.GlobalQualityWeight`: source count, update interval, displacement gain, and shader interpolation scale immediately with current quality.
- Resolution and cell size use a small owner-local layout-quality hysteresis band (`0.035`) before rebuilding the field topology, preventing profiler/thermal micro-jitter from repeatedly resetting or changing upload shape.
- The resulting range is still 16^3 nearest-neighbor survival cost to 64^3 trilinear visual-overkill.
- The scheduler maps the same curve from exact 5Hz thermal-survival cadence (`0.2s`) to exact 60Hz visual-overkill cadence (`1f / 60f`).
- No per-blade collider, trigger, Rigidbody, `Physics.OverlapSphere`, or large-flora collider proxy path is used for procedural sway.
- The old large-flora partial lane is now `HectonMapMagicVegetationBridgeFloraVisualSway.cs`; its methods are lifecycle no-ops so MapMagic vegetation does not generate PhysX representation for sway.


ARM64 layout proof in the touched lane:



- `FloraDisplacementDTO`: 16B, `ForceVector` offset 0, `DecayTimer` offset 12.

- `FloraSwayFieldTelemetryEntry`: 64B, explicit offsets, 300-entry Vault ring.

- Consumed `WakeSource`: 128B explicit layout, manual `uint` padding at offsets 108, 112, 116, 120, and 124; no `Pack=1`.

- Consumed `WakeTelemetryEntry`: 64B explicit layout, byte 60 is `BudgetPressure01`, no `Pack=1`.

- Legacy adjacent `ParasiteNode` and `AbyssalPathTelemetryEntry` are explicit 64B layouts; no `Pack=1` remains in the touched runtime source lane.



Editor-only ABI validators cover:

- Owned `FloraDisplacementDTO`.
- Consumed `WakeSource`.
- Consumed `WakeTelemetryEntry`.
- Checks: `UnsafeUtility.SizeOf` and field offsets.
- Build fence: validation methods and reflection helper stay inside `#if UNITY_EDITOR`.
- Drift response: UI Toolkit tuner logs a hard error.



- On invalid input, upload stall, or NaN, the last 300 field frames dump to `Docs/AgentLogs/Dump_FLORA_SWAY_DIRECTOR.bin`.
- Telemetry ABI remains a 64B entry.
- Flags distinguish reset, toroidal wrapped-shift, and discarded-upload frames.
- State hash mixes current/pending ring offset plus center-shift cells.
- Postmortems can separate modulo recentering, full reset churn, and intentionally skipped stale uploads.



Editor facade: `Tools/Hecton-8/Procedural Flora Sway Tuner`.

Controls: decay, current, mass, mock, gizmo, 10Hz readout.

The max-magnitude label uses a cold precomputed string cache. Secondary resolution/cell detail text updates only on editor value changes.



Indirect vegetation culling:

- Owner: `HectonIndirectVegetationRenderer`.
- Non-owner: sway field.
- Depth route: build `__HectonVegetationDepthPyramid`.
- Compute bind: `_HectonDepthPyramid` into `FloraCulling.compute`.
- Cull: reject occluded instances before visible-ID append.
- Count copy: `GraphicsBuffer.CopyCount` into indirect argument buffers.
- Submit: `Graphics.RenderMeshIndirect`.
- Rejected routes: duplicate CPU HZB readback, direct renderer dependency.

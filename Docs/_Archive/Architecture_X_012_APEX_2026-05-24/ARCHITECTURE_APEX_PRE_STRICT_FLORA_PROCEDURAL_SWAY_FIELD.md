# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/FLORA_PROCEDURAL_SWAY_FIELD.md
Rule: historical snapshot only; not active doctrine.

# Flora Procedural Sway Field

Owner: `SHINOBU_124` / `FloraInteractionManager`.
Source anchor: `Assets/_Project/Scripts/World/FloraInteractionManager.cs`.

Ambient current overlay owner: `SHINOBU_267` / `FloraAmbientSwayRuntime`.
Ambient source anchor: `Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs`.
Ambient runtime assembly: `Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef`.
Ambient editor assembly: `Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef`.
Shader anchor: `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`.
Binary payload ledger anchor: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` / `2026-05-21 SHINOBU_267 Flora Ambient Sway Payload Boundary`.

First 20 Minutes route binding:

- First 20 Minutes moment: World load and swim readability on the selected Copper Wire route biome, where terrain, water, fog, lighting, and nearby flora must look alive without CPU-bone or per-flora `Update` cost.
- Route impact: removes a route blocker for early underwater traversal by replacing `SkinnedMeshRenderer`/`Animator`/CPU sway risk with one visual-only 32B CBuffer and alpha-tested shader deformation.
- Proof required: Unity import and Console, Play Mode or player run through the selected route, 60-second profiler/GC capture, Frame Debugger or RenderDoc evidence for `_GlobalFloraSway`, screenshot/clip from route gameplay, and save/load diff proving the visual-only lane does not enter route state.
- Parked work rejected: net-new flora ecology, gameplay harvesting logic, extra biome spread, CPU colliders for vegetation, and shader visual-overkill not captured on the selected route.

The submarine-to-flora bend path is a visual displacement field, not a physics interaction. Vehicles and other movers publish `WakeGeneratedSignal`; `FloraInteractionManager` resolves those wake sources into a Vault-owned 3D `FloraDisplacementDTO` field:

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

Biome profile CSV is a cold authoring bridge only. Player runtime does not read `StreamingAssets` or perform text file IO for this lane; the current `flora_biome_sway_profiles.csv` ingest is wrapped behind `UNITY_EDITOR`, reads `Docs/flora_biome_sway_profiles.csv`, parses bytes through `ReadOnlySpan<byte>`, and commits finite 32-byte profile DTOs into Vault through pointer-offset `UnsafeUtility.AsRef<FloraBiomeSwayProfileDTO>` writes. Cold profile/generic Vault clearing uses `UnsafeUtility.MemClear`, not NativeArray indexer setter loops. The eventual production source remains the project DataMonolith/static-data route without changing BufferIDs or shader ABI.

- `FloraAmbientSwayRuntime` registers one PRE_SIMULATION dispatcher system for `GenerateMockAmbientFlowJob` and `CalculateFloraSwayParametersJob`, and one VISUAL_SYNC adapter for double-buffered `GraphicsBuffer.Target.Constant` upload.
- The two PRE_SIMULATION kernels are one-row presentation jobs invoked through cold-compiled Burst `FunctionPointer`s to avoid ordinary runtime `IJob.Run()`/same-frame fence debt while still proving Burst entrypoints, `[NoAlias]` source proof, and the XML Task 06 Burst lock: `CompileSynchronously=true`, `FloatMode.Deterministic`, `FloatPrecision.Standard`.
- That deterministic visual-time route is excluded from save, WAL, rollback hashing, and gameplay authority.
- The upload path uses `LockBufferForWrite` and `UnsafeUtility.MemCpy`; it does not use `Shader.SetGlobalVector`, per-renderer material mutation, CPU bones, or per-flora `Update` loops.
- `IDataVault` replacement is event-driven through `IGlobalRegistryHotSwapListener`; old generation handles are released and cleared before cold reacquisition from the new vault.
- `FloraAmbientSwaySelfAudit.ownerPhasePurity` slices `PreSimulationTick` and `VisualSyncTick` and rejects hot `new`, `GlobalRegistry`, `File`, `.Run`, `.Complete`, or scene-search tokens inside those exact owner methods.
- `PreSimulationTick` does not poll `GlobalRegistry`, and `VisualSyncTick` does not allocate replacement `GraphicsBuffer` objects if cold bootstrap has not produced ready buffers.
- Runtime installation is scene-local.
- Authored placement is preferred; if absent, `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` subscribes `SceneManager.sceneLoaded` and creates one `H8_FloraAmbientSwayRuntime` host guarded by a static claim, `HideFlags.DontSave`, and no `DontDestroyOnLoad`.
- `SubsystemRegistration` unsubscribes the scene callback and clears the claim, so domain reload and scene reload do not leave stale static lifecycle state.
- This fallback is a cold lifecycle fence only and does not add per-frame scene searches or persistent root ownership.
- Compile-wall boundary: the runtime is isolated in `Hecton8.World.FloraAmbientSway.asmdef` with `autoReferenced=false`, `allowUnsafeCode=true`, references limited to `Hecton8.Core`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics, with no sibling domain assembly references.
- The editor facade is isolated in `Hecton8.World.FloraAmbientSway.Editor.asmdef` and references the SHINOBU_267 runtime assembly plus direct public-surface dependencies `Hecton8.Core`, `Hecton8.Bootstrap.Contracts`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`; it has no sibling domain reference.
- All SHINOBU_267 script folders, `.cs`, and `.asmdef` assets carry explicit `.meta` GUIDs so Unity import identity is source-controlled instead of generated per workstation.
- Simulation time and frame IDs come from `DispatcherTimingDTO` only.
- The runtime does not read `UnityEngine.Time.deltaTime` or `Time.frameCount`; if dispatcher timing is absent, it uses a bounded owner-local fallback frame counter and `1/60f` visual fallback delta.
- Constant buffers are created during cold bootstrap; VISUAL_SYNC only validates and uploads, and does not allocate replacement GPU buffers in the steady frame path.

`FloraSwayParamsDTO` maps to `_GlobalFloraSway` as two float4 lanes:

- `GlobalFlowVector`: x/y/z normalized flow direction, w flow speed.
- `SwayMathParams`: x wrapped time via `fmod(t,1000)`, y amplitude, z effective spatial frequency, w continuous `GlobalQualityWeight`.

Ambient overlay ABI proof is a five-DTO matrix. `ValidateFloraSwayLayouts()` checks size, minimum alignment, and every `UnsafeUtility.GetFieldOffset` lane for `FloraSwayParamsDTO`, `FloraAmbientFlowStateDTO`, `FloraSwayTuningDTO`, `FloraBiomeSwayProfileDTO`, and `SwayTelemetryEntry`. The editor layout menu and self-audit report measured Params/Flow/Tuning/Telemetry/Profile sizes; `layoutOffsetApi`, `layoutProofOutput`, and `coldVaultMutation` fail the audit if the offset route, measured proof output, or direct-memory Vault mutation route drifts.

The Task 19 proof route is `FloraAnimationScanner` -> `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` -> `FloraAmbientSwaySelfAudit.reportProofArtifact`. The scanner upserts only the `shinobu_267_flora_ambient_sway` section through a `.tmp` + `.bak` `File.Replace` report write and preserves `timestampUtc`, `activeViolationCount`, `findingCount`, scanned flora prefab/scene counts, evidence class, and eradication boolean, so editor validation cannot silently downgrade the shared report artifact.

The runtime folds editor `PhaseSpatialOffset` into `SwayMathParams.z`, preserving the mandated 32-byte CBuffer ABI instead of adding a third lane. The shader computes `sin(time + dot(worldPosition, flowDirection) * effectiveSpatialFrequency)` and scales the result by Vertex Color red stiffness and height. Final displacement is gated by `smoothstep(0.1, 0.4, GlobalQualityWeight)`: at a zero gate the function returns before `FastSinApprox`, weak devices fade to static silhouettes, middle tier keeps cheap global sway, high tier keeps stronger spatial phase, and ultra tier keeps the same route while spending saved CPU elsewhere. Non-finite quality input fail-closes to `0.0` in C# before CBuffer packing and again in shader-side quality resolvers, so corrupt thermal/scalability data cannot accidentally open the expensive shader path. The existing 3D interaction field remains additive in the vertex path, so submarine wake impulses and ambient current sway blend without changing gameplay authority.

The ambient black-box dump format is explicit little-endian. `Dump_SHINOBU_267.bin` writes `"S267"` magic, version, `TelemetrySourceHash`, row size, row count, and cursor as a 24-byte header, then serializes all 300 fixed 32-byte telemetry rows field-by-field; float lanes are converted with `math.asuint`, and no `BinaryWriter` route is used.

Alpha-clipped morphology now samples `_FloraAlphaMask.a`, multiplies coverage, and performs an early `clip` before normal/light/caustic work in the indirect vegetation fragment path. A final post-necrosis clip remains as a safety clamp. This is still alpha-test, not alpha-blend; it exists to support torn kelp/grass texture edges without sort, extra geometry, or bone cost. The texture defaults to white, so authored materials without a mask preserve existing coverage.

The field is generated by Burst gather jobs: `DecayFloraForcesJob`, `AccumulateFloraForcesJob`, optional `MockDisplacementInjectorJob`, and `UploadDisplacementTextureJob` for stats/upload readiness. Source positions are resolved from AUP by subtracting the quantized grid-origin AUP before casting to `float3`. The localized grid uses a toroidal ring offset: center motion is converted from quantized AUP delta to integer cell shift, physical storage is addressed through modulo mapping, and `DecayFloraForcesJob` clears only newly exposed wrapped rows/layers unless resolution/cell-size/large-jump reset requires a full active-range reset. The shader receives `_HectonFloraSwayFieldRingOffset`, resolves it once per field-offset evaluation, and samples the same modulo mapping, so persistent wake energy stays spatially stable without physically shuffling the 64^3 buffer. The mock injector sanitizes prior cell values and re-clamps to the same quality-scaled max displacement after adding synthetic force, so the CI/editor stress path cannot bypass the production magnitude guard. The field is uploaded through double-buffered `GraphicsBuffer` staging and sampled by `Hecton_IndirectVegetation.shader` via `_HectonFloraSwayDisplacementField`. When this field is active, the shader fades out the old direct submarine sphere and direct player/interaction offsets to avoid double-bending.

Sway-frame metadata no longer reads Unity's frame counter. `FloraInteractionManager` owns monotonic local counters for sway simulation frames, wake-source signal stamps, and wake-trail dispatch guards. Hot field-buffer resolves use the cached Vault service obtained during boot; `GlobalRegistry.DataVault` is only used in cold handle acquisition for the initial Vault request.

Wake source budgeting is continuous. The procedural wake lane derives a budget weight from `HomeostasisBrain.GlobalQualityWeight` plus thermal stress, then maps the active wake-slot count between 4 and 16 with a smooth curve. `_GlobalWakeParams.y` is budget pressure, not a minimum-quality boolean; shader/compute consumers may lerp toward cheaper sampling under pressure without a hard hardware profile branch.

Disabling the field does not clear or upload the 64^3 node buffer on the main thread. The runtime clears only metadata and publishes inactive shader globals; if a flora field job is still in flight, the pending upload is marked for discard and skipped after natural completion instead of forcing a main-thread wait. The discard path writes a black-box event with `FloraSwayFieldDiscardedUploadFlag`; pending ring offset and center-shift cells are preserved until that event is recorded, so postmortems can separate a deliberately discarded stale upload from a normal quiet frame. Stale node values are ignored while inactive and are reset inside the Burst decay pass when the next valid resolution/origin schedule starts.

Scalability is driven by continuous `HomeostasisBrain.GlobalQualityWeight`: source count, update interval, displacement gain, and shader interpolation scale immediately with current quality. Resolution and cell size use a small owner-local layout-quality hysteresis band (`0.035`) before rebuilding the field topology, preventing profiler/thermal micro-jitter from repeatedly resetting or changing upload shape. The resulting range is still 16^3 nearest-neighbor survival cost to 64^3 trilinear visual-overkill. The scheduler maps the same curve from exact 5Hz thermal-survival cadence (`0.2s`) to exact 60Hz visual-overkill cadence (`1f / 60f`). No per-blade collider, trigger, Rigidbody, `Physics.OverlapSphere`, or large-flora collider proxy path is used for procedural sway. The old large-flora partial lane is now `HectonMapMagicVegetationBridgeFloraVisualSway.cs`; its methods are lifecycle no-ops so MapMagic vegetation does not generate PhysX representation for sway.

ARM64 layout proof in the touched lane:

- `FloraDisplacementDTO`: 16B, `ForceVector` offset 0, `DecayTimer` offset 12.
- `FloraSwayFieldTelemetryEntry`: 64B, explicit offsets, 300-entry Vault ring.
- Consumed `WakeSource`: 128B explicit layout, manual `uint` padding at offsets 108, 112, 116, 120, and 124; no `Pack=1`.
- Consumed `WakeTelemetryEntry`: 64B explicit layout, byte 60 is `BudgetPressure01`, no `Pack=1`.
- Legacy adjacent `ParasiteNode` and `AbyssalPathTelemetryEntry` are explicit 64B layouts; no `Pack=1` remains in the touched runtime source lane.

Editor-time validators now cover the owned `FloraDisplacementDTO` and the consumed wake ABI (`WakeSource` and `WakeTelemetryEntry`) through `UnsafeUtility.SizeOf` plus field-offset checks. The validation methods and their reflection helper are wrapped in `#if UNITY_EDITOR`, so player builds do not carry runtime reflection. The UI Toolkit tuner logs a hard error if any of these offsets drift.

On invalid input, upload stall, or NaN, the last 300 field frames dump to `Docs/AgentLogs/Dump_FLORA_SWAY_DIRECTOR.bin`. The telemetry ABI remains a 64B entry; reset, toroidal wrapped-shift, and discarded-upload frames are distinguished through flags, and the state hash mixes the current or pending ring offset plus center-shift cells so postmortems can separate normal modulo recentering, full reset churn, and intentionally skipped stale uploads.

Editor facade: `Tools/Hecton-8/Procedural Flora Sway Tuner` exposes decay/current/mass/mock/gizmo controls and a 10Hz readout. The max-magnitude label uses a cold precomputed string cache; secondary resolution/cell detail text updates only on editor value changes and is not part of the player hot path.

Indirect vegetation culling remains owned by `HectonIndirectVegetationRenderer`, not by the sway field. The current static route builds `__HectonVegetationDepthPyramid`, binds `_HectonDepthPyramid` into `FloraCulling.compute`, rejects occluded instances before appending visible IDs, copies append counts into indirect argument buffers with `GraphicsBuffer.CopyCount`, and submits through `Graphics.RenderMeshIndirect`. SHINOBU_124 does not add a duplicate CPU HZB readback or direct renderer dependency.

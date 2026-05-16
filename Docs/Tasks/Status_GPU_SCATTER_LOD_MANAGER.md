# GPU_SCATTER_LOD_MANAGER Status

Prompt: GPU_SCATTER_LOD_MANAGER
Domain: RENDERING/BRG
Task count: 18
Status: PENDING VERIFICATION - BASELINE COMPILE BLOCKED

## Mandates Identified

- REND_GPU_Sovereignty
- REND_GPU_Occlusion_Culling_6000
- REND_Instanced_Flora_Physics
- REND_URP_Graphics_HotPath_Optimization_HLOD
- GPU_Compute_Kernels_Kernels_Optimization_MX350
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- MATH_AUP_Determinism_Sync

## State Machine

- [x] Task 1 PURGE_SINGLETONS | DOD: `rg "FloraManager\.Instance" Assets/_Project/Scripts Assets/_Project/Art` returned no production references; alternative rejected: adding a compatibility singleton would preserve forbidden CPU-manager coupling; estimate 0us hot path saved here, avoids future singleton lookup debt.
- [x] Task 2 DEBT_CLEANUP | DOD: `rg "Instantiate\s*\(\s*KelpPrefab\s*\)" Assets/_Project/Scripts Assets/_Project/Art` returned no production references; alternative rejected: pooling a forbidden KelpPrefab path still keeps GameObject flora; estimate 0us hot path saved here, blocks regression.
- [x] Task 3 DATA_EVICTION | DOD: added `BufferID.FloraScatterMatrices`/metadata/motion IDs and renderer reads `NativeArray<Matrix4x4>` from `IDataVault`; alternative rejected: direct OSHINO class dependency or MonoBehaviour list handoff; estimate 900-1800us CPU transform/manager overhead avoided at 100k instances on i3/MX350.
- [x] Task 4 BURST_ALGORITHM | DOD: implemented `ScatterCullJob : IJobParallelFor` audit using `math.dot` against 6 planes and OBB-projected matrix bounds, plus GPU kernel `ScatterCullJob`; alternative rejected: CPU per-frame main-thread cull; estimate audit disabled in hot path, GPU cull cost expected below CPU cull by >1000us at 100k.
- [x] Task 5 AUP_INTEGRITY | DOD: compute and Burst paths add `AupShiftOffset` before frustum/distance culling, material receives `_GlobalFloatingOffset`; alternative rejected: rebaking vault matrices on every origin shift; estimate saves 6400KB matrix rewrite per shift.
- [x] Task 6 DOD_SOA_LAYOUT | DOD: compute kernel appends `HectonScatterVisibleMatrix` into `_HectonScatterVisibleMatrices` and visible indices into append buffers; alternative rejected: CPU compacted matrix array; estimate avoids 6400KB CPU compact/upload per frame at 100k.
- [x] Task 7 SIGNAL_FLOW | DOD: renderer consumes `SignalBus<CameraFrustumSignal>` and falls back to signal-built frustum planes when no Camera is bound; alternative rejected: `Camera.main` and scene search; estimate 0B GC and avoids camera lookup debt.
- [x] Task 8 LOW_TIER_FAKE | DOD: low/MX350 path clamps cull distance to 100m with hysteresis; alternative rejected: full 500m residency on MX350; estimate saves GPU append/shader work proportional to far vegetation density.
- [x] Task 9 HIGH_END_OVERKILL | DOD: High/Ultra path uses 500m cull distance and writes crossfade range to material; alternative rejected: hard pop-only high tier; estimate spends saved CPU on visual residency, not accuracy.
- [x] Task 10 REACTIVE_VFX | DOD: no C# reactive VFX hook added; shader-owned metadata/motion/vector buffers are bound only; alternative rejected: duplicating VFX authority in renderer; estimate 0us.
- [x] Task 11 STP_STABILIZATION | DOD: compute writes `_HectonScatterMotionVectors[index]` from deterministic sway direction/hash and binds `_HectonFloraMotionVectors`; alternative rejected: CPU per-instance sway integration; estimate avoids 100k CPU sin/transform updates.
- [x] Task 12 NAN_VACCINATION | DOD: C# upload rejects non-finite matrices and dumps blackbox; compute/Burst reject non-finite or zero-scale matrices before append; alternative rejected: trusting producer data; estimate fault path only, hot path branch is GPU-side.
- [x] Task 13 BLACKBOX_LOGGING | DOD: fixed 300-entry `NativeArray<ScatterBlackBoxEntry>` logs `VisibleFloraCount`, active count, cull distance, stress, camera, AUP, generations; alternative rejected: `Debug.Log` telemetry; estimate 0B/frame managed GC.
- [BLOCKED BY DEPENDENCY] Task 14 TRIPLE_STRIKE_REPAIR | DOD: three compile attempts hit pre-existing project dependency wall (`Hecton8.Core.csproj` missing interfaces/types such as `ISimulationBucketer`, `IMacroDatabaseService`, `IPlayerMovementContracts`); filtered builds show no `GpuScatter`/`FloraScatter` errors; alternative rejected: reverting requested scatter implementation without evidence it broke compile; estimate 0us, integrator action required.
- [x] Task 15 HOMEOSTASIS_ADAPTATION | DOD: consumes `SystemHealthSignal` plus public `SetSystemStress01`; if stress > 0.8 desired cull distance is reduced by 50% through hysteresis; alternative rejected: immediate hard capacity drop with flicker; estimate sheds far flora GPU work during pressure.
- [x] Task 16 INDIRECT_ARGS | DOD: `GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint))` writes indirect instance count from append buffer; alternative rejected: CPU `GetData` or managed count readback; estimate avoids blocking GPU/CPU sync.
- [x] Task 17 MEMORY_SENTINEL | DOD: `OnDisable`/`OnDestroy` release all `GraphicsBuffer`s, CPU audit NativeArrays, blackbox NativeArray, and invalidates Vault lease; alternative rejected: scene-global static buffers; estimate prevents VRAM/native retention on scene unload.
- [BLOCKED BY DEPENDENCY] Task 18 FINAL_VALIDATION | DOD: `dotnet build Assembly-CSharp.csproj --no-restore` and isolated variants attempted; blocked by pre-existing missing dependency contracts outside scatter domain; filtered build found no scatter errors; alternative rejected: inventing cross-domain stubs; estimate 0us runtime, integration blocked.

## Iteration Log

1. Created status from extracted XML prompt. No code changed yet.
2. Loop 1 Tasks 1-5: implemented Vault matrix handoff, `ScatterCullJob`, AUP-before-cull. Compile checkpoint: `dotnet build Assembly-CSharp.csproj --no-restore` hit pre-existing `Hecton8.Core.csproj` missing interface/type dependencies before a full project verdict; filtered `dotnet build Hecton8.Core.csproj` output produced no `GpuScatter` errors.
3. Loop 2 Tasks 6-10: implemented append-visible matrix stream, `CameraFrustumSignal` consumption, 100m low-tier and 500m high-tier/crossfade lanes, and preserved shader-owned reactive VFX. Compile checkpoint repeated with filtered `Hecton8.Core.csproj`; no `GpuScatter`/`FloraScatter` errors surfaced before existing dependency wall.
4. Loop 3 Tasks 11-15: implemented motion-vector buffer, NaN/zero-scale guards, blackbox ring, homeostasis 50% shed, and recorded compile wall as dependency-blocked after repeated build attempts. Integrator note: project baseline must restore missing Hecton8.Core contracts before final `dotnet build` can produce a full verdict.
5. Loop 4 Tasks 16-18: verified CopyCount indirect args and teardown paths. Final validation remains dependency-blocked by baseline project compile errors outside rendering scatter.
6. Loop 5 Omega/self-review: `<POLISH_MANDATE>` tag was absent from `CURRENT_BATCH.md`; performed anti-bloat scan anyway. Fixed shader keyword from `_HECTON_GPU_INDIRECT` to `HECTON_GPU_INDIRECT` and widened fallback draw bounds to match active cull distance. Self-review grep found no `Camera.main`, scene find, coroutine, or Update/LateUpdate/FixedUpdate in scatter manager.

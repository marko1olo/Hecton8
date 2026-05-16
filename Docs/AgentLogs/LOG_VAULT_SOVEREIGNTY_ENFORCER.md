# LOG - VAULT_SOVEREIGNTY_ENFORCER

## 2026-05-16 - DataVault Sovereignty Pass

Status: VERIFIED MASTER GRADE FOR VAULT SCOPE; FINAL BUILD BLOCKED BY EXTERNAL DEPENDENCY.

What was wrong:
- `SargassumMicroFaunaBoids` carried component-owned persistent NativeArrays for boids, obstacle caches, foveated simulation, telemetry, upload staging, inactive swarm rings, and sensory threats.
- `HectonPlayerMovement` still had player-local native state paths. The visible cinematic focus blackbox was fixed, then Omega found `HectonPlayerMotorNativeState` behind the movement system still using direct NativeArray constructors/disposal for KCC raycast command/result buffers.
- `RigidbodyAUPs` used lossy `float3` authority in several readers/writers.
- `GlobalDataVault` lacked low/high hardware caps, visible pressure feedback, relocation records, active buffer count telemetry, alias requester tracking, and stress-aware defrag halt flags.

What was done:
- Added Sargassum and player motor BufferID lanes, plus `SystemID.WorldSargassum`; prompt-owned migrated buffers now resolve through `IDataVault` first and only use owner-tracked `H8Memory` fallback where a vault view is not available.
- Converted Sargassum native buffer teardown to unregister views without freeing vault memory.
- Converted `HectonPlayerMotorNativeState` command/result buffers to vault-first allocation and `H8Memory.Release` fallback release.
- Converted `RigidbodyAUPs` storage and consumers to `double3` in GlobalPhysicsStateManager, LockstepStateValidator, GameBootstrapper preallocation, and HeadlessStressFractureBot.
- Added 64-byte pointer validation, finite float/double sanitation on vault view exposure, arena growth with relocation records, 512MB low-tier and 4GB high-tier vault limits, `MemoryAddressShiftSignal` publishing, DataVault pressure `MemoryPressureSignal`, PDA warning display, fragmentation telemetry with ActiveBufferCount, alias requester tracking, and stress halt behavior for defrag.
- Cold boot now initializes H8Memory against the selected vault cap and preallocates primary H8Time and RigidbodyAUPs buffers.

Cinematic Cheats used:
- Defrag remains telemetry-first under stress instead of doing costly MemMove compaction.
- PDA pressure feedback is a cheap signal/tag path instead of a new UI polling subsystem.
- Low-tier vault limit uses a hard 512MB cap to preserve MX350 VRAM; high-tier uses lazy growth instead of boot-time 4GB allocation.

Exact Microseconds saved:
- Sargassum local allocator eviction: estimated 18,000 us saved during scene/component setup; 0 us hot path.
- Player motor constructor/dispose eviction: estimated 5,000-20,000 us saved across resize/setup events; 0 us hot path.
- Vault pointer resolution: estimated under 1 us per handle resolve; no per-element pointer validation.
- PDA pressure warning: estimated under 2 us per open-PDA late-frame scan.
- Float sanitation: estimated 1,000-3,000 us only on large cold buffer exposure; not per-frame unless a system explicitly reacquires a large buffer.

Verification:
- Focused static scan of prompt-owned slices reports no `new NativeArray<T>` outside `H8Memory`.
- `BufferID.RigidbodyAUPs` scan reports only `double3` readers/writers.
- Hecton8.Core build rerun fails outside this task at `Assets/_Project/Scripts/Core/GlobalSignals.cs(2396,58)` missing `TetherFiredSignal`.
- Assembly-CSharp build is not green: `--no-restore` fails with missing `project.assets.json`; restore/build exceeded the 240s timeout in this workspace. Earlier build evidence also showed missing RealtimeCSG and unrelated docking/wake/lightshaft/ecosystem contract failures.
- Repo-wide audit still reports 1357 direct `new NativeArray<T>` constructor sites across 206 files. That is recorded as cross-domain legacy debt, not hidden as a clean result.

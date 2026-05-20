# SHINOBU_149 Dynamic Deferred Decals

Date: 2026-05-19
Owner: SHINOBU_149
Domain: Echelon 8 Presentation and UX, dynamic screen-space decals
Status: STATIC_SOURCE / NARROW CORE BUILD BLOCKED OUTSIDE SHINOBU_149. Unity import, shader compiler, Frame Debugger, profiler, GCMonitor, player build, and decal runtime proof pending.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary
This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.

No Unity import, Unity Console, Play Mode, shader compiler, Frame Debugger, profiler, GCMonitor, decal runtime, or player-build proof is implied unless this document links a fresh evidence artifact.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not dynamic decal runtime, shader compiler, Frame Debugger, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs`
- `Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`

Unity import metadata exists for the new SHINOBU_149 script assets:

- `Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs.meta`
- `Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs.meta`
- `Assets/_Project/Scripts/Visor/Editor/ScreenSpaceDecalTunerWindow.cs.meta`

## Legacy Object-Decal Package Status

- `Assets/Dynamic Decals` was deleted after GUID/reference scans found no `_Project` references to its core scripts.
- `Assets/Dynamic Decals.meta` was deleted in the same operation.
- This removes the old mesh/object projection code, runtime pools, `Resources` assets, and legacy decal shaders from the current filesystem surface.

## Authority Route

Dynamic decals are presentation-only state. They are not gameplay truth, not rollback state, and not Merkle input.

Impact producers publish existing Core signal lanes:

- `HighSpeedImpactSignal` for kinematic and ballistic impacts with explicit AUP and normal.
- `CombatDamageSignal` for hull dents and combat residue with AUP, inverse impact direction, damage type, magnitude, and frame.

`DynamicDecalVaultRuntime` consumes typed `SignalBus<T>` snapshots from Core, converts them into `DecalRequestSignal`, and writes `DecalInstanceDTO` records into a Vault-owned ring. Physics, Gameplay, and Ballistics do not call Visor concrete runtime code.

## Vault Buffers

SHINOBU_149 requests these buffers from `GlobalDataVault`:

| BufferID | Name | Element | Count | Options |
|---:|---|---|---:|---|
| `71490` | `Instances` | `DecalInstanceDTO` | `1024` | `UninitializedMemory` |
| `71491` | `UploadScratch` | `DecalInstanceDTO` | `1024` | `UninitializedMemory` |
| `71492` | `RuntimeState` | `DecalRuntimeStateDTO` | `1` | default |
| `71493` | `TelemetryRing` | `DecalTelemetryEntry` | `300` | default |
| `71494` | `Tuning` | `DecalTuningDTO` | `1` | default |
| `71495` | `MaterialProfiles` | `DecalMaterialProfileDTO` | `256` | `UninitializedMemory` |
| `71496` | `CsvScratch` | `byte` | `16384` | `UninitializedMemory` |

The request queue is a prewarmed owner-local `NativeQueue<DecalRequestSignal>` registered with `NativeMemorySentinel`. It is transient ingress, not authoritative decal storage.

Request admission is fixed-cap. Runtime and signal ingress paths check the pending queue count against `RequestQueuePrewarmCapacity` (`1024`) before enqueue. Overflow is dropped and counted into the frame telemetry instead of allowing native queue growth.

Dropped-request accounting is saturating. Partial mock-request clamps and full-queue runtime drops both increment the same ingress-drop counter without allowing integer wraparound.

Visual sync locks the complete Vault mutation envelope before signal ingestion: `Instances`, `UploadScratch`, `RuntimeState`, `TelemetryRing`, `Tuning`, and `MaterialProfiles`. Upload telemetry patching, tuning writes, CSV profile ingest, fault marking, and black-box reads use smaller dedicated lock envelopes so control paths do not touch Vault memory through stale handles.

Editor/debug reads also use Vault lock envelopes. Tuning, runtime state, and latest telemetry snapshots lock their single buffer during read. The gizmo visualizer uses `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead`, holding `Instances` and `RuntimeState` while iterating active matrix DTOs.

## DTO Layout

`DecalInstanceDTO` is the GPU and Vault ABI:

| Offset | Field | Size |
|---:|---|---:|
| `0` | `float4x4 LocalToWorld` | `64` |
| `64` | `uint MaterialHash` | `4` |
| `68` | `float Opacity01` | `4` |
| `72` | `float LifetimeSeconds` | `4` |
| `76` | `uint Flags` | `4` |

Total size: `80` bytes. Alignment: `80 % 16 == 0` and `80 % 8 == 0`. The runtime/editor validator checks `UnsafeUtility.SizeOf<DecalInstanceDTO>() == 80` and exact offsets.

Offset `72` stores per-decal `LifetimeSeconds`. CSV material profiles and designer tuning therefore affect actual opacity decay without expanding the 80-byte shader ABI.

Telemetry and runtime-state DTOs are explicit 64-byte records to keep ring writes cache-line contained.

## Render Path

`DeferredDecalPass` uploads active decals through double-buffered `GraphicsBuffer` objects. The pass uses `LockBufferForWrite`, copies the Vault upload scratch through `DynamicDecalMappedUploadJob`, unlocks, and renders from the previous readable buffer. `GraphicsBuffer.SetData` is not used.

The pass validates required buffer capacity before capturing the previous readable buffer. If max-decal tuning changes and the buffers are rebuilt, `_hasReadableBuffer` is cleared and the current frame skips the old handle instead of binding a released `GraphicsBuffer`.

The mapped upload count is clamped again immediately before `LockBufferForWrite` against `target.count` and the Vault upload scratch length. This is a last-line guard; normal operation should already be capped by runtime tuning and renderer capacity.

`Hecton_DeferredDecal.shader` performs one fullscreen composite pass:

1. Read scene depth.
2. Reconstruct world position.
3. Convert to camera-relative space.
4. Loop only over `_HectonDeferredDecalCount`.
5. Project into each decal volume using matrix-column dot products and guarded denominators.
6. Sample `Texture2DArray` atlas, or use the procedural scorch/dent fallback when no atlas is bound.

The shader does not invert matrices per pixel. The 80-byte DTO remains sufficient because the matrix columns are orthogonal scaled axes.

## Designer Profile Bridge

`TryLoadMaterialProfilesCsv` is a cold path only. It reads into the Vault-owned `CsvScratch` buffer, rejects empty files, rejects files larger than the 16 KB scratch budget, rejects short reads, and only then parses `ReadOnlySpan<byte>` rows into the Vault-owned `MaterialProfiles` table. This prevents silent prefix parsing of oversized authoring files.

## Scalability Curve

`GlobalQualityWeight` feeds `Smooth01(q) = q*q*(3 - 2*q)`.

- Runtime stores an effective quality value in `DecalRuntimeStateDTO.GlobalQualityWeight`; it follows the Homeostasis target through `math.lerp(previous, target, saturate(deltaTime * response))`, where `response` rises continuously with thermal pressure. This prevents one-frame upload-count cliffs during thermal drops.
- Active upload/evaluation capacity lerps from `LowTierCapacity` (`128` by default) to `MaximumOverkillCapacity` (`1024` by default).
- `MaximumOverkillCapacity` is capped by the capacity requested by `DeferredDecalPass`, so the runtime upload window cannot exceed the current `GraphicsBuffer` allocation.
- `DeferredDecalPass` clamps its GPU buffer capacity to the same 128-decal low floor, so serialized sub-floor values cannot allocate an undersized upload target.
- Runtime low-tier sanitizer and active-count resolution also use the 128-decal floor; no hidden sub-low decal tier exists.
- Thermal pressure raises `DecayRate`, causing older decals to fade instead of pop.
- Per-decal `LifetimeSeconds` scales the opacity decrement by `baseLifetime / decalLifetime`, so material profiles can retain long-lived scorch/dent marks while weak hardware still raises global decay pressure.
- Shader procedural noise lerps from smooth radial marks at low quality to broken/noisy scorch rings at high quality.
- Depth projection tightens with quality, reducing bleed on weak hardware and allowing richer volume detail on high-end hardware.

No binary low/high hardware switch is used.

## Failure And Telemetry

Every visual frame writes a `DecalTelemetryEntry` with frame, active decals, new decals, upload count, CPU us, GPU upload us, global quality, thermal pressure, flags, total written, and state hash. Fault flags trigger `Docs/AgentLogs/Dump_DECAL_PROJECTOR.bin`.

NaN protection exists at every matrix build boundary: finite AUP checks, finite local float conversion, normal fallback, guarded `rsqrt`, guarded shader denominators, and non-finite fault flags.

Upload telemetry is patched into the current telemetry row from `RecordGpuUploadMicroseconds`, after `LockBufferForWrite`/mapped copy completes. Upload stalls above the runtime threshold dump `Docs/AgentLogs/Dump_DECAL_PROJECTOR.bin` immediately.

## Compile Wall

The Visor files for this system reference Core and Core.Memory only. They do not reference Gameplay, Physics, World, VFX, Atmosphere, or Ballistics concrete assemblies. Hull impact bridge changes in `SubmarineStructuralGrid` publish `CombatDamageSignal` through Core instead of calling Visor.

The high-speed impact AUP conversion in `DynamicDecalVaultRuntime` consumes raw fields from the Core signal payload and `HectonPhysicsContract.AupSectorSizeMetersDouble`. It does not name `Hecton8.World.AbsoluteUniversePosition` in the Visor source.

High-speed and combat-damage signal ingestion use separate last-frame cursors. The aggregate last-impact frame exists only for telemetry, so a newer packet in one signal lane cannot starve a valid packet from the other lane.

Each lane also carries an explicit "has ingested" sentinel, so deterministic frame `0` packets are accepted once before duplicate-frame filtering begins.

Runtime layout validation in player builds checks the 80-byte `DecalInstanceDTO` size without reflection. Exact offset reflection is compiled only for editor validation.

The only `JobHandle.Complete()` sites are labeled `[BLOCKING_SYNC_POINT]`: cold mock request generation, one-time first visual sync clear, VISUAL_SYNC upload scratch publication, and mapped `GraphicsBuffer` copy-before-unlock. They remain `PENDING PROFILER VERIFICATION`, not frame-time proof.

The editor tuner avoids interpolation, LINQ, `foreach`, and `string.Format` in the SHINOBU_149 source surface. It is still an editor facade, not runtime player proof.

## Verification Commands

Static scans run on 2026-05-19:

```powershell
rg -n "DecalProjector|BallisticsRuntime|GraphicsBuffer\.SetData|string\.Split|UnityEngine\.Random|Physics\.Raycast|RaycastNonAlloc|new NativeArray|new NativeList|new NativeHashMap|List<GameObject>|Instantiate\(" Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs Assets/_Project/Scripts/Visor/DeferredDecalPass.cs Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs Assets/_Project/Scripts/Visor/Editor/ScreenSpaceDecalTunerWindow.cs Assets/_Project/Art/Shaders/Hecton_DeferredDecal.shader
rg -n "using Hecton8\.Gameplay|using Hecton8\.Physics|using Hecton8\.World|using Hecton8\.VFX|using Hecton8\.Atmosphere" Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs Assets/_Project/Scripts/Visor/DeferredDecalPass.cs Assets/_Project/Scripts/Visor/DynamicDecalGizmoVisualizer.cs Assets/_Project/Scripts/Visor/Editor/ScreenSpaceDecalTunerWindow.cs
```

Both scans returned no matches in SHINOBU_149 scope. `dotnet build Hecton8.Core.csproj --no-restore` was run after CPU dropped below the gate. The first pass exposed missing SHINOBU_149 file inclusion in `Hecton8.Core.csproj`; after adding the files, the second pass no longer reports SHINOBU_149 files and remains blocked by unrelated missing DTO/namespace dependencies in other domains.

Additional filesystem checks confirmed `.meta` files exist for the three new SHINOBU_149 C# assets.

Post-hardening `Select-String` scans also returned no matches for `$"`, `foreach`, `string.Format`, `.Select(`, `.Where(`, `.Any(`, `.ToList(`, `Pack=1`, `Pack = 1`, `get; set;`, and `get; private set;` in the SHINOBU_149 C# source set. Current CPU samples were `100, 100, 100`, so another compile launch remains forbidden by the batch gate.

Profile-lifetime scans additionally returned no `BirthTime`, `CurrentTime`, or `Time.time` matches in the runtime/shader scope after offset 72 was converted to `LifetimeSeconds`.

Legacy object-decal purge checks:

```powershell
rg -n "LlockhamIndustries\.Decals|DynamicDecals\.System|ProjectionRenderer|ProjectionPool|DynamicDecalSettings" Assets/_Project -g "*.cs" -g "*.prefab" -g "*.unity" -g "*.asset"
Test-Path -LiteralPath "Assets/Dynamic Decals"
Test-Path -LiteralPath "Assets/Dynamic Decals.meta"
Get-ChildItem -Recurse -File Assets | Where-Object { $_.Name -like "*.meta" -and -not (Test-Path -LiteralPath ($_.FullName -replace "\.meta$","")) }
```

The `_Project` reference scan returned no legacy decal references. Both `Test-Path` calls returned `False`. The orphan `.meta` scan returned no rows.

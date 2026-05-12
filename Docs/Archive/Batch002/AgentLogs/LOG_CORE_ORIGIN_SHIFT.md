# LOG_CORE_ORIGIN_SHIFT

## Session Entry

What was wrong: Prompt identifies visual tearing during AUP rebases across ParticleSystem, TrailRenderer, VFX bounds, camera interpolation, Rigidbody interpolation, decals, BRG/scatter, and spatial hash caches.
What was done: Intake only. Full XML prompt extracted by CLI, domain mapped to ECHELON 1 Origin Shift/AUP Manager, and relevant mandates loaded.
Cinematic Cheats used: Presentation transaction and shader jitter mask selected over adding physical simulation.
Exact Microseconds saved: PENDING VERIFICATION. No runtime code changed yet.

## Final Session Entry

What was wrong:
- AUP shift authority moved transforms, but presentation systems retained old-epoch state: world-space particles, Unity TrailRenderer history, camera/rigidbody interpolation caches, decal matrices, scatter Hi-Z/foveated caches, drone BRG matrices, and spatial hash runtime metadata.
- `WorldSpatialHashGrid` was rebuilding native entries after shifts, which can turn a virtual-origin operation into O(fish count + occupied cells).
- `OriginShiftTranslateJob` was Burst-attributed while using Unity `TransformAccess`.
- Multiple cross-domain systems still cache class-level runtime/world positions; full list appended to `RECON_CORE_ORIGIN_SHIFT.md`.

What was done:
- Added pre-shift and shift-frame transaction support: `AupPreShiftSignal`, exact-frame dispatcher lock, `_TotalUniverseOffset` render-loop publication, and one-frame `_AupJitterMask`.
- Rebased active world-space `ParticleSystem` particles through preallocated scratch and refreshed local systems without restart.
- Added custom AUP `NativeTrailRenderer` with ring-buffered absolute samples and generated instanced strip mesh; Unity TrailRenderer remains banned by recon, not mass-deleted.
- Reset camera and Rigidbody interpolation/physics presentation caches during shift.
- Rebased construction decal matrices and drone fleet native/render matrices.
- Flushed scatter Hi-Z/foveated visibility state without reallocating the depth pyramid.
- Converted spatial hash origin shift to metadata/runtime-cache rebase while leaving native AUP buckets untouched.
- Raised shift scratch capacities and removed NativeTrailRenderer Tick-time resize.
- Removed Burst from the TransformAccess origin-shift job; remaining Burst origin-shift jobs are native/math only.

Cinematic Cheats used:
- Shader millimeter snap for one frame instead of CPU-wide transform snapping.
- One-frame Hi-Z occlusion disable instead of depth history repair.
- AUP trail strip reconstruction instead of Unity TrailRenderer hidden-vertex migration.
- Metadata-only spatial hash rebase instead of physical reinsertion.
- In-place particle history offset instead of VFX restart.

Exact Microseconds saved:
- Particle rebase: estimated 85 us for 512 particles, 900 us for 8K particles on MX350-class CPU; PENDING VERIFICATION.
- NativeTrailRenderer: estimated 35 us for 32 samples, 120 us for 128 samples; PENDING VERIFICATION.
- Dispatcher lock: skips later lanes for one shift frame; exact savings PENDING VERIFICATION.
- Hi-Z flush: estimated 3 us scalar invalidation plus one skipped depth-pyramid occlusion dispatch; PENDING VERIFICATION.
- Spatial hash metadata rebase: estimated 18 us for 256 entries, 650 us for 10K metadata entries; avoids native multi-hash remove/add churn; PENDING VERIFICATION.
- Drone fleet shift: estimated 50 us for 64 slots; PENDING VERIFICATION.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false` failed before CORE_ORIGIN_SHIFT diagnostics on `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`.
- Unity Console, Play Mode, GCMonitor, profiler, and visual capture are PENDING. No verified runtime claim is made.

## R&D Session Entry - 2026-05-12

What was wrong:
- Recon follow-up found `HectonIndirectVegetationRenderer` still held old-epoch cull camera position, previous motion-vector camera position, explicit world-space bounds, and far-cull snapshot state without subscribing to committed AUP shifts.
- `dotnet build` also exposed that this renderer referenced `HardwareTierDetector.AllowComputeCulling`, while `HardwareTierDetector.cs` is currently untracked/not in the generated `Hecton8.Core.csproj`.

What was done:
- Added `IOriginShiftListener` to `HectonIndirectVegetationRenderer`.
- Registered/unregistered the renderer with `HectonFloatingOrigin`.
- On committed shift, the renderer now subtracts `ShiftOffset` from cached cull/motion camera positions and explicit world bounds, clears previous-motion history, invalidates far-cull history, and restarts the culling cadence.
- Removed the renderer's direct `HardwareTierDetector` dependency; compute path still uses `_preferGpuIndirectRendering`, `SystemInfo.supportsComputeShaders`, valid camera/mesh/kernel checks, and fallback rendering.
- Updated `Status_CORE_ORIGIN_SHIFT.md`, `Rationale_CORE_ORIGIN_SHIFT.md`, and `RECON_CORE_ORIGIN_SHIFT.md`.

Cinematic Cheats used:
- O(1) cache rebase/invalidation instead of vegetation buffer rebuild.
- One refreshed far-cull pass after shift instead of reallocating BRG/indirect resources.

Exact Microseconds saved:
- Vegetation shift cache reset estimated 2 us on MX350-class CPU; PENDING VERIFICATION.
- Avoided BRG/indirect buffer rebuild cost; exact saved time PENDING VERIFICATION.

Verification:
- `git diff --check` on Loop 6 files: no whitespace errors, only LF/CRLF warning on `HectonIndirectVegetationRenderer.cs`.
- `rg "HardwareTierDetector" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`: no matches after remediation.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false` remains BLOCKED. One diagnostic run reported 77 errors dominated by missing shared platform/path/native bridge contracts (`HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HectonNativeBridge`, etc.). Follow-up build attempts timed out before diagnostics; spawned dotnet workers from this pass were stopped.
- Unity MCP `validate_script` timed out and `read_console` failed because the Unity session did not answer ping / was not ready. Spawned dotnet workers from this validation attempt were stopped.
- Unity Console, Play Mode, GCMonitor, profiler, and visual capture remain PENDING.

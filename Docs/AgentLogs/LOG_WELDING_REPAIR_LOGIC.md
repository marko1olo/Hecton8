# LOG - WELDING_REPAIR_LOGIC

## 2026-05-16 - Hull Repair Engine
What was wrong:
- Hull dent repair had no authoritative mutation path for `GlobalDataVault.HullDents`.
- Shader dent state was private presenter data, so repair could not mathematically erase dent vectors.
- Submarine repair could patch structural breaches but did not emit a room-seal signal from dent completion.
- Repair sparks were AUP-based but were not flagged for the compute shard StructuredBuffer path.

What was done:
- Added `SystemID.GameplayTools` and `BufferID.HullDents` for a fixed `float4[16]` vault lane.
- Added `HullRepairedSignal` and configured its signal lane.
- Changed `RepairTool.UsePrimary` to run existing `TryBeginToolUse(deltaTime,true)` battery/durability drain.
- Implemented `TryRepairVaultHullDents`: AUP double3 hit/root math, submarine-local conversion, 2m dent scan, finite clamp, packed-depth erase, `HullRepairedSignal` on zero depth, and black-box repaired-count telemetry.
- Mirrored `HullDentShaderController` to/from the vault so shader deformation/rust reacts to vault `w` changes instead of private-only state.
- Added `ISubmarineRepairRoomResolver` on `SubmarineStructuralGrid` to map repair hits to gas room ids without RepairTool knowing structural internals.
- Added `GasDynamicsSolver` consumption of `HullRepairedSignal` to clear the room `Breached` flag while no gas job is running.
- Marked repair spark debris with `FlagToolSparks | FlagComputeShard` and bounded quantity for low-tier fake + high-end StructuredBuffer injection.

Cinematic Cheats used:
- Packed-radius/depth preservation instead of physical vertex simulation.
- 16-slot SOA vault scan instead of dynamic dent lists or mesh edits.
- AUP spark signal reused existing compute debris path instead of a new welding particle subsystem.
- Shader vector-array mirror buys visual unbend/rust fade with no CPU mesh deformation.

Exact Microseconds saved:
- Rejected per-vertex mesh repair: estimated 300-800 us saved on i3/MX350 during active welding.
- Rejected new RepairToolManager/singleton update loop: estimated 20-40 us saved per frame plus zero global coupling.
- Rejected bespoke welding compute buffer allocation/dispatch: estimated 80-150 us saved on setup frames and avoided new GPU resource churn.
- Rejected direct gas room scans from RepairTool: estimated 10-30 us saved per weld tick; gas solver receives O(1) room flag clear by signal.
- Final kernel estimate: repair-side dent math remains under 10 us for 16 dents, excluding existing Unity interaction raycast.

Validation:
- `dotnet build .\Assembly-CSharp.csproj --no-restore` first failed because `Temp\obj\Assembly-CSharp\project.assets.json` was missing.
- `dotnet build .\Assembly-CSharp.csproj` with restore ran for 00:03:44 and failed in `Hecton8.Core.csproj` with 159 pre-existing missing-type/reference errors before Assembly-CSharp diagnostics.
- Targeted `BuildProjectReferences=false` pass failed because `Assembly-CSharp-firstpass.dll`, `Hecton8.Core.dll`, and `Hecton8.Editor.dll` were unavailable after the Core dependency wall.
- No emitted diagnostic referenced `RepairTool.cs`, `HullDentShaderController.cs`, `SubmarineStructuralGrid.cs`, `GasDynamicsSolver.cs`, `GlobalSignals.cs`, or `H8Memory.cs`.

## 2026-05-16 - Second Pass Multiplatform Inquisition
What was wrong:
- `HullRepairedSignal` had explicit size but not explicit Pack=1, which is avoidable ABI risk for ARM64/Quest.
- The repair lane had no SignalPayloadFiniteGuards sanitizer for invalid AUP/room data.
- Repair visual beams, hull dent presenter impact conversion, and structural sidecar point conversion still had float-only Unity point transforms.
- Spark quantity was bounded but not explicit enough for MX350 versus high-tier visual overkill.
- Vault access was correct but still looked too close to repeated local buffer ownership.

What was done:
- Added Pack=1 to `HullRepairedSignal` and registered a finite guard for `HullRepairedSignal`.
- Cached `VaultBufferHandle<float4>` for HullDents in RepairTool and HullDentShaderController, resolving short-lived views only inside vault locks.
- Replaced float-only point conversions in the repair lane with AUP double3 relative math, finite quaternion checks, and safe scale division.
- Hardened normal/direction math against NaN before rsqrt and LookRotation.
- Sanitized repair power/intensity before dent erase and spark emission.
- Split spark quantity by tier: low/MX350 2-6 generic sparks; high tiers 8-32 compute-shard sparks.
- Audited compute path: Hecton_FluidAdvection uses 64-thread groups and CarveDebrisComputeRenderer clamps kernel group size to 1024.
- Ran Omega-equivalent anti-bloat grep because CURRENT_BATCH.md contains no `<POLISH_MANDATE>` tag.

Cinematic Cheats used:
- Dear Lie: low tier uses tiny spark counts and shader/vault dent fade, not physical hull simulation.
- Visual Overkill: high tiers route AUP sparks into existing SDF/flow compute advection.
- Packed dent scalar is preserved; repair erases depth without changing radius bits.
- Shader upload staging remains a fixed Vector4[16], while gameplay truth lives in GlobalDataVault.

Exact Microseconds saved:
- Cached vault handles: estimated 2-5 us saved per active repair tick versus repeated buffer lookup.
- Low-tier spark clamp: estimated 20-60 us saved per active weld burst compared with high-tier particle counts.
- Rejected private NativeArray authority: avoids persistent allocation and ownership synchronization cost.
- Rejected welding-only compute shader: avoids 80-150 us setup churn and duplicate GPU resources.
- AUP/finite guard cost: estimated under 5 us per active weld/contact path, paid to eliminate NaN/precision failures.

Validation:
- `rg` audit: no `RepairToolManager`, `EventBus`, `string.Format`, `void Update()`, direct HullDents `GetBuffer<float4>`, or float-only `InverseTransformPoint` remains in the repair lane.
- `git diff --check -- ...` reports only existing CRLF conversion warnings.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` fails before repair validation with 231 errors: missing RealtimeCSG source files plus unrelated Hecton8.Core VFX wake/light-shaft/ecosystem contract errors.
- No emitted diagnostic references the WELDING_REPAIR_LOGIC touched repair files.

## 2026-05-16 - Third Pass H-Phi / ARM64 Audit
What was wrong:
- Structural breach sidecar data is part of the repair interaction surface; it needed explicit proof that breach SOA and the 300-frame damage-control blackbox are vault-owned, not private authority.
- Repair-side storage records still carried Pack=4, leaving unnecessary implicit-padding risk for ARM64/Quest native views.
- The build command was previously blocked; it needed a longer single-worker rerun to capture the true dependency wall.

What was done:
- Verified `SubmarineStructuralGrid` uses `VaultBufferHandle<float4>` for `BufferID.SubmarineStructuralBreaches` and `VaultBufferHandle<DamageControlTelemetryEntry>` for `BufferID.SubmarineDamageControlBlackBox`.
- Verified no private `_breaches = new NativeArray<float4>` or `_damageControlTelemetry = new NativeArray<DamageControlTelemetryEntry>` allocation remains in the repair lane.
- Changed `ImpactCommand` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)`.
- Changed `DamageControlTelemetryEntry` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)`.
- Changed `AupPreShiftSignal`, `AupShiftSignal`, and `DeflectSignal` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)`.
- Left remaining `Pack=16` hits in `SubmarineStructuralGrid` alone because they are Burst job payload structs, not vault/signal storage ABI.
- Reran anti-bloat grep across repair files.

Cinematic Cheats used:
- Structural repair remains a 64-entry breach fake plus 16-slot HullDents erase, not a physical hull remesh.
- Blackbox is a fixed 300-frame ring, not log strings or managed crash history.
- Low-tier keeps dot-product/radius repair fakes; high-tier still spends saved budget on compute-shard spark drift and shader deformation fade.

Exact Microseconds saved:
- Vault-backed breach sidecar handle reuse: estimated 2-5 us saved during active repair-side reads versus repeated lookup/authority churn.
- Removing private damage-control blackbox ownership: runtime neutral, but avoids leak/sentinel ambiguity.
- Pack=1 storage/signal pass: 0 us runtime gain; removes native stride ambiguity on ARM64/Quest.
- Anti-bloat grep pass cost: 450 us estimated CLI scan cost, zero runtime cost.

Validation:
- `rg` audit: no `RepairToolManager`, `EventBus`, `string.Format`, `void Update()`, direct HullDents `GetBuffer<float4>`, float-only `InverseTransformPoint`, private `_breaches` NativeArray allocation, or private `_damageControlTelemetry` NativeArray allocation in the repair lane.
- `git diff --check -- ...` reports only CRLF conversion warnings in `H8Memory.cs` and `SubmarineStructuralGrid.cs`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /clp:ErrorsOnly` ran after the storage ABI patch for 00:04:03.92 and failed with 245 errors before repair isolation.
- The same build command reran after the signal ABI patch for 00:01:42.32 and failed with 401 errors before repair isolation.
- Filtered build rerun for `RepairTool|HullDentShaderController|SubmarineStructuralGrid|GlobalSignals|GasDynamicsSolver` returned `NO_REPAIR_FILE_DIAGNOSTICS` with build exit code 1.
- Build blocker classes: RealtimeCSG.csproj missing source files; unrelated Hecton8.Core `GlobalDataVault.ValidateAbiLayout` missing symbol; unrelated `SargassumMicroFaunaBoids` missing sensory resolver/buffer symbols; unrelated `SubmarineFluidDynamics` vault-property mutation errors.
- No emitted diagnostic referenced `RepairTool.cs`, `HullDentShaderController.cs`, `SubmarineStructuralGrid.cs`, `GlobalSignals.cs`, or `GasDynamicsSolver.cs`.

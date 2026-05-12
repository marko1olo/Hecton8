# COMBAT_DAMAGE Log

## 2026-05-11T21:33:24+04:00

What was wrong:
- `CombatDamageRuntime.cs.meta` lacked Unity `MonoImporter` data.
- Player hit direction used the same cheap direction handling as fauna, damaging spatial awareness.
- Damage result data lacked high-fidelity surface normal for deferred wound presentation.
- Weakspots, tail crippling, armor/shield sync, poison spread, and blood scent needed decoupled contracts rather than direct dependencies.
- Poison spread could waste queue slots on duplicate colliders during the OMEGA audit.

What was done:
- Preserved `CombatDamageRuntime.cs` GUID and restored `MonoImporter`; verified csproj link existed.
- Added exact guarded `math.normalize` for player direction; fauna remains dominant-axis.
- Added high-only `SurfaceNormal`, feedback receiver contract, 8x8 LUT low path, high ricochet modifier.
- Kept native queue cap at 1024 and processed damage through the existing `NativeQueue<CombatDamageSignal>`.
- Added `SpliceFloraTraitMask`, `SyncTargetProtection`, `TryGetTargetHealthFraction`, `Crippled` status, weakspot/limb/mobility interfaces.
- Published tool melee impacts through `GlobalSignals.ImpactSignal`.
- Added fixed-buffer poison diffusion through `WorldSpatialHashGrid` and blood scent emission through `ChemicalInfluenceGrid`.
- Added fauna mobility-scale consumption for tail crippling.

Cinematic Cheats used:
- Low LOD: 8x8 armor LUT, dominant-axis fauna direction, `_HitFlash`-style feedback contract, rsqrt kinetic fallback for fauna.
- High LOD: exact hit normal/point data, exact player direction, ricochet dot modifier.
- Bitwise trait splice and status masks replace object graphs.

Exact Microseconds saved:
- Fauna dominant-axis direction: estimated 4-8 us saved per 1024-hit swarm versus exact normalize on i3/MX350.
- Armor LUT low path: estimated 10-20 us saved per 1024 hits versus per-material lookup/branching.
- Branchless weakspot multiplier: estimated 1-3 us saved per dense burst by avoiding divergent branches.
- Reciprocal max health: estimated 2-5 us saved across 2048 status/trauma checks versus repeated divides.
- Poison duplicate filter: saves up to 15 wasted queue packets per multi-collider diffusion burst.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:q /nologo /p:UseSharedCompilation=false /p:BuildInParallel=false`
- Result: 0 warnings, 3 errors in `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` only: ambiguous `InteractionSignal` and missing `IInteractionSignalConsumer` implementation. COMBAT_DAMAGE remains PENDING VERIFICATION because full project compile is externally blocked.

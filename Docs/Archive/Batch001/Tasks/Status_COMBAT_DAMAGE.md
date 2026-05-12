# COMBAT_DAMAGE Status

Agent: COMBAT_MASTER  
Prompt: COMBAT_DAMAGE  
Domain: ECHELON 5 COMBAT & SURVIVAL PHYSIOLOGY  
Source batch: `Docs/Tasks/CURRENT_BATCH.md`

## Core Checklist

- [x] 1. Meta recovery and csproj link.
  - DOD: Preserved existing GUID and restored full Unity `MonoImporter` block; verified `Hecton8.Core.csproj` already includes the script.
  - Rejected: New GUID or csproj churn; either risks broken serialized references or merge noise.
  - Estimate: 0 us runtime.
- [x] 2. Player exact incoming direction; fauna retains dominant-axis.
  - DOD: `CombatEntityKind.Player` now returns guarded `math.normalize`; non-player path still uses dominant-axis.
  - Rejected: Low LOD player rsqrt approximation, because the prompt explicitly requires exact player spatial awareness.
  - Estimate: <1 us worst-case per 1024-hit burst on i3/MX350 due player-only branch.
- [x] 3. High fidelity hit data retained; low fidelity routes cheap hit flash only.
  - DOD: `CombatDamageResult` carries exact `LocalPoint` and high-only `SurfaceNormal`; feedback interface receives LOD so low path can use `_HitFlash` without decal data.
  - Rejected: Direct decal manager dependency; cross-agent integration must stay behind receiver/listener contracts.
  - Estimate: Low saves decal allocation/render cost; high adds 16 KB result-buffer storage and exact normal math only on high.
- [x] 4. 8x8 LUT low path; high path ricochet angle modifier.
  - DOD: Existing packed damage-class 8x8 LUT remains low path; high path multiplies by `saturate(dot(projectileDir, armorNormal)+0.2)`.
  - Rejected: Per-material ScriptableObject lookup in the job; not Burst-safe and not zero-GC.
  - Estimate: Low path table lookup only; high adds two normalizes and one dot per queued hit.
- [x] 5. NativeQueue damage path, no collision damage dependency.
  - DOD: `NativeQueue<CombatDamageSignal>` is the frame ingress and is processed by `ProcessDamageQueueJob`.
  - Rejected: `OnCollisionEnter` damage routing; physics callbacks are unmanaged timing and GC risk.
  - Estimate: Single-pass queue drain under 0.1 ms target at 1024 signals.
- [x] 6. SoA health NativeArray<float>.
  - DOD: Health, max health, reciprocal max health, armor, shield, status masks, and status timers remain flat NativeArrays keyed by target slot.
  - Rejected: Per-entity MonoBehaviour health scanning; not Burst-compatible and scales badly.
  - Estimate: SoA slot access keeps 1024-hit burst in cache; no per-target GC.
- [x] 7. 32-bit status bitmask slow-tick parallel job.
  - DOD: `ProcessCombatStatusJob` runs `IJobParallelFor` over target slots and mutates `uint` status masks.
  - Rejected: Coroutine/status component fanout; cannot guarantee zero-GC or deterministic slow tick.
  - Estimate: 64-slot batches keep status tick under the 0.1 ms suspicion line for 2048 targets.
- [x] 8. Flora TraitMask 64-bit bitwise splice helper.
  - DOD: Added deterministic `SpliceFloraTraitMask(ulong, ulong, uint)` using hash-derived selector and bitwise parent merge.
  - Rejected: Random/managed genetic objects; cross-domain flora callers can use the static helper without allocations.
  - Estimate: Constant-time bit splice, <1 us per batch call.
- [x] 9. Burning when LocalTemp > 100C.
  - DOD: Damage job applies `CombatStatusBits.Burning` when `CombatDamageSignalDetail.LocalTemperatureCelsius > 100`.
  - Rejected: Separate thermal MonoBehaviour event; status belongs in the native combat pass.
  - Estimate: One compare and mask set per hit.
- [x] 10. Kinetic damage from impulse length; exact player, approximate fauna.
  - DOD: Added `ResolveKineticDamage`; player fallback uses `math.length`, fauna uses `lengthSq * rsqrt`.
  - Rejected: One-size exact length; too expensive for fauna swarms and unnecessary for non-HUD feedback.
  - Estimate: Fauna saves sqrt cost across swarm impacts; player path remains readable.
- [x] 11. Melee kickback and ImpactSignal publish.
  - DOD: Tool impacts still queue physics impulse and now publish `ImpactSignal` with AUP point, force, intensity, body id, and weight class.
  - Rejected: Audio/VFX direct calls; impact presentation drains the existing NativeQueue lane.
  - Estimate: One signal enqueue per impact; no managed event allocation.
- [x] 12. Localized trigger weakspot x3.
  - DOD: Localized hit resolver reads `ICombatWeakspot` or fractured field descriptors and packs `CombatWeakspotTier.Weakspot`; Burst job applies branchless `math.select(1,3)`.
  - Rejected: Tag/string weakspot lookup; not deterministic enough and not zero-GC.
  - Estimate: Main-thread component lookup only at tool hit; job remains branchless.
- [x] 13. Tail health <50% predator speed hook.
  - DOD: Added `ICombatLimbHealthSource` and `ICombatMobilityModifierReceiver`; tail health under 50% sets `Crippled`, and `FaunaBrain` consumes the mobility scale without per-frame allocation.
  - Rejected: Hard-coded fauna limb tables inside combat runtime; that would cross-own fauna internals.
  - Estimate: One multiply in fauna fixed tick while crippled.
- [x] 14. Poison spread within 2m via spatial hash.
  - DOD: Status-changed poisoned results query `WorldSpatialHashGrid` with a fixed 16-slot scratch array and queue status-only toxic damage to registered bioform receivers.
  - Rejected: Physics overlap or allocating list; spatial hash is already the domain-wide contact index.
  - Estimate: Bounded 16-contact diffusion, no heap allocation.
- [x] 15. Suit armor slot mitigation from external SoA sync.
  - DOD: Added `SyncTargetProtection(targetId, armorValue, shieldValue)` for Logistics/suit SoA sums; job subtracts `ArmorValues[slot]`.
  - Rejected: Direct `ILogisticsService` API invention; no stable armor-slot contract exists in this checkout.
  - Estimate: One array write on sync, one subtract per hit.
- [x] 16. Wounded creatures emit Eco scent grid signal.
  - DOD: Fauna damage results set `BloodScent`; managed dispatch resolves hit world point and calls `ChemicalInfluenceGrid.QueueBloodScent`.
  - Rejected: Scent emission in Burst job; grid API is managed/world-domain.
  - Estimate: Only on wounded fauna results, no scan.
- [x] 17. Shield absorbs 80%.
  - DOD: Job drains `ShieldValues[slot]` by `min(shield, damage * 0.8)` before armor subtraction and marks `ShieldAbsorbed`.
  - Rejected: Separate shield component callback; would add cross-system ordering risk.
  - Estimate: Two mins/subtracts per shielded hit.
- [x] 18. Damage queue cap 1024.
  - DOD: `TryQueueDamage` rejects when `_queuedSignalCount >= MaxQueuedSignals` and detail index mask remains 10-bit.
  - Rejected: Letting `NativeQueue` grow during explosions; memory blowouts violate prompt.
  - Estimate: Hard cap protects frame memory.
- [x] 19. Branchless multipliers via math.select.
  - DOD: Weakspot x3 uses `math.select(1f, 3f, condition)`.
  - Rejected: Branching critical/weakspot multiplier in Burst hot path.
  - Estimate: Avoids branch divergence in dense hit bursts.
- [x] 20. Reciprocal max health stored and used.
  - DOD: `_invMaxHealth` is maintained on register/sync; `TryGetTargetHealthFraction` exposes health percentage without divide.
  - Rejected: Recomputing division per trauma/status result.
  - Estimate: Replaces repeated divides with multiplies.

## Loop 1: Tasks 1-5

State: DONE. Compile verification pending after next loop.

## Loop 2: Tasks 6-10

State: DONE. Compile check reached C# compiler but is blocked by unrelated `World/ProceduralWreckGenerator.cs` missing methods:
`TryUnregisterWreckSlowTick`, `ProcessNearFieldDebris`, `ProcessArtifactDiscovery`, `UpdateDebrisGravityStateless`, `ValidateBlackBoxState`, `RefreshLootRecords`, `PrepareWreckWorldState`, `ConfigureIntegrityProxy`.

## Loop 3: Tasks 11-14

State: DONE. Compile check still blocked by unrelated dependency errors:
`GlobalSignals.cs` missing signal aliases, `ConstructionManager` origin-shift contract mismatch, and `FaunaBrain.cs` missing `FaunaTier1LodProxyEntry` from pre-existing fauna LOD proxy work.

## Loop 4: Tasks 15-20

State: DONE. Compile still blocked outside combat; latest errors are in wreckage scan contracts, habitat hatch mesh state, save codec/storage, construction signal, and physics transform sync. A ToolHitUtility obsolete ID warning from this loop was fixed by switching to `EntityId`.

## Loop 5: Self-Audit / Polish Gate

State: OMEGA POLISH COMPLETE. STATUS: PENDING VERIFICATION.

- [x] Parsed `<POLISH_MANDATE id="OMEGA_POLISH">` after all 20 tasks were checked.
- [x] Scanned touched files for hot-path `foreach`, LINQ, string formatting, collision callbacks, `OverlapSphere`, obsolete `GetInstanceID`, and unbounded managed allocations.
- [x] Fixed poison diffusion bloat: added a fixed 16-target duplicate filter and skipped unregistered spatial hits before queueing status spread.
- [x] Re-ran `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:q /nologo /p:UseSharedCompilation=false /p:BuildInParallel=false`.
- [ ] Full project compile: blocked outside COMBAT_DAMAGE by `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` ambiguous `InteractionSignal` / missing interface implementation. Latest compile returned 0 warnings, 3 errors, all in that world-domain file.

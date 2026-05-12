# LOG_FLORA_GROWTH_SYSTEM

STATUS: PENDING VERIFICATION

## 2026-05-11 - Botany Shader Growth Pass

What was wrong:
- Flora had sway and interaction metadata, but no renderer-owned SoA age lane for shader growth.
- Growth was not pushed to the vegetation cull compute, so harvested/immature state could not be respected consistently by indirect rendering.
- Existing maturation yield used smoothed scale; seedlings could pay out resource mass.
- Main/depth/shadow/motion vegetation passes did not share the same growth deformation.
- Flora material compliance was undocumented.

What was done:
- Added `NativeArray<float> FloraAges01` and `_HectonFloraAges01` `GraphicsBuffer` ownership to `HectonIndirectVegetationRenderer`.
- Bound `_HectonFloraAges01` to main, depth-only, shadow, motion-vector, and compute culling paths.
- Added `_HectonFloraAges01` to `FloraCulling.compute`; `Age < 0` returns before visible-index append.
- Updated `Hecton_IndirectVegetation.shader` growth logic: local Y scales by age, local XZ scales by `sqrt(age)`.
- Mirrored growth deformation into depth, shadow, and motion-vector shaders to avoid visible/depth mismatch.
- Added age-based emissive pulse behavior: seedlings pulse faster, mature plants pulse slower/deeper.
- Added 10 second FrostTick gating to `FloraRegrowthDirector` maturation and 3x radiation growth via `HectonHazardManager.GetHazardIntensity(..., HazardType.Radiation)`.
- Changed maturation resource yield to linear age, not smoothed scale.
- Changed harvest mass to `BaseYield * Age`; age below 0.2 returns zero.
- Wrote negative growth sentinel through flora metadata on decomposition/suppression; renderer age SoA uploads `-1`.
- Resolved legacy zero ambiguity: authored zero-age seedlings encode `0.0002`; legacy `Reserved0 = 0` remains mature.
- Created `Docs/AgentLogs/RECON_FLORA_GROWTH_SYSTEM.md`; 21 flora-like materials scanned, 12 non-compliant, 9 compliant.

Cinematic cheats used:
- Shader vertex morph instead of CPU transform or mesh rebuild.
- `sqrt(age)` XZ expansion for fast initial pop without simulation.
- Emissive pulse speed/depth keyed by age instead of real algae metabolism.
- Compute cull sentinel for dead flora instead of physical removal.
- 10 second FrostTick for deterministic growth instead of per-frame biology.

Exact microseconds saved:
- CPU transform scaling rejected: estimated 40-150 us saved per 10K flora update on i3/MX350.
- Age upload: estimated 3.8 us per 1K plants using one contiguous `NativeArray<float>` copy.
- FrostTick amortization: estimated 35 us per 2K tracked flora every 10 seconds, ~3.5 us/s amortized.
- Harvest yield age check: estimated <0.2 us per harvest.
- Negative-age compute cull: removes harvested flora from visible append before draw; saved work depends on density, but CPU overhead remains 0 us/frame.

Blocked:
- Task 5: `NativeQueue<SporeEvent>` ABI is not exposed.
- Task 6: GPU scatter has no spore-event ingestion seam.
- Task 8: creeping-vine taxonomy / adjacent AUP cell seed API is not exposed.
- Task 9: indirect BRG flora is not registered as plant contacts in `WorldSpatialHashGrid`.
- Task 11: low-tier spread radius depends on blocked Task 8.
- Task 13: Data Archivist MMF age-array lane is not exposed to botany.
- Task 15: Unity compile is blocked by unrelated errors in `PlayerInventory.cs`, `HectonBoidController.cs`, and `VehicleDockingModule.cs`; botany-owned scripts validated individually where MCP remained stable.

Verification:
- `validate_script` passed for `HectonIndirectVegetationRenderer.cs`, `HectonIndirectVegetationContracts.cs`, `FloraRegrowthDirector.cs`, and `DestructibleOrganicManager.cs` before final self-review.
- After final self-review, `HectonIndirectVegetationRenderer.cs` basic validation passed; `DestructibleOrganicManager.cs` MCP validation disconnected, but the final patch was a local scalar helper and call-site replacement.
- Unity refresh/import was attempted twice. Editor returned to idle, but console remains red from non-botany dependency errors.
- `dotnet build Hecton8.Core.csproj --no-restore -clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false` was attempted and timed out after 124 seconds.

Vertex Growth Morph shader logic:

```hlsl
float growth01 = ResolveGrowth01(sourceInstanceIndex, instanceData.Reserved0);
float visibleGrowth01 = saturate(growth01);
float growthHeightScale = visibleGrowth01;
float growthWidthScale = sqrt(max(visibleGrowth01, 0.0));

localPosition.y *= growthHeightScale;
localPosition.xz *= growthWidthScale;
```

## 2026-05-12 - Honest R&D Upgrade: Age Authoring And Black Box

What was wrong:
- The renderer-owned age SoA existed, but external farming/persistence code had no safe zero-alloc write API.
- GPU-only buffer sources with no CPU metadata could overwrite authored ages with mature defaults.
- Flora growth had no fixed 300-frame black-box trail for NaN/crash diagnosis.

What was done:
- Added `TrySetFloraAge01(int, float)` for single-entry age authoring.
- Added `TryCopyFloraAges01(NativeArray<float>, int)` for deterministic restore/farming lanes without managed arrays.
- Added `MarkFloraAgesDirty()` for callers that mutate the exposed `FloraAges01` NativeArray directly.
- Added `_floraAgesAuthoredExternally` so authored GPU-source ages are not refilled to `1.0`.
- Added `NativeArray<FloraGrowthTelemetryEntry>[300]` circular telemetry.
- Telemetry records frame, instance count, sample count, negative sentinel count, NaN count, dirty-upload flag, min/max age, and bounded hash.
- NaN detection writes `Docs/AgentLogs/Dump_FLORA_GROWTH_SYSTEM.bin` and sanitizes external ages before GPU upload.

Cinematic cheats used:
- Growth remains a shader/compute data lane, not GameObject scale.
- Black-box records compact hashes and summary counters instead of dumping full flora state every frame.
- Steady frames use bounded 64-sample telemetry; full scans happen only on dirty age uploads.

Exact microseconds saved:
- Direct external GraphicsBuffer mutation rejected: prevents ownership bugs, no runtime saving claimed.
- Managed list/callback authoring rejected: estimated 0 B/frame GC saved and avoids per-authoring managed churn.
- Steady telemetry bounded to 64 float samples: estimated <2 us/frame on i3/MX350.
- External dirty-age sanitization: estimated <20 us per 10K plants only when age data changes.

Verification:
- `validate_script` standard passed for `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` after the R&D patch.
- Unity script refresh was requested twice, but editor readiness timed out after 60s each time.
- `read_console` failed with `Unity session not ready for 'read_console' (ping not answered); please retry`.
- Project compile remains `PENDING VERIFICATION`; no success is claimed.

## 2026-05-12 - Mature Toxic Spore Queue ABI

What was wrong:
- Task 5 had mature/toxic detection, but no botany-owned `NativeQueue<SporeEvent>` ABI for scatter/fog consumers.
- Direct GPU scatter mutation would cross domain ownership and create a hard dependency on a renderer not exposed to botany.
- Toxic spore exposure could affect hazards/status, but it did not leave an AUP-backed event trail for future dithered fog.

What was done:
- Added `HectonFloraSporeEventKind`, `HectonFloraSporeEvent`, and `HectonFloraSporeEvents` to `HectonIndirectVegetationContracts.cs`.
- The event payload carries AUP, runtime position, radius, intensity, age, template index, active payload index, frame index, kind, and underwater flag.
- Backed the handoff with a persistent, prewarmed `NativeQueue<HectonFloraSporeEvent>` capped at 64 pending events.
- Added drop counting so queue pressure is observable without allocating or blocking gameplay.
- Updated `FloraInteractionManager` to enqueue mature toxic flora from a 10 second FrostTick with a bounded per-lane scan budget.
- Updated player toxic-spore exposure to queue the nearest mature toxic emitter event with the resolved age/template/payload metadata.
- Updated defensive spore bursts to publish the same ABI as `DefensiveBurst` events for future renderer reuse.

Cinematic cheats used:
- Producer emits compact event impulses instead of simulating drifting spores.
- Mature-toxic discovery uses budgeted FrostTick scans, not per-frame biology.
- AUP plus runtime position gives renderer consumers stable world identity without forcing botany to render fog.

Exact microseconds saved:
- Managed particle/VFX spawning rejected: estimated 0 B/frame GC and avoids unbounded GameObject churn.
- Direct scatter buffer writes rejected: saves integration risk, no runtime saving claimed.
- Default mature-toxic scan budget is 96 candidates per lane every 10 seconds; expected producer cost is <10 us per 10s tick on i3/MX350.
- Queue capacity is 64 events; overflow drops and increments a counter instead of causing a frame spike.

Verification:
- `validate_script` standard passed for `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs`.
- `validate_script` standard passed for `Assets/_Project/Scripts/World/FloraInteractionManager.cs`.
- `git diff --check` reported only CRLF conversion warnings for the edited files.
- `read_console` failed with `Unity session not ready for 'read_console' (ping not answered); please retry`.
- Full project compile remains `PENDING VERIFICATION`; no compile success is claimed.

## 2026-05-12 - Omega Polish Addendum

What was wrong:
- The prior status carried stale polish text. `CURRENT_BATCH.md` does contain `<POLISH_MANDATE id="OMEGA_POLISH">`.
- The mature-toxic proximity scan used a floating-point division for exposure falloff.
- The 10 second mature-toxic scan could continue doing candidate checks after the queue was already saturated.

What was done:
- Read the polish mandate from `Docs/Tasks/CURRENT_BATCH.md`.
- Replaced toxic exposure division with one precomputed reciprocal and multiplication.
- Added an early exit when `HectonFloraSporeEvents.PendingCount >= HectonFloraSporeEvents.PendingEventCapacity`.
- Re-ran standard validation on `HectonIndirectVegetationContracts.cs` and `FloraInteractionManager.cs`.
- Ran the mandated `dotnet build Hecton8.Core.csproj --no-restore -clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false`.

Cinematic cheats used:
- Compact event impulses remain the core spore simulation; no drifting particle biology was added.
- Queue pressure drops or stops producer work instead of expanding capacity mid-frame.
- Exposure remains squared-distance visual/hazard falloff, now reciprocal-multiplied.

Exact microseconds saved:
- One floating-point divide removed per toxic emitter candidate during player proximity scans.
- Up to 96 candidate checks per lane are skipped when the 64-event queue is saturated.
- No managed allocations added; audit found no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` in the touched spore ABI/producers.

Final Git Diff:
- `HectonIndirectVegetationContracts.cs`: spore event payload, kind enum, bounded NativeQueue API, drop count, clear/dequeue/reset/prewarm.
- `FloraInteractionManager.cs`: mature-toxic producer, player exposure spore publication, defensive burst event publication, mature age resolver, toxic template/trait checks, reciprocal falloff, queue saturation guard.
- `Status_FLORA_GROWTH_SYSTEM.md`, `Rationale_FLORA_GROWTH_SYSTEM.md`, and `LOG_FLORA_GROWTH_SYSTEM.md`: updated evidence trail.

Verification:
- `validate_script` standard passed for `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs`.
- `validate_script` standard passed for `Assets/_Project/Scripts/World/FloraInteractionManager.cs`.
- Unity console is red from unrelated Burst `CombatDamageResult` struct-layout mismatch in gameplay code.
- `dotnet build` failed in 54.9s with 111 non-botany errors, primarily missing `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `VoxelChunkModifiedEvents`, and `HectonNativeBridge`/`HectonNativeLibrary`.
- `VERIFIED MASTER GRADE` is not claimed. Project status remains `PENDING VERIFICATION`.

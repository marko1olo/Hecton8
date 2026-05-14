# Rationale_SHALLOWS_ECONOMY_DISTRIBUTOR

Status: PENDING VERIFICATION

## Decision 1 - Prompt Authority And Mandates

Problem: The active batch file path in the user preamble was ambiguous; `C:\hades\Hecton8\CURRENT_BATCH.md` did not exist, while the active task file exists at `Docs/Tasks/CURRENT_BATCH.md`.

Solution: Used PowerShell CLI raw read and regex extraction against `Docs/Tasks/CURRENT_BATCH.md`, isolating only `<AGENT_PROMPT id="SHALLOWS_ECONOMY_DISTRIBUTOR">`. Loaded eight task-relevant mandates before code: deterministic integer RNG, zero-GC hot paths, AUP rebasing, blackbox telemetry, registry/event decoupling, frame budget, native jobs, and visual-fake first.

Rejected Alternatives: Borrowing old WORLD_RESOURCE_SPAWNER archive instructions would contaminate this batch. Reading deprecated batch dumps would violate active-batch hygiene. Treating the in-chat summary as sufficient would skip the required disk extraction.

Scalability potential: Low uses integer weights and cheap distance thresholds. Middle keeps deterministic weighting with full copper clump check. High keeps richer vein continuity. Ultra can spend saved CPU on denser visual markers and richer GPR presentation without changing gameplay authority.

Hardware Impact: CLI/task setup is cold-path only. Runtime target remains 0 B GC and integer branch math in the generation job; expected cost under 0.01 ms on i3/MX350 for weighting over existing candidate counts.

## Decision 2 - Initial Technical Direction

Problem: First-hour ore economy needs authored scarcity without introducing direct dependencies on other agents or changing public APIs destructively.

Solution: Extend existing read models and signals if present, keep authority in the world resource spawner's Burst-friendly data path, and use existing GlobalRegistry or NativeQueue-style signal surfaces. Weighting will use integer probability bands with explicit total-weight guards instead of floating cumulative probabilities.

Rejected Alternatives: Unity `Random.Range` is non-deterministic and banned. ScriptableObject mutation would persist editor data and miss Burst. Per-ore MonoBehaviours would inflate object count and GC pressure. Direct hard references to GPR implementation would couple domains during a 20+ agent batch.

Scalability potential: Low can use sector-hash clump approximation. Middle/High/Ultra can use actual 2m neighbor continuity while retaining deterministic results. The GPR layer can fade non-matching pings visually without changing ore authority.

Hardware Impact: Integer weight branch adds negligible ALU. Avoiding per-node GameObjects and managed filters protects MX350 main thread and VRAM. Saved cycles can go to higher-tier GPR glow/fade detail.

## Decision 3 - Contracts First Ore Exposure

Problem: GPR and HUD needed ore type knowledge without hard-linking to the ore spawner implementation or recreating singleton access.

Solution: Extended `IWorldResourceSpawnerReadModel` with `TryGetOreTypes` and `LocalTitaniumCount`, added stable `WorldOreTypeIds`, and extended `IGroundRadarService` with `SetOreFilterType`. `GroundPenetratingRadarRuntime` now treats the configured spawner as a `MonoBehaviour` only long enough to cast to the read model.

Rejected Alternatives: Keeping `[SerializeField] ProceduralOreSpawner` in GPR would defeat asmdef isolation. Duplicating ore type constants in GPR would drift. Copying ore types into managed arrays would violate zero-GC runtime policy.

Scalability potential: Low/MX350 still reads SoA arrays directly. Middle/High/Ultra can add richer ore-specific GPR visuals by using the same type ids without touching ore authority.

Hardware Impact: One extra NativeArray read in the GPR job when scanning; no managed allocation in Tick/LateFrame. Expected delta under 0.005 ms for 128 pings / 64 rays on MX350.

## Decision 4 - Drop Pod AUP Anchor

Problem: The first-hour distribution needs a crash-site anchor, but no existing drop-pod signal lane existed in the active code.

Solution: Added `DropPodLandedSignal` as a 64-byte unmanaged AUP signal and consumed it through `SignalBus<DropPodLandedSignal>`. The spawner stores both AUP and runtime position, with fallback to the player AUP only until the first real drop-pod signal arrives.

Rejected Alternatives: Reading a scene singleton or transform directly would couple to another agent's prologue object. Using world-space `Vector3` only would break after floating-origin shifts. Dequeue-only consumption would race other systems.

Scalability potential: Low through Ultra use the same AUP anchor. Higher tiers can add richer crash-site visual dressing without changing deterministic ore probabilities.

Hardware Impact: Signal scan is frame-snapshot span iteration; when no signal exists it is empty and effectively free. Runtime shift maintenance is one float3 subtract on shift frames only.

## Decision 5 - Deterministic Integer Weighting

Problem: Titanium/Copper/Silver probabilities must be exact near/far the drop pod without floating cumulative RNG or modulo bias.

Solution: The Burst LCG job computes `distSq` from ore absolute coordinates to the drop-pod AUP absolute coordinate, resolves integer weights summing to 100, and maps RNG to 0-99 with multiply-high. Near band is 70/30/0. Far band is 40/40/20. Middle band linearly tapers and derives Silver as `100 - Ti - Cu`.

Rejected Alternatives: `UnityEngine.Random`, modulo `% 100`, or ScriptableObject curves were rejected for determinism, bias, and Burst unsuitability. A managed quota post-pass was rejected because it would reorder ore slots and allocate pressure.

Scalability potential: Low uses identical quota math. High/Ultra can spend saved CPU/GPU budget on denser ore presentation while keeping economy outputs deterministic.

Hardware Impact: Added work is one double distance, a handful of integer operations, and no allocations per accepted candidate. Expected cost under 0.01 ms for the default scan budget on i3/MX350.

## Decision 6 - Copper Clump Math LOD

Problem: Copper needs vein feel without building spatial acceleration structures or adding per-node objects.

Solution: The job carries the previous accepted ore. If it was Copper, the next accepted roll gets an 85% Copper bias when within 2m. On Low/MX350/Unknown, the 2m distance test is replaced with a sector-seed hash mask.

Rejected Alternatives: Neighbor searches, grids, and NativeHashMaps were rejected because they are unnecessary for "next roll" vein continuity and would add memory churn or setup cost. Forcing every follow-up ore to Copper was rejected because it collapses scarcity.

Scalability potential: Low gets cheap believable clumping. Middle/High/Ultra use actual 2m spatial continuity for better ore vein readability.

Hardware Impact: Low-tier path is one hash mask and one multiply-high roll. Full-tier path is one `distancesq` and one multiply-high roll only after a Copper predecessor.

## Decision 7 - Verification Limits

Problem: Local Unity MCP was unavailable and the generated Core project currently fails on unrelated missing assembly references, so full Unity compile proof could not be produced from this session.

Solution: Built `Hecton8.World.Contracts.csproj` successfully, ran filtered Core builds to check for edited-file errors, attempted Unity MCP refresh, attempted standalone compiler validation, and ran `git diff --check`.

Rejected Alternatives: Reverting unrelated dirty work or editing project references to force a clean Core build would violate multi-agent isolation. Claiming full Unity verification without editor logs would violate the reporting protocol.

Scalability potential: Verification did not change runtime scalability. The blocked compile path is recorded for integrator follow-up.

Hardware Impact: No runtime impact.

## OMEGA POLISH CHANGES

Problem: Polish audit required anti-bloat review after core completion and a check that no honest hot-path math or managed constructs survived in the ore economy changes.

Solution: Read the OMEGA_POLISH mandate after the status checklist reached done/blocked. Searched edited files for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `$"..."`, and `.ToString()`; no matches in edited hot paths. Replaced the new drop-pod gradient reciprocal with `DropPodBandInvDistanceSq` so the job uses a const multiplier instead of a per-call reciprocal expression. Re-ran `Hecton8.World.Contracts.csproj` build and `git diff --check`.

Rejected Alternatives: A LUT for the 50m-100m taper was rejected because the prompt explicitly asks for mathematical tapering and the current integer band is already cold generation work. Shader-only GPR filtering was rejected because it cannot know ore type without CPU-side ore identity.

Cinematic Cheats Used: Integer percent weights instead of real economy simulation. Sector hash mask for Low/MX350 copper clump instead of spatial math. GPR alpha/strength suppression by 0.1 instead of rebuilding radar geometry.

Scalability potential: Low uses hash clump and small GPR ray count. Middle keeps exact 2m clump and existing GPR budget. High/Ultra can spend remaining budget on denser visuals because ore authority remains deterministic and cold.

Hardware Impact: Const reciprocal removes one candidate-level reciprocal expression from the new gradient path. Low-tier hash clump avoids a `distancesq` after Copper predecessor. Expected savings remain sub-0.01 ms but remove avoidable ALU on i3/MX350.

Final Git Diff: Edited `GlobalSignals.cs`, `GroundRadarContracts.cs`, `GroundRadarJobs.cs`, `GroundPenetratingRadarRuntime.cs`, `ProceduralOreSpawner.cs`, `Status_SHALLOWS_ECONOMY_DISTRIBUTOR.md`, and this rationale file. Added `Hecton8.World.Economy.asmdef` plus meta.

## Decision 8 - Post-Compact Hardening

Problem: The GPR persistent ping lane decayed `GprSignalStrength` in-place, so changing the HUD ore filter after a ping existed would apply filtering over an already weakened signal. The ore blackbox dump also still used the old `Dump_WORLD_RESOURCE_SPAWNER.bin` filename instead of this agent's mandated dump file. The active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `SHALLOWS_ECONOMY_DISTRIBUTOR`, so fresh prompt re-extraction is blocked by batch hygiene drift.

Solution: Kept raw GPR signal strength stable in `GprSignalStrength`, computed decayed display strength only for `GprPingGpu`, and recalculated highest signal from active display values every job. Replaced the cold `GetComponents<MonoBehaviour>()` array allocation with a capacity-bound `List<MonoBehaviour>` probe. Changed ore dump output to `Dump_SHALLOWS_ECONOMY_DISTRIBUTOR.bin`. Revalidated with Unity Bee response-file C# compiles for `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`.

Rejected Alternatives: Leaving in-place decay would make ore filter switches non-authoritative after the first frame. Rebuilding pings whenever the filter changes was rejected because it would turn a display preference into generation work. Keeping the old dump filename was rejected because the agent protocol explicitly names `Dump_[YourID].bin`. Re-reading archived batch prompts was rejected because AGENTS forbids previous-batch log dependency without explicit order.

Scalability potential: Low keeps the same small GPR ray count and cheap display-only filter. Middle keeps stable decay and refiltering. High and Ultra can layer richer GPR visuals on the raw lane without changing ore authority or re-running generation.

Hardware Impact: The raw/display separation is one extra local float and one max operation during existing ping compaction, with 0 B GC. The non-alloc component probe removes one cold managed array allocation from dependency discovery. Expected hot-path delta is under 0.005 ms on i3/MX350; correctness improves because filter changes do not permanently erode signal data.

Verification Update: `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` passed 0 warnings/0 errors. `dotnet build Hecton8.Core.csproj -v:minimal /m:1` passed 0 warnings/0 errors. Unity Bee response-file csc validation passed for `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`. Unity Editor console, PlayMode, GCMonitor, and profiler evidence remain absent; status remains PENDING VERIFICATION.

## Decision 9 - Real Drop-Pod Anchor Regeneration

Problem: If a sector generated before a real `DropPodLandedSignal` arrived, the fallback player AUP anchor could permanently seed the active sector. Later accepting the real drop-pod AUP updated the stored anchor, but did not force the already-generated sector to reroll against the real crash-site weights.

Solution: Added `_dropPodAnchorRequiresGenerationRefresh`. Accepting the first real drop-pod signal, or a later changed AUP signal, marks the active generation dirty. `RefreshSectorAndTerrain()` treats that as a generation refresh, completes/discards an obsolete in-flight spawn job if needed, reloads the existing depletion words for the same sector, disables hydrated proxies, and schedules generation against the real drop-pod AUP.

Rejected Alternatives: Immediate regeneration inside `DrainDropPodLandingSignals()` was rejected because it would cross scheduling responsibilities and could run from LateFrame. Ignoring fallback-generated sectors was rejected because first-hour Titanium/Copper/Silver distribution would be anchored to the player rather than the crash site. Wiping depletion state was rejected because a signal timing change must not resurrect mined ore.

Scalability potential: Low and MX350 pay the extra generation only once when the real anchor arrives; normal steady-frame cost remains zero. Middle, High, and Ultra retain deterministic ore placement while allowing richer crash-site presentation to be layered without changing economy authority.

Hardware Impact: One boolean branch in the cold sector refresh path and exact AUP field comparisons when a drop-pod signal is present. No Tick/LateFrame allocation and no per-frame regeneration.

Verification Update: `dotnet build Hecton8.World.Contracts.csproj -v:minimal /m:1` passed 0 warnings/0 errors. `dotnet build Hecton8.Core.csproj -v:minimal /m:1` passed 0 warnings/0 errors. Unity Bee response-file csc validation passed for `Hecton8.World.Contracts`, `Hecton8.World.GPR`, `Hecton8.World.Economy`, and `Hecton8.Core`. Scoped forbidden-pattern scan over the edited ore/GPR files returned `NO_MATCHES`. Unity Editor console, PlayMode, GCMonitor, and profiler evidence remain absent; status remains PENDING VERIFICATION.

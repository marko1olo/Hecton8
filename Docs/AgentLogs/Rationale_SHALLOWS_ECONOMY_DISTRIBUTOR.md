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


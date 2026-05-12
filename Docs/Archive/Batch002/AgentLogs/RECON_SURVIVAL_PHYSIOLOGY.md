# SURVIVAL_PHYSIOLOGY Recon

Status: COMPLETE, PENDING PROJECT COMPILE

Command basis:

- `rg -n "\bvoid\s+Update\s*\(|\bIEnumerator\b|\bHeal\s*\(|\bTakeDamage\s*\(|currentHealth|integrity\s*=" Assets/_Project/Scripts -g '*.cs'`
- `rg -n "\bIEnumerator\b" Assets/_Project/Scripts -g '*.cs'`
- `rg -n "\bvoid\s+Update\s*\(" Assets/_Project/Scripts -g '*.cs'`
- `rg -n "\bHeal\s*\(" Assets/_Project/Scripts -g '*.cs'`

Findings:

- No `IEnumerator` heal-over-time path found under `Assets/_Project/Scripts`.
- Only direct `Update()` match in runtime project scripts is `Core/SystemDispatcher.cs`; player physiology remains tick-dispatched through registry/tick services.
- Player healing is centralized in `Gameplay/HectonPlayerHealth.cs:471`; toxicity reversal was inserted there, so medical items and direct heal calls hit the same rule.
- Player damage entry points found in `HectonSurvivalSystem`, `ModuleLifeSupportComponent`, `HazardZoneManager`, `TraumaDispatcher`, `RandomEventSystem`, `PlayerInventory`, `PlayerTool`, fauna/world strike systems, and tool utilities. They route through `HectonSurvivalSystem.TakeDamage` or `HectonPlayerHealth.TakeDamage`; no parallel player HP owner was introduced.
- Non-player health mutators remain in fauna/resource/destructible systems. They are outside SURVIVAL_PHYSIOLOGY authority and were not modified.
- The status mask bridge is headless-safe through `UIStateStore.SurvivalStatusMask`; visor UI decodes first active bit with `math.tzcnt`.

Decision:

- No coroutine repair required.
- No `Update()`-driven survival physiology was added.
- Existing direct damage callers are acceptable because the actual health/heal bottlenecks stay centralized.

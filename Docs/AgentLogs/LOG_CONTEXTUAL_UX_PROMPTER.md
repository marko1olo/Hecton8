# CONTEXTUAL_UX_PROMPTER Log

## 2026-05-14 Diegetic Input Tooltip Pass
What was wrong: The existing input prompt path was coupled to managed hover events and a tooltip singleton, which made a diegetic hatch prompt behave like scene UI state instead of a signal-fed presentation system. The old rendering path did not meet the requested indirect draw, integer glyph index, AUP shift, Low-tier snap, or black-box telemetry requirements.

What was done: Added `PlayerLookTargetSignal` and signal-bus setup in `GlobalSignals.cs`; published hash-only look-target packets from `PlayerInteraction`; added fixed `PlayerLookTargetPromptCache` sidecar storage; rewired `DiegeticTooltipSystem` to consume signals, stage fixed char buffers, resolve glyphs from `GlobalRegistry.InputDeterminism`, draw icon/text quads through `Graphics.DrawMeshInstancedIndirect`, survive `AupShiftSignal`, dither fade on non-Low tiers, snap alpha on Low, and dump a 300-frame black box on bad anchors. Removed `DiegeticTooltipSystem.ActiveRuntimeInstance`; repair diagnostics now resolve through `GlobalRegistry.Renderables`.

Cinematic cheats used: One atlas quad per glyph instead of true 3D text, shader dither instead of Animator/Canvas alpha, hash-only prompt packets instead of managed UI payloads, VR 0.1m depth bias using `rsqrt`, Low-tier instant alpha instead of fade, and per-instance integer UV selection instead of TMP rich text.

Exact microseconds saved: Estimates only, not profiler-measured. Expected low-end savings: 18-45 us by avoiding Canvas/TMP object rendering for the prompt, 3-7 us by skipping dither/fade on Low, 2-6 us by using bounded char cache instead of text assignment, 1-3 us by direct glyph array lookup, and 3-8 us by removing singleton/scene-search diagnostic routing from the normal hot path.

Scalability matrix: Low snaps alpha and disables shader dither; Middle uses 0.2s dither fade; High can improve atlas sharpness and SDF tuning; Ultra can add richer per-glyph visual treatment on the same indirect payload without changing gameplay authority.

Verification: Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned no touched-file errors for `GlobalSignals`, `PlayerLookTargetPromptCache`, `PlayerInteraction`, `DiegeticTooltipSystem`, `RepairTool`, or tooltip shader references after the final cache collision fix. Unfiltered broad build did not complete inside the tool timeout in the dirty multi-agent workspace. Unity MCP refresh failed because `http://127.0.0.1:8088/mcp` was unreachable, so Editor console verification remains pending.

Final diff evidence: Focused tracked diff covers `GlobalSignals.cs`, `PlayerInteraction.cs`, `RepairTool.cs`, `DiegeticTooltipSystem.cs`, `Status_CONTEXTUAL_UX_PROMPTER.md`, and `Rationale_CONTEXTUAL_UX_PROMPTER.md` with 946 insertions and 410 deletions. Created/untracked files are `PlayerLookTargetPromptCache.cs`, `Hecton_DiegeticTooltipIndirect.shader`, `DiegeticTooltipContracts.cs`, and their Unity `.meta` files.

## 2026-05-14 Recheck Upgrade
What was wrong: The fixed prompt cache still had a direct `hash & 63` placement path during recheck, which is faster but can drop a valid prompt on hash collision.

What was done: Replaced direct placement with bounded linear lookup, first-free-slot insertion, deterministic rollover, and subsystem reset. Re-ran the touched-file build filter; no errors were emitted for `PlayerLookTargetPromptCache`, `GlobalSignals`, `PlayerInteraction`, `DiegeticTooltipSystem`, `RepairTool`, or tooltip shader references.

Cinematic cheats used: Kept hash-only signal payloads and bounded char slab storage instead of introducing managed prompt objects or dictionaries.

Exact microseconds saved: No new savings claimed. This trade spends a small signal-time compare budget for prompt correctness while keeping render-time cost unchanged at fixed-array reads.

# Unity Hardware Tier Read Accessor Pass - UNKNOWN - 2026-05-28

## Scope

Domain: Core hardware tier and Quest Vulkan runtime policy.

Evidence class: static source proof only. Full build, Unity import, Play Mode, profiler, GC monitor, player build, and device runs were not performed in this pass.

## Problem

`HardwareTierDetector` and `QuestVulkanRuntimePolicy` public read properties called `EnsureInitialized()`.

That makes the first property read responsible for platform/XR probing and cached global-policy mutation. The current global systems doctrine requires read accessors to be pure; initialization must be owned by boot or explicit owner routes.

## Change

- Converted hardware tier read properties to pure snapshot reads.
- Initialized `_recommendedVramBudgetMegabytes` to the conservative default `1600`.
- Converted Quest Vulkan policy read properties to pure snapshot reads.
- Made `IsQuestVulkanCandidate` false until policy initialization has run.
- Kept `BeforeSceneLoad` and explicit `EnsureInitialized()` as the only mutation routes.

## Proof

| Check | Result |
|---|---|
| `HardwareTierDetector` getter calls to `EnsureInitialized()` | `0` |
| Quest policy getter calls to `EnsureInitialized()` | `0` |
| Remaining `EnsureInitialized()` references | BeforeSceneLoad calls and method declarations only |
| `HardwareTierDetector` brace delta | `0` |
| `OculusFfrEnforcer` brace delta | `0` |
| Scoped `git diff --check` | exit `0`; line-ending warnings only |
| CPU guard | `100%`; build/doc heavy gates skipped |

## Architecture Verdict

This was worth doing. It removes hidden initialization from globally reused read properties while preserving normal boot initialization.

It does not claim runtime speedup. Runtime microseconds saved: `0`.

## Scaling Behavior

- Low: before explicit initialization, compute-heavy permissions remain false and budget stays bounded.
- Middle: after boot, platform snapshot is available without getter mutation.
- High: high-resource compute policy still unlocks through explicit initialization.
- Ultra: no change to visual-overkill permission after policy initialization.

## Residuals

- Full build was not launched because CPU was `100%`.
- No Quest, SteamDeck, Android, Vulkan, Metal, D3D11, or D3D12 device matrix was run.
- Cross-domain call sites were not edited; the normal `BeforeSceneLoad` prewarm remains the compatibility route.

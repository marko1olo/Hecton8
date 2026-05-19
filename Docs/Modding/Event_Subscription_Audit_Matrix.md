# Event Subscription Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC_SOURCE_AUDIT / RUNTIME_PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner prompt: MODDING_API_SCHEMA_BUILDER

## 2026-05-19 Envelope-Only Override

The managed event subscription surface below is retained as source-audit context. In current envelope-only UGC mode, managed event bridges are quarantined:

- `ModLoader` must not install projected managed event bridges for public UGC;
- public subscribe/publish paths must no-op, reject, or remain uninstalled according to the quarantine source gate;
- no managed callback is a current runtime modding promise;
- future read-only event exposure must be implemented as bounded, unmanaged, envelope-compatible, or SDK-simulated projection with runtime proof.

The active modder interface is SDK authoring plus 64-byte envelope submission. See [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

## Source Files

- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs`

## Public Event Surface

| Method | Payload shape | Rule |
|---|---|---|
| `Subscribe<TPayload>` | unmanaged `in TPayload` | Mod-facing unmanaged payloads only. |
| `SubscribeNative` | `ReadOnlySpan<byte>` for approved native lanes | Span is callback-scoped; mods must not store it. |
| `SubscribeProjected` | `ModEventDto` | Source-backed projected `SignalBus<T>` lanes only. |
| `OnPlayerSpawned` | `ModPlayerSpawnedEvent` | Read-only player spawn snapshot. |
| `OnBiomeChanged` | `ModBiomeChangedEvent` | Read-only biome transition snapshot. |
| `Unsubscribe` | `HectonEventSubscription` | Convenience wrapper for `Dispose`. |
| `Publish<TPayload>` | unmanaged `in TPayload` | Mod-owned coordination payloads only; not first-party signal mutation. |

## Source Counts

| Contract | Source value | Rule |
|---|---:|---|
| Public event methods | `7` | New method requires schema/spec/audit/playbook update. |
| Native event kinds | `2` | Only `Interaction` and `Crafting` are approved byte lanes. |
| Projected event kinds including `None` | `3` | `CombatDamage` and `WeatherChanged` are the only non-none projected kinds. |
| Native queue bridge publish lanes | `2` | Bridge publishes only `Interaction` and `Crafting` payload copies. |
| Dispatch recursion depth cap | `5` | Recursive mod event cascades are dropped after the cap. |
| Callback watchdog | `2.0 ms` | Three consecutive stalls disable the offending mod path. |
| Subscription token active flag | `IsActive` | Tokens must be disposed from `IHectonMod.OnUnload`. |

## Native Event Kinds

| Kind | Value | Source owner | Mod-facing rule |
|---|---:|---|---|
| `Interaction` | `0` | `InteractionEvents` bridge | Immutable copied bytes only; no `NativeQueue` handle. |
| `Crafting` | `1` | `CraftingEvents` bridge | Immutable copied bytes only; no `NativeQueue` handle. |

## Projected Event Kinds

| Kind | Value | Rule |
|---|---:|---|
| `None` | `0` | Reserved. |
| `CombatDamage` | `1` | Allowed only through `ModEventDto`. |
| `WeatherChanged` | `2` | Allowed only through `ModEventDto`. |

## Subscription Lifetime Rules

- Every public subscription returns `HectonEventSubscription`.
- `Dispose` is idempotent and clears the owning channel reference.
- `HectonAPI.Events.Unsubscribe` delegates to `Dispose`.
- `ModLoader.DisableManagedMod` disables bus subscribers and quarantines command dispatch by subscriber id.
- Unload proof requires no callback after `OnUnload` disposes tokens.

## Forbidden Expansion

- No managed `HectonEvent` subscriptions for mods.
- No direct `NativeQueue`, `NativeArray`, `SignalBus<T>`, or DataVault handle exposure.
- No new native event kind without byte-copy source owner, schema update, runtime playbook step, and GC proof.
- No event payload JSON or string event names in hot paths.

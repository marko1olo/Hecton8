# Event Subscription Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC_SOURCE_AUDIT / RUNTIME_PENDING

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract

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
- `Assets/_Project/Scripts/ModdingAPI/HectonGameEvents.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`
- `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`
- `Assets/_Project/Scripts/CraftingEvents.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs`

## Public Event Surface

| Method | Payload shape | Rule |
|---|---|---|
| `Subscribe<TPayload>` | unmanaged `in TPayload` | Mod-facing unmanaged payloads only. |
| `SubscribeNative` | `ReadOnlySpan<byte>` for approved native lanes | Span is callback-scoped; mods must not store it. |
| `SubscribeProjected` | `ModEventDto` | Source-backed projected `SignalBus<T>` lanes only. |
| `OnPlayerSpawned` | `ModPlayerSpawnedEvent` | Read-only player spawn snapshot; explicit 24-byte layout. |
| `OnBiomeChanged` | `ModBiomeChangedEvent` | Read-only biome transition snapshot; explicit 24-byte layout with padding. |
| `Unsubscribe` | `HectonEventSubscription` | Convenience wrapper for owner-checked `Dispose`. |
| `Publish<TPayload>` | unmanaged `in TPayload` | Mod-owned coordination payloads only; rejects engine-owned payload types before bus publish. |

All public event methods are facade-only through `HectonAPI.Events`. `HectonEventBus` is internal first-party infrastructure with no public static bus member surface. `HectonModHooks` is internal first-party publication infrastructure, and its publication methods are internal-only. Subscription and publish calls require an active `ModExecutionScope` before envelope-only quarantine checks; unmanaged, native, and projected bridge routes plus private channel implementations reject anonymous subscribers before token creation; `Publish<TPayload>` rejects engine-owned command, result, projection, and lifecycle payload types when managed events are reopened; non-empty `subscriberId` values must match the active mod id, `Unsubscribe` rejects a token owned by another active mod, and direct `Dispose` validates active mod ownership for mod-owned tokens before channel unsubscribe. `HectonGameEvents` is internal-only legacy first-party payload infrastructure; its constructors and members must not be public and must not expose `ItemData`, `BuildableData`, `HectonSurvivalSystem`, or survival records as mod API handles.

## Source Counts

| Contract | Source value | Rule |
|---|---:|---|
| Public event methods | `7` | New method requires schema/spec/audit/playbook update. |
| Native event kinds | `2` | Only `Interaction` and `Crafting` are approved byte lanes. |
| Native byte payload layout gate | `InteractionEventPayload=32`, `CraftingEventPayload=64` | Source layouts must be explicit and schema-checked before any byte lane is public. |
| Projected event kinds including `None` | `3` | `CombatDamage` and `WeatherChanged` are the only non-none projected kinds. |
| Native queue bridge publish lanes | `2` | Bridge publishes only `Interaction` and `Crafting` payload copies. |
| Projected event cap curve | `round(lerp(10,50,smoothstep(saturate(GlobalQualityWeight01))))` | Continuous budget curve only; no binary low/high branch and no gameplay truth change. |
| Dispatch recursion depth cap | `5` | Recursive mod event cascades are dropped after the cap. |
| Callback watchdog | `2.0 ms` | Three consecutive stalls disable the offending mod path. |
| Subscription token active flag | `IsActive` | Tokens must be disposed from `IHectonMod.OnUnload`. |
| Engine-owned publish forbidden payloads | `11` | Public `Publish<TPayload>` must not publish engine command/result/projection/lifecycle DTOs. |
| Event quarantine ordering | active scope before envelope-only | Anonymous public event facade calls fail owner attribution before quarantine status is reported. |
| Bridge/channel subscriber owner proof | active scope and concrete id before token | Unmanaged, native, and projected event bridge routes plus private channel implementations must not synthesize anonymous subscribers. |

## Native Event Kinds

| Kind | Value | Source owner | Payload | Size | Mod-facing rule |
|---|---:|---|---|---:|---|
| `Interaction` | `0` | `InteractionEvents` bridge | `InteractionEventPayload` | 32 bytes | Immutable copied bytes only; no `NativeQueue` handle. |
| `Crafting` | `1` | `CraftingEvents` bridge | `CraftingEventPayload` | 64 bytes | Immutable copied bytes only; no `NativeQueue` handle. |

## Projected Event Kinds

| Kind | Value | Rule |
|---|---:|---|
| `None` | `0` | Reserved. |
| `CombatDamage` | `1` | Allowed only through `ModEventDto`. |
| `WeatherChanged` | `2` | Allowed only through `ModEventDto`. |

## Subscription Lifetime Rules

- Every public subscription returns `HectonEventSubscription`.
- `Dispose` is idempotent, validates active mod ownership for mod-owned tokens, and clears the owning channel reference.
- Dispose validates active mod ownership before channel unsubscribe for mod-owned tokens.
- `HectonAPI.Events.Unsubscribe` validates active mod ownership, then delegates to the same owner-checked `Dispose`.
- `ModLoader.DisableManagedMod` disables bus subscribers and quarantines command dispatch by subscriber id.
- Unload proof requires no callback after `OnUnload` disposes tokens.

## Forbidden Expansion

- No managed `HectonEvent` subscriptions for mods.
- No direct external `HectonEventBus` access and no public static bus methods; `HectonAPI.Events` is the only public route.
- No public `HectonGameEvents` managed payload members; legacy first-party payloads stay internal-only.
- No mod publication of engine-owned payload types such as `ModEventDto`, lifecycle event payloads, command envelopes, command DTOs, or engine result/rejection DTOs.
- No direct `NativeQueue`, `NativeArray`, `SignalBus<T>`, or DataVault handle exposure.
- No new native event kind without byte-copy source owner, explicit source payload layout/size/offset schema proof, runtime playbook step, and GC proof.
- No event payload JSON or string event names in hot paths.

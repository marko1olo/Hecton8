# Service And Event Drift Deep Dive

Date: 2026-05-07
Status: PENDING VERIFICATION

Purpose:
- describe the current shape of service authority and event signaling in key runtime services
- connect code reality to the projectâ€™s own mandates

## Main Pattern

The project has a real `GlobalRegistry`.

It also still keeps many services in mixed mode:
- singleton ownership
- scene/runtime object ownership
- `DontDestroyOnLoad` persistence
- registry publication

This is not a total architectural failure.
It is a migration state that is staying active for too long.

## Key Service Owners

### `InputDispatcher`

Current shape:
- `MonoBehaviour`, `IInputService`, `IUpdatable`, `ITickable`
- singleton ownership via `_instance`
- runtime instance factory via `EnsureRuntimeInstance()`
- `DontDestroyOnLoad(gameObject)` in `InitializeService()`
- registry publication through `GlobalRegistry.RegisterInputService(this)`
- dispatcher registration through `GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core)`

Reading:
- input service is serious and intentional
- input authority is still mixed, not bootstrap-pure

### `SaveManager`

Current shape:
- `MonoBehaviour`, `ISaveService`, `IUpdatable`
- comment header still describes it as singleton + `DontDestroyOnLoad`
- public `Instance` now resolves through `GlobalRegistry.Save as SaveManager`
- broad native surface and wide save participant registry

Reading:
- save architecture is one of the projectâ€™s real strengths
- service identity still carries legacy singleton language and shape

### `ConstructionManager`

Current shape:
- `MonoBehaviour`, `IUpdatable`, `ISaveable`, `ISlowTickable`, `ILogisticsService`
- singleton `_instance`
- `DontDestroyOnLoad(gameObject)`
- registers logistics service
- registers save participant
- registers update cadence

Reading:
- construction is fully in mixed-authority mode
- it is not a pure game feature anymore; it is also a service owner

### `QuestManager`

Current shape:
- `MonoBehaviour`, `ISaveable`, `IQuestSystem`
- `Instance = this`
- registers into `GlobalRegistry.QuestSystem` / `GlobalRegistry.Quest`
- registers with save service

Reading:
- quest runtime is real
- quest authority is small compared to world/gameplay, but still not architecturally pure

### `SpatialAudioManager`

Current shape:
- `MonoBehaviour`, `IAudioService`, `IUpdatable`
- singleton framing in comments and service identity
- publishes through `GlobalRegistry.Audio`
- owns a `NativeQueue<DelayedAudioEvent>`
- high implementation depth without native `Update()` sprawl

Reading:
- audio service is one of the projectâ€™s better examples of practical engineering
- it still presents itself as both singleton-like and registry-driven

## Event Drift

### Stronger Event Model That Exists In The Project

The project already has evidence of stronger event architecture:
- queue-backed systems
- explicit flush stages
- dispatcher-mediated deferred handling

This is the direction the architecture claims to want.

### Weaker Event Model That Also Exists

Example:
- `PlayerPDA` declares direct static `Action` events

Why this matters:
- this is simpler
- this is also less coherent with the stronger queue-backed event story

This does not automatically make `PlayerPDA` broken.
It does make the event architecture mixed.

## Reality Against Mandates

### Registry Mandate

Partially honored:
- registry is real

Partially violated:
- many important services still keep singleton identity and sometimes `DontDestroyOnLoad`

### Event-Bus Mandate

Partially honored:
- queue-backed buses exist

Partially violated in spirit:
- direct static action/event signaling still appears in feature owners

### Tick-System Mandate

Mostly honored in these owners:
- many services register through dispatcher paths instead of ad-hoc `Update()`

But:
- the service graph is still harder to reason about than a single clean ownership path would allow

## Brutal Summary

The project does not have a fake architecture.
It has two architectures living on top of each other.

One is the stronger target architecture:
- registry
- dispatcher
- explicit service publication
- queue-backed runtime coordination

The other is the older survival architecture:
- singleton identity
- persistent runtime objects
- direct static events
- service self-ownership

The project works because both exist.
The project is hard to trust because both still exist.

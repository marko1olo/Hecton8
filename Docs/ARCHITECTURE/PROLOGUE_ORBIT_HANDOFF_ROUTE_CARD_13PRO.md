# Prologue Orbit Handoff Route Card - 13pro

Status: `YELLOW / STATIC_SOURCE_ONLY / UNITY PROOF PENDING`

Evidence class: `STATIC_SOURCE / STATIC_DOC`. This document is not Unity import, Play Mode, profiler, GC, memory, render, or player-build proof.

## Route Field Contract

Route ID: `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO`

Date: 2026-05-27

Owner: `Hecton8.Prologue.Space.PrologueWorldHandoffSceneLoader`

Owner domain: `13pro` - prologue orbital flight, Aegir approach, Hecton orbit, capsule descent, and final world handoff.

Owning file/system:

- `Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs`
- `Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs`
- `Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs`
- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`
- `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`

Current authority boundary:

- This route card is `YELLOW / STATIC_SOURCE_ONLY`.
- Root production handoff remains `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD` until this route is proven GREEN and root scene-flow authority is explicitly updated.
- `01_ORBIT` may stay enabled and may be worked as standalone prologue content, but it is not mandatory first-20 acceptance proof in the current route.

Problem:

- The proposed 10-15 minute orbital prologue needs a production new-game route through `01_ORBIT`.
- Current root scene-flow authority leaves `01_ORBIT` outside the main handoff.
- This route cannot override first-20 acceptance until proof and authority update exist.

Why owner-local data is insufficient:

- Scene transition crosses from standalone prologue presentation into world scene loading.
- An owner-local bool cannot load `02_HECTON_WORLD` safely or explain failure.

Why direct caller/owner interface is insufficient:

- `AwaitableDropSequenceDirector` is contract-only and must not call `SceneManager` or a concrete scene loader.
- It publishes the final typed handoff.
- The scene loader consumes that handoff in `VISUAL_SYNC`/late-frame after stable snapshots.

Instrument:

- [ ] GlobalRegistry cold service/interface
- [x] SignalBus<T> first-party broadcast
- [ ] GlobalSignals bridge/direct queue
- [ ] HectonEventBus mod/API/cold event
- [ ] GlobalDataVault / IDataVault
- [x] Black-box/telemetry route

Producer/consumer phase: `AwaitableDropSequenceDirector` produces `SignalBus<PrologueCompleteSignal>` after sequence-owned water transition; `PrologueWorldHandoffSceneLoader` consumes the snapshot from late-frame and calls cached `ISceneService.LoadScene`.

Cadence/capacity: One final handoff signal per prologue sequence. Loader scans the bounded frame snapshot until load request, then unregisters.

Expected max events/reads per frame: One `SignalBus<PrologueCompleteSignal>.GetFrameSnapshot()` read per late-frame while the loader is active; one accepted handoff per sequence.

GlobalQualityWeight behavior:

- Quality controls presentation, Math LOD, VFX density, and standalone hydration proxy pressure.
- It must not alter route ownership, scene identity, save identity, DTO layout, or the final handoff signal contract.

Accessor purity:

- [x] No Get/TryGet/Resolve/Read API publishes signals
- [x] No Get/TryGet/Resolve/Read API syncs scene state
- [x] No Get/TryGet/Resolve/Read API allocates/grows buffers
- [x] No Get/TryGet/Resolve/Read API completes jobs
- [x] No Get/TryGet/Resolve/Read API mutates global state
- [x] No Get/TryGet/Resolve/Read API searches the scene

Payload/data shape: `PrologueCompleteSignal` unmanaged payload. Accepted final handoff requires `PhaseOceanHandoff`, `SourceHash == PrologueSignalSourceHashes.SequenceDirector`, non-zero `Sequence`, finite non-negative `WhiteoutHoldSeconds`, and `FlagForceWhiteout`.

Managed fields present: no in signal payload. Scene name string is serialized on the scene loader only.

UnityEngine.Object fields present: no in signal payload. Scene loader holds no scene-object payload.

Layout proof: Existing core signal contract owns layout. This route card does not change DTO layout.

Overflow/failure:

- Non-matching handoff signals are ignored.
- Missing `ISceneService` or blocked scene loading publishes one telemetry warning and keeps waiting.
- Invalid target scene string falls back to `02_HECTON_WORLD` through `OnValidate` and load-time fallback.

Telemetry fields: `PrologueSequenceTelemetryEntry` records sequence stages. `PrologueWorldHandoffSceneLoader` emits one-shot warning hashes for missing scene service, blocked scene load, and missing dispatcher.

Black-box fields: `AwaitableDropSequenceDirector` writes a 300-entry `PrologueSequenceTelemetryEntry` ring and dumps `Docs/AgentLogs/Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin` on fault. `OrbitalRelativityDirector` and `OrbitalDropReentryVfxController` keep their own 300-entry rings for the flight/VFX seam.

Profiler marker: Route does not add a dedicated marker. Existing VFX uses its late-frame profiler marker; sequence/route runtime profiler proof remains required before GREEN.

GC proof required: Unity Play Mode with GCMonitor/Profiler over `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.

Shutdown/disposal:

- Loader unregisters from late-frame before issuing load.
- Scene-local runtime root uses `HideFlags.None` so normal scene unload can destroy prologue owners.
- VFX/audio/orbital late-frame consumers rebind on Dispatcher replacement and unregister on disable.

Scene unload behavior: `01_ORBIT` is a standalone new-game scene and is not treated as hydrated gameplay world by `GameBootstrapper.RequiresGameplaySceneActivation`. Load-game resume may still enter `02_HECTON_WORLD` directly.

Stale-handle behavior: Sequence/orbital/VFX black-box rings use Vault generation handles with writer locks. VFX and orbital telemetry handles reacquire on DataVault replacement.

Rejected alternatives:

- [x] owner-local field
- [x] cached owner interface
- [x] existing SignalBus lane
- [ ] existing Vault buffer
- [x] cold HectonEventBus hook
- [x] no global route needed

Why this does not increase global monolith risk:

- The route adds no catch-all bus, no new registry slot, and no managed hot payload.
- It adds no world truth owner in `01_ORBIT`.
- It narrows final scene loading to one typed sequence-owned handoff signal.

H-Phi impact expected: Not a metric goal. This route exists to make the first-20-minutes new-game path explicit and auditable.

Proof required before GREEN:

- Clean Unity import and Console after script reload.
- Play Mode proof: New Game enters `01_ORBIT`.
- Game View proof: Aegir/Hecton visible from orbit window.
- Play Mode proof: autonomous burn reaches manual release, impact sync, hydration proxy/high-res gate, then `02_HECTON_WORLD`.
- Profiler/GC proof: no per-frame GC from the prologue route and VFX/audio late-frame consumers.
- No survivor root after `01_ORBIT` unload.
- Dump proof after forced fault/NaN path.

Reviewer: pending integrator.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY`.

Status: `BLOCKED` until compile/import/Play Mode/profiler proof exists.

## R43 Route-Card Fields

| Field | Value |
|---|---|
| Route ID | `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO` |
| Owner | `Hecton8.Prologue.Space.PrologueWorldHandoffSceneLoader` |
| Instrument | `SignalBus<PrologueCompleteSignal>`, cached `ISceneService`, prologue black-box telemetry rings |
| Producer phase | Sequence-owned water transition after impact/hydration gate |
| Consumer phase | Late-frame snapshot consumption before scene load request |
| Producer/consumer phase | `AwaitableDropSequenceDirector` final sequence stage -> `PrologueWorldHandoffSceneLoader.LateFrameTick` |
| Cadence | One final accepted handoff per prologue sequence |
| Capacity | One scene-load request; bounded frame snapshot scan; 300-entry black-box rings |
| Overflow/failure | Invalid signals ignored; missing service/blocked load emits one-shot telemetry and waits |
| Overflow policy | Drop non-matching handoff signals by source/phase/sequence/finite checks |
| Failure mode | Missing dispatcher, missing scene service, blocked scene load, invalid sequence, non-finite whiteout hold, or scene proof failure |
| Shutdown/disposal | Late-frame unregister before load request; scene-local root destroyed by normal scene unload; telemetry buffers remain Vault-owned |
| Fault dump target | `Docs/AgentLogs/Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin`, plus orbital/VFX dump files on their own fault paths |
| Proof required before GREEN | Clean import/Console, Play Mode route, Game View capture, profiler/GC, no survivor root, forced dump validation |
| Review disposition | `YELLOW / STATIC_SOURCE_ONLY` |

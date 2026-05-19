# HECTON-8 Technical FAQ

Date: 2026-05-14
Status: STATIC_DOC REVIEWED / RUNTIME PENDING

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

Evidence class: STATIC_DOC

Scope: developer-facing answers for recurring architecture questions. Runtime claims remain `PENDING VERIFICATION` unless a fresh artifact is cited.

## FAQ

1. Why do we not write `Rigidbody.velocity` or call `Rigidbody.AddForce` directly from gameplay?

   Direct body mutation bypasses deterministic force routing, telemetry, and budget ownership. Gameplay systems write force or damage packets into the approved physics lane; the physics apply owner mutates bodies in the fixed gather/apply phase.

2. Why is visual fake first mandatory?

   HECTON-8 spends performance on player belief, not invisible truth. Water, fog, light, pressure, cable sag, particles, and distant motion default to baked data, shaders, VAT, UI/audio/haptic cues, or coarse proxies unless gameplay correctness fails without physical truth.

3. Why are raw `Update`, `LateUpdate`, and `FixedUpdate` banned in gameplay code?

   Private Unity loops destroy cadence ownership. Runtime work belongs to dispatcher phases: `PRE_SIMULATION`, `SIMULATION`, `POST_SIMULATION`, and `VISUAL_SYNC`. Exceptions must be narrow and documented.

4. Why does service access go through `GlobalRegistry` instead of classic singletons?

   `GlobalRegistry` is a static service locator with explicit bootstrap registration. MonoBehaviour singleton self-registration in `Awake` creates order bugs, hidden dependencies, and stale references after scene unload.

5. Why is `GlobalRegistry.Get<T>()` banned in hot paths?

   Repeated service lookup turns registry state into a live bus and hides cross-domain polling. Dependencies used by Tick, jobs, UI sync, or renderer upload are cached during registration/dependency injection and refreshed by typed signals when needed.

6. Why do broadcasts use typed signal lanes instead of string event names?

   String events allocate, collide, and hide payload layout. Typed signal lanes use unmanaged payloads, bounded capacity, explicit overflow policy, and predictable consumption snapshots.

   Signal boundary note: first-party hot broadcasts use typed `SignalBus<T>` lanes or documented NativeQueue bridge lanes. `HectonEventBus` is for mod/API/cold/internal-meta traffic, not default gameplay broadcast.

7. How should GlobalRegistry, SignalBus, HectonEventBus, GlobalSignals, GlobalDataVault, telemetry, and H-Phi be used together?

   Use `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, and `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`: owner-local first, then one fact, one owner, one route, one proof artifact. A new global route needs owner, instrument, phase, cadence, capacity, failure mode, telemetry, shutdown, proof fields, and a `GREEN` review result before acceptance.

8. Why does HECTON-8 use AUP instead of `Transform.position` as truth?

   `Transform.position` is float presentation space. AUP stores large-world location as integer grid plus local float offset, preserving precision across origin shifts and keeping save/runtime math deterministic.

9. Why are LINQ and `foreach` on interface/dictionary surfaces rejected in Tick?

   They can allocate, box, or hide virtual iteration. Hot paths use arrays, `NativeArray<T>`, `NativeList<T>`, or index-based loops over flat registry buffers.

10. Why are coroutines rejected for repeated gameplay behavior?

   Coroutines allocate iterator state and hide scheduling. Repeated gameplay uses explicit state machines driven by dispatcher ticks, with timers stored as fields.

11. Why is `MaterialPropertyBlock` restricted?

    MPB can break SRP Batcher for standard geometry. Per-material data belongs in shader CBUFFERs, instanced data, BRG/GraphicsBuffer pages, or approved UI/particle exceptions.

12. Why are Addressables handles tracked and released explicitly?

    Fire-and-forget asset loads leak memory and hide ownership. Every async asset handle needs an owner, release path, and unload behavior tied to despawn, shutdown, or scene transition.

13. Why is JSON or Easy Save 3 rejected for production save data?

    Save authority is binary delta persistence with checksums, backups, and migration. Text or third-party save paths are too slow, too broad, and not compatible with deterministic world-seed deltas.

14. Why is scene flow fixed to `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`?

    Bootstrap owns service initialization and loading discipline. Main menu owns shell UX. World owns gameplay. Loading heavy terrain/ocean/cave payloads outside that flow risks main-thread stalls and cross-scene references.

15. Why is `Resources.UnloadUnusedAssets()` forbidden after scene unload?

    It can stall and force broad managed/native cleanup at the wrong time. Release queues, Addressables ownership, and controlled low-frame-time GC windows are the accepted cleanup model.

16. Why is Bloom forbidden on MX350/MINIMAL?

    The minimum GPU budget is strict. Bloom is not necessary to preserve underwater readability on the target tier and competes with fog, silhouettes, and UI clarity.

17. Why are shader keywords treated as architecture changes?

    Keywords multiply variants. A new keyword without a warmed and stripped variant path causes shader hitching, memory growth, and build-size bloat.

18. Why does HUD text use char buffers instead of assigning strings?

    `TMP_Text.text = ...` allocates strings. Hot HUD paths write into preallocated `char[]` or span-backed buffers and use allocation-free TextMeshPro APIs.

19. Why must persistent native buffers come from the DataVault?

    Local native allocation fragments ownership and makes relocation, generation checks, disposal, and telemetry unreliable. DataVault handles centralize lifetime, owner id, capacity, generation, and relocation rules.

20. Why are raw prefab, scene, or asset YAML edits restricted?

    Unity YAML has FileID/GUID/property alignment rules. Blind text edits can corrupt assets. Use Unity editor APIs for mutation unless the structure is mathematically certain and then validate it.

21. Why do many docs say `PENDING VERIFICATION` even after source work exists?

    Static source and docs prove text presence only. Runtime readiness needs Unity import, Console, Play Mode, profiler, GCMonitor, player build, memory, frame-time, scene wiring, and visual artifacts.

## Mandatory References

- [AGENTS.md](../AGENTS.md)
- [.agents-skills/README.md](../.agents-skills/README.md)
- [ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md](ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md)
- [ARCHITECTURE/DISPATCH_PIPELINE.md](ARCHITECTURE/DISPATCH_PIPELINE.md)
- [ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md](ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md)
- [ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md](ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md)
- [ARCHITECTURE/AUP_PRECISION_STANDARDS.md](ARCHITECTURE/AUP_PRECISION_STANDARDS.md)
- [QUALITY_GATES.md](QUALITY_GATES.md)

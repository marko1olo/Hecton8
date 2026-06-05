# HECTON-8 Bootstrap, Initialization, And Scene Transition Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: startup, boot state machine, service registration, GlobalRegistry cold setup, data monolith load, hardware detection, non-reload transitions, scene handoff, shutdown, and bootstrap proof gates.

## Prime Law

Boot must make the game predictable before the player is allowed to suffer.

Every runtime owner must be registered, initialized, validated, and failure-routed before gameplay truth starts. HECTON-8 rejects hidden scene order dependencies, lazy runtime discovery, first-frame allocation spikes, service lookup in hot paths, and startup screens that hide broken initialization.

## Truth Ownership

Bootstrap owns initialization order, cold dependency wiring, boot validation, hardware/quality seed, data monolith readiness gate, scene transition fences, and shutdown ordering. It does not own gameplay system truth after handoff.

Runtime systems publish from their owner phases. Bootstrap may inject dependencies and initial immutable snapshots, but it must not become a hot coordinator.

## Presentation-Only Boundary

Splash screens, loading bars, boot captions, transition fades, animated diagnostics, boot audio, and fault panels are presentation-only. They may report explicit boot state, but they must not invent readiness, scene activation, save validity, hardware tier, `GlobalQualityWeight`, or gameplay owner truth.

Progress UI consumes boot state from the bootstrap owner. It must not advance gameplay, mark systems ready, repair missing dependencies, poll scene objects, or hide failed initialization behind cosmetic progress.

## Boot Sequence Law

Required order:

1. platform and hardware facts;
2. logging/proof label setup;
3. native allocator/arena readiness;
4. data monolith/static database validation;
5. GlobalRegistry cold dependency registration;
6. signal lanes and black-box buffers;
7. persistence/session identity;
8. streaming/addressable root setup;
9. systems dispatcher and cadence owners;
10. scene/domain owners;
11. player spawn/readiness;
12. UI transition out of boot only after validation.

Every step must have success/failure state. Failure enters a readable boot fault, not a null-reference cascade.

## Scene Transition And Reset

Rules:

- no stale static state after non-reload transitions;
- clear or rebind GlobalRegistry routes by owner;
- drain signal queues safely;
- stop jobs before disposing native buffers;
- save/session identity survives only through persistence owner;
- player/camera/UI transition waits for required world owners;
- black-box keeps enough pre-fault evidence.

## Runtime Law

Forbidden:

- scene `FindObjectOfType` as dependency injection;
- service discovery in hot paths;
- late creation of core native buffers during gameplay;
- gameplay starting before data monolith validation;
- hidden coroutine boot chains with no state record;
- swallow-and-continue startup exceptions.

## GlobalQualityWeight Scaling

Bootstrap computes or receives initial quality facts and publishes them through the approved route. `GlobalQualityWeight` may influence boot-time asset preloads, diagnostics depth, shader warmup breadth, capture verbosity, and optional validation depth. It must not change ownership, DTO layout, save identity, or authority routes.

Low/Middle/High/Ultra are continuous planning labels on the same `GlobalQualityWeight` curve, not binary switches:

- Low: minimal warmup breadth, required owner validation, compact diagnostics, clear boot faults.
- Middle: broader preload validation, more transition polish, fuller dependency summaries.
- High: deeper shader/material warmup, richer transition presentation, expanded optional diagnostics.
- Ultra: maximum nonblocking validation/capture verbosity and presentation polish without changing boot truth, scene order, or authority routes.

## First-20 Route Hook

Bootstrap first-20 responsibility is to hand off into the playable route only after required owners, data, input, player, camera, UI, world, and proof labels are valid. A beautiful loading transition cannot mask missing first-20 readiness.

## Production Packet

Any bootstrap, startup, initialization, registry, or scene-transition change must declare:

- boot state list and order;
- cold dependency registration route;
- required data monolith or static asset readiness;
- fallback when dependency, data, or scene load fails;
- native allocation and disposal ownership if touched;
- scene transition rules and non-reload behavior;
- Compact and High startup proof if player-visible;
- profiler/GC proof when runtime boot code changes.

Bootstrap that depends on scene search, hidden singleton creation, or hot-path self-repair is rejected.

## Proof Artifacts

Bootstrap work must provide:

- boot state list;
- dependency route table;
- data monolith readiness proof if touched;
- non-reload transition proof if touched;
- native allocation/disposal proof if touched;
- startup fault behavior screenshot/log;
- profiler/GC proof if startup performance is claimed.

## Rejection Gates

Reject:

- hidden scene dependency;
- hot GlobalRegistry polling;
- gameplay starting before required owners;
- startup nulls hidden behind splash screens;
- unbounded boot coroutines;
- no shutdown/dispose path;
- "works in editor" as boot proof.

## Acceptance Sentence

Bootstrap is accepted only when initialization order is explicit, failures are readable, owners are wired cold, gameplay starts after validation, scene transitions do not leak stale state, and runtime claims have proof.

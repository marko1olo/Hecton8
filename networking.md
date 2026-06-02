# HECTON-8 Networking, Rollback, And Shared-State Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: co-op readiness, rollback, Merkle state deltas, logistics sync, bit packing, authority, reconciliation, input/state ownership, transport proof, and public networking claims.

## Prime Law

Networking is not a feature label. It is a proof burden. HECTON-8 may prepare every system for future shared state, rollback, and co-op, but no document, UI, store text, or report may claim operational multiplayer until transport, rollback, save, authority, profiler, and device evidence exist.

The project rejects two equal failures:

- shipping singleplayer systems that are impossible to reconcile later;
- claiming co-op or netcode because static docs, stubs, or Merkle diagrams exist.

Every network-facing fact must have one authority, one packed representation, one reconciliation route, and one failure response. Shared state is not allowed to be "whatever the local Unity scene currently looks like."

## Truth Ownership

Network code does not own gameplay truth. It transports, predicts, verifies, repairs, and rejects state owned by gameplay domains.

Authoritative owners:

- `player.md` owns input intent and local control feel.
- `survival.md` owns oxygen, pressure, trauma, and physiology truth.
- `construction.md` owns power, oxygen, fluid, storage, and module network decisions.
- `vehicles.md` owns submarine, suit, docking, EVA, and platform-relative vehicle truth.
- `persistence.md` owns save identity and replayable deltas.
- `math.md` owns deterministic AUP coordinates, tick comparison, RNG, and replay math.
- `data.md` owns packet/DTO layout and alignment.

Networking owns packet shape, tick envelope, prediction window, rollback window, Merkle hashes, desync detection, reconciliation, transport error handling, and proof artifacts.

## State Authority

Each shared fact must declare its authority class:

- host-authoritative;
- owner-authoritative with server validation;
- deterministic lockstep;
- presentation-only replicated hint;
- mod/SDK envelope request;
- non-networked local-only state.

No system may become networked by sending full local state every frame. The network route transmits commands, deltas, summaries, hashes, or snapshots. Scene transforms, GameObject names, MonoBehaviour references, runtime materials, and visual-only state are not network identity.

## AUP Wire Position Law

World position over wire uses AUP, not float world position.

Required shape:

- sector or grid coordinate;
- quantized local offset;
- yaw/pitch/roll where needed;
- timestamp or tick id;
- owner id;
- authority flags;
- checksum or hash where relevant.

Forbidden:

- raw `Vector3` world position as authoritative network state;
- string scene path as network identity;
- transform parent hierarchy as shared truth;
- quality tier changing packet layout;
- client-side generated ids without deterministic seed and owner route.

## Rollback And Merkle Law

Rollback state is narrow. It includes only gameplay truth required to reproduce decisions. It excludes render targets, VFX, audio playback, UI animation, texture streaming, generated visual meshes, editor gizmos, and local comfort options.

Every rollback-visible domain must define:

- state record layout;
- hash input fields;
- excluded fields;
- snapshot cadence;
- rollback window;
- restore method;
- mismatch detection;
- black-box dump fields;
- local loopback proof.

Merkle repair is accepted only when the owner can identify the divergent domain and apply a bounded repair. If the mismatch cannot be localized, the state must reset from an authoritative full snapshot. Partial merging of desynchronized ring buffers is rejected.

## Logistics Sync

Logistics networks use dirty graph deltas, bit packing, and summaries. They do not simulate cable signal propagation, electrons, plasma, or per-meter waves.

Required:

- network id or node id;
- topology revision;
- dirty bitmask;
- quantized ratios for oxygen, power, pressure, thermal, fuel, coolant, data link, structural integrity, and alarm flags where relevant;
- tick id using overflow-safe modular comparison;
- snapshot ring for interpolation;
- full snapshot route for desync repair;
- fixed per-frame processing cap.

Long-distance or nonresident zones store compressed summaries. Same-zone gameplay reads authoritative local state. Remote visuals interpolate; they do not own gameplay.

## Prediction And Reconciliation

Prediction is a visual and input-latency tool, not a license to invent truth.

Allowed prediction:

- local player input preview;
- UI confidence state;
- remote visual interpolation;
- vehicle cockpit needles from recent snapshots;
- logistics gauge smoothing;
- presentation-only creature pose smoothing.

Rejected prediction:

- oxygen or death state guessed by presentation;
- inventory mutation before authority accepts;
- construction placement committed before owner validation;
- damage applied from predicted collision alone;
- save state written from predicted state.

Reconciliation must report what changed. Silent correction is not accepted when it changes a player-visible or saved fact.

## Transport Boundary

Transport is replaceable. Gameplay code must not depend on a specific UDP, Steam, relay, platform, or third-party API. Transport code packages bytes and delivers packets. It does not parse gameplay truth without a domain owner.

Transport failures must map to readable states:

- packet late;
- packet stale;
- packet future;
- packet duplicate;
- owner mismatch;
- checksum mismatch;
- bandwidth cap;
- rollback mismatch;
- full snapshot required;
- disconnect or migration required.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale packet cadence for presentation-only hints, interpolation sample depth, optional diagnostics, visual proxy update rate, noncritical replicated ambience, and telemetry verbosity.

It must not change gameplay authority, packet ABI, save identity, deterministic tick math, rollback hash fields, or whether a command is valid.

Compact uses sparse presentation hints, strict packet caps, shorter diagnostics, and stronger local interpolation. Middle keeps full gameplay truth with conservative prediction. High adds richer remote presentation and diagnostics. Ultra may add denser non-authority state only after bandwidth and CPU proof.

## Proof Artifacts

Networking work must provide:

- authority class for every shared fact;
- packet/DTO layout with byte size;
- AUP coordinate proof for spatial packets;
- rollback-visible and rollback-excluded field list;
- local loopback proof;
- accepted command and rejected command proof;
- desync detection and full snapshot recovery proof;
- bandwidth cap and per-frame processing cap;
- zero-GC hot-path proof;
- profiler proof for packet ingest, reconciliation, prediction, and restore;
- save/load proof when networked state persists;
- explicit `PENDING RUNTIME VERIFICATION` when only static documents or stubs exist.

## Rejection Gates

Reject networking work if:

- it claims multiplayer, co-op, rollback, or Merkle repair without runtime proof;
- it sends raw float world position as authority;
- it uses GameObject, Transform, scene name, or prefab handle as network identity;
- it broadcasts full state every frame;
- it changes packet layout by quality tier;
- it applies predicted gameplay truth to save or persistence;
- it silently corrects a player-visible consequence;
- it allocates or parses JSON in packet hot paths;
- it hides transport-specific code inside gameplay owners;
- it has no desync recovery route.

## Acceptance Sentence

Networking is accepted only when every shared fact has explicit authority, compact deterministic packet shape, AUP-safe spatial identity, rollback/hash boundaries, bounded reconciliation, readable failure modes, and runtime proof separating static design from operational multiplayer.

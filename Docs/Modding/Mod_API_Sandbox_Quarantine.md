# Mod API Sandbox Quarantine

Date: 2026-05-19
Status: ENVELOPE-ONLY RUNTIME QUARANTINE / SDK AUTHORING REQUIRED / PENDING RUNTIME VERIFICATION

Runtime UGC command execution is binary-only. Managed mod entry points are disabled at load time; boot-registered managed factories are rejected while envelope-only mode is active. Mods must serialize fixed 64-byte `FutureCommandEnvelope` packets and submit them through `HectonAPI.Commands.RequestFuture`.

`RequestFuture` is still an owned public facade, not an anonymous pipe. It requires an active `ModExecutionScope`, and the envelope `ModderSignature` must match the active mod hash. Engine package-loader and editor bulk paths use internal validator routes. `FutureCommandSandboxConstants` is internal control-plane data; mods may use only `FutureCommandEnvelope.SizeBytes` for the 64-byte packet layout fact, not runtime budgets, fault hashes, or tuning caps.

`ModExecutionScope` itself is part of the owner proof. It cannot create an active anonymous/blank owner and `HasActiveMod` requires a non-zero owner hash, so public facade guards cannot be satisfied by a synthetic `"anonymous"` scope.

Subtitle cue opcodes are reserved localization aliases. `TriggerSubtitleCue` and `SubtitleCue` must not appear in `allowed_opcodes.csv`, `GenerateEmergencyMockOpcodes()`, or the editor runtime opcode tuner until the localization owner provides token, quota, rejection, unload, zero-GC subtitle path, and runtime playbook proof.

The legacy `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` surfaces are hard-quarantined: they require active `ModExecutionScope`, then return `false` while envelope-only UGC is enforced. `ModCommandDispatcher.Initialize()` initializes the `FutureCommandSandboxValidator` first and does not allocate the old `NativeQueue` / `NativeHashMap` command lanes unless the dormant legacy surface is explicitly re-enabled in source. Current UGC therefore has no managed command callback path and no legacy dispatcher allocator boot cost.

The public `HectonAPI.Events` subscribe/publish surfaces are also hard-quarantined in current runtime: anonymous calls must fail active `ModExecutionScope` ownership before envelope-only status is reported. Inside a valid mod scope, managed event bridge calls still throw the envelope-only quarantine exception until a first-party runtime owner reopens the bridge with playmode, GC, watchdog, and unload proof.

In envelope-only mode, manifest discovery does not resolve conventional `.dll`, `.bundle`, or `lang_*.json` entry files as runtime ingress. Package identity is still validated first: mod ids and dependency ids must be canonical lowercase token segments, and `EntryAssembly` must be a package-local `.dll` file name rather than a path. Explicit `EntryAssembly`, `EntryType`, or any top-level package `.dll` marks the package as a managed-entry candidate and the loader disables that candidate before execution. Managed DLL file names and metadata identities are still validated across every top-level package DLL: engine/runtime names such as `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, and `netstandard` are rejected so a future managed-mode reopening cannot use friend-assembly spoofing to bypass the public facade. Content-only filesystem ingestion is also disabled; UGC assets must be approved by CRC and referenced by 64-byte `FutureCommandEnvelope` asset opcodes.

## Modder-Facing Model

Modders should not be asked to hand-pack binary envelopes for normal authoring. The project needs an SDK layer:

- Workbench UI for manifest, capability, graph, asset, localization, and settings authoring;
- CLI packer for `init`, `validate`, `simulate`, `pack`, `dump-envelope`, and rejection explanation;
- command graph compiler that proves bounded envelope emission;
- asset importer that emits CRC-approved references instead of loose runtime files;
- local sandbox simulator for thermal, rollback, quota, and DevNull behavior;
- readable validation reports for Workshop/community support.

These tools may use managed code and friendly interfaces because they are offline/editor surfaces. The player runtime still receives only validated package metadata and 64-byte envelope streams. There is no runtime C# interface for modders to implement while this quarantine is active.

The detailed SDK plan is [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

Bulk ingress must avoid per-packet Vault resolution, but it is engine-only. `FutureCommandSandboxValidator.RequestRawEnvelopeStream(...)` and `RequestFromExternalQueue(...)` are internal first-party/package-loader routes for flat binary streams or owned producer queues. `MockModQueue` does not expose a public `NativeQueue` handle or public instance control methods. Public runtime mods submit through `HectonAPI.Commands.RequestFuture` only.

Scheduler integration is also first-party only. It may use `FutureCommandSandboxValidator.TrySchedulePreSimulation(dependsOn, out JobHandle validationHandle)` when the caller owns a dispatcher dependency graph. The current void `ModCommandDispatcher` drains only the future-envelope validator while the legacy command surface is disabled; the scheduled path lets the integrator chain validator work without an immediate main-thread fence. Forced completion remains reserved for teardown or scene-transition boundaries.

`FutureCommandEnvelope` layout:

- `uint OpcodeHash`
- `uint ModderSignature`
- `double3 TargetAUP`
- `float4 PayloadData`
- `ulong IntegrityHash`
- `ulong _pad0`

The validator runs from `ModCommandDispatcher.DrainPreSimulation`, checks opcode allowlist, XXHash3 integrity over bytes `0..47`, finite +/-50 km AUP bounds, CRC32-approved asset references, declared asset byte ceiling, per-signature flood budgets, CPU-overheat backlog shedding through effective quality, and rollback freeze state. Effective quality starts from `GlobalQualityWeight`, optionally applies the editor/test override, then collapses through `CpuThermalPressure01` using a smooth polynomial curve before command budgets and backlog shedding are calculated. Platform monitors may report heat through the internal validator route; that is not SDK/public mod API.

All persistent runtime state is Vault-owned. The validator stores only `VaultBufferHandle<T>` fields and resolves short-lived `NativeArray<T>` views per phase. The pending ring, DevNull ring, staging buffer, opcode records, per-mod counters, memory leases, approved asset manifest, ring state, tuning, blackbox memory, and telemetry ring use `BufferID.ShinobuModSandbox*` IDs. The legacy `NativeHashSet`/`NativeHashMap` implementation was removed in favor of fixed-size open-address tables to avoid private allocator state.

The modder blackbox memory arena is Vault-backed at `BufferID.ShinobuModSandboxBlackboxMemory`. Core simulation ignores this memory; only explicit memory opcodes can read/write inside the mod's assigned chunk. The 300-frame quarantine ring writes to `BufferID.ShinobuModSandboxTelemetryRing` and dumps `Docs/AgentLogs/Dump_QUARANTINE_SURGEON.bin` on memory, NaN, or layout faults.

Rollback freeze is read as a local 64-byte Vault flag view at buffer `70752`, flag bit `1 << 4`. This avoids a direct `Hecton8.Networking` runtime assembly reference from the sandbox validator while preserving Agent 64 resimulation quarantine.

Human controls live in `HECTON-8/Mod API Sandbox Tuner`. The editor facade adjusts continuous command budget, max mod memory, asset ceiling, CPU thermal pressure, opcode gates, CSV opcode reload, self-audit injection, and incoming/rejected traffic histogram. Self-audit is a real single-envelope Burst validation probe: it injects a valid-hash malicious AUP into Vault staging and expects an `InvalidAup` rejection, not merely a successful enqueue.

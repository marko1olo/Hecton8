# SHINOBU_273 Frequency Tuning Decryption Route Card



Status: YELLOW / STATIC SOURCE VERIFIED / UNITY IMPORT PENDING

Owner: SHINOBU_273, Echelon 8 Presentation & UX

Domain: FREQUENCY_TUNING_DECRYPTION_KERNEL

Last updated: 2026-05-21



## Authority



One fact: frequency tuning puzzle state.

One owner: `TerminalOsRuntime`.

One route: `GlobalDataVault` unmanaged DTOs plus `SignalBus<TerminalUnlockedSignal>`.

One proof artifact: `DecryptionTelemetryEntry[300]` and `Docs/AgentLogs/Dump_SHINOBU_273.bin`.



Runtime must not unlock doors, mutate sibling-domain objects, or poll scene objects from decryption kernel.

Solved state emits as unmanaged signal. Downstream gameplay decides what a node hash unlocks.



## Vault Buffers



| BufferID | Name | Type | Length | Owner |

| --- | --- | --- | --- | --- |

| 71376 | `TerminalDecryptionPuzzles` | `DecryptionPuzzleDTO` | 64 | `SystemID.UI` |

| 71377 | `TerminalDecryptionTerminals` | `DecryptionTerminalDTO` | 64 | `SystemID.UI` |

| 71378 | `TerminalDecryptionKnobInput` | `DecryptionKnobInputDTO` | 1 | `SystemID.UI` |

| 71379 | `TerminalDecryptionTelemetryRing` | `DecryptionTelemetryEntry` | 300 | `SystemID.UI` |



`DecryptionPuzzleDTO` is the prompt-required 32-byte explicit layout. The runtime avoids false sharing by evaluating puzzle rows in a fused single Burst `IJob`, not by parallel writes to adjacent 32-byte rows.



## Signal Lane



Signal: `TerminalUnlockedSignal`, 32 bytes, `ISignal`.

Lane hash: `0x5444554E` (`TDUN`).

Capacity: 64 retained rows, 8 fallback rows.

Producer phase: `TerminalOsRuntime.LateFrameTick()` finalizes the decryption job in the owner phase only.

Consumer phase: downstream systems consume `SignalBus<TerminalUnlockedSignal>` by contract; no direct runtime assembly dependency is introduced by SHINOBU_273.



## Timing



- Puzzle mutation uses `HectonPhysicsContract.FixedDeltaTimeSeconds`, not Unity frame delta.
- Decryption scheduling requires a nonzero `SystemDispatcher.CurrentFrameId`; if dispatcher frame identity is unavailable, the unlock-producing solver is not scheduled.
- A pending decryption job finalizes against its stored simulation frame, never a Unity `Time.frameCount` fallback.
- Idle evaluation cadence derives continuously from `GlobalQualityWeight`.
- Stride range: 1 to 6 frames.
- Active knob input forces stride 1.
- Interaction truth does not degrade under low quality.



## Cold Registry Boundary



`GlobalRegistry` is used only for cold identity/dependency bootstrap.

If Vault or dispatcher services are unavailable, `TerminalOsRuntime` schedules bounded retries. Backoff is continuous `GlobalQualityWeight`, `30..120` frames.

No decryption Burst job, read accessor, or hot helper polls `GlobalRegistry`.



## Shader ABI



- `Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader` reads `StructuredBuffer<GlobalDecryptionPuzzleDTO> _GlobalDecryptionPuzzles` and `_GlobalDecryptionPuzzleCount` from the terminal material path.
- The runtime writes a double-buffered GPU mirror with `GraphicsBuffer.LockBufferForWrite`; `_GlobalDecryptionPuzzleCount` is the last successful bounded upload count clamped by terminal capacity, not a blind `_terminalCount`.
- Upload failure clears the material read count to zero so the shader fails closed instead of reading stale or beyond-upload DTO rows.
- The shader draws target/player sine waves, static noise, and solved tint directly on the terminal surface.
- `_HectonDecryptionNoiseDensity` is a continuous scalar from the editor facade and material binding path.
- There is no Canvas, LineRenderer, TMP waveform, or CPU mesh polyline.


## Fault Export



- The owner frame records `DecryptionTelemetryEntry[300]` in Vault.
- On non-finite state or >0.1 ms solver budget, the owner frame copies fixed telemetry rows oldest-to-newest into a cold-created `DecryptionBlackBoxDumpWriter` command and returns.
- Disk I/O for `Dump_SHINOBU_273.bin` is handled by the background writer thread.
- The dump format is a 24-byte little-endian header followed by raw 64-byte `DecryptionTelemetryEntry` rows.
- Backpressure is reported through `GlobalTelemetryBus.PublishPerformanceWarning` with `FaultDecryptionDumpBackpressure`; the owner frame does not call `FileStream` or `BinaryWriter` from the decryption fault path, and the decryption writer does not use `BinaryWriter`.



## Data Monolith Boundary



Production DataMonolith readiness is not claimed. Current X_012 scan finds `static_data.h8bin`; route boot proof remains pending. `decryption_puzzles.csv` and mock puzzles are editor/development fallbacks.



## Proof State



- Static scans verify DTO layout declarations and SignalBus route.
- UI/input gates: no Canvas/GraphicRaycaster/LineRenderer in `TerminalOS`; no direct `Time.unscaledDeltaTime` in decryption input.
- Read/pointer gates: pure SHINOBU_273 public reads; unsafe pointer safety proofs for two decryption pointer fields.
- Export/retry gates: background-only decryption dump export; bounded cold-registry retry backoff.
- Unity import, Play Mode, profiler, GCMonitor, shader import, and player build proof remain pending under the project CPU/build guard.



Subagent audit closure:

- `TryDequeueCommand`: fails closed while click resolution is scheduled.
- Rejected route: public read finalizing a job.
- `Minigame_Canvas_Inquisition`: targeted terminal source/asset token absence only.
- Non-claim: project-wide Canvas purge.
- `TerminalStateDTO.IsDirty`: intentionally packed into `BackgroundColor` byte 7.
- Reason: `TerminalBlit.compute` reads RGB only.
- ABI proof: editor layout validator.



Loop 9 CI math closure:

- Terminal interaction distance has no `math.sqrt` or `math.length` token in SHINOBU_273 TerminalOS scope.
- Terminal plane sizing has no `math.sqrt` or `math.length` token in scope.
- `SafeDistanceFromSq` and `SafeVectorLength` use finite-guarded `dot + rsqrt` helpers.
- Minimum denominators stay explicit.
- Rejected: Unity `Vector3` normalization and `Mathf` routes.



Loop 10 read purity closure:
- public copy getters now use `TryReadVaultBuffer`
- `TryReadVaultBuffer` resolves through `GlobalDataVault.TryReadHandle<T>`
- `TryResolveHandle<T>` remains owner/write scheduling and mutation only
- stale or fenced read accessors no longer record Vault generation faults or debug counters



Loop 11 public mutation surface closure:
- private owner helpers: `OpenTerminalStateRefForOwner`, `ForceDirty`, `ForceAllDirty`
- bounded owner APIs remain for external/editor callers
- allowed APIs include `TrySetDecryptionTarget`, `ApplyDecryptionEditorTuning`, `TrySetTerminalMockState`, `SetScreenCommand`, `SetTerminalAvailability`
- no public mutable-ref escape hatch remains



Loop 13 shader variant closure:

- `Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader` no longer declares `shader_feature_local HECTON_TERMINAL_INSTANCED`.
- `TerminalOsRuntime` no longer toggles `HECTON_TERMINAL_INSTANCED`.
- Instanced mode is driven by `_HectonTerminalInstancedMode`.
- Existing `_TerminalPanelInstances` buffer remains material-bound.
- Terminal material path keeps one shader variant.



- Loop 16 shader read-bounds closure: `_GlobalDecryptionPuzzles` upload count is bounded by `_terminalCount`, Vault row count, and GPU buffer capacity.
- `TerminalOsRuntime` stores `_decryptionPuzzleUploadCount` only after a successful copy, binds `_GlobalDecryptionPuzzleCount` from that value, and clears the material count to zero on upload failure.
- This keeps the Dear Lie oscilloscope from sampling stale or never-uploaded rows when Vault relocation or reduced capacity changes the available source length.



- Loop 17 dispatcher-frame and Vault-count closure: `TerminalOsRuntime` no longer passes Unity `Time.frameCount` into decryption schedule, knob input, telemetry, or `TerminalUnlockedSignal`.
- `TryScheduleDecryptionPipeline` runs only after `SystemDispatcher.CurrentFrameId` resolves. `TryFinalizeDecryptionJob()` records telemetry against stored `_decryptionScheduleFrame`, not current dispatcher/Unity frame.
- The fused solver row count is clamped by puzzle and terminal Vault lengths, zero-length knob input fails closed, and `ValidateNativeBuffers()` refuses `_nativeResourcesReady` unless terminal/decryption buffers meet their requested capacities.



- Loop 18 terminal wrapper bounds closure covers public terminal copy/write helpers, dirty flags, text formatting, interaction jobs, screen command uploads, panel instance uploads, bounds recomputation, and layout hashing.
- Clamp source: current Vault and GPU buffer lengths, not `_terminalCount` after handle relocation.
- Visual compute glitch time now uses owner-frame fixed-step seconds instead of `Time.unscaledTime`.
- The owner terminal black-box dump now uses a raw little-endian header and raw `TerminalTelemetryEntry` rows rather than `BinaryWriter`.

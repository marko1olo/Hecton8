# LOG_CONSOLE_MEDIC

## 2026-05-13 23:03:52 +04:00

What was wrong:
- Current Roslyn sweep found stale Bee graph/source drift after parallel edits: root compile referenced new `HomeostasisBrain`, `VisorDropletSignal`, and diegetic damage hologram contract symbols before the generated rsp had caught up.
- `Hecton8.Core.asmdef` missed `Hecton8.UI.Diegetic.Contracts` while `VehicleSubOsCockpitRuntime` implements that contract.
- `BinaryBlittableSafeAttribute` warning spam came from duplicate root/type ownership.
- `HectonPlayerMovement` still had one deprecated `GetInstanceID()` call.
- `GlobalDataVault` needed current imports for defrag watchdog/gap audit code.
- Unity log showed MapMagic preview shader unsupported warnings and MCP WebSocket/session failure.

What was done:
- Added `Hecton8.UI.Diegetic.Contracts` reference to `Assets/_Project/Scripts/Hecton8.Core.asmdef`.
- Moved `BinaryBlittableSafeAttribute` source ownership into `Assets/_Project/Scripts/Core/Memory/BinaryBlittableSafeAttribute.cs` and removed the root duplicate.
- Replaced the remaining ladder-cache `GetInstanceID()` use with `EntityId.ToULong(collider.GetEntityId())`.
- Added required `System.Diagnostics` / `Unity.Jobs` imports to `GlobalDataVault`.
- Added minimal editor-preview fallback SubShaders to both MapMagic terrain preview shaders.

Cinematic Cheats used:
- MapMagic preview fallback is a cheap editor-only texture preview path, not a physical terrain-lighting simulation.

Exact Microseconds saved:
- Runtime hot path: 0 us claimed. C# fixes are compile/type-boundary repairs.
- Ladder cache: equivalent integer lookup; avoids future obsolete API break with no managed allocation.
- MapMagic fallback: 0 us runtime; editor import fallback only.

Verification:
- `Hecton8.Core.Memory.rsp` manual Roslyn compile: clean.
- `Hecton8.Core.rsp` manual Roslyn compile with pending imported source additions and diegetic contract reference: clean.
- `git diff --check` on touched files: no whitespace errors.
- `read_console`: blocked, MCP returned `no_unity_session`; active Unity log shows MCP server/session stopped.

## 2026-05-13 23:50:53 +04:00

What was wrong:
- Fresh Roslyn compile found new concurrent `GlobalDataVault` drift: removed `_defragCursor` was still referenced, a synchronous gap audit carried a `Unity.Burst.BurstCompile` attribute without a Burst reference, and the same audit used `Unity.Mathematics.math.rcp` without a Mathematics reference.
- The defrag black-box path had drifted to `Dump_AGENT_HOMEOSTASIS_METABOLISM.bin`, which would mislabel memory-defrag crash evidence.
- Unity log showed the MapMagic preview shader import warning source had become mixed line endings after fallback insertion.
- MCP bridge remained non-authoritative: refresh timed out and `read_console` returned `no_unity_session`.

What was done:
- Removed the stale `_defragCursor` reset, removed the stray Burst attribute, replaced `math.rcp` with a local float divide, restored `Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin`, and removed dead high-tier bypass residue.
- Normalized `TerrainPreview.shader` and `TerrainPreviewURP.shader` to consistent CRLF line endings and removed trailing whitespace on the shader declaration.
- Re-ran the current 1300 Android/editor compile graph and the stale 1900 graph with its missing source additions supplied explicitly.

Cinematic Cheats used:
- MapMagic fallback remains an editor-only texture preview fake. No runtime terrain simulation was added.

Exact Microseconds saved:
- Runtime hot path: 0 us claimed.
- Memory audit fix avoids new Burst/Mathematics dependency load; cold audit behavior remains native/no-GC.
- Shader line-ending cleanup is editor/import-only, 0 us runtime.

Verification:
- `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.Memory.rsp`: clean.
- `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.rsp`: clean.
- `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.Memory.rsp`: clean.
- `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` plus `HomeostasisBrain.cs` and `PrologueReentrySignals.cs`: clean; plain 1900 rsp is stale and missing those sources.
- Byte audit: both MapMagic preview shaders have LF count equal to CRLF count, so no mixed line endings remain on disk.
- `git diff --check` on touched shader/core-memory files: no whitespace errors except Git CRLF normalization notice for `GlobalDataVault.cs`.
- `refresh_unity(wait_for_ready=true)`: timed out after 60 seconds.
- `read_console`: blocked, MCP returned `no_unity_session`.

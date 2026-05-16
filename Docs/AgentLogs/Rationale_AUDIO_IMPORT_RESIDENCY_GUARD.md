# AUDIO_IMPORT_RESIDENCY_GUARD Rationale

PROMPT IDENTIFIED: AUDIO_IMPORT_RESIDENCY_GUARD | DOMAIN: CORE/AUDIO | TASK COUNT: 20

## Decision 0: Persistent State Bootstrap
Problem: Required status and rationale files did not exist in the active project path, and the mandatory `powershell "cat Docs/Tasks/Status_AUDIO_IMPORT_RESIDENCY_GUARD.md"` read from `C:\hades` failed.
Solution: Create project-root status and rationale files under `C:\hades\Hecton8\Docs\Tasks` and `C:\hades\Hecton8\Docs\AgentLogs` before code edits.
Rejected Alternatives: Chat-only tracking was rejected because context compression is explicitly hostile and disk state is required. Writing status under `C:\hades\Docs` was rejected because authoritative project files live under `C:\hades\Hecton8`.
Scalability potential: Low/Middle/High/Ultra unaffected directly; this prevents agent-state loss, not runtime cost.
Hardware Impact: 0 microseconds runtime impact on i3/MX350; build-time bookkeeping only.

## Decision 1: Loop 1 Import Policy Authority
Problem: Raw long audio clips can be imported as DecompressOnLoad and reserve tens of megabytes before the menu.
Solution: Add `Assets/_Project/Scripts/Audio/Editor/AudioImportDictator.cs` as the last first-party audio import authority. Clips longer than 5 seconds become Streaming, sub-2-second clips become ADPCM/DecompressOnLoad, spatial 3D domains force mono, and preload is limited to short Player/Creatures/Interface clips.
Rejected Alternatives: Manual inspector cleanup was rejected because it regresses silently. Leaving ambient/music as always CompressedInMemory was rejected for this task because the assignment's RAM explosion is caused by long clips being resident. Streaming sub-2-second SFX was rejected because latency is audible and AGENTS bans streaming SFX.
Scalability potential: Low uses 22050 Hz non-music imports, forced mono 3D, and zero speculative preload. Middle keeps Vorbis compressed memory for medium clips. High keeps 44100 Hz music and can spend saved RAM on richer acoustic beds. Ultra can preserve music quality while the same residency math prevents uncontrolled preload.
Hardware Impact: On i3/MX350, a single 20 MB WAV no longer expands into boot-resident decoded memory. Expected gain is clip-dependent: 800-2200 us boot-load stall avoided per long decompressed clip and 10-80 MB RAM avoided across several clips.

## Decision 2: 50 MB Build Kill Switch
Problem: A build can still ship with preloaded clips if an importer is dirty or another tool edits settings.
Solution: Add `AudioRamBudgetBuildGate` implementing `IPreprocessBuildWithReport`; it estimates preloaded residency and throws `BuildFailedException` above 50 MB with the largest offenders.
Rejected Alternatives: Editor menu-only validation was rejected because it depends on a human. Runtime warning was rejected because the Quest/i3 failure mode is OOM before gameplay.
Scalability potential: Low/Middle/High/Ultra all use the same hard budget for preload; high tiers can stream richer content but cannot bloat boot RAM.
Hardware Impact: 0 us runtime overhead. Prevents OOM-class boot spikes; expected saved residency is the full amount above 50 MB.

## Decision 3: Loop 1 Compile Result
Problem: `dotnet build Hecton8.slnx` failed after 3m43s with unrelated dependency errors before a clean audio verdict.
Solution: Record compile as `[BLOCKED BY DEPENDENCY]` and continue per fail-fast protocol. Primary blockers: deleted RealtimeCSG source references, missing `SanitizeFinite`, missing visor blackbox methods, `IDataVault` type identity split, and missing signal sanitizer symbols.
Rejected Alternatives: Reverting unrelated deleted RealtimeCSG files or repairing other agents' Core edits was rejected as cross-domain sabotage. Marking green was rejected because build output is objective failure.
Scalability potential: Not runtime-relevant; compile wall blocks verification only.
Hardware Impact: 0 us runtime impact from this decision.

## Decision 4: Environment Prefab AudioSource Purge
Problem: Environment prefabs can bypass the SignalBus acoustic pipeline by owning Unity `AudioSource` components directly.
Solution: Add `EnvironmentAudioSourcePurgeGate` to strip environment prefab AudioSources through `PrefabUtility.LoadPrefabContents` and fail builds if the components return.
Rejected Alternatives: Raw YAML prefab mutation was rejected because prefab serialization is not a stable interface. Warning-only validation was rejected because the assignment requires a purge, not advice.
Scalability potential: Low uses zero resident ambient components and relies on centralized streaming. Middle keeps authored prefabs clean. High/Ultra can spend saved component overhead on richer SignalBus beds without uncontrolled source proliferation.
Hardware Impact: On i3/MX350, each stripped ambient source avoids roughly 20-120 us activation/setup cost and prevents hidden clip residency. Current raw scan found no environment prefab offenders; the gate prevents regression.

## Decision 5: Music Residency Release
Problem: Biome track changes stopped voices but did not explicitly unload old clip data from RAM.
Solution: Register music clips in the residency cache and call `AudioResidencyCache.ReleaseClip` from `StopVoiceImmediate`, which hard-unloads loaded audio data after the voice is stopped.
Rejected Alternatives: Rewriting the music director into coroutines or new streaming handles was rejected because the existing director already uses SlowTick/Update mathematical fade state and is lower-risk.
Scalability potential: Low streams one bed and frees the previous one. Middle keeps crossfade behavior. High/Ultra can use richer beds while old tracks are evicted instead of stacking residency.
Hardware Impact: On i3/MX350, expected savings are 1-40 MB per old music bed depending on clip import settings; frame cost is near zero because release happens on voice stop.

## Decision 6: Runtime LRU And Distance Cull
Problem: Repeated creature/world clips can thrash disk when not resident, while far clips can enter Unity source setup before being audibility-rejected.
Solution: Add a fixed 64-slot `AudioResidencyCache` with a 16 MB decoded budget and gate all main 3D paths against `_maxDistance` before `AudioSource` acquisition or cache touch.
Rejected Alternatives: Dictionary-based LRU was rejected for hot-path allocation/cache unpredictability. Post-load volume muting was rejected because it still burns RAM and source setup.
Scalability potential: Low uses culling and a small deterministic cache. Middle keeps common creature cues warm. High increases perceived density through reuse without disk spikes. Ultra can layer more sounds while the same cull prevents inaudible waste.
Hardware Impact: On i3/MX350, expected savings are 8-90 us per far rejected clip and 50-300 us per repeated roar that avoids a reload burst.

## Decision 7: Tool Audio Prewarm Cross-Domain Exception
Problem: Laser Cutter and Repair/Welder loop clips live on tool classes outside `Scripts/Audio`, so an audio-only listener cannot guarantee equip-time residency without polling.
Solution: Add direct `AudioResidencyCache` calls in `LaserCutter` and `RepairTool` `OnEquip`/`OnUnequip`/despawn paths. This is a limited cross-domain edit tied to Task 10 and the serialized tool AudioSources.
Rejected Alternatives: Boot preloading was rejected because Quest/i3 RAM is the failure mode. Slow polling `CurrentTool` was rejected because it adds recurring work and can miss serialized loop references.
Scalability potential: Low keeps tool audio out of boot RAM. Middle pays load at explicit equip. High/Ultra can use stronger tool loops without making them permanent residents.
Hardware Impact: On i3/MX350, boot residency is reduced by the full size of unequipped tool clips; equip cost is estimated at 150-900 us depending on clip size and import state.

## Decision 8: Loop 2 Compile Result
Problem: A focused runtime build still cannot complete due to a non-audio dependency error.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed only on `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs(433,13): ValidateAbiLayout` missing. No audio-specific diagnostics were emitted for the modified runtime audio/tool files.
Rejected Alternatives: Repairing `GlobalDataVault` was rejected as outside CORE/AUDIO authority. Claiming compile success was rejected because the command exited 1.
Scalability potential: Not runtime-relevant; this is verification state.
Hardware Impact: 0 us runtime impact from this decision.

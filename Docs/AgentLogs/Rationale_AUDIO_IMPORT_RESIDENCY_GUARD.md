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

# Runtime Surface Binding Map - AppliedLore 1778

Evidence class: STATIC_SOURCE / CLI_AUDIT

## PDA Encyclopedia

VERIFIED: `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs` can seed metadata from `H8AppliedLoreRuntime.GetPacketRecords()`, resolve active locale through `H8AppliedLoreRuntime.ResolveLocaleHash`, fetch surfaces through `TryGetAppliedLoreUtf8`, write display text through `TryWriteAppliedLoreSurfaceUtf16`, and gate entries through `TryFindRouteForPacket` plus route prerequisite hashes.

Runtime role: PDA encyclopedia is a read-only consumer of baked AppliedLore packet records and route records. It should not read publication Markdown.

## Scanner Title Route

VERIFIED: `Assets/_Project/Scripts/ScannableTarget.cs` calls `H8AppliedLoreRuntime.TryWriteTitleUtf16(hash, H8AppliedLoreRuntime.DefaultLocaleHash, destination, out written)` for title text. `ScannableFragment` publishes staged AppliedLore unlocks at 25/50/100 percent scan progress through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`.

Runtime role: scanner surfaces use baked packet hashes and localized title/string records. Scan completion may also publish world-impact signals for selected packet hashes.

## MessageTerminal

VERIFIED: `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs` has serialized `appliedLorePacketHash`, `appliedLoreLocaleHash`, `terminalOsPreviewIndex`, `terminalOsPreviewHash`, and `terminalOsPreviewSurface`. It resolves terminal/audio/title surfaces through `H8AppliedLoreRuntime.TryGetUtf8` and `TryWriteSurfaceUtf16`, and unlocks packets through `TryRaisePacketUnlockedAt`.

Runtime role: MessageTerminal is a diegetic terminal binding surface. Current source-only audit proves 27 terminal-policy prefabs and 27 TerminalOS preview slots, but scene placement is not complete.

## TerminalOS Preview Line

VERIFIED: `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` defines unmanaged `AppliedLoreTerminalPreviewSignal` as a 32-byte SignalBus payload with continuous low-tier/max-frame capacity fields. `TerminalOsRuntime` configures the lane, consumes frame snapshots, resolves packet UTF-8 through `H8AppliedLoreRuntime.TryGetUtf8`, and writes the preview line into terminal state.

Runtime role: TerminalOS is a visual-sync consumer. It receives packet/locale/surface hashes through SignalBus, not through scene search or parser reads.

## NarrativeDiscovery

VERIFIED: `Assets/_Project/Scripts/NarrativeDiscovery.cs` has serialized `appliedLorePacketHash`, exposes `AppliedLorePacketHash`, publishes unlocks through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`, and emits `NarrativeSpatialTriggerAuthoring` with `AppliedLoreHash` when AUP trigger data is valid.

Runtime role: NarrativeDiscovery is the main world-prop binding surface for manual placement rows. Current backlog is 347 discovery manual rows not yet fully scene-serialized.

## ScannableFragment

VERIFIED: `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs` has `appliedLoreQuarterPacketHash`, `appliedLoreHalfPacketHash`, and `appliedLoreFinalPacketHash`. It raises packet unlocks at scan progress thresholds using AUP and scan recon kind.

Runtime role: ScannableFragment supports staged scanner/codex packet unlocks without runtime authoring-file parsing.

## NarrativeSpatialTriggerAuthoring

VERIFIED: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` defines `NarrativeSpatialTriggerAuthoring` with `AppliedLoreHash` at fixed field offset. `NarrativeDiscovery.TryGetSpatialTrigger` fills that field from `appliedLorePacketHash`; `HectonNarrativeDirector_PoiTriggers.cs` consumes the trigger and can publish AppliedLore unlocks/world-impact presentation from AUP trigger data.

Runtime role: AUP proximity trigger path is an authored struct route, not a Markdown/JSON route.

## Current Binding Coverage

- Packets in source: 460.
- Binding map packet coverage: 460.
- Scene bindings: 7.
- Prefab bindings: 42.
- Total authoring bindings found by audit: 49.
- Placement plan rows: 374, with 27 terminal rows and 347 discovery rows.

Conclusion: runtime surfaces exist and are parser-free; scene placement coverage remains the production blocker.

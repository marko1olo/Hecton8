# Native Memory Cleanup Progress 2026-05-26

Generated: 2026-05-26 12:35:27 +04:00
Agent: UNKNOWN
Evidence class: STATIC_LOCAL_JSONL / STATIC_ROSLYN_LEDGER / STATUS_LOG_REVIEW

## Token Snapshot

- Current token report: `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.md`.
- Stable token ledger: `Docs/TOKEN_USAGE_LEDGER.md`.
- Total local Codex JSONL tokens: `100,190,687,073`.
- Input tokens: `99,842,361,313`.
- Cached input tokens: `95,934,146,304`.
- Output tokens: `347,808,960`.
- Reasoning output tokens: `109,975,170`.
- Sessions with usage: `2,707`; JSONL files: `2,832`.
- First-party `Assets/_Project` C#: `2509` files / `1,810,267` lines.
- Broad source snapshot: `15,421` files / `16,983,272` lines.
- `gpt-5.3-codex` standard API-equivalent estimate: `$28,497.18`.
- `gpt-5.3-codex` no-cache upper-bound estimate: `$179,593.46`.
- Pricing estimate is not billing proof.

## Full Native Ledger Delta

Baseline full ledger:

- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`.
- Timestamp: `2026-05-26T00:55:25`.
- Scanned files: `2421`; parse failures: `0`.
- Native fields: `7324`.
- Forbidden persistent candidates: `1770`.
- Forbidden MonoBehaviour candidates: `358`.
- Job-transient fields: `5490`.
- Stack-only ref-struct views: `19`.
- Raw pointer fields: `865`.

Current comparable full ledger:

- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1315_PASS14.json`.
- Timestamp: `2026-05-26T12:31:01`.
- Scanned files: `2432`; parse failures: `0`.
- Native fields: `6811`.
- Forbidden persistent candidates: `1081`.
- Forbidden MonoBehaviour candidates: `82`.
- Job-transient fields: `5501`.
- Stack-only ref-struct views: `184`.
- Raw pointer fields: `861`.

Measured delta:

- Forbidden persistent: `1770 -> 1081`, down `689`.
- Forbidden MonoBehaviour: `358 -> 82`, down `276`.
- Native fields: `7324 -> 6811`, down `513`.
- Stack-only views: `19 -> 184`, up `165`.
- Raw pointer fields: `865 -> 861`, down `4`.
- Window: `11.593h`.
- Persistent cleanup rate: `59.43/hour`.
- MonoBehaviour cleanup rate: `23.81/hour`.

Verdict: direction is real and good, but global memory ownership is not clean.

## Residual Hotspots

Top current forbidden persistent files:

- `65` - `Assets/_Project/Scripts/World/VegetationMemoryPool.cs`.
- `28` - `Assets/_Project/Scripts/ModularEquipmentEngine.cs`.
- `24` - `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`.
- `22` - `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`.
- `20` - `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs`.
- `20` - `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs`.
- `20` - `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs`.
- `19` - `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs`.
- `19` - `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs`.
- `18` - `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`.

Top current MonoBehaviour residual files:

- `13` - `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`.
- `13` - `Assets/_Project/Scripts/SaveManager.cs`.
- `12` - `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs`.
- `11` - `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs`.
- `10` - `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`.
- `9` - `Assets/_Project/Scripts/ConstructionManager.cs`.
- `9` - `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`.

## Agent Work Quality

Green or mostly green scoped work:

- `1315` Voxel: `HectonVoxelEngine.cs` target persistent fields reached `0`; pass14 removed stale hash-map ingress and cancellation throws.
- `1317` Inventory: domain/touched scope reports `0` persistent native fields.
- `1318` Destructible organics: touched scope reports `0` persistent native fields and current-source build proof existed before later edits.
- `1319` Power/logistics: Power scope reports `0` forbidden persistent fields.
- `1320` Audio: touched audio scope reports `0` forbidden persistent fields and no hidden schedule/complete residue.
- `1321` Cartography/PDA: scope reports `0` persistent fields and a full solution build succeeded with `7` warnings / `0` errors at that checkpoint.
- `1322` Fluid: `HectonFluidEngine.cs` reports `39 -> 0` persistent fields.
- `1323` Submarine atmosphere: target reports `0` persistent fields and target compile errors were cleared.
- `1324` Gas dynamics: Atmosphere scan reports `0` persistent candidates in target domain.
- `1325` Persistent world registry/AUP: touched target reports `0` native field declarations after tombstone rewrite.
- `1326` Submarine structural grid: target reports `15 -> 0` persistent fields.
- `1327` Flora interaction: touched scope reports `0` persistent fields.
- `1328` Procedural wreck generator: target reports `13 -> 0` persistent fields.
- `1329` Fabricator: scoped Fabricator/Crafting work reports `0` persistent fields.

Red or partial work:

- `1316` Vegetation memory remains red. Latest self-report says touched residuals remain `73`: `65` in `VegetationMemoryPool.cs`, `8` in `HectonMapMagicVegetationBridge.cs`.
- Global project ledger still reports `1081` forbidden persistent candidates.
- Global project ledger still reports `82` forbidden MonoBehaviour candidates.
- Raw pointer fields are almost unchanged: `865 -> 861`.
- Build proof is not current after later edits. Current guard check showed CPU `100%` and one active `dotnet`; a fresh build or fresh self-run full ledger would violate project rules.

## Current Dirty Source Surface

- Modified tracked script files: `50`.
- Untracked script entries: `12`.
- Total dirty script entries: `62`.
- This is high integration risk.

## Current Build Boundary

- Fresh build was not launched now.
- Guard result: CPU `100%`, active compiler process count `1` (`dotnet` PID `18852`).
- Current compile status is `PENDING CURRENT BUILD`.
- Last green build claims in agent logs are stale relative to later source edits.

## Conclusion

The cleanup is materially moving in the right direction.

The project is not clean. The biggest remaining problem is no longer voxel/inventory/cartography/fabricator-style scoped work; it is the residual broad ownership pool, especially vegetation, equipment, combat, ore/procedural coral, save/compression, and MonoBehaviour-held native fields.

Next rational work:

- Finish `VegetationMemoryPool.cs` and `HectonMapMagicVegetationBridge.cs` without fake wrappers.
- Attack top residual files by full-ledger order, not by `NativeArray` text count.
- Preserve job-transient fields and stack-only ref-struct views when they are phase-local and proven.
- Do not claim build green until CPU/compiler guard opens and a fresh full build runs after the current dirty source set.

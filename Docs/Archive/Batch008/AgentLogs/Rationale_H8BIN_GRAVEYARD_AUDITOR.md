# Rationale_H8BIN_GRAVEYARD_AUDITOR

Date: 2026-05-17
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM

## Decision 01 - Treat User Request As Ad-Hoc Binary Audit

Problem: No `<AGENT_PROMPT id="H8BIN_GRAVEYARD_AUDITOR">` exists in `Docs/Tasks/CURRENT_BATCH.md`, but the user issued a direct audit request for all `.h8bin` and other generated binary files.

Solution: Use `H8BIN_GRAVEYARD_AUDITOR` as the local tracking ID, keep the domain bounded to data/binary asset archaeology, and record the missing XML prompt as process evidence instead of pretending a batch prompt exists.

Rejected Alternatives: Reusing `SHINOBU_01` would contaminate the assignment with GlobalDataVault implementation tasks. Reusing every SHINOBU prompt that mentions `.h8bin` would create cross-domain scope creep and false ownership.

Scalability potential: Low tier benefits from identifying dead binary payloads that inflate import/load surfaces; middle/high/ultra tiers benefit from preserving useful LUT/static-data binaries that buy visual or simulation richness without runtime generation.

Hardware Impact: Static audit only. Runtime gain is pending; deleting or quarantining truly unused binaries can reduce disk/import scan and accidental residency pressure on i3/MX350, but no deletion is authorized in this task.

## Decision 02 - Evidence Classification First

Problem: The user asks "what is used" and "what is dead weight"; static search can prove references and absence of literal references, not Play Mode runtime load behavior.

Solution: Every file classification will be tagged as `referenced by current source`, `referenced by docs/scripts only`, `archive-only`, or `no literal reference found`. Claims above static evidence remain `PENDING VERIFICATION`.

Rejected Alternatives: Declaring runtime-dead from static absence alone. Unity Addressables, reflection-like custom loaders, or directory scans can load by convention without literal file-name hits.

Scalability potential: Low/middle/high/ultra all need deterministic data ownership; archive-only generated binaries must not silently become runtime dependencies.

Hardware Impact: Prevents false optimization reports. Estimated direct frame-time savings: 0 us until code or asset topology changes are actually made and profiled.

## Decision 03 - Split Product Payloads From Vendor/Editor Binaries

Problem: The current `Tools/VerifyBinaryHygiene.py` scans every `.bin` and `.h8bin` under the repo except broad build/cache folders. It therefore reports 65 binaries and fails on 16 misaligned files: 15 Bakery editor/plugin test chunks plus `Data/Balance/Baked/Babel_Dictionary.h8bin`. The user's target set is the Python-generated HECTON data payloads, not Bakery vendor fixtures.

Solution: Keep two inventories: product/generated payload candidates (47 rows: 46 `.bin/.h8bin` plus `GlitchTable.bytes`) and non-target editor/vendor binaries. Report both, but classify only the product/generated set in full mechanical detail.

Rejected Alternatives: Hiding the Bakery files would make verifier evidence look inconsistent. Treating Bakery fixtures as HECTON data payloads would contaminate dead-weight analysis with third-party editor assets.

Scalability potential: Low devices benefit when actual product payloads are separated from editor-only plugin data; middle/high/ultra payload choices remain visible without false deletion pressure on vendor fixtures.

Hardware Impact: Static audit. Product payload quarantine could save disk/import and possible cold-load bytes later; Bakery alignment has 0 us game-frame impact unless editor/plugin import gates are forced into production validation.

## Decision 04 - Mark Runtime Wiring Separately From Reader Existence

Problem: Several binaries have C# readers but no production bootstrap/scene/prefab path found. Example: `StaticDataStore` can read `Data/Balance/Baked/H8StaticData.bin`, but current production bootstrap also probes the absent `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; `BabelDictionaryStore` has tests but no production instantiation found.

Solution: Use separate classifications for `ACTIVE_RUNTIME_WIRED`, `EDITOR_OR_TEST_ONLY`, `READER_PRESENT_NOT_WIRED`, `SCRIPT_TOOL_ONLY`, `ARCHIVE_DUMP_ONLY`, and `THIRD_PARTY_EDITOR_BINARY`.

Rejected Alternatives: Counting a parser class as proof of live gameplay use. Counting absent literal references as proof of safe deletion.

Scalability potential: Low/middle/high/ultra tiers need the same ownership clarity; Math LOD payloads are useful only if a runtime selector is actually wired to them.

Hardware Impact: Prevents fake savings claims. Estimated direct frame-time savings remains 0 us until dead payloads are actually removed from build/import/runtime load paths.

## Decision 05 - Promote Audit Into Stable Architecture Docs

Problem: The detailed binary inventory lived in `Docs/AgentLogs` and CSV evidence. Agent logs are not stable project authority, and older stable docs still contained stale "46 aligned payloads" claims.

Solution: Create `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, link it from `Docs/ARCHITECTURE/README.md`, and add current binary-payload facts to `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`. Patch the stale co-op protocol binary-payload claim to point at the new ledger.

Rejected Alternatives: Leaving the correction only in chat or agent logs would be lost on context compression. Rewriting every domain doc would create broad doc churn outside this audit's ownership.

Scalability potential: Low tier gets a clean list of payloads that must not load accidentally. Middle/high/ultra tiers get explicit visibility into payload families that need real tier selectors before they can buy richer visuals.

Hardware Impact: Documentation-only change. Runtime impact is 0 us/frame and 0 B/frame. Future benefit is reduced accidental package/import/load surface on i3/MX350 once owners quarantine or wire payloads properly.

## Decision 06 - Do Not Patch Generated Binaries By Hand

Problem: `Data/Balance/Baked/Babel_Dictionary.h8bin` is the only misaligned product payload, but the format is generated, has header/CRC semantics, and is paired with manifests and `H8StaticData.bin`.

Solution: Mark it as `MISALIGNED_PRODUCT_FILE` and require rebake through the owning baker before runtime wiring. Do not append a padding byte manually.

Rejected Alternatives: Hand-padding the file might make byte length divisible by 16 while leaving header length, payload CRC, manifest hash, or reader expectations wrong. That would convert an obvious hygiene failure into a silent data-corruption risk.

Scalability potential: Correct rebake preserves deterministic low/middle/high/ultra data ingestion. Fake alignment would undermine all tiers.

Hardware Impact: No runtime change. Prevents a potential cold-load failure or checksum mismatch on low-end hardware where recovery paths cost more visible frame time.

## Decision 07 - Correct H8LR Lore Classification

Problem: The first audit treated `Data/Lore/Encyclopedia.h8bin` as `READER_PRESENT_NOT_WIRED` because C# lore MMF readers exist. Reinspection showed the generated blob is `H8LR`, while `LoreMmfEncyclopedia` expects an `H8LE` index plus separate payload stream.

Solution: Reclassify `Data/Lore/Encyclopedia.h8bin` in the stable ledger as `SCRIPT_TOOL_ONLY` until a dedicated H8LR reader or H8LR-to-H8LE conversion exists.

Rejected Alternatives: Pretending the existing MMF reader can consume H8LR would produce a false runtime-readiness claim. Auto-wiring `DataArchaeologyRuntime.lorePayloadPath` without an index path would fail or read the wrong format.

Scalability potential: Toaster path can stream one raw slice only after the correct reader exists. High/ultra path can prefetch adjacent slices only after the same format contract is real.

Hardware Impact: Documentation-only correction. Avoids cold-path allocation and IO attempts against an incompatible format.

## Decision 08 - Keep Water Extinction As Active Runtime Wired

Problem: Independent source cross-check found no ordinary C# callsite for `LutArrayResolver.EnsureLoadedAndBound()` and suggested downgrading `Data/Visuals/Water_Extinction_Matrix.bin`.

Solution: Keep the payload classified as `ACTIVE_RUNTIME_WIRED` because `EnsureLoadedAndBound()` is marked `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`. Unity invokes this hook before scene load; it does not require a prefab/scene caller. The ledger now states this explicitly.

Rejected Alternatives: Downgrading runtime-init attributes as "no caller" would create a false dead-weight classification. Claiming Unity runtime proof would also be false; static source proves the hook exists, not that Unity imported and executed it successfully.

Scalability potential: Base water extinction remains the current cold-loaded visual fake. Toaster and overkill variants remain script/tool-only until a real selector and hysteresis policy exist.

Hardware Impact: Documentation-only correction. Runtime impact remains 0 us/frame from this pass.

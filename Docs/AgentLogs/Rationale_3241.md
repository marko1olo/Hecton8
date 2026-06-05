# Rationale 3241

Decision: write P490 as a source-custody receipt, not a developer checklist.

Reason:
- Task requires player-facing/in-world technical lore.
- writing.md requires artifacts with speaker, evidence object, unlock context, source boundary, and all 15 project locales.
- authoring.md and data.md require a hard distinction between source material, binary output, runtime DTO layout, and proof artifacts.
- Lore_Content_System.md and Lore_Localization_Model.md require stable Article ID, Loc namespace, delivery surfaces, and honest localization state.

Mandates followed:
- QA_Evidence_Text_Filter_Audit.txt: static text proves text presence only; runtime claims downgraded.
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt: LocIDs remain stable; RTL/CJK rows are drafts and need later layout/font proof.
- DATA_Runtime_Struct_Layout_ARM64.txt: no runtime DTO introduced; packet names future data fields only as source concepts.
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt: source admission is separated from binary output and bake proof.
- STRM_ModuleDTO_LZ4_Dictionary.txt: no compression or h8bin claim made.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: no runtime or hot-path code changed.

Quality/scalability consequence:
- Low/Compact: shortest scanner/field-warning presentation only.
- Middle: scanner plus terminal/codex chain.
- High: richer annotation and material treatment.
- Ultra: optional archive crosslinks and notary-strip presentation.
- Truth unchanged across all labels: Article ID, LocIDs, spoiler byte meaning, claimant-independent proof boundary, and source/binary separation.

Risk:
- Non-English rows are draft text only and need native review plus RTL/CJK/font/layout proof before any player/public lock.
- No Unity, bake, h8bin, importer, runtime, or native review proof exists from this worker.

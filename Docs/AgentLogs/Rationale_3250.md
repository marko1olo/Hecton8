# Rationale 3250

Task: create P497 evidence relation graph dossier packet.

Scope decision:

- Kept work to four files named by the task.
- Treated the graph as an evidence reading surface, not data ownership.
- Wrote copy that connects packet custody, source object, claimant language, witness hash, route consequence, and redaction/caption status.
- Repeated the required warning in player-facing terms: graph edges are review aids, not verdicts.

Mandates followed:

- QA_Evidence_Text_Filter_Audit.txt: static text evidence is not runtime, import, profiler, or public proof.
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt: all 15 locales present; RTL/CJK kept as draft text requiring later layout/font proof outside this task.
- DATA_Runtime_Struct_Layout_ARM64.txt: no DTO, SignalBus, NativeArray, telemetry, save, GPU, or Burst layout changed.
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt: no CSV, binary, route-card, generated page, h8bin, or source bridge edited.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: no runtime code or hot path changed; packet text is authoring-only.

Non-trivial decisions:

- Used source-object and caption/redaction language to prevent the relation graph from becoming a hidden verdict system.
- Kept witness hash as tamper/custody evidence only; did not let it certify truth.
- Used GlobalQualityWeight only for presentation density; Article ID, LocIDs, truth, and route meaning stay unchanged.
- Wrote locale rows as draft content, not placeholders or final-language claims.

Residual risk:

- Non-English rows are machine/LLM draft quality and require later human language review plus surface layout proof.
- No Unity scene, font atlas, string pool, generated page, or import state was checked because the task forbids those actions.

# P514 PDA Next Proof Checklist Bridge

## Header Metadata

Packet ID: P514_PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Article ID: PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Loc namespace: LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Canonical title: PDA Next Proof Checklist

Evidence class: STATIC_DOC only

Runtime layer: Narrative authoring source

Spoiler level: 1 public next-proof prompt / 3 route proof held / 4 final consequence held

Canon owner: Canon_Locks.md, Lore_Bible.md, Lore_Content_System.md, Lore_Localization_Model.md, Website_Publication_Map.md

Surface targets: future PDA checklist, future scanner prompt, future terminal note, future wiki procedure note, future public archive article, future evidence caption

Connected packets: P511_PDA_EVIDENCE_FAMILY_REVIEW_PROMPT_BRIDGE, P512_PUBLIC_ARCHIVE_DISPUTE_REASON_CODE_BRIDGE, P513_ARCHIVE_RESOLUTION_HOLD_PROMPT_BRIDGE, P510_SCANNER_CONFIDENCE_DOWNGRADE_REASON_BRIDGE

First-20 route boundary: lets the opening route give the player a next-proof action without revealing the final evidence conclusion.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

Proof boundary: this packet is markdown authoring text. It creates no website page, wiki page, route card, graph, binding map, source CSV row, generated asset, Unity object, runtime script, importer output, h8bin payload, DataMonolith payload, public deployment, native localization review, or acceptance state.

## Source Brief

Packet ID: P514_PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Article ID: PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Loc namespace: LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE

Runtime layer: Narrative

Surface targets: PDA checklist, scanner prompt, terminal note, wiki procedure note, public archive article, evidence caption

Spoiler level: next-proof prompt visible at level 1; proof result and final consequence held until later gates.

Canon sources: AGENTS.md, VISION_LOCKS.md, TASTE.md, writing.md, narrative.md, localization.md, data.md, authoring.md, quality.md, Docs/Lore/Canon_Locks.md, Docs/Lore/Lore_Bible.md, Docs/Lore/Lore_Content_System.md, Docs/Lore/Lore_Localization_Model.md, Docs/Lore/Website_Publication_Map.md.

Speaker/source: PDA evidence review interface, scanner confidence interpreter, public archive procedure editor, terminal receipt reviewer.

Audience: player using the PDA, public/wiki readers, future localization reviewer, future source-table owner.

Date/era: player-facing HECTON-8 salvage route, reconstructed for public archive explanation in 2190.

Location/depth/route: PDA evidence family panel, scanner result, terminal receipt screen, public archive procedure note.

Unlock context: the player has a disputed evidence family and needs a short checklist of the next proof target instead of a premature conclusion.

Evidence object: PDA checklist row with reason code, missing proof class, target surface, and unresolved conclusion flag.

What this source knows: the PDA can ask for the next proof object. It cannot decide the final truth from a conflict label alone.

What this source does not know: final receiver, final legal result, protected claimant, Atlas consequence, rescue state, native review status, source import state, runtime placement, or publication state.

What this source hides or gets wrong: checklist language can sound authoritative even when it only reflects current evidence family state. The prompt must stay action-focused.

Player use: gives the player the next concrete evidence action without converting partial data into conclusion text.

Forbidden facts: no final receiver, no named protected claimant, no final-route branch, no Atlas consequence, no legal verdict, no rescue promise, no native review, no source insertion, no h8bin state, no DataMonolith state, no public deployment state.

Required proper nouns/terms: HECTON-8, Deep Reach, Atlas, Marauder, PDA, scanner, public archive, dispute reason, next proof, witness hash, custody route.

LocIDs: proposed in Future Integration Notes.

Localization status: English authority row; non-English draft rows require future native review and layout checks.

## Surface Texts

**Website article seed:**

The PDA next-proof checklist exists because evidence review should change the player's next action, not hand them an answer. When a HECTON-8 evidence family is disputed, the checklist names one missing proof target at a time: scan the object mark, compare custody route, find the second witness hash, open the receipt body, test the route alias, or read the claimant-safe line against the older packet field.

The checklist is deliberately plain. It does not tell the player what the proof will mean. It tells the player what would make the next archive sentence less dishonest. That is the difference between a useful evidence interface and a spoiler machine.

**Wiki article seed:**

PDA next-proof checklist: a player-facing prompt that converts dispute reason and resolution hold state into a concrete evidence action. Checklist entries must name the next proof target and keep conclusion fields locked until recovered proof updates the evidence family.

**PDA / codex entry:**

Checklist: choose the next proof, not the cleanest label. Scan object mark. Compare custody route. Find second witness hash. Open receipt body. Test route alias. Review claimant-safe line. If one target fails, keep the family unresolved and follow the next proof.

**Scanner entry:**

Next-proof target available. Evidence family unresolved. Required action: scan object mark, custody route, witness hash, receipt body, route alias, or claimant-safe line.

**Terminal note:**

PDA NEXT PROOF CHECKLIST

Conclusion locked.

Reason code present.

Choose one proof target.

Do not promote current label to final text.

**Evidence caption:**

PDA checklist row. The system asks for the next proof target and keeps the evidence family unresolved.

**Spoiler policy:**

Next-proof checklist language may appear early. Proof result, final receiver, protected claimant, Atlas consequence, legal result, and ending branch stay masked until later proof gates.

**String-pool key plan:**

Use hashed LocIDs in the Narrative layer. Keep checklist rows short, action-focused, and stable. Runtime must not parse this Markdown.

## Future Integration Notes

Proposed LocID rows:

| LocID | Layer | Category | Purpose |
|---|---|---|---|
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_TITLE | Narrative | codex_title | Canonical title string |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_WEBSITE | Narrative | website_article | Public article seed |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_WIKI | Narrative | wiki_article | Wiki note |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_PDA | Narrative | pda_codex | PDA checklist |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_SCANNER | Narrative | scanner_entry | Scanner prompt |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_TERMINAL | Narrative | terminal_note | Terminal note |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_CAPTION | Narrative | evidence_caption | Evidence caption |
| LORE_EVIDENCE_PDA_NEXT_PROOF_CHECKLIST_BRIDGE_SPOILER | Narrative | spoiler_policy | Spoiler policy |

P511 relation: evidence-family review prompts group the disputed rows; this packet gives the short checklist action.

P512 relation: dispute reason codes select the proof target family.

P513 relation: resolution holds keep the conclusion locked until the checklist target is recovered.

P510 relation: scanner confidence downgrades can feed the PDA checklist.

Runtime boundary: future runtime systems must consume baked string-pool rows or binary source data only, never this Markdown.

## Locale Rows

### en_US
Status: source_authority
Text: The PDA next-proof checklist turns a dispute reason into one action. Scan object mark, compare custody route, find second witness hash, open receipt body, test route alias, or review claimant-safe line. The checklist does not decide the conclusion. It keeps the evidence family unresolved until the named proof target is recovered.

### ar_SA
Status: draft_machine_or_llm
Text: تحول قائمة الدليل التالي في PDA سبب النزاع الى فعل واحد. امسح علامة الجسم، قارن مسار الحيازة، ابحث عن بصمة الشاهد الثانية، افتح متن الايصال، اختبر اسم المسار، او راجع سطر حماية المطالب. القائمة لا تقرر الخلاصة. تبقي عائلة الدليل غير محلولة حتى يستعاد هدف الدليل المسمى.

### de_DE
Status: draft_machine_or_llm
Text: Die PDA-Naechster-Beweis-Liste macht aus einem Streitgrund eine Handlung. Objektmarke scannen, Verwahrungsroute vergleichen, zweiten Zeugenhash finden, Belegkoerper oeffnen, Routenalias testen oder claimant-safe Zeile pruefen. Die Liste entscheidet den Schluss nicht. Sie haelt die Beweisfamilie offen, bis das genannte Ziel wieder da ist.

### es_ES
Status: draft_machine_or_llm
Text: La lista de siguiente prueba del PDA convierte una razon de disputa en una accion. Escanea marca de objeto, compara ruta de custodia, busca segundo hash testigo, abre cuerpo de recibo, prueba alias de ruta o revisa linea segura para reclamante. La lista no decide la conclusion. Mantiene la familia sin resolver hasta recuperar la prueba nombrada.

### fr_FR
Status: draft_machine_or_llm
Text: La checklist PDA de preuve suivante transforme une raison de litige en une action. Scanner marque d'objet, comparer route de garde, trouver second hash temoin, ouvrir corps du recu, tester alias de route ou revoir ligne protegee. La liste ne decide pas la conclusion. Elle garde la famille ouverte jusqu'a la preuve nommee.

### he_IL
Status: draft_machine_or_llm
Text: רשימת ההוכחה הבאה ב-PDA הופכת סיבת מחלוקת לפעולה אחת. סרקו סימן חפץ, השוו מסלול משמורת, מצאו גיבוב עד שני, פתחו גוף קבלה, בדקו כינוי מסלול או עיינו בשורה מוגנת. הרשימה לא קובעת מסקנה. היא משאירה את משפחת הראיות לא פתורה עד שמטרת ההוכחה תימצא.

### id_ID
Status: draft_machine_or_llm
Text: Checklist bukti berikutnya PDA mengubah alasan sengketa menjadi satu aksi. Pindai tanda objek, bandingkan rute kustodi, cari hash saksi kedua, buka isi tanda terima, uji alias rute, atau tinjau baris aman-klaiman. Checklist tidak memutuskan kesimpulan. Ia menjaga keluarga bukti tetap belum selesai sampai target bukti ditemukan.

### ja_JP
Status: draft_machine_or_llm
Text: PDA次証拠チェックリストは、異議理由を一つの行動に変える。物体印をスキャンし、保管ルートを比較し、二つ目の証人ハッシュを探し、受領票本文を開き、ルート別名を試し、請求者保護行を確認する。リストは結論を決めない。名指しの証拠目標が回収されるまで証拠ファミリーを未解決に保つ。

### ko_KR
Status: draft_machine_or_llm
Text: PDA 다음 증거 체크리스트는 분쟁 사유를 하나의 행동으로 바꾼다. 물체 표식 스캔, 보관 경로 비교, 두 번째 증인 해시 찾기, 영수증 본문 열기, 경로 별칭 시험, 청구인 보호 줄 검토 중 하나다. 체크리스트는 결론을 정하지 않는다. 이름 붙은 증거 목표가 회수될 때까지 증거 가족을 미해결로 둔다.

### nl_NL
Status: draft_machine_or_llm
Text: De PDA-checklist voor volgend bewijs zet een geschilreden om in een actie. Scan objectmerk, vergelijk bewaringsroute, vind tweede getuigehash, open bonbody, test routealias of bekijk claimant-safe regel. De checklist beslist de conclusie niet. Hij houdt de bewijsfamilie onopgelost tot het genoemde bewijsdoel terug is.

### pl_PL
Status: draft_machine_or_llm
Text: Lista nastepnego dowodu w PDA zmienia powod sporu w jedno dzialanie. Skanuj znak obiektu, porownaj trase depozytu, znajdz drugi hash swiadka, otworz tresc paragonu, sprawdz alias trasy albo przejrzyj bezpieczna linie roszczacego. Lista nie decyduje wniosku. Trzyma rodzine dowodow otwarta do odzyskania celu.

### pt_BR
Status: draft_machine_or_llm
Text: A checklist de proxima prova do PDA transforma um motivo de disputa em uma acao. Escaneie marca do objeto, compare rota de custodia, encontre segundo hash de testemunha, abra corpo do recibo, teste alias de rota ou revise linha segura de reclamante. A lista nao decide a conclusao. Mantem a familia sem resolver ate recuperar a prova.

### ru_RU
Status: draft_machine_or_llm
Text: Контрольный список следующего доказательства в PDA превращает причину спора в одно действие. Сканируй метку объекта, сравни маршрут хранения, найди второй хэш свидетеля, открой тело квитанции, проверь псевдоним маршрута или строку заявителя. Список не решает вывод. Он держит семью доказательств открытой до получения цели.

### uk_UA
Status: draft_machine_or_llm
Text: Контрольний список наступного доказу в PDA перетворює причину спору на одну дію. Скануй мітку об'єкта, порівняй маршрут зберігання, знайди другий хеш свідка, відкрий тіло квитанції, перевір псевдонім маршруту або рядок заявника. Список не вирішує висновок. Він тримає родину доказів відкритою до отримання цілі.

### zh_CN
Status: draft_machine_or_llm
Text: PDA下一证据清单把争议原因转成一个行动。扫描物件标记，对比保管路线，寻找第二见证哈希，打开收据正文，测试路线别名，或审查索赔人安全行。清单不决定结论。它会让证据家族保持未解决，直到指定证据目标被找回。

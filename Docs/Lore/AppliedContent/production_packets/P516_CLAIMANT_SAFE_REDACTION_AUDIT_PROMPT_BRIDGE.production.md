# P516 Claimant-Safe Redaction Audit Prompt Bridge

## Header Metadata

Packet ID: P516_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Article ID: CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Loc namespace: LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Canonical title: Claimant-Safe Redaction Audit Prompt

Evidence class: STATIC_DOC only

Runtime layer: Narrative authoring source

Spoiler level: 1 public redaction audit prompt / 3 proof target held / 4 final consequence held

Canon owner: Canon_Locks.md, Lore_Bible.md, Lore_Content_System.md, Lore_Localization_Model.md, Website_Publication_Map.md

Surface targets: future public archive article, future wiki procedure note, future PDA audit prompt, future scanner redaction tag, future terminal audit note, future evidence caption

Connected packets: P512_PUBLIC_ARCHIVE_DISPUTE_REASON_CODE_BRIDGE, P513_ARCHIVE_RESOLUTION_HOLD_PROMPT_BRIDGE, P514_PDA_NEXT_PROOF_CHECKLIST_BRIDGE, P511_PDA_EVIDENCE_FAMILY_REVIEW_PROMPT_BRIDGE

First-20 route boundary: lets the opening route show that claimant protection can be audited without exposing the claimant or allowing an office to bury its own action under protection language.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

Proof boundary: this packet is markdown authoring text. It creates no website page, wiki page, route card, graph, binding map, source CSV row, generated page, Unity object, runtime script, importer output, h8bin payload, DataMonolith payload, public deployment, native localization review, or acceptance state.

## Source Brief

Packet ID: P516_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Article ID: CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Loc namespace: LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE

Runtime layer: Narrative

Surface targets: public archive article, wiki procedure note, PDA audit prompt, scanner redaction tag, terminal audit note, evidence caption

Spoiler level: audit prompt visible at level 1; protected identity, proof result, route branch, and final consequence held until later gates.

Canon sources: AGENTS.md, VISION_LOCKS.md, TASTE.md, writing.md, narrative.md, localization.md, data.md, authoring.md, quality.md, Docs/Lore/Canon_Locks.md, Docs/Lore/Lore_Bible.md, Docs/Lore/Lore_Content_System.md, Docs/Lore/Lore_Localization_Model.md, Docs/Lore/Website_Publication_Map.md.

Speaker/source: public archive redaction auditor, PDA evidence reviewer, scanner confidence interpreter, terminal receipt reviewer.

Audience: public readers, wiki readers, player reviewing redacted evidence, future localization reviewer, future source-table owner.

Date/era: 2190 archive reconstruction window after claimant-safe packet families became visible outside internal Deep Reach review.

Location/depth/route: public archive redaction page, PDA evidence family panel, scanner redaction label, terminal audit stamp.

Unlock context: the reader sees a claimant-safe redaction and needs to know whether the redaction protects a person, hides an office action, or does both.

Evidence object: redaction-audit card with redaction reason, protected-field class, non-identifying proof target, office-trace field, custody field, and unresolved conclusion flag.

What this source knows: claimant identity can stay sealed while the reason, custody field, office trace, and next proof target remain auditable.

What this source does not know: final receiver, protected claimant identity, Atlas consequence, legal result, route branch, source admission, rescue state, native review state, source import state, runtime placement, or publication state.

What this source hides or gets wrong: public archive language can make protection sound complete. Marauder reviewers treat a redaction as suspect when the same black bar hides both a claimant and the office action taken against the file.

Player use: tells the player to test what the redaction protects and what it conveniently removes from view before trusting the clean label.

Forbidden facts: no final receiver, no named protected claimant, no Atlas consequence, no legal verdict, no route branch, no source admission, no rescue promise, no native review, no source insertion, no h8bin state, no DataMonolith state, no public deployment state.

Required proper nouns/terms: HECTON-8, Deep Reach, Atlas, Marauder, public archive, claimant-safe redaction, redaction audit, office trace, custody field, witness hash, receipt body.

LocIDs: proposed in Future Integration Notes.

Localization state: English authority row; non-English draft rows require future native review and layout checks.

## Surface Texts

**Website article seed:**

A claimant-safe redaction is supposed to remove a person from harm, not remove an office from review. HECTON-8 archive packets audit that difference by splitting the black bar into fields: protected claimant identity, redaction reason, office trace, custody field, and the next proof target. The public reader does not get the name. The reader still gets the shape of the audit.

The first test is simple: would the exposed field identify the claimant? If yes, it stays sealed. The second test is harder: did the same seal hide who changed the receipt, moved the custody line, or downgraded the witness hash? If yes, the office trace must be kept visible in non-identifying form. Protection cannot be used as a clean drawer for liability.

**Wiki article seed:**

Claimant-safe redaction audit: an archive procedure that checks whether a redaction protects claimant identity while preserving non-identifying evidence review. The audit should list redaction reason, protected-field class, office-trace field, custody field, current confidence state, and next-proof target. It must not reveal final receiver, protected claimant, Atlas consequence, legal result, route branch, or source admission.

**PDA / codex entry:**

PDA prompt: redaction present. Do not read the black bar as proof by itself. Check what it protects and what it hides. If claimant identity is the protected field, keep it sealed. If office trace, custody route, receipt body, or witness hash vanished under the same mark, split the audit and chase the next proof target.

**Scanner entry:**

Claimant-safe redaction detected. Identity field sealed. Audit required: redaction reason, office trace, custody field, witness hash, receipt body, or next proof target.

**Terminal note:**

CLAIMANT-SAFE REDACTION AUDIT

Protect claimant identity.

Do not protect office action.

Record redaction reason, protected-field class, custody field, and non-identifying proof target.

Conclusion remains locked.

**Evidence caption:**

Redaction-audit card. The archive keeps the claimant sealed and leaves the office trace testable.

**Spoiler policy:**

Claimant-safe audit language may appear early. Final receiver, protected claimant, Atlas consequence, legal result, route branch, source admission, proof result, and ending consequence stay masked until later proof gates.

**String-pool key plan:**

Use hashed LocIDs in the Narrative layer. Keep scanner and PDA prompts compact. Keep article/wiki text tied to this packet ID. Runtime systems must consume baked rows or binary source data only, never this Markdown.

## Future Integration Notes

Proposed LocID rows:

| LocID | Layer | Category | Purpose |
|---|---|---|---|
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_TITLE | Narrative | codex_title | Canonical title string |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_WEBSITE | Narrative | website_article | Public article seed |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_WIKI | Narrative | wiki_article | Wiki note |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_PDA | Narrative | pda_codex | PDA audit prompt |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_SCANNER | Narrative | scanner_entry | Scanner redaction tag |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_TERMINAL | Narrative | terminal_note | Terminal audit note |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_CAPTION | Narrative | evidence_caption | Evidence caption |
| LORE_EVIDENCE_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE_SPOILER | Narrative | spoiler_policy | Spoiler policy |

P512 relation: claimant-safe omission can be a dispute reason when redaction hides a proof target.

P513 relation: a resolution hold remains active when the audit names the missing non-identifying proof class.

P514 relation: the PDA checklist can turn the audit into the next concrete evidence action.

P511 relation: evidence-family review groups redaction audit rows beside dispute and hold rows without exposing protected identity.

Runtime boundary: future runtime systems must consume baked string-pool rows or binary source data only, never this Markdown.

## Locale Rows

### en_US
Status: source_authority
Text: The claimant-safe redaction audit asks what is being protected and what is being hidden by protection language. The claimant's identity stays sealed. The office action does not. Audit rows keep the redaction reason, protected-field class, office trace, custody field, and next proof target. If one mark hides both a claimant and liability, split the field: claimant withheld, office trace still reviewable.

### ar_SA
Status: draft_machine_or_llm
Text: يطلب تدقيق الحجب الآمن للمطالب معرفة ما تتم حمايته وما يتم إخفاؤه بلغة الحماية. تبقى هوية المطالب مختومة. أما فعل المكتب فلا. تحتفظ صفوف التدقيق بسبب الحجب، وفئة الحقل المحمي، وأثر المكتب، وحقل الحيازة، وهدف الإثبات التالي. إذا أخفت علامة واحدة المطالب والمسؤولية معا، فاقسم الحقل: يبقى المطالب محجوبا ويبقى أثر المكتب قابلا للمراجعة.

### de_DE
Status: draft_machine_or_llm
Text: Das claimant-safe Redaktionsaudit fragt, was geschützt wird und was durch Schutzsprache verborgen wird. Die Identität des Claimants bleibt versiegelt. Die Bürohandlung nicht. Audit-Zeilen behalten Redaktionsgrund, geschützte Feldklasse, Bürospur, Verwahrungsfeld und nächstes Beweisziel. Wenn eine Markierung Claimant und Haftung zugleich versteckt, wird das Feld getrennt: Claimant zurückgehalten, Bürospur weiter prüfbar.

### es_ES
Status: draft_machine_or_llm
Text: La auditoría de redacción segura para reclamante pregunta qué se protege y qué se oculta con lenguaje de protección. La identidad del reclamante queda sellada. La acción de la oficina no. Las filas de auditoría conservan motivo de redacción, clase de campo protegido, rastro de oficina, campo de custodia y siguiente prueba. Si una marca oculta a la vez reclamante y responsabilidad, se divide el campo: reclamante retenido, rastro de oficina revisable.

### fr_FR
Status: draft_machine_or_llm
Text: L'audit de caviardage sûr pour le demandeur demande ce qui est protégé et ce que le langage de protection cache. L'identité du demandeur reste scellée. L'action du bureau non. Les lignes d'audit gardent raison du caviardage, classe de champ protégé, trace du bureau, champ de garde et prochaine preuve. Si une marque cache à la fois demandeur et responsabilité, on sépare le champ: demandeur retenu, trace du bureau encore vérifiable.

### he_IL
Status: draft_machine_or_llm
Text: ביקורת השחרה בטוחה לתובע שואלת מה מוגן ומה מוסתר באמצעות שפת הגנה. זהות התובע נשארת חתומה. פעולת המשרד לא. שורות הביקורת שומרות את סיבת ההשחרה, סוג השדה המוגן, עקבת המשרד, שדה המשמורת ויעד ההוכחה הבא. אם סימן אחד מסתיר גם תובע וגם אחריות, מפצלים את השדה: התובע מוחזק חסוי, עקבת המשרד עדיין ניתנת לבדיקה.

### id_ID
Status: draft_machine_or_llm
Text: Audit redaksi aman-klaiman menanyakan apa yang dilindungi dan apa yang disembunyikan oleh bahasa perlindungan. Identitas klaiman tetap disegel. Tindakan kantor tidak. Baris audit menyimpan alasan redaksi, kelas bidang terlindungi, jejak kantor, bidang kustodi, dan target bukti berikutnya. Jika satu tanda menyembunyikan klaiman dan liabilitas sekaligus, pisahkan bidang: klaiman ditahan, jejak kantor tetap dapat ditinjau.

### ja_JP
Status: draft_machine_or_llm
Text: 請求者保護型の黒塗り監査は、何を保護しているのか、保護文言で何を隠しているのかを問う。請求者の身元は封印されたままにする。事務所の行為は封印しない。監査行は黒塗り理由、保護フィールド種別、事務所の痕跡、保管フィールド、次の証拠目標を残す。一つの印が請求者と責任の両方を隠すなら、フィールドを分ける。請求者は伏せ、事務所の痕跡は検証可能に残す。

### ko_KR
Status: draft_machine_or_llm
Text: 청구인 안전 편집 감사는 무엇을 보호하고 보호 언어가 무엇을 숨기는지 묻는다. 청구인의 신원은 봉인된 채로 둔다. 사무실의 행위는 그렇지 않다. 감사 행은 편집 사유, 보호 필드 종류, 사무실 흔적, 보관 필드, 다음 증거 목표를 보존한다. 하나의 표시가 청구인과 책임을 함께 숨기면 필드를 나눈다. 청구인은 보류하고 사무실 흔적은 계속 검토 가능하게 둔다.

### nl_NL
Status: draft_machine_or_llm
Text: De claimant-safe redactie-audit vraagt wat wordt beschermd en wat door beschermingstaal wordt verborgen. De identiteit van de claimant blijft verzegeld. De kantoorhandeling niet. Auditregels bewaren redactiereden, beschermde veldklasse, kantoorspoor, bewaringsveld en volgend bewijsdoel. Als één markering claimant en aansprakelijkheid tegelijk verbergt, splits het veld: claimant achtergehouden, kantoorspoor nog controleerbaar.

### pl_PL
Status: draft_machine_or_llm
Text: Audyt redakcji bezpiecznej dla roszczącego pyta, co jest chronione i co ukrywa język ochrony. Tożsamość roszczącego pozostaje zapieczętowana. Działanie biura nie. Wiersze audytu zachowują powód redakcji, klasę pola chronionego, ślad biura, pole depozytu i następny cel dowodowy. Jeśli jeden znak ukrywa roszczącego i odpowiedzialność, podziel pole: roszczący ukryty, ślad biura nadal do przeglądu.

### pt_BR
Status: draft_machine_or_llm
Text: A auditoria de redação segura para reclamante pergunta o que está sendo protegido e o que a linguagem de proteção esconde. A identidade do reclamante fica selada. A ação do escritório não. As linhas de auditoria mantêm motivo da redação, classe de campo protegido, rastro do escritório, campo de custódia e próxima prova. Se uma marca oculta reclamante e responsabilidade, divida o campo: reclamante retido, rastro do escritório revisável.

### ru_RU
Status: draft_machine_or_llm
Text: Аудит безопасного для заявителя сокрытия спрашивает, что защищают и что прячут языком защиты. Личность заявителя остается закрытой. Действие офиса - нет. Строки аудита сохраняют причину сокрытия, класс защищенного поля, след офиса, поле хранения и следующую цель доказательства. Если одна отметка скрывает и заявителя, и ответственность, поле делят: заявитель удержан, след офиса остается проверяемым.

### uk_UA
Status: draft_machine_or_llm
Text: Аудит безпечного для заявника приховування питає, що захищають і що ховають мовою захисту. Особа заявника лишається запечатаною. Дія офісу - ні. Рядки аудиту зберігають причину приховування, клас захищеного поля, слід офісу, поле зберігання і наступну ціль доказу. Якщо одна позначка ховає і заявника, і відповідальність, поле ділять: заявника утримано, слід офісу лишається перевірним.

### zh_CN
Status: draft_machine_or_llm
Text: 索赔人安全遮蔽审计会询问：保护的是什么，保护语言又隐藏了什么。索赔人的身份保持封存。办公室行为不能封存。审计行保留遮蔽原因、受保护字段类别、办公室痕迹、保管字段和下一项证据目标。如果一个标记同时隐藏索赔人和责任，就拆分字段：索赔人保留不公开，办公室痕迹仍可审查。

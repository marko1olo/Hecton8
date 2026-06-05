# P489_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE

Evidence class: STATIC_DOC
Packet ID: P489_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Article ID: IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Loc namespace: LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Runtime layer: Narrative
Canonical title: In-Game Evidence Queue Routing Bridge
Canon owner: Docs/Lore/Canon_Locks.md and Docs/Lore/Lore_Bible.md
Source voices: scanner instrument, Deep Reach terminal routing, player codex, Marauder annotation
Content targets: scanner queue, terminal queue, codex queue, Marauder annotation
Spoiler level: 3, with partial-return debrief language held for consequence records
First unlock route: after the player recovers custody-linked evidence from a relay, quarantine hold, loss ledger, payout claim, or return-action fragment
Write scope: lore/system-integration authoring only. No UI, runtime logic, binding map, source CSV, route card, generated page, h8bin, Unity asset, or importer/exporter change.

## Source Brief

Packet ID: P489_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Article ID: IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Loc namespace: LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE
Runtime layer: Narrative
Surface targets: scanner queue, terminal queue, codex queue, Marauder annotation
Canon sources: AGENTS.md; VISION_LOCKS.md; TASTE.md; writing.md; narrative.md; localization.md; data.md; authoring.md; quality.md; Docs/Lore/Canon_Locks.md; Docs/Lore/Lore_Bible.md; Docs/Lore/Lore_Content_System.md; Docs/Lore/Lore_Localization_Model.md; Docs/Lore/Codex_Delivery_Map.md
Speaker/source: queue language assembled from instrument output, corporate routing text, codex summary, and Marauder correction
Audience: player as debt-bound Marauder and ex-Deep-Reach procedure reader
Date/era: 2190, after HECTON-8 recovery contract contact
Location/depth/route: any evidence-bearing route after custody proof, notary hash, quarantine hold, payout claim, or partial-return record enters the player dossier
Unlock context: the player has at least one physical or packet artifact and needs to decide which evidence route matters next
Evidence object: recovered packet seal, witness hash, quarantine routing hold, Keelmark payout conversion line, or partial-return debrief record
What this source knows: current queue order, why the next item changes custody, payout, quarantine, public ledger exposure, or return leverage
What this source does not know: final receiver, final public effect, rescue timing, native review state, runtime binding state, and whether any authority will honor the evidence
What this source hides or gets wrong: corporate routing text treats people and proof as payload classes; scanner confidence cannot judge motive
Player use: decide whether to chase custody proof, preserve a witness hash, contest quarantine, take a payout, or keep a partial-return record open
Forbidden facts: no clean rescue promise, no final moral answer, no omniscient truth before physical evidence, no native/public/runtime proof claim
Required proper nouns/units: HECTON-8, Black Keel, Deep Reach, Recovery Compliance Office, Keelmark Mutual, Aegir Reclamation Pool, Tau Public Ledger Lane, Luyten Packet Ladder

## Surface Texts

### Scanner Queue

Queue item flagged: custody route incomplete.

Recovered packet carries a partial witness hash and a damaged relay stamp. Next useful action is not collection. Preserve the seal, compare the hash against another physical source, and keep the item out of Black Keel payout sorting until the notary chain is less exposed.

Decision value: a matched witness hash can move the item from salvage value to evidence. A broken chain lets Keelmark Mutual price it as material.

### Terminal Queue

RETURN ACTION QUEUE / LOCAL CACHE

Priority is assigned by control risk, not by discovery time.

1. Custody proof: confirm packet seal and source object before duplicate upload.
2. Notary hash: route through Luyten Packet Ladder when relay geometry opens; do not let claimant-side compression rewrite the witness block.
3. Quarantine hold: check whether the hold protects a living hazard, a legal delay, or a payload bargain.
4. Payout claim: identify whether Keelmark converts the record into mass, sample value, or unresolved worker load.
5. Partial-return debrief: keep the record attached to the route that produced it. A return note without evidence state is only a lien extension.

No item in this queue authorizes extraction by itself. Receiver, window, and evidence custody remain unresolved until the next route proof exists.

### Codex Queue

Evidence does not become useful because the player has found it. It becomes useful when the route can prove where it came from, who handled it, what changed, and who benefits if it disappears.

The queue ranks records by immediate decision pressure. Custody proof protects origin. Notary hashes protect the witness chain from claimant edits. Quarantine holds explain why a route or body was delayed. Payout claims show when Keelmark Mutual is converting people, samples, or silence into recoverable mass. Partial-return debriefs keep a failed or compromised exit from becoming a clean ending in the dossier.

The order can change when new physical evidence contradicts a corporate line. That is the point. A useful queue tells the player why the next record matters and what is still unknown.

### Marauder Annotation

Do not sort this like a library.

If the next card says "payout," ask what got priced. If it says "quarantine," ask who waited outside the door. If it says "notary," keep the original seal wet and ugly. Clean copies are how Deep Reach wins arguments it cannot win in the room.

Partial return is not freedom. It is a debrief with a debt hook unless the evidence rides out with its teeth still in it.

## Future Integration Notes

Proposed LocIDs:

- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_TITLE
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_SCANNER_QUEUE
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_SCANNER_DECISION_VALUE
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_TERMINAL_HEADER
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_TERMINAL_BODY
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_CODEX_BODY
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_MARAUDER_NOTE
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_DENSITY_LOW_COMPACT
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_DENSITY_MIDDLE
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_DENSITY_HIGH
- LORE_EVIDENCE_IN_GAME_EVIDENCE_QUEUE_ROUTING_BRIDGE_DENSITY_ULTRA

Integration boundary:

- Authoring packet only. Future runtime should consume stable LocIDs, baked hashes, unlock flags, and queue-state records owned by gameplay/data systems.
- The visible text must never decide custody, payout, quarantine, return, save identity, or mission truth. It describes owner-issued evidence state.
- Scanner copy should stay short enough for pressure use. Terminal copy can carry procedural order. Codex copy can explain why the queue changed. Marauder text can correct corporate wording without becoming omniscient.
- Future source extraction must preserve Article ID, Loc namespace, spoiler level, source voice, and the difference between evidence custody and material value.
- No future import can use this markdown as proof of Unity placement, player-visible delivery, h8bin/Data Monolith state, native localization review, public publication, or profiler/GC behavior.

## GlobalQualityWeight Presentation Density

Low/Compact: show only the next queued evidence item, one reason it matters, one risk label, and one action limit. Use short scanner and PDA lines. Keep custody/notary/quarantine/payout/partial-return tags readable without optional archive fragments.

Middle: show the next two queued items, last physical source, current evidence gap, and a short codex explanation. Add one crosslink to the related packet family when the player has already seen that family.

High: add terminal routing detail, contradiction hints, and optional Marauder correction when the evidence source supports it. Add richer presentation around the same LocIDs and evidence truth.

Ultra: add archive-side context, secondary contradictions, and optional dossier commentary for players who want the full pressure chain. Do not change Article ID, LocID, canon fact, source voice, unlock truth, receiver, payout meaning, quarantine state, save identity, or final authority route.

## Localization

### en_US

Status: source_authority
Text: The evidence queue ranks what needs proof next: custody seal, notary hash, quarantine hold, payout claim, or partial-return debrief. The next item matters because it changes who can price the record, bury it, publish it, or use it against the player. No queue entry promises extraction or a clean result.

### ar_SA

Status: draft_machine_or_llm
Text: يرتب طابور الأدلة ما يحتاج إلى إثبات بعد ذلك: ختم الحيازة، تجزئة الكاتب العدل، تعليق الحجر، مطالبة الدفع، أو موجز العودة الجزئية. أهمية العنصر التالي أنه يغير الجهة القادرة على تسعير السجل أو دفنه أو نشره أو استخدامه ضد اللاعب. لا يعد أي إدخال في الطابور بالاستخراج أو بنتيجة نظيفة.

### de_DE

Status: draft_machine_or_llm
Text: Die Beweiswarteschlange ordnet, was als Nächstes belegt werden muss: Verwahrungssiegel, Notar-Hash, Quarantänehaltepunkt, Auszahlungsanspruch oder Teilrückkehr-Debrief. Der nächste Eintrag zählt, weil er verändert, wer den Datensatz bepreisen, begraben, veröffentlichen oder gegen den Spieler verwenden kann. Kein Warteschlangeneintrag verspricht Bergung oder ein sauberes Ergebnis.

### es_ES

Status: draft_machine_or_llm
Text: La cola de pruebas ordena qué debe demostrarse después: sello de custodia, hash notarial, retención de cuarentena, reclamación de pago o informe de retorno parcial. El siguiente elemento importa porque cambia quién puede poner precio al registro, enterrarlo, publicarlo o usarlo contra el jugador. Ninguna entrada de la cola promete extracción ni un resultado limpio.

### fr_FR

Status: draft_machine_or_llm
Text: La file de preuves classe ce qui doit être confirmé ensuite : sceau de garde, hash notarié, blocage de quarantaine, demande de paiement ou compte rendu de retour partiel. L'élément suivant compte parce qu'il change qui peut chiffrer le dossier, l'enterrer, le publier ou l'utiliser contre le joueur. Aucune entrée ne promet une extraction ni une issue propre.

### he_IL

Status: draft_machine_or_llm
Text: תור הראיות מדרג מה צריך הוכחה בהמשך: חותם משמורת, גיבוב נוטריוני, עיכוב הסגר, תביעת תשלום או תחקיר חזרה חלקית. הפריט הבא חשוב כי הוא משנה מי יכול לתמחר את הרשומה, לקבור אותה, לפרסם אותה או להשתמש בה נגד השחקן. שום פריט בתור אינו מבטיח חילוץ או תוצאה נקייה.

### id_ID

Status: draft_machine_or_llm
Text: Antrean bukti mengurutkan hal yang harus dibuktikan berikutnya: segel kustodi, hash notaris, penahanan karantina, klaim pembayaran, atau debrief kembali sebagian. Item berikutnya penting karena mengubah siapa yang dapat memberi harga pada catatan, menguburnya, menerbitkannya, atau memakainya melawan pemain. Tidak ada entri antrean yang menjanjikan ekstraksi atau hasil bersih.

### ja_JP

Status: draft_machine_or_llm
Text: 証拠キューは、次に証明すべきものを並べる。保管シール、公証ハッシュ、隔離保留、支払い請求、部分帰還の報告。次の項目が重要なのは、その記録を値付けし、隠し、公開し、またはプレイヤーに対して使える相手が変わるからだ。どの項目も回収やきれいな結末を約束しない。

### ko_KR

Status: draft_machine_or_llm
Text: 증거 대기열은 다음에 입증해야 할 항목을 정렬한다. 보관 인장, 공증 해시, 격리 보류, 지급 청구, 부분 귀환 보고서다. 다음 항목이 중요한 이유는 그 기록에 값을 매기거나 묻어 버리거나 공개하거나 플레이어에게 불리하게 쓸 수 있는 주체가 달라지기 때문이다. 어떤 대기열 항목도 회수나 깨끗한 결과를 약속하지 않는다.

### nl_NL

Status: draft_machine_or_llm
Text: De bewijswachtrij ordent wat hierna bewezen moet worden: bewaarsegel, notarieel hash, quarantaineblokkade, uitbetalingsclaim of debrief van een gedeeltelijke terugkeer. Het volgende item telt omdat het verandert wie het record kan taxeren, begraven, publiceren of tegen de speler gebruiken. Geen enkel wachtrij-item belooft extractie of een schone uitkomst.

### pl_PL

Status: draft_machine_or_llm
Text: Kolejka dowodów porządkuje to, co trzeba potwierdzić dalej: pieczęć depozytu, hash notarialny, wstrzymanie kwarantanny, roszczenie wypłaty albo raport częściowego powrotu. Następny wpis ma znaczenie, bo zmienia, kto może wycenić zapis, ukryć go, opublikować albo użyć przeciw graczowi. Żaden wpis kolejki nie obiecuje wydobycia ani czystego wyniku.

### pt_BR

Status: draft_machine_or_llm
Text: A fila de evidências ordena o que precisa ser provado em seguida: selo de custódia, hash notarial, retenção de quarentena, pedido de pagamento ou relatório de retorno parcial. O próximo item importa porque muda quem pode precificar o registro, enterrá-lo, publicá-lo ou usá-lo contra o jogador. Nenhuma entrada da fila promete extração nem um resultado limpo.

### ru_RU

Status: draft_machine_or_llm
Text: Очередь доказательств расставляет то, что нужно подтвердить дальше: печать цепочки хранения, нотариальный хэш, карантинное удержание, заявку на выплату или разбор частичного возвращения. Следующий пункт важен, потому что меняет того, кто может оценить запись, похоронить ее, опубликовать или использовать против игрока. Ни одна строка очереди не обещает эвакуацию или чистый исход.

### uk_UA

Status: draft_machine_or_llm
Text: Черга доказів упорядковує те, що треба підтвердити далі: печатку ланцюга зберігання, нотаріальний хеш, карантинне утримання, вимогу виплати або розбір часткового повернення. Наступний пункт важливий, бо змінює того, хто може оцінити запис, поховати його, оприлюднити або використати проти гравця. Жоден запис черги не обіцяє евакуацію чи чистий наслідок.

### zh_CN

Status: draft_machine_or_llm
Text: 证据队列排列下一步需要证明的内容：保管封记、公证哈希、隔离暂扣、赔付主张，或部分返回简报。下一个条目之所以重要，是因为它会改变谁能给这份记录定价、掩埋、公开，或拿来对付玩家。队列中的任何条目都不承诺撤离，也不承诺干净的结果。

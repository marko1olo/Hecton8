# P497_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE Production Packet

Packet ID: P497_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Article ID: EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Loc namespace: LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Runtime layer: Narrative AppliedContent source packet

Evidence class: STATIC_DOC

Canon owners: Docs/Lore/Canon_Locks.md; Docs/Lore/Lore_Bible.md; Docs/Lore/Lore_Content_System.md; Docs/Lore/Lore_Localization_Model.md

Surface targets: in-game dossier, terminal note, public archive note, Marauder annotation

Spoiler band: mid-game evidence review through deep-game archive custody

Boundary: copy packet only. It does not define graph schema, edge weights, source rows, route cards, generated pages, h8bin payloads, Unity bindings, or acceptance state.

## Source Brief

Packet ID: P497_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Article ID: EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Loc namespace: LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE

Surface targets: dossier graph legend; terminal graph note; public archive graph note; Marauder annotation

Speaker/source: Packet Notary Interface, public archive caption desk, Marauder field correction

Audience: player reviewer, in-world archive reader, Marauder evidence handler

Date/era: 2190 recovery window

Location/depth/route: dossier screen after first packet custody chain; public archive export after relay notary escape; terminal note beside a redacted object record

Unlock context: player has seen packet custody, a source object, claimant language, witness hash, route consequence, and redaction or caption state in the same evidence chain.

Evidence object: relation overlay tied to a physical source object and a packet witness hash. The overlay is a reading aid, not a trial result.

What this source knows: it can show which records touch the same object, claimant wording, witness hash, route consequence, caption, or redaction.

What this source does not know: it does not know who is guilty, whether a claimant told the truth, or whether a route consequence is deserved.

What this source hides or gets wrong: public archive captions may keep claimant language clean while hiding missing bodies, delayed release gates, or converted losses.

Player use: stop the graph from becoming omniscient. The player should inspect objects, custody marks, captions, and contradictions before treating an edge as evidence.

Forbidden facts: no graph verdicts; no automatic guilt by edge count; no witness-hash truth guarantee; no claimant confession; no implementation of graph data.

Required proper nouns/units: HECTON-8; Deep Reach; Keelmark; Packet Notary Interface; witness hash; route consequence

First-20 route blocker removed: teaches evidence literacy before the dossier UI can mislead the player into treating clean lines as legal truth.

## Surface Texts

### Dossier Graph Legend

RELATION OVERLAY / REVIEW LEGEND

Lines in this dossier show why two records deserve the same table. They are review aids, not verdicts.

Packet custody links a record to the handoff that carried it: tender spool, relay stamp, Packet Notary Interface mark, Keelmark intake, or public ledger copy. Custody says who held the packet. It does not say the packet is honest.

Source object links the text to the thing that carried it: a locker plate, pressure tag, route stamp, black-box frame, captioned image, or damaged terminal. If the object is missing, the line stays weak.

Claimant language preserves the exact office wording. Deep Reach may call a worker an asset category. Keelmark may call a body a load variance. The graph keeps that language visible so the room can contradict it.

Witness hash links packet copies that survived the same notary window. A matching hash can expose tampering or custody drift. It cannot prove that the speaker told the truth.

Route consequence marks what changed after the record entered the chain: opened route, locked gate, delayed recovery, converted loss, quarantine hold, payout cut, or public ledger pressure.

Redaction and caption status shows what the archive lets the reader see. A caption-only object, black bar, cropped plate, or missing source image must be treated as incomplete until another object answers it.

Read the edges as questions: who held this, what object carried it, who claimed it, who witnessed it, what route changed, and what has been hidden.

### Terminal Graph Note

PACKET NOTARY INTERFACE / GRAPH NOTE

Relation overlay loaded from caption and custody fields. Edge display is for review only.

An edge may connect packet custody, source object, claimant language, witness hash, route consequence, or redaction status. Edge weight is not legal weight. Hash match is not witness truth. Caption match is not source recovery.

Open the object record before route action. Open the claimant text before accepting archive wording. Open the redaction card before treating a missing plate as absence.

### Public Archive Graph Note

PUBLIC ARCHIVE NOTE / RELATION VIEW

The archive groups related records so readers can follow the chain without reading every custody packet first. A line between records means the archive found a shared object, phrase, witness hash, consequence, caption, or redaction field.

The line does not accuse a person, clear an office, confirm a casualty, or prove a claim. It marks a place where review should continue. Several HECTON-8 records use lawful claimant language for events that physical evidence later contradicts.

Captioned and redacted records are marked before export. Treat them as partial until the source object or an independent packet copy is available.

### Marauder Annotation

MARAUDER FIELD CORRECTION

Do not let the clean graph do your thinking. A line is a reason to open the locker, not a reason to close the case.

Custody tells you who touched the packet. The object tells you whether the packet had a body in the room. Claimant language tells you what they wanted paid. Hash tells you whether the copy drifted. Route consequence tells you who lost air, access, mass, or leverage after the wording moved.

Caption and redaction state are not decoration. They are where the lie gets cheap.

## Future Integration Notes

Proposed LocIDs:

- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_TITLE
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_DOSSIER_LEGEND
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_TERMINAL_NOTE
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_PUBLIC_ARCHIVE_NOTE
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_MARAUDER_ANNOTATION
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_DENSITY_LOW_COMPACT
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_DENSITY_MIDDLE
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_DENSITY_HIGH
- LORE_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE_DENSITY_ULTRA

Implementation boundary:

- Keep graph copy separate from graph data ownership.
- Do not derive guilt, innocence, route unlocks, payout, quarantine state, or ending branch from visible text.
- Runtime delivery, source tables, hashes, bindings, and generated pages require separate owner work.
- LocIDs stay stable across locales; translations change visible prose only.
- Witness hash, claimant language, and redaction status are evidence descriptors, not gameplay verdicts.

## GlobalQualityWeight Presentation Density

Low/Compact:

- Show title, one-line warning, and the six category names.
- Keep object review instruction visible.
- Hide optional commentary before hiding source object or redaction warnings.
- Same Article ID, LocIDs, custody meaning, and route truth.

Middle:

- Show the full dossier legend and terminal note.
- Include enough category description for first-time review: packet custody, source object, claimant language, witness hash, route consequence, redaction/caption status.
- Keep public archive note short enough for localized expansion.

High:

- Add caption-chain preview, stronger object mismatch labels, and a longer Marauder annotation.
- Add supporting archive snippets only after the physical object remains readable.
- No new truth, no changed route consequence.

Ultra:

- Add dense archive commentary, secondary contradiction hints, and extended dossier history.
- Preserve the warning that edges are review aids, not verdicts.
- Spend presentation budget on better reading context, not extra authority.

## Localization

### en_US

Status: source_authority

Title: Evidence Relation Graph Dossier

Dossier graph legend: Lines in this dossier show why two records deserve the same table. They are review aids, not verdicts. Check packet custody, source object, claimant language, witness hash, route consequence, and redaction or caption status before you treat any edge as evidence.

Terminal graph note: Relation overlay loaded from caption and custody fields. Edge display is for review only. Hash match is not witness truth. Caption match is not source recovery. Open the object record before route action.

Public archive graph note: The archive groups records by shared object, phrase, witness hash, consequence, caption, or redaction field. A line does not accuse, clear, confirm, or prove. It marks where review should continue.

Marauder annotation: Do not let the clean graph do your thinking. Custody says who touched the packet. The object says whether the packet had a body in the room. Caption and redaction state are where the lie gets cheap.

### ar_SA

Status: draft_machine_or_llm

Title: ملف رسم علاقات الأدلة

Dossier graph legend: تظهر الخطوط في هذا الملف سبب وضع سجلين على الطاولة نفسها. إنها أدوات مراجعة، وليست أحكاما. افحص حيازة الحزمة، والشيء المصدر، ولغة المطالب، وبصمة الشاهد، ونتيجة المسار، وحالة الحجب أو التعليق قبل اعتبار أي حافة دليلا.

Terminal graph note: تم تحميل طبقة العلاقات من حقول التعليق والحيازة. عرض الحواف للمراجعة فقط. تطابق البصمة ليس صدق الشاهد. تطابق التعليق ليس استعادة المصدر. افتح سجل الشيء قبل أي إجراء مسار.

Public archive graph note: يجمع الأرشيف السجلات حسب شيء مشترك، أو عبارة، أو بصمة شاهد، أو نتيجة، أو تعليق، أو حقل حجب. الخط لا يتهم ولا يبرئ ولا يؤكد ولا يثبت. إنه يحدد موضعا يجب أن تستمر فيه المراجعة.

Marauder annotation: لا تدع الرسم النظيف يفكر بدلا منك. الحيازة تقول من لمس الحزمة. الشيء يقول هل كانت للحزمة جثة في الغرفة. حالة التعليق والحجب هي المكان الذي تصبح فيه الكذبة رخيصة.

### de_DE

Status: draft_machine_or_llm

Title: Dossier zum Beziehungsgraphen der Beweise

Dossier graph legend: Die Linien in diesem Dossier zeigen, warum zwei Datensätze auf denselben Tisch gehören. Sie sind Prüfhinweise, keine Urteile. Prüfe Paketverwahrung, Quellobjekt, Sprache des Anspruchstellers, Zeugen-Hash, Routenfolge und Schwärzungs- oder Bildunterschriftstatus, bevor du eine Kante als Beweis behandelst.

Terminal graph note: Die Beziehungsebene wurde aus Bildunterschrifts- und Verwahrungsfeldern geladen. Die Kantendarstellung dient nur der Prüfung. Ein Hash-Treffer ist keine Zeugenaussage. Ein Untertitel-Treffer ist keine Quellenbergung. Öffne den Objektdatensatz vor jeder Routenhandlung.

Public archive graph note: Das Archiv gruppiert Datensätze nach gemeinsamem Objekt, Ausdruck, Zeugen-Hash, Folge, Bildunterschrift oder Schwärzungsfeld. Eine Linie klagt nicht an, spricht nicht frei, bestätigt nicht und beweist nichts. Sie markiert, wo die Prüfung weitergehen muss.

Marauder annotation: Lass den sauberen Graphen nicht für dich denken. Verwahrung sagt, wer das Paket berührt hat. Das Objekt sagt, ob zu dem Paket ein Körper im Raum gehörte. Bildunterschrift und Schwärzung sind die Stellen, an denen die Lüge billig wird.

### es_ES

Status: draft_machine_or_llm

Title: Dossier del grafo de relaciones de pruebas

Dossier graph legend: Las líneas de este dossier muestran por qué dos registros merecen la misma mesa. Son ayudas de revisión, no veredictos. Revisa la custodia del paquete, el objeto fuente, el lenguaje del reclamante, el hash testigo, la consecuencia de ruta y el estado de redacción o pie antes de tratar cualquier arista como prueba.

Terminal graph note: La capa de relaciones se cargó desde campos de pie y custodia. La visualización de aristas es solo para revisión. Una coincidencia de hash no es verdad del testigo. Una coincidencia de pie no es recuperación de fuente. Abre el registro del objeto antes de actuar sobre la ruta.

Public archive graph note: El archivo agrupa registros por objeto, frase, hash testigo, consecuencia, pie o campo de redacción compartido. Una línea no acusa, absuelve, confirma ni prueba. Marca dónde debe continuar la revisión.

Marauder annotation: No dejes que el grafo limpio piense por ti. La custodia dice quién tocó el paquete. El objeto dice si el paquete tenía un cuerpo en la sala. El estado del pie y la redacción es donde la mentira se abarata.

### fr_FR

Status: draft_machine_or_llm

Title: Dossier du graphe de relations de preuves

Dossier graph legend: Les lignes de ce dossier montrent pourquoi deux enregistrements doivent être examinés ensemble. Ce sont des aides de revue, pas des verdicts. Vérifie la garde du paquet, l'objet source, le langage du demandeur, le hash témoin, la conséquence de route et l'état de censure ou de légende avant de traiter une arête comme une preuve.

Terminal graph note: La couche de relations a été chargée depuis les champs de légende et de garde. L'affichage des arêtes sert seulement à la revue. Une correspondance de hash ne vaut pas vérité du témoin. Une correspondance de légende ne vaut pas récupération de source. Ouvre la fiche objet avant toute action de route.

Public archive graph note: L'archive regroupe les enregistrements par objet, phrase, hash témoin, conséquence, légende ou champ de censure partagés. Une ligne n'accuse pas, ne blanchit pas, ne confirme pas et ne prouve pas. Elle marque l'endroit où la revue doit continuer.

Marauder annotation: Ne laisse pas le graphe propre penser à ta place. La garde dit qui a touché le paquet. L'objet dit si le paquet avait un corps dans la pièce. L'état de légende et de censure est l'endroit où le mensonge devient bon marché.

### he_IL

Status: draft_machine_or_llm

Title: תיק גרף יחסי הראיות

Dossier graph legend: הקווים בתיק הזה מראים למה שני רשומות צריכות להיבדק על אותה שולחן. אלה כלי סקירה, לא פסקי דין. בדוק משמורת חבילה, עצם מקור, שפת תובע, גיבוב עד, תוצאת מסלול ומצב השחרה או כיתוב לפני שאתה מתייחס לכל קשת כראיה.

Terminal graph note: שכבת היחסים נטענה משדות כיתוב ומשמורת. הצגת הקשתות מיועדת לסקירה בלבד. התאמת גיבוב אינה אמת של עד. התאמת כיתוב אינה שחזור מקור. פתח את רשומת העצם לפני פעולת מסלול.

Public archive graph note: הארכיון מקבץ רשומות לפי עצם משותף, ביטוי, גיבוב עד, תוצאה, כיתוב או שדה השחרה. קו אינו מאשים, מזכה, מאשר או מוכיח. הוא מסמן היכן הסקירה צריכה להמשיך.

Marauder annotation: אל תיתן לגרף הנקי לחשוב במקומך. המשמורת אומרת מי נגע בחבילה. העצם אומר אם לחבילה היה גוף בחדר. מצב הכיתוב וההשחרה הוא המקום שבו השקר נעשה זול.

### id_ID

Status: draft_machine_or_llm

Title: Dossier graf relasi bukti

Dossier graph legend: Garis dalam dossier ini menunjukkan mengapa dua catatan layak diperiksa di meja yang sama. Itu alat tinjau, bukan putusan. Periksa kustodi paket, objek sumber, bahasa pengklaim, hash saksi, konsekuensi rute, serta status redaksi atau kapsi sebelum memperlakukan sisi mana pun sebagai bukti.

Terminal graph note: Lapisan relasi dimuat dari kolom kapsi dan kustodi. Tampilan sisi hanya untuk tinjauan. Kecocokan hash bukan kebenaran saksi. Kecocokan kapsi bukan pemulihan sumber. Buka catatan objek sebelum tindakan rute.

Public archive graph note: Arsip mengelompokkan catatan menurut objek, frasa, hash saksi, konsekuensi, kapsi, atau kolom redaksi yang sama. Garis tidak menuduh, membebaskan, mengonfirmasi, atau membuktikan. Garis menandai tempat tinjauan harus berlanjut.

Marauder annotation: Jangan biarkan graf yang rapi berpikir untukmu. Kustodi mengatakan siapa yang menyentuh paket. Objek mengatakan apakah paket itu punya tubuh di ruangan. Status kapsi dan redaksi adalah tempat kebohongan menjadi murah.

### ja_JP

Status: draft_machine_or_llm

Title: 証拠関係グラフ dossier

Dossier graph legend: この dossier の線は、二つの記録を同じ卓上で確認すべき理由を示す。これは審査の補助であり、判決ではない。どのエッジも証拠として扱う前に、パケットの保管経路、元の物体、請求者の文言、証人ハッシュ、ルート上の結果、墨消しまたはキャプションの状態を確認すること。

Terminal graph note: 関係オーバーレイはキャプション欄と保管欄から読み込まれた。エッジ表示は審査用のみ。ハッシュ一致は証人の真実ではない。キャプション一致は元資料の回収ではない。ルート操作の前に物体記録を開くこと。

Public archive graph note: アーカイブは、共通の物体、語句、証人ハッシュ、結果、キャプション、墨消し欄で記録をまとめる。線は告発も免責も確認も証明もしない。審査を続ける場所を示すだけである。

Marauder annotation: きれいなグラフに考えさせるな。保管経路は誰がパケットに触れたかを示す。物体は、そのパケットに部屋の遺体が伴っていたかを示す。キャプションと墨消しの状態こそ、嘘が安くなる場所だ。

### ko_KR

Status: draft_machine_or_llm

Title: 증거 관계 그래프 문서

Dossier graph legend: 이 문서의 선은 두 기록을 같은 검토대에 올려야 하는 이유를 보여 준다. 그것은 검토 보조 수단이지 판결이 아니다. 어떤 모서리든 증거로 다루기 전에 패킷 보관 경로, 원본 객체, 청구자 문구, 증인 해시, 경로 결과, 삭제 또는 캡션 상태를 확인하라.

Terminal graph note: 관계 오버레이가 캡션 및 보관 필드에서 로드되었다. 모서리 표시는 검토용이다. 해시 일치는 증인의 진실이 아니다. 캡션 일치는 원본 회수가 아니다. 경로 조치 전에 객체 기록을 열어라.

Public archive graph note: 아카이브는 공유 객체, 문구, 증인 해시, 결과, 캡션 또는 삭제 필드로 기록을 묶는다. 선은 고발도, 면책도, 확인도, 증명도 하지 않는다. 검토가 계속되어야 할 지점을 표시한다.

Marauder annotation: 깔끔한 그래프가 대신 생각하게 두지 마라. 보관 경로는 누가 패킷을 만졌는지 말한다. 객체는 그 패킷이 방 안의 시신과 연결되는지 말한다. 캡션과 삭제 상태는 거짓말이 싸게 처리되는 곳이다.

### nl_NL

Status: draft_machine_or_llm

Title: Dossier voor bewijsrelatiegrafiek

Dossier graph legend: De lijnen in dit dossier tonen waarom twee records op dezelfde tafel horen. Het zijn hulpmiddelen voor beoordeling, geen vonnissen. Controleer pakketbewaring, bronobject, taal van de eiser, getuige-hash, routegevolg en redactie- of bijschriftstatus voordat je een rand als bewijs behandelt.

Terminal graph note: De relatielaag is geladen uit bijschrift- en bewaringsvelden. Randweergave is alleen voor beoordeling. Een hashmatch is geen getuigenwaarheid. Een bijschriftmatch is geen bronherstel. Open het objectrecord voor routeactie.

Public archive graph note: Het archief groepeert records op gedeeld object, zin, getuige-hash, gevolg, bijschrift of redactieveld. Een lijn beschuldigt niet, spreekt niet vrij, bevestigt niet en bewijst niets. Ze markeert waar de beoordeling moet doorgaan.

Marauder annotation: Laat de nette grafiek niet voor je denken. Bewaring zegt wie het pakket heeft aangeraakt. Het object zegt of het pakket een lichaam in de kamer had. Bijschrift- en redactiestatus zijn waar de leugen goedkoop wordt.

### pl_PL

Status: draft_machine_or_llm

Title: Dossier grafu relacji dowodów

Dossier graph legend: Linie w tym dossier pokazują, dlaczego dwa zapisy powinny trafić na ten sam stół. To pomoce do przeglądu, nie wyroki. Sprawdź pieczę nad pakietem, obiekt źródłowy, język roszczącego, hash świadka, skutek trasy oraz stan redakcji lub podpisu, zanim potraktujesz jakąkolwiek krawędź jako dowód.

Terminal graph note: Warstwa relacji została wczytana z pól podpisu i pieczy. Wyświetlanie krawędzi służy tylko przeglądowi. Zgodność hasha nie jest prawdą świadka. Zgodność podpisu nie jest odzyskaniem źródła. Otwórz zapis obiektu przed działaniem na trasie.

Public archive graph note: Archiwum grupuje zapisy według wspólnego obiektu, frazy, hasha świadka, skutku, podpisu albo pola redakcji. Linia nie oskarża, nie oczyszcza, nie potwierdza i nie dowodzi. Oznacza miejsce, w którym przegląd ma iść dalej.

Marauder annotation: Nie pozwól, by czysty graf myślał za ciebie. Piecza mówi, kto dotknął pakietu. Obiekt mówi, czy pakiet miał ciało w pomieszczeniu. Stan podpisu i redakcji to miejsce, gdzie kłamstwo robi się tanie.

### pt_BR

Status: draft_machine_or_llm

Title: Dossiê do grafo de relações de evidência

Dossier graph legend: As linhas deste dossiê mostram por que dois registros merecem a mesma mesa. São auxílios de revisão, não veredictos. Verifique custódia do pacote, objeto fonte, linguagem do reclamante, hash de testemunha, consequência de rota e estado de redação ou legenda antes de tratar qualquer aresta como evidência.

Terminal graph note: A camada de relações foi carregada de campos de legenda e custódia. A exibição de arestas é apenas para revisão. Correspondência de hash não é verdade de testemunha. Correspondência de legenda não é recuperação da fonte. Abra o registro do objeto antes da ação de rota.

Public archive graph note: O arquivo agrupa registros por objeto, frase, hash de testemunha, consequência, legenda ou campo de redação compartilhado. Uma linha não acusa, não absolve, não confirma e não prova. Ela marca onde a revisão deve continuar.

Marauder annotation: Não deixe o grafo limpo pensar por você. Custódia diz quem tocou no pacote. O objeto diz se o pacote tinha um corpo na sala. Estado de legenda e redação é onde a mentira fica barata.

### ru_RU

Status: draft_machine_or_llm

Title: Досье графа связей доказательств

Dossier graph legend: Линии в этом досье показывают, почему две записи нужно рассматривать на одном столе. Это подсказки для проверки, а не приговоры. Проверьте хранение пакета, объект-источник, язык заявителя, хэш свидетеля, последствие маршрута и состояние редактуры или подписи, прежде чем считать любую грань доказательством.

Terminal graph note: Слой связей загружен из полей подписи и хранения. Отображение граней предназначено только для проверки. Совпадение хэша не означает правду свидетеля. Совпадение подписи не означает восстановление источника. Откройте запись объекта перед действием по маршруту.

Public archive graph note: Архив группирует записи по общему объекту, фразе, хэшу свидетеля, последствию, подписи или полю редактуры. Линия не обвиняет, не оправдывает, не подтверждает и не доказывает. Она отмечает место, где проверка должна продолжаться.

Marauder annotation: Не позволяй чистому графу думать за тебя. Хранение говорит, кто касался пакета. Объект говорит, был ли у пакета труп в комнате. Состояние подписи и редактуры - там, где ложь дешевеет.

### uk_UA

Status: draft_machine_or_llm

Title: Досьє графа зв'язків доказів

Dossier graph legend: Лінії в цьому досьє показують, чому два записи треба розглядати на одному столі. Це допоміжні позначки для перевірки, а не вироки. Перевірте зберігання пакета, об'єкт-джерело, мову заявника, хеш свідка, наслідок маршруту та стан редагування або підпису, перш ніж вважати будь-яке ребро доказом.

Terminal graph note: Шар зв'язків завантажено з полів підпису та зберігання. Відображення ребер призначене тільки для перевірки. Збіг хешу не є правдою свідка. Збіг підпису не є відновленням джерела. Відкрийте запис об'єкта перед дією маршруту.

Public archive graph note: Архів групує записи за спільним об'єктом, фразою, хешем свідка, наслідком, підписом або полем редагування. Лінія не звинувачує, не виправдовує, не підтверджує і не доводить. Вона позначає місце, де перевірка має тривати.

Marauder annotation: Не дозволяй чистому графу думати за тебе. Зберігання каже, хто торкався пакета. Об'єкт каже, чи був у пакета труп у кімнаті. Стан підпису та редагування - там, де брехня дешевшає.

### zh_CN

Status: draft_machine_or_llm

Title: 证据关系图档案

Dossier graph legend: 本档案中的连线说明为什么两条记录需要放在同一张审查桌上。它们是审查辅助，不是裁决。把任何边当成证据之前，先检查数据包保管、源物件、申索方措辞、见证哈希、路线后果，以及删节或说明文字状态。

Terminal graph note: 关系叠层已从说明文字和保管字段载入。边显示仅用于审查。哈希匹配不等于见证真实。说明文字匹配不等于源资料已找回。执行路线操作前，先打开物件记录。

Public archive graph note: 档案按共同物件、短语、见证哈希、后果、说明文字或删节字段来分组记录。一条线不会指控、洗清、确认或证明。它只标记审查必须继续的位置。

Marauder annotation: 别让干净的图替你思考。保管记录说明谁碰过数据包。物件说明这个数据包是否对应房间里的尸体。说明文字和删节状态，就是谎言变便宜的地方。

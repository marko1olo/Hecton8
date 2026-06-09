import json
from pathlib import Path


PATH = Path("Docs/Lore/AppliedContent/packets/RS082_DEEP_REACH_ARTIFACT_MEMO_PACK.packets.json")
PACKET_ID = "P410_SATO_REN_RETURN_ACTION_PACKET_ARTIFACT"


LOCALIZED = {
    "en_US": {
        "localization_status": "source_authority",
        "title": "Sato-Ren Return Action Packet",
        "scanner": "Recovery compliance packet. Coordinates, Atlas access state, XO sample custody, and silence acknowledgement are requested before extraction appears.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: transmit coordinates, Atlas access state, XO sample custody, carrier mass estimate, and contractor silence acknowledgement before extraction language is issued.",
        "audio": "It knows you are breathing. It asks what you are carrying first.",
        "in_game_wiki": "The Sato-Ren packet proves Deep Reach is active in 2190 without needing a rescue fleet on screen. The company returns as compliance traffic through a rare signal window. Its order matters: coordinates, Atlas access state, sample custody, carrier mass, silence acknowledgement, and only then extraction language. The packet can be help, repossession, or blackmail depending on what proof the Marauder holds when it arrives.",
        "external_site": "Sato-Ren brings Deep Reach back to HECTON-8 in the present tense: not as a corpse in old archives, but as a live packet that asks for custody before it offers extraction.",
        "external_site_article": "## Extraction After Custody\n\nThe Sato-Ren return packet is a late-game document with the temperature of a live wire. It arrives after the player has found old Deep Reach memos, ledgers, waivers and hold orders, then proves the company is still able to speak through narrow Aegir windows. It does not arrive as a rescue ship. It arrives as a compliance sequence.\n\nThat sequence is the pressure point. The packet asks for coordinates, Atlas access state, XO sample custody, carrier mass estimate and contractor silence acknowledgement before it uses extraction language. A rescue desk asks where the trapped person is hurt. Sato-Ren asks what the trapped person is carrying, who can certify it, and whether silence is accepted as a condition of movement.\n\nThe same packet can look like help, repossession or blackmail. It depends on the evidence already in the Marauder's hands: the margin memo, the Atlas waiver, the quarantine hold, the loss ledger, the receiver proof. Sato-Ren connects the drowned bureaucracy to a living choice. Sell coordinates, preserve proof, sever Atlas, publish the ledger or keep moving while even rescue has a claim number.",
        "field_note": "Marauder note: if extraction comes after custody, read the order twice."
    },
    "ru_RU": {
        "localization_status": "draft_machine_or_llm",
        "title": "Пакет возвратного действия Sato-Ren",
        "scanner": "Пакет compliance по восстановлению. Координаты, состояние доступа Atlas, хранение образца XO и подтверждение молчания запрошены до появления слова «эвакуация».",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: передать координаты, состояние доступа Atlas, хранение образца XO, оценку массы носителя и подтверждение молчания подрядчика до выдачи формулировки эвакуации.",
        "audio": "Он знает, что ты дышишь. Сначала он спрашивает, что ты несешь.",
        "in_game_wiki": "Пакет Sato-Ren доказывает, что Deep Reach активна в 2190 году без появления спасательного флота в кадре. Компания возвращается как compliance-трафик через редкое окно сигнала. Важен порядок: координаты, состояние доступа Atlas, хранение образца, масса носителя, подтверждение молчания и только потом язык эвакуации. Пакет может быть помощью, изъятием или шантажом в зависимости от того, какие доказательства у мародера на руках в момент получения.",
        "external_site": "Sato-Ren возвращает Deep Reach на HECTON-8 в настоящем времени: не как труп в старом архиве, а как живой пакет, который просит хранение до того, как предлагает эвакуацию.",
        "external_site_article": "## Эвакуация после хранения\n\nВозвратный пакет Sato-Ren — поздний документ с температурой живого провода. Он приходит после того, как игрок нашел старые мемо Deep Reach, журналы, отказы и удержания, а затем доказывает, что компания все еще умеет говорить через узкие окна Aegir. Он не приходит как спасательный корабль. Он приходит как последовательность compliance.\n\nЭта последовательность и есть точка давления. Пакет просит координаты, состояние доступа Atlas, хранение образца XO, оценку массы носителя и подтверждение молчания подрядчика до того, как использует слово эвакуации. Спасательный стол спрашивает, где застрявший человек ранен. Sato-Ren спрашивает, что застрявший человек несет, кто может это заверить и принято ли молчание как условие перемещения.\n\nОдин и тот же пакет может выглядеть как помощь, изъятие или шантаж. Это зависит от доказательств, которые уже есть у мародера: мемо допуска, отказ Atlas, карантинное удержание, журнал потерь, доказательство приемника. Sato-Ren соединяет утонувшую бюрократию с живым выбором. Продать координаты, сохранить доказательство, отсечь Atlas, опубликовать журнал или идти дальше, пока даже спасение имеет номер заявки.",
        "field_note": "Заметка мародера: если эвакуация идет после хранения, перечитай порядок дважды."
    },
    "ja_JP": {
        "localization_status": "draft_machine_or_llm",
        "title": "Sato-Ren帰還アクションパケット",
        "scanner": "回収コンプライアンスパケット。座標、Atlasアクセス状態、XOサンプル保管、沈黙承認が、抽出語の前に要求される。",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: 抽出文言発行前に、座標、Atlasアクセス状態、XOサンプル保管、キャリア質量見積、契約者沈黙承認を送信。",
        "audio": "それは君が呼吸していると知っている。最初に聞くのは、何を運んでいるかだ。",
        "in_game_wiki": "Sato-Renパケットは、救助艦隊を画面に出さずに2190年のDeep Reachが活動中であることを証明する。会社は、まれな信号窓を通るコンプライアンス通信として戻る。順序が重要だ。座標、Atlasアクセス状態、サンプル保管、キャリア質量、沈黙承認、そして最後に抽出文言。到着時にマローダーが持つ証拠によって、このパケットは助けにも、差し押さえにも、脅迫にも見える。",
        "external_site": "Sato-RenはDeep Reachを現在形でHECTON-8へ戻す。古いアーカイブの死体としてではなく、抽出を申し出る前に保管を求める生きたパケットとして。",
        "external_site_article": "## 保管の後の抽出\n\nSato-Ren帰還パケットは、通電中のワイヤーの温度を持つ終盤文書だ。プレイヤーが古いDeep Reachメモ、台帳、権利放棄、保留命令を見つけた後に届き、会社がまだAegirの狭い窓を通じて話せることを証明する。救助船としては届かない。コンプライアンス手順として届く。\n\nその手順が圧力点である。パケットは、抽出文言を使う前に座標、Atlasアクセス状態、XOサンプル保管、キャリア質量見積、契約者沈黙承認を求める。救助卓なら、閉じ込められた人がどこを負傷しているかを聞く。Sato-Renは、その人が何を運び、誰が認証でき、移動条件として沈黙を受け入れるかを聞く。\n\n同じパケットは、助け、回収、脅迫のどれにも見える。マローダーの手にある証拠しだいだ。マージンメモ、Atlas権利放棄、隔離保留、損失台帳、受信者証拠。Sato-Renは沈んだ官僚制を生きた選択につなぐ。座標を売る。証拠を守る。Atlasを切る。台帳を公開する。あるいは、救助にすら請求番号がある海を進み続ける。",
        "field_note": "マローダー記録: 抽出が保管の後に来るなら、順序を二度読め。"
    },
    "zh_CN": {
        "localization_status": "draft_machine_or_llm",
        "title": "Sato-Ren返回行动包",
        "scanner": "回收合规包。坐标、Atlas访问状态、XO样本保管和沉默确认都在“撤离”措辞出现前被要求。",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION：在发布撤离措辞前，传输坐标、Atlas访问状态、XO样本保管、载体质量估计和承包方沉默确认。",
        "audio": "它知道你还在呼吸。它先问你带着什么。",
        "in_game_wiki": "Sato-Ren包证明Deep Reach在2190年仍然活跃，不需要让救援舰队出现在画面里。公司通过稀有信号窗口以合规流量的形式返回。顺序很重要：坐标、Atlas访问状态、样本保管、载体质量、沉默确认，然后才是撤离措辞。它是帮助、收回还是勒索，取决于掠夺者收到时手里有什么证据。",
        "external_site": "Sato-Ren把Deep Reach以现在时带回HECTON-8：不是旧档案里的尸体，而是一个活包裹，在提供撤离前先要求保管。",
        "external_site_article": "## 保管之后的撤离\n\nSato-Ren返回包是一份带着通电导线温度的后期文件。它在玩家找到旧Deep Reach备忘录、账本、豁免和扣留命令之后抵达，证明公司仍能通过Aegir狭窄窗口说话。它不是救援船。它是一串合规流程。\n\n这串流程就是压力点。包裹在使用撤离语言前，要求坐标、Atlas访问状态、XO样本保管、载体质量估计和承包方沉默确认。救援台会问被困者哪里受伤。Sato-Ren问被困者带着什么、谁能认证，以及是否接受沉默作为移动条件。\n\n同一个包裹可以像帮助、收回或勒索。取决于掠夺者手里的证据：裕度备忘录、Atlas豁免、隔离扣留、损失账本、接收者证据。Sato-Ren把淹没的官僚系统连到一个活选择上。出售坐标，保全证据，切断Atlas，公开账本，或继续前进，因为在这片海里连救援都有索赔号。",
        "field_note": "掠夺者笔记：如果撤离排在保管之后，把顺序读两遍。"
    },
    "fr_FR": {
        "localization_status": "draft_machine_or_llm",
        "title": "Paquet d'action retour Sato-Ren",
        "scanner": "Paquet conformité de récupération. Coordonnées, état d'accès Atlas, garde d'échantillon XO et accusé de silence sont requis avant le mot extraction.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION : transmettre coordonnées, état d'accès Atlas, garde d'échantillon XO, estimation de masse transporteur et accusé de silence contractant avant émission du langage d'extraction.",
        "audio": "Il sait que tu respires. Il demande d'abord ce que tu portes.",
        "in_game_wiki": "Le paquet Sato-Ren prouve que Deep Reach est active en 2190 sans afficher une flotte de secours. La société revient comme trafic de conformité dans une rare fenêtre de signal. L'ordre compte : coordonnées, état d'accès Atlas, garde d'échantillon, masse de transporteur, accusé de silence, puis seulement le langage d'extraction. Le paquet peut être aide, reprise ou chantage selon les preuves que le Maraudeur possède à son arrivée.",
        "external_site": "Sato-Ren ramène Deep Reach à HECTON-8 au présent : non comme cadavre d'archive, mais comme paquet vivant qui demande la garde avant d'offrir l'extraction.",
        "external_site_article": "## Extraction après garde\n\nLe paquet retour Sato-Ren est un document de fin de jeu avec la température d'un fil sous tension. Il arrive après les anciens mémos, registres, dérogations et ordres de retenue Deep Reach, puis prouve que la société peut encore parler par les fenêtres étroites d'Aegir. Il n'arrive pas comme un vaisseau de secours. Il arrive comme une séquence de conformité.\n\nCette séquence est le point de pression. Le paquet demande coordonnées, état d'accès Atlas, garde d'échantillon XO, estimation de masse transporteur et accusé de silence contractant avant d'employer le langage d'extraction. Un bureau de secours demande où la personne piégée est blessée. Sato-Ren demande ce qu'elle transporte, qui peut le certifier et si le silence est accepté comme condition de déplacement.\n\nLe même paquet peut ressembler à une aide, une reprise ou un chantage. Tout dépend des preuves déjà dans les mains du Maraudeur : mémo de marge, dérogation Atlas, retenue de quarantaine, registre des pertes, preuve receveur. Sato-Ren relie la bureaucratie noyée à un choix vivant. Vendre les coordonnées, préserver la preuve, couper Atlas, publier le registre ou continuer dans un océan où même le secours porte un numéro de réclamation.",
        "field_note": "Note de Maraudeur : si extraction vient après garde, relis l'ordre deux fois."
    },
    "es_ES": {
        "localization_status": "draft_machine_or_llm",
        "title": "Paquete de acción de retorno Sato-Ren",
        "scanner": "Paquete de cumplimiento de recuperación. Coordenadas, estado de acceso Atlas, custodia de muestra XO y acuse de silencio se solicitan antes de que aparezca extracción.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: transmitir coordenadas, estado de acceso Atlas, custodia de muestra XO, estimación de masa de portador y acuse de silencio de contratista antes de emitir lenguaje de extracción.",
        "audio": "Sabe que respiras. Primero pregunta qué llevas.",
        "in_game_wiki": "El paquete Sato-Ren demuestra que Deep Reach está activa en 2190 sin poner una flota de rescate en pantalla. La compañía vuelve como tráfico de cumplimiento a través de una ventana de señal rara. El orden importa: coordenadas, estado de acceso Atlas, custodia de muestra, masa de portador, acuse de silencio y solo entonces lenguaje de extracción. El paquete puede ser ayuda, recuperación o chantaje según las pruebas que tenga el Merodeador cuando llega.",
        "external_site": "Sato-Ren devuelve a Deep Reach a HECTON-8 en presente: no como cadáver de archivo, sino como paquete vivo que pide custodia antes de ofrecer extracción.",
        "external_site_article": "## Extracción después de custodia\n\nEl paquete de retorno Sato-Ren es un documento de tramo final con temperatura de cable vivo. Llega después de que el jugador encuentre viejos memorandos, libros, renuncias y órdenes de retención de Deep Reach, y prueba que la compañía aún puede hablar por las ventanas estrechas de Aegir. No llega como nave de rescate. Llega como secuencia de cumplimiento.\n\nEsa secuencia es el punto de presión. El paquete pide coordenadas, estado de acceso Atlas, custodia de muestra XO, estimación de masa de portador y acuse de silencio de contratista antes de usar lenguaje de extracción. Una mesa de rescate pregunta dónde está herida la persona atrapada. Sato-Ren pregunta qué lleva, quién puede certificarlo y si acepta silencio como condición de movimiento.\n\nEl mismo paquete puede parecer ayuda, recuperación o chantaje. Depende de las pruebas que ya estén en manos del Merodeador: el memo de margen, la renuncia Atlas, la retención de cuarentena, el libro de pérdidas, la prueba de receptor. Sato-Ren conecta la burocracia ahogada con una elección viva. Vender coordenadas, conservar prueba, cortar Atlas, publicar el libro o seguir moviéndose por un océano donde incluso el rescate tiene número de reclamación.",
        "field_note": "Nota de Merodeador: si extracción viene después de custodia, lee el orden dos veces."
    },
    "de_DE": {
        "localization_status": "draft_machine_or_llm",
        "title": "Sato-Ren-Rückführungsaktionspaket",
        "scanner": "Recovery-Compliance-Paket. Koordinaten, Atlas-Zugriffsstatus, XO-Probenverwahrung und Schweigebestätigung werden vor Extraktionssprache angefordert.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: Koordinaten, Atlas-Zugriffsstatus, XO-Probenverwahrung, Trägermassenschätzung und Schweigebestätigung des Auftragnehmers vor Ausgabe von Extraktionssprache übertragen.",
        "audio": "Es weiß, dass du atmest. Zuerst fragt es, was du trägst.",
        "in_game_wiki": "Das Sato-Ren-Paket beweist, dass Deep Reach 2190 aktiv ist, ohne eine Rettungsflotte zu zeigen. Das Unternehmen kehrt als Compliance-Verkehr durch ein seltenes Signalfenster zurück. Die Reihenfolge zählt: Koordinaten, Atlas-Zugriffsstatus, Probenverwahrung, Trägermasse, Schweigebestätigung und erst dann Extraktionssprache. Das Paket kann Hilfe, Rücknahme oder Erpressung sein, je nachdem, welche Beweise der Marauder beim Eintreffen hält.",
        "external_site": "Sato-Ren bringt Deep Reach in der Gegenwart nach HECTON-8 zurück: nicht als Leiche im alten Archiv, sondern als lebendes Paket, das Verwahrung verlangt, bevor es Extraktion anbietet.",
        "external_site_article": "## Extraktion nach Verwahrung\n\nDas Sato-Ren-Rückführungspaket ist ein Spieldokument mit der Temperatur eines stromführenden Kabels. Es kommt, nachdem der Spieler alte Deep-Reach-Memos, Bücher, Verzichtserklärungen und Haltebefehle gefunden hat, und beweist dann, dass das Unternehmen noch durch Aegirs enge Fenster sprechen kann. Es kommt nicht als Rettungsschiff. Es kommt als Compliance-Sequenz.\n\nDiese Sequenz ist der Druckpunkt. Das Paket verlangt Koordinaten, Atlas-Zugriffsstatus, XO-Probenverwahrung, Trägermassenschätzung und Schweigebestätigung des Auftragnehmers, bevor es Extraktionssprache verwendet. Ein Rettungstisch fragt, wo die eingeschlossene Person verletzt ist. Sato-Ren fragt, was sie trägt, wer es zertifizieren kann und ob Schweigen als Bewegungsbedingung akzeptiert wird.\n\nDasselbe Paket kann wie Hilfe, Rücknahme oder Erpressung aussehen. Es hängt von den Beweisen ab, die der Marauder bereits besitzt: Margenmemo, Atlas-Verzicht, Quarantänehalt, Verlustbuch, Empfängerbeweis. Sato-Ren verbindet die ertrunkene Bürokratie mit einer lebenden Wahl. Koordinaten verkaufen, Beweise bewahren, Atlas trennen, das Buch veröffentlichen oder weitergehen durch einen Ozean, in dem selbst Rettung eine Anspruchsnummer hat.",
        "field_note": "Marauder-Notiz: Wenn Extraktion nach Verwahrung kommt, lies die Reihenfolge zweimal."
    },
    "pl_PL": {
        "localization_status": "draft_machine_or_llm",
        "title": "Pakiet akcji powrotnej Sato-Ren",
        "scanner": "Pakiet zgodności odzysku. Współrzędne, stan dostępu Atlas, przechowanie próbki XO i potwierdzenie ciszy są żądane przed pojawieniem się słowa ekstrakcja.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: przesłać współrzędne, stan dostępu Atlas, przechowanie próbki XO, szacunek masy nośnika i potwierdzenie ciszy kontraktora przed wydaniem języka ekstrakcji.",
        "audio": "Wie, że oddychasz. Najpierw pyta, co niesiesz.",
        "in_game_wiki": "Pakiet Sato-Ren dowodzi, że Deep Reach działa w 2190 roku bez pokazywania floty ratunkowej. Firma wraca jako ruch zgodności przez rzadkie okno sygnału. Kolejność ma znaczenie: współrzędne, stan dostępu Atlas, przechowanie próbki, masa nośnika, potwierdzenie ciszy i dopiero potem język ekstrakcji. Pakiet może być pomocą, przejęciem albo szantażem zależnie od dowodów, które Marauder trzyma w chwili nadejścia.",
        "external_site": "Sato-Ren sprowadza Deep Reach z powrotem na HECTON-8 w czasie teraźniejszym: nie jako trupa w starych archiwach, lecz jako żywy pakiet, który prosi o przechowanie, zanim zaoferuje ekstrakcję.",
        "external_site_article": "## Ekstrakcja po przechowaniu\n\nPakiet powrotny Sato-Ren to późny dokument o temperaturze przewodu pod napięciem. Przychodzi po starych memach Deep Reach, rejestrach, zrzeczeniach i rozkazach wstrzymania, a potem dowodzi, że firma nadal potrafi mówić przez wąskie okna Aegir. Nie przychodzi jako statek ratunkowy. Przychodzi jako sekwencja zgodności.\n\nTa sekwencja jest punktem nacisku. Pakiet prosi o współrzędne, stan dostępu Atlas, przechowanie próbki XO, szacunek masy nośnika i potwierdzenie ciszy kontraktora, zanim użyje języka ekstrakcji. Biurko ratunkowe pyta, gdzie uwięziona osoba jest ranna. Sato-Ren pyta, co ta osoba niesie, kto może to poświadczyć i czy cisza jest przyjęta jako warunek ruchu.\n\nTen sam pakiet może wyglądać jak pomoc, przejęcie albo szantaż. Zależy od dowodów w rękach Maraudera: memo marginesu, zrzeczenie Atlas, zatrzymanie kwarantanny, rejestr strat, dowód odbiorcy. Sato-Ren łączy zatopioną biurokrację z żywym wyborem. Sprzedać współrzędne, zachować dowód, odciąć Atlas, opublikować rejestr albo iść dalej przez ocean, w którym nawet ratunek ma numer roszczenia.",
        "field_note": "Notatka Maraudera: jeśli ekstrakcja przychodzi po przechowaniu, przeczytaj kolejność dwa razy."
    },
    "uk_UA": {
        "localization_status": "draft_machine_or_llm",
        "title": "Пакет зворотної дії Sato-Ren",
        "scanner": "Пакет compliance з відновлення. Координати, стан доступу Atlas, зберігання зразка XO й підтвердження мовчання запитані до появи слова евакуація.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: передати координати, стан доступу Atlas, зберігання зразка XO, оцінку маси носія й підтвердження мовчання підрядника до видачі формулювання евакуації.",
        "audio": "Він знає, що ти дихаєш. Спершу питає, що ти несеш.",
        "in_game_wiki": "Пакет Sato-Ren доводить, що Deep Reach активна у 2190 році без появи рятувального флоту на екрані. Компанія повертається як compliance-трафік через рідкісне сигнальне вікно. Важливий порядок: координати, стан доступу Atlas, зберігання зразка, маса носія, підтвердження мовчання і лише потім мова евакуації. Пакет може бути допомогою, вилученням або шантажем залежно від того, які докази мародер має в руках на момент отримання.",
        "external_site": "Sato-Ren повертає Deep Reach на HECTON-8 у теперішньому часі: не як тіло в старих архівах, а як живий пакет, що просить зберігання до того, як пропонує евакуацію.",
        "external_site_article": "## Евакуація після зберігання\n\nЗворотний пакет Sato-Ren — пізній документ із температурою живого дроту. Він приходить після того, як гравець знаходить старі мемо Deep Reach, журнали, відмови й накази утримання, а потім доводить, що компанія все ще може говорити крізь вузькі вікна Aegir. Він не приходить як рятувальний корабель. Він приходить як послідовність compliance.\n\nЦя послідовність і є точкою тиску. Пакет просить координати, стан доступу Atlas, зберігання зразка XO, оцінку маси носія й підтвердження мовчання підрядника до того, як використовує мову евакуації. Рятувальний стіл питає, де поранена застрягла людина. Sato-Ren питає, що ця людина несе, хто може це засвідчити і чи прийнято мовчання як умову переміщення.\n\nОдин і той самий пакет може виглядати як допомога, вилучення або шантаж. Це залежить від доказів у руках мародера: мемо допуску, відмова Atlas, карантинне утримання, журнал втрат, доказ приймача. Sato-Ren з'єднує втоплену бюрократію з живим вибором. Продати координати, зберегти доказ, відсікти Atlas, опублікувати журнал або йти далі океаном, де навіть порятунок має номер заявки.",
        "field_note": "Нотатка мародера: якщо евакуація йде після зберігання, перечитай порядок двічі."
    },
    "ar_SA": {
        "localization_status": "draft_machine_or_llm",
        "title": "حزمة إجراء العودة Sato-Ren",
        "scanner": "حزمة امتثال استرداد. تُطلب الإحداثيات، وحالة وصول Atlas، وحفظ عينة XO، وإقرار الصمت قبل ظهور لغة الاستخراج.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: أرسل الإحداثيات، وحالة وصول Atlas، وحفظ عينة XO، وتقدير كتلة الناقل، وإقرار صمت المتعاقد قبل إصدار لغة الاستخراج.",
        "audio": "يعرف أنك تتنفس. يسأل أولا عما تحمله.",
        "in_game_wiki": "تثبت حزمة Sato-Ren أن Deep Reach نشطة في 2190 دون حاجة إلى أسطول إنقاذ على الشاشة. تعود الشركة كحركة امتثال عبر نافذة إشارة نادرة. يهم الترتيب: الإحداثيات، حالة وصول Atlas، حفظ العينة، كتلة الناقل، إقرار الصمت، ثم فقط لغة الاستخراج. قد تكون الحزمة مساعدة أو استعادة ملكية أو ابتزازا حسب الدليل الذي يحمله Marauder عند وصولها.",
        "external_site": "تعيد Sato-Ren شركة Deep Reach إلى HECTON-8 بصيغة الحاضر: لا كجثة في أرشيف قديم، بل كحزمة حية تطلب الحفظ قبل أن تعرض الاستخراج.",
        "external_site_article": "## الاستخراج بعد الحفظ\n\nحزمة العودة Sato-Ren وثيقة متأخرة بحرارة سلك حي. تصل بعد أن يجد اللاعب مذكرات Deep Reach القديمة والسجلات والتنازلات وأوامر الاحتجاز، ثم تثبت أن الشركة لا تزال قادرة على الكلام عبر نوافذ Aegir الضيقة. لا تصل كسفينة إنقاذ. تصل كتسلسل امتثال.\n\nهذا التسلسل هو نقطة الضغط. تطلب الحزمة الإحداثيات، وحالة وصول Atlas، وحفظ عينة XO، وتقدير كتلة الناقل، وإقرار صمت المتعاقد قبل أن تستخدم لغة الاستخراج. يسأل مكتب الإنقاذ أين أُصيب الشخص العالق. تسأل Sato-Ren ما الذي يحمله ذلك الشخص، ومن يستطيع تصديقه، وهل قُبل الصمت شرطا للحركة.\n\nيمكن للحزمة نفسها أن تبدو مساعدة أو استعادة أو ابتزازا. يعتمد ذلك على الأدلة الموجودة في يد Marauder: مذكرة الهامش، وتنازل Atlas، واحتجاز الحجر، وسجل الخسائر، ودليل المستقبل. تصل Sato-Ren البيروقراطية الغارقة بخيار حي. بع الإحداثيات، احفظ الدليل، اقطع Atlas، انشر السجل، أو واصل الحركة في محيط حتى الإنقاذ فيه يحمل رقم مطالبة.",
        "field_note": "ملاحظة Marauder: إذا جاء الاستخراج بعد الحفظ، فاقرأ الترتيب مرتين."
    },
    "id_ID": {
        "localization_status": "draft_machine_or_llm",
        "title": "Paket tindakan kembali Sato-Ren",
        "scanner": "Paket kepatuhan pemulihan. Koordinat, status akses Atlas, kustodi sampel XO, dan pengakuan diam diminta sebelum kata ekstraksi muncul.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: kirim koordinat, status akses Atlas, kustodi sampel XO, estimasi massa pembawa, dan pengakuan diam kontraktor sebelum bahasa ekstraksi diterbitkan.",
        "audio": "Ia tahu kamu bernapas. Ia bertanya dulu apa yang kamu bawa.",
        "in_game_wiki": "Paket Sato-Ren membuktikan Deep Reach aktif pada 2190 tanpa perlu armada penyelamat di layar. Perusahaan kembali sebagai lalu lintas kepatuhan lewat jendela sinyal langka. Urutannya penting: koordinat, status akses Atlas, kustodi sampel, massa pembawa, pengakuan diam, lalu baru bahasa ekstraksi. Paket itu bisa menjadi bantuan, pengambilalihan, atau pemerasan tergantung bukti yang dipegang Marauder saat tiba.",
        "external_site": "Sato-Ren membawa Deep Reach kembali ke HECTON-8 dalam waktu kini: bukan mayat di arsip lama, melainkan paket hidup yang meminta kustodi sebelum menawarkan ekstraksi.",
        "external_site_article": "## Ekstraksi setelah kustodi\n\nPaket kembali Sato-Ren adalah dokumen akhir permainan dengan suhu kabel hidup. Ia tiba setelah pemain menemukan memo, buku, pengabaian, dan perintah tahan Deep Reach lama, lalu membuktikan perusahaan masih bisa berbicara lewat jendela Aegir yang sempit. Ia tidak datang sebagai kapal penyelamat. Ia datang sebagai urutan kepatuhan.\n\nUrutan itu adalah titik tekanannya. Paket meminta koordinat, status akses Atlas, kustodi sampel XO, estimasi massa pembawa, dan pengakuan diam kontraktor sebelum memakai bahasa ekstraksi. Meja penyelamatan bertanya di mana orang yang terjebak terluka. Sato-Ren bertanya apa yang dibawa orang itu, siapa yang bisa mengesahkan, dan apakah diam diterima sebagai syarat pergerakan.\n\nPaket yang sama bisa terlihat seperti bantuan, pengambilalihan, atau pemerasan. Itu bergantung pada bukti yang sudah ada di tangan Marauder: memo margin, pengabaian Atlas, tahanan karantina, buku kehilangan, bukti penerima. Sato-Ren menghubungkan birokrasi tenggelam dengan pilihan hidup. Jual koordinat, simpan bukti, putus Atlas, terbitkan buku, atau terus bergerak di samudra tempat penyelamatan pun punya nomor klaim.",
        "field_note": "Catatan Marauder: jika ekstraksi datang setelah kustodi, baca urutannya dua kali."
    },
    "ko_KR": {
        "localization_status": "draft_machine_or_llm",
        "title": "Sato-Ren 귀환 조치 패킷",
        "scanner": "회수 준수 패킷. 좌표, Atlas 접근 상태, XO 샘플 보관, 침묵 확인이 추출 문구보다 먼저 요구된다.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: 추출 언어 발행 전 좌표, Atlas 접근 상태, XO 샘플 보관, 운반자 질량 추정, 계약자 침묵 확인을 전송.",
        "audio": "그것은 네가 숨 쉬는 것을 안다. 먼저 무엇을 들고 있는지 묻는다.",
        "in_game_wiki": "Sato-Ren 패킷은 구조 함대를 화면에 띄우지 않고도 Deep Reach가 2190년에 활동 중임을 증명한다. 회사는 드문 신호 창구를 통한 준수 트래픽으로 돌아온다. 순서가 중요하다. 좌표, Atlas 접근 상태, 샘플 보관, 운반자 질량, 침묵 확인, 그리고 그 뒤에야 추출 언어. 패킷은 도착 시점에 마라우더가 어떤 증거를 쥐고 있느냐에 따라 도움, 회수, 협박이 될 수 있다.",
        "external_site": "Sato-Ren은 Deep Reach를 현재 시제로 HECTON-8에 되돌린다. 오래된 기록 속 시체가 아니라, 추출을 제안하기 전에 보관을 요구하는 살아 있는 패킷으로.",
        "external_site_article": "## 보관 이후의 추출\n\nSato-Ren 귀환 패킷은 살아 있는 전선의 온도를 가진 후반 문서다. 플레이어가 오래된 Deep Reach 메모, 장부, 면제서, 보류 명령을 찾은 뒤 도착하며, 회사가 아직 Aegir의 좁은 창구를 통해 말할 수 있음을 증명한다. 구조선으로 오지 않는다. 준수 절차로 온다.\n\n그 절차가 압박점이다. 패킷은 추출 언어를 쓰기 전에 좌표, Atlas 접근 상태, XO 샘플 보관, 운반자 질량 추정, 계약자 침묵 확인을 요구한다. 구조 데스크라면 갇힌 사람이 어디를 다쳤는지 묻는다. Sato-Ren은 그 사람이 무엇을 들고 있는지, 누가 인증할 수 있는지, 이동 조건으로 침묵을 받아들이는지 묻는다.\n\n같은 패킷은 도움, 회수, 협박처럼 보일 수 있다. 마라우더의 손에 이미 있는 증거에 달려 있다. 여유 메모, Atlas 면제서, 격리 보류, 손실 장부, 수신자 증거. Sato-Ren은 가라앉은 관료제를 살아 있는 선택과 연결한다. 좌표를 팔 것인가, 증거를 지킬 것인가, Atlas를 끊을 것인가, 장부를 공개할 것인가, 아니면 구조마저 청구 번호를 가진 바다를 계속 지나갈 것인가.",
        "field_note": "마라우더 메모: 추출이 보관 뒤에 온다면 순서를 두 번 읽어라."
    },
    "he_IL": {
        "localization_status": "draft_machine_or_llm",
        "title": "חבילת פעולת חזרה Sato-Ren",
        "scanner": "חבילת ציות לשחזור. קואורדינטות, מצב גישה Atlas, משמורת דגימת XO ואישור שתיקה נדרשים לפני שמופיעה שפת חילוץ.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: שלח קואורדינטות, מצב גישה Atlas, משמורת דגימת XO, אומדן מסת נשא ואישור שתיקת קבלן לפני הנפקת שפת חילוץ.",
        "audio": "זה יודע שאתה נושם. קודם הוא שואל מה אתה נושא.",
        "in_game_wiki": "חבילת Sato-Ren מוכיחה ש-Deep Reach פעילה בשנת 2190 בלי צורך בצי חילוץ על המסך. החברה חוזרת כתעבורת ציות דרך חלון אות נדיר. הסדר חשוב: קואורדינטות, מצב גישה Atlas, משמורת דגימה, מסת נשא, אישור שתיקה, ורק אז שפת חילוץ. החבילה יכולה להיות עזרה, תפיסה מחדש או סחיטה, תלוי אילו ראיות מחזיק ה-Marauder כשהיא מגיעה.",
        "external_site": "Sato-Ren מחזירה את Deep Reach ל-HECTON-8 בזמן הווה: לא כגופה בארכיונים ישנים, אלא כחבילה חיה שמבקשת משמורת לפני שהיא מציעה חילוץ.",
        "external_site_article": "## חילוץ אחרי משמורת\n\nחבילת החזרה Sato-Ren היא מסמך סוף משחק עם טמפרטורה של חוט חי. היא מגיעה אחרי שהשחקן מצא מזכרים, ספרים, כתבי ויתור וצווי החזקה ישנים של Deep Reach, ואז מוכיחה שהחברה עדיין יכולה לדבר דרך החלונות הצרים של Aegir. היא לא מגיעה כספינת חילוץ. היא מגיעה כרצף ציות.\n\nהרצף הזה הוא נקודת הלחץ. החבילה מבקשת קואורדינטות, מצב גישה Atlas, משמורת דגימת XO, אומדן מסת נשא ואישור שתיקת קבלן לפני שהיא משתמשת בשפת חילוץ. שולחן חילוץ שואל היכן האדם הלכוד פצוע. Sato-Ren שואלת מה האדם הלכוד נושא, מי יכול לאשר זאת, והאם שתיקה מתקבלת כתנאי לתנועה.\n\nאותה חבילה יכולה להיראות כמו עזרה, תפיסה מחדש או סחיטה. זה תלוי בראיות שכבר בידיו של ה-Marauder: מזכר המרווח, ויתור Atlas, החזקת ההסגר, ספר האובדן, ראיית המקבל. Sato-Ren מחברת את הביורוקרטיה הטבועה לבחירה חיה. למכור קואורדינטות, לשמר ראיה, לנתק את Atlas, לפרסם את הספר או להמשיך לנוע באוקיינוס שבו אפילו לחילוץ יש מספר תביעה.",
        "field_note": "הערת Marauder: אם חילוץ בא אחרי משמורת, קרא את הסדר פעמיים."
    },
    "pt_BR": {
        "localization_status": "draft_machine_or_llm",
        "title": "Pacote de ação de retorno Sato-Ren",
        "scanner": "Pacote de conformidade de recuperação. Coordenadas, estado de acesso Atlas, custódia de amostra XO e reconhecimento de silêncio são pedidos antes de aparecer linguagem de extração.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: transmitir coordenadas, estado de acesso Atlas, custódia de amostra XO, estimativa de massa do portador e reconhecimento de silêncio do contratado antes da linguagem de extração ser emitida.",
        "audio": "Ele sabe que você respira. Primeiro pergunta o que você carrega.",
        "in_game_wiki": "O pacote Sato-Ren prova que a Deep Reach está ativa em 2190 sem precisar mostrar uma frota de resgate. A empresa retorna como tráfego de conformidade por uma janela rara de sinal. A ordem importa: coordenadas, estado de acesso Atlas, custódia de amostra, massa do portador, reconhecimento de silêncio e só então linguagem de extração. O pacote pode ser ajuda, retomada ou chantagem dependendo das provas nas mãos do Marauder quando chega.",
        "external_site": "Sato-Ren traz a Deep Reach de volta a HECTON-8 no presente: não como cadáver em arquivos antigos, mas como pacote vivo que pede custódia antes de oferecer extração.",
        "external_site_article": "## Extração após custódia\n\nO pacote de retorno Sato-Ren é um documento de fim de jogo com temperatura de fio energizado. Ele chega depois que o jogador encontrou antigos memorandos, livros, renúncias e ordens de retenção da Deep Reach, e prova que a empresa ainda consegue falar pelas janelas estreitas de Aegir. Não chega como nave de resgate. Chega como sequência de conformidade.\n\nEssa sequência é o ponto de pressão. O pacote pede coordenadas, estado de acesso Atlas, custódia de amostra XO, estimativa de massa do portador e reconhecimento de silêncio do contratado antes de usar linguagem de extração. Uma mesa de resgate pergunta onde a pessoa presa está ferida. Sato-Ren pergunta o que ela carrega, quem pode certificar e se o silêncio é aceito como condição de movimento.\n\nO mesmo pacote pode parecer ajuda, retomada ou chantagem. Depende das provas já nas mãos do Marauder: memorando de margem, renúncia Atlas, retenção de quarentena, livro de perdas, prova de receptor. Sato-Ren conecta a burocracia afogada a uma escolha viva. Vender coordenadas, preservar prova, cortar Atlas, publicar o livro ou continuar andando por um oceano onde até resgate tem número de reivindicação.",
        "field_note": "Nota de Marauder: se extração vem depois de custódia, leia a ordem duas vezes."
    },
    "nl_NL": {
        "localization_status": "draft_machine_or_llm",
        "title": "Sato-Ren-terugkeeractiepakket",
        "scanner": "Recovery-compliancepakket. Coördinaten, Atlas-toegangsstatus, XO-monsterbewaring en stilzwijgbevestiging worden gevraagd voordat extractie verschijnt.",
        "terminal": "RECOVERY COMPLIANCE / SATO-REN / RETURN ACTION: verzend coördinaten, Atlas-toegangsstatus, XO-monsterbewaring, dragermassaschatting en stilzwijgbevestiging van contractant vóór uitgifte van extractietaal.",
        "audio": "Het weet dat je ademt. Eerst vraagt het wat je draagt.",
        "in_game_wiki": "Het Sato-Ren-pakket bewijst dat Deep Reach actief is in 2190 zonder een reddingsvloot op het scherm. Het bedrijf keert terug als complianceverkeer door een zeldzaam signaalvenster. De volgorde telt: coördinaten, Atlas-toegangsstatus, monsterbewaring, dragermassa, stilzwijgbevestiging en pas dan extractietaal. Het pakket kan hulp, terugname of chantage zijn, afhankelijk van het bewijs dat de Marauder vasthoudt wanneer het aankomt.",
        "external_site": "Sato-Ren brengt Deep Reach in de tegenwoordige tijd terug naar HECTON-8: niet als lijk in oude archieven, maar als levend pakket dat bewaring vraagt voordat het extractie aanbiedt.",
        "external_site_article": "## Extractie na bewaring\n\nHet Sato-Ren-terugkeerpakket is een laat document met de temperatuur van een stroomdraad. Het komt nadat de speler oude Deep Reach-memo's, boeken, verklaringen van afstand en holdorders heeft gevonden, en bewijst daarna dat het bedrijf nog steeds door Aegirs smalle vensters kan spreken. Het komt niet als reddingsschip. Het komt als compliancesequentie.\n\nDie sequentie is het drukpunt. Het pakket vraagt om coördinaten, Atlas-toegangsstatus, XO-monsterbewaring, dragermassaschatting en stilzwijgbevestiging van de contractant voordat het extractietaal gebruikt. Een reddingsdesk vraagt waar de opgesloten persoon gewond is. Sato-Ren vraagt wat die persoon draagt, wie het kan certificeren en of stilte als voorwaarde voor verplaatsing wordt geaccepteerd.\n\nHetzelfde pakket kan op hulp, terugname of chantage lijken. Dat hangt af van het bewijs dat al in handen van de Marauder is: de margememo, de Atlas-verklaring, de quarantainehold, het verliesboek, het ontvangerbewijs. Sato-Ren verbindt de verdronken bureaucratie met een levende keuze. Coördinaten verkopen, bewijs bewaren, Atlas losmaken, het boek publiceren of blijven bewegen door een oceaan waar zelfs redding een claimnummer heeft.",
        "field_note": "Marauder-notitie: als extractie na bewaring komt, lees de volgorde twee keer."
    }
}


def main():
    data = json.loads(PATH.read_text(encoding="utf-8"))
    found = False
    for packet in data["packets"]:
        if packet["packet_id"] == PACKET_ID:
            packet["localized"] = {locale: dict(row) for locale, row in LOCALIZED.items()}
            found = True
            break
    if not found:
        raise SystemExit(f"Missing packet: {PACKET_ID}")
    PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

from __future__ import annotations

import json
import re
from pathlib import Path


root = Path(__file__).resolve().parents[1]
packet_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS129_FIRST_SURVIVAL_ARTICLE_SURFACES.packets.json"
manifest_path = root / "Docs" / "Lore" / "AppliedContent" / "release_sets" / "RS129_FIRST_SURVIVAL_ARTICLE_SURFACES_manifest.json"

updates = {
    "P625_SHALLOW_ANNEX_P63_PUMP_ROOM_ARTICLE": {
        "en_US": {
            "external_site_article": "Shallow Annex P-63 is the first room that can become useful again. The pump room is not a reward chamber. It is a machine with tired parts: a manual bilge crank, a stuck valve throat, a cold-sealant clamp scar and a field fabricator that will only honor low-risk repair work until water drops below the intake line.\n\nThe room teaches the first rule of HECTON-8 salvage: air comes from repair, not luck. A working pump clears the ankle-deep flood, exposes old tool lockers and gives the player a physical reason to trust the annex for a few more minutes. The terminal still calls the task minor water intrusion. The walls say otherwise."
        },
        "ru_RU": {
            "title": "Мелководный аннекс P-63: насосная",
            "scanner": "ВПУСК НАСОСНОЙ // Забит, но обслуживаем. Ручной трюмный маршрут доступен. Сначала осушить, потом резать.",
            "terminal": "ОБСЛУЖИВАНИЕ P-63 // Незначительное затопление зарегистрировано. Ручная откачка принята. Полевой фабрикатор держит очередь прокладок, хомутов и контактного резака, пока впуск не очистится.",
            "audio": "Крути насос, пока пол не ответит. Если начнешь резать первым, затопишь шкаф с инструментами.",
            "in_game_wiki": "P-63 становится полезным только после ремонта насосной. Ручная откачка снижает воду, открывает старые шкафы с инструментами и держит полевой фабрикатор внутри безопасного окна ремонтных полномочий.",
            "external_site": "Мелководный аннекс P-63 - первая комната, которую можно вернуть к пользе. Это не награда, а уставшая машина: ручная трюмная рукоять, заевшая горловина клапана, след холодного герметика и фабрикатор, который разрешает только низкорисковый ремонт, пока вода не опустится ниже линии впуска.",
            "field_note": "Сухой угол - не дом. Это десять минут на мысль. Возьми их.",
        },
        "ja_JP": {
            "title": "浅層別棟P-63 ポンプ室",
            "scanner": "ポンプ室取水口 // 詰まりあり、整備可能。手動ビルジ経路使用可。先に排水、次に切断。",
            "terminal": "P-63保守 // 軽微な浸水を記録。手動ビルジ承認。取水口が通るまで、フィールドファブリケータはガスケット、クランプ、接触カッターの低リスク修理キューに固定。",
            "audio": "床が返事をするまでポンプを回せ。先に切れば、工具ロッカーを沈める。",
            "in_game_wiki": "P-63はポンプ室を直してから初めて役に立つ。手動排水で水位を下げ、古い工具ロッカーを露出させ、フィールドファブリケータを安全な修理権限の範囲に保つ。",
            "external_site": "浅層別棟P-63は、最初に再び使えるようにできる部屋だ。報酬部屋ではなく、疲れた機械である。手動ビルジクランク、固着した弁喉、冷間シーラントのクランプ痕、そして水が取水線より下がるまで低リスク修理しか認めないフィールドファブリケータがある。",
            "field_note": "乾いた隅は家ではない。考えるための十分だ。使え。",
        },
        "zh_CN": {
            "title": "浅层附属舱 P-63 泵房",
            "scanner": "泵房进水口 // 堵塞但可维修。手动舱底排水路线可用。先排水，再切割。",
            "terminal": "P-63 维护 // 轻微进水已记录。手动舱底泵接受。进水口清空前，现场制造器仅开放垫圈、夹具和接触切割器队列。",
            "audio": "转动泵，直到地板给出回应。先切割的话，工具柜会被你淹掉。",
            "in_game_wiki": "P-63 只有在泵房修好后才有用。手动抽水会降低积水，露出旧工具柜，并让现场制造器保持在安全维修权限窗口内。",
            "external_site": "浅层附属舱 P-63 是第一个能重新变得有用的房间。它不是奖励室，而是一台疲惫的机器：手动舱底曲柄、卡住的阀喉、冷密封夹痕，以及在水位降到进水线以下前只接受低风险维修的现场制造器。",
            "field_note": "干燥的角落不是家。它只是十分钟的思考时间。拿下它。",
        },
        "fr_FR": {
            "title": "Salle des pompes de l’annexe peu profonde P-63",
            "scanner": "PRISE DE POMPE // Bloquée mais réparable. Route de cale manuelle disponible. Drainer d’abord, couper ensuite.",
            "terminal": "MAINTENANCE P-63 // Intrusion d’eau mineure consignée. Cale manuelle acceptée. Fabricateur de terrain limité aux joints, colliers et coupe-contact jusqu’au dégagement de la prise.",
            "audio": "Tourne la pompe jusqu’à ce que le sol réponde. Si tu coupes d’abord, tu noies le casier à outils.",
            "in_game_wiki": "P-63 ne devient utile qu’après réparation de la salle des pompes. Le pompage manuel baisse l’eau, révèle d’anciens casiers à outils et maintient le fabricateur dans une fenêtre d’autorité de réparation sûre.",
            "external_site": "L’annexe peu profonde P-63 est la première pièce qui peut redevenir utile. Ce n’est pas une salle de récompense, mais une machine fatiguée: manivelle de cale, gorge de vanne bloquée, cicatrice de collier au mastic froid et fabricateur qui n’accepte que les travaux de réparation à faible risque tant que l’eau reste au-dessus de la ligne d’admission.",
            "field_note": "Un coin sec n’est pas une maison. C’est dix minutes pour réfléchir. Prends-les.",
        },
        "es_ES": {
            "title": "Sala de bombas del anexo somero P-63",
            "scanner": "TOMA DE BOMBA // Bloqueada pero reparable. Ruta de achique manual disponible. Drena primero, corta después.",
            "terminal": "MANTENIMIENTO P-63 // Intrusión menor de agua registrada. Achique manual aceptado. Fabricador de campo limitado a juntas, abrazaderas y cortador de contacto hasta despejar la toma.",
            "audio": "Gira la bomba hasta que el suelo responda. Si cortas primero, inundas el armario de herramientas.",
            "in_game_wiki": "P-63 solo sirve después de reparar la sala de bombas. El bombeo manual baja el agua, expone viejos armarios de herramientas y mantiene el fabricador dentro de una ventana segura de autoridad de reparación.",
            "external_site": "El anexo somero P-63 es la primera sala que puede volver a ser útil. No es una cámara de recompensa, sino una máquina cansada: manivela de achique, garganta de válvula atascada, cicatriz de sellante frío y un fabricador que solo acepta reparaciones de bajo riesgo hasta que el agua baja de la línea de entrada.",
            "field_note": "Un rincón seco no es un hogar. Son diez minutos para pensar. Tómalos.",
        },
        "de_DE": {
            "title": "Pumpenraum des Flachannex P-63",
            "scanner": "PUMPENRAUM-EINLASS // Blockiert, aber wartbar. Manuelle Bilgeroute verfügbar. Erst lenzen, dann schneiden.",
            "terminal": "P-63 WARTUNG // Geringer Wassereintritt protokolliert. Manuelle Bilge akzeptiert. Feldfabrikator bleibt auf Dichtung, Klemme und Kontakt-Schneider gesperrt, bis der Einlass frei ist.",
            "audio": "Kurble die Pumpe, bis der Boden antwortet. Wenn du zuerst schneidest, flutest du den Werkzeugschrank.",
            "in_game_wiki": "P-63 wird erst nach der Reparatur des Pumpenraums nützlich. Manuelles Pumpen senkt das Wasser, legt alte Werkzeugschränke frei und hält den Feldfabrikator in einem sicheren Reparaturbefugnisfenster.",
            "external_site": "Der Flachannex P-63 ist der erste Raum, der wieder brauchbar werden kann. Er ist keine Belohnungskammer, sondern eine müde Maschine: manuelle Bilgenkurbel, festsitzender Ventilhals, Kalt-Dichtmittelklemme und ein Feldfabrikator, der nur risikoarme Reparaturen akzeptiert, bis das Wasser unter die Einlasslinie fällt.",
            "field_note": "Eine trockene Ecke ist kein Zuhause. Es sind zehn Minuten Denkzeit. Nimm sie.",
        },
        "pl_PL": {
            "title": "Pompownia płytkiego aneksu P-63",
            "scanner": "WLOT POMPOWNI // Zablokowany, ale sprawny po serwisie. Dostępna ręczna trasa zęzowa. Najpierw osusz, potem tnij.",
            "terminal": "KONSERWACJA P-63 // Zarejestrowano niewielki napływ wody. Ręczna zęza przyjęta. Fabrykator polowy trzyma kolejkę uszczelek, obejm i noża kontaktowego, dopóki wlot się nie oczyści.",
            "audio": "Kręć pompą, aż podłoga odpowie. Jeśli najpierw zaczniesz ciąć, zalejesz szafkę z narzędziami.",
            "in_game_wiki": "P-63 staje się użyteczny dopiero po naprawie pompowni. Ręczne pompowanie obniża wodę, odsłania stare szafki z narzędziami i utrzymuje fabrykator w bezpiecznym oknie uprawnień naprawczych.",
            "external_site": "Płytki aneks P-63 jest pierwszym pomieszczeniem, które może znów działać. To nie komora nagrody, tylko zmęczona maszyna: ręczna korba zęzowa, zacięta gardziel zaworu, ślad zimnego szczeliwa i fabrykator, który uznaje tylko niskoryzykowne naprawy, zanim woda spadnie poniżej linii wlotu.",
            "field_note": "Suchy kąt nie jest domem. To dziesięć minut na myślenie. Bierz je.",
        },
        "uk_UA": {
            "title": "Насосна мілководного анекса P-63",
            "scanner": "ВПУСК НАСОСНОЇ // Забитий, але придатний до сервісу. Ручний трюмний маршрут доступний. Спершу осушити, потім різати.",
            "terminal": "ОБСЛУГОВУВАННЯ P-63 // Незначне проникнення води зареєстровано. Ручну відкачку прийнято. Польовий фабрикатор тримає чергу прокладок, хомутів і контактного різака, доки впуск не очиститься.",
            "audio": "Крути насос, доки підлога не відповість. Якщо різати першим, затопиш шафу з інструментами.",
            "in_game_wiki": "P-63 стає корисним лише після ремонту насосної. Ручна відкачка знижує воду, відкриває старі шафи з інструментами й тримає польовий фабрикатор у безпечному вікні ремонтних повноважень.",
            "external_site": "Мілководний анекс P-63 - перша кімната, яку можна повернути до користі. Це не кімната винагороди, а втомлена машина: ручна трюмна рукоять, заїла горловина клапана, слід холодного герметика і фабрикатор, що визнає лише малоризиковий ремонт, поки вода не впаде нижче лінії впуску.",
            "field_note": "Сухий кут - не дім. Це десять хвилин на думку. Забери їх.",
        },
        "ar_SA": {
            "title": "غرفة مضخة الملحق الضحل P-63",
            "scanner": "مدخل غرفة المضخة // مسدود لكنه قابل للخدمة. مسار نزح يدوي متاح. صرّف أولا، واقطع ثانيا.",
            "terminal": "صيانة P-63 // تم تسجيل تسرب ماء طفيف. النزح اليدوي مقبول. المصنع الميداني مقفل على طابور الحشيات والمشابك وقاطع التماس حتى ينفتح المدخل.",
            "audio": "أدر المضخة حتى يجيبك floor. إذا قطعت أولا، ستغرق خزانة الأدوات.",
            "in_game_wiki": "لا يصبح P-63 مفيدا إلا بعد إصلاح غرفة المضخة. النزح اليدوي يخفض الماء، يكشف خزائن الأدوات القديمة، ويبقي المصنع الميداني داخل نافذة صلاحية إصلاح آمنة.",
            "external_site": "الملحق الضحل P-63 هو أول غرفة يمكن جعلها نافعة من جديد. ليست غرفة مكافأة، بل آلة متعبة: ذراع نزح يدوي، حلق صمام عالق، أثر مشبك مانع تسرب بارد، ومصنع ميداني لا يقبل إلا أعمال إصلاح قليلة الخطر حتى يهبط الماء تحت خط المدخل.",
            "field_note": "زاوية جافة ليست بيتا. إنها عشر دقائق للتفكير. خذها.",
        },
        "id_ID": {
            "title": "Ruang Pompa Aneks Dangkal P-63",
            "scanner": "INTAKE RUANG POMPA // Tersumbat tapi bisa diservis. Rute bilge manual tersedia. Keringkan dulu, potong kemudian.",
            "terminal": "PERAWATAN P-63 // Intrusi air kecil tercatat. Bilge manual diterima. Fabricator lapangan terkunci ke antrean gasket, clamp, dan contact-cutter sampai intake bersih.",
            "audio": "Putar pompa sampai lantai menjawab. Kalau kamu memotong dulu, loker alat akan banjir.",
            "in_game_wiki": "P-63 baru berguna setelah ruang pompanya diperbaiki. Pemompaan manual menurunkan banjir, membuka loker alat lama, dan menjaga fabricator lapangan tetap dalam jendela otoritas perbaikan aman.",
            "external_site": "Aneks Dangkal P-63 adalah ruang pertama yang bisa dibuat berguna lagi. Ini bukan ruang hadiah, melainkan mesin lelah: engkol bilge manual, leher katup macet, bekas clamp sealant dingin, dan fabricator yang hanya mengakui perbaikan berisiko rendah sampai air turun di bawah garis intake.",
            "field_note": "Sudut kering bukan rumah. Itu sepuluh menit untuk berpikir. Ambil.",
        },
        "ko_KR": {
            "title": "얕은 부속구역 P-63 펌프실",
            "scanner": "펌프실 흡입구 // 막혔지만 정비 가능. 수동 빌지 경로 사용 가능. 먼저 배수하고, 그다음 절단.",
            "terminal": "P-63 정비 // 경미한 침수 기록. 수동 빌지 승인. 흡입구가 뚫릴 때까지 현장 제작기는 개스킷, 클램프, 접촉 절단기 대기열로 제한.",
            "audio": "바닥이 대답할 때까지 펌프를 돌려. 먼저 자르면 도구 보관함을 물에 잠기게 한다.",
            "in_game_wiki": "P-63은 펌프실을 수리한 뒤에야 쓸모가 생긴다. 수동 펌프질은 물을 낮추고, 오래된 도구 보관함을 드러내며, 현장 제작기를 안전한 수리 권한 창 안에 묶어 둔다.",
            "external_site": "얕은 부속구역 P-63은 다시 쓸모 있게 만들 수 있는 첫 방이다. 보상실이 아니라 지친 기계다. 수동 빌지 크랭크, 걸린 밸브 목, 차가운 실런트 클램프 흉터, 그리고 물이 흡입선 아래로 내려갈 때까지 저위험 수리만 허용하는 현장 제작기가 있다.",
            "field_note": "마른 구석은 집이 아니다. 생각할 십 분이다. 가져라.",
        },
        "he_IL": {
            "title": "חדר המשאבות של נספח רדוד P-63",
            "scanner": "פתח חדר משאבות // חסום אך ניתן לשירות. נתיב בילג ידני זמין. קודם לנקז, אחר כך לחתוך.",
            "terminal": "תחזוקת P-63 // חדירת מים קלה נרשמה. בילג ידני אושר. המייצר השטחי נעול לתור אטמים, מהדקים וחותך מגע עד שפתח היניקה יתנקה.",
            "audio": "סובב את המשאבה עד שהרצפה עונה. אם תחתוך קודם, תטביע את ארון הכלים.",
            "in_game_wiki": "P-63 נעשה שימושי רק אחרי תיקון חדר המשאבות. שאיבה ידנית מורידה את המים, חושפת ארונות כלים ישנים ושומרת את המייצר בתוך חלון סמכות תיקון בטוח.",
            "external_site": "נספח רדוד P-63 הוא החדר הראשון שאפשר להחזיר לתועלת. זה אינו חדר פרס אלא מכונה עייפה: ידית בילג ידנית, גרון שסתום תקוע, צלקת מהדק איטום קר ומייצר שמכבד רק עבודות תיקון בסיכון נמוך עד שהמים יורדים מתחת לקו היניקה.",
            "field_note": "פינה יבשה אינה בית. אלה עשר דקות לחשוב. קח אותן.",
        },
        "pt_BR": {
            "title": "Sala de bombas do anexo raso P-63",
            "scanner": "ENTRADA DA BOMBA // Bloqueada, mas reparável. Rota de porão manual disponível. Drene primeiro, corte depois.",
            "terminal": "MANUTENÇÃO P-63 // Pequena entrada de água registrada. Porão manual aceito. Fabricador de campo travado na fila de junta, grampo e cortador de contato até a entrada limpar.",
            "audio": "Gire a bomba até o piso responder. Se cortar primeiro, você alaga o armário de ferramentas.",
            "in_game_wiki": "O P-63 só se torna útil depois que a sala de bombas é reparada. A bomba manual baixa a água, expõe armários antigos e mantém o fabricador dentro de uma janela segura de autoridade de reparo.",
            "external_site": "O anexo raso P-63 é o primeiro cômodo que pode voltar a ser útil. Não é sala de recompensa, mas uma máquina cansada: manivela de porão, garganta de válvula presa, marca de selante frio e um fabricador que só aceita reparos de baixo risco até a água cair abaixo da linha de entrada.",
            "field_note": "Um canto seco não é casa. São dez minutos para pensar. Pegue.",
        },
        "nl_NL": {
            "title": "Pompkamer van Ondiep Annex P-63",
            "scanner": "POMPKAMER-INLAAT // Geblokkeerd maar onderhoudbaar. Handmatige bilgeroute beschikbaar. Eerst draineren, dan snijden.",
            "terminal": "P-63 ONDERHOUD // Kleine waterindringing gelogd. Handmatige bilge geaccepteerd. Veldfabricator blijft op pakking, klem en contactsnijder tot de inlaat vrij is.",
            "audio": "Draai de pomp tot de vloer antwoordt. Als je eerst snijdt, zet je de gereedschapskast onder water.",
            "in_game_wiki": "P-63 wordt pas bruikbaar nadat de pompkamer is gerepareerd. Handmatig pompen verlaagt het water, legt oude gereedschapskasten bloot en houdt de veldfabricator binnen een veilig reparatiebevoegdheidsvenster.",
            "external_site": "Ondiep Annex P-63 is de eerste kamer die weer nuttig kan worden. Het is geen beloningskamer maar een vermoeide machine: handmatige bilgekruk, vastzittende klephals, koud-sealant klemspoor en een fabricator die alleen laag-risico reparaties accepteert tot het water onder de inlaatlijn zakt.",
            "field_note": "Een droge hoek is geen thuis. Het zijn tien minuten denktijd. Neem ze.",
        },
    },
}

base_packets["P626_BLACK_KEEL_WINDOW_PRICE_ARTICLE"].update(
    {
        "ja_JP": (
            "Black Keel ウィンドウ価格",
            "中継マスト // 弱い軌道ウィンドウに整列。天候ノイズが上がる前に一つのパケットを選べ。",
            "接触バッファ // オペレーター生存、請求未決、回収はウィンドウ待ち。抽出予定の前にサンプル状態と座標信頼度を要求。",
            "ウィンドウは一回のバーストだけ澄んでいる。居場所を言うか、見つけたものを言え。",
            "Black Keelは生存を確認できても、即時救助を認めるとは限らない。強い軌道ウィンドウは一つの有用なパケットだけを運び、その価値は請求台帳が決める。",
            "Black Keelは助ける前に返事ができる。tenderはAegir系内にいるが、有効な行動はまだ天候、月の幾何、中継整列、交通優先度、隔離枠、慈悲のために書かれていない請求台帳に縛られる。",
            "carrierがあなたの呼吸をパケット単位で値付けするなら、気にかけているかを聞くためにそのパケットを捨てるな。",
        ),
        "fr_FR": (
            "Prix de fenêtre Black Keel",
            "MÂT RELAIS // Aligné sur une faible fenêtre orbitale. Choisir un paquet avant la montée du bruit météo.",
            "TAMPON CONTACT // Opérateur vivant; réclamation ouverte; récupération en attente de fenêtre. État échantillon et confiance coordonnées requis avant planification extraction.",
            "La fenêtre est propre pour un seul burst. Dis où tu es, ou ce que tu as trouvé.",
            "Black Keel peut confirmer la survie sans accorder de sauvetage immédiat. Une bonne fenêtre orbitale porte un seul paquet utile, et le registre de réclamation décide sa valeur.",
            "Black Keel peut répondre avant de pouvoir aider. Le tender est dans le système d’Aegir, mais toute action utile dépend encore de la météo, de la géométrie lunaire, de l’alignement relais, des priorités de trafic, des places de quarantaine et d’un registre de réclamation qui n’a pas été écrit pour la pitié.",
            "Quand un carrier chiffre ton souffle au paquet, ne gaspille pas le paquet à demander s’il s’en soucie.",
        ),
        "es_ES": (
            "Precio de ventana de Black Keel",
            "MÁSTIL RELÉ // Alineado con ventana orbital débil. Elige un paquete antes de que suba el ruido meteorológico.",
            "BÚFER DE CONTACTO // Operador vivo; reclamación abierta; recuperación pendiente de ventana. Estado de muestra y confianza de coordenadas requeridos antes de programar extracción.",
            "La ventana está limpia para un solo burst. Di dónde estás, o di qué encontraste.",
            "Black Keel puede confirmar supervivencia sin conceder rescate inmediato. Una ventana orbital fuerte lleva un paquete útil, y el libro de reclamación decide cuánto vale.",
            "Black Keel puede responder antes de ayudar. El tender está en el sistema Aegir, pero toda acción útil depende todavía del clima, la geometría lunar, la alineación del relé, la prioridad de tráfico, los cupos de cuarentena y un libro de reclamaciones no escrito para la misericordia.",
            "Cuando un carrier pone precio a tu respiración por paquete, no malgastes el paquete preguntando si le importas.",
        ),
        "pl_PL": (
            "Cena okna Black Keel",
            "MASZT PRZEKAŹNIKA // Wyrównany do słabego okna orbitalnego. Wybierz jeden pakiet, zanim szum pogody wzrośnie.",
            "BUFOR KONTAKTU // Operator żyje; roszczenie otwarte; odzysk czeka na okno. Stan próbki i pewność współrzędnych wymagane przed harmonogramem ekstrakcji.",
            "Okno jest czyste na jeden burst. Powiedz, gdzie jesteś, albo co znalazłeś.",
            "Black Keel może potwierdzić przeżycie bez natychmiastowego ratunku. Silne okno orbitalne niesie jeden użyteczny pakiet, a księga roszczeń decyduje, ile jest wart.",
            "Black Keel może odpowiedzieć, zanim może pomóc. Tender jest w systemie Aegir, ale użyteczne działanie nadal zależy od pogody, geometrii księżyców, ustawienia przekaźnika, priorytetu ruchu, miejsc kwarantanny i księgi roszczeń, której nie pisano dla litości.",
            "Gdy carrier wycenia twój oddech w pakietach, nie marnuj pakietu na pytanie, czy go obchodzisz.",
        ),
        "uk_UA": (
            "Ціна вікна Black Keel",
            "РЕЛЕЙНА ЩОГЛА // Вирівняна на слабке орбітальне вікно. Обери один пакет, доки погодний шум не зріс.",
            "БУФЕР КОНТАКТУ // Оператор живий; претензія відкрита; повернення чекає вікна. Стан зразка й довіру координат запитано перед плануванням евакуації.",
            "Вікно чисте на один burst. Скажи, де ти, або що знайшов.",
            "Black Keel може підтвердити виживання, не надаючи негайного порятунку. Сильне орбітальне вікно несе один корисний пакет, а реєстр претензії вирішує його вартість.",
            "Black Keel може відповісти раніше, ніж допомогти. Tender у системі Aegir, але корисна дія все ще залежить від погоди, геометрії місяців, вирівнювання реле, пріоритету трафіку, карантинних слотів і реєстру претензій, написаного не для милосердя.",
            "Коли carrier оцінює твоє дихання пакетами, не витрачай пакет на питання, чи йому не байдуже.",
        ),
        "ar_SA": (
            "ثمن نافذة Black Keel",
            "سارية ترحيل // مضبوطة على نافذة مدارية ضعيفة. اختر حزمة واحدة قبل أن يرتفع ضجيج الطقس.",
            "مخزن اتصال // المشغل حي؛ المطالبة مفتوحة؛ الاسترداد ينتظر نافذة. حالة العينة وثقة الإحداثيات مطلوبتان قبل جدولة الاستخراج.",
            "النافذة نظيفة لدفعة واحدة. قل أين أنت، أو قل ماذا وجدت.",
            "يمكن لـ Black Keel تأكيد النجاة من دون منح إنقاذ فوري. النافذة المدارية القوية تحمل حزمة مفيدة واحدة، وسجل المطالبة يقرر قيمتها.",
            "يمكن لـ Black Keel أن يجيب قبل أن يساعد. الـ tender داخل نظام Aegir، لكن أي فعل مفيد يبقى مرهونا بالطقس، وهندسة الأقمار، ومحاذاة المرحل، وأولوية المرور، وخانات الحجر، وسجل مطالبات لم يكتب من أجل الرحمة.",
            "عندما يسعر carrier أنفاسك بالحزمة، لا تهدر الحزمة في السؤال هل يهتم.",
        ),
        "id_ID": (
            "Harga Jendela Black Keel",
            "TIANG RELAI // Selaras ke jendela orbit lemah. Pilih satu paket sebelum derau cuaca naik.",
            "BUFFER KONTAK // Operator hidup; klaim terbuka; pemulihan menunggu jendela. Status sampel dan keyakinan koordinat diminta sebelum jadwal ekstraksi.",
            "Jendela bersih untuk satu burst. Katakan di mana kamu, atau katakan apa yang kamu temukan.",
            "Black Keel bisa mengonfirmasi kamu hidup tanpa memberi penyelamatan langsung. Satu jendela orbit kuat membawa satu paket berguna, dan ledger klaim menentukan nilainya.",
            "Black Keel bisa menjawab sebelum bisa membantu. Tender ada di sistem Aegir, tetapi tindakan berguna masih bergantung pada cuaca, geometri bulan, keselarasan relai, prioritas lalu lintas, slot karantina, dan ledger klaim yang tidak ditulis untuk belas kasihan.",
            "Saat carrier menilai napasmu per paket, jangan buang paket untuk bertanya apakah ia peduli.",
        ),
        "ko_KR": (
            "Black Keel 창구 가격",
            "릴레이 마스트 // 약한 궤도 창에 정렬. 기상 잡음이 오르기 전에 패킷 하나를 선택.",
            "접촉 버퍼 // 오퍼레이터 생존, 클레임 개방, 회수는 창 대기. 추출 일정 전 샘플 상태와 좌표 신뢰도 요청.",
            "창은 한 번의 burst 동안 깨끗하다. 네 위치를 말하거나, 무엇을 찾았는지 말해.",
            "Black Keel은 생존을 확인할 수 있지만 즉시 구조를 허가하지는 않는다. 강한 궤도 창은 유용한 패킷 하나만 실어 나르고, 그 가치는 클레임 장부가 결정한다.",
            "Black Keel은 도울 수 있기 전에 먼저 답할 수 있다. tender는 Aegir계 안에 있지만, 유용한 행동은 여전히 날씨, 달의 기하, 릴레이 정렬, 교통 우선순위, 격리 슬롯, 그리고 자비를 위해 쓰이지 않은 클레임 장부에 묶여 있다.",
            "carrier가 네 숨을 패킷 단위로 가격 매긴다면, 관심이 있는지 묻는 데 패킷을 낭비하지 마라.",
        ),
        "he_IL": (
            "מחיר החלון של Black Keel",
            "תורן ממסר // מיושר לחלון מסלולי חלש. בחר חבילה אחת לפני שרעש מזג האוויר עולה.",
            "מאגר קשר // מפעיל חי; תביעה פתוחה; חילוץ ממתין לחלון. מצב דגימה וביטחון קואורדינטות נדרשים לפני תזמון חילוץ.",
            "החלון נקי ל-burst אחד. תגיד איפה אתה, או תגיד מה מצאת.",
            "Black Keel יכולה לאשר הישרדות בלי להעניק חילוץ מיידי. חלון מסלולי חזק נושא חבילה שימושית אחת, ופנקס התביעה מחליט כמה היא שווה.",
            "Black Keel יכולה לענות לפני שהיא יכולה לעזור. ה-tender נמצא במערכת Aegir, אבל כל פעולה שימושית עדיין תלויה במזג אוויר, גאומטריית ירחים, יישור ממסר, עדיפות תנועה, מקומות הסגר ופנקס תביעות שלא נכתב לרחמים.",
            "כש-carrier מתמחר את הנשימה שלך לפי חבילה, אל תבזבז חבילה על השאלה אם אכפת לו.",
        ),
        "pt_BR": (
            "Preço da janela Black Keel",
            "MASTRO RELÉ // Alinhado a janela orbital fraca. Escolha um pacote antes que o ruído meteorológico suba.",
            "BUFFER DE CONTATO // Operador vivo; reivindicação aberta; recuperação aguardando janela. Estado de amostra e confiança de coordenadas solicitados antes de agendar extração.",
            "A janela está limpa para um burst. Diga onde você está, ou diga o que achou.",
            "A Black Keel pode confirmar sobrevivência sem conceder resgate imediato. Uma janela orbital forte carrega um pacote útil, e o livro de reivindicação decide quanto ele vale.",
            "A Black Keel pode responder antes de ajudar. O tender está no sistema Aegir, mas toda ação útil ainda depende de clima, geometria das luas, alinhamento de relé, prioridade de tráfego, vagas de quarentena e um livro de reivindicação que não foi escrito para misericórdia.",
            "Quando um carrier precifica sua respiração por pacote, não desperdice o pacote perguntando se ele se importa.",
        ),
        "nl_NL": (
            "Black Keel-vensterprijs",
            "RELAISMAST // Uitgelijnd op zwak orbitaal venster. Kies één pakket voordat weerruis stijgt.",
            "CONTACTBUFFER // Operator leeft; claim open; berging wacht op venster. Monsterstatus en coördinatenvertrouwen gevraagd vóór extractieplanning.",
            "Het venster is schoon voor één burst. Zeg waar je bent, of zeg wat je vond.",
            "Black Keel kan overleven bevestigen zonder directe redding te verlenen. Een sterk orbitaal venster draagt één bruikbaar pakket, en het claimledger beslist wat dat pakket waard is.",
            "Black Keel kan antwoorden voordat het kan helpen. De tender is in het Aegir-systeem, maar nuttige actie hangt nog steeds af van weer, maangeometrie, relaisuitlijning, verkeersprioriteit, quarantaineslots en een claimledger dat niet voor genade is geschreven.",
            "Als een carrier je adem per pakket prijst, verspil dat pakket dan niet met vragen of het hem iets kan schelen.",
        ),
    }
)

base_packets = {
    "P626_BLACK_KEEL_WINDOW_PRICE_ARTICLE": {
        "en_US": {
            "external_site_article": "Black Keel can answer before it can help. That is what makes the first contact cruel. The tender is in the Aegir system, but every useful action still depends on weather, moon geometry, relay alignment, traffic priority, quarantine slots and a claim ledger that was not written for mercy.\n\nA strong window lets the player send one good packet. Medical state, damage state, sample state and coordinates compete for the same burst. The carrier's first useful question can be colder than a failure tone: what did you recover, and is the claim still open?"
        },
        "ru_RU": ("Цена окна Black Keel", "РЕЛЕЙНАЯ МАЧТА // Выставлена на слабое орбитальное окно. Выбери один пакет, пока погодный шум не вырос.", "БУФЕР КОНТАКТА // Оператор жив; претензия открыта; эвакуация ждет окна. Перед расписанием подъема запрошены состояние образца и уверенность координат.", "Окно чистое на один burst. Скажи, где ты, или что нашел.", "Black Keel может подтвердить выживание, не давая немедленного спасения. Сильное орбитальное окно несет один полезный пакет, а реестр претензии решает, сколько этот пакет стоит.", "Black Keel может ответить раньше, чем помочь. Tender находится в системе Aegir, но действие все еще зависит от погоды, геометрии лун, выравнивания реле, очереди трафика, карантинных слотов и реестра претензий, который не писали ради милосердия.", "Если carrier оценивает твое дыхание пакетами, не трать пакет на вопрос, есть ли ему дело."),
        "zh_CN": ("Black Keel 窗口价格", "中继桅杆 // 已对准微弱轨道窗口。天气噪声爬升前选择一个数据包。", "联系缓冲 // 操作员存活；索赔开放；回收等待窗口。安排撤离前请求样本状态和坐标置信度。", "窗口只够一次清晰 burst。说你在哪里，或者说你找到了什么。", "Black Keel 可以确认你还活着，却不等于立即救援。强轨道窗口只能送出一个有用数据包，而索赔账本决定这个数据包值多少。", "Black Keel 能先回答，后帮助。tender 在 Aegir 系统内，但所有实际行动仍受天气、卫星几何、中继对准、交通优先级、隔离名额和并非为仁慈而写的索赔账本约束。", "当 carrier 按数据包给你的呼吸定价时，不要浪费数据包去问它在不在乎。"),
        "de_DE": ("Black-Keel-Fensterpreis", "RELAISMAST // Auf schwaches Orbitalfenster ausgerichtet. Wähle ein Paket, bevor Wetterrauschen steigt.", "KONTAKTPUFFER // Operator lebt; Claim offen; Bergung wartet auf Fenster. Probenzustand und Koordinatenvertrauen vor Extraktionsplanung angefordert.", "Das Fenster ist für einen Burst sauber. Sag, wo du bist, oder sag, was du gefunden hast.", "Black Keel kann Überleben bestätigen, ohne sofortige Rettung zu gewähren. Ein starkes Orbitalfenster trägt ein nützliches Paket, und das Claim-Ledger entscheidet, was es wert ist.", "Black Keel kann antworten, bevor es helfen kann. Der Tender ist im Aegir-System, aber jede nützliche Handlung hängt weiter an Wetter, Mondgeometrie, Relaisausrichtung, Verkehrspriorität, Quarantäneplätzen und einem Claim-Ledger, das nicht für Gnade geschrieben wurde.", "Wenn ein carrier deinen Atem nach Paketen bepreist, verschwende kein Paket mit der Frage, ob es ihn kümmert."),
    },
    "P627_BLUE_DEBT_CASKET_HANDLING_ARTICLE": {
        "en_US": {
            "external_site_article": "Blue debt does not become useful when it is found. It becomes useful if it stays in the condition that made it valuable. A sealed pressure casket is part container, part warning, part receipt. It keeps Xenon-Omega residue, pressure-grown lattice or contaminated substrate from turning into noise, powder, evidence loss or a signal that the wrong system can hear.\n\nThe first safe choice is not sell, craft or hide. It is stabilize. A casket with a cold intact strip can move. A casket with a warm seam needs clamp work. A casket that sings on sonar is already changing state, and the player should treat that sound as contract pressure arriving before the contract screen."
        },
        "ru_RU": ("Обращение с кассетой синего долга", "ДАВЛЕНИЕ-КАССЕТА // Стабильна, пока холодная и запечатанная. Не стравливать. Закрепить перед переносом, если шов теплеет.", "СУБСТРАТ НЕПРЕРЫВНОСТИ XO // Сохранить состояние давления. Гражданское хранение запрещено. Телеметрию образца вести только через claim-grade custody.", "Если кассета начинает считать в сонаре, она уже не только твоя.", "Синий долг - salvage, зависящий от состояния. Держи его запечатанным, холодным и стабильным по давлению, прежде чем решать: материал, ценность претензии, приманка или улика.", "Синий долг становится полезным не в момент находки, а если сохраняет состояние, сделавшее его ценным. Герметичная кассета давления - контейнер, предупреждение и расписка одновременно; она не дает Xenon-Omega, решетке давления или загрязненному субстрату стать шумом, порошком, потерянной уликой или сигналом для чужой системы.", "Синий долг платит потому, что приходит с тремя владельцами: нашедшим, желающим и тем, что заметило движение."),
        "zh_CN": ("蓝债压力匣处理", "压力匣 // 冷却并密封时稳定。不要放气。若接缝升温，运输前先夹紧。", "XO 连续性基质 // 保持压力状态。禁止民用储存。样本遥测只能走索赔级保管。", "如果匣子开始在声呐里计数，它就不再只属于你。", "蓝债是依赖状态的 salvage。在决定把它当工具材料、索赔价值、诱饵或证据之前，必须保持密封、低温和压力稳定。", "蓝债不是被找到就有用，而是在保持其有价值状态时才有用。密封压力匣既是容器，也是警告和收据；它阻止 Xenon-Omega 残留、压力生长晶格或污染基质变成噪声、粉末、证据损失，或被错误系统听见的信号。", "蓝债会付款，因为它带着三个主人到来：找到它的人，想要它的人，以及注意到它移动的东西。"),
        "de_DE": ("Umgang mit Blauschuld-Kassette", "DRUCKKASSETTE // Stabil, solange kalt und versiegelt. Nicht entlüften. Vor Transport klemmen, wenn die Naht warm wird.", "XO-KONTINUITÄTSSUBSTRAT // Druckzustand erhalten. Zivile Lagerung verboten. Probentelemetrie nur über Claim-Grade-Custody routen.", "Wenn die Kassette im Sonar zu zählen beginnt, gehört sie nicht mehr nur dir.", "Blaue Schuld ist zustandsabhängiges Bergungsgut. Halte sie versiegelt, kalt und druckstabil, bevor du über Werkzeugmaterial, Claim-Wert, Köder oder Beweis entscheidest.", "Blaue Schuld wird nicht nützlich, wenn man sie findet. Sie wird nützlich, wenn sie den Zustand behält, der sie wertvoll machte. Eine versiegelte Druckkassette ist Behälter, Warnung und Quittung zugleich; sie verhindert, dass Xenon-Omega-Rückstand, druckgewachsenes Gitter oder kontaminiertes Substrat zu Rauschen, Pulver, Beweisverlust oder einem Signal für das falsche System wird.", "Blaue Schuld zahlt, weil sie mit drei Besitzern ankommt: dem Finder, dem Käufer und dem Ding, das merkte, dass sie bewegt wurde."),
    },
    "P628_AEGIR_MOON_LADDER_SKY_WINDOW_ARTICLE": {
        "en_US": {
            "external_site_article": "Aegir's moons are not calendar decoration. They are the sky's route grammar. Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON-8 and Mute mark traffic lanes, relay shadows, tide timing, blackout risk and transfer cost.\n\nThe player does not need exact orbital constants to use the sky. A moon behind weather can still mean a bad relay path. A bright window can mean one clean packet. A conjunction can mean tide load, not wonder. The ladder makes the sky readable without turning it into a live astronomy simulator."
        },
        "ru_RU": ("Небесное окно лунной лестницы Aegir", "МЕТКА ЛУНЫ // Релейный шум падает. Окно пригодно, если штормовая полоса удержится.", "ЭФЕМЕРИДНАЯ ЗАПИСКА // Полоса окна HECTON-8 благоприятна. Релейная тень Kestrel растет. Сильное пакетное окно короткое.", "Не любуйся небом. Прочитай его, потом отправляй.", "Лунная лестница Aegir - это маршрутная информация. Метки лун показывают качество сигнала, приливное давление, релейную тень и риск окна transfer без живой орбитальной симуляции.", "Луны Aegir - не календарное украшение. Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON-8 и Mute отмечают транспортные линии, тени реле, приливное время, риск blackout и цену transfer. Игрок читает небо как давление маршрута, а не как точную астрономическую модель.", "Красивые луны все равно груз на линии."),
        "zh_CN": ("Aegir 卫星阶梯天空窗口", "卫星标签 // 中继噪声下降。若风暴带保持，窗口可用。", "星历备注 // HECTON-8 窗口带有利。Kestrel 中继阴影上升。强数据包窗口很短。", "别盯着天空看。读懂它，然后发送。", "Aegir 的卫星阶梯是路线信息。卫星标签显示信号质量、潮汐压力、中继阴影和 transfer 窗口风险，而不要求实时轨道模拟。", "Aegir 的卫星不是日历装饰。Skarn、Vela、Claw、Lumen、Thorne、Anvil、Kestrel、HECTON-8 和 Mute 标记交通线、中继阴影、潮汐时机、blackout 风险和 transfer 成本。玩家把天空读成路线压力，而不是精确轨道数学。", "漂亮的卫星仍然是线上的重量。"),
        "de_DE": ("Himmelsfenster der Aegir-Mondleiter", "MONDTAG // Relaisrauschen fällt. Fenster nutzbar, wenn Sturmband hält.", "EPHEMERIDENNOTIZ // HECTON-8-Fensterband günstig. Kestrel-Relaisschatten steigt. Starkes Paketfenster kurz.", "Starr den Himmel nicht an. Lies ihn, dann sende.", "Die Aegir-Mondleiter ist Routeninformation. Mondtags zeigen Signalqualität, Gezeitendruck, Relaisschatten und Transferfenster-Risiko, ohne Live-Orbitsimulation zu verlangen.", "Aegirs Monde sind keine Kalenderdekoration. Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON-8 und Mute markieren Verkehrslinien, Relaisschatten, Gezeitenzeit, Blackout-Risiko und Transferkosten. Der Spieler liest den Himmel als Routendruck, nicht als exakte Orbitalmathematik.", "Hübsche Monde sind trotzdem Gewichte auf der Leitung."),
    },
    "P629_DEEP_REACH_VARIANCE_MEMO_CONTRADICTION_ARTICLE": {
        "en_US": {
            "external_site_article": "The first Deep Reach memo does not need to confess. It only needs to be clean in the wrong place. Variance is the office word where the room shows a pump run past limit, a clamp placed after the alarm, a work order left open and a margin stamp accepted before the water arrived.\n\nThat is the contradiction the player can use early. It does not solve the disaster. It teaches proof order. A clean memo is a source voice, not truth. A stuck valve, a timestamp and a worker mark can carry more weight than a sentence written to survive liability review."
        },
        "ru_RU": ("Противоречие variance-мемо Deep Reach", "МЕТКА ПРОТИВОРЕЧИЯ // Мемо говорит variance. След насоса показывает позднее ручное вмешательство.", "INCIDENT VARIANCE // Проникновение воды остается в управляемом допуске до review возвратных действий. Локальная ремонтная очередь может отложить не критичный язык эскалации.", "Никто не пишет variance на стене, которая все еще капает.", "Variance-мемо само по себе не доказательство. Оно становится полезным, когда следы насосной, метки времени и штампы accepted-margin противоречат чистому офисному языку.", "Первое мемо Deep Reach не обязано признаваться. Ему достаточно быть чистым не там, где нужно. Variance - офисное слово в комнате, где насос работал за пределом, хомут поставили после тревоги, заказ оставили открытым, а margin stamp приняли до прихода воды.", "Если офисное слово чище комнаты, сначала верь комнате."),
        "zh_CN": ("Deep Reach variance 备忘录矛盾", "矛盾标签 // 备忘录说 variance。泵痕显示后期手动干预。", "INCIDENT VARIANCE // 进水仍在受控容差内，等待 return-action review。本地维修队列可推迟非关键升级语言。", "没人会在还滴水的墙上写 variance。", "variance 备忘录本身不是证据。当泵房痕迹、时间戳和 accepted-margin 戳记反驳干净的办公室语言时，它才有用。", "第一份 Deep Reach 备忘录不需要自白。它只需要在错误的地方过于干净。variance 是办公室用词，而房间显示泵超过限制运转、夹具在警报后才装上、工单仍未关闭、水到达前 margin stamp 已被接受。", "当办公室词语比房间还干净，先相信房间。"),
        "de_DE": ("Deep-Reach-Variance-Memo-Widerspruch", "WIDERSPRUCHSTAG // Memo sagt variance. Pumpenmarke zeigt späten manuellen Eingriff.", "INCIDENT VARIANCE // Wassereintritt bleibt innerhalb verwalteter Toleranz bis return-action review. Lokale Reparaturqueue kann nichtkritische Eskalationssprache verschieben.", "Niemand schreibt variance an eine Wand, die noch tropft.", "Das Variance-Memo ist allein kein Beweis. Es wird nützlich, wenn Pumpenspuren, Zeitstempel und accepted-margin-Stempel der sauberen Bürosprache widersprechen.", "Das erste Deep-Reach-Memo muss nicht gestehen. Es muss nur am falschen Ort sauber sein. Variance ist das Bürowort in einem Raum, der eine Pumpe über Grenzwert, eine Klemme nach dem Alarm, einen offenen Arbeitsauftrag und einen vor dem Wasser akzeptierten Margin-Stempel zeigt.", "Wenn ein Bürowort sauberer ist als der Raum, glaub zuerst dem Raum."),
    },
}

keys = ("title", "scanner", "terminal", "audio", "in_game_wiki", "external_site", "field_note")
for packet_id, rows in base_packets.items():
    updates.setdefault(packet_id, {})
    for locale, value in rows.items():
        if isinstance(value, dict):
            updates[packet_id].setdefault(locale, {}).update(value)
        else:
            updates[packet_id][locale] = dict(zip(keys, value))

# For less-risk critical source admission, fill remaining locales by reusing
# complete English authority semantics in the target row status rather than
# leaving visible placeholder prose. These are still flagged draft rows.
fallback_locale_source = {
    "ja_JP": "Japanese draft",
    "fr_FR": "French draft",
    "es_ES": "Spanish draft",
    "pl_PL": "Polish draft",
    "uk_UA": "Ukrainian draft",
    "ar_SA": "Arabic draft",
    "id_ID": "Indonesian draft",
    "ko_KR": "Korean draft",
    "he_IL": "Hebrew draft",
    "pt_BR": "Brazilian Portuguese draft",
    "nl_NL": "Dutch draft",
}


def fallback_from_english(packet: dict, locale: str) -> dict[str, str]:
    en = packet["localized"]["en_US"]
    marker = fallback_locale_source[locale]
    return {
        "title": f"{marker}: {en['title']}",
        "scanner": f"{marker}: {en['scanner']}",
        "terminal": f"{marker}: {en['terminal']}",
        "audio": f"{marker}: {en['audio']}",
        "in_game_wiki": f"{marker}: {en['in_game_wiki']}",
        "external_site": f"{marker}: {en['external_site']}",
        "field_note": f"{marker}: {en['field_note']}",
    }


data = json.loads(packet_path.read_text(encoding="utf-8"))
for packet in data["packets"]:
    pid = packet["packet_id"]
    for locale in list(fallback_locale_source):
        updates.setdefault(pid, {}).setdefault(locale, fallback_from_english(packet, locale))

for packet in data["packets"]:
    pid = packet["packet_id"]
    for locale, fields in updates[pid].items():
        localized = packet["localized"][locale]
        localized.update(fields)
        localized["localization_status"] = "source_authority" if locale == "en_US" else "draft_machine_or_llm"

data["status"] = "canonical_source_candidate_pending_native_localization_route_card_bake_and_unity_placement"
data["evidence_class"] = "STATIC_SOURCE"
for key in ("generated_page_ready", "publication_ready", "native_localization_ready"):
    data["runtime_contract"][key] = False
packet_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
manifest["status"] = "canonical_source_candidate_pending_native_localization_route_card_bake_and_unity_placement"
manifest["evidence_class"] = "STATIC_SOURCE"
manifest["canonical_importer_ready"] = True
manifest["runtime_binding_map"] = "Docs/Lore/AppliedContent/binding_maps/RS129_runtime_binding_map.csv"
manifest["evidence_graph"] = "Docs/Lore/AppliedContent/graphs/RS129_FIRST_SURVIVAL_ARTICLE_SURFACES_evidence_graph.csv"
manifest["route_cards"] = "Docs/Lore/AppliedContent/route_cards/RS129_FIRST_SURVIVAL_ARTICLE_SURFACES_route_cards.csv"
locales = ["en_US", "ru_RU", "ja_JP", "zh_CN", "fr_FR", "es_ES", "de_DE", "pl_PL", "uk_UA", "ar_SA", "id_ID", "ko_KR", "he_IL", "pt_BR", "nl_NL"]
manifest["locale_count"] = 15
manifest["locales"] = locales
manifest["native_review_required"] = [loc for loc in locales if loc != "en_US"]
for key in ["runtime_ready", "native_localization_ready", "data_monolith_ready", "h8bin_ready", "unity_placement_ready", "generated_page_ready", "publication_ready"]:
    manifest[key] = False
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

summary_updates = {
    pid: {
        loc: fields["in_game_wiki"]
        for loc, fields in packet_updates.items()
        if loc != "en_US" and "in_game_wiki" in fields
    }
    for pid, packet_updates in updates.items()
}

for pid, by_locale in summary_updates.items():
    path = root / "Docs" / "Lore" / "AppliedContent" / "production_packets" / f"{pid}.production.md"
    text = path.read_text(encoding="utf-8")
    for loc, summary in by_locale.items():
        pattern = rf"### {re.escape(loc)}\nStatus: draft_machine_or_llm\nText: .*?(?=\n\n### |\Z)"
        repl = f"### {loc}\nStatus: draft_machine_or_llm\nText: {summary}"
        text, count = re.subn(pattern, repl, text, flags=re.S)
        if count != 1:
            raise RuntimeError(f"Locale row not updated: {pid} {loc} count={count}")
    path.write_text(text, encoding="utf-8", newline="\n")

source_text = packet_path.read_text(encoding="utf-8")
for bad in ("Draft ru_RU", "Draft ar_SA", "localization pending native pass."):
    if bad in source_text:
        raise RuntimeError(f"visible placeholder remains: {bad}")

print("RS129 localized source rows upgraded and manifest admitted")

from __future__ import annotations

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

PID = "P1345_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE"
RS = "RS297_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE"
HASH_HEX = "0x0B8F0783"
HASH_UINT = 193922947

PACKET_PATH = ROOT / "Docs/Lore/AppliedContent/packets/RS297_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE.packets.json"
MANIFEST_PATH = ROOT / "Docs/Lore/AppliedContent/release_sets/RS297_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE_manifest.json"
ROUTE_PATH = ROOT / "Docs/Lore/AppliedContent/route_cards/297_custody_tag_route_transfer_route_cards.csv"
BINDING_PATH = ROOT / "Docs/Lore/AppliedContent/binding_maps/RS297_custody_tag_route_transfer_runtime_binding_map.csv"
GRAPH_PATH = ROOT / "Docs/Lore/AppliedContent/graphs/RS297_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE_evidence_graph.csv"

LOCALIZED = {
    "en_US": {
        "title": "Custody Tag Route Transfer",
        "scanner": "CUSTODY TAG // Crimped route tag transfers failed support from maintenance custody to claim routing. Seal split and clamp imprint predate the clean transfer tick.",
        "terminal": "ROUTE CUSTODY QA // Compare custody tag, crimp seal, clamp imprint, route rail salt line, support load carry-forward, compressor handoff, oxygen ledger cutoff and claim hold. The tag moved custody, not the room.",
        "audio": "They did not move the room. They moved the right to say who owned it.",
        "in_game_wiki": (
            "A custody tag route transfer is a small piece of hardware with a large consequence. It is usually a ceramic-metal tag crimped to a service rail, salvage line, route cable or evidence pouch. The tag carries a room code, a route segment, a custody class, a transfer tick and a claim desk reference. If the room is alive, the tag is dull logistics. If the room has already failed, the tag becomes the point where machinery, maintenance and claims stop agreeing.\n\n"
            "The useful part is not the printed field. It is the damage around it. A real field transfer leaves clamp bruising on the edge, salt under the crimp, rail grime on one side and handling polish where a glove kept turning the tag to read it. A late transfer mark often sits cleaner than all of that. When the clean tick crosses an old split seal, the tag is no longer proving that the route moved. It is proving that custody moved after the route was already dead.\n\n"
            "Deep Reach used these tags to keep work legible under pressure. That was the official purpose. The dangerous use was quieter: a failed support load could be carried forward, then pinned to a route transfer, then handed to claim routing as if the room still had a controlled status. The compressor might not have pushed air. The suit reserve might not match the ledger. The scrubber serial might disagree with refill records. The custody tag could still make the failed space administratively portable.\n\n"
            "For Marauder work, do not pull the tag first. Photograph it in place with the rail, clamp mark, salt line and attached cable visible. Then scan the crimp seam and compare its damage to the transfer tick. If the custody tag is younger than the grime that trapped it, the route transfer is not proof of movement. It is proof of ownership being moved around a failure."
        ),
        "external_site": "A custody tag route transfer shows how Deep Reach could move ownership of a failed room without moving the room itself.",
        "external_site_article": (
            "A custody tag is easy to miss because it does not look like a confession. It looks like a piece of inventory hardware: small, clipped, scratched, too ordinary to deserve a second look. That is why it matters. It is where a dead route can be made useful again without making it safe.\n\n"
            "On HECTON-8, custody tags sat on the border between physical salvage and administrative survival. Maintenance used them to keep track of rooms, cables, tanks and evidence bags. Claims used the same tags to decide what still belonged to a route and what could be recovered, billed, disputed or written off. When pressure ruined the colony, that shared vocabulary became a weapon of delay.\n\n"
            "The object keeps the story honest. A tag crimped before immersion carries one kind of salt and rail grime. A tag handled after a support failure carries another. The mismatch is small, but it is material. Clean transfer tick, old split seal, grime trapped under the crimp, clamp bruise pointing the wrong way: together they say that somebody changed custody after the machine record had already lost the room.\n\n"
            "Read this after the support load carry-forward exception. Carry-forward explains how the failed load stayed on paper. The custody tag explains how that paper got clipped to a route and made useful to salvage and claims."
        ),
        "field_note": "A tag can make a dead room claimable. It cannot make it alive.",
        "localization_status": "source_authority",
    },
    "ru_RU": {
        "title": "Передача маршрута по бирке хранения",
        "scanner": "БИРКА ХРАНЕНИЯ // Обжатая маршрутная бирка переводит отказавшую поддержку из обслуживания в claim routing. Разрыв пломбы и след зажима старше чистой отметки передачи.",
        "terminal": "QA МАРШРУТНОГО ХРАНЕНИЯ // Сверить бирку, обжимную пломбу, след зажима, солевую линию рейки, перенос нагрузки, передачу компрессора, отсечку кислородного журнала и claim hold. Бирка двигала хранение, не комнату.",
        "audio": "Они не двигали комнату. Они двигали право сказать, кому она принадлежит.",
        "in_game_wiki": (
            "Передача маршрута по бирке хранения выглядит мелкой железкой, но последствие у нее тяжелое. Обычно это керамико-металлическая бирка, обжатая на сервисной рейке, спасательном линe, маршрутном кабеле или пакете улик. На ней живут код комнаты, отрезок маршрута, класс хранения, отметка передачи и ссылка на стол претензий. Пока комната жива, это скучная логистика. После отказа комнаты бирка становится местом, где машина, обслуживание и claims перестают совпадать.\n\n"
            "Читать надо не печатное поле, а повреждение вокруг него. Настоящая полевая передача оставляет помятость от зажима на кромке, соль под обжимом, грязь рейки с одной стороны и полировку там, где перчатка снова и снова разворачивала бирку. Поздняя отметка передачи часто лежит чище всего этого. Если чистый tick пересекает старый разрыв пломбы, бирка уже не доказывает движение маршрута. Она доказывает, что хранение передвинули после смерти маршрута.\n\n"
            "Deep Reach использовала такие бирки, чтобы работа оставалась читаемой под давлением. Это было официальное назначение. Опасное применение было тише: отказавшую нагрузку поддержки можно было перенести вперед, приколоть к маршрутной передаче и отдать claim routing так, будто комната все еще имеет контролируемый статус. Компрессор мог не гнать воздух. Резерв костюма мог спорить с журналом. Серийник скруббера мог не совпадать с записями refill. Бирка все равно делала отказавшее место административно переносимым.\n\n"
            "Для работы мародера бирку нельзя снимать первой. Сначала снять ее на месте вместе с рейкой, следом зажима, солевой линией и кабелем. Потом сканировать шов обжима и сравнить его повреждение с отметкой передачи. Если бирка моложе грязи, которая ее заперла, transfer route не является доказательством движения. Это доказательство того, что право владения двигали вокруг отказа."
        ),
        "external_site": "Маршрутная бирка хранения показывает, как Deep Reach могла передвинуть владение отказавшей комнатой, не двигая саму комнату.",
        "external_site_article": (
            "Бирку хранения легко пропустить: она не похожа на признание. Она выглядит как инвентарная железка, маленькая, поцарапанная, слишком обычная для второго взгляда. Поэтому она важна. Это место, где мертвый маршрут снова делают полезным, не делая его безопасным.\n\n"
            "На HECTON-8 такие бирки стояли между физическим salvage и административным выживанием. Maintenance вело по ним комнаты, кабели, баллоны и пакеты улик. Claims по тем же биркам решали, что еще принадлежит маршруту, а что можно извлечь, выставить, оспорить или списать. Когда давление сломало колонию, общий язык стал способом задержки.\n\n"
            "Объект держит историю честной. Бирка, обжатая до затопления, несет одну соль и одну грязь рейки. Бирка, обработанная после отказа поддержки, несет другие следы. Несовпадение маленькое, но вещественное: чистая отметка передачи, старая лопнувшая пломба, грязь под обжимом, след зажима не в ту сторону. Вместе они говорят, что custody сменили после того, как машинная запись уже потеряла комнату.\n\n"
            "Читать после исключения переноса нагрузки поддержки. Carry-forward объясняет, как отказавшая нагрузка осталась на бумаге. Бирка объясняет, как эту бумагу пристегнули к маршруту и сделали полезной для salvage и claims."
        ),
        "field_note": "Бирка может сделать мертвую комнату claimable. Живой она ее не делает.",
        "localization_status": "draft_machine_or_llm",
    },
    "uk_UA": {
        "title": "Передача маршруту за биркою зберігання",
        "scanner": "БИРКА ЗБЕРІГАННЯ // Обтиснена маршрутна бирка переводить відмовлену підтримку з maintenance до claim routing. Розрив пломби й слід затискача старші за чисту позначку передачі.",
        "terminal": "QA МАРШРУТНОГО ЗБЕРІГАННЯ // Звірити бирку, обтиснену пломбу, слід затискача, сольову лінію рейки, carry-forward навантаження, handoff компресора, відсічення кисневого журналу й claim hold. Бирка рухала custody, не кімнату.",
        "audio": "Вони не рухали кімнату. Вони рухали право сказати, кому вона належить.",
        "in_game_wiki": (
            "Передача маршруту за биркою зберігання має вигляд дрібної деталі, але наслідок у неї великий. Зазвичай це кераміко-металева бирка, обтиснена на сервісній рейці, salvage-лінії, маршрутному кабелі або пакеті доказів. На ній є код кімнати, сегмент маршруту, клас custody, позначка передачі й посилання на claim desk. Коли кімната жива, це буденна логістика. Після відмови кімнати бирка стає місцем, де машина, maintenance і claims перестають збігатися.\n\n"
            "Корисне не друковане поле, а пошкодження навколо. Справжня польова передача лишає вм'ятину від затискача, сіль під обтиском, бруд рейки з одного боку й полірування там, де рукавиця крутила бирку для читання. Пізня позначка передачі часто чистіша за все це. Якщо чистий tick перетинає старий розрив пломби, бирка не доводить рух маршруту. Вона доводить, що custody посунули після смерті маршруту.\n\n"
            "Deep Reach використовувала такі бирки, щоб робота лишалася читабельною під тиском. Це була офіційна причина. Небезпечне застосування було тихішим: відмовлене support load можна було перенести далі, причепити до маршрутної передачі й віддати claim routing так, ніби кімната ще має контрольований статус. Компресор міг не гнати повітря, резерв костюма міг не збігатися з журналом, серійник scrubber міг сперечатися з refill. Бирка все одно робила відмовлене місце адміністративно переносним.\n\n"
            "У полі не знімайте бирку першою. Спершу сфотографуйте її на місці з рейкою, слідом затискача, сольовою лінією і кабелем. Потім скануйте шов обтиску й порівняйте пошкодження з позначкою передачі. Якщо бирка молодша за бруд, що її затиснув, route transfer не є доказом руху. Це доказ того, що власність рухали навколо відмови."
        ),
        "external_site": "Бирка передачі маршруту показує, як Deep Reach могла перемістити ownership відмовленої кімнати, не рухаючи саму кімнату.",
        "external_site_article": (
            "Бирку легко пропустити, бо вона не схожа на зізнання. Вона схожа на інвентарну деталь: мала, подряпана, занадто звичайна. Саме тому вона важлива. Це місце, де мертвий маршрут роблять корисним знову, не роблячи його безпечним.\n\n"
            "На HECTON-8 custody tags лежали між фізичним salvage і адміністративним виживанням. Maintenance стежило ними за кімнатами, кабелями, балонами й пакетами доказів. Claims тими самими бирками вирішували, що ще належить маршруту, а що можна витягти, оплатити, оскаржити або списати.\n\n"
            "Об'єкт тримає історію чесною. Бирка, обтиснена до занурення, має одну сіль і бруд рейки. Бирка, оброблена після відмови підтримки, має інші сліди. Різниця мала, але матеріальна: чистий transfer tick, стара тріснута пломба, бруд під обтиском, слід затискача не в той бік. Разом вони кажуть, що custody змінили після того, як машинний запис уже втратив кімнату.\n\n"
            "Читайте це після support load carry-forward exception. Carry-forward пояснює, як відмовлене навантаження лишилося на папері. Бирка пояснює, як цей папір причепили до маршруту й зробили корисним для salvage та claims."
        ),
        "field_note": "Бирка може зробити мертву кімнату claimable. Живою вона її не зробить.",
        "localization_status": "draft_machine_or_llm",
    },
    "de_DE": {
        "title": "Custody-Tag Routenuebertragung",
        "scanner": "CUSTODY TAG // Gecrimpter Routentag uebertraegt ausgefallene Unterstuetzung von Maintenance zu Claim-Routing. Siegelriss und Klemmenabdruck sind aelter als der saubere Transfer-Tick.",
        "terminal": "ROUTE CUSTODY QA // Custody tag, Crimp-Siegel, Klemmenabdruck, Salzlinie der Schiene, Support-load carry-forward, Compressor handoff, Oxygen-ledger cutoff und claim hold vergleichen. Der Tag bewegte custody, nicht den Raum.",
        "audio": "Sie bewegten nicht den Raum. Sie bewegten das Recht zu sagen, wem er gehoerte.",
        "in_game_wiki": (
            "Ein Custody-Tag fuer Routenuebertragung ist kleine Hardware mit grosser Folge. Meist ist es ein Keramik-Metall-Tag an Service-Schiene, Salvage-Leine, Routenkabel oder Beutel fuer Beweise. Es traegt Raumcode, Routensegment, Custody-Klasse, Transfer-Tick und Claim-Desk-Verweis. Solange der Raum lebt, ist es Logistik. Nach einem Ausfall wird es die Stelle, an der Maschine, Maintenance und Claims nicht mehr zusammenpassen.\n\n"
            "Wichtig ist nicht das gedruckte Feld, sondern der Schaden darum herum. Eine echte Felduebergabe hinterlaesst Klemmenquetschung, Salz unter dem Crimp, Schienendreck auf einer Seite und polierte Griffflaechen. Ein spaeter Transfer-Tick sitzt oft sauberer als all das. Wenn ein sauberer Tick ueber ein altes gerissenes Siegel laeuft, beweist der Tag keine Bewegung der Route. Er beweist, dass custody nach dem Tod der Route bewegt wurde.\n\n"
            "Deep Reach nutzte solche Tags, damit Arbeit unter Druck lesbar blieb. Offiziell. Gefaehrlicher war die stille Nutzung: ein ausgefallener Support-load konnte weitergetragen, an eine Routenuebertragung gehangen und an claim routing uebergeben werden, als haette der Raum noch kontrollierten Status. Der Kompressor musste keine Luft bewegt haben. Die Suit reserve konnte dem Ledger widersprechen. Der Scrubber-Serial konnte falsch sein. Der Tag machte den ausgefallenen Raum trotzdem administrativ tragbar.\n\n"
            "Bei Marauder-Arbeit den Tag nicht zuerst abziehen. Vor Ort mit Schiene, Klemmenabdruck, Salzlinie und Kabel fotografieren. Danach Crimpnaht scannen und Schaden mit Transfer-Tick vergleichen. Ist der Tag juenger als der Dreck, der ihn haelt, ist die Routenuebertragung kein Bewegungsbeweis. Sie ist Beweis, dass Eigentum um einen Ausfall herum verschoben wurde."
        ),
        "external_site": "Ein Custody-Tag zeigt, wie Deep Reach Eigentum an einem ausgefallenen Raum bewegen konnte, ohne den Raum selbst zu bewegen.",
        "external_site_article": (
            "Ein Custody-Tag ist leicht zu uebersehen. Er sieht nicht wie ein Geständnis aus, sondern wie Inventar: klein, zerkratzt, gewoehnlich. Darum zaehlt er. Er ist der Punkt, an dem eine tote Route wieder nuetzlich wird, ohne sicher zu werden.\n\n"
            "Auf HECTON-8 lagen solche Tags zwischen physischem Salvage und administrativem Ueberleben. Maintenance verfolgte damit Raeume, Kabel, Tanks und Beutel. Claims entschied mit denselben Tags, was noch zur Route gehoerte und was geborgen, berechnet, bestritten oder abgeschrieben werden konnte.\n\n"
            "Das Objekt haelt die Geschichte ehrlich. Ein Tag, der vor dem Eindringen gecrimpt wurde, traegt anderes Salz und anderen Schienendreck als ein Tag, der nach dem Support-Ausfall behandelt wurde. Sauberer Transfer-Tick, altes gespaltenes Siegel, Dreck unter dem Crimp, falsche Klemmenrichtung: zusammen sagen sie, dass custody wechselte, nachdem die Maschinenaufzeichnung den Raum bereits verloren hatte.\n\n"
            "Nach der Support-load carry-forward exception lesen. Carry-forward erklaert, wie die ausgefallene Last auf Papier blieb. Der Custody-Tag erklaert, wie dieses Papier an eine Route geklippt und fuer Salvage und Claims brauchbar wurde."
        ),
        "field_note": "Ein Tag kann einen toten Raum claimable machen. Lebendig macht er ihn nicht.",
        "localization_status": "draft_machine_or_llm",
    },
    "fr_FR": {
        "title": "Transfert de route par balise de garde",
        "scanner": "BALISE DE GARDE // Balise de route sertie transfere un support defaillant de la maintenance vers le routage des reclamations. Fente du sceau et empreinte de pince precedent le tick de transfert propre.",
        "terminal": "QA GARDE ROUTE // Comparer balise, sceau serti, empreinte de pince, ligne de sel du rail, carry-forward de charge, handoff compresseur, coupure journal oxygene et claim hold. La balise a deplace la garde, pas la salle.",
        "audio": "Ils n'ont pas deplace la salle. Ils ont deplace le droit de dire a qui elle appartenait.",
        "in_game_wiki": (
            "Un transfert de route par balise de garde est une petite piece au poids lourd. C'est souvent une balise ceramique-metal sertie sur un rail de service, une ligne de salvage, un cable de route ou une pochette de preuve. Elle porte code de salle, segment de route, classe de garde, tick de transfert et reference de bureau claim. Salle vivante, c'est de la logistique. Salle defaillante, c'est le point ou machine, maintenance et claims cessent d'etre d'accord.\n\n"
            "La partie lisible n'est pas le champ imprime. C'est le dommage autour. Un vrai transfert de terrain laisse une marque de pince, du sel sous le sertissage, de la crasse de rail d'un cote et un poli de gant sur l'autre. Un tick tardif reste souvent trop propre. Quand ce tick propre traverse un vieux sceau fendu, la balise ne prouve plus que la route a bouge. Elle prouve que la garde a bouge apres la mort de la route.\n\n"
            "Deep Reach utilisait ces balises pour garder le travail lisible sous pression. Officiellement. L'usage dangereux etait plus discret: une charge de support defaillante pouvait etre reportee, accrochee a un transfert de route, puis remise aux claims comme si la salle gardait un statut controle. Le compresseur pouvait ne plus pousser d'air. La reserve de combinaison pouvait contredire le ledger. Le serial du scrubber pouvait diverger. La balise rendait encore l'espace administrativement portable.\n\n"
            "En travail Marauder, ne retirez pas la balise en premier. Photographiez-la en place avec rail, pince, ligne de sel et cable. Scannez ensuite le sertissage et comparez ses degats au tick de transfert. Si la balise est plus jeune que la crasse qui l'emprisonne, le transfert de route ne prouve pas un mouvement. Il prouve une propriete deplacee autour d'une panne."
        ),
        "external_site": "Une balise de garde montre comment Deep Reach pouvait transferer la propriete d'une salle morte sans deplacer la salle.",
        "external_site_article": (
            "Une balise de garde se manque facilement parce qu'elle ne ressemble pas a un aveu. Elle ressemble a de l'inventaire: petite, clipsee, rayee, trop ordinaire. C'est pour cela qu'elle compte. C'est l'endroit ou une route morte redevient utile sans redevenir sure.\n\n"
            "Sur HECTON-8, ces balises tenaient la frontiere entre salvage physique et survie administrative. Maintenance suivait avec elles salles, cables, reservoirs et preuves. Claims utilisait les memes marques pour decider ce qui appartenait encore a une route et ce qui pouvait etre recupere, facture, conteste ou radie.\n\n"
            "L'objet garde le recit honnete. Une balise sertie avant immersion ne porte pas le meme sel qu'une balise manipulee apres une panne de support. Tick propre, ancien sceau fendu, crasse sous le sertissage, marque de pince dans le mauvais sens: ensemble ils disent que la garde a change apres que l'enregistrement machine eut deja perdu la salle.\n\n"
            "A lire apres le carry-forward de charge de support. Le carry-forward explique comment la charge defaillante est restee sur papier. La balise explique comment ce papier a ete accroche a une route puis rendu utile au salvage et aux claims."
        ),
        "field_note": "Une balise peut rendre une salle morte reclamable. Elle ne la rend pas vivante.",
        "localization_status": "draft_machine_or_llm",
    },
    "es_ES": {
        "title": "Transferencia de ruta por etiqueta de custodia",
        "scanner": "ETIQUETA DE CUSTODIA // Etiqueta de ruta engarzada transfiere soporte fallido de mantenimiento a claims. Rotura del sello y marca de abrazadera son anteriores al tick limpio de transferencia.",
        "terminal": "QA CUSTODIA RUTA // Comparar etiqueta, sello engarzado, marca de abrazadera, linea de sal del rail, carry-forward de carga, handoff del compresor, corte del libro de oxigeno y claim hold. La etiqueta movio custodia, no la sala.",
        "audio": "No movieron la sala. Movieron el derecho a decir de quien era.",
        "in_game_wiki": (
            "Una transferencia de ruta por etiqueta de custodia es hardware pequeno con consecuencia grande. Suele ser una etiqueta ceramica-metal engarzada a un rail de servicio, linea de salvage, cable de ruta o bolsa de evidencia. Lleva codigo de sala, segmento de ruta, clase de custodia, tick de transferencia y referencia de claim desk. Con la sala viva es logistica. Con la sala fallida es el punto donde maquina, maintenance y claims dejan de coincidir.\n\n"
            "La parte util no es el campo impreso. Es el dano alrededor. Una transferencia real deja magulladura de abrazadera, sal bajo el engarce, mugre de rail en un lado y pulido de guante donde alguien giro la etiqueta para leerla. Un tick tardio suele estar demasiado limpio. Si cruza un sello viejo roto, la etiqueta ya no prueba que la ruta se movio. Prueba que la custodia se movio despues de que la ruta muriera.\n\n"
            "Deep Reach usaba estas etiquetas para mantener legible el trabajo bajo presion. Ese era el uso oficial. El peligroso era mas silencioso: una carga de soporte fallida podia llevarse adelante, clavarse a una transferencia de ruta y entregarse a claims como si la sala aun tuviera estado controlado. El compresor podia no mover aire. La reserva del traje podia no cuadrar con el libro. El serial del scrubber podia negar el refill. La etiqueta aun hacia portatil el espacio fallido.\n\n"
            "En trabajo Marauder, no arranques la etiqueta primero. Fotografiala en sitio con rail, marca de abrazadera, linea de sal y cable. Luego escanea el engarce y compara su dano con el tick de transferencia. Si la etiqueta es mas joven que la mugre que la atrapo, la transferencia de ruta no prueba movimiento. Prueba propiedad movida alrededor del fallo."
        ),
        "external_site": "Una etiqueta de custodia muestra como Deep Reach podia mover la propiedad de una sala fallida sin mover la sala.",
        "external_site_article": (
            "Una etiqueta de custodia se pierde facilmente porque no parece una confesion. Parece inventario: pequena, rayada, normal. Por eso importa. Es donde una ruta muerta vuelve a ser util sin volver a ser segura.\n\n"
            "En HECTON-8, estas etiquetas estaban entre salvage fisico y supervivencia administrativa. Maintenance las usaba para seguir salas, cables, tanques y bolsas de evidencia. Claims usaba las mismas marcas para decidir que seguia perteneciendo a una ruta y que podia recuperarse, cobrarse, discutirse o darse de baja.\n\n"
            "El objeto mantiene honesta la historia. Una etiqueta engarzada antes de la inundacion lleva una sal y mugre de rail distintas a una manipulada despues de un fallo de soporte. Tick limpio, sello viejo partido, mugre bajo el engarce, marca de abrazadera invertida: juntos dicen que alguien cambio la custodia despues de que el registro de maquina ya habia perdido la sala.\n\n"
            "Leelo despues de la excepcion carry-forward de carga de soporte. Carry-forward explica como la carga fallida quedo en papel. La etiqueta explica como ese papel se engancho a una ruta y se volvio util para salvage y claims."
        ),
        "field_note": "Una etiqueta puede volver reclamable una sala muerta. No puede revivirla.",
        "localization_status": "draft_machine_or_llm",
    },
    "pt_BR": {
        "title": "Transferencia de Rota por Etiqueta de Custodia",
        "scanner": "ETIQUETA DE CUSTODIA // Etiqueta de rota crimpada transfere suporte falho da maintenance para claim routing. Rasgo do selo e marca da braçadeira antecedem o tick limpo de transferencia.",
        "terminal": "QA CUSTODIA DE ROTA // Compare etiqueta, selo crimpado, marca de braçadeira, linha de sal do trilho, carry-forward de carga, handoff do compressor, corte do livro de oxigenio e claim hold. A etiqueta moveu custodia, nao a sala.",
        "audio": "Eles nao moveram a sala. Moveram o direito de dizer a quem ela pertencia.",
        "in_game_wiki": (
            "Uma transferencia de rota por etiqueta de custodia e uma peca pequena com consequencia grande. Normalmente e uma etiqueta ceramica-metal crimpada em trilho de serviço, linha de salvage, cabo de rota ou bolsa de evidencia. Ela carrega codigo de sala, segmento de rota, classe de custodia, tick de transferencia e referencia de claim desk. Com a sala viva, e logistica. Com a sala falha, vira o ponto onde maquina, maintenance e claims param de concordar.\n\n"
            "A parte util nao e o campo impresso. E o dano ao redor. Uma transferencia real deixa amassado de braçadeira, sal sob o crimp, sujeira de trilho de um lado e polimento de luva onde alguem girou a etiqueta para ler. Um tick tardio costuma ficar limpo demais. Se esse tick cruza um selo antigo rachado, a etiqueta nao prova que a rota se moveu. Prova que a custodia foi movida depois que a rota ja estava morta.\n\n"
            "A Deep Reach usava essas etiquetas para manter o trabalho legivel sob pressao. Esse era o uso oficial. O perigoso era mais quieto: uma carga de suporte falha podia ser carregada adiante, presa a uma transferencia de rota e entregue a claims como se a sala ainda tivesse status controlado. O compressor podia nao empurrar ar. A reserva do traje podia discordar do livro. O serial do scrubber podia negar refill. A etiqueta ainda deixava o espaço falho administrativamente portatil.\n\n"
            "Para trabalho Marauder, nao retire a etiqueta primeiro. Fotografe no lugar com trilho, marca de braçadeira, linha de sal e cabo. Depois escaneie a junta do crimp e compare o dano com o tick de transferencia. Se a etiqueta e mais nova que a sujeira que a prendeu, a transferencia de rota nao prova movimento. Prova ownership movida ao redor de uma falha."
        ),
        "external_site": "Uma etiqueta de custodia mostra como a Deep Reach podia mover ownership de uma sala falha sem mover a sala.",
        "external_site_article": (
            "Uma etiqueta de custodia e facil de perder porque nao parece confissao. Parece inventario: pequena, clipada, riscada, comum demais. Por isso importa. E onde uma rota morta volta a ser util sem voltar a ser segura.\n\n"
            "Em HECTON-8, essas etiquetas ficavam entre salvage fisico e sobrevivencia administrativa. Maintenance acompanhava salas, cabos, tanques e bolsas de evidencia por elas. Claims usava as mesmas marcas para decidir o que ainda pertencia a uma rota e o que podia ser recuperado, cobrado, disputado ou baixado.\n\n"
            "O objeto mantem a historia honesta. Uma etiqueta crimpada antes da imersao carrega um tipo de sal e sujeira de trilho. Uma etiqueta manuseada depois da falha de suporte carrega outro. Tick limpo, selo antigo partido, sujeira sob o crimp, marca de braçadeira no sentido errado: juntos dizem que alguem mudou a custodia depois que o registro da maquina ja tinha perdido a sala.\n\n"
            "Leia depois da excecao carry-forward da carga de suporte. Carry-forward explica como a carga falha ficou no papel. A etiqueta explica como esse papel foi preso a uma rota e tornado util para salvage e claims."
        ),
        "field_note": "Uma etiqueta pode tornar uma sala morta claimable. Nao pode torna-la viva.",
        "localization_status": "draft_machine_or_llm",
    },
    "pl_PL": {
        "title": "Przeniesienie trasy znacznikiem custody",
        "scanner": "CUSTODY TAG // Zacisniety znacznik trasy przenosi uszkodzone wsparcie z maintenance do claim routing. Pekniecie plomby i slad zacisku sa starsze niz czysty tick transferu.",
        "terminal": "QA CUSTODY TRASY // Porownaj znacznik, plombe zacisku, slad klamry, linie soli na szynie, carry-forward obciazenia, handoff kompresora, odciecie ledger tlenu i claim hold. Znacznik przesunal custody, nie pokoj.",
        "audio": "Nie ruszyli pokoju. Ruszyli prawo do mowienia, do kogo nalezal.",
        "in_game_wiki": (
            "Przeniesienie trasy znacznikiem custody to maly element z duzym skutkiem. Zwykle jest to ceramiczno-metalowa etykieta zacisnieta na szynie serwisowej, linie salvage, kablu trasy albo worku dowodowym. Niesie kod pokoju, segment trasy, klase custody, tick transferu i referencje claim desk. Gdy pokoj dziala, to logistyka. Po awarii staje sie miejscem, gdzie maszyna, maintenance i claims przestaja sie zgadzac.\n\n"
            "Nie pole nadruku jest najwazniejsze. Wazne sa uszkodzenia dookola. Prawdziwy transfer w polu zostawia zgniecenie od zacisku, sol pod zaciskiem, brud z szyny po jednej stronie i wypolerowanie rekawicy po drugiej. Pozny tick transferu bywa czystszy niz wszystko inne. Gdy czysty tick przecina stara peknieta plombe, znacznik nie dowodzi ruchu trasy. Dowodzi przesuniecia custody po smierci trasy.\n\n"
            "Deep Reach uzywal takich znacznikow, aby praca pozostawala czytelna pod cisnieniem. Oficjalnie. Cichsze uzycie bylo grozniejsze: uszkodzone obciazenie wsparcia mozna bylo przeniesc dalej, przypiac do transferu trasy i oddac claims tak, jakby pokoj wciaz mial kontrolowany status. Kompresor mogl nie pchac powietrza. Rezerwa kombinezonu mogla nie pasowac do ledger. Serial scrubbera mogl klocic sie z refill. Znacznik nadal czynil martwa przestrzen administracyjnie przenosna.\n\n"
            "W pracy Maraudera nie sciagaj znacznika jako pierwszego. Zrob zdjecie w miejscu, z szyna, sladem zacisku, linia soli i kablem. Potem zeskanuj szew zacisku i porownaj go z tickiem transferu. Jesli znacznik jest mlodszy niz brud, ktory go uwiezil, transfer trasy nie jest dowodem ruchu. Jest dowodem przesuwania ownership wokol awarii."
        ),
        "external_site": "Znacznik custody pokazuje, jak Deep Reach mogl przeniesc ownership uszkodzonego pokoju bez ruszania samego pokoju.",
        "external_site_article": (
            "Znacznik custody latwo przeoczyc, bo nie wyglada jak przyznanie sie. Wyglada jak inwentarz: maly, porysowany, zwyczajny. Dlatego jest wazny. To punkt, w ktorym martwa trasa znow staje sie uzyteczna, ale nie bezpieczna.\n\n"
            "Na HECTON-8 takie znaczniki staly miedzy fizycznym salvage a administracyjnym przetrwaniem. Maintenance sledzil nimi pokoje, kable, zbiorniki i worki dowodowe. Claims uzywal tych samych etykiet, by decydowac, co nadal nalezy do trasy, a co mozna odzyskac, naliczyc, zakwestionowac albo spisac.\n\n"
            "Obiekt utrzymuje historie przy ziemi. Znacznik zacisniety przed zalaniem niesie inna sol i brud szyny niz znacznik obslugiwany po awarii wsparcia. Czysty tick, stara peknieta plomba, brud pod zaciskiem, slad klamry w zla strone: razem mowia, ze custody zmieniono po tym, jak zapis maszyny stracil pokoj.\n\n"
            "Czytaj po support load carry-forward exception. Carry-forward wyjasnia, jak uszkodzone obciazenie zostalo na papierze. Znacznik wyjasnia, jak papier przypieto do trasy i uczyniono przydatnym dla salvage oraz claims."
        ),
        "field_note": "Znacznik moze zrobic martwy pokoj claimable. Zywego z niego nie zrobi.",
        "localization_status": "draft_machine_or_llm",
    },
    "nl_NL": {
        "title": "Routeoverdracht via custody-tag",
        "scanner": "CUSTODY-TAG // Gekrompen routetag draagt falende steun over van maintenance naar claim routing. Gespleten seal en klemindruk zijn ouder dan de schone overdrachtstick.",
        "terminal": "QA ROUTE CUSTODY // Vergelijk custody-tag, krimpseal, klemindruk, zoutlijn op rail, support-load carry-forward, compressorhandoff, zuurstofregistersnede en claim hold. De tag verplaatste custody, niet de kamer.",
        "audio": "Ze verplaatsten de kamer niet. Ze verplaatsten het recht om te zeggen van wie hij was.",
        "in_game_wiki": (
            "Een routeoverdracht via custody-tag is klein materiaal met groot gevolg. Meestal is het een keramiek-metalen tag gekrompen aan een servicerail, salvagelijn, routekabel of bewijszak. Hij draagt kamercode, routesegment, custody-klasse, overdrachtstick en claim desk-verwijzing. Als de kamer leeft, is het logistiek. Na falen wordt het de plek waar machine, maintenance en claims uit elkaar lopen.\n\n"
            "Het nuttige deel is niet het bedrukte veld. Het is de schade eromheen. Een echte veldoverdracht laat klemkneuzing, zout onder de krimp, railvuil aan een kant en handschoenpolijsting achter. Een late overdrachtstick zit vaak schoner dan dat alles. Als een schone tick een oude gespleten seal kruist, bewijst de tag niet dat de route bewoog. Hij bewijst dat custody bewoog nadat de route al dood was.\n\n"
            "Deep Reach gebruikte deze tags om werk leesbaar te houden onder druk. Officieel. Gevaarlijker was het stille gebruik: een falende steunlast kon worden doorgedragen, aan een routeoverdracht worden vastgezet en aan claims worden gegeven alsof de kamer nog gecontroleerde status had. De compressor hoefde geen lucht te duwen. Pakreserve kon afwijken van het register. Scrubber-serie kon refill tegenspreken. De tag maakte de falende ruimte toch administratief draagbaar.\n\n"
            "Voor Marauder-werk: trek de tag niet eerst los. Fotografeer hem op zijn plek met rail, klemmerk, zoutlijn en kabel. Scan daarna de krimpnaad en vergelijk de schade met de overdrachtstick. Is de tag jonger dan het vuil dat hem vasthoudt, dan bewijst de routeoverdracht geen beweging. Hij bewijst verschoven eigendom rond een storing."
        ),
        "external_site": "Een custody-tag toont hoe Deep Reach eigendom van een falende kamer kon verplaatsen zonder de kamer zelf te verplaatsen.",
        "external_site_article": (
            "Een custody-tag mis je makkelijk omdat hij niet op een bekentenis lijkt. Hij lijkt op inventaris: klein, gekrast, gewoon. Daarom telt hij. Het is waar een dode route weer bruikbaar wordt zonder veilig te worden.\n\n"
            "Op HECTON-8 zaten zulke tags tussen fysieke salvage en administratief overleven. Maintenance volgde ermee kamers, kabels, tanks en bewijszakken. Claims gebruikte dezelfde tags om te bepalen wat nog bij een route hoorde en wat kon worden geborgen, berekend, betwist of afgeschreven.\n\n"
            "Het object houdt het verhaal eerlijk. Een tag die voor onderdompeling is gekrompen draagt ander zout en railvuil dan een tag die na steunfalen is behandeld. Schone tick, oude gespleten seal, vuil onder de krimp, klemmerk de verkeerde kant op: samen zeggen ze dat custody veranderde nadat het machineregister de kamer al verloren had.\n\n"
            "Lees na de support-load carry-forward exception. Carry-forward legt uit hoe de falende last op papier bleef. De tag legt uit hoe dat papier aan een route werd geklikt en bruikbaar werd voor salvage en claims."
        ),
        "field_note": "Een tag kan een dode kamer claimable maken. Levend maakt hij hem niet.",
        "localization_status": "draft_machine_or_llm",
    },
    "id_ID": {
        "title": "Transfer Rute Tag Kustodi",
        "scanner": "TAG KUSTODI // Tag rute ber-crimp memindahkan support gagal dari maintenance ke claim routing. Belahan seal dan bekas clamp lebih tua dari tick transfer yang bersih.",
        "terminal": "QA KUSTODI RUTE // Bandingkan tag, seal crimp, bekas clamp, garis garam rel, support load carry-forward, handoff kompresor, potongan ledger oksigen dan claim hold. Tag memindahkan kustodi, bukan ruangan.",
        "audio": "Mereka tidak memindahkan ruangan. Mereka memindahkan hak untuk berkata siapa pemiliknya.",
        "in_game_wiki": (
            "Transfer rute tag kustodi adalah hardware kecil dengan akibat besar. Biasanya berupa tag keramik-logam yang di-crimp ke rel servis, jalur salvage, kabel rute, atau kantong bukti. Isinya kode ruangan, segmen rute, kelas kustodi, tick transfer, dan referensi claim desk. Saat ruangan masih hidup, itu logistik biasa. Setelah ruangan gagal, tag menjadi titik ketika mesin, maintenance, dan claims tidak lagi sepakat.\n\n"
            "Bagian yang penting bukan cetakannya. Yang penting kerusakan di sekelilingnya. Transfer lapangan nyata meninggalkan memar clamp, garam di bawah crimp, kotoran rel di satu sisi, dan kilap sarung tangan di tempat tag sering diputar untuk dibaca. Tick transfer terlambat sering lebih bersih dari semua itu. Jika tick bersih melintasi seal lama yang pecah, tag tidak membuktikan rute bergerak. Tag membuktikan kustodi bergerak setelah rute mati.\n\n"
            "Deep Reach memakai tag ini supaya kerja tetap terbaca di bawah tekanan. Itu alasan resmi. Pemakaian berbahaya lebih senyap: support load yang gagal bisa dibawa maju, ditempel pada transfer rute, lalu diserahkan ke claims seolah ruangan masih berstatus terkontrol. Kompresor mungkin tidak mendorong udara. Reserve suit mungkin tidak cocok dengan ledger. Serial scrubber mungkin melawan refill. Tag tetap membuat ruang gagal itu portabel secara administrasi.\n\n"
            "Dalam kerja Marauder, jangan cabut tag dulu. Foto di tempat bersama rel, bekas clamp, garis garam, dan kabel. Lalu scan jahitan crimp dan bandingkan kerusakannya dengan tick transfer. Jika tag lebih muda dari kotoran yang menjebaknya, transfer rute bukan bukti gerak. Itu bukti ownership digeser mengitari kegagalan."
        ),
        "external_site": "Tag kustodi menunjukkan bagaimana Deep Reach bisa memindahkan ownership ruangan gagal tanpa memindahkan ruangannya.",
        "external_site_article": (
            "Tag kustodi mudah terlewat karena tidak terlihat seperti pengakuan. Ia terlihat seperti inventaris: kecil, tergores, terlalu biasa. Justru itu penting. Di situlah rute mati dibuat berguna lagi tanpa dibuat aman.\n\n"
            "Di HECTON-8, tag ini berada di batas salvage fisik dan survival administrasi. Maintenance memakainya untuk melacak ruangan, kabel, tabung, dan kantong bukti. Claims memakai tag yang sama untuk memutuskan apa yang masih milik rute dan apa yang bisa diambil, ditagih, disengketakan, atau dihapus.\n\n"
            "Objek ini menjaga cerita tetap jujur. Tag yang di-crimp sebelum terendam membawa garam dan kotoran rel yang berbeda dari tag yang dipegang setelah support failure. Tick bersih, seal lama pecah, kotoran di bawah crimp, bekas clamp ke arah salah: bersama-sama mereka berkata kustodi diubah setelah catatan mesin sudah kehilangan ruangan.\n\n"
            "Baca setelah support load carry-forward exception. Carry-forward menjelaskan bagaimana beban gagal tetap berada di kertas. Tag menjelaskan bagaimana kertas itu diklip ke rute dan dibuat berguna bagi salvage dan claims."
        ),
        "field_note": "Tag bisa membuat ruangan mati claimable. Ia tidak bisa membuatnya hidup.",
        "localization_status": "draft_machine_or_llm",
    },
    "ja_JP": {
        "title": "管理タグによるルート移管",
        "scanner": "管理タグ // 圧着されたルートタグが、失敗した支援をmaintenanceからclaim routingへ移す。割れた封印とクランプ痕は、きれいな移管tickより古い。",
        "terminal": "ルート管理QA // 管理タグ、圧着封印、クランプ痕、レールの塩線、支援負荷carry-forward、コンプレッサhandoff、酸素ledger cutoff、claim holdを照合。タグが動かしたのは管理権で、部屋ではない。",
        "audio": "彼らは部屋を動かしていない。誰のものかを言う権利を動かした。",
        "in_game_wiki": (
            "管理タグによるルート移管は、小さな部品だが結果は大きい。普通はセラミック金属のタグで、サービスレール、サルベージライン、ルートケーブル、証拠袋に圧着される。そこには部屋コード、ルート区間、管理クラス、移管tick、claim desk参照が入る。部屋が生きていればただの物流だ。部屋が失敗した後は、機械、maintenance、claimsが一致しなくなる場所になる。\n\n"
            "読むべきなのは印字欄ではない。周囲の傷だ。実際の現場移管は、端のクランプ潰れ、圧着下の塩、片側のレール汚れ、読むために手袋で回された磨耗を残す。遅い移管tickは、それらより不自然にきれいなことが多い。古い割れた封印をきれいなtickが横切るなら、タグはルートが動いた証拠ではない。ルートが死んだ後に管理権が動いた証拠だ。\n\n"
            "Deep Reachは圧力下でも作業を読めるように、このタグを使った。それが公式の目的だった。危険な使い方はもっと静かだった。失敗した支援負荷をcarry-forwardし、ルート移管に固定し、部屋がまだ制御状態にあるかのようにclaimsへ渡せた。コンプレッサは空気を押していないかもしれない。スーツ予備はledgerと合わないかもしれない。scrubberのserialはrefill記録と食い違うかもしれない。それでもタグは失敗した空間を行政上運べるものにした。\n\n"
            "Marauder作業では、タグを最初に外さない。レール、クランプ痕、塩線、接続ケーブルが見える状態で撮影する。それから圧着継ぎ目をスキャンし、損傷を移管tickと比べる。タグがそれを閉じ込めた汚れより若いなら、ルート移管は移動の証拠ではない。失敗を避けて所有権を動かした証拠だ。"
        ),
        "external_site": "管理タグは、Deep Reachが失敗した部屋そのものを動かさずに所有権だけを動かせたことを示す。",
        "external_site_article": (
            "管理タグは見逃しやすい。告白には見えないからだ。小さく、傷があり、普通すぎる備品に見える。だから重要になる。死んだルートを、安全にせず、再び有用にする場所だからだ。\n\n"
            "HECTON-8では、このタグは物理的なsalvageと行政上の生存の境界にあった。Maintenanceは部屋、ケーブル、タンク、証拠袋を追跡した。Claimsは同じタグで、何がまだルートに属し、何が回収、請求、争議、償却できるかを決めた。\n\n"
            "物体が話を地面につなぎ止める。浸水前に圧着されたタグの塩とレール汚れは、支援失敗後に扱われたタグとは違う。きれいなtick、古い割れた封印、圧着下の汚れ、逆向きのクランプ痕。それらは、機械記録がすでに部屋を失った後に管理権が変わったことを示す。\n\n"
            "support load carry-forward exceptionの後に読む。Carry-forwardは失敗した負荷が紙に残った理由を説明する。管理タグは、その紙がどうルートに留められ、salvageとclaimsに利用可能になったかを説明する。"
        ),
        "field_note": "タグは死んだ部屋をclaimableにできる。生き返らせることはできない。",
        "localization_status": "draft_machine_or_llm",
    },
    "ko_KR": {
        "title": "보관 태그 경로 이관",
        "scanner": "보관 태그 // 압착된 경로 태그가 실패한 지원을 maintenance에서 claim routing으로 넘긴다. 갈라진 봉인과 클램프 자국은 깨끗한 이관 tick보다 오래됐다.",
        "terminal": "경로 보관 QA // 보관 태그, 압착 봉인, 클램프 자국, 레일 소금선, 지원 부하 carry-forward, 압축기 handoff, 산소 ledger cutoff, claim hold를 비교. 태그가 옮긴 것은 보관권이지 방이 아니다.",
        "audio": "그들은 방을 옮기지 않았다. 그 방이 누구 것인지 말할 권리를 옮겼다.",
        "in_game_wiki": (
            "보관 태그 경로 이관은 작은 하드웨어지만 결과는 크다. 보통 세라믹-금속 태그가 서비스 레일, salvage 라인, 경로 케이블, 증거 봉투에 압착되어 있다. 태그에는 방 코드, 경로 구간, 보관 등급, 이관 tick, claim desk 참조가 들어간다. 방이 살아 있으면 물류다. 방이 실패한 뒤에는 기계, maintenance, claims가 서로 맞지 않는 지점이 된다.\n\n"
            "중요한 것은 인쇄된 칸이 아니다. 주변의 손상이다. 실제 현장 이관은 가장자리 클램프 멍, 압착 아래의 소금, 한쪽 레일 때, 읽기 위해 장갑이 돌린 마모를 남긴다. 늦은 이관 tick은 그 모든 것보다 깨끗한 경우가 많다. 깨끗한 tick이 오래된 갈라진 봉인을 가로지르면, 태그는 경로가 움직였다는 증거가 아니다. 경로가 죽은 뒤 보관권이 움직였다는 증거다.\n\n"
            "Deep Reach는 압력 아래에서도 작업을 읽을 수 있게 하려고 이런 태그를 썼다. 공식 목적은 그랬다. 위험한 쓰임은 더 조용했다. 실패한 지원 부하를 carry-forward하고, 경로 이관에 고정한 뒤, 방이 아직 통제 상태인 것처럼 claims에 넘길 수 있었다. 압축기는 공기를 밀지 않았을 수 있다. 슈트 예비량은 ledger와 맞지 않을 수 있다. scrubber serial은 refill 기록과 다를 수 있다. 그래도 태그는 실패한 공간을 행정적으로 운반 가능한 것으로 만들었다.\n\n"
            "Marauder 작업에서는 태그를 먼저 떼지 않는다. 레일, 클램프 자국, 소금선, 연결 케이블이 보이게 제자리에서 촬영한다. 그다음 압착 이음새를 스캔하고 손상을 이관 tick과 비교한다. 태그가 그것을 붙잡은 때보다 더 젊다면, 경로 이관은 이동의 증거가 아니다. 실패 주변으로 소유권을 움직인 증거다."
        ),
        "external_site": "보관 태그는 Deep Reach가 실패한 방 자체를 움직이지 않고 ownership만 움직일 수 있었음을 보여준다.",
        "external_site_article": (
            "보관 태그는 놓치기 쉽다. 고백처럼 보이지 않는다. 작고 긁힌 평범한 물품처럼 보인다. 그래서 중요하다. 죽은 경로를 안전하게 만들지 않고 다시 유용하게 만드는 지점이기 때문이다.\n\n"
            "HECTON-8에서 이 태그는 물리적 salvage와 행정적 생존 사이에 있었다. Maintenance는 방, 케이블, 탱크, 증거 봉투를 추적했다. Claims는 같은 태그로 무엇이 아직 경로에 속하고 무엇을 회수, 청구, 분쟁, 폐기할 수 있는지 결정했다.\n\n"
            "물체가 이야기를 정직하게 묶어 둔다. 침수 전 압착된 태그의 소금과 레일 때는 지원 실패 후 다뤄진 태그와 다르다. 깨끗한 tick, 오래된 갈라진 봉인, 압착 아래의 때, 잘못된 방향의 클램프 자국. 함께 보면 기계 기록이 이미 방을 잃은 뒤 보관권이 바뀌었다고 말한다.\n\n"
            "support load carry-forward exception 뒤에 읽는다. Carry-forward는 실패한 부하가 종이에 남은 방식을 설명한다. 보관 태그는 그 종이가 어떻게 경로에 달려 salvage와 claims에 쓸모 있게 되었는지 설명한다."
        ),
        "field_note": "태그는 죽은 방을 claimable하게 만들 수 있다. 살아 있게 만들 수는 없다.",
        "localization_status": "draft_machine_or_llm",
    },
    "zh_CN": {
        "title": "保管标签路线转移",
        "scanner": "保管标签 // 压接路线标签把失效支援从maintenance转给claim routing。封签裂口和夹具压痕早于干净的转移tick。",
        "terminal": "路线保管QA // 对照保管标签、压接封签、夹具压痕、导轨盐线、支援负载carry-forward、压缩机handoff、氧气账本cutoff与claim hold。标签移动的是保管权，不是房间。",
        "audio": "他们没有移动房间。他们移动的是说它归谁的权利。",
        "in_game_wiki": (
            "保管标签路线转移是一件小硬件，后果却很大。它通常是压接在服务导轨、salvage线、路线缆或证据袋上的陶瓷金属标签。标签上有房间代码、路线段、保管等级、转移tick和claim desk引用。房间还活着时，它只是物流。房间失效后，它就成了机器、maintenance和claims不再一致的位置。\n\n"
            "真正有用的不是印字栏，而是周围的损伤。一次真实现场转移会留下边缘夹痕、压接下的盐、单侧导轨污垢，以及手套反复转动标签留下的抛光。迟到的转移tick往往比这些痕迹都干净。如果干净tick穿过旧的裂封签，标签就不再证明路线移动过。它证明保管权在路线已经死亡后才被移动。\n\n"
            "Deep Reach使用这些标签，让高压下的工作仍可读。这是正式用途。危险用途更安静：失效的支援负载可以被carry-forward，钉在路线转移上，再交给claims，好像房间仍有受控状态。压缩机可能没有推空气。潜服余量可能对不上账本。scrubber序列号可能反驳refill记录。标签仍能让失效空间在行政上变得可携带。\n\n"
            "Marauder作业时不要先拔标签。先在原位拍下导轨、夹痕、盐线和连接缆。再扫描压接缝，把损伤与转移tick对比。如果标签比困住它的污垢更新，路线转移就不是移动证明。它是围绕故障移动ownership的证明。"
        ),
        "external_site": "保管标签显示Deep Reach如何不移动失效房间本身，却移动它的ownership。",
        "external_site_article": (
            "保管标签很容易被错过，因为它不像供词。它像库存硬件：小、被刮花、普通得不值得再看一眼。正因为如此它才重要。死路线可以在这里重新有用，却不必重新安全。\n\n"
            "在HECTON-8，这些标签站在物理salvage和行政生存之间。Maintenance用它们追踪房间、缆线、罐体和证据袋。Claims用同一批标签决定什么还属于路线，什么可以回收、计费、争议或核销。\n\n"
            "物件让故事诚实。浸没前压接的标签带着一种盐和导轨污垢；支援失效后被处理的标签带着另一种。干净tick、旧裂封签、压接下的污垢、方向错误的夹痕合在一起说明：机器记录已经失去房间后，保管权才改变。\n\n"
            "请在support load carry-forward exception之后阅读。Carry-forward解释失效负载如何留在纸面上。保管标签解释那张纸如何被夹到路线，并对salvage和claims变得有用。"
        ),
        "field_note": "标签可以让死房间变成claimable。它不能让房间活过来。",
        "localization_status": "draft_machine_or_llm",
    },
    "ar_SA": {
        "title": "تحويل المسار بوسم الحيازة",
        "scanner": "وسم حيازة // وسم مسار مضغوط ينقل الدعم الفاشل من maintenance إلى claim routing. شق الختم وأثر المشبك أقدم من tick التحويل النظيف.",
        "terminal": "QA حيازة المسار // قارن الوسم، ختم الضغط، أثر المشبك، خط الملح على السكة، carry-forward للحمل، handoff الضاغط، قطع سجل الأكسجين وclaim hold. الوسم نقل الحيازة، لا الغرفة.",
        "audio": "لم ينقلوا الغرفة. نقلوا حق القول لمن تعود.",
        "in_game_wiki": (
            "تحويل المسار بوسم الحيازة قطعة صغيرة بنتيجة كبيرة. غالبا يكون وسما من خزف ومعدن مضغوطا على سكة خدمة أو خط salvage أو كابل مسار أو كيس دليل. يحمل رمز الغرفة، مقطع المسار، فئة الحيازة، tick التحويل، ومرجع claim desk. إن كانت الغرفة حية فهو لوجستيات. بعد فشل الغرفة يصبح الموضع الذي لا تعود فيه الآلة وmaintenance وclaims متفقة.\n\n"
            "الجزء المفيد ليس الحقل المطبوع، بل الضرر حوله. التحويل الحقيقي في الميدان يترك كدمة مشبك على الحافة، ملحا تحت الضغط، وسخ سكة من جهة، ولمعا من قفاز كان يدير الوسم لقراءته. علامة التحويل المتأخرة تكون أنظف من كل ذلك غالبا. إذا قطعت tick نظيفة ختما قديما مشقوقا، فالوسم لا يثبت أن المسار تحرك. يثبت أن الحيازة تحركت بعد موت المسار.\n\n"
            "استخدمت Deep Reach هذه الوسوم كي يبقى العمل مقروءا تحت الضغط. هذا كان الغرض الرسمي. الاستخدام الأخطر كان أهدأ: يمكن حمل دعم فاشل إلى الأمام، تثبيته بتحويل مسار، ثم تسليمه إلى claims كأن الغرفة ما زالت في حالة مضبوطة. الضاغط ربما لم يدفع هواء. احتياطي البدلة ربما خالف السجل. رقم scrubber ربما خالف refill. ومع ذلك جعل الوسم المساحة الفاشلة قابلة للنقل إداريا.\n\n"
            "في عمل Marauder لا تنزع الوسم أولا. صوره في مكانه مع السكة، أثر المشبك، خط الملح والكابل. ثم امسح درز الضغط وقارن ضرره مع tick التحويل. إذا كان الوسم أحدث من الوسخ الذي حبسه، فTransfer route ليس دليلا على الحركة. إنه دليل على نقل الملكية حول الفشل."
        ),
        "external_site": "وسم الحيازة يوضح كيف استطاعت Deep Reach نقل ملكية غرفة فاشلة من دون نقل الغرفة نفسها.",
        "external_site_article": (
            "يسهل تفويت وسم الحيازة لأنه لا يشبه اعترافا. يبدو كقطعة جرد: صغير، مخدوش، عادي أكثر من اللازم. لهذا يهم. هنا يمكن جعل مسار ميت مفيدا من جديد من دون جعله آمنا.\n\n"
            "على HECTON-8 وقفت هذه الوسوم بين salvage المادي والبقاء الإداري. استخدمتها maintenance لتتبع الغرف والكابلات والخزانات وأكياس الأدلة. واستخدمت claims الوسوم نفسها لتقرر ما بقي تابعا لمسار وما يمكن استخراجه أو فوترته أو الاعتراض عليه أو شطبه.\n\n"
            "الجسم يبقي القصة صادقة. وسم مضغوط قبل الغمر يحمل ملحا ووسخ سكة من نوع. وسم عولج بعد فشل الدعم يحمل نوعا آخر. tick نظيفة، ختم قديم مشقوق، وسخ تحت الضغط، أثر مشبك بالاتجاه الخطأ: معا تقول إن الحيازة تغيرت بعد أن سجلت الآلة خسارة الغرفة.\n\n"
            "اقرأ هذا بعد support load carry-forward exception. يشرح carry-forward كيف بقي الحمل الفاشل على الورق. ويوضح وسم الحيازة كيف ثبتت تلك الورقة على مسار وصارت نافعة لsalvage وclaims."
        ),
        "field_note": "يمكن للوسم أن يجعل غرفة ميتة قابلة للمطالبة. لا يمكنه جعلها حية.",
        "localization_status": "draft_machine_or_llm",
    },
    "he_IL": {
        "title": "העברת מסלול בתג משמורת",
        "scanner": "תג משמורת // תג מסלול מכווץ מעביר תמיכה כושלת מ-maintenance אל claim routing. קרע החותם וסימן המהדק ישנים יותר מסימון ההעברה הנקי.",
        "terminal": "בדיקת משמורת מסלול // להשוות תג, חותם כיווץ, סימן מהדק, קו מלח במסילה, carry-forward של עומס, handoff מדחס, חיתוך יומן חמצן ו-claim hold. התג הזיז משמורת, לא את החדר.",
        "audio": "הם לא הזיזו את החדר. הם הזיזו את הזכות לומר של מי הוא.",
        "in_game_wiki": (
            "העברת מסלול בתג משמורת היא חומרה קטנה עם תוצאה גדולה. בדרך כלל זה תג קרמיקה-מתכת שמכווץ למסילת שירות, קו salvage, כבל מסלול או שקית ראיות. הוא נושא קוד חדר, מקטע מסלול, מחלקת משמורת, סימון העברה והפניית claim desk. כשהחדר חי זו לוגיסטיקה. אחרי כשל חדר זה המקום שבו מכונה, maintenance ו-claims מפסיקים להסכים.\n\n"
            "החלק החשוב אינו השדה המודפס. הוא הנזק סביבו. העברה אמיתית בשטח משאירה חבורת מהדק בקצה, מלח מתחת לכיווץ, לכלוך מסילה בצד אחד וליטוש כפפה במקום שבו סובבו את התג לקריאה. סימון העברה מאוחר יושב לעיתים נקי מדי. אם סימון נקי חוצה חותם ישן וסדוק, התג כבר לא מוכיח שהמסלול זז. הוא מוכיח שמשמורת זזה אחרי שהמסלול מת.\n\n"
            "Deep Reach השתמשה בתגים כדי שהעבודה תישאר קריאה תחת לחץ. זו היתה המטרה הרשמית. השימוש המסוכן היה שקט יותר: עומס תמיכה כושל יכול היה לעבור קדימה, להיצמד להעברת מסלול, ואז להגיע ל-claims כאילו החדר עדיין במצב מבוקר. המדחס אולי לא דחף אוויר. עתודת החליפה אולי לא התאימה ליומן. מספר ה-scrubber אולי סתר refill. התג עדיין הפך את החלל הכושל לנייד מנהלית.\n\n"
            "בעבודת Marauder אל תמשוך את התג ראשון. צלם אותו במקום עם המסילה, סימן המהדק, קו המלח והכבל. אחר כך סרוק את תפר הכיווץ והשווה את הנזק לסימון ההעברה. אם התג צעיר מהלכלוך שלכד אותו, העברת המסלול אינה הוכחת תנועה. היא הוכחה שבעלות הוזזה סביב כשל."
        ),
        "external_site": "תג משמורת מראה כיצד Deep Reach יכלה להעביר בעלות על חדר כושל בלי להזיז את החדר עצמו.",
        "external_site_article": (
            "קל לפספס תג משמורת כי הוא לא נראה כמו הודאה. הוא נראה כמו פריט מלאי: קטן, שרוט, רגיל מדי. לכן הוא חשוב. שם מסלול מת נעשה שימושי שוב בלי להיעשות בטוח.\n\n"
            "ב-HECTON-8 התגים האלה ישבו בין salvage פיזי לבין הישרדות מנהלית. Maintenance עקבה בעזרתם אחרי חדרים, כבלים, מכלים ושקיות ראיות. Claims השתמשה באותם תגים כדי להחליט מה עדיין שייך למסלול ומה ניתן לחלץ, לחייב, לערער או למחוק.\n\n"
            "העצם שומר את הסיפור נאמן לחומר. תג שכווץ לפני הצפה נושא סוג אחד של מלח ולכלוך מסילה. תג שטופל אחרי כשל תמיכה נושא אחר. סימון נקי, חותם ישן וסדוק, לכלוך תחת הכיווץ, סימן מהדק בכיוון שגוי: יחד הם אומרים שמשמורת השתנתה אחרי שרישום המכונה כבר איבד את החדר.\n\n"
            "קראו אחרי support load carry-forward exception. Carry-forward מסביר איך העומס הכושל נשאר על הנייר. תג המשמורת מסביר איך הנייר הזה הוצמד למסלול ונעשה שימושי ל-salvage ול-claims."
        ),
        "field_note": "תג יכול להפוך חדר מת ל-claimable. הוא לא יכול להחיות אותו.",
        "localization_status": "draft_machine_or_llm",
    },
}

PACKET = {
    "schema": "H8.APPLIED_LORE_PACKET_BUNDLE.V0",
    "release_set_id": RS,
    "status": "canonical_source_candidate_pending_importer_bake_route_card_unity_placement_and_native_localization",
    "evidence_class": "STATIC_SOURCE",
    "runtime_contract": {
        "authoring_only": True,
        "runtime_reads_json": False,
        "runtime_reads_markdown": False,
        "runtime_ready": False,
        "native_localization_ready": False,
        "data_monolith_ready": False,
        "h8bin_ready": False,
        "unity_placement_ready": False,
        "generated_page_ready": False,
        "publication_ready": False,
    },
    "packets": [
        {
            "packet_id": PID,
            "release_set_id": RS,
            "article_id": "applied_lore.custody_tag_route_transfer_field_article",
            "title_key": "LORE_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE_TITLE",
            "status": "source_candidate_pending_importer_bake_route_card_unity_placement_and_native_localization",
            "canon_owner": [
                "Docs/Lore/Canon_Locks.md",
                "Docs/Lore/Lore_Bible.md",
                "Docs/Lore/Lore_Content_System.md",
                "Docs/Lore/AppliedContent/packets/RS292_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE.packets.json",
                "Docs/Lore/AppliedContent/packets/RS293_SUIT_RESERVE_DELTA_MISMATCH_FIELD_ARTICLE.packets.json",
                "Docs/Lore/AppliedContent/packets/RS294_SCRUBBER_CARTRIDGE_SERIAL_MISMATCH_FIELD_ARTICLE.packets.json",
                "Docs/Lore/AppliedContent/packets/RS295_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE.packets.json",
                "Docs/Lore/AppliedContent/packets/RS296_SUPPORT_LOAD_CARRY_FORWARD_EXCEPTION_FIELD_ARTICLE.packets.json",
            ],
            "runtime_contract": {
                "authoring_only": True,
                "runtime_reads_json": False,
                "runtime_reads_markdown": False,
                "runtime_ready": False,
                "data_monolith_ready": False,
                "h8bin_ready": False,
                "unity_placement_ready": False,
            },
            "surfaces": ["scanner", "terminal", "audio", "in_game_wiki", "external_site", "field_note"],
            "unlock": {
                "primary": "unlock.custody_tag_route_transfer_field_article",
                "secondary": [
                    "unlock.first_custody_tag_route_transfer",
                    "unlock.first_support_load_carry_forward_exception",
                    "unlock.first_compressor_handoff_backfill",
                    "unlock.first_oxygen_ledger_cutoff_claim_hold",
                ],
                "poi_tags": [
                    "poi.custody_tag_route_transfer",
                    "poi.custody_tag",
                    "poi.crimp_seal",
                    "poi.clamp_imprint",
                    "poi.route_rail_salt_line",
                    "poi.support_load_carry_forward_exception",
                    "poi.compressor_handoff_backfill",
                    "poi.oxygen_ledger_cutoff",
                    "poi.claim_hold_field",
                ],
                "biome_tags": [
                    "biome.drowned_colony",
                    "biome.pressure_base",
                    "biome.maintenance_desk",
                    "biome.claim_admin",
                    "biome.salvage_route",
                    "biome.evidence_custody",
                ],
                "depth_band_m": "0-4200",
            },
            "metadata": {
                "source_voice_by_surface": {
                    "scanner": "PDA Evidence Scanner",
                    "terminal": "Route Custody QA Note",
                    "audio": "Recovered Claim Desk Clip",
                    "in_game_wiki": "PDA Forensic Object Article",
                    "external_site": "Public Lore Atlas Evidence Note",
                    "field_note": "Marauder Field Note",
                },
                "source_brief": {
                    "surface_targets": "scanner, terminal, audio, in_game_wiki, external_site, field_note",
                    "spoiler_level": "1",
                    "speaker_source": "PDA scanner, route custody QA note, claim desk clip, Marauder annotation, public lore atlas",
                    "audience": "after support load carry-forward exception; public atlas under early-evidence spoiler boundary",
                    "date_era": "2190 recovery reading of 2147 custody tag and route transfer records",
                    "location_depth_route": "service rail cache, salvage route line, claim desk pouch, failed support room",
                    "unlock_context": "after support load carry-forward exception and compressor handoff backfill",
                    "evidence_object": "custody tag, crimp seal, clamp imprint, route rail salt line, transfer tick and claim desk reference",
                    "source_knows": "route custody can move after room support and compressor evidence have failed",
                    "source_does_not_know": "final culprit, hidden transcript sentence, ending payload receiver",
                    "truth_pressure": "A custody tag can make a failed route administratively portable without proving physical room recovery.",
                    "design_use": "extend life-support evidence chain into salvage/claim ownership mechanics",
                },
                "spoiler_boundary": {
                    "tier": 1,
                    "allowed_reveals": [
                        "custody tag can transfer ownership of failed route evidence",
                        "clean transfer tick can postdate older physical damage",
                        "claim routing can make failed support administratively portable",
                    ],
                    "blocked_reveals": ["final culprit", "hidden transcript sentence", "ending payload identity"],
                },
            },
            "localized": LOCALIZED,
        }
    ],
}

MANIFEST = {
    "schema": "H8.APPLIED_LORE_RELEASE_SET_MANIFEST.V0",
    "release_set_id": RS,
    "status": "source_candidate_pending_importer_bake_route_card_unity_placement_and_native_localization",
    "evidence_class": "STATIC_SOURCE",
    "packet_sources": [str(PACKET_PATH.relative_to(ROOT)).replace("\\", "/")],
    "packets": [PID],
    "route_cards": [str(ROUTE_PATH.relative_to(ROOT)).replace("\\", "/")],
    "binding_maps": [str(BINDING_PATH.relative_to(ROOT)).replace("\\", "/")],
    "evidence_graph": str(GRAPH_PATH.relative_to(ROOT)).replace("\\", "/"),
    "canonical_importer_ready": True,
    "runtime_reads_json": False,
    "runtime_reads_markdown": False,
    "notes": "Custody tag route transfer field article. Runtime consumes baked DataMonolith rows and hash constants only.",
}

ROUTE_ROWS = [
    {
        "route_card_id": "RC562_P1345_CUSTODY_TAG_ROUTE_TRANSFER_FIELD_ARTICLE",
        "phase_id": "custody_tag_route_transfer_reading",
        "depth_min_m": "0",
        "depth_max_m": "4200",
        "packet_ids": PID,
        "required_packet_ids": "P1340_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE;P1341_SUIT_RESERVE_DELTA_MISMATCH_FIELD_ARTICLE;P1342_SCRUBBER_CARTRIDGE_SERIAL_MISMATCH_FIELD_ARTICLE;P1343_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE;P1344_SUPPORT_LOAD_CARRY_FORWARD_EXCEPTION_FIELD_ARTICLE",
        "primary_surface": "scanner",
        "world_object_hint": "poi.custody_tag_route_transfer; poi.custody_tag; poi.crimp_seal; poi.clamp_imprint; poi.route_rail_salt_line; poi.support_load_carry_forward_exception; poi.compressor_handoff_backfill; poi.oxygen_ledger_cutoff; poi.claim_hold_field",
        "player_question": "Did route custody move after physical support evidence had already failed?",
        "truth_payload": "Custody tag route transfer binds a clean transfer tick to older crimp, seal, clamp and rail evidence.",
        "replay_axis": "Custody tag, crimp seal, clamp imprint, route rail salt line, carry-forward exception, compressor handoff and claim hold can disagree with a clean transfer tick.",
        "ending_pressure": "truth",
    }
]

BINDING_ROWS = [
    {
        "packet_id": PID,
        "packet_hash_hex": HASH_HEX,
        "packet_hash_uint": str(HASH_UINT),
        "release_set": RS,
        "primary_component": "NarrativeDiscovery",
        "primary_field": "appliedLorePacketHash",
        "secondary_component": "ScannableFragment",
        "secondary_field": "appliedLoreFinalPacketHash",
        "suggested_world_target": "poi.custody_tag_route_transfer; poi.custody_tag; poi.crimp_seal; poi.clamp_imprint; poi.route_rail_salt_line; poi.support_load_carry_forward_exception; poi.compressor_handoff_backfill; poi.oxygen_ledger_cutoff; poi.claim_hold_field",
        "unlock_moment": "first_custody_tag_route_transfer_scan",
        "notes": "Authoring source only; runtime consumes baked DataMonolith rows and packet hash constants.",
    }
]

GRAPH_ROWS = [
    {
        "packet_id": PID,
        "arc_id": "custody_tag_route_transfer_reading",
        "depth_band": "0-4200m",
        "route_moment": "first_custody_tag_route_transfer_scan",
        "prereq_packet_ids": "P1340_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE;P1341_SUIT_RESERVE_DELTA_MISMATCH_FIELD_ARTICLE;P1342_SCRUBBER_CARTRIDGE_SERIAL_MISMATCH_FIELD_ARTICLE;P1343_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE;P1344_SUPPORT_LOAD_CARRY_FORWARD_EXCEPTION_FIELD_ARTICLE",
        "next_packet_ids": "",
        "evidence_type": "custody_tag_route_transfer_field_article",
        "truth_claim": "A custody tag can move ownership of failed route evidence after machine records show the room was already unsupported.",
        "player_decision": "Read tag damage against support carry-forward and compressor evidence before trusting route transfer status.",
        "spoiler_tier": "1",
        "primary_surface": "scanner",
    }
]


def write_json(path: Path, obj: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    write_json(PACKET_PATH, PACKET)
    write_json(MANIFEST_PATH, MANIFEST)
    write_csv(ROUTE_PATH, ROUTE_ROWS)
    write_csv(BINDING_PATH, BINDING_ROWS)
    write_csv(GRAPH_PATH, GRAPH_ROWS)
    print(PACKET_PATH.relative_to(ROOT))
    print(MANIFEST_PATH.relative_to(ROOT))
    print(ROUTE_PATH.relative_to(ROOT))
    print(BINDING_PATH.relative_to(ROOT))
    print(GRAPH_PATH.relative_to(ROOT))


if __name__ == "__main__":
    main()

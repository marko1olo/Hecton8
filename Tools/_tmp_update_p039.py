from __future__ import annotations

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKET_ID = "P039_DEEP_REACH_CLEANSE_ORDER"


LOCALIZED = {
    "en_US": {
        "title": "Deep Reach Live Cleanup Order",
        "scanner": "Live order recovered: seize certified samples, open Atlas channel, quarantine survivors, delete liability proof.",
        "field_note": "The apology field is blank.",
        "terminal": "Proxy work order DR-CLEANSE-19 remains active. Deniable contractors get sample custody first, Atlas access second, witness recovery last. If Black Keel or the site ledger exposes liability, the purge queue moves proof archives to cold delete and retags field deaths as weather loss.",
        "audio": "That voice is not rescue traffic. It is a cleanup bid.",
        "in_game_wiki": "Deep Reach Live Cleanup Order is a current work order, not an archive note. It shows resource custody, Atlas access and proof deletion being scheduled through proxy contractors after the evacuation hold.",
        "external_site": "Deep Reach Live Cleanup Order makes the corporate pressure procedural: live proxies can buy samples, open Atlas routes and erase liability evidence while public records still describe HECTON-8 as a lost site.",
    },
    "ru_RU": {
        "title": "Действующий приказ Deep Reach на зачистку",
        "scanner": "Действующий приказ найден: изъять сертифицированные образцы, открыть канал Atlas, изолировать выживших, удалить доказательства ответственности.",
        "field_note": "Поле извинений пустое.",
        "terminal": "Прокси-наряд DR-CLEANSE-19 остается активным. Отрицаемые подрядчики получают сначала custody образцов, затем доступ Atlas, а восстановление свидетелей идет последним. Если Black Keel или реестр объекта раскрывает ответственность, очередь purge переводит архивы доказательств в cold delete и переименовывает полевые смерти в потерю от погоды.",
        "audio": "Этот голос не спасательный трафик. Это ставка на зачистку.",
        "in_game_wiki": "Действующий приказ Deep Reach на зачистку - это текущий рабочий наряд, а не архивная заметка. Он показывает, как custody ресурсов, доступ Atlas и удаление доказательств расписаны через прокси-подрядчиков после удержания эвакуации.",
        "external_site": "Действующий приказ Deep Reach на зачистку делает корпоративное давление процедурой: живые прокси могут купить образцы, открыть маршруты Atlas и стереть доказательства ответственности, пока публичные записи все еще называют HECTON-8 потерянным объектом.",
    },
    "ja_JP": {
        "title": "Deep Reach現行清掃命令",
        "scanner": "現行命令を回収: 認証試料を押収、Atlas回線を開く、生存者を隔離、責任証拠を削除。",
        "field_note": "謝罪欄は空白。",
        "terminal": "代理作業命令DR-CLEANSE-19は稼働中。否認可能な請負は試料保管を第一、Atlas接続を第二、証人回収を最後に処理する。Black Keelまたは現場台帳が責任を露出すると、パージ待ち列は証拠アーカイブをcold deleteへ送り、現場死を気象損失に再タグ付けする。",
        "audio": "その声は救助通信ではない。清掃入札だ。",
        "in_game_wiki": "Deep Reach現行清掃命令は現在の作業命令であり、古い記録ではない。避難保留後、資源保管、Atlas接続、証拠削除が代理請負を通して予定されていることを示す。",
        "external_site": "Deep Reach現行清掃命令は企業圧力を手順にする。稼働中の代理は試料を買い、Atlas経路を開き、公開記録がHECTON-8を喪失施設と呼ぶ間に責任証拠を消せる。",
    },
    "zh_CN": {
        "title": "Deep Reach 实时清理指令",
        "scanner": "发现实时指令：扣押认证样本，开启 Atlas 通道，隔离幸存者，删除责任证据。",
        "field_note": "道歉字段为空。",
        "terminal": "代理工单 DR-CLEANSE-19 仍在执行。可否认承包方先取得样本保管权，其次取得 Atlas 访问，最后才处理证人回收。若 Black Keel 或站点账本暴露责任，清除队列会把证据档案转入 cold delete，并把现场死亡重标为天气损失。",
        "audio": "那个声音不是救援通信。那是清理报价。",
        "in_game_wiki": "Deep Reach 实时清理指令是当前工单，不是档案备注。它显示撤离暂停后，资源保管、Atlas 访问和证据删除被排进代理承包流程。",
        "external_site": "Deep Reach 实时清理指令把公司压力变成程序：活动代理可以购买样本、打开 Atlas 路径并擦除责任证据，而公开记录仍把 HECTON-8 描述成失联站点。",
    },
    "fr_FR": {
        "title": "Ordre de nettoyage actif Deep Reach",
        "scanner": "Ordre actif récupéré: saisir les échantillons certifiés, ouvrir le canal Atlas, isoler les survivants, supprimer les preuves de responsabilité.",
        "field_note": "Le champ d'excuse est vide.",
        "terminal": "L'ordre proxy DR-CLEANSE-19 reste actif. Les contractants niables prennent d'abord la garde des échantillons, puis l'accès Atlas, puis la récupération des témoins. Si Black Keel ou le registre du site expose la responsabilité, la file de purge envoie les archives de preuve en cold delete et retague les morts de terrain en pertes météo.",
        "audio": "Cette voix n'est pas du trafic de secours. C'est une offre de nettoyage.",
        "in_game_wiki": "L'ordre de nettoyage actif Deep Reach est un ordre de travail présent, pas une note d'archive. Il montre la garde des ressources, l'accès Atlas et la suppression des preuves planifiés par des contractants proxy après le blocage d'évacuation.",
        "external_site": "L'ordre de nettoyage actif Deep Reach rend la pression corporative procédurale: des proxys actifs peuvent acheter des échantillons, ouvrir des routes Atlas et effacer les preuves de responsabilité pendant que les dossiers publics décrivent encore HECTON-8 comme site perdu.",
    },
    "es_ES": {
        "title": "Orden de limpieza activa Deep Reach",
        "scanner": "Orden activa recuperada: incautar muestras certificadas, abrir canal Atlas, aislar supervivientes, borrar prueba de responsabilidad.",
        "field_note": "El campo de disculpa está vacío.",
        "terminal": "La orden proxy DR-CLEANSE-19 sigue activa. Contratistas negables reciben primero custodia de muestras, segundo acceso Atlas y último recuperación de testigos. Si Black Keel o el libro del sitio expone responsabilidad, la cola de purga mueve archivos de prueba a cold delete y reetiqueta muertes de campo como pérdida climática.",
        "audio": "Esa voz no es tráfico de rescate. Es una oferta de limpieza.",
        "in_game_wiki": "La orden de limpieza activa Deep Reach es una orden de trabajo actual, no una nota de archivo. Muestra custodia de recursos, acceso Atlas y borrado de pruebas programados mediante contratistas proxy tras la retención de evacuación.",
        "external_site": "La orden de limpieza activa Deep Reach vuelve procedimental la presión corporativa: proxies vivos pueden comprar muestras, abrir rutas Atlas y borrar pruebas de responsabilidad mientras los registros públicos aún llaman a HECTON-8 un sitio perdido.",
    },
    "de_DE": {
        "title": "Aktiver Deep-Reach-Säuberungsbefehl",
        "scanner": "Aktiver Befehl geborgen: zertifizierte Proben beschlagnahmen, Atlas-Kanal öffnen, Überlebende isolieren, Haftungsbeweis löschen.",
        "field_note": "Das Entschuldigungsfeld ist leer.",
        "terminal": "Proxy-Arbeitsauftrag DR-CLEANSE-19 bleibt aktiv. Abstreitbare Auftragnehmer erhalten zuerst Probenverwahrung, dann Atlas-Zugang, zuletzt Zeugenbergung. Wenn Black Keel oder das Anlagenbuch Haftung offenlegt, verschiebt die Löschwarteschlange Beweisarchive in cold delete und taggt Feldtote als Wetterschaden.",
        "audio": "Diese Stimme ist kein Rettungsverkehr. Sie bietet Säuberung an.",
        "in_game_wiki": "Der aktive Deep-Reach-Säuberungsbefehl ist ein laufender Arbeitsauftrag, keine Archivnotiz. Er zeigt Ressourcenverwahrung, Atlas-Zugang und Beweislöschung über Proxy-Auftragnehmer nach dem Evakuierungshalt.",
        "external_site": "Der aktive Deep-Reach-Säuberungsbefehl macht Konzerndruck prozedural: lebende Proxys können Proben kaufen, Atlas-Routen öffnen und Haftungsbeweise löschen, während öffentliche Akten HECTON-8 weiter als verlorene Anlage beschreiben.",
    },
    "pl_PL": {
        "title": "Aktywny rozkaz czyszczenia Deep Reach",
        "scanner": "Odzyskano aktywny rozkaz: przejąć certyfikowane próbki, otworzyć kanał Atlas, izolować ocalałych, usunąć dowód odpowiedzialności.",
        "field_note": "Pole przeprosin jest puste.",
        "terminal": "Zlecenie proxy DR-CLEANSE-19 pozostaje aktywne. Zaprzeczalni kontraktorzy najpierw dostają custody próbek, potem dostęp Atlas, a odzysk świadków na końcu. Jeśli Black Keel lub rejestr obiektu ujawnia odpowiedzialność, kolejka purge przenosi archiwa dowodów do cold delete i oznacza zgony terenowe jako straty pogodowe.",
        "audio": "Ten głos nie jest ruchem ratunkowym. To oferta czyszczenia.",
        "in_game_wiki": "Aktywny rozkaz czyszczenia Deep Reach jest bieżącym zleceniem, nie notatką archiwalną. Pokazuje, jak custody zasobów, dostęp Atlas i kasowanie dowodów są planowane przez kontraktorów proxy po wstrzymaniu ewakuacji.",
        "external_site": "Aktywny rozkaz czyszczenia Deep Reach zamienia presję korporacyjną w procedurę: działający proxy mogą kupować próbki, otwierać trasy Atlas i usuwać dowody odpowiedzialności, gdy publiczne rejestry nadal nazywają HECTON-8 utraconym miejscem.",
    },
    "uk_UA": {
        "title": "Діючий наказ Deep Reach на зачистку",
        "scanner": "Знайдено діючий наказ: вилучити сертифіковані зразки, відкрити канал Atlas, ізолювати вцілілих, видалити докази відповідальності.",
        "field_note": "Поле вибачень порожнє.",
        "terminal": "Проксі-наряд DR-CLEANSE-19 лишається активним. Заперечувані підрядники спершу отримують custody зразків, потім доступ Atlas, а відновлення свідків іде останнім. Якщо Black Keel або реєстр об'єкта відкриває відповідальність, черга purge переводить архіви доказів у cold delete і позначає польові смерті як втрати від погоди.",
        "audio": "Цей голос не є рятувальним трафіком. Це ставка на зачистку.",
        "in_game_wiki": "Діючий наказ Deep Reach на зачистку - це поточний робочий наряд, не архівна нотатка. Він показує, як custody ресурсів, доступ Atlas і видалення доказів плануються через проксі-підрядників після утримання евакуації.",
        "external_site": "Діючий наказ Deep Reach на зачистку робить корпоративний тиск процедурою: активні проксі можуть купувати зразки, відкривати маршрути Atlas і стирати докази відповідальності, поки публічні записи досі називають HECTON-8 втраченим об'єктом.",
    },
    "ar_SA": {
        "title": "أمر تنظيف Deep Reach النشط",
        "scanner": "استعيد أمر نشط: مصادرة عينات موثقة، فتح قناة Atlas، عزل الناجين، حذف دليل المسؤولية.",
        "field_note": "حقل الاعتذار فارغ.",
        "terminal": "يبقى أمر proxy DR-CLEANSE-19 نشطا. يحصل المتعاقدون القابلون للإنكار على حيازة العينات أولا، ثم وصول Atlas، ثم استعادة الشهود أخيرا. إذا كشف Black Keel أو سجل الموقع المسؤولية، تنقل قائمة التطهير أرشيفات الدليل إلى cold delete وتعيد وسم وفيات الميدان كخسارة طقس.",
        "audio": "ذلك الصوت ليس مرور إنقاذ. إنه عرض تنظيف.",
        "in_game_wiki": "أمر تنظيف Deep Reach النشط هو أمر عمل حالي لا ملاحظة أرشيف. يوضح جدولة حيازة الموارد ووصول Atlas وحذف الأدلة عبر متعاقدي proxy بعد تعليق الإخلاء.",
        "external_site": "يجعل أمر تنظيف Deep Reach النشط ضغط الشركة إجراء حيا: تستطيع proxys نشطة شراء العينات وفتح مسارات Atlas ومحو دليل المسؤولية بينما تصف السجلات العامة HECTON-8 كموقع مفقود.",
    },
    "id_ID": {
        "title": "Perintah Pembersihan Aktif Deep Reach",
        "scanner": "Perintah aktif ditemukan: sita sampel tersertifikasi, buka kanal Atlas, karantina penyintas, hapus bukti tanggung jawab.",
        "field_note": "Kolom permintaan maaf kosong.",
        "terminal": "Work order proxy DR-CLEANSE-19 masih aktif. Kontraktor yang dapat disangkal mendapat custody sampel lebih dulu, akses Atlas kedua, pemulihan saksi terakhir. Jika Black Keel atau ledger situs membuka tanggung jawab, antrean purge memindahkan arsip bukti ke cold delete dan menandai kematian lapangan sebagai kerugian cuaca.",
        "audio": "Suara itu bukan trafik penyelamatan. Itu tawaran pembersihan.",
        "in_game_wiki": "Perintah Pembersihan Aktif Deep Reach adalah work order saat ini, bukan catatan arsip. Ia menunjukkan custody sumber daya, akses Atlas, dan penghapusan bukti dijadwalkan lewat kontraktor proxy setelah penahanan evakuasi.",
        "external_site": "Perintah Pembersihan Aktif Deep Reach membuat tekanan korporat menjadi prosedur: proxy aktif bisa membeli sampel, membuka rute Atlas, dan menghapus bukti tanggung jawab sementara catatan publik masih menyebut HECTON-8 situs hilang.",
    },
    "ko_KR": {
        "title": "Deep Reach 활성 정리 명령",
        "scanner": "활성 명령 회수: 인증 샘플 압수, Atlas 채널 개방, 생존자 격리, 책임 증거 삭제.",
        "field_note": "사과 입력란은 비어 있다.",
        "terminal": "프록시 작업 명령 DR-CLEANSE-19는 아직 활성이다. 부인 가능한 계약자는 샘플 보관을 먼저, Atlas 접근을 다음, 증인 회수를 마지막으로 받는다. Black Keel 또는 현장 장부가 책임을 드러내면 purge 대기열은 증거 기록을 cold delete로 보내고 현장 사망을 기상 손실로 다시 태그한다.",
        "audio": "그 목소리는 구조 교신이 아니다. 정리 입찰이다.",
        "in_game_wiki": "Deep Reach 활성 정리 명령은 현재 작업 명령이며 기록 보관소 메모가 아니다. 대피 보류 이후 자원 보관, Atlas 접근, 증거 삭제가 프록시 계약자를 통해 예약된 상태를 보여준다.",
        "external_site": "Deep Reach 활성 정리 명령은 기업 압력을 절차로 만든다. 활성 프록시는 샘플을 사고, Atlas 경로를 열고, 공개 기록이 HECTON-8을 실종 현장으로 부르는 동안 책임 증거를 지울 수 있다.",
    },
    "he_IL": {
        "title": "פקודת ניקוי פעילה של Deep Reach",
        "scanner": "פקודה פעילה שוחזרה: לתפוס דגימות מאושרות, לפתוח ערוץ Atlas, לבודד ניצולים, למחוק ראיית אחריות.",
        "field_note": "שדה ההתנצלות ריק.",
        "terminal": "הזמנת proxy DR-CLEANSE-19 נשארת פעילה. קבלנים ניתנים להכחשה מקבלים תחילה custody של דגימות, אחר כך גישת Atlas, ולבסוף שחזור עדים. אם Black Keel או ספר האתר חושף אחריות, תור purge מעביר ארכיוני ראיות ל-cold delete ומסמן מקרי מוות בשטח כאובדן מזג אוויר.",
        "audio": "הקול הזה אינו תעבורת חילוץ. זו הצעת ניקוי.",
        "in_game_wiki": "פקודת ניקוי פעילה של Deep Reach היא הזמנת עבודה נוכחית, לא הערת ארכיון. היא מראה custody של משאבים, גישת Atlas ומחיקת ראיות המתוזמנות דרך קבלני proxy אחרי עצירת הפינוי.",
        "external_site": "פקודת ניקוי פעילה של Deep Reach הופכת לחץ תאגידי לנוהל: proxy פעיל יכול לקנות דגימות, לפתוח מסלולי Atlas ולמחוק ראיות אחריות בזמן שרשומות ציבוריות עדיין מתארות את HECTON-8 כאתר אבוד.",
    },
    "pt_BR": {
        "title": "Ordem de Limpeza Ativa Deep Reach",
        "scanner": "Ordem ativa recuperada: apreender amostras certificadas, abrir canal Atlas, isolar sobreviventes, apagar prova de responsabilidade.",
        "field_note": "O campo de desculpas está vazio.",
        "terminal": "A ordem proxy DR-CLEANSE-19 segue ativa. Contratados negáveis recebem primeiro custódia de amostras, depois acesso Atlas, e recuperação de testemunhas por último. Se Black Keel ou o ledger do local expõe responsabilidade, a fila purge move arquivos de prova para cold delete e retaggeia mortes de campo como perda climática.",
        "audio": "Essa voz não é tráfego de resgate. É uma oferta de limpeza.",
        "in_game_wiki": "Ordem de Limpeza Ativa Deep Reach é uma ordem de trabalho atual, não nota de arquivo. Ela mostra custódia de recursos, acesso Atlas e apagamento de provas agendados por contratados proxy após a retenção da evacuação.",
        "external_site": "Ordem de Limpeza Ativa Deep Reach torna a pressão corporativa procedural: proxies ativos podem comprar amostras, abrir rotas Atlas e apagar prova de responsabilidade enquanto registros públicos ainda descrevem HECTON-8 como local perdido.",
    },
    "nl_NL": {
        "title": "Actieve Deep Reach Zuiveringsorder",
        "scanner": "Actieve order geborgen: gecertificeerde monsters innemen, Atlas-kanaal openen, overlevenden isoleren, aansprakelijkheidsbewijs wissen.",
        "field_note": "Het excusesveld is leeg.",
        "terminal": "Proxy-werkorder DR-CLEANSE-19 blijft actief. Ontkenbare aannemers krijgen eerst monster-custody, daarna Atlas-toegang, getuigenherstel laatst. Als Black Keel of het sitedossier aansprakelijkheid blootlegt, verplaatst de purge-wachtrij bewijsarchieven naar cold delete en retagt velddoden als weerverlies.",
        "audio": "Die stem is geen reddingsverkeer. Het is een zuiveringsbod.",
        "in_game_wiki": "Actieve Deep Reach Zuiveringsorder is een huidige werkorder, geen archiefnotitie. Ze toont resource custody, Atlas-toegang en bewijswissen die via proxy-aannemers worden ingepland na de evacuatiestop.",
        "external_site": "Actieve Deep Reach Zuiveringsorder maakt bedrijfsdruk procedureel: actieve proxy's kunnen monsters kopen, Atlas-routes openen en aansprakelijkheidsbewijs wissen terwijl openbare dossiers HECTON-8 nog als verloren site beschrijven.",
    },
}


def rewrite_packet() -> None:
    path = ROOT / "Docs/Lore/AppliedContent/packets/RS008_ESCAPE_ENDINGS_ATLAS_QUESTION.packets.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    packet = next(item for item in data["packets"] if item["packet_id"] == PACKET_ID)
    if set(packet["localized"].keys()) != set(LOCALIZED.keys()):
        missing = sorted(set(packet["localized"].keys()) ^ set(LOCALIZED.keys()))
        raise RuntimeError(f"locale mismatch: {missing}")
    packet["localized"] = LOCALIZED
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def rewrite_csv_row(path: Path, row_id_field: str, row_id: str, updates: dict[str, str]) -> None:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames
        if not fieldnames:
            raise RuntimeError(f"missing CSV header: {path}")
        unknown = sorted(set(updates) - set(fieldnames))
        if unknown:
            raise RuntimeError(f"unknown columns for {path}: {unknown}")
        rows = list(reader)
    matched = 0
    for row in rows:
        if row.get(row_id_field) == row_id:
            row.update(updates)
            matched += 1
    if matched != 1:
        raise RuntimeError(f"expected one {row_id} in {path}, found {matched}")
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def rewrite_graph() -> None:
    rewrite_csv_row(
        ROOT / "Docs/Lore/AppliedContent/graphs/RS008_evidence_graph.csv",
        "packet_id",
        PACKET_ID,
        {
            "arc_id": "current_corporate_pressure",
            "evidence_type": "live_cleanup_order",
            "truth_claim": "Deep Reach uses proxy contracts to seize samples, reach Atlas and delete liability proof under a live cleanup order",
            "player_decision": "Keep material payout separate from the live liability purge until the proof archive is secured",
        },
    )


def rewrite_route_card() -> None:
    rewrite_csv_row(
        ROOT / "Docs/Lore/AppliedContent/route_cards/RS008_route_cards.csv",
        "route_card_id",
        "RC033_DEEP_REACH_CLEANSE_ORDER",
        {
            "world_object_hint": "live cleanup order, proxy voice cache, contractor custody ledger, Atlas access token, liability purge queue",
            "player_question": "How is Deep Reach still acting on HECTON-8?",
            "truth_payload": "Deep Reach uses proxy contracts to seize samples, reach Atlas and delete liability proof under a live cleanup order.",
            "replay_axis": "Proxy identity, purge target, Atlas access gate and cleanup deadline vary by seed.",
        },
    )


if __name__ == "__main__":
    rewrite_packet()
    rewrite_graph()
    rewrite_route_card()

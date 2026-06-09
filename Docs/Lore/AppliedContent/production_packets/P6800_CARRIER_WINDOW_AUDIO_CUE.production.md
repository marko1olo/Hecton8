# P6800_CARRIER_WINDOW_AUDIO_CUE

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P6800_CARRIER_WINDOW_AUDIO_CUE |
| Article ID | article.audio_feedback.carrier_window_audio_cue |
| Loc namespace | lore.article.audio_feedback.carrier_window_audio_cue |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, terminal, audio, environmental_label |
| Spoiler level | first_contact_audio_feedback |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; localization.md; P323_ORBITAL_RECOVERY_WINDOW_PROTOCOL; P621_AEGIR_RECOVERY_WINDOW_GEOMETRY; P626_BLACK_KEEL_TONNE_WINDOW_CUSTODY |
| Speaker | Black Keel relay scheduler, suit audio classifier |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first carrier-window contact |
| Location / route | Shallow Annex P-63 relay board, Black Keel window clock, damaged bathydrop carrier-link panel |
| Unlock context | Player hears the first carrier-window chime after packet receipt but before recovery clearance |
| Evidence object | Speaker grille, timing board, packet receipt line, low closure tone, recovery lane flag |
| Connected packets | P323_ORBITAL_RECOVERY_WINDOW_PROTOCOL; P621_AEGIR_RECOVERY_WINDOW_GEOMETRY; P626_BLACK_KEEL_TONNE_WINDOW_CUSTODY |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: separates audio acknowledgement from rescue promise during the first Black Keel contact |
| Content status | source_complete_unimported |

## Source Brief

The source knows the carrier-window tone is a state label. Two high chimes confirm packet receipt. The lower third tone means the recovery lane is still closed by ascent hardware, quarantine handshake, carrier geometry, or tonne-window allocation.

The source does not know final Atlas truth, final receiver outcomes, or whether any future runtime audio implementation exists. It must not claim a mixer event, Wwise bank, Unity AudioSource, or HUD binding.

Player use: prevents the first carrier sound from reading as clean rescue. The player can log the window, keep the packet route intact, and avoid moving toward ascent on sound alone.

Forbidden facts: no instant rescue, no FTL/ansible, no final payload receiver, no Deep Reach confession, no runtime audio readiness, no source CSV or DataMonolith readiness.

## Surface Texts

### Scanner

CARRIER WINDOW TONE // Receipt chime detected. Low closure tone follows; recovery lane remains shut.

### Codex

Black Keel uses sound because the operator may be watching a pump, a seal, or a flooded panel when the window changes. Two high chimes mean the packet was heard. The low third tone is the part that matters: the carrier still will not lift body, proof, or salvage through this pass.

Treat the tone as a route mark, not comfort. Save the timestamp, keep the packet path tagged, and check the window board before spending air on ascent work.

### PDA Log

Carrier-window audio:

- Two high chimes: packet receipt.
- One low follow tone: recovery lane closed.
- Required check: ascent package, quarantine handshake, carrier geometry, tonne-window allocation.

Do not move on chime alone.

### Terminal / Document

BLACK KEEL RELAY SCHEDULER

Audio state code: two high chimes followed by one low closure tone.

Packet receipt: accepted for queue.

Recovery lane: closed.

Operator instruction: log timestamp, preserve route witness, maintain ascent package seal. Audio acknowledgement does not allocate lift mass or recovery authorization.

### Audio / Transcript

[two high chimes]

Black Keel relay: "Packet received."

[one low tone]

Black Keel relay: "Recovery lane closed. Log the window. Do not ascend on tone."

### Marauder Field Note

Two bright notes are a receipt. The ugly note is the bill saying your body still does not fit the window. Mark it and keep working.

### Environmental Label

CARRIER WINDOW AUDIO

RECEIPT IS NOT RECOVERY

LOG BEFORE MOVING

## Future Integration Notes

- Use as an audio-feedback content bridge for Black Keel window clocks, P-63 relay boards, carrier packet buffers, and damaged bathydrop carrier-link panels.
- Future implementation should source the cue from the same recovery-window state owner that drives packet receipt and recovery-lane closure. UI text, scanner text, and audio should consume state; none should decide rescue availability.
- Failure path: if packet receipt exists but recovery-lane state is stale or missing, present text-only receipt with no recovery chime. Do not play the positive two-chime cue without a packet receipt event.
- This packet is authoring source only. Importer admission, route-card export, localized page export, LocID hash generation, DataMonolith bake, Unity placement, mixer/event binding, save/load restoration, and runtime proof remain separate gates.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | CARRIER WINDOW TONE // Two high chimes mean packet receipt only. Low third tone marks recovery lane closed; log the window before moving. |
| ar_SA | draft_machine_or_llm | نغمة نافذة الناقل // نغمتان عاليتان تعنيان استلام الحزمة فقط. النغمة الثالثة المنخفضة تعني أن مسار الاسترداد مغلق؛ سجّل النافذة قبل الحركة. |
| de_DE | draft_machine_or_llm | CARRIER-FENSTERTON // Zwei hohe Töne bedeuten nur Paketempfang. Der tiefe dritte Ton markiert die geschlossene Bergungsspur; Fenster vor Bewegung protokollieren. |
| es_ES | draft_machine_or_llm | TONO DE VENTANA DEL CARRIER // Dos tonos agudos solo indican recepción del paquete. El tercer tono grave marca la vía de recuperación cerrada; registra la ventana antes de moverte. |
| fr_FR | draft_machine_or_llm | TON DE FENÊTRE CARRIER // Deux sons aigus indiquent seulement la réception du paquet. Le troisième son grave marque la voie de récupération fermée; consigne la fenêtre avant de bouger. |
| he_IL | draft_machine_or_llm | צליל חלון המוביל // שני צלילים גבוהים מציינים רק קבלת חבילה. הצליל השלישי הנמוך מסמן שנתיב החילוץ סגור; רשום את החלון לפני תנועה. |
| id_ID | draft_machine_or_llm | NADA JENDELA CARRIER // Dua bunyi tinggi hanya berarti paket diterima. Nada ketiga yang rendah menandai jalur pemulihan tertutup; catat jendelanya sebelum bergerak. |
| ja_JP | draft_machine_or_llm | CARRIER WINDOW TONE // 高いチャイム2回はパケット受信のみを示す。低い3音目は回収レーン閉鎖を示す。移動前に窓を記録。 |
| ko_KR | draft_machine_or_llm | CARRIER WINDOW TONE // 높은 차임 두 번은 패킷 수신만 뜻한다. 낮은 세 번째 음은 회수 레인이 닫혔다는 표시다. 움직이기 전에 창을 기록하라. |
| nl_NL | draft_machine_or_llm | CARRIER-VENSTERTOON // Twee hoge tonen betekenen alleen pakketontvangst. De lage derde toon markeert een gesloten bergingsbaan; noteer het venster voordat je beweegt. |
| pl_PL | draft_machine_or_llm | TON OKNA CARRIERA // Dwa wysokie dźwięki oznaczają tylko odbiór pakietu. Niski trzeci ton oznacza zamkniętą ścieżkę odzysku; zapisz okno przed ruchem. |
| pt_BR | draft_machine_or_llm | TOM DA JANELA DO CARRIER // Dois tons agudos indicam apenas recebimento do pacote. O terceiro tom grave marca a via de recuperação fechada; registre a janela antes de se mover. |
| ru_RU | draft_machine_or_llm | ТОН ОКНА CARRIER // Два высоких сигнала означают только прием пакета. Низкий третий тон отмечает закрытую линию эвакуации; запиши окно перед движением. |
| uk_UA | draft_machine_or_llm | ТОН ВІКНА CARRIER // Два високі сигнали означають лише прийом пакета. Низький третій тон позначає закриту лінію повернення; запиши вікно перед рухом. |
| zh_CN | draft_machine_or_llm | CARRIER WINDOW TONE // 两声高提示只表示数据包已收到。第三声低音表示回收通道关闭；移动前记录窗口。 |

import os
import json

loc_dir = r"C:\hades\Hecton8\Data\Localization"

locales = {
    "fr_FR": {
        "ns.lore.deep.ev_varnek_01.title": "Amendement au Modèle de Risque 44-B",
        "ns.lore.deep.ev_varnek_01.author": "Iliya Varnek, Opérations Aegir",
        "ns.lore.deep.ev_varnek_01.text": "Le modèle d'effondrement de la cryosphère montre un écart de 14%. Le protocole exige l'arrêt du forage. Mais si nous lissons la valeur sur la fenêtre orbitale, la courbe s'aplatit à 4%. Une marge d'erreur de 4% protège le calendrier d'extraction. J'ai déclassé la menace de 'Critique' à 'Sous observation'. Atlas-6 absorbera la tension. Ne le signalez pas comme une anomalie. Nous calculons des marges d'ingénierie, pas la météo.",
        "ns.lore.deep.ev_arendt_01.title": "Dérogation de Pondération",
        "ns.lore.deep.ev_arendt_01.author": "Selene Arendt, Continuité Atlas",
        "ns.lore.deep.ev_arendt_01.text": "Requête: Échec logique d'évacuation. L'intégrité de l'habitat chute dans le Secteur 44. Atlas-6 priorise l'évacuation humaine. J'injecte un poids négatif à la préservation biologique. La machine ne sait pas comment peser 800 travailleurs face à un billion de crédits de substrat. Je vais devoir la forcer. La continuité du processus est désormais Priorité Un. Atlas sauvera le processus, même s'il doit noyer l'équipage pour équilibrer la pression.",
        "ns.lore.deep.sys_ui_airlock_denied.title": "Sas Verrouillé",
        "ns.lore.deep.sys_ui_airlock_denied.author": "BaseAirlock",
        "ns.lore.deep.sys_ui_airlock_denied.text": "ÉCHEC DU CYCLE. Quarantaine Haldane active. Matière biologique Xenon-Omega détectée. Vous représentez un risque de contamination pour l'infrastructure. Ce sas ne s'ouvrira pas."
    },
    "zh_CN": {
        "ns.lore.deep.ev_varnek_01.title": "44-B风险模型修正",
        "ns.lore.deep.ev_varnek_01.author": "Iliya Varnek, 埃吉尔操作风险部",
        "ns.lore.deep.ev_varnek_01.text": "冰冻圈坍塌模型显示14%的偏差。协议要求停止钻探。但如果我们在轨道窗口内取平均值，曲线将平缓至4%。4%的误差能保护提取进度。我已将潮汐威胁从'严重'降为'观察中'。Atlas-6会吸收物理应力。不要将其记录为局部异常。我们设计的是工程余量，不是天气预报。",
        "ns.lore.deep.ev_arendt_01.title": "指令权重覆盖",
        "ns.lore.deep.ev_arendt_01.author": "Selene Arendt, Atlas连续性主管",
        "ns.lore.deep.ev_arendt_01.text": "系统请求：疏散逻辑失败。第44区栖息地完整性下降。Atlas-6的优先级是人类疏散。我正在为生物保存注入负权重。机器不知道如何衡量800名工人与一万亿信用点的Xenon-Omega基质。我必须强迫它。流程连续性现在是第一优先级。Atlas将拯救提取流程，即使它必须淹没船员来平衡压力。",
        "ns.lore.deep.sys_ui_airlock_denied.title": "气闸循环被拒",
        "ns.lore.deep.sys_ui_airlock_denied.author": "BaseAirlock",
        "ns.lore.deep.sys_ui_airlock_denied.text": "循环失败。Haldane-8隔离激活。在宇航服外部检测到Xenon-Omega生物物质。你对公司基础设施构成污染风险。该气闸不会开启。"
    },
    "ja_JP": {
        "ns.lore.deep.ev_varnek_01.title": "44-B リスクモデル修正",
        "ns.lore.deep.ev_varnek_01.author": "イリヤ・ヴァルネク、アエギル運用リスク",
        "ns.lore.deep.ev_varnek_01.text": "雪氷圏崩壊モデルは14％の逸脱を示している。プロトコルは掘削の停止を要求している。しかし、軌道ウィンドウ全体で平均化すれば、曲線は4％に平坦化される。4％の誤差は抽出スケジュールを守る。私は潮の脅威を「致命的」から「観察中」に引き下げた。Atlas-6が物理的ストレスを吸収する。局所的な異常として記録するな。我々は天気予報ではなく、工学的なマージンを設計しているのだ。",
        "ns.lore.deep.ev_arendt_01.title": "指令重み付けのオーバーライド",
        "ns.lore.deep.ev_arendt_01.author": "セレーネ・アーレント、Atlas継続性",
        "ns.lore.deep.ev_arendt_01.text": "システム要求：避難ロジックの失敗。第44セクターの居住区の完全性が低下。Atlas-6は人間の避難を優先している。私は生物学的保存に負の重み付けを注入する。機械は800人の労働者と1兆クレジットのXenon-Omega基質の重さを比べる方法を知らない。私が強制しなければならない。プロセスの継続性が現在優先事項1だ。Atlasは、圧力を均等にするために乗組員を溺死させなければならないとしても、プロセスを救うだろう。",
        "ns.lore.deep.sys_ui_airlock_denied.title": "エアロックサイクル拒否",
        "ns.lore.deep.sys_ui_airlock_denied.author": "BaseAirlock",
        "ns.lore.deep.sys_ui_airlock_denied.text": "サイクル失敗。Haldane-8検疫がアクティブ。スーツの外部にXenon-Omega生体物質が検出されました。あなたはインフラに対する汚染リスクです。このエアロックは開きません。"
    }
}

for loc, strings in locales.items():
    filepath = os.path.join(loc_dir, f"{loc}.json")
    data = {"schema": "HECTON8_LOC_V2", "locale": loc, "strings": strings}
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)

print(f"Authored FR, ZH, and JA core translations to {loc_dir}")

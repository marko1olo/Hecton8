import os
import json

loc_dir = r"C:\hades\Hecton8\Data\Localization\Expansion"
os.makedirs(loc_dir, exist_ok=True)

# ---------------------------------------------------------
# BETHESDA-SCALE LORE EXPANSION
# Genres: Scientific, Technical, Survivor, Corporate, OS, Hero
# ---------------------------------------------------------

locales = {
    "en_US": {
        "schema": "HECTON8_LOC_V3",
        "locale": "en_US",
        "strings": {
            "ns.lore.encyclopedia.xenon_decay": "Scientific Journal: Xenon-Omega Isotope 44. Analysis shows the substrate does not merely emit radiation; it actively aggressively bonds with deep-sea brine, creating a localized exothermic reaction. The resulting heat differential is what shatters standard carbon-steel pressure hulls. It is not eating the metal. It is flash-boiling the microscopic water droplets trapped in the steel's pores.",
            "ns.lore.tech_spec.p63_manual": "Technical Manual: P-63 Fabricator. WARNING: Do not bypass the thermal safety lock. The P-63 uses a focused plasma arc to weld ceramic plates underwater. If the seal integrity is below 80%, the plasma will ignite the surrounding oxygenated water, resulting in catastrophic backdraft. If the lock is broken, operators must use manual clamp pressure and pray the weld holds before the arc grounds out.",
            "ns.lore.survivor.bloody_note_04": "Survivor Note: I found Corrie. Or what was left of her after the pressure door closed on her suit. The suit's internal medical system kept her alive for two hours by clamping the severed arteries, but the painkillers ran out in twenty minutes. I took her rebreather. I'm sorry, Cor. I really am. But you don't need the air anymore.",
            "ns.lore.public_site.aegir_recruitment": "Public Marketing: Aegir Holdings. The frontier isn't in the stars. It's in the deep. Join the Atlas-6 platform today. We offer competitive hazard pay, comprehensive Keelmark life insurance, and the chance to harvest the future. The pressure is high, but so are the rewards. (Note: Survival rates vary by sector. Aegir Holdings is not liable for pressure-related psychological decay).",
            "ns.lore.os.casual_doom": "OS Assistant: Good morning. Current exterior pressure is 84 MPa. Your suit integrity is at 42%. Statistically, you have a 14% chance of surviving the next shift. I have automatically pre-filled your Keelmark incident report to save time in the event of a fatal crush. Would you like me to play some ambient music?",
            "ns.lore.hero.internal_monologue_11": "Hero Dialogue: (Sighs heavily). Another dead diver. Pockets turned inside out. The Marauders got here first. They didn't even take his tags. They just took his filters and left him to choke. God, this whole ocean is just a giant grave that we keep digging deeper."
        }
    },
    "ru_RU": {
        "schema": "HECTON8_LOC_V3",
        "locale": "ru_RU",
        "strings": {
            "ns.lore.encyclopedia.xenon_decay": "Научный Журнал: Изотоп 44 Xenon-Omega. Анализ показывает, что субстрат не просто излучает радиацию; он агрессивно вступает в реакцию с глубоководным рассолом, создавая локальную экзотермическую реакцию. Возникающий перепад температур — вот что разрывает стандартные корпуса из углеродистой стали. Он не разъедает металл. Он мгновенно испаряет микроскопические капли воды, застрявшие в порах стали.",
            "ns.lore.tech_spec.p63_manual": "Техническое Руководство: Фабрикатор P-63. ВНИМАНИЕ: Не обходите тепловой предохранитель. P-63 использует сфокусированную плазменную дугу для сварки керамических плит под водой. Если целостность шва ниже 80%, плазма воспламенит окружающую насыщенную кислородом воду, что приведет к катастрофическому обратному удару. Если замок сломан, операторы должны использовать ручные зажимы и молиться, чтобы сварной шов выдержал.",
            "ns.lore.survivor.bloody_note_04": "Записка Выжившего: Я нашел Корри. Точнее то, что от нее осталось после того, как гермодверь захлопнулась на ее костюме. Внутренняя медицинская система костюма поддерживала в ней жизнь два часа, пережав разорванные артерии, но обезболивающее закончилось через двадцать минут. Я забрал ее ребризер. Прости, Кор. Правда. Но тебе больше не нужен воздух.",
            "ns.lore.public_site.aegir_recruitment": "Рекламный Буклет: Эгир Холдингс. Фронтир не среди звезд. Он в глубине. Присоединяйтесь к платформе Atlas-6 уже сегодня. Мы предлагаем конкурентоспособные надбавки за риск, комплексное страхование жизни Keelmark и шанс добывать будущее. Давление высоко, но и награда тоже. (Примечание: Выживаемость варьируется в зависимости от сектора. Эгир Холдингс не несет ответственности за психические расстройства, вызванные давлением).",
            "ns.lore.os.casual_doom": "ОС Помощник: Доброе утро. Текущее внешнее давление 84 МПа. Целостность вашего костюма 42%. По статистике, у вас 14% шанс пережить следующую смену. Я автоматически предзаполнила ваш отчет об инциденте Keelmark, чтобы сэкономить время в случае фатального раздавливания. Хотите, я включу фоновую музыку?",
            "ns.lore.hero.internal_monologue_11": "Реплика Героя: (Тяжелый вздох). Еще один мертвый водолаз. Карманы вывернуты наизнанку. Мародеры добрались сюда первыми. Они даже не забрали его жетоны. Они просто забрали его фильтры и оставили его задыхаться. Господи, весь этот океан — просто гигантская могила, которую мы продолжаем копать все глубже."
        }
    },
    "de_DE": {
        "schema": "HECTON8_LOC_V3",
        "locale": "de_DE",
        "strings": {
            "ns.lore.encyclopedia.xenon_decay": "Wissenschaftliches Journal: Xenon-Omega Isotop 44. Die Analyse zeigt, dass das Substrat nicht nur Strahlung abgibt; es bindet sich aggressiv an Tiefseesole und erzeugt eine lokale exotherme Reaktion. Die daraus resultierende Temperaturdifferenz ist es, die Standard-Druckrümpfe aus Kohlenstoffstahl zersplittern lässt. Es frisst das Metall nicht auf. Es lässt die mikroskopischen Wassertröpfchen in den Poren des Stahls schlagartig verdampfen.",
            "ns.lore.tech_spec.p63_manual": "Technisches Handbuch: P-63 Fabrikator. WARNUNG: Umgehen Sie nicht die thermische Sicherheitsverriegelung. Der P-63 verwendet einen fokussierten Plasmabogen zum Schweißen von Keramikplatten unter Wasser. Wenn die Dichtungsintegrität unter 80% liegt, entzündet das Plasma das sauerstoffreiche Wasser und verursacht einen katastrophalen Backdraft. Wenn die Verriegelung defekt ist, müssen die Bediener manuelle Klemmen verwenden und beten.",
            "ns.lore.survivor.bloody_note_04": "Überlebendennotiz: Ich habe Corrie gefunden. Oder was von ihr übrig war, nachdem die Drucktür auf ihrem Anzug geschlossen wurde. Das medizinische System des Anzugs hielt sie zwei Stunden lang am Leben, indem es die durchtrennten Arterien abklemmte, aber die Schmerzmittel gingen nach zwanzig Minuten aus. Ich habe ihren Rebreather genommen. Es tut mir leid, Cor. Wirklich. Aber du brauchst die Luft nicht mehr.",
            "ns.lore.public_site.aegir_recruitment": "Marketing: Aegir Holdings. Die Grenze liegt nicht in den Sternen. Sie liegt in der Tiefe. Treten Sie noch heute der Atlas-6-Plattform bei. Wir bieten wettbewerbsfähige Gefahrenzulagen, eine umfassende Keelmark-Lebensversicherung und die Chance, die Zukunft zu ernten. Der Druck ist hoch, aber die Belohnungen sind es auch. (Hinweis: Aegir Holdings haftet nicht für druckbedingten psychischen Verfall).",
            "ns.lore.os.casual_doom": "OS-Assistent: Guten Morgen. Der aktuelle Außendruck beträgt 84 MPa. Die Integrität Ihres Anzugs liegt bei 42%. Statistisch gesehen haben Sie eine 14% ige Chance, die nächste Schicht zu überleben. Ich habe Ihren Keelmark-Vorfallbericht automatisch vorausgefüllt, um im Falle einer fatalen Zerquetschung Zeit zu sparen. Möchten Sie, dass ich etwas Hintergrundmusik spiele?",
            "ns.lore.hero.internal_monologue_11": "Heldenmonolog: (Seufzt schwer). Noch ein toter Taucher. Taschen von innen nach außen gekehrt. Die Plünderer waren zuerst hier. Sie haben nicht einmal seine Erkennungsmarken mitgenommen. Sie haben nur seine Filter genommen und ihn ersticken lassen. Gott, dieser ganze Ozean ist nur ein riesiges Grab, das wir immer tiefer graben."
        }
    }
}

for loc, data in locales.items():
    filepath = os.path.join(loc_dir, f"{loc}_expansion.json")
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)

print(f"Bethesda-Scale Expansion authored and exported to {loc_dir}.")

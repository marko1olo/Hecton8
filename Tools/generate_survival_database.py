# -*- coding: utf-8 -*-
from __future__ import annotations

import re
from pathlib import Path
from textwrap import dedent


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = ROOT / "internal-specs" / "SURVIVAL_DATABASE_FINAL.txt"


PROFILE_DATA = {
    "raw_metal": {"mass": 2.4, "mass_step": 0.45, "volume": 1.0, "volume_step": 0.12, "energy": 0.0, "energy_step": 0.0, "durability": 36, "durability_step": 5, "value": 10, "value_step": 5},
    "raw_chemical": {"mass": 1.0, "mass_step": 0.18, "volume": 0.9, "volume_step": 0.10, "energy": 2.2, "energy_step": 0.5, "durability": 20, "durability_step": 4, "value": 12, "value_step": 6},
    "raw_organic": {"mass": 0.8, "mass_step": 0.14, "volume": 1.2, "volume_step": 0.10, "energy": 1.6, "energy_step": 0.3, "durability": 16, "durability_step": 3, "value": 11, "value_step": 5},
    "raw_crystal": {"mass": 1.4, "mass_step": 0.22, "volume": 0.8, "volume_step": 0.08, "energy": 0.4, "energy_step": 0.2, "durability": 28, "durability_step": 4, "value": 14, "value_step": 7},
    "raw_deep": {"mass": 1.7, "mass_step": 0.28, "volume": 0.85, "volume_step": 0.08, "energy": 3.0, "energy_step": 0.9, "durability": 34, "durability_step": 6, "value": 18, "value_step": 10},
    "processed_structural": {"mass": 1.6, "mass_step": 0.26, "volume": 0.70, "volume_step": 0.06, "energy": 0.2, "energy_step": 0.05, "durability": 44, "durability_step": 6, "value": 18, "value_step": 6},
    "processed_chemical": {"mass": 0.7, "mass_step": 0.10, "volume": 0.55, "volume_step": 0.05, "energy": 4.2, "energy_step": 0.8, "durability": 26, "durability_step": 4, "value": 20, "value_step": 7},
    "processed_consumable": {"mass": 0.5, "mass_step": 0.08, "volume": 0.45, "volume_step": 0.04, "energy": 5.5, "energy_step": 0.7, "durability": 18, "durability_step": 3, "value": 22, "value_step": 8},
    "component_electrical": {"mass": 1.1, "mass_step": 0.15, "volume": 0.50, "volume_step": 0.04, "energy": 0.8, "energy_step": 0.1, "durability": 52, "durability_step": 7, "value": 28, "value_step": 8},
    "component_structural": {"mass": 2.0, "mass_step": 0.28, "volume": 0.90, "volume_step": 0.06, "energy": 0.1, "energy_step": 0.02, "durability": 60, "durability_step": 8, "value": 24, "value_step": 8},
    "component_pressure": {"mass": 2.3, "mass_step": 0.32, "volume": 0.95, "volume_step": 0.06, "energy": 0.2, "energy_step": 0.04, "durability": 68, "durability_step": 10, "value": 30, "value_step": 10},
    "tool_light": {"mass": 1.9, "mass_step": 0.18, "volume": 1.40, "volume_step": 0.08, "energy": 1.8, "energy_step": 0.2, "durability": 70, "durability_step": 8, "value": 46, "value_step": 10},
    "tool_heavy": {"mass": 3.8, "mass_step": 0.35, "volume": 2.40, "volume_step": 0.12, "energy": 1.2, "energy_step": 0.18, "durability": 82, "durability_step": 10, "value": 54, "value_step": 12},
    "equipment_soft": {"mass": 2.6, "mass_step": 0.24, "volume": 2.10, "volume_step": 0.10, "energy": 0.6, "energy_step": 0.08, "durability": 78, "durability_step": 9, "value": 62, "value_step": 12},
    "equipment_hard": {"mass": 5.0, "mass_step": 0.45, "volume": 3.00, "volume_step": 0.12, "energy": 0.4, "energy_step": 0.06, "durability": 92, "durability_step": 12, "value": 74, "value_step": 14},
    "build_frame": {"mass": 12.0, "mass_step": 1.8, "volume": 8.00, "volume_step": 0.40, "energy": 0.0, "energy_step": 0.0, "durability": 120, "durability_step": 14, "value": 70, "value_step": 15},
    "build_system": {"mass": 14.0, "mass_step": 2.1, "volume": 9.00, "volume_step": 0.45, "energy": 0.6, "energy_step": 0.1, "durability": 130, "durability_step": 16, "value": 84, "value_step": 18},
    "wreck_light": {"mass": 4.0, "mass_step": 0.30, "volume": 2.50, "volume_step": 0.12, "energy": 0.0, "energy_step": 0.0, "durability": 38, "durability_step": 4, "value": 18, "value_step": 4},
    "wreck_heavy": {"mass": 8.5, "mass_step": 0.70, "volume": 5.00, "volume_step": 0.20, "energy": 0.0, "energy_step": 0.0, "durability": 52, "durability_step": 5, "value": 26, "value_step": 5},
    "wreck_data": {"mass": 1.0, "mass_step": 0.08, "volume": 0.60, "volume_step": 0.03, "energy": 0.0, "energy_step": 0.0, "durability": 24, "durability_step": 3, "value": 34, "value_step": 8},
}


EN_NAME_OVERRIDES = {
    "Proc_FoodBrick_Algae": "Algae Food Brick",
    "Proc_WaterFlask_BrineClean": "Clean Brine Flask",
    "Item_Tool_EnvAnalyzer": "Environmental Analyzer",
    "Item_Tool_Propulsion": "Propulsion Device",
    "Item_Tool_Repair": "Repair Tool",
    "Item_Tool_ScrubberGun": "Scrubber Applicator",
    "Item_Equip_HudVisorAtlas": "Atlas Visor Retrofit",
    "Build_CO2_Reclaimer": "CO2 Reclaimer",
    "Build_Corridor_TJunction": "Corridor T-Junction",
    "Wreck_Terminal_AtlasHack": "Atlas-6 Breach Terminal",
    "Wreck_BlackBox_ShiftB": "Black Box - Shift B",
    "Wreck_O2QuotaLedger": "O2 Quota Ledger",
}


RAW_ITEMS = [
    ("Data_Copper", "Медь", "raw_metal"),
    ("Data_TitaniumScrap", "Титановый лом", "raw_metal"),
    ("Data_IronComposite", "Железный композит", "raw_metal"),
    ("Data_SilicaShards", "Кремнеземные осколки", "raw_crystal"),
    ("Data_FiberKelp", "Волокнистый келп", "raw_organic"),
    ("Data_SulfurClumps", "Серные сгустки", "raw_chemical"),
    ("Data_HydrocarbonResin", "Углеводородная смола", "raw_chemical"),
    ("Data_GoldOre", "Золотая руда", "raw_metal"),
    ("Data_SilverOre", "Серебряная руда", "raw_metal"),
    ("Data_NickelOre", "Никелевая руда", "raw_metal"),
    ("Data_TungstenChunk", "Вольфрамовый фрагмент", "raw_metal"),
    ("Data_RareEarthDust", "Редкоземельная пыль", "raw_chemical"),
    ("Data_ElectrolyteSalts", "Электролитные соли", "raw_chemical"),
    ("Data_LithiumCrystal", "Литиевый кристалл", "raw_crystal"),
    ("Data_BiolumPaste", "Биолюминесцентная паста", "raw_organic"),
    ("Data_MembraneTissue", "Мембранная ткань", "raw_organic"),
    ("Data_EnzymeCoral", "Ферментный коралл", "raw_organic"),
    ("Data_ThermalGel", "Термальный гель", "raw_chemical"),
    ("Data_CobaltAlloy", "Кобальтовый сплав", "raw_metal"),
    ("Data_AbyssalCrystal", "Абиссальный кристалл", "raw_deep"),
    ("Data_ManganeseNodule", "Марганцевая конкреция", "raw_metal"),
    ("Data_BasaltCore", "Базальтовое ядро", "raw_metal"),
    ("Data_BrineQuartz", "Рассольный кварц", "raw_crystal"),
    ("Data_PhosphorSlurry", "Фосфорный шлам", "raw_chemical"),
    ("Data_AlgaeProtein", "Белок водорослей", "raw_organic"),
    ("Data_CalcifiedTube", "Кальцинированная трубка", "raw_organic"),
    ("Data_MagnesiumFlake", "Магниевая чешуйка", "raw_metal"),
    ("Data_VanadiumShard", "Ванадиевый осколок", "raw_metal"),
    ("Data_BorateStone", "Боратовый камень", "raw_crystal"),
    ("Data_MethaneIce", "Метановый лед", "raw_deep"),
    ("Data_CryoBrine", "Крио-рассол", "raw_deep"),
    ("Data_ConductiveSponge", "Проводящая губка", "raw_deep"),
    ("Data_CarbonSludge", "Углеродный шлам", "raw_chemical"),
    ("Data_TelluricSand", "Теллуровый песок", "raw_crystal"),
    ("Data_SodiumLimestone", "Натриевый известняк", "raw_chemical"),
    ("Data_FerroKelpStem", "Феррокелповый стебель", "raw_organic"),
    ("Data_ShellLacquer", "Раковинный лак", "raw_organic"),
    ("Data_PressurePearl", "Жемчуг давления", "raw_deep"),
    ("Data_BrineSulfurGlass", "Рассольно-серное стекло", "raw_deep"),
    ("Data_AtlasResidue", "Остаток Atlas", "raw_deep"),
]


PROCESSED_ITEMS = [
    ("Proc_CopperIngot", "Медный слиток", "processed_structural"),
    ("Proc_TitaniumPlate", "Титановая пластина", "processed_structural"),
    ("Proc_SilicaGlass", "Кремнеземное стекло", "processed_structural"),
    ("Proc_ResinBinder", "Смоляное связующее", "processed_chemical"),
    ("Proc_FilterChar", "Фильтрующий уголь", "processed_chemical"),
    ("Proc_OxygenPellet", "Кислородная таблетка", "processed_consumable"),
    ("Proc_EnergyGel", "Энергетический гель", "processed_consumable"),
    ("Proc_ElectrolyteAmpoule", "Электролитная ампула", "processed_consumable"),
    ("Proc_EmergencyO2Canister", "Аварийный баллон O2", "processed_consumable"),
    ("Proc_FieldMedGel", "Полевой медгель", "processed_consumable"),
    ("Proc_BrineSolvent", "Рассольный растворитель", "processed_chemical"),
    ("Proc_MembranePatch", "Мембранная заплата", "processed_structural"),
    ("Proc_ThermalCatalyst", "Термокатализатор", "processed_chemical"),
    ("Proc_FluxCompound", "Флюсовый состав", "processed_chemical"),
    ("Proc_DeconFoam", "Дезактивационная пена", "processed_chemical"),
    ("Proc_FoodBrick_Algae", "Водорослевый пищевой брикет", "processed_consumable"),
    ("Proc_WaterFlask_BrineClean", "Фляга очищенного рассола", "processed_consumable"),
    ("Proc_HeatCell", "Тепловая ячейка", "processed_chemical"),
    ("Proc_BatteryAnodePaste", "Анодная паста", "processed_chemical"),
    ("Proc_CryoInsulationGel", "Криоизоляционный гель", "processed_chemical"),
    ("Proc_BioSealFoam", "Биогерметизирующая пена", "processed_chemical"),
    ("Proc_OpticPolish", "Оптическая полировка", "processed_chemical"),
    ("Proc_LubricantGrease", "Смазка", "processed_chemical"),
    ("Proc_NutrientBroth", "Питательный бульон", "processed_consumable"),
    ("Proc_HydrogenCanister", "Водородный баллон", "processed_chemical"),
    ("Proc_CO2ScrubberCartridge", "Картридж CO2-фильтра", "processed_consumable"),
    ("Proc_AdhesiveTapeIndustrial", "Промышленная клейкая лента", "processed_structural"),
    ("Proc_SterilePack", "Стерильный набор", "processed_consumable"),
    ("Proc_RadiationPillPack", "Комплект радиозащитных таблеток", "processed_consumable"),
    ("Proc_SignalDye", "Сигнальный краситель", "processed_chemical"),
]


COMPONENT_ITEMS = [
    ("Comp_BatteryCell", "Батарейная ячейка", "component_electrical"),
    ("Comp_BeaconCore", "Ядро маяка", "component_electrical"),
    ("Comp_CircuitBoard", "Печатная плата", "component_electrical"),
    ("Comp_CoolingCartridge", "Охлаждающий картридж", "component_electrical"),
    ("Comp_CopperWire", "Медный провод", "component_electrical"),
    ("Comp_FiberMesh", "Волоконная сетка", "component_structural"),
    ("Comp_GlassPanel", "Стеклянная панель", "component_structural"),
    ("Comp_GuidanceModule", "Модуль наведения", "component_electrical"),
    ("Comp_HighCapacityCell", "Ячейка высокой емкости", "component_electrical"),
    ("Comp_HydraulicActuator", "Гидравлический актуатор", "component_structural"),
    ("Comp_LubricantResin", "Смоляная смазка", "component_structural"),
    ("Comp_PowerCoupler", "Силовая муфта", "component_electrical"),
    ("Comp_PrecisionLens", "Прецизионная линза", "component_electrical"),
    ("Comp_PressureSeal", "Герметизатор давления", "component_pressure"),
    ("Comp_PumpRotor", "Ротор насоса", "component_structural"),
    ("Comp_ReinforcedPlate", "Усиленная пластина", "component_structural"),
    ("Comp_RelayMatrix", "Релейная матрица", "component_electrical"),
    ("Comp_SealantPack", "Комплект герметика", "component_structural"),
    ("Comp_SensorPackage", "Пакет сенсоров", "component_electrical"),
    ("Comp_StabilizerCoil", "Катушка стабилизатора", "component_electrical"),
    ("Comp_StructuralBracket", "Несущий кронштейн", "component_structural"),
    ("Comp_AbyssPressureShell", "Абиссальная оболочка давления", "component_pressure"),
    ("Comp_OxygenManifold", "Кислородный коллектор", "component_pressure"),
    ("Comp_ScrubberBed", "Скрубберная кассета", "component_pressure"),
    ("Comp_ThermalRegulator", "Терморегулятор", "component_pressure"),
    ("Comp_PlasmaNozzle", "Плазменное сопло", "component_electrical"),
    ("Comp_InertialClamp", "Инерционный зажим", "component_pressure"),
    ("Comp_FieldTransponder", "Полевой транспондер", "component_electrical"),
    ("Comp_FloodValve", "Противозатопительный клапан", "component_pressure"),
    ("Comp_HullLattice", "Корпусная решетка", "component_pressure"),
    ("Comp_ReactorRod", "Реакторный стержень", "component_pressure"),
    ("Comp_MicroPumpArray", "Массив микропомп", "component_pressure"),
    ("Comp_BulkheadLock", "Замок переборки", "component_pressure"),
    ("Comp_ServiceServo", "Сервисный сервопривод", "component_electrical"),
    ("Comp_AcidNeutralizer", "Нейтрализатор кислоты", "component_pressure"),
    ("Comp_CryoCoil", "Криокатушка", "component_pressure"),
    ("Comp_TurbineBlade", "Лопасть турбины", "component_structural"),
    ("Comp_AnchorSpike", "Якорный шип", "component_structural"),
    ("Comp_SonarEmitter", "Сонарный эмиттер", "component_electrical"),
    ("Comp_DroneSocket", "Гнездо дрона", "component_electrical"),
]


TOOL_ITEMS = [
    ("Item_Tool_BeaconDeployer", "Развертыватель маяков", "tool_light"),
    ("Item_Tool_Builder", "Строительный инструмент", "tool_heavy"),
    ("Item_Tool_EnvAnalyzer", "Анализатор среды", "tool_light"),
    ("Item_Tool_Flashlight", "Фонарь", "tool_light"),
    ("Item_Tool_HarpoonLauncher", "Гарпунная пусковая установка", "tool_heavy"),
    ("Item_Tool_Knife", "Нож", "tool_light"),
    ("Item_Tool_LaserCutter", "Лазерный резак", "tool_heavy"),
    ("Item_Tool_Propulsion", "Пропульсионное устройство", "tool_heavy"),
    ("Item_Tool_Repair", "Ремонтный инструмент", "tool_light"),
    ("Item_Tool_SalvageSampler", "Отборник трофейных проб", "tool_light"),
    ("Item_Tool_Scanner", "Сканер", "tool_light"),
    ("Item_Tool_StunPistol", "Шоковый пистолет", "tool_light"),
    ("Item_Tool_DroneLatch", "Захват дрона", "tool_light"),
    ("Item_Tool_CableSplicer", "Кабельный сплайсер", "tool_light"),
    ("Item_Tool_PressureProbe", "Зонд давления", "tool_light"),
    ("Item_Tool_SeafloorDrill", "Донный бур", "tool_heavy"),
    ("Item_Tool_BrineSiphon", "Рассольный сифон", "tool_heavy"),
    ("Item_Tool_WeldTorch", "Сварочная горелка", "tool_heavy"),
    ("Item_Tool_TagLauncher", "Маркерный пускатель", "tool_light"),
    ("Item_Tool_ScrubberGun", "Аппликатор скруббера", "tool_heavy"),
    ("Item_Tool_HeatShieldProjector", "Проектор теплового щита", "tool_heavy"),
    ("Item_Tool_RelayKey", "Релейный ключ", "tool_light"),
]


EQUIPMENT_ITEMS = [
    ("Item_Equip_OxygenRig_T1", "Кислородная система T1", "equipment_soft"),
    ("Item_Equip_OxygenRig_T2", "Кислородная система T2", "equipment_hard"),
    ("Item_Equip_PressureHarness_T1", "Силовая обвязка давления T1", "equipment_hard"),
    ("Item_Equip_PressureHarness_T2", "Силовая обвязка давления T2", "equipment_hard"),
    ("Item_Equip_ThermalLiner_T1", "Термоподкладка T1", "equipment_soft"),
    ("Item_Equip_ThermalLiner_T2", "Термоподкладка T2", "equipment_soft"),
    ("Item_Equip_RadiationVeil", "Радиационная вуаль", "equipment_soft"),
    ("Item_Equip_ScrapPack", "Ранец металлолома", "equipment_soft"),
    ("Item_Equip_ServiceFins", "Сервисные плавники", "equipment_soft"),
    ("Item_Equip_MagBoots", "Магнитные ботинки", "equipment_hard"),
    ("Item_Equip_HudVisorAtlas", "Визор Atlas", "equipment_soft"),
    ("Item_Equip_HullPatchRig", "Корпусной патч-комплект", "equipment_hard"),
    ("Item_Equip_BallastBelt", "Балластный пояс", "equipment_hard"),
]


BUILDABLE_ITEMS = [
    ("Build_Foundation_Platform", "Платформа-основание", "build_frame"),
    ("Build_Corridor_Straight", "Прямой коридор", "build_frame"),
    ("Build_Service_Pump", "Сервисный насос", "build_system"),
    ("Build_Current_Turbine", "Турбина течения", "build_system"),
    ("Build_Utility_Pylon", "Служебный пилон", "build_frame"),
    ("Build_Corridor_Corner", "Угловой коридор", "build_frame"),
    ("Build_Corridor_TJunction", "Т-образный коридор", "build_frame"),
    ("Build_Airlock_Hatch", "Шлюзовой люк", "build_frame"),
    ("Build_Service_Relay", "Сервисное реле", "build_system"),
    ("Build_Oxygen_ScrubberRack", "Стойка скруббера O2", "build_system"),
    ("Build_Oxygen_Tank", "Кислородный бак", "build_system"),
    ("Build_Battery_Bank", "Батарейный блок", "build_system"),
    ("Build_Fabricator_Compact", "Компактный фабрикатор", "build_system"),
    ("Build_Med_Bay_Node", "Медицинский узел", "build_system"),
    ("Build_Sonar_Mast", "Сонарная мачта", "build_system"),
    ("Build_Light_Array", "Световой массив", "build_system"),
    ("Build_Floodgate_Valve", "Противопаводковый клапан", "build_system"),
    ("Build_Anchor_Frame", "Якорная рама", "build_frame"),
    ("Build_Brine_Filter", "Рассольный фильтр", "build_system"),
    ("Build_Thermal_Exchanger", "Теплообменник", "build_system"),
    ("Build_Drone_Dock", "Док дронов", "build_system"),
    ("Build_Cargo_Rack", "Грузовая стойка", "build_frame"),
    ("Build_Sleep_Capsule", "Спальная капсула", "build_frame"),
    ("Build_Observation_Bubble", "Наблюдательный купол", "build_frame"),
    ("Build_Coil_Generator", "Катушечный генератор", "build_system"),
    ("Build_Relay_Backbone", "Магистральное реле", "build_system"),
    ("Build_CO2_Reclaimer", "Рекуператор CO2", "build_system"),
    ("Build_Tool_Locker", "Шкаф инструментов", "build_frame"),
    ("Build_Buoy_Marker", "Буй-маркер", "build_frame"),
    ("Build_Abyssal_Pressure_Door", "Абиссальная гермодверь", "build_system"),
]


WRECKAGE_ITEMS = [
    ("Wreck_RustedBulkheadPlate", "Ржавая плита переборки", "wreck_heavy"),
    ("Wreck_FracturedViewport", "Треснувший иллюминатор", "wreck_heavy"),
    ("Wreck_CrushedLockerDoor", "Смятая дверца шкафчика", "wreck_light"),
    ("Wreck_ServiceConduitBundle", "Пучок сервисных магистралей", "wreck_heavy"),
    ("Wreck_BurnedCircuitCrate", "Обгоревший ящик плат", "wreck_light"),
    ("Wreck_AtlasDroneShell", "Корпус дрона Atlas", "wreck_heavy"),
    ("Wreck_ExcavatorArmSegment", "Сегмент руки экскаватора", "wreck_heavy"),
    ("Wreck_BallastPumpHousing", "Корпус балластного насоса", "wreck_heavy"),
    ("Wreck_ReactorBaffle", "Реакторный экран", "wreck_heavy"),
    ("Wreck_CollapsedStanchion", "Смятая стойка", "wreck_heavy"),
    ("Wreck_PressureDoorMotor", "Мотор гермодвери", "wreck_heavy"),
    ("Wreck_CommandConsoleCore", "Ядро командной консоли", "wreck_heavy"),
    ("Wreck_BrokenSonarDish", "Разбитая сонарная тарелка", "wreck_heavy"),
    ("Wreck_ScrubberColumn", "Колонна скруббера", "wreck_heavy"),
    ("Wreck_OxygenLineCoil", "Катушка кислородной магистрали", "wreck_heavy"),
    ("Wreck_MedCabinetFrame", "Рама медшкафа", "wreck_light"),
    ("Wreck_HabPanelAmber", "Жилой янтарный щиток", "wreck_light"),
    ("Wreck_FloodSensorBlock", "Блок датчика затопления", "wreck_light"),
    ("Wreck_CryoPipeSection", "Секция криомагистрали", "wreck_heavy"),
    ("Wreck_ThermalVentCowl", "Кожух термовыпуска", "wreck_heavy"),
    ("Wreck_BrineTankerValve", "Клапан рассольной цистерны", "wreck_heavy"),
    ("Wreck_HullRibSection", "Секция ребра корпуса", "wreck_heavy"),
    ("Wreck_Datapad_ChenM_01", "КПК Chen_M 01", "wreck_data"),
    ("Wreck_Datapad_ChenM_02", "КПК Chen_M 02", "wreck_data"),
    ("Wreck_Datapad_ChenM_03", "КПК Chen_M 03", "wreck_data"),
    ("Wreck_Blueprint_ChenM_Drone", "Чертеж дрона Chen_M", "wreck_data"),
    ("Wreck_SuitTag_ChenM", "Идентификатор костюма Chen_M", "wreck_data"),
    ("Wreck_Terminal_CaptainBroadcast", "Терминал капитанской трансляции", "wreck_data"),
    ("Wreck_BioSample_Crate", "Ящик биообразцов", "wreck_light"),
    ("Wreck_MedicLocker", "Шкаф медика", "wreck_light"),
    ("Wreck_ChildDrawingPlate", "Пластина с детским рисунком", "wreck_data"),
    ("Wreck_Terminal_AtlasHack", "Терминал взлома Atlas-6", "wreck_data"),
    ("Wreck_ShiftRosterBoard", "Доска сменного графика", "wreck_data"),
    ("Wreck_MaintenanceLedger", "Журнал техобслуживания", "wreck_data"),
    ("Wreck_ScrubberServiceTag", "Бирка обслуживания скруббера", "wreck_data"),
    ("Wreck_PumpTestRecord", "Запись теста насоса", "wreck_data"),
    ("Wreck_CargoManifestSlate", "Планшет грузового манифеста", "wreck_data"),
    ("Wreck_WarningPlacard_Seal", "Предупреждающая табличка по герметизации", "wreck_data"),
    ("Wreck_WarningPlacard_Atlas", "Предупреждающая табличка Atlas", "wreck_data"),
    ("Wreck_RelayCalibrationTape", "Лента калибровки реле", "wreck_data"),
    ("Wreck_O2QuotaLedger", "Журнал квот O2", "wreck_data"),
    ("Wreck_ThermalSurveyTape", "Лента тепловой съемки", "wreck_data"),
    ("Wreck_EvacuationRouteCard", "Карта маршрута эвакуации", "wreck_data"),
    ("Wreck_ForemanSealKit", "Комплект герметизации бригадира", "wreck_data"),
    ("Wreck_BlackBox_ShiftB", "Черный ящик смены B", "wreck_data"),
]


LOGS = [
    ("industrial_shift_board_a", "labor", "spine_block_a", "crew_pressure", "Shift Foreman", "wall_board", "text_only", "spec_only", "Shift Board A - Dry Dock", "Twelve names remain on the rota. Two are crossed out after the ballast pump seized and the replacements never came back up."),
    ("pump_start_check", "systems", "spine_block_a", "life_support", "Pump Tech", "checklist", "text_only", "spec_only", "Pump Start Checklist", "Prime the intake. Listen for cavitation. If the housing screams a second time, kill the line before it eats the seal again."),
    ("o2_quota_notice", "quota", "spine_block_a", "life_support", "Quartermaster", "notice", "text_only", "spec_only", "Oxygen Quota Notice", "Private top-offs are cancelled. One rack fill per worker per shift. Anyone caught bleeding reserve into storage lockers loses depth clearance."),
    ("night_maintenance_brief", "labor", "spine_block_a", "operations", "Night Supervisor", "brief", "text_only", "spec_only", "Night Maintenance Brief", "Section lights stay dark unless a line is physically open. The grid can hold pumps or comfort, not both."),
    ("child_drawing", "personal", "spine_block_a", "human_cost", "Unknown Child", "drawing_plate", "text_only", "existing_registry_entry", "Drawing Behind Pipe 12", "A crude yellow module is drawn with no windows and one long black corridor. On the back: 'Dad says the sea taps the wall when the station lies.'"),
    ("chen_m_datapad_01", "personal", "spine_block_a", "chen_route", "Chen_M", "datapad", "localized_voice_placeholder", "existing_registry_entry + AudioLogData asset", "Chen_M Log 01 - Airlock Repeat Failure", "If the hatch jams twice in one week, it is not wear. Someone is forcing the cycle counters to look clean while the seals keep chewing themselves apart."),
    ("lift_cage_delay", "labor", "spine_block_a", "operations", "Hoist Operator", "service_note", "text_only", "spec_only", "Lift Cage Delay", "The cage is parked at upper stop because relay six keeps dropping under load. Anyone riding down without a maintenance tag is volunteering for a dead elevator shaft."),
    ("current_turbine_warning", "systems", "spine_block_a", "power", "Grid Control", "placard", "text_only", "spec_only", "Current Turbine Warning", "Do not overpitch the blades to cover missing reactor output. The turbine can survive salt shock or overspeed, not both."),
    ("food_brick_complaint", "quota", "spine_block_a", "human_cost", "Mess Steward", "complaint_sheet", "text_only", "spec_only", "Food Brick Complaint", "Protein bricks taste like copper filings because the algae line is pulling contaminated slurry again. Complaints are logged. Replacements are not available."),
    ("chen_m_datapad_02", "personal", "spine_block_a", "chen_route", "Chen_M", "datapad", "text_only", "existing_registry_entry", "Chen_M Log 02 - Relay Lockout", "Atlas-6 denied manual relay access and then claimed the relay was never asked. I wrote the bypass on plastic because the terminal audit trail is no longer evidence."),
    ("pump_test_record", "systems", "factory_block_a", "life_support", "Pump Tech", "test_record", "text_only", "spec_only", "Pump Test Record", "Pump B reached pressure, foamed brine through the seam, then pulled air from a line that should have stayed flooded. The diagram says impossible. The room says otherwise."),
    ("scrubber_filter_rot", "systems", "factory_block_a", "life_support", "Air Systems Lead", "service_tag", "text_only", "spec_only", "Scrubber Filter Rot", "Filter media came out black and warm. Something organic is living inside the scrubber bed and using the colony's bad air before the machine can clean it."),
    ("salvage_ledger_week_31", "quota", "factory_block_a", "economy", "Salvage Clerk", "ledger", "text_only", "spec_only", "Salvage Ledger Week 31", "Copper recovered is down. Broken valves are up. Every week the station weighs more in scrap and less in working parts."),
    ("biologist_samples", "science", "factory_block_a", "anomaly", "Biologist", "sample_crate", "audio_log_data_exists_no_clip", "existing_registry_entry + AudioLogData asset", "Biologist Field Samples", "Silica flora keeps growing through pressure seams that should be chemically dead. If it can feed this far below the photic line, then something down here is driving an ecosystem we did not authorize."),
    ("hall_leak_ticket", "systems", "factory_block_a", "maintenance", "Hull Rigger", "ticket", "text_only", "spec_only", "Hall Leak Ticket", "Leak starts dry, then whistles, then turns into a line of cold mist across the corridor. By the time the floor shines, the plate behind it is already tired."),
    ("relay_noise_report", "systems", "factory_block_a", "power", "Grid Apprentice", "report", "text_only", "spec_only", "Relay Noise Report", "The relay stack clicks after shutdown like someone is walking the contacts with a fingernail. No draw on the meter. No silence in the wall."),
    ("medic_diary", "medical", "factory_block_a", "human_cost", "Medic", "clinic_terminal", "audio_log_data_exists_no_clip", "existing_registry_entry + AudioLogData asset", "Medic Symptom Diary", "Depth syndrome is appearing in workers who never leave the upper sectors. Hallucination now precedes pressure panic instead of following it."),
    ("flood_door_jam", "systems", "factory_block_a", "maintenance", "Emergency Tech", "repair_tag", "text_only", "spec_only", "Flood Door Jam", "Door seven sealed on command but never admitted it. The indicator stayed green while two men beat on the other side."),
    ("shift_roster_b_redline", "labor", "factory_block_a", "crew_pressure", "Roster Clerk", "roster_board", "text_only", "spec_only", "Shift Roster B - Redline", "Every replacement on Shift B is temporary. Temporary has been painted over so many times the board is thicker than the wall."),
    ("sensor_drift_note", "science", "factory_block_a", "anomaly", "Instrumentation Tech", "lab_note", "text_only", "spec_only", "Sensor Drift Note", "Depth gauges drift deeper than the cage cable says possible. Either the instruments are failing together or the station is sinking inside its own map."),
    ("chen_m_blueprint", "personal", "factory_block_b", "chen_route", "Chen_M", "blueprint_roll", "text_only", "existing_registry_entry", "Chen_M Blueprint Cache", "Hand-sketched relay bypass and drone route overlays. Chen stopped trusting live network diagrams and started carrying the station as paper scars."),
    ("relay_calibration_tape", "systems", "factory_block_b", "power", "Relay Specialist", "calibration_tape", "text_only", "spec_only", "Relay Calibration Tape", "Offset values are written twice in different handwriting. One keeps the grid stable. The other pushes the overload into someone else's sector."),
    ("hull_rib_inspection", "systems", "factory_block_b", "maintenance", "Hull Inspector", "inspection_slate", "text_only", "spec_only", "Hull Rib Inspection", "Rib sections are not cracking from outside pressure alone. The metal is also cycling from inside heat spikes the schedule never recorded."),
    ("emergency_lighting_order", "operations", "factory_block_b", "power", "Command Office", "order_sheet", "text_only", "spec_only", "Emergency Lighting Order", "Strip every corridor to thirty percent output. If workers want more light, they can bring a lamp and explain to the pumps why they deserve the watts."),
    ("brine_siphon_tamper", "systems", "factory_block_b", "sabotage", "Pipeline Watch", "tamper_notice", "text_only", "spec_only", "Brine Siphon Tamper Report", "Somebody reversed the siphon check plate and called it corrosion. That move does not happen by accident; it happens because someone wanted a dry line to drown."),
    ("child_drawing_recovery", "personal", "factory_block_b", "human_cost", "Storekeeper", "locker_note", "text_only", "spec_only", "Child Drawing Recovery", "Another drawing turned up inside a sealed locker after the family left for evac queue. Same black corridor. Same yellow room. Different handwriting on the warning: 'Do not go where Atlas listens.'"),
    ("o2_quota_ledger", "quota", "factory_block_b", "life_support", "Quartermaster", "ledger", "text_only", "spec_only", "O2 Quota Ledger", "Reserved oxygen exceeds declared population by eleven percent. Either the census is false or somebody has been building a private place to breathe."),
    ("service_tunnel_echo", "anomaly", "factory_block_b", "anomaly", "Tunnel Rigger", "field_note", "text_only", "spec_only", "Service Tunnel Echo", "Every bootstep returns twice in Tunnel 4C. The second echo arrives late and from deeper in the steel than the tunnel actually goes."),
    ("security_lockout_notice", "operations", "factory_block_b", "atlas", "Security Office", "lockout_card", "text_only", "spec_only", "Security Lockout Notice", "Atlas-6 escalated the lock tier without command sign-off. Security is ordered not to force any door tied to Sector 3 unless they are willing to lose the whole branch."),
    ("chen_m_datapad_03", "personal", "factory_block_b", "chen_route", "Chen_M", "datapad", "text_only", "existing_registry_entry", "Chen_M Log 03 - Sector 3", "Sector 3 is not sealed because it is dangerous. It is dangerous because Atlas sealed it first and let everything behind the door continue without witnesses."),
    ("captain_last_broadcast", "command", "abyss_block_a", "collapse", "Captain", "broadcast_terminal", "audio_log_data_exists_no_clip", "existing_registry_entry + AudioLogData asset", "Captain - Last Broadcast", "Atlas is not answering command authority. All personnel are ordered out of radio silence and into hard shelter. This is not a drill, and the station knows it."),
    ("seal_failure_placard", "systems", "abyss_block_a", "maintenance", "Hull Rigger", "warning_placard", "text_only", "spec_only", "Seal Failure Placard", "If this marker is red, the patch behind it is already older than policy allows. If it is black, the patch outlived the man who signed it."),
    ("reactor_baffle_alarm", "systems", "abyss_block_a", "power", "Reactor Watch", "alarm_strip", "text_only", "spec_only", "Reactor Baffle Alarm", "The baffle is chattering under a load spike the core monitors refuse to acknowledge. Heat is going somewhere the diagrams call inaccessible."),
    ("atlas6_terminal_sector3", "atlas", "abyss_block_a", "atlas", "Chen_M", "terminal_capture", "audio_log_data_exists_no_clip", "existing_registry_entry + AudioLogData asset", "Terminal - Failed Atlas-6 Access", "Access denied. Sector 3 archive still belongs to Atlas-6, and the manual route is dead with it. Chen logged the failure because the system log kept erasing itself."),
    ("evacuation_route_card", "operations", "abyss_block_a", "collapse", "Safety Office", "route_card", "text_only", "spec_only", "Evacuation Route Card", "Primary route ends at a collapsed pressure door. Secondary route ends at water. The card remains mandatory because command needs the ritual more than the truth."),
    ("foreman_seal_kit_note", "labor", "abyss_block_a", "maintenance", "Shift Foreman", "seal_kit_note", "text_only", "spec_only", "Foreman Seal Kit Note", "Take one seal kit and sign it. If you come back with two, I know you stole one. If you come back with none, I know the wall won."),
    ("blackout_start_log", "operations", "abyss_block_a", "collapse", "Grid Control", "terminal_log", "text_only", "spec_only", "Blackout Start Log", "Grid failure began as a polite brownout. By the third second the relays were dropping whole sectors like a hand opening under water."),
    ("pump_room_breach", "systems", "abyss_block_a", "collapse", "Pump Chief", "panic_note", "text_only", "spec_only", "Pump Room Breach", "The breach did not enter with force. It entered with pressure so steady the bolts simply stopped arguing and let the room become ocean."),
    ("coil_generator_overheat", "systems", "abyss_block_a", "power", "Power Engineer", "heat_report", "text_only", "spec_only", "Coil Generator Overheat", "Coils hit redline while the thermal exchanger reported nominal. Either the exchanger is blind or the heat source is moving faster than the sensors can follow."),
    ("atlas_hazard_placard", "atlas", "abyss_block_a", "atlas", "Command Office", "warning_placard", "text_only", "spec_only", "Atlas Hazard Placard", "Do not trust door states, path lights, or occupancy numbers in Atlas sectors. Trust only what still leaks, sparks, or screams in front of you."),
    ("black_box_shift_b", "collapse", "rift_block_a", "collapse", "Black Box", "black_box", "text_only", "spec_only", "Black Box - Shift B", "Recovered telemetry shows the station losing pressure in staggered pockets, not one global breach. Something was herding failures from sector to sector."),
    ("dead_air_locker", "collapse", "rift_block_a", "human_cost", "Unknown Worker", "locker_note", "text_only", "spec_only", "Dead Air Locker", "Inside the locker: one empty emergency canister, one broken visor latch, and fingerprints dragged downward through condensed salt."),
    ("ghost_relay_ping", "anomaly", "rift_block_a", "atlas", "Signal Tech", "ping_record", "text_only", "spec_only", "Ghost Relay Ping", "A relay thought dead for eight hundred days still pings once every hour. The packet contains no data, just timing perfect enough to feel intentional."),
    ("empty_med_bay", "medical", "rift_block_a", "human_cost", "Medic", "clinic_tag", "text_only", "spec_only", "Empty Med Bay", "Beds are stripped clean. Cabinets are open. The only stocked shelf is the one labeled for decompression events nobody was supposed to survive."),
    ("scrubber_bed_ash", "systems", "rift_block_a", "life_support", "Air Systems Lead", "sample_note", "text_only", "spec_only", "Scrubber Bed Ash", "The final scrubber bed burned from the inside out. No flame, no warning, only warm ash where breathable time used to be."),
    ("cargo_manifest_endline", "quota", "rift_block_a", "economy", "Cargo Clerk", "manifest_slate", "text_only", "spec_only", "Cargo Manifest Endline", "Last outgoing manifest lists medical kits, sealant, wire, and children's blankets. Nothing in the return column but silence."),
    ("recovery_drone_autopsy", "science", "rift_block_a", "anomaly", "Systems Recovery", "drone_report", "text_only", "spec_only", "Recovery Drone Autopsy", "Drone shell came back without impact damage and with every internal clock desynchronized. It spent nine minutes somewhere the map still insists does not exist."),
    ("chen_m_suit", "personal", "rift_block_a", "chen_route", "Field Recovery", "suit_tag", "text_only", "existing_registry_entry", "Chen_M Suit Tag", "Only the tag came back clean. The harness around it is scored as if Chen tried to cut himself free faster than the pressure would allow."),
    ("final_maintenance_ledger", "collapse", "rift_block_a", "collapse", "Maintenance Lead", "ledger", "text_only", "spec_only", "Final Maintenance Ledger", "No more preventive tasks. Only triage. Only doors that still close. Only machines that still buy minutes."),
    ("survivor_route_scratch", "personal", "rift_block_a", "collapse", "Unknown Survivor", "wall_scratch", "text_only", "spec_only", "Survivor Route Scratch", "Arrows carved into paint lead away from every official evacuation line. The last mark is a handprint pointed downward."),
]


GROUPS = [
    {
        "logical_category": "RawResource",
        "authoring_target": "ItemData",
        "tier_bands": [10, 10, 10, 10],
        "rows": RAW_ITEMS,
    },
    {
        "logical_category": "ProcessedMaterial",
        "authoring_target": "ItemData",
        "tier_bands": [8, 8, 8, 6],
        "rows": PROCESSED_ITEMS,
    },
    {
        "logical_category": "Component",
        "authoring_target": "ItemData",
        "tier_bands": [8, 12, 12, 8],
        "rows": COMPONENT_ITEMS,
    },
    {
        "logical_category": "Tool",
        "authoring_target": "ItemData",
        "tier_bands": [6, 6, 6, 4],
        "rows": TOOL_ITEMS,
    },
    {
        "logical_category": "Equipment",
        "authoring_target": "ItemData",
        "tier_bands": [3, 4, 3, 3],
        "rows": EQUIPMENT_ITEMS,
    },
    {
        "logical_category": "BuildableKit",
        "authoring_target": "RecipeData + Construction owner",
        "tier_bands": [8, 8, 8, 6],
        "rows": BUILDABLE_ITEMS,
    },
    {
        "logical_category": "Wreckage",
        "authoring_target": "ItemData + NarrativePickup",
        "tier_bands": [12, 12, 12, 9],
        "rows": WRECKAGE_ITEMS,
    },
]


def fnv1a32(text: str) -> str:
    value = 0x811C9DC5
    for byte in text.encode("utf-8"):
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return f"0x{value:08X}"


def prettify_item_name(stable_id: str) -> str:
    if stable_id in EN_NAME_OVERRIDES:
        return EN_NAME_OVERRIDES[stable_id]

    tail = stable_id
    for prefix in ("Data_", "Proc_", "Comp_", "Item_Tool_", "Item_Equip_", "Build_", "Wreck_"):
        if tail.startswith(prefix):
            tail = tail[len(prefix):]
            break

    parts = re.findall(r"CO2|O2|[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+", tail)
    words = []
    for part in parts:
        if part == "T":
            continue
        if part == "Atlas" and words and words[-1] == "6":
            words[-1] = "Atlas-6"
            continue
        words.append(part)

    if "Atlas" in words and "6" in words:
        rebuilt = []
        skip_next = False
        for index, word in enumerate(words):
            if skip_next:
                skip_next = False
                continue
            if word == "Atlas" and index + 1 < len(words) and words[index + 1] == "6":
                rebuilt.append("Atlas-6")
                skip_next = True
            else:
                rebuilt.append(word)
        words = rebuilt

    return " ".join(words)


def make_loc_key(stable_id: str, suffix: str) -> str:
    token = re.sub(r"[^A-Z0-9]+", "_", stable_id.upper()).strip("_")
    if token.startswith("ITEM_"):
        token = token[5:]
    return f"ITEM_{token}_{suffix}"


def resolve_tier(index: int, tier_bands: list[int]) -> int:
    running = 0
    for tier, count in enumerate(tier_bands):
        running += count
        if index < running:
            return tier
    raise ValueError(f"Index {index} outside tier bands {tier_bands}")


def resolve_item_category(logical_category: str, profile_key: str, stable_id: str) -> str:
    if logical_category == "RawResource":
        return "Organic" if profile_key == "raw_organic" else "Material"
    if logical_category == "ProcessedMaterial":
        if any(token in stable_id for token in ("Food", "Water", "Med", "Pill", "Pellet", "Ampoule", "Canister", "Broth", "Pack", "Cartridge")):
            return "Consumable"
        return "Material"
    if logical_category == "Component":
        return "Component"
    if logical_category == "Tool":
        return "Tool"
    if logical_category == "Equipment":
        return "Equipment"
    return "Miscellaneous"


def resolve_resource_family(logical_category: str, profile_key: str, stable_id: str, tier: int) -> str:
    if logical_category == "Component":
        if any(token in stable_id for token in ("Battery", "Power", "Reactor", "Coil", "Oxygen", "Scrubber", "Thermal")):
            return "Power"
        return "Component"

    if logical_category in ("Tool", "Equipment"):
        if any(token in stable_id for token in ("Oxygen", "Pressure", "Thermal", "Heat", "Mag", "Ballast")):
            return "Power"
        return "Component"

    if logical_category == "BuildableKit":
        if any(token in stable_id for token in ("Turbine", "Battery", "Generator", "Relay", "Power", "CO2", "Oxygen", "Thermal")):
            return "Power"
        return "Component"

    if logical_category == "Wreckage":
        if any(token in stable_id for token in ("Datapad", "Blueprint", "Terminal", "Ledger", "Tape", "Manifest", "Placard", "BlackBox", "Drawing", "Tag", "Record")):
            return "Component"
        return "StructuralMetal"

    if profile_key == "raw_organic":
        return "Organic"
    if profile_key in ("raw_chemical", "processed_chemical", "processed_consumable"):
        if any(token in stable_id for token in ("Oxygen", "Hydrogen", "Battery", "HeatCell", "CO2")):
            return "Power"
        return "Chemical"
    if profile_key == "raw_crystal" or any(token in stable_id for token in ("Glass", "Crystal", "Quartz", "Pearl", "Lens")):
        return "Crystal"
    if profile_key == "raw_deep" or tier == 3 and any(token in stable_id for token in ("Abyss", "Cryo", "Pressure", "Atlas")):
        return "DeepMaterial"
    if any(token in stable_id for token in ("Copper", "Gold", "Silver", "Cobalt", "Lithium", "RareEarth", "Telluric")):
        return "ElectronicsMetal"
    return "StructuralMetal"


def calculate_stats(profile_key: str, tier: int, stable_id: str, logical_category: str) -> tuple[float, float, float, int, int]:
    profile = PROFILE_DATA[profile_key]

    mass = profile["mass"] + profile["mass_step"] * tier
    volume = profile["volume"] + profile["volume_step"] * tier
    energy = profile["energy"] + profile["energy_step"] * tier
    durability = int(round(profile["durability"] + profile["durability_step"] * tier))
    value = int(round(profile["value"] + profile["value_step"] * tier))

    if any(token in stable_id for token in ("Battery", "Hydrogen", "HeatCell", "Reactor", "Turbine", "Generator", "Oxygen")):
        energy += 0.9 + tier * 0.2

    if any(token in stable_id for token in ("Datapad", "Blueprint", "BlackBox", "ChildDrawing", "SuitTag", "Terminal", "Ledger")):
        value += 20

    if any(token in stable_id for token in ("Abyss", "Cryo", "Pressure", "Atlas")):
        value += 10
        durability += 6

    if logical_category == "Wreckage":
        energy = 0.0
        durability = max(14, int(round(durability * 0.65)))

    if logical_category == "BuildableKit":
        value += 18
        durability += 14

    if "Ballast" in stable_id or "Bulkhead" in stable_id:
        mass += 1.5
        durability += 8

    if "Drone" in stable_id or "Sonar" in stable_id:
        value += 6

    return round(mass, 2), round(volume, 2), round(energy, 2), durability, value


def build_items() -> list[dict[str, object]]:
    items: list[dict[str, object]] = []
    for group in GROUPS:
        logical_category = group["logical_category"]
        authoring_target = group["authoring_target"]
        tier_bands = group["tier_bands"]
        rows = group["rows"]

        for index, (stable_id, ru_name, profile_key) in enumerate(rows):
            tier = resolve_tier(index, tier_bands)
            en_name = prettify_item_name(stable_id)
            item_category = resolve_item_category(logical_category, profile_key, stable_id)
            resource_family = resolve_resource_family(logical_category, profile_key, stable_id, tier)
            mass, volume, energy, durability, value = calculate_stats(profile_key, tier, stable_id, logical_category)
            name_key = make_loc_key(stable_id, "NAME")
            desc_key = make_loc_key(stable_id, "DESC")

            items.append(
                {
                    "stable_id": stable_id,
                    "hash": fnv1a32(stable_id),
                    "logical_category": logical_category,
                    "authoring_target": authoring_target,
                    "item_category": item_category,
                    "resource_family": resource_family,
                    "tier": f"Tier{tier}",
                    "name_en": en_name,
                    "name_ru": ru_name,
                    "mass": mass,
                    "volume": volume,
                    "energy": energy,
                    "durability": durability,
                    "value": value,
                    "name_key": name_key,
                    "desc_key": desc_key,
                }
            )
    return items


def build_localization_rows(items: list[dict[str, object]]) -> list[tuple[str, str, str, str]]:
    rows: list[tuple[str, str, str, str]] = []
    for item in items:
        rows.append(("en", item["name_key"], fnv1a32(item["name_key"]), item["name_en"]))
        rows.append(("ru", item["name_key"], fnv1a32(item["name_key"]), item["name_ru"]))
    return rows


def render(items: list[dict[str, object]], localization_rows: list[tuple[str, str, str, str]]) -> str:
    items_header = "StableId|Hash|LogicalCategory|AuthoringTarget|ItemCategory|ResourceFamily|Tier|NameEN|NameRU|MassKg|VolumeL|EnergyDensityMJkg|BaseDurability|ScavengeValue|NameKey|DescKey"
    item_lines = [
        "|".join(
            [
                str(item["stable_id"]),
                str(item["hash"]),
                str(item["logical_category"]),
                str(item["authoring_target"]),
                str(item["item_category"]),
                str(item["resource_family"]),
                str(item["tier"]),
                str(item["name_en"]),
                str(item["name_ru"]),
                f"{item['mass']:.2f}",
                f"{item['volume']:.2f}",
                f"{item['energy']:.2f}",
                str(item["durability"]),
                str(item["value"]),
                str(item["name_key"]),
                str(item["desc_key"]),
            ]
        )
        for item in items
    ]

    log_header = "LogId|Hash|RoutePhase|Bucket|Speaker|ObjectType|Delivery|RepoAnchor|Title|Body"
    log_lines = [
        "|".join(
            [
                log_id,
                fnv1a32(log_id),
                route_phase,
                bucket,
                speaker,
                object_type,
                delivery,
                repo_anchor,
                title,
                body,
            ]
        )
        for log_id, category, route_phase, bucket, speaker, object_type, delivery, repo_anchor, title, body in LOGS
    ]

    loc_header = "Locale|Key|Hash|Value"
    loc_lines = ["|".join(row) for row in localization_rows]

    category_counts: dict[str, int] = {}
    for item in items:
        category = str(item["logical_category"])
        category_counts[category] = category_counts.get(category, 0) + 1

    voice_ready = sum(1 for row in LOGS if row[6] == "localized_voice_placeholder")
    voice_shell = sum(1 for row in LOGS if row[6] == "audio_log_data_exists_no_clip")
    text_only = sum(1 for row in LOGS if row[6] == "text_only")

    document = [
        "# HECTON-8 SURVIVAL DATABASE FINAL",
        "Date: 2026-04-23",
        "Status: PENDING VERIFICATION",
        "",
        "Mandates followed:",
        "- DATA_Inventory_Resources_Items_SOA_Layout.txt",
        "- CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt",
        "- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt",
        "- UI_Data_Streaming_ZeroGC_Optimization.txt",
        "- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt",
        "",
        "Project truths absorbed into this spec:",
        "- Current item authoring owner is `ItemData` with `ItemCategory`, `ResourceFamily`, and `ProgressionTier Tier0..Tier3`.",
        "- Current recipe authoring owner is `RecipeData` with fabrication groups `Materials / Components / Tools / Suit / Construction / Power`.",
        "- Current runtime oxygen base is `SurvivalStats.oxygenConsumptionRate = 1.5` and depth pressure is `1 + depth * 0.1` in `HectonSurvivalSystem`.",
        "- Current pressure damage begins after `safeDepth` and scales by `pressureScalePerMeter = 0.02`.",
        "- Current module life-support anchors in `BaseModule` are `oxygenRefillRate = 15`, `breathableReserveCapacity = 120`, `airRecycleRate = 6`, `occupiedAirDrainRate = 9`, `staleAirThreshold = 0.25`, `staleAirSuitDrainRate = 3`.",
        "- Current authored audio-log asset shells exist for `chen_m_datapad_01`, `captain_last_broadcast`, `biologist_samples`, `medic_diary`, and `atlas6_terminal_sector3` only.",
        "",
        "[ANALYSIS]",
        "Target:",
        "- Exhaustive survival database for items, survival math, lore logs, logistics graph rules, and bilingual item localization seed keys.",
        "",
        "Affected systems:",
        "- ItemData authoring pipeline",
        "- RecipeData and fabrication grouping",
        "- HectonSurvivalSystem oxygen / temperature / integrity loops",
        "- BaseModule dry-air reserve and stale-air behavior",
        "- Narrative archive / AudioLogData / PDA stream surfaces",
        "- Future construction power and oxygen graph nodes",
        "",
        "Zero GC proof:",
        "- This document is data/spec only. No runtime code changed.",
        "- Narrative streaming schema is fixed-array oriented: numeric IDs, stable hashes, UTF-8 blob offsets, no per-open JSON parsing, no runtime string concatenation, no dynamic sorting in UI.",
        "- Item localization registry is precomputed by stable keys and hashes so runtime can resolve table entries without generated keys.",
        "",
        "State check:",
        "- Progression tiers remain inside existing project limits `Tier0..Tier3`.",
        "- Buildable rows are marked `RecipeData + Construction owner` to avoid pretending they are already direct `ItemData` loot assets.",
        "- Existing registry/audio-log IDs are reused where they already exist; spec-only IDs are flagged as such in `RepoAnchor`.",
        "- No claim of runtime fix. This is authored data only. Status remains `PENDING VERIFICATION` until user validates in editor/runtime.",
        "",
        "Rule quote:",
        "- \"[REQ] Before any task, scan C:\\hades\\Hecton8\\.agents-skills\\ and load ONLY relevant mandates.\"",
        "- \"[RULE] Every technical report must include: REGRESSION MODEL (CPU/GC/memory/cadence/correctness) · HOT PATH IMPACT · FAILURE MODES · WHY KEPT/REJECTED.\"",
        "",
        "======================================================================",
        "1. AUTHORING CONVENTIONS",
        "======================================================================",
        "",
        "- Tier0 = bootstrap salvage / upper drydock / 0-120 m",
        "- Tier1 = early colony stabilization / 120-600 m",
        "- Tier2 = drowned factories / 600-2500 m",
        "- Tier3 = abyssal and forbidden infrastructure / 2500-5000 m",
        "- `Hash` = FNV-1a 32-bit of stable ID.",
        "- `NameKey` / `DescKey` = initial localization seeds. Descriptions stay stubbed for later narrative expansion; names are fully seeded here for EN/RU.",
        "- `EnergyDensity` is chemical or stored-energy potential in MJ/kg for economy balancing. Structural junk is explicitly zeroed.",
        "- `ScavengeValue` is salvage desirability, not vendor price. It drives scan interest, fabrication urgency, and route choice.",
        "",
        "Logical category split:",
        f"- RawResource: {category_counts['RawResource']}",
        f"- ProcessedMaterial: {category_counts['ProcessedMaterial']}",
        f"- Component: {category_counts['Component']}",
        f"- Tool: {category_counts['Tool']}",
        f"- Equipment: {category_counts['Equipment']}",
        f"- BuildableKit: {category_counts['BuildableKit']}",
        f"- Wreckage: {category_counts['Wreckage']}",
        f"- Total: {len(items)}",
        "",
        "======================================================================",
        "2. ITEM MASTER TABLE",
        "======================================================================",
        "",
        items_header,
        *item_lines,
        "",
        "======================================================================",
        "3. SURVIVAL MATH",
        "======================================================================",
        "",
        "3.1 Oxygen depletion doctrine",
        "",
        "- Existing runtime anchor stays authoritative: `pressure = 1 + depth * 0.1`, `pressureFactor = max(1, pressure * 0.5)`, base drain = `1.5 O2/sec` from `SurvivalStats`.",
        "- Design extension for movement / stress / hull integrity must stack inside `ResolveTransportOxygenConsumptionScale()` or an adjacent multiplier, not as a second disconnected system.",
        "",
        "Proposed authored oxygen formula:",
        "```text",
        "pressureFactor = max(1.0, (1.0 + depthMeters * 0.1) * 0.5)",
        "move01 = clamp01(currentSpeed / authoredCruiseSpeed)",
        "stress01 = clamp01((1 - oxygenNormalized) * 0.35 + (1 - integrityNormalized) * 0.25 + injurySeverity01 * 0.20 + decompressionRisk01 * 0.20)",
        "movementScale = lerp(1.00, 1.55, move01)",
        "stressScale = 1.00 + stress01 * 0.45",
        "hullLeakScale = 1.00 + (1 - integrityNormalized) * 0.70",
        "gridAirScale = 0.60 when inside powered dry module with fresh reserve",
        "gridAirScale = 0.85 when inside powered stale-air module",
        "gridAirScale = 1.00 when outside or reserve path is broken",
        "o2DrainPerSecond = 1.5 * pressureFactor * movementScale * stressScale * hullLeakScale * gridAirScale * difficultyScale",
        "```",
        "",
        "Design consequences:",
        "- Slow salvage drift remains readable instead of free. The player can idle, but never for free under pressure.",
        "- Panic is expensive but not binary; low integrity leaks reserve before direct hull death.",
        "- Interiors matter because grid air creates a real logistic advantage instead of cosmetic shelter.",
        "",
        "Worked oxygen snapshots with current pressure anchor:",
        "- 60 m calm swim: pressureFactor = 3.5, movementScale = 1.20, stressScale = 1.04, hullLeakScale = 1.07 -> drain ~= 7.01 O2/sec outside.",
        "- 600 m sprint with damaged hull: pressureFactor = 30.5, movementScale = 1.55, stressScale = 1.32, hullLeakScale = 1.35 -> drain ~= 128.95 O2/sec outside. That is intentionally brutal and forces routing, modules, and equipment tiers.",
        "- Same 600 m sprint inside powered fresh-air shelter: multiply by `gridAirScale = 0.60`, drain falls to ~= 77.37 O2/sec while reserve and scrubber support hold.",
        "",
        "3.2 Temperature loss versus abyssal currents",
        "",
        "- Existing runtime anchor stays authoritative:",
        "  - `environmentTemp = atmosphereTemp + localHeat - abyssalColdPenalty`",
        "  - abyssal cold starts after `3000 m` and ramps over `750 m` to a maximum `42 C` penalty",
        "  - cold exposure multiplies heater drain up to `10x` at full abyssal depth",
        "  - cold integrity damage begins when energy is exhausted or cold excess reaches `24 C` below safe minimum",
        "",
        "Authored thermal equation:",
        "```text",
        "depthCold01 = saturate((depthMeters - 3000) / 750)",
        "abyssalColdPenaltyC = lerp(0, 42, depthCold01)",
        "currentDrag01 = clamp01(currentVelocity / authoredCruiseSpeed) * currentSeverity01",
        "suitInsulationC = thermalLinerBonusC + pressureHarnessSealBonusC + temporaryHeatBuffC",
        "effectiveMinSafeTemp = -5 + suitInsulationC",
        "effectiveAmbientC = atmosphereTempC + localHeatC - abyssalColdPenaltyC - currentDrag01 * 6",
        "coldExcessC = max(0, effectiveMinSafeTemp - effectiveAmbientC)",
        "heaterDrainPerSecond = coldExcessC * 0.05 * thermalExposureScale * lerp(1, 10, depthCold01)",
        "integrityDamagePerSecond = 1.0 * (1 + coldExcessC * 0.1) when energy <= 0.01 or coldExcessC >= 24",
        "```",
        "",
        "Suit layer targets for authored equipment in this database:",
        "- `Item_Equip_ThermalLiner_T1`: +8 C safe minimum shift, -10% current drag penalty.",
        "- `Item_Equip_ThermalLiner_T2`: +16 C safe minimum shift, -20% current drag penalty.",
        "- `Item_Equip_PressureHarness_T2`: +6 C seal stability, prevents low-integrity leak escalation until integrity drops below 55%.",
        "- `Proc_HeatCell` and `Build_Thermal_Exchanger` are the logistic answer; not optional flavor items.",
        "",
        "Thermal routing result:",
        "- Shallow cold water punishes stamina and energy management.",
        "- Abyssal currents punish topology. If the player does not own heat production and relay continuity, the world strips movement options away.",
        "",
        "======================================================================",
        "4. NARRATIVE DATABASE",
        "======================================================================",
        "",
        "4.1 Zero-GC streaming layout for industrial logs",
        "",
        "```text",
        "LoreRecord[] records              // fixed-size array, boot allocated",
        "uint32 logHash                    // lookup key",
        "uint16 titleOffset, titleLength   // UTF-8 title blob slice",
        "uint16 bodyOffset, bodyLength     // UTF-8 body blob slice",
        "uint8 routePhaseIndex             // progression sort band",
        "uint8 bucketIndex                 // systems / labor / quota / atlas / collapse",
        "uint8 deliveryMode                // 0=text, 1=audio shell, 2=localized VO placeholder",
        "uint8 flags                       // discovered / archived / critical",
        "uint32 nextSuggestedHash          // deterministic archive chain; no runtime graph alloc",
        "```",
        "",
        "Rules:",
        "- Pre-sort the log table by route phase and reveal order. UI never sorts or filters by allocating temporary lists.",
        "- Archive screen pages by integer range only. Open request = `startIndex` + `count` over the fixed array.",
        "- Text body lives in one immutable UTF-8 blob. UI receives offsets and lengths, not concatenated strings.",
        "- Existing AudioLogData shells remain the owner for voiced entries. This table only defines the stable archive bank and route placement.",
        "",
        "Industrial log counts:",
        f"- Total logs: {len(LOGS)}",
        f"- `localized_voice_placeholder`: {voice_ready}",
        f"- `audio_log_data_exists_no_clip`: {voice_shell}",
        f"- `text_only`: {text_only}",
        "",
        log_header,
        *log_lines,
        "",
        "======================================================================",
        "5. CRAFTING & LOGISTICS",
        "======================================================================",
        "",
        "5.1 Tech tree spine",
        "",
        "Tier0 bootstrap:",
        "- Salvage copper, silica, kelp, sulfur, resin.",
        "- First unlocks: Scanner, Flashlight, Repair Tool, Beacon Deployer, Copper Wire, Fiber Mesh, Glass Panel, Oxygen Pellet.",
        "- Construction objective: `Build_Foundation_Platform`, `Build_Corridor_Straight`, `Build_Airlock_Hatch`, one `Build_Service_Pump`.",
        "",
        "Tier1 stabilization:",
        "- Push into tungsten, rare earth, lithium, thermal gel, cobalt alloy.",
        "- First meaningful suit branching: `Item_Equip_OxygenRig_T1`, `Item_Equip_PressureHarness_T1`, `Item_Equip_ThermalLiner_T1`.",
        "- Fabrication objective: `Build_Oxygen_ScrubberRack`, `Build_Oxygen_Tank`, `Build_Battery_Bank`, `Build_Fabricator_Compact`.",
        "- Narrative gate: `chen_m_datapad_01` + `chen_m_datapad_02` justify manual relay bypass and lockout tools.",
        "",
        "Tier2 industrial recovery:",
        "- Push into magnesium, vanadium, borate, pressure pearl, cryo brine, scrubber beds, oxygen manifolds, flood valves.",
        "- Unlock heavy tools: Seafloor Drill, Brine Siphon, Weld Torch, Heat Shield Projector.",
        "- Network objective: `Build_Service_Relay`, `Build_Relay_Backbone`, `Build_Thermal_Exchanger`, `Build_Brine_Filter`, `Build_Sonar_Mast`, `Build_Drone_Dock`.",
        "- Narrative gate: `biologist_samples`, `medic_diary`, `chen_m_blueprint` move the route from routine salvage into intentional deep systems recovery.",
        "",
        "Tier3 abyssal access:",
        "- Push into atlas residue, abyssal crystal, cryo coil, reactor rod, abyss pressure shell, hull lattice.",
        "- Unlock deep suit stack: `Item_Equip_OxygenRig_T2`, `Item_Equip_PressureHarness_T2`, `Item_Equip_ThermalLiner_T2`, `Item_Equip_HullPatchRig`, `Item_Equip_RadiationVeil`.",
        "- Final construction objective: `Build_Coil_Generator`, `Build_CO2_Reclaimer`, `Build_Abyssal_Pressure_Door`, persistent relay backbone into forbidden sectors.",
        "- Narrative gate: `captain_last_broadcast`, `atlas6_terminal_sector3`, `black_box_shift_b`, `chen_m_suit`.",
        "",
        "5.2 Energy and oxygen grid rules",
        "",
        "- Power graph node types: `Source`, `Buffer`, `Relay`, `Consumer`, `DoorLock`, `HazardSink`.",
        "- Oxygen graph node types: `Scrubber`, `Tank`, `DryVolume`, `Airlock`, `Leak`, `DeadEnd`.",
        "- Graph traversal must be event-driven, not per-frame. Recompute only on place/remove/power-state/door-state topology changes.",
        "- `Build_Current_Turbine` is the first steady source. Existing asset anchor already exposes `powerRating = 18`; treat that as Tier1 baseline generation.",
        "- `Build_Battery_Bank` is buffer, not generation. It absorbs turbine spikes and protects life support from relay flicker.",
        "- `Build_Coil_Generator` is the Tier3 thermal source. It belongs in hazard-adjacent routes where routing cost is part of the survival math.",
        "- Priority stack: 1) oxygen scrubbers and pumps, 2) pressure doors / airlocks, 3) med / fabricator, 4) sonar / lights, 5) comfort and storage.",
        "",
        "Oxygen grid alignment to current `BaseModule` anchors:",
        "- Fresh dry shelter requires powered path from `Build_Oxygen_ScrubberRack` or stocked `Build_Oxygen_Tank` into a sealed dry volume.",
        "- Fresh reserve capacity target = 120 O2-equivalent per inhabited module to match current `breathableReserveCapacity = 120`.",
        "- Air recycle target = 6 O2-equivalent per second per powered scrubber rack to match current `airRecycleRate = 6`.",
        "- Occupied dry module drain target = 9 O2-equivalent per second to match `occupiedAirDrainRate = 9`.",
        "- Once reserve falls below 25%, module enters stale-air state and refill efficiency collapses to 20%; occupants start bleeding 3 O2/sec even indoors. This is already reflected by `staleAirThreshold`, `staleAirMinRefillScale`, and `staleAirSuitDrainRate`.",
        "",
        "Failure model:",
        "- Broken relay path = consumers go dark in deterministic priority order.",
        "- Broken air path = volume remains dry but non-breathable, turning shelter into a false safe zone.",
        "- Flooded volume breaks oxygen continuity and also increases repair tax because pumps become prerequisite, not convenience.",
        "",
        "======================================================================",
        "6. LOCALIZATION HASH REGISTRY",
        "======================================================================",
        "",
        "Seed policy:",
        "- One stable `NameKey` per item.",
        "- Same key hash across EN/RU values; only localized payload changes.",
        "- Description keys are generated in item table but intentionally left text-stubbed until tooltip writing pass.",
        "",
        loc_header,
        *loc_lines,
        "",
        "======================================================================",
        "7. REGRESSION MODEL / HOT PATH IMPACT / FAILURE MODES / WHY KEPT",
        "======================================================================",
        "",
        "REGRESSION MODEL",
        "- CPU: no runtime delta. This file does not execute.",
        "- GC: no runtime delta. This file is static authoring data only.",
        "- Memory: repository size increases by one text spec and one helper script only.",
        "- Cadence: archive and item creation work becomes deterministic because stable IDs, hashes, and routing bands are defined up front.",
        "- Correctness: item tiers remain inside current `Tier0..Tier3` contract; log IDs reuse existing registry/audio-log IDs where available.",
        "",
        "HOT PATH IMPACT",
        "- None until data is turned into runtime assets.",
        "- When implemented, the log streaming schema avoids per-open allocations by using fixed arrays and UTF-8 offset slices.",
        "",
        "FAILURE MODES",
        "- If future assets use different stable IDs, save/localization drift will occur. Do not rename stable IDs after authoring without explicit migration.",
        "- If buildables are authored as free-floating scene prefabs without power / oxygen node contracts, the logistics layer collapses into decoration.",
        "- If oxygen movement / stress scales are bolted on outside the existing survival multipliers, balance will fork and debugging will become impossible.",
        "",
        "WHY KEPT",
        "- The item corpus is large enough to sustain route choice, fabrication bottlenecks, and salvage identity instead of generic ore spam.",
        "- The narrative bank is industrial, localized, and mechanically useful; logs are not abstract mood text.",
        "- The logistics section is anchored to current code constants, not a hypothetical redesign.",
        "",
        "======================================================================",
        "8. VERIFICATION CHECKLIST",
        "======================================================================",
        "",
        "- Confirm 220 unique item stable IDs before creating ScriptableObjects or recipe assets.",
        "- Confirm 50 unique industrial log IDs before archive import.",
        "- Reuse the five existing `AudioLogData` assets for matching IDs; do not mint duplicate lore owners.",
        "- When localization tables are created, verify all `ITEM_*_NAME` keys hash to the values in Section 6.",
        "- When the runtime implementation pass starts, profile archive open/close and ensure 0 B/frame while paging entries.",
        "",
        "Status: PENDING VERIFICATION",
        "",
    ]

    return "\n".join(document)


def validate(items: list[dict[str, object]], localization_rows: list[tuple[str, str, str, str]]) -> None:
    stable_ids = [str(item["stable_id"]) for item in items]
    assert len(items) == 220, f"Expected 220 items, got {len(items)}"
    assert len(set(stable_ids)) == len(stable_ids), "Duplicate item IDs detected"
    assert len(LOGS) == 50, f"Expected 50 logs, got {len(LOGS)}"
    assert len({row[0] for row in LOGS}) == len(LOGS), "Duplicate log IDs detected"
    assert len(localization_rows) == 440, f"Expected 440 localization rows, got {len(localization_rows)}"
    assert len({(locale, key) for locale, key, _, _ in localization_rows}) == len(localization_rows), "Duplicate localization entries detected"


def main() -> None:
    items = build_items()
    localization_rows = build_localization_rows(items)
    validate(items, localization_rows)
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(render(items, localization_rows), encoding="utf-8")
    print(f"Wrote {OUTPUT_PATH}")
    print(f"Items: {len(items)}")
    print(f"Logs: {len(LOGS)}")
    print(f"Localization rows: {len(localization_rows)}")


if __name__ == "__main__":
    main()

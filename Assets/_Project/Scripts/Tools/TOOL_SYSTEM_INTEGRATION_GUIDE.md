# Tool System Integration Guide — v2.0 ENTERPRISE

## Obzor

Tool System — eto kompleksnaya enterprise-uroven sistema upravleniya instrumentami s:
- **Durability system** — iznos i remont instrumentov
- **Upgrade system** — moduli uluchsheniya (do 3 slotov)
- **Stat modifiers** — efficiency, speed, energy consumption
- **HUD integration** — detalnaya panel v HUD
- **Save/Load** — avtomaticheskoe sohranenie sostoyaniya
- **Event-driven** — zero polling, reactive updates

## Arhitektura

```
ToolMetadata (ScriptableObject)
    ↓
PlayerTool (MonoBehaviour)
    ↓
ToolDurabilitySystem (Singleton)
    ↓
ToolHUDPanel (world-space TMP / instanced marker path)
```

## Bystraya ustanovka

### 1. Sozdanie ToolMetadata

```
Assets → Create → Hecton8 → Tools → Tool Metadata
```

**Nastroyka:**
- `toolID` — unikalnyy ID (naprimer, "tool_laser_cutter")
- `tier` — Basic/Advanced/Master
- `category` — Utility/Construction/Combat/Survival/Science
- `maxDurability` — maksimalnaya prochnost (1000-10000)
- `durabilityDrainRate` — iznos/sek pri Primary action (1-50)
- `durabilityDrainRateSecondary` — iznos/sek pri Secondary action (0.5-25)
- `criticalDurabilityThreshold` — kriticheskiy uroven v % (10-30)
- `efficiency` — bazovaya effektivnost (0.5-2.0)
- `speed` — bazovaya skorost (0.5-2.0)
- `energyConsumptionRate` — energiya/sek (0-10)
- `maxUpgradeSlots` — kolichestvo slotov dlya uluchsheniy (0-3)
- `repairCostFull` — stoimost polnogo remonta (1-100)
- `repairResourceID` — ID resursa dlya remonta (naprimer, "titanium")

### 2. Sozdanie ToolModuleData (runtime matrix route)

SHINOBU_231 note: `ToolUpgradeData` is a legacy authoring facade only. Runtime stat upgrades must flow through `ToolModuleData` -> `ToolUpgradeModuleRuleDTO` -> `ToolUpgradeSystem` / `UpgradeMatrixCompiler` LUTs. Do not call `ToolMetadata.GetTotalEfficiency/Speed/Energy` as an upgrade stat path.

Runtime module menu: `Assets -> Create -> Hecton8 -> Tools -> Tool Module`.
Runtime module fields: `moduleId`, `upgradeBits`, `compatibleCategories`, `rangeMultiplier`, `powerMultiplier`, `efficiencyMultiplier`, `speedMultiplier`, `heatGenerationMultiplier`, `cooldownMultiplier`, `batteryCapacityMultiplier`, `batteryDrainMultiplier`, `durabilityDrainMultiplier`, `recoilMultiplier`.
The older `Tool Upgrade` asset path below is legacy migration context only.

```
Assets → Create → Hecton8 → Tools → Tool Upgrade
```

**Nastroyka:**
- `upgradeID` — unikalnyy ID (naprimer, "upgrade_efficiency_mk1")
- `requiredTier` — minimalnyy tier instrumenta
- `compatibleCategories` — kategorii instrumentov
- `efficiencyBonus` — bonus k effektivnosti (+0.2 = +20%)
- `speedBonus` — bonus k skorosti (+0.1 = +10%)
- `energyConsumptionModifier` — modifikator energopotrebleniya (-0.2 = -20%)
- `durabilityDrainMultiplier` — mnozhitel iznosa (0.8 = -20% iznosa)
- `repairCostReduction` — snizhenie stoimosti remonta (%)
- `craftingCost` — stoimost sozdaniya (1-50)
- `craftingResourceID` — ID resursa dlya sozdaniya

### 3. Nastroyka PlayerTool

Na prefabe instrumenta (naprimer, LaserCutter):

**Inspector:**
- `_toolData` → naznachit ItemData instrumenta
- `_toolMetadata` → naznachit sozdannyy ToolMetadata
- `enableDurabilityDrain` → true (vklyuchit iznos)
- `enableEnergyConsumption` → true (vklyuchit energopotreblenie)

### 4. Dobavlenie ToolDurabilitySystem na stsenu

```
GameObject → Create Empty → Add Component → Tool Durability System
```

**Inspector:**
- `enableDurabilityDrain` → true
- `globalDurabilityMultiplier` → 1.0 (globalnyy mnozhitel iznosa)
- `autoBreakOnZero` → true (avtomaticheski lomat pri durability=0)

**Singleton:** Sistema avtomaticheski registriruetsya kak Instance.

### 5. Dobavlenie ToolHUDPanel na HUD Camera

```
HUD Camera → Add Component → Tool HUD Panel
```

**Inspector:**
- `hudCamera` → naznachit Camera komponent
- `hudFont` → naznachit TMP_FontAsset (tot zhe chto v HectonSuitHUD)
- `toolManager` → naznachit PlayerToolManager na Player root (ili ostavit null dlya auto-resolve)

**Colors:** (optsionalno, est defolty)
- `normalColor`, `warningColor`, `criticalColor` — tsveta sostoyaniy
- `durabilityGoodColor` — tsvet shkaly pri horoshey prochnosti
- `upgradeSlotColor` — tsvet slotov uluchsheniy

## Ispolzovanie v kode

### PlayerTool nasledniki

```csharp
public class MyCustomTool : PlayerTool
{
    public override void UsePrimary(float deltaTime)
    {
        // v2.0 ENTERPRISE: base.UsePrimary() avtomaticheski:
        // - Proveryaet IsBroken
        // - Primenyaet iznos (durabilityDrainRate)
        // - Primenyaet energopotreblenie
        // - Vyzyvaet OnToolUsed event
        // - Proveryaet low durability warning
        base.UsePrimary(deltaTime);

        // Vasha logika instrumenta
        float efficiency = GetEfficiency(); // uchityvaet upgrades
        float speed = GetSpeed();           // uchityvaet upgrades
        
        // Primenyaem stat modifiers
        float damage = baseDamage * efficiency;
        float animSpeed = baseAnimSpeed * speed;
        
        // ... ostalnaya logika
    }

    protected override void OnToolBrokenWhileUsing()
    {
        // Kastomnaya reaktsiya na popytku ispolzovaniya slomannogo instrumenta
        PlayBrokenSound();
        ShowBrokenVFX();
    }
}
```

### Podpiska na sobytiya

```csharp
// PlayerTool events
myTool.OnToolUsed += (isPrimary) => 
{
    Debug.Log($"Tool used: {(isPrimary ? "Primary" : "Secondary")}");
};

myTool.OnDurabilityLow += () => 
{
    Debug.Log("Tool durability is low!");
    PlayWarningSound();
};

myTool.OnToolBroken += () => 
{
    Debug.Log("Tool is broken!");
    ShowRepairPrompt();
};

// ToolDurabilitySystem events
GlobalRegistry.ToolDurability.OnDurabilityChanged += (toolID, current, max) =>
{
    Debug.Log($"Tool {toolID}: {current}/{max}");
};

GlobalRegistry.ToolDurability.OnToolBroken += (toolID) =>
{
    Debug.Log($"Tool {toolID} is broken!");
};

GlobalRegistry.ToolDurability.OnToolRepaired += (toolID, newDurability) =>
{
    Debug.Log($"Tool {toolID} repaired to {newDurability}");
};
```

### Remont instrumenta

```csharp
// Runtime repair path: resolve itemHashId during equip/slot command, not inside Tick.
uint itemHashId = cachedToolItemHashId;

// Polnyy remont
GlobalRegistry.ToolDurabilityService.TryRepairToolFull(itemHashId, maxDurability);

// Chastichnyy remont
GlobalRegistry.ToolDurabilityService.TryRepairTool(itemHashId, repairAmount, maxDurability);

// Proverka stoimosti remonta
ToolMetadata metadata = tool.Metadata;
int cost = metadata.repairCostFull;
string resourceID = metadata.repairResourceID;

// Proverka nalichiya resursov
if (inventory.HasResource(resourceID, cost))
{
    inventory.RemoveResource(resourceID, cost);
    GlobalRegistry.ToolDurabilityService.TryRepairToolFull(itemHashId, metadata.maxDurability);
}

// Legacy string repair APIs exist only for cold compatibility/save bridge code.
```

### Ustanovka uluchsheniy

```csharp
ToolMetadata metadata = tool.Metadata;
ToolModuleData module = myModuleAsset;
uint toolId = registeredToolId; // returned by IModularEquipmentService.RegisterTool(owner)

// Runtime path: module is packed into ToolUpgradeModuleRuleDTO, then stats rebuild from LUT/mask math.
if (!GlobalRegistry.ModularEquipment.TryInstallModule(toolId, module))
{
    Debug.Log("Module install rejected.");
    return;
}

if (GlobalRegistry.ModularEquipment.TryGetToolStats(toolId, out ToolRuntimeStats stats))
{
    float newEfficiency = stats.EfficiencyScalar;
    float newSpeed = stats.SpeedScalar;
}

// Udalenie modulya
if (GlobalRegistry.ModularEquipment.TryRemoveModule(toolId, module.ModuleId))
{
    Debug.Log("Module removed.");
}
```

### Poluchenie informatsii o instrumente

```csharp
PlayerTool tool = toolManager.CurrentTool;

// Durability
float current = tool.CurrentDurability;
float normalized = tool.DurabilityNormalized; // 0-1
bool broken = tool.IsBroken;

// Stats (s uchetom upgrades)
float efficiency = tool.GetEfficiency();
float speed = tool.GetSpeed();
float energy = tool.GetEnergyConsumption();

// Metadata
ToolMetadata metadata = tool.Metadata;
ToolTier tier = metadata.tier;
ToolCategory category = metadata.category;
int upgradeSlots = metadata.maxUpgradeSlots;
```

## Save/Load

**Avtomaticheskoe sohranenie:**
- ToolDurabilitySystem realizuet ISaveable (priority 20)
- Sohranyaet durability i broken maps v SaveData
- Zagruzhaetsya avtomaticheski pri LoadFromSaveData()

**Format:**
```csharp
SaveData.toolDurabilityMap["tool_laser_cutter"] = 750.5f;
SaveData.toolBrokenMap["tool_laser_cutter"] = false;
```

## HUD Integration

**ToolHUDPanel avtomaticheski otobrazhaet:**
- Tool name + tier badge (BASIC/ADVANCED/MASTER)
- Durability bar s tsvetovoy indikatsiey:
  - Zelenyy (good) — > 50%
  - Zheltyy (warning) — 20-50%
  - Krasnyy (critical) — < 20%
  - Krasnyy migayuschiy (broken) — 0%
- Real-time stats (efficiency, speed, energy)
- Upgrade slots (do 3 slotov s ikonkami)
- Warning overlay pri critical/broken

**Layout:** nizhniy levyy ugol, 280x160px, nad life support panel

**Animations:**
- Fade in/out pri smene instrumenta (5s)
- Pulse effect pri critical/broken (1.2s period)

## Performance

**ToolDurabilitySystem:**
- Memory: ~8KB (Dictionary capacity 32)
- CPU: ~0.05ms per frame (event-driven, no polling)
- GC: 0 allocations per frame

**ToolHUDPanel:**
- Memory: ~1KB (string cache)
- CPU: ~0.15ms per frame (immediate mode rendering)
- GC: 0 allocations per frame
- Draw calls: +1 (tolko kogda panel vidna)

**PlayerTool:**
- Memory: ~0.5KB per instance
- CPU: ~0.02ms per UsePrimary/UseSecondary call
- GC: 0 allocations per frame

## Troubleshooting

**Problema:** Iznos ne rabotaet
- Proverte chto ToolDurabilitySystem dobavlen na stsenu
- Proverte chto enableDurabilityDrain = true v PlayerTool
- Proverte chto _toolMetadata naznachen v inspektore
- Proverte chto toolID ne pustoy v ToolMetadata

**Problema:** HUD panel ne poyavlyaetsya
- Proverte chto ToolHUDPanel dobavlen na HUD Camera
- Proverte chto hudCamera i hudFont naznacheny
- Proverte chto toolManager naznachen (ili auto-resolve rabotaet)
- Proverte chto _toolMetadata naznachen na PlayerTool

**Problema:** Upgrades ne primenyayutsya
- Proverte chto module ustanovlen cherez `IModularEquipmentService.TryInstallModule`
- Proverte sovmestimost cherez IsCompatibleWith()
- Proverte chto `PlayerTool.GetEfficiency/GetSpeed/GetEnergyConsumption` or `IModularEquipmentService.TryGetToolStats` reads the compiled runtime stats; do not route upgrade math through `ToolMetadata.GetTotal*`.

**Problema:** Save/Load ne rabotaet
- Proverte chto SaveManager.Instance suschestvuet
- Proverte chto ToolDurabilitySystem.OnEnable() vyzyvaetsya
- Proverte chto SaveData.version = 2 (ili vyshe)

## Best Practices

1. **Vsegda naznachayte ToolMetadata** na PlayerTool prefaby
2. **Ispolzuyte GetEfficiency/Speed** v naslednikah dlya stat modifiers
3. **Podpisyvaytes na sobytiya** dlya reactive UI updates
4. **Proveryayte IsBroken** pered kriticheskimi operatsiyami
5. **Ispolzuyte tier system** dlya progression (Basic → Advanced → Master)
6. **Pre-allocate upgrade slots** v ToolMetadata (maxUpgradeSlots)
7. **Testiruyte save/load** posle izmeneniy v SaveData

## Primery ispolzovaniya

### Laser Cutter s iznosom

```csharp
public class LaserCutter : PlayerTool
{
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private ParticleSystem laserVFX;
    
    public override void UsePrimary(float deltaTime)
    {
        base.UsePrimary(deltaTime); // avtomaticheskiy iznos + energopotreblenie
        
        if (IsBroken) return; // uzhe obrabotano v base
        
        // Primenyaem stat modifiers
        float damage = baseDamage * GetEfficiency();
        float beamSpeed = 1f * GetSpeed();
        
        // Strelyaem lazerom
        FireLaser(damage, beamSpeed);
        
        // VFX
        if (!laserVFX.isPlaying)
            laserVFX.Play();
    }
    
    protected override void OnToolBrokenWhileUsing()
    {
        // Kastomnaya reaktsiya
        laserVFX.Stop();
        PlaySound(brokenSound);
        ShowMessage("Laser Cutter is broken! Repair required.");
    }
}
```

### Scanner s upgrades

```csharp
public class Scanner : PlayerTool
{
    [SerializeField] private float baseScanRange = 10f;
    [SerializeField] private float baseScanSpeed = 1f;
    
    public override void UsePrimary(float deltaTime)
    {
        base.UsePrimary(deltaTime);
        
        if (IsBroken) return;
        
        // Upgrades uvelichivayut range i speed
        float scanRange = baseScanRange * GetEfficiency();
        float scanSpeed = baseScanSpeed * GetSpeed();
        
        PerformScan(scanRange, scanSpeed, deltaTime);
    }
}
```

## Changelog

**v2.0 ENTERPRISE:**
- Initial release
- ToolMetadata, ToolUpgradeData, ToolDurabilitySystem
- PlayerTool v2.0 integration
- ToolHUDPanel
- Save/Load support
- Event system
- Zero GC design

## Support

Dlya voprosov i bagreportov sm. README2.md i BACKLOG.txt

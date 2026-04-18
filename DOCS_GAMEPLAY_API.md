# HECTON-8 Gameplay API Documentation

> **Source of Truth** for all public gameplay APIs. Last updated: 2026-04-18

---

## Table of Contents

1. [Core Systems](#core-systems)
2. [Survival System](#survival-system)
3. [Inventory System](#inventory-system)
4. [Power System](#power-system)
5. [Crafting System](#crafting-system)
6. [Interaction System](#interaction-system)
7. [Item System](#item-system)
8. [Consumable System](#consumable-system)
9. [World Props](#world-props)
10. [Event Buses](#event-buses)

---

## Core Systems

### GameTickManager
**Namespace:** `Hecton8.Core`  
**Singleton:** `GameTickManager.Instance`

```csharp
// Registration
void Register(ITickable tickable);
void Register(IFixedTickable fixedTickable);
void Register(ISlowTickable slowTickable);
void RegisterAll(object obj); // Auto-detects interfaces

void Unregister(ITickable tickable);
void Unregister(IFixedTickable fixedTickable);
void Unregister(ISlowTickable slowTickable);
void UnregisterAll(object obj);

// Properties
int TickableCount { get; }
int FixedTickableCount { get; }
int SlowTickableCount { get; }
```

### ITickable / IFixedTickable / ISlowTickable
**Namespace:** `Hecton8.Core`

```csharp
public interface ITickable
{
    void Tick(float deltaTime); // Called every frame
}

public interface IFixedTickable
{
    void FixedTick(float fixedDeltaTime); // Called every physics step
}

public interface ISlowTickable
{
    void SlowTick(); // Called ~2 times per second
}
```

---

## Survival System

### HectonSurvivalSystem
**Namespace:** `Hecton8.Gameplay`  
**Singleton:** No (attached to player)  
**Interfaces:** `ITickable`, `ISlowTickable`, `ISaveable`

```csharp
// Properties
float Oxygen { get; }
float Energy { get; }
float Integrity { get; }
float Hunger { get; }
float Thirst { get; }
float Depth { get; }
float Pressure { get; }
float Weight { get; }
bool IsAlive { get; }

// Normalized properties (0-1)
float OxygenNormalized { get; }
float EnergyNormalized { get; }
float IntegrityNormalized { get; }
float HungerNormalized { get; }
float ThirstNormalized { get; }

// Public API
void RefillOxygen(float amount);
void RechargeEnergy(float amount);
void DrainEnergy(float amount);
void TakeDamage(float amount);
void Repair(float amount);
void AddHunger(float amount);
void AddThirst(float amount);
void SetWeight(float kg);
void OverrideStats(SurvivalStats newStats);

// Events
event Action<float> OnOxygenChanged;
event Action<float> OnEnergyChanged;
event Action<float> OnIntegrityChanged;
event Action<float> OnHungerChanged;
event Action<float> OnThirstChanged;
event Action<float> OnDepthChanged;
event Action<float> OnPressureChanged;
event Action<float> OnOxygenCritical;
event Action<float> OnHungerCritical;
event Action<float> OnThirstCritical;
event Action OnDeath;
```

---

## Inventory System

### PlayerInventory
**Namespace:** `Hecton8.Inventory`  
**Singleton:** No (attached to player)  
**Interfaces:** `ISaveable`

```csharp
// Properties
InventoryGrid Grid { get; }
float CurrentWeight { get; }
float MaxWeight { get; }

// Public API
bool TryAddItem(ItemData item, out int px, out int py);
void RemoveItem(ItemData item, int x, int y);
void AddWeight(float weight);
void RemoveWeight(float weight);
int CountItem(ItemData item);
bool HasItem(ItemData item, int amount);
```

### InventoryGrid
**Namespace:** `Hecton8.Inventory`

```csharp
// Properties
int Columns { get; }
int Rows { get; }
int FreeCells { get; }

// Public API
ItemData GetCell(int x, int y);
bool TryAddItem(ItemData item, out int px, out int py);
void RemoveItem(int x, int y);
bool IsCellEmpty(int x, int y);
```

---

## Power System

### IPowerComponent
**Namespace:** `Hecton8.Power`

```csharp
public interface IPowerComponent
{
    float PowerRating { get; } // Positive = generator, Negative = consumer
    int PowerPriority { get; } // 0 = critical, 100 = luxury
    bool HasPower { get; }
    void OnPowerStatusChanged(bool hasPower);
}
```

### PowerGridManager
**Namespace:** `Hecton8.Power`  
**Singleton:** `PowerGridManager.Instance`

```csharp
// Properties
int GridCount { get; }
float TotalGeneration { get; }
float TotalConsumption { get; }

// Static API
PowerGrid CreateGrid(PowerNode initialNode);
void DestroyGrid(PowerGrid grid);
PowerGrid MergeGrids(PowerGrid a, PowerGrid b);
void CheckAndSplitGrid(PowerGrid grid);
```

### PowerGrid
**Namespace:** `Hecton8.Power`

```csharp
// Properties
int Id { get; }
int NodeCount { get; }
float TotalGeneration { get; }
float TotalConsumption { get; }
float Balance { get; }
bool HasPowerDeficit { get; }
HashSet<PowerNode> Nodes { get; }

// Public API
void AddNode(PowerNode node);
void RemoveNode(PowerNode node);
void AbsorbAll(PowerGrid other);
void UpdateBalance();
void ConsumePower(float amount); // One-time power consumption (crafting, etc.)
```

### PowerNode
**Namespace:** `Hecton8.Power`  
**Interfaces:** `IPoolable`, `IPowerComponent`

```csharp
// Properties
PowerGrid Grid { get; }
List<IPowerComponent> Components { get; }
List<PowerNode> Neighbors { get; }

// Public API
void SetGrid(PowerGrid grid);

// IPowerComponent
float PowerRating { get; }
int PowerPriority { get; }
bool HasPower { get; }
void OnPowerStatusChanged(bool hasPower);
```

---

## Crafting System

### Fabricator
**Namespace:** `Hecton8.Crafting`  
**Interfaces:** `IInteractable`, `ITickable`, `IPowerComponent`, `IFabricator`

```csharp
// Properties
bool IsCrafting { get; }
float CraftProgress { get; }
RecipeData ActiveRecipe { get; }
IReadOnlyList<RecipeData> AvailableRecipes { get; }
int TotalRecipeCount { get; }
int LockedRecipeCount { get; }
bool IsPausedNoPower { get; }

// IPowerComponent
float PowerRating { get; } // 0 when idle, -craftPowerDraw when crafting
int PowerPriority { get; }
bool HasPower { get; }
void OnPowerStatusChanged(bool hasPower);

// Public API
bool CanCraft(RecipeData recipe);
bool StartCraft(RecipeData recipe);
void CancelCraft();
// Note: CompleteCraft() consumes recipe.powerCost from PowerGrid
```

### RecipeData
**Namespace:** `Hecton8.Crafting`  
**Type:** `ScriptableObject`

```csharp
// Identity
string recipeName;
Sprite overrideIcon;
string description;

// Result
ItemData resultItem;
int resultQuantity;

// Ingredients
List<InventoryCost> ingredients;

// Timing
float craftTime; // Seconds

// Power
float powerCost; // Energy consumed on craft completion (Watt-hours)

// Unlock
string requiredScanEntryId;
FabricationGroup fabricationGroup;

// Public API
string GetCraftText();
string GetCostSummary();
Sprite Icon { get; }
bool RequiresScanUnlock { get; }
bool IsUnlocked(ScanLogSystem scanLogSystem);
```

### IFabricator
**Namespace:** `Hecton8.Crafting`

```csharp
public interface IFabricator
{
    IReadOnlyList<RecipeData> AvailableRecipes { get; }
    bool IsCrafting { get; }
    void StartCraft(RecipeData recipe);
    void CancelCraft();
}
```

---

## Interaction System

### IInteractable
**Namespace:** `Hecton8.Interaction`

```csharp
public interface IInteractable
{
    void OnHoverStart();
    void OnHoverEnd();
    void Interact(Transform interactor);
    string GetInteractText();
}
```

### ICuttable
**Namespace:** `Hecton8.Interaction`

```csharp
public interface ICuttable
{
    void ApplyCutDamage(float damage, Vector3 hitPoint);
}
```

---

## Item System

### ItemData
**Namespace:** `Hecton8.Items`  
**Type:** `ScriptableObject`

```csharp
// Identity
string itemName { get; }
string description { get; }
Sprite icon;

// Properties
float weight;
bool stackable;
int maxStack;
ItemCategory category;
ResourceFamily resourceFamily;
ProgressionTier progressionTier;
bool isRawResource;

// Grid
int width;
int height;
int CellArea { get; }

// Consumable
bool isConsumable;
float UseDuration { get; } // Time to consume (0 = instant)
float oxygenRestore;
float energyRestore;
float integrityRestore;
float hungerRestore;
float thirstRestore;
AudioClip useSound;

// World
GameObject worldPrefab;
BuoyancyProfile worldBuoyancyProfile;
```

---

## Consumable System

### ConsumableItem (Static)
**Namespace:** `Hecton8.Gameplay`

```csharp
// Public API
static bool TryConsume(ItemData item, HectonSurvivalSystem survivalSystem);
static bool TryConsume(ItemData item); // Auto-resolves survival system
static bool TryConsumeFromWorld(ItemData item);
static string GetEffectDescription(ItemData item);
static bool HasAnyEffect(ItemData item);
static float GetUseDuration(ItemData item);
static bool RequiresUseTime(ItemData item);
```

### PlayerActionController
**Namespace:** `Hecton8.Gameplay`  
**Interfaces:** `ITickable`

```csharp
// Properties
bool IsActionInProgress { get; }
float Progress { get; } // 0-1
ItemData ActiveItem { get; }

// Public API
bool StartAction(ItemData item);
void CancelAction();
void OnDamageTaken(); // External interrupt call

// Events (UnityEvent for designer hooks)
UnityEvent<float> OnActionProgress; // Progress 0-1
UnityEvent<ItemData> OnActionCompleted;
UnityEvent OnActionCancelled;
```

---

## World Props

### BeaconRegistry (Static)
**Namespace:** `Hecton8.Gameplay`

```csharp
// Registration
static void Register(DeployableBeacon beacon);
static void Unregister(DeployableBeacon beacon);

// Query
static int ActiveCount { get; }
static IEnumerable<DeployableBeacon> AllBeacons { get; }
static DeployableBeacon GetNearest(Vector3 position);
```

### IBatteryTool
**Namespace:** `Hecton8.Tools`

```csharp
public interface IBatteryTool
{
    bool HasBattery { get; }
    float BatteryPercent { get; }
    void ConsumeBattery(float amount);
    bool TryInsertBattery(ItemData battery);
    ItemData RemoveBattery();
}
```

---

## Event Buses

### InteractionEvents
**Namespace:** `Hecton8.Interaction`

```csharp
static event Action<ItemData> OnItemCollected;
static event Action<IInteractable, Transform> OnInteractionStarted;
static event Action<IInteractable> OnHoverChanged;
```

### CraftingEvents
**Namespace:** `Hecton8.Crafting`

```csharp
static event Action<IFabricator> OnFabricatorOpened;
static event Action OnFabricatorClosed;
static event Action<RecipeData> OnCraftStarted;
static event Action<ItemData> OnCraftCompleted;
static event Action OnCraftCancelled;
static event Action<float> OnCraftProgressUpdated; // [0..1]

// Invocation methods
static void RaiseFabricatorOpened(IFabricator fabricator);
static void RaiseFabricatorClosed();
static void RaiseCraftStarted(RecipeData recipe);
static void RaiseCraftCompleted(ItemData result);
static void RaiseCraftCancelled();
static void RaiseCraftProgressUpdated(float progress);
```

### SaveEvents
**Namespace:** `Hecton8.SaveSystem`

```csharp
static event Action OnSaveStarted;
static event Action OnSaveCompleted;
static event Action OnSaveFailed;
static event Action OnLoadStarted;
static event Action OnLoadCompleted;
static event Action OnLoadFailed;
```

### FlashlightEvents
**Namespace:** `Hecton8.Tools`

```csharp
static event Action<bool> OnToggled;
static event Action OnBatteryDepleted;
static event Action OnOverheat;
```

---

## Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Private fields | `_camelCase` | `_currentIntensity` |
| Serialized fields | `[SerializeField] private _camelCase` | `[SerializeField] private float _maxDistance;` |
| Public properties | `PascalCase` | `public float Intensity { get; }` |
| Methods | `PascalCase` | `public void ApplyDamage()` |
| Constants | `PascalCase` | `public const int MaxItems = 64;` |
| Static readonly | `_PascalCase` | `private static readonly int _ColorId;` |
| Events | `OnPascalCase` | `public event Action OnDeath;` |

---

## Integration Patterns

### Registering with GameTickManager

```csharp
private bool _registered;

private void OnEnable()
{
    if (GameTickManager.Instance != null && !_registered)
    {
        GameTickManager.Instance.RegisterAll(this);
        _registered = true;
    }
}

private void OnDisable()
{
    if (GameTickManager.Instance != null && _registered)
    {
        GameTickManager.Instance.UnregisterAll(this);
        _registered = false;
    }
}
```

### Implementing IPowerComponent

```csharp
public float PowerRating => _isActive ? -powerConsumption : 0f;
public int PowerPriority => powerPriority;
public bool HasPower => _hasPower;

public void OnPowerStatusChanged(bool hasPower)
{
    _hasPower = hasPower;
    if (!hasPower && _isActive)
    {
        // Pause operation, don't cancel
    }
}
```

### MaterialPropertyBlock Pattern

```csharp
private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
private static readonly int _IntensityId = Shader.PropertyToID("_HighlightIntensity");

private void ApplyProperties()
{
    _mpb.SetFloat(_IntensityId, _intensity);
    _renderer.SetPropertyBlock(_mpb);
}
```

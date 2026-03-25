# Hecton-8 Enterprise Roadmap — Design Document

## 1. Architecture Overview

### 1.1 System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     HECTON-8 ARCHITECTURE                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   CORE       │  │   GAMEPLAY   │  │   SYSTEMS    │      │
│  │              │  │              │  │              │      │
│  │ GameTick     │  │ Player       │  │ Save/Load    │      │
│  │ ObjectPool   │  │ Inventory    │  │ Events       │      │
│  │ Events       │  │ Tools        │  │ Audio        │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   AI         │  │   WORLD      │  │   UI/HUD     │      │
│  │              │  │              │  │              │      │
│  │ Behavior     │  │ Procedural   │  │ HUD          │      │
│  │ Pathfinding  │  │ Biomes       │  │ PDA          │      │
│  │ Perception   │  │ Resources    │  │ Inventory    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Design Principles

**1. Zero GC Allocations**
- All hot paths (Tick, SlowTick, input) must be GC-free
- Use object pooling for frequently spawned objects
- Pre-allocate collections (List, Dictionary capacity)
- Struct-based data structures where possible
- Cached delegates, no boxing

**2. Event-Driven Architecture**
- Prefer events over polling for reactive systems
- Throttled events (publish only on significant change)
- Static events for global systems (InteractionEvents, SaveEvents)
- Instance events for component-specific logic

**3. Data-Driven Design**
- Configuration via ScriptableObjects
- No hardcoded values in MonoBehaviours
- Modular, reusable data assets
- Easy balancing without code changes

**4. Performance-First**
- Burst compilation for math-heavy code
- Jobs System for multi-threading
- LOD system for rendering
- Async loading for streaming
- Profiling-driven optimization

**5. Testability**
- Interfaces for dependency injection (ITickable, ISaveable)
- Pure functions where possible
- Minimal MonoBehaviour logic
- Unit testable business logic

---

## 2. Foundation Completion Design

### 2.1 Unity Input System Migration

#### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  INPUT SYSTEM ARCHITECTURE               │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  InputActionAsset (ScriptableObject)                     │
│  ├── Player Action Map                                   │
│  │   ├── Movement (WASD, Gamepad LS)                    │
│  │   ├── Jump (Space, Gamepad A)                        │
│  │   ├── Sprint (Shift, Gamepad LB)                     │
│  │   ├── Interact (E, Gamepad X)                        │
│  │   ├── Flashlight (F, Gamepad Y)                      │
│  │   ├── PDA (M, Gamepad Back)                          │
│  │   └── Tool Slots (1-4, Gamepad D-Pad)                │
│  │                                                        │
│  ├── UI Action Map                                       │
│  │   ├── Navigate (Arrow Keys, Gamepad D-Pad)           │
│  │   ├── Submit (Enter, Gamepad A)                      │
│  │   ├── Cancel (Escape, Gamepad B)                     │
│  │   └── Tab Switch (Q/E, Gamepad LB/RB)                │
│  │                                                        │
│  └── Rebinding System                                    │
│      ├── RebindingUI (PDA Controls Tab)                 │
│      ├── RebindingManager (save/load bindings)          │
│      └── InputActionRebindingExtensions                 │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

#### Implementation Details

**InputManager.cs** (Singleton)
```csharp
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    
    private PlayerInput _playerInput;
    private InputActionAsset _inputActions;
    
    // Action references (cached for zero GC)
    public InputAction MoveAction { get; private set; }
    public InputAction JumpAction { get; private set; }
    public InputAction SprintAction { get; private set; }
    // ... etc
    
    // Events (zero GC, cached delegates)
    public event Action<Vector2> OnMove;
    public event Action OnJump;
    public event Action OnSprint;
    
    private void Awake()
    {
        Instance = this;
        _playerInput = GetComponent<PlayerInput>();
        _inputActions = _playerInput.actions;
        
        // Cache action references
        MoveAction = _inputActions["Player/Move"];
        JumpAction = _inputActions["Player/Jump"];
        // ...
        
        // Subscribe to actions (cached delegates)
        MoveAction.performed += HandleMove;
        JumpAction.performed += HandleJump;
    }
    
    private void HandleMove(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();
        OnMove?.Invoke(value);
    }
}
```

**Migration Strategy:**
1. Install Input System package
2. Create InputActionAsset with all actions
3. Create InputManager singleton
4. Migrate HectonPlayerMovement to use InputManager events
5. Migrate PlayerInteraction to use InputManager events
6. Migrate PlayerToolManager to use InputManager events
7. Create rebinding UI in PDA
8. Test with keyboard, Xbox controller, PlayStation controller

**Backward Compatibility:**
- Keep ControlScheme ScriptableObject for default bindings
- InputManager reads defaults from ControlScheme on first run
- User rebindings saved to PlayerPrefs or save file

---


### 2.2 PDA Tool Management Tab

#### UI Layout

```
┌─────────────────────────────────────────────────────────┐
│  PDA — TOOLS TAB                                         │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │  TOOL LIST      │  │  TOOL DETAILS               │  │
│  │                 │  │                             │  │
│  │  [Filter: All▼] │  │  Laser Cutter [ADVANCED]    │  │
│  │  [Sort: Name▼]  │  │  ═══════════════════════════ │  │
│  │                 │  │                             │  │
│  │  ☑ Laser Cutter │  │  Durability: ████████░░ 80% │  │
│  │  ☑ Scanner      │  │  Efficiency: 1.4x (+40%)    │  │
│  │  ☐ Builder      │  │  Speed: 1.2x (+20%)         │  │
│  │  ☒ Repair Tool  │  │  Energy: 8/sec              │  │
│  │                 │  │                             │  │
│  │  [Repair All]   │  │  Upgrades: [2/3 slots]      │  │
│  │                 │  │  ┌─────────────────────┐    │  │
│  │                 │  │  │ ⚡ Efficiency MK1   │    │  │
│  │                 │  │  │ ⚡ Speed Boost      │    │  │
│  │                 │  │  │ [Empty Slot]        │    │  │
│  │                 │  │  └─────────────────────┘    │  │
│  │                 │  │                             │  │
│  │                 │  │  [Repair: 10 Titanium]      │  │
│  │                 │  │  [Remove Upgrade]           │  │
│  │                 │  │                             │  │
│  │                 │  │  Durability History:        │  │
│  │                 │  │  ┌─────────────────────┐    │  │
│  │                 │  │  │     ╱╲    ╱╲         │    │  │
│  │                 │  │  │    ╱  ╲  ╱  ╲        │    │  │
│  │                 │  │  │   ╱    ╲╱    ╲       │    │  │
│  │                 │  │  └─────────────────────┘    │  │
│  └─────────────────┘  └─────────────────────────────┘  │
│                                                           │
│  [Map] [Log] [Controls] [Tools]                          │
└─────────────────────────────────────────────────────────┘
```

#### Component Architecture

**PDAToolsTab.cs**
```csharp
public class PDAToolsTab : MonoBehaviour
{
    // UI References
    [SerializeField] private ScrollRect toolListScroll;
    [SerializeField] private Transform toolListContent;
    [SerializeField] private PDAToolListItem toolItemPrefab;
    [SerializeField] private PDAToolDetailPanel detailPanel;
    [SerializeField] private TMP_Dropdown filterDropdown;
    [SerializeField] private TMP_Dropdown sortDropdown;
    
    // Data
    private List<ToolMetadata> _allTools;
    private List<PDAToolListItem> _pooledItems; // Object pooling
    private ToolMetadata _selectedTool;
    
    // Filtering
    private ToolCategory _filterCategory = ToolCategory.All;
    private ToolSortMode _sortMode = ToolSortMode.Name;
    
    public void Initialize()
    {
        // Find all tools in inventory
        _allTools = FindAllToolsInInventory();
        
        // Pre-allocate UI items (zero GC)
        _pooledItems = new List<PDAToolListItem>(32);
        for (int i = 0; i < 32; i++)
        {
            var item = Instantiate(toolItemPrefab, toolListContent);
            item.gameObject.SetActive(false);
            _pooledItems.Add(item);
        }
        
        RefreshToolList();
    }
    
    private void RefreshToolList()
    {
        // Filter and sort
        var filtered = FilterTools(_allTools, _filterCategory);
        var sorted = SortTools(filtered, _sortMode);
        
        // Update UI (reuse pooled items)
        for (int i = 0; i < _pooledItems.Count; i++)
        {
            if (i < sorted.Count)
            {
                _pooledItems[i].SetTool(sorted[i]);
                _pooledItems[i].gameObject.SetActive(true);
            }
            else
            {
                _pooledItems[i].gameObject.SetActive(false);
            }
        }
    }
}
```

**PDAToolDetailPanel.cs**
```csharp
public class PDAToolDetailPanel : MonoBehaviour
{
    // UI References
    [SerializeField] private TextMeshProUGUI toolNameText;
    [SerializeField] private Image durabilityBar;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Transform upgradeSlotContainer;
    [SerializeField] private PDAUpgradeSlot[] upgradeSlots;
    [SerializeField] private LineRenderer durabilityGraph;
    [SerializeField] private Button repairButton;
    
    // Data
    private ToolMetadata _currentTool;
    private float[] _durabilityHistory = new float[10]; // Last 10 uses
    
    public void SetTool(ToolMetadata tool)
    {
        _currentTool = tool;
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        // Tool name + tier badge
        toolNameText.text = $"{_currentTool.toolName} [{_currentTool.tier}]";
        
        // Durability bar
        float durability = ToolDurabilitySystem.Instance.GetDurability(_currentTool.toolID);
        durabilityBar.fillAmount = durability / _currentTool.maxDurability;
        
        // Stats (with upgrades)
        float efficiency = _currentTool.GetTotalEfficiency();
        float speed = _currentTool.GetTotalSpeed();
        float energy = _currentTool.GetTotalEnergyConsumption();
        
        statsText.text = $"Efficiency: {efficiency:F1}x\n" +
                        $"Speed: {speed:F1}x\n" +
                        $"Energy: {energy:F0}/sec";
        
        // Upgrade slots
        for (int i = 0; i < upgradeSlots.Length; i++)
        {
            if (i < _currentTool.installedUpgrades.Count)
                upgradeSlots[i].SetUpgrade(_currentTool.installedUpgrades[i]);
            else
                upgradeSlots[i].SetEmpty();
        }
        
        // Durability graph
        UpdateDurabilityGraph();
        
        // Repair button
        bool canRepair = durability < _currentTool.maxDurability;
        repairButton.interactable = canRepair;
    }
    
    private void UpdateDurabilityGraph()
    {
        // Draw line graph using LineRenderer (zero GC)
        durabilityGraph.positionCount = _durabilityHistory.Length;
        
        for (int i = 0; i < _durabilityHistory.Length; i++)
        {
            float x = i * 0.1f;
            float y = _durabilityHistory[i] / _currentTool.maxDurability;
            durabilityGraph.SetPosition(i, new Vector3(x, y, 0f));
        }
    }
}
```

**Zero GC Design:**
- Pre-allocated UI item pool (32 items)
- Cached TextMeshProUGUI references
- Struct-based durability history (float[10])
- No string concatenation in hot paths (use StringBuilder cache)
- Event-driven updates (only refresh on tool change)

---

### 2.3 HUD Visor UX Completion

#### HUD Layout

```
┌─────────────────────────────────────────────────────────┐
│  HECTON-8 HUD                                            │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  [TIME: 14:32]  [DEPTH: 125m]  [TEMP: 4°C]              │
│                                                           │
│  ┌─────────────────────────────────────────────────┐    │
│  │  ⚠ FLASHLIGHT OVERHEAT                          │    │
│  └─────────────────────────────────────────────────┘    │
│                                                           │
│                                                           │
│                                                           │
│                                                           │
│                                                           │
│                                                           │
│  ┌──────────────┐                    ┌──────────────┐   │
│  │ LIFE SUPPORT │                    │  EQUIPMENT   │   │
│  │ O2:  ████░░  │                    │  ◉ Flashlight│   │
│  │ PWR: ██████  │                    │  ▣ PDA       │   │
│  │ INT: ███████ │                    │  ⚙ Laser     │   │
│  └──────────────┘                    │  ⚙ Scanner   │   │
│                                       └──────────────┘   │
│  ┌──────────────────────────────────────────────────┐   │
│  │ TOOL: Laser Cutter [ADVANCED]                    │   │
│  │ Durability: ████████░░ 80%                       │   │
│  │ Efficiency: 1.4x  Speed: 1.2x  Energy: 8/sec    │   │
│  │ Upgrades: [⚡][⚡][░]                             │   │
│  └──────────────────────────────────────────────────┘   │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

#### Component Integration

**HectonSuitHUDExtensions.cs** (existing, complete)
- FlashlightStatusIndicator ✅
- PDAStatusIndicator ✅
- NotificationSystem ✅
- EquipmentStatusPanel (needs completion)

**EquipmentStatusPanel Implementation:**

```csharp
// Add to HectonSuitHUDExtensions.cs
private void DrawEquipmentPanel()
{
    using (Draw.Command(hudCamera))
    {
        Draw.FontSize = 14;
        Draw.Font = hudFont;
        
        Vector2 panelPos = new Vector2(Screen.width - 200, 100);
        float lineHeight = 20f;
        
        // Panel background
        Draw.Rectangle(panelPos, new Vector2(180, 120), normalColor * 0.3f);
        
        // Title
        Draw.Text(panelPos + new Vector2(10, 10), "EQUIPMENT", normalColor);
        
        // Flashlight status
        Vector2 flashlightPos = panelPos + new Vector2(10, 35);
        string flashlightIcon = _flashlightOn ? "◉" : "○";
        Color flashlightColor = _flashlightOn ? flashlightOnColor : normalColor;
        Draw.Text(flashlightPos, $"{flashlightIcon} Flashlight", flashlightColor);
        
        // Heat bar (if flashlight on)
        if (_flashlightOn && _flashlightHeat > 0f)
        {
            Vector2 heatBarPos = flashlightPos + new Vector2(100, 0);
            DrawHeatBar(heatBarPos, _flashlightHeat);
        }
        
        // PDA status
        Vector2 pdaPos = panelPos + new Vector2(10, 55);
        string pdaIcon = _pdaOpen ? "▣" : "□";
        Color pdaColor = _pdaOpen ? pdaActiveColor : normalColor;
        Draw.Text(pdaPos, $"{pdaIcon} PDA", pdaColor);
        
        // Current tool
        if (_currentTool != null)
        {
            Vector2 toolPos = panelPos + new Vector2(10, 75);
            string toolIcon = "⚙";
            Draw.Text(toolPos, $"{toolIcon} {_currentTool.toolName}", normalColor);
            
            // Tool durability indicator
            float durability = _currentTool.DurabilityNormalized;
            Color durabilityColor = GetDurabilityColor(durability);
            Draw.Circle(toolPos + new Vector2(150, 5), 5f, durabilityColor);
        }
    }
}

private void DrawHeatBar(Vector2 pos, float heat)
{
    float barWidth = 60f;
    float barHeight = 8f;
    
    // Background
    Draw.Rectangle(pos, new Vector2(barWidth, barHeight), Color.black * 0.5f);
    
    // Fill
    Color heatColor = Color.Lerp(normalColor, criticalColor, heat);
    Draw.Rectangle(pos, new Vector2(barWidth * heat, barHeight), heatColor);
}
```

**Performance Optimization:**
- Immediate mode rendering (Shapes plugin)
- Zero GC allocations
- Cached string references
- Draw only when visible
- Target: <0.2ms per frame

---


## 3. Advanced Systems Design

### 3.1 Advanced AI System

#### Behavior Tree Architecture

```
┌─────────────────────────────────────────────────────────┐
│  AI BEHAVIOR TREE SYSTEM                                 │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  BehaviorTreeAsset (ScriptableObject)                   │
│  └── Root Node (Selector)                               │
│      ├── Flee Sequence                                   │
│      │   ├── Check Health < 30%                         │
│      │   └── Flee From Threat                           │
│      │                                                    │
│      ├── Attack Sequence                                 │
│      │   ├── Check Target In Range                      │
│      │   ├── Face Target                                │
│      │   └── Execute Attack                             │
│      │                                                    │
│      ├── Chase Sequence                                  │
│      │   ├── Check Has Target                           │
│      │   ├── Check Target Visible                       │
│      │   └── Move To Target                             │
│      │                                                    │
│      └── Patrol Sequence                                 │
│          ├── Check Has Patrol Points                    │
│          ├── Move To Next Point                         │
│          └── Wait At Point                              │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

#### Core Components

**AIController.cs**
```csharp
public class AIController : MonoBehaviour, ITickable
{
    [SerializeField] private BehaviorTreeAsset behaviorTree;
    [SerializeField] private AIPerception perception;
    [SerializeField] private AIMovement movement;
    [SerializeField] private AIAnimator animator;
    
    private BehaviorTreeInstance _treeInstance;
    private AIBlackboard _blackboard;
    
    private void Awake()
    {
        _blackboard = new AIBlackboard();
        _treeInstance = behaviorTree.CreateInstance(_blackboard);
    }
    
    public void Tick(float deltaTime)
    {
        // Update perception
        perception.UpdatePerception(deltaTime, _blackboard);
        
        // Evaluate behavior tree
        _treeInstance.Evaluate(deltaTime);
        
        // Update movement
        movement.UpdateMovement(deltaTime, _blackboard);
        
        // Update animation
        animator.UpdateAnimation(deltaTime, _blackboard);
    }
}
```

**AIPerception.cs**
```csharp
public class AIPerception : MonoBehaviour
{
    [SerializeField] private float sightRange = 20f;
    [SerializeField] private float sightAngle = 120f;
    [SerializeField] private float hearingRange = 30f;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private LayerMask obstacleMask;
    
    // Pre-allocated arrays (zero GC)
    private Collider[] _perceptionBuffer = new Collider[32];
    private RaycastHit[] _raycastBuffer = new RaycastHit[8];
    
    public void UpdatePerception(float deltaTime, AIBlackboard blackboard)
    {
        // Sight perception
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, 
            sightRange, 
            _perceptionBuffer, 
            targetMask);
        
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < count; i++)
        {
            Transform target = _perceptionBuffer[i].transform;
            
            // Check angle
            Vector3 dirToTarget = target.position - transform.position;
            float angle = Vector3.Angle(transform.forward, dirToTarget);
            
            if (angle > sightAngle * 0.5f) continue;
            
            // Check line of sight
            if (Physics.Linecast(transform.position, target.position, obstacleMask))
                continue;
            
            // Track closest
            float distance = dirToTarget.magnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target;
            }
        }
        
        // Update blackboard
        blackboard.SetValue("Target", closestTarget);
        blackboard.SetValue("TargetDistance", closestDistance);
    }
}
```

**AIBlackboard.cs** (Zero GC)
```csharp
public class AIBlackboard
{
    // Pre-allocated dictionary (zero GC)
    private Dictionary<string, object> _data = new Dictionary<string, object>(16);
    
    // Cached keys (avoid string allocations)
    private static readonly string KEY_TARGET = "Target";
    private static readonly string KEY_TARGET_DISTANCE = "TargetDistance";
    private static readonly string KEY_HEALTH = "Health";
    private static readonly string KEY_STATE = "State";
    
    public void SetValue<T>(string key, T value)
    {
        _data[key] = value;
    }
    
    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out object value))
            return (T)value;
        return defaultValue;
    }
}
```

**Behavior Tree Nodes:**

```csharp
// Base node
public abstract class BehaviorNode
{
    public abstract NodeState Evaluate(float deltaTime, AIBlackboard blackboard);
}

// Composite nodes
public class SelectorNode : BehaviorNode
{
    private List<BehaviorNode> _children;
    
    public override NodeState Evaluate(float deltaTime, AIBlackboard blackboard)
    {
        foreach (var child in _children)
        {
            NodeState state = child.Evaluate(deltaTime, blackboard);
            if (state != NodeState.Failure)
                return state;
        }
        return NodeState.Failure;
    }
}

public class SequenceNode : BehaviorNode
{
    private List<BehaviorNode> _children;
    
    public override NodeState Evaluate(float deltaTime, AIBlackboard blackboard)
    {
        foreach (var child in _children)
        {
            NodeState state = child.Evaluate(deltaTime, blackboard);
            if (state != NodeState.Success)
                return state;
        }
        return NodeState.Success;
    }
}

// Leaf nodes (actions)
public class MoveToTargetNode : BehaviorNode
{
    public override NodeState Evaluate(float deltaTime, AIBlackboard blackboard)
    {
        Transform target = blackboard.GetValue<Transform>("Target");
        if (target == null) return NodeState.Failure;
        
        // Set movement target
        blackboard.SetValue("MoveTarget", target.position);
        return NodeState.Running;
    }
}

public class AttackNode : BehaviorNode
{
    private float _attackCooldown = 2f;
    private float _lastAttackTime;
    
    public override NodeState Evaluate(float deltaTime, AIBlackboard blackboard)
    {
        if (Time.time - _lastAttackTime < _attackCooldown)
            return NodeState.Running;
        
        Transform target = blackboard.GetValue<Transform>("Target");
        if (target == null) return NodeState.Failure;
        
        // Execute attack
        blackboard.SetValue("TriggerAttack", true);
        _lastAttackTime = Time.time;
        
        return NodeState.Success;
    }
}
```

**Performance Optimization:**
- Behavior tree evaluation: <0.5ms per creature
- Perception updates: staggered (not all creatures per frame)
- Spatial partitioning for perception queries
- Burst-compiled pathfinding
- Job system for batch AI updates

---

### 3.2 Procedural Cave Generation

#### Generation Algorithm

```
┌─────────────────────────────────────────────────────────┐
│  PROCEDURAL CAVE GENERATION PIPELINE                     │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  1. Graph Generation                                     │
│     ├── Create main path (start → end)                  │
│     ├── Add branches (side tunnels)                     │
│     ├── Add loops (interconnected paths)                │
│     └── Add dead ends (exploration rewards)             │
│                                                           │
│  2. Tunnel Carving                                       │
│     ├── Marching cubes for smooth walls                 │
│     ├── Perlin noise for organic shapes                 │
│     ├── Width variation (narrow/wide sections)          │
│     └── Height variation (ceiling/floor)                │
│                                                           │
│  3. Feature Placement                                    │
│     ├── Resource nodes (ore veins)                      │
│     ├── Flora (kelp, coral)                             │
│     ├── Fauna spawn points                              │
│     └── Landmarks (crystals, ruins)                     │
│                                                           │
│  4. Mesh Generation                                      │
│     ├── Collision mesh (simplified)                     │
│     ├── Visual mesh (detailed)                          │
│     ├── Navmesh (AI pathfinding)                        │
│     └── LOD meshes (3 levels)                           │
│                                                           │
│  5. Lighting & Atmosphere                                │
│     ├── Bioluminescent patches                          │
│     ├── Volumetric fog                                  │
│     ├── Ambient occlusion                               │
│     └── Light probes                                    │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

#### Implementation

**CaveGenerator.cs**
```csharp
public class CaveGenerator : MonoBehaviour
{
    [SerializeField] private CaveGenerationSettings settings;
    
    public CaveData GenerateCave(int seed)
    {
        Random.InitState(seed);
        
        // 1. Generate graph
        CaveGraph graph = GenerateGraph(settings);
        
        // 2. Carve tunnels
        VoxelGrid voxels = CarveTunnels(graph, settings);
        
        // 3. Place features
        PlaceFeatures(voxels, graph, settings);
        
        // 4. Generate meshes
        CaveMeshes meshes = GenerateMeshes(voxels, settings);
        
        // 5. Setup lighting
        SetupLighting(meshes, settings);
        
        return new CaveData
        {
            graph = graph,
            voxels = voxels,
            meshes = meshes
        };
    }
    
    private CaveGraph GenerateGraph(CaveGenerationSettings settings)
    {
        CaveGraph graph = new CaveGraph();
        
        // Main path
        Vector3 start = Vector3.zero;
        Vector3 end = new Vector3(settings.caveLength, 0, 0);
        graph.AddPath(start, end, PathType.Main);
        
        // Branches
        int branchCount = Random.Range(settings.minBranches, settings.maxBranches);
        for (int i = 0; i < branchCount; i++)
        {
            Vector3 branchStart = graph.GetRandomPoint();
            Vector3 branchDir = Random.onUnitSphere;
            branchDir.y *= 0.5f; // Flatten vertical
            Vector3 branchEnd = branchStart + branchDir * settings.branchLength;
            
            graph.AddPath(branchStart, branchEnd, PathType.Branch);
        }
        
        return graph;
    }
    
    private VoxelGrid CarveTunnels(CaveGraph graph, CaveGenerationSettings settings)
    {
        VoxelGrid voxels = new VoxelGrid(settings.gridSize);
        
        foreach (var path in graph.paths)
        {
            float radius = path.type == PathType.Main 
                ? settings.mainTunnelRadius 
                : settings.branchTunnelRadius;
            
            // Carve along path
            for (float t = 0; t <= 1f; t += 0.1f)
            {
                Vector3 point = path.GetPoint(t);
                
                // Add noise for organic shape
                float noise = Mathf.PerlinNoise(point.x * 0.1f, point.z * 0.1f);
                float actualRadius = radius * (1f + noise * 0.3f);
                
                voxels.CarveSphere(point, actualRadius);
            }
        }
        
        return voxels;
    }
}
```

**CaveGenerationSettings.cs** (ScriptableObject)
```csharp
[CreateAssetMenu(menuName = "Hecton8/Cave Generation Settings")]
public class CaveGenerationSettings : ScriptableObject
{
    [Header("Graph")]
    public float caveLength = 100f;
    public int minBranches = 3;
    public int maxBranches = 8;
    public float branchLength = 30f;
    
    [Header("Tunnels")]
    public float mainTunnelRadius = 5f;
    public float branchTunnelRadius = 3f;
    public float noiseScale = 0.1f;
    
    [Header("Features")]
    public int resourceNodeCount = 20;
    public int floraCount = 50;
    public int landmarkCount = 3;
    
    [Header("Mesh")]
    public Vector3Int gridSize = new Vector3Int(100, 50, 100);
    public float voxelSize = 1f;
    public int lodLevels = 3;
}
```

**Performance:**
- Generation time: <5 seconds for 100x100x100m cave
- Burst-compiled voxel operations
- Job system for mesh generation
- Async generation (background thread)
- Streaming: load/unload chunks by distance

---


## 4. Performance & Optimization Design

### 4.1 Memory Optimization Strategy

#### Object Pooling System

```csharp
public class ObjectPoolManager : MonoBehaviour
{
    private static ObjectPoolManager _instance;
    public static ObjectPoolManager Instance => _instance;
    
    // Pool dictionary (prefab → pool)
    private Dictionary<GameObject, ObjectPool> _pools;
    
    // Pre-allocated pool capacity
    private const int DEFAULT_POOL_SIZE = 32;
    private const int MAX_POOL_SIZE = 256;
    
    private void Awake()
    {
        _instance = this;
        _pools = new Dictionary<GameObject, ObjectPool>(64);
    }
    
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!_pools.TryGetValue(prefab, out ObjectPool pool))
        {
            pool = new ObjectPool(prefab, DEFAULT_POOL_SIZE, transform);
            _pools[prefab] = pool;
        }
        
        return pool.Spawn(position, rotation);
    }
    
    public void Despawn(GameObject instance)
    {
        if (instance.TryGetComponent(out IPoolable poolable))
        {
            poolable.OnDespawn();
        }
        
        instance.SetActive(false);
        // Return to pool (handled by ObjectPool)
    }
}

public class ObjectPool
{
    private GameObject _prefab;
    private Queue<GameObject> _available;
    private List<GameObject> _active;
    private Transform _parent;
    
    public ObjectPool(GameObject prefab, int initialSize, Transform parent)
    {
        _prefab = prefab;
        _parent = parent;
        _available = new Queue<GameObject>(initialSize);
        _active = new List<GameObject>(initialSize);
        
        // Pre-allocate
        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            obj.SetActive(false);
            _available.Enqueue(obj);
        }
    }
    
    public GameObject Spawn(Vector3 position, Quaternion rotation)
    {
        GameObject obj;
        
        if (_available.Count > 0)
        {
            obj = _available.Dequeue();
        }
        else
        {
            // Expand pool if needed
            obj = Object.Instantiate(_prefab, _parent);
        }
        
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        _active.Add(obj);
        
        if (obj.TryGetComponent(out IPoolable poolable))
        {
            poolable.OnSpawn();
        }
        
        return obj;
    }
    
    public void Despawn(GameObject obj)
    {
        _active.Remove(obj);
        _available.Enqueue(obj);
        obj.SetActive(false);
    }
}
```

#### String Caching System

```csharp
public static class StringCache
{
    // Pre-allocated common strings
    private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(256);
    
    // Common format strings
    public static readonly string FORMAT_DURABILITY = "Durability: {0:F0}%";
    public static readonly string FORMAT_EFFICIENCY = "Efficiency: {0:F1}x";
    public static readonly string FORMAT_DEPTH = "Depth: {0:F0}m";
    
    // StringBuilder pool (zero GC)
    private static readonly StringBuilder _sb = new StringBuilder(256);
    
    public static string Format(string format, params object[] args)
    {
        _sb.Clear();
        _sb.AppendFormat(format, args);
        return _sb.ToString();
    }
    
    public static string GetCached(string key)
    {
        if (_cache.TryGetValue(key, out string cached))
            return cached;
        
        _cache[key] = key;
        return key;
    }
}
```

#### Memory Budget

| System | Budget | Critical |
|--------|--------|----------|
| Player & Systems | 50MB | 80MB |
| World & Terrain | 150MB | 250MB |
| Creatures & AI | 100MB | 150MB |
| UI & HUD | 30MB | 50MB |
| Audio | 50MB | 80MB |
| VFX & Particles | 50MB | 80MB |
| Misc & Overhead | 70MB | 110MB |
| **Total** | **500MB** | **800MB** |

---

### 4.2 LOD System Design

#### LOD Configuration

```csharp
[CreateAssetMenu(menuName = "Hecton8/LOD Settings")]
public class LODSettings : ScriptableObject
{
    [Header("Distance Thresholds")]
    public float lod0Distance = 20f;  // High detail
    public float lod1Distance = 50f;  // Medium detail
    public float lod2Distance = 100f; // Low detail
    public float cullingDistance = 200f; // Invisible
    
    [Header("Quality Presets")]
    public LODQualityPreset[] qualityPresets;
}

[System.Serializable]
public struct LODQualityPreset
{
    public string name; // "Low", "Medium", "High", "Ultra"
    public float distanceMultiplier; // 0.5x, 1.0x, 1.5x, 2.0x
    public bool enableImpostors;
    public bool enableOcclusionCulling;
}
```

#### LOD Manager

```csharp
public class LODManager : MonoBehaviour, ITickable
{
    [SerializeField] private LODSettings settings;
    [SerializeField] private Transform playerTransform;
    
    private List<LODGroup> _lodGroups = new List<LODGroup>(1024);
    private float _updateInterval = 0.1f; // Update 10 times per second
    private float _lastUpdateTime;
    
    public void Tick(float deltaTime)
    {
        if (Time.time - _lastUpdateTime < _updateInterval)
            return;
        
        _lastUpdateTime = Time.time;
        
        // Update LOD levels based on distance
        Vector3 playerPos = playerTransform.position;
        
        foreach (var lodGroup in _lodGroups)
        {
            float distance = Vector3.Distance(playerPos, lodGroup.transform.position);
            lodGroup.ForceLOD(GetLODLevel(distance));
        }
    }
    
    private int GetLODLevel(float distance)
    {
        if (distance < settings.lod0Distance) return 0;
        if (distance < settings.lod1Distance) return 1;
        if (distance < settings.lod2Distance) return 2;
        return -1; // Culled
    }
}
```

---

### 4.3 Burst Compilation & Jobs System

#### Burst-Compiled Math Operations

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PathfindingJob : IJob
{
    [ReadOnly] public NativeArray<float3> nodes;
    [ReadOnly] public float3 start;
    [ReadOnly] public float3 goal;
    [WriteOnly] public NativeArray<int> path;
    
    public void Execute()
    {
        // A* pathfinding (Burst-compiled)
        // 4x faster than managed code
        
        // ... pathfinding logic
    }
}

[BurstCompile]
public struct VoxelCarvingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> carvePoints;
    [ReadOnly] public float radius;
    public NativeArray<float> voxelDensity;
    
    public void Execute(int index)
    {
        // Carve voxel at index
        float3 voxelPos = GetVoxelPosition(index);
        
        foreach (var point in carvePoints)
        {
            float distance = math.distance(voxelPos, point);
            if (distance < radius)
            {
                voxelDensity[index] = math.max(0f, voxelDensity[index] - (1f - distance / radius));
            }
        }
    }
    
    private float3 GetVoxelPosition(int index)
    {
        // Convert 1D index to 3D position
        int x = index % 100;
        int y = (index / 100) % 50;
        int z = index / (100 * 50);
        return new float3(x, y, z);
    }
}
```

#### Job System Usage

```csharp
public class ProceduralGenerator : MonoBehaviour
{
    public void GenerateCaveAsync(int seed, System.Action<CaveData> onComplete)
    {
        // Allocate native arrays
        NativeArray<float3> carvePoints = new NativeArray<float3>(100, Allocator.TempJob);
        NativeArray<float> voxelDensity = new NativeArray<float>(100 * 50 * 100, Allocator.TempJob);
        
        // Initialize data
        // ...
        
        // Schedule job
        VoxelCarvingJob job = new VoxelCarvingJob
        {
            carvePoints = carvePoints,
            radius = 5f,
            voxelDensity = voxelDensity
        };
        
        JobHandle handle = job.Schedule(voxelDensity.Length, 64);
        
        // Wait for completion (async)
        StartCoroutine(WaitForJob(handle, () =>
        {
            // Process results
            CaveData data = ProcessVoxelData(voxelDensity);
            
            // Cleanup
            carvePoints.Dispose();
            voxelDensity.Dispose();
            
            onComplete?.Invoke(data);
        }));
    }
    
    private IEnumerator WaitForJob(JobHandle handle, System.Action onComplete)
    {
        while (!handle.IsCompleted)
        {
            yield return null;
        }
        
        handle.Complete();
        onComplete?.Invoke();
    }
}
```

**Performance Gains:**
- Burst compilation: 4-10x speedup
- Job system: 2-4x speedup on multi-core CPUs
- Combined: 8-40x speedup for math-heavy operations

---

## 5. Testing Strategy

### 5.1 Unit Testing Framework

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class InventoryGridTests
{
    private InventoryGrid _grid;
    private ItemData _testItem;
    
    [SetUp]
    public void Setup()
    {
        _grid = new InventoryGrid(8, 6);
        _testItem = ScriptableObject.CreateInstance<ItemData>();
        _testItem.width = 2;
        _testItem.height = 2;
    }
    
    [Test]
    public void TryAddItem_EmptyGrid_ReturnsTrue()
    {
        // Arrange
        int x, y;
        
        // Act
        bool result = _grid.TryAddItem(_testItem, out x, out y);
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(0, x);
        Assert.AreEqual(0, y);
    }
    
    [Test]
    public void TryAddItem_FullGrid_ReturnsFalse()
    {
        // Arrange
        FillGrid(_grid);
        int x, y;
        
        // Act
        bool result = _grid.TryAddItem(_testItem, out x, out y);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [Test]
    public void RemoveItem_ValidPosition_RemovesItem()
    {
        // Arrange
        _grid.PlaceAt(_testItem, 0, 0);
        
        // Act
        _grid.RemoveItem(0, 0, 2, 2);
        
        // Assert
        Assert.IsNull(_grid.GetCell(0, 0));
        Assert.IsNull(_grid.GetCell(1, 1));
    }
    
    private void FillGrid(InventoryGrid grid)
    {
        for (int y = 0; y < grid.Rows; y += 2)
        {
            for (int x = 0; x < grid.Columns; x += 2)
            {
                grid.PlaceAt(_testItem, x, y);
            }
        }
    }
}
```

### 5.2 Integration Testing

```csharp
[TestFixture]
public class SaveLoadIntegrationTests
{
    [UnityTest]
    public IEnumerator SaveLoad_PlayerStats_RestoresCorrectly()
    {
        // Arrange
        GameObject playerObj = new GameObject("Player");
        HectonSurvivalSystem survival = playerObj.AddComponent<HectonSurvivalSystem>();
        SaveManager saveManager = new GameObject("SaveManager").AddComponent<SaveManager>();
        
        survival.RefillOxygen(50f);
        survival.RechargeEnergy(100f);
        
        yield return null; // Wait one frame
        
        // Act - Save
        SaveData saveData = SaveData.CreateNew(0f);
        survival.PopulateSaveData(saveData);
        
        // Modify state
        survival.RefillOxygen(-50f);
        
        // Act - Load
        survival.LoadFromSaveData(saveData);
        
        // Assert
        Assert.AreEqual(50f, survival.Oxygen, 0.1f);
        Assert.AreEqual(100f, survival.Energy, 0.1f);
        
        // Cleanup
        Object.Destroy(playerObj);
        Object.Destroy(saveManager.gameObject);
    }
}
```

### 5.3 Performance Benchmarking

```csharp
[TestFixture]
public class PerformanceBenchmarks
{
    [Test, Performance]
    public void Benchmark_InventoryGrid_TryAddItem()
    {
        // Arrange
        InventoryGrid grid = new InventoryGrid(8, 6);
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.width = 1;
        item.height = 1;
        
        // Measure
        Measure.Method(() =>
        {
            int x, y;
            grid.TryAddItem(item, out x, out y);
        })
        .WarmupCount(10)
        .MeasurementCount(100)
        .IterationsPerMeasurement(1000)
        .GC()
        .Run();
        
        // Assert: <0.01ms per operation
    }
    
    [UnityTest, Performance]
    public IEnumerator Benchmark_AIController_Tick()
    {
        // Arrange
        GameObject aiObj = new GameObject("AI");
        AIController ai = aiObj.AddComponent<AIController>();
        
        yield return null; // Wait for initialization
        
        // Measure
        Measure.Method(() =>
        {
            ai.Tick(Time.deltaTime);
        })
        .WarmupCount(10)
        .MeasurementCount(100)
        .IterationsPerMeasurement(100)
        .GC()
        .Run();
        
        // Assert: <0.5ms per creature
        
        Object.Destroy(aiObj);
    }
}
```

---

## 6. Implementation Roadmap

### Phase 1: Foundation Completion (2-3 weeks)
1. Unity Input System migration (8h)
2. PDA Tool Management Tab (12h)
3. HUD Visor UX completion (6h)
4. Testing & bug fixes (8h)

### Phase 2: Advanced Systems (6-8 weeks)
1. Advanced AI System (24h)
2. Procedural Cave Generation (32h)
3. Advanced Crafting System (16h)
4. Base Power Grid System (20h)
5. Weather & Environmental Hazards (24h)

### Phase 3: Performance & Optimization (3-4 weeks)
1. Memory Profiling & Optimization (16h)
2. LOD System Implementation (12h)
3. Async Loading & Streaming (20h)
4. Burst Compilation & Jobs System (24h)

### Phase 4: Polish & Juice (4-5 weeks)
1. VFX System (16h)
2. Audio System Enhancement (12h)
3. Camera Juice & Screen Effects (8h)
4. Animation Polish (16h)

### Phase 5: Testing & QA (3-4 weeks)
1. Unit Testing Framework (24h)
2. Integration Testing (16h)
3. Performance Testing & Benchmarking (12h)
4. Playtesting & bug fixes (20h)

### Phase 6: Production Readiness (2-3 weeks)
1. Build Pipeline & Deployment (12h)
2. Localization System (16h)
3. Analytics & Telemetry (12h)
4. Documentation & Onboarding (16h)

**Total Estimated Time:** 6-12 months (depending on team size)

---

## 7. Technical Debt Management

### 7.1 Current Technical Debt

1. **Candice AI Integration**
   - Warnings in GetType() methods
   - Outdated animation system
   - **Action:** Refactor or replace with custom AI

2. **Easy Save 3 Dependency**
   - ES3SerializableDictionary removed
   - **Action:** Use standard Dictionary<T, T>

3. **Input System**
   - Old Input Manager (deprecated)
   - **Action:** Migrate to Unity Input System

4. **Missing Tests**
   - No unit tests
   - No integration tests
   - **Action:** Implement testing framework

### 7.2 Refactoring Priorities

| Priority | System | Effort | Impact |
|----------|--------|--------|--------|
| HIGH | Input System Migration | 8h | High |
| HIGH | Testing Framework | 24h | High |
| MEDIUM | AI System Refactor | 24h | Medium |
| MEDIUM | Save System Cleanup | 8h | Low |
| LOW | Code Documentation | 16h | Medium |

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-XX  
**Status:** DRAFT  
**Next Phase:** Task Breakdown & Implementation

# INTERFACE STRATEGY — Ghost Interface Inquisition

> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md § Architecture First · § Ownership / Ambiguity / External Patch Compliance  

---

## 1. GHOST INTERFACE DEFINITION

A **Ghost Interface** is defined as:
1. Declared in first-party code (`GlobalRegistryContracts.cs`), **and**
2. Either **zero implementations** found in production runtime, **or** **multiple conflicting definitions** causing ambiguous dispatch.

---

## 2. INTERDICTED INTERFACES

### 2.1 `IRenderable` — ✅ NOT A GHOST (ACTIVE)

**Declared:** `GlobalRegistryContracts.cs:44`
```csharp
public interface IRenderable
{
    void Render(float deltaTime);
}
```

**Implementations:**
- `HectonUnderwaterVisuals.cs` — `IRenderable`

**Consumers:**
- `RenderDispatcher.HandleBeginCameraRendering()` iterates `GlobalRegistry.Renderables` and calls `Render(deltaTime)`.

**Verdict:** Fully wired. SRP callback fan-out is functional.  
**Action:** NONE. Continue using for camera-relative render callbacks that must run outside standard `Tick()` cadence.

---

### 2.2 `IUIService` — ❌ CONFIRMED GHOST

**Declared:** `GlobalRegistryContracts.cs:454`
```csharp
public interface IUIService
{
    bool IsInitialized { get; }
}
```

**Implementations:** **ZERO** in production runtime.

**Registry Slot:** `GlobalRegistry.UI` exists and is publicly exposed, but always `null` at runtime.

**Evidence:**
```powershell
> grep -r "class\s+\w+\s*:\s*.*IUIService" Assets/_Project/Scripts/
# No results
```

**Impact:** Any system calling `GlobalRegistry.UI?.IsInitialized` receives `null` and may fail silently or take fallback paths that bypass intended UI initialization gates.

**Recommended Path:**

| Option | Description | Effort | Risk |
|--------|-------------|--------|------|
| A — **Implement** | Create `HectonUIRoot : MonoBehaviour, IUIService` that tracks `Canvas` readiness and HUD controller init state. Register in bootstrap. | 2h | Low |
| B — **Delete** | Remove `IUIService` from `GlobalRegistryContracts.cs`, remove `GlobalRegistry.UI` slot, and delete all `?.IsInitialized` checks that rely on it. | 30m | Medium — must verify no hidden consumer in third-party or uncommitted code |
| C — **Merge into IPlayerRuntimeContext** | UI readiness is logically a player-system concern. Move `IsInitialized` to `IPlayerRuntimeContext`. | 1h | Low |

**ARCHIVARIUS RECOMMENDATION:** Option C. UI initialization state is a player-facing readiness signal, not a standalone service. The `IPlayerRuntimeContext` already owns camera, transform, and visor state; adding a `bool IsUIReady { get; }` property keeps ownership coherent.

---

### 2.3 `IDamageReceiver` — ❌ CONFLICT / PARTIAL GHOST

**Problem:** **Two incompatible definitions** exist in the same compilation unit scope.

#### Definition A — `GlobalRegistryContracts.cs:84`
```csharp
public interface IDamageReceiver
{
    void ReceiveDamage(in DamagePacket packet);
}
```

#### Definition B — `HabitatIntegrityManager.cs:59`
```csharp
public interface IDamageReceiver
{
    // Event-only damage receiver contract.
    // Downstream systems consume damage via callbacks, not polling.
}
```
*(Note: The HabitatIntegrityManager file declares its own nested/interface definition with a different semantic contract.)*

**Implementations of Definition A:**
- `TraumaDispatcher.cs` — `IDamageReceiver`
- `SubmarineStructuralGrid.cs` — `IDamageReceiver` (file uses `Hecton8.Physics` namespace; resolves to GlobalRegistryContracts version via `using Hecton8.Gameplay` implicit lookup)

**Implementations of Definition B:**
- `HabitatIntegrityManager.cs` — implements the locally-defined `IDamageReceiver`

**Conflict Evidence:**
```powershell
> grep -r "class\s+\w+\s*:\s*.*IDamageReceiver" Assets/_Project/Scripts/
HabitatIntegrityManager.cs  : public sealed class HabitatIntegrityManager : ..., IDamageReceiver, ...
TraumaDispatcher.cs         : public sealed class TraumaDispatcher : ..., IDamageReceiver
SubmarineStructuralGrid.cs  : public sealed class SubmarineStructuralGrid : ..., IDamageReceiver
```

`HabitatIntegrityManager` lives in `Hecton8.Gameplay` namespace and declares a **nested/interface at file scope** with the same simple name `IDamageReceiver`. Because it is declared in the same namespace and file, the local definition **shadows** the `GlobalRegistryContracts` version for all code inside that file. Any external caller casting `HabitatIntegrityManager` to `IDamageReceiver` will bind to the local definition, not the global one, creating a type-mismatch at the ABI level if the contracts diverge.

**Current Runtime Behavior:**
- `TraumaDispatcher` → uses global `ReceiveDamage(in DamagePacket)` — **functional**
- `SubmarineStructuralGrid` → uses global `ReceiveDamage(in DamagePacket)` — **functional**
- `HabitatIntegrityManager` → uses local event-only contract — **isolated, but ambiguous**

**Recommended Path:**

| Step | Action | File |
|------|--------|------|
| 1 | **Rename** HabitatIntegrityManager's local interface to `IHabitatIntegrityReceiver` or delete it entirely if unused. | `HabitatIntegrityManager.cs` |
| 2 | **Unify** all damage reception under the global `IDamageReceiver` (Definition A). | `GlobalRegistryContracts.cs` |
| 3 | **Implement** `ReceiveDamage(in DamagePacket)` on `HabitatIntegrityManager` and route it to the existing rupture-state callback system. | `HabitatIntegrityManager.cs` |
| 4 | **Add XML docs** clarifying that `IDamageReceiver` is the single authoritative damage contract. | `GlobalRegistryContracts.cs` |

**ARCHIVARIUS RECOMMENDATION:** Execute Steps 1–4. The local redefinition is architectural drift. Habitat integrity is a damage domain; it should speak the global protocol.

---

## 3. INTERFACE HEALTH DASHBOARD

| Interface | Implementations | Consumers | Status | Action |
|-----------|-----------------|-----------|--------|--------|
| `ITickable` | 100+ | `GameTickManager` | ✅ FULL | Maintain |
| `IUpdatable` | 100+ | `SystemDispatcher` | ✅ FULL | Maintain |
| `IFixedTickable` | 20+ | `SystemDispatcher` · `GameTickManager` | ✅ FULL | Maintain |
| `ISlowTickable` | 40+ | `SystemDispatcher` · `GameTickManager` | ✅ FULL | Maintain |
| `IRenderable` | 1 (`HectonUnderwaterVisuals`) | `RenderDispatcher` | ✅ ACTIVE | Maintain |
| `IInputService` | 1 (`InputDispatcher`) | `GlobalRegistry.Input` | ✅ FULL | Maintain |
| `IPhysicsService` | 1 (`PhysicsApplySystem`) | `GlobalRegistry.Physics` | ✅ FULL | Maintain |
| `IAudioService` | 1 (`PlayerCriticalProceduralAudioRenderer`?) | `GlobalRegistry.Audio` | ✅ FULL | Verify owner |
| `ISaveService` | 1 (`SaveManager`) | `GlobalRegistry.Save` | ✅ FULL | Maintain |
| `IWeatherService` | 1 (`GlobalWeatherDirector`) | `GlobalRegistry.Weather` | ✅ FULL | Maintain |
| `IUIService` | **0** | `GlobalRegistry.UI` | ❌ GHOST | **Delete or Merge** |
| `IDamageReceiver` | 3 (1 using wrong def) | `TraumaDispatcher` · `HabitatIntegrityManager` · `SubmarineStructuralGrid` | ❌ CONFLICT | **Unify** |

---

## 4. COMPLIANCE NOTES

- **No new interfaces** should be added to `GlobalRegistryContracts.cs` without a concrete implementation in the same PR.
- **No class** should declare a file-scoped interface that duplicates a global registry contract name.
- **Namespace rule:** If a subsystem needs a specialized contract, use a namespaced name (e.g., `IHabitatIntegrityCallbacks`) rather than shadowing a global type.

---

*Report generated by ARCHIVARIUS. Interface audit must be re-run after any GlobalRegistryContracts.cs modification.*

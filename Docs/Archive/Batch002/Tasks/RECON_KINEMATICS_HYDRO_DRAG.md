# RECON - KINEMATICS_HYDRO_DRAG

Prompt ID: KINEMATICS_HYDRO_DRAG  
Role: HYDRO_MECHANIC  
Status: PENDING VERIFICATION  
Scan date: 2026-05-12

## Scope

Recon target: Unity built-in `Rigidbody.drag` / `Rigidbody.angularDrag` equivalents in the current Unity API (`linearDamping`, `angularDamping`) and serialized YAML (`m_LinearDamping`, `m_AngularDamping`). Hydro domain rule: submarine hydrodynamic resistance must be custom, not Unity built-in damping.

## Commands Used

```powershell
rg -n "\.(drag|angularDrag|linearDamping|angularDamping)\s*=\s*([^;]+)" Assets/_Project/Scripts -g "*.cs"
rg --pcre2 -n "m_(Drag|AngularDrag|LinearDamping|AngularDamping):\s*(?!0(\.0+)?\s*$)" Assets/_Project -g "*.prefab" -g "*.unity" -g "*.asset"
```

## Hydro-Critical Finding

- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab`: was `m_AngularDamping: 0.05`; fixed to `0`. This prefab is inside the assigned submarine hydro domain.
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: now forces submarine `linearDamping` and `angularDamping` to `0f` in runtime reference/cache/restore paths. Custom Burst hydro drag supplies directional linear drag and angular damping.

## Remaining Serialized Non-Zero Damping

These are not edited here because they are outside the submarine hydro core boundary or are object-specific damping systems:

- `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`: `m_AngularDamping: 0.05`
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`: `m_AngularDamping: 0.05`
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`: `m_AngularDamping: 0.05`
- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`: `m_AngularDamping: 0.05`
- `Assets/_Project/Prefabs/Player.prefab`: `m_AngularDamping: 0.05`
- `Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab`: `m_LinearDamping: 0.18`, `m_AngularDamping: 0.55`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SulfurClumps.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_MembraneTissue.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_HydrocarbonResin.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 1.6`
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/TerritorialProxy.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 4`
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/LeviathanProxy.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 4`
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HunterProxy.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 4`
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HeavyHunterProxy.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 4`
- `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/DroneProxy.prefab`: `m_LinearDamping: 1.2`, `m_AngularDamping: 4`

## Runtime Assignment Hotspots

- `GlobalPhysicsStateManager.cs`: restores/scales cached `BaseAngularDamping`. Risk if it touches submarine after hydro zeroing. Current mitigation: `SubmarineFluidDynamics` reasserts zero damping in its runtime paths.
- `BaseModule.cs`: restores default module damping and clamps to `0.1f`. Out of hydro core; possible construction-domain review.
- `Items/PickupItem.cs` and resource authoring scripts: intentional loose-item underwater damping. Out of submarine hydro core.
- `PhysicalHandController.cs` and `BaseAirlock.cs`: snap/hand body damping restore paths. Interaction domain.
- `FaunaBrain.cs` and generated proxy authoring: fauna locomotion damping. Out of scope.
- `SargassumCollapseChunk.cs`: world collapse chunk damping. Out of scope.

## Integrator Note

The hydro-critical core submarine prefab and `SubmarineFluidDynamics` runtime damping are now custom-drag-only. Remaining non-zero damping entries should be reviewed by their owning domains, not erased blindly from this batch.

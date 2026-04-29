using System.Collections.Generic;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Cold-built collider lookup cache for interaction prompt routing.
    /// </summary>
    internal static class InteractableRegistry
    {
        // COLD ALLOC: Dictionary<int,TargetInfo>[512] - collider to interaction target cache - owner: InteractableRegistry
        private static readonly Dictionary<int, TargetInfo> s_targets = new Dictionary<int, TargetInfo>(512);
        private static int s_cachedSceneHandle = -1;

        internal readonly struct TargetInfo
        {
            public TargetInfo(
                IInteractable interactable,
                IBatteryTool batteryTool,
                BatteryCharger charger,
                BioReactor reactor,
                StorageCrate crate,
                PickupItem pickup)
            {
                Interactable = interactable;
                BatteryTool = batteryTool;
                Charger = charger;
                Reactor = reactor;
                Crate = crate;
                Pickup = pickup;
            }

            public IInteractable Interactable { get; }
            public IBatteryTool BatteryTool { get; }
            public BatteryCharger Charger { get; }
            public BioReactor Reactor { get; }
            public StorageCrate Crate { get; }
            public PickupItem Pickup { get; }
            public bool HasAny =>
                Interactable != null ||
                BatteryTool != null ||
                Charger != null ||
                Reactor != null ||
                Crate != null ||
                Pickup != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_targets.Clear();
            s_cachedSceneHandle = -1;
        }

        internal static void WarmSceneCache()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            int sceneHandle = activeScene.handle;
            if (sceneHandle == s_cachedSceneHandle && s_targets.Count > 0)
                return;

            s_targets.Clear();
            s_cachedSceneHandle = sceneHandle;

            Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                TargetInfo info = ResolveTargetInfo(collider);
                if (!info.HasAny)
                    continue;

                s_targets[collider.GetInstanceID()] = info;
            }
        }

        internal static bool TryResolve(Collider collider, out TargetInfo info)
        {
            if (collider == null)
            {
                info = default;
                return false;
            }

            int instanceId = collider.GetInstanceID();
            if (s_targets.TryGetValue(instanceId, out info))
                return info.HasAny;

            info = ResolveTargetInfo(collider);
            s_targets[instanceId] = info;
            return info.HasAny;
        }

        private static TargetInfo ResolveTargetInfo(Collider collider)
        {
            if (collider == null)
                return default;

            IInteractable interactable = null;
            if (!collider.TryGetComponent(out interactable))
                interactable = collider.GetComponentInParent<IInteractable>();

            IBatteryTool batteryTool = null;
            if (!collider.TryGetComponent(out batteryTool))
                batteryTool = collider.GetComponentInParent<IBatteryTool>();

            BatteryCharger charger = null;
            if (!collider.TryGetComponent(out charger))
                charger = collider.GetComponentInParent<BatteryCharger>();

            BioReactor reactor = null;
            if (!collider.TryGetComponent(out reactor))
                reactor = collider.GetComponentInParent<BioReactor>();

            StorageCrate crate = null;
            if (!collider.TryGetComponent(out crate))
                crate = collider.GetComponentInParent<StorageCrate>();

            PickupItem pickup = null;
            if (!collider.TryGetComponent(out pickup))
                pickup = collider.GetComponentInParent<PickupItem>();

            return new TargetInfo(interactable, batteryTool, charger, reactor, crate, pickup);
        }
    }
}

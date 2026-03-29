using Hecton8.AI;
using Hecton8.Building;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public static class FieldLoadoutAdvisor
    {
        public readonly struct LoadoutAdvice
        {
            public readonly string PresetName;
            public readonly string Summary;

            public LoadoutAdvice(string presetName, string summary)
            {
                PresetName = presetName;
                Summary = summary;
            }
        }

        public static bool TryBuildForwardAdvice(Transform origin, float range, LayerMask mask, out LoadoutAdvice advice)
        {
            advice = default;
            if (origin == null)
                return false;

            if (!UnityEngine.Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Collide))
                return false;

            return TryBuildAdvice(hit.collider, hit.distance, out advice);
        }

        public static bool TryBuildAdvice(Component source, float distance, out LoadoutAdvice advice)
        {
            advice = default;
            if (source == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(source, out FieldTargetDescriptor descriptor))
                return TryBuildDescriptorAdvice(descriptor, distance, out advice);

            BaseModule module = source.GetComponent<BaseModule>() ?? source.GetComponentInParent<BaseModule>();
            if (module != null)
            {
                advice = new LoadoutAdvice(
                    "CONSTRUCTION",
                    module.IsFlooded
                        ? "Flooded module ahead. Construction kit is a strong option if you want repair, cutter, and builder coverage."
                        : "Serviceable module ahead. Construction kit fits this situation well.");
                return true;
            }

            ResourceNode node = source.GetComponent<ResourceNode>() ?? source.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                advice = new LoadoutAdvice(
                    "FIELD RECOVERY",
                    node.IsDepleted
                        ? "Spent resource node ahead. Recovery tools still fit this route if you want to clear the area."
                        : "Live resource node ahead. Recovery kit is a strong option here.");
                return true;
            }

            HectonBaseAI ai = source.GetComponent<HectonBaseAI>() ?? source.GetComponentInParent<HectonBaseAI>();
            if (ai != null)
            {
                advice = new LoadoutAdvice(
                    "DEFENSE",
                    ai.CurrentState == HectonBaseAI.AIState.Aggressive
                        ? "Aggressive contact ahead. Defense kit gives the safest margin."
                        : "Bioform contact ahead. Defense tools are the safer choice if you want control.");
                return true;
            }

            PickupItem pickup = source.GetComponent<PickupItem>() ?? source.GetComponentInParent<PickupItem>();
            if (pickup != null)
            {
                advice = new LoadoutAdvice(
                    "FIELD RECOVERY",
                    "Recoverable field asset ahead. Recovery kit is an efficient option.");
                return true;
            }

            ScannableTarget scannable = source.GetComponent<ScannableTarget>() ?? source.GetComponentInParent<ScannableTarget>();
            if (scannable != null)
            {
                advice = new LoadoutAdvice(
                    "EXPLORATION",
                    "Scannable point ahead. Exploration kit is a good fit for route and intel work.");
                return true;
            }

            return false;
        }

        private static bool TryBuildDescriptorAdvice(FieldTargetDescriptor descriptor, float distance, out LoadoutAdvice advice)
        {
            advice = default;
            if (descriptor == null)
                return false;

            switch (descriptor.Role)
            {
                case FieldTargetRole.CargoLight:
                case FieldTargetRole.CargoWork:
                case FieldTargetRole.CargoHeavy:
                case FieldTargetRole.CargoOverweight:
                case FieldTargetRole.SalvagePickup:
                case FieldTargetRole.ResourceCache:
                case FieldTargetRole.ResourceNodeActive:
                case FieldTargetRole.ResourceNodeDepleted:
                    advice = new LoadoutAdvice(
                        "FIELD RECOVERY",
                        $"Recovery lane ahead at {distance:0.0} m. Recovery tools are a strong fit if you want salvage or cargo control.");
                    return true;

                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                case FieldTargetRole.HazardProbe:
                case FieldTargetRole.ExpeditionCheckpoint:
                case FieldTargetRole.StructureRelay:
                    advice = new LoadoutAdvice(
                        "EXPLORATION",
                        $"Route or intel objective ahead at {distance:0.0} m. Exploration kit fits this situation well.");
                    return true;

                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                    advice = new LoadoutAdvice(
                        "CONSTRUCTION",
                        $"Service or build target ahead at {distance:0.0} m. Construction kit covers builder, repair, and service work well.");
                    return true;

                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                    advice = new LoadoutAdvice(
                        "DEFENSE",
                        $"Combat contact ahead at {distance:0.0} m. Defense kit is the safer option before closing distance.");
                    return true;
            }

            return false;
        }
    }
}

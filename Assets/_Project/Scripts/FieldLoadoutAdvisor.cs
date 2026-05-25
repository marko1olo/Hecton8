using Hecton8.AI;
using Hecton8.Building;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public static class FieldLoadoutAdvisor
    {
        private const string PresetConstruction = "CONSTRUCTION";
        private const string PresetFieldRecovery = "FIELD RECOVERY";
        private const string PresetDefense = "DEFENSE";
        private const string PresetExploration = "EXPLORATION";
        private const float ForwardConeTangent = 0.18f;
        private const float ForwardConeMinimumRadiusMeters = 0.75f;
        private static readonly SpatialQueryHit[] _forwardCandidates = new SpatialQueryHit[8]; // COLD ALLOC: SpatialQueryHit[8] - broadphase-backed loadout advice candidate buffer - owner: FieldLoadoutAdvisor

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

            if (!TryGetForwardTarget(origin, range, mask, out Component source, out float distance))
                return false;

            return TryBuildAdvice(source, distance, out advice);
        }

        public static bool TryBuildForwardPresetName(Transform origin, float range, LayerMask mask, out string presetName)
        {
            presetName = null;
            if (origin == null)
                return false;

            if (!TryGetForwardTarget(origin, range, mask, out Component source, out _))
                return false;

            return TryBuildPresetName(source, out presetName);
        }

        public static bool TryBuildPresetName(Component source, out string presetName)
        {
            presetName = null;
            if (source == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(source, out FieldTargetDescriptor descriptor))
                return TryBuildDescriptorPresetName(descriptor, out presetName);

            BaseModule module = ResolveLocalOrParent<BaseModule>(source);
            if (module != null)
            {
                presetName = PresetConstruction;
                return true;
            }

            ResourceNode node = ResolveLocalOrParent<ResourceNode>(source);
            if (node != null)
            {
                presetName = PresetFieldRecovery;
                return true;
            }

            FaunaBrain ai = ResolveLocalOrParent<FaunaBrain>(source);
            if (ai != null)
            {
                presetName = PresetDefense;
                return true;
            }

            PickupItem pickup = ResolveLocalOrParent<PickupItem>(source);
            if (pickup != null)
            {
                presetName = PresetFieldRecovery;
                return true;
            }

            ScannableTarget scannable = ResolveLocalOrParent<ScannableTarget>(source);
            if (scannable != null)
            {
                presetName = PresetExploration;
                return true;
            }

            return false;
        }

        public static bool TryBuildAdvice(Component source, float distance, out LoadoutAdvice advice)
        {
            advice = default;
            if (source == null)
                return false;

            if (FieldTargetDescriptor.TryResolve(source, out FieldTargetDescriptor descriptor))
                return TryBuildDescriptorAdvice(descriptor, distance, out advice);

            BaseModule module = ResolveLocalOrParent<BaseModule>(source);
            if (module != null)
            {
                advice = new LoadoutAdvice(
                    PresetConstruction,
                    module.IsFlooded
                        ? "Flooded module ahead. Construction kit is a strong option if you want repair, cutter, and builder coverage."
                        : "Serviceable module ahead. Construction kit fits this situation well.");
                return true;
            }

            ResourceNode node = ResolveLocalOrParent<ResourceNode>(source);
            if (node != null)
            {
                advice = new LoadoutAdvice(
                    PresetFieldRecovery,
                    node.IsDepleted
                        ? "Spent resource node ahead. Recovery tools still fit this route if you want to clear the area."
                        : "Live resource node ahead. Recovery kit is a strong option here.");
                return true;
            }

            FaunaBrain ai = ResolveLocalOrParent<FaunaBrain>(source);
            if (ai != null)
            {
                advice = new LoadoutAdvice(
                    PresetDefense,
                    ai.CurrentState == FaunaBrain.AIState.Aggressive
                        ? "Aggressive contact ahead. Defense kit gives the safest margin."
                        : "Bioform contact ahead. Defense tools are the safer choice if you want control.");
                return true;
            }

            PickupItem pickup = ResolveLocalOrParent<PickupItem>(source);
            if (pickup != null)
            {
                advice = new LoadoutAdvice(
                    PresetFieldRecovery,
                    "Recoverable field asset ahead. Recovery kit is an efficient option.");
                return true;
            }

            ScannableTarget scannable = ResolveLocalOrParent<ScannableTarget>(source);
            if (scannable != null)
            {
                advice = new LoadoutAdvice(
                    PresetExploration,
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
                        PresetFieldRecovery,
                        "Recovery lane ahead. Recovery tools are a strong fit if you want salvage or cargo control.");
                    return true;

                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                case FieldTargetRole.HazardProbe:
                case FieldTargetRole.ExpeditionCheckpoint:
                case FieldTargetRole.StructureRelay:
                    advice = new LoadoutAdvice(
                        PresetExploration,
                        "Route or intel objective ahead. Exploration kit fits this situation well.");
                    return true;

                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                case FieldTargetRole.PowerGeneration:
                case FieldTargetRole.PowerRelay:
                case FieldTargetRole.PowerLoad:
                    advice = new LoadoutAdvice(
                        PresetConstruction,
                        "Service, power, or build target ahead. Construction kit is a strong fit if you want builder, repair, and support coverage.");
                    return true;

                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                    advice = new LoadoutAdvice(
                        PresetDefense,
                        "Combat contact ahead. Defense kit is the safer option before closing distance.");
                    return true;
            }

            return false;
        }

        private static T ResolveLocalOrParent<T>(Component source) where T : Component
        {
            if (source == null)
                return null;

            return source.TryGetComponent(out T local)
                ? local
                : source.GetComponentInParent<T>();
        }

        private static bool TryGetForwardTarget(Transform origin, float range, LayerMask mask, out Component source, out float distance)
        {
            source = null;
            distance = 0f;

            if (origin == null || range <= 0f)
                return false;

            Vector3 originPosition = origin.position;
            Vector3 forward = origin.forward;
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                originPosition,
                range,
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Signal |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module,
                _forwardCandidates);

            bool found = false;
            float rangeSqr = range * range;
            float bestProjection = range;
            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit candidate = _forwardCandidates[i];
                if (!MatchesLayer(candidate.Layer, mask))
                    continue;

                Vector3 offset = candidate.Position - originPosition;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr > rangeSqr)
                    continue;

                float projection = Vector3.Dot(offset, forward);
                if (projection <= 0f || projection > range)
                    continue;

                float lateralSqr = distanceSqr - (projection * projection);
                float coneRadius = ForwardConeMinimumRadiusMeters + (projection * ForwardConeTangent);
                if (lateralSqr > coneRadius * coneRadius)
                    continue;

                if (found && projection >= bestProjection)
                    continue;

                Component candidateSource = candidate.Owner != null ? candidate.Owner : candidate.Transform;
                if (candidateSource == null)
                    continue;

                found = true;
                bestProjection = projection;
                source = candidateSource;
                distance = projection;
            }

            return found;
        }

        private static bool MatchesLayer(int layer, LayerMask mask)
        {
            return layer >= 0 && ((mask.value & (1 << layer)) != 0);
        }

        private static bool TryBuildDescriptorPresetName(FieldTargetDescriptor descriptor, out string presetName)
        {
            presetName = null;
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
                    presetName = PresetFieldRecovery;
                    return true;

                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                case FieldTargetRole.HazardProbe:
                case FieldTargetRole.ExpeditionCheckpoint:
                case FieldTargetRole.StructureRelay:
                    presetName = PresetExploration;
                    return true;

                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                case FieldTargetRole.PowerGeneration:
                case FieldTargetRole.PowerRelay:
                case FieldTargetRole.PowerLoad:
                    presetName = PresetConstruction;
                    return true;

                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                    presetName = PresetDefense;
                    return true;
            }

            return false;
        }
    }
}


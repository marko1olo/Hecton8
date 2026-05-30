using Hecton8.AI;
using Hecton8.Building;
using Hecton8.Construction;
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
        public const byte PresetIdNone = 0;
        public const byte PresetIdConstruction = 1;
        public const byte PresetIdFieldRecovery = 2;
        public const byte PresetIdDefense = 3;
        public const byte PresetIdExploration = 4;
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

        internal readonly struct ForwardLoadoutSnapshot
        {
            public readonly byte PresetId;
            public readonly SpatialTargetKind Kind;
            public readonly FieldTargetRole SignalRole;
            public readonly float Distance;

            public ForwardLoadoutSnapshot(byte presetId, SpatialTargetKind kind, FieldTargetRole signalRole, float distance)
            {
                PresetId = presetId;
                Kind = kind;
                SignalRole = signalRole;
                Distance = distance;
            }
        }

        private readonly struct ForwardTargetInfo
        {
            public readonly Component Source;
            public readonly SpatialTargetKind Kind;
            public readonly FieldTargetRole SignalRole;
            public readonly float Distance;

            public ForwardTargetInfo(Component source, SpatialTargetKind kind, FieldTargetRole signalRole, float distance)
            {
                Source = source;
                Kind = kind;
                SignalRole = signalRole;
                Distance = distance;
            }
        }

        private readonly struct ForwardTargetSignalInfo
        {
            public readonly SpatialTargetKind Kind;
            public readonly FieldTargetRole SignalRole;
            public readonly float Distance;

            public ForwardTargetSignalInfo(SpatialTargetKind kind, FieldTargetRole signalRole, float distance)
            {
                Kind = kind;
                SignalRole = signalRole;
                Distance = distance;
            }
        }

        public static bool TryBuildForwardAdvice(Transform origin, float range, LayerMask mask, out LoadoutAdvice advice)
        {
            advice = default;
            if (origin == null)
                return false;

            if (!TryGetForwardTargetInfo(origin, range, mask, out ForwardTargetInfo target))
                return false;

            return TryBuildForwardAdvice(in target, out advice);
        }

        public static bool TryBuildForwardPresetName(Transform origin, float range, LayerMask mask, out string presetName)
        {
            presetName = null;
            if (origin == null)
                return false;

            if (!TryGetForwardTargetInfo(origin, range, mask, out ForwardTargetInfo target))
                return false;

            return TryBuildForwardPresetName(in target, out presetName);
        }

        public static bool TryBuildForwardPresetId(Transform origin, float range, LayerMask mask, out byte presetId)
        {
            presetId = PresetIdNone;
            if (origin == null)
                return false;

            if (!TryGetForwardTargetSignalInfo(origin, range, mask, out ForwardTargetSignalInfo target))
                return false;

            return TryBuildPresetId(target.SignalRole, target.Kind, out presetId);
        }

        internal static bool TryBuildForwardSnapshot(Transform origin, float range, LayerMask mask, out ForwardLoadoutSnapshot snapshot)
        {
            snapshot = default;
            if (origin == null)
                return false;

            if (!TryGetForwardTargetSignalInfo(origin, range, mask, out ForwardTargetSignalInfo target))
                return false;

            if (!TryBuildPresetId(target.SignalRole, target.Kind, out byte presetId))
                return false;

            snapshot = new ForwardLoadoutSnapshot(presetId, target.Kind, target.SignalRole, target.Distance);
            return true;
        }

        public static bool TryGetPresetName(byte presetId, out string presetName)
        {
            switch (presetId)
            {
                case PresetIdConstruction:
                    presetName = PresetConstruction;
                    return true;
                case PresetIdFieldRecovery:
                    presetName = PresetFieldRecovery;
                    return true;
                case PresetIdDefense:
                    presetName = PresetDefense;
                    return true;
                case PresetIdExploration:
                    presetName = PresetExploration;
                    return true;
                default:
                    presetName = null;
                    return false;
            }
        }

        public static bool TryGetPresetSummary(byte presetId, out string summary)
        {
            switch (presetId)
            {
                case PresetIdConstruction:
                    summary = "Service, power, or build target ahead. Construction kit is a strong fit if you want builder, repair, and support coverage.";
                    return true;
                case PresetIdFieldRecovery:
                    summary = "Recovery lane ahead. Recovery tools are a strong fit if you want salvage or cargo control.";
                    return true;
                case PresetIdDefense:
                    summary = "Combat contact ahead. Defense kit is the safer option before closing distance.";
                    return true;
                case PresetIdExploration:
                    summary = "Route or intel objective ahead. Exploration kit fits this situation well.";
                    return true;
                default:
                    summary = null;
                    return false;
            }
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

        private static bool TryBuildForwardAdvice(in ForwardTargetInfo target, out LoadoutAdvice advice)
        {
            advice = default;

            if (target.SignalRole != FieldTargetRole.Generic &&
                TryBuildDescriptorAdvice(target.SignalRole, target.Distance, out advice))
            {
                return true;
            }

            Component source = target.Source;
            if (source is ModuleMarker marker &&
                marker.SpatialRole != FieldTargetRole.Generic &&
                TryBuildDescriptorAdvice(marker.SpatialRole, target.Distance, out advice))
            {
                return true;
            }

            if (source is BaseModule module)
            {
                advice = new LoadoutAdvice(
                    PresetConstruction,
                    module.IsFlooded
                        ? "Flooded module ahead. Construction kit is a strong option if you want repair, cutter, and builder coverage."
                        : "Serviceable module ahead. Construction kit fits this situation well.");
                return true;
            }

            if (source is ResourceNode node)
            {
                advice = new LoadoutAdvice(
                    PresetFieldRecovery,
                    node.IsDepleted
                        ? "Spent resource node ahead. Recovery tools still fit this route if you want to clear the area."
                        : "Live resource node ahead. Recovery kit is a strong option here.");
                return true;
            }

            if (source is FaunaBrain ai)
            {
                advice = new LoadoutAdvice(
                    PresetDefense,
                    ai.CurrentState == FaunaBrain.AIState.Aggressive
                        ? "Aggressive contact ahead. Defense kit gives the safest margin."
                        : "Bioform contact ahead. Defense tools are the safer choice if you want control.");
                return true;
            }

            if (source is PickupItem)
            {
                advice = new LoadoutAdvice(
                    PresetFieldRecovery,
                    "Recoverable field asset ahead. Recovery kit is an efficient option.");
                return true;
            }

            if (source is ScannableTarget || source is ScannableFragment)
            {
                advice = new LoadoutAdvice(
                    PresetExploration,
                    "Scannable point ahead. Exploration kit is a good fit for route and intel work.");
                return true;
            }

            return TryBuildKindAdvice(target.Kind, out advice);
        }

        private static bool TryBuildForwardPresetName(in ForwardTargetInfo target, out string presetName)
        {
            presetName = null;

            if (target.SignalRole != FieldTargetRole.Generic &&
                TryBuildDescriptorPresetName(target.SignalRole, out presetName))
            {
                return true;
            }

            Component source = target.Source;
            if (source is ModuleMarker marker &&
                marker.SpatialRole != FieldTargetRole.Generic &&
                TryBuildDescriptorPresetName(marker.SpatialRole, out presetName))
            {
                return true;
            }

            if (source is BaseModule)
            {
                presetName = PresetConstruction;
                return true;
            }

            if (source is ResourceNode || source is PickupItem)
            {
                presetName = PresetFieldRecovery;
                return true;
            }

            if (source is FaunaBrain)
            {
                presetName = PresetDefense;
                return true;
            }

            if (source is ScannableTarget || source is ScannableFragment)
            {
                presetName = PresetExploration;
                return true;
            }

            return TryBuildKindPresetName(target.Kind, out presetName);
        }

        private static bool TryBuildKindAdvice(SpatialTargetKind kind, out LoadoutAdvice advice)
        {
            advice = default;
            if ((kind & SpatialTargetKind.Module) != 0)
            {
                advice = new LoadoutAdvice(
                    PresetConstruction,
                    "Service or build target ahead. Construction kit fits this situation well.");
                return true;
            }

            if ((kind & (SpatialTargetKind.Resource | SpatialTargetKind.Pickup)) != 0)
            {
                advice = new LoadoutAdvice(
                    PresetFieldRecovery,
                    "Recoverable field asset ahead. Recovery kit is an efficient option.");
                return true;
            }

            if ((kind & SpatialTargetKind.Bioform) != 0)
            {
                advice = new LoadoutAdvice(
                    PresetDefense,
                    "Bioform contact ahead. Defense tools are the safer choice if you want control.");
                return true;
            }

            if ((kind & (SpatialTargetKind.Signal | SpatialTargetKind.Scannable)) != 0)
            {
                advice = new LoadoutAdvice(
                    PresetExploration,
                    "Route or intel objective ahead. Exploration kit fits this situation well.");
                return true;
            }

            return false;
        }

        private static bool TryBuildKindPresetName(SpatialTargetKind kind, out string presetName)
        {
            presetName = null;
            if ((kind & SpatialTargetKind.Module) != 0)
                presetName = PresetConstruction;
            else if ((kind & (SpatialTargetKind.Resource | SpatialTargetKind.Pickup)) != 0)
                presetName = PresetFieldRecovery;
            else if ((kind & SpatialTargetKind.Bioform) != 0)
                presetName = PresetDefense;
            else if ((kind & (SpatialTargetKind.Signal | SpatialTargetKind.Scannable)) != 0)
                presetName = PresetExploration;

            return presetName != null;
        }

        private static bool TryBuildDescriptorAdvice(FieldTargetDescriptor descriptor, float distance, out LoadoutAdvice advice)
        {
            advice = default;
            if (descriptor == null)
                return false;

            return TryBuildDescriptorAdvice(descriptor.Role, distance, out advice);
        }

        private static bool TryBuildDescriptorAdvice(FieldTargetRole role, float distance, out LoadoutAdvice advice)
        {
            advice = default;
            switch (role)
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

            if (source.TryGetComponent(out T local))
                return local;

            return TryResolveComponentInParents(source.transform.parent, out T parent)
                ? parent
                : null;
        }

        private static bool TryResolveComponentInParents<T>(Transform current, out T component) where T : Component
        {
            component = null;

            for (; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out component))
                    return true;
            }

            return false;
        }

        private static bool TryGetForwardTargetInfo(Transform origin, float range, LayerMask mask, out ForwardTargetInfo target)
        {
            target = default;

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
                target = new ForwardTargetInfo(candidateSource, candidate.Kind, candidate.SignalRole, projection);
            }

            return found;
        }

        private static bool TryGetForwardTargetSignalInfo(Transform origin, float range, LayerMask mask, out ForwardTargetSignalInfo target)
        {
            target = default;

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

                found = true;
                bestProjection = projection;
                target = new ForwardTargetSignalInfo(candidate.Kind, candidate.SignalRole, projection);
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

            return TryBuildDescriptorPresetName(descriptor.Role, out presetName);
        }

        private static bool TryBuildPresetId(FieldTargetRole role, SpatialTargetKind kind, out byte presetId)
        {
            if (role != FieldTargetRole.Generic &&
                TryBuildDescriptorPresetId(role, out presetId))
            {
                return true;
            }

            return TryBuildKindPresetId(kind, out presetId);
        }

        private static bool TryBuildKindPresetId(SpatialTargetKind kind, out byte presetId)
        {
            if ((kind & SpatialTargetKind.Module) != 0)
                presetId = PresetIdConstruction;
            else if ((kind & (SpatialTargetKind.Resource | SpatialTargetKind.Pickup)) != 0)
                presetId = PresetIdFieldRecovery;
            else if ((kind & SpatialTargetKind.Bioform) != 0)
                presetId = PresetIdDefense;
            else if ((kind & (SpatialTargetKind.Signal | SpatialTargetKind.Scannable)) != 0)
                presetId = PresetIdExploration;
            else
                presetId = PresetIdNone;

            return presetId != PresetIdNone;
        }

        private static bool TryBuildDescriptorPresetId(FieldTargetRole role, out byte presetId)
        {
            switch (role)
            {
                case FieldTargetRole.CargoLight:
                case FieldTargetRole.CargoWork:
                case FieldTargetRole.CargoHeavy:
                case FieldTargetRole.CargoOverweight:
                case FieldTargetRole.SalvagePickup:
                case FieldTargetRole.ResourceCache:
                case FieldTargetRole.ResourceNodeActive:
                case FieldTargetRole.ResourceNodeDepleted:
                    presetId = PresetIdFieldRecovery;
                    return true;

                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                case FieldTargetRole.HazardProbe:
                case FieldTargetRole.ExpeditionCheckpoint:
                case FieldTargetRole.StructureRelay:
                    presetId = PresetIdExploration;
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
                    presetId = PresetIdConstruction;
                    return true;

                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                    presetId = PresetIdDefense;
                    return true;
                default:
                    presetId = PresetIdNone;
                    return false;
            }
        }

        private static bool TryBuildDescriptorPresetName(FieldTargetRole role, out string presetName)
        {
            presetName = null;
            switch (role)
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


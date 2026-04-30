using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Construction
{
    internal enum DroneFleetTaskKind : byte
    {
        None = 0,
        RepairModule = 1,
        CutParasite = 2
    }

    internal readonly struct DroneFleetTask
    {
        public DroneFleetTask(DroneFleetTaskKind kind, BaseModule module, Vector3 position, float radius)
        {
            Kind = kind;
            Module = module;
            Position = position;
            Radius = radius;
        }

        public DroneFleetTaskKind Kind { get; }
        public BaseModule Module { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public bool IsValid => Kind != DroneFleetTaskKind.None && Module != null;
    }

    /// <summary>
    /// Read-only fleet snapshot consumed by diagnostics owners such as the submarine OS.
    /// </summary>
    public readonly struct HectonDroneFleetSnapshot
    {
        public HectonDroneFleetSnapshot(
            int activeHubCount,
            int activeDroneCount,
            int assignedTaskCount,
            int dockedStasisSlotCount,
            int destroyedDroneCount,
            bool emergencyOverclockActive,
            SubmarineEmergencyLevel emergencyLevel)
        {
            ActiveHubCount = activeHubCount;
            ActiveDroneCount = activeDroneCount;
            AssignedTaskCount = assignedTaskCount;
            DockedStasisSlotCount = dockedStasisSlotCount;
            DestroyedDroneCount = destroyedDroneCount;
            EmergencyOverclockActive = emergencyOverclockActive;
            EmergencyLevel = emergencyLevel;
        }

        public int ActiveHubCount { get; }
        public int ActiveDroneCount { get; }
        public int AssignedTaskCount { get; }
        public int DockedStasisSlotCount { get; }
        public int DestroyedDroneCount { get; }
        public bool EmergencyOverclockActive { get; }
        public SubmarineEmergencyLevel EmergencyLevel { get; }
    }

    /// <summary>
    /// Fleet telemetry bridge. The submarine OS and any diegetic diagnostics can subscribe without scene scans.
    /// </summary>
    public static class HectonDroneFleetEvents
    {
        public delegate void SnapshotUpdatedHandler(in HectonDroneFleetSnapshot snapshot);

        public static event SnapshotUpdatedHandler OnSnapshotUpdated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnSnapshotUpdated = null;
        }

        internal static void RaiseSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            OnSnapshotUpdated?.Invoke(snapshot);
        }
    }

    /// <summary>
    /// Central zero-alloc fleet arbitration owner for repair drones.
    /// It replaces isolated per-hub target scans with a shared priority queue and bounded claim counters.
    /// </summary>
    internal static class DroneFleetManager
    {
        private const int InitialDroneCapacity = 16;
        private const int InitialTaskCapacity = 64;
        private const int DefaultMaxClaimsPerTarget = 2;
        private const float MinimumScoreDistanceMeters = 0.75f;
        private const float RuptureCriticalityBonus = 2.5f;
        private const float FloodCriticalityBonus = 2f;
        private const float BreachCriticalityBonus = 3f;
        private const float CascadeCriticalityBonus = 1.5f;
        private const float ParasiteCriticalityBonus = 4f;
        private const float AirReserveCriticalityScale = 1.5f;
        private const float EmergencyCriticalityScale = 1.35f;
        private const float SeparationDistanceEpsilon = 0.0001f;
        private const float RouteFollowWeight = 1f;
        private const float DroneAvoidanceWeight = 1.9f;
        private const float PlayerAvoidanceWeight = DroneAvoidanceWeight * 3f;
        private const float SwarmAlignmentWeight = 0.35f;
        private const float MaxSwarmSteeringMagnitude = 6f;
        private const float EmergencyThrusterSpeedMultiplier = 3f;
        private const float EmergencyBatteryDrainMultiplier = 5f;

        private struct RepairTaskCandidate
        {
            public DroneFleetTaskKind Kind;
            public BaseModule Module;
            public int ModuleIndex;
            public Vector3 Position;
            public float Radius;
            public float Score;
            public float CriticalityWeight;
        }

        // COLD ALLOC: List<RepairDroneEntity>[16] — active pooled repair-drone registry for fleet arbitration and boid separation — owner: DroneFleetManager
        private static readonly List<RepairDroneEntity> s_ActiveDrones = new List<RepairDroneEntity>(InitialDroneCapacity);
        // COLD ALLOC: RepairTaskCandidate[64] — binary-heap backing store for rupture repair arbitration — owner: DroneFleetManager
        private static RepairTaskCandidate[] s_TaskHeap = new RepairTaskCandidate[InitialTaskCapacity];
        private static int s_TaskHeapCount;
        private static NativeArray<int> s_TaskClaimCounts;
        private static bool s_Initialized;
        private static bool s_FleetSacrificeRequested;
        private static int s_DestroyedDroneCount;
        private static SubmarineEmergencyLevel s_EmergencyLevel;
        private static HectonDroneFleetSnapshot s_LastSnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (s_Initialized)
                HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSubmarineSnapshotUpdated;

            s_ActiveDrones.Clear();
            s_TaskHeapCount = 0;
            s_FleetSacrificeRequested = false;
            s_DestroyedDroneCount = 0;
            s_EmergencyLevel = SubmarineEmergencyLevel.Nominal;
            s_LastSnapshot = default;
            s_Initialized = false;

            if (s_TaskClaimCounts.IsCreated)
            {
                s_TaskClaimCounts.Dispose();
                s_TaskClaimCounts = default;
            }
        }

        internal static HectonDroneFleetSnapshot CurrentSnapshot
        {
            get
            {
                EnsureInitialized();
                return s_LastSnapshot;
            }
        }

        internal static bool IsEmergencyOverclockActive
        {
            get
            {
                EnsureInitialized();
                return s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate;
            }
        }

        internal static float ResolveThrusterSpeedMultiplier()
        {
            return IsEmergencyOverclockActive ? EmergencyThrusterSpeedMultiplier : 1f;
        }

        internal static float ResolveBatteryDrainMultiplier()
        {
            return IsEmergencyOverclockActive ? EmergencyBatteryDrainMultiplier : 1f;
        }

        public static void RequestFleetSacrifice()
        {
            EnsureInitialized();
            s_FleetSacrificeRequested = true;
            PublishSnapshot();
        }

        internal static bool ConsumeFleetSacrificeFlag()
        {
            EnsureInitialized();
            if (!s_FleetSacrificeRequested)
                return false;

            s_FleetSacrificeRequested = false;
            PublishSnapshot();
            return true;
        }

        internal static void RegisterActiveDrone(RepairDroneEntity drone)
        {
            if (drone == null)
                return;

            EnsureInitialized();
            for (int i = 0; i < s_ActiveDrones.Count; i++)
            {
                if (ReferenceEquals(s_ActiveDrones[i], drone))
                {
                    PublishSnapshot();
                    return;
                }
            }

            s_ActiveDrones.Add(drone);
            PublishSnapshot();
        }

        internal static void UnregisterActiveDrone(RepairDroneEntity drone)
        {
            if (drone == null)
                return;

            for (int i = s_ActiveDrones.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_ActiveDrones[i], drone))
                    s_ActiveDrones.RemoveAt(i);
            }

            PublishSnapshot();
        }

        internal static void ReportDroneDestroyed()
        {
            EnsureInitialized();
            s_DestroyedDroneCount++;
            PublishSnapshot();
        }

        internal static void NotifyFleetStateChanged()
        {
            EnsureInitialized();
            PublishSnapshot();
        }

        internal static bool TryAssignRepairTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out BaseModule target,
            out float assignmentScore,
            out float criticalityWeight)
        {
            target = null;
            if (!TryAssignFleetTask(hub, dispatchIntegrityThreshold, out DroneFleetTask task, out assignmentScore, out criticalityWeight))
                return false;

            target = task.Module;
            return target != null;
        }

        internal static bool TryAssignFleetTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out DroneFleetTask task,
            out float assignmentScore,
            out float criticalityWeight)
        {
            task = default;
            assignmentScore = 0f;
            criticalityWeight = 0f;

            if (hub == null)
                return false;

            EnsureInitialized();

            ConstructionManager manager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            IReadOnlyList<GameObject> modules = manager != null ? manager.SpawnedModules : null;
            if (modules == null || modules.Count == 0)
                return false;

            EnsureTaskCapacity(modules.Count * 2);
            ClearClaimCounts(modules.Count);
            RebuildActiveClaimCounts(modules, modules.Count);
            ResetHeap();

            Vector3 hubPosition = hub.DockPosition;
            PowerGrid hubGrid = hub.CurrentGrid;
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;

            for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                GameObject moduleObject = modules[moduleIndex];
                if (moduleObject == null ||
                    !moduleObject.activeInHierarchy ||
                    !moduleObject.TryGetComponent(out BaseModule module))
                {
                    continue;
                }

                if (IsEligibleRepairTarget(hubGrid, module, dispatchIntegrityThreshold))
                {
                    float distanceMeters = Vector3.Distance(hubPosition, module.transform.position);
                    float taskCriticality = ResolveCriticalityWeight(module);
                    float taskScore = ComputeTaskAssignmentScore(distanceMeters, taskCriticality);
                    PushTask(new RepairTaskCandidate
                    {
                        Kind = DroneFleetTaskKind.RepairModule,
                        Module = module,
                        ModuleIndex = moduleIndex,
                        Position = module.transform.position,
                        Radius = 0f,
                        Score = taskScore,
                        CriticalityWeight = taskCriticality
                    });
                }

                if (floraInteractionManager == null ||
                    module.ParasiteInfectionLevel <= 0.0001f ||
                    IsDifferentGrid(hubGrid, module) ||
                    !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                {
                    continue;
                }

                float parasiteDistanceMeters = Vector3.Distance(hubPosition, parasiteTarget.Position);
                float parasiteCriticality = ResolveParasiteCriticalityWeight(module, in parasiteTarget);
                float parasiteScore = ComputeTaskAssignmentScore(parasiteDistanceMeters, parasiteCriticality);
                PushTask(new RepairTaskCandidate
                {
                    Kind = DroneFleetTaskKind.CutParasite,
                    Module = module,
                    ModuleIndex = moduleIndex,
                    Position = parasiteTarget.Position,
                    Radius = parasiteTarget.Radius,
                    Score = parasiteScore,
                    CriticalityWeight = parasiteCriticality
                });
            }

            while (TryPopTask(out RepairTaskCandidate bestTask))
            {
                if (bestTask.Module == null)
                    continue;

                if (s_TaskClaimCounts[bestTask.ModuleIndex] >= DefaultMaxClaimsPerTarget)
                    continue;

                s_TaskClaimCounts[bestTask.ModuleIndex] = s_TaskClaimCounts[bestTask.ModuleIndex] + 1;
                task = new DroneFleetTask(
                    bestTask.Kind,
                    bestTask.Module,
                    bestTask.Position,
                    bestTask.Radius);
                assignmentScore = bestTask.Score;
                criticalityWeight = bestTask.CriticalityWeight;
                PublishSnapshot();
                return true;
            }

            PublishSnapshot();
            return false;
        }

        internal static Vector3 ResolveSwarmAvoidance(
            RepairDroneEntity drone,
            Vector3 position,
            float droneSeparationRadius,
            float playerSeparationRadius)
        {
            return ResolveSwarmSteering(
                drone,
                position,
                Vector3.zero,
                Vector3.zero,
                droneSeparationRadius,
                droneSeparationRadius,
                playerSeparationRadius,
                0f);
        }

        internal static Vector3 ResolveSwarmSteering(
            RepairDroneEntity drone,
            Vector3 position,
            Vector3 currentVelocity,
            Vector3 routeDirection,
            float perceptionRadius,
            float droneSeparationRadius,
            float playerSeparationRadius,
            float cohesionWeight)
        {
            Vector3 totalAvoidance = Vector3.zero;
            Vector3 alignmentSum = Vector3.zero;
            Vector3 cohesionSum = Vector3.zero;
            int neighborCount = 0;

            if (perceptionRadius > 0f)
            {
                float maxPerceptionDistanceSq = perceptionRadius * perceptionRadius;
                float maxDroneDistanceSq = droneSeparationRadius * droneSeparationRadius;
                for (int i = 0; i < s_ActiveDrones.Count; i++)
                {
                    RepairDroneEntity other = s_ActiveDrones[i];
                    if (other == null || ReferenceEquals(other, drone) || !other.IsSwarmAvoidanceParticipant)
                        continue;

                    Vector3 otherPosition = other.SwarmPosition;
                    Vector3 offset = position - otherPosition;
                    float distanceSq = offset.sqrMagnitude;
                    if (distanceSq <= SeparationDistanceEpsilon || distanceSq > maxPerceptionDistanceSq)
                        continue;

                    neighborCount++;
                    alignmentSum += other.SwarmVelocity;
                    cohesionSum += otherPosition;

                    if (droneSeparationRadius > 0f && distanceSq <= maxDroneDistanceSq)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        totalAvoidance += (offset / distance) * (DroneAvoidanceWeight / Mathf.Max(distanceSq, 0.04f));
                    }
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null && playerSeparationRadius > 0f)
            {
                Vector3 playerOffset = position - playerTransform.position;
                float playerDistanceSq = playerOffset.sqrMagnitude;
                float maxPlayerDistanceSq = playerSeparationRadius * playerSeparationRadius;
                if (playerDistanceSq > SeparationDistanceEpsilon && playerDistanceSq <= maxPlayerDistanceSq)
                {
                    float playerDistance = Mathf.Sqrt(playerDistanceSq);
                    totalAvoidance += (playerOffset / playerDistance) * (PlayerAvoidanceWeight / Mathf.Max(playerDistanceSq, 0.04f));
                }
            }

            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            if (neighborCount > 0)
            {
                float inverseNeighborCount = 1f / neighborCount;
                Vector3 averageVelocity = alignmentSum * inverseNeighborCount;
                if (averageVelocity.sqrMagnitude > SeparationDistanceEpsilon)
                {
                    Vector3 currentDirection = SafeNormalize(currentVelocity);
                    alignment = (SafeNormalize(averageVelocity) - currentDirection) * SwarmAlignmentWeight;
                }

                Vector3 centerOffset = (cohesionSum * inverseNeighborCount) - position;
                if (centerOffset.sqrMagnitude > SeparationDistanceEpsilon)
                    cohesion = SafeNormalize(centerOffset) * Mathf.Clamp01(cohesionWeight);
            }

            Vector3 route = SafeNormalize(routeDirection) * RouteFollowWeight;
            return ClampVector(route + totalAvoidance + alignment + cohesion, MaxSwarmSteeringMagnitude);
        }

        public static float ComputeTaskAssignmentScore(float distanceMeters, float criticalityWeight)
        {
            float clampedDistance = Mathf.Max(MinimumScoreDistanceMeters, distanceMeters);
            return (1f / clampedDistance) * Mathf.Max(0.1f, criticalityWeight);
        }

        private static void EnsureInitialized()
        {
            if (s_Initialized)
                return;

            HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSubmarineSnapshotUpdated;
            HectonSubmarineOsEvents.OnSnapshotUpdated += HandleSubmarineSnapshotUpdated;
            s_Initialized = true;
            PublishSnapshot();
        }

        private static void HandleSubmarineSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            s_EmergencyLevel = snapshot.EmergencyLevel;
            PublishSnapshot();
        }

        private static Vector3 SafeNormalize(Vector3 vector)
        {
            float lengthSq = vector.sqrMagnitude;
            if (lengthSq <= SeparationDistanceEpsilon)
                return Vector3.zero;

            return vector / Mathf.Sqrt(lengthSq);
        }

        private static Vector3 ClampVector(Vector3 vector, float maxMagnitude)
        {
            if (maxMagnitude <= 0f)
                return Vector3.zero;

            float lengthSq = vector.sqrMagnitude;
            float maxMagnitudeSq = maxMagnitude * maxMagnitude;
            if (lengthSq <= maxMagnitudeSq)
                return vector;

            return vector * (maxMagnitude / Mathf.Sqrt(lengthSq));
        }

        private static bool IsEligibleRepairTarget(PowerGrid hubGrid, BaseModule module, float dispatchIntegrityThreshold)
        {
            if (module == null)
                return false;

            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
            bool graphRuptured = BaseDegradationSystem.IsModuleRuptured(module);
            bool belowThreshold = integrity01 < dispatchIntegrityThreshold;

            if (!belowThreshold && !module.IsFlooded && !module.HasCascadeFailure && !graphRuptured)
                return false;

            if (IsDifferentGrid(hubGrid, module))
                return false;

            return module.CurrentIntegrity < recoverableIntegrity || module.IsFlooded || module.HasCascadeFailure || graphRuptured;
        }

        private static bool IsDifferentGrid(PowerGrid hubGrid, BaseModule module)
        {
            if (hubGrid == null || module == null)
                return false;

            if (!module.TryGetComponent(out PowerNode modulePowerNode) || modulePowerNode.Grid == null)
                return false;

            return !ReferenceEquals(modulePowerNode.Grid, hubGrid);
        }

        private static float ResolveCriticalityWeight(BaseModule module)
        {
            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
            float integrityDeficit01 = 1f - integrity01;
            float weight = 1f + (integrityDeficit01 * 4f);

            if (module.IsFlooded)
                weight += FloodCriticalityBonus;

            if (module.IsBreached)
                weight += BreachCriticalityBonus;

            if (module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (BaseDegradationSystem.IsModuleRuptured(module))
                weight += RuptureCriticalityBonus;

            weight += (1f - Mathf.Clamp01(module.AirReserveNormalized)) * AirReserveCriticalityScale;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static float ResolveParasiteCriticalityWeight(BaseModule module, in FloraInteractionManager.ModuleParasiteTarget parasiteTarget)
        {
            float moduleAirRisk = module != null ? 1f - Mathf.Clamp01(module.AirReserveNormalized) : 0f;
            float infection = Mathf.Clamp01(parasiteTarget.InfectionLevel);
            float weight = ParasiteCriticalityBonus + (infection * 6f) + (moduleAirRisk * AirReserveCriticalityScale);
            if (module != null && module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static void EnsureTaskCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            if (s_TaskHeap == null || s_TaskHeap.Length < requiredCount)
            {
                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskHeap = new RepairTaskCandidate[nextCapacity]; // COLD ALLOC: RepairTaskCandidate[nextCapacity] — fleet repair-task max-heap storage — owner: DroneFleetManager
            }

            if (!s_TaskClaimCounts.IsCreated || s_TaskClaimCounts.Length < requiredCount)
            {
                if (s_TaskClaimCounts.IsCreated)
                    s_TaskClaimCounts.Dispose();

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskClaimCounts = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[nextCapacity] — per-module active-claim locks for fleet dispatch — owner: DroneFleetManager
            }
        }

        private static void ClearClaimCounts(int moduleCount)
        {
            for (int i = 0; i < moduleCount; i++)
                s_TaskClaimCounts[i] = 0;
        }

        private static void RebuildActiveClaimCounts(IReadOnlyList<GameObject> modules, int moduleCount)
        {
            for (int droneIndex = 0; droneIndex < s_ActiveDrones.Count; droneIndex++)
            {
                RepairDroneEntity drone = s_ActiveDrones[droneIndex];
                if (drone == null || !drone.HasActiveMission || drone.CurrentTarget == null)
                    continue;

                for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
                {
                    GameObject moduleObject = modules[moduleIndex];
                    if (moduleObject == null ||
                        !moduleObject.TryGetComponent(out BaseModule module) ||
                        !ReferenceEquals(module, drone.CurrentTarget))
                    {
                        continue;
                    }

                    s_TaskClaimCounts[moduleIndex] = s_TaskClaimCounts[moduleIndex] + 1;
                    break;
                }
            }
        }

        private static void ResetHeap()
        {
            s_TaskHeapCount = 0;
        }

        private static void PushTask(in RepairTaskCandidate candidate)
        {
            int index = s_TaskHeapCount++;
            s_TaskHeap[index] = candidate;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (s_TaskHeap[parent].Score >= s_TaskHeap[index].Score)
                    break;

                RepairTaskCandidate swap = s_TaskHeap[parent];
                s_TaskHeap[parent] = s_TaskHeap[index];
                s_TaskHeap[index] = swap;
                index = parent;
            }
        }

        private static bool TryPopTask(out RepairTaskCandidate candidate)
        {
            if (s_TaskHeapCount <= 0)
            {
                candidate = default;
                return false;
            }

            candidate = s_TaskHeap[0];
            s_TaskHeapCount--;
            if (s_TaskHeapCount <= 0)
                return true;

            s_TaskHeap[0] = s_TaskHeap[s_TaskHeapCount];
            int index = 0;
            while (true)
            {
                int left = (index << 1) + 1;
                if (left >= s_TaskHeapCount)
                    break;

                int right = left + 1;
                int bestChild = right < s_TaskHeapCount && s_TaskHeap[right].Score > s_TaskHeap[left].Score
                    ? right
                    : left;

                if (s_TaskHeap[index].Score >= s_TaskHeap[bestChild].Score)
                    break;

                RepairTaskCandidate swap = s_TaskHeap[index];
                s_TaskHeap[index] = s_TaskHeap[bestChild];
                s_TaskHeap[bestChild] = swap;
                index = bestChild;
            }

            return true;
        }

        private static void PublishSnapshot()
        {
            int activeHubCount = 0;
            int dockedStasisSlotCount = 0;
            List<RepairDroneHub> hubs = RepairDroneHub.ActiveHubs;
            for (int i = 0; i < hubs.Count; i++)
            {
                RepairDroneHub hub = hubs[i];
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                activeHubCount++;
                dockedStasisSlotCount += hub.ResolveDockedStasisSlotCount();
            }

            int activeDroneCount = 0;
            int assignedTaskCount = 0;
            for (int i = 0; i < s_ActiveDrones.Count; i++)
            {
                RepairDroneEntity drone = s_ActiveDrones[i];
                if (drone == null || !drone.gameObject.activeInHierarchy)
                    continue;

                activeDroneCount++;
                if (drone.CurrentTarget != null)
                    assignedTaskCount++;
            }

            HectonDroneFleetSnapshot nextSnapshot = new HectonDroneFleetSnapshot(
                activeHubCount,
                activeDroneCount,
                assignedTaskCount,
                dockedStasisSlotCount,
                s_DestroyedDroneCount,
                IsEmergencyOverclockActive,
                s_EmergencyLevel);

            if (AreSnapshotsEqual(in s_LastSnapshot, in nextSnapshot))
                return;

            s_LastSnapshot = nextSnapshot;
            HectonDroneFleetEvents.RaiseSnapshotUpdated(in nextSnapshot);
        }

        private static bool AreSnapshotsEqual(in HectonDroneFleetSnapshot a, in HectonDroneFleetSnapshot b)
        {
            return a.ActiveHubCount == b.ActiveHubCount &&
                   a.ActiveDroneCount == b.ActiveDroneCount &&
                   a.AssignedTaskCount == b.AssignedTaskCount &&
                   a.DockedStasisSlotCount == b.DockedStasisSlotCount &&
                   a.DestroyedDroneCount == b.DestroyedDroneCount &&
                   a.EmergencyOverclockActive == b.EmergencyOverclockActive &&
                   a.EmergencyLevel == b.EmergencyLevel;
        }
    }
}

using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ToolHitUtility
    {
        private static int s_x001ToolHitUtilitySignalPushDropCount;
        private const int MaxParentResolveDepth = 32;
        private static HUDNotification s_notification;
        private static PlayerToolManager s_playerToolManager;
        private static IPhysicsService s_physicsService;
        private static IPlayerMovementForceSink s_playerMovementForceSink;
        private const float PlayerEquivalentMassKg = 80f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_notification = null;
            s_playerToolManager = null;
            s_physicsService = null;
            s_playerMovementForceSink = null;
        }

        internal static void CachePlayerToolManagerCold(PlayerToolManager toolManager)
        {
            if (toolManager != null)
                s_playerToolManager = toolManager;
        }

        internal static void ClearPlayerToolManagerCold(PlayerToolManager toolManager)
        {
            if (ReferenceEquals(s_playerToolManager, toolManager))
                s_playerToolManager = null;
        }

        internal static void CachePhysicsServiceCold(IPhysicsService physicsService)
        {
            if (physicsService != null)
                s_physicsService = physicsService;
        }

        internal static void ClearPhysicsServiceCold(IPhysicsService physicsService)
        {
            if (physicsService != null && ReferenceEquals(s_physicsService, physicsService))
                s_physicsService = null;
        }

        internal static void CachePlayerMovementForceSinkCold(IPlayerMovementForceSink forceSink)
        {
            if (forceSink != null)
                s_playerMovementForceSink = forceSink;
        }

        internal static void ClearPlayerMovementForceSinkCold(IPlayerMovementForceSink forceSink)
        {
            if (forceSink != null && ReferenceEquals(s_playerMovementForceSink, forceSink))
                s_playerMovementForceSink = null;
        }

        public static bool ApplyDamage(
            Collider hitCollider,
            float damage,
            Vector3 hitPoint,
            Vector3 forceDirection,
            float impulse)
        {
            return ApplyDamage(
                hitCollider,
                damage,
                hitPoint,
                forceDirection,
                impulse,
                DamageSourceIds.PlayerToolImpact,
                CombatDamageTypes.Impact,
                0u,
                0f);
        }

        public static bool ApplyDamage(
            Collider hitCollider,
            float damage,
            Vector3 hitPoint,
            Vector3 forceDirection,
            float impulse,
            int sourceId,
            uint damageType,
            uint statusBits = 0u,
            float statusDurationSeconds = 0f)
        {
            if (hitCollider == null || !math.isfinite(damage) || damage <= 0f)
                return false;

            bool applied = false;
            float safeImpulse = math.isfinite(impulse) ? math.max(0f, impulse) : 0f;
            int safeSourceId = sourceId != 0 ? sourceId : DamageSourceIds.PlayerToolImpact;
            uint safeDamageType = damageType != 0u ? damageType : CombatDamageTypes.Impact;
            uint safeStatusBits = statusBits & (uint)CombatStatusBits.KnownRuntimeMask64;
            float safeStatusDuration = math.isfinite(statusDurationSeconds) ? math.max(0f, statusDurationSeconds) : 0f;

            if (TryQueueCentralDamage(
                    hitCollider,
                    damage,
                    hitPoint,
                    forceDirection,
                    safeImpulse,
                    safeSourceId,
                    safeDamageType,
                    safeStatusBits,
                    safeStatusDuration))
            {
                applied = true;
            }
            else if (TryApplyCuttableDamage(hitCollider, damage, hitPoint))
            {
                applied = true;
            }
            else if (TryApplyUnregisteredDamageReceiverPacket(hitCollider, damage, hitPoint, safeSourceId, safeDamageType, safeStatusBits))
            {
                applied = true;
            }

            if (safeImpulse > 0f)
                ApplyImpulse(hitCollider, forceDirection, safeImpulse, hitPoint);

            return applied;
        }

        private static bool TryApplyCuttableDamage(Collider hitCollider, float damage, Vector3 hitPoint)
        {
            if (!TryFindCuttableTarget(hitCollider, out ICuttable cuttable))
                return false;

            cuttable.ApplyCutDamage(damage, hitPoint);
            return true;
        }

        private static bool TryFindCuttableTarget(Collider hitCollider, out ICuttable cuttable)
        {
            cuttable = null;
            if (hitCollider == null)
                return false;

            if (hitCollider.TryGetComponent(out cuttable))
                return true;

            return TryFindParentComponentBounded(hitCollider.transform, out cuttable);
        }

        private static bool TryFindParentComponentBounded<T>(Transform start, out T component)
        {
            component = default;
            Transform current = start != null ? start.parent : null;
            int depth = 0;
            while (current != null && depth < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool TryFindDamageReceiverTarget(
            Collider hitCollider,
            out IDamageReceiver receiver,
            out Component receiverComponent)
        {
            receiver = null;
            receiverComponent = null;
            if (hitCollider == null)
                return false;

            if (!hitCollider.TryGetComponent(out receiver))
                TryFindParentComponentBounded(hitCollider.transform, out receiver);

            receiverComponent = receiver as Component;
            return receiverComponent != null;
        }

        private static bool TryQueueCentralDamage(
            Collider hitCollider,
            float damage,
            Vector3 hitPoint,
            Vector3 forceDirection,
            float impulse,
            int sourceId,
            uint damageType,
            uint statusBits,
            float statusDurationSeconds)
        {
            if (!TryFindDamageReceiverTarget(hitCollider, out _, out Component receiverComponent))
                return false;

            int targetId = CombatDamageRuntime.ResolveTargetId(receiverComponent.gameObject);
            if (!CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Vector3 safeHitPoint = hitPoint;
            if (!math.all(math.isfinite(new float3(safeHitPoint.x, safeHitPoint.y, safeHitPoint.z))))
                safeHitPoint = receiverComponent.transform.position;

            Vector3 normalizedDirection = NormalizeOrForward(forceDirection);
            double3 impactAup = double3.zero;
            if (TryResolveImpactPointAup(safeHitPoint, out AbsoluteUniversePosition pointAup) && pointAup.IsFinite())
            {
                double3 resolvedImpactAup = pointAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolvedImpactAup)))
                    impactAup = resolvedImpactAup;
            }

            Vector3 localPoint = receiverComponent.transform.InverseTransformPoint(safeHitPoint);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            if (!math.all(math.isfinite(localPoint3)))
                localPoint3 = float3.zero;

            CombatDamageRequest signal = new CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = sourceId <= 0 ? (ushort)0 : sourceId >= ushort.MaxValue ? (ushort)ushort.MaxValue : (ushort)sourceId,
                Amount = damage,
                ImpulseMagnitude = math.max(0f, impulse),
                Direction = new float3(normalizedDirection.x, normalizedDirection.y, normalizedDirection.z),
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    damageType,
                    statusBits,
                    CombatWeakspotTier.None)
            };
            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = localPoint3,
                ArmorNormal = new float3(-normalizedDirection.x, -normalizedDirection.y, -normalizedDirection.z),
                LocalTemperatureCelsius = 20f,
                StatusDurationSeconds = statusDurationSeconds
            };
            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
            return true;
        }

        private static bool TryApplyUnregisteredDamageReceiverPacket(
            Collider hitCollider,
            float damage,
            Vector3 hitPoint,
            int sourceId,
            uint damageType,
            uint statusBits)
        {
            if ((statusBits & (uint)CombatStatusBits.KnownRuntimeMask64) != 0u)
                return false;

            if (!TryFindDamageReceiverTarget(hitCollider, out IDamageReceiver receiver, out Component receiverComponent))
                return false;

            int targetId = CombatDamageRuntime.ResolveTargetId(receiverComponent.gameObject);
            if (CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Vector3 localPoint = receiverComponent.transform.InverseTransformPoint(hitPoint);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            if (!math.all(math.isfinite(localPoint3)))
                localPoint3 = float3.zero;

            ushort packetSourceId = sourceId <= 0
                ? (ushort)0
                : sourceId >= ushort.MaxValue
                    ? (ushort)ushort.MaxValue
                    : (ushort)sourceId;

            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = math.max(0f, damage),
                LocalPoint = localPoint3,
                DamageType = damageType,
                IntegrityDelta = 0,
                Depth = 0f,
                SourceId = packetSourceId,
                TraumaLevel = 0
            };
            receiver.ReceiveDamage(in packet);
            return true;
        }

        public static bool TryCollectItem(Collider hitCollider, Transform interactor)
        {
            return TryCollectItem(hitCollider, interactor, out _);
        }

        public static bool TryPeekCollectible(Collider hitCollider, out ItemData itemData, out int quantity)
        {
            itemData = null;
            quantity = 0;

            if (hitCollider == null)
                return false;

            if (!hitCollider.TryGetComponent(out IInventoryPickupPreviewSource previewSource))
                TryFindParentComponentBounded(hitCollider.transform, out previewSource);

            if (previewSource == null)
                return false;

            return previewSource.TryPeekInventoryPickup(out itemData, out quantity);
        }

        public static bool TryCollectItem(Collider hitCollider, Transform interactor, out ItemData collectedItem)
        {
            collectedItem = null;

            if (hitCollider == null || interactor == null)
                return false;

            if (!hitCollider.TryGetComponent(out IInventoryPickupSource pickupSource))
                TryFindParentComponentBounded(hitCollider.transform, out pickupSource);

            if (pickupSource == null)
                return false;

            if (pickupSource is IInventoryPickupPreviewSource previewSource)
                previewSource.TryPeekInventoryPickup(out collectedItem, out _);

            PlayerInventory inventory = null;
            if (TryResolvePlayerToolManager(out PlayerToolManager toolManager))
                inventory = toolManager.Inventory;

            return pickupSource.TryHandleInventoryPickup(inventory, interactor);
        }

        public static bool TryGetRigidbody(Collider hitCollider, out Rigidbody body)
        {
            body = null;
            if (hitCollider == null)
                return false;

            body = hitCollider.attachedRigidbody;
            if (body != null)
                return true;

            TryFindParentComponentBounded(hitCollider.transform, out body);
            return body != null;
        }

        public static void ApplyImpulse(Collider hitCollider, Vector3 direction, float impulse)
        {
            Vector3 hitPoint = hitCollider != null ? hitCollider.bounds.center : Vector3.zero;
            ApplyImpulse(hitCollider, direction, impulse, hitPoint);
        }

        public static void ApplyImpulse(Collider hitCollider, Vector3 direction, float impulse, Vector3 hitPoint)
        {
            if (impulse <= 0f || !TryGetRigidbody(hitCollider, out Rigidbody body))
                return;

            Vector3 normalizedDirection = NormalizeOrForward(direction);
            if (IsPlayerBody(body))
            {
                if (!TryQueuePlayerVelocityChange(normalizedDirection * impulse, PlayerEquivalentMassKg))
                    return;

                TryApplyRelativeCarrierImpulse(normalizedDirection, impulse);
                PublishImpactSignal(hitCollider, body, hitPoint, normalizedDirection, impulse);
                return;
            }

            if (!TryQueuePhysicsForce(body, normalizedDirection * impulse, ForceMode.Impulse))
                return;

            TryApplyRelativeCarrierImpulse(normalizedDirection, impulse);
            PublishImpactSignal(hitCollider, body, hitPoint, normalizedDirection, impulse);
        }

        internal static bool TryApplyRelativeCarrierImpulse(Vector3 direction, float impulse)
        {
            if (impulse <= 0f || direction.sqrMagnitude < 0.0001f)
                return false;

            if (!TryResolvePlayerToolManager(out PlayerToolManager toolManager) ||
                !toolManager.TryResolveInteriorCarrierBody(out Rigidbody carrierBody) ||
                carrierBody == null)
            {
                return false;
            }

            Vector3 normalizedDirection = NormalizeOrForward(direction);
            return TryQueuePhysicsForce(carrierBody, -normalizedDirection * impulse, ForceMode.Impulse);
        }

        private static bool TryQueuePhysicsForce(Rigidbody body, Vector3 force, ForceMode mode)
        {
            if (body == null || !math.all(math.isfinite(new float3(force.x, force.y, force.z))))
                return false;

            IPhysicsService physicsService = s_physicsService;
            return physicsService != null &&
                   physicsService.IsInitialized &&
                   physicsService.QueueForce(body, force, mode);
        }

        private static bool TryQueuePlayerVelocityChange(Vector3 impulse, float mass)
        {
            IPlayerMovementForceSink forceSink = s_playerMovementForceSink;
            if (forceSink == null || !math.all(math.isfinite(new float3(impulse.x, impulse.y, impulse.z))))
                return false;

            float safeMass = math.max(math.isfinite(mass) && mass > 0.0001f ? mass : 1f, 0.0001f);
            Vector3 velocityDelta = impulse / safeMass;
            if (!math.all(math.isfinite(new float3(velocityDelta.x, velocityDelta.y, velocityDelta.z))))
                return false;

            forceSink.QueueExternalVelocityChange(velocityDelta);
            return true;
        }

        private static bool IsPlayerBody(Rigidbody body)
        {
            if (body == null || !TryResolvePlayerToolManager(out PlayerToolManager toolManager))
                return false;

            IPlayerRuntimeContext playerContext = toolManager.PlayerRuntimeContext;
            return playerContext != null && ReferenceEquals(playerContext.PlayerRigidbody, body);
        }

        private static void PublishImpactSignal(
            Collider hitCollider,
            Rigidbody body,
            Vector3 hitPoint,
            Vector3 normalizedDirection,
            float impulse)
        {
            if (impulse <= 0f)
                return;

            if (!math.all(math.isfinite(new float3(hitPoint.x, hitPoint.y, hitPoint.z))))
                hitPoint = body != null ? body.position : hitCollider.bounds.center;

            if (!TryResolveImpactPointAup(hitPoint, out AbsoluteUniversePosition pointAup))
                return;

            ImpactSignal signal = default;
            signal.PointAup = pointAup;
            signal.Force = impulse;
            signal.Intensity = math.saturate(math.log10(1f + (impulse * 0.01f)));
            signal.PrimaryBodyId = ResolveImpactBodyId(hitCollider, body);
            signal.WeightClass = ResolveImpactWeightClass(body);
            signal.Flags = normalizedDirection.y > 0.35f ? (byte)1 : (byte)0;
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001ToolHitUtilitySignalPushDropCount);
        }

        private static bool TryResolveImpactPointAup(Vector3 hitPoint, out AbsoluteUniversePosition pointAup)
        {
            pointAup = default;
            float3 localRuntime = new float3(hitPoint.x, hitPoint.y, hitPoint.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            if (TryResolvePlayerToolManager(out PlayerToolManager toolManager))
            {
                IPlayerRuntimeContext playerContext = toolManager.PlayerRuntimeContext;
                if (playerContext != null &&
                    playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    snapshot.Aup.IsFinite())
                {
                    double3 deltaMeters = new double3(
                        (double)localRuntime.x - snapshot.RuntimePosition.x,
                        (double)localRuntime.y - snapshot.RuntimePosition.y,
                        (double)localRuntime.z - snapshot.RuntimePosition.z);
                    pointAup = AbsoluteUniversePosition.OffsetMeters(
                        in snapshot.Aup,
                        deltaMeters);
                    if (pointAup.IsFinite())
                        return true;
                }
            }

            double3 absolute = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hitPoint);
            if (!math.all(math.isfinite(absolute)))
                return false;

            pointAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute);
            return pointAup.IsFinite();
        }

        private static byte ResolveImpactWeightClass(Rigidbody body)
        {
            if (body == null)
                return 0;

            float mass = IsPlayerBody(body)
                ? PlayerEquivalentMassKg
                : math.max(0f, body.mass);
            if (mass >= 250f)
                return 2;
            if (mass >= 40f)
                return 1;
            return 0;
        }

        private static uint ResolveImpactBodyId(Collider hitCollider, Rigidbody body)
        {
            GameObject owner = body != null
                ? body.gameObject
                : hitCollider != null ? hitCollider.gameObject : null;
            return owner != null
                ? unchecked((uint)EntityId.ToULong(owner.GetEntityId()))
                : 0u;
        }

        private static Vector3 NormalizeOrForward(Vector3 direction)
        {
            float3 direction3 = new float3(direction.x, direction.y, direction.z);
            if (!math.all(math.isfinite(direction3)))
                return Vector3.forward;

            float sqrMagnitude = math.lengthsq(direction3);
            if (sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                return direction;

            float invMagnitude = math.rsqrt(math.max(sqrMagnitude, 0.0001f));
            return direction * invMagnitude;
        }

        private static bool TryResolvePlayerToolManager(out PlayerToolManager toolManager)
        {
            toolManager = s_playerToolManager;
            return toolManager != null && toolManager.isActiveAndEnabled;
        }

        public static void ShowInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowInfo(message);
            else
                LogToolInfo(message);
        }

        public static void ShowInfo(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowInfo(in messageBuffer);
            else
                LogToolInfo(in messageBuffer);
        }

        public static void ShowWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowWarning(message);
            else
                LogToolWarning(message);
        }

        public static void ShowWarning(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            if (s_notification == null)
                HUDNotification.TryGetActive(out s_notification);

            if (s_notification != null)
                s_notification.ShowWarning(in messageBuffer);
            else
                LogToolWarning(in messageBuffer);
        }

#if UNITY_EDITOR
        private static void LogToolInfo(string message)
        {
            Hecton8.Core.H8Debug.Log($"[ToolInfo] {message}");
        }

        private static void LogToolInfo(in FixedCharBuffer messageBuffer)
        {
            Hecton8.Core.H8Debug.Log($"[ToolInfo] {messageBuffer.ToString()}");
        }

        private static void LogToolWarning(string message)
        {
            Hecton8.Core.H8Debug.LogWarning($"[ToolWarning] {message}");
        }

        private static void LogToolWarning(in FixedCharBuffer messageBuffer)
        {
            Hecton8.Core.H8Debug.LogWarning($"[ToolWarning] {messageBuffer.ToString()}");
        }
#else
        private static void LogToolInfo(string message) { }
        private static void LogToolInfo(in FixedCharBuffer messageBuffer) { }
        private static void LogToolWarning(string message) { }
        private static void LogToolWarning(in FixedCharBuffer messageBuffer) { }
#endif
    }
}


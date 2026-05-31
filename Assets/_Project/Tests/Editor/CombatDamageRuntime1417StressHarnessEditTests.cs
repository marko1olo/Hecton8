using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class CombatDamageRuntime1417StressHarnessEditTests
    {
        private const int StressPacketCount = 100000;

        [Test]
        [Explicit("Agent 1417 heavy ingress harness. Run in isolated Unity Editor test pass with GCMonitor/profiler attached.")]
        public void CombatDamageIngress_OneHundredThousandPackets_FailsClosedWithoutException()
        {
            GlobalDataVault createdVault = null;
            bool ownsVault = false;
            GameObject targetObject = null;
            DamageStressReceiver receiver = null;
            bool targetRegistered = false;
            int accepted = 0;
            int rejected = 0;

            try
            {
                EnsureVault(out createdVault, out ownsVault);
                CombatDamageRuntime.Prewarm();

                targetObject = new GameObject("CombatDamage_1417_Stress_Target");
                receiver = targetObject.AddComponent<DamageStressReceiver>();
                int targetId = unchecked((int)EntityId.ToULong(targetObject.GetEntityId()));
                targetRegistered = CombatDamageRuntime.RegisterTarget(
                    targetId,
                    receiver,
                    currentHealth: 100000f,
                    maximumHealth: 100000f,
                    kind: CombatEntityKind.Fauna,
                    armorClass: CombatArmorClass.Shell,
                    armorValue: 12f,
                    shieldValue: 0f);
                Assert.IsTrue(targetRegistered);

                CombatDamageRequest request = new CombatDamageRequest
                {
                    TargetId = targetId,
                    SourceId = 1417,
                    Amount = 1f,
                    ImpulseMagnitude = 0.25f,
                    Direction = new float3(0f, 0f, 1f),
                    PackedMeta = CombatDamageRuntime.PackSignalMeta(CombatDamageTypes.Impact, 0u, CombatWeakspotTier.None)
                };
                CombatDamageSignalDetail detail = new CombatDamageSignalDetail
                {
                    LocalPoint = float3.zero,
                    ArmorNormal = new float3(0f, 0f, -1f),
                    LocalTemperatureCelsius = 12f,
                    StatusDurationSeconds = 0f
                };

                for (int i = 0; i < StressPacketCount; i++)
                {
                    if (CombatDamageRuntime.TryQueueDamage(in request, in detail, double3.zero))
                        accepted++;
                    else
                        rejected++;
                }
            }
            finally
            {
                if (targetRegistered && targetObject != null && receiver != null)
                    CombatDamageRuntime.UnregisterTarget(unchecked((int)EntityId.ToULong(targetObject.GetEntityId())), receiver);
                if (targetObject != null)
                    UnityEngine.Object.DestroyImmediate(targetObject);
                CombatDamageRuntime.Shutdown();
                if (ownsVault && createdVault != null)
                {
                    GlobalRegistry.UnregisterDataVault(createdVault);
                    createdVault.Dispose();
                }
            }

            Assert.Greater(accepted, 0);
            Assert.Greater(rejected, 0);
            Assert.Less(accepted, StressPacketCount);
        }

        [Test]
        public void CombatDamageStaticAudit_NoPersistentNativeFieldOrForbiddenHotToken()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] files =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Combat", "CombatDamageRuntime.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Combat", "CombatDamageRuntime_StatusEffects.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Combat", "CombatDamageRuntime_VaultViews.cs"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Combat", "HectonCombatRuntime_ArmorPenetration.cs")
            };

            string[] forbidden =
            {
                "private static NativeArray<",
                "private static NativeQueue<",
                "private static NativeList<",
                "private static NativeParallelHashMap<",
                "private static NativeHashMap<",
                "string.Format",
                ".ToString(",
                ".Select(",
                ".Where(",
                ".ToArray(",
                ".ToList(",
                "Enumerable.",
                "foreach ("
            };

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                for (int j = 0; j < forbidden.Length; j++)
                    Assert.Less(source.IndexOf(forbidden[j], StringComparison.Ordinal), 0, forbidden[j] + " in " + files[i]);
            }
        }

        private static void EnsureVault(out GlobalDataVault createdVault, out bool ownsVault)
        {
            createdVault = null;
            ownsVault = false;
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (currentVault != null && !currentVault.IsCompactionFenceActive)
                return;

            createdVault = GlobalDataVault.Create();
            Assert.IsNotNull(createdVault);
            Assert.IsFalse(createdVault.IsCompactionFenceActive);
            GlobalRegistry.RegisterDataVault(createdVault);
            ownsVault = true;
        }

        private sealed class DamageStressReceiver : MonoBehaviour, IDamageReceiver, ICombatHitProfileSource
        {
            public Vector3 CombatForward => Vector3.forward;

            public float CombatHeight => 2f;

            public void ReceiveDamage(in DamagePacket packet)
            {
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    [CreateAssetMenu(fileName = "H8InputMappingFacade", menuName = "Hecton-8/Bridge/Input Mapping Facade")]
    public sealed class H8InputMappingFacade : ScriptableObject
    {
        private static int s_x001H8InputMappingFacadeSignalPushDropCount;
        private const ulong InputSyncMutationGuardMask =
            1UL << (unchecked((int)(uint)(int)BufferID.BridgeInputFacadeBindings) & 31);
        [Serializable]
        public sealed class Binding
        {
            [SerializeField] private string buttonName = "Interact";
            [SerializeField] private PlayerInputAction actionMask = PlayerInputAction.Interact;
            [SerializeField] private byte playerCommand = PlayerInputSignalCommands.Interact;
            [SerializeField] private uint actionNameHash;
            [SerializeField] private uint displayGroupHash;
            [SerializeField] private byte flags;

            public string ButtonName => buttonName;
            public PlayerInputAction ActionMask => actionMask;
            public byte PlayerCommand => playerCommand;
            public uint ActionNameHash => actionNameHash;

            public void Configure(string name, PlayerInputAction mask, byte command)
            {
                buttonName = name;
                actionMask = mask;
                playerCommand = command;
                RebuildHashes();
            }

            public void RebuildHashes()
            {
                if (string.IsNullOrWhiteSpace(buttonName))
                    buttonName = "Input";

                actionNameHash = H8BridgeHashes.ComputeFnv1A(buttonName);
                if (displayGroupHash == 0u)
                    displayGroupHash = H8BridgeHashes.ComputeFnv1A("InputFacade");
            }

            public H8InputFacadeBindingEntry ToEntry()
            {
                return new H8InputFacadeBindingEntry
                {
                    ActionNameHash = actionNameHash,
                    ButtonMask = (uint)actionMask,
                    PlayerCommand = playerCommand,
                    Flags = flags,
                    DisplayGroupHash = displayGroupHash
                };
            }
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>(32);
        [SerializeField] private bool pushOnValidateInPlayMode = true;
        [SerializeField, HideInInspector] private int validationNullBindingCount;
        [SerializeField, HideInInspector] private int validationFirstNullBindingIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeBindingCount;
        [SerializeField, HideInInspector] private int validationDuplicateActionHashCount;
        [SerializeField, HideInInspector] private int validationFirstDuplicateActionHashIndex = -1;

        public int BindingCount => bindings != null ? bindings.Count : 0;
        public bool HasValidationErrors => validationNullBindingCount > 0 || validationDuplicateActionHashCount > 0;
        public int ValidationNullBindingCount => validationNullBindingCount;
        public int ValidationFirstNullBindingIndex => validationFirstNullBindingIndex;
        public int ValidationRuntimeBindingCount => validationRuntimeBindingCount;
        public int ValidationDuplicateActionHashCount => validationDuplicateActionHashCount;
        public int ValidationFirstDuplicateActionHashIndex => validationFirstDuplicateActionHashIndex;

        public Binding GetBinding(int index)
        {
            return bindings != null && index >= 0 && index < bindings.Count ? bindings[index] : null;
        }

        public unsafe bool SyncToVault(IDataVault vault)
        {
            return SyncToVault(vault, allowAuthoringRepair: true, allowBufferGrowth: true);
        }

        internal unsafe bool SyncToVaultExistingBuffer(IDataVault vault)
        {
            return SyncToVault(vault, allowAuthoringRepair: false, allowBufferGrowth: false);
        }

        private unsafe bool SyncToVault(IDataVault vault, bool allowAuthoringRepair, bool allowBufferGrowth)
        {
            if (vault == null)
                return false;

            if (!ValidateBindings(allowAuthoringRepair))
                return false;

            int rawCount = bindings != null ? bindings.Count : 0;
            int runtimeBindingCount = validationRuntimeBindingCount;
            if (validationDuplicateActionHashCount > 0)
                return false;

            if (runtimeBindingCount <= 0)
            {
                if (!ClearExistingBuffer(vault))
                    return false;

                PublishInputUpdateSignal(0);
                GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.InputFacade, H8BridgeHashes.InputFacade, 0f);
                return true;
            }

            if (vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(InputSyncMutationGuardMask))
            {
                return false;
            }

            int activeCount = 0;
            bool syncWritten = false;
            try
            {
                if (!TryAcquireGuardedBuffer(
                        vault,
                        runtimeBindingCount,
                        allowBufferGrowth,
                        out NativeArray<H8InputFacadeBindingEntry> buffer))
                {
                    return false;
                }

                Thread.MemoryBarrier();
                ClearBuffer(buffer);

                for (int i = 0; i < rawCount; i++)
                {
                    Binding binding = bindings[i];
                    if (binding == null)
                        continue;

                    buffer[activeCount++] = binding.ToEntry();
                }

                if (activeCount != runtimeBindingCount)
                    return false;

                Thread.MemoryBarrier();
                syncWritten = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(InputSyncMutationGuardMask);
            }

            if (!syncWritten)
                return false;

            PublishInputUpdateSignal(activeCount);
            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.InputFacade, H8BridgeHashes.InputFacade, activeCount);
            return true;
        }

        private static unsafe bool ClearExistingBuffer(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(InputSyncMutationGuardMask))
            {
                return false;
            }

            bool cleared = false;
            try
            {
                if (!TryReadExistingGuardedBuffer(vault, out NativeArray<H8InputFacadeBindingEntry> buffer, out bool exists))
                    return false;

                if (exists)
                {
                    Thread.MemoryBarrier();
                    ClearBuffer(buffer);
                    Thread.MemoryBarrier();
                }

                cleared = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(InputSyncMutationGuardMask);
            }

            return cleared;
        }

        private static bool TryAcquireGuardedBuffer(
            IDataVault vault,
            int requiredLength,
            bool allowBufferGrowth,
            out NativeArray<H8InputFacadeBindingEntry> buffer)
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<H8InputFacadeBindingEntry> handle;
            if (allowBufferGrowth)
            {
                handle = vault.EnsureGenerationHandle<H8InputFacadeBindingEntry>(
                    BufferID.BridgeInputFacadeBindings,
                    requiredLength,
                    SystemID.CoreBridge,
                    NativeArrayOptions.ClearMemory);
            }
            else if (!vault.TryGetGenerationHandle<H8InputFacadeBindingEntry>(BufferID.BridgeInputFacadeBindings, out handle))
            {
                return false;
            }

            return handle.BufferID != 0u &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadExistingGuardedBuffer(
            IDataVault vault,
            out NativeArray<H8InputFacadeBindingEntry> buffer,
            out bool exists)
        {
            buffer = default;
            exists = false;
            if (vault == null ||
                !vault.TryGetGenerationHandle<H8InputFacadeBindingEntry>(BufferID.BridgeInputFacadeBindings, out VaultGenerationHandle<H8InputFacadeBindingEntry> handle) ||
                handle.BufferID == 0u)
            {
                return true;
            }

            if (!vault.TryReadHandle(in handle, out buffer))
                return false;

            if (!buffer.IsCreated)
                return false;

            exists = true;
            return true;
        }

        private static unsafe void ClearBuffer(NativeArray<H8InputFacadeBindingEntry> buffer)
        {
            if (!buffer.IsCreated || buffer.Length <= 0)
                return;

            long byteCount = (long)buffer.Length * UnsafeUtility.SizeOf<H8InputFacadeBindingEntry>();
            UnsafeUtility.MemClear(buffer.GetUnsafePtr(), byteCount);
        }

        private static void PublishInputUpdateSignal(int bindingCount)
        {
            if (!Application.isPlaying)
                return;

            DataVaultUpdateSignal signal = new DataVaultUpdateSignal
            {
                SourceHash = H8BridgeHashes.InputFacade,
                FieldHash = H8BridgeHashes.InputFacade,
                OffsetBytes = -1,
                OldValue = 0f,
                NewValue = bindingCount > 0 ? bindingCount : 0f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BufferId = (ushort)BufferID.BridgeInputFacadeBindings,
                Flags = 0
            };
            SignalBus<DataVaultUpdateSignal>.TryPushTracked(in signal, ref s_x001H8InputMappingFacadeSignalPushDropCount);
        }

        private void Reset()
        {
            EnsureDefaultBindings();
            ValidateBindings(allowAuthoringRepair: true);
        }

        private void OnValidate()
        {
            ValidateBindings(allowAuthoringRepair: true);

            if (pushOnValidateInPlayMode && Application.isPlaying)
                H8BridgeLiveSyncScheduler.RequestInputSync(this, GlobalRegistry.DataVault);
        }

        private void OnEnable()
        {
            ValidateBindings(allowAuthoringRepair: true);
        }

        [ContextMenu("Seed Default Input Bindings")]
        private void SeedDefaultBindings()
        {
            EnsureDefaultBindings();
            ValidateBindings(allowAuthoringRepair: true);
        }

        private void EnsureBindingList()
        {
            if (bindings == null)
                bindings = new List<Binding>(32);
        }

        private void EnsureDefaultBindings()
        {
            EnsureBindingList();
            if (bindings.Count > 0)
                return;

            AddDefault("Interact", PlayerInputAction.Interact, PlayerInputSignalCommands.Interact);
            AddDefault("PrimaryFire", PlayerInputAction.PrimaryFire, PlayerInputSignalCommands.PrimaryAction);
            AddDefault("SecondaryFire", PlayerInputAction.SecondaryFire, PlayerInputSignalCommands.SecondaryAction);
            AddDefault("Sprint", PlayerInputAction.Sprint, 0);
        }

        private void AddDefault(string buttonName, PlayerInputAction mask, byte command)
        {
            Binding binding = new Binding();
            binding.Configure(buttonName, mask, command);
            bindings.Add(binding);
        }

        private bool ValidateBindings(bool allowAuthoringRepair)
        {
            ResetValidationState();
            if (bindings == null)
            {
                if (!allowAuthoringRepair)
                    return false;

                EnsureBindingList();
            }

            validationRuntimeBindingCount = RebuildHashesAndCountRuntimeBindings(bindings.Count);
            validationDuplicateActionHashCount = CountDuplicateActionHashes(out validationFirstDuplicateActionHashIndex);
            return true;
        }

        private void ResetValidationState()
        {
            validationNullBindingCount = 0;
            validationFirstNullBindingIndex = -1;
            validationRuntimeBindingCount = 0;
            validationDuplicateActionHashCount = 0;
            validationFirstDuplicateActionHashIndex = -1;
        }

        private int RebuildHashesAndCountRuntimeBindings(int rawCount)
        {
            int runtimeCount = 0;
            for (int i = 0; i < rawCount; i++)
            {
                Binding binding = bindings[i];
                if (binding == null)
                {
                    validationNullBindingCount++;
                    if (validationFirstNullBindingIndex < 0)
                        validationFirstNullBindingIndex = i;
                    continue;
                }

                binding.RebuildHashes();
                runtimeCount++;
            }

            return runtimeCount;
        }

        private int CountDuplicateActionHashes(out int firstDuplicateIndex)
        {
            firstDuplicateIndex = -1;
            if (bindings == null || bindings.Count <= 1)
                return 0;

            int duplicateRows = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (!IsRuntimeHashCandidate(binding))
                    continue;

                bool duplicatesEarlierRow = false;
                for (int j = 0; j < i; j++)
                {
                    Binding previous = bindings[j];
                    if (IsRuntimeHashCandidate(previous) && previous.ActionNameHash == binding.ActionNameHash)
                    {
                        duplicatesEarlierRow = true;
                        break;
                    }
                }

                if (!duplicatesEarlierRow)
                    continue;

                duplicateRows++;
                if (firstDuplicateIndex < 0)
                    firstDuplicateIndex = i;
            }

            return duplicateRows;
        }

        private static bool IsRuntimeHashCandidate(Binding binding)
        {
            return binding != null && binding.ActionNameHash != 0u;
        }
    }
}

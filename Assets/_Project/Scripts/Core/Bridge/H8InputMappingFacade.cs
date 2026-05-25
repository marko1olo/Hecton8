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

        public int BindingCount => bindings != null ? bindings.Count : 0;

        public Binding GetBinding(int index)
        {
            return bindings != null && index >= 0 && index < bindings.Count ? bindings[index] : null;
        }

        public unsafe bool SyncToVault(IDataVault vault)
        {
            if (vault == null)
                return false;

            EnsureBindingList();
            int count = bindings.Count;
            if (count <= 0)
            {
                ClearExistingBuffer(vault);
                PublishInputUpdateSignal(0);
                GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.InputFacade, H8BridgeHashes.InputFacade, 0f);
                return true;
            }

            VaultGenerationHandle<H8InputFacadeBindingEntry> handle = vault.EnsureGenerationHandle<H8InputFacadeBindingEntry>(
                BufferID.BridgeInputFacadeBindings,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out NativeArray<H8InputFacadeBindingEntry> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < count)
            {
                return false;
            }

            Thread.MemoryBarrier();
            ClearBuffer(buffer);

            int activeCount = 0;
            for (int i = 0; i < count; i++)
            {
                Binding binding = bindings[i];
                if (binding == null)
                    continue;

                binding.RebuildHashes();
                buffer[activeCount++] = binding.ToEntry();
            }

            Thread.MemoryBarrier();
            PublishInputUpdateSignal(activeCount);
            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.InputFacade, H8BridgeHashes.InputFacade, activeCount);
            return true;
        }

        private static unsafe void ClearExistingBuffer(IDataVault vault)
        {
            if (vault == null ||
                !vault.TryGetGenerationHandle<H8InputFacadeBindingEntry>(BufferID.BridgeInputFacadeBindings, out VaultGenerationHandle<H8InputFacadeBindingEntry> handle) ||
                handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out NativeArray<H8InputFacadeBindingEntry> buffer) ||
                !buffer.IsCreated)
            {
                return;
            }

            Thread.MemoryBarrier();
            ClearBuffer(buffer);
            Thread.MemoryBarrier();
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
        }

        private void OnValidate()
        {
            EnsureBindingList();
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (binding != null)
                    binding.RebuildHashes();
            }

            if (pushOnValidateInPlayMode && Application.isPlaying)
                SyncToVault(GlobalRegistry.DataVault);
        }

        [ContextMenu("Seed Default Input Bindings")]
        private void SeedDefaultBindings()
        {
            EnsureDefaultBindings();
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
    }
}

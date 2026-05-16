using System;
using System.Collections.Generic;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    [CreateAssetMenu(fileName = "H8InputMappingFacade", menuName = "Hecton-8/Bridge/Input Mapping Facade")]
    public sealed class H8InputMappingFacade : ScriptableObject
    {
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

        public bool SyncToVault(IDataVault vault)
        {
            if (vault == null)
                return false;

            EnsureDefaultBindings();
            int count = bindings.Count;
            if (count <= 0)
                return true;

            NativeArray<H8InputFacadeBindingEntry> buffer = vault.GetBuffer<H8InputFacadeBindingEntry>(
                BufferID.BridgeInputFacadeBindings,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (!buffer.IsCreated || buffer.Length < count)
                return false;

            for (int i = 0; i < count; i++)
            {
                Binding binding = bindings[i];
                if (binding == null)
                    continue;

                binding.RebuildHashes();
                buffer[i] = binding.ToEntry();
            }

            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.InputFacade, H8BridgeHashes.InputFacade, count);
            return true;
        }

        private void Reset()
        {
            EnsureDefaultBindings();
        }

        private void OnValidate()
        {
            EnsureDefaultBindings();
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (binding != null)
                    binding.RebuildHashes();
            }

            if (pushOnValidateInPlayMode && Application.isPlaying)
                SyncToVault(GlobalRegistry.DataVault);
        }

        private void EnsureDefaultBindings()
        {
            if (bindings == null)
                bindings = new List<Binding>(32);

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

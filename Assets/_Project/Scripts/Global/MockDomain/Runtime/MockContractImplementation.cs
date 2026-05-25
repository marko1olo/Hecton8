using Hecton8.Global.Contracts;
using Hecton8.MockDomain.Contracts;

namespace Hecton8.MockDomain.Runtime
{
    /// <summary>
    /// Contract-only mock implementation proving Runtime can compile without UI, Physics, or Core runtime references.
    /// </summary>
    public readonly struct MockContractImplementation : IMockContractImplementation
    {
        public const uint NodeId = 0x4D4F434Bu;

        private static MockDomainState _state;

        public static MockContractImplementation Create()
        {
            return default;
        }

        public uint GetNodeId()
        {
            return NodeId;
        }

        public BootstrapPhase GetPhase()
        {
            return BootstrapPhase.Simulation;
        }

        public int GetDependencyCount()
        {
            return 0;
        }

        public bool TryGetDependencyId(int index, out uint dependencyId)
        {
            dependencyId = 0u;
            return false;
        }

        public void OnRegister(ref BootstrapRegistryContext context)
        {
            _state.Generation = context.FrameIndex;
            _state.Flags |= 1u;
        }

        public void OnDependencyInject(ref BootstrapDependencySnapshot snapshot)
        {
            _state.Generation = snapshot.Generation;
            _state.Flags |= 2u;
        }

        public void ResetStaticState()
        {
            _state = default;
        }

        public PhysicsFacade CreatePhysicsFacade(GlobalNativeBufferHandle bodyBuffer)
        {
            return new PhysicsFacade(default, bodyBuffer);
        }

        public ref readonly MockDomainState GetStateRef()
        {
            return ref _state;
        }

    }
}

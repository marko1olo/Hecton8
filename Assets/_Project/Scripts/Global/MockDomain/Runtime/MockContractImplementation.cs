using Hecton8.Global.Contracts;
using Hecton8.MockDomain.Contracts;
using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.MockDomain.Runtime
{
    /// <summary>
    /// Contract-only mock implementation proving Runtime can compile without UI, Physics, or Core runtime references.
    /// </summary>
    public readonly struct MockContractImplementation : IMockContractImplementation
    {
        public const uint NodeId = 0x4D4F434Bu;

        private static readonly Unity.Burst.FunctionPointer<PhysicsApplyForceDelegate> _applyForcePointer =
            BurstCompiler.CompileFunctionPointer<PhysicsApplyForceDelegate>(MockApplyForce);

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
            return new PhysicsFacade(_applyForcePointer, bodyBuffer);
        }

        public ref MockDomainState GetStateRef()
        {
            return ref _state;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static void MockApplyForce(GlobalNativeBufferHandle bodyBuffer, int bodyIndex, float3 force, float deltaTime)
        {
        }
    }
}

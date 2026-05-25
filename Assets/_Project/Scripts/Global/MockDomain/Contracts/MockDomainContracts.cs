using System.Runtime.InteropServices;
using Hecton8.Global.Contracts;
using Unity.Mathematics;

namespace Hecton8.MockDomain.Contracts
{
    /// <summary>
    /// Mock command id used to prove contract-only assembly routing.
    /// </summary>
    public enum MockDomainCommand : byte
    {
        None = 0,
        ApplyForce = 1,
        Reset = 2
    }

    /// <summary>
    /// Explicitly padded mock state used by dependency inversion smoke tests.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockDomainState
    {
        [FieldOffset(0)] public double3 AnchorAup;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Generation;
    }

    /// <summary>
    /// Contract facade implementation surface. Runtime implementors must not expose managed properties.
    /// </summary>
    public interface IMockContractImplementation : IBootstrapNode
    {
        PhysicsFacade CreatePhysicsFacade(GlobalNativeBufferHandle bodyBuffer);
        ref readonly MockDomainState GetStateRef();
    }
}

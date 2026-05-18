using Hecton8.Global.Contracts;
using Hecton8.MockDomain.Contracts;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.MockDomain.Authoring
{
    [CreateAssetMenu(menuName = "Hecton-8/Compile Wall/Mock Domain Profile")]
    public sealed class MockDomainAuthoringProfile : ScriptableObject
    {
        [SerializeField] private HardwareQualityRoute minQuality = HardwareQualityRoute.Low;
        [SerializeField] private HardwareQualityRoute maxQuality = HardwareQualityRoute.Ultra;
        [SerializeField, Range(0f, 1f)] private float minQualityWeight;
        [SerializeField, Range(0f, 1f)] private float maxQualityWeight = 1f;
        [SerializeField] private MockDomainCommand defaultCommand = MockDomainCommand.ApplyForce;
        [SerializeField] private double anchorAupX;
        [SerializeField] private double anchorAupY;
        [SerializeField] private double anchorAupZ;
        [SerializeField] private uint contractHash = 0x4D4F434Bu;
        [SerializeField] private uint implementationHash = 0x4D4F434Bu;
        [SerializeField] private uint mockImplementationHash = 0x4D4F434Bu;
        [SerializeField] private uint routeFlags = 1u;

        public void BuildRoutingOverride(out AssemblyRoutingOverride route)
        {
            route = default;
            route.ContractHash = contractHash;
            route.ImplementationHash = implementationHash;
            route.MockImplementationHash = mockImplementationHash;
            route.Flags = routeFlags | ((uint)defaultCommand << 16);
            route.MinQuality = minQuality;
            route.MaxQuality = maxQuality;
            route.MinQualityWeight = math.clamp(minQualityWeight, 0f, 1f);
            route.MaxQualityWeight = math.max(route.MinQualityWeight, math.clamp(maxQualityWeight, 0f, 1f));
            route.QualityCurveHash = 0x5157414Cu;
        }

        public void BuildInitialState(out MockDomainState state)
        {
            state = default;
            state.AnchorAup = new double3(anchorAupX, anchorAupY, anchorAupZ);
            state.Flags = routeFlags;
            state.Generation = 1u;
        }
    }
}

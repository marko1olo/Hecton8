#!/usr/bin/env python3
"""
HECTON-8 Agent 1333 static proof scanner.

Audits compute shader thread-group sizes and C# compute dispatch call sites.
This is intentionally conservative: unresolved dynamic expressions are reported
instead of treated as safe.
"""

from __future__ import annotations

import argparse
import ast
import hashlib
import json
import re
from pathlib import Path
from typing import Any


THREAD_LIMIT = 256
MAX_DISPATCH_GROUPS_PER_DIMENSION = 65535
GLES31_GUARANTEED_COMPUTE_BUFFER_LIMIT = 4
VENDOR_PREFIXES = (
    "Assets/Crest/",
    "Assets/GPUInstancer/",
    "Assets/Bakery/",
    "Assets/Editor/x64/Bakery/",
)
EXCLUDED_DIRS = {
    ".git",
    ".vs",
    "Library",
    "Logs",
    "Temp",
    "obj",
    "bin",
    "__pycache__",
}
AGENT_1333_DYNAMIC_DISPATCH_FILES = (
    "Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs",
    "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs",
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs",
    "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs",
    "Assets/_Project/Scripts/HectonBoidController.cs",
    "Assets/_Project/Scripts/HectonCelestialEngine.cs",
    "Assets/_Project/Scripts/HectonFluidEngine.cs",
    "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs",
    "Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs",
    "Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs",
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs",
    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs",
    "Assets/_Project/Scripts/SubmarineStructuralGrid.cs",
    "Assets/_Project/Scripts/UI/PDAMapTab.cs",
    "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs",
    "Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs",
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs",
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs",
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs",
    "Assets/_Project/Scripts/World/AbyssalThermalManager.cs",
    "Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs",
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs",
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs",
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs",
    "Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs",
    "Assets/_Project/Scripts/World/SargassumCrestDampingController.cs",
    "Assets/_Project/Scripts/World/SargassumCutManager.cs",
)
AGENT_1333_COMPUTE_SERVICE_BRIDGE_CONTRACTS = {
    "Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs": {
        "required_fragments": (
            "[DefaultExecutionOrder(-120)]",
            "GlobalRegistry.RegisterInstanceCullingService(_service)",
            "ReferenceEquals(GlobalRegistry.InstanceCulling, _service)",
        ),
        "resolver_signatures": (),
    },
    "Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs": {
        "required_fragments": (
            "CacheInstanceCullingServiceCold();",
            "culling.Dispatch(in descriptor)",
            "GlobalRegistryServiceSlot.InstanceCullingRuntime",
        ),
        "resolver_signatures": (
            "private IInstanceCullingService ResolveInstanceCullingService()",
        ),
    },
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs": {
        "required_fragments": (
            "CacheInstanceCullingServiceCold();",
            "GlobalRegistryServiceSlot.InstanceCullingRuntime",
            "TryGetCulledFloraVisibleBuffer",
        ),
        "resolver_signatures": (),
    },
}
AGENT_1333_VENDOR_COMPUTE_INTEGRATION_CONTRACTS = {
    "Assets/_Project/Scripts/Plugins/Crest/Crest4KinematicsAdapter.cs": {
        "required_fragments": (
            "HardwareTierDetector.AllowHighResourceComputeShaders",
            "DisableUnsupportedHighResourceCrestCompute()",
            "global::Crest.ShapeFFT",
            "shapeFft.enabled = false",
        ),
        "guarded_methods": (
            "private void Awake()",
            "private void OnEnable()",
            "private void DisableUnsupportedHighResourceCrestCompute()",
        ),
    },
    "Assets/_Project/Scripts/HectonRockManager.cs": {
        "required_fragments": (
            "HardwareTierDetector.AllowHighResourceComputeShaders",
            "CanUseVendorGpuInstancerCompute()",
            "ApplyVendorGpuiManagerAdmission()",
            "gpuiManager.enabled = false",
            "allowVendorGpuiCompute",
            "GPUInstancerAPI.InitializePrototype",
            "GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array",
        ),
        "guarded_methods": (
            "public void SlowTick()",
        ),
    },
    "Assets/_Project/Scripts/WorldProceduralScatterDirector.cs": {
        "required_fragments": (
            "HardwareTierDetector.AllowHighResourceComputeShaders",
            "ApplyVendorGpuiManagerAdmission()",
            "floraGpuiManager.enabled = false",
            "ScatterInstancingService",
        ),
        "guarded_methods": (
            "private void ResolveReferences()",
            "private void ApplyVendorGpuiManagerAdmission()",
        ),
    },
    "Assets/_Project/Scripts/World/ScatterInstancingService.cs": {
        "required_fragments": (
            "HardwareTierDetector.AllowHighResourceComputeShaders",
            "CanUseVendorGpuInstancerCompute()",
            "GPUInstancerAPI.InitializePrototype",
            "GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array",
        ),
        "guarded_methods": (
            "public void FlushBuffers(",
            "public void ClearVisibility(",
            "private static bool ShouldUseFloraGpuiPath(",
        ),
    },
}
VENDOR_COMPONENT_MANAGER_CLASSIFIERS = (
    "GPUInstancer::GPUInstancer.GPUInstancerPrefabManager",
    "GPUInstancer::GPUInstancer.GPUInstancerDetailManager",
    "GPUInstancer::GPUInstancer.GPUInstancerTreeManager",
    "GPUInstancer::GPUInstancer.GPUInstancerManager",
)
VENDOR_PREFAB_MARKER_CLASSIFIER = "GPUInstancer::GPUInstancer.GPUInstancerPrefab"
CREST_SHAPE_FFT_CLASSIFIER = "Crest::Crest.ShapeFFT"
CREST_ADAPTER_CLASSIFIER = "Hecton8.Core::Hecton8.Physics.Crest4KinematicsAdapter"
AGENT_1333_GPU_WRITTEN_BUFFER_FIELDS = {
    "Assets/_Project/Scripts/HectonFluidEngine.cs": {
        "_advectedSiltBufferA",
        "_advectedSiltBufferB",
        "_advectedBubbleBufferA",
        "_advectedBubbleBufferB",
        "_advectedDebrisBufferA",
        "_advectedDebrisBufferB",
        "_emptyAdvectedSiltBuffer",
        "_emptyAdvectedBubbleBuffer",
        "_emptyAdvectedDebrisBuffer",
        "_emptyAbyssalFlowBuffer",
        "_gpuBuoyancyResultBuffers",
        "_gpuAbyssalFlowResultBuffer",
    },
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs": {
        "_particleBufferA",
        "_particleBufferB",
        "_particleMetaBufferA",
        "_particleMetaBufferB",
        "_visibleParticleIndexBuffer",
    },
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs": {
        "_requestBuffer0",
        "_requestBuffer1",
        "_requestBuffer2",
    },
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs": {
        "_positionBufferA",
        "_positionBufferB",
        "_velocityBufferA",
        "_velocityBufferB",
        "_visibleIndicesBuffer",
        "_indirectArgsBuffer",
    },
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs": {
        "_particleBufferA",
        "_particleBufferB",
        "_visibleIndicesBuffer",
        "_indirectArgsBuffer",
    },
    "Assets/_Project/Scripts/HectonBoidController.cs": {
        "_boidsBufferA",
        "_boidsBufferB",
        "_spatialGridCountBuffer",
        "_spatialGridCellBuffer",
        "_visibleBoidIndexBuffer",
        "_visibleIndirectArgsBuffer",
    },
    "Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs": {
        "_boidsBufferA",
        "_boidsBufferB",
        "_latchStatsBuffer",
        "_pbdCorrectionBuffer",
        "_spatialGridCountBuffer",
        "_spatialGridCellBuffer",
    },
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs": {
        "_instanceBuffer",
        "_visibleIndicesBuffer",
        "_visibilityCacheBuffer",
        "_scatterDensityBuffer",
        "_argsBuffer",
    },
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs": {
        "_visibleIndicesLod0Buffer",
        "_visibleIndicesLod1Buffer",
        "_visibleIndicesShadowBuffer",
        "_indirectArgsLod0Buffer",
        "_indirectArgsLod1Buffer",
        "_indirectArgsShadowBuffer",
        "_cullTelemetryCountersBuffer",
        "_floraSnapFlagBuffer",
    },
    "Assets/_Project/Scripts/World/AbyssalThermalManager.cs": {
        "_particleBufferA",
        "_particleBufferB",
    },
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs": {
        "s_DroneVisibleMatrixBuffer",
        "s_DroneVisibleInstanceBuffer",
        "s_DroneVisibleIndexBuffer",
        "s_DroneProceduralArgsBuffer",
        "s_PhantomDroneMatrixBuffer",
        "s_PhantomDroneColorBuffer",
    },
}
AGENT_1333_PAYLOAD_OWNER_QUERY_CONTRACTS = {
    "Assets/_Project/Scripts/HectonFluidEngine.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "compute.IsSupported(kernel)",
        "sizeY != 1u",
        "totalThreads > PortableMaxComputeThreadsPerGroup",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs": (
        "TryResolveKernelThreadGroupSize2D(_calculateKernel",
        "TryResolveKernelThreadGroupSize2D(_advectKernel",
        "TryResolveKernelThreadGroupSize2D(_clearKernel",
        "PortableMaxThreadsPerThreadGroup",
    ),
}
AGENT_1333_STRICT_THREAD_QUERY_CONTRACTS = {
    "Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "ClearArgsThreadGroupSizeX",
        "CeilDividePositive(1, cullingParams.ClearArgsThreadGroupSizeX)",
    ),
    "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "ResolveDispatchGroups(count, _waveSamplerThreadGroupSize)",
    ),
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "s_DroneCullThreadGroupSizeX = ResolveKernelThreadGroupSizeX(s_DroneCullingCompute, s_DroneCullKernel)",
        "int cullDispatchGroups = CeilDividePositive(HeadlessDroneCapacity, s_DroneCullThreadGroupSizeX)",
        "cullDispatchGroups <= 0",
        "phantomDispatchGroups <= 0",
        "s_PhantomDroneKernelResolved = s_PhantomDroneThreadGroupSizeX > 0;",
    ),
    "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "queryZ != 1u",
        "value <= 0 || divisor <= 0",
        "groupCountX <= 0 || groupCountY <= 0",
    ),
    "Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs": (
        "SystemInfo.supportsComputeShaders",
        "PortableMaxComputeThreadsPerGroup",
        "_threadGroupSize > 0",
        "groupY == 1u",
        "groupZ == 1u",
        "dispatchGroups <= 0",
    ),
    "Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs": (
        "SystemInfo.supportsComputeShaders",
        "PortableMaxComputeThreadsPerGroup",
        "shader.IsSupported(kernel)",
        "groupZ > ActiveMaxThreadsPerGroup / xyThreads",
        "value <= 0 || divisor <= 0",
        "groups > MaxDispatchGroupsPerDimension",
        "ResolveDispatchGroups(workItemsX, groupX)",
    ),
    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs": (
        "SystemInfo.supportsComputeShaders",
        "PortableMaxThreadsPerThreadGroup",
        "groupY != 1u",
        "groupZ != 1u",
        "_dispatchThreadGroupSizeX = 0",
        "ResolveDispatchGroups(activeCount, _dispatchThreadGroupSizeX)",
    ),
    "Assets/_Project/Scripts/HectonCelestialEngine.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "xyThreads > PortableMaxComputeThreadsPerGroup",
        "queryZ > PortableMaxComputeThreadsPerGroup / xyThreads",
        "value <= 0 || divisor <= 0",
        "clearGroupsX <= 0 || clearGroupsY <= 0 || starGroupsX <= 0",
        "atmosphereGroupsX <= 0 || atmosphereGroupsY <= 0",
    ),
    "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeZ != 1u",
        "value <= 0 || threadGroupSize <= 0",
        "ResolveDispatchGroups(FlashlightPhotophobiaFieldResolution",
        "ResolveDispatchGroups(1, _hudFogLuminanceThreadGroupSizeX)",
        "TryResolveKernelThreadGroupSize2D(",
    ),
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "ResolveDispatchGroups(_dispatchRequestCount, _threadGroupSize)",
    ),
    "Assets/_Project/Scripts/SubmarineStructuralGrid.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/UI/PDAMapTab.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "groups > MaxDispatchGroupsPerDimension",
        "ResolveDispatchGroups(1, _sonarClearArgsThreadGroupSizeX)",
        "ResolveDispatchGroups(dispatchWordCount, _sonarBuildMapPointsThreadGroupSizeX)",
    ),
    "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "z != 1u",
        "value <= 0 || divisor <= 0",
        "groups > MaxDispatchGroupsPerDimension",
        "ResolveDispatchGroups(resolution, _threadsX)",
        "dirtyCount > MaxDispatchGroupsPerDimension",
    ),
    "Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "compute.IsSupported(kernel)",
        "radarCompute.IsSupported(_radarKernel)",
        "damageHologramCompute.IsSupported(_damageHologramKernel)",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "threadGroupSizeX <= 0",
        "_clearArgsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _clearArgsKernel",
        "_clearArgsThreadGroupSizeX <= 0",
        "ResolveDispatchGroups(1, _clearArgsThreadGroupSizeX)",
        "ResetComputeKernelState();",
    ),
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs": (
        "ThreadGroupPortableMaxSize",
        "y != 1u",
        "z != 1u",
        "count <= 0 || groupSize <= 0",
        "TryResolveKernelThreadGroupSizeX(_clearArgsKernel, out _clearArgsThreadGroupSize)",
        "ResolveDispatchGroups(1, _clearArgsThreadGroupSize)",
    ),
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "threadGroupSize <= 0",
        "TryResolveKernelThreadGroupSizeX(_clearVisibleKernel, out _clearVisibleThreadGroupSize)",
        "CeilDivide(1, _clearVisibleThreadGroupSize)",
    ),
    "Assets/_Project/Scripts/World/AbyssalThermalManager.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "sizeY != 1u",
        "sizeZ != 1u",
        "value <= 0 || divisor <= 0",
        "ResolveDispatchGroups(smokeParticleCount, _threadGroupSizeX)",
    ),
    "Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs": (
        "MaxPortableComputeThreadsPerGroup",
        "sizeX == 0u || sizeY == 0u || sizeZ == 0u",
        "resolution <= 0 || threadGroupSize == 0u",
        "groups > MaxDispatchGroupsPerDimension",
        "TryResolveKernel(",
    ),
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "queryZ != 1u",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "queryY != 1u",
        "queryZ != 1u",
        "value <= 0 || divisor <= 0",
        "copyGroupsX <= 0 || copyGroupsY <= 0",
    ),
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "queryY != 1u",
        "queryZ != 1u",
        "value <= 0 || divisor <= 0",
        "copyGroupsX <= 0 || copyGroupsY <= 0",
    ),
    "Assets/_Project/Scripts/World/SargassumCrestDampingController.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "queryZ != 1u",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/World/SargassumCutManager.cs": (
        "PortableMaxComputeThreadsPerGroup",
        "xyThreads > PortableMaxComputeThreadsPerGroup",
        "queryZ > PortableMaxComputeThreadsPerGroup / xyThreads",
        "value <= 0 || divisor <= 0",
    ),
    "Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs": (
        "PortableThreadGroupMaxSize",
        "groupSizeY != 1u",
        "groupSizeZ != 1u",
        "CeilDivPositive(1, (int)_clearStatsThreadGroupSizeX)",
        "PortableMaxDispatchGroupsPerDimension",
    ),
}
AGENT_1333_COMPUTE_SUPPORT_GATE_CONTRACTS = {
    path: ("SystemInfo.supportsComputeShaders",)
    for path in AGENT_1333_DYNAMIC_DISPATCH_FILES
}
AGENT_1333_COMPUTE_SUPPORT_GATE_CONTRACTS.update(
    {
        "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs": ("SystemInfo.supportsComputeShaders",),
        "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs": ("SystemInfo.supportsComputeShaders",),
    }
)
AGENT_1333_HIGH_RESOURCE_COMPUTE_API_GUARD_CONTRACTS = {
    "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
        "s_PhantomDronesCompute == null || !HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/HectonBoidController.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/HectonFluidEngine.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/UI/PDAMapTab.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/World/SargassumCrestDampingController.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
    "Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs": (
        "HardwareTierDetector.AllowHighResourceComputeShaders",
    ),
}
AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_PATH = (
    "Assets/_Project/Scripts/Core/HardwareTierDetector.cs"
)
AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_FRAGMENTS = (
    "SystemInfo.supportsComputeShaders",
    "_isSharedMemoryArchitecture",
    "!_isSharedMemoryArchitecture",
    "Application.isMobilePlatform",
    "_isQuest3Like",
    "_isSteamDeckLike",
    "!(Application.isMobilePlatform && (_isVulkan || _isMetal))",
    "!(_isVulkan && (_isQuest3Like || _isSteamDeckLike))",
    "_isLegacyDirect3D11",
    "_isDirect3D12",
    "_isVulkan",
    "_isMetal",
)
AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_FORBIDDEN = (
    "GraphicsDeviceType.OpenGLES3",
    "GraphicsDeviceType.OpenGLCore",
)
AGENT_1333_COMPUTE_TEXTURE_RANDOM_WRITE_CONTRACTS = {
    "Assets/_Project/Scripts/HectonCelestialEngine.cs": (
        "_ID_BakedStarCubemap",
        "_ID_HectonAtmosphereScatteringLut",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/HectonFluidEngine.cs": (
        "_AbyssalFlowTextureWriteId",
        "_AbyssalFlowTextureRWId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs": (
        "_HectonHudFogLuminanceOutputId",
        "_HectonPhotophobiaTargetTexId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs": (
        "AtlasAlbedoDepthId",
        "AtlasNormalXYId",
        "OutputAtlasId",
        "CreateAtlasTexture",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs": (
        "EdgeMaskWriteId",
        "EdgeMaskArrayWriteId",
        "UpscaledColorId",
        "UpscaledColorArrayId",
        "edgeDesc.enableRandomWrite = true",
        "outputDesc.enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs": (
        "WakeTextureWriteId",
        "wakeDesc.enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs": (
        "ShaderConstants.GatherId",
        "ShaderConstants.ResultId",
        "\"_HectonBiolumSSGIGather\"",
        "\"_HectonBiolumSSGITexture\"",
        "true,",
    ),
    "Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs": (
        "DiegeticLensMaskWriteId",
        "maskDesc.enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs": (
        "ShaderConstants.HalfResultId",
        "ShaderConstants.VolumeWriteId",
        "desc.enableRandomWrite = enableRandomWrite",
        "\"_HectonVolumetricFogHalf\"",
        "\"_HectonVolumetricFogFrustumGrid\"",
    ),
    "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs": (
        "ShaderConstants.ResultId",
        "aoDesc.name = \"_HectonVoxelSSAOTexture\"",
        "aoDesc.enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs": (
        "ShaderConstants.HalfResultId",
        "ShaderConstants.CompositeResultId",
        "\"_HectonVolumetricLightHalf\"",
        "\"_HectonVolumetricLightComposite\"",
        "true,",
    ),
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs": (
        "ShaderIds.SonarGlowResultId",
        "ShaderIds.FogDensityResultId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs": (
        "ShaderConstants.GenerationTextureId",
        "ShaderConstants.OutputTextureId",
        "desc.enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs": (
        "enableRandomWrite: true",
    ),
    "Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs": (
        "_VolumeOutputId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs": (
        "_WakeTrailResultId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs": (
        "_DepthPyramidTargetId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs": (
        "_DepthPyramidTargetId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/World/SargassumCrestDampingController.cs": (
        "_WaveDampingMaskResultId",
        "_OilFilmMaskResultId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/World/SargassumCutManager.cs": (
        "_ResultId",
        "_DamageVolumeResultId",
        "enableRandomWrite = true",
    ),
    "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs": (
        "TerminalTextureArrayId",
        "enableRandomWrite = true",
    ),
}
AGENT_1333_FIND_KERNEL_FAIL_CLOSED_CONTRACTS = {
    path: (".HasKernel(",)
    for path in AGENT_1333_DYNAMIC_DISPATCH_FILES
}
AGENT_1333_SUPPORTED_KERNEL_RESOLVE_CONTRACTS = {
    "Assets/_Project/Scripts/HectonFluidEngine.cs": (
        "return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs": (
        "return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs": (
        "!s_PhantomDronesCompute.IsSupported(s_PhantomDroneKernel)",
        "s_PhantomDroneThreadGroupSizeX = 0;",
        "s_PhantomDroneKernelResolved = s_PhantomDroneThreadGroupSizeX > 0;",
    ),
    "Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs": (
        "return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs": (
        "return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs": (
        "TryFindSupportedKernel",
        "return kernel >= 0 && _computeShader.IsSupported(kernel) ? kernel : -1;",
        "_calculateThreadGroupSizeX = 0;",
        "_clearThreadGroupSizeY = 0;",
    ),
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs": (
        "return kernel >= 0 && shader.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs": (
        "return kernel >= 0 && computeShader.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs": (
        "return kernel >= 0 && computeShader.IsSupported(kernel) ? kernel : -1;",
    ),
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs": (
        "return kernel >= 0 && computeShader.IsSupported(kernel) ? kernel : -1;",
    ),
}
AGENT_1333_DISPATCH_GROUP_LIMIT_CONTRACTS = {
    path: ("MaxDispatchGroupsPerDimension",)
    for path in AGENT_1333_DYNAMIC_DISPATCH_FILES
    if path != "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs"
}
AGENT_1333_DISPATCH_GROUP_LIMIT_CONTRACTS.update(
    {
        "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveThreadGroups",
            "ResolveDispatchDepth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveThreadGroupSizes",
            "ResolveDispatchDepth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveKernel",
            "ResolveDispatchGroups(giWidth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryValidateKernelThreadGroups",
            "ResolveDispatchGroups(1, _clearHistogramThreadGroupSizeX)",
            "ResolveDispatchGroups(1, _resolveExposureThreadGroupSizeX)",
            "ResolveDispatchGroups(sourceWidth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "ResolveDispatchGroups(maskWidth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveKernelThreadGroups",
            "ResolveDispatchGroups(volumeWidth",
            "ResolveDispatchGroups(activeViewCount",
        ),
        "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveKernel",
            "ResolveDispatchGroups(aoWidth",
            "threadProduct > MaxKernelThreadProduct",
        ),
        "Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs": (
            "MaxDispatchGroupsPerDimension",
            "TryResolveKernel",
            "ResolveDispatchGroups(halfWidth",
            "threadProduct > MaxKernelThreadProduct",
            "ResetComputeKernelState();",
        ),
        "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs": (
            "JacobianFoamContracts.ResolveDispatchGroups",
            "ClearPreparedPayload();",
        ),
        "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs": (
            "MaxDispatchGroupsPerDimension",
            "resolution <= 0 || threadGroupSize <= 0",
            "groups <= MaxDispatchGroupsPerDimension",
        ),
        "Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs": (
            "MaxDispatchGroupsPerDimension",
            "particleCount <= 0 || threadGroupSize <= 0",
            "groups <= MaxDispatchGroupsPerDimension",
        ),
    }
)
COMPUTE_RESOURCE_DECLARATION_PATTERN = re.compile(
    r"^\s*(?:globallycoherent\s+)?(?:(?:RW)?(?:StructuredBuffer|ByteAddressBuffer|AppendStructuredBuffer|ConsumeStructuredBuffer|RWByteAddressBuffer|RWStructuredBuffer)\s*(?:<[^>]+>)?|(?:RW)?Texture\dD(?:Array)?\s*(?:<[^>]+>)?|SamplerState|SamplerComparisonState)\s+([A-Za-z_][A-Za-z0-9_]*)\b"
)
COMPUTE_RESOURCE_MACRO_PATTERN = re.compile(
    r"^\s*(?:RW_)?TEXTURE(?:1D|2D|3D|CUBE)(?:_X|_ARRAY|_X_ARRAY)?\s*\(\s*(?:(?:[A-Za-z_][A-Za-z0-9_<>,\s]*)\s*,\s*)?([A-Za-z_][A-Za-z0-9_]*)\s*\)"
)
_TEXT_CACHE: dict[Path, str] = {}
_SHA256_CACHE: dict[Path, str] = {}


def read_text(path: Path) -> str:
    cached = _TEXT_CACHE.get(path)
    if cached is not None:
        return cached

    try:
        text = path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        text = path.read_text(encoding="utf-8", errors="replace")
    _TEXT_CACHE[path] = text
    return text


def file_sha256(path: Path) -> str:
    cached = _SHA256_CACHE.get(path)
    if cached is not None:
        return cached

    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    value = digest.hexdigest()
    _SHA256_CACHE[path] = value
    return value


def repo_path(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def is_vendor(relative_path: str) -> bool:
    return relative_path.startswith(VENDOR_PREFIXES)


def iter_files(root: Path, suffix: str) -> list[Path]:
    result: list[Path] = []
    for path in root.rglob(f"*{suffix}"):
        if any(part in EXCLUDED_DIRS for part in path.parts):
            continue
        result.append(path)
    return sorted(result)


def safe_eval_int(expression: str, symbols: dict[str, int]) -> int | None:
    cleaned = expression.strip()
    cleaned = re.sub(r"(?<=\d)[uUlL]+", "", cleaned)
    if not cleaned:
        return None

    def visit(node: ast.AST) -> int:
        if isinstance(node, ast.Expression):
            return visit(node.body)
        if isinstance(node, ast.Constant) and isinstance(node.value, int):
            return int(node.value)
        if isinstance(node, ast.Name):
            if node.id not in symbols:
                raise ValueError(node.id)
            return int(symbols[node.id])
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub):
            return -visit(node.operand)
        if isinstance(node, ast.BinOp):
            left = visit(node.left)
            right = visit(node.right)
            if isinstance(node.op, ast.Add):
                return left + right
            if isinstance(node.op, ast.Sub):
                return left - right
            if isinstance(node.op, ast.Mult):
                return left * right
            if isinstance(node.op, ast.FloorDiv):
                return left // right
            if isinstance(node.op, ast.Div):
                if right == 0 or left % right != 0:
                    raise ValueError(cleaned)
                return left // right
            if isinstance(node.op, ast.LShift):
                return left << right
            if isinstance(node.op, ast.RShift):
                return left >> right
        raise ValueError(cleaned)

    try:
        parsed = ast.parse(cleaned, mode="eval")
        value = visit(parsed)
    except (SyntaxError, ValueError, ZeroDivisionError):
        return None
    return value if value >= 0 else None


def parse_define(line: str) -> tuple[str, int] | None:
    match = re.match(r"\s*#define\s+([A-Za-z_][A-Za-z0-9_]*)\s+([0-9A-Za-z_()+\-*/<>\s]+)", line)
    if not match:
        return None
    name = match.group(1)
    raw_value = match.group(2).split("//", 1)[0].strip()
    value = safe_eval_int(raw_value, {})
    if value is None:
        return None
    return name, value


def resolve_include_path(root: Path, current_file: Path, include_path: str) -> Path | None:
    candidates = [
        current_file.parent / include_path,
        root / include_path,
        root / "Assets" / include_path,
    ]
    for candidate in candidates:
        if candidate.exists() and candidate.is_file():
            return candidate.resolve()
    return None


def collect_included_defines(root: Path, path: Path, visited: set[Path] | None = None) -> dict[str, int]:
    visited = visited or set()
    resolved = path.resolve()
    if resolved in visited:
        return {}
    visited.add(resolved)

    defines: dict[str, int] = {}
    text = read_text(path)
    for line in text.splitlines():
        include_match = re.match(r'\s*#include\s+"([^"]+)"', line)
        if include_match:
            include_file = resolve_include_path(root, path, include_match.group(1))
            if include_file is not None:
                defines.update(collect_included_defines(root, include_file, visited))
            continue

        define = parse_define(line)
        if define:
            defines[define[0]] = define[1]
    return defines


def parse_pragma(line: str) -> tuple[str, dict[str, int]] | None:
    stripped = line.strip()
    if not stripped.startswith("#pragma kernel "):
        return None
    parts = stripped.split()
    if len(parts) < 3:
        return None
    kernel = parts[2]
    symbols: dict[str, int] = {}
    for token in parts[3:]:
        if "=" not in token:
            continue
        key, raw_value = token.split("=", 1)
        value = safe_eval_int(raw_value, symbols)
        if value is not None:
            symbols[key] = value
    return kernel, symbols


def split_args(raw: str) -> list[str]:
    args: list[str] = []
    depth = 0
    start = 0
    for index, char in enumerate(raw):
        if char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
        elif char == "," and depth == 0:
            args.append(raw[start:index].strip())
            start = index + 1
    args.append(raw[start:].strip())
    return args


def analyze_compute_file(root: Path, path: Path) -> dict[str, Any]:
    text = read_text(path)
    relative = repo_path(root, path)
    defines: dict[str, int] = collect_included_defines(root, path)
    pragma_symbols: dict[str, dict[str, int]] = {}
    lines = text.splitlines()
    resource_declarations: list[dict[str, Any]] = []
    for index, line in enumerate(lines):
        resource_match = COMPUTE_RESOURCE_DECLARATION_PATTERN.match(line)
        macro_resource_match = COMPUTE_RESOURCE_MACRO_PATTERN.match(line)
        if resource_match or macro_resource_match:
            resource_declarations.append(
                {
                    "line": index + 1,
                    "name": (resource_match or macro_resource_match).group(1),
                    "declaration": normalize_snippet(line, 180),
                }
            )
        define = parse_define(line)
        if define:
            defines[define[0]] = define[1]
            continue
        pragma = parse_pragma(line)
        if pragma:
            pragma_symbols[pragma[0]] = pragma[1]

    declarations: list[dict[str, Any]] = []
    max_product = 0
    unresolved_count = 0
    for index, line in enumerate(lines):
        match = re.search(r"\[numthreads\(([^)]*)\)\]", line)
        if not match:
            continue
        raw_args = split_args(match.group(1))
        kernel = "UNKNOWN"
        for next_line in lines[index + 1 : min(index + 8, len(lines))]:
            function_match = re.search(r"\b(?:void|[A-Za-z_][A-Za-z0-9_<>,\s]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", next_line)
            if function_match:
                kernel = function_match.group(1)
                break
        symbols = dict(defines)
        symbols.update(pragma_symbols.get(kernel, {}))
        resolved = [safe_eval_int(arg, symbols) for arg in raw_args]
        product = None
        if len(resolved) == 3 and all(value is not None for value in resolved):
            product = int(resolved[0]) * int(resolved[1]) * int(resolved[2])
            max_product = max(max_product, product)
        else:
            unresolved_count += 1
        declarations.append(
            {
                "line": index + 1,
                "kernel": kernel,
                "raw": raw_args,
                "resolved": resolved,
                "product": product,
                "over_limit": product is not None and product > THREAD_LIMIT,
            }
        )

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "declarations": declarations,
        "max_product": max_product,
        "unresolved_numthreads": unresolved_count,
        "groupshared_count": len(re.findall(r"\bgroupshared\b", text)),
        "barrier_count": len(re.findall(r"\b(?:GroupMemoryBarrier|DeviceMemoryBarrier)(?:WithGroupSync)?\s*\(", text)),
        "interlocked_count": len(re.findall(r"\bInterlocked[A-Za-z0-9_]*\s*\(", text)),
        "resource_count": len(resource_declarations),
        "resource_declarations": resource_declarations,
    }


def collect_dispatch_expression(lines: list[str], start_index: int) -> str:
    expression = lines[start_index].strip()
    balance = expression.count("(") - expression.count(")")
    index = start_index + 1
    while balance > 0 and index < len(lines) and index <= start_index + 12:
        part = lines[index].strip()
        expression += " " + part
        balance += part.count("(") - part.count(")")
        index += 1
    return expression


def is_compute_dispatch_expression(expression: str) -> bool:
    if ".DispatchCompute(" in expression:
        return True
    match = re.search(r"([A-Za-z_][A-Za-z0-9_\.]*)\.Dispatch\s*\(", expression)
    if not match:
        return False
    receiver = match.group(1).lower()
    return "compute" in receiver or "shader" in receiver


def uses_external_payload_sizing(expression: str) -> bool:
    return bool(re.search(r"\b[A-Za-z_][A-Za-z0-9_\.]*\.[A-Za-z0-9_]*DispatchGroups[A-Za-z0-9_]*\b", expression))


def normalize_snippet(text: str, limit: int = 220) -> str:
    return " ".join(text.split())[:limit]


def strip_csharp_comments_and_strings(text: str) -> str:
    result: list[str] = []
    index = 0
    length = len(text)
    while index < length:
        char = text[index]
        next_char = text[index + 1] if index + 1 < length else ""
        if char == "/" and next_char == "/":
            while index < length and text[index] != "\n":
                result.append(" ")
                index += 1
            continue
        if char == "/" and next_char == "*":
            result.extend("  ")
            index += 2
            while index + 1 < length and not (text[index] == "*" and text[index + 1] == "/"):
                result.append("\n" if text[index] == "\n" else " ")
                index += 1
            if index + 1 < length:
                result.extend("  ")
                index += 2
            continue
        if char == '"':
            result.append(" ")
            index += 1
            while index < length:
                if text[index] == "\\":
                    result.append(" ")
                    index += 1
                    if index < length:
                        result.append(" ")
                        index += 1
                    continue
                if text[index] == '"':
                    result.append(" ")
                    index += 1
                    break
                result.append("\n" if text[index] == "\n" else " ")
                index += 1
            continue
        result.append(char)
        index += 1
    return "".join(result)


def analyze_legacy_compute_buffer_contract(root: Path, path: Path) -> list[dict[str, Any]]:
    text = read_text(path)
    if "ComputeBuffer" not in text:
        return []

    stripped = strip_csharp_comments_and_strings(text)
    relative = repo_path(root, path)
    owner = "vendor" if is_vendor(relative) else "first_party"
    lines = text.splitlines()
    violations: list[dict[str, Any]] = []
    for match in re.finditer(r"\bnew\s+ComputeBuffer\s*\(", stripped):
        line = stripped[: match.start()].count("\n") + 1
        source_line = lines[line - 1].strip() if line <= len(lines) else ""
        violations.append(
            {
                "path": relative,
                "owner": owner,
                "line": line,
                "type": "legacy_compute_buffer_allocation",
                "snippet": normalize_snippet(source_line),
            }
        )
    return violations


def analyze_graphics_buffer_lock_contract(root: Path, path: Path) -> list[dict[str, Any]]:
    text = read_text(path)
    if "GraphicsBuffer" not in text and "CopyCount" not in text:
        return []

    relative = repo_path(root, path)
    owner = "vendor" if is_vendor(relative) else "first_party"
    buffer_field = r"(?:_|s_)[A-Za-z][A-Za-z0-9_]*"
    allocation_pattern = re.compile(
        rf"(?P<field>{buffer_field})\s*(?:\?\?=|=)\s*new\s+GraphicsBuffer\s*\((?P<body>.*?)\);",
        re.DOTALL,
    )
    utility_allocation_pattern = re.compile(
        rf"(?P<field>{buffer_field})(?:\s*\[[^\]]+\])?\s*(?:\?\?=|=)\s*GraphicsBufferUploadUtility\.CreateStructured(?P<lock>Lock)?Buffer\s*<(?P<type>[^>]+)>\s*\((?P<body>.*?)\);",
        re.DOTALL,
    )
    ref_helper_allocation_pattern = re.compile(
        rf"(?P<helper>Ensure(?:GpuWrite)?Buffer)\s*<(?P<type>[^>]+)>\s*\(\s*ref\s+(?P<field>{buffer_field})(?:\s*\[[^\]]+\])?",
    )
    non_generic_ref_helper_allocation_pattern = re.compile(
        rf"(?P<helper>Ensure(?:GpuWrite)?(?:Raw)?Buffer|EnsureIndirectArgsBuffer)\s*\(\s*ref\s+(?P<field>{buffer_field})(?:\s*\[[^\]]+\])?",
    )
    local_helper_pattern = re.compile(
        r"private\s+static\s+GraphicsBuffer\s+(?P<helper>[A-Za-z][A-Za-z0-9_]*Buffer)\s*<[^>]+>\s*\([^)]*\)\s*"
        r"(?:where\s+[^{}]+)?\{(?P<body>.*?)\n\s*\}",
        re.DOTALL,
    )
    non_generic_ref_helper_declaration_pattern = re.compile(
        r"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:void|bool)\s+"
        r"(?P<helper>Ensure[A-Za-z0-9_]*Buffer)\s*\([^)]*ref\s+GraphicsBuffer\s+[^)]*\)",
    )
    local_helper_allocation_pattern = re.compile(
        rf"(?P<field>{buffer_field})\s*(?:\?\?=|=)\s*(?P<helper>[A-Za-z][A-Za-z0-9_]*Buffer)\s*<(?P<type>[^>]+)>\s*\((?P<body>.*?)\);",
        re.DOTALL,
    )
    lock_pattern = re.compile(rf"(?P<field>{buffer_field})\.LockBufferForWrite\s*<")
    lock_upload_pattern = re.compile(
        rf"GraphicsBufferUploadUtility\.Upload(?:NativeArray|Array)\s*\(\s*(?P<field>{buffer_field})\s*,"
    )
    copy_count_pattern = re.compile(
        rf"GraphicsBuffer\.CopyCount\s*\([^,]+,\s*(?P<field>{buffer_field})\s*,"
    )

    allocations: dict[str, dict[str, Any]] = {}
    local_helper_lock_usage = {
        match.group("helper"): "UsageFlags.LockBufferForWrite" in match.group("body")
        for match in local_helper_pattern.finditer(text)
    }
    non_generic_ref_helper_lock_usage: dict[str, bool] = {}
    next_method_pattern = re.compile(
        r"\n\s*(?:private|internal|public|protected)\s+(?:static\s+)?(?:void|bool|int|uint|float|GraphicsBuffer)\s+[A-Za-z_][A-Za-z0-9_]*\s*\("
    )
    for match in non_generic_ref_helper_declaration_pattern.finditer(text):
        body_slice = text[match.end(): match.end() + 2000]
        next_method = next_method_pattern.search(body_slice)
        if next_method:
            body_slice = body_slice[: next_method.start()]
        non_generic_ref_helper_lock_usage[match.group("helper")] = "UsageFlags.LockBufferForWrite" in body_slice

    for match in allocation_pattern.finditer(text):
        body = match.group("body")
        allocations[match.group("field")] = {
            "line": text[: match.start()].count("\n") + 1,
            "has_lock_usage": "UsageFlags.LockBufferForWrite" in body,
            "is_append": "Target.Append" in body,
            "raw": normalize_snippet(body),
        }

    for match in utility_allocation_pattern.finditer(text):
        body = match.group("body")
        allocation_type = match.group("type")
        allocations[match.group("field")] = {
            "line": text[: match.start()].count("\n") + 1,
            "has_lock_usage": match.group("lock") is not None,
            "is_append": False,
            "raw": normalize_snippet(f"GraphicsBufferUploadUtility.CreateStructured{match.group('lock') or ''}Buffer<{allocation_type}>({body})"),
        }

    for match in ref_helper_allocation_pattern.finditer(text):
        helper = match.group("helper")
        allocations[match.group("field")] = {
            "line": text[: match.start()].count("\n") + 1,
            "has_lock_usage": helper == "EnsureBuffer",
            "is_append": False,
            "raw": normalize_snippet(f"{helper}<{match.group('type')}>(ref {match.group('field')})"),
        }

    for match in non_generic_ref_helper_allocation_pattern.finditer(text):
        helper = match.group("helper")
        helper_has_lock_usage = non_generic_ref_helper_lock_usage.get(helper, "GpuWrite" not in helper)
        allocations[match.group("field")] = {
            "line": text[: match.start()].count("\n") + 1,
            "has_lock_usage": helper_has_lock_usage,
            "is_append": False,
            "raw": normalize_snippet(f"{helper}(ref {match.group('field')})"),
        }

    for match in local_helper_allocation_pattern.finditer(text):
        helper = match.group("helper")
        if helper not in local_helper_lock_usage:
            continue

        body = match.group("body")
        allocations[match.group("field")] = {
            "line": text[: match.start()].count("\n") + 1,
            "has_lock_usage": local_helper_lock_usage[helper],
            "is_append": False,
            "raw": normalize_snippet(f"{helper}<{match.group('type')}>({body})"),
        }

    violations: list[dict[str, Any]] = []
    for match in lock_pattern.finditer(text):
        field = match.group("field")
        allocation = allocations.get(field)
        if allocation is not None and not allocation["has_lock_usage"]:
            violations.append(
                {
                    "path": relative,
                    "owner": owner,
                    "type": "lock_buffer_for_write_without_usage_flag",
                    "field": field,
                    "line": text[: match.start()].count("\n") + 1,
                    "allocationLine": allocation["line"],
                    "allocation": allocation["raw"],
                }
            )

    gpu_written_fields = AGENT_1333_GPU_WRITTEN_BUFFER_FIELDS.get(relative, set())
    for match in lock_pattern.finditer(text):
        field = match.group("field")
        if field not in gpu_written_fields:
            continue

        violations.append(
            {
                "path": relative,
                "owner": owner,
                "type": "gpu_write_buffer_calls_lock_buffer_for_write",
                "field": field,
                "line": text[: match.start()].count("\n") + 1,
                "allocationLine": allocations.get(field, {}).get("line", 0),
                "allocation": allocations.get(field, {}).get("raw", "scanner-visible allocation missing"),
            }
        )

    for match in lock_upload_pattern.finditer(text):
        field = match.group("field")
        if field not in gpu_written_fields:
            continue

        violations.append(
            {
                "path": relative,
                "owner": owner,
                "type": "gpu_write_buffer_uses_lock_upload_helper",
                "field": field,
                "line": text[: match.start()].count("\n") + 1,
                "allocationLine": allocations.get(field, {}).get("line", 0),
                "allocation": allocations.get(field, {}).get("raw", "scanner-visible allocation missing"),
            }
        )

    for field in gpu_written_fields:
        allocation = allocations.get(field)
        if allocation is None:
            violations.append(
                {
                    "path": relative,
                    "owner": owner,
                    "type": "gpu_write_buffer_allocation_not_found",
                    "field": field,
                    "line": 0,
                    "allocationLine": 0,
                    "allocation": "missing scanner-visible allocation",
                }
            )
            continue

        if allocation["has_lock_usage"]:
            violations.append(
                {
                    "path": relative,
                    "owner": owner,
                    "type": "gpu_write_buffer_uses_lock_buffer_for_write",
                    "field": field,
                    "line": allocation["line"],
                    "allocationLine": allocation["line"],
                    "allocation": allocation["raw"],
                }
            )

    for match in copy_count_pattern.finditer(text):
        field = match.group("field")
        allocation = allocations.get(field)
        if allocation is not None and allocation["has_lock_usage"]:
            violations.append(
                {
                    "path": relative,
                    "owner": owner,
                    "type": "copy_count_destination_uses_lock_buffer_for_write",
                    "field": field,
                    "line": text[: match.start()].count("\n") + 1,
                    "allocationLine": allocation["line"],
                    "allocation": allocation["raw"],
                }
            )

    for field, allocation in allocations.items():
        if allocation["is_append"] and allocation["has_lock_usage"]:
            violations.append(
                {
                    "path": relative,
                    "owner": owner,
                    "type": "append_buffer_uses_lock_buffer_for_write",
                    "field": field,
                    "line": allocation["line"],
                    "allocationLine": allocation["line"],
                    "allocation": allocation["raw"],
                }
            )

    return violations


def analyze_csharp_thread_group_contract(root: Path, path: Path) -> list[dict[str, Any]]:
    text = read_text(path)
    relative = repo_path(root, path)
    owner = "vendor" if is_vendor(relative) else "first_party"
    violations: list[dict[str, Any]] = []
    portable_max_pattern = re.compile(
        r"\b(?:private|internal|public|protected)?\s*(?:const|static\s+readonly)\s+(?:int|uint)\s+"
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*(?:(?:ThreadGroup[A-Za-z0-9_]*(?:Max|Maximum))|(?:(?:Max|Maximum)[A-Za-z0-9_]*ThreadGroup))[A-Za-z0-9_]*)\s*=\s*(?P<value>\d+)",
    )
    for match in portable_max_pattern.finditer(text):
        value = int(match.group("value"))
        if value <= THREAD_LIMIT:
            continue
        violations.append(
            {
                "path": relative,
                "owner": owner,
                "type": "thread_group_portable_max_exceeds_limit",
                "name": match.group("name"),
                "value": value,
                "line": text[: match.start()].count("\n") + 1,
            }
        )
    return violations


def analyze_payload_owner_query_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_PAYLOAD_OWNER_QUERY_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    forbidden: list[str] = []
    if "sizeX = math.max(1, fallbackX)" in text or "sizeX = Mathf.Max(1, fallbackX)" in text:
        forbidden.append("3D kernel thread-size resolver keeps fallback X after invalid query")
    if "sizeY = math.max(1, fallbackY)" in text or "sizeY = Mathf.Max(1, fallbackY)" in text:
        forbidden.append("3D kernel thread-size resolver keeps fallback Y after invalid query")
    if "sizeZ = math.max(1, fallbackZ)" in text or "sizeZ = Mathf.Max(1, fallbackZ)" in text:
        forbidden.append("3D kernel thread-size resolver keeps fallback Z after invalid query")
    if "groupSizeX = FallbackThreadGroupSizeX" in text:
        forbidden.append("2D payload resolver keeps fallback X after invalid query")
    if "groupSizeY = FallbackThreadGroupSizeY" in text:
        forbidden.append("2D payload resolver keeps fallback Y after invalid query")
    if "_calculateThreadGroupSizeX = FallbackThreadGroupSizeX" in text:
        forbidden.append("payload invalidation keeps fallback calculate X after invalid query")
    if "_clearThreadGroupSizeY = FallbackThreadGroupSizeY" in text:
        forbidden.append("payload invalidation keeps fallback clear Y after invalid query")
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
        "forbidden_fragments": forbidden,
    }


def analyze_strict_thread_query_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_STRICT_THREAD_QUERY_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    forbidden: list[str] = []
    if "safeDivisor" in text:
        forbidden.append("safeDivisor masks invalid thread-group divisor")
    if re.search(r"GetKernelThreadGroupSizes\([^;\n]*out\s*_,\s*out\s*_", text):
        forbidden.append("thread-group query ignores Y/Z dimensions")
    if re.search(r"return\s+(?:math|Mathf)\.max\(1,\s*fallback", text):
        forbidden.append("fallback masks invalid kernel thread-group contract")
    if "FallbackThreadGroupSize" in text:
        forbidden.append("cached dispatch thread-group size initializes from fallback constant")
    if re.search(r"\bconst\s+int\s+PhantomDroneThreadGroupSize\b", text):
        forbidden.append("phantom drone dispatch thread-group size initializes from fallback constant")
    nonzero_thread_group_state = re.findall(
        r"\bprivate\s+(?:int|uint)\s+_[A-Za-z0-9_]*ThreadGroupSize[A-Za-z0-9_]*\s*=\s*(?!0\b)[^;]+;",
        text,
    )
    if nonzero_thread_group_state:
        forbidden.append(
            "persistent dispatch thread-group metadata initializes nonzero before GetKernelThreadGroupSizes"
        )
    fallback_resolver_signature = re.findall(
        r"ResolveKernelThreadGroupSize[A-Za-z0-9_]*\s*\([^)]*\bfallback[A-Za-z0-9_]*",
        text,
        flags=re.DOTALL,
    )
    if fallback_resolver_signature:
        forbidden.append("thread-group resolver API still accepts fallback sizing")
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
        "forbidden_fragments": forbidden,
    }


def analyze_compute_support_gate_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_COMPUTE_SUPPORT_GATE_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    dispatch_or_kernel = ".Dispatch(" in text or ".DispatchCompute(" in text or "FindKernel" in text
    missing = []
    if dispatch_or_kernel and not (
        any(fragment in text for fragment in required_fragments) or
        "HardwareTierDetector.AllowHighResourceComputeShaders" in text
    ):
        missing.extend(required_fragments)
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
    }


def analyze_high_resource_compute_api_guard_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_HIGH_RESOURCE_COMPUTE_API_GUARD_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
    }


def extract_high_resource_backend_assignment(text: str) -> str:
    assignments = re.findall(r"_allowHighResourceComputeShaders\s*=\s*(.*?);", text, re.DOTALL)
    for assignment in assignments:
        if "SystemInfo.supportsComputeShaders" in assignment:
            return "_allowHighResourceComputeShaders = " + assignment + ";"
    if not assignments:
        return ""
    return "_allowHighResourceComputeShaders = " + assignments[-1] + ";"


def analyze_high_resource_compute_backend_policy(root: Path) -> dict[str, Any]:
    path = root / AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_PATH
    text = read_text(path)
    assignment = extract_high_resource_backend_assignment(text)
    missing = [
        fragment
        for fragment in AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_FRAGMENTS
        if fragment not in assignment
    ]
    forbidden = [
        fragment
        for fragment in AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_FORBIDDEN
        if fragment in assignment
    ]
    return {
        "path": AGENT_1333_HIGH_RESOURCE_COMPUTE_BACKEND_POLICY_PATH,
        "sha256": file_sha256(path),
        "owner": "first_party",
        "assignment": " ".join(assignment.split()),
        "missing_fragments": missing,
        "forbidden_fragments": forbidden,
    }


def analyze_compute_texture_random_write_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_COMPUTE_TEXTURE_RANDOM_WRITE_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
    }


def analyze_find_kernel_fail_closed_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_FIND_KERNEL_FAIL_CLOSED_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    lines = text.splitlines()
    violations: list[dict[str, Any]] = []
    for index, line in enumerate(lines):
        stripped = line.strip()
        if ".FindKernel(" not in stripped or stripped.startswith("//"):
            continue

        context = "\n".join(lines[max(0, index - 8) : index + 1])
        if any(fragment in context for fragment in required_fragments):
            continue

        violations.append(
            {
                "line": index + 1,
                "snippet": normalize_snippet(stripped),
            }
        )

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "violations": violations,
    }


def analyze_supported_kernel_resolve_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_SUPPORTED_KERNEL_RESOLVE_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    forbidden: list[str] = []
    if re.search(r"return\s+[^;\n]+\?\s*[A-Za-z0-9_\.]+\.FindKernel\(", text):
        forbidden.append("ResolveKernel returns FindKernel without kernel-level IsSupported proof")
    if re.search(r"TryFindKernel\([^)]*\).*?return\s+[^;\n]+\?\s*shader\.FindKernel\(", text, re.DOTALL):
        forbidden.append("TryFindKernel returns shader.FindKernel without kernel-level IsSupported proof")
    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
        "forbidden_fragments": forbidden,
    }


def analyze_dispatch_group_limit_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    required_fragments = AGENT_1333_DISPATCH_GROUP_LIMIT_CONTRACTS.get(relative)
    if required_fragments is None:
        return None

    text = read_text(path)
    missing = [fragment for fragment in required_fragments if fragment not in text]
    forbidden: list[str] = []
    forbidden_patterns = (
        (r"(?:math|Mathf)\.max\(1,\s*CeilDivPositive", "max(1, CeilDivPositive) masks zero/capped dispatch groups"),
        (r"(?:math|Mathf)\.max\(1,\s*CeilDividePositive", "max(1, CeilDividePositive) masks zero/capped dispatch groups"),
        (r"safeDenominator\s*=", "safeDenominator masks invalid divisor"),
        (r"safeGroupSize\s*=\s*(?:math|Mathf)\.max\(1,\s*groupSize\)", "safeGroupSize masks invalid group size"),
        (r"(?:Mathf\.Max|math\.max)\(1u,\s*data\.threadGroupSize", "CommandBuffer dispatch renderfunc masks invalid uint thread groups"),
        (r"Mathf\.CeilToInt\([^;\n]+/\s*Mathf\.Max\(1u", "CommandBuffer renderfunc computes dispatch through fallback uint divisor"),
        (r"math\.rcp\(math\.max\(1f,\s*\(float\)_[A-Za-z0-9_]*ThreadGroup", "inverse thread group cache masks invalid kernel query"),
        (r"DispatchCompute\([^;\n]+Mathf\.Max\(1,", "CommandBuffer DispatchCompute masks invalid dispatch axis"),
        (r"return\s+1\s+\(\([A-Za-z0-9_]+\s*-\s*1\)\s*/", "old 1+ceil helper lacks dispatch group cap"),
        (r"return\s+(?:math|Mathf)\.max\(1,\s*groups\)", "return max(1, groups) masks capped dispatch groups"),
        (r"\(int\)\(\(\(long\)[^;\n]+\+\s*[^;\n]+-\s*1L\)\s*/\s*[^;\n]+\)", "direct long-backed dispatch ceil lacks max per-dimension cap"),
    )
    for pattern, reason in forbidden_patterns:
        if re.search(pattern, text):
            forbidden.append(reason)

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing,
        "forbidden_fragments": forbidden,
    }


def analyze_literal_one_group_dispatch_contract(root: Path, path: Path) -> list[dict[str, Any]]:
    text = read_text(path)
    if ".Dispatch(" not in text and ".DispatchCompute(" not in text:
        return []

    relative = repo_path(root, path)
    owner = "vendor" if is_vendor(relative) else "first_party"
    lines = text.splitlines()
    violations: list[dict[str, Any]] = []
    literal_one_group_pattern = re.compile(
        r"\.(?:Dispatch|DispatchCompute)\s*\([^;\n]*,\s*1\s*,\s*1\s*,\s*1\s*\)\s*;?$"
    )
    for index, line in enumerate(lines):
        if ".Dispatch(" not in line and ".DispatchCompute(" not in line:
            continue

        expression = collect_dispatch_expression(lines, index)
        if not is_compute_dispatch_expression(expression):
            continue
        if not literal_one_group_pattern.search(expression):
            continue

        violations.append(
            {
                "path": relative,
                "owner": owner,
                "line": index + 1,
                "snippet": normalize_snippet(expression),
            }
        )

    return violations


def extract_csharp_method_body(text: str, signature: str) -> str:
    signature_index = text.find(signature)
    if signature_index < 0:
        return ""

    brace_index = text.find("{", signature_index)
    if brace_index < 0:
        return ""

    depth = 0
    for index in range(brace_index, len(text)):
        character = text[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index : index + 1]

    return text[brace_index:]


def analyze_compute_service_bridge_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    contract = AGENT_1333_COMPUTE_SERVICE_BRIDGE_CONTRACTS.get(relative)
    if contract is None:
        return None

    text = read_text(path)
    missing_fragments = [
        fragment for fragment in contract["required_fragments"] if fragment not in text
    ]
    forbidden_fragments: list[str] = []
    for signature in contract["resolver_signatures"]:
        body = extract_csharp_method_body(text, signature)
        if not body:
            missing_fragments.append(signature)
            continue

        if "GlobalRegistry.InstanceCulling" in body:
            forbidden_fragments.append(f"{signature} -> GlobalRegistry.InstanceCulling")

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing_fragments,
        "forbidden_fragments": forbidden_fragments,
    }


def analyze_vendor_compute_integration_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    contract = AGENT_1333_VENDOR_COMPUTE_INTEGRATION_CONTRACTS.get(relative)
    if contract is None:
        return None

    text = read_text(path)
    missing_fragments = [
        fragment for fragment in contract["required_fragments"] if fragment not in text
    ]
    forbidden_fragments: list[str] = []
    for signature in contract["guarded_methods"]:
        body = extract_csharp_method_body(text, signature)
        if not body:
            missing_fragments.append(signature)
            continue

        if "GPUInstancerAPI." in body and "CanUseVendorGpuInstancerCompute()" not in body:
            forbidden_fragments.append(f"{signature} -> unguarded GPUInstancerAPI")
        if signature.endswith("ShouldUseFloraGpuiPath(") and "CanUseVendorGpuInstancerCompute()" not in body:
            forbidden_fragments.append(f"{signature} -> unguarded GPUI path selection")

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "missing_fragments": missing_fragments,
        "forbidden_fragments": forbidden_fragments,
    }


def extract_component_blocks(text: str, classifier: str) -> list[dict[str, Any]]:
    blocks: list[dict[str, Any]] = []
    if classifier not in text:
        return blocks

    if "--- !u!" not in text:
        return [
            {
                "game_object_id": "",
                "enabled": True,
            }
            for _ in range(text.count(classifier))
        ]

    for raw_block in re.split(r"\n(?=--- !u!)", text):
        if classifier not in raw_block:
            continue
        game_object_match = re.search(r"m_GameObject:\s*\{fileID:\s*(-?\d+)\}", raw_block)
        enabled_match = re.search(r"m_Enabled:\s*([01])", raw_block)
        blocks.append(
            {
                "game_object_id": game_object_match.group(1) if game_object_match else "",
                "enabled": enabled_match.group(1) != "0" if enabled_match else True,
            }
        )
    return blocks


def vendor_gpui_manager_admission_guard_proven(root: Path) -> bool:
    guard_contracts = {
        "Assets/_Project/Scripts/HectonRockManager.cs": (
            "ApplyVendorGpuiManagerAdmission()",
            "gpuiManager.enabled = false",
            "HardwareTierDetector.AllowHighResourceComputeShaders",
        ),
        "Assets/_Project/Scripts/WorldProceduralScatterDirector.cs": (
            "ApplyVendorGpuiManagerAdmission()",
            "floraGpuiManager.enabled = false",
            "HardwareTierDetector.AllowHighResourceComputeShaders",
        ),
    }
    for relative, fragments in guard_contracts.items():
        path = root / relative
        if not path.exists():
            return False
        text = read_text(path)
        if any(fragment not in text for fragment in fragments):
            return False
    return True


def analyze_vendor_component_activation_contract(root: Path, path: Path) -> dict[str, Any] | None:
    relative = repo_path(root, path)
    if path.suffix == ".unity":
        data = path.read_bytes()
        classifier_bytes = (
            CREST_SHAPE_FFT_CLASSIFIER.encode("utf-8"),
            CREST_ADAPTER_CLASSIFIER.encode("utf-8"),
            VENDOR_PREFAB_MARKER_CLASSIFIER.encode("utf-8"),
            *(classifier.encode("utf-8") for classifier in VENDOR_COMPONENT_MANAGER_CLASSIFIERS),
        )
        if not any(classifier in data for classifier in classifier_bytes):
            return None

        gpui_manager_admission_guard_proven = vendor_gpui_manager_admission_guard_proven(root)
        crest_shape_count = data.count(CREST_SHAPE_FFT_CLASSIFIER.encode("utf-8"))
        crest_adapter_count = data.count(CREST_ADAPTER_CLASSIFIER.encode("utf-8"))
        manager_hits: list[str] = []
        forbidden_fragments: list[str] = []
        for classifier in VENDOR_COMPONENT_MANAGER_CLASSIFIERS:
            count = data.count(classifier.encode("utf-8"))
            for _ in range(count):
                manager_hits.append(f"{classifier}@unity-bytes")
                if not gpui_manager_admission_guard_proven:
                    forbidden_fragments.append(classifier)
        missing_fragments: list[str] = []
        if crest_shape_count > 0 and crest_adapter_count < crest_shape_count:
            missing_fragments.append("Crest4KinematicsAdapter on active Crest.ShapeFFT GameObject")
        return {
            "path": relative,
            "sha256": file_sha256(path),
            "owner": "vendor" if is_vendor(relative) else "first_party",
            "crest_shape_fft_count": crest_shape_count,
            "guarded_crest_shape_fft_count": min(crest_shape_count, crest_adapter_count),
            "gpui_manager_hits": manager_hits,
            "gpui_manager_admission_guard_proven": gpui_manager_admission_guard_proven,
            "gpui_prefab_marker_count": data.count(VENDOR_PREFAB_MARKER_CLASSIFIER.encode("utf-8")),
            "missing_fragments": missing_fragments,
            "forbidden_fragments": forbidden_fragments,
        }

    text = read_text(path)
    if (
        CREST_SHAPE_FFT_CLASSIFIER not in text
        and VENDOR_PREFAB_MARKER_CLASSIFIER not in text
        and not any(classifier in text for classifier in VENDOR_COMPONENT_MANAGER_CLASSIFIERS)
    ):
        return None

    crest_shape_blocks = extract_component_blocks(text, CREST_SHAPE_FFT_CLASSIFIER)
    crest_adapter_blocks = extract_component_blocks(text, CREST_ADAPTER_CLASSIFIER)
    crest_adapter_game_objects = {
        block["game_object_id"]
        for block in crest_adapter_blocks
        if block["enabled"] and block["game_object_id"]
    }
    missing_fragments: list[str] = []
    forbidden_fragments: list[str] = []
    gpui_manager_admission_guard_proven = vendor_gpui_manager_admission_guard_proven(root)
    guarded_shape_count = 0
    for shape in crest_shape_blocks:
        if not shape["enabled"]:
            continue
        if shape["game_object_id"] and shape["game_object_id"] in crest_adapter_game_objects:
            guarded_shape_count += 1
        else:
            missing_fragments.append("Crest4KinematicsAdapter on active Crest.ShapeFFT GameObject")

    manager_hits: list[str] = []
    for classifier in VENDOR_COMPONENT_MANAGER_CLASSIFIERS:
        manager_blocks = [
            block
            for block in extract_component_blocks(text, classifier)
            if block["enabled"]
        ]
        for block in manager_blocks:
            manager_hits.append(f"{classifier}@{block['game_object_id'] or 'unknown'}")
            if not gpui_manager_admission_guard_proven:
                forbidden_fragments.append(classifier)

    prefab_marker_blocks = [
        block
        for block in extract_component_blocks(text, VENDOR_PREFAB_MARKER_CLASSIFIER)
        if block["enabled"]
    ]

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "crest_shape_fft_count": len([block for block in crest_shape_blocks if block["enabled"]]),
        "guarded_crest_shape_fft_count": guarded_shape_count,
        "gpui_manager_hits": manager_hits,
        "gpui_manager_admission_guard_proven": gpui_manager_admission_guard_proven,
        "gpui_prefab_marker_count": len(prefab_marker_blocks),
        "missing_fragments": missing_fragments,
        "forbidden_fragments": forbidden_fragments,
    }


def analyze_cs_file(root: Path, path: Path) -> dict[str, Any] | None:
    text = read_text(path)
    if ".Dispatch(" not in text and ".DispatchCompute(" not in text:
        return None
    relative = repo_path(root, path)
    lines = text.splitlines()
    dispatches: list[dict[str, Any]] = []
    for index, line in enumerate(lines):
        if ".Dispatch(" not in line and ".DispatchCompute(" not in line:
            continue
        expression = collect_dispatch_expression(lines, index)
        if not is_compute_dispatch_expression(expression):
            continue
        sizing_context = "\n".join(lines[max(0, index - 6) : min(len(lines), index + 7)])
        hardcoded_evidence = bool(
            re.search(
                r"(\+\s*(?:31|63|127|255|511)\)|>>\s*(?:5|6|7|8|9)|CeilToInt\([^)]*/\s*(?:32f?|64f?|128f?|256f?|512f?)|\b(?:CeilDiv(?:ide)?(?:Positive)?|ResolveDispatchGroups|CalculateDispatchGroups)\s*\([^;\n]*,\s*(?:32|64|128|256|512)\s*\))",
                sizing_context,
            )
        )
        dispatches.append(
            {
                "line": index + 1,
                "expression": expression,
                "hardcoded_sizing_evidence": hardcoded_evidence,
                "external_payload_sizing": uses_external_payload_sizing(expression),
            }
        )
    if not dispatches:
        return None

    return {
        "path": relative,
        "sha256": file_sha256(path),
        "owner": "vendor" if is_vendor(relative) else "first_party",
        "dispatch_count": len(dispatches),
        "file_has_thread_group_query": "GetKernelThreadGroupSizes" in text,
        "file_has_kernel_support_query": "IsSupported(" in text,
        "file_has_compute_support_gate": "SystemInfo.supportsComputeShaders" in text
        or "HardwareTierDetector.AllowHighResourceComputeShaders" in text,
        "file_has_high_resource_compute_gate": "HardwareTierDetector.AllowHighResourceComputeShaders" in text,
        "compute_asset_references": sorted(
            set(re.findall(r'"(Assets/_Project/[^"]+\.compute)"', text))
        ),
        "file_uses_external_payload_sizing": all(
            dispatch["external_payload_sizing"] for dispatch in dispatches
        ),
        "dispatches": dispatches,
    }


def build_report(root: Path) -> dict[str, Any]:
    compute_files = [analyze_compute_file(root, path) for path in iter_files(root / "Assets", ".compute")]
    csharp_source_paths = iter_files(root / "Assets", ".cs")
    cs_files = [
        report
        for path in csharp_source_paths
        for report in (analyze_cs_file(root, path),)
        if report is not None
    ]
    graphics_buffer_lock_contract_violations = [
        violation
        for path in csharp_source_paths
        for violation in analyze_graphics_buffer_lock_contract(root, path)
    ]
    legacy_compute_buffer_violations = [
        violation
        for path in csharp_source_paths
        for violation in analyze_legacy_compute_buffer_contract(root, path)
    ]
    csharp_thread_group_contract_violations = [
        violation
        for path in csharp_source_paths
        for violation in analyze_csharp_thread_group_contract(root, path)
    ]
    payload_owner_query_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_payload_owner_query_contract(root, path),)
        if report is not None
    ]
    strict_thread_query_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_strict_thread_query_contract(root, path),)
        if report is not None
    ]
    kernel_support_contracts = [
        {
            "path": report["path"],
            "sha256": report["sha256"],
            "owner": report["owner"],
            "dispatch_count": report["dispatch_count"],
            "file_has_kernel_support_query": report["file_has_kernel_support_query"],
            "file_uses_external_payload_sizing": report["file_uses_external_payload_sizing"],
            "missing_fragments": []
            if report["file_has_kernel_support_query"] or report["file_uses_external_payload_sizing"]
            else ["ComputeShader.IsSupported(kernel)"],
        }
        for report in cs_files
    ]
    compute_support_gate_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_compute_support_gate_contract(root, path),)
        if report is not None
    ]
    high_resource_compute_api_guard_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_high_resource_compute_api_guard_contract(root, path),)
        if report is not None
    ]
    high_resource_compute_backend_policy = analyze_high_resource_compute_backend_policy(root)
    compute_service_bridge_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_compute_service_bridge_contract(root, path),)
        if report is not None
    ]
    vendor_compute_integration_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_vendor_compute_integration_contract(root, path),)
        if report is not None
    ]
    authored_asset_paths = (
        iter_files(root / "Assets" / "_Project", ".prefab")
        + iter_files(root / "Assets" / "_Project", ".unity")
    )
    vendor_component_activation_contracts = [
        report
        for path in authored_asset_paths
        for report in (analyze_vendor_component_activation_contract(root, path),)
        if report is not None
    ]
    compute_texture_random_write_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_compute_texture_random_write_contract(root, path),)
        if report is not None
    ]
    find_kernel_fail_closed_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_find_kernel_fail_closed_contract(root, path),)
        if report is not None
    ]
    supported_kernel_resolve_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_supported_kernel_resolve_contract(root, path),)
        if report is not None
    ]
    dispatch_group_limit_contracts = [
        report
        for path in csharp_source_paths
        for report in (analyze_dispatch_group_limit_contract(root, path),)
        if report is not None
    ]
    literal_one_group_dispatch_violations = [
        violation
        for path in csharp_source_paths
        for violation in analyze_literal_one_group_dispatch_contract(root, path)
    ]
    first_party_graphics_buffer_lock_contract_violations = [
        violation
        for violation in graphics_buffer_lock_contract_violations
        if violation["owner"] == "first_party"
    ]
    first_party_legacy_compute_buffer_violations = [
        violation
        for violation in legacy_compute_buffer_violations
        if violation["owner"] == "first_party"
    ]
    first_party_csharp_thread_group_contract_violations = [
        violation
        for violation in csharp_thread_group_contract_violations
        if violation["owner"] == "first_party"
    ]
    first_party_payload_owner_query_contract_violations = [
        report
        for report in payload_owner_query_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_strict_thread_query_contract_violations = [
        report
        for report in strict_thread_query_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_kernel_support_contract_violations = [
        report
        for report in kernel_support_contracts
        if report["owner"] == "first_party" and report["missing_fragments"]
    ]
    first_party_compute_support_gate_violations = [
        report
        for report in compute_support_gate_contracts
        if report["owner"] == "first_party" and report["missing_fragments"]
    ]
    first_party_high_resource_compute_api_guard_violations = [
        report
        for report in high_resource_compute_api_guard_contracts
        if report["owner"] == "first_party" and report["missing_fragments"]
    ]
    first_party_high_resource_compute_backend_policy_violations = (
        [high_resource_compute_backend_policy]
        if high_resource_compute_backend_policy["missing_fragments"]
        or high_resource_compute_backend_policy["forbidden_fragments"]
        else []
    )
    first_party_compute_service_bridge_violations = [
        report
        for report in compute_service_bridge_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_vendor_compute_integration_violations = [
        report
        for report in vendor_compute_integration_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_vendor_component_activation_violations = [
        report
        for report in vendor_component_activation_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_compute_texture_random_write_violations = [
        report
        for report in compute_texture_random_write_contracts
        if report["owner"] == "first_party" and report["missing_fragments"]
    ]
    first_party_find_kernel_fail_closed_violations = [
        report
        for report in find_kernel_fail_closed_contracts
        if report["owner"] == "first_party" and report["violations"]
    ]
    first_party_supported_kernel_resolve_violations = [
        report
        for report in supported_kernel_resolve_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_dispatch_group_limit_violations = [
        report
        for report in dispatch_group_limit_contracts
        if report["owner"] == "first_party"
        and (report["missing_fragments"] or report["forbidden_fragments"])
    ]
    first_party_literal_one_group_dispatch_violations = [
        violation
        for violation in literal_one_group_dispatch_violations
        if violation["owner"] == "first_party"
    ]

    compute_over_limit = [
        {
            "path": report["path"],
            "owner": report["owner"],
            "max_product": report["max_product"],
            "groupshared_count": report["groupshared_count"],
            "barrier_count": report["barrier_count"],
            "interlocked_count": report["interlocked_count"],
            "kernels": [
                declaration
                for declaration in report["declarations"]
                if declaration["over_limit"]
            ],
        }
        for report in compute_files
        if any(declaration["over_limit"] for declaration in report["declarations"])
    ]
    first_party_compute_over_limit = [
        item for item in compute_over_limit if item["owner"] == "first_party"
    ]
    first_party_gles31_resource_pressure = [
        {
            "path": report["path"],
            "resource_count": report["resource_count"],
            "resource_declarations": report["resource_declarations"],
        }
        for report in compute_files
        if report["owner"] == "first_party"
        and report["resource_count"] > GLES31_GUARANTEED_COMPUTE_BUFFER_LIMIT
    ]
    first_party_gles31_resource_pressure_by_path = {
        report["path"]: report["resource_count"]
        for report in compute_files
        if report["owner"] == "first_party"
        and report["resource_count"] > GLES31_GUARANTEED_COMPUTE_BUFFER_LIMIT
    }
    high_resource_compute_reference_contracts = [
        {
            "path": report["path"],
            "sha256": report["sha256"],
            "owner": report["owner"],
            "resource_pressure_references": [
                {
                    "path": compute_path,
                    "resource_count": first_party_gles31_resource_pressure_by_path[compute_path],
                }
                for compute_path in report["compute_asset_references"]
                if compute_path in first_party_gles31_resource_pressure_by_path
            ],
            "file_has_high_resource_compute_gate": report["file_has_high_resource_compute_gate"],
            "file_uses_external_payload_sizing": report["file_uses_external_payload_sizing"],
            "missing_fragments": [],
        }
        for report in cs_files
        if any(
            compute_path in first_party_gles31_resource_pressure_by_path
            for compute_path in report["compute_asset_references"]
        )
    ]
    for contract in high_resource_compute_reference_contracts:
        if (
            contract["owner"] == "first_party"
            and not contract["file_uses_external_payload_sizing"]
            and not contract["file_has_high_resource_compute_gate"]
        ):
            contract["missing_fragments"] = ["HardwareTierDetector.AllowHighResourceComputeShaders"]
    first_party_high_resource_compute_reference_violations = [
        report
        for report in high_resource_compute_reference_contracts
        if report["owner"] == "first_party" and report["missing_fragments"]
    ]
    first_party_dispatch_missing_query = [
        {
            "path": report["path"],
            "dispatch_count": report["dispatch_count"],
            "hardcoded_sizing_lines": [
                dispatch["line"]
                for dispatch in report["dispatches"]
                if dispatch["hardcoded_sizing_evidence"]
            ],
        }
        for report in cs_files
        if report["owner"] == "first_party"
        and not report["file_has_thread_group_query"]
        and not report["file_uses_external_payload_sizing"]
    ]
    first_party_dispatch_hardcoded = [
        {
            "path": report["path"],
            "lines": [
                dispatch["line"]
                for dispatch in report["dispatches"]
                if dispatch["hardcoded_sizing_evidence"]
            ],
        }
        for report in cs_files
        if report["owner"] == "first_party"
        and any(dispatch["hardcoded_sizing_evidence"] for dispatch in report["dispatches"])
    ]
    sonar_map = next(
        (report for report in compute_files if report["path"] == "Assets/_Project/Art/Shaders/Hecton_SonarMap.compute"),
        None,
    )
    sonar_raycast = None
    if sonar_map is not None:
        sonar_raycast = next(
            (
                declaration
                for declaration in sonar_map["declarations"]
                if declaration["kernel"] == "CSRaymarch"
            ),
            None,
        )
    dynamic_dispatch_proof = [
        {
            "path": report["path"],
            "sha256": report["sha256"],
            "dispatch_count": report["dispatch_count"],
            "agent_1333_modified": report["path"] in AGENT_1333_DYNAMIC_DISPATCH_FILES,
        }
        for report in cs_files
        if report["owner"] == "first_party"
        and report["file_has_thread_group_query"]
    ]
    external_payload_bridges = [
        {
            "path": report["path"],
            "sha256": report["sha256"],
            "dispatch_count": report["dispatch_count"],
        }
        for report in cs_files
        if report["owner"] == "first_party"
        and report["file_uses_external_payload_sizing"]
    ]

    return {
        "scanner": "Tools/OOP_ComputeDispatch_Scanner.py",
        "thread_group_limit": THREAD_LIMIT,
        "gles31_guaranteed_compute_buffer_limit": GLES31_GUARANTEED_COMPUTE_BUFFER_LIMIT,
        "compute_file_count": len(compute_files),
        "csharp_dispatch_file_count": len(cs_files),
        "agent_1333_change_summary": {
            "shader_thread_reductions": [
                {
                    "path": "Assets/_Project/Art/Shaders/Hecton_SonarMap.compute",
                    "kernel": "CSRaymarch",
                    "previous_numthreads": [8, 8, 8],
                    "previous_product": 512,
                    "current_numthreads": sonar_raycast["resolved"] if sonar_raycast else None,
                    "current_product": sonar_raycast["product"] if sonar_raycast else None,
                    "sha256": sonar_map["sha256"] if sonar_map else None,
                }
            ],
            "dynamic_dispatch_query_files": [
                item for item in dynamic_dispatch_proof if item["agent_1333_modified"]
            ],
            "external_payload_sized_dispatch_bridges": external_payload_bridges,
            "payload_owner_query_contracts": payload_owner_query_contracts,
            "strict_thread_query_contracts": strict_thread_query_contracts,
            "kernel_support_contracts": kernel_support_contracts,
            "compute_support_gate_contracts": compute_support_gate_contracts,
            "high_resource_compute_api_guard_contracts": high_resource_compute_api_guard_contracts,
            "high_resource_compute_reference_contracts": high_resource_compute_reference_contracts,
            "high_resource_compute_backend_policy": high_resource_compute_backend_policy,
            "compute_service_bridge_contracts": compute_service_bridge_contracts,
            "vendor_compute_integration_contracts": vendor_compute_integration_contracts,
            "vendor_component_activation_contracts": vendor_component_activation_contracts,
            "compute_texture_random_write_contracts": compute_texture_random_write_contracts,
            "find_kernel_fail_closed_contracts": find_kernel_fail_closed_contracts,
            "supported_kernel_resolve_contracts": supported_kernel_resolve_contracts,
            "dispatch_group_limit_contracts": dispatch_group_limit_contracts,
            "literal_one_group_dispatch_contracts": literal_one_group_dispatch_violations,
        },
        "compute_over_limit": compute_over_limit,
        "first_party_compute_over_limit_count": len(first_party_compute_over_limit),
        "first_party_dispatch_missing_query": first_party_dispatch_missing_query,
        "first_party_dispatch_hardcoded": first_party_dispatch_hardcoded,
        "first_party_graphics_buffer_lock_contract_violation_count": len(first_party_graphics_buffer_lock_contract_violations),
        "graphics_buffer_lock_contract_violations": graphics_buffer_lock_contract_violations,
        "first_party_legacy_compute_buffer_violation_count": len(first_party_legacy_compute_buffer_violations),
        "legacy_compute_buffer_violations": legacy_compute_buffer_violations,
        "first_party_csharp_thread_group_contract_violation_count": len(first_party_csharp_thread_group_contract_violations),
        "csharp_thread_group_contract_violations": csharp_thread_group_contract_violations,
        "first_party_payload_owner_query_contract_violation_count": len(first_party_payload_owner_query_contract_violations),
        "payload_owner_query_contract_violations": first_party_payload_owner_query_contract_violations,
        "first_party_strict_thread_query_contract_violation_count": len(first_party_strict_thread_query_contract_violations),
        "strict_thread_query_contract_violations": first_party_strict_thread_query_contract_violations,
        "first_party_kernel_support_contract_violation_count": len(first_party_kernel_support_contract_violations),
        "kernel_support_contract_violations": first_party_kernel_support_contract_violations,
        "first_party_compute_support_gate_violation_count": len(first_party_compute_support_gate_violations),
        "compute_support_gate_violations": first_party_compute_support_gate_violations,
        "first_party_high_resource_compute_api_guard_violation_count": len(first_party_high_resource_compute_api_guard_violations),
        "high_resource_compute_api_guard_violations": first_party_high_resource_compute_api_guard_violations,
        "first_party_high_resource_compute_reference_violation_count": len(first_party_high_resource_compute_reference_violations),
        "high_resource_compute_reference_violations": first_party_high_resource_compute_reference_violations,
        "first_party_high_resource_compute_backend_policy_violation_count": len(first_party_high_resource_compute_backend_policy_violations),
        "high_resource_compute_backend_policy_violations": first_party_high_resource_compute_backend_policy_violations,
        "first_party_compute_service_bridge_violation_count": len(first_party_compute_service_bridge_violations),
        "compute_service_bridge_violations": first_party_compute_service_bridge_violations,
        "first_party_vendor_compute_integration_violation_count": len(first_party_vendor_compute_integration_violations),
        "vendor_compute_integration_violations": first_party_vendor_compute_integration_violations,
        "first_party_vendor_component_activation_violation_count": len(first_party_vendor_component_activation_violations),
        "vendor_component_activation_violations": first_party_vendor_component_activation_violations,
        "vendor_component_activation_contracts": vendor_component_activation_contracts,
        "first_party_compute_texture_random_write_violation_count": len(first_party_compute_texture_random_write_violations),
        "compute_texture_random_write_violations": first_party_compute_texture_random_write_violations,
        "first_party_find_kernel_fail_closed_violation_count": len(first_party_find_kernel_fail_closed_violations),
        "find_kernel_fail_closed_violations": first_party_find_kernel_fail_closed_violations,
        "first_party_supported_kernel_resolve_violation_count": len(first_party_supported_kernel_resolve_violations),
        "supported_kernel_resolve_violations": first_party_supported_kernel_resolve_violations,
        "first_party_dispatch_group_limit_violation_count": len(first_party_dispatch_group_limit_violations),
        "dispatch_group_limit_violations": first_party_dispatch_group_limit_violations,
        "first_party_literal_one_group_dispatch_violation_count": len(first_party_literal_one_group_dispatch_violations),
        "literal_one_group_dispatch_violations": first_party_literal_one_group_dispatch_violations,
        "first_party_gles31_resource_pressure_count": len(first_party_gles31_resource_pressure),
        "first_party_gles31_resource_pressure": first_party_gles31_resource_pressure,
        "compute_files": compute_files,
        "csharp_dispatch_files": cs_files,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit HECTON-8 compute dispatch portability.")
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--json",
        default="Docs/Reports/COMPUTE_DISPATCH_OPTIMIZATION_REPORT_1333.json",
        help="Output JSON path.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    report = build_report(root)
    output_path = (root / args.json).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")

    print(f"compute_files={report['compute_file_count']}")
    print(f"csharp_dispatch_files={report['csharp_dispatch_file_count']}")
    print(f"first_party_compute_over_limit={report['first_party_compute_over_limit_count']}")
    print(f"compute_over_limit_total={len(report['compute_over_limit'])}")
    print(f"first_party_dispatch_missing_query={len(report['first_party_dispatch_missing_query'])}")
    print(f"first_party_dispatch_hardcoded={len(report['first_party_dispatch_hardcoded'])}")
    print(f"first_party_graphics_buffer_lock_contract_violations={report['first_party_graphics_buffer_lock_contract_violation_count']}")
    print(f"first_party_legacy_compute_buffer_violations={report['first_party_legacy_compute_buffer_violation_count']}")
    print(f"first_party_csharp_thread_group_contract_violations={report['first_party_csharp_thread_group_contract_violation_count']}")
    print(f"first_party_payload_owner_query_contract_violations={report['first_party_payload_owner_query_contract_violation_count']}")
    print(f"first_party_strict_thread_query_contract_violations={report['first_party_strict_thread_query_contract_violation_count']}")
    print(f"first_party_kernel_support_contract_violations={report['first_party_kernel_support_contract_violation_count']}")
    print(f"first_party_compute_support_gate_violations={report['first_party_compute_support_gate_violation_count']}")
    print(f"first_party_high_resource_compute_api_guard_violations={report['first_party_high_resource_compute_api_guard_violation_count']}")
    print(f"first_party_high_resource_compute_reference_violations={report['first_party_high_resource_compute_reference_violation_count']}")
    print(f"first_party_high_resource_compute_backend_policy_violations={report['first_party_high_resource_compute_backend_policy_violation_count']}")
    print(f"first_party_compute_service_bridge_violations={report['first_party_compute_service_bridge_violation_count']}")
    print(f"first_party_vendor_compute_integration_violations={report['first_party_vendor_compute_integration_violation_count']}")
    print(f"first_party_vendor_component_activation_violations={report['first_party_vendor_component_activation_violation_count']}")
    print(f"first_party_compute_texture_random_write_violations={report['first_party_compute_texture_random_write_violation_count']}")
    print(f"first_party_find_kernel_fail_closed_violations={report['first_party_find_kernel_fail_closed_violation_count']}")
    print(f"first_party_supported_kernel_resolve_violations={report['first_party_supported_kernel_resolve_violation_count']}")
    print(f"first_party_dispatch_group_limit_violations={report['first_party_dispatch_group_limit_violation_count']}")
    print(f"first_party_literal_one_group_dispatch_violations={report['first_party_literal_one_group_dispatch_violation_count']}")
    print(f"first_party_gles31_resource_pressure={report['first_party_gles31_resource_pressure_count']}")
    print(f"json={output_path.relative_to(root).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

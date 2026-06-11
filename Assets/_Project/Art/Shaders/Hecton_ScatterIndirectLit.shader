Shader "Hecton8/World/ScatterIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        [NoScaleOffset] _HectonMicroNormalTex("Micro Normal 128", 2D) = "bump" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.35
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EnvironmentalWear("Environmental Wear", Range(0, 1)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _MicroNormalStrength("Micro Normal Strength", Range(0, 1)) = 0.24
        _MicroNormalTiling("Micro Normal Tiling", Range(4, 128)) = 52
        _StochasticTilingStrength("Stochastic Tiling Strength", Range(0, 1)) = 0.55
        _StormRainDripAmplitude("Storm Rain Drip Amplitude", Range(0, 0.025)) = 0.003
        _StormRainDripTiling("Storm Rain Drip Tiling", Range(0.5, 16)) = 5
        _StormRainDripSpeed("Storm Rain Drip Speed", Range(0, 8)) = 1.8
        _ScatterSwayAmplitude("Scatter Sway Amplitude", Range(0, 0.35)) = 0.08
        _ScatterSwayFrequency("Scatter Sway Frequency", Range(0, 8)) = 1.4
        _ProceduralRockDisplacement("Procedural Rock Displacement", Range(0, 0.35)) = 0.08
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma multi_compile _ DOTS_INSTANCING_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

        struct ScatterInstanceGpuData
        {
            float4 PositionScale;
            float4 NormalRotation;
            float4 AtlasFlow;
        };

        StructuredBuffer<ScatterInstanceGpuData> _HectonScatterInstances;
        StructuredBuffer<uint> _HectonVisibleScatterIndices;
        float4 _HectonScatterAupGridOffset;
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _RustSaltColor;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EnvironmentalWear;
            float _MicroNormalStrength;
            float _MicroNormalTiling;
            float _StochasticTilingStrength;
            float _StormRainDripAmplitude;
            float _StormRainDripTiling;
            float _StormRainDripSpeed;
            float _ScatterSwayAmplitude;
            float _ScatterSwayFrequency;
            float _ProceduralRockDisplacement;
            float _DepthBias;
        CBUFFER_END

        float HectonScatterFiniteOr(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float HectonScatterNonNegativeFinite(float value)
        {
            return isfinite(value) ? max(0.0, value) : 0.0;
        }

        float HectonScatterPositiveFinite(float value, float fallbackValue)
        {
            return isfinite(value) && value > 0.0001 ? value : fallbackValue;
        }

        float2 HectonScatterFinite2(float2 value, float2 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        float3 HectonScatterFinite3(float3 value, float3 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        float4 HectonScatterFinite4(float4 value, float4 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 viewDirWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ScatterInstanceGpuData ResolveScatterInstance(uint instanceID)
        {
            ScatterInstanceGpuData instanceData = _HectonScatterInstances[_HectonVisibleScatterIndices[instanceID]];
            instanceData.PositionScale.xyz = HectonScatterFinite3(instanceData.PositionScale.xyz, float3(0.0, 0.0, 0.0));
            instanceData.PositionScale.w = clamp(HectonScatterPositiveFinite(instanceData.PositionScale.w, 1.0), 0.05, 16.0);
            instanceData.NormalRotation.xyz = HectonCoreLitSafeNormalize(HectonScatterFinite3(instanceData.NormalRotation.xyz, float3(0.0, 1.0, 0.0)));
            instanceData.NormalRotation.w = min(HectonScatterNonNegativeFinite(instanceData.NormalRotation.w), 16777215.0);
            instanceData.AtlasFlow.xy = clamp(HectonScatterFinite2(instanceData.AtlasFlow.xy, float2(1.0, 1.0)), float2(-64.0, -64.0), float2(64.0, 64.0));
            instanceData.AtlasFlow.zw = clamp(HectonScatterFinite2(instanceData.AtlasFlow.zw, float2(0.0, 0.0)), float2(-4096.0, -4096.0), float2(4096.0, 4096.0));
            return instanceData;
        }

        float2 ResolveScatterYawOctant(float rotation)
        {
            uint sector = (uint)floor(HectonScatterNonNegativeFinite(rotation)) & 7u;
            if (sector == 0u) return float2(1.0, 0.0);
            if (sector == 1u) return float2(0.70710677, 0.70710677);
            if (sector == 2u) return float2(0.0, 1.0);
            if (sector == 3u) return float2(-0.70710677, 0.70710677);
            if (sector == 4u) return float2(-1.0, 0.0);
            if (sector == 5u) return float2(-0.70710677, -0.70710677);
            if (sector == 6u) return float2(0.0, -1.0);
            return float2(0.70710677, -0.70710677);
        }

        void BuildScatterBasis(float3 normalWS, float rotation, float scale, out float3 rightWS, out float3 upWS, out float3 forwardWS)
        {
            float safeScale = HectonScatterPositiveFinite(scale, 1.0);
            float3 safeNormalWS = HectonCoreLitSafeNormalize(HectonScatterFinite3(normalWS, float3(0.0, 1.0, 0.0)));
            float2 forwardXZ = ResolveScatterYawOctant(rotation);
            upWS = safeNormalWS.y < 0.0 ? float3(0.0, -1.0, 0.0) : float3(0.0, 1.0, 0.0);
            rightWS = float3(forwardXZ.y, 0.0, -forwardXZ.x) * safeScale;
            forwardWS = float3(forwardXZ.x, 0.0, forwardXZ.y) * safeScale;
            upWS *= safeScale;
        }

        float3 ResolveScatterNormal(float3 normalOS, float3 rightWS, float3 upWS, float3 forwardWS, float invScale)
        {
            float safeInvScale = HectonScatterPositiveFinite(abs(invScale), 1.0);
            float3 safeNormalOS = HectonCoreLitSafeNormalize(normalOS);
            float3 rightAxisWS = HectonScatterFinite3(rightWS * safeInvScale, float3(1.0, 0.0, 0.0));
            float3 upAxisWS = HectonScatterFinite3(upWS * safeInvScale, float3(0.0, 1.0, 0.0));
            float3 forwardAxisWS = HectonScatterFinite3(forwardWS * safeInvScale, float3(0.0, 0.0, 1.0));
            return HectonCoreLitSafeNormalize(rightAxisWS * safeNormalOS.x + upAxisWS * safeNormalOS.y + forwardAxisWS * safeNormalOS.z);
        }

        float3 ResolveScatterSineParabolaSway(float3 rootWS, float3 localPosition, float3 forwardWS, float scale, float rotation)
        {
            rootWS = HectonCoreLitSanitizePositionWS(rootWS);
            localPosition = HectonCoreLitSanitizePositionOS(localPosition);
            forwardWS = HectonScatterFinite3(forwardWS, float3(0.0, 0.0, 1.0));
            float safeScale = HectonScatterPositiveFinite(scale, 1.0);
            float heightMask = saturate(HectonScatterFiniteOr(localPosition.y, 0.0));
            float heightParabola = heightMask * heightMask;
            float2 flowXZ = HectonScatterFinite2(_AbyssalFlowWeatherCurrent.xz, float2(0.0, 0.0));
            float flowLenSq = dot(flowXZ, flowXZ);
            flowLenSq = isfinite(flowLenSq) ? flowLenSq : 0.0;
            float2 forwardXZ = forwardWS.xz;
            float forwardLenSq = dot(forwardXZ, forwardXZ);
            forwardLenSq = isfinite(forwardLenSq) ? forwardLenSq : 0.0;
            float2 fallbackDir = forwardLenSq > 0.0001 ? forwardXZ * rsqrt(forwardLenSq) : float2(0.0, 1.0);
            float2 flowDir = flowLenSq > 0.0001 ? flowXZ * rsqrt(flowLenSq) : fallbackDir;
            float flowGain = saturate(flowLenSq * 0.0625);
            float safeFrequency = min(HectonScatterNonNegativeFinite(_ScatterSwayFrequency), 8.0);
            float safeAmplitude = min(HectonScatterNonNegativeFinite(_ScatterSwayAmplitude), 0.35);
            float phase = HectonScatterFiniteOr(_Time.y, 0.0) * safeFrequency + dot(rootWS.xz, float2(0.073, -0.051)) + rotation;
            phase = HectonScatterFiniteOr(phase, 0.0);
            float triangleWave = HectonCoreLitTrianglePulse01(phase) * 2.0 - 1.0;
            float sineParabola = triangleWave * abs(triangleWave);
            return HectonScatterFinite3(float3(flowDir.x, 0.0, flowDir.y) * (sineParabola * heightParabola * safeAmplitude * safeScale * (0.25 + flowGain)), float3(0.0, 0.0, 0.0));
        }

        float HectonScatterHash31(float3 value)
        {
            value = frac(value * 0.1031);
            value += dot(value, value.yzx + 33.33);
            return frac((value.x + value.y) * value.z);
        }

        float3 ResolveProceduralRockOffset(float3 rootWS, float3 localPosition, float3 normalOS, float scale)
        {
            rootWS = HectonCoreLitSanitizePositionWS(rootWS);
            localPosition = HectonCoreLitSanitizePositionOS(localPosition);
            float3 safeNormalOS = HectonCoreLitSafeNormalize(normalOS);
            float safeScale = HectonScatterPositiveFinite(scale, 1.0);
            float2 safeAupOffset = HectonScatterFinite2(_HectonScatterAupGridOffset.xy, float2(0.0, 0.0));
            float3 stableRoot = rootWS + float3(safeAupOffset.x, 0.0, safeAupOffset.y);
            float rockHash = HectonScatterHash31(stableRoot * 0.173 + localPosition * 3.71);
            float ridge = rockHash * 2.0 - 1.0;
            float verticalMask = saturate(HectonScatterFiniteOr(localPosition.y, 0.0) + 0.55);
            float displacement = min(HectonScatterNonNegativeFinite(_ProceduralRockDisplacement), 0.35);
            return HectonScatterFinite3(safeNormalOS * (ridge * displacement * safeScale * (0.25 + verticalMask * 0.75)), float3(0.0, 0.0, 0.0));
        }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            uint instanceID = input.instanceID;
        #if UNITY_ANY_INSTANCING_ENABLED
            instanceID = unity_InstanceID;
        #endif
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = HectonScatterPositiveFinite(instanceData.PositionScale.w, 1.0);
            float invScale = rcp(max(scale, 0.0001));
            float3 normalWS = instanceData.NormalRotation.xyz;
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);

            float3 localPosition = HectonCoreLitSanitizePositionOS(input.positionOS.xyz);
            float3 normalOS = HectonCoreLitSafeNormalize(input.normalOS);
            localPosition += ResolveProceduralRockOffset(positionWS, localPosition, normalOS, scale);
            float3 swayOffsetWS = ResolveScatterSineParabolaSway(positionWS, localPosition, forwardWS, scale, rotation);
            float3 resolvedPositionWS = HectonCoreLitSanitizePositionWS(
                positionWS + rightWS * localPosition.x + upWS * localPosition.y + forwardWS * localPosition.z + swayOffsetWS);
            float3 resolvedNormalWS = ResolveScatterNormal(normalOS, rightWS, upWS, forwardWS, invScale);

            half dripAmplitude = (half)min(HectonScatterNonNegativeFinite(_StormRainDripAmplitude), 0.025);
            half dripTiling = (half)clamp(HectonScatterFiniteOr(_StormRainDripTiling, 1.0), 0.5, 16.0);
            half dripSpeed = (half)min(HectonScatterNonNegativeFinite(_StormRainDripSpeed), 8.0);
            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(resolvedPositionWS, resolvedNormalWS, dripAmplitude, dripTiling, dripSpeed);
            output.normalWS = resolvedNormalWS;
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionCS = HectonScatterFinite4(output.positionCS, float4(0.0, 0.0, UNITY_NEAR_CLIP_VALUE, 1.0));
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, min(HectonScatterNonNegativeFinite(_DepthBias), 0.01), 1.0);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
            float2 atlasUv = HectonScatterFinite2(input.uv, float2(0.0, 0.0)) * instanceData.AtlasFlow.xy + instanceData.AtlasFlow.zw;
            output.uv = TRANSFORM_TEX(atlasUv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleSurface(float2 uv)
        {
            float2 safeUv = HectonScatterFinite2(uv, float2(0.0, 0.0));
            half stochasticStrength = (half)saturate(HectonScatterFiniteOr(_StochasticTilingStrength, 0.0));
            half4 baseSample = HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), safeUv, safeUv * 0.031, stochasticStrength);
            return baseSample * (half4)HectonScatterFinite4(_BaseColor, float4(1.0, 1.0, 1.0, 1.0));
        }

        half4 SamplePackedMask(float2 uv)
        {
            float2 safeUv = HectonScatterFinite2(uv, float2(0.0, 0.0));
            half stochasticStrength = (half)saturate(HectonScatterFiniteOr(_StochasticTilingStrength, 0.0));
            return HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), safeUv, safeUv * 0.031, stochasticStrength);
        }

        half3 EvaluateLighting(
            float3 positionWS,
            float4 positionCS,
            half3 normalWS,
            half3 viewDirWS,
            half3 albedo,
            half metallic,
            half smoothness,
            half ambientOcclusion)
        {
            positionWS = HectonCoreLitSanitizePositionWS(positionWS);
            positionCS = HectonScatterFinite4(positionCS, float4(0.0, 0.0, UNITY_NEAR_CLIP_VALUE, 1.0));
            normalWS = (half3)HectonCoreLitSafeNormalize(normalWS);
            viewDirWS = (half3)HectonCoreLitSafeNormalize(viewDirWS);
            albedo = all(isfinite(albedo)) ? albedo : half3(0.0h, 0.0h, 0.0h);
            metallic = (half)saturate(HectonScatterFiniteOr(metallic, 0.0));
            smoothness = (half)saturate(HectonScatterFiniteOr(smoothness, 0.0));
            ambientOcclusion = (half)saturate(HectonScatterFiniteOr(ambientOcclusion, 1.0));
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            caveAmbientFactor = (half)saturate(HectonScatterFiniteOr(caveAmbientFactor, 1.0));
            half3 color = H8CustomLightProbeResolveAmbient(positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * albedo * ambientOcclusion * caveAmbientFactor;
            color = all(isfinite(color)) ? color : half3(0.0h, 0.0h, 0.0h);
            half specularStrength = lerp(0.04h, 0.18h, metallic);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (nDotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                if (specularBase > 0.0001h)
                {
                    half specular2 = specularBase * specularBase;
                    half specular4 = specular2 * specular2;
                    half specular8 = specular4 * specular4;
                    half specular16 = specular8 * specular8;
                    half specular32 = specular16 * specular16;
                    half specular64 = specular32 * specular32;
                    specular = lerp(specular16, specular64, smoothness) * specularEnergy;
                }
            }
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            half3 mainLightColor = all(isfinite(mainLight.color)) ? mainLight.color : half3(0.0h, 0.0h, 0.0h);
            half distanceAttenuation = (half)saturate(HectonScatterFiniteOr(mainLight.distanceAttenuation, 0.0));
            half shadowFactor = (half)saturate(HectonScatterFiniteOr(mainShadow * contactShadow, 0.0));
            color += (albedo * nDotL + specular) * mainLightColor * (distanceAttenuation * shadowFactor);
            half3 caustics = (half3)HectonScatterFinite3(HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS), float3(0.0, 0.0, 0.0));
            color += caustics * albedo;
            return all(isfinite(color)) ? color : half3(0.0h, 0.0h, 0.0h);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 surface = SampleSurface(input.uv);
            half4 packedMask = SamplePackedMask(input.uv);
            surface = all(isfinite(surface)) ? surface : half4(0.0h, 0.0h, 0.0h, 1.0h);
            packedMask = all(isfinite(packedMask)) ? packedMask : half4(0.0h, 1.0h, 0.0h, 0.0h);
            half metallicScale = (half)saturate(HectonScatterFiniteOr(_Metallic, 0.0));
            half occlusionStrength = (half)saturate(HectonScatterFiniteOr(_OcclusionStrength, 1.0));
            half smoothnessScale = (half)saturate(HectonScatterFiniteOr(_Smoothness, 0.35));
            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, metallicScale, occlusionStrength, smoothnessScale);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;
            half3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
            half microNormalStrength = (half)saturate(HectonScatterFiniteOr(_MicroNormalStrength, 0.0));
            half microNormalTiling = (half)clamp(HectonScatterFiniteOr(_MicroNormalTiling, 52.0), 4.0, 128.0);
            normalWS = HectonCoreLitApplyTripleDetailMicroNormals(input.positionWS, normalWS, microNormalStrength, microNormalTiling, 2.0h);
            half3 viewDirWS = HectonCoreLitSafeNormalize(input.viewDirWS);
            half3 albedo = surface.rgb;
            half environmentalWear = (half)saturate(HectonScatterFiniteOr(_EnvironmentalWear, 0.0));
            half3 rustSaltColor = (half3)saturate(HectonScatterFinite3(_RustSaltColor.rgb, float3(0.62, 0.35, 0.16)));
            HectonCoreLitApplyEnvironmentalWear(input.positionWS, normalWS, environmentalWear, rustSaltColor, albedo, metallic, smoothness);

            half3 litColor = EvaluateLighting(
                input.positionWS,
                input.positionCS,
                normalWS,
                viewDirWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 biolum = (half3)HectonScatterFinite3(HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS), float3(0.0, 0.0, 0.0)) * emissionMask * 0.2h;
            half3 emissionColor = (half3)HectonScatterFinite3(_EmissionColor.rgb, float3(0.0, 0.0, 0.0));
            half3 emission = emissionColor * emissionMask + biolum;
            emission += (half3)HectonScatterFinite3(HectonCoreLitEvaluateActiveSonarGeoEmission(input.positionWS), float3(0.0, 0.0, 0.0));
            half safeFogFactor = (half)saturate(HectonScatterFiniteOr(input.fogFactor, 0.0));
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, safeFogFactor, input.positionWS);
            finalColor = all(isfinite(finalColor)) ? finalColor : half3(0.0h, 0.0h, 0.0h);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input, uint instanceID)
        {
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = HectonScatterPositiveFinite(instanceData.PositionScale.w, 1.0);
            float invScale = rcp(max(scale, 0.0001));
            float3 normalWS = instanceData.NormalRotation.xyz;
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);
            float3 localPosition = HectonCoreLitSanitizePositionOS(input.positionOS.xyz);
            float3 normalOS = HectonCoreLitSafeNormalize(input.normalOS);
            localPosition += ResolveProceduralRockOffset(positionWS, localPosition, normalOS, scale);
            float3 swayOffsetWS = ResolveScatterSineParabolaSway(positionWS, localPosition, forwardWS, scale, rotation);
            float3 resolvedPositionWS = positionWS + rightWS * localPosition.x + upWS * localPosition.y + forwardWS * localPosition.z + swayOffsetWS;
            resolvedPositionWS = HectonCoreLitSanitizePositionWS(resolvedPositionWS);
            float3 resolvedNormalWS = ResolveScatterNormal(normalOS, rightWS, upWS, forwardWS, invScale);
            half dripAmplitude = (half)min(HectonScatterNonNegativeFinite(_StormRainDripAmplitude), 0.025);
            half dripTiling = (half)clamp(HectonScatterFiniteOr(_StormRainDripTiling, 1.0), 0.5, 16.0);
            half dripSpeed = (half)min(HectonScatterNonNegativeFinite(_StormRainDripSpeed), 8.0);
            resolvedPositionWS = HectonCoreLitApplyStormRainDripVertexRipple(resolvedPositionWS, resolvedNormalWS, dripAmplitude, dripTiling, dripSpeed);

            float3 lightDirectionWS = HectonCoreLitSafeNormalize(_MainLightPosition.xyz);
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(resolvedPositionWS, resolvedNormalWS, lightDirectionWS));
            positionCS = HectonScatterFinite4(positionCS, float4(0.0, 0.0, UNITY_NEAR_CLIP_VALUE, 1.0));
        #if UNITY_REVERSED_Z
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                output.positionCS = GetShadowPositionHClip(input, instanceID);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0.0h;
            }
            ENDHLSL
        }
    }
}

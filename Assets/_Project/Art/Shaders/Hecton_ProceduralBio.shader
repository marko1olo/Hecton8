Shader "Hecton8/Flora/ProceduralBio"
{
    Properties
    {
        [MainTexture] _AlbedoAtlas ("Albedo Atlas 2048", 2D) = "white" {}
        [Normal] _NormalAtlas ("Normal Atlas 2048", 2D) = "bump" {}
        _ORMAtlas ("ORM Atlas 2048", 2D) = "white" {}
        _MatCap ("Low Tier MatCap", 2D) = "gray" {}

        [Header(Colors)]
        [MainColor] _BaseColor ("Base Color", Color) = (0.56, 0.82, 0.62, 1)
        _RootTint ("Root Tint", Color) = (0.035, 0.16, 0.08, 1)
        _TipTint ("Tip Biolum Tint", Color) = (0.18, 0.95, 0.92, 1)
        _EmissionColor ("Emission Color", Color) = (0.18, 0.95, 0.88, 1)

        [Header(Projection)]
        _TriplanarScale ("Triplanar Scale", Range(0.01, 8)) = 0.42
        _TriplanarSharpness ("High Tier Blend Sharpness", Range(1, 8)) = 4
        _SeedOffsetScale ("Seed Offset Scale", Range(0, 8)) = 1.25
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.82

        [Header(Shading)]
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.52
        _SubsurfaceStrength ("Subsurface Fake Strength", Range(0, 2)) = 0.34
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.28
        _SmoothnessBoost ("Smoothness Boost", Range(0, 2)) = 1
        _MetallicBoost ("Metallic Boost", Range(0, 1)) = 0
        _BiomeTintStrength ("Biome Tint Strength", Range(0, 1)) = 0.32
        _EmissionStrength ("Emission Strength", Range(0, 6)) = 1.4
        _BiolumPulseSharpness ("Biolum Pulse Sharpness", Range(0.25, 8)) = 2
        _MatCapStrength ("Low Tier MatCap Strength", Range(0, 1)) = 0.58

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MATH_LOD_LOW
            #pragma shader_feature_local _QUALITY_HIGH
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RootTint;
                half4 _TipTint;
                half4 _EmissionColor;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _SeedOffsetScale;
                half _NormalScale;
                half _AmbientStrength;
                half _SubsurfaceStrength;
                half _RimStrength;
                half _SmoothnessBoost;
                half _MetallicBoost;
                half _BiomeTintStrength;
                half _EmissionStrength;
                half _BiolumPulseSharpness;
                half _MatCapStrength;
            CBUFFER_END

            TEXTURE2D(_AlbedoAtlas);
            SAMPLER(sampler_AlbedoAtlas);
            TEXTURE2D(_NormalAtlas);
            SAMPLER(sampler_NormalAtlas);
            TEXTURE2D(_ORMAtlas);
            SAMPLER(sampler_ORMAtlas);
            TEXTURE2D(_MatCap);
            SAMPLER(sampler_MatCap);

            float4 _HectonFloatingOffset;
            float4 _HectonFloraBiomeTint;
            float4 _HectonFloraBiomeTintParams;
            float4 _BiolumMasterPhase;
            float4 _BiolumIntensity;
            float4x4 _GlobalBiolumDearLieGroups;
            float4 _GlobalBiolumParams;
            float4 _GlobalBiolumClock;
            float4 _GlobalBiolumAupOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                #if !defined(_MATH_LOD_LOW)
                float3 positionWS : TEXCOORD0;
                float3 projectWS : TEXCOORD1;
                #endif
                half3 normalWS : TEXCOORD2;
                #if !defined(_MATH_LOD_LOW)
                half3 viewDirWS : TEXCOORD3;
                #endif
                half4 color : COLOR;
                half fogFactor : TEXCOORD4;
                #if !defined(_MATH_LOD_LOW)
                float seed : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonProceduralBioHash13(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float2 HectonProceduralBioHash22(float seed, float axis)
            {
                float2 p = float2(seed + axis * 17.17, seed * 1.37 + axis * 29.11);
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 19.19);
                return frac(float2((p.x + p.y) * p.x, (p.x + p.y) * p.y));
            }

            float ResolveProceduralBioInstanceSeed()
            {
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float packedSeed = abs(objectToWorld._m31);
                if (packedSeed > 0.0001 && isfinite(packedSeed))
                    return packedSeed;

                float3 objectOrigin = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);
                objectOrigin = all(isfinite(objectOrigin)) ? objectOrigin : float3(0.0, 0.0, 0.0);
                return HectonProceduralBioHash13(objectOrigin);
            }

            float3 ResolveProceduralBioProjectionPosition(float3 positionWS)
            {
                float3 explicitOffset = all(isfinite(_HectonFloatingOffset.xyz)) ? _HectonFloatingOffset.xyz : float3(0.0, 0.0, 0.0);
                float explicitWeight = saturate(dot(abs(explicitOffset), float3(1.0, 1.0, 1.0)) * 1000000.0);
                float3 projectOffset = all(isfinite(_TotalUniverseOffset.xyz)) ? _TotalUniverseOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 absolutePositionWS = positionWS + projectOffset;
                return lerp(absolutePositionWS, positionWS - explicitOffset, explicitWeight);
            }

            float3 ResolveProceduralBioBlendWeights(float3 worldNormal)
            {
                float3 blend = abs(worldNormal);
                blend *= rcp(max(dot(blend, float3(1.0, 1.0, 1.0)), 0.0001));

                #if defined(_QUALITY_HIGH)
                float sharpen = saturate(((float)_TriplanarSharpness - 1.0) * 0.14285715);
                blend = lerp(blend, blend * blend, sharpen);
                blend *= rcp(max(dot(blend, float3(1.0, 1.0, 1.0)), 0.0001));
                #endif

                return blend;
            }

            half3 HectonProceduralBioNormalizeRsqrt(half3 value)
            {
                return value * rsqrt(max(dot(value, value), 0.0001h));
            }

            float2 ResolveProceduralBioAxisUv(float3 positionWS, float seed, float axis)
            {
                float scale = max((float)_TriplanarScale, 0.0001);
                float2 seededOffset = (HectonProceduralBioHash22(seed, axis) - 0.5) * (float)_SeedOffsetScale;
                if (axis < 0.5)
                    return positionWS.zy * scale + seededOffset;
                if (axis < 1.5)
                    return positionWS.xz * scale + seededOffset;
                return positionWS.xy * scale + seededOffset;
            }

            half4 SampleProceduralBioAlbedo(float3 positionWS, float3 blend, float seed)
            {
                float2 uvX = ResolveProceduralBioAxisUv(positionWS, seed, 0.0);
                float2 uvY = ResolveProceduralBioAxisUv(positionWS, seed, 1.0);
                float2 uvZ = ResolveProceduralBioAxisUv(positionWS, seed, 2.0);
                half4 sampleX = SAMPLE_TEXTURE2D(_AlbedoAtlas, sampler_AlbedoAtlas, uvX);
                half4 sampleY = SAMPLE_TEXTURE2D(_AlbedoAtlas, sampler_AlbedoAtlas, uvY);
                half4 sampleZ = SAMPLE_TEXTURE2D(_AlbedoAtlas, sampler_AlbedoAtlas, uvZ);
                return sampleX * (half)blend.x + sampleY * (half)blend.y + sampleZ * (half)blend.z;
            }

            half4 SampleProceduralBioOrm(float3 positionWS, float3 blend, float seed)
            {
                float2 uvX = ResolveProceduralBioAxisUv(positionWS, seed + 3.1, 0.0);
                float2 uvY = ResolveProceduralBioAxisUv(positionWS, seed + 3.1, 1.0);
                float2 uvZ = ResolveProceduralBioAxisUv(positionWS, seed + 3.1, 2.0);
                half4 sampleX = SAMPLE_TEXTURE2D(_ORMAtlas, sampler_ORMAtlas, uvX);
                half4 sampleY = SAMPLE_TEXTURE2D(_ORMAtlas, sampler_ORMAtlas, uvY);
                half4 sampleZ = SAMPLE_TEXTURE2D(_ORMAtlas, sampler_ORMAtlas, uvZ);
                return sampleX * (half)blend.x + sampleY * (half)blend.y + sampleZ * (half)blend.z;
            }

            half3 SampleProceduralBioTriplanarNormal(float3 positionWS, half3 baseNormalWS, float3 blend, float seed)
            {
                float2 uvX = ResolveProceduralBioAxisUv(positionWS, seed + 7.7, 0.0);
                float2 uvY = ResolveProceduralBioAxisUv(positionWS, seed + 7.7, 1.0);
                float2 uvZ = ResolveProceduralBioAxisUv(positionWS, seed + 7.7, 2.0);

                half3 tangentX = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, uvX), _NormalScale);
                half3 tangentY = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, uvY), _NormalScale);
                half3 tangentZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalAtlas, sampler_NormalAtlas, uvZ), _NormalScale);

                half signX = baseNormalWS.x < 0.0h ? -1.0h : 1.0h;
                half signY = baseNormalWS.y < 0.0h ? -1.0h : 1.0h;
                half signZ = baseNormalWS.z < 0.0h ? -1.0h : 1.0h;

                half3 worldX = HectonProceduralBioNormalizeRsqrt(half3(tangentX.z * signX, tangentX.y, tangentX.x * signX));
                half3 worldY = HectonProceduralBioNormalizeRsqrt(half3(tangentY.x, tangentY.z * signY, tangentY.y));
                half3 worldZ = HectonProceduralBioNormalizeRsqrt(half3(tangentZ.x, tangentZ.y, tangentZ.z * signZ));
                half3 triplanarNormal = HectonProceduralBioNormalizeRsqrt(worldX * (half)blend.x + worldY * (half)blend.y + worldZ * (half)blend.z);

                half3 udn = half3(baseNormalWS.xy + triplanarNormal.xy, baseNormalWS.z * triplanarNormal.z);
                return HectonProceduralBioNormalizeRsqrt(udn);
            }

            half3 ResolveProceduralBioBiomeTint(half strength)
            {
                half3 tint = _HectonFloraBiomeTint.rgb;
                half hasTint = step(0.001h, dot(tint, half3(1.0h, 1.0h, 1.0h)));
                tint = lerp(half3(1.0h, 1.0h, 1.0h), tint, hasTint);
                half globalStrength = saturate((half)_HectonFloraBiomeTintParams.x);
                return lerp(half3(1.0h, 1.0h, 1.0h), tint, saturate(strength * globalStrength));
            }

            half4 ResolveProceduralBioGlobalBiolum(float3 positionWS)
            {
                if (!all(isfinite(positionWS)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);
                float4 safeAupOffset = all(isfinite(_GlobalBiolumAupOffset)) ? _GlobalBiolumAupOffset : float4(0.0, 0.0, 0.0, 0.0);
                float safeClock = isfinite(_GlobalBiolumClock.x) ? _GlobalBiolumClock.x : 0.0;
                int activeCount = min(max((int)floor(max(safeParams.x, 0.0)), 0), 4);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float selector = frac(abs(positionWS.x * 0.037 + positionWS.z * 0.053 + safeAupOffset.x * 0.0013 + safeAupOffset.z * 0.0017));
                int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                half strobe = saturate((half)max(safeParams.z, 0.0));
                half qualityCurve = saturate((half)max(safeParams.y, 0.0));
                qualityCurve = qualityCurve * qualityCurve * (3.0h - 2.0h * qualityCurve);
                int secondaryIndex = stateIndex + 1;
                if (secondaryIndex >= activeCount)
                    secondaryIndex = 0;
                float4 secondaryStateRaw = _GlobalBiolumDearLieGroups[secondaryIndex];
                float4 secondaryState = all(isfinite(secondaryStateRaw)) ? secondaryStateRaw : float4(0.0, 0.0, 0.0, 0.0);
                half overdrive = 0.0h;
                half godSpark = 0.0h;
                half godHaze = 0.0h;
                half overPulse = (half)(1.0 - abs(frac(safeClock * 0.07 + selector * 3.0) * 2.0 - 1.0));
                half filament = (half)(1.0 - abs(frac(positionWS.x * 0.173 + positionWS.y * 0.097 + positionWS.z * 0.131 + safeClock * 0.23) * 2.0 - 1.0));
                godHaze = smoothstep(0.42h, 0.92h, overPulse) * (0.55h + filament * 0.45h) * qualityCurve;
                godSpark = smoothstep(0.82h, 0.98h, filament) * overPulse * qualityCurve;
                overdrive = saturate(overPulse * 0.35h + godSpark * 0.22h) * qualityCurve;
                half3 color = lerp(saturate((half3)state.rgb), half3(1.0h, 1.0h, 1.0h), strobe);
                half intensity = clamp(max((half)max(state.w, 0.0), strobe * 10.0h), 0.0h, 10.0h);
                color = lerp(color, saturate((half3)secondaryState.rgb), overdrive);
                color = saturate(color + godHaze * half3(0.07h, 0.21h, 0.18h));
                intensity = clamp(intensity + (half)max(secondaryState.w, 0.0) * overdrive + godSpark * 0.65h + godHaze * 0.32h, 0.0h, 10.0h);
                return half4(color, intensity);
            }

            half3 ResolveProceduralBioEmission(float3 positionWS, half height01, half mask)
            {
                half4 globalState = ResolveProceduralBioGlobalBiolum(positionWS);
                half hasGlobal = step(0.001h, globalState.w);
                half master = max(max((half)_BiolumIntensity.x, 0.0h), globalState.w);
                half phase = saturate((half)_BiolumMasterPhase.y);
                half sharpMask = saturate((_BiolumPulseSharpness - 0.25h) * 0.12903225h);
                half pulse = lerp(phase, phase * phase, sharpMask);
                half seeded = (half)HectonProceduralBioHash13(positionWS * 0.03125 + height01);
                half organicPulse = saturate(0.55h + seeded * 0.45h + pulse * 0.35h);
                half3 emissionColor = lerp(_EmissionColor.rgb, globalState.rgb, hasGlobal);
                half emissionEnergy = clamp(_EmissionStrength * master * organicPulse * mask * height01, 0.0h, 10.0h);
                return emissionColor * emissionEnergy;
            }

            half3 ResolveProceduralBioEmissionLow(half height01, half mask)
            {
                half4 globalState = ResolveProceduralBioGlobalBiolum(float3(0.0, 0.0, 0.0));
                half hasGlobal = step(0.001h, globalState.w);
                half master = max(max((half)_BiolumIntensity.x, 0.0h), globalState.w);
                half phase = saturate((half)_BiolumMasterPhase.y);
                half pulse = lerp(phase, phase * phase, 0.25h);
                half cheapPulse = saturate(0.62h + pulse * 0.38h);
                half3 emissionColor = lerp(_EmissionColor.rgb, globalState.rgb, hasGlobal);
                half emissionEnergy = clamp(_EmissionStrength * master * cheapPulse * mask * height01, 0.0h, 10.0h);
                return emissionColor * emissionEnergy;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 normalOS = all(isfinite(input.normalOS)) ? input.normalOS : float3(0.0, 1.0, 0.0);
                normalOS = dot(normalOS, normalOS) > 0.000001 ? normalOS : float3(0.0, 1.0, 0.0);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = positionInputs.positionCS;
                #if !defined(_MATH_LOD_LOW)
                output.positionWS = positionInputs.positionWS;
                output.projectWS = ResolveProceduralBioProjectionPosition(positionInputs.positionWS);
                output.seed = ResolveProceduralBioInstanceSeed();
                output.viewDirWS = HectonProceduralBioNormalizeRsqrt(GetWorldSpaceViewDir(positionInputs.positionWS));
                #endif
                output.normalWS = HectonProceduralBioNormalizeRsqrt(normalInputs.normalWS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half3 baseNormalWS = HectonProceduralBioNormalizeRsqrt(input.normalWS);
                half height01 = saturate(input.color.r);
                half3 rootTipTint = lerp(_RootTint.rgb, _TipTint.rgb, height01);
                #if defined(_MATH_LOD_LOW)
                Light mainLight = GetMainLight();
                #else
                half3 viewDirWS = HectonProceduralBioNormalizeRsqrt(input.viewDirWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                #endif
                half mainLightAttenuation = saturate((half)(mainLight.distanceAttenuation * mainLight.shadowAttenuation));

                #if defined(_MATH_LOD_LOW)
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, (float3)baseNormalWS);
                normalVS *= rsqrt(max(dot(normalVS, normalVS), 0.0001));
                float2 matcapUv = normalVS.xy * 0.5 + 0.5;
                half3 matcap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, matcapUv).rgb;
                half wrapDiffuse = max(0.0h, dot(baseNormalWS, (half3)mainLight.direction) + 0.5h) * 0.6666667h;
                half3 albedo = lerp(_BaseColor.rgb * rootTipTint, matcap * _TipTint.rgb, _MatCapStrength);
                albedo *= ResolveProceduralBioBiomeTint(_BiomeTintStrength);
                half3 ambient = H8CustomLightProbeResolveAmbient(input.positionWS, baseNormalWS, half3(0.015h, 0.025h, 0.035h)) * _AmbientStrength;
                half3 emission = ResolveProceduralBioEmissionLow(height01, 0.65h);
                half3 color = albedo * (ambient + mainLight.color * (wrapDiffuse * mainLightAttenuation)) + emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
                #else
                float3 blend = ResolveProceduralBioBlendWeights((float3)baseNormalWS);
                half4 albedoSample = SampleProceduralBioAlbedo(input.projectWS, blend, input.seed);
                half4 ormSample = SampleProceduralBioOrm(input.projectWS, blend, input.seed);
                half3 normalWS = SampleProceduralBioTriplanarNormal(input.projectWS, baseNormalWS, blend, input.seed);

                half occlusion = saturate(ormSample.r);
                half roughness = saturate(ormSample.g);
                half metallic = saturate(ormSample.b * _MetallicBoost);
                half emissionMask = saturate(ormSample.a);
                half smoothness = saturate((1.0h - roughness) * _SmoothnessBoost);

                half3 albedo = albedoSample.rgb * _BaseColor.rgb * rootTipTint;
                albedo *= ResolveProceduralBioBiomeTint(_BiomeTintStrength);
                albedo = lerp(albedo * 0.72h, albedo, height01);

                half3 lightDir = (half3)mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir));
                half wrapDiffuse = max(0.0h, dot(normalWS, lightDir) + 0.45h) * 0.6896552h;
                half3 halfDir = HectonProceduralBioNormalizeRsqrt(lightDir + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specularLobe = NdotH * NdotH;
                specularLobe *= specularLobe;
                specularLobe *= lerp(0.08h, 1.0h, smoothness);

                half3 ambient = H8CustomLightProbeResolveAmbient(input.positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * (_AmbientStrength * occlusion);
                half3 diffuse = albedo * (1.0h - metallic) * (ambient + mainLight.color * (wrapDiffuse * mainLightAttenuation));
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, metallic);
                half3 specular = f0 * specularLobe * mainLight.color * mainLightAttenuation;

                half rim = (half)HectonCoreLitFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), 3.0) * _RimStrength;
                half3 subsurface = _TipTint.rgb * ((1.0h - NdotL) * _SubsurfaceStrength * height01 * mainLightAttenuation);
                half3 emission = ResolveProceduralBioEmission(input.projectWS, height01, emissionMask);

                half3 color = diffuse + specular + subsurface + _TipTint.rgb * rim + emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, albedoSample.a);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 normalOS = all(isfinite(input.normalOS)) ? input.normalOS : float3(0.0, 1.0, 0.0);
                normalOS = dot(normalOS, normalOS) > 0.000001 ? normalOS : float3(0.0, 1.0, 0.0);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

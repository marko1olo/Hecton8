Shader "Hidden/Hecton8/VisorUberPost"
{
    Properties
    {
        _HectonVisorCrackTex ("Packed Crack Normal Alpha", 2D) = "black" {}
        _HectonLensDirtTex ("Lens Dirt", 2D) = "white" {}
        _HectonBlueNoiseTex ("Blue Noise", 2D) = "gray" {}
        _HectonVRComfortMaskTex ("VR Comfort Low Tier Mask", 2D) = "gray" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "VisorUberPost"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if !defined(SHADER_API_MOBILE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float _HectonUberHealthFraction;
                float _HectonUberLocalTemperature;
                float _HectonUberAmbientPressure;
                float _HectonUberPlayerStress01;
                float _HectonUberHypoxia01;
                float _HectonUberBleeding01;
                float _HectonUberWetLens01;
                float _HectonUberHullStress01;
                float _HectonUberAupShiftFrame;
                float _HectonUberLowTier;
                float _HectonUberDepthlessTBDR;
                float _VRComfortVignette01;
                float4 _HectonUberStrengths0;
                float4 _HectonUberStrengths1;
                float4 _HectonUberWaveParams;
                float4 _HectonUberTextureFlags;
                float4 _HectonVRComfortJerkState;
                float4 _InternalWaterlineParams;
                float4 _InternalWaterlineDistortion;
            CBUFFER_END

            float _BrineHeightY;
            float4 _BrineColor;
            float _BrineFogHardClip;
            float _InternalWaterlineY;
            float4 _InternalWaterColor;

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            TEXTURE2D(_HectonVisorCrackTex);
            SAMPLER(sampler_HectonVisorCrackTex);
            TEXTURE2D(_HectonLensDirtTex);
            SAMPLER(sampler_HectonLensDirtTex);
            TEXTURE2D(_HectonBlueNoiseTex);
            SAMPLER(sampler_HectonBlueNoiseTex);
            TEXTURE2D(_HectonVRComfortMaskTex);
            SAMPLER(sampler_HectonVRComfortMaskTex);

            float4 _HectonLightShaftParams;
            float4 _HectonLightShaftQuality;
            float4 _HectonLightShaftSource0;
            float4 _HectonLightShaftSource1;
            float4 _HectonLightShaftSource2;
            float4 _HectonLightShaftColor0;
            float4 _HectonLightShaftColor1;
            float4 _HectonLightShaftColor2;
            float _HectonAtmosphereSoot;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
            #else
                return screenUV;
            #endif
            }

            float InterleavedGradientNoise(float2 uv, float frameSalt)
            {
                float2 pixel = floor(uv * _ScreenParams.xy);
                pixel += float2(frameSalt, frameSalt);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float CheapSignedTriangle(float value)
            {
                return abs(frac(value) * 2.0 - 1.0) * 2.0 - 1.0;
            }

            float FastEdge01(float2 uv)
            {
                float2 centered = uv - 0.5;
                return saturate(dot(centered, centered) * 4.0);
            }

            float2 BarrelWarp(float2 uv, float pressure01, float strength)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radiusSq = dot(centered, centered);
                float barrel = pressure01 * strength;
                centered *= 1.0 + radiusSq * barrel;
                return saturate(centered * 0.5 + 0.5);
            }

            float2 HeatHazeOffset(float2 uv, float heat01, float lowTier)
            {
                float enabled = 1.0 - step(0.5, lowTier);
                float freq = max(1.0, _HectonUberWaveParams.x);
                float speed = _HectonUberWaveParams.y;
                float amplitude = _HectonUberWaveParams.z * heat01 * enabled;
                float2 wave;
                wave.x = sin(uv.y * freq + _Time.y * speed);
                wave.y = sin(uv.x * freq * 0.73 - _Time.y * speed * 0.71);
                return wave * amplitude;
            }

            float ResolveInternalWaterMask(float2 uv)
            {
                float active = saturate(_InternalWaterlineParams.y);
                float softness = max(0.001, _InternalWaterlineDistortion.z);
            #if defined(SHADER_API_MOBILE)
            #if UNITY_REVERSED_Z
                float farDepth = 0.0;
            #else
                float farDepth = 1.0;
            #endif
                float3 farWorld = ComputeWorldSpacePosition(uv, farDepth, UNITY_MATRIX_I_VP);
                float3 cameraPosition = _WorldSpaceCameraPos.xyz;
                float3 cameraRay = farWorld - cameraPosition;
                float yDelta = _InternalWaterlineY - cameraPosition.y;
                float cameraSubmerged = step(cameraPosition.y, _InternalWaterlineY - 0.03);
                float planeInFront = smoothstep(-softness, softness, yDelta * cameraRay.y);
                return active * saturate(max(cameraSubmerged, planeInFront));
            #else
                float splitLine = _InternalWaterlineParams.x;
                return active * saturate(1.0 - smoothstep(splitLine - softness, splitLine + softness, uv.y));
            #endif
            }

            float2 InternalWaterOffset(float2 uv, float mask)
            {
                float strength = max(0.0, _InternalWaterlineDistortion.x) * mask;
                float droplets01 = saturate(_InternalWaterlineParams.w);
                float2 wave;
                wave.x = CheapSignedTriangle(uv.y * 7.31 + _Time.y * 0.28) + CheapSignedTriangle((uv.x + uv.y) * 3.67 - _Time.y * 0.15);
                wave.y = CheapSignedTriangle(uv.x * 6.21 - _Time.y * 0.21) + CheapSignedTriangle((uv.x - uv.y) * 2.71 + _Time.y * 0.12);
                return wave * strength * (0.55 + droplets01 * 0.65);
            }

            float ResolveInternalDropletMask(float2 uv, float droplets01)
            {
                float2 cell = floor(uv * float2(78.0, 44.0));
                float seed = Hash21(cell);
                float density = step(0.78, Hash21(cell + float2(19.17, 5.31)));
                float streak = 1.0 - smoothstep(0.03, 0.18, abs(frac(uv.y * 44.0 + seed * 6.17 + (1.0 - droplets01) * 0.92) - 0.5));
                return density * streak * saturate(droplets01 * 1.35 - seed * 0.28);
            }

            float2 InternalDropletOffset(float2 uv, float mask)
            {
                float2 offset;
                offset.x = CheapSignedTriangle(uv.y * 19.0 + _Time.y * 0.11) * 0.0011;
                offset.y = -abs(CheapSignedTriangle(uv.x * 11.0 + _Time.y * 0.07)) * 0.0014;
                return offset * mask;
            }

            half3 ApplySingleSampleChroma(half3 color, float edge01, float damageDrive, float strength)
            {
                float drive = saturate(edge01 * damageDrive * strength);
                half heat = (half)drive;
                half3 shifted;
                shifted.r = color.r + heat * (0.035h + color.r * 0.045h);
                shifted.g = color.g * (1.0h - heat * 0.025h);
                shifted.b = color.b * (1.0h - heat * 0.075h) + heat * 0.018h;
                return shifted;
            }

            void ResolveProceduralCracks(float2 uv, float damage01, out float crackReveal, out float2 crackNormal)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float2 cell = floor(uv * 11.0);
                float seed = Hash21(cell);
                float primary = abs(centered.x * 0.72 + centered.y * 0.31 + (seed - 0.5) * 0.13);
                float branch = abs(centered.x * -0.27 + centered.y * 0.86 + sin((centered.x + seed) * 9.0) * 0.025);
                float primaryVein = 1.0 - smoothstep(0.008, 0.035, primary);
                float branchVein = 1.0 - smoothstep(0.004, 0.019, branch);
                float vein = saturate(max(primaryVein, branchVein * 0.62) * smoothstep(0.08, 0.96, radial));
                float threshold = lerp(1.15, 0.18 + seed * 0.54, vein);
                crackReveal = step(threshold, damage01) * vein;
                float2 gradient = float2(primaryVein - branchVein, branchVein - primaryVein * 0.38);
                float2 normalSeed = gradient + centered * (0.15 + seed * 0.1);
                crackNormal = normalSeed * rsqrt(max(dot(normalSeed, normalSeed), 0.0001));
            }

            float ResolveDitherNoise(float2 uv, float shiftSalt)
            {
                float2 salt2 = float2(shiftSalt, shiftSalt);
                float proceduralNoise = frac(InterleavedGradientNoise(uv, shiftSalt) + Hash21(floor(uv * _ScreenParams.xy * 0.25) + salt2) * 0.5);
                [branch]
                if (_HectonUberTextureFlags.z > 0.5)
                {
                    float textureNoise = SAMPLE_TEXTURE2D(_HectonBlueNoiseTex, sampler_HectonBlueNoiseTex, uv * _ScreenParams.xy * 0.00390625 + salt2).r;
                    return frac(textureNoise + proceduralNoise * 0.5);
                }

                return proceduralNoise;
            }

            float ResolveComfortShaftMask(float2 uv, float edge01)
            {
                float comfortVignette01 = saturate(max(_VRComfortVignette01, _HectonVRComfortJerkState.x * _HectonVRComfortJerkState.w));
                float comfortEdgeProcedural = smoothstep(0.16, 1.0, edge01);
                float comfortEdgeLowTier = step(0.42, edge01);
                float comfortLowTier01 = step(0.5, _HectonUberLowTier);
                [branch]
                if (comfortLowTier01 > 0.5 && _HectonUberTextureFlags.w > 0.5)
                    comfortEdgeLowTier = SAMPLE_TEXTURE2D(_HectonVRComfortMaskTex, sampler_HectonVRComfortMaskTex, uv).r;

                float comfortEdge = lerp(comfortEdgeProcedural, comfortEdgeLowTier, comfortLowTier01);
                return 1.0 - saturate(comfortEdge * comfortVignette01 * 0.92);
            }

            #if !defined(SHADER_API_MOBILE)
            half3 AccumulateLightShaftSource(float2 uv, float centerEyeDepth, float4 source, float4 sourceColor, float sampleBudget)
            {
                float intensity = max(0.0, source.z);
                [branch]
                if (intensity <= 0.0001)
                    return half3(0.0h, 0.0h, 0.0h);

                float2 lightUv = saturate(source.xy);
                float2 toLight = lightUv - uv;
                float distanceToLight = length(toLight);
                [branch]
                if (distanceToLight <= 0.0001)
                    return half3(0.0h, 0.0h, 0.0h);

                float taps = clamp(sampleBudget, 1.0, 16.0);
                float2 stepUv = toLight * rcp(taps);
                float emissionThreshold = max(0.01, _HectonLightShaftQuality.x);
                float depthBias = max(0.001, _HectonLightShaftQuality.z);
                float radialFalloff = max(0.35, source.w);
                half3 colorSum = half3(0.0h, 0.0h, 0.0h);
                float weightSum = 0.0;

                [unroll(16)]
                for (int i = 0; i < 16; i++)
                {
                    float tapMask = step((float)i + 0.5, taps);
                    float tapIndex = (float)i + 0.5;
                    float2 sampleUv = saturate(uv + stepUv * tapIndex);
                    half3 sampleColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUv).rgb;
                    float sampleLuma = dot(sampleColor, half3(0.2126h, 0.7152h, 0.0722h));
                    float emissionMask = saturate((sampleLuma - emissionThreshold) * 2.0);
                    float rawDepth = SampleSceneDepth(sampleUv);
                    float sampleEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float occlusion = lerp(0.12, 1.0, step(centerEyeDepth - depthBias, sampleEyeDepth));
                    float radial01 = saturate(1.0 - (tapIndex * rcp(taps)) * 0.92);
                    float weight = tapMask * emissionMask * occlusion * pow(radial01, radialFalloff);
                    colorSum += sampleColor * (half)weight;
                    weightSum += weight;
                }

                half invWeight = (half)rcp(max(weightSum, 0.0001));
                half3 shaftTint = half3((half)sourceColor.x, (half)sourceColor.y, (half)sourceColor.z);
                return colorSum * invWeight * shaftTint * (half)intensity;
            }
            #endif

            half3 ResolveLightShafts(float2 uv, float edge01)
            {
            #if defined(SHADER_API_MOBILE)
                return half3(0.0h, 0.0h, 0.0h);
            #else
                [branch]
                if (_HectonUberDepthlessTBDR > 0.5)
                    return half3(0.0h, 0.0h, 0.0h);

                float activeCount = _HectonLightShaftParams.x;
                [branch]
                if (activeCount <= 0.5)
                    return half3(0.0h, 0.0h, 0.0h);

                float rawDepth = SampleSceneDepth(uv);
                float centerEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float sampleBudget = clamp(_HectonLightShaftQuality.y, 1.0, 16.0);
                float comfortMask = ResolveComfortShaftMask(uv, edge01);
                float sootBoost = lerp(0.35, 1.65, saturate(_HectonAtmosphereSoot));
                float globalIntensity = max(0.0, _HectonLightShaftParams.y) * sootBoost * comfortMask;

                half3 shafts = half3(0.0h, 0.0h, 0.0h);
                shafts += AccumulateLightShaftSource(uv, centerEyeDepth, _HectonLightShaftSource0, _HectonLightShaftColor0, sampleBudget) * (half)step(0.5, activeCount);
                shafts += AccumulateLightShaftSource(uv, centerEyeDepth, _HectonLightShaftSource1, _HectonLightShaftColor1, sampleBudget) * (half)step(1.5, activeCount);
                shafts += AccumulateLightShaftSource(uv, centerEyeDepth, _HectonLightShaftSource2, _HectonLightShaftColor2, sampleBudget) * (half)step(2.5, activeCount);
                return shafts * (half)globalIntensity;
            #endif
            }

            half3 ResolveLensDirt(float2 uv, float edge01)
            {
                [branch]
                if (_HectonUberTextureFlags.y > 0.5)
                    return SAMPLE_TEXTURE2D(_HectonLensDirtTex, sampler_HectonLensDirtTex, uv).rgb;

                float2 cell = floor(uv * 18.0);
                float grain = Hash21(cell);
                float streak = 1.0 - smoothstep(0.08, 0.34, abs(frac(uv.x * 7.0 + uv.y * 1.7 + grain) - 0.5));
                float grime = saturate(edge01 * 0.55 + streak * 0.22 + grain * 0.16);
                return half3(
                    (half)(1.0 - grime * 0.34),
                    (half)(1.0 - grime * 0.24),
                    (half)(1.0 - grime * 0.18));
            }

            half3 ApplyBrinePlaneFog(half3 color, float2 uv, float lowTier)
            {
            #if defined(SHADER_API_MOBILE)
                return color;
            #else
                [branch]
                if (_HectonUberDepthlessTBDR > 0.5)
                    return color;

                float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                float depthValid = step(0.000001, rawDepth);
            #else
                float depthValid = step(rawDepth, 0.999999);
            #endif
                float3 worldPosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float distanceBelowPlane = max(0.0, _BrineHeightY - worldPosition.y);
                float belowPlane = depthValid * step(worldPosition.y, _BrineHeightY) * step(0.0001, _BrineColor.a);
                float softFog = saturate(distanceBelowPlane * 0.20);
                float hardFog = step(0.0001, distanceBelowPlane);
                float fogMode = saturate(max(lowTier, _BrineFogHardClip));
                float fog = belowPlane * lerp(softFog, hardFog, fogMode) * saturate(_BrineColor.a);
                return lerp(color, (half3)_BrineColor.rgb, (half)fog);
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ResolveXRStereoScreenUV(input.screenUV);
                float health01 = saturate(_HectonUberHealthFraction);
                float damage01 = saturate(1.0 - health01);
                float edge01 = FastEdge01(uv);
                float pressure01 = saturate((_HectonUberAmbientPressure - 1.0) * _HectonUberStrengths1.x);
                float heat01 = saturate(abs(_HectonUberLocalTemperature) * _HectonUberStrengths1.y);
                float stress01 = saturate(_HectonUberPlayerStress01);
                float hypoxia01 = saturate(_HectonUberHypoxia01);

                float crackReveal;
                float2 crackNormal;
                [branch]
                if (_HectonUberTextureFlags.x > 0.5)
                {
                    float4 crackSample = SAMPLE_TEXTURE2D(_HectonVisorCrackTex, sampler_HectonVisorCrackTex, uv);
                    crackReveal = step(crackSample.a, damage01);
                    crackNormal = crackSample.rg * 2.0 - 1.0;
                }
                else
                {
                    ResolveProceduralCracks(uv, damage01, crackReveal, crackNormal);
                }

                float crackMask = crackReveal * saturate(_HectonUberStrengths0.w);

                float2 warpedUV = BarrelWarp(uv, pressure01, _HectonUberStrengths0.z);
                warpedUV += HeatHazeOffset(uv, heat01, _HectonUberLowTier);
                warpedUV += crackNormal * (crackMask * _HectonUberStrengths1.z);
                warpedUV = saturate(warpedUV);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUV);
                float internalWaterMask = ResolveInternalWaterMask(uv);
                [branch]
                if (internalWaterMask > 0.001)
                {
                    float2 waterUV = saturate(warpedUV + InternalWaterOffset(uv, internalWaterMask));
                    float refractionBlend = saturate(internalWaterMask * step(0.00001, _InternalWaterlineDistortion.x));
                    [branch]
                    if (refractionBlend > 0.001)
                    {
                        half4 refractedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, waterUV);
                        color = lerp(color, refractedColor, (half)refractionBlend);
                    }

                    half3 waterTint = lerp(color.rgb, (half3)_InternalWaterColor.rgb, (half)saturate(_InternalWaterlineDistortion.y));
                    color.rgb = lerp(color.rgb, waterTint, (half)(internalWaterMask * _InternalWaterColor.a));
                }
                float droplets01 = saturate(_InternalWaterlineParams.w);
                [branch]
                if (droplets01 > 0.001)
                {
                    float dropletMask = ResolveInternalDropletMask(uv, droplets01) * (0.35 + edge01 * 0.65);
                    float highTierDropletRefraction = (1.0 - step(0.5, _InternalWaterlineDistortion.w)) * step(0.00001, _InternalWaterlineDistortion.x);
                    float dropletRefractBlend = saturate(dropletMask * highTierDropletRefraction);
                    [branch]
                    if (dropletRefractBlend > 0.001)
                    {
                        float2 dropletUV = saturate(warpedUV + InternalDropletOffset(uv, dropletRefractBlend));
                        half4 dropletColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, dropletUV);
                        color = lerp(color, dropletColor, (half)(dropletRefractBlend * 0.65));
                    }

                    color.rgb += (half3(0.09h, 0.13h, 0.14h) * (half)(dropletMask * droplets01));
                }
                color.rgb += ResolveLightShafts(uv, edge01);

                float damageDrive = saturate(max(damage01, max(_HectonUberHullStress01, stress01)) + crackMask * 0.35);
                color.rgb = ApplySingleSampleChroma(color.rgb, edge01, damageDrive, _HectonUberStrengths0.x);
                color.rgb = ApplyBrinePlaneFog(color.rgb, uv, _HectonUberLowTier);

                float shiftSalt = frac(_HectonUberAupShiftFrame * 0.6180339887);
                float blueNoise = ResolveDitherNoise(uv, shiftSalt);
                half3 dirt = ResolveLensDirt(uv, edge01);
                float dirtDrive = saturate(_HectonUberStrengths1.w * (0.18 + edge01 * 0.82 + _HectonUberWetLens01 * 0.35));
                float dirtMask = step(blueNoise, dirtDrive);
                color.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), dirt, (half)dirtMask);

                half crackDarken = (half)(crackMask * (0.22 + edge01 * 0.18));
                color.rgb *= 1.0h - crackDarken;
                color.rgb += (half3(0.16h, 0.22h, 0.24h) * (half)(crackMask * 0.035));

                half luma = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 hypoxiaLuma = half3(luma, luma, luma);
                color.rgb = lerp(color.rgb, hypoxiaLuma * half3(0.78h, 0.91h, 1.05h), (half)(hypoxia01 * _HectonUberStrengths0.y));

                float cold01 = saturate(-_HectonUberLocalTemperature * _HectonUberStrengths1.y);
                float frostNoise = ResolveDitherNoise(uv * 1.73 + crackNormal * 0.013, shiftSalt + 11.17);
                float frostDrive = saturate(cold01 * (0.18 + edge01 * 0.92 + crackMask * 0.34));
                float frostMask = step(frostNoise, frostDrive);
                half3 frostTint = lerp(color.rgb, max(color.rgb, half3(0.64h, 0.82h, 0.94h)), (half)(0.45 * cold01));
                color.rgb = lerp(color.rgb, frostTint, (half)(frostMask * frostDrive));

                float comfortVignette01 = saturate(max(_VRComfortVignette01, _HectonVRComfortJerkState.x * _HectonVRComfortJerkState.w));
                float comfortEdgeProcedural = smoothstep(0.16, 1.0, edge01);
                float comfortEdgeLowTier = step(0.42, edge01);
                float comfortLowTier01 = step(0.5, _HectonUberLowTier);
                [branch]
                if (comfortLowTier01 > 0.5 && _HectonUberTextureFlags.w > 0.5)
                    comfortEdgeLowTier = SAMPLE_TEXTURE2D(_HectonVRComfortMaskTex, sampler_HectonVRComfortMaskTex, uv).r;
                float comfortEdge = lerp(comfortEdgeProcedural, comfortEdgeLowTier, comfortLowTier01);
                float vignette = saturate(
                    edge01 * stress01 * _HectonUberStrengths1.w +
                    edge01 * damageDrive * _HectonUberWaveParams.w +
                    comfortEdge * comfortVignette01 * 0.92);
                color.rgb *= 1.0h - (half)vignette;

                float bleeding = saturate(_HectonUberBleeding01);
                half bloodEdge = (half)(bleeding * edge01 * _HectonUberStrengths1.w);
                color.rgb = lerp(color.rgb, half3(0.48h, 0.015h, 0.012h), bloodEdge);
                color.rgb = max(color.rgb, half3(0.0015h, 0.0022h, 0.0030h));
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

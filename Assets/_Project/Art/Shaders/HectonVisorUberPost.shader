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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if !defined(SHADER_API_MOBILE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif
            #include "Assets/_Project/Art/Shaders/Post/Hecton_SnellRefractionCore.hlsl"

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
            float _HectonUberVisualTime;
            float _HectonUberDepthlessTBDR;
            float _VRComfortVignette01;
            float4 _HectonUberStrengths0;
            float4 _HectonUberStrengths1;
            float4 _HectonUberWaveParams;
            float4 _HectonUberTextureFlags;
            float4 _HectonVRComfortJerkState;
            float4 _InternalWaterlineParams;
            float4 _InternalWaterlineDistortion;

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
                float2 screenParams = max(HectonFinite4(_ScreenParams, float4(1.0, 1.0, 1.0, 1.0)).xy, float2(1.0, 1.0));
                float safeSalt = HectonFiniteValue(frameSalt, 0.0);
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float2 pixel = floor(safeUv * screenParams);
                pixel += float2(safeSalt, safeSalt);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float Hash21(float2 p)
            {
                p = all(isfinite(p)) ? p : float2(0.0, 0.0);
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float CheapSignedTriangle(float value)
            {
                value = HectonFiniteValue(value, 0.0);
                return abs(frac(value) * 2.0 - 1.0) * 2.0 - 1.0;
            }

            float FastEdge01(float2 uv)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float2 centered = safeUv - 0.5;
                return saturate(dot(centered, centered) * 4.0);
            }

            float2 BarrelWarp(float2 uv, float pressure01, float strength)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float2 centered = safeUv * 2.0 - 1.0;
                float radiusSq = dot(centered, centered);
                float barrel = HectonFinite01(pressure01) * HectonFiniteValue(strength, 0.0);
                centered *= 1.0 + radiusSq * barrel;
                return saturate(centered * 0.5 + 0.5);
            }

            float2 HeatHazeOffset(float2 uv, float heat01, float lowTier)
            {
                float4 waveParams = HectonFinite4(_HectonUberWaveParams, float4(1.0, 0.0, 0.0, 0.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float enabled = 1.0 - smoothstep(0.35, 0.95, HectonFinite01(lowTier));
                float freq = max(1.0, waveParams.x);
                float speed = waveParams.y;
                float amplitude = HectonFiniteValue(waveParams.z, 0.0) * HectonFinite01(heat01) * enabled;
                float2 wave;
                float visualTime = HectonFiniteValue(_HectonUberVisualTime, 0.0);
                wave.x = sin(safeUv.y * freq + visualTime * speed);
                wave.y = sin(safeUv.x * freq * 0.73 - visualTime * speed * 0.71);
                return HectonClampUvOffset(wave * amplitude, 0.1);
            }

            float ResolveInternalWaterMask(float2 uv)
            {
                float4 waterlineParams = HectonFinite4(_InternalWaterlineParams, float4(0.5, 0.0, 0.0, 0.0));
                float4 waterlineDistortion = HectonFinite4(_InternalWaterlineDistortion, float4(0.0, 0.0, 0.01, 1.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float active = HectonFinite01(waterlineParams.y);
                float softness = max(0.001, abs(waterlineDistortion.z));
            #if defined(SHADER_API_MOBILE)
                float farDepth = HectonInvalidSceneRawDepth();
                float3 farWorld = ComputeWorldSpacePosition(safeUv, farDepth, UNITY_MATRIX_I_VP);
                farWorld = HectonFinite3(farWorld, float3(0.0, 0.0, 1.0));
                float3 cameraPosition = HectonFinite3(_WorldSpaceCameraPos.xyz, float3(0.0, 0.0, 0.0));
                float3 cameraRay = farWorld - cameraPosition;
                float waterlineY = HectonFiniteValue(_InternalWaterlineY, cameraPosition.y - 1.0);
                float yDelta = waterlineY - cameraPosition.y;
                float cameraSubmerged = smoothstep(-softness, softness, (waterlineY - 0.03) - cameraPosition.y);
                float planeInFront = smoothstep(-softness, softness, yDelta * cameraRay.y);
                return active * saturate(max(cameraSubmerged, planeInFront));
            #else
                float splitLine = HectonFinite01(waterlineParams.x);
                return active * saturate(1.0 - smoothstep(splitLine - softness, splitLine + softness, safeUv.y));
            #endif
            }

            float2 InternalWaterOffset(float2 uv, float mask)
            {
                float4 waterlineParams = HectonFinite4(_InternalWaterlineParams, float4(0.5, 0.0, 0.0, 0.0));
                float4 waterlineDistortion = HectonFinite4(_InternalWaterlineDistortion, float4(0.0, 0.0, 0.01, 1.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float strength = HectonFiniteNonNegative(waterlineDistortion.x, 0.0) * HectonFinite01(mask);
                float droplets01 = HectonFinite01(waterlineParams.w);
                float2 wave;
                float visualTime = HectonFiniteValue(_HectonUberVisualTime, 0.0);
                wave.x = CheapSignedTriangle(safeUv.y * 7.31 + visualTime * 0.28) + CheapSignedTriangle((safeUv.x + safeUv.y) * 3.67 - visualTime * 0.15);
                wave.y = CheapSignedTriangle(safeUv.x * 6.21 - visualTime * 0.21) + CheapSignedTriangle((safeUv.x - safeUv.y) * 2.71 + visualTime * 0.12);
                return HectonClampUvOffset(wave * strength * (0.55 + droplets01 * 0.65), 0.1);
            }

            float ResolveInternalDropletMask(float2 uv, float droplets01)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float safeDroplets01 = HectonFinite01(droplets01);
                float2 cell = floor(safeUv * float2(78.0, 44.0));
                float seed = Hash21(cell);
                float density = step(0.78, Hash21(cell + float2(19.17, 5.31)));
                float streak = 1.0 - smoothstep(0.03, 0.18, abs(frac(safeUv.y * 44.0 + seed * 6.17 + (1.0 - safeDroplets01) * 0.92) - 0.5));
                return density * streak * HectonFinite01(safeDroplets01 * 1.35 - seed * 0.28);
            }

            float2 InternalDropletOffset(float2 uv, float mask)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float safeMask = HectonFinite01(mask);
                float2 offset;
                float visualTime = HectonFiniteValue(_HectonUberVisualTime, 0.0);
                offset.x = CheapSignedTriangle(safeUv.y * 19.0 + visualTime * 0.11) * 0.0011;
                offset.y = -abs(CheapSignedTriangle(safeUv.x * 11.0 + visualTime * 0.07)) * 0.0014;
                return HectonClampUvOffset(offset * safeMask, 0.1);
            }

            half3 ApplySingleSampleChroma(half3 color, float edge01, float damageDrive, float strength)
            {
                float drive = HectonFinite01(edge01) * HectonFinite01(damageDrive) * HectonFiniteNonNegative(strength, 0.0);
                drive = saturate(drive);
                half heat = (half)drive;
                half3 shifted;
                shifted.r = color.r + heat * (0.035h + color.r * 0.045h);
                shifted.g = color.g * (1.0h - heat * 0.025h);
                shifted.b = color.b * (1.0h - heat * 0.075h) + heat * 0.018h;
                return shifted;
            }

            float FastRadialFalloff01(float value, float exponent)
            {
                float v = HectonFinite01(value);
                float e = max(0.35, HectonFiniteValue(exponent, 1.0));
                float v2 = v * v;
                float v4 = v2 * v2;
                float v8 = v4 * v4;
                float low = lerp(v, v2, saturate(e - 1.0));
                float high = lerp(v2, v8, saturate((e - 2.0) * 0.16666667));
                return lerp(low, high, smoothstep(1.85, 2.15, e));
            }

            void ResolveProceduralCracks(float2 uv, float damage01, out float crackReveal, out float2 crackNormal)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float2 centered = safeUv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float2 cell = floor(safeUv * 11.0);
                float seed = Hash21(cell);
                float primary = abs(centered.x * 0.72 + centered.y * 0.31 + (seed - 0.5) * 0.13);
                float branch = abs(centered.x * -0.27 + centered.y * 0.86 + sin((centered.x + seed) * 9.0) * 0.025);
                float primaryVein = 1.0 - smoothstep(0.008, 0.035, primary);
                float branchVein = 1.0 - smoothstep(0.004, 0.019, branch);
                float vein = saturate(max(primaryVein, branchVein * 0.62) * smoothstep(0.08, 0.96, radial));
                float threshold = lerp(1.15, 0.18 + seed * 0.54, vein);
                crackReveal = smoothstep(threshold - 0.045, threshold + 0.045, damage01) * vein;
                float2 gradient = float2(primaryVein - branchVein, branchVein - primaryVein * 0.38);
                float2 normalSeed = gradient + centered * (0.15 + seed * 0.1);
                crackNormal = normalSeed * rsqrt(max(dot(normalSeed, normalSeed), 0.0001));
            }

            float ResolveTornVisorEdgeMask(float2 uv, float edge01, float damage01, float stress01)
            {
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float2 centered = safeUv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float angleWave = sin(atan2(centered.y, centered.x) * 11.0 + Hash21(floor(safeUv * 9.0)) * 6.2831853);
                float serration = 0.58 + 0.42 * angleWave;
                float edgeBand = smoothstep(0.54, 0.98, radial) * edge01;
                float drive = HectonFinite01(max(damage01, stress01));
                return saturate(edgeBand * drive * serration);
            }

            float ResolveDitherNoise(float2 uv, float shiftSalt)
            {
                float2 screenParams = max(HectonFinite4(_ScreenParams, float4(1.0, 1.0, 1.0, 1.0)).xy, float2(1.0, 1.0));
                float4 textureFlags = HectonFinite4(_HectonUberTextureFlags, float4(0.0, 0.0, 0.0, 0.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                float safeSalt = HectonFiniteValue(shiftSalt, 0.0);
                float2 salt2 = float2(safeSalt, safeSalt);
                float proceduralNoise = frac(InterleavedGradientNoise(safeUv, safeSalt) + Hash21(floor(safeUv * screenParams * 0.25) + salt2) * 0.5);
                [branch]
                if (textureFlags.z > 0.5)
                {
                    float textureNoise = SAMPLE_TEXTURE2D(_HectonBlueNoiseTex, sampler_HectonBlueNoiseTex, safeUv * screenParams * 0.00390625 + salt2).r;
                    return frac(textureNoise + proceduralNoise * 0.5);
                }

                return proceduralNoise;
            }

            float ResolveComfortShaftMask(float2 uv, float edge01)
            {
                float4 textureFlags = HectonFinite4(_HectonUberTextureFlags, float4(0.0, 0.0, 0.0, 0.0));
                float4 comfortJerkState = HectonFinite4(_HectonVRComfortJerkState, float4(0.0, 0.0, 0.0, 0.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float lowTier = HectonFinite01(_HectonUberLowTier);
                float comfortVignette01 = HectonFinite01(max(HectonFinite01(_VRComfortVignette01), comfortJerkState.x * comfortJerkState.w));
                float comfortEdgeProcedural = smoothstep(0.16, 1.0, edge01);
                float comfortEdgeLowTier = smoothstep(0.36, 0.48, edge01);
                float comfortLowTier01 = smoothstep(0.25, 0.95, lowTier);
                float comfortMaskTexture = SAMPLE_TEXTURE2D(_HectonVRComfortMaskTex, sampler_HectonVRComfortMaskTex, safeUv).r;
                comfortEdgeLowTier = lerp(comfortEdgeLowTier, comfortMaskTexture, saturate(textureFlags.w));

                float comfortEdge = lerp(comfortEdgeProcedural, comfortEdgeLowTier, comfortLowTier01);
                return 1.0 - saturate(comfortEdge * comfortVignette01 * 0.92);
            }

            #if !defined(SHADER_API_MOBILE)
            half3 AccumulateLightShaftSource(float2 uv, float centerEyeDepth, float4 source, float4 sourceColor, float sampleBudget)
            {
                uv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                source = HectonFinite4(source, float4(0.5, 0.5, 0.0, 1.0));
                sourceColor = HectonFinite4(sourceColor, float4(1.0, 1.0, 1.0, 1.0));
                float intensity = HectonFiniteNonNegative(source.z, 0.0);
                [branch]
                if (intensity <= 0.0001)
                    return half3(0.0h, 0.0h, 0.0h);

                float2 lightUv = saturate(source.xy);
                float2 toLight = lightUv - uv;
                float2 toLightAbs = abs(toLight);
                float distanceToLight = max(toLightAbs.x, toLightAbs.y) + min(toLightAbs.x, toLightAbs.y) * 0.375;
                [branch]
                if (distanceToLight <= 0.0001)
                    return half3(0.0h, 0.0h, 0.0h);

                float taps = clamp(HectonFiniteValue(sampleBudget, 1.0), 1.0, 16.0);
                float2 stepUv = toLight * rcp(taps);
                float4 shaftQuality = HectonFinite4(_HectonLightShaftQuality, float4(0.01, 1.0, 0.001, 1.0));
                float emissionThreshold = max(0.01, shaftQuality.x);
                float depthBias = max(0.001, shaftQuality.z);
                float radialFalloff = max(0.35, source.w);
                half3 colorSum = half3(0.0h, 0.0h, 0.0h);
                float weightSum = 0.0;
                float4 zBufferParams = HectonFinite4(_ZBufferParams, float4(1.0, 1.0, 1.0, 1.0));

                [unroll(16)]
                for (int i = 0; i < 16; i++)
                {
                    float tapMask = step((float)i + 0.5, taps);
                    float tapIndex = (float)i + 0.5;
                    float2 sampleUv = saturate(uv + stepUv * tapIndex);
                    half3 sampleColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUv).rgb;
                    float sampleLuma = dot(sampleColor, half3(0.2126h, 0.7152h, 0.0722h));
                    float emissionMask = saturate((sampleLuma - emissionThreshold) * 2.0);
                    float rawDepth = HectonFiniteSceneRawDepth(SampleSceneDepth(sampleUv));
                    float sampleDepthValid = HectonSceneDepthValid01(rawDepth);
                    float sampleEyeDepth = HectonFiniteNonNegative(LinearEyeDepth(rawDepth, zBufferParams), centerEyeDepth + depthBias);
                    float occlusion = lerp(0.12, 1.0, sampleDepthValid * step(centerEyeDepth - depthBias, sampleEyeDepth));
                    float radial01 = saturate(1.0 - (tapIndex * rcp(taps)) * 0.92);
                    float weight = tapMask * emissionMask * occlusion * FastRadialFalloff01(radial01, radialFalloff);
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
                float2 safeUv = saturate(HectonFinite2(uv, float2(0.5, 0.5)));
                float4 shaftParams = HectonFinite4(_HectonLightShaftParams, float4(0.0, 0.0, 0.0, 0.0));
                float4 shaftQuality = HectonFinite4(_HectonLightShaftQuality, float4(0.01, 1.0, 0.001, 1.0));
                float lowTier = HectonFinite01(_HectonUberLowTier);
                float shaftQualityWeight = 1.0 - smoothstep(0.35, 0.95, lowTier);

                [branch]
                if (HectonFinite01(_HectonUberDepthlessTBDR) > 0.5)
                    return half3(0.0h, 0.0h, 0.0h);

                float activeCount = max(0.0, shaftParams.x);
                [branch]
                if (activeCount <= 0.5)
                    return half3(0.0h, 0.0h, 0.0h);

                float4 zBufferParams = HectonFinite4(_ZBufferParams, float4(1.0, 1.0, 1.0, 1.0));
                float rawDepth = HectonFiniteSceneRawDepth(SampleSceneDepth(safeUv));
                float centerDepthValid = HectonSceneDepthValid01(rawDepth);
                float centerEyeDepth = HectonFiniteNonNegative(LinearEyeDepth(rawDepth, zBufferParams), 0.0);
                float sampleBudget = lerp(1.0, clamp(shaftQuality.y, 1.0, 16.0), shaftQualityWeight);
                float comfortMask = centerDepthValid * ResolveComfortShaftMask(safeUv, edge01);
                float sootBoost = lerp(0.35, 1.65, HectonFinite01(_HectonAtmosphereSoot));
                float globalIntensity = HectonFiniteNonNegative(shaftParams.y, 0.0) * sootBoost * comfortMask * shaftQualityWeight;

                half3 shafts = half3(0.0h, 0.0h, 0.0h);
                shafts += AccumulateLightShaftSource(safeUv, centerEyeDepth, _HectonLightShaftSource0, _HectonLightShaftColor0, sampleBudget) * (half)smoothstep(0.01, 0.99, activeCount);
                shafts += AccumulateLightShaftSource(safeUv, centerEyeDepth, _HectonLightShaftSource1, _HectonLightShaftColor1, sampleBudget) * (half)smoothstep(1.01, 1.99, activeCount);
                shafts += AccumulateLightShaftSource(safeUv, centerEyeDepth, _HectonLightShaftSource2, _HectonLightShaftColor2, sampleBudget) * (half)smoothstep(2.01, 2.99, activeCount);
                return shafts * (half)globalIntensity;
            #endif
            }

            half3 ResolveLensDirt(float2 uv, float edge01)
            {
                float4 textureFlags = HectonFinite4(_HectonUberTextureFlags, float4(0.0, 0.0, 0.0, 0.0));
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.0, 0.0);
                [branch]
                if (textureFlags.y > 0.5)
                    return SAMPLE_TEXTURE2D(_HectonLensDirtTex, sampler_HectonLensDirtTex, safeUv).rgb;

                float2 cell = floor(safeUv * 18.0);
                float grain = Hash21(cell);
                float streak = 1.0 - smoothstep(0.08, 0.34, abs(frac(safeUv.x * 7.0 + safeUv.y * 1.7 + grain) - 0.5));
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
                float2 safeUv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float4 brineColor = HectonFinite4(_BrineColor, float4(0.0, 0.0, 0.0, 0.0));
                float brineHeightY = HectonFiniteValue(_BrineHeightY, -100000.0);
                float hardClip = HectonFinite01(_BrineFogHardClip);
                [branch]
                if (HectonFinite01(_HectonUberDepthlessTBDR) > 0.5)
                    return color;

                float rawDepth = HectonFiniteSceneRawDepth(SampleSceneDepth(safeUv));
                float depthValid = HectonSceneDepthValid01(rawDepth);
                float3 worldPosition = ComputeWorldSpacePosition(safeUv, rawDepth, UNITY_MATRIX_I_VP);
                worldPosition = HectonFinite3(worldPosition, float3(0.0, brineHeightY + 1.0, 0.0));
                float distanceBelowPlane = max(0.0, brineHeightY - worldPosition.y);
                float belowPlane = depthValid * step(worldPosition.y, brineHeightY) * step(0.0001, brineColor.a);
                float softFog = saturate(distanceBelowPlane * 0.20);
                float hardFog = step(0.0001, distanceBelowPlane);
                float fogMode = HectonFinite01(max(lowTier, hardClip));
                float fog = belowPlane * lerp(softFog, hardFog, fogMode) * HectonFinite01(brineColor.a);
                return lerp(color, (half3)brineColor.rgb, (half)fog);
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 rawUv = ResolveXRStereoScreenUV(input.screenUV);
                float2 uv = all(isfinite(rawUv)) ? saturate(rawUv) : float2(0.5, 0.5);
                float4 strengths0 = HectonFinite4(_HectonUberStrengths0, float4(0.0, 0.0, 0.0, 0.0));
                float4 strengths1 = HectonFinite4(_HectonUberStrengths1, float4(0.0, 0.0, 0.0, 0.0));
                float4 waveParams = HectonFinite4(_HectonUberWaveParams, float4(1.0, 0.0, 0.0, 0.0));
                float4 textureFlags = HectonFinite4(_HectonUberTextureFlags, float4(0.0, 0.0, 0.0, 0.0));
                float4 comfortJerkState = HectonFinite4(_HectonVRComfortJerkState, float4(0.0, 0.0, 0.0, 0.0));
                float4 waterlineParams = HectonFinite4(_InternalWaterlineParams, float4(0.5, 0.0, 0.0, 0.0));
                float4 waterlineDistortion = HectonFinite4(_InternalWaterlineDistortion, float4(0.0, 0.0, 0.01, 1.0));
                float4 internalWaterColor = HectonFinite4(_InternalWaterColor, float4(0.0, 0.0, 0.0, 0.0));
                float health01 = HectonFinite01(_HectonUberHealthFraction);
                float damage01 = saturate(1.0 - health01);
                float edge01 = FastEdge01(uv);
                float localTemperature = HectonFiniteValue(_HectonUberLocalTemperature, 0.0);
                float pressure01 = HectonFinite01((HectonFiniteValue(_HectonUberAmbientPressure, 1.0) - 1.0) * strengths1.x);
                float heat01 = HectonFinite01(abs(localTemperature) * strengths1.y);
                float stress01 = HectonFinite01(_HectonUberPlayerStress01);
                float hypoxia01 = HectonFinite01(_HectonUberHypoxia01);
                float lowTier01 = HectonFinite01(_HectonUberLowTier);
                float hullStress01 = HectonFinite01(_HectonUberHullStress01);
                float wetLens01 = HectonFinite01(_HectonUberWetLens01);

                float crackReveal;
                float2 crackNormal;
                [branch]
                if (textureFlags.x > 0.5)
                {
                    float4 crackSample = SAMPLE_TEXTURE2D(_HectonVisorCrackTex, sampler_HectonVisorCrackTex, uv);
                    crackReveal = smoothstep(crackSample.a - 0.045, crackSample.a + 0.045, damage01);
                    crackNormal = crackSample.rg * 2.0 - 1.0;
                }
                else
                {
                    ResolveProceduralCracks(uv, damage01, crackReveal, crackNormal);
                }

                crackNormal = all(isfinite(crackNormal)) ? clamp(crackNormal, float2(-1.0, -1.0), float2(1.0, 1.0)) : float2(0.0, 0.0);
                float crackMask = crackReveal * HectonFinite01(strengths0.w);
                float tornEdgeMask = ResolveTornVisorEdgeMask(uv, edge01, damage01, max(stress01, hullStress01));
                float2 visorEdgeSeed = (uv - 0.5) + float2(0.0007, -0.0004);
                float2 visorEdgeNormal = visorEdgeSeed * rsqrt(max(dot(visorEdgeSeed, visorEdgeSeed), 0.0001));

                float2 warpedUV = BarrelWarp(uv, pressure01, strengths0.z);
                warpedUV += HeatHazeOffset(uv, heat01, lowTier01);
                warpedUV += HectonClampUvOffset(crackNormal * (crackMask * HectonFiniteValue(strengths1.z, 0.0)), 0.1);
                warpedUV += HectonClampUvOffset(visorEdgeNormal * (tornEdgeMask * HectonFiniteValue(strengths1.z, 0.0) * 0.35), 0.04);
                warpedUV = saturate(warpedUV);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUV);
                float internalWaterMask = ResolveInternalWaterMask(uv);
                [branch]
                if (internalWaterMask > 0.001)
                {
                    float2 waterUV = saturate(warpedUV + InternalWaterOffset(uv, internalWaterMask));
                    float refractionBlend = HectonFinite01(internalWaterMask * smoothstep(1e-5, 8e-5, waterlineDistortion.x));
                    [branch]
                    if (refractionBlend > 0.001)
                    {
                        half4 refractedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, waterUV);
                        color = lerp(color, refractedColor, (half)refractionBlend);
                    }

                    half3 waterTint = lerp(color.rgb, (half3)internalWaterColor.rgb, (half)HectonFinite01(waterlineDistortion.y));
                    color.rgb = lerp(color.rgb, waterTint, (half)(internalWaterMask * HectonFinite01(internalWaterColor.a)));
                }
                float droplets01 = HectonFinite01(waterlineParams.w);
                [branch]
                if (droplets01 > 0.001)
                {
                    float dropletMask = ResolveInternalDropletMask(uv, droplets01) * (0.35 + edge01 * 0.65);
                    float dropletRefractionQuality =
                        (1.0 - smoothstep(0.35, 0.95, HectonFinite01(waterlineDistortion.w))) *
                        smoothstep(1e-5, 8e-5, waterlineDistortion.x);
                    float dropletRefractBlend = saturate(dropletMask * dropletRefractionQuality);
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

                float damageDrive = HectonFinite01(max(damage01, max(hullStress01, stress01)) + crackMask * 0.35);
                color.rgb = ApplySingleSampleChroma(color.rgb, edge01, damageDrive, strengths0.x);
                color.rgb = ApplyBrinePlaneFog(color.rgb, uv, lowTier01);

                float shiftSalt = frac(HectonFiniteValue(_HectonUberAupShiftFrame, 0.0) * 0.6180339887);
                float blueNoise = ResolveDitherNoise(uv, shiftSalt);
                half3 dirt = ResolveLensDirt(uv, edge01);
                float dirtDrive = HectonFinite01(strengths1.w * (0.18 + edge01 * 0.82 + wetLens01 * 0.35));
                float dirtMask = step(blueNoise, dirtDrive);
                color.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), dirt, (half)dirtMask);

                half crackDarken = (half)(crackMask * (0.22 + edge01 * 0.18));
                color.rgb *= 1.0h - crackDarken;
                color.rgb += (half3(0.16h, 0.22h, 0.24h) * (half)(crackMask * 0.035));
                half tornEdge = (half)tornEdgeMask;
                color.rgb *= 1.0h - tornEdge * 0.14h;
                color.rgb += half3(0.11h, 0.025h, 0.018h) * tornEdge * (half)(0.35 + HectonFinite01(_HectonUberBleeding01) * 0.65);

                half luma = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 hypoxiaLuma = half3(luma, luma, luma);
                color.rgb = lerp(color.rgb, hypoxiaLuma * half3(0.78h, 0.91h, 1.05h), (half)(hypoxia01 * HectonFiniteNonNegative(strengths0.y, 0.0)));

                float cold01 = HectonFinite01(-localTemperature * strengths1.y);
                float frostNoise = ResolveDitherNoise(uv * 1.73 + crackNormal * 0.013, shiftSalt + 11.17);
                float frostDrive = saturate(cold01 * (0.18 + edge01 * 0.92 + crackMask * 0.34));
                float frostMask = step(frostNoise, frostDrive);
                half3 frostTint = lerp(color.rgb, max(color.rgb, half3(0.64h, 0.82h, 0.94h)), (half)(0.45 * cold01));
                color.rgb = lerp(color.rgb, frostTint, (half)(frostMask * frostDrive));

                float comfortVignette01 = HectonFinite01(max(HectonFinite01(_VRComfortVignette01), comfortJerkState.x * comfortJerkState.w));
                float comfortEdgeProcedural = smoothstep(0.16, 1.0, edge01);
                float comfortEdgeLowTier = smoothstep(0.36, 0.48, edge01);
                float comfortLowTier01 = smoothstep(0.25, 0.95, lowTier01);
                float comfortMaskTexture = SAMPLE_TEXTURE2D(_HectonVRComfortMaskTex, sampler_HectonVRComfortMaskTex, uv).r;
                comfortEdgeLowTier = lerp(comfortEdgeLowTier, comfortMaskTexture, saturate(textureFlags.w));
                float comfortEdge = lerp(comfortEdgeProcedural, comfortEdgeLowTier, comfortLowTier01);
                float vignette = saturate(
                    edge01 * stress01 * strengths1.w +
                    edge01 * damageDrive * waveParams.w +
                    comfortEdge * comfortVignette01 * 0.92);
                color.rgb *= 1.0h - (half)vignette;

                float bleeding = HectonFinite01(_HectonUberBleeding01);
                half bloodEdge = (half)(bleeding * saturate(edge01 + tornEdgeMask * 0.75) * strengths1.w);
                color.rgb = lerp(color.rgb, half3(0.48h, 0.015h, 0.012h), bloodEdge);
                color.rgb = max(color.rgb, half3(0.0015h, 0.0022h, 0.0030h));
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

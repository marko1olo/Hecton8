Shader "Hecton8/Environment/Hecton_AbyssalVoxelRock"
{
    Properties
    {
        [MainTexture] _Base_Map ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _Normal_Map ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _Mask_Map ("Mask Map", 2D) = "white" {}
        [NoScaleOffset] _FreshRockAlbedoMap ("Fresh Rock Albedo Map", 2D) = "white" {}
        [NoScaleOffset] _FreshRockNormalMap ("Fresh Rock Normal Map", 2D) = "bump" {}
        _Instance_Color ("Instance Color", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling", Range(0.01, 4)) = 0.2
        _TriplanarBlendSharpness ("Triplanar Blend Sharpness", Range(1, 8)) = 8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Mask Map", 2D) = "white" {}
        _SkirtSandTint ("Skirt Sand Tint", Color) = (0.42, 0.38, 0.31, 1)
        _SkirtBlendContrast ("Skirt Blend Contrast", Range(0.1, 4)) = 1.4
        _CutScarColor ("Cut Scar Color", Color) = (1.0, 0.42, 0.12, 1)
        _CutScarWarmColor ("Cut Scar Warm Color", Color) = (0.42, 0.06, 0.03, 1)
        _CutScarCharColor ("Cut Scar Char Color", Color) = (0.06, 0.03, 0.02, 1)
        _CutScarEmission ("Cut Scar Emission", Range(0, 8)) = 1.8
        _CutScarSharpness ("Cut Scar Sharpness", Range(0.5, 8)) = 2.2
        _CutScarDarkening ("Cut Scar Darkening", Range(0, 1)) = 0.24
        _ShadowScarErosion ("Shadow Scar Erosion", Range(0, 0.35)) = 0.14
        _SkirtDepthBias ("Skirt Depth Bias", Range(0, 0.01)) = 0.00035
        _CurvatureWearTint ("Curvature Wear Tint", Color) = (0.84, 0.81, 0.77, 1)
        _CurvatureEdgeWearStrength ("Curvature Edge Wear Strength", Range(0, 1)) = 0.24
        _CurvatureCavityDarkenStrength ("Curvature Cavity Darken Strength", Range(0, 1)) = 0.28
        _CurvatureContrast ("Curvature Contrast", Range(0.5, 4)) = 1.35
        _ProceduralDirtAge ("Procedural Dirt Age", Range(0, 1)) = 0.65
        _SiltStrength ("Procedural Silt Strength", Range(0, 1)) = 0.42
        _RustStrength ("Procedural Rust Strength", Range(0, 1)) = 0.26
        _SiltTint ("Procedural Silt Tint", Color) = (0.31, 0.30, 0.25, 1)
        _RustTint ("Procedural Rust Tint", Color) = (0.47, 0.16, 0.06, 1)
        _ChunkDissolveFade ("Chunk Dissolve Fade", Range(0, 1)) = 1
        _ChunkDissolveGlitchStrength ("Chunk Dissolve Glitch Strength", Range(0, 1)) = 0.18
        _ChunkDissolvePhosphorTint ("Chunk Dissolve Phosphor Tint", Color) = (0.04, 0.82, 0.18, 1)
        _FreshCutColorBoost ("Fresh Cut Color Boost", Range(1, 2)) = 1.18
        _FreshCutNormalBoost ("Fresh Cut Normal Boost", Range(1, 3)) = 1.45
        _CaveMouthDisplacementStrength ("Cave Mouth GPU Jag Strength", Range(0, 0.35)) = 0.08
        _CaveMouthDisplacementScale ("Cave Mouth GPU Jag Scale", Range(0.05, 4)) = 0.85
        _LocalCausticStrength ("Local Caustic Strength", Range(0, 1)) = 0.22
        _LocalCausticScale ("Local Caustic Scale", Range(0.1, 4)) = 0.7
        _LocalCausticSpeed ("Local Caustic Speed", Range(0, 4)) = 0.36
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
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

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Instance_Color;
            float4 _SkirtSandTint;
            float4 _CutScarColor;
            float4 _CutScarWarmColor;
            float4 _CutScarCharColor;
            float4 _CurvatureWearTint;
            float4 _SiltTint;
            float4 _RustTint;
            float4 _ChunkDissolvePhosphorTint;
            float _Tiling;
            float _TriplanarBlendSharpness;
            float _Smoothness;
            float _SkirtBlendContrast;
            float _CutScarEmission;
            float _CutScarSharpness;
            float _CutScarDarkening;
            float _ShadowScarErosion;
            float _SkirtDepthBias;
            float _CurvatureEdgeWearStrength;
            float _CurvatureCavityDarkenStrength;
            float _CurvatureContrast;
            float _ProceduralDirtAge;
            float _SiltStrength;
            float _RustStrength;
            float _ChunkDissolveFade;
            float _ChunkDissolveGlitchStrength;
            float _FreshCutColorBoost;
            float _FreshCutNormalBoost;
            float _CaveMouthDisplacementStrength;
            float _CaveMouthDisplacementScale;
            float _LocalCausticStrength;
            float _LocalCausticScale;
            float _LocalCausticSpeed;
        CBUFFER_END

        float4 _SargassumCutMaskWorldRect;
        float4 _HectonDamageVolumeWorldMin;
        float4 _HectonDamageVolumeInvSize;
        float4 _HectonFloatingOriginOffset;
        float4 _HectonNoirResolveSettings;
        float4 _HectonNoirCausticsLayerA;
        float4 _HectonNoirCausticsLayerB;
        float4 _HectonNoirCausticsShape;
        float4 _HectonNoirCaveAttenuation;
        float _SargassumCutMaskActive;
        float _HectonDamageVolumeActive;
        float _HectonVoxelSSAOActive;
        int _HectonRecentCutHeatCount;
        float3 _LightDirection;

        #define HECTON_RECENT_CUT_HEAT_MAX 16
        float4 _HectonRecentCutHeatPositionRadius[HECTON_RECENT_CUT_HEAT_MAX];
        float4 _HectonRecentCutHeatStrengthTime[HECTON_RECENT_CUT_HEAT_MAX];

        TEXTURE2D(_Base_Map);
        SAMPLER(sampler_Base_Map);
        TEXTURE2D(_Normal_Map);
        SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_Mask_Map);
        SAMPLER(sampler_Mask_Map);
        TEXTURE2D(_FreshRockAlbedoMap);
        SAMPLER(sampler_FreshRockAlbedoMap);
        TEXTURE2D(_FreshRockNormalMap);
        SAMPLER(sampler_FreshRockNormalMap);
        TEXTURE2D(_HectonVoxelSSAOTex);
        SAMPLER(sampler_HectonVoxelSSAOTex);
        TEXTURE2D(_SargassumCutMaskRT);
        SAMPLER(sampler_SargassumCutMaskRT);
        TEXTURE3D(_HectonDamageVolumeTex);
        SAMPLER(sampler_HectonDamageVolumeTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float4 bakedAmbientOcclusion : TEXCOORD1;
            float4 dirtyBlendUv2 : TEXCOORD2;
            float3 absolutePositionWS : TEXCOORD3;
        };

        struct SurfaceVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half3 viewDirWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            half skirtAlpha : TEXCOORD4;
            float3 absolutePositionWS : TEXCOORD5;
            half curvature : TEXCOORD6;
            half bakedAmbientOcclusion : TEXCOORD7;
            half freshCutBlend : TEXCOORD8;
            half4 terrainSplatColor : TEXCOORD9;
        };

        struct ClipVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half skirtAlpha : TEXCOORD1;
        };

        struct ShadowVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half skirtAlpha : TEXCOORD2;
        };

        half3 SafeNormalize3(half3 value)
        {
            half lenSq = dot(value, value);
            return lenSq > 0.0001h ? value * rsqrt(lenSq) : half3(0.0h, 1.0h, 0.0h);
        }

        float4 ApplySkirtDepthBias(float4 positionCS, half skirtAlpha)
        {
            float skirtMask = saturate(skirtAlpha);
            float gradientBias = skirtMask * skirtMask;
            float clipBias = gradientBias * _SkirtDepthBias * max(positionCS.w, 0.0001);
            #if UNITY_REVERSED_Z
            positionCS.z += clipBias;
            #else
            positionCS.z -= clipBias;
            #endif
            return positionCS;
        }

        float Hash21(float2 value)
        {
            return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
        }

        float ResolveDitherNoise(float2 positionCS)
        {
            float2 pixel = floor(positionCS);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float Hash31(float3 value)
        {
            value = frac(value * 0.1031);
            value += dot(value, value.yzx + 33.33);
            return frac((value.x + value.y) * value.z);
        }

        float ValueNoise3(float3 value)
        {
            float3 cell = floor(value);
            float3 fracValue = frac(value);
            float3 smoothValue = fracValue * fracValue * (3.0 - 2.0 * fracValue);

            float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
            float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
            float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
            float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
            float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
            float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
            float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
            float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

            float nx00 = lerp(n000, n100, smoothValue.x);
            float nx10 = lerp(n010, n110, smoothValue.x);
            float nx01 = lerp(n001, n101, smoothValue.x);
            float nx11 = lerp(n011, n111, smoothValue.x);
            float nxy0 = lerp(nx00, nx10, smoothValue.y);
            float nxy1 = lerp(nx01, nx11, smoothValue.y);
            return lerp(nxy0, nxy1, smoothValue.z);
        }

        float3 ResolveSamplePositionWS(float3 positionWS)
        {
            return positionWS + _HectonFloatingOriginOffset.xyz;
        }

        void ResolveDominantAxisProjection(float3 positionWS, half3 normalWS, out float2 uv, out half dominantAxis)
        {
            half3 absNormal = saturate(abs(normalWS));
            float tiling = max(_Tiling, 0.0001);

            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
            {
                uv = positionWS.zy * tiling;
                dominantAxis = 0.0h;
            }
            else if (absNormal.z >= absNormal.y)
            {
                uv = positionWS.xy * tiling;
                dominantAxis = 2.0h;
            }
            else
            {
                uv = positionWS.xz * tiling;
                dominantAxis = 1.0h;
            }

            half edgeBand = saturate((1.0h - max(absNormal.x, max(absNormal.y, absNormal.z))) * 2.0h);
            float noise = Hash31(float3(floor(uv * 37.0), (float)dominantAxis + 5.37)) * 2.0 - 1.0;
            float stochasticEdgeOffset = noise * edgeBand * 0.035;
            uv += float2(stochasticEdgeOffset, -stochasticEdgeOffset);
        }

        float ResolveCaveMouthVertexDisplacement(float3 absolutePositionWS, float3 normalOS, half splatBlend, half skirtAlpha)
        {
            float seamMask = saturate(max(splatBlend, skirtAlpha));
            if (seamMask <= 0.0001)
                return 0.0;

            float scale = max(_CaveMouthDisplacementScale, 0.05);
            float lowNoise = ValueNoise3(absolutePositionWS * scale + 17.13);
            float highNoise = ValueNoise3(absolutePositionWS * (scale * 2.37) + 41.9);
            float jagged = ((lowNoise * 0.72 + highNoise * 0.28) * 2.0) - 1.0;
            return jagged * _CaveMouthDisplacementStrength * seamMask;
        }

        float3 ResolveVoxelPositionOS(Attributes input)
        {
            float3 safeNormalOS = normalize(input.normalOS + float3(0.0, 0.0001, 0.0));
            float displacement = ResolveCaveMouthVertexDisplacement(
                input.absolutePositionWS,
                safeNormalOS,
                saturate((half)input.color.a),
                saturate((half)input.dirtyBlendUv2.y));
            return input.positionOS.xyz + safeNormalOS * displacement;
        }

        void ApplyChunkDissolveFade(float4 positionCS)
        {
            half fade = saturate((half)_ChunkDissolveFade);
            if (fade >= 0.999h)
                return;

            clip(fade - ResolveDitherNoise(positionCS.xy));
        }

        half ApplyChunkDissolveMalfunction(float4 positionCS, float3 positionWS, inout half3 albedo, inout half smoothness)
        {
            half reveal = saturate(1.0h - (half)_ChunkDissolveFade);
            half strength = saturate((half)_ChunkDissolveGlitchStrength);
            if (reveal <= 0.0001h || strength <= 0.0001h)
                return 0.0h;

            float scanline = frac(positionCS.y * 0.125 + _Time.y * 17.0);
            half scanPulse = smoothstep(0.84h, 0.98h, (half)scanline);
            half staticNoise = (half)Hash31(floor(positionWS * 5.0 + _Time.y * 3.0));
            half edgeNoise = smoothstep(0.52h, 0.93h, staticNoise);
            half malfunction = saturate((scanPulse * 0.55h + edgeNoise * 0.45h) * reveal * strength);

            albedo = lerp(albedo, (half3)_ChunkDissolvePhosphorTint.rgb, malfunction * 0.18h);
            smoothness = lerp(smoothness, 0.72h, malfunction * 0.5h);
            return malfunction;
        }

        half4 SampleDominantAxisColor(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS)
        {
            float2 uv;
            half dominantAxis;
            ResolveDominantAxisProjection(positionWS, normalWS, uv, dominantAxis);
            return SAMPLE_TEXTURE2D(tex, samp, uv);
        }

        half3 SampleDominantAxisNormal(float3 positionWS, half3 baseNormalWS)
        {
            float2 uv;
            half dominantAxis;
            ResolveDominantAxisProjection(positionWS, baseNormalWS, uv, dominantAxis);

            half3 normalSign = sign(baseNormalWS);
            half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, uv));
            if (dominantAxis < 0.5h)
                return SafeNormalize3(half3(tangentNormal.z * normalSign.x, tangentNormal.y, tangentNormal.x));

            if (dominantAxis > 1.5h)
                return SafeNormalize3(half3(tangentNormal.x, tangentNormal.y, tangentNormal.z * normalSign.z));

            return SafeNormalize3(half3(tangentNormal.x, tangentNormal.z * normalSign.y, tangentNormal.y));
        }

        half EvaluateGlobalCutMask(float3 positionWS)
        {
            if (_SargassumCutMaskActive < 0.5)
                return 0.0h;

            float2 uv = float2(
                (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return 0.0h;

            return SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r;
        }

        half EvaluateDamageVolumeMask(float3 positionWS)
        {
            if (_HectonDamageVolumeActive < 0.5)
                return 0.0h;

            float3 uvw = (positionWS - _HectonDamageVolumeWorldMin.xyz) * _HectonDamageVolumeInvSize.xyz;
            if (uvw.x < 0.0 || uvw.x > 1.0 || uvw.y < 0.0 || uvw.y > 1.0 || uvw.z < 0.0 || uvw.z > 1.0)
                return 0.0h;

            return SAMPLE_TEXTURE3D_LOD(_HectonDamageVolumeTex, sampler_HectonDamageVolumeTex, uvw, 0).r;
        }

        half ResolveSkirtCoverageMask(half vertexAlpha)
        {
            return saturate(pow(saturate(vertexAlpha), max(_SkirtBlendContrast, 0.1)));
        }

        half ResolveSkirtCoverage(half vertexAlpha, float2 positionCS)
        {
            half shapedAlpha = ResolveSkirtCoverageMask(vertexAlpha);
            clip(shapedAlpha - ResolveDitherNoise(positionCS));
            return shapedAlpha;
        }

        half ResolveShadowCoverage(half vertexAlpha, float2 positionCS, half scarMask, float3 positionWS)
        {
            half shapedAlpha = saturate(pow(saturate(vertexAlpha), max(_SkirtBlendContrast, 0.1)));
            float scarNoise = Hash21(floor(positionWS.xz * 3.0 + scarMask * 19.0));
            half erosion = scarMask * _ShadowScarErosion * (1.0h - scarNoise);
            half coverage = saturate(shapedAlpha - erosion);
            clip(coverage - ResolveDitherNoise(positionCS));
            return coverage;
        }

        half SampleVoxelAmbientOcclusion(float4 positionCS)
        {
            if (_HectonVoxelSSAOActive < 0.5)
                return 1.0h;

            float2 screenUV = saturate(positionCS.xy * _ScaledScreenParams.zw);
            return SAMPLE_TEXTURE2D_LOD(_HectonVoxelSSAOTex, sampler_HectonVoxelSSAOTex, screenUV, 0).r;
        }

        half ResolveNoirVoxelCaustic(float3 absolutePositionWS, half3 normalWS, float4 positionCS)
        {
            float waterDepth = max(0.0, _HectonNoirFogStratification.x - absolutePositionWS.y);
            float depthFade = 1.0 - saturate((waterDepth - _HectonNoirCausticsShape.z) / max(_HectonNoirCausticsShape.w, 0.25));
            if (depthFade <= 0.0)
                return 1.0h;

            float timePhase = _Time.y;
            float3 layerABasis = absolutePositionWS * max(_HectonNoirCausticsLayerA.x, 0.02);
            float3 layerBSampleAnchor = absolutePositionWS * max(_HectonNoirCausticsLayerB.x, 0.02);
            float3 layerAInput = float3(
                layerABasis.x + timePhase * _HectonNoirCausticsLayerA.y,
                layerABasis.y * 0.23 + timePhase * (_HectonNoirCausticsLayerA.y * 0.31 + _HectonNoirCausticsLayerA.z * 0.17),
                layerABasis.z + timePhase * _HectonNoirCausticsLayerA.z);
            float distortion = (ValueNoise3(layerAInput * 0.73 + 7.1) * 2.0 - 1.0) * _HectonNoirCausticsShape.y;
            float3 layerBInput = float3(
                layerBSampleAnchor.x + timePhase * _HectonNoirCausticsLayerB.y + distortion,
                layerBSampleAnchor.y * 0.19 + timePhase * (_HectonNoirCausticsLayerB.y * 0.27 + _HectonNoirCausticsLayerB.z * 0.23),
                layerBSampleAnchor.z + timePhase * _HectonNoirCausticsLayerB.z - distortion);
            half layerA = (half)ValueNoise3(layerAInput);
            half layerB = (half)ValueNoise3(layerBInput);
            half causticRaw = pow(saturate(layerA * layerB), (half)max(_HectonNoirCausticsShape.x, 1.0));
            half upFacingMask = saturate(normalWS.y * 1.25h);
            half caveOcclusion = 1.0h - SampleVoxelAmbientOcclusion(positionCS);
            half caveFade = saturate(1.0h - caveOcclusion * _HectonNoirCaveAttenuation.x);
            half strength = saturate((_HectonNoirCausticsLayerA.w + _HectonNoirCausticsLayerB.w) * depthFade);
            return lerp(1.0h, 1.0h + causticRaw * strength * caveFade, upFacingMask);
        }

        half ResolveLocalLightCaustic(float3 absolutePositionWS, half3 normalWS, float4 positionCS)
        {
            if (_HectonNoirResolveSettings.z > 0.5h)
                return ResolveNoirVoxelCaustic(absolutePositionWS, normalWS, positionCS);

            float scale = max(_LocalCausticScale, 0.05);
            float speed = max(_LocalCausticSpeed, 0.0);
            float timePhase = _Time.y * speed;
            float3 causticBasis = absolutePositionWS * scale;
            float3 layerASample = float3(
                causticBasis.x + timePhase * 0.83,
                causticBasis.y * 0.27 + timePhase * 0.19,
                causticBasis.z + timePhase * 0.56);
            float3 layerBSample = float3(
                causticBasis.x * 1.37 - timePhase * 0.41 + 13.1,
                causticBasis.y * 0.19 + timePhase * 0.23 + 7.3,
                causticBasis.z * 1.37 + timePhase * 0.91 + 5.7);
            half layerA = (half)ValueNoise3(layerASample);
            half layerB = (half)ValueNoise3(layerBSample);
            half causticRaw = pow(saturate(layerA * layerB), 3.0h);
            half upFacingMask = saturate(normalWS.y * 0.9h + 0.1h);
            half causticMask = saturate(_LocalCausticStrength * upFacingMask);
            return lerp(1.0h, 1.0h + causticRaw * _LocalCausticStrength, causticMask);
        }

        void EvaluateRecentCutHeat(float3 positionWS, out half heatMask, out half age01)
        {
            heatMask = 0.0h;
            age01 = 1.0h;

            [unroll]
            for (int i = 0; i < HECTON_RECENT_CUT_HEAT_MAX; i++)
            {
                if (i >= _HectonRecentCutHeatCount)
                    break;

                float4 positionRadius = _HectonRecentCutHeatPositionRadius[i];
                float4 strengthTime = _HectonRecentCutHeatStrengthTime[i];
                float lifetime = max(strengthTime.z, 0.01);
                float elapsed = _Time.y - strengthTime.y;
                if (elapsed < 0.0 || elapsed >= lifetime)
                    continue;

                float radius = max(positionRadius.w, 0.05);
                float3 delta = positionWS - positionRadius.xyz;
                float distanceToStamp = length(delta);
                if (distanceToStamp >= radius)
                    continue;

                float radial = saturate(1.0 - distanceToStamp / radius);
                radial *= radial;
                float localAge01 = saturate(elapsed / lifetime);
                float thermal = strengthTime.x * radial * (1.0 - localAge01);
                if (thermal <= heatMask)
                    continue;

                heatMask = thermal;
                age01 = localAge01;
            }
        }

        half3 EvaluateLighting(float3 positionWS, half3 normalWS, half3 viewDirWS, half3 albedo, half metallic, half smoothness, half occlusion, half localCausticMask)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = SampleSH(normalWS) * albedo * occlusion * caveAmbientFactor;
            half specularStrength = lerp(0.04h, 0.22h, metallic);
            half specularPower = lerp(16.0h, 96.0h, smoothness);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = SafeNormalize3(mainLight.direction);
            half NdotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            color += (albedo * NdotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);

            #if defined(_ADDITIONAL_LIGHTS)
            uint lightCount = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(lightCount)
                Light light = GetAdditionalLight(lightIndex, positionWS);
                half3 additionalDir = SafeNormalize3(light.direction);
                half additionalNdotL = saturate(dot(normalWS, additionalDir));
                half3 additionalHalfDir = SafeNormalize3(additionalDir + viewDirWS);
                half additionalSpecular = pow(saturate(dot(normalWS, additionalHalfDir)), specularPower) * smoothness * specularStrength;
                half causticLightMask = lerp(1.0h, localCausticMask, saturate(additionalNdotL * light.distanceAttenuation));
                float additionalShadowAttenuation = HectonCoreLitResolveFlashlightAdditionalShadow(lightIndex, positionWS, normalWS, light.shadowAttenuation);
                color += ((albedo * additionalNdotL + additionalSpecular) * causticLightMask) * light.color * (light.distanceAttenuation * additionalShadowAttenuation);
            LIGHT_LOOP_END
            #endif

            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;

            return color;
        }

        SurfaceVaryings Vert(Attributes input)
        {
            SurfaceVaryings output;
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = SafeNormalize3(normalInputs.normalWS);
            output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(positionInputs.positionWS));
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.absolutePositionWS = input.absolutePositionWS;
            output.curvature = saturate(input.dirtyBlendUv2.z);
            output.bakedAmbientOcclusion = HectonCoreLitResolveVertexAmbientOcclusion(input.bakedAmbientOcclusion.w);
            output.freshCutBlend = saturate(input.dirtyBlendUv2.x);
            output.terrainSplatColor = saturate((half4)input.color);
            output.positionCS = ApplySkirtDepthBias(output.positionCS, output.skirtAlpha);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        ClipVaryings DepthVert(Attributes input)
        {
            ClipVaryings output;
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPositionOS);
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.positionCS = ApplySkirtDepthBias(positionInputs.positionCS, output.skirtAlpha);
            output.positionWS = positionInputs.positionWS;
            return output;
        }

        ShadowVaryings ShadowVert(Attributes input)
        {
            ShadowVaryings output;
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionWS = positionInputs.positionWS;
            output.normalWS = SafeNormalize3(normalInputs.normalWS);
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, output.normalWS, _LightDirection));
            output.positionCS = ApplySkirtDepthBias(output.positionCS, output.skirtAlpha);

            #if UNITY_REVERSED_Z
            output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
            output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            AlphaToMask On
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 Frag(SurfaceVaryings input) : SV_Target
            {
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                half skirtCoverage = ResolveSkirtCoverageMask(input.skirtAlpha);
                half3 baseNormalWS = SafeNormalize3(input.normalWS);
                float3 samplePositionWS = input.absolutePositionWS;
                half3 dominantNormalWS = SampleDominantAxisNormal(samplePositionWS, baseNormalWS);

                half4 baseSample = SampleDominantAxisColor(TEXTURE2D_ARGS(_Base_Map, sampler_Base_Map), samplePositionWS, baseNormalWS);
                half cutMask = max(EvaluateGlobalCutMask(input.positionWS), EvaluateDamageVolumeMask(input.positionWS));
                half freshCutMask = saturate(max(input.freshCutBlend, cutMask));
                half scarMask = pow(saturate(cutMask), max(_CutScarSharpness, 0.5h));
                half recentHeatMask;
                half recentHeatAge01;
                EvaluateRecentCutHeat(input.positionWS, recentHeatMask, recentHeatAge01);
                half3 boostedNormalWS = dominantNormalWS * lerp(1.0h, (half)_FreshCutNormalBoost, freshCutMask * 0.5h);
                half3 normalWS = SafeNormalize3(baseNormalWS + boostedNormalWS);
                half skirtBlend = 1.0h - skirtCoverage;
                half curvature = saturate(input.curvature);
                half curvatureContrast = max(_CurvatureContrast, 0.5h);
                half convexMask = pow(saturate((curvature - 0.5h) * 2.0h), curvatureContrast);
                half cavityMask = pow(saturate((0.5h - curvature) * 2.0h), curvatureContrast);

                half3 albedo = baseSample.rgb * _Instance_Color.rgb;
                half3 freshAlbedo = baseSample.rgb * _Instance_Color.rgb * (half)_FreshCutColorBoost;
                freshAlbedo = lerp(freshAlbedo, (half3)_CutScarColor.rgb, scarMask * 0.45h);
                albedo = lerp(albedo, freshAlbedo, freshCutMask * 0.62h);
                albedo = lerp(albedo, input.terrainSplatColor.rgb, saturate(input.terrainSplatColor.a) * 0.78h);
                albedo = lerp(albedo, _SkirtSandTint.rgb, skirtBlend * 0.72h);
                albedo = lerp(albedo, lerp(albedo, _CurvatureWearTint.rgb, 0.4h), convexMask * _CurvatureEdgeWearStrength);
                albedo *= 1.0h - cavityMask * (_CurvatureCavityDarkenStrength * 0.32h);
                albedo *= lerp(1.0h, 1.0h - _CutScarDarkening, scarMask);
                albedo = lerp(albedo, _CutScarCharColor.rgb, scarMask * 0.38h);

                half3 thermalColor = lerp(_CutScarWarmColor.rgb, _CutScarColor.rgb, saturate(1.0h - recentHeatAge01 * 0.9h));
                albedo = lerp(albedo, thermalColor, recentHeatMask * 0.18h);

                half metallic = 0.0h;
                half smoothness = saturate(lerp(_Smoothness, 0.88h, scarMask * 0.65h) + convexMask * (_CurvatureEdgeWearStrength * 0.08h));
                half ambientOcclusion = saturate(input.bakedAmbientOcclusion * (1.0h - cavityMask * _CurvatureCavityDarkenStrength)) * SampleVoxelAmbientOcclusion(input.positionCS);
                half localCausticMask = ResolveLocalLightCaustic(samplePositionWS, normalWS, input.positionCS);
                HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);
                HectonCoreLitApplyProceduralRustSilt(
                    samplePositionWS,
                    normalWS,
                    dominantNormalWS,
                    convexMask,
                    (half)_ProceduralDirtAge,
                    (half)_SiltStrength,
                    (half)_RustStrength,
                    (half3)_SiltTint.rgb,
                    (half3)_RustTint.rgb,
                    albedo,
                    metallic,
                    smoothness);
                half dissolveMalfunction = ApplyChunkDissolveMalfunction(input.positionCS, samplePositionWS, albedo, smoothness);

                half3 litColor = EvaluateLighting(input.positionWS, normalWS, SafeNormalize3(input.viewDirWS), albedo, metallic, smoothness, ambientOcclusion, localCausticMask);
                litColor += (half3)_ChunkDissolvePhosphorTint.rgb * dissolveMalfunction * 0.16h;
                half thermalEmission = _CutScarEmission * recentHeatMask * lerp(0.22h, 1.0h, saturate(1.0h - recentHeatAge01));
                half3 emission = (_CutScarWarmColor.rgb * (_CutScarEmission * scarMask * 0.12h)) + (thermalColor * thermalEmission);
                half3 finalColor = MixFog(litColor + emission, input.fogFactor);
                return half4(finalColor, skirtCoverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                half scarMask = pow(saturate(max(EvaluateGlobalCutMask(input.positionWS), EvaluateDamageVolumeMask(input.positionWS))), max(_CutScarSharpness, 0.5h));
                ResolveShadowCoverage(input.skirtAlpha, input.positionCS.xy, scarMask, input.positionWS);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 DepthFrag(ClipVaryings input) : SV_Target
            {
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                ResolveSkirtCoverage(input.skirtAlpha, input.positionCS.xy);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

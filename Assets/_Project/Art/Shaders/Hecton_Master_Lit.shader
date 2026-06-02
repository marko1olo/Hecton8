Shader "Hecton8/Rendering/Hecton_Master_Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _MaskMap("Packed MRAO Height", 2D) = "white" {}

        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color RGB, A Mask Weight", Color) = (0, 0, 0, 0)
        _MasterSurfaceParams("Surface: MetallicMap RoughnessMap AO Normal", Vector) = (0, 0, 1, 1)
        _MasterAlphaParams("Alpha: Cutoff Scale Dither ClipWeight", Vector) = (0.5, 1, 0.35, 0)
        _MasterPomParams("POM: Scale Steps Bias QualityCap", Vector) = (0, 0, 0, 1)
        _MasterNoirParams("Noir: Ambient Wetness Specular Emission", Vector) = (0.34, 0.18, 0.42, 1)
        _MasterShadowParams("Shadow: Contact FogDarken Micro MaskLayout", Vector) = (1, 0.15, 0.18, 0)

        _Metallic("Legacy Metallic", Range(0, 1)) = 0
        _Smoothness("Legacy Smoothness", Range(0, 1)) = 0.55
        _OcclusionStrength("Legacy Occlusion", Range(0, 1)) = 1
        _BumpScale("Legacy Normal Scale", Range(0, 2)) = 1
        _Cutoff("Legacy Cutoff", Range(0, 1)) = 0.5
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

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;          //   0..15
            float4 _BumpMap_ST;          //  16..31
            float4 _MaskMap_ST;          //  32..47
            float4 _BaseColor;           //  48..63
            float4 _EmissionColor;       //  64..79
            float4 _MasterSurfaceParams; //  80..95  x=metallic map weight, y=roughness map weight, z=AO strength, w=normal scale
            float4 _MasterAlphaParams;   //  96..111 x=cutoff, y=alpha scale, z=dither strength, w=clip weight
            float4 _MasterPomParams;     // 112..127 x=height scale, y=max steps, z=height bias, w=quality cap
            float4 _MasterNoirParams;    // 128..143 x=ambient, y=wetness, z=specular, w=emission scale
            float4 _MasterShadowParams;  // 144..159 x=contact, y=fog darken, z=micro contrast, w=mask layout: 0 MRAO, 1 legacy, 2 MetallicGloss, 3 ARM
            float _Metallic;             // 160..163
            float _Smoothness;           // 164..167
            float _OcclusionStrength;    // 168..171
            float _BumpScale;            // 172..175
            float _Cutoff;               // 176..179
            float _H8MasterPadding0;     // 180..183
            float _H8MasterPadding1;     // 184..187
            float _H8MasterPadding2;     // 188..191
        CBUFFER_END

        float _H8GlobalQualityWeight;
        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            half3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            float4 shadowCoord : TEXCOORD5;
            half fogFactor : TEXCOORD6;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct ShadowVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float H8MasterQuality()
        {
            float globalQuality = isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0;
            float materialCap = isfinite(_MasterPomParams.w) ? saturate(_MasterPomParams.w) : 1.0;
            return saturate(globalQuality * materialCap);
        }

        float H8MasterSafeRcp(float value)
        {
            float safeValue = max(abs(value), 0.0001);
            float signValue = lerp(-1.0, 1.0, step(0.0, value));
            return signValue * rcp(safeValue);
        }

        float2 H8MasterSafeRcp2(float2 value)
        {
            float2 safeValue = max(abs(value), float2(0.0001, 0.0001));
            float2 signValue = lerp(float2(-1.0, -1.0), float2(1.0, 1.0), step(float2(0.0, 0.0), value));
            return signValue * rcp(safeValue);
        }

        half3 H8MasterSafeNormalize(half3 value, half3 fallbackValue)
        {
            half lenSq = dot(value, value);
            half valid = (half)step(0.0001, (float)lenSq);
            half3 normalized = value * (half)rsqrt(max((float)lenSq, 0.0001));
            return lerp(fallbackValue, normalized, valid);
        }

        float2 H8MasterNormalizedScreenSpaceUv(float4 positionCS)
        {
        #if defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
            float2 preRotatedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
            switch (UNITY_DISPLAY_ORIENTATION_PRETRANSFORM)
            {
                default:
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_0:
                    return preRotatedScreenSpaceUV;
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_90:
                    return float2(1.0 - preRotatedScreenSpaceUV.y, preRotatedScreenSpaceUV.x);
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_180:
                    return float2(1.0 - preRotatedScreenSpaceUV.x, 1.0 - preRotatedScreenSpaceUV.y);
                case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_270:
                    return float2(preRotatedScreenSpaceUV.y, 1.0 - preRotatedScreenSpaceUV.x);
            }

            return preRotatedScreenSpaceUV;
        #else
            return GetNormalizedScreenSpaceUV(positionCS);
        #endif
        }

        float H8MasterBayer4(float2 positionCS)
        {
            uint2 p = (uint2)positionCS & 3u;
            uint2 lowBits = p & 1u;
            uint2 highBits = (p >> 1) & 1u;
            uint lowPattern = ((lowBits.x ^ lowBits.y) << 1) | lowBits.y;
            uint highPattern = ((highBits.x ^ highBits.y) << 1) | highBits.y;
            return (float)((lowPattern << 2) | highPattern) * 0.0625;
        }

        float2 H8MasterResolveParallaxUv(float2 uv, half3 viewDirWS, half3 normalWS, half4 tangentWS, half packedHeight, float quality)
        {
            float steps = floor(saturate(quality) * clamp(_MasterPomParams.y, 0.0, 16.0) + 0.5);
            if (steps <= 0.0)
                return uv;

            half3 tangent = H8MasterSafeNormalize(tangentWS.xyz, half3(1.0h, 0.0h, 0.0h));
            half3 normal = H8MasterSafeNormalize(normalWS, half3(0.0h, 1.0h, 0.0h));
            half3 bitangent = H8MasterSafeNormalize(cross(normal, tangent) * tangentWS.w, half3(0.0h, 0.0h, 1.0h));
            float2 viewTS = float2(dot(viewDirWS, tangent), dot(viewDirWS, bitangent));
            float height = saturate((float)packedHeight + _MasterPomParams.z - 0.5);
            float heightScale = max(_MasterPomParams.x, 0.0) * quality;
            float2 totalOffset = viewTS * (height * heightScale);
            float2 resolvedUv = uv;
            float invSteps = H8MasterSafeRcp(max(steps, 1.0));

            [loop]
            for (int i = 0; i < 16; i++)
            {
                float active = step((float)i, steps - 0.5);
                resolvedUv -= totalOffset * invSteps * active;
            }

            return resolvedUv;
        }

        half3 H8MasterNormalWS(Varyings input, float2 uv, float quality)
        {
            half normalScale = (half)max(_MasterSurfaceParams.w, _BumpScale);
            normalScale *= (half)lerp(0.62, 1.0, quality);
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), normalScale);
            half3 normal = H8MasterSafeNormalize(input.normalWS, half3(0.0h, 1.0h, 0.0h));
            half3 tangent = H8MasterSafeNormalize(input.tangentWS.xyz, half3(1.0h, 0.0h, 0.0h));
            half3 bitangent = H8MasterSafeNormalize(cross(normal, tangent) * input.tangentWS.w, half3(0.0h, 0.0h, 1.0h));
            float3x3 tangentToWorld = float3x3((float3)tangent, (float3)bitangent, (float3)normal);
            return H8MasterSafeNormalize((half3)TransformTangentToWorld(normalTS, tangentToWorld), normal);
        }

        half3 H8MasterLighting(Varyings input, half3 albedo, half3 normalWS, half metallic, half roughness, half occlusion, half emissionMask)
        {
            half3 viewDir = H8MasterSafeNormalize(input.viewDirWS, half3(0.0h, 0.0h, 1.0h));
            half ambientWeight = (half)saturate(_MasterNoirParams.x);

            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.positionCS = input.positionCS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = viewDir;
            inputData.fogCoord = input.fogFactor;
            inputData.normalizedScreenSpaceUV = H8MasterNormalizedScreenSpaceUv(input.positionCS);
            inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
            inputData.shadowCoord = input.shadowCoord;
            inputData.bakedGI = SampleSH(normalWS) * ambientWeight;

            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = albedo;
            surfaceData.metallic = metallic;
            surfaceData.specular = half3(0.04h, 0.04h, 0.04h) * (half)saturate(_MasterNoirParams.z);
            surfaceData.smoothness = saturate(1.0h - roughness);
            surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
            surfaceData.emission = _EmissionColor.rgb * emissionMask * (half)max(_MasterNoirParams.w, 0.0);
            surfaceData.occlusion = occlusion;
            surfaceData.alpha = 1.0h;
            surfaceData.clearCoatMask = 0.0h;
            surfaceData.clearCoatSmoothness = 0.0h;

            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            return color.rgb;
        }

        void H8MasterDecodeMaskLayout(half4 packedMask, out half metallicMask, out half roughnessMask, out half occlusionMask, out half emissionHeightMask)
        {
            half layout = (half)clamp(_MasterShadowParams.w, 0.0, 3.0);
            half mraoLayout = (half)saturate(1.0 - layout);
            half legacyMaskLayout = (half)saturate(1.0 - abs((float)layout - 1.0));
            half metallicGlossLayout = (half)saturate(1.0 - abs((float)layout - 2.0));
            half armLayout = (half)saturate(1.0 - abs((float)layout - 3.0));
            metallicMask = saturate(
                packedMask.r * mraoLayout +
                packedMask.r * legacyMaskLayout +
                packedMask.r * metallicGlossLayout +
                packedMask.b * armLayout);
            roughnessMask = saturate(
                packedMask.g * mraoLayout +
                (1.0h - packedMask.b) * legacyMaskLayout +
                (1.0h - packedMask.a) * metallicGlossLayout +
                packedMask.g * armLayout);
            occlusionMask = saturate(
                packedMask.b * mraoLayout +
                packedMask.g * legacyMaskLayout +
                metallicGlossLayout +
                packedMask.r * armLayout);
            emissionHeightMask = lerp(packedMask.a, 0.5h, metallicGlossLayout);
        }

        half4 H8MasterSampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        void H8MasterClipAlpha(half alpha, float4 positionCS, float quality)
        {
            half clipWeight = (half)saturate(_MasterAlphaParams.w);
            half cutoff = (half)max(_Cutoff, _MasterAlphaParams.x);
            half dither = (half)((H8MasterBayer4(positionCS.xy) - 0.5) * _MasterAlphaParams.z * (1.0 - quality)) * clipWeight;
            half clipValue = alpha - cutoff + dither;
            clip(lerp(1.0h, clipValue, clipWeight));
        }

        void H8MasterClipAlphaFromRawUv(float2 rawUv, float4 positionCS)
        {
            float quality = H8MasterQuality();
            half4 baseSample = H8MasterSampleBase(TRANSFORM_TEX(rawUv, _BaseMap));
            H8MasterClipAlpha(baseSample.a * (half)_MasterAlphaParams.y, positionCS, quality);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = H8MasterSafeNormalize((half3)normalInputs.normalWS, half3(0.0h, 1.0h, 0.0h));
            output.tangentWS = half4(H8MasterSafeNormalize((half3)normalInputs.tangentWS, half3(1.0h, 0.0h, 0.0h)), input.tangentOS.w);
            output.viewDirWS = H8MasterSafeNormalize((half3)GetWorldSpaceViewDir(positionInputs.positionWS), half3(0.0h, 0.0h, 1.0h));
            output.uv = input.uv;
            output.shadowCoord = GetShadowCoord(positionInputs);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float quality = H8MasterQuality();
            float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
            float2 maskUv = TRANSFORM_TEX(input.uv, _MaskMap);
            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUv);
            half metallicMask;
            half roughnessMask;
            half occlusionMask;
            half emissionHeightMask;
            H8MasterDecodeMaskLayout(packedMask, metallicMask, roughnessMask, occlusionMask, emissionHeightMask);
            float2 uv = H8MasterResolveParallaxUv(baseUv, input.viewDirWS, input.normalWS, input.tangentWS, emissionHeightMask, quality);
            float2 parallaxDelta = uv - baseUv;
            float2 parallaxRawDelta = parallaxDelta * H8MasterSafeRcp2(_BaseMap_ST.xy);
            half4 baseSample = H8MasterSampleBase(uv);
            half alpha = baseSample.a * (half)_MasterAlphaParams.y;
            H8MasterClipAlpha(alpha, input.positionCS, quality);

            half metallic = saturate(lerp((half)_Metallic, metallicMask, (half)saturate(_MasterSurfaceParams.x)));
            half roughness = saturate(lerp(1.0h - (half)_Smoothness, roughnessMask, (half)saturate(_MasterSurfaceParams.y)));
            half occlusion = lerp(1.0h, occlusionMask, (half)saturate(_MasterSurfaceParams.z * _OcclusionStrength));
            half emissionLayoutWeight = 1.0h - (half)saturate(1.0 - abs(clamp(_MasterShadowParams.w, 0.0, 3.0) - 2.0));
            half emissionMask = emissionHeightMask * emissionLayoutWeight * (half)saturate(_EmissionColor.a);
            half3 normalWS = H8MasterNormalWS(input, TRANSFORM_TEX(input.uv + parallaxRawDelta, _BumpMap), quality);
            half3 albedo = baseSample.rgb * lerp(1.0h - (half)_MasterShadowParams.z, 1.0h, (half)quality);
            half3 lit = H8MasterLighting(input, albedo, normalWS, metallic, roughness, occlusion, emissionMask);
            half3 finalColor = MixFog(lit, input.fogFactor);
            half outputAlpha = lerp(1.0h, alpha, (half)saturate(_MasterAlphaParams.w));
            return half4(finalColor, outputAlpha);
        }

        ShadowVaryings ShadowVert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            ShadowVaryings output = (ShadowVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
        #else
            float3 lightDirectionWS = _LightDirection;
        #endif
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            positionCS = ApplyShadowClamping(positionCS);
            output.positionCS = positionCS;
            output.uv = input.uv;
            return output;
        }

        half4 ShadowFrag(ShadowVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            H8MasterClipAlphaFromRawUv(input.uv, input.positionCS);
            return 0;
        }

        DepthVaryings DepthVert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            DepthVaryings output = (DepthVaryings)0;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        half4 DepthFrag(DepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            H8MasterClipAlphaFromRawUv(input.uv, input.positionCS);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            ENDHLSL
        }
    }

    FallBack Off
}

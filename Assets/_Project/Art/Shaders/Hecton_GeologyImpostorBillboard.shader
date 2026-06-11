Shader "Hecton8/Environment/Hecton_GeologyImpostorBillboard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _HectonImpostorAtlasRect ("Atlas Rect", Vector) = (0, 0, 1, 1)
        _HectonImpostorTintFlags ("Tint Flags", Vector) = (1, 1, 1, 1)
        _HectonImpostorProceduralDrawEnabled ("Procedural Draw Enabled", Float) = 0
        _H8GlobalQualityWeight ("Global Quality Weight", Range(0, 1)) = 1
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.45
        _AmbientFloor ("Ambient Floor", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardUnlitAtlas"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _HectonImpostorAtlasRect;
                float4 _HectonImpostorTintFlags;
                int _HectonImpostorDrawInstanceCount;
                int _HectonImpostorProceduralDrawEnabled;
                half _H8GlobalQualityWeight;
                half _AlphaClipThreshold;
                half _AmbientFloor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct HectonImpostorDrawInstance
            {
                float4 CenterWidth;
                float4 HeightFlags;
                float4 AtlasRect;
                float4 TintFlags;
            };

            StructuredBuffer<HectonImpostorDrawInstance> _HectonImpostorDrawInstances;

            float HectonGeologyFiniteOr(float value, float fallbackValue)
            {
                return isfinite(value) ? value : fallbackValue;
            }

            float2 HectonGeologyFiniteUv(float2 value)
            {
                return all(isfinite(value)) ? saturate(value) : float2(0.5, 0.5);
            }

            half4 HectonGeologyFiniteColor(half4 value, half4 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            half HectonGeologyQualityWeight01()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 1.0);
            }

            float4 HectonGeologySafeAtlasRect(float4 value)
            {
                bool valid = all(isfinite(value)) && value.z > 0.0 && value.w > 0.0;
                float4 safeValue = valid ? value : float4(0.0, 0.0, 1.0, 1.0);
                safeValue.zw = max(safeValue.zw, float2(0.0001, 0.0001));
                safeValue.xy = saturate(safeValue.xy);
                return safeValue;
            }

            float3 HectonGeologySafeNormalize(float3 value, float3 fallbackValue)
            {
                value = all(isfinite(value)) ? value : fallbackValue;
                float lengthSq = dot(value, value);
                return lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallbackValue;
            }

            float4 HectonResolveAtlasRect(uint drawInstanceID)
            {
                float4 rect = _HectonImpostorAtlasRect;
                if (_HectonImpostorProceduralDrawEnabled != 0 && drawInstanceID < (uint)_HectonImpostorDrawInstanceCount)
                    rect = _HectonImpostorDrawInstances[drawInstanceID].AtlasRect;
                return HectonGeologySafeAtlasRect(rect);
            }

            float4 HectonResolveTintFlags(uint drawInstanceID)
            {
                float4 tintFlags = _HectonImpostorTintFlags;
                if (_HectonImpostorProceduralDrawEnabled != 0 && drawInstanceID < (uint)_HectonImpostorDrawInstanceCount)
                    tintFlags = _HectonImpostorDrawInstances[drawInstanceID].TintFlags;
                return all(isfinite(tintFlags)) ? saturate(tintFlags) : float4(1.0, 1.0, 1.0, 1.0);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half3 tint : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float2 safeUv = HectonGeologyFiniteUv(input.uv);
                uint drawInstanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                drawInstanceID = unity_InstanceID;
            #endif
                float4 atlasRect = HectonResolveAtlasRect(drawInstanceID);
                float4 tintFlags = HectonResolveTintFlags(drawInstanceID);

                if (_HectonImpostorProceduralDrawEnabled != 0 && drawInstanceID < (uint)_HectonImpostorDrawInstanceCount)
                {
                    HectonImpostorDrawInstance instanceData = _HectonImpostorDrawInstances[drawInstanceID];
                    float3 centerWS = all(isfinite(instanceData.CenterWidth.xyz)) ? instanceData.CenterWidth.xyz : float3(0.0, 0.0, 0.0);
                    float width = max(0.25, HectonGeologyFiniteOr(instanceData.CenterWidth.w, 1.0));
                    float height = max(0.25, HectonGeologyFiniteOr(instanceData.HeightFlags.x, 1.0));
                    float2 quad = safeUv * 2.0 - 1.0;
                    if (dot(quad, quad) <= 0.000001)
                        quad = safePositionOS.xy * 2.0;

                    float3 cameraRight = HectonGeologySafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0, 0.0, 0.0));
                    float3 cameraUp = HectonGeologySafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0, 1.0, 0.0));
                    float3 positionWS = centerWS + (cameraRight * quad.x * width + cameraUp * quad.y * height) * 0.5;
                    output.positionCS = TransformWorldToHClip(positionWS);
                }
                else
                {
                    VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                    output.positionCS = positionInputs.positionCS;
                }

                output.uv = atlasRect.xy + (safeUv * atlasRect.zw);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.tint = (half3)tintFlags.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

                half4 baseColor = HectonGeologyFiniteColor(_BaseColor, half4(1.0h, 1.0h, 1.0h, 1.0h));
                baseColor.rgb *= input.tint;
                half4 albedoSample = HectonGeologyFiniteColor(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, HectonGeologyFiniteUv(input.uv)), half4(0.0h, 0.0h, 0.0h, 0.0h)) * baseColor;
                half qualityWeight = HectonGeologyQualityWeight01();
                half alphaClipThreshold = (half)saturate(HectonGeologyFiniteOr(_AlphaClipThreshold, 0.45));
                alphaClipThreshold = lerp(saturate(alphaClipThreshold + 0.08h), alphaClipThreshold, qualityWeight);
                clip(albedoSample.a - alphaClipThreshold);

                half ambientFloor = (half)saturate(HectonGeologyFiniteOr(_AmbientFloor, 0.18));
                half3 ambient = half3(ambientFloor, ambientFloor, ambientFloor);
                half3 color = albedoSample.rgb * ambient;
                color = MixFog(color, input.fogFactor);
                return HectonGeologyFiniteColor(half4(color, albedoSample.a), half4(0.0h, 0.0h, 0.0h, 0.0h));
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardUnlitAtlasFallback"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _HectonImpostorAtlasRect;
                float4 _HectonImpostorTintFlags;
                half _H8GlobalQualityWeight;
                half _AlphaClipThreshold;
                half _AmbientFloor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float2 HectonFallbackFiniteUv(float2 value)
            {
                return all(isfinite(value)) ? saturate(value) : float2(0.5, 0.5);
            }

            half4 HectonFallbackFiniteColor(half4 value, half4 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float4 HectonFallbackSafeAtlasRect(float4 value)
            {
                bool valid = all(isfinite(value)) && value.z > 0.0 && value.w > 0.0;
                float4 safeValue = valid ? value : float4(0.0, 0.0, 1.0, 1.0);
                safeValue.xy = saturate(safeValue.xy);
                safeValue.zw = max(safeValue.zw, float2(0.0001, 0.0001));
                return safeValue;
            }

            half HectonFallbackQualityWeight01()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 1.0);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half3 tint : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                float4 atlasRect = HectonFallbackSafeAtlasRect(_HectonImpostorAtlasRect);
                float4 tintFlags = all(isfinite(_HectonImpostorTintFlags)) ? saturate(_HectonImpostorTintFlags) : float4(1.0, 1.0, 1.0, 1.0);
                output.positionCS = positionInputs.positionCS;
                output.uv = atlasRect.xy + (HectonFallbackFiniteUv(input.uv) * atlasRect.zw);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.tint = (half3)tintFlags.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

                half4 baseColor = HectonFallbackFiniteColor(_BaseColor, half4(1.0h, 1.0h, 1.0h, 1.0h));
                half4 albedoSample = HectonFallbackFiniteColor(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, HectonFallbackFiniteUv(input.uv)), half4(0.0h, 0.0h, 0.0h, 0.0h));
                half qualityWeight = HectonFallbackQualityWeight01();
                half alphaClipThreshold = (half)saturate(isfinite(_AlphaClipThreshold) ? _AlphaClipThreshold : 0.45);
                alphaClipThreshold = lerp(saturate(alphaClipThreshold + 0.08h), alphaClipThreshold, qualityWeight);
                albedoSample *= baseColor;
                albedoSample.rgb *= input.tint;
                clip(albedoSample.a - alphaClipThreshold);

                half ambientFloor = (half)saturate(isfinite(_AmbientFloor) ? _AmbientFloor : 0.18);
                half3 color = albedoSample.rgb * half3(ambientFloor, ambientFloor, ambientFloor);
                color = MixFog(color, input.fogFactor);
                return HectonFallbackFiniteColor(half4(color, albedoSample.a), half4(0.0h, 0.0h, 0.0h, 0.0h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

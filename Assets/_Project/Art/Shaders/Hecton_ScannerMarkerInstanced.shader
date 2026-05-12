Shader "HECTON/Scanner/MarkerInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 0.9, 1.0, 0.9)
        _FlickerFrequency ("Flicker Frequency", Float) = 25
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.4)) = 0.15
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.08
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.12
        _OccludedColor ("Occluded Color", Color) = (0.9, 0.42, 0.08, 0.74)
        _OccludedBoost ("Occluded Boost", Range(0, 2)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+20"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "OccludedResource"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
            ZWrite Off
            ZTest Greater
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _FlickerFrequency;
                float _FlickerIntensity;
                float _EdgeWidth;
                float _FillAlpha;
                float4 _OccludedColor;
                float _OccludedBoost;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceData)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

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
                float instanceAlpha : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonTemporalFlicker01(float timeSeconds, float speed, float phaseOffset)
            {
                float hash = frac((timeSeconds * max(speed, 0.001) + phaseOffset) * 0.1031);
                hash *= hash + 33.33;
                hash *= hash + hash;
                return frac(hash);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceData);
                output.instanceAlpha = lerp(1.0, saturate(instanceData.x), saturate(instanceData.y));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float instanceAlpha = saturate(input.instanceAlpha);
                float2 centered = input.uv * 2.0 - 1.0;
                float diamond = abs(centered.x) + abs(centered.y);
                float border = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, abs(diamond - 1.0));
                float fill = smoothstep(1.0, 0.82, diamond) * _FillAlpha * instanceAlpha;
                float flicker01 = HectonTemporalFlicker01(_Time.y, _FlickerFrequency, dot(input.uv, float2(7.17, 13.31)));
                float flicker = 1.0 - (_FlickerIntensity * 2.0) + (_FlickerIntensity * 2.0 * flicker01);
                float alpha = saturate((border + fill) * _OccludedColor.a * flicker * _OccludedBoost * instanceAlpha);
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.0005));
                return half4(_OccludedColor.rgb * alpha, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _FlickerFrequency;
                float _FlickerIntensity;
                float _EdgeWidth;
                float _FillAlpha;
                float4 _OccludedColor;
                float _OccludedBoost;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceData)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

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
                float instanceAlpha : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonTemporalFlicker01(float timeSeconds, float speed, float phaseOffset)
            {
                float hash = frac((timeSeconds * max(speed, 0.001) + phaseOffset) * 0.1031);
                hash *= hash + 33.33;
                hash *= hash + hash;
                return frac(hash);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceData);
                output.instanceAlpha = lerp(1.0, saturate(instanceData.x), saturate(instanceData.y));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float instanceAlpha = saturate(input.instanceAlpha);
                float2 centered = input.uv * 2.0 - 1.0;
                float diamond = abs(centered.x) + abs(centered.y);
                float border = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, abs(diamond - 1.0));
                float fill = smoothstep(1.0, 0.82, diamond) * _FillAlpha * instanceAlpha;
                float flicker01 = HectonTemporalFlicker01(_Time.y, _FlickerFrequency, dot(input.uv, float2(7.17, 13.31)));
                float flicker = 1.0 - (_FlickerIntensity * 2.0) + (_FlickerIntensity * 2.0 * flicker01);
                float alpha = saturate((border + fill) * _BaseColor.a * flicker * instanceAlpha);
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.0005));
                return half4(_BaseColor.rgb * alpha, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

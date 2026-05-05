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
            "RenderType"="Transparent"
            "Queue"="Transparent+20"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "OccludedResource"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest Greater
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float diamond = abs(centered.x) + abs(centered.y);
                float border = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, abs(diamond - 1.0));
                float fill = smoothstep(1.0, 0.82, diamond) * _FillAlpha;
                float flicker = 1.0 - _FlickerIntensity + _FlickerIntensity * sin(_Time.y * _FlickerFrequency * 6.2831853);
                float alpha = saturate((border + fill) * _OccludedColor.a * flicker * _OccludedBoost);
                return half4(_OccludedColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float diamond = abs(centered.x) + abs(centered.y);
                float border = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, abs(diamond - 1.0));
                float fill = smoothstep(1.0, 0.82, diamond) * _FillAlpha;
                float flicker = 1.0 - _FlickerIntensity + _FlickerIntensity * sin(_Time.y * _FlickerFrequency * 6.2831853);
                float alpha = saturate((border + fill) * _BaseColor.a * flicker);
                return half4(_BaseColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}

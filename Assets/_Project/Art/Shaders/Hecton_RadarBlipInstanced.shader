Shader "HECTON/HUD/RadarBlipInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 0.24, 0.28, 0.92)
        _FlickerFrequency ("Flicker Frequency", Float) = 18
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.4)) = 0.18
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.08
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.36
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
            Name "HUDForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

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

            float HectonFastTriangleSine(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
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
                float flicker = 1.0 - _FlickerIntensity + _FlickerIntensity * HectonFastTriangleSine(_Time.y * _FlickerFrequency * 6.2831853);
                float alpha = saturate((border + fill) * _BaseColor.a * flicker * instanceAlpha);
                return half4(_BaseColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

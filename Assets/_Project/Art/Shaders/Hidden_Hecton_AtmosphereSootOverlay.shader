Shader "Hidden/Hecton8/AtmosphereSootOverlay"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "AtmosphereSootOverlay"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(HectonAtmosphereSootGlobals)
                float4 _HectonAtmosphereSootParams;
                float4 _HectonAtmosphereSootCenter;
            CBUFFER_END

            TEXTURE2D_X(_BlitTexture);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
                float2 sootRadial : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                float aspect = max(1.0, _HectonAtmosphereSootCenter.z);
                output.sootRadial = float2(
                    output.screenUV.x * aspect - _HectonAtmosphereSootCenter.w,
                    output.screenUV.y - _HectonAtmosphereSootCenter.y);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float intensity = saturate(_HectonAtmosphereSootParams.x);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.screenUV);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (intensity <= 0.0001)
                    return color;

                float radius = max(0.001, _HectonAtmosphereSootParams.y);
                float ditherStrength = saturate(_HectonAtmosphereSootParams.z);
                float darkenStrength = saturate(_HectonAtmosphereSootParams.w);
                float radiusSq = radius * radius;
                float local = saturate(1.0 - (dot(input.sootRadial, input.sootRadial) / max(0.000001, radiusSq)));
                local = local * local * (3.0 - (2.0 * local));

                float2 pixel = floor(input.screenUV * _ScreenParams.xy);
                float dither = Hash21(pixel);
                float sootGate = step(dither, saturate(local * ditherStrength));
                half soot = (half)(sootGate * local * intensity);
                half darken = (half)(local * intensity * darkenStrength);
                half3 sootTint = half3(0.018h, 0.015h, 0.012h);

                color.rgb = lerp(color.rgb, color.rgb * (1.0h - darken), (half)intensity);
                color.rgb = lerp(color.rgb, sootTint, soot * 0.42h);
                return color;
            }
            ENDHLSL
        }
    }
}

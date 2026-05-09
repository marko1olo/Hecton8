Shader "Hecton8/UI/IGNDitheredBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OpacityScale ("Opacity Scale", Range(0, 1)) = 1
        _DitherBias ("Dither Bias", Range(-0.25, 0.25)) = 0
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One Zero
        ColorMask RGB

        Pass
        {
            Name "IGNDitherClip"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile _ _HUD_PHOSPHOR_MODE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 maskPosition : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _OpacityScale;
                float _DitherBias;
                float4 _ClipRect;
            CBUFFER_END

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.vertex);
                output.maskPosition = input.vertex;
                return output;
            }

            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveTemporalIgnOffset()
            {
                float temporalPhase = fmod(floor(_Time.y * 60.0), 3.0);
                return temporalPhase * float2(19.0, 47.0);
            }

            float UnityGet2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(UNITY_UI_CLIP_RECT)
                clip(UnityGet2DClipping(input.maskPosition.xy, _ClipRect) - 0.5);
#endif
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                screenUv = UnityStereoTransformScreenSpaceTex(screenUv);
#endif
                float threshold = InterleavedGradientNoise(floor(screenUv * _ScreenParams.xy) + ResolveTemporalIgnOffset());
#if defined(_HUD_PHOSPHOR_MODE)
                float coverage = saturate(input.color.a * _OpacityScale + _DitherBias);
                float ditherAlpha = step(threshold, coverage);
                clip(ditherAlpha - 0.5);
                return half4(0.0, 1.0, 0.0, ditherAlpha);
#else
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                half4 color = texel * input.color;
                float coverage = saturate(color.a * _OpacityScale + _DitherBias);
                clip(coverage - threshold);
                return half4(color.rgb, 1.0);
#endif
            }
            ENDHLSL
        }
    }
}

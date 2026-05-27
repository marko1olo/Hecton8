Shader "Hidden/Hecton8/VRBrownout"
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
            Name "VRBrownout"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            CBUFFER_START(HectonVRBrownoutGlobals)
                float4 _HectonVRBrownoutParams0;
                float4 _HectonVRBrownoutParams1;
                float4 _HectonVrComfortSignals;
                float4 _HectonVrComfortMotion;
            CBUFFER_END

            #define _HectonVRBrownoutIntensity _HectonVRBrownoutParams0.x
            #define _HectonWorldFocusBlur _HectonVRBrownoutParams0.y
            #define _HectonVRNearCollisionIntensity _HectonVRBrownoutParams0.z
            #define _HectonWorldBlurTexelRadius _HectonVRBrownoutParams0.w
            #define _HectonVRBrownoutScanlineStrength _HectonVRBrownoutParams1.x
            #define _HectonVRBrownoutDitherStrength _HectonVRBrownoutParams1.y

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;

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

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(uv);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float ResolveEyeStableNoiseSeed()
            {
                float frame = floor(_Time.y * 60.0);
                return frame - (floor(frame * 0.33333334) * 3.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float brownout = saturate(_HectonVRBrownoutIntensity);
                float worldBlur = saturate(_HectonWorldFocusBlur);
                float nearCollision = saturate(_HectonVRNearCollisionIntensity);
                float2 linearUv = UnityStereoTransformScreenSpaceTex(input.screenUV);
                float2 cameraTextureUv = ResolveFoveatedSourceUV(linearUv);
                float2 sampleUv = linearUv;
                float2 eyeStableUv = input.screenUV;
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float vrComfortTunnel = saturate(max(_HectonVrComfortSignals.x, _HectonVrComfortMotion.z)) * vrComfortEnabled;
                float vrComfortBlackout = saturate(_HectonVrComfortSignals.y) * vrComfortEnabled;
                float vrComfortPeripheralBlur = saturate(_HectonVrComfortSignals.z) * vrComfortEnabled;
                float2 radialOffset = eyeStableUv * 2.0 - 1.0;
                radialOffset.x *= _ScreenParams.x * rcp(max(_ScreenParams.y, 1.0));
                float radialMagnitudeSq = saturate(dot(radialOffset, radialOffset));
                float vrComfortEdge = saturate((radialMagnitudeSq - 0.1296) * 1.1485452);
                [branch]
                if (brownout <= 0.0001 &&
                    worldBlur <= 0.0001 &&
                    nearCollision <= 0.0001 &&
                    vrComfortTunnel <= 0.0001 &&
                    vrComfortBlackout <= 0.0001 &&
                    vrComfortPeripheralBlur <= 0.0001)
                {
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, cameraTextureUv);
                }

                float eyeStableSeed = 0.0;
                float row = 0.0;
                [branch]
                if (brownout > 0.0001 || vrComfortTunnel > 0.0001 || vrComfortBlackout > 0.0001)
                {
                    eyeStableSeed = ResolveEyeStableNoiseSeed();
                    row = floor(eyeStableUv.y * _ScreenParams.y * 0.5);
                    [branch]
                    if (brownout > 0.0001)
                    {
                        float rowNoise = Hash21(float2(row, eyeStableSeed));
                        float rowGate = step(0.62, rowNoise);
                        sampleUv.x += (rowNoise - 0.5) * brownout * rowGate * 0.0075;
                        sampleUv = saturate(sampleUv);
                    }
                }

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(sampleUv));
                [branch]
                if (worldBlur > 0.0001 || vrComfortPeripheralBlur > 0.0001)
                {
                    float blurMix = saturate(max(worldBlur, vrComfortPeripheralBlur * vrComfortEdge));
                    float2 blurStep = _BlitTexture_TexelSize.xy * max(0.0, _HectonWorldBlurTexelRadius) * blurMix;
                    half4 blurColor = color;
                    blurColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(sampleUv + blurStep)));
                    blurColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(sampleUv - blurStep)));
                    blurColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(sampleUv + blurStep.yx)));
                    blurColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(sampleUv - blurStep.yx)));
                    color = lerp(color, blurColor * 0.2h, (half)blurMix);
                }

                [branch]
                if (brownout > 0.0001)
                {
                    float2 pixel = floor(eyeStableUv * _ScreenParams.xy);
                    float scanPhase = frac((row * 0.5) + (eyeStableSeed * 0.33333334));
                    float scanline = lerp(1.0, 0.52 + 0.48 * step(0.42, scanPhase), saturate(_HectonVRBrownoutScanlineStrength));
                    float dither = Hash21(pixel + float2(eyeStableSeed * 37.0, row));
                    half luminance = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    float threshold = lerp(0.18, dither, saturate(_HectonVRBrownoutDitherStrength));
                    float bit = step(threshold, saturate(luminance * (1.35 + brownout * 0.85) * scanline));
                    half3 biosGreen = (half3(0.015h, 0.92h, 0.19h) * (half)bit) + half3(0.0h, 0.018h, 0.004h);
                    half phosphorTail = (half)(Hash21(float2(row * 0.25, eyeStableSeed + 13.0)) * 0.028 * brownout);
                    biosGreen += half3(0.0h, phosphorTail, phosphorTail * 0.18h);
                    color.rgb = lerp(color.rgb, biosGreen, (half)brownout);
                }

                [branch]
                if (nearCollision > 0.0001)
                {
                    if (brownout <= 0.0001)
                        eyeStableSeed = ResolveEyeStableNoiseSeed();

                    float2 pixel = floor(input.screenUV * _ScreenParams.xy);
                    float ign = frac(52.9829189 * frac(dot(pixel + eyeStableSeed * 17.0, float2(0.06711056, 0.00583715))));
                    float crawl = Hash21(pixel * 0.25 + float2(_Time.y * 31.0, eyeStableSeed * 19.0));
                    float gate = step(ign, lerp(0.24, 0.92, nearCollision));
                    half scan = (half)lerp(0.72, 1.24, step(0.48, frac(pixel.y * 0.5 + eyeStableSeed)));
                    half3 darkRed = half3(0.065h, 0.006h, 0.003h);
                    half3 redStatic =
                        darkRed +
                        (half3(0.94h, 0.034h, 0.018h) * (half)gate * scan) +
                        half3((half)(crawl * 0.05 * nearCollision), 0.004h, 0.002h);
                    color.rgb = lerp(color.rgb, redStatic, (half)saturate(nearCollision * (0.78 + gate * 0.22)));
                }

                [branch]
                if (vrComfortTunnel > 0.0001 || vrComfortBlackout > 0.0001)
                {
                    float2 pixel = floor(eyeStableUv * _ScreenParams.xy);
                    float ign = frac(52.9829189 * frac(dot(pixel + eyeStableSeed * 23.0, float2(0.06711056, 0.00583715))));
                    float tunnelInner = lerp(0.74, 0.34, vrComfortTunnel);
                    float tunnelInnerSq = tunnelInner * tunnelInner;
                    float tunnelMask = saturate((radialMagnitudeSq - tunnelInnerSq) * rcp(max(1.0 - tunnelInnerSq, 0.0009765625))) * vrComfortTunnel;
                    float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                    float ditheredTunnel = tunnelMask * lerp(0.50, 0.96, tunnelDither);
                    half blackAmount = (half)saturate(max(ditheredTunnel, vrComfortBlackout));
                    color.rgb = lerp(color.rgb, half3(0.0015h, 0.0023h, 0.0031h), blackAmount);
                }

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

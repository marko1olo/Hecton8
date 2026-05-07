Shader "Hidden/Hecton8/BiosDiagnostic"
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
            Name "BiosDiagnostic"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HectonBiosDiagnosticIntensity;
                float _HectonBiosLootActive;
                float _HectonBiosDitherStrength;
                float _HectonBiosScanlineStrength;
                float4 _HectonBiosLootSphere;
            CBUFFER_END

            float4 _TotalUniverseOffset;
            TEXTURE2D_X(_BlitTexture);

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

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453123);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = UnityStereoTransformScreenSpaceTex(input.screenUV);
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 pixel = floor(uv * _ScreenParams.xy);
                float scanline = lerp(1.0, 0.54 + 0.46 * step(0.5, frac(pixel.y * 0.5)), saturate(_HectonBiosScanlineStrength));
                float dither = Hash21(pixel + floor(_Time.y * 48.0));
                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                float threshold = lerp(0.18, dither, saturate(_HectonBiosDitherStrength));
                half bit = (half)step(threshold, saturate(luminance * 1.42 * scanline));

                float depth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 absoluteWorld = worldPos + _TotalUniverseOffset.xyz;
                float radius = max(0.1, _HectonBiosLootSphere.w);
                float3 lootDelta = absoluteWorld - _HectonBiosLootSphere.xyz;
                float distSq = dot(lootDelta, lootDelta);
                float innerRadius = radius * 0.72;
                float loot = _HectonBiosLootActive * (1.0 - smoothstep(innerRadius * innerRadius, radius * radius, distSq));

                float crawl = frac(sin(_Time.y * 37.0) * 43758.5453123);
                half3 baseGreen = half3(0.0h, 0.018h, 0.004h) + half3(0.018h, 0.88h, 0.16h) * bit;
                half3 lootGreen = half3(0.08h, 1.0h, 0.2h) * (half)(loot * (0.68 + crawl * 0.32));
                half3 bios = max(baseGreen, lootGreen);
                source.rgb = lerp(source.rgb, bios, (half)saturate(_HectonBiosDiagnosticIntensity));
                return source;
            }
            ENDHLSL
        }
    }
}

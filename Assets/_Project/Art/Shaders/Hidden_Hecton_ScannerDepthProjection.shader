Shader "Hidden/Hecton8/ScannerDepthProjection"
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
            Name "ScannerDepthProjection"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HectonScannerProjectionOriginRadius;
                float4 _HectonScannerProjectionRightDepth;
                float4 _HectonScannerProjectionUpAge;
                float4 _HectonScannerProjectionForwardIntensity;
                half4 _HectonScannerProjectionColor;
                float _HectonScannerProjectionGridScale;
                float _HectonScannerProjectionDitherCutoff;
                float _HectonScannerProjectionFlickerSpeed;
            CBUFFER_END

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
                float depth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float3 origin = _HectonScannerProjectionOriginRadius.xyz;
                float radius = max(0.1, _HectonScannerProjectionOriginRadius.w);
                float3 rightAxis = normalize(_HectonScannerProjectionRightDepth.xyz);
                float3 upAxis = normalize(_HectonScannerProjectionUpAge.xyz);
                float3 forwardAxis = normalize(_HectonScannerProjectionForwardIntensity.xyz);
                float projectionDepth = max(0.001, _HectonScannerProjectionRightDepth.w);
                float age01 = saturate(_HectonScannerProjectionUpAge.w);
                float intensity = saturate(_HectonScannerProjectionForwardIntensity.w);

                float3 delta = worldPos - origin;
                float forwardMeters = dot(delta, forwardAxis);
                float2 projector = float2(dot(delta, rightAxis), dot(delta, upAxis)) / radius;
                float radialSq = dot(projector, projector);
                float insideRadius = 1.0 - smoothstep(0.6724, 1.0, radialSq);
                float insideDepth = smoothstep(0.0, 0.12, forwardMeters) * (1.0 - smoothstep(projectionDepth * 0.72, projectionDepth, forwardMeters));
                float2 gridUv = projector * _HectonScannerProjectionGridScale;
                float2 cell = floor(gridUv);
                float2 local = abs(frac(gridUv) - 0.5);
                float wire = 1.0 - smoothstep(0.35, 0.49, max(local.x, local.y));
                float dither = Hash21(cell + floor(_Time.y * _HectonScannerProjectionFlickerSpeed));
                float temporal = frac(sin(_Time.y * _HectonScannerProjectionFlickerSpeed) * 43758.5453123);
                float gate = step(_HectonScannerProjectionDitherCutoff, min(dither, temporal));
                float fade = (1.0 - age01) * intensity;
                half mask = (half)saturate(insideRadius * insideDepth * wire * gate * fade);
                half3 color = source.rgb + _HectonScannerProjectionColor.rgb * (_HectonScannerProjectionColor.a * mask);
                return half4(color, source.a);
            }
            ENDHLSL
        }
    }
}

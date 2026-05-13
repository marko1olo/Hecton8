Shader "Hecton8/PDA/FrequencyTuningWave"
{
    Properties
    {
        _HectonFrequencyTuningTubeRadius ("Tube Radius", Float) = 0.003
    }

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
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FrequencyTuningWave"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct FrequencyTuningWaveGpuSegment
            {
                float4 CenterRadius;
                float4 TangentLength;
                float4 ColorStage;
            };

            StructuredBuffer<FrequencyTuningWaveGpuSegment> _HectonFrequencyTuningSegments;
            float4x4 _HectonFrequencyTuningLocalToWorld;
            float _HectonFrequencyTuningTubeRadius;
            float4 _HectonFrequencyTuningTimeErrorStage;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                FrequencyTuningWaveGpuSegment segment = _HectonFrequencyTuningSegments[input.instanceID];
                float pulse = 1.0 + saturate(1.0 - _HectonFrequencyTuningTimeErrorStage.y) * 0.22;
                float2 tangent = segment.TangentLength.xy;
                float2 normal = float2(-tangent.y, tangent.x);
                float tubeRadius = _HectonFrequencyTuningTubeRadius * segment.CenterRadius.w * pulse;
                float2 local2 = segment.CenterRadius.xy +
                    tangent * (input.positionOS.x * segment.TangentLength.w) +
                    normal * (input.positionOS.y * tubeRadius * 2.0);
                float3 local = float3(local2, segment.CenterRadius.z);
                float3 world = mul(_HectonFrequencyTuningLocalToWorld, float4(local, 1.0)).xyz;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.color = half4(segment.ColorStage);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                half sideMask = (half)(1.0 - smoothstep(0.72, 1.0, abs(centered.y)));
                half mask = sideMask;
                half alpha = input.color.a * mask;
                return half4(input.color.rgb * (0.72h + mask * 0.55h), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

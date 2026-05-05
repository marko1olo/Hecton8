Shader "Hecton8/VFX/MarineSnow"
{
    Properties
    {
        _MarineSnowTint ("Marine Snow Tint", Color) = (0.54, 0.61, 0.58, 0.55)
        _MarineSnowRenderParams ("Render Params", Vector) = (0.55, 3.2, 18.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 Pos;
                float Life;
                float3 Vel;
                float Size;
                float3 PrevPos;
                uint Flags;
                float2 UV;
                float2 Pad;
            };

            StructuredBuffer<Particle> _MarineSnowParticles;

            struct MarineSnowFrameData
            {
                float4 CameraPositionTime;
                float4 CameraRightDeltaTime;
                float4 CameraUpDensity;
                float4 FlowFieldCenterCellSize;
                float4 ShellParams;
                float4 MetaParams;
            };

            StructuredBuffer<MarineSnowFrameData> _HectonMarineSnowFrame;

            #define _MarineSnowCameraPosition_Time (_HectonMarineSnowFrame[0].CameraPositionTime)
            #define _MarineSnowCameraRight_DeltaTime (_HectonMarineSnowFrame[0].CameraRightDeltaTime)
            #define _MarineSnowCameraUp_Density (_HectonMarineSnowFrame[0].CameraUpDensity)
            #define _MarineSnowFlowFieldCenterCellSize (_HectonMarineSnowFrame[0].FlowFieldCenterCellSize)
            #define _MarineSnowShellParams (_HectonMarineSnowFrame[0].ShellParams)
            #define _MarineSnowMetaParams (_HectonMarineSnowFrame[0].MetaParams)

            float4 _MarineSnowTint;
            float4 _MarineSnowRenderParams;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            float2 ResolveQuadCorner(uint vertexID)
            {
                if (vertexID == 0) return float2(-1.0, -1.0);
                if (vertexID == 1) return float2(-1.0,  1.0);
                if (vertexID == 2) return float2( 1.0,  1.0);
                if (vertexID == 3) return float2(-1.0, -1.0);
                if (vertexID == 4) return float2( 1.0,  1.0);
                return float2(1.0, -1.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                Particle particle = _MarineSnowParticles[input.instanceID];
                float active = step(0.5, _MarineSnowMetaParams.w);
                float densityScale = saturate(_MarineSnowCameraUp_Density.w);
                float2 corner = ResolveQuadCorner(input.vertexID);
                float3 cameraRight = _MarineSnowCameraRight_DeltaTime.xyz;
                float3 cameraUp = _MarineSnowCameraUp_Density.xyz;
                float maxDistance = max(_MarineSnowRenderParams.z, 0.25);
                float distanceFade = saturate(1.0 - distance(particle.Pos, _MarineSnowCameraPosition_Time.xyz) / maxDistance);
                float isBubble = ((particle.Flags & 1u) != 0u) ? 1.0 : 0.0;
                float size = particle.Size * lerp(0.65, 1.0, distanceFade) * lerp(1.0, 1.65, isBubble);
                float3 billboardOffset = (cameraRight * corner.x + cameraUp * corner.y) * size;
                float3 worldPosition = particle.Pos + billboardOffset;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.color = lerp(_MarineSnowTint, float4(0.72, 0.88, 0.94, _MarineSnowTint.a * 0.72), isBubble);
                output.color.a *= active * densityScale * particle.Life * distanceFade;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = pow(radial, _MarineSnowRenderParams.y) * input.color.a;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

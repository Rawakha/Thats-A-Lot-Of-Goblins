// Flying blood droplets (Effect_BloodSpray): small chunky blobs drawn GPU instanced, unlit so they stay bright and
// readable from the plane, a touch lighter on the side facing up. No shadows.
//
// Instancing must be on (Effect_BloodSpray turns it on for its material).
Shader "Goblins/Blood Droplet"
{
    Properties
    {
        _Color ("Color", Color) = (0.69, 0.15, 0.62, 1)
        _TopLight ("Top Light", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _TopLight;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float up : TEXCOORD0;
                float fog : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.position = TransformObjectToHClip(input.position.xyz);
                output.up = TransformObjectToWorldNormal(input.normal).y;
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 colour = _Color.rgb * (1.0 + saturate(input.up) * _TopLight);
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
}

// Blood splats on the ground (Effect_BloodSplats): flat quads drawn GPU instanced, each a blobby circle worked out
// in the shader, so there's no texture and no per-splat object. Each splat gets its own shape and shade from a hash of
// where it is, so no two look alike: a wobbly edge (Edge Wobble, a few lobes at random angles), a darker or lighter
// red (Shade Variation) and a slightly darker rim. Drawn just above the ground (depth offset), unlit, no shadows.
//
// Each splat's opacity comes in on its matrix's Y scale (a flat quad doesn't use it), so fading needs no extra per-instance
// data.
//
// Instancing must be on (Effect_BloodSplats turns it on for its material).
Shader "Goblins/Blood Splat"
{
    Properties
    {
        _Color ("Color (alpha is the opacity)", Color) = (0.69, 0.15, 0.62, 0.7)
        _RimColor ("Rim Color", Color) = (0.36, 0.05, 0.34, 1)
        _RimWidth ("Rim Width", Range(0, 0.5)) = 0.12
        _EdgeWobble ("Edge Wobble", Range(0, 0.4)) = 0.18
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.03
        _ShadeVariation ("Shade Variation", Range(0, 0.5)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                half _RimWidth;
                half _EdgeWobble;
                half _Softness;
                half _ShadeVariation;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float seed : TEXCOORD1;
                float fade : TEXCOORD3;
                float fog : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // The splat's own random number, from where it lies
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = Hash(centre.xz);
                output.fade = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));

                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.uv;
                float angle = atan2(p.y, p.x);
                float seed = input.seed * 6.2831;

                // A blob: a circle whose edge bulges in a few lobes at angles of its own
                float wobble = sin(angle * 3.0 + seed) * 0.5 + sin(angle * 5.0 - seed * 1.7) * 0.3 + sin(angle * 9.0 + seed * 3.1) * 0.2;
                float radius = 0.75 + wobble * _EdgeWobble;
                float distance = length(p);
                float alpha = 1.0 - smoothstep(radius - _Softness, radius, distance);

                if (alpha <= 0.001)
                    discard;

                // Darker toward the rim, each splat a little darker or lighter
                float rim = smoothstep(radius - _RimWidth, radius, distance);
                half3 colour = lerp(_Color.rgb, _RimColor.rgb, rim);
                colour *= 1.0 + (input.seed - 0.5) * 2.0 * _ShadeVariation;
                colour = MixFog(colour, input.fog);

                return half4(colour, alpha * _Color.a * saturate(input.fade));
            }
            ENDHLSL
        }
    }
}

// Chunky grass for Level_Grass: clumps of squarish, flat-shaded blades drawn GPU instanced. Each blade fades from
// Base Color at the root to Tip Color at the top (the mesh's UV.y is the height up the blade), each clump is a little
// lighter or darker (Color Variation, from a hash of where it stands), and the tops sway in the wind (stronger the
// higher up the blade, in slow waves rolling across the field). Soft-lit like the level shader: Lighting Influence
// splits it between flat colour and the main light, including cookies. No mesh shadows cast or received.
//
// Instancing must be on (Level_Grass turns it on for its material).
Shader "Goblins/Grass"
{
    Properties
    {
        [Header(Colour)]
        _BaseColor ("Base Color", Color) = (0.22, 0.12, 0.32, 1)
        _TipColor ("Tip Color", Color) = (0.62, 0.42, 0.82, 1)
        _ColorVariation ("Color Variation", Range(0, 0.5)) = 0.15
        _GradientPower ("Gradient Power", Range(0.2, 4)) = 1.2
        _LightingInfluence ("Lighting Influence", Range(0, 1)) = 0.5

        [Header(Wind)]
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.15
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.2
        _WindScale ("Wind Wave Size", Range(1, 60)) = 12
        _WindDirection ("Wind Direction (XZ)", Vector) = (1, 0, 0.3, 0)
        _WindFlutter ("Flutter", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TipColor;
            half _ColorVariation;
            half _GradientPower;
            half _LightingInfluence;
            half _WindStrength;
            half _WindSpeed;
            half _WindScale;
            float4 _WindDirection;
            half _WindFlutter;
        CBUFFER_END

        struct Attributes
        {
            float4 position : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 position : SV_POSITION;
            float3 normal : TEXCOORD0;
            float height : TEXCOORD1;
            float shade : TEXCOORD2;
            float fog : TEXCOORD3;
            float3 positionWS : TEXCOORD4;
        };

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);

            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            float3 world = TransformObjectToWorld(input.position.xyz);
            float height = input.uv.y;

            // Wind: slow waves rolling across the field along the wind, plus a quicker flutter per clump; only the
            // upper blade moves (none at the root)
            float2 direction = normalize(_WindDirection.xz + 0.0001);
            float seed = Hash(root.xz);
            float wave = sin(dot(root.xz, direction) / _WindScale - _Time.y * _WindSpeed);
            float flutter = sin(_Time.y * _WindSpeed * 3.1 + seed * 6.2831) * _WindFlutter;
            float bend = (wave * 0.5 + 0.5 + flutter * 0.5) * _WindStrength * height * height;
            world.xz += direction * bend;
            world.y -= bend * bend * 0.3;   // bending over shortens it a little

            output.position = TransformWorldToHClip(world);
            output.positionWS = world;
            output.normal = TransformObjectToWorldNormal(input.normal);
            output.height = height;
            output.shade = seed;
            output.fog = ComputeFogFactor(output.position.z);
            return output;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 colour = lerp(_BaseColor.rgb, _TipColor.rgb, pow(saturate(input.height), _GradientPower));
                colour *= 1.0 + (input.shade - 0.5) * 2.0 * _ColorVariation;

                // Soft light: flat faces catch the sun differently, which gives the chunky facets
                Light light = GetMainLight();
            #if defined(_LIGHT_COOKIES)
                light.color *= SampleMainLightCookie(input.positionWS);
            #endif
                half diffuse = saturate(dot(normalize(input.normal), light.direction)) * 0.75 + 0.25;
                half3 lit = colour * (diffuse * light.color + SampleSH(normalize(input.normal)) * 0.5);
                colour = lerp(colour, lit, _LightingInfluence);

                colour = MixFog(colour, input.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        // Depth for SSAO and anything reading the depth texture, with the same wind so it lines up
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            half4 DepthFragment(Varyings input) : SV_Target
            {
                return input.position.z;
            }
            ENDHLSL
        }
    }
}

// Goblin gibs (Horde_GibSet chunks), drawn GPU instanced: the outside in each goblin's own skin colour (per instance,
// _SkinColor), the cut faces (red in the mesh's vertex colours) in Gore Color, darker toward the middle of the wound.
// Green in the vertex colours is drawn dark: the X eyes on a popped head. Each chunk wobbles like jelly as it flies (a
// soft squash-and-stretch pulse, its own timing from _Seed, its strength from _WobbleAmount, 0 at rest), so it reads as
// a lump of goblin, not a shard. Soft lighting like the goblins; casts no shadows.
//
// Instancing must be on. Per instance: _SkinColor (rgb), _Seed (0-1) and _WobbleAmount (0-1), from property block arrays.
Shader "Goblins/Gib"
{
    Properties
    {
        _SkinColor ("Skin Color (per instance)", Color) = (0.4, 0.7, 0.3, 1)
        _GoreColor ("Gore Color", Color) = (0.69, 0.15, 0.62, 1)
        _GoreDeepColor ("Gore Deep Color", Color) = (0.32, 0.04, 0.3, 1)
        _LightingInfluence ("Lighting Influence", Range(0, 1)) = 0.6
        _Wobble ("Wobble", Range(0, 0.5)) = 0.12
        _WobbleSpeed ("Wobble Speed", Range(0, 40)) = 18
        _EyeColor ("X Eye Color", Color) = (0.03, 0.02, 0.03, 1)
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GoreColor;
                half4 _GoreDeepColor;
                half _LightingInfluence;
                half _Wobble;
                half _WobbleSpeed;
                half4 _EyeColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
                UNITY_DEFINE_INSTANCED_PROP(float, _WobbleAmount)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float4 colour : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 gore : TEXCOORD1;   // x cut face, y X eye
                float depth : TEXCOORD2;
                float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Jelly: squash on one axis while bulging on the others, in a pulse with its own timing
                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);
                float pulse = sin(_Time.y * _WobbleSpeed + seed * 6.2831) * _Wobble * UNITY_ACCESS_INSTANCED_PROP(Props, _WobbleAmount);
                float3 position = input.position.xyz * float3(1.0 + pulse, 1.0 - pulse, 1.0 + pulse);

                output.position = TransformObjectToHClip(position);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.gore = input.colour.rg;
                output.depth = saturate(1.0 - length(input.position.xyz) * 6.0);
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 skin = UNITY_ACCESS_INSTANCED_PROP(Props, _SkinColor).rgb;
                half3 gore = lerp(_GoreColor.rgb, _GoreDeepColor.rgb, input.depth);
                half3 colour = lerp(skin, gore, step(0.5, input.gore.x));
                colour = lerp(colour, _EyeColor.rgb, step(0.5, input.gore.y));

                Light light = GetMainLight();
                half3 normal = normalize(input.normal);
                half diffuse = saturate(dot(normal, light.direction)) * 0.75 + 0.25;
                half3 lit = colour * (diffuse * light.color + SampleSH(normal) * 0.5);
                colour = lerp(colour, lit, _LightingInfluence);

                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
}

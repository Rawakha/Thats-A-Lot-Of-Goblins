// Two triangles per rectangular grass blade. Flower sources use a few tiny square-petal cards.
// Saved chunks carry each card's root in UV1 and colour kind in UV2. Identical deformation in all passes.
Shader "Goblins/Grass Cards"
{
    Properties
    {
        _Color ("Matching Ground Colour", Color) = (0.39,0.51,0.29,1)
        [HDR] _TipTint ("Tip Tint Multiplier", Color) = (1.15,1.2,1.05,1)
        _FlowerColour ("Flower Petals", Color) = (1,0.6,0.77,1)
        _FlowerCentre ("Flower Centre", Color) = (1,0.85,0.4,1)
        _GrassColour ("Independent Grass Colour", Color) = (0.42,0.34,0.62,1)
        _GroundMatch ("Ground Colour Match", Range(0,1)) = 1
        _GradientPower ("Tip Gradient Power", Range(0.2,4)) = 1
        _ColourVariation ("Card Brightness Variation", Range(0,0.4)) = 0
        [HDR] _AlternateTipTint ("Alternate Tip Tint", Color) = (1.25,1.1,1.3,1)
        _TipVariation ("Tip Colour Variation", Range(0,1)) = 0
        _AlternateFlowerColour ("Alternate Flower Colour", Color) = (1,0.85,0.55,1)
        _AlternateFlowerChance ("Alternate Flower Chance", Range(0,1)) = 0
        _VariationScale ("Colour Patch Size", Float) = 12
        _VariationStrength ("Colour Variation", Range(0,0.4)) = 0.12
        _DetailStrength ("Fine Variation", Range(0,0.1)) = 0.02
        _Seed ("Variation Seed", Float) = 1
        _LightingInfluence ("Lighting Influence", Range(0,1)) = 0.6
        _WindStrength ("Wind", Range(0,0.3)) = 0.045
        _WindSpeed ("Wind Speed", Range(0,4)) = 1.2
        _WindDirection ("Wind Direction XZ", Vector) = (1,0.3,0,0)
        _WindWaveSize ("Wind Wave Size", Float) = 12
        _Flutter ("Individual Flutter", Range(0,1)) = 0
        [Enum(Upright,0,Camera,1,Authored,2)] _GrassFacing ("Grass Facing", Float) = 0
        [Enum(Upright,0,Camera,1,Authored,2)] _FlowerFacing ("Flower Facing", Float) = 1
        _ShadowStrength ("Received Shadow Strength", Range(0,1)) = 1
        _DrawDistance ("Draw Distance", Float) = 90
        [HideInInspector] _WearEnabled ("Wear Enabled", Float) = 0
        [HideInInspector] _WearLayer ("Wear Layer", Float) = 0
        [HideInInspector] _WearThreshold ("Wear Threshold", Float) = 0.5
        [HideInInspector] _WearMask ("Wear Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "DisableBatching"="True" }
        Cull Off
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "GrassSurfaceColour.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Color, _TipTint, _FlowerColour, _FlowerCentre;
        half4 _GrassColour, _AlternateTipTint, _AlternateFlowerColour;
        float4 _WindDirection;
        float _VariationScale, _VariationStrength, _DetailStrength, _Seed;
        float _LightingInfluence, _WindStrength, _WindSpeed, _DrawDistance;
        float _GroundMatch, _GradientPower, _ColourVariation, _TipVariation, _AlternateFlowerChance;
        float _WindWaveSize, _Flutter, _GrassFacing, _FlowerFacing, _ShadowStrength;
        float4x4 _WearWorldToLocal;
        float4 _WearBounds;
        float _WearEnabled, _WearLayer, _WearThreshold;
        CBUFFER_END
        #include "GrassWear.hlsl"
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float3 rootOS : TEXCOORD1;
            float2 kind : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 colour : TEXCOORD1;
            float fog : TEXCOORD2;
            float3 rootWS : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 root = TransformObjectToWorld(input.rootOS);
            float3 offset = input.positionOS.xyz - input.rootOS;
            // Preserve authored card dimensions, tilt, roll, offsets and ground scaling.
            float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
            offset *= scale;
            float3 right = normalize(float3(UNITY_MATRIX_V._m00, 0, UNITY_MATRIX_V._m02) + float3(0.00001,0,0));
            float3 up = float3(0,1,0);
            float3 forward = cross(right, up);
            // Flower heads face the full view so the little squares stay visible at steep camera angles.
            // Stems and petals use one basis, keeping the flower joined together.
            float facing = input.kind.x > 0.5 ? _FlowerFacing : _GrassFacing;
            if (facing > 0.5 && facing < 1.5)
            {
                right = float3(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02);
                up = float3(UNITY_MATRIX_V._m10, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m12);
                forward = cross(right, up);
            }
            float distance = length(GetCameraPositionWS() - root);
            float visible = 1 - smoothstep(_DrawDistance * 0.75, max(_DrawDistance,1), distance);
            float3 authored = TransformObjectToWorldDir(input.positionOS.xyz - input.rootOS, false);
            float3 world = root + (facing > 1.5 ? authored : right * offset.x + up * offset.y + forward * offset.z) * visible;
            float weight = saturate(input.uv.y);
            float random = GrassColourHash(root.xz + _Seed);
            float2 windDirection = normalize(_WindDirection.xy + float2(0.00001,0));
            float wave = sin(dot(root.xz, windDirection) / max(_WindWaveSize,0.1) * 6.28318 + _Time.y * _WindSpeed);
            wave += sin(_Time.y * _WindSpeed * 3.1 + random * 6.28318) * _Flutter;
            world.xz += windDirection * wave * _WindStrength * weight * weight * visible;
            half3 ground = GrassSurfaceColour(root, _Color.rgb, _VariationScale, _VariationStrength, _DetailStrength, _Seed);
            half3 baseColour = lerp(_GrassColour.rgb, ground, _GroundMatch);
            half3 tipTint = lerp(_TipTint.rgb, _AlternateTipTint.rgb, random * _TipVariation);
            half3 colour = baseColour * lerp(half3(1,1,1), tipTint, pow(weight, _GradientPower));
            // Keep the root join exact even when adding stronger variation to the upper blade.
            colour *= 1 + (random * 2 - 1) * _ColourVariation * weight;
            if (input.kind.x > 1.5) colour = lerp(_FlowerColour.rgb, _AlternateFlowerColour.rgb,
                step(random, _AlternateFlowerChance - 0.000001));
            if (input.kind.x > 2.5) colour = _FlowerCentre.rgb;
            output.colour = colour;
            output.rootWS = root;
            output.positionWS = world;
            output.positionCS = TransformWorldToHClip(world);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipGrassWear(input.rootWS);
                float4 shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                // The position-aware overload applies the directional light's cookie as well as shadows.
                Light light = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
                // Up-facing lighting agrees with the shelf instead of darkening camera-facing cards.
                half diffuse = saturate(light.direction.y);
                half shadow = lerp(1, light.shadowAttenuation, _ShadowStrength);
                half3 lighting = SampleSH(half3(0,1,0)) + light.color * diffuse * shadow;
                half3 colour = input.colour * lerp(half3(1,1,1), lighting, _LightingInfluence);
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half4 Depth(Varyings input) : SV_Target { ClipGrassWear(input.rootWS); return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Normals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 Normals(Varyings input) : SV_Target
            {
                ClipGrassWear(input.rootWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(float3(0,1,0));
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(0,1,0,0);
                #endif
            }
            ENDHLSL
        }
    }
}

// The soft mist round the edge of an area (Run_EdgeMist). Clear inside the area, thickening to solid outside it, with a
// billowing edge from drifting noise worked out from world position, so the mesh can be anything.
Shader "Goblins/Edge Mist"
{
    Properties
    {
        _Color ("Mist Colour", Color) = (0.86, 0.82, 0.95, 1)
        _ShadeColor ("Shade Colour", Color) = (0.62, 0.56, 0.78, 1)
        _NoiseScale ("Billow Size (smaller is bigger)", Float) = 0.045
        _NoiseSpeed ("Drift Speed", Float) = 0.02
        _Wobble ("Edge Wobble (metres)", Float) = 18
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Mist"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadeColor;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Wobble;
                float4 _Area;       // centre x, centre z, half width, half length
                float4 _Edge;       // metres inside the line it starts, metres it takes to thicken, seed, strength
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Billows(float2 p)
            {
                float a = Noise(p) * 0.55;
                a += Noise(p * 2.1 + 7.3) * 0.3;
                a += Noise(p * 4.3 + 13.7) * 0.15;
                return a;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 world = input.positionWS.xz;
                float2 drift = float2(_Time.y, _Time.y * 0.6) * _NoiseSpeed;
                float n = Billows(world * _NoiseScale + drift + _Edge.z);
                float n2 = Billows(world * _NoiseScale * 1.7 - drift * 1.4 + _Edge.z * 2.0);

                // Distance outside the area's rectangle: negative inside
                float2 d = abs(world - _Area.xy) - _Area.zw;
                float outside = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);

                float amount = saturate((outside + _Edge.x + (n - 0.5) * _Wobble) / max(_Edge.y, 0.01));
                amount = amount * amount * (3.0 - 2.0 * amount);

                float3 colour = lerp(_ShadeColor.rgb, _Color.rgb, saturate(n2 * 1.4 + amount * 0.4));
                return float4(colour, amount * _Color.a * _Edge.w);
            }
            ENDHLSL
        }
    }
}

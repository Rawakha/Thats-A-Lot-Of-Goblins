// Transparent unlit ground marker. Colour alpha controls opacity; no depth/shadow passes or invisible occlusion.
Shader "Goblins/Map Warning"
{
    Properties { _Color ("Colour", Color) = (1,0.08,0.04,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "WarningLine"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 colour : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 colour : COLOR; float fog : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.colour = input.colour * _Color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            { return half4(MixFog(input.colour.rgb,input.fog),input.colour.a); }
            ENDHLSL
        }
    }
}

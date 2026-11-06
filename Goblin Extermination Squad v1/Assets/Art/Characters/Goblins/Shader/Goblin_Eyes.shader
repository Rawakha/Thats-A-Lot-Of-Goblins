// Procedural eye cards: white rims, dark movable pupils, shared blinking/hurt/death state per goblin.
// Full 0-1 UVs per eye; combined mesh UV1 stores each eye's centre for independent size variation.
// Unlit, clipped outside the eye/X. Matching depth passes avoid solid rectangular depth/SSAO cards.
Shader "Goblins/Eyes"
{
    Properties
    {
        _EyeColor ("Eye White", Color) = (1,1,1,1)
        _PupilColor ("Pupil", Color) = (0.008,0.012,0.018,1)
        _Radius ("Eye Radius (UV)", Range(0.1,0.48)) = 0.44
        _PupilRadius ("Pupil Radius / Eye", Range(0.1,0.95)) = 0.76
        _Gaze ("Gaze XY (-1 to 1)", Vector) = (0,0,0,0)
        _GazeTravel ("Pupil Travel", Range(0,1)) = 0.85
        _GlanceAmount ("Automatic Glance Amount", Range(0,1)) = 0.25
        _GlanceSpeed ("Automatic Glance Speed", Range(0,4)) = 0.7
        _BlinkMin ("Blink Interval Min (seconds)", Float) = 2.5
        _BlinkMax ("Blink Interval Max (seconds)", Float) = 5.5
        _BlinkDuration ("Blink Duration (seconds, 0 disables)", Range(0,0.5)) = 0.14
        _HurtDuration ("Hurt Squint Duration (seconds)", Range(0.01,1)) = 0.2
        _HurtSquint ("Hurt Squint Strength", Range(0,1)) = 0.7
        _XSize ("Death X Stroke Length (UV)", Range(0.05,1.1)) = 0.87
        _XWidth ("Death X Stroke Half Width (UV)", Range(0.005,0.2)) = 0.055
        _PreviewBlink ("Preview Blink", Range(0,1)) = 0
        _PreviewHurt ("Preview Hurt", Range(0,1)) = 0
        [ToggleUI] _PreviewDead ("Preview Death X", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _EyeColor, _PupilColor;
            float4 _Gaze;
            float _Radius, _PupilRadius, _GazeTravel, _GlanceAmount, _GlanceSpeed;
            float _BlinkMin, _BlinkMax, _BlinkDuration, _HurtDuration, _HurtSquint, _XWidth, _XSize;
            float _PreviewBlink, _PreviewHurt, _PreviewDead;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(EyeInstances)
            UNITY_DEFINE_INSTANCED_PROP(float4, _GoblinEyeState) // seed, last hit time, dead, size
            UNITY_DEFINE_INSTANCED_PROP(float4, _GoblinEyeGaze)
        UNITY_INSTANCING_BUFFER_END(EyeInstances)
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 centreOS : TEXCOORD1; // xyz centre, w = -1 left / +1 right
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 eye : TEXCOORD2; // gaze.xy, openness, dead
            float fog : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings EyeVertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output = (Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float4 state = float4(0, -10000, 0, 1);
            float2 gaze = _Gaze.xy;
            float asymmetry = 0;
        #if UNITY_ANY_INSTANCING_ENABLED
            float4 instance = UNITY_ACCESS_INSTANCED_PROP(EyeInstances, _GoblinEyeState);
            if (instance.w > 0)
            {
                state = instance;
                gaze += UNITY_ACCESS_INSTANCED_PROP(EyeInstances, _GoblinEyeGaze).xy;
                asymmetry = UNITY_ACCESS_INSTANCED_PROP(EyeInstances, _GoblinEyeGaze).z;
            }
        #endif
            float dead = max(state.z, _PreviewDead);
            float interval = max(0.5, lerp(min(_BlinkMin, _BlinkMax), max(_BlinkMin, _BlinkMax), state.x));
            float phase = frac((_Time.y + state.x * 37.0) / interval) * interval;
            float blink = _BlinkDuration > 0 ? saturate(1.0 - abs(phase - _BlinkDuration * 0.5) / max(_BlinkDuration * 0.5, 0.001)) : 0;
            blink = max(blink, _PreviewBlink);
            float hurt = max(_PreviewHurt, saturate(1.0 - (_Time.y - state.y) / max(_HurtDuration, 0.01)));
            float openness = max(0.035, (1.0 - blink) * (1.0 - hurt * _HurtSquint));
            gaze += _GlanceAmount * float2(sin(_Time.y * _GlanceSpeed + state.x * 83.0),
                sin(_Time.y * _GlanceSpeed * 0.73 + state.x * 53.0));
            gaze /= max(1.0, length(gaze));
            output.eye = float4(gaze * (1.0 - hurt) * (1.0 - dead), openness, dead);
            float size = min(2.0, state.w * (1.0 + max(0.0, asymmetry * input.centreOS.w)));
            float3 position = input.centreOS.xyz + (input.positionOS.xyz - input.centreOS.xyz) * size;
            output.positionCS = TransformObjectToHClip(position);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        // Signed distance to a rectangle: square ends/corners on both diagonal strokes.
        float Stroke(float2 p, float2 halfSize)
        {
            float2 d = abs(p) - halfSize;
            return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
        }
        half4 EyeColour(Varyings input)
        {
            float2 p = input.uv - 0.5;
            float2 open = float2(p.x, p.y / input.eye.z);
            float distance = length(open) - _Radius;
            float pupilRadius = _Radius * _PupilRadius;
            float2 offset = input.eye.xy * (_Radius - pupilRadius) * _GazeTravel;
            float pupilDistance = length(open - offset) - pupilRadius;
            float aa = max(fwidth(distance), 0.001);
            float pupil = 1.0 - smoothstep(-aa, aa, pupilDistance);
            half3 colour = lerp(_EyeColor.rgb, _PupilColor.rgb, pupil);
            if (input.eye.w > 0.5)
            {
                float2 diagonal = float2(p.x + p.y, p.y - p.x) * 0.70710678;
                distance = min(Stroke(diagonal, float2(_XSize * 0.5, _XWidth)),
                    Stroke(diagonal.yx, float2(_XSize * 0.5, _XWidth)));
                aa = max(fwidth(distance), 0.001);
                colour = half3(0.0, 0.0, 0.0);
            }
            float coverage = 1.0 - smoothstep(-aa, aa, distance);
            return half4(colour, coverage);
        }
        half4 EyeFragment(Varyings input) : SV_Target
        {
            half4 colour = EyeColour(input);
            clip(colour.a - 0.01);
            colour.rgb = MixFog(colour.rgb, input.fog);
            return colour;
        }
        half4 EyeDepth(Varyings input) : SV_Target
        {
            clip(EyeColour(input).a - 0.5);
            return 0;
        }
        half4 EyeNormals(Varyings input) : SV_Target
        {
            clip(EyeColour(input).a - 0.5);
            float3 normal = normalize(input.normalWS);
        #if defined(_GBUFFER_NORMALS_OCT)
            float2 oct = saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5);
            return half4(PackFloat2To888(oct), 0);
        #else
            return half4(normal, 0);
        #endif
        }
        ENDHLSL
        Pass
        {
            Name "Eyes"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment EyeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment EyeDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment EyeNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

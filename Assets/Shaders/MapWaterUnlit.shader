Shader "Custom/URP/MapWaterUnlit"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Renderer Tint", Color) = (1, 1, 1, 1)
        _ShallowColor ("Shallow Color", Color) = (0.55, 0.9, 0.96, 1)
        _DeepColor ("Deep Color", Color) = (0.08, 0.32, 0.58, 1)
        [NoScaleOffset] _DistanceMask ("Distance Mask", 2D) = "white" {}
        _DistanceMaskScaleOffset ("Distance Mask Scale Offset", Vector) = (1, 1, 0, 0)
        _UseWorldMaskUV ("Use World Mask UV", Float) = 0
        _DistanceMaskWorldBounds ("Distance Mask World Bounds", Vector) = (0, 0, 1, 1)
        _DebugView ("Debug View", Float) = 0
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _ShoreStart ("Shore Start", Range(0, 1)) = 0.08
        _ShoreEnd ("Shore End", Range(0, 1)) = 0.85
        _NoiseTiling ("Noise Tiling", Float) = 6
        _NoiseSpeed ("Noise Speed", Vector) = (0.015, 0.01, 0, 0)
        _NoiseStrength ("Noise Strength", Range(0, 0.25)) = 0.035
        _Alpha ("Alpha", Range(0, 1)) = 0.92
        _FocusStrength ("District Focus Strength", Range(0, 1)) = 0
        _FocusBrightness ("District Focus Brightness", Range(0, 1)) = 0.68
        _FocusSaturation ("District Focus Saturation", Range(0, 1)) = 0.35
        _FoamColor ("Foam Color", Color) = (0.88, 1, 1, 1)
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.12
        _FoamWidth ("Foam Width", Range(0.001, 0.25)) = 0.055
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "MapWaterUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DistanceMask);
            SAMPLER(sampler_DistanceMask);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _DistanceMaskScaleOffset;
                float4 _DistanceMaskWorldBounds;
                half _UseWorldMaskUV;
                half _DebugView;
                half _ShoreStart;
                half _ShoreEnd;
                half _NoiseTiling;
                float4 _NoiseSpeed;
                half _NoiseStrength;
                half _Alpha;
                half _FocusStrength;
                half _FocusBrightness;
                half _FocusSaturation;
                half4 _FoamColor;
                half _FoamAmount;
                half _FoamWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float2 uvMaskUv = input.uv * _DistanceMaskScaleOffset.xy + _DistanceMaskScaleOffset.zw;
                float2 boundsSize = max(_DistanceMaskWorldBounds.zw, float2(0.0001, 0.0001));
                float2 worldMaskUv = (input.positionWS.xy - _DistanceMaskWorldBounds.xy) / boundsSize;
                float2 maskUv = lerp(uvMaskUv, worldMaskUv, saturate(_UseWorldMaskUV));
                half maskValue = SAMPLE_TEXTURE2D(_DistanceMask, sampler_DistanceMask, maskUv).r;

                if (_DebugView > 0.5)
                    return half4(maskValue, maskValue, maskValue, saturate(_Alpha * input.color.a * spriteAlpha));

                half shoreStart = min(_ShoreStart, _ShoreEnd - 0.0001);
                half shoreEnd = max(_ShoreEnd, shoreStart + 0.0001);
                half depthBlend = smoothstep(shoreStart, shoreEnd, maskValue);
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);

                float2 noiseUv = input.uv * max(_NoiseTiling, 0.001) + _Time.y * _NoiseSpeed.xy;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUv).r;
                waterColor = saturate(waterColor + (noise - 0.5) * _NoiseStrength);

                half foamMask = 1.0 - smoothstep(_ShoreStart, _ShoreStart + max(_FoamWidth, 0.001), maskValue);
                foamMask *= saturate(_FoamAmount);
                waterColor = lerp(waterColor, _FoamColor.rgb, foamMask);

                half luminance = dot(waterColor, half3(0.2126, 0.7152, 0.0722));
                half3 focusedWaterColor = lerp(half3(luminance, luminance, luminance), waterColor, saturate(_FocusSaturation));
                focusedWaterColor *= saturate(_FocusBrightness);
                waterColor = lerp(waterColor, focusedWaterColor, saturate(_FocusStrength));

                half alpha = saturate(_Alpha * input.color.a * spriteAlpha);
                return half4(waterColor * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

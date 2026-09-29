Shader "VNovelizer/UI/MainMenuBurnReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CharEdgeTex ("Charred Edge", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _SequenceTime ("Sequence Seconds", Float) = 0
        _Center ("Center", Vector) = (0.5,0.5,0,0)
        _Aspect ("Aspect", Float) = 1.777778
        _NoiseStrength ("Irregularity", Range(0,0.2)) = 0.075
        _RadiusScale ("Radius Scale", Range(0.75,1.5)) = 1.12
        _Impact ("Impact", Range(0,1)) = 0
        _FinalCover ("Final Cover", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+50"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "MainMenuBurnReveal"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _ClipRect;
                half4 _TextureSampleAdd;
                float4 _Center;
                float _Aspect;
                float _NoiseStrength;
                float _RadiusScale;
                float _Progress;
                float _SequenceTime;
                float _Impact;
                float _FinalCover;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPosition : TEXCOORD1;
                float2 localPosition : TEXCOORD2;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.localPosition = input.positionOS.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            #include "MainMenuBurnBoundary.hlsl"

            float SignedDistanceToBurn(float2 uv)
            {
                return BurnSignedDistance(uv, _Center.xy, _Aspect, _Progress,
                                          _RadiusScale, _NoiseStrength, _SequenceTime);
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float sd = SignedDistanceToBurn(screenUv);
                // One pixel on each side: roughly two screen pixels overall.
                float halfWidth = max(fwidth(sd), 1.0 / max(_ScreenParams.y, 1.0));
                float revealed = (1.0 - smoothstep(-halfWidth, halfWidth, sd))
                               * step(0.0001, _Progress + _Impact);
                half4 color = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv)
                             + _TextureSampleAdd) * input.color;
                color.a *= revealed;
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.localPosition)
                              * step(input.localPosition, _ClipRect.zw);
                color.a *= inside.x * inside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}

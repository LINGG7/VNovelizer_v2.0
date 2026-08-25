Shader "VNovelizer/UI/MainMenuBurnMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CharEdgeTex ("Charred Edge", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _SequenceTime ("Sequence Time", Range(0,1)) = 0
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
            Name "MainMenuBurnThrough"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CharEdgeTex);
            SAMPLER(sampler_CharEdgeTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            // This exact angular profile is mirrored in MainMenuBurnTransition.
            float BoundaryProfile(float angle)
            {
                return (sin(angle * 5.0 + 1.7) * 0.28
                      + sin(angle * 9.0 - 0.8) * 0.22
                      + sin(angle * 17.0 + 2.4) * 0.16
                      + sin(angle * 29.0 - 1.1) * 0.10) * 0.46;
            }

            float SignedDistanceToBurn(float2 uv)
            {
                float2 p = uv - _Center.xy;
                p.x *= max(_Aspect, 0.01);
                float angle = atan2(p.y, p.x);
                float cornerRadius = length(float2(max(_Center.x, 1.0 - _Center.x) * _Aspect,
                                                   max(_Center.y, 1.0 - _Center.y)));
                float ignitionRadius = _Impact * 0.035;
                float radius = max(_Progress * cornerRadius * _RadiusScale, ignitionRadius);
                radius += BoundaryProfile(angle) * _NoiseStrength;
                return length(p) - max(radius, 0.0);
            }

            float MirroredRepeat(float value)
            {
                float f = frac(value);
                return 1.0 - abs(f * 2.0 - 1.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float sd = SignedDistanceToBurn(input.uv);
                float2 texel = float2(0.0025 / max(_Aspect, 0.01), 0.0025);
                float dx = SignedDistanceToBurn(input.uv + float2(texel.x, 0.0))
                         - SignedDistanceToBurn(input.uv - float2(texel.x, 0.0));
                float dy = SignedDistanceToBurn(input.uv + float2(0.0, texel.y))
                         - SignedDistanceToBurn(input.uv - float2(0.0, texel.y));
                float3 fakeNormal = normalize(float3(-dx, -dy, 0.018));
                float bevelLight = saturate(dot(fakeNormal, normalize(float3(-0.35, 0.62, 0.70))));

                float2 p = input.uv - _Center.xy;
                p.x *= max(_Aspect, 0.01);
                float angle = atan2(p.y, p.x);
                float angularU = MirroredRepeat(angle * 0.477465 + 0.31);
                float edgeV = saturate(0.5 + sd * 18.0);
                half3 edgeSample = SAMPLE_TEXTURE2D(_CharEdgeTex, sampler_CharEdgeTex, float2(angularU, edgeV)).rgb;
                half edgeLuma = dot(edgeSample, half3(0.299h, 0.587h, 0.114h));
                half textureMask = smoothstep(0.018h, 0.22h, edgeLuma);

                float hole = 1.0 - smoothstep(-0.0025, 0.0035, sd);
                float hotLip = smoothstep(-0.012, -0.004, sd) * (1.0 - smoothstep(0.001, 0.005, sd));
                float crust = smoothstep(-0.002, 0.004, sd) * (1.0 - smoothstep(0.024, 0.038, sd));
                float soot = smoothstep(0.012, 0.025, sd) * (1.0 - smoothstep(0.052, 0.082, sd));
                float ignition = step(0.0001, _Progress + _Impact);
                hole *= ignition;
                hotLip *= ignition;
                crust *= ignition;
                soot *= ignition;

                half emberPulse = 0.82h + 0.18h * sin(_SequenceTime * 61.0 + angle * 11.0);
                half3 holeColor = half3(0.0003h, 0.00015h, 0.00008h);
                half3 sootColor = half3(0.018h, 0.012h, 0.009h);
                half3 crustColor = lerp(half3(0.010h, 0.004h, 0.001h), edgeSample * 0.20h,
                                        textureMask * (0.18h + 0.26h * bevelLight));
                half crackBreakup = smoothstep(0.48h, 0.78h,
                    0.55h + 0.30h * sin(angle * 37.0h + 0.8h) + 0.15h * sin(angle * 61.0h));
                half3 lipColor = lerp(half3(0.18h, 0.004h, 0.001h), half3(1.0h, 0.12h, 0.002h),
                                      saturate(edgeLuma * 1.4h) * crackBreakup) * emberPulse;

                half alpha = saturate(hole + hotLip * 0.88 + crust * (0.72 + textureMask * 0.12) + soot * 0.22);
                half3 color = sootColor;
                color = lerp(color, crustColor, saturate(crust));
                color = lerp(color, lipColor, saturate(hotLip));
                color = lerp(color, holeColor, saturate(hole));
                float impactFlash = smoothstep(0.065, 0.0, length(p)) * _Impact;
                color += impactFlash * half3(1.0h, 0.25h, 0.008h) * (1.0h - hole * 0.75h);
                alpha = max(alpha, impactFlash * 0.72h);

                color = lerp(color, holeColor, _FinalCover);
                alpha = lerp(alpha, 1.0h, _FinalCover);
                return half4(color * input.color.rgb, saturate(alpha * input.color.a));
            }
            ENDHLSL
        }
    }
}

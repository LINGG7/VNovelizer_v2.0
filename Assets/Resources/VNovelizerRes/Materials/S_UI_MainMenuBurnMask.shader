Shader "VNovelizer/UI/MainMenuBurnMask"
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
            #pragma target 3.5
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
                float4 screenPosition : TEXCOORD1;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
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
            // Integer lattice hashing gives shared corners identical values.
            // A floating-point frac hash can disagree after shader multiply-add
            // optimization and expose the rectangular interpolation cells.
            float BurnLatticeHash(int2 cell)
            {
                uint2 bits = asuint(cell);
                uint hash = (bits.x * 1597334677u) ^ (bits.y * 3812015801u);
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return float(hash & 0x00ffffffu) * (1.0 / 16777216.0);
            }

            float Noise(float2 p)
            {
                int2 cell = (int2)floor(p);
                float2 f = frac(p);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(lerp(BurnLatticeHash(cell), BurnLatticeHash(cell + int2(1, 0)), f.x),
                            lerp(BurnLatticeHash(cell + int2(0, 1)), BurnLatticeHash(cell + int2(1, 1)), f.x), f.y);
            }

            float Turbulence(float2 p)
            {
                return Noise(p) * 0.57 + Noise(p * 2.03 + 17.1) * 0.29
                     + Noise(p * 4.11 + 9.2) * 0.14;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float ignition = step(0.0001, _Progress + _Impact);
                if (ignition < 0.5 && _FinalCover < 0.0001)
                    return half4(0, 0, 0, 0);

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float2 p = screenUv - _Center.xy;
                p.x *= max(_Aspect, 0.01);
                float sd = SignedDistanceToBurn(screenUv);
                float seconds = _SequenceTime;

                // Advect elongated detail upward; domain warping breaks up straight columns.
                float2 flow = p * float2(13.0, 5.0) - float2(0.0, seconds * 2.4);
                float warp = Turbulence(flow * 0.63 + float2(0.0, seconds * 0.35));
                flow.x += (warp - 0.5) * 2.8;
                float fuel = Turbulence(flow);
                float detail = Noise(flow * float2(2.1, 1.4) - float2(0.0, seconds));
                float flameField = fuel * 0.8 + detail * 0.2;
                float coverage = (1.0 - smoothstep(-0.05, 0.025, sd)) * ignition;
                float fire = smoothstep(0.25, 0.70, flameField);
                float heat = smoothstep(0.42, 0.86, flameField);
                half3 fireColor = lerp(half3(0.32h, 0.014h, 0.001h),
                                      half3(1.0h, 0.25h, 0.008h), fire);
                fireColor = lerp(fireColor, half3(1.0h, 0.88h, 0.40h), heat * heat);

                // A translucent warm halo ties the advancing fire to the menu.
                float halo = (1.0 - smoothstep(0.0, 0.14, max(sd, 0.0)))
                           * (1.0 - coverage) * ignition * (0.07 + fuel * 0.09);
                float fireAlpha = coverage * lerp(0.30, 0.96, fire) * 0.20;
                float alpha = fireAlpha + halo * (1.0 - fireAlpha);
                half3 premultiplied = fireColor * fireAlpha
                    + half3(0.9h, 0.19h, 0.012h) * halo * (1.0 - fireAlpha);
                float impactFlash = (1.0 - smoothstep(0.0, 0.065, length(p))) * _Impact;
                float flashAlpha = impactFlash * 0.72;
                premultiplied = half3(1.0h, 0.65h, 0.12h) * flashAlpha
                    + premultiplied * (1.0 - flashAlpha);
                alpha = flashAlpha + alpha * (1.0 - flashAlpha);
                half3 color = premultiplied / max(alpha, 0.0001);

                // Only the final handoff becomes opaque black.
                color = lerp(color, half3(0, 0, 0), _FinalCover);
                alpha = lerp(alpha, 1.0, _FinalCover);
                return half4(color * input.color.rgb, saturate(alpha * input.color.a));
            }
            ENDHLSL
        }
    }
}

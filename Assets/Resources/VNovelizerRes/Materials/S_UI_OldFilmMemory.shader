Shader "VNovelizer/UI/OldFilmMemory"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SepiaAlpha ("Sepia Alpha", Range(0, 1)) = 0.36
        _VignetteAlpha ("Vignette Alpha", Range(0, 1)) = 0.58
        _DirtAlpha ("Dirt Alpha", Range(0, 1)) = 0.22
        _WhiteDustAlpha ("White Dust Alpha", Range(0, 1)) = 0.42
        _ScratchAlpha ("Scratch Alpha", Range(0, 1)) = 0.32
        _FlickerSpeed ("White Dust Refresh", Range(1, 24)) = 7
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _SepiaAlpha;
            float _VignetteAlpha;
            float _DirtAlpha;
            float _WhiteDustAlpha;
            float _ScratchAlpha;
            float _FlickerSpeed;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float verticalScratch(float2 uv, float seed, float width)
            {
                float x = hash(float2(seed, 31.7));
                float waviness = (noise(float2(uv.y * 16.0, seed)) - 0.5) * 0.018;
                float scratchLine = 1.0 - smoothstep(0.0, width, abs(uv.x - x - waviness));
                float broken = step(0.34, noise(float2(seed * 2.1, uv.y * 36.0)));
                return scratchLine * broken;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv * 2.0 - 1.0;
                float dist = length(centered);

                float vignette = smoothstep(0.52, 1.18, dist);
                float edgeBurn = smoothstep(0.66, 1.1, dist) * (0.65 + 0.35 * noise(uv * 7.0));

                float fineDirt = step(0.72, noise(uv * 420.0)) * (0.35 + 0.65 * hash(floor(uv * 420.0)));
                float darkDirt = step(0.955, hash(floor(uv * 115.0))) * (0.55 + 0.45 * noise(uv * 900.0));

                float frame = floor(_Time.y * _FlickerSpeed);
                float2 dustGrid = uv * float2(220.0, 124.0);
                float2 dustCell = floor(dustGrid);
                float2 dustLocal = frac(dustGrid) - 0.5;
                float dustSeed = hash(dustCell + frame * float2(17.3, 9.1));
                float dustRadius = lerp(0.045, 0.18, hash(dustCell + 19.7));
                float dustDot = 1.0 - smoothstep(dustRadius, dustRadius + 0.035, length(dustLocal));
                float whiteDust = step(0.992, dustSeed) * dustDot;
                whiteDust *= step(0.45, hash(float2(frame * 1.73, frame * 0.41)));

                float scratch =
                    verticalScratch(uv, 3.1, 0.0022) +
                    verticalScratch(uv, 9.7, 0.0014) +
                    verticalScratch(uv, 18.4, 0.0018) +
                    verticalScratch(uv, 27.2, 0.0011);
                scratch = saturate(scratch);

                float hair = step(0.992, noise(float2(uv.x * 260.0 + uv.y * 24.0, uv.y * 620.0)));
                float whiteSpecks = saturate(whiteDust + hair * 0.45);

                float3 sepia = float3(0.66, 0.48, 0.25);
                float3 burn = float3(0.06, 0.045, 0.035);
                float3 dirt = float3(0.02, 0.018, 0.014);

                float3 color = sepia * _SepiaAlpha;
                color = lerp(color, burn, vignette * _VignetteAlpha);
                color = lerp(color, dirt, saturate(fineDirt * _DirtAlpha + darkDirt * _DirtAlpha * 1.8));
                color += whiteSpecks * _WhiteDustAlpha;
                color += scratch * _ScratchAlpha;

                float alpha = saturate(_SepiaAlpha + vignette * _VignetteAlpha + fineDirt * _DirtAlpha + darkDirt * _DirtAlpha + whiteSpecks * _WhiteDustAlpha + scratch * _ScratchAlpha);
                fixed sourceAlpha = tex2D(_MainTex, uv).a;
                return fixed4(saturate(color), alpha * sourceAlpha) * i.color;
            }
            ENDCG
        }
    }
}

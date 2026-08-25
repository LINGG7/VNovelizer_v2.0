Shader "VNovelizer/UI/TVSnow"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Density ("Density", Range(0, 0.2)) = 0.025
        _Alpha ("Alpha", Range(0, 1)) = 0.38
        _NoiseScale ("Snow Cell Scale", Range(30, 260)) = 140
        _RefreshRate ("Refresh Rate", Range(1, 30)) = 10
        _BurstChance ("Burst Chance", Range(0, 1)) = 0.45
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.08
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

            fixed4 _Color;
            float _Density;
            float _Alpha;
            float _NoiseScale;
            float _RefreshRate;
            float _BurstChance;
            float _ScanlineStrength;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
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
                float frame = floor(_Time.y * _RefreshRate);
                float burst = step(1.0 - _BurstChance, hash(float2(frame * 0.173, frame * 1.913)));

                float2 cell = floor(i.uv * _NoiseScale);
                float n = hash(cell + frame * float2(13.1, 7.7));
                float sparkle = step(1.0 - _Density, n) * burst;

                float localFlicker = hash(cell * 1.37 + frame * 23.41);
                float scanline = sin((i.uv.y * 1080.0 + frame) * 3.14159265);
                scanline = lerp(1.0, 0.82 + 0.18 * scanline, _ScanlineStrength);

                float gray = lerp(0.55, 1.0, localFlicker) * sparkle * scanline;
                float alpha = saturate(_Alpha * sparkle * lerp(0.45, 1.0, localFlicker));

                return fixed4(gray, gray, gray, alpha) * i.color;
            }
            ENDCG
        }
    }
}

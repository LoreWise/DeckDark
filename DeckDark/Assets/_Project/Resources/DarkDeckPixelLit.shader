// Shader do teste 3D: ilumina tudo com uma unica lampada (calculada aqui mesmo),
// quantiza a luz em poucas faixas e usa dither de Bayer, para ficar com cara de pixel art.
// Nao depende do pipeline: as matrizes vem do script (_DD_M e _DD_VP).
Shader "DarkDeck/PixelLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Tiling ("Tiling", Vector) = (1,1,0,0)
        _Emission ("Emission", Range(0,1)) = 0
        _Flat ("Flat (billboard)", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZTest LEqual
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _MainTex;
            float4 _Color;
            float4 _Tiling;
            float _Emission, _Flat, _Cutoff;

            float4x4 _DD_M;
            float4x4 _DD_VP;
            float3 _DD_CamPos;
            float3 _LampPos;
            float4 _LampColor;
            float _LampIntensity;
            float _LampRange;
            float4 _Ambient;
            float _Levels;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wnrm : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 w = mul(_DD_M, float4(v.vertex.xyz, 1));
                o.wpos = w.xyz;
                o.pos = mul(_DD_VP, w);
                o.wnrm = mul((float3x3)_DD_M, v.normal);
                o.uv = v.uv * _Tiling.xy + _Tiling.zw;
                return o;
            }

            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float4 frag (v2f i) : SV_Target
            {
                float4 albedo = tex2D(_MainTex, i.uv) * _Color;
                clip(albedo.a - _Cutoff);

                float3 n = normalize(i.wnrm);
                float3 toL = _LampPos - i.wpos;
                float d = length(toL);
                float3 l = toL / max(d, 1e-4);
                // faces viradas para o lado oposto (Cull Off) usam a normal invertida
                float3 toCam = _DD_CamPos - i.wpos;
                if (dot(n, toCam) < 0) n = -n;
                float ndl = saturate(dot(n, l) * 0.8 + 0.2);
                ndl = lerp(ndl, 1.0, _Flat);
                float att = saturate(1 - d / _LampRange);
                att *= att;
                float s = _LampIntensity * att * ndl;

                // quantiza com dither ordenado na tela
                int2 p = int2(i.pos.xy) & 3;
                float b = (bayer[p.y * 4 + p.x] + 0.5) / 16.0;
                float q = floor(s * _Levels + b) / _Levels;

                float3 light = _Ambient.rgb + _LampColor.rgb * q;
                float3 col = albedo.rgb * light;
                col = lerp(col, albedo.rgb, _Emission);
                return float4(col, albedo.a);
            }
            ENDCG
        }
    }
}

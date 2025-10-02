Shader "Unlit/HighContrastGreyscale"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Contrast("Contrast", Range(0.5, 3.0)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Contrast;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                gray = (gray - 0.5) * _Contrast + 0.5;
                gray = saturate(gray);
                return float4(gray, gray, gray, col.a);
            }
            ENDCG
        }
    }
}

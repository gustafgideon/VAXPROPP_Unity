Shader "Unlit/GlitchRGBSplit"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Offset("Channel Offset (UV)", Range(0.0, 0.05)) = 0.01
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
            float _Offset;

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
                float2 uv = i.uv;
                float r = tex2D(_MainTex, uv + float2(_Offset, 0.0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(_Offset, 0.0)).b;
                return float4(r, g, b, 1.0);
            }
            ENDCG
        }
    }
}

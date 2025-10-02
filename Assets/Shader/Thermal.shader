Shader "Unlit/Thermal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Contrast("Contrast", Range(0,3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Contrast;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed3 ThermalPalette(float v)
            {
                // simple palette: black -> blue -> purple -> red -> yellow -> white
                if (v < 0.2) return lerp(fixed3(0,0,0), fixed3(0,0,0.5), v / 0.2);
                if (v < 0.4) return lerp(fixed3(0,0,0.5), fixed3(0.5,0,0.8), (v-0.2)/0.2);
                if (v < 0.6) return lerp(fixed3(0.5,0,0.8), fixed3(1,0,0), (v-0.4)/0.2);
                if (v < 0.8) return lerp(fixed3(1,0,0), fixed3(1,0.8,0), (v-0.6)/0.2);
                return lerp(fixed3(1,0.8,0), fixed3(1,1,1), (v-0.8)/0.2);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                lum = pow(lum, 1.0 / _Contrast); // adjust response
                fixed3 color = ThermalPalette(saturate(lum));
                return fixed4(color, c.a);
            }
            ENDCG
        }
    }
}
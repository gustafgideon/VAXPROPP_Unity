Shader "Skybox/Dual Panoramic With Overcast"
{
    Properties {
        _Tint1("Tint Color 1", Color) = (.5,.5,.5,1)
        _Tint2("Tint Color 2", Color) = (.5,.5,.5,1)
        _Exposure1("Exposure 1", Range(0,8)) = 1.0
        _Exposure2("Exposure 2", Range(0,8)) = 1.0
        _Rotation1("Rotation1", Range(0,360)) = 0
        _Rotation2("Rotation2", Range(0,360)) = 0
        [NoScaleOffset] _Texture1("Texture 1", 2D) = "grey" {}
        [NoScaleOffset] _Texture2("Texture 2", 2D) = "grey" {}
        [NoScaleOffset] _OvercastTexture("Overcast Texture", 2D) = "grey" {}
        [Enum(360 Degrees,0,180 Degrees,1)] _ImageType("Image Type", Float) = 0
        [Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
        [Enum(None,0,Side by Side,1,Over Under,2)] _Layout("3D Layout", Float) = 0
        _Blend("Overcast Blend (RainIntensity)", Range(0,1)) = 0
        _TimeOfDayLerp("Time of Day Lerp", Range(0,1)) = 0
    }

    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _Texture1;
            sampler2D _Texture2;
            sampler2D _OvercastTexture;

            float4 _Tint1;
            float4 _Tint2;
            float _Exposure1;
            float _Exposure2;
            float _Rotation1;
            float _Rotation2;

            float _Blend;
            float _TimeOfDayLerp;

            bool _MirrorOnBack;
            int _ImageType;
            int _Layout;

            inline float2 ToRadialCoords(float3 coords)
            {
                float3 n = normalize(coords);
                float lat = acos(n.y);
                float lon = atan2(n.z, n.x);
                float2 sphereCoords = float2(lon, lat) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5,1.0)-sphereCoords;
            }

            float3 RotateY(float3 v, float deg)
            {
                float a = deg * UNITY_PI/180;
                float sina, cosa;
                sincos(a, sina, cosa);
                float2x2 m = float2x2(cosa,-sina,sina,cosa);
                return float3(mul(m,v.xz),v.y).xzy;
            }

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float2 image180ScaleAndCutoff : TEXCOORD1;
                float4 layout3DScaleAndOffset : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 rotated = RotateY(v.vertex.xyz, _Rotation1);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;

                if (_ImageType==0) o.image180ScaleAndCutoff=float2(1,1);
                else o.image180ScaleAndCutoff=float2(2,_MirrorOnBack?1:0.5);

                if (_Layout==0) o.layout3DScaleAndOffset=float4(0,0,1,1);
                else if (_Layout==1) o.layout3DScaleAndOffset=float4(unity_StereoEyeIndex,0,0.5,1);
                else o.layout3DScaleAndOffset=float4(0,1-unity_StereoEyeIndex,1,0.5);

                return o;
            }

            half3 ApplyExposure(half3 col, float exposure)
            {
                return col * exposure;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 tc = ToRadialCoords(i.texcoord);
                if(tc.x>i.image180ScaleAndCutoff[1]) return half4(0,0,0,1);
                tc.x = fmod(tc.x*i.image180ScaleAndCutoff[0],1);
                tc = (tc + i.layout3DScaleAndOffset.xy)*i.layout3DScaleAndOffset.zw;

                // Sample main sky
                half3 c1 = ApplyExposure(tex2D(_Texture1,tc).rgb, _Exposure1);
                tc.x = frac(tc.x + (_Rotation2-_Rotation1)/360);
                half3 c2 = ApplyExposure(tex2D(_Texture2,tc).rgb, _Exposure2);

                half3 normalSky = lerp(c1,c2,_TimeOfDayLerp);

                // Sample overcast
                half3 overcast = tex2D(_OvercastTexture,tc).rgb;

                // Blend overcast using RainIntensity
                half3 finalColor = lerp(normalSky, overcast,_Blend);

                // Apply tints
                finalColor *= lerp(_Tint1.rgb,_Tint2.rgb,_TimeOfDayLerp);

                return half4(finalColor,1);
            }

            ENDCG
        }
    }
    Fallback Off
}

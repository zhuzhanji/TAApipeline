// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "TaaPP/gbuffer"
{
    Properties
    {
        _DiffuseTex ("Diffuse", 2D) = "white" {}
        _SpecularTex ("Specular", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "LightMode"="gbuffer" }
        Ztest On Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 preScreenPosition: TEXCOORD1;
                float4 nowScreenPosition: TEXCOORD2;
            };

            sampler2D _DiffuseTex;
            float4 _DiffuseTex_ST;

            sampler2D _SpecularTex;
            float2 _ScreenSize;
            float3 _Color;

            //float4x4 _CurrentM;
            //float4x4 _CurrentVP;
            float4x4 _LastM;
            float4x4 _LastVP;


            float2 _Jitter;
            int _FrameCount;

            float4 taa_vert(float4 pos)
            {
                float deltaWidth = 1.0/_ScreenSize.x, deltaHeight = 1.0/_ScreenSize.y;
                //const float2 Halton_2_3[8] =
                //{
                //	float2(0.0f, -1.0f / 3.0f),
                //	float2(-1.0f / 2.0f, 1.0f / 3.0f),
                //	float2(1.0f / 2.0f, -7.0f / 9.0f),
                //	float2(-3.0f / 4.0f, -1.0f / 9.0f),
                //	float2(1.0f / 4.0f, 5.0f / 9.0f),
                //	float2(-1.0f / 4.0f, -5.0f / 9.0f),
                //	float2(3.0f / 4.0f, 1.0f / 9.0f),
                //	float2(-7.0f / 8.0f, 7.0f / 9.0f)
                //};

                float4x4 jitterMat = UNITY_MATRIX_P;
	            //jitterMat[2][0] += _Jitter.x;
	            //jitterMat[2][1] += _Jitter.y;
                float4 posout = mul(mul(mul(jitterMat , UNITY_MATRIX_V), unity_ObjectToWorld), pos);
                posout /= posout.w;
                posout.xy += _Jitter;
                return posout;
            }
            v2f vert (appdata v)
            {
                v2f o;
                //o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _DiffuseTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.vertex = taa_vert(v.vertex);

                o.nowScreenPosition = mul(mul(mul(UNITY_MATRIX_P,  UNITY_MATRIX_V), unity_ObjectToWorld) , v.vertex);
                o.preScreenPosition = mul(mul(_LastVP , unity_ObjectToWorld) , v.vertex);

                return o;
            }

            void frag (v2f i,
                        out float4 GT0 : SV_Target0,
                        out float4 GT1 : SV_Target1,
                        out float4 GT2 : SV_Target2,
                        out float4 GT3 : SV_Target3)
            {
                
                GT0.rgb = tex2D(_DiffuseTex, i.uv).rgb * _Color;
                GT0.a = tex2D(_SpecularTex, i.uv).r;
                GT1 = float4(normalize(i.normal), 1);
                //motion vector(rg) + roughness + metallic

                float2 newPos = ((i.nowScreenPosition.xy / i.nowScreenPosition.w) * 0.5 + 0.5);
	            float2 prePos = ((i.preScreenPosition.xy / i.preScreenPosition.w) * 0.5 + 0.5);
	            float2 gVelo = newPos - prePos;

                GT2 = float4(gVelo,0,1);
                GT3 = float4(1,0,0,1);
            }
            ENDCG
        }
    }
}
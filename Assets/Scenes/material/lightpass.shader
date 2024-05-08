Shader "TaaRP/lightpass"
{
    SubShader
    {
        Tags { "LightMode" = "ForwardBase" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            sampler2D _gdepth;
            sampler2D _GT0;
            sampler2D _GT1;
            sampler2D _GT2;
            sampler2D _GT3;

            float4x4 _vpMatrix;
            float4x4 _vpMatrixInv;
            

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if(tex2D(_gdepth, uv).x < 0.0001) return float4(tex2D(_GT0, uv).rgb, 1.0);
                
                float4 GT2 = tex2D(_GT2, uv);
                float4 GT3 = tex2D(_GT3, uv);
                
                // Gbuffer
                float3 Diffuse = tex2D(_GT0, uv).rgb;
                float Specular = tex2D(_GT0, uv).a;

                float3 normal = tex2D(_GT1, uv).rgb;

                float2 motionVec = GT2.rg;
                float roughness = GT2.b;
                float metallic = GT2.a;
                float3 emission = GT3.rgb;
                float occlusion = GT3.a;
                
                float d = UNITY_SAMPLE_DEPTH(tex2D(_gdepth, uv));
                float d_lin = Linear01Depth(d);

                float4 ndcPos = float4(uv*2-1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                //
                float3 N = normalize(normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                float3 radiance = _LightColor0.rgb;

                float3 lighting  = Diffuse * 0.1;
                float3 diffuse = max(dot(N, L), 0.0) * Diffuse * radiance;

                float3 halfwayDir = normalize(L + V);
                float spec = pow(max(dot(N, halfwayDir), 0.0), 4.0);
                float3 specular = radiance * spec * Specular;

                lighting += diffuse + specular;

                return float4(lighting, 1.0);
            }
            ENDCG
        }
    }
}
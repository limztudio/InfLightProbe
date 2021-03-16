Shader "Custom/FinderShader"{
    Properties{
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader{
        Tags{ "RenderType"="Opaque" }
        LOD 100

        Pass{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "InfProbe/probeFinder.cginc"

            struct appdata{
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f{
                float3 normal : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            samplerCUBE _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v){
                half3 vNormalM = mul((float3x3)unity_ObjectToWorld, v.normal);

                v2f o;

                o.normal = vNormalM;
                o.vertex = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target{
                float3 vNormalM = normalize(i.normal);

                half4 col = texCUBE(_MainTex, vNormalM);

                //col.rgb = getIrradiance(half4(vNormalM, 1.h));
                col.rgb = ShadeSH9(half4(vNormalM, 1.h));

                col.a = 1;

                return col;
            }
            ENDCG
        }
    }
}


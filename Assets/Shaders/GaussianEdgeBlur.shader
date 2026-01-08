Shader "Hidden/GaussianEdgeBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BlurTex ("BlurTex", 2D) = "white" {}
        _Sigma ("Sigma", Float) = 2.0
        _BlurDirection ("BlurDirection", Vector) = (1,0,0,0)
        _EdgeRadius ("EdgeRadius", Float) = 0.7
        _EdgeFeather ("EdgeFeather", Float) = 0.25
        _Intensity ("Intensity", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZTest Always ZWrite Off Cull Off Blend One Zero
        Pass
        {
            Name "GAUSSIAN_BLUR"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x=1/width, y=1/height
            float2 _BlurDirection;
            float _Sigma;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            inline float gaussian(float x, float sigma)
            {
                return exp(-0.5 * (x*x) / (sigma*sigma));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 5-tap separable kernel (0, ±1, ±2)
                float2 texel = float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y) * _BlurDirection;
                float w0 = gaussian(0.0, _Sigma);
                float w1 = gaussian(1.0, _Sigma);
                float w2 = gaussian(2.0, _Sigma);
                float norm = w0 + 2.0*w1 + 2.0*w2;
                w0 /= norm; w1 /= norm; w2 /= norm;

                fixed4 c = tex2D(_MainTex, i.uv) * w0;
                c += tex2D(_MainTex, i.uv + texel * 1.0) * w1;
                c += tex2D(_MainTex, i.uv - texel * 1.0) * w1;
                c += tex2D(_MainTex, i.uv + texel * 2.0) * w2;
                c += tex2D(_MainTex, i.uv - texel * 2.0) * w2;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "COMPOSITE_EDGE"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; // original
            sampler2D _BlurTex; // blurred
            float _EdgeRadius;
            float _EdgeFeather;
            float _Intensity;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // radial mask: 0 at center, 1 at edges
                float2 centered = (i.uv - 0.5) * float2(_ScreenParams.x/_ScreenParams.y, 1.0);
                float r = length(centered);
                // mask starts at radius, ramps over feather
                float startR = saturate(_EdgeRadius);
                float feather = max(_EdgeFeather, 1e-4);
                float m = saturate((r - startR) / feather);

                fixed4 src = tex2D(_MainTex, i.uv);
                fixed4 blur = tex2D(_BlurTex, i.uv);
                fixed4 comp = lerp(src, blur, m * _Intensity);
                return comp;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

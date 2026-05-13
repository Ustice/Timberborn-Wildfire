Shader "Wildfire/AshOverlay"
{
    Properties
    {
        _AshTex ("Ash Texture", 2D) = "white" {}
        _AshIntensityTex ("Ash Intensity Texture", 2D) = "black" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.9
        _SigmoidSharpness ("Sigmoid Sharpness", Float) = 14
        _ThresholdLow ("Full Ash Midpoint", Range(0, 1)) = 0.08
        _ThresholdHigh ("Low Ash Midpoint", Range(0, 1)) = 0.92
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-500"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _AshTex;
            sampler2D _AshIntensityTex;
            sampler2D _MaskTex;
            float _MaxOpacity;
            float _SigmoidSharpness;
            float _ThresholdLow;
            float _ThresholdHigh;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.uv2.x < -1.5)
                {
                    float cloudMask = tex2D(_MaskTex, i.uv).r;
                    float cloudCoverage = smoothstep(0.18, 0.92, cloudMask);
                    return fixed4(i.color.rgb, i.color.a * cloudCoverage);
                }

                float textureAsh = tex2D(_AshIntensityTex, i.uv2).r;
                float ash = i.uv2.x < 0.0 ? saturate(i.color.a) : saturate(max(textureAsh, i.color.a));
                fixed4 ashTexel = tex2D(_AshTex, i.uv);
                float mask = tex2D(_MaskTex, i.uv).r;
                float midpoint = lerp(_ThresholdHigh, _ThresholdLow, ash);
                float coverage = 1.0 / (1.0 + exp(-_SigmoidSharpness * (mask - midpoint)));
                float3 visibleAsh = max(ashTexel.rgb, float3(0.24, 0.23, 0.21));
                float alpha = max(coverage * ash * _MaxOpacity, ash * 0.42);
                return fixed4(visibleAsh, saturate(alpha));
            }
            ENDCG
        }
    }
    Fallback "Transparent/Diffuse"
}

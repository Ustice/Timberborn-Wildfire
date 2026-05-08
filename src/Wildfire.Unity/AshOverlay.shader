Shader "Wildfire/AshOverlay"
{
    Properties
    {
        _AshTex ("Ash Texture", 2D) = "white" {}
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
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _AshTex;
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float ash = saturate(i.color.a);
                fixed4 ashTexel = tex2D(_AshTex, i.uv);
                float mask = tex2D(_MaskTex, i.uv).r;
                float midpoint = lerp(_ThresholdHigh, _ThresholdLow, ash);
                float coverage = 1.0 / (1.0 + exp(-_SigmoidSharpness * (mask - midpoint)));
                return fixed4(ashTexel.rgb, coverage * ash * _MaxOpacity);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Transparent"
}

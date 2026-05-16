// GPU-driven smoke/steam cloud billboard shader.
// Each instance is one cell (instanceID = cellIndex).
// Reads packed atmospheric data directly from _AtmosphericFields (no CPU readback).
// _IsSteam selects the steam channel (white) vs. smoke channel (gray→burgundy by contamination).
// Blend: SrcAlpha OneMinusSrcAlpha (standard alpha).
// Requires #pragma target 4.5 for StructuredBuffer in vertex stage.
Shader "Wildfire/Cloud"
{
    Properties
    {
        _BaseColor    ("Base Color",               Color)       = (0.45, 0.45, 0.45, 1)
        _ContamColor  ("Contamination Color",      Color)       = (0.35, 0.05, 0.10, 1)
        _Radius       ("Billboard Radius (world)", Float)       = 0.65
        _HeightOffset ("Center Height Offset",     Float)       = 1.2
        _MaxOpacity   ("Max Opacity",              Range(0, 1)) = 0.45
        _IsSteam      ("Is Steam (0=smoke 1=steam)", Float)     = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+2"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // Atmospheric fields: packed uint per cell.
            // bits  0-2  : steam (0-7)
            // bits  3-5  : smoke (0-7)
            // bits  6-8  : smoke contamination (0-7)
            // bits  9-11 : ash (0-7)
            // bits 12-14 : ash contamination (0-7)
            StructuredBuffer<uint>   _AtmosphericFields;
            StructuredBuffer<float3> _CellWorldPositions;

            float4 _BaseColor;
            float4 _ContamColor;
            float  _Radius;
            float  _HeightOffset;
            float  _MaxOpacity;
            float  _IsSteam;

            // Six corners for a screen-facing billboard quad (two CCW triangles).
            static const float2 BillboardCorners[6] =
            {
                float2(-0.5, -0.5), float2( 0.5, -0.5), float2( 0.5,  0.5),
                float2(-0.5, -0.5), float2( 0.5,  0.5), float2(-0.5,  0.5),
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float  intensity : TEXCOORD1;
                float  contam    : TEXCOORD2;
            };

            v2f vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                v2f o;
                uint atm = _AtmosphericFields[instanceID];

                float steam       = (float)((atm >> 0u) & 0x7u) / 7.0;
                float smoke       = (float)((atm >> 3u) & 0x7u) / 7.0;
                float smokeContam = (float)((atm >> 6u) & 0x7u) / 7.0;

                float intensity = _IsSteam > 0.5 ? steam : smoke;
                float contam    = _IsSteam > 0.5 ? 0.0  : smokeContam;

                // Cull invisible cells.
                if (intensity < 0.02)
                {
                    o.pos       = float4(2.0, 2.0, 2.0, 1.0);
                    o.uv        = float2(0.5, 0.5);
                    o.intensity = 0.0;
                    o.contam    = 0.0;
                    return o;
                }

                float3 worldPos = _CellWorldPositions[instanceID];
                worldPos.y += _HeightOffset;

                // Camera right and up from the view matrix (world-space columns).
                float3 camRight = float3(UNITY_MATRIX_V[0][0], UNITY_MATRIX_V[1][0], UNITY_MATRIX_V[2][0]);
                float3 camUp    = float3(UNITY_MATRIX_V[0][1], UNITY_MATRIX_V[1][1], UNITY_MATRIX_V[2][1]);

                float2 corner = BillboardCorners[vertexID];
                float3 vPos   = worldPos
                    + camRight * (corner.x * _Radius)
                    + camUp    * (corner.y * _Radius);

                o.pos       = UnityWorldToClipPos(vPos);
                o.uv        = corner + 0.5;
                o.intensity = intensity;
                o.contam    = contam;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Soft circular alpha falloff.
                float d     = length(i.uv - 0.5) * 2.0;
                float alpha = saturate(1.0 - d * d) * i.intensity * _MaxOpacity;
                float3 col  = lerp(_BaseColor.rgb, _ContamColor.rgb, saturate(i.contam));
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}

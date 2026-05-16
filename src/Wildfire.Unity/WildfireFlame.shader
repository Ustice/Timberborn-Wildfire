// GPU-driven flame tongue shader. Each instance is one tongue slot within a cell.
// instanceID = cellIndex * 5 + tongueSlot.
// Reads fire intensity directly from _VisualFields (no CPU readback).
// Blend: SrcAlpha One (additive) — fire adds light without occluding.
// Requires #pragma target 4.5 for StructuredBuffer in vertex stage.
Shader "Wildfire/Flame"
{
    Properties
    {
        _MaxTongueHeight ("Max Tongue Height", Float) = 2.0
        _BaseWidth       ("Base Width",        Float) = 0.5
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha One
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

            StructuredBuffer<float4> _VisualFields;
            StructuredBuffer<float3> _CellWorldPositions;

            float _MaxTongueHeight;
            float _BaseWidth;

            // Five within-cell base offsets (normalized to BaseWidth units).
            static const float2 TongueBaseOffsets[5] =
            {
                float2( 0.00,  0.00),
                float2( 0.30,  0.20),
                float2(-0.30,  0.20),
                float2( 0.20, -0.25),
                float2(-0.20, -0.25),
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float  tHeight : TEXCOORD0;   // 0 = base, 1 = tip
                float  fire    : TEXCOORD1;
            };

            // Cheap hash for deterministic per-tongue wobble phase.
            float Hash(uint n)
            {
                n ^= n << 13u;
                n ^= n >> 17u;
                n ^= n << 5u;
                return frac((float)(n & 0x7FFFFFFFu) / 2147483648.0);
            }

            v2f vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                v2f o;
                uint cellIndex = instanceID / 5u;
                uint slot      = instanceID % 5u;
                float4 visual  = _VisualFields[cellIndex];
                float fire     = visual.x;

                // Cull invisible tongue slots by pushing off-screen.
                uint activeTongues = min(5u, max(1u, (uint)ceil(fire * 5.0)));
                if (fire < 0.02 || slot >= activeTongues)
                {
                    o.pos     = float4(2.0, 2.0, 2.0, 1.0);
                    o.tHeight = 0.0;
                    o.fire    = 0.0;
                    return o;
                }

                float3 base = _CellWorldPositions[cellIndex];
                float2 ofs  = TongueBaseOffsets[slot] * _BaseWidth;
                base.x += ofs.x;
                base.z += ofs.y;

                float height    = fire * _MaxTongueHeight;
                float phase     = Hash(cellIndex * 5u + slot) * 6.28318;
                float speed     = 2.0 + fire * 3.0;
                float wobbleMag = fire * _BaseWidth * 0.4;
                float wx        = sin(_Time.y * speed + phase)             * wobbleMag;
                float wz        = cos(_Time.y * speed * 0.71 + phase + 1.3) * wobbleMag;

                float3 tipPos = base + float3(wx, height, wz);

                float3 vPos;
                float  tHeight;
                if (vertexID == 0u)
                {
                    vPos    = base + float3(-_BaseWidth * 0.5, 0.0, 0.0);
                    tHeight = 0.0;
                }
                else if (vertexID == 1u)
                {
                    vPos    = base + float3( _BaseWidth * 0.5, 0.0, 0.0);
                    tHeight = 0.0;
                }
                else
                {
                    vPos    = tipPos;
                    tHeight = 1.0;
                }

                o.pos     = UnityWorldToClipPos(vPos);
                o.tHeight = tHeight;
                o.fire    = fire;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = i.tHeight;

                // Yellow-white at base → orange mid → dark red at tip.
                float3 col = lerp(
                    lerp(float3(1.0, 0.88, 0.25), float3(1.0, 0.38, 0.0), saturate(t * 1.2)),
                    float3(0.5, 0.02, 0.0),
                    saturate((t - 0.5) * 2.0)
                );

                // Alpha fades toward the tip; overall brightness scales with intensity.
                float alpha = saturate(1.0 - t * 0.85) * i.fire;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}

Shader "Wildfire/AshOverlay"
{
    Properties
    {
        _AshTex ("Ash Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.9
        _Level1Coverage ("Level 1 Coverage", Range(0, 1)) = 0.30
        _Level2Coverage ("Level 2 Coverage", Range(0, 1)) = 0.65
        _Level3Coverage ("Level 3 Coverage", Range(0, 1)) = 0.92
        _CoverageSoftness ("Coverage Softness", Range(0, 0.2)) = 0.065
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZTest LEqual
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _AshTex;
            sampler2D _MaskTex;
            StructuredBuffer<uint> _CompanionFields;
            StructuredBuffer<uint> _AtmosphericFields;
            float _UseCompanionAsh;
            float _UseAtmosphericAsh;
            int _GridWidth;
            int _GridHeight;
            int _GridDepth;
            float _MaxOpacity;
            float _Level1Coverage;
            float _Level2Coverage;
            float _Level3Coverage;
            float _CoverageSoftness;
            int _WildfireVisualRegionCount;
            int _WildfireVisualRegionStride;
            float _WildfireTime;
            static const float AshBoundaryWarpCells = 0.25;
            static const uint FireChannel = 1u;
            static const uint SmokeChannel = 2u;
            static const uint SteamChannel = 4u;
            static const uint SparkEligibleChannel = 8u;
            static const float BufferFireMarker = -40.0;
            static const float BufferSmokePuffMarker = -30.0;
            static const float BufferSteamPuffMarker = -31.0;

            struct WildfireVisualRegion
            {
                float4 PositionAndRadius;
                float4 Intensities;
                float4 Bounds;
                uint ChannelMask;
                uint Seed;
                uint SourceCount;
                uint Reserved;
            };

            StructuredBuffer<WildfireVisualRegion> _WildfireVisualRegions;

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
                float3 worldPos : TEXCOORD2;
                float4 fireData : TEXCOORD3;
            };

            uint CompanionIndex(int x, int y, int z)
            {
                int clampedX = clamp(x, 0, max(_GridWidth - 1, 0));
                int clampedY = clamp(y, 0, max(_GridHeight - 1, 0));
                int clampedZ = clamp(z, 0, max(_GridDepth - 1, 0));
                return (uint)((clampedZ * _GridWidth * _GridHeight) + (clampedY * _GridWidth) + clampedX);
            }

            float2 CompanionAshAndContamination(int x, int y, int z)
            {
                if (x < 0 || y < 0 || z < 0 || x >= _GridWidth || y >= _GridHeight || z >= _GridDepth)
                {
                    return float2(0.0, 0.0);
                }

                uint companion = _CompanionFields[CompanionIndex(x, y, z)];
                return float2(
                    (float)((companion >> 16) & 0x3u) / 3.0,
                    (float)((companion >> 18) & 0x3u) / 3.0);
            }

            uint CompanionMaterialClass(int x, int y, int z)
            {
                if (x < 0 || y < 0 || z < 0 || x >= _GridWidth || y >= _GridHeight || z >= _GridDepth)
                {
                    return 0u;
                }

                return _CompanionFields[CompanionIndex(x, y, z)] & 0xFFu;
            }

            bool IsEntityMaterial(int x, int y, int z)
            {
                uint materialClass = CompanionMaterialClass(x, y, z);
                return materialClass == 2u ||
                    materialClass == 3u ||
                    materialClass == 4u ||
                    materialClass == 5u ||
                    materialClass == 6u ||
                    materialClass == 7u;
            }

            bool IsAshLandingSurface(int x, int y, int z)
            {
                uint materialClass = CompanionMaterialClass(x, y, z);
                return materialClass == 1u || (IsEntityMaterial(x, y, z) && CompanionMaterialClass(x, y, z - 1) != materialClass);
            }

            float2 AtmosphericFalloutAshAndContamination(int x, int y, int z)
            {
                if (_UseAtmosphericAsh <= 0.5 || x < 0 || y < 0 || z < 0 || x >= _GridWidth || y >= _GridHeight || z >= _GridDepth)
                {
                    return float2(0.0, 0.0);
                }

                uint atmospheric = _AtmosphericFields[CompanionIndex(x, y, z)] & 0xFFFFu;
                uint ash = (atmospheric >> 9) & 0x7u;
                uint contamination = (atmospheric >> 12) & 0x7u;
                float ashLevel = ash == 0u ? 0.0 : (float)min(3u, max(1u, (ash + 1u) / 2u)) / 3.0;
                float contaminationLevel = contamination == 0u ? 0.0 : (float)min(3u, max(1u, (contamination + 1u) / 2u)) / 3.0;
                return float2(ashLevel, contaminationLevel);
            }

            float2 AshBoundaryWarp(float2 worldXz)
            {
                float2 tileUv = worldXz / 16.0;
                float2 broad = float2(
                    tex2D(_MaskTex, tileUv + float2(0.173, 0.619)).r,
                    tex2D(_MaskTex, tileUv + float2(0.731, 0.281)).r) - 0.5;
                float2 detail = float2(
                    tex2D(_MaskTex, (tileUv * 2.0) + float2(0.417, 0.137)).r,
                    tex2D(_MaskTex, (tileUv * 2.0) + float2(0.269, 0.853)).r) - 0.5;
                return ((broad * 0.68) + (detail * 0.32)) * (AshBoundaryWarpCells * 2.0);
            }

            float AshBoundaryBlend(float t)
            {
                return smoothstep(0.22, 0.78, t);
            }

            float2 FeatheredCompanionAsh(float3 worldPos, int z)
            {
                float2 samplePoint = worldPos.xz + AshBoundaryWarp(worldPos.xz) - 0.5;
                int x = (int)floor(samplePoint.x);
                int y = (int)floor(samplePoint.y);
                if (!IsAshLandingSurface(x, y, z))
                {
                    return float2(0.0, 0.0);
                }

                float2 blend = float2(
                    AshBoundaryBlend(frac(samplePoint.x)),
                    AshBoundaryBlend(frac(samplePoint.y)));
                float2 lower = lerp(
                    CompanionAshAndContamination(x, y, z),
                    CompanionAshAndContamination(x + 1, y, z),
                    blend.x);
                float2 upper = lerp(
                    CompanionAshAndContamination(x, y + 1, z),
                    CompanionAshAndContamination(x + 1, y + 1, z),
                    blend.x);
                float2 projectedAsh = lerp(lower, upper, blend.y);
                for (int scanZ = z + 1; scanZ < _GridDepth; scanZ += 1)
                {
                    if (IsAshLandingSurface(x, y, scanZ))
                    {
                        break;
                    }

                    float2 atmosphericLower = lerp(
                        AtmosphericFalloutAshAndContamination(x, y, scanZ),
                        AtmosphericFalloutAshAndContamination(x + 1, y, scanZ),
                        blend.x);
                    float2 atmosphericUpper = lerp(
                        AtmosphericFalloutAshAndContamination(x, y + 1, scanZ),
                        AtmosphericFalloutAshAndContamination(x + 1, y + 1, scanZ),
                        blend.x);
                    projectedAsh = max(projectedAsh, lerp(atmosphericLower, atmosphericUpper, blend.y));
                }

                return projectedAsh;
            }

            float CoverageForAsh(float ash)
            {
                float level = saturate(ash) * 3.0;
                if (level <= 1.0)
                {
                    return lerp(0.0, _Level1Coverage, level);
                }

                if (level <= 2.0)
                {
                    return lerp(_Level1Coverage, _Level2Coverage, level - 1.0);
                }

                return lerp(_Level2Coverage, _Level3Coverage, level - 2.0);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float HashSeed(uint seed)
            {
                uint value = seed;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (float)(value & 65535u) / 65535.0;
            }

            float2 PerlinGradient(float2 p)
            {
                float angle = frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453) * 6.2831853;
                return float2(cos(angle), sin(angle));
            }

            float PerlinNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = dot(PerlinGradient(i + float2(0.0, 0.0)), f - float2(0.0, 0.0));
                float b = dot(PerlinGradient(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
                float c = dot(PerlinGradient(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
                float d = dot(PerlinGradient(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            float SigmoidLevels(float value, float center, float contrast)
            {
                return 1.0 / (1.0 + exp(-(value - center) * contrast));
            }

            float3 FireDualMaskData(float2 uv, float height, float seed, float intensity)
            {
                float speed = lerp(0.7, 1.85, intensity);
                float upwardA = _WildfireTime * speed;
                float upwardB = _WildfireTime * (speed * 1.27 + 0.18);
                float2 maskUvA = float2(uv.x * 1.45 + upwardA * 0.32 + seed * 19.1, height * 2.75 - upwardA + seed * 7.3);
                float2 maskUvB = float2(uv.x * 1.85 - upwardB * 0.29 - seed * 13.7, height * 3.25 - upwardB - seed * 11.9);
                float islandA =
                    PerlinNoise(maskUvA) * 0.55 +
                    PerlinNoise(maskUvA * 2.2 + float2(4.9, 2.2)) * 0.28 +
                    PerlinNoise(maskUvA * 4.0 + float2(12.7, 8.3)) * 0.17;
                float islandB =
                    PerlinNoise(maskUvB) * 0.53 +
                    PerlinNoise(maskUvB * 2.1 + float2(9.1, 6.4)) * 0.29 +
                    PerlinNoise(maskUvB * 3.9 + float2(2.6, 14.2)) * 0.18;
                float maskA = SigmoidLevels(islandA, 0.50, 15.0);
                float maskB = SigmoidLevels(islandB, 0.51, 16.0);
                return float3(maskA, maskB, min(maskA, maskB));
            }

            float FireSparkMask(float2 uv, float height, float seed, float intensity, float sparkEligible)
            {
                float sparkCount = floor(lerp(1.0, 5.0, intensity)) * sparkEligible;
                float sparkVelocity = lerp(1.4, 3.2, intensity);
                float2 sparkUv = float2(
                    uv.x * 2.6 + seed * 41.0,
                    height * 3.0 - _WildfireTime * sparkVelocity + seed * 17.0);
                float sparkNoise = PerlinNoise(sparkUv * 5.5 + float2(23.0, 11.0));
                float risingBand = frac(height * sparkCount - _WildfireTime * sparkVelocity + seed * 9.0);
                float boundedBand = smoothstep(0.04, 0.11, risingBand) * (1.0 - smoothstep(0.14, 0.24, risingBand));
                float upperBody = smoothstep(0.30, 0.72, height) * (1.0 - smoothstep(0.86, 1.0, height));
                return boundedBand * upperBody * smoothstep(0.88, 0.97, sparkNoise) * sparkEligible;
            }

            float IsMarker(float marker, float expected)
            {
                return 1.0 - step(0.25, abs(marker - expected));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.color = v.color;
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.fireData = 0.0;
                float3 objectPosition = v.vertex.xyz;
                if (IsMarker(v.uv2.y, BufferFireMarker) > 0.5)
                {
                    int slot = (int)round(v.uv2.x);
                    int safeSlot = clamp(slot, 0, max(_WildfireVisualRegionCount - 1, 0));
                    bool active = slot >= 0 && slot < _WildfireVisualRegionCount && _WildfireVisualRegionStride == 64;
                    WildfireVisualRegion region = _WildfireVisualRegions[safeSlot];
                    active = active && ((region.ChannelMask & FireChannel) != 0u);
                    float intensity = active ? saturate(region.Intensities.x) : 0.0;
                    float sparkEligible = active && ((region.ChannelMask & SparkEligibleChannel) != 0u) ? 1.0 : 0.0;
                    float seed = active ? HashSeed(region.Seed) : 0.0;
                    float radius = active ? max(0.25, region.PositionAndRadius.w) : 0.25;
                    float baseWidth = radius * lerp(0.85, 1.45, intensity);
                    float height = max(0.9, radius * lerp(1.65, 4.15, intensity));
                    float normalizedHeight = saturate(objectPosition.y);
                    float swaySpeed = lerp(1.8, 4.6, intensity);
                    float swayAmplitude = radius * lerp(0.035, 0.18, intensity);
                    float tongueSeed = Hash21(float2(seed, v.color.r));
                    float sway =
                        sin(_WildfireTime * swaySpeed + tongueSeed * 31.0 + normalizedHeight * 5.5) * 0.66 +
                        sin(_WildfireTime * (swaySpeed * 1.63) + tongueSeed * 71.0 + normalizedHeight * 9.0) * 0.34;
                    float2 swayDirection = normalize(float2(0.35 + seed, 0.75 - seed));
                    float2 anchoredSway = swayDirection * sway * swayAmplitude * normalizedHeight * normalizedHeight;
                    float angle = seed * 6.2831853;
                    float s = sin(angle);
                    float c = cos(angle);
                    float2 rotated = float2(
                        (objectPosition.x * c) - (objectPosition.z * s),
                        (objectPosition.x * s) + (objectPosition.z * c));
                    rotated += anchoredSway;
                    objectPosition = float3(
                        region.PositionAndRadius.x + rotated.x * baseWidth,
                        region.PositionAndRadius.y + 0.12 + objectPosition.y * height,
                        region.PositionAndRadius.z + rotated.y * baseWidth);
                    objectPosition = active ? objectPosition : float3(0.0, 0.0, 0.0);
                    o.color = float4(intensity, radius, seed, active ? 1.0 : 0.0);
                    o.fireData = float4(intensity, seed, sparkEligible, active ? 1.0 : 0.0);
                    o.worldPos = objectPosition;
                    o.vertex = UnityObjectToClipPos(float4(objectPosition, 1.0));
                    return o;
                }

                if (IsMarker(v.uv2.y, BufferSmokePuffMarker) > 0.5 || IsMarker(v.uv2.y, BufferSteamPuffMarker) > 0.5)
                {
                    int slot = (int)round(v.uv2.x);
                    int safeSlot = clamp(slot, 0, max(_WildfireVisualRegionCount - 1, 0));
                    bool active = slot >= 0 && slot < _WildfireVisualRegionCount && _WildfireVisualRegionStride == 64;
                    WildfireVisualRegion region = _WildfireVisualRegions[safeSlot];
                    bool isSteam = IsMarker(v.uv2.y, BufferSteamPuffMarker) > 0.5;
                    uint channel = isSteam ? SteamChannel : SmokeChannel;
                    active = active && ((region.ChannelMask & channel) != 0u);
                    float intensity = active ? saturate(isSteam ? region.Intensities.z : region.Intensities.y) : 0.0;
                    float contamination = active && !isSteam ? saturate(region.Intensities.w) : 0.0;
                    float seed = active ? HashSeed(region.Seed) : 0.0;
                    float radius = active ? max(0.45, region.PositionAndRadius.w) : 0.45;
                    float puffHash = saturate(v.color.r);
                    float age = frac(_WildfireTime * (isSteam ? 0.46 : 0.19) + seed + puffHash * 0.37);
                    float fade = isSteam
                        ? (1.0 - smoothstep(0.34, 1.0, age))
                        : (1.0 - smoothstep(0.72, 1.0, age));
                    float expand = isSteam
                        ? lerp(0.58, 1.65, age)
                        : lerp(0.76, 1.28, age);
                    float verticalDrift = radius * (isSteam ? lerp(0.45, 1.8, age) : lerp(0.22, 1.05, age));
                    float2 downwind = normalize(float2(seed - 0.5, 0.78 + puffHash * 0.3));
                    float windDrift = radius * (isSteam ? 0.42 : 0.72) * age * lerp(0.35, 1.0, intensity);
                    float phase = _WildfireTime * (isSteam ? 1.5 : 0.72) + seed * 31.0 + puffHash * 17.0;
                    float wobble = sin(phase + v.vertex.y * 2.6) * radius * (isSteam ? 0.07 : 0.11);
                    float scale = radius * expand * lerp(isSteam ? 0.74 : 0.95, isSteam ? 1.28 : 1.55, intensity);
                    objectPosition = float3(
                        region.PositionAndRadius.x + v.vertex.x * scale + downwind.x * windDrift + wobble,
                        region.PositionAndRadius.y + 0.32 + v.vertex.y * scale + verticalDrift,
                        region.PositionAndRadius.z + v.vertex.z * scale + downwind.y * windDrift);
                    objectPosition = active ? objectPosition : float3(0.0, 0.0, 0.0);
                    o.color = float4(intensity, contamination, seed, active ? fade : 0.0);
                    o.fireData = float4(isSteam ? 2.0 : 1.0, seed, fade, active ? 1.0 : 0.0);
                    o.worldPos = objectPosition;
                    o.vertex = UnityObjectToClipPos(float4(objectPosition, 1.0));
                    return o;
                }

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (IsMarker(i.uv2.y, BufferFireMarker) > 0.5)
                {
                    clip(i.color.a - 0.001);
                    float intensity = saturate(i.color.r);
                    float seed = saturate(i.fireData.y);
                    float sparkEligible = saturate(i.fireData.z);
                    float height = saturate(i.uv.y);
                    float edge = 1.0 - smoothstep(0.48, 0.82, abs(i.uv.x - 0.5));
                    float3 dualMasks = FireDualMaskData(i.uv, height, seed, intensity);
                    float lowerBodyAnchor = 1.0 - smoothstep(0.12, 0.34, height);
                    float brokenIslands = lerp(1.0, dualMasks.z, smoothstep(0.18, 0.58, height));
                    clip(lerp(1.0, brokenIslands, smoothstep(0.16, 0.84, height)) - 0.34);
                    float baseAnchor = lerp(0.88, 1.0, smoothstep(0.0, 0.06, height));
                    float tipFade = 1.0 - smoothstep(0.74, 1.0, height + (1.0 - dualMasks.x) * 0.08);
                    float neckFade = lerp(1.0, 0.5, smoothstep(0.48, 1.0, height));
                    float alpha = saturate(edge * max(lowerBodyAnchor, brokenIslands) * baseAnchor * tipFade * neckFade * lerp(0.68, 0.94, intensity));
                    float3 baseColor = lerp(float3(1.0, 0.58, 0.08), float3(1.0, 0.96, 0.34), smoothstep(0.2, 1.0, intensity));
                    float3 bodyColor = float3(1.0, 0.34, 0.035);
                    float3 tipColor = float3(0.95, 0.08, 0.015);
                    float3 color = height < 0.34
                        ? lerp(baseColor, bodyColor, smoothstep(0.0, 0.34, height))
                        : lerp(bodyColor, tipColor, smoothstep(0.34, 1.0, height));
                    float spark = FireSparkMask(i.uv, height, seed, intensity, sparkEligible);
                    color = lerp(color, float3(1.0, 0.94, 0.30), saturate(spark * 0.75));
                    alpha = saturate(max(alpha, spark * 0.42));
                    return fixed4(color, alpha);
                }

                if (IsMarker(i.uv2.y, BufferSmokePuffMarker) > 0.5 || IsMarker(i.uv2.y, BufferSteamPuffMarker) > 0.5)
                {
                    clip(i.color.a - 0.001);
                    bool isSteam = i.fireData.x > 1.5;
                    float intensity = saturate(i.color.r);
                    float contamination = isSteam ? 0.0 : saturate(i.color.g);
                    float seed = saturate(i.color.b);
                    float2 centered = i.uv * 2.0 - 1.0;
                    float radius = length(centered * float2(isSteam ? 0.82 : 0.74, 1.0));
                    float body = 1.0 - smoothstep(0.18, 1.0, radius);
                    float softEdge = 1.0 - smoothstep(0.64, 1.0, radius);
                    float breakup = PerlinNoise(i.uv * (isSteam ? 2.2 : 3.1) + i.worldPos.xz * 0.13 + seed * 11.0 + _WildfireTime * (isSteam ? 0.16 : 0.055));
                    float lowerFade = smoothstep(-0.08, 0.16, centered.y);
                    float upperFade = 1.0 - smoothstep(isSteam ? 0.42 : 0.68, 1.04, centered.y);
                    float alpha = body * softEdge * lowerFade * upperFade * lerp(0.62, 1.0, breakup);
                    alpha *= (isSteam ? lerp(0.16, 0.42, intensity) : lerp(0.34, 0.82, intensity)) * i.color.a;
                    float3 smokeClean = lerp(float3(0.34, 0.34, 0.32), float3(0.70, 0.70, 0.66), breakup);
                    float3 smokeDirty = lerp(float3(0.26, 0.20, 0.18), float3(0.55, 0.43, 0.38), breakup);
                    float3 steamColor = lerp(float3(0.74, 0.90, 1.0), float3(1.0, 1.0, 1.0), saturate(i.uv.y + breakup * 0.32));
                    float3 smokeColor = lerp(smokeClean, smokeDirty, contamination);
                    return fixed4(isSteam ? steamColor : smokeColor, saturate(alpha));
                }

                if (i.uv2.x < -25.0)
                {
                    float intensity = saturate(i.color.r);
                    float2 centered = i.uv * 2.0 - 1.0;
                    float t = _Time.y * (0.55 + i.color.b * 0.25) + i.color.b * 8.0;
                    centered.x += sin(t + i.uv.y * 4.0) * 0.18;
                    float radius = length(centered * float2(0.72, 1.0));
                    float body = 1.0 - smoothstep(0.28, 1.08, radius);
                    float vertical = 1.0 - smoothstep(0.62, 1.0, i.uv.y);
                    float alpha = body * vertical * (0.18 + intensity * 0.52) * i.color.a;
                    float3 color = lerp(float3(0.66, 0.92, 1.0), float3(1.0, 1.0, 1.0), saturate(i.uv.y + body * 0.25));
                    return fixed4(color, saturate(alpha));
                }

                if (i.uv2.x < -15.0)
                {
                    float intensity = saturate(i.color.r);
                    float contamination = saturate(i.color.g);
                    float2 drift = float2(
                        sin(_Time.y * 0.45 + i.color.b * 11.0),
                        cos(_Time.y * 0.38 + i.color.b * 7.0)) * 0.13;
                    float2 centered = i.uv * 2.0 - 1.0 + drift;
                    float radius = length(centered * float2(0.78, 1.0));
                    float body = 1.0 - smoothstep(0.24, 1.05, radius);
                    float breakup = smoothstep(0.18, 0.84, Hash21(i.worldPos.xz * 0.37 + i.uv * 2.3 + _Time.y * 0.035));
                    float vertical = 1.0 - smoothstep(0.72, 1.0, i.uv.y);
                    float alpha = body * lerp(0.72, 1.0, breakup) * vertical * (0.28 + intensity * 0.58) * i.color.a;
                    float3 clean = lerp(float3(0.28, 0.28, 0.26), float3(0.68, 0.68, 0.64), breakup);
                    float3 dirty = lerp(float3(0.20, 0.16, 0.14), float3(0.50, 0.42, 0.36), breakup);
                    return fixed4(lerp(clean, dirty, contamination), saturate(alpha));
                }

                if (i.uv2.x < -5.0)
                {
                    float intensity = saturate(i.color.r);
                    float seed = i.color.b;
                    float2 centered = i.uv * 2.0 - 1.0;
                    float height = saturate(i.uv.y);
                    float sway = sin(_Time.y * 8.0 + seed * 19.0 + height * 6.0) * 0.18 * height;
                    centered.x += sway;
                    float width = lerp(0.62, 0.2, height);
                    float flame = 1.0 - smoothstep(width, width + 0.28, abs(centered.x));
                    flame *= smoothstep(0.0, 0.16, height) * (1.0 - smoothstep(0.78, 1.0, height));
                    float lick = smoothstep(0.28, 0.92, Hash21(i.worldPos.xz * 0.91 + i.uv * 4.7 + _Time.y * 0.18));
                    float alpha = saturate(flame * lerp(0.72, 1.15, lick) * (0.72 + intensity * 0.34));
                    float3 baseColor = float3(1.0, 0.86, 0.18);
                    float3 bodyColor = float3(1.0, 0.36, 0.035);
                    float3 tipColor = float3(0.86, 0.08, 0.015);
                    float3 color = height < 0.28
                        ? lerp(baseColor, bodyColor, smoothstep(0.0, 0.28, height))
                        : lerp(bodyColor, tipColor, smoothstep(0.28, 1.0, height));
                    return fixed4(color * (1.0 + intensity * 0.35), alpha * i.color.a);
                }

                bool useCompanionAsh = _UseCompanionAsh > 0.5 && i.uv2.x >= 0.0;
                float2 companionAsh = useCompanionAsh
                    ? FeatheredCompanionAsh(i.worldPos, (int)floor(i.uv2.y + 0.5))
                    : float2(saturate(i.color.a), 0.0);
                float ash = saturate(companionAsh.x);
                fixed4 ashTexel = tex2D(_AshTex, i.uv);
                float mask = tex2D(_MaskTex, i.uv).r;
                float targetCoverage = CoverageForAsh(ash);
                float threshold = 1.0 - targetCoverage;
                float coverage = targetCoverage <= 0.0
                    ? 0.0
                    : smoothstep(threshold - _CoverageSoftness, threshold + _CoverageSoftness, mask);
                float3 visibleAsh = lerp(
                    float3(0.18, 0.155, 0.12),
                    ashTexel.rgb * float3(0.74, 0.68, 0.58),
                    0.62);
                visibleAsh = lerp(visibleAsh, visibleAsh * float3(0.72, 0.84, 0.42), saturate(companionAsh.y));
                return fixed4(visibleAsh, saturate(coverage * _MaxOpacity));
            }
            ENDCG
        }
    }

    Fallback "Transparent/Diffuse"
}

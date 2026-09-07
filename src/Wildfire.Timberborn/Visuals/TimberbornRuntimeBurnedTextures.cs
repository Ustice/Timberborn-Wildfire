using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.GoodStackSystem;
using Timberborn.Goods;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.Yielding;
using UnityEngine;

namespace Wildfire.Timberborn.Visuals;

public static class TimberbornRuntimeBurnedTextureBehavior
{
    public const string TreeComponentProbeFailedToken = "wildfire_timberborn_tree_component_probe_failed";
    public const string TreeKillAlreadyTerminalToken = "wildfire_timberborn_tree_kill_skipped";
    public const string TreeBurnedLeftoverAlreadyTerminalToken = "wildfire_timberborn_tree_burned_leftover_skipped";
    public const string TreeBurnedTextureRendererSkippedToken =
        "wildfire_timberborn_tree_burned_texture_renderer_skipped";
    public const string TreeBurnedTextureMaterialSkippedToken =
        "wildfire_timberborn_tree_burned_texture_material_skipped";
    public const string CropBurnedResourceDeletedReason = "native_entity_service";

    public static bool ShouldSkipStaleTreeComponentProbe(Exception exception) =>
        exception is NullReferenceException or InvalidOperationException;

    public static bool ShouldSkipInvalidRendererOrMaterial(Exception exception) =>
        exception is MissingReferenceException or NullReferenceException;


}

public sealed class TimberbornRuntimeBurnedTextureDeriver
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<EntityId, Texture2D> _burnedTexturesBySourceId = new();

    public TimberbornRuntimeBurnedTextureDeriver(ITimberbornFireLogSink? logSink = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public Texture2D? DeriveBurnedTexture(Texture sourceTexture, string textureLabel)
    {
        EntityId sourceId = sourceTexture.GetEntityId();
        if (_burnedTexturesBySourceId.TryGetValue(sourceId, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        try
        {
            Texture2D burnedTexture = CreateBurnedTexture(sourceTexture, textureLabel);
            _burnedTexturesBySourceId[sourceId] = burnedTexture;
            _logSink.Info(
                "wildfire_timberborn_burned_texture_derived " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"source_texture={TimberbornQaCommandBridge.FormatToken(sourceTexture.name)} " +
                $"width={burnedTexture.width} height={burnedTexture.height}");
            return burnedTexture;
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_burned_texture_derive_failed " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"source_texture={TimberbornQaCommandBridge.FormatToken(sourceTexture.name)} " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            return null;
        }
    }

    private static Texture2D CreateBurnedTexture(Texture sourceTexture, string textureLabel)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            depthBuffer: 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture? previousRenderTexture = RenderTexture.active;

        try
        {
            Graphics.Blit(sourceTexture, renderTexture);
            RenderTexture.active = renderTexture;
            Texture2D readableTexture = new(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGBA32,
                mipChain: true)
            {
                name = $"{textureLabel} RuntimeBurned",
                wrapMode = sourceTexture.wrapMode,
                filterMode = sourceTexture.filterMode,
                hideFlags = HideFlags.HideAndDontSave,
            };
            readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            readableTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            int width = readableTexture.width;
            int height = readableTexture.height;
            Color32[] pixels = readableTexture.GetPixels32();
            Color32[] burnedPixels = pixels
                .Select((pixel, index) => CharPixel(pixel, index % width, index / width, width, height))
                .ToArray();
            readableTexture.SetPixels32(burnedPixels);
            readableTexture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return readableTexture;
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static Color32 CharPixel(Color32 pixel, int x, int y, int width, int height)
    {
        float u = width <= 1 ? 0f : x / (float)(width - 1);
        float v = height <= 1 ? 0f : y / (float)(height - 1);
        float luminance = ((pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f)) / 255f;
        float verticalGrain = Mathf.PerlinNoise(u * 24f, v * 5f);
        float barkGrooves = Mathf.PerlinNoise(u * 72f, v * 16f);
        float ashNoise = Mathf.PerlinNoise((u + 17.13f) * 180f, (v + 3.91f) * 180f);
        float exposedNoise = Mathf.PerlinNoise((u + 41.7f) * 42f, (v + 9.25f) * 9f);
        float charcoal = Mathf.Clamp01(0.035f + luminance * 0.11f + verticalGrain * 0.045f - barkGrooves * 0.035f);
        Color color = new(charcoal, charcoal * 0.92f, charcoal * 0.78f, pixel.a / 255f);

        if (exposedNoise > 0.78f && luminance > 0.18f)
        {
            float exposed = Mathf.InverseLerp(0.78f, 1f, exposedNoise) *
                Mathf.InverseLerp(0.18f, 0.55f, luminance);
            color = Color.Lerp(color, new Color(0.32f, 0.14f, 0.055f, pixel.a / 255f), exposed * 0.42f);
        }

        if (ashNoise > 0.9f)
        {
            float ash = Mathf.InverseLerp(0.9f, 1f, ashNoise);
            color = Color.Lerp(color, new Color(0.28f, 0.27f, 0.25f, pixel.a / 255f), ash * 0.32f);
        }

        return new Color32(ToByte(color.r), ToByte(color.g), ToByte(color.b), pixel.a);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}

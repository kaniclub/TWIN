using System.Text.Json;
using TrueWindow.Models;

namespace TrueWindow.Formats;

// ----------------------------
// format 1.x 定義 reader
// ----------------------------
internal sealed class TrueWindowFormat100Reader : ITrueWindowFormatReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // format 1.x を読めるか判定する
    public bool CanRead(string formatVersion)
    {
        return formatVersion.StartsWith("1.", StringComparison.OrdinalIgnoreCase);
    }

    // format 1.x の JSON を内部定義へ変換する
    public TrueWindowDefinitionSet Read(string rawJson)
    {
        Format100TargetFile file = JsonSerializer.Deserialize<Format100TargetFile>(rawJson, JsonOptions) ?? new Format100TargetFile();
        TrueWindowDefinitionSet results = new();

        foreach (Format100TargetDefinition target in file.Targets ?? Enumerable.Empty<Format100TargetDefinition>())
        {
            if (string.IsNullOrWhiteSpace(target.TrueWindowId))
            {
                continue;
            }

            string trueWindowId = target.TrueWindowId.Trim();
            results.FurnitureTargets.Add(new TrueWindowTargetDefinition
            {
                TrueWindowId = trueWindowId,
                ReservedColor = string.IsNullOrWhiteSpace(target.ReservedColor) ? "#FF00FF" : target.ReservedColor.Trim(),
                SpecifiedImagePath = string.IsNullOrWhiteSpace(target.SpecifiedImage) ? null : target.SpecifiedImage.Trim(),
                RenderLightGlows = target.RenderLightGlows ?? false,
                ViewSettings = new TrueWindowViewSettings
                {
                    OffsetX = target.ViewSettings?.OffsetX ?? 0,
                    OffsetY = target.ViewSettings?.OffsetY ?? 0,
                    Zoom = target.ViewSettings?.Zoom ?? 1.0f,
                    ParallaxStrength = target.ViewSettings?.ParallaxStrength ?? 0.2f,
                },
                AllowRightClickConfig = target.AllowRightClickConfig ?? true,
            });
        }

        foreach (Format100MapTargetDefinition target in file.MapTargets ?? Enumerable.Empty<Format100MapTargetDefinition>())
        {
            if (string.IsNullOrWhiteSpace(target.TrueWindowId))
            {
                continue;
            }

            string trueWindowId = target.TrueWindowId.Trim();
            results.MapTargets.Add(new TrueWindowMapTargetDefinition
            {
                TrueWindowId = trueWindowId,
                LocationNames = (target.LocationNames ?? new List<string>())
                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                    .Select(static name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                MapPaths = (target.MapPaths ?? new List<string>())
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .Select(static path => path.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Area = target.Area?.ToRectangle() ?? Microsoft.Xna.Framework.Rectangle.Empty,
                SourceLayers = (target.SourceLayers ?? new List<string>())
                    .Where(static layer => !string.IsNullOrWhiteSpace(layer))
                    .Select(static layer => layer.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                OverlayDrawAfterLayer = string.IsNullOrWhiteSpace(target.OverlayDrawAfterLayer) ? string.Empty : target.OverlayDrawAfterLayer.Trim(),
                ReservedColor = string.IsNullOrWhiteSpace(target.ReservedColor) ? "#FF00FF" : target.ReservedColor.Trim(),
                SpecifiedImagePath = string.IsNullOrWhiteSpace(target.SpecifiedImage) ? null : target.SpecifiedImage.Trim(),
                RenderLightGlows = target.RenderLightGlows ?? false,
                ViewSettings = new TrueWindowViewSettings
                {
                    OffsetX = target.ViewSettings?.OffsetX ?? 0,
                    OffsetY = target.ViewSettings?.OffsetY ?? 0,
                    Zoom = target.ViewSettings?.Zoom ?? 1.0f,
                    ParallaxStrength = target.ViewSettings?.ParallaxStrength ?? 0.2f,
                },
                Enabled = target.Enabled ?? true,
            });
        }

        return results;
    }
}

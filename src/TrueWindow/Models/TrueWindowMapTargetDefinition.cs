using Microsoft.Xna.Framework;
using StardewValley;

namespace TrueWindow.Models;

// ----------------------------
// TrueWindow 対応 MAP 対象の定義モデル
// ----------------------------
internal sealed class TrueWindowMapTargetDefinition
{
    public string TrueWindowId { get; set; } = string.Empty;

    public List<string> LocationNames { get; set; } = new();

    public List<string> MapPaths { get; set; } = new();

    public Rectangle Area { get; set; }

    public List<string> SourceLayers { get; set; } = new();

    public string OverlayDrawAfterLayer { get; set; } = string.Empty;

    public string ReservedColor { get; set; } = "#FF00FF";

    public string? SpecifiedImagePath { get; set; }

    public bool RenderLightGlows { get; set; }

    public TrueWindowViewSettings ViewSettings { get; set; } = new();

    public bool Enabled { get; set; } = true;

    // 既定値付きの定義を作る
    public static TrueWindowMapTargetDefinition CreateDefault(string trueWindowId)
    {
        return new TrueWindowMapTargetDefinition
        {
            TrueWindowId = trueWindowId,
            LocationNames = new List<string>(),
            MapPaths = new List<string>(),
            Area = Rectangle.Empty,
            SourceLayers = new List<string>(),
            OverlayDrawAfterLayer = string.Empty,
            ReservedColor = "#FF00FF",
            SpecifiedImagePath = null,
            RenderLightGlows = false,
            ViewSettings = new TrueWindowViewSettings
            {
                OffsetX = 0,
                OffsetY = 0,
                Zoom = 1.0f,
                ParallaxStrength = 0.2f,
            },
            Enabled = true,
        };
    }

    // 既定値へプリセット定義を上書き適用する
    public static TrueWindowMapTargetDefinition MergeWithDefaults(string trueWindowId, TrueWindowMapTargetDefinition? preset)
    {
        TrueWindowMapTargetDefinition merged = CreateDefault(trueWindowId);
        if (preset is null)
        {
            return merged;
        }

        merged.TrueWindowId = string.IsNullOrWhiteSpace(preset.TrueWindowId) ? trueWindowId : preset.TrueWindowId;
        merged.LocationNames = preset.LocationNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        merged.MapPaths = preset.MapPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        merged.Area = preset.Area;
        merged.SourceLayers = preset.SourceLayers
            .Where(static layer => !string.IsNullOrWhiteSpace(layer))
            .Select(static layer => layer.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        merged.OverlayDrawAfterLayer = string.IsNullOrWhiteSpace(preset.OverlayDrawAfterLayer) ? string.Empty : preset.OverlayDrawAfterLayer.Trim();
        merged.ReservedColor = string.IsNullOrWhiteSpace(preset.ReservedColor) ? merged.ReservedColor : preset.ReservedColor;
        merged.SpecifiedImagePath = string.IsNullOrWhiteSpace(preset.SpecifiedImagePath) ? null : preset.SpecifiedImagePath;
        merged.RenderLightGlows = preset.RenderLightGlows;
        merged.Enabled = preset.Enabled;
        merged.ViewSettings = new TrueWindowViewSettings
        {
            OffsetX = preset.ViewSettings.OffsetX,
            OffsetY = preset.ViewSettings.OffsetY,
            Zoom = preset.ViewSettings.Zoom <= 0 ? merged.ViewSettings.Zoom : preset.ViewSettings.Zoom,
            ParallaxStrength = preset.ViewSettings.ParallaxStrength,
        };

        return merged;
    }

    // 現在ロケーションへ適用できるか判定する
    public bool Matches(GameLocation location)
    {
        bool hasLocationNames = this.LocationNames.Count > 0;
        bool hasMapPaths = this.MapPaths.Count > 0;
        if (!hasLocationNames && !hasMapPaths)
        {
            return false;
        }

        bool locationMatched = !hasLocationNames || this.LocationNames.Any(name =>
            string.Equals(name, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase));

        string? mapPath = location.mapPath?.Value;
        bool mapPathMatched = !hasMapPaths || this.MapPaths.Any(path =>
            string.Equals(path, mapPath, StringComparison.OrdinalIgnoreCase));

        return locationMatched && mapPathMatched;
    }
}

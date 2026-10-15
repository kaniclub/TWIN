using System.Text.Json;
using Microsoft.Xna.Framework;

namespace TrueWindow.Models;

// ----------------------------
// CP定義ファイル読み込み用モデル
// ----------------------------
internal sealed class ContentPatcherRoot
{
    public List<ContentPatcherChange>? Changes { get; set; }

    public JsonElement? TrueWindow { get; set; }
}

internal sealed class ContentPatcherChange
{
    public string? Action { get; set; }
    public string? Target { get; set; }
    public string? FromFile { get; set; }
}

internal sealed class Format100TargetFile
{
    public string? Format { get; set; }
    public List<Format100TargetDefinition>? Targets { get; set; }
    public List<Format100MapTargetDefinition>? MapTargets { get; set; }
}

internal sealed class Format100TargetDefinition
{
    public string? TrueWindowId { get; set; }
    public string? ReservedColor { get; set; }
    public string? SpecifiedImage { get; set; }
    public bool? RenderLightGlows { get; set; }
    public Format100ViewSettings? ViewSettings { get; set; }
    public bool? AllowRightClickConfig { get; set; }
}

internal sealed class Format100ViewSettings
{
    public int? OffsetX { get; set; }
    public int? OffsetY { get; set; }
    public float? Zoom { get; set; }
    public float? ParallaxStrength { get; set; }
}

internal sealed class Format100MapTargetDefinition
{
    public string? TrueWindowId { get; set; }
    public List<string>? LocationNames { get; set; }
    public List<string>? MapPaths { get; set; }
    public Format100AreaDefinition? Area { get; set; }
    public List<string>? SourceLayers { get; set; }
    public string? OverlayDrawAfterLayer { get; set; }
    public string? ReservedColor { get; set; }
    public string? SpecifiedImage { get; set; }
    public bool? RenderLightGlows { get; set; }
    public Format100ViewSettings? ViewSettings { get; set; }
    public bool? Enabled { get; set; }
}

internal sealed class Format100AreaDefinition
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public Rectangle ToRectangle()
    {
        return new Rectangle(this.X ?? 0, this.Y ?? 0, this.Width ?? 0, this.Height ?? 0);
    }
}

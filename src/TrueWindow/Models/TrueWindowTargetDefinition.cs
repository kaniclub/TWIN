namespace TrueWindow.Models;

// ----------------------------
// TrueWindow 対応対象の定義モデル
// ----------------------------
internal sealed class TrueWindowTargetDefinition
{
    public string TrueWindowId { get; set; } = string.Empty;

    public string ReservedColor { get; set; } = "#FF00FF";

    public string? SpecifiedImagePath { get; set; }


    public bool RenderLightGlows { get; set; }

    public TrueWindowViewSettings ViewSettings { get; set; } = new();

    public bool AllowRightClickConfig { get; set; } = true;

    // 既定値付きの定義を作る
    public static TrueWindowTargetDefinition CreateDefault(string trueWindowId)
    {
        return new TrueWindowTargetDefinition
        {
            TrueWindowId = trueWindowId,
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
            AllowRightClickConfig = true,
        };
    }

    // 既定値へプリセット定義を上書き適用する
    public static TrueWindowTargetDefinition MergeWithDefaults(string trueWindowId, TrueWindowTargetDefinition? preset)
    {
        TrueWindowTargetDefinition merged = CreateDefault(trueWindowId);
        if (preset is null)
        {
            return merged;
        }

        merged.TrueWindowId = string.IsNullOrWhiteSpace(preset.TrueWindowId) ? trueWindowId : preset.TrueWindowId;
        merged.ReservedColor = string.IsNullOrWhiteSpace(preset.ReservedColor) ? merged.ReservedColor : preset.ReservedColor;
        merged.SpecifiedImagePath = string.IsNullOrWhiteSpace(preset.SpecifiedImagePath) ? null : preset.SpecifiedImagePath;
        merged.RenderLightGlows = preset.RenderLightGlows;
        merged.AllowRightClickConfig = preset.AllowRightClickConfig;
        merged.ViewSettings = new TrueWindowViewSettings
        {
            OffsetX = preset.ViewSettings.OffsetX,
            OffsetY = preset.ViewSettings.OffsetY,
            Zoom = preset.ViewSettings.Zoom <= 0 ? merged.ViewSettings.Zoom : preset.ViewSettings.Zoom,
            ParallaxStrength = preset.ViewSettings.ParallaxStrength,
        };

        return merged;
    }
}

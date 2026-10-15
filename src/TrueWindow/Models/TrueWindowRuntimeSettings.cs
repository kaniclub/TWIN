namespace TrueWindow.Models;

// ----------------------------
// 共有表示元のモード
// ----------------------------
internal enum TrueWindowDisplaySourceMode
{
    Auto,
    Location,
    SpecifiedImage,
}

// ----------------------------
// 実行時設定モデル
// ----------------------------
internal sealed class TrueWindowRuntimeSettings
{
    public TrueWindowViewSettings ViewSettings { get; set; } = new();

    public TrueWindowDisplaySourceMode DisplaySourceMode { get; set; } = TrueWindowDisplaySourceMode.Auto;

    public string? SelectedImageSourceId { get; set; }

    // 設定内容を複製する
    public TrueWindowRuntimeSettings Clone()
    {
        return new TrueWindowRuntimeSettings
        {
            ViewSettings = this.ViewSettings.Clone(),
            DisplaySourceMode = this.DisplaySourceMode,
            SelectedImageSourceId = this.SelectedImageSourceId,
        };
    }
}

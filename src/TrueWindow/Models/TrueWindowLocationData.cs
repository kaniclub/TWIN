namespace TrueWindow.Models;

// ----------------------------
// ロケーションごとの保存データ
// ----------------------------
internal sealed class TrueWindowLocationData
{
    public string LocationName { get; set; } = string.Empty;

    public TrueWindowRuntimeSettings RuntimeSettings { get; set; } = new();
}

namespace TrueWindow.Models;

// ----------------------------
// セーブデータのルートモデル
// ----------------------------
internal sealed class TrueWindowSaveData
{
    public List<TrueWindowLocationData> Locations { get; set; } = new();
}

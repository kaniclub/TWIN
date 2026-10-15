using StardewModdingAPI;

namespace TrueWindow.Integrations;

// ----------------------------
// Generic Mod Config Menu API 定義
// ----------------------------
public interface IGenericModConfigMenuApi
{
    // 設定画面へ MOD を登録する
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    // 既存登録を解除する
    void Unregister(IManifest mod);

    // 整数設定を追加する
    void AddNumberOption(
        IManifest mod,
        Func<int> getValue,
        Action<int> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        int? min = null,
        int? max = null,
        int? interval = null,
        Func<int, string>? formatValue = null,
        string? fieldId = null);
}

using TrueWindow.Models;

// ----------------------------
// 表示元関連の共通補助型
// ----------------------------
namespace TrueWindow.Services;

// 指定画像の読み込み結果をまとめる
internal sealed class SpecifiedImageData
{
    public SpecifiedImageData(string path, int width, int height, Microsoft.Xna.Framework.Color[] pixels)
    {
        this.Path = path;
        this.Width = width;
        this.Height = height;
        this.Pixels = pixels;
    }

    public string Path { get; }
    public int Width { get; }
    public int Height { get; }
    public Microsoft.Xna.Framework.Color[] Pixels { get; }
}

// 選択可能な画像ソースをまとめる
internal sealed record ImageSourceOption(
    string SourceId,
    string DisplayName,
    string? FilePath,
    string? GameAssetName,
    bool IsBuiltIn);

// ロケーション共有設定の既定値候補をまとめる
internal sealed record TrueWindowDisplayDefaults(
    TrueWindowViewSettings ViewSettings,
    string? SpecifiedImagePath);

// 実際に使用する表示元をまとめる
internal sealed record TrueWindowResolvedDisplaySource(
    TrueWindowDisplaySourceMode Mode,
    string? SelectedImageSourceId);

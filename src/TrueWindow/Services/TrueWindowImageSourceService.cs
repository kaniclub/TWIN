using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

// ----------------------------
// 画像ソースの読み込みとキャッシュを扱うサービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowImageSourceService
{
    private readonly IModHelper helper;

    // 指定画像の読み込み結果をキャッシュする
    private readonly Dictionary<string, SpecifiedImageData> specifiedImageCache = new(StringComparer.OrdinalIgnoreCase);

    public TrueWindowImageSourceService(IModHelper helper)
    {
        this.helper = helper;
    }

    // 指定画像を取得または読み込む
    public SpecifiedImageData? TryGetSpecifiedImage(string? selectedImageSourceId, List<ImageSourceOption> imageOptions)
    {
        if (string.IsNullOrWhiteSpace(selectedImageSourceId))
        {
            return null;
        }

        ImageSourceOption? option = imageOptions.FirstOrDefault(option => string.Equals(option.SourceId, selectedImageSourceId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            return null;
        }

        if (this.specifiedImageCache.TryGetValue(option.SourceId, out SpecifiedImageData? existing))
        {
            return existing;
        }

        try
        {
            Texture2D? loadedTexture = null;
            FileStream? stream = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(option.GameAssetName))
                {
                    loadedTexture = this.TryLoadGameImage(option.GameAssetName);
                }
                else if (!string.IsNullOrWhiteSpace(option.FilePath) && File.Exists(option.FilePath))
                {
                    stream = File.OpenRead(option.FilePath);
                    loadedTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                }

                if (loadedTexture is null)
                {
                    return null;
                }

                Color[] pixels = new Color[loadedTexture.Width * loadedTexture.Height];
                loadedTexture.GetData(pixels);
                SpecifiedImageData created = new(option.SourceId, loadedTexture.Width, loadedTexture.Height, pixels);
                this.specifiedImageCache[option.SourceId] = created;
                return created;
            }
            finally
            {
                stream?.Dispose();
                if (!option.IsBuiltIn)
                {
                    loadedTexture?.Dispose();
                }
            }
        }
        catch
        {
            return null;
        }
    }

    // 画像キャッシュを破棄する
    public void Clear()
    {
        this.specifiedImageCache.Clear();
    }

    // ゲーム本体の画像アセットを読み込む
    private Texture2D? TryLoadGameImage(string assetName)
    {
        string normalized = assetName.Replace((char)92, '/');
        string[] candidates =
        {
            normalized,
            normalized + ".png",
            normalized.Replace('/', (char)92),
            (normalized + ".png").Replace('/', (char)92),
        };

        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return this.helper.GameContent.Load<Texture2D>(candidate);
            }
            catch
            {
            }
        }

        return null;
    }
}

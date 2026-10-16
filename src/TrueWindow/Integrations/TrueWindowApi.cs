using TrueWindow.Services;

namespace TrueWindow.Integrations;

public sealed class TrueWindowApi
{
    private readonly RegisteredImageCatalog catalog;
    private readonly Action invalidateImages;

    internal TrueWindowApi(RegisteredImageCatalog catalog, Action invalidateImages)
    {
        this.catalog = catalog;
        this.invalidateImages = invalidateImages;
    }

    /// <summary>TrueWindowの表示元に画像を追加する。同じMOD・画像IDでの再登録は置き換え。</summary>
    /// <param name="modId">登録元MODのUniqueID。</param>
    /// <param name="imageId">MOD内で一意なID。セーブに保存するため変更しないこと。</param>
    /// <param name="displayName">設定画面に表示する名前。</param>
    /// <param name="filePath">画像ファイルの絶対パス。</param>
    public void RegisterImageSource(string modId, string imageId, string displayName, string filePath)
    {
        catalog.Register(modId, imageId, displayName, filePath);
        invalidateImages();
    }
}

using Microsoft.Xna.Framework;
using StardewValley;
using TrueWindow.Models;

// ----------------------------
// ズーム・オフセット・視差を共通計算するサービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowViewTransformService
{
    private readonly ModConfig config;

    public TrueWindowViewTransformService(ModConfig config)
    {
        this.config = config;
    }

    // 外景用 viewport を計算する
    public xTile.Dimensions.Rectangle BuildExteriorViewport(GameLocation interior, TrueWindowRuntimeSettings runtimeSettings, Vector2 baseCenter, int renderWidthPx, int renderHeightPx)
    {
        float zoom = Math.Clamp(runtimeSettings.ViewSettings.Zoom, this.config.MinEditableZoom, this.config.MaxRenderScale);
        int viewWidth = Math.Max(32, (int)Math.Round(renderWidthPx / zoom));
        int viewHeight = Math.Max(32, (int)Math.Round(renderHeightPx / zoom));

        Vector2 center = baseCenter
            + new Vector2(runtimeSettings.ViewSettings.OffsetX, runtimeSettings.ViewSettings.OffsetY)
            + this.GetLocationParallaxOffset(interior, runtimeSettings.ViewSettings.ParallaxStrength);

        int x = (int)Math.Round(center.X - viewWidth / 2f);
        int y = (int)Math.Round(center.Y - viewHeight / 2f);
        return new xTile.Dimensions.Rectangle(x, y, viewWidth, viewHeight);
    }

    // 指定画像表示用の仮想 viewport を作る
    public Vector4 BuildSpecifiedImageViewport(TrueWindowViewSettings viewSettings, int roomWidth, int roomHeight)
    {
        float zoom = Math.Clamp(viewSettings.Zoom, this.config.MinEditableZoom, this.config.MaxRenderScale);
        float viewWidth = Math.Max(1f, roomWidth / zoom);
        float viewHeight = Math.Max(1f, roomHeight / zoom);

        Vector2 center = new Vector2(roomWidth / 2f, roomHeight / 2f)
            + new Vector2(viewSettings.OffsetX, viewSettings.OffsetY)
            + this.GetImageParallaxOffset(roomWidth, roomHeight, viewSettings.ParallaxStrength);

        return new Vector4(
            center.X - (viewWidth / 2f),
            center.Y - (viewHeight / 2f),
            viewWidth,
            viewHeight);
    }

    // ロケーション表示用の視差量を計算する
    public Vector2 GetLocationParallaxOffset(GameLocation interior, float strength)
    {
        Rectangle roomBounds = new(0, 0, interior.map.DisplayWidth, interior.map.DisplayHeight);
        Vector2 roomCenter = roomBounds.Center.ToVector2();
        Vector2 cameraCenter = new(Game1.viewport.X + Game1.viewport.Width / 2f, Game1.viewport.Y + Game1.viewport.Height / 2f);
        return -(cameraCenter - roomCenter) * Math.Clamp(strength, 0f, 1f);
    }

    // 指定画像表示用の視差量を計算する
    public Vector2 GetImageParallaxOffset(int roomWidth, int roomHeight, float strength)
    {
        Vector2 roomCenter = new(roomWidth / 2f, roomHeight / 2f);
        Vector2 cameraCenter = new(Game1.viewport.X + Game1.viewport.Width / 2f, Game1.viewport.Y + Game1.viewport.Height / 2f);
        return -(cameraCenter - roomCenter) * Math.Clamp(strength, 0f, 1f);
    }
}

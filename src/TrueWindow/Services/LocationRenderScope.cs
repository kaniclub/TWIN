using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using System.Reflection;

// ----------------------------
// 外景レンダリング用スコープ
// ----------------------------
namespace TrueWindow.Services;

internal sealed class LocationRenderScope : IDisposable
{
    // 描画用の一時切替ではLocationChangedの通知・音楽・トリガーを発生させない。
    private static readonly FieldInfo? CurrentLocationField = typeof(Game1).GetField("instanceGameLocation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private readonly GameLocation originalLocation;
    private readonly xTile.Dimensions.Rectangle originalViewport;
    private readonly Color originalAmbientLight;
    private readonly float originalAlpha;
    private readonly Building? hiddenBuilding;

    // スコープ開始時に描画対象ロケーションへ切り替える
    public LocationRenderScope(GameLocation targetLocation, xTile.Dimensions.Rectangle targetViewport, Building? hiddenBuilding)
    {
        this.originalLocation = Game1.currentLocation;
        this.originalViewport = Game1.viewport;
        this.hiddenBuilding = hiddenBuilding;
        this.originalAmbientLight = Game1.ambientLight;
        this.originalAlpha = hiddenBuilding?.alpha ?? 1f;

        SetRenderLocation(targetLocation);
        Game1.viewport = targetViewport;

        if (targetLocation.IsOutdoors)
        {
            Game1.ambientLight = Color.White;
        }

        if (hiddenBuilding is not null)
        {
            hiddenBuilding.alpha = 0f;
        }
    }

    // 元の状態へ戻す
    public void Dispose()
    {
        if (this.hiddenBuilding is not null)
        {
            this.hiddenBuilding.alpha = this.originalAlpha;
        }

        SetRenderLocation(this.originalLocation);
        Game1.viewport = this.originalViewport;
        Game1.ambientLight = this.originalAmbientLight;
    }

    private static void SetRenderLocation(GameLocation location)
    {
        if (CurrentLocationField is not null) CurrentLocationField.SetValue(Game1.game1, location);
        else Game1.currentLocation = location;
    }
}

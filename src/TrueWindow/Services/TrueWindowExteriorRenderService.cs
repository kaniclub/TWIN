using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using TrueWindow.Models;

// ----------------------------
// 外景レンダリングサービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowExteriorRenderService
{
    private readonly ModConfig config;
    private readonly ParentBuildingLocator parentBuildingLocator;
    private readonly TrueWindowViewTransformService viewTransformService;
    private readonly Dictionary<string, RenderTarget2D> renderTargetCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TrueWindowRenderSnapshot> renderSnapshotCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> lastRenderedTick = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (RenderTarget2D Target, long Revision, Rectangle Bounds, Color[] Pixels)> pixelCache = new(StringComparer.OrdinalIgnoreCase);
    private long snapshotRevision;
    private SpriteBatch? offscreenSpriteBatch;
    private SpriteBatch? readbackSpriteBatch;
    private readonly Dictionary<string, RenderTarget2D> readbackTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReusableOpenGlReadback reusableReadback = new();

    // 外景レンダリングサービスを初期化する
    public TrueWindowExteriorRenderService(ModConfig config, ParentBuildingLocator parentBuildingLocator, TrueWindowViewTransformService viewTransformService)
    {
        this.config = config;
        this.parentBuildingLocator = parentBuildingLocator;
        this.viewTransformService = viewTransformService;
    }

    public bool IsOffscreenRendering { get; private set; }

    // 外景を描画して返す
    public RenderTarget2D? RenderForLocation(GameLocation interior, string cacheKey, TrueWindowRuntimeSettings runtimeSettings, bool renderLightGlows)
    {
        GameLocation? exterior = this.ResolveExteriorLocation(interior);
        if (exterior is null)
        {
            return null;
        }

        int roomWidth = Math.Max(4, interior.map.DisplayWidth);
        int roomHeight = Math.Max(4, interior.map.DisplayHeight);
        Building? parentBuilding = this.parentBuildingLocator.Find(interior);
        xTile.Dimensions.Rectangle sourceViewport = this.viewTransformService.BuildExteriorViewport(
            interior,
            runtimeSettings,
            this.GetBaseCenter(parentBuilding),
            roomWidth,
            roomHeight);

        RenderTarget2D target = this.GetOrCreateTarget(cacheKey, Math.Max(4, sourceViewport.Width), Math.Max(4, sourceViewport.Height));
        long currentTick = Game1.ticks;
        if (this.lastRenderedTick.TryGetValue(cacheKey, out long lastTick)
            && this.renderSnapshotCache.TryGetValue(cacheKey, out var cached) && ReferenceEquals(cached.Target, target)
            && currentTick - lastTick < Math.Max(1, this.config.RenderThrottleFrames))
        {
            return target;
        }

        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport previousViewport = device.Viewport;

        try
        {
            this.IsOffscreenRendering = true;
            this.offscreenSpriteBatch ??= new SpriteBatch(device);

            device.SetRenderTarget(target);
            device.Viewport = new Viewport(0, 0, target.Width, target.Height);
            device.Clear(Color.Transparent);

            using LocationRenderScope _ = new(exterior, sourceViewport, parentBuilding);
            this.DrawLocationToTarget(exterior, renderLightGlows);
            this.renderSnapshotCache[cacheKey] = new TrueWindowRenderSnapshot(target,
                new Rectangle(sourceViewport.X, sourceViewport.Y, sourceViewport.Width, sourceViewport.Height),
                roomWidth, roomHeight, ++this.snapshotRevision);
            this.lastRenderedTick[cacheKey] = currentTick;
            return target;
        }
        catch
        {
            return this.renderTargetCache.TryGetValue(cacheKey, out RenderTarget2D? fallback) ? fallback : null;
        }
        finally
        {
            if (previousTargets.Length > 0)
            {
                device.SetRenderTargets(previousTargets);
            }
            else
            {
                device.SetRenderTarget(null);
            }

            device.Viewport = previousViewport;
            this.IsOffscreenRendering = false;
        }
    }

    // キャッシュ済みの外景を取得する
    public bool TryGetCachedSnapshot(string cacheKey, out TrueWindowRenderSnapshot snapshot)
    {
        if (this.renderSnapshotCache.TryGetValue(cacheKey, out TrueWindowRenderSnapshot existing))
        {
            snapshot = existing;
            return true;
        }

        snapshot = default;
        return false;
    }

    // 大きな画像バッファは再利用し、外景が更新されたときだけGPUから読み戻す。
    public SnapshotPixelData GetSnapshotPixels(string key, TrueWindowRenderSnapshot snapshot, Rectangle bounds)
    {
        int length = bounds.Width * bounds.Height;
        if (!pixelCache.TryGetValue(key, out var cached) || !ReferenceEquals(cached.Target, snapshot.Target) || cached.Pixels.Length < length)
            cached = (snapshot.Target, -1, Rectangle.Empty, GC.AllocateArray<Color>(length, pinned: true));
        if (cached.Revision != snapshot.Revision || cached.Bounds != bounds)
        {
            // MonoGame/OpenGLは範囲指定GetDataでも元テクスチャ全体の一時配列を作る。
            // 先にGPU上で1:1に切り出し、元画像全体の転送・一時確保を避ける。
            RenderTarget2D compact = CopyReadbackRegion(key, snapshot.Target, bounds);
            if (!reusableReadback.TryRead(key, compact, cached.Pixels))
            {
                compact.GetData(cached.Pixels, 0, length);
            }
            cached.Revision = snapshot.Revision;
            cached.Bounds = bounds;
            pixelCache[key] = cached;
        }
        return new SnapshotPixelData(cached.Pixels, bounds);
    }

    private RenderTarget2D CopyReadbackRegion(string key, RenderTarget2D source, Rectangle bounds)
    {
        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        if (!readbackTargets.TryGetValue(key, out RenderTarget2D? compact) || compact.Width != bounds.Width || compact.Height != bounds.Height)
        {
            compact?.Dispose();
            compact = new RenderTarget2D(device, bounds.Width, bounds.Height, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            readbackTargets[key] = compact;
        }
        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport previousViewport = device.Viewport;
        bool began = false;
        try
        {
            device.SetRenderTarget(compact);
            device.Viewport = new Viewport(0, 0, bounds.Width, bounds.Height);
            readbackSpriteBatch ??= new SpriteBatch(device);
            readbackSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            began = true;
            readbackSpriteBatch.Draw(source, new Rectangle(0, 0, bounds.Width, bounds.Height), bounds, Color.White);
            readbackSpriteBatch.End();
            began = false;
        }
        finally
        {
            if (began) { try { readbackSpriteBatch?.End(); } catch { } }
            if (previousTargets.Length > 0) device.SetRenderTargets(previousTargets);
            else device.SetRenderTarget(null);
            device.Viewport = previousViewport;
        }
        return compact;
    }

    // 外景キャッシュを全て破棄する
    public void Clear()
    {
        foreach (RenderTarget2D target in this.renderTargetCache.Values)
        {
            target.Dispose();
        }

        this.renderTargetCache.Clear();
        this.renderSnapshotCache.Clear();
        this.lastRenderedTick.Clear();
        this.pixelCache.Clear();
        reusableReadback.Dispose();
        foreach (RenderTarget2D compact in readbackTargets.Values) compact.Dispose();
        readbackTargets.Clear();
        readbackSpriteBatch?.Dispose();
        readbackSpriteBatch = null;
        this.offscreenSpriteBatch?.Dispose();
        this.offscreenSpriteBatch = null;
    }

    // 対応する外景ロケーションを決定する
    private GameLocation? ResolveExteriorLocation(GameLocation interior)
    {
        if (interior is FarmHouse or Cabin)
        {
            return Game1.getFarm();
        }

        Building? parentBuilding = this.parentBuildingLocator.Find(interior);
        return parentBuilding is null ? null : this.TryGetParentLocation(parentBuilding);
    }

    // 建物から親ロケーションを取得する
    private GameLocation? TryGetParentLocation(Building building)
    {
        try
        {
            return building.GetType().GetMethod("GetParentLocation", Type.EmptyTypes)?.Invoke(building, null) as GameLocation;
        }
        catch
        {
            return null;
        }
    }

    // 外景の基準中心を求める
    private Vector2 GetBaseCenter(Building? parentBuilding)
    {
        if (parentBuilding is null)
        {
            Farm farm = Game1.getFarm();
            return new Vector2(farm.map.DisplayWidth / 2f, farm.map.DisplayHeight / 2f);
        }

        Rectangle bounds = new(
            parentBuilding.tileX.Value * Game1.tileSize,
            parentBuilding.tileY.Value * Game1.tileSize,
            parentBuilding.tilesWide.Value * Game1.tileSize,
            parentBuilding.tilesHigh.Value * Game1.tileSize);

        const int margin = 128;
        return new Vector2(bounds.Center.X, bounds.Top - margin);
    }

    // ロケーションを RenderTarget へ描画する
    private void DrawLocationToTarget(GameLocation location, bool renderLightGlows)
    {
        if (this.offscreenSpriteBatch is null)
        {
            return;
        }

        SpriteBatch b = this.offscreenSpriteBatch;
        Game1.mapDisplayDevice.BeginScene(b);
        try
        {
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            Game1.background?.draw(b);
            location.drawBackground(b);
            b.End();

            foreach (var backgroundLayer in location.backgroundLayers)
            {
                b.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.PointClamp);
                backgroundLayer.Key.Draw(Game1.mapDisplayDevice, Game1.viewport, xTile.Dimensions.Location.Origin, false, 4, -1f);
                b.End();
            }

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            location.drawWater(b);
            b.End();

            b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);
            location.drawFloorDecorations(b);

            float layerStep = 0.1f;
            for (int i = 0; i < location.buildingLayers.Count; i++)
            {
                float layer = location.buildingLayers.Count > 1 ? (float)i / (location.buildingLayers.Count - 1) : 0f;
                location.buildingLayers[i].Key.Draw(Game1.mapDisplayDevice, Game1.viewport, xTile.Dimensions.Location.Origin, false, 4, layerStep * layer);
            }

            using (new ExteriorLightGlowScope(location, renderLightGlows))
            {
                location.draw(b);
            }

            for (int i = 0; i < location.frontLayers.Count; i++)
            {
                float layer = location.frontLayers.Count > 1 ? (float)i / (location.frontLayers.Count - 1) : 0f;
                location.frontLayers[i].Key.Draw(Game1.mapDisplayDevice, Game1.viewport, xTile.Dimensions.Location.Origin, false, 4, 64f + layerStep * layer);
            }

            location.drawAboveFrontLayer(b);
            b.End();

            foreach (var alwaysFrontLayer in location.alwaysFrontLayers)
            {
                b.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.PointClamp);
                alwaysFrontLayer.Key.Draw(Game1.mapDisplayDevice, Game1.viewport, xTile.Dimensions.Location.Origin, false, 4, -1f);
                b.End();
            }

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            location.drawAboveAlwaysFrontLayer(b);
            b.End();
        }
        finally
        {
            Game1.mapDisplayDevice.EndScene();
        }
    }

    // RenderTarget を取得または作成する
    private RenderTarget2D GetOrCreateTarget(string key, int width, int height)
    {
        if (this.renderTargetCache.TryGetValue(key, out RenderTarget2D? existing))
        {
            if (existing.Width == width && existing.Height == height)
            {
                return existing;
            }

            existing.Dispose();
            this.renderTargetCache.Remove(key);
            this.renderSnapshotCache.Remove(key);
            this.lastRenderedTick.Remove(key);
            this.pixelCache.Remove(key);
        }

        RenderTarget2D target = new(Game1.graphics.GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        this.renderTargetCache[key] = target;
        return target;
    }

    private sealed class ExteriorLightGlowScope : IDisposable
    {
        private readonly GameLocation location;
        private readonly bool restore;
        private readonly List<Vector2> savedLightGlows = new();

        public ExteriorLightGlowScope(GameLocation location, bool renderLightGlows)
        {
            this.location = location;
            if (renderLightGlows)
            {
                return;
            }

            this.restore = true;
            foreach (Vector2 lightGlow in location.lightGlows)
            {
                this.savedLightGlows.Add(lightGlow);
            }

            location.lightGlows.Clear();
            location.lightGlowLayerCache.Clear();
        }

        public void Dispose()
        {
            if (!this.restore)
            {
                return;
            }

            this.location.lightGlows.Clear();
            foreach (Vector2 lightGlow in this.savedLightGlows)
            {
                this.location.lightGlows.Add(lightGlow);
            }

            this.location.lightGlowLayerCache.Clear();
        }
    }
}

// 外景スナップショットの情報をまとめる
internal readonly record struct TrueWindowRenderSnapshot(
    RenderTarget2D Target,
    Rectangle SourceViewport,
    int RoomWidth,
    int RoomHeight,
    long Revision = 0);

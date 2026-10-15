using System.Reflection;
using Microsoft.Xna.Framework;
using Netcode;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.Objects;
using TrueWindow.Menus;
using TrueWindow.Models;

// ----------------------------
// TrueWindow 家具処理の中核サービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowFurnitureService
{
    // ランタイム用に横へ確保する最大スロット数
    private const int MaxRuntimeSlotsPerTexture = 64;

    // バニラ家具の雨・点灯用フレームずらしを無効化する private field
    private static readonly FieldInfo? SourceIndexOffsetField = typeof(Furniture).GetField("sourceIndexOffset", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly IModHelper helper;
    private readonly ModConfig config;
    private readonly TrueWindowTargetRegistry targetRegistry;
    private readonly TrueWindowLocationDisplayService locationDisplayService;
    private readonly TrueWindowMaskCache maskCache;
    private readonly TrueWindowExteriorRenderService exteriorRenderService;
    private readonly TrueWindowImageSourceService imageSourceService;
    private readonly TrueWindowViewTransformService viewTransformService;

    // 家具種別とプリセットごとのランタイムテクスチャ情報
    private readonly Dictionary<string, RuntimeTextureBinding> runtimeBindingsByKey = new(StringComparer.OrdinalIgnoreCase);

    // ランタイムアセット名ごとのランタイムテクスチャ状態
    private readonly Dictionary<string, RuntimeTextureState> runtimeStatesByAssetName = new(StringComparer.OrdinalIgnoreCase);


    // 現在ロケーションで TW 処理を走らせるべきかを管理する
    private LocationProcessingMode processingMode = LocationProcessingMode.OutdoorSkip;
    private string? monitoredLocationName;
    private ulong lastProbeTick;
    private int pendingProbeTicks;

    // 家具処理サービスを初期化する
    public TrueWindowFurnitureService(
        IModHelper helper,
        ModConfig config,
        TrueWindowTargetRegistry targetRegistry,
        TrueWindowLocationDisplayService locationDisplayService,
        TrueWindowMaskCache maskCache,
        TrueWindowExteriorRenderService exteriorRenderService,
        TrueWindowImageSourceService imageSourceService,
        TrueWindowViewTransformService viewTransformService)
    {
        this.helper = helper;
        this.config = config;
        this.targetRegistry = targetRegistry;
        this.locationDisplayService = locationDisplayService;
        this.maskCache = maskCache;
        this.exteriorRenderService = exteriorRenderService;
        this.imageSourceService = imageSourceService;
        this.viewTransformService = viewTransformService;
    }

    // 現在ロケーションで TW の重い処理を走らせる必要があるか返す
    public bool ShouldProcessCurrentLocation()
    {
        return this.processingMode == LocationProcessingMode.IndoorActive;
    }

    // タイトル戻り時などに監視状態を初期化する
    public void ResetMonitoringState()
    {
        this.processingMode = LocationProcessingMode.OutdoorSkip;
        this.monitoredLocationName = null;
        this.lastProbeTick = 0;
        this.pendingProbeTicks = 0;
    }

    // ワープ直後に、次ロケーションで処理が必要かを即時判定する
    public void OnWarped(GameLocation oldLocation, GameLocation newLocation)
    {
        this.RestoreLocationFurnitureSourceRects(oldLocation);
        this.exteriorRenderService.Clear();
        this.RefreshLocationState(newLocation, forceScan: true);
    }

    // 屋外では何もしない。屋内で TW 家具が無い場合だけ、再探索を実行する
    public void UpdateMonitoring(ulong currentTick)
    {
        if (!Context.IsWorldReady || this.exteriorRenderService.IsOffscreenRendering)
        {
            return;
        }

        GameLocation location = Game1.currentLocation;
        if (!string.Equals(this.monitoredLocationName, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            this.RefreshLocationState(location, forceScan: true);
            return;
        }

        if (this.processingMode == LocationProcessingMode.OutdoorSkip)
        {
            return;
        }

        if (this.processingMode == LocationProcessingMode.IndoorDormant)
        {
            if (this.pendingProbeTicks > 0)
            {
                this.pendingProbeTicks--;
                if (this.pendingProbeTicks == 0)
                {
                    this.RefreshLocationState(location, forceScan: true);
                    return;
                }
            }

            int interval = Math.Max(5, this.config.IndoorProbeIntervalTicks);
            if (currentTick - this.lastProbeTick >= (ulong)interval)
            {
                this.RefreshLocationState(location, forceScan: true);
            }
        }
    }

    // 屋内で家具配置や撤去が起きた可能性があるので、再探索予約を入れる
    public void NotifyPossibleFurnitureChange()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        GameLocation location = Game1.currentLocation;
        if (this.IsOutdoorLocation(location))
        {
            return;
        }

        if (this.processingMode == LocationProcessingMode.IndoorDormant)
        {
            this.pendingProbeTicks = 2;
        }
    }

    // 現在ロケーションが屋外か、屋内か、TW 家具ありかを判定する
    public void RefreshCurrentLocationState(bool forceScan)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.RefreshLocationState(Game1.currentLocation, forceScan);
    }

    // 現在ロケーションの共有外景を準備し、それを使って各家具用のランタイムテクスチャを更新する
    public void PrepareCurrentLocationTargets()
    {
        if (!Context.IsWorldReady || this.exteriorRenderService.IsOffscreenRendering)
        {
            return;
        }

        // 現在ロケーションの対象家具は既定 sourceRect に戻す
        this.RestoreCurrentLocationFurnitureSourceRects();
        this.processingMode = LocationProcessingMode.IndoorActive;

        List<TrueWindowRuntimeContext> contexts = this.BuildCurrentLocationContexts();

        if (contexts.Count == 0)
        {

            this.ResetAllRuntimeTexturesToBase();
            this.processingMode = LocationProcessingMode.IndoorDormant;
            this.lastProbeTick = (ulong)Game1.ticks;
            return;
        }

        string locationName = Game1.currentLocation.NameOrUniqueName;
        List<TrueWindowDisplayDefaults> displayDefaults = this.BuildDisplayDefaults(contexts);
        List<ImageSourceOption> imageOptions = this.locationDisplayService.BuildImageSourceOptions(displayDefaults);
        string? preferredSpecifiedImageSourceId = this.locationDisplayService.GetPreferredSpecifiedImageSourceId(displayDefaults, imageOptions);
        TrueWindowRuntimeSettings runtimeSettings = this.locationDisplayService.GetOrCreateRuntimeSettings(locationName, displayDefaults[0], preferredSpecifiedImageSourceId);
        TrueWindowResolvedDisplaySource resolvedDisplaySource = this.locationDisplayService.ResolveDisplaySource(runtimeSettings, imageOptions, preferredSpecifiedImageSourceId);
        TrueWindowDisplaySourceMode effectiveDisplaySourceMode = resolvedDisplaySource.Mode;

        // 窓家具自身が出す光源を制御する
        foreach (TrueWindowRuntimeContext context in contexts)
        {
            this.ApplyFurnitureLightGlowSetting(context.Furniture, context.Definition.RenderLightGlows);
        }

        SpecifiedImageData? specifiedImage = null;
        Dictionary<bool, SnapshotBundle> snapshotsByLightGlow = new();
        if (effectiveDisplaySourceMode == TrueWindowDisplaySourceMode.SpecifiedImage)
        {
            specifiedImage = this.imageSourceService.TryGetSpecifiedImage(resolvedDisplaySource.SelectedImageSourceId, imageOptions);
            if (specifiedImage is null)
            {
                effectiveDisplaySourceMode = TrueWindowDisplaySourceMode.Location;
            }
        }

        if (effectiveDisplaySourceMode == TrueWindowDisplaySourceMode.Location)
        {
            foreach (bool renderLightGlows in contexts.Select(static p => p.Definition.RenderLightGlows).Distinct())
            {
                string renderKey = this.BuildLocationRenderKey(runtimeSettings, renderLightGlows);
                this.exteriorRenderService.RenderForLocation(Game1.currentLocation, renderKey, runtimeSettings, renderLightGlows);

                if (!this.exteriorRenderService.TryGetCachedSnapshot(renderKey, out TrueWindowRenderSnapshot snapshot))
                {
                    this.ResetAllRuntimeTexturesToBase();
                    return;
                }

                Rectangle readbackBounds = GetReadbackBounds(contexts, renderLightGlows, snapshot);
                SnapshotPixelData snapshotPixels = this.exteriorRenderService.GetSnapshotPixels(renderKey, snapshot, readbackBounds);
                snapshotsByLightGlow[renderLightGlows] = new SnapshotBundle(snapshot, snapshotPixels);
            }
        }

        HashSet<string> updatedAssets = new(StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, TrueWindowRuntimeContext> group in contexts.GroupBy(static p => p.Binding.RuntimeAssetName, StringComparer.OrdinalIgnoreCase))
        {
            RuntimeTextureState state = this.GetOrCreateRuntimeTexture(group.First().Binding);
            updatedAssets.Add(state.Binding.RuntimeAssetName);

            // 対応家具の ParsedItemData が、このランタイム atlas を直接参照するようにする
            this.AssignRuntimeTextureToItem(state.Binding, state);

            Color[] atlasPixels = state.WorkingPixels;
            Array.Copy(state.BaseAtlasPixels, atlasPixels, atlasPixels.Length);

            int slotIndex = 0;
            foreach (TrueWindowRuntimeContext context in group
                         .OrderBy(static p => p.Furniture.TileLocation.Y)
                         .ThenBy(static p => p.Furniture.TileLocation.X))
            {
                if (slotIndex >= MaxRuntimeSlotsPerTexture)
                {
                    break;
                }

                Rectangle slotRect = new(
                    state.OriginalTextureWidth + (slotIndex * state.SlotWidth),
                    0,
                    context.SourceRect.Width,
                    context.SourceRect.Height);

                this.CopyRegion(
                    state.OriginalPixels,
                    state.OriginalTextureWidth,
                    state.OriginalTextureHeight,
                    context.SourceRect,
                    atlasPixels,
                    state.AtlasWidth,
                    state.AtlasHeight,
                    slotRect.Location);

                if (effectiveDisplaySourceMode == TrueWindowDisplaySourceMode.SpecifiedImage && specifiedImage is not null)
                {
                    this.CompositeSpecifiedImageIntoSlot(atlasPixels, state.AtlasWidth, state.AtlasHeight, slotRect, context, specifiedImage, runtimeSettings);
                }
                else
                {
                    SnapshotBundle snapshotBundle = snapshotsByLightGlow[context.Definition.RenderLightGlows];
                    this.CompositeExteriorIntoSlot(atlasPixels, state.AtlasWidth, state.AtlasHeight, slotRect, context, snapshotBundle.Snapshot, snapshotBundle.SnapshotPixels);
                }

                context.Furniture.sourceRect.Value = slotRect;
                this.ResetVanillaSourceIndexOffset(context.Furniture);

                slotIndex++;
            }

            if (!atlasPixels.AsSpan().SequenceEqual(state.UploadedPixels))
            {
                state.RuntimeTexture.SetData(atlasPixels);
                Array.Copy(atlasPixels, state.UploadedPixels, atlasPixels.Length);
            }
        }

        // 使っていないランタイムテクスチャは元画像状態へ戻す
        foreach (RuntimeTextureState state in this.runtimeStatesByAssetName.Values)
        {
            if (!updatedAssets.Contains(state.Binding.RuntimeAssetName))
            {
                this.ResetRuntimeTextureToBase(state);
            }
        }
    }

    // 現在ロケーションの一時 sourceRect を既定値へ戻す
    public void RestoreCurrentLocationFurnitureSourceRects()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.RestoreLocationFurnitureSourceRects(Game1.currentLocation);
    }

    // セーブ前などに、読み込み済みロケーションの一時 sourceRect をまとめて戻す
    public void RestoreLoadedFurnitureSourceRects()
    {
        foreach (GameLocation location in Game1.locations)
        {
            this.RestoreLocationFurnitureSourceRects(location);

            foreach (Building building in location.buildings)
            {
                if (building.indoors.Value is GameLocation indoors)
                {
                    this.RestoreLocationFurnitureSourceRects(indoors);
                }
            }
        }
    }

    // ランタイムテクスチャを初期状態へ戻す
    public void ClearRuntimeTextures()
    {
        foreach (RuntimeTextureState state in this.runtimeStatesByAssetName.Values)
        {
            this.RestoreOriginalTextureToItem(state.Binding, state);
            state.RuntimeTexture.Dispose();
        }

        this.runtimeStatesByAssetName.Clear();
        this.runtimeBindingsByKey.Clear();
        this.imageSourceService.Clear();
    }

    // 右クリックで現在ロケーション共有設定のメニューを開く
    public void TryOpenSettingsMenu(Point absoluteCursor)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
        {
            return;
        }

        Furniture? furniture = this.TryFindTargetFurniture(absoluteCursor);
        if (furniture is null)
        {
            return;
        }

        if (!this.targetRegistry.TryResolve(furniture, out TrueWindowTargetDefinition? definition) || definition is null)
        {
            return;
        }

        if (!definition.AllowRightClickConfig)
        {
            return;
        }

        string locationName = Game1.currentLocation.NameOrUniqueName;
        List<TrueWindowRuntimeContext> contexts = this.BuildCurrentLocationContexts();
        List<TrueWindowDisplayDefaults> displayDefaults = this.BuildDisplayDefaults(contexts);
        if (displayDefaults.Count == 0)
        {
            displayDefaults.Add(new TrueWindowDisplayDefaults(definition.ViewSettings.Clone(), definition.SpecifiedImagePath));
        }

        List<ImageSourceOption> imageOptions = this.locationDisplayService.BuildImageSourceOptions(displayDefaults);
        string? preferredSpecifiedImageSourceId = this.locationDisplayService.GetPreferredSpecifiedImageSourceId(displayDefaults, imageOptions);

        TrueWindowRuntimeSettings original = this.locationDisplayService.GetOrCreateRuntimeSettings(locationName, displayDefaults[0], preferredSpecifiedImageSourceId).Clone();
        this.locationDisplayService.NormalizeRuntimeSettingsSelection(original, imageOptions, preferredSpecifiedImageSourceId);
        TrueWindowRuntimeSettings editing = original.Clone();

        Game1.activeClickableMenu = new TrueWindowSettingsMenu(
            editing,
            this.config,
            this.helper.Translation,
            imageOptions,
            onChanged: updated =>
            {
                this.locationDisplayService.UpdateRuntimeSettings(locationName, updated);
                this.exteriorRenderService.Clear();
            },
            onCancel: () =>
            {
                this.locationDisplayService.UpdateRuntimeSettings(locationName, original);
                this.exteriorRenderService.Clear();
            });
    }

    // ロケーションの処理状態を更新する
    private void RefreshLocationState(GameLocation location, bool forceScan)
    {
        this.monitoredLocationName = location.NameOrUniqueName;

        if (this.IsOutdoorLocation(location))
        {
            this.processingMode = LocationProcessingMode.OutdoorSkip;
            this.pendingProbeTicks = 0;
            this.lastProbeTick = (ulong)Game1.ticks;
            this.exteriorRenderService.Clear();
            this.ResetAllRuntimeTexturesToBase();
            this.RestoreLocationFurnitureSourceRects(location);
            return;
        }

        this.lastProbeTick = (ulong)Game1.ticks;
        this.pendingProbeTicks = 0;

        if (!forceScan)
        {
            return;
        }

        if (this.HasTrueWindowFurniture(location))
        {
            this.processingMode = LocationProcessingMode.IndoorActive;
            return;
        }

        this.processingMode = LocationProcessingMode.IndoorDormant;
        this.exteriorRenderService.Clear();
        this.ResetAllRuntimeTexturesToBase();
        this.RestoreLocationFurnitureSourceRects(location);
    }

    // 屋外ロケーションか判定する
    private bool IsOutdoorLocation(GameLocation location)
    {
        return location.IsOutdoors;
    }

    // TrueWindow 家具があるか調べる
    private bool HasTrueWindowFurniture(GameLocation location)
    {
        foreach (Furniture furniture in location.furniture)
        {
            if (!this.targetRegistry.TryResolve(furniture, out TrueWindowTargetDefinition? definition) || definition is null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // ロケーション共有設定に渡す既定値一覧を作る
    private List<TrueWindowDisplayDefaults> BuildDisplayDefaults(IEnumerable<TrueWindowRuntimeContext> contexts)
    {
        return contexts
            .OrderBy(static p => p.Furniture.TileLocation.Y)
            .ThenBy(static p => p.Furniture.TileLocation.X)
            .Select(static p => new TrueWindowDisplayDefaults(p.Definition.ViewSettings.Clone(), p.Definition.SpecifiedImagePath))
            .ToList();
    }

    // 現在ロケーションの描画コンテキストを作る
    private List<TrueWindowRuntimeContext> BuildCurrentLocationContexts()
    {
        List<TrueWindowRuntimeContext> contexts = new();

        foreach (Furniture furniture in this.GetCurrentLocationFurniture())
        {
            if (!this.TryBuildContext(furniture, out TrueWindowRuntimeContext? context) || context is null)
            {
                continue;
            }

            contexts.Add(context);
        }

        return contexts;
    }

    // 家具から描画コンテキストを作る
    private bool TryBuildContext(Furniture furniture, out TrueWindowRuntimeContext? context)
    {
        context = null;

        if (!this.targetRegistry.TryResolve(furniture, out TrueWindowTargetDefinition? definition) || definition is null)
        {
            return false;
        }

        string normalizedItemId = this.NormalizeItemId(furniture.ItemId);
        string runtimeBindingKey = $"{normalizedItemId}|{definition.TrueWindowId}";

        if (!this.runtimeBindingsByKey.TryGetValue(runtimeBindingKey, out RuntimeTextureBinding? binding))
        {
            ParsedItemData itemDataFallback = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
            string runtimeAssetName = this.BuildRuntimeAssetName(normalizedItemId, definition);
            binding = new RuntimeTextureBinding(normalizedItemId, definition.TrueWindowId, itemDataFallback.TextureName, runtimeAssetName);
            this.runtimeBindingsByKey[runtimeBindingKey] = binding;
        }

        Texture2D originalTexture = this.LoadOriginalTexture(binding.OriginalTextureAssetName);
        Rectangle sourceRect = this.TryGetSourceRect(furniture, originalTexture, Furniture.GetDefaultSourceRect(binding.ItemId, originalTexture));
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return false;
        }

        if (!this.TryParseColor(definition.ReservedColor, out Color reservedColor))
        {
            return false;
        }

        string maskKey = $"{binding.OriginalTextureAssetName}|{sourceRect.X}|{sourceRect.Y}|{sourceRect.Width}|{sourceRect.Height}|{definition.ReservedColor}";
        TrueWindowMaskData mask = this.maskCache.GetOrCreate(maskKey, originalTexture, sourceRect, reservedColor);
        if (!mask.HasMask)
        {
            return false;
        }

        Rectangle worldBounds = this.GetApproximateWorldBounds(furniture, sourceRect);
        context = new TrueWindowRuntimeContext(furniture, definition, binding, sourceRect, mask, worldBounds, reservedColor);


        return true;
    }

    // ランタイム atlas を取得または作成する
    private RuntimeTextureState GetOrCreateRuntimeTexture(RuntimeTextureBinding binding)
    {
        if (this.runtimeStatesByAssetName.TryGetValue(binding.RuntimeAssetName, out RuntimeTextureState? existing))
        {
            return existing;
        }

        Texture2D originalTexture = this.LoadOriginalTexture(binding.OriginalTextureAssetName);
        Rectangle defaultSourceRect = Furniture.GetDefaultSourceRect(binding.ItemId, originalTexture);
        int slotWidth = Math.Max(1, defaultSourceRect.Width);
        int atlasWidth = originalTexture.Width + (slotWidth * MaxRuntimeSlotsPerTexture);
        int atlasHeight = Math.Max(originalTexture.Height, defaultSourceRect.Height);

        Color[] originalPixels = new Color[originalTexture.Width * originalTexture.Height];
        originalTexture.GetData(originalPixels);

        Texture2D runtimeTexture = new(Game1.graphics.GraphicsDevice, atlasWidth, atlasHeight, false, SurfaceFormat.Color);
        Color[] baseAtlasPixels = new Color[atlasWidth * atlasHeight];
        this.CopyRegion(
            originalPixels,
            originalTexture.Width,
            originalTexture.Height,
            new Rectangle(0, 0, originalTexture.Width, originalTexture.Height),
            baseAtlasPixels,
            atlasWidth,
            atlasHeight,
            Point.Zero);
        runtimeTexture.SetData(baseAtlasPixels);

        RuntimeTextureState created = new(
            binding,
            runtimeTexture,
            originalTexture,
            originalPixels,
            baseAtlasPixels,
            originalTexture.Width,
            originalTexture.Height,
            atlasWidth,
            atlasHeight,
            slotWidth);

        this.runtimeStatesByAssetName[binding.RuntimeAssetName] = created;

        return created;
    }

    // atlas を元画像状態へ戻す
    private void ResetAllRuntimeTexturesToBase()
    {
        foreach (RuntimeTextureState state in this.runtimeStatesByAssetName.Values)
        {
            this.ResetRuntimeTextureToBase(state);
        }
    }

    private void ResetRuntimeTextureToBase(RuntimeTextureState state)
    {
        if (state.BaseAtlasPixels.AsSpan().SequenceEqual(state.UploadedPixels)) return;
        state.RuntimeTexture.SetData(state.BaseAtlasPixels);
        Array.Copy(state.BaseAtlasPixels, state.UploadedPixels, state.BaseAtlasPixels.Length);
    }

    // スロットへ外景を合成する
    private void CompositeExteriorIntoSlot(
        Color[] atlasPixels,
        int atlasWidth,
        int atlasHeight,
        Rectangle slotRect,
        TrueWindowRuntimeContext context,
        TrueWindowRenderSnapshot snapshot,
        SnapshotPixelData snapshotPixels)
    {
        // 予約色として検出した行だけを外景へ置き換える
        foreach (TrueWindowMaskRow row in context.Mask.Rows)
        {
            for (int x = 0; x < row.Width; x++)
            {
                int sourcePixelX = row.StartX + x;
                int sourcePixelY = row.Y;
                Color sampled = this.SampleExteriorColor(snapshot, snapshotPixels, context.WorldBounds, sourcePixelX, sourcePixelY);

                int atlasX = slotRect.X + sourcePixelX;
                int atlasY = slotRect.Y + sourcePixelY;
                if (atlasX < 0 || atlasX >= atlasWidth || atlasY < 0 || atlasY >= atlasHeight)
                {
                    continue;
                }

                atlasPixels[(atlasY * atlasWidth) + atlasX] = sampled;
            }
        }
    }

    // 指定画像をスロットへ合成する
    private void CompositeSpecifiedImageIntoSlot(
        Color[] atlasPixels,
        int atlasWidth,
        int atlasHeight,
        Rectangle slotRect,
        TrueWindowRuntimeContext context,
        SpecifiedImageData specifiedImage,
        TrueWindowRuntimeSettings runtimeSettings)
    {
        int roomWidth = Math.Max(1, Game1.currentLocation.map.DisplayWidth);
        int roomHeight = Math.Max(1, Game1.currentLocation.map.DisplayHeight);
        Vector4 imageViewport = this.viewTransformService.BuildSpecifiedImageViewport(runtimeSettings.ViewSettings, roomWidth, roomHeight);

        foreach (TrueWindowMaskRow row in context.Mask.Rows)
        {
            for (int x = 0; x < row.Width; x++)
            {
                int sourcePixelX = row.StartX + x;
                int sourcePixelY = row.Y;
                Color sampled = this.SampleSpecifiedImageColor(specifiedImage, imageViewport, context.WorldBounds, roomWidth, roomHeight, sourcePixelX, sourcePixelY);

                int atlasX = slotRect.X + sourcePixelX;
                int atlasY = slotRect.Y + sourcePixelY;
                if (atlasX < 0 || atlasX >= atlasWidth || atlasY < 0 || atlasY >= atlasHeight)
                {
                    continue;
                }

                atlasPixels[(atlasY * atlasWidth) + atlasX] = sampled;
            }
        }
    }



    // 指定画像の色をサンプリングする
    private Color SampleSpecifiedImageColor(
        SpecifiedImageData specifiedImage,
        Vector4 imageViewport,
        Rectangle worldBounds,
        int roomWidth,
        int roomHeight,
        int sourcePixelX,
        int sourcePixelY)
    {
        if (specifiedImage.Width <= 0 || specifiedImage.Height <= 0)
        {
            return Color.Transparent;
        }

        float worldX = worldBounds.X + (sourcePixelX * 4f) + 2f;
        float worldY = worldBounds.Y + (sourcePixelY * 4f) + 2f;

        float roomU = worldX / roomWidth;
        float roomV = worldY / roomHeight;
        float viewportX = imageViewport.X + (roomU * imageViewport.Z);
        float viewportY = imageViewport.Y + (roomV * imageViewport.W);
        float imageU = viewportX / roomWidth;
        float imageV = viewportY / roomHeight;

        if (imageU < 0f || imageU >= 1f || imageV < 0f || imageV >= 1f)
        {
            return Color.Transparent;
        }

        int sampleX = Math.Clamp((int)MathF.Floor(imageU * specifiedImage.Width), 0, specifiedImage.Width - 1);
        int sampleY = Math.Clamp((int)MathF.Floor(imageV * specifiedImage.Height), 0, specifiedImage.Height - 1);
        return specifiedImage.Pixels[(sampleY * specifiedImage.Width) + sampleX];
    }

    // 外景色をサンプリングする
    private Color SampleExteriorColor(TrueWindowRenderSnapshot snapshot, SnapshotPixelData snapshotPixels, Rectangle worldBounds, int sourcePixelX, int sourcePixelY)
    {
        if (snapshot.RoomWidth <= 0 || snapshot.RoomHeight <= 0 || snapshot.Target.Width <= 0 || snapshot.Target.Height <= 0)
        {
            return Color.Transparent;
        }

        float worldX = worldBounds.X + (sourcePixelX * 4f) + 2f;
        float worldY = worldBounds.Y + (sourcePixelY * 4f) + 2f;

        float u = worldX / snapshot.RoomWidth;
        float v = worldY / snapshot.RoomHeight;

        int sampleX = Math.Clamp((int)MathF.Floor(u * snapshot.Target.Width), 0, snapshot.Target.Width - 1);
        int sampleY = Math.Clamp((int)MathF.Floor(v * snapshot.Target.Height), 0, snapshot.Target.Height - 1);
        Rectangle bounds = snapshotPixels.Bounds;
        if (!bounds.Contains(sampleX, sampleY)) return Color.Transparent;
        return snapshotPixels.Pixels[((sampleY - bounds.Y) * bounds.Width) + sampleX - bounds.X];
    }

    // 窓が実際にサンプルする範囲だけを読み戻す。画質や外景の描画頻度は変更しない。
    private static Rectangle GetReadbackBounds(List<TrueWindowRuntimeContext> contexts, bool renderLightGlows, TrueWindowRenderSnapshot snapshot)
    {
        Rectangle world = Rectangle.Empty;
        bool found = false;
        foreach (TrueWindowRuntimeContext context in contexts)
        {
            if (context.Definition.RenderLightGlows != renderLightGlows) continue;
            world = found ? Rectangle.Union(world, context.WorldBounds) : context.WorldBounds;
            found = true;
        }
        ReadbackRegion region = ReadbackRegion.FromWorld(world.Left, world.Top, world.Right, world.Bottom,
            snapshot.RoomWidth, snapshot.RoomHeight, snapshot.Target.Width, snapshot.Target.Height);
        return new(region.X, region.Y, region.Width, region.Height);
    }

    // 領域をコピーする
    private void CopyRegion(
        Color[] sourcePixels,
        int sourceWidth,
        int sourceHeight,
        Rectangle sourceRect,
        Color[] destinationPixels,
        int destinationWidth,
        int destinationHeight,
        Point destinationPoint)
    {
        for (int y = 0; y < sourceRect.Height; y++)
        {
            int sourceY = sourceRect.Y + y;
            int destinationY = destinationPoint.Y + y;
            if (sourceY < 0 || sourceY >= sourceHeight || destinationY < 0 || destinationY >= destinationHeight)
            {
                continue;
            }

            for (int x = 0; x < sourceRect.Width; x++)
            {
                int sourceX = sourceRect.X + x;
                int destinationX = destinationPoint.X + x;
                if (sourceX < 0 || sourceX >= sourceWidth || destinationX < 0 || destinationX >= destinationWidth)
                {
                    continue;
                }

                destinationPixels[(destinationY * destinationWidth) + destinationX] = sourcePixels[(sourceY * sourceWidth) + sourceX];
            }
        }
    }


    // 元テクスチャを読み込む
    private Texture2D LoadOriginalTexture(string assetName)
    {
        return this.helper.GameContent.Load<Texture2D>(assetName);
    }

    // ParsedItemData の private Texture をランタイム atlas へ差し替える
    private void AssignRuntimeTextureToItem(RuntimeTextureBinding binding, RuntimeTextureState state)
    {
        ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem("(F)" + binding.ItemId);

        this.SetParsedItemTexture(itemData, state.RuntimeTexture);
    }

    // タイトル戻り時などに、ParsedItemData の private Texture を元へ戻す
    private void RestoreOriginalTextureToItem(RuntimeTextureBinding binding, RuntimeTextureState state)
    {
        ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem("(F)" + binding.ItemId);
        this.SetParsedItemTexture(itemData, state.OriginalTexture);
    }

    // ParsedItemData のテクスチャを差し替える
    private void SetParsedItemTexture(ParsedItemData itemData, Texture2D texture)
    {
        typeof(ParsedItemData).GetField("Texture", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(itemData, texture);
        typeof(ParsedItemData).GetField("LoadedTexture", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(itemData, true);
        Rectangle defaultSource = itemData.ItemType.GetSourceRect(itemData, texture, itemData.SpriteIndex);
        typeof(ParsedItemData).GetField("DefaultSourceRect", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(itemData, defaultSource);
    }

    // 家具自身が持つ光源を制御する
    private void ApplyFurnitureLightGlowSetting(Furniture furniture, bool renderLightGlows)
    {
        if (furniture.furniture_type.Value != Furniture.window)
        {
            return;
        }

        GameLocation? location = furniture.Location;
        if (location is null)
        {
            return;
        }

        // 設定で無効にしている場合は常に光源をOFFにする
        if (!renderLightGlows)
        {
            furniture.RemoveLightGlow();
            return;
        }

        if (location.IsRainingHere() || furniture.timeToTurnOnLights())
        {
            furniture.RemoveLightGlow();
            return;
        }

        furniture.AddLightGlow();
        location.lightGlowLayerCache.Clear();
    }

    // 指定ロケーションの sourceRect を戻す
    private void RestoreLocationFurnitureSourceRects(GameLocation location)
    {
        foreach (Furniture furniture in location.furniture)
        {
            if (!this.targetRegistry.TryResolve(furniture, out TrueWindowTargetDefinition? definition) || definition is null)
            {
                continue;
            }

            furniture.sourceRect.Value = furniture.defaultSourceRect.Value;
            this.ResetVanillaSourceIndexOffset(furniture);
        }
    }

    // バニラの sourceIndexOffset private field を 0 へ戻す
    private void ResetVanillaSourceIndexOffset(Furniture furniture)
    {
        if (SourceIndexOffsetField?.GetValue(furniture) is NetInt sourceIndexOffset)
        {
            sourceIndexOffset.Value = 0;
        }
    }

    // 対象家具を探す
    private Furniture? TryFindTargetFurniture(Point absoluteCursor)
    {
        foreach (Furniture furniture in this.GetCurrentLocationFurniture())
        {
            if (!this.targetRegistry.TryResolve(furniture, out _))
            {
                continue;
            }

            if (this.GetFurnitureClickBounds(furniture).Contains(absoluteCursor))
            {
                return furniture;
            }
        }

        return null;
    }

    // 現在ロケーションの家具を列挙する
    private IEnumerable<Furniture> GetCurrentLocationFurniture()
    {
        foreach (Furniture furniture in Game1.currentLocation.furniture)
        {
            yield return furniture;
        }
    }

    // 同一ロケーション内では、共有設定とロケーション状態だけで外景を決める
    private string BuildLocationRenderKey(TrueWindowRuntimeSettings runtimeSettings, bool renderLightGlows)
    {
        return string.Join('|', new[]
        {
            Game1.currentLocation.NameOrUniqueName,
            Game1.currentLocation.map.DisplayWidth.ToString(),
            Game1.currentLocation.map.DisplayHeight.ToString(),
            runtimeSettings.ViewSettings.OffsetX.ToString(),
            runtimeSettings.ViewSettings.OffsetY.ToString(),
            runtimeSettings.ViewSettings.Zoom.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            runtimeSettings.ViewSettings.ParallaxStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            renderLightGlows ? "1" : "0",
        });
    }

    // 家具描画の近似矩形をワールド座標で作る
    private Rectangle GetApproximateWorldBounds(Furniture furniture, Rectangle sourceRect)
    {
        Rectangle clickBounds = this.GetFurnitureClickBounds(furniture);
        int drawWidth = sourceRect.Width * 4;
        int drawHeight = sourceRect.Height * 4;
        int drawX = clickBounds.X;
        int drawY = clickBounds.Bottom - drawHeight;
        return new Rectangle(drawX, drawY, drawWidth, drawHeight);
    }

    // 家具のクリック矩形を取得する
    private Rectangle GetFurnitureClickBounds(Furniture furniture)
    {
        try
        {
            return furniture.GetBoundingBox();
        }
        catch
        {
            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
            Rectangle sourceRect = itemData.GetSourceRect();
            return new Rectangle((int)furniture.TileLocation.X * Game1.tileSize, (int)furniture.TileLocation.Y * Game1.tileSize, sourceRect.Width * 4, sourceRect.Height * 4);
        }
    }

    // sourceRect を安全に取得する
    private Rectangle TryGetSourceRect(Furniture furniture, Texture2D texture, Rectangle fallback)
    {
        Rectangle sourceRect;

        try
        {
            sourceRect = furniture.sourceRect.Value;
        }
        catch
        {
            sourceRect = fallback;
        }

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return fallback;
        }

        if (sourceRect.X < 0 || sourceRect.Y < 0)
        {
            return fallback;
        }

        if (sourceRect.Right > texture.Width || sourceRect.Bottom > texture.Height)
        {
            return fallback;
        }

        return sourceRect;
    }

    // 文字列を Color へ変換する
    private bool TryParseColor(string rawColor, out Color color)
    {
        color = Color.White;
        string value = rawColor.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        try
        {
            if (value.Length == 6)
            {
                byte r = Convert.ToByte(value[..2], 16);
                byte g = Convert.ToByte(value[2..4], 16);
                byte b = Convert.ToByte(value[4..6], 16);
                color = new Color(r, g, b, (byte)255);
                return true;
            }

            if (value.Length == 8)
            {
                byte a = Convert.ToByte(value[..2], 16);
                byte r = Convert.ToByte(value[2..4], 16);
                byte g = Convert.ToByte(value[4..6], 16);
                byte b = Convert.ToByte(value[6..8], 16);
                color = new Color(r, g, b, a);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }


    // 家具 itemId を正規化する
    private string NormalizeItemId(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return "unknown";
        }

        string trimmed = itemId.Trim();
        return trimmed.StartsWith("(F)", StringComparison.OrdinalIgnoreCase)
            ? trimmed[3..]
            : trimmed;
    }

    // Data/Furniture の Texture フィールドは / 区切りなので、アセット名に / を含めない
    private string BuildRuntimeAssetName(string itemId, TrueWindowTargetDefinition definition)
    {
        string safeId = $"{itemId}.{definition.TrueWindowId}"
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace("/", ".")
            .Replace("\\", ".")
            .Replace(":", ".")
            .Replace(" ", ".");

        return $"TrueWindow.Runtime.{safeId}";
    }

}

using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TrueWindow.Integrations;
using TrueWindow.Services;

namespace TrueWindow;

// ----------------------------
// MOD エントリポイント
// ----------------------------
public sealed class ModEntry : Mod
{
    private ModConfig config = null!;
    private ModConfig defaultConfig = null!;
    private GmcmIntegration gmcmIntegration = null!;
    private TrueWindowTargetRegistry targetRegistry = null!;
    private TrueWindowInstanceRepository instanceRepository = null!;
    private TrueWindowMaskCache maskCache = null!;
    private ParentBuildingLocator parentBuildingLocator = null!;
    private TrueWindowViewTransformService viewTransformService = null!;
    private TrueWindowLocationDisplayService locationDisplayService = null!;
    private TrueWindowImageSourceService imageSourceService = null!;
    private TrueWindowExteriorRenderService exteriorRenderService = null!;
    private TrueWindowFurnitureService furnitureService = null!;

    // ----------------------------
    // MOD の初期化を行う
    // ----------------------------
    public override void Entry(IModHelper helper)
    {
        this.defaultConfig = new ModConfig();
        this.config = helper.ReadConfig<ModConfig>();
        this.gmcmIntegration = new GmcmIntegration(
            helper,
            this.ModManifest,
            helper.Translation,
            () => this.config,
            this.ResetConfig,
            this.SaveConfig);
        this.targetRegistry = new TrueWindowTargetRegistry(helper);
        this.instanceRepository = new TrueWindowInstanceRepository(helper);
        this.maskCache = new TrueWindowMaskCache();
        this.parentBuildingLocator = new ParentBuildingLocator();
        this.viewTransformService = new TrueWindowViewTransformService(this.config);
        this.locationDisplayService = new TrueWindowLocationDisplayService(helper, this.instanceRepository);
        this.imageSourceService = new TrueWindowImageSourceService(helper);
        this.exteriorRenderService = new TrueWindowExteriorRenderService(this.config, this.parentBuildingLocator, this.viewTransformService);
        this.furnitureService = new TrueWindowFurnitureService(
            helper,
            this.config,
            this.targetRegistry,
            this.locationDisplayService,
            this.maskCache,
            this.exteriorRenderService,
            this.imageSourceService,
            this.viewTransformService);

        this.targetRegistry.Reload();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Display.RenderingWorld += this.OnRenderingWorld;
    }

    // ----------------------------
    // ゲーム起動完了後の初期化を行う
    // ----------------------------
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.targetRegistry.Reload();
        this.gmcmIntegration.RegisterOrReload();
    }

    // ----------------------------
    // セーブ読込後の状態を初期化する
    // ----------------------------
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.targetRegistry.Reload();
        this.instanceRepository.Load();
        this.exteriorRenderService.Clear();
        this.imageSourceService.Clear();
        this.maskCache.Clear();
        this.furnitureService.RefreshCurrentLocationState(forceScan: true);
    }

    // ----------------------------
    // セーブ前に一時状態を戻して保存する
    // ----------------------------
    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.furnitureService.RestoreLoadedFurnitureSourceRects();
        this.instanceRepository.Save();
    }

    // ----------------------------
    // タイトルへ戻るときにキャッシュを片付ける
    // ----------------------------
    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        if (Context.IsWorldReady)
        {
            this.furnitureService.RestoreLoadedFurnitureSourceRects();
        }

        this.exteriorRenderService.Clear();
        this.maskCache.Clear();
        this.furnitureService.ClearRuntimeTextures();
        this.furnitureService.ResetMonitoringState();
    }

    // ----------------------------
    // 毎 tick の監視処理を進める
    // ----------------------------
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || this.exteriorRenderService.IsOffscreenRendering)
        {
            return;
        }

        this.furnitureService.UpdateMonitoring(e.Ticks);
    }

    // ----------------------------
    // ワープ時に監視対象ロケーションを切り替える
    // ----------------------------
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.furnitureService.OnWarped(e.OldLocation, e.NewLocation);
    }

    // ----------------------------
    // ワールド描画直前に対象家具の外景を準備する
    // ----------------------------
    private void OnRenderingWorld(object? sender, RenderingWorldEventArgs e)
    {
        if (!Context.IsWorldReady || !this.furnitureService.ShouldProcessCurrentLocation())
        {
            return;
        }

        this.furnitureService.PrepareCurrentLocationTargets();
    }

    // ----------------------------
    // 入力から家具変化監視と設定メニュー起動を行う
    // ----------------------------
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || this.exteriorRenderService.IsOffscreenRendering)
        {
            return;
        }

        if (e.Button.IsActionButton() || e.Button.IsUseToolButton())
        {
            this.furnitureService.NotifyPossibleFurnitureChange();
        }

        if (Game1.activeClickableMenu is not null || !e.Button.IsActionButton())
        {
            return;
        }

        Point cursor = new((int)e.Cursor.AbsolutePixels.X, (int)e.Cursor.AbsolutePixels.Y);
        this.furnitureService.TryOpenSettingsMenu(cursor);
    }

    // ----------------------------
    // 設定を既定値へ戻す
    // ----------------------------
    private void ResetConfig()
    {
        this.config = this.defaultConfig.Clone();
    }

    // ----------------------------
    // 現在の設定を書き出す
    // ----------------------------
    private void SaveConfig()
    {
        this.Helper.WriteConfig(this.config);
    }
}

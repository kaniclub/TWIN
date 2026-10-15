using StardewModdingAPI;

namespace TrueWindow.Integrations;

// ----------------------------
// Generic Mod Config Menu 連携
// ----------------------------
internal sealed class GmcmIntegration
{
    private const string GmcmId = "spacechase0.GenericModConfigMenu";

    private readonly IModHelper helper;
    private readonly IManifest manifest;
    private readonly ITranslationHelper translation;
    private readonly Func<ModConfig> getConfig;
    private readonly Action resetConfig;
    private readonly Action saveConfig;

    // GMCM 連携を初期化する
    public GmcmIntegration(
        IModHelper helper,
        IManifest manifest,
        ITranslationHelper translation,
        Func<ModConfig> getConfig,
        Action resetConfig,
        Action saveConfig)
    {
        this.helper = helper;
        this.manifest = manifest;
        this.translation = translation;
        this.getConfig = getConfig;
        this.resetConfig = resetConfig;
        this.saveConfig = saveConfig;
    }

    // GMCM があれば設定画面へ登録する
    public void RegisterOrReload()
    {
        if (!this.helper.ModRegistry.IsLoaded(GmcmId))
        {
            return;
        }

        IGenericModConfigMenuApi? api = this.helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmId);
        if (api is null)
        {
            return;
        }

        api.Unregister(this.manifest);
        api.Register(this.manifest, this.resetConfig, this.saveConfig);

        this.AddOptions(api);
    }

    // GMCM へ TrueWindow の通常設定を追加する
    private void AddOptions(IGenericModConfigMenuApi api)
    {
        api.AddNumberOption(
            this.manifest,
            () => this.getConfig().RenderThrottleFrames,
            value => this.getConfig().RenderThrottleFrames = Math.Max(1, value),
            () => this.T("config.render_throttle_frames.name"),
            () => this.T("config.render_throttle_frames.description"),
            min: 1,
            max: 60,
            interval: 1);

        api.AddNumberOption(
            this.manifest,
            () => this.getConfig().IndoorProbeIntervalTicks,
            value => this.getConfig().IndoorProbeIntervalTicks = Math.Max(1, value),
            () => this.T("config.indoor_probe_interval_ticks.name"),
            () => this.T("config.indoor_probe_interval_ticks.description"),
            min: 1,
            max: 300,
            interval: 1);
    }

    private string T(string key)
    {
        return this.translation.Get(key).ToString();
    }
}
